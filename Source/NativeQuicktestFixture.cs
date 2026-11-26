#if HNS_NATIVE_FIXTURE
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Security.Cryptography;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace HorticultureNovelSeeds
{
    // Compiled only in the test candidate; requires unique temp root, token, and exact loadout.
    public sealed class NativeQuicktestFixture : GameComponent
    {
        private static string run, root, expectedPackages, expectedDll;
        private static string version;
        private static string savedId;
        private static int savedHarvests;
        private static string saveName;
        private static int savedYield;
        private static FieldInfo boost;
        private static bool oldBoost, boostSet;
        private bool active;
        private int nextTick, deadline;
        private int stage;
        private Plant crop;
        private Pawn farmer;
        private ThingDef cropDef;
        private VarietyTraitDef trait;
        private int baseline, harvestBefore;
        private string Prefix => "[HNS NativeFixture] " + run + " " + version + " ";
        public NativeQuicktestFixture(Game game) { }

        public override void StartedNewGame()
        {
            string token = Environment.GetEnvironmentVariable("HNS_NATIVE_RUN");
            if (!Guid.TryParseExact(token, "N", out _)) return;
            string expectedRoot = Path.Combine(Path.GetTempPath(), "hns-native-" + token);
            if (!Environment.GetCommandLineArgs().Any(a => a.Equals("-quicktest", StringComparison.OrdinalIgnoreCase)) ||
                !Environment.GetCommandLineArgs().Any(a => a.Equals("-savedatafolder=" + expectedRoot, StringComparison.OrdinalIgnoreCase))) return;
            run = token; root = expectedRoot;
            expectedPackages = Environment.GetEnvironmentVariable("HNS_NATIVE_PACKAGES");
            expectedDll = Environment.GetEnvironmentVariable("HNS_NATIVE_DLL");
            version = typeof(NativeQuicktestFixture).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            bool architectureGarden = Environment.GetEnvironmentVariable("HNS_NATIVE_ARCHITECTURE_GARDEN") == "1";
            Environment.SetEnvironmentVariable("HNS_NATIVE_ARCHITECTURE_GARDEN", null);
            bool detailGarden = Environment.GetEnvironmentVariable("HNS_NATIVE_DETAIL_GARDEN") == "1";
            Environment.SetEnvironmentVariable("HNS_NATIVE_DETAIL_GARDEN", null);
            bool surfaceGarden = Environment.GetEnvironmentVariable("HNS_NATIVE_SURFACE_GARDEN") == "1";
            Environment.SetEnvironmentVariable("HNS_NATIVE_SURFACE_GARDEN", null);
            bool windGarden = Environment.GetEnvironmentVariable("HNS_NATIVE_WIND_GARDEN") == "1";
            Environment.SetEnvironmentVariable("HNS_NATIVE_WIND_GARDEN", null);
            bool developmentGarden = Environment.GetEnvironmentVariable("HNS_NATIVE_DEVELOPMENT_GARDEN") == "1";
            Environment.SetEnvironmentVariable("HNS_NATIVE_DEVELOPMENT_GARDEN", null);
            bool organGarden = Environment.GetEnvironmentVariable("HNS_NATIVE_ORGAN_GARDEN") == "1";
            Environment.SetEnvironmentVariable("HNS_NATIVE_ORGAN_GARDEN", null);
            bool formGarden = Environment.GetEnvironmentVariable("HNS_NATIVE_FORM_GARDEN") == "1";
            Environment.SetEnvironmentVariable("HNS_NATIVE_FORM_GARDEN", null);
            bool cultivarGarden = Environment.GetEnvironmentVariable("HNS_NATIVE_CULTIVAR_GARDEN") == "1";
            Environment.SetEnvironmentVariable("HNS_NATIVE_CULTIVAR_GARDEN", null);
            bool creativeGarden = Environment.GetEnvironmentVariable("HNS_NATIVE_CREATIVE_GARDEN") == "1";
            Environment.SetEnvironmentVariable("HNS_NATIVE_CREATIVE_GARDEN", null);
            bool catalogGarden = Environment.GetEnvironmentVariable("HNS_NATIVE_CATALOG_GARDEN") == "1";
            Environment.SetEnvironmentVariable("HNS_NATIVE_CATALOG_GARDEN", null);
            bool proceduralGarden = Environment.GetEnvironmentVariable("HNS_NATIVE_PATTERN_GARDEN") == "1";
            Environment.SetEnvironmentVariable("HNS_NATIVE_PATTERN_GARDEN", null);
            bool gardenProbe = Environment.GetEnvironmentVariable("HNS_NATIVE_GARDEN_PROBE") == "1";
            Environment.SetEnvironmentVariable("HNS_NATIVE_GARDEN_PROBE", null);
            bool shaderProbe = Environment.GetEnvironmentVariable("HNS_NATIVE_SHADER_PROBE") == "1";
            Environment.SetEnvironmentVariable("HNS_NATIVE_SHADER_PROBE", null);
            Environment.SetEnvironmentVariable("HNS_NATIVE_RUN", null);
            LongEventHandler.ExecuteWhenFinished(() =>
            {
                try
                {
                    Require(Isolated(), "ACTUAL_ISOLATION_NOT_PROVEN");
                    string dll = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(NativeQuicktestFixture).Assembly.Location)));
                    Require(dll == expectedDll, "loaded Horticulture hash");
                    Require(!typeof(NativeQuicktestFixture).Assembly.GetReferencedAssemblies().Any(a =>
                        a.Name.Contains("KnowledgeFramework", StringComparison.OrdinalIgnoreCase) ||
                        a.Name.Contains("InsightCanvas", StringComparison.OrdinalIgnoreCase) ||
                        a.Name.Contains("ProgressionAgriculture", StringComparison.OrdinalIgnoreCase)), "no framework AssemblyRef");
                    if (expectedPackages.Contains("vr.missilegirl"))
                    {
                        var pack = LoadedModManager.RunningModsListForReading.Single(m => m.PackageIdPlayerFacing.Equals("vr.missilegirl", StringComparison.OrdinalIgnoreCase));
                        var asm = pack.assemblies.loadedAssemblies.Single(a => Path.GetFileName(a.Location).Equals("Cosmodrome.dll", StringComparison.OrdinalIgnoreCase));
                        string hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(asm.Location)));
                        Require(hash == "1E9DDB56998F7B963962095944AF3AEABBC90826E55BD53CB8083D3CC7E4B0B3", "Missile Girl assembly identity");
                        Require(Harmony.GetAllPatchedMethods().Any(m => Harmony.GetPatchInfo(m)?.Owners.Any(o => o.StartsWith("VR.MissileGirl", StringComparison.OrdinalIgnoreCase)) == true), "Missile Girl patches");
                    }
                    if (gardenProbe)
                    {
                        Stage("Startup");
                        NativeGardenProbe.Begin(root, Stage, Finish, proceduralGarden, catalogGarden, creativeGarden, cultivarGarden, formGarden, organGarden, developmentGarden, windGarden, surfaceGarden, detailGarden, architectureGarden);
                        return;
                    }
                    if (shaderProbe)
                    {
                        Stage("Startup");
                        NativeShaderProbe.Begin(root, Stage, Finish);
                        return;
                    }
                    boost = typeof(TickManager).GetField("UltraSpeedBoost", BindingFlags.Static | BindingFlags.NonPublic);
                    Require(boost?.FieldType == typeof(bool), "UltraBoost available");
                    oldBoost = (bool)boost.GetValue(null); boost.SetValue(null, true); boostSet = true;
                    Find.TickManager.CurTimeSpeed = TimeSpeed.Ultrafast;
                    nextTick = Find.TickManager.TicksGame + 60; active = true;
                    Log.Message(Prefix + "READY packages=" + expectedPackages + "; dll=" + dll + "; speed=UltraBoost");
                    Stage("Startup");
                }
                catch (Exception e) { Finish("FAIL", e.ToString()); }
            });
        }
        public override void LoadedGame()
        {
            if (string.IsNullOrEmpty(run) || string.IsNullOrEmpty(savedId)) return;
            LongEventHandler.ExecuteWhenFinished(() =>
            {
                try
                {
                    Require(Isolated(), "reload isolation");
                    var v = GameComponent_NovelSeeds.Instance?.GetVariety(savedId);
                    Require(v?.customName == "Native fixture cultivar" && v.traits.Any(t => t.defName == "HNS_Giant"), "cultivar persisted");
                    var species = GameComponent_CultivationJournal.Instance?.ForSpecies(v.cropDef);
                    var record = GameComponent_CultivationJournal.Instance?.ForCultivar(v);
                    Require(species?.harvests == savedHarvests && species.maxYield >= savedYield, "species journal persisted");
                    Require(record?.harvests == 1 && record.minYield == savedYield && record.maxYield == savedYield, "cultivar journal persisted");
                    Stage("SaveReload");
                    Finish("PASS", "startup, natural harvest, seed recovery, registration, documented cultivar harvest, save/reload");
                }
                catch (Exception e) { Finish("FAIL", e.ToString()); }
            });
        }
        private bool Isolated() => !string.IsNullOrEmpty(root) &&
            Path.GetFullPath(GenFilePaths.SaveDataFolderPath).TrimEnd(Path.DirectorySeparatorChar).Equals(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase) &&
            string.Join(",", LoadedModManager.RunningModsListForReading.Select(m => m.PackageIdPlayerFacing)).Equals(expectedPackages, StringComparison.OrdinalIgnoreCase);

        public override void GameComponentUpdate() { NativeShaderProbe.Update(); NativeGardenProbe.Update(); }

        public override void GameComponentTick()
        {
            if (!active || Find.TickManager.TicksGame < nextTick) return;
            nextTick = Find.TickManager.TicksGame + 20;
            try
            {
                Require(Isolated(), "isolation lost");
                if (stage == 0)
                {
                    var map = Find.Maps.Single(m => m.IsPlayerHome);
                    farmer = map.mapPawns.FreeColonistsSpawned.Where(p => p.skills != null && !p.Dead && !p.Downed && !p.InMentalState &&
                        !p.WorkTypeIsDisabled(WorkTypeDefOf.PlantCutting) && !p.WorkTypeIsDisabled(WorkTypeDefOf.Growing))
                        .OrderByDescending(p => p.skills.GetSkill(SkillDefOf.Plants).Level).First();
                    farmer.skills.GetSkill(SkillDefOf.Plants).Level = 20;
                    cropDef = DefDatabase<ThingDef>.GetNamed("Plant_Corn");
                    trait = DefDatabase<VarietyTraitDef>.GetNamed("HNS_Giant");
                    Require(GameComponent_CultivationJournal.Instance != null && GameComponent_NovelSeeds.Instance != null, "local components");
                    baseline = GameComponent_CultivationJournal.Instance.ForSpecies(cropDef)?.harvests ?? 0;
                    crop = SpawnCrop();
                    crop.TryGetComp<CompPlantVariety>().SetPendingTraits(new List<VarietyTraitDef> { trait });
                    Require(HorticulturePresentationPolicy.ForCultivar(crop.TryGetComp<CompPlantVariety>().Variety) == null, "pending genotype not documented");
                    var action = crop.TryGetComp<CompPlantVariety>().CompGetGizmosExtra().OfType<Command_Action>().First();
                    action.action();
                    Require(crop.TryGetComp<CompPlantVariety>().SaveSeedsRequested, "actual preserve-seeds action");
                    Dispatch(); stage = 1;
                    return;
                }
                if (!crop.Destroyed)
                {
                    Require(Find.TickManager.TicksGame <= deadline, "vanilla harvest timeout");
                    return;
                }
                var journal = GameComponent_CultivationJournal.Instance;
                var facts = journal.ForSpecies(cropDef);
                int delta = ProductCount(farmer.Map) - harvestBefore;
                Require(delta > 0, "actual harvest produced items");
                Require(facts?.harvests == baseline + 1 && facts.maxYield >= delta, "journal confirmed real output");
                if (stage == 1)
                {
                    Stage("HarvestAndSeedRecovery");
                    var pack = farmer.Map.listerThings.ThingsOfDef(HNS_DefOf.HNS_NovelSeedPack)
                        .FirstOrDefault(t => t.TryGetComp<CompNovelSeedPack>()?.CropDef == cropDef);
                    Require(pack != null, "seed pack recovered from real harvest");
                    pack.TryGetComp<CompNovelSeedPack>().UnlockWithName("Native fixture cultivar", farmer);
                    var v = GameComponent_NovelSeeds.Instance.VarietiesFor(cropDef).Single(v => v.customName == "Native fixture cultivar");
                    Require(pack.Destroyed && HorticulturePresentationPolicy.ForCultivar(v)?.AuthorizedTraits.Contains(trait) == true, "seed registration and documented trait");
                    Stage("Registration");
                    savedId = v.id;
                    baseline = facts.harvests;
                    crop = SpawnCrop();
                    crop.TryGetComp<CompPlantVariety>().SetVariety(v);
                    Dispatch(); stage = 2;
                    return;
                }
                var cultivar = GameComponent_NovelSeeds.Instance.GetVariety(savedId);
                var observed = journal.ForCultivar(cultivar);
                Require(observed?.harvests == 1 && observed.minYield == delta && observed.maxYield == delta, "actual cultivar yield recorded");
                savedHarvests = facts.harvests; savedYield = delta;
                Stage("CultivarHarvest");
                BeginReload();
            }
            catch (Exception e) { Finish("FAIL", e.ToString()); }
        }
        private Plant SpawnCrop()
        {
            var map = farmer.Map;
            var location = GenRadial.RadialCellsAround(farmer.Position, 20, true).First(c => c.InBounds(map) && c.Standable(map) &&
                c.GetPlant(map) == null && c.GetEdifice(map) == null && c.GetZone(map) == null &&
                c.GetTerrain(map).fertility >= cropDef.plant.fertilityMin && farmer.CanReach(c, PathEndMode.Touch, Danger.Deadly));
            var plant = (Plant)ThingMaker.MakeThing(cropDef);
            plant.Growth = 1; plant.sown = true;
            GenSpawn.Spawn(plant, location, map);
            return plant;
        }
        private void Dispatch()
        {
            var map = crop.Map;
            if (map.designationManager.DesignationOn(crop, DesignationDefOf.HarvestPlant) == null)
                map.designationManager.AddDesignation(new Designation(crop, DesignationDefOf.HarvestPlant));
            var giver = DefDatabase<WorkGiverDef>.GetNamed("PlantsCut").Worker as WorkGiver_PlantsCut;
            Job job = giver?.JobOnThing(farmer, crop, true);
            Require(job?.def == JobDefOf.HarvestDesignated && job.targetA.Thing == crop, "vanilla harvest job");
            harvestBefore = ProductCount(map);
            Require(farmer.jobs.TryTakeOrderedJob(job, JobTag.Misc), "job accepted");
            deadline = Find.TickManager.TicksGame + 12000;
        }
        private int ProductCount(Map map) => map.listerThings.ThingsOfDef(cropDef.plant.harvestedThingDef).Sum(t => t.stackCount);
        private void BeginReload()
        {
            active = false; Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
            saveName = "HNSFixture-" + run;
            LongEventHandler.QueueLongEvent(() =>
            {
                try
                {
                    Require(Isolated(), "save isolation");
                    string path = Path.GetFullPath(GenFilePaths.FilePathForSavedGame(saveName));
                    Require(path.StartsWith(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) && !File.Exists(path), "unique isolated save");
                    GameDataSaveLoader.SaveGame(saveName);
                    Require(File.Exists(path) && new FileInfo(path).Length > 0, "save written");
                    Log.Message(Prefix + "SAVED bytes=" + new FileInfo(path).Length);
                    LongEventHandler.ExecuteWhenFinished(() => { try { GameDataSaveLoader.LoadGame(saveName); } catch (Exception e) { Finish("FAIL", e.ToString()); } });
                }
                catch (Exception e) { Finish("FAIL", e.ToString()); }
            }, "SavingLongEvent", false, null);
        }
        private void Stage(string name) => Log.Message(Prefix + "STAGE_PASS " + name);
        private static void Require(bool condition, string label) { if (!condition) throw new InvalidOperationException(label); }
        private void Finish(string status, string message)
        {
            active = false;
            if (boostSet) { boost.SetValue(null, oldBoost); boostSet = false; }
            if (Find.TickManager != null) Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
            Log.Message(Prefix + status + " " + message.Replace('\r', ' ').Replace('\n', ' '));
        }
    }
}
#endif
