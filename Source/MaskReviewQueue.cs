using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace HorticultureNovelSeeds
{
    public sealed class MaskReviewQueueRow
    {
        public string IdentityKey;
        public ThingDef PlantDef;
        public int Variation;
        public List<string> Uses = new List<string>();
        public List<VisualMaskLayerRecord> Layers;
        public float Confidence;
        public int IssueCount;
        public int TransparentCount;
        public int OverlapCount;
        public int TinyCount;
        public int UnmaskedCount;
        public bool Missing;
        public bool Ambiguous;
        public string Origin;
        public string ValidationKey;

        public bool Failed => Missing || Ambiguous || IssueCount > 0;
        public string UsageSummary => Uses.Count <= 1 ? Uses.FirstOrDefault() ?? PlantDef?.LabelCap.ToString()
            : Uses.FirstOrDefault() + " + " + (Uses.Count - 1) + " more";
    }

    internal static class MaskReviewQueueBuilder
    {
        private sealed class Usage
        {
            public ThingDef plant;
            public int variation;
            public string label;
            public List<VisualMaskLayerRecord> manual;
        }

        private static readonly Dictionary<string, MaskValidationResult> ValidationCache = new Dictionary<string, MaskValidationResult>();

        public static List<MaskReviewQueueRow> Build()
        {
            Dictionary<string, List<Usage>> grouped = new Dictionary<string, List<Usage>>();
            foreach (ThingDef plant in DefDatabase<ThingDef>.AllDefsListForReading.Where(def => def?.plant != null).OrderBy(def => def.defName))
            {
                int variationCount = PlantMaskUtility.VariationCount(plant);
                for (int variation = 0; variation < variationCount; variation++)
                {
                    if (!MaskTextureIdentity.TryGet(plant, variation, out string identity)) continue;
                    PlantSettingsRecord settings = HorticultureNovelSeedsMod.Settings?.GetPlantSettings(plant, false);
                    List<VisualMaskLayerRecord> manual = settings?.HasManualPlantMask(variation) == true
                        ? settings.ManualPlantMaskLayersForVariation(variation).Select(layer => layer.Clone()).ToList() : null;
                    if (!grouped.TryGetValue(identity, out List<Usage> usages)) grouped[identity] = usages = new List<Usage>();
                    usages.Add(new Usage
                    {
                        plant = plant,
                        variation = variation,
                        label = plant.LabelCap + " / " + PlantMaskUtility.VariationLabel(plant, variation),
                        manual = manual
                    });
                }
            }

            List<MaskReviewQueueRow> rows = new List<MaskReviewQueueRow>();
            foreach (KeyValuePair<string, List<Usage>> group in grouped.OrderBy(item => item.Key, StringComparer.Ordinal))
            {
                List<VisualMaskLayerRecord> firstManual = group.Value.Select(usage => usage.manual).FirstOrDefault(layers => layers != null);
                bool conflicting = group.Value.Where(usage => usage.manual != null).Select(usage => usage.manual)
                    .Any(layers => !LayersEqual(firstManual, layers));
                Usage representative = group.Value.FirstOrDefault(usage => usage.manual != null) ?? group.Value[0];
                MaskReviewQueueRow row = new MaskReviewQueueRow
                {
                    IdentityKey = group.Key,
                    PlantDef = representative.plant,
                    Variation = representative.variation,
                    Layers = firstManual,
                    Ambiguous = conflicting,
                    Origin = conflicting ? "Ambiguous manual masks" : firstManual != null ? "Def-specific manual" : "Missing"
                };
                row.Uses.AddRange(group.Value.Select(usage => usage.label));
                if (row.Layers == null && !row.Ambiguous)
                {
                    SharedManualMaskResolution shared = SharedManualMaskCache.Resolve(representative.plant, representative.variation, true);
                    if (shared.Ambiguous)
                    {
                        row.Ambiguous = true;
                        row.Origin = "Ambiguous shared manual";
                    }
                    else if (shared.Found)
                    {
                        row.Layers = shared.Layers;
                        row.Origin = "Shared manual";
                    }
                }
                    AutoPlantMaskRecord auto = PlantAutoMaskCache.GetRecord(representative.plant, representative.variation, false, true);
                if (row.Layers == null && !row.Ambiguous && auto != null)
                {
                    row.Layers = auto.Layers.Select(layer => layer.Clone()).ToList();
                    row.Confidence = auto.Confidence;
                    row.Origin = auto.LowConfidence ? "Auto-generated, low confidence" : "Auto-generated";
                }
                row.Missing = row.Layers == null;
                if (row.Layers != null && !row.Ambiguous)
                {
                    row.ValidationKey = group.Key + "|mask:" + LayersHash(row.Layers);
                    MaskValidationResult validation = ValidationFor(row, representative.plant, representative.variation);
                    row.IssueCount = validation?.allIssuePixels ?? 0;
                    row.TransparentCount = validation?.transparentPixels ?? 0;
                    row.OverlapCount = validation?.overlappingPixels ?? 0;
                    row.TinyCount = validation?.tinyFragments ?? 0;
                    row.UnmaskedCount = validation?.unmaskedVisiblePixels ?? 0;
                    if (row.Origin.Contains("manual")) row.Confidence = 1f;
                }
                rows.Add(row);
            }
            return rows.OrderBy(row => row.Failed ? 0 : 1).ThenBy(row => row.Confidence)
                .ThenByDescending(row => row.IssueCount).ThenBy(row => row.IdentityKey, StringComparer.Ordinal).ToList();
        }

        public static void ClearValidationCache()
        {
            ValidationCache.Clear();
        }

        private static MaskValidationResult ValidationFor(MaskReviewQueueRow row, ThingDef plant, int variation)
        {
            if (row.ValidationKey.NullOrEmpty()) return null;
            if (ValidationCache.TryGetValue(row.ValidationKey, out MaskValidationResult cached)) return cached;
            Color32[] pixels = MaskTextureIdentity.ReadPixels(PlantMaskUtility.TextureForVariation(plant, variation),
                VisualMaskLayerRecord.Resolution, VisualMaskLayerRecord.Resolution);
            if (pixels == null) return null;
            MaskValidationResult validation = MaskPainterOperations.Validate(row.Layers, pixels, 12);
            ValidationCache[row.ValidationKey] = validation;
            return validation;
        }

        private static int LayersHash(IReadOnlyList<VisualMaskLayerRecord> layers)
        {
            unchecked
            {
                int hash = 17;
                foreach (VisualMaskLayerRecord layer in layers ?? Enumerable.Empty<VisualMaskLayerRecord>()) hash = hash * 31 + (layer?.ContentHash ?? 0);
                return hash;
            }
        }

        private static bool LayersEqual(IReadOnlyList<VisualMaskLayerRecord> first, IReadOnlyList<VisualMaskLayerRecord> second)
        {
            if (first == null || second == null || first.Count != second.Count) return false;
            for (int index = 0; index < first.Count; index++)
                if (first[index]?.ContentHash != second[index]?.ContentHash) return false;
            return true;
        }
    }

    public sealed class Dialog_MaskReviewQueue : Window, IHorticultureCollectionDialogSurface
    {
        private List<MaskReviewQueueRow> rows;
        private string search = string.Empty;
        private HorticultureCollectionDialogDocument canvasDocument;

        public override Vector2 InitialSize => new Vector2(980f, 720f);

        public Dialog_MaskReviewQueue()
        {
            doCloseX = true;
            closeOnClickedOutside = false;
            absorbInputAroundWindow = true;
            RefreshRows();
            canvasDocument = new HorticultureCollectionDialogDocument(this, "hns.mask.review-queue");
        }

        public override void DoWindowContents(Rect inRect)
        {
            canvasDocument?.Draw(inRect);
        }

        public override void PostClose()
        {
            canvasDocument?.PostClose();
            base.PostClose();
        }

        private void Open(MaskReviewQueueRow row)
        {
            Find.WindowStack.Add(new Dialog_PlantMasks(row.PlantDef, false, row.Variation, RefreshRows));
        }

        private void RefreshRows()
        {
            MaskReviewQueueBuilder.ClearValidationCache();
            rows = MaskReviewQueueBuilder.Build();
        }

        string IHorticultureCollectionDialogSurface.Title => "Mask Review Queue";
        string IHorticultureCollectionDialogSurface.Description =>
            "Exact shared textures are grouped. Failed or missing masks appear first. Open a row to edit its semantic channels.";
        string IHorticultureCollectionDialogSurface.Search { get => search; set => search = value ?? string.Empty; }
        IReadOnlyList<HorticultureDialogRow> IHorticultureCollectionDialogSurface.Rows =>
            (rows ?? new List<MaskReviewQueueRow>()).Select((row, index) => new HorticultureDialogRow
            {
                Id = row.IdentityKey ?? "mask-" + index,
                Label = row.UsageSummary ?? "Unknown mask group",
                Detail = "Confidence " + row.Confidence.ToStringPercent("F0") + "  Issues " + row.IssueCount
                    + "  Transparent " + row.TransparentCount + "  Overlap " + row.OverlapCount
                    + "  Fragments " + row.TinyCount + "  Unmasked " + row.UnmaskedCount
                    + "  Variation " + row.Variation,
                Status = row.Missing ? "Missing" : row.Ambiguous ? "Ambiguous" : row.Origin,
                Warning = row.Failed,
                Activate = () => Open(row)
            }).ToArray();
        string IHorticultureCollectionDialogSurface.EmptyText => "No mask review items match this search.";
        string IHorticultureCollectionDialogSurface.EntryLabel => string.Empty;
        string IHorticultureCollectionDialogSurface.Entry { get => string.Empty; set { } }
        Action IHorticultureCollectionDialogSurface.EntryAction => null;
        string IHorticultureCollectionDialogSurface.PrimaryLabel => "Refresh";
        Action IHorticultureCollectionDialogSurface.PrimaryAction => RefreshRows;
        string IHorticultureCollectionDialogSurface.SecondaryLabel => string.Empty;
        Action IHorticultureCollectionDialogSurface.SecondaryAction => null;
        void IHorticultureCollectionDialogSurface.Close() => Close();

    }
}
