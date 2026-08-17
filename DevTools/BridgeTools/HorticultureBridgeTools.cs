using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RimBridgeServer.Sdk;
using ProgressionAgriculture;
using RimWorld;
using Verse;
using HorticultureNovelSeeds;

namespace HorticultureNovelSeeds.BridgeTools;

public sealed class HorticultureBridgeTools
{
    [Tool("horticulture/run_suite", Title = "Run Horticulture live suite",
        Description = "Run deterministic Horticulture assertions in the current real RimWorld game. " +
                      "The companion owns fixtures and evidence; DevBridge2 owns lifecycle and the lease.",
        ResultDescription = "A Horticulture evidence manifest and assertion report.",
        Tags = new[] { "horticulture", "testing", "destructive" }, RequiresAuth = true)]
    public async Task<object> RunSuite(
        [ToolParameter(Description = "Suite name: complete, startup, ux-discovery, ordinary-crop, sowable-tree, cross-pollination, produce-processing, knowledge, negative, long-running, auto-mask-suite, or save-reload")]
        string scenario = "complete",
        [ToolParameter(Description = "Real game ticks to advance before assertions")]
        int warmupTicks = 120,
        [ToolParameter(Description = "Real game ticks to advance after assertions")]
        int settleTicks = 30,
        [ToolParameter(Description = "Capture a full-frame screenshot through RimBridgeServer")]
        bool captureScreenshot = false,
        IRimBridgeContext context = null,
        CancellationToken cancellationToken = default)
    {
        if (context == null)
            throw new InvalidOperationException("RimBridgeServer did not inject an execution context.");

        warmupTicks = Math.Max(0, Math.Min(warmupTicks, 2000));
        settleTicks = Math.Max(0, Math.Min(settleTicks, 1000));
        string normalizedScenario = NormalizeScenario(scenario);
        RimBridgeEvidenceManifest evidence = RimBridgeEvidence.CreateManifest(
            "Horticulture.NovelSeeds.LiveSuite", Guid.NewGuid().ToString("N"));
        HorticultureBridgeRunResult result = new HorticultureBridgeRunResult
        {
            scenario = normalizedScenario
        };

        AddBridgeAssertions(evidence, context);
        if (warmupTicks > 0)
        {
            RimBridgeTickResult warmup = await context.Game.RunForTicksAsync(warmupTicks,
                new RimBridgeRunTicksOptions { TimeoutMs = 30000, ForceNormalSpeed = true, PauseWhenDone = true },
                cancellationToken).ConfigureAwait(true);
            evidence.assertions.Add(RimBridgeEvidence.IsTrue("bridge-warmup-ticks", warmup.Success,
                warmup.Message, new { warmup.RequestedTicks, warmup.CompletedTicks, warmup.StartTicksGame, warmup.EndTicksGame }));
            if (!warmup.Success)
                evidence.errors.Add(new RimBridgeEvidenceError { stage = "warmup", message = warmup.Message });
        }

        HorticultureSuiteReport report;
        if (normalizedScenario == "save-reload")
            report = await RunSaveReload(context, cancellationToken).ConfigureAwait(true);
        else
        {
            report = await context.MainThread.InvokeAsync(
                () => HorticultureSuite.Run(normalizedScenario), cancellationToken).ConfigureAwait(true);
            if (normalizedScenario == "complete")
            {
                HorticultureSuiteReport persistence = await RunSaveReload(context, cancellationToken).ConfigureAwait(true);
                report.Merge(persistence);
            }
        }
        if (settleTicks > 0)
        {
            RimBridgeTickResult settle = await context.Game.RunForTicksAsync(settleTicks,
                new RimBridgeRunTicksOptions { TimeoutMs = 30000, ForceNormalSpeed = true, PauseWhenDone = true },
                cancellationToken).ConfigureAwait(true);
            evidence.assertions.Add(RimBridgeEvidence.IsTrue("bridge-settle-ticks", settle.Success,
                settle.Message, new { settle.RequestedTicks, settle.CompletedTicks, settle.StartTicksGame, settle.EndTicksGame }));
            if (!settle.Success)
                evidence.errors.Add(new RimBridgeEvidenceError { stage = "settle", message = settle.Message });
        }

        if (captureScreenshot && context.Tools.Exists("rimworld/take_screenshot"))
        {
            RimBridgeToolCallResult<object> screenshot = await context.Tools.CallAsync<object>(
                "rimworld/take_screenshot",
                new { fileName = "horticulture-live-" + evidence.runId, suppressMessage = true },
                new RimBridgeToolCallOptions { TimeoutMs = 30000 }, cancellationToken).ConfigureAwait(true);
            evidence.assertions.Add(RimBridgeEvidence.ToolSucceeded("bridge-screenshot", screenshot));
            if (screenshot.Success)
                evidence.captures.Add(new RimBridgeEvidenceCapture
                {
                    label = "horticulture-live",
                    kind = "rimbridge-screenshot",
                    details = screenshot.Result
                });
        }
        else if (captureScreenshot)
            evidence.assertions.Add(RimBridgeEvidence.Fail("bridge-screenshot", "RimBridgeServer screenshot capability is unavailable."));

        evidence.environment.gameVersion = VersionControl.CurrentVersionString;
        evidence.environment.modVersion = typeof(HorticultureBridgeTools).Assembly.GetName().Version?.ToString() ?? string.Empty;
        evidence.environment.details = new
        {
            tool = "horticulture/run_suite",
            bridgeClock = true,
            bridgeToolDiscovery = true,
            scenario = normalizedScenario
        };
        evidence.assertions.Add(RimBridgeEvidence.IsTrue("horticulture-suite", report.status == "PASS",
            "Horticulture live suite status: " + report.status,
            new { report.assertionCount, report.passedAssertions, report.failedAssertionsCount, report.blockedAssertionsCount }));
        if (report.failedAssertionsCount > 0)
            evidence.errors.Add(new RimBridgeEvidenceError
            {
                stage = "horticulture-suite",
                message = string.Join("; ", report.failedAssertions)
            });
        RimBridgeEvidence.Complete(evidence);
        result.success = evidence.success;
        result.evidence = HorticultureBridgeEvidenceSummary.From(evidence);
        result.report = HorticultureSuiteReportSummary.From(report);
        return result;
    }

    [Tool("horticulture/get_test_surface", Title = "Describe Horticulture test surface",
        Description = "Return the scenarios and ownership boundary for the Horticulture companion.",
        Tags = new[] { "horticulture", "testing", "read-only" })]
    public object GetTestSurface()
    {
        return new
        {
            success = true,
            owner = "Horticulture Novel Seeds companion",
            coordinator = "DevBridge2",
            bridge = "RimBridgeServer",
            scenarios = HorticultureSuite.Scenarios,
            oldHarnessRemoved = true,
            requestFiles = false,
            harmonyTestBootstrap = false,
            gameComponentTestRunner = false
        };
    }

    private static void AddBridgeAssertions(RimBridgeEvidenceManifest evidence, IRimBridgeContext context)
    {
        IReadOnlyList<RimBridgeToolDescriptor> tools = context.Tools.List();
        evidence.assertions.Add(RimBridgeEvidence.IsTrue("bridge-tool-discovery", tools != null && tools.Count > 0,
            "RimBridgeServer tool surface was discovered.", new { toolCount = tools?.Count ?? 0 }));
        evidence.assertions.Add(RimBridgeEvidence.IsTrue("bridge-live-game-info",
            context.Tools.Exists("rimworld/get_game_info"),
            "RimBridgeServer live game inspection is available."));
        evidence.assertions.Add(RimBridgeEvidence.IsTrue("bridge-tick-control",
            context.Game != null, "RimBridgeServer real tick control was injected."));
    }

    private static async Task<HorticultureSuiteReport> RunSaveReload(IRimBridgeContext context,
        CancellationToken cancellationToken)
    {
        const string scenario = "save-reload";
        HorticultureSuiteReport report = new HorticultureSuiteReport(scenario);
        string saveName = "Horticulture_LiveSuite_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        string varietyId = null;
        try
        {
            await context.MainThread.InvokeAsync(() =>
            {
                ThingDef crop = HorticultureSuite.FindCrop(false);
                Pawn observer = HorticultureSuite.Observer();
                List<VarietyTraitDef> traits = HorticultureSuite.GetTraits(1);
                Require(crop != null && observer != null && traits.Count > 0,
                    "A supported crop, colonist, and trait were required for save/reload.");
                VarietyRecord variety = GameComponent_NovelSeeds.Instance.UnlockVariety(crop, traits,
                    "Horticulture live save fixture", hiddenFromMenus: true, discoverer: observer,
                    originKind: "mutation");
                Require(variety != null, "The save/reload cultivar fixture could not be created.");
                varietyId = variety.id;
            }, cancellationToken).ConfigureAwait(true);

            RimBridgeToolCallResult<object> save = await context.Tools.CallAsync<object>("rimworld/save_game",
                new { saveName }, new RimBridgeToolCallOptions { TimeoutMs = 30000 }, cancellationToken).ConfigureAwait(true);
            report.CheckTool("save-game", save);
            if (!save.Success)
                return report;

            RimBridgeToolCallResult<object> load = await context.Tools.CallAsync<object>("rimworld/load_game_ready",
                new { saveName, readiness = "mapData", ignoreModCompatibility = false },
                new RimBridgeToolCallOptions { TimeoutMs = 120000 }, cancellationToken).ConfigureAwait(true);
            report.CheckTool("load-game-ready", load);
            if (!load.Success)
                return report;

            await context.Game.RunForTicksAsync(30,
                new RimBridgeRunTicksOptions { TimeoutMs = 30000, ForceNormalSpeed = true, PauseWhenDone = true },
                cancellationToken).ConfigureAwait(true);
            await context.MainThread.InvokeAsync(() =>
            {
                VarietyRecord restored = GameComponent_NovelSeeds.Instance.GetVariety(varietyId);
                report.Check("save-reload-cultivar", () =>
                {
                    Require(restored != null, "The cultivar ID was not restored after RimBridgeServer load_game_ready.");
                    return "restored cultivar=" + restored.id;
                });
                report.Check("save-reload-knowledge", () =>
                {
                    Require(HorticultureKnowledgeAdapter.Diagnostics != null,
                        "Knowledge diagnostics disappeared after reload.");
                    return HorticultureKnowledgeAdapter.Diagnostics.ToString();
                });
            }, cancellationToken).ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            report.Fail("save-reload", exception);
        }
        finally
        {
            try
            {
                await context.MainThread.InvokeAsync(() =>
                {
                    string path = GenFilePaths.FilePathForSavedGame(saveName);
                    if (File.Exists(path)) File.Delete(path);
                }, cancellationToken).ConfigureAwait(true);
            }
            catch (Exception cleanupException)
            {
                report.diagnostics.Add("save cleanup: " + cleanupException.Message);
            }
        }
        report.Complete();
        return report;
    }

    private static string NormalizeScenario(string scenario)
    {
        string value = (scenario ?? "complete").Trim().ToLowerInvariant();
        return HorticultureSuite.Scenarios.Contains(value, StringComparer.Ordinal) ? value : value;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

public sealed class HorticultureBridgeRunResult
{
    public bool success { get; set; }
    public string scenario { get; set; }
    public HorticultureBridgeEvidenceSummary evidence { get; set; }
    public HorticultureSuiteReportSummary report { get; set; }
}

public sealed class HorticultureBridgeEvidenceSummary
{
    public string runId { get; set; }
    public bool success { get; set; }
    public int assertionCount { get; set; }
    public int errorCount { get; set; }
    public List<string> errors { get; set; } = new List<string>();

    public static HorticultureBridgeEvidenceSummary From(RimBridgeEvidenceManifest manifest)
    {
        return new HorticultureBridgeEvidenceSummary
        {
            runId = manifest?.runId ?? string.Empty,
            success = manifest?.success == true,
            assertionCount = manifest?.assertions?.Count ?? 0,
            errorCount = manifest?.errors?.Count ?? 0,
            errors = (manifest?.errors ?? new List<RimBridgeEvidenceError>())
                .Where(error => error != null && !string.IsNullOrEmpty(error.message))
                .Select(error => error.message)
                .Take(8)
                .ToList()
        };
    }
}

public sealed class HorticultureSuiteReportSummary
{
    public string schemaVersion { get; set; }
    public string suiteVersion { get; set; }
    public string suite { get; set; }
    public string scenario { get; set; }
    public string status { get; set; }
    public int startTick { get; set; }
    public int endTick { get; set; }
    public int elapsedTicks { get; set; }
    public int assertionCount { get; set; }
    public int passedAssertions { get; set; }
    public int failedAssertionsCount { get; set; }
    public int blockedAssertionsCount { get; set; }
    public List<string> failedAssertions { get; set; } = new List<string>();
    public List<string> exceptionDetails { get; set; } = new List<string>();
    public List<string> diagnostics { get; set; } = new List<string>();

    public static HorticultureSuiteReportSummary From(HorticultureSuiteReport report)
    {
        return new HorticultureSuiteReportSummary
        {
            schemaVersion = report?.schemaVersion ?? string.Empty,
            suiteVersion = report?.suiteVersion ?? string.Empty,
            suite = report?.suite ?? string.Empty,
            scenario = report?.scenario ?? string.Empty,
            status = report?.status ?? "FAIL",
            startTick = report?.startTick ?? 0,
            endTick = report?.endTick ?? 0,
            elapsedTicks = report?.elapsedTicks ?? 0,
            assertionCount = report?.assertionCount ?? 0,
            passedAssertions = report?.passedAssertions ?? 0,
            failedAssertionsCount = report?.failedAssertionsCount ?? 0,
            blockedAssertionsCount = report?.blockedAssertionsCount ?? 0,
            failedAssertions = (report?.failedAssertions ?? new List<string>()).Take(32).ToList(),
            exceptionDetails = (report?.exceptionDetails ?? new List<string>()).Take(4).ToList(),
            diagnostics = (report?.diagnostics ?? new List<string>()).Take(8).ToList()
        };
    }
}

public sealed class HorticultureSuiteReport
{
    public string schemaVersion = "3";
    public string suiteVersion = "2.0";
    public string suite = "Horticulture.NovelSeeds.LiveSuite";
    public string scenario;
    public string status = "PASS";
    public int startTick;
    public int endTick;
    public int elapsedTicks;
    public int assertionCount;
    public int passedAssertions;
    public int failedAssertionsCount;
    public int blockedAssertionsCount;
    public List<string> failedAssertions = new List<string>();
    public List<string> exceptionDetails = new List<string>();
    public List<string> diagnostics = new List<string>();
    public List<HorticultureSuiteAssertion> assertions = new List<HorticultureSuiteAssertion>();

    public HorticultureSuiteReport() { }

    public HorticultureSuiteReport(string scenario)
    {
        this.scenario = scenario;
        startTick = Find.TickManager?.TicksGame ?? 0;
    }

    public void Check(string id, Func<string> action)
    {
        assertionCount++;
        try
        {
            string detail = action() ?? "ok";
            passedAssertions++;
            assertions.Add(new HorticultureSuiteAssertion { id = id, status = "PASS", detail = detail });
        }
        catch (HorticultureSuiteBlockedException exception)
        {
            blockedAssertionsCount++;
            assertions.Add(new HorticultureSuiteAssertion { id = id, status = "BLOCKED", detail = exception.Message });
        }
        catch (Exception exception)
        {
            Fail(id, exception);
        }
    }

    public void CheckTool(string id, IRimBridgeToolCallResult result)
    {
        assertionCount++;
        if (result != null && result.Succeeded())
        {
            passedAssertions++;
            assertions.Add(new HorticultureSuiteAssertion { id = id, status = "PASS", detail = result.Status });
        }
        else
        {
            failedAssertionsCount++;
            failedAssertions.Add(id);
            string detail = result?.Error?.Message ?? "RimBridgeServer tool call failed.";
            assertions.Add(new HorticultureSuiteAssertion { id = id, status = "FAIL", detail = detail });
        }
    }

    public void Fail(string id, Exception exception)
    {
        status = "FAIL";
        failedAssertionsCount++;
        failedAssertions.Add(id);
        exceptionDetails.Add(exception.ToString());
        assertions.Add(new HorticultureSuiteAssertion
        {
            id = id,
            status = "FAIL",
            detail = exception.Message,
            exception = exception.ToString()
        });
    }

    public void Merge(HorticultureSuiteReport other)
    {
        if (other == null) return;
        assertionCount += other.assertionCount;
        passedAssertions += other.passedAssertions;
        failedAssertionsCount += other.failedAssertionsCount;
        blockedAssertionsCount += other.blockedAssertionsCount;
        failedAssertions.AddRange(other.failedAssertions);
        exceptionDetails.AddRange(other.exceptionDetails);
        diagnostics.AddRange(other.diagnostics);
        assertions.AddRange(other.assertions);
        if (other.status == "FAIL") status = "FAIL";
        else if (other.status == "BLOCKED" && status == "PASS") status = "BLOCKED";
        endTick = other.endTick;
        elapsedTicks = Math.Max(0, endTick - startTick);
    }

    public void Complete()
    {
        endTick = Find.TickManager?.TicksGame ?? startTick;
        elapsedTicks = Math.Max(0, endTick - startTick);
        if (failedAssertionsCount > 0) status = "FAIL";
        else if (blockedAssertionsCount > 0) status = "BLOCKED";
    }
}

public sealed class HorticultureSuiteAssertion
{
    public string id;
    public string status;
    public string detail;
    public string exception;
}

internal sealed class HorticultureSuiteBlockedException : Exception
{
    public HorticultureSuiteBlockedException(string message) : base(message) { }
}

internal static class HorticultureSuite
{
    internal static readonly string[] Scenarios =
    {
        "complete", "startup", "ux-discovery", "ordinary-crop", "sowable-tree",
        "cross-pollination", "produce-processing", "knowledge", "negative",
        "long-running", "auto-mask-suite", "save-reload"
    };

    private static readonly List<Thing> Fixtures = new List<Thing>();

    internal static HorticultureSuiteReport Run(string scenario)
    {
        HorticultureSuiteReport report = new HorticultureSuiteReport(scenario);
        try
        {
            switch (scenario)
            {
                case "complete":
                    Startup(report); UxDiscovery(report); OrdinaryCrop(report); SowableTree(report);
                    CrossPollination(report); ProduceProcessing(report); Knowledge(report);
                    Negative(report); LongRunning(report); AutoMaskSuite(report);
                    break;
                case "startup": Startup(report); break;
                case "ux-discovery": UxDiscovery(report); break;
                case "ordinary-crop": OrdinaryCrop(report); break;
                case "sowable-tree": SowableTree(report); break;
                case "cross-pollination": CrossPollination(report); break;
                case "produce-processing": ProduceProcessing(report); break;
                case "knowledge": Knowledge(report); break;
                case "negative": Negative(report); break;
                case "long-running": LongRunning(report); break;
                case "auto-mask-suite": AutoMaskSuite(report); break;
                default: report.Fail("scenario", new ArgumentException("Unknown scenario: " + scenario)); break;
            }
        }
        catch (Exception exception)
        {
            report.Fail("scenario", exception);
        }
        finally
        {
            CleanupFixtures();
            report.Complete();
        }
        return report;
    }

    internal static ThingDef FindCrop(bool tree)
    {
        return DefDatabase<ThingDef>.AllDefsListForReading
            .Where(def => HorticulturePlantPolicy.IsSupported(def))
            .Where(def => HorticulturePlantPolicy.IsSowableTree(def) == tree)
            .OrderBy(def => def.defName, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    internal static Pawn Observer()
    {
        return Find.CurrentMap?.mapPawns?.FreeColonistsSpawned?.FirstOrDefault();
    }

    internal static List<VarietyTraitDef> GetTraits(int count)
    {
        return TraitConfigUtility.TopLevelTraits()
            .Where(def => def != null)
            .GroupBy(def => def.configFamily.NullOrEmpty() ? def.defName : def.configFamily)
            .Select(group => group.First())
            .Take(Math.Max(1, count)).ToList();
    }

    private static void Startup(HorticultureSuiteReport report)
    {
        report.Check("startup-game-component", () =>
        {
            Require(GameComponent_NovelSeeds.Instance != null, "GameComponent_NovelSeeds is unavailable.");
            Require(HorticultureNovelSeedsMod.Settings != null, "Horticulture settings were not loaded.");
            return "game component and settings are available";
        });
        report.Check("startup-supported-defs", () =>
        {
            int count = DefDatabase<ThingDef>.AllDefsListForReading.Count(HorticulturePlantPolicy.IsSupported);
            Require(count > 0, "No supported plant definitions were discovered.");
            return "supported plant defs=" + count;
        });
        report.Check("startup-knowledge", () =>
        {
            HorticultureKnowledgeDiagnosticSnapshot diagnostics = HorticultureKnowledgeAdapter.Diagnostics;
            Require(diagnostics != null && diagnostics.IsUsable, "Knowledge Framework is not usable: " + diagnostics);
            return diagnostics.ToString();
        });
    }

    private static void UxDiscovery(HorticultureSuiteReport report)
    {
        report.Check("ux-keyed-guidance", () =>
        {
            string[] keys = { "HNS_SaveSeeds", "HNS_RegistryPlantsTab", "HNS_SettingsAdvancedShow" };
            foreach (string key in keys)
                Require(!key.Translate().ToString().NullOrEmpty() && key.Translate().ToString() != key,
                    "Missing keyed text: " + key);
            return "player-facing guidance keys are localized";
        });
        report.Check("ux-settings-document", () =>
        {
            Type documentType = GenTypes.GetTypeInAnyAssembly("HorticultureNovelSeeds.InsightSettingsDocument");
            Require(documentType != null, "Insight Canvas settings document type is missing.");
            return "Insight Canvas settings document is loaded";
        });
    }

    private static void OrdinaryCrop(HorticultureSuiteReport report)
    {
        ThingDef crop = FindCrop(false);
        if (crop == null) { Block(report, "ordinary-crop", "No supported crop was loaded."); return; }
        Plant plant = null;
        try
        {
            report.Check("ordinary-crop-spawn", () =>
            {
                plant = SpawnPlant(crop);
                Require(plant.TryGetComp<CompPlantVariety>() != null, "Spawned crop has no variety component.");
                return "spawned " + crop.defName;
            });
            report.Check("ordinary-crop-variety", () =>
            {
                VarietyRecord variety = GameComponent_NovelSeeds.Instance.UnlockVariety(crop, GetTraits(1),
                    "Horticulture live crop", hiddenFromMenus: true, discoverer: Observer(), originKind: "mutation");
                Require(variety != null, "Crop cultivar creation failed.");
                plant.TryGetComp<CompPlantVariety>().SetVariety(variety);
                plant.sown = true;
                plant.Growth = 1f;
                HorticultureEventRouter.SowingCompleted(Observer(), plant);
                HorticultureEventRouter.HarvestCompleted(Observer(), plant, 1);
                return "cultivar=" + variety.id;
            });
        }
        finally { DestroyFixture(plant); }
    }

    private static void SowableTree(HorticultureSuiteReport report)
    {
        ThingDef tree = FindCrop(true);
        if (tree == null) { Block(report, "sowable-tree", "No sowable tree was loaded."); return; }
        Plant plant = null;
        try
        {
            report.Check("sowable-tree-spawn", () =>
            {
                plant = SpawnPlant(tree);
                Require(HorticulturePlantPolicy.IsSowableTree(tree), "Selected tree is not sowable.");
                return "spawned " + tree.defName;
            });
            report.Check("sowable-tree-variety", () =>
            {
                VarietyRecord variety = GameComponent_NovelSeeds.Instance.UnlockVariety(tree, GetTraits(1),
                    "Horticulture live tree", hiddenFromMenus: true, discoverer: Observer(), originKind: "mutation");
                Require(variety != null, "Tree cultivar creation failed.");
                plant.TryGetComp<CompPlantVariety>().SetVariety(variety);
                HorticultureEventRouter.CuttingCompleted(Observer(), plant, 1);
                return "tree cultivar=" + variety.id;
            });
        }
        finally { DestroyFixture(plant); }
    }

    private static void CrossPollination(HorticultureSuiteReport report)
    {
        ThingDef crop = FindCrop(false);
        if (crop == null) { Block(report, "cross-pollination", "No supported crop was loaded."); return; }
        report.Check("cross-pollination-registry", () =>
        {
            List<VarietyTraitDef> traits = GetTraits(1);
            VarietyRecord first = GameComponent_NovelSeeds.Instance.UnlockVariety(crop, traits,
                "Horticulture parent A", hiddenFromMenus: true, discoverer: Observer(), originKind: "mutation");
            VarietyRecord second = GameComponent_NovelSeeds.Instance.UnlockVariety(crop, GetTraits(2),
                "Horticulture parent B", hiddenFromMenus: true, discoverer: Observer(), originKind: "mutation");
            Require(first != null && second != null && first.id != second.id, "Distinct parents were not created.");
            return "parents=" + first.id + "," + second.id;
        });
    }

    private static void ProduceProcessing(HorticultureSuiteReport report)
    {
        ThingDef crop = FindCrop(false);
        ThingDef produce = crop?.plant?.harvestedThingDef;
        if (produce == null) { Block(report, "produce-processing", "No harvested produce was loaded."); return; }
        report.Check("produce-processing-components", () =>
        {
            Thing item = ThingMaker.MakeThing(produce);
            Require(item != null, "Produce could not be instantiated.");
            return "produce=" + produce.defName;
        });
    }

    private static void Knowledge(HorticultureSuiteReport report)
    {
        ThingDef crop = FindCrop(false);
        if (crop == null) { Block(report, "knowledge", "No supported crop was loaded."); return; }
        report.Check("knowledge-registration", () =>
        {
            HorticultureKnowledgeDiagnosticSnapshot diagnostics = HorticultureKnowledgeAdapter.Diagnostics;
            Require(diagnostics != null && diagnostics.IsUsable, "Knowledge Framework is not usable.");
            return diagnostics.ToString();
        });
        Plant plant = null;
        try
        {
            report.Check("knowledge-events", () =>
            {
                plant = SpawnPlant(crop);
                Pawn observer = Observer();
                Require(observer != null, "No colonist was available as event observer.");
                HorticultureEventRouter.SowingCompleted(observer, plant);
                HorticultureEventRouter.GrowthObserved(observer, plant);
                HorticultureEventRouter.HarvestCompleted(observer, plant, 1);
                return "sowing, growth, and harvest events routed";
            });
        }
        finally { DestroyFixture(plant); }
    }

    private static void Negative(HorticultureSuiteReport report)
    {
        report.Check("negative-unsupported-plant", () =>
        {
            Require(!HorticulturePlantPolicy.IsSupported(ThingDefOf.Steel), "Steel was classified as a plant.");
            return "unsupported thing rejected";
        });
        report.Check("negative-missing-cultivar", () =>
        {
            Require(GameComponent_NovelSeeds.Instance.GetVariety("HNS_missing_live_cultivar") == null,
                "Missing cultivar unexpectedly resolved.");
            return "missing cultivar lookup is safe";
        });
        report.Check("negative-mask-cache", () =>
        {
            ThingDef crop = FindCrop(false);
            Require(crop != null, "No crop was available for mask cache inspection.");
            object record = PlantAutoMaskCache.GetRecord(crop, 0, generateIfMissing: false);
            return record == null ? "missing automatic mask safely returned no record" : "automatic mask record is readable";
        });
    }

    private static void LongRunning(HorticultureSuiteReport report)
    {
        report.Check("long-running-cache", () =>
        {
            ThingDef crop = FindCrop(false);
            Require(crop != null && GameComponent_NovelSeeds.Instance.AllVarieties != null,
                "Crop or cultivar cache became unavailable.");
            for (int index = 0; index < 4; index++)
            {
                Plant plant = SpawnPlant(crop);
                plant.Growth = 0.2f + index * 0.2f;
                HorticultureEventRouter.GrowthObserved(Observer(), plant);
            }
            return "repeated growth observations completed";
        });
    }

    private static void AutoMaskSuite(HorticultureSuiteReport report)
    {
        report.Check("auto-mask-safe-lookup", () =>
        {
            ThingDef crop = FindCrop(false);
            Require(crop != null, "No crop was available for automatic-mask testing.");
            object record = PlantAutoMaskCache.GetRecord(crop, 0, generateIfMissing: false, allowIdentityGeneration: true);
            return record == null ? "automatic mask safely unavailable" : "automatic mask lookup returned a record";
        });
        report.Check("auto-mask-generation", () =>
        {
            AutoMaskBatchResult result = PlantAutoMaskCache.GenerateMissing(false);
            return result.ToString();
        });
    }

    private static Plant SpawnPlant(ThingDef def)
    {
        Require(Find.CurrentMap != null, "Current map is unavailable.");
        IntVec3 cell = Find.CurrentMap.AllCells
            .FirstOrDefault(candidate => candidate.Standable(Find.CurrentMap) &&
                Find.CurrentMap.thingGrid.ThingsListAt(candidate).Count == 0);
        Require(cell.IsValid, "No empty standable fixture cell was available.");
        Plant plant = ThingMaker.MakeThing(def) as Plant;
        Require(plant != null, "ThingDef did not create a Plant: " + def.defName);
        GenSpawn.Spawn(plant, cell, Find.CurrentMap, WipeMode.Vanish);
        Fixtures.Add(plant);
        return plant;
    }

    private static void CleanupFixtures()
    {
        foreach (Thing fixture in Fixtures.ToList()) DestroyFixture(fixture);
        Fixtures.Clear();
    }

    private static void DestroyFixture(Thing fixture)
    {
        if (fixture == null) return;
        try
        {
            if (!fixture.Destroyed && fixture.Spawned) fixture.Destroy(DestroyMode.Vanish);
        }
        catch (ArgumentOutOfRangeException) { }
        catch (NullReferenceException) { }
        Fixtures.Remove(fixture);
    }

    private static void Block(HorticultureSuiteReport report, string id, string detail)
    {
        report.assertionCount++;
        report.blockedAssertionsCount++;
        report.assertions.Add(new HorticultureSuiteAssertion { id = id, status = "BLOCKED", detail = detail });
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
