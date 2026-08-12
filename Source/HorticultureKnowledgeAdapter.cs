using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using KnowledgeFramework;
using RimWorld;
using UnityEngine;
using Verse;

namespace HorticultureNovelSeeds
{
    public enum HorticultureKnowledgeEvent
    {
        Sowing,
        Germination,
        GrowthStage,
        EnvironmentalStress,
        Fertilization,
        DiseaseSurvival,
        MatureObservation,
        Cutting,
        Harvest,
        HarvestDiscovery,
        FailedHarvest,
        RepeatedHarvest,
        ProduceProcessing,
        WildDiscovery,
        MutationDiscovery,
        CrossPollination,
        TraitInheritance,
        MultiSeasonStability,
        Documentation
    }

    public enum HorticultureKnowledgeRole
    {
        Grower,
        Researcher,
        Cook,
        Observer
    }

    /// <summary>
    /// The only Horticulture integration boundary with Knowledge Framework.
    /// Horticulture supplies observed facts; the framework stores and interprets them.
    /// </summary>
    public static class HorticultureKnowledgeAdapter
    {
        public const string DomainId = HorticultureKnowledgeContract.DomainId;
        public const string LegacyDomainId = HorticultureKnowledgeContract.LegacyDomainId;
        public const string PackageId = HorticultureKnowledgeContract.PackageId;
        public const string ConsumerId = HorticultureKnowledgeContract.ConsumerId;
        public const string RegistrationSource = HorticultureKnowledgeContract.RegistrationSource;
        public const string ProviderId = HorticultureKnowledgeContract.ProviderId;
        public const string MigrationNamespace = HorticultureKnowledgeContract.MigrationNamespace;
        public const string SpeciesArchetype = "horticulture.plant-species";
        public const string CultivarArchetype = "horticulture.cultivar";
        public const string WildVariantArchetype = "horticulture.wild-variant";
        public const string FieldArchetype = "horticulture.experimental-field";
        public const string EnvironmentArchetype = "horticulture.environment";

        public const string FacetIdentity = "identity";
        public const string FacetGrowth = "growth";
        public const string FacetYield = "yield";
        public const string FacetSowing = "sowing";
        public const string FacetHarvesting = "harvesting";
        public const string FacetSoil = "soil-compatibility";
        public const string FacetClimate = "climate-tolerance";
        public const string FacetLifespan = "lifespan";
        public const string FacetTraits = "trait-expression";
        public const string FacetProduce = "produce";
        public const string FacetResilience = "disease-resilience";
        public const string FacetLineage = "lineage";
        public const string FacetEnvironment = "environment";

        public const string StageUnknown = "unknown";
        public const string StageIdentified = "identified";
        public const string StageTrialed = "trialed";
        public const string StageCultivated = "cultivated";
        public const string StageEstablished = "established";
        public const string StageDocumented = "documented";

        public const string ContextZone = "horticulture.growing-zone";
        public const string ContextHydroponic = "horticulture.hydroponic";
        public const string ContextWildSite = "horticulture.wild-site";
        public const string ContextGreenhouse = "horticulture.greenhouse";
        public const string ContextMap = "horticulture.map";
        public const string ContextBiome = "horticulture.biome";
        public const string ContextGlobal = "horticulture.global";

        public const string ExpertiseTrack = "horticulture-fieldcraft";
        public const string ExpertiseNamespace = HorticultureKnowledgeContract.ExpertiseNamespace;
        public static HorticultureKnowledgeRegistrationState RegistrationState => HorticultureKnowledgeRegistration.State;
        public static HorticultureKnowledgeDiagnosticSnapshot Diagnostics => HorticultureKnowledgeRegistration.Diagnostics;
        public static HorticultureKnowledgeEventDiagnosticsSnapshot EventDiagnostics => HorticultureKnowledgeEventDiagnostics.Snapshot();
        internal static bool IsFrameworkUsable => HorticultureKnowledgeRegistration.IsRegistered;
        internal static int KnowledgeRevision => IsFrameworkUsable ? KnowledgeQuery.Revision : -1;
        internal static int RegistryRevision => IsFrameworkUsable ? KnowledgeRegistry.Revision : -1;

        private static readonly string[] Facets =
        {
            FacetIdentity, FacetGrowth, FacetYield, FacetSowing, FacetHarvesting,
            FacetSoil, FacetClimate, FacetLifespan, FacetTraits, FacetProduce,
            FacetResilience, FacetLineage, FacetEnvironment
        };

        public static bool Register() => HorticultureKnowledgeRegistration.EnsureRegistered();

        public static KnowledgeInvalidationResult InvalidateSubjects(IEnumerable<string> subjectIds)
        {
            if (!IsFrameworkUsable) return null;
            List<string> ids = (subjectIds ?? Enumerable.Empty<string>()).Where(value => !value.NullOrEmpty())
                .Distinct(StringComparer.Ordinal).ToList();
            KnowledgeInvalidationResult last = null;
            for (int offset = 0; offset < ids.Count; offset += KnowledgeConsumerApi.MaxTargetedInvalidationSubjects)
            {
                KnowledgeInvalidationResult result;
                try
                {
                    result = KnowledgeConsumerApi.InvalidateSubjects(DomainId,
                        ids.Skip(offset).Take(KnowledgeConsumerApi.MaxTargetedInvalidationSubjects));
                }
                catch (InvalidOperationException)
                {
                    // Knowledge Framework 3.1 can observe its dynamic-subject cache while another
                    // registration invalidates it. Cultivar registration is rare, so recover with
                    // the framework's safe domain-wide invalidation instead of leaking the exception.
                    result = KnowledgeConsumerApi.InvalidateDomain(DomainId);
                }
                if (result == null || !result.Success) return result;
                HorticultureKnowledgeEventDiagnostics.TargetedInvalidation(result.invalidatedCount);
                last = result;
            }
            return last;
        }

        public static string SubjectId(ThingDef plant) => plant?.defName;
        public static string CultivarSubjectId(VarietyRecord variety) => variety?.id.NullOrEmpty() == false ? "cultivar:" + variety.id : null;
        public static string FieldSubjectId(IPlantToGrowSettable grower)
        {
            string key = grower == null ? null : GameComponent_NovelSeeds.GrowerKey(grower);
            return key.NullOrEmpty() ? null : "field:" + key;
        }
        public static string EnvironmentSubjectId(KnowledgeContextKey context) => context.IsEmpty ? null : "environment:" + context;

        public static KnowledgeContextKey ContextFor(Map map, IntVec3 cell = default(IntVec3), IPlantToGrowSettable grower = null, bool wild = false)
        {
            if (map == null) return new KnowledgeContextKey(ContextGlobal, "global");
            if (wild)
            {
                IntVec3 actualWildCell = cell.IsValid ? cell : map.Center;
                return new KnowledgeContextKey(ContextWildSite, map.uniqueID + ":" + actualWildCell.x + ":" + actualWildCell.z);
            }
            if (grower != null)
            {
                string growerKey = GameComponent_NovelSeeds.GrowerKey(grower) ?? "map";
                string stable = map.uniqueID + ":" + growerKey;
                string typeName = grower.GetType().FullName ?? string.Empty;
                if (typeName.IndexOf("hydro", StringComparison.OrdinalIgnoreCase) >= 0)
                    return new KnowledgeContextKey(ContextHydroponic, stable);
                if (grower is Zone zone && zone.Cells != null && zone.Cells.Any(value => value.Roofed(map)))
                    return new KnowledgeContextKey(ContextGreenhouse, stable);
                return new KnowledgeContextKey(ContextZone, stable);
            }
            return MapContext(map);
        }

        public static KnowledgeContextKey MapContext(Map map) => map == null
            ? new KnowledgeContextKey(ContextGlobal, "global")
            : new KnowledgeContextKey(ContextMap, map.uniqueID.ToString());

        public static KnowledgeContextKey BiomeContext(BiomeDef biome) => biome == null
            ? new KnowledgeContextKey(ContextGlobal, "global")
            : new KnowledgeContextKey(ContextBiome, biome.defName);

        public static KnowledgeFacetSnapshotV2 Facet(Pawn pawn, ThingDef plant, string facetId = FacetIdentity,
            Map map = null, bool colony = false, KnowledgeContextKey context = default(KnowledgeContextKey))
        {
            if (!Register()) return null;
            KnowledgeContextKey requested = context.IsEmpty ? (map == null ? KnowledgeContextKey.Empty : ContextFor(map)) : context;
            return HorticultureKnowledgeSnapshots.Facet(DomainId, SubjectId(plant), facetId, pawn,
                colony ? KnowledgeScope.Colony : KnowledgeScope.Personal, requested,
                KnowledgeContextFallbackMode.ParentThenGlobal);
        }

        public static KnowledgeClaimSnapshot Claim(Pawn pawn, ThingDef plant, string facetId, string claimId,
            KnowledgeContextKey context = default(KnowledgeContextKey), bool colony = false)
        {
            if (!Register()) return null;
            return KnowledgeClaimService.Snapshot(DomainId, SubjectId(plant), facetId, claimId, pawn,
                colony ? KnowledgeScope.Colony : KnowledgeScope.Personal, context,
                KnowledgeContextFallbackMode.ParentThenGlobal);
        }

        public static KnowledgeSubjectSnapshotV2 SubjectSnapshot(Pawn pawn, ThingDef plant, bool colony)
        {
            if (plant == null || !Register()) return null;
            return HorticultureKnowledgeSnapshots.Subject(DomainId, SubjectId(plant), pawn,
                colony ? KnowledgeScope.Colony : KnowledgeScope.Personal);
        }

        public static KnowledgeExpertiseSnapshotV2 ExpertiseSnapshot(Pawn pawn)
        {
            if (!IsPlayerColonist(pawn) || !Register()) return null;
            return KnowledgeQuery.Expertise(DomainId, pawn, ExpertiseTrack);
        }

        public static IReadOnlyList<KnowledgeMilestoneState> Milestones(Pawn pawn, ThingDef plant)
        {
            if (plant == null || !Register()) return Array.Empty<KnowledgeMilestoneState>();
            return KnowledgeQuery.Milestones(DomainId, plant.defName, pawn);
        }

        public static string StageFor(ThingDef plant, Pawn pawn, bool colony, KnowledgeContextKey context = default(KnowledgeContextKey))
        {
            if (plant == null) return StageUnknown;
            if (!Register()) return StageUnknown;
            KnowledgeSubjectSnapshotV2 state = HorticultureKnowledgeSnapshots.Subject(DomainId, SubjectId(plant), pawn,
                colony ? KnowledgeScope.Colony : KnowledgeScope.Personal);
            return state?.stageId.NullOrEmpty() != false ? StageUnknown : state.stageId;
        }

        public static string CultivarStageFor(VarietyRecord variety, Pawn pawn, bool colony)
        {
            string subjectId = CultivarSubjectId(variety);
            if (subjectId.NullOrEmpty()) return StageUnknown;
            if (!Register()) return StageUnknown;
            KnowledgeSubjectSnapshotV2 state = HorticultureKnowledgeSnapshots.Subject(DomainId, subjectId, pawn,
                colony ? KnowledgeScope.Colony : KnowledgeScope.Personal);
            return state?.stageId.NullOrEmpty() != false ? StageUnknown : state.stageId;
        }

        public static string StageLabel(string stageId)
        {
            switch (stageId)
            {
                case StageIdentified: return "Identified";
                case StageTrialed: return "Trialed";
                case StageCultivated: return "Cultivated";
                case StageEstablished: return "Established";
                case StageDocumented: return "Documented";
                default: return "Unknown";
            }
        }

        public static int StageOrder(string stageId)
        {
            switch (stageId)
            {
                case StageDocumented: return 5;
                case StageEstablished: return 4;
                case StageCultivated: return 3;
                case StageTrialed: return 2;
                case StageIdentified: return 1;
                default: return 0;
            }
        }

        public static KnowledgeRank TierFor(ThingDef plant, Pawn pawn, bool colony)
        {
            int order = StageOrder(StageFor(plant, pawn, colony));
            return order >= 5 ? KnowledgeRank.Master : order >= 4 ? KnowledgeRank.Expert :
                order >= 2 ? KnowledgeRank.Adept : KnowledgeRank.Novice;
        }

        public static KnowledgeRank CultivarTierFor(VarietyRecord variety, Pawn pawn, bool colony)
        {
            int order = StageOrder(CultivarStageFor(variety, pawn, colony));
            return order >= 5 ? KnowledgeRank.Master : order >= 4 ? KnowledgeRank.Expert :
                order >= 2 ? KnowledgeRank.Adept : KnowledgeRank.Novice;
        }

        public static KnowledgeRank ExpertiseRank(Pawn pawn) => IsPlayerColonist(pawn) && Register()
            ? KnowledgeQuery.Expertise(DomainId, pawn, ExpertiseTrack).rank : KnowledgeRank.Novice;

        public static float ExpertiseProgress(Pawn pawn) => IsPlayerColonist(pawn) && Register()
            ? KnowledgeQuery.Expertise(DomainId, pawn, ExpertiseTrack).progress : 0f;

        public static float PersonalKnowledge(Pawn pawn, ThingDef plant, string facetId = FacetIdentity) =>
            pawn == null || plant == null ? 0f : Facet(pawn, plant, facetId)?.amount ?? 0f;

        public static float ColonyKnowledge(ThingDef plant, string facetId = FacetIdentity) =>
            plant == null ? 0f : Facet(null, plant, facetId, null, true)?.amount ?? 0f;

        public static float PlantWorkSpeedFactor(Pawn pawn, ThingDef plant)
        {
            if (!IsPlayerColonist(pawn) || plant == null || !Register()) return 1f;
            KnowledgeRank rank = ExpertiseRank(pawn);
            return 1f + Mathf.Clamp((int)rank * 0.03f, 0f, 0.12f);
        }

        public static float ProduceWorkSpeedFactor(Pawn pawn, IEnumerable<Thing> ingredients)
        {
            if (!IsPlayerColonist(pawn) || ingredients == null) return 1f;
            float best = 1f;
            foreach (ThingDef crop in ingredients.Select(item => item?.TryGetComp<CompNovelProduceAppearance>()?.SourcePlantDef)
                .Where(item => item != null).Distinct())
                best = Mathf.Max(best, PlantWorkSpeedFactor(pawn, crop));
            return best;
        }

        public static bool Observe(Pawn observer, ThingDef plant, HorticultureKnowledgeEvent eventKind,
            Map map = null, bool success = true, float quality = 1f, string summary = null,
            string sourceInstanceId = null, HorticultureKnowledgeRole role = HorticultureKnowledgeRole.Grower,
            IReadOnlyList<KnowledgeMeasurement> measurements = null, KnowledgeContextKey context = default(KnowledgeContextKey),
            IReadOnlyList<Pawn> witnesses = null, bool wild = false, float directKnowledge = 0f)
        {
            return ObserveSubject(observer, plant, SubjectId(plant), eventKind, map, success, quality, summary,
                sourceInstanceId, role, measurements, context, witnesses, wild, directKnowledge);
        }

        internal static bool RecordLegacyGain(Pawn observer, ThingDef plant, float amount, string reasonId)
        {
            HorticultureKnowledgeEvent eventKind = reasonId == "sowing" ? HorticultureKnowledgeEvent.Sowing :
                reasonId == "harvesting" ? HorticultureKnowledgeEvent.Harvest :
                reasonId == "cutting" ? HorticultureKnowledgeEvent.MatureObservation :
                reasonId == "fertilizing" ? HorticultureKnowledgeEvent.Fertilization :
                reasonId == "discovery" ? HorticultureKnowledgeEvent.WildDiscovery :
                HorticultureKnowledgeEvent.ProduceProcessing;
            return Observe(observer, plant, eventKind, success: true, quality: 1f,
                summary: "Legacy Horticulture knowledge record", sourceInstanceId: HorticultureKnowledgeEventIdentity.LegacyGain(
                    observer, plant, reasonId, 0),
                directKnowledge: amount);
        }

        public static bool ObserveCultivar(Pawn observer, VarietyRecord variety, HorticultureKnowledgeEvent eventKind,
            Map map = null, bool success = true, float quality = 1f, string summary = null,
            string sourceInstanceId = null, HorticultureKnowledgeRole role = HorticultureKnowledgeRole.Grower,
            IReadOnlyList<KnowledgeMeasurement> measurements = null, KnowledgeContextKey context = default(KnowledgeContextKey),
            IReadOnlyList<Pawn> witnesses = null, bool wild = false, float directKnowledge = 0f)
        {
            return variety?.cropDef == null ? false : ObserveSubject(observer, variety.cropDef, CultivarSubjectId(variety),
                eventKind, map, success, quality, summary, sourceInstanceId ?? HorticultureKnowledgeEventIdentity.Normalize(
                    "cultivar", variety.id + ":" + RecipeId(eventKind)), role, measurements, context, witnesses, wild,
                directKnowledge);
        }

        private static bool ObserveSubject(Pawn observer, ThingDef plant, string subjectId, HorticultureKnowledgeEvent eventKind,
            Map map, bool success, float quality, string summary, string sourceInstanceId, HorticultureKnowledgeRole role,
            IReadOnlyList<KnowledgeMeasurement> measurements, KnowledgeContextKey context, IReadOnlyList<Pawn> witnesses,
            bool wild, float directKnowledge)
        {
            if (plant == null || subjectId.NullOrEmpty()) return false;
            if (!HorticulturePlantPolicy.IsSupported(plant))
            {
                HorticultureKnowledgeEventDiagnostics.UnsupportedPlant();
                return false;
            }
            if (!Register()) return false;
            string recipe = RecipeId(eventKind);
            KnowledgeContextKey resolvedContext = context.IsEmpty ? ContextFor(map, IntVec3.Invalid, null, wild) : context;
            string instance = HorticultureKnowledgeEventIdentity.Normalize(recipe,
                sourceInstanceId ?? InstanceId(observer, plant, recipe));
            if (!HorticultureKnowledgeEventDiagnostics.Accept(eventKind.ToString(), instance)) return true;
            Dictionary<string, float> facetWeights = FacetWeights(eventKind, role);
            KnowledgeTransaction transaction = new KnowledgeTransaction
            {
                source = "Horticulture",
                transactionId = instance,
                notify = false
            };
            foreach (KeyValuePair<string, float> weightedFacet in facetWeights)
            {
                List<KnowledgeMeasurement> facetMeasurements = (measurements ?? Array.Empty<KnowledgeMeasurement>())
                    .Where(value => value != null && (value.facetId == weightedFacet.Key || value.facetId.NullOrEmpty() &&
                        KnowledgeRegistry.Schema(DomainId)?.Claim(value.claimId)?.facetId == weightedFacet.Key))
                    .Select(value => value.Clone()).ToList();
                if (observer != null)
                    transaction.Add(NewObservation(observer, plant, subjectId, recipe, weightedFacet.Key, instance,
                        weightedFacet.Value, success, quality, summary, resolvedContext, role, facetMeasurements, witnesses,
                        directKnowledge));
                transaction.Add(NewColonyObservation(plant, subjectId, recipe, weightedFacet.Key, instance,
                    weightedFacet.Value, success, quality, summary, resolvedContext, facetMeasurements, directKnowledge));
            }
            KnowledgeTransactionResult result = KnowledgeEngine.Submit(transaction);
            if (!result.success) return false;
            HorticultureKnowledgeEventDiagnostics.SubmittedEvent(eventKind.ToString(), instance);
            ReportMilestones(observer, subjectId, eventKind, resolvedContext);
            return true;
        }

        public static void RegisterCultivar(VarietyRecord variety)
        {
            if (variety?.cropDef == null) return;
            if (!HorticulturePlantPolicy.IsSupported(variety.cropDef))
            {
                HorticultureKnowledgeEventDiagnostics.UnsupportedPlant();
                return;
            }
            if (!Register()) return;
            string subjectId = CultivarSubjectId(variety);
            if (subjectId.NullOrEmpty()) return;
            List<string> affectedSubjects = new List<string> { subjectId, SubjectId(variety.cropDef) };
            if (variety.parentVarietyIds != null)
                foreach (string parentId in variety.parentVarietyIds.Where(value => !value.NullOrEmpty()))
                {
                    VarietyRecord parent = GameComponent_NovelSeeds.Instance?.GetVariety(parentId);
                    string parentSubject = CultivarSubjectId(parent);
                    if (!parentSubject.NullOrEmpty()) affectedSubjects.Add(parentSubject);
                }
            KnowledgeInvalidationResult invalidation = InvalidateSubjects(affectedSubjects.Distinct().ToList());
            if (invalidation == null || !invalidation.Success) return;
            string originType = variety.originKind.NullOrEmpty() ? "mutation" : variety.originKind;
            if (variety.parentVarietyIds != null)
                foreach (string parentId in variety.parentVarietyIds.Where(value => !value.NullOrEmpty()))
                    if (!AddRelation(CultivarSubjectId(GameComponent_NovelSeeds.Instance?.GetVariety(parentId)), subjectId,
                        parentId == variety.parentVarietyIds.FirstOrDefault() ? "seed-parent" : "pollen-parent", variety)) return;
            string originSubject = SubjectId(variety.cropDef);
            string relationType = originType == "wild" ? "wild-origin" : originType == "cross-pollination" ? "cross-origin" : "mutation-origin";
            if (!AddRelation(originSubject, subjectId, relationType, variety)) return;
            HorticultureKnowledgeEventDiagnostics.SubjectCounts(
                DefDatabase<ThingDef>.AllDefsListForReading.Count(HorticulturePlantPolicy.IsSupported),
                GameComponent_NovelSeeds.Instance?.AllVarieties?.Count() ?? 0);
        }

        public static KnowledgeStructuredComparisonSnapshot CompareCultivars(IEnumerable<VarietyRecord> varieties, Pawn pawn = null, bool colony = false)
        {
            if (!Register() || !HorticultureKnowledgeCompatibility.HasOptional(KnowledgeFrameworkApi.StructuredComparisonCapability)) return null;
            List<string> ids = (varieties ?? Enumerable.Empty<VarietyRecord>()).Where(value => value != null)
                .Select(CultivarSubjectId).Where(value => !value.NullOrEmpty()).Distinct().Take(8).ToList();
            return KnowledgeComparisonService.CompareMany(DomainId, ids, pawn,
                colony ? KnowledgeScope.Colony : KnowledgeScope.Personal);
        }

        public static KnowledgeMenuModel Menu(Pawn pawn, bool colony)
        {
            if (!Register()) return UnavailableMenu();
            KnowledgeMenuSection section = new KnowledgeMenuSection
            {
                id = DomainId,
                label = "Plant knowledge",
                emptyText = "No horticulture evidence yet. Sow, observe, harvest, and preserve a variety."
            };
            foreach (ThingDef plant in DefDatabase<ThingDef>.AllDefsListForReading.Where(NovelSeedUtility.IsGrowableCrop)
                .OrderBy(value => value.label))
            {
                KnowledgeFacetSnapshotV2 identity = Facet(pawn, plant, FacetIdentity, null, colony);
                string stage = StageFor(plant, pawn, colony);
                if (identity == null || identity.amount <= 0f && stage == StageUnknown) continue;
                section.rows.Add(new KnowledgeMenuRow
                {
                    label = plant.LabelCap.ToString(),
                    iconDef = plant,
                    subjectId = SubjectId(plant),
                    rank = TierFor(plant, pawn, colony),
                    progress = Mathf.Clamp01(identity?.completeness ?? 0f),
                    confidence = identity?.confidence ?? 0f,
                    stageId = stage,
                    status = StageLabel(stage) + " - " + (identity?.confidence ?? 0f).ToStringPercent() + " confidence",
                    tooltip = plant.LabelCap + "\n" + StageLabel(stage) + "\nEvidence: " + (identity?.evidenceCount ?? 0) +
                        "\nContext: " + (identity == null ? "Unavailable" : ContextDescription(identity)),
                    select = () => MainTabWindow_CultivarRegistry.OpenPlant(plant)
                });
            }
            KnowledgeExpertiseSnapshotV2 expertise = KnowledgeQuery.Expertise(DomainId, pawn, ExpertiseTrack);
            return new KnowledgeMenuModel
            {
                title = colony ? "Colony Horticulture Knowledge" : (pawn?.LabelShortCap ?? "Colonist") + " - Horticulture",
                expertiseLabel = "Horticulture expertise",
                expertiseRank = IsPlayerColonist(pawn) ? expertise.rank : KnowledgeRank.Novice,
                expertiseProgress = IsPlayerColonist(pawn) ? expertise.progress : 0f,
                sections = new List<KnowledgeMenuSection> { section }
            };
        }

        public static KnowledgeEntry BioEntry(Pawn pawn)
        {
            if (pawn == null || !Register()) return null;
            int known = KnowledgeQuery.PersonalFacets(DomainId, pawn).Count(value => value.amount > 0f);
            KnowledgeExpertiseSnapshotV2 expertise = KnowledgeQuery.Expertise(DomainId, pawn, ExpertiseTrack);
            if (known == 0 && expertise.amount <= 0f) return null;
            return new KnowledgeEntry
            {
                label = "Horticulture",
                rank = IsPlayerColonist(pawn) ? expertise.rank : KnowledgeRank.Novice,
                progress = IsPlayerColonist(pawn) ? expertise.progress : 0f,
                summary = known + " plant records",
                tooltip = "Horticulture knowledge records observations, uncertainty, contexts, and preserved cultivars.\n" +
                    "Expertise improves sowing, harvesting, and produce-processing work.",
                openDetails = () => MainTabWindow_CultivarRegistry.OpenKnowledge(pawn)
            };
        }

        public static bool TryMigrateLegacy(IEnumerable<PlantKnowledgeRecord> records) =>
            HorticultureKnowledgeMigration.TryMigrate(records);

        private static KnowledgeObservation NewObservation(Pawn observer, ThingDef plant, string subjectId, string recipe, string facet,
            string instance, float weight, bool success, float quality, string summary, KnowledgeContextKey context,
            HorticultureKnowledgeRole role, IReadOnlyList<KnowledgeMeasurement> measurements, IReadOnlyList<Pawn> witnesses,
            float directKnowledge)
        {
            bool expert = IsPlayerColonist(observer);
            return new KnowledgeObservation
            {
                observer = observer,
                domainId = DomainId,
                subjectId = subjectId,
                facetId = facet,
                observationId = recipe,
                methodId = recipe,
                directKnowledge = Mathf.Max(0f, directKnowledge > 0f ? directKnowledge * weight : weight * 5f),
                directExpertise = expert ? ExpertiseFor(role, weight) : 0f,
                directFamiliarity = weight,
                expertiseTrackId = ExpertiseTrack,
                quality = Mathf.Clamp(quality, 0.05f, 8f),
                success = success,
                disposition = success ? KnowledgeEvidenceDisposition.Supporting : KnowledgeEvidenceDisposition.Contradictory,
                witnesses = witnesses,
                witnessDistribution = new KnowledgeWitnessDistribution
                {
                    policy = KnowledgeWitnessDistributionPolicy.WitnessesReduced,
                    efficiency = 0.55f,
                    expertiseEfficiency = 0.45f,
                    confidenceEfficiency = 0.75f,
                    maximumRecipients = 8
                },
                source = "Horticulture",
                sourceInstanceId = instance + ":" + facet,
                reasonId = recipe,
                context = context,
                summary = summary,
                claimMeasurements = measurements,
                documented = recipe == "documentation",
                notify = false
            };
        }

        private static IReadOnlyList<KnowledgeMeasurement> ColonyMeasurements(IReadOnlyList<KnowledgeMeasurement> measurements,
            KnowledgeContextKey context)
        {
            return (measurements ?? Array.Empty<KnowledgeMeasurement>()).Select(measurement =>
            {
                KnowledgeMeasurement copy = measurement.Clone();
                copy.observer = null;
                copy.scope = KnowledgeScope.Colony;
                copy.context = context;
                return copy;
            }).ToList();
        }

        private static KnowledgeObservation NewColonyObservation(ThingDef plant, string subjectId, string recipe, string facet,
            string instance, float weight, bool success, float quality, string summary, KnowledgeContextKey context,
            IReadOnlyList<KnowledgeMeasurement> measurements, float directKnowledge)
        {
            return new KnowledgeObservation
            {
                domainId = DomainId,
                subjectId = subjectId,
                facetId = facet,
                observationId = recipe,
                methodId = recipe + ":colony",
                directKnowledge = Mathf.Max(0f, directKnowledge > 0f ? directKnowledge * weight : weight * 4f),
                directFamiliarity = weight,
                targetColony = true,
                quality = Mathf.Clamp(quality * 0.9f, 0.05f, 8f),
                success = success,
                disposition = success ? KnowledgeEvidenceDisposition.Supporting : KnowledgeEvidenceDisposition.Contradictory,
                source = "Horticulture",
                sourceInstanceId = instance + ":colony:" + facet,
                reasonId = recipe,
                context = context,
                summary = summary,
                claimMeasurements = ColonyMeasurements(measurements, context),
                documented = recipe == "documentation",
                notify = false
            };
        }

        private static float ExpertiseFor(HorticultureKnowledgeRole role, float weight)
        {
            float roleFactor = role == HorticultureKnowledgeRole.Researcher ? 1.25f :
                role == HorticultureKnowledgeRole.Cook ? 0.85f : role == HorticultureKnowledgeRole.Observer ? 0.5f : 1f;
            return Mathf.Max(0f, weight * 1.5f * roleFactor);
        }

        private static Dictionary<string, float> FacetWeights(HorticultureKnowledgeEvent eventKind, HorticultureKnowledgeRole role)
        {
            string[] selected;
            switch (eventKind)
            {
                case HorticultureKnowledgeEvent.Sowing: selected = new[] { FacetIdentity, FacetSowing, FacetGrowth, FacetSoil }; break;
                case HorticultureKnowledgeEvent.Germination: selected = new[] { FacetIdentity, FacetGrowth, FacetEnvironment }; break;
                case HorticultureKnowledgeEvent.GrowthStage: selected = new[] { FacetGrowth, FacetEnvironment }; break;
                case HorticultureKnowledgeEvent.EnvironmentalStress: selected = new[] { FacetClimate, FacetResilience, FacetGrowth }; break;
                case HorticultureKnowledgeEvent.Fertilization: selected = new[] { FacetSoil, FacetGrowth }; break;
                case HorticultureKnowledgeEvent.DiseaseSurvival: selected = new[] { FacetResilience, FacetGrowth }; break;
                case HorticultureKnowledgeEvent.MatureObservation:
                case HorticultureKnowledgeEvent.Cutting: selected = new[] { FacetIdentity, FacetGrowth, FacetTraits, FacetClimate, FacetProduce }; break;
                case HorticultureKnowledgeEvent.Harvest:
                case HorticultureKnowledgeEvent.RepeatedHarvest: selected = new[] { FacetYield, FacetHarvesting, FacetProduce, FacetLifespan }; break;
                case HorticultureKnowledgeEvent.HarvestDiscovery: selected = new[] { FacetIdentity, FacetYield, FacetHarvesting, FacetProduce, FacetLifespan, FacetTraits, FacetLineage }; break;
                case HorticultureKnowledgeEvent.FailedHarvest: selected = new[] { FacetYield, FacetHarvesting, FacetResilience }; break;
                case HorticultureKnowledgeEvent.ProduceProcessing: selected = new[] { FacetProduce, FacetTraits }; break;
                case HorticultureKnowledgeEvent.WildDiscovery: selected = new[] { FacetIdentity, FacetEnvironment, FacetTraits, FacetLineage }; break;
                case HorticultureKnowledgeEvent.MutationDiscovery: selected = new[] { FacetIdentity, FacetTraits, FacetProduce, FacetLineage }; break;
                case HorticultureKnowledgeEvent.CrossPollination:
                case HorticultureKnowledgeEvent.TraitInheritance: selected = new[] { FacetTraits, FacetLineage, FacetProduce }; break;
                case HorticultureKnowledgeEvent.MultiSeasonStability: selected = new[] { FacetYield, FacetClimate, FacetResilience, FacetLifespan }; break;
                case HorticultureKnowledgeEvent.Documentation: selected = Facets; break;
                default: selected = new[] { FacetIdentity }; break;
            }
            float roleFactor = role == HorticultureKnowledgeRole.Researcher && (eventKind == HorticultureKnowledgeEvent.MutationDiscovery ||
                eventKind == HorticultureKnowledgeEvent.CrossPollination || eventKind == HorticultureKnowledgeEvent.TraitInheritance) ? 1.35f : 1f;
            return selected.Distinct().ToDictionary(value => value, _ => roleFactor);
        }

        private static string RecipeId(HorticultureKnowledgeEvent value) => value switch
        {
            HorticultureKnowledgeEvent.Sowing => "sowing",
            HorticultureKnowledgeEvent.Fertilization => "fertilizing",
            HorticultureKnowledgeEvent.Harvest => "harvesting",
            HorticultureKnowledgeEvent.HarvestDiscovery => "harvest-discovery",
            HorticultureKnowledgeEvent.ProduceProcessing => "produce-processing",
            HorticultureKnowledgeEvent.WildDiscovery => "wild-discovery",
            HorticultureKnowledgeEvent.MutationDiscovery => "mutation-discovery",
            HorticultureKnowledgeEvent.CrossPollination => "cross-pollination",
            HorticultureKnowledgeEvent.TraitInheritance => "trait-inheritance",
            HorticultureKnowledgeEvent.FailedHarvest => "failed-harvest",
            HorticultureKnowledgeEvent.RepeatedHarvest => "repeated-harvest",
            HorticultureKnowledgeEvent.EnvironmentalStress => "environmental-stress",
            HorticultureKnowledgeEvent.DiseaseSurvival => "disease-survival",
            HorticultureKnowledgeEvent.GrowthStage => "growth-stage",
            HorticultureKnowledgeEvent.MatureObservation => "mature-observation",
            HorticultureKnowledgeEvent.Cutting => "cutting",
            HorticultureKnowledgeEvent.MultiSeasonStability => "multi-season-stability",
            HorticultureKnowledgeEvent.Documentation => "documentation",
            _ => value.ToString().ToLowerInvariant()
        };

        private static void ReportMilestones(Pawn observer, string subjectId, HorticultureKnowledgeEvent eventKind, KnowledgeContextKey context)
        {
            string milestone = eventKind == HorticultureKnowledgeEvent.Germination ? "first-germination" :
                eventKind == HorticultureKnowledgeEvent.Harvest || eventKind == HorticultureKnowledgeEvent.HarvestDiscovery ? "first-harvest" :
                eventKind == HorticultureKnowledgeEvent.RepeatedHarvest ? "stable-yield" :
                eventKind == HorticultureKnowledgeEvent.Documentation ? "documented-record" : null;
            if (milestone.NullOrEmpty()) return;
            KnowledgeMilestoneService.Confirm(DomainId, subjectId, "cultivation", milestone,
                IsPlayerColonist(observer) ? observer : null, context);
        }

        private static bool AddRelation(string from, string to, string role, VarietyRecord variety)
        {
            if (from.NullOrEmpty() || to.NullOrEmpty() || from == to) return true;
            if (!IsFrameworkUsable) return false;
            return KnowledgeRelationService.Add(new KnowledgeSubjectRelation
            {
                domainId = DomainId,
                fromSubjectId = from,
                toDomainId = DomainId,
                toSubjectId = to,
                relationTypeId = role == "mutation-origin" ? "mutation-origin" : role == "wild-origin" ? "wild-origin" :
                    role == "cross-origin" ? "cross-pollination" : "parent-of",
                role = role,
                order = variety?.generation ?? 0,
                revealed = true,
                confidence = 1f,
                source = "Horticulture cultivar registry",
                tick = Find.TickManager?.TicksGame ?? 0,
                metadata = new Dictionary<string, string>
                {
                    { "cultivar", variety?.id ?? string.Empty },
                    { "origin", variety?.originKind ?? "mutation" }
                }
            });
        }

        internal static KnowledgeDomainRegistration BuildRegistration()
        {
            List<KnowledgeFacetDef> facetDefs = Facets.Select((id, index) => new KnowledgeFacetDef
            {
                defName = "HNS_KnowledgeFacet_" + index,
                stableId = id,
                label = FacetLabel(id),
                description = "Evidence about " + FacetLabel(id).ToLowerInvariant() + ".",
                completenessAmount = id == FacetIdentity ? 50f : 100f,
                personallyKnowable = true,
                documentable = true,
                shareable = true,
                approximateWhenUncertain = true
            }).ToList();
            List<KnowledgeClaimDef> claims = BuildClaims();
            return new KnowledgeDomainRegistration
            {
                id = DomainId,
                label = "Horticulture",
                description = "Cultivate, observe, discover, preserve, compare, and specialize.",
                enableUncertainty = true,
                enableFamiliarity = true,
                sharingModel = KnowledgeSharingModel.Reportable,
                sortOrder = 30,
                provenanceLimit = 24,
                evidenceAggregateLimit = 160,
                facets = facetDefs,
                stages = BuildStages(),
                expertiseTracks = new[] { new KnowledgeExpertiseTrackDef
                {
                    defName = "HNS_Fieldcraft", stableId = ExpertiseTrack, label = "Horticulture expertise",
                    adept = 100f, expert = 300f, master = 700f
                } },
                observations = BuildObservations(),
                claims = claims,
                archetypes = new[]
                {
                    Archetype(SpeciesArchetype, Facets, claims),
                    Archetype(CultivarArchetype, Facets, claims),
                    Archetype(WildVariantArchetype, new[] { FacetIdentity, FacetEnvironment, FacetTraits, FacetLineage }, claims),
                    Archetype(FieldArchetype, new[] { FacetEnvironment, FacetSoil, FacetClimate, FacetYield, FacetResilience }, claims),
                    Archetype(EnvironmentArchetype, new[] { FacetEnvironment, FacetSoil, FacetClimate }, claims)
                },
                milestoneTracks = new[] { BuildMilestoneTrack() },
                expertiseNamespaces = new[] { new KnowledgeExpertiseNamespaceDef
                {
                    defName = "HNS_FieldcraftNamespace", stableId = ExpertiseNamespace, label = "Horticulture fieldcraft",
                    adept = 100f, expert = 300f, master = 700f
                } },
                subjectResolver = ResolveSubject,
                subjectSource = SubjectSource,
                source = RegistrationSource
            };
        }

        private static List<KnowledgeStageDef> BuildStages() => new List<KnowledgeStageDef>
        {
            Stage(StageUnknown, "Unknown", 0, 0f, 0f, false, null),
            Stage(StageIdentified, "Identified", 1, 1f, 0.10f, false,
                All(Requirement(KnowledgeRequirementKind.EvidenceCount, FacetIdentity, null, 1f))),
            Stage(StageTrialed, "Trialed", 2, 12f, 0.25f, false, All(
                Requirement(KnowledgeRequirementKind.EventCount, FacetGrowth, "germination", 1f),
                Requirement(KnowledgeRequirementKind.EventCount, FacetGrowth, "growth-stage", 1f))),
            Stage(StageCultivated, "Cultivated", 3, 28f, 0.40f, false,
                All(Requirement(KnowledgeRequirementKind.EventCount, FacetYield, "harvesting", 1f))),
            Stage(StageEstablished, "Established", 4, 48f, 0.55f, false, Any(
                Requirement(KnowledgeRequirementKind.EventCount, FacetYield, "repeated-harvest", 2f),
                Requirement(KnowledgeRequirementKind.EventCount, FacetYield, "multi-season-stability", 1f))),
            Stage(StageDocumented, "Documented", 5, 72f, 0.65f, true, All(
                Requirement(KnowledgeRequirementKind.Documentation, FacetIdentity, null, 0f),
                Requirement(KnowledgeRequirementKind.EvidenceCount, FacetIdentity, null, 5f)))
        };

        private static KnowledgeStageDef Stage(string id, string label, int order, float knowledge, float confidence,
            bool documented, KnowledgeRequirementGroup requirements) => new KnowledgeStageDef
        {
            defName = id, label = label, order = order, minimumKnowledge = knowledge, minimumConfidence = confidence,
            documented = documented, allowRegression = false, requirementGroup = requirements
        };

        private static KnowledgeRequirementGroup All(params KnowledgeRequirement[] requirements) => new KnowledgeRequirementGroup
        {
            mode = KnowledgeRequirementGroupMode.All,
            requirements = requirements.ToList()
        };

        private static KnowledgeRequirementGroup Any(params KnowledgeRequirement[] requirements) => new KnowledgeRequirementGroup
        {
            mode = KnowledgeRequirementGroupMode.Any,
            requirements = requirements.ToList()
        };

        private static KnowledgeRequirement Requirement(KnowledgeRequirementKind kind, string facet, string eventId, float minimum) => new KnowledgeRequirement
        {
            kind = kind, facetId = facet, eventId = eventId, minimum = minimum, comparison = KnowledgeRequirementComparison.GreaterOrEqual
        };

        private static KnowledgeMilestoneTrackDef BuildMilestoneTrack() => new KnowledgeMilestoneTrackDef
        {
            defName = "HNS_CultivationMilestones",
            stableId = "cultivation",
            ordered = true,
            milestones = new List<KnowledgeMilestoneDef>
            {
                Milestone("first-germination", "First germination", 1),
                Milestone("first-harvest", "First harvest", 2),
                Milestone("stable-yield", "Stable yield", 3),
                Milestone("documented-record", "Documented record", 4)
            }
        };

        private static KnowledgeMilestoneDef Milestone(string id, string label, int order) => new KnowledgeMilestoneDef
        {
            stableId = id, label = label, description = "A cultivation milestone for this plant record.", order = order,
            permanent = true, pauseBehavior = KnowledgeMilestonePauseBehavior.Pause, resetBehavior = KnowledgeMilestoneResetBehavior.Never
        };

        private static List<KnowledgeObservationDef> BuildObservations()
        {
            return Enum.GetValues(typeof(HorticultureKnowledgeEvent)).Cast<HorticultureKnowledgeEvent>().Select(value => new KnowledgeObservationDef
            {
                defName = "HNS_Observation_" + RecipeId(value).Replace("-", "_"),
                stableId = RecipeId(value),
                label = value.ToString(),
                baseKnowledge = 0f,
                baseExpertise = 0f,
                baseFamiliarity = 0f,
                facetIds = Facets.ToList(),
                retainProvenance = true,
                countFailureAsEvidence = true,
                witnessDistribution = new KnowledgeWitnessDistribution
                {
                    policy = KnowledgeWitnessDistributionPolicy.WitnessesReduced,
                    efficiency = 0.55f,
                    expertiseEfficiency = 0.45f,
                    confidenceEfficiency = 0.75f,
                    maximumRecipients = 8
                },
                accrualPolicy = new KnowledgeAccrualPolicy
                {
                    uniquePerSourceInstance = true,
                    stateLimit = 4096
                }
            }).Concat(new[] { new KnowledgeObservationDef
            {
                defName = "HNS_Observation_LegacyImport", stableId = "legacy-import", label = "Legacy import",
                baseKnowledge = 0f, baseExpertise = 0f, baseFamiliarity = 0f, facetIds = Facets.ToList(),
                retainProvenance = true,
                accrualPolicy = new KnowledgeAccrualPolicy { uniquePerSourceInstance = true, stateLimit = 4096 }
            } }).ToList();
        }

        private static List<KnowledgeClaimDef> BuildClaims() => new List<KnowledgeClaimDef>
        {
            Claim("growth_duration", "Growth duration", FacetGrowth, KnowledgeClaimValueType.Float, KnowledgeClaimAggregation.WeightedMean, KnowledgeClaimStalenessPolicy.SlowlyStale),
            Claim("sow_work", "Sow work", FacetSowing, KnowledgeClaimValueType.Float, KnowledgeClaimAggregation.WeightedMean, KnowledgeClaimStalenessPolicy.SlowlyStale),
            Claim("harvest_work", "Harvest work", FacetHarvesting, KnowledgeClaimValueType.Float, KnowledgeClaimAggregation.WeightedMean, KnowledgeClaimStalenessPolicy.SlowlyStale),
            Claim("yield_range", "Observed yield", FacetYield, KnowledgeClaimValueType.Float, KnowledgeClaimAggregation.ObservedRange, KnowledgeClaimStalenessPolicy.Seasonal),
            Claim("minimum_fertility", "Minimum fertility", FacetSoil, KnowledgeClaimValueType.Percentage, KnowledgeClaimAggregation.Highest, KnowledgeClaimStalenessPolicy.SlowlyStale),
            Claim("preferred_soil", "Preferred soil", FacetSoil, KnowledgeClaimValueType.EnumId, KnowledgeClaimAggregation.MostSupported, KnowledgeClaimStalenessPolicy.Seasonal),
            Claim("temperature_range", "Observed temperature range", FacetClimate, KnowledgeClaimValueType.Float, KnowledgeClaimAggregation.ObservedRange, KnowledgeClaimStalenessPolicy.Seasonal),
            Claim("lifespan", "Lifespan", FacetLifespan, KnowledgeClaimValueType.EnumId, KnowledgeClaimAggregation.MostSupported, KnowledgeClaimStalenessPolicy.SlowlyStale),
            Claim("harvest_cycles", "Harvest cycles", FacetLifespan, KnowledgeClaimValueType.Integer, KnowledgeClaimAggregation.Highest, KnowledgeClaimStalenessPolicy.Permanent),
            Claim("seed_viability", "Seed viability", FacetLineage, KnowledgeClaimValueType.Percentage, KnowledgeClaimAggregation.WeightedMean, KnowledgeClaimStalenessPolicy.SlowlyStale),
            Claim("parent_lineage", "Parent lineage", FacetLineage, KnowledgeClaimValueType.SetOfIds, KnowledgeClaimAggregation.Union, KnowledgeClaimStalenessPolicy.Permanent),
            Claim("trait_identity", "Trait identity", FacetTraits, KnowledgeClaimValueType.SetOfIds, KnowledgeClaimAggregation.Union, KnowledgeClaimStalenessPolicy.Permanent),
            Claim("trait_expression", "Observed trait expression", FacetTraits, KnowledgeClaimValueType.SetOfIds, KnowledgeClaimAggregation.Union, KnowledgeClaimStalenessPolicy.SlowlyStale),
            Claim("produce_identity", "Produce identity", FacetProduce, KnowledgeClaimValueType.DefReference, KnowledgeClaimAggregation.MostSupported, KnowledgeClaimStalenessPolicy.Permanent),
            Claim("environmental_response", "Environmental response", FacetClimate, KnowledgeClaimValueType.EnumId, KnowledgeClaimAggregation.MostSupported, KnowledgeClaimStalenessPolicy.Seasonal),
            Claim("cultivation_stability", "Cultivation stability", FacetYield, KnowledgeClaimValueType.Percentage, KnowledgeClaimAggregation.WeightedMean, KnowledgeClaimStalenessPolicy.Seasonal)
        };

        private static KnowledgeClaimDef Claim(string id, string label, string facet, KnowledgeClaimValueType type,
            KnowledgeClaimAggregation aggregation, KnowledgeClaimStalenessPolicy staleness) => new KnowledgeClaimDef
        {
            defName = "HNS_Claim_" + id,
            stableId = id,
            label = label,
            facetId = facet,
            valueType = type,
            aggregation = aggregation,
            stalenessPolicy = staleness,
            halfLifeTicks = staleness == KnowledgeClaimStalenessPolicy.Seasonal ? 900000f : 1500000f,
            provisionalConfidence = 0.45f,
            documentable = true,
            provenanceLimit = 16,
            measurementHistoryLimit = 64
        };

        private static KnowledgeSubjectArchetypeDef Archetype(string id, IEnumerable<string> facets, IEnumerable<KnowledgeClaimDef> claims) => new KnowledgeSubjectArchetypeDef
        {
            defName = "HNS_Archetype_" + id.Replace(".", "_"),
            stableId = id,
            applicableFacetIds = facets.ToList(),
            applicableClaimIds = claims.Select(value => value.StableId).ToList(),
            comparisonSchemaId = "horticulture.cultivar-comparison"
        };

        private static IEnumerable<KnowledgeSubjectRegistration> SubjectSource()
        {
            List<KnowledgeSubjectRegistration> result = new List<KnowledgeSubjectRegistration>();
            foreach (ThingDef plant in DefDatabase<ThingDef>.AllDefsListForReading.Where(NovelSeedUtility.IsGrowableCrop))
                result.Add(Subject(SubjectId(plant), plant.LabelCap.ToString(), plant.description, plant, SpeciesArchetype));
            foreach (VarietyRecord variety in GameComponent_NovelSeeds.Instance?.AllVarieties ?? Enumerable.Empty<VarietyRecord>())
                if (variety?.cropDef != null && HorticulturePlantPolicy.IsSupported(variety.cropDef)) result.Add(Subject(CultivarSubjectId(variety), variety.Label, variety.cropDef.description,
                    variety.cropDef, variety.originKind == "wild" ? WildVariantArchetype : CultivarArchetype));
            foreach (Map map in Find.Maps ?? Enumerable.Empty<Map>())
            {
                foreach (Zone zone in map.zoneManager?.AllZones ?? Enumerable.Empty<Zone>())
                    if (zone is IPlantToGrowSettable) result.Add(Subject(FieldSubjectId((IPlantToGrowSettable)zone),
                        zone.label ?? "Experimental field", "A growing zone used for contextual horticultural evidence.", null, FieldArchetype));
                if (map.Biome != null) result.Add(Subject("environment:biome:" + map.Biome.defName, map.Biome.LabelCap.ToString(),
                    map.Biome.description, map.Biome, EnvironmentArchetype));
            }
            return result.GroupBy(value => value.id).Select(group => group.First());
        }

        private static KnowledgeSubjectRegistration ResolveSubject(string id)
        {
            if (id.NullOrEmpty()) return null;
            if (id.StartsWith("cultivar:", StringComparison.Ordinal))
            {
                VarietyRecord variety = GameComponent_NovelSeeds.Instance?.GetVariety(id.Substring("cultivar:".Length));
                return variety?.cropDef == null || !HorticulturePlantPolicy.IsSupported(variety.cropDef) ? null : Subject(id, variety.Label, variety.cropDef.description, variety.cropDef,
                    variety.originKind == "wild" ? WildVariantArchetype : CultivarArchetype);
            }
            if (id.StartsWith("field:", StringComparison.Ordinal))
            {
                IPlantToGrowSettable grower = Find.Maps?.SelectMany(map => map.zoneManager?.AllZones ?? Enumerable.Empty<Zone>())
                    .OfType<IPlantToGrowSettable>().FirstOrDefault(value => GameComponent_NovelSeeds.GrowerKey(value) == id.Substring("field:".Length));
                return grower == null ? null : Subject(id, "Experimental field", "A growing zone used for contextual horticultural evidence.", null, FieldArchetype);
            }
            if (id.StartsWith("environment:biome:", StringComparison.Ordinal))
            {
                BiomeDef biome = DefDatabase<BiomeDef>.GetNamedSilentFail(id.Substring("environment:biome:".Length));
                return biome == null ? null : Subject(id, biome.LabelCap.ToString(), biome.description, biome, EnvironmentArchetype);
            }
            ThingDef plant = DefDatabase<ThingDef>.GetNamedSilentFail(id);
            return plant != null && NovelSeedUtility.IsGrowableCrop(plant) ? Subject(id, plant.LabelCap.ToString(), plant.description, plant, SpeciesArchetype) : null;
        }

        private static KnowledgeSubjectRegistration Subject(string id, string label, string description, Def source, string archetype) => new KnowledgeSubjectRegistration
        {
            id = id,
            label = label ?? id,
            description = description ?? string.Empty,
            unidentifiedLabel = "Unidentified plant record",
            unidentifiedDescription = "The colony has evidence, but not enough to identify this plant record.",
            sourceDef = source,
            archetypeId = archetype,
            sortOrder = archetype == SpeciesArchetype ? 0 : archetype == CultivarArchetype || archetype == WildVariantArchetype ? 10 : 20,
            source = RegistrationSource
        };

        internal static bool RegisterUiProvider()
        {
            return KnowledgeV3Ui.Register(new HorticultureV3UiProvider(), true);
        }

        private static KnowledgeMenuModel UnavailableMenu() => new KnowledgeMenuModel
        {
            title = "Horticulture Knowledge Unavailable",
            expertiseLabel = "Knowledge Framework unavailable",
            expertiseRank = KnowledgeRank.Novice,
            expertiseProgress = 0f,
            sections = new List<KnowledgeMenuSection>
            {
                new KnowledgeMenuSection
                {
                    id = "horticulture-unavailable",
                    label = "Plant knowledge unavailable",
                    emptyText = "Knowledge Framework is not ready or is incompatible. Horticulture gameplay remains available."
                }
            }
        };

        internal static bool RegisterContexts()
        {
            if (!KnowledgeContextRegistry.RegisterType(new KnowledgeContextTypeDef { defName = "HNS_ContextZone", stableId = ContextZone }, true)) return false;
            if (!KnowledgeContextRegistry.RegisterType(new KnowledgeContextTypeDef { defName = "HNS_ContextHydroponic", stableId = ContextHydroponic }, true)) return false;
            if (!KnowledgeContextRegistry.RegisterType(new KnowledgeContextTypeDef { defName = "HNS_ContextWildSite", stableId = ContextWildSite }, true)) return false;
            if (!KnowledgeContextRegistry.RegisterType(new KnowledgeContextTypeDef { defName = "HNS_ContextGreenhouse", stableId = ContextGreenhouse }, true)) return false;
            if (!KnowledgeContextRegistry.RegisterType(new KnowledgeContextTypeDef { defName = "HNS_ContextMap", stableId = ContextMap }, true)) return false;
            if (!KnowledgeContextRegistry.RegisterType(new KnowledgeContextTypeDef { defName = "HNS_ContextBiome", stableId = ContextBiome }, true)) return false;
            if (!KnowledgeContextRegistry.RegisterType(new KnowledgeContextTypeDef { defName = "HNS_ContextGlobal", stableId = ContextGlobal }, true)) return false;
            HorticultureContextResolver resolver = new HorticultureContextResolver();
            foreach (string type in new[] { ContextZone, ContextHydroponic, ContextWildSite, ContextGreenhouse, ContextMap, ContextBiome, ContextGlobal })
                if (!KnowledgeContextRegistry.RegisterResolver(type, resolver, true)) return false;
            return true;
        }

        internal static bool RegisterRelationsAndComparisons()
        {
            foreach (string type in new[] { "parent-of", "mutation-origin", "wild-origin", "cross-pollination" })
                if (!KnowledgeRelationService.RegisterType(new KnowledgeSubjectRelationTypeDef
                {
                    defName = "HNS_Relation_" + type.Replace("-", "_"), stableId = type, parentage = true, metadataLimit = 8
                }, true)) return false;
            if (!HorticultureKnowledgeCompatibility.HasOptional(KnowledgeFrameworkApi.StructuredComparisonCapability)) return true;
            return KnowledgeComparisonService.RegisterSchema(new KnowledgeComparisonSchema
            {
                id = "horticulture.cultivar-comparison",
                label = "Cultivar comparison",
                claimIds = BuildClaims().Select(value => value.StableId).ToList(),
                facetIds = Facets.ToList()
            }, true);
        }

        private sealed class HorticultureContextResolver : IKnowledgeContextResolver
        {
            public KnowledgeContextKey Parent(KnowledgeContextKey context)
            {
                if (context.IsEmpty) return KnowledgeContextKey.Empty;
                if (context.typeId == ContextZone || context.typeId == ContextHydroponic || context.typeId == ContextGreenhouse)
                    return MapFromStable(context.stableId);
                if (context.typeId == ContextWildSite)
                    return MapFromStable(context.stableId);
                if (context.typeId == ContextMap)
                {
                    Map map = Find.Maps?.FirstOrDefault(value => value?.uniqueID.ToString() == context.stableId);
                    return map?.Biome == null ? new KnowledgeContextKey(ContextGlobal, "global") : BiomeContext(map.Biome);
                }
                if (context.typeId == ContextBiome) return new KnowledgeContextKey(ContextGlobal, "global");
                return KnowledgeContextKey.Empty;
            }

            private static KnowledgeContextKey MapFromStable(string stable)
            {
                string mapId = (stable ?? string.Empty).Split(':').FirstOrDefault();
                return new KnowledgeContextKey(ContextMap, mapId);
            }
        }

        private sealed class HorticultureV3UiProvider : IKnowledgeDomainUiV3
        {
            public string DomainId => HorticultureKnowledgeAdapter.DomainId;

            public IEnumerable<string> ListBadges(KnowledgeBrowserRow row, Pawn pawn, KnowledgeScope scope)
            {
                if (row == null) return Array.Empty<string>();
                List<string> result = new List<string> { StageLabel(row.lastStage) };
                if (row.confidence >= 0.7f) result.Add("reliable");
                else if (row.confidence > 0.05f) result.Add("uncertain");
                if (row.usedContextFallback) result.Add("fallback context");
                if (row.relations.Count > 0) result.Add("lineage");
                return result;
            }

            public IEnumerable<string> ListColumns(KnowledgeBrowserRow row, Pawn pawn, KnowledgeScope scope) => new[]
            {
                StageLabel(row?.lastStage), "Confidence " + (row?.confidence ?? 0f).ToStringPercent(),
                "Evidence " + (row?.evidenceCount ?? 0), row == null || row.recencyTick <= 0 ? "No recent evidence" :
                    "Last evidence " + (Find.TickManager.TicksGame - row.recencyTick).ToStringTicksToPeriod() + " ago"
            };

            public void DrawDetailPanels(Rect rect, KnowledgeBrowserRow row, Pawn pawn, KnowledgeScope scope)
            {
                if (row == null || rect.width <= 20f) return;
                Rect panel = new Rect(rect.x, rect.y, rect.width, Mathf.Min(82f, rect.height));
                Widgets.DrawMenuSection(panel);
                string context = row.usedContextFallback ? "Expected from " + row.resolvedContext + "; not yet confirmed here." :
                    row.resolvedContext.IsEmpty ? "No contextual evidence yet." : "Confirmed in " + row.resolvedContext + ".";
                Widgets.Label(panel.ContractedBy(8f), StageLabel(row.lastStage) + "\n" +
                    row.confidence.ToStringPercent() + " confidence. " + context);
            }
        }

        private static KnowledgeSubjectDefinition LegacySubject(ThingDef plant) => new KnowledgeSubjectDefinition(plant.defName,
            plant.LabelCap, plant.description, plant);

        private static bool IsPlayerColonist(Pawn pawn) => pawn?.Faction?.def?.isPlayer == true && pawn.RaceProps?.Humanlike == true;

        private static string ContextDescription(KnowledgeFacetSnapshotV2 value) => value.usedContextFallback
            ? "Expected from " + value.context + "; not yet confirmed here." : value.context.IsEmpty ? "Global" : value.context.ToString();

        private static string InstanceId(Pawn pawn, ThingDef plant, string recipe) =>
            HorticultureKnowledgeEventIdentity.Normalize(recipe, (pawn?.thingIDNumber ?? 0) + ":" +
                plant?.defName + ":" + recipe);

        private static string FacetLabel(string id) => id == FacetSoil ? "Soil compatibility" : id == FacetTraits ? "Trait expression" :
            id == FacetResilience ? "Disease and resilience" : id == FacetLifespan ? "Lifespan" : id.CapitalizeFirst();
    }
}
