[CmdletBinding()]
param(
    [ValidateSet('AllPerformance','Minimal','BothLoading')]
    [string]$Loadout = 'AllPerformance',
    [string]$GameRoot = 'C:\Games\Steam\steamapps\common\RimWorld',
    [string]$ReceiptPath
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$workshop = Join-Path (Split-Path -Parent (Split-Path -Parent $GameRoot)) 'workshop\content\294100'
$result = [ordered]@{
    schemaVersion = 1
    sourceHead = $null
    gameVersion = $null
    loadout = $Loadout
    status = 'BLOCKED'
    reason = 'NOT_STARTED'
    fixtureSpeed = 'UltraBoost'
    packagesBeforeCandidate = @()
    packagesAfterCandidate = @()
    performanceIdentities = @()
    dependencyStatus = @()
    runtimeStatus = 'BLOCKED'
    verifiedUtc = [DateTime]::UtcNow.ToString('o')
}
function Read-Identity([string]$WorkshopId,[string]$PackageId,[string]$DllName,[switch]$AllowMissileGirl16) {
    $root = Join-Path $workshop $WorkshopId
    $about = Join-Path $root 'About\About.xml'
    $metadata = ([xml](Get-Content -LiteralPath $about -Raw)).ModMetaData
    if ($metadata.packageId -ine $PackageId) { throw "PACKAGE_MISMATCH $PackageId" }
    $versions = @($metadata.supportedVersions.li)
    $folder = '1.7'
    if ($versions -notcontains '1.7') {
        if (!$AllowMissileGirl16 -or $PackageId -ne 'vr.missilegirl' -or $versions -notcontains '1.6') {
            throw "VERSION_MISMATCH $PackageId"
        }
        $folder = '1.6'
    }
    $dll = Join-Path $root ($folder+'\Assemblies\'+$DllName)
    if (!(Test-Path -LiteralPath $dll -PathType Leaf)) { throw "ASSEMBLY_MISSING $PackageId" }
    $identity = [ordered]@{
        packageId=$PackageId; workshopId=$WorkshopId; version=[string]$metadata.modVersion
        declaredVersions=$versions; versionFolder=$folder; metadataException=($folder -ne '1.7')
        aboutSha256=(Get-FileHash -LiteralPath $about -Algorithm SHA256).Hash
        assemblySha256=(Get-FileHash -LiteralPath $dll -Algorithm SHA256).Hash
    }
    if ($AllowMissileGirl16) {
        # This exception is limited to the exact assembly owner-qualified in
        # Innovations' 2026-11-25 Agriculture/Cooking native receipts.
        if ($identity.assemblySha256 -ne '1E9DDB56998F7B963962095944AF3AEABBC90826E55BD53CB8083D3CC7E4B0B3') {
            throw 'MISSILE_GIRL_ASSEMBLY_CHANGED_REQUALIFICATION_REQUIRED'
        }
        $identity.loadFoldersSha256=(Get-FileHash -LiteralPath (Join-Path $root 'LoadFolders.xml') -Algorithm SHA256).Hash
    }
    return $identity
}
try {
    $result.sourceHead = (& git -C $repo rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0 -or $result.sourceHead -notmatch '^[0-9a-f]{40}$') { throw 'SOURCE_IDENTITY_UNAVAILABLE' }
    $result.gameVersion = (Get-Content -LiteralPath (Join-Path $GameRoot 'Version.txt') -Raw).Trim()
    if ($result.gameVersion -notmatch '^1\.7\.') { throw 'RIMWORLD_1_7_REQUIRED' }
    if (!(Test-Path -LiteralPath (Join-Path $GameRoot 'RimWorld.exe') -PathType Leaf)) { throw 'GAME_EXE_MISSING' }
    $result.packagesBeforeCandidate = @('brrainz.harmony')
    $harmony = Read-Identity '2009463077' 'brrainz.harmony' '0Harmony.dll'
    if ($Loadout -ne 'Minimal') {
        $result.performanceIdentities += Read-Identity '3817423183' 'aruclya.rimworldloadingoptimizer' 'RimWorldLoadingOptimizer.dll'
        $result.performanceIdentities += Read-Identity '3652938473' 'taranchuk.fastergameloading' 'FasterGameLoading.dll'
        $result.packagesBeforeCandidate += @('aruclya.rimworldloadingoptimizer','taranchuk.fastergameloading')
    }
    $result.packagesBeforeCandidate += 'ludeon.rimworld'
    if ($Loadout -eq 'AllPerformance') {
        $result.performanceIdentities += Read-Identity '3712928623' 'vr.missilegirl' 'Cosmodrome.dll' -AllowMissileGirl16
        $result.packagesAfterCandidate = @('vr.missilegirl')
    }
    $result.harmonyIdentity = $harmony
    $result.status = 'PASS'
    $result.reason = 'PERFORMANCE_LOADOUT_PREFLIGHT_ONLY'
    # Do not represent a 1.6 source tree or dependency as a 1.7 candidate.
    $metadata = ([xml](Get-Content -LiteralPath (Join-Path $repo 'About\About.xml') -Raw)).ModMetaData
    $result.horticultureDeclaredVersions = @($metadata.supportedVersions.li)
    $result.dependencyStatus = @(foreach ($d in @(
        @{id='lan.knowledgeframework';root='C:\Projects\Knowledge-Framework'},
        @{id='lan.insightcanvas';root='C:\Projects\InsightCanvas'},
        @{id='ferny.progressionagriculture';root=(Join-Path $workshop '3418017359')}
    )) {
        $about = Join-Path $d.root 'About\About.xml'
        $versions = @()
        if (Test-Path -LiteralPath $about -PathType Leaf) {
            $m = ([xml](Get-Content -LiteralPath $about -Raw)).ModMetaData
            if ($m.packageId -ine $d.id) { throw "DEPENDENCY_ID_MISMATCH $($d.id)" }
            $versions = @($m.supportedVersions.li)
        }
        [ordered]@{packageId=$d.id;declaredVersions=$versions;declares17=($versions -contains '1.7')}
    })
    $result.runtimeStatus = 'NOT_QUALIFIED'
    if ($result.horticultureDeclaredVersions -notcontains '1.7' -or @($result.dependencyStatus | Where-Object {!$_.declares17}).Count -gt 0) {
        $result.runtimeStatus = 'BLOCKED_1_7_PORT_OR_DEPENDENCIES'
    }
    $result.gameClosed = (@(Get-CimInstance Win32_Process -Filter "Name='RimWorld.exe' OR Name='RimWorldWin64.exe'").Count -eq 0)
} catch {
    $result.status = 'BLOCKED'
    $result.reason = $_.Exception.Message
}
$json = $result | ConvertTo-Json -Depth 8
if ($ReceiptPath) {
    # Only write under the candidate's ignored test-results directory.
    $base = [IO.Path]::GetFullPath((Join-Path $repo 'DevTools\TestResults')).TrimEnd('\')+'\'
    $path = [IO.Path]::GetFullPath($ReceiptPath)
    if (!$path.StartsWith($base,[StringComparison]::OrdinalIgnoreCase)) { throw 'RECEIPT_MUST_BE_UNDER_TEST_RESULTS' }
    New-Item -ItemType Directory -Path (Split-Path -Parent $path) -Force | Out-Null
    [IO.File]::WriteAllText($path,$json,(New-Object Text.UTF8Encoding($false)))
}
Write-Output $json
if ($result.status -ne 'PASS') { exit 2 }
exit 0
