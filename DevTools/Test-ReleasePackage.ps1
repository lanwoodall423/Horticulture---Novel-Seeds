param(
    [string]$PackageRoot = (Split-Path -Parent $PSScriptRoot),
    [string]$RepositoryRoot = (Split-Path -Parent $PSScriptRoot)
)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath($PackageRoot)
$repository = [IO.Path]::GetFullPath($RepositoryRoot)
$productionAssembly = Join-Path $root '1.6\Assemblies\HorticultureNovelSeeds.dll'
$testAssemblies = @(Get-ChildItem -LiteralPath (Join-Path $root '1.6\Assemblies') -Filter 'HorticultureNovelSeeds.RuntimeTests*.dll' -File -ErrorAction SilentlyContinue)
if (-not (Test-Path -LiteralPath $productionAssembly -PathType Leaf)) { throw 'Production Release DLL is missing.' }
if ($testAssemblies.Count -gt 0) { throw 'Runtime test DLL is present in the release assembly directory.' }
$bundle = Join-Path $root '1.6\AutoMasks\BundledAutoMasks.xml'
$manifestPath = Join-Path $root '1.6\AutoMasks\BundledAutoMasks.manifest.json'
if (-not (Test-Path -LiteralPath $bundle -PathType Leaf)) { throw 'Bundled automatic mask XML is missing.' }
if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) { throw 'Bundled automatic mask manifest is missing.' }
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
if ($manifest.packageId -ne 'lan.horticulture.novelseeds') { throw 'Automatic mask manifest package ID is incorrect.' }
if ($manifest.formatVersion -ne 2 -or $manifest.generatorVersion -ne 15) { throw 'Automatic mask manifest version is stale.' }
if ($manifest.recordCount -le 0) { throw 'Automatic mask manifest contains no records.' }
if ((Get-FileHash -LiteralPath $bundle -Algorithm SHA256).Hash -ne $manifest.xmlSha256) { throw 'Automatic mask manifest hash does not match the XML bundle.' }
$trackedPaths = @(git -C $repository ls-files)
if (@($trackedPaths | Where-Object {
    ($_ -match '(^|/)(BridgeAdapter|BridgeAdapters)(/|$)|Build-HotBridgeAdapter|Test-BridgeAdapter|DEVBRIDGE_AGENT|DevBridge/agent\.json') -and
    (Test-Path -LiteralPath (Join-Path $repository $_) -PathType Leaf)
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
$isStagedPackage = $root.TrimEnd('\') -ne $repository.TrimEnd('\')
if ($isStagedPackage) {
    $forbidden = @('Source', 'DevTools', '.git', '.tura', 'bin', 'obj', '1.6\Assemblies\HorticultureNovelSeeds.RuntimeTests.v12.dll')
    foreach ($relative in $forbidden) {
        if (Test-Path -LiteralPath (Join-Path $root $relative)) { throw "Forbidden development content is present in the release package: $relative" }
    }
}
Write-Output 'Release package validation passed: production DLL contains no runtime-test or bridge implementation markers.'
