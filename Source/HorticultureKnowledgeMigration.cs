using System;
using System.Collections.Generic;
using System.Linq;
using KnowledgeFramework;
using Verse;

namespace HorticultureNovelSeeds
{
    internal static class HorticultureKnowledgeMigration
    {
        internal static bool RegisterLegacyAlias()
        {
            KnowledgeSchema current = KnowledgeRegistry.Schema(HorticultureKnowledgeContract.DomainId);
            if (current == null) return false;
            KnowledgeSchema legacy = KnowledgeRegistry.Schema(HorticultureKnowledgeContract.LegacyDomainId);
            if (legacy == null)
            {
                KnowledgeDomainRegistration compatibility = HorticultureKnowledgeAdapter.BuildRegistration();
                compatibility.id = HorticultureKnowledgeContract.LegacyDomainId;
                compatibility.source = HorticultureKnowledgeContract.LegacyRegistrationSource;
                KnowledgeConsumerRegistrationResult compatibilityResult = KnowledgeConsumerApi.RegisterDomain(compatibility,
                    new KnowledgeRegistrationOptions
                {
                    source = HorticultureKnowledgeContract.LegacyRegistrationSource,
                    priority = HorticultureKnowledgeContract.RegistrationPriority,
                    conflict = KnowledgeRegistrationConflict.Reject
                    });
                if (compatibilityResult == null || !compatibilityResult.Success) return false;
                if (KnowledgeRegistry.RegisterDomainAlias(HorticultureKnowledgeContract.LegacyDomainId,
                    HorticultureKnowledgeContract.DomainId)) return true;
                KnowledgeRegistry.UnregisterDomain(HorticultureKnowledgeContract.LegacyDomainId,
                    HorticultureKnowledgeContract.LegacyRegistrationSource);
                return false;
            }
            if (legacy.source != HorticultureKnowledgeContract.LegacyRegistrationSource &&
                legacy.source != HorticultureKnowledgeContract.RegistrationSource)
                return false;
            return KnowledgeRegistry.RegisterDomainAlias(HorticultureKnowledgeContract.LegacyDomainId,
                HorticultureKnowledgeContract.DomainId);
        }

        internal static bool TryMigrate(IEnumerable<PlantKnowledgeRecord> records)
        {
            if (!HorticultureKnowledgeRegistration.IsRegistered) return false;
            if (KnowledgeMigrationService.IsCommitted(HorticultureKnowledgeContract.MigrationId,
                HorticultureKnowledgeContract.MigrationVersion)) return true;
            List<PlantKnowledgeRecord> valid = (records ?? Enumerable.Empty<PlantKnowledgeRecord>())
                .Where(value => value != null).ToList();
            if (valid.Any(value => !ValidRecord(value))) return false;
            if (KnowledgeMigrationService.IsCommitted(HorticultureKnowledgeContract.LegacyMigrationId, 1))
                return CommitMarker();
            foreach (PlantKnowledgeRecord record in valid.Where(value => value.pawn != null))
            {
                if (!ImportRecord(record, record.pawn, record.experience, 0f,
                    "pawn:" + record.pawn.thingIDNumber + ":" + record.CropDef.defName)) return false;
            }
            foreach (IGrouping<string, PlantKnowledgeRecord> group in valid.GroupBy(value => value.CropDef.defName))
            {
                ThingDef crop = group.First().CropDef;
                double colonyKnowledge = group.Sum(value => (double)value.experience);
                if (double.IsNaN(colonyKnowledge) || double.IsInfinity(colonyKnowledge) || colonyKnowledge > 100000000d)
                    return false;
                Dictionary<string, int> counts = EventCounts(group);
                if (counts.Values.Any(value => value < 0)) return false;
                if (!ImportRecord(null, null, 0f, (float)colonyKnowledge,
                    "colony:" + crop.defName, crop, counts)) return false;
            }
            return CommitMarker();
        }

        private static bool ImportRecord(PlantKnowledgeRecord record, Pawn pawn, float personal, float colony,
            string key, ThingDef crop = null, IDictionary<string, int> counts = null)
        {
            crop = crop ?? record?.CropDef;
            if (crop == null) return true;
            string consumerId = HorticultureKnowledgeContract.MigrationId + ":" + key;
            if (KnowledgeMigrationService.IsCommitted(consumerId, HorticultureKnowledgeContract.MigrationVersion)) return true;
            return KnowledgeMigrationService.Import(new KnowledgeConsumerMigration
            {
                consumerId = consumerId,
                version = HorticultureKnowledgeContract.MigrationVersion,
                domainId = HorticultureKnowledgeContract.DomainId,
                subjectId = crop.defName,
                pawn = pawn,
                personalKnowledge = personal,
                colonyKnowledge = colony,
                expertise = pawn == null ? 0f : personal,
                eventCounts = counts ?? EventCounts(record)
            });
        }

        private static bool CommitMarker() => KnowledgeMigrationService.Import(new KnowledgeConsumerMigration
        {
            consumerId = HorticultureKnowledgeContract.MigrationId,
            version = HorticultureKnowledgeContract.MigrationVersion
        });

        private static Dictionary<string, int> EventCounts(IEnumerable<PlantKnowledgeRecord> values)
        {
            List<PlantKnowledgeRecord> records = (values ?? Enumerable.Empty<PlantKnowledgeRecord>()).Where(value => value != null).ToList();
            return new Dictionary<string, int>
            {
                { "sowing", BoundedCount(records.Select(value => value.plantsSown)) },
                { "harvesting", BoundedCount(records.Select(value => value.plantsHarvested)) },
                { "cutting", BoundedCount(records.Select(value => value.plantsCut)) },
                { "fertilizing", BoundedCount(records.Select(value => value.plantsFertilized)) },
                { "mutation-discovery", BoundedCount(records.Select(value => value.seedsDiscovered)) },
                { "produce-processing", BoundedCount(records.Select(value => value.recipesCompleted)) }
            };
        }

        private static bool ValidRecord(PlantKnowledgeRecord value)
        {
            return value?.CropDef != null && HorticulturePlantPolicy.IsSupported(value.CropDef) &&
                !float.IsNaN(value.experience) && !float.IsInfinity(value.experience) &&
                value.experience >= 0f && value.experience <= 100000000f &&
                value.plantsSown >= 0 && value.plantsSown <= 100000000 &&
                value.plantsHarvested >= 0 && value.plantsHarvested <= 100000000 &&
                value.plantsCut >= 0 && value.plantsCut <= 100000000 &&
                value.plantsFertilized >= 0 && value.plantsFertilized <= 100000000 &&
                value.seedsDiscovered >= 0 && value.seedsDiscovered <= 100000000 &&
                value.recipesCompleted >= 0 && value.recipesCompleted <= 100000000;
        }

        private static int BoundedCount(IEnumerable<int> values)
        {
            long total = 0;
            foreach (int value in values ?? Enumerable.Empty<int>())
            {
                total += value;
                if (total > 100000000L)
                    return -1;
            }
            return (int)total;
        }

        private static Dictionary<string, int> EventCounts(PlantKnowledgeRecord value) => value == null
            ? new Dictionary<string, int>()
            : EventCounts(new[] { value });
    }
}
