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
            bool shadedFoliage = ShadedMultiIslandRegression();
            bool largeAmbiguousRegion = LargeAmbiguousRegionRegression();
            bool editorHistory = EditorHistoryRegression(projection);
            return translatedAndScaled && correctAssignment && transparentBoundary && conflictDetected && rejected && cancelWithoutMutation
                && changed && undoRedo && sameIdentity && conflictingManual && legacyCache && manualLoading
                && confidence && shadedFoliage && largeAmbiguousRegion && editorHistory;
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
            List<VisualMaskLayerRecord> changedLeaves = sourceLayers.Select(layer => layer.Clone()).ToList();
            Color32[] changedLeafPixels = (Color32[])sourcePixels.Clone();
            PaintRect(changedLeaves[1], 122, 112, 4, 4, new Color32(180, 92, 64, 255), changedLeafPixels);
            MaskProjectionResult changedLeafProjection = SemanticMaskProjection.Build(changedLeaves,
                changedLeafPixels, EmptyLayers(), targetPixels);
            bool emptyIsZero = empty.Channels[1].Confidence == 0f && empty.Channels[2].Confidence == 0f;
            bool channelLocal = Mathf.Abs(correct.Channels[0].Confidence - changedLeafProjection.Channels[0].Confidence) < 0.0001f
                && Mathf.Abs(correct.Channels[2].Confidence - changedLeafProjection.Channels[2].Confidence) < 0.0001f;
            return bounded && emptyIsZero && channelLocal && correctConfidence > incorrectConfidence
                && ambiguousCannotImprove && conflictPenalty && missingCoveragePenalty;
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
