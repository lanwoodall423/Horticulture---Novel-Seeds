using System;
using System.Collections.Generic;

namespace HorticultureNovelSeeds
{
    // Bounded, engine-independent journal facts. No XP, inferred yield, or trait authority.
    public class CultivationFacts
    {
        public const int RecentLimit = 64;
        public long firstObservedTick = -1;
        public long firstHarvestTick = -1;
        public long lastHarvestTick = -1;
        public int sowings;
        public int harvests;
        public int minYield = -1;
        public int maxYield = -1;
        public List<string> recentEvents = new List<string>();

        public void Normalize()
        {
            sowings = Math.Max(0, sowings);
            harvests = Math.Max(0, harvests);
            if (harvests == 0 || minYield <= 0 || maxYield < minYield)
                minYield = maxYield = -1;
            recentEvents ??= new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var normalized = new List<string>();
            for (int i = recentEvents.Count - 1; i >= 0 && normalized.Count < RecentLimit; i--)
                if (ValidId(recentEvents[i]) && seen.Add(recentEvents[i])) normalized.Add(recentEvents[i]);
            normalized.Reverse();
            recentEvents = normalized;
        }

        public bool RecordSowing(long tick, string identity)
        {
            if (tick < 0 || !Accept(identity)) return false;
            Observe(tick);
            if (sowings < int.MaxValue) sowings++;
            return true;
        }

        // Only call for a positively confirmed output, never for a predicted YieldNow.
        public bool RecordHarvest(long tick, string identity, int placedYield)
        {
            if (tick < 0 || placedYield <= 0 || !Accept(identity)) return false;
            Observe(tick);
            if (firstHarvestTick < 0 || tick < firstHarvestTick) firstHarvestTick = tick;
            lastHarvestTick = Math.Max(lastHarvestTick, tick);
            if (harvests < int.MaxValue) harvests++;
            minYield = minYield < 0 ? placedYield : Math.Min(minYield, placedYield);
            maxYield = Math.Max(maxYield, placedYield);
            return true;
        }

        public bool RecordDiscovery(long tick, string identity)
        {
            if (tick < 0 || !Accept(identity)) return false;
            Observe(tick);
            return true;
        }

        private void Observe(long tick)
        {
            if (firstObservedTick < 0 || tick < firstObservedTick) firstObservedTick = tick;
        }

        private static bool ValidId(string identity) => !string.IsNullOrWhiteSpace(identity) && identity.Length <= 256;
        private bool Accept(string identity)
        {
            if (!ValidId(identity)) return false;
            recentEvents ??= new List<string>();
            if (recentEvents.Contains(identity)) return false;
            recentEvents.Add(identity);
            while (recentEvents.Count > RecentLimit) recentEvents.RemoveAt(0);
            return true;
        }
    }
}
