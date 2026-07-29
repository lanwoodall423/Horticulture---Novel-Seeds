using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace HorticultureNovelSeeds
{
    public static class ExpandedTraitUtility
    {
        private static readonly Dictionary<SynergyCacheKey, CachedFactor> CompanionCache = new Dictionary<SynergyCacheKey, CachedFactor>();
        private static readonly Dictionary<SynergyCacheKey, CachedFactor> SowSynergyCache = new Dictionary<SynergyCacheKey, CachedFactor>();
        private struct CachedFactor { public int tick; public float factor; }
        private struct SynergyCacheKey : IEquatable<SynergyCacheKey>
        {
            public int location;
            public ushort donor;
            public int stat;
            public bool Equals(SynergyCacheKey other) => location == other.location && donor == other.donor && stat == other.stat;
            public override bool Equals(object obj) => obj is SynergyCacheKey other && Equals(other);
            public override int GetHashCode() => ((location * 397) ^ donor) * 397 ^ stat;
        }
        private static int lastCachePruneTick;

        public static float Product(IEnumerable<VarietyTraitDef> traits, Func<VarietyTraitDef, float> selector)
        {
            float factor = 1f;
            if (traits == null) return factor;
            foreach (VarietyTraitDef trait in traits)
            {
                if (trait == null) continue;
                float value = selector(trait);
                factor *= value <= 0f ? 1f : value;
            }
            return Mathf.Max(0.05f, factor);
        }

        public static float SowWorkFactor(IEnumerable<VarietyTraitDef> traits) => Product(traits, t => t.sowWorkFactor) * NovelSeedUtility.WorkFactor(traits);
        public static float HarvestWorkFactor(IEnumerable<VarietyTraitDef> traits) => Product(traits, t => t.harvestWorkFactor) * NovelSeedUtility.WorkFactor(traits);
        public static float HarvestWorkFactor(Plant plant)
        {
            CompPlantVariety comp = plant?.TryGetComp<CompPlantVariety>();
            if (comp == null) return 1f;
            return comp.HarvestWorkFactor / SynergyFactor(plant, comp.ActiveTraits, "HarvestSpeed");
        }
        public static int SowSkillOffset(IEnumerable<VarietyTraitDef> traits) => traits?.Where(t => t != null).Sum(t => t.sowSkillOffset) ?? 0;
        public static string RequiredSowTag(IEnumerable<VarietyTraitDef> traits) => traits?.FirstOrDefault(t => t != null && !t.requiredSowTag.NullOrEmpty())?.requiredSowTag;

        public static void ClearAdjacentPlantsForHumongous(Plant plant)
        {
            if (plant?.Spawned != true || plant.TryGetComp<CompPlantVariety>()?.HasHumongousSpacing != true) return;
            foreach (IntVec3 cell in GenAdj.CellsAdjacent8Way(plant))
            {
                if (!cell.InBounds(plant.Map)) continue;
                Plant adjacent = cell.GetPlant(plant.Map);
                if (adjacent != null && adjacent != plant) adjacent.Destroy(DestroyMode.KillFinalize);
            }
        }

        public static bool VarietyMatchesGrowers(VarietyRecord variety, IEnumerable<IPlantToGrowSettable> growers)
        {
            string tag = RequiredSowTag(variety?.traits);
            return tag.NullOrEmpty() || (growers != null && growers.Where(g => g != null).All(g => GrowerMatchesTag(g, tag)));
        }

        public static bool StandardPlantMatchesGrowers(ThingDef plantDef, IEnumerable<IPlantToGrowSettable> growers)
        {
            if (plantDef?.plant == null || growers == null) return false;
            List<IPlantToGrowSettable> validGrowers = growers.Where(g => g != null).ToList();
            if (validGrowers.Count == 0) return false;
            return validGrowers.All(grower =>
            {
                string zoneTag = GrowerZoneTag(grower);
                return zoneTag.NullOrEmpty() || (plantDef.plant.sowTags?.Contains(zoneTag) ?? false);
            });
        }

        public static bool VarietyCanExposePlantInGrower(VarietyRecord variety, IPlantToGrowSettable grower)
        {
            string tag = RequiredSowTag(variety?.traits);
            return !tag.NullOrEmpty() && GrowerMatchesTag(grower, tag);
        }

        public static string ZoneLabel(VarietyRecord variety)
        {
            string tag = RequiredSowTag(variety?.traits);
            return tag == "VCE_Aquatic" ? "aquatic" : tag == "VCE_Sandy" ? "sandy" : "matching";
        }

        public static bool GrowerMatchesTag(object grower, string tag)
        {
            if (tag.NullOrEmpty()) return true;
            return GrowerZoneTag(grower) == tag;
        }

        private static string GrowerZoneTag(object grower)
        {
            string typeName = grower?.GetType().FullName ?? string.Empty;
            if (typeName == "VanillaPlantsExpandedMorePlants.Zone_GrowingAquatic") return "VCE_Aquatic";
            if (typeName == "VanillaPlantsExpandedMorePlants.Zone_GrowingSandy") return "VCE_Sandy";
            return null;
        }

        public static void ApplyResinEffects(Pawn pawn, Plant source, IEnumerable<VarietyTraitDef> traits)
        {
            if (pawn == null || traits == null) return;
            foreach (VarietyTraitDef trait in traits.Where(t => t != null))
            {
                if (trait.resinHediff != null && pawn.health != null)
                {
                    Hediff hediff = pawn.health.AddHediff(trait.resinHediff);
                    if (hediff != null && trait.resinHediffSeverity > 0f) hediff.Severity = Mathf.Max(hediff.Severity, trait.resinHediffSeverity);
                }
                if (trait.resinDamage != null && trait.resinDamageAmount > 0f)
                    pawn.TakeDamage(new DamageInfo(trait.resinDamage, trait.resinDamageAmount, 0f, -1f, source));
            }
        }

        public static void DropByproducts(Plant plant, IEnumerable<VarietyTraitDef> traits)
        {
            if (plant?.Map == null || traits == null) return;
            foreach (VarietyTraitDef trait in traits.Where(t => t?.byproductDef != null && t.byproductChance > 0f))
            {
                if (!Rand.Chance(Mathf.Clamp01(trait.byproductChance))) continue;
                Thing thing = ThingMaker.MakeThing(trait.byproductDef);
                thing.stackCount = Mathf.Max(1, trait.byproductCount.RandomInRange);
                GenPlace.TryPlaceThing(thing, plant.Position, plant.Map, ThingPlaceMode.Near);
            }
        }

        public static void TrySelfSeed(Plant plant)
        {
            CompPlantVariety comp = plant?.TryGetComp<CompPlantVariety>();
            if (comp == null || !comp.HasSelfSeeding) return;
            bool mature = plant.Growth >= 0.99f;
            if (!comp.TryMarkSelfSeededAtMaturity(mature) || plant.Map == null) return;
            foreach (IntVec3 cell in GenAdj.CellsAdjacent8Way(plant).InRandomOrder())
            {
                if (!cell.InBounds(plant.Map)) continue;
                if (!PlantUtility.CanNowPlantAt(plant.def, cell, plant.Map, false) || cell.GetPlant(plant.Map) != null) continue;
                Plant child = ThingMaker.MakeThing(plant.def) as Plant;
                if (child == null) return;
                child.Growth = 0.05f;
                child.sown = true;
                GenSpawn.Spawn(child, cell, plant.Map);
                CompPlantVariety childComp = child.TryGetComp<CompPlantVariety>();
                if (comp.Variety != null) childComp?.SetVariety(comp.Variety);
                else childComp?.SetPendingTraits(comp.ActiveTraits.ToList());
                return;
            }
        }

        public static float CompanionFactor(Plant plant, IEnumerable<VarietyTraitDef> traits)
        {
            return SynergyFactor(plant, traits, "GrowthRate");
        }

        public static float SynergyFactorAt(IntVec3 cell, Map map, IEnumerable<VarietyTraitDef> traits, string stat)
        {
            if (map == null || traits == null) return 1f;
            VarietyTraitDef synergy = FindSynergy(traits, stat);
            if (synergy == null) return 1f;
            int tick = Find.TickManager.TicksGame;
            PruneCaches(tick);
            SynergyCacheKey key = new SynergyCacheKey { location = (map.GetHashCode() * 397) ^ cell.GetHashCode(), donor = synergy.synergyPlantDef.shortHash, stat = stat.GetHashCode() };
            if (SowSynergyCache.TryGetValue(key, out CachedFactor cached) && tick - cached.tick < 250) return cached.factor;
            bool found = HasNearbyPlant(cell, map, synergy.synergyPlantDef);
            float factor = found ? (synergy.synergyFactor > 0f ? synergy.synergyFactor : 1.15f) : 1f;
            SowSynergyCache[key] = new CachedFactor { tick = tick, factor = factor };
            return factor;
        }

        public static float SynergyFactor(Plant plant, IEnumerable<VarietyTraitDef> traits, string stat)
        {
            if (plant?.Map == null || traits == null) return 1f;
            VarietyTraitDef synergy = FindSynergy(traits, stat);
            if (synergy == null) return 1f;
            int tick = Find.TickManager.TicksGame;
            PruneCaches(tick);
            SynergyCacheKey key = new SynergyCacheKey { location = plant.thingIDNumber, donor = synergy.synergyPlantDef.shortHash, stat = stat.GetHashCode() };
            if (CompanionCache.TryGetValue(key, out CachedFactor cached) && tick - cached.tick < 250) return cached.factor;
            bool found = HasNearbyPlant(plant.Position, plant.Map, synergy.synergyPlantDef, plant);
            float factor = found ? (synergy.synergyFactor > 0f ? synergy.synergyFactor : 1.15f) : 1f;
            CompanionCache[key] = new CachedFactor { tick = tick, factor = factor };
            return factor;
        }

        private static bool HasNearbyPlant(IntVec3 center, Map map, ThingDef plantDef, Plant excluded = null)
        {
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(center, 3f, true))
            {
                if (!cell.InBounds(map)) continue;
                Plant other = cell.GetPlant(map);
                if (other != null && other != excluded && other.def == plantDef && other.Growth > 0.25f) return true;
            }
            return false;
        }

        private static VarietyTraitDef FindSynergy(IEnumerable<VarietyTraitDef> traits, string stat)
        {
            foreach (VarietyTraitDef trait in traits)
                if (trait?.synergyPlantDef != null && trait.synergyStat == stat) return trait;
            return null;
        }

        private static void PruneCaches(int tick)
        {
            if (tick - lastCachePruneTick < 2000) return;
            lastCachePruneTick = tick;
            foreach (SynergyCacheKey key in CompanionCache.Where(pair => tick - pair.Value.tick > 1000).Select(pair => pair.Key).ToList()) CompanionCache.Remove(key);
            foreach (SynergyCacheKey key in SowSynergyCache.Where(pair => tick - pair.Value.tick > 1000).Select(pair => pair.Key).ToList()) SowSynergyCache.Remove(key);
        }

        public static bool HasFishHabitatNear(IntVec3 cell, Map map, out float factor)
        {
            factor = 1f;
            if (map == null) return false;
            foreach (IntVec3 nearby in GenRadial.RadialCellsAround(cell, 6f, true))
            {
                if (!nearby.InBounds(map)) continue;
                Plant plant = nearby.GetPlant(map);
                CompPlantVariety comp = plant?.TryGetComp<CompPlantVariety>();
                if (comp == null || plant.Growth < 0.5f) continue;
                factor = Mathf.Max(factor, Product(comp.ActiveTraits, t => t.fishingYieldFactor));
            }
            return factor > 1f;
        }
    }

    [HarmonyPatch(typeof(PlantUtility), nameof(PlantUtility.CanSowOnGrower))]
    public static class PlantUtility_CanSowOnGrower_VarietyZone_Patch
    {
        public static void Postfix(ThingDef plantDef, object obj, ref bool __result)
        {
            if (!(obj is IPlantToGrowSettable grower)) return;
            VarietyRecord variety = GameComponent_NovelSeeds.Instance?.SelectedVarietyFor(grower);
            if (variety?.cropDef == plantDef)
            {
                string tag = ExpandedTraitUtility.RequiredSowTag(variety.traits);
                if (!tag.NullOrEmpty()) __result = ExpandedTraitUtility.GrowerMatchesTag(obj, tag);
                return;
            }
            IReadOnlyList<VarietyRecord> breeding = GameComponent_NovelSeeds.Instance?.BreedingVarietiesFor(grower);
            if (breeding == null || breeding.Count == 0 || breeding.Any(item => item.cropDef != plantDef)) return;
            __result = breeding.All(item =>
            {
                string tag = ExpandedTraitUtility.RequiredSowTag(item.traits);
                return tag.NullOrEmpty() || ExpandedTraitUtility.GrowerMatchesTag(obj, tag);
            });
        }
    }

    [HarmonyPatch(typeof(Plant), nameof(Plant.Growth), MethodType.Setter)]
    public static class Plant_Growth_SelfSeed_Patch
    {
        public static void Postfix(Plant __instance) => ExpandedTraitUtility.TrySelfSeed(__instance);
    }

    [HarmonyPatch(typeof(Plant), nameof(Plant.GrowthRate), MethodType.Getter)]
    public static class Plant_GrowthRate_ExpandedTraits_Patch
    {
        public static void Postfix(Plant __instance, ref float __result)
        {
            CompPlantVariety comp = __instance.TryGetComp<CompPlantVariety>();
            if (comp == null || !comp.HasAnyTraits) return;
            if (comp.NeedsResource) { __result = 0f; return; }
            if (__result <= 0f && comp.HasPerennialDormancy && GenTemperature.TryGetTemperatureForCell(__instance.Position, __instance.Map, out float temperature) && temperature < __instance.def.plant.minOptimalGrowthTemperature)
            {
                __result = Mathf.Max(__result, comp.DormantGrowthFactor);
            }
            __result *= ExpandedTraitUtility.CompanionFactor(__instance, comp.ActiveTraits);
        }
    }

    [HarmonyPatch(typeof(Pawn_PathFollower), "TryEnterNextPathCell")]
    public static class PawnPathFollower_TramplePlant_Patch
    {
        public static void Postfix(Pawn ___pawn)
        {
            Plant plant = ___pawn?.Spawned == true ? ___pawn.Position.GetPlant(___pawn.Map) : null;
            CompPlantVariety comp = plant?.TryGetComp<CompPlantVariety>();
            float damage = comp?.TramplingDamage ?? 0f;
            if (damage > 0f) plant.TakeDamage(new DamageInfo(DamageDefOf.Blunt, damage, 0f, -1f, ___pawn));
        }
    }

    [HarmonyPatch(typeof(FishingUtility), nameof(FishingUtility.GetCatchesFor))]
    public static class FishingUtility_FishHabitat_Patch
    {
        public static void Postfix(Pawn pawn, IntVec3 cell, ref List<Thing> __result)
        {
            if (__result == null || !ExpandedTraitUtility.HasFishHabitatNear(cell, pawn?.Map, out float factor)) return;
            foreach (Thing catchThing in __result)
                if (catchThing != null && catchThing.def.stackLimit > 1) catchThing.stackCount = Mathf.Clamp(GenMath.RoundRandom(catchThing.stackCount * factor), 1, catchThing.def.stackLimit);
        }
    }

    [HarmonyPatch(typeof(PlantUtility), nameof(PlantUtility.AdjacentSowBlocker))]
    public static class PlantUtility_HumongousSpacing_Patch
    {
        public static void Postfix(ThingDef plantDef, IntVec3 c, Map map, ref Thing __result)
        {
            if (__result != null || map == null) return;
            foreach (IntVec3 offset in GenAdj.AdjacentCells8WayRandomized())
            {
                IntVec3 adjacent = c + offset;
                if (!adjacent.InBounds(map)) continue;
                Plant neighbor = adjacent.GetPlant(map);
                bool neighborHumongous = neighbor?.TryGetComp<CompPlantVariety>()?.HasHumongousSpacing == true;
                if (neighborHumongous && neighbor != null) { __result = neighbor; return; }
            }
        }
    }
}
