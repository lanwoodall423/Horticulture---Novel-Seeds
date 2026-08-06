using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Verse;

namespace HorticultureNovelSeeds
{
    public sealed class HorticultureKnowledgeEventDiagnosticsSnapshot
    {
        public readonly IReadOnlyDictionary<string, int> submittedByEvent;
        public readonly IReadOnlyDictionary<string, int> deduplicatedByEvent;
        public readonly int rejectedUnsupportedPlants;
        public readonly int targetedInvalidations;
        public readonly int broadInvalidations;
        public readonly int speciesSubjectCount;
        public readonly int cultivarSubjectCount;

        internal HorticultureKnowledgeEventDiagnosticsSnapshot(IDictionary<string, int> submittedByEvent,
            IDictionary<string, int> deduplicatedByEvent, int rejectedUnsupportedPlants, int targetedInvalidations,
            int broadInvalidations, int speciesSubjectCount, int cultivarSubjectCount)
        {
            this.submittedByEvent = new ReadOnlyDictionary<string, int>(new Dictionary<string, int>(submittedByEvent));
            this.deduplicatedByEvent = new ReadOnlyDictionary<string, int>(new Dictionary<string, int>(deduplicatedByEvent));
            this.rejectedUnsupportedPlants = rejectedUnsupportedPlants;
            this.targetedInvalidations = targetedInvalidations;
            this.broadInvalidations = broadInvalidations;
            this.speciesSubjectCount = speciesSubjectCount;
            this.cultivarSubjectCount = cultivarSubjectCount;
        }
    }

    internal static class HorticultureKnowledgeEventDiagnostics
    {
        private const int MaxRecentEvents = 1024;
        private static readonly Queue<string> RecentOrder = new Queue<string>();
        private static readonly HashSet<string> Recent = new HashSet<string>(StringComparer.Ordinal);
        private static readonly Dictionary<string, int> Submitted = new Dictionary<string, int>(StringComparer.Ordinal);
        private static readonly Dictionary<string, int> Deduplicated = new Dictionary<string, int>(StringComparer.Ordinal);
        private static Game observedGame;
        private static int rejectedUnsupportedPlants;
        private static int targetedInvalidations;
        private static int broadInvalidations;
        private static int speciesSubjectCount;
        private static int cultivarSubjectCount;

        internal static bool Accept(string eventType, string identity)
        {
            ResetForGameTransition();
            string key = eventType + "|" + identity;
            if (Recent.Contains(key))
            {
                Increment(Deduplicated, eventType);
                return false;
            }
            return true;
        }

        internal static void SubmittedEvent(string eventType, string identity)
        {
            ResetForGameTransition();
            string key = eventType + "|" + identity;
            if (Recent.Add(key))
            {
                RecentOrder.Enqueue(key);
                while (RecentOrder.Count > MaxRecentEvents) Recent.Remove(RecentOrder.Dequeue());
            }
            Increment(Submitted, eventType);
        }

        internal static void UnsupportedPlant() => rejectedUnsupportedPlants++;

        internal static void TargetedInvalidation(int count)
        {
            targetedInvalidations += Math.Max(0, count);
        }

        internal static void BroadInvalidation() => broadInvalidations++;

        internal static void SubjectCounts(int species, int cultivars)
        {
            speciesSubjectCount = Math.Max(0, species);
            cultivarSubjectCount = Math.Max(0, cultivars);
        }

        internal static HorticultureKnowledgeEventDiagnosticsSnapshot Snapshot()
        {
            ResetForGameTransition();
            return new HorticultureKnowledgeEventDiagnosticsSnapshot(Submitted, Deduplicated,
                rejectedUnsupportedPlants, targetedInvalidations, broadInvalidations, speciesSubjectCount,
                cultivarSubjectCount);
        }

        private static void ResetForGameTransition()
        {
            Game current = Current.Game;
            if (ReferenceEquals(current, observedGame)) return;
            observedGame = current;
            Recent.Clear();
            RecentOrder.Clear();
            Submitted.Clear();
            Deduplicated.Clear();
            rejectedUnsupportedPlants = 0;
            targetedInvalidations = 0;
            broadInvalidations = 0;
            speciesSubjectCount = 0;
            cultivarSubjectCount = 0;
        }

        private static void Increment(IDictionary<string, int> values, string key)
        {
            if (key.NullOrEmpty()) key = "unknown";
            values[key] = values.TryGetValue(key, out int count) ? count + 1 : 1;
        }
    }
}
