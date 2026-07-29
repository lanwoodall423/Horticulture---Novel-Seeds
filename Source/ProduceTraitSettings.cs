using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace HorticultureNovelSeeds
{
    public static class ProduceTraitEffectUtility
    {
        public static string Summary(VarietyTraitDef root, NovelSeedsSettings settings = null)
        {
            if (root == null) return "No Effect";
            List<VarietyTraitDef> variants = new List<VarietyTraitDef> { root };
            if (root.configFamily == PercentageTraitFactory.NutritiousFamily)
            {
                PercentageTraitFactory.GenerateAll();
                variants.AddRange(DefDatabase<VarietyTraitDef>.AllDefsListForReading
                    .Where(trait => trait.generated && trait.configFamily == PercentageTraitFactory.NutritiousFamily));
            }
            else if (root.configRoot && !root.configFamily.NullOrEmpty()) variants.AddRange(TraitConfigUtility.Types(root.configFamily));

            List<string> effects = new List<string>();
            AddFactorRange(effects, variants, trait => trait.nutritionFactor, "Nutrition");
            AddFactorRange(effects, variants, trait => trait.medicalPotencyFactor, "Medical potency");
            AddFactorRange(effects, variants, trait => trait.produceArmorHeatFactor, "Armor - Heat for materials and apparel");
            AddFactorRange(effects, variants, trait => trait.produceColdInsulationFactor, "Cold insulation for materials and apparel");
            AddFactorRange(effects, variants, trait => trait.produceMaxHitPointsFactor, "Maximum hit points");

            List<float> beauty = variants.Select(trait => trait.produceBeautyOffset)
                .Where(value => !Mathf.Approximately(value, 0f)).Distinct().ToList();
            if (beauty.Count == 1) effects.Add("Beauty " + beauty[0].ToStringWithSign());
            else if (beauty.Count > 1) effects.Add("Beauty varies by subtype");

            List<string> compounds = variants
                .SelectMany(trait => new[] { trait.compoundThought?.LabelCap.ToString(), trait.compoundHediff?.LabelCap.ToString() })
                .Where(label => !label.NullOrEmpty()).Distinct().ToList();
            if (compounds.Count == 1) effects.Add("Eating applies " + compounds[0]);
            else if (compounds.Count > 1) effects.Add("Eating effect varies by subtype");

            if (variants.Any(trait => trait.produceOnlyVisual)) effects.Add("Applies its configured coloration to the Produce mask");
            if (effects.Count == 0) return "No Effect";

            List<string> requiredTags = variants.SelectMany(trait => trait.requiredPlantTags ?? new List<string>())
                .Where(tag => !tag.NullOrEmpty()).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(tag => tag).ToList();
            List<string> anyTags = variants.SelectMany(trait => trait.anyPlantTags ?? new List<string>())
                .Where(tag => !tag.NullOrEmpty()).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(tag => tag).ToList();
            GlobalTraitSettingsRecord record = settings?.GetGlobalTraitSettings(root, false);
            List<string> configuredTags = record?.tagExclusive == true
                ? record.ExclusiveTags.Where(tag => !tag.NullOrEmpty()).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(tag => tag).ToList()
                : new List<string>();
            if (requiredTags.Count > 0) effects.Add("Requires plant tag" + (requiredTags.Count == 1 ? ": " : "s: ") + string.Join(", ", requiredTags));
            if (anyTags.Count > 0) effects.Add("Requires one plant tag from: " + string.Join(", ", anyTags));
            if (record?.tagExclusive == true) effects.Add(configuredTags.Count == 0 ? "Configured tag gate has no tags selected" : "Configured tag gate: " + string.Join(", ", configuredTags));
            return string.Join(".  ", effects) + ".";
        }

        private static void AddFactorRange(List<string> effects, IEnumerable<VarietyTraitDef> traits, Func<VarietyTraitDef, float> selector, string label)
        {
            List<float> values = traits.Select(selector)
                .Select(value => value <= 0f ? 1f : value)
                .Where(value => !Mathf.Approximately(value, 1f))
                .Distinct().ToList();
            if (values.Count == 1) effects.Add(label + ": " + values[0].ToStringPercent() + " of base");
            else if (values.Count > 1) effects.Add(label + " varies by subtype");
        }
    }
}