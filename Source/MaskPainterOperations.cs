using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace HorticultureNovelSeeds
{
    public enum MaskValidationCategory
    {
        All,
        TransparentPaint,
        Overlap,
        TinyFragments,
        UnmaskedVisible
    }

    public sealed class MaskIssueComponent
    {
        public readonly List<int> Pixels;
        public readonly RectInt Bounds;
        public readonly Vector2 Center;

        public int Count => Pixels?.Count ?? 0;

        internal MaskIssueComponent(List<int> pixels, int resolution)
        {
            Pixels = pixels ?? new List<int>();
            int minX = resolution;
            int minY = resolution;
            int maxX = -1;
            int maxY = -1;
            foreach (int index in Pixels)
            {
                int x = index % resolution;
                int y = index / resolution;
                minX = Mathf.Min(minX, x);
                minY = Mathf.Min(minY, y);
                maxX = Mathf.Max(maxX, x);
                maxY = Mathf.Max(maxY, y);
            }
            Bounds = maxX < minX ? new RectInt() : new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
            Center = maxX < minX ? Vector2.zero : new Vector2((minX + maxX + 1) * 0.5f, (minY + maxY + 1) * 0.5f);
        }
    }

    public sealed class MaskValidationResult
    {
        public int transparentPixels;
        public int overlappingPixels;
        public int emptyChannels;
        public int tinyFragments;
        public int unmaskedVisiblePixels;
        public int allIssuePixels;
        public VisualMaskLayerRecord transparentPaintIssues = new VisualMaskLayerRecord { name = "Transparent Paint" };
        public VisualMaskLayerRecord overlapIssues = new VisualMaskLayerRecord { name = "Overlap" };
        public VisualMaskLayerRecord tinyFragmentIssues = new VisualMaskLayerRecord { name = "Tiny Fragments" };
        public VisualMaskLayerRecord unmaskedVisibleIssues = new VisualMaskLayerRecord { name = "Unmasked Visible" };
        public VisualMaskLayerRecord issues = new VisualMaskLayerRecord { name = "Validation" };
        public List<MaskIssueComponent> transparentPaintComponents = new List<MaskIssueComponent>();
        public List<MaskIssueComponent> overlapComponents = new List<MaskIssueComponent>();
        public List<MaskIssueComponent> tinyFragmentComponents = new List<MaskIssueComponent>();
        public List<MaskIssueComponent> unmaskedVisibleComponents = new List<MaskIssueComponent>();
        public List<MaskIssueComponent> allComponents = new List<MaskIssueComponent>();

        public bool HasIssues => transparentPixels > 0 || overlappingPixels > 0 || emptyChannels > 0
            || tinyFragments > 0 || unmaskedVisiblePixels > 0;

        public VisualMaskLayerRecord IssuesFor(MaskValidationCategory category)
        {
            switch (category)
            {
                case MaskValidationCategory.TransparentPaint: return transparentPaintIssues;
                case MaskValidationCategory.Overlap: return overlapIssues;
                case MaskValidationCategory.TinyFragments: return tinyFragmentIssues;
                case MaskValidationCategory.UnmaskedVisible: return unmaskedVisibleIssues;
                default: return issues;
            }
        }

        public List<MaskIssueComponent> ComponentsFor(MaskValidationCategory category)
        {
            switch (category)
            {
                case MaskValidationCategory.TransparentPaint: return transparentPaintComponents;
                case MaskValidationCategory.Overlap: return overlapComponents;
                case MaskValidationCategory.TinyFragments: return tinyFragmentComponents;
                case MaskValidationCategory.UnmaskedVisible: return unmaskedVisibleComponents;
                default: return allComponents;
            }
        }
    }

    public sealed class MaskValidationNavigator
    {
        public MaskValidationCategory Category { get; private set; } = MaskValidationCategory.All;
        public int IssueIndex { get; private set; } = -1;

        public void Reset()
        {
            Category = MaskValidationCategory.All;
            IssueIndex = -1;
        }

        public void Invalidate()
        {
            Reset();
        }

        public void SelectCategory(MaskValidationCategory category, MaskValidationResult result)
        {
            Category = category;
            IssueIndex = -1;
        }

        public bool Move(MaskValidationResult result, int direction)
        {
            List<MaskIssueComponent> components = result?.ComponentsFor(Category);
            if (components == null || components.Count == 0 || direction == 0)
            {
                IssueIndex = -1;
                return false;
            }
            if (IssueIndex < 0) IssueIndex = direction > 0 ? 0 : components.Count - 1;
            else IssueIndex = (IssueIndex + (direction > 0 ? 1 : -1) + components.Count) % components.Count;
            return true;
        }

        public MaskIssueComponent Current(MaskValidationResult result)
        {
            List<MaskIssueComponent> components = result?.ComponentsFor(Category);
            return components != null && IssueIndex >= 0 && IssueIndex < components.Count ? components[IssueIndex] : null;
        }
    }

    public static class MaskPainterOperations
    {
        private const byte VisibleAlpha = 16;
        private static readonly int[] NeighborX = { -1, 1, 0, 0 };
        private static readonly int[] NeighborY = { 0, 0, -1, 1 };

        public static bool Grow(VisualMaskLayerRecord layer, int amount)
        {
            return Transform(layer, Dilate(ToBits(layer), Mathf.Clamp(amount, 1, 32)));
        }

        public static bool Shrink(VisualMaskLayerRecord layer, int amount)
        {
            return Transform(layer, Erode(ToBits(layer), Mathf.Clamp(amount, 1, 32)));
        }

        public static bool Smooth(VisualMaskLayerRecord layer, int amount = 1)
        {
            bool[] source = ToBits(layer);
            int radius = Mathf.Clamp(amount, 1, 8);
            bool[] closed = Erode(Dilate(source, radius), radius);
            return Transform(layer, Dilate(Erode(closed, radius), radius));
        }

        public static bool Feather(VisualMaskLayerRecord layer, int passes = 1)
        {
            bool[] source = ToBits(layer);
            for (int pass = 0; pass < Mathf.Clamp(passes, 1, 8); pass++)
            {
                bool[] next = (bool[])source.Clone();
                for (int y = 1; y < VisualMaskLayerRecord.Resolution - 1; y++)
                for (int x = 1; x < VisualMaskLayerRecord.Resolution - 1; x++)
                {
                    int selected = 0;
                    for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                        if (source[(y + dy) * VisualMaskLayerRecord.Resolution + x + dx]) selected++;
                    next[y * VisualMaskLayerRecord.Resolution + x] = selected >= 5;
                }
                source = next;
            }
            return Transform(layer, source);
        }

        public static bool RemoveSmallComponents(VisualMaskLayerRecord layer, int minimumSize)
        {
            bool[] bits = ToBits(layer);
            foreach (List<int> component in Components(bits))
                if (component.Count < Mathf.Max(1, minimumSize))
                    foreach (int index in component) bits[index] = false;
            return Transform(layer, bits);
        }

        public static bool FillHoles(VisualMaskLayerRecord layer)
        {
            int size = VisualMaskLayerRecord.Resolution;
            bool[] bits = ToBits(layer);
            bool[] outside = new bool[bits.Length];
            Queue<int> pending = new Queue<int>();
            Action<int> enqueue = index =>
            {
                if (bits[index] || outside[index]) return;
                outside[index] = true;
                pending.Enqueue(index);
            };
            for (int x = 0; x < size; x++) { enqueue(x); enqueue((size - 1) * size + x); }
            for (int y = 0; y < size; y++) { enqueue(y * size); enqueue(y * size + size - 1); }
            while (pending.Count > 0)
            {
                int index = pending.Dequeue(); int x = index % size; int y = index / size;
                for (int direction = 0; direction < 4; direction++)
                {
                    int nx = x + NeighborX[direction]; int ny = y + NeighborY[direction];
                    if (nx >= 0 && nx < size && ny >= 0 && ny < size) enqueue(ny * size + nx);
                }
            }
            for (int index = 0; index < bits.Length; index++) if (!bits[index] && !outside[index]) bits[index] = true;
            return Transform(layer, bits);
        }

        public static bool FillUnmasked(VisualMaskLayerRecord target, IReadOnlyList<VisualMaskLayerRecord> layers,
            Color32[] pixels, bool targetLocked = false)
        {
            int size = VisualMaskLayerRecord.Resolution;
            if (target == null || targetLocked || layers == null || pixels?.Length != size * size) return false;
            bool changed = false;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int pixelIndex = (size - 1 - y) * size + x;
                if (pixels[pixelIndex].a < VisibleAlpha) continue;
                bool assigned = false;
                for (int layer = 0; layer < layers.Count; layer++)
                    if (layers[layer]?.IsPainted(x, y) == true) { assigned = true; break; }
                if (!assigned) changed |= target.PaintPixel(x, y, true);
            }
            return changed;
        }

        public static bool KeepLargest(VisualMaskLayerRecord layer)
        {
            bool[] bits = ToBits(layer);
            List<int> largest = Components(bits).OrderByDescending(component => component.Count).FirstOrDefault();
            bool[] result = new bool[bits.Length];
            if (largest != null) foreach (int index in largest) result[index] = true;
            return Transform(layer, result);
        }

        public static List<int> ConnectedTextureRegion(Color32[] pixels, int seedX, int seedTopY, float tolerance)
        {
            int size = VisualMaskLayerRecord.Resolution;
            List<int> region = new List<int>();
            if (pixels?.Length != size * size || seedX < 0 || seedX >= size || seedTopY < 0 || seedTopY >= size) return region;
            int seedY = size - 1 - seedTopY;
            int seedIndex = seedY * size + seedX;
            Color32 seed = pixels[seedIndex];
            if (seed.a < VisibleAlpha) return region;
            bool[] visited = new bool[pixels.Length];
            Queue<int> pending = new Queue<int>();
            visited[seedIndex] = true; pending.Enqueue(seedIndex);
            while (pending.Count > 0)
            {
                int index = pending.Dequeue(); Color32 candidate = pixels[index];
                if (candidate.a < VisibleAlpha || ColorDistance(seed, candidate) > tolerance) continue;
                region.Add(index);
                int x = index % size; int y = index / size;
                Enqueue(x - 1, y, size, visited, pending);
                Enqueue(x + 1, y, size, visited, pending);
                Enqueue(x, y - 1, size, visited, pending);
                Enqueue(x, y + 1, size, visited, pending);
            }
            return region;
        }

        public static bool SmartExpand(VisualMaskLayerRecord layer, Color32[] pixels, int maximumDistance, float edgeTolerance)
        {
            int size = VisualMaskLayerRecord.Resolution;
            if (pixels?.Length != size * size || layer == null) return false;
            bool[] selected = ToBits(layer);
            for (int pass = 0; pass < Mathf.Clamp(maximumDistance, 1, 32); pass++)
            {
                bool[] next = (bool[])selected.Clone();
                bool added = false;
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    int topIndex = y * size + x;
                    if (selected[topIndex]) continue;
                    int pixelIndex = (size - 1 - y) * size + x;
                    Color32 candidate = pixels[pixelIndex];
                    if (candidate.a < VisibleAlpha) continue;
                    for (int direction = 0; direction < 4; direction++)
                    {
                        int nx = x + NeighborX[direction]; int ny = y + NeighborY[direction];
                        if (nx < 0 || nx >= size || ny < 0 || ny >= size || !selected[ny * size + nx]) continue;
                        Color32 neighbor = pixels[(size - 1 - ny) * size + nx];
                        float alphaDelta = Mathf.Abs(candidate.a - neighbor.a) / 255f;
                        if (alphaDelta <= 0.20f && ColorDistance(candidate, neighbor) <= edgeTolerance)
                        { next[topIndex] = true; added = true; break; }
                    }
                }
                selected = next;
                if (!added) break;
            }
            return Transform(layer, selected);
        }

        public static VisualMaskLayerRecord Project(VisualMaskLayerRecord source, Color32[] sourcePixels, Color32[] targetPixels)
        {
            int size = VisualMaskLayerRecord.Resolution;
            VisualMaskLayerRecord result = new VisualMaskLayerRecord { name = source?.name ?? "Mask" };
            if (source == null || sourcePixels?.Length != size * size || targetPixels?.Length != size * size) return result;
            RectInt sourceBounds = VisibleBounds(sourcePixels);
            RectInt targetBounds = VisibleBounds(targetPixels);
            if (sourceBounds.width <= 0 || sourceBounds.height <= 0 || targetBounds.width <= 0 || targetBounds.height <= 0) return result;
            for (int targetY = targetBounds.yMin; targetY < targetBounds.yMax; targetY++)
            for (int targetX = targetBounds.xMin; targetX < targetBounds.xMax; targetX++)
            {
                int targetPixelIndex = (size - 1 - targetY) * size + targetX;
                if (targetPixels[targetPixelIndex].a < VisibleAlpha) continue;
                float u = (targetX - targetBounds.xMin + 0.5f) / targetBounds.width;
                float v = (targetY - targetBounds.yMin + 0.5f) / targetBounds.height;
                int sourceX = Mathf.Clamp(sourceBounds.xMin + Mathf.FloorToInt(u * sourceBounds.width), sourceBounds.xMin, sourceBounds.xMax - 1);
                int sourceY = Mathf.Clamp(sourceBounds.yMin + Mathf.FloorToInt(v * sourceBounds.height), sourceBounds.yMin, sourceBounds.yMax - 1);
                if (source.IsPainted(sourceX, sourceY)) result.PaintPixel(targetX, targetY, true);
            }
            return result;
        }

        public static MaskValidationResult Validate(IReadOnlyList<VisualMaskLayerRecord> layers, Color32[] pixels, int tinySize)
        {
            int size = VisualMaskLayerRecord.Resolution;
            MaskValidationResult result = new MaskValidationResult();
            if (layers == null || pixels?.Length != size * size) return result;
            result.emptyChannels = layers.Count(layer => layer?.HasPixels != true);
            for (int layerIndex = 0; layerIndex < layers.Count; layerIndex++)
            {
                VisualMaskLayerRecord layer = layers[layerIndex];
                if (layer == null) continue;
                foreach (List<int> component in Components(ToBits(layer)))
                {
                    if (component.Count >= Mathf.Max(1, tinySize)) continue;
                    result.tinyFragments++;
                    foreach (int index in component)
                    {
                        int x = index % size;
                        int y = index / size;
                        result.tinyFragmentIssues.PaintPixel(x, y, true);
                        result.issues.PaintPixel(x, y, true);
                    }
                }
            }
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int count = 0;
                for (int layer = 0; layer < layers.Count; layer++) if (layers[layer]?.IsPainted(x, y) == true) count++;
                bool visible = pixels[(size - 1 - y) * size + x].a >= VisibleAlpha;
                if (count > 0 && !visible)
                {
                    result.transparentPixels++;
                    result.transparentPaintIssues.PaintPixel(x, y, true);
                    result.issues.PaintPixel(x, y, true);
                }
                if (count > 1)
                {
                    result.overlappingPixels++;
                    result.overlapIssues.PaintPixel(x, y, true);
                    result.issues.PaintPixel(x, y, true);
                }
                if (count == 0 && visible)
                {
                    result.unmaskedVisiblePixels++;
                    result.unmaskedVisibleIssues.PaintPixel(x, y, true);
                    result.issues.PaintPixel(x, y, true);
                }
            }
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    if (result.issues.IsPainted(x, y)) result.allIssuePixels++;
            result.transparentPaintComponents = IssueComponents(result.transparentPaintIssues);
            result.overlapComponents = IssueComponents(result.overlapIssues);
            result.tinyFragmentComponents = IssueComponents(result.tinyFragmentIssues);
            result.unmaskedVisibleComponents = IssueComponents(result.unmaskedVisibleIssues);
            result.allComponents = IssueComponents(result.issues);
            return result;
        }

#if HNS_VALIDATION
        internal static bool MaskPainterOperationsRegression()
        {
            int size = VisualMaskLayerRecord.Resolution;
            VisualMaskLayerRecord layer = new VisualMaskLayerRecord { name = "Leaves" };
            for (int y = 100; y <= 110; y++) for (int x = 100; x <= 110; x++) layer.PaintPixel(x, y, true);
            layer.PaintPixel(105, 105, false);
            layer.PaintPixel(20, 20, true);
            int original = Count(layer);
            if (!Grow(layer, 2) || Count(layer) <= original || !Shrink(layer, 1)) return false;
            FillHoles(layer); RemoveSmallComponents(layer, 8); KeepLargest(layer); Smooth(layer); Feather(layer);
            if (!layer.IsPainted(105, 105) || layer.IsPainted(20, 20)) return false;

            Color32[] pixels = Enumerable.Repeat(new Color32(40, 130, 55, 255), size * size).ToArray();
            for (int y = 0; y < size; y++) for (int x = 180; x < size; x++) pixels[y * size + x] = new Color32(190, 45, 48, 255);
            List<int> region = ConnectedTextureRegion(pixels, 50, 50, 0.08f);
            if (region.Count != 180 * size) return false;
            int beforeSmart = Count(layer); SmartExpand(layer, pixels, 3, 0.10f);
            if (Count(layer) <= beforeSmart) return false;

            Color32[] shifted = new Color32[pixels.Length];
            for (int y = 30; y < 230; y++) for (int x = 20; x < 220; x++) shifted[(size - 1 - y) * size + x] = new Color32(80, 90, 160, 255);
            VisualMaskLayerRecord projected = Project(layer, pixels, shifted);
            if (!projected.HasPixels) return false;
            MaskValidationResult validation = Validate(new[] { layer, layer.Clone(), new VisualMaskLayerRecord() }, pixels, 8);
            if (validation.overlappingPixels <= 0 || validation.emptyChannels != 1 || !validation.HasIssues) return false;

            Color32[] fillPixels = new Color32[size * size];
            fillPixels[(size - 1 - 10) * size + 10] = new Color32(255, 255, 255, 255);
            fillPixels[(size - 1 - 11) * size + 11] = new Color32(255, 255, 255, 255);
            fillPixels[(size - 1 - 12) * size + 12] = new Color32(0, 0, 0, 0);
            VisualMaskLayerRecord fillTarget = new VisualMaskLayerRecord { name = "Produce" };
            VisualMaskLayerRecord assigned = new VisualMaskLayerRecord { name = "Leaves" };
            assigned.PaintPixel(11, 11, true);
            List<VisualMaskLayerRecord> fillLayers = new List<VisualMaskLayerRecord> { fillTarget, assigned };
            VisualMaskLayerRecord beforeFill = fillTarget.Clone();
            bool filled = FillUnmasked(fillTarget, fillLayers, fillPixels);
            VisualMaskLayerRecord afterFill = fillTarget.Clone();
            bool fillRules = filled && fillTarget.IsPainted(10, 10) && !fillTarget.IsPainted(11, 11)
                && !fillTarget.IsPainted(12, 12) && !FillUnmasked(fillTarget, fillLayers, fillPixels);
            VisualMaskLayerRecord lockedTarget = beforeFill.Clone();
            bool lockRules = !FillUnmasked(lockedTarget, new[] { lockedTarget, assigned }, fillPixels, true)
                && lockedTarget.ContentHash == beforeFill.ContentHash;
            RestoreMask(fillTarget, beforeFill);
            bool undoRules = fillTarget.ContentHash == beforeFill.ContentHash;
            RestoreMask(fillTarget, afterFill);
            bool redoRules = fillTarget.ContentHash == afterFill.ContentHash;

            VisualMaskLayerRecord transparent = new VisualMaskLayerRecord { name = "Transparent" };
            VisualMaskLayerRecord overlap = new VisualMaskLayerRecord { name = "Overlap" };
            VisualMaskLayerRecord overlapOther = new VisualMaskLayerRecord { name = "OverlapOther" };
            VisualMaskLayerRecord tiny = new VisualMaskLayerRecord { name = "Tiny" };
            transparent.PaintPixel(2, 2, true);
            overlap.PaintPixel(3, 3, true);
            overlapOther.PaintPixel(3, 3, true);
            tiny.PaintPixel(5, 5, true);
            Color32[] categoryPixels = new Color32[size * size];
            categoryPixels[(size - 1 - 3) * size + 3] = new Color32(255, 255, 255, 255);
            categoryPixels[(size - 1 - 5) * size + 5] = new Color32(255, 255, 255, 255);
            categoryPixels[(size - 1 - 10) * size + 10] = new Color32(255, 255, 255, 255);
            categoryPixels[(size - 1 - 10) * size + 12] = new Color32(255, 255, 255, 255);
            MaskValidationResult categories = Validate(new[] { transparent, overlap, overlapOther, tiny }, categoryPixels, 2);
            MaskValidationNavigator navigator = new MaskValidationNavigator();
            navigator.SelectCategory(MaskValidationCategory.TransparentPaint, categories);
            bool transparentCategory = categories.transparentPaintIssues.IsPainted(2, 2)
                && categories.transparentPaintComponents.Count == 1;
            navigator.SelectCategory(MaskValidationCategory.Overlap, categories);
            bool overlapCategory = categories.overlapIssues.IsPainted(3, 3) && categories.overlapComponents.Count == 1;
            navigator.SelectCategory(MaskValidationCategory.TinyFragments, categories);
            bool tinyCategory = categories.tinyFragmentIssues.IsPainted(5, 5) && categories.tinyFragmentComponents.Count == 1;
            navigator.SelectCategory(MaskValidationCategory.UnmaskedVisible, categories);
            bool navigation = navigator.Move(categories, 1) && navigator.Current(categories)?.Count == 1
                && navigator.Move(categories, 1) && navigator.Move(categories, -1);
            navigator.Invalidate();
            bool invalidated = navigator.Category == MaskValidationCategory.All && navigator.IssueIndex < 0;
            return fillRules && lockRules && undoRules && redoRules && transparentCategory && overlapCategory
                && tinyCategory && categories.unmaskedVisibleComponents.Count == 2 && navigation && invalidated
                && MaskProjectionRegression.Run();
        }
#endif

        private static void RestoreMask(VisualMaskLayerRecord destination, VisualMaskLayerRecord source)
        {
            destination.Clear();
            for (int y = 0; y < VisualMaskLayerRecord.Resolution; y++)
                for (int x = 0; x < VisualMaskLayerRecord.Resolution; x++)
                    if (source.IsPainted(x, y)) destination.PaintPixel(x, y, true);
        }

        private static List<MaskIssueComponent> IssueComponents(VisualMaskLayerRecord layer)
        {
            List<MaskIssueComponent> result = new List<MaskIssueComponent>();
            foreach (List<int> component in Components(ToBits(layer)))
                result.Add(new MaskIssueComponent(component, VisualMaskLayerRecord.Resolution));
            return result;
        }

        private static bool[] ToBits(VisualMaskLayerRecord layer)
        {
            int size = VisualMaskLayerRecord.Resolution;
            bool[] result = new bool[size * size];
            if (layer == null) return result;
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++) result[y * size + x] = layer.IsPainted(x, y);
            return result;
        }

        private static bool Transform(VisualMaskLayerRecord layer, bool[] result)
        {
            if (layer == null || result == null) return false;
            int previous = layer.ContentHash;
            layer.Clear();
            int size = VisualMaskLayerRecord.Resolution;
            for (int index = 0; index < result.Length; index++) if (result[index]) layer.PaintPixel(index % size, index / size, true);
            return previous != layer.ContentHash;
        }

        private static bool[] Dilate(bool[] source, int amount)
        {
            int size = VisualMaskLayerRecord.Resolution;
            bool[] current = (bool[])source.Clone();
            for (int pass = 0; pass < amount; pass++)
            {
                bool[] next = (bool[])current.Clone();
                for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
                {
                    if (!current[y * size + x]) continue;
                    for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
                    {
                        int nx = x + dx; int ny = y + dy;
                        if (nx >= 0 && nx < size && ny >= 0 && ny < size) next[ny * size + nx] = true;
                    }
                }
                current = next;
            }
            return current;
        }

        private static bool[] Erode(bool[] source, int amount)
        {
            int size = VisualMaskLayerRecord.Resolution;
            bool[] current = (bool[])source.Clone();
            for (int pass = 0; pass < amount; pass++)
            {
                bool[] next = (bool[])current.Clone();
                for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
                {
                    int index = y * size + x;
                    if (!current[index]) continue;
                    bool keep = true;
                    for (int dy = -1; dy <= 1 && keep; dy++) for (int dx = -1; dx <= 1; dx++)
                    {
                        int nx = x + dx; int ny = y + dy;
                        if (nx < 0 || nx >= size || ny < 0 || ny >= size || !current[ny * size + nx]) { keep = false; break; }
                    }
                    next[index] = keep;
                }
                current = next;
            }
            return current;
        }

        private static List<List<int>> Components(bool[] bits)
        {
            int size = VisualMaskLayerRecord.Resolution;
            List<List<int>> result = new List<List<int>>();
            bool[] visited = new bool[bits.Length];
            for (int seed = 0; seed < bits.Length; seed++)
            {
                if (!bits[seed] || visited[seed]) continue;
                List<int> component = new List<int>(); Queue<int> pending = new Queue<int>();
                visited[seed] = true; pending.Enqueue(seed);
                while (pending.Count > 0)
                {
                    int index = pending.Dequeue(); component.Add(index); int x = index % size; int y = index / size;
                    for (int direction = 0; direction < 4; direction++)
                    {
                        int nx = x + NeighborX[direction]; int ny = y + NeighborY[direction];
                        if (nx < 0 || nx >= size || ny < 0 || ny >= size) continue;
                        int next = ny * size + nx;
                        if (bits[next] && !visited[next]) { visited[next] = true; pending.Enqueue(next); }
                    }
                }
                result.Add(component);
            }
            return result;
        }

        private static RectInt VisibleBounds(Color32[] pixels)
        {
            int size = VisualMaskLayerRecord.Resolution;
            int minX = size, minY = size, maxX = -1, maxY = -1;
            for (int pixelY = 0; pixelY < size; pixelY++) for (int x = 0; x < size; x++)
            {
                if (pixels[pixelY * size + x].a < VisibleAlpha) continue;
                int topY = size - 1 - pixelY;
                minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x);
                minY = Mathf.Min(minY, topY); maxY = Mathf.Max(maxY, topY);
            }
            return maxX < minX ? new RectInt() : new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
        }

        private static void Enqueue(int x, int y, int size, bool[] visited, Queue<int> pending)
        {
            if (x < 0 || x >= size || y < 0 || y >= size) return;
            int index = y * size + x;
            if (visited[index]) return;
            visited[index] = true; pending.Enqueue(index);
        }

        private static float ColorDistance(Color32 first, Color32 second)
        {
            float red = (first.r - second.r) / 255f;
            float green = (first.g - second.g) / 255f;
            float blue = (first.b - second.b) / 255f;
            return Mathf.Sqrt(red * red + green * green + blue * blue) / 1.7320508f;
        }

        private static int Count(VisualMaskLayerRecord layer)
        {
            int result = 0; int size = VisualMaskLayerRecord.Resolution;
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++) if (layer.IsPainted(x, y)) result++;
            return result;
        }
    }
}
