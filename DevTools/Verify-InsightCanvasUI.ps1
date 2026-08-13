$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$about = Get-Content -Raw (Join-Path $root 'About\About.xml')
$project = Get-Content -Raw (Join-Path $root 'Source\HorticultureNovelSeeds.csproj')
$runtimeProject = Get-Content -Raw (Join-Path $root 'DevTools\RuntimeTests\HorticultureNovelSeeds.RuntimeTests.csproj')
$mod = Get-Content -Raw (Join-Path $root 'Source\ModCore.cs')
$compat = Get-Content -Raw (Join-Path $root 'Source\ModernSettingsUI.cs')
$ui = Get-Content -Raw (Join-Path $root 'Source\InsightSettingsUI.cs')
$runtime = Get-Content -Raw (Join-Path $root 'DevTools\RuntimeTests\RuntimeScenarioSuite.cs')
$architecture = Get-Content -Raw (Join-Path $root 'docs\UI_ARCHITECTURE.md')
$plantInspector = Get-Content -Raw (Join-Path $root 'Source\PlantVarietyTab.cs')
$produceInspector = Get-Content -Raw (Join-Path $root 'Source\ProduceVarietyTab.cs')

$checks = [ordered]@{
    'Insight Canvas dependency and load order' = $about -match 'lan\.insightcanvas' -and $about -match '<loadAfter>'
    'portable framework reference' = $project -match 'InsightCanvas\.dll' -and $project -match '<Private>false</Private>'
    'framework provenance recorded' = $project -match 'InsightCanvasVersion>2\.1\.0' -and $project -match '93a09005fa15190009daee625352cf4004974472' -and $project -match 'E8D163B6A2B39EB80BBF8A5EA5AA0B8A80481D69A8CEE1D74526548D0A28C011'
    'framework DLL is not bundled' = -not (Test-Path (Join-Path $root '1.6\Assemblies\InsightCanvas.dll'))
    'runtime UI tests reference framework without copying' = $runtimeProject -match '<Reference Include="InsightCanvas">' -and $runtimeProject -match '<Private>false</Private>'
    'mod owns document lifecycle' = $mod -match 'InsightSettingsDocument' -and $mod -match 'settingsDocument\.Draw'
    'compatibility facade has no legacy renderer' = $compat -notmatch 'Widgets\.' -and $compat -notmatch 'static HashSet' -and $compat -notmatch 'static Vector2'
    'document and botanical theme' = $ui -match 'InsightUiDocument' -and $ui -match 'InsightTheme\.Default\.Clone' -and $ui -match 'theme\.Selected'
    'duplicate diagnostics' = $ui -match 'TrackDuplicateIds = true' -and $ui -match 'Diagnostics\.DuplicateIds'
    'all production pages' = @('Gameplay', 'Plants & Traits', 'Visuals', 'Profiles', 'Advanced' | ForEach-Object { $ui -match $_ }) -notcontains $false
    'document-owned searches and scoped rows' = $ui -match 'SearchField' -and $ui -match 'SetSearch' -and $ui -match 'InsightUi\.Scope'
    'bounded virtualized registries' = $ui -match 'VirtualList' -and $ui -match 'CacheLimit' -and $ui -match 'Overscan'
    'responsive workspaces' = $ui -match 'InsightUi\.Split' -and $ui -match 'workspaceGroupsSplit' -and $ui -match 'workspacePlantsSplit' -and $ui -match 'workspaceTraitsSplit' -and $ui -match 'profilesSplit' -and $ui -match 'workspaceGroupsOrientation' -and $ui -match 'InsightUiOrientation\.Vertical'
    'advanced gameplay disclosure and dependent controls' = $ui -match 'gameplay\.advanced-inheritance' -and $ui -match 'UpdateDependentControlState' -and $ui -match 'enableTraitBalancing'
    'direct authoritative bindings' = $ui -match 'FloatControl\([\s\S]*?settings\.' -and $ui -match 'ToggleControl\([\s\S]*?settings\.' -and $ui -match 'settingsChanged'
    'document feedback and destructive confirmation' = $ui -match 'uiDocument\.Toasts\.Show' -and $ui -match 'Dialog_MessageBox\.CreateConfirmation'
    'visual generation and review actions' = $ui -match 'Generate Missing Auto-Masks' -and $ui -match 'Review Mask Queue' -and $ui -match 'InitializeAndGenerateMissing'
    'embedded inspectors expose Horticulture navigation' = $plantInspector -match 'ActionLabel\s*=\s*"Open in Horticulture"' -and $produceInspector -match 'ActionLabel\s*=\s*.*Open in Horticulture' -and $produceInspector -match 'SourceVarietyIds'
    'runtime UI assertions' = @('ux-insight-navigation-and-search', 'ux-insight-selections-and-group-action', 'ux-insight-bindings-and-dependent-controls', 'ux-insight-responsive-accessibility', 'ux-insight-diagnostics' | ForEach-Object { $runtime -match $_ }) -notcontains $false
    'provenance documentation' = $architecture -match 'E8D163B6A2B39EB80BBF8A5EA5AA0B8A80481D69A8CEE1D74526548D0A28C011' -and $architecture -match 'no fallback'
}

$failed = @($checks.GetEnumerator() | Where-Object { -not $_.Value })
if ($failed.Count) {
    $failed | ForEach-Object { Write-Error ('FAILED: ' + $_.Key) }
    exit 1
}
Write-Output ('Insight Canvas UI verification passed ({0} checks).' -f $checks.Count)
