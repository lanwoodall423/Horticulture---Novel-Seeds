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

            List<string> sources = comp.SourceVarietyLabels ?? new List<string>();
            List<VarietyTraitDef> qualities = comp.InheritedTraits?.Where(trait => trait != null).ToList()
                ?? new List<VarietyTraitDef>();
            ThingDef productDef = SelThing?.def;
            document.Refresh(new HorticultureInspectorSnapshot
            {
                Title = "Produce Cultivar",
                Subtitle = "Nutrition factor: " + comp.NutritionFactor.ToStringPercent(),
                PrimaryHeader = "Source Cultivars",
                SecondaryHeader = "Inherited qualities",
                PrimaryEmpty = "No source Cultivars",
                SecondaryEmpty = "No inherited qualities",
                PrimaryRows = sources.Select((source, index) => new HorticultureInspectorRow
                {
                    Id = "source-" + index,
                    Label = source,
                    Detail = "Source Cultivar"
                }).ToArray(),
                SecondaryRows = qualities.Select((trait, index) => new HorticultureInspectorRow
                {
                    Id = trait.defName ?? "quality-" + index,
                    Label = TraitColorUI.Label(trait),
                    Detail = ProduceEffectLine(comp, trait, productDef)
                }).ToArray()
            });
            document.Draw(ContentRect());
        }

        private static string ProduceEffectLine(CompNovelProduceAppearance comp, VarietyTraitDef trait, ThingDef productDef)
        {
            List<string> effects = new List<string>();
            if (comp.HasProduceEffect(trait))
            {
                string gameplayEffect = NovelSeedUtility.InheritedProduceQualityLine(trait, productDef);
                if (!gameplayEffect.NullOrEmpty() && gameplayEffect != "No Effect") effects.Add(gameplayEffect);
            }
            if (NovelSeedUtility.HasProduceColorVisual(comp.SourcePlantDef, new[] { trait }))
                effects.Add("Visual: Applies the configured produce appearance.");
            return effects.Count == 0 ? "No Effect" : string.Join("\n", effects);
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
