using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace HorticultureNovelSeeds
{
    // Small, local drawing helpers for Horticulture windows. No persistent UI authority.
    internal static class NativeUi
    {
        internal static Rect Row(Rect area, ref float y, float height = 30f)
        {
            var rect = new Rect(area.x, y, area.width, height);
            y += height + 4f;
            return rect;
        }
        internal static void Label(Rect area, ref float y, string text, float height = 30f)
        {
            if (!string.IsNullOrEmpty(text)) Widgets.Label(Row(area, ref y, height), text);
        }
        internal static void Button(Rect area, ref float y, string label, Action action)
        {
            if (action != null && !string.IsNullOrEmpty(label) && Widgets.ButtonText(Row(area, ref y), label)) action();
        }
        internal static string Field(Rect area, ref float y, string value) =>
            Widgets.TextField(Row(area, ref y), value ?? "");
        internal static void Toggle(Rect area, ref float y, string label, bool value, Action<bool> change)
        {
            bool next = value;
            Widgets.CheckboxLabeled(Row(area, ref y), label, ref next);
            if (next != value) change(next);
        }
        internal static void Slider(Rect area, ref float y, string label, float value, float min, float max, Action<float> change)
        {
            Label(area, ref y, label + ": " + value.ToString("0.##"), 22);
            float next = Widgets.HorizontalSlider(Row(area, ref y, 20), value, min, max);
            if (!Mathf.Approximately(value, next)) change(next);
        }
        internal static void Choice(Rect area, ref float y, string label, IReadOnlyList<string> options, int selected, Action<int> change)
        {
            if (options == null || options.Count == 0) return;
            int index = Mathf.Clamp(selected, 0, options.Count - 1);
            Button(area, ref y, label + ": " + options[index], () =>
                Find.WindowStack.Add(new FloatMenu(options.Take(1000).Select((name, i) =>
                    new FloatMenuOption(name, () => change(i))).ToList())));
        }
        internal static Rect Scroll(Rect outer, ref Vector2 scroll, float height)
        {
            var view = new Rect(0, 0, Math.Max(1, outer.width - 18), Math.Max(outer.height, height));
            Widgets.BeginScrollView(outer, ref scroll, view);
            return view;
        }
    }
}
