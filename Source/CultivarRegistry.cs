using System;
using System.Collections.Generic;
using System.Linq;
using KnowledgeFramework;
using RimWorld;
using UnityEngine;
using Verse;

namespace HorticultureNovelSeeds
{
    public static class WildlifeRegistryIntegration
    {
        public static void Apply(HarmonyLib.Harmony harmony)
        {
            LongEventHandler.ExecuteWhenFinished(Register);
        }

        private static void Register()
        {
            try
            {
                Type registry = HarmonyLib.AccessTools.TypeByName("Herds.WildlifeMenuRegistry");
                System.Reflection.MethodInfo register = registry?.GetMethod("Register",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
                    null, new[] { typeof(string), typeof(string), typeof(string), typeof(int),
                        typeof(Func<bool>), typeof(Action) }, null);
                if (register == null) return;
                Action open = () =>
                {
                    MainButtonDef button = DefDatabase<MainButtonDef>.GetNamedSilentFail("HNS_CultivarRegistry");
                    if (button != null) Find.MainTabsRoot.SetCurrentTab(button, true);
                };
                Func<bool> visible = () => DefDatabase<MainButtonDef>.GetNamedSilentFail("HNS_CultivarRegistry")
                    ?.tabWindowClass == typeof(MainTabWindow_CultivarRegistry);
                register.Invoke(null, new object[]
                {
                    "horticulture.novel-seeds", "Horticulture", "Open the existing Novel Seeds Cultivar Registry.",
                    10, visible, open
                });
            }
            catch (Exception exception)
            {
                Log.Warning("[Horticulture - Novel Seeds] Wildlife Cultivar Registry integration was skipped: " + exception.Message);
            }
        }
    }

    public class MainTabWindow_CultivarRegistry : MainTabWindow
    {
        private enum RegistryPage { Plants, Cultivars, Knowledge, Compare }
        private enum DiscoveryFilter { All, Discovered, Undiscovered }
        private enum BalanceFilter { All, Balanced, Beneficial, Detrimental }

        private RegistryPage page;
        private DiscoveryFilter discoveryFilter;
        private BalanceFilter balanceFilter;
        private readonly KnowledgeMenuState knowledgeState = new KnowledgeMenuState();
        private readonly HashSet<string> comparisonIds = new HashSet<string>();
        private readonly Dictionary<string, RegistryAvailability> availability = new Dictionary<string, RegistryAvailability>();
        private string search = string.Empty;
        private string selectedPlantDefName;
        private string selectedCultivarId;
        private bool showArchived;
        private bool requireProduceEffect;
        private Vector2 listScroll;
        private Vector2 detailScroll;

        public override Vector2 RequestedTabSize => new Vector2(1180f, 720f);

        public static void OpenKnowledge(Pawn pawn)
        {
            MainButtonDef button = DefDatabase<MainButtonDef>.GetNamedSilentFail("HNS_CultivarRegistry");
            if (button == null) return;
            Find.MainTabsRoot.SetCurrentTab(button, true);
            if (button.TabWindow is MainTabWindow_CultivarRegistry registry)
            {
                registry.page = RegistryPage.Knowledge;
                registry.knowledgeState.scope = KnowledgeMenuScope.Colonist;
                registry.knowledgeState.selectedPawn = pawn;
            }
        }

        public static void OpenPlant(ThingDef plant)
        {
            MainButtonDef button = DefDatabase<MainButtonDef>.GetNamedSilentFail("HNS_CultivarRegistry");
            if (button == null || plant == null) return;
            Find.MainTabsRoot.SetCurrentTab(button, true);
            if (button.TabWindow is MainTabWindow_CultivarRegistry registry)
            {
                registry.page = RegistryPage.Plants;
                registry.selectedPlantDefName = plant.defName;
            }
        }

        public override void PreOpen()
        {
            base.PreOpen();
            RefreshAvailability();
            EnsureSelections();
        }

        public override void DoWindowContents(Rect inRect)
        {
            GameComponent_NovelSeeds component = GameComponent_NovelSeeds.Instance;
            if (component == null)
            {
                Widgets.Label(inRect, "HNS_RegistryUnavailable".Translate());
                return;
            }

            DrawHeader(new Rect(inRect.x, inRect.y, inRect.width, 70f), component);
            Rect content = new Rect(inRect.x, inRect.y + 78f, inRect.width, inRect.height - 78f);
            if (page == RegistryPage.Knowledge)
            {
                KnowledgeMenuUI.Draw(content, knowledgeState, KnowledgeModelFor, KnowledgeRankFor);
                return;
            }
            if (page == RegistryPage.Compare)
            {
                Widgets.DrawMenuSection(content);
                DrawComparisonTable(content.ContractedBy(14f), component);
                return;
            }

            Rect left = new Rect(content.x, content.y, 380f, content.height);
            Rect right = new Rect(left.xMax + 12f, content.y, content.width - left.width - 12f, content.height);
            Widgets.DrawMenuSection(left);
            Widgets.DrawMenuSection(right);
            if (page == RegistryPage.Plants)
            {
                DrawPlantList(left.ContractedBy(10f), component);
                DrawPlantDetail(right.ContractedBy(14f), component);
            }
            else
            {
                DrawCultivarList(left.ContractedBy(10f), component);
                DrawCultivarDetail(right.ContractedBy(14f), component);
            }
        }

        private void DrawHeader(Rect rect, GameComponent_NovelSeeds component)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(rect.x, rect.y, 320f, 34f), "HNS_RegistryTitle".Translate());
            Text.Font = GameFont.Small;
            int total = PlantDefs().Count;
            int discovered = PlantDefs().Count(def => IsDiscovered(def, component));
            Widgets.Label(new Rect(rect.x, rect.y + 36f, 500f, 28f),
                "Plants " + discovered + " / " + total + "   Cultivars " + component.AllVarieties.Count());

            const float tabWidth = 132f;
            const float tabGap = 8f;
            float start = rect.xMax - tabWidth * 4f - tabGap * 3f;
            if (Widgets.ButtonText(new Rect(start, rect.y + 12f, tabWidth, 40f), "Discovered Plants",
                    active: page != RegistryPage.Plants)) ChangePage(RegistryPage.Plants);
            if (Widgets.ButtonText(new Rect(start + tabWidth + tabGap, rect.y + 12f, tabWidth, 40f), "Cultivars",
                    active: page != RegistryPage.Cultivars)) ChangePage(RegistryPage.Cultivars);
            if (Widgets.ButtonText(new Rect(start + (tabWidth + tabGap) * 2f, rect.y + 12f, tabWidth, 40f), "Knowledge",
                    active: page != RegistryPage.Knowledge)) ChangePage(RegistryPage.Knowledge);
            bool enabled = CanCompare;
            GUI.enabled = enabled;
            if (Widgets.ButtonText(new Rect(start + (tabWidth + tabGap) * 3f, rect.y + 12f, tabWidth, 40f), "Compare",
                    active: page != RegistryPage.Compare)) ChangePage(RegistryPage.Compare);
            GUI.enabled = true;
            TooltipHandler.TipRegion(new Rect(start + (tabWidth + tabGap) * 3f, rect.y + 12f, tabWidth, 40f),
                enabled ? "Compare the selected cultivars side by side." : "Select at least two discovered cultivars to compare.");
        }

        private void ChangePage(RegistryPage value)
        {
            page = value;
            listScroll = Vector2.zero;
            detailScroll = Vector2.zero;
        }

        private void DrawPlantList(Rect rect, GameComponent_NovelSeeds component)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(rect.x, rect.y, rect.width, 32f), "Discovered Plants");
            Text.Font = GameFont.Small;
            DrawSearch(new Rect(rect.x, rect.y + 38f, rect.width, 30f));
            if (Widgets.ButtonText(new Rect(rect.x, rect.y + 76f, rect.width, 30f), "Show: " + discoveryFilter))
            {
                Find.WindowStack.Add(new FloatMenu(Enum.GetValues(typeof(DiscoveryFilter)).Cast<DiscoveryFilter>()
                    .Select(value => new FloatMenuOption(value.ToString(), () => discoveryFilter = value)).ToList()));
            }
            List<ThingDef> plants = PlantDefs().Where(def => MatchesPlantFilter(def, component)).ToList();
            Rect outer = new Rect(rect.x, rect.y + 114f, rect.width, rect.height - 114f);
            Rect view = new Rect(0f, 0f, outer.width - 16f, Mathf.Max(outer.height, plants.Count * 58f));
            Widgets.BeginScrollView(outer, ref listScroll, view);
            for (int i = 0; i < plants.Count; i++)
            {
                ThingDef plant = plants[i];
                bool discovered = IsDiscovered(plant, component);
                Rect row = new Rect(0f, i * 58f, view.width, 52f);
                if (plant.defName == selectedPlantDefName) Widgets.DrawHighlightSelected(row);
                else Widgets.DrawHighlightIfMouseover(row);
                if (discovered) Widgets.ThingIcon(new Rect(4f, row.y + 5f, 42f, 42f), plant);
                Widgets.Label(new Rect(54f, row.y + 5f, row.width - 60f, 24f),
                    discovered ? plant.LabelCap.ToString() : "Undiscovered plant");
                GUI.color = Color.gray;
                Widgets.Label(new Rect(54f, row.y + 27f, row.width - 60f, 22f), discovered
                    ? component.VarietiesFor(plant).Count() + " cultivars   " + ColonyKnowledgeRank(plant)
                    : "Learn about this species through plant work or seed discovery.");
                GUI.color = Color.white;
                if (Widgets.ButtonInvisible(row)) selectedPlantDefName = plant.defName;
            }
            Widgets.EndScrollView();
            if (plants.Count == 0) Widgets.Label(outer.ContractedBy(8f), "No plants match the current search and filter.");
        }

        private void DrawPlantDetail(Rect rect, GameComponent_NovelSeeds component)
        {
            ThingDef plant = DefDatabase<ThingDef>.GetNamedSilentFail(selectedPlantDefName);
            bool discovered = IsDiscovered(plant, component);
            if (plant == null || !discovered)
            {
                Widgets.Label(rect, plant == null ? "Select a plant to inspect it." :
                    "Undiscovered plant\n\nPerform plant work or discover a cultivar to reveal this species.");
                return;
            }
            KnowledgeRank rank = VisibleKnowledgeRank(plant);
            Rect view = new Rect(0f, 0f, rect.width - 16f, 620f);
            Widgets.BeginScrollView(rect, ref detailScroll, view);
            Widgets.ThingIcon(new Rect(0f, 0f, 72f, 72f), plant);
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(84f, 5f, view.width - 84f, 34f), plant.LabelCap);
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(84f, 42f, view.width - 84f, 28f), rank + " knowledge");
            float y = 92f;
            DrawSection(view, ref y, "Plant Knowledge");
            DrawRecord(view, ref y, "Colony rank", ColonyKnowledgeRank(plant).ToString());
            DrawRecord(view, ref y, "Known cultivars", component.VarietiesFor(plant).Count().ToString());
            if (rank >= KnowledgeRank.Adept)
            {
                DrawSection(view, ref y, "Growing");
                DrawRecord(view, ref y, "Grow time", plant.plant.growDays.ToString("0.##") + " days");
                DrawRecord(view, ref y, "Sow work", plant.plant.sowWork.ToString("0") + " work");
                DrawRecord(view, ref y, "Minimum fertility", plant.plant.fertilityMin.ToStringPercent());
            }
            if (rank >= KnowledgeRank.Expert)
            {
                DrawSection(view, ref y, "Harvest");
                DrawRecord(view, ref y, "Harvest yield", plant.plant.harvestYield.ToString("0.##"));
                DrawRecord(view, ref y, "Harvest product", plant.plant.harvestedThingDef?.LabelCap ?? "None");
                DrawRecord(view, ref y, "Lifespan", plant.plant.LimitedLifespan ? "Limited" : "Persistent");
            }
            if (rank >= KnowledgeRank.Master)
            {
                DrawSection(view, ref y, "Environment");
                DrawRecord(view, ref y, "Growth temperature", plant.plant.minGrowthTemperature.ToString("0.#") + " to " +
                    plant.plant.maxGrowthTemperature.ToString("0.#") + " C");
                DrawRecord(view, ref y, "Purpose", plant.plant.purpose.ToString());
            }
            Widgets.EndScrollView();
        }

        private void DrawCultivarList(Rect rect, GameComponent_NovelSeeds component)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(rect.x, rect.y, rect.width, 32f), "Cultivars");
            Text.Font = GameFont.Small;
            DrawSearch(new Rect(rect.x, rect.y + 38f, rect.width, 30f));
            float half = (rect.width - 8f) * 0.5f;
            if (Widgets.ButtonText(new Rect(rect.x, rect.y + 76f, half, 30f), "Balance: " + balanceFilter))
            {
                Find.WindowStack.Add(new FloatMenu(Enum.GetValues(typeof(BalanceFilter)).Cast<BalanceFilter>()
                    .Select(value => new FloatMenuOption(value.ToString(), () => balanceFilter = value)).ToList()));
            }
            Widgets.CheckboxLabeled(new Rect(rect.x + half + 8f, rect.y + 76f, half, 30f), "Archived", ref showArchived);
            Widgets.CheckboxLabeled(new Rect(rect.x, rect.y + 110f, rect.width, 28f), "Inherited produce effects", ref requireProduceEffect);
            List<VarietyRecord> varieties = component.AllVarieties.Where(MatchesCultivarFilter)
                .OrderByDescending(value => value.registryFavorite).ThenBy(value => value.cropDef?.label)
                .ThenBy(value => value.Label).ToList();
            Rect outer = new Rect(rect.x, rect.y + 144f, rect.width, rect.height - 144f);
            Rect view = new Rect(0f, 0f, outer.width - 16f, Mathf.Max(outer.height, varieties.Count * 64f));
            Widgets.BeginScrollView(outer, ref listScroll, view);
            for (int i = 0; i < varieties.Count; i++)
            {
                VarietyRecord variety = varieties[i];
                Rect row = new Rect(0f, i * 64f, view.width, 58f);
                if (variety.id == selectedCultivarId) Widgets.DrawHighlightSelected(row);
                else Widgets.DrawHighlightIfMouseover(row);
                Widgets.ThingIcon(new Rect(4f, row.y + 8f, 42f, 42f), variety.cropDef);
                bool chosen = comparisonIds.Contains(variety.id);
                Widgets.Checkbox(new Vector2(row.xMax - 28f, row.y + 18f), ref chosen);
                if (chosen) comparisonIds.Add(variety.id); else comparisonIds.Remove(variety.id);
                Widgets.Label(new Rect(54f, row.y + 5f, row.width - 92f, 24f),
                    (variety.registryFavorite ? "* " : string.Empty) + variety.Label);
                GUI.color = Color.gray;
                Widgets.Label(new Rect(54f, row.y + 29f, row.width - 92f, 22f),
                    variety.cropDef.LabelCap + "   Generation " + LineageDepth(variety));
                GUI.color = Color.white;
                Rect selectRect = new Rect(row.x, row.y, row.width - 38f, row.height);
                if (Widgets.ButtonInvisible(selectRect)) selectedCultivarId = variety.id;
            }
            Widgets.EndScrollView();
            if (varieties.Count == 0) Widgets.Label(outer.ContractedBy(8f), "HNS_RegistryNoMatches".Translate());
        }

        private void DrawCultivarDetail(Rect rect, GameComponent_NovelSeeds component)
        {
            VarietyRecord selected = component.GetVariety(selectedCultivarId);
            if (selected == null)
            {
                Widgets.Label(rect, "HNS_RegistrySelectVariety".Translate());
                return;
            }
            KnowledgeRank rank = VisibleCultivarRank(selected);
            Rect view = new Rect(0f, 0f, rect.width - 16f, Mathf.Max(rect.height, 760f));
            Widgets.BeginScrollView(rect, ref detailScroll, view);
            Widgets.ThingIcon(new Rect(0f, 0f, 72f, 72f), selected.cropDef);
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(84f, 5f, view.width - 84f, 34f), selected.Label);
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(84f, 42f, view.width - 84f, 28f), selected.cropDef.LabelCap + "   " + rank);
            float y = 92f;
            RegistryAvailability stock = AvailabilityFor(selected);
            DrawRecord(view, ref y, "Origin", selected.originKind.NullOrEmpty() ? "mutation" : selected.originKind);
            DrawRecord(view, ref y, "Generation", (selected.generation > 0 ? selected.generation : LineageDepth(selected)).ToString());
            DrawRecord(view, ref y, "Availability", stock.Plants + " plants, " + stock.Produce + " produce, " + stock.SeedPacks + " seed packs");
            if (!selected.FirstDiscoveredInfo.NullOrEmpty()) DrawRecord(view, ref y, "Discovered", selected.FirstDiscoveredInfo);

            float buttonWidth = (view.width - 24f) / 4f;
            if (Widgets.ButtonText(new Rect(0f, y + 4f, buttonWidth, 30f), "HNS_RenameVariety".Translate()))
                Find.WindowStack.Add(new Dialog_RenameVariety(selected));
            if (Widgets.ButtonText(new Rect(buttonWidth + 8f, y + 4f, buttonWidth, 30f),
                    selected.registryFavorite ? "HNS_RegistryUnfavorite".Translate() : "HNS_RegistryFavorite".Translate()))
                selected.registryFavorite = !selected.registryFavorite;
            if (Widgets.ButtonText(new Rect((buttonWidth + 8f) * 2f, y + 4f, buttonWidth, 30f),
                    selected.registryArchived ? "HNS_RegistryRestore".Translate() : "HNS_RegistryArchive".Translate()))
                selected.registryArchived = !selected.registryArchived;
            GUI.enabled = stock.Target != null;
            if (Widgets.ButtonText(new Rect((buttonWidth + 8f) * 3f, y + 4f, buttonWidth, 30f), "HNS_RegistryLocate".Translate()))
                CameraJumper.TryJumpAndSelect(stock.Target);
            GUI.enabled = true;
            y += 48f;
            if (Widgets.ButtonText(new Rect(0f, y, 150f, 30f), "HNS_Lineage".Translate()))
                Find.WindowStack.Add(new Dialog_VarietyLineage(selected.Label, selected.traits, selected.parentVarietyIds));
            y += 44f;

            DrawSection(view, ref y, "Traits");
            if (rank < KnowledgeRank.Adept) DrawDetailLine(view, ref y, "Advance plant knowledge to reveal cultivar traits.");
            else foreach (VarietyTraitDef trait in selected.traits.Where(value => value != null))
            {
                Rect row = new Rect(8f, y, view.width - 16f, Mathf.Max(30f, Text.CalcHeight(TraitColorUI.Label(trait), view.width - 24f) + 8f));
                Widgets.DrawHighlightIfMouseover(row);
                Widgets.Label(row.ContractedBy(4f), TraitColorUI.Label(trait));
                TooltipHandler.TipRegion(row, TraitColorUI.Tooltip(trait));
                y += row.height + 4f;
            }
            if (rank >= KnowledgeRank.Expert)
            {
                DrawSection(view, ref y, "Cultivar Modifiers");
                DrawRecord(view, ref y, "Yield", NovelSeedUtility.YieldFactor(selected.traits).ToStringPercent());
                DrawRecord(view, ref y, "Growth rate", NovelSeedUtility.GrowthRateFactor(selected.traits).ToStringPercent() + " of base");
                DrawRecord(view, ref y, "Sow time", ExpandedTraitUtility.SowWorkFactor(selected.traits).ToStringPercent() + " of base");
                DrawRecord(view, ref y, "Harvest time", ExpandedTraitUtility.HarvestWorkFactor(selected.traits).ToStringPercent() + " of base");
                DrawRecord(view, ref y, "Beauty", NovelSeedUtility.BeautyOffset(selected.traits).ToStringWithSign());
                DrawRecord(view, ref y, "Nutrition", NovelSeedUtility.ProduceNutritionFactor(selected.traits).ToStringPercent());
            }
            Widgets.EndScrollView();
        }

        internal static bool CanCompareCount(int count) => count >= 2;

        private bool CanCompare => CanCompareCount(comparisonIds.Count);

        private void DrawComparisonTable(Rect rect, GameComponent_NovelSeeds component)
        {
            List<VarietyRecord> selected = comparisonIds.Select(component.GetVariety).Where(value => value != null)
                .OrderBy(value => value.cropDef?.label).ThenBy(value => value.Label).ToList();
            if (selected.Count < 2)
            {
                Widgets.Label(rect, "Select at least two discovered cultivars on the Cultivars page.");
                return;
            }
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(rect.x, rect.y, rect.width, 34f), "Cultivar Comparison");
            Text.Font = GameFont.Small;
            float labelWidth = 150f;
            float columnWidth = Mathf.Max(150f, (rect.width - labelWidth - 16f) / selected.Count);
            Rect outer = new Rect(rect.x, rect.y + 42f, rect.width, rect.height - 42f);
            Rect view = new Rect(0f, 0f, Mathf.Max(outer.width - 16f, labelWidth + columnWidth * selected.Count), 570f);
            Widgets.BeginScrollView(outer, ref detailScroll, view);
            for (int i = 0; i < selected.Count; i++)
            {
                float x = labelWidth + i * columnWidth;
                Widgets.ThingIcon(new Rect(x + 8f, 4f, 40f, 40f), selected[i].cropDef);
                Widgets.Label(new Rect(x + 56f, 4f, columnWidth - 62f, 42f), selected[i].Label);
            }
            float y = 56f;
            KnowledgeStructuredComparisonSnapshot frameworkComparison = HorticultureKnowledgeAdapter.CompareCultivars(selected,
                knowledgeState.scope == KnowledgeMenuScope.Colony ? null : knowledgeState.selectedPawn,
                knowledgeState.scope == KnowledgeMenuScope.Colony);
            DrawCompareRow(view, ref y, "Parent plant", selected, value => value.cropDef.LabelCap);
            DrawCompareRow(view, ref y, "Generation", selected,
                value => (value.generation > 0 ? value.generation : LineageDepth(value)).ToString());
            DrawCompareRow(view, ref y, "Framework confidence", selected,
                value => FrameworkConfidence(frameworkComparison, selected, value));
            DrawCompareRow(view, ref y, "Traits", selected, value => VisibleKnowledgeRank(value.cropDef) >= KnowledgeRank.Adept
                ? TraitColorUI.Summary(value.traits) : "Undiscovered information");
            DrawCompareRow(view, ref y, "Yield", selected, value => VisibleKnowledgeRank(value.cropDef) >= KnowledgeRank.Expert
                ? NovelSeedUtility.YieldFactor(value.traits).ToStringPercent() : "Undiscovered information");
            DrawCompareRow(view, ref y, "Growth rate", selected, value => VisibleKnowledgeRank(value.cropDef) >= KnowledgeRank.Expert
                ? NovelSeedUtility.GrowthRateFactor(value.traits).ToStringPercent() + " of base" : "Undiscovered information");
            DrawCompareRow(view, ref y, "Sow time", selected, value => VisibleKnowledgeRank(value.cropDef) >= KnowledgeRank.Expert
                ? ExpandedTraitUtility.SowWorkFactor(value.traits).ToStringPercent() + " of base" : "Undiscovered information");
            DrawCompareRow(view, ref y, "Harvest time", selected, value => VisibleKnowledgeRank(value.cropDef) >= KnowledgeRank.Expert
                ? ExpandedTraitUtility.HarvestWorkFactor(value.traits).ToStringPercent() + " of base" : "Undiscovered information");
            DrawCompareRow(view, ref y, "Beauty", selected, value => VisibleKnowledgeRank(value.cropDef) >= KnowledgeRank.Expert
                ? NovelSeedUtility.BeautyOffset(value.traits).ToStringWithSign() : "Undiscovered information");
            DrawCompareRow(view, ref y, "Nutrition", selected, value => VisibleKnowledgeRank(value.cropDef) >= KnowledgeRank.Expert
                ? NovelSeedUtility.ProduceNutritionFactor(value.traits).ToStringPercent() : "Undiscovered information");
            DrawCompareRow(view, ref y, "Lineage", selected, value => VisibleKnowledgeRank(value.cropDef) >= KnowledgeRank.Master
                ? (value.parentVarietyIds.Count == 0 ? "Founder" : value.parentVarietyIds.Count + " recorded parents")
                : "Undiscovered information");
            DrawCompareRow(view, ref y, "Products / byproducts", selected, value => VisibleKnowledgeRank(value.cropDef) >= KnowledgeRank.Master
                ? ProductSummary(value) : "Undiscovered information");
            Widgets.EndScrollView();
        }

        private static void DrawCompareRow(Rect view, ref float y, string label, List<VarietyRecord> values,
            Func<VarietyRecord, string> valueFor)
        {
            float labelWidth = 150f;
            float columnWidth = (view.width - labelWidth) / values.Count;
            float height = values.Select(value => Text.CalcHeight(valueFor(value), columnWidth - 16f)).DefaultIfEmpty(28f).Max();
            height = Mathf.Max(36f, height + 12f);
            Widgets.DrawHighlightIfMouseover(new Rect(0f, y, view.width, height));
            Widgets.Label(new Rect(8f, y + 8f, labelWidth - 16f, height - 8f), label);
            for (int i = 0; i < values.Count; i++)
            {
                float x = labelWidth + i * columnWidth;
                Widgets.DrawLineVertical(x, y, height);
                Widgets.Label(new Rect(x + 8f, y + 8f, columnWidth - 16f, height - 8f), valueFor(values[i]));
            }
            Widgets.DrawLineHorizontal(0f, y + height, view.width);
            y += height;
        }

        private static string FrameworkConfidence(KnowledgeStructuredComparisonSnapshot snapshot,
            List<VarietyRecord> selected, VarietyRecord variety)
        {
            int index = selected.IndexOf(variety);
            if (snapshot?.rows == null || index < 0) return "No evidence";
            List<float> confidence = snapshot.rows.Where(row => row?.confidences != null && index < row.confidences.Count &&
                row.knownValues != null && index < row.knownValues.Count && row.knownValues[index])
                .Select(row => row.confidences[index]).ToList();
            return confidence.Count == 0 ? "No evidence" : confidence.Average().ToStringPercent();
        }

        private KnowledgeMenuModel KnowledgeModelFor(Pawn pawn, bool colony)
        {
            return HorticultureKnowledgeAdapter.Menu(pawn, colony);
        }

        private static KnowledgeRank KnowledgeRankFor(Pawn pawn)
        {
            return HorticultureKnowledgeAdapter.ExpertiseRank(pawn);
        }

        private KnowledgeRank VisibleKnowledgeRank(ThingDef plant)
        {
            if (plant == null) return KnowledgeRank.Novice;
            if (knowledgeState.scope == KnowledgeMenuScope.Colony) return ColonyKnowledgeRank(plant);
            return HorticultureKnowledgeAdapter.TierFor(plant, knowledgeState.selectedPawn, false);
        }

        private KnowledgeRank VisibleCultivarRank(VarietyRecord variety)
        {
            if (variety == null) return KnowledgeRank.Novice;
            KnowledgeRank cultivar = HorticultureKnowledgeAdapter.CultivarTierFor(variety,
                knowledgeState.scope == KnowledgeMenuScope.Colony ? null : knowledgeState.selectedPawn,
                knowledgeState.scope == KnowledgeMenuScope.Colony);
            return (KnowledgeRank)Math.Max((int)VisibleKnowledgeRank(variety.cropDef), (int)cultivar);
        }

        private static KnowledgeRank ColonyKnowledgeRank(ThingDef plant)
        {
            return HorticultureKnowledgeAdapter.TierFor(plant, null, true);
        }

        private void DrawSearch(Rect rect)
        {
            Widgets.Label(new Rect(rect.x, rect.y + 3f, 52f, 24f), "Search");
            search = Widgets.TextField(new Rect(rect.x + 56f, rect.y, rect.width - 92f, 30f), search ?? string.Empty);
            if (!search.NullOrEmpty() && Widgets.ButtonText(new Rect(rect.xMax - 30f, rect.y, 30f, 30f), "X")) search = string.Empty;
        }

        private List<ThingDef> PlantDefs() => DefDatabase<ThingDef>.AllDefsListForReading.Where(NovelSeedUtility.IsGrowableCrop)
            .OrderBy(def => def.label).ToList();

        private static bool IsDiscovered(ThingDef plant, GameComponent_NovelSeeds component) => plant != null &&
            (component.VarietiesFor(plant).Any() || HorticultureKnowledgeAdapter.ColonyKnowledge(plant) > 0f ||
                HorticultureKnowledgeAdapter.StageOrder(HorticultureKnowledgeAdapter.StageFor(plant, null, true)) > 0);

        private bool MatchesPlantFilter(ThingDef plant, GameComponent_NovelSeeds component)
        {
            bool discovered = IsDiscovered(plant, component);
            if (discoveryFilter == DiscoveryFilter.Discovered && !discovered) return false;
            if (discoveryFilter == DiscoveryFilter.Undiscovered && discovered) return false;
            return search.NullOrEmpty() || (discovered ? plant.LabelCap.ToString() : "Undiscovered plant")
                .IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private bool MatchesCultivarFilter(VarietyRecord variety)
        {
            if (variety?.cropDef == null || (!showArchived && variety.registryArchived)) return false;
            if (!search.NullOrEmpty() && (variety.Label + " " + variety.cropDef.label + " " + NovelSeedUtility.TraitSummary(variety.traits))
                .IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0) return false;
            if (requireProduceEffect && !variety.traits.Any(trait =>
                    ProduceTraitEffectUtility.Summary(TraitConfigUtility.Root(trait), HorticultureNovelSeedsMod.Settings) != "No Effect")) return false;
            float balance = NovelSeedUtility.TraitBalanceScore(variety.traits);
            switch (balanceFilter)
            {
                case BalanceFilter.Balanced: return Mathf.Abs(balance) <= (HorticultureNovelSeedsMod.Settings?.allowedTraitImbalance ?? 1);
                case BalanceFilter.Beneficial: return balance > 0f;
                case BalanceFilter.Detrimental: return balance < 0f;
                default: return true;
            }
        }

        private void EnsureSelections()
        {
            GameComponent_NovelSeeds component = GameComponent_NovelSeeds.Instance;
            if (component == null) return;
            List<Pawn> colonists = Find.Maps.SelectMany(map => map.mapPawns.FreeColonists)
                .Where(pawn => pawn?.Faction?.def?.isPlayer == true && !pawn.Dead).Distinct().OrderBy(pawn => pawn.LabelShort).ToList();
            if (knowledgeState.selectedPawn == null || !colonists.Contains(knowledgeState.selectedPawn))
                knowledgeState.selectedPawn = colonists.FirstOrDefault();
            if (selectedPlantDefName.NullOrEmpty() || DefDatabase<ThingDef>.GetNamedSilentFail(selectedPlantDefName) == null)
                selectedPlantDefName = PlantDefs().FirstOrDefault(def => IsDiscovered(def, component))?.defName ?? PlantDefs().FirstOrDefault()?.defName;
            if (component.GetVariety(selectedCultivarId) == null)
                selectedCultivarId = component.AllVarieties.OrderBy(value => value.cropDef?.label).ThenBy(value => value.Label).FirstOrDefault()?.id;
            comparisonIds.RemoveWhere(id => component.GetVariety(id) == null);
        }

        private void RefreshAvailability()
        {
            availability.Clear();
            foreach (VarietyRecord variety in GameComponent_NovelSeeds.Instance?.AllVarieties ?? Enumerable.Empty<VarietyRecord>())
                availability[variety.id] = new RegistryAvailability();
            foreach (Map map in Find.Maps)
            foreach (Thing thing in map.listerThings.AllThings)
            {
                CompPlantVariety plant = thing.TryGetComp<CompPlantVariety>();
                if (plant?.VarietyId != null && availability.TryGetValue(plant.VarietyId, out RegistryAvailability plantStock))
                {
                    plantStock.Plants++;
                    if (plantStock.Target == null) plantStock.Target = thing;
                }
                CompNovelProduceAppearance produce = thing.TryGetComp<CompNovelProduceAppearance>();
                if (produce != null)
                foreach (string id in produce.SourceVarietyIds.Where(id => !id.NullOrEmpty()).Distinct())
                {
                    if (!availability.TryGetValue(id, out RegistryAvailability stock)) continue;
                    stock.Produce += thing.stackCount;
                    if (stock.Target == null) stock.Target = thing;
                }
                CompNovelSeedPack pack = thing.TryGetComp<CompNovelSeedPack>();
                if (pack?.Valid == true)
                {
                    VarietyRecord match = GameComponent_NovelSeeds.Instance.FindMatchingVariety(pack.CropDef, pack.Traits);
                    if (match != null && availability.TryGetValue(match.id, out RegistryAvailability stock))
                    {
                        stock.SeedPacks += thing.stackCount;
                        if (stock.Target == null) stock.Target = thing;
                    }
                }
            }
        }

        private RegistryAvailability AvailabilityFor(VarietyRecord variety) => variety != null &&
            availability.TryGetValue(variety.id, out RegistryAvailability value) ? value : new RegistryAvailability();

        private static string ProductSummary(VarietyRecord variety)
        {
            List<string> values = new List<string>();
            if (variety.cropDef?.plant?.harvestedThingDef != null) values.Add(variety.cropDef.plant.harvestedThingDef.LabelCap);
            values.AddRange(variety.traits.Where(trait => trait?.byproductDef != null).Select(trait => trait.byproductDef.LabelCap.ToString()));
            return values.Count == 0 ? "None" : string.Join(", ", values.Distinct());
        }

        private static void DrawSection(Rect rect, ref float y, string text)
        {
            y += 8f;
            Widgets.DrawLineHorizontal(rect.x, y, rect.width);
            y += 12f;
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(rect.x, y, rect.width, 30f), text);
            Text.Font = GameFont.Small;
            y += 36f;
        }

        private static void DrawRecord(Rect rect, ref float y, string label, string value)
        {
            Widgets.Label(new Rect(rect.x + 8f, y, rect.width * 0.45f, 28f), label);
            Text.Anchor = TextAnchor.UpperRight;
            Widgets.Label(new Rect(rect.x + rect.width * 0.45f, y, rect.width * 0.53f - 8f, 28f), value);
            Text.Anchor = TextAnchor.UpperLeft;
            y += 30f;
        }

        private static void DrawDetailLine(Rect rect, ref float y, string text)
        {
            float height = Mathf.Max(24f, Text.CalcHeight(text, rect.width));
            Widgets.Label(new Rect(rect.x, y, rect.width, height), text);
            y += height + 4f;
        }

        private static int LineageDepth(VarietyRecord variety) => LineageDepth(variety, new HashSet<string>());

        private static int LineageDepth(VarietyRecord variety, HashSet<string> path)
        {
            if (variety == null || variety.parentVarietyIds == null || variety.parentVarietyIds.Count == 0 ||
                !path.Add(variety.id)) return 0;
            int depth = 1 + variety.parentVarietyIds.Select(id =>
                LineageDepth(GameComponent_NovelSeeds.Instance?.GetVariety(id), path)).DefaultIfEmpty(0).Max();
            path.Remove(variety.id);
            return depth;
        }

        private sealed class RegistryAvailability
        {
            public int Plants;
            public int Produce;
            public int SeedPacks;
            public Thing Target;
        }
    }
}
