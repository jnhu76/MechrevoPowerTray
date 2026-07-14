Add-Type -AssemblyName System.Drawing

$pngPath = "icon.png"
$icoPath = [System.IO.Path]::GetFullPath("icon.ico")

if (-not (Test-Path $pngPath)) {
    Write-Error "icon.png not found at $pngPath"
    exit 1
}

$source = [System.Drawing.Bitmap]::new($pngPath)
$sizes = @(16, 24, 32, 48, 64, 128, 256)
$pngDataList = @()

foreach ($size in $sizes) {
    $resized = [System.Drawing.Bitmap]::new($source, [System.Drawing.Size]::new($size, $size))
    $ms = [System.IO.MemoryStream]::new()
    $resized.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngDataList += @{ Width = $size; Data = $ms.ToArray() }
    $ms.Dispose()
    $resized.Dispose()
}

$source.Dispose()

$fs = [System.IO.File]::Open($icoPath, [System.IO.FileMode]::Create)
$writer = [System.IO.BinaryWriter]::new($fs)

$count = $pngDataList.Count

$writer.Write([UInt16]0)        # Reserved
$writer.Write([UInt16]1)        # Type: 1 = ICO
$writer.Write([UInt16]$count)   # Count

$offset = 6 + $count * 16

$offsets = @()
foreach ($png in $pngDataList) {
    $offsets += $offset
    $offset += $png.Data.Length
}

for ($i = 0; $i -lt $count; $i++) {
    $w = if ($pngDataList[$i].Width -ge 256) { 0 } else { $pngDataList[$i].Width }
    $h = if ($pngDataList[$i].Width -ge 256) { 0 } else { $pngDataList[$i].Width }
    $writer.Write([byte]$w)
    $writer.Write([byte]$h)
    $writer.Write([byte]0)     # Color count
    $writer.Write([byte]0)     # Reserved
    $writer.Write([UInt16]1)   # Planes
    $writer.Write([UInt16]32)  # Bit count
    $writer.Write([UInt32]$pngDataList[$i].Data.Length)
    $writer.Write([UInt32]$offsets[$i])
}

foreach ($png in $pngDataList) {
    $writer.Write($png.Data)
}

$writer.Dispose()
$fs.Dispose()

Write-Host "icon.ico created at $icoPath with $count sizes: $($sizes -join ', ')"
