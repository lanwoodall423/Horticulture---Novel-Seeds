using System.Collections.Generic;
using UnityEngine;

namespace HorticultureNovelSeeds
{
    internal static class CrossPollinationRegression
    {
        internal static bool Run()
        {
            return DonorEligibilityRegression()
                && DonorWeightRegression()
                && FixedChanceRegression()
                && SlotGateRegression()
                && CosmeticBudgetRegression()
                && SettingsRegression();
        }

        private static bool DonorEligibilityRegression()
        {
            NovelSeedUtility.CrossPollinationDonorState established = new NovelSeedUtility.CrossPollinationDonorState
            {
                spawned = true,
                healthy = true,
                sown = true,
                sameThingDef = true,
                distinctCultivar = true,
                growth = 0.5f,
                ableToGrow = true
            };
            if (!NovelSeedUtility.IsEligibleCrossPollinationDonor(established, 0.5f)) return false;

            NovelSeedUtility.CrossPollinationDonorState immature = established;
            immature.growth = 0f;
            if (NovelSeedUtility.IsEligibleCrossPollinationDonor(immature, 0f)) return false;
            immature.growth = 0.499999f;
            if (NovelSeedUtility.IsEligibleCrossPollinationDonor(immature, 0.5f)) return false;
            immature.growth = 0.500001f;
            if (!NovelSeedUtility.IsEligibleCrossPollinationDonor(immature, 0.5f)) return false;

            NovelSeedUtility.CrossPollinationDonorState blighted = established;
            blighted.blighted = true;
            NovelSeedUtility.CrossPollinationDonorState dormant = established;
            dormant.dormant = true;
            NovelSeedUtility.CrossPollinationDonorState sameCultivar = established;
            sameCultivar.distinctCultivar = false;
            NovelSeedUtility.CrossPollinationDonorState unavailable = established;
            unavailable.spawned = false;
            unavailable.destroyed = true;
            unavailable.healthy = false;
            unavailable.sown = false;
            unavailable.sameThingDef = false;
            unavailable.ableToGrow = false;
            return !NovelSeedUtility.IsEligibleCrossPollinationDonor(blighted, 0.5f)
                && !NovelSeedUtility.IsEligibleCrossPollinationDonor(dormant, 0.5f)
                && !NovelSeedUtility.IsEligibleCrossPollinationDonor(sameCultivar, 0.5f)
                && !NovelSeedUtility.IsEligibleCrossPollinationDonor(unavailable, 0.5f);
        }

        private static bool DonorWeightRegression()
        {
            if (!(NovelSeedUtility.CrossPollinationDonorWeight(1f, 0.5f)
                > NovelSeedUtility.CrossPollinationDonorWeight(4f, 0.5f))) return false;
            if (!(NovelSeedUtility.CrossPollinationDonorWeight(1f, 1f)
                > NovelSeedUtility.CrossPollinationDonorWeight(1f, 0.5f))) return false;

            VarietyRecord first = new VarietyRecord { id = "cultivar-a" };
            VarietyRecord second = new VarietyRecord { id = "cultivar-b" };
            List<NovelSeedUtility.CrossPollinationDonorCandidate> samples = new List<NovelSeedUtility.CrossPollinationDonorCandidate>
            {
                new NovelSeedUtility.CrossPollinationDonorCandidate { variety = second, weight = 0.1f },
                new NovelSeedUtility.CrossPollinationDonorCandidate { variety = first, weight = 0.2f },
                new NovelSeedUtility.CrossPollinationDonorCandidate { variety = second, weight = 0.3f }
            };
            List<NovelSeedUtility.CrossPollinationCultivarCandidate> aggregate = NovelSeedUtility.AggregateCrossPollinationDonors(samples);
            return aggregate.Count == 2
                && aggregate[0].id == "cultivar-a"
                && aggregate[1].id == "cultivar-b"
                && Mathf.Approximately(aggregate[0].weight, 0.2f)
                && Mathf.Approximately(aggregate[1].weight, 0.4f);
        }

        private static bool FixedChanceRegression()
        {
            return NovelSeedUtility.CrossPollinationChancePasses(0.006f, 0.007f, 1)
                && NovelSeedUtility.CrossPollinationChancePasses(0.006f, 0.007f, 8)
                && !NovelSeedUtility.CrossPollinationChancePasses(0.007f, 0.007f, 1)
                && !NovelSeedUtility.CrossPollinationChancePasses(0.007f, 0.007f, 8)
                && !NovelSeedUtility.CrossPollinationChancePasses(0.006f, 0.007f, 0);
        }

        private static bool SlotGateRegression()
        {
            const float second = NovelSeedUtility.DefaultSecondCrossPollinationTraitChance;
            const float later = NovelSeedUtility.DefaultLaterCrossPollinationTraitChance;
            return NovelSeedUtility.CrossPollinationSlotPasses(0, 1, 1f, second, later)
                && !NovelSeedUtility.CrossPollinationSlotPasses(1, 1, 0f, second, later)
                && NovelSeedUtility.CrossPollinationSlotPasses(1, 2, 0.099f, second, later)
                && !NovelSeedUtility.CrossPollinationSlotPasses(1, 2, 0.1f, second, later)
                && NovelSeedUtility.CrossPollinationSlotPasses(2, 3, 0.009f, second, later)
                && !NovelSeedUtility.CrossPollinationSlotPasses(2, 3, 0.01f, second, later)
                && NovelSeedUtility.CrossPollinationSlotPasses(3, 4, 0.009f, second, later)
                && !NovelSeedUtility.CrossPollinationSlotPasses(3, 4, 0.01f, second, later)
                && NovelSeedUtility.CrossPollinationSlotPasses(9, 10, 0f, second, later)
                && !NovelSeedUtility.CrossPollinationSlotPasses(10, 10, 0f, second, later);
        }

        private static bool CosmeticBudgetRegression()
        {
            VarietyTraitDef color = new VarietyTraitDef { configFamily = "ProduceColor" };
            VarietyTraitDef nutrition = new VarietyTraitDef { configFamily = PercentageTraitFactory.NutritiousFamily };
            VarietyTraitDef ordinary = new VarietyTraitDef { configFamily = "HNS_RegressionMechanical" };
            return NovelSeedUtility.IsCosmeticCrossPollinationTrait(color)
                && !NovelSeedUtility.IsCosmeticCrossPollinationTrait(nutrition)
                && NovelSeedUtility.MechanicalCrossPollinationTraitCount(new[] { nutrition }) == 1
                && NovelSeedUtility.MechanicalCrossPollinationTraitCount(new[] { color }) == 0
                && NovelSeedUtility.MechanicalCrossPollinationTraitCount(new[] { nutrition, color, ordinary }) == 2
                && !NovelSeedUtility.CrossPollinationSlotPasses(1, 1, 0f,
                    NovelSeedUtility.DefaultSecondCrossPollinationTraitChance,
                    NovelSeedUtility.DefaultLaterCrossPollinationTraitChance)
                && NovelSeedUtility.CrossPollinationSlotPasses(1, 2, 0.099f,
                    NovelSeedUtility.DefaultSecondCrossPollinationTraitChance,
                    NovelSeedUtility.DefaultLaterCrossPollinationTraitChance);
        }

        private static bool SettingsRegression()
        {
            NovelSeedsSettings defaults = new NovelSeedsSettings();
            if (!Mathf.Approximately(defaults.globalCrossPollinationChance, NovelSeedUtility.DefaultCrossPollinationChance)
                || !Mathf.Approximately(defaults.minimumDonorGrowth, NovelSeedUtility.DefaultMinimumDonorGrowth)
                || !Mathf.Approximately(defaults.secondCrossPollinationTraitChance, NovelSeedUtility.DefaultSecondCrossPollinationTraitChance)
                || !Mathf.Approximately(defaults.laterCrossPollinationTraitChance, NovelSeedUtility.DefaultLaterCrossPollinationTraitChance)) return false;

            NovelSeedsSettings legacy = new NovelSeedsSettings
            {
                globalCrossPollinationChance = 0.123f,
                maxCrossPollinationTraits = 2,
                minimumDonorGrowth = 0.64f,
                secondCrossPollinationTraitChance = 0.23f,
                laterCrossPollinationTraitChance = 0.04f
            };
            NovelSeedsSettings applied = new NovelSeedsSettings();
            applied.ApplyFrom(legacy);
            return Mathf.Approximately(applied.globalCrossPollinationChance, 0.123f)
                && applied.maxCrossPollinationTraits == 2
                && Mathf.Approximately(applied.minimumDonorGrowth, 0.64f)
                && Mathf.Approximately(applied.secondCrossPollinationTraitChance, 0.23f)
                && Mathf.Approximately(applied.laterCrossPollinationTraitChance, 0.04f);
        }
    }
}
