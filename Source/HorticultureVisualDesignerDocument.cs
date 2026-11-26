using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using UnityEngine;

namespace HorticultureNovelSeeds
{
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

    public sealed class HorticultureVisualDesignerDocument
    {
        private readonly IHorticultureVisualDesignerSurface surface;
        private Vector2 scroll;
        private string section = "color";
        public HorticultureVisualDesignerDocument(IHorticultureVisualDesignerSurface surface) { this.surface = surface; }
        public void PostClose() { }
        public string ActiveSectionId => section;
        public IReadOnlyList<string> SectionIds => new[] { "color", "shape", "effects" };
        public IReadOnlyList<string> SemanticMaskChannels => surface.MaskOptions;
        public void SelectSection(string id) { if (SectionIds.Contains(id)) { section = id; surface.ActiveSection = id; scroll = Vector2.zero; } }
        public void Draw(Rect rect)
        {
            float y = rect.y;
            NativeUi.Label(rect, ref y, surface.TraitLabel + " ? " + surface.ContextLabel);
            NativeUi.Choice(rect, ref y, "Mode", new[] { "Plant", "Produce" }, surface.EditingProduce ? 1 : 0, i => surface.SetEditingProduce(i == 1));
            NativeUi.Choice(rect, ref y, "Preview plant", surface.PreviewPlantOptions, surface.SelectedPreviewPlant, i => surface.SelectedPreviewPlant = i);
            NativeUi.Label(rect, ref y, surface.OriginLabel + " ? " + surface.InheritanceLabel, 44);
            float previewWidth = rect.width * .45f;
            surface.DrawPreview(new Rect(rect.x, y, previewWidth - 6, Math.Max(1, rect.yMax - y - 38)));
            var outer = new Rect(rect.x + previewWidth + 6, y, rect.width - previewWidth - 6, Math.Max(1, rect.yMax - y - 38));
            var view = NativeUi.Scroll(outer, ref scroll, 1030);
            try
            {
                float ry = 0;
                NativeUi.Toggle(view, ref ry, "Enable override", surface.OverrideEnabled, v => surface.OverrideEnabled = v);
                bool previous = GUI.enabled;
                try
                {
                    GUI.enabled = previous && surface.CanEdit;
                    if (!surface.EditingProduce) NativeUi.Toggle(view, ref ry, "Regional color effects", surface.PerMaskEnabled, v => surface.PerMaskEnabled = v);
                    if (surface.PerMaskEnabled && !surface.EditingProduce)
                    {
                        int offset = surface.EditingProduce ? 3 : 0;
                        string[] labels = new[] { "Produce", "Leaves", "Stem" };
                        NativeUi.Choice(view, ref ry, "Channel", labels, surface.SelectedMask - offset, i => surface.SelectedMask = i + offset);
                    }
                    NativeUi.Choice(view, ref ry, "Section", SectionIds, Array.IndexOf(SectionIds.ToArray(), section), i => SelectSection(SectionIds[i]));
                    if (section == "color")
                    {
                        Slider(view, ref ry, "color.red", "Red", 0, 1); Slider(view, ref ry, "color.green", "Green", 0, 1); Slider(view, ref ry, "color.blue", "Blue", 0, 1);
                        Slider(view, ref ry, "color.saturation", "Saturation", 0, 2); Slider(view, ref ry, "color.brightness", "Brightness", 0, 2);
                        Slider(view, ref ry, "color.hue", "Hue", -1, 1); Slider(view, ref ry, "color.contrast", "Contrast", 0, 2); Slider(view, ref ry, "color.opacity", "Opacity", 0, 1);
                    }
                    else if (section == "shape")
                    {
                        Slider(view, ref ry, "shape.scale", "Scale", .1f, 3); Slider(view, ref ry, "shape.width", "Width", .1f, 3); Slider(view, ref ry, "shape.height", "Height", .1f, 3);
                        Slider(view, ref ry, "shape.density", "Density", .1f, 3); Slider(view, ref ry, "shape.spread", "Spread", 0, 3);
                        Slider(view, ref ry, "shape.rotation", "Rotation", -180, 180); Slider(view, ref ry, "shape.offsetX", "Horizontal offset", -2, 2); Slider(view, ref ry, "shape.offsetZ", "Depth offset", -2, 2);
                    }
                    else
                    {
                        NativeUi.Toggle(view, ref ry, "Apply to produce", surface.GetValue("effects.apply") > .5f, v => surface.SetValue("effects.apply", v ? 1 : 0));
                        Slider(view, ref ry, "effects.radiance", "Radiance", 0, 1); Slider(view, ref ry, "effects.gloom", "Gloom", 0, 1); Slider(view, ref ry, "effects.overlay", "Overlay intensity", 0, 1);
                        Slider(view, ref ry, "effects.radianceScale", "Radiance scale", 0, 3); Slider(view, ref ry, "effects.gloomScale", "Gloom scale", 0, 3);
                        NativeUi.Toggle(view, ref ry, "Spikes", surface.GetValue("effects.spikes") > .5f, v => surface.SetValue("effects.spikes", v ? 1 : 0));
                    }
                    NativeUi.Button(view, ref ry, "Reset current section", () => surface.ResetSection(section));
                    if (!surface.EditingProduce && surface.PerMaskEnabled) NativeUi.Button(view, ref ry, "Reset current region", surface.ResetCurrentMask);
                    NativeUi.Button(view, ref ry, "Restore XML/default", surface.RestoreXmlDefault);
                }
                finally { GUI.enabled = previous; }
                NativeUi.Button(view, ref ry, "Restore inherited", surface.RestoreInherited);
                NativeUi.Label(view, ref ry, surface.StatusLabel, 48);
            }
            finally { Widgets.EndScrollView(); }
            if (Widgets.ButtonText(new Rect(rect.x, rect.yMax - 32, rect.width, 30), "Done")) surface.Close();
        }
        private void Slider(Rect rect, ref float y, string key, string label, float min, float max) =>
            NativeUi.Slider(rect, ref y, label, surface.GetValue(key), min, max, v => surface.SetValue(key, v));
    }
}
