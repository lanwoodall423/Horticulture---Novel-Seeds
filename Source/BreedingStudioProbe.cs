#if HNS_NATIVE_FIXTURE
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using RimWorld;
using LudeonTK;
using UnityEngine;
using Verse;
using Verse.AI;

namespace HorticultureNovelSeeds
{
    internal static class BreedingStudioProbe
    {
        private static string root,shot;
        private static Action<string> stage;
        private static Action<string,string> finish;
        private static Map map;
        private static Pawn farmer;
        private static ThingDef crop;
        private static Plant seedPlant,donorPlant,offspring;
        private static VarietyRecord seed,donor,documented;
        private static Zone_Growing zone;
        private static IntVec3 target;
        private static Thing pack;
        private static HorticultureWorkspaceDocument workspace;
        private static bool active;
        private static int step,age,baselineDraw,baselineSow,baselineHarvest;
        private static IntPtr hwnd;
        private static readonly List<string> evidence=new(){"Stage\tObservation\tScreenshot"};
        [DllImport("user32.dll")]private static extern bool InvalidateRect(IntPtr h,IntPtr r,bool erase);
        [DllImport("user32.dll")]private static extern bool UpdateWindow(IntPtr h);
        [DllImport("user32.dll")]private static extern uint GetWindowThreadProcessId(IntPtr h,out uint owner);
        private static void Redraw()
        {
            if(!OperatingSystem.IsWindows())return;
            if(hwnd==IntPtr.Zero){using var p=System.Diagnostics.Process.GetCurrentProcess();hwnd=p.MainWindowHandle;}
            if(hwnd!=IntPtr.Zero&&GetWindowThreadProcessId(hwnd,out uint owner)>0&&owner==(uint)Environment.ProcessId&&InvalidateRect(hwnd,IntPtr.Zero,false))UpdateWindow(hwnd);
        }
        private static void Require(bool ok,string message){if(!ok)throw new InvalidOperationException(message);}
        private static VarietyTraitDef Trait(string n)=>DefDatabase<VarietyTraitDef>.GetNamed("HNS_"+n);
        private static CompPlantVariety Comp(Plant p)=>p.TryGetComp<CompPlantVariety>();
        private static GameComponent_NovelSeeds Registry=>GameComponent_NovelSeeds.Instance;
        private static GameComponent_CultivationJournal Journal=>GameComponent_CultivationJournal.Instance;
        private static bool Clear(IntVec3 c)=>c.InBounds(map)&&c.Standable(map)&&!c.Fogged(map)&&c.GetEdifice(map)==null&&c.GetZone(map)==null&&map.roofGrid.RoofAt(c)==null;
        private static IEnumerable<IntVec3> Plot(IntVec3 c){for(int z=-3;z<=3;z++)for(int x=-5;x<=5;x++)yield return c+new IntVec3(x,0,z);}
        private static Plant Spawn(IntVec3 c,VarietyRecord v)
        {
            var p=(Plant)ThingMaker.MakeThing(crop);p.sown=true;p.Growth=1;Comp(p).SetVariety(v);p.HitPoints=p.MaxHitPoints;GenSpawn.Spawn(p,c,map);return p;
        }
        private static void Pause()=>Find.TickManager.CurTimeSpeed=TimeSpeed.Paused;
        private static void Run()=>Find.TickManager.CurTimeSpeed=TimeSpeed.Ultrafast;
        private static void Reset(){age=0;shot=null;baselineDraw=HorticultureWorkspaceDocument.LastDrawnCount;}
        private static void Next(){step++;Reset();}
        private static void ShowMap()
        {
            Find.MainTabsRoot.SetCurrentTab(MainButtonDefOf.Inspect,false);
            Find.Selector.ClearSelection();Find.CameraDriver.JumpToCurrentMapLoc(target);Find.CameraDriver.SetRootSize(5.4f);
        }
        private static bool Capture(string name,string observation,string page=null)
        {
            if(age<75||page!=null&&(HorticultureWorkspaceDocument.LastDrawnCount<=baselineDraw||HorticultureWorkspaceDocument.LastDrawnPage!=page))return false;
            if(shot==null){shot=Path.Combine(root,"HNS-garden-breeding-"+name+".png");ScreenCapture.CaptureScreenshot(shot);Redraw();return false;}
            if(!File.Exists(shot)||new FileInfo(shot).Length<1000)return false;
            evidence.Add(string.Join("\t",name,observation,Path.GetFileName(shot)));
            File.WriteAllLines(Path.Combine(root,"HNS-garden-breeding-evidence.tsv"),evidence);return true;
        }
        private static void Studio()
        {
            workspace=MainTabWindow_CultivarRegistry.FixtureWorkspace();Require(workspace!=null,"native workspace");workspace.FixtureStudio(seed,donor,zone);
        }
        internal static void Begin(string dir,Action<string> onStage,Action<string,string> done)
        {
            root=dir;stage=onStage;finish=done;
            try
            {
                map=Find.CurrentMap;Require(map?.IsPlayerHome==true,"isolated home map");crop=DefDatabase<ThingDef>.GetNamed("Plant_Corn");
                farmer=map.mapPawns.FreeColonistsSpawned.Where(p=>p.skills!=null&&!p.Dead&&!p.Downed&&!p.InMentalState&&!p.WorkTypeIsDisabled(WorkTypeDefOf.PlantCutting)&&!p.WorkTypeIsDisabled(WorkTypeDefOf.Growing)).OrderByDescending(p=>p.skills.GetSkill(SkillDefOf.Plants).Level).First();
                farmer.skills.GetSkill(SkillDefOf.Plants).Level=20;Pause();
                var settings=HorticultureNovelSeedsMod.Settings;
                settings.enableBotanicalExpression=true;settings.enableIndependentOrnamentalInheritance=true;
                settings.globalMutationChance=0;settings.globalCrossPollinationChance=1;settings.minimumDonorGrowth=.45f;settings.maxCrossPollinationTraits=1;
                int advance=Mathf.RoundToInt(Mathf.Repeat(.5f-GenLocalDate.DayPercent(map),1f)*60000f);Find.TickManager.DebugSetTicksGame(Find.TickManager.TicksGame+advance);
                var clear=WeatherDefOf.Clear;map.weatherManager.TransitionTo(clear);map.weatherManager.lastWeather=clear;map.weatherManager.curWeatherAge=4000;map.skyManager.SkyManagerUpdate();
                seed=Registry.UnlockVariety(crop,new(){Trait("HighYield")},"Golden Harvest",originKind:"mutation");
                donor=Registry.UnlockVariety(crop,new(){Trait("ColdResistant"),Trait("CrimsonNetwork")},"Crimson Frost",originKind:"mutation");
                var incompatible=Registry.UnlockVariety(crop,new(){Trait("IvoryNerves")},"Ivory line",originKind:"mutation");
                var hidden=Registry.UnlockVariety(crop,new(){Trait("SatinCuticle")},"Hidden test parent",hiddenFromMenus:true,originKind:"mutation");
                var other=Registry.UnlockVariety(DefDatabase<ThingDef>.GetNamed("Plant_Cotton"),new(){Trait("HighYield")},"Other species",originKind:"mutation");
                Require(!BreedingStudio.ValidPair(seed,hidden)&&!BreedingStudio.ValidPair(seed,other)&&!BreedingStudio.ValidPair(seed,seed),"hidden, species and same-parent rejection");
                Require(BreedingStudio.Describe(incompatible,donor).Candidates.Any(r=>r.Trait==Trait("CrimsonNetwork")&&r.Status.StartsWith("Conflicting")),"tissue channel conflict classified");
                var plan=BreedingStudio.Describe(seed,donor);
                Require(plan.Independent&&plan.Cap==1&&plan.Candidates.All(r=>r.Status=="Eligible candidate")&&plan.Example.Count()==3,"read-only compatible independent inheritance example");
                Require(!BreedingStudio.Outcomes(seed,donor,Registry.AllVarieties).Any(),"no invented outcome");
                var order=GameComponent_NovelSeeds.OrderBreedingMixVarieties(new[]{seed,donor});
                target=GenRadial.RadialCellsAround(farmer.Position,45,true).First(c=>Plot(c).All(Clear)&&farmer.CanReach(c,PathEndMode.Touch,Danger.Deadly)&&GameComponent_NovelSeeds.SelectBreedingMixVariety(order,c)==seed);
                var cells=Plot(target).ToHashSet();foreach(var t in map.listerThings.AllThings.Where(t=>t is Plant||t.def.category==ThingCategory.Item).Where(t=>cells.Contains(t.Position)).ToArray())if(t.Spawned)t.Destroy(DestroyMode.Vanish);
                foreach(var c in cells)map.terrainGrid.SetTerrain(c,TerrainDefOf.Soil);
                seedPlant=Spawn(target+new IntVec3(-3,0,0),seed);donorPlant=Spawn(target+new IntVec3(3,0,0),donor);
                zone=new Zone_Growing(map.zoneManager);map.zoneManager.RegisterZone(zone);zone.AddCell(target);zone.SetPlantDefToGrow(crop);zone.label="Corn breeding trial";
                // Open the actual native studio and choose documented parents via the same state.
                ShowMap();Studio();Reset();active=true;stage("StudioSetup");
            }
            catch(Exception e){Fail(e);}
        }
        private static bool ReadyPortraits()
        {
            var plan=BreedingStudio.Describe(seed,donor);
            BreedingStudio.Portrait(seed,null,seed.traits,out bool a);
            BreedingStudio.Portrait(donor,null,donor.traits,out bool b);
            BreedingStudio.Portrait(seed,donor,plan.Example,out bool c);return a&&b&&c;
        }
        private static void CheckNativeRender(Plant p)
        {
            Require(BotanicalExpressionEngine.Prints.TryGetValue(p.thingIDNumber,out var pr)&&BotanicalExpressionEngine.MeshPayload.GetValueOrDefault(p.thingIDNumber),"native offspring/parent renderer");
            var source=(Texture2D)pr.Material.mainTexture;int w=source.width,h=source.height;
            var reference=OrganSpriteExperiment.Read(source,w,h);
            bool arch=pr.Material.GetVector("_HnsExpression").x>.5f;
            var tex=BotanicalExpressionEngine.Readback(pr.Material,0,pr.Seed,0,pr.Material.GetVector("_HnsExpressionEnv"));
            try
            {
                var actual=tex.GetPixels32();int W=tex.width,H=tex.height,ox=(W-w)/2,oy=(H-h)/2;
                var r=new int[h];var a=new int[h];
                for(int y=0;y<h;y++)for(int x=0;x<w;x++){if(reference[y*w+x].a>=90)r[y]++;if(actual[(y+oy)*W+x+ox].a>=90)a[y]++;}
                int upright=0,inverted=0;for(int y=0;y<h;y++){upright+=Math.Abs(a[y]-r[y]);inverted+=Math.Abs(a[y]-r[h-1-y]);}
                Require(r.Sum()>30&&upright<r.Sum()*.3f&&upright+r.Sum()*.025f<inverted,"upright native source parity");
            }
            finally{UnityEngine.Object.Destroy(tex);}
        }
        private static void RecordMature()
        {
            foreach(var p in new[]{seedPlant,donorPlant,offspring})
            {
                CheckNativeRender(p);
                var pr=BotanicalExpressionEngine.Prints[p.thingIDNumber];
                var tex=BotanicalExpressionEngine.Readback(pr.Material,pr.Band,pr.Seed,pr.Flipped?-pr.Bias:pr.Bias,pr.Material.GetVector("_HnsExpressionEnv"));
                try{File.WriteAllBytes(Path.Combine(root,"HNS-garden-breeding-gpu-"+(p==seedPlant?"seed":p==donorPlant?"donor":"offspring")+".png"),tex.EncodeToPNG());}finally{UnityEngine.Object.Destroy(tex);}
            }
        }
        internal static void Update()
        {
            if(!active)return;
            try
            {
                if(Find.WindowStack.TryRemove(typeof(EditWindow_Log),false)){age=0;Redraw();return;}
                age++;Redraw();Require(age<3000,"studio stage timeout "+step+" job="+farmer?.CurJobDef+" page="+HorticultureWorkspaceDocument.LastDrawnPage);
                if(step==0)
                {
                    if(!ReadyPortraits()||!Capture("01-studio","Actual native parent cards, directional candidate classification and conditional GPU preview","Breeding"))return;
                    stage("StudioPreview");
                    Require(BreedingStudio.Apply(zone,seed,donor)&&Registry.VarietyForSowing(zone,target)==seed,"actual mix applied with deterministic seed cell");
                    workspace.FixtureStudio(seed,donor,zone);Next();return;
                }
                if(step==1)
                {
                    if(FieldGuideIndex.Current?.Zones.Any(z=>z.Zone==zone&&z.Mix.Length==2)!=true||!ReadyPortraits()||!Capture("02-mix","Actual zone assignment contains both documented parents","Breeding"))return;
                    stage("StudioMix");
                    ShowMap();Next();return;
                }
                if(step==2)
                {
                    if(BotanicalExpressionEngine.PendingCount>0||!Capture("03-parents","Actual mature seed parent left, empty recipient center, pollen donor right"))return;
                    baselineSow=Journal.ForSpecies(crop)?.sowings??0;
                    var giver=DefDatabase<WorkGiverDef>.AllDefsListForReading.Select(d=>d.Worker).OfType<WorkGiver_GrowerSow>().FirstOrDefault();
                    var job=giver?.JobOnCell(farmer,target,true);
                    Require(job!=null&&job.plantDefToSow==crop,"real vanilla sow job");
                    Require(farmer.jobs.TryTakeOrderedJob(job,JobTag.Misc),"accepted sow job");
                    Run();Next();return;
                }
                if(step==3)
                {
                    var plant=target.GetPlant(map);if(plant==null||Comp(plant)?.CrossPollinated!=true)return;
                    offspring=plant;var c=Comp(offspring);
                    Require(c.Variety==seed&&c.CrossPollinationParent==donor&&c.DiscoveryTraits.Count==3&&c.DiscoveryTraits.Contains(Trait("ColdResistant"))&&c.DiscoveryTraits.Contains(Trait("CrimsonNetwork")),"actual sow completion inherited both budgets");
                    Require((Journal.ForSpecies(crop)?.sowings??0)>baselineSow,"real journal sow completion");
                    Require(!BreedingStudio.Outcomes(seed,donor,Registry.AllVarieties).Any(),"pending offspring not exposed");
                    Pause();stage("StudioSow");Next();return;
                }
                if(step==4)
                {
                    if(!Capture("04-sown","Actual cross-pollinated seedling from real pawn sowing job; genotype not documented"))return;
                    offspring.Growth=1;Next();return;
                }
                if(step==5)
                {
                    if(BotanicalExpressionEngine.PendingCount>0||new[]{seedPlant,donorPlant,offspring}.Any(p=>!BotanicalExpressionEngine.Prints.TryGetValue(p.thingIDNumber,out var pr)||pr.Band!=ExpressionMath.Band(p.Growth)))return;
                    if(shot==null)RecordMature();
                    if(!Capture("05-mature","Actual mature parents and undocumented offspring; native upright GPU parity for all three"))return;
                    stage("StudioOrientation");workspace=MainTabWindow_CultivarRegistry.FixtureWorkspace();workspace.PrepareDiscoveries();FieldGuideIndex.RequestRefresh();Next();return;
                }
                if(step==6)
                {
                    if(FieldGuideIndex.Current?.Discoveries.Any(d=>d.Thing==offspring&&d.Status==FieldGuideIndex.State.Ready)!=true)return;
                    if(!Capture("06-discovery","Unusual specimen in real discovery inbox; no exact genotype/donor disclosed","Discoveries"))return;
                    Require(FieldGuideIndex.Preserve(offspring),"actual preserve request");baselineHarvest=Journal.ForSpecies(crop)?.harvests??0;
                    var giver=DefDatabase<WorkGiverDef>.GetNamed("PlantsCut").Worker as WorkGiver_PlantsCut;
                    var job=giver?.JobOnThing(farmer,offspring,true);
                    Require(job?.def==JobDefOf.HarvestDesignated&&job.targetA.Thing==offspring,"real harvest job");
                    Require(farmer.jobs.TryTakeOrderedJob(job,JobTag.Misc),"accepted harvest job");Run();Next();return;
                }
                if(step==7)
                {
                    if(!offspring.Destroyed)return;
                    pack=map.listerThings.ThingsOfDef(HNS_DefOf.HNS_NovelSeedPack).FirstOrDefault(t=>t.TryGetComp<CompNovelSeedPack>()?.ParentVarietyIds.ToHashSet().SetEquals(new[]{seed.id,donor.id})==true);
                    Require(pack!=null&&(Journal.ForSpecies(crop)?.harvests??0)>baselineHarvest,"real harvest produced actual lineage seed pack and journal entry");
                    Pause();FieldGuideIndex.RequestRefresh();Next();return;
                }
                if(step==8)
                {
                    if(FieldGuideIndex.Current?.Discoveries.Any(d=>d.Thing==pack&&d.Status==FieldGuideIndex.State.Recovered)!=true)return;
                    if(!Capture("07-pack","Actual recovered seed pack in discovery inbox; undocumented genetics","Discoveries"))return;
                    stage("StudioPreservation");
                    Require(FieldGuideIndex.OrderDocumentation(pack,farmer),"actual documentation job");Run();Next();return;
                }
                if(step==9)
                {
                    var dialog=Find.WindowStack.WindowOfType<Dialog_NameVariety>();if(dialog==null)return;
                    var naming=(IHorticultureNamingSurface)dialog;
                    Require(naming.TraitLabels.Count==0&&!pack.Destroyed,"undocumented naming does not disclose genes");
                    naming.Name="Crimson Harvest";
                    if(!Capture("08-naming","Actual naming dialog from pawn documentation job","Discoveries"))return;
                    naming.Save();Require(pack.Destroyed,"documented seed pack consumed");
                    documented=Registry.VarietiesFor(crop).Single(v=>v.customName=="Crimson Harvest");
                    Require(HorticulturePresentationPolicy.IsDocumented(documented)&&documented.generation==1&&documented.parentVarietyIds.ToHashSet().SetEquals(new[]{seed.id,donor.id})&&documented.traits.Count==3&&Journal.ForCultivar(documented)!=null,"actual registered genotype, lineage and journal");
                    Require(BreedingStudio.Outcomes(seed,donor,Registry.AllVarieties).Single()==documented,"studio outcome uses documented registry");
                    Pause();Studio();workspace.FixtureStudioOutcomes();FieldGuideIndex.RequestRefresh();Next();return;
                }
                if(step==10)
                {
                    if(!Capture("09-outcome","Actual documented offspring listed by parent IDs, not a predicted result","Breeding"))return;
                    workspace.PrepareCultivar(documented);Next();return;
                }
                if(step==11)
                {
                    if(!Capture("10-dossier","Documented offspring dossier and inherited traits","Cultivars"))return;
                    stage("StudioDocumentation");active=false;finish("PASS","native Breeding Studio, actual zone mix, real pawn sow and cross, upright native rendering, actual harvest/seed pack and documentation, documented lineage outcome");
                }
            }
            catch(Exception e){Fail(e);}
        }
        private static void Fail(Exception e){active=false;finish("FAIL",e.ToString());}
    }
}
#endif
