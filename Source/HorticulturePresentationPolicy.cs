using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace HorticultureNovelSeeds
{
    public sealed class HorticultureWorkspaceRelevance
    {
        public bool Plants { get; internal set; }
        public bool Cultivars { get; internal set; }
        public bool Breeding { get; internal set; }
        public bool AnyMeaningfulState => Plants || Cultivars || Breeding;
    }

    public sealed class HorticultureLineageReference
    {
        public string Label { get; internal set; }
        public bool IsKnown { get; internal set; }
        internal VarietyRecord Record { get; set; }
    }

    // Read-only projection of the documented registry, not a second genetic authority.
    public sealed class HorticultureCultivarPresentation
    {
        internal VarietyRecord Authority { get; set; }
        public IReadOnlyList<VarietyTraitDef> AuthorizedTraits { get; internal set; }
        public IReadOnlyList<string> TraitNames => AuthorizedTraits.Select(TraitColorUI.Label).ToArray();
        public bool HasKnownTraits => true;
        public bool HasKnownProducts => Authority.cropDef?.plant?.harvestedThingDef != null;
        public string TraitText => AuthorizedTraits.Count == 0 ? "No documented traits" : string.Join(", ", TraitNames);
        public string PotentialAppearance => PhenotypeObservation.Potential(AuthorizedTraits);
        public string TraitDescriptionText => string.Join("; ", AuthorizedTraits.Select(TraitColorUI.Description));
        public string ModifierText => "Genetic modifiers: yield " + Delta(NovelSeedUtility.YieldFactor(AuthorizedTraits)) +
            ", growth rate " + Delta(NovelSeedUtility.GrowthRateFactor(AuthorizedTraits)) +
            ", sow work " + Delta(ExpandedTraitUtility.SowWorkFactor(AuthorizedTraits)) +
            ", harvest work " + Delta(ExpandedTraitUtility.HarvestWorkFactor(AuthorizedTraits));
        public string ProductText => Authority.cropDef?.plant?.harvestedThingDef?.LabelCap.ToString() ?? "No ordinary harvested product";
        public string Origin => Authority.originKind == "wild" ? "Wild origin" :
            Authority.originKind == "cross-pollination" ? "Cross-pollination origin" : "Mutation origin";
        public IReadOnlyList<HorticultureLineageReference> Parents { get; internal set; }
        public int? Generation => Parents.Any(p => !p.IsKnown) ? null : Authority.generation;
        public string LineageText => Parents.Count == 0 ? Origin :
            string.Join(" ? ", Parents.Select(p => p.Label)) + (Generation.HasValue ? " ? generation " + Generation.Value : "");
        public CultivationJournalRecord Journal => GameComponent_CultivationJournal.Instance?.ForCultivar(Authority);
        private static string Delta(float factor) => ((factor - 1f) * 100f).ToString("+0.#;-0.#;0") + "%";
    }

    public static class HorticulturePresentationPolicy
    {
        public static bool IsDocumented(VarietyRecord variety)
        {
            if (variety == null || variety.hiddenFromMenus || string.IsNullOrWhiteSpace(variety.id) ||
                string.IsNullOrWhiteSpace(variety.customName) || !HorticulturePlantPolicy.IsSupported(variety.cropDef)) return false;
            return ReferenceEquals(GameComponent_NovelSeeds.Instance?.GetVariety(variety.id), variety);
        }

        public static HorticultureCultivarPresentation ForCultivar(VarietyRecord variety, Pawn pawn = null, bool colony = true)
        {
            if (!IsDocumented(variety)) return null;
            var parents = new List<HorticultureLineageReference>();
            foreach (string id in (variety.parentVarietyIds ?? new List<string>()).Distinct().Take(128))
            {
                var parent = GameComponent_NovelSeeds.Instance?.GetVariety(id);
                bool known = IsDocumented(parent) && parent != variety;
                parents.Add(new HorticultureLineageReference { IsKnown = known, Label = known ? parent.Label : "Unknown parent", Record = known ? parent : null });
            }
            return new HorticultureCultivarPresentation
            {
                Authority = variety,
                AuthorizedTraits = (variety.traits ?? new List<VarietyTraitDef>()).Where(t => t != null).Distinct().ToArray(),
                Parents = parents
            };
        }

        public static bool HasPlantEvidence(ThingDef plant) => HorticulturePlantPolicy.IsSupported(plant) &&
            (GameComponent_CultivationJournal.Instance?.ForSpecies(plant)?.firstObservedTick >= 0 ||
             GameComponent_NovelSeeds.Instance?.VarietiesFor(plant).Any(IsDocumented) == true);

        public static HorticultureWorkspaceRelevance WorkspaceRelevance()
        {
            var registry = GameComponent_NovelSeeds.Instance;
            bool cultivars = registry?.AllVarieties.Any(IsDocumented) == true;
            return new HorticultureWorkspaceRelevance
            {
                Plants = cultivars || GameComponent_CultivationJournal.Instance?.ObservedSpecies.Any() == true,
                Cultivars = cultivars,
                Breeding = cultivars || registry?.BreedingPrograms.Count > 0
            };
        }
    }
}
