using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace HorticultureNovelSeeds
{
    public class NovelSeedsSettings : ModSettings
    {
        public float globalMutationChance = NovelSeedUtility.SpontaneousMutationChance;
        public float globalCrossPollinationChance = NovelSeedUtility.DefaultCrossPollinationChance;
        public float minimumDonorGrowth = NovelSeedUtility.DefaultMinimumDonorGrowth;
        public float secondCrossPollinationTraitChance = NovelSeedUtility.DefaultSecondCrossPollinationTraitChance;
        public float laterCrossPollinationTraitChance = NovelSeedUtility.DefaultLaterCrossPollinationTraitChance;
        public float wildMutationChance = NovelSeedUtility.DefaultWildMutationChance;
        public int maxCrossPollinationTraits = 3;
        public int maxTraitsPerEvent = 3;
        public bool enableTraitBalancing = true;
        public float traitBalanceStrength = 0.75f;
        public int allowedTraitImbalance = 1;
        public float exceptionalVarietyChance = 0.08f;
        public bool enableProduceVisuals = true;
        public int minimumPaletteSize = 2;
        public int maximumPaletteSize = 5;
        public float allowedHueRangeDegrees = 140f;
        public float minimumPaletteSaturation = 0.55f;
        public float maximumPaletteSaturation = 0.95f;
        public float minimumPaletteValue = 0.62f;
        public float maximumPaletteValue = 0.95f;
        private List<PlantSettingsRecord> plantSettings = new List<PlantSettingsRecord>();
        private List<PlantTagOverrideRecord> plantTagOverrides = new List<PlantTagOverrideRecord>();
        private List<PlantGroupRecord> plantGroups = new List<PlantGroupRecord>();
        private int nextPlantGroupId = 1;
        private bool defaultPlantGroupsInitialized;
        private bool vanillaFlowersExpandedGroupMigrated;
        private List<GlobalTraitSettingsRecord> globalTraitSettings = new List<GlobalTraitSettingsRecord>();
        private List<WildTraitSettingsRecord> wildTraitSettings = new List<WildTraitSettingsRecord>();
        private List<CategorySettingsRecord> categorySettings = new List<CategorySettingsRecord>();
        private List<FamilySettingsRecord> familySettings = new List<FamilySettingsRecord>();
        private bool subtypeVisualDefaultMigrated;
        private readonly Dictionary<string, VisualSettingsRecord> visualOverrideCache = new Dictionary<string, VisualSettingsRecord>();
        private readonly Dictionary<string, List<VisualSettingsRecord>> visualInstancesCache = new Dictionary<string, List<VisualSettingsRecord>>();
        private readonly HashSet<string> noVisualOverrideCache = new HashSet<string>();
        private readonly Dictionary<string, PlantSettingsRecord> plantSettingsCache = new Dictionary<string, PlantSettingsRecord>();
        private readonly Dictionary<string, GlobalTraitSettingsRecord> globalTraitSettingsCache = new Dictionary<string, GlobalTraitSettingsRecord>();
        private readonly Dictionary<string, WildTraitSettingsRecord> wildTraitSettingsCache = new Dictionary<string, WildTraitSettingsRecord>();
        private readonly Dictionary<string, PlantGroupRecord> plantGroupCache = new Dictionary<string, PlantGroupRecord>();
        private bool lookupCachesValid;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref globalMutationChance, "globalMutationChance", NovelSeedUtility.SpontaneousMutationChance);
            Scribe_Values.Look(ref globalCrossPollinationChance, "globalCrossPollinationChance", NovelSeedUtility.DefaultCrossPollinationChance);
            Scribe_Values.Look(ref minimumDonorGrowth, "minimumDonorGrowth", NovelSeedUtility.DefaultMinimumDonorGrowth);
            Scribe_Values.Look(ref secondCrossPollinationTraitChance, "secondCrossPollinationTraitChance", NovelSeedUtility.DefaultSecondCrossPollinationTraitChance);
            Scribe_Values.Look(ref laterCrossPollinationTraitChance, "laterCrossPollinationTraitChance", NovelSeedUtility.DefaultLaterCrossPollinationTraitChance);
            Scribe_Values.Look(ref wildMutationChance, "wildMutationChance", NovelSeedUtility.DefaultWildMutationChance);
            Scribe_Values.Look(ref maxCrossPollinationTraits, "maxCrossPollinationTraits", 3);
            Scribe_Values.Look(ref maxTraitsPerEvent, "maxTraitsPerEvent", 3);
            Scribe_Values.Look(ref enableTraitBalancing, "enableTraitBalancing", true);
            Scribe_Values.Look(ref traitBalanceStrength, "traitBalanceStrength", 0.75f);
            Scribe_Values.Look(ref allowedTraitImbalance, "allowedTraitImbalance", 1);
            Scribe_Values.Look(ref exceptionalVarietyChance, "exceptionalVarietyChance", 0.08f);
            Scribe_Values.Look(ref enableProduceVisuals, "enableProduceVisuals", true);
            Scribe_Values.Look(ref minimumPaletteSize, "minimumPaletteSize", 2);
            Scribe_Values.Look(ref maximumPaletteSize, "maximumPaletteSize", 5);
            Scribe_Values.Look(ref allowedHueRangeDegrees, "allowedHueRangeDegrees", 140f);
            Scribe_Values.Look(ref minimumPaletteSaturation, "minimumPaletteSaturation", 0.55f);
            Scribe_Values.Look(ref maximumPaletteSaturation, "maximumPaletteSaturation", 0.95f);
            Scribe_Values.Look(ref minimumPaletteValue, "minimumPaletteValue", 0.62f);
            Scribe_Values.Look(ref maximumPaletteValue, "maximumPaletteValue", 0.95f);
            Scribe_Collections.Look(ref plantSettings, "plantSettings", LookMode.Deep);
            Scribe_Collections.Look(ref plantTagOverrides, "plantTagOverrides", LookMode.Deep);
            Scribe_Collections.Look(ref plantGroups, "plantGroups", LookMode.Deep);
            Scribe_Values.Look(ref nextPlantGroupId, "nextPlantGroupId", 1);
            Scribe_Values.Look(ref defaultPlantGroupsInitialized, "defaultPlantGroupsInitialized", false);
            Scribe_Values.Look(ref vanillaFlowersExpandedGroupMigrated, "vanillaFlowersExpandedGroupMigrated", false);
            Scribe_Collections.Look(ref globalTraitSettings, "globalTraitSettings", LookMode.Deep);
            Scribe_Collections.Look(ref wildTraitSettings, "wildTraitSettings", LookMode.Deep);
            Scribe_Collections.Look(ref categorySettings, "categorySettings", LookMode.Deep);
            Scribe_Collections.Look(ref familySettings, "familySettings", LookMode.Deep);
            Scribe_Values.Look(ref subtypeVisualDefaultMigrated, "subtypeVisualDefaultMigrated", false);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (plantSettings == null)
                {
                    plantSettings = new List<PlantSettingsRecord>();
                }
                if (globalTraitSettings == null) globalTraitSettings = new List<GlobalTraitSettingsRecord>();
                if (plantTagOverrides == null) plantTagOverrides = new List<PlantTagOverrideRecord>();
                if (wildTraitSettings == null) wildTraitSettings = new List<WildTraitSettingsRecord>();
                if (categorySettings == null) categorySettings = new List<CategorySettingsRecord>();
                if (familySettings == null) familySettings = new List<FamilySettingsRecord>();
                Normalize();
            }
        }

        public void ApplyFrom(NovelSeedsSettings other)
        {
            if (other == null) return;
            globalMutationChance = other.globalMutationChance;
            globalCrossPollinationChance = other.globalCrossPollinationChance;
            minimumDonorGrowth = other.minimumDonorGrowth;
            secondCrossPollinationTraitChance = other.secondCrossPollinationTraitChance;
            laterCrossPollinationTraitChance = other.laterCrossPollinationTraitChance;
            wildMutationChance = other.wildMutationChance;
            maxCrossPollinationTraits = other.maxCrossPollinationTraits;
            maxTraitsPerEvent = other.maxTraitsPerEvent;
            enableTraitBalancing = other.enableTraitBalancing;
            traitBalanceStrength = other.traitBalanceStrength;
            allowedTraitImbalance = other.allowedTraitImbalance;
            exceptionalVarietyChance = other.exceptionalVarietyChance;
            enableProduceVisuals = other.enableProduceVisuals;
            minimumPaletteSize = other.minimumPaletteSize;
            maximumPaletteSize = other.maximumPaletteSize;
            allowedHueRangeDegrees = other.allowedHueRangeDegrees;
            minimumPaletteSaturation = other.minimumPaletteSaturation;
            maximumPaletteSaturation = other.maximumPaletteSaturation;
            minimumPaletteValue = other.minimumPaletteValue;
            maximumPaletteValue = other.maximumPaletteValue;
            plantSettings = other.plantSettings ?? new List<PlantSettingsRecord>();
            plantTagOverrides = other.plantTagOverrides ?? new List<PlantTagOverrideRecord>();
            plantGroups = other.plantGroups ?? new List<PlantGroupRecord>();
            nextPlantGroupId = other.nextPlantGroupId;
            defaultPlantGroupsInitialized = other.defaultPlantGroupsInitialized;
            vanillaFlowersExpandedGroupMigrated = other.vanillaFlowersExpandedGroupMigrated;
            globalTraitSettings = other.globalTraitSettings ?? new List<GlobalTraitSettingsRecord>();
            wildTraitSettings = other.wildTraitSettings ?? new List<WildTraitSettingsRecord>();
            categorySettings = other.categorySettings ?? new List<CategorySettingsRecord>();
            familySettings = other.familySettings ?? new List<FamilySettingsRecord>();
            subtypeVisualDefaultMigrated = other.subtypeVisualDefaultMigrated;
            Normalize();
            ClearVisualCache();
            ProduceMaskRenderer.ClearAll();
            PlantTagUtility.RebuildCache();
        }
        public void Normalize()
        {
            globalMutationChance = Mathf.Clamp01(globalMutationChance);
            globalCrossPollinationChance = Mathf.Clamp01(globalCrossPollinationChance);
            minimumDonorGrowth = Mathf.Clamp01(minimumDonorGrowth);
            secondCrossPollinationTraitChance = Mathf.Clamp01(secondCrossPollinationTraitChance);
            laterCrossPollinationTraitChance = Mathf.Clamp01(laterCrossPollinationTraitChance);
            wildMutationChance = Mathf.Clamp01(wildMutationChance);
            maxCrossPollinationTraits = Mathf.Clamp(maxCrossPollinationTraits, 1, 10);
            maxTraitsPerEvent = Mathf.Clamp(maxTraitsPerEvent, 1, 10);
            traitBalanceStrength = Mathf.Clamp01(traitBalanceStrength);
            allowedTraitImbalance = Mathf.Clamp(allowedTraitImbalance, 0, 10);
            exceptionalVarietyChance = Mathf.Clamp01(exceptionalVarietyChance);
            minimumPaletteSize = Mathf.Clamp(minimumPaletteSize, 1, 24);
            maximumPaletteSize = Mathf.Clamp(maximumPaletteSize, minimumPaletteSize, 24);
            allowedHueRangeDegrees = Mathf.Clamp(allowedHueRangeDegrees, 0f, 360f);
            minimumPaletteSaturation = Mathf.Clamp01(minimumPaletteSaturation);
            maximumPaletteSaturation = Mathf.Clamp(maximumPaletteSaturation, minimumPaletteSaturation, 1f);
            minimumPaletteValue = Mathf.Clamp01(minimumPaletteValue);
            maximumPaletteValue = Mathf.Clamp(maximumPaletteValue, minimumPaletteValue, 1f);
            if (plantSettings == null)
            {
                plantSettings = new List<PlantSettingsRecord>();
            }
            if (plantGroups == null) plantGroups = new List<PlantGroupRecord>();
            if (plantTagOverrides == null) plantTagOverrides = new List<PlantTagOverrideRecord>();
            if (globalTraitSettings == null) globalTraitSettings = new List<GlobalTraitSettingsRecord>();
            if (wildTraitSettings == null) wildTraitSettings = new List<WildTraitSettingsRecord>();
            if (categorySettings == null) categorySettings = new List<CategorySettingsRecord>();
            if (familySettings == null) familySettings = new List<FamilySettingsRecord>();
            if (!subtypeVisualDefaultMigrated)
            {
                foreach (FamilySettingsRecord family in familySettings) family.useTypeSpecificVisuals = true;
                subtypeVisualDefaultMigrated = true;
            }
            plantSettings.RemoveAll(record => record == null || string.IsNullOrEmpty(record.PlantDefName));
            plantTagOverrides.RemoveAll(record => record == null || record.PlantDefName.NullOrEmpty());
            foreach (PlantTagOverrideRecord record in plantTagOverrides) record.Normalize();
            plantGroups.RemoveAll(group => group == null || group.Id.NullOrEmpty() || group.Name.NullOrEmpty());
            EnsureDefaultPlantGroups();
            HashSet<string> claimedPlants = new HashSet<string>();
            foreach (PlantGroupRecord group in plantGroups)
            {
                group.Normalize();
                group.RemoveAlreadyClaimedPlants(claimedPlants);
            }
            globalTraitSettings.RemoveAll(record => record == null || string.IsNullOrEmpty(record.TraitDefName));
            wildTraitSettings.RemoveAll(record => record == null || string.IsNullOrEmpty(record.TraitDefName));
            foreach (PlantSettingsRecord record in plantSettings)
            {
                record.Normalize();
            }
            foreach (GlobalTraitSettingsRecord record in globalTraitSettings) record.Normalize();
            foreach (WildTraitSettingsRecord record in wildTraitSettings) record.Normalize();
            categorySettings.RemoveAll(record => record == null || record.Key.NullOrEmpty());
            familySettings.RemoveAll(record => record == null || record.Key.NullOrEmpty());
            foreach (FamilySettingsRecord record in familySettings) record.Normalize();
            RebuildLookupCaches();
        }

        public void ResetAll()
        {
            globalMutationChance = NovelSeedUtility.SpontaneousMutationChance;
            globalCrossPollinationChance = NovelSeedUtility.DefaultCrossPollinationChance;
            minimumDonorGrowth = NovelSeedUtility.DefaultMinimumDonorGrowth;
            secondCrossPollinationTraitChance = NovelSeedUtility.DefaultSecondCrossPollinationTraitChance;
            laterCrossPollinationTraitChance = NovelSeedUtility.DefaultLaterCrossPollinationTraitChance;
            wildMutationChance = NovelSeedUtility.DefaultWildMutationChance;
            maxCrossPollinationTraits = 3;
            maxTraitsPerEvent = 3;
            enableTraitBalancing = true;
            traitBalanceStrength = 0.75f;
            allowedTraitImbalance = 1;
            exceptionalVarietyChance = 0.08f;
            enableProduceVisuals = true;
            minimumPaletteSize = 2;
            maximumPaletteSize = 5;
            allowedHueRangeDegrees = 140f;
            minimumPaletteSaturation = 0.55f;
            maximumPaletteSaturation = 0.95f;
            minimumPaletteValue = 0.62f;
            maximumPaletteValue = 0.95f;
            if (plantSettings == null)
            {
                plantSettings = new List<PlantSettingsRecord>();
            }
            if (globalTraitSettings == null)
            {
                globalTraitSettings = new List<GlobalTraitSettingsRecord>();
            }
            plantSettings.Clear();
            plantTagOverrides?.Clear();
            plantGroups?.Clear();
            nextPlantGroupId = 1;
            defaultPlantGroupsInitialized = false;
            vanillaFlowersExpandedGroupMigrated = false;
            EnsureDefaultPlantGroups();
            globalTraitSettings.Clear();
            wildTraitSettings?.Clear();
            categorySettings?.Clear();
            familySettings?.Clear();
            RebuildLookupCaches();
            ClearVisualCache();
            ProduceMaskRenderer.ClearAll();
            PlantTagUtility.RebuildCache();
        }

        public void ResetWildSettings()
        {
            wildMutationChance = NovelSeedUtility.DefaultWildMutationChance;
            wildTraitSettings?.Clear();
            RebuildLookupCaches();
        }

        public void ResetGlobalWeights()
        {
            if (globalTraitSettings == null)
            {
                globalTraitSettings = new List<GlobalTraitSettingsRecord>();
            }
            foreach (GlobalTraitSettingsRecord record in globalTraitSettings)
            {
                VarietyTraitDef trait = DefDatabase<VarietyTraitDef>.GetNamedSilentFail(record.TraitDefName);
                record.weight = DefaultWeight(trait);
            }
            if (familySettings != null) foreach (FamilySettingsRecord family in familySettings) family.ResetWeights();
        }

        public void ResetPlant(ThingDef plantDef)
        {
            PlantSettingsRecord record = GetPlantSettings(plantDef, false);
            record?.ResetToDefaults();
            ResetPlantTags(plantDef);
        }

        public PlantTagOverrideRecord GetPlantTagOverrides(ThingDef plantDef, bool create = true)
        {
            if (plantDef == null) return null;
            if (plantTagOverrides == null) plantTagOverrides = new List<PlantTagOverrideRecord>();
            PlantTagOverrideRecord record = plantTagOverrides.FirstOrDefault(item => item?.Matches(plantDef) == true);
            if (record == null && create)
            {
                record = new PlantTagOverrideRecord(plantDef);
                plantTagOverrides.Add(record);
            }
            return record;
        }

        public void ApplyPlantTagOverrides(ThingDef plantDef, HashSet<string> tags)
        {
            PlantTagOverrideRecord record = GetPlantTagOverrides(plantDef, false);
            if (record == null || tags == null) return;
            foreach (string tag in record.RemovedTags) tags.Remove(tag);
            foreach (string tag in record.AddedTags) tags.Add(tag);
        }

        public void SetPlantTag(ThingDef plantDef, string tag, bool enabled)
        {
            string normalized = tag?.Trim();
            if (plantDef == null || normalized.NullOrEmpty()) return;
            bool inferred = PlantTagUtility.InferredHasTag(plantDef, normalized);
            PlantTagOverrideRecord record = GetPlantTagOverrides(plantDef);
            record.Set(normalized, enabled, inferred);
            if (record.IsEmpty) plantTagOverrides.Remove(record);
            PlantTagUtility.RebuildCache();
        }

        public void ScanAllPlantTags()
        {
            if (plantTagOverrides == null) plantTagOverrides = new List<PlantTagOverrideRecord>();
            plantTagOverrides.Clear();
            PlantTagUtility.RebuildCache();
        }

        public void ResetPlantTags(ThingDef plantDef)
        {
            PlantTagOverrideRecord record = GetPlantTagOverrides(plantDef, false);
            if (record != null) plantTagOverrides.Remove(record);
            PlantTagUtility.RebuildCache();
        }

        public void ResetTag(string tag)
        {
            string normalized = tag?.Trim();
            if (normalized.NullOrEmpty() || plantTagOverrides == null) return;
            foreach (PlantTagOverrideRecord record in plantTagOverrides) record.Clear(normalized);
            plantTagOverrides.RemoveAll(record => record == null || record.IsEmpty);
            PlantTagUtility.RebuildCache();
        }

        public IEnumerable<string> ConfiguredPlantTags()
        {
            return (plantTagOverrides ?? new List<PlantTagOverrideRecord>())
                .Where(record => record != null)
                .SelectMany(record => record.AddedTags.Concat(record.RemovedTags))
                .Distinct(System.StringComparer.OrdinalIgnoreCase);
        }

        public float MutationChanceFor(ThingDef plantDef)
        {
            PlantSettingsRecord record = GetEffectivePlantSettings(plantDef, false);
            return record != null && record.useCustomMutationChance ? record.mutationChance : globalMutationChance;
        }

        public int MaxTraitsPerEvent => Mathf.Clamp(maxTraitsPerEvent, 1, 10);
        public int MaxCrossPollinationTraits => Mathf.Clamp(maxCrossPollinationTraits, 1, 10);
        public float MinimumDonorGrowth => Mathf.Clamp01(minimumDonorGrowth);
        public float SecondCrossPollinationTraitChance => Mathf.Clamp01(secondCrossPollinationTraitChance);
        public float LaterCrossPollinationTraitChance => Mathf.Clamp01(laterCrossPollinationTraitChance);

        public float CrossPollinationChanceFor(ThingDef plantDef)
        {
            PlantSettingsRecord record = GetEffectivePlantSettings(plantDef, false);
            return record != null && record.useCustomCrossPollinationChance ? record.crossPollinationChance : globalCrossPollinationChance;
        }

        public PlantSettingsRecord GetPlantSettings(ThingDef plantDef, bool create = true)
        {
            if (plantDef == null)
            {
                return null;
            }
            if (plantSettings == null)
            {
                plantSettings = new List<PlantSettingsRecord>();
            }

            EnsureLookupCaches();
            plantSettingsCache.TryGetValue(plantDef.defName, out PlantSettingsRecord record);
            if (record == null && create)
            {
                record = new PlantSettingsRecord(plantDef);
                plantSettings.Add(record);
                plantSettingsCache[plantDef.defName] = record;
            }
            return record;
        }

        internal IEnumerable<PlantSettingsRecord> PlantSettingsRecords => plantSettings ?? Enumerable.Empty<PlantSettingsRecord>();

        internal void ClearAllMasks()
        {
            foreach (PlantSettingsRecord record in PlantSettingsRecords.Where(item => item != null)) record.ClearMasks();
            ClearVisualCache();
            ProduceMaskRenderer.ClearAll();
        }
        public IReadOnlyList<PlantGroupRecord> PlantGroups => plantGroups;

        public void EnsureDefaultPlantGroups()
        {
            bool migrateExpandedFlowers = defaultPlantGroupsInitialized
                && NovelSeedUtility.VanillaFlowersExpandedActive
                && !vanillaFlowersExpandedGroupMigrated;
            if (defaultPlantGroupsInitialized && !migrateExpandedFlowers) return;

            List<ThingDef> flowers = DefDatabase<ThingDef>.AllDefsListForReading.Where(NovelSeedUtility.IsFlowerPlant).ToList();
            if (flowers.Count == 0) return;
            if (plantGroups == null) plantGroups = new List<PlantGroupRecord>();
            if (plantTagOverrides == null) plantTagOverrides = new List<PlantTagOverrideRecord>();

            PlantGroupRecord flowerGroup = plantGroups.FirstOrDefault(group => group != null && group.Name.Equals("Flower", System.StringComparison.OrdinalIgnoreCase));
            if (flowerGroup == null)
            {
                flowerGroup = new PlantGroupRecord("HNS_Group_" + nextPlantGroupId++, "Flower");
                plantGroups.Add(flowerGroup);
            }
            foreach (ThingDef flower in flowers)
            {
                if (!defaultPlantGroupsInitialized)
                {
                    foreach (PlantGroupRecord group in plantGroups) group.Remove(flower);
                    flowerGroup.Add(flower);
                }
                else if (flower.plant.sowTags?.Contains("VPE_Blooming") == true && !plantGroups.Any(group => group.Contains(flower)))
                {
                    flowerGroup.Add(flower);
                }
            }
            defaultPlantGroupsInitialized = true;
            if (NovelSeedUtility.VanillaFlowersExpandedActive) vanillaFlowersExpandedGroupMigrated = true;
            RebuildLookupCaches();
            ClearVisualCache();
        }
        public PlantGroupRecord CreatePlantGroup(string name)
        {
            string normalized = name?.Trim();
            if (normalized.NullOrEmpty() || plantGroups.Any(group => group.Name.Equals(normalized, System.StringComparison.OrdinalIgnoreCase))) return null;
            PlantGroupRecord groupRecord = new PlantGroupRecord("HNS_Group_" + nextPlantGroupId++, normalized);
            plantGroups.Add(groupRecord);
            return groupRecord;
        }

        public void RenamePlantGroup(PlantGroupRecord group, string name)
        {
            string normalized = name?.Trim();
            if (group == null || normalized.NullOrEmpty() || plantGroups.Any(other => other != group && other.Name.Equals(normalized, System.StringComparison.OrdinalIgnoreCase))) return;
            group.SetName(normalized);
        }

        public void DeletePlantGroup(PlantGroupRecord group)
        {
            if (group != null) plantGroups.Remove(group);
            RebuildLookupCaches();
            ClearVisualCache();
        }

        public PlantGroupRecord GroupForPlant(ThingDef plantDef)
        {
            if (plantDef == null) return null;
            EnsureLookupCaches();
            plantGroupCache.TryGetValue(plantDef.defName, out PlantGroupRecord group);
            return group;
        }

        public void AssignPlantToGroup(ThingDef plantDef, PlantGroupRecord group)
        {
            if (plantDef == null) return;
            foreach (PlantGroupRecord existing in plantGroups) existing.Remove(plantDef);
            group?.Add(plantDef);
            RebuildLookupCaches();
            ClearVisualCache();
        }

        public void RemovePlantFromGroup(ThingDef plantDef)
        {
            AssignPlantToGroup(plantDef, null);
        }

        public PlantSettingsRecord GetEffectivePlantSettings(ThingDef plantDef, bool createPersonal = true)
        {
            return GroupForPlant(plantDef)?.Settings ?? GetPlantSettings(plantDef, createPersonal);
        }
        public GlobalTraitSettingsRecord GetGlobalTraitSettings(VarietyTraitDef traitDef, bool create = true)
        {
            if (traitDef == null)
            {
                return null;
            }
            if (globalTraitSettings == null)
            {
                globalTraitSettings = new List<GlobalTraitSettingsRecord>();
            }

            EnsureLookupCaches();
            globalTraitSettingsCache.TryGetValue(traitDef.defName, out GlobalTraitSettingsRecord record);
            if (record == null && create)
            {
                record = new GlobalTraitSettingsRecord(traitDef);
                globalTraitSettings.Add(record);
                globalTraitSettingsCache[traitDef.defName] = record;
            }
            return record;
        }

        public WildTraitSettingsRecord GetWildTraitSettings(VarietyTraitDef traitDef, bool create = true)
        {
            if (traitDef == null) return null;
            VarietyTraitDef root = TraitConfigUtility.Root(traitDef);
            if (wildTraitSettings == null) wildTraitSettings = new List<WildTraitSettingsRecord>();
            EnsureLookupCaches();
            wildTraitSettingsCache.TryGetValue(root.defName, out WildTraitSettingsRecord record);
            if (record == null && create)
            {
                record = new WildTraitSettingsRecord(root);
                wildTraitSettings.Add(record);
                wildTraitSettingsCache[root.defName] = record;
            }
            return record;
        }

        public bool IsWildTraitAllowed(VarietyTraitDef traitDef)
        {
            return GetWildTraitSettings(traitDef, false)?.enabled ?? true;
        }

        public float WildTraitWeight(VarietyTraitDef traitDef)
        {
            VarietyTraitDef root = TraitConfigUtility.Root(traitDef);
            return GetWildTraitSettings(root, false)?.weight ?? DefaultWeight(root);
        }

        public CategorySettingsRecord GetCategorySettings(string key, bool create = true)
        {
            CategorySettingsRecord record = categorySettings.FirstOrDefault(x => x?.Key == key);
            if (record == null && create) { record = new CategorySettingsRecord(key); categorySettings.Add(record); }
            return record;
        }

        public string TraitGroup(VarietyTraitDef traitDef)
        {
            VarietyTraitDef root = TraitConfigUtility.Root(traitDef);
            if (root == null) return "Ungrouped";
            GlobalTraitSettingsRecord record = GetGlobalTraitSettings(root, false);
            if (record?.groupCustomized == true) return record.Group.NullOrEmpty() ? "Ungrouped" : record.Group;
            return TraitConfigUtility.Category(root);
        }

        public IReadOnlyList<string> TraitGroupNames()
        {
            HashSet<string> groups = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase) { "Ungrouped" };
            foreach (VarietyTraitDef trait in TraitConfigUtility.TopLevelTraits())
            {
                groups.Add(TraitConfigUtility.Category(trait));
                groups.Add(TraitGroup(trait));
            }
            return groups.OrderBy(group => group == "Ungrouped" ? 1 : 0).ThenBy(group => group).ToList();
        }

        public void SetTraitGroup(VarietyTraitDef traitDef, string group)
        {
            VarietyTraitDef root = TraitConfigUtility.Root(traitDef);
            if (root == null) return;
            string normalized = group?.Trim();
            if (!normalized.NullOrEmpty() && normalized.Equals(TraitConfigUtility.Category(root), System.StringComparison.OrdinalIgnoreCase))
            {
                ResetTraitGroup(root);
                return;
            }
            GetGlobalTraitSettings(root).SetGroup(normalized.NullOrEmpty() || normalized.Equals("Ungrouped", System.StringComparison.OrdinalIgnoreCase) ? null : normalized);
        }

        public void ResetTraitGroup(VarietyTraitDef traitDef)
        {
            VarietyTraitDef root = TraitConfigUtility.Root(traitDef);
            GetGlobalTraitSettings(root, false)?.ResetGroup();
        }
        public FamilySettingsRecord GetFamilySettings(string key, bool create = true)
        {
            if (key.NullOrEmpty()) return null;
            FamilySettingsRecord record = familySettings.FirstOrDefault(x => x?.Key == key);
            if (record == null && create) { record = new FamilySettingsRecord(key); familySettings.Add(record); }
            return record;
        }

        public bool IsTraitTagEligible(ThingDef plantDef, VarietyTraitDef traitDef)
        {
            return traitDef != null;
        }

        public bool ProduceTraitHasEffect(ThingDef plantDef, VarietyTraitDef traitDef)
        {
            if (plantDef == null || traitDef == null || !PlantTagUtility.MeetsProduceEffectRequirements(plantDef, traitDef)) return false;
            GlobalTraitSettingsRecord record = GetGlobalTraitSettings(TraitConfigUtility.Root(traitDef), false);
            return record?.tagExclusive != true || record.ExclusiveTags.Any(tag => PlantTagUtility.HasTag(plantDef, tag));
        }

        public bool IsTraitAllowed(ThingDef plantDef, VarietyTraitDef traitDef)
        {
            if (traitDef == null) return false;
            if (!IsTraitGloballyAllowed(traitDef)) return false;
            string traitGroup = TraitGroup(traitDef);
            VarietyTraitDef root = TraitConfigUtility.Root(traitDef);
            PlantSettingsRecord record = GetEffectivePlantSettings(plantDef, false);
            if (record?.GetTraitGroupSettings(traitGroup, false)?.enabled == false) return false;
            return record?.GetTraitSettings(root, false)?.enabled ?? true;
        }

        public bool IsTraitGloballyAllowed(VarietyTraitDef traitDef)
        {
            if (traitDef == null) return false;
            if (GetCategorySettings(TraitGroup(traitDef), false)?.enabled == false) return false;
            FamilySettingsRecord family = GetFamilySettings(traitDef.configFamily, false);
            if (family?.enabled == false) return false;
            return !TraitConfigUtility.IsSubtype(traitDef) || family?.GetType(traitDef, false)?.enabled != false;
        }

        public float GlobalTraitWeight(VarietyTraitDef traitDef)
        {
            VarietyTraitDef root = TraitConfigUtility.Root(traitDef);
            float weight = GetGlobalTraitSettings(root, false)?.weight ?? DefaultWeight(root);
            FamilySettingsRecord family = GetFamilySettings(traitDef.configFamily, false);
            if (TraitConfigUtility.IsSubtype(traitDef)) weight *= family?.GetType(traitDef, false)?.weight ?? Mathf.Max(0f, traitDef.commonality);
            return weight;
        }

        public bool GlobalTraitAppliesToProduce(VarietyTraitDef traitDef)
        {
            return traitDef != null;
        }

        public bool TraitAppliesToProduce(ThingDef plantDef, VarietyTraitDef traitDef)
        {
            return traitDef != null;
        }
        public float TraitWeight(ThingDef plantDef, VarietyTraitDef traitDef)
        {
            VarietyTraitDef root = TraitConfigUtility.Root(traitDef);
            PlantSettingsRecord record = GetEffectivePlantSettings(plantDef, false);
            TraitSettingsRecord traitSettings = record?.GetTraitSettings(root, false);
            float rootWeight = traitSettings != null && traitSettings.useCustomWeight ? traitSettings.weight : (GetGlobalTraitSettings(root, false)?.weight ?? DefaultWeight(root));
            if (TraitConfigUtility.IsSubtype(traitDef)) rootWeight *= GetFamilySettings(traitDef.configFamily, false)?.GetType(traitDef, false)?.weight ?? Mathf.Max(0f, traitDef.commonality);
            return rootWeight;
        }

        public IReadOnlyList<string> TraitTags(VarietyTraitDef traitDef)
        {
            if (traitDef == null) return new string[0];
            GlobalTraitSettingsRecord record = GetGlobalTraitSettings(traitDef, false);
            if (record?.tagsCustomized == true) return record.Tags;
            if (traitDef.traitTags != null && traitDef.traitTags.Count > 0) return traitDef.traitTags;
            if (traitDef.generated && !traitDef.configFamily.NullOrEmpty())
            {
                VarietyTraitDef root = TraitConfigUtility.Root(traitDef);
                if (root != traitDef) return TraitTags(root);
            }
            return new string[0];
        }

        public bool TraitHasTag(VarietyTraitDef traitDef, string tag)
        {
            return !tag.NullOrEmpty() && TraitTags(traitDef).Any(existing => existing.Equals(tag, System.StringComparison.OrdinalIgnoreCase));
        }

        public bool TraitTagsCustomized(VarietyTraitDef traitDef)
        {
            return GetGlobalTraitSettings(traitDef, false)?.tagsCustomized == true;
        }

        public void SetTraitTags(VarietyTraitDef traitDef, IEnumerable<string> tags)
        {
            if (traitDef != null) GetGlobalTraitSettings(traitDef).SetTags(tags);
        }

        public void ResetTraitTags(VarietyTraitDef traitDef)
        {
            GetGlobalTraitSettings(traitDef, false)?.ResetTags();
        }
        public VisualSettingsRecord VisualOverrideFor(ThingDef plantDef, VarietyTraitDef traitDef)
        {
            if (traitDef == null) return null;
            string key = (plantDef?.defName ?? string.Empty) + "|" + traitDef.defName;
            if (visualOverrideCache.TryGetValue(key, out VisualSettingsRecord cached)) return cached;
            if (noVisualOverrideCache.Contains(key)) return null;

            VarietyTraitDef root = TraitConfigUtility.Root(traitDef);
            FamilySettingsRecord family = TraitConfigUtility.IsSubtype(traitDef) ? GetFamilySettings(traitDef.configFamily, false) : null;
            OptionWeightRecord typeRecord = family?.useTypeSpecificVisuals == true ? family.GetType(traitDef, false) : null;
            TraitSettingsRecord plantTrait = GetEffectivePlantSettings(plantDef, false)?.GetTraitSettings(root, false);
            GlobalTraitSettingsRecord global = GetGlobalTraitSettings(root, false);
            VisualSettingsRecord result;
            if (family?.useTypeSpecificVisuals == true)
                result = typeRecord?.visualCustomized == true ? typeRecord.SharedVisual(traitDef) : null;
            else
                result = plantTrait?.useCustomVisual == true ? plantTrait.visual
                    : global?.visualCustomized == true ? global.visual : null;
            if (result == null) noVisualOverrideCache.Add(key);
            else visualOverrideCache[key] = result;
            return result;
        }

        public IReadOnlyList<VisualSettingsRecord> VisualInstancesFor(ThingDef plantDef, VarietyTraitDef traitDef)
        {
            if (traitDef == null) return new List<VisualSettingsRecord>();
            string key = (plantDef?.defName ?? string.Empty) + "|" + traitDef.defName;
            if (visualInstancesCache.TryGetValue(key, out List<VisualSettingsRecord> cached)) return cached;
            VarietyTraitDef root = TraitConfigUtility.Root(traitDef);
            FamilySettingsRecord family = TraitConfigUtility.IsSubtype(traitDef) ? GetFamilySettings(traitDef.configFamily, false) : null;
            OptionWeightRecord typeRecord = family?.useTypeSpecificVisuals == true ? family.GetType(traitDef, false) : null;
            TraitSettingsRecord plantTrait = GetEffectivePlantSettings(plantDef, false)?.GetTraitSettings(root, false);
            GlobalTraitSettingsRecord global = GetGlobalTraitSettings(root, false);
            List<VisualSettingsRecord> result;
            if (family?.useTypeSpecificVisuals == true)
                result = typeRecord != null ? typeRecord.VisualsFor(traitDef) : new OptionWeightRecord(traitDef.defName).VisualsFor(traitDef);
            else
                result = plantTrait?.useCustomVisual == true ? plantTrait.Visuals
                    : global != null ? global.Visuals : new GlobalTraitSettingsRecord(root).Visuals;
            if (traitDef.visualMaskIndex >= 0) result = ColorTraitFactory.ApplyIntrinsicColor(traitDef, result);
            visualInstancesCache[key] = result;
            return result;
        }

        public void ClearVisualCache()
        {
            visualOverrideCache.Clear();
            visualInstancesCache.Clear();
            noVisualOverrideCache.Clear();
        }

        private void EnsureLookupCaches()
        {
            if (!lookupCachesValid) RebuildLookupCaches();
        }

        private void RebuildLookupCaches()
        {
            plantSettingsCache.Clear();
            globalTraitSettingsCache.Clear();
            wildTraitSettingsCache.Clear();
            plantGroupCache.Clear();
            if (plantSettings != null)
                foreach (PlantSettingsRecord record in plantSettings)
                    if (record != null && !record.PlantDefName.NullOrEmpty()) plantSettingsCache[record.PlantDefName] = record;
            if (globalTraitSettings != null)
                foreach (GlobalTraitSettingsRecord record in globalTraitSettings)
                    if (record != null && !record.TraitDefName.NullOrEmpty()) globalTraitSettingsCache[record.TraitDefName] = record;
            if (wildTraitSettings != null)
                foreach (WildTraitSettingsRecord record in wildTraitSettings)
                    if (record != null && !record.TraitDefName.NullOrEmpty()) wildTraitSettingsCache[record.TraitDefName] = record;
            if (plantGroups != null)
                foreach (PlantGroupRecord group in plantGroups)
                    if (group != null)
                        foreach (string plantDefName in group.PlantDefNames)
                            if (!plantDefName.NullOrEmpty()) plantGroupCache[plantDefName] = group;
            lookupCachesValid = true;
        }

        public VisualSettingsRecord EffectiveVisualCopy(ThingDef plantDef, VarietyTraitDef traitDef)
        {
            return VisualOverrideFor(plantDef, traitDef)?.Clone() ?? new VisualSettingsRecord(traitDef);
        }

        public VisualSettingsRecord GlobalVisualCopy(VarietyTraitDef traitDef) => GlobalVisualCopies(traitDef).First();

        public List<VisualSettingsRecord> GlobalVisualCopies(VarietyTraitDef traitDef)
        {
            if (traitDef == null) return new List<VisualSettingsRecord> { new VisualSettingsRecord() };
            VarietyTraitDef root = TraitConfigUtility.Root(traitDef);
            GlobalTraitSettingsRecord global = GetGlobalTraitSettings(root, false);
            return (global != null ? global.Visuals : new GlobalTraitSettingsRecord(root).Visuals).Select(item => item.Clone()).ToList();
        }

        public static float DefaultWeight(VarietyTraitDef traitDef)
        {
            return traitDef == null ? 1f : Mathf.Max(0f, traitDef.commonality);
        }
    }

    public static class TraitConfigUtility
    {
        private static readonly Dictionary<VarietyTraitDef, VarietyTraitDef> RootCache = new Dictionary<VarietyTraitDef, VarietyTraitDef>();
        public static string Category(VarietyTraitDef trait)
        {
            if (trait == null) return "General";
            if (!trait.configCategory.NullOrEmpty()) return trait.configCategory;
            string tag = trait.exclusionTags?.FirstOrDefault() ?? "General";
            switch (tag)
            {
                case "color": return "Color";
                case "resin": return "Resin";
                case "size": return "Size";
                case "cold": case "heat": return "Climate";
                case "sowWork": case "harvestWork": case "skill": return "Labor";
                case "hardiness": case "disease": return "Hardiness";
                case "zone": case "fish": return "Habitat";
                case "lifecycle": return "Lifecycle";
                case "beauty": return "Appearance";
                case "specialNeed": return "Resource";
                case "synergy": return "Synergy";
                default: return char.ToUpperInvariant(tag[0]) + tag.Substring(1);
            }
        }

        public static bool IsSubtype(VarietyTraitDef trait) => trait != null && !trait.configFamily.NullOrEmpty() && !trait.configRoot;
        public static VarietyTraitDef Root(VarietyTraitDef trait)
        {
            if (trait == null || trait.configFamily.NullOrEmpty() || trait.configRoot) return trait;
            if (RootCache.TryGetValue(trait, out VarietyTraitDef cached)) return cached;
            VarietyTraitDef root = DefDatabase<VarietyTraitDef>.AllDefsListForReading.FirstOrDefault(t => t.configRoot && t.configFamily == trait.configFamily) ?? trait;
            RootCache[trait] = root;
            return root;
        }

        public static List<VarietyTraitDef> TopLevelTraits()
        {
            return DefDatabase<VarietyTraitDef>.AllDefsListForReading
                .Where(t => t != null && !t.hiddenFromConfig && !IsSubtype(t) && !t.generated)
                .OrderBy(t => Category(t)).ThenBy(t => t.label).ToList();
        }

        public static List<VarietyTraitDef> Types(string family)
        {
            return DefDatabase<VarietyTraitDef>.AllDefsListForReading
                .Where(t => t != null && t.configFamily == family && !t.configRoot && !t.generated)
                .OrderBy(t => t.configType.NullOrEmpty() ? t.label : t.configType).ToList();
        }
    }

    public class CategorySettingsRecord : IExposable
    {
        private string key;
        public bool enabled = true;
        public string Key => key;
        public CategorySettingsRecord() { }
        public CategorySettingsRecord(string key) { this.key = key; }
        public void ExposeData() { Scribe_Values.Look(ref key, "key"); Scribe_Values.Look(ref enabled, "enabled", true); }
    }

    public class OptionWeightRecord : IExposable
    {
        private static readonly string[] MaskVisualNames =
        {
            "Plant: Produce", "Plant: Leaves", "Plant: Stem",
            "Produce: Produce", "Produce: Leaves", "Produce: Container"
        };

        private string key;
        public bool enabled = true;
        public float weight = 1f;
        public bool visualCustomized;
        public bool usePerMaskVisuals = true;
        private VisualSettingsRecord visual;
        private List<VisualSettingsRecord> maskVisuals = new List<VisualSettingsRecord>();
        public string Key => key;

        public OptionWeightRecord() { }
        public OptionWeightRecord(string key, float weight = 1f) { this.key = key; this.weight = weight; }

        public void ExposeData()
        {
            Scribe_Values.Look(ref key, "key");
            Scribe_Values.Look(ref enabled, "enabled", true);
            Scribe_Values.Look(ref weight, "weight", 1f);
            Scribe_Values.Look(ref visualCustomized, "visualCustomized", false);
            Scribe_Values.Look(ref usePerMaskVisuals, "usePerMaskVisuals", true);
            Scribe_Deep.Look(ref visual, "visual");
            Scribe_Collections.Look(ref maskVisuals, "maskVisuals", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit) Normalize();
        }

        public void Normalize()
        {
            weight = Mathf.Clamp(weight, 0f, 20f);
            if (maskVisuals == null) maskVisuals = new List<VisualSettingsRecord>();
            maskVisuals.RemoveAll(item => item == null);
            if (visual != null)
            {
                ConfigureSharedVisual(visual);
                if (usePerMaskVisuals) EnsureMaskVisuals(null);
            }
        }

        public void EnsureVisual(VarietyTraitDef trait)
        {
            if (visual == null) visual = new VisualSettingsRecord(trait);
            ConfigureSharedVisual(visual);
            if (usePerMaskVisuals) EnsureMaskVisuals(trait);
        }

        public List<VisualSettingsRecord> VisualsFor(VarietyTraitDef trait)
        {
            EnsureVisual(trait);
            if (!usePerMaskVisuals) return new List<VisualSettingsRecord> { visual };
            EnsureMaskVisuals(trait);
            return maskVisuals;
        }

        public VisualSettingsRecord SharedVisual(VarietyTraitDef trait)
        {
            EnsureVisual(trait);
            return visual;
        }

        public VisualSettingsRecord VisualForMask(VarietyTraitDef trait, int maskIndex)
        {
            EnsureMaskVisuals(trait);
            return maskVisuals[Mathf.Clamp(maskIndex, 0, 5)];
        }

        public void SetPerMaskVisuals(VarietyTraitDef trait, bool value)
        {
            EnsureVisual(trait);
            if (value) EnsureMaskVisuals(trait);
            usePerMaskVisuals = value;
            visualCustomized = true;
        }

        public void ResetVisuals(VarietyTraitDef trait)
        {
            visual = new VisualSettingsRecord(trait);
            maskVisuals.Clear();
            usePerMaskVisuals = true;
            visualCustomized = false;
            ConfigureSharedVisual(visual);
            EnsureMaskVisuals(trait);
        }

        private void EnsureMaskVisuals(VarietyTraitDef trait)
        {
            EnsureVisualWithoutMasks(trait);
            bool fixedMasks = maskVisuals.Count == 6 && maskVisuals.Select(item => item.instanceName).SequenceEqual(MaskVisualNames);
            if (!fixedMasks) maskVisuals = Enumerable.Range(0, 6).Select(index => visual.Clone()).ToList();
            for (int i = 0; i < 6; i++) ConfigureMaskVisual(maskVisuals[i], i);
        }

        private void EnsureVisualWithoutMasks(VarietyTraitDef trait)
        {
            if (visual == null) visual = new VisualSettingsRecord(trait);
            ConfigureSharedVisual(visual);
            if (maskVisuals == null) maskVisuals = new List<VisualSettingsRecord>();
        }

        private static void ConfigureSharedVisual(VisualSettingsRecord item)
        {
            item.instanceName = "Shared Visual";
            item.targetPlantProduce = item.targetPlantLeaves = item.targetPlantStem = true;
            item.targetProduceProduce = item.targetProduceLeaves = item.targetProduceContainer = true;
            item.Normalize();
        }

        private static void ConfigureMaskVisual(VisualSettingsRecord item, int index)
        {
            item.instanceName = MaskVisualNames[index];
            item.targetPlantProduce = index == 0; item.targetPlantLeaves = index == 1; item.targetPlantStem = index == 2;
            item.targetProduceProduce = index == 3; item.targetProduceLeaves = index == 4; item.targetProduceContainer = index == 5;
            item.Normalize();
        }
    }
    public class FamilySettingsRecord : IExposable
    {
        private string key;
        public bool enabled = true;
        public bool useTypeSpecificVisuals = true;
        private List<OptionWeightRecord> types = new List<OptionWeightRecord>();
        private List<OptionWeightRecord> plants = new List<OptionWeightRecord>();
        private List<OptionWeightRecord> stats = new List<OptionWeightRecord>();
        public string Key => key;
        public bool HasCustomizedSubtypeVisual => types?.Any(record => record?.visualCustomized == true) == true;
        public FamilySettingsRecord() { }
        public FamilySettingsRecord(string key) { this.key = key; }
        public void ExposeData()
        {
            Scribe_Values.Look(ref key, "key"); Scribe_Values.Look(ref enabled, "enabled", true);
            Scribe_Values.Look(ref useTypeSpecificVisuals, "useTypeSpecificVisuals", true);
            Scribe_Collections.Look(ref types, "types", LookMode.Deep); Scribe_Collections.Look(ref plants, "plants", LookMode.Deep); Scribe_Collections.Look(ref stats, "stats", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit) Normalize();
        }
        public void Normalize() { if (types == null) types = new List<OptionWeightRecord>(); if (plants == null) plants = new List<OptionWeightRecord>(); if (stats == null) stats = new List<OptionWeightRecord>(); }
        private OptionWeightRecord Get(List<OptionWeightRecord> list, string optionKey, bool create, float defaultWeight)
        {
            OptionWeightRecord record = list.FirstOrDefault(x => x?.Key == optionKey);
            if (record == null && create) { record = new OptionWeightRecord(optionKey, defaultWeight); list.Add(record); }
            return record;
        }
        public OptionWeightRecord GetType(VarietyTraitDef trait, bool create = true)
        {
            OptionWeightRecord record = Get(types, trait.defName, create, Mathf.Max(0f, trait.commonality));
            record?.EnsureVisual(trait);
            return record;
        }
        public OptionWeightRecord GetPlant(ThingDef plant, bool create = true) => Get(plants, plant.defName, create, 1f);
        public OptionWeightRecord GetStat(string stat, bool create = true) => Get(stats, stat, create, 1f);
        public void ResetWeights()
        {
            foreach (OptionWeightRecord record in types)
            {
                VarietyTraitDef trait = DefDatabase<VarietyTraitDef>.GetNamedSilentFail(record.Key);
                record.weight = trait == null ? 1f : Mathf.Max(0f, trait.commonality);
            }
            foreach (OptionWeightRecord record in plants) record.weight = 1f;
            foreach (OptionWeightRecord record in stats) record.weight = 1f;
        }
    }

    public class WildTraitSettingsRecord : IExposable
    {
        private string traitDefName;
        public bool enabled = true;
        public float weight = 1f;
        public string TraitDefName => traitDefName;

        public WildTraitSettingsRecord() { }
        public WildTraitSettingsRecord(VarietyTraitDef traitDef)
        {
            traitDefName = traitDef?.defName;
            weight = NovelSeedsSettings.DefaultWeight(traitDef);
        }
        public bool Matches(VarietyTraitDef traitDef) => traitDef != null && traitDef.defName == traitDefName;
        public void ExposeData()
        {
            Scribe_Values.Look(ref traitDefName, "traitDef");
            Scribe_Values.Look(ref enabled, "enabled", true);
            Scribe_Values.Look(ref weight, "weight", 1f);
            if (Scribe.mode == LoadSaveMode.PostLoadInit) Normalize();
        }
        public void Normalize() { weight = Mathf.Clamp(weight, 0f, 20f); }
    }

    public class GlobalTraitSettingsRecord : IExposable
    {
        private static readonly string[] MaskVisualNames =
        {
            "Plant: Produce", "Plant: Leaves", "Plant: Stem",
            "Produce: Produce", "Produce: Leaves", "Produce: Container"
        };

        private string traitDefName;
        public float weight = 1f;
        public bool applyTraitToProduce;
        public bool visualCustomized;
        public bool usePerMaskVisuals = true;
        public VisualSettingsRecord visual = new VisualSettingsRecord();
        private List<VisualSettingsRecord> additionalVisuals = new List<VisualSettingsRecord>();
        public List<VisualSettingsRecord> Visuals => usePerMaskVisuals ? MaskVisuals : new List<VisualSettingsRecord> { visual };
        public List<VisualSettingsRecord> MaskVisuals
        {
            get
            {
                EnsureMaskVisuals();
                return additionalVisuals;
            }
        }
        public bool tagsCustomized;
        private List<string> tags = new List<string>();
        public bool tagExclusive;
        private List<string> exclusiveTags = new List<string>();
        public bool groupCustomized;
        private string group;

        public string TraitDefName => traitDefName;
        public IReadOnlyList<string> Tags => tags;
        public IReadOnlyList<string> ExclusiveTags => exclusiveTags;
        public string Group => group;

        public GlobalTraitSettingsRecord()
        {
        }

        public GlobalTraitSettingsRecord(VarietyTraitDef traitDef)
        {
            traitDefName = traitDef?.defName;
            weight = NovelSeedsSettings.DefaultWeight(traitDef);
            applyTraitToProduce = NovelSeedUtility.DefaultTraitAppliesToProduce(traitDef);
            visual = new VisualSettingsRecord(traitDef);
            visual.applyToProduce = true;
        }

        public bool Matches(VarietyTraitDef traitDef)
        {
            return traitDef != null && traitDef.defName == traitDefName;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref traitDefName, "traitDef");
            Scribe_Values.Look(ref weight, "weight", 1f);
            VarietyTraitDef traitDef = DefDatabase<VarietyTraitDef>.GetNamedSilentFail(traitDefName);
            Scribe_Values.Look(ref applyTraitToProduce, "applyTraitToProduce", NovelSeedUtility.DefaultTraitAppliesToProduce(traitDef));
            Scribe_Values.Look(ref visualCustomized, "visualCustomized", false);
            Scribe_Values.Look(ref usePerMaskVisuals, "usePerMaskVisuals", true);
            Scribe_Deep.Look(ref visual, "visual");
            Scribe_Collections.Look(ref additionalVisuals, "additionalVisuals", LookMode.Deep);
            Scribe_Values.Look(ref tagsCustomized, "tagsCustomized", false);
            Scribe_Collections.Look(ref tags, "tags", LookMode.Value);
            Scribe_Values.Look(ref tagExclusive, "tagExclusive", false);
            Scribe_Collections.Look(ref exclusiveTags, "exclusiveTags", LookMode.Value);
            Scribe_Values.Look(ref groupCustomized, "groupCustomized", false);
            Scribe_Values.Look(ref group, "group");
            if (Scribe.mode == LoadSaveMode.PostLoadInit) Normalize();
        }

        public void Normalize()
        {
            weight = Mathf.Clamp(weight, 0f, 20f);
            applyTraitToProduce = true;
            if (visual == null) visual = new VisualSettingsRecord(DefDatabase<VarietyTraitDef>.GetNamedSilentFail(traitDefName));
            if (additionalVisuals == null) additionalVisuals = new List<VisualSettingsRecord>();
            additionalVisuals.RemoveAll(item => item == null);
            visual.instanceName = "Shared Visual";
            visual.targetPlantProduce = visual.targetPlantLeaves = visual.targetPlantStem = true;
            visual.targetProduceProduce = visual.targetProduceLeaves = visual.targetProduceContainer = true;
            visual.applyToProduce = true;
            visual.Normalize();
            if (usePerMaskVisuals) EnsureMaskVisuals();
            foreach (VisualSettingsRecord item in additionalVisuals) item.applyToProduce = true;
            tags = NormalizeTags(tags);
            exclusiveTags = NormalizeTags(exclusiveTags);
            group = group?.Trim();
        }

        public void SetProduceInheritance(bool enabled)
        {
            applyTraitToProduce = enabled;
            visual.applyToProduce = enabled;
            foreach (VisualSettingsRecord item in additionalVisuals) item.applyToProduce = enabled;
        }
        public void SetPerMaskVisuals(bool enabled)
        {
            if (enabled) EnsureMaskVisuals();
            usePerMaskVisuals = enabled;
        }

        public VisualSettingsRecord VisualForMask(int maskIndex)
        {
            EnsureMaskVisuals();
            return additionalVisuals[Mathf.Clamp(maskIndex, 0, 5)];
        }

        public void CopyVisualsFrom(IEnumerable<VisualSettingsRecord> source)
        {
            List<VisualSettingsRecord> copies = source?.Where(item => item != null).Select(item => item.Clone()).ToList() ?? new List<VisualSettingsRecord>();
            if (copies.Count == 0) copies.Add(new VisualSettingsRecord(DefDatabase<VarietyTraitDef>.GetNamedSilentFail(traitDefName)));
            if (copies.Count == 6 && copies[0].instanceName.StartsWith("Plant:"))
            {
                usePerMaskVisuals = true;
                visual = copies[0].Clone();
                additionalVisuals = copies;
            }
            else
            {
                usePerMaskVisuals = false;
                visual = copies[0];
                additionalVisuals.Clear();
            }
            Normalize();
        }

        private void EnsureMaskVisuals()
        {
            if (additionalVisuals == null) additionalVisuals = new List<VisualSettingsRecord>();
            List<VisualSettingsRecord> oldVisuals = new[] { visual }.Concat(additionalVisuals).Where(item => item != null).ToList();
            bool alreadyFixed = additionalVisuals.Count == 6 && additionalVisuals.Select(item => item.instanceName).SequenceEqual(MaskVisualNames);
            if (!alreadyFixed)
            {
                additionalVisuals = Enumerable.Range(0, 6).Select(index =>
                {
                    VisualSettingsRecord source = oldVisuals.FirstOrDefault(item => index < 3 ? item.TargetsPlantMask(index) : item.TargetsProduceMask(index - 3)) ?? visual;
                    return source.Clone();
                }).ToList();
            }
            for (int i = 0; i < 6; i++) ConfigureMaskVisual(additionalVisuals[i], i);
        }

        private static void ConfigureMaskVisual(VisualSettingsRecord item, int index)
        {
            item.instanceName = MaskVisualNames[index];
            item.targetPlantProduce = index == 0; item.targetPlantLeaves = index == 1; item.targetPlantStem = index == 2;
            item.targetProduceProduce = index == 3; item.targetProduceLeaves = index == 4; item.targetProduceContainer = index == 5;
            item.Normalize();
        }
        public void ResetVisuals(VarietyTraitDef traitDef)
        {
            visual = new VisualSettingsRecord(traitDef);
            additionalVisuals.Clear();
            usePerMaskVisuals = true;
            visualCustomized = false;
            Normalize();
        }

        public void SetTags(IEnumerable<string> values)
        {
            tags = NormalizeTags(values);
            tagsCustomized = true;
        }

        public void ResetTags()
        {
            tagsCustomized = false;
            tags.Clear();
        }

        public void SetExclusiveTag(string tag, bool enabled)
        {
            string normalized = tag?.Trim();
            if (normalized.NullOrEmpty()) return;
            exclusiveTags.RemoveAll(item => item.Equals(normalized, System.StringComparison.OrdinalIgnoreCase));
            if (enabled) exclusiveTags.Add(normalized);
            exclusiveTags = NormalizeTags(exclusiveTags);
        }

        public void SetGroup(string value)
        {
            group = value?.Trim();
            groupCustomized = true;
        }

        public void ResetGroup()
        {
            groupCustomized = false;
            group = null;
        }
        private static List<string> NormalizeTags(IEnumerable<string> values)
        {
            List<string> result = new List<string>();
            if (values == null) return result;
            foreach (string value in values)
            {
                string normalized = value?.Trim();
                if (!normalized.NullOrEmpty() && !result.Any(existing => existing.Equals(normalized, System.StringComparison.OrdinalIgnoreCase))) result.Add(normalized);
            }
            return result.OrderBy(value => value).ToList();
        }
    }
    public class PlantGroupRecord : IExposable
    {
        private string id;
        private string name;
        private List<string> plantDefNames = new List<string>();
        private PlantSettingsRecord settings = new PlantSettingsRecord();

        public string Id => id;
        public string Name => name;
        public PlantSettingsRecord Settings => settings;
        public int PlantCount => Plants.Count();
        internal IReadOnlyList<string> PlantDefNames => plantDefNames;

        public PlantGroupRecord() { }
        public PlantGroupRecord(string id, string name) { this.id = id; this.name = name; }

        public void SetName(string value) { name = value?.Trim(); }
        public bool Contains(ThingDef plantDef) { return NovelSeedUtility.IsGrowableCrop(plantDef) && plantDefNames.Contains(plantDef.defName); }
        public void Add(ThingDef plantDef) { if (plantDef != null && !plantDefNames.Contains(plantDef.defName)) plantDefNames.Add(plantDef.defName); }
        public void Remove(ThingDef plantDef) { if (plantDef != null) plantDefNames.Remove(plantDef.defName); }

        public IEnumerable<ThingDef> Plants
        {
            get
            {
                foreach (string defName in plantDefNames)
                {
                    ThingDef plant = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
                    if (NovelSeedUtility.IsGrowableCrop(plant)) yield return plant;
                }
            }
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref id, "id");
            Scribe_Values.Look(ref name, "name");
            Scribe_Collections.Look(ref plantDefNames, "plantDefNames", LookMode.Value);
            Scribe_Deep.Look(ref settings, "settings");
            if (Scribe.mode == LoadSaveMode.PostLoadInit) Normalize();
        }

        public void Normalize()
        {
            if (plantDefNames == null) plantDefNames = new List<string>();
            plantDefNames = plantDefNames.Where(defName => !defName.NullOrEmpty()).Distinct().ToList();
            if (settings == null) settings = new PlantSettingsRecord();
            settings.Normalize();
        }

        public void RemoveAlreadyClaimedPlants(HashSet<string> claimed)
        {
            plantDefNames.RemoveAll(defName => !claimed.Add(defName));
        }
    }
    public class PlantTagOverrideRecord : IExposable
    {
        private string plantDefName;
        private List<string> addedTags = new List<string>();
        private List<string> removedTags = new List<string>();

        public string PlantDefName => plantDefName;
        public IReadOnlyList<string> AddedTags => addedTags;
        public IReadOnlyList<string> RemovedTags => removedTags;
        public bool IsEmpty => addedTags.Count == 0 && removedTags.Count == 0;

        public PlantTagOverrideRecord() { }
        public PlantTagOverrideRecord(ThingDef plantDef) { plantDefName = plantDef?.defName; }
        public bool Matches(ThingDef plantDef) => plantDef != null && plantDef.defName == plantDefName;

        public void ExposeData()
        {
            Scribe_Values.Look(ref plantDefName, "plantDef");
            Scribe_Collections.Look(ref addedTags, "addedTags", LookMode.Value);
            Scribe_Collections.Look(ref removedTags, "removedTags", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit) Normalize();
        }

        public void Normalize()
        {
            addedTags = NormalizeTags(addedTags);
            removedTags = NormalizeTags(removedTags);
            removedTags.RemoveAll(tag => addedTags.Any(added => added.Equals(tag, System.StringComparison.OrdinalIgnoreCase)));
        }

        public void Set(string tag, bool enabled, bool inferred)
        {
            Clear(tag);
            if (enabled != inferred)
            {
                if (enabled) addedTags.Add(tag);
                else removedTags.Add(tag);
            }
            Normalize();
        }

        public void Clear(string tag)
        {
            addedTags.RemoveAll(item => item.Equals(tag, System.StringComparison.OrdinalIgnoreCase));
            removedTags.RemoveAll(item => item.Equals(tag, System.StringComparison.OrdinalIgnoreCase));
        }

        private static List<string> NormalizeTags(IEnumerable<string> tags)
        {
            return (tags ?? Enumerable.Empty<string>()).Where(tag => !tag.NullOrEmpty()).Select(tag => tag.Trim())
                .Distinct(System.StringComparer.OrdinalIgnoreCase).OrderBy(tag => tag).ToList();
        }
    }
    public class PlantSettingsRecord : IExposable
    {
        private string plantDefName;
        public bool useCustomMutationChance;
        public float mutationChance = NovelSeedUtility.SpontaneousMutationChance;
        public bool useCustomCrossPollinationChance;
        public float crossPollinationChance = NovelSeedUtility.DefaultCrossPollinationChance;
        public bool usePlantMasks;
        public bool disableAutoPlantMasks;
        private List<VisualMaskLayerRecord> plantMaskLayers = new List<VisualMaskLayerRecord>();
        private List<PlantMaskVariationRecord> plantMaskVariations = new List<PlantMaskVariationRecord>();
        public bool useProduceMasks;
        public bool unrestrictedColors;
        private List<VisualMaskLayerRecord> produceMaskLayers = new List<VisualMaskLayerRecord>();
        private List<CategorySettingsRecord> traitGroupSettings = new List<CategorySettingsRecord>();
        private List<TraitSettingsRecord> traitSettings = new List<TraitSettingsRecord>();
        [Unsaved(false)] private Dictionary<string, CategorySettingsRecord> traitGroupSettingsCache;
        [Unsaved(false)] private Dictionary<string, TraitSettingsRecord> traitSettingsCache;

        public string PlantDefName => plantDefName;
        public List<VisualMaskLayerRecord> PlantMaskLayers => plantMaskLayers ?? (plantMaskLayers = new List<VisualMaskLayerRecord>());
        public List<VisualMaskLayerRecord> ProduceMaskLayers => produceMaskLayers ?? (produceMaskLayers = new List<VisualMaskLayerRecord>());
        public bool HasActivePlantMasks => usePlantMasks;
        public bool HasAnyManualPlantMask => PlantMaskLayers.Any(layer => layer.HasPixels)
            || (plantMaskVariations?.Any(record => record.Layers.Any(layer => layer.HasPixels)) == true);
        public bool HasActiveProduceMasks => useProduceMasks;

        public PlantSettingsRecord()
        {
        }

        public PlantSettingsRecord(ThingDef plantDef)
        {
            plantDefName = plantDef?.defName;
        }

        public bool Matches(ThingDef plantDef)
        {
            return plantDef != null && plantDef.defName == plantDefName;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref plantDefName, "plantDef");
            Scribe_Values.Look(ref useCustomMutationChance, "useCustomMutationChance", false);
            Scribe_Values.Look(ref mutationChance, "mutationChance", NovelSeedUtility.SpontaneousMutationChance);
            Scribe_Values.Look(ref useCustomCrossPollinationChance, "useCustomCrossPollinationChance", false);
            Scribe_Values.Look(ref crossPollinationChance, "crossPollinationChance", NovelSeedUtility.DefaultCrossPollinationChance);
            Scribe_Values.Look(ref usePlantMasks, "usePlantMasks", false);
            Scribe_Values.Look(ref disableAutoPlantMasks, "disableAutoPlantMasks", false);
            Scribe_Collections.Look(ref plantMaskLayers, "plantMaskLayers", LookMode.Deep);
            Scribe_Collections.Look(ref plantMaskVariations, "plantMaskVariations", LookMode.Deep);
            Scribe_Values.Look(ref useProduceMasks, "useProduceMasks", false);
            Scribe_Values.Look(ref unrestrictedColors, "unrestrictedColors", false);
            Scribe_Collections.Look(ref produceMaskLayers, "produceMaskLayers", LookMode.Deep);
            Scribe_Collections.Look(ref traitGroupSettings, "traitGroupSettings", LookMode.Deep);
            Scribe_Collections.Look(ref traitSettings, "traitSettings", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (traitSettings == null)
                {
                    traitSettings = new List<TraitSettingsRecord>();
                }
                if (traitGroupSettings == null) traitGroupSettings = new List<CategorySettingsRecord>();
                if (plantMaskLayers == null) plantMaskLayers = new List<VisualMaskLayerRecord>();
                if (plantMaskVariations == null) plantMaskVariations = new List<PlantMaskVariationRecord>();
                if (produceMaskLayers == null) produceMaskLayers = new List<VisualMaskLayerRecord>();
                Normalize();
            }
        }

        public void Normalize()
        {
            mutationChance = Mathf.Clamp01(mutationChance);
            crossPollinationChance = Mathf.Clamp01(crossPollinationChance);
            NormalizeFixedMasks(PlantMaskLayers, "Produce", "Leaves", "Stem");
            if (plantMaskVariations == null) plantMaskVariations = new List<PlantMaskVariationRecord>();
            plantMaskVariations.RemoveAll(record => record == null);
            foreach (PlantMaskVariationRecord record in plantMaskVariations) record.Normalize();
            plantMaskVariations = plantMaskVariations.GroupBy(record => record.VariationIndex).Select(group => group.First()).OrderBy(record => record.VariationIndex).ToList();
            NormalizeFixedMasks(ProduceMaskLayers, "Produce", "Leaves", "Container");
            if (traitGroupSettings == null) traitGroupSettings = new List<CategorySettingsRecord>();
            traitGroupSettings.RemoveAll(record => record == null || record.Key.NullOrEmpty());
            if (traitSettings == null)
            {
                traitSettings = new List<TraitSettingsRecord>();
            }
            traitSettings.RemoveAll(record => record == null || string.IsNullOrEmpty(record.TraitDefName));
            foreach (TraitSettingsRecord record in traitSettings)
            {
                record.Normalize();
            }
            RebuildTraitCaches();
            SharedManualMaskCache.Invalidate();
        }

        public void ResetToDefaults()
        {
            useCustomMutationChance = false;
            mutationChance = NovelSeedUtility.SpontaneousMutationChance;
            useCustomCrossPollinationChance = false;
            crossPollinationChance = NovelSeedUtility.DefaultCrossPollinationChance;
            usePlantMasks = false;
            disableAutoPlantMasks = false;
            PlantMaskLayers.Clear();
            plantMaskVariations.Clear();
            useProduceMasks = false;
            unrestrictedColors = false;
            ProduceMaskLayers.Clear();
            if (traitSettings == null)
            {
                traitSettings = new List<TraitSettingsRecord>();
            }
            if (traitGroupSettings == null) traitGroupSettings = new List<CategorySettingsRecord>();
            traitGroupSettings.Clear();
            traitSettings.Clear();
            RebuildTraitCaches();
            SharedManualMaskCache.Invalidate();
        }

        private static void NormalizeFixedMasks(List<VisualMaskLayerRecord> layers, string firstName, string secondName, string thirdName)
        {
            layers.RemoveAll(layer => layer == null);
            while (layers.Count < 3) layers.Add(new VisualMaskLayerRecord());
            if (layers.Count > 3) layers.RemoveRange(3, layers.Count - 3);
            layers[0].name = firstName;
            layers[1].name = secondName;
            layers[2].name = thirdName;
            layers[0].Normalize();
            layers[1].Normalize();
            layers[2].Normalize();
        }

        public List<VisualMaskLayerRecord> PlantMaskLayersForVariation(int variationIndex, bool create = true)
        {
            if (variationIndex <= 0) return PlantMaskLayers;
            if (plantMaskVariations == null) plantMaskVariations = new List<PlantMaskVariationRecord>();
            PlantMaskVariationRecord record = plantMaskVariations.FirstOrDefault(item => item.VariationIndex == variationIndex);
            if (record == null && create)
            {
                record = new PlantMaskVariationRecord(variationIndex, PlantMaskLayers);
                plantMaskVariations.Add(record);
            }
            return record?.Layers ?? PlantMaskLayers;
        }

        public List<VisualMaskLayerRecord> ManualPlantMaskLayersForVariation(int variationIndex)
        {
            if (variationIndex <= 0) return PlantMaskLayers;
            return plantMaskVariations?.FirstOrDefault(item => item.VariationIndex == variationIndex)?.Layers ?? PlantMaskLayers;
        }

        public bool HasManualPlantMask(int variationIndex)
        {
            return ManualPlantMaskLayersForVariation(variationIndex)?.Any(layer => layer?.HasPixels == true) == true;
        }

        public List<VisualMaskLayerRecord> SetManualPlantMask(int variationIndex, IEnumerable<VisualMaskLayerRecord> source)
        {
            List<VisualMaskLayerRecord> replacement = source?.Select(layer => layer?.Clone()).Where(layer => layer != null).ToList()
                ?? new List<VisualMaskLayerRecord>();
            NormalizeFixedMasks(replacement, "Produce", "Leaves", "Stem");
            if (variationIndex <= 0) plantMaskLayers = replacement;
            else
            {
                if (plantMaskVariations == null) plantMaskVariations = new List<PlantMaskVariationRecord>();
                plantMaskVariations.RemoveAll(record => record.VariationIndex == variationIndex);
                plantMaskVariations.Add(new PlantMaskVariationRecord(variationIndex, replacement));
            }
            usePlantMasks = true;
            disableAutoPlantMasks = false;
            SharedManualMaskCache.Invalidate();
            return ManualPlantMaskLayersForVariation(variationIndex);
        }

        public void RemoveManualPlantMask(int variationIndex)
        {
            if (variationIndex <= 0)
            {
                foreach (VisualMaskLayerRecord layer in PlantMaskLayers) layer.Clear();
            }
            else
            {
                if (plantMaskVariations == null) plantMaskVariations = new List<PlantMaskVariationRecord>();
                plantMaskVariations.RemoveAll(record => record.VariationIndex == variationIndex);
                plantMaskVariations.Add(new PlantMaskVariationRecord(variationIndex, new[]
                {
                    new VisualMaskLayerRecord { name = "Produce" },
                    new VisualMaskLayerRecord { name = "Leaves" },
                    new VisualMaskLayerRecord { name = "Stem" }
                }));
            }
            SharedManualMaskCache.Invalidate();
        }

        public void EnsurePlantMaskVariationCount(int count)
        {
            for (int i = 1; i < Mathf.Max(1, count); i++) PlantMaskLayersForVariation(i);
        }

        public bool AnyPlantMaskLayerHasPixels(int layerIndex)
        {
            int index = Mathf.Clamp(layerIndex, 0, 2);
            if (PlantMaskLayers[index].HasPixels) return true;
            return plantMaskVariations != null && plantMaskVariations.Any(record => record.Layers[index].HasPixels);
        }

        public IEnumerable<List<VisualMaskLayerRecord>> AllPlantMaskVariations()
        {
            yield return PlantMaskLayers;
            if (plantMaskVariations != null)
                foreach (PlantMaskVariationRecord record in plantMaskVariations.OrderBy(item => item.VariationIndex)) yield return record.Layers;
        }
        internal IEnumerable<PlantMaskVariationRecord> PlantMaskVariationRecords => plantMaskVariations ?? Enumerable.Empty<PlantMaskVariationRecord>();

        internal bool HasMaskData => usePlantMasks || useProduceMasks || PlantMaskLayers.Any(layer => layer.HasPixels)
            || ProduceMaskLayers.Any(layer => layer.HasPixels)
            || PlantMaskVariationRecords.Any(record => record.Layers.Any(layer => layer.HasPixels));

        internal void ReplaceMasks(bool enablePlantMasks, IEnumerable<VisualMaskLayerRecord> plantLayers,
            IEnumerable<PlantMaskVariationRecord> variations, bool enableProduceMasks, IEnumerable<VisualMaskLayerRecord> produceLayers)
        {
            usePlantMasks = enablePlantMasks;
            plantMaskLayers = plantLayers?.Select(layer => layer?.Clone()).Where(layer => layer != null).ToList() ?? new List<VisualMaskLayerRecord>();
            plantMaskVariations = variations?.Where(record => record != null)
                .Select(record => new PlantMaskVariationRecord(record.VariationIndex, record.Layers)).ToList() ?? new List<PlantMaskVariationRecord>();
            useProduceMasks = enableProduceMasks;
            produceMaskLayers = produceLayers?.Select(layer => layer?.Clone()).Where(layer => layer != null).ToList() ?? new List<VisualMaskLayerRecord>();
            Normalize();
        }

        internal void ClearMasks()
        {
            ReplaceMasks(false, null, null, false, null);
        }
        public CategorySettingsRecord GetTraitGroupSettings(string key, bool create = true)
        {
            if (key.NullOrEmpty()) return null;
            if (traitGroupSettings == null) traitGroupSettings = new List<CategorySettingsRecord>();
            EnsureTraitCaches();
            traitGroupSettingsCache.TryGetValue(key, out CategorySettingsRecord record);
            if (record == null && create)
            {
                record = new CategorySettingsRecord(key);
                traitGroupSettings.Add(record);
                traitGroupSettingsCache[key] = record;
            }
            return record;
        }

        public TraitSettingsRecord GetTraitSettings(VarietyTraitDef traitDef, bool create = true)
        {
            if (traitDef == null)
            {
                return null;
            }
            if (traitSettings == null)
            {
                traitSettings = new List<TraitSettingsRecord>();
            }

            EnsureTraitCaches();
            traitSettingsCache.TryGetValue(traitDef.defName, out TraitSettingsRecord record);
            if (record == null && create)
            {
                record = new TraitSettingsRecord(traitDef);
                traitSettings.Add(record);
                traitSettingsCache[traitDef.defName] = record;
            }
            return record;
        }

        private void EnsureTraitCaches()
        {
            if (traitSettingsCache == null) RebuildTraitCaches();
        }

        private void RebuildTraitCaches()
        {
            traitGroupSettingsCache = new Dictionary<string, CategorySettingsRecord>();
            traitSettingsCache = new Dictionary<string, TraitSettingsRecord>();
            if (traitGroupSettings != null)
                foreach (CategorySettingsRecord record in traitGroupSettings)
                    if (record != null && !record.Key.NullOrEmpty()) traitGroupSettingsCache[record.Key] = record;
            if (traitSettings != null)
                foreach (TraitSettingsRecord record in traitSettings)
                    if (record != null && !record.TraitDefName.NullOrEmpty()) traitSettingsCache[record.TraitDefName] = record;
        }
    }

    public class TraitSettingsRecord : IExposable
    {
        private static readonly string[] MaskVisualNames =
        {
            "Plant: Produce", "Plant: Leaves", "Plant: Stem",
            "Produce: Produce", "Produce: Leaves", "Produce: Container"
        };

        private string traitDefName;
        public bool enabled = true;
        public bool useCustomWeight;
        public float weight = 1f;
        public bool useCustomProduceInheritance;
        public bool applyTraitToProduce;
        public bool useCustomVisual;
        public bool usePerMaskVisuals = true;
        public VisualSettingsRecord visual = new VisualSettingsRecord();
        private List<VisualSettingsRecord> additionalVisuals = new List<VisualSettingsRecord>();
        public List<VisualSettingsRecord> Visuals => usePerMaskVisuals ? MaskVisuals : new List<VisualSettingsRecord> { visual };
        public List<VisualSettingsRecord> MaskVisuals
        {
            get
            {
                EnsureMaskVisuals();
                return additionalVisuals;
            }
        }

        public string TraitDefName => traitDefName;

        public TraitSettingsRecord()
        {
        }

        public TraitSettingsRecord(VarietyTraitDef traitDef)
        {
            traitDefName = traitDef?.defName;
            weight = NovelSeedsSettings.DefaultWeight(traitDef);
            applyTraitToProduce = NovelSeedUtility.DefaultTraitAppliesToProduce(traitDef);
            visual = new VisualSettingsRecord(traitDef);
            visual.applyToProduce = true;
        }

        public bool Matches(VarietyTraitDef traitDef)
        {
            return traitDef != null && traitDef.defName == traitDefName;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref traitDefName, "traitDef");
            Scribe_Values.Look(ref enabled, "enabled", true);
            Scribe_Values.Look(ref useCustomWeight, "useCustomWeight", false);
            Scribe_Values.Look(ref weight, "weight", 1f);
            VarietyTraitDef traitDef = DefDatabase<VarietyTraitDef>.GetNamedSilentFail(traitDefName);
            Scribe_Values.Look(ref useCustomProduceInheritance, "useCustomProduceInheritance", false);
            Scribe_Values.Look(ref applyTraitToProduce, "applyTraitToProduce", NovelSeedUtility.DefaultTraitAppliesToProduce(traitDef));
            Scribe_Values.Look(ref useCustomVisual, "useCustomVisual", false);
            Scribe_Values.Look(ref usePerMaskVisuals, "usePerMaskVisuals", true);
            Scribe_Deep.Look(ref visual, "visual");
            Scribe_Collections.Look(ref additionalVisuals, "additionalVisuals", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                Normalize();
            }
        }

        public void Normalize()
        {
            weight = Mathf.Clamp(weight, 0f, 20f);
            applyTraitToProduce = true;
            if (visual == null) visual = new VisualSettingsRecord(DefDatabase<VarietyTraitDef>.GetNamedSilentFail(traitDefName));
            if (additionalVisuals == null) additionalVisuals = new List<VisualSettingsRecord>();
            additionalVisuals.RemoveAll(item => item == null);
            visual.instanceName = "Shared Visual";
            visual.targetPlantProduce = visual.targetPlantLeaves = visual.targetPlantStem = true;
            visual.targetProduceProduce = visual.targetProduceLeaves = visual.targetProduceContainer = true;
            visual.Normalize();
            if (usePerMaskVisuals)
            {
                EnsureMaskVisuals();
                SyncProduceSettingsToMasks();
            }
        }

        public void SetProduceInheritance(bool enabled)
        {
            useCustomProduceInheritance = true;
            applyTraitToProduce = enabled;
            visual.applyToProduce = enabled;
            foreach (VisualSettingsRecord item in additionalVisuals) item.applyToProduce = enabled;
        }
        public void SetPerMaskVisuals(bool enabled, IEnumerable<VisualSettingsRecord> initialVisuals = null)
        {
            if (enabled && !HasInitializedMaskVisuals())
            {
                VisualSettingsRecord baseline = initialVisuals?.FirstOrDefault(item => item != null)?.Clone() ?? visual.Clone();
                additionalVisuals = Enumerable.Range(0, 6).Select(_ => baseline.Clone()).ToList();
                for (int i = 0; i < 6; i++) ConfigureMaskVisual(additionalVisuals[i], i);
            }
            if (enabled) EnsureMaskVisuals();
            usePerMaskVisuals = enabled;
            if (enabled) SyncProduceSettingsToMasks();
        }

        private bool HasInitializedMaskVisuals()
        {
            return additionalVisuals?.Count == 6
                && additionalVisuals.Select(item => item?.instanceName).SequenceEqual(MaskVisualNames);
        }

        public VisualSettingsRecord VisualForMask(int maskIndex)
        {
            EnsureMaskVisuals();
            return additionalVisuals[Mathf.Clamp(maskIndex, 0, 5)];
        }

        public void SyncProduceSettingsToMasks()
        {
            if (!usePerMaskVisuals) return;
            EnsureMaskVisuals();
            foreach (VisualSettingsRecord maskVisual in additionalVisuals) maskVisual.CopyProduceSettingsFrom(visual);
        }

        public void CopyVisualsFrom(IEnumerable<VisualSettingsRecord> source)
        {
            List<VisualSettingsRecord> copies = source?.Where(item => item != null).Select(item => item.Clone()).ToList() ?? new List<VisualSettingsRecord>();
            if (copies.Count == 0) copies.Add(new VisualSettingsRecord(DefDatabase<VarietyTraitDef>.GetNamedSilentFail(traitDefName)));
            if (copies.Count == 6 && copies[0].instanceName.StartsWith("Plant:"))
            {
                usePerMaskVisuals = true;
                visual = copies[0].Clone();
                additionalVisuals = copies;
            }
            else
            {
                usePerMaskVisuals = false;
                visual = copies[0];
                additionalVisuals.Clear();
            }
            Normalize();
        }

        private void EnsureMaskVisuals()
        {
            if (additionalVisuals == null) additionalVisuals = new List<VisualSettingsRecord>();
            List<VisualSettingsRecord> oldVisuals = new[] { visual }.Concat(additionalVisuals).Where(item => item != null).ToList();
            bool alreadyFixed = additionalVisuals.Count == 6 && additionalVisuals.Select(item => item.instanceName).SequenceEqual(MaskVisualNames);
            if (!alreadyFixed)
            {
                additionalVisuals = Enumerable.Range(0, 6).Select(index =>
                {
                    VisualSettingsRecord source = oldVisuals.FirstOrDefault(item => index < 3 ? item.TargetsPlantMask(index) : item.TargetsProduceMask(index - 3)) ?? visual;
                    return source.Clone();
                }).ToList();
            }
            for (int i = 0; i < 6; i++) ConfigureMaskVisual(additionalVisuals[i], i);
        }

        private static void ConfigureMaskVisual(VisualSettingsRecord item, int index)
        {
            item.instanceName = MaskVisualNames[index];
            item.targetPlantProduce = index == 0; item.targetPlantLeaves = index == 1; item.targetPlantStem = index == 2;
            item.targetProduceProduce = index == 3; item.targetProduceLeaves = index == 4; item.targetProduceContainer = index == 5;
            item.Normalize();
        }
    }

    public enum FamilyOptionMode { Types, Plants, Stats }

    public class Dialog_TraitFamilyOptions : Window
    {
        private readonly VarietyTraitDef root;
        private readonly FamilyOptionMode mode;
        private readonly ThingDef previewPlant;
        private string search = string.Empty;
        private HorticultureCollectionDialogDocument canvasDocument;
        private HorticultureCollectionDialogSurfaceAdapter canvasSurface;
        public override Vector2 InitialSize => new Vector2(700f, 720f);

        public Dialog_TraitFamilyOptions(VarietyTraitDef root, FamilyOptionMode mode, ThingDef previewPlant = null)
        {
            this.root = root;
            this.mode = mode;
            this.previewPlant = previewPlant;
            doCloseX = true;
            closeOnClickedOutside = false;
            absorbInputAroundWindow = true;
            canvasSurface = new HorticultureCollectionDialogSurfaceAdapter
            {
                TitleProvider = () => root.LabelCap + " - " + (mode == FamilyOptionMode.Types ? "Subtypes" : mode == FamilyOptionMode.Plants ? "Plants" : "Stats"),
                DescriptionProvider = () => "Weighted family selection. Toggle options to preserve the existing settings records; visual editing remains in the Visual Designer.",
                SearchProvider = () => search,
                SearchSetter = value => search = value,
                RowsProvider = OptionRows,
                EmptyProvider = () => "No matching options.",
                PrimaryLabelProvider = () => "Close",
                PrimaryActionCallback = () => Close(),
                CloseAction = () => Close()
            };
            canvasDocument = new HorticultureCollectionDialogDocument(canvasSurface, "hns.family-options");
        }

        private ThingDef PreviewPlant => previewPlant ?? NovelSeedsSettingsUI.CurrentPlantPreview;

        public override void DoWindowContents(Rect inRect) => canvasDocument?.Draw(inRect);

        public override void PostClose()
        {
            canvasDocument?.PostClose();
            base.PostClose();
        }

        private IReadOnlyList<HorticultureDialogRow> OptionRows()
        {
            FamilySettingsRecord family = HorticultureNovelSeedsMod.Settings.GetFamilySettings(root.configFamily);
            List<HorticultureDialogRow> rows = new List<HorticultureDialogRow>
            {
                new HorticultureDialogRow
                {
                    Id = "family-enabled",
                    Label = "Enable Family",
                    Detail = family.enabled ? "Family is active" : "Family is disabled",
                    Status = family.enabled ? "Enabled" : "Disabled",
                    Selected = family.enabled,
                    CanToggle = true,
                    Toggle = value => family.enabled = value
                }
            };
            if (mode == FamilyOptionMode.Types)
            {
                rows.Add(new HorticultureDialogRow
                {
                    Id = "family-type-specific-visuals",
                    Label = "Use subtype-specific visuals",
                    Detail = family.useTypeSpecificVisuals
                        ? "Each subtype can override the shared family visual."
                        : "All subtypes use the shared family visual.",
                    Status = family.useTypeSpecificVisuals ? "Overrides enabled" : "Shared visual",
                    Selected = family.useTypeSpecificVisuals,
                    CanToggle = true,
                    Toggle = value =>
                    {
                        if (family.useTypeSpecificVisuals == value) return;
                        family.useTypeSpecificVisuals = value;
                        HorticultureNovelSeedsMod.Settings.ClearVisualCache();
                        ProduceMaskRenderer.ClearAll();
                    },
                    ActionLabel = "Edit shared visual",
                    Activate = () => Find.WindowStack.Add(new Dialog_TraitVisualDesigner(
                        HorticultureNovelSeedsMod.Settings, root, PreviewPlant, true))
                });
            }
            rows.AddRange(BuildOptions(family).Take(1000).Select((option, index) => new HorticultureDialogRow
            {
                Id = option.label ?? "option-" + index,
                Label = option.label,
                Detail = "Weight " + option.record.weight.ToString("0.##"),
                Status = option.record.enabled ? "Enabled" : "Disabled",
                Selected = option.record.enabled,
                CanToggle = true,
                Toggle = value => option.record.enabled = value,
                ActionLabel = option.trait == null ? null : "Edit visual",
                HasValue = true,
                Value = option.record.weight,
                Minimum = 0f,
                Maximum = 20f,
                ValueChanged = value =>
                {
                    option.record.weight = value;
                    option.record.Normalize();
                },
                Activate = option.trait == null || mode != FamilyOptionMode.Types ? null : () =>
                    Find.WindowStack.Add(new Dialog_TraitVisualDesigner(HorticultureNovelSeedsMod.Settings, option.trait, option.record, PreviewPlant))
            }));
            return rows;
        }


        private List<OptionDisplay> BuildOptions(FamilySettingsRecord family)
        {
            List<OptionDisplay> result = new List<OptionDisplay>();
            if (mode == FamilyOptionMode.Types)
            {
                foreach (VarietyTraitDef trait in TraitConfigUtility.Types(root.configFamily))
                    result.Add(new OptionDisplay(ColorTraitFactory.IsColorFamily(trait.configFamily) ? TraitColorUI.Label(trait)
                        : trait.configType.NullOrEmpty() ? trait.LabelCap.ToString() : trait.configType, family.GetType(trait), trait));
            }
            else if (mode == FamilyOptionMode.Plants)
            {
                foreach (ThingDef plant in DefDatabase<ThingDef>.AllDefsListForReading.Where(NovelSeedUtility.IsGrowableCrop).OrderBy(plant => plant.label)) result.Add(new OptionDisplay(plant.LabelCap, family.GetPlant(plant)));
            }
            else
            {
                foreach (string stat in SynergyTraitFactory.StatOptions) result.Add(new OptionDisplay(SynergyTraitFactory.StatLabel(stat), family.GetStat(stat)));
            }
            return result;
        }

        private class OptionDisplay
        {
            public readonly string label;
            public readonly OptionWeightRecord record;
            public readonly VarietyTraitDef trait;
            public OptionDisplay(string label, OptionWeightRecord record, VarietyTraitDef trait = null) { this.label = label; this.record = record; this.trait = trait; }
        }
    }
}
