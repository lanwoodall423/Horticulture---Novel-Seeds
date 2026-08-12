using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using HarmonyLib;
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
                        case "registry-scale": RegistryScale(report); break;
                        case "rc-performance": RcPerformance(report); break;
                        case "auto-mask-suite": AutoMaskSuite(report); break;
                        case "auto-mask-export": AutoMaskExport(report, request); break;
                        case "complete":
                            Startup(report);
                            CleanDefault(report);
                            UxDiscovery(report);
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
    }
}
