using System;

namespace HorticultureNovelSeeds
{
    internal static class DevelopmentalPhenotype
    {
        internal static bool IsMode(int mode) => mode>=51 && mode<=55;
        // Three finite, shared material states. No tick-based material keys.
        internal static int Band(float growth) => !float.IsFinite(growth)||growth<.4f ? 0 : growth<.8f ? 1 : 2;
        internal static float Phase(float growth) => Band(growth)*.5f;
        internal static bool Crossed(float before,float after) => Band(before)!=Band(after);
    }
}
