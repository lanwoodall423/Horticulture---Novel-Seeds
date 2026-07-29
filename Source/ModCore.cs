using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using HarmonyLib;
using ProgressionAgriculture;
using RimWorld;
using UnityEngine;
using Verse;

namespace HorticultureNovelSeeds
{
    public class HorticultureNovelSeedsMod : Mod
    {
        public static Harmony HarmonyInstance;
        public static NovelSeedsSettings Settings;
        public static string ContentRootPath;

        public HorticultureNovelSeedsMod(ModContentPack pack) : base(pack)
        {
            ContentRootPath = pack.RootDir;
            bool localSettingsExist = SettingsProfileManager.LocalSettingsExist(pack.FolderName, GetType().Name);
            Settings = GetSettings<NovelSeedsSettings>();
            if (!localSettingsExist)
            {
                if (SettingsProfileManager.ApplyDefault(Settings, out bool usedBundledDefault, out string defaultError))
                {
                    if (usedBundledDefault) LoadedModManager.WriteModSettings(pack.FolderName, GetType().Name, Settings);
                }
                else if (!defaultError.NullOrEmpty())
                {
                    Log.Error("Horticulture - Novel Seeds could not initialize its bundled default configuration: " + defaultError);
                }
            }
            HarmonyInstance = new Harmony("lan.horticulture.novelseeds");
            HarmonyInstance.PatchAll(typeof(HorticultureNovelSeedsMod).Assembly);
            WildlifeRegistryIntegration.Apply(HarmonyInstance);
            NicePlantsMenuCompat.Apply(HarmonyInstance);
            LongEventHandler.ExecuteWhenFinished(() => IPlantToGrowSettable_SetPlantDefToGrow_ClearVariety_Patch.Apply(HarmonyInstance));
        }

        public override string SettingsCategory()
        {
            return "Horticulture - Novel Seeds";
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            NovelSeedsSettingsUI.DoWindowContents(inRect, Settings);
        }

        public override void WriteSettings()
        {
            Settings?.Normalize();
            Settings?.ClearVisualCache();
            ProduceMaskRenderer.ClearAll();
            base.WriteSettings();
            if (Current.Game != null)
            {
                foreach (Map map in Find.Maps)
                {
                    if (map?.mapDrawer == null || map.listerThings == null) continue;
                    foreach (Thing thing in map.listerThings.AllThings)
                    {
                        bool novelPlant = thing is Plant plant && plant.TryGetComp<CompPlantVariety>()?.HasAnyTraits == true;
                        bool novelProduce = thing.TryGetComp<CompNovelProduceAppearance>()?.HasStoredAppearance == true;
                        if (novelPlant || novelProduce)
                        {
                            map.mapDrawer.MapMeshDirty(thing.Position, (ulong)MapMeshFlagDefOf.Things);
                        }
                    }
                }
            }
        }
    }

    [DefOf]
    public static class HNS_DefOf
    {
        public static ThingDef HNS_NovelSeedPack;
        public static JobDef HNS_UnlockVariety;
        public static JobDef HNS_FertilizePlant;
    }

    public class VarietyTraitDef : Def
    {
        // Kept for compatibility with older integrations; use configurable trait tags for new behavior.
        public bool positive;
        public List<string> traitTags = new List<string>();
        public float balanceValue;
        public float commonality = 1f;
        public float visualScale = 1f;
        public float yieldFactor = 1f;
        public float coldGrowthOffset;
        public float heatGrowthOffset;
        public float blightChanceFactor = 1f;
        public float blightDamageFactor = 1f;
        public float workFactor = 1f;
        public float maxHitPointsFactor = 1f;
        public float beautyOffset;
        public float tintRed = 1f;
        public float tintGreen = 1f;
        public float tintBlue = 1f;
        public int visualMaskIndex = -1;
        public int percentageBonus;
        public bool produceOnlyVisual;
        public bool inheritToProduce;
        public float produceArmorHeatFactor = 1f;
        public float produceColdInsulationFactor = 1f;
        public float produceMaxHitPointsFactor = 1f;
        public float produceBeautyOffset;
        public string visualTintLabel;
        public float visualRadiance;
        public float visualDullness;
        public float visualGloom;
        public float visualWidth = 1f;
        public float visualHeight = 1f;
        public float visualDensity = 1f;
        public int sowSkillOffset;
        public float sowWorkFactor = 1f;
        public float harvestWorkFactor = 1f;
        public bool perennialColdDormancy;
        public float dormantGrowthFactor = 0.01f;
        public bool selfSeeding;
        public float tramplingDamage;
        public float forageNutritionFactor = 1f;
        public bool humongousSpacing;
        public bool visualSpikes;
        public string requiredSowTag;
        public float fishingYieldFactor = 1f;
        public float companionGrowthFactor = 1f;
        public ThingDef byproductDef;
        public float byproductChance;
        public IntRange byproductCount = new IntRange(1, 1);
        public HediffDef resinHediff;
        public float resinHediffSeverity;
        public DamageDef resinDamage;
        public float resinDamageAmount;
        public ThingDef requiredResourceDef;
        public int requiredResourceCount = 1;
        public string configCategory;
        public string configFamily;
        public string configType;
        public bool configRoot;
        public bool hiddenFromConfig;
        public ThingDef synergyPlantDef;
        public string synergyStat;
        public float synergyFactor = 1.15f;
        public bool perennial;
        public float harvestAfterGrowth = 0.30f;
        public ThoughtDef joyResinThought;
        public float thornScratchChance;
        public float thornScratchDamage = 3f;
        public float nutritionFactor = 1f;
        public float medicalPotencyFactor = 1f;
        public List<string> requiredPlantTags = new List<string>();
        public List<string> anyPlantTags = new List<string>();
        public List<string> excludedPlantTags = new List<string>();
        public ThoughtDef compoundThought;
        public HediffDef compoundHediff;
        public float compoundHediffSeverity;
        public List<string> exclusionTags = new List<string>();
    }

    public class VarietyRecord : IExposable
    {
        public string id;
        public ThingDef cropDef;
        public string customName;
        public List<VarietyTraitDef> traits = new List<VarietyTraitDef>();
        public List<string> parentVarietyIds = new List<string>();
        public bool hiddenFromMenus;
        public bool registryFavorite;
        public bool registryArchived;
        public string firstDiscoveredBy;
        public long firstDiscoveredTick = -1;
        public int firstDiscoveredTile = -1;

        public string Label => customName.NullOrEmpty() ? "HNS_PendingVariety".Translate().ToString() : customName;
        public string TraitKey => NovelSeedUtility.TraitKey(traits);

        public string FirstDiscoveredDate
        {
            get
            {
                if (firstDiscoveredTick < 0) return string.Empty;
                Vector2 longLat = Find.WorldGrid != null && firstDiscoveredTile >= 0 ? Find.WorldGrid.LongLatOf(firstDiscoveredTile) : Vector2.zero;
                return GenDate.DateFullStringAt(firstDiscoveredTick, longLat);
            }
        }
        public string FirstDiscoveredInfo => firstDiscoveredTick < 0 ? string.Empty : "HNS_FirstDiscoveredLine".Translate(
            firstDiscoveredBy.NullOrEmpty() ? "HNS_UnknownDiscoverer".Translate() : firstDiscoveredBy, FirstDiscoveredDate);

        public void ExposeData()
        {
            Scribe_Values.Look(ref id, "id");
            Scribe_Defs.Look(ref cropDef, "cropDef");
            Scribe_Values.Look(ref customName, "customName");
            Scribe_Collections.Look(ref traits, "traits", LookMode.Def);
            Scribe_Collections.Look(ref parentVarietyIds, "parentVarietyIds", LookMode.Value);
            Scribe_Values.Look(ref hiddenFromMenus, "hiddenFromMenus", false);
            Scribe_Values.Look(ref registryFavorite, "registryFavorite", false);
            Scribe_Values.Look(ref registryArchived, "registryArchived", false);
            Scribe_Values.Look(ref firstDiscoveredBy, "firstDiscoveredBy");
            Scribe_Values.Look(ref firstDiscoveredTick, "firstDiscoveredTick", -1L);
            Scribe_Values.Look(ref firstDiscoveredTile, "firstDiscoveredTile", -1);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (traits == null) traits = new List<VarietyTraitDef>();
                if (parentVarietyIds == null) parentVarietyIds = new List<string>();
            }
        }
    }

    public class BreedingProgramRecord : IExposable
    {
        public string id;
        public string name;
        public ThingDef cropDef;
        public List<string> desiredTraitRootDefNames = new List<string>();
        public List<string> notifiedVarietyIds = new List<string>();
        public bool active = true;

        public void ExposeData()
        {
            Scribe_Values.Look(ref id, "id");
            Scribe_Values.Look(ref name, "name");
            Scribe_Defs.Look(ref cropDef, "cropDef");
            Scribe_Collections.Look(ref desiredTraitRootDefNames, "desiredTraitRootDefNames", LookMode.Value);
            Scribe_Collections.Look(ref notifiedVarietyIds, "notifiedVarietyIds", LookMode.Value);
            Scribe_Values.Look(ref active, "active", true);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                desiredTraitRootDefNames = desiredTraitRootDefNames?.Where(value => !value.NullOrEmpty()).Distinct().ToList() ?? new List<string>();
                notifiedVarietyIds = notifiedVarietyIds?.Where(value => !value.NullOrEmpty()).Distinct().ToList() ?? new List<string>();
            }
        }

        public bool Matches(VarietyRecord variety)
        {
            if (variety?.cropDef != cropDef || desiredTraitRootDefNames == null || desiredTraitRootDefNames.Count == 0) return false;
            HashSet<string> roots = new HashSet<string>((variety.traits ?? new List<VarietyTraitDef>())
                .Where(trait => trait != null).Select(trait => TraitConfigUtility.Root(trait)?.defName).Where(value => !value.NullOrEmpty()));
            return desiredTraitRootDefNames.All(roots.Contains);
        }

        public int MatchCount(VarietyRecord variety)
        {
            if (variety?.cropDef != cropDef) return 0;
            HashSet<string> roots = new HashSet<string>((variety.traits ?? new List<VarietyTraitDef>())
                .Where(trait => trait != null).Select(trait => TraitConfigUtility.Root(trait)?.defName).Where(value => !value.NullOrEmpty()));
            return desiredTraitRootDefNames?.Count(roots.Contains) ?? 0;
        }

        public string DesiredTraitSummary
        {
            get
            {
                return string.Join(", ", (desiredTraitRootDefNames ?? new List<string>())
                    .Select(defName => DefDatabase<VarietyTraitDef>.GetNamedSilentFail(defName)?.LabelCap.ToString() ?? defName).ToArray());
            }
        }
    }

    public class GameComponent_NovelSeeds : GameComponent
    {
        private List<VarietyRecord> unlockedVarieties = new List<VarietyRecord>();
        private List<BreedingProgramRecord> breedingPrograms = new List<BreedingProgramRecord>();
        private Dictionary<string, string> selectedVarietyIdsByGrower = new Dictionary<string, string>();
        private Dictionary<string, string> breedingVarietyIdsByGrower = new Dictionary<string, string>();
        private Dictionary<string, VarietyRecord> varietiesById = new Dictionary<string, VarietyRecord>();
        private Dictionary<ThingDef, List<VarietyRecord>> visibleVarietiesByCrop = new Dictionary<ThingDef, List<VarietyRecord>>();
        private Dictionary<string, VarietyRecord> visibleVarietiesByTraits = new Dictionary<string, VarietyRecord>();
        private List<VarietyRecord> allVisibleVarieties = new List<VarietyRecord>();
        private static readonly ConditionalWeakTable<object, GrowerKeyHolder> GrowerKeys = new ConditionalWeakTable<object, GrowerKeyHolder>();
        private sealed class GrowerKeyHolder { public string key; }
        private int nextVarietyId = 1;
        private int nextBreedingProgramId = 1;
        private List<string> selectionKeysWorking;
        private List<string> selectionValuesWorking;
        private List<string> breedingKeysWorking;
        private List<string> breedingValuesWorking;

        public static GameComponent_NovelSeeds Instance => Current.Game?.GetComponent<GameComponent_NovelSeeds>();

        public GameComponent_NovelSeeds()
        {
            RebuildCache();
        }

        public GameComponent_NovelSeeds(Game game)
        {
            RebuildCache();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref unlockedVarieties, "unlockedVarieties", LookMode.Deep);
            Scribe_Collections.Look(ref breedingPrograms, "breedingPrograms", LookMode.Deep);
            Scribe_Collections.Look(ref selectedVarietyIdsByGrower, "selectedVarietyIdsByGrower", LookMode.Value, LookMode.Value, ref selectionKeysWorking, ref selectionValuesWorking);
            Scribe_Collections.Look(ref breedingVarietyIdsByGrower, "breedingVarietyIdsByGrower", LookMode.Value, LookMode.Value, ref breedingKeysWorking, ref breedingValuesWorking);
            Scribe_Values.Look(ref nextVarietyId, "nextVarietyId", 1);
            Scribe_Values.Look(ref nextBreedingProgramId, "nextBreedingProgramId", 1);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (unlockedVarieties == null)
                {
                    unlockedVarieties = new List<VarietyRecord>();
                }
                if (breedingPrograms == null) breedingPrograms = new List<BreedingProgramRecord>();
                if (selectedVarietyIdsByGrower == null)
                {
                    selectedVarietyIdsByGrower = new Dictionary<string, string>();
                }
                if (breedingVarietyIdsByGrower == null)
                {
                    breedingVarietyIdsByGrower = new Dictionary<string, string>();
                }
                RebuildCache();
            }
        }

        public IEnumerable<VarietyRecord> VarietiesFor(ThingDef cropDef)
        {
            return cropDef != null && visibleVarietiesByCrop.TryGetValue(cropDef, out List<VarietyRecord> varieties)
                ? varieties : Enumerable.Empty<VarietyRecord>();
        }

        public IEnumerable<VarietyRecord> AllVarieties => allVisibleVarieties;
        public IReadOnlyList<BreedingProgramRecord> BreedingPrograms => breedingPrograms;

        public VarietyRecord GetVariety(string id)
        {
            if (id.NullOrEmpty())
            {
                return null;
            }
            varietiesById.TryGetValue(id, out VarietyRecord variety);
            return variety;
        }

        public VarietyRecord FindMatchingVariety(ThingDef cropDef, List<VarietyTraitDef> traits)
        {
            if (cropDef == null) return null;
            visibleVarietiesByTraits.TryGetValue(MatchKey(cropDef, NovelSeedUtility.TraitKey(traits)), out VarietyRecord variety);
            return variety;
        }

        public VarietyRecord UnlockVariety(ThingDef cropDef, List<VarietyTraitDef> traits, string customName, IEnumerable<string> parentVarietyIds = null, bool hiddenFromMenus = false, Pawn discoverer = null)
        {
            GameComponent_UnlockedCrops cropRegistry = GameComponent_UnlockedCrops.Instance;
            if (cropDef != null && cropRegistry != null && !cropRegistry.IsCropUnlocked(cropDef))
            {
                cropRegistry.UnlockCrop(cropDef);
            }

            VarietyRecord existing = FindMatchingVariety(cropDef, traits);
            if (existing != null)
            {
                return existing;
            }

            VarietyRecord variety = new VarietyRecord
            {
                id = "HNS_" + cropDef.defName + "_" + nextVarietyId++,
                cropDef = cropDef,
                customName = customName,
                traits = traits?.Where(t => t != null).Distinct().ToList() ?? new List<VarietyTraitDef>(),
                parentVarietyIds = parentVarietyIds?.Where(id => !id.NullOrEmpty()).Distinct().ToList() ?? new List<string>(),
                hiddenFromMenus = hiddenFromMenus,
                firstDiscoveredBy = discoverer?.LabelShortCap ?? "HNS_UnknownDiscoverer".Translate().ToString(),
                firstDiscoveredTick = Find.TickManager?.TicksAbs ?? -1,
                firstDiscoveredTile = discoverer?.Map != null ? discoverer.Map.Tile : -1
            };
            unlockedVarieties.Add(variety);
            varietiesById[variety.id] = variety;
            if (!variety.hiddenFromMenus) IndexVisibleVariety(variety);
            if (!variety.hiddenFromMenus) NotifyMatchingBreedingPrograms(variety);
            return variety;
        }

        public void RenameVariety(VarietyRecord variety, string customName)
        {
            string trimmed = customName?.Trim();
            if (variety == null || trimmed.NullOrEmpty())
            {
                return;
            }
            variety.customName = trimmed;
        }

        public BreedingProgramRecord AddBreedingProgram(string name, ThingDef cropDef, IEnumerable<VarietyTraitDef> desiredTraits)
        {
            if (cropDef == null) return null;
            List<string> roots = desiredTraits?.Where(trait => trait != null)
                .Select(trait => TraitConfigUtility.Root(trait)?.defName).Where(value => !value.NullOrEmpty()).Distinct().ToList()
                ?? new List<string>();
            if (roots.Count == 0) return null;
            BreedingProgramRecord program = new BreedingProgramRecord
            {
                id = "HNS_PROGRAM_" + nextBreedingProgramId++,
                name = name.NullOrEmpty() ? cropDef.LabelCap + " Program" : name.Trim(),
                cropDef = cropDef,
                desiredTraitRootDefNames = roots
            };
            breedingPrograms.Add(program);
            return program;
        }

        public void RemoveBreedingProgram(BreedingProgramRecord program)
        {
            if (program != null) breedingPrograms.Remove(program);
        }

        public IEnumerable<VarietyRecord> CandidateVarieties(BreedingProgramRecord program)
        {
            if (program?.cropDef == null) return Enumerable.Empty<VarietyRecord>();
            return VarietiesFor(program.cropDef)
                .OrderByDescending(program.MatchCount)
                .ThenBy(variety => Mathf.Abs(NovelSeedUtility.TraitBalanceScore(variety.traits)))
                .ThenBy(variety => variety.Label);
        }

        private void NotifyMatchingBreedingPrograms(VarietyRecord variety)
        {
            foreach (BreedingProgramRecord program in breedingPrograms.Where(item => item?.active == true && item.Matches(variety)))
            {
                if (program.notifiedVarietyIds.Contains(variety.id)) continue;
                program.notifiedVarietyIds.Add(variety.id);
                Find.LetterStack?.ReceiveLetter("HNS_BreedingProgramMatched".Translate(program.name),
                    "HNS_BreedingProgramMatchedDesc".Translate(variety.Label, program.name, NovelSeedUtility.TraitSummary(variety.traits)),
                    LetterDefOf.PositiveEvent);
            }
        }

        public VarietyRecord SelectedVarietyFor(IPlantToGrowSettable settable)
        {
            string key = GrowerKey(settable);
            if (key == null || !selectedVarietyIdsByGrower.TryGetValue(key, out string id))
            {
                return null;
            }
            return GetVariety(id);
        }

        public IReadOnlyList<VarietyRecord> BreedingVarietiesFor(IPlantToGrowSettable settable)
        {
            string key = GrowerKey(settable);
            if (key == null || !breedingVarietyIdsByGrower.TryGetValue(key, out string ids) || ids.NullOrEmpty())
            {
                return new List<VarietyRecord>();
            }
            return ids.Split(',')
                .Select(GetVariety)
                .Where(variety => variety != null)
                .GroupBy(variety => variety.id)
                .Select(group => group.First())
                .OrderBy(variety => variety.id)
                .ToList();
        }

        public VarietyRecord VarietyForSowing(IPlantToGrowSettable settable, IntVec3 cell)
        {
            VarietyRecord selected = SelectedVarietyFor(settable);
            if (selected != null) return selected;
            IReadOnlyList<VarietyRecord> breeding = BreedingVarietiesFor(settable);
            if (breeding.Count == 0) return null;
            unchecked
            {
                int hash = (cell.x * 397) ^ (cell.z * 7919);
                return breeding[(hash & int.MaxValue) % breeding.Count];
            }
        }

        public void SetSelectedVariety(IPlantToGrowSettable settable, VarietyRecord variety)
        {
            string key = GrowerKey(settable);
            if (key == null)
            {
                return;
            }
            if (variety == null)
            {
                selectedVarietyIdsByGrower.Remove(key);
            }
            else
            {
                selectedVarietyIdsByGrower[key] = variety.id;
            }
            breedingVarietyIdsByGrower.Remove(key);
        }

        public void SetBreedingMix(IPlantToGrowSettable settable, IEnumerable<VarietyRecord> varieties)
        {
            string key = GrowerKey(settable);
            if (key == null) return;
            List<string> ids = varieties?.Where(variety => variety != null && !variety.id.NullOrEmpty())
                .Select(variety => variety.id).Distinct().OrderBy(id => id).ToList() ?? new List<string>();
            selectedVarietyIdsByGrower.Remove(key);
            if (ids.Count >= 2) breedingVarietyIdsByGrower[key] = string.Join(",", ids);
            else breedingVarietyIdsByGrower.Remove(key);
        }

        public void ClearSelectedVariety(IPlantToGrowSettable settable)
        {
            string key = GrowerKey(settable);
            if (key == null) return;
            selectedVarietyIdsByGrower.Remove(key);
            breedingVarietyIdsByGrower.Remove(key);
        }

        public static string GrowerKey(IPlantToGrowSettable settable)
        {
            if (settable == null) return null;
            return GrowerKeys.GetValue(settable, key => new GrowerKeyHolder
            {
                key = key is Zone zone ? "zone:" + zone.GetUniqueLoadID()
                    : key is Thing thing ? "thing:" + thing.GetUniqueLoadID() : null
            }).key;
        }

        private void RebuildCache()
        {
            varietiesById = new Dictionary<string, VarietyRecord>();
            visibleVarietiesByCrop = new Dictionary<ThingDef, List<VarietyRecord>>();
            visibleVarietiesByTraits = new Dictionary<string, VarietyRecord>();
            allVisibleVarieties = new List<VarietyRecord>();
            if (unlockedVarieties == null)
            {
                return;
            }
            foreach (VarietyRecord variety in unlockedVarieties)
            {
                if (variety?.id != null)
                {
                    varietiesById[variety.id] = variety;
                    if (variety.cropDef != null && !variety.hiddenFromMenus) IndexVisibleVariety(variety);
                }
            }
        }

        private void IndexVisibleVariety(VarietyRecord variety)
        {
            if (!visibleVarietiesByCrop.TryGetValue(variety.cropDef, out List<VarietyRecord> cropVarieties))
            {
                cropVarieties = new List<VarietyRecord>();
                visibleVarietiesByCrop.Add(variety.cropDef, cropVarieties);
            }
            cropVarieties.Add(variety);
            allVisibleVarieties.Add(variety);
            visibleVarietiesByTraits[MatchKey(variety.cropDef, variety.TraitKey)] = variety;
        }

        private static string MatchKey(ThingDef cropDef, string traitKey)
        {
            return cropDef.shortHash + "|" + (traitKey ?? string.Empty);
        }
    }
}

