using System;
using System.Linq;
using System.Collections.Generic;
using Pixel=HorticultureNovelSeeds.OrganSpriteMath.Pixel;

namespace HorticultureNovelSeeds
{
    // Each allele defines an organ-level developmental vocabulary, not one
    // whole-plant pose. A stable instance seed selects one of eight mosaics.
    // Frame zero is reserved for the exact native source / orientation probe.
    internal static class MosaicMath
    {
        internal static bool Valid(ResponsiveHabitKind k)=>k>=ResponsiveHabitKind.SuccessionBloom&&k<=ResponsiveHabitKind.RipeningMosaic;
        internal static int Frame(int payload)=>1+Math.Abs(payload%8);
        internal enum OrganState { Native, Bud, Sport, Stout, Slender, Daughter, Unripe, Senescent }
        internal static OrganState State(ResponsiveHabitKind kind,int frame,int count,int index)
        {
            if(!Valid(kind)||frame<=0||count<2)return OrganState.Native;
            int shift=(frame-1)%count;
            int rank=(index+count-shift)%count;
            // One exact native organ is always reserved; no uniform plant-wide effect.
            if(rank==0)return OrganState.Native;
            return kind switch
            {
                ResponsiveHabitKind.SuccessionBloom=>rank%3==1?OrganState.Bud:rank%3==2&&count>=4?OrganState.Stout:OrganState.Native,
                ResponsiveHabitKind.SectorSport=>rank==1||count>=7&&rank==2?OrganState.Sport:OrganState.Native,
                ResponsiveHabitKind.BiformHarvest=>rank%3==1?OrganState.Stout:rank%3==2?OrganState.Slender:OrganState.Native,
                ResponsiveHabitKind.SisterBuds=>rank%3==1?OrganState.Daughter:OrganState.Native,
                ResponsiveHabitKind.RipeningMosaic=>rank%3==1?OrganState.Unripe:rank%3==2?OrganState.Senescent:OrganState.Native,
                _=>OrganState.Native
            };
        }
        private static Pixel Shade(Pixel p,OrganState state,int frame)
        {
            float lum=(Math.Max(p.R,Math.Max(p.G,p.B))*.78f+(p.R+p.G+p.B)/3f*.22f)/255f;
            (float r,float g,float b,float mix) target=state switch
            {
                OrganState.Bud=>(.68f,.81f,.28f,.56f),
                OrganState.Sport=>(.98f,.36f+(frame%2)*.10f,.50f,.82f),
                OrganState.Daughter=>(.76f,.88f,.38f,.52f),
                OrganState.Unripe=>(.49f,.84f,.25f,.78f),
                OrganState.Senescent=>(.88f,.47f,.20f,.73f),
                _=>(0f,0f,0f,0f)
            };
            if(target.mix<=0f)return p;
            byte C(byte source,float t)=>(byte)Math.Clamp((int)MathF.Round(source*(1f-target.mix)+255f*lum*t*target.mix),0,255);
            return new Pixel(C(p.R,target.r),C(p.G,target.g),C(p.B,target.b),p.A);
        }
        internal static Pixel[] Compose(ArticulationMath.Prepared src,ResponsiveHabitKind kind,int frame,out int changedOrgans)
        {
            var dst=(Pixel[])src.Original.Clone();
            changedOrgans=0;
            int w=src.W,h=src.H;
            if(!Valid(kind)||frame<=0||src.Organs.Length<2)return dst;
            // Adjacent position order gives a coherent lineage sector rather than
            // an arbitrary pixel mask. Rotation changes which organs express it.
            var organs=src.Organs.OrderBy(p=>MathF.Atan2(p.Part.Y-h*.5f,p.Part.X-w*.5f)).ThenBy(p=>p.Part.X).ThenBy(p=>p.Part.Y).ToArray();
            var owners=new int[dst.Length];Array.Fill(owners,-1);
            for(int n=0;n<organs.Length;n++)
                for(int i=0;i<owners.Length;i++)if(organs[n].Mask[i]&&owners[i]<0)owners[i]=n;
            for(int n=0;n<organs.Length;n++)
            {
                var state=State(kind,frame,organs.Length,n);
                if(state==OrganState.Native)continue;
                var organ=organs[n];var p=organ.Part;
                float sx=1f,sy=1f,dx=0f,dy=0f;
                float bw=p.MaxX-p.MinX+1,bh=p.MaxY-p.MinY+1;
                float sign=(p.X<w*.5f?-1f:1f);
                switch(state)
                {
                    case OrganState.Bud:sx=.49f;sy=.66f;dy=-.13f*bh;break;
                    case OrganState.Sport:sx=1.13f;sy=1.10f;break;
                    case OrganState.Stout:sx=1.48f;sy=.96f;break;
                    case OrganState.Slender:sx=.72f;sy=1.41f;break;
                    case OrganState.Daughter:sx=.54f;sy=.62f;dx=sign*bw*.48f;dy=bh*.26f;break;
                    case OrganState.Unripe:sx=.82f;sy=.90f;break;
                    case OrganState.Senescent:sx=1.08f;sy=.95f;break;
                }
                // Reconstruct only the organ being changed. Never erase any
                // native organ or the original plant's alpha coverage.
                if(state!=OrganState.Daughter)
                    for(int i=0;i<owners.Length;i++)if(owners[i]==n)dst[i]=src.Base[i];
                int x0=Math.Max(0,(int)MathF.Floor(p.X+dx+(p.MinX-2-p.X)*sx));
                int x1=Math.Min(w-1,(int)MathF.Ceiling(p.X+dx+(p.MaxX+2-p.X)*sx));
                int y0=Math.Max(0,(int)MathF.Floor(p.Y+dy+(p.MinY-2-p.Y)*sy));
                int y1=Math.Min(h-1,(int)MathF.Ceiling(p.Y+dy+(p.MaxY+2-p.Y)*sy));
                bool changed=false;
                for(int y=y0;y<=y1;y++)for(int x=x0;x<=x1;x++)
                {
                    int k=y*w+x;
                    if(owners[k]>=0&&owners[k]!=n)continue;
                    int xx=(int)MathF.Round(p.X+(x-p.X-dx)/sx),yy=(int)MathF.Round(p.Y+(y-p.Y-dy)/sy);
                    if(xx<0||xx>=w||yy<0||yy>=h)continue;
                    int i=yy*w+xx;
                    if(!organ.Mask[i])continue;
                    var next=OrganSpriteMath.Over(dst[k],Shade(src.Original[i],state,frame));
                    changed|=next!=dst[k];dst[k]=next;
                }
                if(changed)changedOrgans++;
            }
            return dst;
        }
    }
}
