using System;
using System.Linq;
using Pixel=HorticultureNovelSeeds.OrganSpriteMath.Pixel;

namespace HorticultureNovelSeeds
{
    // A single source-art analysis shared by twenty independent visual modes.
    // Mask alpha=255 inferred foliage, 128 reproductive component, 0 unknown.
    // No guessed reproductive regions are substituted for missing components.
    internal static class LifeSuiteMath
    {
        internal const int Modes=20,Frames=9;
        internal static bool Valid(BotanicalDetailKind k)=>Mode(k)>=1&&Mode(k)<=Modes;
        internal static int Mode(BotanicalDetailKind k)=>(int)k-(int)BotanicalDetailKind.PhotosyntheticPulse+1;
        internal static bool AtlasMode(int m)=>m==2||m==11||m==19;
        internal static bool Overlay(int m)=>m==5||m==8||m==10||m==14||m==18;
        internal static bool NeedsOrgan(int m)=>m==5||m==13||m==16;
        internal sealed record Anatomy(Pixel[] Mask,int Leaves,int Organs,int OrganPixels,int W,int H,ArticulationMath.Prepared Articulation,CanopyArchitectureMath.Module Branch);
        internal static Anatomy Analyze(Pixel[] p,int w,int h)
        {
            if(p.Length!=w*h)return null;
            var vascular=VascularMath.Build(p,w,h);if(vascular==null)return null;
            var a=(Pixel[])vascular.Mask.Clone();
            var prep=ArticulationMath.Prepare(p,w,h);
            var organs=prep?.Organs.OrderBy(o=>o.Part.Y).ThenBy(o=>o.Part.X).ToArray()??Array.Empty<ArticulationMath.Organ>();
            int pixels=0;
            for(int j=0;j<organs.Length;j++)
            {
                var organ=organs[j];var part=organ.Part;
                byte rank=(byte)MathF.Round(255f*(j+.5f)/organs.Length);
                float rx=Math.Max(1f,(part.MaxX-part.MinX+1)*.5f),ry=Math.Max(1f,(part.MaxY-part.MinY+1)*.5f);
                for(int i=0;i<p.Length;i++)if(organ.Mask[i]&&p[i].A>=90&&!VascularMath.Eligible(p[i]))
                {
                    float x=(i%w-part.X)/rx,y=(i/w-part.Y)/ry;
                    byte thin=(byte)Math.Clamp((int)MathF.Round(MathF.Sqrt(x*x+y*y)*220f),0,255);
                    if(a[i].A!=128)pixels++;
                    a[i]=new(0,thin,rank,128);
                }
            }
            return new(a,vascular.Territories,organs.Length,pixels,w,h,prep,CanopyArchitectureMath.Extract(p,w,h)?.Leaves);
        }
        internal static Pixel[] Frame(Anatomy a,Pixel[] p,int mode,int frame)
        {
            int w=a.W,h=a.H;float t=Math.Clamp(frame,0,8)/8f;
            if(mode==2&&a.Articulation!=null)return ArticulationMath.Compose(a.Articulation,ResponsiveHabitKind.DawnPetals,frame);
            if(mode==11&&a.Articulation!=null)
            {
                // Independent lag/angle per component. The reconstructed base
                // retains the original alpha wherever organs are moved.
                var d=(Pixel[])a.Articulation.Base.Clone();
                var os=a.Articulation.Organs;
                for(int j=0;j<os.Length;j++)
                {
                    var o=os[j];var b=o.Part;float phase=(t-.5f)*6.283185f;
                    float angle=.40f*MathF.Sin(phase+j*1.65f);
                    float dx=.15f*(b.MaxX-b.MinX+1)*MathF.Sin(phase*.85f+j*.9f);
                    float dy=.08f*(b.MaxY-b.MinY+1)*MathF.Cos(phase+j*1.2f);
                    float c=MathF.Cos(angle),s=MathF.Sin(angle),cx=b.X,cy=b.MinY;
                    int radius=(int)MathF.Ceiling(MathF.Sqrt((b.MaxX-b.MinX+3)*(b.MaxX-b.MinX+3)+(b.MaxY-b.MinY+3)*(b.MaxY-b.MinY+3))*1.3f);
                    for(int y=Math.Max(0,(int)cy-radius);y<=Math.Min(h-1,(int)cy+radius);y++)
                    for(int x=Math.Max(0,(int)cx-radius);x<=Math.Min(w-1,(int)cx+radius);x++)
                    {
                        float px=x-cx-dx,py=y-cy-dy;
                        int xx=(int)MathF.Round(cx+c*px+s*py),yy=(int)MathF.Round(cy-s*px+c*py);
                        if(xx>=0&&xx<w&&yy>=0&&yy<h&&o.Mask[yy*w+xx])
                            d[y*w+x]=OrganSpriteMath.Over(d[y*w+x],p[yy*w+xx]);
                    }
                }
                return d;
            }
            var dst=(Pixel[])p.Clone();
            if(mode!=19||a.Branch==null||frame==0)return dst;
            // Additional foliage develops gradually from native leaf material,
            // not from painting over the original reproductive structures.
            var m=a.Branch;var box=m.Box;
            for(int side=-1;side<=1;side+=2)
            {
                float amount=Math.Clamp(t*1.3f-(side>0?.28f:0f),0,1);
                if(amount<.08f)continue;
                float scale=.50f+.48f*amount;
                float cx=w*(.5f+side*(.11f+.16f*amount)),cy=h*(.37f+(side>0?.15f:0));
                float angle=side*.34f,co=MathF.Cos(angle),si=MathF.Sin(angle);
                for(int y=0;y<h;y++)for(int x=0;x<w;x++)
                {
                    int i=y*w+x;if(p[i].A>=90)continue;
                    float rx=x-cx,ry=y-cy;
                    int sx=(int)MathF.Round((co*rx+si*ry)/scale+(box.W-1)*.5f);
                    int sy=(int)MathF.Round((-si*rx+co*ry)/scale+(box.H-1)*.5f);
                    if(sx<0||sx>=box.W||sy<0||sy>=box.H)continue;
                    var f=m.Pixels[sy*box.W+sx];
                    if(f.A>=100&&VascularMath.Eligible(f))dst[i]=OrganSpriteMath.Over(dst[i],f);
                }
            }
            return dst;
        }
        internal static int Lateral(float l,float r)=>(l>=.5f?1:0)|(r>=.5f?2:0);
        internal static float Stress(int mode,float temperature,float health)
        {
            if(!float.IsFinite(temperature))temperature=20f;
            return mode==17?Math.Clamp((4f-temperature)/12f,0,1):
                mode==3?Math.Max(Math.Clamp((temperature-28f)/16f,0,1),Math.Clamp((1f-health)*1.8f,0,1)):0f;
        }
        internal static bool Active(int mode,float light,float wet,float day,float season,float growth,float stress,int lateral)
        {
            return mode switch
            {
                1=>light>.08f,2=>light<.9f,3=>stress>.08f,4=>wet>.08f||day>.18f&&day<.36f,
                5=>growth>.65f&&light>.08f&&wet<.65f,7=>season>.08f,9=>season>.28f||stress>.2f,
                10=>light<.5f,12=>light>.08f,13=>light>.08f,15=>lateral==1||lateral==2,
                17=>stress>.08f,18=>lateral!=0,19=>growth>.60f,20=>wet>.08f,_=>true
            };
        }
    }
}
