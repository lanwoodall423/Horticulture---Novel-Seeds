using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace HorticultureNovelSeeds
{
    public static class NicePlantsMenuCompat
    {
        private const string PackageId = "Andromeda.NicePlantsMenu";
        private static readonly Color NicePlantsGray = new Color(0.72f, 0.72f, 0.72f);
        private static readonly ConditionalWeakTable<object, VarietyHolder> selectedVarietiesByRecord = new ConditionalWeakTable<object, VarietyHolder>();
        private static readonly FieldInfo DefCachedLabelCapField = AccessTools.Field(typeof(Def), "cachedLabelCap");
        private static bool patchApplied;
        private static bool warnedMissingBrowser;
        private static bool warnedPatchFailure;
        private static bool warnedApplyFailure;
        private static bool warnedSelectFailure;
        private static Type dialogType;
        private static Type plantRecordType;
        private static MethodInfo selectMethod;
        private static MethodInfo drawInfoMethod;
        private static MethodInfo drawRelatedRecipesMethod;
        private static MethodInfo doFilterMethod;
        private static MethodInfo sewAvailableMethod;
        private static MethodInfo setPlantDefToGrowMethod;
        private static FieldInfo plantZonesField;
        private static FieldInfo plantRecordPlantField;
        private static FieldInfo selectedListField;
        private static FieldInfo drawInfoForField;
        private static FieldInfo hoveredInfoField;
        private static FieldInfo currentField;
        private static FieldInfo lastShowedInfoField;
        private static FieldInfo cannotBeHoveredTicksField;
        private static FieldInfo ticksDelayInfoField;
        private static FieldInfo shouldRecheckCurrentPlantsField;

        public static bool IsHandlingPlantMenu()
        {
            if (!IsModActive())
            {
                return false;
            }

            Type settingsType = AccessTools.TypeByName("NicePlantsMenu.Settings");
            FieldInfo enableModField = settingsType == null ? null : AccessTools.Field(settingsType, "EnableMod");
            if (enableModField != null && enableModField.GetValue(null) is bool enabled)
            {
                if (enabled)
                {
                    Apply(HorticultureNovelSeedsMod.HarmonyInstance);
                }
                return enabled;
            }

            bool hasPlantBrowser = AccessTools.TypeByName("NicePlantsMenu.Dialog_PlantBrowser") != null;
            if (hasPlantBrowser)
            {
                Apply(HorticultureNovelSeedsMod.HarmonyInstance);
            }
            return hasPlantBrowser;
        }

        public static bool TryOpenForGrowers(List<IPlantToGrowSettable> growers)
        {
            if (!IsHandlingPlantMenu() || !EnsureMembers() || growers.NullOrEmpty()) return false;
            try
            {
                Window dialog = Activator.CreateInstance(dialogType) as Window;
                if (dialog == null) return false;
                plantZonesField.SetValue(dialog, growers.Where(grower => grower != null).Distinct().ToList());
                Find.WindowStack.Add(dialog);
                return true;
            }
            catch (Exception exception)
            {
                Log.WarningOnce("[Horticulture - Novel Seeds] Could not open Nice Plants Menu for a custom growing zone. " + exception, 804214419);
                return false;
            }
        }

        public static void Apply(Harmony harmony)
        {
            if (patchApplied || !IsModActive() || harmony == null)
            {
                return;
            }
            if (!EnsureMembers())
            {
                if (!warnedMissingBrowser)
                {
                    warnedMissingBrowser = true;
                    Log.Warning("[Horticulture - Novel Seeds] Nice Plants Menu is active, but its plant browser could not be found for variety integration.");
                }
                return;
            }

            try
            {
                harmony.Patch(selectMethod, prefix: new HarmonyMethod(AccessTools.Method(typeof(NicePlantsMenuCompat), nameof(SelectPrefix))));
                harmony.Patch(drawInfoMethod, prefix: new HarmonyMethod(AccessTools.Method(typeof(NicePlantsMenuCompat), nameof(DrawInfoPrefix))), finalizer: new HarmonyMethod(AccessTools.Method(typeof(NicePlantsMenuCompat), nameof(DrawInfoFinalizer))));
                harmony.Patch(drawRelatedRecipesMethod, postfix: new HarmonyMethod(AccessTools.Method(typeof(NicePlantsMenuCompat), nameof(DrawRelatedRecipesPostfix))));
                harmony.Patch(sewAvailableMethod, postfix: new HarmonyMethod(AccessTools.Method(typeof(NicePlantsMenuCompat), nameof(SewAvailablePostfix))));
                if (doFilterMethod != null) harmony.Patch(doFilterMethod, postfix: new HarmonyMethod(AccessTools.Method(typeof(NicePlantsMenuCompat), nameof(DoFilterPostfix))));
                patchApplied = true;
                Log.Message("[Horticulture - Novel Seeds] Nice Plants Menu variety integration enabled.");
            }
            catch (Exception ex)
            {
                if (!warnedPatchFailure)
                {
                    warnedPatchFailure = true;
                    Log.Warning("[Horticulture - Novel Seeds] Nice Plants Menu variety integration patch failed; Nice Plants Menu will use its default selection behavior. " + ex);
                }
            }
        }

        public static bool SelectPrefix(object __instance, [HarmonyArgument("record")] object plantRecord)
        {
            try
            {
                if (!EnsureMembers())
                {
                    return true;
                }

                ThingDef plantDef = plantRecordPlantField.GetValue(plantRecord) as ThingDef;
                if (plantDef == null)
                {
                    return true;
                }

                List<VarietyRecord> varieties = GameComponent_NovelSeeds.Instance?.VarietiesFor(plantDef).ToList() ?? new List<VarietyRecord>();
                if (varieties.Count == 0)
                {
                    SetRecordVariety(plantRecord, null);
                    return true;
                }

                List<IPlantToGrowSettable> plantZones = plantZonesField.GetValue(__instance) as List<IPlantToGrowSettable>;
                if (plantZones == null || plantZones.Count == 0)
                {
                    return true;
                }

                PlantVarietySelectionUtility.OpenVarietyMenu(plantZones, plantDef, varieties, delegate(VarietyRecord selectedVariety)
                {
                    MirrorNicePlantsSelection(__instance, plantRecord, selectedVariety);
                }, ApplyPlantDefThroughNicePlantsMenu);
                return false;
            }
            catch (Exception ex)
            {
                if (!warnedSelectFailure)
                {
                    warnedSelectFailure = true;
                    Log.Warning("[Horticulture - Novel Seeds] Nice Plants Menu variety selection failed; falling back to Nice Plants Menu default selection. " + ex);
                }
                return true;
            }
        }

        public static void DrawInfoPrefix(object __instance, out NicePlantsInfoOverrideState __state)
        {
            __state = null;
            if (!EnsureMembers())
            {
                return;
            }

            object plantRecord = drawInfoForField?.GetValue(__instance);
            VarietyRecord variety = VarietyForRecord(__instance, plantRecord);
            if (variety == null)
            {
                return;
            }

            ThingDef plantDef = plantRecordPlantField.GetValue(plantRecord) as ThingDef;
            if (plantDef?.plant == null || variety.cropDef != plantDef)
            {
                return;
            }

            __state = NicePlantsInfoOverrideState.Apply(plantDef, variety);
        }

        public static Exception DrawInfoFinalizer(NicePlantsInfoOverrideState __state, Exception __exception)
        {
            __state?.Restore();
            return __exception;
        }

        public static void DrawRelatedRecipesPostfix(object __instance, [HarmonyArgument("y")] ref float y, [HarmonyArgument("innerRect")] Rect innerRect)
        {
            try
            {
                DrawNovelSeedsInfo(__instance, ref y, innerRect);
            }
            catch (Exception ex)
            {
                Log.WarningOnce("[Horticulture - Novel Seeds] Nice Plants Menu variety info drawing failed. " + ex, 804214411);
            }
        }

        public static void SewAvailablePostfix(
            [HarmonyArgument(0)] ThingDef plantDef,
            [HarmonyArgument(1)] IPlantToGrowSettable grower,
            ref bool __result)
        {
            if (!NovelSeedUtility.IsGrowableCrop(plantDef))
            {
                __result = false;
                return;
            }
            if (__result || plantDef == null || grower == null) return;
            IEnumerable<VarietyRecord> varieties = GameComponent_NovelSeeds.Instance?.VarietiesFor(plantDef);
            if (varieties != null && varieties.Any(variety => ExpandedTraitUtility.VarietyCanExposePlantInGrower(variety, grower)))
            {
                __result = true;
            }
        }

        public static void DoFilterPostfix([HarmonyArgument(0)] object plantRecord, ref bool __result)
        {
            if (!__result || plantRecord == null || plantRecordPlantField == null) return;
            ThingDef plantDef = plantRecordPlantField.GetValue(plantRecord) as ThingDef;
            if (!NovelSeedUtility.IsGrowableCrop(plantDef)) __result = false;
        }

        private static void DrawNovelSeedsInfo(object dialog, ref float y, Rect innerRect)
        {
            if (!EnsureMembers())
            {
                return;
            }

            object plantRecord = drawInfoForField?.GetValue(dialog);
            VarietyRecord variety = VarietyForRecord(dialog, plantRecord);
            ThingDef plantDef = plantRecord == null ? null : plantRecordPlantField.GetValue(plantRecord) as ThingDef;
            List<VarietyRecord> breedingMix = BreedingMixForRecord(dialog, plantRecord);
            if (plantDef == null)
            {
                return;
            }
            if (variety == null && breedingMix.Count >= 2)
            {
                y += 10f;
                DrawNicePlantsHeader(ref y, innerRect, "HNS_BreedingMixHeader".Translate().ToString());
                DrawNicePlantsLine(ref y, innerRect, "HNS_BreedingMixMembers".Translate(string.Join(", ", breedingMix.Select(item => item.Label).ToArray())).ToString());
                foreach (VarietyRecord member in breedingMix)
                    DrawNicePlantsLine(ref y, innerRect, member.Label + ": " + NovelSeedUtility.TraitSummary(member.traits));
                y += 4f;
                return;
            }
            if (variety == null || variety.cropDef != plantDef) return;

            List<VarietyTraitDef> traits = variety.traits?.Where(t => t != null).ToList() ?? new List<VarietyTraitDef>();
            List<string> effects = NovelSeedUtility.StatChangeLines(traits, plantDef);
            y += 10f;

            DrawNicePlantsHeader(ref y, innerRect, "HNS_NicePlantsMenuVarietyHeader".Translate().ToString());
            DrawSelectedVarietyRow(dialog, ref y, innerRect, variety);

            if (traits.Count > 0)
            {
                DrawNicePlantsHeader(ref y, innerRect, "HNS_Traits".Translate().ToString());
                foreach (VarietyTraitDef trait in traits)
                {
                    DrawNicePlantsTraitRow(ref y, innerRect, trait);
                }
            }

            if (effects.Count > 0)
            {
                DrawNicePlantsHeader(ref y, innerRect, "HNS_NicePlantsMenuEffectsHeader".Translate().ToString());
                foreach (string effect in effects)
                {
                    DrawNicePlantsLine(ref y, innerRect, "HNS_NicePlantsMenuBullet".Translate(effect).ToString());
                }
            }

            if (!variety.FirstDiscoveredInfo.NullOrEmpty())
            {
                DrawNicePlantsHeader(ref y, innerRect, "HNS_FirstDiscoveredHeader".Translate().ToString());
                DrawNicePlantsLine(ref y, innerRect, variety.FirstDiscoveredInfo);
            }

            DrawNicePlantsHeader(ref y, innerRect, "Trait Balance");
            DrawNicePlantsLine(ref y, innerRect, NovelSeedUtility.TraitBalanceSummary(traits));

            y += 4f;
        }

        private static void DrawSelectedVarietyRow(object dialog, ref float y, Rect innerRect, VarietyRecord variety)
        {
            Rect rowRect = new Rect(innerRect.x, y, innerRect.width, 32f);
            Rect buttonRect = new Rect(rowRect.xMax - 88f, rowRect.y + 1f, 86f, 28f);
            Rect labelRect = new Rect(rowRect.x, rowRect.y + 4f, rowRect.width - 96f, 24f);
            Widgets.Label(labelRect, "HNS_NicePlantsMenuVarietyTitle".Translate(variety.Label));
            if (Widgets.ButtonText(buttonRect, "HNS_RenameVariety".Translate()))
            {
                Find.WindowStack.Add(new Dialog_RenameVariety(variety));
                lastShowedInfoField?.SetValue(dialog, null);
            }
            y += rowRect.height + 2f;
        }

        private static void DrawNicePlantsHeader(ref float y, Rect innerRect, string label)
        {
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
            Color previous = GUI.color;
            GUI.color = NicePlantsGray;
            float height = Mathf.Max(24f, Text.CalcHeight(label, innerRect.width));
            Widgets.Label(new Rect(innerRect.x, y, innerRect.width, height), label);
            GUI.color = previous;
            y += height + 2f;
        }

        private static void DrawNicePlantsTraitRow(ref float y, Rect innerRect, VarietyTraitDef trait)
        {
            string label = "HNS_NicePlantsMenuBullet".Translate(trait.LabelCap).ToString();
            float height = Mathf.Max(24f, Text.CalcHeight(label, innerRect.width - 8f));
            Rect rowRect = new Rect(innerRect.x + 8f, y, innerRect.width - 8f, height);
            Widgets.DrawHighlightIfMouseover(rowRect);
            Widgets.Label(rowRect, label);
            if (!trait.description.NullOrEmpty())
            {
                TooltipHandler.TipRegion(rowRect, trait.LabelCap + "\n\n" + trait.description);
            }
            y += height + 2f;
        }

        private static void DrawNicePlantsLine(ref float y, Rect innerRect, string label)
        {
            float height = Mathf.Max(24f, Text.CalcHeight(label, innerRect.width - 8f));
            Rect rowRect = new Rect(innerRect.x + 8f, y, innerRect.width - 8f, height);
            Widgets.Label(rowRect, label);
            y += height + 2f;
        }

        private static bool IsModActive()
        {
            return ModsConfig.IsActive(PackageId) || ModsConfig.IsActive(PackageId.ToLowerInvariant());
        }

        private static bool EnsureMembers()
        {
            if (dialogType != null && plantRecordType != null && selectMethod != null && drawInfoMethod != null && drawRelatedRecipesMethod != null && sewAvailableMethod != null && plantZonesField != null && plantRecordPlantField != null && drawInfoForField != null)
            {
                return true;
            }

            dialogType = AccessTools.TypeByName("NicePlantsMenu.Dialog_PlantBrowser");
            plantRecordType = AccessTools.TypeByName("NicePlantsMenu.PlantRecord");
            if (dialogType == null || plantRecordType == null)
            {
                return false;
            }

            selectMethod = AccessTools.Method(dialogType, "Select", new[] { plantRecordType });
            drawInfoMethod = AccessTools.Method(dialogType, "DrawInfo");
            drawRelatedRecipesMethod = AccessTools.Method(dialogType, "DrawRelatedRecipes");
            doFilterMethod = AccessTools.Method(dialogType, "DoFilter", new[] { plantRecordType });
            sewAvailableMethod = AccessTools.Method(dialogType, "SewAvailable");
            setPlantDefToGrowMethod = AccessTools.Method(dialogType, "SetPlantDefToGrow");
            plantZonesField = AccessTools.Field(dialogType, "plantZones");
            selectedListField = AccessTools.Field(dialogType, "selectedList");
            drawInfoForField = AccessTools.Field(dialogType, "drawInfoFor");
            hoveredInfoField = AccessTools.Field(dialogType, "hoveredInfo");
            currentField = AccessTools.Field(dialogType, "current");
            lastShowedInfoField = AccessTools.Field(dialogType, "lastShowedInfo");
            cannotBeHoveredTicksField = AccessTools.Field(dialogType, "cannotBeHoveredTicks");
            ticksDelayInfoField = AccessTools.Field(dialogType, "ticksDelayInfo");
            shouldRecheckCurrentPlantsField = AccessTools.Field(dialogType, "shouldRecheckCurrentPlants");
            plantRecordPlantField = AccessTools.Field(plantRecordType, "plant");

            return selectMethod != null && drawInfoMethod != null && drawRelatedRecipesMethod != null && sewAvailableMethod != null && plantZonesField != null && plantRecordPlantField != null && drawInfoForField != null;
        }

        private static void ApplyPlantDefThroughNicePlantsMenu(List<IPlantToGrowSettable> settables, ThingDef plantDef)
        {
            if (settables == null || plantDef == null)
            {
                return;
            }

            if (EnsureMembers() && setPlantDefToGrowMethod != null)
            {
                try
                {
                    setPlantDefToGrowMethod.Invoke(null, new object[] { settables, plantDef });
                    return;
                }
                catch (Exception ex)
                {
                    if (!warnedApplyFailure)
                    {
                        warnedApplyFailure = true;
                        Log.Warning("[Horticulture - Novel Seeds] Nice Plants Menu plant setter failed; falling back to direct plant assignment. " + ex);
                    }
                }
            }

            foreach (IPlantToGrowSettable settable in settables)
            {
                settable?.SetPlantDefToGrow(plantDef);
            }
        }

        private static void MirrorNicePlantsSelection(object dialog, object plantRecord, VarietyRecord variety)
        {
            SetRecordVariety(plantRecord, variety);
            if (variety == null)
            {
                List<VarietyRecord> breeding = PersistedBreedingMixForRecord(dialog, plantRecord);
                if (breeding.Count >= 2) SetRecordBreedingMix(plantRecord, breeding);
            }
            cannotBeHoveredTicksField?.SetValue(dialog, 100);
            ticksDelayInfoField?.SetValue(dialog, 30);
            hoveredInfoField?.SetValue(dialog, null);
            drawInfoForField?.SetValue(dialog, plantRecord);

            IList selectedList = selectedListField?.GetValue(dialog) as IList;
            if (selectedList != null)
            {
                selectedList.Clear();
                selectedList.Add(plantRecord);
            }

            shouldRecheckCurrentPlantsField?.SetValue(null, false);
            lastShowedInfoField?.SetValue(dialog, null);
            (currentField?.GetValue(dialog) as IList)?.Clear();
        }

        private static void SetRecordVariety(object plantRecord, VarietyRecord variety)
        {
            if (plantRecord == null)
            {
                return;
            }
            selectedVarietiesByRecord.Remove(plantRecord);
            if (variety != null)
            {
                selectedVarietiesByRecord.Add(plantRecord, new VarietyHolder(variety));
            }
        }

        private static void SetRecordBreedingMix(object plantRecord, IEnumerable<VarietyRecord> varieties)
        {
            if (plantRecord == null) return;
            selectedVarietiesByRecord.Remove(plantRecord);
            List<VarietyRecord> mix = varieties?.Where(item => item != null).GroupBy(item => item.id)
                .Select(group => group.First()).OrderBy(item => item.id).ToList() ?? new List<VarietyRecord>();
            if (mix.Count >= 2) selectedVarietiesByRecord.Add(plantRecord, new VarietyHolder(mix));
        }

        private static VarietyRecord VarietyForRecord(object dialog, object plantRecord)
        {
            if (plantRecord == null)
            {
                return null;
            }
            if (selectedVarietiesByRecord.TryGetValue(plantRecord, out VarietyHolder holder))
            {
                return holder.variety;
            }

            VarietyRecord selected = PersistedVarietyForRecord(dialog, plantRecord);
            if (selected != null)
            {
                SetRecordVariety(plantRecord, selected);
            }
            return selected;
        }

        private static List<VarietyRecord> BreedingMixForRecord(object dialog, object plantRecord)
        {
            if (plantRecord == null) return new List<VarietyRecord>();
            if (selectedVarietiesByRecord.TryGetValue(plantRecord, out VarietyHolder holder) && holder.breedingMix.Count >= 2)
                return holder.breedingMix;
            List<VarietyRecord> persisted = PersistedBreedingMixForRecord(dialog, plantRecord);
            if (persisted.Count >= 2) SetRecordBreedingMix(plantRecord, persisted);
            return persisted;
        }

        private static List<VarietyRecord> PersistedBreedingMixForRecord(object dialog, object plantRecord)
        {
            ThingDef plantDef = plantRecordPlantField?.GetValue(plantRecord) as ThingDef;
            IList plantZones = plantZonesField?.GetValue(dialog) as IList;
            GameComponent_NovelSeeds component = GameComponent_NovelSeeds.Instance;
            if (plantDef == null || plantZones == null || component == null) return new List<VarietyRecord>();
            List<VarietyRecord> result = null;
            foreach (object item in plantZones)
            {
                if (!(item is IPlantToGrowSettable settable) || settable.GetPlantDefToGrow() != plantDef) continue;
                List<VarietyRecord> current = component.BreedingVarietiesFor(settable).Where(variety => variety.cropDef == plantDef).ToList();
                if (current.Count < 2) return new List<VarietyRecord>();
                if (result == null) result = current;
                else if (!result.Select(variety => variety.id).SequenceEqual(current.Select(variety => variety.id))) return new List<VarietyRecord>();
            }
            return result ?? new List<VarietyRecord>();
        }

        private static VarietyRecord PersistedVarietyForRecord(object dialog, object plantRecord)
        {
            ThingDef plantDef = plantRecordPlantField?.GetValue(plantRecord) as ThingDef;
            if (dialog == null || plantDef == null)
            {
                return null;
            }

            IList plantZones = plantZonesField?.GetValue(dialog) as IList;
            GameComponent_NovelSeeds component = GameComponent_NovelSeeds.Instance;
            if (plantZones == null || component == null)
            {
                return null;
            }

            VarietyRecord selected = null;
            bool sawMatchingGrower = false;
            foreach (object item in plantZones)
            {
                if (!(item is IPlantToGrowSettable settable))
                {
                    continue;
                }

                ThingDef currentPlant = settable.GetPlantDefToGrow();
                if (currentPlant != plantDef)
                {
                    continue;
                }

                sawMatchingGrower = true;
                VarietyRecord growerVariety = component.SelectedVarietyFor(settable);
                if (growerVariety == null)
                {
                    if (selected != null)
                    {
                        return null;
                    }
                    continue;
                }
                if (growerVariety.cropDef != plantDef)
                {
                    return null;
                }
                if (selected != null && selected.id != growerVariety.id)
                {
                    return null;
                }
                selected = growerVariety;
            }

            return sawMatchingGrower ? selected : null;
        }

        private static string VarietyLabel(ThingDef plantDef, VarietyRecord variety)
        {
            return variety.Label + " " + plantDef.LabelCap;
        }

        private static TaggedString GetCachedLabelCap(ThingDef def)
        {
            if (DefCachedLabelCapField == null)
            {
                return (TaggedString)null;
            }
            return (TaggedString)DefCachedLabelCapField.GetValue(def);
        }

        private static void ClearCachedLabelCap(ThingDef def)
        {
            SetCachedLabelCap(def, (TaggedString)null);
        }

        private static void SetCachedLabelCap(ThingDef def, TaggedString value)
        {
            DefCachedLabelCapField?.SetValue(def, value);
        }
        private class VarietyHolder
        {
            public readonly VarietyRecord variety;
            public readonly List<VarietyRecord> breedingMix;

            public VarietyHolder(VarietyRecord variety)
            {
                this.variety = variety;
                breedingMix = new List<VarietyRecord>();
            }

            public VarietyHolder(List<VarietyRecord> breedingMix)
            {
                this.breedingMix = breedingMix ?? new List<VarietyRecord>();
            }
        }

        public class NicePlantsInfoOverrideState
        {
            private readonly ThingDef plantDef;
            private readonly PlantProperties plantProperties;
            private readonly string originalLabel;
            private readonly string originalDescription;
            private readonly TaggedString originalCachedLabelCap;
            private readonly float originalHarvestYield;
            private readonly float originalHarvestWork;
            private readonly float originalSowWork;
            private readonly float originalHarvestAfterGrowth;
            private readonly float originalMinGrowthTemperature;
            private readonly float originalMinOptimalGrowthTemperature;
            private readonly float originalMaxOptimalGrowthTemperature;
            private readonly float originalMaxGrowthTemperature;
            private readonly List<StatModifier> originalStatBases;

            private NicePlantsInfoOverrideState(ThingDef plantDef)
            {
                this.plantDef = plantDef;
                plantProperties = plantDef.plant;
                originalLabel = plantDef.label;
                originalDescription = plantDef.description;
                originalCachedLabelCap = GetCachedLabelCap(plantDef);
                originalHarvestYield = plantProperties.harvestYield;
                originalHarvestWork = plantProperties.harvestWork;
                originalSowWork = plantProperties.sowWork;
                originalHarvestAfterGrowth = plantProperties.harvestAfterGrowth;
                originalMinGrowthTemperature = plantProperties.minGrowthTemperature;
                originalMinOptimalGrowthTemperature = plantProperties.minOptimalGrowthTemperature;
                originalMaxOptimalGrowthTemperature = plantProperties.maxOptimalGrowthTemperature;
                originalMaxGrowthTemperature = plantProperties.maxGrowthTemperature;
                originalStatBases = CopyStatBases(plantDef.statBases);
            }

            public static NicePlantsInfoOverrideState Apply(ThingDef plantDef, VarietyRecord variety)
            {
                NicePlantsInfoOverrideState state = new NicePlantsInfoOverrideState(plantDef);
                float yieldFactor = NovelSeedUtility.YieldFactor(variety.traits);
                float harvestWorkFactor = ExpandedTraitUtility.HarvestWorkFactor(variety.traits);
                float sowWorkFactor = ExpandedTraitUtility.SowWorkFactor(variety.traits);
                float perennialResetGrowth = NovelSeedUtility.PerennialHarvestAfterGrowth(variety.traits);
                float beautyOffset = NovelSeedUtility.BeautyOffset(variety.traits);
                NovelSeedUtility.TemperatureOffsets(variety.traits, out float coldOffset, out float heatOffset);

                plantDef.label = VarietyLabel(plantDef, variety);
                ClearCachedLabelCap(plantDef);
                state.plantProperties.harvestYield *= yieldFactor;
                state.plantProperties.harvestWork = UnityEngine.Mathf.Max(1f, state.plantProperties.harvestWork * harvestWorkFactor);
                state.plantProperties.sowWork = UnityEngine.Mathf.Max(1f, state.plantProperties.sowWork * sowWorkFactor);
                if (perennialResetGrowth > 0f)
                {
                    state.plantProperties.harvestAfterGrowth = UnityEngine.Mathf.Max(state.plantProperties.harvestAfterGrowth, perennialResetGrowth);
                }
                state.plantProperties.minGrowthTemperature += coldOffset;
                state.plantProperties.minOptimalGrowthTemperature += coldOffset;
                state.plantProperties.maxOptimalGrowthTemperature += heatOffset;
                state.plantProperties.maxGrowthTemperature += heatOffset;
                state.ApplyBeautyOffset(beautyOffset);
                return state;
            }

            public void Restore()
            {
                plantDef.label = originalLabel;
                plantDef.description = originalDescription;
                SetCachedLabelCap(plantDef, originalCachedLabelCap);
                plantProperties.harvestYield = originalHarvestYield;
                plantProperties.harvestWork = originalHarvestWork;
                plantProperties.sowWork = originalSowWork;
                plantProperties.harvestAfterGrowth = originalHarvestAfterGrowth;
                plantProperties.minGrowthTemperature = originalMinGrowthTemperature;
                plantProperties.minOptimalGrowthTemperature = originalMinOptimalGrowthTemperature;
                plantProperties.maxOptimalGrowthTemperature = originalMaxOptimalGrowthTemperature;
                plantProperties.maxGrowthTemperature = originalMaxGrowthTemperature;
                plantDef.statBases = CopyStatBases(originalStatBases);
            }

            private void ApplyBeautyOffset(float beautyOffset)
            {
                if (UnityEngine.Mathf.Approximately(beautyOffset, 0f))
                {
                    return;
                }
                ApplyStatBaseOffset("Beauty", beautyOffset);
                ApplyStatBaseOffset("BeautyOutdoors", beautyOffset);
            }

            private void ApplyStatBaseOffset(string statDefName, float offset)
            {
                StatDef stat = DefDatabase<StatDef>.GetNamedSilentFail(statDefName);
                if (stat == null)
                {
                    return;
                }
                if (plantDef.statBases == null)
                {
                    plantDef.statBases = new List<StatModifier>();
                }
                StatModifier modifier = plantDef.statBases.FirstOrDefault(statBase => statBase.stat == stat);
                if (modifier != null)
                {
                    modifier.value += offset;
                }
                else
                {
                    plantDef.statBases.Add(new StatModifier { stat = stat, value = offset });
                }
            }

            private static List<StatModifier> CopyStatBases(List<StatModifier> statBases)
            {
                return statBases?.Select(statBase => new StatModifier { stat = statBase.stat, value = statBase.value }).ToList();
            }
        }
    }
}

