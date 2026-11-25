using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using UnityEngine;

namespace HorticultureNovelSeeds
{
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

    public sealed class HorticultureMaskEditorDocument
    {
        private readonly IHorticultureMaskEditorSurface surface;
        private Vector2 scroll;
        public HorticultureMaskEditorDocument(IHorticultureMaskEditorSurface surface) { this.surface = surface; }
        public void PostClose() { }
        public void Draw(Rect rect)
        {
            float y = rect.y;
            NativeUi.Label(rect, ref y, surface.Title);
            NativeUi.Choice(rect, ref y, "Page", surface.PageOptions, surface.SelectedPage, i => surface.SelectedPage = i);
            NativeUi.Choice(rect, ref y, "Variation", surface.VariationOptions, surface.SelectedVariation, i => surface.SelectedVariation = i);
            NativeUi.Label(rect, ref y, surface.PageLabel + " ? " + surface.OriginLabel);
            float previewWidth = rect.width * .58f;
            Rect canvas = new Rect(rect.x, y, previewWidth - 8, Math.Max(1, rect.yMax - y - 38));
            surface.DrawCanvas(canvas);
            Rect outer = new Rect(rect.x + previewWidth, y, rect.width - previewWidth, Math.Max(1, rect.yMax - y - 38));
            var view = NativeUi.Scroll(outer, ref scroll, 1850);
            try
            {
                float ry = 0;
                NativeUi.Toggle(view, ref ry, "Use mask", surface.Enabled, v => surface.Enabled = v);
                NativeUi.Choice(view, ref ry, "Channel", surface.LayerOptions, surface.SelectedLayer, i => surface.SelectedLayer = i);
                NativeUi.Label(view, ref ry, surface.LayerStatus, 38);
                for (int i = 0; i < Math.Min(3, surface.LayerOptions.Count); i++) { int n = i; NativeUi.Toggle(view, ref ry, "Lock " + surface.LayerOptions[n], surface.GetLayerLocked(n), v => surface.SetLayerLocked(n, v)); }
                NativeUi.Choice(view, ref ry, "Preview", new[] {"Original", "Mask", "Final"}, surface.PreviewMode, i => surface.PreviewMode = i);
                NativeUi.Choice(view, ref ry, "Tool", new[] {"Brush", "Erase", "Wand", "Move", "Region"}, surface.ToolMode, i => surface.ToolMode = i);
                NativeUi.Choice(view, ref ry, "Selection mode", new[] {"Add", "Remove", "Replace"}, surface.PaintSelectionMode, i => surface.PaintSelectionMode = i);
                NativeUi.Slider(view, ref ry, "Brush size", surface.BrushSize, 1, 25, v => surface.BrushSize = v);
                NativeUi.Slider(view, ref ry, "Tolerance", surface.Tolerance, .01f, .5f, v => surface.Tolerance = v);
                NativeUi.Slider(view, ref ry, "Selection radius", surface.SelectionRadius, 1, 16, v => surface.SelectionRadius = v);
                NativeUi.Slider(view, ref ry, "Fragment limit", surface.FragmentLimit, 1, 128, v => surface.FragmentLimit = v);
                var actions = new (string, Action)[] {
                    ("Grow",surface.GrowSelection),("Shrink",surface.ShrinkSelection),("Smooth",surface.SmoothSelection),("Feather",surface.FeatherSelection),
                    ("Remove tiny",surface.RemoveTinyFragments),("Fill holes",surface.FillSelectionHoles),("Fill unmasked",surface.FillUnmaskedPixels),
                    ("Keep largest",surface.KeepLargestSelection),("Smart expand",surface.SmartExpandSelection),("Clear selection",surface.ClearSelection),
                    ("Validate",surface.Validate),("Previous issue",surface.PreviousIssue),("Next issue",surface.NextIssue),
                    ("Undo",surface.CanUndo ? surface.Undo : null),("Redo",surface.CanRedo ? surface.Redo : null),
                    ("Copy to variation",surface.CopyToVariation),("Project to variation",surface.ProjectToVariation),("Regenerate auto-mask",surface.RegenerateAutoMask),
                    ("Reset to auto-mask",surface.ResetToAutoMask),("Apply projection",surface.ProjectionPreviewActive ? surface.ApplyProjection : null),
                    ("Cancel projection",surface.ProjectionPreviewActive ? surface.CancelProjection : null)
                };
                foreach (var action in actions) NativeUi.Button(view, ref ry, action.Item1, action.Item2);
                NativeUi.Label(view, ref ry, surface.ValidationLabel, 64);
                NativeUi.Label(view, ref ry, surface.StatusLabel, 64);
            }
            finally { Widgets.EndScrollView(); }
            if (Widgets.ButtonText(new Rect(rect.x, rect.yMax - 32, rect.width, 30), "Done")) surface.Close();
        }
    }
}
