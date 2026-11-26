using System;
using System.Collections.Generic;
using System.Linq;

namespace HorticultureNovelSeeds
{
    public enum OrganFormKind { None, PlumpFruit, LongFruit, BroadFruit, ClusteredFruit, ThickStalk }
    internal static class OrganSpriteMath
    {
        internal readonly record struct Pixel(byte R,byte G,byte B,byte A);
        internal sealed record Part(int[] Indices,int MinX,int MinY,int MaxX,int MaxY,float X,float Y);
        internal static bool Valid(OrganFormKind kind) => kind>=OrganFormKind.PlumpFruit && kind<=OrganFormKind.ThickStalk;
        internal static bool Reproductive(Pixel p)
        {
            if(p.A<80) return false;
            int max=Math.Max(p.R,Math.Max(p.G,p.B)), min=Math.Min(p.R,Math.Min(p.G,p.B));
            // Bright warm reproductive pigments or pale flower/boll tissue.
            return (p.R>=78 && p.R>=p.G*.96f && p.R>=p.B*1.34f && p.R-p.B>=27) ||
                   (min>=120 && max-min<=max*.23f);
        }
        internal static List<Part> Parts(bool[] selected,int w,int h,int opaque,bool stem)
        {
            var seen=new bool[selected.Length];var list=new List<Part>();var queue=new Queue<int>();
            for(int k=0;k<selected.Length;k++)
            {
                if(!selected[k]||seen[k])continue;
                var indices=new List<int>();int x0=w,y0=h,x1=0,y1=0;long sx=0,sy=0;
                seen[k]=true;queue.Enqueue(k);
                while(queue.Count>0)
                {
                    int a=queue.Dequeue(),x=a%w,y=a/w;indices.Add(a);
                    sx+=x;sy+=y;x0=Math.Min(x0,x);x1=Math.Max(x1,x);y0=Math.Min(y0,y);y1=Math.Max(y1,y);
                    for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++)
                    {
                        int nx=x+dx,ny=y+dy;if(nx<0||nx>=w||ny<0||ny>=h)continue;
                        int b=ny*w+nx;if(!seen[b]&&selected[b]){seen[b]=true;queue.Enqueue(b);}
                    }
                }
                int area=indices.Count,bw=x1-x0+1,bh=y1-y0+1;
                // Reject specks, massive regions (whole plant), and broad
                // ambiguous regions. Prefer compact organs, rooted stems.
                if(area<Math.Max(6,w*h/12000)||area>opaque*(stem?.55f:.35f)||
                   bw>w*(stem?.55f:.65f)||(!stem&&bh>h*.65f)||area<(bw*bh)*.11f)continue;
                list.Add(new Part(indices.ToArray(),x0,y0,x1,y1,(float)sx/area,(float)sy/area));
            }
            return list.OrderByDescending(p=>p.Indices.Length).Take(stem?4:12).ToList();
        }
        // Source-over composite, retaining every untouched pixel exactly. Each
        // component is resampled from its own shape, not a rectangular crop.
        internal static Pixel Over(Pixel bg,Pixel fg)
        {
            if(fg.A==0)return bg;if(fg.A==255||bg.A==0)return fg;
            float fa=fg.A/255f,ba=bg.A/255f*(1-fa),a=fa+ba;
            byte C(byte f,byte b)=>(byte)Math.Clamp((int)MathF.Round((f*fa+b*ba)/a),0,255);
            return new Pixel(C(fg.R,bg.R),C(fg.G,bg.G),C(fg.B,bg.B),(byte)Math.Clamp((int)MathF.Round(a*255),0,255));
        }
        internal static Pixel[] Compose(Pixel[] original,int w,int h,List<Part> parts,OrganFormKind kind)
        {
            var dst=(Pixel[])original.Clone();
            if(!Valid(kind)||parts.Count==0)return dst;
            var mask=new bool[original.Length];
            foreach(var p in parts)
            {
                Array.Clear(mask);foreach(int i in p.Indices)mask[i]=true;
                // Small dilation includes source edge/shading pixels, but only
                // adjacent nontransparent pixels with compatible pigment.
                if(kind!=OrganFormKind.ThickStalk)
                    foreach(int i in p.Indices)
                    {
                        int x=i%w,y=i/w;var c=original[i];
                        for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++)
                        {
                            int nx=x+dx,ny=y+dy;if(nx<0||nx>=w||ny<0||ny>=h)continue;
                            int k=ny*w+nx;var v=original[k];
                            if(v.A>12&&v.R>=v.G*.85f&&Math.Abs(v.R-c.R)+Math.Abs(v.G-c.G)+Math.Abs(v.B-c.B)<190)mask[k]=true;
                        }
                    }
                void Draw(float scaleX,float scaleY,float offX,float offY)
                {
                    float cx=p.X+offX,cy=p.Y+offY;
                    int x0=Math.Max(0,(int)MathF.Floor(cx+(p.MinX-2-p.X)*scaleX));
                    int x1=Math.Min(w-1,(int)MathF.Ceiling(cx+(p.MaxX+2-p.X)*scaleX));
                    int y0=Math.Max(0,(int)MathF.Floor(cy+(p.MinY-2-p.Y)*scaleY));
                    int y1=Math.Min(h-1,(int)MathF.Ceiling(cy+(p.MaxY+2-p.Y)*scaleY));
                    for(int y=y0;y<=y1;y++)for(int x=x0;x<=x1;x++)
                    {
                        int sx=(int)MathF.Round(p.X+(x-cx)/scaleX),sy=(int)MathF.Round(p.Y+(y-cy)/scaleY);
                        if(sx<0||sx>=w||sy<0||sy>=h)continue;
                        int i=sy*w+sx;if(mask[i])dst[y*w+x]=Over(dst[y*w+x],original[i]);
                    }
                }
                switch(kind)
                {
                    case OrganFormKind.PlumpFruit: Draw(1.58f,1.48f,0,0); break;
                    case OrganFormKind.LongFruit: Draw(1.05f,1.82f,0,0); break;
                    case OrganFormKind.BroadFruit: Draw(1.82f,1.06f,0,0); break;
                    case OrganFormKind.ClusteredFruit:
                        float d=(p.MaxX-p.MinX+1)*.39f;
                        Draw(.89f,.91f,-d,(p.MaxY-p.MinY+1)*.13f);
                        Draw(.94f,.96f,d,0);
                        // Original is preserved between the new lobes.
                        break;
                    case OrganFormKind.ThickStalk: Draw(1.72f,1f,0,0); break;
                }
            }
            return dst;
        }
    }
}
