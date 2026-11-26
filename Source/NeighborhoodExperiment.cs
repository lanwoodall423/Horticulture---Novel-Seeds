using HarmonyLib;
using RimWorld;
using Verse;

namespace HorticultureNovelSeeds
{
    // Opt-in only. Sample just the two lateral cells; a change event dirties
    // the adjacent opted-in plants, not the entire map or a polling scan.
    internal static class NeighborhoodExperiment
    {
        internal static bool Enabled {get;set;}
#if HNS_NATIVE_FIXTURE
        internal static readonly System.Collections.Generic.Dictionary<int,int> Invalidations=new();
#endif
        internal static NeighborhoodMath.Pressure Current(Plant plant)
        {
            if(plant?.Spawned!=true)return default;
            return new(At(plant.Map,plant.Position+new IntVec3(-1,0,0)),At(plant.Map,plant.Position+new IntVec3(1,0,0)));
        }
        private static float At(Map map,IntVec3 cell)
        {
            if(!cell.InBounds(map))return 0f;
            var neighbor=cell.GetPlant(map);
            return neighbor?.Spawned==true?NeighborhoodMath.Band(neighbor.Growth):0f;
        }
        internal static void Changed(Map map,IntVec3 cell)
        {
            BotanicalExpressionEngine.NeighborChanged(map,cell);
            if(!Enabled||map==null)return;
            for(int dx=-1;dx<=1;dx+=2)
            {
                var pos=cell+new IntVec3(dx,0,0);
                if(!pos.InBounds(map))continue;
                var plant=pos.GetPlant(map);
                if(plant?.Spawned!=true||!NeighborhoodMath.Valid(PlantFormMesh.Resolve(plant.TryGetComp<CompPlantVariety>()).Kind))continue;
                plant.DirtyMapMesh(map);
#if HNS_NATIVE_FIXTURE
                int id=plant.thingIDNumber;
                Invalidations.TryGetValue(id,out int count);Invalidations[id]=count+1;
#endif
            }
        }
        internal static void GrowthChanged(Plant plant,float before)
        {
            if((Enabled||BotanicalExpressionEngine.Enabled)&&plant.Spawned&&NeighborhoodMath.Band(before)!=NeighborhoodMath.Band(plant.Growth))
                Changed(plant.Map,plant.Position);
        }
    }
    // Patch the actual method resolved for Plant. The receiver can be a
    // nonplant when inherited, so use Thing and guard before doing any work.
    [HarmonyPatch]
    internal static class NeighborhoodSpawnPatch
    {
        private static System.Reflection.MethodBase TargetMethod()=>AccessTools.Method(typeof(Plant),nameof(Plant.SpawnSetup));
        private static void Postfix(Thing __instance)
        {
            if((NeighborhoodExperiment.Enabled||BotanicalExpressionEngine.Enabled)&&__instance is Plant plant&&plant.Spawned)
                NeighborhoodExperiment.Changed(plant.Map,plant.Position);
        }
    }
    [HarmonyPatch]
    internal static class NeighborhoodDespawnPatch
    {
        private static System.Reflection.MethodBase TargetMethod()=>AccessTools.Method(typeof(Plant),nameof(Plant.DeSpawn));
        private static void Prefix(Thing __instance,out (Map Map,IntVec3 Cell) __state)
            =>__state=(NeighborhoodExperiment.Enabled||BotanicalExpressionEngine.Enabled)&&__instance is Plant plant&&plant.Spawned?(plant.Map,plant.Position):default;
        private static void Postfix((Map Map,IntVec3 Cell) __state)
        {
            if(__state.Map!=null)NeighborhoodExperiment.Changed(__state.Map,__state.Cell);
        }
    }
}
