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
        private static int notesCaptureAge;
        private static bool notesOpened;
        private static bool active;
        private static bool patternGarden, catalogGarden, creativeGarden, cultivarGarden, formGarden, organGarden, developmentGarden, windGarden, surfaceGarden, detailGarden, architectureGarden, poseGarden, groundGarden;
        private static readonly string[] GroundTraits={"Rootflare","RunnerHabit","BasalCarpet","LeafLitter","PetalDrift"};
        private static bool previousGroundWind;
        private static bool articulationGarden,mosaicGarden,neighborhoodGarden,chronicleGarden,vascularGarden,lifeSuiteGarden,sculptureGarden,forgeGarden,expressionGarden;
        private static readonly string[] ForgeTraits={"BanyanProps","CandelabraTips","StolonDaughters","BraidedLeaders","WingedPetioles","PitcherLeaves","HoodedSpathes","EpiphyllousBloom","ScalePhyllotaxis","SerratedLamina","DehiscentCapsules","WingedSamaras","AntherEmergence","PetaloidDoubling","SeedShedding","PrismaticLamina","RollingLamina","AmberResin","ExfoliatingBark","SelfShadowLace"};
        private static readonly string[] SculptureTraits={"CircinateFrond","ClimbingHelix","UmbelCrown","PendantRaceme","CrestedFan"};
        private static readonly string[] LifeSuiteTraits={"PhotosyntheticPulse","NyctinasticFolding","HydraulicWilt","CrystalDewLensing","PollinationCloud","MosaicOrganism","SeasonalColorFront","SubsurfaceRootHalo","LeafSkeletonization","BioluminescentSignaling","WindDifferentialAnatomy","FractalVenationBloom","TranslucentPetalTissue","CanopyVolumeParallax","MicroHabitatResponse","FruitRipeningCascade","FrostEtching","MycelialRelay","MorphogeneticReconfiguration","EventDrivenExpression"};
        private static readonly string[] VascularTraits={"IvoryNerves","CrimsonNetwork","LivingGlass","VascularChimera","BronzeReticulum"};
        private static readonly string[] ChronicleTraits={"CrookedChronicle","RingedHabit","AsymmetricWhorls","ReleaseCrown","StretchBands"};
        private static int chronicleSampledPanel=-1;
        private static readonly string[] NeighborhoodTraits={"GapSeeker","CompanionBow","CrownPartition","StandSpire","FreeRosette"};
        private static readonly List<Plant> NeighborhoodCompanions=new();
        private static int neighborhoodSampledPanel=-1;
        private static bool previousNeighborhoodWind;
        private static readonly string[] MosaicTraits={"SuccessionBloom","SectorSport","BiformHarvest","SisterBuds","RipeningMosaic"};
        private static int mosaicReprintPanel=-1;
        private static readonly string[] ArticulationTraits={"DawnPetals","DuskPetals","RainPendant","SolarTilt","SilkFlutter"};
        private const int PoseFrames=5;
        private static readonly string[] PoseTraits={"Nightfold","Sunseeker","Rainbowed","Nightbloom","Raincup"};
        private static readonly string[] PoseCrops={"Plant_Corn","Plant_Cotton","Plant_Daylily"};
        private static readonly List<string> PoseEvidence=new();
        private static readonly Dictionary<int,int> PoseInitialIDs=new();
        private static readonly Dictionary<int,Vector4> PoseInitialControls=new();
        private static int poseSampledPanel=-1,poseInitialMaterials;
        private static bool previousPoseWind;
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
        private static int PanelCount => expressionGarden?6:forgeGarden?36:sculptureGarden?9:lifeSuiteGarden?8:chronicleGarden||neighborhoodGarden?5:groundGarden ? 2 : poseGarden ? PoseFrames : architectureGarden ? 2 : detailGarden ? 3 : surfaceGarden ? SurfaceFrames : windGarden ? WindFrames : developmentGarden ? 3 : organGarden ? 2 : formGarden ? 2 : catalogGarden ? 6 : cultivarGarden ? 3 : creativeGarden ? 5 : SpeciesOptions.Length;
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
            Require(all.Where(t=>!t.configRoot && !t.generated && !ProceduralPhenotypeUtility.IsCreativeMode(t.proceduralPhenotype) && !ProceduralPhenotypeUtility.IsCultivarMode(t.proceduralPhenotype) && t.plantForm == PlantFormKind.None && t.organForm == OrganFormKind.None && t.windHabit == WindHabitKind.None && t.surfaceFinish == SurfaceFinishKind.None && t.botanicalDetail==BotanicalDetailKind.None && t.canopyArchitecture==CanopyArchitectureKind.None && t.responsiveHabit==ResponsiveHabitKind.None && t.groundHabit==GroundHabitKind.None && !DevelopmentalPhenotype.IsMode((int)t.proceduralPhenotype)).All(singles.Contains), "every baseline authored concrete trait has an individual fixture; creative candidates use CreativeGarden");
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

        internal static void Begin(string path, Action<string> stage, Action<string,string> completed, bool procedural = false, bool catalog = false, bool creative = false, bool cultivar = false, bool form = false, bool organ = false, bool development = false, bool wind = false, bool surface = false, bool detail = false, bool architecture = false,bool pose = false,bool ground = false,bool articulation = false,bool mosaic = false,bool neighborhood=false,bool chronicle=false,bool vascular=false,bool lifeSuite=false)
        {
            patternGarden = procedural || creative || cultivar || form || organ || development || wind || surface || detail || architecture || pose || ground; catalogGarden = catalog; creativeGarden = creative; cultivarGarden = cultivar; formGarden = form; organGarden = organ; developmentGarden = development; windGarden = wind; surfaceGarden=surface; detailGarden=detail; architectureGarden=architecture;poseGarden=pose;groundGarden=ground;articulationGarden=articulation;mosaicGarden=mosaic;neighborhoodGarden=neighborhood;chronicleGarden=chronicle;vascularGarden=vascular;lifeSuiteGarden=lifeSuite;
            root = path; onStage = stage; finish = completed;
            try
            {
                map = Find.CurrentMap;
                Require(map != null, "current map");
                WaylithPhenotypeShader.Enabled = !organGarden && !architectureGarden && !poseGarden && !groundGarden && !neighborhoodGarden && !chronicleGarden;
                PlantFormMesh.Enabled = formGarden;
                NeighborhoodExperiment.Enabled=neighborhoodGarden;
                ChronicleExperiment.Enabled=chronicleGarden;
                if(neighborhoodGarden||chronicleGarden){previousNeighborhoodWind=Prefs.PlantWindSway;Prefs.PlantWindSway=false;}
                OrganSpriteExperiment.Enabled = organGarden;
                WindHabitUtility.Enabled=windGarden;
                SurfaceFinishUtility.Enabled=surfaceGarden;
                expressionGarden=detailGarden&&Environment.GetEnvironmentVariable("HNS_NATIVE_EXPRESSION_GARDEN")=="1";
                Environment.SetEnvironmentVariable("HNS_NATIVE_EXPRESSION_GARDEN",null);
                BotanicalExpressionEngine.Enabled=expressionGarden;
                BotanicalDetailExperiment.Enabled=detailGarden&&!expressionGarden;
                VascularExperiment.Enabled=vascularGarden;
                LifeSuiteExperiment.Enabled=lifeSuiteGarden;
                sculptureGarden=lifeSuiteGarden&&Environment.GetEnvironmentVariable("HNS_NATIVE_SCULPTURE_GARDEN")=="1";
                Environment.SetEnvironmentVariable("HNS_NATIVE_SCULPTURE_GARDEN",null);
                forgeGarden=sculptureGarden&&Environment.GetEnvironmentVariable("HNS_NATIVE_FORGE_GARDEN")=="1";
                Environment.SetEnvironmentVariable("HNS_NATIVE_FORGE_GARDEN",null);
                LifeSuiteExperiment.ForgeEnabled=forgeGarden;LifeSuiteExperiment.SculptureEnabled=sculptureGarden;LifeSuiteExperiment.SculpturePhase=0;
                lifeSuiteMotion=lifeSuiteGarden&&Environment.GetEnvironmentVariable("HNS_NATIVE_LIFE_MOTION")=="1";
                Environment.SetEnvironmentVariable("HNS_NATIVE_LIFE_MOTION",null);
                CanopyArchitectureExperiment.Enabled=architectureGarden;
                ResponsiveHabitExperiment.Enabled=poseGarden;
                ResponsiveHabitExperiment.ArticulationEnabled=articulationGarden;
                ResponsiveHabitExperiment.MosaicEnabled=mosaicGarden;
                GroundHabitExperiment.Enabled=groundGarden;
                if(groundGarden)
                {
                    previousGroundWind=Prefs.PlantWindSway;Prefs.PlantWindSway=false;
                    var defs=DefDatabase<VarietyTraitDef>.AllDefsListForReading.Where(t=>GroundHabitMath.Valid(t.groundHabit)).ToList();
                    Require(defs.Count==5&&defs.All(t=>t.commonality==0f&&TraitAppearanceCatalog.TryGet(t,out _)),"five neutral ground alleles");
                    Require(TraitCatalogValidation.Validate(DefDatabase<VarietyTraitDef>.AllDefsListForReading).Count==0,"trait catalog validation");
                }
                if(poseGarden)
                {
                    previousPoseWind=Prefs.PlantWindSway;Prefs.PlantWindSway=articulationGarden;
                    var defs=DefDatabase<VarietyTraitDef>.AllDefsListForReading.Where(t=>ResponsiveHabitExperiment.ModeMatches(t.responsiveHabit)).ToList();
                    Require(defs.Count==5&&defs.All(t=>t.commonality==0f&&TraitAppearanceCatalog.TryGet(t,out _)),"five neutral pose alleles");
                    Require(TraitCatalogValidation.Validate(DefDatabase<VarietyTraitDef>.AllDefsListForReading).Count==0,"trait catalog validation");
                    if(articulationGarden||mosaicGarden){ArticulationAtlas.DiagnosticRoot=root;VerifyArticulationAtlasPixels();}
                    PoseEvidence.Add("Panel\tSpecies\tColumn\tPlant ID\tTrait\tLocal hour\tWeather\tSky glow\tRain rate\tFlipped\tHeight\tContraction\tLean\tDroop\tMaterial ID");
                }
                if(architectureGarden)
                {
                    CanopyArchitectureExperiment.DiagnosticRoot=root;
                    previousArchitectureWind=Prefs.PlantWindSway;Prefs.PlantWindSway=false;
                    var defs=DefDatabase<VarietyTraitDef>.AllDefsListForReading.Where(t=>CanopyArchitectureMath.Valid(t.canopyArchitecture)).ToList();
                    Require(defs.Count==5 && defs.All(t=>t.commonality==0f && TraitAppearanceCatalog.TryGet(t,out _)),"five neutral architecture alleles");
                    Require(TraitCatalogValidation.Validate(DefDatabase<VarietyTraitDef>.AllDefsListForReading).Count==0,"trait catalog validation");
                }
                if(detailGarden)
                {
                    previousDetailWind=Prefs.PlantWindSway;Prefs.PlantWindSway=lifeSuiteGarden;
                    var defs=DefDatabase<VarietyTraitDef>.AllDefsListForReading.Where(t=>BotanicalDetailMath.Valid(t.botanicalDetail)&&BotanicalDetailExperiment.ModeMatches(t.botanicalDetail)).ToList();
                    Require(expressionGarden||defs.Count==(forgeGarden||lifeSuiteGarden&&!sculptureGarden?20:5) && defs.All(t=>t.commonality==0f && TraitAppearanceCatalog.TryGet(t,out _)),"neutral detail alleles");
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
                    var defs=DefDatabase<VarietyTraitDef>.AllDefsListForReading.Where(t=>t.plantForm!=PlantFormKind.None&&PlantFormMesh.ModeMatches(t.plantForm)).ToList();
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
                if (creativeGarden || cultivarGarden || formGarden || organGarden || developmentGarden || windGarden || surfaceGarden || detailGarden || architectureGarden || poseGarden || groundGarden)
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
                if(sculptureGarden)SetSculpturePhase();else if(lifeSuiteGarden)SetLifeSuiteTime();else if(detailGarden)SetDetailTime();
                if(poseGarden)SetPoseWeather();
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
            if (groundGarden) return col==0 ? new() : new() { Trait(GroundTraits[col-1]) };
            if (mosaicGarden) return col==0 ? new() : new() { Trait(MosaicTraits[panel]) };
            if (poseGarden) return col==0 ? new() : new() { Trait((articulationGarden?ArticulationTraits:PoseTraits)[col-1]) };
            if (architectureGarden) return col==0 ? new() : new() { Trait(ArchitectureTraits[col-1]) };
            if(expressionGarden)return col==0?new():ExpressionMath.Recipe(row%3,col<=3?0:col-3).Select(Trait).ToList();
            if(forgeGarden)return col==0?new():new(){Trait(ForgeTraits[(panel/9)*5+col-1])};
            if(sculptureGarden)return col==0?new():new(){Trait(SculptureTraits[col-1])};
            if(lifeSuiteGarden)return col==0?new():new(){Trait(LifeSuiteTraits[(panel/2)*5+col-1])};
            if (detailGarden) return col==0 ? new() : new() { Trait((vascularGarden?VascularTraits:DetailTraits)[col-1]) };
            if (surfaceGarden) return col==0 ? new() : new() { Trait(SurfaceTraits[col-1]) };
            if (windGarden) return col==0 ? new() : new() { Trait(WindTraits[col-1]) };
            if (developmentGarden) return col==0 ? new() : new() { Trait(DevelopmentTraits[col-1]) };
            if (organGarden) return col==0 ? new() : new() { Trait(OrganTraits[col-1]) };
            if(chronicleGarden)return col==0?new():new(){Trait(ChronicleTraits[col-1])};
            if(neighborhoodGarden)return col==0?new():new(){Trait(NeighborhoodTraits[col-1])};
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
                var def = (poseGarden||neighborhoodGarden||chronicleGarden||lifeSuiteGarden||expressionGarden) ? DefDatabase<ThingDef>.GetNamed(PoseCrops[r]) : (groundGarden || architectureGarden || organGarden) ? Choose(OrganCrops[panel*3+r]) : (creativeGarden || cultivarGarden || formGarden || developmentGarden || windGarden || surfaceGarden || detailGarden) ? DefDatabase<ThingDef>.GetNamed(CreativeCrops[r]) : catalogGarden ? DefDatabase<ThingDef>.GetNamed(CatalogCrops[panel*3+r]) : Choose(patternGarden && panel == 3 && r == 1 ? "Plant_Haygrass" : SpeciesOptions[panel][r]);
                Require(HorticulturePlantPolicy.IsSupported(def), "supported garden crop");
                if (catalogGarden || creativeGarden || cultivarGarden || formGarden || developmentGarden || windGarden || surfaceGarden || detailGarden || poseGarden) chosen.Add(def);
                // Prewarm actual resolved masks before rendering. If shared
                // manual or seasonal materials cannot use the experimental
                // backend, their existing CPU path is intentionally retained.
                if(articulationGarden||mosaicGarden)
                    for(int v=0;v<PlantMaskUtility.VariationCount(def);v++) ArticulationAtlas.Prepare(def,v);
                if(groundGarden)
                    for(int v=0;v<PlantMaskUtility.VariationCount(def);v++) GroundHabitExperiment.Prepare(def,v);
                if(architectureGarden)
                    for(int v=0;v<PlantMaskUtility.VariationCount(def);v++) CanopyArchitectureExperiment.Prepare(def,v);
                if(expressionGarden)for(int variant=0;variant<3;variant++)BotanicalExpressionEngine.Prepare(def,ExpressionMath.Recipe(r,variant));
                else if(detailGarden)
                    for(int v=0;v<PlantMaskUtility.VariationCount(def);v++) BotanicalDetailExperiment.Prepare(def,v);
                if (organGarden)
                    for(int v=0;v<PlantMaskUtility.VariationCount(def);v++) OrganSpriteExperiment.Prepare(def,v);
                if (!patternGarden)
                    for (int v = 0; v < PlantMaskUtility.VariationCount(def); v++)
                        WaylithPhenotypeShader.PrepareMask(def, v);
                VarietyRecord sharedCultivar=null;
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
                        cultivar = (mosaicGarden||expressionGarden&&c<=3)&&sharedCultivar!=null?sharedCultivar:registry.UnlockVariety(def, traits, "Garden " + def.label + " " + c, originKind:"debug-visual-fixture");
                        if(mosaicGarden||expressionGarden&&c<=3)sharedCultivar=cultivar;
                        Require(cultivar != null && cultivar.traits.Count == traits.Count, "registered cultivar");
                        comp.SetVariety(cultivar);
                        Require(comp.Variety == cultivar && comp.ActiveTraits.Count == traits.Count, "assigned real genetics");
                    }
                    else comp.SetVariety(null);
                    plant.sown = true; plant.Growth = expressionGarden?.25f:!sculptureGarden&&lifeSuiteGarden&&panel==6?.72f:chronicleGarden?.25f:developmentGarden ? DevelopmentGrowth[0] : 1f;
                    plant.HitPoints = plant.MaxHitPoints;
                    // Row A on top, columns left-to-right.
                    GenSpawn.Spawn(plant, start + new IntVec3(c * 3,0,(2-r)*4), map);
                    Require(plant.Spawned, "spawned garden plant");
                    current.Add(plant); inventory.Add(new Row(panel + 1,r + 1,c + 1,plant,cultivar));
                    plant.DirtyMapMesh(map);
                }
            }
            if(expressionGarden)AdvanceExpressionGrowth();
            if(chronicleGarden)GrowChronicle();
            if(lifeSuiteGarden&&!sculptureGarden)ApplyLifeSuiteContext();
            Log.Message("[HNS Garden] panel=" + (panel+1) + "; species=" + string.Join(", ",current.Select(p=>p.def.defName).Distinct()));
        }

        private static readonly List<string> DescriptionEvidence=new(){"Panel\tSpecies\tColumn\tPlant ID\tCultivar\tFacts\tIndividual phenotype\tBotanical observations\tRecorded history\tCultivar potential"};
        private static void SampleDescriptions()
        {
            foreach(var r in inventory.Where(r=>r.Panel==panel+1))
            {
                var c=r.Plant.TryGetComp<CompPlantVariety>();
                var o=c.VisualObservation;var fs=o?.Facts()??Array.Empty<PhenotypeVocabulary.Fact>();
                string prose=o?.Prose()??"",detail=o?.Detail()??"",history=PhenotypeObservation.History(c);
                var policy=HorticulturePresentationPolicy.ForCultivar(c.Variety);
                string potential=policy?.PotentialAppearance??"";
                if(r.Cultivar==null)Require(fs.Length==0&&history.Length==0&&potential.Length==0,"no fabricated observations or potential on controls");
                else
                {
                    Require(r.Plant.GetInspectTabs().OfType<ITab_PlantPhenotype>().Count()==1,"native phenotype tab registered exactly once");
                    Require(!c.CompGetGizmosExtra().OfType<Command_Action>().Any(g=>g.defaultLabel=="Field notes"),"no redundant field notes command");
                    Require(potential.Length>0||!formGarden,"documented cultivar potential");
                    string notes=PhenotypeObservation.Notes(c);
                    Require(notes.Contains("Individual phenotype")&&(prose.Length==0||notes.Contains(prose))&&(history.Length==0||notes.Contains(history)),"field notes content");
                    if(formGarden&&!neighborhoodGarden&&!chronicleGarden)
                    {
                        Require(fs.Any(f=>f.Region==PhenotypeVocabulary.Region.Habit&&f.Slot=="shape"),"actual deformed geometry described");
                        if(panel==1)Require(fs.Any(f=>f.Slot=="pigment"),"combined rendered pigment and form described");
                        else Require(!fs.Any(f=>f.Slot=="pigment"),"no invented pigment in pure-form case");
                    }
                    if(groundGarden)
                        Require(fs.Any(f=>f.Region==PhenotypeVocabulary.Region.Ground)==GroundHabitExperiment.PrintedPlants.ContainsKey(r.Plant.thingIDNumber),"ground expression follows actual renderer acceptance");
                    if(mosaicGarden)
                        Require(fs.Any(f=>f.Region==PhenotypeVocabulary.Region.Reproductive&&f.Slot=="pose"&&f.Phrase.Contains("native growth")),"actual mosaic metadata described");
                    if(chronicleGarden)
                    {
                        Require(history==PhenotypeVocabulary.History(c.VisualChronicle)&&history.Contains("Early growth"),"actual historical growth described");
                        if(panel==0)Require(!fs.Any(f=>f.Slot=="shape"),"open growth does not invent historical deformation");
                    }
                }
                DescriptionEvidence.Add(string.Join("\t",panel+1,r.Plant.def.defName,r.Column,r.Plant.thingIDNumber,r.Cultivar?.id??"Vanilla",fs.Length,prose,detail,history,potential));
            }
            File.WriteAllLines(Path.Combine(root,"HNS-garden-phenotype-language.tsv"),DescriptionEvidence);
            Log.Message("[HNS Language] panel="+(panel+1)+"; observation rows="+(DescriptionEvidence.Count-1));
        }

        private static int botanicalTabIndex,botanicalTabAge;
        private static bool botanicalTabsStarted;
        private static readonly Type[] BotanicalTabs={typeof(ITab_PlantVariety),typeof(ITab_PlantEcology),typeof(ITab_PlantHarvest)};
        private static readonly string[] BotanicalNames={"cultivar","ecology","harvest"};
        private static readonly List<string> BotanicalEvidence=new(){"Tab\tPlant ID\tVerified live data"};
        private static bool CaptureBotanicalTabs(MainTabWindow_Inspect inspector)
        {
            var p=Find.Selector.SingleSelectedThing as Plant;
            Require(p!=null&&p.Spawned,"native botanical inspector selected plant");
            var c=p.TryGetComp<CompPlantVariety>();
            var a=HorticulturePresentationPolicy.ForCultivar(c.Variety);
            Require(a!=null&&!c.PendingDiscovery,"documented native selected specimen");
            if(!botanicalTabsStarted)
            {
                foreach(var t in BotanicalTabs.Append(typeof(ITab_PlantPhenotype)))
                    Require(inspector.CurTabs.Count(tab=>tab.GetType()==t&&tab.IsVisible)==1,"exactly one visible "+t.Name);
                // Read models without harvesting or altering the selection.
                var sample=inventory.Where(r=>r.Panel==panel+1&&r.Cultivar==null&&r.Plant.def==p.def).First().Plant;
                var sc=sample.TryGetComp<CompPlantVariety>();
                var vf=PlantInspectionFacts.ReadHarvest(sample,sc,null);
                Require(vf.Known&&vf.Factor==1f&&vf.Estimate==vf.Base,"ordinary plant species estimate");
                // A detached specimen exercises the pending authority gate.
                // No game output, random rounding or new persistent state.
                var probe=(Plant)ThingMaker.MakeThing(p.def);
                var pc=probe.TryGetComp<CompPlantVariety>();
                pc.SetPendingTraits(new(){Trait("HighYield")});probe.Growth=1f;probe.HitPoints=probe.MaxHitPoints;
                var pf=PlantInspectionFacts.ReadHarvest(probe,pc,null);
                Require(pc.PendingDiscovery&&!pf.Known&&pf.Factor==1f&&pf.Estimate==pf.Base&&pf.SeedEligible,"pending traits withheld from estimate; seed eligibility derived");
                probe.Growth=.01f;
                var young=PlantInspectionFacts.ReadHarvest(probe,pc,null);
                Require(!young.Ready&&young.Estimate==0&&!young.SeedEligible,"immature crop and seed recovery not ready");
                probe.Destroy(DestroyMode.Vanish);
                BotanicalEvidence.Add("Authority checks\t"+p.thingIDNumber+"\tVanilla estimate, pending yield factor withheld, mature seed eligibility, immature no yield");
                botanicalTabsStarted=true;
            }
            if(botanicalTabIndex>=BotanicalTabs.Length)return true;
            var type=BotanicalTabs[botanicalTabIndex];string path=Path.Combine(root,"HNS-garden-"+BotanicalNames[botanicalTabIndex]+"-tab.png");
            if(botanicalTabAge==0)
            {
                if(chronicleGarden&&type==typeof(ITab_PlantEcology))PlaceCompanions(new(1,0));
                if(chronicleGarden&&type==typeof(ITab_PlantHarvest))PlaceCompanions(default);
                inspector.OpenTabType=type;botanicalTabAge=1;return false;
            }
            if(++botanicalTabAge==15)
            {
                int id=p.thingIDNumber;
                if(type==typeof(ITab_PlantVariety))
                {
                    Require(ITab_PlantVariety.LastDrawn==id&&ITab_PlantVariety.LastAuthorized&&ITab_PlantVariety.LastTraits==a.AuthorizedTraits.Count&&ITab_PlantVariety.LastParents==a.Parents.Count,"actual cultivar tab repaint with authorized traits/parents");
                    BotanicalEvidence.Add("Cultivar\t"+id+"\tTraits="+a.AuthorizedTraits.Count+"; documented parents="+a.Parents.Count);
                }
                else if(type==typeof(ITab_PlantEcology))
                {
                    var f=ITab_PlantEcology.LastFacts;var actual=PlantInspectionFacts.ReadEcology(p);
                    Require(ITab_PlantEcology.LastDrawn==id&&Math.Abs(f.Fertility-actual.Fertility)<.001f&&Math.Abs(f.Glow-actual.Glow)<.001f&&f.Pressure==actual.Pressure&&f.Occupied==actual.Occupied,"actual ecological tab repaint with live map conditions");
                    if(chronicleGarden)Require(f.Pressure==new NeighborhoodMath.Pressure(1,0)&&f.Occupied==1,"real west companion and open east");
                    BotanicalEvidence.Add("Ecology\t"+id+"\tFertility="+f.Fertility+"; glow="+f.Glow+"; temperature="+f.Temperature+"; growth="+f.GrowthRate+"; west="+f.Pressure.Left+"; east="+f.Pressure.Right+"; occupied="+f.Occupied);
                }
                else
                {
                    var f=ITab_PlantHarvest.LastFacts;
                    Require(ITab_PlantHarvest.LastDrawn==id&&ITab_PlantHarvest.LastAuthorized&&f==PlantInspectionFacts.ReadHarvest(p,c,a)&&f.Ready==p.CanYieldNow(),"actual harvest tab repaint with authorized read-only estimate");
                    BotanicalEvidence.Add("Harvest\t"+id+"\tReady="+f.Ready+"; species expectation="+f.Base+"; estimated="+f.Estimate+"; known factor="+f.Factor+"; seed eligible="+f.SeedEligible);
                }
                ScreenCapture.CaptureScreenshot(path);return false;
            }
            if(botanicalTabAge<30||!File.Exists(path)||new FileInfo(path).Length<1000)return false;
            botanicalTabIndex++;botanicalTabAge=0;
            File.WriteAllLines(Path.Combine(root,"HNS-garden-botanical-tabs.tsv"),BotanicalEvidence);
            return botanicalTabIndex>=BotanicalTabs.Length;
        }

        internal static void Update()
        {
            if (!active) return;
            try
            {
                frames++;
                if (Find.WindowStack.TryRemove(typeof(EditWindow_Log), false)) { frames = 0; return; }
                if(chronicleGarden&&chronicleSampledPanel!=panel&&frames>=100)
                {SampleChronicle();chronicleSampledPanel=panel;frames=92;return;}
                if(neighborhoodGarden&&neighborhoodSampledPanel!=panel&&frames>=100)
                {SampleNeighborhood();neighborhoodSampledPanel=panel;frames=92;return;}
                if(mosaicGarden&&mosaicReprintPanel!=panel&&frames>=100&&inventory.Where(r=>r.Panel==panel+1&&r.Cultivar!=null).All(r=>PlantFormMesh.MosaicPrints.ContainsKey(r.Plant.thingIDNumber)))
                {
                    foreach(var plant in current)plant.DirtyMapMesh(map);
                    mosaicReprintPanel=panel;frames=85;return;
                }
                if(poseGarden && poseSampledPanel!=panel && frames>=100 && inventory.Where(r=>r.Panel==(mosaicGarden?panel+1:1)&&r.Cultivar!=null).All(r=>ResponsiveHabitExperiment.PrintedPlants.ContainsKey(r.Plant.thingIDNumber)||(articulationGarden&&custom.Contains(r.Plant.thingIDNumber))))
                {
                    if(mosaicGarden)SampleMosaic();else SamplePose();poseSampledPanel=panel;frames=92;return;
                }
                if(detailGarden && detailSampledPanel!=panel && frames>=100 && inventory.Where(r=>r.Panel==1&&r.Cultivar?.traits.Any(t=>t.botanicalDetail==BotanicalDetailKind.LanternBuds)==true).All(r=>BotanicalDetailExperiment.GlowPlants.ContainsKey(r.Plant.thingIDNumber)))
                {
                    if(expressionGarden)SampleExpression();else if(sculptureGarden)SampleSculpture();else if(lifeSuiteGarden)SampleLifeSuite();else if(vascularGarden)SampleVascular();else SampleDetail();detailSampledPanel=panel;frames=92;return;
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
                if (capture == null && frames >= 100 && (!windGarden || windSampledPanel==panel) && (!surfaceGarden || surfaceSampledPanel==panel) && (!detailGarden||detailSampledPanel==panel) && (!poseGarden||poseSampledPanel==panel) && (!neighborhoodGarden||neighborhoodSampledPanel==panel) && (!chronicleGarden||chronicleSampledPanel==panel) && current.All(p => custom.Contains(p.thingIDNumber) || vanilla.Contains(p.thingIDNumber)) &&
                    (!developmentGarden || inventory.Where(r=>r.Panel==panel+1 && r.Cultivar!=null).All(r=>WaylithPhenotypeShader.DevelopmentalPrints.Contains((r.Plant.thingIDNumber,panel)))))
                {
                    SampleDescriptions();
                    capture = Path.Combine(root, "HNS-garden-panel-" + (panel+1) + ".png");
                    Log.Message("[HNS Garden] screenshot " + (panel+1) + "; local hour=" + GenLocalDate.HourOfDay(map) +
                        "; sky glow=" + map.skyManager.CurSkyGlow + "; shader materials=" + WaylithPhenotypeShader.MaterialCount +
                        "; prepared masks=" + WaylithPhenotypeShader.MaskCount);
                    if(expressionGarden)capture=CaptureExpressionGpu();
                    else if(sculptureGarden)capture=CaptureSculptureGpu();
                    else ScreenCapture.CaptureScreenshot(capture);
                }
                if (capture != null && File.Exists(capture) && new FileInfo(capture).Length > (sculptureGarden||expressionGarden?500:1000) && ++captureAge >= 30)
                {
                    if(panel==PanelCount-1&&(formGarden||mosaicGarden||groundGarden))
                    {
                        string notesPath=Path.Combine(root,"HNS-garden-field-notes.png");
                        if(!notesOpened)
                        {
                            var subject=inventory.Where(r=>r.Panel==panel+1&&r.Cultivar!=null)
                                .OrderByDescending(r=>r.Plant.TryGetComp<CompPlantVariety>().VisualObservation?.Prose().Length??0).First();
                            var c=subject.Plant.TryGetComp<CompPlantVariety>();
                            Find.Selector.ClearSelection();
                            Find.Selector.Select(subject.Plant);
                            Find.MainTabsRoot.SetCurrentTab(MainButtonDefOf.Inspect,false);
                            var inspector=(MainTabWindow_Inspect)MainButtonDefOf.Inspect.TabWindow;
                            var tab=inspector.CurTabs.OfType<ITab_PlantPhenotype>().Single();
                            Require(tab.IsVisible,"native phenotype tab visible for selected individual");
                            inspector.OpenTabType=typeof(ITab_PlantPhenotype);
                            notesOpened=true;notesCaptureAge=0;
                            ITab_PlantPhenotype.LastDrawnId=-1;
                            File.WriteAllText(Path.Combine(root,"HNS-garden-field-notes.txt"),subject.Plant.LabelCap+"\n\n"+PhenotypeObservation.Notes(c));
                            return;
                        }
                        if(++notesCaptureAge==15)
                        {
                            var selected=Find.Selector.SingleSelectedThing as Plant;
                            var c=selected?.TryGetComp<CompPlantVariety>();
                            Require(selected!=null&&ITab_PlantPhenotype.LastDrawnId==selected.thingIDNumber&&ITab_PlantPhenotype.LastFactCount==(c.VisualObservation?.Facts().Length??0)&&ITab_PlantPhenotype.LastHistorySamples==ChronicleMath.Count(c.VisualChronicle)&&ITab_PlantPhenotype.LastPotentialVisible,"native tab actually repainted with specimen facts, growth history and authorized potential");
                            Log.Message("[HNS PhenotypeTab] rendered selected id="+selected.thingIDNumber+"; facts="+ITab_PlantPhenotype.LastFactCount+"; growth samples="+ITab_PlantPhenotype.LastHistorySamples);
                            ScreenCapture.CaptureScreenshot(notesPath);return;
                        }
                        if(notesCaptureAge<30||!File.Exists(notesPath)||new FileInfo(notesPath).Length<1000)return;
                        var nativeInspector=(MainTabWindow_Inspect)MainButtonDefOf.Inspect.TabWindow;
                        if(!CaptureBotanicalTabs(nativeInspector))return;
                        nativeInspector.CloseOpenTab();
                        Find.Selector.ClearSelection();
                    }
                    if(expressionGarden&&panel==PanelCount-1&&!CheckExpressionLab())return;
                    if(!CaptureLifeSuiteMotion())return;
                    lifeMotionFrame=0;lifeMotionAge=0;
                    onStage("GardenPanel" + (panel+1));
                    if (++panel == PanelCount)
                    {
                        WriteInventory();
                        onStage("GardenInventory");
                        End("PASS", expressionGarden ? "nine composed cultivar identities; six actual growth phases; native preview and stable individual variation" : forgeGarden ? "twenty botanical forge recipes; 540 native GPU phase previews" : sculptureGarden ? "five branch rigs; nine native GPU developmental previews and live material acceptance" : lifeSuiteGarden ? "20 experimental visual candidates; 4 paired gardens; actual renderer and layer acceptance" : vascularGarden ? "18 same plants; 45 live tissue render states; bounded anatomy maps; native day/night/day controls" : chronicleGarden ? "90 mature plants in identical empty plots; 75 historical morphologies; 225 actual growth-band observations; Scribe values" : neighborhoodGarden ? "18 same plants; five lateral neighbor states; native rooted mesh response and automatic invalidation" : mosaicGarden ? "90 plants; 75 same-cultivar instances; per-organ mosaics, native mesh seed and remesh stability" : articulationGarden ? "18 same plants; five native light/rain/wind states; local organ atlas material checks; explicit fallbacks" : groundGarden ? "36 plants; 26 ground detail planes, four explicit nonflower fallbacks; two daylight screenshots" : poseGarden ? "18 same plants; five actual light/rain states; 75 pose material checks and stable IDs" : architectureGarden ? "36 plants; 30 modular canopy cases on six species; two daylight screenshots; explicit fallback for unsupported modules" : detailGarden ? "18 same plants; 12 topology cases and three night overlays; day-night-day screenshots" : surfaceGarden ? "18 same mature plants; 15 surface finishes; four native lighting/weather states; 60 verified material states" : windGarden ? "18 same mature plants; 15 genetic wind materials; 12 actual wind-phase captures; 180 material-phase checks" : developmentGarden ? "18 same plants across three growth stages; 45 verified cultivar-stage GPU prints; 30 automatic mesh invalidations; three daylight screenshots" : organGarden ? "36 plants; 30 organ-form cases; eligible components composited; uncertain organs fall back; two daylight screenshots" : formGarden ? "36 plants; 30 genetically deformed cultivars; 15 pure morphology and 15 morphology plus pigmentation; two daylight screenshots" : cultivarGarden ? "54 plants; 15 organ-aware botanical candidates across three species; three daylight screenshots; 45 mask-free GPU cultivars" : creativeGarden ? "90 garden plants; 25 refined botanical traits across three species; five daylight screenshots; 75 mask-free GPU cultivars" : catalogGarden ? "108 garden plants; 90 baseline trait cases; six daylight screenshots; every loaded HNS appearance rule covered" : "72 garden plants, 60 registered cultivars, 12 species, four daylight screenshots");
                        return;
                    }
                    if(expressionGarden)
                    {
                        frames=0;captureAge=0;capture=null;
                        foreach(var row in inventory.Where(r=>r.Panel==1).ToArray())inventory.Add(row with {Panel=panel+1});
                        AdvanceExpressionGrowth();return;
                    }
                    if(neighborhoodGarden)
                    {
                        frames=0;captureAge=0;capture=null;
                        foreach(var row in inventory.Where(r=>r.Panel==1).ToArray())inventory.Add(row with {Panel=panel+1});
                        SetNeighborhood();return;
                    }
                    if(poseGarden&&!mosaicGarden)
                    {
                        frames=0;captureAge=0;capture=null;
                        foreach(var row in inventory.Where(r=>r.Panel==1).ToArray())inventory.Add(row with {Panel=panel+1});
                        SetPoseWeather();return;
                    }
                    if(sculptureGarden)
                    {
                        frames=0;captureAge=0;capture=null;
                        if(forgeGarden&&panel%9==0)
                        {
                            foreach(var p in current)if(p.Spawned)p.Destroy(DestroyMode.Vanish);
                            current.Clear();SetSculpturePhase();MakePanel();return;
                        }
                        int first=forgeGarden?(panel/9)*9+1:1;
                        foreach(var row in inventory.Where(r=>r.Panel==first).ToArray())inventory.Add(row with {Panel=panel+1});
                        SetSculpturePhase();return;
                    }
                    if(lifeSuiteGarden)
                    {
                        AdvanceLifeSuite();return;
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

        private static void SetPoseWeather()
        {
            float target=panel==3?0f:panel==0||panel==4?.375f:.625f;
            int advance=Mathf.RoundToInt(Mathf.Repeat(target-GenLocalDate.DayPercent(map),1f)*60000f);
            Find.TickManager.DebugSetTicksGame(Find.TickManager.TicksGame+advance);
            var weather=panel==2?DefDatabase<WeatherDef>.GetNamed("Rain"):WeatherDefOf.Clear;
            map.weatherManager.TransitionTo(weather);map.weatherManager.lastWeather=weather;map.weatherManager.curWeatherAge=4000;
            map.skyManager.SkyManagerUpdate();map.windManager.WindManagerTick();
        }
        private static readonly List<string> MosaicEvidence=new(){"Panel\tSpecies\tColumn\tPlant ID\tTrait\tNative mesh seed\tVariant frame\tMaterial ID\tPrint count\tSource atlas\tSame cultivar"};
        private static readonly List<string> NeighborhoodEvidence=new(){"Panel\tSpecies\tColumn\tPlant ID\tTrait\tLeft pressure\tRight pressure\tNative mesh prints\tNeighbor invalidations\tRoot fixed\tTip X shift\tTip height\tTop width\tGeometry changed"};
        private static readonly Dictionary<int,PlantFormMesh.NeighborhoodPrint> NeighborhoodInitial=new(),NeighborhoodPrevious=new();
        private static readonly Dictionary<int,int> NeighborhoodPreviousInvalidations=new();
        private static NeighborhoodMath.Pressure ExpectedNeighborhood()=>panel switch{1=>new(1,0),2=>new(0,1),3=>new(1,1),_=>default};
        private static void SetNeighborhood()=>PlaceCompanions(ExpectedNeighborhood());
        private static void PlaceCompanions(NeighborhoodMath.Pressure target)
        {
            foreach(var p in NeighborhoodCompanions)if(p.Spawned)p.Destroy(DestroyMode.Vanish);
            NeighborhoodCompanions.Clear();
            foreach(var subject in current)
                for(int dx=-1;dx<=1;dx+=2)
                {
                    if((dx<0?target.Left:target.Right)==0f)continue;
                    var cell=subject.Position+new IntVec3(dx,0,0);
                    Require(cell.GetPlant(map)==null&&ClearCell(cell),"clear companion position");
                    var neighbor=(Plant)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("Plant_Rice"));
                    neighbor.Growth=1f;neighbor.sown=true;GenSpawn.Spawn(neighbor,cell,map);
                    NeighborhoodCompanions.Add(neighbor);
                }
            Log.Message("[HNS Neighborhood] state="+(panel+1)+"; actual neighbors="+NeighborhoodCompanions.Count);
        }
        private static readonly int[][] ChronicleHistories={new[]{1,1,1},new[]{2,2,1},new[]{3,3,1},new[]{2,3,2},new[]{4,4,1}};
        private static readonly List<string> ChronicleEvidence=new(){"Panel\tSpecies\tColumn\tPlant ID\tCultivar ID\tTrait\tEarly\tMiddle\tLate\tPacked history\tSamples\tScribe parity\tRoot fixed\tLower shift\tMiddle shift\tUpper shift\tTip shift\tTip height\tLower width\tMiddle width\tTop width\tVertex error\tVertices\tMaterial ID"};
        private static readonly Dictionary<(ThingDef,PlantFormKind),string> ChronicleCultivars=new();
        private static int ExpectedChronicle()=>ChronicleHistories[panel][0]|(ChronicleHistories[panel][1]<<3)|(ChronicleHistories[panel][2]<<6);
        private static NeighborhoodMath.Pressure ChroniclePressure(int code)=>new(code==2||code==4?1f:0f,code==3||code==4?1f:0f);
        private static void GrowChronicle()
        {
            for(int i=0;i<3;i++)
            {
                var pressure=ChroniclePressure(ChronicleHistories[panel][i]);
                PlaceCompanions(pressure);
                foreach(var p in current)
                {
                    Require(NeighborhoodExperiment.Current(p)==pressure,"actual live neighbors at growth checkpoint");
                    p.Growth=i==0?.45f:i==1?.70f:1f;
                }
            }
            PlaceCompanions(default);
            // Simulate new neighbors after maturity, and remove again. No old
            // growth checkpoint may change, even if the growth setter runs.
            PlaceCompanions(new(1,1));
            foreach(var p in current){p.Growth=.99f;p.Growth=1f;}
            PlaceCompanions(default);
            foreach(var p in current)p.DirtyMapMesh(map);
        }
        private static bool ChronicleScribeParity(int expected,int id)
        {
            string path=Path.Combine(root,"HNS-chronicle-value-"+id+".xml");
            try
            {
                int value=expected;
                Scribe.saver.InitSaving(path,"HNSChronicleFixture");
                ChronicleExperiment.Expose(ref value);Scribe.saver.FinalizeSaving();
                value=0;Scribe.loader.InitLoading(path);ChronicleExperiment.Expose(ref value);Scribe.loader.FinalizeLoading();
                return value==expected;
            }
            catch{Scribe.ForceStop();throw;}
        }
        private static void SampleChronicle()
        {
            int code=ExpectedChronicle();var styled=inventory.Where(r=>r.Panel==panel+1&&r.Cultivar!=null).ToArray();
            Require(styled.Length==15&&NeighborhoodCompanions.Count==0&&current.All(p=>p.Growth==1f&&NeighborhoodExperiment.Current(p)==default),"all mature subjects in identical empty surroundings");
            foreach(var row in styled)
            {
                var p=row.Plant;int id=p.thingIDNumber;var kind=row.Cultivar.traits.Single().plantForm;
                var comp=p.TryGetComp<CompPlantVariety>();
                Require(comp.VisualChronicle==code&&ChronicleExperiment.Samples.TryGetValue(id,out int count)&&count==3,"three first-crossing growth observations retained after neighbor removal");
                var key=(p.def,kind);
                if(panel==0)ChronicleCultivars[key]=row.Cultivar.id;
                else Require(ChronicleCultivars[key]==row.Cultivar.id,"identical actual cultivar across histories");
                Require(PlantFormMesh.ChroniclePrints.TryGetValue(id,out var print)&&print.Code==code&&print.RootFixed&&print.MaxError<.00001f&&print.Vertices==81,"actual native chronicle mesh agrees with anchored historical geometry");
                bool parity=ChronicleScribeParity(code,id);Require(parity,"actual native Scribe value roundtrip");
                if(panel==0)Require(Math.Abs(print.TipX)<.001f&&Math.Abs(print.TipHeight-1f)<.001f&&Math.Abs(print.UpperWidth-1f)<.001f,"unexposed original geometry");
                if(kind==PlantFormKind.CrookedChronicle&&panel==1)Require(print.TipX>.31f,"historical left pressure remains after clearing");
                if(kind==PlantFormKind.CrookedChronicle&&panel==2)Require(print.TipX<-.31f,"historical right pressure remains after clearing");
                if(kind==PlantFormKind.CrookedChronicle&&panel==3)Require(print.LowerX>print.MiddleX&&print.TipX>print.UpperX,"opposing historical growth bends");
                if(kind==PlantFormKind.ReleaseCrown&&panel==4)Require(print.UpperWidth>1.7f&&print.LowerWidth<.8f,"real release crown over compact older growth");
                if(kind==PlantFormKind.StretchBands&&panel==4)Require(print.TipHeight>1.19f,"historical crowding leaves tall growth");
                ChronicleEvidence.Add(string.Join("\t",panel+1,p.def.defName,row.Column,id,row.Cultivar.id,kind,ChronicleMath.At(code,0),ChronicleMath.At(code,1),ChronicleMath.At(code,2),code,3,parity,print.RootFixed,print.LowerX,print.MiddleX,print.UpperX,print.TipX,print.TipHeight,print.LowerWidth,print.MiddleWidth,print.UpperWidth,print.MaxError,print.Vertices,print.MaterialId));
            }
            Log.Message("[HNS Chronicle] panel="+(panel+1)+"; history="+code+"; 15 mature plants; no current neighbors; 15 Scribe value roundtrips");
        }
        private static void WriteChronicle()
        {
            Require(inventory.Count==90&&inventory.Select(r=>r.Plant.thingIDNumber).Distinct().Count()==90&&ChronicleEvidence.Count==76,"90 plants, 75 historical morphology cases");
            Require(PlantFormMesh.ChroniclePrints.Count==75&&ChronicleExperiment.Samples.Values.Sum()==225&&ChronicleCultivars.Count==15,"all historical observations and actual registered cultivars");
            Require(inventory.Where(r=>r.Cultivar==null).All(r=>Backend(r.Plant)=="Vanilla"&&r.Plant.TryGetComp<CompPlantVariety>().VisualChronicle==0),"15 native controls and no history recorded");
            Require(WaylithPhenotypeShader.MaterialCount==0&&WaylithPhenotypeShader.MaskCount==0&&PlantFormMesh.Vertices==PlantFormMesh.Meshes*81&&PlantFormMesh.Triangles==PlantFormMesh.Meshes*128,"bounded original material grid");
            File.WriteAllLines(Path.Combine(root,"HNS-garden-chronicle.tsv"),ChronicleEvidence);
            Log.Message("[HNS Chronicle] verified=75; observations=225; Scribe value parity=75; mesh prints="+PlantFormMesh.Meshes+"; vertices="+PlantFormMesh.Vertices);
        }
        private static void SampleNeighborhood()
        {
            var expected=ExpectedNeighborhood();
            var styled=inventory.Where(r=>r.Panel==panel+1&&r.Cultivar!=null).ToArray();
            Require(styled.Length==15&&NeighborhoodCompanions.Count==(int)(18*(expected.Left+expected.Right)),"real neighborhood state");
            foreach(var row in styled)
            {
                var p=row.Plant;int id=p.thingIDNumber;var kind=row.Cultivar.traits.Single().plantForm;
                Require(NeighborhoodExperiment.Current(p)==expected,"actual neighbor sampling");
                Require(PlantFormMesh.NeighborhoodPrints.TryGetValue(id,out var print)&&print.Pressure==expected&&print.RootFixed,"actual root-anchored native mesh and current neighbor state");
                NeighborhoodExperiment.Invalidations.TryGetValue(id,out int invalidations);
                if(panel==0)NeighborhoodInitial[id]=print;
                else
                {
                    var previous=NeighborhoodPrevious[id];
                    Require(print.Prints>previous.Prints&&invalidations>NeighborhoodPreviousInvalidations[id],"automatic neighbor invalidation and fresh mesh");
                }
                var initial=NeighborhoodInitial[id];
                if(panel==1||panel==2)
                {
                    float dir=expected.Difference;
                    if(kind==PlantFormKind.GapSeeker)Require(print.TipShift*dir>.30f,"actual gap-facing crown");
                    if(kind==PlantFormKind.CompanionBow)Require(print.TipShift*dir<-.27f,"actual companion-facing crown");
                    Require(print.GeometryChanged,"single neighbor changes actual geometry");
                }
                if(panel==3&&kind==PlantFormKind.StandSpire)Require(print.TipHeight>initial.TipHeight+.24f&&print.Width<initial.Width*.55f,"actual tall narrow crowded canopy");
                if(panel==3&&kind==PlantFormKind.CrownPartition)Require(print.Width<initial.Width*.58f,"both neighbors narrow actual canopy");
                if(panel==3&&kind==PlantFormKind.FreeRosette)Require(print.Width<initial.Width*.50f&&print.TipHeight>initial.TipHeight+.20f,"solitary rosette contracts");
                if(panel==4)Require(Math.Abs(print.TipShift-initial.TipShift)<.0001f&&Math.Abs(print.TipHeight-initial.TipHeight)<.0001f&&Math.Abs(print.Width-initial.Width)<.0001f,"actual original shape restored");
                NeighborhoodPrevious[id]=print;NeighborhoodPreviousInvalidations[id]=invalidations;
                NeighborhoodEvidence.Add(string.Join("\t",panel+1,p.def.defName,row.Column,id,kind,expected.Left,expected.Right,print.Prints,invalidations,print.RootFixed,print.TipShift,print.TipHeight,print.Width,print.GeometryChanged));
            }
            Log.Message("[HNS Neighborhood] verified state="+(panel+1)+"; 15 rooted meshes; neighbor count="+NeighborhoodCompanions.Count);
        }
        private static void WriteNeighborhood()
        {
            Require(inventory.Count==90&&inventory.Select(r=>r.Plant.thingIDNumber).Distinct().Count()==18&&NeighborhoodEvidence.Count==76,"same 18 individuals, 75 contextual states");
            Require(inventory.Where(r=>r.Cultivar==null).All(r=>Backend(r.Plant)=="Vanilla")&&PlantFormMesh.NeighborhoodPrints.Count==15,"three untouched controls");
            Require(WaylithPhenotypeShader.MaterialCount==0&&WaylithPhenotypeShader.MaskCount==0&&PlantFormMesh.Vertices==PlantFormMesh.Meshes*25&&PlantFormMesh.Triangles==PlantFormMesh.Meshes*32,"bounded original material mesh path");
            File.WriteAllLines(Path.Combine(root,"HNS-garden-neighborhood.tsv"),NeighborhoodEvidence);
            Log.Message("[HNS Neighborhood] verified=75; contextual plants=15; mesh prints="+PlantFormMesh.Meshes+"; vertices="+PlantFormMesh.Vertices+"; invalidations="+NeighborhoodExperiment.Invalidations.Values.Sum());
        }
        private static void SampleMosaic()
        {
            var styled=inventory.Where(r=>r.Panel==panel+1&&r.Cultivar!=null).ToArray();
            Require(styled.Length==15&&map.skyManager.CurSkyGlow>.8f&&mosaicReprintPanel==panel,"daylight mosaic fixture and remesh");
            foreach(var group in styled.GroupBy(r=>r.Plant.def))
            {
                Require(group.Count()==5&&group.All(r=>ReferenceEquals(r.Cultivar,group.First().Cultivar)),"five individuals of one actual registered cultivar");
                Require(group.Select(r=>MosaicMath.Frame(r.Plant.HashOffset()%1024)).Distinct().Count()>=2,"same cultivar expresses multiple individual seed variants");
            }
            foreach(var row in styled)
            {
                int id=row.Plant.thingIDNumber;var trait=row.Cultivar.traits.Single();
                Require(ResponsiveHabitExperiment.PrintedPlants.TryGetValue(id,out var mat)&&
                    mat.shader==DefDatabase<ShaderDef>.GetNamed(ArticulationAtlas.ShaderName).Shader&&mat.GetVector("_HnsPose").z==1f&&mat.mainTexture.name.EndsWith("_"+trait.responsiveHabit),"actual seed-selected mosaic material");
                Require(PlantFormMesh.MosaicPrints.TryGetValue(id,out var print)&&print.Valid&&print.Prints>=2&&print.Payload==row.Plant.HashOffset()%1024&&print.MaterialId==mat.GetInstanceID(),"native appended UV.z and material stable on reprint");
                MosaicEvidence.Add(string.Join("\t",panel+1,row.Plant.def.defName,row.Column,id,trait.defName,print.Payload,MosaicMath.Frame((int)print.Payload),print.MaterialId,print.Prints,mat.mainTexture.name,true));
            }
            Require(styled.GroupBy(r=>ResponsiveHabitExperiment.PrintedPlants[r.Plant.thingIDNumber]).Any(g=>g.Select(r=>MosaicMath.Frame(r.Plant.HashOffset()%1024)).Distinct().Count()>1),"shared material carries different per-plant variants");
            Log.Message("[HNS Mosaic] panel="+(panel+1)+"; 15 same-cultivar cases; atlas count="+ArticulationAtlas.Count+"; materials="+ResponsiveHabitExperiment.MaterialCount);
        }
        private static void WriteMosaic()
        {
            var styled=inventory.Where(r=>r.Cultivar!=null).ToArray();
            Require(inventory.Count==90&&styled.Length==75&&inventory.Select(r=>r.Plant.thingIDNumber).Distinct().Count()==90&&MosaicEvidence.Count==76,"90 actual plants, 75 validated cases");
            Require(styled.All(r=>Backend(r.Plant)=="GPU individual organ mosaic")&&inventory.Where(r=>r.Cultivar==null).All(r=>Backend(r.Plant)=="Vanilla"),"native mosaic and original controls");
            Require(ArticulationAtlas.OrientationEvidence.Count==1+ArticulationAtlas.Count&&ArticulationAtlas.MosaicPreparation.Count==1+8*ArticulationAtlas.Count,"every atlas orientation and all mosaic variants verified");
            Require(ArticulationAtlas.Count==30&&ResponsiveHabitExperiment.MaterialCount<=30&&WaylithPhenotypeShader.MaterialCount==0&&WaylithPhenotypeShader.MaskCount==0,"bounded shared atlas/material use");
            File.WriteAllLines(Path.Combine(root,"HNS-garden-mosaic.tsv"),MosaicEvidence);
            File.WriteAllLines(Path.Combine(root,"HNS-garden-mosaic-preparation.tsv"),ArticulationAtlas.MosaicPreparation);
            Log.Message("[HNS Mosaic] verified=75; materials="+ResponsiveHabitExperiment.MaterialCount+"; atlases="+ArticulationAtlas.Count+"; eight variants each");
        }
        private static void SamplePose()
        {
            var styled=inventory.Where(r=>r.Panel==1&&r.Cultivar!=null).ToArray();
            Require(styled.Length==15,"pose fixture state");
            if(panel==0)poseInitialMaterials=ResponsiveHabitExperiment.MaterialCount;
            if(articulationGarden){SampleArticulation(styled);return;}
            float sky=map.skyManager.CurSkyGlow,rain=map.weatherManager.RainRate;
            Require((panel==3?sky<.2f:sky>.8f)&&(panel==2?rain>.1f:rain==0f),"actual light and rain states");
            Require(ResponsiveHabitExperiment.MaterialCount==poseInitialMaterials,"stable material count across poses");
            foreach(var row in styled)
            {
                int id=row.Plant.thingIDNumber;
                var mat=ResponsiveHabitExperiment.PrintedPlants[id];
                bool flip=ResponsiveHabitExperiment.PrintedFlips[id];
                var trait=row.Cultivar.traits.Single();
                var controls=ResponsiveHabitExperiment.Current(map,trait.responsiveHabit,false,flip);
                Require(mat.shader==DefDatabase<ShaderDef>.GetNamed(ResponsiveHabitExperiment.DefName).Shader&&mat.GetVector("_HnsPose")==controls,"actual pose material and updated native environment");
                if(panel==0){PoseInitialIDs[id]=mat.GetInstanceID();PoseInitialControls[id]=controls;}
                else Require(PoseInitialIDs[id]==mat.GetInstanceID(),"no pose material replacement");
                if(panel==4)Require(PoseInitialControls[id]==controls,"original morning pose restored");
                PoseEvidence.Add(string.Join("\t",panel+1,row.Plant.def.defName,row.Column,id,trait.defName,GenLocalDate.HourOfDay(map),map.weatherManager.curWeather.defName,sky,rain,flip,controls.x,controls.y,controls.z,controls.w,mat.GetInstanceID()));
            }
            Log.Message("[HNS Pose] phase="+(panel+1)+"; sky="+sky+"; rain="+rain+"; materials="+poseInitialMaterials+"; uniform updates="+ResponsiveHabitExperiment.Updates);
        }
        // Explicit GPU readback of synthetic per-cell identifiers. The garden
        // verifies material state; this verifies the actual selected atlas cell.
        private static void VerifyArticulationAtlasPixels()
        {
            const int tile=32, side=tile*3;
            var colors=new Color[side*side];
            Color Swatch(int frame,int x,int y)=>new Color(.08f+.095f*frame,x>=tile/2?.65f:.25f,y>=tile/2?.65f:.25f,1f);
            for(int y=0;y<side;y++)for(int x=0;x<side;x++)colors[y*side+x]=Swatch((y/tile)*3+x/tile,x%tile,y%tile);
            var atlas=new Texture2D(side,side,TextureFormat.RGBA32,false,true){filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};
            atlas.SetPixels(colors);atlas.Apply();
            var shader=DefDatabase<ShaderDef>.GetNamed(ArticulationAtlas.ShaderName).Shader;
            var mat=new Material(shader){mainTexture=atlas};
            mat.SetVector("_HnsAtlas",new Vector4(1f/tile,1f/tile,0f,0f));
            var previous=RenderTexture.active;
            var rt=RenderTexture.GetTemporary(tile,tile,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);
            var read=new Texture2D(tile,tile,TextureFormat.RGBA32,false,true);
            var observations=new List<string>{"Requested frame\tObserved frame\tMax swatch RGB error"};
            float max=0;
            try
            {
                for(int f=0;f<ArticulationMath.Frames;f++)
                {
                    mat.SetVector("_HnsPose",new Vector4(f/8f,0f,0f,0f));
                    Graphics.Blit(atlas,rt,mat,0);RenderTexture.active=rt;
                    read.ReadPixels(new Rect(0,0,tile,tile),0,0,false);read.Apply();
                    var values=new[]{read.GetPixel(8,8),read.GetPixel(24,8),read.GetPixel(8,24),read.GetPixel(24,24)};
                    int observed=(int)MathF.Round((values[0].r-.08f)/.095f);
                    var expected=new[]{Swatch(f,8,8),Swatch(f,24,8),Swatch(f,8,24),Swatch(f,24,24)};
                    float error=0;
                    foreach(var v in values)
                    {
                        float e=expected.Min(c=>Math.Max(Math.Abs(c.r-v.r),Math.Max(Math.Abs(c.g-v.g),Math.Abs(c.b-v.b))));
                        error=Math.Max(error,e);
                    }
                    max=Math.Max(max,error);
                    observations.Add(string.Join("\t",f,observed,error.ToString("R",CultureInfo.InvariantCulture)));
                }
                File.WriteAllLines(Path.Combine(root,"HNS-garden-articulation-gpu-parity.tsv"),observations);
                Log.Message("[HNS Articulation] atlas GPU frame parity: "+string.Join(" | ",observations.Skip(1))+"; max error="+max);
                Require(max<.025f,"GPU samples correct atlas cells for all nine frame indices");
            }
            finally
            {
                RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);
                UnityEngine.Object.Destroy(read);UnityEngine.Object.Destroy(mat);UnityEngine.Object.Destroy(atlas);
            }
        }
        private static readonly List<string> ArticulationEvidence=new(){"Panel\tSpecies\tColumn\tPlant ID\tTrait\tSky\tRain\tFrame\tSway head\tMaterial ID\tRenderer"};
        private static readonly Dictionary<int,int> ArticulationMorning=new();
        private static float articulationLastHead;
        private static void SampleArticulation(Row[] styled)
        {
            float sky=map.skyManager.CurSkyGlow,rain=map.weatherManager.RainRate;
            Require((panel==3?sky<.2f:sky>.8f)&&(panel==2?rain>.1f:rain==0f),"actual articulation environment");
            Require(ResponsiveHabitExperiment.MaterialCount==poseInitialMaterials,"stable articulation material count");
            var printed=styled.Where(r=>ResponsiveHabitExperiment.PrintedPlants.ContainsKey(r.Plant.thingIDNumber)).ToArray();
            Require(printed.Length>=9&&printed.Select(r=>r.Cultivar.traits.Single().responsiveHabit).Distinct().Count()==5,"substantial coverage of every articulation allele");
            var flutter=printed.First(r=>r.Cultivar.traits.Single().responsiveHabit==ResponsiveHabitKind.SilkFlutter);
            var wind=ResponsiveHabitExperiment.PrintedPlants[flutter.Plant.thingIDNumber];
            float before=wind.GetFloat("_SwayHead");
            int updates=0;
            do{map.windManager.WindManagerTick();updates++;}
            while((wind.GetFloat("_SwayHead")<before+2f||ArticulationMath.WindFrame(wind.GetFloat("_SwayHead"))==ArticulationMath.WindFrame(before))&&updates<250);
            float head=wind.GetFloat("_SwayHead");
            Require(head>before&&ArticulationMath.WindFrame(head)!=ArticulationMath.WindFrame(before)&&(panel==0||head>articulationLastHead),"actual wind phase changes local organ frame");
            articulationLastHead=head;
            foreach(var row in styled)
            {
                int id=row.Plant.thingIDNumber;var trait=row.Cultivar.traits.Single();
                if(!ResponsiveHabitExperiment.PrintedPlants.TryGetValue(id,out var mat))
                {ArticulationEvidence.Add(string.Join("\t",panel+1,row.Plant.def.defName,row.Column,id,trait.defName,sky,rain,-1,head,0,"Fallback"));continue;}
                bool flip=ResponsiveHabitExperiment.PrintedFlips[id];var controls=ResponsiveHabitExperiment.Current(map,trait.responsiveHabit,false,flip);
                Require(mat.shader==DefDatabase<ShaderDef>.GetNamed(ArticulationAtlas.ShaderName).Shader&&mat.GetVector("_HnsPose")==controls&&mat.mainTexture.name.Contains("_HNS_Articulation_"),"actual organ atlas and native controls");
                int frame=trait.responsiveHabit==ResponsiveHabitKind.SilkFlutter?ArticulationMath.WindFrame(mat.GetFloat("_SwayHead")):ArticulationMath.Frame(controls.x);
                if(panel==0){PoseInitialIDs[id]=mat.GetInstanceID();ArticulationMorning[id]=frame;}
                else Require(PoseInitialIDs[id]==mat.GetInstanceID(),"stable organ material identity");
                if(panel==4&&trait.responsiveHabit!=ResponsiveHabitKind.SilkFlutter)Require(ArticulationMorning[id]==frame,"environmental pose restored");
                ArticulationEvidence.Add(string.Join("\t",panel+1,row.Plant.def.defName,row.Column,id,trait.defName,sky,rain,frame,mat.GetFloat("_SwayHead"),mat.GetInstanceID(),"Atlas"));
            }
            Log.Message("[HNS Articulation] panel="+(panel+1)+"; rendered="+printed.Length+"; wind head="+head+"; atlas count="+ArticulationAtlas.Count);
        }
        private static void WriteArticulation(List<Row> styled)
        {
            Require(inventory.Where(r=>r.Cultivar==null).All(r=>Backend(r.Plant)=="Vanilla"),"vanilla controls");
            Require(ArticulationEvidence.Count==1+15*PoseFrames&&ResponsiveHabitExperiment.MaterialCount==poseInitialMaterials&&WaylithPhenotypeShader.MaterialCount==0&&WaylithPhenotypeShader.MaskCount==0,"complete local articulation evidence without material growth or GPU masks");
            Require(ArticulationAtlas.OrientationEvidence.Count==1+ArticulationAtlas.Count,"all prepared source atlases preserve the original sprite orientation");
            File.WriteAllLines(Path.Combine(root,"HNS-garden-articulation.tsv"),ArticulationEvidence);
            File.WriteAllLines(Path.Combine(root,"HNS-garden-articulation-preparation.tsv"),new[]{"Species\tVariation\tSource\tTrait\tWidth\tHeight\tOrgans\tRecovered pixels\tMin change\tMax change\tAlpha preserved"}.Concat(ArticulationAtlas.Preparation));
            Log.Message("[HNS Articulation] rendered="+ResponsiveHabitExperiment.PrintedPlants.Count+"; verified="+(ArticulationEvidence.Count-1)+"; materials="+poseInitialMaterials+"; atlases="+ArticulationAtlas.Count);
        }
        private static readonly List<Plant> LifeSuiteNeighbors=new();
        private static readonly List<string> LifeSuiteEvidence=new(){"Panel\tSpecies\tColumn\tPlant ID\tTrait\tMode\tMaterial ID\tExtra material ID\tAtlas\tExtra planes\tFoliage territories\tReproductive components\tLight\tWetness\tDay\tSeason input\tGrowth\tStress input\tLateral links\tActive\tDescription"};
        private static readonly HashSet<int> LifeSuiteAcceptedModes=new(),LifeSuiteActiveModes=new(),LifeSuiteAtlasModes=new();
        // Motion evidence uses only native captures from the isolated game.
        // Advance the native wind manager and actual local solar time. The
        // seasonal/temperature inputs remain explicit experimental controls.
        private static bool lifeSuiteMotion;
        private const int LifeMotionFrames=12;
        private static int lifeMotionFrame,lifeMotionAge;
        private static readonly List<string> LifeMotionEvidence=new(){"Panel\tFrame\tLocal day\tSky glow\tRain rate\tSeason input\tTemperature input\tFirst plant growth\tNative wind head"};
        private static bool CaptureLifeSuiteMotion()
        {
            if(!lifeSuiteMotion)return true;
            if(lifeMotionFrame>=LifeMotionFrames)return true;
            string path=Path.Combine(root,$"HNS-garden-motion-{panel+1:D2}-{lifeMotionFrame:D2}.png");
            if(lifeMotionAge==0)
            {
                float t=lifeMotionFrame/(float)(LifeMotionFrames-1);
                int group=panel/2;bool second=panel%2==1;
                float target=group switch
                {
                    0=>second?.005f+.065f*t:.215f+.165f*t,
                    1=>second?.005f+.065f*t:.44f+.12f*t,
                    2=>second?.57f+.13f*t:.34f+.14f*t,
                    _=>.43f+.14f*t
                };
                int advance=Mathf.RoundToInt(Mathf.Repeat(target-GenLocalDate.DayPercent(map),1f)*60000f);
                Find.TickManager.DebugSetTicksGame(Find.TickManager.TicksGame+advance);
                if(group==0&&!second)LifeSuiteExperiment.TemperatureOverride=43f-13f*t;
                if(group==1)LifeSuiteExperiment.SeasonOverride=second?.87f+.13f*t:.28f+.62f*t;
                if(group==3&&!second)
                    foreach(var p in current)p.Growth=.72f+.24f*t;
                if(group==3&&second)LifeSuiteExperiment.TemperatureOverride=3f-11f*t;
                if(group==0&&!second||group==3)
                    foreach(var p in current)p.DirtyMapMesh(map);
                map.skyManager.SkyManagerUpdate();
                var first=current.Select(p=>LifeSuiteExperiment.Prints.TryGetValue(p.thingIDNumber,out var pr)?pr.Base:null).FirstOrDefault(m=>m!=null);
                Require(first!=null,"motion uses native accepted plant material");
                float before=first.GetFloat("_SwayHead");int updates=0;
                do{map.windManager.WindManagerTick();updates++;}
                while(first.GetFloat("_SwayHead")<before+1.3f&&updates<250);
                float head=first.GetFloat("_SwayHead");
                Require(float.IsFinite(head)&&head>before,"actual motion wind phase advanced");
                LifeSuiteExperiment.Refresh(map);
                LifeMotionEvidence.Add(string.Join("\t",panel+1,lifeMotionFrame,GenLocalDate.DayPercent(map),map.skyManager.CurSkyGlow,map.weatherManager.RainRate,LifeSuiteExperiment.SeasonOverride,LifeSuiteExperiment.TemperatureOverride,current[0].Growth,head));
                lifeMotionAge=1;return false;
            }
            if(++lifeMotionAge==7){ScreenCapture.CaptureScreenshot(path);return false;}
            if(lifeMotionAge<11||!File.Exists(path)||new FileInfo(path).Length<1000)return false;
            lifeMotionFrame++;lifeMotionAge=0;
            File.WriteAllLines(Path.Combine(root,"HNS-garden-motion.tsv"),LifeMotionEvidence);
            if(lifeMotionFrame<LifeMotionFrames)return false;
            Log.Message("[HNS LifeMotion] "+(panel+1)+"; "+LifeMotionFrames+" native frames");
            return true;
        }
        private static readonly List<string> SculptureGpuEvidence=new(){"Frame\tSpecies\tColumn\tPlant ID\tMode\tWidth\tHeight\tSource RGB error\tMirrored source RGB error\tColored pixels\tSHA256"};
        private static readonly Dictionary<int,HashSet<string>> SculptureGpuFrames=new();
        private static readonly Dictionary<int,Color32[]> SculptureGpuOriginal=new();
        private static Color32[] SculptureReadback(Texture source,Material mat,int w,int h,string path)
        {
            var old=RenderTexture.active;
            var rt=RenderTexture.GetTemporary(w,h,0,RenderTextureFormat.ARGB32);
            Texture2D read=null;
            try
            {
                Graphics.Blit(Texture2D.blackTexture,rt);
                Graphics.Blit(source,rt,mat,0);
                RenderTexture.active=rt;
                read=new Texture2D(w,h,TextureFormat.RGBA32,false);
                read.ReadPixels(new Rect(0,0,w,h),0,0,false);read.Apply();
                File.WriteAllBytes(path,read.EncodeToPNG());
                return read.GetPixels32();
            }
            finally
            {
                RenderTexture.active=old;RenderTexture.ReleaseTemporary(rt);
                if(read!=null)UnityEngine.Object.Destroy(read);
            }
        }
        private static float SculptureSourceError(Color32[] src,Color32[] dst,int sw,int sh,int dw,int dh,bool flip)
        {
            int ox=(dw-sw)/2,oy=(dh-sh)/2,n=0;long e=0;
            for(int y=1;y<sh-1;y++)for(int x=1;x<sw-1;x++)
            {
                var a=src[y*sw+x];
                if(a.r+a.g+a.b<24)continue;
                // Ignore cutout antialiasing at the silhouette margin.
                if(src[y*sw+x-1].r+src[y*sw+x-1].g+src[y*sw+x-1].b<24||
                   src[y*sw+x+1].r+src[y*sw+x+1].g+src[y*sw+x+1].b<24||
                   src[(y-1)*sw+x].r+src[(y-1)*sw+x].g+src[(y-1)*sw+x].b<24||
                   src[(y+1)*sw+x].r+src[(y+1)*sw+x].g+src[(y+1)*sw+x].b<24)continue;
                var b=dst[(oy+(flip?sh-1-y:y))*dw+ox+x];
                e+=Math.Max(Math.Abs(a.r-b.r),Math.Max(Math.Abs(a.g-b.g),Math.Abs(a.b-b.b)));n++;
            }
            return n==0?1f:e/(255f*n);
        }
        private static string CaptureSculptureGpu()
        {
            string first=null;
            foreach(var row in inventory.Where(r=>r.Panel==panel+1&&r.Cultivar!=null))
            {
                var p=row.Plant;int id=p.thingIDNumber;
                var pr=LifeSuiteExperiment.Prints[id];var m=pr.Base;
                var source=m.mainTexture;int sw=source.width,sh=source.height,w=SculptureMath.Side(sw),h=SculptureMath.Side(sh);
                string tag=p.def.defName+"-"+(forgeGarden?pr.Mode:row.Column);
                // These are real shader readbacks, not map screenshots. Freeze
                // wind in the preview copy only; the live material was checked.
                if(!SculptureGpuOriginal.TryGetValue(id,out var original))
                {
                    var control=new Material(ShaderDatabase.CutoutPlant){mainTexture=source,renderQueue=2900};
                    control.SetFloat("_FallBehaviorEnabled",0f);control.SetFloat("_SwayHead",0f);
                    try{original=SculptureReadback(source,control,sw,sh,Path.Combine(root,"HNS-garden-sculpture-control-"+tag+".png"));}
                    finally{UnityEngine.Object.Destroy(control);}
                    SculptureGpuOriginal.Add(id,original);
                    SculptureGpuFrames.Add(id,new HashSet<string>());
                }
                string path=Path.Combine(root,$"HNS-garden-sculpture-gpu-{panel+1:D2}-{tag}.png");
                var preview=new Material(m);preview.SetFloat("_SwayHead",0f);
                Color32[] result;
                try{result=SculptureReadback(source,preview,w,h,path);}
                finally{UnityEngine.Object.Destroy(preview);}
                float direct=SculptureSourceError(original,result,sw,sh,w,h,false);
                float mirrored=SculptureSourceError(original,result,sw,sh,w,h,true);
                int colored=result.Count(c=>c.r+c.g+c.b>24);
                string sha=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path)));
                Require(colored>30&&File.Exists(path)&&new FileInfo(path).Length>500,"nonblank native GPU frame");
                Require(direct<(SculptureMath.ForgeTint(pr.Mode)?.23f:.16f)&&direct<=mirrored+(SculptureMath.ForgeTint(pr.Mode)?.065f:.025f),"native GPU foreground orientation and color "+tag+" direct="+direct+" mirror="+mirrored);
                SculptureGpuFrames[id].Add(sha);
                SculptureGpuEvidence.Add(string.Join("\t",panel+1,p.def.defName,row.Column,id,pr.Mode,w,h,direct,mirrored,colored,sha));
                first??=path;
            }
            File.WriteAllLines(Path.Combine(root,"HNS-garden-sculpture-gpu.tsv"),SculptureGpuEvidence);
            Log.Message("[HNS Sculpture] native GPU readback phase="+(panel+1)+" count="+(SculptureGpuEvidence.Count-1));
            return first;
        }

        private static readonly float[] ExpressionGrowth={.40f,.55f,.65f,.80f,.90f,1f};
        private static readonly List<Plant> ExpressionNeighbors=new();
        private static readonly List<string> ExpressionEvidence=new(){"Phase\tSpecies\tColumn\tPlant ID\tCultivar ID\tGrowth\tBand\tSeed\tHistory code\tBias\tMaterial ID\tAccepted\tDeferred\tMesh payload\tPhenotype\tSHA256"};
        private static readonly Dictionary<int,HashSet<string>> ExpressionHashes=new();
        private static bool expressionLabOpened;
        private static int expressionLabAge;
        private static void AdvanceExpressionGrowth()
        {
            foreach(var n in ExpressionNeighbors)if(n.Spawned)n.Destroy(DestroyMode.Vanish);
            ExpressionNeighbors.Clear();
            var rice=DefDatabase<ThingDef>.GetNamed("Plant_Rice");
            foreach(var row in inventory.Where(r=>r.Panel==panel+1&&r.Cultivar!=null))
            {
                int side=row.Column switch{2=>-1,3=>1,4=>panel==2?-1:panel==4?1:0,5=>panel%2==0?-1:1,_=>-1};
                if(panel==5&&row.Column!=5)side=0;
                if(side!=0)
                {
                    var cell=row.Plant.Position+new IntVec3(side,0,0);
                    Require(ClearCell(cell)&&cell.GetPlant(map)==null,"actual neighboring test cell");
                    var n=(Plant)ThingMaker.MakeThing(rice);n.Growth=1f;n.sown=true;
                    GenSpawn.Spawn(n,cell,map);ExpressionNeighbors.Add(n);
                }
            }
            // Real growth setters exercise the production hook. No forced
            // DirtyMapMesh calls in this transition.
            foreach(var p in current)p.Growth=ExpressionGrowth[panel];
            var weather=panel==3||panel==4?DefDatabase<WeatherDef>.GetNamed("Rain"):WeatherDefOf.Clear;
            map.weatherManager.TransitionTo(weather);map.weatherManager.lastWeather=weather;map.weatherManager.curWeatherAge=4000;
            map.skyManager.SkyManagerUpdate();BotanicalExpressionEngine.Refresh(map);
        }
        private static void SampleExpression()
        {
            var rows=inventory.Where(r=>r.Panel==panel+1&&r.Cultivar!=null).ToArray();
            Require(rows.Length==15,"15 actual composed plants");
            int accepted=0;
            foreach(var r in rows)
            {
                var p=r.Plant;int id=p.thingIDNumber;var c=p.TryGetComp<CompPlantVariety>();
                Require(BotanicalExpressionEngine.Invalidations.Contains((id,ExpressionMath.Band(p.Growth))),"actual automatic growth mesh invalidation "+id);
                Require(c.Variety==r.Cultivar,"same registered cultivar identity");
                if(!BotanicalExpressionEngine.Prints.TryGetValue(id,out var pr))
                {
                    Require(panel<5,"mature plant must use composed renderer "+p.def.defName);
                    continue;
                }
                accepted++;
                Require(pr.Band==ExpressionMath.Band(p.Growth)&&pr.Seed==ExpressionMath.Seed(id)&&pr.Code==c.VisualChronicle,"actual growth, stable ID seed and stored history");
                Require(BotanicalExpressionEngine.MeshPayload.TryGetValue(id,out bool payload)&&payload,"actual native mesh color/UV payload");
                Require(pr.Material.shader==DefDatabase<ShaderDef>.GetNamed("HNS_BotanicalExpression").Shader,"actual native expression shader");
                Require(pr.Material.GetVector("_HnsPreview")==Vector4.zero,"live material never uses preview override");
                if(panel==5)
                {
                    Require(pr.Decision.Plan.Count==5&&pr.Decision.Rejected.Length==0,"all five compatible channels "+p.def.defName);
                    if(pr.Decision.Plan.Context==1)Require(ChronicleMath.Count(pr.Code)==3,"recorded three actual growth history thresholds");
                }
                var facts=c.VisualObservation?.Facts()??Array.Empty<PhenotypeVocabulary.Fact>();
                Require(facts.Any(f=>f.Slot=="individual"),"native expressed individual facts");
            }
            Require(accepted>0,"native composed material acceptance");
            Log.Message("[HNS Expression] phase="+(panel+1)+"; accepted="+accepted+"; materials="+BotanicalExpressionEngine.MaterialCount+"; bytes="+BotanicalExpressionEngine.Bytes);
        }
        private static string CaptureExpressionGpu()
        {
            string first=null;
            foreach(var r in inventory.Where(r=>r.Panel==panel+1&&r.Cultivar!=null))
            {
                var p=r.Plant;int id=p.thingIDNumber;
                string tag=p.def.defName+"-"+r.Column;
                string path=Path.Combine(root,$"HNS-garden-expression-{panel+1:D2}-{tag}.png");
                string controlPath=Path.Combine(root,$"HNS-garden-expression-control-{panel+1:D2}-{tag}.png");
                bool accepted=BotanicalExpressionEngine.Prints.TryGetValue(id,out var pr);
                var src=accepted?pr.Material.mainTexture:p.Graphic.MatSingleFor(p).mainTexture;
                var control=new Material(ShaderDatabase.CutoutPlant){mainTexture=src,renderQueue=2900};
                control.SetFloat("_FallBehaviorEnabled",0f);control.SetFloat("_SwayHead",0f);
                try{SculptureReadback(src,control,src.width,src.height,controlPath);}
                finally{UnityEngine.Object.Destroy(control);}
                if(accepted)
                {
                    var tex=BotanicalExpressionEngine.Readback(pr.Material,pr.Band,pr.Seed,pr.Flipped?-pr.Bias:pr.Bias,pr.Material.GetVector("_HnsExpressionEnv"));
                    try
                    {
                        Require(tex.GetPixels32().Count(c=>c.r+c.g+c.b>24)>30,"nonblank composed GPU render");
                        File.WriteAllBytes(path,tex.EncodeToPNG());
                    }
                    finally{UnityEngine.Object.Destroy(tex);}
                }
                else File.Copy(controlPath,path,true);
                string sha=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path)));
                if(!ExpressionHashes.TryGetValue(id,out var hashes))ExpressionHashes[id]=hashes=new();
                hashes.Add(sha);
                var c=p.TryGetComp<CompPlantVariety>();var description=c.VisualObservation?.Prose()??"";
                ExpressionEvidence.Add(string.Join("\t",panel+1,p.def.defName,r.Column,id,r.Cultivar.id,p.Growth,ExpressionMath.Band(p.Growth),ExpressionMath.Seed(id),c.VisualChronicle,accepted?pr.Bias:0f,accepted?pr.Material.GetInstanceID():0,accepted?string.Join(",",pr.Decision.Accepted):"",accepted?string.Join("; ",pr.Decision.Rejected):"source fallback",accepted,description,sha));
                if(accepted)first??=path;
            }
            File.WriteAllLines(Path.Combine(root,"HNS-garden-expression.tsv"),ExpressionEvidence);
            return first;
        }
        private static bool CheckExpressionLab()
        {
            if(!expressionLabOpened)
            {
                Find.Selector.ClearSelection();
                Find.Selector.Select(inventory.First(r=>r.Panel==panel+1&&r.Cultivar!=null).Plant);
                Dialog_BotanicalLaboratory.LastDrawn=false;
                Find.WindowStack.Add(new Dialog_BotanicalLaboratory());expressionLabOpened=true;return false;
            }
            if(++expressionLabAge<20)return false;
            Require(Dialog_BotanicalLaboratory.LastDrawn,"native laboratory window repainted a selected documented cultivar");
            Find.WindowStack.TryRemove(typeof(Dialog_BotanicalLaboratory),false);Find.Selector.ClearSelection();
            File.WriteAllText(Path.Combine(root,"HNS-garden-expression-lab.txt"),"PASS: native lab repainted actual documented cultivar preview.");
            return true;
        }
        private static void WriteExpression()
        {
            Require(inventory.Count==108&&inventory.Select(r=>r.Plant.thingIDNumber).Distinct().Count()==18&&ExpressionEvidence.Count==91,"six actual growth phases; 18 same specimens");
            Require(ExpressionHashes.Count==15&&ExpressionHashes.Values.All(h=>h.Count>=3),"all individuals have distinct rendered growth phases");
            foreach(var group in inventory.Where(r=>r.Panel==6&&r.Column>=2&&r.Column<=4).GroupBy(r=>r.Plant.def))
            {
                Require(group.Select(r=>r.Cultivar.id).Distinct().Count()==1,"three plants share one registered cultivar");
                var prints=group.Select(r=>BotanicalExpressionEngine.Prints[r.Plant.thingIDNumber]).ToArray();
                Require(prints.Select(p=>p.Seed).Distinct().Count()==3,"stable different seeds within cultivar");
                Require(prints.Select(p=>p.Code).Distinct().Count()>=2,"different real growth histories");
                Require(prints.Any(p=>p.Bias>.25f)&&prints.Any(p=>p.Bias<-.25f),"opposite retained historical bias");
            }
            Require(BotanicalExpressionEngine.Bytes<=64L*1024*1024&&BotanicalExpressionEngine.MaterialCount<=160,"bounded renderer cache");
            File.WriteAllText(Path.Combine(root,"HNS-garden-expression-budget.txt"),"Source maps="+BotanicalExpressionEngine.SourceCount+"; shared materials="+BotanicalExpressionEngine.MaterialCount+"; texture bytes="+BotanicalExpressionEngine.Bytes+"; growth invalidations="+BotanicalExpressionEngine.Invalidations.Count);
        }

        private static readonly List<string> SculptureEvidence=new(){"Frame\tSpecies\tColumn\tPlant ID\tTrait\tMode\tMaterial ID\tAtlas width\tAtlas height\tPhase\tNative wind head\tDescription"};
        private static readonly Dictionary<int,int> SculptureMaterials=new();
        private static void SetSculpturePhase()
        {
            if(panel==0)
            {
                int advance=Mathf.RoundToInt(Mathf.Repeat(.5f-GenLocalDate.DayPercent(map),1f)*60000f);
                Find.TickManager.DebugSetTicksGame(Find.TickManager.TicksGame+advance);
                map.weatherManager.TransitionTo(WeatherDefOf.Clear);map.weatherManager.lastWeather=WeatherDefOf.Clear;map.weatherManager.curWeatherAge=4000;
                map.skyManager.SkyManagerUpdate();
            }
            LifeSuiteExperiment.SculpturePhase=(forgeGarden?panel%9:panel)/8f;
            LifeSuiteExperiment.Refresh(map);
        }
        private static void SampleSculpture()
        {
            var rows=inventory.Where(r=>r.Panel==panel+1&&r.Cultivar!=null).ToArray();
            Require(rows.Length==15,"fifteen real sculpture specimens");
            LifeSuiteExperiment.Refresh(map);
            foreach(var row in rows)
            {
                var plant=row.Plant;int id=plant.thingIDNumber;
                var kind=row.Cultivar.traits.Single().botanicalDetail;int mode=forgeGarden?SculptureMath.ForgeMode(kind):SculptureMath.Mode(kind);
                Require(LifeSuiteExperiment.Prints.TryGetValue(id,out var pr)&&pr.Mode==mode&&pr.Atlas&&pr.Planes==0,"native sculpture atlas accepted "+kind+" "+plant.def.defName);
                var m=pr.Base;var atlas=m.GetTexture("_HnsLifeAtlas");
                var controls=m.GetVector("_HnsLife");var env=m.GetVector("_HnsLifeEnv");
                Require(m.shader==DefDatabase<ShaderDef>.GetNamed(LifeSuiteExperiment.ShaderName).Shader&&controls.x==mode&&controls.z==1f&&Math.Abs(env.w-(forgeGarden?panel%9:panel)/8f)<.001f,"real shared phase and shader");
                Require(atlas!=null&&atlas.width==SculptureMath.Side(m.mainTexture.width)*3&&atlas.height==SculptureMath.Side(m.mainTexture.height)*3,"expanded source atlas");
                if(!SculptureMaterials.ContainsKey(id))SculptureMaterials[id]=m.GetInstanceID();else Require(SculptureMaterials[id]==m.GetInstanceID(),"material remains shared and unchanged across all nine phases");
                string text=plant.TryGetComp<CompPlantVariety>().VisualObservation?.Prose()??"";
                Require(text.Contains(PhenotypeVocabulary.Detail(kind).Phrase),"native expressed phenotype language");
                SculptureEvidence.Add(string.Join("\t",panel+1,plant.def.defName,row.Column,id,kind,mode,m.GetInstanceID(),atlas.width,atlas.height,env.w,m.GetFloat("_SwayHead"),text));
            }
            Log.Message("[HNS Sculpture] frame="+(panel+1)+"; phase="+panel/8f+"; maps="+LifeSuiteExperiment.MapCount+"; materials="+LifeSuiteExperiment.MaterialCount);
        }
        private static void WriteSculpture()
        {
            int groups=forgeGarden?4:1;
            Require(inventory.Count==162*groups&&inventory.Select(r=>r.Plant.thingIDNumber).Distinct().Count()==18*groups&&SculptureEvidence.Count==135*groups+1,"nine rendered states per eighteen-specimen garden");
            Require(inventory.Where(r=>r.Cultivar==null).All(r=>Backend(r.Plant)=="Vanilla"),"vanilla controls remain original");
            Require(SculptureGpuEvidence.Count==135*groups+1&&SculptureGpuFrames.Count==15*groups&&SculptureGpuFrames.Values.All(x=>x.Count>=2),"all native GPU samples and distinct phase renders");
            File.WriteAllLines(Path.Combine(root,"HNS-garden-sculpture.tsv"),SculptureEvidence);
            File.WriteAllLines(Path.Combine(root,"HNS-garden-sculpture-preparation.tsv"),new[]{"Species\tVariation\tSource\tAtlas mode\tFoliage territories\tOrgan count\tOrgan pixels\tDistinct frames\tAdded pixels"}.Concat(LifeSuiteExperiment.Preparation));
        }

        private static void SetLifeSuiteTime()
        {
            int group=panel/2;bool second=panel%2==1;
            float target=group==0?(second?0f:.27f):group==1?(second?0f:.50f):group==2?(second?.64f:.40f):.50f;
            int advance=Mathf.RoundToInt(Mathf.Repeat(target-GenLocalDate.DayPercent(map),1f)*60000f);
            Find.TickManager.DebugSetTicksGame(Find.TickManager.TicksGame+advance);
            var weather=group==3&&second?DefDatabase<WeatherDef>.GetNamed("Rain"):WeatherDefOf.Clear;
            map.weatherManager.TransitionTo(weather);map.weatherManager.lastWeather=weather;map.weatherManager.curWeatherAge=4000;
            LifeSuiteExperiment.TemperatureOverride=group==0&&!second?43f:group==3&&second?-8f:20f;
            LifeSuiteExperiment.SeasonOverride=group==1?(second?.98f:.87f):0f;
            map.skyManager.SkyManagerUpdate();map.windManager.WindManagerTick();
            LifeSuiteExperiment.Refresh(map);
        }
        private static void ApplyLifeSuiteContext()
        {
            foreach(var p in LifeSuiteNeighbors)if(p.Spawned)p.Destroy(DestroyMode.Vanish);
            LifeSuiteNeighbors.Clear();
            int group=panel/2;bool second=panel%2==1;
            if(group==3||group==2&&second)
            {
                var rice=DefDatabase<ThingDef>.GetNamed("Plant_Rice");
                VarietyRecord relay=null;
                if(group==3)relay=GameComponent_NovelSeeds.Instance.UnlockVariety(rice,new(){Trait("MycelialRelay")},"Relay neighbor",originKind:"debug-visual-fixture");
                foreach(var row in inventory.Where(r=>r.Panel==panel+1&&r.Cultivar!=null).ToArray())
                {
                    var mode=LifeSuiteMath.Mode(row.Cultivar.traits.Single().botanicalDetail);
                    if(mode!=15&&mode!=18)continue;
                    int dx=mode==15?-1:1;
                    var pos=row.Plant.Position+new IntVec3(dx,0,0);
                    Require(pos.GetPlant(map)==null&&ClearCell(pos),"clear living context neighbor cell");
                    var companion=(Plant)ThingMaker.MakeThing(rice);
                    companion.Growth=1f;companion.sown=true;
                    companion.TryGetComp<CompPlantVariety>().SetVariety(mode==18?relay:null);
                    GenSpawn.Spawn(companion,pos,map);
                    LifeSuiteNeighbors.Add(companion);
                }
            }
            foreach(var p in current)p.DirtyMapMesh(map);
        }
        private static void AdvanceLifeSuite()
        {
            foreach(var p in LifeSuiteNeighbors)if(p.Spawned)p.Destroy(DestroyMode.Vanish);
            LifeSuiteNeighbors.Clear();
            SetLifeSuiteTime();
            if(panel%2==1)
            {
                frames=0;captureAge=0;capture=null;
                foreach(var row in inventory.Where(r=>r.Panel==panel).ToArray())
                {
                    if(panel==7)row.Plant.Growth=1f;
                    inventory.Add(row with{Panel=panel+1});
                }
                ApplyLifeSuiteContext();return;
            }
            foreach(var p in current)if(p.Spawned)p.Destroy(DestroyMode.Vanish);
            current.Clear();MakePanel();
        }
        private static void SampleLifeSuite()
        {
            var rows=inventory.Where(r=>r.Panel==panel+1&&r.Cultivar!=null).ToArray();
            Require(rows.Length==15,"15 experimental specimens in each paired garden");
            var e=LifeSuiteExperiment.Environment(map);
            LifeSuiteExperiment.Refresh(map);
            foreach(var r in rows)
            {
                var p=r.Plant;int id=p.thingIDNumber;
                var kind=r.Cultivar.traits.Single().botanicalDetail;int mode=LifeSuiteMath.Mode(kind);
                string description=p.TryGetComp<CompPlantVariety>().VisualObservation?.Prose()??"";
                if(!LifeSuiteExperiment.Prints.TryGetValue(id,out var pr))
                {
                    Require(LifeSuiteExperiment.Fallback.Contains(id),"explicit anatomy fallback "+kind+" "+p.def.defName);
                    Require(!description.Contains(PhenotypeVocabulary.Detail(kind).Phrase),"no invented fallback appearance");
                    LifeSuiteEvidence.Add(string.Join("\t",panel+1,p.def.defName,r.Column,id,kind,mode,0,0,false,0,0,0,e.x,e.y,e.z,e.w,p.Growth,0,0,false,description));
                    continue;
                }
                Require(pr.Mode==mode&&pr.Base.shader==DefDatabase<ShaderDef>.GetNamed(LifeSuiteExperiment.ShaderName).Shader&&pr.Base.GetTexture("_HnsAnatomy")!=null,"native shared anatomy material "+kind);
                var c=pr.Base.GetVector("_HnsLife");var live=pr.Base.GetVector("_HnsLifeEnv");
                Require(c.x==mode&&c.y==0&&c.z==(pr.Atlas?1f:0f)&&(live-e).sqrMagnitude<.00001f,"native shader mode and real environment controls");
                Require(pr.Territories>=2&&(!LifeSuiteMath.NeedsOrgan(mode)||pr.Organs>=1),"qualified source anatomy");
                Require(pr.Planes==(LifeSuiteMath.Overlay(mode)?1:0)&&(!LifeSuiteMath.Overlay(mode)||pr.Extra!=null),"bounded actual extra plane "+kind);
                Require(Math.Abs(pr.Growth-p.Growth)<.002f,"actual plant maturity encoded");
                if(mode==15&&panel==5)Require(pr.Lateral==1,"actual west vegetation context");
                if(mode==18)Require(pr.Lateral==2,"actual compatible east relay");
                if(mode==3&&panel==0||mode==17&&panel==7)Require(pr.Stress>.85f,"controlled temperature sample encoded");
                bool active=LifeSuiteMath.Active(mode,e.x,e.y,e.z,e.w,pr.Growth,pr.Stress,pr.Lateral);
                Require(active==description.Contains(PhenotypeVocabulary.Detail(kind).Phrase),"phenotype language matches active expression "+kind);
                LifeSuiteAcceptedModes.Add(mode);if(active)LifeSuiteActiveModes.Add(mode);if(pr.Atlas)LifeSuiteAtlasModes.Add(mode);
                LifeSuiteEvidence.Add(string.Join("\t",panel+1,p.def.defName,r.Column,id,kind,mode,pr.Base.GetInstanceID(),pr.Extra?.GetInstanceID()??0,pr.Atlas,pr.Planes,pr.Territories,pr.Organs,e.x,e.y,e.z,e.w,pr.Growth,pr.Stress,pr.Lateral,active,description));
            }
            Log.Message("[HNS LifeSuite] panel="+(panel+1)+"; accepted modes="+LifeSuiteAcceptedModes.Count+"; active="+LifeSuiteActiveModes.Count+"; atlas modes="+LifeSuiteAtlasModes.Count+"; maps="+LifeSuiteExperiment.MapCount+"; materials="+LifeSuiteExperiment.MaterialCount);
        }
        private static void WriteLifeSuite()
        {
            Require(inventory.Count==144&&inventory.Select(r=>r.Plant.thingIDNumber).Distinct().Count()==72&&LifeSuiteEvidence.Count==121,"four paired 18-plant gardens");
            Require(inventory.Where(r=>r.Cultivar==null).All(r=>Backend(r.Plant)=="Vanilla"),"original controls");
            Require(LifeSuiteAcceptedModes.Count==20&&LifeSuiteActiveModes.Count==20,"all twenty modes accepted and active in at least one native specimen");
            Require(LifeSuiteAtlasModes.SetEquals(new[]{2,11,19}),"three atlas-driven structural modes");
            if(lifeSuiteMotion)Require(LifeMotionEvidence.Count==1+8*LifeMotionFrames,"all native motion frames captured");
            File.WriteAllLines(Path.Combine(root,"HNS-garden-life-suite.tsv"),LifeSuiteEvidence);
            File.WriteAllLines(Path.Combine(root,"HNS-garden-life-suite-preparation.tsv"),new[]{"Species\tVariation\tSource\tAtlas mode\tFoliage territories\tOrgan count\tOrgan pixels\tDistinct frames\tChanged pixels"}.Concat(LifeSuiteExperiment.Preparation));
            Log.Message("[HNS LifeSuite] 20 modes; "+LifeSuiteExperiment.MapCount+" maps; "+LifeSuiteExperiment.MaterialCount+" materials; "+LifeSuiteEvidence.Count+" evidence lines");
        }
        private static void SetDetailTime()
        {
            float target=panel==1?0f:.5f;
            int advance=Mathf.RoundToInt(Mathf.Repeat(target-GenLocalDate.DayPercent(map),1f)*60000f);
            Find.TickManager.DebugSetTicksGame(Find.TickManager.TicksGame+advance);
            map.skyManager.SkyManagerUpdate();map.windManager.WindManagerTick();
        }
        private static readonly List<string> VascularEvidence=new(){"Panel\tSpecies\tColumn\tPlant ID\tTrait\tRegions\tTissue pixels\tVein pixels\tThin pixels\tSky glow\tLight\tWetness\tMaterial ID\tRenderer\tDescription"};
        private static readonly Dictionary<int,int> VascularInitialMaterials=new();
        private static int vascularMasks,vascularMaterials;
        private static void SampleVascular()
        {
            var styled=inventory.Where(r=>r.Panel==1&&r.Cultivar!=null).ToArray();
            Require(styled.Length==15,"living tissue fixture count");
            float sky=map.skyManager.CurSkyGlow,light=VascularMath.Light(sky),wet=VascularMath.Wet(map.weatherManager.RainRate);
            Require(panel==1?light<.1f:light>.95f,"native daylight cycle");
            if(panel==0){vascularMasks=VascularExperiment.MaskCount;vascularMaterials=VascularExperiment.MaterialCount;}
            Require(vascularMasks==VascularExperiment.MaskCount&&vascularMaterials==VascularExperiment.MaterialCount,"stable shared anatomy textures and materials");
            foreach(var r in styled)
            {
                var p=r.Plant;int id=p.thingIDNumber;
                var kind=r.Cultivar.traits.Single().botanicalDetail;
                var obs=p.TryGetComp<CompPlantVariety>().VisualObservation;
                string description=obs?.Prose()??"";
                if(VascularExperiment.Printed.TryGetValue(id,out var print))
                {
                    var m=print.Material;var a=print.Anatomy;var c=m.GetVector("_HnsTissue");
                    Require(print.Kind==kind&&m.shader==DefDatabase<ShaderDef>.GetNamed("HNS_VascularTissue").Shader&&m.mainTexture==print.Source&&m.GetTexture("_HnsAnatomy")!=null,"native original source and anatomy material");
                    Require(c.x==(int)kind-(int)BotanicalDetailKind.IvoryNerves+1&&Math.Abs(c.y-light)<.004f&&Math.Abs(c.z-wet)<.004f,"live material controls");
                    Require(a.Territories>=2&&a.VeinPixels>=10&&a.ThinPixels>=10,"derived internal anatomy");
                    if(panel==0)VascularInitialMaterials[id]=m.GetInstanceID();else Require(VascularInitialMaterials[id]==m.GetInstanceID(),"same material across native daylight changes");
                    Require(kind==BotanicalDetailKind.LivingGlass&&panel==1?description.Length==0:description.Contains(PhenotypeVocabulary.Detail(kind).Phrase),"actual phenotype expression description");
                    VascularEvidence.Add(string.Join("\t",panel+1,p.def.defName,r.Column,id,kind,a.Territories,a.TissuePixels,a.VeinPixels,a.ThinPixels,sky,c.y,c.z,m.GetInstanceID(),Backend(p),description));
                }
                else
                {
                    Require(VascularExperiment.Fallback.Contains(id),"explicit uncertain art fallback");
                    Require(!description.Contains(PhenotypeVocabulary.Detail(kind).Phrase),"fallback has no invented expression");
                    VascularEvidence.Add(string.Join("\t",panel+1,p.def.defName,r.Column,id,kind,0,0,0,0,sky,light,wet,0,Backend(p),description));
                }
            }
            Log.Message("[HNS Vascular] phase="+panel+"; light="+light+"; printed="+VascularExperiment.Printed.Count+"; fallback="+VascularExperiment.Fallback.Count+"; shared maps="+vascularMasks+"; materials="+vascularMaterials);
        }
        private static void WriteVascular()
        {
            Require(inventory.Count==54&&inventory.Select(r=>r.Plant.thingIDNumber).Distinct().Count()==18&&VascularEvidence.Count==46,"18 same plants across three states");
            Require(inventory.Where(r=>r.Cultivar==null).All(r=>Backend(r.Plant)=="Vanilla"),"untouched controls");
            Require(VascularExperiment.Printed.Count>=12&&VascularExperiment.Printed.Count+VascularExperiment.Fallback.Count>=15,"qualified anatomy or explicit fallback");
            File.WriteAllLines(Path.Combine(root,"HNS-garden-vascular.tsv"),VascularEvidence);
            File.WriteAllLines(Path.Combine(root,"HNS-garden-vascular-preparation.tsv"),new[]{"Species\tVariation\tSource\tWidth\tHeight\tTerritories\tTissue pixels\tVein pixels\tThin pixels"}.Concat(VascularExperiment.Preparation));
            Log.Message("[HNS Vascular] final native cases="+VascularExperiment.Printed.Count*3+"; maps="+vascularMasks+"; materials="+vascularMaterials);
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
            if(BotanicalExpressionEngine.Prints.ContainsKey(id))return "GPU composed botanical expression";
            if(LifeSuiteExperiment.Prints.ContainsKey(id))return "GPU botanical life suite";
            if(LifeSuiteExperiment.Fallback.Contains(id))return "Life suite unavailable, original path";
            if(VascularExperiment.Printed.ContainsKey(id))return "GPU living tissue atlas";
            if(VascularExperiment.Fallback.Contains(id))return "Tissue unavailable, original path";
            if(GroundHabitExperiment.PrintedPlants.ContainsKey(id))return "Botanical ground plane";
            if(groundGarden&&p.TryGetComp<CompPlantVariety>()?.ActiveTraits.Any(t=>GroundHabitMath.Valid(t.groundHabit))==true)return "Ground detail unavailable, original path";
            if(ResponsiveHabitExperiment.PrintedPlants.ContainsKey(id))return mosaicGarden?"GPU individual organ mosaic":articulationGarden?"GPU organ articulation":"GPU responsive UV pose";
            if(articulationGarden&&p.TryGetComp<CompPlantVariety>()?.ActiveTraits.Any(t=>ArticulationMath.Valid(t.responsiveHabit))==true)return "Articulation unavailable, original path";
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
            if(groundGarden)
            {
                var styled=inventory.Where(r=>r.Cultivar!=null).ToList();
                Require(inventory.Count==36&&styled.Count==30&&chosen.Count==6,"36 plants six species");
                Require(inventory.Where(r=>r.Cultivar==null).All(r=>Backend(r.Plant)=="Vanilla"),"six original controls");
                int expected=styled.Count(r=>r.Cultivar.traits.Single().groundHabit!=GroundHabitKind.PetalDrift||GroundHabitExperiment.FlowerSpecies(r.Plant.def));
                Require(expected==26&&GroundHabitExperiment.PrintedPlants.Count==expected,"26 ground details, four explicit nonflower fallbacks");
                foreach(var r in styled)
                {
                    bool eligible=r.Cultivar.traits.Single().groundHabit!=GroundHabitKind.PetalDrift||GroundHabitExperiment.FlowerSpecies(r.Plant.def);
                    Require(GroundHabitExperiment.PrintedPlants.ContainsKey(r.Plant.thingIDNumber)==eligible,"ground render eligibility");
                    if(eligible)
                    {
                        var p=GroundHabitExperiment.PrintedPlants[r.Plant.thingIDNumber];
                        Require(p.Material.shader==ShaderDatabase.Cutout&&Math.Abs(p.Center.y-AltitudeLayer.Filth.AltitudeFor())<.001f&&p.Size>=.95f&&p.Size<=1.55f,"actual ground layer and bounded size");
                    }
                }
                Require(WaylithPhenotypeShader.MaterialCount==0&&WaylithPhenotypeShader.MaskCount==0,"original canopy shaders and no GPU masks");
                File.WriteAllLines(Path.Combine(root,"HNS-garden-ground-preparation.tsv"),new[]{"Species\tVariation\tTexture\tTrait\tVisible pixels\tLeaf R\tLeaf G\tLeaf B\tPetal R\tPetal G\tPetal B"}.Concat(GroundHabitExperiment.Preparation));
                var evidence=new List<string>{"Panel\tRow\tColumn\tSpecies\tPlant ID\tTrait\tRenderer\tGround material ID\tAltitude\tSize"};
                foreach(var r in inventory)
                {
                    bool printed=GroundHabitExperiment.PrintedPlants.TryGetValue(r.Plant.thingIDNumber,out var detail);
                    evidence.Add(string.Join("\t",r.Panel,r.RowNumber,r.Column,r.Plant.def.defName,r.Plant.thingIDNumber,r.Cultivar?.traits.Single().defName??"Control",Backend(r.Plant),printed?detail.Material.GetInstanceID():0,printed?detail.Center.y:0f,printed?detail.Size:0f));
                }
                File.WriteAllLines(Path.Combine(root,"HNS-garden-ground.tsv"),evidence);
                Log.Message("[HNS Ground] rendered="+expected+"; fallback="+(styled.Count-expected)+"; materials="+GroundHabitExperiment.MaterialCount+"; one plane per eligible plant");return;
            }
            if(chronicleGarden){WriteChronicle();return;}
            if(neighborhoodGarden){WriteNeighborhood();return;}
            if(mosaicGarden){WriteMosaic();return;}
            if(poseGarden)
            {
                var styled=inventory.Where(r=>r.Cultivar!=null).ToList();
                Require(inventory.Count==18*PoseFrames&&styled.Count==15*PoseFrames&&inventory.Select(r=>r.Plant.thingIDNumber).Distinct().Count()==18,"same 18 plants in five poses");
                if(articulationGarden){WriteArticulation(styled);return;}
                Require(styled.All(r=>Backend(r.Plant)=="GPU responsive UV pose")&&inventory.Where(r=>r.Cultivar==null).All(r=>Backend(r.Plant)=="Vanilla"),"all pose cultivars and original controls");
                Require(PoseEvidence.Count==1+15*PoseFrames&&ResponsiveHabitExperiment.MaterialCount==poseInitialMaterials&&WaylithPhenotypeShader.MaterialCount==0&&WaylithPhenotypeShader.MaskCount==0,"complete pose evidence without material growth or masks");
                File.WriteAllLines(Path.Combine(root,"HNS-garden-pose.tsv"),PoseEvidence);
                Log.Message("[HNS Pose] verified="+(PoseEvidence.Count-1)+"; materials="+poseInitialMaterials+"; updates="+ResponsiveHabitExperiment.Updates);return;
            }
            if(architectureGarden)
            {
                var styled=inventory.Where(r=>r.Cultivar!=null).ToList();
                Require(inventory.Count==36&&styled.Count==30&&chosen.Count==6,"36 plants six species");
                Require(inventory.Where(r=>r.Cultivar==null).All(r=>Backend(r.Plant)=="Vanilla"),"six original controls");
                Require(styled.All(r=>Backend(r.Plant)=="Modular canopy planes"||Backend(r.Plant)=="Architecture unavailable, original path"),"every architecture case accounted for");
                Require(CanopyArchitectureExperiment.PrintedPlants.Count>=15,"at least 15 actual modular variants");
                Require(Directory.GetFiles(root,"HNS-garden-module-*.png").Length==CanopyArchitectureExperiment.PreparedCount,"same-source module diagnostics");
                Require(CanopyArchitectureExperiment.PlaneCounts.Values.All(n=>n>0&&n<=5),"bounded extra planes");
                Require(WaylithPhenotypeShader.MaterialCount==0&&WaylithPhenotypeShader.MaskCount==0,"original shader and no GPU masks");
                File.WriteAllLines(Path.Combine(root,"HNS-garden-architecture-preparation.tsv"),new[]{"Species\tVariation\tTexture\tWidth\tHeight\tLeaf W\tLeaf H\tFruit W\tFruit H\tAnchors\tReconstructed pixels\tUnreached reproductive pixels\tTapered edge pixels"}.Concat(CanopyArchitectureExperiment.Preparation));
                File.WriteAllLines(Path.Combine(root,"HNS-garden-architecture.tsv"),new[]{"Panel\tRow\tColumn\tSpecies\tPlant ID\tTrait\tRenderer\tAdditional planes"}.Concat(inventory.Select(r=>string.Join("\t",r.Panel,r.RowNumber,r.Column,r.Plant.def.defName,r.Plant.thingIDNumber,r.Cultivar?.traits.Single().defName??"Control",Backend(r.Plant),CanopyArchitectureExperiment.PlaneCounts.TryGetValue(r.Plant.thingIDNumber,out int n)?n:0))));
                Log.Message("[HNS Architecture] rendered="+CanopyArchitectureExperiment.PrintedPlants.Count+"; fallback="+(styled.Count-CanopyArchitectureExperiment.PrintedPlants.Count)+"; source modules="+CanopyArchitectureExperiment.PreparedCount+"; materials="+CanopyArchitectureExperiment.MaterialCount+"; additional planes="+CanopyArchitectureExperiment.PlaneCounts.Values.Sum());return;
            }
            if(expressionGarden){WriteExpression();return;}
            if(sculptureGarden){WriteSculpture();return;}
            if(lifeSuiteGarden){WriteLifeSuite();return;}
            if(vascularGarden){WriteVascular();return;}
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
            NeighborhoodExperiment.Enabled=false;
            ChronicleExperiment.Enabled=false;
            if(neighborhoodGarden||chronicleGarden)Prefs.PlantWindSway=previousNeighborhoodWind;
            OrganSpriteExperiment.Enabled = false;
            WindHabitUtility.Enabled=false;
            SurfaceFinishUtility.Enabled=false;
            BotanicalExpressionEngine.Enabled=false;
            BotanicalDetailExperiment.Enabled=false;
            VascularExperiment.Enabled=false;
            LifeSuiteExperiment.Enabled=false;LifeSuiteExperiment.ForgeEnabled=false;LifeSuiteExperiment.SculptureEnabled=false;LifeSuiteExperiment.SculpturePhase=0;LifeSuiteExperiment.TemperatureOverride=null;LifeSuiteExperiment.SeasonOverride=null;
            CanopyArchitectureExperiment.Enabled=false;
            ResponsiveHabitExperiment.Enabled=false;
            ResponsiveHabitExperiment.ArticulationEnabled=false;
            ResponsiveHabitExperiment.MosaicEnabled=false;
            GroundHabitExperiment.Enabled=false;
            if(groundGarden)Prefs.PlantWindSway=previousGroundWind;
            if(poseGarden)Prefs.PlantWindSway=previousPoseWind;
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
