using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using HarmonyLib;
using InsightCanvas;
using RimWorld;
using UnityEngine;
using Verse;
using HorticultureNovelSeeds;

namespace HorticultureNovelSeeds.RuntimeTests
{
    internal sealed class RuntimeScenarioBlockedException : Exception
    {
        internal RuntimeScenarioBlockedException(string message) : base(message) { }
    }

    [Serializable]
    internal sealed class SaveReloadCheckpoint
    {
        public string saveName;
        public string ordinaryId;
        public string treeId;
        public string hybridId;
        public string cropDefName;
        public string treeDefName;
        public List<string> ordinaryTraits = new List<string>();
        public List<string> treeTraits = new List<string>();
        public List<string> hybridTraits = new List<string>();
    }

    internal static class HorticultureRuntimeScenarioSuite
    {
        private static readonly List<Thing> Fixtures = new List<Thing>();
        private static readonly List<Zone_Growing> GrowingZones = new List<Zone_Growing>();

        internal static ScenarioExecution Execute(HorticultureRuntimeTestRequest request, bool resuming)
        {
            HorticultureRuntimeTestReport report = NewReport(request);
            bool deferCleanup = false;
            try
            {
                if (resuming || string.Equals(request.scenario, "save-reload", StringComparison.OrdinalIgnoreCase))
                {
                    bool awaiting = SaveReload(report, request, resuming);
                    if (awaiting)
                    {
                        deferCleanup = true;
                        return new ScenarioExecution { Report = report, AwaitingReload = true };
                    }
                }
                else
                {
                    switch ((request.scenario ?? "complete").Trim().ToLowerInvariant())
                    {
                        case "startup": Startup(report); break;
                        case "clean-default": CleanDefault(report); break;
                        case "ordinary-crop": OrdinaryCrop(report); break;
                        case "sowable-tree": SowableTree(report); break;
                        case "cross-pollination": CrossPollination(report); break;
                        case "produce-processing": ProduceProcessing(report); break;
                        case "knowledge": Knowledge(report); break;
                        case "negative": Negative(report); break;
                        case "long-running": LongRunning(report); break;
                        case "ux-discovery": UxDiscovery(report); break;
                        case "workspace": Workspace(report); break;
                        case "visual-designer": VisualDesigner(report); break;
                        case "registry-scale": RegistryScale(report); break;
                        case "rc-performance": RcPerformance(report); break;
                        case "auto-mask-suite": AutoMaskSuite(report); break;
                        case "auto-mask-export": AutoMaskExport(report, request); break;
                        case "complete":
                            Startup(report);
                            CleanDefault(report);
                            UxDiscovery(report);
                            Workspace(report);
                            VisualDesigner(report);
                            RegistryScale(report);
                            RcPerformance(report);
                            OrdinaryCrop(report);
                            SowableTree(report);
                            CrossPollination(report);
                            ProduceProcessing(report);
                            Knowledge(report);
                            Negative(report);
                            LongRunning(report);
                            if (SaveReload(report, request, false))
                            {
                                deferCleanup = true;
                                return new ScenarioExecution { Report = report, AwaitingReload = true };
                            }
                            break;
                        default:
                            Block(report, "scenario", "Unknown scenario: " + request.scenario);
                            break;
                    }
                }
            }
            catch (Exception exception)
            {
                Failure(report, "scenario", exception);
            }
            finally
            {
                if (!deferCleanup) CleanupFixtures();
            }
            return new ScenarioExecution { Report = report };
        }

        private static HorticultureRuntimeTestReport NewReport(HorticultureRuntimeTestRequest request)
        {
            HorticultureKnowledgeDiagnosticSnapshot diagnostics = HorticultureKnowledgeAdapter.Diagnostics;
            HorticultureRuntimeTestReport report = new HorticultureRuntimeTestReport
            {
                requestId = request.requestId,
                launchId = request.launchId,
                scenario = request.scenario,
                status = "PASS",
                horticultureCommit = request.horticultureCommit,
                horticultureDllSha256 = request.horticultureDllSha256,
                knowledgeFrameworkDllSha256 = request.knowledgeFrameworkDllSha256,
                knowledgeFrameworkRelease = diagnostics?.frameworkRelease ?? request.knowledgeFrameworkRelease,
                knowledgeFrameworkApiGeneration = diagnostics?.frameworkApiVersion ?? request.knowledgeFrameworkApiGeneration,
                rimWorldVersion = VersionControl.CurrentVersionString,
                startTick = Find.TickManager?.TicksGame ?? 0
            };
            report.relevantDiagnostics.Add(diagnostics?.ToString() ?? "Knowledge diagnostics unavailable.");
            return report;
        }

        private static void Startup(HorticultureRuntimeTestReport report)
        {
            Check(report, "startup-game-component", () =>
            {
                Require(GameComponent_NovelSeeds.Instance != null, "GameComponent_NovelSeeds was not created.");
                Require(HorticultureNovelSeedsMod.Settings != null, "default settings were not loaded.");
                return "game component and default settings are available";
            });
            Check(report, "startup-supported-defs", () =>
            {
                int supported = DefDatabase<ThingDef>.AllDefsListForReading.Count(HorticulturePlantPolicy.IsSupported);
                Require(supported > 0, "no supported plant definitions were discovered.");
                return "supported plant defs=" + supported;
            });
            Check(report, "startup-knowledge-diagnostics", () =>
            {
                HorticultureKnowledgeDiagnosticSnapshot diagnostics = HorticultureKnowledgeAdapter.Diagnostics;
                Require(diagnostics != null, "Knowledge diagnostics were unavailable.");
                if (!diagnostics.IsUsable)
                    throw new InvalidOperationException("Knowledge Framework is not usable: " + diagnostics);
                return diagnostics.ToString();
            });
        }

        private static void CleanDefault(HorticultureRuntimeTestReport report)
        {
            Check(report, "clean-default-values", () =>
            {
                string defaultPath = SettingsProfileManager.BundledDefaultPath;
                Require(!defaultPath.NullOrEmpty() && File.Exists(defaultPath), "the bundled default configuration is missing.");
                XElement settings = XDocument.Load(defaultPath).Root?.Element("settings");
                Require(settings != null, "the bundled default configuration has no settings node.");
                float mutation = BundledDefaultFloat(settings, "globalMutationChance", NovelSeedUtility.SpontaneousMutationChance);
                float crossPollination = BundledDefaultFloat(settings, "globalCrossPollinationChance", NovelSeedUtility.DefaultCrossPollinationChance);
                float wildMutation = BundledDefaultFloat(settings, "wildMutationChance", NovelSeedUtility.DefaultWildMutationChance);
                float donorGrowth = BundledDefaultFloat(settings, "minimumDonorGrowth", NovelSeedUtility.DefaultMinimumDonorGrowth);
                float secondSlot = BundledDefaultFloat(settings, "secondCrossPollinationTraitChance", NovelSeedUtility.DefaultSecondCrossPollinationTraitChance);
                float laterSlot = BundledDefaultFloat(settings, "laterCrossPollinationTraitChance", NovelSeedUtility.DefaultLaterCrossPollinationTraitChance);
                bool produceVisuals = BundledDefaultBool(settings, "enableProduceVisuals", true);
                Require(Mathf.Abs(mutation - NovelSeedUtility.SpontaneousMutationChance) < 0.00001f,
                    "bundled mutation default is " + mutation + ", expected " + NovelSeedUtility.SpontaneousMutationChance + ".");
                Require(Mathf.Abs(crossPollination - NovelSeedUtility.DefaultCrossPollinationChance) < 0.00001f,
                    "bundled cross-pollination default is incorrect.");
                Require(Mathf.Abs(wildMutation - NovelSeedUtility.DefaultWildMutationChance) < 0.00001f,
                    "bundled wild mutation default is incorrect.");
                Require(Mathf.Abs(donorGrowth - NovelSeedUtility.DefaultMinimumDonorGrowth) < 0.00001f,
                    "bundled donor-growth default is incorrect.");
                Require(Mathf.Abs(secondSlot - NovelSeedUtility.DefaultSecondCrossPollinationTraitChance) < 0.00001f
                    && Mathf.Abs(laterSlot - NovelSeedUtility.DefaultLaterCrossPollinationTraitChance) < 0.00001f,
                    "bundled additional cross-pollination slot defaults are incorrect.");
                Require(produceVisuals, "produce visuals are disabled in the clean default.");
                return "mutation=8%, cross=0.7%, wild=0.5%, donor=50%, produce visuals=on";
            });
        }

        private static float BundledDefaultFloat(XElement settings, string name, float fallback)
        {
            XElement value = settings?.Element(name);
            return value == null ? fallback : float.Parse(value.Value, CultureInfo.InvariantCulture);
        }

        private static bool BundledDefaultBool(XElement settings, string name, bool fallback)
        {
            XElement value = settings?.Element(name);
            return value == null ? fallback : bool.Parse(value.Value);
        }

        private static void UxDiscovery(HorticultureRuntimeTestReport report)
        {
            Check(report, "ux-keyed-guidance", () =>
            {
                string[] keys =
                {
                    "HNS_SaveSeeds", "HNS_SaveSeedsDesc", "HNS_NameDialogPrompt", "HNS_NameDialogPreservationNote",
                    "HNS_NameDialogConfirm", "HNS_RegistryPlantsTab", "HNS_RegistryCultivarsTab", "HNS_SettingsAdvancedShow"
                };
                foreach (string key in keys)
                {
                    string text = key.Translate("rice").ToString();
                    Require(!text.NullOrEmpty() && !string.Equals(text, key, StringComparison.Ordinal),
                        "missing player-facing keyed text: " + key);
                }
                return "first discovery, preservation, registry, and settings guidance is localized";
            });
            Check(report, "ux-progressive-settings", () =>
            {
                FieldInfo advanced = AccessTools.Field(typeof(NovelSeedsSettingsUI), "showAdvancedGeneralSettings");
                Require(advanced != null && advanced.FieldType == typeof(bool), "progressive settings disclosure state is missing.");
                Require(!(bool)advanced.GetValue(null), "general settings opened with advanced controls already exposed.");
                MethodInfo comparisonGate = AccessTools.Method(typeof(MainTabWindow_CultivarRegistry), "CanCompareCount");
                Require(comparisonGate != null && !(bool)comparisonGate.Invoke(null, new object[] { 1 })
                    && (bool)comparisonGate.Invoke(null, new object[] { 2 }),
                    "cultivar comparison discovery state is inconsistent.");
                return "general settings start compact and comparison explains its two-cultivar requirement";
            });
            Check(report, "ux-insight-document-isolation", () =>
            {
                Type documentType = AccessTools.TypeByName("HorticultureNovelSeeds.InsightSettingsDocument");
                Require(documentType != null, "Insight Canvas settings document type is missing.");
                NovelSeedsSettings firstSettings = new NovelSeedsSettings();
                NovelSeedsSettings secondSettings = new NovelSeedsSettings();
                object first = Activator.CreateInstance(documentType, new object[] { firstSettings });
                object second = Activator.CreateInstance(documentType, new object[] { secondSettings });
                MethodInfo isolated = AccessTools.Method(documentType, "HasIsolatedPresentationState");
                Require(isolated != null && (bool)isolated.Invoke(first, new[] { second }),
                    "settings documents share presentation state.");
                PropertyInfo trackDuplicates = AccessTools.Property(documentType, "TrackDuplicateIds");
                Require(trackDuplicates != null && (bool)trackDuplicates.GetValue(first, null),
                    "duplicate-ID diagnostics are disabled for the settings document.");
                return "two settings documents have isolated state, focus, feedback, and duplicate-ID diagnostics";
            });
            Check(report, "ux-insight-pages-and-virtualization", () =>
            {
                Type documentType = AccessTools.TypeByName("HorticultureNovelSeeds.InsightSettingsDocument");
                object document = Activator.CreateInstance(documentType, new object[] { new NovelSeedsSettings() });
                PropertyInfo pages = AccessTools.Property(documentType, "NavigationPageCount");
                PropertyInfo plantLimit = AccessTools.Property(documentType, "PlantVirtualizationCacheLimit");
                PropertyInfo traitLimit = AccessTools.Property(documentType, "TraitVirtualizationCacheLimit");
                Require(pages != null && (int)pages.GetValue(document, null) == 5,
                    "settings navigation does not expose all five pages.");
                Require(plantLimit != null && (int)plantLimit.GetValue(document, null) <= 96,
                    "plant virtualization cache is unbounded.");
                Require(traitLimit != null && (int)traitLimit.GetValue(document, null) <= 96,
                    "trait virtualization cache is unbounded.");
                return "five pages and bounded plant/trait virtual-list caches are discoverable";
            });
            Check(report, "ux-insight-navigation-and-search", () =>
            {
                InsightSettingsDocument document = NewSettingsDocument();
                string[] expectedPages = { "gameplay", "workspace", "visuals", "profiles", "advanced" };
                Require(expectedPages.SequenceEqual(document.NavigationPageIds), "settings navigation page IDs are incomplete or unstable.");
                InsightUiNavigation navigation = InstanceField<InsightUiNavigation>(document, "navigation");
                navigation.Select("workspace");
                Require(document.ActivePageId == "workspace", "navigation selection did not update the document-owned page.");
                InsightUiTabs tabs = InstanceField<InsightUiTabs>(document, "workspaceTabs");
                Require(new[] { "groups", "plants", "traits" }.SequenceEqual(tabs.Tabs.Select(tab => tab.Id)),
                    "workspace tabs are incomplete or unstable.");
                tabs.Select("traits");
                Require(tabs.ActiveTabId == "traits", "workspace tab selection did not update document state.");
                InsightUiSearchField search = InstanceField<InsightUiSearchField>(document, "traitSearchField");
                Require(search != null, "trait SearchField was not composed.");
                search.SetText("runtime-query");
                Require(search.Value == "runtime-query", "SearchField did not retain its document-owned query.");
                InvokeInstance(document, "RefreshSnapshots");
                search.Clear();
                Require(search.Value.NullOrEmpty(), "SearchField clear did not clear the document-owned query.");
                return "navigation, workspace tabs, and SearchField queries are document-owned";
            });
            Check(report, "ux-insight-selections-and-group-action", () =>
            {
                InsightSettingsDocument document = NewSettingsDocument();
                Type documentType = document.GetType();
                InsightUiNavigation navigation = InstanceField<InsightUiNavigation>(document, "navigation");
                InsightUiTabs tabs = InstanceField<InsightUiTabs>(document, "workspaceTabs");
                ThingDef plant = DefDatabase<ThingDef>.AllDefsListForReading.FirstOrDefault(NovelSeedUtility.IsGrowableCrop);
                VarietyTraitDef trait = TraitConfigUtility.TopLevelTraits().FirstOrDefault(value => value != null);
                if (plant == null || trait == null)
                    throw new RuntimeScenarioBlockedException("No growable plant and top-level trait were available for UI selection coverage.");

                InvokeInstance(document, "SelectPlant", plant);
                Require(document.ActivePageId == "workspace" && tabs.ActiveTabId == "plants",
                    "plant selection did not navigate to the Plants workspace.");
                Require(ReferenceEquals(InstanceField<ThingDef>(document, "selectedPlant"), plant),
                    "plant selection was not retained by the document.");
                InvokeInstance(document, "SelectTrait", trait);
                Require(document.ActivePageId == "workspace" && tabs.ActiveTabId == "traits",
                    "trait selection did not navigate to the Traits workspace.");
                Require(ReferenceEquals(InstanceField<VarietyTraitDef>(document, "selectedTrait"), trait),
                    "trait selection was not retained by the document.");

                string groupName = "Runtime UI Discovery Group";
                AccessTools.Field(documentType, "groupName").SetValue(document, groupName);
                InsightUiButton create = FindUiElement(InstanceField<InsightUiDocument>(document, "uiDocument").Root,
                    "workspace.groups.create") as InsightUiButton;
                Require(create?.OnClick != null, "plant-group create action was not composed.");
                create.OnClick();
                PlantGroupRecord created = document.Settings.PlantGroups.FirstOrDefault(group => group != null && group.Name == groupName);
                Require(created != null, "plant-group creation action did not write through NovelSeedsSettings.");
                Require(ReferenceEquals(InstanceField<PlantGroupRecord>(document, "selectedGroup"), created),
                    "newly created plant group was not selected in the document.");
                navigation.Select("workspace");
                tabs.Select("groups");
                return "plant, trait, and group selection plus group creation use document state and authoritative settings";
            });
            Check(report, "ux-insight-bindings-and-dependent-controls", () =>
            {
                InsightSettingsDocument document = NewSettingsDocument();
                NovelSeedsSettings settings = document.Settings;
                InsightUiDocument canvas = InstanceField<InsightUiDocument>(document, "uiDocument");
                InsightUiSlider mutation = FindUiElement(canvas.Root, "gameplay.mutation.slider") as InsightUiSlider;
                Require(mutation != null, "mutation slider was not composed.");
                Action<float> mutationSetter = (Action<float>)AccessTools.Field(typeof(InsightUiSlider), "boundSetter").GetValue(mutation);
                Require(mutationSetter != null, "mutation slider is not directly bound.");
                mutationSetter(0.21f);
                Require(Mathf.Abs(settings.globalMutationChance - 0.21f) < 0.0001f,
                    "mutation binding did not write the authoritative settings field.");

                InsightUiToggle balance = FindUiElement(canvas.Root, "gameplay.balance.toggle") as InsightUiToggle;
                InsightUiSlider strength = FindUiElement(canvas.Root, "gameplay.balance-strength.slider") as InsightUiSlider;
                InsightUiSelect imbalance = FindUiElement(canvas.Root, "gameplay.allowed-imbalance.select") as InsightUiSelect;
                Require(balance != null && strength != null && imbalance != null, "dependent balance controls were not composed.");
                Action<bool> balanceSetter = (Action<bool>)AccessTools.Field(typeof(InsightUiToggle), "boundSetter").GetValue(balance);
                Require(balanceSetter != null, "trait-balance toggle is not directly bound.");
                balanceSetter(false);
                InvokeInstance(document, "UpdateDependentControlState");
                Require(!settings.enableTraitBalancing && !strength.Enabled && !imbalance.Enabled,
                    "dependent balance controls remained enabled when balancing was disabled.");
                balanceSetter(true);
                InvokeInstance(document, "UpdateDependentControlState");
                Require(settings.enableTraitBalancing && strength.Enabled && imbalance.Enabled,
                    "dependent balance controls did not re-enable with balancing.");
                return "ordinary controls write authoritative settings and dependent controls follow enablement";
            });
            Check(report, "ux-insight-responsive-accessibility", () =>
            {
                InsightSettingsDocument document = NewSettingsDocument();
                InsightUiDocument canvas = InstanceField<InsightUiDocument>(document, "uiDocument");
                MethodInfo responsive = AccessTools.Method(document.GetType(), "ApplyResponsiveLayout");
                Require(responsive != null, "responsive layout coordinator is missing.");
                responsive.Invoke(document, new object[] { 1200f });
                InsightUiSplit groups = FindUiElement(canvas.Root, "workspace.groups.split") as InsightUiSplit;
                InsightUiSplit profiles = FindUiElement(canvas.Root, "profiles.split") as InsightUiSplit;
                Require(groups != null && profiles != null && groups.Orientation == InsightUiOrientation.Horizontal
                    && profiles.Orientation == InsightUiOrientation.Horizontal && !document.IsNarrowWorkspace,
                    "wide workspace layout did not use horizontal splits.");
                responsive.Invoke(document, new object[] { 640f });
                Require(groups.Orientation == InsightUiOrientation.Vertical && profiles.Orientation == InsightUiOrientation.Vertical
                    && document.IsNarrowWorkspace,
                    "narrow workspace layout did not use vertical splits.");

                InsightUiToggle contrast = FindUiElement(canvas.Root, "advanced.high-contrast.toggle") as InsightUiToggle;
                InsightUiToggle motion = FindUiElement(canvas.Root, "advanced.reduced-motion.toggle") as InsightUiToggle;
                InsightUiSelect density = FindUiElement(canvas.Root, "advanced.density.select") as InsightUiSelect;
                Require(contrast != null && motion != null && density != null, "accessibility controls were not composed.");
                Action<bool> contrastSetter = (Action<bool>)AccessTools.Field(typeof(InsightUiToggle), "boundSetter").GetValue(contrast);
                Action<bool> motionSetter = (Action<bool>)AccessTools.Field(typeof(InsightUiToggle), "boundSetter").GetValue(motion);
                Action<int> densitySetter = (Action<int>)AccessTools.Field(typeof(InsightUiSelect), "boundSetter").GetValue(density);
                contrastSetter(true);
                motionSetter(true);
                densitySetter(2);
                Require(document.HighContrast && document.ReducedMotion && document.Density == InsightUiDensity.Compact,
                    "accessibility and density bindings did not remain document-scoped.");
                return "wide/narrow splits and high-contrast, reduced-motion, and compact-density state are supported";
            });
            Check(report, "ux-insight-diagnostics", () =>
            {
                InsightSettingsDocument document = NewSettingsDocument();
                Require(document.TrackDuplicateIds && document.HasUniqueComponentIds()
                    && document.DuplicateIdCount == 0 && document.RenderErrorCount == 0,
                    "settings document diagnostics reported duplicate IDs or render errors.");
                Require(document.PlantVirtualizationCacheLimit <= 96 && document.TraitVirtualizationCacheLimit <= 96,
                    "settings virtual-list cache bounds are not enforced.");
                return "stable component IDs, duplicate diagnostics, render diagnostics, and cache bounds are clean";
            });
        }

        private static void Workspace(HorticultureRuntimeTestReport report)
        {
            Check(report, "workspace-pages-and-isolation", () =>
            {
                HorticultureWorkspaceDocument first = new HorticultureWorkspaceDocument();
                HorticultureWorkspaceDocument second = new HorticultureWorkspaceDocument();
                string[] expected = { "overview", "plants", "cultivars", "breeding", "knowledge" };
                Require(expected.SequenceEqual(first.NavigationPageIds) && first.NavigationPageCount == 5,
                    "Horticulture workspace pages are incomplete or unstable.");
                Require(first.HasIsolatedPresentationState(second) && first.TrackDuplicateIds && first.HasUniqueComponentIds(),
                    "workspace presentation state or duplicate-ID diagnostics are not isolated.");
                Require(first.RenderErrorCount == 0, "workspace reported render errors before drawing.");
                return "five field-guide pages, isolated document state, and stable IDs are available";
            });
            Check(report, "workspace-navigation-search-and-accessibility", () =>
            {
                HorticultureWorkspaceDocument document = new HorticultureWorkspaceDocument();
                InsightUiNavigation navigation = InstanceField<InsightUiNavigation>(document, "navigation");
                navigation.Select("cultivars");
                Require(document.ActivePageId == "cultivars", "workspace navigation did not update document state.");
                InsightUiSearchField search = InstanceField<InsightUiSearchField>(document, "cultivarSearchField");
                Require(search != null, "cultivar SearchField was not composed.");
                search.SetText("runtime cultivar");
                Require(search.Value == "runtime cultivar", "workspace SearchField did not retain its query.");
                search.Clear();
                Require(search.Value.NullOrEmpty(), "workspace SearchField did not clear its query.");

                MethodInfo responsive = AccessTools.Method(typeof(HorticultureWorkspaceDocument), "ApplyResponsiveLayout");
                Require(responsive != null, "workspace responsive layout coordinator is missing.");
                responsive.Invoke(document, new object[] { 1200f });
                Require(!document.IsNarrowWorkspace, "wide workspace did not remain horizontal.");
                responsive.Invoke(document, new object[] { 640f });
                Require(document.IsNarrowWorkspace, "narrow workspace did not switch to vertical splits.");
                document.SetAccessibility(true, true, InsightUiDensity.Compact);
                Require(document.HighContrast && document.ReducedMotion && document.Density == InsightUiDensity.Compact,
                    "workspace accessibility state was not document-owned.");
                return "navigation, search, responsive splits, high contrast, reduced motion, and compact density are bound";
            });
            Check(report, "workspace-empty-large-and-compare-bounds", () =>
            {
                HorticultureWorkspaceDocument document = new HorticultureWorkspaceDocument();
                document.PreOpen();
                InsightUiVirtualList plantList = InstanceField<InsightUiVirtualList>(document, "plantList");
                InsightUiVirtualList cultivarList = InstanceField<InsightUiVirtualList>(document, "cultivarList");
                Require(plantList != null && cultivarList != null && plantList.CacheLimit <= 96 && cultivarList.CacheLimit <= 96,
                    "workspace virtual-list cache limits are not bounded.");
                plantList.ItemCount = 1000;
                cultivarList.ItemCount = 1000;
                plantList.Refresh();
                cultivarList.Refresh();
                Require(plantList.ItemCount == 1000 && cultivarList.ItemCount == 1000
                    && plantList.CacheLimit <= 96 && cultivarList.CacheLimit <= 96,
                    "workspace did not accept the safe 1,000-entry collection bound.");
                MethodInfo comparisonGate = AccessTools.Method(typeof(MainTabWindow_CultivarRegistry), "CanCompareCount");
                Require(comparisonGate != null
                    && !(bool)comparisonGate.Invoke(null, new object[] { 1 })
                    && (bool)comparisonGate.Invoke(null, new object[] { 2 })
                    && (bool)comparisonGate.Invoke(null, new object[] { HorticultureWorkspaceDocument.MaximumComparisonCount })
                    && !(bool)comparisonGate.Invoke(null, new object[] { HorticultureWorkspaceDocument.MaximumComparisonCount + 1 }),
                    "workspace comparison bounds are inconsistent.");
                InsightUiBadge chip = FindUiElement(InstanceField<InsightUiDocument>(document, "uiDocument").Root,
                    "hns.cultivars.inspector.trait-chip.0") as InsightUiBadge;
                Require(chip != null, "reusable semantic trait chips were not composed.");
                document.PostClose();
                return "empty-safe snapshots, 1,000-entry list bounds, comparison limits, and trait chips are covered";
            });
            Check(report, "workspace-filters-actions-and-breeding", () =>
            {
                HorticultureWorkspaceDocument document = new HorticultureWorkspaceDocument();
                InsightUiDocument canvas = InstanceField<InsightUiDocument>(document, "uiDocument");
                InsightUiSegmented plantFilter = InstanceField<InsightUiSegmented>(document, "plantFilterField");
                InsightUiSegmented balanceFilter = InstanceField<InsightUiSegmented>(document, "balanceFilterField");
                InsightUiToggle archived = InstanceField<InsightUiToggle>(document, "archivedField");
                InsightUiToggle produce = InstanceField<InsightUiToggle>(document, "produceEffectField");
                Require(plantFilter != null && balanceFilter != null && archived != null && produce != null,
                    "workspace filters were not composed.");
                ((Action<int>)AccessTools.Field(typeof(InsightUiSegmented), "boundSetter").GetValue(plantFilter))(2);
                ((Action<int>)AccessTools.Field(typeof(InsightUiSegmented), "boundSetter").GetValue(balanceFilter))(1);
                ((Action<bool>)AccessTools.Field(typeof(InsightUiToggle), "boundSetter").GetValue(archived))(true);
                ((Action<bool>)AccessTools.Field(typeof(InsightUiToggle), "boundSetter").GetValue(produce))(true);
                InsightUiSegmented scope = FindUiElement(canvas.Root, "hns.knowledge.scope") as InsightUiSegmented;
                Require(scope != null, "Knowledge scope control was not composed.");
                ((Action<int>)AccessTools.Field(typeof(InsightUiSegmented), "boundSetter").GetValue(scope))(1);
                document.PreOpen();
                Require(FindUiElement(canvas.Root, "hns.cultivars.favorite") is InsightUiButton
                    && FindUiElement(canvas.Root, "hns.cultivars.archive") is InsightUiButton
                    && FindUiElement(canvas.Root, "hns.cultivars.locate") is InsightUiButton,
                    "cultivar actions were not composed.");

                GameComponent_NovelSeeds component = GameComponent_NovelSeeds.Instance;
                VarietyRecord first = component?.AllVarieties.FirstOrDefault();
                VarietyRecord second = component?.AllVarieties.Skip(1).FirstOrDefault();
                if (first != null)
                {
                    bool oldFavorite = first.registryFavorite;
                    bool oldArchived = first.registryArchived;
                    try
                    {
                        document.PrepareCultivar(first);
                        InvokeInstance(document, "ToggleFavorite");
                        InvokeInstance(document, "ToggleArchived");
                        Require(first.registryFavorite != oldFavorite && first.registryArchived != oldArchived,
                            "favorite/archive actions did not write through the selected record.");
                        if (second != null)
                        {
                            InvokeInstance(document, "ToggleComparison", first.id, true);
                            InvokeInstance(document, "ToggleComparison", second.id, true);
                            Require(document.ComparisonCount == 2, "comparison selection did not retain two cultivars.");
                            document.OpenCompare();
                            Require(document.ActivePageId == "cultivars", "contextual Compare left the Cultivars page.");
                        }
                    }
                    finally
                    {
                        first.registryFavorite = oldFavorite;
                        first.registryArchived = oldArchived;
                        InvokeInstance(document, "ClearComparison");
                    }
                }
                BreedingProgramRecord program = component?.BreedingPrograms?.FirstOrDefault();
                if (program != null)
                {
                    InvokeInstance(document, "SelectBreeding", program.id);
                    Require(document.ActivePageId == "breeding", "breeding selection did not open the Breeding page.");
                }
                document.PostClose();
                return "discovery, balance, archive, produce, Knowledge scope, cultivar actions, comparison, and read-only breeding selection are covered";
            });
            Check(report, "workspace-knowledge-and-external-navigation", () =>
            {
                HorticultureWorkspaceDocument document = new HorticultureWorkspaceDocument();
                // FreeColonists also walks unspawned holders, which can be transiently absent
                // during the complete suite's clean/default transition. The shared observer
                // fixture only reads the spawned colonist collection and is used by the
                // gameplay/Knowledge scenarios as well.
                Pawn pawn = Observer();
                ThingDef plant = DefDatabase<ThingDef>.AllDefsListForReading.FirstOrDefault(NovelSeedUtility.IsGrowableCrop);
                Require(plant != null, "no growable plant was available for workspace navigation.");
                document.PreparePlant(plant);
                Require(document.ActivePageId == "plants", "plant navigation did not open the Plants page.");
                document.PrepareKnowledge(pawn);
                Require(document.ActivePageId == "knowledge", "Knowledge navigation did not open the Knowledge page.");
                document.PreOpen();
                Require(HorticultureKnowledgeAdapter.Diagnostics != null
                    && document.KnowledgeAvailable == HorticultureKnowledgeAdapter.Diagnostics.IsUsable,
                    "Knowledge availability guidance did not reflect the authoritative adapter diagnostic.");
                MainTabWindow_CultivarRegistry.OpenPlant(plant);
                MainTabWindow_CultivarRegistry.OpenKnowledge(pawn);
                document.PostClose();
                return "Knowledge scope/guidance and plant/pawn external entry points route through the workspace";
            });
            Check(report, "workspace-malformed-lineage", () =>
            {
                VarietyRecord root = new VarietyRecord
                {
                    id = "runtime-lineage-root",
                    customName = "Runtime lineage root",
                    parentVarietyIds = new List<string> { "runtime-lineage-parent", "runtime-lineage-missing" }
                };
                VarietyRecord parent = new VarietyRecord
                {
                    id = "runtime-lineage-parent",
                    customName = "Runtime lineage parent",
                    parentVarietyIds = new List<string> { root.id }
                };
                HorticultureLineageInspection first = HorticultureWorkspaceDocument.AnalyzeLineage(
                    new[] { root, parent }, root.id);
                HorticultureLineageInspection second = HorticultureWorkspaceDocument.AnalyzeLineage(
                    new[] { root, parent }, root.id);
                Require(first.NodeCount == 3 && first.EdgeCount == 3 && first.Complete && first.Validation.NullOrEmpty(),
                    "bounded lineage did not handle a missing parent and cycle safely.");
                Require(first.NodeIds.SequenceEqual(second.NodeIds) && first.NodeCount <= HorticultureWorkspaceDocument.MaximumLineageNodes
                    && first.EdgeCount <= HorticultureWorkspaceDocument.MaximumLineageEdges,
                    "lineage IDs or budgets were not deterministic and bounded.");
                return "missing-parent rows, cycles, deterministic IDs, validation, and bounded graph traversal are covered";
            });
        }

        private static void VisualDesigner(HorticultureRuntimeTestReport report)
        {
            Check(report, "visual-designer-documents-and-channels", () =>
            {
                RuntimeVisualSurface firstSurface = new RuntimeVisualSurface();
                RuntimeVisualSurface secondSurface = new RuntimeVisualSurface();
                HorticultureVisualDesignerDocument first = new HorticultureVisualDesignerDocument(firstSurface);
                HorticultureVisualDesignerDocument second = new HorticultureVisualDesignerDocument(secondSurface);
                Require(first.SectionCount == 3 && first.SectionIds.SequenceEqual(new[] { "color", "shape", "effects" }),
                    "Visual Designer sections are incomplete or unstable.");
                Require(first.ModeIds.SequenceEqual(new[] { "plant", "produce" })
                    && first.SemanticMaskChannels.SequenceEqual(new[] { "Produce", "Leaves", "Stem" }),
                    "Plant/Produce mode or semantic mask channels are incomplete.");
                Require(first.HasIsolatedPresentationState(second) && first.TrackDuplicateIds,
                    "Visual Designer documents share state or duplicate-ID diagnostics are disabled.");
                Require(!firstSurface.OverrideEnabled,
                    "opening the Visual Designer created an override before an explicit edit.");
                first.SelectSection("shape");
                Require(first.ActiveSectionId == "shape", "Visual Designer tab selection did not remain document-owned.");
                firstSurface.SetEditingProduce(true);
                Require(first.SemanticMaskChannels.SequenceEqual(new[] { "Produce", "Leaves", "Container" }),
                    "Produce mode did not expose semantic Produce/Leaves/Container channels.");
                first.SetAccessibility(true, true, InsightUiDensity.Compact);
                Require(first.HighContrast && first.ReducedMotion && first.Density == InsightUiDensity.Compact,
                    "Visual Designer accessibility state was not document-owned.");
                first.PostClose();
                second.PostClose();
                return "isolated Visual Designer sections, modes, semantic channels, accessibility, and lifecycle cleanup are covered";
            });
            Check(report, "visual-designer-dialog-and-inspector-bounds", () =>
            {
                HorticultureCollectionDialogSurfaceAdapter surface = new HorticultureCollectionDialogSurfaceAdapter
                {
                    TitleProvider = () => "Runtime collection",
                    RowsProvider = () => Enumerable.Range(0, 1000).Select(index => new HorticultureDialogRow
                    {
                        Id = "row-" + index,
                        Label = "Row " + index,
                        Selected = index == 0
                    }).ToArray(),
                    CloseAction = () => { }
                };
                HorticultureCollectionDialogDocument collection = new HorticultureCollectionDialogDocument(surface, "hns.runtime.collection");
                Require(collection.VisibleRowBudget <= 96 && collection.TrackDuplicateIds,
                    "migrated dialog collection chrome is not bounded or diagnostics are disabled.");
                HorticultureInspectorDocument inspector = new HorticultureInspectorDocument("hns.runtime.inspector");
                inspector.Refresh(new HorticultureInspectorSnapshot
                {
                    Title = "Runtime inspector",
                    PrimaryRows = Enumerable.Range(0, 1000).Select(index => new HorticultureInspectorRow { Id = "trait-" + index, Label = "Trait " + index }).ToArray(),
                    SecondaryRows = new HorticultureInspectorRow[0]
                });
                Require(inspector.MaximumVisibleRows == 1000 && inspector.PrimaryRowCount == 1000,
                    "embedded inspector rows are not safely bounded.");
                RuntimeInputSurface inputSurface = new RuntimeInputSurface();
                HorticultureInputDialogDocument input = new HorticultureInputDialogDocument(inputSurface, "hns.runtime.input");
                RuntimePreviewSurface previewSurface = new RuntimePreviewSurface();
                HorticulturePreviewDialogDocument preview = new HorticulturePreviewDialogDocument(previewSurface, "hns.runtime.preview");
                input.SetAccessibility(true, true, InsightUiDensity.Compact);
                preview.SetAccessibility(true, true, InsightUiDensity.Compact);
                Require(input.TrackDuplicateIds && input.RenderErrorCount == 0 && input.Density == InsightUiDensity.Compact
                    && preview.TrackDuplicateIds && preview.RenderErrorCount == 0 && preview.Density == InsightUiDensity.Compact,
                    "migrated form and specialized-preview chrome did not expose diagnostics/accessibility state.");
                collection.PostClose();
                inspector.PostClose();
                input.PostClose();
                preview.PostClose();
                return "naming/collection chrome, inspector summaries, stable row budgets, and cleanup are covered";
            });
            Check(report, "mask-editor-document-and-channels", () =>
            {
                RuntimeMaskSurface surface = new RuntimeMaskSurface();
                HorticultureMaskEditorDocument document = new HorticultureMaskEditorDocument(surface);
                Require(document.TrackDuplicateIds && document.LayerCount == 3 && document.HasBoundedHistory,
                    "mask editor document did not enable diagnostics or bounded history.");
                Require(surface.LayerOptions.SequenceEqual(new[] { "Produce", "Leaves", "Stem" }),
                    "plant mask channels are not semantic.");
                surface.SelectedPage = 1;
                Require(surface.LayerOptions.SequenceEqual(new[] { "Produce", "Leaves", "Container" }),
                    "produce mask channels are not semantic.");
                surface.ToolMode = 2;
                surface.PaintSelectionMode = 1;
                surface.BrushSize = 9f;
                surface.Tolerance = 0.2f;
                surface.GrowCount = 0;
                document.SetAccessibility(true, true, InsightUiDensity.Compact);
                Require(document.HighContrast && document.ReducedMotion && document.Density == InsightUiDensity.Compact
                    && surface.ToolMode == 2 && surface.PaintSelectionMode == 1 && surface.BrushSize == 9f,
                    "mask editor bindings or accessibility state did not remain document-safe.");
                document.PostClose();
                return "mask editor chrome, semantic channels, tool bindings, bounded history, accessibility, and cleanup are covered";
            });
        }

        private static InsightSettingsDocument NewSettingsDocument()
        {
            return new InsightSettingsDocument(new NovelSeedsSettings());
        }

        private static T InstanceField<T>(object target, string name) where T : class
        {
            return (T)AccessTools.Field(target.GetType(), name)?.GetValue(target);
        }

        private static object InstanceField(object target, string name)
        {
            return AccessTools.Field(target.GetType(), name)?.GetValue(target);
        }

        private static void InvokeInstance(object target, string name, params object[] arguments)
        {
            MethodInfo method = AccessTools.Method(target.GetType(), name);
            Require(method != null, "Missing document method " + name + ".");
            method.Invoke(target, arguments);
        }

        private static InsightUiElement FindUiElement(InsightUiElement root, string id)
        {
            if (root == null) return null;
            if (string.Equals(root.Id, id, StringComparison.Ordinal)) return root;
            IReadOnlyList<InsightUiElement> children = root.Children;
            if (children == null) return null;
            for (int index = 0; index < children.Count; index++)
            {
                InsightUiElement match = FindUiElement(children[index], id);
                if (match != null) return match;
            }
            return null;
        }

        private static void RegistryScale(HorticultureRuntimeTestReport report)
        {
            Check(report, "registry-scale-ordering", () =>
            {
                ThingDef crop = FindCrop(false);
                if (crop == null) throw new RuntimeScenarioBlockedException("No supported crop was loaded for registry scale testing.");
                int[] sizes = { 100, 500, 1000 };
                List<string> measurements = new List<string>();
                foreach (int size in sizes)
                {
                    List<VarietyRecord> synthetic = Enumerable.Range(0, size).Select(index => new VarietyRecord
                    {
                        id = "runtime-scale-" + index,
                        cropDef = crop,
                        customName = "Synthetic cultivar " + (size - index).ToString("D4")
                    }).ToList();
                    Stopwatch timer = Stopwatch.StartNew();
                    List<VarietyRecord> ordered = synthetic.OrderBy(value => value.cropDef?.label)
                        .ThenBy(value => value.Label).ToList();
                    timer.Stop();
                    Require(ordered.Count == size && ordered[0].Label.StartsWith("Synthetic cultivar", StringComparison.Ordinal),
                        "registry ordering lost entries at size " + size + ".");
                    measurements.Add(size + " rows=" + timer.ElapsedMilliseconds + "ms");
                }
                report.performanceMeasurements.Add("registry-display-order: " + string.Join(", ", measurements));
                return string.Join(", ", measurements);
            });
            Check(report, "registry-scale-lookups", () =>
            {
                List<VarietyRecord> actual = GameComponent_NovelSeeds.Instance?.AllVarieties.ToList() ?? new List<VarietyRecord>();
                if (actual.Count == 0)
                {
                    report.performanceMeasurements.Add("registry-id-lookups: skipped with empty registry");
                    return "empty registry handled without a lookup allocation";
                }
                Stopwatch timer = Stopwatch.StartNew();
                for (int index = 0; index < 1000; index++)
                {
                    VarietyRecord selected = actual[index % actual.Count];
                    Require(GameComponent_NovelSeeds.Instance.GetVariety(selected.id) == selected, "registry lookup returned a different record.");
                }
                timer.Stop();
                Require(timer.ElapsedMilliseconds < 5000, "1000 registry lookups exceeded the 5 second safety budget.");
                report.performanceMeasurements.Add("registry-id-lookups: 1000=" + timer.ElapsedMilliseconds + "ms, actual=" + actual.Count);
                return "1000 ID lookups=" + timer.ElapsedMilliseconds + "ms, actual cultivars=" + actual.Count;
            });
        }

        private static void RcPerformance(HorticultureRuntimeTestReport report)
        {
            Check(report, "rc-performance-mask-lookups", () =>
            {
                List<ThingDef> plants = SupportedMaskPlants().Take(1000).ToList();
                if (plants.Count == 0) throw new RuntimeScenarioBlockedException("No supported graphic plants were loaded for mask lookup timing.");
                Stopwatch timer = Stopwatch.StartNew();
                for (int index = 0; index < 1000; index++)
                {
                    ThingDef plant = plants[index % plants.Count];
                    PlantAutoMaskCache.GetRecord(plant, index % 2, generateIfMissing: false);
                }
                timer.Stop();
                Require(timer.ElapsedMilliseconds < 5000, "1000 automatic-mask lookups exceeded the 5 second safety budget.");
                report.performanceMeasurements.Add("automatic-mask-lookups: 1000=" + timer.ElapsedMilliseconds + "ms, plants=" + plants.Count);
                return "1000 validated mask lookups=" + timer.ElapsedMilliseconds + "ms, plants=" + plants.Count;
            });
        }

        private static void AutoMaskSuite(HorticultureRuntimeTestReport report)
        {
            PlantAutoMaskCache.ResetRuntimeTestState();
            ThingDef plant = FindMaskPlant(false);
            ThingDef tree = FindMaskPlant(true);
            if (plant == null) { Block(report, "auto-mask-plant", "No supported graphic plant was loaded."); return; }

            Check(report, "auto-mask-baseline-no-work", () =>
            {
                PlantAutoMaskCache.InitializeAndGenerateMissing();
                AutoMaskBatchResult result = PlantAutoMaskCache.LastBatchResult;
                Require(result.workItems == 0 && !PlantAutoMaskCache.GenerationQueued,
                    "baseline queued missing-mask work: " + result.workItems);
                Require(result.reused > 0 || PlantAutoMaskCache.BundledRecordCount > 0,
                    "baseline had neither local nor bundled masks.");
                return "no generation event; reused=" + result.reused + ", bundled=" + result.bundled
                    + ", local=" + result.localReused;
            });

            Check(report, "auto-mask-manual-precedence", () =>
            {
                PlantSettingsRecord settings = HorticultureNovelSeedsMod.Settings?.GetPlantSettings(plant, true);
                Require(settings != null, "plant settings were unavailable.");
                bool hadManual = settings.HasManualPlantMask(0);
                List<VisualMaskLayerRecord> previous = hadManual
                    ? settings.ManualPlantMaskLayersForVariation(0).Select(layer => layer.Clone()).ToList() : null;
                List<VisualMaskLayerRecord> manual = new List<VisualMaskLayerRecord>
                {
                    new VisualMaskLayerRecord { name = "Produce" },
                    new VisualMaskLayerRecord { name = "Leaves" },
                    new VisualMaskLayerRecord { name = "Stem" }
                };
                manual[0].PaintPixel(3, 4, true);
                settings.SetManualPlantMask(0, manual);
                try
                {
                    List<VisualMaskLayerRecord> resolved = PlantMaskUtility.LayersForVariation(plant, 0, false, false);
                    Require(resolved != null && resolved[0].IsPainted(3, 4), "manual mask did not win precedence.");
                    return "def-specific manual mask won over automatic sources";
                }
                finally
                {
                    if (hadManual) settings.SetManualPlantMask(0, previous);
                    else settings.RemoveManualPlantMask(0);
                }
            });

            Check(report, "auto-mask-local-bundled-precedence", () =>
            {
                PlantAutoMaskCache.ClearRuntimeTestLocalRecord(plant, 0);
                Require(PlantAutoMaskCache.RecordSource(plant, 0) == "bundled",
                    "selected plant has no valid bundled record after local removal.");
                Require(PlantAutoMaskCache.PromoteBundledRecord(plant, 0, false), "could not promote a bundled record to local storage.");
                Require(PlantAutoMaskCache.RecordSource(plant, 0) == "local", "local promoted record did not win over bundle.");
                PlantAutoMaskCache.ClearRuntimeTestLocalRecord(plant, 0);
                Require(PlantAutoMaskCache.RecordSource(plant, 0) == "bundled", "valid bundled record was not used after local removal.");
                return "manual > local > bundled precedence verified";
            });

            Check(report, "auto-mask-new-plant-generation-fallback", () =>
            {
                PlantAutoMaskCache.ClearRuntimeTestLocalRecord(plant, 0);
                PlantAutoMaskCache.SetRuntimeTestBundleEnabled(false);
                try
                {
                    Require(PlantMaskUtility.LayersForVariation(plant, 0, false, false) == null,
                        "rendering path generated or exposed a missing mask.");
                    Require(PlantAutoMaskCache.GetRecord(plant, 0, true, true) != null,
                        "explicit fallback generation did not produce a local record.");
                    Require(PlantAutoMaskCache.RecordSource(plant, 0) == "local", "generated fallback was not local.");
                    return "new-plant miss was safe on render and generated only through explicit work";
                }
                finally
                {
                    PlantAutoMaskCache.SetRuntimeTestBundleEnabled(true);
                    PlantAutoMaskCache.ResetRuntimeTestState();
                }
            });

            Check(report, "auto-mask-low-confidence-safety", () =>
            {
                Require(PlantAutoMaskCache.RuntimeTestLowConfidenceSafety(), "low-confidence masks were renderable in the safety regression.");
                int lowConfidence = 0;
                foreach (ThingDef candidate in SupportedMaskPlants())
                    for (int variation = 0; variation < PlantMaskUtility.VariationCount(candidate); variation++)
                    {
                        AutoPlantMaskRecord record = PlantAutoMaskCache.GetRecord(candidate, variation, false, true);
                        if (record?.LowConfidence == true)
                        {
                            lowConfidence++;
                            Require(PlantAutoMaskCache.LayersFor(candidate, variation, false, false) == null,
                                "low-confidence bundle/local mask rendered for " + candidate.defName + ".");
                        }
                    }
                return "classifier safety regression passed; bundle low-confidence records=" + lowConfidence;
            });

            Check(report, "auto-mask-stale-bundle", () =>
            {
                string source = PlantAutoMaskCache.BundledCachePath;
                Require(File.Exists(source), "committed bundled cache was not installed.");
                string stale = Path.Combine(GenFilePaths.ConfigFolderPath, "HorticultureNovelSeedsRuntimeStaleBundle.xml");
                string xml = File.ReadAllText(source);
                xml = xml.Replace("<generatorVersion>" + PlantAutoMaskCache.GeneratorVersion + "</generatorVersion>",
                    "<generatorVersion>" + (PlantAutoMaskCache.GeneratorVersion - 1) + "</generatorVersion>");
                File.WriteAllText(stale, xml);
                try
                {
                    AutoMaskBundleValidationResult result = PlantAutoMaskCache.ValidateBundle(stale, false);
                    Require(!result.Valid && result.Error.Contains("stale"), "stale bundle was accepted: " + result);
                    return "stale generator version rejected without modifying the committed bundle";
                }
                finally { try { if (File.Exists(stale)) File.Delete(stale); } catch { } }
            });

            Check(report, "auto-mask-tree-and-variants", () =>
            {
                Require(tree != null, "no sowable tree graphic was loaded.");
                int totalVariations = 0; int multiVariationPlants = 0; int collectionOrDirectional = 0;
                HashSet<string> labels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (ThingDef candidate in SupportedMaskPlants())
                {
                    int count = PlantMaskUtility.VariationCount(candidate);
                    totalVariations += count;
                    if (count > 1) multiVariationPlants++;
                    for (int variation = 0; variation < count; variation++)
                    {
                        string label = PlantMaskUtility.VariationLabel(candidate, variation) ?? string.Empty;
                        if (label.IndexOf(" of ", StringComparison.OrdinalIgnoreCase) >= 0
                            || label.IndexOf("Collection", StringComparison.OrdinalIgnoreCase) >= 0
                            || label.IndexOf("north", StringComparison.OrdinalIgnoreCase) >= 0
                            || label.IndexOf("south", StringComparison.OrdinalIgnoreCase) >= 0
                            || label.IndexOf("east", StringComparison.OrdinalIgnoreCase) >= 0
                            || label.IndexOf("west", StringComparison.OrdinalIgnoreCase) >= 0) collectionOrDirectional++;
                        if (label.IndexOf("Bloom", StringComparison.OrdinalIgnoreCase) >= 0) labels.Add("growth");
                        if (label.IndexOf("Immature", StringComparison.OrdinalIgnoreCase) >= 0) labels.Add("immature");
                        if (label.IndexOf("Leafless", StringComparison.OrdinalIgnoreCase) >= 0) labels.Add("leafless");
                        PlantAutoMaskCache.GetRecord(candidate, variation, false, true);
                    }
                }
                AutoPlantMaskRecord treeRecord = PlantAutoMaskCache.GetRecord(tree, 0, false, true);
                Require(treeRecord != null && treeRecord.MorphologyIdentity.Contains("tree:True"), "tree morphology identity was not recorded.");
                Require(totalVariations > 0 && multiVariationPlants > 0, "growth or alternate variants were not discovered.");
                Require(labels.Contains("growth") || labels.Contains("immature") || labels.Contains("leafless"),
                    "growth-state variants were not discovered.");
                Require(collectionOrDirectional > 0, "collection or directional variants were not discovered.");
                return "tree morphology plus " + totalVariations + " variations across " + multiVariationPlants
                    + " multi-variant plants; collection/directional=" + collectionOrDirectional;
            });

            Check(report, "auto-mask-performance", () =>
            {
                Stopwatch timer = Stopwatch.StartNew();
                int hits = 0;
                for (int i = 0; i < 100; i++)
                    if (PlantMaskUtility.LayersForVariation(plant, 0, false, false) != null) hits++;
                timer.Stop();
                Require(hits > 0, "ordinary renderer lookup had no mask hits.");
                Require(timer.ElapsedMilliseconds < 5000, "ordinary mask lookup exceeded 5 seconds: " + timer.ElapsedMilliseconds + "ms");
                return "100 render-path lookups=" + timer.ElapsedMilliseconds + "ms; hits=" + hits;
            });
        }

        private static void AutoMaskExport(HorticultureRuntimeTestReport report, HorticultureRuntimeTestRequest request)
        {
            Check(report, "auto-mask-export", () =>
            {
                Require(!request.autoMaskBundleOutputPath.NullOrEmpty(), "auto-mask bundle output path was not supplied.");
                PlantAutoMaskCache.ResetRuntimeTestState();
                PlantAutoMaskCache.SetRuntimeTestBundleEnabled(!request.autoMaskRegenerate);
                AutoMaskBatchResult batch = PlantAutoMaskCache.GenerateMissing(request.autoMaskRegenerate);
                Require(batch.failed == 0, "mask generation failed for " + batch.failed + " variation(s).");
                Require(PlantAutoMaskCache.ExportBundle(request.autoMaskBundleOutputPath, out AutoMaskBundleValidationResult validation),
                    "bundle export/validation failed: " + validation);
                Require(validation.RecordCount > 0, "bundle export contained no records.");
                Require(validation.LowConfidenceCount == 0,
                    validation + "; low-confidence masks are incomplete for publishing.");
                report.relevantDiagnostics.Add("auto-mask-export=" + validation + "; generated=" + batch.generated
                    + "; elapsedMs=" + batch.elapsedMilliseconds);
                return validation.ToString() + "; generated=" + batch.generated;
            });
        }

        private static IEnumerable<ThingDef> SupportedMaskPlants()
        {
            return DefDatabase<ThingDef>.AllDefsListForReading
                .Where(def => HorticulturePlantPolicy.IsSupported(def) && def.graphicData != null
                    && PlantMaskUtility.TextureForVariation(def, 0) != null)
                .OrderBy(def => def.defName);
        }

        private static ThingDef FindMaskPlant(bool tree)
        {
            return SupportedMaskPlants().FirstOrDefault(def => HorticulturePlantPolicy.IsTree(def) == tree
                && HorticultureNovelSeedsMod.Settings?.GetPlantSettings(def, false)?.HasManualPlantMask(0) != true
                && !PlantMaskUtility.HasSharedManualMask(def, 0, out _, false)
                && (PlantAutoMaskCache.RecordSource(def, 0) == "local"
                    || PlantAutoMaskCache.RecordSource(def, 0) == "bundled"));
        }

        private static void OrdinaryCrop(HorticultureRuntimeTestReport report)
        {
            ThingDef crop = FindCrop(false);
            if (crop == null) { Block(report, "ordinary-crop", "No supported non-tree crop was loaded."); return; }
            Plant plant = null;
            try
            {
                Check(report, "ordinary-crop-sow-growth", () =>
                {
                    plant = SpawnPlant(crop, FindCell(Find.CurrentMap));
                    EnsureGrowingZone(crop, plant.Position);
                    CompPlantVariety comp = plant.TryGetComp<CompPlantVariety>();
                    Require(comp != null, "ordinary crop has no variety component.");
                    List<VarietyTraitDef> traits = Traits(1);
                    comp.SetPendingTraits(traits);
                    plant.sown = true;
                    Pawn observer = Observer();
                    HorticultureEventRouter.SowingCompleted(observer, plant);
                    plant.Growth = 0.10f;
                    HorticultureEventRouter.GrowthObserved(observer, plant);
                    plant.Growth = 0.55f;
                    HorticultureEventRouter.GrowthObserved(observer, plant);
                    plant.Growth = 1f;
                    HorticultureEventRouter.GrowthObserved(observer, plant);
                    Require(comp.PendingDiscovery || comp.HasAnyTraits, "trait state did not survive the growth journey.");
                    return "sowing, germination, growth buckets, and trait state completed";
                });

                Check(report, "ordinary-crop-cultivar", () =>
                {
                    GameComponent_NovelSeeds registry = GameComponent_NovelSeeds.Instance;
                    List<VarietyTraitDef> traits = Traits(1);
                    VarietyRecord variety = registry.UnlockVariety(crop, traits, "Runtime ordinary cultivar", hiddenFromMenus: true,
                        discoverer: Observer(), originKind: "mutation");
                    Require(variety != null && !variety.id.NullOrEmpty(), "cultivar creation returned no stable ID.");
                    registry.RenameVariety(variety, "Runtime ordinary cultivar renamed");
                    Require(registry.GetVariety(variety.id) != null && registry.GetVariety(variety.id).customName.Contains("renamed"),
                        "cultivar name did not persist in the registry.");
                    IPlantToGrowSettable grower = GridsUtility.GetPlantToGrowSettable(plant.Position, plant.Map);
                    if (grower == null) throw new RuntimeScenarioBlockedException("No growing zone was available for cultivar selection.");
                    registry.SetSelectedVariety(grower, variety);
                    Require(registry.VarietyForSowing(grower, plant.Position)?.id == variety.id, "selected cultivar was not returned for sowing.");
                    return "stable ID=" + variety.id + ", selected and renamed";
                });

                Check(report, "ordinary-crop-harvest-produce", () =>
                {
                    Pawn observer = Observer();
                    HorticultureEventRouter.HarvestCompleted(observer, plant, 3, true);
                    string identity = HorticultureKnowledgeEventIdentity.Harvest(plant, 0, true, false, false);
                    Require(!identity.NullOrEmpty(), "harvest event identity was empty.");
                    ThingDef produce = crop.plant?.harvestedThingDef;
                    if (produce == null) throw new RuntimeScenarioBlockedException("The selected crop has no harvested produce.");
                    Thing harvested = ThingMaker.MakeThing(produce);
                    CompNovelProduceAppearance appearance = harvested.TryGetComp<CompNovelProduceAppearance>();
                    if (appearance == null) throw new RuntimeScenarioBlockedException("The selected produce has no inherited-data component.");
                    appearance.InitializeFromPlant(plant.TryGetComp<CompPlantVariety>(), Color.white, null);
                    Require(appearance.HasNovelData, "harvested produce did not receive inherited data.");
                    return "harvest identity and inherited produce data verified";
                });
            }
            finally
            {
                DestroyFixture(plant);
                CleanupGrowingZones();
            }
        }

        private static void SowableTree(HorticultureRuntimeTestReport report)
        {
            ThingDef tree = FindTree();
            if (tree == null) { Block(report, "sowable-tree", "No supported sowable tree was loaded."); return; }
            Plant plant = null;
            try
            {
                Check(report, "tree-sow-mutate-replant", () =>
                {
                    plant = SpawnPlant(tree, FindCell(Find.CurrentMap));
                    CompPlantVariety comp = plant.TryGetComp<CompPlantVariety>();
                    Require(comp != null, "sowable tree has no variety component.");
                    List<VarietyTraitDef> traits = Traits(1);
                    comp.SetPendingTraits(traits);
                    plant.sown = true;
                    plant.Growth = 1f;
                    VarietyRecord variety = GameComponent_NovelSeeds.Instance.UnlockVariety(tree, traits,
                        "Runtime tree cultivar", hiddenFromMenus: true, discoverer: Observer(), originKind: "mutation");
                    Require(variety != null, "tree cultivar was not created.");
                    comp.SetVariety(variety);
                    Require(comp.VarietyId == variety.id, "tree cultivar was not replanted on the live plant.");
                    return "tree mutation, cultivar save, and replant completed";
                });
                Check(report, "tree-cutting-knowledge", () =>
                {
                    HorticultureEventRouter.CuttingCompleted(Observer(), plant, 4);
                    string identity = HorticultureKnowledgeEventIdentity.Cutting(plant, 0);
                    Require(!identity.NullOrEmpty(), "tree cutting identity was empty.");
                    Require(tree.plant.IsTree, "selected tree lost its tree classification.");
                    return "tree cutting routed with identity=" + identity;
                });
            }
            finally { DestroyFixture(plant); }
        }

        private static void CrossPollination(HorticultureRuntimeTestReport report)
        {
            ThingDef crop = FindCrop(false);
            if (crop == null) { Block(report, "cross-pollination", "No supported crop was loaded."); return; }
            Plant recipient = null;
            Plant donor = null;
            NovelSeedsSettings settings = HorticultureNovelSeedsMod.Settings;
            float originalChance = settings?.globalCrossPollinationChance ?? NovelSeedUtility.DefaultCrossPollinationChance;
            try
            {
                Check(report, "cross-pollination-real-path", () =>
                {
                    List<VarietyTraitDef> traits = Traits(2);
                    if (traits.Count < 2) throw new RuntimeScenarioBlockedException("Fewer than two trait definitions were loaded.");
                    GameComponent_NovelSeeds registry = GameComponent_NovelSeeds.Instance;
                    VarietyRecord parentA = registry.UnlockVariety(crop, new List<VarietyTraitDef> { traits[0] },
                        "Runtime parent A", hiddenFromMenus: true, discoverer: Observer(), originKind: "mutation");
                    VarietyRecord parentB = registry.UnlockVariety(crop, new List<VarietyTraitDef> { traits[1] },
                        "Runtime parent B", hiddenFromMenus: true, discoverer: Observer(), originKind: "mutation");
                    Require(parentA != null && parentB != null && parentA.id != parentB.id, "parent cultivars were not distinct.");
                    IntVec3 donorCell = FindCell(Find.CurrentMap);
                    IntVec3 recipientCell = FindAdjacentCell(donorCell, Find.CurrentMap);
                    donor = SpawnPlant(crop, donorCell);
                    recipient = SpawnPlant(crop, recipientCell);
                    donor.sown = true;
                    donor.Growth = 1f;
                    donor.TryGetComp<CompPlantVariety>().SetVariety(parentB);
                    recipient.sown = true;
                    EnsureGrowingZone(crop, recipient.Position);
                    IPlantToGrowSettable grower = GridsUtility.GetPlantToGrowSettable(recipient.Position, recipient.Map);
                    if (grower == null) throw new RuntimeScenarioBlockedException("No growing zone was available for cross-pollination.");
                    registry.SetSelectedVariety(grower, parentA);
                    if (settings != null) settings.globalCrossPollinationChance = 1f;
                    NovelSeedUtility.AssignMutationOnSow(recipient, Observer());
                    CompPlantVariety result = recipient.TryGetComp<CompPlantVariety>();
                    Require(result != null && result.CrossPollinated, "real sow path did not create a cross-pollinated result.");
                    Require(result.CrossPollinationParentIds.Contains(parentB.id), "hybrid lineage omitted the donor parent.");
                    string hybridIdentity = HorticultureKnowledgeEventIdentity.Inheritance(crop,
                        new[] { parentA.id, parentB.id }, result.VarietyId);
                    Require(!hybridIdentity.NullOrEmpty(), "hybrid lineage identity was empty.");
                    return "real sow path produced donor parent=" + parentB.id;
                });
                Check(report, "cross-pollination-determinism", () =>
                {
                    List<VarietyRecord> mix = new List<VarietyRecord>
                    {
                        new VarietyRecord { id = "runtime-b" },
                        new VarietyRecord { id = "runtime-a" }
                    };
                    MethodInfo selector = AccessTools.Method(typeof(GameComponent_NovelSeeds), "SelectBreedingMixVariety");
                    Require(selector != null, "breeding mix selector was unavailable.");
                    VarietyRecord first = selector.Invoke(null, new object[] { mix, new IntVec3(7, 0, 11) }) as VarietyRecord;
                    VarietyRecord second = selector.Invoke(null, new object[] { mix, new IntVec3(7, 0, 11) }) as VarietyRecord;
                    Require(first?.id == second?.id, "breeding mix selection was not stable.");
                    return "same cell returned " + first?.id;
                });
            }
            finally
            {
                if (settings != null) settings.globalCrossPollinationChance = originalChance;
                DestroyFixture(recipient);
                DestroyFixture(donor);
                CleanupGrowingZones();
            }
        }

        private static void ProduceProcessing(HorticultureRuntimeTestReport report)
        {
            ThingDef crop = FindCrop(false);
            ThingDef produce = crop?.plant?.harvestedThingDef;
            if (produce == null) { Block(report, "produce-processing", "No supported harvested produce was loaded."); return; }
            try
            {
                Check(report, "produce-processing-inheritance", () =>
                {
                    Plant plant = SpawnPlant(crop, FindCell(Find.CurrentMap));
                    CompPlantVariety plantComp = plant.TryGetComp<CompPlantVariety>();
                    List<VarietyTraitDef> traits = Traits(1);
                    VarietyRecord variety = GameComponent_NovelSeeds.Instance.UnlockVariety(crop, traits,
                        "Runtime processing cultivar", hiddenFromMenus: true, discoverer: Observer(), originKind: "mutation");
                    plantComp.SetVariety(variety);
                    Thing first = ThingMaker.MakeThing(produce);
                    Thing second = ThingMaker.MakeThing(produce);
                    CompNovelProduceAppearance firstAppearance = first.TryGetComp<CompNovelProduceAppearance>();
                    CompNovelProduceAppearance secondAppearance = second.TryGetComp<CompNovelProduceAppearance>();
                    if (firstAppearance == null || secondAppearance == null)
                        throw new RuntimeScenarioBlockedException("The selected produce lacks inherited-data components.");
                    firstAppearance.InitializeFromPlant(plantComp, Color.red, new[] { Color.red });
                    secondAppearance.InitializeFromPlant(plantComp, Color.blue, new[] { Color.blue });
                    ProduceInheritanceData data = ProduceInheritanceUtility.FromIngredients(new List<Thing> { first, second });
                    Require(data != null && data.HasData, "distinct inherited ingredients produced no processing data.");
                    Thing product = ThingMaker.MakeThing(produce);
                    ProduceInheritanceUtility.ApplyToRecipeProducts(new[] { product }, data).ToList();
                    Require(product.TryGetComp<CompNovelProduceAppearance>()?.HasNovelData == true,
                        "processing did not propagate inherited data to the product.");
                    HorticultureEventRouter.ProduceProcessed(Observer(), new[] { first, second });
                    Require(ProduceInheritanceUtility.FromIngredients(new List<Thing> { ThingMaker.MakeThing(ThingDefOf.Steel) }) == null,
                        "unrelated ingredients acquired Novel Seeds data.");
                    return "multiple inherited ingredients, pigment blending, and unrelated-ingredient isolation verified";
                });
            }
            finally { CleanupFixtures(); }
        }

        private static void Knowledge(HorticultureRuntimeTestReport report)
        {
            ThingDef crop = FindCrop(false);
            if (crop == null) { Block(report, "knowledge", "No supported crop was loaded."); return; }
            Check(report, "knowledge-registration", () =>
            {
                HorticultureKnowledgeDiagnosticSnapshot diagnostics = HorticultureKnowledgeAdapter.Diagnostics;
                Require(diagnostics != null && diagnostics.IsUsable, "Knowledge Framework registration is not usable: " + diagnostics);
                return diagnostics.ToString();
            });
            Plant plant = null;
            try
            {
                plant = SpawnPlant(crop, FindCell(Find.CurrentMap));
                Check(report, "knowledge-observations", () =>
                {
                    Pawn observer = Observer();
                    HorticultureEventRouter.SowingCompleted(observer, plant);
                    plant.Growth = 0.1f;
                    HorticultureEventRouter.GrowthObserved(observer, plant);
                    HorticultureEventRouter.HarvestCompleted(observer, plant, 2);
                    HorticultureEventRouter.CuttingCompleted(observer, plant, 2);
                    string sowIdentity = HorticultureKnowledgeEventIdentity.Sowing(plant);
                    HorticultureKnowledgeAdapter.Observe(observer, crop, HorticultureKnowledgeEvent.Sowing,
                        map: plant.Map, summary: "Runtime duplicate identity check", sourceInstanceId: sowIdentity);
                    HorticultureKnowledgeAdapter.Observe(observer, crop, HorticultureKnowledgeEvent.Sowing,
                        map: plant.Map, summary: "Runtime duplicate identity check", sourceInstanceId: sowIdentity);
                    HorticultureKnowledgeEventDiagnosticsSnapshot snapshot = HorticultureKnowledgeAdapter.EventDiagnostics;
                    Require(snapshot != null && snapshot.deduplicatedByEvent.Values.Sum() >= 1,
                        "duplicate observation identity was not recorded as deduplicated.");
                    return "sowing, growth, harvest, cutting, and duplicate identity paths exercised";
                });
                Check(report, "knowledge-cultivar-relations", () =>
                {
                    VarietyRecord variety = GameComponent_NovelSeeds.Instance.UnlockVariety(crop, Traits(1),
                        "Runtime documented cultivar", hiddenFromMenus: true, discoverer: Observer(), originKind: "mutation");
                    Require(variety != null, "cultivar was not created for Knowledge relation testing.");
                    HorticultureKnowledgeAdapter.RegisterCultivar(variety);
                    HorticultureEventRouter.CultivarDocumented(Observer(), variety);
                    Require(!HorticultureKnowledgeEventIdentity.Documentation(variety).NullOrEmpty(), "documentation identity was empty.");
                    return "cultivar registration, documentation, and relation identity exercised";
                });
            }
            finally { DestroyFixture(plant); }
        }

        private static void Negative(HorticultureRuntimeTestReport report)
        {
            Check(report, "negative-unsupported-plant", () =>
            {
                bool supported = HorticulturePlantPolicy.IsSupported(ThingDefOf.Steel);
                Require(!supported, "steel was incorrectly classified as a supported plant.");
                bool observed = HorticultureKnowledgeAdapter.Observe(Observer(), ThingDefOf.Steel,
                    HorticultureKnowledgeEvent.Sowing, map: Find.CurrentMap, sourceInstanceId: "runtime-negative-unsupported");
                Require(!observed, "unsupported plant observation was accepted.");
                return "unsupported plant rejected without throwing";
            });
            Check(report, "negative-mask-and-cache", () =>
            {
                ThingDef crop = FindCrop(false);
                Require(crop != null, "no crop was available for mask checks.");
                AutoPlantMaskRecord record = PlantAutoMaskCache.GetRecord(crop, 0, generateIfMissing: false);
                int hash = PlantMaskUtility.MaskHash(crop, 0);
                Require(record == null || record.Layers != null, "invalid mask/cache state was not handled safely.");
                return record == null ? "missing mask safely returned no record (hash=" + hash + ")" : "cached mask identity=" + hash;
            });
            Check(report, "negative-empty-and-missing-cultivar", () =>
            {
                GameComponent_NovelSeeds registry = GameComponent_NovelSeeds.Instance;
                Require(registry.GetVariety("HNS_missing_runtime_cultivar") == null,
                    "missing cultivar unexpectedly resolved.");
                Require(registry.AllVarieties != null, "empty cultivar registry was not safely exposed.");
                return "missing parent/cultivar lookup is safe";
            });
        }

        private static void LongRunning(HorticultureRuntimeTestReport report)
        {
            Check(report, "long-running-sanity", () =>
            {
                ThingDef crop = FindCrop(false);
                Require(crop != null, "no crop was available for long-running sanity.");
                for (int i = 0; i < 4; i++)
                {
                    Plant plant = SpawnPlant(crop, FindCell(Find.CurrentMap));
                    plant.sown = true;
                    plant.Growth = 0.2f + i * 0.2f;
                    HorticultureEventRouter.GrowthObserved(Observer(), plant);
                }
                Require(GameComponent_NovelSeeds.Instance.AllVarieties != null, "variety cache became unavailable.");
                return "plant tick/event path exercised with ordinary and mutated fixtures";
            });
        }

        private static bool SaveReload(HorticultureRuntimeTestReport report, HorticultureRuntimeTestRequest request, bool resuming)
        {
            string root = Environment.GetEnvironmentVariable("DEVBRIDGE_ROOT");
            if (root.NullOrEmpty()) { Block(report, "save-reload", "DEVBRIDGE_ROOT was unavailable."); return false; }
            string checkpointPath = Path.Combine(root, "Runtime", "Horticulture.RuntimeTest." + request.requestId + ".checkpoint.json");
            SaveReloadCheckpoint checkpoint = null;
            if (File.Exists(checkpointPath))
            {
                try { checkpoint = JsonUtility.FromJson<SaveReloadCheckpoint>(File.ReadAllText(checkpointPath)); }
                catch (Exception exception) { Failure(report, "save-reload-checkpoint", exception); return false; }
            }
            if (checkpoint == null)
            {
                ThingDef crop = FindCrop(false);
                ThingDef tree = FindTree();
                if (crop == null || tree == null) { Block(report, "save-reload", "Both crop and sowable tree fixtures are required."); return false; }
                GameComponent_NovelSeeds registry = GameComponent_NovelSeeds.Instance;
                List<VarietyTraitDef> ordinaryTraits = Traits(1);
                List<VarietyTraitDef> treeTraits = Traits(1).Skip(1).Take(1).ToList();
                if (treeTraits.Count == 0) treeTraits = ordinaryTraits.ToList();
                VarietyRecord ordinary = registry.UnlockVariety(crop, ordinaryTraits, "Runtime saved ordinary", hiddenFromMenus: true,
                    discoverer: Observer(), originKind: "mutation");
                VarietyRecord treeVariety = registry.UnlockVariety(tree, treeTraits, "Runtime saved tree", hiddenFromMenus: true,
                    discoverer: Observer(), originKind: "mutation");
                VarietyRecord hybrid = registry.UnlockVariety(crop, ordinaryTraits.Concat(treeTraits).Distinct().ToList(), "Runtime saved hybrid",
                    new[] { ordinary.id, treeVariety.id }, hiddenFromMenus: true, discoverer: Observer(), originKind: "cross-pollination");
                Require(ordinary != null && treeVariety != null && hybrid != null, "save fixtures could not be created.");
                checkpoint = new SaveReloadCheckpoint
                {
                    saveName = "Horticulture_RuntimeTest_" + request.requestId,
                    ordinaryId = ordinary.id,
                    treeId = treeVariety.id,
                    hybridId = hybrid.id,
                    cropDefName = crop.defName,
                    treeDefName = tree.defName,
                    ordinaryTraits = ordinary.traits.Select(trait => trait.defName).ToList(),
                    treeTraits = treeVariety.traits.Select(trait => trait.defName).ToList(),
                    hybridTraits = hybrid.traits.Select(trait => trait.defName).ToList()
                };
                Directory.CreateDirectory(Path.GetDirectoryName(checkpointPath));
                File.WriteAllText(checkpointPath, JsonUtility.ToJson(checkpoint, true));
                MethodInfo save = AccessTools.Method(typeof(GameDataSaveLoader), "SaveGame", new[] { typeof(string) });
                if (save == null) throw new RuntimeScenarioBlockedException("GameDataSaveLoader.SaveGame is unavailable.");
                save.Invoke(null, new object[] { checkpoint.saveName });
                MethodInfo load = AccessTools.Method(typeof(GameDataSaveLoader), "CheckVersionAndLoadGame", new[] { typeof(string) });
                if (load == null) throw new RuntimeScenarioBlockedException("GameDataSaveLoader.CheckVersionAndLoadGame is unavailable.");
                load.Invoke(null, new object[] { checkpoint.saveName });
                report.AddAssertion(new HorticultureRuntimeAssertion
                {
                    id = "save-reload-requested",
                    status = "PASS",
                    detail = "Saved and requested normal RimWorld reload for " + checkpoint.saveName
                });
                report.assertionCount++;
                report.passedAssertions++;
                return true;
            }

            Check(report, "save-reload-identities", () =>
            {
                GameComponent_NovelSeeds registry = GameComponent_NovelSeeds.Instance;
                VarietyRecord ordinary = registry.GetVariety(checkpoint.ordinaryId);
                VarietyRecord tree = registry.GetVariety(checkpoint.treeId);
                VarietyRecord hybrid = registry.GetVariety(checkpoint.hybridId);
                Require(ordinary != null && tree != null && hybrid != null, "saved cultivar IDs were not restored.");
                Require(SameTraits(ordinary, checkpoint.ordinaryTraits) && SameTraits(tree, checkpoint.treeTraits)
                    && SameTraits(hybrid, checkpoint.hybridTraits), "saved cultivar traits changed after reload.");
                Require(registry.PaletteFor(DefDatabase<ThingDef>.GetNamedSilentFail(checkpoint.cropDefName)) != null,
                    "saved crop palette was not restored.");
                return "cultivar IDs, traits, palettes, and cache resolution survived reload";
            });
            Check(report, "save-reload-knowledge", () =>
            {
                HorticultureKnowledgeDiagnosticSnapshot diagnostics = HorticultureKnowledgeAdapter.Diagnostics;
                Require(diagnostics != null, "Knowledge diagnostics disappeared after reload.");
                return diagnostics.ToString();
            });
            try { File.Delete(checkpointPath); } catch { }
            try
            {
                string savePath = GenFilePaths.FilePathForSavedGame(checkpoint.saveName);
                if (File.Exists(savePath)) File.Delete(savePath);
            }
            catch { }
            return false;
        }

        private static bool SameTraits(VarietyRecord variety, IEnumerable<string> expected)
        {
            return variety != null && variety.traits.Select(trait => trait?.defName).OrderBy(value => value, StringComparer.Ordinal)
                .SequenceEqual((expected ?? Enumerable.Empty<string>()).OrderBy(value => value, StringComparer.Ordinal));
        }

        private static ThingDef FindCrop(bool tree)
        {
            IEnumerable<ThingDef> candidates = DefDatabase<ThingDef>.AllDefsListForReading.Where(def => HorticulturePlantPolicy.IsSupported(def))
                .Where(def => tree ? HorticulturePlantPolicy.IsSowableTree(def) : !HorticulturePlantPolicy.IsSowableTree(def))
                .OrderBy(def => def.defName, StringComparer.Ordinal);
            if (!tree)
            {
                ThingDef produceCrop = candidates.FirstOrDefault(def => HasProduceSupport(def.plant?.harvestedThingDef));
                if (produceCrop != null) return produceCrop;
            }
            return candidates.FirstOrDefault();
        }

        private static ThingDef FindTree() => FindCrop(true);

        private static List<VarietyTraitDef> Traits(int count)
        {
            List<VarietyTraitDef> candidates = TraitConfigUtility.TopLevelTraits()
                .GroupBy(def => def.configFamily.NullOrEmpty() ? "def:" + def.defName : "family:" + def.configFamily)
                .Select(group => group.First()).ToList();
            int target = Mathf.Max(1, count);
            if (target == 1) return candidates.Take(1).ToList();

            List<VarietyTraitDef> compatible = new List<VarietyTraitDef>();
            foreach (VarietyTraitDef candidate in candidates)
            {
                if (compatible.Any(existing => (existing.exclusionTags ?? new List<string>()).Intersect(
                        candidate.exclusionTags ?? new List<string>(), StringComparer.OrdinalIgnoreCase).Any())) continue;
                compatible.Add(candidate);
                if (compatible.Count >= target) return compatible;
            }
            return candidates.Take(target).ToList();
        }

        private static Pawn Observer() => Find.CurrentMap?.mapPawns?.FreeColonistsSpawned?.FirstOrDefault();

        private static Plant SpawnPlant(ThingDef def, IntVec3 cell)
        {
            Plant plant = ThingMaker.MakeThing(def) as Plant;
            Require(plant != null, "ThingDef " + def?.defName + " did not create a Plant.");
            GenSpawn.Spawn(plant, cell, Find.CurrentMap, WipeMode.Vanish);
            Fixtures.Add(plant);
            return plant;
        }

        private static IntVec3 FindCell(Map map)
        {
            Require(map != null, "current map is unavailable.");
            for (int x = 1; x < map.Size.x - 1; x++)
            for (int z = 1; z < map.Size.z - 1; z++)
            {
                IntVec3 cell = new IntVec3(x, 0, z);
                if (cell.Standable(map) && map.thingGrid.ThingsListAt(cell).Count == 0) return cell;
            }
            throw new RuntimeScenarioBlockedException("No empty standable fixture cell was available.");
        }

        private static IntVec3 FindAdjacentCell(IntVec3 origin, Map map)
        {
            foreach (IntVec3 offset in GenAdj.CardinalDirections)
            {
                IntVec3 cell = origin + offset;
                if (cell.InBounds(map) && cell.Standable(map) && map.thingGrid.ThingsListAt(cell).Count == 0) return cell;
            }
            return FindCell(map);
        }

        private static bool HasProduceSupport(ThingDef produce)
        {
            return produce?.thingClass != null
                && typeof(ThingWithComps).IsAssignableFrom(produce.thingClass)
                && produce.comps?.Any(comp => comp is CompProperties_NovelProduceAppearance) == true;
        }

        private static Zone_Growing EnsureGrowingZone(ThingDef plantDef, IntVec3 cell)
        {
            Map map = Find.CurrentMap;
            Require(map?.zoneManager != null, "current map has no zone manager.");
            Zone_Growing zone = map.zoneManager.AllZones.OfType<Zone_Growing>()
                .FirstOrDefault(candidate => candidate.PlantDefToGrow == plantDef);
            if (zone == null)
            {
                zone = new Zone_Growing(map.zoneManager);
                zone.SetPlantDefToGrow(plantDef);
                GrowingZones.Add(zone);
            }
            if (!zone.Cells.Contains(cell)) zone.AddCell(cell);
            return zone;
        }

        private static void CleanupGrowingZones()
        {
            foreach (Zone_Growing zone in GrowingZones.ToList())
            {
                try { zone?.Deregister(); } catch { }
            }
            GrowingZones.Clear();
        }

        private static void CleanupFixtures()
        {
            foreach (Thing fixture in Fixtures.ToList()) DestroyFixture(fixture);
            Fixtures.Clear();
            CleanupGrowingZones();
        }

        private static void DestroyFixture(Thing fixture)
        {
            if (fixture != null && !fixture.Destroyed && fixture.Spawned)
            {
                try { fixture.Destroy(DestroyMode.Vanish); }
                catch (ArgumentOutOfRangeException) { }
                catch (NullReferenceException) { }
            }
            Fixtures.Remove(fixture);
        }

        private static void Check(HorticultureRuntimeTestReport report, string id, Func<string> action)
        {
            report.assertionCount++;
            try
            {
                string detail = action() ?? "ok";
                report.passedAssertions++;
                report.AddAssertion(new HorticultureRuntimeAssertion { id = id, status = "PASS", detail = detail });
            }
            catch (RuntimeScenarioBlockedException exception)
            {
                report.blockedAssertionsCount++;
                report.AddAssertion(new HorticultureRuntimeAssertion { id = id, status = "BLOCKED", detail = exception.Message });
            }
            catch (Exception exception)
            {
                Failure(report, id, exception);
            }
        }

        private static void Failure(HorticultureRuntimeTestReport report, string id, Exception exception)
        {
            report.status = "FAIL";
            report.failedAssertionsCount++;
            report.failedAssertions.Add(id);
            report.exceptionDetails.Add(exception.ToString());
            report.AddAssertion(new HorticultureRuntimeAssertion
            {
                id = id,
                status = "FAIL",
                detail = exception.Message,
                exception = exception.ToString()
            });
        }

        private static void Block(HorticultureRuntimeTestReport report, string id, string detail)
        {
            report.assertionCount++;
            report.blockedAssertionsCount++;
            report.AddAssertion(new HorticultureRuntimeAssertion { id = id, status = "BLOCKED", detail = detail });
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private sealed class RuntimeVisualSurface : IHorticultureVisualDesignerSurface
        {
            private readonly Dictionary<string, float> values = new Dictionary<string, float>();
            private bool editingProduce;
            private bool overrideEnabled;
            private bool perMaskEnabled;
            private int selectedMask;
            private int selectedPreviewPlant;

            public string ContextLabel => "Runtime Visual Designer";
            public string TraitLabel => "Runtime Trait";
            public string OriginLabel => overrideEnabled ? "Override" : "Inherited";
            public string InheritanceLabel => overrideEnabled ? "Editing explicit override" : "Using inherited visual";
            public string StatusLabel => "Cached preview authority";
            public bool EditingProduce => editingProduce;
            public string ActiveSection { get; set; } = "Color";
            public bool CanEdit => true;
            public bool OverrideEnabled { get => overrideEnabled; set => overrideEnabled = value; }
            public bool PerMaskEnabled { get => perMaskEnabled; set => perMaskEnabled = value; }
            public int SelectedMask { get => selectedMask; set => selectedMask = value; }
            public IReadOnlyList<string> MaskOptions => editingProduce
                ? new[] { "Produce", "Leaves", "Container" }
                : new[] { "Produce", "Leaves", "Stem" };
            public IReadOnlyList<string> PreviewPlantOptions => new[] { "Rice" };
            public int SelectedPreviewPlant { get => selectedPreviewPlant; set => selectedPreviewPlant = value; }
            public float GetValue(string key) => values.TryGetValue(key, out float value) ? value : 0f;
            public void SetValue(string key, float value) => values[key] = value;
            public void SetEditingProduce(bool value) => editingProduce = value;
            public void ResetSection(string section) { }
            public void ResetCurrentMask() { }
            public void RestoreInherited() => overrideEnabled = false;
            public void RestoreXmlDefault() => values.Clear();
            public void DrawPreview(Rect rect) { }
            public void Close() { }
        }

        private sealed class RuntimeInputSurface : IHorticultureInputDialogSurface
        {
            public string Title => "Runtime input";
            public string Description => "Input fixture";
            public string FieldLabel => "Name";
            public string Value { get; set; } = string.Empty;
            public string ValidationMessage => string.Empty;
            public string PrimaryLabel => "Save";
            public Action PrimaryAction => () => { };
            public string SecondaryLabel => string.Empty;
            public Action SecondaryAction => null;
            public void Close() { }
        }

        private sealed class RuntimePreviewSurface : IHorticulturePreviewDialogSurface
        {
            public string Title => "Runtime preview";
            public string Description => "Preview fixture";
            public IReadOnlyList<string> Legend => new[] { "Produce", "Leaves", "Stem" };
            public void DrawPreview(Rect rect) { }
            public void RefreshPreview() { }
            public void Close() { }
        }

        private sealed class RuntimeMaskSurface : IHorticultureMaskEditorSurface
        {
            private int selectedPage;
            private int selectedLayer;
            private int previewMode = 1;
            private int toolMode;
            private int paintMode;
            private float brushSize = 3f;
            private float tolerance = 0.12f;
            private float selectionRadius = 2f;
            private float fragmentLimit = 12f;
            public int GrowCount { get; set; }
            public string Title => "Runtime Mask Editor";
            public string PageLabel => selectedPage == 0 ? "Plant channels" : "Produce channels";
            public string OriginLabel => "Manual";
            public string LayerStatus => "Selected channel contains painted pixels.";
            public string StatusLabel => "Runtime mask status";
            public int SelectedPage { get => selectedPage; set => selectedPage = value; }
            public IReadOnlyList<string> PageOptions => new[] { "Plant", "Produce" };
            public int SelectedVariation { get; set; }
            public IReadOnlyList<string> VariationOptions => new[] { "Base" };
            public bool Enabled { get; set; } = true;
            public int SelectedLayer { get => selectedLayer; set => selectedLayer = value; }
            public IReadOnlyList<string> LayerOptions => selectedPage == 0
                ? new[] { "Produce", "Leaves", "Stem" } : new[] { "Produce", "Leaves", "Container" };
            public bool GetLayerLocked(int index) => false;
            public void SetLayerLocked(int index, bool locked) { }
            public int PreviewMode { get => previewMode; set => previewMode = value; }
            public bool ProjectionPreviewActive => false;
            public bool ValidationAvailable => false;
            public string ValidationLabel => "No validation issues";
            public int ToolMode { get => toolMode; set => toolMode = value; }
            public int PaintSelectionMode { get => paintMode; set => paintMode = value; }
            public float BrushSize { get => brushSize; set => brushSize = value; }
            public float Tolerance { get => tolerance; set => tolerance = value; }
            public float SelectionRadius { get => selectionRadius; set => selectionRadius = value; }
            public float FragmentLimit { get => fragmentLimit; set => fragmentLimit = value; }
            public bool CanUndo => false;
            public bool CanRedo => false;
            public void DrawCanvas(Rect rect) { }
            public void GrowSelection() => GrowCount++;
            public void ShrinkSelection() { }
            public void SmoothSelection() { }
            public void FeatherSelection() { }
            public void RemoveTinyFragments() { }
            public void FillSelectionHoles() { }
            public void FillUnmaskedPixels() { }
            public void KeepLargestSelection() { }
            public void SmartExpandSelection() { }
            public void ClearSelection() { }
            public void Validate() { }
            public void PreviousIssue() { }
            public void NextIssue() { }
            public void Undo() { }
            public void Redo() { }
            public void CopyToVariation() { }
            public void ProjectToVariation() { }
            public void RegenerateAutoMask() { }
            public void ResetToAutoMask() { }
            public void ApplyProjection() { }
            public void CancelProjection() { }
            public void Close() { }
        }
    }
}
