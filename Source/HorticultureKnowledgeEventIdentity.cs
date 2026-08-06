using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;

namespace HorticultureNovelSeeds
{
    /// <summary>Creates bounded, semantic identities for integration-boundary observations.</summary>
    public static class HorticultureKnowledgeEventIdentity
    {
        private const int MaxPartLength = 48;
        private const int MaxIdentityLength = 180;

        public static string Sowing(Plant plant) => Build("sow", PlantKey(plant));

        public static string Germination(Plant plant) => Build("germination", PlantKey(plant));

        public static string Growth(Plant plant, int bucket) => Build("growth", PlantKey(plant), bucket.ToString());

        public static string EnvironmentalStress(Plant plant, float temperature, bool cold, bool survived) =>
            Build("stress", PlantKey(plant), temperature.ToString("R", CultureInfo.InvariantCulture),
                cold ? "cold" : "heat", survived ? "survived" : "failed");

        public static string Fertilization(Plant plant, int cycle) => Build("fertilize", PlantKey(plant), cycle.ToString());

        public static string DiseaseSurvival(Plant plant) => Build("disease", PlantKey(plant), plant?.HitPoints.ToString() ?? "0");

        public static string Harvest(Plant plant, int cycle, bool success, bool repeated, bool multiSeason) => Build(
            "harvest", PlantKey(plant), cycle.ToString(), success ? "success" : "failed", repeated ? "repeated" : "first",
            multiSeason ? "multi" : "single");

        public static string Cutting(Plant plant, int cycle) => Build("cutting", PlantKey(plant), cycle.ToString());

        public static string Processing(Pawn worker, IEnumerable<Thing> ingredients)
        {
            IEnumerable<string> parts = (ingredients ?? Enumerable.Empty<Thing>()).Where(value => value != null)
                .Select(value => value.def?.defName + "#" + value.thingIDNumber).OrderBy(value => value, StringComparer.Ordinal);
            return Build("processing", PawnKey(worker), string.Join(",", parts));
        }

        public static string Processing(Pawn worker, IEnumerable<Thing> ingredients, ThingDef crop) => Build(
            "processing", PawnKey(worker), crop?.defName, Processing(worker, ingredients));

        public static string Discovery(ThingDef crop, string origin, IEnumerable<VarietyTraitDef> traits,
            IEnumerable<string> parentIds, string resultId = null)
        {
            return Build("discovery", crop?.defName, origin, resultId,
                string.Join(",", (traits ?? Enumerable.Empty<VarietyTraitDef>()).Where(value => value != null)
                    .Select(value => value.defName).Distinct().OrderBy(value => value, StringComparer.Ordinal)),
                string.Join(",", (parentIds ?? Enumerable.Empty<string>()).Where(value => !value.NullOrEmpty())
                    .Distinct().OrderBy(value => value, StringComparer.Ordinal)));
        }

        public static string Inheritance(ThingDef crop, IEnumerable<string> parentIds, string resultId = null) => Build(
            "inheritance", crop?.defName, resultId,
            string.Join(",", (parentIds ?? Enumerable.Empty<string>()).Where(value => !value.NullOrEmpty())
                .Distinct().OrderBy(value => value, StringComparer.Ordinal)));

        public static string Documentation(VarietyRecord variety) => Build("documentation", variety?.id,
            variety?.cropDef?.defName, variety?.generation.ToString() ?? "0");

        public static string LegacyGain(Pawn pawn, ThingDef plant, string reasonId, int cycle) => Build(
            "legacy", PawnKey(pawn), plant?.defName, reasonId, cycle.ToString());

        public static string Normalize(string prefix, string semanticSource) => Build(prefix, semanticSource);

        private static string PlantKey(Plant plant) => plant?.def?.defName + "#" + (plant?.thingIDNumber ?? 0);

        private static string PawnKey(Pawn pawn) => pawn == null ? "colony" : "pawn#" + pawn.thingIDNumber;

        private static string Build(string prefix, params string[] parts)
        {
            string payload = prefix + "|" + string.Join("|", parts ?? Array.Empty<string>());
            string readable = string.Join(":", (parts ?? Array.Empty<string>()).Where(value => !value.NullOrEmpty())
                .Take(3).Select(SafePart));
            string identity = prefix + ":" + (readable.NullOrEmpty() ? "event" : readable) + ":" + StableHash(payload);
            return identity.Length <= MaxIdentityLength ? identity : identity.Substring(0, MaxIdentityLength);
        }

        private static string SafePart(string value)
        {
            StringBuilder builder = new StringBuilder();
            foreach (char character in value ?? string.Empty)
                if (char.IsLetterOrDigit(character) || character == '_' || character == '-') builder.Append(character);
            string result = builder.ToString();
            return result.Length <= MaxPartLength ? result : result.Substring(0, MaxPartLength);
        }

        private static string StableHash(string value)
        {
            unchecked
            {
                uint hash = 2166136261u;
                foreach (char character in value ?? string.Empty)
                {
                    hash ^= character;
                    hash *= 16777619u;
                }
                return hash.ToString("X8");
            }
        }
    }
}
