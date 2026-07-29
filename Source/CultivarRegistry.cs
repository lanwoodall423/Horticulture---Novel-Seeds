using System;
using System.Collections.Generic;
using System.Linq;
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
                System.Type registry = HarmonyLib.AccessTools.TypeByName("Herds.WildlifeMenuRegistry");
                System.Reflection.MethodInfo register = registry?.GetMethod("Register",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
                    null, new[] { typeof(string), typeof(string), typeof(string), typeof(int),
                        typeof(System.Func<bool>), typeof(System.Action) }, null);
                if (register == null) return;
                System.Action open = () =>
                {
                    MainButtonDef cultivarRegistry =
                        DefDatabase<MainButtonDef>.GetNamedSilentFail("HNS_CultivarRegistry");
                    if (cultivarRegistry != null) Find.MainTabsRoot.SetCurrentTab(cultivarRegistry, true);
                };
                System.Func<bool> visible = () =>
                {
                    MainButtonDef cultivarRegistry =
                        DefDatabase<MainButtonDef>.GetNamedSilentFail("HNS_CultivarRegistry");
                    return cultivarRegistry?.tabWindowClass == typeof(MainTabWindow_CultivarRegistry);
                };
                register.Invoke(null, new object[]
                {
                    "horticulture.novel-seeds",
                    "Horticulture",
                    "Open the existing Novel Seeds Cultivar Registry.",
                    10,
                    visible,
                    open
                });
            }
            catch (System.Exception exception)
            {
                Log.Warning("[Horticulture - Novel Seeds] Wildlife Cultivar Registry integration was skipped: " + exception.Message);
            }
        }
    }

    public class MainTabWindow_CultivarRegistry : MainTabWindow
    {
        private enum BalanceFilter { All, Balanced, Beneficial, Detrimental }
        private enum RightPage { Compare, Programs }

        private const float Gap = 10f;
        private const float LeftWidth = 330f;
        private const float DetailWidth = 430f;
        private string search = string.Empty;
        private bool showArchived;
        private bool requireProduceEffect;
        private int minimumGeneration;
        private VarietyTraitDef traitFilter;
        private BalanceFilter balanceFilter;
        private RightPage rightPage;
        private VarietyRecord selected;
        private VarietyRecord comparison;
        private Vector2 catalogScroll;
        private Vector2 traitScroll;
        private Vector2 programScroll;
        private readonly Dictionary<string, RegistryAvailability> availability = new Dictionary<string, RegistryAvailability>();

        public override Vector2 RequestedTabSize => new Vector2(1240f, 720f);

        public override void PreOpen()
        {
            base.PreOpen();
            RefreshAvailability();
            EnsureSelection();
        }

        public override void DoWindowContents(Rect inRect)
        {
            GameComponent_NovelSeeds component = GameComponent_NovelSeeds.Instance;
            if (component == null)
            {
                Widgets.Label(inRect, "HNS_RegistryUnavailable".Translate());
                return;
            }

            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width - 180f, 34f), "HNS_RegistryTitle".Translate());
            Text.Font = GameFont.Small;
            if (Widgets.ButtonText(new Rect(inRect.xMax - 132f, inRect.y, 132f, 30f), "HNS_RegistryRefresh".Translate()))
                RefreshAvailability();

            Rect body = new Rect(inRect.x, inRect.y + 44f, inRect.width, inRect.height - 44f);
            float rightWidth = body.width - LeftWidth - DetailWidth - Gap * 2f;
            Rect catalogRect = new Rect(body.x, body.y, LeftWidth, body.height);
            Rect detailRect = new Rect(catalogRect.xMax + Gap, body.y, DetailWidth, body.height);
            Rect rightRect = new Rect(detailRect.xMax + Gap, body.y, rightWidth, body.height);
            DrawCatalog(catalogRect, component);
            DrawDetails(detailRect, component);
            DrawRightPanel(rightRect, component);
        }

        private void DrawCatalog(Rect rect, GameComponent_NovelSeeds component)
        {
            Widgets.DrawMenuSection(rect);
            Rect inner = rect.ContractedBy(10f);
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inner.x, inner.y, inner.width, 30f), "HNS_RegistryCatalog".Translate());
            Text.Font = GameFont.Small;
            search = Widgets.TextField(new Rect(inner.x, inner.y + 36f, inner.width, 30f), search);

            float half = (inner.width - 6f) * 0.5f;
            if (Widgets.ButtonText(new Rect(inner.x, inner.y + 72f, half, 28f), "HNS_RegistryBalanceFilter".Translate(BalanceFilterLabel())))
            {
                List<FloatMenuOption> options = Enum.GetValues(typeof(BalanceFilter)).Cast<BalanceFilter>()
                    .Select(value => new FloatMenuOption(BalanceFilterLabel(value), delegate { balanceFilter = value; })).ToList();
                Find.WindowStack.Add(new FloatMenu(options));
            }
            Widgets.CheckboxLabeled(new Rect(inner.x + half + 6f, inner.y + 72f, half, 28f), "HNS_RegistryArchived".Translate(), ref showArchived);
            if (Widgets.ButtonText(new Rect(inner.x, inner.y + 106f, half, 28f),
                traitFilter == null ? "HNS_RegistryAnyTrait".Translate() : traitFilter.LabelCap))
            {
                List<FloatMenuOption> traitOptions = new List<FloatMenuOption>
                {
                    new FloatMenuOption("HNS_RegistryAnyTrait".Translate(), delegate { traitFilter = null; })
                };
                traitOptions.AddRange(TraitConfigUtility.TopLevelTraits().Select(trait =>
                    new FloatMenuOption(trait.LabelCap, delegate { traitFilter = trait; })));
                Find.WindowStack.Add(new FloatMenu(traitOptions));
            }
            if (Widgets.ButtonText(new Rect(inner.x + half + 6f, inner.y + 106f, 76f, 28f), "HNS_RegistryGenerationShort".Translate(minimumGeneration)))
            {
                Find.WindowStack.Add(new FloatMenu(Enumerable.Range(0, 5)
                    .Select(value => new FloatMenuOption("HNS_RegistryGenerationMinimum".Translate(value), delegate { minimumGeneration = value; })).ToList()));
            }
            Widgets.CheckboxLabeled(new Rect(inner.x + half + 88f, inner.y + 106f, half - 88f, 28f), "HNS_RegistryProduceShort".Translate(), ref requireProduceEffect);
            TooltipHandler.TipRegion(new Rect(inner.x + half + 88f, inner.y + 106f, half - 88f, 28f), "HNS_RegistryProduceFilterTip".Translate());

            List<VarietyRecord> varieties = component.AllVarieties
                .Where(MatchesCatalogFilter)
                .OrderByDescending(variety => variety.registryFavorite)
                .ThenBy(variety => variety.cropDef?.label)
                .ThenBy(variety => variety.Label).ToList();
            Rect outRect = new Rect(inner.x, inner.y + 142f, inner.width, inner.height - 142f);
            float viewWidth = outRect.width - 16f;
            Rect viewRect = new Rect(0f, 0f, viewWidth, Mathf.Max(outRect.height, varieties.Count * 58f));
            Widgets.BeginScrollView(outRect, ref catalogScroll, viewRect);
            if (varieties.Count == 0)
            {
                Color old = GUI.color;
                GUI.color = Color.gray;
                Widgets.Label(new Rect(8f, 10f, viewRect.width - 16f, 50f), "HNS_RegistryNoMatches".Translate());
                GUI.color = old;
            }
            for (int i = 0; i < varieties.Count; i++)
            {
                VarietyRecord variety = varieties[i];
                Rect row = new Rect(0f, i * 58f, viewRect.width, 54f);
                if (selected == variety) Widgets.DrawHighlightSelected(row);
                else Widgets.DrawHighlightIfMouseover(row);
                Widgets.DefIcon(new Rect(row.x + 4f, row.y + 7f, 40f, 40f), variety.cropDef);
                string favorite = variety.registryFavorite ? "* " : string.Empty;
                Widgets.Label(new Rect(row.x + 50f, row.y + 5f, row.width - 54f, 24f), favorite + variety.Label);
                Color old = GUI.color;
                GUI.color = Color.gray;
                string status = variety.cropDef.LabelCap + "  |  " + NovelSeedUtility.TraitBalanceSummary(variety.traits).Replace("Trait Balance: ", "");
                Widgets.Label(new Rect(row.x + 50f, row.y + 28f, row.width - 54f, 22f), status);
                GUI.color = old;
                if (Widgets.ButtonInvisible(row))
                {
                    selected = variety;
                    if (comparison?.cropDef != selected.cropDef || comparison == selected) comparison = null;
                }
            }
            Widgets.EndScrollView();
        }

        private void DrawDetails(Rect rect, GameComponent_NovelSeeds component)
        {
            Widgets.DrawMenuSection(rect);
            Rect inner = rect.ContractedBy(12f);
            if (selected == null)
            {
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(inner, "HNS_RegistrySelectVariety".Translate());
                Text.Anchor = TextAnchor.UpperLeft;
                return;
            }

            Widgets.DefIcon(new Rect(inner.x, inner.y, 72f, 72f), selected.cropDef);
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inner.x + 82f, inner.y, inner.width - 82f, 32f), selected.Label);
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(inner.x + 82f, inner.y + 34f, inner.width - 82f, 24f), selected.cropDef.LabelCap);
            string score = "HNS_RegistryScore".Translate(RegistryScore(selected));
            Widgets.Label(new Rect(inner.x + 82f, inner.y + 56f, inner.width - 82f, 24f), score);
            TooltipHandler.TipRegion(new Rect(inner.x + 82f, inner.y + 56f, inner.width - 82f, 24f), "HNS_RegistryScoreTip".Translate());

            float y = inner.y + 84f;
            DrawDetailLine(inner, ref y, NovelSeedUtility.TraitBalanceSummary(selected.traits));
            DrawDetailLine(inner, ref y, "HNS_RegistryGeneration".Translate(LineageDepth(selected)));
            if (!selected.FirstDiscoveredInfo.NullOrEmpty()) DrawDetailLine(inner, ref y, "HNS_FirstDiscoveredHeader".Translate() + ": " + selected.FirstDiscoveredInfo);
            RegistryAvailability stock = AvailabilityFor(selected);
            DrawDetailLine(inner, ref y, "HNS_RegistryAvailability".Translate(stock.Plants, stock.Produce, stock.SeedPacks));

            float buttonWidth = (inner.width - 12f) / 3f;
            if (Widgets.ButtonText(new Rect(inner.x, y + 4f, buttonWidth, 28f), "HNS_RenameVariety".Translate()))
                Find.WindowStack.Add(new Dialog_RenameVariety(selected));
            if (Widgets.ButtonText(new Rect(inner.x + buttonWidth + 6f, y + 4f, buttonWidth, 28f),
                selected.registryFavorite ? "HNS_RegistryUnfavorite".Translate() : "HNS_RegistryFavorite".Translate()))
                selected.registryFavorite = !selected.registryFavorite;
            if (Widgets.ButtonText(new Rect(inner.x + (buttonWidth + 6f) * 2f, y + 4f, buttonWidth, 28f),
                selected.registryArchived ? "HNS_RegistryRestore".Translate() : "HNS_RegistryArchive".Translate()))
                selected.registryArchived = !selected.registryArchived;
            y += 40f;
            if (Widgets.ButtonText(new Rect(inner.x, y, buttonWidth, 28f), "HNS_Lineage".Translate()))
                Find.WindowStack.Add(new Dialog_VarietyLineage(selected.Label, selected.traits, selected.parentVarietyIds));
            GUI.enabled = stock.Target != null;
            if (Widgets.ButtonText(new Rect(inner.x + buttonWidth + 6f, y, buttonWidth, 28f), "HNS_RegistryLocate".Translate()))
                CameraJumper.TryJumpAndSelect(stock.Target);
            GUI.enabled = true;
            if (Widgets.ButtonText(new Rect(inner.x + (buttonWidth + 6f) * 2f, y, buttonWidth, 28f), "HNS_RegistryCompare".Translate()))
            {
                rightPage = RightPage.Compare;
                OpenComparisonMenu(component);
            }
            y += 42f;

            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inner.x, y, inner.width, 30f), "HNS_Traits".Translate());
            Text.Font = GameFont.Small;
            y += 34f;
            Rect traitOut = new Rect(inner.x, y, inner.width, inner.yMax - y);
            float viewWidth = traitOut.width - 16f;
            List<float> heights = selected.traits.Select(trait => Mathf.Max(34f, Text.CalcHeight(trait.LabelCap, viewWidth - 12f) + 12f)).ToList();
            Rect traitView = new Rect(0f, 0f, viewWidth, Mathf.Max(traitOut.height, heights.Sum()));
            Widgets.BeginScrollView(traitOut, ref traitScroll, traitView);
            float traitY = 0f;
            for (int i = 0; i < selected.traits.Count; i++)
            {
                VarietyTraitDef trait = selected.traits[i];
                Rect row = new Rect(0f, traitY, traitView.width, heights[i] - 3f);
                Widgets.DrawHighlightIfMouseover(row);
                Widgets.Label(new Rect(row.x + 6f, row.y + 5f, row.width - 12f, row.height - 6f), trait.LabelCap);
                TooltipHandler.TipRegion(row, trait.LabelCap + "\n\n" + trait.description);
                traitY += heights[i];
            }
            Widgets.EndScrollView();
        }

        private void DrawRightPanel(Rect rect, GameComponent_NovelSeeds component)
        {
            Rect body = new Rect(rect.x, rect.y + 30f, rect.width, rect.height - 30f);
            Widgets.DrawMenuSection(body);
            TabDrawer.DrawTabs(body, new List<TabRecord>
            {
                new TabRecord("HNS_RegistryCompare".Translate(), delegate { rightPage = RightPage.Compare; }, rightPage == RightPage.Compare),
                new TabRecord("HNS_RegistryPrograms".Translate(), delegate { rightPage = RightPage.Programs; }, rightPage == RightPage.Programs)
            });
            Rect inner = body.ContractedBy(12f);
            if (rightPage == RightPage.Compare) DrawComparison(inner, component);
            else DrawPrograms(inner, component);
        }

        private void DrawComparison(Rect rect, GameComponent_NovelSeeds component)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(rect.x, rect.y, rect.width - 124f, 30f), "HNS_RegistryComparison".Translate());
            Text.Font = GameFont.Small;
            if (Widgets.ButtonText(new Rect(rect.xMax - 120f, rect.y, 120f, 28f), comparison == null ? "HNS_RegistryChoose".Translate() : "HNS_RegistryChange".Translate()))
                OpenComparisonMenu(component);
            if (selected == null || comparison == null)
            {
                Color old = GUI.color;
                GUI.color = Color.gray;
                Widgets.Label(new Rect(rect.x, rect.y + 46f, rect.width, 70f), "HNS_RegistryComparisonPrompt".Translate());
                GUI.color = old;
                return;
            }
            float y = rect.y + 42f;
            Widgets.DefIcon(new Rect(rect.x, y, 54f, 54f), comparison.cropDef);
            Widgets.Label(new Rect(rect.x + 64f, y, rect.width - 64f, 26f), comparison.Label);
            Widgets.Label(new Rect(rect.x + 64f, y + 27f, rect.width - 64f, 24f), NovelSeedUtility.TraitBalanceSummary(comparison.traits));
            y += 68f;
            List<VarietyTraitDef> selectedTraits = selected.traits.Where(trait => trait != null).Distinct().ToList();
            List<VarietyTraitDef> comparisonTraits = comparison.traits.Where(trait => trait != null).Distinct().ToList();
            DrawComparisonGroup(rect, ref y, "HNS_RegistrySharedTraits".Translate().ToString(), selectedTraits.Intersect(comparisonTraits));
            DrawComparisonGroup(rect, ref y, "HNS_RegistryOnlySelected".Translate(selected.Label).ToString(), selectedTraits.Except(comparisonTraits));
            DrawComparisonGroup(rect, ref y, "HNS_RegistryOnlyCompared".Translate(comparison.Label).ToString(), comparisonTraits.Except(selectedTraits));
            List<string> produceEffects = comparison.traits
                .Select(trait => ProduceTraitEffectUtility.Summary(TraitConfigUtility.Root(trait), HorticultureNovelSeedsMod.Settings))
                .Where(line => !line.NullOrEmpty() && line != "No Effect").Distinct().ToList();
            if (produceEffects.Count > 0)
            {
                Text.Font = GameFont.Medium;
                Widgets.Label(new Rect(rect.x, y, rect.width, 28f), "HNS_RegistryProduceEffects".Translate());
                Text.Font = GameFont.Small;
                y += 30f;
                foreach (string line in produceEffects.Take(5)) DrawDetailLine(rect, ref y, line);
            }
        }

        private void DrawPrograms(Rect rect, GameComponent_NovelSeeds component)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(rect.x, rect.y, rect.width - 126f, 30f), "HNS_RegistryPrograms".Translate());
            Text.Font = GameFont.Small;
            if (Widgets.ButtonText(new Rect(rect.xMax - 122f, rect.y, 122f, 28f), "HNS_RegistryNewProgram".Translate()))
                Find.WindowStack.Add(new Dialog_CreateBreedingProgram());
            List<BreedingProgramRecord> programs = component.BreedingPrograms.Where(program => program != null).ToList();
            Rect outRect = new Rect(rect.x, rect.y + 40f, rect.width, rect.height - 40f);
            float viewWidth = outRect.width - 16f;
            float viewHeight = Mathf.Max(outRect.height, programs.Count * 154f);
            Rect viewRect = new Rect(0f, 0f, viewWidth, viewHeight);
            Widgets.BeginScrollView(outRect, ref programScroll, viewRect);
            for (int i = 0; i < programs.Count; i++)
            {
                BreedingProgramRecord program = programs[i];
                Rect row = new Rect(0f, i * 154f, viewRect.width, 146f);
                Widgets.DrawBoxSolid(row, new Color(0.16f, 0.17f, 0.18f));
                Widgets.CheckboxLabeled(new Rect(row.x + 8f, row.y + 7f, row.width - 82f, 24f), program.name, ref program.active);
                if (Widgets.ButtonText(new Rect(row.xMax - 68f, row.y + 5f, 60f, 26f), "Delete".Translate()))
                    Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("HNS_RegistryDeleteProgramConfirm".Translate(program.name),
                        delegate { component.RemoveBreedingProgram(program); }, true));
                Widgets.Label(new Rect(row.x + 8f, row.y + 36f, row.width - 16f, 22f), program.cropDef.LabelCap);
                Color old = GUI.color;
                GUI.color = Color.gray;
                Widgets.Label(new Rect(row.x + 8f, row.y + 60f, row.width - 16f, 38f), program.DesiredTraitSummary);
                GUI.color = old;
                VarietyRecord best = component.CandidateVarieties(program).FirstOrDefault();
                string candidate = best == null ? "HNS_RegistryNoCandidate".Translate().ToString()
                    : "HNS_RegistryBestCandidate".Translate(best.Label, program.MatchCount(best), program.desiredTraitRootDefNames.Count);
                Widgets.Label(new Rect(row.x + 8f, row.y + 103f, row.width - 96f, 34f), candidate);
                if (best != null && Widgets.ButtonText(new Rect(row.xMax - 82f, row.y + 105f, 74f, 28f), "HNS_RegistryView".Translate()))
                {
                    selected = best;
                    rightPage = RightPage.Compare;
                }
            }
            if (programs.Count == 0) Widgets.Label(new Rect(8f, 8f, viewRect.width - 16f, 60f), "HNS_RegistryNoPrograms".Translate());
            Widgets.EndScrollView();
        }

        private void OpenComparisonMenu(GameComponent_NovelSeeds component)
        {
            if (selected == null) return;
            List<FloatMenuOption> options = component.VarietiesFor(selected.cropDef).Where(variety => variety != selected)
                .OrderBy(variety => variety.Label).Select(variety => new FloatMenuOption(variety.Label, delegate { comparison = variety; })).ToList();
            if (options.Count == 0) options.Add(new FloatMenuOption("HNS_RegistryNoComparison".Translate(), null));
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private bool MatchesCatalogFilter(VarietyRecord variety)
        {
            if (variety == null || variety.cropDef == null || (!showArchived && variety.registryArchived)) return false;
            if (!search.NullOrEmpty())
            {
                string text = variety.Label + " " + variety.cropDef.label + " " + NovelSeedUtility.TraitSummary(variety.traits) + " " + variety.FirstDiscoveredInfo;
                if (text.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0) return false;
            }
            if (traitFilter != null && !(variety.traits ?? new List<VarietyTraitDef>())
                .Any(trait => TraitConfigUtility.Root(trait) == traitFilter)) return false;
            if (LineageDepth(variety) < minimumGeneration) return false;
            if (requireProduceEffect && !(variety.traits ?? new List<VarietyTraitDef>())
                .Any(trait => ProduceTraitEffectUtility.Summary(TraitConfigUtility.Root(trait), HorticultureNovelSeedsMod.Settings) != "No Effect")) return false;
            float balance = NovelSeedUtility.TraitBalanceScore(variety.traits);
            switch (balanceFilter)
            {
                case BalanceFilter.Balanced: return Mathf.Abs(balance) <= (HorticultureNovelSeedsMod.Settings?.allowedTraitImbalance ?? 1);
                case BalanceFilter.Beneficial: return balance > 0f;
                case BalanceFilter.Detrimental: return balance < 0f;
                default: return true;
            }
        }

        private void EnsureSelection()
        {
            if (selected == null || !GameComponent_NovelSeeds.Instance.AllVarieties.Contains(selected))
                selected = GameComponent_NovelSeeds.Instance.AllVarieties.OrderBy(variety => variety.cropDef?.label).ThenBy(variety => variety.Label).FirstOrDefault();
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
                {
                    foreach (string id in produce.SourceVarietyIds.Where(id => !id.NullOrEmpty()).Distinct())
                    {
                        if (!availability.TryGetValue(id, out RegistryAvailability produceStock)) continue;
                        produceStock.Produce += thing.stackCount;
                        if (produceStock.Target == null) produceStock.Target = thing;
                    }
                }
                CompNovelSeedPack pack = thing.TryGetComp<CompNovelSeedPack>();
                if (pack?.Valid == true)
                {
                    VarietyRecord match = GameComponent_NovelSeeds.Instance.FindMatchingVariety(pack.CropDef, pack.Traits);
                    if (match != null && availability.TryGetValue(match.id, out RegistryAvailability seedStock))
                    {
                        seedStock.SeedPacks += thing.stackCount;
                        if (seedStock.Target == null) seedStock.Target = thing;
                    }
                }
            }
        }

        private RegistryAvailability AvailabilityFor(VarietyRecord variety)
        {
            return variety != null && availability.TryGetValue(variety.id, out RegistryAvailability result) ? result : new RegistryAvailability();
        }

        private static void DrawDetailLine(Rect rect, ref float y, string text)
        {
            float height = Mathf.Max(24f, Text.CalcHeight(text, rect.width));
            Widgets.Label(new Rect(rect.x, y, rect.width, height), text);
            y += height + 3f;
        }

        private static void DrawComparisonGroup(Rect rect, ref float y, string heading, IEnumerable<VarietyTraitDef> traits)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(rect.x, y, rect.width, 28f), heading);
            Text.Font = GameFont.Small;
            y += 29f;
            string value = string.Join(", ", traits.Select(trait => trait.LabelCap.ToString()).ToArray());
            DrawDetailLine(rect, ref y, value.NullOrEmpty() ? "HNS_RegistryNone".Translate().ToString() : value);
            y += 5f;
        }

        private string BalanceFilterLabel() => BalanceFilterLabel(balanceFilter);
        private static string BalanceFilterLabel(BalanceFilter filter)
        {
            switch (filter)
            {
                case BalanceFilter.Balanced: return "HNS_RegistryBalanced".Translate();
                case BalanceFilter.Beneficial: return "HNS_RegistryBeneficial".Translate();
                case BalanceFilter.Detrimental: return "HNS_RegistryDetrimental".Translate();
                default: return "HNS_RegistryAll".Translate();
            }
        }

        private static int RegistryScore(VarietyRecord variety)
        {
            int traitValue = Mathf.Min(25, (variety.traits?.Count ?? 0) * 5);
            int lineageValue = Mathf.Min(15, LineageDepth(variety) * 3);
            int balancePenalty = Mathf.Min(20, Mathf.RoundToInt(Mathf.Abs(NovelSeedUtility.TraitBalanceScore(variety.traits)) * 5f));
            float rarity = variety.traits?.Where(trait => trait != null).Sum(trait => Mathf.Max(0f, 1f - Mathf.Min(1f, trait.commonality))) ?? 0f;
            return Mathf.Clamp(50 + traitValue + lineageValue + Mathf.RoundToInt(Mathf.Min(10f, rarity * 2f)) - balancePenalty, 0, 100);
        }

        private static int LineageDepth(VarietyRecord variety)
        {
            return LineageDepth(variety, new HashSet<string>());
        }

        private static int LineageDepth(VarietyRecord variety, HashSet<string> path)
        {
            if (variety?.parentVarietyIds == null || variety.parentVarietyIds.Count == 0 || !path.Add(variety.id)) return 0;
            int depth = 1 + variety.parentVarietyIds.Select(id => LineageDepth(GameComponent_NovelSeeds.Instance?.GetVariety(id), path)).DefaultIfEmpty(0).Max();
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

    public class Dialog_CreateBreedingProgram : Window
    {
        private ThingDef cropDef;
        private string programName = string.Empty;
        private string traitSearch = string.Empty;
        private readonly HashSet<VarietyTraitDef> selectedTraits = new HashSet<VarietyTraitDef>();
        private Vector2 scroll;

        public override Vector2 InitialSize => new Vector2(760f, 680f);

        public Dialog_CreateBreedingProgram()
        {
            doCloseX = true;
            forcePause = true;
            absorbInputAroundWindow = true;
            closeOnAccept = false;
            cropDef = GameComponent_NovelSeeds.Instance?.AllVarieties.Select(variety => variety.cropDef).FirstOrDefault(def => def != null)
                ?? DefDatabase<ThingDef>.AllDefsListForReading.FirstOrDefault(NovelSeedUtility.IsGrowableCrop);
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 32f), "HNS_RegistryCreateProgram".Translate());
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(inRect.x, inRect.y + 42f, 150f, 28f), "HNS_RegistryProgramName".Translate());
            programName = Widgets.TextField(new Rect(inRect.x + 156f, inRect.y + 40f, inRect.width - 156f, 30f), programName);
            Widgets.Label(new Rect(inRect.x, inRect.y + 80f, 150f, 28f), "HNS_RegistryPlant".Translate());
            if (Widgets.ButtonText(new Rect(inRect.x + 156f, inRect.y + 78f, 280f, 30f), cropDef?.LabelCap ?? "HNS_RegistryChoose".Translate()))
            {
                List<FloatMenuOption> options = DefDatabase<ThingDef>.AllDefsListForReading.Where(NovelSeedUtility.IsGrowableCrop)
                    .OrderBy(def => def.label).Select(def => new FloatMenuOption(def.LabelCap, delegate
                    {
                        cropDef = def;
                        selectedTraits.RemoveWhere(trait => HorticultureNovelSeedsMod.Settings?.IsTraitAllowed(cropDef, trait) == false);
                    })).ToList();
                Find.WindowStack.Add(new FloatMenu(options));
            }
            Widgets.Label(new Rect(inRect.x, inRect.y + 122f, 150f, 28f), "HNS_RegistryDesiredTraits".Translate());
            traitSearch = Widgets.TextField(new Rect(inRect.x + 156f, inRect.y + 120f, inRect.width - 156f, 30f), traitSearch);
            List<VarietyTraitDef> traits = TraitConfigUtility.TopLevelTraits()
                .Where(trait => cropDef == null || HorticultureNovelSeedsMod.Settings?.IsTraitAllowed(cropDef, trait) != false)
                .Where(trait => traitSearch.NullOrEmpty() || trait.LabelCap.ToString().IndexOf(traitSearch, StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();
            Rect outRect = new Rect(inRect.x, inRect.y + 160f, inRect.width, inRect.height - 214f);
            Widgets.DrawMenuSection(outRect);
            Rect scrollOut = outRect.ContractedBy(8f);
            float viewWidth = scrollOut.width - 16f;
            Rect viewRect = new Rect(0f, 0f, viewWidth, Mathf.Max(scrollOut.height, traits.Count * 34f));
            Widgets.BeginScrollView(scrollOut, ref scroll, viewRect);
            for (int i = 0; i < traits.Count; i++)
            {
                VarietyTraitDef trait = traits[i];
                bool chosen = selectedTraits.Contains(trait);
                Rect row = new Rect(0f, i * 34f, viewRect.width, 30f);
                Widgets.CheckboxLabeled(row, trait.LabelCap, ref chosen);
                if (chosen) selectedTraits.Add(trait); else selectedTraits.Remove(trait);
                TooltipHandler.TipRegion(row, trait.description);
            }
            Widgets.EndScrollView();
            GUI.enabled = cropDef != null && selectedTraits.Count > 0;
            if (Widgets.ButtonText(new Rect(inRect.xMax - 130f, inRect.yMax - 38f, 130f, 32f), "HNS_RegistryCreate".Translate()))
            {
                GameComponent_NovelSeeds.Instance?.AddBreedingProgram(programName, cropDef, selectedTraits);
                Close();
            }
            GUI.enabled = true;
        }

        public override void OnAcceptKeyPressed()
        {
            if (cropDef != null && selectedTraits.Count > 0)
            {
                GameComponent_NovelSeeds.Instance?.AddBreedingProgram(programName, cropDef, selectedTraits);
                Close();
            }
        }
    }
}
