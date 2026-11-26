using System;

namespace HorticultureNovelSeeds
{
    public enum SurfaceFinishKind { None, SatinCuticle, VelvetNap, SilverBloom, RainBeads, PearlCuticle }
    internal static class SurfaceFinishMath
    {
        internal static bool Valid(SurfaceFinishKind kind) => kind>=SurfaceFinishKind.SatinCuticle && kind<=SurfaceFinishKind.PearlCuticle;
        private static float Q(float value) => MathF.Round(Math.Clamp(float.IsFinite(value)?value:0f,0f,1f)*32f)/32f;
        // A stylized 2D light direction, not actual scene normals or ray tracing.
        // Quantize only the shared environment; never add time to a material key.
        internal static (float X,float Y,float Light,float Rain) Environment(float day,float glow,float rain)
        {
            day=float.IsFinite(day)?day:.5f;
            float angle=(day-.5f)*2f*MathF.PI;
            return (MathF.Round(MathF.Sin(angle)*32f)/32f, MathF.Round(MathF.Cos(angle)*32f)/32f,Q(glow),Q(rain*2f));
        }
    }
}
