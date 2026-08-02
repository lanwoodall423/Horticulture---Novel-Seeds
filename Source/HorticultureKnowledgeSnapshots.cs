using System.Collections.Generic;
using KnowledgeFramework;
using Verse;

namespace HorticultureNovelSeeds
{
    /// <summary>Bounds repeated registry queries to the framework's current knowledge revision.</summary>
    public static class HorticultureKnowledgeSnapshots
    {
        private const int MaximumEntries = 4096;
        private static int knowledgeRevision = -1;
        private static int registryRevision = -1;
        private static readonly Dictionary<string, KnowledgeFacetSnapshotV2> Facets = new Dictionary<string, KnowledgeFacetSnapshotV2>();
        private static readonly Dictionary<string, KnowledgeSubjectSnapshotV2> Subjects = new Dictionary<string, KnowledgeSubjectSnapshotV2>();

        public static KnowledgeFacetSnapshotV2 Facet(string domainId, string subjectId, string facetId, Pawn pawn,
            KnowledgeScope scope, KnowledgeContextKey context, KnowledgeContextFallbackMode fallback)
        {
            EnsureRevision();
            string key = domainId + "|" + subjectId + "|" + facetId + "|" + PawnKey(pawn) + "|" + scope + "|" + context;
            if (Facets.TryGetValue(key, out KnowledgeFacetSnapshotV2 snapshot)) return snapshot;
            snapshot = KnowledgeQuery.Facet(domainId, subjectId, facetId, pawn, scope, true, true, context, fallback);
            if (Facets.Count >= MaximumEntries) Facets.Clear();
            Facets[key] = snapshot;
            return snapshot;
        }

        public static KnowledgeSubjectSnapshotV2 Subject(string domainId, string subjectId, Pawn pawn, KnowledgeScope scope)
        {
            EnsureRevision();
            string key = domainId + "|" + subjectId + "|" + PawnKey(pawn) + "|" + scope;
            if (Subjects.TryGetValue(key, out KnowledgeSubjectSnapshotV2 snapshot)) return snapshot;
            snapshot = KnowledgeQuery.Subject(domainId, subjectId, pawn, scope);
            if (Subjects.Count >= MaximumEntries) Subjects.Clear();
            Subjects[key] = snapshot;
            return snapshot;
        }

        public static void Clear()
        {
            Facets.Clear();
            Subjects.Clear();
            knowledgeRevision = -1;
            registryRevision = -1;
        }

        private static void EnsureRevision()
        {
            int currentKnowledgeRevision = KnowledgeQuery.Revision;
            int currentRegistryRevision = KnowledgeRegistry.Revision;
            if (currentKnowledgeRevision == knowledgeRevision && currentRegistryRevision == registryRevision) return;
            Facets.Clear();
            Subjects.Clear();
            knowledgeRevision = currentKnowledgeRevision;
            registryRevision = currentRegistryRevision;
        }

        private static string PawnKey(Pawn pawn) => pawn == null ? "colony" : pawn.thingIDNumber.ToString();
    }
}
