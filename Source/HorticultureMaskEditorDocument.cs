using System;
using System.Collections.Generic;
using System.Linq;
using InsightCanvas;
using UnityEngine;

namespace HorticultureNovelSeeds
{
    /// <summary>
    /// Presentation bridge for the semantic mask editor.  The document owns editor chrome and
    /// accessibility state; the surface remains the authority for pixels, brush operations,
    /// projection, validation, history, and settings persistence.
    /// </summary>
    public interface IHorticultureMaskEditorSurface
    {
        string Title { get; }
        string PageLabel { get; }
        string OriginLabel { get; }
        string LayerStatus { get; }
        string StatusLabel { get; }
        int SelectedPage { get; set; }
        IReadOnlyList<string> PageOptions { get; }
        int SelectedVariation { get; set; }
        IReadOnlyList<string> VariationOptions { get; }
        bool Enabled { get; set; }
        int SelectedLayer { get; set; }
        IReadOnlyList<string> LayerOptions { get; }
        bool GetLayerLocked(int index);
        void SetLayerLocked(int index, bool locked);
        int PreviewMode { get; set; }
        bool ProjectionPreviewActive { get; }
        bool ValidationAvailable { get; }
        string ValidationLabel { get; }
        int ToolMode { get; set; }
        int PaintSelectionMode { get; set; }
        float BrushSize { get; set; }
        float Tolerance { get; set; }
        float SelectionRadius { get; set; }
        float FragmentLimit { get; set; }
        bool CanUndo { get; }
        bool CanRedo { get; }
        void DrawCanvas(Rect rect);
        void GrowSelection();
        void ShrinkSelection();
        void SmoothSelection();
        void FeatherSelection();
        void RemoveTinyFragments();
        void FillSelectionHoles();
        void FillUnmaskedPixels();
        void KeepLargestSelection();
        void SmartExpandSelection();
        void ClearSelection();
        void Validate();
        void PreviousIssue();
        void NextIssue();
        void Undo();
        void Redo();
        void CopyToVariation();
        void ProjectToVariation();
        void RegenerateAutoMask();
        void ResetToAutoMask();
        void ApplyProjection();
        void CancelProjection();
        void Close();
    }

    /// <summary>
    /// Document-owned mask editor shell.  It deliberately embeds the specialized Horticulture
    /// canvas through InsightUi.Custom so mask generation, painting, validation, and resource
    /// lifetime remain in the existing authoritative implementation.
    /// </summary>
    public sealed class HorticultureMaskEditorDocument
    {
        private readonly IHorticultureMaskEditorSurface surface;
        private readonly InsightUiDocument uiDocument;
        private readonly InsightUiHost uiHost;
        private InsightUiSplit editorSplit;
        private readonly InsightUiSegmented pageSelector;
        private readonly InsightUiSelect variationSelector;
        private readonly InsightUiSegmented plantLayerSelector;
        private readonly InsightUiSegmented produceLayerSelector;
        private readonly InsightUiSegmented previewSelector;
        private readonly InsightUiSegmented toolSelector;
        private readonly InsightUiSegmented paintModeSelector;
        private readonly InsightUiLabel originLabel;
        private readonly InsightUiLabel statusLabel;
        private readonly InsightUiBadge validationBadge;
        private InsightUiOrientation orientation = InsightUiOrientation.Horizontal;
        private InsightUiDensity density = InsightUiDensity.Normal;
        private bool highContrast;
        private bool reducedMotion;

        public HorticultureMaskEditorDocument(IHorticultureMaskEditorSurface surface)
        {
            this.surface = surface ?? throw new ArgumentNullException(nameof(surface));
            pageSelector = InsightUi.Segmented("hns.mask.page", surface.PageOptions.ToArray(), surface.SelectedPage)
                .Bind(() => surface.SelectedPage, value => surface.SelectedPage = value);
            variationSelector = InsightUi.Select("hns.mask.variation", "Variation",
                SafeOptions(surface.VariationOptions, "Current variation"), surface.SelectedVariation)
                .Bind(() => surface.SelectedVariation, value => surface.SelectedVariation = value);
            plantLayerSelector = InsightUi.Segmented("hns.mask.layer.plant", new[] { "Produce", "Leaves", "Stem" },
                Mathf.Clamp(surface.SelectedLayer, 0, 2))
                .Bind(() => surface.SelectedLayer, value => surface.SelectedLayer = value);
            produceLayerSelector = InsightUi.Segmented("hns.mask.layer.produce", new[] { "Produce", "Leaves", "Container" },
                Mathf.Clamp(surface.SelectedLayer, 0, 2))
                .Bind(() => surface.SelectedLayer, value => surface.SelectedLayer = value);
            previewSelector = InsightUi.Segmented("hns.mask.preview", new[] { "Original", "Mask", "Final" },
                Mathf.Clamp(surface.PreviewMode, 0, 2))
                .Bind(() => surface.PreviewMode, value => surface.PreviewMode = value);
            toolSelector = InsightUi.Segmented("hns.mask.tool", new[] { "Brush", "Erase", "Wand", "Move", "Region" },
                Mathf.Clamp(surface.ToolMode, 0, 4))
                .Bind(() => surface.ToolMode, value => surface.ToolMode = value);
            paintModeSelector = InsightUi.Segmented("hns.mask.paint-mode", new[] { "Add", "Remove", "Replace" },
                Mathf.Clamp(surface.PaintSelectionMode, 0, 2))
                .Bind(() => surface.PaintSelectionMode, value => surface.PaintSelectionMode = value);
            originLabel = DynamicLabel("hns.mask.origin", () => surface.OriginLabel, InsightUiTextStyle.Caption);
            statusLabel = DynamicLabel("hns.mask.status", () => surface.StatusLabel, InsightUiTextStyle.Caption);
            validationBadge = InsightUi.Badge("hns.mask.validation.badge", "Ready");

            InsightUiElement root = BuildRoot();
            uiDocument = new InsightUiDocument("hns.mask.editor.document", root)
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

        public bool IsNarrowWorkspace => orientation == InsightUiOrientation.Vertical;
        public bool HighContrast => highContrast;
        public bool ReducedMotion => reducedMotion;
        public InsightUiDensity Density => density;
        public bool TrackDuplicateIds => uiDocument.TrackDuplicateIds;
        public int DuplicateIdCount => uiDocument.Diagnostics.DuplicateIds;
        public int RenderErrorCount => uiDocument.Diagnostics.RenderErrors;
        public int PreviewSurfaceCount => 1;
        public int LayerCount => surface.LayerOptions?.Count ?? 0;
        public bool HasBoundedHistory => true;

        public void Draw(Rect rect)
        {
            bool presentationChanged = RefreshPresentation();
            InsightUiOrientation nextOrientation = rect.width < 820f
                ? InsightUiOrientation.Vertical
                : InsightUiOrientation.Horizontal;
            if (orientation != nextOrientation)
            {
                orientation = nextOrientation;
                editorSplit.Orientation = orientation;
                presentationChanged = true;
            }
            if (presentationChanged) uiDocument.Invalidate();
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

        private InsightUiElement BuildRoot()
        {
            InsightUiStack header = InsightUi.Row("hns.mask.header",
                DynamicLabel("hns.mask.title", () => surface.Title, InsightUiTextStyle.Heading),
                InsightUi.Spacer("hns.mask.header.spacer"),
                validationBadge,
                ActionButton("hns.mask.close", "Done", surface.Close));
            header.Style.Gap = 6f;

            InsightUiStack context = InsightUi.Column("hns.mask.context",
                pageSelector,
                variationSelector,
                InsightUi.Toggle("hns.mask.enabled", "Use this mask")
                    .Bind(() => surface.Enabled, value => surface.Enabled = value),
                DynamicLabel("hns.mask.page-label", () => surface.PageLabel, InsightUiTextStyle.Label),
                originLabel);
            context.Style.Gap = 5f;

            InsightUiElement canvas = InsightUi.Surface("hns.mask.canvas.surface",
                InsightUi.Column("hns.mask.canvas.content", previewSelector,
                    InsightUi.Custom("hns.mask.canvas", DrawCanvas, MeasureCanvas)));
            canvas.Style.Flex = 1f;

            InsightUiElement layers = BuildLayerPanel();
            InsightUiElement controls = BuildControls();
            InsightUiStack content = InsightUi.Row("hns.mask.content", layers,
                InsightUi.Split("hns.mask.canvas.split", canvas, controls, 0.58f));
            content.Style.Flex = 1f;
            editorSplit = InsightUi.Split("hns.mask.editor.split", context, content, 0.22f);
            editorSplit.Draggable = true;
            editorSplit.Style.Flex = 1f;

            InsightUiStack footer = InsightUi.Row("hns.mask.footer", statusLabel,
                InsightUi.Spacer("hns.mask.footer.spacer"),
                ActionButton("hns.mask.undo", "Undo", surface.Undo),
                ActionButton("hns.mask.redo", "Redo", surface.Redo),
                ActionButton("hns.mask.validate", "Validate", surface.Validate));
            footer.Style.Gap = 5f;

            InsightUiStack root = InsightUi.Column("hns.mask.root", header, editorSplit, footer,
                InsightUi.Toast("hns.mask.toast"));
            root.Style.Gap = 7f;
            root.Style.Padding = InsightUiPadding.All(4f);
            return root;
        }

        private InsightUiElement BuildLayerPanel()
        {
            InsightUiElement[] locks = Enumerable.Range(0, 3).Select(index =>
                InsightUi.Toggle("hns.mask.lock." + index, "Lock " + LayerName(index))
                    .Bind(() => surface.GetLayerLocked(index), value => surface.SetLayerLocked(index, value))).ToArray();
            InsightUiStack panel = InsightUi.Column("hns.mask.layers", DynamicLabel("hns.mask.layers.title",
                () => surface.PageLabel + " channels", InsightUiTextStyle.Heading), plantLayerSelector, produceLayerSelector);
            foreach (InsightUiElement toggle in locks) panel.Add(toggle);
            panel.Add(InsightUi.Callout("hns.mask.layers.guidance", InsightUiCalloutSeverity.Info,
                "Semantic channels", "Plant uses Produce, Leaves, and Stem. Produce uses Produce, Leaves, and Container."));
            panel.Add(DynamicLabel("hns.mask.layers.status", () => surface.LayerStatus, InsightUiTextStyle.Caption));
            panel.Style.Gap = 5f;
            panel.Style.Flex = 0.26f;
            return panel;
        }

        private InsightUiElement BuildControls()
        {
            InsightUiElement advanced = InsightUi.Expander("hns.mask.advanced", "Selection and cleanup",
                InsightUi.Column("hns.mask.advanced.body",
                    Slider("hns.mask.selection-radius", "Selection radius", 1f, 16f,
                        () => surface.SelectionRadius, value => surface.SelectionRadius = value),
                    Slider("hns.mask.fragment-limit", "Fragment limit", 1f, 128f,
                        () => surface.FragmentLimit, value => surface.FragmentLimit = value),
                    ActionButton("hns.mask.grow", "Grow", surface.GrowSelection),
                    ActionButton("hns.mask.shrink", "Shrink", surface.ShrinkSelection),
                    ActionButton("hns.mask.smooth", "Smooth", surface.SmoothSelection),
                    ActionButton("hns.mask.feather", "Feather", surface.FeatherSelection),
                    ActionButton("hns.mask.remove-tiny", "Remove tiny", surface.RemoveTinyFragments),
                    ActionButton("hns.mask.fill-holes", "Fill holes", surface.FillSelectionHoles),
                    ActionButton("hns.mask.keep-largest", "Keep largest", surface.KeepLargestSelection),
                    ActionButton("hns.mask.smart-expand", "Smart expand", surface.SmartExpandSelection)), false);

            InsightUiStack controls = InsightUi.Column("hns.mask.controls", toolSelector, paintModeSelector,
                Slider("hns.mask.brush-size", "Brush size", 1f, 25f, () => surface.BrushSize,
                    value => surface.BrushSize = value),
                Slider("hns.mask.tolerance", "Tolerance", 0.01f, 0.5f, () => surface.Tolerance,
                    value => surface.Tolerance = value),
                InsightUi.Callout("hns.mask.region.guidance", InsightUiCalloutSeverity.Info,
                    "Region tool", "Click replaces the selected channel; Shift adds; Ctrl removes."),
                ActionButton("hns.mask.fill-unmasked", "Fill unmasked", surface.FillUnmaskedPixels),
                ActionButton("hns.mask.clear", "Clear selected", surface.ClearSelection),
                ActionButton("hns.mask.copy", "Copy to variation", surface.CopyToVariation),
                ActionButton("hns.mask.project", "Project to variation", surface.ProjectToVariation),
                advanced,
                InsightUi.Callout("hns.mask.validation", InsightUiCalloutSeverity.Info,
                    "Validation", surface.ValidationLabel ?? "Validate to inspect transparency, overlap, fragments, and unmasked pixels."),
                ActionButton("hns.mask.previous", "Previous issue", surface.PreviousIssue),
                ActionButton("hns.mask.next", "Next issue", surface.NextIssue),
                ActionButton("hns.mask.apply-projection", "Apply accepted projection", surface.ApplyProjection),
                ActionButton("hns.mask.cancel-projection", "Cancel projection", surface.CancelProjection),
                ActionButton("hns.mask.regenerate", "Regenerate auto-mask", surface.RegenerateAutoMask),
                ActionButton("hns.mask.reset-auto", "Reset to auto-mask", surface.ResetToAutoMask));
            controls.Style.Gap = 5f;
            controls.Style.Flex = 0.42f;
            return InsightUi.Scroll("hns.mask.controls.scroll", controls);
        }

        private InsightUiElement Slider(string id, string label, float min, float max,
            Func<float> getter, Action<float> setter)
        {
            InsightUiSlider slider = InsightUi.Slider(id + ".slider", getter(), min, max)
                .Bind(getter, setter);
            slider.Style.Flex = 1f;
            return InsightUi.Row(id + ".row", InsightUi.Label(id + ".label", label, InsightUiTextStyle.Label),
                slider, DynamicLabel(id + ".value", () => getter().ToString("0.##"), InsightUiTextStyle.Caption));
        }

        private void DrawCanvas(InsightUiCustomDrawContext context)
        {
            surface.DrawCanvas(new Rect(context.Bounds.X, context.Bounds.Y,
                context.Bounds.Width, context.Bounds.Height));
        }

        private static InsightUiSize MeasureCanvas(InsightUiConstraints constraints, InsightUiFrame frame)
        {
            return new InsightUiSize(Math.Min(720f, constraints.MaxWidth), 520f);
        }

        private bool RefreshPresentation()
        {
            bool changed = false;
            InsightColor desiredColor = surface.ValidationAvailable ? InsightTheme.Default.Negative : InsightTheme.Default.Positive;
            if (validationBadge.Color.HasValue && !validationBadge.Color.Value.Equals(desiredColor)
                || !validationBadge.Color.HasValue)
            {
                validationBadge.Color = desiredColor;
                changed = true;
            }
            string desiredText = surface.ValidationAvailable ? "Needs review" : "Ready";
            if (!string.Equals(validationBadge.Text, desiredText, StringComparison.Ordinal))
            {
                validationBadge.Text = desiredText;
                changed = true;
            }
            bool pageVisible = surface.PageOptions != null && surface.PageOptions.Count > 1;
            bool variationVisible = surface.VariationOptions != null && surface.VariationOptions.Count > 1;
            bool plantVisible = surface.SelectedPage == 0;
            if (pageSelector.Visible != pageVisible)
            {
                pageSelector.Visible = pageVisible;
                changed = true;
            }
            if (variationSelector.Visible != variationVisible)
            {
                variationSelector.Visible = variationVisible;
                changed = true;
            }
            if (plantLayerSelector.Visible != plantVisible)
            {
                plantLayerSelector.Visible = plantVisible;
                changed = true;
            }
            if (produceLayerSelector.Visible == plantVisible)
            {
                produceLayerSelector.Visible = !plantVisible;
                changed = true;
            }
            return changed;
        }

        private string LayerName(int index)
        {
            IReadOnlyList<string> labels = surface.LayerOptions;
            return labels != null && index >= 0 && index < labels.Count ? labels[index] : "Channel " + (index + 1);
        }

        private static string[] SafeOptions(IReadOnlyList<string> values, string fallback)
        {
            return values == null || values.Count == 0 ? new[] { fallback } : values.ToArray();
        }

        private static InsightUiLabel DynamicLabel(string id, Func<string> provider, InsightUiTextStyle style)
        {
            return InsightUi.Label(id, string.Empty, style).SetTextProvider(provider);
        }

        private static InsightUiButton ActionButton(string id, string label, Action action)
        {
            InsightUiButton button = InsightUi.Button(id, label ?? string.Empty, action ?? (() => { }));
            button.Style.HorizontalAlignment = InsightAlignment.Start;
            button.SetTooltip(label ?? string.Empty);
            return button;
        }
    }
}
