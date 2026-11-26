using HarmonyLib;
using RimWorld;
using Verse;

namespace HorticultureNovelSeeds
{
    // Growth setters and the game's direct TickLong growth writes both reach
    // a bounded visual invalidation. No new gameplay state or per-frame scan.
    internal static class DevelopmentalPhenotypeRefresh
    {
#if HNS_NATIVE_FIXTURE
        internal static readonly System.Collections.Generic.HashSet<(int,int)> Invalidations=new();
#endif
        internal static void Refresh(Plant plant,float before)
        {
            if(!WaylithPhenotypeShader.Enabled || !plant.Spawned ||
               !DevelopmentalPhenotype.Crossed(before,plant.Growth))return;
            var comp=plant.TryGetComp<CompPlantVariety>();
            if(!DevelopmentalPhenotype.IsMode((int)ProceduralPhenotypeUtility.Resolve(comp).x))return;
            plant.DirtyMapMesh(plant.Map);
#if HNS_NATIVE_FIXTURE
            Invalidations.Add((plant.thingIDNumber,DevelopmentalPhenotype.Band(plant.Growth)));
#endif
        }
    }
    [HarmonyPatch(typeof(Plant),nameof(Plant.Growth),MethodType.Setter)]
    internal static class DevelopmentalGrowthSetterPatch
    {
        internal static void Prefix(Plant __instance,out float __state)=>__state=__instance.Growth;
        internal static void Postfix(Plant __instance,float __state){DevelopmentalPhenotypeRefresh.Refresh(__instance,__state);NeighborhoodExperiment.GrowthChanged(__instance,__state);ChronicleExperiment.GrowthChanged(__instance,__state);}
    }
    [HarmonyPatch(typeof(Plant),nameof(Plant.TickLong))]
    internal static class DevelopmentalGrowthTickPatch
    {
        internal static void Prefix(Plant __instance,out float __state)=>__state=__instance.Growth;
        internal static void Postfix(Plant __instance,float __state){DevelopmentalPhenotypeRefresh.Refresh(__instance,__state);NeighborhoodExperiment.GrowthChanged(__instance,__state);ChronicleExperiment.GrowthChanged(__instance,__state);}
    }
}
