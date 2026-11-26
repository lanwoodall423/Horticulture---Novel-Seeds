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
