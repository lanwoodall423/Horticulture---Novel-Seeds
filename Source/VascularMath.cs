using System;
using System.Collections.Generic;
using System.Linq;
using Pixel=HorticultureNovelSeeds.OrganSpriteMath.Pixel;

namespace HorticultureNovelSeeds
{
    // An art-derived anatomy proxy, not a claim of exact botanical segmentation.
    // Distance-to-edge + geodesic watersheds identify local blade territories.
    // Each territory has its own principal axis and branching vein coordinates.
    internal static class VascularMath
    {
        internal static bool Valid(BotanicalDetailKind k)=>k>=BotanicalDetailKind.IvoryNerves&&k<=BotanicalDetailKind.BronzeReticulum;
        internal sealed record Anatomy(Pixel[] Mask,int Territories,int TissuePixels,int VeinPixels,int ThinPixels,int W,int H);
        private static byte Byte(float x)=>(byte)Math.Clamp((int)MathF.Round(x*255),0,255);
        private static float Unit(float x)=>Math.Clamp(x,0f,1f);
        private static float Smooth(float x){x=Unit(x);return x*x*(3-2*x);}
        internal static bool Eligible(Pixel p)=>BotanicalDetailMath.Green(p)&&!OrganSpriteMath.Reproductive(p);
        internal static Anatomy Build(Pixel[] src,int w,int h)
        {
            if(w<16||h<16||w>512||h>512||src.Length!=w*h)return null;
            int n=w*h;bool[] leaf=src.Select(Eligible).ToArray();int count=leaf.Count(x=>x);
            if(count<40)return null;
            int[] dist=new int[n];
            for(int i=0;i<n;i++)dist[i]=leaf[i]?9999:0;
            int D(int x,int y)=>x<0||x>=w||y<0||y>=h?0:dist[y*w+x];
            for(int y=0;y<h;y++)for(int x=0;x<w;x++)
            {
                int i=y*w+x;if(!leaf[i])continue;
                dist[i]=Math.Min(dist[i],Math.Min(Math.Min(D(x-1,y)+3,D(x,y-1)+3),Math.Min(D(x-1,y-1)+4,D(x+1,y-1)+4)));
            }
            for(int y=h-1;y>=0;y--)for(int x=w-1;x>=0;x--)
            {
                int i=y*w+x;if(!leaf[i])continue;
                dist[i]=Math.Min(dist[i],Math.Min(Math.Min(D(x+1,y)+3,D(x,y+1)+3),Math.Min(D(x-1,y+1)+4,D(x+1,y+1)+4)));
            }
            int spacing=Math.Clamp(Math.Min(w,h)/9,6,34);
            var seeds=new List<int>();
            // Thick interiors first. Seed spacing is local to the actual art.
            foreach(int i in Enumerable.Range(0,n).Where(i=>dist[i]>=6).OrderByDescending(i=>dist[i]).ThenBy(i=>i))
            {
                int x=i%w,y=i/w;
                if(seeds.Any(j=>(j%w-x)*(j%w-x)+(j/w-y)*(j/w-y)<spacing*spacing))continue;
                seeds.Add(i);if(seeds.Count==24)break;
            }
            if(seeds.Count<2)return null;
            int[] owner=new int[n],cost=new int[n];Array.Fill(owner,-1);Array.Fill(cost,int.MaxValue);
            var queue=new PriorityQueue<int,(int,int)>();
            for(int s=0;s<seeds.Count;s++){int i=seeds[s];owner[i]=s;cost[i]=0;queue.Enqueue(i,(0,i));}
            while(queue.TryDequeue(out int i,out var pri))
            {
                if(pri.Item1!=cost[i])continue;
                int x=i%w,y=i/w;
                for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++)
                {
                    if(dx==0&&dy==0)continue;
                    int xx=x+dx,yy=y+dy;if(xx<0||xx>=w||yy<0||yy>=h)continue;
                    int j=yy*w+xx;if(!leaf[j])continue;
                    // Prefer routes inside thicker tissue, not across the air.
                    int d=cost[i]+(dx==0||dy==0?10:14)+Math.Max(0,9-dist[j]);
                    if(d>=cost[j])continue;cost[j]=d;owner[j]=owner[i];queue.Enqueue(j,(d,j));
                }
            }
            var territories=new List<int>[seeds.Count];for(int s=0;s<seeds.Count;s++)territories[s]=new();
            for(int i=0;i<n;i++)if(owner[i]>=0)territories[owner[i]].Add(i);
            var result=new Pixel[n];int regions=0,tissue=0,veins=0,thin=0;
            foreach(var part in territories)
            {
                if(part.Count<12)continue;
                float cx=part.Average(i=>(float)(i%w)),cy=part.Average(i=>(float)(i/w));
                float xx=0,yy=0,xy=0;
                foreach(int i in part){float x=i%w-cx,y=i/w-cy;xx+=x*x;yy+=y*y;xy+=x*y;}
                float ang=.5f*MathF.Atan2(2*xy,xx-yy),ax=MathF.Cos(ang),ay=MathF.Sin(ang);
                if(ax*(cx-(w-1)*.5f)+ay*cy<0){ax=-ax;ay=-ay;}
                float U(int i)=>(i%w-cx)*ax+(i/w-cy)*ay;
                float V(int i)=>(i%w-cx)*(-ay)+(i/w-cy)*ax;
                float lo=part.Min(U),hi=part.Max(U),len=Math.Max(4,hi-lo);
                float radius=Math.Max(1.5f,part.Max(i=>Math.Abs(V(i))));
                float maxD=Math.Max(6,part.Max(i=>dist[i]));
                // Seed-derived variation is region-specific and stable across
                // remeshes. It does not depend on the entire sprite's UV.
                uint hash=unchecked((uint)(part[0]*1103515245+12345));
                float sector=(hash%997)/996f;
                foreach(int i in part)
                {
                    float u=Unit((U(i)-lo)/len),v=V(i)/radius;
                    float curve=.12f*MathF.Sin(u*MathF.PI)*MathF.Sin(sector*6.28f);
                    float across=Math.Abs(v-curve);
                    float rib=1-Smooth(across*radius/Math.Max(1.6f,radius*.20f));
                    float branches=Math.Clamp(len/5f,2f,7f);
                    float phase=u*branches-across*.82f;
                    float line=Math.Abs(phase-MathF.Round(phase));
                    float secondary=(1-Smooth(line/.24f))*.70f*(.50f+.50f*Smooth(across/.28f));
                    float taper=Smooth(u/.12f)*Smooth((1-u)/.10f);
                    float vein=Math.Max(rib,secondary)*(.40f+.60f*taper);
                    float thickness=Unit(dist[i]/maxD);
                    float lamina=1-Smooth(thickness);
                    // R veins, G thin lamina, B local territory pigment,
                    // A eligible foliage. Every protected source pixel is 0.
                    result[i]=new(Byte(vein),Byte(lamina),Byte(sector),255);
                    tissue++;if(vein>.33f)veins++;if(lamina>.55f)thin++;
                }
                regions++;
            }
            if(regions<2||tissue<40||veins<10||thin<10)return null;
            return new(result,regions,tissue,veins,thin,w,h);
        }
        internal static float Light(float sky)=>Smooth((float.IsFinite(sky)?sky:0f)-.12f<0?0f:((float.IsFinite(sky)?sky:0f)-.12f)/.65f);
        internal static float Wet(float rain)=>Smooth((float.IsFinite(rain)?rain:0f)*1.5f);
    }
}
