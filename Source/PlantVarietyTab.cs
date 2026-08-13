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
            List<VarietyTraitDef> traits = comp.ActiveTraits?.Where(trait => trait != null).ToList()
                ?? new List<VarietyTraitDef>();
            List<string> statLines = NovelSeedUtility.StatChangeLines(traits, SelThing.def) ?? new List<string>();
            statLines.Add(NovelSeedUtility.TraitBalanceSummary(traits));
            VarietyRecord variety = comp.PendingDiscovery ? null : comp.Variety;
            string subtitle = variety == null ? "Discovery details are still unknown." :
                (variety.FirstDiscoveredInfo.NullOrEmpty() ? "Discovered Cultivar" :
                    "First discovered: " + variety.FirstDiscoveredInfo);
            document.Refresh(new HorticultureInspectorSnapshot
            {
                Title = "Cultivar: " + comp.DisplayVarietyName,
                Subtitle = subtitle,
                PrimaryHeader = "Traits",
                SecondaryHeader = "Stat changes",
                PrimaryEmpty = "No traits",
                SecondaryEmpty = "No stat changes",
                PrimaryRows = traits.Select((trait, index) => new HorticultureInspectorRow
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
