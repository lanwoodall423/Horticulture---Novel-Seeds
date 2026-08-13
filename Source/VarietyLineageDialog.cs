using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace HorticultureNovelSeeds
{
    /// <summary>
    /// Compatibility shim for external callers compiled against the old lineage dialog.
    /// PlantVarietyTab now routes directly to the persistent Insight Canvas workspace; no
    /// second manual lineage renderer is retained.
    /// </summary>
    [Obsolete("Lineage is presented by HorticultureWorkspaceDocument.")]
    public sealed class Dialog_VarietyLineage : Window
    {
        private readonly string varietyName;
        private readonly List<string> parentIds;

        public override Vector2 InitialSize => new Vector2(420f, 160f);

        public Dialog_VarietyLineage(string varietyName, IEnumerable<VarietyTraitDef> currentTraits,
            IEnumerable<string> parentIds)
        {
            this.varietyName = varietyName ?? string.Empty;
            this.parentIds = parentIds?.Where(id => !id.NullOrEmpty()).Distinct().ToList() ?? new List<string>();
            doCloseX = true;
            closeOnClickedOutside = true;
        }

        public override void PreOpen()
        {
            base.PreOpen();
            VarietyRecord variety = GameComponent_NovelSeeds.Instance?.AllVarieties
                .FirstOrDefault(record => record != null && string.Equals(record.Label, varietyName, StringComparison.Ordinal)
                    && (parentIds.Count == 0 || parentIds.All(id => record.parentVarietyIds?.Contains(id) == true)));
            if (variety != null) MainTabWindow_CultivarRegistry.OpenLineage(variety);
            Close();
        }

        public override void DoWindowContents(Rect inRect)
        {
            // The window closes during PreOpen. This body only keeps the compatibility type safe
            // for callers that instantiate it outside a WindowStack.
        }
    }
}
