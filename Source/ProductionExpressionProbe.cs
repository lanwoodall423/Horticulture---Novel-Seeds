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
using E=HorticultureNovelSeeds.ExpressionMath;

namespace HorticultureNovelSeeds
{
    // This fixture does not directly construct finished hybrid genotypes:
    // actual donor discovery, slot rolls, conflict checks, and pending trait
    // inheritance run through NovelSeedUtility.AssignMutationOnSow.
    internal static class ProductionExpressionProbe
    {
        private static string root,run,saveName;
        private static Action<string> stage;
        private static Action<string,string> finish;
        private static Map map;
        private static IntVec3 origin;
        private static bool active;
        internal static bool AwaitingReload{get;private set;}
        private static int phase,frames,shotAge;
        private static string shot;
        private static IntPtr fixtureWindow;
        private static readonly List<string> captureStatus=new(){"Phase\tNative map screenshot\tRedraw requests"};
        private static int redrawRequests;
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool InvalidateRect(IntPtr hWnd,IntPtr rect,bool erase);
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool UpdateWindow(IntPtr hWnd);
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd,out uint processId);
        private static void RequestMapRedraw()
        {
            if(!OperatingSystem.IsWindows())return;
            if(fixtureWindow==IntPtr.Zero)
            {
                using var process=System.Diagnostics.Process.GetCurrentProcess();
                fixtureWindow=process.MainWindowHandle;
            }
            if(fixtureWindow==IntPtr.Zero||GetWindowThreadProcessId(fixtureWindow,out uint owner)==0||owner!=(uint)Environment.ProcessId)return;
            // Request WM_PAINT only for this isolated game window. The native
            // Waylith backend translates it into an actual presentation request.
            if(InvalidateRect(fixtureWindow,IntPtr.Zero,false))
            {
                redrawRequests++;
                UpdateWindow(fixtureWindow);
            }
        }
        private static readonly string[] crops={"Plant_Corn","Plant_Cotton","Plant_Daylily"};
        private static readonly float[] growth={.40f,.55f,.65f,.80f,.90f,1f};
        private static readonly List<Plant> samples=new(),dense=new(),neighbors=new();
        private static readonly List<VarietyRecord> offspring=new(),other=new();
        private static readonly List<string> inheritance=new(){"Species\tMother\tDonor\tChild\tGeneration\tTraits\tMutation draws\tPromoted draws"};
        private static readonly List<string> field=new(){"Phase\tSpecies\tPlant ID\tCultivar\tGrowth\tSeed\tHistory\tBias\tPlan channels\tMaterial ID\tNative payload\tX\tZ"};
        private static readonly List<string> perf=new(){"Phase\tPlants\tRendered\tMaterials\tSource maps\tTexture bytes\tPrepare jobs\tMax prepare ms\tMedian update interval ms\tP95 update interval ms"};
        private sealed record Saved(int ID,string Cultivar,string Key,int History,int Seed,float Growth,int X,int Z);
        private static Saved[] expected;
        private static readonly List<double> intervals=new();
        private static long lastStamp;
        private static void Require(bool ok,string text){if(!ok)throw new InvalidOperationException(text);}
        private static bool Clear(IntVec3 p)=>p.InBounds(map)&&p.Standable(map)&&p.GetEdifice(map)==null&&p.GetZone(map)==null&&!p.Fogged(map)&&map.roofGrid.RoofAt(p)==null;
        private static bool Candidate(IntVec3 p)=>p.InBounds(map)&&p.GetEdifice(map)==null&&p.GetZone(map)==null&&!p.Fogged(map)&&map.roofGrid.RoofAt(p)==null&&(p.Standable(map)||p.GetPlant(map)!=null);
        private static IEnumerable<IntVec3> Plot(IntVec3 c)
        {
            for(int z=0;z<15;z++)for(int x=0;x<20;x++)yield return c+new IntVec3(x,0,z);
        }
        private static VarietyTraitDef Trait(string id)=>DefDatabase<VarietyTraitDef>.GetNamed("HNS_"+id);
        private static Plant Spawn(ThingDef def,IntVec3 cell,float g,VarietyRecord variety=null)
        {
            Require(Clear(cell)&&cell.GetPlant(map)==null,"clear isolated plant cell "+cell);
            var p=(Plant)ThingMaker.MakeThing(def);p.sown=true;p.Growth=g;p.HitPoints=p.MaxHitPoints;
            p.TryGetComp<CompPlantVariety>().SetVariety(variety);GenSpawn.Spawn(p,cell,map);return p;
        }
        private static void SetWeather(bool rain)
        {
            var w=rain?DefDatabase<WeatherDef>.GetNamed("Rain"):WeatherDefOf.Clear;
            map.weatherManager.TransitionTo(w);map.weatherManager.lastWeather=w;map.weatherManager.curWeatherAge=4000;map.skyManager.SkyManagerUpdate();
            BotanicalExpressionEngine.Refresh(map);
        }
        private static void Camera(IntVec3 center,float size)
        {
            Find.WindowStack.TryRemove(typeof(EditWindow_Log),false);Find.Selector.ClearSelection();
            Find.CameraDriver.JumpToCurrentMapLoc(center);Find.CameraDriver.SetRootSize(size);
        }
        private static void ResetCapture(){frames=0;shotAge=0;shot=null;intervals.Clear();lastStamp=0;}
        internal static void Begin(string dir,string token,Action<string> onStage,Action<string,string> completed)
        {
            root=dir;run=token;stage=onStage;finish=completed;
            try
            {
                map=Find.CurrentMap;Require(map!=null,"home map");
                var settings=HorticultureNovelSeedsMod.Settings;
                Require(!settings.enableBotanicalExpression,"beta remains opt-in by default");
                settings.enableBotanicalExpression=true;
                Require(BotanicalExpressionEngine.Enabled,"production settings activation (no fixture renderer override)");
                settings.globalMutationChance=0f;settings.globalCrossPollinationChance=1f;settings.minimumDonorGrowth=.45f;
                settings.maxCrossPollinationTraits=5;settings.secondCrossPollinationTraitChance=1f;settings.laterCrossPollinationTraitChance=1f;
                // Only the isolated config is adjusted; the game still selects
                // random trait roots and actual donors using the normal logic.
                foreach(var a in E.Library)settings.GetGlobalTraitSettings(Trait(a.Id)).weight=E.DefaultWeight(a.Id);
                var farmer=map.mapPawns.FreeColonistsSpawned.First();
                int advance=Mathf.RoundToInt(Mathf.Repeat(.5f-GenLocalDate.DayPercent(map),1f)*60000f);
                Find.TickManager.DebugSetTicksGame(Find.TickManager.TicksGame+advance);
                Find.TickManager.CurTimeSpeed=TimeSpeed.Paused;SetWeather(false);
                var locations=GenRadial.RadialCellsAround(farmer.Position,75,true).Where(c=>Plot(c).All(Candidate)).Take(1).ToArray();
                Require(locations.Length==1,"large clear unroofed field plot");origin=locations[0];
                var area=Plot(origin).ToHashSet();
                foreach(var t in map.listerThings.AllThings.Where(t=>t is Plant||t.def.category==ThingCategory.Item).Where(t=>area.Contains(t.Position)).ToArray())
                    if(t.Spawned)t.Destroy(DestroyMode.Vanish);
                foreach(var cell in area)map.terrainGrid.SetTerrain(cell,TerrainDefOf.Soil);
                var registry=GameComponent_NovelSeeds.Instance;
                for(int r=0;r<3;r++)
                {
                    var def=DefDatabase<ThingDef>.GetNamed(crops[r]);
                    var ids=E.Recipe(r,0);
                    var mother=registry.UnlockVariety(def,ids.Take(2).Select(Trait).ToList(),"Field parent "+def.label,originKind:"fixture-parent");
                    var donor=registry.UnlockVariety(def,ids.Skip(2).Select(Trait).ToList(),"Donor parent "+def.label,originKind:"fixture-parent");
                    var dest=origin+new IntVec3(3,0,8-4*r);
                    var dp=Spawn(def,dest+new IntVec3(1,0,0),1f,donor);
                    var p=Spawn(def,dest,.25f);
                    var c=p.TryGetComp<CompPlantVariety>();
                    // Test the ordinary sow selection path using a real growing zone.
                    var zone=new Zone_Growing(map.zoneManager);
                    map.zoneManager.RegisterZone(zone);zone.AddCell(dest);zone.SetPlantDefToGrow(def);
                    registry.SetSelectedVariety(zone,mother);
                    Require(registry.VarietyForSowing(zone,dest)==mother,"actual selected parent");
                    NovelSeedUtility.AssignMutationOnSow(p);
                    Require(c.CrossPollinated&&c.Variety==mother&&c.CrossPollinationParent==donor,"real donor and maternal lineage "+def.defName);
                    Require(c.ActiveTraits.Count==5&&c.ActiveTraits.Select(t=>E.Id(t.defName)).ToHashSet().SetEquals(ids),"actual five-channel cross "+def.defName);
                    var child=registry.UnlockVariety(def,c.DiscoveryTraits,crops[r].Replace("Plant_","")+" Heritage",c.CrossPollinationParentIds,originKind:"cross-pollination");
                    Require(child.parentVarietyIds.Count==2&&child.generation==1&&child.traits.Count==5,"documented offspring lineage");
                    offspring.Add(child);c.SetVariety(child);registry.SetSelectedVariety(zone,child);
                    dp.Destroy(DestroyMode.Vanish);
                    var variant=registry.UnlockVariety(def,E.Recipe(r,1).Select(Trait).ToList(),"Collector "+def.label,originKind:"fixture-comparison");
                    other.Add(variant);
                    // Sample real mutation selection without forcing a mutation
                    // outcome. Controlled RNG state is restored after sampling.
                    int draws=320,found=0;Rand.PushState(740+r);
                    try
                    {
                        for(int k=0;k<draws;k++)
                        {
                            var set=NovelSeedUtility.RandomTraitSet(def);
                            var promoted=set.Where(t=>E.Known(t.defName)).ToArray();
                            Require(promoted.All(t=>E.Eligible(t.defName,def.defName)),"species-gated actual mutation");
                            Require(promoted.Select(t=>E.ChannelTag(t.defName)).Distinct().Count()==promoted.Length,"actual mutation channel conflict");
                            found+=promoted.Length;
                        }
                    }
                    finally{Rand.PopState();}
                    Require(found>0,"nonzero actual promoted mutation samples "+def.defName);
                    inheritance.Add(string.Join("\t",def.defName,mother.id,donor.id,child.id,child.generation,NovelSeedUtility.TraitKey(child.traits),draws,found));
                    // One control, three sibling individuals of the cross, and
                    // two comparison cultivars in each row.
                    samples.Add(Spawn(def,origin+new IntVec3(0,0,8-4*r),.25f));
                    samples.Add(p);
                    for(int col=2;col<6;col++)
                        samples.Add(Spawn(def,origin+new IntVec3(col*3,0,8-4*r),.25f,col<4?child:variant));
                }
                // Verify ordinary re-sowing inheritance, with cross rate and
                // mutation rate disabled rather than altering the plant comp.
                settings.globalCrossPollinationChance=0f;
                var inherited=samples[1];var comp=inherited.TryGetComp<CompPlantVariety>();
                comp.SetVariety(null);NovelSeedUtility.AssignMutationOnSow(inherited);
                Require(comp.Variety==offspring[0]&&!comp.PendingDiscovery&&comp.ActiveTraits.Count==5,"ordinary sow inherits documented five-channel cultivar");
                File.WriteAllLines(Path.Combine(root,"HNS-garden-production-inheritance.tsv"),inheritance);
                stage("ExpressionInheritance");
                Camera(origin+new IntVec3(8,0,4),8.4f);phase=0;AdvanceGrowth();active=true;
            }
            catch(Exception e){Fail(e);}
        }
        private static void AdvanceGrowth()
        {
            foreach(var n in neighbors)if(n.Spawned)n.Destroy(DestroyMode.Vanish);neighbors.Clear();
            if(phase<5)
            {
                var rice=DefDatabase<ThingDef>.GetNamed("Plant_Rice");
                for(int r=0;r<3;r++)for(int c=1;c<=3;c++)
                {
                    int side=c==1?-1:c==2?1:phase==2?-1:phase==4?1:0;
                    if(side!=0)neighbors.Add(Spawn(rice,samples[r*6+c].Position+new IntVec3(side,0,0),1f));
                }
            }
            foreach(var p in samples)p.Growth=growth[phase];
            SetWeather(phase==3||phase==4);ResetCapture();
        }
        private static bool Rendered(Plant p,int count)
        {
            if(!BotanicalExpressionEngine.Prints.TryGetValue(p.thingIDNumber,out var pr))return false;
            return (count<0?pr.Decision.Plan.Count>0&&pr.Decision.Accepted.Length+pr.Decision.Rejected.Length==5:pr.Decision.Plan.Count==count)&&pr.Band==E.Band(p.Growth)&&pr.Seed==E.Seed(p.thingIDNumber)&&
                pr.Code==p.TryGetComp<CompPlantVariety>().VisualChronicle&&BotanicalExpressionEngine.MeshPayload.GetValueOrDefault(p.thingIDNumber)&&
                pr.Material.GetVector("_HnsPreview")==Vector4.zero;
        }
        private static IEnumerable<Plant> Styled(IEnumerable<Plant> ps)=>ps.Where(p=>p.TryGetComp<CompPlantVariety>()?.HasAnyTraits==true);
        private static void Record(IEnumerable<Plant> ps,string name)
        {
            foreach(var p in Styled(ps))
            {
                var c=p.TryGetComp<CompPlantVariety>();var pr=BotanicalExpressionEngine.Prints[p.thingIDNumber];
                field.Add(string.Join("\t",name,p.def.defName,p.thingIDNumber,c.VarietyId,p.Growth,pr.Seed,pr.Code,pr.Bias,pr.Decision.Plan.Count,pr.Material.GetInstanceID(),BotanicalExpressionEngine.MeshPayload[p.thingIDNumber],p.Position.x,p.Position.z));
            }
            File.WriteAllLines(Path.Combine(root,"HNS-garden-production-mesh.tsv"),field);
        }
        private static void CapturePerf(string name,int plants,int rendered)
        {
            var a=intervals.OrderBy(x=>x).ToArray();
            double median=a.Length==0?0:a[a.Length/2],p95=a.Length==0?0:a[Math.Min(a.Length-1,(int)Math.Floor(a.Length*.95))];
            perf.Add(string.Join("\t",name,plants,rendered,BotanicalExpressionEngine.MaterialCount,BotanicalExpressionEngine.SourceCount,BotanicalExpressionEngine.Bytes,BotanicalExpressionEngine.PrepareJobs,BotanicalExpressionEngine.MaxPrepareMilliseconds.ToString("F2",CultureInfo.InvariantCulture),median.ToString("F2",CultureInfo.InvariantCulture),p95.ToString("F2",CultureInfo.InvariantCulture)));
            File.WriteAllLines(Path.Combine(root,"HNS-garden-production-performance.tsv"),perf);
        }
        private static bool Capture(string name)
        {
            if(shot==null)
            {
                shot=Path.Combine(root,"HNS-garden-production-"+name+".png");
                // Native screen capture is optional in unattended Windows sessions.
                // Always retain real GPU readbacks of the live, validated specimens.
                for(int i=0;i<samples.Count;i++)
                {
                    var p=samples[i];
                    if(!BotanicalExpressionEngine.Prints.TryGetValue(p.thingIDNumber,out var pr))continue;
                    var tex=BotanicalExpressionEngine.Readback(pr.Material,pr.Band,pr.Seed,pr.Flipped?-pr.Bias:pr.Bias,pr.Material.GetVector("_HnsExpressionEnv"));
                    try
                    {
                        Require(tex.GetPixels32().Count(c=>c.r+c.g+c.b>24)>30,"nonblank production GPU render");
                        File.WriteAllBytes(Path.Combine(root,"HNS-garden-production-gpu-"+name+"-"+i.ToString("D2")+".png"),tex.EncodeToPNG());
                    }
                    finally{UnityEngine.Object.Destroy(tex);}
                }
                redrawRequests=0;ScreenCapture.CaptureScreenshot(shot);RequestMapRedraw();shotAge=0;return false;
            }
            shotAge++;
            bool written=shotAge>=20&&File.Exists(shot)&&new FileInfo(shot).Length>1000;
            if(!written&&shotAge<360){RequestMapRedraw();return false;}
            captureStatus.Add(name+"\t"+(written?"written":"unavailable")+"\t"+redrawRequests);
            File.WriteAllLines(Path.Combine(root,"HNS-garden-production-map-capture.tsv"),captureStatus);
            return true;
        }
        private static void MakeDense()
        {
            // Retain the same showcase plants and add a contiguous field in the
            // remaining clear positions. A single row per species is vanilla.
            var used=samples.Select(p=>p.Position).ToHashSet();
            for(int z=0;z<15;z++)for(int x=0;x<20;x++)
            {
                var cell=origin+new IntVec3(x,0,z);if(used.Contains(cell))continue;
                int r=z/5;var def=DefDatabase<ThingDef>.GetNamed(crops[r]);
                var p=Spawn(def,cell,.65f,z%5==0?null:x<10?offspring[r]:other[r]);
                dense.Add(p);
            }
            foreach(var p in dense)p.Growth=1f;
            Camera(origin+new IntVec3(9,0,7),12.3f);SetWeather(false);phase=6;ResetCapture();
        }
        internal static void Update()
        {
            if(!active)return;
            try
            {
                frames++;
                long now=System.Diagnostics.Stopwatch.GetTimestamp();
                if(lastStamp!=0&&frames>=20)intervals.Add((now-lastStamp)*1000d/System.Diagnostics.Stopwatch.Frequency);
                lastStamp=now;
                if(Find.WindowStack.TryRemove(typeof(EditWindow_Log),false)){frames=0;return;}
                if(frames>1200)throw new InvalidOperationException("production field render/capture timeout phase="+phase+"; prepared="+BotanicalExpressionEngine.PendingCount+"; jobs="+BotanicalExpressionEngine.PrepareJobs+"; materials="+BotanicalExpressionEngine.MaterialCount+"; capture="+shot+"; states="+string.Join(",",Styled(samples.Concat(dense)).Take(40).Select(p=>p.def.defName+":"+p.thingIDNumber+":"+(BotanicalExpressionEngine.Prints.TryGetValue(p.thingIDNumber,out var q)?q.Decision.Plan.Count+":"+q.Band+":"+BotanicalExpressionEngine.MeshPayload.GetValueOrDefault(p.thingIDNumber):"missing"))));
                if(phase<6)
                {
                    var styled=Styled(samples).ToArray();
                    if(frames<90||BotanicalExpressionEngine.PendingCount!=0||!styled.All(p=>Rendered(p,phase==5?5:-1)))return;
                    if(!Capture("showcase-"+(phase+1).ToString("D2")))return;
                    Record(samples,"showcase-"+phase);CapturePerf("showcase-"+phase,samples.Count,styled.Length);
                    if(++phase<6){AdvanceGrowth();return;}
                    Require(samples.Where((p,i)=>i%6 is 1 or 2 or 3).All(p=>ChronicleMath.Count(p.TryGetComp<CompPlantVariety>().VisualChronicle)==3),"actual recorded growth histories");
                    stage("ExpressionShowcase");MakeDense();return;
                }
                var all=samples.Concat(dense).ToArray();var modified=Styled(all).ToArray();
                if(frames<160||BotanicalExpressionEngine.PendingCount!=0||!modified.All(p=>Rendered(p,5)))return;
                if(phase==6)
                {
                    if(!Capture("field-before-save"))return;
                    Require(all.Length==300&&modified.Length>=230,"dense actual mixed field");
                    Require(BotanicalExpressionEngine.MaterialCount<=160&&BotanicalExpressionEngine.Bytes<=64L*1024*1024,"dense renderer budget");
                    Record(all,"dense-before-save");CapturePerf("dense-before-save",all.Length,modified.Length);stage("ExpressionDenseField");
                    expected=all.Select(p=>{var c=p.TryGetComp<CompPlantVariety>();return new Saved(p.thingIDNumber,c.VarietyId,NovelSeedUtility.TraitKey(c.ActiveTraits),c.VisualChronicle,E.Seed(p.thingIDNumber),p.Growth,p.Position.x,p.Position.z);}).ToArray();
                    SaveReload();return;
                }
                if(phase==7)
                {
                    if(!Capture("field-after-reload"))return;
                    Record(all,"dense-after-reload");CapturePerf("dense-after-reload",all.Length,modified.Length);
                    foreach(var s in expected)
                    {
                        var p=all.Single(q=>q.thingIDNumber==s.ID);var c=p.TryGetComp<CompPlantVariety>();
                        Require(c.VarietyId==s.Cultivar&&NovelSeedUtility.TraitKey(c.ActiveTraits)==s.Key&&c.VisualChronicle==s.History&&E.Seed(p.thingIDNumber)==s.Seed&&Math.Abs(p.Growth-s.Growth)<.001f&&p.Position.x==s.X&&p.Position.z==s.Z,"full save/reload plant identity/history "+s.ID);
                    }
                    stage("ExpressionSaveReload");active=false;finish("PASS","actual sowing cross-pollination, promoted mutation draws, six live-map growth stages with native GPU readbacks, 300 plant mixed field, full colony reload");
                }
            }
            catch(Exception e){Fail(e);}
        }
        private static void SaveReload()
        {
            active=false;AwaitingReload=true;saveName="HNS-Expression-"+run;
            Log.Message("[HNS Expression] Queueing full isolated colony save; no GUI repaint dependency");
            LongEventHandler.QueueLongEvent(()=>
            {
                try
                {
                    string path=Path.GetFullPath(GenFilePaths.FilePathForSavedGame(saveName));
                    Require(path.StartsWith(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)&&!File.Exists(path),"isolated save path");
                    Log.Message("[HNS Expression] Save routine entered");
                    GameDataSaveLoader.SaveGame(saveName);Require(File.Exists(path)&&new FileInfo(path).Length>0,"full colony save created");
                    File.WriteAllText(Path.Combine(root,"HNS-garden-production-save.txt"),"Saved bytes: "+new FileInfo(path).Length+"\nSHA256: "+Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path))));
                    Log.Message("[HNS Expression] Save complete bytes="+new FileInfo(path).Length);
                    BotanicalExpressionEngine.ClearPending();BotanicalExpressionEngine.Prints.Clear();BotanicalExpressionEngine.MeshPayload.Clear();
                    LongEventHandler.ExecuteWhenFinished(()=>{try{Log.Message("[HNS Expression] Starting normal load routine");GameDataSaveLoader.LoadGame(saveName);}catch(Exception e){Fail(e);}});
                }
                catch(Exception e){Fail(e);}
            },null,false,null,forceHideUI:true);
        }
        internal static void AfterReload()
        {
            try
            {
                Log.Message("[HNS Expression] LoadedGame callback reached");
                AwaitingReload=false;
                Require(Path.GetFullPath(GenFilePaths.SaveDataFolderPath).TrimEnd(Path.DirectorySeparatorChar).Equals(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar),StringComparison.OrdinalIgnoreCase),"actual reload isolation");
                map=Find.CurrentMap;Require(map!=null,"reloaded current map");
                var registry=GameComponent_NovelSeeds.Instance;
                foreach(var v in offspring)
                {
                    var loaded=registry.GetVariety(v.id);Require(loaded!=null&&!ReferenceEquals(loaded,v)&&loaded.parentVarietyIds.SequenceEqual(v.parentVarietyIds)&&loaded.generation==v.generation&&NovelSeedUtility.TraitKey(loaded.traits)==NovelSeedUtility.TraitKey(v.traits),"reloaded documented cross lineage");
                }
                var byId=map.listerThings.AllThings.OfType<Plant>().Where(p=>expected.Any(s=>s.ID==p.thingIDNumber)).ToDictionary(p=>p.thingIDNumber);
                Require(byId.Count==expected.Length,"all 300 actual plant IDs reloaded");
                var old=samples.Concat(dense).ToArray();
                Require(old.All(p=>!ReferenceEquals(p,byId[p.thingIDNumber])),"real new plant instances after load");
                samples.Clear();dense.Clear();samples.AddRange(expected.Take(18).Select(s=>byId[s.ID]));dense.AddRange(expected.Skip(18).Select(s=>byId[s.ID]));
                Find.TickManager.CurTimeSpeed=TimeSpeed.Paused;Camera(origin+new IntVec3(9,0,7),12.3f);SetWeather(false);
                phase=7;ResetCapture();active=true;
            }
            catch(Exception e){Fail(e);}
        }
        private static void Fail(Exception e)
        {active=false;AwaitingReload=false;finish("FAIL",e.ToString());}
    }
}
#endif
