param(
    [switch]$Quiet
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$workspace = [System.IO.Path]::GetFullPath((Join-Path $scriptDir ".."))
$assetDir = Join-Path $workspace "src\AliasCockpit.App\Assets"

Add-Type -AssemblyName System.Drawing

function New-Canvas {
    param(
        [Parameter(Mandatory = $true)]
        [int]$Width,
        [Parameter(Mandatory = $true)]
        [int]$Height
    )

    $bitmap = New-Object System.Drawing.Bitmap($Width, $Height, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
    return [pscustomobject]@{
        Bitmap = $bitmap
        Graphics = $graphics
    }
}

function New-RoundedRectPath {
    param(
        [Parameter(Mandatory = $true)]
        [System.Drawing.RectangleF]$Rectangle,
        [Parameter(Mandatory = $true)]
        [float]$Radius
    )

    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $diameter = $Radius * 2
    $path.AddArc($Rectangle.X, $Rectangle.Y, $diameter, $diameter, 180, 90)
    $path.AddArc($Rectangle.Right - $diameter, $Rectangle.Y, $diameter, $diameter, 270, 90)
    $path.AddArc($Rectangle.Right - $diameter, $Rectangle.Bottom - $diameter, $diameter, $diameter, 0, 90)
    $path.AddArc($Rectangle.X, $Rectangle.Bottom - $diameter, $diameter, $diameter, 90, 90)
    $path.CloseFigure()
    return $path
}

function Save-Png {
    param(
        [Parameter(Mandatory = $true)]
        [System.Drawing.Bitmap]$Bitmap,
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $Bitmap.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
}

function New-BrandBitmap {
    param(
        [Parameter(Mandatory = $true)]
        [int]$Size,
        [switch]$Wide
    )

    $width = if ($Wide) { [int]($Size * 2.0667) } else { $Size }
    $canvas = New-Canvas -Width $width -Height $Size
    $bitmap = $canvas.Bitmap
    $graphics = $canvas.Graphics

    try {
        $graphics.Clear([System.Drawing.Color]::Transparent)
        $bounds = [System.Drawing.RectangleF]::new(0, 0, $width, $Size)
        $radius = [Math]::Max(8, $Size * 0.16)
        $backgroundPath = New-RoundedRectPath -Rectangle $bounds -Radius $radius
        $backgroundBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
            $bounds,
            [System.Drawing.Color]::FromArgb(255, 12, 42, 54),
            [System.Drawing.Color]::FromArgb(255, 30, 95, 84),
            [System.Drawing.Drawing2D.LinearGradientMode]::ForwardDiagonal)
        $graphics.FillPath($backgroundBrush, $backgroundPath)

        $accentPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(210, 247, 194, 84), [Math]::Max(2, $Size * 0.026))
        $tealPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(220, 91, 220, 196), [Math]::Max(2, $Size * 0.025))
        $whitePen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(245, 245, 250, 244), [Math]::Max(3, $Size * 0.035))
        $whiteBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(250, 245, 250, 244))
        $goldBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 247, 194, 84))
        $tealBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 91, 220, 196))

        $cx = if ($Wide) { $Size * 0.58 } else { $Size * 0.5 }
        $cy = $Size * 0.5
        $shieldPath = New-Object System.Drawing.Drawing2D.GraphicsPath
        $shieldPath.AddLines([System.Drawing.PointF[]]@(
            [System.Drawing.PointF]::new($cx, $Size * 0.15),
            [System.Drawing.PointF]::new($Size * 0.79, $Size * 0.27),
            [System.Drawing.PointF]::new($Size * 0.73, $Size * 0.68),
            [System.Drawing.PointF]::new($cx, $Size * 0.86),
            [System.Drawing.PointF]::new($Size * 0.21, $Size * 0.68),
            [System.Drawing.PointF]::new($Size * 0.21, $Size * 0.27)
        ))
        $shieldPath.CloseFigure()
        $shieldBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(52, 255, 255, 255))
        $graphics.FillPath($shieldBrush, $shieldPath)
        $graphics.DrawPath($tealPen, $shieldPath)

        $fontSize = [Math]::Max(18, $Size * 0.42)
        $font = New-Object System.Drawing.Font("Segoe UI", $fontSize, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
        $format = New-Object System.Drawing.StringFormat
        $format.Alignment = [System.Drawing.StringAlignment]::Center
        $format.LineAlignment = [System.Drawing.StringAlignment]::Center
        $graphics.DrawString("@", $font, $whiteBrush, [System.Drawing.RectangleF]::new($Size * 0.17, $Size * 0.21, $Size * 0.66, $Size * 0.52), $format)

        $branchY = $Size * 0.62
        $graphics.DrawLine($accentPen, $Size * 0.36, $branchY, $Size * 0.64, $branchY)
        $graphics.DrawLine($accentPen, $Size * 0.5, $branchY, $Size * 0.42, $Size * 0.74)
        $graphics.DrawLine($accentPen, $Size * 0.5, $branchY, $Size * 0.58, $Size * 0.74)
        foreach ($point in @(
            [System.Drawing.PointF]::new($Size * 0.36, $branchY),
            [System.Drawing.PointF]::new($Size * 0.64, $branchY),
            [System.Drawing.PointF]::new($Size * 0.42, $Size * 0.74),
            [System.Drawing.PointF]::new($Size * 0.58, $Size * 0.74)
        )) {
            $r = $Size * 0.036
            $graphics.FillEllipse($goldBrush, $point.X - $r, $point.Y - $r, $r * 2, $r * 2)
        }

        if ($Wide) {
            $wordFont = New-Object System.Drawing.Font -ArgumentList "Segoe UI", ([single]($Size * 0.18)), ([System.Drawing.FontStyle]::Bold), ([System.Drawing.GraphicsUnit]::Pixel)
            $smallFont = New-Object System.Drawing.Font -ArgumentList "Segoe UI", ([single]($Size * 0.08)), ([System.Drawing.FontStyle]::Regular), ([System.Drawing.GraphicsUnit]::Pixel)
            $graphics.DrawString("Alias Cockpit", $wordFont, $whiteBrush, [single]($Size * 1.0), [single]($Size * 0.3))
            $graphics.DrawString("local alias control", $smallFont, $tealBrush, [single]($Size * 1.02), [single]($Size * 0.55))
            $wordFont.Dispose()
            $smallFont.Dispose()
        }

        $font.Dispose()
        $format.Dispose()
        $backgroundBrush.Dispose()
        $accentPen.Dispose()
        $tealPen.Dispose()
        $whitePen.Dispose()
        $whiteBrush.Dispose()
        $goldBrush.Dispose()
        $tealBrush.Dispose()
        $shieldBrush.Dispose()
        $shieldPath.Dispose()
        $backgroundPath.Dispose()

        return $bitmap
    }
    finally {
        $graphics.Dispose()
    }
}

function New-Ico {
    param(
        [Parameter(Mandatory = $true)]
        [int[]]$Sizes,
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $frames = New-Object System.Collections.Generic.List[object]
    foreach ($size in $Sizes) {
        $bitmap = New-BrandBitmap -Size $size
        $stream = New-Object System.IO.MemoryStream
        $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
        $frames.Add([pscustomobject]@{
            Size = $size
            Bytes = $stream.ToArray()
        }) | Out-Null
        $stream.Dispose()
        $bitmap.Dispose()
    }

    $file = [System.IO.File]::Create($Path)
    $writer = New-Object System.IO.BinaryWriter($file)
    try {
        $writer.Write([UInt16]0)
        $writer.Write([UInt16]1)
        $writer.Write([UInt16]$frames.Count)
        $offset = 6 + (16 * $frames.Count)
        foreach ($frame in $frames) {
            $dimension = if ($frame.Size -ge 256) { 0 } else { $frame.Size }
            $writer.Write([byte]$dimension)
            $writer.Write([byte]$dimension)
            $writer.Write([byte]0)
            $writer.Write([byte]0)
            $writer.Write([UInt16]1)
            $writer.Write([UInt16]32)
            $writer.Write([UInt32]$frame.Bytes.Length)
            $writer.Write([UInt32]$offset)
            $offset += $frame.Bytes.Length
        }

        foreach ($frame in $frames) {
            $writer.Write([byte[]]$frame.Bytes)
        }
    }
    finally {
        $writer.Dispose()
        $file.Dispose()
    }
}

New-Item -ItemType Directory -Path $assetDir -Force | Out-Null

$square300 = New-BrandBitmap -Size 300
Save-Png -Bitmap $square300 -Path (Join-Path $assetDir "Square150x150Logo.scale-200.png")
$square300.Dispose()

$square88 = New-BrandBitmap -Size 88
Save-Png -Bitmap $square88 -Path (Join-Path $assetDir "Square44x44Logo.scale-200.png")
$square88.Dispose()

$target24 = New-BrandBitmap -Size 24
Save-Png -Bitmap $target24 -Path (Join-Path $assetDir "Square44x44Logo.targetsize-24_altform-unplated.png")
$target24.Dispose()

$target48 = New-BrandBitmap -Size 48
Save-Png -Bitmap $target48 -Path (Join-Path $assetDir "Square44x44Logo.targetsize-48_altform-lightunplated.png")
$target48.Dispose()

$store = New-BrandBitmap -Size 50
Save-Png -Bitmap $store -Path (Join-Path $assetDir "StoreLogo.png")
$store.Dispose()

$lock = New-BrandBitmap -Size 48
Save-Png -Bitmap $lock -Path (Join-Path $assetDir "LockScreenLogo.scale-200.png")
$lock.Dispose()

$splash = New-BrandBitmap -Size 620 -Wide
Save-Png -Bitmap $splash -Path (Join-Path $assetDir "SplashScreen.scale-200.png")
$splash.Dispose()

$wide = New-BrandBitmap -Size 300 -Wide
Save-Png -Bitmap $wide -Path (Join-Path $assetDir "Wide310x150Logo.scale-200.png")
$wide.Dispose()

New-Ico -Sizes @(16, 24, 32, 48, 64, 128, 256) -Path (Join-Path $assetDir "AppIcon.ico")

if (-not $Quiet) {
    Get-ChildItem -LiteralPath $assetDir | Select-Object Name, Length
}
