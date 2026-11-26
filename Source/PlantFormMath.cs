using System;

namespace HorticultureNovelSeeds
{
    public enum PlantFormKind { None, Arching, FanCrowned, Rounded, Weeping, Spired, GapSeeker, CompanionBow, CrownPartition, StandSpire, FreeRosette, CrookedChronicle, RingedHabit, AsymmetricWhorls, ReleaseCrown, StretchBands }

    // Pure normalized geometry, independent of the renderer. x/y are original
    // plane coordinates in [0,1]; base row is anchored, all transforms bounded.
    internal static class PlantFormMath
    {
        internal const int Subdivisions = 4;
        internal static bool IsValid(PlantFormKind kind, float strength) =>
            kind >= PlantFormKind.Arching && kind <= PlantFormKind.StretchBands &&
            float.IsFinite(strength) && strength > 0f && strength <= 1f;

        internal static (float X, float Y) Warp(PlantFormKind kind, float strength, float x, float y, bool flipped, NeighborhoodMath.Pressure pressure=default,int chronicle=0)
        {
            if (!IsValid(kind,strength)) return (x,y);
            if(ChronicleMath.Valid(kind))return ChronicleMath.Warp(kind,strength,x,y,chronicle);
            if(NeighborhoodMath.Valid(kind))return NeighborhoodMath.Warp(kind,strength,x,y,pressure);
            float w=x-.5f, t=y, t2=t*t, edge=4f*w*w;
            float a=w,b=t;
            switch(kind)
            {
                case PlantFormKind.Arching:
                    a=w*(1f-.12f*t)+.34f*t2*(flipped?-1f:1f);
                    b=t-.09f*t2;
                    break;
                case PlantFormKind.FanCrowned:
                    a=w*(1f+.66f*t2);
                    b=t-.10f*edge*t2;
                    break;
                case PlantFormKind.Rounded:
                    a=w*(1f+.65f*MathF.Sin(MathF.PI*t));
                    b=t-.20f*t2;
                    break;
                case PlantFormKind.Weeping:
                    a=w*(1f+.48f*t);
                    b=t-.36f*edge*t2;
                    break;
                case PlantFormKind.Spired:
                    a=w*(1f-.48f*t);
                    b=t+.24f*t2;
                    break;
            }
            return (x+(a-w)*strength, y+(b-t)*strength);
        }
    }
}
