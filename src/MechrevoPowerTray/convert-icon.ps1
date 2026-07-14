$ErrorActionPreference = "Stop"
$Magick = "C:\Program Files\ImageMagick-7.1.2-Q16-HDRI\magick.exe"
$SourcePng = [System.IO.Path]::GetFullPath("..\..\ChatGPT Image 2026年7月14日 15_39_54.png")
$OutDir = Split-Path -Parent $PSCommandPath

Set-Location $OutDir

# Remove background → rounded corners → resize → compress
& cmd /c """$Magick"" ""$SourcePng"" -fuzz 15% -transparent white -resize 256x256 -alpha on ( +clone -alpha extract -draw ""roundrectangle 0,0,255,255,40,40"" ) -compose CopyOpacity -composite -strip -quality 90 icon.png 2>&1"

# Generate ICO with multiple sizes
& $Magick icon.png -background none -define icon:auto-resize=256,64,48,32,16 icon.ico 2>&1

Write-Host "icon.ico generated" -ForegroundColor Green
