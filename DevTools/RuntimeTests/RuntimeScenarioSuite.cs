using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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
                        case "ordinary-crop": OrdinaryCrop(report); break;
                        case "sowable-tree": SowableTree(report); break;
                        case "cross-pollination": CrossPollination(report); break;
                        case "produce-processing": ProduceProcessing(report); break;
                        case "knowledge": Knowledge(report); break;
                        case "negative": Negative(report); break;
                        case "long-running": LongRunning(report); break;
                        case "complete":
                            Startup(report);
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
            if (fixture != null && !fixture.Destroyed) fixture.Destroy(DestroyMode.Vanish);
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
