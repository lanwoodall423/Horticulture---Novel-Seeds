using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace HorticultureNovelSeeds
{
    [HarmonyPatch(typeof(KnowledgeFramework.GameComponent_KnowledgeFramework), nameof(KnowledgeFramework.GameComponent_KnowledgeFramework.FinalizeInit))]
    public static class KnowledgeFramework_FinalizeInit_HorticultureKnowledge_Patch
    {
        public static void Postfix()
        {
            GameComponent_NovelSeeds.Instance?.RetryKnowledgeIntegration();
        }
    }

    [HarmonyPatch(typeof(Game), nameof(Game.FinalizeInit))]
    public static class Game_FinalizeInit_HorticultureKnowledge_Patch
    {
        public static void Postfix(Game __instance)
        {
            __instance?.GetComponent<GameComponent_NovelSeeds>()?.RetryKnowledgeIntegration();
        }
    }

    [HarmonyPatch(typeof(GameComponentUtility), nameof(GameComponentUtility.FinalizeInit))]
    public static class GameComponentUtility_FinalizeInit_HorticultureKnowledge_Patch
    {
        public static void Postfix()
        {
            GameComponent_NovelSeeds.Instance?.RetryKnowledgeIntegration();
        }
    }

    [HarmonyPatch(typeof(Game), nameof(Game.InitNewGame))]
    public static class Game_InitNewGame_HorticultureKnowledge_Patch
    {
        public static void Postfix(Game __instance)
        {
            __instance?.GetComponent<GameComponent_NovelSeeds>()?.RetryKnowledgeIntegration();
        }
    }

    [HarmonyPatch(typeof(Game), nameof(Game.LoadGame))]
    public static class Game_LoadGame_HorticultureKnowledge_Patch
    {
        public static void Postfix(Game __instance)
        {
            __instance?.GetComponent<GameComponent_NovelSeeds>()?.RetryKnowledgeIntegration();
        }
    }

    [HarmonyPatch]
    public static class Plant_TickLong_Knowledge_Patch
    {
        public static IEnumerable<MethodBase> TargetMethods()
        {
            MethodBase method = AccessTools.Method(typeof(Plant), "TickLong");
            if (method != null) yield return method;
        }

        public static void Postfix(Plant __instance)
        {
            if (__instance?.Spawned == true && HorticulturePlantPolicy.IsSupported(__instance.def))
                HorticultureEventRouter.GrowthObserved(null, __instance);
        }
    }

    [HarmonyPatch(typeof(Plant), nameof(Plant.GetGizmos))]
    public static class Plant_GetGizmos_MaskEditor_Patch
    {
        public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> values, Plant __instance)
        {
            foreach (Gizmo gizmo in values ?? Enumerable.Empty<Gizmo>()) yield return gizmo;
            if (!Prefs.DevMode || __instance?.Spawned != true || Find.Selector.SingleSelectedThing != __instance) yield break;
            yield return new Command_Action
            {
                defaultLabel = "DEV: Edit Plant Mask",
                defaultDesc = "Open the mask editor for this plant and its currently displayed texture variation.",
                icon = __instance.def.uiIcon,
                action = delegate
                {
                    Material material = __instance.Graphic?.MatSingleFor(__instance);
                    int variation = PlantMaskUtility.VariationIndexForTexture(__instance.def, material?.mainTexture, 0);
                    Find.WindowStack.Add(new Dialog_PlantMasks(__instance.def, false, variation));
                }
            };
        }
    }

    [HarmonyPatch(typeof(DefGenerator), nameof(DefGenerator.GenerateImpliedDefs_PreResolve))]
    public static class DefGenerator_AddPlantMutationComps_Patch
    {
        public static void Postfix(bool hotReload = false)
        {
            ColorTraitFactory.GenerateAll();
            PercentageTraitFactory.GenerateAll();
            SynergyTraitFactory.GenerateAll();
            TraitCatalogValidation.Run();
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
        public sealed class CompletionState
        {
            public JobDriver_PlantSow driver;
            public Plant plant;
            public float priorWork;
            public float requiredWork;
        }

        public static MethodBase TargetMethod()
        {
            return JobDriverPlantSow_MutationWorkUtility.CompletionMethod();
        }

        public static void Prefix(object __instance, out CompletionState __state)
        {
            __state = null;
            JobDriver_PlantSow driver = JobDriverPlantSow_MutationWorkUtility.DriverFromDisplayClass(__instance);
            Plant plant = driver?.job?.GetTarget(TargetIndex.A).Thing as Plant;
            if (plant?.def?.plant == null) return;
            float requiredWork = JobDriverPlantSow_MutationWorkUtility.AdjustedSowWork(plant.def.plant.sowWork, driver);
            float priorWork = JobDriverPlantSow_MutationWorkUtility.SowWorkDone(driver);
            if (priorWork >= requiredWork) return;
            __state = new CompletionState
            {
                driver = driver,
                plant = plant,
                priorWork = priorWork,
                requiredWork = requiredWork
            };
        }

        public static void Postfix(CompletionState __state)
        {
            if (__state?.plant?.Destroyed != false
                || __state.priorWork >= __state.requiredWork
                || JobDriverPlantSow_MutationWorkUtility.SowWorkDone(__state.driver) < __state.requiredWork)
            {
                return;
            }

            NovelSeedUtility.AssignMutationOnSow(__state.plant, __state.driver?.pawn);
            HorticultureEventRouter.SowingCompleted(__state.driver?.pawn, __state.plant);
            CompPlantVariety comp = __state.plant.TryGetComp<CompPlantVariety>();
            NovelSeedUtility.ApplyJoyResinThought(__state.driver?.pawn, comp?.ActiveTraits);
            ExpandedTraitUtility.ApplyResinEffects(__state.driver?.pawn, __state.plant, comp?.ActiveTraits);
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
        public int yield;
        public bool regularHarvest;
        public bool cutting;
        public bool harvestable;
        public string eventIdentity;
        public float perennialResetGrowth;
        public bool pendingDiscoveryHarvest;
        public bool shouldSaveSeeds;
        public bool shouldApplyHarvestEffects;
    }

    [HarmonyPatch(typeof(Plant), nameof(Plant.PlantCollected))]
    public static class PlantCollected_DropMutationSeed_Patch
    {
        private const PlantDestructionMode HarvestMode = (PlantDestructionMode)2;
        private const PlantDestructionMode CutMode = (PlantDestructionMode)3;
        private static readonly MethodInfo HarvestDestroysGetter = AccessTools.PropertyGetter(typeof(PlantProperties), nameof(PlantProperties.HarvestDestroys));
        private static readonly MethodInfo EffectiveHarvestDestroysMethod = AccessTools.Method(
            typeof(PlantCollected_DropMutationSeed_Patch), nameof(EffectiveHarvestDestroys),
            new[] { typeof(bool), typeof(Plant), typeof(PlantDestructionMode) });

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            foreach (CodeInstruction instruction in instructions)
            {
                yield return instruction;
                if (instruction.Calls(HarvestDestroysGetter))
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldarg_2);
                    yield return new CodeInstruction(OpCodes.Call, EffectiveHarvestDestroysMethod);
                }
            }
        }

        public static bool EffectiveHarvestDestroys(bool baseValue, Plant plant, PlantDestructionMode mode)
        {
            if (!baseValue) return false;
            if (mode != HarvestMode) return true;
            return EffectiveHarvestDestroys(true, true,
                NovelSeedUtility.PerennialHarvestAfterGrowth(plant?.TryGetComp<CompPlantVariety>()));
        }

        public static bool EffectiveHarvestDestroys(bool baseValue, bool regularHarvest, float perennialResetGrowth)
        {
            return baseValue && (!regularHarvest || perennialResetGrowth <= 0f);
        }

        public static void Prefix(Plant __instance, Pawn by, PlantDestructionMode plantDestructionMode, out HarvestState __state)
        {
            __state = null;
            CompPlantVariety comp = __instance.TryGetComp<CompPlantVariety>();
            bool regularHarvest = plantDestructionMode == HarvestMode;
            bool supported = HorticulturePlantPolicy.IsSupported(__instance.def);
            bool cutting = plantDestructionMode == CutMode && supported;
            if (regularHarvest && !supported) return;
            if (!regularHarvest && !cutting)
            {
                return;
            }

            List<VarietyTraitDef> activeTraits = comp?.ActiveTraits?.ToList() ?? new List<VarietyTraitDef>();
            bool matureDiscovery = comp?.PendingDiscovery == true && __instance.Growth >= 0.999f && !__instance.Blighted;
            bool harvestable = regularHarvest && __instance.HarvestableNow && !__instance.Blighted;
            int harvestCycle = comp?.BeginHarvestCycle() ?? 0;
            bool repeated = regularHarvest && NovelSeedUtility.PerennialHarvestAfterGrowth(activeTraits) > 0f;
            __state = new HarvestState
            {
                cropDef = __instance.def,
                traits = comp?.DiscoveryTraits?.ToList(),
                activeTraits = activeTraits,
                lineageParentIds = comp?.CrossPollinationParentIds ?? new List<string>(),
                comp = comp,
                position = __instance.Position,
                map = __instance.Map,
                harvester = by,
                plant = __instance,
                yield = harvestable || cutting ? __instance.YieldNow() : 0,
                regularHarvest = regularHarvest,
                cutting = cutting,
                harvestable = harvestable,
                perennialResetGrowth = harvestable ? NovelSeedUtility.PerennialHarvestAfterGrowth(activeTraits) : 0f,
                pendingDiscoveryHarvest = matureDiscovery,
                shouldSaveSeeds = comp?.SaveSeedsRequested == true && matureDiscovery,
                shouldApplyHarvestEffects = harvestable,
                eventIdentity = regularHarvest
                    ? HorticultureKnowledgeEventIdentity.Harvest(__instance, harvestCycle, harvestable, repeated, repeated)
                    : HorticultureKnowledgeEventIdentity.Cutting(__instance, harvestCycle)
            };
        }

        public static void Postfix(Plant __instance, Pawn by, PlantDestructionMode plantDestructionMode, HarvestState __state)
        {
            if (__state?.regularHarvest == true)
            {
                if (!__state.pendingDiscoveryHarvest)
                    HorticultureEventRouter.HarvestCompleted(by, __state.plant, __state.yield, __state.harvestable,
                        __state.perennialResetGrowth > 0f, __state.perennialResetGrowth > 0f, __state.eventIdentity);
            }
            else if (__state?.cutting == true)
            {
                HorticultureEventRouter.CuttingCompleted(by, __state.plant, __state.yield, __state.eventIdentity);
            }
            if (__state?.pendingDiscoveryHarvest == true)
            {
                string origin = __state.plant?.sown == false ? "wild" :
                    __state.lineageParentIds?.Count > 0 ? "cross-pollination" : "mutation";
                if (HorticultureEventRouter.NovelSeedDiscovered(by, __state.cropDef, __state.traits, origin,
                    __state.map, __state.lineageParentIds, __state.eventIdentity, __state.yield,
                    __state.perennialResetGrowth > 0f, __state.perennialResetGrowth > 0f))
                    __state.comp?.ClearPendingDiscovery();
            }
            if (__state?.shouldSaveSeeds == true)
            {
                string origin = __state.plant?.sown == false ? "wild" :
                    __state.lineageParentIds?.Count > 0 ? "cross-pollination" : "mutation";
                NovelSeedUtility.DropDiscoverySeed(__state.cropDef, __state.traits, __state.position, __state.map,
                    __state.lineageParentIds, origin);
            }
            if (__state?.perennialResetGrowth > 0f && __instance?.Destroyed == false)
            {
                __instance.Growth = Mathf.Max(__instance.Growth, __state.perennialResetGrowth);
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
            factor = ExpandedTraitUtility.ApplyDiseaseResistanceFactor(factor, synergy);
            return factor >= 1f || Rand.Chance(factor);
        }

        public static void Postfix(Plant __instance)
        {
            CompPlantVariety comp = __instance.TryGetComp<CompPlantVariety>();
            if (!__instance.Blighted) return;
            if (comp?.HasAnyTraits == true)
            {
                float extraDamageFactor = NovelSeedUtility.BlightDamageFactor(comp);
                if (extraDamageFactor > 1f)
                {
                    float amount = Mathf.Max(1f, __instance.MaxHitPoints * 0.05f * (extraDamageFactor - 1f));
                    __instance.TakeDamage(new DamageInfo(DamageDefOf.Rotting, amount));
                }
            }
            if (GenTemperature.TryGetTemperatureForCell(__instance.Position, __instance.Map, out float temperature))
                HorticultureEventRouter.EnvironmentalStressObserved(null, __instance, temperature,
                    temperature < __instance.def.plant.minOptimalGrowthTemperature, false);
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
            if (visual.IsDefault && (!NovelSeedUtility.HasPlantMaskVisual(comp) || !PlantMaskUtility.HasActiveMasks(__instance.def)))
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
        public static void Postfix(Pawn actor, Plant plant, ref float __result)
        {
            if (plant != null) __result *= PlantKnowledgeUtility.PlantWorkSpeedFactor(actor, plant.def);
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
        private static readonly FieldInfo SowWorkField = AccessTools.Field(typeof(PlantProperties), nameof(PlantProperties.sowWork));
        private static readonly FieldInfo SowWorkDoneField = AccessTools.Field(typeof(JobDriver_PlantSow), "sowWorkDone");
        private static readonly Dictionary<Type, FieldInfo> DriverFields = new Dictionary<Type, FieldInfo>();
        private static List<MethodBase> sowWorkMethods;
        private static MethodBase completionMethod;

        public static IEnumerable<MethodBase> SowWorkMethods()
        {
            EnsureMethods();
            return sowWorkMethods;
        }

        public static MethodBase CompletionMethod()
        {
            EnsureMethods();
            return completionMethod;
        }

        public static JobDriver_PlantSow DriverFromDisplayClass(object displayClass)
        {
            if (displayClass == null) return null;
            Type type = displayClass.GetType();
            if (!DriverFields.TryGetValue(type, out FieldInfo field))
            {
                field = DriverField(type);
                DriverFields[type] = field;
            }
            return field?.GetValue(displayClass) as JobDriver_PlantSow;
        }

        public static float AdjustedSowWork(float baseWork, object displayClass)
        {
            return AdjustedSowWork(baseWork, DriverFromDisplayClass(displayClass));
        }

        public static float AdjustedSowWork(float baseWork, JobDriver_PlantSow driver)
        {
            ThingDef plantDef = driver?.job?.plantDefToSow;
            if (plantDef == null) return Mathf.Max(1f, baseWork);
            IntVec3 cell = driver.job.GetTarget(TargetIndex.A).Cell;
            Map map = driver.pawn?.Map;
            float traitWork = NovelSeedUtility.SowingWorkFactor(plantDef, cell, map);
            float factor = traitWork / PlantKnowledgeUtility.PlantWorkSpeedFactor(driver.pawn, plantDef);
            return Mathf.Max(1f, baseWork * factor);
        }

        public static float SowWorkDone(JobDriver_PlantSow driver)
        {
            return driver == null || SowWorkDoneField == null ? 0f : (float)SowWorkDoneField.GetValue(driver);
        }

        private static void EnsureMethods()
        {
            if (sowWorkMethods != null) return;
            List<MethodBase> readers = new List<MethodBase>();
            BindingFlags nestedFlags = BindingFlags.Public | BindingFlags.NonPublic;
            BindingFlags memberFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;
            foreach (Type nestedType in typeof(JobDriver_PlantSow).GetNestedTypes(nestedFlags))
            {
                FieldInfo driverField = DriverField(nestedType);
                if (driverField == null) continue;
                DriverFields[nestedType] = driverField;
                readers.AddRange(nestedType.GetMethods(memberFlags).Where(method => ReadsField(method, SowWorkField)));
            }

            sowWorkMethods = readers.Distinct().OrderBy(method => method.MetadataToken).ToList();
            completionMethod = sowWorkMethods.SingleOrDefault(method =>
                method is MethodInfo methodInfo
                && methodInfo.ReturnType == typeof(void)
                && method.GetParameters().Length == 1
                && method.GetParameters()[0].ParameterType == typeof(int));
            if (sowWorkMethods.Count == 0 || completionMethod == null)
            {
                throw new MissingMethodException("Could not structurally identify RimWorld's sow-work closures.");
            }
        }

        private static FieldInfo DriverField(Type type)
        {
            return type?.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .FirstOrDefault(field => typeof(JobDriver_PlantSow).IsAssignableFrom(field.FieldType));
        }

        private static bool ReadsField(MethodBase method, FieldInfo field)
        {
            byte[] body = method?.GetMethodBody()?.GetILAsByteArray();
            if (body == null || field == null) return false;
            byte[] token = BitConverter.GetBytes(field.MetadataToken);
            for (int index = 0; index <= body.Length - token.Length; index++)
            {
                bool match = true;
                for (int offset = 0; offset < token.Length; offset++)
                {
                    if (body[index + offset] == token[offset]) continue;
                    match = false;
                    break;
                }
                if (match) return true;
            }
            return false;
        }
    }

    [HarmonyPatch]
    public static class JobDriverPlantSow_LaborFactor_Patch
    {
        private static readonly FieldInfo SowWorkField = AccessTools.Field(typeof(PlantProperties), nameof(PlantProperties.sowWork));
        private static readonly MethodInfo AdjustedSowWorkMethod = AccessTools.Method(typeof(JobDriverPlantSow_MutationWorkUtility), nameof(JobDriverPlantSow_MutationWorkUtility.AdjustedSowWork), new[] { typeof(float), typeof(object) });

        public static IEnumerable<MethodBase> TargetMethods()
        {
            return JobDriverPlantSow_MutationWorkUtility.SowWorkMethods();
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            foreach (CodeInstruction instruction in instructions)
            {
                yield return instruction;
                if (instruction.opcode == OpCodes.Ldfld && Equals(instruction.operand, SowWorkField))
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Call, AdjustedSowWorkMethod);
                }
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
                HorticultureCultivarPresentation authority = HorticulturePresentationPolicy.ForCultivar(localVariety, null, true);
                string optionLabel = "HNS_SelectVariety".Translate(localVariety.Label,
                    authority?.TraitText ?? "Traits not documented");
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
