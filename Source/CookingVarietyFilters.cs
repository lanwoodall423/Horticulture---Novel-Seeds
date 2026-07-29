using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace HorticultureNovelSeeds
{
    internal sealed class BillVarietyFilterState
    {
        private List<string> excludedVarietyIds = new List<string>();
        private List<string> excludedBaseProductDefs = new List<string>();
        private List<string> initializedProductDefs = new List<string>();
        [Unsaved(false)] private HashSet<string> excludedVarietySet;
        [Unsaved(false)] private HashSet<string> excludedBaseProductSet;
        [Unsaved(false)] private HashSet<string> initializedProductSet;

        public bool HasRules { get { EnsureSets(); return initializedProductSet.Count > 0; } }

        public bool Allows(string varietyId)
        {
            EnsureSets();
            return varietyId.NullOrEmpty() || !excludedVarietySet.Contains(varietyId);
        }

        public bool AllowsBase(ThingDef productDef)
        {
            EnsureSets();
            return productDef == null || !excludedBaseProductSet.Contains(productDef.defName);
        }

        public bool Tracks(ThingDef productDef)
        {
            EnsureSets();
            return productDef != null && initializedProductSet.Contains(productDef.defName);
        }

        public void EnsureProduct(ThingDef productDef, IEnumerable<string> varietyIds, bool parentAllowed)
        {
            EnsureSets();
            if (productDef == null || initializedProductSet.Contains(productDef.defName)) return;
            initializedProductDefs.Add(productDef.defName);
            initializedProductSet.Add(productDef.defName);
            SetBaseAllowed(productDef, parentAllowed);
            SetAll(varietyIds, parentAllowed);
        }

        public void SetAllowed(string varietyId, bool allowed)
        {
            if (varietyId.NullOrEmpty()) return;
            EnsureSets();
            if (allowed)
            {
                if (excludedVarietySet.Remove(varietyId)) excludedVarietyIds.Remove(varietyId);
            }
            else if (excludedVarietySet.Add(varietyId)) excludedVarietyIds.Add(varietyId);
        }

        public void SetBaseAllowed(ThingDef productDef, bool allowed)
        {
            if (productDef == null) return;
            EnsureSets();
            if (allowed)
            {
                if (excludedBaseProductSet.Remove(productDef.defName)) excludedBaseProductDefs.Remove(productDef.defName);
            }
            else if (excludedBaseProductSet.Add(productDef.defName)) excludedBaseProductDefs.Add(productDef.defName);
        }

        public void SetAll(IEnumerable<string> varietyIds, bool allowed)
        {
            foreach (string id in varietyIds.Where(id => !id.NullOrEmpty()).Distinct()) SetAllowed(id, allowed);
        }

        public void SetProduct(ThingDef productDef, IEnumerable<string> varietyIds, bool allowed)
        {
            SetBaseAllowed(productDef, allowed);
            SetAll(varietyIds, allowed);
        }

        public bool AnyAllowed(ThingDef productDef, IEnumerable<string> varietyIds)
        {
            return AllowsBase(productDef) || varietyIds.Any(Allows);
        }

        public bool AllAllowed(ThingDef productDef, IEnumerable<string> varietyIds)
        {
            return AllowsBase(productDef) && varietyIds.All(Allows);
        }

        public void CopyFrom(BillVarietyFilterState other)
        {
            excludedVarietyIds = other?.excludedVarietyIds?.ToList() ?? new List<string>();
            excludedBaseProductDefs = other?.excludedBaseProductDefs?.ToList() ?? new List<string>();
            initializedProductDefs = other?.initializedProductDefs?.ToList() ?? new List<string>();
            InvalidateSets();
        }

        public void ExposeData()
        {
            Scribe_Collections.Look(ref excludedVarietyIds, "HNS_excludedIngredientVarieties", LookMode.Value);
            Scribe_Collections.Look(ref excludedBaseProductDefs, "HNS_excludedBaseIngredientProducts", LookMode.Value);
            Scribe_Collections.Look(ref initializedProductDefs, "HNS_initializedIngredientProducts", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                excludedVarietyIds = Normalize(excludedVarietyIds);
                excludedBaseProductDefs = Normalize(excludedBaseProductDefs);
                initializedProductDefs = Normalize(initializedProductDefs);
                InvalidateSets();
            }
        }

        private void InvalidateSets()
        {
            excludedVarietySet = null;
            excludedBaseProductSet = null;
            initializedProductSet = null;
        }

        private void EnsureSets()
        {
            if (excludedVarietySet != null) return;
            excludedVarietySet = new HashSet<string>(excludedVarietyIds ?? Enumerable.Empty<string>());
            excludedBaseProductSet = new HashSet<string>(excludedBaseProductDefs ?? Enumerable.Empty<string>());
            initializedProductSet = new HashSet<string>(initializedProductDefs ?? Enumerable.Empty<string>());
        }

        private static List<string> Normalize(IEnumerable<string> values)
        {
            return values?.Where(value => !value.NullOrEmpty()).Distinct().ToList() ?? new List<string>();
        }
    }

    internal static class BillVarietyFilterUtility
    {
        private static readonly ConditionalWeakTable<Bill_Production, BillVarietyFilterState> States =
            new ConditionalWeakTable<Bill_Production, BillVarietyFilterState>();

        public static BillVarietyFilterState StateFor(Bill_Production bill)
        {
            return bill == null ? null : States.GetOrCreateValue(bill);
        }

        public static bool TryGetState(Bill_Production bill, out BillVarietyFilterState state)
        {
            state = null;
            return bill != null && States.TryGetValue(bill, out state);
        }

        public static List<VarietyRecord> RelevantVarieties(Bill_Production bill)
        {
            if (bill?.recipe?.ingredients.NullOrEmpty() != false) return new List<VarietyRecord>();
            return (GameComponent_NovelSeeds.Instance?.AllVarieties ?? Enumerable.Empty<VarietyRecord>())
                .Where(variety => IsRelevantToBill(bill, variety))
                .GroupBy(variety => variety.id)
                .Select(group => group.First())
                .OrderBy(variety => variety.cropDef.label ?? variety.cropDef.defName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(variety => variety.Label ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static List<VarietyRecord> RelevantVarieties(Bill_Production bill, ThingDef productDef)
        {
            return RelevantVarieties(bill)
                .Where(variety => variety.cropDef?.plant?.harvestedThingDef == productDef)
                .ToList();
        }

        private static bool IsRelevantToBill(Bill_Production bill, VarietyRecord variety)
        {
            ThingDef product = variety?.cropDef?.plant?.harvestedThingDef;
            if (product == null || variety.id.NullOrEmpty()) return false;
            return bill.recipe.ingredients.Any(ingredient => ingredient?.filter?.Allows(product) == true);
        }

        public static bool Allows(Bill_Production bill, Thing thing)
        {
            if (thing == null || !TryGetState(bill, out BillVarietyFilterState state) || !state.HasRules) return true;
            CompNovelProduceAppearance comp = thing.TryGetComp<CompNovelProduceAppearance>();
            if (comp?.DirectVarietyProduce == true)
            {
                foreach (string id in comp.SourceVarietyIds) if (!state.Allows(id)) return false;
                return true;
            }
            return !state.Tracks(thing.def) || state.AllowsBase(thing.def);
        }
    }

    internal static class StorageVarietyFilterUtility
    {
        private static readonly ConditionalWeakTable<StorageSettings, BillVarietyFilterState> States =
            new ConditionalWeakTable<StorageSettings, BillVarietyFilterState>();

        public static BillVarietyFilterState StateFor(StorageSettings settings)
        {
            return settings == null ? null : States.GetOrCreateValue(settings);
        }

        public static bool TryGetState(StorageSettings settings, out BillVarietyFilterState state)
        {
            state = null;
            return settings != null && States.TryGetValue(settings, out state);
        }

        public static List<VarietyRecord> RelevantVarieties(ThingDef productDef)
        {
            if (productDef == null) return new List<VarietyRecord>();
            return (GameComponent_NovelSeeds.Instance?.AllVarieties ?? Enumerable.Empty<VarietyRecord>())
                .Where(variety => variety?.cropDef?.plant?.harvestedThingDef == productDef && !variety.id.NullOrEmpty())
                .GroupBy(variety => variety.id)
                .Select(group => group.First())
                .OrderBy(variety => variety.Label)
                .ToList();
        }

        public static bool Allows(StorageSettings settings, Thing thing)
        {
            if (thing == null || !TryGetState(settings, out BillVarietyFilterState state) || !state.HasRules) return true;
            CompNovelProduceAppearance comp = thing.TryGetComp<CompNovelProduceAppearance>();
            if (comp?.DirectVarietyProduce == true)
            {
                foreach (string id in comp.SourceVarietyIds) if (!state.Allows(id)) return false;
                return true;
            }
            return !state.Tracks(thing.def) || state.AllowsBase(thing.def);
        }
    }
    internal static class VarietyFilterRenderContext
    {
        [ThreadStatic] private static object activeOwner;
        [ThreadStatic] private static ThingFilter activeFilter;
        [ThreadStatic] private static BillVarietyFilterState activeState;
        [ThreadStatic] private static Dictionary<ThingDef, List<VarietyRecord>> varietiesByProduct;

        public static void Begin(Bill_Production bill)
        {
            activeOwner = bill;
            activeFilter = bill?.ingredientFilter;
            activeState = BillVarietyFilterUtility.StateFor(bill);
            varietiesByProduct = BillVarietyFilterUtility.RelevantVarieties(bill)
                .GroupBy(variety => variety.cropDef.plant.harvestedThingDef)
                .ToDictionary(group => group.Key, group => group.ToList());
        }

        public static void Begin(StorageSettings settings)
        {
            activeOwner = settings;
            activeFilter = settings?.filter;
            activeState = StorageVarietyFilterUtility.StateFor(settings);
            varietiesByProduct = (GameComponent_NovelSeeds.Instance?.AllVarieties ?? Enumerable.Empty<VarietyRecord>())
                .Where(variety => variety?.cropDef?.plant?.harvestedThingDef != null && !variety.id.NullOrEmpty())
                .GroupBy(variety => variety.cropDef.plant.harvestedThingDef)
                .ToDictionary(group => group.Key, group => group.GroupBy(variety => variety.id).Select(items => items.First()).OrderBy(variety => variety.Label).ToList());
        }

        public static bool TryGet(ThingFilter filter, ThingDef productDef, out object owner,
            out BillVarietyFilterState state, out List<VarietyRecord> varieties)
        {
            owner = activeOwner;
            state = activeState;
            varieties = null;
            return owner != null && filter == activeFilter && productDef != null && state != null
                && varietiesByProduct != null && varietiesByProduct.TryGetValue(productDef, out varieties)
                && !varieties.NullOrEmpty();
        }

        public static bool IsActive(object owner)
        {
            return owner != null && ReferenceEquals(activeOwner, owner);
        }

        public static void Clear()
        {
            activeOwner = null;
            activeFilter = null;
            activeState = null;
            varietiesByProduct = null;
            VarietyParentCheckboxContext.Clear();
        }
    }

    internal static class VarietyParentCheckboxContext
    {
        [ThreadStatic] private static bool replaceNext;

        public static void Set(bool partial)
        {
            replaceNext = partial;
        }

        public static bool Consume()
        {
            bool result = replaceNext;
            replaceNext = false;
            return result;
        }

        public static void Clear()
        {
            replaceNext = false;
        }
    }
    [HarmonyPatch(typeof(Bill_Production), nameof(Bill_Production.ExposeData))]
    internal static class BillProductionVarietyFilterExposePatch
    {
        private static void Postfix(Bill_Production __instance)
        {
            BillVarietyFilterUtility.StateFor(__instance)?.ExposeData();
        }
    }

    [HarmonyPatch(typeof(Bill_Production), nameof(Bill_Production.Clone))]
    internal static class BillProductionVarietyFilterClonePatch
    {
        private static void Postfix(Bill_Production __instance, Bill __result)
        {
            if (__result is Bill_Production clone)
                BillVarietyFilterUtility.StateFor(clone)?.CopyFrom(BillVarietyFilterUtility.StateFor(__instance));
        }
    }

    [HarmonyPatch(typeof(WorkGiver_DoBill), "IsUsableIngredient")]
    internal static class WorkGiverDoBillVarietyFilterPatch
    {
        private static void Postfix(Thing t, Bill bill, ref bool __result)
        {
            if (__result && bill is Bill_Production production)
                __result = BillVarietyFilterUtility.Allows(production, t);
        }
    }

    [HarmonyPatch(typeof(Dialog_BillConfig), "DoIngredientConfigPane")]
    internal static class DialogBillConfigVarietyFilterContextPatch
    {
        private static void Prefix(Bill_Production ___bill)
        {
            VarietyFilterRenderContext.Begin(___bill);
        }

        private static Exception Finalizer(Exception __exception)
        {
            VarietyFilterRenderContext.Clear();
            return __exception;
        }
    }

    [HarmonyPatch(typeof(ITab_Storage), "FillTab")]
    internal static class StorageTabVarietyFilterContextPatch
    {
        private static void Prefix(ITab_Storage __instance)
        {
            IStoreSettingsParent parent = Traverse.Create(__instance).Property("SelStoreSettingsParent").GetValue<IStoreSettingsParent>();
            VarietyFilterRenderContext.Begin(parent?.GetStoreSettings());
        }

        private static Exception Finalizer(Exception __exception)
        {
            VarietyFilterRenderContext.Clear();
            return __exception;
        }
    }

    [HarmonyPatch(typeof(StorageSettings), nameof(StorageSettings.ExposeData))]
    internal static class StorageSettingsVarietyFilterExposePatch
    {
        private static void Postfix(StorageSettings __instance)
        {
            StorageVarietyFilterUtility.StateFor(__instance)?.ExposeData();
        }
    }

    [HarmonyPatch(typeof(StorageSettings), nameof(StorageSettings.CopyFrom))]
    internal static class StorageSettingsVarietyFilterCopyPatch
    {
        private static void Postfix(StorageSettings __instance, StorageSettings other)
        {
            StorageVarietyFilterUtility.StateFor(__instance)?.CopyFrom(StorageVarietyFilterUtility.StateFor(other));
        }
    }

    [HarmonyPatch(typeof(StorageSettings), nameof(StorageSettings.AllowedToAccept), new[] { typeof(Thing) })]
    internal static class StorageSettingsVarietyFilterAcceptPatch
    {
        private static void Postfix(StorageSettings __instance, Thing t, ref bool __result)
        {
            if (__result) __result = StorageVarietyFilterUtility.Allows(__instance, t);
        }
    }

    [HarmonyPatch(typeof(Widgets), nameof(Widgets.CheckboxDraw), new[]
    {
        typeof(float), typeof(float), typeof(bool), typeof(bool), typeof(float), typeof(Texture2D), typeof(Texture2D)
    })]
    internal static class VarietyParentCheckboxDrawPatch
    {
        private static void Prefix(bool active, ref Texture2D texChecked)
        {
            if (VarietyParentCheckboxContext.Consume() && active) texChecked = Widgets.CheckboxPartialTex;
        }
    }
    [HarmonyPatch(typeof(Listing_TreeThingFilter), "DoThingDef")]
    internal static class ListingTreeThingFilterVarietiesPatch
    {
        private sealed class RowState
        {
            public object owner;
            public BillVarietyFilterState state;
            public ThingFilter filter;
            public ThingDef product;
            public List<VarietyRecord> varieties;
            public bool parentAllowedBefore;
        }

        private static void Prefix(ThingDef tDef, ThingFilter ___filter, ref RowState __state)
        {
            VarietyParentCheckboxContext.Clear();
            if (!VarietyFilterRenderContext.TryGet(___filter, tDef, out object owner,
                out BillVarietyFilterState state, out List<VarietyRecord> varieties)) return;

            List<string> ids = varieties.Select(variety => variety.id).ToList();
            bool parentAllowed = ___filter.Allows(tDef);
            state.EnsureProduct(tDef, ids, parentAllowed);

            bool customAny = state.AnyAllowed(tDef, ids);
            if (parentAllowed != customAny)
            {
                state.SetProduct(tDef, ids, parentAllowed);
                customAny = parentAllowed;
            }

            VarietyParentCheckboxContext.Set(customAny && !state.AllAllowed(tDef, ids));
            __state = new RowState
            {
                owner = owner,
                state = state,
                filter = ___filter,
                product = tDef,
                varieties = varieties,
                parentAllowedBefore = customAny
            };
        }

        private static void Postfix(Listing_TreeThingFilter __instance, ThingDef tDef, int nestLevel, RowState __state)
        {
            VarietyParentCheckboxContext.Clear();
            if (__state == null || !VarietyFilterRenderContext.IsActive(__state.owner)) return;
            BillVarietyFilterState state = __state.state;
            List<string> ids = __state.varieties.Select(variety => variety.id).ToList();
            bool parentAllowedAfter = __state.filter.Allows(tDef);
            if (parentAllowedAfter != __state.parentAllowedBefore) state.SetProduct(tDef, ids, parentAllowedAfter);

            bool anyAllowed = state.AnyAllowed(tDef, ids);
            if (__state.filter.Allows(tDef) != anyAllowed) __state.filter.SetAllow(tDef, anyAllowed);

            int visualParentIndent = nestLevel + (HasUsableIcon(tDef) ? 1 : 0);
            Rect baseRow = __instance.GetRect(__instance.lineHeight);
            bool baseAllowed = state.AllowsBase(tDef);
            bool previousBase = baseAllowed;
            DrawChildRow(__instance, baseRow, visualParentIndent + 1, "Base", ref baseAllowed,
                "Regular " + tDef.LabelCap + " without a variety.");
            if (baseAllowed != previousBase) state.SetBaseAllowed(tDef, baseAllowed);

            foreach (VarietyRecord variety in __state.varieties)
            {
                Rect row = __instance.GetRect(__instance.lineHeight);
                bool allowed = state.Allows(variety.id);
                bool previous = allowed;
                string tooltip = variety.TraitKey.NullOrEmpty() ? null : "Traits: " + NovelSeedUtility.TraitSummary(variety.traits);
                DrawChildRow(__instance, row, visualParentIndent + 1, variety.Label, ref allowed, tooltip);
                if (allowed != previous) state.SetAllowed(variety.id, allowed);
            }

            anyAllowed = state.AnyAllowed(tDef, ids);
            if (__state.filter.Allows(tDef) != anyAllowed) __state.filter.SetAllow(tDef, anyAllowed);
        }

        private static void DrawChildRow(Listing_TreeThingFilter listing, Rect row, int indentLevel, string label,
            ref bool allowed, string tooltip)
        {
            float checkboxX = listing.ColumnWidth - 26f;
            float labelX = indentLevel * listing.nestIndentWidth + 6f;
            Rect labelRect = new Rect(labelX, row.y, Mathf.Max(40f, checkboxX - labelX - 4f), row.height);
            Widgets.Label(labelRect, label);
            Widgets.Checkbox(new Vector2(checkboxX, row.y), ref allowed, listing.lineHeight);
            if (!tooltip.NullOrEmpty())
                TooltipHandler.TipRegion(new Rect(labelX, row.y, listing.ColumnWidth - labelX, row.height), tooltip);
        }

        private static bool HasUsableIcon(ThingDef def)
        {
            return def?.uiIcon != null && def.uiIcon != BaseContent.BadTex;
        }
    }
}
