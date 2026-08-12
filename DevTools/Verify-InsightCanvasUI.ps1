$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$about = Get-Content -Raw (Join-Path $root 'About\About.xml')
$project = Get-Content -Raw (Join-Path $root 'Source\HorticultureNovelSeeds.csproj')
$mod = Get-Content -Raw (Join-Path $root 'Source\ModCore.cs')
$compat = Get-Content -Raw (Join-Path $root 'Source\ModernSettingsUI.cs')
$ui = Get-Content -Raw (Join-Path $root 'Source\InsightSettingsUI.cs')
$architecture = Get-Content -Raw (Join-Path $root 'docs\UI_ARCHITECTURE.md')

$checks = [ordered]@{
    'Insight Canvas dependency and load order' = $about -match 'lan\.insightcanvas' -and $about -match '<loadAfter>'
    'portable framework reference' = $project -match 'InsightCanvas\.dll' -and $project -match '<Private>false</Private>'
    'framework provenance recorded' = $project -match 'InsightCanvasVersion>2\.0\.0' -and $project -match '93a09005fa15190009daee625352cf4004974472' -and $project -match 'DFEC9DB76B6ABD7442E82A5029005CE09DECC281CC34FB37C080FD015458A613'
    'framework DLL is not bundled' = -not (Test-Path (Join-Path $root '1.6\Assemblies\InsightCanvas.dll'))
    'mod owns document lifecycle' = $mod -match 'InsightSettingsDocument' -and $mod -match 'settingsDocument\.Draw'
    'compatibility facade has no legacy renderer' = $compat -notmatch 'Widgets\.' -and $compat -notmatch 'static HashSet' -and $compat -notmatch 'static Vector2'
    'document and botanical theme' = $ui -match 'InsightUiDocument' -and $ui -match 'InsightTheme\.Default\.Clone' -and $ui -match 'theme\.Selected'
    'duplicate diagnostics' = $ui -match 'TrackDuplicateIds = true' -and $ui -match 'Diagnostics\.DuplicateIds'
    'all production pages' = @('Gameplay', 'Plants & Traits', 'Visuals', 'Profiles', 'Advanced' | ForEach-Object { $ui -match $_ }) -notcontains $false
    'document-owned searches and scoped rows' = $ui -match 'SearchField' -and $ui -match 'SetSearch' -and $ui -match 'InsightUi\.Scope'
    'bounded virtualized registries' = $ui -match 'VirtualList' -and $ui -match 'CacheLimit' -and $ui -match 'Overscan'
    'responsive workspaces' = $ui -match 'InsightUi\.Split' -and $ui -match 'SetSplitOrientation' -and $ui -match 'InsightUiOrientation\.Vertical'
    'direct authoritative bindings' = $ui -match 'FloatControl\([\s\S]*?settings\.' -and $ui -match 'ToggleControl\([\s\S]*?settings\.' -and $ui -match 'settingsChanged'
    'document feedback and destructive confirmation' = $ui -match 'uiDocument\.Toasts\.Show' -and $ui -match 'Dialog_MessageBox\.CreateConfirmation'
    'visual generation and review actions' = $ui -match 'Generate Missing Auto-Masks' -and $ui -match 'Review Mask Queue' -and $ui -match 'InitializeAndGenerateMissing'
    'provenance documentation' = $architecture -match 'DFEC9DB76B6ABD7442E82A5029005CE09DECC281CC34FB37C080FD015458A613' -and $architecture -match 'no fallback'
}

$failed = @($checks.GetEnumerator() | Where-Object { -not $_.Value })
if ($failed.Count) {
    $failed | ForEach-Object { Write-Error ('FAILED: ' + $_.Key) }
    exit 1
}
Write-Output ('Insight Canvas UI verification passed ({0} checks).' -f $checks.Count)
