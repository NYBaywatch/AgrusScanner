# Generate custom installer images for Agrus Scanner
# Banner: 493x58 (top of wizard pages)
# Dialog: 493x312 (welcome/finish side panel)

Add-Type -AssemblyName System.Drawing

$installerDir = $PSScriptRoot

# --- Banner (493 x 58) ---
$bw = 493; $bh = 58
$banner = New-Object System.Drawing.Bitmap($bw, $bh)
$g = [System.Drawing.Graphics]::FromImage($banner)
$g.SmoothingMode = 'AntiAlias'
$g.TextRenderingHint = 'AntiAliasGridFit'

# Dark gradient background
$bgBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
    (New-Object System.Drawing.Point(0, 0)),
    (New-Object System.Drawing.Point($bw, 0)),
    [System.Drawing.Color]::FromArgb(10, 10, 10),
    [System.Drawing.Color]::FromArgb(20, 30, 20)
)
$g.FillRectangle($bgBrush, 0, 0, $bw, $bh)

# Subtle green accent line at bottom
$accentPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(0, 255, 65), 2)
$g.DrawLine($accentPen, 0, ($bh - 2), $bw, ($bh - 2))

# Subtle grid pattern
$gridPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(15, 0, 255, 65), 1)
for ($x = 0; $x -lt $bw; $x += 20) { $g.DrawLine($gridPen, $x, 0, $x, $bh) }
for ($y = 0; $y -lt $bh; $y += 20) { $g.DrawLine($gridPen, 0, $y, $bw, $y) }

# "AGRUS" text - right aligned
$fontBig = New-Object System.Drawing.Font("Consolas", 22, [System.Drawing.FontStyle]::Bold)
$greenBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(0, 255, 65))
$textSize = $g.MeasureString("AGRUS", $fontBig)
$g.DrawString("AGRUS", $fontBig, $greenBrush, ($bw - $textSize.Width - 12), (($bh - $textSize.Height) / 2))

# "SCANNER" subtitle
$fontSm = New-Object System.Drawing.Font("Consolas", 9, [System.Drawing.FontStyle]::Regular)
$dimBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(100, 100, 100))
$subSize = $g.MeasureString("SCANNER", $fontSm)
$g.DrawString("SCANNER", $fontSm, $dimBrush, ($bw - $textSize.Width - 12 + ($textSize.Width - $subSize.Width)), ($bh / 2 + $textSize.Height / 2 - $subSize.Height + 2))

# Small crosshair/scan icon on the left
$iconPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(0, 170, 42), 2)
$cx = 30; $cy = $bh / 2
$g.DrawEllipse($iconPen, ($cx - 12), ($cy - 12), 24, 24)
$g.DrawLine($iconPen, ($cx - 18), $cy, ($cx + 18), $cy)
$g.DrawLine($iconPen, $cx, ($cy - 18), $cx, ($cy + 18))

$g.Dispose()
$bannerPath = Join-Path $installerDir "banner.bmp"
$banner.Save($bannerPath, [System.Drawing.Imaging.ImageFormat]::Bmp)
$banner.Dispose()
Write-Host "Created: $bannerPath"

# --- Dialog (493 x 312) ---
$dw = 493; $dh = 312
$dialog = New-Object System.Drawing.Bitmap($dw, $dh)
$g = [System.Drawing.Graphics]::FromImage($dialog)
$g.SmoothingMode = 'AntiAlias'
$g.TextRenderingHint = 'AntiAliasGridFit'

# Dark background
$bgBrush2 = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
    (New-Object System.Drawing.Point(0, 0)),
    (New-Object System.Drawing.Point(0, $dh)),
    [System.Drawing.Color]::FromArgb(10, 10, 10),
    [System.Drawing.Color]::FromArgb(5, 15, 8)
)
$g.FillRectangle($bgBrush2, 0, 0, $dw, $dh)

# Grid overlay
$gridPen2 = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(12, 0, 255, 65), 1)
for ($x = 0; $x -lt $dw; $x += 24) { $g.DrawLine($gridPen2, $x, 0, $x, $dh) }
for ($y = 0; $y -lt $dh; $y += 24) { $g.DrawLine($gridPen2, 0, $y, $dw, $y) }

# Large crosshair/scan icon centered
$iconPen2 = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(0, 200, 50), 2)
$cx2 = $dw / 2; $cy2 = 120
# Outer ring
$g.DrawEllipse($iconPen2, ($cx2 - 50), ($cy2 - 50), 100, 100)
# Inner ring
$innerPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(0, 255, 65), 2)
$g.DrawEllipse($innerPen, ($cx2 - 25), ($cy2 - 25), 50, 50)
# Crosshairs
$g.DrawLine($iconPen2, ($cx2 - 65), $cy2, ($cx2 + 65), $cy2)
$g.DrawLine($iconPen2, $cx2, ($cy2 - 65), $cx2, ($cy2 + 65))
# Corner brackets
$bracketPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(0, 170, 42), 2)
$bLen = 15
# Top-left
$g.DrawLine($bracketPen, ($cx2 - 55), ($cy2 - 55), ($cx2 - 55 + $bLen), ($cy2 - 55))
$g.DrawLine($bracketPen, ($cx2 - 55), ($cy2 - 55), ($cx2 - 55), ($cy2 - 55 + $bLen))
# Top-right
$g.DrawLine($bracketPen, ($cx2 + 55), ($cy2 - 55), ($cx2 + 55 - $bLen), ($cy2 - 55))
$g.DrawLine($bracketPen, ($cx2 + 55), ($cy2 - 55), ($cx2 + 55), ($cy2 - 55 + $bLen))
# Bottom-left
$g.DrawLine($bracketPen, ($cx2 - 55), ($cy2 + 55), ($cx2 - 55 + $bLen), ($cy2 + 55))
$g.DrawLine($bracketPen, ($cx2 - 55), ($cy2 + 55), ($cx2 - 55), ($cy2 + 55 - $bLen))
# Bottom-right
$g.DrawLine($bracketPen, ($cx2 + 55), ($cy2 + 55), ($cx2 + 55 - $bLen), ($cy2 + 55))
$g.DrawLine($bracketPen, ($cx2 + 55), ($cy2 + 55), ($cx2 + 55), ($cy2 + 55 - $bLen))

# "AGRUS" large text
$fontTitle = New-Object System.Drawing.Font("Consolas", 32, [System.Drawing.FontStyle]::Bold)
$greenBrush2 = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(0, 255, 65))
$titleSize = $g.MeasureString("AGRUS", $fontTitle)
$g.DrawString("AGRUS", $fontTitle, $greenBrush2, (($dw - $titleSize.Width) / 2), 195)

# "SCANNER" subtitle
$fontSub = New-Object System.Drawing.Font("Consolas", 14, [System.Drawing.FontStyle]::Regular)
$dimBrush2 = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(0, 170, 42))
$subSize2 = $g.MeasureString("SCANNER", $fontSub)
$g.DrawString("SCANNER", $fontSub, $dimBrush2, (($dw - $subSize2.Width) / 2), 238)

# Version text
$fontVer = New-Object System.Drawing.Font("Consolas", 9, [System.Drawing.FontStyle]::Regular)
$dimBrush3 = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(80, 80, 80))
$verSize = $g.MeasureString("Network Reconnaissance Tool", $fontVer)
$g.DrawString("Network Reconnaissance Tool", $fontVer, $dimBrush3, (($dw - $verSize.Width) / 2), 268)

# Green accent line at bottom
$g.DrawLine($accentPen, 0, ($dh - 2), $dw, ($dh - 2))

$g.Dispose()
$dialogPath = Join-Path $installerDir "dialog.bmp"
$dialog.Save($dialogPath, [System.Drawing.Imaging.ImageFormat]::Bmp)
$dialog.Dispose()
Write-Host "Created: $dialogPath"

# Cleanup
$bgBrush.Dispose(); $bgBrush2.Dispose()
$accentPen.Dispose(); $gridPen.Dispose(); $gridPen2.Dispose()
$iconPen.Dispose(); $iconPen2.Dispose(); $innerPen.Dispose(); $bracketPen.Dispose()
$fontBig.Dispose(); $fontSm.Dispose(); $fontTitle.Dispose(); $fontSub.Dispose(); $fontVer.Dispose()
$greenBrush.Dispose(); $dimBrush.Dispose(); $greenBrush2.Dispose(); $dimBrush2.Dispose(); $dimBrush3.Dispose()

Write-Host "`nDone! Banner and dialog images generated." -ForegroundColor Green
