using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace HorticultureNovelSeeds
{
    public sealed class ITab_ProduceVariety : ITab
    {
        private Vector2 scrollPosition;

        public ITab_ProduceVariety()
        {
            size = new Vector2(470f, 380f);
            labelKey = "HNS_Traits";
        }

        public override bool IsVisible => SelThing?.TryGetComp<CompNovelProduceAppearance>()?.HasVarietyData == true;

        protected override void FillTab()
        {
            CompNovelProduceAppearance comp = SelThing?.TryGetComp<CompNovelProduceAppearance>();
            Rect rect = new Rect(0f, 0f, size.x, size.y).ContractedBy(12f);
            if (comp?.HasVarietyData != true)
            {
                Widgets.Label(rect, "HNS_NoVarietyData".Translate());
                return;
            }

            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(rect.x, rect.y, rect.width, 32f), "HNS_VarietyProduce".Translate());
            Text.Font = GameFont.Small;

            List<string> sources = comp.SourceVarietyLabels;
            string allSources = string.Join(", ", sources);
            string visibleSources = sources.Count <= 3 ? allSources : string.Join(", ", sources.Take(3)) + ", +" + (sources.Count - 3);
            string sourceText = (sources.Count == 1 ? "HNS_SourceVariety" : "HNS_SourceVarieties").Translate(visibleSources);
            float sourceHeight = Mathf.Clamp(Text.CalcHeight(sourceText, rect.width), 28f, 72f);
            Rect sourceRect = new Rect(rect.x, rect.y + 38f, rect.width, sourceHeight);
            Widgets.Label(sourceRect, sourceText);
            if (sources.Count > 3) TooltipHandler.TipRegion(sourceRect, (sources.Count == 1 ? "HNS_SourceVariety" : "HNS_SourceVarieties").Translate(allSources));

            float y = rect.y + 44f + sourceHeight;
            if (!Mathf.Approximately(comp.NutritionFactor, 1f))
            {
                string nutrition = "HNS_EffectiveNutrition".Translate(comp.NutritionFactor.ToStringPercent());
                Widgets.Label(new Rect(rect.x, y, rect.width, 24f), nutrition);
                y += 28f;
            }

            Rect panel = new Rect(rect.x, y + 4f, rect.width, rect.yMax - y - 4f);
            DrawQualitiesPanel(panel, comp);
        }

        private void DrawQualitiesPanel(Rect rect, CompNovelProduceAppearance comp)
        {
            Widgets.DrawMenuSection(rect);
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(rect.x + 8f, rect.y + 3f, rect.width - 16f, 32f), "HNS_InheritedQualities".Translate());
            Text.Font = GameFont.Small;

            Rect outRect = new Rect(rect.x + 8f, rect.y + 38f, rect.width - 16f, rect.height - 46f);
            List<VarietyTraitDef> qualities = comp.InheritedTraits;
            float viewWidth = outRect.width - 16f;
            ThingDef productDef = SelThing?.def;
            List<float> heights = qualities.Select(trait => RowHeight(comp, trait, viewWidth, productDef)).ToList();
            float viewHeight = Mathf.Max(outRect.height, qualities.Count == 0 ? 30f : heights.Sum());
            Rect view = new Rect(0f, 0f, viewWidth, viewHeight);
            Widgets.BeginScrollView(outRect, ref scrollPosition, view);
            if (qualities.Count == 0)
            {
                Widgets.Label(new Rect(4f, 4f, view.width - 8f, 26f), "HNS_NoInheritedQualities".Translate());
            }
            else
            {
                float y = 0f;
                for (int i = 0; i < qualities.Count; i++)
                {
                    DrawQualityRow(new Rect(0f, y, view.width, heights[i] - 4f), comp, qualities[i], productDef);
                    y += heights[i];
                }
            }
            Widgets.EndScrollView();
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

        private static float RowHeight(CompNovelProduceAppearance comp, VarietyTraitDef trait, float width, ThingDef productDef)
        {
            string effect = ProduceEffectLine(comp, trait, productDef);
            float labelHeight = Mathf.Max(22f, Text.CalcHeight(trait.LabelCap, width - 12f));
            float effectHeight = effect.NullOrEmpty() ? 0f : Text.CalcHeight(effect, width - 12f);
            return Mathf.Max(42f, 8f + labelHeight + effectHeight + (effectHeight > 0f ? 4f : 0f));
        }

        private static void DrawQualityRow(Rect rect, CompNovelProduceAppearance comp, VarietyTraitDef trait, ThingDef productDef)
        {
            Widgets.DrawHighlightIfMouseover(rect);
            float width = rect.width - 12f;
            float labelHeight = Mathf.Max(22f, Text.CalcHeight(trait.LabelCap, width));
            Widgets.Label(new Rect(rect.x + 6f, rect.y + 3f, width, labelHeight), trait.LabelCap);
            string effect = ProduceEffectLine(comp, trait, productDef);
            if (!effect.NullOrEmpty())
            {
                Color previous = GUI.color;
                GUI.color = new Color(0.72f, 0.72f, 0.72f);
                Widgets.Label(new Rect(rect.x + 6f, rect.y + 3f + labelHeight, width, rect.height - labelHeight - 5f), effect);
                GUI.color = previous;
            }
            if (!trait.description.NullOrEmpty()) TooltipHandler.TipRegion(rect, trait.description);
        }
    }
}