using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace HorticultureNovelSeeds
{
    public sealed class CultivationJournalRecord : CultivationFacts, IExposable
    {
        public string subjectId;
        public void ExposeData()
        {
            Scribe_Values.Look(ref subjectId, "subjectId");
            Scribe_Values.Look(ref firstObservedTick, "firstObservedTick", -1L);
            Scribe_Values.Look(ref firstHarvestTick, "firstHarvestTick", -1L);
            Scribe_Values.Look(ref lastHarvestTick, "lastHarvestTick", -1L);
            Scribe_Values.Look(ref sowings, "sowings");
            Scribe_Values.Look(ref harvests, "harvests");
            Scribe_Values.Look(ref minYield, "minYield", -1);
            Scribe_Values.Look(ref maxYield, "maxYield", -1);
            Scribe_Collections.Look(ref recentEvents, "recentEvents", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit) Normalize();
        }
    }

    public sealed class GameComponent_CultivationJournal : GameComponent
    {
        private List<CultivationJournalRecord> records = new List<CultivationJournalRecord>();
        private Dictionary<string, CultivationJournalRecord> byId = new Dictionary<string, CultivationJournalRecord>(StringComparer.Ordinal);
        public GameComponent_CultivationJournal(Game game) { }
        public static GameComponent_CultivationJournal Instance => Current.Game?.GetComponent<GameComponent_CultivationJournal>();

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref records, "horticultureCultivationJournalV1", LookMode.Deep);
            if (Scribe.mode != LoadSaveMode.PostLoadInit) return;
            records ??= new List<CultivationJournalRecord>();
            byId = new Dictionary<string, CultivationJournalRecord>(StringComparer.Ordinal);
            foreach (var record in records.Where(r => r != null && !string.IsNullOrEmpty(r.subjectId)))
                if (!byId.TryAdd(record.subjectId, record))
                    Log.Warning("[Horticulture] Duplicate journal subject ignored: " + record.subjectId);
            records = byId.Values.ToList();
        }

        public CultivationJournalRecord ForSpecies(ThingDef crop) => FindRecord(SpeciesKey(crop));
        public CultivationJournalRecord ForCultivar(VarietyRecord variety) => FindRecord(CultivarKey(variety));
        public IEnumerable<string> ObservedSpecies => byId.Keys.Where(key => key.StartsWith("species:", StringComparison.Ordinal))
            .Select(key => key.Substring(8));

        internal void Sowing(Pawn pawn, Plant plant)
        {
            if (!Eligible(pawn, plant)) return;
            string identity = HorticultureKnowledgeEventIdentity.Sowing(plant);
            long tick = Find.TickManager?.TicksAbs ?? -1;
            RecordForPlant(plant, r => r.RecordSowing(tick, identity));
        }

        internal void Harvest(Pawn pawn, Plant plant, int actualYield, string identity)
        {
            if (!Eligible(pawn, plant) || actualYield <= 0) return;
            long tick = Find.TickManager?.TicksAbs ?? -1;
            RecordForPlant(plant, r => r.RecordHarvest(tick, identity, actualYield));
        }

        internal void Document(Pawn pawn, VarietyRecord variety)
        {
            if (!Player(pawn) || !HorticulturePlantPolicy.IsSupported(variety?.cropDef) || variety.hiddenFromMenus) return;
            long tick = Find.TickManager?.TicksAbs ?? -1;
            string identity = HorticultureKnowledgeEventIdentity.Documentation(variety);
            GetOrCreate(SpeciesKey(variety.cropDef))?.RecordDiscovery(tick, identity);
            GetOrCreate(CultivarKey(variety))?.RecordDiscovery(tick, identity);
        }

        private void RecordForPlant(Plant plant, Func<CultivationJournalRecord, bool> record)
        {
            var species = GetOrCreate(SpeciesKey(plant.def));
            if (species != null) record(species);
            var comp = plant.TryGetComp<CompPlantVariety>();
            // Never attribute a transient mutation or hidden genotype to a documented cultivar.
            var variety = comp?.PendingDiscovery == false ? comp.Variety : null;
            if (variety == null || variety.hiddenFromMenus || variety.cropDef != plant.def) return;
            var cultivar = GetOrCreate(CultivarKey(variety));
            if (cultivar != null) record(cultivar);
        }

        private CultivationJournalRecord FindRecord(string id) => id != null && byId.TryGetValue(id, out var value) ? value : null;
        private CultivationJournalRecord GetOrCreate(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (byId.TryGetValue(id, out var existing)) return existing;
            var record = new CultivationJournalRecord { subjectId = id };
            records.Add(record);
            byId.Add(id, record);
            return record;
        }
        private static string SpeciesKey(ThingDef crop) => crop == null ? null : "species:" + crop.defName;
        private static string CultivarKey(VarietyRecord variety) => string.IsNullOrEmpty(variety?.id) ? null : "cultivar:" + variety.id;
        private static bool Player(Pawn pawn) => pawn?.Faction?.IsPlayer == true && pawn.RaceProps?.Humanlike == true;
        private static bool Eligible(Pawn pawn, Plant plant) => Player(pawn) && HorticulturePlantPolicy.IsSupported(plant?.def);
    }
}
