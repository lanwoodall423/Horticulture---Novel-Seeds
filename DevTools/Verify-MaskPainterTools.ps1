$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$operations = Get-Content -Raw (Join-Path $root 'Source\MaskPainterOperations.cs') -ErrorAction SilentlyContinue
$editor = Get-Content -Raw (Join-Path $root 'Source\ProduceMasking.cs')
$settings = Get-Content -Raw (Join-Path $root 'Source\Settings.cs')
$bridge = Get-Content -Raw (Join-Path $root 'DevTools\BridgeAdapter\HorticultureBridgeAdapter.cs')

$checks = [ordered]@{
    'grow and shrink support configurable distance' = $operations -match 'Grow\(' -and $operations -match 'Shrink\(' -and $editor -match 'selectionAmount'
    'smooth and binary-compatible feather operations exist' = $operations -match 'Smooth\(' -and $operations -match 'Feather\('
    'paint add remove replace modes exist' = $editor -match 'PaintSelectionMode' -and $editor -match 'Add' -and $editor -match 'Remove' -and $editor -match 'Replace'
    'brush size remains adjustable' = $editor -match 'brushSize' -and $editor -match 'HorizontalSlider'
    'region tool honors shift add and control remove' = $editor -match 'regionSelect' -and $editor -match 'current\.shift' -and $editor -match 'current\.control' -and $operations -match 'ConnectedTextureRegion'
    'cleanup supports tiny islands holes and largest component' = $operations -match 'RemoveSmallComponents' -and $operations -match 'FillHoles' -and $operations -match 'KeepLargest'
    'smart expansion follows texture boundaries' = $operations -match 'SmartExpand' -and $operations -match 'ColorDistance' -and $operations -match 'alpha'
    'live preview has original mask and final modes' = $editor -match 'MaskPreviewMode' -and $editor -match 'Original' -and $editor -match 'Final' -and $editor -match 'FinalPreviewTexture'
    'all semantic channels can be locked' = $editor -match 'channelLocks' -and $editor -match 'ToggleChannelLock' -and $editor -match '!SelectedLocked'
    'keyboard workflow covers tools layers operations and history' = $editor -match 'HandleEditorShortcuts' -and $editor -match 'KeyCode\.Alpha1' -and $editor -match 'KeyCode\.LeftBracket' -and $editor -match 'KeyCode\.Z'
    'masks can copy and project between texture variations' = $editor -match 'CopyMaskToVariation' -and $editor -match 'ProjectMaskToVariation' -and $operations -match 'Project\('
    'validation covers transparency overlaps empty fragments and gaps' = $operations -match 'MaskValidationResult' -and $operations -match 'transparentPixels' -and $operations -match 'overlappingPixels' -and $operations -match 'emptyChannels' -and $operations -match 'tinyFragments' -and $operations -match 'unmaskedVisiblePixels'
    'validation state clears when masks or variations change' = $editor -match 'validationResult = null' -and $editor -match 'ResetToAutoMask[\s\S]*?validationResult = null'
    'new operations have deterministic regression coverage' = $operations -match 'MaskPainterOperationsRegression' -and $bridge -match 'MaskPainterOperationsRegression'
    'manual mask serialization keys remain unchanged' = $settings -match '"plantMaskLayers"' -and $settings -match '"plantMaskVariations"'
    'three semantic layer contract remains unchanged' = $editor -match 'new VisualMaskLayerRecord \{ name = "Produce" \}' -and $editor -match 'new VisualMaskLayerRecord \{ name = "Leaves" \}' -and $editor -match 'new VisualMaskLayerRecord \{ name = "Stem" \}'
}

$failed = @($checks.GetEnumerator() | Where-Object { -not $_.Value })
if ($failed.Count) {
    $failed | ForEach-Object { Write-Error ("FAILED: " + $_.Key) }
    exit 1
}
Write-Output ("Mask painter tool verification passed ({0} checks)." -f $checks.Count)
