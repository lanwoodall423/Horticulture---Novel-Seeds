using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace HorticultureNovelSeeds
{
    public class Dialog_TraitGroupMembers : Window
    {
        private readonly NovelSeedsSettings settings;
        private readonly string groupName;
        private string search = string.Empty;
        private HorticultureCollectionDialogDocument canvasDocument;
        private HorticultureCollectionDialogSurfaceAdapter canvasSurface;

        public override Vector2 InitialSize => new Vector2(720f, 760f);

        public Dialog_TraitGroupMembers(NovelSeedsSettings settings, string groupName)
        {
            this.settings = settings;
            this.groupName = groupName;
            doCloseX = true;
            absorbInputAroundWindow = true;
            canvasSurface = new HorticultureCollectionDialogSurfaceAdapter
            {
                TitleProvider = () => "Manage Traits - " + groupName,
                DescriptionProvider = () => groupName == "Ungrouped"
                    ? "Select traits to move them here. Clearing a trait restores its default group."
                    : "Select traits to move them into this group. Clearing a trait moves it to Ungrouped.",
                SearchProvider = () => search,
                SearchSetter = value => search = value,
                RowsProvider = TraitRows,
                EmptyProvider = () => "No traits match this search.",
                PrimaryLabelProvider = () => "Restore All Defaults",
                PrimaryActionCallback = RestoreAll,
                CloseAction = () => Close()
            };
            canvasDocument = new HorticultureCollectionDialogDocument(canvasSurface, "hns.trait-group.members");
        }

        public override void DoWindowContents(Rect inRect) => canvasDocument?.Draw(inRect);

        public override void PostClose()
        {
            canvasDocument?.PostClose();
            base.PostClose();
        }

        private IReadOnlyList<HorticultureDialogRow> TraitRows()
        {
            return TraitConfigUtility.TopLevelTraits()
                .OrderBy(trait => trait.label)
                .Take(1000)
                .Select((trait, index) =>
                {
                    string currentGroup = settings.TraitGroup(trait);
                    bool assigned = string.Equals(currentGroup, groupName, StringComparison.OrdinalIgnoreCase);
                    return new HorticultureDialogRow
                    {
                        Id = trait.defName ?? "trait-" + index,
                        Label = TraitColorUI.Label(trait),
                        Detail = assigned ? "In this group" : currentGroup,
                        Status = assigned ? "Selected" : "Inherited/default",
                        Selected = assigned,
                        CanToggle = true,
                        Toggle = value =>
                        {
                            if (value) settings.SetTraitGroup(trait, groupName);
                            else if (string.Equals(groupName, "Ungrouped", StringComparison.OrdinalIgnoreCase)) settings.ResetTraitGroup(trait);
                            else settings.SetTraitGroup(trait, "Ungrouped");
                        }
                    };
                }).ToArray();
        }

        private void RestoreAll()
        {
            foreach (VarietyTraitDef trait in TraitConfigUtility.TopLevelTraits()) settings.ResetTraitGroup(trait);
        }
    }
}
