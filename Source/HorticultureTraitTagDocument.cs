using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
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
        private readonly IHorticultureTraitTagSurface surface;
        private Vector2 scroll;
        public HorticultureTraitTagDocument(IHorticultureTraitTagSurface surface, string id) { this.surface = surface; }
        public void PostClose() { }
        public void Draw(Rect rect)
        {
            float y = rect.y;
            NativeUi.Label(rect, ref y, surface.Title);
            NativeUi.Label(rect, ref y, surface.Description, 44);
            var traits = surface.TraitOptions.ToArray();
            NativeUi.Choice(rect, ref y, "Trait", traits.Select(t => t == null ? "Unknown" : TraitColorUI.Label(t)).ToArray(),
                Array.IndexOf(traits, surface.SelectedTrait), i => surface.SelectedTrait = traits[i]);
            foreach (var tag in new[] { "Positive", "Negative" })
                NativeUi.Toggle(rect, ref y, tag, surface.Tags.Any(t => t.Equals(tag, StringComparison.OrdinalIgnoreCase)), v => surface.SetTag(tag, v));
            string search = NativeUi.Field(rect, ref y, surface.Search);
            if (search != surface.Search) surface.Search = search;
            string newTag = NativeUi.Field(rect, ref y, surface.NewTag);
            if (newTag != surface.NewTag) surface.NewTag = newTag;
            NativeUi.Button(rect, ref y, "Add tag", surface.AddTag);
            NativeUi.Button(rect, ref y, "Reset tags", surface.Reset);
            var tags = surface.Tags.Where(t => t.IndexOf(surface.Search ?? "", StringComparison.OrdinalIgnoreCase) >= 0)
                .Where(t => t != "Positive" && t != "Negative").Take(1000).ToArray();
            var view = NativeUi.Scroll(new Rect(rect.x, y, rect.width, Math.Max(1, rect.yMax - y - 38)), ref scroll, tags.Length * 34);
            try { float ry = 0; foreach (var tag in tags) NativeUi.Button(view, ref ry, "Remove " + tag, () => surface.RemoveTag(tag)); }
            finally { Widgets.EndScrollView(); }
            if (Widgets.ButtonText(new Rect(rect.x, rect.yMax - 32, rect.width, 30), "Close")) surface.Close();
        }
    }
}
