using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using HarmonyLib;
using KnowledgeFramework;
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
        private InsightSettingsDocument settingsDocument;

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
            HorticultureKnowledgeAdapter.Register();
            WildlifeRegistryIntegration.Apply(HarmonyInstance);
            NicePlantsMenuCompat.Apply(HarmonyInstance);
            LongEventHandler.ExecuteWhenFinished(() =>
            {
                IPlantToGrowSettable_SetPlantDefToGrow_ClearVariety_Patch.Apply(HarmonyInstance);
                // Identity readbacks happen during the long event, never from plant rendering.
                MaskTextureIdentity.PreloadPlantTextures();
                PlantAutoMaskCache.InitializeAndGenerateMissing();
            });
        }

        public override string SettingsCategory()
        {
            return "Horticulture - Novel Seeds";
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            if (settingsDocument == null || !ReferenceEquals(settingsDocument.Settings, Settings))
            {
                settingsDocument?.PostClose();
                settingsDocument = new InsightSettingsDocument(Settings);
            }
            settingsDocument.Draw(inRect);
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
        public bool balanceValueExplicit;
        public float commonality = 1f;
        public float visualScale = 1f;
        public float yieldFactor = 1f;
        public float growthRateFactor = 1f;
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
        public float synergyAbsentFactor = 1f;
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
        public string originKind = "mutation";
        public int generation;

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
            Scribe_Values.Look(ref originKind, "originKind", "mutation");
            Scribe_Values.Look(ref generation, "generation", 0);
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
        private const int KnowledgeIntegrationRetryIntervalTicks = 15;
        private bool knowledgeIntegrationRetryScheduled;
        private bool knowledgeIntegrationRetryAttempted;
        private int nextKnowledgeIntegrationRetryTick;
        private Game knowledgeIntegrationGame;
        private List<VarietyRecord> unlockedVarieties = new List<VarietyRecord>();
        private List<BreedingProgramRecord> legacyBreedingPrograms = new List<BreedingProgramRecord>();
        private List<SpeciesColorPaletteRecord> speciesColorPalettes = new List<SpeciesColorPaletteRecord>();
        private List<PlantKnowledgeRecord> legacyHorticultureKnowledge = new List<PlantKnowledgeRecord>();
        private Dictionary<string, string> selectedVarietyIdsByGrower = new Dictionary<string, string>();
        private Dictionary<string, string> breedingVarietyIdsByGrower = new Dictionary<string, string>();
        private Dictionary<string, VarietyRecord> varietiesById = new Dictionary<string, VarietyRecord>();
        private Dictionary<ThingDef, List<VarietyRecord>> visibleVarietiesByCrop = new Dictionary<ThingDef, List<VarietyRecord>>();
        private Dictionary<string, VarietyRecord> visibleVarietiesByTraits = new Dictionary<string, VarietyRecord>();
        private List<VarietyRecord> allVisibleVarieties = new List<VarietyRecord>();
        private static readonly ConditionalWeakTable<object, GrowerKeyHolder> GrowerKeys = new ConditionalWeakTable<object, GrowerKeyHolder>();
        private sealed class GrowerKeyHolder { public string key; }
        private int nextVarietyId = 1;
        private int legacyNextBreedingProgramId = 1;
        private List<string> selectionKeysWorking;
        private List<string> selectionValuesWorking;
        private List<string> breedingKeysWorking;
        private List<string> breedingValuesWorking;
        private Dictionary<string, SpeciesColorPaletteRecord> palettesByPlant = new Dictionary<string, SpeciesColorPaletteRecord>();

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
            Scribe_Collections.Look(ref speciesColorPalettes, "speciesColorPalettes", LookMode.Deep);
            if (Scribe.mode != LoadSaveMode.Saving)
            {
                Scribe_Collections.Look(ref legacyBreedingPrograms, "breedingPrograms", LookMode.Deep);
                Scribe_Collections.Look(ref legacyHorticultureKnowledge, "horticultureKnowledge", LookMode.Deep);
                Scribe_Values.Look(ref legacyNextBreedingProgramId, "nextBreedingProgramId", 1);
            }
            Scribe_Collections.Look(ref selectedVarietyIdsByGrower, "selectedVarietyIdsByGrower", LookMode.Value, LookMode.Value, ref selectionKeysWorking, ref selectionValuesWorking);
            Scribe_Collections.Look(ref breedingVarietyIdsByGrower, "breedingVarietyIdsByGrower", LookMode.Value, LookMode.Value, ref breedingKeysWorking, ref breedingValuesWorking);
            Scribe_Values.Look(ref nextVarietyId, "nextVarietyId", 1);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (unlockedVarieties == null)
                {
                    unlockedVarieties = new List<VarietyRecord>();
                }
                if (speciesColorPalettes == null) speciesColorPalettes = new List<SpeciesColorPaletteRecord>();
                if (legacyBreedingPrograms == null) legacyBreedingPrograms = new List<BreedingProgramRecord>();
                if (legacyHorticultureKnowledge == null) legacyHorticultureKnowledge = new List<PlantKnowledgeRecord>();
                if (selectedVarietyIdsByGrower == null)
                {
                    selectedVarietyIdsByGrower = new Dictionary<string, string>();
                }
                if (breedingVarietyIdsByGrower == null)
                {
                    breedingVarietyIdsByGrower = new Dictionary<string, string>();
                }
                RebuildCache();
                EnsureSpeciesColorPalettes();
            }
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            InitializeKnowledgeIntegration();
            EnsureSpeciesColorPalettes();
        }

        public override void GameComponentTick()
        {
            base.GameComponentTick();
            if (knowledgeIntegrationGame == Current.Game &&
                HorticultureKnowledgeRegistration.State == HorticultureKnowledgeRegistrationState.Registered &&
                KnowledgeConsumerApi.Readiness.IsReady)
                return;
            TickManager tickManager = Find.TickManager;
            int currentTick = tickManager?.TicksGame ?? -1;
            if (currentTick < 0 || currentTick < nextKnowledgeIntegrationRetryTick) return;
            nextKnowledgeIntegrationRetryTick = currentTick + KnowledgeIntegrationRetryIntervalTicks;
            RetryKnowledgeIntegration();
        }

        internal void RetryKnowledgeIntegration()
        {
            knowledgeIntegrationRetryScheduled = false;
            InitializeKnowledgeIntegration();
        }

        private void InitializeKnowledgeIntegration()
        {
            HorticultureKnowledgeSnapshots.Clear();
            if (!HorticultureKnowledgeAdapter.Register())
            {
                ScheduleKnowledgeIntegrationRetry();
                return;
            }
            knowledgeIntegrationGame = Current.Game;
            if (!HorticultureKnowledgeAdapter.TryMigrateLegacy(legacyHorticultureKnowledge))
            {
                ScheduleKnowledgeIntegrationRetry();
                return;
            }
            legacyHorticultureKnowledge?.Clear();
            foreach (VarietyRecord variety in AllVarieties.ToList())
                HorticultureKnowledgeAdapter.RegisterCultivar(variety);
        }

        private void ScheduleKnowledgeIntegrationRetry()
        {
            if (knowledgeIntegrationRetryScheduled || knowledgeIntegrationRetryAttempted) return;
            HorticultureKnowledgeRegistrationState state = HorticultureKnowledgeAdapter.RegistrationState;
            if (state != HorticultureKnowledgeRegistrationState.WaitingForFrameworkReadiness && state !=
                HorticultureKnowledgeRegistrationState.Registered) return;
            knowledgeIntegrationRetryAttempted = true;
            knowledgeIntegrationRetryScheduled = true;
            LongEventHandler.ExecuteWhenFinished(() =>
            {
                knowledgeIntegrationRetryScheduled = false;
                InitializeKnowledgeIntegration();
            });
        }

        public IReadOnlyList<SpeciesColorPaletteRecord> SpeciesColorPalettes
        {
            get { EnsureSpeciesColorPalettes(); return speciesColorPalettes; }
        }

        public SpeciesColorPaletteRecord PaletteFor(ThingDef plantDef)
        {
            if (plantDef == null) return null;
            EnsureSpeciesColorPalettes();
            palettesByPlant.TryGetValue(plantDef.defName, out SpeciesColorPaletteRecord palette);
            return palette;
        }

        private void EnsureSpeciesColorPalettes()
        {
            if (speciesColorPalettes == null) speciesColorPalettes = new List<SpeciesColorPaletteRecord>();
            palettesByPlant = speciesColorPalettes.Where(record => record != null && !record.plantDefName.NullOrEmpty())
                .GroupBy(record => record.plantDefName).ToDictionary(group => group.Key, group => group.First());
            string worldSeed = Find.World?.info?.seedString ?? "legacy-save";
            foreach (ThingDef plantDef in DefDatabase<ThingDef>.AllDefsListForReading.Where(NovelSeedUtility.IsGrowableCrop).OrderBy(def => def.defName))
            {
                if (palettesByPlant.ContainsKey(plantDef.defName)) continue;
                SpeciesColorPaletteRecord palette = SpeciesColorPaletteUtility.Generate(plantDef, worldSeed, HorticultureNovelSeedsMod.Settings);
                speciesColorPalettes.Add(palette);
                palettesByPlant[plantDef.defName] = palette;
            }
        }

        public IEnumerable<VarietyRecord> VarietiesFor(ThingDef cropDef)
        {
            return cropDef != null && HorticulturePlantPolicy.IsSupported(cropDef) &&
                visibleVarietiesByCrop.TryGetValue(cropDef, out List<VarietyRecord> varieties)
                ? varieties.Where(value => value?.cropDef != null && HorticulturePlantPolicy.IsSupported(value.cropDef))
                : Enumerable.Empty<VarietyRecord>();
        }

        public IEnumerable<VarietyRecord> AllVarieties => allVisibleVarieties.Where(value =>
            value?.cropDef != null && HorticulturePlantPolicy.IsSupported(value.cropDef));

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
            if (!HorticulturePlantPolicy.IsSupported(cropDef)) return null;
            visibleVarietiesByTraits.TryGetValue(MatchKey(cropDef, NovelSeedUtility.TraitKey(traits)), out VarietyRecord variety);
            return variety;
        }

        public VarietyRecord UnlockVariety(ThingDef cropDef, List<VarietyTraitDef> traits, string customName, IEnumerable<string> parentVarietyIds = null,
            bool hiddenFromMenus = false, Pawn discoverer = null, string originKind = null)
        {
            if (!HorticulturePlantPolicy.IsSupported(cropDef))
            {
                HorticultureKnowledgeEventDiagnostics.UnsupportedPlant();
                return null;
            }
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

            List<string> parentIds = parentVarietyIds?.Where(id => !id.NullOrEmpty()).Distinct().ToList() ?? new List<string>();
            int generation = parentIds.Count == 0 ? 0 : parentIds.Select(GetVariety).Where(value => value != null)
                .Select(value => value.generation).DefaultIfEmpty(0).Max() + 1;

            VarietyRecord variety = new VarietyRecord
            {
                id = "HNS_" + cropDef.defName + "_" + nextVarietyId++,
                cropDef = cropDef,
                customName = customName,
                traits = traits?.Where(t => t != null).Distinct().ToList() ?? new List<VarietyTraitDef>(),
                parentVarietyIds = parentIds,
                hiddenFromMenus = hiddenFromMenus,
                firstDiscoveredBy = discoverer?.LabelShortCap ?? "HNS_UnknownDiscoverer".Translate().ToString(),
                firstDiscoveredTick = Find.TickManager?.TicksAbs ?? -1,
                firstDiscoveredTile = discoverer?.Map != null ? discoverer.Map.Tile : -1,
                originKind = originKind.NullOrEmpty() ? (parentIds.Count == 0 ? "mutation" : "cross-pollination") : originKind,
                generation = generation
            };
            DeriveHybridPalette(cropDef, variety.parentVarietyIds);
            unlockedVarieties.Add(variety);
            varietiesById[variety.id] = variety;
            if (!variety.hiddenFromMenus) IndexVisibleVariety(variety);
            HorticultureKnowledgeAdapter.RegisterCultivar(variety);
            return variety;
        }

        private void DeriveHybridPalette(ThingDef cropDef, IEnumerable<string> parentIds)
        {
            List<ThingDef> parentSpecies = parentIds?.Select(GetVariety).Where(parent => parent?.cropDef != null)
                .Select(parent => parent.cropDef).Distinct().ToList() ?? new List<ThingDef>();
            if (cropDef == null || parentSpecies.Count < 2 || parentSpecies.All(parent => parent == cropDef)) return;
            SpeciesColorPaletteRecord target = PaletteFor(cropDef);
            List<Color> parentColors = parentSpecies.SelectMany(parent => PaletteFor(parent)?.Colors ?? Enumerable.Empty<Color>()).ToList();
            if (target == null || parentColors.Count == 0) return;
            List<Color> derived = parentColors.ToList();
            for (int i = 0; i < parentSpecies.Count; i++)
            for (int j = i + 1; j < parentSpecies.Count; j++)
            {
                Color left = PaletteFor(parentSpecies[i])?.Colors.FirstOrDefault() ?? SpeciesColorPaletteUtility.BaseColor(parentSpecies[i]);
                Color right = PaletteFor(parentSpecies[j])?.Colors.FirstOrDefault() ?? SpeciesColorPaletteUtility.BaseColor(parentSpecies[j]);
                derived.Add(PigmentColorUtility.Blend(left, right));
            }
            int limit = HorticultureNovelSeedsMod.Settings?.maximumPaletteSize ?? 5;
            target.packedColors = derived.Select(SpeciesColorPaletteRecord.Pack).Distinct().Take(Mathf.Clamp(limit, 1, 24)).ToList();
            target.hybridDerived = true;
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
            return OrderBreedingMixVarieties(ids.Split(',').Select(GetVariety));
        }

        public VarietyRecord VarietyForSowing(IPlantToGrowSettable settable, IntVec3 cell)
        {
            if (!HorticulturePlantPolicy.IsSupported(settable?.GetPlantDefToGrow())) return null;
            VarietyRecord selected = SelectedVarietyFor(settable);
            if (selected != null) return selected;
            IReadOnlyList<VarietyRecord> breeding = BreedingVarietiesFor(settable);
            return SelectBreedingMixVariety(breeding, cell);
        }

        internal static int BreedingMixIndex(IntVec3 cell, int count)
        {
            if (count <= 0) return -1;
            unchecked
            {
                int hash = (cell.x * 397) ^ (cell.z * 7919);
                return (hash & int.MaxValue) % count;
            }
        }

        internal static List<VarietyRecord> OrderBreedingMixVarieties(IEnumerable<VarietyRecord> varieties)
        {
            return (varieties ?? Enumerable.Empty<VarietyRecord>())
                .Where(variety => variety != null && !variety.id.NullOrEmpty())
                .GroupBy(variety => variety.id)
                .Select(group => group.First())
                .OrderBy(variety => variety.id, StringComparer.Ordinal)
                .ToList();
        }

        internal static VarietyRecord SelectBreedingMixVariety(IReadOnlyList<VarietyRecord> breeding, IntVec3 cell)
        {
            if (breeding == null || breeding.Count == 0) return null;
            int index = BreedingMixIndex(cell, breeding.Count);
            return index < 0 ? null : breeding[index];
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

