using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using HarmonyLib;
using KnowledgeFramework;
using RimWorld;
using UnityEngine;
using Verse;
using HorticultureNovelSeeds;

namespace HorticultureNovelSeeds.RuntimeTests
{
    [StaticConstructorOnStartup]
    public static class RuntimeTestBootstrap
    {
        static RuntimeTestBootstrap()
        {
            const string legacyHarmonyId = "lan.horticulture.novelseeds.runtimetests";
            const string previousHarmonyId = "lan.horticulture.novelseeds.runtimetests.v12";
            const string currentHarmonyId = "lan.horticulture.novelseeds.runtimetests.v18";
            Harmony harmony = new Harmony(currentHarmonyId);
            harmony.UnpatchAll(legacyHarmonyId);
            harmony.UnpatchAll(previousHarmonyId);
            harmony.UnpatchAll("lan.horticulture.novelseeds.runtimetests.v13");
            harmony.UnpatchAll("lan.horticulture.novelseeds.runtimetests.v14");
            harmony.UnpatchAll("lan.horticulture.novelseeds.runtimetests.v15");
            harmony.UnpatchAll("lan.horticulture.novelseeds.runtimetests.v16");
            harmony.UnpatchAll("lan.horticulture.novelseeds.runtimetests.v17");
            harmony.UnpatchAll(currentHarmonyId);
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            HorticultureRuntimeTestRunner.Trace("bootstrap loaded assembly=" + Assembly.GetExecutingAssembly().FullName);
            Log.Message("[Horticulture][RuntimeTests] test assembly loaded");
        }
    }

    [HarmonyPatch(typeof(KnowledgeFramework.GameComponent_KnowledgeFramework), nameof(KnowledgeFramework.GameComponent_KnowledgeFramework.FinalizeInit))]
    internal static class RuntimeTestKnowledgeFinalizeProbe
    {
        [HarmonyPostfix]
        private static void Postfix()
        {
            string components = string.Join(",", (Verse.Current.Game?.components ?? new List<GameComponent>())
                .Where(component => component != null)
                .Select(component => component.GetType().FullName));
            string message = "probe KnowledgeFramework.FinalizeInit components=" + components
                + " horticulture=" + (GameComponent_NovelSeeds.Instance != null)
                + " diagnostics=" + (HorticultureKnowledgeAdapter.Diagnostics?.ToString() ?? "null");
            HorticultureRuntimeTestRunner.Trace(message);
            Log.Message("[Horticulture][RuntimeTests] " + message);
        }
    }

    [HarmonyPatch(typeof(GameComponent_NovelSeeds), nameof(GameComponent_NovelSeeds.FinalizeInit))]
    internal static class RuntimeTestHorticultureFinalizeProbe
    {
        [HarmonyPostfix]
        private static void Postfix()
        {
            string message = "probe Horticulture.FinalizeInit diagnostics="
                + (HorticultureKnowledgeAdapter.Diagnostics?.ToString() ?? "null");
            HorticultureRuntimeTestRunner.Trace(message);
            Log.Message("[Horticulture][RuntimeTests] " + message);
        }
    }

    [HarmonyPatch(typeof(GameComponentUtility), nameof(GameComponentUtility.FinalizeInit))]
    internal static class RuntimeTestComponentsFinalizeProbe
    {
        [HarmonyPostfix]
        private static void Postfix()
        {
            string message = "probe GameComponentUtility.FinalizeInit diagnostics="
                + (HorticultureKnowledgeAdapter.Diagnostics?.ToString() ?? "null");
            HorticultureRuntimeTestRunner.Trace(message);
            Log.Message("[Horticulture][RuntimeTests] " + message);
        }
    }

    [HarmonyPatch(typeof(Game), nameof(Game.InitNewGame))]
    internal static class RuntimeTestInitNewGameProbe
    {
        [HarmonyPostfix]
        private static void Postfix(Game __instance)
        {
            string message = "probe Game.InitNewGame postfix instance=" + RuntimeTestProbeUtility.GameIdentity(__instance)
                + " current=" + RuntimeTestProbeUtility.GameIdentity(Current.Game);
            HorticultureRuntimeTestRunner.Trace(message);
            Log.Message("[Horticulture][RuntimeTests] " + message);
        }
    }

    [HarmonyPatch(typeof(Game), nameof(Game.LoadGame))]
    internal static class RuntimeTestLoadGameProbe
    {
        [HarmonyPostfix]
        private static void Postfix(Game __instance)
        {
            string message = "probe Game.LoadGame postfix instance=" + RuntimeTestProbeUtility.GameIdentity(__instance)
                + " current=" + RuntimeTestProbeUtility.GameIdentity(Current.Game);
            HorticultureRuntimeTestRunner.Trace(message);
            Log.Message("[Horticulture][RuntimeTests] " + message);
        }
    }

    internal static class RuntimeTestProbeUtility
    {
        internal static string GameIdentity(Game game)
        {
            return game == null ? "null" : RuntimeHelpers.GetHashCode(game).ToString();
        }
    }

    public sealed class HorticultureRuntimeTestComponent : GameComponent
    {
        public HorticultureRuntimeTestComponent() { }
        public HorticultureRuntimeTestComponent(Game game) { }

        public override void GameComponentTick()
        {
            HorticultureRuntimeTestRunner.Tick();
        }
    }

    [HarmonyPatch(typeof(Game), nameof(Game.FinalizeInit))]
    internal static class RuntimeTestGamePatch
    {
        [HarmonyPostfix]
        private static void Postfix(Game __instance)
        {
            EnsureComponent(__instance);
        }

        internal static void EnsureComponent(Game game)
        {
            if (game == null) return;
            if (game.components == null) game.components = new List<GameComponent>();
            for (int i = game.components.Count - 1; i >= 0; i--)
            {
                GameComponent existing = game.components[i];
                if (existing != null && existing.GetType().FullName == typeof(HorticultureRuntimeTestComponent).FullName
                    && existing.GetType().Assembly != typeof(HorticultureRuntimeTestComponent).Assembly)
                    game.components.RemoveAt(i);
            }
            if (!game.components.Any(component => component is HorticultureRuntimeTestComponent))
                game.components.Add(new HorticultureRuntimeTestComponent(game));
        }
    }

    [HarmonyPatch]
    internal static class RuntimeTestFillComponentsPatch
    {
        private static MethodBase TargetMethod() => AccessTools.Method(typeof(Game), "FillComponents");

        [HarmonyPostfix]
        private static void Postfix(Game __instance) => RuntimeTestGamePatch.EnsureComponent(__instance);
    }

    [HarmonyPatch(typeof(GameComponentUtility), nameof(GameComponentUtility.GameComponentTick))]
    internal static class RuntimeTestComponentTickPatch
    {
        [HarmonyPostfix]
        private static void Postfix() => HorticultureRuntimeTestRunner.Tick();
    }

    [HarmonyPatch(typeof(GameComponentUtility), nameof(GameComponentUtility.StartedNewGame))]
    internal static class RuntimeTestStartedNewGamePatch
    {
        [HarmonyPrefix]
        private static void Prefix() => HorticultureRuntimeTestRunner.TickBeforeAutomaticSuites();
    }

    [HarmonyPatch(typeof(GameComponentUtility), nameof(GameComponentUtility.LoadedGame))]
    internal static class RuntimeTestLoadedGamePatch
    {
        [HarmonyPrefix]
        private static void Prefix() => HorticultureRuntimeTestRunner.TickBeforeAutomaticSuites();
    }

    [HarmonyPatch]
    internal static class RuntimeTestSingleTickPatch
    {
        private static MethodBase TargetMethod() => AccessTools.Method(typeof(TickManager), "DoSingleTick");

        [HarmonyPostfix]
        private static void Postfix() => HorticultureRuntimeTestRunner.Tick();
    }

    [HarmonyPatch(typeof(TickManager), nameof(TickManager.TickManagerUpdate))]
    internal static class RuntimeTestUpdatePatch
    {
        [HarmonyPrefix]
        private static void Prefix() => HorticultureRuntimeTestRunner.Tick();

        [HarmonyPostfix]
        private static void Postfix() => HorticultureRuntimeTestRunner.Tick();
    }

    [HarmonyPatch(typeof(Root_Play), nameof(Root_Play.Update))]
    internal static class RuntimeTestRootUpdatePatch
    {
        private static bool logged;

        [HarmonyPrefix]
        private static void Prefix()
        {
            if (!logged)
            {
                logged = true;
                HorticultureRuntimeTestRunner.Trace("root update prefix");
            }
            HorticultureRuntimeTestRunner.Tick();
        }
    }

    [Serializable]
    public sealed class HorticultureRuntimeTestRequest
    {
        public string schemaVersion = "1";
        public string requestId;
        public string launchId;
        public string scenario = "complete";
        public int warmupTicks = 60;
        public int timeoutTicks = 1200;
        public string resultPath;
        public string horticultureCommit;
        public string horticultureDllSha256;
        public string knowledgeFrameworkDllSha256;
        public string knowledgeFrameworkRelease;
        public int knowledgeFrameworkApiGeneration;
        public string playerLogPath;
        public int playerLogBaselineLines;
    }

    [Serializable]
    public sealed class HorticultureRuntimeTestReport
    {
        public string schemaVersion = "2";
        public string suiteVersion = "1.0";
        public string suite = "Horticulture.RuntimeTests";
        public string requestId;
        public string launchId;
        public string scenario;
        public string status;
        public string horticultureCommit;
        public string horticultureDllSha256;
        public string knowledgeFrameworkRelease;
        public int knowledgeFrameworkApiGeneration;
        public string knowledgeFrameworkDllSha256;
        public string rimWorldVersion;
        public int startTick;
        public int endTick;
        public int elapsedTicks;
        public int assertionCount;
        public int passedAssertions;
        public int failedAssertionsCount;
        public int blockedAssertionsCount;
        public int newRimWorldErrors;
        public int newRimWorldWarnings;
        public string outputPath;
        public List<string> failedAssertions = new List<string>();
        public List<string> exceptionDetails = new List<string>();
        public List<string> relevantDiagnostics = new List<string>();
        public List<string> logFindings = new List<string>();
        [NonSerialized]
        private readonly List<HorticultureRuntimeAssertion> assertionBuffer = new List<HorticultureRuntimeAssertion>();

        internal void AddAssertion(HorticultureRuntimeAssertion assertion)
        {
            assertionBuffer.Add(assertion);
        }

        internal HorticultureRuntimeAssertion[] GetAssertions()
        {
            return assertionBuffer.ToArray();
        }
    }

    [Serializable]
    public sealed class HorticultureRuntimeAssertion
    {
        public string id;
        public string status;
        public string detail;
        public string exception;
    }

    internal sealed class ScenarioExecution
    {
        internal HorticultureRuntimeTestReport Report;
        internal bool AwaitingReload;
    }

    internal static class HorticultureRuntimeTestRunner
    {
        private const int PollIntervalTicks = 15;
        private static string activeRequestId;
        private static HorticultureRuntimeTestRequest request;
        private static int warmupStartTick;
        private static int startTick;
        private static bool executing;
        private static bool awaitingReload;
        private static Game gameBeforeReload;
        private static string completedRequestId;
        private static string requestPath;
        private static bool loggedTick;
        private static bool loggedRequestGate;
        private static bool loggedNotPlayable;
        private static int tickCount;
        private static string lastStateProbe;
        private static FileStream executionLease;

        private static bool TryAcquireExecutionLease()
        {
            if (executionLease != null) return true;
            string bridgeRoot = Environment.GetEnvironmentVariable("DEVBRIDGE_ROOT");
            if (bridgeRoot.NullOrEmpty()) return false;
            try
            {
                string lockPath = Path.Combine(bridgeRoot, "Runtime", "Horticulture.RuntimeTest.execution.lock");
                executionLease = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                Trace("execution lease acquired");
                return true;
            }
            catch (IOException)
            {
                return false;
            }
        }

        private static void ReleaseExecutionLease()
        {
            if (executionLease == null) return;
            executionLease.Dispose();
            executionLease = null;
            Trace("execution lease released");
        }

        internal static void Tick()
        {
            RunTick(false);
        }

        internal static void TickBeforeAutomaticSuites()
        {
            RunTick(true);
        }

        private static void RunTick(bool forceExecution)
        {
            try
            {
                TickCore(forceExecution);
            }
            catch (Exception exception)
            {
                if (request == null || request.requestId.NullOrEmpty() || request.requestId == completedRequestId) return;
                Trace("runner exception: " + exception);
                try
                {
                    HorticultureRuntimeTestReport report = NewReport("FAIL");
                    AddFailure(report, "runner", exception);
                    Complete(report);
                }
                catch (Exception completionException)
                {
                    Trace("runner completion exception: " + completionException);
                    try
                    {
                        string directory = Path.GetDirectoryName(request.resultPath);
                        string temporary = request.resultPath + ".tmp";
                        HorticultureRuntimeTestReport report = new HorticultureRuntimeTestReport
                        {
                            requestId = request.requestId,
                            launchId = request.launchId,
                            scenario = request.scenario,
                            status = "FAIL",
                            outputPath = request.resultPath
                        };
                        AddFailure(report, "runner", exception);
                        Directory.CreateDirectory(directory);
                        File.WriteAllText(temporary, JsonUtility.ToJson(report, true));
                        if (File.Exists(request.resultPath)) File.Delete(request.resultPath);
                        File.Move(temporary, request.resultPath);
                    }
                    catch (Exception fallbackException)
                    {
                        Trace("runner fallback report exception: " + fallbackException);
                    }
                }
            }
        }

        private static void TickCore(bool forceExecution)
        {
            tickCount++;
            ProbeKnowledgeState();
            if (request == null) LoadRequest();
            if (!IsPlayable(out string notPlayableReason))
            {
                if (!loggedNotPlayable)
                {
                    loggedNotPlayable = true;
                    Log.Message("[Horticulture][RuntimeTests] request held: not playable: " + notPlayableReason);
                    Trace("not playable after tick=" + tickCount + ": " + notPlayableReason);
                }
                return;
            }
            if (!loggedTick)
            {
                loggedTick = true;
                Log.Message("[Horticulture][RuntimeTests] runner tick loaded; requestPath="
                    + (Environment.GetEnvironmentVariable("DEVBRIDGE_ROOT") ?? "") + ", playable=True");
                Trace("first playable tick gameTick=" + Find.TickManager.TicksGame);
            }
            if (request != null && tickCount % PollIntervalTicks == 0)
                Trace("tick request=" + request.requestId + " warmupStartTick=" + warmupStartTick
                    + " gameTick=" + Find.TickManager.TicksGame);
            if (request == null && Find.TickManager.TicksGame % PollIntervalTicks != 0) return;
            if (request == null || !LoadRequest()) return;
            if (request == null || request.requestId.NullOrEmpty() || request.requestId == completedRequestId) return;

            bool resumingReload = false;
            if (awaitingReload)
            {
                if (ReferenceEquals(Current.Game, gameBeforeReload)) return;
                awaitingReload = false;
                executing = false;
                warmupStartTick = Find.TickManager.TicksGame;
                resumingReload = true;
            }

            if (!executing)
            {
                if (!forceExecution
                    && Find.TickManager.TicksGame - warmupStartTick < Mathf.Max(1, request.warmupTicks)) return;
                if (!TryAcquireExecutionLease()) return;
                executing = true;
                startTick = Find.TickManager.TicksGame;
            }

            if (Find.TickManager.TicksGame - startTick > Mathf.Max(1, request.timeoutTicks))
            {
                HorticultureRuntimeTestReport timeout = NewReport("BLOCKED");
                timeout.assertionCount = 1;
                timeout.blockedAssertionsCount = 1;
                timeout.AddAssertion(new HorticultureRuntimeAssertion
                {
                    id = "scenario-timeout",
                    status = "BLOCKED",
                    detail = "The scenario exceeded its tick budget before completing."
                });
                Complete(timeout);
                return;
            }

            Game gameBeforeScenario = Current.Game;
            ScenarioExecution execution;
            try
            {
                execution = HorticultureRuntimeScenarioSuite.Execute(request, resumingReload);
            }
            catch (Exception exception)
            {
                HorticultureRuntimeTestReport report = NewReport("FAIL");
                AddFailure(report, "runner", exception);
                execution = new ScenarioExecution { Report = report };
            }

            if (execution == null || execution.Report == null) return;
            if (execution.AwaitingReload)
            {
                awaitingReload = true;
                gameBeforeReload = gameBeforeScenario;
                PersistProgress(execution.Report);
                return;
            }
            Complete(execution.Report);
        }

        private static void ProbeKnowledgeState()
        {
            try
            {
                KnowledgeFrameworkReadinessStatus readiness = KnowledgeConsumerApi.Readiness;
                string state = RuntimeTestProbeUtility.GameIdentity(Current.Game) + "|"
                    + (readiness == null ? "null" : readiness.state + "/" + readiness.reason) + "|"
                    + (HorticultureKnowledgeAdapter.Diagnostics?.state.ToString() ?? "null") + "|"
                    + (HorticultureKnowledgeAdapter.Diagnostics?.phase ?? "null");
                if (state == lastStateProbe) return;
                lastStateProbe = state;
                string message = "probe state game=" + state;
                Trace(message);
                Log.Message("[Horticulture][RuntimeTests] " + message);
            }
            catch (Exception exception)
            {
                Trace("probe state exception: " + exception.Message);
            }
        }

        private static bool LoadRequest()
        {
            string bridgeRoot = Environment.GetEnvironmentVariable("DEVBRIDGE_ROOT");
            if (bridgeRoot.NullOrEmpty())
            {
                Trace("request poll skipped: DEVBRIDGE_ROOT empty");
                return false;
            }
            string candidate = Path.Combine(bridgeRoot, "Runtime", "Horticulture.RuntimeTest.request.json");
            if (!File.Exists(candidate)) return false;
            Trace("request file found");
            if (requestPath == candidate && request != null && !File.GetLastWriteTimeUtc(candidate).Ticks.Equals(0L))
            {
                // The request is atomically replaced by the harness; re-read only on the poll cadence.
            }
            try
            {
            HorticultureRuntimeTestRequest loaded = JsonUtility.FromJson<HorticultureRuntimeTestRequest>(File.ReadAllText(candidate));
            if (loaded == null || loaded.requestId.NullOrEmpty()) return false;
            Trace("request parsed id=" + loaded.requestId + ", launch=" + loaded.launchId);
            string currentLaunchId = Environment.GetEnvironmentVariable("DEVBRIDGE_LAUNCH_ID");
            if (!loggedRequestGate)
            {
                loggedRequestGate = true;
                Log.Message("[Horticulture][RuntimeTests] request observed; requestLaunch=" + loaded.launchId
                    + ", processLaunch=" + currentLaunchId);
            }
            if (loaded.launchId.NullOrEmpty() || currentLaunchId.NullOrEmpty()
                || !string.Equals(loaded.launchId, currentLaunchId, StringComparison.Ordinal))
            {
                Trace("request rejected launch process=" + currentLaunchId);
                return false;
            }
                if (HasTerminalResult(loaded.resultPath))
                {
                    activeRequestId = loaded.requestId;
                    request = loaded;
                    requestPath = candidate;
                    completedRequestId = loaded.requestId;
                    Trace("request already completed; result=" + loaded.resultPath);
                    return false;
                }
                if (activeRequestId != loaded.requestId)
                {
                    activeRequestId = loaded.requestId;
                    request = loaded;
                    requestPath = candidate;
                    warmupStartTick = Find.TickManager.TicksGame;
                    executing = false;
                    awaitingReload = false;
                }
                Trace("request accepted id=" + loaded.requestId);
                return true;
            }
            catch (Exception exception)
            {
                Log.Warning("[Horticulture][RuntimeTests] request read deferred: " + exception.Message);
                return false;
            }
        }

        private static bool HasTerminalResult(string resultPath)
        {
            if (resultPath.NullOrEmpty() || !File.Exists(resultPath)) return false;
            try
            {
                HorticultureRuntimeTestReport report = JsonUtility.FromJson<HorticultureRuntimeTestReport>(File.ReadAllText(resultPath));
                return report != null && (string.Equals(report.status, "PASS", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(report.status, "FAIL", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(report.status, "BLOCKED", StringComparison.OrdinalIgnoreCase));
            }
            catch
            {
                return false;
            }
        }

        private static bool IsPlayable(out string reason)
        {
            if (Current.Game == null) { reason = "Current.Game=null"; return false; }
            if (Find.CurrentMap == null) { reason = "Find.CurrentMap=null"; return false; }
            if (Find.TickManager == null) { reason = "Find.TickManager=null"; return false; }
            reason = null;
            return true;
        }

        private static HorticultureRuntimeTestReport NewReport(string status)
        {
            HorticultureKnowledgeDiagnosticSnapshot diagnostics = HorticultureKnowledgeAdapter.Diagnostics;
            HorticultureRuntimeTestReport report = new HorticultureRuntimeTestReport
            {
                requestId = request.requestId,
                launchId = request.launchId,
                scenario = request.scenario,
                status = status,
                horticultureCommit = request.horticultureCommit,
                horticultureDllSha256 = request.horticultureDllSha256,
                knowledgeFrameworkDllSha256 = request.knowledgeFrameworkDllSha256,
                knowledgeFrameworkRelease = diagnostics?.frameworkRelease ?? request.knowledgeFrameworkRelease,
                knowledgeFrameworkApiGeneration = diagnostics?.frameworkApiVersion ?? request.knowledgeFrameworkApiGeneration,
                rimWorldVersion = VersionControl.CurrentVersionString,
                startTick = startTick,
                endTick = Find.TickManager?.TicksGame ?? startTick
            };
            report.relevantDiagnostics.Add(diagnostics?.ToString() ?? "Knowledge diagnostics unavailable.");
            return report;
        }

        private static void AddFailure(HorticultureRuntimeTestReport report, string id, Exception exception)
        {
            report.status = "FAIL";
            report.assertionCount++;
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

        private static void Complete(HorticultureRuntimeTestReport report)
        {
            if (report == null || request == null || request.requestId.NullOrEmpty()
                || request.requestId == completedRequestId) return;
            completedRequestId = request.requestId;
            report.endTick = Find.TickManager?.TicksGame ?? report.endTick;
            report.elapsedTicks = Mathf.Max(0, report.endTick - report.startTick);
            CollectLogFindings(report);
            if (report.failedAssertionsCount > 0 || report.newRimWorldErrors > 0 || report.newRimWorldWarnings > 0) report.status = "FAIL";
            else if (report.blockedAssertionsCount > 0) report.status = "BLOCKED";
            else report.status = "PASS";
            Persist(report);
            executing = false;
            ReleaseExecutionLease();
            Log.Message("[Horticulture][RuntimeTests] " + report.status + " scenario=" + report.scenario
                + " assertions=" + report.assertionCount + " failed=" + report.failedAssertionsCount
                + " blocked=" + report.blockedAssertionsCount + " errors=" + report.newRimWorldErrors);
        }

        private static void PersistProgress(HorticultureRuntimeTestReport report)
        {
            report.status = "RUNNING";
            report.outputPath = request.resultPath;
            WriteJson(report);
        }

        private static void Persist(HorticultureRuntimeTestReport report)
        {
            report.outputPath = request.resultPath;
            WriteJson(report);
        }

        private static void WriteJson(HorticultureRuntimeTestReport report)
        {
            if (request.resultPath.NullOrEmpty())
            {
                Log.Warning("[Horticulture][RuntimeTests] request did not provide a result path.");
                return;
            }
            try
            {
                string directory = Path.GetDirectoryName(request.resultPath);
                if (directory.NullOrEmpty()) return;
                Directory.CreateDirectory(directory);
                string temporary = request.resultPath + ".tmp";
                File.WriteAllText(temporary, SerializeReport(report));
                if (File.Exists(request.resultPath)) File.Delete(request.resultPath);
                File.Move(temporary, request.resultPath);
                Trace("result written status=" + report.status);
            }
            catch (Exception exception)
            {
                Log.Error("[Horticulture][RuntimeTests] result write failed: " + exception);
                Trace("result write failed: " + exception.Message);
            }
        }

        private static string SerializeReport(HorticultureRuntimeTestReport report)
        {
            string json = JsonUtility.ToJson(report, true);
            int closingBrace = json.LastIndexOf('}');
            if (closingBrace < 0) return json;

            StringBuilder output = new StringBuilder(json.Substring(0, closingBrace).TrimEnd());
            if (output.Length > 0 && output[output.Length - 1] != '{') output.Append(',');
            output.Append("\n  \"assertions\": [");
            HorticultureRuntimeAssertion[] assertions = report.GetAssertions();
            for (int index = 0; index < assertions.Length; index++)
            {
                HorticultureRuntimeAssertion assertion = assertions[index];
                if (index > 0) output.Append(',');
                output.Append("\n    {\n      \"id\": ").Append(JsonQuote(assertion.id));
                output.Append(",\n      \"status\": ").Append(JsonQuote(assertion.status));
                output.Append(",\n      \"detail\": ").Append(JsonQuote(assertion.detail));
                output.Append(",\n      \"exception\": ").Append(JsonQuote(assertion.exception));
                output.Append("\n    }");
            }
            output.Append("\n  ]\n}");
            return output.ToString();
        }

        private static string JsonQuote(string value)
        {
            if (value == null) return "null";
            StringBuilder output = new StringBuilder(value.Length + 2);
            output.Append('"');
            foreach (char character in value)
            {
                switch (character)
                {
                    case '\\': output.Append("\\\\"); break;
                    case '"': output.Append("\\\""); break;
                    case '\b': output.Append("\\b"); break;
                    case '\f': output.Append("\\f"); break;
                    case '\n': output.Append("\\n"); break;
                    case '\r': output.Append("\\r"); break;
                    case '\t': output.Append("\\t"); break;
                    default:
                        if (character < ' ')
                            output.Append("\\u").Append(((int)character).ToString("x4"));
                        else
                            output.Append(character);
                        break;
                }
            }
            return output.Append('"').ToString();
        }

        internal static void Trace(string message)
        {
            try
            {
                string bridgeRoot = Environment.GetEnvironmentVariable("DEVBRIDGE_ROOT");
                if (bridgeRoot.NullOrEmpty()) return;
                string path = Path.Combine(bridgeRoot, "Runtime", "Horticulture.RuntimeTest.trace.log");
                File.AppendAllText(path, DateTime.UtcNow.ToString("O") + " " + message + Environment.NewLine);
            }
            catch
            {
            }
        }

        private static void CollectLogFindings(HorticultureRuntimeTestReport report)
        {
            if (request.playerLogPath.NullOrEmpty() || !File.Exists(request.playerLogPath)) return;
            try
            {
                string[] lines;
                using (FileStream stream = new FileStream(request.playerLogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (StreamReader reader = new StreamReader(stream))
                {
                    List<string> readLines = new List<string>();
                    while (!reader.EndOfStream) readLines.Add(reader.ReadLine());
                    lines = readLines.ToArray();
                }
                int start = lines.Length < request.playerLogBaselineLines ? 0 : Mathf.Clamp(request.playerLogBaselineLines, 0, lines.Length);
                foreach (string line in lines.Skip(start).Where(value => value.IndexOf("[Horticulture]", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    bool testLine = line.IndexOf("[RuntimeTests]", StringComparison.OrdinalIgnoreCase) >= 0;
                    bool error = !testLine && (line.IndexOf("Exception", StringComparison.OrdinalIgnoreCase) >= 0
                        || line.IndexOf("Error", StringComparison.OrdinalIgnoreCase) >= 0
                        || line.IndexOf("missing method", StringComparison.OrdinalIgnoreCase) >= 0
                        || line.IndexOf("type load", StringComparison.OrdinalIgnoreCase) >= 0
                        || line.IndexOf("serialization", StringComparison.OrdinalIgnoreCase) >= 0
                        || line.IndexOf("patch failed", StringComparison.OrdinalIgnoreCase) >= 0);
                    bool warning = line.IndexOf("Warning", StringComparison.OrdinalIgnoreCase) >= 0
                        || line.IndexOf("incompatible", StringComparison.OrdinalIgnoreCase) >= 0;
                    if (error) report.newRimWorldErrors++;
                    else if (warning) report.newRimWorldWarnings++;
                    if (error || warning) report.logFindings.Add(line);
                }
            }
            catch (Exception exception)
            {
                report.logFindings.Add("log inspection failed: " + exception);
            }
        }
    }
}
