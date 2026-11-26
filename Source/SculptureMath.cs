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
        internal static bool ForgeValid(BotanicalDetailKind k)=>k>=BotanicalDetailKind.BanyanProps&&k<=BotanicalDetailKind.SelfShadowLace;
        internal static int ForgeMode(BotanicalDetailKind k)=>26+(int)k-(int)BotanicalDetailKind.BanyanProps;
        internal static bool ForgeTint(int mode)=>mode is 41 or 45;
        internal static bool ForgeTintAllowed(int mode,LifeSuiteMath.Anatomy a,Pixel p,int i)=>ForgeTint(mode)&&a.Mask[i].A==255&&VascularMath.Eligible(p);
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
            else if(mode>=26&&mode<=45)
            {
                // Botanical Forge: independent recipes on the same source-aware
                // drawing rig. Coordinates remain normalized to plant bounds.
                float f=Math.Clamp(frame,0,8)/8f, z=.18f+.82f*smooth;
                P Clamp(P p,float margin=3)=>new(Math.Clamp(p.X,margin,W-1-margin),Math.Clamp(p.Y,margin,H-1-margin));
                float Space(int s)=>s<0?cx-3:W-4-cx;
                P At(int s,float x,float y)=>Clamp(new(cx+s*Math.Min(span*x,Space(s)*.90f),minY+height*y));
                Pixel Tint(Pixel c,float r,float g,float b,float amount=1)
                {
                    byte C(byte v,float m)=>(byte)Math.Clamp((int)MathF.Round(v*(1-amount+amount*m)),0,255);
                    return new(C(c.R,r),C(c.G,g),C(c.B,b),c.A);
                }
                void Paint(P at,float rx,float ry,Func<float,float,Pixel> color)
                {
                    if(rx<.25f||ry<.25f)return;
                    for(int y=(int)MathF.Floor(at.Y-ry-1);y<=at.Y+ry+1;y++)
                    for(int x=(int)MathF.Floor(at.X-rx-1);x<=at.X+rx+1;x++)
                    {
                        float px=(x-at.X)/rx,py=(y-at.Y)/ry,edge=1-MathF.Sqrt(px*px+py*py);
                        if(edge<-.08f)continue;
                        var c=color(px,py);Put(x,y,c with{A=(byte)MathF.Round(c.A*Unit(edge*Math.Min(rx,ry)+.55f))});
                    }
                }
                void Seed(P at,float radius,float shade)
                {
                    Paint(at,radius*.70f,radius,(x,y)=>new Pixel((byte)(98+55*shade+25*Unit(-x)),(byte)(69+42*shade+17*Unit(-x)),(byte)(32+29*shade),255));
                }
                void Cup(P root,float length,float width,int side)
                {
                    P end=Clamp(root+new P(side*length*.08f,length),width+2);
                    Stem(root,root+new P(side*length*.08f,length*.30f),end,unit*1.0f);
                    // Flared tubular lamina, shaded inner lip and asymmetric
                    // rim. Draw a cup rather than a scaled leaf silhouette.
                    float x0=end.X,y0=end.Y;
                    Paint(new P(x0,y0-length*.36f),width*.72f,length*.42f,(x,y)=>Color(.30f+.34f*Unit(-x)+.12f*Unit(y)));
                    Paint(end,width,width*.37f,(x,y)=>Color(.58f+.19f*Unit(-x)));
                    Paint(end+new P(0,width*.05f),width*.70f,width*.23f,(x,y)=>Tint(Color(.25f+.10f*Unit(-x)),.66f,.54f,.53f));
                    Paint(end+new P(side*width*.15f,width*.48f),width*.48f,width*.18f,(x,y)=>Color(.72f+.11f*Unit(-x)));
                }
                void StampPlant(P at,float scale)
                {
                    int rw=(int)MathF.Ceiling(w*scale),rh=(int)MathF.Ceiling(h*scale);
                    for(int y=0;y<rh;y++)for(int x=0;x<rw;x++)
                    {
                        int sx=Math.Clamp((int)(x/scale),0,w-1),sy=Math.Clamp((int)(y/scale),0,h-1);
                        var c=original[sy*w+sx];if(c.A==0)continue;
                        Put((int)MathF.Round(at.X+(x-rw*.5f)),(int)MathF.Round(at.Y+y-(minY-oy)*scale),c);
                    }
                }
                if(mode==26)
                {
                    // Adventitious aerial support roots drop from two
                    // load-bearing branches and fork near the soil.
                    for(int side=-1;side<=1;side+=2)
                    {
                        P start=At(side,.08f,.53f),elbow=At(side,.60f,.73f),end=At(side,.73f,.67f);
                        Stem(start,elbow,end,unit*1.45f);
                        for(int j=0;j<3;j++)
                        {
                            P origin=Curve(start,elbow,end,.45f+j*.22f);
                            float reach=height*(.10f+.58f*z)*(1-.08f*j);
                            P foot=Clamp(origin+new P(side*span*.035f*(j+1),-reach),3);
                            Stem(origin,Mix(origin,foot,.46f)+new P(side*span*.035f,0),foot,unit*(1.05f-.12f*j));
                            if(f>.45f)Line(foot,Clamp(foot+new P(side*span*.08f,-unit*2)),unit*.55f,unit*.30f);
                        }
                        Leaf(end,Clamp(end+new P(side*span*.11f,height*.16f)),span*.045f,.59f);
                    }
                }
                else if(mode==27)
                {
                    // Candelabra habit: sequential upright apices emerge
                    // from a pair of sweeping lower arms, not a top fan.
                    for(int side=-1;side<=1;side+=2)
                    {
                        P start=At(side,.05f,.35f),elbow=At(side,.40f,.38f),end=At(side,.69f,.47f);
                        Stem(start,elbow,end,unit*1.45f);
                        for(int j=0;j<3;j++)
                        {
                            P n=Curve(start,elbow,end,.42f+j*.27f);
                            float rise=height*(.14f+.36f*z)*(1-.18f*j);
                            P tip=Clamp(n+new P(side*span*.035f,rise),5);
                            Stem(n,n+new P(side*span*.04f,rise*.55f),tip,unit*1.05f);
                            Leaf(Mix(n,tip,.60f),Clamp(Mix(n,tip,.60f)+new P(side*span*.12f,height*.08f)),span*.045f,.58f);
                            Organ(tip,Math.Max(3,span*(.09f+.065f*z)),0);
                        }
                    }
                }
                else if(mode==28)
                {
                    // Low creeping stolons, ending in miniature daughters
                    // made from the original species rather than new art.
                    for(int side=-1;side<=1;side+=2)
                    {
                        P start=At(side,.04f,.13f),end=At(side,.37f+.38f*z,.07f+side*.025f);
                        Stem(start,At(side,.34f,.02f),end,unit*1.10f);
                        float sc=.14f+.26f*z*(side>0?.83f:1);
                        StampPlant(end,sc);
                        P node=Mix(start,end,.53f);
                        Leaf(node,Clamp(node+new P(side*span*.10f,height*.09f)),span*.033f,.55f);
                    }
                }
                else if(mode==29)
                {
                    // Braid two leaders with opposite crossing phase.
                    // Draw each segment in alternating over/under order.
                    P root=At(1,.24f,.19f);
                    float length=height*(.20f+.70f*z);
                    float drift=span*.37f*z,amp=span*(.055f+.075f*z);
                    P Path(int j,float q)=>Clamp(root+new P(drift*q+amp*MathF.Sin(q*10.0f+j*MathF.PI)*MathF.Sin(q*MathF.PI*.9f),length*q),4);
                    for(int k=1;k<=36;k++)
                    {
                        float q=k/36f,prev=(k-1)/36f;
                        int front=MathF.Sin(q*10)>0?1:0;
                        for(int layer=0;layer<2;layer++)
                        {
                            int j=layer==1?front:1-front;
                            P n=Path(j,q),a0=Path(j,prev);
                            Line(a0,n,unit*(layer==1?1.25f:.94f),unit*.92f);
                            if(k%9==0&&layer==1)
                                Leaf(n,Clamp(n+new P((j==0?-1:1)*span*.18f,height*.10f)),span*.045f,.56f);
                        }
                    }
                    for(int j=0;j<2;j++)Organ(Path(j,1),Math.Max(3,span*.12f*z),0);
                }
                else if(mode==30)
                {
                    // Winged petioles: paired lateral expansions along
                    // each petiole, with a long terminal blade.
                    for(int side=-1;side<=1;side+=2)
                    {
                        P a0=At(side,.08f,.36f),end=At(side,.42f+.33f*z,.42f+side*.12f);
                        Stem(a0,At(side,.36f,.50f),end,unit*1.0f);
                        for(int j=1;j<=4;j++)
                        {
                            P n=Curve(a0,At(side,.36f,.50f),end,j/5f);
                            float len=span*(.09f+.12f*z);
                            Leaf(n,Clamp(n+new P(side*len*.50f,len*.85f)),len*.30f,.60f);
                            Leaf(n,Clamp(n+new P(side*len*.50f,-len*.70f)),len*.28f,.42f);
                        }
                        Leaf(end,Clamp(end+new P(side*span*.15f,height*.15f)),span*.075f,.57f);
                    }
                }
                else if(mode==31)
                {
                    // Pitcher-like leaves with shaded interiors and lips.
                    for(int side=-1;side<=1;side+=2)
                    {
                        P a0=At(side,.13f,.39f),tip=At(side,.52f+.16f*z,.36f+side*.10f);
                        Stem(a0,At(side,.42f,.57f),tip,unit*1.05f);
                        float len=height*(.13f+.25f*z);
                        Cup(tip,len,span*(.065f+.07f*z),side);
                    }
                }
                else if(mode==32)
                {
                    // Hooded spathes: curved leaf hoods separate as the
                    // source-derived reproductive center emerges.
                    for(int side=-1;side<=1;side+=2)
                    {
                        P a0=At(side,.09f,.54f),node=At(side,.51f+.11f*z,.72f+side*.06f);
                        Stem(a0,At(side,.32f,.67f),node,unit*1.15f);
                        float len=height*(.16f+.16f*z),wide=span*(.055f+.045f*z);
                        Leaf(node,Clamp(node+new P(-side*len*(.12f+.36f*z),len)),wide,.43f);
                        Leaf(node,Clamp(node+new P(side*len*(.14f+.51f*z),len*.76f)),wide,.69f);
                        Organ(Clamp(node+new P(0,len*.40f)),span*(.10f+.13f*z),side*.14f);
                    }
                }
                else if(mode==33)
                {
                    // Epiphyllous blooms form on the upper surface of
                    // new leaf blades; no independent floating flowers.
                    for(int side=-1;side<=1;side+=2)
                    for(int j=0;j<2;j++)
                    {
                        P root=At(side,.10f,.40f+j*.22f),tip=At(side,.45f+.25f*z,.47f+j*.24f);
                        Leaf(root,tip,span*(.07f+.05f*z),.50f+j*.08f);
                        for(int k=1;k<=3;k++)
                        {
                            P n=Mix(root,tip,k/4f);
                            Organ(n+new P(0,unit*.7f),span*(.05f+.07f*z)*(1-.09f*k),side*.22f);
                        }
                    }
                }
                else if(mode==34)
                {
                    // Imbricate scale leaves form a living overlapping
                    // rosette around a new apical stem.
                    P start=At(-1,.25f,.62f),top=At(-1,.43f,.79f+.27f*z);
                    Stem(start,Mix(start,top,.55f),top,unit*1.7f);
                    float rad=span*(.13f+.16f*z);
                    for(int j=0;j<13;j++)
                    {
                        float a0=j*2.399963f,r=(.23f+.77f*(j/12f))*rad;
                        P tip=Clamp(top+new P(r*MathF.Cos(a0),r*MathF.Sin(a0)*.66f),3);
                        P root=Mix(top,tip,.18f);
                        Leaf(root,tip,Math.Max(unit*1.3f,r*.38f),.43f+.22f*(j%3)/2f);
                    }
                    Organ(top,span*.10f*z,0);
                }
                else if(mode==35)
                {
                    // Serrated compound lamina: the saw-tooth margin
                    // is drawn as overlapping small lobes, not holes.
                    for(int side=-1;side<=1;side+=2)
                    {
                        P root=At(side,.10f,.55f),tip=At(side,.48f+.24f*z,.68f+side*.10f);
                        float length=span*(.10f+.09f*z);
                        Leaf(root,tip,length*.66f,.49f);
                        for(int k=1;k<7;k++)
                        {
                            P n=Mix(root,tip,k/7f);
                            float r=length*MathF.Sin(k/7f*MathF.PI);
                            Leaf(n,Clamp(n+new P(side*r*.44f,r*.83f)),r*.30f,.59f);
                            Leaf(n,Clamp(n+new P(side*r*.32f,-r*.71f)),r*.25f,.44f);
                        }
                    }
                }
                else if(mode==36)
                {
                    // Dehiscent capsule: two split valves expose seeds
                    // along their attached central column.
                    for(int side=-1;side<=1;side+=2)
                    {
                        P root=At(side,.12f,.58f),n=At(side,.52f,.70f+side*.08f);
                        Stem(root,At(side,.37f,.81f),n,unit*1.1f);
                        float r=span*(.09f+.035f*z),len=height*.19f;
                        P left=Clamp(n+new P(-r*(.22f+.88f*z),len*.44f),r+2);
                        P right=Clamp(n+new P(r*(.22f+.88f*z),len*.44f),r+2);
                        Paint(left,r*.56f,len*.61f,(x,y)=>Tint(Color(.43f+.21f*Unit(-x)),1.21f,.90f,.54f));
                        Paint(right,r*.56f,len*.61f,(x,y)=>Tint(Color(.43f+.21f*Unit(x)),1.21f,.90f,.54f));
                        Line(n,n+new P(0,len),unit*.55f,unit*.55f);
                        for(int j=0;j<4;j++)Seed(n+new P((j%2==0?-1:1)*r*.28f,len*(.15f+.21f*j)),unit*(.75f+.36f*z),.62f);
                    }
                }
                else if(mode==37)
                {
                    // Paired winged samaras unfold and change their
                    // projected angle while remaining tethered.
                    for(int side=-1;side<=1;side+=2)
                    {
                        P root=At(side,.15f,.50f),n=At(side,.61f,.70f+side*.04f);
                        Stem(root,At(side,.36f,.77f),n,unit*1.1f);
                        float angle=.30f+1.30f*z,len=span*(.14f+.16f*z);
                        for(int j=-1;j<=1;j+=2)
                        {
                            P end=Clamp(n+new P(j*len*MathF.Sin(angle),len*MathF.Cos(angle)),4);
                            Leaf(n,end,len*.20f,.70f);
                            Seed(Mix(n,end,.18f),unit*1.3f,.72f);
                        }
                    }
                }
                else if(mode==38)
                {
                    // Extended anther filaments open from two real
                    // source-art flowers/bolls on new short pedicels.
                    for(int side=-1;side<=1;side+=2)
                    {
                        P root=At(side,.12f,.65f),n=At(side,.51f,.91f+side*.05f);
                        Stem(root,At(side,.33f,.82f),n,unit*1.1f);
                        Organ(n,span*(.13f+.06f*z),0);
                        float r=span*(.09f+.16f*z);
                        for(int j=0;j<6;j++)
                        {
                            float theta=.24f+j*2.66f/5f;
                            P tip=Clamp(n+new P(r*MathF.Cos(theta),r*MathF.Sin(theta)),3);
                            Line(n,tip,unit*.55f,unit*.48f);
                            Paint(tip,unit*1.15f,unit*.72f,(x,y)=>new Pixel(230,184,(byte)(69+33*Unit(-x)),255));
                        }
                    }
                }
                else if(mode==39)
                {
                    // Petaloid doubling: an outer whorl of pale lobes,
                    // followed by a second inner whorl of actual organs.
                    for(int side=-1;side<=1;side+=2)
                    {
                        P root=At(side,.10f,.60f),n=At(side,.49f,.91f+side*.06f);
                        Stem(root,At(side,.33f,.76f),n,unit*1.1f);
                        float radius=span*(.065f+.16f*z);
                        for(int j=0;j<7;j++)
                        {
                            float theta=j*MathF.PI*2/7;
                            P tip=Clamp(n+new P(radius*MathF.Cos(theta),radius*MathF.Sin(theta)),3);
                            Organ(tip,span*(.07f+.11f*z),theta);
                        }
                        Organ(n,span*.15f,0);
                    }
                }
                else if(mode==40)
                {
                    // Seed shedding from real visual capsule appendages.
                    // A bounded decorative trail; no gameplay spawn.
                    for(int side=-1;side<=1;side+=2)
                    {
                        P root=At(side,.10f,.57f),n=At(side,.59f,.87f+side*.05f);
                        Stem(root,At(side,.40f,.80f),n,unit*1.10f);
                        Organ(n,span*.18f,side*.23f);
                        for(int j=0;j<8;j++)
                        {
                            float q=Unit((f-j*.066f)*1.7f);
                            if(q<.015f)continue;
                            P at=Clamp(n+new P(side*span*(.05f+.16f*q)*MathF.Sin(j*2.3f+q*2),-height*.53f*q),2);
                            Seed(at,Math.Max(.8f,unit*(1.0f+.25f*(j%3))),.65f);
                        }
                    }
                }
                else if(mode==42)
                {
                    // Rolling lamina: a moving rolled blade shows a
                    // narrow silver-green back and later the open face.
                    for(int side=-1;side<=1;side+=2)
                    {
                        P root=At(side,.12f,.43f),tip=At(side,.46f+.25f*z,.60f+side*.08f);
                        float width=span*(.032f+.13f*z);
                        Leaf(root,tip,width,.55f);
                        P middle=Mix(root,tip,.50f);
                        Leaf(root,Clamp(middle+new P(side*span*.12f,height*(.10f-.08f*z))),width*.57f,.76f);
                        if(f<.7f)Paint(Mix(root,tip,.68f),Math.Max(unit,width*.50f),unit*(1.05f+.5f*(1-f)),(x,y)=>Tint(Color(.71f),1.16f,1.10f,1.33f));
                    }
                }
                else if(mode==43)
                {
                    // Amber resin slowly elongates into transparent-looking
                    // droplets at attached petiole junctions.
                    for(int side=-1;side<=1;side+=2)
                    {
                        P root=At(side,.11f,.49f),n=At(side,.52f,.53f+side*.10f);
                        Stem(root,At(side,.32f,.68f),n,unit*1.15f);
                        Leaf(n,Clamp(n+new P(side*span*.09f,height*.16f)),span*.04f,.55f);
                        float r=span*(.037f+.038f*z),len=r*(1.2f+1.5f*z);
                        P drop=Clamp(n+new P(0,-len*.83f),r+2);
                        Line(n,drop,unit*.55f,unit*.4f);
                        Paint(drop,r,len,(x,y)=>new Pixel((byte)(148+87*Unit(-x-y*.4f)),(byte)(78+94*Unit(-x-y*.5f)),(byte)(20+42*Unit(-x)),255));
                        Paint(drop+new P(-r*.27f,len*.36f),r*.23f,len*.19f,(x,y)=>new Pixel(255,230,161,255));
                    }
                }
                else if(mode==44)
                {
                    // Exfoliating bark ribbons, curling away from the
                    // base in opposite directions, using muted stem hues.
                    for(int side=-1;side<=1;side+=2)
                    {
                        P root=At(side,.22f,.10f+side*.035f),last=root;
                        float radius=span*(.12f+.20f*z),reach=height*(.07f+.22f*z);
                        for(int k=1;k<=25;k++)
                        {
                            float q=k/25f,a0=q*(2.0f+3.2f*z);
                            P n=Clamp(root+new P(side*(radius*(1-MathF.Cos(a0))*.92f+span*.21f*q),reach*q+radius*.37f*MathF.Sin(a0)),3);
                            float r=unit*(1.8f-.90f*q);
                            float dis=MathF.Sqrt((last.X-n.X)*(last.X-n.X)+(last.Y-n.Y)*(last.Y-n.Y));
                            int steps=Math.Max(1,(int)MathF.Ceiling(dis*2));
                            for(int k2=0;k2<=steps;k2++)
                            {
                                P pos=Mix(last,n,k2/(float)steps);
                                Paint(pos,r,r*.70f,(x,y)=>new Pixel((byte)(111+64*Unit(-x)),(byte)(83+48*Unit(-x)),(byte)(48+29*Unit(-x)),255));
                            }
                            last=n;
                        }
                    }
                }
            }
            // Immutable exact original color and alpha in all occupied
            // pixels. No masking of flowers; no occlusion reconstruction.
            for(int y=0;y<h;y++)for(int x=0;x<w;x++)
            {int i=y*w+x;if(original[i].A>0)outp[(y+oy)*W+x+ox]=original[i];}
            // Two optical modes intentionally modify only classified foliage.
            // All reproductive and unknown tissue stays byte-exact.
            if(mode is 41 or 45)
            {
                float phase=Math.Clamp(frame,0,8)/8f;
                for(int y=0;y<h;y++)for(int x=0;x<w;x++)
                {
                    int i=y*w+x;var p=original[i];
                    if(!ForgeTintAllowed(mode,a,p,i))continue;
                    var map=a.Mask[i];
                    float u=x/(float)Math.Max(1,w-1),v=y/(float)Math.Max(1,h-1);
                    float amount;float r,g,b;
                    if(mode==41)
                    {
                        // Directional thin-film illusion. Local anatomy
                        // territories and inferred vein normals dephase the
                        // sweep; no uniform whole-plant rainbow.
                        float theta=phase*6.283185f+map.B/255f*3.2f+u*4.2f-v*1.8f;
                        float lobe=MathF.Pow(.5f+.5f*MathF.Sin(theta),2.0f);
                        amount=lobe*(.28f+.48f*map.G/255f);
                        r=.80f+.76f*(.5f+.5f*MathF.Cos(theta+1.6f));
                        g=.90f+.25f*(.5f+.5f*MathF.Sin(theta+2.2f));
                        b=1.12f+1.45f*(.5f+.5f*MathF.Cos(theta));
                    }
                    else
                    {
                        // Self-shadow projection from the source's own
                        // alpha silhouette, offset by a moving light angle.
                        float ang=phase*5.15f-.8f;
                        int dx=(int)MathF.Round(w*.15f*MathF.Cos(ang)),dy=(int)MathF.Round(h*.13f*MathF.Sin(ang));
                        float coverage=0;int count=0;
                        for(int j=-1;j<=1;j++)for(int k=-1;k<=1;k++)
                        {
                            int sx=x+dx+k,sy=y+dy+j;count++;
                            if(sx>=0&&sx<w&&sy>=0&&sy<h&&original[sy*w+sx].A>=90)coverage++;
                        }
                        amount=(coverage/count)*(.24f+.43f*map.G/255f);
                        r=.63f;g=.68f;b=.85f;
                    }
                    byte C(byte c,float m)=>(byte)Math.Clamp((int)MathF.Round(c*(1-amount+amount*m)),0,255);
                    outp[(y+oy)*W+x+ox]=new(C(p.R,r),C(p.G,g),C(p.B,b),p.A);
                }
            }

            return outp;
        }
    }
}
