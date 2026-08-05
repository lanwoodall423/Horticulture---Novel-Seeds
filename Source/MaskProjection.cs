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
        public float TransformedSourceCoverage;
        public float SpatialAgreement;
        public float SemanticAgreement;
        public float AssignmentAgreement;
        public float SourceAreaShare;
        public float TargetAreaShare;
        public float ConflictRatio;
        public int ArbitrationDomainPixels;
        public int ExpectedTargetPixels;
        public int AssignedTargetPixels;
        public int MissingCoveragePixels;
        public int AmbiguousAssignments;
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
        public int ArbitrationDomainPixels;
        public int UnresolvedConflictPixels;
        public int Conflicts => UnresolvedConflictPixels;
        public int AmbiguousAssignments => UnresolvedConflictPixels;
        public int RemainingUnmaskedVisiblePixels;
        public VisualMaskLayerRecord UnresolvedConflictMask = new VisualMaskLayerRecord { name = "Projection Conflicts" };
        internal float[][] EvidenceByChannel;

        public bool HasCandidate => CandidateLayers.Count == 3;
    }

    internal static class SemanticMaskProjection
    {
        private const int Resolution = VisualMaskLayerRecord.Resolution;
        private const byte VisibleAlpha = 16;
        private const float ColorConnectionTolerance = 0.09f;
        private const float MinimumMatchScore = 0.20f;
        private const float MinimumArbitrationSeparation = 0.08f;

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

        private struct VisibleDomain
        {
            public Bounds Bounds;
            public int PixelCount;

            public bool HasPixels => Bounds.HasPixels && PixelCount > 0;
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
            VisibleDomain sourceDomain = SourceVisibleDomain(source);
            VisibleDomain targetDomain = TargetVisibleDomain(targetPixels);
            NormalizeFeatures(source, sourcePixels, true, sourceDomain);
            NormalizeFeatures(targets, targetPixels, false, targetDomain);
            result.VisibleTargetPixels = targetDomain.PixelCount;

            // The shared transform is evidence only. Final candidates are assigned by whole
            // semantic target regions, so an unrelated visible background cannot be painted
            // merely because a transformed source pixel landed there.
            List<VisualMaskLayerRecord> initialCandidates = InitialCandidates(sourceLayers, sourcePixels, targetPixels,
                sourceDomain.Bounds, targetDomain.Bounds);

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
                    // A semantic region still needs a small amount of transform evidence. This
                    // rejects unrelated visible islands without copying the initial mask into
                    // the final assignment; the whole target region is still painted only by
                    // the semantic arbitration below.
                    if (best.InitialOverlap <= 0f || best.Score < MinimumMatchScore
                        || ColorAgreement(best.Source, best.Target) < 0.35f) continue;
                    best.Evidence = best.Score * 0.70f + best.InitialOverlap * 0.30f;
                    proposals.Add(best);
                }
            }

            bool[] arbitrationDomain = new bool[Resolution * Resolution];
            bool[][] channelDomains = EmptyChannelPixelSets();
            bool[][] ambiguousByChannel = EmptyChannelPixelSets();

            bool[] ambiguousPixels = new bool[Resolution * Resolution];
            ResolveRegionProposals(result, proposals, arbitrationDomain, channelDomains, ambiguousPixels,
                ambiguousByChannel);
            int[] targetRegionByPixel = TargetRegionByPixel(targets);
            result.EvidenceByChannel = BuildPixelEvidence(result.CandidateLayers, initialCandidates, source,
                targets, targetRegionByPixel, targetPixels);

            VisualMaskLayerRecord[][] beforeArbitration = result.CandidateLayers
                .Select(layer => new[] { layer.Clone() }).ToArray();
            ArbitrateCandidateOverlaps(result, beforeArbitration.Select(value => value[0]).ToList(), targetRegionByPixel,
                arbitrationDomain, channelDomains, ambiguousPixels, ambiguousByChannel);

            result.ArbitrationDomainPixels = CountPixels(arbitrationDomain);
            result.UnresolvedConflictPixels = CountPixels(arbitrationDomain, ambiguousPixels);
            result.RemainingUnmaskedVisiblePixels = RemainingVisible(targetPixels, result.CandidateLayers);
            for (int topPixel = 0; topPixel < ambiguousPixels.Length; topPixel++)
                if (arbitrationDomain[topPixel] && ambiguousPixels[topPixel]) PaintTargetPixel(result.UnresolvedConflictMask,
                    topPixel, true);

            for (int channel = 0; channel < 3; channel++)
            {
                MaskProjectionChannelResult channelResult = GetChannel(result, channel);
                channelResult.CandidateLayer = result.CandidateLayers[channel];
                CountAmbiguity(channelResult, ambiguousByChannel[channel]);
                channelResult.RemainingUnmaskedVisiblePixels = RemainingVisible(targetPixels,
                    new List<VisualMaskLayerRecord> { channelResult.CandidateLayer }, channelDomains[channel]);
                VisualMaskLayerRecord existing = targetLayers != null && channel < targetLayers.Count
                    ? targetLayers[channel] : new VisualMaskLayerRecord();
                int initialPixels = CountPainted(initialCandidates[channel]);
                int sourcePixelsForChannel = source.Where(component => component.Channel == channel)
                    .Sum(component => component.Pixels.Count);
                int finalPixels = CountPainted(channelResult.CandidateLayer);
                int retainedInitialPixels = IntersectionCount(initialCandidates[channel], channelResult.CandidateLayer);
                channelResult.ArbitrationDomainPixels = CountPixels(channelDomains[channel]);
                channelResult.ExpectedTargetPixels = initialPixels;
                channelResult.AssignedTargetPixels = finalPixels;
                channelResult.SourceAreaShare = SafeRatio(sourcePixelsForChannel, sourceDomain.PixelCount);
                channelResult.TargetAreaShare = SafeRatio(CountPixels(channelDomains[channel]), targetDomain.PixelCount);
                channelResult.MissingCoveragePixels = Math.Max(0, initialPixels - retainedInitialPixels);
                float expectedCoverage = SafeRatio(retainedInitialPixels, initialPixels);
                float assignedPrecision = SafeRatio(retainedInitialPixels, finalPixels);
                channelResult.TransformedSourceCoverage = expectedCoverage;
                channelResult.SpatialAgreement = LayerIoU(initialCandidates[channel], channelResult.CandidateLayer);
                channelResult.SemanticAgreement = SemanticAgreement(channelResult.CandidateLayer, source, channel,
                    targets, targetRegionByPixel);
                channelResult.AssignmentAgreement = AssignmentAgreement(initialCandidates[channel],
                    channelResult.CandidateLayer);
                int conflictDomain = Math.Max(1, initialPixels + channelResult.Conflicts);
                channelResult.ConflictRatio = SafeRatio(channelResult.Conflicts, conflictDomain);
                float conflictFree = 1f - channelResult.ConflictRatio;
                float ambiguityFree = 1f - SafeRatio(channelResult.AmbiguousAssignments, conflictDomain);
                channelResult.Confidence = BoundedConfidence(expectedCoverage, assignedPrecision,
                    channelResult.SpatialAgreement, channelResult.SemanticAgreement, conflictFree,
                    ambiguityFree, initialPixels, finalPixels, sourcePixelsForChannel > 0);
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

            // Clear all accepted channels before evaluating occupancy. Rejected channels remain
            // authoritative and can block a projected pixel.
            for (int channel = 0; channel < 3; channel++) if (accepted[channel]) result[channel].Clear();
            for (int maskY = 0; maskY < Resolution; maskY++) for (int x = 0; x < Resolution; x++)
            {
                List<int> contenders = new List<int>();
                for (int channel = 0; channel < 3; channel++)
                    if (accepted[channel] && projection.CandidateLayers[channel].IsPainted(x, maskY)) contenders.Add(channel);
                if (contenders.Count == 0) continue;
                int winner = WinnerForPixel(projection, contenders, x, maskY);
                if (winner < 0) continue;
                bool blocked = false;
                for (int other = 0; other < 3; other++)
                    if (!accepted[other] && result[other].IsPainted(x, maskY)) { blocked = true; break; }
                if (!blocked) result[winner].PaintPixel(x, maskY, true);
            }
            changed = Enumerable.Range(0, 3).Any(channel => beforeHashes[channel] != result[channel].ContentHash);
            return result;
        }

        private static void ResolveRegionProposals(MaskProjectionResult result, List<RegionProposal> proposals,
            bool[] arbitrationDomain, bool[][] channelDomains, bool[] ambiguousPixels, bool[][] ambiguousByChannel)
        {
            // Evidence orders proposals deterministically. Channel and component indexes only
            // break genuinely equal, already-separated evidence; a gap below the arbitration
            // threshold (including an exact boundary tie) remains unresolved instead of becoming
            // a hidden channel priority.
            foreach (IGrouping<int, RegionProposal> group in proposals.GroupBy(proposal => proposal.Target.Index))
            {
                List<RegionProposal> contenders = group.OrderByDescending(proposal => proposal.Evidence)
                    .ThenByDescending(proposal => proposal.Score).ThenBy(proposal => proposal.Source.Channel)
                    .ThenBy(proposal => proposal.Source.Index).ToList();
                foreach (RegionProposal proposal in contenders)
                    foreach (int pixel in proposal.Target.Pixels)
                    {
                        arbitrationDomain[pixel] = true;
                        channelDomains[proposal.Source.Channel][pixel] = true;
                    }
                RegionProposal winner = contenders[0];
                RegionProposal second = contenders.FirstOrDefault(proposal => proposal.Source.Channel != winner.Source.Channel);
                if (second != null && winner.Evidence - second.Evidence <= MinimumArbitrationSeparation)
                {
                    foreach (RegionProposal contender in contenders.Where(proposal =>
                        winner.Evidence - proposal.Evidence <= MinimumArbitrationSeparation))
                        foreach (int pixel in contender.Target.Pixels)
                        {
                            ambiguousPixels[pixel] = true;
                            ambiguousByChannel[contender.Source.Channel][pixel] = true;
                        }
                    continue;
                }
                foreach (int pixel in winner.Target.Pixels) PaintTargetPixel(result.CandidateLayers[winner.Source.Channel], pixel, true);
            }
        }

        private static void ArbitrateCandidateOverlaps(MaskProjectionResult result,
            IReadOnlyList<VisualMaskLayerRecord> beforeArbitration, int[] targetRegionByPixel,
            bool[] arbitrationDomain, bool[][] channelDomains, bool[] ambiguousPixels, bool[][] ambiguousByChannel)
        {
            for (int maskY = 0; maskY < Resolution; maskY++) for (int x = 0; x < Resolution; x++)
            {
                int topPixel = (Resolution - 1 - maskY) * Resolution + x;
                List<int> contenders = new List<int>();
                for (int channel = 0; channel < 3; channel++)
                    if (beforeArbitration[channel].IsPainted(x, maskY)) contenders.Add(channel);
                if (contenders.Count == 0) continue;
                if (ambiguousPixels[topPixel])
                {
                    foreach (int channel in contenders)
                    {
                        channelDomains[channel][topPixel] = true;
                        ambiguousByChannel[channel][topPixel] = true;
                        result.CandidateLayers[channel].PaintPixel(x, maskY, false);
                    }
                    continue;
                }
                int winner = WinnerForPixel(result, contenders, x, maskY);
                if (winner < 0)
                {
                    ambiguousPixels[topPixel] = true;
                    arbitrationDomain[topPixel] = true;
                    foreach (int channel in contenders)
                    {
                        channelDomains[channel][topPixel] = true;
                        ambiguousByChannel[channel][topPixel] = true;
                        result.CandidateLayers[channel].PaintPixel(x, maskY, false);
                    }
                    continue;
                }
                foreach (int channel in contenders)
                    if (channel != winner) result.CandidateLayers[channel].PaintPixel(x, maskY, false);
            }
        }

        private static int WinnerForPixel(MaskProjectionResult result, List<int> contenders, int x, int maskY)
        {
            if (contenders == null || contenders.Count == 0) return -1;
            int topPixel = (Resolution - 1 - maskY) * Resolution + x;
            List<int> ordered = contenders.OrderByDescending(channel => result.EvidenceByChannel == null
                    || result.EvidenceByChannel[channel] == null ? 0f : result.EvidenceByChannel[channel][topPixel])
                .ThenBy(channel => channel).ToList();
            // The ordinal channel order is only a stable final tie-break after the evidence
            // separation test below. It never resolves an insufficiently separated conflict.
            if (ordered.Count > 1)
            {
                float first = EvidenceAt(result, ordered[0], topPixel);
                float second = EvidenceAt(result, ordered[1], topPixel);
                if (first - second <= MinimumArbitrationSeparation) return -1;
            }
            return ordered[0];
        }

        private static float EvidenceAt(MaskProjectionResult result, int channel, int topPixel)
        {
            return result.EvidenceByChannel != null && channel < result.EvidenceByChannel.Length
                && result.EvidenceByChannel[channel] != null
                ? result.EvidenceByChannel[channel][topPixel] : 0f;
        }

        private static float[][] BuildPixelEvidence(IReadOnlyList<VisualMaskLayerRecord> candidates,
            IReadOnlyList<VisualMaskLayerRecord> initial, List<Component> source, List<Component> targets,
            int[] targetRegionByPixel, Color32[] targetPixels)
        {
            float[][] result = new float[3][];
            for (int channel = 0; channel < 3; channel++) result[channel] = new float[Resolution * Resolution];
            for (int topPixel = 0; topPixel < targetRegionByPixel.Length; topPixel++)
            {
                int targetIndex = targetRegionByPixel[topPixel];
                if (targetIndex < 0) continue;
                Component target = targets.FirstOrDefault(component => component.Index == targetIndex);
                if (target == null) continue;
                int x = topPixel % Resolution;
                int maskY = Resolution - 1 - topPixel / Resolution;
                for (int channel = 0; channel < 3; channel++)
                {
                    if (!candidates[channel].IsPainted(x, maskY)) continue;
                    float semantic = source.Where(component => component.Channel == channel)
                        .Select(component => Score(component, target)).DefaultIfEmpty(0f).Max();
                    float initialMembership = initial[channel].IsPainted(x, maskY) ? 1f : 0f;
                    float distanceAgreement = DistanceAgreement(initial[channel], x, maskY);
                    float neighborSupport = NeighborSupport(candidates[channel], x, maskY);
                    result[channel][topPixel] = Mathf.Clamp01(semantic * 0.55f + initialMembership * 0.15f
                        + distanceAgreement * 0.10f + neighborSupport * 0.20f);
                }
            }
            return result;
        }

        private static float NeighborSupport(VisualMaskLayerRecord layer, int x, int maskY)
        {
            int present = 0;
            int total = 0;
            for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dy == 0) continue;
                int nx = x + dx; int ny = maskY + dy;
                if (nx < 0 || nx >= Resolution || ny < 0 || ny >= Resolution) continue;
                total++;
                if (layer.IsPainted(nx, ny)) present++;
            }
            return SafeRatio(present, total);
        }

        private static float DistanceAgreement(VisualMaskLayerRecord layer, int x, int maskY)
        {
            if (layer.IsPainted(x, maskY)) return 1f;
            int nearest = 5;
            for (int dy = -4; dy <= 4; dy++) for (int dx = -4; dx <= 4; dx++)
            {
                int distance = Mathf.Abs(dx) + Mathf.Abs(dy);
                if (distance == 0 || distance >= nearest) continue;
                int nx = x + dx; int ny = maskY + dy;
                if (nx >= 0 && nx < Resolution && ny >= 0 && ny < Resolution && layer.IsPainted(nx, ny)) nearest = distance;
            }
            return nearest >= 5 ? 0f : 1f - nearest / 5f;
        }

        private static void CountAmbiguity(MaskProjectionChannelResult result, bool[] ambiguousPixels)
        {
            int count = CountPixels(ambiguousPixels);
            result.Conflicts = count;
            result.AmbiguousAssignments = count;
        }

        private static float BoundedConfidence(float expectedCoverage, float assignedPrecision,
            float spatialAgreement, float semanticAgreement, float conflictFree, float ambiguityFree,
            int expectedPixels, int assignedPixels, bool hasSourcePixels)
        {
            // Confidence is channel-local: expected coverage (recall) 25%, assigned precision
            // 25%, spatial agreement 15%, semantic agreement 20%, conflict-free 7.5%, and
            // ambiguity-free 7.5%. Expected and assigned pixels are the transformed source
            // and final channel sets; no global unmasked-pixel count participates. A channel
            // that is absent, has no expected pixels, or receives no assignments is exactly 0.
            if (!hasSourcePixels || expectedPixels <= 0 || assignedPixels <= 0) return 0f;
            float value = ClampFinite01(expectedCoverage) * 0.25f + ClampFinite01(assignedPrecision) * 0.25f
                + ClampFinite01(spatialAgreement) * 0.15f + ClampFinite01(semanticAgreement) * 0.20f
                + ClampFinite01(conflictFree) * 0.075f + ClampFinite01(ambiguityFree) * 0.075f;
            if (float.IsNaN(value) || float.IsInfinity(value)) return 0f;
            return Mathf.Clamp01(value);
        }

        private static bool[][] EmptyChannelPixelSets()
        {
            return new[]
            {
                new bool[Resolution * Resolution],
                new bool[Resolution * Resolution],
                new bool[Resolution * Resolution]
            };
        }

        private static void MarkLayerPixels(VisualMaskLayerRecord layer, bool[] destination, bool[] domain,
            Color32[] targetPixels)
        {
            if (layer == null || destination == null || domain == null || targetPixels == null) return;
            for (int maskY = 0; maskY < Resolution; maskY++) for (int x = 0; x < Resolution; x++)
            {
                int topPixel = (Resolution - 1 - maskY) * Resolution + x;
                if (layer.IsPainted(x, maskY) && targetPixels[topPixel].a >= VisibleAlpha)
                {
                    destination[topPixel] = true;
                    domain[topPixel] = true;
                }
            }
        }

        private static int CountPixels(bool[] pixels)
        {
            return pixels?.Count(value => value) ?? 0;
        }

        private static int CountPixels(bool[] domain, bool[] pixels)
        {
            if (domain == null || pixels == null) return 0;
            int count = 0;
            for (int index = 0; index < Mathf.Min(domain.Length, pixels.Length); index++)
                if (domain[index] && pixels[index]) count++;
            return count;
        }

        private static int IntersectionCount(VisualMaskLayerRecord first, VisualMaskLayerRecord second)
        {
            int count = 0;
            if (first == null || second == null) return count;
            for (int y = 0; y < Resolution; y++) for (int x = 0; x < Resolution; x++)
                if (first.IsPainted(x, y) && second.IsPainted(x, y)) count++;
            return count;
        }

        private static float AssignmentAgreement(VisualMaskLayerRecord expected, VisualMaskLayerRecord assigned)
        {
            int expectedPixels = CountPainted(expected);
            int assignedPixels = CountPainted(assigned);
            if (expectedPixels <= 0 || assignedPixels <= 0) return 0f;
            int intersection = IntersectionCount(expected, assigned);
            float recall = SafeRatio(intersection, expectedPixels);
            float precision = SafeRatio(intersection, assignedPixels);
            return Mathf.Clamp01((recall + precision) * 0.5f);
        }

        private static float SemanticAgreement(VisualMaskLayerRecord layer, List<Component> source, int channel,
            List<Component> targets, int[] targetRegionByPixel)
        {
            float sum = 0f; int count = 0;
            for (int maskY = 0; maskY < Resolution; maskY++) for (int x = 0; x < Resolution; x++)
            {
                if (!layer.IsPainted(x, maskY)) continue;
                int targetIndex = targetRegionByPixel[(Resolution - 1 - maskY) * Resolution + x];
                Component target = targets.FirstOrDefault(component => component.Index == targetIndex);
                if (target == null) continue;
                sum += source.Where(component => component.Channel == channel)
                    .Select(component => Score(component, target)).DefaultIfEmpty(0f).Max();
                count++;
            }
            return count == 0 ? 0f : Mathf.Clamp01(sum / count);
        }

        private static float LayerIoU(VisualMaskLayerRecord first, VisualMaskLayerRecord second)
        {
            int intersection = 0; int union = 0;
            for (int y = 0; y < Resolution; y++) for (int x = 0; x < Resolution; x++)
            {
                bool a = first.IsPainted(x, y); bool b = second.IsPainted(x, y);
                if (a && b) intersection++;
                if (a || b) union++;
            }
            return SafeRatio(intersection, union);
        }

        private static int CountPainted(VisualMaskLayerRecord layer)
        {
            int count = 0;
            for (int y = 0; y < Resolution; y++) for (int x = 0; x < Resolution; x++)
                if (layer.IsPainted(x, y)) count++;
            return count;
        }

        private static float SafeRatio(int numerator, int denominator)
        {
            if (denominator <= 0) return 0f;
            return Mathf.Clamp01(numerator / (float)denominator);
        }

        private static float ClampFinite01(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? 0f : Mathf.Clamp01(value);
        }

        private static int[] TargetRegionByPixel(List<Component> targets)
        {
            int[] result = Enumerable.Repeat(-1, Resolution * Resolution).ToArray();
            foreach (Component target in targets) foreach (int pixel in target.Pixels) result[pixel] = target.Index;
            return result;
        }

        private static List<VisualMaskLayerRecord> InitialCandidates(IReadOnlyList<VisualMaskLayerRecord> sourceLayers,
            Color32[] sourcePixels, Color32[] targetPixels, Bounds sourceBounds, Bounds targetBounds)
        {
            List<VisualMaskLayerRecord> candidates = EmptyLayers();
            for (int channel = 0; channel < 3; channel++)
            {
                VisualMaskLayerRecord sourceLayer = sourceLayers != null && channel < sourceLayers.Count
                    ? sourceLayers[channel] : null;
                if (sourceLayer == null) continue;
                if (!sourceBounds.HasPixels || !targetBounds.HasPixels) continue;
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
            x = Mathf.Clamp(x, bounds.MinX, bounds.MaxX); y = Mathf.Clamp(y, bounds.MinY, bounds.MaxY);
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

        private static VisibleDomain TargetVisibleDomain(Color32[] pixels)
        {
            return DomainFromPixels(pixels?.Select(pixel => pixel.a >= VisibleAlpha).ToArray(), false);
        }

        private static VisibleDomain SourceVisibleDomain(List<Component> components)
        {
            bool[] occupied = new bool[Resolution * Resolution];
            if (components != null)
                foreach (Component component in components)
                    foreach (int pixel in component.Pixels)
                        occupied[pixel] = true;
            return DomainFromPixels(occupied, true);
        }

        private static VisibleDomain DomainFromPixels(bool[] pixels, bool maskCoordinates)
        {
            Bounds bounds = new Bounds { MinX = Resolution, MinY = Resolution, MaxX = -1, MaxY = -1 };
            int count = 0;
            if (pixels == null) return new VisibleDomain { Bounds = bounds };
            for (int index = 0; index < pixels.Length; index++) if (pixels[index])
            {
                int x = index % Resolution;
                int y = maskCoordinates ? Resolution - 1 - index / Resolution : index / Resolution;
                bounds.MinX = Mathf.Min(bounds.MinX, x); bounds.MinY = Mathf.Min(bounds.MinY, y);
                bounds.MaxX = Mathf.Max(bounds.MaxX, x); bounds.MaxY = Mathf.Max(bounds.MaxY, y);
                count++;
            }
            bounds.HasPixels = bounds.MaxX >= bounds.MinX && bounds.MaxY >= bounds.MinY;
            return new VisibleDomain { Bounds = bounds, PixelCount = count };
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
                Queue<int> queue = new Queue<int>(); List<int> region = new List<int>();
                queue.Enqueue(start); visited[start] = true;
                while (queue.Count > 0)
                {
                    int index = queue.Dequeue(); region.Add(index);
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

        private static void NormalizeFeatures(List<Component> components, Color32[] pixels, bool maskCoordinates,
            VisibleDomain domain)
        {
            if (components.Count == 0 || !domain.HasPixels) return;
            // All source channels share one normalized source frame. This is the same
            // frame used by InitialCandidates and prevents channel-specific affine drift.
            int minX = domain.Bounds.MinX; int minY = domain.Bounds.MinY;
            int maxX = domain.Bounds.MaxX; int maxY = domain.Bounds.MaxY;
            int visible = domain.PixelCount;
            bool[] occupied = new bool[Resolution * Resolution];
            foreach (Component component in components) foreach (int pixelIndex in component.Pixels) occupied[pixelIndex] = true;
            float width = Mathf.Max(1, maxX - minX + 1); float height = Mathf.Max(1, maxY - minY + 1);
            foreach (Component component in components)
            {
                int componentMinX = Resolution; int componentMinY = Resolution; int componentMaxX = 0; int componentMaxY = 0;
                float centerX = 0f; float centerY = 0f; float red = 0f; float green = 0f; float blue = 0f;
                foreach (int componentPixel in component.Pixels)
                {
                    int x = componentPixel % Resolution; int y = maskCoordinates ? Resolution - 1 - componentPixel / Resolution : componentPixel / Resolution;
                    componentMinX = Mathf.Min(componentMinX, x); componentMinY = Mathf.Min(componentMinY, y);
                    componentMaxX = Mathf.Max(componentMaxX, x); componentMaxY = Mathf.Max(componentMaxY, y);
                    centerX += x; centerY += y;
                    Color color = ToColor(pixels[maskCoordinates ? (Resolution - 1 - y) * Resolution + x : componentPixel]);
                    red += color.r; green += color.g; blue += color.b;
                }
                float count = Mathf.Max(1, component.Pixels.Count);
                component.CenterX = centerX / count; component.CenterY = centerY / count;
                component.RelativeX = Mathf.Clamp01((component.CenterX - minX) / width);
                component.RelativeY = Mathf.Clamp01((component.CenterY - minY) / height);
                component.RelativeWidth = (componentMaxX - componentMinX + 1) / width;
                component.RelativeHeight = (componentMaxY - componentMinY + 1) / height;
                // Area is measured against the one combined visible domain, never against
                // a channel-local pixel count. This keeps source and target area comparable
                // when a semantic layer is split into several regions.
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
            return Mathf.Clamp01(position * 0.30f + color * 0.20f + area * 0.15f + shape * 0.15f
                + adjacency * 0.10f + connectivity * 0.10f);
        }

        private static float ColorAgreement(Component source, Component target)
        {
            return source == null || target == null ? 0f
                : 1f - Mathf.Clamp01(ColorDistance(source.Color, target.Color) / 0.75f);
        }

        private static int RemainingVisible(Color32[] pixels, List<VisualMaskLayerRecord> layers,
            bool[] domain = null)
        {
            int count = 0;
            for (int y = 0; y < Resolution; y++) for (int x = 0; x < Resolution; x++)
            {
                int topPixel = y * Resolution + x;
                if (pixels[topPixel].a < VisibleAlpha || domain != null && !domain[topPixel]) continue;
                bool assigned = layers.Any(layer => layer.IsPainted(x, Resolution - 1 - y));
                if (!assigned) count++;
            }
            return count;
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
