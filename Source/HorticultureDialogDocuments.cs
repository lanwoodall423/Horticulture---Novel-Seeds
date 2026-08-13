using System;
using System.Collections.Generic;
using System.Linq;
using InsightCanvas;
using UnityEngine;

namespace HorticultureNovelSeeds
{
    /// <summary>
    /// A player-facing row summary for the small Horticulture management dialogs.  It is
    /// intentionally a presentation record: callbacks still write through NovelSeedsSettings
    /// and the row never exposes a DefName, cache key, or mask hash.
    /// </summary>
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

    /// <summary>
    /// Shared Insight Canvas chrome for bounded Horticulture registries.  The legacy Windows
    /// remain lifecycle adapters for vanilla WindowStack, while list/search/presentation state
    /// belongs to this document and is cleaned up through PostClose.
    /// </summary>
    public sealed class HorticultureCollectionDialogDocument
    {
        private const int MaximumRows = 1000;
        private readonly IHorticultureCollectionDialogSurface surface;
        private readonly InsightUiDocument uiDocument;
        private readonly InsightUiHost uiHost;
        private readonly InsightUiSearchField searchField;
        private readonly InsightUiVirtualList rowList;
        private readonly InsightUiLabel emptyLabel;
        private readonly InsightUiLabel statusLabel;
        private readonly InsightUiTextField entryField;
        private readonly InsightUiLabel entryLabel;
        private readonly InsightUiButton entryButton;
        private readonly List<HorticultureDialogRow> rows = new List<HorticultureDialogRow>();
        private int rowFingerprint;
        private InsightUiDensity density = InsightUiDensity.Normal;
        private bool highContrast;
        private bool reducedMotion;
        private InsightUiOrientation orientation = InsightUiOrientation.Horizontal;

        public HorticultureCollectionDialogDocument(IHorticultureCollectionDialogSurface surface, string id)
        {
            this.surface = surface ?? throw new ArgumentNullException(nameof(surface));
            searchField = InsightUi.SearchField(id + ".search", string.Empty, "Search")
                .Bind(() => surface.Search ?? string.Empty, value =>
                {
                    surface.Search = value ?? string.Empty;
                    uiDocument.Invalidate();
                });
            rowList = InsightUi.VirtualList(id + ".rows", 0, 38f, RowItem);
            rowList.Overscan = 3;
            rowList.CacheLimit = 96;
            emptyLabel = InsightUi.Label(id + ".empty", string.Empty, InsightUiTextStyle.Caption);
            statusLabel = InsightUi.Label(id + ".status", string.Empty, InsightUiTextStyle.Caption);
            entryLabel = DynamicLabel(id + ".entry.label", () => surface.EntryLabel, InsightUiTextStyle.Caption);
            entryField = InsightUi.TextField(id + ".entry", string.Empty)
                .Bind(() => surface.Entry ?? string.Empty, value => surface.Entry = value ?? string.Empty);
            entryButton = InsightUi.Button(id + ".entry.add", "Add", () => surface.EntryAction?.Invoke());

            InsightUiStack root = InsightUi.Column(id + ".root",
                InsightUi.Row(id + ".header",
                    DynamicLabel(id + ".title", () => surface.Title, InsightUiTextStyle.Heading),
                    InsightUi.Spacer(id + ".header.spacer"),
                    ActionButton(id + ".secondary", surface.SecondaryLabel, () => surface.SecondaryAction?.Invoke()),
                    ActionButton(id + ".primary", surface.PrimaryLabel, () => surface.PrimaryAction?.Invoke())),
                InsightUi.Callout(id + ".description", InsightUiCalloutSeverity.Info,
                    "Horticulture workspace", surface.Description ?? string.Empty),
                searchField,
                InsightUi.Row(id + ".entry.row", entryLabel, entryField, entryButton),
                rowList,
                emptyLabel,
                statusLabel,
                InsightUi.Button(id + ".close", "Done", surface.Close),
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

        public int RowCount => rows.Count;
        public int VisibleRowBudget => rowList.CacheLimit;
        public bool IsNarrow => orientation == InsightUiOrientation.Vertical;
        public bool HighContrast => highContrast;
        public bool ReducedMotion => reducedMotion;
        public InsightUiDensity Density => density;
        public int DuplicateIdCount => uiDocument.Diagnostics.DuplicateIds;
        public int RenderErrorCount => uiDocument.Diagnostics.RenderErrors;
        public bool TrackDuplicateIds => uiDocument.TrackDuplicateIds;

        public void Draw(Rect rect)
        {
            RefreshRows();
            orientation = rect.width < 820f ? InsightUiOrientation.Vertical : InsightUiOrientation.Horizontal;
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

        private void RefreshRows()
        {
            IReadOnlyList<HorticultureDialogRow> source = surface.Rows ?? new HorticultureDialogRow[0];
            string query = surface.Search?.Trim();
            List<HorticultureDialogRow> next = source.Where(row => row != null)
                .Where(row => string.IsNullOrEmpty(query)
                    || (row.Label ?? string.Empty).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
                    || (row.Detail ?? string.Empty).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
                    || (row.Status ?? string.Empty).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                .Take(MaximumRows).ToList();
            int nextFingerprint = Fingerprint(next);
            bool changed = nextFingerprint != rowFingerprint || next.Count != rows.Count;
            rows.Clear();
            rows.AddRange(next);
            if (changed)
            {
                rowFingerprint = nextFingerprint;
                rowList.ItemCount = rows.Count;
                rowList.Refresh();
                emptyLabel.Visible = rows.Count == 0;
                emptyLabel.SetTextProvider(() => rows.Count == 0 ? (surface.EmptyText ?? "Nothing to show.") : string.Empty);
                statusLabel.SetTextProvider(() => rows.Count + " items shown. Lists are bounded and searchable.");
                uiDocument.Invalidate();
            }
            bool showEntry = !string.IsNullOrEmpty(surface.EntryLabel);
            entryField.Visible = showEntry;
            entryLabel.Visible = showEntry;
            entryButton.Visible = showEntry && surface.EntryAction != null;
        }

        private InsightUiElement RowItem(int index)
        {
            if (index < 0 || index >= rows.Count) return InsightUi.Empty("empty." + index);
            HorticultureDialogRow row = rows[index];
            InsightUiElement main;
            if (row.CanToggle)
            {
                InsightUiToggle toggle = InsightUi.Toggle("toggle", row.Label)
                    .Bind(() => index < rows.Count && rows[index].Selected, value =>
                    {
                        if (index < rows.Count) rows[index].Toggle?.Invoke(value);
                        uiDocument.Invalidate();
                    });
                toggle.Enabled = row.Toggle != null;
                main = toggle;
            }
            else
            {
                InsightUiButton button = InsightUi.Button("select", row.Label, () => row.Activate?.Invoke());
                button.SelectedProvider = () => index < rows.Count && rows[index].Selected;
                main = button;
            }

            InsightUiElement detail = InsightUi.Label("detail", row.Detail ?? string.Empty, InsightUiTextStyle.Caption);
            InsightUiElement status = InsightUi.Badge("status", row.Status ?? (row.Warning ? "Needs review" : "Ready"));
            List<InsightUiElement> children = new List<InsightUiElement> { main, detail, status };
            if (row.HasValue && row.ValueChanged != null)
            {
                InsightUiSlider value = InsightUi.Slider("value", row.Value,
                    row.Minimum, row.Maximum).Bind(() => index < rows.Count ? rows[index].Value : row.Value,
                        next =>
                        {
                            if (index < rows.Count) rows[index].ValueChanged?.Invoke(next);
                            uiDocument.Invalidate();
                        });
                value.Enabled = row.Toggle == null || row.Selected;
                children.Add(value);
            }
            if (row.Activate != null && row.CanToggle)
            {
                InsightUiButton action = InsightUi.Button("action", row.ActionLabel ?? "Open", row.Activate);
                action.Style.HorizontalAlignment = InsightAlignment.Start;
                children.Add(action);
            }
            return InsightUi.Scope("row." + SafeId(row.Id), InsightUi.Row("body", children.ToArray()));
        }

        private static InsightUiLabel DynamicLabel(string id, Func<string> provider, InsightUiTextStyle style)
        {
            return InsightUi.Label(id, string.Empty, style).SetTextProvider(provider);
        }

        private static InsightUiButton ActionButton(string id, string label, Action action)
        {
            InsightUiButton button = InsightUi.Button(id, label ?? string.Empty, action ?? (() => { }));
            button.Visible = !string.IsNullOrEmpty(label);
            button.Style.HorizontalAlignment = InsightAlignment.Start;
            button.SetTooltip(label ?? string.Empty);
            return button;
        }

        private static int Fingerprint(IReadOnlyList<HorticultureDialogRow> values)
        {
            unchecked
            {
                int hash = 17;
                for (int i = 0; i < values.Count; i++)
                {
                    HorticultureDialogRow row = values[i];
                    hash = hash * 31 + (row.Id ?? string.Empty).GetHashCode();
                    hash = hash * 31 + (row.Label ?? string.Empty).GetHashCode();
                    hash = hash * 31 + (row.Detail ?? string.Empty).GetHashCode();
                    hash = hash * 31 + (row.Status ?? string.Empty).GetHashCode();
                    hash = hash * 31 + (row.Selected ? 1 : 0);
                    hash = hash * 31 + row.Value.GetHashCode();
                }
                return hash;
            }
        }

        private static string SafeId(string value)
        {
            if (string.IsNullOrEmpty(value)) return "unknown";
            char[] chars = value.Select(character => char.IsLetterOrDigit(character) ? character : '_').ToArray();
            return new string(chars);
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

    /// <summary>
    /// Persistent naming-dialog chrome.  Saving remains an explicit callback, so cancellation
    /// cannot create a cultivar and the existing UnlockVariety/RenameVariety authority is kept.
    /// </summary>
    public sealed class HorticultureNamingDocument
    {
        private readonly IHorticultureNamingSurface surface;
        private readonly InsightUiDocument uiDocument;
        private readonly InsightUiHost uiHost;
        private readonly InsightUiTextField nameField;
        private readonly InsightUiLabel validationLabel;
        private bool highContrast;
        private bool reducedMotion;

        public HorticultureNamingDocument(IHorticultureNamingSurface surface, string id)
        {
            this.surface = surface ?? throw new ArgumentNullException(nameof(surface));
            nameField = InsightUi.TextField(id + ".name", "").Bind(
                () => surface.Name ?? string.Empty,
                value => surface.Name = value ?? string.Empty);
            validationLabel = InsightUi.Label(id + ".validation", string.Empty, InsightUiTextStyle.Caption);
            InsightUiElement[] chips = (surface.TraitLabels ?? new string[0]).Take(12)
                .Select((label, index) => (InsightUiElement)InsightUi.Badge(id + ".trait." + index, label ?? "Trait"))
                .ToArray();
            InsightUiElement traitArea = chips.Length == 0
                ? InsightUi.Label(id + ".traits.empty", "No additional traits", InsightUiTextStyle.Caption)
                : InsightUi.Wrap(id + ".traits", chips);
            InsightUiStack root = InsightUi.Column(id + ".root",
                DynamicLabel(id + ".title", () => surface.Title, InsightUiTextStyle.Heading),
                InsightUi.SectionHeader(id + ".source.header", "Source plant", "Where this cultivar came from."),
                DynamicLabel(id + ".source", () => surface.SourceLabel),
                DynamicLabel(id + ".origin", () => surface.OriginLabel, InsightUiTextStyle.Caption),
                DynamicLabel(id + ".lineage", () => surface.LineageLabel, InsightUiTextStyle.Caption),
                InsightUi.SectionHeader(id + ".traits.header", "Traits", "Inherited and discovered traits."),
                traitArea,
                nameField,
                validationLabel,
                InsightUi.Row(id + ".actions",
                    InsightUi.Button(id + ".cancel", "Cancel", surface.Cancel),
                    InsightUi.Spacer(id + ".actions.spacer"),
                    InsightUi.Button(id + ".save", "Save Cultivar", surface.Save)),
                InsightUi.Toast(id + ".toast"));
            root.Style.Gap = 7f;
            uiDocument = new InsightUiDocument(id + ".document", root)
            {
                Theme = InsightTheme.Default.Clone(),
                HighContrast = highContrast,
                ReducedMotion = reducedMotion,
                DrawBackground = true,
                TrackDuplicateIds = true
            };
            uiHost = new InsightUiHost(uiDocument);
        }

        public int DuplicateIdCount => uiDocument.Diagnostics.DuplicateIds;
        public int RenderErrorCount => uiDocument.Diagnostics.RenderErrors;
        public bool HighContrast => highContrast;
        public bool ReducedMotion => reducedMotion;

        public void Draw(Rect rect)
        {
            validationLabel.SetTextProvider(() => surface.ValidationMessage ?? string.Empty);
            uiHost.Draw(rect, Time.deltaTime);
        }

        public void PostClose() => uiHost.PostClose();

        public void SetAccessibility(bool useHighContrast, bool useReducedMotion, InsightUiDensity density)
        {
            highContrast = useHighContrast;
            reducedMotion = useReducedMotion;
            uiDocument.HighContrast = highContrast;
            uiDocument.ReducedMotion = reducedMotion;
            uiDocument.Density = density;
            uiDocument.Invalidate();
        }

        private static InsightUiLabel DynamicLabel(string id, Func<string> provider,
            InsightUiTextStyle style = InsightUiTextStyle.Body)
        {
            return InsightUi.Label(id, string.Empty, style).SetTextProvider(provider);
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

    /// <summary>
    /// Small Canvas-owned form chrome for compatibility dialogs that still have a focused
    /// text-entry workflow. The authority callback remains responsible for validation and save.
    /// </summary>
    public sealed class HorticultureInputDialogDocument
    {
        private readonly IHorticultureInputDialogSurface surface;
        private readonly InsightUiDocument uiDocument;
        private readonly InsightUiHost uiHost;
        private readonly InsightUiTextField valueField;
        private readonly InsightUiLabel validationLabel;
        private readonly InsightUiLabel fieldLabel;
        private readonly InsightUiButton primaryButton;
        private readonly InsightUiButton secondaryButton;
        private bool highContrast;
        private bool reducedMotion;
        private InsightUiDensity density = InsightUiDensity.Normal;

        public HorticultureInputDialogDocument(IHorticultureInputDialogSurface surface, string id)
        {
            this.surface = surface ?? throw new ArgumentNullException(nameof(surface));
            valueField = InsightUi.TextField(id + ".value", string.Empty)
                .Bind(() => surface.Value ?? string.Empty, value => surface.Value = value ?? string.Empty);
            fieldLabel = DynamicLabel(id + ".field.label", () => surface.FieldLabel, InsightUiTextStyle.Label);
            validationLabel = DynamicLabel(id + ".validation", () => surface.ValidationMessage, InsightUiTextStyle.Caption);
            primaryButton = InsightUi.Button(id + ".primary", surface.PrimaryLabel ?? string.Empty,
                () => surface.PrimaryAction?.Invoke());
            secondaryButton = InsightUi.Button(id + ".secondary", surface.SecondaryLabel ?? string.Empty,
                () => surface.SecondaryAction?.Invoke());

            InsightUiStack root = InsightUi.Column(id + ".root",
                DynamicLabel(id + ".title", () => surface.Title, InsightUiTextStyle.Heading),
                InsightUi.Callout(id + ".description", InsightUiCalloutSeverity.Info,
                    "Horticulture", surface.Description ?? string.Empty),
                InsightUi.Row(id + ".field", fieldLabel, valueField),
                validationLabel,
                InsightUi.Row(id + ".actions",
                    InsightUi.Button(id + ".cancel", "Cancel", surface.Close),
                    InsightUi.Spacer(id + ".actions.spacer"), secondaryButton, primaryButton),
                InsightUi.Toast(id + ".toast"));
            root.Style.Gap = 8f;
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

        public int DuplicateIdCount => uiDocument.Diagnostics.DuplicateIds;
        public int RenderErrorCount => uiDocument.Diagnostics.RenderErrors;
        public bool TrackDuplicateIds => uiDocument.TrackDuplicateIds;
        public bool HighContrast => highContrast;
        public bool ReducedMotion => reducedMotion;
        public InsightUiDensity Density => density;

        public void Draw(Rect rect)
        {
            fieldLabel.Text = surface.FieldLabel ?? string.Empty;
            valueField.Visible = !string.IsNullOrEmpty(surface.FieldLabel);
            fieldLabel.Visible = valueField.Visible;
            validationLabel.Visible = !string.IsNullOrEmpty(surface.ValidationMessage);
            primaryButton.Visible = !string.IsNullOrEmpty(surface.PrimaryLabel) && surface.PrimaryAction != null;
            secondaryButton.Visible = !string.IsNullOrEmpty(surface.SecondaryLabel) && surface.SecondaryAction != null;
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

        private static InsightUiLabel DynamicLabel(string id, Func<string> provider,
            InsightUiTextStyle style = InsightUiTextStyle.Body)
        {
            return InsightUi.Label(id, string.Empty, style).SetTextProvider(provider);
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

    /// <summary>
    /// Canvas chrome for a specialized preview. The custom surface is deliberately delegated
    /// back to Horticulture so texture composition and resource ownership stay local.
    /// </summary>
    public sealed class HorticulturePreviewDialogDocument
    {
        private readonly IHorticulturePreviewDialogSurface surface;
        private readonly InsightUiDocument uiDocument;
        private readonly InsightUiHost uiHost;
        private readonly InsightUiLabel[] legendLabels;
        private readonly InsightUiSplit previewSplit;
        private bool highContrast;
        private bool reducedMotion;
        private InsightUiDensity density = InsightUiDensity.Normal;

        public HorticulturePreviewDialogDocument(IHorticulturePreviewDialogSurface surface, string id)
        {
            this.surface = surface ?? throw new ArgumentNullException(nameof(surface));
            legendLabels = Enumerable.Range(0, 3)
                .Select(index => DynamicLabel(id + ".legend." + index,
                    () => LegendAt(index), InsightUiTextStyle.Caption)).ToArray();
            InsightUiSurface preview = InsightUi.Surface(id + ".preview.surface",
                InsightUi.Custom(id + ".preview", context =>
                    surface.DrawPreview(new Rect(context.Bounds.X, context.Bounds.Y, context.Bounds.Width, context.Bounds.Height)),
                    (constraints, frame) => new InsightUiSize(Math.Min(640f, constraints.MaxWidth), 420f)));
            preview.SetPadding(8f);
            preview.Style.Flex = 1f;
            InsightUiStack legend = InsightUi.Row(id + ".legend", legendLabels.Cast<InsightUiElement>().ToArray());
            legend.Style.Gap = 8f;
            InsightUiStack left = InsightUi.Column(id + ".preview.column", preview, legend);
            left.Style.Flex = 1f;
            InsightUiStack right = InsightUi.Column(id + ".guidance",
                InsightUi.SectionHeader(id + ".guidance.header", "Preview guidance", "Preview-only colors never change saved masks."),
                DynamicLabel(id + ".guidance.text", () => surface.Description, InsightUiTextStyle.Caption));
            previewSplit = InsightUi.Split(id + ".split", left, right, 0.68f);
            previewSplit.Draggable = true;
            previewSplit.Style.Flex = 1f;
            InsightUiStack root = InsightUi.Column(id + ".root",
                InsightUi.Row(id + ".header",
                    DynamicLabel(id + ".title", () => surface.Title, InsightUiTextStyle.Heading),
                    InsightUi.Spacer(id + ".header.spacer"),
                    InsightUi.Button(id + ".close", "Close", surface.Close)),
                previewSplit,
                InsightUi.Row(id + ".footer",
                    InsightUi.Button(id + ".refresh", "New Colors", surface.RefreshPreview),
                    InsightUi.Spacer(id + ".footer.spacer"),
                    InsightUi.Button(id + ".done", "Done", surface.Close)),
                InsightUi.Toast(id + ".toast"));
            root.Style.Gap = 8f;
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

        public bool IsNarrow => previewSplit.Orientation == InsightUiOrientation.Vertical;
        public int DuplicateIdCount => uiDocument.Diagnostics.DuplicateIds;
        public int RenderErrorCount => uiDocument.Diagnostics.RenderErrors;
        public bool TrackDuplicateIds => uiDocument.TrackDuplicateIds;
        public bool HighContrast => highContrast;
        public bool ReducedMotion => reducedMotion;
        public InsightUiDensity Density => density;

        public void Draw(Rect rect)
        {
            previewSplit.Orientation = rect.width < 820f ? InsightUiOrientation.Vertical : InsightUiOrientation.Horizontal;
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

        private string LegendAt(int index)
        {
            IReadOnlyList<string> legend = surface.Legend;
            return legend != null && index < legend.Count ? legend[index] ?? string.Empty : string.Empty;
        }

        private static InsightUiLabel DynamicLabel(string id, Func<string> provider,
            InsightUiTextStyle style = InsightUiTextStyle.Body)
        {
            return InsightUi.Label(id, string.Empty, style).SetTextProvider(provider);
        }
    }
}
