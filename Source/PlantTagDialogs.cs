using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using RimWorld;
using Verse;

namespace HorticultureNovelSeeds
{
    public class Dialog_PlantTagEditor : Window
    {
        private readonly NovelSeedsSettings settings;
        private readonly ThingDef plant;
        private string search = string.Empty;
        private string newTag = string.Empty;
        private HorticultureCollectionDialogDocument canvasDocument;
        private HorticultureCollectionDialogSurfaceAdapter canvasSurface;

        public override Vector2 InitialSize => new Vector2(700f, 700f);

        public Dialog_PlantTagEditor(NovelSeedsSettings settings, ThingDef plant)
        {
            this.settings = settings;
            this.plant = plant;
            doCloseX = true;
            absorbInputAroundWindow = true;
            canvasSurface = new HorticultureCollectionDialogSurfaceAdapter
            {
                TitleProvider = () => plant.LabelCap + " Tags",
                DescriptionProvider = () => "Automatic tags come from the plant and its harvested product. Manual changes remain explicit and resettable.",
                SearchProvider = () => search,
                SearchSetter = value => search = value,
                RowsProvider = PlantTagRows,
                EmptyProvider = () => "No configurable tags match this search.",
                EntryLabelProvider = () => "Add custom tag",
                EntryProvider = () => newTag,
                EntrySetter = value => newTag = value,
                EntryActionCallback = AddTag,
                PrimaryLabelProvider = () => "Scan Plant",
                PrimaryActionCallback = ScanPlant,
                CloseAction = () => Close()
            };
            canvasDocument = new HorticultureCollectionDialogDocument(canvasSurface, "hns.plant-tags");
        }

        public override void DoWindowContents(Rect inRect) => canvasDocument?.Draw(inRect);

        public override void PostClose()
        {
            canvasDocument?.PostClose();
            base.PostClose();
        }

        private IReadOnlyList<HorticultureDialogRow> PlantTagRows()
        {
            string query = search?.Trim();
            return PlantTagUtility.ConfigurableTags()
                .Where(tag => string.IsNullOrEmpty(query) || tag.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                .Take(1000)
                .Select((tag, index) => new HorticultureDialogRow
                {
                    Id = tag,
                    Label = tag,
                    Detail = TagStatus(tag),
                    Status = PlantTagUtility.HasTag(plant, tag) ? "Enabled" : "Disabled",
                    Selected = PlantTagUtility.HasTag(plant, tag),
                    CanToggle = true,
                    Toggle = value => settings.SetPlantTag(plant, tag, value)
                }).ToArray();
        }

        private string TagStatus(string tag)
        {
            PlantTagOverrideRecord record = settings.GetPlantTagOverrides(plant, false);
            return record?.AddedTags.Any(item => item.Equals(tag, StringComparison.OrdinalIgnoreCase)) == true ? "Manually added"
                : record?.RemovedTags.Any(item => item.Equals(tag, StringComparison.OrdinalIgnoreCase)) == true ? "Manually removed"
                : PlantTagUtility.InferredHasTag(plant, tag) ? "Detected" : "Not detected";
        }

        private void ScanPlant()
        {
            settings.ResetPlantTags(plant);
            Messages.Message(plant.LabelCap + " tags scanned.", MessageTypeDefOf.TaskCompletion, false);
        }

        private void AddTag()
        {
            string normalized = newTag?.Trim();
            if (string.IsNullOrEmpty(normalized) || normalized.IndexOf(':') >= 0) return;
            settings.SetPlantTag(plant, normalized, true);
            newTag = string.Empty;
        }
    }

    public class Dialog_TagPlantMembers : Window
    {
        private readonly NovelSeedsSettings settings;
        private readonly string tag;
        private string search = string.Empty;
        private HorticultureCollectionDialogDocument canvasDocument;
        private HorticultureCollectionDialogSurfaceAdapter canvasSurface;

        public override Vector2 InitialSize => new Vector2(700f, 700f);

        public Dialog_TagPlantMembers(NovelSeedsSettings settings, string tag)
        {
            this.settings = settings;
            this.tag = tag;
            doCloseX = true;
            absorbInputAroundWindow = true;
            canvasSurface = new HorticultureCollectionDialogSurfaceAdapter
            {
                TitleProvider = () => tag,
                DescriptionProvider = () => "Plant membership is derived from the authoritative tag settings and remains searchable.",
                SearchProvider = () => search,
                SearchSetter = value => search = value,
                RowsProvider = PlantRows,
                EmptyProvider = () => "No growable plants match this search.",
                PrimaryLabelProvider = () => "Scan Tag",
                PrimaryActionCallback = ScanTag,
                CloseAction = () => Close()
            };
            canvasDocument = new HorticultureCollectionDialogDocument(canvasSurface, "hns.tag-plants");
        }

        public override void DoWindowContents(Rect inRect) => canvasDocument?.Draw(inRect);

        public override void PostClose()
        {
            canvasDocument?.PostClose();
            base.PostClose();
        }

        private IReadOnlyList<HorticultureDialogRow> PlantRows()
        {
            string query = search?.Trim();
            return DefDatabase<ThingDef>.AllDefsListForReading
                .Where(NovelSeedUtility.IsGrowableCrop)
                .Where(plant => string.IsNullOrEmpty(query)
                    || plant.LabelCap.ToString().IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
                    || plant.defName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderBy(plant => plant.label)
                .Take(1000)
                .Select((plant, index) => new HorticultureDialogRow
                {
                    Id = plant.defName ?? "plant-" + index,
                    Label = plant.LabelCap.ToString(),
                    Detail = PlantTagUtility.InferredHasTag(plant, tag) ? "Detected" : "Manual override",
                    Status = PlantTagUtility.HasTag(plant, tag) ? "Enabled" : "Disabled",
                    Selected = PlantTagUtility.HasTag(plant, tag),
                    CanToggle = true,
                    Toggle = value => settings.SetPlantTag(plant, tag, value)
                }).ToArray();
        }

        private void ScanTag()
        {
            settings.ResetTag(tag);
            Messages.Message(tag + " membership scanned.", MessageTypeDefOf.TaskCompletion, false);
        }
    }

    public class Dialog_TraitExclusiveTags : Window
    {
        private readonly NovelSeedsSettings settings;
        private readonly VarietyTraitDef trait;
        private readonly GlobalTraitSettingsRecord record;
        private string search = string.Empty;
        private HorticultureCollectionDialogDocument canvasDocument;
        private HorticultureCollectionDialogSurfaceAdapter canvasSurface;

        public override Vector2 InitialSize => new Vector2(660f, 680f);

        public Dialog_TraitExclusiveTags(NovelSeedsSettings settings, VarietyTraitDef trait)
        {
            this.settings = settings;
            this.trait = TraitConfigUtility.Root(trait);
            record = settings.GetGlobalTraitSettings(this.trait);
            doCloseX = true;
            absorbInputAroundWindow = true;
            canvasSurface = new HorticultureCollectionDialogSurfaceAdapter
            {
                TitleProvider = () => TraitColorUI.Label(this.trait) + " - Valid Plant Tags",
                DescriptionProvider = () => "A trait is valid when a plant has at least one selected tag. Built-in trait requirements still apply.",
                SearchProvider = () => search,
                SearchSetter = value => search = value,
                RowsProvider = TagRows,
                EmptyProvider = () => "No plant tags match this search.",
                PrimaryLabelProvider = () => "Clear All",
                PrimaryActionCallback = ClearAll,
                CloseAction = () => Close()
            };
            canvasDocument = new HorticultureCollectionDialogDocument(canvasSurface, "hns.trait-exclusive-tags");
        }

        public override void DoWindowContents(Rect inRect) => canvasDocument?.Draw(inRect);

        public override void PostClose()
        {
            canvasDocument?.PostClose();
            base.PostClose();
        }

        private IReadOnlyList<HorticultureDialogRow> TagRows()
        {
            string query = search?.Trim();
            return PlantTagUtility.ConfigurableTags()
                .Where(tag => string.IsNullOrEmpty(query) || tag.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                .Take(1000)
                .Select((tag, index) => new HorticultureDialogRow
                {
                    Id = tag,
                    Label = tag,
                    Detail = DefDatabase<ThingDef>.AllDefsListForReading.Count(plant => NovelSeedUtility.IsGrowableCrop(plant) && PlantTagUtility.HasTag(plant, tag)) + " plants",
                    Status = record.ExclusiveTags.Any(item => item.Equals(tag, StringComparison.OrdinalIgnoreCase)) ? "Required" : "Optional",
                    Selected = record.ExclusiveTags.Any(item => item.Equals(tag, StringComparison.OrdinalIgnoreCase)),
                    CanToggle = true,
                    Toggle = value => record.SetExclusiveTag(tag, value)
                }).ToArray();
        }

        private void ClearAll()
        {
            foreach (string tag in record.ExclusiveTags.ToList()) record.SetExclusiveTag(tag, false);
        }
    }
}
