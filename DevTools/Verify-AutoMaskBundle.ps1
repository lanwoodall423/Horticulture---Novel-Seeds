param(
    [Parameter(Mandatory = $true)]
    [string]$BundlePath,
    [string]$ManifestPath = '',
    [int]$ExpectedFormatVersion = 2,
    [int]$ExpectedGeneratorVersion = 15,
    [switch]$RejectLowConfidence
)

$ErrorActionPreference = 'Stop'
if (-not (Test-Path -LiteralPath $BundlePath -PathType Leaf)) { throw "Bundle file is missing: $BundlePath" }

[xml]$document = Get-Content -LiteralPath $BundlePath -Raw
$root = $document.SelectSingleNode('//autoMaskBundle')
if ($null -eq $root) { throw 'Bundle root <autoMaskBundle> is missing.' }

function Get-Value([System.Xml.XmlNode]$node, [string]$name) {
    $child = $node.SelectSingleNode('./' + $name)
    if ($null -eq $child) { return '' }
    return $child.InnerText
}

$format = [int](Get-Value $root 'formatVersion')
$generator = [int](Get-Value $root 'generatorVersion')
if ($format -ne $ExpectedFormatVersion) { throw "Bundle format $format does not match expected $ExpectedFormatVersion." }
if ($generator -ne $ExpectedGeneratorVersion) { throw "Bundle generator $generator does not match expected $ExpectedGeneratorVersion." }

$maskContainer = $root.SelectSingleNode('./masks')
if ($null -eq $maskContainer) { throw 'Bundle masks collection is missing.' }
$records = @($maskContainer.ChildNodes | Where-Object { $_.NodeType -eq [System.Xml.XmlNodeType]::Element })
if ($records.Count -eq 0) { throw 'Bundle contains no mask records.' }

$required = @(
    'formatVersion', 'generatorVersion', 'plantDef', 'texturePath', 'textureContentHash',
    'textureWidth', 'textureHeight', 'textureKey', 'graphicIdentity', 'growthState',
    'directionIdentity', 'variationIdentity', 'produceSignature', 'eligibilityKey',
    'morphologyIdentity'
)
$keys = New-Object 'System.Collections.Generic.HashSet[string]'
$lowConfidence = 0
$failures = New-Object System.Collections.Generic.List[string]
$recordRows = New-Object System.Collections.Generic.List[object]
foreach ($record in $records) {
    $plant = Get-Value $record 'plantDef'
    $variation = Get-Value $record 'variationIndex'
    $key = $plant + '|' + $variation
    if (-not $keys.Add($key)) { $failures.Add("duplicate record $key") }
    foreach ($field in $required) {
        if ([string]::IsNullOrWhiteSpace((Get-Value $record $field))) { $failures.Add("$key missing $field") }
    }
    if ([int](Get-Value $record 'formatVersion') -ne $ExpectedFormatVersion) { $failures.Add("$key has stale format") }
    if ([int](Get-Value $record 'generatorVersion') -ne $ExpectedGeneratorVersion) { $failures.Add("$key has stale generator") }
    if ((Get-Value $record 'lowConfidence') -eq 'True') { $lowConfidence++ }
    $recordRows.Add([ordered]@{
        plantDef = $plant
        variation = [int]$variation
        texturePath = Get-Value $record 'texturePath'
        textureContentHash = Get-Value $record 'textureContentHash'
        textureKey = Get-Value $record 'textureKey'
        sourcePackageId = Get-Value $record 'sourcePackageId'
        sourceModName = Get-Value $record 'sourceModName'
        lowConfidence = ((Get-Value $record 'lowConfidence') -eq 'True')
    })
}
if ($failures.Count -gt 0) { throw ('Bundle validation failed: ' + ($failures -join '; ')) }
if ($RejectLowConfidence -and $lowConfidence -gt 0) {
    throw "Bundle validation failed: $lowConfidence low-confidence records are not complete for publishing."
}

$hash = (Get-FileHash -LiteralPath $BundlePath -Algorithm SHA256).Hash
$manifest = [ordered]@{
    schemaVersion = 1
    packageId = 'lan.horticulture.novelseeds'
    modName = 'Horticulture - Novel Seeds'
    formatVersion = $format
    generatorVersion = $generator
    bundleId = Get-Value $root 'bundleId'
    sourcePackageId = Get-Value $root 'sourcePackageId'
    sourceModName = Get-Value $root 'sourceModName'
    generatedUtc = (Get-Item -LiteralPath $BundlePath).LastWriteTimeUtc.ToString('o')
    xmlSha256 = $hash
    recordCount = $records.Count
    lowConfidenceCount = $lowConfidence
    failureCount = 0
    records = [object[]]$recordRows.ToArray()
}
if (-not [string]::IsNullOrWhiteSpace($ManifestPath)) {
    $manifestDirectory = Split-Path -Parent $ManifestPath
    if ($manifestDirectory) { New-Item -ItemType Directory -Path $manifestDirectory -Force | Out-Null }
    $manifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $ManifestPath -Encoding UTF8
}
Write-Output ("Automatic mask bundle validated: records={0}, lowConfidence={1}, sha256={2}" -f $records.Count, $lowConfidence, $hash)
