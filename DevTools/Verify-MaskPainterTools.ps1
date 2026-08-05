$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$operations = Get-Content -Raw (Join-Path $root 'Source\MaskPainterOperations.cs') -ErrorAction SilentlyContinue
$editor = Get-Content -Raw (Join-Path $root 'Source\ProduceMasking.cs')
$settings = Get-Content -Raw (Join-Path $root 'Source\Settings.cs')
$bridge = Get-Content -Raw (Join-Path $root 'DevTools\BridgeAdapter\HorticultureBridgeAdapter.cs')
$projection = Get-Content -Raw (Join-Path $root 'Source\MaskProjection.cs')
$identity = Get-Content -Raw (Join-Path $root 'Source\MaskTextureIdentity.cs')
$auto = Get-Content -Raw (Join-Path $root 'Source\AutoPlantMasks.cs')
$review = Get-Content -Raw (Join-Path $root 'Source\MaskReviewQueue.cs')
$breeding = Get-Content -Raw (Join-Path $root 'Source\BreedingMixRegression.cs')
$modern = Get-Content -Raw (Join-Path $root 'Source\ModernSettingsUI.cs')
$patches = Get-Content -Raw (Join-Path $root 'Source\Patches.cs')
$resource = Get-Content -Raw (Join-Path $root 'Source\ResourceNeed.cs')
$traitRegression = Get-Content -Raw (Join-Path $root 'Source\TraitCatalogRegression.cs')

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
    'fill unmasked excludes transparent and assigned pixels' = $operations -match 'FillUnmasked\(' -and $operations -match 'VisibleAlpha' -and $operations -match 'assigned' -and $editor -match 'Fill Unmasked'
    'fill unmasked is lock-aware and one transaction' = $operations -match 'targetLocked' -and $editor -match 'FillUnmaskedPixels' -and $editor -match 'CompleteImmediateChange\(before, changed\)'
    'validation retains separate issue categories' = $operations -match 'transparentPaintIssues' -and $operations -match 'overlapIssues' -and $operations -match 'tinyFragmentIssues' -and $operations -match 'unmaskedVisibleIssues'
    'validation navigation groups components and centers issues' = $operations -match 'MaskValidationNavigator' -and $operations -match 'MaskIssueComponent' -and $editor -match 'Previous Issue' -and $editor -match 'Next Issue' -and $editor -match 'CenterOnCurrentValidationIssue'
    'region labeling documents replace add and remove' = $editor -match 'normal click replaces the selected channel' -and $editor -match 'Shift adds; Ctrl removes'
    'validation state clears when masks or variations change' = $editor -match 'validationResult = null' -and $editor -match 'ResetToAutoMask[\s\S]*?validationResult = null'
    'new operations have deterministic regression coverage' = $operations -match 'MaskPainterOperationsRegression' -and $bridge -match 'MaskPainterOperationsRegression'
    'manual mask serialization keys remain unchanged' = $settings -match '"plantMaskLayers"' -and $settings -match '"plantMaskVariations"'
    'three semantic layer contract remains unchanged' = $editor -match 'new VisualMaskLayerRecord \{ name = "Produce" \}' -and $editor -match 'new VisualMaskLayerRecord \{ name = "Leaves" \}' -and $editor -match 'new VisualMaskLayerRecord \{ name = "Stem" \}'
    'projection is preview-only before settings mutation' = $editor -match 'projectionPreview' -and $editor -match 'ApplyProjectionPreview' -and $editor -match 'CancelProjectionPreview' -and $editor -match 'SemanticMaskProjection\.Build'
    'semantic projection scores spatial and visual correspondence' = $projection -match 'RelativeX' -and $projection -match 'ColorDistance' -and $projection -match 'Compactness' -and $projection -match 'Adjacency' -and $projection -match 'Connectivity' -and $projection -match '0\.30f' -and $projection -match '0\.20f' -and $projection -match '0\.15f' -and $projection -match '0\.10f'
    'projection preserves target transparency and channel exclusivity' = $projection -match 'VisibleAlpha' -and $projection -match 'PaintTargetPixel' -and $projection -match 'ApplyAccepted' -and $projection -match 'accepted\[channel\]' -and $projection -match 'blocked'
    'projection starts with one shared transformed source frame and resolves overlap deterministically' = $projection -match 'InitialCandidates' -and $projection -match 'Bounds sourceBounds = MaskBounds\(sourceLayers\)' -and $projection -match 'CandidateLayers\[channel\] = initialCandidates\[channel\]\.Clone' -and $projection -match 'ArbitrateCandidateOverlaps' -and $projection -match 'MinimumArbitrationSeparation'
    'accepted channels clear before painting while rejected channels remain authoritative' = $projection -match 'Clear all accepted channels before evaluating occupancy' -and $projection -match 'if \(accepted\[channel\]\) result\[channel\]\.Clear' -and $projection -match '!accepted\[other\].*result\[other\]\.IsPainted'
    'projection reports channel confidence and mutation counts' = $projection -match 'Confidence' -and $projection -match 'AddedPixels' -and $projection -match 'RemovedPixels' -and $projection -match 'Conflicts' -and $projection -match 'RemainingUnmaskedVisiblePixels' -and $projection -match 'AmbiguousAssignments'
    'exact texture identity is def-independent and deterministic' = $identity -match 'PixelFingerprint' -and $identity -match 'texture\.width' -and $identity -match 'texture\.height' -and $identity -match 'OrientationFor' -and $identity -match 'ClearCache'
    'normal rendering uses cached identity while startup/editor may precompute readbacks' = $identity -match 'TryGetCached' -and $identity -match 'allowRead' -and $identity -match 'PreloadPlantTextures' -and $auto -match 'allowIdentityGeneration'
    'shared manual conflicts are ambiguous and manual authority remains first' = $identity -match 'entry\.ambiguous' -and $editor -match 'CurrentUsesSharedManual' -and $editor -match 'PromoteAutoToManual'
    'batch review groups and sorts exact texture work' = $review -match 'MaskReviewQueueBuilder' -and $review -match 'IdentityKey' -and $review -match 'ThenBy\(row => row\.Confidence\)' -and $review -match 'ThenByDescending\(row => row\.IssueCount\)'
    'batch review validates lazily by identity and mask hash' = $review -match 'ValidationCache' -and $review -match 'ValidationKey' -and $review -match 'MaskPainterOperations\.Validate' -and $modern -match 'Review Mask Queue'
    'review opens existing painter and refreshes after close' = $review -match 'new Dialog_PlantMasks' -and $editor -match 'Action reviewRefresh' -and $editor -match 'reviewRefresh\?\.Invoke'
    'projection regression measures per-channel coverage and IoU' = (Test-Path (Join-Path $root 'Source\MaskProjectionRegression.cs')) -and (Get-Content -Raw (Join-Path $root 'Source\MaskProjectionRegression.cs')) -match 'IntersectionOverUnion' -and (Get-Content -Raw (Join-Path $root 'Source\MaskProjectionRegression.cs')) -match 'Coverage' -and (Get-Content -Raw (Join-Path $root 'Source\MaskProjectionRegression.cs')) -match 'ConfidenceRegression'
    'projection regression covers translation scale conflicts rejection cancel undo and identity' = $projection -match 'MaskProjectionRegression' -or (Test-Path (Join-Path $root 'Source\MaskProjectionRegression.cs'))
    'projection application uses one existing editor history transaction' = $editor -match 'ApplyProjectionPreview\([\s\S]*?CaptureHistory\(0, projectionTargetVariation\)[\s\S]*?CompleteImmediateChange\(before, true\)' -and $editor -match 'BeginProjectionPreviewForRegression' -and $editor -match 'UndoForRegression' -and $editor -match 'RedoForRegression'
    'breeding mix diagnostic covers empty staggered and complete harvest scenarios' = $breeding -match 'initialEmptyField' -and $breeding -match 'partialStaggeredHarvest' -and $breeding -match 'completeHarvestReplant' -and $bridge -match 'HNS_BREEDING_MIX_DIAGNOSTIC'
    'manual edits invalidate shared identity indexes' = $editor -match 'SharedManualMaskCache\.Invalidate' -and $settings -match 'ReplaceMasks' -and $settings -match 'SharedManualMaskCache\.Invalidate'
    'perennial harvest transpiler selects the exact helper overload' = $patches -match 'AccessTools\.Method\([\s\S]*typeof\(bool\), typeof\(Plant\), typeof\(PlantDestructionMode\)'
    'projection confidence is bounded and penalizes ambiguity and coverage gaps' = $projection -match 'BoundedConfidence' -and $projection -match 'Mathf\.Clamp01' -and $projection -match 'ambiguityFree' -and (Get-Content -Raw (Join-Path $root 'Source\MaskProjectionRegression.cs')) -match 'ambiguousCannotImprove' -and (Get-Content -Raw (Join-Path $root 'Source\MaskProjectionRegression.cs')) -match 'missingCoveragePenalty'
    'projection conflicts use the full arbitration domain without channel priority' = $projection -match 'ArbitrationDomainPixels' -and $projection -match 'UnresolvedConflictPixels' -and $projection -match 'CountPixels\(arbitrationDomain, ambiguousPixels\)' -and $projection -match 'MinimumArbitrationSeparation' -and $projection -match 'WinnerForPixel'
    'projection confidence is channel-local and empty channels are zero' = $projection -match 'sourcePixelsForChannel' -and $projection -match 'channelResult\.SpatialAgreement' -and $projection -match 'channelResult\.SemanticAgreement' -and $projection -match 'hasSourcePixels' -and $projection -match 'if \(!hasSourcePixels\) return 0f' -and (Get-Content -Raw (Join-Path $root 'Source\MaskProjectionRegression.cs')) -match 'emptyIsZero' -and (Get-Content -Raw (Join-Path $root 'Source\MaskProjectionRegression.cs')) -match 'channelLocal'
    'projection regression covers shaded multi-island and full ambiguous regions' = (Get-Content -Raw (Join-Path $root 'Source\MaskProjectionRegression.cs')) -match 'ShadedMultiIslandRegression' -and (Get-Content -Raw (Join-Path $root 'Source\MaskProjectionRegression.cs')) -match 'LargeAmbiguousRegionRegression' -and (Get-Content -Raw (Join-Path $root 'Source\MaskProjectionRegression.cs')) -match 'ArbitrationDomainPixels == expected'
    'breeding diagnostics reuse production donor and mix helpers' = $breeding -match 'MakeCrossPollinationDonor' -and $breeding -match 'AggregateCrossPollinationDonors' -and $breeding -match 'SelectWeightedCrossPollinationDonor' -and $breeding -match 'SelectBreedingMixVariety' -and $breeding -notmatch '397.*7919'
    'resource production path covers work eligibility payment and retry behavior' = $resource -match 'CanStartJob' -and $resource -match 'EvaluatePayment' -and $resource -match 'TryMakePreToilReservations' -and $resource -match 'SatisfyResource' -and $traitRegression -match 'ResourceProductionRegression' -and $traitRegression -match 'noDoublePayment' -and $bridge -match 'HNS_RESOURCE_JOB_REGRESSION' -and $bridge -match 'ResourceJobRegression'
    'resource payment diagnostics cover exact payment and growth gate' = $traitRegression -match 'ConsumedUnits' -and $traitRegression -match 'ApplyResourceGrowthGate' -and $resource -match 'CanReserveStack' -and $resource -match 'EvaluatePayment'
}

$failed = @($checks.GetEnumerator() | Where-Object { -not $_.Value })
if ($failed.Count) {
    $failed | ForEach-Object { Write-Error ("FAILED: " + $_.Key) }
    exit 1
}
Write-Output ("Mask painter tool verification passed ({0} checks)." -f $checks.Count)
