using System;
using System.Collections.Generic;
using System.Linq;
using Pixel=HorticultureNovelSeeds.OrganSpriteMath.Pixel;

namespace HorticultureNovelSeeds
{
    // Isolated source-art articulation, not whole-sprite deformation.
    // Every frame retains the original alpha footprint; uncertain detached
    // components are never erased. The selected organ is resampled in place.
    internal static class ArticulationMath
    {
        internal const int Frames=9, Grid=3;
        internal static bool Valid(ResponsiveHabitKind kind)=>kind>=ResponsiveHabitKind.DawnPetals&&kind<=ResponsiveHabitKind.SilkFlutter;
        internal static float Phase(ResponsiveHabitKind kind,float day,float sky,float rain,bool sheltered,bool flipped)
        {
            float Unit(float v)=>Math.Clamp(float.IsFinite(v)?v:0f,0f,1f);
            float Smooth(float v)=>v*v*(3f-2f*v);
            float light=Smooth(Unit((sky-.12f)/.6f)),wet=sheltered?0f:Smooth(Unit(rain*2f));
            return kind switch {
                ResponsiveHabitKind.DawnPetals=>light,
                ResponsiveHabitKind.DuskPetals=>1f-light,
                ResponsiveHabitKind.RainPendant=>wet,
                ResponsiveHabitKind.SolarTilt=>.5f+.5f*MathF.Sin((Unit(day)-.5f)*2f*MathF.PI)*light*(flipped?-1f:1f),
                _=>.5f
            };
        }
        internal static int Frame(float phase)=>(int)MathF.Floor(Math.Clamp(float.IsFinite(phase)?phase:0f,0f,1f)*(Frames-1)+.5f);
        internal static int WindFrame(float head)=>Frame(.5f+.5f*MathF.Sin(head*.65f));
        internal sealed record Organ(OrganSpriteMath.Part Part,bool[] Mask);
        internal sealed record Prepared(Pixel[] Original,Pixel[] Base,Organ[] Organs,int W,int H,int Recovered);
        internal static Prepared Prepare(Pixel[] p,int w,int h)
        {
            if(w<16||h<16||p.Length!=w*h)return null;
            var parts=OrganSpriteMath.Parts(p.Select(OrganSpriteMath.Reproductive).ToArray(),w,h,p.Count(c=>c.A>=80),false);
            if(parts.Count==0)return null;
            var leaves=CanopyArchitectureMath.Extract(p,w,h)?.Leaves;
            bool Green(int i)=>p[i].A>=90&&BotanicalDetailMath.Green(p[i])&&!OrganSpriteMath.Reproductive(p[i]);
            var organs=new List<Organ>();var basePixels=(Pixel[])p.Clone();
            var claimed=new bool[p.Length];int recovered=0;
            IEnumerable<int> Adjacent(int i)
            {
                int x=i%w,y=i/w;
                if(x>0)yield return i-1;if(x<w-1)yield return i+1;
                if(y>0)yield return i-w;if(y<h-1)yield return i+w;
            }
            foreach(var part in parts)
            {
                // Conservative segmentation: skip tiny, previously claimed or
                // unattached components. No extrapolation across transparent air.
                if(part.Indices.Length<Math.Max(12,w*h/2500)||part.Indices.Any(i=>claimed[i]))continue;
                var mask=new bool[p.Length];foreach(int i in part.Indices)mask[i]=true;
                foreach(int i in part.Indices)
                    foreach(int j in Adjacent(i))
                    {
                        var c=p[j];var v=p[i];
                        if(!claimed[j]&&c.A>0&&!Green(j)&&
                           Math.Abs(c.R-v.R)+Math.Abs(c.G-v.G)+Math.Abs(c.B-v.B)<135)mask[j]=true;
                    }
                var selected=Enumerable.Range(0,p.Length).Where(i=>mask[i]).ToArray();
                var owner=new int[p.Length];Array.Fill(owner,-1);
                var queue=new Queue<int>();int contacts=0;
                foreach(int i in selected)
                    foreach(int j in Adjacent(i))
                        if(!mask[j]&&Green(j))
                        {contacts++;if(owner[i]<0){owner[i]=j;queue.Enqueue(i);}}
                if(contacts<3)continue;
                while(queue.Count>0)
                {
                    int i=queue.Dequeue();
                    foreach(int j in Adjacent(i))
                        if(mask[j]&&owner[j]<0){owner[j]=owner[i];queue.Enqueue(j);}
                }
                if(selected.Any(i=>owner[i]<0))continue;
                foreach(int i in selected)
                {
                    int x=i%w,y=i/w;var donor=p[owner[i]];
                    if(leaves!=null)
                    {
                        var b=leaves.Box;
                        if(x>=b.X&&x<b.X+b.W&&y>=b.Y&&y<b.Y+b.H)
                        {
                            var v=leaves.Pixels[(y-b.Y)*b.W+x-b.X];
                            if(v.A>=90&&!OrganSpriteMath.Reproductive(v)&&BotanicalDetailMath.Green(v))donor=v;
                        }
                    }
                    basePixels[i]=donor with {A=p[i].A};claimed[i]=true;recovered++;
                }
                organs.Add(new(part,mask));
            }
            return organs.Count==0?null:new(p,basePixels,organs.ToArray(),w,h,recovered);
        }
        internal static Pixel[] Compose(Prepared src,ResponsiveHabitKind kind,int frame)
        {
            int w=src.W,h=src.H;float t=Math.Clamp(frame,0,Frames-1)/(float)(Frames-1);
            bool neutral=(kind==ResponsiveHabitKind.DawnPetals||kind==ResponsiveHabitKind.DuskPetals)?frame==Frames-1:
                kind==ResponsiveHabitKind.RainPendant?frame==0:frame==(Frames-1)/2;
            if(!Valid(kind)||neutral)return (Pixel[])src.Original.Clone();
            var dst=(Pixel[])src.Base.Clone();
            foreach(var organ in src.Organs)
            {
                var p=organ.Part;
                float scaleX=1,scaleY=1,angle=0,dx=0,dy=0;
                float sign=p.X<w*.5f?-1f:1f;
                float cx=p.X,cy=p.Y;
                float size=p.MaxY-p.MinY+1;
                switch(kind)
                {
                    case ResponsiveHabitKind.DawnPetals:
                        scaleX=.48f+.52f*t;scaleY=.80f+.20f*t;cy=p.MinY;break;
                    case ResponsiveHabitKind.DuskPetals:
                        scaleX=.60f+.40f*t;scaleY=.68f+.32f*t;cy=p.MinY;break;
                    case ResponsiveHabitKind.RainPendant:
                        cy=p.MaxY;angle=sign*t*.38f;scaleY=1f+.20f*t;dy=-.15f*size*t;break;
                    case ResponsiveHabitKind.SolarTilt:
                        cy=p.MinY;angle=(t-.5f)*.65f;break;
                    case ResponsiveHabitKind.SilkFlutter:
                        cy=p.MinY;angle=(t-.5f)*.72f*sign;scaleX=1f-.18f*MathF.Abs(2f*t-1f);break;
                }
                float c=MathF.Cos(angle),s=MathF.Sin(angle);
                // Inverse sample only the selected shape. Use a conservative
                // per-component bounding box, and never write outside the quad.
                float radius=MathF.Sqrt((p.MaxX-p.MinX+5)*(p.MaxX-p.MinX+5)+(p.MaxY-p.MinY+5)*(p.MaxY-p.MinY+5))*1.4f;
                int x0=Math.Max(0,(int)MathF.Floor(cx+dx-radius)),x1=Math.Min(w-1,(int)MathF.Ceiling(cx+dx+radius));
                int y0=Math.Max(0,(int)MathF.Floor(cy+dy-radius)),y1=Math.Min(h-1,(int)MathF.Ceiling(cy+dy+radius));
                for(int y=y0;y<=y1;y++)for(int x=x0;x<=x1;x++)
                {
                    float rx=x-cx-dx,ry=y-cy-dy;
                    int sx=(int)MathF.Round(cx+(c*rx+s*ry)/scaleX),sy=(int)MathF.Round(cy+(-s*rx+c*ry)/scaleY);
                    if(sx<0||sx>=w||sy<0||sy>=h)continue;
                    int i=sy*w+sx;
                    if(organ.Mask[i])dst[y*w+x]=OrganSpriteMath.Over(dst[y*w+x],src.Original[i]);
                }
            }
            return dst;
        }
    }
}
