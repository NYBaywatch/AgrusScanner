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
Write-Host "`n[1/4] Cleaning previous build output..." -ForegroundColor Yellow
if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
$binDir = Join-Path $installerProj "bin"
$objDir = Join-Path $installerProj "obj"
if (Test-Path $binDir) { Remove-Item $binDir -Recurse -Force }
if (Test-Path $objDir) { Remove-Item $objDir -Recurse -Force }

# Publish self-contained app
# ── Code signing (Azure Trusted Signing) ──────────────────────────────────────
# Signs the app's own binaries after publish (so the installed exe/dll are signed)
# and the MSI after packing. Local: `az login` + --azure-credential-type azure-cli.
# CI: AZURE_CLIENT_ID / AZURE_CLIENT_SECRET / AZURE_TENANT_ID env vars (environment credential).
# Install the tool with: dotnet tool install --global sign --prerelease
$signTool = Get-Command sign -ErrorAction SilentlyContinue
$tsEndpoint = if ($env:AGRUS_SIGNING_ENDPOINT) { $env:AGRUS_SIGNING_ENDPOINT } else { "https://eus.codesigning.azure.net/" }
$tsAccount  = if ($env:AGRUS_SIGNING_ACCOUNT) { $env:AGRUS_SIGNING_ACCOUNT } else { "agrussigning" }
$tsProfile  = if ($env:AGRUS_SIGNING_PROFILE) { $env:AGRUS_SIGNING_PROFILE } else { "agrus-public" }
$tsSubscription = $env:AGRUS_SIGNING_SUBSCRIPTION
# CI (AZURE_CLIENT_SECRET set): no --azure-credential-type, so the tool's DefaultAzureCredential reads the AZURE_* env vars.
$credType = if ($env:AZURE_CLIENT_SECRET) { "default" } else { "azure-cli" }
$canSign = $false
if (-not $signTool) {
    Write-Host "`nNote: 'sign' tool not found - output will be unsigned." -ForegroundColor DarkYellow
    Write-Host "  Install with: dotnet tool install --global sign --prerelease" -ForegroundColor DarkYellow
} elseif ($credType -eq "azure-cli" -and -not $tsSubscription) {
    Write-Host "`nWarning: AGRUS_SIGNING_SUBSCRIPTION env var not set - skipping signing." -ForegroundColor DarkYellow
    Write-Host "  Set env vars: AGRUS_SIGNING_SUBSCRIPTION, AGRUS_SIGNING_ENDPOINT, AGRUS_SIGNING_ACCOUNT, AGRUS_SIGNING_PROFILE" -ForegroundColor DarkYellow
} else {
    $canSign = $true
}

function Invoke-AgrusSign([string]$label, [string[]]$files) {
    if (-not $canSign) { return }
    Write-Host "`n[sign] $label ($($files.Count) file(s)) via Azure Trusted Signing..." -ForegroundColor Yellow
    $previousSub = $null
    if ($credType -eq "azure-cli") {
        $previousSub = (az account show --query id -o tsv 2>$null)
        az account set --subscription $tsSubscription 2>$null
    }
    try {
        foreach ($f in $files) {
            $signArgs = @("code", "artifact-signing", $f,
                "--artifact-signing-endpoint", $tsEndpoint,
                "--artifact-signing-account", $tsAccount,
                "--artifact-signing-certificate-profile", $tsProfile)
            if ($credType -ne "default") { $signArgs += @("--azure-credential-type", $credType) }
            & sign @signArgs
            if ($LASTEXITCODE -ne 0) { throw "Signing failed for $f" }
            $sig = Get-AuthenticodeSignature $f
            if ($sig.Status -ne "Valid") { throw "Signature verification failed for $f : $($sig.Status)" }
            Write-Host "  signed: $(Split-Path $f -Leaf)" -ForegroundColor Green
        }
    } finally {
        if ($previousSub) { az account set --subscription $previousSub 2>$null }
    }
}

# Publish
Write-Host "`n[2/4] Publishing self-contained app (win-x64)..." -ForegroundColor Yellow
dotnet publish "$repoRoot\AgrusScanner" -c Release -r win-x64 --self-contained -o $publishDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

# Sign our own binaries (not the .NET runtime, which Microsoft already signs)
Invoke-AgrusSign "App binaries" @("$publishDir\AgrusScanner.exe", "$publishDir\AgrusScanner.dll")

# Build MSI
Write-Host "`n[3/4] Building MSI installer..." -ForegroundColor Yellow
dotnet build $installerProj -c Release
if ($LASTEXITCODE -ne 0) { throw "WiX build failed" }

# Report output
$msi = Get-ChildItem "$binDir\Release" -Filter "*.msi" -Recurse | Select-Object -First 1
if (-not $msi) {
    Write-Host "`nWarning: MSI file not found in expected location." -ForegroundColor Red
    exit 1
}

Write-Host "`n[4/4] Signing MSI..." -ForegroundColor Yellow
Invoke-AgrusSign "MSI" @($msi.FullName)
if (-not $canSign) { Write-Host "MSI built but UNSIGNED." -ForegroundColor DarkYellow }

Write-Host "`n=== Build Complete ===" -ForegroundColor Green
Write-Host "MSI: $($msi.FullName)" -ForegroundColor Green
Write-Host "Size: $([math]::Round($msi.Length / 1MB, 1)) MB" -ForegroundColor Green
