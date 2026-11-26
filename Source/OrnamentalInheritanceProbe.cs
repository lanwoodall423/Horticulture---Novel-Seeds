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
using E=HorticultureNovelSeeds.ExpressionMath;

namespace HorticultureNovelSeeds
{
    internal static class OrnamentalInheritanceProbe
    {
        private static string root,shot;
        private static Action<string> stage;
        private static Action<string,string> finish;
        private static Map map;
        private static bool active;
        private static int phase,age;
        private static IntPtr hwnd;
        private static IntVec3 origin;
        private static readonly List<Plant> subjects=new();
        private static readonly List<string> evidence=new(){"Species\tMother\tDonor\tOffspring\tMechanical\tOrnamental\tYield factor\tGrowth factor\tCold offset\tHeat offset"};
        private static readonly List<string> renders=new(){"Phase\tSpecies\tPlant ID\tCultivar ID\tGrowth\tChannels\tSeed\tNative mesh\tImage"};
        private static readonly float[] growth={.40f,.65f,.85f,1f};
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
        private static VarietyTraitDef Trait(string id)=>DefDatabase<VarietyTraitDef>.GetNamed("HNS_"+id);
        private static CompPlantVariety Comp(Plant p)=>p.TryGetComp<CompPlantVariety>();
        private static bool Candidate(IntVec3 p)=>p.InBounds(map)&&p.GetEdifice(map)==null&&p.GetZone(map)==null&&!p.Fogged(map)&&map.roofGrid.RoofAt(p)==null&&(p.Standable(map)||p.GetPlant(map)!=null);
        private static IEnumerable<IntVec3> Plot(IntVec3 c){for(int z=0;z<10;z++)for(int x=0;x<13;x++)yield return c+new IntVec3(x,0,z);}
        private static Plant Spawn(ThingDef def,IntVec3 cell,float g,VarietyRecord variety=null)
        {
            Require(cell.GetPlant(map)==null&&Candidate(cell),"clear isolated cell "+cell);
            var p=(Plant)ThingMaker.MakeThing(def);p.sown=true;p.Growth=g;Comp(p).SetVariety(variety);p.HitPoints=p.MaxHitPoints;GenSpawn.Spawn(p,cell,map);return p;
        }
        private static List<VarietyTraitDef> Draw(int seed,ThingDef crop,List<VarietyTraitDef> maternal,List<VarietyTraitDef> donor,NovelSeedsSettings settings,int maximum)
        {
            Rand.PushState(seed);
            try{return NovelSeedUtility.CrossPollinationAdditions(maternal,donor,crop,settings,false,maximum,null,new());}
            finally{Rand.PopState();}
        }
        private static void Parity(NovelSeedsSettings s)
        {
            var corn=DefDatabase<ThingDef>.GetNamed("Plant_Corn");
            var neutral=E.Library.Select(a=>Trait(a.Id)).ToArray();
            Require(neutral.All(OrnamentalInheritance.Pure),"all eighteen promoted stock definitions are truly neutral");
            Require(!OrnamentalInheritance.Pure(new VarietyTraitDef{defName="HNS_IvoryNerves",balanceValueExplicit=true,yieldFactor=1.2f}),"mechanical modifier on ornamental definition rejected");
            Require(!OrnamentalInheritance.Pure(new VarietyTraitDef{defName="HNS_IvoryNerves",balanceValueExplicit=true,beautyOffset=1f}),"beauty is mechanical, not free");
            Require(!OrnamentalInheritance.Pure(Trait("HighYield")),"functional phenotype is not a free ornamental gene");
            var m=new List<VarietyTraitDef>{Trait("HighYield")};
            var mech=new List<VarietyTraitDef>{Trait("ColdResistant"),Trait("Robust"),Trait("EasyHarvest")};
            var ornaments=new List<VarietyTraitDef>{Trait("CrimsonNetwork"),Trait("SilverBloom"),Trait("IvoryNerves")};
            var mixed=mech.Concat(ornaments).ToList();
            var results=new List<string>{"Seed\tMechanical only\tMixed mechanical\tMixed ornamental\tLegacy count"};
            int mechanicalCount=0,ornamentalCount=0,legacyMech=0;
            for(int i=0;i<512;i++)
            {
                var only=Draw(99200+i,corn,m,mech,s,1);
                var both=Draw(99200+i,corn,m,mixed,s,1);
                var actualMechanical=both.Where(t=>!OrnamentalInheritance.Pure(t)).ToArray();
                var actualOrnamental=both.Where(OrnamentalInheritance.Pure).ToArray();
                Require(only.SequenceEqual(actualMechanical),"identical seeded mechanical selection with or without decorative donors");
                Require(actualMechanical.Length==1&&actualOrnamental.Length==1,"exact separate cap");
                mechanicalCount+=actualMechanical.Length;ornamentalCount+=actualOrnamental.Length;
                s.enableIndependentOrnamentalInheritance=false;
                var old=Draw(99200+i,corn,m,mixed,s,1);
                Require(old.Count==1,"legacy cap unchanged");legacyMech+=old.Count(t=>!OrnamentalInheritance.Pure(t));
                s.enableIndependentOrnamentalInheritance=true;
                results.Add(string.Join("\t",i,only[0].defName,actualMechanical[0].defName,actualOrnamental[0].defName,old.Count));
            }
            // Actual blocked-tag filtering still applies to independent choices.
            var conflict=new List<VarietyTraitDef>{Trait("HighYield"),Trait("IvoryNerves")};
            var blocked=Draw(9,corn,conflict,new(){Trait("ColdResistant"),Trait("CrimsonNetwork")},s,1);
            Require(blocked.Count==1&&blocked[0]==Trait("ColdResistant"),"same tissue channel remains blocked");
            s.enableBotanicalExpression=false;
            Require(!OrnamentalInheritance.Enabled(s,corn),"independent mode depends on expression beta");
            s.enableBotanicalExpression=true;
            File.WriteAllLines(Path.Combine(root,"HNS-garden-ornamental-parity.tsv"),results);
            File.WriteAllText(Path.Combine(root,"HNS-garden-ornamental-summary.txt"),"512 paired actual selection seeds; mechanical parity 512/512; independent mechanical additions "+mechanicalCount+"; independent ornamental additions "+ornamentalCount+"; legacy mixed mechanical additions "+legacyMech+"; all 18 definitions mechanically neutral; incompatible channel rejected");
        }
        internal static void Begin(string dir,Action<string> onStage,Action<string,string> completed)
        {
            root=dir;stage=onStage;finish=completed;
            try
            {
                map=Find.CurrentMap;Require(map?.IsPlayerHome==true,"isolated home map");
                var s=HorticultureNovelSeedsMod.Settings;
                Require(!s.enableIndependentOrnamentalInheritance,"separate option defaults off");
                s.enableBotanicalExpression=true;s.enableIndependentOrnamentalInheritance=true;
                s.globalMutationChance=0f;s.globalCrossPollinationChance=1f;s.minimumDonorGrowth=.45f;s.maxCrossPollinationTraits=1;
                Parity(s);stage("OrnamentalParity");
                var farmer=map.mapPawns.FreeColonistsSpawned.First();
                int advance=Mathf.RoundToInt(Mathf.Repeat(.5f-GenLocalDate.DayPercent(map),1f)*60000f);
                Find.TickManager.DebugSetTicksGame(Find.TickManager.TicksGame+advance);Find.TickManager.CurTimeSpeed=TimeSpeed.Paused;
                var w=WeatherDefOf.Clear;map.weatherManager.TransitionTo(w);map.weatherManager.lastWeather=w;map.weatherManager.curWeatherAge=4000;map.skyManager.SkyManagerUpdate();
                origin=GenRadial.RadialCellsAround(farmer.Position,70,true).First(c=>Plot(c).All(Candidate));
                var area=Plot(origin).ToHashSet();
                foreach(var t in map.listerThings.AllThings.Where(t=>t is Plant||t.def.category==ThingCategory.Item).Where(t=>area.Contains(t.Position)).ToArray())if(t.Spawned)t.Destroy(DestroyMode.Vanish);
                foreach(var cell in area)map.terrainGrid.SetTerrain(cell,TerrainDefOf.Soil);
                var registry=GameComponent_NovelSeeds.Instance;
                string[] crops={"Corn","Cotton","Daylily"};
                for(int r=0;r<3;r++)
                {
                    var def=DefDatabase<ThingDef>.GetNamed("Plant_"+crops[r]);int z=8-4*r;
                    var maternal=registry.UnlockVariety(def,new(){Trait("HighYield")},"Field parent "+def.label,originKind:"fixture-parent");
                    var donor=registry.UnlockVariety(def,new(){Trait("ColdResistant"),Trait("CrimsonNetwork")},"Ornamental donor "+def.label,originKind:"fixture-parent");
                    var control=Spawn(def,origin+new IntVec3(0,0,z),.4f,maternal);
                    var d=Spawn(def,origin+new IntVec3(4,0,z),1f,donor);
                    var p=Spawn(def,origin+new IntVec3(8,0,z),.25f);
                    var zone=new Zone_Growing(map.zoneManager);map.zoneManager.RegisterZone(zone);zone.AddCell(p.Position);zone.SetPlantDefToGrow(def);registry.SetSelectedVariety(zone,maternal);
                    Require(registry.VarietyForSowing(zone,p.Position)==maternal,"actual maternal zone selection");
                    NovelSeedUtility.AssignMutationOnSow(p);
                    var c=Comp(p);
                    Require(c.CrossPollinated&&c.Variety==maternal&&c.CrossPollinationParent==donor&&c.ActiveTraits.Count==3,"real cross inherits both budgets with cap one");
                    Require(c.ActiveTraits.Contains(Trait("ColdResistant"))&&c.ActiveTraits.Contains(Trait("CrimsonNetwork")),"actual functional and ornamental donor traits");
                    var child=registry.UnlockVariety(def,c.DiscoveryTraits,"Adaptive hybrid "+def.label,c.CrossPollinationParentIds,originKind:"cross-pollination");
                    c.SetVariety(child);Require(child.parentVarietyIds.Count==2&&child.generation==1,"documented offspring lineage");
                    Require(Math.Abs(NovelSeedUtility.YieldFactor(c)-1.15f)<.001f&&Math.Abs(c.GrowthRateFactor-.90f)<.001f&&c.ColdGrowthOffset==-6&&c.HeatGrowthOffset==-3,"actual combined unchanged mechanical modifiers");
                    evidence.Add(string.Join("\t",def.defName,maternal.id,donor.id,child.id,"ColdResistant","CrimsonNetwork",NovelSeedUtility.YieldFactor(c),c.GrowthRateFactor,c.ColdGrowthOffset,c.HeatGrowthOffset));
                    subjects.Add(control);subjects.Add(d);subjects.Add(p);
                }
                File.WriteAllLines(Path.Combine(root,"HNS-garden-ornamental-inheritance.tsv"),evidence);stage("OrnamentalSow");
                Find.CameraDriver.JumpToCurrentMapLoc(origin+new IntVec3(5,0,4));Find.CameraDriver.SetRootSize(7.2f);active=true;Advance();
            }
            catch(Exception ex){Fail(ex);}
        }
        private static void Advance()
        {
            age=0;shot=null;foreach(var p in subjects)p.Growth=growth[phase];
        }
        private static void CaptureGpu()
        {
            for(int i=0;i<subjects.Count;i++)
            {
                if(i%3==0)continue;
                var p=subjects[i];var pr=BotanicalExpressionEngine.Prints[p.thingIDNumber];
                Require(pr.Decision.Plan.Tissue==2&&pr.Band==E.Band(p.Growth)&&BotanicalExpressionEngine.MeshPayload.GetValueOrDefault(p.thingIDNumber),"actual native tissue and mesh");
                var tex=BotanicalExpressionEngine.Readback(pr.Material,pr.Band,pr.Seed,pr.Flipped?-pr.Bias:pr.Bias,pr.Material.GetVector("_HnsExpressionEnv"));
                string name="HNS-garden-ornamental-gpu-"+(phase+1).ToString("D2")+"-"+i.ToString("D2")+".png";
                try{Require(tex.GetPixels32().Count(c=>c.r+c.g+c.b>24)>30,"nonblank GPU image");File.WriteAllBytes(Path.Combine(root,name),tex.EncodeToPNG());}
                finally{UnityEngine.Object.Destroy(tex);}
                renders.Add(string.Join("\t",phase+1,p.def.defName,p.thingIDNumber,Comp(p).VarietyId,p.Growth,pr.Decision.Plan.Count,pr.Seed,true,name));
            }
            File.WriteAllLines(Path.Combine(root,"HNS-garden-ornamental-render.tsv"),renders);
        }
        internal static void Update()
        {
            if(!active)return;
            try
            {
                age++;Redraw();
                if(Find.WindowStack.TryRemove(typeof(EditWindow_Log),false)){age=0;Redraw();return;}
                Require(age<2200,"ornamental native render timeout phase "+phase);
                if(age<100||BotanicalExpressionEngine.PendingCount!=0||subjects.Where((p,i)=>i%3!=0).Any(p=>!BotanicalExpressionEngine.Prints.TryGetValue(p.thingIDNumber,out var q)||q.Band!=E.Band(p.Growth)))return;
                if(shot==null){CaptureGpu();shot=Path.Combine(root,"HNS-garden-ornamental-map-"+(phase+1).ToString("D2")+".png");ScreenCapture.CaptureScreenshot(shot);Redraw();return;}
                if(!File.Exists(shot)||new FileInfo(shot).Length<1000||age<130)return;
                if(++phase<growth.Length){Advance();return;}
                stage("OrnamentalMaturity");active=false;finish("PASS","512 paired native selector seeds, three actual crosses with one mechanical plus one ornamental slot, four growth stages and native GPU/map evidence");
            }
            catch(Exception ex){Fail(ex);}
        }
        private static void Fail(Exception ex){active=false;finish("FAIL",ex.ToString());}
    }
}
#endif
