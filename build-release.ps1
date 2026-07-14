$ErrorActionPreference = "Stop"

$Project = Join-Path $PSScriptRoot "src\MechrevoPowerTray\MechrevoPowerTray.csproj"
$Output = Join-Path $PSScriptRoot "artifacts\publish\win-x64"

New-Item -ItemType Directory -Force $Output | Out-Null

dotnet publish $Project `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -o $Output

Write-Host ""
Write-Host "Published:" -ForegroundColor Green
Write-Host (Join-Path $Output "MechrevoPowerTray.exe")
