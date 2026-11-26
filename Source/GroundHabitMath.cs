using System;
using System.Linq;
using Pixel=HorticultureNovelSeeds.OrganSpriteMath.Pixel;
namespace HorticultureNovelSeeds
{
    public enum GroundHabitKind {None,Rootflare,RunnerHabit,BasalCarpet,LeafLitter,PetalDrift}
    internal static class GroundHabitMath
    {
        internal const int Size=128;
        internal static bool Valid(GroundHabitKind k)=>k>=GroundHabitKind.Rootflare&&k<=GroundHabitKind.PetalDrift;
        internal readonly record struct Palette(Pixel Leaf,Pixel Petal,bool HasPetal);
        internal static Palette Sample(Pixel[] pixels)
        {
            var samples=pixels.Where((p,i)=>i%Math.Max(1,pixels.Length/12000)==0&&p.A>=150).ToArray();
            Pixel Pick(Pixel[] p,Pixel fallback)=>p.Length==0?fallback:p.OrderBy(c=>c.R*.299f+c.G*.587f+c.B*.114f).ElementAt((int)((p.Length-1)*.62f));
            var leaf=Pick(samples.Where(BotanicalDetailMath.Green).ToArray(),new Pixel(75,125,46,255));
            var petals=samples.Where(p=>OrganSpriteMath.Reproductive(p)&&Math.Max(p.R,Math.Max(p.G,p.B))>125).ToArray();
            return new(leaf,Pick(petals,new Pixel(210,135,62,255)),petals.Length>=8);
        }
        internal static int StableSeed(string s)
        {
            unchecked{uint h=2166136261;foreach(char c in s)h=(h^c)*16777619;return (int)(h&0x7fffffff);}
        }
        internal static Pixel[] Generate(GroundHabitKind kind,Palette palette,int seed)
        {
            var output=new Pixel[Size*Size];
            if(!Valid(kind)||kind==GroundHabitKind.PetalDrift&&!palette.HasPetal)return output;
            var random=new Random(seed);
            float R(float a,float b)=>a+(b-a)*(float)random.NextDouble();
            Pixel Shade(Pixel c,float f,byte alpha=255)
            {
                byte C(byte v)=>(byte)Math.Clamp((int)MathF.Round(v*f),0,255);
                return new(C(c.R),C(c.G),C(c.B),alpha);
            }
            Pixel Blend(Pixel a,Pixel b,float t)
            {
                byte C(byte x,byte y)=>(byte)Math.Clamp((int)MathF.Round(x+(y-x)*t),0,255);
                return new(C(a.R,b.R),C(a.G,b.G),C(a.B,b.B),255);
            }
            var leaf=Blend(palette.Leaf,new Pixel(83,138,47,255),.18f);
            var root=Blend(new Pixel(126,89,53,255),leaf,.13f);
            float X(int x)=>(x+.5f-Size*.5f)/(Size*.5f);
            void Put(int x,int y,Pixel c)
            {
                if(x<0||y<0||x>=Size||y>=Size)return;
                if(X(x)*X(x)+X(y)*X(y)>.94f*.94f)return;
                int i=y*Size+x;output[i]=OrganSpriteMath.Over(output[i],c);
            }
            void Disk(float x,float y,float r,Pixel c)
            {
                int a=Math.Max(0,(int)((x-r+1)*Size*.5f)),b=Math.Min(Size-1,(int)((x+r+1)*Size*.5f));
                int d=Math.Max(0,(int)((y-r+1)*Size*.5f)),e=Math.Min(Size-1,(int)((y+r+1)*Size*.5f));
                for(int yy=d;yy<=e;yy++)for(int xx=a;xx<=b;xx++)
                {
                    float dx=X(xx)-x,dy=X(yy)-y,l=MathF.Sqrt(dx*dx+dy*dy);
                    if(l>r)continue;
                    Put(xx,yy,Shade(c,1f,(byte)Math.Clamp((int)((r-l)*Size*.5f*255),0,255)));
                }
            }
            void Stroke(float ax,float ay,float bx,float by,float r0,float r1,Pixel c)
            {
                float dx=bx-ax,dy=by-ay,len=MathF.Sqrt(dx*dx+dy*dy);
                int n=Math.Max(1,(int)MathF.Ceiling(len*Size));
                for(int j=0;j<=n;j++)
                {
                    float t=j/(float)n,r=r0+(r1-r0)*t;
                    Disk(ax+dx*t,ay+dy*t,r,Shade(c,.77f));
                    Disk(ax+dx*t-r*.10f,ay+dy*t+r*.16f,Math.Max(.008f,r*.60f),Shade(c,1.13f));
                }
            }
            // A pointed, curved lamina with a muted edge and central vein.
            void Leaf(float ax,float ay,float angle,float length,float width,Pixel color,float curve=0f)
            {
                float dx=MathF.Cos(angle),dy=MathF.Sin(angle),nx=-dy,ny=dx;
                for(int y=0;y<Size;y++)for(int x=0;x<Size;x++)
                {
                    float px=X(x)-ax,py=X(y)-ay,t=(px*dx+py*dy)/length;
                    if(t<=0f||t>=1f)continue;
                    float q=px*nx+py*ny-curve*MathF.Sin(MathF.PI*t);
                    float half=width*MathF.Pow(MathF.Sin(MathF.PI*t),.72f);
                    float edge=half-Math.Abs(q);
                    if(edge<=0f)continue;
                    float side=q/Math.Max(.004f,half);
                    float shade=.84f+.17f*(1f-Math.Abs(side))+.08f*side;
                    if(Math.Abs(q)<.013f&&t>.08f&&t<.91f)shade+=.20f;
                    if(edge<.015f)shade*=.82f;
                    Put(x,y,Shade(color,shade,(byte)Math.Clamp((int)(edge*Size*.5f*255),0,255)));
                }
            }
            float start=R(0f,MathF.PI*2f);
            if(kind==GroundHabitKind.Rootflare||kind==GroundHabitKind.RunnerHabit)
            {
                int n=kind==GroundHabitKind.Rootflare?5:3;
                for(int j=0;j<n;j++)
                {
                    float angle=start+j*MathF.PI*2f/n+R(-.28f,.28f),length=R(.55f,.87f);
                    float ax=0,ay=0;
                    for(int k=1;k<=4;k++)
                    {
                        float t=k/4f,a=angle+MathF.Sin(t*3f+j)*.24f;
                        float bx=MathF.Cos(a)*length*t,by=MathF.Sin(a)*length*t;
                        float radius=kind==GroundHabitKind.Rootflare?.066f*(1f-.78f*(k-1)/4f):.029f*(1f-.45f*(k-1)/4f);
                        Stroke(ax,ay,bx,by,radius,radius*.69f,kind==GroundHabitKind.Rootflare?root:leaf);
                        if(kind==GroundHabitKind.Rootflare&&k>=2)
                        {
                            float side=a+(k%2==0?-.87f:.82f),reach=R(.12f,.24f);
                            Stroke(bx,by,bx+MathF.Cos(side)*reach,by+MathF.Sin(side)*reach,radius*.67f,.012f,root);
                        }
                        if(kind==GroundHabitKind.RunnerHabit&&k>=2)
                        {
                            Leaf(bx,by,a+.92f,R(.16f,.24f),.063f,Shade(leaf,R(.92f,1.13f)),.015f);
                            Leaf(bx,by,a-.88f,R(.14f,.23f),.059f,Shade(leaf,R(.95f,1.14f)),-.018f);
                        }
                        ax=bx;ay=by;
                    }
                    if(kind==GroundHabitKind.RunnerHabit)
                        for(int k=0;k<3;k++)Leaf(ax,ay,angle+k*2.1f,.19f,.073f,leaf,.01f);
                }
            }
            else if(kind==GroundHabitKind.BasalCarpet)
            {
                for(int j=0;j<9;j++)
                {
                    float a=start+j*2f*MathF.PI/9+R(-.19f,.19f),l=R(.52f,.83f);
                    Leaf(.05f*MathF.Cos(a),.05f*MathF.Sin(a),a,l,R(.12f,.18f),Shade(leaf,R(.80f,1.16f)),R(-.045f,.045f));
                }
                for(int j=0;j<4;j++)Leaf(0,0,start+j*1.63f+.5f,R(.35f,.51f),.11f,Shade(leaf,1.08f),.022f);
            }
            else if(kind==GroundHabitKind.LeafLitter)
            {
                for(int j=0;j<8;j++)
                {
                    float a=start+j*.87f+R(-.24f,.24f),r=R(.39f,.75f);
                    var c=Blend(leaf,new Pixel(158,99,49,255),R(.43f,.87f));
                    Leaf(MathF.Cos(a)*r,MathF.Sin(a)*r,R(0f,6.28f),R(.20f,.34f),R(.067f,.104f),c,R(-.02f,.02f));
                }
            }
            else
            {
                // Irregular loose groups, not a circular halo. Petals take
                // the original reproductive palette; no particle simulation.
                for(int j=0;j<11;j++)
                {
                    float a=start+j*.69f+R(-.5f,.5f),r=R(.38f,.80f);
                    var c=Shade(palette.Petal,R(.79f,1.07f));
                    Leaf(MathF.Cos(a)*r,MathF.Sin(a)*r,R(0f,6.28f),R(.14f,.23f),R(.051f,.078f),c,.006f);
                }
            }
            return output;
        }
    }
}
