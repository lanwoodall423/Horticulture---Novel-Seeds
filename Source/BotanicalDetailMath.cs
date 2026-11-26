using System;
using System.Collections.Generic;
using System.Linq;
using Pixel = HorticultureNovelSeeds.OrganSpriteMath.Pixel;

namespace HorticultureNovelSeeds
{
    public enum BotanicalDetailKind { None,LobedLeaf,Windowleaf,SideShoots,CurlingTendrils,LanternBuds,IvoryNerves,CrimsonNetwork,LivingGlass,VascularChimera,BronzeReticulum,PhotosyntheticPulse,NyctinasticFolding,HydraulicWilt,CrystalDewLensing,PollinationCloud,MosaicOrganism,SeasonalColorFront,SubsurfaceRootHalo,LeafSkeletonization,BioluminescentSignaling,WindDifferentialAnatomy,FractalVenationBloom,TranslucentPetalTissue,CanopyVolumeParallax,MicroHabitatResponse,FruitRipeningCascade,FrostEtching,MycelialRelay,MorphogeneticReconfiguration,EventDrivenExpression,CircinateFrond,ClimbingHelix,UmbelCrown,PendantRaceme,CrestedFan }
    internal static class BotanicalDetailMath
    {
        internal static bool Valid(BotanicalDetailKind k)=>k>=BotanicalDetailKind.LobedLeaf&&k<=BotanicalDetailKind.CrestedFan;
        internal static bool Topology(BotanicalDetailKind k)=>k>=BotanicalDetailKind.LobedLeaf&&k<=BotanicalDetailKind.CurlingTendrils;
        internal static bool Green(Pixel p)=>p.A>=100&&p.G>=p.R*1.035f&&p.G>=p.B*1.16f&&p.G>45;
        internal static float Darkness(float sky)=>1f-Math.Clamp((float.IsFinite(sky)?sky:1f)-.12f,0f,.48f)/.48f;
        internal sealed record Result(Pixel[] Pixels,int Removed,int Added);
        // Keep all original non-foliage pixels; new shapes may occupy transparent pixels only.
        internal static Result Edit(Pixel[] source,int w,int h,BotanicalDetailKind kind)
        {
            var dst=(Pixel[])source.Clone();
            if(!Topology(kind)||w<16||h<16||source.Length!=w*h)return new(dst,0,0);
            var green=Enumerable.Range(0,source.Length).Where(i=>Green(source[i])).ToArray();
            if(green.Length<24)return new(dst,0,0);
            int x0=green.Min(i=>i%w),x1=green.Max(i=>i%w),y0=green.Min(i=>i/w),y1=green.Max(i=>i/w);
            int bw=x1-x0+1,bh=y1-y0+1;
            if(bw<8||bh<8)return new(dst,0,0);
            int Index(int x,int y)=>y*w+x;
            bool In(int x,int y)=>x>=0&&x<w&&y>=0&&y<h;
            bool Leaf(int x,int y)=>In(x,y)&&Green(source[Index(x,y)]);
            bool Air(int x,int y)=>In(x,y)&&source[Index(x,y)].A<70;
            var anchors=new List<(int X,int Y)>();
            int removed=0,added=0;
            void Cut(int x,int y,int rx,int ry)
            {
                for(int dy=-ry;dy<=ry;dy++)for(int dx=-rx;dx<=rx;dx++)
                {
                    int xx=x+dx,yy=y+dy;
                    if(dx*dx/(float)(rx*rx)+dy*dy/(float)(ry*ry)>1f||!Leaf(xx,yy))continue;
                    int i=Index(xx,yy);if(dst[i].A==0)continue;
                    if(removed>=green.Length*.13f)continue;
                    dst[i]=default;removed++;
                }
            }
            // An interior window must have an intact green border in every direction.
            if(kind==BotanicalDetailKind.Windowleaf)
            {
                int r=Math.Clamp(bw/15,2,7),spacing=r*4;
                for(int y=y0+r+2;y<=y1-r-2;y+=Math.Max(2,r))
                    for(int x=x0+r+2;x<=x1-r-2;x+=Math.Max(2,r))
                    {
                        if(anchors.Count>=7||anchors.Any(a=>(a.X-x)*(a.X-x)+(a.Y-y)*(a.Y-y)<spacing*spacing))continue;
                        bool clear=true;
                        for(int dy=-r-1;dy<=r+1&&clear;dy++)for(int dx=-r-1;dx<=r+1;dx++)
                            if(dx*dx+dy*dy<=(r+1)*(r+1)&&!Leaf(x+dx,y+dy)){clear=false;break;}
                        if(!clear)continue;
                        Cut(x,y,Math.Max(1,r-1),r);anchors.Add((x,y));
                    }
            }
            else if(kind==BotanicalDetailKind.LobedLeaf)
            {
                int r=Math.Clamp(bw/10,2,8);
                foreach(int side in new[]{-1,1})
                    for(int y=y0+Math.Max(r,bh/5);y<y1-r;y+=Math.Max(r*3,bh/6))
                    {
                        // Only scallop a sufficiently thick patch of leaf.
                        int edge=side<0?x0:x1;
                        while(edge>=x0&&edge<=x1&&!Leaf(edge,y))edge-=side;
                        if(edge<x0||edge>x1||!Air(edge+side,y)||!Leaf(edge-side*(r+2),y)||
                           !Leaf(edge-side*(r+1),y-r/2)||!Leaf(edge-side*(r+1),y+r/2))continue;
                        Cut(edge+side,y,r,r);anchors.Add((edge,y));
                    }
            }
            else
            {
                void Ink(float x,float y,float radius,Pixel color)
                {
                    int xlo=(int)MathF.Floor(x-radius),xhi=(int)MathF.Ceiling(x+radius);
                    int ylo=(int)MathF.Floor(y-radius),yhi=(int)MathF.Ceiling(y+radius);
                    for(int yy=ylo;yy<=yhi;yy++)for(int xx=xlo;xx<=xhi;xx++)
                    {
                        if(!In(xx,yy)||!Air(xx,yy)||(xx-x)*(xx-x)+(yy-y)*(yy-y)>radius*radius)continue;
                        int i=Index(xx,yy);
                        if(dst[i].A<100){if(added>=green.Length*.34f)continue;added++;}
                        dst[i]=color;
                    }
                }
                Pixel Shade(Pixel p,float f)=>new((byte)Math.Clamp((int)(p.R*f),0,255),(byte)Math.Clamp((int)(p.G*f),0,255),(byte)Math.Clamp((int)(p.B*f),0,255),255);
                void Line(float ax,float ay,float bx,float by,float radius,Pixel p)
                {
                    int n=Math.Max(1,(int)MathF.Ceiling(MathF.Sqrt((bx-ax)*(bx-ax)+(by-ay)*(by-ay))*2f));
                    for(int j=0;j<=n;j++){float t=j/(float)n;Ink(ax+(bx-ax)*t,ay+(by-ay)*t,radius,p);}
                }
                void Leaflet(float ax,float ay,float bx,float by,float width,Pixel p)
                {
                    float dx=bx-ax,dy=by-ay,len=MathF.Sqrt(dx*dx+dy*dy);
                    if(len<2)return;
                    int n=(int)MathF.Ceiling(len*2f);
                    for(int j=0;j<=n;j++)
                    {
                        float t=j/(float)n,r=Math.Max(.65f,width*MathF.Pow(MathF.Sin(MathF.PI*t),.80f));
                        Ink(ax+dx*t,ay+dy*t,r,Shade(p,.78f));
                    }
                    for(int j=1;j<n;j++)
                    {
                        float t=j/(float)n,r=Math.Max(.4f,width*MathF.Pow(MathF.Sin(MathF.PI*t),.80f)-.65f);
                        Ink(ax+dx*t,ay+dy*t,r,Shade(p,1.05f+.13f*MathF.Sin(MathF.PI*t)));
                    }
                    Line(ax,ay,bx,by,.55f,Shade(p,1.15f));
                }
                foreach(int side in new[]{-1,1})
                {
                    int y=y0+(int)(bh*(side<0?.38f:.63f));
                    int ax=-1,ay=y;
                    for(int dy=0;dy<=Math.Max(2,bh/8)&&ax<0;dy++)
                        foreach(int sign in new[]{1,-1})
                        {
                            int yy=y+sign*dy;if(yy<y0||yy>y1)continue;
                            int x=side<0?x0:x1;
                            while(x>=x0&&x<=x1&&!Leaf(x,yy))x-=side;
                            if(x>=x0&&x<=x1&&Air(x+side,yy)){ax=x;ay=yy;break;}
                        }
                    if(ax<0)continue;
                    float space=side<0?ax-2:w-3-ax;
                    float reach=Math.Min(space,Math.Max(5,bw*.23f));
                    if(reach<4||ay+reach*.75f>=h-2)continue;
                    var p=source[Index(ax,ay)];
                    float r=Math.Clamp(bw*.023f,.8f,2.1f);
                    if(kind==BotanicalDetailKind.SideShoots)
                    {
                        float bx=ax+side*reach*.72f,by=ay+reach*.40f;
                        Line(ax,ay,bx,by,r,Shade(p,.85f));
                        Leaflet(ax+side*reach*.27f,ay+reach*.16f,ax+side*reach*.28f,ay+reach*.78f,reach*.13f,p);
                        Leaflet(ax+side*reach*.44f,ay+reach*.24f,ax+side*reach*.93f,ay+reach*.10f,reach*.13f,p);
                        Leaflet(bx,by,ax+side*reach,ay+reach*.70f,reach*.145f,p);
                    }
                    else
                    {
                        // A hooked, tapering tendril, joined to a source pixel.
                        float px=ax,py=ay;
                        for(int j=1;j<=44;j++)
                        {
                            float t=j/44f,x,z;
                            if(t<.58f){float q=t/.58f;x=ax+side*reach*.82f*q;z=ay+reach*.42f*q*q;}
                            else{float q=(t-.58f)/.42f,ang=q*4.8f;float rr=reach*.18f*(1f-.42f*q);x=ax+side*(reach*.82f+rr*MathF.Sin(ang));z=ay+reach*.42f+rr*(1f-MathF.Cos(ang));}
                            Line(px,py,x,z,Math.Max(.65f,r*(1f-.5f*t)),Shade(p,.90f+.17f*t));px=x;py=z;
                        }
                    }
                }
            }
            return new(dst,removed,added);
        }
    }
}
