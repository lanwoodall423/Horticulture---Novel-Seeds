using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace HorticultureNovelSeeds
{
    /// <summary>Runs the Horticulture-owned quicktest suite after a playable map is available.</summary>
    public sealed class HorticultureInGameTestComponent : GameComponent
    {
        private const int SettleTicks = 60;
        public HorticultureInGameTestComponent() { }

        public HorticultureInGameTestComponent(Game game) { }

        public override void GameComponentTick()
        {
            HorticultureInGameTestRunner.Tick(SettleTicks);
        }
    }

    [HarmonyLib.HarmonyPatch(typeof(GameComponentUtility), nameof(GameComponentUtility.GameComponentTick))]
    internal static class HorticultureInGameTestTickPatch
    {
        [HarmonyLib.HarmonyPostfix]
        private static void Postfix()
        {
            HorticultureInGameTestRunner.Tick(60);
        }
    }

    [HarmonyLib.HarmonyPatch(typeof(Game), nameof(Game.FinalizeInit))]
    internal static class HorticultureInGameTestGamePatch
    {
        [HarmonyLib.HarmonyPostfix]
        private static void Postfix(Game __instance)
        {
            HorticultureInGameTestRunner.EnsureComponent(__instance);
        }
    }

    internal static class HorticultureInGameTestRunner
    {
        private const string ResultFileName = "Horticulture-InGameTests.json";
        private static int playableTicks;
        private static bool completed;
        private static bool loggedTick;

        internal static bool Requested
        {
            get
            {
                if (IsTrue(Environment.GetEnvironmentVariable("HNS_IN_GAME_TESTS"))) return true;
                return !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DEVBRIDGE_LAUNCH_ID"));
            }
        }

        internal static bool IsPlayable()
        {
            try
            {
                return GenScene.InPlayScene && Current.Game != null && Find.CurrentMap != null &&
                    Find.TickManager != null && Find.CurrentMap.listerThings != null;
            }
            catch
            {
                return false;
            }
        }

        internal static void Tick(int settleTicks)
        {
            if (!loggedTick)
            {
                loggedTick = true;
                Log.Message("[Horticulture][InGameTests] runner tick loaded; requested=" + Requested + ", playable=" + IsPlayable());
            }
            if (completed || !Requested || !IsPlayable()) return;
            if (++playableTicks < settleTicks) return;
            completed = true;
            Execute();
        }

        internal static void EnsureComponent(Game game)
        {
            if (!Requested || game?.components == null ||
                game.components.Any(component => component is HorticultureInGameTestComponent)) return;
            game.components.Add(new HorticultureInGameTestComponent(game));
        }

        internal static void Execute()
        {
            HorticultureInGameTestReport report;
            try
            {
                report = Run();
            }
            catch (Exception exception)
            {
                report = NewReport();
                report.results.Add(HorticultureInGameTestResult.Failed("runner", exception));
            }

            Persist(report);
            LogReport(report);
        }

        private static HorticultureInGameTestReport Run()
        {
            HorticultureInGameTestReport report = NewReport();
            Check(report, "playable-map", () =>
            {
                Require(GenScene.InPlayScene, "RimWorld is not in a play scene");
                Require(Current.Game != null, "Current.Game is null");
                Require(Find.CurrentMap != null, "Find.CurrentMap is null");
                Require(Find.CurrentMap.uniqueID >= 0, "current map has no stable unique ID");
                Require(Find.TickManager != null, "Find.TickManager is null");
                return "map=" + Find.CurrentMap.uniqueID + " tick=" + Find.TickManager.TicksGame;
            });

            Check(report, "game-component-health", () =>
            {
                GameComponent_NovelSeeds component = GameComponent_NovelSeeds.Instance;
                Require(component != null, "GameComponent_NovelSeeds is unavailable");
                List<VarietyRecord> varieties = (component.AllVarieties ?? Enumerable.Empty<VarietyRecord>()).ToList();
                Require(varieties.All(value => value != null && !value.id.NullOrEmpty()),
                    "the visible variety registry contains a null or ID-less record");
                Require(varieties.Select(value => value.id).Distinct(StringComparer.Ordinal).Count() == varieties.Count,
                    "the visible variety registry contains duplicate IDs");
                Require(varieties.All(value => value.cropDef != null && HorticulturePlantPolicy.IsSupported(value.cropDef)),
                    "the visible variety registry contains an unsupported crop");
                return "visibleVarieties=" + varieties.Count + " palettes=" + component.SpeciesColorPalettes.Count;
            });

            Check(report, "plant-policy-defs", () =>
            {
                List<ThingDef> plantDefs = DefDatabase<ThingDef>.AllDefsListForReading
                    .Where(value => value?.plant != null).ToList();
                List<ThingDef> supported = plantDefs.Where(HorticulturePlantPolicy.IsSupported).ToList();
                Require(plantDefs.Count > 0, "no plant definitions were loaded");
                Require(supported.Count > 0, "no sowable plant definitions are supported");
                foreach (ThingDef plant in supported)
                {
                    Require(NovelSeedUtility.IsGrowableCrop(plant), "supported plant is not a growable crop: " + plant.defName);
                    Require(HorticulturePlantPolicy.RejectionReason(plant).NullOrEmpty(),
                        "supported plant has a rejection reason: " + plant.defName);
                }
                foreach (ThingDef plant in plantDefs.Where(value => !HorticulturePlantPolicy.IsSupported(value)))
                    Require(!HorticulturePlantPolicy.RejectionReason(plant).NullOrEmpty(),
                        "unsupported plant has no rejection reason: " + plant.defName);
                return "plantDefs=" + plantDefs.Count + " supported=" + supported.Count +
                    " trees=" + supported.Count(HorticulturePlantPolicy.IsSowableTree);
            });

            Check(report, "trait-definitions", () =>
            {
                List<VarietyTraitDef> traits = DefDatabase<VarietyTraitDef>.AllDefsListForReading
                    .Where(value => value != null).ToList();
                Require(traits.Count > 0, "no Novel Seeds trait definitions were loaded");
                HashSet<string> names = new HashSet<string>(StringComparer.Ordinal);
                foreach (VarietyTraitDef trait in traits)
                {
                    Require(!trait.defName.NullOrEmpty(), "a trait definition has no defName");
                    Require(names.Add(trait.defName), "duplicate trait definition: " + trait.defName);
                    float balance = NovelSeedUtility.TraitBalanceValue(trait);
                    Require(IsFinite(balance), "trait balance is not finite: " + trait.defName);
                }
                string firstKey = NovelSeedUtility.TraitKey(traits);
                Require(firstKey == NovelSeedUtility.TraitKey(traits.AsEnumerable().Reverse()),
                    "trait key depends on input order");
                return "traits=" + traits.Count + " balance=" + NovelSeedUtility.TraitBalanceScore(traits).ToString("0.###", CultureInfo.InvariantCulture);
            });

            Check(report, "palette-coverage", () =>
            {
                GameComponent_NovelSeeds component = GameComponent_NovelSeeds.Instance;
                Require(component != null, "GameComponent_NovelSeeds is unavailable");
                List<SpeciesColorPaletteRecord> palettes = (component.SpeciesColorPalettes ?? new List<SpeciesColorPaletteRecord>()).ToList();
                Dictionary<string, SpeciesColorPaletteRecord> byPlant = palettes
                    .Where(value => value != null && !value.plantDefName.NullOrEmpty())
                    .GroupBy(value => value.plantDefName, StringComparer.Ordinal)
                    .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
                Require(byPlant.Count == palettes.Count, "palette records contain null, missing, or duplicate plant IDs");
                foreach (SpeciesColorPaletteRecord palette in palettes)
                {
                    Require(palette.PlantDef != null && HorticulturePlantPolicy.IsSupported(palette.PlantDef),
                        "palette points to an unsupported or missing plant: " + palette.plantDefName);
                    Require(palette.packedColors != null && palette.packedColors.Count > 0,
                        "palette has no colors: " + palette.plantDefName);
                }
                int supportedCount = DefDatabase<ThingDef>.AllDefsListForReading.Count(HorticulturePlantPolicy.IsSupported);
                Require(DefDatabase<ThingDef>.AllDefsListForReading.Where(HorticulturePlantPolicy.IsSupported)
                    .All(value => byPlant.ContainsKey(value.defName)), "a supported plant has no color palette");
                return "palettes=" + palettes.Count + " supportedPlants=" + supportedCount;
            });

            Check(report, "breeding-mix-determinism", () =>
            {
                List<VarietyRecord> source = new List<VarietyRecord>
                {
                    new VarietyRecord { id = "hns-b" },
                    new VarietyRecord { id = "hns-a" },
                    new VarietyRecord { id = "hns-a" }
                };
                List<VarietyRecord> ordered = GameComponent_NovelSeeds.OrderBreedingMixVarieties(source);
                Require(ordered.Count == 2 && ordered[0].id == "hns-a" && ordered[1].id == "hns-b",
                    "breeding mix ordering or duplicate filtering is incorrect");
                VarietyRecord first = GameComponent_NovelSeeds.SelectBreedingMixVariety(ordered, new IntVec3(17, 0, 23));
                VarietyRecord second = GameComponent_NovelSeeds.SelectBreedingMixVariety(ordered, new IntVec3(17, 0, 23));
                Require(first?.id == second?.id, "breeding mix selection is not deterministic");
                Require(GameComponent_NovelSeeds.SelectBreedingMixVariety(null, new IntVec3(0, 0, 0)) == null,
                    "empty breeding mix did not return null");
                return "ordered=" + string.Join(",", ordered.Select(value => value.id).ToArray()) + " selected=" + (first?.id ?? "none");
            });

            Check(report, "knowledge-event-identity", () =>
            {
                string first = HorticultureKnowledgeEventIdentity.Normalize("in-game-test", "semantic-action");
                string same = HorticultureKnowledgeEventIdentity.Normalize("in-game-test", "semantic-action");
                string different = HorticultureKnowledgeEventIdentity.Normalize("in-game-test", "semantic-action-2");
                Require(first == same && first != different, "semantic event identities are not stable or distinct");
                Require(first.Length <= 180 && different.Length <= 180, "semantic event identity exceeded its bound");
                Require(first.IndexOf("tick", StringComparison.OrdinalIgnoreCase) < 0,
                    "semantic event identity contains a tick-based component");
                return "identity=" + first;
            });

            Check(report, "knowledge-registration", () =>
            {
                HorticultureKnowledgeDiagnosticSnapshot diagnostics = HorticultureKnowledgeAdapter.Diagnostics;
                Require(HorticultureKnowledgeAdapter.RegistrationState == HorticultureKnowledgeRegistrationState.Registered,
                    diagnostics?.ToString() ?? "Knowledge Framework registration is unavailable");
                Require(diagnostics != null && diagnostics.IsUsable, "Knowledge Framework diagnostics are not usable");
                Require(HorticultureKnowledgeAdapter.DomainId == HorticultureKnowledgeContract.DomainId,
                    "Horticulture Knowledge domain ID changed");
                return "state=" + HorticultureKnowledgeAdapter.RegistrationState + " domain=" + HorticultureKnowledgeAdapter.DomainId;
            });

            Check(report, "knowledge-diagnostics", () =>
            {
                HorticultureKnowledgeEventDiagnosticsSnapshot diagnostics = HorticultureKnowledgeAdapter.EventDiagnostics;
                Require(diagnostics != null && diagnostics.submittedByEvent != null && diagnostics.deduplicatedByEvent != null,
                    "Horticulture Knowledge event diagnostics are unavailable");
                Require(diagnostics.rejectedUnsupportedPlants >= 0 && diagnostics.targetedInvalidations >= 0 &&
                    diagnostics.broadInvalidations >= 0 && diagnostics.speciesSubjectCount >= 0 && diagnostics.cultivarSubjectCount >= 0,
                    "Horticulture Knowledge event diagnostics contain a negative counter");
                return "subjects=" + diagnostics.speciesSubjectCount + "+" + diagnostics.cultivarSubjectCount +
                    " targetedInvalidations=" + diagnostics.targetedInvalidations;
            });

            Check(report, "live-plant-components", () =>
            {
                Map map = Find.CurrentMap;
                List<Plant> plants = map.listerThings.ThingsInGroup(ThingRequestGroup.Plant).OfType<Plant>()
                    .Where(value => HorticulturePlantPolicy.IsSupported(value.def)).ToList();
                int novelPlants = 0;
                foreach (Plant plant in plants)
                {
                    CompPlantVariety component = plant.TryGetComp<CompPlantVariety>();
                    if (component == null) continue;
                    novelPlants++;
                    Require(component.ActiveTraits != null && component.ActiveTraits.All(value => value != null),
                        "a Novel Seeds plant has a null active trait");
                    if (component.HasAnyTraits && !component.PendingDiscovery)
                        Require(component.Variety != null, "a committed Novel Seeds plant has no registry variety");
                }
                return "supportedPlants=" + plants.Count + " novelComponents=" + novelPlants;
            });

            return report;
        }

        private static HorticultureInGameTestReport NewReport()
        {
            return new HorticultureInGameTestReport
            {
                launchId = Environment.GetEnvironmentVariable("DEVBRIDGE_LAUNCH_ID"),
                generation = ParseGeneration(),
                completedUtc = DateTime.UtcNow,
                gameTick = Find.TickManager?.TicksGame ?? 0
            };
        }

        private static void Check(HorticultureInGameTestReport report, string id, Func<string> action)
        {
            try
            {
                report.results.Add(HorticultureInGameTestResult.Passed(id, action() ?? "ok"));
            }
            catch (Exception exception)
            {
                report.results.Add(HorticultureInGameTestResult.Failed(id, exception));
                Log.Error("[Horticulture][InGameTests] FAIL " + id + ": " + exception);
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private static bool IsTrue(string value)
        {
            return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);
        }

        private static int ParseGeneration()
        {
            return int.TryParse(Environment.GetEnvironmentVariable("DEVBRIDGE_GENERATION"), out int value) ? value : 0;
        }

        private static void Persist(HorticultureInGameTestReport report)
        {
            foreach (string path in OutputPaths().Distinct(StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    string directory = Path.GetDirectoryName(path);
                    if (!directory.NullOrEmpty()) Directory.CreateDirectory(directory);
                    report.outputPath = path;
                    WriteAtomic(path, BuildJson(report));
                    return;
                }
                catch (Exception exception)
                {
                    Log.Warning("[Horticulture][InGameTests] Could not write " + path + ": " + exception.Message);
                }
            }
            Log.Error("[Horticulture][InGameTests] No writable test-result path was available.");
        }

        private static IEnumerable<string> OutputPaths()
        {
            string configured = Environment.GetEnvironmentVariable("HNS_TEST_RESULTS");
            if (!configured.NullOrEmpty()) yield return Path.GetFullPath(configured);

            string assemblyLocation = typeof(HorticultureInGameTestComponent).Assembly.Location;
            if (!assemblyLocation.NullOrEmpty())
            {
                string assemblyDirectory = Path.GetDirectoryName(assemblyLocation);
                if (!assemblyDirectory.NullOrEmpty())
                    yield return Path.GetFullPath(Path.Combine(assemblyDirectory, "..", "..", "DevTools", "TestResults", ResultFileName));
            }

            string bridgeRoot = Environment.GetEnvironmentVariable("DEVBRIDGE_ROOT");
            if (!bridgeRoot.NullOrEmpty())
                yield return Path.Combine(bridgeRoot, "Runtime", ResultFileName);

            if (!Application.persistentDataPath.NullOrEmpty())
                yield return Path.Combine(Application.persistentDataPath, "HorticultureNovelSeeds", "TestResults", ResultFileName);
        }

        private static void WriteAtomic(string path, string contents)
        {
            string temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                File.WriteAllText(temporary, contents, new UTF8Encoding(false));
                if (File.Exists(path))
                {
                    try { File.Replace(temporary, path, null); }
                    catch
                    {
                        File.Delete(path);
                        File.Move(temporary, path);
                    }
                }
                else File.Move(temporary, path);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }

        private static string BuildJson(HorticultureInGameTestReport report)
        {
            int passed = report.results.Count(value => value.status == "pass");
            int failed = report.results.Count(value => value.status == "fail");
            int warnings = report.results.Count(value => value.status == "warn");
            StringBuilder builder = new StringBuilder();
            builder.Append("{\n")
                .Append("  \"schemaVersion\": 1,\n")
                .Append("  \"suite\": \"horticulture-owned-in-game\",\n")
                .Append("  \"launchId\": ").Append(Quote(report.launchId)).Append(",\n")
                .Append("  \"generation\": ").Append(report.generation.ToString(CultureInfo.InvariantCulture)).Append(",\n")
                .Append("  \"completedUtc\": ").Append(Quote(report.completedUtc.ToString("O", CultureInfo.InvariantCulture))).Append(",\n")
                .Append("  \"gameTick\": ").Append(report.gameTick.ToString(CultureInfo.InvariantCulture)).Append(",\n")
                .Append("  \"passed\": ").Append(failed == 0 ? "true" : "false").Append(",\n")
                .Append("  \"passedCount\": ").Append(passed.ToString(CultureInfo.InvariantCulture)).Append(",\n")
                .Append("  \"warningCount\": ").Append(warnings.ToString(CultureInfo.InvariantCulture)).Append(",\n")
                .Append("  \"failedCount\": ").Append(failed.ToString(CultureInfo.InvariantCulture)).Append(",\n")
                .Append("  \"outputPath\": ").Append(Quote(report.outputPath)).Append(",\n")
                .Append("  \"results\": [\n");
            for (int i = 0; i < report.results.Count; i++)
            {
                HorticultureInGameTestResult result = report.results[i];
                builder.Append("    {\"id\": ").Append(Quote(result.id))
                    .Append(", \"status\": ").Append(Quote(result.status))
                    .Append(", \"detail\": ").Append(Quote(result.detail)).Append('}');
                if (i + 1 < report.results.Count) builder.Append(',');
                builder.AppendLine();
            }
            return builder.Append("  ]\n}\n").ToString();
        }

        private static string Quote(string value)
        {
            if (value == null) return "null";
            StringBuilder builder = new StringBuilder(value.Length + 2).Append('"');
            foreach (char character in value)
            {
                switch (character)
                {
                    case '\\': builder.Append("\\\\"); break;
                    case '"': builder.Append("\\\""); break;
                    case '\r': builder.Append("\\r"); break;
                    case '\n': builder.Append("\\n"); break;
                    case '\t': builder.Append("\\t"); break;
                    default:
                        if (character < 32) builder.Append("\\u").Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                        else builder.Append(character);
                        break;
                }
            }
            return builder.Append('"').ToString();
        }

        private static void LogReport(HorticultureInGameTestReport report)
        {
            int passed = report.results.Count(value => value.status == "pass");
            int failed = report.results.Count(value => value.status == "fail");
            int warnings = report.results.Count(value => value.status == "warn");
            Log.Message("[Horticulture][InGameTests] " + (failed == 0 ? "PASS" : "FAIL") +
                " passed=" + passed + " warnings=" + warnings + " failed=" + failed +
                " generation=" + report.generation + " output=" + (report.outputPath ?? "unavailable"));
        }
    }

    internal sealed class HorticultureInGameTestReport
    {
        internal string launchId;
        internal int generation;
        internal DateTime completedUtc;
        internal int gameTick;
        internal string outputPath;
        internal readonly List<HorticultureInGameTestResult> results = new List<HorticultureInGameTestResult>();
    }

    internal sealed class HorticultureInGameTestResult
    {
        internal string id;
        internal string status;
        internal string detail;

        internal static HorticultureInGameTestResult Passed(string id, string detail) => new HorticultureInGameTestResult
        {
            id = id,
            status = "pass",
            detail = detail
        };

        internal static HorticultureInGameTestResult Failed(string id, Exception exception) => new HorticultureInGameTestResult
        {
            id = id,
            status = "fail",
            detail = exception?.ToString() ?? "unknown failure"
        };
    }
}
