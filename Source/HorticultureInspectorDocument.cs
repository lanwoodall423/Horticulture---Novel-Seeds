using System;
using System.Collections.Generic;
using System.Linq;
using InsightCanvas;
using UnityEngine;

namespace HorticultureNovelSeeds
{
    public sealed class HorticultureInspectorRow
    {
        public string Id;
        public string Label;
        public string Detail;
        public bool Warning;
    }

    public sealed class HorticultureInspectorSnapshot
    {
        public string Title;
        public string Subtitle;
        public string PrimaryHeader;
        public string SecondaryHeader;
        public string PrimaryEmpty;
        public string SecondaryEmpty;
        public IReadOnlyList<HorticultureInspectorRow> PrimaryRows = new HorticultureInspectorRow[0];
        public IReadOnlyList<HorticultureInspectorRow> SecondaryRows = new HorticultureInspectorRow[0];
        public string ActionLabel;
        public Action Action;
    }

    /// <summary>
    /// Shared embedded inspector chrome.  The tab creates immutable summaries before Draw;
    /// this document only lays out bounded rows and forwards explicit actions.
    /// </summary>
    public sealed class HorticultureInspectorDocument
    {
        private const int MaximumRows = 1000;
        private readonly InsightUiDocument uiDocument;
        private readonly InsightUiHost uiHost;
        private readonly InsightUiTabs tabs;
        private readonly InsightUiVirtualList primaryList;
        private readonly InsightUiVirtualList secondaryList;
        private readonly InsightUiLabel titleLabel;
        private readonly InsightUiLabel subtitleLabel;
        private readonly InsightUiLabel primaryHeaderLabel;
        private readonly InsightUiLabel secondaryHeaderLabel;
        private readonly InsightUiLabel primaryEmptyLabel;
        private readonly InsightUiLabel secondaryEmptyLabel;
        private readonly InsightUiButton actionButton;
        private HorticultureInspectorSnapshot snapshot = new HorticultureInspectorSnapshot();
        private InsightUiOrientation orientation = InsightUiOrientation.Horizontal;
        private bool highContrast;
        private bool reducedMotion;
        private InsightUiDensity density = InsightUiDensity.Normal;

        public HorticultureInspectorDocument(string id)
        {
            titleLabel = DynamicLabel(id + ".title", () => snapshot.Title, InsightUiTextStyle.Heading);
            subtitleLabel = DynamicLabel(id + ".subtitle", () => snapshot.Subtitle, InsightUiTextStyle.Caption);
            primaryHeaderLabel = DynamicLabel(id + ".primary.header", () => snapshot.PrimaryHeader,
                InsightUiTextStyle.Label);
            secondaryHeaderLabel = DynamicLabel(id + ".secondary.header", () => snapshot.SecondaryHeader,
                InsightUiTextStyle.Label);
            primaryEmptyLabel = DynamicLabel(id + ".primary.empty", () => snapshot.PrimaryEmpty,
                InsightUiTextStyle.Caption);
            secondaryEmptyLabel = DynamicLabel(id + ".secondary.empty", () => snapshot.SecondaryEmpty,
                InsightUiTextStyle.Caption);

            primaryList = InsightUi.VirtualList(id + ".primary.list", 0, 38f, index => RowItem(snapshot.PrimaryRows, index,
                id + ".primary"));
            primaryList.Overscan = 3;
            primaryList.CacheLimit = 96;
            secondaryList = InsightUi.VirtualList(id + ".secondary.list", 0, 38f, index => RowItem(snapshot.SecondaryRows, index,
                id + ".secondary"));
            secondaryList.Overscan = 3;
            secondaryList.CacheLimit = 96;

            tabs = InsightUi.Tabs(id + ".tabs");
            tabs.Add("primary", "Traits", BuildPanel(id + ".primary", primaryHeaderLabel, primaryList, primaryEmptyLabel));
            tabs.Add("secondary", "Effects", BuildPanel(id + ".secondary", secondaryHeaderLabel, secondaryList, secondaryEmptyLabel));

            actionButton = InsightUi.Button(id + ".action", "Open details", () => snapshot.Action?.Invoke());
            actionButton.Visible = false;
            actionButton.Style.HorizontalAlignment = InsightAlignment.Start;

            InsightUiStack root = InsightUi.Column(id + ".root",
                InsightUi.Row(id + ".heading", titleLabel, InsightUi.Spacer(id + ".heading.spacer"), actionButton),
                subtitleLabel,
                InsightUi.Callout(id + ".guidance", InsightUiCalloutSeverity.Info,
                    "Cultivar details", "These values are read-only summaries of the selected plant or produce."),
                tabs,
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

        public int MaximumVisibleRows => MaximumRows;
        public int PrimaryRowCount => Math.Min(MaximumRows, snapshot.PrimaryRows?.Count ?? 0);
        public int SecondaryRowCount => Math.Min(MaximumRows, snapshot.SecondaryRows?.Count ?? 0);
        public bool IsNarrowWorkspace => orientation == InsightUiOrientation.Vertical;
        public bool HighContrast => highContrast;
        public bool ReducedMotion => reducedMotion;
        public InsightUiDensity Density => density;
        public bool TrackDuplicateIds => uiDocument.TrackDuplicateIds;
        public int DuplicateIdCount => uiDocument.Diagnostics.DuplicateIds;
        public int RenderErrorCount => uiDocument.Diagnostics.RenderErrors;

        public void Refresh(HorticultureInspectorSnapshot next)
        {
            snapshot = next ?? new HorticultureInspectorSnapshot();
            actionButton.Label = snapshot.ActionLabel ?? string.Empty;
            actionButton.Visible = snapshot.Action != null && !string.IsNullOrEmpty(snapshot.ActionLabel);
            primaryList.ItemCount = PrimaryRowCount;
            secondaryList.ItemCount = SecondaryRowCount;
            primaryList.Refresh();
            secondaryList.Refresh();
            uiDocument.Invalidate();
        }

        public void Draw(Rect rect)
        {
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

        private static InsightUiElement BuildPanel(string id, InsightUiLabel header, InsightUiVirtualList list,
            InsightUiLabel empty)
        {
            InsightUiSurface surface = InsightUi.Surface(id + ".surface", InsightUi.Column(id + ".body", header, list, empty));
            surface.SetPadding(6f);
            surface.Style.Flex = 1f;
            return surface;
        }

        private static InsightUiElement RowItem(IReadOnlyList<HorticultureInspectorRow> rows, int index, string prefix)
        {
            if (rows == null || index < 0 || index >= rows.Count || index >= MaximumRows)
                return InsightUi.Empty(prefix + ".empty." + index);
            HorticultureInspectorRow row = rows[index] ?? new HorticultureInspectorRow { Id = "unknown", Label = "Unknown" };
            string id = SafeId(row.Id, index);
            InsightUiElement detail = string.IsNullOrEmpty(row.Detail)
                ? InsightUi.Empty(prefix + "." + id + ".detail.empty")
                : InsightUi.Label(prefix + "." + id + ".detail", row.Detail, InsightUiTextStyle.Caption);
            InsightUiElement body = InsightUi.Column(prefix + "." + id + ".body",
                InsightUi.Label(prefix + "." + id + ".label", row.Label ?? "Unknown", InsightUiTextStyle.Body), detail);
            return InsightUi.Scope(prefix + "." + id, body);
        }

        private static string SafeId(string id, int index)
        {
            string safe = string.IsNullOrEmpty(id) ? "row" : new string(id.Select(character =>
                char.IsLetterOrDigit(character) || character == '_' || character == '-' ? character : '_').ToArray());
            return safe + "." + index;
        }

        private static InsightUiLabel DynamicLabel(string id, Func<string> provider,
            InsightUiTextStyle style = InsightUiTextStyle.Body)
        {
            return InsightUi.Label(id, string.Empty, style).SetTextProvider(provider);
        }
    }
}
