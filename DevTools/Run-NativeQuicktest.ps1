[CmdletBinding()]
param(
    [ValidateSet('AllPerformance','BothLoading','Minimal')][string]$Loadout='AllPerformance',
    [ValidateRange(30,300)][int]$TimeoutSeconds=180,
    [switch]$ShaderProbe,
    [switch]$GardenProbe,
    [switch]$PatternGarden,
    [switch]$CatalogGarden,
    [switch]$CreativeGarden,
    [switch]$CultivarGarden,
    [switch]$FormGarden,
    [switch]$OrganGarden,
    [switch]$DevelopmentGarden,
    [switch]$WindGarden,
    [switch]$SurfaceGarden,
    [switch]$DetailGarden,
    [switch]$ArchitectureGarden,
    [switch]$PoseGarden,
    [switch]$GroundGarden,
    [switch]$ArticulationGarden,
    [switch]$MosaicGarden,
    [switch]$NeighborhoodGarden,
    [switch]$ChronicleGarden,
    [switch]$VascularGarden,
    [switch]$LifeSuiteGarden
)
if($LifeSuiteGarden){$DetailGarden=$true}
if($VascularGarden){$DetailGarden=$true}
if($ChronicleGarden){$FormGarden=$true}
if($ChronicleGarden -and $NeighborhoodGarden){throw 'CHOOSE_ONE_PROBE'}
if($NeighborhoodGarden){$FormGarden=$true}
if($MosaicGarden){$PoseGarden=$true}
if($MosaicGarden -and $ArticulationGarden){throw 'CHOOSE_ONE_PROBE'}
if($ArticulationGarden){$PoseGarden=$true}
if($PatternGarden -or $CatalogGarden -or $CreativeGarden -or $CultivarGarden -or $FormGarden -or $OrganGarden -or $DevelopmentGarden -or $WindGarden -or $SurfaceGarden -or $DetailGarden -or $ArchitectureGarden -or $PoseGarden -or $GroundGarden){$GardenProbe=$true}
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent $PSScriptRoot
$game='C:\Games\Steam\steamapps\common\RimWorld'
$exe=Join-Path $game 'RimWorld.exe'
$normal=Join-Path $env:USERPROFILE 'AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios'
$normalConfig=Join-Path $normal 'Config\ModsConfig.xml'
$normalPrefs=Join-Path $normal 'Config\Prefs.xml'
$normalSaves=Join-Path $normal 'Saves'
$log=Join-Path $normal 'Logs\Player.log'
$run=[Guid]::NewGuid().ToString('N')
$temp=Join-Path ([IO.Path]::GetTempPath()) ('hns-native-'+$run)
$stage=Join-Path $repo ('DevTools\Staged\Native17Package-'+$run)
$installed=Join-Path $game ('Mods\HNS-NativeFixture-'+$run)
$outDir=Join-Path $repo ('DevTools\TestResults\native-'+$run)
$receipt=[ordered]@{schemaVersion=1;run=$run;loadout=$Loadout;shaderProbe=[bool]$ShaderProbe;gardenProbe=[bool]$GardenProbe;patternGarden=[bool]$PatternGarden;catalogGarden=[bool]$CatalogGarden;creativeGarden=[bool]$CreativeGarden;cultivarGarden=[bool]$CultivarGarden;formGarden=[bool]$FormGarden;organGarden=[bool]$OrganGarden;developmentGarden=[bool]$DevelopmentGarden;windGarden=[bool]$WindGarden;surfaceGarden=[bool]$SurfaceGarden;detailGarden=[bool]$DetailGarden;vascularGarden=[bool]$VascularGarden;lifeSuiteGarden=[bool]$LifeSuiteGarden;architectureGarden=[bool]$ArchitectureGarden;poseGarden=[bool]$PoseGarden;articulationGarden=[bool]$ArticulationGarden;mosaicGarden=[bool]$MosaicGarden;neighborhoodGarden=[bool]$NeighborhoodGarden;chronicleGarden=[bool]$ChronicleGarden;groundGarden=[bool]$GroundGarden;status='BLOCKED';reason='NOT_STARTED';sourceHead=$null;version=$null;assemblySha256=$null;activePackages=@();optimizerIdentities=@();stages=@();fixtureLine=$null;processId=$null;testProcessStopped=$false;testPackageRemoved=$false;normalConfigUnchanged=$null;normalPrefsUnchanged=$null;normalSavesUnchanged=$null;isolatedSaveDirectory=$temp;startedUtc=$null;finishedUtc=$null}
$proc=$null; $installedByRun=$false; $before=$null; $started=$null; $capturedPlayerBaseline=$false
$utf8=New-Object Text.UTF8Encoding($false)
function Assert-Closed {
    if (@(Get-CimInstance Win32_Process -Filter "Name='RimWorld.exe' OR Name='RimWorldWin64.exe'").Count -ne 0) {throw 'RIMWORLD_ALREADY_RUNNING'}
}
function Hash-File([string]$path) {
    if(Test-Path -LiteralPath $path -PathType Leaf){return (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash}
    return '(missing)'
}
function Save-Inventory {
    if(!(Test-Path -LiteralPath $normalSaves)){return @()}
    @(Get-ChildItem -LiteralPath $normalSaves -File -Recurse|Sort-Object FullName|ForEach-Object {$_.FullName+'|'+$_.Length+'|'+$_.LastWriteTimeUtc.Ticks})
}
function Assert-NoPackage {
    $roots=@((Join-Path $game 'Mods'),(Join-Path $game 'MyMods'),(Join-Path (Split-Path -Parent (Split-Path -Parent $game)) 'workshop\content\294100'))
    foreach($base in $roots){
        if(!(Test-Path -LiteralPath $base -PathType Container)){continue}
        foreach($dir in Get-ChildItem -LiteralPath $base -Directory){
            $about=Join-Path $dir.FullName 'About\About.xml'
            if(Test-Path -LiteralPath $about -PathType Leaf){
                try{$id=([xml](Get-Content -LiteralPath $about -Raw)).ModMetaData.packageId}catch{continue}
                if($id -ieq 'lan.horticulture.novelseeds'){throw 'EXISTING_HORTICULTURE_INSTALLATION: refusing duplicate or overwrite'}
            }
        }
    }
}
New-Item -ItemType Directory -Path $outDir | Out-Null
try {
    if(($ShaderProbe -and $GardenProbe) -or (@(@($PatternGarden,$CatalogGarden,$CreativeGarden,$CultivarGarden,$FormGarden,$OrganGarden,$DevelopmentGarden,$WindGarden,$SurfaceGarden,$DetailGarden,$ArchitectureGarden,$PoseGarden,$GroundGarden) | Where-Object {$_}).Count -gt 1)){throw "CHOOSE_ONE_PROBE"}
    Assert-Closed
    Assert-NoPackage
    $head=(& git -C $repo rev-parse HEAD).Trim()
    if($LASTEXITCODE -ne 0 -or $head -notmatch '^[a-f0-9]{40}$' -or @(git -C $repo status --porcelain).Count -ne 0){throw 'CLEAN_SOURCE_REQUIRED'}
    $receipt.sourceHead=$head
    $version='0.1.0-alpha+g'+$head.Substring(0,12)
    $receipt.version=$version
    $pre=& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'Get-NativeTestLoadout.ps1') -Loadout $Loadout
    if($LASTEXITCODE -ne 0){throw 'LOADOUT_PREFLIGHT_FAILED'}
    $pre=$pre|Out-String|ConvertFrom-Json
    if($pre.status -ne 'PASS' -or !$pre.gameClosed){throw 'LOADOUT_OR_GAME_CLOSED_NOT_PROVEN'}
    $receipt.optimizerIdentities=@($pre.performanceIdentities)
    $packages=@($pre.packagesBeforeCandidate)+@('lan.horticulture.novelseeds')+@($pre.packagesAfterCandidate)
    $receipt.activePackages=$packages
    $receipt.gameVersion=$pre.gameVersion
    $before=@(Save-Inventory);$cfgBefore=Hash-File $normalConfig;$prefsBefore=Hash-File $normalPrefs;$capturedPlayerBaseline=$true
    [IO.File]::WriteAllText((Join-Path $outDir 'preservation-before.json'),([ordered]@{configHash=$cfgBefore;prefsHash=$prefsBefore;saves=@($before)}|ConvertTo-Json -Depth 3),$utf8)
    if(!(Test-Path -LiteralPath $normalConfig -PathType Leaf)){throw 'NORMAL_CONFIG_VERSION_UNAVAILABLE'}
    $gameVersion=[string]([xml](Get-Content -LiteralPath $normalConfig -Raw)).ModsConfigData.version
    if($gameVersion -notmatch '^1\.7\.\d+( rev\d+)?$'){throw 'NORMAL_CONFIG_VERSION_MISMATCH'}
    # Build only to ignored staging, never the tracked 1.6 assembly.
    & dotnet build (Join-Path $repo 'Source\HorticultureNovelSeeds.csproj') -c Release -p:NativeFixture=true "-p:InformationalVersion=$version" --nologo
    if($LASTEXITCODE -ne 0){throw 'CANDIDATE_BUILD_FAILED'}
    $dll=Join-Path $repo 'DevTools\Staged\Native17\Assemblies\HorticultureNovelSeeds.dll'
    $sha=Hash-File $dll
    if($sha -eq '(missing)'){throw 'CANDIDATE_DLL_MISSING'}
    $receipt.assemblySha256=$sha
    New-Item -ItemType Directory -Path (Join-Path $stage 'About'),(Join-Path $stage '1.7\Assemblies') -Force|Out-Null
    foreach($d in @('Defs','Languages','AutoMasks','Defaults')){
        Copy-Item -LiteralPath (Join-Path $repo ('1.6\'+$d)) -Destination (Join-Path $stage '1.7') -Recurse
    }
    # Strip only retired manual payloads from the staged 1.7 defaults.
    $defaults=Join-Path $stage '1.7\Defaults\DefaultConfiguration.xml'
    $doc=New-Object Xml.XmlDocument; $doc.PreserveWhitespace=$true; $doc.Load($defaults)
    $nodes=@($doc.SelectNodes('//plantMaskLayers|//plantMaskVariations|//produceMaskLayers|//usePlantMasks|//useProduceMasks'))
    foreach($node in $nodes){$null=$node.ParentNode.RemoveChild($node)}
    $doc.Save($defaults)
    $receipt.retiredManualDefaultNodes=$nodes.Count
    # The native publisher art direction supersedes legacy bundled presets.
    # This affects only the copied package, never player settings or 1.6.
    $artNodes=@($doc.SelectNodes("//visual|//additionalVisuals|//maskVisuals|//visualCustomized|//useCustomVisual"))
    foreach($node in $artNodes){$null=$node.ParentNode.RemoveChild($node)}
    $doc.Save($defaults)
    $receipt.retiredLegacyVisualNodes=$artNodes.Count
    Copy-Item -LiteralPath (Join-Path $repo '1.7\Defs\ThingDefs_Items.xml') -Destination (Join-Path $stage '1.7\Defs\ThingDefs_Items.xml') -Force
    Copy-Item -LiteralPath (Join-Path $repo '1.7\Defs\ProceduralPhenotypes.xml') -Destination (Join-Path $stage '1.7\Defs')
    if($ShaderProbe -or $GardenProbe){
        Copy-Item -LiteralPath (Join-Path $repo 'DevTools\ShaderPrototype\Defs\HNS_PhenotypeShaders.xml') -Destination (Join-Path $stage '1.7\Defs')
        Copy-Item -LiteralPath (Join-Path $repo 'DevTools\ShaderPrototype\Shaders') -Destination (Join-Path $stage '1.7') -Recurse
    }
    Copy-Item -LiteralPath $dll -Destination (Join-Path $stage '1.7\Assemblies\HorticultureNovelSeeds.dll')
    $about=@'
<ModMetaData>
  <name>Horticulture - Novel Seeds (isolated native test candidate)</name>
  <author>Lan</author>
  <packageId>lan.horticulture.novelseeds</packageId>
  <supportedVersions><li>1.7</li></supportedVersions>
  <modDependencies><li><packageId>brrainz.harmony</packageId><displayName>Harmony</displayName><steamWorkshopUrl>steam://url/CommunityFilePage/2009463077</steamWorkshopUrl></li></modDependencies>
  <loadAfter><li>brrainz.harmony</li></loadAfter>
  <description>Unreleased test candidate; do not enable in a real colony.</description>
</ModMetaData>
'@
    [IO.File]::WriteAllText((Join-Path $stage 'About\About.xml'),$about,$utf8)
    [IO.File]::WriteAllText((Join-Path $stage 'LoadFolders.xml'),'<loadFolders><v1.7><li>/</li><li>1.7</li></v1.7></loadFolders>',$utf8)
    [IO.File]::WriteAllText((Join-Path $stage '.hns-native-run'),$run,$utf8)
    Assert-Closed;Assert-NoPackage
    if(Test-Path -LiteralPath $installed){throw 'TEST_PACKAGE_DESTINATION_EXISTS'}
    Copy-Item -LiteralPath $stage -Destination $installed -Recurse
    $installedByRun=$true
    if((Hash-File (Join-Path $installed '1.7\Assemblies\HorticultureNovelSeeds.dll')) -ne $sha){throw 'INSTALLED_HASH_MISMATCH'}
    $configDir=Join-Path $temp 'Config'
    if(Test-Path -LiteralPath $temp){throw 'TEMP_ROOT_EXISTS'}
    New-Item -ItemType Directory -Path $configDir -Force|Out-Null
    $config=New-Object Xml.XmlDocument
    $config.LoadXml('<ModsConfigData><version/><activeMods/><knownExpansions/></ModsConfigData>')
    $config.DocumentElement.SelectSingleNode("version").InnerText=$gameVersion
    foreach($pkg in $packages){$node=$config.CreateElement('li');$node.InnerText=$pkg;$null=$config.DocumentElement.SelectSingleNode("activeMods").AppendChild($node)}
    foreach($dlc in @('royalty','ideology','biotech','anomaly','odyssey')){$node=$config.CreateElement('li');$node.InnerText='ludeon.rimworld.'+$dlc;$null=$config.DocumentElement.SelectSingleNode("knownExpansions").AppendChild($node)}
    $config.Save((Join-Path $configDir 'ModsConfig.xml'))
    $prefs='<PrefsData><runInBackground>True</runInBackground><fullscreen>False</fullscreen><screenWidth>1280</screenWidth><screenHeight>720</screenHeight><volumeMaster>0</volumeMaster><devMode>True</devMode><adaptiveTrainingEnabled>False</adaptiveTrainingEnabled><autosaveIntervalDays>7</autosaveIntervalDays></PrefsData>'
    if($GardenProbe){$prefs=$prefs.Replace("<screenWidth>1280</screenWidth><screenHeight>720</screenHeight>","<screenWidth>1600</screenWidth><screenHeight>900</screenHeight>")}
    [IO.File]::WriteAllText((Join-Path $configDir 'Prefs.xml'),$prefs,$utf8)
    if(Test-Path -LiteralPath $log -PathType Leaf){Copy-Item -LiteralPath $log -Destination (Join-Path $outDir 'prior-player.log')}
    Assert-Closed
    $psi=New-Object Diagnostics.ProcessStartInfo
    $psi.FileName=$exe;$psi.WorkingDirectory=$game;$psi.UseShellExecute=$false
    $psi.Arguments='"-savedatafolder='+$temp+'" -quicktest'
    $psi.EnvironmentVariables['HNS_NATIVE_RUN']=$run
    $psi.EnvironmentVariables['HNS_NATIVE_PACKAGES']=($packages -join ',')
    $psi.EnvironmentVariables['HNS_NATIVE_DLL']=$sha
    if($ShaderProbe){$psi.EnvironmentVariables['HNS_NATIVE_SHADER_PROBE']='1'}
    if($GardenProbe){$psi.EnvironmentVariables['HNS_NATIVE_GARDEN_PROBE']='1'}
    if($PatternGarden){$psi.EnvironmentVariables['HNS_NATIVE_PATTERN_GARDEN']='1'}
    if($CatalogGarden){$psi.EnvironmentVariables['HNS_NATIVE_CATALOG_GARDEN']='1'}
    if($CreativeGarden){$psi.EnvironmentVariables['HNS_NATIVE_CREATIVE_GARDEN']='1'}
    if($CultivarGarden){$psi.EnvironmentVariables['HNS_NATIVE_CULTIVAR_GARDEN']='1'}
    if($FormGarden){$psi.EnvironmentVariables['HNS_NATIVE_FORM_GARDEN']='1'}
    if($NeighborhoodGarden){$psi.EnvironmentVariables['HNS_NATIVE_NEIGHBORHOOD_GARDEN']='1'}
    if($ChronicleGarden){$psi.EnvironmentVariables['HNS_NATIVE_CHRONICLE_GARDEN']='1'}
    if($OrganGarden){$psi.EnvironmentVariables['HNS_NATIVE_ORGAN_GARDEN']='1'}
    if($DevelopmentGarden){$psi.EnvironmentVariables['HNS_NATIVE_DEVELOPMENT_GARDEN']='1'}
    if($WindGarden){$psi.EnvironmentVariables['HNS_NATIVE_WIND_GARDEN']='1'}
    if($SurfaceGarden){$psi.EnvironmentVariables['HNS_NATIVE_SURFACE_GARDEN']='1'}
    if($DetailGarden){$psi.EnvironmentVariables['HNS_NATIVE_DETAIL_GARDEN']='1'}
    if($VascularGarden){$psi.EnvironmentVariables['HNS_NATIVE_VASCULAR_GARDEN']='1'}
    if($LifeSuiteGarden){$psi.EnvironmentVariables['HNS_NATIVE_LIFE_SUITE_GARDEN']='1'}
    if($ArchitectureGarden){$psi.EnvironmentVariables['HNS_NATIVE_ARCHITECTURE_GARDEN']='1'}
    if($PoseGarden){$psi.EnvironmentVariables['HNS_NATIVE_POSE_GARDEN']='1'}
    if($ArticulationGarden){$psi.EnvironmentVariables['HNS_NATIVE_ARTICULATION_GARDEN']='1'}
    if($MosaicGarden){$psi.EnvironmentVariables['HNS_NATIVE_MOSAIC_GARDEN']='1'}
    if($GroundGarden){$psi.EnvironmentVariables['HNS_NATIVE_GROUND_GARDEN']='1'}
    $receipt.startedUtc=[DateTime]::UtcNow.ToString('o')
    $proc=[Diagnostics.Process]::Start($psi)
    $started=$proc.StartTime
    $receipt.processId=$proc.Id
    Write-Output ('FIXTURE_STARTED='+$run+' PID='+$proc.Id+' '+$version)
    $prefix='[HNS NativeFixture] '+$run+' '+$version+' '
    $deadline=[DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    $receipt.reason='FIXTURE_TIMEOUT'
    while([DateTime]::UtcNow -lt $deadline){
        if(Test-Path -LiteralPath $log -PathType Leaf){
            $lines=@(Get-Content -LiteralPath $log -Tail 2000 -ErrorAction SilentlyContinue)
            $header=@(Get-Content -LiteralPath $log -TotalCount 100 -ErrorAction SilentlyContinue)
            $all=$lines -join [char]10
            if(($header -join [char]10).Contains($temp) -and $all.Contains('Caught exception while loading play data')){
                $receipt.status='FAIL';$receipt.reason='GAME_RECOVERED_FROM_MOD_LOADING_FAILURE';break
            }
            foreach($line in $lines|Where-Object {$_.Contains($prefix+'STAGE_PASS ')}){
                $stageName=$line.Substring($line.IndexOf($prefix+'STAGE_PASS ')+($prefix+'STAGE_PASS ').Length).Trim()
                if($receipt.stages -cnotcontains $stageName){$receipt.stages+= $stageName;Write-Output ('STAGE_PASS '+$stageName)}
            }
            $final=$lines|Where-Object {$_.Contains($prefix+'PASS ') -or $_.Contains($prefix+'FAIL ')}|Select-Object -Last 1
            if($final){
                $receipt.fixtureLine=[string]::new(([string]$final).ToCharArray())
                $result=$final.Substring($final.IndexOf($prefix)+$prefix.Length)
                if($result -match '^(PASS|FAIL)\s+(.+)$'){$receipt.status=$Matches[1];$receipt.reason=$Matches[2]}
                $requiredStages=if($LifeSuiteGarden){'Startup,GardenSetup,GardenPanel1,GardenPanel2,GardenPanel3,GardenPanel4,GardenPanel5,GardenPanel6,GardenPanel7,GardenPanel8,GardenInventory'}elseif($ChronicleGarden -or $NeighborhoodGarden){'Startup,GardenSetup,GardenPanel1,GardenPanel2,GardenPanel3,GardenPanel4,GardenPanel5,GardenInventory'}elseif($GroundGarden){'Startup,GardenSetup,GardenPanel1,GardenPanel2,GardenInventory'}elseif($PoseGarden){'Startup,GardenSetup,GardenPanel1,GardenPanel2,GardenPanel3,GardenPanel4,GardenPanel5,GardenInventory'}elseif($ArchitectureGarden){'Startup,GardenSetup,GardenPanel1,GardenPanel2,GardenInventory'}elseif($DetailGarden){'Startup,GardenSetup,GardenPanel1,GardenPanel2,GardenPanel3,GardenInventory'}elseif($SurfaceGarden){'Startup,GardenSetup,GardenPanel1,GardenPanel2,GardenPanel3,GardenPanel4,GardenInventory'}elseif($WindGarden){'Startup,GardenSetup,GardenPanel1,GardenPanel2,GardenPanel3,GardenPanel4,GardenPanel5,GardenPanel6,GardenPanel7,GardenPanel8,GardenPanel9,GardenPanel10,GardenPanel11,GardenPanel12,GardenInventory'}elseif($DevelopmentGarden){'Startup,GardenSetup,GardenPanel1,GardenPanel2,GardenPanel3,GardenInventory'}elseif($FormGarden -or $OrganGarden){'Startup,GardenSetup,GardenPanel1,GardenPanel2,GardenInventory'}elseif($CultivarGarden){'Startup,GardenSetup,GardenPanel1,GardenPanel2,GardenPanel3,GardenInventory'}elseif($CatalogGarden){'Startup,GardenSetup,GardenPanel1,GardenPanel2,GardenPanel3,GardenPanel4,GardenPanel5,GardenPanel6,GardenInventory'}elseif($CreativeGarden){'Startup,GardenSetup,GardenPanel1,GardenPanel2,GardenPanel3,GardenPanel4,GardenPanel5,GardenInventory'}elseif($GardenProbe){'Startup,GardenSetup,GardenPanel1,GardenPanel2,GardenPanel3,GardenPanel4,GardenInventory'}elseif($ShaderProbe){'Startup,ShaderPixelParity,ShaderMaskParity,ShaderFallback,ShaderField'}else{'Startup,HarvestAndSeedRecovery,Registration,CultivarHarvest,SaveReload'}
                if($receipt.status -eq 'PASS' -and (($receipt.stages -join ',') -ne $requiredStages)){$receipt.status='FAIL';$receipt.reason='INCOMPLETE_STAGE_EVIDENCE'}
                break
            }
        }
        if($proc.HasExited){$receipt.reason='TEST_PROCESS_EXITED';break}
        Start-Sleep -Milliseconds 500
    }
}catch{$receipt.status='BLOCKED';$receipt.reason=$_.Exception.Message}
finally{
    if($proc -ne $null){
        try {
            $proc.Refresh()
            if(!$proc.HasExited){
                $current=Get-CimInstance Win32_Process -Filter ("ProcessId="+$proc.Id)
                if($current -and $current.ExecutablePath -ieq $exe -and $proc.StartTime -eq $started){
                    $proc.Kill();$null=$proc.WaitForExit(10000)
                }else{$receipt.status='FAIL';$receipt.reason='OWNED_PROCESS_IDENTITY_LOST'}
            }
            $receipt.testProcessStopped=$proc.HasExited
        }catch{$receipt.status='FAIL';$receipt.reason='TEST_PROCESS_CLEANUP_FAILED '+$_.Exception.Message}
    }else{$receipt.testProcessStopped=$true}
    if($installedByRun -and $receipt.testProcessStopped){
        try{
            Assert-Closed
            $marker=Join-Path $installed '.hns-native-run'
            if((Get-Content -LiteralPath $marker -Raw) -cne $run -or (Hash-File (Join-Path $installed '1.7\Assemblies\HorticultureNovelSeeds.dll')) -ne $receipt.assemblySha256){throw 'PACKAGE_CUSTODY_LOST'}
            Remove-Item -LiteralPath $installed -Recurse -Force
            $receipt.testPackageRemoved=!(Test-Path -LiteralPath $installed)
        }catch{$receipt.status='FAIL';$receipt.reason='PACKAGE_CLEANUP_FAILED '+$_.Exception.Message}
    }elseif(!$installedByRun){$receipt.testPackageRemoved=$true}
    if($capturedPlayerBaseline){
        $receipt.normalConfigUnchanged=(Hash-File $normalConfig) -eq $cfgBefore
        $receipt.normalPrefsUnchanged=(Hash-File $normalPrefs) -eq $prefsBefore
        $receipt.normalSavesUnchanged=((@(Save-Inventory) -join [char]10) -ceq ($before -join [char]10))
        if(!$receipt.normalConfigUnchanged -or !$receipt.normalPrefsUnchanged -or !$receipt.normalSavesUnchanged){$receipt.status='FAIL';$receipt.reason='PLAYER_DATA_PRESERVATION_NOT_PROVEN'}
    }
    if($proc -ne $null -and $receipt.testProcessStopped -and (Test-Path -LiteralPath $log -PathType Leaf)){Copy-Item -LiteralPath $log -Destination (Join-Path $outDir 'player.log')}
    [IO.File]::WriteAllText((Join-Path $outDir 'preservation-result.json'),([ordered]@{testProcessStopped=$receipt.testProcessStopped;testPackageRemoved=$receipt.testPackageRemoved;normalConfigUnchanged=$receipt.normalConfigUnchanged;normalPrefsUnchanged=$receipt.normalPrefsUnchanged;normalSavesUnchanged=$receipt.normalSavesUnchanged}|ConvertTo-Json -Depth 2),$utf8)
    if($ShaderProbe -and (Test-Path -LiteralPath $temp)){
        foreach($file in Get-ChildItem -LiteralPath $temp -Filter 'HNS-shader-*.png' -File){Copy-Item -LiteralPath $file.FullName -Destination $outDir}
    }
    if($GardenProbe -and (Test-Path -LiteralPath $temp)){
        foreach($file in Get-ChildItem -LiteralPath $temp -Filter 'HNS-garden-*' -File){Copy-Item -LiteralPath $file.FullName -Destination $outDir}
    }
    $receipt.finishedUtc=[DateTime]::UtcNow.ToString('o')
    $json=$receipt|ConvertTo-Json -Depth 4
    [IO.File]::WriteAllText((Join-Path $outDir 'receipt.json'),$json,$utf8)
    Write-Output $json
}
if($receipt.status -ne 'PASS'){exit 2}
