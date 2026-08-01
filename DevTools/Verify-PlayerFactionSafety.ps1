param([string]$Root = (Split-Path -Parent $PSScriptRoot))

$ErrorActionPreference = 'Stop'
$sourcePath = Join-Path $Root 'Source/PlantKnowledge.cs'
$source = Get-Content $sourcePath -Raw
$gainMatch = [regex]::Match(
    $source,
    'private static void Gain\([\s\S]*?\n        \}',
    [System.Text.RegularExpressions.RegexOptions]::CultureInvariant)

if (!$gainMatch.Success) {
    throw 'Could not locate PlantKnowledgeUtility.Gain.'
}

$gain = $gainMatch.Value
if ($gain -match 'Faction\.OfPlayer') {
    throw 'Knowledge gain still queries the global player faction.'
}
if ($gain -notmatch 'pawn\?\.Faction\?\.def\?\.isPlayer != true') {
    throw 'Knowledge gain does not safely reject pawns without a player faction.'
}

Write-Host 'Player faction safety check passed.'
