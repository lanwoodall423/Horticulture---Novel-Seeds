using HorticultureNovelSeeds;
using System.Text.Json;

static void Require(bool condition, string label)
{
    if (!condition) throw new Exception(label);
    Console.WriteLine("PASS " + label);
}
var facts = new CultivationFacts();
Require(facts.firstObservedTick == -1 && facts.minYield == -1 && facts.harvests == 0, "fresh journal unknown");
Require(!facts.RecordHarvest(10, "failed", 0) && facts.harvests == 0, "failed harvest excluded");
Require(!facts.RecordSowing(-1, "invalid") && !facts.RecordDiscovery(10, ""), "invalid input excluded");
Require(facts.RecordSowing(20, "plant:1") && !facts.RecordSowing(20, "plant:1") && facts.sowings == 1, "duplicate sow rejected");
Require(facts.RecordHarvest(40, "plant:1:cycle:1", 8) && facts.RecordHarvest(50, "plant:1:cycle:2", 12), "distinct harvest cycles");
Require(!facts.RecordHarvest(50, "plant:1:cycle:2", 12) && facts.harvests == 2, "duplicate harvest rejected");
Require(facts.firstObservedTick == 20 && facts.firstHarvestTick == 40 && facts.lastHarvestTick == 50 && facts.minYield == 8 && facts.maxYield == 12, "real observed range and dates");
var options = new JsonSerializerOptions { IncludeFields = true };
var restored = JsonSerializer.Deserialize<CultivationFacts>(JsonSerializer.Serialize(facts, options), options);
restored.Normalize();
Require(!restored.RecordHarvest(50, "plant:1:cycle:2", 12) && restored.harvests == 2, "serialized recent-event identity");
Require(restored.RecordDiscovery(10, "document:1") && restored.firstObservedTick == 10 && restored.harvests == 2, "documentation does not invent harvest");
for (int i = 0; i < 100; i++) restored.RecordSowing(100 + i, "sow:" + i);
Require(restored.recentEvents.Count == CultivationFacts.RecentLimit && !restored.RecordSowing(200, "sow:99"), "bounded recent-event window");
restored.sowings = int.MaxValue;
Require(restored.RecordSowing(201, "saturation") && restored.sowings == int.MaxValue, "counter saturation");
restored.recentEvents = new List<string> { null, "", "a", "a", new string('x', 257) };
restored.Normalize();
Require(restored.recentEvents.SequenceEqual(new[] { "a" }), "corrupt duplicate identities normalized");
Console.WriteLine("Native rule tests passed. JSON round-trip is not RimWorld Scribe evidence.");

// Verify the geometry contract independently of Unity, including orientation,
// base anchoring and non-folding triangles at maximum strength.
foreach (PlantFormKind kind in Enum.GetValues<PlantFormKind>().Where(k=>k!=PlantFormKind.None&&!NeighborhoodMath.Valid(k)&&!ChronicleMath.Valid(k)))
{
    foreach(bool flip in new[]{false,true})
    {
        const int n=PlantFormMath.Subdivisions;
        for(int i=0;i<=n;i++)
        {
            float x=i/(float)n;
            Require(PlantFormMath.Warp(kind,1f,x,0f,flip)==(x,0f),kind+" base anchor");
        }
        var grid=new (float X,float Y)[n+1,n+1];
        for(int j=0;j<=n;j++)for(int i=0;i<=n;i++)
        {
            var q=PlantFormMath.Warp(kind,1f,i/(float)n,j/(float)n,flip);
            if(!float.IsFinite(q.X)||!float.IsFinite(q.Y)||q.X<-.5f||q.X>1.5f||q.Y<0f||q.Y>1.3f)throw new Exception(kind+" bounds");
            grid[i,j]=q;
        }
        static float Area((float X,float Y)a,(float X,float Y)b,(float X,float Y)c)=>
            (b.X-a.X)*(c.Y-a.Y)-(b.Y-a.Y)*(c.X-a.X);
        for(int j=0;j<n;j++)for(int i=0;i<n;i++)
        {
            var a=grid[i,j];var b=grid[i+1,j];var c=grid[i+1,j+1];var d=grid[i,j+1];
            if(Area(a,b,c)<=.0001f||Area(a,c,d)<=.0001f)throw new Exception(kind+" folded triangle");
        }
        Require(grid[0,n]!=(0f,1f)||grid[n,n]!=(1f,1f),kind+" actual bounded deformation");
    }
}
Require(PlantFormMath.Warp(PlantFormKind.Weeping,float.NaN,.3f,.8f,false)==(.3f,.8f),"invalid form falls back");
Console.WriteLine("Plant form geometry tests passed.");

// Synthetic organ components: distinguish red fruit from green foliage, enlarge
// the fruit without changing a remote leaf, and reject a whole-plant region.
{
    const int n=32;
    var p=new OrganSpriteMath.Pixel[n*n];
    var green=new OrganSpriteMath.Pixel(35,155,47,255);
    var red=new OrganSpriteMath.Pixel(198,35,44,255);
    for(int y=5;y<27;y++)for(int x=5;x<27;x++)p[y*n+x]=green;
    for(int y=14;y<18;y++)for(int x=14;x<18;x++)p[y*n+x]=red;
    Require(OrganSpriteMath.Reproductive(red)&&!OrganSpriteMath.Reproductive(green),"organ palette separates fruit and foliage");
    var selected=p.Select(OrganSpriteMath.Reproductive).ToArray();
    var parts=OrganSpriteMath.Parts(selected,n,n,22*22,false);
    Require(parts.Count==1&&parts[0].Indices.Length==16,"connected organ recognized");
    foreach(var kind in Enum.GetValues<OrganFormKind>().Where(k=>k!=OrganFormKind.None&&k!=OrganFormKind.ThickStalk))
    {
        var outp=OrganSpriteMath.Compose(p,n,n,parts,kind);
        Require(outp.Count(v=>v==red)>16&&outp[6*n+6]==green&&p.Count(v=>v==red)==16,kind+" localized original-preserving enlargement");
    }
    Require(OrganSpriteMath.Parts(Enumerable.Repeat(true,n*n).ToArray(),n,n,n*n,false).Count==0,"whole sprite rejected");
    Require(OrganSpriteMath.Compose(p,n,n,new(),OrganFormKind.PlumpFruit).SequenceEqual(p),"missing organ unchanged");
}

Require(DevelopmentalPhenotype.Band(.39f)==0 && DevelopmentalPhenotype.Band(.4f)==1 && DevelopmentalPhenotype.Band(.8f)==2,"three developmental boundaries");
Require(!DevelopmentalPhenotype.Crossed(.42f,.78f)&&DevelopmentalPhenotype.Crossed(.8f,.3f),"stable within band and reset crossing");
Require(DevelopmentalPhenotype.Phase(float.NaN)==0f&&DevelopmentalPhenotype.Phase(1f)==1f,"bounded finite phase");
Require(!DevelopmentalPhenotype.IsMode(50)&&DevelopmentalPhenotype.IsMode(51)&&DevelopmentalPhenotype.IsMode(55)&&!DevelopmentalPhenotype.IsMode(56),"isolated mode range");

foreach(var habit in Enum.GetValues<WindHabitKind>()){var c=WindHabitMath.Controls(habit);Require(c.X>=0f&&c.X+c.Flutter<=2.5f&&c.Z>=0f&&c.Z+c.Flutter<=2.5f&&c.Rate>=.5f&&c.Rate<=1.5f,habit+" bounded wind profile");}
Require(WindHabitMath.Controls(WindHabitKind.None)==(1f,1f,1f,0f),"neutral wind profile");
Require(WindHabitMath.Controls(WindHabitKind.RigidHabit).X<.2f&&WindHabitMath.Controls(WindHabitKind.Flutterleaf).Flutter>.3f,"distinct wind habits");

Require(SurfaceFinishMath.Environment(.375f,1f,0f).X<0f && SurfaceFinishMath.Environment(.625f,1f,0f).X>0f,"direction changes with local solar time");
Require(SurfaceFinishMath.Environment(.5f,1f,.8f)==(0f,1f,1f,1f),"rain and light bounded");
Require(SurfaceFinishMath.Environment(float.NaN,float.NaN,float.NaN)==(0f,1f,0f,0f),"invalid environment finite");
Require(SurfaceFinishMath.Environment(.5f,1f,.01f).Rain==.03125f,"quantized weather state");

// Topological additions and cuts on a synthetic crop with untouched red fruit.
{
    const int w=64,h=64;
    var source=new OrganSpriteMath.Pixel[w*h];
    var green=new OrganSpriteMath.Pixel(50,150,55,255);
    var red=new OrganSpriteMath.Pixel(190,35,43,255);
    for(int y=13;y<47;y++)for(int x=17;x<47;x++)source[y*w+x]=green;
    for(int y=31;y<36;y++)for(int x=29;x<34;x++)source[y*w+x]=red;
    foreach(var k in Enum.GetValues<BotanicalDetailKind>().Where(BotanicalDetailMath.Topology))
    {
        var result=BotanicalDetailMath.Edit(source,w,h,k);
        Require(result.Removed+result.Added>0,k+" changes topology");
        Require(source.Select((p,i)=>(p,i)).Where(q=>q.p==red).All(q=>result.Pixels[q.i]==red),k+" retains fruit");
        Require(k==BotanicalDetailKind.LobedLeaf||k==BotanicalDetailKind.Windowleaf?result.Removed>0&&result.Added==0:result.Added>0&&result.Removed==0,k+" changes only intended alpha");
    }
    Require(BotanicalDetailMath.Edit(new OrganSpriteMath.Pixel[w*h],w,h,BotanicalDetailKind.SideShoots).Added==0,"no floating shoots on empty art");
    Require(BotanicalDetailMath.Darkness(1f)==0f&&BotanicalDetailMath.Darkness(.1f)==1f&&BotanicalDetailMath.Darkness(float.NaN)==0f,"day/night emission bounded");
}

{
    const int n=64;var p=new OrganSpriteMath.Pixel[n*n];
    var green=new OrganSpriteMath.Pixel(40,145,48,255);var red=new OrganSpriteMath.Pixel(196,40,38,255);
    for(int y=12;y<50;y++)for(int x=18;x<46;x++)p[y*n+x]=green;
    for(int y=32;y<38;y++)for(int x=29;x<35;x++)p[y*n+x]=red;
    var parts=CanopyArchitectureMath.Extract(p,n,n);
    Require(parts?.Leaves!=null&&parts.Fruit!=null&&parts.Anchors.Length>=3,"separate reusable canopy and fruit modules");
    Require(parts.Leaves.Pixels.All(v=>v!=red)&&parts.Fruit.Pixels.Any(v=>v==red),"organ-specific source modules");
    foreach(var k in Enum.GetValues<CanopyArchitectureKind>().Where(CanopyArchitectureMath.Valid))
    {
        var recipe=CanopyArchitectureMath.Recipe(k,parts);
        Require(recipe.Length>=2&&recipe.Length<=5&&recipe.All(a=>a.X>=0f&&a.X<=1f&&a.Y>=0f&&a.Y<=1.1f&&a.Scale>0f&&a.Scale<=1.3f&&Math.Abs(a.Angle)<=60f),k+" bounded modular arrangement");
    }
    Require(CanopyArchitectureMath.Extract(new OrganSpriteMath.Pixel[n*n],n,n)==null,"no modules on empty sprite");
    Require(CanopyArchitectureMath.Recipe(CanopyArchitectureKind.FlowerSpray,parts with {Fruit=null}).Length==0,"missing fruit falls back");
}

{
    const int n=64;var p=new OrganSpriteMath.Pixel[n*n];
    var green=new OrganSpriteMath.Pixel(38,138,46,255);
    var fruit=new OrganSpriteMath.Pixel(216,172,30,255);
    for(int y=10;y<52;y++)for(int x=14;x<48;x++)p[y*n+x]=green;
    for(int y=29;y<42;y++)for(int x=23;x<32;x++)p[y*n+x]=fruit;
    for(int y=35;y<39;y++)for(int x=38;x<43;x++)p[y*n+x]=default;
    for(int y=34;y<39;y++)for(int x=53;x<58;x++)p[y*n+x]=fruit;
    var original=(OrganSpriteMath.Pixel[])p.Clone();
    var before=CanopyArchitectureMath.Extract(p,n,n,false);
    var after=CanopyArchitectureMath.Extract(p,n,n,true);
    OrganSpriteMath.Pixel At(CanopyArchitectureMath.Module m,int x,int y)
    {
        var b=m.Box;
        return x<b.X||x>=b.X+b.W||y<b.Y||y>=b.Y+b.H?default:m.Pixels[(y-b.Y)*b.W+x-b.X];
    }
    Require(At(before.Leaves,27,35).A==0,"legacy extraction exposes fruit-shaped hole");
    Require(Enumerable.Range(29,13).All(y=>Enumerable.Range(23,9).All(x=>{var c=At(after.Leaves,x,y);return c.A==255&&c.G>c.R&&c.G>c.B;})),"embedded organ is reconstructed as green foliage");
    Require(At(after.Leaves,40,36).A==0,"real transparent window preserved");
    Require(At(after.Leaves,55,36).A==0,"detached organ not connected to canopy");
    Require(Enumerable.Range(0,n*n).All(i=>At(after.Leaves,i%n,i/n).A<=p[i].A),"never adds alpha beyond original silhouette");
    Require(after.Leaves.Reconstructed>=117&&after.Leaves.RemainingOcclusion>=25,"reconstruction and conservative exclusion measured");
    Require(p.SequenceEqual(original),"source artwork untouched");
    Require(before.Fruit.Box==after.Fruit.Box&&before.Fruit.Pixels.SequenceEqual(after.Fruit.Pixels),"independent reproductive module unchanged");
}

{
    const int n=64;
    var green=new OrganSpriteMath.Pixel(38,138,46,255);
    var gold=new OrganSpriteMath.Pixel(216,172,30,255);
    var white=new OrganSpriteMath.Pixel(240,240,240,255);
    var p=new OrganSpriteMath.Pixel[n*n];
    for(int y=10;y<=34;y++)for(int x=20;x<=43;x++)p[y*n+x]=green;
    for(int y=34;y<=51;y++)for(int x=20;x<=44;x++)
        if((x-32)*(x-32)+(y-41)*(y-41)<=100)p[y*n+x]=gold;
    var light=p.Select(c=>c==gold?white:c).ToArray();
    var old=CanopyArchitectureMath.Extract(p,n,n,true,false);
    var updated=CanopyArchitectureMath.Extract(p,n,n);
    var alternate=CanopyArchitectureMath.Extract(light,n,n);
    Require(updated.Leaves.Trimmed>0&&updated.Leaves.Trimmed<old.Leaves.Reconstructed,"exposed reproductive tip tapered, not erased");
    Require(updated.Leaves.Box==alternate.Leaves.Box&&updated.Leaves.Pixels.SequenceEqual(alternate.Leaves.Pixels),"reconstructed foliage independent of fruit brightness");
    Require(updated.Leaves.Pixels.Where(c=>c.A>=90).All(c=>c.G>c.R&&c.G>c.B),"no fruit hue in synthesized leaf module");
    Require(old.Fruit.Box==updated.Fruit.Box&&old.Fruit.Pixels.SequenceEqual(updated.Fruit.Pixels),"tip shaping does not change the reproductive module");
}


{
    foreach(var k in Enum.GetValues<ResponsiveHabitKind>())
    foreach(var env in new[]{(.375f,1f,0f),(.625f,1f,0f),(.625f,1f,.5f),(0f,0f,0f),(.5f,1f,0f)})
    {
        var pose=ResponsiveHabitMath.Controls(k,env.Item1,env.Item2,env.Item3,false,false);
        for(int y=0;y<=10;y++)for(int x=0;x<=10;x++)
        {
            float u=x/10f,v=y/10f;
            var f=ResponsiveHabitMath.Forward(pose,u,v);
            var inv=ResponsiveHabitMath.Inverse(pose,f.X,f.Y);
            if(f.X<-.0001f||f.X>1.0001f||f.Y<-.0001f||f.Y>1.0001f||Math.Abs(inv.X-u)>.0001f||Math.Abs(inv.Y-v)>.0001f)throw new Exception("pose must be bounded and reversible "+k);
        }
    }
    Require(true,"all 3630 sampled forward/inverse poses bounded and reversible");
    Require(ResponsiveHabitMath.Controls(ResponsiveHabitKind.Nightfold,0,0,0,false,false).Contraction>.5f,"nightfold closes after dark");
    Require(ResponsiveHabitMath.Controls(ResponsiveHabitKind.Nightbloom,0,0,0,false,false).Contraction==0f,"nightbloom opens after dark");
    Require(ResponsiveHabitMath.Controls(ResponsiveHabitKind.Sunseeker,.375f,1,0,false,false).Lean<0f&&ResponsiveHabitMath.Controls(ResponsiveHabitKind.Sunseeker,.625f,1,0,false,false).Lean>0f,"solar tracking reverses");
    Require(ResponsiveHabitMath.Controls(ResponsiveHabitKind.Sunseeker,.625f,1,0,false,true).Lean<0f,"mirrored sprites preserve direction");
    Require(ResponsiveHabitMath.Controls(ResponsiveHabitKind.Rainbowed,.625f,1,.5f,false,false).Droop>.25f,"rain bows canopy");
    Require(ResponsiveHabitMath.Controls(ResponsiveHabitKind.Raincup,.625f,1,.5f,true,false).Contraction==0f,"shelter suppresses rain response");
}


{
    var green=new OrganSpriteMath.Pixel(44,130,53,255);
    var petal=new OrganSpriteMath.Pixel(230,133,47,255);
    var pal=GroundHabitMath.Sample(Enumerable.Repeat(green,100).Concat(Enumerable.Repeat(petal,100)).ToArray());
    Require(pal.Leaf==green&&pal.Petal==petal&&pal.HasPetal,"ground pigments sampled from actual source colors");
    foreach(var k in Enum.GetValues<GroundHabitKind>().Where(GroundHabitMath.Valid))
    {
        var p=GroundHabitMath.Generate(k,pal,72);
        Require(p.SequenceEqual(GroundHabitMath.Generate(k,pal,72)),k+" deterministic ground footprint");
        Require(p.Count(c=>c.A>=90)>100&&p.Count(c=>c.A>=90)<p.Length*.55f,k+" bounded visible ground detail");
        Require(p[0].A==0&&p[GroundHabitMath.Size-1].A==0&&p[^1].A==0,k+" transparent corners");
    }
    Require(GroundHabitMath.Generate(GroundHabitKind.PetalDrift,pal with {HasPetal=false},72).All(c=>c.A==0),"uncertain petal palette falls back");
}

{
    const int n=64;
    var green=new OrganSpriteMath.Pixel(38,138,46,255);
    var gold=new OrganSpriteMath.Pixel(216,172,30,255);
    var p=new OrganSpriteMath.Pixel[n*n];
    for(int y=10;y<52;y++)for(int x=14;x<48;x++)p[y*n+x]=green;
    for(int y=29;y<42;y++)for(int x=23;x<32;x++)p[y*n+x]=gold;
    for(int y=34;y<39;y++)for(int x=53;x<58;x++)p[y*n+x]=gold;
    var before=(OrganSpriteMath.Pixel[])p.Clone();
    var src=ArticulationMath.Prepare(p,n,n);
    Require(src!=null&&src.Organs.Length==1&&src.Recovered>=117,"only attached reproductive component articulated");
    foreach(var k in Enum.GetValues<ResponsiveHabitKind>().Where(ArticulationMath.Valid))
    {
        var frames=Enumerable.Range(0,ArticulationMath.Frames).Select(i=>ArticulationMath.Compose(src,k,i)).ToArray();
        Require(frames.Any(f=>f.SequenceEqual(p))&&frames.Any(f=>!f.SequenceEqual(p)),k+" native source pose and real organ change");
        Require(frames.All(f=>f.Length==p.Length&&Enumerable.Range(0,p.Length).All(i=>f[i].A>=p[i].A)),k+" no anatomical alpha holes");
        Require(frames.All(f=>f[36*n+55]==gold&&f[12*n+25]==green),k+" detached organ and stationary foliage preserved");
    }
    Require(before.SequenceEqual(p),"immutable source art");
    Require(ArticulationMath.Frame(ArticulationMath.Phase(ResponsiveHabitKind.DawnPetals,.5f,1f,0f,false,false))==8&&ArticulationMath.Frame(ArticulationMath.Phase(ResponsiveHabitKind.DawnPetals,0f,0f,0f,false,false))==0,"day and night local opening");
    Require(ArticulationMath.Frame(ArticulationMath.Phase(ResponsiveHabitKind.RainPendant,.5f,1f,.5f,true,false))==0,"shelter suppresses rain articulation");
    Require(ArticulationMath.WindFrame(0)!=ArticulationMath.WindFrame(2),"native wind phase changes organ frame");
}

{
    const int n=64;
    var green=new OrganSpriteMath.Pixel(38,138,46,255);
    var gold=new OrganSpriteMath.Pixel(216,172,30,255);
    var p=new OrganSpriteMath.Pixel[n*n];
    for(int y=9;y<55;y++)for(int x=9;x<55;x++)p[y*n+x]=green;
    foreach(var center in new[]{(20,20),(39,21),(20,39),(39,39)})
        for(int y=center.Item2-3;y<=center.Item2+3;y++)for(int x=center.Item1-3;x<=center.Item1+3;x++)p[y*n+x]=gold;
    var src=ArticulationMath.Prepare(p,n,n);
    Require(src!=null&&src.Organs.Length==4,"four attached organs prepared for mosaic");
    foreach(var k in Enum.GetValues<ResponsiveHabitKind>().Where(MosaicMath.Valid))
    {
        Require(MosaicMath.Compose(src,k,0,out _).SequenceEqual(p),k+" exact orientation reference");
        var frames=new List<OrganSpriteMath.Pixel[]>();
        for(int f=1;f<=8;f++)
        {
            var frame=MosaicMath.Compose(src,k,f,out int changed);
            Require(changed>0&&changed<src.Organs.Length&&frame.Where((c,i)=>c!=p[i]).Any(),k+" mixed organ expression "+f);
            Require(Enumerable.Range(0,p.Length).All(i=>frame[i].A>=p[i].A),k+" no alpha holes "+f);
            Require(frame.SequenceEqual(MosaicMath.Compose(src,k,f,out _)),k+" deterministic "+f);
            frames.Add(frame);
        }
        Require(frames.Skip(1).Any(f=>!f.SequenceEqual(frames[0])),k+" different individual variants");
    }
    Require(Enumerable.Range(0,1024).Select(MosaicMath.Frame).Distinct().Count()==8&&MosaicMath.Frame(-7)==8&&MosaicMath.Frame(0)==1,"bounded native seed selection");
}

{
    var l=new NeighborhoodMath.Pressure(1,0);var r=new NeighborhoodMath.Pressure(0,1);var both=new NeighborhoodMath.Pressure(1,1);
    Require(NeighborhoodMath.Band(.1f)==0f&&NeighborhoodMath.Band(.5f)==.5f&&NeighborhoodMath.Band(1f)==1f,"quantized neighbor growth");
    foreach(var k in Enum.GetValues<PlantFormKind>().Where(NeighborhoodMath.Valid))
    {
        foreach(var p in new[]{default(NeighborhoodMath.Pressure),l,r,both,new NeighborhoodMath.Pressure(.25f,.75f)})
        {
            for(int i=0;i<=4;i++)Require(NeighborhoodMath.Warp(k,1f,i/4f,0,p)==(i/4f,0f),k+" stationary base");
            for(int y=0;y<4;y++)for(int x=0;x<4;x++)
            {
                var a=NeighborhoodMath.Warp(k,1f,x/4f,y/4f,p);
                var b=NeighborhoodMath.Warp(k,1f,(x+1)/4f,y/4f,p);
                var c=NeighborhoodMath.Warp(k,1f,x/4f,(y+1)/4f,p);
                var d=NeighborhoodMath.Warp(k,1f,(x+1)/4f,(y+1)/4f,p);
                float Area((float X,float Y)a,(float X,float Y)b,(float X,float Y)c)=>(b.X-a.X)*(c.Y-a.Y)-(b.Y-a.Y)*(c.X-a.X);
                Require(Area(a,b,c)>0f&&Area(b,d,c)>0f,k+" positive mesh triangles");
            }
        }
        for(int y=0;y<=4;y++)for(int x=0;x<=4;x++)
        {
            var a=NeighborhoodMath.Warp(k,1f,x/4f,y/4f,l);var b=NeighborhoodMath.Warp(k,1f,1f-x/4f,y/4f,r);
            Require(Math.Abs(a.X+b.X-1f)<.0001f&&Math.Abs(a.Y-b.Y)<.0001f,k+" mirror world-side response");
        }
    }
    Require(NeighborhoodMath.Warp(PlantFormKind.GapSeeker,1f,.5f,1f,l).X>.8f&&NeighborhoodMath.Warp(PlantFormKind.CompanionBow,1f,.5f,1f,l).X<.2f,"different directional responses");
    Require(NeighborhoodMath.Warp(PlantFormKind.StandSpire,1f,.5f,1f,both).Y>1.25f,"crowd elongation");
    Require(NeighborhoodMath.Warp(PlantFormKind.FreeRosette,1f,1f,1f,default).X>1.3f,"isolated crown spread");
}

{
    var l=new NeighborhoodMath.Pressure(1,0);var r=new NeighborhoodMath.Pressure(0,1);var both=new NeighborhoodMath.Pressure(1,1);
    int h=ChronicleMath.Record(0,.25f,.45f,l);
    h=ChronicleMath.Record(h,.45f,.7f,r);h=ChronicleMath.Record(h,.7f,1f,l);
    Require(ChronicleMath.Count(h)==3&&ChronicleMath.At(h,0)==2&&ChronicleMath.At(h,1)==3&&ChronicleMath.At(h,2)==2,"chronicle records three ordered growth contexts");
    Require(ChronicleMath.Record(ChronicleMath.Record(h,1f,.25f,both),.25f,1f,both)==h,"old growth history cannot be overwritten");
    Require(ChronicleMath.Record(0,.95f,1f,l)==0&&ChronicleMath.Sanitize(-1)==0,"missing early history is not invented; corrupt values bounded");
    foreach(var kind in Enum.GetValues<PlantFormKind>().Where(ChronicleMath.Valid))
    {
        foreach(int code in new[]{0,73,82,91,154,100,292,ChronicleMath.Record(0,.25f,1f,both),h})
        {
            for(int x=0;x<=8;x++)Require(ChronicleMath.Warp(kind,1f,x/8f,0f,code)==(x/8f,0f),kind+" root fixed");
            for(int y=0;y<8;y++)for(int x=0;x<8;x++)
            {
                var a=ChronicleMath.Warp(kind,1f,x/8f,y/8f,code);
                var b=ChronicleMath.Warp(kind,1f,(x+1)/8f,y/8f,code);
                var c=ChronicleMath.Warp(kind,1f,x/8f,(y+1)/8f,code);
                var d=ChronicleMath.Warp(kind,1f,(x+1)/8f,(y+1)/8f,code);
                float Area((float X,float Y)a,(float X,float Y)b,(float X,float Y)c)=>(b.X-a.X)*(c.Y-a.Y)-(b.Y-a.Y)*(c.X-a.X);
                if(!(Area(a,b,c)>0f&&Area(b,d,c)>0f))throw new Exception(kind+" folded historical mesh");
            }
        }
        Require(Enumerable.Range(0,9).All(j=>Enumerable.Range(0,9).All(i=>ChronicleMath.Warp(kind,1f,i/8f,j/8f,73)==(i/8f,j/8f))),kind+" no prior pressure retains original shape");
    }
    Require(ChronicleMath.Warp(PlantFormKind.CrookedChronicle,1f,.5f,1f,82).X>.8f,"two early left pressures persist");
    Require(ChronicleMath.Warp(PlantFormKind.ReleaseCrown,1f,1f,1f,100).X>1.3f,"release crown expands late");
}

{
    var v=typeof(PhenotypeVocabulary);
    var f=PhenotypeVocabulary.Form(PlantFormKind.FanCrowned,1,default,0);
    var p=PhenotypeVocabulary.Pigment(39,1f);
    var languageFacts=new[]{f,p,f,PhenotypeVocabulary.F(PhenotypeVocabulary.Region.Habit,"shape","conflicting low-priority profile",10)};
    string prose=PhenotypeVocabulary.Prose(languageFacts);
    Require(prose.Contains("fan-shaped crown")&&prose.Contains("rose reproductive pigment")&&!prose.Contains("conflicting"),"language combines geometry and pigment and resolves conflicts");
    Require(prose==PhenotypeVocabulary.Prose(languageFacts.Reverse()),"stable language independent of ordering");
    Require(PhenotypeVocabulary.Resolve(languageFacts).Length==2,"language deduplicates repeated renderer languageFacts");
    Require(PhenotypeVocabulary.Prose(new[]{PhenotypeVocabulary.Form(PlantFormKind.GapSeeker,1,default,0)})=="","no latent neighbor effect");
    Require(PhenotypeVocabulary.Form(PlantFormKind.GapSeeker,1,new(1,0),0).Phrase.Contains("right")&&PhenotypeVocabulary.Form(PlantFormKind.GapSeeker,1,new(0,1),0).Phrase.Contains("left"),"individual directional expression");
    Require(PhenotypeVocabulary.Form(PlantFormKind.CrookedChronicle,1,default,73).Phrase==null&&PhenotypeVocabulary.Form(PlantFormKind.CrookedChronicle,1,default,82).Phrase!=null,"history-dependent actual expression");
    Require(PhenotypeVocabulary.History(0)==""&&PhenotypeVocabulary.History(82).Contains("Early growth: competition on the left"),"unknown history is not invented");
    Require(PhenotypeVocabulary.Surface(SurfaceFinishKind.RainBeads,false).Phrase==null&&PhenotypeVocabulary.Surface(SurfaceFinishKind.RainBeads,true).Phrase!=null,"rain beads described only while active");
    Require(PhenotypeVocabulary.Pose(ResponsiveHabitKind.DawnPetals,8,0,0,0).Phrase==null&&PhenotypeVocabulary.Pose(ResponsiveHabitKind.DawnPetals,0,0,0,0).Phrase!=null,"neutral articulation not described");
    Require(PhenotypeVocabulary.Pigment(51,0).Phrase!=PhenotypeVocabulary.Pigment(51,1).Phrase,"growth-stage vocabulary");
    Require(PhenotypeVocabulary.Mosaic(1<<(int)MosaicMath.OrganState.Bud,1,4).Phrase.Contains("one localized")&&!PhenotypeVocabulary.Mosaic(1<<(int)MosaicMath.OrganState.Bud,1,4).Phrase.Contains("stout"),"mosaic vocabulary reflects actual changed states");
    Require(PhenotypeVocabulary.Pose(ResponsiveHabitKind.None,0,0,0,0).Phrase==null&&PhenotypeVocabulary.Ground(GroundHabitKind.None).Phrase==null,"unexpressed paths produce no observations");
    foreach(var k in Enum.GetValues<PlantFormKind>().Where(k=>k!=PlantFormKind.None))
        Require(PhenotypeVocabulary.Form(k,1,default,0,true).Phrase!=null,"form vocabulary "+k);
    foreach(var k in Enum.GetValues<ResponsiveHabitKind>().Where(k=>k!=ResponsiveHabitKind.None))
        Require(PhenotypeVocabulary.Pose(k,4,0,0,0,true).Phrase!=null,"pose vocabulary "+k);
    foreach(var k in Enum.GetValues<GroundHabitKind>().Where(k=>k!=GroundHabitKind.None))
        Require(PhenotypeVocabulary.Ground(k).Phrase!=null,"ground vocabulary "+k);
    foreach(var k in Enum.GetValues<OrganFormKind>().Where(k=>k!=OrganFormKind.None))
        Require(PhenotypeVocabulary.Organ(k).Phrase!=null,"organ vocabulary "+k);
    foreach(var k in Enum.GetValues<CanopyArchitectureKind>().Where(k=>k!=CanopyArchitectureKind.None))
        Require(PhenotypeVocabulary.Architecture(k).Phrase!=null,"canopy vocabulary "+k);
    foreach(var k in Enum.GetValues<BotanicalDetailKind>().Where(k=>k!=BotanicalDetailKind.None))
        Require(PhenotypeVocabulary.Detail(k).Phrase!=null,"botanical vocabulary "+k);
    foreach(var k in Enum.GetValues<SurfaceFinishKind>().Where(k=>k!=SurfaceFinishKind.None))
        Require(PhenotypeVocabulary.Surface(k,true,true).Phrase!=null,"surface vocabulary "+k);
    foreach(var k in Enum.GetValues<WindHabitKind>().Where(k=>k!=WindHabitKind.None))
        Require(PhenotypeVocabulary.Wind(k).Phrase!=null,"wind vocabulary "+k);
    for(int mode=1;mode<=55;mode++)Require(PhenotypeVocabulary.Pigment(mode,1).Phrase!=null,"procedural vocabulary "+mode);
}

{
    const int w=80,h=80;
    var art=new OrganSpriteMath.Pixel[w*h];
    for(int y=8;y<64;y++)for(int x=4;x<75;x++)
    {
        float a=(x-23)*(x-23)/190f+(y-28)*(y-28)/380f;
        float b=(x-57)*(x-57)/175f+(y-42)*(y-42)/340f;
        if(a<=1||b<=1)art[y*w+x]=new(62,143,37,255);
    }
    for(int y=31;y<40;y++)for(int x=18;x<27;x++)art[y*w+x]=new(222,183,61,255);
    var anatomy=VascularMath.Build(art,w,h);
    Require(anatomy!=null&&anatomy.Territories>=2&&anatomy.VeinPixels>0&&anatomy.ThinPixels>0,"vascular anatomy has local territories, veins and thin tissue");
    var again=VascularMath.Build(art,w,h);
    Require(anatomy.Mask.SequenceEqual(again.Mask),"deterministic vascular map");
    Require(Enumerable.Range(0,art.Length).All(i=>anatomy.Mask[i].A==0||VascularMath.Eligible(art[i])),"no fruit, air or protected pixels in anatomy mask");
    Require(anatomy.Mask.Where(p=>p.A>0).Select(p=>p.B).Distinct().Count()>1,"local territory variation");
    Require(VascularMath.Build(new OrganSpriteMath.Pixel[w*h],w,h)==null,"blank art rejected");
    Require(VascularMath.Light(0f)==0f&&VascularMath.Light(1f)==1f&&VascularMath.Light(float.NaN)==0f&&VascularMath.Wet(0f)==0f&&VascularMath.Wet(1f)==1f,"bounded environmental response");
}

{
    Require(Enum.GetValues<BotanicalDetailKind>().Count(LifeSuiteMath.Valid)==20,"twenty unique botanical life modes");
    Require(Enumerable.Range(1,20).Count(LifeSuiteMath.Overlay)==5&&Enumerable.Range(1,20).Count(LifeSuiteMath.AtlasMode)==3,"bounded extra planes and structural atlases");
    Require(LifeSuiteMath.Lateral(1,0)==1&&LifeSuiteMath.Lateral(0,1)==2&&LifeSuiteMath.Lateral(1,1)==3,"directional local connections");
    Require(LifeSuiteMath.Stress(3,43,1)>.9f&&LifeSuiteMath.Stress(17,-8,1)==1&&LifeSuiteMath.Stress(3,20,1)==0,"bounded stress");
    Require(!LifeSuiteMath.Active(10,1,0,.5f,0,1,0,0)&&LifeSuiteMath.Active(10,0,0,0,0,1,0,0),"night-only signaling");
    Require(!LifeSuiteMath.Active(18,1,0,.5f,0,1,0,0)&&LifeSuiteMath.Active(18,1,0,.5f,0,1,0,2),"relay requires actual compatible connection");
    const int w=80,h=80;var image=new OrganSpriteMath.Pixel[w*h];
    for(int y=6;y<68;y++)for(int x=7;x<74;x++)
    {
        if((x-28)*(x-28)/280f+(y-31)*(y-31)/480f<1||(x-50)*(x-50)/260f+(y-44)*(y-44)/430f<1)
            image[y*w+x]=new(55,132,38,255);
    }
    for(int y=30;y<42;y++)for(int x=25;x<34;x++)image[y*w+x]=new(210,171,58,255);
    for(int y=48;y<58;y++)for(int x=47;x<55;x++)image[y*w+x]=new(238,222,205,255);
    var a=LifeSuiteMath.Analyze(image,w,h);Require(a!=null&&a.Leaves>=2&&a.Organs>=1&&a.OrganPixels>0,"derived combined leaf and reproductive anatomy");
    Require(a.Mask.Where(p=>p.A==128).Any()&&a.Mask.Where(p=>p.A==255).Any(),"separate anatomy classes");
    Require(Enumerable.Range(0,image.Length).All(i=>a.Mask[i].A==0||image[i].A>=90),"air excluded from atlas");
    foreach(int mode in new[]{2,11,19})foreach(int frame in new[]{0,4,8})
    {
        var result=LifeSuiteMath.Frame(a,image,mode,frame);
        Require(result.Length==image.Length&&Enumerable.Range(0,image.Length).All(i=>result[i].A>=image[i].A),"structural atlas preserves native alpha");
    }
}

{
    Require(Enum.GetValues<BotanicalDetailKind>().Count(SculptureMath.Valid)==5,"five distinct sculpture traits");
    const int w=64,h=64;var art=new OrganSpriteMath.Pixel[w*h];
    for(int y=6;y<54;y++)for(int x=8;x<56;x++)
        if((x-31)*(x-31)/390f+(y-30)*(y-30)/520f<1)
            art[y*w+x]=new((byte)(45+x/3),(byte)(106+y),(byte)(32+x/4),255);
    for(int y=39;y<47;y++)for(int x=25;x<32;x++)art[y*w+x]=new(229,182,48,255);
    var a=LifeSuiteMath.Analyze(art,w,h);Require(a!=null&&a.Organs>0,"sculpture source anatomy");
    int W=SculptureMath.Side(w),H=SculptureMath.Side(h),ox=(W-w)/2,oy=(H-h)/2;
    for(int mode=21;mode<=25;mode++)
    {
        var frames=Enumerable.Range(0,9).Select(f=>SculptureMath.Frame(art,w,h,a,mode,f)).ToArray();
        Require(frames.All(f=>f!=null&&f.Length==W*H),"expanded rig frames");
        Require(frames.All(f=>Enumerable.Range(0,art.Length).All(i=>art[i].A==0||f[(i/w+oy)*W+i%w+ox]==art[i])),"all original RGBA preserved");
        Require(frames.All(f=>f.Count(p=>p.A>0)>=art.Count(p=>p.A>0)),"no source holes");
        Require(frames[8].Count(p=>p.A>0)>art.Count(p=>p.A>0)+20,"visible added structural growth");
        Require(!frames[0].SequenceEqual(frames[8])&&Enumerable.Range(1,8).Count(i=>!frames[i].SequenceEqual(frames[i-1]))>=6,"distinct progressive developmental frames");
    }
}

{
    Require(Enum.GetValues<BotanicalDetailKind>().Count(SculptureMath.ForgeValid)==20,"twenty independent forge traits");
    const int w=64,h=64;var art=new OrganSpriteMath.Pixel[w*h];
    for(int y=6;y<54;y++)for(int x=8;x<56;x++)
        if((x-31)*(x-31)/390f+(y-30)*(y-30)/520f<1)
            art[y*w+x]=new((byte)(45+x/3),(byte)(106+y),(byte)(32+x/4),255);
    for(int y=39;y<47;y++)for(int x=25;x<32;x++)art[y*w+x]=new(229,182,48,255);
    var a=LifeSuiteMath.Analyze(art,w,h);Require(a!=null&&a.Organs>0,"forge source anatomy");
    int W=SculptureMath.Side(w),H=SculptureMath.Side(h),ox=(W-w)/2,oy=(H-h)/2;
    var finalFrames=new List<OrganSpriteMath.Pixel[]>();
    for(int mode=26;mode<=45;mode++)
    {
        var frames=Enumerable.Range(0,9).Select(f=>SculptureMath.Frame(art,w,h,a,mode,f)).ToArray();
        Require(frames.All(f=>f!=null&&f.Length==W*H),"forge "+mode+" padded frames");
        Require(frames.All(f=>Enumerable.Range(0,art.Length).All(i=>art[i].A==0||f[(i/w+oy)*W+i%w+ox].A==art[i].A&&(f[(i/w+oy)*W+i%w+ox]==art[i]||SculptureMath.ForgeTintAllowed(mode,a,art[i],i)))),"forge "+mode+" protected source RGBA");
        Require(Enumerable.Range(1,8).Count(i=>!frames[i].SequenceEqual(frames[i-1]))>=3,"forge "+mode+" varied phase frames");
        Require(finalFrames.All(f=>!f.SequenceEqual(frames[8])),"forge "+mode+" distinct final phenotype");
        finalFrames.Add(frames[8]);
    }
}
