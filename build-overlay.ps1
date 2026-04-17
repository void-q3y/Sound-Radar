$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName Microsoft.CSharp

$source = Join-Path $PSScriptRoot "RadarOverlay.cs"
$output = Join-Path $PSScriptRoot "SoundRadarOverlay.exe"
$iconPath = Join-Path ([System.IO.Path]::GetTempPath()) "sound-radar-overlay.ico"

$destroyIconSignature = @"
using System;
using System.Runtime.InteropServices;
public static class NativeIconMethods
{
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern bool DestroyIcon(IntPtr handle);
}
"@

Add-Type -TypeDefinition $destroyIconSignature -Language CSharp | Out-Null

function New-RadarIcon {
    param(
        [string]$Path
    )

    $size = 256
    $bitmap = New-Object System.Drawing.Bitmap $size, $size
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.Clear([System.Drawing.Color]::Transparent)

    $outerRect = New-Object System.Drawing.RectangleF 6, 6, 244, 244
    $midRect = New-Object System.Drawing.RectangleF 24, 24, 208, 208
    $innerRect = New-Object System.Drawing.RectangleF 52, 52, 152, 152
    $coreRect = New-Object System.Drawing.RectangleF 80, 80, 96, 96

    $bgBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(235, 28, 28, 30))
    $ringBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(235, 36, 36, 39))
    $gridPen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(170, 205, 205, 205), ([single]2.2))
    $midPen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(235, 245, 245, 245), ([single]4))
    $basePen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(210, 85, 145, 255), ([single]4))
    $pointerPen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(235, 255, 70, 70), ([single]4))
    $dotBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(235, 255, 70, 70))

    $graphics.FillEllipse($bgBrush, $outerRect)
    $graphics.FillEllipse($ringBrush, $midRect)
    $graphics.DrawEllipse($gridPen, $outerRect)
    $graphics.DrawEllipse($gridPen, $midRect)
    $graphics.DrawEllipse($gridPen, $innerRect)
    $graphics.DrawEllipse($gridPen, $coreRect)
    $graphics.DrawLine($midPen, 128, 20, 128, 236)
    $graphics.DrawLine($basePen, 34, 128, 222, 128)
    $graphics.DrawLine($pointerPen, 128, 128, 128, 74)
    $graphics.FillEllipse($dotBrush, 118, 118, 20, 20)

    $handle = $bitmap.GetHicon()
    try {
        $icon = [System.Drawing.Icon]::FromHandle($handle)
        $stream = [System.IO.File]::Open($Path, [System.IO.FileMode]::Create, [System.IO.FileAccess]::Write)
        try {
            $icon.Save($stream)
        }
        finally {
            $stream.Dispose()
            $icon.Dispose()
        }
    }
    finally {
        [NativeIconMethods]::DestroyIcon($handle) | Out-Null
        $pointerPen.Dispose()
        $basePen.Dispose()
        $midPen.Dispose()
        $gridPen.Dispose()
        $dotBrush.Dispose()
        $ringBrush.Dispose()
        $bgBrush.Dispose()
        $graphics.Dispose()
        $bitmap.Dispose()
    }
}

function Build-OverlayExe {
    param(
        [string]$Code,
        [string]$OutputPath,
        [string]$ExeIconPath
    )

    if (Test-Path $OutputPath) {
        Remove-Item -LiteralPath $OutputPath -Force
    }

    $provider = New-Object Microsoft.CSharp.CSharpCodeProvider
    $parameters = New-Object System.CodeDom.Compiler.CompilerParameters
    $parameters.GenerateExecutable = $true
    $parameters.GenerateInMemory = $false
    $parameters.IncludeDebugInformation = $false
    $parameters.OutputAssembly = $OutputPath
    $parameters.CompilerOptions = "/target:winexe /optimize /win32icon:`"$ExeIconPath`""
    [void]$parameters.ReferencedAssemblies.Add("System.dll")
    [void]$parameters.ReferencedAssemblies.Add("System.Core.dll")
    [void]$parameters.ReferencedAssemblies.Add("System.Drawing.dll")
    [void]$parameters.ReferencedAssemblies.Add("System.Windows.Forms.dll")

    $result = $provider.CompileAssemblyFromSource($parameters, $Code)
    if ($result.Errors.HasErrors) {
        $messages = @()
        foreach ($error in $result.Errors) {
            if (-not $error.IsWarning) {
                $messages += $error.ToString()
            }
        }

        if ($messages.Count -gt 0) {
            throw ($messages -join [Environment]::NewLine)
        }
    }
}

$code = Get-Content -LiteralPath $source -Raw
New-RadarIcon -Path $iconPath

try {
    Build-OverlayExe -Code $code -OutputPath $output -ExeIconPath $iconPath
}
finally {
    if (Test-Path $iconPath) {
        Remove-Item -LiteralPath $iconPath -Force
    }
}

Write-Host "Built $output"
