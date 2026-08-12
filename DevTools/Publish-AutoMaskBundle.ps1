param(
    [string]$DevBridgeRoot = (Join-Path $PSScriptRoot '..\..\DevBridge2'),
    [string]$StagingDirectory = (Join-Path $PSScriptRoot 'Staged\AutoMasks'),
    [int]$TimeoutSeconds = 900,
    [switch]$InstallBundle
)

$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$staging = [IO.Path]::GetFullPath($StagingDirectory)
New-Item -ItemType Directory -Path $staging -Force | Out-Null
$xml = Join-Path $staging 'BundledAutoMasks.xml'
$manifest = Join-Path $staging 'BundledAutoMasks.manifest.json'

Write-Output 'Building the Release production and Horticulture-owned runtime test assemblies.'
& (Join-Path $PSScriptRoot 'Run-RuntimeTests.ps1') -DevBridgeRoot $DevBridgeRoot -Scenario auto-mask-export `
    -RegenerateAutoMasks -BundleOutputPath $xml -TimeoutSeconds $TimeoutSeconds
if ($LASTEXITCODE -ne 0) { throw "Horticulture auto-mask export scenario failed with exit code $LASTEXITCODE." }

& (Join-Path $PSScriptRoot 'Verify-AutoMaskBundle.ps1') -BundlePath $xml -ManifestPath $manifest -RejectLowConfidence
if ($LASTEXITCODE -ne 0) { throw 'Automatic mask bundle validation failed.' }

$packageBundle = Join-Path $root '1.6\AutoMasks\BundledAutoMasks.xml'
$packageManifest = Join-Path $root '1.6\AutoMasks\BundledAutoMasks.manifest.json'
if ($InstallBundle) {
    $packageDirectory = [IO.Path]::GetFullPath((Split-Path -Parent $packageBundle))
    if (-not $packageDirectory.StartsWith($root + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to install outside the mod root: $packageDirectory"
    }
    New-Item -ItemType Directory -Path $packageDirectory -Force | Out-Null
    Copy-Item -LiteralPath $xml -Destination $packageBundle -Force
    Copy-Item -LiteralPath $manifest -Destination $packageManifest -Force
    Write-Output "Installed validated bundle: $packageBundle"
    Write-Output "Installed manifest: $packageManifest"
}
else {
    Write-Output "Validated staging bundle: $xml"
    Write-Output "Use -InstallBundle after reviewing it to copy into 1.6\AutoMasks."
}
