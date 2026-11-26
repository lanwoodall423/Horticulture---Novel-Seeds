using System;
using System.Linq;
using System.Collections.Generic;
using Pixel=HorticultureNovelSeeds.OrganSpriteMath.Pixel;

namespace HorticultureNovelSeeds
{
    // Visual interpretation of existing mechanics, never a stat provider.
    internal static class AdaptivePhenotypeMath
    {
        [Flags] internal enum Cue { None=0,Yield=1,Cold=2,Heat=4,Resource=8,Perennial=16 }
        internal const int FullerArchitecture=80;
        internal readonly record struct State(Cue Cues,bool Blocked,bool Regrown)
        {
            internal bool Valid=>Cues!=Cue.None;
            internal bool Has(Cue cue)=>(Cues&cue)!=0;
        }
        internal static Cue ForTrait(string id)=>ExpressionMath.Id(id) switch
        {
            "HighYield"=>Cue.Yield,
            "ColdResistant"=>Cue.Cold,
            "HeatResistant"=>Cue.Heat,
            "ResourceDependent_Mulch" or "ResourceDependent_Hay" or "ResourceDependent_Fungus"=>Cue.Resource,
            "Perennial"=>Cue.Perennial,
            _=>Cue.None
        };
        internal static Cue From(IEnumerable<string> ids)=>ids.Aggregate(Cue.None,(a,b)=>a|ForTrait(b));
        internal static State Resolve(Cue requested,int leaves,int organs,bool blocked,int harvestCycle,bool otherArchitecture)
        {
            var cues=leaves>0?requested:requested&Cue.Yield;
            if(organs<1||otherArchitecture)cues&=~Cue.Yield;
            return new(cues,blocked&&(cues&Cue.Resource)!=0,harvestCycle>0&&(cues&Cue.Perennial)!=0);
        }
        // Native organ component expansion, not uniform scaling or an organ
        // fabricated from foliage. Only transparent pixels can be filled.
        // The original occupied RGBA is copied verbatim in every frame.
        internal static Pixel[] Fuller(Pixel[] original,LifeSuiteMath.Anatomy a,int frame)
        {
            int w=a.W,h=a.H,W=SculptureMath.Side(w),H=SculptureMath.Side(h),ox=(W-w)/2,oy=(H-h)/2;
            var result=new Pixel[W*H];
            if(a.Articulation?.Organs==null||a.Organs==0)return null;
            float t=Math.Clamp((frame-3)/5f,0f,1f);
            foreach(var organ in a.Articulation.Organs)
            {
                var b=organ.Part;float scale=1+.40f*t;
                if(t<=0)continue;
                int rx=(int)MathF.Ceiling((b.MaxX-b.MinX+1)*scale*.55f)+2;
                int ry=(int)MathF.Ceiling((b.MaxY-b.MinY+1)*scale*.55f)+2;
                for(int y=Math.Max(0,(int)b.Y+oy-ry);y<Math.Min(H,(int)b.Y+oy+ry+1);y++)
                for(int x=Math.Max(0,(int)b.X+ox-rx);x<Math.Min(W,(int)b.X+ox+rx+1);x++)
                {
                    int sx=(int)MathF.Round(b.X+(x-ox-b.X)/scale),sy=(int)MathF.Round(b.Y+(y-oy-b.Y)/scale);
                    if(sx<0||sx>=w||sy<0||sy>=h)continue;
                    int src=sy*w+sx;
                    if(a.Mask[src].A!=128||!organ.Mask[src]||original[src].A<90)continue;
                    int tx=x-ox,ty=y-oy;
                    if(tx>=0&&tx<w&&ty>=0&&ty<h&&original[ty*w+tx].A>0)continue;
                    result[y*W+x]=original[src];
                }
            }
            for(int y=0;y<h;y++)for(int x=0;x<w;x++)
                if(original[y*w+x].A>0)result[(y+oy)*W+x+ox]=original[y*w+x];
            return result;
        }
    }
}
