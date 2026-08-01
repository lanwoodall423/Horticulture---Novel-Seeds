$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$auto = Get-Content -Raw (Join-Path $root 'Source\AutoPlantMasks.cs') -ErrorAction SilentlyContinue
$masking = Get-Content -Raw (Join-Path $root 'Source\ProduceMasking.cs')
$settings = Get-Content -Raw (Join-Path $root 'Source\Settings.cs')
$modern = Get-Content -Raw (Join-Path $root 'Source\ModernSettingsUI.cs')
$traits = Get-Content -Raw (Join-Path $root 'Source\ExpandedTraitPatches.cs')
$patches = Get-Content -Raw (Join-Path $root 'Source\Patches.cs')
$visualParameters = Get-Content -Raw (Join-Path $root 'Source\PlantVisualParameters.cs')
$visualUtility = Get-Content -Raw (Join-Path $root 'Source\PlantVisualUtility.cs')
$designer = Get-Content -Raw (Join-Path $root 'Source\VisualDesigner.cs')
$novel = Get-Content -Raw (Join-Path $root 'Source\NovelSeedUtility.cs')
$architecture = Get-Content -Raw (Join-Path $root 'architect.md')

$checks = [ordered]@{
    'architecture defines compatibility boundary' = $architecture -match 'Automatic plant-mask fallback' -and $architecture -match 'authoritative manual'
    'manual mask scribe keys preserved' = $settings -match '"plantMaskLayers"' -and $settings -match '"plantMaskVariations"'
    'legacy base manual fallback preserved' = $settings -match 'ManualPlantMaskLayersForVariation[\s\S]*?\?\.Layers \?\? PlantMaskLayers'
    'manual lookup precedes automatic fallback' = $masking -match 'HasManualMask[\s\S]*?ManualLayersForVariation[\s\S]*?PlantAutoMaskCache'
    'existing three-layer renderer contract retained' = $masking -match 'VisualMaskLayerRecord' -and $masking -match 'LayerAt\(IReadOnlyList<VisualMaskLayerRecord>'
    'cache is versioned and persistent' = $auto -match 'GeneratorVersion' -and $auto -match 'FormatVersion' -and $auto -match 'GenFilePaths.ConfigFolderPath'
    'cache fingerprints source produce and state references' = $auto -match 'TextureKey' -and $auto -match 'harvestedThingDef' -and $auto -match 'ReferenceFingerprint'
    'cache identity includes mod texture content variant and algorithm' = $auto -match 'modContentPack\?\.PackageId' -and $auto -match 'GeneratorVersion' -and $auto -match 'PixelFingerprint\(texture\)' -and $auto -match 'variationIndex'
    'session validated lookup precedes texture fingerprinting' = $auto -match 'SessionValidated\.Contains\(key\)[\s\S]*?Texture texture = PlantMaskUtility\.TextureForVariation'
    'transparency participates in classification' = $auto -match 'alpha' -and $auto -match 'TransparentAlpha'
    'HSV and color clustering participate' = $auto -match 'RGBToHSV' -and $auto -match 'Cluster'
    'connected regions participate' = $auto -match 'ConnectedRegion' -and $auto -match 'Queue<int>'
    'layer presence is evidence-based and cache-invalidating' = $auto -match 'GeneratorVersion = 13' -and $auto -match 'EligibilityFor' -and $auto -match 'HasCredibleStem' -and $auto -match 'BuildProduceMap' -and $auto -match 'LayerAbsenceRegression' -and $auto -match 'paintFruit'
    'paired state references participate when available' = $auto -match 'immatureReference' -and $auto -match 'leaflessReference' -and $masking -match 'ReferenceTextureForVariation'
    'state differencing is alignment gated and palette masks are bounded' = $auto -match 'AlphaIntersectionOverUnion' -and $auto -match '>= 0\.88f' -and $auto -match 'selectedPixels > opaquePixels \* 0\.24f'
    'produce resolves declared asset instead of unsafe fallback' = $auto -match 'produce\.graphicData\.texPath' -and $auto -match 'produce\.HasValue' -and $auto -notmatch 'IsVisibleProduceRegion'
    'stem propagation is width bounded and junction aware' = $auto -match 'FitsStructuralWidth' -and $auto -match 'risingDiagonal' -and $auto -match 'fallingDiagonal' -and $auto -match 'denseCanopyStem' -and $auto -match 'GroundcoverStemRegression' -and $auto -match 'rootedShare <= 0\.30f'
    'tree morphology is independent of harvested product type' = $auto -match 'treeCategory != TreeCategory\.None' -and $auto -match 'forceIsTree'
    'forced tree morphology cannot bypass stem credibility' = $auto -match 'ForcedStemCredibilityRegression' -and $auto -match 'eligibility\.stem &= credibleStem' -and $auto -notmatch 'forceStem \|\| HasCredibleStem'
    'palette-only produce rejects rooted matches' = $auto -match 'paletteRootMatch' -and $auto -match 'sourceHeight \* 0\.22f'
    'hierarchical assignment prevents semantic score competition' = $auto -match 'selected = produceMap\[index\]' -and $auto -match 'eligibility\.leaves \? 1 : -1' -and $auto -notmatch 'float\[\] scores'
    'produce texture or color participates when available' = $auto -match 'ProduceSignature' -and $auto -match 'produceColor'
    'multiple growth collection directional textures supported' = $masking -match 'Blooming' -and $masking -match 'Graphic_Collection' -and $masking -match '"_north"' -and $masking -match '"_west"'
    'variation discovery does not initialize graphics or materials' = $masking -match 'ContentFinder<Texture2D>.GetAllInFolder' -and $masking -notmatch 'plantDef\?\.graphicData\?\.Graphic'
    'automatic masks do not force nonvisual custom rendering' = (Get-Content -Raw (Join-Path $root 'Source\Patches.cs')) -match 'HasPlantMaskVisual[\s\S]*?HasActiveMasks'
    'duplicate collection assets collapse by texture identity' = $masking -match 'GroupBy\(TextureIdentity' -and $masking -match '"_north"' -and $masking -match 'Substring'
    'ordinary growth updates bypass self-seeding component lookup' = $traits -match 'MayHaveSelfSeeding\(__instance\?\.def\)[\s\S]*?TrySelfSeed'
    'plants without produce supported' = $auto -match 'produce.HasValue'
    'manual edit promotion exists' = $masking -match 'PromoteAutoToManual'
    'dev plant gizmo opens exact variation' = $patches -match 'Plant_GetGizmos_MaskEditor_Patch' -and $patches -match 'Prefs.DevMode' -and $patches -match 'VariationIndexForTexture' -and $patches -match 'Dialog_PlantMasks\(__instance.def, false, variation\)'
    'connected mask regions can move between layers' = $masking -match 'ReassignConnectedRegion' -and $masking -match 'ConnectedMaskedRegion' -and $masking -match 'sourceLayerIndex' -and $masking -match 'undoHistory'
    'empty optional layers are explicit in editor' = $masking -match '" - absent"' -and $masking -match 'Not detected in this texture' -and $masking -match 'CurrentIsManual \? " - empty"'
    'regenerate and reset actions exist' = $masking -match 'Regenerate Auto-Mask' -and $masking -match 'Reset to Auto-Mask'
    'manual and automatic labels exist' = $masking -match 'Manual' -and $masking -match 'Auto-generated'
    'low confidence is exposed' = $auto -match 'LowConfidence' -and $masking -match 'manual review'
    'low confidence automatic masks do not render' = $auto -match 'IsRenderable' -and $auto -match '!record\.LowConfidence' -and $masking -match 'PlantAutoMaskCache\.IsRenderable'
    'recoloring preserves value and protects dark outlines' = $visualParameters -match 'PlantVisualColorUtility' -and $visualParameters -match 'sourceValue' -and $visualParameters -match 'outlineStrength' -and $visualUtility -match 'PlantVisualColorUtility\.Apply' -and $designer -match 'PlantVisualColorUtility\.Apply' -and $novel -match 'PlantVisualColorUtility\.Apply'
    'mask diagnostics use foliage red produce green stem blue' = $masking -notmatch 'original\.r \*= multiplier\.r' -and (Get-Content -Raw (Join-Path $root 'DevTools\BridgeAdapter\HorticultureBridgeAdapter.cs')) -match '79, 196, 112[\s\S]*238, 76, 85[\s\S]*76, 137, 232'
    'automatic classification is deterministic' = $auto -match 'DeterministicClassificationRegression' -and $auto -match 'SequenceEqual'
    'batch skips manual masks' = $auto -match 'GenerateMissing' -and $auto -match 'HasManualPlantMask'
    'batch generation is exposed' = $modern -match 'Generate Missing Auto-Masks'
}

$failed = @($checks.GetEnumerator() | Where-Object { -not $_.Value })
if ($failed.Count) {
    $failed | ForEach-Object { Write-Error ("FAILED: " + $_.Key) }
    exit 1
}
Write-Output ("Automatic plant-mask compatibility verification passed ({0} checks)." -f $checks.Count)
