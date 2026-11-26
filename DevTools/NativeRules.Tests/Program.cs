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
foreach (PlantFormKind kind in Enum.GetValues<PlantFormKind>().Where(k=>k!=PlantFormKind.None))
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
