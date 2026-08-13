using System;
using System.Collections.Generic;
using System.Linq;
using InsightCanvas;
using UnityEngine;

namespace HorticultureNovelSeeds
{
    public interface IHorticultureTraitTagSurface
    {
        string Title { get; }
        string Description { get; }
        IReadOnlyList<VarietyTraitDef> TraitOptions { get; }
        VarietyTraitDef SelectedTrait { get; set; }
        string Search { get; set; }
        string NewTag { get; set; }
        IReadOnlyList<string> Tags { get; }
        void SetTag(string tag, bool enabled);
        void AddTag();
        void RemoveTag(string tag);
        void Reset();
        void Close();
    }

    public sealed class HorticultureTraitTagSurfaceAdapter : IHorticultureTraitTagSurface
    {
        public Func<string> TitleProvider;
        public Func<string> DescriptionProvider;
        public Func<IReadOnlyList<VarietyTraitDef>> TraitOptionsProvider;
        public Func<VarietyTraitDef> SelectedTraitProvider;
        public Action<VarietyTraitDef> SelectedTraitSetter;
        public Func<string> SearchProvider;
        public Action<string> SearchSetter;
        public Func<string> NewTagProvider;
        public Action<string> NewTagSetter;
        public Func<IReadOnlyList<string>> TagsProvider;
        public Action<string, bool> SetTagCallback;
        public Action AddTagCallback;
        public Action<string> RemoveTagCallback;
        public Action ResetCallback;
        public Action CloseAction;

        public string Title => TitleProvider?.Invoke() ?? string.Empty;
        public string Description => DescriptionProvider?.Invoke() ?? string.Empty;
        public IReadOnlyList<VarietyTraitDef> TraitOptions => TraitOptionsProvider?.Invoke() ?? new VarietyTraitDef[0];
        public VarietyTraitDef SelectedTrait { get => SelectedTraitProvider?.Invoke(); set => SelectedTraitSetter?.Invoke(value); }
        public string Search { get => SearchProvider?.Invoke() ?? string.Empty; set => SearchSetter?.Invoke(value ?? string.Empty); }
        public string NewTag { get => NewTagProvider?.Invoke() ?? string.Empty; set => NewTagSetter?.Invoke(value ?? string.Empty); }
        public IReadOnlyList<string> Tags => TagsProvider?.Invoke() ?? new string[0];
        public void SetTag(string tag, bool enabled) => SetTagCallback?.Invoke(tag, enabled);
        public void AddTag() => AddTagCallback?.Invoke();
        public void RemoveTag(string tag) => RemoveTagCallback?.Invoke(tag);
        public void Reset() => ResetCallback?.Invoke();
        public void Close() => CloseAction?.Invoke();
    }

    public sealed class HorticultureTraitTagDocument
    {
        private const int MaximumTags = 1000;
        private readonly IHorticultureTraitTagSurface surface;
        private readonly InsightUiDocument uiDocument;
        private readonly InsightUiHost uiHost;
        private readonly InsightUiSelect traitSelect;
        private readonly InsightUiSearchField searchField;
        private readonly InsightUiToggle positiveToggle;
        private readonly InsightUiToggle negativeToggle;
        private readonly InsightUiTextField newTagField;
        private readonly InsightUiVirtualList tagList;
        private readonly InsightUiLabel tagsEmpty;
        private readonly string[] traitOptions;
        private int tagCount;
        private int fingerprint;
        private InsightUiDensity density = InsightUiDensity.Normal;
        private bool highContrast;
        private bool reducedMotion;

        public HorticultureTraitTagDocument(IHorticultureTraitTagSurface surface, string id)
        {
            this.surface = surface ?? throw new ArgumentNullException(nameof(surface));
            traitOptions = surface.TraitOptions.Select(trait => trait == null ? "Unknown" : TraitColorUI.Label(trait)).ToArray();
            traitSelect = InsightUi.Select(id + ".trait", "Trait", traitOptions, 0).Bind(
                () => SelectedTraitIndex(),
                index =>
                {
                    if (index >= 0 && index < surface.TraitOptions.Count) surface.SelectedTrait = surface.TraitOptions[index];
                    uiDocument.Invalidate();
                });
            searchField = InsightUi.SearchField(id + ".search", string.Empty, "Search tags")
                .Bind(() => surface.Search ?? string.Empty, value => { surface.Search = value ?? string.Empty; uiDocument.Invalidate(); });
            positiveToggle = InsightUi.Toggle(id + ".positive", "Positive")
                .Bind(() => HasTag("Positive"), value => surface.SetTag("Positive", value));
            negativeToggle = InsightUi.Toggle(id + ".negative", "Negative")
                .Bind(() => HasTag("Negative"), value => surface.SetTag("Negative", value));
            newTagField = InsightUi.TextField(id + ".new-tag", string.Empty)
                .Bind(() => surface.NewTag ?? string.Empty, value => surface.NewTag = value ?? string.Empty);
            tagList = InsightUi.VirtualList(id + ".tags", 0, 36f, TagItem);
            tagList.Overscan = 3;
            tagList.CacheLimit = 96;
            tagsEmpty = InsightUi.Label(id + ".empty", "No custom tags", InsightUiTextStyle.Caption);

            InsightUiStack root = InsightUi.Column(id + ".root",
                DynamicLabel(id + ".title", () => surface.Title, InsightUiTextStyle.Heading),
                DynamicLabel(id + ".description", () => surface.Description, InsightUiTextStyle.Caption),
                traitSelect,
                searchField,
                InsightUi.Row(id + ".built-in", positiveToggle, negativeToggle),
                InsightUi.Row(id + ".new-tag", newTagField, InsightUi.Button(id + ".add", "Add", surface.AddTag)),
                tagList,
                tagsEmpty,
                InsightUi.Row(id + ".actions", InsightUi.Button(id + ".reset", "Restore Defaults", surface.Reset),
                    InsightUi.Spacer(id + ".actions.spacer"), InsightUi.Button(id + ".close", "Done", surface.Close)),
                InsightUi.Toast(id + ".toast"));
            root.Style.Gap = 6f;
            root.Style.Flex = 1f;
            uiDocument = new InsightUiDocument(id + ".document", root)
            {
                Theme = InsightTheme.Default.Clone(),
                Density = density,
                HighContrast = highContrast,
                ReducedMotion = reducedMotion,
                DrawBackground = true,
                TrackDuplicateIds = true
            };
            uiHost = new InsightUiHost(uiDocument);
        }

        public int TagCount => tagCount;
        public int DuplicateIdCount => uiDocument.Diagnostics.DuplicateIds;
        public int RenderErrorCount => uiDocument.Diagnostics.RenderErrors;
        public bool HighContrast => highContrast;
        public bool ReducedMotion => reducedMotion;

        public void Draw(Rect rect)
        {
            int nextFingerprint = Fingerprint(surface.Tags, surface.Search, surface.SelectedTrait);
            if (nextFingerprint != fingerprint)
            {
                fingerprint = nextFingerprint;
                tagCount = FilteredTags().Count;
                tagList.ItemCount = tagCount;
                tagList.Refresh();
                tagsEmpty.Visible = tagCount == 0;
                uiDocument.Invalidate();
            }
            uiHost.Draw(rect, Time.deltaTime);
        }

        public void PostClose() => uiHost.PostClose();

        public void SetAccessibility(bool useHighContrast, bool useReducedMotion, InsightUiDensity requestedDensity)
        {
            highContrast = useHighContrast;
            reducedMotion = useReducedMotion;
            density = requestedDensity;
            uiDocument.HighContrast = highContrast;
            uiDocument.ReducedMotion = reducedMotion;
            uiDocument.Density = density;
            uiDocument.Invalidate();
        }

        private int SelectedTraitIndex()
        {
            for (int i = 0; i < surface.TraitOptions.Count; i++) if (ReferenceEquals(surface.TraitOptions[i], surface.SelectedTrait)) return i;
            return 0;
        }

        private bool HasTag(string tag) => surface.Tags.Any(item => string.Equals(item, tag, StringComparison.OrdinalIgnoreCase));

        private List<string> FilteredTags()
        {
            string query = surface.Search?.Trim();
            return surface.Tags.Where(tag => !string.Equals(tag, "Positive", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(tag, "Negative", StringComparison.OrdinalIgnoreCase))
                .Where(tag => string.IsNullOrEmpty(query) || tag.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderBy(tag => tag).Take(MaximumTags).ToList();
        }

        private InsightUiElement TagItem(int index)
        {
            List<string> tags = FilteredTags();
            if (index < 0 || index >= tags.Count) return InsightUi.Empty("tag.empty." + index);
            string tag = tags[index];
            return InsightUi.Scope("tag." + SafeId(tag), InsightUi.Row("body",
                InsightUi.Label("label", tag), InsightUi.Spacer("spacer"),
                InsightUi.Button("remove", "Remove", () => surface.RemoveTag(tag))));
        }

        private static int Fingerprint(IReadOnlyList<string> tags, string search, VarietyTraitDef trait)
        {
            unchecked
            {
                int hash = (trait?.defName ?? string.Empty).GetHashCode() * 31 + (search ?? string.Empty).GetHashCode();
                foreach (string tag in tags ?? new string[0]) hash = hash * 31 + (tag ?? string.Empty).GetHashCode();
                return hash;
            }
        }

        private static string SafeId(string value)
        {
            return new string((value ?? "unknown").Select(character => char.IsLetterOrDigit(character) ? character : '_').ToArray());
        }

        private static InsightUiLabel DynamicLabel(string id, Func<string> provider, InsightUiTextStyle style)
        {
            return InsightUi.Label(id, string.Empty, style).SetTextProvider(provider);
        }
    }
}
