using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using KnowledgeFramework;
using RimWorld;
using UnityEngine;
using Verse;

namespace HorticultureNovelSeeds
{
    /// <summary>Turns completed game events into bounded cultivation observations.</summary>
    public static class HorticultureEventRouter
    {
        public static void SowingCompleted(Pawn grower, Plant plant)
        {
            if (plant == null) return;
            if (Observe(grower, plant, HorticultureKnowledgeEvent.Sowing, "The plant was successfully sown.",
                HorticultureKnowledgeEventIdentity.Sowing(plant)))
                GerminationStateFor(plant).ResetIfNeeded(plant);
        }

        public static void GrowthObserved(Pawn observer, Plant plant, string sourceInstanceId = null)
        {
            if (plant == null || plant.Growth <= 0f) return;
            GrowthObservationState state = GerminationStateFor(plant);
            if (!state.germinated && plant.Growth >= 0.05f)
            {
                if (!Observe(observer, plant, HorticultureKnowledgeEvent.Germination, "Germination was observed.",
                    sourceInstanceId == null ? HorticultureKnowledgeEventIdentity.Germination(plant) :
                        HorticultureKnowledgeEventIdentity.Normalize("germination", sourceInstanceId))) return;
                state.germinated = true;
                return;
            }
            int bucket = Mathf.Clamp(Mathf.FloorToInt(plant.Growth * 4f), 0, 3);
            if (bucket <= state.lastGrowthBucket) return;
            if (Observe(observer, plant, HorticultureKnowledgeEvent.GrowthStage, "A new growth stage was observed.",
                sourceInstanceId == null ? HorticultureKnowledgeEventIdentity.Growth(plant, bucket) :
                    HorticultureKnowledgeEventIdentity.Normalize("growth", sourceInstanceId + ":" + bucket)))
                state.lastGrowthBucket = bucket;
        }

        public static void EnvironmentalStressObserved(Pawn observer, Plant plant, float temperature, bool cold, bool survived)
        {
            if (plant == null) return;
            List<KnowledgeMeasurement> measurements = new List<KnowledgeMeasurement>
            {
                new KnowledgeMeasurement
                {
                    facetId = HorticultureKnowledgeAdapter.FacetClimate,
                    claimId = "temperature_range",
                    value = KnowledgeClaimValue.Float(temperature),
                    quality = 1f,
                    evidenceWeight = 1f,
                    confidenceFactor = survived ? 0.9f : 0.65f,
                    summary = (cold ? "Cold" : "Heat") + " stress at " + temperature.ToString("0.#") + " C."
                },
                new KnowledgeMeasurement
                {
                    facetId = HorticultureKnowledgeAdapter.FacetClimate,
                    claimId = "environmental_response",
                    value = KnowledgeClaimValue.Text(KnowledgeClaimValueType.EnumId,
                        survived ? (cold ? "cold-survived" : "heat-survived") : (cold ? "cold-failed" : "heat-failed")),
                    quality = 1f,
                    evidenceWeight = 1f,
                    confidenceFactor = 0.8f,
                    summary = "Observed environmental response."
                }
            };
            Observe(observer, plant, survived ? HorticultureKnowledgeEvent.DiseaseSurvival : HorticultureKnowledgeEvent.EnvironmentalStress,
                survived ? "The plant survived an environmental extreme." : "The plant showed serious environmental stress.",
                HorticultureKnowledgeEventIdentity.EnvironmentalStress(plant, temperature, cold, survived), measurements, !survived);
        }

        public static void FertilizationCompleted(Pawn grower, Plant plant, string sourceInstanceId = null)
        {
            if (plant == null) return;
            Observe(grower, plant, HorticultureKnowledgeEvent.Fertilization, "Fertilization was successfully applied.",
                sourceInstanceId ?? HorticultureKnowledgeEventIdentity.Fertilization(plant,
                    plant.TryGetComp<CompPlantVariety>()?.ResourceCycle ?? 0));
        }

        public static void DiseaseSurvivalObserved(Pawn observer, Plant plant)
        {
            if (plant == null) return;
            Observe(observer, plant, HorticultureKnowledgeEvent.DiseaseSurvival, "The plant survived disease pressure.",
                HorticultureKnowledgeEventIdentity.DiseaseSurvival(plant));
        }

        public static void HarvestCompleted(Pawn harvester, Plant plant, int yield, bool success = true,
            bool repeated = false, bool multiSeason = false, string sourceInstanceId = null)
        {
            if (plant == null) return;
            HorticultureKnowledgeEvent eventKind = !success ? HorticultureKnowledgeEvent.FailedHarvest :
                multiSeason ? HorticultureKnowledgeEvent.MultiSeasonStability : repeated ? HorticultureKnowledgeEvent.RepeatedHarvest :
                HorticultureKnowledgeEvent.Harvest;
            List<KnowledgeMeasurement> measurements = new List<KnowledgeMeasurement>();
            if (success)
            {
                measurements.Add(new KnowledgeMeasurement
                {
                    facetId = HorticultureKnowledgeAdapter.FacetYield,
                    claimId = "yield_range",
                    value = KnowledgeClaimValue.Float(Mathf.Max(0, yield)),
                    quality = repeated || multiSeason ? 1.2f : 1f,
                    evidenceWeight = 1f,
                    confidenceFactor = 1f,
                    summary = "Observed harvest yield: " + yield
                });
                if (plant.def.plant.harvestedThingDef != null)
                    measurements.Add(new KnowledgeMeasurement
                    {
                        facetId = HorticultureKnowledgeAdapter.FacetProduce,
                        claimId = "produce_identity",
                        value = KnowledgeClaimValue.Text(KnowledgeClaimValueType.DefReference, plant.def.plant.harvestedThingDef.defName),
                        summary = "Observed harvested produce."
                    });
                measurements.Add(new KnowledgeMeasurement
                {
                    facetId = HorticultureKnowledgeAdapter.FacetLifespan,
                    claimId = "harvest_cycles",
                    value = KnowledgeClaimValue.Integer(repeated || multiSeason ? 2 : 1),
                    summary = "Observed harvest cycle."
                });
            }
            Observe(harvester, plant, eventKind, success ? "A successful harvest provided evidence." :
                "A failed harvest revealed a cultivation limit.", sourceInstanceId ?? HorticultureKnowledgeEventIdentity.Harvest(plant,
                    plant.TryGetComp<CompPlantVariety>()?.HarvestCycle ?? 0, success, repeated, multiSeason), measurements, !success);
        }

        public static void CuttingCompleted(Pawn cutter, Plant plant, int yield = 0, string sourceInstanceId = null)
        {
            if (plant == null) return;
            List<KnowledgeMeasurement> measurements = new List<KnowledgeMeasurement>
            {
                new KnowledgeMeasurement
                {
                    facetId = HorticultureKnowledgeAdapter.FacetYield,
                    claimId = "yield_range",
                    value = KnowledgeClaimValue.Float(Mathf.Max(0, yield)),
                    summary = "Observed wood or tree-product yield from cutting."
                },
                new KnowledgeMeasurement
                {
                    facetId = HorticultureKnowledgeAdapter.FacetProduce,
                    claimId = "produce_identity",
                    value = KnowledgeClaimValue.Text(KnowledgeClaimValueType.DefReference,
                        plant.def.plant.harvestedThingDef?.defName ?? plant.def.defName),
                    summary = "Observed tree product identity."
                }
            };
            Observe(cutter, plant, HorticultureKnowledgeEvent.Cutting,
                "A supported plant was cut and its product was observed.", sourceInstanceId ??
                HorticultureKnowledgeEventIdentity.Cutting(plant, plant.TryGetComp<CompPlantVariety>()?.HarvestCycle ?? 0), measurements);
        }

        public static void ProduceProcessed(Pawn worker, IEnumerable<Thing> ingredients)
        {
            List<Thing> source = (ingredients ?? Enumerable.Empty<Thing>()).Where(value => value != null).ToList();
            foreach (ThingDef crop in source.Select(value => value.TryGetComp<CompNovelProduceAppearance>()?.SourcePlantDef)
                .Where(value => value != null).Distinct())
            {
                HorticultureKnowledgeAdapter.Observe(worker, crop, HorticultureKnowledgeEvent.ProduceProcessing,
                    map: worker?.MapHeld, success: true, quality: 1f,
                    summary: "Produce processing preserved evidence about the harvested qualities.",
                    sourceInstanceId: HorticultureKnowledgeEventIdentity.Processing(worker, source, crop),
                    role: HorticultureKnowledgeRole.Cook,
                    context: HorticultureKnowledgeAdapter.ContextFor(worker?.MapHeld),
                    witnesses: HorticultureWitnesses(worker, worker?.MapHeld));
            }
        }

        public static bool NovelSeedDiscovered(Pawn discoverer, ThingDef crop, IEnumerable<VarietyTraitDef> traits,
            string origin, Map map, IEnumerable<string> parentIds = null, string sourceInstanceId = null,
            int harvestYield = -1, bool repeatedHarvest = false, bool multiSeason = false)
        {
            if (crop == null) return false;
            HorticultureKnowledgeEvent eventKind = harvestYield >= 0 ? HorticultureKnowledgeEvent.HarvestDiscovery :
                origin == "wild" ? HorticultureKnowledgeEvent.WildDiscovery :
                origin == "cross-pollination" ? HorticultureKnowledgeEvent.CrossPollination : HorticultureKnowledgeEvent.MutationDiscovery;
            List<VarietyTraitDef> traitList = (traits ?? Enumerable.Empty<VarietyTraitDef>()).Where(value => value != null).ToList();
            List<string> parentList = (parentIds ?? Enumerable.Empty<string>()).Where(value => !value.NullOrEmpty()).Distinct().ToList();
            List<string> traitIds = traitList
                .Select(value => value.defName).Distinct().ToList();
            List<KnowledgeMeasurement> measurements = new List<KnowledgeMeasurement>
            {
                new KnowledgeMeasurement
                {
                    facetId = HorticultureKnowledgeAdapter.FacetTraits,
                    claimId = "trait_identity",
                    value = KnowledgeClaimValue.Set(traitIds),
                    summary = "Visible traits of a newly discovered plant."
                },
                new KnowledgeMeasurement
                {
                    facetId = HorticultureKnowledgeAdapter.FacetTraits,
                    claimId = "trait_expression",
                    value = KnowledgeClaimValue.Set(traitIds),
                    summary = "Traits were observed on the living plant."
                }
            };
            if (parentList.Count > 0)
            {
                measurements.Add(new KnowledgeMeasurement
                {
                    facetId = HorticultureKnowledgeAdapter.FacetLineage,
                    claimId = "parent_lineage",
                    value = KnowledgeClaimValue.Set(parentList),
                    summary = "Parent cultivars were observed with the discovery."
                });
            }
            if (harvestYield >= 0)
            {
                measurements.Add(new KnowledgeMeasurement
                {
                    facetId = HorticultureKnowledgeAdapter.FacetYield,
                    claimId = "yield_range",
                    value = KnowledgeClaimValue.Float(Mathf.Max(0, harvestYield)),
                    quality = repeatedHarvest || multiSeason ? 1.2f : 1f,
                    summary = "Observed harvest yield during discovery."
                });
                if (crop.plant?.harvestedThingDef != null)
                    measurements.Add(new KnowledgeMeasurement
                    {
                        facetId = HorticultureKnowledgeAdapter.FacetProduce,
                        claimId = "produce_identity",
                        value = KnowledgeClaimValue.Text(KnowledgeClaimValueType.DefReference, crop.plant.harvestedThingDef.defName),
                        summary = "Observed harvested produce during discovery."
                    });
                measurements.Add(new KnowledgeMeasurement
                {
                    facetId = HorticultureKnowledgeAdapter.FacetLifespan,
                    claimId = "harvest_cycles",
                    value = KnowledgeClaimValue.Integer(repeatedHarvest || multiSeason ? 2 : 1),
                    summary = "Observed harvest cycle during discovery."
                });
            }
            return HorticultureKnowledgeAdapter.Observe(discoverer, crop, eventKind, map, true, 1.3f,
                "A novel plant was discovered through " + (origin ?? "mutation") + ".", sourceInstanceId ??
                HorticultureKnowledgeEventIdentity.Discovery(crop, origin, traitList, parentList), HorticultureKnowledgeRole.Researcher, measurements,
                HorticultureKnowledgeAdapter.ContextFor(map, IntVec3.Invalid, null, origin == "wild"),
                HorticultureWitnesses(discoverer, map));
        }

        public static void CultivarDocumented(Pawn author, VarietyRecord variety)
        {
            if (variety?.cropDef == null) return;
            HorticultureKnowledgeAdapter.ObserveCultivar(author, variety, HorticultureKnowledgeEvent.Documentation,
                 author?.MapHeld, true, 1.1f, "A cultivar was named and preserved in the colony registry.",
                 HorticultureKnowledgeEventIdentity.Documentation(variety), HorticultureKnowledgeRole.Researcher,
                TraitMeasurements(variety.traits), HorticultureKnowledgeAdapter.ContextFor(author?.MapHeld),
                HorticultureWitnesses(author, author?.MapHeld));
        }

        private static bool Observe(Pawn observer, Plant plant, HorticultureKnowledgeEvent eventKind, string summary,
            string sourceInstanceId, List<KnowledgeMeasurement> measurements = null, bool failure = false)
        {
            if (plant == null) return false;
            IPlantToGrowSettable grower = plant.Map == null ? null : GridsUtility.GetPlantToGrowSettable(plant.Position, plant.Map);
            return HorticultureKnowledgeAdapter.Observe(observer, plant.def, eventKind, plant.Map, !failure, 1f, summary,
                sourceInstanceId, HorticultureKnowledgeRole.Grower, measurements,
                HorticultureKnowledgeAdapter.ContextFor(plant.Map, plant.Position,
                    grower, false),
                HorticultureWitnesses(observer, plant.Map));
        }

        private static List<KnowledgeMeasurement> TraitMeasurements(IEnumerable<VarietyTraitDef> traits) => new List<KnowledgeMeasurement>
        {
            new KnowledgeMeasurement
            {
                facetId = HorticultureKnowledgeAdapter.FacetTraits,
                claimId = "trait_identity",
                value = KnowledgeClaimValue.Set((traits ?? Enumerable.Empty<VarietyTraitDef>()).Where(value => value != null).Select(value => value.defName)),
                summary = "Traits recorded during cultivar documentation.",
                documented = true,
                revealed = true
            }
        };

        private static IReadOnlyList<Pawn> HorticultureWitnesses(Pawn observer, Map map) =>
            (map?.mapPawns?.FreeColonistsSpawned ?? Enumerable.Empty<Pawn>()).Where(value => value != null && value != observer)
                .Take(8).ToList();

        private static GrowthObservationState GerminationStateFor(Plant plant) => GrowthObservationState.For(plant);

        private static readonly ConditionalWeakTable<Plant, GrowthObservationState> GrowthStates =
            new ConditionalWeakTable<Plant, GrowthObservationState>();

        private sealed class GrowthObservationState
        {
            public bool germinated;
            public int lastGrowthBucket = -1;

            public static GrowthObservationState For(Plant plant)
            {
                if (plant == null) return new GrowthObservationState();
                return GrowthStates.GetValue(plant, _ => new GrowthObservationState());
            }

            public void ResetIfNeeded(Plant plant)
            {
                germinated = false;
                lastGrowthBucket = -1;
            }
        }
    }
}
