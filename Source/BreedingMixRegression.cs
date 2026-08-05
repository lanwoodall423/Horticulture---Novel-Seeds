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

        private sealed class HarvestScenario
        {
            public string cultivar;
            public float growth;
            public float distanceSquared;
            public bool harvested;
            public bool healthy = true;
            public bool blighted;
            public bool dormant;
            public bool ableToGrow = true;

            public NovelSeedUtility.CrossPollinationDonorCandidate ProductionDonor()
            {
                return Donor(cultivar, growth, distanceSquared, spawned: !harvested,
                    healthy: healthy, blighted: blighted, dormant: dormant, ableToGrow: ableToGrow);
            }
        }

        internal static bool Run()
        {
            List<VarietyRecord> mix = OrderedMix("cultivar-a", "cultivar-b");
            VarietyRecord initialAssignment = GameComponent_NovelSeeds.SelectBreedingMixVariety(mix, new IntVec3(0, 0, 0));
            VarietyRecord staggeredAssignment = GameComponent_NovelSeeds.SelectBreedingMixVariety(mix, new IntVec3(0, 0, 0));
            VarietyRecord completeAssignment = GameComponent_NovelSeeds.SelectBreedingMixVariety(mix, new IntVec3(1, 0, 0));
            return initialAssignment?.id == "cultivar-a"
                && staggeredAssignment?.id == "cultivar-a"
                && completeAssignment?.id == "cultivar-b"
                && HarvestScenarios() && EligibilityBoundaries() && AggregationAndSelection()
                && DeterministicAssignments(mix) && ResourceEconomics();
        }

        internal static string Report()
        {
            return "initial-empty-field=eligible-donor:false; partial-staggered-harvest-replant=eligible-donor:true; "
                + "complete-harvest-replant=eligible-donor:false; exact-donor-maturity=50%; "
                + "resource-payment=one-unit; fulfilled-growth=1.15x";
        }

        private static bool HarvestScenarios()
        {
            List<HarvestScenario> empty = new List<HarvestScenario>();
            List<HarvestScenario> staggered = new List<HarvestScenario>
            {
                new HarvestScenario { cultivar = "cultivar-a", growth = 1f, distanceSquared = 0f, harvested = true },
                new HarvestScenario { cultivar = "cultivar-b", growth = 0.50f, distanceSquared = 1f }
            };
            List<HarvestScenario> complete = new List<HarvestScenario>
            {
                new HarvestScenario { cultivar = "cultivar-a", growth = 1f, harvested = true },
                new HarvestScenario { cultivar = "cultivar-b", growth = 1f, harvested = true }
            };
            List<HarvestScenario> blocked = new List<HarvestScenario>
            {
                new HarvestScenario { cultivar = "blighted", growth = 1f, blighted = true },
                new HarvestScenario { cultivar = "dormant", growth = 1f, dormant = true }
            };
            List<NovelSeedUtility.CrossPollinationDonorCandidate> emptyDonors = ProductionDonors(empty);
            List<NovelSeedUtility.CrossPollinationDonorCandidate> staggeredDonors = ProductionDonors(staggered);
            List<NovelSeedUtility.CrossPollinationDonorCandidate> completeDonors = ProductionDonors(complete);
            List<NovelSeedUtility.CrossPollinationDonorCandidate> blockedDonors = ProductionDonors(blocked);
            return emptyDonors.Count == 0
                && staggeredDonors.Count == 1 && staggeredDonors[0].variety.id == "cultivar-b"
                && Mathf.Approximately(staggeredDonors[0].weight, 0.25f)
                && completeDonors.Count == 0 && blockedDonors.Count == 0;
        }

        private static List<NovelSeedUtility.CrossPollinationDonorCandidate> ProductionDonors(
            IEnumerable<HarvestScenario> scenarios)
        {
            return (scenarios ?? Enumerable.Empty<HarvestScenario>())
                .Select(scenario => scenario.ProductionDonor()).Where(donor => donor != null).ToList();
        }

        private static bool DeterministicAssignments(List<VarietyRecord> mix)
        {
            VarietyRecord first = GameComponent_NovelSeeds.SelectBreedingMixVariety(mix, new IntVec3(17, 0, 23));
            VarietyRecord second = GameComponent_NovelSeeds.SelectBreedingMixVariety(mix, new IntVec3(17, 0, 23));
            List<VarietyRecord> reversed = GameComponent_NovelSeeds.OrderBreedingMixVarieties(mix.AsEnumerable().Reverse());
            VarietyRecord reordered = GameComponent_NovelSeeds.SelectBreedingMixVariety(reversed, new IntVec3(17, 0, 23));
            return first?.id == second?.id && first?.id == reordered?.id;
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
