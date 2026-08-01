using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace HorticultureNovelSeeds
{
    public static class TraitColorUI
    {
        private const string SwatchGlyph = "\u25a0";

        public static bool HasSwatch(VarietyTraitDef trait)
        {
            return trait != null && !trait.configRoot && trait.visualMaskIndex >= 0
                && ColorTraitFactory.IsColorFamily(trait.configFamily);
        }

        public static Color InheritedColor(VarietyTraitDef trait)
        {
            return trait == null ? Color.white : new Color(
                Mathf.Clamp01(trait.tintRed), Mathf.Clamp01(trait.tintGreen), Mathf.Clamp01(trait.tintBlue), 1f);
        }

        public static string Swatch(VarietyTraitDef trait)
        {
            return HasSwatch(trait) ? Swatch(InheritedColor(trait)) : string.Empty;
        }

        public static string Swatch(Color color)
        {
            Color32 value = color;
            string hex = value.r.ToString("X2") + value.g.ToString("X2") + value.b.ToString("X2");
            return "<color=#" + hex + ">" + SwatchGlyph + "</color>";
        }

        public static string Label(VarietyTraitDef trait)
        {
            if (trait == null) return string.Empty;
            string label = trait.LabelCap.ToString();
            string swatch = Swatch(trait);
            return swatch.NullOrEmpty() ? label : swatch + " " + label;
        }

        public static string Description(VarietyTraitDef trait)
        {
            if (trait == null) return string.Empty;
            string description = trait.description ?? string.Empty;
            string swatch = Swatch(trait);
            return swatch.NullOrEmpty() ? description : swatch + " " + description;
        }

        public static string Tooltip(VarietyTraitDef trait)
        {
            if (trait == null) return string.Empty;
            string description = trait.description ?? string.Empty;
            return description.NullOrEmpty() ? Label(trait) : Label(trait) + "\n\n" + description;
        }

        public static string Summary(IEnumerable<VarietyTraitDef> traits)
        {
            return traits == null ? string.Empty : string.Join(", ", traits.Where(trait => trait != null).Select(Label).ToArray());
        }
    }
}
