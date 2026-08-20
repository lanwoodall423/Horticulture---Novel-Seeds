using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace HorticultureNovelSeeds
{
    public sealed class ITab_ProduceVariety : ITab
    {
        private HorticultureInspectorDocument document;
        private Thing lastThing;

        public ITab_ProduceVariety()
        {
            size = new Vector2(470f, 380f);
            labelKey = "HNS_Traits";
        }

        public override bool IsVisible => SelThing?.TryGetComp<CompNovelProduceAppearance>()?.HasVarietyData == true;

        protected override void FillTab()
        {
            CompNovelProduceAppearance comp = SelThing?.TryGetComp<CompNovelProduceAppearance>();
            EnsureDocument();
            if (comp?.HasVarietyData != true)
            {
                document.Refresh(new HorticultureInspectorSnapshot
                {
                    Title = "Produce Cultivar",
                    Subtitle = "No produce Cultivar data is available.",
                    PrimaryHeader = "Source Cultivars",
                    SecondaryHeader = "Inherited qualities",
                    PrimaryEmpty = "No source Cultivars",
                    SecondaryEmpty = "No inherited qualities"
                });
                document.Draw(ContentRect());
                return;
            }

            ThingDef sourcePlant = comp.SourcePlantDef;
            List<VarietyRecord> sourceCultivars = (comp.SourceVarietyIds ?? new string[0])
                .Where(id => !id.NullOrEmpty())
                .Select(id => GameComponent_NovelSeeds.Instance?.GetVariety(id))
                .Where(variety => variety != null).ToList();
            List<Tuple<VarietyRecord, HorticultureCultivarPresentation>> sourceRows = sourceCultivars
                .Select(variety => Tuple.Create(variety, HorticulturePresentationPolicy.ForCultivar(variety, null, true))).ToList();
            List<HorticultureCultivarPresentation> sourceAuthority = sourceRows.Select(value => value.Item2)
                .Where(value => value != null).ToList();
            VarietyRecord sourceCultivar = sourceCultivars.FirstOrDefault();
            HorticultureCultivarPresentation primaryAuthority = sourceAuthority.FirstOrDefault();
            List<VarietyTraitDef> qualities = sourceAuthority.SelectMany(value => value.AuthorizedTraits ?? Array.Empty<VarietyTraitDef>())
                .Where(trait => trait != null).Distinct().ToList();
            Action openSource = sourceCultivar != null
                ? (Action)(() => MainTabWindow_CultivarRegistry.OpenCultivar(sourceCultivar))
                : sourcePlant == null ? null : (Action)(() => MainTabWindow_CultivarRegistry.OpenPlant(sourcePlant));
            document.Refresh(new HorticultureInspectorSnapshot
            {
                Title = "Produce Cultivar",
                Subtitle = primaryAuthority?.ProductText ?? "Produce identity remains unknown until a source cultivar claim is available.",
                PrimaryHeader = "Source Cultivars",
                SecondaryHeader = "Inherited qualities",
                PrimaryEmpty = "No source Cultivars",
                SecondaryEmpty = "No inherited qualities",
                PrimaryRows = sourceRows.Select((source, index) => new HorticultureInspectorRow
                {
                    Id = "source-" + index,
                    Label = source.Item2 == null ? "Source cultivar identity unknown" : source.Item1.Label,
                    Detail = source.Item2 == null ? "Identity unknown" : "Source Cultivar"
                }).ToArray(),
                SecondaryRows = qualities.Select((trait, index) => new HorticultureInspectorRow
                {
                    Id = trait.defName ?? "quality-" + index,
                    Label = TraitColorUI.Label(trait),
                    Detail = ProduceEffectLine(primaryAuthority)
                }).ToArray(),
                ActionLabel = openSource == null ? null : "Open in Horticulture",
                Action = openSource
            });
            document.Draw(ContentRect());
        }

        private static string ProduceEffectLine(HorticultureCultivarPresentation authority)
        {
            return authority?.HasKnownProducts == true
                ? "Produce identity is documented; detailed effects remain claim-scoped."
                : "Produce effects unknown until the source cultivar is documented.";
        }

        private void EnsureDocument()
        {
            if (document != null && ReferenceEquals(lastThing, SelThing)) return;
            document?.PostClose();
            document = new HorticultureInspectorDocument("hns.inspector.produce");
            lastThing = SelThing;
        }

        private Rect ContentRect() => new Rect(0f, 0f, size.x, size.y).ContractedBy(12f);
    }
}
