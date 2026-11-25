using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using UnityEngine;

namespace HorticultureNovelSeeds
{
    public sealed class HorticultureInspectorRow
    {
        public string Id;
        public string Label;
        public string Detail;
        public bool Warning;
    }

    public sealed class HorticultureInspectorSnapshot
    {
        public string Title;
        public string Subtitle;
        public string PrimaryHeader;
        public string SecondaryHeader;
        public string PrimaryEmpty;
        public string SecondaryEmpty;
        public IReadOnlyList<HorticultureInspectorRow> PrimaryRows = new HorticultureInspectorRow[0];
        public IReadOnlyList<HorticultureInspectorRow> SecondaryRows = new HorticultureInspectorRow[0];
        public string ActionLabel;
        public Action Action;
    }

    public sealed class HorticultureInspectorDocument
    {
        private HorticultureInspectorSnapshot snapshot = new HorticultureInspectorSnapshot();
        private Vector2 scroll;
        private bool secondary;
        public HorticultureInspectorDocument(string id) { }
        public void PostClose() { }
        public int PrimaryRowCount => Math.Min(1000, snapshot.PrimaryRows?.Count ?? 0);
        public int SecondaryRowCount => Math.Min(1000, snapshot.SecondaryRows?.Count ?? 0);
        public void Refresh(HorticultureInspectorSnapshot next) { snapshot = next ?? new HorticultureInspectorSnapshot(); }
        public void Draw(Rect rect)
        {
            float y = rect.y;
            NativeUi.Label(rect, ref y, snapshot.Title);
            NativeUi.Label(rect, ref y, snapshot.Subtitle, 48);
            NativeUi.Button(rect, ref y, snapshot.ActionLabel, snapshot.Action);
            float w = rect.width / 2;
            if (Widgets.ButtonText(new Rect(rect.x, y, w - 3, 30), snapshot.PrimaryHeader ?? "Details")) { secondary = false; scroll = Vector2.zero; }
            if (Widgets.ButtonText(new Rect(rect.x + w, y, w - 3, 30), snapshot.SecondaryHeader ?? "Effects")) { secondary = true; scroll = Vector2.zero; }
            y += 34;
            var rows = (secondary ? snapshot.SecondaryRows : snapshot.PrimaryRows) ?? Array.Empty<HorticultureInspectorRow>();
            var view = NativeUi.Scroll(new Rect(rect.x, y, rect.width, Math.Max(1, rect.yMax - y)), ref scroll, Math.Min(rows.Count, 1000) * 86);
            try
            {
                float ry = 0;
                if (rows.Count == 0) NativeUi.Label(view, ref ry, secondary ? snapshot.SecondaryEmpty : snapshot.PrimaryEmpty);
                foreach (var row in rows.Take(1000))
                {
                    NativeUi.Label(view, ref ry, row.Label, 30);
                    NativeUi.Label(view, ref ry, row.Detail, 48);
                }
            }
            finally { Widgets.EndScrollView(); }
        }
    }
}
