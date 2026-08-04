using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace HorticultureNovelSeeds
{
    public static class TraitCatalogValidation
    {
        public static List<string> Validate(IEnumerable<VarietyTraitDef> source)
        {
            List<VarietyTraitDef> traits = source?.Where(trait => trait != null).ToList() ?? new List<VarietyTraitDef>();
            List<string> errors = new List<string>();
            foreach (VarietyTraitDef trait in traits)
            {
                if (trait.balanceValueExplicit && !Mathf.Approximately(NovelSeedUtility.TraitBalanceValue(trait), trait.balanceValue))
                    errors.Add(trait.defName + " has an explicit balance value but still uses tag fallback.");
                if (trait.requiredResourceDef != null && trait.requiredResourceCount != 1)
                    errors.Add(trait.defName + " must consume exactly one required resource unit.");
                if (IsCosmetic(trait) || trait.configRoot) continue;
                if (!HasBenefit(trait)) errors.Add(trait.defName + " has no mechanical benefit.");
                if (!HasCostOrRestriction(trait)) errors.Add(trait.defName + " has no mechanical cost or substantial restriction.");
            }

            foreach (VarietyTraitDef trait in traits.Where(value => value.generated))
            {
                VarietyTraitDef root = traits.FirstOrDefault(value => value.configRoot && value.configFamily == trait.configFamily);
                if (root == null)
                {
                    errors.Add(trait.defName + " has no generated-family root.");
                    continue;
                }
                if (root.balanceValueExplicit != trait.balanceValueExplicit || !Mathf.Approximately(root.balanceValue, trait.balanceValue))
                    errors.Add(trait.defName + " does not inherit its root balance rule.");
                if (!ContainsAll(trait.exclusionTags, root.exclusionTags) || !ContainsAll(trait.requiredPlantTags, root.requiredPlantTags))
                    errors.Add(trait.defName + " does not inherit its root exclusion or eligibility tags.");
                if (root.inheritToProduce != trait.inheritToProduce)
                    errors.Add(trait.defName + " does not inherit its root produce behavior.");
                if (root.configFamily == "Synergy")
                {
                    if (!Mathf.Approximately(trait.synergyAbsentFactor, root.synergyAbsentFactor)
                        || !Mathf.Approximately(trait.synergyFactor, root.synergyFactor))
                        errors.Add(trait.defName + " does not inherit synergy present/absent factors.");
                }
                else if (root.configFamily == PercentageTraitFactory.NutritiousFamily)
                {
                    float expectedWork = 1f + trait.percentageBonus / 100f;
                    if (!Mathf.Approximately(trait.nutritionFactor, expectedWork)
                        || !Mathf.Approximately(trait.harvestWorkFactor, expectedWork))
                        errors.Add(trait.defName + " does not inherit the nutrition tradeoff rule.");
                }
            }
            return errors;
        }

        public static bool Run()
        {
            List<string> errors = Validate(DefDatabase<VarietyTraitDef>.AllDefsListForReading);
            foreach (string error in errors) Log.Error("[Horticulture - Novel Seeds] Trait catalog validation: " + error);
            return errors.Count == 0;
        }

        private static bool IsCosmetic(VarietyTraitDef trait)
        {
            return trait != null && (ColorTraitFactory.IsColorFamily(trait.configFamily) || trait.visualMaskIndex >= 0);
        }

        private static bool HasBenefit(VarietyTraitDef trait)
        {
            return (trait.yieldFactor > 1f) || (trait.growthRateFactor > 1f) || (trait.sowWorkFactor > 0f && trait.sowWorkFactor < 1f)
                || (trait.harvestWorkFactor > 0f && trait.harvestWorkFactor < 1f) || trait.blightChanceFactor < 1f
                || trait.blightDamageFactor < 1f || trait.maxHitPointsFactor > 1f || trait.beautyOffset > 0f
                || trait.coldGrowthOffset < 0f || trait.heatGrowthOffset > 0f || trait.sowSkillOffset < 0
                || trait.selfSeeding || trait.fishingYieldFactor > 1f || trait.nutritionFactor > 1f
                || trait.medicalPotencyFactor > 1f || trait.compoundThought != null || trait.compoundHediff != null
                || trait.joyResinThought != null || trait.byproductDef != null || trait.perennial
                || !trait.requiredSowTag.NullOrEmpty() || trait.synergyFactor > 1f || trait.forageNutritionFactor > 1f;
        }

        private static bool HasCostOrRestriction(VarietyTraitDef trait)
        {
            return (trait.yieldFactor > 0f && trait.yieldFactor < 1f) || (trait.growthRateFactor > 0f && trait.growthRateFactor < 1f)
                || trait.sowWorkFactor > 1f || trait.harvestWorkFactor > 1f || trait.blightChanceFactor > 1f
                || trait.blightDamageFactor > 1f || (trait.maxHitPointsFactor > 0f && trait.maxHitPointsFactor < 1f)
                || trait.beautyOffset < 0f || trait.coldGrowthOffset > 0f || trait.heatGrowthOffset < 0f
                || trait.sowSkillOffset > 0 || trait.humongousSpacing || trait.requiredResourceDef != null
                || !trait.requiredSowTag.NullOrEmpty() || trait.tramplingDamage > 0f || trait.resinHediff != null || trait.resinDamage != null
                || trait.thornScratchChance > 0f || trait.forageNutritionFactor > 1f || trait.nutritionFactor < 1f
                || trait.synergyAbsentFactor < 1f;
        }

        private static bool ContainsAll(List<string> actual, List<string> required)
        {
            if (required == null) return true;
            return required.Where(value => !value.NullOrEmpty()).All(value => actual != null && actual.Contains(value));
        }
    }
}
