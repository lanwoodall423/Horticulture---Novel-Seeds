using System;
namespace HorticultureNovelSeeds
{
    public enum ResponsiveHabitKind { None,Nightfold,Sunseeker,Rainbowed,Nightbloom,Raincup,DawnPetals,DuskPetals,RainPendant,SolarTilt,SilkFlutter,SuccessionBloom,SectorSport,BiformHarvest,SisterBuds,RipeningMosaic }
    internal static class ResponsiveHabitMath
    {
        internal static bool Valid(ResponsiveHabitKind kind)=>kind>=ResponsiveHabitKind.Nightfold&&kind<=ResponsiveHabitKind.RipeningMosaic;
        internal readonly record struct Pose(float Height,float Contraction,float Lean,float Droop);
        private static float Unit(float x)=>Math.Clamp(float.IsFinite(x)?x:0f,0f,1f);
        private static float Q(float x)=>MathF.Round(x*64f)/64f;
        private static float Smooth(float x)=>x*x*(3f-2f*x);
        internal static Pose Controls(ResponsiveHabitKind kind,float day,float sky,float rain,bool sheltered,bool flipped)
        {
            float light=Smooth(Unit((sky-.12f)/.60f));
            float night=1f-light,wet=sheltered?0f:Smooth(Unit(rain*2f));
            float solar= MathF.Sin((Unit(day)-.5f)*2f*MathF.PI)*light*(flipped?-1f:1f);
            var p=kind switch
            {
                ResponsiveHabitKind.Nightfold=>new Pose(1f-.13f*night,.60f*night,0f,0f),
                ResponsiveHabitKind.Sunseeker=>new Pose(1f-.035f*MathF.Abs(solar),.43f*MathF.Abs(solar),.19f*solar,0f),
                ResponsiveHabitKind.Rainbowed=>new Pose(1f-.22f*wet,.13f*wet,0f,.29f*wet),
                ResponsiveHabitKind.Nightbloom=>new Pose(1f-.06f*night,.57f*light,0f,0f),
                ResponsiveHabitKind.Raincup=>new Pose(1f-.04f*wet,.66f*wet,0f,0f),
                _=>new Pose(1f,0f,0f,0f)
            };
            return new(Q(p.Height),Q(p.Contraction),Q(p.Lean),Q(p.Droop));
        }
        // CPU reference for the shader. The mapping is bijective on the
        // source rectangle, bounded to the existing quad, and root anchored.
        internal static (float X,float Y) Forward(Pose p,float x,float y)
        {
            if(p.Droop>0f)return (.5f+(x-.5f)*(1f-p.Contraction),y*p.Height*(1f-p.Droop*(2f*x-1f)*(2f*x-1f)));
            return (.5f+(x-.5f)*(1f-p.Contraction*y)+p.Lean*y*y,y*p.Height);
        }
        internal static (float X,float Y) Inverse(Pose p,float x,float y)
        {
            if(p.Droop>0f)
            {
                float u=.5f+(x-.5f)/(1f-p.Contraction);
                return (u,y/(p.Height*(1f-p.Droop*(2f*u-1f)*(2f*u-1f))));
            }
            float v=y/p.Height;
            return (.5f+(x-.5f-p.Lean*v*v)/(1f-p.Contraction*v),v);
        }
    }
}
