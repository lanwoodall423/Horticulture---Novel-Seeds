using System;
using System.Collections.Generic;
using System.Linq;
using InsightCanvas;
using KnowledgeFramework;
using RimWorld;
using UnityEngine;
using Verse;

namespace HorticultureNovelSeeds
{
    /// <summary>Bounded, immutable lineage diagnostics for UI and runtime validation.</summary>
    public sealed class HorticultureLineageInspection
    {
        public int NodeCount { get; private set; }
        public int EdgeCount { get; private set; }
        public bool Complete { get; private set; }
        public string Validation { get; private set; }
        public IReadOnlyList<string> NodeIds { get; private set; }

        internal HorticultureLineageInspection(int nodeCount, int edgeCount, bool complete,
            string validation, IEnumerable<string> nodeIds)
        {
            NodeCount = nodeCount;
            EdgeCount = edgeCount;
            Complete = complete;
            Validation = validation ?? string.Empty;
            NodeIds = (nodeIds ?? Enumerable.Empty<string>()).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        }
    }

    /// <summary>
    /// Presentation document for the Horticulture field guide and cultivar collection.
    /// GameComponent_NovelSeeds remains the authority for all persistent data. This document
    /// owns navigation, filters, selections, accessibility, snapshots, and transient comparison state.
    /// </summary>
    public sealed class HorticultureWorkspaceDocument
    {
        public const int MaximumComparisonCount = 8;
        public const int MaximumLineageNodes = 128;
        public const int MaximumLineageEdges = 256;
        public const int MaximumLineageDepth = 12;

        private readonly InsightUiDocument uiDocument;
        private readonly InsightUiHost uiHost;
        private readonly InsightUiNavigation navigation;
        private readonly InsightUiVirtualList plantList;
        private readonly InsightUiVirtualList cultivarList;
        private readonly InsightUiVirtualList breedingList;
        private readonly InsightUiVirtualList knowledgeList;
        private readonly InsightUiVirtualList lineageNodeList;
        private readonly InsightUiSearchField plantSearchField;
        private readonly InsightUiSearchField cultivarSearchField;
        private readonly InsightUiSearchField breedingSearchField;
        private readonly InsightUiSearchField knowledgeSearchField;
        private readonly InsightUiSegmented plantFilterField;
        private readonly InsightUiSegmented balanceFilterField;
        private readonly InsightUiToggle archivedField;
        private readonly InsightUiToggle produceEffectField;
        private InsightUiButton compareButton;
        private readonly InsightUiSplit plantSplit;
        private readonly InsightUiSplit cultivarSplit;
        private readonly InsightUiSplit breedingSplit;
        private readonly InsightUiElement cultivarCollectionSurface;
        private readonly InsightUiElement comparisonSurface;
        private readonly InsightUiElement lineageSurface;
        private readonly List<InsightUiBadge> traitChips = new List<InsightUiBadge>();

        private readonly List<PlantView> plantViews = new List<PlantView>();
        private readonly List<PlantView> filteredPlantViews = new List<PlantView>();
        private readonly List<CultivarView> allCultivarViews = new List<CultivarView>();
        private readonly List<CultivarView> cultivarViews = new List<CultivarView>();
        private readonly List<CultivarView> filteredCultivarViews = new List<CultivarView>();
        private readonly List<BreedingView> breedingViews = new List<BreedingView>();
        private readonly List<BreedingView> filteredBreedingViews = new List<BreedingView>();
        private readonly List<KnowledgeView> knowledgeViews = new List<KnowledgeView>();
        private readonly List<KnowledgeView> filteredKnowledgeViews = new List<KnowledgeView>();
        private readonly List<LineageNodeView> lineageNodeViews = new List<LineageNodeView>();
        private readonly HashSet<string> comparisonIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, AvailabilityView> availability = new Dictionary<string, AvailabilityView>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> comparisonConfidence = new Dictionary<string, string>(StringComparer.Ordinal);

        private string activePageId = "overview";
        private string selectedPlantDefName;
        private string selectedCultivarId;
        private Pawn selectedPawn;
        private KnowledgeMenuScope knowledgeScope = KnowledgeMenuScope.Colonist;
        private string plantSearch = string.Empty;
        private string cultivarSearch = string.Empty;
        private string breedingSearch = string.Empty;
        private string knowledgeSearch = string.Empty;
        private int plantFilter;
        private int balanceFilter;
        private bool showArchived;
        private bool requireProduceEffect;
        private bool compareMode;
        private bool snapshotDirty = true;
        private bool highContrast;
        private bool reducedMotion;
        private InsightUiDensity density = InsightUiDensity.Normal;
        private InsightUiOrientation splitOrientation = InsightUiOrientation.Horizontal;
        private int lastVarietyCount = -1;
        private int lastBreedingCount = -1;
        private int lastKnowledgeRevision = -1;
        private int lastKnowledgeRegistryRevision = -1;
        private int lineageNodeCount;
        private int lineageEdgeCount;
        private bool lineageComplete;
        private string lineageValidation = string.Empty;
        private InsightModelSnapshot lineageSnapshot;
        private InsightGraphLayoutResult lineageLayout;
        private readonly Dictionary<string, string> lineageLabels = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> lineageRawIds = new Dictionary<string, string>(StringComparer.Ordinal);
        private KnowledgeFrameworkDiagnosticView knowledgeDiagnostic;

        public HorticultureWorkspaceDocument()
        {
            navigation = InsightUi.Navigation("hns.workspace.navigation", 760f);
            navigation.Bind(() => activePageId, value =>
            {
                if (string.IsNullOrEmpty(value)) return;
                activePageId = value;
                compareMode = false;
                uiDocument?.Invalidate();
            });

            plantSearchField = InsightUi.SearchField("hns.workspace.plants.search", string.Empty, "Search plants")
                .Bind(() => plantSearch, value => SetSearch(ref plantSearch, value));
            cultivarSearchField = InsightUi.SearchField("hns.workspace.cultivars.search", string.Empty, "Search cultivars")
                .Bind(() => cultivarSearch, value => SetSearch(ref cultivarSearch, value));
            breedingSearchField = InsightUi.SearchField("hns.workspace.breeding.search", string.Empty, "Search programs")
                .Bind(() => breedingSearch, value => SetSearch(ref breedingSearch, value));
            knowledgeSearchField = InsightUi.SearchField("hns.workspace.knowledge.search", string.Empty, "Search knowledge")
                .Bind(() => knowledgeSearch, value => SetSearch(ref knowledgeSearch, value));

            plantFilterField = InsightUi.Segmented("hns.workspace.plants.filter", new[] { "All", "Discovered", "Undiscovered" }, 0)
                .Bind(() => plantFilter, value => SetFilter(ref plantFilter, value));
            balanceFilterField = InsightUi.Segmented("hns.workspace.cultivars.balance", new[] { "All", "Balanced", "Beneficial", "Detrimental" }, 0)
                .Bind(() => balanceFilter, value => SetFilter(ref balanceFilter, value));
            archivedField = InsightUi.Toggle("hns.workspace.cultivars.archived", "Show archived")
                .Bind(() => showArchived, value => SetBool(ref showArchived, value));
            produceEffectField = InsightUi.Toggle("hns.workspace.cultivars.produce", "Produce effects only")
                .Bind(() => requireProduceEffect, value => SetBool(ref requireProduceEffect, value));

            plantList = InsightUi.VirtualList("hns.workspace.plants.list", 0, 38f, PlantListItem);
            plantList.Overscan = 3;
            plantList.CacheLimit = 96;
            cultivarList = InsightUi.VirtualList("hns.workspace.cultivars.list", 0, 46f, CultivarListItem);
            cultivarList.Overscan = 3;
            cultivarList.CacheLimit = 96;
            breedingList = InsightUi.VirtualList("hns.workspace.breeding.list", 0, 42f, BreedingListItem);
            breedingList.Overscan = 3;
            breedingList.CacheLimit = 64;
            knowledgeList = InsightUi.VirtualList("hns.workspace.knowledge.list", 0, 44f, KnowledgeListItem);
            knowledgeList.Overscan = 3;
            knowledgeList.CacheLimit = 96;
            lineageNodeList = InsightUi.VirtualList("hns.workspace.lineage.nodes", 0, 34f, LineageNodeListItem);
            lineageNodeList.Overscan = 2;
            lineageNodeList.CacheLimit = 48;

            InsightUiElement root = BuildRoot(out plantSplit, out cultivarSplit, out breedingSplit,
                out cultivarCollectionSurface, out comparisonSurface, out lineageSurface);
            uiDocument = new InsightUiDocument("hns.horticulture.workspace.document", root)
            {
                Theme = CreateBotanicalTheme(),
                Density = density,
                HighContrast = highContrast,
                ReducedMotion = reducedMotion,
                DrawBackground = true,
                TrackDuplicateIds = true
            };
            uiHost = new InsightUiHost(uiDocument);
            comparisonSurface.Visible = false;
        }

        public string ActivePageId => activePageId;
        public IReadOnlyList<string> NavigationPageIds => navigation.Pages.Select(page => page.Id).ToArray();
        public int NavigationPageCount => navigation.Pages.Count;
        public bool TrackDuplicateIds => uiDocument.TrackDuplicateIds;
        public int DuplicateIdCount => uiDocument.Diagnostics.DuplicateIds;
        public int RenderErrorCount => uiDocument.Diagnostics.RenderErrors;
        public int PlantVirtualizationCacheLimit => plantList.CacheLimit;
        public int CultivarVirtualizationCacheLimit => cultivarList.CacheLimit;
        public int BreedingVirtualizationCacheLimit => breedingList.CacheLimit;
        public int KnowledgeVirtualizationCacheLimit => knowledgeList.CacheLimit;
        public int LineageNavigationCacheLimit => lineageNodeList.CacheLimit;
        public bool HighContrast => highContrast;
        public bool ReducedMotion => reducedMotion;
        public InsightUiDensity Density => uiDocument.Density;
        public bool IsNarrowWorkspace => splitOrientation == InsightUiOrientation.Vertical;
        public int ComparisonCount => comparisonIds.Count;
        public int LineageNodeCount => lineageNodeCount;
        public int LineageEdgeCount => lineageEdgeCount;
        public bool LineageComplete => lineageComplete;
        public string LineageValidation => lineageValidation;
        public bool KnowledgeAvailable => knowledgeDiagnostic != null && knowledgeDiagnostic.Available;

        public static HorticultureLineageInspection AnalyzeLineage(IEnumerable<VarietyRecord> records, string rootId)
        {
            LineageGraphBuild graph = CreateLineageGraph(records, rootId);
            return new HorticultureLineageInspection(graph.NodeCount, graph.EdgeCount, graph.Complete,
                graph.Validation, graph.GraphIds.Values);
        }

        public bool HasIsolatedPresentationState(HorticultureWorkspaceDocument other)
        {
            return other != null && !ReferenceEquals(uiDocument.State, other.uiDocument.State)
                && !ReferenceEquals(uiDocument.Focus, other.uiDocument.Focus)
                && !ReferenceEquals(uiDocument.Toasts, other.uiDocument.Toasts);
        }

        public bool HasUniqueComponentIds()
        {
            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            return CountDuplicateIds(uiDocument.Root, ids) == 0;
        }

        public void PrepareKnowledge(Pawn pawn)
        {
            activePageId = "knowledge";
            compareMode = false;
            selectedPawn = pawn;
            knowledgeScope = KnowledgeMenuScope.Colonist;
            snapshotDirty = true;
            uiDocument.Invalidate();
        }

        public void PreparePlant(ThingDef plant)
        {
            if (plant == null) return;
            activePageId = "plants";
            compareMode = false;
            selectedPlantDefName = plant.defName;
            snapshotDirty = true;
            uiDocument.Invalidate();
        }

        public void PrepareCultivar(VarietyRecord variety)
        {
            if (variety == null || variety.id.NullOrEmpty()) return;
            activePageId = "cultivars";
            compareMode = false;
            selectedCultivarId = variety.id;
            snapshotDirty = true;
            uiDocument.Invalidate();
        }

        public void PrepareLineage(VarietyRecord variety)
        {
            PrepareCultivar(variety);
        }

        public void OpenCompare()
        {
            if (!MainTabWindow_CultivarRegistry.CanCompareCount(comparisonIds.Count))
            {
                uiDocument.Toasts.Show("Select at least two cultivars to compare.", InsightToastSeverity.Warning);
                return;
            }
            activePageId = "cultivars";
            compareMode = true;
            uiDocument.Invalidate();
        }

        public void PreOpen()
        {
            snapshotDirty = true;
            RefreshSnapshots();
        }

        public void Draw(Rect rect)
        {
            RefreshSnapshots();
            bool presentationChanged = ApplyResponsiveLayout(rect.width);
            presentationChanged |= UpdatePresentationVisibility();
            if (presentationChanged) uiDocument.Invalidate();
            uiHost.Draw(rect, Time.deltaTime);
        }

        public void PostClose()
        {
            uiHost.PostClose();
        }

        public void SetAccessibility(bool useHighContrast, bool useReducedMotion, InsightUiDensity requestedDensity)
        {
            highContrast = useHighContrast;
            reducedMotion = useReducedMotion;
            density = requestedDensity;
            uiDocument.HighContrast = highContrast;
            uiDocument.ReducedMotion = reducedMotion;
            uiDocument.Density = density;
            uiDocument.Invalidate();
        }

        private InsightUiElement BuildRoot(out InsightUiSplit plantSplitOut, out InsightUiSplit cultivarSplitOut,
            out InsightUiSplit breedingSplitOut, out InsightUiElement collectionOut, out InsightUiElement compareOut,
            out InsightUiElement lineageOut)
        {
            navigation.Add("overview", "Overview", BuildOverviewPage());
            navigation.Add("plants", "Plants", BuildPlantsPage(out plantSplitOut));
            navigation.Add("cultivars", "Cultivars", BuildCultivarsPage(out cultivarSplitOut, out collectionOut, out compareOut, out lineageOut));
            navigation.Add("breeding", "Breeding", BuildBreedingPage(out breedingSplitOut));
            navigation.Add("knowledge", "Knowledge", BuildKnowledgePage());
            InsightUiStack root = InsightUi.Column("hns.workspace.root", navigation, InsightUi.Toast("hns.workspace.toast"));
            root.Style.Gap = 8f;
            root.Style.Padding = InsightUiPadding.All(4f);
            return root;
        }

        private InsightUiElement BuildOverviewPage()
        {
            InsightUiElement guidance = InsightUi.Callout("hns.overview.guidance", InsightUiCalloutSeverity.Info,
                "Horticulture field guide", "A bounded, searchable collection of plants, cultivars, breeding records, and Knowledge Framework evidence. Unknown values stay unknown until evidence is available.");
            InsightUiGrid stats = InsightUi.Grid("hns.overview.stats", 210f);
            stats.Add(
                DynamicLabel("hns.overview.plants", () => "Plants in guide: " + plantViews.Count),
                DynamicLabel("hns.overview.cultivars", () => "Cultivars in collection: " + allCultivarViews.Count),
                DynamicLabel("hns.overview.breeding", () => "Breeding programs: " + breedingViews.Count),
                DynamicLabel("hns.overview.knowledge", () => "Knowledge entries: " + knowledgeViews.Count));
            InsightUiElement actions = Panel("hns.overview.actions",
                InsightUi.SectionHeader("hns.overview.actions.header", "Start with a collection", "Open a bounded registry or follow a Knowledge Framework entry point."),
                InsightUi.Row("hns.overview.actions.row",
                    ActionButton("hns.overview.open-plants", "Browse plants", () => SelectPage("plants")),
                    ActionButton("hns.overview.open-cultivars", "Browse cultivars", () => SelectPage("cultivars")),
                    ActionButton("hns.overview.open-knowledge", "Open knowledge", () => SelectPage("knowledge"))));
            InsightUiElement integrity = Panel("hns.overview.integrity",
                InsightUi.SectionHeader("hns.overview.integrity.header", "Presentation integrity", "Snapshots are rebuilt before a frame; graph traversal and virtual-list caches are bounded."),
                DynamicLabel("hns.overview.integrity.lineage", () => "Lineage graph: " + lineageNodeCount + " nodes, " + lineageEdgeCount + " edges"),
                DynamicLabel("hns.overview.integrity.diagnostics", () => "Render diagnostics: " + uiDocument.Diagnostics.RenderErrors + " errors, " + uiDocument.Diagnostics.DuplicateIds + " duplicate IDs"));
            return Page("hns.overview.page", guidance, stats, actions, integrity);
        }

        private InsightUiElement BuildPlantsPage(out InsightUiSplit splitOut)
        {
            InsightUiElement listPane = Panel("hns.plants.list-pane",
                InsightUi.SectionHeader("hns.plants.list.header", "Plants", "Growable plant definitions with discovery masking and bounded search."),
                plantSearchField, plantFilterField, plantList);
            InsightUiElement inspector = Panel("hns.plants.inspector",
                InsightUi.SectionHeader("hns.plants.inspector.header", "Plant field guide", "Plant facts are progressively revealed by the selected Knowledge scope."),
                DynamicLabel("hns.plants.inspector.name", () => SelectedPlant()?.Label ?? "Select a plant", InsightUiTextStyle.Heading),
                DynamicLabel("hns.plants.inspector.status", PlantDetailStatus),
                DynamicLabel("hns.plants.inspector.rank", () => SelectedPlant() == null ? string.Empty : "Knowledge: " + SelectedPlant().Rank),
                DynamicLabel("hns.plants.inspector.growth", PlantGrowthDetail),
                DynamicLabel("hns.plants.inspector.cultivars", () => SelectedPlant() == null ? string.Empty : "Known cultivars: " + SelectedPlant().CultivarCount),
                ActionButton("hns.plants.inspector.cultivars-action", "View cultivars", () =>
                {
                    PlantView plant = SelectedPlant();
                    if (plant == null) return;
                    cultivarSearch = plant.Label;
                    SelectPage("cultivars");
                }));
            splitOut = InsightUi.Split("hns.plants.split", listPane, InsightUi.Scroll("hns.plants.inspector.scroll", inspector), 0.38f);
            splitOut.Draggable = true;
            splitOut.Style.Flex = 1f;
            return Page("hns.plants.page", splitOut);
        }

        private InsightUiElement BuildCultivarsPage(out InsightUiSplit splitOut, out InsightUiElement collectionOut,
            out InsightUiElement compareOut, out InsightUiElement lineageOut)
        {
            compareButton = ActionButton("hns.cultivars.compare", "Compare selected", OpenCompare);
            collectionOut = Panel("hns.cultivars.collection",
                InsightUi.SectionHeader("hns.cultivars.collection.header", "Cultivar collection", "Favorites, archive state, balance, and produce-effect filters preserve the registry’s existing behavior."),
                cultivarSearchField, balanceFilterField, archivedField, produceEffectField,
                InsightUi.Row("hns.cultivars.collection.actions", compareButton,
                    ActionButton("hns.cultivars.clear-compare", "Clear comparison", ClearComparison)), cultivarList);
            InsightUiElement inspector = Panel("hns.cultivars.inspector",
                InsightUi.SectionHeader("hns.cultivars.inspector.header", "Cultivar inspector", "Trait chips, modifiers, products, and lineage remain gated by Knowledge rank."),
                DynamicLabel("hns.cultivars.inspector.name", () => SelectedCultivar()?.Label ?? "Select a cultivar", InsightUiTextStyle.Heading),
                DynamicLabel("hns.cultivars.inspector.status", CultivarStatus),
                BuildTraitChips(),
                DynamicLabel("hns.cultivars.inspector.traits", CultivarTraits),
                DynamicLabel("hns.cultivars.inspector.modifiers", CultivarModifiers),
                InsightUi.Row("hns.cultivars.inspector.actions",
                    ActionButton("hns.cultivars.rename", "Rename", RenameSelected),
                    ActionButton("hns.cultivars.favorite", "Favorite", ToggleFavorite),
                    ActionButton("hns.cultivars.archive", "Archive / restore", ToggleArchived),
                ActionButton("hns.cultivars.locate", "Locate", LocateSelected)),
                DynamicLabel("hns.cultivars.inspector.lineage-heading", () => "Lineage graph (bounded to " + MaximumLineageNodes + " nodes)", InsightUiTextStyle.Label),
                lineageOut = InsightUi.Custom("hns.cultivars.lineage.graph", DrawLineageGraph, MeasureLineageGraph),
                DynamicLabel("hns.cultivars.inspector.lineage-navigation", () => lineageNodeViews.Count == 0
                    ? "No navigable lineage records." : "Select a node to inspect it.", InsightUiTextStyle.Caption),
                lineageNodeList);
            splitOut = InsightUi.Split("hns.cultivars.split", collectionOut, InsightUi.Scroll("hns.cultivars.inspector.scroll", inspector), 0.4f);
            splitOut.Draggable = true;
            splitOut.Style.Flex = 1f;
            compareOut = BuildComparisonSurface();
            InsightUiElement body = InsightUi.Column("hns.cultivars.body", splitOut, compareOut);
            body.Style.Flex = 1f;
            return Page("hns.cultivars.page", body);
        }

        private InsightUiElement BuildComparisonSurface()
        {
            InsightUiElement table = InsightUi.Custom("hns.compare.table", DrawComparison, MeasureComparison);
            return Panel("hns.compare.panel",
                InsightUi.Row("hns.compare.header",
                    DynamicLabel("hns.compare.title", () => "Compare (" + comparisonIds.Count + "/" + MaximumComparisonCount + ")", InsightUiTextStyle.Heading),
                    ActionButton("hns.compare.back", "Back to collection", CloseCompare)),
                DynamicLabel("hns.compare.guidance", () => comparisonIds.Count < 2 ? "Select at least two cultivars." : "Knowledge evidence and rank-gated fields are compared side by side."),
                table);
        }

        private InsightUiElement BuildBreedingPage(out InsightUiSplit splitOut)
        {
            InsightUiElement listPane = Panel("hns.breeding.list-pane",
                InsightUi.SectionHeader("hns.breeding.list.header", "Breeding programs", "Legacy BreedingProgramRecord entries are shown faithfully and remain read-only here."),
                breedingSearchField, breedingList);
            InsightUiElement inspector = Panel("hns.breeding.inspector",
                InsightUi.SectionHeader("hns.breeding.inspector.header", "Program inspector", "Program matching remains owned by BreedingProgramRecord; this page does not create or mutate programs."),
                DynamicLabel("hns.breeding.inspector.name", () => SelectedBreeding()?.Name ?? "Select a breeding program", InsightUiTextStyle.Heading),
                DynamicLabel("hns.breeding.inspector.crop", () => SelectedBreeding()?.CropLabel ?? string.Empty),
                DynamicLabel("hns.breeding.inspector.traits", () => SelectedBreeding()?.DesiredTraits ?? string.Empty),
                DynamicLabel("hns.breeding.inspector.matches", () => SelectedBreeding() == null ? string.Empty : "Matching cultivars: " + SelectedBreeding().MatchingCount),
                DynamicLabel("hns.breeding.inspector.notifications", () => SelectedBreeding() == null ? string.Empty : "Notified cultivars: " + SelectedBreeding().NotifiedCount),
                DynamicLabel("hns.breeding.inspector.active", () => SelectedBreeding() == null ? string.Empty : (SelectedBreeding().Active ? "Active" : "Completed / inactive")));
            splitOut = InsightUi.Split("hns.breeding.split", listPane, InsightUi.Scroll("hns.breeding.inspector.scroll", inspector), 0.4f);
            splitOut.Draggable = true;
            splitOut.Style.Flex = 1f;
            return Page("hns.breeding.page", splitOut);
        }

        private InsightUiElement BuildKnowledgePage()
        {
            InsightUiSegmented scope = InsightUi.Segmented("hns.knowledge.scope", new[] { "Colonist", "Colony" }, 0)
                .Bind(() => knowledgeScope == KnowledgeMenuScope.Colony ? 1 : 0, value =>
                {
                    knowledgeScope = value == 1 ? KnowledgeMenuScope.Colony : KnowledgeMenuScope.Colonist;
                    snapshotDirty = true;
                    uiDocument.Invalidate();
                });
            InsightUiElement navigationBlock = Panel("hns.knowledge.navigation",
                InsightUi.SectionHeader("hns.knowledge.navigation.header", "Knowledge scope", "Personal expertise is shown only for a selected colonist; colony scope shows shared evidence without inventing expertise."),
                scope,
                DynamicLabel("hns.knowledge.pawn", () => knowledgeScope == KnowledgeMenuScope.Colony ? "Colony knowledge" :
                    "Colonist: " + (selectedPawn?.LabelShortCap ?? "None")),
                knowledgeSearchField);
            InsightUiElement content = Panel("hns.knowledge.content",
                DynamicLabel("hns.knowledge.title", () => knowledgeDiagnostic != null && !knowledgeDiagnostic.Available
                    ? "Knowledge Framework unavailable" : "Plant knowledge", InsightUiTextStyle.Heading),
                DynamicLabel("hns.knowledge.guidance", KnowledgeGuidance), knowledgeList);
            InsightUiSplit split = InsightUi.Split("hns.knowledge.split", navigationBlock, content, 0.34f);
            split.Draggable = true;
            split.Style.Flex = 1f;
            return Page("hns.knowledge.page", split);
        }

        private InsightUiElement Page(string id, params InsightUiElement[] children)
        {
            InsightUiStack page = InsightUi.Column(id, children);
            page.Style.Gap = 8f;
            page.Style.Flex = 1f;
            return page;
        }

        private static InsightUiElement Panel(string id, params InsightUiElement[] children)
        {
            InsightUiSurface panel = InsightUi.Surface(id, InsightUi.Column(id + ".body", children));
            panel.SetPadding(10f);
            panel.SetCornerRadius(4f);
            panel.Style.Gap = 6f;
            return panel;
        }

        private InsightUiButton ActionButton(string id, string label, Action action)
        {
            InsightUiButton button = InsightUi.Button(id, label, action);
            button.Style.HorizontalAlignment = InsightAlignment.Start;
            button.SetTooltip(label);
            return button;
        }

        private InsightUiLabel DynamicLabel(string id, Func<string> provider,
            InsightUiTextStyle style = InsightUiTextStyle.Body)
        {
            return InsightUi.Label(id, string.Empty, style).SetTextProvider(provider);
        }

        private InsightUiElement BuildTraitChips()
        {
            InsightUiBadge[] chips = Enumerable.Range(0, 8)
                .Select(index =>
                {
                    InsightUiBadge chip = InsightUi.Badge("hns.cultivars.inspector.trait-chip." + index, "Unknown");
                    chip.Visible = false;
                    traitChips.Add(chip);
                    return chip;
                }).ToArray();
            return InsightUi.Wrap("hns.cultivars.inspector.trait-chips", chips);
        }

        private InsightUiElement PlantListItem(int index)
        {
            if (index < 0 || index >= filteredPlantViews.Count) return InsightUi.Empty("hns.plants.empty." + index);
            PlantView view = filteredPlantViews[index];
            InsightUiButton button = InsightUi.Button("select", view.Label, () => SelectPlant(view.Definition));
            button.SelectedProvider = () => string.Equals(selectedPlantDefName, view.Definition?.defName, StringComparison.Ordinal);
            return InsightUi.Scope("plant." + SafeId(view.Definition?.defName), button);
        }

        private InsightUiElement CultivarListItem(int index)
        {
            if (index < 0 || index >= filteredCultivarViews.Count) return InsightUi.Empty("hns.cultivars.empty." + index);
            CultivarView view = filteredCultivarViews[index];
            InsightUiButton select = InsightUi.Button("select", (view.Favorite ? "★ " : string.Empty) + view.Label,
                () => SelectCultivar(view.Id));
            select.SelectedProvider = () => string.Equals(selectedCultivarId, view.Id, StringComparison.Ordinal);
            InsightUiToggle compare = InsightUi.Toggle("compare", "Compare")
                .Bind(() => comparisonIds.Contains(view.Id), value => ToggleComparison(view.Id, value));
            return InsightUi.Scope("cultivar." + SafeId(view.Id), InsightUi.Row("row", select, compare));
        }

        private InsightUiElement BreedingListItem(int index)
        {
            if (index < 0 || index >= filteredBreedingViews.Count) return InsightUi.Empty("hns.breeding.empty." + index);
            BreedingView view = filteredBreedingViews[index];
            InsightUiButton button = InsightUi.Button("select", view.Name + " — " + view.CropLabel,
                () => SelectBreeding(view.Id));
            button.SelectedProvider = () => string.Equals(SelectedBreeding()?.Id, view.Id, StringComparison.Ordinal);
            return InsightUi.Scope("breeding." + SafeId(view.Id), button);
        }

        private InsightUiElement KnowledgeListItem(int index)
        {
            if (index < 0 || index >= filteredKnowledgeViews.Count) return InsightUi.Empty("hns.knowledge.empty." + index);
            KnowledgeView view = filteredKnowledgeViews[index];
            InsightUiButton button = InsightUi.Button("select", view.Label + " — " + view.Status,
                () =>
                {
                    if (view.Plant != null) MainTabWindow_CultivarRegistry.OpenPlant(view.Plant);
                });
            return InsightUi.Scope("knowledge." + SafeId(view.SubjectId ?? view.Label), button);
        }

        private InsightUiElement LineageNodeListItem(int index)
        {
            if (index < 0 || index >= lineageNodeViews.Count) return InsightUi.Empty("hns.lineage.empty." + index);
            LineageNodeView view = lineageNodeViews[index];
            InsightUiButton button = InsightUi.Button("select", view.Label, () => SelectLineageNode(view.RawId));
            button.SelectedProvider = () => string.Equals(selectedCultivarId, view.RawId, StringComparison.Ordinal);
            return InsightUi.Scope("lineage." + SafeId(view.GraphId), button);
        }

        private void RefreshSnapshots()
        {
            GameComponent_NovelSeeds component = GameComponent_NovelSeeds.Instance;
            if (component == null)
            {
                ClearSnapshots();
                return;
            }

            int varietyCount = component.AllVarieties.Count();
            int breedingCount = component.BreedingPrograms?.Count ?? 0;
            int knowledgeRevision = HorticultureKnowledgeAdapter.KnowledgeRevision;
            int knowledgeRegistryRevision = HorticultureKnowledgeAdapter.RegistryRevision;
            if (!snapshotDirty && varietyCount == lastVarietyCount && breedingCount == lastBreedingCount
                && knowledgeRevision == lastKnowledgeRevision && knowledgeRegistryRevision == lastKnowledgeRegistryRevision) return;

            EnsureSelections(component);
            List<VarietyRecord> varieties = component.AllVarieties
                .Where(value => value?.cropDef != null && HorticulturePlantPolicy.IsSupported(value.cropDef))
                .GroupBy(value => value.id ?? string.Empty).Select(group => group.First())
                .ToList();
            RefreshAvailability(component, varieties);

            plantViews.Clear();
            foreach (ThingDef plant in PlantDefinitions())
            {
                bool discovered = IsDiscovered(plant, component);
                int count = component.VarietiesFor(plant).Count();
                KnowledgeRank rank = VisiblePlantRank(plant);
                plantViews.Add(new PlantView(plant, discovered ? plant.LabelCap.ToString() : "Undiscovered plant",
                    discovered, rank, count, plant.plant?.growDays ?? 0f, plant.plant?.harvestYield ?? 0f));
            }
            filteredPlantViews.Clear();
            filteredPlantViews.AddRange(plantViews.Where(MatchesPlantFilter));

            cultivarViews.Clear();
            allCultivarViews.Clear();
            foreach (VarietyRecord variety in varieties) allCultivarViews.Add(CreateCultivarView(variety));
            cultivarViews.AddRange(allCultivarViews.Where(view => MatchesCultivarFilter(view.Authority)));
            cultivarViews.Sort((left, right) =>
            {
                int favorite = right.Favorite.CompareTo(left.Favorite);
                if (favorite != 0) return favorite;
                int crop = string.Compare(left.CropLabel, right.CropLabel, StringComparison.OrdinalIgnoreCase);
                return crop != 0 ? crop : string.Compare(left.Label, right.Label, StringComparison.OrdinalIgnoreCase);
            });
            filteredCultivarViews.Clear();
            filteredCultivarViews.AddRange(cultivarViews);

            breedingViews.Clear();
            foreach (BreedingProgramRecord program in component.BreedingPrograms ?? new BreedingProgramRecord[0])
                if (program != null) breedingViews.Add(CreateBreedingView(program, varieties));
            breedingViews.Sort((left, right) => string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase));
            filteredBreedingViews.Clear();
            filteredBreedingViews.AddRange(breedingViews.Where(view => Matches(view.Name, view.CropLabel, view.DesiredTraits, breedingSearch)));

            BuildKnowledgeViews();
            BuildLineageSnapshot(component, varieties);
            BuildComparisonSnapshot();
            EnsureSnapshotSelections();
            SetListState(plantList, filteredPlantViews.Count);
            SetListState(cultivarList, filteredCultivarViews.Count);
            SetListState(breedingList, filteredBreedingViews.Count);
            SetListState(knowledgeList, filteredKnowledgeViews.Count);
            UpdateTraitChips();
            lastVarietyCount = varietyCount;
            lastBreedingCount = breedingCount;
            lastKnowledgeRevision = HorticultureKnowledgeAdapter.KnowledgeRevision;
            lastKnowledgeRegistryRevision = HorticultureKnowledgeAdapter.RegistryRevision;
            snapshotDirty = false;
        }

        private void ClearSnapshots()
        {
            plantViews.Clear();
            filteredPlantViews.Clear();
            cultivarViews.Clear();
            allCultivarViews.Clear();
            filteredCultivarViews.Clear();
            breedingViews.Clear();
            filteredBreedingViews.Clear();
            knowledgeViews.Clear();
            filteredKnowledgeViews.Clear();
            lineageNodeViews.Clear();
            availability.Clear();
            lineageLabels.Clear();
            lineageRawIds.Clear();
            lineageSnapshot = null;
            lineageLayout = null;
            lineageNodeCount = 0;
            lineageEdgeCount = 0;
            lastVarietyCount = -1;
            lastBreedingCount = -1;
            lastKnowledgeRevision = -1;
            lastKnowledgeRegistryRevision = -1;
            SetListState(plantList, 0);
            SetListState(cultivarList, 0);
            SetListState(breedingList, 0);
            SetListState(knowledgeList, 0);
            SetListState(lineageNodeList, 0);
        }

        private void EnsureSelections(GameComponent_NovelSeeds component)
        {
            List<Pawn> colonists = (Find.Maps ?? new List<Map>()).SelectMany(map => map?.mapPawns?.FreeColonists ?? new List<Pawn>())
                .Where(pawn => pawn?.Faction?.def?.isPlayer == true && !pawn.Dead).Distinct()
                .OrderBy(pawn => pawn.LabelShort).ToList();
            if (selectedPawn == null || !colonists.Contains(selectedPawn)) selectedPawn = colonists.FirstOrDefault();
            if (selectedPlantDefName.NullOrEmpty() || DefDatabase<ThingDef>.GetNamedSilentFail(selectedPlantDefName) == null)
                selectedPlantDefName = PlantDefinitions().FirstOrDefault(def => IsDiscovered(def, component))?.defName ??
                    PlantDefinitions().FirstOrDefault()?.defName;
            VarietyRecord cultivar = component.GetVariety(selectedCultivarId);
            if (cultivar == null || !HorticulturePlantPolicy.IsSupported(cultivar.cropDef))
                selectedCultivarId = component.AllVarieties.OrderBy(value => value.cropDef?.label).ThenBy(value => value.Label)
                    .FirstOrDefault()?.id;
            foreach (string id in comparisonIds.Where(value => component.GetVariety(value) == null).ToList()) comparisonIds.Remove(id);
            foreach (string id in comparisonIds.OrderBy(value => value, StringComparer.Ordinal).Skip(MaximumComparisonCount).ToList())
                comparisonIds.Remove(id);
        }

        private void EnsureSnapshotSelections()
        {
            if (selectedCultivarId.NullOrEmpty() || !allCultivarViews.Any(value => value.Id == selectedCultivarId))
                selectedCultivarId = allCultivarViews.FirstOrDefault()?.Id;
            if (selectedPlantDefName.NullOrEmpty() || !plantViews.Any(value => value.Definition?.defName == selectedPlantDefName))
                selectedPlantDefName = plantViews.FirstOrDefault()?.Definition?.defName;
        }

        private List<ThingDef> PlantDefinitions()
        {
            return DefDatabase<ThingDef>.AllDefsListForReading.Where(NovelSeedUtility.IsGrowableCrop)
                .OrderBy(def => def.label).ThenBy(def => def.defName).ToList();
        }

        private bool IsDiscovered(ThingDef plant, GameComponent_NovelSeeds component)
        {
            return plant != null && (component.VarietiesFor(plant).Any() ||
                HorticultureKnowledgeAdapter.ColonyKnowledge(plant) > 0f ||
                HorticultureKnowledgeAdapter.StageOrder(HorticultureKnowledgeAdapter.StageFor(plant, null, true)) > 0);
        }

        private bool MatchesPlantFilter(PlantView view)
        {
            if (plantFilter == 1 && !view.Discovered || plantFilter == 2 && view.Discovered) return false;
            return Matches(view.Label, view.Definition?.defName, plantSearch);
        }

        private bool MatchesCultivarFilter(VarietyRecord variety)
        {
            if (variety == null || variety.cropDef == null || !HorticulturePlantPolicy.IsSupported(variety.cropDef)) return false;
            if (!showArchived && variety.registryArchived) return false;
            if (!Matches(variety.Label, variety.cropDef.label, NovelSeedUtility.TraitSummary(variety.traits), cultivarSearch)) return false;
            if (requireProduceEffect && !(variety.traits ?? new List<VarietyTraitDef>()).Any(trait =>
                    trait != null && ProduceTraitEffectUtility.Summary(TraitConfigUtility.Root(trait), HorticultureNovelSeedsMod.Settings) != "No Effect")) return false;
            float balance = NovelSeedUtility.TraitBalanceScore(variety.traits);
            switch (balanceFilter)
            {
                case 1: return Mathf.Abs(balance) <= (HorticultureNovelSeedsMod.Settings?.allowedTraitImbalance ?? 1);
                case 2: return balance > 0f;
                case 3: return balance < 0f;
                default: return true;
            }
        }

        private static bool Matches(params string[] values)
        {
            string query = values == null || values.Length == 0 ? string.Empty : values[values.Length - 1] ?? string.Empty;
            if (query.NullOrEmpty()) return true;
            return values.Take(values.Length - 1).Any(value => (value ?? string.Empty).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private CultivarView CreateCultivarView(VarietyRecord variety)
        {
            KnowledgeRank rank = VisibleCultivarRank(variety);
            RegistryAvailabilityRead availabilityRead = AvailabilityFor(variety);
            return new CultivarView(variety.id, variety.Label, variety.cropDef?.LabelCap.ToString() ?? "Unknown plant", variety.registryFavorite,
                variety.registryArchived, rank, variety.originKind.NullOrEmpty() ? "mutation" : variety.originKind,
                variety.generation > 0 ? variety.generation : LineageDepth(variety),
                TraitText(variety, rank), ModifierText(variety, rank), ProductText(variety, rank),
                availabilityRead.Plants, availabilityRead.Produce, availabilityRead.SeedPacks,
                variety.parentVarietyIds?.Count ?? 0, variety);
        }

        private BreedingView CreateBreedingView(BreedingProgramRecord program, List<VarietyRecord> varieties)
        {
            int matches = varieties.Count(value => program.Matches(value));
            return new BreedingView(program.id ?? string.Empty, program.name.NullOrEmpty() ? "Unnamed program" : program.name,
                program.cropDef?.LabelCap.ToString() ?? "Any crop", program.DesiredTraitSummary ?? string.Empty,
                program.active, program.notifiedVarietyIds?.Count ?? 0, matches);
        }

        private void BuildKnowledgeViews()
        {
            knowledgeViews.Clear();
            knowledgeDiagnostic = new KnowledgeFrameworkDiagnosticView(HorticultureKnowledgeRegistration.Diagnostics);
            KnowledgeMenuModel model = HorticultureKnowledgeAdapter.Menu(selectedPawn, knowledgeScope == KnowledgeMenuScope.Colony);
            if (model?.sections != null)
                foreach (KnowledgeMenuSection section in model.sections)
                foreach (KnowledgeMenuRow row in section?.rows ?? new List<KnowledgeMenuRow>())
                {
                    ThingDef plant = row.iconDef;
                    knowledgeViews.Add(new KnowledgeView(row.label ?? "Unknown plant", row.subjectId, row.status ?? "Unknown",
                        row.stageId ?? HorticultureKnowledgeAdapter.StageUnknown, row.rank, row.progress, row.confidence, plant));
                }
            filteredKnowledgeViews.Clear();
            filteredKnowledgeViews.AddRange(knowledgeViews.Where(view => Matches(view.Label, view.Status, view.Stage, knowledgeSearch)));
        }

        private void BuildComparisonSnapshot()
        {
            comparisonConfidence.Clear();
            List<VarietyRecord> selected = comparisonIds.Select(id => GameComponent_NovelSeeds.Instance?.GetVariety(id))
                .Where(value => value != null).OrderBy(value => value.cropDef?.label).ThenBy(value => value.Label).Take(MaximumComparisonCount).ToList();
            if (selected.Count < 2) return;
            KnowledgeStructuredComparisonSnapshot snapshot = HorticultureKnowledgeAdapter.CompareCultivars(selected,
                knowledgeScope == KnowledgeMenuScope.Colony ? null : selectedPawn, knowledgeScope == KnowledgeMenuScope.Colony);
            if (snapshot?.rows == null) return;
            for (int index = 0; index < selected.Count; index++)
            {
                List<float> confidence = snapshot.rows.Where(row => row?.confidences != null && index < row.confidences.Count &&
                    row.knownValues != null && index < row.knownValues.Count && row.knownValues[index])
                    .Select(row => row.confidences[index]).ToList();
                comparisonConfidence[selected[index].id] = confidence.Count == 0 ? "No evidence" : confidence.Average().ToStringPercent();
            }
        }

        private void BuildLineageSnapshot(GameComponent_NovelSeeds component, List<VarietyRecord> varieties)
        {
            lineageLabels.Clear();
            lineageRawIds.Clear();
            lineageNodeViews.Clear();
            lineageSnapshot = null;
            lineageLayout = null;
            lineageNodeCount = 0;
            lineageEdgeCount = 0;
            lineageComplete = true;
            lineageValidation = string.Empty;
            VarietyRecord root = component.GetVariety(selectedCultivarId);
            if (root == null) return;
            LineageGraphBuild graph = CreateLineageGraph(varieties, root.id);
            lineageLabels.Clear();
            foreach (KeyValuePair<string, string> label in graph.Labels) lineageLabels[label.Key] = label.Value;
            lineageRawIds.Clear();
            foreach (KeyValuePair<string, string> rawId in graph.RawIds) lineageRawIds[rawId.Key] = rawId.Value;
            lineageNodeCount = graph.NodeCount;
            lineageEdgeCount = graph.EdgeCount;
            lineageComplete = graph.Complete;
            lineageValidation = graph.Validation;
            lineageNodeViews.AddRange(graph.GraphIds.Values
                .Select(graphId => new LineageNodeView(graphId,
                    lineageLabels.TryGetValue(graphId, out string label) ? label : graphId,
                    lineageRawIds.TryGetValue(graphId, out string rawId) ? rawId : graphId))
                .OrderBy(value => value.Label, StringComparer.Ordinal));
            SetListState(lineageNodeList, lineageNodeViews.Count);
            lineageSnapshot = graph.Model.Snapshot();
            try
            {
                lineageLayout = InsightGraphLayout.Compute(lineageSnapshot, 640f, 280f,
                    MaximumLineageNodes, MaximumLineageEdges, 14);
            }
            catch (Exception exception)
            {
                lineageComplete = false;
                lineageValidation = exception.Message;
                lineageLayout = null;
            }
        }

        private static LineageGraphBuild CreateLineageGraph(IEnumerable<VarietyRecord> records, string rootId)
        {
            List<VarietyRecord> safeRecords = (records ?? Enumerable.Empty<VarietyRecord>())
                .Where(value => value != null && !value.id.NullOrEmpty())
                .GroupBy(value => value.id, StringComparer.Ordinal).Select(group => group.First()).ToList();
            Dictionary<string, VarietyRecord> byId = safeRecords.ToDictionary(value => value.id, value => value, StringComparer.Ordinal);
            LineageGraphBuild result = new LineageGraphBuild(rootId);
            if (rootId.NullOrEmpty() || !byId.TryGetValue(rootId, out VarietyRecord root)) return result;
            HashSet<string> expanded = new HashSet<string>(StringComparer.Ordinal);
            Queue<LineageVisit> pending = new Queue<LineageVisit>();
            pending.Enqueue(new LineageVisit(root.id, 0, root));
            while (pending.Count > 0)
            {
                LineageVisit visit = pending.Dequeue();
                string rawId = visit.RawId ?? "missing";
                if (!result.GraphIds.ContainsKey(rawId))
                {
                    if (result.GraphIds.Count >= MaximumLineageNodes)
                    {
                        result.Complete = false;
                        break;
                    }
                    string graphId = InsightIds.Stable("hns.lineage.node", rawId);
                    result.GraphIds[rawId] = graphId;
                    string label = visit.Record?.Label ?? "Unknown parent (" + rawId + ")";
                    string subtitle = visit.Record?.cropDef?.LabelCap.ToString() ?? "Missing record";
                    result.Model.Entity(new InsightEntity(graphId, label, subtitle,
                        visit.Record == null ? "missing-parent" : "cultivar", sourceId: rawId));
                    result.Labels[graphId] = label;
                    result.RawIds[graphId] = rawId;
                }
                if (visit.Record == null || visit.Depth >= MaximumLineageDepth)
                {
                    if (visit.Record != null && visit.Depth >= MaximumLineageDepth) result.Complete = false;
                    continue;
                }
                if (!expanded.Add(rawId)) continue;
                string childGraphId = result.GraphIds[rawId];
                foreach (string parentId in (visit.Record.parentVarietyIds ?? new List<string>()).Where(value => !value.NullOrEmpty()).Distinct())
                {
                    if (result.EdgeCount >= MaximumLineageEdges)
                    {
                        result.Complete = false;
                        break;
                    }
                    VarietyRecord parent = byId.TryGetValue(parentId, out VarietyRecord known) ? known : null;
                    string parentKey = parent == null ? "missing:" + parentId : parent.id;
                    if (!result.GraphIds.ContainsKey(parentKey))
                    {
                        if (result.GraphIds.Count >= MaximumLineageNodes)
                        {
                            result.Complete = false;
                            break;
                        }
                        string parentGraphId = InsightIds.Stable("hns.lineage.node", parentKey);
                        result.GraphIds[parentKey] = parentGraphId;
                        result.Model.Entity(new InsightEntity(parentGraphId,
                            parent?.Label ?? "Unknown parent (" + parentId + ")",
                            parent?.cropDef?.LabelCap.ToString() ?? "Missing record",
                            parent == null ? "missing-parent" : "cultivar", sourceId: parentKey));
                        result.Labels[parentGraphId] = parent?.Label ?? "Unknown parent (" + parentId + ")";
                        result.RawIds[parentGraphId] = parentKey;
                    }
                    result.Model.Relation(result.GraphIds[parentKey], childGraphId, "parent-of", 1f, true,
                        parent == null ? 0f : 1f, parent != null);
                    result.EdgeCount++;
                    if (parent != null && !expanded.Contains(parentKey))
                        pending.Enqueue(new LineageVisit(parentKey, visit.Depth + 1, parent));
                }
            }
            InsightModelValidation validation = result.Model.Validate();
            result.Validation = validation == null || validation.IsValid
                ? string.Empty : string.Join("; ", validation.Errors.ToArray());
            return result;
        }

        private void RefreshAvailability(GameComponent_NovelSeeds component, List<VarietyRecord> varieties)
        {
            availability.Clear();
            foreach (VarietyRecord variety in varieties)
                if (!variety.id.NullOrEmpty()) availability[variety.id] = new AvailabilityView();
            foreach (Map map in Find.Maps ?? new List<Map>())
            foreach (Thing thing in map?.listerThings?.AllThings ?? new List<Thing>())
            {
                CompPlantVariety plant = thing.TryGetComp<CompPlantVariety>();
                if (plant?.VarietyId != null && availability.TryGetValue(plant.VarietyId, out AvailabilityView plantStock))
                {
                    plantStock.Plants++;
                    if (plantStock.Target == null) plantStock.Target = thing;
                }
                CompNovelProduceAppearance produce = thing.TryGetComp<CompNovelProduceAppearance>();
                foreach (string id in produce?.SourceVarietyIds?.Where(value => !value.NullOrEmpty()).Distinct() ?? new string[0])
                    if (availability.TryGetValue(id, out AvailabilityView produceStock))
                    {
                        produceStock.Produce += thing.stackCount;
                        if (produceStock.Target == null) produceStock.Target = thing;
                    }
                CompNovelSeedPack pack = thing.TryGetComp<CompNovelSeedPack>();
                if (pack?.Valid == true)
                {
                    VarietyRecord match = component.FindMatchingVariety(pack.CropDef, pack.Traits);
                    if (match != null && availability.TryGetValue(match.id, out AvailabilityView seedStock))
                    {
                        seedStock.SeedPacks += thing.stackCount;
                        if (seedStock.Target == null) seedStock.Target = thing;
                    }
                }
            }
        }

        private CultivarView SelectedCultivar()
        {
            return allCultivarViews.FirstOrDefault(value => value.Id == selectedCultivarId);
        }

        private PlantView SelectedPlant()
        {
            return plantViews.FirstOrDefault(value => value.Definition?.defName == selectedPlantDefName);
        }

        private BreedingView SelectedBreeding()
        {
            return breedingViews.FirstOrDefault(value => value.Id == selectedBreedingId);
        }

        private string selectedBreedingId;

        private void SelectPage(string page)
        {
            activePageId = page;
            compareMode = false;
            navigation.Select(page);
            uiDocument.Invalidate();
        }

        private void SelectPlant(ThingDef plant)
        {
            if (plant == null) return;
            selectedPlantDefName = plant.defName;
            SelectPage("plants");
        }

        private void SelectCultivar(string id)
        {
            if (id.NullOrEmpty()) return;
            selectedCultivarId = id;
            snapshotDirty = true;
            uiDocument.Invalidate();
        }

        private void SelectLineageNode(string rawId)
        {
            if (rawId.NullOrEmpty() || rawId.StartsWith("missing:", StringComparison.Ordinal)) return;
            selectedCultivarId = rawId;
            activePageId = "cultivars";
            compareMode = false;
            snapshotDirty = true;
            uiDocument.Invalidate();
        }

        private void SelectBreeding(string id)
        {
            selectedBreedingId = id;
            SelectPage("breeding");
        }

        private void ToggleComparison(string id, bool enabled)
        {
            if (enabled)
            {
                if (comparisonIds.Count >= MaximumComparisonCount && !comparisonIds.Contains(id))
                {
                    uiDocument.Toasts.Show("Compare is limited to " + MaximumComparisonCount + " cultivars.", InsightToastSeverity.Warning);
                    return;
                }
                comparisonIds.Add(id);
            }
            else comparisonIds.Remove(id);
            snapshotDirty = true;
            uiDocument.Invalidate();
        }

        private void ClearComparison()
        {
            comparisonIds.Clear();
            compareMode = false;
            snapshotDirty = true;
            uiDocument.Invalidate();
        }

        private void CloseCompare()
        {
            compareMode = false;
            uiDocument.Invalidate();
        }

        private static void SetListState(InsightUiVirtualList list, int count)
        {
            if (list == null) return;
            bool changed = list.ItemCount != count;
            list.ItemCount = Math.Max(0, count);
            if (changed) list.Refresh();
        }

        private void RenameSelected()
        {
            VarietyRecord variety = GameComponent_NovelSeeds.Instance?.GetVariety(selectedCultivarId);
            if (variety == null) return;
            Find.WindowStack.Add(new Dialog_RenameVariety(variety));
            snapshotDirty = true;
        }

        private void ToggleFavorite()
        {
            VarietyRecord variety = GameComponent_NovelSeeds.Instance?.GetVariety(selectedCultivarId);
            if (variety == null) return;
            variety.registryFavorite = !variety.registryFavorite;
            snapshotDirty = true;
            uiDocument.Invalidate();
        }

        private void ToggleArchived()
        {
            VarietyRecord variety = GameComponent_NovelSeeds.Instance?.GetVariety(selectedCultivarId);
            if (variety == null) return;
            variety.registryArchived = !variety.registryArchived;
            snapshotDirty = true;
            uiDocument.Invalidate();
        }

        private void LocateSelected()
        {
            if (selectedCultivarId.NullOrEmpty() || !availability.TryGetValue(selectedCultivarId, out AvailabilityView stock) || stock.Target == null) return;
            CameraJumper.TryJumpAndSelect(stock.Target);
        }

        private KnowledgeRank VisiblePlantRank(ThingDef plant)
        {
            if (plant == null) return KnowledgeRank.Novice;
            return knowledgeScope == KnowledgeMenuScope.Colony
                ? HorticultureKnowledgeAdapter.TierFor(plant, null, true)
                : HorticultureKnowledgeAdapter.TierFor(plant, selectedPawn, false);
        }

        private KnowledgeRank VisibleCultivarRank(VarietyRecord variety)
        {
            if (variety == null) return KnowledgeRank.Novice;
            KnowledgeRank cultivar = HorticultureKnowledgeAdapter.CultivarTierFor(variety,
                knowledgeScope == KnowledgeMenuScope.Colony ? null : selectedPawn, knowledgeScope == KnowledgeMenuScope.Colony);
            return (KnowledgeRank)Math.Max((int)VisiblePlantRank(variety.cropDef), (int)cultivar);
        }

        private static int LineageDepth(VarietyRecord variety)
        {
            return LineageDepth(variety, new HashSet<string>(StringComparer.Ordinal), 0);
        }

        private static int LineageDepth(VarietyRecord variety, HashSet<string> path, int depth)
        {
            if (variety == null || depth >= MaximumLineageDepth || !path.Add(variety.id ?? string.Empty)) return 0;
            int result = 1;
            foreach (string parentId in variety.parentVarietyIds ?? new List<string>())
                result = Math.Max(result, 1 + LineageDepth(GameComponent_NovelSeeds.Instance?.GetVariety(parentId), path, depth + 1));
            path.Remove(variety.id ?? string.Empty);
            return result;
        }

        private RegistryAvailabilityRead AvailabilityFor(VarietyRecord variety)
        {
            if (variety != null && availability.TryGetValue(variety.id, out AvailabilityView view))
                return new RegistryAvailabilityRead(view.Plants, view.Produce, view.SeedPacks, view.Target);
            return new RegistryAvailabilityRead(0, 0, 0, null);
        }

        private static string TraitText(VarietyRecord variety, KnowledgeRank rank)
        {
            return rank < KnowledgeRank.Adept ? "Unknown until Adept knowledge." :
                (variety?.traits == null || variety.traits.Count == 0 ? "No recorded traits" : NovelSeedUtility.TraitSummary(variety.traits));
        }

        private static string ModifierText(VarietyRecord variety, KnowledgeRank rank)
        {
            if (rank < KnowledgeRank.Expert || variety == null) return rank < KnowledgeRank.Expert ? "Unknown until Expert knowledge." : "";
            return "Yield " + NovelSeedUtility.YieldFactor(variety.traits).ToStringPercent() + "; growth " +
                NovelSeedUtility.GrowthRateFactor(variety.traits).ToStringPercent() + "; sow " +
                ExpandedTraitUtility.SowWorkFactor(variety.traits).ToStringPercent();
        }

        private static string ProductText(VarietyRecord variety, KnowledgeRank rank)
        {
            if (rank < KnowledgeRank.Master) return "Unknown until Master knowledge.";
            List<string> products = new List<string>();
            if (variety?.cropDef?.plant?.harvestedThingDef != null) products.Add(variety.cropDef.plant.harvestedThingDef.LabelCap.ToString());
            products.AddRange((variety?.traits ?? new List<VarietyTraitDef>()).Where(trait => trait?.byproductDef != null)
                .Select(trait => trait.byproductDef.LabelCap.ToString()));
            return products.Count == 0 ? "None recorded" : string.Join(", ", products.Distinct());
        }

        private string PlantDetailStatus()
        {
            PlantView plant = SelectedPlant();
            return plant == null ? string.Empty : plant.Discovered ? "Discovered" : "Undiscovered plant — details are intentionally masked.";
        }

        private string PlantGrowthDetail()
        {
            PlantView plant = SelectedPlant();
            if (plant == null) return string.Empty;
            if (plant.Rank < KnowledgeRank.Adept) return "Growing details are unknown until Adept knowledge.";
            return "Growth: " + plant.GrowDays.ToString("0.##") + " days; harvest yield " + plant.HarvestYield.ToString("0.##");
        }

        private string CultivarStatus()
        {
            CultivarView view = SelectedCultivar();
            if (view == null) return string.Empty;
            return view.CropLabel + " · " + view.Rank + " · " + view.Origin + " · generation " + view.Generation +
                " · plants " + view.Plants + ", produce " + view.Produce + ", seed packs " + view.SeedPacks;
        }

        private string CultivarTraits()
        {
            CultivarView view = SelectedCultivar();
            return view == null ? string.Empty : "Traits: " + view.Traits + "\nProducts: " + view.Products;
        }

        private void UpdateTraitChips()
        {
            CultivarView view = SelectedCultivar();
            List<VarietyTraitDef> traits = view?.Authority?.traits ?? new List<VarietyTraitDef>();
            if (view == null || view.Rank < KnowledgeRank.Adept)
            {
                for (int index = 0; index < traitChips.Count; index++)
                {
                    traitChips[index].Text = index == 0 ? "Traits unknown" : string.Empty;
                    traitChips[index].Visible = index == 0;
                    traitChips[index].Color = uiDocument.Theme.Unknown;
                }
                return;
            }
            List<VarietyTraitDef> visible = traits.Where(trait => trait != null).Distinct().Take(traitChips.Count).ToList();
            for (int index = 0; index < traitChips.Count; index++)
            {
                VarietyTraitDef trait = index < visible.Count ? visible[index] : null;
                traitChips[index].Text = trait == null ? string.Empty : (trait.LabelCap.ToString() ?? trait.defName ?? "Trait");
                traitChips[index].Visible = trait != null;
                traitChips[index].Color = trait == null ? uiDocument.Theme.Unknown :
                    (trait.positive ? uiDocument.Theme.Positive : uiDocument.Theme.Warning);
            }
        }

        private string CultivarModifiers()
        {
            CultivarView view = SelectedCultivar();
            return view == null ? string.Empty : "Modifiers: " + view.Modifiers;
        }

        private string KnowledgeGuidance()
        {
            if (knowledgeDiagnostic != null && !knowledgeDiagnostic.Available)
                return "Knowledge Framework is not ready or compatible. Horticulture gameplay remains available; evidence will appear when the framework is usable.";
            return knowledgeViews.Count == 0 ? "No evidence yet. Sow, observe, harvest, and preserve a variety." :
                "Evidence is supplied by the Horticulture Knowledge Framework adapter.";
        }

        private void DrawComparison(InsightUiCustomDrawContext context)
        {
            context.Painter.Text(context.Bounds, ComparisonText(), InsightUiTextStyle.Body,
                context.Frame.Theme.PrimaryText, true, context.Frame);
        }

        private InsightUiSize MeasureComparison(InsightUiConstraints constraints, InsightUiFrame frame)
        {
            return new InsightUiSize(Math.Max(1f, constraints.MaxWidth), Math.Max(160f, 48f + comparisonIds.Count * 86f));
        }

        private string ComparisonText()
        {
            List<CultivarView> selected = comparisonIds.Select(id => allCultivarViews.FirstOrDefault(view => view.Id == id))
                .Where(view => view != null).OrderBy(view => view.CropLabel).ThenBy(view => view.Label).ToList();
            if (selected.Count < 2) return "Select at least two cultivars to compare.";
            List<string> lines = new List<string>
            {
                string.Join("  |  ", selected.Select(view => view.Label)),
                "Plant: " + string.Join("  |  ", selected.Select(view => view.CropLabel)),
                "Knowledge evidence: " + string.Join("  |  ", selected.Select(view => comparisonConfidence.TryGetValue(view.Id, out string value) ? value : "No evidence")),
                "Traits: " + string.Join("  |  ", selected.Select(view => view.Traits)),
                "Modifiers: " + string.Join("  |  ", selected.Select(view => view.Modifiers)),
                "Lineage: " + string.Join("  |  ", selected.Select(view => view.Generation + " generations / " + view.ParentCount + " parents")),
                "Products: " + string.Join("  |  ", selected.Select(view => view.Products)),
                "Meaningful differences: " + ComparisonDifferences(selected)
            };
            return string.Join("\n", lines);
        }

        private static string ComparisonDifferences(IReadOnlyList<CultivarView> selected)
        {
            List<string> differences = new List<string>();
            if (selected.Select(value => value.Rank).Distinct().Count() > 1) differences.Add("Knowledge rank differs");
            if (selected.Select(value => value.Traits).Distinct(StringComparer.Ordinal).Count() > 1) differences.Add("traits differ");
            if (selected.Select(value => value.Modifiers).Distinct(StringComparer.Ordinal).Count() > 1) differences.Add("modifiers differ");
            if (selected.Select(value => value.Products).Distinct(StringComparer.Ordinal).Count() > 1) differences.Add("products differ");
            if (selected.Select(value => value.Generation).Distinct().Count() > 1 ||
                selected.Select(value => value.ParentCount).Distinct().Count() > 1) differences.Add("lineage differs");
            return differences.Count == 0 ? "No differences in known values." : string.Join(", ", differences);
        }

        private void DrawLineageGraph(InsightUiCustomDrawContext context)
        {
            if (lineageLayout == null || lineageSnapshot == null || lineageLayout.ActiveNodeCount == 0)
            {
                context.Painter.Text(context.Bounds, "Select a cultivar with recorded or unknown parents.", InsightUiTextStyle.Caption,
                    context.Frame.Theme.SecondaryText, true, context.Frame);
                return;
            }
            IInsightUiCustomPainter custom = context.Painter as IInsightUiCustomPainter;
            float minX = lineageLayout.ActiveNodeIds.Select(id => lineageLayout.Position(id).X).DefaultIfEmpty(0f).Min();
            float maxX = lineageLayout.ActiveNodeIds.Select(id => lineageLayout.Position(id).X).DefaultIfEmpty(1f).Max();
            float minY = lineageLayout.ActiveNodeIds.Select(id => lineageLayout.Position(id).Y).DefaultIfEmpty(0f).Min();
            float maxY = lineageLayout.ActiveNodeIds.Select(id => lineageLayout.Position(id).Y).DefaultIfEmpty(1f).Max();
            float width = Math.Max(1f, maxX - minX);
            float height = Math.Max(1f, maxY - minY);
            foreach (InsightRelation relation in lineageLayout.Edges)
            {
                InsightPoint from = lineageLayout.Position(relation.FromId);
                InsightPoint to = lineageLayout.Position(relation.ToId);
                if (custom != null) custom.Line(
                    context.Bounds.X + 12f + (from.X - minX) / width * Math.Max(1f, context.Bounds.Width - 24f),
                    context.Bounds.Y + 12f + (from.Y - minY) / height * Math.Max(1f, context.Bounds.Height - 24f),
                    context.Bounds.X + 12f + (to.X - minX) / width * Math.Max(1f, context.Bounds.Width - 24f),
                    context.Bounds.Y + 12f + (to.Y - minY) / height * Math.Max(1f, context.Bounds.Height - 24f),
                    relation.Known ? context.Frame.Theme.Selected : context.Frame.Theme.Unknown, 1f, context.Frame);
            }
            foreach (string id in lineageLayout.ActiveNodeIds)
            {
                InsightPoint point = lineageLayout.Position(id);
                float x = context.Bounds.X + 12f + (point.X - minX) / width * Math.Max(1f, context.Bounds.Width - 24f);
                float y = context.Bounds.Y + 12f + (point.Y - minY) / height * Math.Max(1f, context.Bounds.Height - 24f);
                string label = lineageLabels.TryGetValue(id, out string value) ? value : id;
                InsightRect node = new InsightRect(x - 48f, y - 16f, 96f, 32f);
                if (custom != null) custom.FillRect(node, context.Frame.Theme.ElevatedSurface, context.Frame);
                context.Painter.Text(node, label, InsightUiTextStyle.Caption, context.Frame.Theme.PrimaryText, true, context.Frame);
            }
        }

        private InsightUiSize MeasureLineageGraph(InsightUiConstraints constraints, InsightUiFrame frame)
        {
            return new InsightUiSize(Math.Max(1f, constraints.MaxWidth), 280f);
        }

        private bool ApplyResponsiveLayout(float width)
        {
            InsightUiOrientation nextOrientation = width < 820f
                ? InsightUiOrientation.Vertical
                : InsightUiOrientation.Horizontal;
            if (splitOrientation == nextOrientation) return false;
            splitOrientation = nextOrientation;
            plantSplit.Orientation = splitOrientation;
            cultivarSplit.Orientation = splitOrientation;
            breedingSplit.Orientation = splitOrientation;
            return true;
        }

        private bool UpdatePresentationVisibility()
        {
            bool changed = false;
            bool collectionVisible = !compareMode;
            if (cultivarCollectionSurface.Visible != collectionVisible)
            {
                cultivarCollectionSurface.Visible = collectionVisible;
                changed = true;
            }
            if (comparisonSurface.Visible == collectionVisible)
            {
                comparisonSurface.Visible = !collectionVisible;
                changed = true;
            }
            bool compareEnabled = MainTabWindow_CultivarRegistry.CanCompareCount(comparisonIds.Count);
            if (compareButton.Enabled != compareEnabled)
            {
                compareButton.Enabled = compareEnabled;
                changed = true;
            }
            return changed;
        }

        private void SetSearch(ref string field, string value)
        {
            string normalized = value ?? string.Empty;
            if (field == normalized) return;
            field = normalized;
            snapshotDirty = true;
            uiDocument.Invalidate();
        }

        private void SetFilter(ref int field, int value)
        {
            if (field == value) return;
            field = Mathf.Clamp(value, 0, 3);
            snapshotDirty = true;
            uiDocument.Invalidate();
        }

        private void SetBool(ref bool field, bool value)
        {
            if (field == value) return;
            field = value;
            snapshotDirty = true;
            uiDocument.Invalidate();
        }

        private static string SafeId(string value)
        {
            if (value.NullOrEmpty()) return "unknown";
            char[] chars = value.Select(character => char.IsLetterOrDigit(character) || character == '_' || character == '-' ? character : '_').ToArray();
            return new string(chars);
        }

        private static int CountDuplicateIds(InsightUiElement element, HashSet<string> ids)
        {
            if (element == null) return 0;
            int duplicates = ids.Add(element.Id) ? 0 : 1;
            foreach (InsightUiElement child in element.Children ?? new InsightUiElement[0]) duplicates += CountDuplicateIds(child, ids);
            return duplicates;
        }

        private static InsightTheme CreateBotanicalTheme()
        {
            InsightTheme theme = InsightTheme.Default.Clone();
            theme.Id = "hns-botanical";
            theme.Selected = new InsightColor(0.35f, 0.68f, 0.42f);
            theme.Positive = new InsightColor(0.38f, 0.74f, 0.46f);
            theme.Warning = new InsightColor(0.88f, 0.68f, 0.26f);
            theme.Spacing = 7f;
            theme.CornerRadius = 4f;
            return theme;
        }

        private sealed class PlantView
        {
            public readonly ThingDef Definition;
            public readonly string Label;
            public readonly bool Discovered;
            public readonly KnowledgeRank Rank;
            public readonly int CultivarCount;
            public readonly float GrowDays;
            public readonly float HarvestYield;

            public PlantView(ThingDef definition, string label, bool discovered, KnowledgeRank rank, int cultivarCount,
                float growDays, float harvestYield)
            {
                Definition = definition;
                Label = label;
                Discovered = discovered;
                Rank = rank;
                CultivarCount = cultivarCount;
                GrowDays = growDays;
                HarvestYield = harvestYield;
            }
        }

        private sealed class CultivarView
        {
            public readonly string Id;
            public readonly string Label;
            public readonly string CropLabel;
            public readonly bool Favorite;
            public readonly bool Archived;
            public readonly KnowledgeRank Rank;
            public readonly string Origin;
            public readonly int Generation;
            public readonly string Traits;
            public readonly string Modifiers;
            public readonly string Products;
            public readonly int Plants;
            public readonly int Produce;
            public readonly int SeedPacks;
            public readonly int ParentCount;
            public readonly VarietyRecord Authority;

            public CultivarView(string id, string label, string cropLabel, bool favorite, bool archived, KnowledgeRank rank,
                string origin, int generation, string traits, string modifiers, string products, int plants, int produce,
                int seedPacks, int parentCount, VarietyRecord authority)
            {
                Id = id ?? string.Empty;
                Label = label ?? string.Empty;
                CropLabel = cropLabel ?? string.Empty;
                Favorite = favorite;
                Archived = archived;
                Rank = rank;
                Origin = origin ?? string.Empty;
                Generation = generation;
                Traits = traits ?? string.Empty;
                Modifiers = modifiers ?? string.Empty;
                Products = products ?? string.Empty;
                Plants = plants;
                Produce = produce;
                SeedPacks = seedPacks;
                ParentCount = parentCount;
                Authority = authority;
            }
        }

        private sealed class BreedingView
        {
            public readonly string Id;
            public readonly string Name;
            public readonly string CropLabel;
            public readonly string DesiredTraits;
            public readonly bool Active;
            public readonly int NotifiedCount;
            public readonly int MatchingCount;

            public BreedingView(string id, string name, string cropLabel, string desiredTraits, bool active,
                int notifiedCount, int matchingCount)
            {
                Id = id ?? string.Empty;
                Name = name ?? string.Empty;
                CropLabel = cropLabel ?? string.Empty;
                DesiredTraits = desiredTraits ?? string.Empty;
                Active = active;
                NotifiedCount = notifiedCount;
                MatchingCount = matchingCount;
            }
        }

        private sealed class KnowledgeView
        {
            public readonly string Label;
            public readonly string SubjectId;
            public readonly string Status;
            public readonly string Stage;
            public readonly KnowledgeRank Rank;
            public readonly float Progress;
            public readonly float Confidence;
            public readonly ThingDef Plant;

            public KnowledgeView(string label, string subjectId, string status, string stage, KnowledgeRank rank,
                float progress, float confidence, ThingDef plant)
            {
                Label = label ?? string.Empty;
                SubjectId = subjectId ?? string.Empty;
                Status = status ?? string.Empty;
                Stage = stage ?? string.Empty;
                Rank = rank;
                Progress = progress;
                Confidence = confidence;
                Plant = plant;
            }
        }

        private sealed class LineageNodeView
        {
            public readonly string GraphId;
            public readonly string Label;
            public readonly string RawId;

            public LineageNodeView(string graphId, string label, string rawId)
            {
                GraphId = graphId;
                Label = label;
                RawId = rawId;
            }
        }

        private sealed class AvailabilityView
        {
            public int Plants;
            public int Produce;
            public int SeedPacks;
            public Thing Target;
        }

        private readonly struct RegistryAvailabilityRead
        {
            public readonly int Plants;
            public readonly int Produce;
            public readonly int SeedPacks;
            public readonly Thing Target;

            public RegistryAvailabilityRead(int plants, int produce, int seedPacks, Thing target)
            {
                Plants = plants;
                Produce = produce;
                SeedPacks = seedPacks;
                Target = target;
            }
        }

        private readonly struct LineageVisit
        {
            public readonly string RawId;
            public readonly int Depth;
            public readonly VarietyRecord Record;

            public LineageVisit(string rawId, int depth, VarietyRecord record)
            {
                RawId = rawId;
                Depth = depth;
                Record = record;
            }
        }

        private sealed class LineageGraphBuild
        {
            public readonly InsightModel Model;
            public readonly Dictionary<string, string> GraphIds = new Dictionary<string, string>(StringComparer.Ordinal);
            public readonly Dictionary<string, string> Labels = new Dictionary<string, string>(StringComparer.Ordinal);
            public readonly Dictionary<string, string> RawIds = new Dictionary<string, string>(StringComparer.Ordinal);
            public int EdgeCount;
            public bool Complete = true;
            public string Validation = string.Empty;

            public LineageGraphBuild(string rootId)
            {
                Model = InsightModel.Create("hns.lineage." + SafeId(rootId));
            }

            public int NodeCount => GraphIds.Count;
        }

        private sealed class KnowledgeFrameworkDiagnosticView
        {
            public readonly bool Available;

            public KnowledgeFrameworkDiagnosticView(HorticultureKnowledgeDiagnosticSnapshot source)
            {
                Available = source?.IsUsable == true;
            }
        }
    }
}
