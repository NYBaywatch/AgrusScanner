# Build script for Agrus Scanner MSI installer
# Prerequisites: .NET 9 SDK, WiX Toolset v5 (dotnet tool install --global wix)
# Optional: dotnet sign tool (dotnet tool install --global sign --prerelease) + Azure Trusted Signing account

$ErrorActionPreference = "Stop"

$repoRoot = $PSScriptRoot
$publishDir = Join-Path $repoRoot "Installer\publish"
$installerProj = Join-Path $repoRoot "Installer"

Write-Host "=== Agrus Scanner Installer Build ===" -ForegroundColor Cyan

# Check prerequisites
$wixInstalled = dotnet tool list --global 2>$null | Select-String "wix"
if (-not $wixInstalled) {
    throw "WiX Toolset v5 not found. Install with: dotnet tool install --global wix"
}

# Clean previous output
Write-Host "`n[1/3] Cleaning previous build output..." -ForegroundColor Yellow
if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
$binDir = Join-Path $installerProj "bin"
$objDir = Join-Path $installerProj "obj"
if (Test-Path $binDir) { Remove-Item $binDir -Recurse -Force }
if (Test-Path $objDir) { Remove-Item $objDir -Recurse -Force }

# Publish self-contained app
Write-Host "`n[2/3] Publishing self-contained app (win-x64)..." -ForegroundColor Yellow
dotnet publish "$repoRoot\AgrusScanner" -c Release -r win-x64 --self-contained -o $publishDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

# Build MSI
Write-Host "`n[3/3] Building MSI installer..." -ForegroundColor Yellow
dotnet build $installerProj -c Release
if ($LASTEXITCODE -ne 0) { throw "WiX build failed" }

# Report output
$msi = Get-ChildItem "$binDir\Release" -Filter "*.msi" -Recurse | Select-Object -First 1
if (-not $msi) {
    Write-Host "`nWarning: MSI file not found in expected location." -ForegroundColor Red
    exit 1
}

# Code signing (optional — requires Azure Trusted Signing setup)
# Install sign tool:  dotnet tool install --global sign --prerelease
# Azure resources needed: signing account + certificate profile
# See: https://learn.microsoft.com/en-us/azure/artifact-signing/
$signTool = Get-Command sign -ErrorAction SilentlyContinue
if ($signTool) {
    $tsEndpoint = "https://eus.codesigning.azure.net/"
    $tsAccount  = "agrussigning"
    $tsProfile  = "agrus-public"

    Write-Host "`n[4/4] Signing MSI with Azure Trusted Signing..." -ForegroundColor Yellow
    sign code trusted-signing $msi.FullName `
        --trusted-signing-endpoint $tsEndpoint `
        --trusted-signing-account $tsAccount `
        --trusted-signing-certificate-profile $tsProfile
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Warning: Signing failed. MSI built but unsigned." -ForegroundColor Red
    } else {
        Write-Host "MSI signed successfully." -ForegroundColor Green
    }
} else {
    Write-Host "`nNote: 'sign' tool not found - MSI will be unsigned." -ForegroundColor DarkYellow
    Write-Host "  Install with: dotnet tool install --global sign --prerelease" -ForegroundColor DarkYellow
}

Write-Host "`n=== Build Complete ===" -ForegroundColor Green
Write-Host "MSI: $($msi.FullName)" -ForegroundColor Green
Write-Host "Size: $([math]::Round($msi.Length / 1MB, 1)) MB" -ForegroundColor Green
