param([string]$Root = (Split-Path -Parent $PSScriptRoot))

$ErrorActionPreference = 'Stop'
$failed = @()
$ui = Get-Content (Join-Path $Root 'Source/TraitColorUI.cs') -Raw
$families = Get-Content (Join-Path $Root 'Source/TraitFamilies.cs') -Raw
$utility = Get-Content (Join-Path $Root 'Source/NovelSeedUtility.cs') -Raw

if ($ui -notmatch 'trait\.tintRed' -or $ui -notmatch 'trait\.tintGreen' -or $ui -notmatch 'trait\.tintBlue') {
    $failed += 'swatch does not use inherited trait color'
}
if ($ui -notmatch '<color=#' -or $ui -notmatch '\\u25a0') { $failed += 'visible rich-text swatch' }
if ($ui -notmatch 'trait\.LabelCap') { $failed += 'localized color label preservation' }
if ($families -match 'description\s*=\s*"[^"]*RGB:') { $failed += 'generated description exposes RGB' }
if ($utility -match '"RGB " \+ tintRed') { $failed += 'stat description exposes RGB' }
if ($utility -notmatch 'TraitColorUI\.Summary') { $failed += 'inspect and summary integration' }
if ($utility -notmatch 'TraitColorUI\.Swatch') { $failed += 'stat swatch integration' }
if ($ui -match 'Scribe_') { $failed += 'presentation helper changes persistence' }

if ($failed.Count -gt 0) { throw 'Color trait UI checks failed: ' + ($failed -join ', ') }
Write-Host 'Color trait UI checks passed (8 checks).'
