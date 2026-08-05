using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace HorticultureNovelSeeds
{
    internal static class MaskProjectionRegression
    {
        private const int Size = VisualMaskLayerRecord.Resolution;

        internal static bool Run()
        {
            List<VisualMaskLayerRecord> sourceLayers = EmptyLayers();
            PaintRect(sourceLayers[0], 24, 198, 14, 14, new Color32(190, 82, 64, 255), null);
            PaintRect(sourceLayers[0], 48, 172, 10, 10, new Color32(188, 84, 66, 255), null);
            PaintRect(sourceLayers[1], 92, 148, 26, 18, new Color32(180, 92, 64, 255), null);
            PaintRect(sourceLayers[2], 166, 56, 3, 116, new Color32(88, 124, 72, 255), null);
            Color32[] sourcePixels = new Color32[Size * Size];
            PaintRect(sourceLayers[0], 24, 198, 14, 14, new Color32(190, 82, 64, 255), sourcePixels);
            PaintRect(sourceLayers[0], 48, 172, 10, 10, new Color32(188, 84, 66, 255), sourcePixels);
            PaintRect(sourceLayers[1], 92, 148, 26, 18, new Color32(180, 92, 64, 255), sourcePixels);
            PaintRect(sourceLayers[2], 166, 56, 3, 116, new Color32(88, 124, 72, 255), sourcePixels);

            Color32[] targetPixels = new Color32[Size * Size];
            SetTopRect(targetPixels, 40, 30, 12, 12, new Color32(190, 84, 66, 255));
            SetTopRect(targetPixels, 59, 54, 8, 8, new Color32(188, 84, 66, 255));
            SetTopRect(targetPixels, 94, 67, 21, 15, new Color32(182, 94, 66, 255));
            SetTopRect(targetPixels, 154, 62, 3, 93, new Color32(88, 124, 72, 255));
            List<VisualMaskLayerRecord> targetLayers = EmptyLayers();
            MaskProjectionResult projection = SemanticMaskProjection.Build(sourceLayers, sourcePixels, targetLayers, targetPixels);
            if (!projection.HasCandidate || projection.VisibleTargetPixels <= 0) return false;

            List<VisualMaskLayerRecord> expectedLayers = EmptyLayers();
            PaintTopRect(expectedLayers[0], 40, 30, 12, 12);
            PaintTopRect(expectedLayers[0], 59, 54, 8, 8);
            PaintTopRect(expectedLayers[1], 94, 67, 21, 15);
            PaintTopRect(expectedLayers[2], 154, 62, 3, 93);
            float produceIoU = IntersectionOverUnion(projection.CandidateLayers[0], expectedLayers[0]);
            float leavesIoU = IntersectionOverUnion(projection.CandidateLayers[1], expectedLayers[1]);
            float stemIoU = IntersectionOverUnion(projection.CandidateLayers[2], expectedLayers[2]);
            bool translatedAndScaled = produceIoU >= 0.70f && leavesIoU >= 0.70f && stemIoU >= 0.70f
                && Coverage(projection.CandidateLayers[0], expectedLayers[0]) >= 0.80f
                && Coverage(projection.CandidateLayers[1], expectedLayers[1]) >= 0.80f
                && Coverage(projection.CandidateLayers[2], expectedLayers[2]) >= 0.80f;
            bool correctAssignment = IntersectionOverUnion(projection.CandidateLayers[0], expectedLayers[1]) < 0.20f
                && IntersectionOverUnion(projection.CandidateLayers[1], expectedLayers[0]) < 0.20f
                && IntersectionOverUnion(projection.CandidateLayers[2], expectedLayers[0]) < 0.20f;
            bool transparentBoundary = !HasTransparentPaint(projection.CandidateLayers, targetPixels);

            List<VisualMaskLayerRecord> conflictSource = EmptyLayers();
            PaintRect(conflictSource[0], 30, 180, 16, 16, new Color32(190, 84, 66, 255), sourcePixels);
            PaintRect(conflictSource[1], 30, 180, 16, 16, new Color32(182, 94, 66, 255), sourcePixels);
            Color32[] conflictPixels = new Color32[Size * Size];
            SetTopRect(conflictPixels, 80, 80, 30, 30, new Color32(186, 88, 65, 255));
            MaskProjectionResult conflict = SemanticMaskProjection.Build(conflictSource, sourcePixels, EmptyLayers(), conflictPixels);
            bool conflictDetected = conflict.Channels[0].Conflicts > 0 || conflict.Channels[1].Conflicts > 0;

            VisualMaskLayerRecord rejectedChannel = targetLayers[1];
            rejectedChannel.PaintPixel(4, 4, true);
            int[] beforeCancel = targetLayers.Select(layer => layer.ContentHash).ToArray();
            List<VisualMaskLayerRecord> accepted = SemanticMaskProjection.ApplyAccepted(targetLayers, projection,
                new[] { true, false, true }, out bool changed);
            bool rejected = accepted[1].IsPainted(4, 4);
            bool cancelWithoutMutation = beforeCancel.SequenceEqual(targetLayers.Select(layer => layer.ContentHash));
            List<VisualMaskLayerRecord> undo = targetLayers.Select(layer => layer.Clone()).ToList();
            List<VisualMaskLayerRecord> redo = SemanticMaskProjection.ApplyAccepted(undo, projection,
                new[] { true, true, true }, out bool undoChanged);
            int[] redoHashes = redo.Select(layer => layer.ContentHash).ToArray();
            List<VisualMaskLayerRecord> restored = targetLayers.Select(layer => layer.Clone()).ToList();
            List<VisualMaskLayerRecord> redone = SemanticMaskProjection.ApplyAccepted(restored, projection,
                new[] { true, true, true }, out bool redoChanged);
            bool undoRedo = undoChanged && redoChanged && redoHashes.SequenceEqual(redone.Select(layer => layer.ContentHash));

            bool sameIdentity = false;
            Texture2D texture = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            try
            {
                texture.name = "synthetic_north";
                texture.SetPixels32(Enumerable.Repeat(new Color32(120, 80, 60, 255), 16).ToArray());
                texture.Apply(false, false);
                MaskTextureIdentity.TryGet(texture, "North", out string firstKey);
                MaskTextureIdentity.TryGet(texture, "North", out string secondKey);
                sameIdentity = !string.IsNullOrEmpty(firstKey) && firstKey == secondKey && firstKey.Contains("4x4")
                    && firstKey.Contains("orientation:north");
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }

            List<VisualMaskLayerRecord> conflictingManualLayers = EmptyLayers();
            conflictingManualLayers[0].PaintPixel(1, 1, true);
            bool conflictingManual = SharedManualMaskCache.ConflictingForRegression(EmptyLayers(), conflictingManualLayers);
            bool legacyCache = PlantAutoMaskCache.RequiresIdentityRegeneration(new AutoPlantMaskRecord())
                && !PlantAutoMaskCache.RequiresIdentityRegeneration(new AutoPlantMaskRecord("def", 0, "key", 1f,
                    EmptyLayers(), "eligible"));
            PlantSettingsRecord manualRecord = new PlantSettingsRecord();
            List<VisualMaskLayerRecord> manualLayers = EmptyLayers();
            manualLayers[2].PaintPixel(7, 9, true);
            manualRecord.SetManualPlantMask(2, manualLayers);
            bool manualLoading = manualRecord.HasManualPlantMask(2)
                && manualRecord.ManualPlantMaskLayersForVariation(2)[2].IsPainted(7, 9);
            bool confidence = ConfidenceRegression(sourceLayers, sourcePixels, targetPixels, projection, conflictPixels,
                conflictSource);
            bool combinedFixture = CombinedFixtureRegression();
            bool areaNormalization = AreaNormalizationRegression();
            bool shadedFoliage = ShadedMultiIslandRegression();
            bool largeAmbiguousRegion = LargeAmbiguousRegionRegression();
            bool editorHistory = EditorHistoryRegression(projection);
            return translatedAndScaled && correctAssignment && transparentBoundary && conflictDetected && rejected && cancelWithoutMutation
                && changed && undoRedo && sameIdentity && conflictingManual && legacyCache && manualLoading
                && confidence && combinedFixture && areaNormalization && shadedFoliage
                && largeAmbiguousRegion && editorHistory;
        }

        private static bool ConfidenceRegression(List<VisualMaskLayerRecord> sourceLayers, Color32[] sourcePixels,
            Color32[] targetPixels, MaskProjectionResult correct, Color32[] conflictPixels,
            List<VisualMaskLayerRecord> conflictSource)
        {
            MaskProjectionResult incorrect = SemanticMaskProjection.Build(sourceLayers, sourcePixels, EmptyLayers(),
                IncorrectTargetPixels());
            MaskProjectionResult conflict = SemanticMaskProjection.Build(conflictSource, sourcePixels, EmptyLayers(), conflictPixels);
            float correctConfidence = AverageConfidence(correct);
            float incorrectConfidence = AverageConfidence(incorrect);
            float conflictConfidence = AverageConfidence(conflict);
            bool bounded = correct.Channels.Concat(incorrect.Channels).Concat(conflict.Channels).All(channel =>
                channel != null && !float.IsNaN(channel.Confidence) && !float.IsInfinity(channel.Confidence)
                && channel.Confidence >= 0f && channel.Confidence <= 1f);
            bool ambiguousCannotImprove = conflictConfidence <= correctConfidence + 0.0001f;
            bool conflictPenalty = conflict.Channels.Sum(channel => channel.Conflicts) > 0
                && conflictConfidence < AverageConfidence(SemanticMaskProjection.Build(conflictSource, sourcePixels,
                    EmptyLayers(), targetPixels));
            bool missingCoveragePenalty = incorrect.Channels.Sum(channel => channel.RemainingUnmaskedVisiblePixels)
                >= correct.Channels.Sum(channel => channel.RemainingUnmaskedVisiblePixels)
                && incorrectConfidence <= correctConfidence + 0.0001f;
            List<VisualMaskLayerRecord> emptySource = EmptyLayers();
            emptySource[0] = sourceLayers[0].Clone();
            MaskProjectionResult empty = SemanticMaskProjection.Build(emptySource, sourcePixels,
                EmptyLayers(), targetPixels);
            List<VisualMaskLayerRecord> zeroAssignmentSource = EmptyLayers();
            Color32[] zeroAssignmentPixels = new Color32[Size * Size];
            PaintTopSourceRect(zeroAssignmentSource[0], zeroAssignmentPixels, 30, 30, 12, 12,
                new Color32(210, 70, 50, 255));
            Color32[] unrelatedTarget = new Color32[Size * Size];
            SetTopRect(unrelatedTarget, 130, 130, 18, 18, new Color32(20, 210, 220, 255));
            MaskProjectionResult zeroAssignment = SemanticMaskProjection.Build(zeroAssignmentSource,
                zeroAssignmentPixels, EmptyLayers(), unrelatedTarget);
            List<VisualMaskLayerRecord> changedLeaves = sourceLayers.Select(layer => layer.Clone()).ToList();
            Color32[] changedLeafPixels = (Color32[])sourcePixels.Clone();
            bool emptyIsZero = empty.Channels[1].Confidence == 0f && empty.Channels[2].Confidence == 0f;
            bool presentZeroAssignmentIsZero = zeroAssignment.Channels[0].Confidence == 0f
                && zeroAssignment.Channels[0].AssignedTargetPixels == 0;
            MaskProjectionResult changedLeafProjection;
            for (int maskY = 148; maskY < 166; maskY++) for (int x = 92; x < 118; x++)
                changedLeafPixels[(Size - 1 - maskY) * Size + x] = new Color32(70, 130, 75, 255);
            changedLeaves[1] = sourceLayers[1].Clone();
            changedLeafProjection = SemanticMaskProjection.Build(changedLeaves, changedLeafPixels,
                EmptyLayers(), targetPixels);
            bool channelLocal = Mathf.Abs(correct.Channels[0].Confidence - changedLeafProjection.Channels[0].Confidence) < 0.0001f
                && Mathf.Abs(correct.Channels[2].Confidence - changedLeafProjection.Channels[2].Confidence) < 0.0001f;
            return bounded && emptyIsZero && presentZeroAssignmentIsZero && channelLocal && correctConfidence > incorrectConfidence
                && ambiguousCannotImprove && conflictPenalty && missingCoveragePenalty;
        }

        private sealed class CombinedFixture
        {
            public List<VisualMaskLayerRecord> sourceLayers;
            public Color32[] sourcePixels;
            public Color32[] targetPixels;
            public List<VisualMaskLayerRecord> expectedLayers;
            public VisualMaskLayerRecord background;
            public VisualMaskLayerRecord ambiguous;
        }

        private static bool CombinedFixtureRegression()
        {
            CombinedFixture original = BuildCombinedFixture(0, 0, 1f);
            CombinedFixture translated = BuildCombinedFixture(8, 6, 1f);
            CombinedFixture scaled = BuildCombinedFixture(30, 25, 0.8f);
            MaskProjectionResult first = SemanticMaskProjection.Build(original.sourceLayers, original.sourcePixels,
                EmptyLayers(), original.targetPixels);
            MaskProjectionResult second = SemanticMaskProjection.Build(translated.sourceLayers, translated.sourcePixels,
                EmptyLayers(), translated.targetPixels);
            MaskProjectionResult third = SemanticMaskProjection.Build(scaled.sourceLayers, scaled.sourcePixels,
                EmptyLayers(), scaled.targetPixels);
            bool exactVariants = ExactCombinedProjection(first, original)
                && ExactCombinedProjection(second, translated) && ExactCombinedProjection(third, scaled);
            bool deterministic = first.CandidateLayers.Select(layer => layer.ContentHash).SequenceEqual(
                SemanticMaskProjection.Build(original.sourceLayers, original.sourcePixels, EmptyLayers(),
                    original.targetPixels).CandidateLayers.Select(layer => layer.ContentHash));
            List<VisualMaskLayerRecord> reorderedSource = new List<VisualMaskLayerRecord>
            {
                original.sourceLayers[2].Clone(), original.sourceLayers[0].Clone(), original.sourceLayers[1].Clone()
            };
            MaskProjectionResult reordered = SemanticMaskProjection.Build(reorderedSource,
                (Color32[])original.sourcePixels.Clone(), EmptyLayers(), (Color32[])original.targetPixels.Clone());
            bool orderIndependent = first.CandidateLayers.Select(layer => layer.ContentHash).OrderBy(hash => hash)
                .SequenceEqual(reordered.CandidateLayers.Select(layer => layer.ContentHash).OrderBy(hash => hash));
            return exactVariants && deterministic && orderIndependent
                && CountComponents(original.expectedLayers[1]) == 3
                && first.Channels[0].Conflicts == CountPainted(original.ambiguous)
                && first.Channels[1].Conflicts == CountPainted(original.ambiguous)
                && first.Channels[2].Conflicts == 0
                && first.UnresolvedConflictPixels == CountPainted(original.ambiguous)
                && first.ArbitrationDomainPixels == CountPainted(original.expectedLayers[0])
                    + CountPainted(original.expectedLayers[1]) + CountPainted(original.expectedLayers[2])
                    + CountPainted(original.ambiguous);
        }

        private static bool AreaNormalizationRegression()
        {
            CombinedFixture original = BuildCombinedFixture(0, 0, 1f);
            CombinedFixture translated = BuildCombinedFixture(12, 9, 1f);
            CombinedFixture scaled = BuildCombinedFixture(36, 31, 0.7f);
            MaskProjectionResult first = SemanticMaskProjection.Build(original.sourceLayers, original.sourcePixels,
                EmptyLayers(), original.targetPixels);
            MaskProjectionResult translatedResult = SemanticMaskProjection.Build(translated.sourceLayers,
                translated.sourcePixels, EmptyLayers(), translated.targetPixels);
            MaskProjectionResult scaledResult = SemanticMaskProjection.Build(scaled.sourceLayers, scaled.sourcePixels,
                EmptyLayers(), scaled.targetPixels);
            bool comparableSourceArea = first.Channels.Zip(translatedResult.Channels, (left, right) =>
                    Mathf.Abs(left.SourceAreaShare - right.SourceAreaShare) < 0.0001f)
                .All(value => value)
                && first.Channels.Zip(scaledResult.Channels, (left, right) =>
                    Mathf.Abs(left.SourceAreaShare - right.SourceAreaShare) < 0.0001f)
                .All(value => value);
            bool comparableTargetArea = first.Channels.Zip(translatedResult.Channels, (left, right) =>
                    Mathf.Abs(left.TargetAreaShare - right.TargetAreaShare) < 0.08f)
                .All(value => value)
                && first.Channels.Zip(scaledResult.Channels, (left, right) =>
                    Mathf.Abs(left.TargetAreaShare - right.TargetAreaShare) < 0.12f)
                .All(value => value);
            return comparableSourceArea && comparableTargetArea
                && first.Channels.Zip(translatedResult.Channels, (left, right) =>
                    Mathf.Abs(left.SemanticAgreement - right.SemanticAgreement) < 0.08f
                    && Mathf.Abs(left.Confidence - right.Confidence) < 0.12f)
                .All(value => value)
                && first.Channels.Zip(scaledResult.Channels, (left, right) =>
                    Mathf.Abs(left.SemanticAgreement - right.SemanticAgreement) < 0.12f
                    && Mathf.Abs(left.Confidence - right.Confidence) < 0.18f)
                .All(value => value);
        }

        private static bool ExactCombinedProjection(MaskProjectionResult result, CombinedFixture fixture)
        {
            if (result == null || fixture == null) return false;
            bool layersExact = Enumerable.Range(0, 3).All(channel => SamePixels(result.CandidateLayers[channel],
                fixture.expectedLayers[channel]));
            bool noTransparent = !HasTransparentPaint(result.CandidateLayers, fixture.targetPixels);
            bool backgroundExact = UnmaskedPixels(result, fixture.targetPixels).OrderBy(pixel => pixel).SequenceEqual(
                PixelSet(fixture.background).Concat(PixelSet(fixture.ambiguous)).OrderBy(pixel => pixel));
            bool conflictExact = PixelSet(result.UnresolvedConflictMask).SequenceEqual(PixelSet(fixture.ambiguous));
            bool noBackgroundPaint = Enumerable.Range(0, 3).All(channel =>
                !PixelSet(result.CandidateLayers[channel]).Intersect(PixelSet(fixture.background)).Any());
            return layersExact && noTransparent && backgroundExact && conflictExact && noBackgroundPaint;
        }

        private static CombinedFixture BuildCombinedFixture(int offsetX, int offsetY, float scale)
        {
            CombinedFixture fixture = new CombinedFixture
            {
                sourceLayers = EmptyLayers(),
                sourcePixels = new Color32[Size * Size],
                targetPixels = new Color32[Size * Size],
                expectedLayers = EmptyLayers(),
                background = new VisualMaskLayerRecord { name = "background" },
                ambiguous = new VisualMaskLayerRecord { name = "ambiguous" }
            };
            Color32 produce = new Color32(198, 78, 58, 255);
            Color32 leaves = new Color32(70, 120, 70, 255);
            Color32 stem = new Color32(55, 105, 65, 255);
            Color32 ambiguous = new Color32(132, 92, 54, 255);
            PaintTopSourceRect(fixture.sourceLayers[0], fixture.sourcePixels, 35, 40, 10, 10, produce);
            PaintTopSourceRect(fixture.sourceLayers[0], fixture.sourcePixels, 90, 100, 12, 8, produce);
            PaintTopSourceRect(fixture.sourceLayers[0], fixture.sourcePixels, 125, 140, 10, 10, ambiguous);
            PaintTopSourceRect(fixture.sourceLayers[1], fixture.sourcePixels, 60, 70, 50, 30, leaves);
            PaintTopSourceRect(fixture.sourceLayers[1], fixture.sourcePixels, 125, 140, 10, 10, ambiguous);
            PaintTopSourceRect(fixture.sourceLayers[2], fixture.sourcePixels, 140, 50, 3, 100, stem);

            Color32 unrelatedBackground = new Color32(20, 40, 210, 255);
            PaintTopTransformed(fixture.targetPixels, fixture.background, null, 50, 45, 3, 3,
                unrelatedBackground, offsetX, offsetY, scale);
            PaintTopTransformed(fixture.targetPixels, fixture.background, null, 130, 45, 3, 3,
                unrelatedBackground, offsetX, offsetY, scale);
            PaintTopTransformed(fixture.targetPixels, fixture.background, null, 130, 120, 3, 3,
                unrelatedBackground, offsetX, offsetY, scale);
            PaintTopTransformed(fixture.targetPixels, fixture.background, null, 110, 120, 3, 3,
                unrelatedBackground, offsetX, offsetY, scale);
            PaintTopTransformed(fixture.targetPixels, fixture.expectedLayers[0], fixture.background, 35, 40,
                10, 10, produce, offsetX, offsetY, scale);
            PaintTopTransformed(fixture.targetPixels, fixture.expectedLayers[0], fixture.background, 90, 100,
                12, 8, produce, offsetX, offsetY, scale);
            PaintTopTransformed(fixture.targetPixels, fixture.expectedLayers[1], fixture.background, 60, 70,
                14, 10, leaves, offsetX, offsetY, scale, shaded: true);
            PaintTopTransformed(fixture.targetPixels, fixture.expectedLayers[1], fixture.background, 80, 80,
                14, 10, leaves, offsetX, offsetY, scale, shaded: true);
            PaintTopTransformed(fixture.targetPixels, fixture.expectedLayers[1], fixture.background, 100, 90,
                10, 10, leaves, offsetX, offsetY, scale, shaded: true);
            PaintTopTransformed(fixture.targetPixels, fixture.expectedLayers[2], fixture.background, 140, 50,
                3, 100, stem, offsetX, offsetY, scale);
            PaintTopTransformed(fixture.targetPixels, fixture.ambiguous, fixture.background, 125, 140,
                10, 10, ambiguous, offsetX, offsetY, scale);
            return fixture;
        }

        private static void PaintTopTransformed(Color32[] pixels, VisualMaskLayerRecord expected,
            VisualMaskLayerRecord background, int x, int y, int width, int height, Color32 color,
            int offsetX, int offsetY, float scale, bool shaded = false)
        {
            int targetX = Mathf.RoundToInt(x * scale) + offsetX;
            int targetY = Mathf.RoundToInt(y * scale) + offsetY;
            int targetWidth = Mathf.Max(1, Mathf.RoundToInt(width * scale));
            int targetHeight = Mathf.Max(1, Mathf.RoundToInt(height * scale));
            for (int row = 0; row < targetHeight; row++) for (int column = 0; column < targetWidth; column++)
            {
                int px = targetX + column; int py = targetY + row;
                if (px < 0 || px >= Size || py < 0 || py >= Size) continue;
                Color32 shade = shaded && column >= targetWidth / 2
                    ? new Color32(78, 128, 78, 255) : color;
                pixels[py * Size + px] = shade;
                expected?.PaintPixel(px, Size - 1 - py, true);
                background?.PaintPixel(px, Size - 1 - py, false);
            }
        }

        private static void PaintTopSourceRect(VisualMaskLayerRecord layer, Color32[] pixels, int x, int y,
            int width, int height, Color32 color)
        {
            for (int row = 0; row < height; row++) for (int column = 0; column < width; column++)
            {
                int px = x + column; int py = y + row;
                layer.PaintPixel(px, Size - 1 - py, true);
                pixels[py * Size + px] = color;
            }
        }

        private static IEnumerable<int> PixelSet(VisualMaskLayerRecord layer)
        {
            for (int y = 0; y < Size; y++) for (int x = 0; x < Size; x++)
                if (layer != null && layer.IsPainted(x, y)) yield return y * Size + x;
        }

        private static IEnumerable<int> UnmaskedPixels(MaskProjectionResult result, Color32[] pixels)
        {
            for (int y = 0; y < Size; y++) for (int x = 0; x < Size; x++)
                if (pixels[y * Size + x].a >= 16 && !result.CandidateLayers.Any(layer => layer.IsPainted(x, Size - 1 - y)))
                    yield return (Size - 1 - y) * Size + x;
        }

        private static bool SamePixels(VisualMaskLayerRecord first, VisualMaskLayerRecord second)
        {
            return PixelSet(first).SequenceEqual(PixelSet(second));
        }

        private static bool ShadedMultiIslandRegression()
        {
            List<VisualMaskLayerRecord> source = EmptyLayers();
            Color32[] sourcePixels = new Color32[Size * Size];
            PaintRect(source[1], 28, 164, 96, 54, new Color32(92, 128, 74, 255), sourcePixels);
            Color32[] targetPixels = new Color32[Size * Size];
            List<VisualMaskLayerRecord> expected = EmptyLayers();
            int islandCount = 0;
            for (int row = 0; row < 4; row++) for (int column = 0; column < 5; column++)
            {
                int x = 44 + column * 24;
                int y = 34 + row * 20;
                int width = 18;
                int height = 14;
                Color32 color = new Color32((byte)(74 + row * 8), (byte)(118 + column * 5),
                    (byte)(66 + (row + column) * 3), 255);
                SetTopRect(targetPixels, x, y, width, height, color);
                PaintTopRect(expected[1], x, y, width, height);
                islandCount++;
            }
            MaskProjectionResult result = SemanticMaskProjection.Build(source, sourcePixels, EmptyLayers(), targetPixels);
            float iou = IntersectionOverUnion(result.CandidateLayers[1], expected[1]);
            float coverage = Coverage(result.CandidateLayers[1], expected[1]);
            int expectedPixels = CountPainted(expected[1]);
            int actualPixels = CountPainted(result.CandidateLayers[1]);
            return islandCount >= 10 && expectedPixels > 0 && actualPixels > expectedPixels / 2
                && iou >= 0.70f && coverage >= 0.80f
                && CountComponents(result.CandidateLayers[1]) >= islandCount
                && result.Channels[1].Conflicts == 0
                && result.RemainingUnmaskedVisiblePixels == 0
                && result.UnresolvedConflictPixels == 0
                && !HasTransparentPaint(result.CandidateLayers, targetPixels);
        }

        private static bool LargeAmbiguousRegionRegression()
        {
            List<VisualMaskLayerRecord> source = EmptyLayers();
            Color32[] sourcePixels = new Color32[Size * Size];
            Color32 shared = new Color32(126, 116, 82, 255);
            PaintRect(source[0], 32, 188, 8, 8, shared, sourcePixels);
            PaintRect(source[1], 32, 188, 8, 8, shared, sourcePixels);
            Color32[] targetPixels = new Color32[Size * Size];
            const int expected = 40 * 40;
            SetTopRect(targetPixels, 72, 72, 40, 40, shared);
            MaskProjectionResult result = SemanticMaskProjection.Build(source, sourcePixels, EmptyLayers(), targetPixels);
            return result.ArbitrationDomainPixels == expected
                && result.UnresolvedConflictPixels == expected
                && result.Conflicts == expected
                && result.AmbiguousAssignments == expected
                && result.Channels[0].Conflicts == expected
                && result.Channels[1].Conflicts == expected
                && CountPainted(result.UnresolvedConflictMask) == expected
                && CountPainted(result.CandidateLayers[0]) == 0
                && CountPainted(result.CandidateLayers[1]) == 0;
        }

        private static float AverageConfidence(MaskProjectionResult result)
        {
            return result?.Channels == null || result.Channels.Length == 0
                ? 0f : result.Channels.Where(channel => channel != null).Select(channel => channel.Confidence).DefaultIfEmpty(0f).Average();
        }

        private static Color32[] IncorrectTargetPixels()
        {
            Color32[] pixels = new Color32[Size * Size];
            SetTopRect(pixels, 8, 8, 170, 18, new Color32(30, 210, 230, 255));
            SetTopRect(pixels, 160, 190, 12, 12, new Color32(220, 40, 220, 255));
            return pixels;
        }

        private static bool EditorHistoryRegression(MaskProjectionResult projection)
        {
            ThingDef plant = DefDatabase<ThingDef>.AllDefsListForReading.FirstOrDefault(def => def.plant != null
                && PlantMaskUtility.TextureForVariation(def, 0) != null);
            if (plant == null || HorticultureNovelSeedsMod.Settings == null) return false;
            PlantSettingsRecord record = HorticultureNovelSeedsMod.Settings.GetPlantSettings(plant);
            List<VisualMaskLayerRecord> savedBase = record.PlantMaskLayers.Select(layer => layer.Clone()).ToList();
            List<PlantMaskVariationRecord> savedVariations = record.PlantMaskVariationRecords
                .Select(item => new PlantMaskVariationRecord(item.VariationIndex, item.Layers)).ToList();
            List<VisualMaskLayerRecord> savedProduce = record.ProduceMaskLayers.Select(layer => layer.Clone()).ToList();
            bool savedUsePlant = record.usePlantMasks;
            bool savedDisableAuto = record.disableAutoPlantMasks;
            bool savedUseProduce = record.useProduceMasks;
            List<VisualMaskLayerRecord> established = EmptyLayers();
            established[0].PaintPixel(3, 3, true);
            record.SetManualPlantMask(0, established);
            try
            {
                Dialog_PlantMasks dialog = new Dialog_PlantMasks(plant, false, 0);
                int[] before = dialog.CurrentLayerHashesForRegression;
                dialog.BeginProjectionPreviewForRegression(projection, dialog.CurrentLayersForRegression(), 0,
                    new[] { true, true, true });
                dialog.ApplyProjectionPreviewForRegression();
                int[] projected = dialog.CurrentLayerHashesForRegression;
                bool oneEntry = dialog.UndoHistoryCountForRegression == 1;
                dialog.UndoForRegression();
                bool exactUndo = before.SequenceEqual(dialog.CurrentLayerHashesForRegression);
                dialog.RedoForRegression();
                bool exactRedo = projected.SequenceEqual(dialog.CurrentLayerHashesForRegression);
                return oneEntry && exactUndo && exactRedo;
            }
            finally
            {
                record.ReplaceMasks(savedUsePlant, savedBase, savedVariations, savedUseProduce, savedProduce);
                record.disableAutoPlantMasks = savedDisableAuto;
            }
        }

        private static List<VisualMaskLayerRecord> EmptyLayers()
        {
            return new List<VisualMaskLayerRecord>
            {
                new VisualMaskLayerRecord { name = "Produce" },
                new VisualMaskLayerRecord { name = "Leaves" },
                new VisualMaskLayerRecord { name = "Stem" }
            };
        }

        private static void PaintRect(VisualMaskLayerRecord layer, int x, int y, int width, int height,
            Color32 color, Color32[] pixels)
        {
            for (int maskY = y; maskY < y + height; maskY++) for (int px = x; px < x + width; px++)
            {
                layer.PaintPixel(px, maskY, true);
                if (pixels != null) pixels[(Size - 1 - maskY) * Size + px] = color;
            }
        }

        private static void SetTopRect(Color32[] pixels, int x, int topY, int width, int height, Color32 color)
        {
            for (int y = topY; y < topY + height; y++) for (int px = x; px < x + width; px++)
                pixels[y * Size + px] = color;
        }

        private static void PaintTopRect(VisualMaskLayerRecord layer, int x, int topY, int width, int height)
        {
            for (int y = topY; y < topY + height; y++) for (int px = x; px < x + width; px++)
                layer.PaintPixel(px, Size - 1 - y, true);
        }

        private static float Coverage(VisualMaskLayerRecord actual, VisualMaskLayerRecord expected)
        {
            int expectedPixels = 0;
            int coveredPixels = 0;
            for (int y = 0; y < Size; y++) for (int x = 0; x < Size; x++)
            {
                if (!expected.IsPainted(x, y)) continue;
                expectedPixels++;
                if (actual.IsPainted(x, y)) coveredPixels++;
            }
            return coveredPixels / (float)System.Math.Max(1, expectedPixels);
        }

        private static int CountPainted(VisualMaskLayerRecord layer)
        {
            int count = 0;
            if (layer == null) return count;
            for (int y = 0; y < Size; y++) for (int x = 0; x < Size; x++)
                if (layer.IsPainted(x, y)) count++;
            return count;
        }

        private static int CountComponents(VisualMaskLayerRecord layer)
        {
            if (layer == null) return 0;
            bool[] visited = new bool[Size * Size];
            int count = 0;
            for (int y = 0; y < Size; y++) for (int x = 0; x < Size; x++)
            {
                int start = y * Size + x;
                if (!layer.IsPainted(x, y) || visited[start]) continue;
                count++;
                Queue<int> queue = new Queue<int>();
                queue.Enqueue(start); visited[start] = true;
                while (queue.Count > 0)
                {
                    int current = queue.Dequeue();
                    int cx = current % Size; int cy = current / Size;
                    for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
                    {
                        if (Mathf.Abs(dx) + Mathf.Abs(dy) != 1) continue;
                        int nx = cx + dx; int ny = cy + dy;
                        if (nx < 0 || nx >= Size || ny < 0 || ny >= Size) continue;
                        int next = ny * Size + nx;
                        if (visited[next] || !layer.IsPainted(nx, ny)) continue;
                        visited[next] = true; queue.Enqueue(next);
                    }
                }
            }
            return count;
        }

        private static float IntersectionOverUnion(VisualMaskLayerRecord first, VisualMaskLayerRecord second)
        {
            int intersection = 0;
            int union = 0;
            for (int y = 0; y < Size; y++) for (int x = 0; x < Size; x++)
            {
                bool a = first.IsPainted(x, y);
                bool b = second.IsPainted(x, y);
                if (a && b) intersection++;
                if (a || b) union++;
            }
            return intersection / (float)System.Math.Max(1, union);
        }

        private static bool HasTransparentPaint(IReadOnlyList<VisualMaskLayerRecord> layers, Color32[] pixels)
        {
            for (int maskY = 0; maskY < Size; maskY++) for (int x = 0; x < Size; x++)
                if (layers.Any(layer => layer.IsPainted(x, maskY)) && pixels[(Size - 1 - maskY) * Size + x].a < 16) return true;
            return false;
        }
    }
}
