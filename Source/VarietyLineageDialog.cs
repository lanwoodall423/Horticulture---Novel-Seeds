using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace HorticultureNovelSeeds
{
    public class Dialog_VarietyLineage : Window
    {
        private readonly string varietyName;
        private readonly List<VarietyTraitDef> currentTraits;
        private readonly List<string> parentIds;
        private readonly List<LineageRow> rows = new List<LineageRow>();
        private Vector2 scrollPosition;

        public override Vector2 InitialSize => new Vector2(680f, 640f);

        public Dialog_VarietyLineage(string varietyName, IEnumerable<VarietyTraitDef> currentTraits, IEnumerable<string> parentIds)
        {
            this.varietyName = varietyName;
            this.currentTraits = currentTraits?.Where(t => t != null).ToList() ?? new List<VarietyTraitDef>();
            this.parentIds = parentIds?.Where(id => !id.NullOrEmpty()).Distinct().ToList() ?? new List<string>();
            doCloseX = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = true;
            BuildRows();
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width - 40f, 32f), "HNS_LineageTitle".Translate(varietyName));
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(inRect.x, inRect.y + 38f, inRect.width, 26f), "HNS_LineageSubtitle".Translate());

            Rect outRect = new Rect(inRect.x, inRect.y + 72f, inRect.width, inRect.height - 118f);
            Widgets.DrawMenuSection(outRect);
            Rect scrollOut = outRect.ContractedBy(8f);
            float viewWidth = scrollOut.width - 16f;
            List<float> rowHeights = rows.Select(row => GetRowHeight(row, viewWidth)).ToList();
            float rowsHeight = rowHeights.Sum() + 8f;
            float contentHeight = parentIds.Count == 0 ? scrollOut.height : Mathf.Max(scrollOut.height, rowsHeight);
            Rect viewRect = new Rect(0f, 0f, viewWidth, contentHeight);
            Widgets.BeginScrollView(scrollOut, ref scrollPosition, viewRect);
            if (parentIds.Count == 0)
            {
                DrawNoLineage(viewRect);
            }
            else
            {
                float y = 0f;
                for (int i = 0; i < rows.Count; i++)
                {
                    DrawRow(new Rect(0f, y, viewRect.width, rowHeights[i]), rows[i]);
                    y += rowHeights[i];
                }
            }
            Widgets.EndScrollView();

            if (Widgets.ButtonText(new Rect(inRect.xMax - 110f, inRect.yMax - 34f, 110f, 30f), "CloseButton".Translate())) Close();
        }
        private void BuildRows()
        {
            rows.Clear();
            rows.Add(new LineageRow(0, "HNS_LineageCurrent".Translate().ToString(), varietyName, NovelSeedUtility.TraitSummary(currentTraits), false, false));
            HashSet<string> path = new HashSet<string>();
            AddParents(parentIds, 1, path);
        }

        private void AddParents(List<string> ids, int depth, HashSet<string> path)
        {
            for (int i = 0; i < ids.Count; i++)
            {
                string id = ids[i];
                string role = i == 0 ? "HNS_LineageSeedParent".Translate().ToString() : i == 1 ? "HNS_LineagePollenParent".Translate().ToString() : "HNS_LineageParent".Translate().ToString();
                VarietyRecord record = GameComponent_NovelSeeds.Instance?.GetVariety(id);
                if (record == null)
                {
                    rows.Add(new LineageRow(depth, role, "HNS_UnknownVariety".Translate().ToString(), string.Empty, true, false));
                    continue;
                }

                bool cycle = path.Contains(id);
                rows.Add(new LineageRow(depth, role, record.Label, NovelSeedUtility.TraitSummary(record.traits), false, cycle));
                if (!cycle && record.parentVarietyIds != null && record.parentVarietyIds.Count > 0)
                {
                    path.Add(id);
                    AddParents(record.parentVarietyIds.Where(parentId => !parentId.NullOrEmpty()).ToList(), depth + 1, path);
                    path.Remove(id);
                }
            }
        }

        private static void DrawNoLineage(Rect rect)
        {
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(new Rect(rect.x + 30f, rect.y + 40f, rect.width - 60f, 80f), "HNS_NoCrossLineage".Translate());
            Text.Anchor = TextAnchor.UpperLeft;
        }

        private static float GetRowHeight(LineageRow row, float width)
        {
            float indent = Mathf.Min(row.depth, 8) * 28f;
            float rowWidth = Mathf.Max(180f, width - indent);
            float detailWidth = Mathf.Max(80f, rowWidth - 130f);
            float roleHeight = Text.CalcHeight(row.role ?? string.Empty, 118f);
            float nameHeight = Text.CalcHeight(row.name + (row.cycle ? " (cycle)" : string.Empty), detailWidth);
            float topHeight = Mathf.Max(22f, Mathf.Max(roleHeight, nameHeight));
            float traitsHeight = row.traits.NullOrEmpty() ? 0f : Text.CalcHeight("HNS_LineageTraits".Translate(row.traits), detailWidth);
            return Mathf.Max(58f, 7f + topHeight + (traitsHeight > 0f ? traitsHeight + 5f : 0f) + 5f);
        }

        private static void DrawRow(Rect rect, LineageRow row)
        {
            float indent = Mathf.Min(row.depth, 8) * 28f;
            Rect rowRect = new Rect(rect.x + indent, rect.y, rect.width - indent, rect.height - 3f);
            Widgets.DrawHighlightIfMouseover(rowRect);
            if (row.depth > 0)
            {
                float lineX = rowRect.x - 14f;
                Widgets.DrawLineVertical(lineX, rect.y, rect.height * 0.5f);
                Widgets.DrawLineHorizontal(lineX, rect.y + rect.height * 0.5f, 11f);
            }
            float detailWidth = Mathf.Max(80f, rowRect.width - 130f);
            float roleHeight = Mathf.Max(22f, Text.CalcHeight(row.role ?? string.Empty, 118f));
            string displayName = row.name + (row.cycle ? " (cycle)" : string.Empty);
            float nameHeight = Mathf.Max(22f, Text.CalcHeight(displayName, detailWidth));
            float topHeight = Mathf.Max(roleHeight, nameHeight);
            Color old = GUI.color;
            GUI.color = new Color(0.72f, 0.72f, 0.72f);
            Widgets.Label(new Rect(rowRect.x + 6f, rowRect.y + 3f, 118f, roleHeight), row.role);
            GUI.color = old;
            Widgets.Label(new Rect(rowRect.x + 124f, rowRect.y + 3f, detailWidth, nameHeight), displayName);
            if (!row.traits.NullOrEmpty())
            {
                string traitText = "HNS_LineageTraits".Translate(row.traits);
                float traitsHeight = Text.CalcHeight(traitText, detailWidth);
                GUI.color = new Color(0.72f, 0.72f, 0.72f);
                Widgets.Label(new Rect(rowRect.x + 124f, rowRect.y + 3f + topHeight + 3f, detailWidth, traitsHeight), traitText);
                GUI.color = old;
            }
            string tooltip = row.missing ? "HNS_LineageMissingParent".Translate().ToString() : row.traits;
            if (!tooltip.NullOrEmpty()) TooltipHandler.TipRegion(rowRect, tooltip);
        }
        private readonly struct LineageRow
        {
            public readonly int depth;
            public readonly string role;
            public readonly string name;
            public readonly string traits;
            public readonly bool missing;
            public readonly bool cycle;

            public LineageRow(int depth, string role, string name, string traits, bool missing, bool cycle)
            {
                this.depth = depth;
                this.role = role;
                this.name = name;
                this.traits = traits;
                this.missing = missing;
                this.cycle = cycle;
            }
        }
    }
}