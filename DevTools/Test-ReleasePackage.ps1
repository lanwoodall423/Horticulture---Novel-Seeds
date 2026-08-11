$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$productionAssembly = Join-Path $root '1.6\Assemblies\HorticultureNovelSeeds.dll'
$testAssemblies = @(Get-ChildItem -LiteralPath (Join-Path $root '1.6\Assemblies') -Filter 'HorticultureNovelSeeds.RuntimeTests*.dll' -File -ErrorAction SilentlyContinue)
if (-not (Test-Path -LiteralPath $productionAssembly -PathType Leaf)) { throw 'Production Release DLL is missing.' }
if ($testAssemblies.Count -gt 0) { throw 'Runtime test DLL is present in the release assembly directory.' }
$trackedPaths = @(git -C $root ls-files)
if (@($trackedPaths | Where-Object {
    ($_ -match '(^|/)(BridgeAdapter|BridgeAdapters)(/|$)|Build-HotBridgeAdapter|Test-BridgeAdapter|DEVBRIDGE_AGENT|DevBridge/agent\.json') -and
    (Test-Path -LiteralPath (Join-Path $root $_) -PathType Leaf)
}).Count -gt 0) {
    throw 'Retired bridge source or packaging files remain tracked.'
}
$bytes = [IO.File]::ReadAllBytes($productionAssembly)
$text = [Text.Encoding]::UTF8.GetString($bytes)
foreach ($marker in @('HorticultureRuntimeTestComponent', 'Horticulture.RuntimeTests', 'HorticultureBridgeAdapter', 'DevBridge2')) {
    if ($text.Contains($marker)) { throw "Production DLL contains test/bridge marker: $marker" }
}
if (@(Get-ChildItem -LiteralPath (Join-Path $root '1.6\Defs') -Recurse -File | Where-Object { $_.Name -match 'RuntimeTest|TestRequest|TestResult' }).Count -gt 0) {
    throw 'Synthetic runtime test Defs or result files are present in the release package.'
}
Write-Output 'Release package validation passed: production DLL contains no runtime-test or bridge implementation markers.'
