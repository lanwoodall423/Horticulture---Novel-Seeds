using System;
using System.Collections.Generic;
using System.Linq;
using InsightCanvas;
using UnityEngine;

namespace HorticultureNovelSeeds
{
    /// <summary>
    /// The small authority bridge used by the Visual Designer document.  The document owns
    /// presentation state; the existing designer owns visual settings, preview textures, and
    /// the specialized plant/produce renderer.
    /// </summary>
    public interface IHorticultureVisualDesignerSurface
    {
        string ContextLabel { get; }
        string TraitLabel { get; }
        string OriginLabel { get; }
        string InheritanceLabel { get; }
        string StatusLabel { get; }
        bool EditingProduce { get; }
        bool CanEdit { get; }
        bool OverrideEnabled { get; set; }
        bool PerMaskEnabled { get; set; }
        int SelectedMask { get; set; }
        IReadOnlyList<string> MaskOptions { get; }
        IReadOnlyList<string> PreviewPlantOptions { get; }
        int SelectedPreviewPlant { get; set; }
        string ActiveSection { get; set; }
        float GetValue(string key);
        void SetValue(string key, float value);
        void SetEditingProduce(bool value);
        void ResetSection(string section);
        void ResetCurrentMask();
        void RestoreInherited();
        void RestoreXmlDefault();
        void DrawPreview(Rect rect);
        void Close();
    }

    /// <summary>
    /// Persistent-in-window presentation state for the Visual Designer.  This is intentionally
    /// independent of the settings authority and embeds Horticulture's renderer through Custom.
    /// </summary>
    public sealed class HorticultureVisualDesignerDocument
    {
        private readonly IHorticultureVisualDesignerSurface surface;
        private readonly InsightUiDocument uiDocument;
        private readonly InsightUiHost uiHost;
        private InsightUiSplit editorSplit;
        private readonly InsightUiSegmented modeSelector;
        private readonly InsightUiSegmented plantMaskSelector;
        private readonly InsightUiSegmented produceMaskSelector;
        private readonly InsightUiTabs sectionTabs;
        private readonly InsightUiSelect previewPlantSelector;
        private readonly InsightUiLabel inheritanceLabel;
        private readonly InsightUiLabel statusLabel;
        private readonly InsightUiBadge originBadge;
        private readonly InsightUiBadge accessibilityBadge;
        private InsightUiDensity density = InsightUiDensity.Normal;
        private bool highContrast;
        private bool reducedMotion;
        private int sectionIndex;
        private InsightUiOrientation splitOrientation = InsightUiOrientation.Horizontal;

        public HorticultureVisualDesignerDocument(IHorticultureVisualDesignerSurface surface)
        {
            this.surface = surface ?? throw new ArgumentNullException(nameof(surface));
            modeSelector = InsightUi.Segmented("hns.visual.mode", new[] { "Plant", "Produce" },
                surface.EditingProduce ? 1 : 0).Bind(
                    () => surface.EditingProduce ? 1 : 0,
                    value => surface.SetEditingProduce(value == 1));
            plantMaskSelector = InsightUi.Segmented("hns.visual.mask-channel.plant", new[] { "Produce", "Leaves", "Stem" },
                Mathf.Clamp(surface.SelectedMask, 0, 2)).Bind(
                    () => Mathf.Clamp(surface.SelectedMask % 3, 0, 2),
                    value => surface.SelectedMask = (surface.EditingProduce ? 3 : 0) + value);
            produceMaskSelector = InsightUi.Segmented("hns.visual.mask-channel.produce", new[] { "Produce", "Leaves", "Container" },
                Mathf.Clamp(surface.SelectedMask - 3, 0, 2)).Bind(
                    () => Mathf.Clamp(surface.SelectedMask - 3, 0, 2),
                    value => surface.SelectedMask = 3 + value);
            sectionTabs = InsightUi.Tabs("hns.visual.sections").Bind(
                () => SectionId(sectionIndex), value =>
                {
                    sectionIndex = SectionIndex(value);
                    surface.ActiveSection = SectionId(sectionIndex);
                });

            string[] previewOptions = (surface.PreviewPlantOptions ?? new string[0]).ToArray();
            previewPlantSelector = InsightUi.Select("hns.visual.preview-plant", "Preview plant",
                previewOptions.Length == 0 ? new[] { "Current plant" } : previewOptions,
                Mathf.Clamp(surface.SelectedPreviewPlant, 0, Math.Max(0, previewOptions.Length - 1)))
                .Bind(() => Mathf.Clamp(surface.SelectedPreviewPlant, 0, Math.Max(0, previewOptions.Length - 1)),
                    value => surface.SelectedPreviewPlant = value);

            inheritanceLabel = DynamicLabel("hns.visual.inheritance", () => surface.InheritanceLabel,
                InsightUiTextStyle.Label);
            statusLabel = DynamicLabel("hns.visual.status", () => surface.StatusLabel,
                InsightUiTextStyle.Caption);
            originBadge = InsightUi.Badge("hns.visual.origin", surface.OriginLabel ?? "Inherited");
            accessibilityBadge = InsightUi.Badge("hns.visual.accessibility", "Accessible controls");

            InsightUiElement root = BuildRoot();
            uiDocument = new InsightUiDocument("hns.visual.designer.document", root)
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

        public string ActiveSectionId => SectionId(sectionIndex);
        public bool IsNarrowWorkspace => splitOrientation == InsightUiOrientation.Vertical;
        public bool HighContrast => highContrast;
        public bool ReducedMotion => reducedMotion;
        public InsightUiDensity Density => density;
        public bool TrackDuplicateIds => uiDocument.TrackDuplicateIds;
        public int DuplicateIdCount => uiDocument.Diagnostics.DuplicateIds;
        public int RenderErrorCount => uiDocument.Diagnostics.RenderErrors;
        public int SectionCount => sectionTabs.Tabs.Count;
        public IReadOnlyList<string> SectionIds => sectionTabs.Tabs.Select(tab => tab.Id).ToArray();
        public IReadOnlyList<string> ModeIds => new[] { "plant", "produce" };
        public IReadOnlyList<string> SemanticMaskChannels => MaskLabels();
        public bool HasIsolatedPresentationState(HorticultureVisualDesignerDocument other)
        {
            return other != null && !ReferenceEquals(uiDocument.State, other.uiDocument.State)
                && !ReferenceEquals(uiDocument.Focus, other.uiDocument.Focus)
                && !ReferenceEquals(uiDocument.Toasts, other.uiDocument.Toasts);
        }

        public void Draw(Rect rect)
        {
            bool presentationChanged = RefreshPresentation();
            InsightUiOrientation nextOrientation = rect.width < 820f
                ? InsightUiOrientation.Vertical
                : InsightUiOrientation.Horizontal;
            if (splitOrientation != nextOrientation)
            {
                splitOrientation = nextOrientation;
                editorSplit.Orientation = splitOrientation;
                presentationChanged = true;
            }
            if (presentationChanged) uiDocument.Invalidate();
            uiHost.Draw(rect, Time.deltaTime);
        }

        public void PostClose()
        {
            uiHost.PostClose();
        }

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

        public void SelectSection(string id)
        {
            sectionIndex = SectionIndex(id);
            uiDocument.Invalidate();
        }

        private InsightUiElement BuildRoot()
        {
            InsightUiStack header = InsightUi.Row("hns.visual.header",
                DynamicLabel("hns.visual.context", () => surface.ContextLabel, InsightUiTextStyle.Heading),
                InsightUi.Spacer("hns.visual.header.spacer"), originBadge,
                ActionButton("hns.visual.restore", "Restore inherited", surface.RestoreInherited),
                ActionButton("hns.visual.done", "Done", surface.Close));
            header.Style.Gap = 6f;

            InsightUiElement context = InsightUi.Column("hns.visual.context-panel",
                DynamicLabel("hns.visual.trait", () => surface.TraitLabel, InsightUiTextStyle.Heading),
                DynamicLabel("hns.visual.origin-label", () => surface.OriginLabel, InsightUiTextStyle.Caption),
                modeSelector, previewPlantSelector, plantMaskSelector, produceMaskSelector);
            context.Style.Gap = 6f;

            InsightUiSurface preview = InsightUi.Surface("hns.visual.preview.surface",
                InsightUi.Custom("hns.visual.preview", DrawPreview, MeasurePreview));
            preview.SetPadding(8f);
            preview.Style.Flex = 1f;

            InsightUiElement inspector = InsightUi.Scroll("hns.visual.inspector.scroll",
                InsightUi.Column("hns.visual.inspector", sectionTabs,
                    InsightUi.Callout("hns.visual.inheritance.callout", InsightUiCalloutSeverity.Info,
                        "Inheritance", "Opening the editor does not create an override. Enable one explicitly."),
                    InsightUi.Toggle("hns.visual.override", "Enable override")
                        .Bind(() => surface.OverrideEnabled, value => surface.OverrideEnabled = value),
                    inheritanceLabel,
                    accessibilityBadge));
            editorSplit = InsightUi.Split("hns.visual.editor.split", preview, inspector, 0.46f);
            editorSplit.Draggable = true;
            editorSplit.Style.Flex = 1f;

            InsightUiStack footer = InsightUi.Row("hns.visual.footer",
                statusLabel,
                InsightUi.Spacer("hns.visual.footer.spacer"),
                ActionButton("hns.visual.reset.color", "Reset Color", () => surface.ResetSection("Color")),
                ActionButton("hns.visual.reset.shape", "Reset Shape", () => surface.ResetSection("Shape")),
                ActionButton("hns.visual.reset.effects", "Reset Effects", () => surface.ResetSection("Effects")),
                ActionButton("hns.visual.reset.mask", "Reset current mask", surface.ResetCurrentMask),
                ActionButton("hns.visual.reset.xml", "Restore XML/default", surface.RestoreXmlDefault));
            footer.Style.Gap = 5f;

            InsightUiStack root = InsightUi.Column("hns.visual.root", header, context, editorSplit, footer,
                InsightUi.Toast("hns.visual.toast"));
            root.Style.Gap = 8f;
            root.Style.Padding = InsightUiPadding.All(4f);

            sectionTabs.Add("color", "Color", BuildColorSection());
            sectionTabs.Add("shape", "Shape", BuildShapeSection());
            sectionTabs.Add("effects", "Effects", BuildEffectsSection());
            return root;
        }

        private InsightUiElement BuildColorSection()
        {
            return InsightUi.Column("hns.visual.color.section",
                Slider("color.red", "Red", 0f, 1f),
                Slider("color.green", "Green", 0f, 1f),
                Slider("color.blue", "Blue", 0f, 1f),
                Slider("color.saturation", "Saturation", 0f, 2f),
                Slider("color.brightness", "Brightness", 0f, 2f),
                InsightUi.Expander("hns.visual.color.advanced", "Advanced color controls",
                    InsightUi.Column("hns.visual.color.advanced.body",
                        Slider("color.hue", "Hue shift", -1f, 1f),
                        Slider("color.contrast", "Contrast", 0f, 2f),
                        Slider("color.opacity", "Opacity", 0f, 1f)), false));
        }

        private InsightUiElement BuildShapeSection()
        {
            return InsightUi.Column("hns.visual.shape.section",
                Slider("shape.scale", "Scale", 0.1f, 3f),
                Slider("shape.width", "Width", 0.1f, 3f),
                Slider("shape.height", "Height", 0.1f, 3f),
                Slider("shape.density", "Density", 0.1f, 3f),
                Slider("shape.spread", "Spread", 0f, 3f),
                InsightUi.Expander("hns.visual.shape.advanced", "Advanced placement",
                    InsightUi.Column("hns.visual.shape.advanced.body",
                        Slider("shape.rotation", "Rotation", -180f, 180f),
                        Slider("shape.offsetX", "Horizontal offset", -2f, 2f),
                        Slider("shape.offsetZ", "Depth offset", -2f, 2f)), false));
        }

        private InsightUiElement BuildEffectsSection()
        {
            return InsightUi.Column("hns.visual.effects.section",
                InsightUi.Toggle("hns.visual.effects.apply", "Apply to harvested produce")
                    .Bind(() => surface.GetValue("effects.apply") > 0.5f,
                        value => surface.SetValue("effects.apply", value ? 1f : 0f)),
                Slider("effects.radiance", "Radiance", 0f, 1f),
                Slider("effects.gloom", "Gloom", 0f, 1f),
                Slider("effects.overlay", "Overlay intensity", 0f, 1f),
                InsightUi.Expander("hns.visual.effects.advanced", "Advanced effects",
                    InsightUi.Column("hns.visual.effects.advanced.body",
                        Slider("effects.radianceScale", "Radiance scale", 0f, 3f),
                        Slider("effects.gloomScale", "Gloom scale", 0f, 3f),
                        InsightUi.Toggle("hns.visual.effects.spikes", "Spikes")
                            .Bind(() => surface.GetValue("effects.spikes") > 0.5f,
                                value => surface.SetValue("effects.spikes", value ? 1f : 0f))), false));
        }

        private InsightUiElement Slider(string key, string label, float minimum, float maximum)
        {
            InsightUiSlider slider = InsightUi.Slider("hns.visual." + key + ".slider", 0f, minimum, maximum)
                .Bind(() => surface.GetValue(key), value => surface.SetValue(key, value));
            slider.Enabled = surface.CanEdit;
            slider.Style.Flex = 1f;
            InsightUiStack row = InsightUi.Row("hns.visual." + key + ".row",
                InsightUi.Label("hns.visual." + key + ".label", label, InsightUiTextStyle.Label), slider,
                DynamicLabel("hns.visual." + key + ".value", () => surface.GetValue(key).ToString("0.##")));
            row.Style.Gap = 5f;
            return row;
        }

        private void DrawPreview(InsightUiCustomDrawContext context)
        {
            surface.DrawPreview(new Rect(context.Bounds.X, context.Bounds.Y, context.Bounds.Width, context.Bounds.Height));
        }

        private static InsightUiSize MeasurePreview(InsightUiConstraints constraints, InsightUiFrame frame)
        {
            return new InsightUiSize(Math.Min(520f, constraints.MaxWidth), 420f);
        }

        private bool RefreshPresentation()
        {
            bool changed = false;
            string origin = surface.OriginLabel ?? "Inherited";
            if (!string.Equals(originBadge.Text, origin, StringComparison.Ordinal))
            {
                originBadge.Text = origin;
                changed = true;
            }
            if (!string.Equals(originBadge.TooltipText, origin, StringComparison.Ordinal))
                originBadge.SetTooltip(origin);

            string accessibility = highContrast ? "High contrast" : reducedMotion ? "Reduced motion" : "Accessible controls";
            if (!string.Equals(accessibilityBadge.Text, accessibility, StringComparison.Ordinal))
            {
                accessibilityBadge.Text = accessibility;
                changed = true;
            }

            bool editingProduce = surface.EditingProduce;
            bool canEdit = surface.CanEdit;
            bool plantVisible = !editingProduce;
            if (plantMaskSelector.Visible != plantVisible)
            {
                plantMaskSelector.Visible = plantVisible;
                changed = true;
            }
            if (produceMaskSelector.Visible == plantVisible)
            {
                produceMaskSelector.Visible = !plantVisible;
                changed = true;
            }
            if (plantMaskSelector.Enabled != canEdit)
            {
                plantMaskSelector.Enabled = canEdit;
                changed = true;
            }
            if (produceMaskSelector.Enabled != canEdit)
            {
                produceMaskSelector.Enabled = canEdit;
                changed = true;
            }
            if (modeSelector.Enabled != canEdit)
            {
                modeSelector.Enabled = canEdit;
                changed = true;
            }
            return changed;
        }

        private string[] MaskLabels()
        {
            IReadOnlyList<string> labels = surface.MaskOptions;
            return labels == null || labels.Count == 0
                ? new[] { "Produce", "Leaves", "Stem" }
                : labels.Take(3).ToArray();
        }

        private static string SectionId(int index)
        {
            return index == 1 ? "shape" : index == 2 ? "effects" : "color";
        }

        private static int SectionIndex(string id)
        {
            return string.Equals(id, "shape", StringComparison.Ordinal) ? 1
                : string.Equals(id, "effects", StringComparison.Ordinal) ? 2 : 0;
        }

        private static InsightUiLabel DynamicLabel(string id, Func<string> provider,
            InsightUiTextStyle style = InsightUiTextStyle.Body)
        {
            return InsightUi.Label(id, string.Empty, style).SetTextProvider(provider);
        }

        private static InsightUiButton ActionButton(string id, string label, Action action)
        {
            InsightUiButton button = InsightUi.Button(id, label, action);
            button.Style.HorizontalAlignment = InsightAlignment.Start;
            button.SetTooltip(label);
            return button;
        }
    }
}
