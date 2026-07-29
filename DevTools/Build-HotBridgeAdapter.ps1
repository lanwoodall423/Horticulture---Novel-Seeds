param(
    [string]$BridgeRoot = 'C:\Games\Steam\steamapps\common\RimWorld\Mods\RimWorldDevBridge'
)

$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'BridgeAdapter\HorticultureNovelSeeds.BridgeAdapter.csproj'
$build = Join-Path $PSScriptRoot 'BridgeAdapter\Build'
$destination = Join-Path $BridgeRoot 'DevTools\HotAdapters'
$stamp = Get-Date -Format 'yyyyMMddHHmmssfff'
$assemblyName = "HorticultureNovelSeeds.BridgeAdapter.$stamp"

New-Item -ItemType Directory -Force -Path $build, $destination | Out-Null
dotnet build $project -c Release "-p:AssemblyName=$assemblyName" "-p:OutputPath=$build"
if ($LASTEXITCODE -ne 0) { throw "Hot adapter build failed with exit code $LASTEXITCODE." }

$built = Join-Path $build ($assemblyName + '.dll')
$target = Join-Path $destination ($assemblyName + '.dll')
Copy-Item -LiteralPath $built -Destination $target

Write-Output "adapter=$target"
Write-Output 'reload=RELOAD_HOT_ADAPTERS'
