using System;
using RimWorld;
using Verse;

namespace HorticultureNovelSeeds
{
    // Journal observations are best-effort and never gate core gameplay.
    public static class HorticultureEventRouter
    {
        public static void SowingCompleted(Pawn grower, Plant plant) =>
            Record(journal => journal.Sowing(grower, plant));

        internal static void HarvestOutputPlaced(Pawn worker, Plant plant, int count, string identity) =>
            Record(journal => journal.Harvest(worker, plant, count, identity));

        public static void CultivarDocumented(Pawn author, VarietyRecord variety) =>
            Record(journal => journal.Document(author, variety));

        private static void Record(Action<GameComponent_CultivationJournal> action)
        {
            try
            {
                var journal = GameComponent_CultivationJournal.Instance;
                if (journal != null) action(journal);
            }
            catch (Exception e)
            {
                Log.WarningOnce("[Horticulture] Cultivation journal observation failed: " + e.Message, 174329018);
            }
        }
    }
}
