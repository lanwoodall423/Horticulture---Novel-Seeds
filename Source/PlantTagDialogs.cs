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
        private Vector2 scroll;
        private string search = string.Empty;
        private string newTag = string.Empty;

        public override Vector2 InitialSize => new Vector2(700f, 700f);

        public Dialog_PlantTagEditor(NovelSeedsSettings settings, ThingDef plant)
        {
            this.settings = settings;
            this.plant = plant;
            doCloseX = true;
            absorbInputAroundWindow = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width - 130f, 32f), plant.LabelCap + " Tags");
            Text.Font = GameFont.Small;
            if (Widgets.ButtonText(new Rect(inRect.xMax - 120f, inRect.y, 120f, 30f), "Scan Plant"))
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("Clear manual tag changes for " + plant.LabelCap + " and scan its definitions and harvested product again?", delegate
                {
                    settings.ResetPlantTags(plant);
                    Messages.Message(plant.LabelCap + " tags scanned.", MessageTypeDefOf.TaskCompletion, false);
                }, true));

            Widgets.Label(new Rect(inRect.x, inRect.y + 38f, inRect.width, 42f), "Tags control which produce-related traits this plant can receive. Automatic tags come from the plant and its harvested product.");
            search = Widgets.TextField(new Rect(inRect.x, inRect.y + 84f, inRect.width, 30f), search ?? string.Empty);

            List<string> tags = PlantTagUtility.ConfigurableTags()
                .Where(tag => search.NullOrEmpty() || tag.IndexOf(search.Trim(), StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();
            Rect outer = new Rect(inRect.x, inRect.y + 124f, inRect.width, inRect.height - 214f);
            Rect view = new Rect(0f, 0f, outer.width - 18f, Mathf.Max(outer.height, tags.Count * 38f));
            Widgets.BeginScrollView(outer, ref scroll, view);
            float y = 0f;
            foreach (string tag in tags)
            {
                Rect row = new Rect(0f, y, view.width, 36f);
                Widgets.DrawHighlightIfMouseover(row);
                bool enabled = PlantTagUtility.HasTag(plant, tag);
                bool before = enabled;
                Widgets.CheckboxLabeled(new Rect(8f, y + 4f, view.width - 150f, 28f), tag, ref enabled);
                if (enabled != before) settings.SetPlantTag(plant, tag, enabled);
                DrawStatus(new Rect(view.width - 134f, y + 7f, 126f, 24f), tag);
                y += 38f;
            }
            Widgets.EndScrollView();

            float addY = inRect.yMax - 78f;
            newTag = Widgets.TextField(new Rect(inRect.x, addY, inRect.width - 100f, 30f), newTag ?? string.Empty, 50);
            if (Widgets.ButtonText(new Rect(inRect.xMax - 90f, addY, 90f, 30f), "Add Tag")) AddTag();
            if (Widgets.ButtonText(new Rect(inRect.xMax - 110f, inRect.yMax - 34f, 110f, 30f), "Done")) Close();
        }

        private void DrawStatus(Rect rect, string tag)
        {
            PlantTagOverrideRecord record = settings.GetPlantTagOverrides(plant, false);
            string status = record?.AddedTags.Any(item => item.Equals(tag, StringComparison.OrdinalIgnoreCase)) == true ? "Manually Added"
                : record?.RemovedTags.Any(item => item.Equals(tag, StringComparison.OrdinalIgnoreCase)) == true ? "Manually Removed"
                : PlantTagUtility.InferredHasTag(plant, tag) ? "Detected" : "Not Detected";
            Color old = GUI.color;
            TextAnchor anchor = Text.Anchor;
            GUI.color = new Color(0.70f, 0.72f, 0.73f);
            Text.Anchor = TextAnchor.MiddleRight;
            Widgets.Label(rect, status);
            Text.Anchor = anchor;
            GUI.color = old;
        }

        private void AddTag()
        {
            string normalized = newTag?.Trim();
            if (normalized.NullOrEmpty() || normalized.IndexOf(':') >= 0) return;
            settings.SetPlantTag(plant, normalized, true);
            newTag = string.Empty;
        }
    }

    public class Dialog_TagPlantMembers : Window
    {
        private readonly NovelSeedsSettings settings;
        private readonly string tag;
        private Vector2 scroll;
        private string search = string.Empty;

        public override Vector2 InitialSize => new Vector2(700f, 700f);

        public Dialog_TagPlantMembers(NovelSeedsSettings settings, string tag)
        {
            this.settings = settings;
            this.tag = tag;
            doCloseX = true;
            absorbInputAroundWindow = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width - 130f, 32f), tag);
            Text.Font = GameFont.Small;
            if (Widgets.ButtonText(new Rect(inRect.xMax - 120f, inRect.y, 120f, 30f), "Scan Tag"))
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("Clear manual '" + tag + "' changes for every plant and scan all plant and product definitions again?", delegate
                {
                    settings.ResetTag(tag);
                    Messages.Message(tag + " membership scanned.", MessageTypeDefOf.TaskCompletion, false);
                }, true));

            int memberCount = GrowablePlants().Count(plant => PlantTagUtility.HasTag(plant, tag));
            Widgets.Label(new Rect(inRect.x, inRect.y + 40f, inRect.width, 24f), memberCount + " matching plants");
            search = Widgets.TextField(new Rect(inRect.x, inRect.y + 74f, inRect.width, 30f), search ?? string.Empty);
            List<ThingDef> plants = GrowablePlants().Where(plant => search.NullOrEmpty()
                || plant.LabelCap.ToString().IndexOf(search.Trim(), StringComparison.OrdinalIgnoreCase) >= 0
                || plant.defName.IndexOf(search.Trim(), StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            Rect outer = new Rect(inRect.x, inRect.y + 114f, inRect.width, inRect.height - 164f);
            Rect view = new Rect(0f, 0f, outer.width - 18f, Mathf.Max(outer.height, plants.Count * 38f));
            Widgets.BeginScrollView(outer, ref scroll, view);
            float y = 0f;
            foreach (ThingDef plant in plants)
            {
                Rect row = new Rect(0f, y, view.width, 36f);
                Widgets.DrawHighlightIfMouseover(row);
                bool enabled = PlantTagUtility.HasTag(plant, tag);
                bool before = enabled;
                Widgets.CheckboxLabeled(new Rect(8f, y + 4f, view.width - 150f, 28f), plant.LabelCap, ref enabled);
                if (enabled != before) settings.SetPlantTag(plant, tag, enabled);
                string source = PlantTagUtility.InferredHasTag(plant, tag) ? "Detected" : "Manual";
                Color old = GUI.color;
                TextAnchor anchor = Text.Anchor;
                GUI.color = new Color(0.70f, 0.72f, 0.73f);
                Text.Anchor = TextAnchor.MiddleRight;
                Widgets.Label(new Rect(view.width - 110f, y + 7f, 102f, 24f), source);
                Text.Anchor = anchor;
                GUI.color = old;
                y += 38f;
            }
            Widgets.EndScrollView();
            if (Widgets.ButtonText(new Rect(inRect.xMax - 110f, inRect.yMax - 34f, 110f, 30f), "Done")) Close();
        }

        private static List<ThingDef> GrowablePlants()
        {
            return DefDatabase<ThingDef>.AllDefsListForReading.Where(NovelSeedUtility.IsGrowableCrop).OrderBy(def => def.label).ToList();
        }
    }
    public class Dialog_TraitExclusiveTags : Window
    {
        private readonly NovelSeedsSettings settings;
        private readonly VarietyTraitDef trait;
        private readonly GlobalTraitSettingsRecord record;
        private Vector2 scroll;
        private string search = string.Empty;

        public override Vector2 InitialSize => new Vector2(660f, 680f);

        public Dialog_TraitExclusiveTags(NovelSeedsSettings settings, VarietyTraitDef trait)
        {
            this.settings = settings;
            this.trait = TraitConfigUtility.Root(trait);
            record = settings.GetGlobalTraitSettings(this.trait);
            doCloseX = true;
            absorbInputAroundWindow = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 32f), trait.LabelCap + " - Valid Plant Tags");
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(inRect.x, inRect.y + 38f, inRect.width, 42f), "This trait is valid when a plant has at least one selected tag. Built-in trait requirements still apply.");
            search = Widgets.TextField(new Rect(inRect.x, inRect.y + 84f, inRect.width, 30f), search ?? string.Empty);

            List<string> tags = PlantTagUtility.ConfigurableTags()
                .Where(tag => search.NullOrEmpty() || tag.IndexOf(search.Trim(), StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();
            Rect outer = new Rect(inRect.x, inRect.y + 124f, inRect.width, inRect.height - 178f);
            Rect view = new Rect(0f, 0f, outer.width - 18f, Mathf.Max(outer.height, tags.Count * 38f));
            Widgets.BeginScrollView(outer, ref scroll, view);
            float y = 0f;
            foreach (string tag in tags)
            {
                Rect row = new Rect(0f, y, view.width, 36f);
                Widgets.DrawHighlightIfMouseover(row);
                bool enabled = record.ExclusiveTags.Any(item => item.Equals(tag, StringComparison.OrdinalIgnoreCase));
                bool before = enabled;
                Widgets.CheckboxLabeled(new Rect(8f, y + 4f, view.width - 126f, 28f), tag, ref enabled);
                if (enabled != before) record.SetExclusiveTag(tag, enabled);
                int count = DefDatabase<ThingDef>.AllDefsListForReading.Count(plant => NovelSeedUtility.IsGrowableCrop(plant) && PlantTagUtility.HasTag(plant, tag));
                Color old = GUI.color;
                TextAnchor anchor = Text.Anchor;
                GUI.color = new Color(0.70f, 0.72f, 0.73f);
                Text.Anchor = TextAnchor.MiddleRight;
                Widgets.Label(new Rect(view.width - 116f, y + 7f, 108f, 24f), count + " plants");
                Text.Anchor = anchor;
                GUI.color = old;
                y += 38f;
            }
            Widgets.EndScrollView();

            if (Widgets.ButtonText(new Rect(inRect.x, inRect.yMax - 34f, 110f, 30f), "Clear All"))
                foreach (string tag in record.ExclusiveTags.ToList()) record.SetExclusiveTag(tag, false);
            if (Widgets.ButtonText(new Rect(inRect.xMax - 110f, inRect.yMax - 34f, 110f, 30f), "Done")) Close();
        }
    }}