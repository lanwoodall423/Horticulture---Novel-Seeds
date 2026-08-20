using System;
using System.Collections.Generic;
using System.Linq;
using KnowledgeFramework;
using ProgressionAgriculture;
using RimWorld;
using UnityEngine;
using Verse;

namespace HorticultureNovelSeeds
{
    public enum HorticultureKnowledgeValueState
    {
        Unknown,
        Approximate,
        Uncertain,
        Exact
    }

    /// <summary>
    /// A read-only claim projection for presentation. The framework snapshot is kept
    /// internal so callers cannot accidentally turn stable IDs or raw values into UI.
    /// </summary>
    public sealed class HorticultureKnowledgeClaimView
    {
        private readonly KnowledgeClaimSnapshot snapshot;

        internal HorticultureKnowledgeClaimView(KnowledgeClaimSnapshot value)
        {
            snapshot = value;
        }

        public bool HasValue => snapshot != null && snapshot.value != null && snapshot.observationCount > 0;
        public bool Revealed => snapshot?.revealed == true;
        public bool Documented => snapshot?.documented == true;
        public float Confidence => snapshot?.effectiveConfidence ?? 0f;
        public HorticultureKnowledgeValueState State
        {
            get
            {
                if (!HasValue) return HorticultureKnowledgeValueState.Unknown;
                if (snapshot.contradictory || snapshot.provisional || snapshot.effectiveConfidence < 0.7f)
                    return HorticultureKnowledgeValueState.Uncertain;
                return snapshot.value.type == KnowledgeClaimValueType.NumericRange
                    ? HorticultureKnowledgeValueState.Approximate
                    : HorticultureKnowledgeValueState.Exact;
            }
        }

        internal KnowledgeClaimValue Value => snapshot?.value;
        internal KnowledgeClaimSnapshot Snapshot => snapshot;
    }

    public sealed class HorticultureWorkspaceRelevance
    {
        public bool Plants { get; internal set; }
        public bool Cultivars { get; internal set; }
        public bool Breeding { get; internal set; }
        public bool Knowledge { get; internal set; }
        public bool AnyMeaningfulState => Plants || Cultivars || Breeding || Knowledge;
    }

    public sealed class HorticultureLineageReference
    {
        private readonly string subjectId;

        internal HorticultureLineageReference(string id, string label, bool known)
        {
            subjectId = id;
            Label = label.NullOrEmpty() ? "Unknown parent" : label;
            IsKnown = known;
        }

        public string Label { get; private set; }
        public bool IsKnown { get; private set; }
        internal string SubjectId => subjectId;
    }

    public sealed class HorticulturePlantPresentation
    {
        internal HorticulturePlantPresentation(ThingDef definition, bool explicitContext, bool identityKnown,
            bool technologicallyAvailable, string stage, KnowledgeRank rank, int cultivarCount,
            HorticultureKnowledgeClaimView identity, HorticultureKnowledgeClaimView growthDuration,
            HorticultureKnowledgeClaimView sowWork, HorticultureKnowledgeClaimView harvestWork,
            HorticultureKnowledgeClaimView yield, HorticultureKnowledgeClaimView minimumFertility,
            HorticultureKnowledgeClaimView preferredSoil, HorticultureKnowledgeClaimView temperatureRange,
            HorticultureKnowledgeClaimView produce)
        {
            Definition = definition;
            ExplicitContext = explicitContext;
            IdentityKnown = identityKnown;
            TechnologicallyAvailable = technologicallyAvailable;
            Stage = stage.NullOrEmpty() ? HorticultureKnowledgeAdapter.StageUnknown : stage;
            Rank = rank;
            CultivarCount = cultivarCount;
            Identity = identity;
            GrowthDuration = growthDuration;
            SowWork = sowWork;
            HarvestWork = harvestWork;
            Yield = yield;
            MinimumFertility = minimumFertility;
            PreferredSoil = preferredSoil;
            TemperatureRange = temperatureRange;
            Produce = produce;
        }

        public ThingDef Definition { get; private set; }
        public bool ExplicitContext { get; private set; }
        public bool IdentityKnown { get; private set; }
        public bool TechnologicallyAvailable { get; private set; }
        public string Stage { get; private set; }
        public KnowledgeRank Rank { get; private set; }
        public int CultivarCount { get; private set; }
        public HorticultureKnowledgeClaimView Identity { get; private set; }
        public HorticultureKnowledgeClaimView GrowthDuration { get; private set; }
        public HorticultureKnowledgeClaimView SowWork { get; private set; }
        public HorticultureKnowledgeClaimView HarvestWork { get; private set; }
        public HorticultureKnowledgeClaimView Yield { get; private set; }
        public HorticultureKnowledgeClaimView MinimumFertility { get; private set; }
        public HorticultureKnowledgeClaimView PreferredSoil { get; private set; }
        public HorticultureKnowledgeClaimView TemperatureRange { get; private set; }
        public HorticultureKnowledgeClaimView Produce { get; private set; }

        public bool HasEvidence => IdentityKnown || Claims().Any(value => value.HasValue);

        public IEnumerable<HorticultureKnowledgeClaimView> Claims()
        {
            return new[] { Identity, GrowthDuration, SowWork, HarvestWork, Yield, MinimumFertility,
                PreferredSoil, TemperatureRange, Produce }.Where(value => value != null);
        }
    }

    public sealed class HorticultureCultivarPresentation
    {
        internal HorticultureCultivarPresentation(VarietyRecord authority, bool technologicallyAvailable,
            string stage, KnowledgeRank rank, HorticultureKnowledgeClaimView traitIdentity,
            HorticultureKnowledgeClaimView traitExpression, HorticultureKnowledgeClaimView produceIdentity,
            HorticultureKnowledgeClaimView parentLineage, HorticultureKnowledgeClaimView yield,
            HorticultureKnowledgeClaimView growthDuration, HorticultureKnowledgeClaimView sowWork,
            HorticultureKnowledgeClaimView harvestWork, HorticultureKnowledgeClaimView temperatureRange,
            string origin, IReadOnlyList<HorticultureLineageReference> parents, int? generation)
        {
            Authority = authority;
            TechnologicallyAvailable = technologicallyAvailable;
            Stage = stage.NullOrEmpty() ? HorticultureKnowledgeAdapter.StageUnknown : stage;
            Rank = rank;
            TraitIdentity = traitIdentity;
            TraitExpression = traitExpression;
            ProduceIdentity = produceIdentity;
            ParentLineage = parentLineage;
            Yield = yield;
            GrowthDuration = growthDuration;
            SowWork = sowWork;
            HarvestWork = harvestWork;
            TemperatureRange = temperatureRange;
            Origin = origin.NullOrEmpty() ? "Origin unknown" : origin;
            Parents = parents ?? Array.Empty<HorticultureLineageReference>();
            Generation = generation;
            AuthorizedTraits = ResolveTraits(traitIdentity);
            TraitNames = AuthorizedTraits.Select(TraitColorUI.Label).ToArray();
        }

        internal VarietyRecord Authority { get; private set; }
        public bool TechnologicallyAvailable { get; private set; }
        public string Stage { get; private set; }
        public KnowledgeRank Rank { get; private set; }
        public HorticultureKnowledgeClaimView TraitIdentity { get; private set; }
        public HorticultureKnowledgeClaimView TraitExpression { get; private set; }
        public HorticultureKnowledgeClaimView ProduceIdentity { get; private set; }
        public HorticultureKnowledgeClaimView ParentLineage { get; private set; }
        public HorticultureKnowledgeClaimView Yield { get; private set; }
        public HorticultureKnowledgeClaimView GrowthDuration { get; private set; }
        public HorticultureKnowledgeClaimView SowWork { get; private set; }
        public HorticultureKnowledgeClaimView HarvestWork { get; private set; }
        public HorticultureKnowledgeClaimView TemperatureRange { get; private set; }
        public string Origin { get; private set; }
        public IReadOnlyList<HorticultureLineageReference> Parents { get; private set; }
        public int? Generation { get; private set; }
        public IReadOnlyList<VarietyTraitDef> AuthorizedTraits { get; private set; }
        public IReadOnlyList<string> TraitNames { get; private set; }
        public bool HasKnownTraits => TraitIdentity?.HasValue == true;
        public bool HasKnownProducts => ProduceIdentity?.HasValue == true;
        public bool HasLineage => Parents.Count > 0 || Origin != "Origin unknown";

        public string TraitText => FormatTraits(TraitIdentity, AuthorizedTraits);
        public string TraitDescriptionText => FormatTraitDescriptions(AuthorizedTraits);
        public string ModifierText => FormatModifiers(Yield, GrowthDuration, SowWork, HarvestWork, TemperatureRange);
        public string ProductText => FormatProduct(ProduceIdentity);
        public string LineageText => FormatLineage(Parents, Generation, Origin);

        private static IReadOnlyList<VarietyTraitDef> ResolveTraits(HorticultureKnowledgeClaimView claim)
        {
            if (claim?.Value?.type != KnowledgeClaimValueType.SetOfIds) return Array.Empty<VarietyTraitDef>();
            return (claim.Value.setValues ?? new List<string>())
                .Select(value => DefDatabase<VarietyTraitDef>.GetNamedSilentFail(value))
                .Where(value => value != null).Distinct().ToArray();
        }

        internal static string FormatTraits(HorticultureKnowledgeClaimView claim, IReadOnlyList<VarietyTraitDef> traits)
        {
            if (claim?.HasValue != true) return "Traits unknown until a cultivar claim is recorded.";
            string value = traits == null || traits.Count == 0 ? "No documented traits" :
                string.Join(", ", traits.Select(TraitColorUI.Label).Where(text => !text.NullOrEmpty()));
            return Qualify(claim, value);
        }

        private static string FormatTraitDescriptions(IReadOnlyList<VarietyTraitDef> traits)
        {
            if (traits == null || traits.Count == 0) return string.Empty;
            return string.Join("; ", traits.Select(TraitColorUI.Description).Where(value => !value.NullOrEmpty()));
        }

        internal static string FormatModifiers(params HorticultureKnowledgeClaimView[] claims)
        {
            List<string> values = new List<string>();
            if (claims != null)
            {
                foreach (HorticultureKnowledgeClaimView claim in claims.Where(value => value?.HasValue == true))
                    values.Add(FormatClaim(claim));
            }
            return values.Count == 0 ? "No cultivar-specific measurements are documented." : string.Join("; ", values);
        }

        internal static string FormatProduct(HorticultureKnowledgeClaimView claim)
        {
            if (claim?.HasValue != true) return "Product identity unknown.";
            KnowledgeClaimValue value = claim.Value;
            if (value.type == KnowledgeClaimValueType.DefReference)
            {
                ThingDef product = DefDatabase<ThingDef>.GetNamedSilentFail(value.textValue);
                return Qualify(claim, product == null ? "Observed produce (identity unavailable)" : product.LabelCap.ToString());
            }
            return Qualify(claim, "Observed produce (identity unavailable)");
        }

        internal static string FormatLineage(IReadOnlyList<HorticultureLineageReference> parents, int? generation, string origin)
        {
            if (parents == null || parents.Count == 0)
                return origin == "Origin unknown" ? "Lineage unknown." : origin + "; original recorded origin.";
            string depth = generation.HasValue ? "generation " + generation.Value : "generation unknown";
            return origin + "; " + depth + "; " + parents.Count + " recorded parent relationship" + (parents.Count == 1 ? string.Empty : "s");
        }

        internal static string FormatClaim(HorticultureKnowledgeClaimView claim)
        {
            if (claim?.HasValue != true) return "Unknown";
            KnowledgeClaimValue value = claim.Value;
            string formatted;
            switch (value.type)
            {
                case KnowledgeClaimValueType.Integer: formatted = value.integerValue.ToString(); break;
                case KnowledgeClaimValueType.Float: formatted = value.numericValue.ToString("0.##"); break;
                case KnowledgeClaimValueType.Percentage: formatted = value.numericValue.ToString("0.##") + "%"; break;
                case KnowledgeClaimValueType.NumericRange: formatted = value.rangeValue.ToString(); break;
                case KnowledgeClaimValueType.Boolean: formatted = value.booleanValue ? "Yes" : "No"; break;
                default: formatted = "Recorded"; break;
            }
            return Qualify(claim, formatted);
        }

        private static string Qualify(HorticultureKnowledgeClaimView claim, string value)
        {
            if (claim == null || claim.State == HorticultureKnowledgeValueState.Exact) return value;
            return (claim.State == HorticultureKnowledgeValueState.Approximate ? "Approximate: " : "Uncertain: ") + value;
        }

        private static string ClaimLabel(HorticultureKnowledgeClaimView claim, string label)
        {
            return claim?.HasValue == true ? label + " " + FormatClaim(claim) : null;
        }
    }

    /// <summary>
    /// The single read-only policy used by colony-facing presentation. It owns no
    /// persistent state and never substitutes gameplay records for missing knowledge.
    /// </summary>
    public static class HorticulturePresentationPolicy
    {
        public static HorticultureWorkspaceRelevance WorkspaceRelevance()
        {
            GameComponent_NovelSeeds component = GameComponent_NovelSeeds.Instance;
            HorticultureWorkspaceRelevance relevance = new HorticultureWorkspaceRelevance();
            if (component == null) return relevance;
            relevance.Cultivars = component.AllVarieties.Any();
            relevance.Breeding = (component.BreedingPrograms ?? new BreedingProgramRecord[0]).Any(value => value != null);
            relevance.Plants = relevance.Cultivars || SupportedPlants().Any(HasPlantEvidence);
            relevance.Knowledge = SupportedPlants().Any(HasPlantEvidence) || component.AllVarieties.Any(HasCultivarEvidence);
            return relevance;
        }

        public static HorticulturePlantPresentation ForPlant(ThingDef plant, Pawn pawn = null, bool colony = true,
            bool explicitContext = false)
        {
            if (plant == null || !HorticulturePlantPolicy.IsSupported(plant)) return null;
            GameComponent_NovelSeeds component = GameComponent_NovelSeeds.Instance;
            bool identityKnown = explicitContext || component?.VarietiesFor(plant).Any() == true || HasPlantEvidence(plant);
            KnowledgeScopeData scope = Scope(pawn, colony);
            bool framework = HorticultureKnowledgeAdapter.IsFrameworkUsable;
            return new HorticulturePlantPresentation(plant, explicitContext, identityKnown,
                TechnologicallyAvailable(plant), framework ? HorticultureKnowledgeAdapter.StageFor(plant, pawn, colony) : HorticultureKnowledgeAdapter.StageUnknown,
                framework ? HorticultureKnowledgeAdapter.TierFor(plant, pawn, colony) : KnowledgeRank.Novice,
                component?.VarietiesFor(plant).Count() ?? 0,
                new HorticultureKnowledgeClaimView(null),
                Claim(plant.defName, HorticultureKnowledgeAdapter.FacetGrowth, "growth_duration", scope),
                Claim(plant.defName, HorticultureKnowledgeAdapter.FacetSowing, "sow_work", scope),
                Claim(plant.defName, HorticultureKnowledgeAdapter.FacetHarvesting, "harvest_work", scope),
                Claim(plant.defName, HorticultureKnowledgeAdapter.FacetYield, "yield_range", scope),
                Claim(plant.defName, HorticultureKnowledgeAdapter.FacetSoil, "minimum_fertility", scope),
                Claim(plant.defName, HorticultureKnowledgeAdapter.FacetSoil, "preferred_soil", scope),
                Claim(plant.defName, HorticultureKnowledgeAdapter.FacetClimate, "temperature_range", scope),
                Claim(plant.defName, HorticultureKnowledgeAdapter.FacetProduce, "produce_identity", scope));
        }

        public static HorticultureCultivarPresentation ForCultivar(VarietyRecord variety, Pawn pawn = null, bool colony = true)
        {
            if (variety?.cropDef == null || !HorticulturePlantPolicy.IsSupported(variety.cropDef)) return null;
            KnowledgeScopeData scope = Scope(pawn, colony);
            string subjectId = HorticultureKnowledgeAdapter.CultivarSubjectId(variety);
            bool framework = HorticultureKnowledgeAdapter.IsFrameworkUsable;
            HorticultureKnowledgeClaimView traitIdentity = Claim(subjectId, HorticultureKnowledgeAdapter.FacetTraits, "trait_identity", scope);
            HorticultureKnowledgeClaimView traitExpression = Claim(subjectId, HorticultureKnowledgeAdapter.FacetTraits, "trait_expression", scope);
            HorticultureKnowledgeClaimView produce = Claim(subjectId, HorticultureKnowledgeAdapter.FacetProduce, "produce_identity", scope);
            HorticultureKnowledgeClaimView lineage = Claim(subjectId, HorticultureKnowledgeAdapter.FacetLineage, "parent_lineage", scope);
            IReadOnlyList<HorticultureLineageReference> parents = ParentReferences(variety, lineage);
            string origin = Origin(variety, subjectId);
            return new HorticultureCultivarPresentation(variety, TechnologicallyAvailable(variety.cropDef),
                framework ? HorticultureKnowledgeAdapter.CultivarStageFor(variety, pawn, colony) : HorticultureKnowledgeAdapter.StageUnknown,
                framework ? HorticultureKnowledgeAdapter.CultivarTierFor(variety, pawn, colony) : KnowledgeRank.Novice,
                traitIdentity, traitExpression, produce, lineage,
                Claim(subjectId, HorticultureKnowledgeAdapter.FacetYield, "yield_range", scope),
                Claim(subjectId, HorticultureKnowledgeAdapter.FacetGrowth, "growth_duration", scope),
                Claim(subjectId, HorticultureKnowledgeAdapter.FacetSowing, "sow_work", scope),
                Claim(subjectId, HorticultureKnowledgeAdapter.FacetHarvesting, "harvest_work", scope),
                Claim(subjectId, HorticultureKnowledgeAdapter.FacetClimate, "temperature_range", scope),
                origin, parents, AuthorizedGeneration(variety, parents, 0, new HashSet<string>(StringComparer.Ordinal)));
        }

        public static bool HasPlantEvidence(ThingDef plant)
        {
            if (plant == null || !HorticulturePlantPolicy.IsSupported(plant) || !HorticultureKnowledgeAdapter.IsFrameworkUsable) return false;
            KnowledgeSubjectSnapshotV2 subject = HorticultureKnowledgeSnapshots.Subject(HorticultureKnowledgeAdapter.DomainId,
                HorticultureKnowledgeAdapter.SubjectId(plant), null, KnowledgeScope.Colony);
            KnowledgeFacetSnapshotV2 identity = HorticultureKnowledgeSnapshots.Facet(HorticultureKnowledgeAdapter.DomainId,
                HorticultureKnowledgeAdapter.SubjectId(plant), HorticultureKnowledgeAdapter.FacetIdentity, null,
                KnowledgeScope.Colony, KnowledgeContextKey.Empty, KnowledgeContextFallbackMode.ParentThenGlobal);
            return subject?.documented == true || subject?.familiarity > 0f || HorticultureKnowledgeAdapter.StageOrder(subject?.stageId) > 0 ||
                identity?.evidenceCount > 0 || identity?.amount > 0f;
        }

        public static bool HasCultivarEvidence(VarietyRecord variety)
        {
            if (variety == null || !HorticultureKnowledgeAdapter.IsFrameworkUsable) return false;
            KnowledgeSubjectSnapshotV2 subject = HorticultureKnowledgeSnapshots.Subject(HorticultureKnowledgeAdapter.DomainId,
                HorticultureKnowledgeAdapter.CultivarSubjectId(variety), null, KnowledgeScope.Colony);
            return subject?.documented == true || subject?.familiarity > 0f || HorticultureKnowledgeAdapter.StageOrder(subject?.stageId) > 0 ||
                Claim(HorticultureKnowledgeAdapter.CultivarSubjectId(variety), HorticultureKnowledgeAdapter.FacetTraits,
                    "trait_identity", Scope(null, true))?.HasValue == true;
        }

        private static IEnumerable<ThingDef> SupportedPlants()
        {
            return DefDatabase<ThingDef>.AllDefsListForReading.Where(HorticulturePlantPolicy.IsSupported);
        }

        private static bool TechnologicallyAvailable(ThingDef crop)
        {
            GameComponent_UnlockedCrops registry = GameComponent_UnlockedCrops.Instance;
            return registry != null && crop != null && registry.IsCropUnlocked(crop);
        }

        private static KnowledgeScopeData Scope(Pawn pawn, bool colony)
        {
            return new KnowledgeScopeData(pawn, colony ? KnowledgeScope.Colony : KnowledgeScope.Personal);
        }

        private static HorticultureKnowledgeClaimView Claim(string subjectId, string facetId, string claimId, KnowledgeScopeData scope)
        {
            if (!HorticultureKnowledgeAdapter.IsFrameworkUsable || subjectId.NullOrEmpty()) return new HorticultureKnowledgeClaimView(null);
            return new HorticultureKnowledgeClaimView(HorticultureKnowledgeSnapshots.Claim(HorticultureKnowledgeAdapter.DomainId,
                subjectId, facetId, claimId, scope.Pawn, scope.Scope, KnowledgeContextKey.Empty,
                KnowledgeContextFallbackMode.ParentThenGlobal));
        }

        private static IReadOnlyList<HorticultureLineageReference> ParentReferences(VarietyRecord variety,
            HorticultureKnowledgeClaimView lineage)
        {
            if (variety == null) return Array.Empty<HorticultureLineageReference>();
            Dictionary<string, HorticultureLineageReference> references = new Dictionary<string, HorticultureLineageReference>(StringComparer.Ordinal);
            string subjectId = HorticultureKnowledgeAdapter.CultivarSubjectId(variety);
            if (HorticultureKnowledgeAdapter.IsFrameworkUsable)
            {
                IReadOnlyList<KnowledgeSubjectRelation> relations = KnowledgeQuery.StructuralRelations(HorticultureKnowledgeAdapter.DomainId,
                    subjectId, outgoing: false, incoming: true);
                foreach (KnowledgeSubjectRelation relation in relations ?? Array.Empty<KnowledgeSubjectRelation>())
                {
                    if (relation == null || relation.relationTypeId != "parent-of" || relation.toSubjectId != subjectId ||
                        !relation.revealed || relation.confidence <= 0f) continue;
                    AddParentReference(references, relation.fromSubjectId);
                }
            }
            if (lineage?.Value?.type == KnowledgeClaimValueType.SetOfIds)
                foreach (string id in lineage.Value.setValues ?? new List<string>()) AddParentReference(references, id);
            return references.Values.OrderBy(value => value.Label, StringComparer.Ordinal).ToArray();
        }

        private static void AddParentReference(Dictionary<string, HorticultureLineageReference> references, string id)
        {
            if (id.NullOrEmpty()) return;
            string subjectId = id.StartsWith("cultivar:", StringComparison.Ordinal) ? id : "cultivar:" + id;
            string rawId = subjectId.Substring("cultivar:".Length);
            VarietyRecord parent = GameComponent_NovelSeeds.Instance?.GetVariety(rawId);
            bool known = parent != null && !parent.hiddenFromMenus;
            references[subjectId] = new HorticultureLineageReference(subjectId, known ? parent.Label : "Unknown parent", known);
        }

        private static string Origin(VarietyRecord variety, string subjectId)
        {
            if (!HorticultureKnowledgeAdapter.IsFrameworkUsable) return "Origin unknown";
            IReadOnlyList<KnowledgeSubjectRelation> relations = KnowledgeQuery.StructuralRelations(HorticultureKnowledgeAdapter.DomainId,
                subjectId, outgoing: false, incoming: true);
            KnowledgeSubjectRelation relation = (relations ?? Array.Empty<KnowledgeSubjectRelation>()).FirstOrDefault(value =>
                value != null && value.toSubjectId == subjectId && value.revealed && value.confidence > 0f &&
                (value.relationTypeId == "wild-origin" || value.relationTypeId == "cross-pollination" || value.relationTypeId == "mutation-origin"));
            if (relation == null) return "Origin unknown";
            switch (relation.relationTypeId)
            {
                case "wild-origin": return "Wild origin";
                case "cross-pollination": return "Cross-pollination origin";
                default: return "Mutation origin";
            }
        }

        private static int? AuthorizedGeneration(VarietyRecord variety, IReadOnlyList<HorticultureLineageReference> parents,
            int depth, HashSet<string> path)
        {
            if (variety == null || depth >= HorticultureWorkspaceDocument.MaximumLineageDepth) return null;
            string subjectId = HorticultureKnowledgeAdapter.CultivarSubjectId(variety);
            if (subjectId.NullOrEmpty() || !path.Add(subjectId)) return null;
            if (parents == null || parents.Count == 0)
            {
                path.Remove(subjectId);
                return Origin(variety, subjectId) == "Origin unknown" ? (int?)null : 0;
            }
            if (parents.Any(value => !value.IsKnown))
            {
                path.Remove(subjectId);
                return null;
            }
            List<int> depths = new List<int>();
            foreach (HorticultureLineageReference reference in parents)
            {
                string rawId = reference.SubjectId.Substring("cultivar:".Length);
                VarietyRecord parent = GameComponent_NovelSeeds.Instance?.GetVariety(rawId);
                HorticultureKnowledgeClaimView parentLineage = Claim(HorticultureKnowledgeAdapter.CultivarSubjectId(parent),
                    HorticultureKnowledgeAdapter.FacetLineage, "parent_lineage", Scope(null, true));
                IReadOnlyList<HorticultureLineageReference> parentRefs = ParentReferences(parent, parentLineage);
                int? parentDepth = AuthorizedGeneration(parent, parentRefs, depth + 1, path);
                if (!parentDepth.HasValue)
                {
                    path.Remove(subjectId);
                    return null;
                }
                depths.Add(parentDepth.Value);
            }
            path.Remove(subjectId);
            return depths.Count == 0 ? (int?)null : depths.Max() + 1;
        }

        private readonly struct KnowledgeScopeData
        {
            public readonly Pawn Pawn;
            public readonly KnowledgeScope Scope;

            public KnowledgeScopeData(Pawn pawn, KnowledgeScope scope)
            {
                Pawn = pawn;
                Scope = scope;
            }
        }
    }
}
