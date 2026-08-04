using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace HorticultureNovelSeeds
{
    public sealed class MaskProjectionChannelResult
    {
        public int ChannelIndex;
        public float Confidence;
        public int AddedPixels;
        public int RemovedPixels;
        public int Conflicts;
        public int RemainingUnmaskedVisiblePixels;
        public VisualMaskLayerRecord CandidateLayer;
        public string ChannelName => ChannelIndex == 0 ? "Produce" : ChannelIndex == 1 ? "Leaves" : "Stem";
    }

    public sealed class MaskProjectionResult
    {
        public List<VisualMaskLayerRecord> CandidateLayers = new List<VisualMaskLayerRecord>();
        public MaskProjectionChannelResult[] Channels = new MaskProjectionChannelResult[3];
        public int VisibleTargetPixels;

        public bool HasCandidate => CandidateLayers.Count == 3;
    }

    internal static class SemanticMaskProjection
    {
        private const int Resolution = VisualMaskLayerRecord.Resolution;
        private const byte VisibleAlpha = 16;
        private const float ColorConnectionTolerance = 0.09f;
        private const float MinimumMatchScore = 0.20f;

        private sealed class Component
        {
            public int Channel;
            public int Index;
            public List<int> Pixels = new List<int>();
            public float CenterX;
            public float CenterY;
            public float RelativeX;
            public float RelativeY;
            public float RelativeWidth;
            public float RelativeHeight;
            public float AreaShare;
            public float Compactness;
            public float Aspect;
            public float Adjacency;
            public float Connectivity;
            public Color Color;
        }

        private sealed class Match
        {
            public Component Source;
            public Component Target;
            public float Score;
        }

        private sealed class RegionProposal
        {
            public Component Source;
            public Component Target;
            public float Score;
            public float InitialOverlap;
            public float Evidence;
        }

        private struct Bounds
        {
            public int MinX;
            public int MinY;
            public int MaxX;
            public int MaxY;
            public bool HasPixels;

            public float Width => Mathf.Max(1, MaxX - MinX);
            public float Height => Mathf.Max(1, MaxY - MinY);
        }

        public static MaskProjectionResult Build(IReadOnlyList<VisualMaskLayerRecord> sourceLayers,
            Color32[] sourcePixels, IReadOnlyList<VisualMaskLayerRecord> targetLayers, Color32[] targetPixels)
        {
            MaskProjectionResult result = new MaskProjectionResult();
            for (int channel = 0; channel < 3; channel++) result.CandidateLayers.Add(new VisualMaskLayerRecord { name = LayerName(channel) });
            if (sourcePixels == null || targetPixels == null || sourcePixels.Length != Resolution * Resolution
                || targetPixels.Length != Resolution * Resolution) return result;

            List<Component> source = SourceComponents(sourceLayers, sourcePixels);
            List<Component> targets = TargetComponents(targetPixels);
            NormalizeFeatures(source, sourcePixels, true);
            NormalizeFeatures(targets, targetPixels, false);
            result.VisibleTargetPixels = targetPixels.Count(pixel => pixel.a >= VisibleAlpha);

            List<VisualMaskLayerRecord> initialCandidates = InitialCandidates(sourceLayers, sourcePixels, targetPixels);
            for (int channel = 0; channel < 3; channel++)
                result.CandidateLayers[channel] = initialCandidates[channel].Clone();
            List<RegionProposal> proposals = new List<RegionProposal>();
            for (int channel = 0; channel < 3; channel++)
            {
                List<Component> channelComponents = source.Where(component => component.Channel == channel)
                    .OrderByDescending(component => component.Pixels.Count).ThenBy(component => component.Index).ToList();
                foreach (Component target in targets)
                {
                    RegionProposal best = channelComponents.Select(component => new RegionProposal
                    {
                        Source = component,
                        Target = target,
                        Score = Score(component, target)
                    }).OrderByDescending(proposal => proposal.Score)
                        .ThenBy(proposal => proposal.Source.Index).FirstOrDefault();
                    if (best == null) continue;
                    best.InitialOverlap = InitialOverlap(target, initialCandidates[channel]);
                    if (best.InitialOverlap <= 0f && best.Score < MinimumMatchScore) continue;
                    best.Evidence = best.Score * 0.70f + best.InitialOverlap * 0.30f;
                    proposals.Add(best);
                }
            }

            List<Match> matches = new List<Match>();
            foreach (IGrouping<int, RegionProposal> group in proposals.GroupBy(proposal => proposal.Target.Index))
            {
                List<RegionProposal> contenders = group.OrderByDescending(proposal => proposal.Evidence)
                    .ThenByDescending(proposal => proposal.Score)
                    .ThenBy(proposal => proposal.Source.Channel).ThenBy(proposal => proposal.Source.Index).ToList();
                RegionProposal winner = contenders[0];
                matches.Add(new Match { Source = winner.Source, Target = winner.Target, Score = winner.Score });
                foreach (int pixel in winner.Target.Pixels) PaintTargetPixel(result.CandidateLayers[winner.Source.Channel], pixel, true);
                if (contenders.Count > 1)
                    foreach (RegionProposal contender in contenders.Skip(1))
                    {
                        GetChannel(result, contender.Source.Channel).Conflicts += contender.Target.Pixels.Count;
                    }
            }

            // Resolve only genuine source overlap after the complete visible-bounds candidates and
            // region refinements have been combined. The lowest channel index wins ties so output is
            // deterministic, while accepted-channel application still protects rejected layers.
            ResolveCandidateOverlaps(result);

            for (int channel = 0; channel < 3; channel++)
            {
                List<Match> channelMatches = matches.Where(match => match.Source.Channel == channel).ToList();
                MaskProjectionChannelResult channelResult = GetChannel(result, channel);
                channelResult.CandidateLayer = result.CandidateLayers[channel];
                channelResult.Confidence = Confidence(channelMatches, source, channel, channelResult.Conflicts);
                channelResult.RemainingUnmaskedVisiblePixels = RemainingVisible(targetPixels, result.CandidateLayers);
                VisualMaskLayerRecord existing = targetLayers != null && channel < targetLayers.Count
                    ? targetLayers[channel] : new VisualMaskLayerRecord();
                for (int y = 0; y < Resolution; y++) for (int x = 0; x < Resolution; x++)
                {
                    bool before = existing.IsPainted(x, y);
                    bool after = result.CandidateLayers[channel].IsPainted(x, y);
                    if (!before && after) channelResult.AddedPixels++;
                    if (before && !after) channelResult.RemovedPixels++;
                }
            }
            return result;
        }

        public static List<VisualMaskLayerRecord> ApplyAccepted(IReadOnlyList<VisualMaskLayerRecord> current,
            MaskProjectionResult projection, bool[] accepted, out bool changed)
        {
            List<VisualMaskLayerRecord> result = current?.Select(layer => layer?.Clone() ?? new VisualMaskLayerRecord()).ToList()
                ?? EmptyLayers();
            while (result.Count < 3) result.Add(new VisualMaskLayerRecord());
            if (result.Count > 3) result.RemoveRange(3, result.Count - 3);
            int[] beforeHashes = result.Select(layer => layer.ContentHash).ToArray();
            changed = false;
            bool anyAccepted = accepted != null && accepted.Length >= 3 && accepted.Any(value => value);
            if (!anyAccepted || projection == null || !projection.HasCandidate) return result;
            // Clear every accepted channel first. Rejected channels retain their current content and
            // therefore remain authoritative when accepted candidates are made exclusive.
            for (int channel = 0; channel < 3; channel++)
                if (accepted[channel]) result[channel].Clear();
            for (int channel = 0; channel < 3; channel++)
            {
                if (!accepted[channel]) continue;
                for (int y = 0; y < Resolution; y++) for (int x = 0; x < Resolution; x++)
                {
                    if (!projection.CandidateLayers[channel].IsPainted(x, y)) continue;
                    bool blocked = false;
                    for (int other = 0; other < 3; other++)
                        if (other != channel && result[other].IsPainted(x, y)) { blocked = true; break; }
                    if (!blocked) result[channel].PaintPixel(x, y, true);
                }
            }
            changed = Enumerable.Range(0, 3).Any(channel => beforeHashes[channel] != result[channel].ContentHash);
            return result;
        }

        private static List<VisualMaskLayerRecord> InitialCandidates(IReadOnlyList<VisualMaskLayerRecord> sourceLayers,
            Color32[] sourcePixels, Color32[] targetPixels)
        {
            List<VisualMaskLayerRecord> candidates = EmptyLayers();
            Bounds sourceBounds = VisibleBounds(sourcePixels);
            if (!sourceBounds.HasPixels) sourceBounds = MaskBounds(sourceLayers);
            Bounds targetBounds = VisibleBounds(targetPixels);
            if (!sourceBounds.HasPixels || !targetBounds.HasPixels) return candidates;
            for (int channel = 0; channel < 3; channel++)
            {
                VisualMaskLayerRecord sourceLayer = sourceLayers != null && channel < sourceLayers.Count
                    ? sourceLayers[channel] : null;
                if (sourceLayer == null) continue;
                for (int maskY = 0; maskY < Resolution; maskY++) for (int x = 0; x < Resolution; x++)
                {
                    if (!sourceLayer.IsPainted(x, maskY)) continue;
                    int topY = Resolution - 1 - maskY;
                    float relativeX = (x - sourceBounds.MinX) / sourceBounds.Width;
                    float relativeY = (topY - sourceBounds.MinY) / sourceBounds.Height;
                    int targetX = Mathf.RoundToInt(targetBounds.MinX + relativeX * (targetBounds.MaxX - targetBounds.MinX));
                    int targetY = Mathf.RoundToInt(targetBounds.MinY + relativeY * (targetBounds.MaxY - targetBounds.MinY));
                    int targetPixel = NearestVisiblePixel(targetPixels, targetX, targetY, targetBounds);
                    if (targetPixel >= 0) PaintTargetPixel(candidates[channel], targetPixel, true);
                }
            }
            return candidates;
        }

        private static int NearestVisiblePixel(Color32[] pixels, int x, int y, Bounds bounds)
        {
            x = Mathf.Clamp(x, bounds.MinX, bounds.MaxX);
            y = Mathf.Clamp(y, bounds.MinY, bounds.MaxY);
            if (pixels[y * Resolution + x].a >= VisibleAlpha) return y * Resolution + x;
            for (int radius = 1; radius <= 8; radius++)
                for (int dy = -radius; dy <= radius; dy++) for (int dx = -radius; dx <= radius; dx++)
                {
                    if (Mathf.Abs(dx) != radius && Mathf.Abs(dy) != radius) continue;
                    int nx = Mathf.Clamp(x + dx, bounds.MinX, bounds.MaxX);
                    int ny = Mathf.Clamp(y + dy, bounds.MinY, bounds.MaxY);
                    if (pixels[ny * Resolution + nx].a >= VisibleAlpha) return ny * Resolution + nx;
                }
            return -1;
        }

        private static float InitialOverlap(Component target, VisualMaskLayerRecord initial)
        {
            if (target == null || initial == null || target.Pixels.Count == 0) return 0f;
            int overlap = target.Pixels.Count(pixel => initial.IsPainted(pixel % Resolution, Resolution - 1 - pixel / Resolution));
            return overlap / (float)target.Pixels.Count;
        }

        private static Bounds VisibleBounds(Color32[] pixels)
        {
            Bounds bounds = new Bounds { MinX = Resolution, MinY = Resolution, MaxX = -1, MaxY = -1 };
            if (pixels == null) return bounds;
            for (int index = 0; index < pixels.Length; index++) if (pixels[index].a >= VisibleAlpha)
            {
                int x = index % Resolution; int y = index / Resolution;
                bounds.MinX = Mathf.Min(bounds.MinX, x); bounds.MinY = Mathf.Min(bounds.MinY, y);
                bounds.MaxX = Mathf.Max(bounds.MaxX, x); bounds.MaxY = Mathf.Max(bounds.MaxY, y);
            }
            bounds.HasPixels = bounds.MaxX >= bounds.MinX && bounds.MaxY >= bounds.MinY;
            return bounds;
        }

        private static Bounds MaskBounds(IReadOnlyList<VisualMaskLayerRecord> layers)
        {
            Bounds bounds = new Bounds { MinX = Resolution, MinY = Resolution, MaxX = -1, MaxY = -1 };
            if (layers == null) return bounds;
            for (int channel = 0; channel < layers.Count; channel++)
            {
                VisualMaskLayerRecord layer = layers[channel];
                if (layer == null) continue;
                for (int maskY = 0; maskY < Resolution; maskY++) for (int x = 0; x < Resolution; x++)
                    if (layer.IsPainted(x, maskY))
                    {
                        int topY = Resolution - 1 - maskY;
                        bounds.MinX = Mathf.Min(bounds.MinX, x); bounds.MinY = Mathf.Min(bounds.MinY, topY);
                        bounds.MaxX = Mathf.Max(bounds.MaxX, x); bounds.MaxY = Mathf.Max(bounds.MaxY, topY);
                    }
            }
            bounds.HasPixels = bounds.MaxX >= bounds.MinX && bounds.MaxY >= bounds.MinY;
            return bounds;
        }

        private static List<Component> SourceComponents(IReadOnlyList<VisualMaskLayerRecord> layers, Color32[] pixels)
        {
            List<Component> components = new List<Component>();
            for (int channel = 0; channel < 3; channel++)
            {
                bool[] bits = new bool[Resolution * Resolution];
                if (layers != null && channel < layers.Count && layers[channel] != null)
                    for (int y = 0; y < Resolution; y++) for (int x = 0; x < Resolution; x++)
                        bits[y * Resolution + x] = layers[channel].IsPainted(x, y);
                List<List<int>> pixelsForComponents = Components(bits, false);
                for (int index = 0; index < pixelsForComponents.Count; index++)
                    components.Add(new Component { Channel = channel, Index = components.Count, Pixels = pixelsForComponents[index] });
            }
            return components;
        }

        private static List<Component> TargetComponents(Color32[] pixels)
        {
            bool[] visible = pixels.Select(pixel => pixel.a >= VisibleAlpha).ToArray();
            bool[] visited = new bool[visible.Length];
            List<Component> result = new List<Component>();
            for (int y = 0; y < Resolution; y++) for (int x = 0; x < Resolution; x++)
            {
                int start = y * Resolution + x;
                if (!visible[start] || visited[start]) continue;
                Color root = ToColor(pixels[start]);
                Queue<int> queue = new Queue<int>();
                List<int> region = new List<int>();
                queue.Enqueue(start); visited[start] = true;
                while (queue.Count > 0)
                {
                    int index = queue.Dequeue();
                    region.Add(index);
                    int cx = index % Resolution; int cy = index / Resolution;
                    for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        int nx = cx + dx; int ny = cy + dy;
                        if (nx < 0 || nx >= Resolution || ny < 0 || ny >= Resolution) continue;
                        int neighbor = ny * Resolution + nx;
                        if (!visible[neighbor] || visited[neighbor] || ColorDistance(root, ToColor(pixels[neighbor])) > ColorConnectionTolerance) continue;
                        visited[neighbor] = true; queue.Enqueue(neighbor);
                    }
                }
                result.Add(new Component { Channel = -1, Index = result.Count, Pixels = region });
            }
            return result;
        }

        private static void NormalizeFeatures(List<Component> components, Color32[] pixels, bool maskCoordinates)
        {
            if (components.Count == 0) return;
            int minX = Resolution; int minY = Resolution; int maxX = 0; int maxY = 0; int visible = 0;
            bool[] occupied = new bool[Resolution * Resolution];
            foreach (Component component in components)
                foreach (int pixelIndex in component.Pixels) occupied[pixelIndex] = true;
            if (!maskCoordinates)
                for (int scanIndex = 0; scanIndex < pixels.Length; scanIndex++) if (pixels[scanIndex].a >= VisibleAlpha)
                {
                    int x = scanIndex % Resolution; int y = scanIndex / Resolution;
                    minX = Mathf.Min(minX, x); minY = Mathf.Min(minY, y); maxX = Mathf.Max(maxX, x); maxY = Mathf.Max(maxY, y); visible++;
                }
            else
                for (int maskIndex = 0; maskIndex < occupied.Length; maskIndex++) if (occupied[maskIndex])
                {
                    int x = maskIndex % Resolution; int y = Resolution - 1 - maskIndex / Resolution;
                    minX = Mathf.Min(minX, x); minY = Mathf.Min(minY, y); maxX = Mathf.Max(maxX, x); maxY = Mathf.Max(maxY, y); visible++;
                }
            float width = Mathf.Max(1, maxX - minX + 1); float height = Mathf.Max(1, maxY - minY + 1);
            foreach (Component component in components)
            {
                int componentMinX = Resolution; int componentMinY = Resolution; int componentMaxX = 0; int componentMaxY = 0;
                float centerX = 0f; float centerY = 0f; float red = 0f; float green = 0f; float blue = 0f;
                foreach (int componentIndex in component.Pixels)
                {
                    int x = componentIndex % Resolution; int y = maskCoordinates ? Resolution - 1 - componentIndex / Resolution : componentIndex / Resolution;
                    componentMinX = Mathf.Min(componentMinX, x); componentMinY = Mathf.Min(componentMinY, y);
                    componentMaxX = Mathf.Max(componentMaxX, x); componentMaxY = Mathf.Max(componentMaxY, y);
                    centerX += x; centerY += y;
                    Color color = ToColor(pixels[maskCoordinates ? (Resolution - 1 - y) * Resolution + x : componentIndex]);
                    red += color.r; green += color.g; blue += color.b;
                }
                float count = Mathf.Max(1, component.Pixels.Count);
                component.CenterX = centerX / count; component.CenterY = centerY / count;
                component.RelativeX = Mathf.Clamp01((component.CenterX - minX) / width);
                component.RelativeY = Mathf.Clamp01((component.CenterY - minY) / height);
                component.RelativeWidth = (componentMaxX - componentMinX + 1) / width;
                component.RelativeHeight = (componentMaxY - componentMinY + 1) / height;
                component.AreaShare = component.Pixels.Count / (float)Mathf.Max(1, visible);
                float boxArea = Mathf.Max(1, (componentMaxX - componentMinX + 1) * (componentMaxY - componentMinY + 1));
                component.Compactness = component.Pixels.Count / boxArea;
                component.Aspect = Mathf.Min(componentMaxX - componentMinX + 1, componentMaxY - componentMinY + 1)
                    / (float)Mathf.Max(1, Mathf.Max(componentMaxX - componentMinX + 1, componentMaxY - componentMinY + 1));
                int adjacent = 0; int connected = 0;
                foreach (int index in component.Pixels)
                {
                    int x = index % Resolution; int y = index / Resolution;
                    for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        int nx = x + dx; int ny = y + dy;
                        if (nx < 0 || nx >= Resolution || ny < 0 || ny >= Resolution) { adjacent++; continue; }
                        if (occupied[ny * Resolution + nx]) connected++; else adjacent++;
                    }
                }
                component.Adjacency = adjacent / (float)Mathf.Max(1, component.Pixels.Count * 8);
                component.Connectivity = connected / (float)Mathf.Max(1, component.Pixels.Count * 8);
                component.Color = new Color(red / count, green / count, blue / count, 1f);
            }
        }

        private static float Score(Component source, Component target)
        {
            float dx = source.RelativeX - target.RelativeX; float dy = source.RelativeY - target.RelativeY;
            float position = 1f - Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy) / 1.4143f);
            float color = 1f - Mathf.Clamp01(ColorDistance(source.Color, target.Color) / 0.75f);
            float area = 1f - Mathf.Clamp01(Mathf.Abs(source.AreaShare - target.AreaShare)
                / Mathf.Max(source.AreaShare, Mathf.Max(target.AreaShare, 0.01f)));
            float shape = 1f - Mathf.Clamp01((Mathf.Abs(source.Compactness - target.Compactness)
                + Mathf.Abs(source.Aspect - target.Aspect)) * 0.5f);
            float adjacency = 1f - Mathf.Abs(source.Adjacency - target.Adjacency);
            float connectivity = 1f - Mathf.Abs(source.Connectivity - target.Connectivity);
            return position * 0.30f + color * 0.20f + area * 0.15f + shape * 0.15f
                + adjacency * 0.10f + connectivity * 0.10f;
        }

        private static float Confidence(List<Match> matches, List<Component> source, int channel, int conflicts)
        {
            List<Component> sourceChannel = source.Where(component => component.Channel == channel).ToList();
            if (sourceChannel.Count == 0) return 0f;
            float score = matches.Count == 0 ? 0f : matches.Average(match => match.Score);
            float coverage = matches.Count / (float)sourceChannel.Count;
            float conflictPenalty = conflicts / (float)Mathf.Max(1, matches.Sum(match => match.Target.Pixels.Count));
            return Mathf.Clamp01(score * (0.70f + coverage * 0.30f) * (1f - Mathf.Clamp01(conflictPenalty * 0.5f)));
        }

        private static int RemainingVisible(Color32[] pixels, List<VisualMaskLayerRecord> layers)
        {
            int count = 0;
            for (int y = 0; y < Resolution; y++) for (int x = 0; x < Resolution; x++)
            {
                if (pixels[y * Resolution + x].a < VisibleAlpha) continue;
                bool assigned = layers.Any(layer => layer.IsPainted(x, Resolution - 1 - y));
                if (!assigned) count++;
            }
            return count;
        }

        private static void ResolveCandidateOverlaps(MaskProjectionResult result)
        {
            for (int y = 0; y < Resolution; y++) for (int x = 0; x < Resolution; x++)
            {
                int winner = -1;
                for (int channel = 0; channel < 3; channel++)
                {
                    if (!result.CandidateLayers[channel].IsPainted(x, y)) continue;
                    if (winner < 0) winner = channel;
                    else
                    {
                        result.CandidateLayers[channel].PaintPixel(x, y, false);
                        GetChannel(result, channel).Conflicts++;
                    }
                }
            }
        }

        private static MaskProjectionChannelResult GetChannel(MaskProjectionResult result, int channel)
        {
            if (result.Channels[channel] == null) result.Channels[channel] = new MaskProjectionChannelResult { ChannelIndex = channel };
            return result.Channels[channel];
        }

        private static List<List<int>> Components(bool[] bits, bool diagonal)
        {
            List<List<int>> result = new List<List<int>>();
            bool[] visited = new bool[bits.Length];
            for (int y = 0; y < Resolution; y++) for (int x = 0; x < Resolution; x++)
            {
                int start = y * Resolution + x;
                if (!bits[start] || visited[start]) continue;
                List<int> component = new List<int>(); Queue<int> queue = new Queue<int>();
                visited[start] = true; queue.Enqueue(start);
                while (queue.Count > 0)
                {
                    int index = queue.Dequeue(); component.Add(index);
                    int cx = index % Resolution; int cy = index / Resolution;
                    for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0 || !diagonal && Mathf.Abs(dx) + Mathf.Abs(dy) != 1) continue;
                        int nx = cx + dx; int ny = cy + dy;
                        if (nx < 0 || nx >= Resolution || ny < 0 || ny >= Resolution) continue;
                        int next = ny * Resolution + nx;
                        if (!bits[next] || visited[next]) continue;
                        visited[next] = true; queue.Enqueue(next);
                    }
                }
                result.Add(component);
            }
            return result;
        }

        private static float ColorDistance(Color first, Color second)
        {
            float red = first.r - second.r; float green = first.g - second.g; float blue = first.b - second.b;
            return Mathf.Sqrt(red * red + green * green + blue * blue);
        }

        private static Color ToColor(Color32 color)
        {
            return new Color(color.r / 255f, color.g / 255f, color.b / 255f, color.a / 255f);
        }

        private static void PaintTargetPixel(VisualMaskLayerRecord layer, int targetPixelIndex, bool painted)
        {
            layer.PaintPixel(targetPixelIndex % Resolution, Resolution - 1 - targetPixelIndex / Resolution, painted);
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

        private static string LayerName(int channel)
        {
            return channel == 0 ? "Produce" : channel == 1 ? "Leaves" : "Stem";
        }
    }
}
