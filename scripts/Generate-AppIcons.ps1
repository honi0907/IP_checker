#Requires -Version 5.1
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Drawing

$repoRoot = Split-Path -Parent $PSScriptRoot
$assetsDir = Join-Path $repoRoot 'IPChecker\Assets'
New-Item -ItemType Directory -Force -Path $assetsDir | Out-Null

function New-Color {
    param([byte]$R, [byte]$G, [byte]$B, [byte]$A = 255)
    return [System.Drawing.Color]::FromArgb($A, $R, $G, $B)
}

function New-LinearBrush {
    param(
        [System.Drawing.Rectangle]$Rect,
        [System.Drawing.Color]$Top,
        [System.Drawing.Color]$Bottom
    )
    return New-Object System.Drawing.Drawing2D.LinearGradientBrush($Rect, $Top, $Bottom, 90)
}

function Set-Quality {
    param([System.Drawing.Graphics]$G)
    $G.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $G.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
    $G.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $G.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
}

function Draw-RoundedRect {
    param(
        [System.Drawing.Graphics]$G,
        [System.Drawing.Brush]$Brush,
        [int]$X,
        [int]$Y,
        [int]$W,
        [int]$H,
        [int]$Radius
    )
    $d = $Radius * 2
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddArc($X, $Y, $d, $d, 180, 90)
    $path.AddArc($X + $W - $d, $Y, $d, $d, 270, 90)
    $path.AddArc($X + $W - $d, $Y + $H - $d, $d, $d, 0, 90)
    $path.AddArc($X, $Y + $H - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    $G.FillPath($Brush, $path)
    $path.Dispose()
}

function Draw-NetworkGlyph {
    param(
        [System.Drawing.Graphics]$G,
        [int]$CenterX,
        [int]$TopY,
        [int]$Scale,
        [System.Drawing.Color]$Accent
    )

    $node = [Math]::Max(2, [int](4 * $Scale))
    $gap = [Math]::Max(4, [int](10 * $Scale))
    $y = $TopY + [int](6 * $Scale)
    $leftX = $CenterX - $gap
    $rightX = $CenterX + $gap
    $brush = New-Object System.Drawing.SolidBrush($Accent)
    $pen = New-Object System.Drawing.Pen($Accent, [Math]::Max(1, [int](1.6 * $Scale)))
    $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round

    $G.DrawLine($pen, $leftX, $y, $CenterX, $y - [int](8 * $Scale))
    $G.DrawLine($pen, $rightX, $y, $CenterX, $y - [int](8 * $Scale))
    $G.DrawLine($pen, $leftX, $y, $rightX, $y)

    foreach ($x in @($leftX, $CenterX, $rightX)) {
        $G.FillEllipse($brush, $x - $node, $y - $node, $node * 2, $node * 2)
    }

    $brush.Dispose()
    $pen.Dispose()
}

function New-AppBitmap {
    param(
        [int]$Size,
        [Nullable[System.Drawing.Color]]$Accent = $null
    )

    $bmp = New-Object System.Drawing.Bitmap $Size, $Size
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    Set-Quality $g
    $g.Clear([System.Drawing.Color]::Transparent)

    $pad = [Math]::Max(1, [int]($Size * 0.08))
    $rect = New-Object System.Drawing.Rectangle $pad, $pad, ($Size - $pad * 2), ($Size - $pad * 2)
    $radius = [Math]::Max(2, [int]($Size * 0.18))

    $bgTop = New-Color 15 23 42
    $bgBottom = New-Color 30 58 95
    $bgBrush = New-LinearBrush $rect $bgTop $bgBottom
    Draw-RoundedRect $g $bgBrush $rect.X $rect.Y $rect.Width $rect.Height $radius
    $bgBrush.Dispose()

    $accent = if ($Accent) { $Accent.Value } else { (New-Color 56 189 248) }
    $accentPen = New-Object System.Drawing.Pen($accent, [Math]::Max(1, [int]($Size * 0.03)))
    $g.DrawPath($accentPen, (Get-RoundedRectPath $rect.X $rect.Y $rect.Width $rect.Height $radius))
    $accentPen.Dispose()

    $scale = $Size / 64.0
    Draw-NetworkGlyph $g ([int]($Size / 2)) ([int]($Size * 0.12)) $scale $accent

    $fontSize = [Math]::Max(8, [int]($Size * 0.34))
    $font = New-Object System.Drawing.Font("Segoe UI", [single]$fontSize, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
    $text = 'IP'
    $textSize = $g.MeasureString($text, $font)
    $textX = ($Size - $textSize.Width) / 2
    $textY = $Size * 0.46
    $shadowBrush = New-Object System.Drawing.SolidBrush (New-Color 0 0 0 120)
    $textBrush = New-Object System.Drawing.SolidBrush (New-Color 240 249 255)
    $g.DrawString($text, $font, $shadowBrush, ($textX + 1), ($textY + 1))
    $g.DrawString($text, $font, $textBrush, $textX, $textY)

    $font.Dispose()
    $shadowBrush.Dispose()
    $textBrush.Dispose()
    $g.Dispose()
    return $bmp
}

function Get-RoundedRectPath {
    param([int]$X, [int]$Y, [int]$W, [int]$H, [int]$Radius)
    $d = $Radius * 2
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddArc($X, $Y, $d, $d, 180, 90)
    $path.AddArc($X + $W - $d, $Y, $d, $d, 270, 90)
    $path.AddArc($X + $W - $d, $Y + $H - $d, $d, $d, 0, 90)
    $path.AddArc($X, $Y + $H - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    return $path
}

function New-TrayBitmap {
    param(
        [int]$Size,
        [System.Drawing.Color]$Accent
    )

    $bmp = New-Object System.Drawing.Bitmap $Size, $Size
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    Set-Quality $g
    $g.Clear([System.Drawing.Color]::Transparent)

    $pad = [Math]::Max(0, [int]($Size * 0.06))
    $rect = New-Object System.Drawing.Rectangle $pad, $pad, ($Size - $pad * 2), ($Size - $pad * 2)
    $radius = [Math]::Max(2, [int]($Size * 0.22))
    $bgBrush = New-Object System.Drawing.SolidBrush (New-Color 15 23 42)
    Draw-RoundedRect $g $bgBrush $rect.X $rect.Y $rect.Width $rect.Height $radius
    $bgBrush.Dispose()

    $dotSize = [Math]::Max(4, [int]($Size * 0.42))
    $dotX = [int](($Size - $dotSize) / 2)
    $dotY = [int](($Size - $dotSize) / 2)
    $dotBrush = New-Object System.Drawing.SolidBrush $Accent
    $g.FillEllipse($dotBrush, $dotX, $dotY, $dotSize, $dotSize)
    $dotBrush.Dispose()

    if ($Size -ge 24) {
        $fontSize = [Math]::Max(6, [int]($Size * 0.28))
        $font = New-Object System.Drawing.Font("Segoe UI", [single]$fontSize, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
        $textBrush = New-Object System.Drawing.SolidBrush (New-Color 15 23 42)
        $text = 'IP'
        $textSize = $g.MeasureString($text, $font)
        $g.DrawString($text, $font, $textBrush, (($Size - $textSize.Width) / 2), (($Size - $textSize.Height) / 2) - 1)
        $font.Dispose()
        $textBrush.Dispose()
    }

    $g.Dispose()
    return $bmp
}

function Save-Png {
    param(
        [System.Drawing.Bitmap]$Bitmap,
        [string]$Path
    )
    $dir = Split-Path -Parent $Path
    if ($dir -and -not (Test-Path $dir)) {
        New-Item -ItemType Directory -Force -Path $dir | Out-Null
    }
    $Bitmap.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
}

function Save-Ico {
    param(
        [System.Drawing.Bitmap[]]$Sizes,
        [string]$Path
    )

    $ordered = $Sizes | Sort-Object { $_.Width }
    $ms = New-Object System.IO.MemoryStream
    $writer = New-Object System.IO.BinaryWriter $ms

    $writer.Write([uint16]0)
    $writer.Write([uint16]1)
    $writer.Write([uint16]$ordered.Count)

    $offset = 6 + (16 * $ordered.Count)
    $imageDataList = New-Object System.Collections.Generic.List[byte[]]

    foreach ($bmp in $ordered) {
        $pngMs = New-Object System.IO.MemoryStream
        $bmp.Save($pngMs, [System.Drawing.Imaging.ImageFormat]::Png)
        $pngBytes = $pngMs.ToArray()
        $pngMs.Dispose()
        $imageDataList.Add($pngBytes) | Out-Null

        $w = $bmp.Width
        $h = $bmp.Height
        if ($w -ge 256) { $w = 0 }
        if ($h -ge 256) { $h = 0 }

        $writer.Write([byte]$w)
        $writer.Write([byte]$h)
        $writer.Write([byte]0)
        $writer.Write([byte]0)
        $writer.Write([uint16]1)
        $writer.Write([uint16]32)
        $writer.Write([uint32]$pngBytes.Length)
        $writer.Write([uint32]$offset)
        $offset += $pngBytes.Length
    }

    foreach ($data in $imageDataList) {
        $writer.Write($data)
    }

    [System.IO.File]::WriteAllBytes($Path, $ms.ToArray())
    $writer.Dispose()
    $ms.Dispose()
}

function Save-IcoFromBitmap {
    param(
        [scriptblock]$Factory,
        [string]$Path
    )
    $sizes = @(16, 24, 32, 48, 64, 256) | ForEach-Object {
        & $Factory $_
    }
    try {
        Save-Ico -Sizes $sizes -Path $Path
    }
    finally {
        foreach ($bmp in $sizes) { $bmp.Dispose() }
    }
}

function New-WideBitmap {
    param([int]$Width, [int]$Height)
    $bmp = New-Object System.Drawing.Bitmap $Width, $Height
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    Set-Quality $g
    $rect = New-Object System.Drawing.Rectangle 0, 0, $Width, $Height
    $bgBrush = New-LinearBrush $rect (New-Color 15 23 42) (New-Color 30 58 95)
    $g.FillRectangle($bgBrush, $rect)
    $bgBrush.Dispose()

    $logoSize = [Math]::Min($Height - 40, [int]($Width * 0.22))
    $logo = New-AppBitmap -Size $logoSize
    $x = [int](($Width - $logoSize) / 2)
    $y = [int](($Height - $logoSize) / 2)
    $g.DrawImage($logo, $x, $y, $logoSize, $logoSize)
    $logo.Dispose()

    $titleFont = New-Object System.Drawing.Font("Segoe UI", [single]([Math]::Max(18, [int]($Height * 0.16))), [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
    $subFont = New-Object System.Drawing.Font("Segoe UI", [single]([Math]::Max(10, [int]($Height * 0.08))), [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
    $titleBrush = New-Object System.Drawing.SolidBrush (New-Color 240 249 255)
    $subBrush = New-Object System.Drawing.SolidBrush (New-Color 148 163 184)
    $title = 'IP Checker'
    $sub = 'Network status at a glance'
    $titleSize = $g.MeasureString($title, $titleFont)
    $subSize = $g.MeasureString($sub, $subFont)
    $textLeft = $x + $logoSize + 24
    $titleY = ($Height - $titleSize.Height - $subSize.Height - 4) / 2
    $g.DrawString($title, $titleFont, $titleBrush, $textLeft, $titleY)
    $g.DrawString($sub, $subFont, $subBrush, $textLeft, $titleY + $titleSize.Height + 4)
    $titleFont.Dispose()
    $subFont.Dispose()
    $titleBrush.Dispose()
    $subBrush.Dispose()
    $g.Dispose()
    return $bmp
}

Write-Host 'Generating app icons...'

Save-IcoFromBitmap -Path (Join-Path $assetsDir 'AppIcon.ico') -Factory {
    param($s) New-AppBitmap -Size $s
}

Save-IcoFromBitmap -Path (Join-Path $assetsDir 'TrayIconDhcp.ico') -Factory {
    param($s) New-TrayBitmap -Size $s -Accent (New-Color 34 197 94)
}

Save-IcoFromBitmap -Path (Join-Path $assetsDir 'TrayIconStatic.ico') -Factory {
    param($s) New-TrayBitmap -Size $s -Accent (New-Color 245 158 11)
}

Save-IcoFromBitmap -Path (Join-Path $assetsDir 'TrayIconNoIp.ico') -Factory {
    param($s) New-TrayBitmap -Size $s -Accent (New-Color 148 163 184)
}

$pngJobs = @(
    @{ Path = 'Square150x150Logo.scale-200.png'; Size = 300 },
    @{ Path = 'Square44x44Logo.scale-200.png'; Size = 88 },
    @{ Path = 'Square44x44Logo.targetsize-24_altform-unplated.png'; Size = 24 },
    @{ Path = 'Square44x44Logo.targetsize-48_altform-lightunplated.png'; Size = 48 },
    @{ Path = 'StoreLogo.png'; Size = 50 },
    @{ Path = 'LockScreenLogo.scale-200.png'; Size = 48 }
)

foreach ($job in $pngJobs) {
    $bmp = New-AppBitmap -Size $job.Size
    try {
        Save-Png -Bitmap $bmp -Path (Join-Path $assetsDir $job.Path)
        Write-Host "  $($job.Path)"
    }
    finally {
        $bmp.Dispose()
    }
}

$wide = New-WideBitmap -Width 620 -Height 300
try {
    Save-Png -Bitmap $wide -Path (Join-Path $assetsDir 'Wide310x150Logo.scale-200.png')
    Write-Host '  Wide310x150Logo.scale-200.png'
}
finally {
    $wide.Dispose()
}

$splash = New-WideBitmap -Width 620 -Height 300
try {
    Save-Png -Bitmap $splash -Path (Join-Path $assetsDir 'SplashScreen.scale-200.png')
    Write-Host '  SplashScreen.scale-200.png'
}
finally {
    $splash.Dispose()
}

Write-Host "Done. Assets written to $assetsDir"
