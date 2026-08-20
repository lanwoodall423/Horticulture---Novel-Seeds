using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace HorticultureNovelSeeds
{
    public class ITab_PlantVariety : ITab
    {
        private HorticultureInspectorDocument document;
        private Thing lastThing;

        public ITab_PlantVariety()
        {
            size = new Vector2(500f, 440f);
            labelKey = "HNS_VarietyTab";
        }

        public override bool IsVisible
        {
            get
            {
                CompPlantVariety comp = SelThing?.TryGetComp<CompPlantVariety>();
                return comp != null && comp.HasAnyTraits;
            }
        }

        protected override void FillTab()
        {
            CompPlantVariety comp = SelThing?.TryGetComp<CompPlantVariety>();
            if (comp == null || !comp.HasAnyTraits)
            {
                EnsureDocument();
                document.Refresh(new HorticultureInspectorSnapshot
                {
                    Title = "Cultivar details",
                    Subtitle = "No Cultivar data is available for this plant.",
                    PrimaryHeader = "Traits",
                    SecondaryHeader = "Effects",
                    PrimaryEmpty = "No traits",
                    SecondaryEmpty = "No effects"
                });
                document.Draw(ContentRect());
                return;
            }

            EnsureDocument();
            VarietyRecord variety = comp.PendingDiscovery ? null : comp.Variety;
            HorticultureCultivarPresentation authority = variety == null ? null :
                HorticulturePresentationPolicy.ForCultivar(variety, null, true);
            List<VarietyTraitDef> traits = authority?.AuthorizedTraits?.Where(trait => trait != null).ToList()
                ?? new List<VarietyTraitDef>();
            List<string> statLines = authority == null ? new List<string> { "Cultivar claims are not documented yet." } :
                new List<string> { authority.ModifierText };
            string subtitle = variety == null ? "Observable novel variation is not a documented cultivar." :
                "Cultivar evidence is shown only from Knowledge claims.";
            document.Refresh(new HorticultureInspectorSnapshot
            {
                Title = "Cultivar: " + comp.DisplayVarietyName,
                Subtitle = subtitle,
                PrimaryHeader = "Traits",
                SecondaryHeader = "Stat changes",
                PrimaryEmpty = authority == null ? "Trait identity is unknown." : "No documented traits",
                SecondaryEmpty = "No documented cultivar measurements",
                PrimaryRows = authority == null
                    ? new[] { new HorticultureInspectorRow { Id = "unknown-traits", Label = "Traits unknown", Detail = string.Empty } }
                    : traits.Select((trait, index) => new HorticultureInspectorRow
                {
                    Id = trait.defName ?? "trait-" + index,
                    Label = TraitColorUI.Label(trait),
                    Detail = TraitColorUI.Description(trait)
                }).ToArray(),
                SecondaryRows = statLines.Select((line, index) => new HorticultureInspectorRow
                {
                    Id = "stat-" + index,
                    Label = line,
                    Detail = string.Empty
                }).ToArray(),
                ActionLabel = "Open in Horticulture",
                Action = () =>
                {
                    if (variety != null) MainTabWindow_CultivarRegistry.OpenLineage(variety);
                    else MainTabWindow_CultivarRegistry.OpenPlant(SelThing?.def);
                }
            });
            document.Draw(ContentRect());
        }

        private void EnsureDocument()
        {
            if (document != null && ReferenceEquals(lastThing, SelThing)) return;
            document?.PostClose();
            document = new HorticultureInspectorDocument("hns.inspector.plant");
            lastThing = SelThing;
        }

        private Rect ContentRect() => new Rect(0f, 0f, size.x, size.y).ContractedBy(12f);
    }
}
