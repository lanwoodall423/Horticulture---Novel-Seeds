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
    internal static class FieldGuideProbe
    {
        private static string root;
        private static Action<string> stage;
        private static Action<string,string> finish;
        private static bool active;
        private static int step,age,baselineDraw;
        private static string shot;
        private static Pawn farmer;
        private static Map map;
        private static Plant ready,growing,requested;
        private static Thing pack,recovered;
        private static VarietyRecord[] cultivars;
        private static Zone_Growing zone;
        private static HorticultureWorkspaceDocument workspace;
        private static int harvests;
        private static bool namingRetried;
        private static readonly List<string> evidence=new(){"Stage\tDiscovery count\tReady\tPacks\tPage\tRepaint count\tScreenshot"};
        [DllImport("user32.dll")]private static extern bool InvalidateRect(IntPtr h,IntPtr r,bool erase);
        [DllImport("user32.dll")]private static extern bool UpdateWindow(IntPtr h);
        [DllImport("user32.dll")]private static extern uint GetWindowThreadProcessId(IntPtr h,out uint owner);
        private static IntPtr hwnd;
        private static void Redraw()
        {
            if(!OperatingSystem.IsWindows())return;
            if(hwnd==IntPtr.Zero){using var p=System.Diagnostics.Process.GetCurrentProcess();hwnd=p.MainWindowHandle;}
            if(hwnd!=IntPtr.Zero&&GetWindowThreadProcessId(hwnd,out uint owner)>0&&owner==(uint)Environment.ProcessId&&InvalidateRect(hwnd,IntPtr.Zero,false))UpdateWindow(hwnd);
        }
        private static void Require(bool ok,string what){if(!ok)throw new InvalidOperationException(what);}
        private static void Reset(){age=0;shot=null;baselineDraw=HorticultureWorkspaceDocument.LastDrawnCount;}
        private static ThingDef Def(string n)=>DefDatabase<ThingDef>.GetNamed("Plant_"+n);
        private static VarietyTraitDef Trait(string n)=>DefDatabase<VarietyTraitDef>.GetNamed("HNS_"+n);
        private static IntVec3 Cell(ThingDef def)=>GenRadial.RadialCellsAround(farmer.Position,25,true).First(c=>c.InBounds(map)&&c.Standable(map)&&!c.Fogged(map)&&c.GetPlant(map)==null&&c.GetEdifice(map)==null&&c.GetZone(map)==null&&c.GetTerrain(map).fertility>=def.plant.fertilityMin&&farmer.CanReach(c,PathEndMode.Touch,Danger.Deadly));
        private static Plant Spawn(ThingDef def,float growth,VarietyTraitDef pending=null,VarietyRecord variety=null)
        {
            var p=(Plant)ThingMaker.MakeThing(def);p.Growth=growth;p.sown=true;GenSpawn.Spawn(p,Cell(def),map);
            var c=p.TryGetComp<CompPlantVariety>();if(pending!=null)c.SetPendingTraits(new(){pending});else c.SetVariety(variety);
            return p;
        }
        internal static void Begin(string dir,Action<string> onStage,Action<string,string> done)
        {
            root=dir;stage=onStage;finish=done;
            try
            {
                map=Find.CurrentMap;Require(map?.IsPlayerHome==true,"home map");
                farmer=map.mapPawns.FreeColonistsSpawned.Where(p=>p.skills!=null&&!p.Dead&&!p.Downed&&!p.InMentalState&&!p.WorkTypeIsDisabled(WorkTypeDefOf.PlantCutting)).OrderByDescending(p=>p.skills.GetSkill(SkillDefOf.Plants).Level).First();
                farmer.skills.GetSkill(SkillDefOf.Plants).Level=20;
                Find.TickManager.CurTimeSpeed=TimeSpeed.Paused;Find.Selector.ClearSelection();
                // Fixture starts from a new colony; do not display randomly
                // generated undiscovered plants as the empty-state example.
                foreach(var t in map.listerThings.ThingsInGroup(ThingRequestGroup.Plant).OfType<Plant>())
                    if(t.TryGetComp<CompPlantVariety>()?.PendingDiscovery==true)t.TryGetComp<CompPlantVariety>().ClearPendingDiscovery();
                workspace=MainTabWindow_CultivarRegistry.FixtureWorkspace();Require(workspace!=null,"actual native registry");
                workspace.FixtureOverview();FieldGuideIndex.RequestRefresh();Reset();active=true;
            }
            catch(Exception e){Fail(e);}
        }
        private static void Populate()
        {
            var reg=GameComponent_NovelSeeds.Instance;HorticultureNovelSeedsMod.Settings.enableBotanicalExpression=true;
            cultivars=new[]{
                reg.UnlockVariety(Def("Corn"),ExpressionMath.Recipe(0,0).Select(Trait).ToList(),"Amber Crown",originKind:"mutation"),
                reg.UnlockVariety(Def("Corn"),ExpressionMath.Recipe(0,1).Select(Trait).ToList(),"Silvercrest",originKind:"mutation"),
                reg.UnlockVariety(Def("Cotton"),ExpressionMath.Recipe(1,0).Select(Trait).ToList(),"Roseveil",originKind:"mutation"),
                reg.UnlockVariety(Def("Daylily"),ExpressionMath.Recipe(2,1).Select(Trait).ToList(),"Ivory Star",originKind:"mutation"),
                reg.UnlockVariety(Def("Corn"),new(){Trait("Giant")},"Tall Harvest",originKind:"mutation")
            };
            Require(cultivars.All(HorticulturePresentationPolicy.IsDocumented),"registered sample cultivars");
            cultivars[0].registryFavorite=true;
            for(int i=0;i<cultivars.Length;i++)Spawn(cultivars[i].cropDef,1,variety:cultivars[i]);
            ready=Spawn(Def("Corn"),1,Trait("HNS_Giant".Substring(4))); // duplicate genotype for later recovery check is avoided below.
            ready.TryGetComp<CompPlantVariety>().SetPendingTraits(new(){Trait("BronzeReticulum")});
            growing=Spawn(Def("Cotton"),.68f,Trait("WingedPetioles"));
            requested=Spawn(Def("Daylily"),1,Trait("SilverBloom"));
            Require(requested.TryGetComp<CompPlantVariety>().TryRequestSaveSeeds(),"existing preserve request");
            Require(NovelSeedUtility.DropDiscoverySeed(Def("Daylily"),new(){Trait("PearlCuticle")},Cell(Def("Daylily")),map),"existing recovered pack transaction");
            pack=map.listerThings.ThingsOfDef(HNS_DefOf.HNS_NovelSeedPack).Single();
            zone=new Zone_Growing(map.zoneManager);map.zoneManager.RegisterZone(zone);zone.AddCell(Cell(Def("Corn")));zone.SetPlantDefToGrow(Def("Corn"));zone.label="Heritage corn trial";
            PlantVarietySelectionUtility.SelectBreedingMix(new List<IPlantToGrowSettable>{zone},Def("Corn"),cultivars.Take(2));
            Require(reg.BreedingVarietiesFor(zone).Count==2,"actual existing growing-zone breeding mix");
            FieldGuideIndex.RequestRefresh();Reset();
        }
        private static bool Capture(string name,string page)
        {
            // Require a real Repaint of the requested page before requesting the
            // native screenshot. The isolated game may be unfocused.
            if(age<45||HorticultureWorkspaceDocument.LastDrawnCount<=baselineDraw||HorticultureWorkspaceDocument.LastDrawnPage!=page)return false;
            if(shot==null){shot=Path.Combine(root,"HNS-garden-guide-"+name+".png");ScreenCapture.CaptureScreenshot(shot);Redraw();return false;}
            if(!File.Exists(shot)||new FileInfo(shot).Length<1000)return false;
            var s=FieldGuideIndex.Current;
            evidence.Add(string.Join("\t",name,s?.Total??-1,s?.Ready??-1,s?.Packs??-1,page,HorticultureWorkspaceDocument.LastDrawnCount,Path.GetFileName(shot)));
            File.WriteAllLines(Path.Combine(root,"HNS-garden-guide-evidence.tsv"),evidence);
            return true;
        }
        private static void Next(){step++;Reset();}
        internal static void Update()
        {
            if(!active)return;
            try
            {
                if(Find.WindowStack.TryRemove(typeof(EditWindow_Log),false)){age=0;Redraw();return;}
                age++;Redraw();Require(age<2400,"field guide stage timed out "+step+"; page="+HorticultureWorkspaceDocument.LastDrawnPage+"; draw="+HorticultureWorkspaceDocument.LastDrawnCount);
                var s=FieldGuideIndex.Current;
                if(step==0)
                {
                    if(s==null)return;
                    Require(s.Total==0&&!GameComponent_NovelSeeds.Instance.AllVarieties.Any(HorticulturePresentationPolicy.IsDocumented),"empty guide authority");
                    if(!Capture("01-empty","Overview"))return;
                    stage("GuideEmpty");Populate();Next();return;
                }
                if(step==1)
                {
                    if(s?.Total!=4||s.Ready!=1||s.Packs!=1)return;
                    FieldGuideIndex.Portrait(cultivars[0],out bool rendered);if(!rendered)return;
                    if(!Capture("02-overview","Overview"))return;
                    stage("GuideOverview");workspace.PrepareDiscoveries();Next();return;
                }
                if(step==2)
                {
                    Require(s!=null&&s.Discoveries.Any(r=>r.Thing==growing&&r.Status==FieldGuideIndex.State.Growing)&&s.Discoveries.Any(r=>r.Thing==requested&&r.Status==FieldGuideIndex.State.Requested),"actual discovery statuses");
                    Require(!HorticulturePresentationPolicy.IsDocumented(ready.TryGetComp<CompPlantVariety>().Variety),"undocumented specimen remains unknown");
                    if(!Capture("03-inbox","Discoveries"))return;
                    stage("GuideInbox");workspace.PrepareCultivar(cultivars[0]);Next();return;
                }
                if(step==3)
                {
                    if(!Capture("04-cultivar","Cultivars"))return;
                    stage("GuideCultivar");workspace.FixtureCompare(cultivars.Take(2));Next();return;
                }
                if(step==4)
                {
                    if(!Capture("05-compare","Compare"))return;
                    stage("GuideComparison");workspace.PrepareBreeding();Next();return;
                }
                if(step==5)
                {
                    Require(s?.Zones.Any(z=>z.Zone==zone&&z.Mix.Length==2)==true,"real zone snapshot");
                    if(!Capture("06-breeding","Breeding"))return;
                    stage("GuideBreeding");
                    Require(!FieldGuideIndex.Preserve(growing)&&!FieldGuideIndex.Preserve(requested),"stale/immature preserve rejected");
                    Require(FieldGuideIndex.Preserve(ready),"same inbox preserve action");
                    Require(ready.TryGetComp<CompPlantVariety>().SaveSeedsRequested&&map.designationManager.DesignationOn(ready,DesignationDefOf.HarvestPlant)!=null,"actual harvest designation");
                    workspace.PrepareDiscoveries();Next();return;
                }
                if(step==6)
                {
                    if(s?.Discoveries.Any(r=>r.Thing==ready&&r.Status==FieldGuideIndex.State.Requested)!=true)return;
                    if(!Capture("07-preserved","Discoveries"))return;
                    stage("GuidePreservation");
                    harvests=GameComponent_CultivationJournal.Instance.ForSpecies(ready.def)?.harvests??0;
                    var giver=DefDatabase<WorkGiverDef>.GetNamed("PlantsCut").Worker as WorkGiver_PlantsCut;
                    var job=giver?.JobOnThing(farmer,ready,true);Require(job?.def==JobDefOf.HarvestDesignated&&job.targetA.Thing==ready,"real vanilla harvest job");
                    Require(farmer.jobs.TryTakeOrderedJob(job,JobTag.Misc),"accepted harvest job");
                    Find.TickManager.CurTimeSpeed=TimeSpeed.Ultrafast;Next();return;
                }
                if(step==7)
                {
                    if(!ready.Destroyed)return;
                    recovered=map.listerThings.ThingsOfDef(HNS_DefOf.HNS_NovelSeedPack).FirstOrDefault(t=>t.TryGetComp<CompNovelSeedPack>()?.CropDef==Def("Corn"));
                    Require(recovered!=null&&(GameComponent_CultivationJournal.Instance.ForSpecies(Def("Corn"))?.harvests??0)>harvests,"actual harvest, recovery and journal observation");
                    Find.TickManager.CurTimeSpeed=TimeSpeed.Paused;FieldGuideIndex.RequestRefresh();Next();return;
                }
                if(step==8)
                {
                    if(s?.Discoveries.Any(r=>r.Thing==recovered&&r.Status==FieldGuideIndex.State.Recovered)!=true)return;
                    if(!Capture("08-recovered","Discoveries"))return;
                    Require(FieldGuideIndex.OrderDocumentation(recovered,farmer),"same inbox documentation job");
                    Find.TickManager.CurTimeSpeed=TimeSpeed.Ultrafast;Next();return;
                }
                if(step==9)
                {
                    var dialog=Find.WindowStack.WindowOfType<Dialog_NameVariety>();if(dialog==null)return;
                    var naming=(IHorticultureNamingSurface)dialog;
                    Require(naming.TraitLabels.Count==0&&!recovered.Destroyed,"naming preserves undisclosed genes and pack");
                    naming.Name="First field guide discovery";
                    if(!namingRetried)
                    {
                        if(!Capture("09-naming","Discoveries"))return;
                        naming.Cancel();Require(!recovered.Destroyed,"cancel retains actual seed pack");
                        Require(FieldGuideIndex.OrderDocumentation(recovered,farmer),"actual retry after cancellation");
                        namingRetried=true;Find.TickManager.CurTimeSpeed=TimeSpeed.Ultrafast;Reset();return;
                    }
                    if(age<45)return;
                    naming.Save();Require(recovered.Destroyed,"actual naming registration consumes recovered pack");
                    var v=GameComponent_NovelSeeds.Instance.VarietiesFor(Def("Corn")).Single(v=>v.customName=="First field guide discovery");
                    Require(HorticulturePresentationPolicy.IsDocumented(v)&&GameComponent_CultivationJournal.Instance.ForCultivar(v)!=null,"documented cultivar and journal");
                    FieldGuideIndex.RequestRefresh();workspace.PrepareCultivar(v);Next();return;
                }
                if(step==10)
                {
                    if(s==null||s.Discoveries.Any(r=>r.Thing==recovered))return;
                    if(!Capture("10-documented","Cultivars"))return;
                    stage("GuideDocumentation");active=false;finish("PASS","actual native field guide pages and real preserve-harvest-seed-pack-documentation loop");
                }
            }
            catch(Exception e){Fail(e);}
        }
        private static void Fail(Exception e){active=false;finish("FAIL",e.ToString());}
    }
}
#endif
