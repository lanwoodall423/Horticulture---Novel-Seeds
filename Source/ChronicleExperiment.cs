using RimWorld;
using Verse;

namespace HorticultureNovelSeeds
{
    // Purely visual and opt-in. Only record at real growth threshold crossings.
    internal static class ChronicleExperiment
    {
        internal static bool Enabled {get;set;}
#if HNS_NATIVE_FIXTURE
        internal static readonly System.Collections.Generic.Dictionary<int,int> Samples=new();
#endif
        internal static void Expose(ref int code)
        {
            Scribe_Values.Look(ref code,"hnsVisualChronicle",0);
            if(Scribe.mode==LoadSaveMode.PostLoadInit||Scribe.mode==LoadSaveMode.LoadingVars)code=ChronicleMath.Sanitize(code);
        }
        internal static void GrowthChanged(Plant plant,float before)
        {
            if(!Enabled||plant?.Spawned!=true||plant.Growth<=before)return;
            var comp=plant.TryGetComp<CompPlantVariety>();
            if(comp==null||!ChronicleMath.Valid(PlantFormMesh.Resolve(comp).Kind))return;
            int old=comp.VisualChronicle;
            int next=ChronicleMath.Record(old,before,plant.Growth,NeighborhoodExperiment.Current(plant));
            if(old==next)return;
            comp.VisualChronicle=next;plant.DirtyMapMesh(plant.Map);
#if HNS_NATIVE_FIXTURE
            int id=plant.thingIDNumber;Samples.TryGetValue(id,out int count);
            Samples[id]=count+ChronicleMath.Count(next)-ChronicleMath.Count(old);
#endif
        }
    }
}
