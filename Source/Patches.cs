using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace HorticultureNovelSeeds
{
    [HarmonyPatch(typeof(DefGenerator), nameof(DefGenerator.GenerateImpliedDefs_PreResolve))]
    public static class DefGenerator_AddPlantMutationComps_Patch
    {
        public static void Postfix(bool hotReload = false)
        {
            ColorTraitFactory.GenerateAll();
            PercentageTraitFactory.GenerateAll();
            SynergyTraitFactory.GenerateAll();
            LongEventHandler.ExecuteWhenFinished(PlantTagUtility.RebuildCache);
            HorticultureNovelSeedsMod.Settings?.EnsureDefaultPlantGroups();
            HashSet<ThingDef> harvestedProducts = new HashSet<ThingDef>();
            foreach (ThingDef cropDef in DefDatabase<ThingDef>.AllDefsListForReading.Where(NovelSeedUtility.IsGrowableCrop))
            {
                if (cropDef.comps == null) cropDef.comps = new List<CompProperties>();
                if (!cropDef.comps.Any(c => c is CompProperties_PlantVariety))
                    cropDef.comps.Add(new CompProperties_PlantVariety());

                ThingDef productDef = cropDef.plant?.harvestedThingDef;
                if (productDef != null) harvestedProducts.Add(productDef);

                Type varietyTabType = typeof(ITab_PlantVariety);
                if (cropDef.inspectorTabs == null) cropDef.inspectorTabs = new List<Type>();
                if (!cropDef.inspectorTabs.Contains(varietyTabType)) cropDef.inspectorTabs.Add(varietyTabType);
                if (cropDef.inspectorTabsResolved != null && !cropDef.inspectorTabsResolved.Any(tab => tab.GetType() == varietyTabType))
                    cropDef.inspectorTabsResolved.Add(InspectTabManager.GetSharedInstance(varietyTabType));
            }

            HashSet<ThingDef> inheritableProducts = new HashSet<ThingDef>(harvestedProducts);
            bool foundMoreProducts;
            do
            {
                foundMoreProducts = false;
                foreach (RecipeDef recipe in DefDatabase<RecipeDef>.AllDefsListForReading)
                {
                    if (recipe?.ingredients.NullOrEmpty() != false || recipe.products.NullOrEmpty()) continue;
                    if (!recipe.ingredients.Any(ingredient => inheritableProducts.Any(product => ingredient.filter?.Allows(product) == true))) continue;
                    foreach (ThingDefCountClass product in recipe.products)
                    {
                        if (product?.thingDef != null && inheritableProducts.Add(product.thingDef)) foundMoreProducts = true;
                    }
                }
            }
            while (foundMoreProducts);

            foreach (ThingDef thingDef in DefDatabase<ThingDef>.AllDefsListForReading
                .Where(def => inheritableProducts.Contains(def) || def.ingestible != null))
            {
                EnsureProduceSupport(thingDef);
            }
        }

        private static void EnsureProduceSupport(ThingDef thingDef)
        {
            if (thingDef?.thingClass == null || !typeof(ThingWithComps).IsAssignableFrom(thingDef.thingClass)) return;
            if (thingDef.comps == null) thingDef.comps = new List<CompProperties>();
            if (!thingDef.comps.Any(comp => comp is CompProperties_NovelProduceAppearance))
                thingDef.comps.Add(new CompProperties_NovelProduceAppearance());

            Type tabType = typeof(ITab_ProduceVariety);
            if (thingDef.inspectorTabs == null) thingDef.inspectorTabs = new List<Type>();
            if (!thingDef.inspectorTabs.Contains(tabType)) thingDef.inspectorTabs.Add(tabType);
            if (thingDef.inspectorTabsResolved != null && !thingDef.inspectorTabsResolved.Any(tab => tab.GetType() == tabType))
                thingDef.inspectorTabsResolved.Add(InspectTabManager.GetSharedInstance(tabType));
        }
    }
    [HarmonyPatch(typeof(WildPlantSpawner), "SpawnPlant")]
    public static class WildPlantSpawner_SpawnPlant_NovelVariety_Patch
    {
        public static void Postfix(Plant __result)
        {
            NovelSeedUtility.AssignWildMutation(__result);
        }
    }

    [HarmonyPatch]
    public static class JobDriverPlantSow_AssignMutation_Patch
    {
        public static MethodBase TargetMethod()
        {
            Type displayClass = AccessTools.TypeByName("RimWorld.JobDriver_PlantSow+<>c__DisplayClass5_0");
            return AccessTools.Method(displayClass, "<MakeNewToils>b__3");
        }

        public static void Postfix(object __instance)
        {
            JobDriver_PlantSow driver = AccessTools.Field(__instance.GetType(), "<>4__this")?.GetValue(__instance) as JobDriver_PlantSow;
            Plant plant = driver?.job?.GetTarget(TargetIndex.A).Thing as Plant;
            NovelSeedUtility.AssignMutationOnSow(plant);
            CompPlantVariety comp = plant?.TryGetComp<CompPlantVariety>();
            NovelSeedUtility.ApplyJoyResinThought(driver?.pawn, comp?.ActiveTraits);
            ExpandedTraitUtility.ApplyResinEffects(driver?.pawn, plant, comp?.ActiveTraits);
        }
    }

    [HarmonyPatch(typeof(Plant), nameof(Plant.YieldNow))]
    public static class PlantYieldNow_Mutation_Patch
    {
        [HarmonyPriority(Priority.Last)]
        public static void Postfix(Plant __instance, ref int __result)
        {
            CompPlantVariety comp = __instance.TryGetComp<CompPlantVariety>();
            if (comp != null && comp.HasAnyTraits && __result > 0)
            {
                float factor = NovelSeedUtility.YieldFactor(comp) * ExpandedTraitUtility.SynergyFactor(__instance, comp.ActiveTraits, "Yield");
                __result = Mathf.Max(0, GenMath.RoundRandom(__result * factor));
            }
            ProduceAppearanceContext.Capture(__instance, __result);
        }
    }

    public class HarvestState
    {
        public ThingDef cropDef;
        public List<VarietyTraitDef> traits;
        public List<VarietyTraitDef> activeTraits;
        public List<string> lineageParentIds;
        public CompPlantVariety comp;
        public IntVec3 position;
        public Map map;
        public Pawn harvester;
        public Plant plant;
        public PlantProperties plantProperties;
        public float originalHarvestAfterGrowth;
        public bool pendingDiscoveryHarvest;
        public bool shouldSaveSeeds;
        public bool shouldApplyHarvestEffects;
        public bool restoreHarvestAfterGrowth;

        public void RestoreHarvestAfterGrowth()
        {
            if (restoreHarvestAfterGrowth && plantProperties != null)
            {
                plantProperties.harvestAfterGrowth = originalHarvestAfterGrowth;
            }
        }
    }

    [HarmonyPatch(typeof(Plant), nameof(Plant.PlantCollected))]
    public static class PlantCollected_DropMutationSeed_Patch
    {
        private const PlantDestructionMode HarvestMode = (PlantDestructionMode)2;
        private const PlantDestructionMode CutMode = (PlantDestructionMode)3;

        public static void Prefix(Plant __instance, Pawn by, PlantDestructionMode plantDestructionMode, out HarvestState __state)
        {
            __state = null;
            CompPlantVariety comp = __instance.TryGetComp<CompPlantVariety>();
            bool regularHarvest = plantDestructionMode == HarvestMode;
            bool seedCut = plantDestructionMode == CutMode && comp?.PendingDiscovery == true;
            if (comp == null || !comp.HasAnyTraits || (!regularHarvest && !seedCut))
            {
                return;
            }

            List<VarietyTraitDef> activeTraits = comp.ActiveTraits.ToList();
            bool matureDiscovery = comp.PendingDiscovery && __instance.Growth >= 0.999f && !__instance.Blighted;
            bool harvestable = regularHarvest && __instance.HarvestableNow && !__instance.Blighted;
            __state = new HarvestState
            {
                cropDef = __instance.def,
                traits = comp.DiscoveryTraits?.ToList(),
                activeTraits = activeTraits,
                lineageParentIds = comp.CrossPollinationParentIds,
                comp = comp,
                position = __instance.Position,
                map = __instance.Map,
                harvester = by,
                plant = __instance,
                pendingDiscoveryHarvest = matureDiscovery,
                shouldSaveSeeds = comp.SaveSeedsRequested && matureDiscovery,
                shouldApplyHarvestEffects = harvestable
            };

            float resetGrowth = NovelSeedUtility.PerennialHarvestAfterGrowth(activeTraits);
            if (harvestable && resetGrowth > 0f && __instance.def?.plant != null)
            {
                PlantProperties plantProperties = __instance.def.plant;
                __state.plantProperties = plantProperties;
                __state.originalHarvestAfterGrowth = plantProperties.harvestAfterGrowth;
                __state.restoreHarvestAfterGrowth = true;
                plantProperties.harvestAfterGrowth = Mathf.Max(plantProperties.harvestAfterGrowth, resetGrowth);
            }
        }

        public static void Postfix(HarvestState __state)
        {
            if (__state?.shouldSaveSeeds == true)
            {
                NovelSeedUtility.DropDiscoverySeed(__state.cropDef, __state.traits, __state.position, __state.map, __state.lineageParentIds);
            }
            if (__state?.pendingDiscoveryHarvest == true)
            {
                __state.comp?.ClearPendingDiscovery();
            }

            if (__state?.shouldApplyHarvestEffects == true && __state.harvester != null)
            {
                if (!NovelSeedUtility.HandsProtectedFromPlantContact(__state.harvester))
                {
                    NovelSeedUtility.ApplyJoyResinThought(__state.harvester, __state.activeTraits);
                    ExpandedTraitUtility.ApplyResinEffects(__state.harvester, __state.plant, __state.activeTraits);
                }
                NovelSeedUtility.TryThornScratch(__state.harvester, __state.plant, __state.activeTraits);
                ExpandedTraitUtility.DropByproducts(__state.plant, __state.activeTraits);
            }
        }

        public static Exception Finalizer(HarvestState __state, Exception __exception)
        {
            __state?.RestoreHarvestAfterGrowth();
            return __exception;
        }
    }
    [HarmonyPatch(typeof(Plant), nameof(Plant.CropBlighted))]
    public static class PlantCropBlighted_Mutation_Patch
    {
        public static bool Prefix(Plant __instance)
        {
            CompPlantVariety comp = __instance.TryGetComp<CompPlantVariety>();
            if (comp == null || !comp.HasAnyTraits)
            {
                return true;
            }
            float factor = NovelSeedUtility.BlightChanceFactor(comp);
            float synergy = ExpandedTraitUtility.SynergyFactor(__instance, comp.ActiveTraits, "DiseaseResistance");
            if (synergy > 1f) factor /= synergy;
            return factor >= 1f || Rand.Chance(factor);
        }

        public static void Postfix(Plant __instance)
        {
            CompPlantVariety comp = __instance.TryGetComp<CompPlantVariety>();
            if (comp == null || !comp.HasAnyTraits || !__instance.Blighted)
            {
                return;
            }
            float extraDamageFactor = NovelSeedUtility.BlightDamageFactor(comp);
            if (extraDamageFactor > 1f)
            {
                float amount = Mathf.Max(1f, __instance.MaxHitPoints * 0.05f * (extraDamageFactor - 1f));
                __instance.TakeDamage(new DamageInfo(DamageDefOf.Rotting, amount));
            }
        }
    }

    [HarmonyPatch(typeof(Plant), nameof(Plant.GrowthRateFactor_Temperature), MethodType.Getter)]
    public static class PlantGrowthRateFactorTemperature_Mutation_Patch
    {
        public static void Postfix(Plant __instance, ref float __result)
        {
            CompPlantVariety comp = __instance.TryGetComp<CompPlantVariety>();
            if (comp == null || !comp.HasAnyTraits || !GenTemperature.TryGetTemperatureForCell(__instance.Position, __instance.Map, out float cellTemp))
            {
                return;
            }

            NovelSeedUtility.TemperatureOffsets(comp, out float coldOffset, out float heatOffset);
            if (Mathf.Approximately(coldOffset, 0f) && Mathf.Approximately(heatOffset, 0f))
            {
                return;
            }

            PlantProperties plant = __instance.def.plant;
            float minGrowth = plant.minGrowthTemperature + coldOffset;
            float minOptimal = plant.minOptimalGrowthTemperature + coldOffset;
            float maxOptimal = plant.maxOptimalGrowthTemperature + heatOffset;
            float maxGrowth = plant.maxGrowthTemperature + heatOffset;

            if (cellTemp < minOptimal)
            {
                __result = Mathf.InverseLerp(minGrowth, minOptimal, cellTemp);
            }
            else if (cellTemp > maxOptimal)
            {
                __result = Mathf.InverseLerp(maxGrowth, maxOptimal, cellTemp);
            }
            else
            {
                __result = 1f;
            }
        }
    }

    [HarmonyPatch(typeof(Plant), nameof(Plant.Print))]
    public static class PlantPrint_MutationVisualScale_Patch
    {
        public static bool Prefix(Plant __instance, SectionLayer layer)
        {
            CompPlantVariety comp = __instance.TryGetComp<CompPlantVariety>();
            if (comp == null || !comp.HasAnyTraits)
            {
                return true;
            }
            PlantVisualParameters visual = NovelSeedUtility.ResolveVisualParameters(comp);
            if (visual.IsDefault && !PlantMaskUtility.HasActiveMasks(__instance.def))
            {
                return true;
            }
            return PlantVisualUtility.PrintScaledPlant(__instance, layer, visual);
        }
    }

    [HarmonyPatch(typeof(StatExtension), nameof(StatExtension.GetStatValue), new[] { typeof(Thing), typeof(StatDef), typeof(bool), typeof(int) })]
    public static class StatExtension_GetStatValue_NovelSeedBeauty_Patch
    {
        public static void Postfix(Thing thing, StatDef stat, ref float __result)
        {
            if (thing == null || stat == null) return;

            if (thing is Plant plant)
            {
                CompPlantVariety plantComp = plant.TryGetComp<CompPlantVariety>();
                if (plantComp == null || !plantComp.HasAnyTraits) return;
                if (NovelSeedUtility.IsBeautyStat(stat))
                {
                    float plantOffset = NovelSeedUtility.BeautyOffset(plantComp);
                    if (!Mathf.Approximately(plantOffset, 0f)) __result += plantOffset;
                }
                else if (stat == StatDefOf.Nutrition)
                {
                    float factor = plantComp.ForageNutritionFactor;
                    if (!Mathf.Approximately(factor, 1f)) __result *= factor;
                }
                return;
            }

            if (!NovelSeedUtility.IsBeautyStat(stat)) return;
            if (!NovelProduceDefCache.Contains(thing.def)) return;
            CompNovelProduceAppearance produceComp = thing.TryGetComp<CompNovelProduceAppearance>();
            float produceOffset = produceComp?.ProduceBeautyOffset ?? 0f;
            if (!Mathf.Approximately(produceOffset, 0f)) __result += produceOffset;
        }
    }

    [HarmonyPatch(typeof(Thing), nameof(Thing.MaxHitPoints), MethodType.Getter)]
    public static class ThingMaxHitPoints_Mutation_Patch
    {
        public static void Postfix(Thing __instance, ref int __result)
        {
            if (!(__instance is Plant plant))
            {
                return;
            }

            CompPlantVariety comp = plant.TryGetComp<CompPlantVariety>();
            if (comp == null || !comp.HasAnyTraits)
            {
                return;
            }

            float factor = NovelSeedUtility.MaxHitPointsFactor(comp);
            if (!Mathf.Approximately(factor, 1f))
            {
                __result = Mathf.Max(1, Mathf.RoundToInt(__result * factor));
            }
        }
    }

    [HarmonyPatch(typeof(JobDriver_PlantWork), nameof(JobDriver_PlantWork.WorkDonePerTick))]
    public static class JobDriverPlantWork_WorkDonePerTick_Mutation_Patch
    {
        public static void Postfix(Plant plant, ref float __result)
        {
            CompPlantVariety comp = plant?.TryGetComp<CompPlantVariety>();
            if (comp == null || !comp.HasAnyTraits)
            {
                return;
            }

            float factor = ExpandedTraitUtility.HarvestWorkFactor(plant);
            if (!Mathf.Approximately(factor, 1f))
            {
                __result /= factor;
            }
        }
    }

    public static class JobDriverPlantSow_MutationWorkUtility
    {
        private static readonly Type DisplayClassType = AccessTools.TypeByName("RimWorld.JobDriver_PlantSow+<>c__DisplayClass5_0");
        private static readonly FieldInfo DriverField = DisplayClassType == null ? null : AccessTools.Field(DisplayClassType, "<>4__this");

        public static MethodBase TickMethod()
        {
            return AccessTools.Method(DisplayClassType, "<MakeNewToils>b__4", new[] { typeof(int) });
        }

        public static MethodBase ProgressMethod()
        {
            return AccessTools.Method(DisplayClassType, "<MakeNewToils>b__5");
        }

        public static JobDriver_PlantSow DriverFromDisplayClass(object displayClass)
        {
            return DriverField?.GetValue(displayClass) as JobDriver_PlantSow;
        }

        public static float SowingWorkFactor(JobDriver_PlantSow driver)
        {
            ThingDef plantDef = driver?.job?.plantDefToSow;
            if (plantDef == null)
            {
                return 1f;
            }
            IntVec3 cell = driver.job.GetTarget(TargetIndex.A).Cell;
            Map map = driver.pawn?.Map;
            return NovelSeedUtility.SowingWorkFactor(plantDef, cell, map);
        }
    }

    [HarmonyPatch]
    public static class JobDriverPlantSow_LaborFactor_Patch
    {
        public class SowingWorkState
        {
            public PlantProperties plantProperties;
            public float originalSowWork;
        }

        public static MethodBase TargetMethod()
        {
            return JobDriverPlantSow_MutationWorkUtility.TickMethod();
        }

        public static void Prefix(object __instance, out SowingWorkState __state)
        {
            __state = null;
            JobDriver_PlantSow driver = JobDriverPlantSow_MutationWorkUtility.DriverFromDisplayClass(__instance);
            ThingDef plantDef = driver?.job?.plantDefToSow;
            if (plantDef?.plant == null)
            {
                return;
            }

            float factor = JobDriverPlantSow_MutationWorkUtility.SowingWorkFactor(driver);
            if (Mathf.Approximately(factor, 1f))
            {
                return;
            }

            __state = new SowingWorkState
            {
                plantProperties = plantDef.plant,
                originalSowWork = plantDef.plant.sowWork
            };
            plantDef.plant.sowWork = Mathf.Max(1f, plantDef.plant.sowWork * factor);
        }

        public static Exception Finalizer(SowingWorkState __state, Exception __exception)
        {
            if (__state?.plantProperties != null)
            {
                __state.plantProperties.sowWork = __state.originalSowWork;
            }
            return __exception;
        }
    }

    [HarmonyPatch]
    public static class JobDriverPlantSow_ProgressFactor_Patch
    {
        public static MethodBase TargetMethod()
        {
            return JobDriverPlantSow_MutationWorkUtility.ProgressMethod();
        }

        public static void Postfix(object __instance, ref float __result)
        {
            JobDriver_PlantSow driver = JobDriverPlantSow_MutationWorkUtility.DriverFromDisplayClass(__instance);
            float factor = JobDriverPlantSow_MutationWorkUtility.SowingWorkFactor(driver);
            if (!Mathf.Approximately(factor, 1f))
            {
                __result = Mathf.Clamp01(__result / factor);
            }
        }
    }
    [HarmonyPatch(typeof(Command_SetPlantToGrow), nameof(Command_SetPlantToGrow.ProcessInput))]
    public static class CommandSetPlantToGrow_ProcessInput_Varieties_Patch
    {
        private static readonly FieldInfo SettablesField = AccessTools.Field(typeof(Command_SetPlantToGrow), "settables");
        private static readonly MethodInfo WarnAsAppropriateMethod = AccessTools.Method(typeof(Command_SetPlantToGrow), "WarnAsAppropriate");
        private static readonly MethodInfo GetPlantListPriorityMethod = AccessTools.Method(typeof(Command_SetPlantToGrow), "GetPlantListPriority");

        public static bool Prefix(Command_SetPlantToGrow __instance, Event ev)
        {
            List<IPlantToGrowSettable> settables = GetOrCreateSettles(__instance);
            if (NicePlantsMenuCompat.TryOpenForGrowers(settables))
            {
                return false;
            }

            List<FloatMenuOption> options = new List<FloatMenuOption>();
            IEnumerable<ThingDef> basePlants = PlantUtility.ValidPlantTypesForGrowers(settables);
            IEnumerable<ThingDef> varietyPlants = (GameComponent_NovelSeeds.Instance?.AllVarieties ?? Enumerable.Empty<VarietyRecord>())
                .Where(variety => variety?.cropDef != null && ExpandedTraitUtility.VarietyMatchesGrowers(variety, settables))
                .Select(variety => variety.cropDef);
            List<ThingDef> plants = basePlants.Concat(varietyPlants).Distinct()
                .Where(p => Command_SetPlantToGrow.IsPlantAvailable(p, __instance.settable.Map))
                .OrderByDescending(p => PlantPriority(__instance, p))
                .ThenBy(p => p.label)
                .ToList();

            foreach (ThingDef plantDef in plants)
            {
                List<VarietyRecord> varieties = GameComponent_NovelSeeds.Instance?.VarietiesFor(plantDef).ToList() ?? new List<VarietyRecord>();
                string label = plantDef.LabelCap;
                if (plantDef.plant.sowMinSkill > 0)
                {
                    label += " (" + "MinSkill".Translate() + ": " + plantDef.plant.sowMinSkill + ")";
                }

                if (varieties.Count == 0)
                {
                    options.Add(new FloatMenuOption(label, delegate { SelectPlant(__instance, settables, plantDef, null); }, plantDef));
                }
                else
                {
                    options.Add(new FloatMenuOption("HNS_SelectVarietySubmenu".Translate(label), delegate { OpenVarietyMenu(__instance, settables, plantDef, varieties); }, plantDef));
                }
            }

            if (options.Any())
            {
                Find.WindowStack.Add(new FloatMenu(options));
            }
            return false;
        }

        private static List<IPlantToGrowSettable> GetOrCreateSettles(Command_SetPlantToGrow command)
        {
            List<IPlantToGrowSettable> settables = SettablesField.GetValue(command) as List<IPlantToGrowSettable>;
            if (settables == null)
            {
                settables = new List<IPlantToGrowSettable>();
                SettablesField.SetValue(command, settables);
            }
            if (command.settable != null && !settables.Contains(command.settable))
            {
                settables.Add(command.settable);
            }
            return settables;
        }

        private static float PlantPriority(Command_SetPlantToGrow command, ThingDef plantDef)
        {
            return GetPlantListPriorityMethod == null ? 0f : (float)GetPlantListPriorityMethod.Invoke(command, new object[] { plantDef });
        }

        private static void OpenVarietyMenu(Command_SetPlantToGrow command, List<IPlantToGrowSettable> settables, ThingDef plantDef, List<VarietyRecord> varieties)
        {
            List<FloatMenuOption> options = new List<FloatMenuOption>();
            string standardLabel = "HNS_SelectStandard".Translate(plantDef.label.CapitalizeFirst());
            if (ExpandedTraitUtility.StandardPlantMatchesGrowers(plantDef, settables))
            {
                options.Add(new FloatMenuOption(standardLabel, delegate { SelectPlant(command, settables, plantDef, null); }, plantDef));
            }
            else
            {
                options.Add(new FloatMenuOption(standardLabel + " (" + "HNS_RequiresZone".Translate("matching") + ")", null, plantDef));
            }
            foreach (VarietyRecord variety in varieties.OrderBy(v => v.Label))
            {
                VarietyRecord localVariety = variety;
                string optionLabel = "HNS_SelectVariety".Translate(localVariety.Label, NovelSeedUtility.TraitSummary(localVariety.traits));
                if (!ExpandedTraitUtility.VarietyMatchesGrowers(localVariety, settables))
                {
                    optionLabel += " (" + "HNS_RequiresZone".Translate(ExpandedTraitUtility.ZoneLabel(localVariety)) + ")";
                    options.Add(new FloatMenuOption(optionLabel, null, plantDef));
                }
                else
                {
                    options.Add(new FloatMenuOption(optionLabel, delegate { SelectPlant(command, settables, plantDef, localVariety); }, plantDef));
                }
            }
            List<VarietyRecord> breedingCandidates = varieties
                .Where(variety => ExpandedTraitUtility.VarietyMatchesGrowers(variety, settables)).ToList();
            if (breedingCandidates.Count >= 2)
            {
                options.Add(new FloatMenuOption("HNS_SelectBreedingMix".Translate(), delegate
                {
                    Find.WindowStack.Add(new Dialog_BreedingMix(settables, plantDef, breedingCandidates,
                        delegate { WarnAsAppropriateMethod?.Invoke(command, new object[] { plantDef }); }, null));
                }, plantDef));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private static void SelectPlant(Command_SetPlantToGrow command, List<IPlantToGrowSettable> settables, ThingDef plantDef, VarietyRecord variety)
        {
            foreach (IPlantToGrowSettable settable in settables)
            {
                settable.SetPlantDefToGrow(plantDef);
                GameComponent_NovelSeeds.Instance?.SetSelectedVariety(settable, variety);
            }
            PlayerKnowledgeDatabase.KnowledgeDemonstrated(ConceptDefOf.SetGrowingZonePlant, KnowledgeAmount.Total);
            WarnAsAppropriateMethod?.Invoke(command, new object[] { plantDef });
        }
    }

    public static class IPlantToGrowSettable_SetPlantDefToGrow_ClearVariety_Patch
    {
        private static bool applied;

        public static void Apply(Harmony harmony)
        {
            if (applied || harmony == null) return;
            applied = true;
            HarmonyMethod postfix = new HarmonyMethod(typeof(IPlantToGrowSettable_SetPlantDefToGrow_ClearVariety_Patch), nameof(Postfix));
            Type interfaceType = typeof(IPlantToGrowSettable);
            foreach (Type type in GenTypes.AllTypes)
            {
                if (type != interfaceType && interfaceType.IsAssignableFrom(type))
                {
                    MethodInfo method = AccessTools.DeclaredMethod(type, "SetPlantDefToGrow");
                    if (method != null) harmony.Patch(method, postfix: postfix);
                }
            }
        }

        public static void Postfix(object __instance)
        {
            if (__instance is IPlantToGrowSettable settable)
            {
                GameComponent_NovelSeeds.Instance?.ClearSelectedVariety(settable);
            }
        }
    }
}
