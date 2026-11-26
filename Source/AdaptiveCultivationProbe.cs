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
using A=HorticultureNovelSeeds.AdaptivePhenotypeMath;

namespace HorticultureNovelSeeds
{
    internal static class AdaptiveCultivationProbe
    {
        private static string root,shot;
        private static Action<string> stage;
        private static Action<string,string> finish;
        private static Map map;
        private static Pawn farmer;
        private static bool active;
        private static int phase,age;
        private static IntVec3 origin;
        private static IntPtr hwnd;
        private static readonly List<Plant> plants=new();
        private static readonly List<string> evidence=new(){"Phase\tSpecies\tColumn\tPlant ID\tGrowth\tCues\tResource blocked\tHarvest cycle\tRegrown\tYield factor\tGrowth factor\tCold offset\tHeat offset\tGrowth rate\tMaterial\tMesh payload\tPhenotype\tDocumented explanation"};
        private static readonly string[][] recipes={Array.Empty<string>(),new[]{"HighYield"},new[]{"ColdResistant"},new[]{"HeatResistant"},new[]{"ResourceDependent_Mulch"},new[]{"Perennial"},new[]{"HighYield","IvoryNerves","SatinCuticle"}};
        private static Thing wood;
        private static int woodBefore;
        private static float supplyGrowth;
        [DllImport("user32.dll")]private static extern bool InvalidateRect(IntPtr h,IntPtr r,bool erase);
        [DllImport("user32.dll")]private static extern bool UpdateWindow(IntPtr h);
        [DllImport("user32.dll")]private static extern uint GetWindowThreadProcessId(IntPtr h,out uint owner);
        private static void Redraw()
        {
            if(!OperatingSystem.IsWindows())return;
            if(hwnd==IntPtr.Zero){using var p=System.Diagnostics.Process.GetCurrentProcess();hwnd=p.MainWindowHandle;}
            if(hwnd!=IntPtr.Zero&&GetWindowThreadProcessId(hwnd,out uint owner)>0&&owner==(uint)Environment.ProcessId&&InvalidateRect(hwnd,IntPtr.Zero,false))UpdateWindow(hwnd);
        }
        private static void Require(bool ok,string what){if(!ok)throw new InvalidOperationException(what);}
        private static bool Clear(IntVec3 p)=>p.InBounds(map)&&p.GetEdifice(map)==null&&p.GetZone(map)==null&&!p.Fogged(map)&&map.roofGrid.RoofAt(p)==null&&(p.Standable(map)||p.GetPlant(map)!=null);
        private static IEnumerable<IntVec3> Plot(IntVec3 c){for(int z=0;z<8;z++)for(int x=0;x<20;x++)yield return c+new IntVec3(x,0,z);}
        private static VarietyTraitDef Trait(string name)=>DefDatabase<VarietyTraitDef>.GetNamed("HNS_"+name);
        private static CompPlantVariety Comp(Plant p)=>p.TryGetComp<CompPlantVariety>();
        private static Plant Corn(int col)=>plants[col];
        private static void Reset(){age=0;shot=null;}
        private static void Pause()=>Find.TickManager.CurTimeSpeed=TimeSpeed.Paused;
        private static void Camera()
        {
            Find.Selector.ClearSelection();Find.CameraDriver.JumpToCurrentMapLoc(origin+new IntVec3(9,0,3));Find.CameraDriver.SetRootSize(8.4f);
        }
        internal static void Begin(string dir,Action<string> onStage,Action<string,string> completed)
        {
            root=dir;stage=onStage;finish=completed;
            try
            {
                map=Find.CurrentMap;Require(map?.IsPlayerHome==true,"home map");
                var settings=HorticultureNovelSeedsMod.Settings;Require(!settings.enableBotanicalExpression,"opt-in beta");settings.enableBotanicalExpression=true;
                farmer=map.mapPawns.FreeColonistsSpawned.Where(p=>p.skills!=null&&!p.Dead&&!p.Downed&&!p.InMentalState&&!p.WorkTypeIsDisabled(WorkTypeDefOf.PlantCutting)).OrderByDescending(p=>p.skills.GetSkill(SkillDefOf.Plants).Level).First();
                farmer.skills.GetSkill(SkillDefOf.Plants).Level=20;Pause();
                int advance=Mathf.RoundToInt(Mathf.Repeat(.5f-GenLocalDate.DayPercent(map),1f)*60000f);
                Find.TickManager.DebugSetTicksGame(Find.TickManager.TicksGame+advance);
                var clear=WeatherDefOf.Clear;map.weatherManager.TransitionTo(clear);map.weatherManager.lastWeather=clear;map.weatherManager.curWeatherAge=4000;map.skyManager.SkyManagerUpdate();
                origin=GenRadial.RadialCellsAround(farmer.Position,70,true).Where(c=>Plot(c).All(Clear)&&farmer.CanReach(c+new IntVec3(12,0,5),PathEndMode.Touch,Danger.Deadly)).First();
                var area=Plot(origin).ToHashSet();foreach(var t in map.listerThings.AllThings.Where(t=>t is Plant||t.def.category==ThingCategory.Item).Where(t=>area.Contains(t.Position)).ToArray())if(t.Spawned)t.Destroy(DestroyMode.Vanish);
                foreach(var cell in area)map.terrainGrid.SetTerrain(cell,TerrainDefOf.Soil);
                var reg=GameComponent_NovelSeeds.Instance;
                string[] species={"Corn","Cotton"};
                for(int r=0;r<2;r++)
                {
                    var def=DefDatabase<ThingDef>.GetNamed("Plant_"+species[r]);
                    for(int c=0;c<recipes.Length;c++)
                    {
                        var p=(Plant)ThingMaker.MakeThing(def);p.Growth=c==4?.25f:.65f;p.sown=true;
                        var v=c==0?null:reg.UnlockVariety(def,recipes[c].Select(Trait).ToList(),species[r]+" "+string.Join(" + ",recipes[c]),originKind:"fixture-comparison");
                        var comp=Comp(p);comp.SetVariety(v);p.HitPoints=p.MaxHitPoints;
                        GenSpawn.Spawn(p,origin+new IntVec3(c*3,0,5-r*4),map);plants.Add(p);
                    }
                }
                // A separate undocumented specimen is used for the authority test.
                var pending=(Plant)ThingMaker.MakeThing(Corn(0).def);pending.Growth=1;pending.sown=true;Comp(pending).SetPendingTraits(new(){Trait("ColdResistant")});GenSpawn.Spawn(pending,origin+new IntVec3(1,0,3),map);plants.Add(pending);
                Require(Comp(Corn(4)).NeedsResource&&Corn(4).GrowthRate==0,"existing resource gate blocks actual growth");
                Require(Math.Abs(NovelSeedUtility.YieldFactor(Comp(Corn(1)))-1.15f)<.001f&&Math.Abs(Comp(Corn(1)).GrowthRateFactor-.9f)<.001f,"unchanged yield tradeoff");
                Require(Comp(Corn(2)).ColdGrowthOffset==-6&&Comp(Corn(2)).HeatGrowthOffset==-3&&Comp(Corn(3)).HeatGrowthOffset==6&&Comp(Corn(3)).ColdGrowthOffset==3,"unchanged thermal tradeoffs");
                Require(Math.Abs(NovelSeedUtility.YieldFactor(Comp(Corn(5)))-.70f)<.001f&&NovelSeedUtility.PerennialHarvestAfterGrowth(Comp(Corn(5)))==.30f,"unchanged perennial tradeoffs");
                stage("AdaptiveSetup");Camera();Reset();active=true;
            }
            catch(Exception e){Fail(e);}
        }
        private static bool Ready()
        {
            if(BotanicalExpressionEngine.PendingCount!=0||age<100)return false;
            foreach(var p in plants)
            {
                if(!Comp(p).HasAnyTraits)continue;
                int col=plants.IndexOf(p)%7;var expected=A.From(Comp(p).ActiveTraits.Select(t=>t.defName));
                if(!BotanicalExpressionEngine.Prints.TryGetValue(p.thingIDNumber,out var pr))
                {if((expected&A.Cue.Yield)!=0&&p.Growth<.8f)continue;return false;}
                if(pr.Band!=ExpressionMath.Band(p.Growth)||pr.Function.Blocked!=Comp(p).NeedsResource||pr.Function.Regrown!=(Comp(p).HarvestCycle>0&&(expected&A.Cue.Perennial)!=0)||!BotanicalExpressionEngine.MeshPayload.GetValueOrDefault(p.thingIDNumber))return false;
                if((expected&A.Cue.Yield)!=0&&p.Growth<.8f)expected&=~A.Cue.Yield;
                if((pr.Function.Cues&expected)!=expected)return false;
            }
            return true;
        }
        private static void Record(string name)
        {
            foreach(var p in plants)
            {
                int col=plants.IndexOf(p);var c=Comp(p);
                bool accepted=BotanicalExpressionEngine.Prints.TryGetValue(p.thingIDNumber,out var pr);
                if(accepted)
                {
                    var tex=BotanicalExpressionEngine.Readback(pr.Material,pr.Band,pr.Seed,pr.Flipped?-pr.Bias:pr.Bias,pr.Material.GetVector("_HnsExpressionEnv"));
                    try
                    {
                        Require(tex.GetPixels32().Count(c0=>c0.r+c0.g+c0.b>24)>30,"nonblank native GPU material");
                        File.WriteAllBytes(Path.Combine(root,"HNS-garden-adaptive-gpu-"+name+"-"+col.ToString("D2")+".png"),tex.EncodeToPNG());
                    }
                    finally{UnityEngine.Object.Destroy(tex);}
                }
                evidence.Add(string.Join("\t",name,p.def.defName,col,p.thingIDNumber,p.Growth,accepted?pr.Function.Cues.ToString():"none",c.NeedsResource,c.HarvestCycle,accepted&&pr.Function.Regrown,NovelSeedUtility.YieldFactor(c),c.GrowthRateFactor,c.ColdGrowthOffset,c.HeatGrowthOffset,p.GrowthRate,accepted?pr.Material.GetInstanceID():0,accepted&&BotanicalExpressionEngine.MeshPayload.GetValueOrDefault(p.thingIDNumber),c.VisualObservation?.Prose()??"",AdaptivePhenotypePresentation.Explain(c)?.Replace("\n","; ")??""));
            }
            Require(string.IsNullOrEmpty(AdaptivePhenotypePresentation.Explain(Comp(plants[14]))),"undocumented cue has no genetic explanation");
            File.WriteAllLines(Path.Combine(root,"HNS-garden-adaptive.tsv"),evidence);
        }
        private static bool Capture(string name,bool gpu=true)
        {
            if(shot==null)
            {
                if(gpu)Record(name);
                shot=Path.Combine(root,"HNS-garden-adaptive-"+name+".png");ScreenCapture.CaptureScreenshot(shot);Redraw();return false;
            }
            return File.Exists(shot)&&new FileInfo(shot).Length>1000&&age>=125;
        }
        private static void StartSupply()
        {
            var p=Corn(4);supplyGrowth=p.Growth;
            var cell=p.Position+new IntVec3(0,0,-1);
            wood=ThingMaker.MakeThing(ThingDefOf.WoodLog);wood.stackCount=1;woodBefore=wood.stackCount;GenSpawn.Spawn(wood,cell,map);
            var giver=DefDatabase<WorkGiverDef>.GetNamed("HNS_FertilizeNovelPlant").Worker as WorkGiver_FertilizeNovelPlant;
            Require(giver?.HasJobOnThing(farmer,p,true)==true,"ordinary resource job available");
            var job=giver.JobOnThing(farmer,p,true);Require(job?.def==HNS_DefOf.HNS_FertilizePlant,"actual fertilizer job");wood=job.targetB.Thing;woodBefore=wood.stackCount;
            Require(farmer.jobs.TryTakeOrderedJob(job,JobTag.Misc),"resource job accepted");Find.TickManager.CurTimeSpeed=TimeSpeed.Ultrafast;
        }
        private static void StartHarvest()
        {
            var p=Corn(5);map.designationManager.AddDesignation(new Designation(p,DesignationDefOf.HarvestPlant));
            var giver=DefDatabase<WorkGiverDef>.GetNamed("PlantsCut").Worker as WorkGiver_PlantsCut;
            var job=giver?.JobOnThing(farmer,p,true);Require(job?.def==JobDefOf.HarvestDesignated&&job.targetA.Thing==p,"actual vanilla perennial harvest job");
            Require(farmer.jobs.TryTakeOrderedJob(job,JobTag.Misc),"harvest job accepted");Find.TickManager.CurTimeSpeed=TimeSpeed.Ultrafast;
        }
        internal static void Update()
        {
            if(!active)return;
            try
            {
                age++;Redraw();
                if(Find.WindowStack.TryRemove(typeof(EditWindow_Log),false)){age=0;Redraw();return;}
                if(age>=2600)
                {
                    var states=plants.Select(p=>{var c=Comp(p);return p.thingIDNumber+":"+(BotanicalExpressionEngine.Prints.TryGetValue(p.thingIDNumber,out var pr)?pr.Function.Cues+":"+pr.Band+":"+pr.Function.Blocked+":"+pr.Function.Regrown:"fallback/eligible="+BotanicalExpressionEngine.CanAttempt(c)+"/compatible="+BotanicalExpressionEngine.CompatibleVisual(c,NovelSeedUtility.ResolveVisualParameters(c))+"/cues="+BotanicalExpressionEngine.FunctionalCues(c));});
                    throw new InvalidOperationException("adaptive timeout "+phase+" states "+string.Join(";",states)+"; jobs="+BotanicalExpressionEngine.PrepareJobs+"; sources="+BotanicalExpressionEngine.SourceCount+"; materials="+BotanicalExpressionEngine.MaterialCount);
                }
                if(phase==0)
                {
                    if(!Ready()||!Capture("01-young"))return;
                    for(int i=0;i<plants.Count-1;i++)if(i%7!=4)plants[i].Growth=1;
                    phase=1;Reset();return;
                }
                if(phase==1)
                {
                    if(!Ready()||!Capture("02-mature"))return;
                    Require(BotanicalExpressionEngine.Prints[Corn(6).thingIDNumber].Decision.Plan.Count==2,"mechanical high-yield and two ornamental channels accepted");
                    stage("AdaptiveVisuals");StartSupply();phase=2;Reset();return;
                }
                if(phase==2)
                {
                    if(Comp(Corn(4)).NeedsResource)return;
                    Pause();Require(wood.Destroyed||wood.stackCount<woodBefore||!wood.Spawned,"actual resource carried and paid");
                    // Hold the identical growth stage for the before/after
                    // visual comparison; only the actual job changed the gate.
                    Corn(4).Growth=supplyGrowth;phase=3;Reset();return;
                }
                if(phase==3)
                {
                    if(!Ready()||!Capture("03-supplied"))return;
                    Require(!BotanicalExpressionEngine.Prints[Corn(4).thingIDNumber].Function.Blocked,"resource cue removed on actual paid state");
                    stage("AdaptiveResource");StartHarvest();phase=4;Reset();return;
                }
                if(phase==4)
                {
                    var p=Corn(5);if(Comp(p).HarvestCycle==0)return;
                    Pause();Require(p.Spawned&&!p.Destroyed&&p.Growth>=.29f&&p.Growth<.5f,"real perennial survival and reset");
                    phase=5;Reset();return;
                }
                if(phase==5)
                {
                    if(!Ready()||!Capture("04-regrowth"))return;
                    Require(BotanicalExpressionEngine.Prints[Corn(5).thingIDNumber].Function.Regrown,"real harvest-cycle-driven tissue");
                    Corn(5).Growth=1;Corn(4).Growth=1;phase=6;Reset();return;
                }
                if(phase==6)
                {
                    if(!Ready()||!Capture("05-recovered"))return;
                    stage("AdaptivePerennial");
                    Find.Selector.ClearSelection();Find.Selector.Select(Corn(4));Find.MainTabsRoot.SetCurrentTab(MainButtonDefOf.Inspect,false);
                    var tab=(MainTabWindow_Inspect)MainButtonDefOf.Inspect.TabWindow;
                    tab.OpenTabType=typeof(ITab_PlantPhenotype);ITab_PlantPhenotype.LastDrawnId=-1;phase=7;Reset();return;
                }
                if(phase==7)
                {
                    if(ITab_PlantPhenotype.LastDrawnId!=Corn(4).thingIDNumber||age<60)return;
                    if(!Capture("06-phenotype",false))return;
                    Require(AdaptivePhenotypePresentation.Explain(Comp(Corn(4))).Contains("supplied"),"documented explanation reflects paid state");
                    stage("AdaptiveInspector");active=false;finish("PASS","five gameplay-linked cue families, actual resource job, perennial harvest and native phenotype inspector");
                }
            }
            catch(Exception e){Fail(e);}
        }
        private static void Fail(Exception e){active=false;finish("FAIL",e.ToString());}
    }
}
#endif
