param(
    [Parameter(Mandatory=$true)][string]$PngPath,
    [Parameter(Mandatory=$true)][string]$IcoPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Drawing

if (!(Test-Path (Split-Path $IcoPath))) {
  New-Item -ItemType Directory -Path (Split-Path $IcoPath) -Force | Out-Null
}

# Desired sizes
$sizes = @(16,24,32,48,64,128,256)

$original = [System.Drawing.Bitmap]::new($PngPath)
try {
  $streams = @()
  foreach ($s in ($sizes | Sort-Object -Unique)) {
    $bmp = [System.Drawing.Bitmap]::new($s, $s)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    try {
      $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
      $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
      $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
      $g.Clear([System.Drawing.Color]::Transparent)
      $g.DrawImage($original, [System.Drawing.Rectangle]::new(0,0,$s,$s))
    } finally {
      $g.Dispose()
    }
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    $streams += ,(@($s, $ms))
  }

  $fs = [System.IO.File]::Create($IcoPath)
  $bw = New-Object System.IO.BinaryWriter($fs)
  try {
    # ICONDIR
    $bw.Write([UInt16]0)
    $bw.Write([UInt16]1)
    $bw.Write([UInt16]$($streams.Count))

    $offset = 6 + (16 * $streams.Count)
    foreach ($entry in $streams) {
      $size = [int]$entry[0]
      $ms = [System.IO.MemoryStream]$entry[1]
      $pngBytes = $ms.ToArray()

      $bw.Write([byte]($size -band 0xFF)) # width (0 means 256)
      $bw.Write([byte]($size -band 0xFF)) # height
      $bw.Write([byte]0)
      $bw.Write([byte]0)
      $bw.Write([UInt16]1) # planes
      $bw.Write([UInt16]32) # bpp
      $bw.Write([UInt32]$pngBytes.Length)
      $bw.Write([UInt32]$offset)

      $offset += $pngBytes.Length
    }

    foreach ($entry in $streams) {
      $ms = [System.IO.MemoryStream]$entry[1]
      $pngBytes = $ms.ToArray()
      $bw.Write($pngBytes)
    }
  } finally {
    $bw.Dispose(); $fs.Dispose()
    foreach ($entry in $streams) { $entry[1].Dispose() }
  }
} finally {
  $original.Dispose()
}
