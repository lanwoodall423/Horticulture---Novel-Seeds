using System;
using System.Collections.Generic;
using System.Linq;
using InsightCanvas;
using UnityEngine;
using Verse;

namespace HorticultureNovelSeeds
{
    public class Dialog_TraitTags : Window
    {
        private readonly NovelSeedsSettings settings;
        private readonly List<VarietyTraitDef> traits;
        private VarietyTraitDef selectedTrait;
        private string search = string.Empty;
        private string newTag = string.Empty;
        private HorticultureTraitTagDocument canvasDocument;

        public override Vector2 InitialSize => new Vector2(760f, 620f);

        public Dialog_TraitTags(NovelSeedsSettings settings, VarietyTraitDef root)
        {
            this.settings = settings;
            traits = new List<VarietyTraitDef> { root };
            if (root != null && root.configRoot && !root.configFamily.NullOrEmpty()) traits.AddRange(TraitConfigUtility.Types(root.configFamily));
            selectedTrait = root;
            doCloseX = true;
            absorbInputAroundWindow = true;
            canvasDocument = new HorticultureTraitTagDocument(new HorticultureTraitTagSurfaceAdapter
            {
                TitleProvider = () => "Trait Tags",
                DescriptionProvider = () => "Tags are global classifications available to gameplay systems and saved configuration profiles.",
                TraitOptionsProvider = () => traits,
                SelectedTraitProvider = () => selectedTrait,
                SelectedTraitSetter = value => { selectedTrait = value; newTag = string.Empty; },
                SearchProvider = () => search,
                SearchSetter = value => search = value,
                NewTagProvider = () => newTag,
                NewTagSetter = value => newTag = value,
                TagsProvider = () => settings.TraitTags(selectedTrait),
                SetTagCallback = SetBuiltInTag,
                AddTagCallback = AddCustomTag,
                RemoveTagCallback = RemoveTag,
                ResetCallback = () => { if (selectedTrait != null) settings.ResetTraitTags(selectedTrait); },
                CloseAction = () => Close()
            }, "hns.trait-tags");
        }

        public override void DoWindowContents(Rect inRect) => canvasDocument?.Draw(inRect);

        public override void PostClose()
        {
            canvasDocument?.PostClose();
            base.PostClose();
        }

        private void SetBuiltInTag(string tag, bool enabled)
        {
            if (selectedTrait == null) return;
            List<string> tags = settings.TraitTags(selectedTrait).ToList();
            tags.RemoveAll(existing => existing.Equals(tag, StringComparison.OrdinalIgnoreCase));
            if (enabled) tags.Add(tag);
            settings.SetTraitTags(selectedTrait, tags);
        }

        private void AddCustomTag()
        {
            if (selectedTrait == null) return;
            string normalized = newTag?.Trim();
            if (string.IsNullOrEmpty(normalized) || normalized.IndexOf(':') >= 0) return;
            List<string> tags = settings.TraitTags(selectedTrait).ToList();
            if (!tags.Any(tag => tag.Equals(normalized, StringComparison.OrdinalIgnoreCase))) tags.Add(normalized);
            settings.SetTraitTags(selectedTrait, tags);
            newTag = string.Empty;
        }

        private void RemoveTag(string tag)
        {
            if (selectedTrait == null) return;
            List<string> tags = settings.TraitTags(selectedTrait).ToList();
            tags.RemoveAll(existing => existing.Equals(tag, StringComparison.OrdinalIgnoreCase));
            settings.SetTraitTags(selectedTrait, tags);
        }
    }
}
