param(
    [string]$OutputDirectory = (Join-Path $PSScriptRoot 'Staged\ReleasePackage'),
    [string]$ZipPath = (Join-Path $PSScriptRoot 'Staged\Horticulture-Novel-Seeds-0.1.0-rc.1.zip'),
    [string]$Version = '0.1.0-rc.1',
    [string]$BuildTimestampUtc = '',
    [string]$RimWorldRoot = (Join-Path $PSScriptRoot '..\..\..'),
    [string]$RuntimeResultsDirectory = (Join-Path $PSScriptRoot 'Staged\RuntimeResults'),
    [switch]$SkipBuild,
    [switch]$RequireRuntimePass
)

$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$package = [IO.Path]::GetFullPath($OutputDirectory)
$zip = [IO.Path]::GetFullPath($ZipPath)
$repoRoot = $root

function Require-File([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { throw "Required release input is missing: $Path" }
}

function Copy-ReleaseInput([string]$RelativePath) {
    $source = Join-Path $root $RelativePath
    Require-File $source
    $destination = Join-Path $package $RelativePath
    New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
    Copy-Item -LiteralPath $source -Destination $destination -Force
}

function Copy-ReleaseDirectory([string]$RelativePath) {
    $source = Join-Path $root $RelativePath
    if (-not (Test-Path -LiteralPath $source -PathType Container)) { throw "Required release directory is missing: $source" }
    $destination = Join-Path $package $RelativePath
    New-Item -ItemType Directory -Path $destination -Force | Out-Null
    Copy-Item -Path (Join-Path $source '*') -Destination $destination -Recurse -Force
}

if (-not $SkipBuild) {
    & dotnet build (Join-Path $root 'Source\HorticultureNovelSeeds.csproj') --configuration Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Horticulture Release build failed.' }
}

& (Join-Path $PSScriptRoot 'Test-ReleasePackage.ps1') -PackageRoot $root -RepositoryRoot $repoRoot

if (Test-Path -LiteralPath $package) { Remove-Item -LiteralPath $package -Recurse -Force }
New-Item -ItemType Directory -Path $package -Force | Out-Null
foreach ($file in @(
    'About\About.xml', 'LoadFolders.xml', 'README.md', 'CHANGELOG.md',
    'docs\DEFAULTS.md', 'docs\COMPATIBILITY.md', 'docs\PERFORMANCE.md', 'docs\BETA_FEEDBACK.md',
    '1.6\Assemblies\HorticultureNovelSeeds.dll', '1.6\AutoMasks\BundledAutoMasks.xml',
    '1.6\AutoMasks\BundledAutoMasks.manifest.json', '1.6\Defaults\DefaultConfiguration.xml'
)) { Copy-ReleaseInput $file }
foreach ($directory in @('1.6\Defs', '1.6\Languages')) { Copy-ReleaseDirectory $directory }
if (Test-Path -LiteralPath (Join-Path $root 'About\Preview.png') -PathType Leaf) { Copy-ReleaseInput 'About\Preview.png' }

& (Join-Path $PSScriptRoot 'Test-ReleasePackage.ps1') -PackageRoot $package -RepositoryRoot $repoRoot

if ([string]::IsNullOrWhiteSpace($BuildTimestampUtc)) {
    if (-not [string]::IsNullOrWhiteSpace($env:SOURCE_DATE_EPOCH)) {
        $BuildTimestampUtc = [DateTimeOffset]::FromUnixTimeSeconds([int64]$env:SOURCE_DATE_EPOCH).UtcDateTime.ToString('o')
    } else { $BuildTimestampUtc = [DateTime]::UtcNow.ToString('o') }
}
$timestamp = [DateTime]::Parse($BuildTimestampUtc, [Globalization.CultureInfo]::InvariantCulture,
    [Globalization.DateTimeStyles]::AssumeUniversal -bor [Globalization.DateTimeStyles]::AdjustToUniversal)

$gameRoot = [IO.Path]::GetFullPath($RimWorldRoot)
$gameExe = Join-Path $gameRoot 'RimWorldWin64.exe'
$gameAssembly = Join-Path $gameRoot 'RimWorldWin64_Data\Managed\Assembly-CSharp.dll'
$rimWorldVersion = '1.6'
if (Test-Path -LiteralPath $gameAssembly -PathType Leaf) {
    $rimWorldVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo($gameAssembly).ProductVersion
} elseif (Test-Path -LiteralPath $gameExe -PathType Leaf) {
    $rimWorldVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo($gameExe).ProductVersion
}

$production = Join-Path $package '1.6\Assemblies\HorticultureNovelSeeds.dll'
$maskBundle = Join-Path $package '1.6\AutoMasks\BundledAutoMasks.xml'
$maskManifestPath = Join-Path $package '1.6\AutoMasks\BundledAutoMasks.manifest.json'
$maskManifest = Get-Content -LiteralPath $maskManifestPath -Raw | ConvertFrom-Json
$knowledgePath = Join-Path $root '..\KnowledgeFramework\1.6\Assemblies\KnowledgeFramework.dll'
if (-not (Test-Path -LiteralPath $knowledgePath -PathType Leaf)) { throw "Knowledge Framework DLL is missing: $knowledgePath" }
$knowledge = [ordered]@{
    packageId = 'lan.knowledgeframework'
    apiGeneration = 3
    release = [Diagnostics.FileVersionInfo]::GetVersionInfo($knowledgePath).ProductVersion
    dllSha256 = (Get-FileHash -LiteralPath $knowledgePath -Algorithm SHA256).Hash
}

$resultFiles = @(Get-ChildItem -LiteralPath $RuntimeResultsDirectory -Filter '*.json' -File -ErrorAction SilentlyContinue)
$requiredScenarios = @('complete', 'auto-mask-suite')
$runtimeSummary = New-Object System.Collections.Generic.List[object]
$testedRimWorldVersions = New-Object System.Collections.Generic.List[string]
foreach ($scenario in $requiredScenarios) {
    $matches = @($resultFiles | Where-Object { $_.BaseName -like ($scenario + '.*') } | Sort-Object LastWriteTimeUtc -Descending)
    $report = if ($matches.Count -gt 0) { Get-Content -LiteralPath $matches[0].FullName -Raw | ConvertFrom-Json } else { $null }
    if ($null -ne $report -and -not [string]::IsNullOrWhiteSpace([string]$report.rimWorldVersion)) {
        $testedRimWorldVersions.Add([string]$report.rimWorldVersion)
    }
    $performanceMeasurements = @()
    if ($null -ne $report -and $null -ne $report.performanceMeasurements) {
        $performanceMeasurements = @($report.performanceMeasurements | Where-Object { $null -ne $_ })
    }
    $summary = [ordered]@{
        scenario = $scenario
        status = if ($null -eq $report) { 'NOT_RUN' } else { $report.status }
        reportFile = if ($null -eq $report) { '' } else { $matches[0].Name }
        suiteVersion = if ($null -eq $report) { '' } else { $report.suiteVersion }
        assertionCount = if ($null -eq $report) { 0 } else { $report.assertionCount }
        passedAssertions = if ($null -eq $report) { 0 } else { $report.passedAssertions }
        failedAssertions = if ($null -eq $report) { 0 } else { $report.failedAssertionsCount }
        blockedAssertions = if ($null -eq $report) { 0 } else { $report.blockedAssertionsCount }
        elapsedTicks = if ($null -eq $report) { 0 } else { $report.elapsedTicks }
        performanceMeasurements = $performanceMeasurements
    }
    $runtimeSummary.Add($summary)
    if ($RequireRuntimePass -and ($null -eq $report -or $report.status -ne 'PASS')) { throw "Required runtime scenario did not pass: $scenario" }
}
$distinctTestedVersions = @($testedRimWorldVersions | Sort-Object -Unique)
if ($distinctTestedVersions.Count -gt 1) { throw 'Required runtime reports disagree about the RimWorld version.' }
if ($distinctTestedVersions.Count -eq 1) { $rimWorldVersion = $distinctTestedVersions[0] }

$manifest = [ordered]@{
    schemaVersion = 1
    manifestVersion = 1
    packageId = 'lan.horticulture.novelseeds'
    modName = 'Horticulture - Novel Seeds'
    version = $Version
    commit = ((git -C $root rev-parse HEAD) | Out-String).Trim()
    buildTimestampUtc = $timestamp.ToString('o')
    rimWorldVersion = $rimWorldVersion
    productionDllSha256 = (Get-FileHash -LiteralPath $production -Algorithm SHA256).Hash
    maskBundleSha256 = (Get-FileHash -LiteralPath $maskBundle -Algorithm SHA256).Hash
    maskManifestSha256 = (Get-FileHash -LiteralPath $maskManifestPath -Algorithm SHA256).Hash
    maskBundle = [ordered]@{
        formatVersion = $maskManifest.formatVersion
        generatorVersion = $maskManifest.generatorVersion
        recordCount = $maskManifest.recordCount
        lowConfidenceCount = $maskManifest.lowConfidenceCount
        xmlSha256 = $maskManifest.xmlSha256
    }
    knowledgeFramework = $knowledge
    testSuite = [ordered]@{
        suite = 'Horticulture.RuntimeTests'
        requiredScenarios = $requiredScenarios
        summary = [object[]]$runtimeSummary.ToArray()
    }
    testedCompatibilitySet = @(
        'RimWorld 1.6 vanilla conventional sowable crops',
        'RimWorld 1.6 vanilla sowable trees',
        'Harmony', 'Knowledge Framework API generation 3', 'Progression: Agriculture',
        'Installed conventional sowable plant definitions'
    )
}
$manifest.packageFiles = @(
    Get-ChildItem -LiteralPath $package -Recurse -File | ForEach-Object {
        [ordered]@{
            path = $_.FullName.Substring($package.Length + 1).Replace('\', '/')
            bytes = $_.Length
            sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
        }
    }
)

$manifestPath = Join-Path $package 'RELEASE_MANIFEST.json'
$manifest | ConvertTo-Json -Depth 16 | Set-Content -LiteralPath $manifestPath -Encoding UTF8
if (Test-Path -LiteralPath $zip) { Remove-Item -LiteralPath $zip -Force }
New-Item -ItemType Directory -Path (Split-Path -Parent $zip) -Force | Out-Null
Compress-Archive -Path (Join-Path $package '*') -DestinationPath $zip -CompressionLevel Optimal
$artifactManifest = Join-Path $PSScriptRoot 'Staged\ReleaseManifest.json'
$manifest | ConvertTo-Json -Depth 16 | Set-Content -LiteralPath $artifactManifest -Encoding UTF8
Write-Output "Release package built: $package"
Write-Output "Release archive built: $zip"
Write-Output "Release manifest built: $artifactManifest"
