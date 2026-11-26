using System;
using System.Collections.Generic;
using System.Linq;
using Pixel=HorticultureNovelSeeds.OrganSpriteMath.Pixel;

namespace HorticultureNovelSeeds
{
    public enum CanopyArchitectureKind { None,ForkedCrown,TieredCanopy,SpreadingRosette,FlowerSpray,AbundantClusters }
    internal static class CanopyArchitectureMath
    {
        internal static bool Valid(CanopyArchitectureKind k)=>k>=CanopyArchitectureKind.ForkedCrown&&k<=CanopyArchitectureKind.AbundantClusters;
        internal readonly record struct Bounds(int X,int Y,int W,int H);
        internal sealed record Module(Pixel[] Pixels,Bounds Box,int Reconstructed=0,int RemainingOcclusion=0,int Trimmed=0);
        internal sealed record Parts(Bounds Body,Module Leaves,Module Fruit,(float X,float Y)[] Anchors);
        // Placement is normalized to the visible source silhouette, not its
        // transparent margins. Each module retains the source artwork.
        internal readonly record struct Placement(bool Fruit,float X,float Y,float Scale,float Angle,bool Front);
        internal static Parts Extract(Pixel[] p,int w,int h,bool repairOcclusion=true,bool leafSynthesis=true)
        {
            if(w<16||h<16||p.Length!=w*h)return null;
            var body=Enumerable.Range(0,p.Length).Where(i=>p[i].A>=100).ToArray();
            if(body.Length<24)return null;
            Bounds Box(IEnumerable<int> a)
            {
                var q=a.ToArray();int x=q.Min(i=>i%w),y=q.Min(i=>i/w);
                return new(x,y,q.Max(i=>i%w)-x+1,q.Max(i=>i/w)-y+1);
            }
            var b=Box(body);
            if(b.W<6||b.H<6)return null;
            var greens=body.Where(i=>BotanicalDetailMath.Green(p[i])).ToArray();
            if(greens.Length<24)return null;
            var leafset=greens.Where(i=>i/w>=b.Y+b.H*.27f).ToArray();
            if(repairOcclusion)
            {
                var clean=leafset.Where(i=>!OrganSpriteMath.Reproductive(p[i])).ToArray();
                if(clean.Length>=24)leafset=clean;
            }
            Module Crop(IEnumerable<int> source,bool leaf)
            {
                var selected=new HashSet<int>(source);
                if(selected.Count<8)return null;
                // Include neighboring painted shadows, not transparent gaps
                // or reproductive tissue in the leaf module.
                foreach(var i in selected.ToArray())
                    for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++)
                    {
                        int x=i%w+dx,y=i/w+dy;if(x<0||x>=w||y<0||y>=h)continue;
                        int j=y*w+x;var c=p[j];
                        if(c.A<36)continue;
                        if(leaf ? c.G>=c.R*.86f&&c.G>=c.B*.98f&&!OrganSpriteMath.Reproductive(c) :
                            c.R>=c.G*.83f&&Math.Abs(c.R-p[i].R)+Math.Abs(c.G-p[i].G)+Math.Abs(c.B-p[i].B)<175)selected.Add(j);
                    }
                var reconstructed=new Dictionary<int,Pixel>();
                int unresolved=0,trimmed=0;
                if(leaf&&repairOcclusion)
                {
                    // Recover attached opaque crown tissue; never traverse air.
                    int floorY=(int)MathF.Ceiling(b.Y+b.H*.27f);
                    int limit=Math.Clamp((int)MathF.Ceiling(Math.Max(b.W,b.H)*.40f),6,128);
                    // A small exclusion halo around real reproductive
                    // components removes pale fruit-colored fringe pixels
                    // from the sample set, not from the final crown.
                    if(leafSynthesis)
                    {
                        int r=Math.Clamp((int)MathF.Round(Math.Min(b.W,b.H)*.025f),2,6);
                        var halo=new HashSet<int>();
                        var fruitParts=OrganSpriteMath.Parts(p.Select(OrganSpriteMath.Reproductive).ToArray(),w,h,body.Length,false);
                        foreach(var part in fruitParts)
                            foreach(int i in part.Indices)
                            {
                                int x=i%w,y=i/w;
                                for(int dy=-r;dy<=r;dy++)for(int dx=-r;dx<=r;dx++)
                                {
                                    int xx=x+dx,yy=y+dy;
                                    if(dx*dx+dy*dy<=r*r&&xx>=0&&xx<w&&yy>=floorY&&yy<h)halo.Add(yy*w+xx);
                                }
                            }
                        var clean=selected.Where(i=>!halo.Contains(i)&&!OrganSpriteMath.Reproductive(p[i])&&p[i].A>=90).ToArray();
                        if(clean.Length>=24&&clean.Length>=selected.Count*.22f)
                            selected.ExceptWith(halo);
                    }
                    var trusted=new HashSet<int>(selected.Where(i=>p[i].A>=90&&!OrganSpriteMath.Reproductive(p[i])));
                    if(trusted.Count<8)trusted=new HashSet<int>(selected);
                    var owner=new int[p.Length];Array.Fill(owner,-1);
                    var distance=new int[p.Length];Array.Fill(distance,int.MaxValue);
                    var queue=new Queue<int>();
                    foreach(int i in (leafSynthesis?trusted:selected).OrderBy(i=>i))
                    {
                        if(p[i].A<90||i/w<floorY-1)continue;
                        owner[i]=i;distance[i]=0;queue.Enqueue(i);
                    }
                    var directions=new[]{(-1,0),(1,0),(0,-1),(0,1)};
                    while(queue.Count>0)
                    {
                        int i=queue.Dequeue();if(distance[i]>=limit)continue;
                        int x=i%w,y=i/w;
                        foreach(var (dx,dy) in directions)
                        {
                            int xx=x+dx,yy=y+dy;if(xx<0||xx>=w||yy<floorY||yy>=h)continue;
                            int j=yy*w+xx;
                            if(p[j].A<90||owner[j]>=0)continue;
                            owner[j]=owner[i];distance[j]=distance[i]+1;queue.Enqueue(j);
                        }
                    }
                    float Lum(Pixel c)=>.299f*c.R+.587f*c.G+.114f*c.B;
                    for(int y=floorY;y<h;y++)for(int x=0;x<w;x++)
                    {
                        int i=y*w+x;if(p[i].A<90||selected.Contains(i))continue;
                        if(owner[i]<0){if(OrganSpriteMath.Reproductive(p[i]))unresolved++;continue;}
                        var original=p[i];
                        // Blend locally propagated foliage colors and retain
                        // restrained original shading, never fruit hue.
                        float rr=0,gg=0,bb=0,total=0;
                        for(int dy=-2;dy<=2;dy++)for(int dx=-2;dx<=2;dx++)
                        {
                            int xx=x+dx,yy=y+dy;if(xx<0||xx>=w||yy<floorY-1||yy>=h)continue;
                            int j=yy*w+xx;if(owner[j]<0)continue;
                            var c=p[owner[j]];float weight=1f/(1+dx*dx+dy*dy);
                            rr+=c.R*weight;gg+=c.G*weight;bb+=c.B*weight;total+=weight;
                        }
                        if(total<=0)continue;
                        rr/=total;gg/=total;bb/=total;
                        float reference=.299f*rr+.587f*gg+.114f*bb;
                        // Reflect the target offset back into the nearest
                        // surviving leaf. This transfers painted texture and
                        // shadows from the source rather than from the fruit.
                        int donor=owner[i];
                        if(leafSynthesis)
                        {
                            int sx=donor%w,sy=donor/w;
                            int xx=(int)MathF.Round(sx+(sx-x)*.65f),yy=(int)MathF.Round(sy+(sy-y)*.65f);
                            if(xx>=0&&xx<w&&yy>=floorY&&yy<h&&trusted.Contains(yy*w+xx))donor=yy*w+xx;
                        }
                        var exemplar=p[donor];
                        float shade=leafSynthesis?1f:Math.Clamp(.85f+.20f*(Lum(original)/Math.Max(15f,reference)),.78f,1.17f);
                        float blend=leafSynthesis?.64f:0f;
                        byte C(float v,byte example)=>(byte)Math.Clamp((int)MathF.Round((v*(1-blend)+example*blend)*shade),0,255);
                        reconstructed[i]=new Pixel(C(rr,exemplar.R),C(gg,exemplar.G),C(bb,exemplar.B),original.A);
                    }
                    if(leafSynthesis)trimmed=RefineTips(p,w,h,selected,reconstructed,distance,floorY);
                    foreach(int i in reconstructed.Keys)selected.Add(i);
                    // Recover antialiasing only where source alpha exists.
                    foreach(int i in reconstructed.Keys.ToArray())
                    {
                        int x=i%w,y=i/w;
                        for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++)
                        {
                            int xx=x+dx,yy=y+dy;if(xx<0||xx>=w||yy<floorY||yy>=h)continue;
                            int j=yy*w+xx;if(p[j].A==0||p[j].A>=90||selected.Contains(j))continue;
                            var c=reconstructed[i];
                            reconstructed[j]=new Pixel(c.R,c.G,c.B,p[j].A);selected.Add(j);
                        }
                    }
                }
                var box=Box(selected);
                if(box.W<2||box.H<2)return null;
                var pixels=new Pixel[box.W*box.H];
                foreach(int i in selected)pixels[(i/w-box.Y)*box.W+i%w-box.X]=reconstructed.TryGetValue(i,out var value)?value:p[i];
                return new(pixels,box,reconstructed.Count,unresolved,trimmed);
            }
            var leaves=Crop(leafset,true);
            var fruitMask=p.Select(OrganSpriteMath.Reproductive).ToArray();
            var candidates=OrganSpriteMath.Parts(fruitMask,w,h,body.Length,false);
            var fruit=candidates.Count==0?null:Crop(candidates[0].Indices,false);
            // Distribute the extra reproductive modules only at existing green
            // tissue. Prefer upper/middle leaves, distinct and separated.
            var anchors=new List<(float X,float Y)>();
            foreach(var target in new[]{(.18f,.62f),(.82f,.66f),(.48f,.86f),(.30f,.43f),(.72f,.40f)})
            {
                var pts=greens.Select(i=>((i%w-b.X+.5f)/b.W,(i/w-b.Y+.5f)/b.H))
                    .Where(q=>q.Item2>.30f&&anchors.All(a=>(a.X-q.Item1)*(a.X-q.Item1)+(a.Y-q.Item2)*(a.Y-q.Item2)>.055f))
                    .OrderBy(q=>(q.Item1-target.Item1)*(q.Item1-target.Item1)+(q.Item2-target.Item2)*(q.Item2-target.Item2)).ToArray();
                if(pts.Length>0)anchors.Add(pts[0]);
            }
            return new(b,leaves,fruit,anchors.ToArray());
        }
        // Replace source-organ brightness with actual leaf texture. Where an
        // attached occlusion projects beyond the canopy, taper its silhouette
        // into a leaf tip. Only remove pixels reachable from existing air.
        private static int RefineTips(Pixel[] p,int w,int h,HashSet<int> seeds,
            Dictionary<int,Pixel> restored,int[] distance,int floorY)
        {
            var visited=new HashSet<int>();var remove=new HashSet<int>();
            var directions=new[]{(-1,0),(1,0),(0,-1),(0,1)};
            // Separate exterior air from enclosed natural windows. Only
            // exterior-connected occlusions can change the outline.
            var exterior=new bool[p.Length];var airQueue=new Queue<int>();
            void MarkAir(int x,int y)
            {
                if(x<0||x>=w||y<0||y>=h)return;
                int i=y*w+x;if(exterior[i]||p[i].A>=90)return;
                exterior[i]=true;airQueue.Enqueue(i);
            }
            for(int x=0;x<w;x++){MarkAir(x,0);MarkAir(x,h-1);}
            for(int y=0;y<h;y++){MarkAir(0,y);MarkAir(w-1,y);}
            while(airQueue.Count>0)
            {
                int i=airQueue.Dequeue(),x=i%w,y=i/w;
                foreach(var (dx,dy) in directions)MarkAir(x+dx,y+dy);
            }
            bool Air(int i)=>exterior[i];
            IEnumerable<int> Neighbors(int i)
            {
                int x=i%w,y=i/w;
                foreach(var (dx,dy) in directions)
                {
                    int xx=x+dx,yy=y+dy;
                    if(xx>=0&&xx<w&&yy>=floorY&&yy<h)yield return yy*w+xx;
                }
            }
            foreach(int start in restored.Keys.OrderBy(i=>i))
            {
                if(p[start].A<90||!visited.Add(start))continue;
                var group=new List<int>();var contacts=new HashSet<int>();var edges=new List<int>();
                var queue=new Queue<int>();queue.Enqueue(start);
                while(queue.Count>0)
                {
                    int i=queue.Dequeue();group.Add(i);
                    bool edge=false;
                    foreach(int j in Neighbors(i))
                    {
                        if(Air(j))edge=true;
                        else if(seeds.Contains(j))contacts.Add(j);
                        else if(restored.ContainsKey(j)&&visited.Add(j))queue.Enqueue(j);
                    }
                    if(edge||i%w==0||i%w==w-1||i/w==h-1)edges.Add(i);
                }
                // Embedded organ: do not change its alpha. Small edge areas
                // and broad attachment zones also retain their silhouette.
                if(group.Count<32||edges.Count<4||contacts.Count<3)continue;
                float ax=(float)contacts.Average(i=>i%w),ay=(float)contacts.Average(i=>i/w);
                float cx=(float)group.Average(i=>i%w),cy=(float)group.Average(i=>i/w);
                float dx=cx-ax,dy=cy-ay,len=MathF.Sqrt(dx*dx+dy*dy);
                float radius=MathF.Sqrt(group.Count/MathF.PI);
                if(len<Math.Max(2.5f,radius*.43f))continue;
                dx/=len;dy/=len;
                float Along(int i)=>(i%w-ax)*dx+(i/w-ay)*dy;
                float Across(int i)=>-(i%w-ax)*dy+(i/w-ay)*dx;
                float reach=group.Max(Along);
                if(reach<5f)continue;
                float contactSpread=contacts.Max(Along)-contacts.Min(Along);
                if(contactSpread>reach*.58f)continue;
                float width=Math.Max(2f,Math.Min(group.Max(i=>Math.Abs(Across(i)))*.90f,radius*.80f));
                bool Outside(int i)
                {
                    float t=Along(i)/reach;
                    if(t<.13f||distance[i]<=2)return false;
                    // Lanceolate shape: broad near its base, pointed tip.
                    float half=width*MathF.Pow(MathF.Sin(MathF.PI*Math.Clamp(t,0f,1f)),.72f);
                    return Math.Abs(Across(i))>Math.Max(.65f,half);
                }
                var groupSet=new HashSet<int>(group);
                var trimmed=new HashSet<int>();
                foreach(int i in edges)if(Outside(i)&&trimmed.Add(i))queue.Enqueue(i);
                while(queue.Count>0)
                {
                    int i=queue.Dequeue();
                    foreach(int j in Neighbors(i))
                        if(groupSet.Contains(j)&&Outside(j)&&trimmed.Add(j))queue.Enqueue(j);
                }
                // Never erase a majority of a component; that would create
                // an obviously missing branch instead of a smaller leaf.
                if(trimmed.Count>group.Count*.48f)continue;
                foreach(int i in trimmed)remove.Add(i);
                // Add restrained midrib shading to the new lamina, based on
                // its constructed axis and not the old fruit's lightness.
                foreach(int i in group)
                {
                    if(trimmed.Contains(i))continue;
                    var c=restored[i];float t=Math.Clamp(Along(i)/reach,0f,1f);
                    float q=Math.Abs(Across(i))/Math.Max(1f,width);
                    float factor=1f+.12f*MathF.Exp(-q*q*14f)*MathF.Sin(MathF.PI*t)-.11f*Math.Clamp(q,0f,1f);
                    byte C(byte v)=>(byte)Math.Clamp((int)MathF.Round(v*factor),0,255);
                    restored[i]=new Pixel(C(c.R),C(c.G),C(c.B),c.A);
                }
            }
            foreach(int i in remove)restored.Remove(i);
            return remove.Count;
        }

        internal static Placement[] Recipe(CanopyArchitectureKind k,Parts p)
        {
            if(!Valid(k)||p?.Leaves==null)return Array.Empty<Placement>();
            Placement L(float x,float y,float s,float a,bool front=false)=>new(false,x,y,s,a,front);
            Placement F(float x,float y,float s,float a)=>new(true,x,y,s,a,true);
            return k switch
            {
                CanopyArchitectureKind.ForkedCrown=>new[]{L(.08f,.72f,1.02f,-33f),L(.92f,.77f,.95f,28f)},
                CanopyArchitectureKind.TieredCanopy=>new[]{L(.28f,.48f,1.06f,-13f,true),L(.68f,.84f,.93f,17f),L(.48f,1.01f,.76f,-5f)},
                CanopyArchitectureKind.SpreadingRosette=>new[]{L(.07f,.31f,.94f,-55f),L(.93f,.34f,.88f,52f),L(.48f,.26f,.66f,0f,true)},
                CanopyArchitectureKind.FlowerSpray when p.Fruit!=null=>new[]{L(.16f,.80f,.70f,-26f),L(.84f,.88f,.73f,25f),F(.06f,.91f,1.16f,-16f),F(.94f,1.00f,1.12f,12f),F(.50f,1.04f,1.08f,0f)},
                CanopyArchitectureKind.AbundantClusters when p.Fruit!=null=>p.Anchors.Select((a,i)=>F(a.X,a.Y,1.17f+.08f*(i%2),i%2==0?-11f:12f)).ToArray(),
                _=>Array.Empty<Placement>()
            };
        }
    }
}
