using Verse;

namespace HorticultureNovelSeeds
{
    // Historical Horticulture save type; intentionally no framework base class.
    // Personal knowledge is retired in 1.7. These fields are read for legacy Scribe
    // compatibility only and never affect traits, work speed, or new journal facts.
    public sealed class PlantKnowledgeRecord : IExposable
    {
        public Pawn pawn;
        public string subjectDefName;
        public float experience;
        public string cropDefName;
        public int plantsSown, plantsHarvested, plantsCut, plantsFertilized, seedsDiscovered, recipesCompleted;
        public void ExposeData()
        {
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_Values.Look(ref subjectDefName, "subjectDefName");
            Scribe_Values.Look(ref experience, "experience");
            Scribe_Values.Look(ref cropDefName, "cropDefName");
            Scribe_Values.Look(ref plantsSown, "plantsSown");
            Scribe_Values.Look(ref plantsHarvested, "plantsHarvested");
            Scribe_Values.Look(ref plantsCut, "plantsCut");
            Scribe_Values.Look(ref plantsFertilized, "plantsFertilized");
            Scribe_Values.Look(ref seedsDiscovered, "seedsDiscovered");
            Scribe_Values.Look(ref recipesCompleted, "recipesCompleted");
        }
    }
}
