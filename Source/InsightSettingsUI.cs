using System;
using System.Collections.Generic;
using System.Linq;
using InsightCanvas;
using RimWorld;
using UnityEngine;
using Verse;

namespace HorticultureNovelSeeds
{
    /// <summary>
    /// Presentation document for the Horticulture settings surface.
    ///
    /// NovelSeedsSettings remains the only owner of persistent gameplay data. This document
    /// owns page, search, selection, disclosure, responsive, feedback, and accessibility state
    /// through one InsightUiDocument, so opening two settings surfaces cannot share UI state.
    /// </summary>
    public sealed class InsightSettingsDocument
    {
        private readonly NovelSeedsSettings settings;
        private readonly Action settingsChanged;
        private readonly InsightUiDocument uiDocument;
        private readonly InsightUiHost uiHost;
        private readonly InsightUiNavigation navigation;
        private readonly InsightUiTabs workspaceTabs;

        private InsightUiVirtualList groupList;
        private InsightUiVirtualList groupMemberList;
        private InsightUiVirtualList plantList;
        private InsightUiVirtualList plantGroupList;
        private InsightUiVirtualList traitList;
        private InsightUiVirtualList traitGroupList;
        private InsightUiVirtualList tagList;
        private InsightUiVirtualList profileList;

        private InsightUiSearchField groupSearchField;
        private InsightUiSearchField groupMemberSearchField;
        private InsightUiSearchField plantSearchField;
        private InsightUiSearchField traitSearchField;
        private InsightUiSearchField tagSearchField;
        private InsightUiSearchField profileSearchField;
        private InsightUiTextField groupNameField;
        private InsightUiTextField profileNameField;

        private readonly List<ThingDef> plants = new List<ThingDef>();
        private readonly List<ThingDef> filteredPlants = new List<ThingDef>();
        private readonly List<PlantGroupRecord> groups = new List<PlantGroupRecord>();
        private readonly List<PlantGroupRecord> filteredGroups = new List<PlantGroupRecord>();
        private readonly List<ThingDef> groupMembers = new List<ThingDef>();
        private readonly List<VarietyTraitDef> traits = new List<VarietyTraitDef>();
        private readonly List<VarietyTraitDef> filteredTraits = new List<VarietyTraitDef>();
        private readonly List<string> traitGroups = new List<string>();
        private readonly List<string> tags = new List<string>();
        private readonly List<SettingsProfileInfo> profiles = new List<SettingsProfileInfo>();
        private readonly List<SettingsProfileInfo> filteredProfiles = new List<SettingsProfileInfo>();
        private readonly List<PlantUiSummary> plantSummaries = new List<PlantUiSummary>();
        private readonly List<PlantUiSummary> filteredPlantSummaries = new List<PlantUiSummary>();
        private readonly List<GroupUiSummary> groupSummaries = new List<GroupUiSummary>();
        private readonly List<GroupUiSummary> filteredGroupSummaries = new List<GroupUiSummary>();
        private readonly List<TraitUiSummary> traitSummaries = new List<TraitUiSummary>();
        private readonly List<TraitUiSummary> filteredTraitSummaries = new List<TraitUiSummary>();
        private readonly List<ProfileUiSummary> profileSummaries = new List<ProfileUiSummary>();
        private readonly List<ProfileUiSummary> filteredProfileSummaries = new List<ProfileUiSummary>();

        private string activePageId = "gameplay";
        private string activeWorkspaceTab = "groups";
        private string groupSearch = string.Empty;
        private string groupMemberSearch = string.Empty;
        private string plantSearch = string.Empty;
        private string traitSearch = string.Empty;
        private string tagSearch = string.Empty;
        private string profileSearch = string.Empty;
        private string groupName = string.Empty;
        private string profileName = string.Empty;
        private ThingDef selectedPlant;
        private PlantGroupRecord selectedGroup;
        private VarietyTraitDef selectedTrait;
        private SettingsProfileInfo selectedProfile;
        private bool snapshotDirty = true;
        private bool normalizationPending;
        private bool highContrast;
        private bool reducedMotion;
        private int densityIndex = 1;

        public InsightSettingsDocument(NovelSeedsSettings settings, Action settingsChanged = null)
        {
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.settingsChanged = settingsChanged;

            navigation = InsightUi.Navigation("hns.navigation", 720f);
            navigation.Bind(() => activePageId, value =>
            {
                if (string.IsNullOrEmpty(value)) return;
                activePageId = value;
                uiDocument?.Invalidate();
            });

            workspaceTabs = InsightUi.Tabs("hns.workspace.tabs");
            workspaceTabs.Bind(() => activeWorkspaceTab, value =>
            {
                if (string.IsNullOrEmpty(value)) return;
                activeWorkspaceTab = value;
                uiDocument?.Invalidate();
            });

            InsightUiElement root = BuildRoot();
            uiDocument = new InsightUiDocument("hns.settings.document", root)
            {
                Theme = CreateBotanicalTheme(),
                Density = InsightUiDensity.Normal,
                TrackDuplicateIds = true,
                DrawBackground = true,
                HighContrast = highContrast,
                ReducedMotion = reducedMotion
            };
            uiHost = new InsightUiHost(uiDocument);
        }

        public NovelSeedsSettings Settings => settings;
        public string ActivePageId => activePageId;
        public int NavigationPageCount => navigation.Pages.Count;
        public bool TrackDuplicateIds => uiDocument.TrackDuplicateIds;
        public int DuplicateIdCount => uiDocument.Diagnostics.DuplicateIds;
        public int RenderErrorCount => uiDocument.Diagnostics.RenderErrors;
        public int PlantVirtualizationCacheLimit => plantList?.CacheLimit ?? 0;
        public int TraitVirtualizationCacheLimit => traitList?.CacheLimit ?? 0;
        public bool HasIsolatedPresentationState(InsightSettingsDocument other)
        {
            return other != null && !ReferenceEquals(uiDocument.State, other.uiDocument.State)
                && !ReferenceEquals(uiDocument.Focus, other.uiDocument.Focus)
                && !ReferenceEquals(uiDocument.Toasts, other.uiDocument.Toasts);
        }

        /// <summary>Returns whether the currently composed non-virtual tree has unique raw IDs.</summary>
        public bool HasUniqueComponentIds()
        {
            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            return CountDuplicateIds(uiDocument.Root, ids) == 0;
        }

        public void Draw(Rect rect)
        {
            RefreshSnapshots();
            ApplyResponsiveLayout(rect.width);
            uiHost.Draw(rect, Time.deltaTime);
        }

        public void PostClose()
        {
            uiHost.PostClose();
        }

        private InsightUiElement BuildRoot()
        {
            navigation.Add("gameplay", "Gameplay", BuildGameplayPage());
            navigation.Add("workspace", "Plants & Traits", BuildWorkspacePage());
            navigation.Add("visuals", "Visuals", BuildVisualsPage());
            navigation.Add("profiles", "Profiles", BuildProfilesPage());
            navigation.Add("advanced", "Advanced", BuildAdvancedPage());

            InsightUiStack root = InsightUi.Column("hns.root", navigation, InsightUi.Toast("hns.toast"));
            root.Style.Gap = 8f;
            root.Style.Padding = InsightUiPadding.All(4f);
            return root;
        }

        private InsightUiElement BuildGameplayPage()
        {
            InsightUiElement introduction = InsightUi.Callout("gameplay.introduction", InsightUiCalloutSeverity.Info,
                "Botanical mutation controls", "These controls affect novel seed generation, inheritance, balancing, and produce visuals. Changes are normalized using the existing settings rules.");

            InsightUiGrid grid = InsightUi.Grid("gameplay.controls", 285f);
            grid.Add(
                FloatControl("gameplay.mutation", "Mutation chance", () => settings.globalMutationChance,
                    value => settings.globalMutationChance = value, 0f, 1f,
                    "Chance that a mature plant creates a novel seed."),
                FloatControl("gameplay.cross", "Cross-pollination chance", () => settings.globalCrossPollinationChance,
                    value => settings.globalCrossPollinationChance = value, 0f, 1f,
                    "Chance that compatible nearby plants contribute traits."),
                FloatControl("gameplay.wild", "Wild variation chance", () => settings.wildMutationChance,
                    value => settings.wildMutationChance = value, 0f, 1f,
                    "Chance that an eligible wild plant starts a novel lineage."),
                FloatControl("gameplay.donor", "Minimum donor growth", () => settings.minimumDonorGrowth,
                    value => settings.minimumDonorGrowth = value, 0f, 1f,
                    "Minimum growth required before a plant can donate cross-pollination traits."),
                FloatControl("gameplay.second-slot", "Second cross-pollination trait", () => settings.secondCrossPollinationTraitChance,
                    value => settings.secondCrossPollinationTraitChance = value, 0f, 1f,
                    "Chance of filling the second inherited trait slot."),
                FloatControl("gameplay.later-slots", "Later cross-pollination traits", () => settings.laterCrossPollinationTraitChance,
                    value => settings.laterCrossPollinationTraitChance = value, 0f, 1f,
                    "Chance of filling later inherited trait slots."),
                IntControl("gameplay.max-cross", "Maximum cross-pollination traits", () => settings.maxCrossPollinationTraits,
                    value => settings.maxCrossPollinationTraits = value, 1, 10,
                    "Caps how many traits may be inherited from cross-pollination."),
                IntControl("gameplay.max-event", "Maximum traits per event", () => settings.maxTraitsPerEvent,
                    value => settings.maxTraitsPerEvent = value, 1, 10,
                    "Caps the number of traits generated by one event."),
                ToggleControl("gameplay.balance", "Enable trait balancing", () => settings.enableTraitBalancing,
                    value => settings.enableTraitBalancing = value,
                    "Keeps strong and detrimental traits within the configured balance envelope."),
                FloatControl("gameplay.balance-strength", "Balance strength", () => settings.traitBalanceStrength,
                    value => settings.traitBalanceStrength = value, 0f, 1f,
                    "How strongly balance values influence eligible trait choices."),
                IntControl("gameplay.allowed-imbalance", "Allowed trait imbalance", () => settings.allowedTraitImbalance,
                    value => settings.allowedTraitImbalance = value, 0, 10,
                    "Number of balance points an event may exceed before correction."),
                FloatControl("gameplay.exceptional", "Exceptional variety chance", () => settings.exceptionalVarietyChance,
                    value => settings.exceptionalVarietyChance = value, 0f, 1f,
                    "Chance that a generated variety receives an exceptional roll."));

            InsightUiElement reset = Panel("gameplay.reset", InsightUi.SectionHeader("gameplay.reset.header", "Scoped resets",
                "Reset only the selected part of the gameplay model; profiles and serialized keys remain compatible."),
                InsightUi.Row("gameplay.reset.actions",
                    ActionButton("gameplay.reset.wild", "Reset wild settings", () => Confirm("Reset wild mutation and wild trait settings?", () =>
                    {
                        Mutate(() => settings.ResetWildSettings());
                    })),
                    ActionButton("gameplay.reset.weights", "Reset global weights", () => Confirm("Reset all customized global trait weights?", () =>
                    {
                        Mutate(() => settings.ResetGlobalWeights());
                    }))));

            InsightUiElement traitAndProduce = Panel("gameplay.traits-produce", InsightUi.SectionHeader("gameplay.traits.header",
                "Trait and produce behavior", "Use Plants & Traits for per-record enablement, weights, tags, groups, and visual overrides."),
                ToggleControl("gameplay.produce", "Enable produce visuals", () => settings.enableProduceVisuals,
                    value => settings.enableProduceVisuals = value,
                    "Applies inherited visual traits to harvested produce."),
                FloatControl("gameplay.palette-hue", "Allowed palette hue range", () => settings.allowedHueRangeDegrees,
                    value => settings.allowedHueRangeDegrees = value, 0f, 360f,
                    "Maximum hue distance used when generating a produce palette."));

            return Page("gameplay.page", "Gameplay", "Tune the mutation and inheritance model with direct authoritative bindings.",
                introduction, grid, reset, traitAndProduce);
        }

        private InsightUiElement BuildWorkspacePage()
        {
            groupSearchField = InsightUi.SearchField("workspace.groups.search", "", "Search groups")
                .Bind(() => groupSearch, value => SetSearch(ref groupSearch, value));
            groupMemberSearchField = InsightUi.SearchField("workspace.groups.members.search", "", "Search plants")
                .Bind(() => groupMemberSearch, value => SetSearch(ref groupMemberSearch, value));
            plantSearchField = InsightUi.SearchField("workspace.plants.search", "", "Search plants")
                .Bind(() => plantSearch, value => SetSearch(ref plantSearch, value));
            traitSearchField = InsightUi.SearchField("workspace.traits.search", "", "Search traits")
                .Bind(() => traitSearch, value => SetSearch(ref traitSearch, value));

            groupList = InsightUi.VirtualList("workspace.groups.list", 0, 34f, GroupListItem);
            groupList.Overscan = 3;
            groupList.CacheLimit = 72;
            groupMemberList = InsightUi.VirtualList("workspace.groups.members.list", 0, 32f, GroupMemberListItem);
            groupMemberList.Overscan = 2;
            groupMemberList.CacheLimit = 96;
            plantList = InsightUi.VirtualList("workspace.plants.list", 0, 34f, PlantListItem);
            plantList.Overscan = 3;
            plantList.CacheLimit = 96;
            plantGroupList = InsightUi.VirtualList("workspace.plants.groups.list", 0, 30f, PlantGroupListItem);
            plantGroupList.Overscan = 2;
            plantGroupList.CacheLimit = 48;
            traitList = InsightUi.VirtualList("workspace.traits.list", 0, 34f, TraitListItem);
            traitList.Overscan = 3;
            traitList.CacheLimit = 96;
            traitGroupList = InsightUi.VirtualList("workspace.traits.groups.list", 0, 30f, TraitGroupListItem);
            traitGroupList.Overscan = 2;
            traitGroupList.CacheLimit = 48;

            workspaceTabs.Add("groups", "Groups", BuildGroupsWorkspace());
            workspaceTabs.Add("plants", "Plants", BuildPlantsWorkspace());
            workspaceTabs.Add("traits", "Traits", BuildTraitsWorkspace());

            return Page("workspace.page", "Plants & Traits", "Search bounded registries, select an authority, and edit only its persistent record.",
                InsightUi.Callout("workspace.guidance", InsightUiCalloutSeverity.Info, "Workspace model",
                    "Groups, plants, and traits are read-only summaries until a control writes through NovelSeedsSettings. Lists are virtualized and their queries belong to this document."),
                workspaceTabs);
        }

        private InsightUiElement BuildGroupsWorkspace()
        {
            InsightUiElement listPane = Panel("workspace.groups.list-pane", InsightUi.SectionHeader("workspace.groups.list.header", "Groups",
                "Reusable plant settings with explicit membership."), groupSearchField, groupList,
                ActionButton("workspace.groups.new", "Create group", () =>
                {
                    groupName = string.Empty;
                    selectedGroup = null;
                    uiDocument.Invalidate();
                }));
            InsightUiElement inspector = BuildGroupInspector();
            InsightUiSplit split = InsightUi.Split("workspace.groups.split", listPane, inspector, 0.38f);
            split.Draggable = true;
            split.Style.Flex = 1f;
            return split;
        }

        private InsightUiElement BuildGroupInspector()
        {
            groupNameField = InsightUi.TextField("workspace.groups.name", "")
                .Bind(() => groupName, value => groupName = value ?? string.Empty);
            groupNameField.Style.Flex = 1f;

            InsightUiElement groupActions = InsightUi.Row("workspace.groups.actions",
                ActionButton("workspace.groups.create", "Create", () =>
                {
                    if (selectedGroup != null) return;
                    PlantGroupRecord created = null;
                    Mutate(() => created = settings.CreatePlantGroup(groupName));
                    if (created == null)
                    {
                        uiDocument.Toasts.Show("Enter a unique group name.", InsightToastSeverity.Warning);
                        return;
                    }
                    selectedGroup = created;
                    groupName = created.Name;
                }),
                ActionButton("workspace.groups.rename", "Rename", () =>
                {
                    if (selectedGroup == null) return;
                    string value = groupName?.Trim();
                    if (value.NullOrEmpty()) return;
                    Mutate(() => settings.RenamePlantGroup(selectedGroup, value));
                }),
                ActionButton("workspace.groups.delete", "Delete", () =>
                {
                    if (selectedGroup == null) return;
                    PlantGroupRecord doomed = selectedGroup;
                    Confirm("Delete the '" + doomed.Name + "' group? Member plants will return to personal settings.", () =>
                    {
                        Mutate(() => settings.DeletePlantGroup(doomed));
                        if (ReferenceEquals(selectedGroup, doomed)) selectedGroup = null;
                        groupName = string.Empty;
                    });
                }));

            InsightUiElement groupMembership = Panel("workspace.groups.members", InsightUi.SectionHeader("workspace.groups.members.header",
                "Membership", "Assigning a plant here moves it from any other group."), groupMemberSearchField, groupMemberList);

            InsightUiElement groupOverrides = Panel("workspace.groups.overrides", InsightUi.SectionHeader("workspace.groups.overrides.header",
                "Group overrides", "These values are shared by every member unless a plant-specific record takes precedence."),
                FloatControl("workspace.groups.mutation", "Mutation override", () => SelectedGroupSettings()?.mutationChance ?? settings.globalMutationChance,
                    value => EnsureSelectedGroupSettings().mutationChance = value, 0f, 1f, "Shared mutation chance."),
                ToggleControl("workspace.groups.custom-mutation", "Use shared mutation override", () => SelectedGroupSettings()?.useCustomMutationChance == true,
                    value => EnsureSelectedGroupSettings().useCustomMutationChance = value, "Enable the shared mutation value."),
                FloatControl("workspace.groups.cross", "Cross-pollination override", () => SelectedGroupSettings()?.crossPollinationChance ?? settings.globalCrossPollinationChance,
                    value => EnsureSelectedGroupSettings().crossPollinationChance = value, 0f, 1f, "Shared cross-pollination chance."),
                ToggleControl("workspace.groups.custom-cross", "Use shared cross-pollination override", () => SelectedGroupSettings()?.useCustomCrossPollinationChance == true,
                    value => EnsureSelectedGroupSettings().useCustomCrossPollinationChance = value, "Enable the shared cross-pollination value."));

            InsightUiStack body = InsightUi.Column("workspace.groups.inspector", DynamicLabel("workspace.groups.title", () =>
                selectedGroup == null ? "Select a group" : selectedGroup.Name, InsightUiTextStyle.Heading),
                DynamicLabel("workspace.groups.count", () => selectedGroup == null ? "Create or select a group to inspect its members." : selectedGroup.PlantCount + " plants use this group."),
                InsightUi.Row("workspace.groups.name-row", InsightUi.Label("workspace.groups.name-label", "Group name", InsightUiTextStyle.Label), groupNameField),
                groupActions, groupMembership, groupOverrides);
            body.Style.Gap = 8f;
            return InsightUi.Scroll("workspace.groups.inspector.scroll", body);
        }

        private InsightUiElement BuildPlantsWorkspace()
        {
            InsightUiElement listPane = Panel("workspace.plants.list-pane", InsightUi.SectionHeader("workspace.plants.list.header", "Plants",
                "Growable plant definitions with bounded search."), plantSearchField, plantList);
            InsightUiElement inspector = BuildPlantInspector();
            InsightUiSplit split = InsightUi.Split("workspace.plants.split", listPane, inspector, 0.38f);
            split.Draggable = true;
            split.Style.Flex = 1f;
            return split;
        }

        private InsightUiElement BuildPlantInspector()
        {
            InsightUiElement basic = Panel("workspace.plants.identity", InsightUi.SectionHeader("workspace.plants.identity.header", "Plant identity",
                "The selected definition and its effective group are read-only summaries."),
                DynamicLabel("workspace.plants.identity.name", () => selectedPlant == null ? "Select a plant" : selectedPlant.LabelCap.ToString(), InsightUiTextStyle.Heading),
                DynamicLabel("workspace.plants.identity.def", () => selectedPlant == null ? "" : "Definition: " + selectedPlant.defName),
                DynamicLabel("workspace.plants.identity.group", () => selectedPlant == null ? "Group: —" : "Group: " + (settings.GroupForPlant(selectedPlant)?.Name ?? "Personal settings")),
                plantGroupList);

            InsightUiElement overrides = Panel("workspace.plants.overrides", InsightUi.SectionHeader("workspace.plants.overrides.header", "Plant overrides",
                "Controls write through the selected plant record; group settings remain the effective fallback."),
                ToggleControl("workspace.plants.custom-mutation", "Use custom mutation chance", () => SelectedPlantSettings(false)?.useCustomMutationChance == true,
                    value => EnsureSelectedPlantSettings().useCustomMutationChance = value, "Use a plant-specific mutation chance."),
                FloatControl("workspace.plants.mutation", "Mutation chance", () => SelectedPlantSettings(false)?.mutationChance ?? settings.globalMutationChance,
                    value => EnsureSelectedPlantSettings().mutationChance = value, 0f, 1f, "Plant-specific mutation chance."),
                ToggleControl("workspace.plants.custom-cross", "Use custom cross-pollination chance", () => SelectedPlantSettings(false)?.useCustomCrossPollinationChance == true,
                    value => EnsureSelectedPlantSettings().useCustomCrossPollinationChance = value, "Use a plant-specific cross-pollination chance."),
                FloatControl("workspace.plants.cross", "Cross-pollination chance", () => SelectedPlantSettings(false)?.crossPollinationChance ?? settings.globalCrossPollinationChance,
                    value => EnsureSelectedPlantSettings().crossPollinationChance = value, 0f, 1f, "Plant-specific cross-pollination chance."),
                ToggleControl("workspace.plants.plant-masks", "Use plant visual masks", () => SelectedPlantSettings(false)?.usePlantMasks == true,
                    value => EnsureSelectedPlantSettings().usePlantMasks = value, "Use the plant's serialized visual mask layers."),
                ToggleControl("workspace.plants.auto-masks", "Disable automatic plant masks", () => SelectedPlantSettings(false)?.disableAutoPlantMasks == true,
                    value => EnsureSelectedPlantSettings().disableAutoPlantMasks = value, "Keep automatic mask generation from filling missing layers."),
                ToggleControl("workspace.plants.produce-masks", "Use produce visual masks", () => SelectedPlantSettings(false)?.useProduceMasks == true,
                    value => EnsureSelectedPlantSettings().useProduceMasks = value, "Use serialized produce mask layers."));

            InsightUiElement actions = Panel("workspace.plants.actions", InsightUi.SectionHeader("workspace.plants.actions.header", "Plant actions",
                "Open focused editors only when you need detailed mask or tag work."),
                ActionButton("workspace.plants.tags", "Edit plant tags", OpenPlantTags),
                ActionButton("workspace.plants.export", "Export plant masks", OpenPlantMaskExport),
                ActionButton("workspace.plants.reset", "Reset plant overrides", () =>
                {
                    if (selectedPlant == null) return;
                    ThingDef plant = selectedPlant;
                    Confirm("Reset all saved overrides for " + plant.LabelCap + "?", () => Mutate(() => settings.ResetPlant(plant)));
                }));

            InsightUiStack body = InsightUi.Column("workspace.plants.inspector", basic, overrides, actions);
            body.Style.Gap = 8f;
            return InsightUi.Scroll("workspace.plants.inspector.scroll", body);
        }

        private InsightUiElement BuildTraitsWorkspace()
        {
            InsightUiElement listPane = Panel("workspace.traits.list-pane", InsightUi.SectionHeader("workspace.traits.list.header", "Traits",
                "Mechanical, cosmetic, and produce behavior share one bounded trait registry."), traitSearchField, traitList);
            InsightUiElement inspector = BuildTraitInspector();
            InsightUiSplit split = InsightUi.Split("workspace.traits.split", listPane, inspector, 0.38f);
            split.Draggable = true;
            split.Style.Flex = 1f;
            return split;
        }

        private InsightUiElement BuildTraitInspector()
        {
            InsightUiElement identity = Panel("workspace.traits.identity", InsightUi.SectionHeader("workspace.traits.identity.header", "Trait identity",
                "Category and mechanical/cosmetic classification are derived from the authoritative definition."),
                DynamicLabel("workspace.traits.name", () => selectedTrait == null ? "Select a trait" : selectedTrait.LabelCap.ToString(), InsightUiTextStyle.Heading),
                DynamicLabel("workspace.traits.def", () => selectedTrait == null ? "" : "Definition: " + selectedTrait.defName),
                DynamicLabel("workspace.traits.category", () => selectedTrait == null ? "Category: —" : "Category: " + settings.TraitGroup(selectedTrait)),
                DynamicLabel("workspace.traits.kind", () => selectedTrait == null ? "Kind: —" : "Kind: " + (selectedTrait.produceOnlyVisual ? "Cosmetic visual" : "Mechanical and visual")),
                DynamicLabel("workspace.traits.balance", () => selectedTrait == null ? "Balance: —" : "Balance value: " + selectedTrait.balanceValue.ToString("0.##")),
                DynamicLabel("workspace.traits.visual-state", () =>
                    selectedTrait == null ? "Visual state: —" : "Visual state: " + (SelectedGlobalTrait()?.visualCustomized == true ? "Customized" : "Inherited")
                        + (SelectedGlobalTrait()?.usePerMaskVisuals == true ? " / per-mask" : " / shared")),
                traitGroupList);

            InsightUiElement global = Panel("workspace.traits.global", InsightUi.SectionHeader("workspace.traits.global.header", "Global trait settings",
                "Global settings apply before optional plant and group overrides."),
                ToggleControl("workspace.traits.category-enabled", "Category enabled", () => SelectedTraitCategoryEnabled(),
                    value => SetSelectedTraitCategoryEnabled(value), "Enable or disable the selected trait's current category."),
                FloatControl("workspace.traits.weight", "Global weight", () => selectedTrait == null ? 1f : settings.GlobalTraitWeight(selectedTrait),
                    value => SetSelectedTraitWeight(value), 0f, 20f, "Base selection weight for the trait."),
                ToggleControl("workspace.traits.produce", "Apply to produce", () => SelectedGlobalTrait()?.applyTraitToProduce ?? (selectedTrait?.inheritToProduce == true),
                    value => EnsureSelectedGlobalTrait().SetProduceInheritance(value), "Allow the trait's visual/mechanical effect to reach harvested produce."),
                DynamicLabel("workspace.traits.tags", () => selectedTrait == null ? "Tags: —" : "Tags: " + string.Join(", ", settings.TraitTags(selectedTrait))),
                ActionButton("workspace.traits.edit-tags", "Edit trait tags", OpenTraitTags),
                ActionButton("workspace.traits.visual", "Open visual editor", OpenTraitVisual));

            InsightUiElement plantOverride = Panel("workspace.traits.plant-override", InsightUi.SectionHeader("workspace.traits.plant.header", "Selected plant override",
                "Select a plant in the Plants tab to edit its trait enablement and weight."),
                DynamicLabel("workspace.traits.plant.name", () => selectedPlant == null ? "No plant selected" : selectedPlant.LabelCap.ToString()),
                ToggleControl("workspace.traits.plant.enabled", "Plant trait enabled", () => SelectedPlantTraitSettings(false)?.enabled ?? true,
                    value => EnsureSelectedPlantTraitSettings().enabled = value, "Enable or disable this trait for the selected plant."),
                FloatControl("workspace.traits.plant.weight", "Plant trait weight", () => SelectedPlantTraitSettings(false)?.weight ?? settings.TraitWeight(selectedPlant, selectedTrait),
                    value => SetSelectedPlantTraitWeight(value), 0f, 20f, "Optional plant-specific trait weight."));

            InsightUiStack body = InsightUi.Column("workspace.traits.inspector", identity, global, plantOverride);
            body.Style.Gap = 8f;
            return InsightUi.Scroll("workspace.traits.inspector.scroll", body);
        }

        private InsightUiElement BuildVisualsPage()
        {
            InsightUiElement overview = Panel("visuals.overview", InsightUi.SectionHeader("visuals.overview.header", "High-level visual controls",
                "The visual pipeline keeps manual masks, generated masks, palettes, and trait visuals in their existing serialized authorities."),
                ToggleControl("visuals.produce", "Enable produce visuals", () => settings.enableProduceVisuals,
                    value => settings.enableProduceVisuals = value, "Allow inherited visual traits on harvested produce."),
                IntControl("visuals.palette-min", "Minimum palette size", () => settings.minimumPaletteSize,
                    value => settings.minimumPaletteSize = value, 1, 24, "Smallest generated palette."),
                IntControl("visuals.palette-max", "Maximum palette size", () => settings.maximumPaletteSize,
                    value => settings.maximumPaletteSize = value, 1, 24, "Largest generated palette."),
                FloatControl("visuals.saturation-min", "Minimum saturation", () => settings.minimumPaletteSaturation,
                    value => settings.minimumPaletteSaturation = value, 0f, 1f, "Lower saturation bound."),
                FloatControl("visuals.saturation-max", "Maximum saturation", () => settings.maximumPaletteSaturation,
                    value => settings.maximumPaletteSaturation = value, 0f, 1f, "Upper saturation bound."),
                FloatControl("visuals.value-min", "Minimum value", () => settings.minimumPaletteValue,
                    value => settings.minimumPaletteValue = value, 0f, 1f, "Lower brightness bound."),
                FloatControl("visuals.value-max", "Maximum value", () => settings.maximumPaletteValue,
                    value => settings.maximumPaletteValue = value, 0f, 1f, "Upper brightness bound."),
                FloatControl("visuals.hue-range", "Hue range", () => settings.allowedHueRangeDegrees,
                    value => settings.allowedHueRangeDegrees = value, 0f, 360f, "Allowed hue distance."));

            InsightUiElement tools = Panel("visuals.tools", InsightUi.SectionHeader("visuals.tools.header", "Visual tools",
                "Focused tools open as separate modal editors and preserve their existing behavior."),
                ActionButton("visuals.selected-plant", "Open selected plant editor", OpenPlantMaskEditor),
                ActionButton("visuals.generate-missing", "Generate Missing Auto-Masks", GenerateMissingAutoMasks),
                ActionButton("visuals.review", "Review Mask Queue", () => Find.WindowStack.Add(new Dialog_MaskReviewQueue())),
                ActionButton("visuals.export", "Export all plant masks", () => Find.WindowStack.Add(new Dialog_ExportPlantMasks(settings))),
                ActionButton("visuals.clear-cache", "Clear visual caches", () =>
                {
                    Confirm("Clear generated visual caches? Saved masks and settings will remain.", () => Mutate(() =>
                    {
                        settings.ClearVisualCache();
                        ProduceMaskRenderer.ClearAll();
                    }));
                }));

            return Page("visuals.page", "Visuals", "Tune palette generation and open focused visual tools without changing global skins.", overview, tools);
        }

        private InsightUiElement BuildProfilesPage()
        {
            profileSearchField = InsightUi.SearchField("profiles.search", "", "Search profiles")
                .Bind(() => profileSearch, value => SetSearch(ref profileSearch, value));
            profileList = InsightUi.VirtualList("profiles.list", 0, 34f, ProfileListItem);
            profileList.Overscan = 2;
            profileList.CacheLimit = 48;
            profileNameField = InsightUi.TextField("profiles.name", "")
                .Bind(() => profileName, value => profileName = value ?? string.Empty);

            InsightUiElement listPane = Panel("profiles.list-pane", InsightUi.SectionHeader("profiles.list.header", "Saved profiles",
                "Profiles are copied through the existing Scribe snapshot path."), profileSearchField, profileList,
                ActionButton("profiles.refresh", "Refresh", () =>
                {
                    SettingsProfileManager.Refresh();
                    snapshotDirty = true;
                    uiDocument.Invalidate();
                }));
            InsightUiElement editor = Panel("profiles.editor", InsightUi.SectionHeader("profiles.editor.header", "Profile card",
                "Save a named snapshot, apply it, update it, or remove it with confirmation."),
                DynamicLabel("profiles.selected", () => selectedProfile == null ? "No profile selected" : selectedProfile.Name, InsightUiTextStyle.Heading),
                DynamicLabel("profiles.modified", () => selectedProfile == null ? "" : "Modified: " + selectedProfile.Modified.ToString("g")),
                InsightUi.Row("profiles.name-row", InsightUi.Label("profiles.name-label", "Name", InsightUiTextStyle.Label), profileNameField),
                ActionButton("profiles.save", "Save new", SaveProfile),
                ActionButton("profiles.update", "Update selected", UpdateProfile),
                ActionButton("profiles.apply", "Apply selected", ApplyProfile),
                ActionButton("profiles.delete", "Delete selected", DeleteProfile),
                ActionButton("profiles.reset", "Reset active settings", ResetAllSettings),
                ActionButton("profiles.publisher", "Export publisher default", ExportPublisherDefault));
            InsightUiSplit split = InsightUi.Split("profiles.split", listPane, editor, 0.42f);
            split.Draggable = true;
            split.Style.Flex = 1f;
            return Page("profiles.page", "Profiles", "Keep named configurations separate from the live settings authority.",
                InsightUi.Callout("profiles.guidance", InsightUiCalloutSeverity.Info, "Safe configuration workflow",
                    "Apply and reset operations use document-local feedback and confirmation for destructive changes."), split);
        }

        private InsightUiElement BuildAdvancedPage()
        {
            tagSearchField = InsightUi.SearchField("advanced.tags.search", "", "Search configurable tags")
                .Bind(() => tagSearch, value => SetSearch(ref tagSearch, value));
            tagList = InsightUi.VirtualList("advanced.tags.list", 0, 34f, TagListItem);
            tagList.Overscan = 2;
            tagList.CacheLimit = 72;

            InsightUiElement accessibility = Panel("advanced.accessibility", InsightUi.SectionHeader("advanced.accessibility.header", "Accessibility and density",
                "These options are document-scoped and never mutate RimWorld's global GUI skin."),
                ToggleControl("advanced.high-contrast", "High contrast", () => highContrast, value =>
                {
                    highContrast = value;
                    uiDocument.HighContrast = value;
                    uiDocument.Invalidate();
                }, "Increase semantic contrast for this settings document."),
                ToggleControl("advanced.reduced-motion", "Reduced motion", () => reducedMotion, value =>
                {
                    reducedMotion = value;
                    uiDocument.ReducedMotion = value;
                    uiDocument.Invalidate();
                }, "Reduce document-local transition effects."),
                IntControl("advanced.density", "Density", () => densityIndex, value =>
                {
                    densityIndex = Mathf.Clamp(value, 0, 2);
                    uiDocument.Density = densityIndex == 0 ? InsightUiDensity.Comfortable : densityIndex == 2 ? InsightUiDensity.Compact : InsightUiDensity.Normal;
                    uiDocument.Invalidate();
                }, 0, 2, "Comfortable, normal, or compact control spacing."));

            InsightUiElement tagsPanel = Panel("advanced.tags", InsightUi.SectionHeader("advanced.tags.header", "Tags and overrides",
                "Plant tag membership remains in PlantTagUtility and its serialized overrides."), tagSearchField, tagList,
                ActionButton("advanced.open-tag-editor", "Open selected plant tags", OpenPlantTags),
                ActionButton("advanced.open-trait-tags", "Open selected trait tags", OpenTraitTags));

            InsightUiElement diagnostics = Panel("advanced.diagnostics", InsightUi.SectionHeader("advanced.diagnostics.header", "Diagnostics and compatibility",
                "The framework reports duplicate IDs and render errors without hiding them."),
                DynamicLabel("advanced.diagnostics.ids", () => "Duplicate IDs: " + uiDocument.Diagnostics.DuplicateIds),
                DynamicLabel("advanced.diagnostics.errors", () => "Render errors: " + uiDocument.Diagnostics.RenderErrors),
                DynamicLabel("advanced.diagnostics.virtualization", () => "Virtualized caches: plants " + (plantList?.CachedItemCount ?? 0) + ", traits " + (traitList?.CachedItemCount ?? 0)),
                InsightUi.Label("advanced.diagnostics.framework", "Insight Canvas 2.0.0 | commit 93a09005fa15190009daee625352cf4004974472 | DLL SHA-256 DFEC9DB76B6ABD7442E82A5029005CE09DECC281CC34FB37C080FD015458A613", InsightUiTextStyle.Caption),
                InsightUi.Label("advanced.diagnostics.compatibility", "Requires lan.insightcanvas. Missing dependencies are rejected by RimWorld's normal mod loader.", InsightUiTextStyle.Caption));

            InsightUiElement reset = Panel("advanced.reset", InsightUi.SectionHeader("advanced.reset.header", "Full reset",
                "This is the destructive path: it clears gameplay overrides, groups, tags, traits, visuals, and caches using ResetAll."),
                ActionButton("advanced.reset.all", "Reset all settings", ResetAllSettings));

            return Page("advanced.page", "Advanced", "Inspect tags, caches, compatibility, accessibility modes, and destructive maintenance actions.",
                accessibility, tagsPanel, diagnostics, reset);
        }

        private InsightUiElement Page(string id, string title, string subtitle, params InsightUiElement[] content)
        {
            List<InsightUiElement> items = new List<InsightUiElement>
            {
                InsightUi.SectionHeader(id + ".header", title, subtitle, null, null, true)
            };
            if (content != null) items.AddRange(content.Where(item => item != null));
            InsightUiStack body = InsightUi.Column(id + ".body", items.ToArray());
            body.Style.Gap = 8f;
            body.Style.Padding = InsightUiPadding.All(2f);
            return InsightUi.Scroll(id + ".scroll", body);
        }

        private InsightUiElement Panel(string id, params InsightUiElement[] content)
        {
            InsightUiStack body = InsightUi.Column(id + ".body", content ?? new InsightUiElement[0]);
            body.Style.Gap = 6f;
            InsightUiSurface surface = InsightUi.Surface(id, body);
            surface.Style.CornerRadius = 3f;
            return surface;
        }

        private InsightUiElement FloatControl(string id, string label, Func<float> getter, Action<float> setter,
            float minimum, float maximum, string description)
        {
            InsightUiSlider slider = InsightUi.Slider(id + ".slider", 0f, minimum, maximum)
                .Bind(getter, value => Mutate(() => setter(value)));
            slider.Style.Flex = 1f;
            InsightUiStack row = InsightUi.Row(id + ".row", InsightUi.Label(id + ".label", label, InsightUiTextStyle.Label), slider,
                DynamicLabel(id + ".value", () => FormatPercent(getter())));
            row.Style.Gap = 6f;
            return Panel(id, row, InsightUi.Label(id + ".description", description, InsightUiTextStyle.Caption));
        }

        private InsightUiElement IntControl(string id, string label, Func<int> getter, Action<int> setter,
            int minimum, int maximum, string description)
        {
            string[] options = Enumerable.Range(minimum, maximum - minimum + 1).Select(value => value.ToString()).ToArray();
            InsightUiSelect select = InsightUi.Select(id + ".select", label, options, 0)
                .Bind(() => Mathf.Clamp(getter() - minimum, 0, options.Length - 1), value => Mutate(() => setter(value + minimum)));
            return Panel(id, select, InsightUi.Label(id + ".description", description, InsightUiTextStyle.Caption));
        }

        private InsightUiElement ToggleControl(string id, string label, Func<bool> getter, Action<bool> setter, string description)
        {
            InsightUiToggle toggle = InsightUi.Toggle(id + ".toggle", label)
                .Bind(getter, value => Mutate(() => setter(value)));
            return Panel(id, toggle, InsightUi.Label(id + ".description", description, InsightUiTextStyle.Caption));
        }

        private InsightUiElement ActionButton(string id, string label, Action action)
        {
            InsightUiButton button = InsightUi.Button(id, label, action);
            button.Style.HorizontalAlignment = InsightAlignment.Start;
            button.SetTooltip(label);
            return button;
        }

        private InsightUiLabel DynamicLabel(string id, Func<string> provider, InsightUiTextStyle style = InsightUiTextStyle.Body)
        {
            return InsightUi.Label(id, string.Empty, style).SetTextProvider(provider);
        }

        private InsightUiElement GroupListItem(int index)
        {
            if (index < 0 || index >= filteredGroupSummaries.Count) return InsightUi.Empty("groups.empty." + index);
            GroupUiSummary summary = filteredGroupSummaries[index];
            PlantGroupRecord group = summary.Authority;
            InsightUiButton button = InsightUi.Button("select", summary.Name + "  (" + summary.PlantCount + ")", () => SelectGroup(group));
            button.SelectedProvider = () => ReferenceEquals(selectedGroup, group);
            return InsightUi.Scope("group." + SafeId(group.Id), button);
        }

        private InsightUiElement GroupMemberListItem(int index)
        {
            if (index < 0 || index >= groupMembers.Count) return InsightUi.Empty("group-members.empty." + index);
            ThingDef plant = groupMembers[index];
            PlantGroupRecord group = selectedGroup;
            InsightUiToggle toggle = InsightUi.Toggle("membership", plant.LabelCap.ToString())
                .Bind(() => group != null && group.Contains(plant), value =>
                {
                    if (group == null) return;
                    Mutate(() =>
                    {
                        if (value) settings.AssignPlantToGroup(plant, group);
                        else settings.RemovePlantFromGroup(plant);
                    });
                });
            return InsightUi.Scope("group-member." + SafeId(plant.defName), toggle);
        }

        private InsightUiElement PlantListItem(int index)
        {
            if (index < 0 || index >= filteredPlantSummaries.Count) return InsightUi.Empty("plants.empty." + index);
            PlantUiSummary summary = filteredPlantSummaries[index];
            ThingDef plant = summary.Definition;
            InsightUiButton button = InsightUi.Button("select", summary.Label, () => SelectPlant(plant));
            button.SelectedProvider = () => ReferenceEquals(selectedPlant, plant);
            return InsightUi.Scope("plant." + SafeId(plant.defName), button);
        }

        private InsightUiElement PlantGroupListItem(int index)
        {
            if (index < 0 || index >= groupSummaries.Count) return InsightUi.Empty("plant-groups.empty." + index);
            GroupUiSummary summary = groupSummaries[index];
            PlantGroupRecord group = summary.Authority;
            InsightUiToggle toggle = InsightUi.Toggle("membership", summary.Name)
                .Bind(() => selectedPlant != null && ReferenceEquals(settings.GroupForPlant(selectedPlant), group), value =>
                {
                    if (selectedPlant == null) return;
                    Mutate(() =>
                    {
                        if (value) settings.AssignPlantToGroup(selectedPlant, group);
                        else if (ReferenceEquals(settings.GroupForPlant(selectedPlant), group)) settings.RemovePlantFromGroup(selectedPlant);
                    });
                });
            return InsightUi.Scope("plant-group." + SafeId(group.Id), toggle);
        }

        private InsightUiElement TraitListItem(int index)
        {
            if (index < 0 || index >= filteredTraitSummaries.Count) return InsightUi.Empty("traits.empty." + index);
            TraitUiSummary summary = filteredTraitSummaries[index];
            VarietyTraitDef trait = summary.Definition;
            InsightUiButton button = InsightUi.Button("select", summary.Label, () => SelectTrait(trait));
            button.SelectedProvider = () => ReferenceEquals(selectedTrait, trait);
            return InsightUi.Scope("trait." + SafeId(trait.defName), button);
        }

        private InsightUiElement TraitGroupListItem(int index)
        {
            if (index < 0 || index >= traitGroups.Count) return InsightUi.Empty("trait-groups.empty." + index);
            string group = traitGroups[index];
            InsightUiButton button = InsightUi.Button("select", group, () =>
            {
                if (selectedTrait == null) return;
                Mutate(() => settings.SetTraitGroup(selectedTrait, group));
            });
            button.SelectedProvider = () => selectedTrait != null && string.Equals(settings.TraitGroup(selectedTrait), group, StringComparison.OrdinalIgnoreCase);
            return InsightUi.Scope("trait-group." + SafeId(group), button);
        }

        private InsightUiElement TagListItem(int index)
        {
            if (index < 0 || index >= tags.Count) return InsightUi.Empty("tags.empty." + index);
            string tag = tags[index];
            return InsightUi.Scope("tag." + SafeId(tag), InsightUi.Button("open", tag, () =>
            {
                if (selectedPlant != null) Find.WindowStack.Add(new Dialog_TagPlantMembers(settings, tag));
            }));
        }

        private InsightUiElement ProfileListItem(int index)
        {
            if (index < 0 || index >= filteredProfileSummaries.Count) return InsightUi.Empty("profiles.empty." + index);
            ProfileUiSummary summary = filteredProfileSummaries[index];
            SettingsProfileInfo profile = summary.Authority;
            InsightUiButton button = InsightUi.Button("select", summary.Name, () =>
            {
                selectedProfile = profile;
                profileName = profile.Name;
                uiDocument.Invalidate();
            });
            button.SelectedProvider = () => ReferenceEquals(selectedProfile, profile);
            return InsightUi.Scope("profile." + SafeId(profile.Name), button);
        }

        private void RefreshSnapshots()
        {
            if (normalizationPending)
            {
                settings.Normalize();
                PlantTagUtility.RebuildCache();
                normalizationPending = false;
            }
            if (!snapshotDirty) return;

            plants.Clear();
            plants.AddRange(DefDatabase<ThingDef>.AllDefsListForReading.Where(NovelSeedUtility.IsGrowableCrop)
                .OrderBy(plant => plant.label).ThenBy(plant => plant.defName));
            plantSummaries.Clear();
            plantSummaries.AddRange(plants.Select(plant => new PlantUiSummary(plant)));
            filteredPlants.Clear();
            filteredPlantSummaries.Clear();
            filteredPlantSummaries.AddRange(plantSummaries.Where(summary => Matches(summary.Label, summary.DefName, plantSearch)));
            filteredPlants.AddRange(filteredPlantSummaries.Select(summary => summary.Definition));

            groups.Clear();
            groups.AddRange(settings.PlantGroups.Where(group => group != null).OrderBy(group => group.Name));
            groupSummaries.Clear();
            groupSummaries.AddRange(groups.Select(group => new GroupUiSummary(group)));
            filteredGroups.Clear();
            filteredGroupSummaries.Clear();
            filteredGroupSummaries.AddRange(groupSummaries.Where(summary => Matches(summary.Name, summary.Id, groupSearch)));
            filteredGroups.AddRange(filteredGroupSummaries.Select(summary => summary.Authority));
            groupMembers.Clear();
            groupMembers.AddRange(plants.Where(plant => Matches(plant.LabelCap.ToString(), plant.defName, groupMemberSearch)));

            traits.Clear();
            traits.AddRange(TraitConfigUtility.TopLevelTraits().Where(trait => trait != null)
                .OrderBy(trait => trait.label).ThenBy(trait => trait.defName));
            traitSummaries.Clear();
            traitSummaries.AddRange(traits.Select(trait => new TraitUiSummary(trait)));
            filteredTraits.Clear();
            filteredTraitSummaries.Clear();
            filteredTraitSummaries.AddRange(traitSummaries.Where(summary => Matches(summary.Label, summary.DefName, traitSearch)));
            filteredTraits.AddRange(filteredTraitSummaries.Select(summary => summary.Definition));
            traitGroups.Clear();
            traitGroups.AddRange(settings.TraitGroupNames().Where(group => !group.NullOrEmpty()).OrderBy(group => group));

            tags.Clear();
            tags.AddRange(PlantTagUtility.ConfigurableTags().Where(tag => Matches(tag, tag, tagSearch)).OrderBy(tag => tag));

            SettingsProfileManager.Refresh();
            profiles.Clear();
            profiles.AddRange(SettingsProfileManager.Profiles);
            profileSummaries.Clear();
            profileSummaries.AddRange(profiles.Select(profile => new ProfileUiSummary(profile)));
            filteredProfiles.Clear();
            filteredProfileSummaries.Clear();
            filteredProfileSummaries.AddRange(profileSummaries.Where(summary => Matches(summary.Name, summary.Path, profileSearch)));
            filteredProfiles.AddRange(filteredProfileSummaries.Select(summary => summary.Authority));

            if (selectedGroup != null && !groups.Contains(selectedGroup)) selectedGroup = null;
            if (selectedPlant != null && !plants.Contains(selectedPlant)) selectedPlant = null;
            if (selectedTrait != null && !traits.Contains(selectedTrait)) selectedTrait = null;
            if (selectedProfile != null && !profiles.Contains(selectedProfile)) selectedProfile = null;
            if (selectedGroup != null && groupName.NullOrEmpty()) groupName = selectedGroup.Name;

            SetListState(groupList, filteredGroupSummaries.Count, true);
            SetListState(groupMemberList, groupMembers.Count, true);
            SetListState(plantList, filteredPlantSummaries.Count, true);
            SetListState(plantGroupList, groups.Count, true);
            SetListState(traitList, filteredTraitSummaries.Count, true);
            SetListState(traitGroupList, traitGroups.Count, true);
            SetListState(tagList, tags.Count, true);
            SetListState(profileList, filteredProfiles.Count, true);
            snapshotDirty = false;
        }

        private static void SetListState(InsightUiVirtualList list, int count, bool refresh)
        {
            if (list == null) return;
            if (list.ItemCount != count || refresh) list.Refresh();
            list.ItemCount = count;
        }

        private void ApplyResponsiveLayout(float width)
        {
            InsightUiOrientation orientation = width < 820f ? InsightUiOrientation.Vertical : InsightUiOrientation.Horizontal;
            SetSplitOrientation("workspace.groups.split", orientation);
            SetSplitOrientation("workspace.plants.split", orientation);
            SetSplitOrientation("workspace.traits.split", orientation);
            SetSplitOrientation("profiles.split", orientation);
        }

        private void SetSplitOrientation(string id, InsightUiOrientation orientation)
        {
            InsightUiElement element = FindElement(uiDocument.Root, id);
            InsightUiSplit split = element as InsightUiSplit;
            if (split != null) split.Orientation = orientation;
        }

        private static InsightUiElement FindElement(InsightUiElement root, string id)
        {
            if (root == null) return null;
            if (root.Id == id) return root;
            foreach (InsightUiElement child in root.Children ?? new InsightUiElement[0])
            {
                InsightUiElement result = FindElement(child, id);
                if (result != null) return result;
            }
            return null;
        }

        private void SelectGroup(PlantGroupRecord group)
        {
            selectedGroup = group;
            groupName = group?.Name ?? string.Empty;
            activePageId = "workspace";
            activeWorkspaceTab = "groups";
            uiDocument.Invalidate();
        }

        private void SelectPlant(ThingDef plant)
        {
            selectedPlant = plant;
            activePageId = "workspace";
            activeWorkspaceTab = "plants";
            uiDocument.Invalidate();
        }

        private void SelectTrait(VarietyTraitDef trait)
        {
            selectedTrait = trait;
            activePageId = "workspace";
            activeWorkspaceTab = "traits";
            uiDocument.Invalidate();
        }

        private void SetSearch(ref string field, string value)
        {
            string normalized = value ?? string.Empty;
            if (field == normalized) return;
            field = normalized;
            snapshotDirty = true;
            uiDocument?.Invalidate();
        }

        private void Mutate(Action mutation)
        {
            if (mutation == null) return;
            mutation();
            settings.ClearVisualCache();
            ProduceMaskRenderer.ClearAll();
            normalizationPending = true;
            snapshotDirty = true;
            uiDocument?.Invalidate();
            settingsChanged?.Invoke();
        }

        private void Confirm(string message, Action accepted)
        {
            if (accepted == null) return;
            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(message, accepted, true));
        }

        private PlantSettingsRecord SelectedPlantSettings(bool create)
        {
            return selectedPlant == null ? null : settings.GetPlantSettings(selectedPlant, create);
        }

        private PlantSettingsRecord EnsureSelectedPlantSettings()
        {
            return SelectedPlantSettings(true) ?? new PlantSettingsRecord(selectedPlant);
        }

        private PlantSettingsRecord SelectedGroupSettings()
        {
            return selectedGroup?.Settings;
        }

        private PlantSettingsRecord EnsureSelectedGroupSettings()
        {
            if (selectedGroup == null) return new PlantSettingsRecord();
            if (selectedGroup.Settings != null) return selectedGroup.Settings;
            return new PlantSettingsRecord();
        }

        private GlobalTraitSettingsRecord SelectedGlobalTrait()
        {
            return selectedTrait == null ? null : settings.GetGlobalTraitSettings(selectedTrait, false);
        }

        private GlobalTraitSettingsRecord EnsureSelectedGlobalTrait()
        {
            return settings.GetGlobalTraitSettings(selectedTrait);
        }

        private bool SelectedTraitCategoryEnabled()
        {
            return selectedTrait == null || settings.GetCategorySettings(settings.TraitGroup(selectedTrait), false)?.enabled != false;
        }

        private void SetSelectedTraitCategoryEnabled(bool value)
        {
            if (selectedTrait == null) return;
            settings.GetCategorySettings(settings.TraitGroup(selectedTrait)).enabled = value;
        }

        private void SetSelectedTraitWeight(float value)
        {
            if (selectedTrait != null) settings.GetGlobalTraitSettings(selectedTrait).weight = value;
        }

        private TraitSettingsRecord SelectedPlantTraitSettings(bool create)
        {
            if (selectedPlant == null || selectedTrait == null) return null;
            return SelectedPlantSettings(create)?.GetTraitSettings(selectedTrait, create);
        }

        private TraitSettingsRecord EnsureSelectedPlantTraitSettings()
        {
            return SelectedPlantTraitSettings(true) ?? new TraitSettingsRecord(selectedTrait);
        }

        private void SetSelectedPlantTraitWeight(float value)
        {
            if (selectedPlant == null || selectedTrait == null) return;
            EnsureSelectedPlantTraitSettings().weight = value;
            EnsureSelectedPlantTraitSettings().useCustomWeight = true;
        }

        private void SaveProfile()
        {
            string name = SettingsProfileManager.NormalizeName(profileName);
            if (name.NullOrEmpty()) return;
            if (SettingsProfileManager.Exists(name) && (selectedProfile == null || !string.Equals(selectedProfile.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                Confirm("Replace the saved profile '" + name + "'?", () => SaveProfileNow(name));
                return;
            }
            SaveProfileNow(name);
        }

        private void SaveProfileNow(string name)
        {
            string error;
            if (SettingsProfileManager.Save(name, settings, out error))
            {
                uiDocument.Toasts.Show("Saved profile '" + name + "'.", InsightToastSeverity.Success);
                profileName = name;
                snapshotDirty = true;
            }
            else uiDocument.Toasts.Show("Could not save profile: " + error, InsightToastSeverity.Error);
        }

        private void UpdateProfile()
        {
            if (selectedProfile == null) return;
            profileName = selectedProfile.Name;
            SaveProfile();
        }

        private void ApplyProfile()
        {
            if (selectedProfile == null) return;
            SettingsProfileInfo profile = selectedProfile;
            string error;
            if (SettingsProfileManager.Load(profile, settings, out error))
            {
                normalizationPending = false;
                snapshotDirty = true;
                settings.ClearVisualCache();
                ProduceMaskRenderer.ClearAll();
                uiDocument.Toasts.Show("Applied profile '" + profile.Name + "'.", InsightToastSeverity.Success);
                uiDocument.Invalidate();
            }
            else uiDocument.Toasts.Show("Could not apply profile: " + error, InsightToastSeverity.Error);
        }

        private void DeleteProfile()
        {
            if (selectedProfile == null) return;
            SettingsProfileInfo profile = selectedProfile;
            Confirm("Delete the saved profile '" + profile.Name + "'?", () =>
            {
                string error;
                if (SettingsProfileManager.Delete(profile, out error))
                {
                    selectedProfile = null;
                    profileName = string.Empty;
                    snapshotDirty = true;
                    uiDocument.Toasts.Show("Deleted profile '" + profile.Name + "'.", InsightToastSeverity.Success);
                }
                else uiDocument.Toasts.Show("Could not delete profile: " + error, InsightToastSeverity.Error);
            });
        }

        private void ResetAllSettings()
        {
            Confirm("Reset every Horticulture setting, group, tag, trait, visual, and cache?", () =>
            {
                Mutate(() => settings.ResetAll());
                uiDocument.Toasts.Show("All Horticulture settings were reset.", InsightToastSeverity.Warning);
            });
        }

        private void ExportPublisherDefault()
        {
            string error;
            if (SettingsProfileManager.ExportPublisherDefault(settings, out error))
                uiDocument.Toasts.Show("Exported publisher default.", InsightToastSeverity.Success);
            else uiDocument.Toasts.Show("Could not export publisher default: " + error, InsightToastSeverity.Error);
        }

        private void OpenPlantTags()
        {
            if (selectedPlant != null) Find.WindowStack.Add(new Dialog_PlantTagEditor(settings, selectedPlant));
        }

        private void OpenPlantMaskExport()
        {
            Find.WindowStack.Add(new Dialog_ExportPlantMasks(settings, selectedPlant));
        }

        private void OpenPlantMaskEditor()
        {
            if (selectedPlant != null) Find.WindowStack.Add(new Dialog_PlantMasks(selectedPlant));
            else Find.WindowStack.Add(new Dialog_ExportPlantMasks(settings));
        }

        private void GenerateMissingAutoMasks()
        {
            PlantAutoMaskCache.InitializeAndGenerateMissing();
            uiDocument.Toasts.Show("Automatic mask generation was queued for missing masks.", InsightToastSeverity.Info);
        }

        private void OpenTraitTags()
        {
            if (selectedTrait != null) Find.WindowStack.Add(new Dialog_TraitTags(settings, selectedTrait));
        }

        private void OpenTraitVisual()
        {
            if (selectedTrait != null) Find.WindowStack.Add(new Dialog_TraitVisualDesigner(settings, selectedTrait, selectedPlant));
        }

        private static InsightTheme CreateBotanicalTheme()
        {
            InsightTheme theme = InsightTheme.Default.Clone();
            theme.Id = "horticulture-botanical";
            theme.Background = new InsightColor(0.035f, 0.047f, 0.043f);
            theme.Surface = new InsightColor(0.09f, 0.115f, 0.10f);
            theme.ElevatedSurface = new InsightColor(0.13f, 0.16f, 0.14f);
            theme.PrimaryText = new InsightColor(0.91f, 0.93f, 0.88f);
            theme.SecondaryText = new InsightColor(0.64f, 0.70f, 0.65f);
            theme.Selected = new InsightColor(0.25f, 0.68f, 0.38f);
            theme.Hover = new InsightColor(0.21f, 0.42f, 0.28f);
            theme.Focus = new InsightColor(0.58f, 0.86f, 0.42f);
            theme.Positive = new InsightColor(0.34f, 0.78f, 0.45f);
            theme.Negative = new InsightColor(0.82f, 0.36f, 0.32f);
            theme.Warning = new InsightColor(0.88f, 0.68f, 0.28f);
            theme.CornerRadius = 3f;
            theme.Spacing = 6f;
            theme.Shadow = new InsightColor(0f, 0f, 0f, 0.22f);
            return theme;
        }

        private sealed class PlantUiSummary
        {
            public PlantUiSummary(ThingDef definition)
            {
                Definition = definition;
                DefName = definition?.defName ?? string.Empty;
                Label = definition?.LabelCap.ToString() ?? DefName;
            }

            public readonly ThingDef Definition;
            public readonly string DefName;
            public readonly string Label;
        }

        private sealed class GroupUiSummary
        {
            public GroupUiSummary(PlantGroupRecord authority)
            {
                Authority = authority;
                Id = authority?.Id ?? string.Empty;
                Name = authority?.Name ?? string.Empty;
                PlantCount = authority?.PlantCount ?? 0;
            }

            public readonly PlantGroupRecord Authority;
            public readonly string Id;
            public readonly string Name;
            public readonly int PlantCount;
        }

        private sealed class TraitUiSummary
        {
            public TraitUiSummary(VarietyTraitDef definition)
            {
                Definition = definition;
                DefName = definition?.defName ?? string.Empty;
                Label = definition?.LabelCap.ToString() ?? DefName;
            }

            public readonly VarietyTraitDef Definition;
            public readonly string DefName;
            public readonly string Label;
        }

        private sealed class ProfileUiSummary
        {
            public ProfileUiSummary(SettingsProfileInfo authority)
            {
                Authority = authority;
                Name = authority?.Name ?? string.Empty;
                Path = authority?.Path ?? string.Empty;
                Modified = authority?.Modified ?? default(DateTime);
            }

            public readonly SettingsProfileInfo Authority;
            public readonly string Name;
            public readonly string Path;
            public readonly DateTime Modified;
        }

        private static int CountDuplicateIds(InsightUiElement element, HashSet<string> ids)
        {
            if (element == null) return 0;
            int duplicates = ids.Add(element.Id) ? 0 : 1;
            foreach (InsightUiElement child in element.Children ?? new InsightUiElement[0])
                duplicates += CountDuplicateIds(child, ids);
            return duplicates;
        }

        private static bool Matches(string label, string defName, string query)
        {
            if (query.NullOrEmpty()) return true;
            string value = query.Trim();
            return (label ?? string.Empty).IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0
                || (defName ?? string.Empty).IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string FormatPercent(float value)
        {
            return Mathf.Clamp01(value).ToString("P0");
        }

        private static string SafeId(string value)
        {
            if (value.NullOrEmpty()) return "empty";
            char[] chars = value.Select(character => char.IsLetterOrDigit(character) ? character : '_').ToArray();
            return new string(chars);
        }
    }
}
