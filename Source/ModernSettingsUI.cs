using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace HorticultureNovelSeeds
{
    public static class NovelSeedsSettingsUI
    {
        private const float SidebarWidth = 248f;
        private const float NavRowHeight = 36f;
        private const float CategoryRowHeight = 38f;
        private const float TraitRowHeight = 48f;
        private const float ExpandedGlobalHeight = 166f;
        private const float ExpandedWildHeight = 72f;
        private const float ExpandedPlantHeight = 124f;
        private static readonly Color Accent = new Color(0.34f, 0.72f, 0.45f);
        private static readonly Color SectionBand = new Color(0.15f, 0.17f, 0.18f);
        private static readonly Color ExpandedBand = new Color(0.12f, 0.14f, 0.15f);
        private static readonly HashSet<string> OpenGlobalCategories = new HashSet<string>();
        private static readonly HashSet<string> OpenWildCategories = new HashSet<string>();
        private static readonly HashSet<string> OpenPlantCategories = new HashSet<string>();
        private static Vector2 plantScroll;
        private static Vector2 groupScroll;
        private static Vector2 contentScroll;
        private static ThingDef selectedPlant;
        private static PlantGroupRecord selectedGroup;
        private static SettingsPage selectedPage = SettingsPage.General;
        private static string plantSearch = string.Empty;
        private static string globalTraitSearch = string.Empty;
        private static string wildTraitSearch = string.Empty;
        private static string plantTraitSearch = string.Empty;
        private static string plantTagSearch = string.Empty;
        private static string expandedGlobalTrait;
        private static string expandedWildTrait;
        private static string expandedPlantTrait;

        public static ThingDef CurrentPlantPreview => selectedPlant
            ?? (selectedPage == SettingsPage.Group ? selectedGroup?.Plants.FirstOrDefault() : null);

        public static void DoWindowContents(Rect inRect, NovelSeedsSettings settings)
        {
            settings.Normalize();
            Rect sidebar = new Rect(inRect.x, inRect.y, SidebarWidth, inRect.height);
            Rect content = new Rect(sidebar.xMax + 12f, inRect.y, inRect.width - SidebarWidth - 12f, inRect.height);
            DrawSidebar(sidebar);
            Widgets.DrawMenuSection(content);
            DrawCurrentPage(content.ContractedBy(14f), settings);
        }

        private static void DrawSidebar(Rect rect)
        {
            Widgets.DrawMenuSection(rect);
            Rect inner = rect.ContractedBy(6f);
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inner.x + 6f, inner.y + 4f, inner.width - 12f, 30f), "Novel Seeds");
            Text.Font = GameFont.Small;
            float y = inner.y + 42f;
            DrawNavRow(new Rect(inner.x, y, inner.width, NavRowHeight), "General Settings", selectedPlant == null && selectedPage == SettingsPage.General, () => SelectPage(SettingsPage.General));
            y += NavRowHeight;
            DrawNavRow(new Rect(inner.x, y, inner.width, NavRowHeight), "Produce Traits", selectedPlant == null && selectedPage == SettingsPage.ProduceTraits, () => SelectPage(SettingsPage.ProduceTraits));
            y += NavRowHeight;
            DrawNavRow(new Rect(inner.x, y, inner.width, NavRowHeight), "Wild Settings", selectedPlant == null && selectedPage == SettingsPage.Wild, () => SelectPage(SettingsPage.Wild));
            y += NavRowHeight;
            DrawNavRow(new Rect(inner.x, y, inner.width, NavRowHeight), "Config Profiles", selectedPlant == null && selectedPage == SettingsPage.Profiles, () => SelectPage(SettingsPage.Profiles));
            y += NavRowHeight;
            DrawNavRow(new Rect(inner.x, y, inner.width, NavRowHeight), "Plant Tags", selectedPlant == null && selectedPage == SettingsPage.Tags, () => SelectPage(SettingsPage.Tags));
            y += NavRowHeight + 12f;
            DrawSmallHeader(new Rect(inner.x + 6f, y, inner.width - 88f, 24f), "Plant Groups");
            if (Widgets.ButtonText(new Rect(inner.xMax - 78f, y - 4f, 78f, 28f), "New")) Find.WindowStack.Add(new Dialog_PlantGroupName(HorticultureNovelSeedsMod.Settings));
            y += 28f;
            List<PlantGroupRecord> groups = HorticultureNovelSeedsMod.Settings.PlantGroups.OrderBy(group => group.Name).ToList();
            float groupHeight = groups.Count == 0 ? 30f : Mathf.Min(108f, groups.Count * NavRowHeight);
            Rect groupOut = new Rect(inner.x, y, inner.width, groupHeight);
            Rect groupView = new Rect(0f, 0f, groupOut.width - (groups.Count * NavRowHeight > groupHeight ? 16f : 0f), Mathf.Max(groupHeight, groups.Count * NavRowHeight));
            Widgets.BeginScrollView(groupOut, ref groupScroll, groupView);
            if (groups.Count == 0) DrawMutedLabel(new Rect(6f, 4f, groupView.width - 12f, 24f), "No groups yet");
            float groupY = 0f;
            foreach (PlantGroupRecord group in groups)
            {
                PlantGroupRecord localGroup = group;
                DrawNavRow(new Rect(0f, groupY, groupView.width, NavRowHeight), group.Name + "  (" + group.PlantCount + ")", selectedGroup == group && selectedPlant == null, delegate
                {
                    selectedGroup = localGroup;
                    selectedPlant = null;
                    selectedPage = SettingsPage.Group;
                    contentScroll = Vector2.zero;
                });
                groupY += NavRowHeight;
            }
            Widgets.EndScrollView();
            y += groupHeight + 12f;
            DrawSmallHeader(new Rect(inner.x + 6f, y, inner.width - 12f, 24f), "Plants");
            y += 28f;
            plantSearch = DrawSearchField(new Rect(inner.x, y, inner.width, 30f), plantSearch, "Plant search");
            y += 38f;
            List<ThingDef> plants = GrowablePlants().Where(plant => MatchesSearch(plant.LabelCap, plant.defName, plantSearch)).ToList();
            Rect outRect = new Rect(inner.x, y, inner.width, inner.yMax - y);
            Rect viewRect = new Rect(0f, 0f, outRect.width - 16f, Mathf.Max(outRect.height, plants.Count * NavRowHeight));
            Widgets.BeginScrollView(outRect, ref plantScroll, viewRect);
            float rowY = 0f;
            foreach (ThingDef plant in plants)
            {
                ThingDef localPlant = plant;
                DrawNavRow(new Rect(0f, rowY, viewRect.width, NavRowHeight), plant.LabelCap, selectedPlant == plant, delegate
                {
                    selectedPlant = localPlant;
                    selectedGroup = null;
                    selectedPage = SettingsPage.Plant;
                    contentScroll = Vector2.zero;
                    expandedPlantTrait = null;
                });
                rowY += NavRowHeight;
            }
            Widgets.EndScrollView();
        }

        private static void DrawNavRow(Rect rect, string label, bool selected, System.Action action)
        {
            if (selected)
            {
                Widgets.DrawHighlightSelected(rect);
                Widgets.DrawBoxSolid(new Rect(rect.x, rect.y, 3f, rect.height), Accent);
            }
            else Widgets.DrawHighlightIfMouseover(rect);
            Widgets.Label(new Rect(rect.x + 10f, rect.y + 8f, rect.width - 16f, 24f), label);
            if (Widgets.ButtonInvisible(rect)) action();
        }

        private static void SelectPage(SettingsPage page)
        {
            selectedPlant = null;
            selectedGroup = null;
            selectedPage = page;
            contentScroll = Vector2.zero;
        }

        private static void DrawCurrentPage(Rect rect, NovelSeedsSettings settings)
        {
            if (selectedGroup != null && selectedPlant == null) DrawGroupPage(rect, settings, selectedGroup);
            else if (selectedPlant != null) DrawPlantPage(rect, settings, selectedPlant);
            else if (selectedPage == SettingsPage.ProduceTraits) DrawProduceTraitsPage(rect, settings);
            else if (selectedPage == SettingsPage.Wild) DrawWildPage(rect, settings);
            else if (selectedPage == SettingsPage.Profiles) DrawProfilesPage(rect, settings);
            else if (selectedPage == SettingsPage.Tags) DrawPlantTagsPage(rect, settings);
            else DrawGeneralPage(rect, settings);
        }

        private static void DrawGeneralPage(Rect rect, NovelSeedsSettings settings)
        {
            List<VarietyTraitDef> allTraits = AllTraits();
            List<string> categories = settings.TraitGroupNames().ToList();
            List<VarietyTraitDef> shownTraits = FilterTraits(allTraits, globalTraitSearch, settings);
            float contentHeight = 970f + Mathf.CeilToInt(categories.Count / 2f) * 32f + TraitGroupsHeight(shownTraits, settings, OpenGlobalCategories, expandedGlobalTrait, ExpandedGlobalHeight, 124f, globalTraitSearch);
            BeginPage(rect, contentHeight, out Rect view);
            float y = 0f;
            DrawPageTitle(view, ref y, "General Settings", "Defaults used by every growable plant unless overridden.");
            DrawSectionHeader(view, ref y, "Mutation And Cross-Pollination");
            float half = (view.width - 14f) / 2f;
            DrawPercentControl(new Rect(0f, y, half, 58f), "Default Mutation Rate", ref settings.globalMutationChance, 0f, 1f);
            DrawPercentControl(new Rect(half + 14f, y, half, 58f), "Default Cross-Pollination Rate", ref settings.globalCrossPollinationChance, 0f, 1f);
            y += 66f;
            DrawStepper(new Rect(0f, y, half, 84f), "Max New Traits Per Mutation", ref settings.maxTraitsPerEvent, 1, 10);
            DrawStepper(new Rect(half + 14f, y, half, 84f), "Max Donor Traits Per Cross", ref settings.maxCrossPollinationTraits, 1, 10);
            y += 94f;
            DrawSectionHeader(view, ref y, "Trait Balance");
            Rect balanceToggleRect = new Rect(0f, y, view.width, 28f);
            Widgets.CheckboxLabeled(balanceToggleRect, "Balance Positive And Negative Traits", ref settings.enableTraitBalancing);
            TooltipHandler.TipRegion(balanceToggleRect, "When enabled, new traits are weighted toward reducing the resulting variety's positive or negative imbalance.");
            y += 36f;
            GUI.enabled = settings.enableTraitBalancing;
            DrawPercentControl(new Rect(0f, y, half, 58f), "Balance Strength", ref settings.traitBalanceStrength, 0f, 1f);
            DrawPercentControl(new Rect(half + 14f, y, half, 58f), "Exceptional Variety Chance", ref settings.exceptionalVarietyChance, 0f, 0.5f);
            y += 66f;
            DrawStepper(new Rect(0f, y, half, 58f), "Allowed Trait Imbalance", ref settings.allowedTraitImbalance, 0, 10);
            TooltipHandler.TipRegion(new Rect(0f, y, half, 58f), "A resulting score within this distance of zero is considered balanced. Strong traits may cause an extra compensating trait, up to the event maximum.");
            GUI.enabled = true;
            y += 68f;
            Rect produceVisualsRect = new Rect(0f, y, view.width, 28f);
            Widgets.CheckboxLabeled(produceVisualsRect, "Carry Variety Visuals To Harvested Produce", ref settings.enableProduceVisuals);
            TooltipHandler.TipRegion(produceVisualsRect, "When enabled, harvested produce inherits the Produce visual configured for its variety traits. Different appearances remain separate stacks without creating extra product definitions.");
            y += 40f;
            DrawSectionHeader(view, ref y, "Per-Save Species Color Palettes");
            DrawStepper(new Rect(0f, y, half, 58f), "Minimum Palette Size", ref settings.minimumPaletteSize, 1, 24);
            DrawStepper(new Rect(half + 14f, y, half, 58f), "Maximum Palette Size", ref settings.maximumPaletteSize, 1, 24);
            y += 66f;
            settings.maximumPaletteSize = Mathf.Max(settings.minimumPaletteSize, settings.maximumPaletteSize);
            settings.allowedHueRangeDegrees = Widgets.HorizontalSlider(new Rect(0f, y + 24f, view.width, 24f), settings.allowedHueRangeDegrees, 0f, 360f, false,
                "Allowed Hue Range: " + Mathf.RoundToInt(settings.allowedHueRangeDegrees) + " degrees");
            y += 58f;
            DrawPercentControl(new Rect(0f, y, half, 58f), "Minimum Saturation", ref settings.minimumPaletteSaturation, 0f, 1f);
            DrawPercentControl(new Rect(half + 14f, y, half, 58f), "Maximum Saturation", ref settings.maximumPaletteSaturation, 0f, 1f);
            y += 66f;
            DrawPercentControl(new Rect(0f, y, half, 58f), "Minimum Value", ref settings.minimumPaletteValue, 0f, 1f);
            DrawPercentControl(new Rect(half + 14f, y, half, 58f), "Maximum Value", ref settings.maximumPaletteValue, 0f, 1f);
            y += 68f;
            if (Widgets.ButtonText(new Rect(0f, y, 168f, 30f), "Reset Global Weights")) Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("Reset global trait weights to defaults? Plant-specific settings will be kept.", delegate { settings.ResetGlobalWeights(); }, true));
            if (Widgets.ButtonText(new Rect(178f, y, 110f, 30f), "Full Reset")) Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("Reset all active Horticulture - Novel Seeds settings to the bundled mod defaults? Saved configuration profiles will be kept.", delegate { ApplyDefaults(settings); contentScroll = Vector2.zero; }, true));
            y += 46f;
            DrawSectionHeader(view, ref y, "Trait Groups");
            DrawMutedLabel(new Rect(0f, y, view.width, 34f), "These toggles enable or disable every trait in the group globally. Plant and plant-group overrides take precedence.");
            y += 40f;
            float categoryWidth = (view.width - 12f) / 2f;
            for (int i = 0; i < categories.Count; i++)
            {
                string category = categories[i];
                CategorySettingsRecord record = settings.GetCategorySettings(category);
                Rect groupRect = new Rect((i % 2) * (categoryWidth + 12f), y + (i / 2) * 32f, categoryWidth, 28f);
                Widgets.CheckboxLabeled(new Rect(groupRect.x, groupRect.y, groupRect.width - 84f, groupRect.height), category, ref record.enabled);
                if (Widgets.ButtonText(new Rect(groupRect.xMax - 76f, groupRect.y, 76f, groupRect.height), "Traits")) Find.WindowStack.Add(new Dialog_TraitGroupMembers(settings, category));
            }
            y += Mathf.CeilToInt(categories.Count / 2f) * 32f + 12f;
            DrawTraitListToolbar(view, ref y, "Trait Defaults", ref globalTraitSearch, OpenGlobalCategories, categories);
            DrawGlobalTraitGroups(view, ref y, shownTraits, settings);
            EndPage();
        }
        private static void DrawProduceTraitsPage(Rect rect, NovelSeedsSettings settings)
        {
            List<VarietyTraitDef> traits = AllTraits().OrderBy(trait => trait.label).ToList();
            float contentHeight = 84f + traits.Sum(trait => ProduceTraitRowHeight(trait, rect.width - 16f, settings));
            BeginPage(rect, contentHeight, out Rect view);
            float y = 0f;
            DrawPageTitle(view, ref y, "Produce Traits", "Every trait is inherited by harvested produce. Traits without a programmed produce effect remain informational.");
            foreach (VarietyTraitDef trait in traits)
            {
                string effect = ProduceTraitEffectUtility.Summary(trait, settings);
                float rowHeight = ProduceTraitRowHeight(trait, view.width, settings);
                Rect row = new Rect(0f, y, view.width, rowHeight - 6f);
                Widgets.DrawBoxSolid(row, SectionBand);
                Widgets.DrawBoxSolid(new Rect(row.x, row.y, 3f, row.height), Accent);
                Widgets.Label(new Rect(row.x + 12f, row.y + 8f, row.width - 24f, 24f), ConfigTraitLabel(trait));
                DrawMutedLabel(new Rect(row.x + 12f, row.y + 34f, row.width - 24f, row.height - 40f), effect);
                TooltipHandler.TipRegion(row, TraitColorUI.Tooltip(trait));
                y += rowHeight;
            }
            EndPage();
        }
        private static float ProduceTraitRowHeight(VarietyTraitDef trait, float width, NovelSeedsSettings settings)
        {
            string effect = ProduceTraitEffectUtility.Summary(trait, settings);
            return Mathf.Max(70f, 46f + Text.CalcHeight(effect, Mathf.Max(160f, width - 24f)));
        }
        private static void DrawWildPage(Rect rect, NovelSeedsSettings settings)
        {
            List<VarietyTraitDef> allTraits = AllTraits();
            List<VarietyTraitDef> shownTraits = FilterTraits(allTraits, wildTraitSearch, settings);
            List<string> categories = shownTraits.Select(settings.TraitGroup).Distinct().OrderBy(category => category).ToList();
            float contentHeight = 310f + TraitGroupsHeight(shownTraits, settings, OpenWildCategories, expandedWildTrait, ExpandedWildHeight, 42f, wildTraitSearch);
            BeginPage(rect, contentHeight, out Rect view);
            float y = 0f;
            DrawPageTitle(view, ref y, "Wild Settings", "Controls rare novel traits on naturally generated plants.");
            DrawSectionHeader(view, ref y, "Wild Mutation");
            DrawPercentControl(new Rect(0f, y, Mathf.Min(460f, view.width), 58f), "Wild Variety Rate", ref settings.wildMutationChance, 0f, 0.10f);
            if (Widgets.ButtonText(new Rect(view.width - 150f, y + 58f, 150f, 30f), "Reset Wild Settings")) Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("Reset the wild variety rate and all wild trait settings to defaults?", delegate { settings.ResetWildSettings(); }, true));
            y += 102f;
            DrawTraitListToolbar(view, ref y, "Possible Wild Traits", ref wildTraitSearch, OpenWildCategories, categories);
            DrawWildTraitGroups(view, ref y, shownTraits, settings);
            EndPage();
        }

        private static void DrawPlantTagsPage(Rect rect, NovelSeedsSettings settings)
        {
            List<string> tags = PlantTagUtility.ConfigurableTags()
                .Where(tag => plantTagSearch.NullOrEmpty() || tag.IndexOf(plantTagSearch.Trim(), System.StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();
            float contentHeight = 144f + tags.Count * 48f;
            BeginPage(rect, contentHeight, out Rect view);
            float y = 0f;
            DrawPageTitle(view, ref y, "Plant Tags", "Automatic produce classifications control which specialized traits each plant can receive.");
            plantTagSearch = DrawSearchField(new Rect(0f, y, view.width - 142f, 30f), plantTagSearch, "Tag search");
            if (Widgets.ButtonText(new Rect(view.width - 132f, y, 132f, 30f), "Scan All Plants"))
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("Clear every manual plant-tag change and scan all plants and harvested products again?", delegate
                {
                    settings.ScanAllPlantTags();
                    Messages.Message("All plant tags scanned.", MessageTypeDefOf.TaskCompletion, false);
                }, true));
            y += 42f;
            foreach (string tag in tags)
            {
                int members = GrowablePlants().Count(plant => PlantTagUtility.HasTag(plant, tag));
                Rect row = new Rect(0f, y, view.width, 40f);
                Widgets.DrawHighlightIfMouseover(row);
                Widgets.Label(new Rect(10f, y + 9f, view.width - 286f, 24f), tag);
                DrawMutedLabel(new Rect(view.width - 274f, y + 9f, 92f, 24f), members + " plants", TextAnchor.UpperRight);
                if (Widgets.ButtonText(new Rect(view.width - 172f, y + 5f, 76f, 30f), "Plants")) Find.WindowStack.Add(new Dialog_TagPlantMembers(settings, tag));
                if (Widgets.ButtonText(new Rect(view.width - 88f, y + 5f, 80f, 30f), "Scan"))
                {
                    string selectedTag = tag;
                    Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("Clear manual '" + selectedTag + "' changes for every plant and scan all definitions again?", delegate
                    {
                        settings.ResetTag(selectedTag);
                        Messages.Message(selectedTag + " membership scanned.", MessageTypeDefOf.TaskCompletion, false);
                    }, true));
                }
                y += 48f;
            }
            EndPage();
        }

        private static void DrawPlantPage(Rect rect, NovelSeedsSettings settings, ThingDef plantDef)
        {
            PlantGroupRecord activeGroup = settings.GroupForPlant(plantDef);
            PlantSettingsRecord plant = activeGroup?.Settings ?? settings.GetPlantSettings(plantDef);
            List<VarietyTraitDef> allTraits = AllTraits().Where(settings.IsTraitGloballyAllowed).ToList();
            List<VarietyTraitDef> shownTraits = FilterTraits(allTraits, plantTraitSearch, settings);
            List<string> traitGroups = shownTraits.Select(settings.TraitGroup).Distinct().OrderBy(group => group).ToList();
            List<string> categories = traitGroups.ToList();
            IReadOnlyList<string> displayTags = PlantTagUtility.DisplayTagsFor(plantDef);
            string tagSummary = displayTags.Count == 0 ? "None detected" : string.Join(", ", displayTags);
            float tagTextHeight = Mathf.Max(24f, Text.CalcHeight(tagSummary, Mathf.Max(200f, rect.width - 32f)));
            float rateHeight = 88f + (plant.useCustomMutationChance ? 48f : 0f) + (plant.useCustomCrossPollinationChance ? 48f : 0f);
            bool hasProduce = plantDef.plant?.harvestedThingDef != null;
            float contentHeight = 284f + tagTextHeight + 46f + rateHeight + TraitGroupToggleHeight(traitGroups.Count) + (activeGroup == null ? 0f : 68f) + TraitGroupsHeight(shownTraits, settings, OpenPlantCategories, expandedPlantTrait, ExpandedPlantHeight, 84f, plantTraitSearch);
            BeginPage(rect, contentHeight, out Rect view);
            float y = 0f;
            DrawPageTitle(view, ref y, plantDef.LabelCap, activeGroup == null ? "Plant-Specific Settings" : "Using Group Settings");
            if (activeGroup != null) DrawGroupBanner(view, ref y, settings, plantDef, activeGroup);
            if (Widgets.ButtonText(new Rect(view.width - 116f, 0f, 116f, 30f), activeGroup == null ? "Reset Plant" : "Reset Group"))
            {
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("Reset every setting for " + (activeGroup == null ? plantDef.LabelCap : activeGroup.Name + " group") + "?", delegate
                {
                    if (activeGroup == null) settings.ResetPlant(plantDef); else activeGroup.Settings.ResetToDefaults();
                    expandedPlantTrait = null;
                }, true));
            }
            DrawSectionHeader(view, ref y, "Plant Capabilities");
            Rect tagsRect = new Rect(0f, y, view.width, tagTextHeight);
            DrawMutedLabel(tagsRect, tagSummary);
            TooltipHandler.TipRegion(tagsRect, string.Join("\n", PlantTagUtility.TagsFor(plantDef)));
            y += tagTextHeight + 12f;
            if (Widgets.ButtonText(new Rect(0f, y, 126f, 30f), "Edit Tags")) Find.WindowStack.Add(new Dialog_PlantTagEditor(settings, plantDef));
            if (Widgets.ButtonText(new Rect(136f, y, 126f, 30f), "Scan Tags"))
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("Clear manual tag changes for " + plantDef.LabelCap + " and scan its definitions and harvested product again?", delegate
                {
                    settings.ResetPlantTags(plantDef);
                    Messages.Message(plantDef.LabelCap + " tags scanned.", MessageTypeDefOf.TaskCompletion, false);
                }, true));
            y += 42f;
            PlantSettingsRecord personalSettings = settings.GetPlantSettings(plantDef);
            string maskStatus = "Plant: " + (personalSettings.usePlantMasks ? "On" : "Off") + (hasProduce ? "    Produce: " + (personalSettings.useProduceMasks ? "On" : "Off") : "    No harvested produce");
            if (Widgets.ButtonText(new Rect(0f, y, 120f, 30f), "Masks")) Find.WindowStack.Add(new Dialog_PlantMasks(plantDef));
            if (Widgets.ButtonText(new Rect(130f, y, 110f, 30f), "Export")) Find.WindowStack.Add(new Dialog_ExportPlantMasks(settings, plantDef));
            if (Widgets.ButtonText(new Rect(250f, y, 110f, 30f), "Import")) Find.WindowStack.Add(new Dialog_ImportPlantMaskForPlant(settings, plantDef));
            DrawMutedLabel(new Rect(374f, y + 5f, view.width - 374f, 24f), maskStatus);
            y += 42f;
            Widgets.CheckboxLabeled(new Rect(0f, y, view.width, 28f), "Use Unrestricted Colors", ref personalSettings.unrestrictedColors);
            TooltipHandler.TipRegion(new Rect(0f, y, view.width, 28f), "New saves may use the full hue wheel for this species instead of staying near its configured base color.");
            y += 38f;
            DrawSectionHeader(view, ref y, "Rates");
            DrawRateControls(view, ref y, settings, plant);
            DrawSectionHeader(view, ref y, "Trait Groups");
            DrawPlantTraitGroupToggles(view, ref y, settings, plant, traitGroups);
            DrawTraitListToolbar(view, ref y, "Possible Traits", ref plantTraitSearch, OpenPlantCategories, categories);
            DrawPlantTraitGroups(view, ref y, shownTraits, settings, plant, plantDef, activeGroup);
            EndPage();
        }

        private static void DrawGroupBanner(Rect view, ref float y, NovelSeedsSettings settings, ThingDef plantDef, PlantGroupRecord group)
        {
            Rect band = new Rect(0f, y, view.width, 54f);
            Widgets.DrawBoxSolid(band, ExpandedBand);
            Widgets.DrawBoxSolid(new Rect(band.x, band.y, 3f, band.height), Accent);
            Widgets.Label(new Rect(12f, band.y + 7f, band.width - 250f, 24f), "Using Group: " + group.Name);
            DrawMutedLabel(new Rect(12f, band.y + 29f, band.width - 250f, 20f), "Changes here apply to " + group.PlantCount + " plants.");
            if (Widgets.ButtonText(new Rect(band.xMax - 218f, band.y + 12f, 100f, 30f), "Open Group"))
            {
                selectedGroup = group;
                selectedPlant = null;
                selectedPage = SettingsPage.Group;
                contentScroll = Vector2.zero;
            }
            if (Widgets.ButtonText(new Rect(band.xMax - 108f, band.y + 12f, 96f, 30f), "Remove"))
            {
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("Remove " + plantDef.LabelCap + " from the " + group.Name + " group? Its saved personal settings will become active again.", delegate { settings.RemovePlantFromGroup(plantDef); }, true));
            }
            y += 68f;
        }

        private static void DrawGroupPage(Rect rect, NovelSeedsSettings settings, PlantGroupRecord group)
        {
            if (group == null || !settings.PlantGroups.Contains(group))
            {
                selectedGroup = null;
                selectedPage = SettingsPage.General;
                return;
            }
            PlantSettingsRecord plant = group.Settings;
            ThingDef previewPlant = group.Plants.FirstOrDefault();
            List<VarietyTraitDef> allTraits = AllTraits().Where(settings.IsTraitGloballyAllowed).ToList();
            List<VarietyTraitDef> shownTraits = FilterTraits(allTraits, plantTraitSearch, settings);
            List<string> traitGroups = shownTraits.Select(settings.TraitGroup).Distinct().OrderBy(category => category).ToList();
            List<string> categories = traitGroups.ToList();
            float rateHeight = 88f + (plant.useCustomMutationChance ? 48f : 0f) + (plant.useCustomCrossPollinationChance ? 48f : 0f);
            float contentHeight = 250f + rateHeight + TraitGroupToggleHeight(traitGroups.Count) + TraitGroupsHeight(shownTraits, settings, OpenPlantCategories, expandedPlantTrait, ExpandedPlantHeight, 84f, plantTraitSearch);
            BeginPage(rect, contentHeight, out Rect view);
            float y = 0f;
            DrawPageTitle(view, ref y, group.Name, "Plant Group - " + group.PlantCount + " plants");
            float buttonWidth = Mathf.Min(120f, (view.width - 30f) / 4f);
            if (Widgets.ButtonText(new Rect(0f, y, buttonWidth, 30f), "Manage Plants")) Find.WindowStack.Add(new Dialog_PlantGroupMembers(settings, group));
            if (Widgets.ButtonText(new Rect(buttonWidth + 10f, y, buttonWidth, 30f), "Rename")) Find.WindowStack.Add(new Dialog_PlantGroupName(settings, group));
            if (Widgets.ButtonText(new Rect((buttonWidth + 10f) * 2f, y, buttonWidth, 30f), "Reset Group")) Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("Reset every setting for the " + group.Name + " group?", delegate { group.Settings.ResetToDefaults(); expandedPlantTrait = null; }, true));
            if (Widgets.ButtonText(new Rect((buttonWidth + 10f) * 3f, y, buttonWidth, 30f), "Delete Group")) Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("Delete the " + group.Name + " group? Member plants will return to their saved personal settings.", delegate { settings.DeletePlantGroup(group); selectedGroup = null; selectedPage = SettingsPage.General; contentScroll = Vector2.zero; }, true));
            y += 48f;
            DrawSectionHeader(view, ref y, "Rates");
            DrawRateControls(view, ref y, settings, plant);
            DrawSectionHeader(view, ref y, "Trait Groups");
            DrawPlantTraitGroupToggles(view, ref y, settings, plant, traitGroups);
            DrawTraitListToolbar(view, ref y, "Group Trait Settings", ref plantTraitSearch, OpenPlantCategories, categories);
            DrawPlantTraitGroups(view, ref y, shownTraits, settings, plant, previewPlant, group);
            EndPage();
        }

        private static void DrawRateControls(Rect view, ref float y, NovelSeedsSettings settings, PlantSettingsRecord plant)
        {
            float toggleWidth = Mathf.Min(420f, Mathf.Max(300f, view.width - 286f));
            float inheritedX = toggleWidth + 12f;
            float inheritedWidth = Mathf.Max(0f, view.width - inheritedX);
            Widgets.CheckboxLabeled(new Rect(0f, y, toggleWidth, 28f), "Override Mutation Rate", ref plant.useCustomMutationChance);
            y += 32f;
            if (plant.useCustomMutationChance)
            {
                DrawPercentControl(new Rect(18f, y, view.width - 18f, 42f), "Mutation Rate", ref plant.mutationChance, 0f, 1f, true);
                y += 48f;
            }
            else DrawInheritedValue(new Rect(inheritedX, y - 32f, inheritedWidth, 28f), "Global: " + settings.globalMutationChance.ToStringPercent());
            Widgets.CheckboxLabeled(new Rect(0f, y, toggleWidth, 28f), "Override Cross-Pollination Rate", ref plant.useCustomCrossPollinationChance);
            y += 32f;
            if (plant.useCustomCrossPollinationChance)
            {
                DrawPercentControl(new Rect(18f, y, view.width - 18f, 42f), "Cross-Pollination Rate", ref plant.crossPollinationChance, 0f, 1f, true);
                y += 48f;
            }
            else DrawInheritedValue(new Rect(inheritedX, y - 32f, inheritedWidth, 28f), "Global: " + settings.globalCrossPollinationChance.ToStringPercent());
            y += 12f;
        }
        private static float TraitGroupToggleHeight(int groupCount)
        {
            return 52f + Mathf.CeilToInt(groupCount / 2f) * 32f;
        }

        private static void DrawPlantTraitGroupToggles(Rect view, ref float y, NovelSeedsSettings settings, PlantSettingsRecord plant, List<string> traitGroups)
        {
            float columnWidth = (view.width - 12f) / 2f;
            for (int i = 0; i < traitGroups.Count; i++)
            {
                string groupName = traitGroups[i];
                CategorySettingsRecord record = plant.GetTraitGroupSettings(groupName);
                bool globallyEnabled = settings.GetCategorySettings(groupName, false)?.enabled ?? true;
                Rect row = new Rect((i % 2) * (columnWidth + 12f), y + (i / 2) * 32f, columnWidth, 28f);
                bool enabled = record.enabled;
                bool oldGuiEnabled = GUI.enabled;
                GUI.enabled = oldGuiEnabled && globallyEnabled;
                Widgets.CheckboxLabeled(new Rect(row.x, row.y, row.width - (globallyEnabled ? 0f : 112f), row.height), groupName, ref enabled);
                GUI.enabled = oldGuiEnabled;
                record.enabled = enabled;
                if (!globallyEnabled)
                {
                    DrawMutedLabel(new Rect(row.xMax - 108f, row.y + 2f, 108f, 24f), "Global Off", TextAnchor.UpperRight);
                    TooltipHandler.TipRegion(row, "This trait group is disabled in General Settings.");
                }
            }
            y += Mathf.CeilToInt(traitGroups.Count / 2f) * 32f + 12f;
        }
        private static void DrawProfilesPage(Rect rect, NovelSeedsSettings settings)
        {
            List<SettingsProfileInfo> profiles = SettingsProfileManager.Profiles.ToList();
            List<PlantMaskFileInfo> maskFiles = PlantMaskFileManager.Files.ToList();
            float contentHeight = 468f + profiles.Count * 62f + maskFiles.Count * 62f;
            BeginPage(rect, contentHeight, out Rect view);
            float y = 0f;
            DrawPageTitle(view, ref y, "Configuration Profiles", "Save complete configurations or transfer only the painted masks for every plant.");
            if (Widgets.ButtonText(new Rect(0f, y, 210f, 32f), "Save Current Configuration")) Find.WindowStack.Add(new Dialog_SaveSettingsProfile(settings));
            if (Widgets.ButtonText(new Rect(220f, y, 130f, 32f), "Use Defaults")) Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("Switch all active settings and masks to the bundled mod defaults? Saved profiles will be kept.", delegate { ApplyDefaults(settings); }, true));
            y += 42f;
            if (Widgets.ButtonText(new Rect(0f, y, 210f, 32f), "Export As Mod Default")) ConfirmExportPublisherDefault(settings);
            if (Widgets.ButtonText(new Rect(220f, y, 170f, 32f), "Open Export Folder") && !SettingsProfileManager.OpenPublisherDirectory(out string openError)) Messages.Message("Could not open the publisher export folder: " + openError, MessageTypeDefOf.RejectInput, false);
            DrawMutedLabel(new Rect(400f, y, view.width - 400f, 32f), SettingsProfileManager.HasBundledDefault ? "Bundled default active" : "No bundled default", TextAnchor.MiddleLeft);
            y += 52f;
            DrawSectionHeader(view, ref y, "Saved Configurations");
            if (profiles.Count == 0)
            {
                DrawMutedLabel(new Rect(0f, y + 8f, view.width, 32f), "No saved configurations.", TextAnchor.MiddleCenter);
                y += 48f;
            }
            else
            {
                foreach (SettingsProfileInfo profile in profiles)
                {
                    Rect row = new Rect(0f, y, view.width, 58f);
                    Widgets.DrawHighlightIfMouseover(row);
                    Widgets.Label(new Rect(8f, y + 6f, view.width - 218f, 24f), profile.Name);
                    DrawMutedLabel(new Rect(8f, y + 30f, view.width - 218f, 22f), "Saved " + profile.Modified.ToString("g"));
                    if (Widgets.ButtonText(new Rect(view.width - 200f, y + 13f, 88f, 30f), "Load")) ConfirmLoadProfile(profile, settings);
                    if (Widgets.ButtonText(new Rect(view.width - 102f, y + 13f, 88f, 30f), "Delete")) ConfirmDeleteProfile(profile);
                    y += 62f;
                }
            }

            DrawSectionHeader(view, ref y, "Plant Mask Files");
            if (Widgets.ButtonText(new Rect(0f, y, 176f, 32f), "Export Masks")) Find.WindowStack.Add(new Dialog_ExportPlantMasks(settings));
            if (Widgets.ButtonText(new Rect(186f, y, 126f, 32f), "Open Folder"))
            {
                if (!PlantMaskFileManager.OpenDirectory(out string error)) Messages.Message("Could not open the plant mask folder: " + error, MessageTypeDefOf.RejectInput, false);
            }
            if (Widgets.ButtonText(new Rect(322f, y, 100f, 32f), "Refresh")) PlantMaskFileManager.Refresh();
            if (Widgets.ButtonText(new Rect(432f, y, 230f, 32f), "Generate Missing Auto-Masks"))
            {
                AutoMaskBatchResult result = PlantAutoMaskCache.GenerateMissing(false);
                Messages.Message("Auto masks: " + result.generated + " generated, " + result.reused + " cached, "
                    + result.manualSkipped + " manual skipped, " + result.lowConfidence + " flagged for review, " + result.failed + " failed.",
                    result.failed > 0 ? MessageTypeDefOf.CautionInput : MessageTypeDefOf.TaskCompletion, false);
            }
            y += 38f;
            DrawMutedLabel(new Rect(0f, y, view.width, 38f), PlantMaskFileManager.DirectoryPath);
            y += 46f;
            if (maskFiles.Count == 0)
            {
                DrawMutedLabel(new Rect(0f, y + 8f, view.width, 32f), "No plant mask files found.", TextAnchor.MiddleCenter);
                y += 48f;
            }
            else
            {
                foreach (PlantMaskFileInfo file in maskFiles)
                {
                    Rect row = new Rect(0f, y, view.width, 58f);
                    Widgets.DrawHighlightIfMouseover(row);
                    Widgets.Label(new Rect(8f, y + 6f, view.width - 218f, 24f), file.Name + ".xml");
                    DrawMutedLabel(new Rect(8f, y + 30f, view.width - 218f, 22f), "Modified " + file.Modified.ToString("g"));
                    if (Widgets.ButtonText(new Rect(view.width - 200f, y + 13f, 88f, 30f), "Import")) ConfirmImportMasks(file, settings);
                    if (Widgets.ButtonText(new Rect(view.width - 102f, y + 13f, 88f, 30f), "Delete")) ConfirmDeleteMasks(file);
                    y += 62f;
                }
            }
            EndPage();
        }
        private static void ConfirmLoadProfile(SettingsProfileInfo profile, NovelSeedsSettings settings)
        {
            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("Load configuration '" + profile.Name + "'?", delegate
            {
                if (SettingsProfileManager.Load(profile, settings, out string error))
                {
                    contentScroll = Vector2.zero;
                    Messages.Message("Configuration '" + profile.Name + "' loaded.", MessageTypeDefOf.TaskCompletion, false);
                }
                else Messages.Message("Could not load configuration: " + error, MessageTypeDefOf.RejectInput, false);
            }, true));
        }

        private static void ConfirmExportPublisherDefault(NovelSeedsSettings settings)
        {
            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("Export all current settings and masks as the publisher default? This replaces the previous export. Copy DefaultConfiguration.xml into 1.6/Defaults before packaging or uploading the mod.", delegate
            {
                if (SettingsProfileManager.ExportPublisherDefault(settings, out string error)) Messages.Message("Publisher default exported to " + SettingsProfileManager.PublisherExportPath, MessageTypeDefOf.TaskCompletion, false);
                else Messages.Message("Could not export the publisher default: " + error, MessageTypeDefOf.RejectInput, false);
            }, true));
        }

        private static void ApplyDefaults(NovelSeedsSettings settings)
        {
            if (SettingsProfileManager.ApplyDefault(settings, out bool usedBundledDefault, out string error))
            {
                contentScroll = Vector2.zero;
                Messages.Message(usedBundledDefault ? "Bundled mod default loaded." : "Built-in XML defaults loaded.", MessageTypeDefOf.TaskCompletion, false);
            }
            else Messages.Message("Could not load the default configuration: " + error, MessageTypeDefOf.RejectInput, false);
        }

        private static void ConfirmDeleteProfile(SettingsProfileInfo profile)
        {
            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("Delete saved configuration '" + profile.Name + "'?", delegate
            {
                if (!SettingsProfileManager.Delete(profile, out string error)) Messages.Message("Could not delete configuration: " + error, MessageTypeDefOf.RejectInput, false);
            }, true));
        }
        private static void ConfirmImportMasks(PlantMaskFileInfo file, NovelSeedsSettings settings)
        {
            Find.WindowStack.Add(new Dialog_ImportPlantMasks(file, settings));
        }

        private static void ConfirmDeleteMasks(PlantMaskFileInfo file)
        {
            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("Delete plant mask file '" + file.Name + ".xml'?", delegate
            {
                if (!PlantMaskFileManager.Delete(file, out string error)) Messages.Message("Could not delete plant mask file: " + error, MessageTypeDefOf.RejectInput, false);
            }, true));
        }
        private static void DrawGlobalTraitGroups(Rect view, ref float y, List<VarietyTraitDef> traits, NovelSeedsSettings settings)
        {
            foreach (IGrouping<string, VarietyTraitDef> group in traits.GroupBy(settings.TraitGroup).OrderBy(group => group.Key))
            {
                bool open = DrawCategoryHeader(view, ref y, group.Key, group.Count(), OpenGlobalCategories, globalTraitSearch);
                if (!open) continue;
                foreach (VarietyTraitDef trait in group.OrderBy(trait => trait.label))
                {
                    GlobalTraitSettingsRecord record = settings.GetGlobalTraitSettings(trait);
                    bool expanded = expandedGlobalTrait == trait.defName;
                    Rect row = new Rect(0f, y, view.width, TraitRowHeight);
                    DrawTraitBaseRow(row, trait, "Weight " + record.weight.ToString("0.##"), expanded, delegate { expandedGlobalTrait = expanded ? null : trait.defName; });
                    bool hasSubtypes = HasSubtypes(trait);
                    FamilySettingsRecord family = hasSubtypes ? settings.GetFamilySettings(trait.configFamily) : null;
                    bool visualCustomized = hasSubtypes && family.useTypeSpecificVisuals ? family.HasCustomizedSubtypeVisual : record.visualCustomized;
                    if (Widgets.ButtonText(new Rect(view.width - 118f, y + 9f, 76f, 28f), visualCustomized ? "Visual *" : "Visual"))
                    {
                        if (hasSubtypes) Find.WindowStack.Add(new Dialog_TraitFamilyOptions(trait, FamilyOptionMode.Types));
                        else Find.WindowStack.Add(new Dialog_TraitVisualDesigner(settings, trait));
                    }
                    y += TraitRowHeight;
                    if (expanded)
                    {
                        float detailHeight = HasFamilyOptions(trait) ? ExpandedGlobalHeight : 124f;
                        Rect details = new Rect(0f, y, view.width, detailHeight);
                        Widgets.DrawBoxSolid(details, ExpandedBand);
                        Widgets.Label(new Rect(12f, y + 8f, 120f, 24f), "Trait Weight");
                        record.weight = Widgets.HorizontalSlider(new Rect(126f, y + 10f, view.width - 230f, 20f), record.weight, 0f, 10f);
                        if (Widgets.ButtonText(new Rect(view.width - 92f, y + 5f, 80f, 28f), "Reset")) record.weight = NovelSeedsSettings.DefaultWeight(trait);
                        IReadOnlyList<string> tags = settings.TraitTags(trait);
                        string tagSummary = tags.Count == 0 ? "No tags" : string.Join(", ", tags.ToArray());
                        Widgets.Label(new Rect(12f, y + 48f, view.width - 126f, 24f), "Tags: " + tagSummary);
                        if (Widgets.ButtonText(new Rect(view.width - 104f, y + 43f, 92f, 28f), "Edit Tags")) Find.WindowStack.Add(new Dialog_TraitTags(settings, trait));
                        Rect tagEffectRect = new Rect(12f, y + 82f, 220f, 28f);
                        Widgets.CheckboxLabeled(tagEffectRect, "Tag-Exclusive Effect", ref record.tagExclusive);
                        TooltipHandler.TipRegion(tagEffectRect, "Restricts this trait's produce effect to plants with one of the selected tags. It does not restrict which plants can gain the trait.");
                        if (record.tagExclusive)
                        {
                            string exclusiveSummary = record.ExclusiveTags.Count == 0 ? "No tags selected" : record.ExclusiveTags.Count + " selected";
                            DrawMutedLabel(new Rect(236f, y + 86f, view.width - 348f, 24f), exclusiveSummary);
                            if (Widgets.ButtonText(new Rect(view.width - 104f, y + 81f, 92f, 28f), "Tags")) Find.WindowStack.Add(new Dialog_TraitExclusiveTags(settings, trait));
                        }
                        if (HasFamilyOptions(trait)) DrawFamilyActions(new Rect(12f, y + 124f, view.width - 24f, 30f), trait);

                        y += detailHeight;
                    }
                }
            }
        }

        private static void DrawWildTraitGroups(Rect view, ref float y, List<VarietyTraitDef> traits, NovelSeedsSettings settings)
        {
            foreach (IGrouping<string, VarietyTraitDef> group in traits.GroupBy(settings.TraitGroup).OrderBy(group => group.Key))
            {
                bool open = DrawCategoryHeader(view, ref y, group.Key, group.Count(), OpenWildCategories, wildTraitSearch);
                if (!open) continue;
                foreach (VarietyTraitDef trait in group.OrderBy(trait => trait.label))
                {
                    WildTraitSettingsRecord record = settings.GetWildTraitSettings(trait);
                    bool expanded = expandedWildTrait == trait.defName;
                    Rect row = new Rect(0f, y, view.width, TraitRowHeight);
                    Widgets.DrawHighlightIfMouseover(row);
                    if (!trait.description.NullOrEmpty()) TooltipHandler.TipRegion(row, TraitColorUI.Tooltip(trait));
                    Widgets.CheckboxLabeled(new Rect(8f, y + 10f, view.width - 250f, 28f), ConfigTraitLabel(trait), ref record.enabled);
                    Widgets.Label(new Rect(view.width - 214f, y + 12f, 130f, 24f), "Weight " + record.weight.ToString("0.##"));
                    DrawExpandButton(new Rect(view.width - 36f, y + 9f, 28f, 28f), expanded, delegate { expandedWildTrait = expanded ? null : trait.defName; });
                    y += TraitRowHeight;
                    if (expanded)
                    {
                        float detailHeight = HasFamilyOptions(trait) ? ExpandedWildHeight : 42f;
                        Rect details = new Rect(0f, y, view.width, detailHeight);
                        Widgets.DrawBoxSolid(details, ExpandedBand);
                        Widgets.Label(new Rect(12f, y + 8f, 110f, 24f), "Wild Weight");
                        record.weight = Widgets.HorizontalSlider(new Rect(120f, y + 10f, view.width - 224f, 20f), record.weight, 0f, 10f);
                        if (Widgets.ButtonText(new Rect(view.width - 92f, y + 5f, 80f, 28f), "Reset")) record.weight = NovelSeedsSettings.DefaultWeight(trait);
                        if (HasFamilyOptions(trait)) DrawFamilyActions(new Rect(12f, y + 38f, view.width - 24f, 28f), trait);
                        y += detailHeight;
                    }
                }
            }
        }

        private static void DrawPlantTraitGroups(Rect view, ref float y, List<VarietyTraitDef> traits, NovelSeedsSettings settings, PlantSettingsRecord plant, ThingDef plantDef, PlantGroupRecord plantGroup = null)
        {
            foreach (IGrouping<string, VarietyTraitDef> group in traits.GroupBy(settings.TraitGroup).OrderBy(group => group.Key))
            {
                bool open = DrawCategoryHeader(view, ref y, group.Key, group.Count(), OpenPlantCategories, plantTraitSearch);
                if (!open) continue;
                foreach (VarietyTraitDef trait in group.OrderBy(trait => trait.label))
                {
                    TraitSettingsRecord record = plant.GetTraitSettings(trait);
                    float globalWeight = settings.GlobalTraitWeight(trait);
                    bool expanded = expandedPlantTrait == trait.defName;
                    Rect row = new Rect(0f, y, view.width, TraitRowHeight);
                    Widgets.DrawHighlightIfMouseover(row);
                    if (!trait.description.NullOrEmpty()) TooltipHandler.TipRegion(row, TraitColorUI.Tooltip(trait));
                    Widgets.CheckboxLabeled(new Rect(8f, y + 10f, view.width - 300f, 28f), ConfigTraitLabel(trait), ref record.enabled);
                    Widgets.Label(new Rect(view.width - 264f, y + 12f, 120f, 24f), record.useCustomWeight ? "Weight " + record.weight.ToString("0.##") : "Global " + globalWeight.ToString("0.##"));
                    Rect visualActionRect = new Rect(view.width - 142f, y + 9f, 96f, 28f);
                    if (record.useCustomVisual)
                    {
                        if (Widgets.ButtonText(visualActionRect, "Visual *"))
                        {
                            if (HasSubtypes(trait)) Find.WindowStack.Add(new Dialog_TraitFamilyOptions(trait, FamilyOptionMode.Types, plantDef));
                            else Find.WindowStack.Add(plantGroup == null ? new Dialog_TraitVisualDesigner(settings, trait, plantDef) : new Dialog_TraitVisualDesigner(settings, trait, plantGroup));
                        }
                    }
                    else DrawMutedLabel(visualActionRect, "Global Visual", TextAnchor.MiddleCenter);
                    DrawExpandButton(new Rect(view.width - 36f, y + 9f, 28f, 28f), expanded, delegate { expandedPlantTrait = expanded ? null : trait.defName; });
                    y += TraitRowHeight;
                    if (expanded)
                    {
                        float detailHeight = HasFamilyOptions(trait) ? ExpandedPlantHeight : 84f;
                        Rect details = new Rect(0f, y, view.width, detailHeight);
                        Widgets.DrawBoxSolid(details, ExpandedBand);
                        Widgets.CheckboxLabeled(new Rect(12f, y + 8f, 170f, 28f), "Override Weight", ref record.useCustomWeight);
                        if (record.useCustomWeight)
                        {
                            record.weight = Widgets.HorizontalSlider(new Rect(188f, y + 12f, view.width - 292f, 20f), record.weight, 0f, 10f);
                            if (Widgets.ButtonText(new Rect(view.width - 92f, y + 7f, 80f, 28f), "Reset")) { record.useCustomWeight = false; record.weight = globalWeight; }
                        }
                        else DrawInheritedValue(new Rect(view.width - 260f, y + 8f, 248f, 28f), "Using global weight " + globalWeight.ToString("0.##"));
                        bool usedCustomVisual = record.useCustomVisual;
                        string visualToggleLabel = plantGroup == null ? "Plant-Specific Visual" : "Group-Specific Visual";
                        Widgets.CheckboxLabeled(new Rect(12f, y + 48f, 184f, 28f), visualToggleLabel, ref record.useCustomVisual);
                        if (!usedCustomVisual && record.useCustomVisual) record.CopyVisualsFrom(settings.GlobalVisualCopies(trait));
                        DrawInheritedValue(new Rect(202f, y + 48f, view.width - 214f, 28f), record.useCustomVisual
                            ? (plantGroup == null ? "Using Plant-Specific Visual" : "Using Group-Specific Visual")
                            : "Using Global Visual");
                        if (HasFamilyOptions(trait)) DrawFamilyActions(new Rect(12f, y + 88f, view.width - 24f, 30f), trait, plantDef);

                        y += detailHeight;
                    }
                }
            }
        }
        private static void DrawTraitBaseRow(Rect row, VarietyTraitDef trait, string value, bool expanded, System.Action toggle)
        {
            Widgets.DrawHighlightIfMouseover(row);
            if (!trait.description.NullOrEmpty()) TooltipHandler.TipRegion(row, TraitColorUI.Tooltip(trait));
            Widgets.Label(new Rect(row.x + 8f, row.y + 12f, row.width - 330f, 24f), ConfigTraitLabel(trait));
            Widgets.Label(new Rect(row.xMax - 270f, row.y + 12f, 140f, 24f), value);
            DrawExpandButton(new Rect(row.xMax - 36f, row.y + 9f, 28f, 28f), expanded, toggle);
        }

        private static string ConfigTraitLabel(VarietyTraitDef trait)
        {
            return trait == null ? string.Empty : TraitColorUI.Label(trait) + (HasSubtypes(trait) ? " (subtype)" : string.Empty);
        }

        private static bool DrawCategoryHeader(Rect view, ref float y, string category, int count, HashSet<string> openCategories, string search)
        {
            bool forcedOpen = !search.NullOrEmpty();
            bool open = forcedOpen || openCategories.Contains(category);
            Rect row = new Rect(0f, y + 2f, view.width, CategoryRowHeight - 4f);
            Widgets.DrawBoxSolid(row, SectionBand);
            Widgets.DrawBoxSolid(new Rect(row.x, row.y, 3f, row.height), open ? Accent : new Color(0.34f, 0.36f, 0.38f));
            Widgets.Label(new Rect(row.x + 12f, row.y + 8f, row.width - 90f, 24f), (open ? "-  " : "+  ") + category);
            DrawMutedLabel(new Rect(row.xMax - 70f, row.y + 8f, 56f, 24f), count.ToString(), TextAnchor.UpperRight);
            if (!forcedOpen && Widgets.ButtonInvisible(row))
            {
                if (open) openCategories.Remove(category); else openCategories.Add(category);
                open = !open;
            }
            y += CategoryRowHeight;
            return open;
        }

        private static void DrawTraitListToolbar(Rect view, ref float y, string title, ref string search, HashSet<string> openCategories, List<string> categories)
        {
            DrawSectionHeader(view, ref y, title);
            Widgets.Label(new Rect(0f, y + 5f, 52f, 24f), "Search");
            search = DrawSearchField(new Rect(56f, y, Mathf.Max(150f, view.width - 274f), 30f), search, "Trait search");
            if (Widgets.ButtonText(new Rect(view.width - 208f, y, 96f, 30f), "Expand All")) foreach (string category in categories) openCategories.Add(category);
            if (Widgets.ButtonText(new Rect(view.width - 104f, y, 96f, 30f), "Collapse All")) openCategories.Clear();
            y += 40f;
        }

        private static string DrawSearchField(Rect rect, string value, string tooltip)
        {
            string result = Widgets.TextField(new Rect(rect.x, rect.y, rect.width - 34f, rect.height), value ?? string.Empty);
            TooltipHandler.TipRegion(rect, tooltip);
            if (!result.NullOrEmpty() && Widgets.ButtonText(new Rect(rect.xMax - 30f, rect.y, 30f, rect.height), "X")) result = string.Empty;
            return result;
        }

        private static void DrawFamilyActions(Rect rect, VarietyTraitDef trait, ThingDef previewPlant = null)
        {
            if (trait.configFamily.NullOrEmpty() || !trait.configRoot) return;
            DrawMutedLabel(new Rect(rect.x, rect.y + 4f, 112f, 24f), "Subtype Options");
            if (trait.configFamily == "Synergy")
            {
                if (Widgets.ButtonText(new Rect(rect.x + 118f, rect.y, 104f, rect.height), "Plants")) Find.WindowStack.Add(new Dialog_TraitFamilyOptions(trait, FamilyOptionMode.Plants, previewPlant));
                if (Widgets.ButtonText(new Rect(rect.x + 230f, rect.y, 104f, rect.height), "Stats")) Find.WindowStack.Add(new Dialog_TraitFamilyOptions(trait, FamilyOptionMode.Stats, previewPlant));
            }
            else if (Widgets.ButtonText(new Rect(rect.x + 118f, rect.y, 104f, rect.height), "Subtypes")) Find.WindowStack.Add(new Dialog_TraitFamilyOptions(trait, FamilyOptionMode.Types, previewPlant));
        }

        private static void DrawExpandButton(Rect rect, bool expanded, System.Action action)
        {
            if (Widgets.ButtonText(rect, expanded ? "-" : "+")) action();
            TooltipHandler.TipRegion(rect, expanded ? "Hide details" : "Show details");
        }

        private static void DrawPercentControl(Rect rect, string label, ref float value, float min, float max, bool compact = false)
        {
            Widgets.Label(new Rect(rect.x, rect.y, rect.width - 80f, 24f), label);
            DrawMutedLabel(new Rect(rect.xMax - 78f, rect.y, 76f, 24f), value.ToStringPercent(), TextAnchor.UpperRight);
            value = Widgets.HorizontalSlider(new Rect(rect.x, rect.y + (compact ? 24f : 28f), rect.width, 18f), value, min, max);
        }

        private static void DrawStepper(Rect rect, string label, ref int value, int min, int max)
        {
            float labelHeight = Mathf.Max(24f, Text.CalcHeight(label, rect.width));
            Widgets.Label(new Rect(rect.x, rect.y, rect.width, labelHeight), label);
            float controlsY = rect.y + labelHeight + 4f;
            if (Widgets.ButtonText(new Rect(rect.xMax - 108f, controlsY, 30f, 28f), "-")) value--;
            TextAnchor old = Text.Anchor;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(new Rect(rect.xMax - 74f, controlsY, 36f, 28f), value.ToString());
            Text.Anchor = old;
            if (Widgets.ButtonText(new Rect(rect.xMax - 34f, controlsY, 30f, 28f), "+")) value++;
            value = Mathf.Clamp(value, min, max);
        }
        private static void DrawPageTitle(Rect view, ref float y, string title, string subtitle)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, y, view.width, 30f), title);
            Text.Font = GameFont.Small;
            DrawMutedLabel(new Rect(0f, y + 30f, view.width, 24f), subtitle);
            y += 64f;
        }

        private static void DrawSectionHeader(Rect view, ref float y, string title)
        {
            y += 4f;
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, y, view.width, 30f), title);
            Text.Font = GameFont.Small;
            Widgets.DrawLineHorizontal(0f, y + 30f, view.width);
            y += 40f;
        }

        private static void DrawSmallHeader(Rect rect, string title)
        {
            Color old = GUI.color;
            GUI.color = new Color(0.72f, 0.72f, 0.72f);
            Widgets.Label(rect, title.ToUpperInvariant());
            GUI.color = old;
        }

        private static void DrawInheritedValue(Rect rect, string text)
        {
            DrawMutedLabel(rect, text, TextAnchor.UpperRight);
        }

        private static void DrawMutedLabel(Rect rect, string text, TextAnchor anchor = TextAnchor.UpperLeft)
        {
            Color oldColor = GUI.color;
            TextAnchor oldAnchor = Text.Anchor;
            GUI.color = new Color(0.70f, 0.72f, 0.73f);
            Text.Anchor = anchor;
            Widgets.Label(rect, text);
            Text.Anchor = oldAnchor;
            GUI.color = oldColor;
        }

        private static void BeginPage(Rect rect, float contentHeight, out Rect view)
        {
            view = new Rect(0f, 0f, rect.width - 16f, Mathf.Max(rect.height, contentHeight));
            Widgets.BeginScrollView(rect, ref contentScroll, view);
        }

        private static void EndPage()
        {
            Widgets.EndScrollView();
        }
        private static float TraitGroupsHeight(List<VarietyTraitDef> traits, NovelSeedsSettings settings, HashSet<string> openCategories, string expandedTrait, float familyExpandedHeight, float simpleExpandedHeight, string search)
        {
            float height = 0f;
            foreach (IGrouping<string, VarietyTraitDef> group in traits.GroupBy(settings.TraitGroup))
            {
                height += CategoryRowHeight;
                if (!search.NullOrEmpty() || openCategories.Contains(group.Key))
                {
                    height += group.Count() * TraitRowHeight;
                    if (!expandedTrait.NullOrEmpty())
                    {
                        VarietyTraitDef expanded = group.FirstOrDefault(trait => trait.defName == expandedTrait);
                        if (expanded != null) height += HasFamilyOptions(expanded) ? familyExpandedHeight : simpleExpandedHeight;
                    }
                }
            }
            return height + 24f;
        }

        private static bool HasFamilyOptions(VarietyTraitDef trait)
        {
            if (trait != null && (ColorTraitFactory.IsColorFamily(trait.configFamily) || trait.configFamily == PercentageTraitFactory.NutritiousFamily)) return false;
            return trait != null && !trait.configFamily.NullOrEmpty() && trait.configRoot;
        }

        private static bool HasSubtypes(VarietyTraitDef trait)
        {
            return HasFamilyOptions(trait) && TraitConfigUtility.Types(trait.configFamily).Count > 0;
        }

        private static List<VarietyTraitDef> FilterTraits(List<VarietyTraitDef> traits, string search, NovelSeedsSettings settings)
        {
            if (search.NullOrEmpty()) return traits;
            string query = search.Trim().ToLowerInvariant();
            return traits.Where(trait => (trait.label ?? string.Empty).ToLowerInvariant().Contains(query)
                || trait.defName.ToLowerInvariant().Contains(query)
                || (trait.description ?? string.Empty).ToLowerInvariant().Contains(query)
                || settings.TraitGroup(trait).ToLowerInvariant().Contains(query)).ToList();
        }

        private static bool MatchesSearch(string label, string defName, string search)
        {
            if (search.NullOrEmpty()) return true;
            string query = search.Trim().ToLowerInvariant();
            return (label ?? string.Empty).ToLowerInvariant().Contains(query) || (defName ?? string.Empty).ToLowerInvariant().Contains(query);
        }

        private static List<ThingDef> GrowablePlants()
        {
            return DefDatabase<ThingDef>.AllDefsListForReading.Where(NovelSeedUtility.IsGrowableCrop).OrderBy(def => def.label).ToList();
        }

        private static List<VarietyTraitDef> AllTraits()
        {
            return TraitConfigUtility.TopLevelTraits();
        }

        private enum SettingsPage
        {
            General,
            ProduceTraits,
            Wild,
            Profiles,
            Tags,
            Group,
            Plant
        }
    }
}
