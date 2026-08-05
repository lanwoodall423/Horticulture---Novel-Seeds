using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace HorticultureNovelSeeds
{
    internal static class TraitCatalogRegression
    {
        public static bool Run()
        {
            return GrowthYieldWorkRegression()
                && ClimateAndBlightRegression()
                && PerennialRegression()
                && ResourceRegression()
                && ResourceProductionRegression()
                && SynergyRegression()
                && NutritionRegression()
                && BalanceAndValidationRegression()
                && ExistingDefResolutionRegression();
        }

        private static bool GrowthYieldWorkRegression()
        {
            VarietyTraitDef specialization = new VarietyTraitDef
            {
                defName = "HNS_TestGrowth",
                yieldFactor = 0.90f,
                growthRateFactor = 1.10f,
                sowWorkFactor = 0.85f,
                harvestWorkFactor = 0.85f
            };
            return Mathf.Approximately(NovelSeedUtility.YieldFactor(new[] { specialization }), 0.90f)
                && Mathf.Approximately(NovelSeedUtility.GrowthRateFactor(new[] { specialization }), 1.10f)
                && Mathf.Approximately(ExpandedTraitUtility.SowWorkFactor(new[] { specialization }), 0.85f)
                && Mathf.Approximately(ExpandedTraitUtility.HarvestWorkFactor(new[] { specialization }), 0.85f);
        }

        private static bool ClimateAndBlightRegression()
        {
            VarietyTraitDef climate = new VarietyTraitDef { coldGrowthOffset = -6f, heatGrowthOffset = -3f };
            NovelSeedUtility.TemperatureOffsets(new[] { climate }, out float cold, out float heat);
            VarietyTraitDef disease = new VarietyTraitDef { blightChanceFactor = 0.50f, blightDamageFactor = 1.25f };
            return Mathf.Approximately(cold, -6f) && Mathf.Approximately(heat, -3f)
                && Mathf.Approximately(NovelSeedUtility.BlightChanceFactor(new[] { disease }), 0.50f)
                && Mathf.Approximately(NovelSeedUtility.BlightDamageFactor(new[] { disease }), 1.25f)
                && Mathf.Approximately(ExpandedTraitUtility.ApplyDiseaseResistanceFactor(1f, 0.90f), 1f / 0.90f);
        }

        private static bool PerennialRegression()
        {
            VarietyTraitDef perennial = new VarietyTraitDef
            {
                perennial = true,
                harvestAfterGrowth = 0.30f,
                yieldFactor = 0.70f,
                growthRateFactor = 0.90f,
                perennialColdDormancy = true,
                dormantGrowthFactor = 0.01f
            };
            return Mathf.Approximately(NovelSeedUtility.PerennialHarvestAfterGrowth(new[] { perennial }), 0.30f)
                && !PlantCollected_DropMutationSeed_Patch.EffectiveHarvestDestroys(true, true, 0.30f)
                && PlantCollected_DropMutationSeed_Patch.EffectiveHarvestDestroys(true, true, 0f)
                && Mathf.Approximately(NovelSeedUtility.YieldFactor(new[] { perennial }), 0.70f)
                && Mathf.Approximately(NovelSeedUtility.GrowthRateFactor(new[] { perennial }), 0.90f);
        }

        private static bool ResourceRegression()
        {
            VarietyTraitDef[] resources =
            {
                DefDatabase<VarietyTraitDef>.GetNamedSilentFail("HNS_ResourceDependent_Mulch"),
                DefDatabase<VarietyTraitDef>.GetNamedSilentFail("HNS_ResourceDependent_Hay"),
                DefDatabase<VarietyTraitDef>.GetNamedSilentFail("HNS_ResourceDependent_Fungus")
            };
            return resources.All(resource => resource != null && resource.requiredResourceCount == 1
                && resource.requiredResourceDef != null && Mathf.Approximately(resource.growthRateFactor, 1.15f)
                && Mathf.Approximately(NovelSeedUtility.GrowthRateFactor(new[] { resource }), 1.15f))
                && ResourcePaymentUtility.CanSatisfyStack(1, 1)
                && !ResourcePaymentUtility.CanSatisfyStack(0, 1)
                && ResourcePaymentUtility.CanReserveStack(1, 1, true)
                && !ResourcePaymentUtility.CanReserveStack(1, 1, false)
                && ResourcePaymentUtility.ConsumedUnits(3, 1) == 1
                && 3 - ResourcePaymentUtility.ConsumedUnits(3, 1) == 2
                && ResourcePaymentUtility.ConsumedUnits(0, 1) == 0
                && Mathf.Approximately(NovelSeedUtility.ApplyResourceGrowthGate(1.15f, false), 1.15f)
                && Mathf.Approximately(NovelSeedUtility.ApplyResourceGrowthGate(1.15f, true), 0f);
        }

        private static bool ResourceProductionRegression()
        {
            bool start = ResourcePaymentUtility.CanStartJob(true, 1, false, true, true, true);
            bool unavailable = !ResourcePaymentUtility.CanStartJob(true, 1, false, true, false, false);
            bool plantReservation = !ResourcePaymentUtility.CanStartJob(true, 1, false, false, true, true);
            bool resourceReservation = !ResourcePaymentUtility.CanStartJob(true, 1, false, true, true, false);
            ResourcePaymentUtility.PaymentResult paid = ResourcePaymentUtility.EvaluatePayment(true, 1, 1);
            ResourcePaymentUtility.PaymentResult removed = ResourcePaymentUtility.EvaluatePayment(true, 0, 1);
            ResourcePaymentUtility.PaymentResult retry = ResourcePaymentUtility.EvaluatePayment(true, 1, 1);
            ResourcePaymentUtility.PaymentResult noDoublePayment = ResourcePaymentUtility.EvaluatePayment(false, 1, 1);
            return start && unavailable && plantReservation && resourceReservation
                && paid.Consumed == 1 && paid.FullyPaid
                && removed.Consumed == 0 && !removed.FullyPaid
                && retry.Consumed == 1 && retry.FullyPaid
                && noDoublePayment.Consumed == 0 && !noDoublePayment.FullyPaid
                && Mathf.Approximately(NovelSeedUtility.ApplyResourceGrowthGate(1.15f, false), 1.15f)
                && Mathf.Approximately(NovelSeedUtility.ApplyResourceGrowthGate(1.15f, true), 0f);
        }

        private static bool SynergyRegression()
        {
            VarietyTraitDef synergy = new VarietyTraitDef
            {
                synergyAbsentFactor = 0.90f,
                synergyFactor = 1.15f
            };
            return Mathf.Approximately(ExpandedTraitUtility.SynergyFactorValue(synergy, false), 0.90f)
                && Mathf.Approximately(ExpandedTraitUtility.SynergyFactorValue(synergy, true), 1.15f)
                && Mathf.Approximately(ExpandedTraitUtility.ApplyDiseaseResistanceFactor(1f, 0.90f), 1f / 0.90f)
                && Mathf.Approximately(ExpandedTraitUtility.ApplyDiseaseResistanceFactor(1f, 1.15f), 1f / 1.15f);
        }

        private static bool NutritionRegression()
        {
            VarietyTraitDef nutrition = new VarietyTraitDef
            {
                percentageBonus = 15,
                nutritionFactor = 1.15f,
                harvestWorkFactor = 1.15f
            };
            return Mathf.Approximately(nutrition.nutritionFactor, 1.15f)
                && Mathf.Approximately(nutrition.harvestWorkFactor, 1.15f)
                && Mathf.Approximately(ExpandedTraitUtility.HarvestWorkFactor(new[] { nutrition }), 1.15f);
        }

        private static bool BalanceAndValidationRegression()
        {
            VarietyTraitDef valid = new VarietyTraitDef
            {
                defName = "HNS_TestGenerated",
                positive = true,
                balanceValue = 0f,
                balanceValueExplicit = true,
                configFamily = "TestFamily",
                generated = true,
                growthRateFactor = 1.15f,
                requiredResourceDef = DefDatabase<ThingDef>.GetNamedSilentFail("WoodLog"),
                exclusionTags = new List<string> { "test" }
            };
            VarietyTraitDef root = new VarietyTraitDef
            {
                defName = "HNS_TestRoot",
                configFamily = "TestFamily",
                configRoot = true,
                balanceValue = 0f,
                balanceValueExplicit = true,
                exclusionTags = new List<string> { "test" }
            };
            VarietyTraitDef legacy = new VarietyTraitDef
            {
                defName = "HNS_Legacy",
                positive = true,
                traitTags = new List<string> { "Positive" },
                balanceValue = 0f,
                balanceValueExplicit = false
            };
            List<string> errors = TraitCatalogValidation.Validate(new[] { root, valid });
            bool legacyFallback = Mathf.Approximately(NovelSeedUtility.TraitBalanceValue(legacy), 1f);
            legacy.balanceValueExplicit = true;
            return errors.Count == 0 && legacyFallback
                && Mathf.Approximately(NovelSeedUtility.TraitBalanceValue(legacy), 0f);
        }

        private static bool ExistingDefResolutionRegression()
        {
            VarietyTraitDef giant = DefDatabase<VarietyTraitDef>.GetNamedSilentFail("HNS_Giant");
            VarietyTraitDef perennial = DefDatabase<VarietyTraitDef>.GetNamedSilentFail("HNS_Perennial");
            return giant != null && perennial != null && giant.balanceValueExplicit && perennial.perennial
                && Mathf.Approximately(NovelSeedUtility.TraitBalanceValue(giant), 0f)
                && Mathf.Approximately(NovelSeedUtility.PerennialHarvestAfterGrowth(new[] { perennial }), 0.30f);
        }
    }
}
