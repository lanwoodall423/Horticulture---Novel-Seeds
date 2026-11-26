using System;

namespace HorticultureNovelSeeds
{
    // Directional lateral occupancy, not a simulated light or root network.
    // Quantize neighboring growth so small changes do not trigger remeshing.
    internal static class NeighborhoodMath
    {
        internal readonly record struct Pressure(float Left,float Right)
        {
            internal float Density=>(Left+Right)*.5f;
            internal float Difference=>Left-Right;
        }
        internal static bool Valid(PlantFormKind kind)=>kind>=PlantFormKind.GapSeeker&&kind<=PlantFormKind.FreeRosette;
        internal static float Band(float growth)=>!float.IsFinite(growth)||growth<.20f?0f:growth<.45f?.25f:growth<.75f?.5f:growth<.95f?.75f:1f;
        internal static (float X,float Y) Warp(PlantFormKind kind,float strength,float x,float y,Pressure pressure)
        {
            if(!Valid(kind)||!float.IsFinite(strength)||strength<=0f||strength>1f)return(x,y);
            float l=Math.Clamp(pressure.Left,0f,1f),r=Math.Clamp(pressure.Right,0f,1f);
            if(!float.IsFinite(l)||!float.IsFinite(r))return(x,y);
            float d=(l+r)*.5f,delta=l-r,t=y,t2=t*t,w=x-.5f;
            float a=w,b=t,side=w<0f?l:r,other=w<0f?r:l;
            switch(kind)
            {
                case PlantFormKind.GapSeeker:
                    a=w*(1f-.13f*d*t)+.36f*delta*t2;b=t-.045f*d*t2;break;
                case PlantFormKind.CompanionBow:
                    a=w*(1f-.08f*d*t)-.32f*delta*t2;b=t-.16f*MathF.Abs(delta)*t2;break;
                case PlantFormKind.CrownPartition:
                    a=w*(1f+(.63f*other*(1f-side)-.48f*side)*t2);b=t-.12f*MathF.Abs(w)*side*t2;break;
                case PlantFormKind.StandSpire:
                    a=w*(1f-.56f*d*t);b=t+.27f*d*t2;break;
                case PlantFormKind.FreeRosette:
                    a=w*(1f+.72f*(1f-d)*t2-.24f*d*t);b=t-.22f*(1f-d)*t2;break;
            }
            return(x+(a-w)*strength,y+(b-t)*strength);
        }
    }
}
