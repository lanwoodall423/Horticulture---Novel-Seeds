using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace HorticultureNovelSeeds
{
    public class ITab_PlantVariety : ITab
    {
        private const float PanelPadding = 8f;
        private const float PanelGap = 10f;
        private const float PanelHeaderHeight = 36f;
        private Vector2 traitScrollPosition;
        private Vector2 statScrollPosition;

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
            Rect rect = new Rect(0f, 0f, size.x, size.y).ContractedBy(12f);
            if (comp == null || !comp.HasAnyTraits)
            {
                Widgets.Label(rect, "HNS_NoVarietyData".Translate());
                return;
            }
            string varietyName = comp.DisplayVarietyName;
            List<VarietyTraitDef> traits = comp.ActiveTraits.ToList();
            List<string> statLines = NovelSeedUtility.StatChangeLines(traits, SelThing.def);
            statLines.Add(NovelSeedUtility.TraitBalanceSummary(traits));

            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(rect.x, rect.y, rect.width - 150f, 30f), "HNS_VarietyLabel".Translate(varietyName));
            if (Widgets.ButtonText(new Rect(rect.xMax - 138f, rect.y, 104f, 30f), "HNS_Lineage".Translate()))
            {
                VarietyRecord variety = comp.Variety;
                List<string> parentIds = comp.CrossPollinated
                    ? comp.CrossPollinationParentIds
                    : variety?.parentVarietyIds?.ToList() ?? new List<string>();
                Find.WindowStack.Add(new Dialog_VarietyLineage(varietyName, traits, parentIds));
            }
            Text.Font = GameFont.Small;

            VarietyRecord discoveredVariety = comp.PendingDiscovery ? null : comp.Variety;
            float headerHeight = 40f;
            if (discoveredVariety != null && !discoveredVariety.FirstDiscoveredInfo.NullOrEmpty())
            {
                Color previous = GUI.color;
                GUI.color = new Color(0.72f, 0.72f, 0.72f);
                Widgets.Label(new Rect(rect.x, rect.y + 31f, rect.width - 8f, 24f), "HNS_FirstDiscoveredHeader".Translate() + ": " + discoveredVariety.FirstDiscoveredInfo);
                GUI.color = previous;
                headerHeight = 64f;
            }

            Rect contentRect = new Rect(rect.x, rect.y + headerHeight, rect.width, rect.height - headerHeight);
            float traitsHeight = Mathf.Min(178f, Mathf.Max(126f, contentRect.height * 0.44f));
            Rect traitsPanel = new Rect(contentRect.x, contentRect.y, contentRect.width, traitsHeight);
            Rect statsPanel = new Rect(contentRect.x, traitsPanel.yMax + PanelGap, contentRect.width, contentRect.height - traitsHeight - PanelGap);

            DrawTraitsPanel(traitsPanel, traits);
            DrawStatChangesPanel(statsPanel, statLines);
        }

        private void DrawTraitsPanel(Rect rect, List<VarietyTraitDef> traits)
        {
            DrawPanelFrame(rect, "HNS_Traits".Translate().ToString());
            Rect outRect = PanelContentRect(rect);
            float viewWidth = outRect.width - 16f;
            List<float> rowHeights = traits.Select(trait => TraitHeight(trait, viewWidth)).ToList();
            float viewHeight = Mathf.Max(outRect.height, traits.Count == 0 ? 28f : rowHeights.Sum());
            Rect viewRect = new Rect(0f, 0f, viewWidth, viewHeight);
            Widgets.BeginScrollView(outRect, ref traitScrollPosition, viewRect);
            if (traits.Count == 0) Widgets.Label(new Rect(0f, 0f, viewRect.width, 28f), "HNS_NoTraits".Translate());
            else
            {
                float y = 0f;
                for (int i = 0; i < traits.Count; i++)
                {
                    DrawTraitRow(new Rect(0f, y, viewRect.width, rowHeights[i] - 4f), traits[i]);
                    y += rowHeights[i];
                }
            }
            Widgets.EndScrollView();
        }
        private void DrawStatChangesPanel(Rect rect, List<string> statLines)
        {
            DrawPanelFrame(rect, "HNS_StatChanges".Translate().ToString());
            Rect outRect = PanelContentRect(rect);
            float viewWidth = outRect.width - 16f;
            List<float> rowHeights = statLines.Select(line => StatHeight(line, viewWidth)).ToList();
            float viewHeight = Mathf.Max(outRect.height, statLines.Count == 0 ? 28f : rowHeights.Sum());
            Rect viewRect = new Rect(0f, 0f, viewWidth, viewHeight);
            Widgets.BeginScrollView(outRect, ref statScrollPosition, viewRect);
            if (statLines.Count == 0) Widgets.Label(new Rect(0f, 0f, viewRect.width, 28f), "HNS_NoStatChanges".Translate());
            else
            {
                float y = 0f;
                for (int i = 0; i < statLines.Count; i++)
                {
                    DrawStatRow(new Rect(0f, y, viewRect.width, rowHeights[i] - 2f), statLines[i]);
                    y += rowHeights[i];
                }
            }
            Widgets.EndScrollView();
        }
        private static Rect PanelContentRect(Rect panelRect)
        {
            return new Rect(panelRect.x + PanelPadding, panelRect.y + PanelHeaderHeight, panelRect.width - PanelPadding * 2f, panelRect.height - PanelHeaderHeight - PanelPadding);
        }

        private static void DrawPanelFrame(Rect rect, string title)
        {
            Widgets.DrawMenuSection(rect);
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(rect.x + PanelPadding, rect.y + 2f, rect.width - PanelPadding * 2f, 32f), title);
            Text.Font = GameFont.Small;
        }

        private static float TraitHeight(VarietyTraitDef trait, float width)
        {
            float textWidth = Mathf.Max(80f, width - 12f);
            float labelHeight = Mathf.Max(22f, Text.CalcHeight(trait.LabelCap, textWidth));
            float descriptionHeight = trait.description.NullOrEmpty() ? 0f : Text.CalcHeight(trait.description, textWidth);
            return Mathf.Max(42f, 7f + labelHeight + descriptionHeight + (descriptionHeight > 0f ? 5f : 0f));
        }

        private static float StatHeight(string line, float width)
        {
            return Mathf.Max(34f, Text.CalcHeight(line ?? string.Empty, Mathf.Max(80f, width - 12f)) + 10f);
        }

        private static void DrawTraitRow(Rect rect, VarietyTraitDef trait)
        {
            Widgets.DrawHighlightIfMouseover(rect);
            float textWidth = rect.width - 12f;
            float labelHeight = Mathf.Max(22f, Text.CalcHeight(trait.LabelCap, textWidth));
            Widgets.Label(new Rect(rect.x + 6f, rect.y + 3f, textWidth, labelHeight), trait.LabelCap);
            if (!trait.description.NullOrEmpty())
            {
                float descriptionY = rect.y + 3f + labelHeight;
                float descriptionHeight = Mathf.Max(18f, Text.CalcHeight(trait.description, textWidth));
                Color previous = GUI.color;
                GUI.color = new Color(0.72f, 0.72f, 0.72f);
                Widgets.Label(new Rect(rect.x + 6f, descriptionY, textWidth, descriptionHeight), trait.description);
                GUI.color = previous;
                TooltipHandler.TipRegion(rect, trait.description);
            }
        }

        private static void DrawStatRow(Rect rect, string line)
        {
            Widgets.DrawHighlightIfMouseover(rect);
            Widgets.Label(new Rect(rect.x + 6f, rect.y + 4f, rect.width - 12f, rect.height - 6f), line);
        }
    }
}
