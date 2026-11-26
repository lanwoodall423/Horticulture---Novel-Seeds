using System;

namespace HorticultureNovelSeeds
{
    // Three first-crossing growth observations, packed in nine bits.
    // 0=unobserved, 1=open, 2=left, 3=right, 4=both. Older bands never rewrite.
    internal static class ChronicleMath
    {
        internal static bool Valid(PlantFormKind kind)=>kind>=PlantFormKind.CrookedChronicle&&kind<=PlantFormKind.StretchBands;
        internal static int At(int code,int band)=>band>=0&&band<3?((code>>(band*3))&7):0;
        internal static int Sanitize(int code)
        {
            int v=0;
            for(int i=0;i<3;i++){int x=At(code,i);if(x<=4)v|=x<<(3*i);}
            return v;
        }
        internal static int Count(int code)=>(At(code,0)>0?1:0)+(At(code,1)>0?1:0)+(At(code,2)>0?1:0);
        internal static int Context(NeighborhoodMath.Pressure p)=>1+(p.Left>=.5f?1:0)+(p.Right>=.5f?2:0);
        internal static int Record(int code,float before,float after,NeighborhoodMath.Pressure p)
        {
            code=Sanitize(code);
            if(!float.IsFinite(before)||!float.IsFinite(after)||after<=before)return code;
            for(int i=0;i<3;i++)
            {
                float threshold=i==0?.40f:i==1?.65f:.90f;
                if(before<threshold&&after>=threshold&&At(code,i)==0)code|=Context(p)<<(3*i);
            }
            return code;
        }
        private static float Side(int context)=>context==2?1f:context==3?-1f:0f;
        private static float Density(int context)=>context==4?1f:context==2||context==3?.5f:0f;
        private static float Smooth(float t){t=Math.Clamp(t,0f,1f);return t*t*(3f-2f*t);}
        private static float Integral(int code,float y,Func<int,float> f)
        {
            float sum=0;
            for(int i=0;i<3;i++)sum+=f(At(code,i))*Smooth(y*3f-i);
            return sum;
        }
        private static float Local(int code,float y,Func<int,float> f)
        {
            float t=Math.Clamp(y*3f-.5f,0f,2f);int a=(int)t,b=Math.Min(2,a+1);
            float f0=f(At(code,a)),f1=f(At(code,b));
            return f0+(f1-f0)*Smooth(t-a);
        }
        internal static (float X,float Y) Warp(PlantFormKind kind,float strength,float x,float y,int code)
        {
            if(!Valid(kind)||!float.IsFinite(strength)||strength<=0f||strength>1f)return(x,y);
            code=Sanitize(code);
            float t=y,w=x-.5f,root=Smooth(t*4f),d=Local(code,t,Density),s=Local(code,t,Side);
            float a=w,b=t;
            switch(kind)
            {
                case PlantFormKind.CrookedChronicle:
                    a=w*(1f-.12f*d*root)+.17f*Integral(code,t,Side);break;
                case PlantFormKind.RingedHabit:
                    a=w*(1f-.58f*d*root);break;
                case PlantFormKind.AsymmetricWhorls:
                    a=w*(1f+root*(.46f*s*(w>=0f?1f:-1f)-.15f*d))+.07f*Integral(code,t,Side);break;
                case PlantFormKind.ReleaseCrown:
                    float earlier=Math.Max(Density(At(code,0)),Density(At(code,1)));
                    float release=At(code,2)==1?earlier:0f;
                    a=w*(1f-.40f*d*root+.78f*release*Smooth((t-.46f)/.54f));
                    b=t+.035f*Integral(code,t,Density);break;
                case PlantFormKind.StretchBands:
                    a=w*(1f-.25f*d*root);
                    b=t+.10f*Integral(code,t,Density);break;
            }
            return(x+(a-w)*strength,y+(b-t)*strength);
        }
    }
}
