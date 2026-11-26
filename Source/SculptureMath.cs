using System;
using System.Linq;
using Pixel=HorticultureNovelSeeds.OrganSpriteMath.Pixel;

namespace HorticultureNovelSeeds
{
    // A parametric, rooted secondary branch rig. The source sprite is an
    // immutable foreground; every new curve and organ is generated behind it.
    // The palette and reproductive stamps are sampled from actual species art.
    internal static class SculptureMath
    {
        internal const float Expansion=1.5f;
        internal static bool Valid(BotanicalDetailKind k)=>k>=BotanicalDetailKind.CircinateFrond&&k<=BotanicalDetailKind.CrestedFan;
        internal static int Mode(BotanicalDetailKind k)=>21+(int)k-(int)BotanicalDetailKind.CircinateFrond;
        internal static int Side(int n)=>n+n/2;
        private readonly record struct P(float X,float Y)
        {
            public static P operator +(P a,P b)=>new(a.X+b.X,a.Y+b.Y);
            public static P operator -(P a,P b)=>new(a.X-b.X,a.Y-b.Y);
            public static P operator *(P a,float k)=>new(a.X*k,a.Y*k);
        }
        private static float Lum(Pixel p)=>.299f*p.R+.587f*p.G+.114f*p.B;
        private static float Unit(float f)=>Math.Clamp(f,0,1);
        private static P Mix(P a,P b,float t)=>a+(b-a)*t;
        internal static Pixel[] Frame(Pixel[] original,int w,int h,LifeSuiteMath.Anatomy a,int mode,int frame)
        {
            int W=Side(w),H=Side(h),ox=(W-w)/2,oy=(H-h)/2;
            var outp=new Pixel[W*H];
            var green=original.Where(VascularMath.Eligible).OrderBy(Lum).ToArray();
            var body=Enumerable.Range(0,original.Length).Where(i=>original[i].A>=90).ToArray();
            if(green.Length<20||body.Length<20)return null;
            float minX=body.Min(i=>i%w)+ox,maxX=body.Max(i=>i%w)+ox;
            float minY=body.Min(i=>i/w)+oy,maxY=body.Max(i=>i/w)+oy;
            float cx=(minX+maxX)*.5f,cy=minY+(maxY-minY)*.58f;
            float span=Math.Max(12,maxX-minX),height=Math.Max(16,maxY-minY);
            float unit=Math.Min(w,h)/64f;
            float t=.16f+.84f*Math.Clamp(frame,0,8)/8f;
            float smooth=t*t*(3-2*t);
            Pixel Color(float value,float alpha=1)
            {
                var c=green[(int)MathF.Round(Unit(value)*(green.Length-1))];
                return c with {A=(byte)MathF.Round(Unit(alpha)*255)};
            }
            void Put(int x,int y,Pixel c)
            {
                if(x<1||x>=W-1||y<1||y>=H-1||c.A==0)return;
                int i=y*W+x;outp[i]=OrganSpriteMath.Over(outp[i],c);
            }
            void Dot(P p,float radius,float shade)
            {
                for(int y=(int)MathF.Floor(p.Y-radius-1);y<=p.Y+radius+1;y++)
                for(int x=(int)MathF.Floor(p.X-radius-1);x<=p.X+radius+1;x++)
                {
                    float d=MathF.Sqrt((x-p.X)*(x-p.X)+(y-p.Y)*(y-p.Y));
                    float alpha=Unit(radius+.60f-d);
                    if(alpha>0)Put(x,y,Color(shade+.14f*Unit((p.X-x)/(radius+.1f)),alpha));
                }
            }
            void Line(P a0,P b0,float ra,float rb)
            {
                float distance=MathF.Sqrt((a0.X-b0.X)*(a0.X-b0.X)+(a0.Y-b0.Y)*(a0.Y-b0.Y));
                int steps=Math.Max(1,(int)MathF.Ceiling(distance*2));
                for(int i=0;i<=steps;i++)
                {float q=i/(float)steps;Dot(Mix(a0,b0,q),ra+(rb-ra)*q,.28f+.12f*q);}
            }
            P Curve(P a0,P b0,P c0,float q)=>Mix(Mix(a0,b0,q),Mix(b0,c0,q),q);
            void Stem(P a0,P b0,P c0,float thickness)
            {
                P last=a0;for(int j=1;j<=24;j++)
                {float q=j/24f;P next=Curve(a0,b0,c0,q);Line(last,next,thickness*(1-.45f*q),thickness*(1-.45f*q));last=next;}
            }
            // A tapered leaf blade with an asymmetric midrib and a palette
            // sampled from the source. Each blade starts at its petiole.
            void Leaf(P root,P tip,float width,float shade)
            {
                P delta=tip-root;float len=MathF.Sqrt(delta.X*delta.X+delta.Y*delta.Y);if(len<2)return;
                P axis=delta*(1/len),normal=new(-axis.Y,axis.X);
                int x0=(int)MathF.Floor(Math.Min(root.X,tip.X)-width-1),x1=(int)MathF.Ceiling(Math.Max(root.X,tip.X)+width+1);
                int y0=(int)MathF.Floor(Math.Min(root.Y,tip.Y)-width-1),y1=(int)MathF.Ceiling(Math.Max(root.Y,tip.Y)+width+1);
                for(int y=y0;y<=y1;y++)for(int x=x0;x<=x1;x++)
                {
                    P p=new(x-root.X,y-root.Y);
                    float q=(p.X*axis.X+p.Y*axis.Y)/len;if(q<0||q>1)continue;
                    float v=p.X*normal.X+p.Y*normal.Y;
                    float belly=width*MathF.Pow(MathF.Sin(q*MathF.PI),.80f)*(.72f+.28f*q);
                    float bend=.16f*width*MathF.Sin(q*MathF.PI);
                    float edge=belly-MathF.Abs(v-bend);if(edge<-.45f)continue;
                    float rib=1-Unit(MathF.Abs(v-bend)/Math.Max(.6f,unit));
                    float tone=Unit(shade+.19f*MathF.Cos(q*2.6f)+.19f*Unit(v/Math.Max(.5f,width))+.16f*rib);
                    Put(x,y,Color(tone,Unit(edge+.6f)));
                }
            }
            var organ=a.Articulation?.Organs.OrderByDescending(o=>o.Part.Indices.Length).FirstOrDefault();
            void Organ(P at,float length,float angle)
            {
                if(organ==null){Leaf(at,at+new P(0,length),length*.24f,.64f);return;}
                var p=organ.Part;float bw=p.MaxX-p.MinX+1,bh=p.MaxY-p.MinY+1;
                float scale=Math.Max(.1f,length/Math.Max(bw,bh));
                float co=MathF.Cos(angle),si=MathF.Sin(angle),radius=MathF.Sqrt(bw*bw+bh*bh)*scale*.64f+1;
                for(int y=(int)MathF.Floor(at.Y-radius);y<=at.Y+radius;y++)
                for(int x=(int)MathF.Floor(at.X-radius);x<=at.X+radius;x++)
                {
                    float dx=(x-at.X)/scale,dy=(y-at.Y)/scale;
                    int sx=(int)MathF.Round(p.X+co*dx+si*dy),sy=(int)MathF.Round(p.Y-si*dx+co*dy);
                    if(sx>=0&&sx<w&&sy>=0&&sy<h&&organ.Mask[sy*w+sx])Put(x,y,original[sy*w+sx]);
                }
            }
            P anchor=new(cx,cy);
            // Rooted secondary structures: all additions are connected to
            // original foliage, not arbitrary floating sprite ornaments.
            if(mode==21)
            {
                // Circinate vernation: two fern-like fronds unroll from
                // tight fiddleheads into broad, pinnate arches.
                for(int side=-1;side<=1;side+=2)
                {
                    P start=new(cx+side*span*.09f,cy-height*.13f);
                    P last=start;float reach=span*(.19f+.52f*smooth);
                    int count=24;
                    for(int j=1;j<=count;j++)
                    {
                        float q=j/(float)count;
                        float curl=(1-smooth)*q*q*5.6f;
                        P next=start+new P(side*(reach*q+.13f*span*MathF.Sin(curl)),height*(.62f*q-.24f*smooth*q*q)+.10f*height*(1-MathF.Cos(curl)));
                        Line(last,next,Math.Max(.7f,unit*(1.55f-.8f*q)),Math.Max(.6f,unit*(1.35f-.8f*q)));
                        if(j%4==0&&j<23)
                        {
                            float len=span*(.10f+.16f*smooth)*MathF.Sin(q*MathF.PI);
                            Leaf(next,next+new P(side*len*.28f,len),len*.23f,.51f);
                            Leaf(next,next+new P(side*len*.70f,-len*.43f),len*.22f,.42f);
                        }
                        last=next;
                    }
                    Dot(last,unit*1.15f,.68f);
                }
            }
            else if(mode==22)
            {
                // Helical climbing growth winds around the existing central
                // stem, with alternating side-facing leaf blades.
                float tall=height*(.30f+.68f*smooth);float radius=span*(.25f+.30f*smooth);
                P start=new(cx+span*.22f,minY+height*.23f),last=start;
                for(int j=1;j<=48;j++)
                {
                    float q=j/48f,angle=q*(3.8f*MathF.PI);
                    P next=start+new P(span*.29f*smooth*q+radius*MathF.Sin(angle)*MathF.Sin(q*MathF.PI*.75f),tall*q);
                    Line(last,next,unit*1.25f,unit*.85f);
                    if(j%8==0)
                    {
                        float side=MathF.Cos(angle)>0?1:-1,len=span*(.19f+.13f*smooth)*(1-.40f*q);
                        Leaf(next,next+new P(side*len,len*.50f),len*.29f,.56f);
                    }
                    last=next;
                }
                Leaf(last,last+new P(0,height*.17f),span*.075f,.70f);
            }
            else if(mode==23)
            {
                // A true umbel-like fan of individual pedicels and terminal
                // reproductive stamps, each emerging from the same junction.
                P junction=new(cx,cy+height*.12f);
                for(int j=0;j<7;j++)
                {
                    float q=(j-3)/3f,spread=span*(.30f+.39f*smooth);
                    P end=junction+new P(q*spread,height*(.20f+.28f*smooth)*(1-.30f*q*q));
                    Stem(junction,Mix(junction,end,.43f)+new P(q*span*.08f,height*.05f),end,unit*1.1f);
                    float size=span*(.10f+.115f*smooth)*(1-.14f*MathF.Abs(q));
                    Organ(end,size,-q*.40f);
                }
            }
            else if(mode==24)
            {
                // Pendant racemes: articulated stems carry source-derived
                // reproductive clusters in progressively hanging chains.
                for(int side=-1;side<=1;side+=2)
                {
                    P start=anchor;
                    P elbow=new(cx+side*span*(.38f+.27f*smooth),cy+height*.29f);
                    P end=new(elbow.X+side*span*.06f,cy-height*(.06f+.37f*smooth));
                    Stem(start,elbow,end,unit*1.35f);
                    for(int j=1;j<=4;j++)
                    {
                        float q=.26f+.17f*j;
                        P n=Curve(start,elbow,end,q);
                        P ped=n+new P(side*span*.09f,-height*(.035f+.045f*smooth));
                        Line(n,ped,unit*.95f,unit*.65f);
                        Organ(ped+new P(0,-height*.035f),span*(.105f+.11f*smooth)*(1-.08f*j),side*.23f);
                    }
                    Leaf(Mix(start,elbow,.45f),Mix(start,elbow,.45f)+new P(side*span*.20f,height*.13f),span*.055f,.48f);
                }
            }
            else if(mode==25)
            {
                // Fasciation-inspired crested fan, built from a continuous
                // curved rib and radiating leaf blades rather than scaling
                // the original texture horizontally.
                P basePoint=new(cx,cy-height*.04f);
                P prev=basePoint;
                for(int j=0;j<=12;j++)
                {
                    float q=(j-6)/6f,spread=span*(.25f+.48f*smooth);
                    P crest=new(cx+q*spread,cy+height*(.24f+.20f*smooth)*MathF.Cos(q*1.12f));
                    if(j>0)Line(prev,crest,unit*1.35f,unit*1.35f);
                    if(j%2==0)Stem(basePoint,Mix(basePoint,crest,.5f),crest,unit*1.25f);
                    float len=height*(.10f+.18f*smooth)*(1-.24f*MathF.Abs(q));
                    Leaf(crest,crest+new P(q*len*.65f,len),len*.30f,.57f+.10f*(j%2));
                    if(j%3==0)Organ(crest+new P(0,len*.52f),span*(.08f+.065f*smooth),-q*.3f);
                    prev=crest;
                }
            }
            // Immutable exact original color and alpha in all occupied
            // pixels. No masking of flowers; no occlusion reconstruction.
            for(int y=0;y<h;y++)for(int x=0;x<w;x++)
            {int i=y*w+x;if(original[i].A>0)outp[(y+oy)*W+x+ox]=original[i];}
            return outp;
        }
    }
}
