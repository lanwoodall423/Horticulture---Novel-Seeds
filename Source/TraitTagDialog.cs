using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace HorticultureNovelSeeds
{
    public class Dialog_TraitTags : Window
    {
        private readonly NovelSeedsSettings settings;
        private readonly List<VarietyTraitDef> traits;
        private VarietyTraitDef selectedTrait;
        private Vector2 tagScroll;
        private string newTag = string.Empty;

        public override Vector2 InitialSize => new Vector2(760f, 620f);

        public Dialog_TraitTags(NovelSeedsSettings settings, VarietyTraitDef root)
        {
            this.settings = settings;
            traits = new List<VarietyTraitDef> { root };
            if (root != null && root.configRoot && !root.configFamily.NullOrEmpty()) traits.AddRange(TraitConfigUtility.Types(root.configFamily));
            selectedTrait = root;
            doCloseX = true;
            absorbInputAroundWindow = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 32f), "Trait Tags");
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(inRect.x, inRect.y + 36f, inRect.width, 24f), "Tags are global classifications available to gameplay systems and saved configuration profiles.");

            float top = inRect.y + 74f;
            float leftWidth = traits.Count > 1 ? 244f : 0f;
            if (traits.Count > 1) DrawTraitList(new Rect(inRect.x, top, leftWidth, inRect.height - 124f));
            Rect editor = new Rect(inRect.x + (leftWidth > 0f ? leftWidth + 18f : 0f), top, inRect.width - (leftWidth > 0f ? leftWidth + 18f : 0f), inRect.height - 124f);
            DrawTagEditor(editor);
            if (Widgets.ButtonText(new Rect(inRect.xMax - 110f, inRect.yMax - 34f, 110f, 30f), "Done")) Close();
        }

        private void DrawTraitList(Rect rect)
        {
            Widgets.DrawMenuSection(rect);
            float y = rect.y + 6f;
            foreach (VarietyTraitDef trait in traits)
            {
                Rect row = new Rect(rect.x + 6f, y, rect.width - 12f, 38f);
                if (trait == selectedTrait) Widgets.DrawHighlightSelected(row); else Widgets.DrawHighlightIfMouseover(row);
                Widgets.Label(new Rect(row.x + 8f, row.y + 9f, row.width - 16f, 24f), trait.LabelCap);
                if (Widgets.ButtonInvisible(row))
                {
                    selectedTrait = trait;
                    newTag = string.Empty;
                    tagScroll = Vector2.zero;
                }
                y += 40f;
            }
        }

        private void DrawTagEditor(Rect rect)
        {
            if (selectedTrait == null) return;
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(rect.x, rect.y, rect.width, 30f), selectedTrait.LabelCap);
            Text.Font = GameFont.Small;
            string source = settings.TraitTagsCustomized(selectedTrait) ? "Customized" : "Using XML defaults";
            Color oldColor = GUI.color;
            GUI.color = new Color(0.70f, 0.72f, 0.73f);
            Widgets.Label(new Rect(rect.x, rect.y + 30f, rect.width, 22f), source);
            GUI.color = oldColor;

            List<string> tags = settings.TraitTags(selectedTrait).ToList();
            float y = rect.y + 66f;
            Widgets.Label(new Rect(rect.x, y, rect.width, 24f), "Classification");
            y += 30f;
            bool positive = Contains(tags, "Positive");
            bool wasPositive = positive;
            Widgets.CheckboxLabeled(new Rect(rect.x, y, (rect.width - 12f) / 2f, 28f), "Positive", ref positive);
            if (positive != wasPositive) SetBuiltInTag("Positive", positive);
            bool negative = Contains(settings.TraitTags(selectedTrait), "Negative");
            bool wasNegative = negative;
            Widgets.CheckboxLabeled(new Rect(rect.x + (rect.width + 12f) / 2f, y, (rect.width - 12f) / 2f, 28f), "Negative", ref negative);
            if (negative != wasNegative) SetBuiltInTag("Negative", negative);
            y += 44f;

            Widgets.Label(new Rect(rect.x, y, rect.width, 24f), "Custom Tags");
            y += 30f;
            newTag = Widgets.TextField(new Rect(rect.x, y, rect.width - 92f, 30f), newTag ?? string.Empty, 40);
            if (Widgets.ButtonText(new Rect(rect.xMax - 82f, y, 82f, 30f), "Add")) AddCustomTag();
            y += 42f;

            List<string> customTags = settings.TraitTags(selectedTrait)
                .Where(tag => !tag.Equals("Positive", System.StringComparison.OrdinalIgnoreCase) && !tag.Equals("Negative", System.StringComparison.OrdinalIgnoreCase))
                .OrderBy(tag => tag)
                .ToList();
            Rect outer = new Rect(rect.x, y, rect.width, Mathf.Max(80f, rect.yMax - y - 54f));
            Rect view = new Rect(0f, 0f, outer.width - 18f, Mathf.Max(outer.height, customTags.Count * 36f));
            Widgets.BeginScrollView(outer, ref tagScroll, view);
            float rowY = 0f;
            if (customTags.Count == 0)
            {
                oldColor = GUI.color;
                GUI.color = new Color(0.70f, 0.72f, 0.73f);
                Widgets.Label(new Rect(6f, 8f, view.width - 12f, 24f), "No custom tags");
                GUI.color = oldColor;
            }
            foreach (string tag in customTags)
            {
                Rect row = new Rect(0f, rowY, view.width, 34f);
                Widgets.DrawHighlightIfMouseover(row);
                Widgets.Label(new Rect(8f, rowY + 7f, view.width - 94f, 24f), tag);
                if (Widgets.ButtonText(new Rect(view.width - 78f, rowY + 3f, 70f, 28f), "Remove")) RemoveTag(tag);
                rowY += 36f;
            }
            Widgets.EndScrollView();

            if (Widgets.ButtonText(new Rect(rect.x, rect.yMax - 32f, 148f, 30f), "Restore Defaults")) settings.ResetTraitTags(selectedTrait);
        }

        private void SetBuiltInTag(string tag, bool enabled)
        {
            List<string> tags = settings.TraitTags(selectedTrait).ToList();
            tags.RemoveAll(existing => existing.Equals(tag, System.StringComparison.OrdinalIgnoreCase));
            if (enabled) tags.Add(tag);
            settings.SetTraitTags(selectedTrait, tags);
        }

        private void AddCustomTag()
        {
            string normalized = newTag?.Trim();
            if (normalized.NullOrEmpty()) return;
            List<string> tags = settings.TraitTags(selectedTrait).ToList();
            if (!Contains(tags, normalized)) tags.Add(normalized);
            settings.SetTraitTags(selectedTrait, tags);
            newTag = string.Empty;
        }

        private void RemoveTag(string tag)
        {
            List<string> tags = settings.TraitTags(selectedTrait).ToList();
            tags.RemoveAll(existing => existing.Equals(tag, System.StringComparison.OrdinalIgnoreCase));
            settings.SetTraitTags(selectedTrait, tags);
        }

        private static bool Contains(IEnumerable<string> tags, string value)
        {
            return tags != null && tags.Any(tag => tag.Equals(value, System.StringComparison.OrdinalIgnoreCase));
        }
    }
}