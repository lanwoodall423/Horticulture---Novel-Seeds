param(
    [string]$RimTest = 'C:\Games\Steam\steamapps\common\RimWorld\Mods\RimTest\rimtest.cmd',
    [switch]$BuildCompanion
)

$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Push-Location $root
try {
    if ($BuildCompanion) {
        & dotnet build (Join-Path $root 'DevTools\BridgeTools\HorticultureNovelSeeds.BridgeTools.csproj') --configuration Release
        if ($LASTEXITCODE -ne 0) { throw 'Horticulture RimBridge companion build failed.' }
    }

    & $RimTest run horticulture-in-game-smoke --json
    exit $LASTEXITCODE
}
finally {
    Pop-Location
}
