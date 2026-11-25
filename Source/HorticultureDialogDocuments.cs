using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using UnityEngine;

namespace HorticultureNovelSeeds
{
    public sealed class HorticultureDialogRow
    {
        public string Id;
        public string Label;
        public string Detail;
        public string Status;
        public bool Selected;
        public bool Warning;
        public bool CanToggle;
        public Action<bool> Toggle;
        public Action Activate;
        public string ActionLabel;
        public bool HasValue;
        public float Value;
        public float Minimum;
        public float Maximum;
        public Action<float> ValueChanged;
    }

    public interface IHorticultureCollectionDialogSurface
    {
        string Title { get; }
        string Description { get; }
        string Search { get; set; }
        IReadOnlyList<HorticultureDialogRow> Rows { get; }
        string EmptyText { get; }
        string EntryLabel { get; }
        string Entry { get; set; }
        Action EntryAction { get; }
        string PrimaryLabel { get; }
        Action PrimaryAction { get; }
        string SecondaryLabel { get; }
        Action SecondaryAction { get; }
        void Close();
    }

    public sealed class HorticultureCollectionDialogSurfaceAdapter : IHorticultureCollectionDialogSurface
    {
        public Func<string> TitleProvider;
        public Func<string> DescriptionProvider;
        public Func<string> SearchProvider;
        public Action<string> SearchSetter;
        public Func<IReadOnlyList<HorticultureDialogRow>> RowsProvider;
        public Func<string> EmptyProvider;
        public Func<string> EntryLabelProvider;
        public Func<string> EntryProvider;
        public Action<string> EntrySetter;
        public Action EntryActionCallback;
        public Func<string> PrimaryLabelProvider;
        public Action PrimaryActionCallback;
        public Func<string> SecondaryLabelProvider;
        public Action SecondaryActionCallback;
        public Action CloseAction;

        public string Title => TitleProvider?.Invoke() ?? string.Empty;
        public string Description => DescriptionProvider?.Invoke() ?? string.Empty;
        public string Search { get => SearchProvider?.Invoke() ?? string.Empty; set => SearchSetter?.Invoke(value ?? string.Empty); }
        public IReadOnlyList<HorticultureDialogRow> Rows => RowsProvider?.Invoke() ?? new HorticultureDialogRow[0];
        public string EmptyText => EmptyProvider?.Invoke() ?? "Nothing to show.";
        public string EntryLabel => EntryLabelProvider?.Invoke() ?? string.Empty;
        public string Entry { get => EntryProvider?.Invoke() ?? string.Empty; set => EntrySetter?.Invoke(value ?? string.Empty); }
        public Action EntryAction => EntryActionCallback;
        public string PrimaryLabel => PrimaryLabelProvider?.Invoke() ?? string.Empty;
        public Action PrimaryAction => PrimaryActionCallback;
        public string SecondaryLabel => SecondaryLabelProvider?.Invoke() ?? string.Empty;
        public Action SecondaryAction => SecondaryActionCallback;
        public void Close() => CloseAction?.Invoke();
    }

    public sealed class HorticultureCollectionDialogDocument
    {
        private readonly IHorticultureCollectionDialogSurface surface;
        private Vector2 scroll;
        public HorticultureCollectionDialogDocument(IHorticultureCollectionDialogSurface surface, string id) { this.surface = surface; }
        public void PostClose() { }
        public void Draw(Rect rect)
        {
            float y = rect.y;
            NativeUi.Label(rect, ref y, surface.Title, 28);
            NativeUi.Label(rect, ref y, surface.Description, 42);
            string search = NativeUi.Field(rect, ref y, surface.Search);
            if (search != surface.Search) { surface.Search = search; scroll = Vector2.zero; }
            if (surface.EntryAction != null)
            {
                NativeUi.Label(rect, ref y, surface.EntryLabel, 22);
                string entry = NativeUi.Field(rect, ref y, surface.Entry);
                if (entry != surface.Entry) surface.Entry = entry;
                NativeUi.Button(rect, ref y, "Add", surface.EntryAction);
            }
            var rows = (surface.Rows ?? Array.Empty<HorticultureDialogRow>()).Take(1000).ToArray();
            float footer = rect.yMax - 38;
            Rect list = new Rect(rect.x, y, rect.width, Math.Max(20, footer - y - 6));
            float height = rows.Sum(r => 74 + (r.HasValue ? 46 : 0) + (r.Activate != null ? 34 : 0));
            var view = NativeUi.Scroll(list, ref scroll, height);
            try
            {
                float ry = 0;
                if (rows.Length == 0) NativeUi.Label(view, ref ry, surface.EmptyText);
                foreach (var row in rows)
                {
                    if (row.CanToggle && row.Toggle != null) NativeUi.Toggle(view, ref ry, row.Label, row.Selected, row.Toggle);
                    else NativeUi.Label(view, ref ry, row.Label);
                    NativeUi.Label(view, ref ry, string.Join(" ? ", new[] { row.Detail, row.Status }.Where(t => !string.IsNullOrEmpty(t))), 36);
                    if (row.HasValue && row.ValueChanged != null) NativeUi.Slider(view, ref ry, "Weight", row.Value, row.Minimum, row.Maximum, row.ValueChanged);
                    if (row.Activate != null) NativeUi.Button(view, ref ry, string.IsNullOrEmpty(row.ActionLabel) ? "Open" : row.ActionLabel, row.Activate);
                }
            }
            finally { Widgets.EndScrollView(); }
            var actions = new[] { (surface.PrimaryLabel, surface.PrimaryAction), (surface.SecondaryLabel, surface.SecondaryAction), ("Close", (Action)surface.Close) }
                .Where(a => a.Item2 != null && !string.IsNullOrEmpty(a.Item1)).ToArray();
            for (int i = 0; i < actions.Length; i++)
            {
                float w = rect.width / actions.Length;
                if (Widgets.ButtonText(new Rect(rect.x + i * w, footer, w - 4, 32), actions[i].Item1)) actions[i].Item2();
            }
        }
    }

    public interface IHorticultureNamingSurface
    {
        string Title { get; }
        string SourceLabel { get; }
        string OriginLabel { get; }
        string LineageLabel { get; }
        IReadOnlyList<string> TraitLabels { get; }
        string Name { get; set; }
        string ValidationMessage { get; }
        void Save();
        void Cancel();
    }

    public sealed class HorticultureNamingDocument
    {
        private readonly IHorticultureNamingSurface surface;
        private Vector2 scroll;
        public HorticultureNamingDocument(IHorticultureNamingSurface surface, string id) { this.surface = surface; }
        public void PostClose() { }
        public void Draw(Rect rect)
        {
            float y = rect.y;
            NativeUi.Label(rect, ref y, surface.Title);
            NativeUi.Label(rect, ref y, surface.SourceLabel);
            string name = NativeUi.Field(rect, ref y, surface.Name);
            if (name != surface.Name) surface.Name = name;
            var traits = surface.TraitLabels ?? Array.Empty<string>();
            var view = NativeUi.Scroll(new Rect(rect.x, y, rect.width, Math.Max(40, rect.yMax - y - 72)), ref scroll, 100 + traits.Count * 34);
            try
            {
                float ry = 0;
                NativeUi.Label(view, ref ry, surface.OriginLabel, 42);
                NativeUi.Label(view, ref ry, surface.LineageLabel, 42);
                foreach (var trait in traits.Take(128)) NativeUi.Label(view, ref ry, trait);
            }
            finally { Widgets.EndScrollView(); }
            Widgets.Label(new Rect(rect.x, rect.yMax - 66, rect.width, 30), surface.ValidationMessage ?? "");
            float w = rect.width / 2;
            if (Widgets.ButtonText(new Rect(rect.x, rect.yMax - 32, w - 4, 30), "Save")) surface.Save();
            if (Widgets.ButtonText(new Rect(rect.x + w, rect.yMax - 32, w - 4, 30), "Cancel")) surface.Cancel();
        }
    }

    public interface IHorticultureInputDialogSurface
    {
        string Title { get; }
        string Description { get; }
        string FieldLabel { get; }
        string Value { get; set; }
        string ValidationMessage { get; }
        string PrimaryLabel { get; }
        Action PrimaryAction { get; }
        string SecondaryLabel { get; }
        Action SecondaryAction { get; }
        void Close();
    }

    public sealed class HorticultureInputDialogDocument
    {
        private readonly IHorticultureInputDialogSurface surface;
        public HorticultureInputDialogDocument(IHorticultureInputDialogSurface surface, string id) { this.surface = surface; }
        public void PostClose() { }
        public void Draw(Rect rect)
        {
            float y = rect.y;
            NativeUi.Label(rect, ref y, surface.Title);
            NativeUi.Label(rect, ref y, surface.Description, 64);
            NativeUi.Label(rect, ref y, surface.FieldLabel);
            string value = NativeUi.Field(rect, ref y, surface.Value);
            if (value != surface.Value) surface.Value = value;
            NativeUi.Label(rect, ref y, surface.ValidationMessage, 42);
            NativeUi.Button(rect, ref y, surface.PrimaryLabel, surface.PrimaryAction);
            NativeUi.Button(rect, ref y, surface.SecondaryLabel, surface.SecondaryAction);
            NativeUi.Button(rect, ref y, "Close", surface.Close);
        }
    }

    public interface IHorticulturePreviewDialogSurface
    {
        string Title { get; }
        string Description { get; }
        IReadOnlyList<string> Legend { get; }
        void DrawPreview(Rect rect);
        void RefreshPreview();
        void Close();
    }

    public sealed class HorticulturePreviewDialogDocument
    {
        private readonly IHorticulturePreviewDialogSurface surface;
        public HorticulturePreviewDialogDocument(IHorticulturePreviewDialogSurface surface, string id) { this.surface = surface; }
        public void PostClose() { }
        public void Draw(Rect rect)
        {
            float y = rect.y;
            NativeUi.Label(rect, ref y, surface.Title);
            NativeUi.Label(rect, ref y, surface.Description, 40);
            foreach (var line in (surface.Legend ?? Array.Empty<string>()).Take(8)) NativeUi.Label(rect, ref y, line, 22);
            NativeUi.Button(rect, ref y, "Refresh preview", surface.RefreshPreview);
            surface.DrawPreview(new Rect(rect.x, y, rect.width, Math.Max(1, rect.yMax - y - 38)));
            if (Widgets.ButtonText(new Rect(rect.x, rect.yMax - 32, rect.width, 30), "Close")) surface.Close();
        }
    }
}
