#if HNS_NATIVE_FIXTURE
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Globalization;
using RimWorld;
using LudeonTK;
using UnityEngine;
using Verse;

namespace HorticultureNovelSeeds
{
    // A controlled visual test of real registered cultivar trait sets, not a
    // synthetic color override. Deliberately samples extreme colors independent
    // of ordinary mutation weights, compatibility tags and species palettes.
    internal static class NativeGardenProbe
    {
        private static readonly string[][] SpeciesOptions =
        {
            new[] {"Plant_Rice","Plant_Corn","Plant_Potato"},
            new[] {"Plant_Strawberry","Plant_Cotton","Plant_Healroot"},
            new[] {"Plant_Hops","Plant_Smokeleaf","Plant_Psychoid"},
            new[] {"Plant_Devilstrand","Plant_TreeOak","Plant_TreeCocoa"}
        };
        private static readonly string[] ExtraSpecies = {"Plant_TreePine","Plant_TreeBirch","Plant_TreePoplar","Plant_Haygrass","Plant_Rose","Plant_Daylily"};
        private static readonly Color[] Pigments =
        {
            new Color(.98f,.14f,.36f), new Color(.20f,.38f,.96f), new Color(.95f,.65f,.10f),
            new Color(.12f,.88f,.73f), new Color(.83f,.14f,.94f), new Color(.97f,.30f,.10f)
        };
        private static readonly List<Plant> current = new();
        private static readonly List<Row> inventory = new();
        private static readonly HashSet<int> custom = new(), legacy = new(), vanilla = new();
        private static readonly HashSet<ThingDef> chosen = new();
        private static readonly Dictionary<int, HashSet<string>> maskSources = new();
        internal static void RecordMaskSource(Plant plant, int variation)
        {
            if (!active || plant == null) return;
            if (!maskSources.TryGetValue(plant.thingIDNumber, out var sources)) maskSources.Add(plant.thingIDNumber, sources = new());
            if ((patternGarden || catalogGarden) && ProceduralPhenotypeUtility.Resolve(plant.TryGetComp<CompPlantVariety>()).x > 0f && !NovelSeedUtility.HasRegionalPlantColor(plant.TryGetComp<CompPlantVariety>()))
            { sources.Add(variation + ":none"); return; }
            var layers = PlantMaskUtility.LayersForVariation(plant.def, variation, false);
            sources.Add(variation + ":" + (layers == null ? "none" : PlantAutoMaskCache.RecordSource(plant.def, variation, false)));
        }
        private static Map map;
        private static IntVec3 start;
        private static string root;
        private static Action<string> onStage;
        private static Action<string,string> finish;
        private static int panel, frames, captureAge;
        private static string capture;
        private static bool active;
        private static bool patternGarden, catalogGarden, creativeGarden, cultivarGarden, formGarden, organGarden, developmentGarden, windGarden, surfaceGarden, detailGarden, architectureGarden;
        private static readonly string[] ArchitectureTraits={"ForkedCrown","TieredCanopy","SpreadingRosette","FlowerSpray","AbundantClusters"};
        private static bool previousArchitectureWind;
        private static readonly string[] DetailTraits={"LobedLeaf","Windowleaf","SideShoots","CurlingTendrils","LanternBuds"};
        private static readonly List<string> DetailEvidence=new();
        private static int detailSampledPanel=-1,detailInitialGlows;
        private static bool previousDetailWind;
        private const int SurfaceFrames=4;
        private static readonly string[] SurfaceTraits={"SatinCuticle","VelvetNap","SilverBloom","RainBeads","PearlCuticle"};
        private static readonly List<string> SurfaceEvidence=new();
        private static int surfaceSampledPanel=-1,surfaceInitialMaterials;
        private static bool previousSurfaceWind;
        private const int WindFrames = 12;
        private static readonly string[] WindTraits = { "RigidHabit", "SuppleHabit", "Flutterleaf", "NoddingHabit", "SweepingHabit" };
        private static readonly List<string> WindEvidence = new();
        private static int windSampledPanel=-1, windInitialMaterialCount;
        private static float lastWindHead;
        private static bool previousWindPref;
        private static readonly string[] DevelopmentTraits = { "BronzeFlush", "Frostbud", "AutumnCrown", "RipeningBlush", "MaturingVariegation" };
        private static readonly float[] DevelopmentGrowth = { .30f, .60f, 1f };
        private static readonly string[] OrganTraits = { "PlumpFruit", "LongFruit", "BroadFruit", "ClusteredFruit", "ThickStalk" };
        private static readonly string[] OrganCrops = { "Plant_Corn","Plant_Cotton","Plant_Strawberry","Plant_Rose","Plant_Daylily","Plant_Hops" };
        private static readonly string[] FormTraits = { "Arching", "FanCrowned", "Rounded", "Weeping", "Spired" };
        private static readonly string[] FormPigments = { "RoseHarvest", "Variegated", "CopperTips", "RubyStalk", "BlueJade" };
        private static readonly string[] CultivarTraits = { "HoneyHarvest", "IvoryHarvest", "MulberryHarvest", "RoseHarvest", "BicolorHarvest", "CreamMargins", "LimeHeart", "CopperTips", "IvorySpeckle", "BurgundyMargins", "RubyStalk", "AmberStalk", "StripedStalk", "BlueJade", "TwoToneCanopy" };
        private static readonly string[] CreativeTraits = { "Velvetleaf", "Lacquered", "Dustbloom", "Silvered", "HaloEdged", "Netveined", "Tigerstriped", "Marbled", "Opaline", "Crystalline", "Bioluminescent", "NebulaMottle", "Coralbranch", "Prismatic", "Saltbloom", "Ashen", "Boggloss", "Waxbound", "Peatstained", "ScorchedMargins", "Variegated", "Anthocyanin", "MoonFrost", "GoldFreckled", "AuroraVeins" };
        private static readonly string[] CreativeCrops = { "Plant_Corn", "Plant_Cotton", "Plant_Strawberry" };
        private static List<List<VarietyTraitDef>> catalogCases;
        private static int PanelCount => architectureGarden ? 2 : detailGarden ? 3 : surfaceGarden ? SurfaceFrames : windGarden ? WindFrames : developmentGarden ? 3 : organGarden ? 2 : formGarden ? 2 : catalogGarden ? 6 : cultivarGarden ? 3 : creativeGarden ? 5 : SpeciesOptions.Length;
        private static readonly string[] CatalogCrops = {
            "Plant_Rice","Plant_Cotton","Plant_Corn", "Plant_Potato","Plant_Cotton","Plant_Hops",
            "Plant_Cotton","Plant_Healroot","Plant_Strawberry", "Plant_Corn","Plant_Corn","Plant_Corn",
            "Plant_Corn","Plant_Cotton","Plant_Corn", "Plant_Corn","Plant_Corn","Plant_Cotton" };
        private static List<List<VarietyTraitDef>> CatalogCases()
        {
            var result = new List<List<VarietyTraitDef>>();
            string[] named = {
                "Giant","Dwarf","Humongous","HighYield","LowYield",
                "Delicate","Robust","VeryDelicate","DiseaseResistant","DiseaseVulnerable",
                "Easy","Difficult","EasySow","DifficultSow","EasyHarvest",
                "DifficultHarvest","ColdResistant","ColdVulnerable","HeatResistant","HeatVulnerable",
                "ResourceDependent_Mulch","ResourceDependent_Hay","ResourceDependent_Fungus","Perennial","SelfSeeding",
                "Forage","FishHabitat","AquaticForage","Aquatic","Sandy",
                "HayByproduct","Joyresin","Painresin","Burnresin","Toxresin",
                "Thorny","Beautiful","Ugly","HighlyMedicinal","Joycompound",
                "Variegated","Anthocyanin","MoonFrost","GoldFreckled","AuroraVeins"
            };
            foreach (string name in named) result.Add(new() { Trait(name) });
            for (int n = 5; n <= 100; n += 5) result.Add(new() { Trait("Nutritious_" + n) });
            foreach (string stat in SynergyTraitFactory.StatOptions) result.Add(new() { Trait("SynergyGenerated_Plant_Rice_" + stat) });
            foreach (string family in new[] { "ProduceColor", "LeafColor", "StemColor" })
                for (int i = 0; i < 5; i++) result.Add(new() { Pigment(family, i) });
            result.Add(new() { Trait("Humongous"), Trait("AuroraVeins") });
            result.Add(new() { Trait("Robust"), Trait("ColdResistant"), Trait("DiseaseResistant") });
            result.Add(new() { Trait("Beautiful"), Trait("Variegated"), Pigment("ProduceColor", 0) });
            result.Add(new() { Trait("Burnresin"), Trait("HeatResistant"), Trait("Thorny") });
            result.Add(new() { Trait("Nutritious_100"), Trait("GoldFreckled"), Trait("HighYield") });
            Require(result.Count == 90 && result.SelectMany(x=>x).All(t=>t != null), "90 concrete catalog test cases");
            var all = DefDatabase<VarietyTraitDef>.AllDefsListForReading.Where(t => t.defName.StartsWith("HNS_", StringComparison.Ordinal)).ToList();
            Require(all.All(t => TraitAppearanceCatalog.TryGet(t, out _)), "all loaded HNS definitions have appearance rules");
            var singles = result.Where(x=>x.Count==1).Select(x=>x[0]).ToHashSet();
            Require(all.Where(t=>!t.configRoot && !t.generated && !ProceduralPhenotypeUtility.IsCreativeMode(t.proceduralPhenotype) && !ProceduralPhenotypeUtility.IsCultivarMode(t.proceduralPhenotype) && t.plantForm == PlantFormKind.None && t.organForm == OrganFormKind.None && t.windHabit == WindHabitKind.None && t.surfaceFinish == SurfaceFinishKind.None && t.botanicalDetail==BotanicalDetailKind.None && t.canopyArchitecture==CanopyArchitectureKind.None && !DevelopmentalPhenotype.IsMode((int)t.proceduralPhenotype)).All(singles.Contains), "every baseline authored concrete trait has an individual fixture; creative candidates use CreativeGarden");
            var manifest = new List<string> { "Trait ID\tLabel\tGenerated\tConfig root\tTier\tMotif\tPattern\tStrength\tScale\tWidth\tHeight\tDensity\tSampled individually" };
            foreach (var t in all.OrderBy(t=>t.defName))
            {
                TraitAppearanceCatalog.TryGet(t, out var p);
                manifest.Add(string.Join("\t", t.defName, t.label, t.generated, t.configRoot, p.Tier, p.Motif, p.Pattern,
                    p.Strength.ToString(CultureInfo.InvariantCulture), p.Scale.ToString(CultureInfo.InvariantCulture),
                    p.Width.ToString(CultureInfo.InvariantCulture), p.Height.ToString(CultureInfo.InvariantCulture),
                    p.Density.ToString(CultureInfo.InvariantCulture), singles.Contains(t)));
            }
            File.WriteAllLines(Path.Combine(root,"HNS-garden-catalog.tsv"),manifest);
            Log.Message("[HNS Garden] catalog covered=" + all.Count + "; authored=" + all.Count(t=>!t.generated) + "; generated=" + all.Count(t=>t.generated));
            return result;
        }
        private static readonly string[] PatternTraits = { "Variegated", "Anthocyanin", "MoonFrost", "GoldFreckled", "AuroraVeins" };
        private sealed record Row(int Panel, int RowNumber, int Column, Plant Plant, VarietyRecord Cultivar);
        internal static void RecordPrint(Plant plant, bool customPrint)
        {
            if (!active) return;
            if (customPrint) custom.Add(plant.thingIDNumber);
            else vanilla.Add(plant.thingIDNumber);
        }
        internal static void RecordLegacy(Plant plant)
        {
            if (active) legacy.Add(plant.thingIDNumber);
        }

        internal static void Begin(string path, Action<string> stage, Action<string,string> completed, bool procedural = false, bool catalog = false, bool creative = false, bool cultivar = false, bool form = false, bool organ = false, bool development = false, bool wind = false, bool surface = false, bool detail = false, bool architecture = false)
        {
            patternGarden = procedural || creative || cultivar || form || organ || development || wind || surface || detail || architecture; catalogGarden = catalog; creativeGarden = creative; cultivarGarden = cultivar; formGarden = form; organGarden = organ; developmentGarden = development; windGarden = wind; surfaceGarden=surface; detailGarden=detail; architectureGarden=architecture;
            root = path; onStage = stage; finish = completed;
            try
            {
                map = Find.CurrentMap;
                Require(map != null, "current map");
                WaylithPhenotypeShader.Enabled = !organGarden;
                PlantFormMesh.Enabled = formGarden;
                OrganSpriteExperiment.Enabled = organGarden;
                WindHabitUtility.Enabled=windGarden;
                SurfaceFinishUtility.Enabled=surfaceGarden;
                BotanicalDetailExperiment.Enabled=detailGarden;
                CanopyArchitectureExperiment.Enabled=architectureGarden;
                if(architectureGarden)
                {
                    previousArchitectureWind=Prefs.PlantWindSway;Prefs.PlantWindSway=false;
                    var defs=DefDatabase<VarietyTraitDef>.AllDefsListForReading.Where(t=>CanopyArchitectureMath.Valid(t.canopyArchitecture)).ToList();
                    Require(defs.Count==5 && defs.All(t=>t.commonality==0f && TraitAppearanceCatalog.TryGet(t,out _)),"five neutral architecture alleles");
                    Require(TraitCatalogValidation.Validate(DefDatabase<VarietyTraitDef>.AllDefsListForReading).Count==0,"trait catalog validation");
                }
                if(detailGarden)
                {
                    previousDetailWind=Prefs.PlantWindSway;Prefs.PlantWindSway=false;
                    var defs=DefDatabase<VarietyTraitDef>.AllDefsListForReading.Where(t=>BotanicalDetailMath.Valid(t.botanicalDetail)).ToList();
                    Require(defs.Count==5 && defs.All(t=>t.commonality==0f && TraitAppearanceCatalog.TryGet(t,out _)),"five neutral detail alleles");
                    Require(TraitCatalogValidation.Validate(DefDatabase<VarietyTraitDef>.AllDefsListForReading).Count==0,"trait catalog validation");
                    DetailEvidence.Add("Panel\tSpecies\tColumn\tPlant ID\tTrait\tSky glow\tDarkness\tGlow material ID\tRenderer");
                }
                if(surfaceGarden)
                {
                    previousSurfaceWind=Prefs.PlantWindSway; Prefs.PlantWindSway=false;
                    var defs=DefDatabase<VarietyTraitDef>.AllDefsListForReading.Where(t=>SurfaceFinishMath.Valid(t.surfaceFinish)).ToList();
                    Require(defs.Count==5 && defs.All(t=>t.commonality==0f && TraitAppearanceCatalog.TryGet(t,out _)),"five neutral surface alleles");
                    Require(TraitCatalogValidation.Validate(DefDatabase<VarietyTraitDef>.AllDefsListForReading).Count==0,"trait catalog validation");
                    SurfaceEvidence.Add("Panel\tSpecies\tColumn\tPlant ID\tTrait\tLocal hour\tWeather\tSky glow\tRain rate\tDirection X\tDirection Y\tLight\tWetness\tMaterial ID");
                }
                if(windGarden)
                {
                    previousWindPref=Prefs.PlantWindSway;
                    Prefs.PlantWindSway=true; // isolated process only, restored at End
                    var defs=DefDatabase<VarietyTraitDef>.AllDefsListForReading.Where(t=>WindHabitMath.Valid(t.windHabit)).ToList();
                    Require(defs.Count==5 && defs.All(t=>t.commonality==0f && TraitAppearanceCatalog.TryGet(t,out _)), "five neutral wind alleles");
                    Require(TraitCatalogValidation.Validate(DefDatabase<VarietyTraitDef>.AllDefsListForReading).Count==0,"trait catalog validation");
                    WindEvidence.Add("Frame\tSpecies\tColumn\tPlant ID\tTrait\tWind head\tWind speed\tExposure\tX\tZ\tRate\tFlutter\tMaterial ID");
                }
                if (developmentGarden)
                {
                    var defs=DefDatabase<VarietyTraitDef>.AllDefsListForReading.Where(t=>DevelopmentalPhenotype.IsMode((int)t.proceduralPhenotype)).ToList();
                    Require(defs.Count==5 && defs.All(t=>t.commonality==0f && TraitAppearanceCatalog.TryGet(t,out _)), "five neutral developmental alleles");
                    Require(TraitCatalogValidation.Validate(DefDatabase<VarietyTraitDef>.AllDefsListForReading).Count==0,"trait catalog validation");
                }
                if (organGarden)
                {
                    var defs=DefDatabase<VarietyTraitDef>.AllDefsListForReading.Where(t=>OrganSpriteMath.Valid(t.organForm)).ToList();
                    Require(defs.Count==5 && defs.All(t=>t.commonality==0f && TraitAppearanceCatalog.TryGet(t,out _)), "five neutral organ alleles");
                    Require(TraitCatalogValidation.Validate(DefDatabase<VarietyTraitDef>.AllDefsListForReading).Count==0,"trait catalog validation");
                }
                if (formGarden)
                {
                    var defs=DefDatabase<VarietyTraitDef>.AllDefsListForReading.Where(t=>t.plantForm!=PlantFormKind.None).ToList();
                    Require(defs.Count==5 && defs.All(t=>t.commonality==0f && PlantFormMath.IsValid(t.plantForm,t.plantFormStrength) && TraitAppearanceCatalog.TryGet(t,out _)), "five neutral form alleles");
                    Require(TraitCatalogValidation.Validate(DefDatabase<VarietyTraitDef>.AllDefsListForReading).Count==0,"trait catalog validation");
                }
                if (catalogGarden) catalogCases = CatalogCases();
                if (cultivarGarden)
                {
                    var all = DefDatabase<VarietyTraitDef>.AllDefsListForReading.Where(t=>ProceduralPhenotypeUtility.IsCultivarMode(t.proceduralPhenotype)).ToList();
                    Require(all.Count == 15 && all.All(t=>t.commonality == 0f && TraitAppearanceCatalog.TryGet(t,out _)), "15 neutral botanical candidates");
                    Require(all.Select(t=>t.proceduralPhenotype).Distinct().Count()==15 && CultivarTraits.Select(Trait).ToHashSet().SetEquals(all), "all new modes");
                    Require(TraitCatalogValidation.Validate(DefDatabase<VarietyTraitDef>.AllDefsListForReading).Count == 0, "trait catalog validation");
                }
                if (creativeGarden)
                {
                    var all = DefDatabase<VarietyTraitDef>.AllDefsListForReading.Where(t => ProceduralPhenotypeUtility.IsCreativeMode(t.proceduralPhenotype)).ToList();
                    Require(all.Count == 20 && all.All(t => t.commonality == 0f && TraitAppearanceCatalog.TryGet(t, out _)), "20 neutral creative definitions");
                    Require(all.Select(t => t.proceduralPhenotype).Distinct().Count() == 20, "distinct creative modes");
                    Require(CreativeTraits.Length == 25 && CreativeTraits.Select(Trait).All(t => ProceduralPhenotypeUtility.IsExplicitMode(t.proceduralPhenotype)), "all 25 explicit botanical motifs");
                    Require(TraitCatalogValidation.Validate(DefDatabase<VarietyTraitDef>.AllDefsListForReading).Count == 0, "trait catalog validation");
                }
                // Noon in local solar time, not merely the global time of day.
                int advance = Mathf.RoundToInt(Mathf.Repeat(.5f - GenLocalDate.DayPercent(map), 1f) * 60000f);
                Find.TickManager.DebugSetTicksGame(Find.TickManager.TicksGame + advance);
                map.weatherManager.TransitionTo(WeatherDefOf.Clear);
                map.weatherManager.lastWeather = WeatherDefOf.Clear;
                map.weatherManager.curWeatherAge = 4000;
                Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
                var origin = map.mapPawns.FreeColonistsSpawned.First().Position;
                // Reuse one deliberately cleared, unroofed and obstacle-free test
                // bed. Remove only vegetation in this disposable map.
                var candidates = GenRadial.RadialCellsAround(origin, 70, true)
                    .Where(c => Cells(c).All(x => x.InBounds(map)) && PlantCells(c).All(ClearCell))
                    .Take(120).Select(c => (Cell:c, Score:Cells(c).Count(ClearCell)))
                    .OrderByDescending(c => c.Score).ToArray();
                Require(candidates.Length > 0, "a visible unroofed plot with 18 clear plant positions");
                start = candidates[0].Cell;
                Log.Message("[HNS Garden] plot clear cells=" + candidates[0].Score);
                // Clear the entire visible foreground of this disposable map.
                // Trees can make a cell non-standable, so filtering plant removal
                // through ClearCell left their canopies covering adjacent rows.
                var vegetation = map.listerThings.AllThings.OfType<Plant>().Where(p =>
                    p.Position.x >= start.x - 7 && p.Position.x <= start.x + 22 &&
                    p.Position.z >= start.z - 7 && p.Position.z <= start.z + 16).ToArray();
                foreach (var plant in vegetation) if (plant.Spawned) plant.Destroy(DestroyMode.Vanish);
                if (creativeGarden || cultivarGarden || formGarden || organGarden || developmentGarden || windGarden || surfaceGarden || detailGarden || architectureGarden)
                {
                    // Remove dropped starter supplies only from this disposable visual plot.
                    var items = map.listerThings.AllThings.Where(t => t.def.category == ThingCategory.Item &&
                        t.Position.x >= start.x - 3 && t.Position.x <= start.x + 18 &&
                        t.Position.z >= start.z - 3 && t.Position.z <= start.z + 12).ToArray();
                    foreach (var item in items) if (item.Spawned) item.Destroy(DestroyMode.Vanish);
                }
                foreach (var cell in Cells(start).Where(ClearCell))
                    map.terrainGrid.SetTerrain(cell, TerrainDefOf.Soil);
                Find.WindowStack.TryRemove(typeof(EditWindow_Log), false);
                Find.Selector.ClearSelection();
                Find.CameraDriver.JumpToCurrentMapLoc(start + new IntVec3(8,0,4));
                Find.CameraDriver.SetRootSize(8.4f);
                if(surfaceGarden) SetSurfaceWeather();
                if(detailGarden)SetDetailTime();
                active = true;
                MakePanel();
                onStage("GardenSetup");
            }
            catch (Exception e) { End("FAIL", e.ToString()); }
        }

        private static bool ClearCell(IntVec3 x) => x.InBounds(map) && x.Standable(map) && x.GetEdifice(map) == null && !x.Fogged(map) && x.GetZone(map) == null && map.roofGrid.RoofAt(x) == null;

        private static IEnumerable<IntVec3> PlantCells(IntVec3 c)
        {
            for(int z = 0; z < 3; z++)
                for(int x = 0; x < 6; x++)
                    yield return c + new IntVec3(x * 3, 0, z * 4);
        }

        private static IEnumerable<IntVec3> Cells(IntVec3 c)
        {
            for (int z = -3; z <= 12; z++)
                for (int x = -3; x <= 18; x++)
                    yield return c + new IntVec3(x,0,z);
        }

        private static ThingDef Choose(string name)
        {
            foreach (string candidate in new[] {name}.Concat(ExtraSpecies))
            {
                var def = DefDatabase<ThingDef>.GetNamedSilentFail(candidate);
                if (HorticulturePlantPolicy.IsSupported(def) && chosen.Add(def)) return def;
            }
            throw new InvalidOperationException("No supported unique species for " + name);
        }
        private static VarietyTraitDef Trait(string name) => DefDatabase<VarietyTraitDef>.GetNamed("HNS_" + name);
        private static VarietyTraitDef Pigment(string family, int color) =>
            ColorTraitFactory.TraitForColor(family, Pigments[(color + Pigments.Length) % Pigments.Length]);

        private static List<VarietyTraitDef> Traits(int row, int col)
        {
            // Deliberate extremes of real trait definitions; no changes to trait
            // defs/settings and no claims about natural mutation eligibility.
            if (architectureGarden) return col==0 ? new() : new() { Trait(ArchitectureTraits[col-1]) };
            if (detailGarden) return col==0 ? new() : new() { Trait(DetailTraits[col-1]) };
            if (surfaceGarden) return col==0 ? new() : new() { Trait(SurfaceTraits[col-1]) };
            if (windGarden) return col==0 ? new() : new() { Trait(WindTraits[col-1]) };
            if (developmentGarden) return col==0 ? new() : new() { Trait(DevelopmentTraits[col-1]) };
            if (organGarden) return col==0 ? new() : new() { Trait(OrganTraits[col-1]) };
            if (formGarden) return col==0 ? new() : row<3 ? new() { Trait(FormTraits[col-1]) } : new() { Trait(FormTraits[col-1]), Trait(FormPigments[col-1]) };
            if (cultivarGarden) return col == 0 ? new() : new() { Trait(CultivarTraits[(row / 3) * 5 + col - 1]) };
            if (creativeGarden) return col == 0 ? new() : new() { Trait(CreativeTraits[(row / 3) * 5 + col - 1]) };
            if (catalogGarden) return col == 0 ? new() : catalogCases[row * 5 + col - 1];
            if (patternGarden) return col == 0 ? new() : new() { Trait(PatternTraits[col - 1]) };
            return col switch
            {
                1 => new() { Trait(row % 3 == 0 ? "Humongous" : "Giant"), Trait("HighYield"), Trait("Robust") },
                2 => new() { Trait("Dwarf"), Trait("LowYield"), Trait(row % 2 == 0 ? "VeryDelicate" : "Delicate") },
                3 => new() { Pigment("ProduceColor", row), Trait("DiseaseResistant") },
                4 => new() { Pigment("LeafColor", row + 3), Trait("Giant"), Trait("ColdResistant") },
                5 => new() { Pigment("ProduceColor", row + 2), Pigment("LeafColor", row + 4),
                    Pigment("StemColor", row + 1), Trait(row % 2 == 0 ? "Beautiful" : "Ugly"),
                    Trait(row % 2 == 0 ? "HeatResistant" : "DiseaseVulnerable") },
                _ => new()
            };
        }
        private static void MakePanel()
        {
            frames = 0; captureAge = 0; capture = null;
            var registry = GameComponent_NovelSeeds.Instance;
            Require(registry != null, "registry");
            for (int r = 0; r < 3; r++)
            {
                var def = (architectureGarden || organGarden) ? Choose(OrganCrops[panel*3+r]) : (creativeGarden || cultivarGarden || formGarden || developmentGarden || windGarden || surfaceGarden || detailGarden) ? DefDatabase<ThingDef>.GetNamed(CreativeCrops[r]) : catalogGarden ? DefDatabase<ThingDef>.GetNamed(CatalogCrops[panel*3+r]) : Choose(patternGarden && panel == 3 && r == 1 ? "Plant_Haygrass" : SpeciesOptions[panel][r]);
                Require(HorticulturePlantPolicy.IsSupported(def), "supported garden crop");
                if (catalogGarden || creativeGarden || cultivarGarden || formGarden || developmentGarden || windGarden || surfaceGarden || detailGarden) chosen.Add(def);
                // Prewarm actual resolved masks before rendering. If shared
                // manual or seasonal materials cannot use the experimental
                // backend, their existing CPU path is intentionally retained.
                if(architectureGarden)
                    for(int v=0;v<PlantMaskUtility.VariationCount(def);v++) CanopyArchitectureExperiment.Prepare(def,v);
                if(detailGarden)
                    for(int v=0;v<PlantMaskUtility.VariationCount(def);v++) BotanicalDetailExperiment.Prepare(def,v);
                if (organGarden)
                    for(int v=0;v<PlantMaskUtility.VariationCount(def);v++) OrganSpriteExperiment.Prepare(def,v);
                if (!patternGarden)
                    for (int v = 0; v < PlantMaskUtility.VariationCount(def); v++)
                        WaylithPhenotypeShader.PrepareMask(def, v);
                for (int c = 0; c < 6; c++)
                {
                    var plant = (Plant)ThingMaker.MakeThing(def);
                    var comp = plant.TryGetComp<CompPlantVariety>();
                    Require(comp != null, "plant genetics component " + def.defName);
                    VarietyRecord cultivar = null;
                    if (c != 0)
                    {
                        var traits = Traits(panel * 3 + r, c);
                        Require(traits.All(t => t != null) && traits.Count > 0, "valid actual traits");
                        cultivar = registry.UnlockVariety(def, traits, "Garden " + def.label + " " + c, originKind:"debug-visual-fixture");
                        Require(cultivar != null && cultivar.traits.Count == traits.Count, "registered cultivar");
                        comp.SetVariety(cultivar);
                        Require(comp.Variety == cultivar && comp.ActiveTraits.Count == traits.Count, "assigned real genetics");
                    }
                    else comp.SetVariety(null);
                    plant.sown = true; plant.Growth = developmentGarden ? DevelopmentGrowth[0] : 1f;
                    plant.HitPoints = plant.MaxHitPoints;
                    // Row A on top, columns left-to-right.
                    GenSpawn.Spawn(plant, start + new IntVec3(c * 3,0,(2-r)*4), map);
                    Require(plant.Spawned, "spawned garden plant");
                    current.Add(plant); inventory.Add(new Row(panel + 1,r + 1,c + 1,plant,cultivar));
                    plant.DirtyMapMesh(map);
                }
            }
            Log.Message("[HNS Garden] panel=" + (panel+1) + "; species=" + string.Join(", ",current.Select(p=>p.def.defName).Distinct()));
        }

        internal static void Update()
        {
            if (!active) return;
            try
            {
                frames++;
                if (Find.WindowStack.TryRemove(typeof(EditWindow_Log), false)) { frames = 0; return; }
                if(detailGarden && detailSampledPanel!=panel && frames>=100 && inventory.Where(r=>r.Panel==1&&r.Cultivar?.traits.Any(t=>t.botanicalDetail==BotanicalDetailKind.LanternBuds)==true).All(r=>BotanicalDetailExperiment.GlowPlants.ContainsKey(r.Plant.thingIDNumber)))
                {
                    SampleDetail();detailSampledPanel=panel;frames=92;return;
                }
                if(surfaceGarden && surfaceSampledPanel!=panel && frames>=100 && inventory.Where(r=>r.Panel==1 && r.Cultivar!=null).All(r=>WaylithPhenotypeShader.SurfacePrints.ContainsKey(r.Plant.thingIDNumber)))
                {
                    SampleSurface();surfaceSampledPanel=panel;frames=92;return;
                }
                if(windGarden && windSampledPanel!=panel && frames>=100 && inventory.Where(r=>r.Panel==1 && r.Cultivar!=null).All(r=>WaylithPhenotypeShader.WindPrints.ContainsKey(r.Plant.thingIDNumber)))
                {
                    SampleWind();
                    windSampledPanel=panel;
                    frames=92; // let the real renderer consume updated uniforms
                    return;
                }
                if (capture == null && frames >= 100 && (!windGarden || windSampledPanel==panel) && (!surfaceGarden || surfaceSampledPanel==panel) && (!detailGarden||detailSampledPanel==panel) && current.All(p => custom.Contains(p.thingIDNumber) || vanilla.Contains(p.thingIDNumber)) &&
                    (!developmentGarden || inventory.Where(r=>r.Panel==panel+1 && r.Cultivar!=null).All(r=>WaylithPhenotypeShader.DevelopmentalPrints.Contains((r.Plant.thingIDNumber,panel)))))
                {
                    capture = Path.Combine(root, "HNS-garden-panel-" + (panel+1) + ".png");
                    Log.Message("[HNS Garden] screenshot " + (panel+1) + "; local hour=" + GenLocalDate.HourOfDay(map) +
                        "; sky glow=" + map.skyManager.CurSkyGlow + "; shader materials=" + WaylithPhenotypeShader.MaterialCount +
                        "; prepared masks=" + WaylithPhenotypeShader.MaskCount);
                    ScreenCapture.CaptureScreenshot(capture);
                }
                if (capture != null && File.Exists(capture) && new FileInfo(capture).Length > 1000 && ++captureAge >= 30)
                {
                    onStage("GardenPanel" + (panel+1));
                    if (++panel == PanelCount)
                    {
                        WriteInventory();
                        onStage("GardenInventory");
                        End("PASS", architectureGarden ? "36 plants; 30 modular canopy cases on six species; two daylight screenshots; explicit fallback for unsupported modules" : detailGarden ? "18 same plants; 12 topology cases and three night overlays; day-night-day screenshots" : surfaceGarden ? "18 same mature plants; 15 surface finishes; four native lighting/weather states; 60 verified material states" : windGarden ? "18 same mature plants; 15 genetic wind materials; 12 actual wind-phase captures; 180 material-phase checks" : developmentGarden ? "18 same plants across three growth stages; 45 verified cultivar-stage GPU prints; 30 automatic mesh invalidations; three daylight screenshots" : organGarden ? "36 plants; 30 organ-form cases; eligible components composited; uncertain organs fall back; two daylight screenshots" : formGarden ? "36 plants; 30 genetically deformed cultivars; 15 pure morphology and 15 morphology plus pigmentation; two daylight screenshots" : cultivarGarden ? "54 plants; 15 organ-aware botanical candidates across three species; three daylight screenshots; 45 mask-free GPU cultivars" : creativeGarden ? "90 garden plants; 25 refined botanical traits across three species; five daylight screenshots; 75 mask-free GPU cultivars" : catalogGarden ? "108 garden plants; 90 baseline trait cases; six daylight screenshots; every loaded HNS appearance rule covered" : "72 garden plants, 60 registered cultivars, 12 species, four daylight screenshots");
                        return;
                    }
                    if(detailGarden)
                    {
                        frames=0;captureAge=0;capture=null;
                        foreach(var row in inventory.Where(r=>r.Panel==1).ToArray()) inventory.Add(row with { Panel=panel+1 });
                        SetDetailTime();return;
                    }
                    if(surfaceGarden)
                    {
                        frames=0; captureAge=0; capture=null;
                        foreach(var row in inventory.Where(r=>r.Panel==1).ToArray()) inventory.Add(row with { Panel=panel+1 });
                        SetSurfaceWeather();return;
                    }
                    if(windGarden)
                    {
                        frames=0; captureAge=0; capture=null;
                        foreach(var row in inventory.Where(r=>r.Panel==1).ToArray()) inventory.Add(row with { Panel=panel+1 });
                        return;
                    }
                    if (developmentGarden)
                    {
                        frames=0; captureAge=0; capture=null;
                        var original=inventory.Where(r=>r.Panel==1).ToArray();
                        foreach(var row in original)
                        {
                            row.Plant.Growth=DevelopmentGrowth[panel];
                            // Controls have no developmental allele. The styled
                            // plants must be invalidated by the growth patch.
                            if(row.Cultivar==null) row.Plant.DirtyMapMesh(map);
                            inventory.Add(row with { Panel=panel+1 });
                        }
                        Log.Message("[HNS Development] phase="+panel+"; growth="+DevelopmentGrowth[panel]+"; same plants="+current.Count);
                        return;
                    }
                    foreach (var plant in current) if (plant.Spawned) plant.Destroy(DestroyMode.Vanish);
                    current.Clear();
                    MakePanel();
                    return;
                }
                if (frames > 1200) throw new InvalidOperationException("garden print/capture timeout " + (panel+1));
            }
            catch (Exception e) { End("FAIL", e.ToString()); }
        }

        private static void SetDetailTime()
        {
            float target=panel==1?0f:.5f;
            int advance=Mathf.RoundToInt(Mathf.Repeat(target-GenLocalDate.DayPercent(map),1f)*60000f);
            Find.TickManager.DebugSetTicksGame(Find.TickManager.TicksGame+advance);
            map.skyManager.SkyManagerUpdate();map.windManager.WindManagerTick();
        }
        private static void SampleDetail()
        {
            var rows=inventory.Where(r=>r.Panel==1&&r.Cultivar!=null).ToArray();
            Require(rows.Length==15,"detail fixture state");
            if(panel==0)detailInitialGlows=BotanicalDetailExperiment.GlowCount;
            float sky=map.skyManager.CurSkyGlow,dark=BotanicalDetailMath.Darkness(sky);
            Require(panel==1?dark>.8f:dark==0f,"native day night sky state");
            Require(detailInitialGlows==BotanicalDetailExperiment.GlowCount,"stable glow materials");
            foreach(var r in rows)
            {
                var kind=r.Cultivar.traits.Single().botanicalDetail;
                int matid=0;
                if(kind==BotanicalDetailKind.LanternBuds)
                {
                    var mat=BotanicalDetailExperiment.GlowPlants[r.Plant.thingIDNumber];
                    Require(mat.shader==DefDatabase<ShaderDef>.GetNamed("HNS_BotanicalNight").Shader&&Math.Abs(mat.GetFloat("_HnsDarkness")-dark)<.004f&&mat.renderQueue==3151,"actual post-light shader and darkness");matid=mat.GetInstanceID();
                }
                else Require(BotanicalDetailExperiment.PrintedPlants.Contains(r.Plant.thingIDNumber)||BotanicalDetailExperiment.FallbackPlants.Contains(r.Plant.thingIDNumber),"topology render or fallback");
                DetailEvidence.Add(string.Join("\t",panel+1,r.Plant.def.defName,r.Column,r.Plant.thingIDNumber,kind,sky,dark,matid,Backend(r.Plant)));
            }
            Log.Message("[HNS Detail] phase="+panel+"; sky="+sky+"; darkness="+dark+"; glow materials="+detailInitialGlows+"; topology rendered="+BotanicalDetailExperiment.PrintedPlants.Count+"; fallback="+BotanicalDetailExperiment.FallbackPlants.Count);
        }
        private static void SetSurfaceWeather()
        {
            float target=panel==0?.375f:.625f;
            int advance=Mathf.RoundToInt(Mathf.Repeat(target-GenLocalDate.DayPercent(map),1f)*60000f);
            Find.TickManager.DebugSetTicksGame(Find.TickManager.TicksGame+advance);
            var weather=panel==2?DefDatabase<WeatherDef>.GetNamed("Rain"):WeatherDefOf.Clear;
            map.weatherManager.TransitionTo(weather);map.weatherManager.lastWeather=weather;map.weatherManager.curWeatherAge=4000;
            map.skyManager.SkyManagerUpdate();
            // Keep motion constant; the variable is lighting/weather only.
            map.windManager.WindManagerTick();
        }
        private static void SampleSurface()
        {
            var styled=inventory.Where(r=>r.Panel==1 && r.Cultivar!=null).ToArray();
            Require(styled.Length==15,"surface fixture state");
            if(panel==0)surfaceInitialMaterials=WaylithPhenotypeShader.MaterialCount;
            var env=SurfaceFinishUtility.Current(map);
            Require((panel==0?env.x<0f:env.x>0f) && (panel==2?env.w>0f:env.w==0f),"actual weather and light direction");
            Require(WaylithPhenotypeShader.MaterialCount==surfaceInitialMaterials && SurfaceFinishUtility.MaterialCount==surfaceInitialMaterials,"stable shared materials");
            foreach(var row in styled)
            {
                var mat=WaylithPhenotypeShader.SurfacePrints[row.Plant.thingIDNumber];var trait=row.Cultivar.traits.Single();
                Require(mat.GetFloat("_HnsSurface")==(int)trait.surfaceFinish && mat.GetVector("_HnsEnvironment")==env,"actual surface material and refreshed map environment");
                SurfaceEvidence.Add(string.Join("\t",panel+1,row.Plant.def.defName,row.Column,row.Plant.thingIDNumber,trait.defName,GenLocalDate.HourOfDay(map),map.weatherManager.curWeather.defName,map.skyManager.CurSkyGlow,map.weatherManager.RainRate,env.x,env.y,env.z,env.w,mat.GetInstanceID()));
            }
            Log.Message("[HNS Surface] panel="+(panel+1)+"; environment="+env+"; rain="+map.weatherManager.RainRate+"; materials="+surfaceInitialMaterials);
        }

        private static void SampleWind()
        {
            var styled=inventory.Where(r=>r.Panel==1 && r.Cultivar!=null).ToArray();
            Require(styled.Length==15 && Prefs.PlantWindSway,"wind fixture state");
            var first=WaylithPhenotypeShader.WindPrints[styled[0].Plant.thingIDNumber];
            if(panel==0) windInitialMaterialCount=WaylithPhenotypeShader.MaterialCount;
            // Invoke the native wind manager while gameplay is paused. No plant
            // growth, time-speed changes, or direct shader phase injection.
            float before=first.GetFloat("_SwayHead");
            int count=0;
            do { map.windManager.WindManagerTick(); count++; }
            while(first.GetFloat("_SwayHead")<before+3.5f && count<250);
            float head=first.GetFloat("_SwayHead");
            Require(float.IsFinite(head) && head>before && (panel==0||head>lastWindHead),"native wind phase advanced");
            Require(WaylithPhenotypeShader.MaterialCount==windInitialMaterialCount,"no new phase materials");
            lastWindHead=head;
            foreach(var r in styled)
            {
                var mat=WaylithPhenotypeShader.WindPrints[r.Plant.thingIDNumber];
                var trait=r.Cultivar.traits.Single();
                var controls=WindHabitUtility.Controls(trait.windHabit);
                Require(mat.shader==DefDatabase<ShaderDef>.GetNamed(WaylithPhenotypeShader.WindDefName).Shader && mat.GetVector("_HnsWind")==controls && mat.GetFloat("_SwayHead")==head,"actual genetic material and synchronized phase");
                Require(PlantUtility.GetWindExposure(r.Plant)>0,"nonzero original plant exposure");
                WindEvidence.Add(string.Join("\t",panel+1,r.Plant.def.defName,r.Column,r.Plant.thingIDNumber,trait.defName,head.ToString("R",CultureInfo.InvariantCulture),map.windManager.WindSpeed.ToString("R",CultureInfo.InvariantCulture),PlantUtility.GetWindExposure(r.Plant),controls.x,controls.y,controls.z,controls.w,mat.GetInstanceID()));
            }
            Log.Message("[HNS Wind] frame="+(panel+1)+"; head="+head+"; native manager updates="+count+"; materials="+windInitialMaterialCount);
        }

        private static string Backend(Plant p)
        {
            int id = p.thingIDNumber;
            if(CanopyArchitectureExperiment.PrintedPlants.Contains(id))return "Modular canopy planes";
            if(architectureGarden && p.TryGetComp<CompPlantVariety>()?.ActiveTraits.Any(t=>CanopyArchitectureMath.Valid(t.canopyArchitecture))==true)return "Architecture unavailable, original path";
            if(BotanicalDetailExperiment.GlowPlants.ContainsKey(id))return "Post-light night overlay";
            if(BotanicalDetailExperiment.PrintedPlants.Contains(id))return "Botanical topology composite";
            if(BotanicalDetailExperiment.FallbackPlants.Contains(id))return "Topology unavailable, original path";
            if (WaylithPhenotypeShader.SurfacePrints.ContainsKey(id)) return "GPU genetic surface";
            if (WaylithPhenotypeShader.WindPrints.ContainsKey(id)) return "GPU genetic wind";
            if (OrganSpriteExperiment.PrintedPlants.Contains(id)) return "Organ composite, original shader";
            if (OrganSpriteExperiment.FallbackPlants.Contains(id)) return "Organ unavailable, original path";
            bool gpu = WaylithPhenotypeShader.PrintedPlants.Contains(id), cpu = legacy.Contains(id);
            if (WaylithPhenotypeShader.MaskFreeProceduralPlants.Contains(id)) return cpu ? "Mixed procedural/legacy" : "GPU procedural, no mask";
            return gpu ? (cpu ? "Mixed GPU/legacy" : WaylithPhenotypeShader.MaskedPlants.Contains(id) ? "GPU masked" : "GPU whole") :
                cpu ? "Legacy material" : custom.Contains(id) ? "Geometry/effects only" : "Vanilla";
        }
        private static void WriteInventory()
        {
            var lines = new List<string> { "Panel\tRow\tColumn\tSpecies\tCultivar\tTrait IDs\tRenderer\tAutomatic mask sources\tTrait labels\tTiers\tMotifs\tForm mesh" };
            foreach (var entry in inventory)
                lines.Add(string.Join("\t", entry.Panel, entry.RowNumber, entry.Column, entry.Plant.def.defName,
                    entry.Cultivar?.customName ?? "Vanilla control", string.Join(", ",entry.Cultivar?.traits.Select(t=>t.defName) ?? Enumerable.Empty<string>()), Backend(entry.Plant), maskSources.TryGetValue(entry.Plant.thingIDNumber, out var sources) ? string.Join(", ", sources.OrderBy(x => x)) : "not styled",
                    string.Join(" + ",entry.Cultivar?.traits.Select(t=>t.label) ?? Enumerable.Empty<string>()),
                    string.Join(" + ",entry.Cultivar?.traits.Select(t=>TraitAppearanceCatalog.TryGet(t,out var p)?p.Tier:"External") ?? Enumerable.Empty<string>()),
                    string.Join(" + ",entry.Cultivar?.traits.Select(t=>TraitAppearanceCatalog.TryGet(t,out var p)?p.Motif:"External") ?? Enumerable.Empty<string>()),
                    PlantFormMesh.PrintedPlants.Contains(entry.Plant.thingIDNumber) ? "Deformed" : "Original"));
            File.WriteAllLines(Path.Combine(root,"HNS-garden-inventory.tsv"),lines);
            if(architectureGarden)
            {
                var styled=inventory.Where(r=>r.Cultivar!=null).ToList();
                Require(inventory.Count==36&&styled.Count==30&&chosen.Count==6,"36 plants six species");
                Require(inventory.Where(r=>r.Cultivar==null).All(r=>Backend(r.Plant)=="Vanilla"),"six original controls");
                Require(styled.All(r=>Backend(r.Plant)=="Modular canopy planes"||Backend(r.Plant)=="Architecture unavailable, original path"),"every architecture case accounted for");
                Require(CanopyArchitectureExperiment.PrintedPlants.Count>=15,"at least 15 actual modular variants");
                Require(CanopyArchitectureExperiment.PlaneCounts.Values.All(n=>n>0&&n<=5),"bounded extra planes");
                Require(WaylithPhenotypeShader.MaterialCount==0&&WaylithPhenotypeShader.MaskCount==0,"original shader and no GPU masks");
                File.WriteAllLines(Path.Combine(root,"HNS-garden-architecture-preparation.tsv"),new[]{"Species\tVariation\tTexture\tWidth\tHeight\tLeaf W\tLeaf H\tFruit W\tFruit H\tAnchors"}.Concat(CanopyArchitectureExperiment.Preparation));
                File.WriteAllLines(Path.Combine(root,"HNS-garden-architecture.tsv"),new[]{"Panel\tRow\tColumn\tSpecies\tPlant ID\tTrait\tRenderer\tAdditional planes"}.Concat(inventory.Select(r=>string.Join("\t",r.Panel,r.RowNumber,r.Column,r.Plant.def.defName,r.Plant.thingIDNumber,r.Cultivar?.traits.Single().defName??"Control",Backend(r.Plant),CanopyArchitectureExperiment.PlaneCounts.TryGetValue(r.Plant.thingIDNumber,out int n)?n:0))));
                Log.Message("[HNS Architecture] rendered="+CanopyArchitectureExperiment.PrintedPlants.Count+"; fallback="+(styled.Count-CanopyArchitectureExperiment.PrintedPlants.Count)+"; source modules="+CanopyArchitectureExperiment.PreparedCount+"; materials="+CanopyArchitectureExperiment.MaterialCount+"; additional planes="+CanopyArchitectureExperiment.PlaneCounts.Values.Sum());return;
            }
            if(detailGarden)
            {
                var styled=inventory.Where(r=>r.Cultivar!=null).ToList();
                Require(inventory.Count==54&&styled.Count==45&&inventory.Select(r=>r.Plant.thingIDNumber).Distinct().Count()==18,"same 18 plants in three lighting states");
                Require(DetailEvidence.Count==46&&BotanicalDetailExperiment.GlowPlants.Count==3&&BotanicalDetailExperiment.GlowCount==detailInitialGlows,"complete phase evidence");
                Require(inventory.Where(r=>r.Cultivar==null).All(r=>Backend(r.Plant)=="Vanilla"),"unchanged controls");
                Require(inventory.Where(r=>r.Panel==1&&r.Cultivar!=null).Count(r=>BotanicalDetailExperiment.PrintedPlants.Contains(r.Plant.thingIDNumber))>=6,"at least six topology variants");
                Require(WaylithPhenotypeShader.MaterialCount==0&&WaylithPhenotypeShader.MaskCount==0,"original base materials, no GPU masks");
                File.WriteAllLines(Path.Combine(root,"HNS-garden-detail.tsv"),DetailEvidence);
                File.WriteAllLines(Path.Combine(root,"HNS-garden-detail-preparation.tsv"),new[]{"Species\tVariation\tTexture\tTrait\tWidth\tHeight\tRemoved\tAdded"}.Concat(BotanicalDetailExperiment.Preparation));
                Log.Message("[HNS Detail] prepared="+BotanicalDetailExperiment.PreparedCount+"; topology="+BotanicalDetailExperiment.PrintedPlants.Count+"; fallback="+BotanicalDetailExperiment.FallbackPlants.Count+"; glows="+BotanicalDetailExperiment.GlowPlants.Count);return;
            }
            if(surfaceGarden)
            {
                var styled=inventory.Where(r=>r.Cultivar!=null).ToList();
                Require(inventory.Count==18*SurfaceFrames && styled.Count==15*SurfaceFrames && inventory.Select(r=>r.Plant.thingIDNumber).Distinct().Count()==18,"same plants in four environments");
                Require(styled.All(r=>Backend(r.Plant)=="GPU genetic surface") && inventory.Where(r=>r.Cultivar==null).All(r=>Backend(r.Plant)=="Vanilla"),"all surface cultivars and unchanged controls");
                Require(SurfaceEvidence.Count==1+15*SurfaceFrames && WaylithPhenotypeShader.MaskCount==0 && WaylithPhenotypeShader.MaterialCount==surfaceInitialMaterials,"60 environment states without material growth");
                File.WriteAllLines(Path.Combine(root,"HNS-garden-surface.tsv"),SurfaceEvidence);
                Log.Message("[HNS Surface] verified states="+(SurfaceEvidence.Count-1)+"; materials="+surfaceInitialMaterials+"; masks="+WaylithPhenotypeShader.MaskCount);return;
            }
            if(windGarden)
            {
                var styled=inventory.Where(r=>r.Cultivar!=null).ToList();
                Require(inventory.Count==18*WindFrames && styled.Count==15*WindFrames && inventory.Select(r=>r.Plant.thingIDNumber).Distinct().Count()==18,"same plants across 12 phases");
                Require(styled.All(r=>Backend(r.Plant)=="GPU genetic wind") && inventory.Where(r=>r.Cultivar==null).All(r=>Backend(r.Plant)=="Vanilla"),"15 wind cultivars and three original controls");
                Require(WindEvidence.Count==1+15*WindFrames && WaylithPhenotypeShader.MaskCount==0 && WaylithPhenotypeShader.MaterialCount==windInitialMaterialCount,"complete phase evidence without material growth");
                File.WriteAllLines(Path.Combine(root,"HNS-garden-wind.tsv"),WindEvidence);
                Log.Message("[HNS Wind] verified phases="+(WindEvidence.Count-1)+"; materials="+windInitialMaterialCount+"; masks="+WaylithPhenotypeShader.MaskCount);
                return;
            }
            if(developmentGarden)
            {
                var styled=inventory.Where(r=>r.Cultivar!=null).ToList();
                Require(inventory.Count==54 && styled.Count==45 && inventory.Select(r=>r.Plant.thingIDNumber).Distinct().Count()==18,"same plants across all stages");
                Require(styled.All(r=>WaylithPhenotypeShader.DevelopmentalPrints.Contains((r.Plant.thingIDNumber,r.Panel-1))),"all 45 actual developmental material states");
                Require(styled.Where(r=>r.Panel>1).All(r=>DevelopmentalPhenotypeRefresh.Invalidations.Contains((r.Plant.thingIDNumber,r.Panel-1))),"30 automatic mesh invalidations");
                Require(inventory.Where(r=>r.Cultivar==null).All(r=>Backend(r.Plant)=="Vanilla"),"controls remain original");
                Require(WaylithPhenotypeShader.MaskCount==0,"no prepared masks");
                var evidence=new List<string>{"Panel\tGrowth\tPhase\tSpecies\tColumn\tPlant ID\tTrait\tMaterial verified\tGrowth invalidation"};
                foreach(var row in inventory) evidence.Add(string.Join("\t",row.Panel,DevelopmentGrowth[row.Panel-1].ToString(CultureInfo.InvariantCulture),(row.Panel-1)*.5f,row.Plant.def.defName,row.Column,row.Plant.thingIDNumber,row.Cultivar?.traits.Single().defName??"Control",WaylithPhenotypeShader.DevelopmentalPrints.Contains((row.Plant.thingIDNumber,row.Panel-1)),DevelopmentalPhenotypeRefresh.Invalidations.Contains((row.Plant.thingIDNumber,row.Panel-1))));
                File.WriteAllLines(Path.Combine(root,"HNS-garden-development.tsv"),evidence);
                Log.Message("[HNS Development] verified stages="+WaylithPhenotypeShader.DevelopmentalPrints.Count+"; invalidations="+DevelopmentalPhenotypeRefresh.Invalidations.Count+"; materials="+WaylithPhenotypeShader.MaterialCount);
                return;
            }
            Require(inventory.Count == (organGarden ? 36 : formGarden ? 36 : catalogGarden ? 108 : cultivarGarden ? 54 : creativeGarden ? 90 : 72) && inventory.Count(r=>r.Cultivar != null)==(organGarden ? 30 : formGarden ? 30 : catalogGarden ? 90 : cultivarGarden ? 45 : creativeGarden ? 75 : 60) && (catalogGarden || chosen.Count == (organGarden ? 6 : (creativeGarden || cultivarGarden || formGarden) ? 3 : 12)),"complete garden inventory");
            if (organGarden)
            {
                File.WriteAllLines(Path.Combine(root,"HNS-garden-organ-preparation.tsv"),new[]{"Species\tVariation\tLabel\tTexture\tWidth\tHeight\tReproductive parts\tStem parts\tReproductive pixels\tStem pixels"}.Concat(OrganSpriteExperiment.Preparation));
                var styled=inventory.Where(r=>r.Cultivar!=null).ToList();
                Require(styled.All(r=>OrganSpriteExperiment.PrintedPlants.Contains(r.Plant.thingIDNumber)||OrganSpriteExperiment.FallbackPlants.Contains(r.Plant.thingIDNumber)),"all 30 cases explicitly accounted for");
                Require(inventory.Where(r=>r.Cultivar==null).All(r=>Backend(r.Plant)=="Vanilla"),"six untouched controls");
                Require(styled.Count(r=>r.Panel==1 && r.Column<6 && OrganSpriteExperiment.PrintedPlants.Contains(r.Plant.thingIDNumber))==12,"all twelve core crop reproductive variants printed");
                Require(WaylithPhenotypeShader.MaterialCount==0 && WaylithPhenotypeShader.MaskCount==0,"no custom shader or GPU mask preparation");
                Log.Message("[HNS Organ] rendered="+styled.Count(r=>OrganSpriteExperiment.PrintedPlants.Contains(r.Plant.thingIDNumber))+"; fallback="+styled.Count(r=>!OrganSpriteExperiment.PrintedPlants.Contains(r.Plant.thingIDNumber))+"; prepared textures="+OrganSpriteExperiment.PreparedCount+"; stem="+styled.Count(r=>r.Column==6 && OrganSpriteExperiment.PrintedPlants.Contains(r.Plant.thingIDNumber)));
                return;
            }
            if (catalogGarden)
            {
                // Geometry-only traits need no texture treatment. For actual
                // shader motifs require a GPU print, not merely a placed plant.
                var patternCases = inventory.Where(r=>r.Cultivar?.traits.Any(t=>TraitAppearanceCatalog.TryPattern(t,out _,out _)) == true).ToList();
                Require(patternCases.All(r=>WaylithPhenotypeShader.ProceduralPlants.Contains(r.Plant.thingIDNumber)), "every motif case reached the procedural GPU");
                Require(inventory.Where(r=>r.Cultivar == null).All(r=>Backend(r.Plant)=="Vanilla"), "all controls vanilla");
                Log.Message("[HNS Garden] procedural cases=" + patternCases.Count + "; materials=" + WaylithPhenotypeShader.MaterialCount);
            }
            if (formGarden)
            {
                Require(inventory.Where(r=>r.Cultivar!=null).All(r=>PlantFormMesh.PrintedPlants.Contains(r.Plant.thingIDNumber)), "all 30 cultivars emitted deformed meshes");
                Require(inventory.Where(r=>r.Cultivar==null).All(r=>!PlantFormMesh.PrintedPlants.Contains(r.Plant.thingIDNumber) && Backend(r.Plant)=="Vanilla"), "six unchanged controls");
                Require(inventory.Where(r=>r.Cultivar!=null && r.Panel==1).All(r=>Backend(r.Plant)=="Geometry/effects only"), "15 form-only cultivars use original material path");
                Require(inventory.Where(r=>r.Cultivar!=null && r.Panel==2).All(r=>Backend(r.Plant)=="GPU procedural, no mask"), "15 combined cultivars retain GPU pigmentation");
                Require(WaylithPhenotypeShader.MaskCount==0 && PlantFormMesh.Meshes>=30 && PlantFormMesh.Vertices==PlantFormMesh.Meshes*25 && PlantFormMesh.Triangles==PlantFormMesh.Meshes*32,"bounded grid evidence");
                Log.Message("[HNS Form] mesh prints="+PlantFormMesh.Meshes+"; vertices="+PlantFormMesh.Vertices+"; triangles="+PlantFormMesh.Triangles+"; unique deformed plants="+PlantFormMesh.PrintedPlants.Count);
                return;
            }
            Require(inventory.Where(r=>r.Cultivar != null && (!catalogGarden || legacy.Contains(r.Plant.thingIDNumber) || WaylithPhenotypeShader.PrintedPlants.Contains(r.Plant.thingIDNumber))).All(r=>maskSources.TryGetValue(r.Plant.thingIDNumber, out var sources) && sources.All(x=>x.EndsWith(":local") || x.EndsWith(":bundled") || x.EndsWith(":none"))), "automatic-only provenance for all cultivars");
            if (patternGarden)
            {
                Require(inventory.Where(r => r.Cultivar != null).All(r => Backend(r.Plant) == "GPU procedural, no mask"), "every pattern cultivar printed on mask-free GPU path");
                Require(WaylithPhenotypeShader.MaskCount == 0, "no prepared masks in pattern garden");
                if (creativeGarden || cultivarGarden)
                {
                    Require(inventory.Where(r=>r.Cultivar != null).GroupBy(r=>r.Cultivar.traits.Single().proceduralPhenotype).Count() == (cultivarGarden ? 15 : 25), "all botanical motifs sampled");
                    Require(inventory.Where(r=>r.Cultivar != null).GroupBy(r=>r.Cultivar.traits.Single().proceduralPhenotype).All(g=>g.Count()==3 && g.Select(r=>r.Plant.def).Distinct().Count()==3), "each motif on all three species");
                }
            }
            Log.Message("[HNS Garden] renderer counts: " + string.Join("; ",inventory.GroupBy(r=>Backend(r.Plant)).Select(g=>g.Key+"="+g.Count())));
        }
        private static void Require(bool value, string label) { if (!value) throw new InvalidOperationException(label); }
        private static void End(string status, string message)
        {
            active = false;
            WaylithPhenotypeShader.Enabled = false;
            PlantFormMesh.Enabled = false;
            OrganSpriteExperiment.Enabled = false;
            WindHabitUtility.Enabled=false;
            SurfaceFinishUtility.Enabled=false;
            BotanicalDetailExperiment.Enabled=false;
            CanopyArchitectureExperiment.Enabled=false;
            if(architectureGarden)Prefs.PlantWindSway=previousArchitectureWind;
            if(detailGarden)Prefs.PlantWindSway=previousDetailWind;
            if(surfaceGarden)Prefs.PlantWindSway=previousSurfaceWind;
            if(windGarden) Prefs.PlantWindSway=previousWindPref;
            if (Find.TickManager != null) Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
            finish(status,message);
        }
    }
}
#endif
