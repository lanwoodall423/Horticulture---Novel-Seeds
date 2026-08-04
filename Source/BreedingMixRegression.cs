using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace HorticultureNovelSeeds
{
    internal static class BreedingMixRegression
    {
        private const float ResourceGrowthFactor = 1.15f;
        private const float ResourceGrowthDelta = 0.15f;

        internal static bool Run()
        {
            List<VarietyRecord> mix = OrderedMix("cultivar-a", "cultivar-b");
            VarietyRecord initial = GameComponent_NovelSeeds.SelectBreedingMixVariety(mix, new IntVec3(0, 0, 0));
            VarietyRecord staggered = GameComponent_NovelSeeds.SelectBreedingMixVariety(mix, new IntVec3(0, 0, 0));
            VarietyRecord complete = GameComponent_NovelSeeds.SelectBreedingMixVariety(mix, new IntVec3(1, 0, 0));

            bool initialEmptyField = initial?.id == "cultivar-a" && Aggregate().Count == 0;
            bool partialStaggeredHarvest = staggered?.id == "cultivar-a"
                && Aggregate(Donor("cultivar-b", 1f, 1f)).Count == 1;
            bool completeHarvestReplant = complete?.id == "cultivar-b" && Aggregate().Count == 0;
            return initialEmptyField && partialStaggeredHarvest && completeHarvestReplant
                && EligibilityBoundaries() && AggregationAndSelection() && ResourceEconomics();
        }

        internal static string Report()
        {
            return "initial-empty-field=eligible-donor:false; partial-staggered-harvest-replant=eligible-donor:true; "
                + "complete-harvest-replant=eligible-donor:false; resource-growth=15%; "
                + "mulch=1 WoodLog, 13.0435% maturation-time saved, 6.6667 raw units per +1.0 growth factor; "
                + "hay=1 Hay, 13.0435% maturation-time saved, 6.6667 raw units per +1.0 growth factor; "
                + "fungus=1 RawFungus, 13.0435% maturation-time saved, 6.6667 raw units per +1.0 growth factor; "
                + "market-value-competitive=undetermined-without-resource-prices";
        }

        private static List<VarietyRecord> OrderedMix(params string[] ids)
        {
            return GameComponent_NovelSeeds.OrderBreedingMixVarieties(ids.Select(id => new VarietyRecord
            {
                id = id
            }));
        }

        private static NovelSeedUtility.CrossPollinationDonorCandidate Donor(string id, float growth,
            float distanceSquared, bool spawned = true, bool destroyed = false, bool healthy = true,
            bool sown = true, bool sameThingDef = true, bool distinctCultivar = true,
            bool blighted = false, bool dormant = false, bool ableToGrow = true)
        {
            return NovelSeedUtility.MakeCrossPollinationDonor(new VarietyRecord { id = id },
                new NovelSeedUtility.CrossPollinationDonorState
                {
                    spawned = spawned,
                    destroyed = destroyed,
                    healthy = healthy,
                    sown = sown,
                    sameThingDef = sameThingDef,
                    distinctCultivar = distinctCultivar,
                    blighted = blighted,
                    dormant = dormant,
                    ableToGrow = ableToGrow,
                    growth = growth
                }, distanceSquared, NovelSeedUtility.DefaultMinimumDonorGrowth);
        }

        private static List<NovelSeedUtility.CrossPollinationCultivarCandidate> Aggregate(
            params NovelSeedUtility.CrossPollinationDonorCandidate[] donors)
        {
            return NovelSeedUtility.AggregateCrossPollinationDonors(donors.Where(donor => donor != null).ToList());
        }

        private static bool EligibilityBoundaries()
        {
            return Donor("boundary", 0.50f, 1f) != null
                && Donor("under-boundary", 0.4999f, 1f) == null
                && Donor("blighted", 1f, 1f, blighted: true) == null
                && Donor("dormant", 1f, 1f, dormant: true) == null
                && Donor("unable", 1f, 1f, ableToGrow: false) == null;
        }

        private static bool AggregationAndSelection()
        {
            NovelSeedUtility.CrossPollinationDonorCandidate cultivarBNear = Donor("cultivar-b", 1f, 1f);
            NovelSeedUtility.CrossPollinationDonorCandidate cultivarBFar = Donor("cultivar-b", 1f, 4f);
            NovelSeedUtility.CrossPollinationDonorCandidate cultivarCNear = Donor("cultivar-c", 1f, 0f);
            List<NovelSeedUtility.CrossPollinationCultivarCandidate> aggregates = Aggregate(cultivarBNear,
                cultivarBFar, cultivarCNear);
            NovelSeedUtility.CrossPollinationCultivarCandidate selected =
                NovelSeedUtility.SelectWeightedCrossPollinationDonor(aggregates, 0.20f);
            return aggregates.Count == 2
                && aggregates[0].id == "cultivar-b"
                && Mathf.Abs(aggregates[0].weight - 0.70f) < 0.0001f
                && aggregates[1].id == "cultivar-c"
                && Mathf.Abs(aggregates[1].weight - 1f) < 0.0001f
                && selected?.id == "cultivar-b";
        }

        private static bool ResourceEconomics()
        {
            VarietyTraitDef[] resources =
            {
                DefDatabase<VarietyTraitDef>.GetNamedSilentFail("HNS_ResourceDependent_Mulch"),
                DefDatabase<VarietyTraitDef>.GetNamedSilentFail("HNS_ResourceDependent_Hay"),
                DefDatabase<VarietyTraitDef>.GetNamedSilentFail("HNS_ResourceDependent_Fungus")
            };
            float timeSaved = 1f - 1f / ResourceGrowthFactor;
            return resources.All(resource => resource != null && resource.requiredResourceCount == 1
                && resource.requiredResourceDef != null && Math.Abs(resource.growthRateFactor - ResourceGrowthFactor) < 0.0001f)
                && Math.Abs(timeSaved - 0.13043478f) < 0.0001f
                && Math.Abs(1f / ResourceGrowthDelta - 6.6667f) < 0.01f;
        }
    }
}
