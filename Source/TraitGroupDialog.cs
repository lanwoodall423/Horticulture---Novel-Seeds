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
        private Vector2 scrollPosition;
        private string search = string.Empty;

        public override Vector2 InitialSize => new Vector2(720f, 760f);

        public Dialog_TraitGroupMembers(NovelSeedsSettings settings, string groupName)
        {
            this.settings = settings;
            this.groupName = groupName;
            doCloseX = true;
            absorbInputAroundWindow = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 32f), "Manage Traits - " + groupName);
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(inRect.x, inRect.y + 40f, inRect.width, 24f),
                groupName == "Ungrouped"
                    ? "Select traits to move them here. Clearing a trait restores its default group."
                    : "Select traits to move them into this group. Clearing a trait moves it to Ungrouped.");
            search = Widgets.TextField(new Rect(inRect.x, inRect.y + 72f, inRect.width, 30f), search ?? string.Empty);

            List<VarietyTraitDef> traits = TraitConfigUtility.TopLevelTraits()
                .Where(trait => search.NullOrEmpty()
                    || trait.LabelCap.ToString().IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0
                    || trait.defName.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0
                    || settings.TraitGroup(trait).IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderBy(trait => trait.label)
                .ToList();

            Rect outer = new Rect(inRect.x, inRect.y + 114f, inRect.width, inRect.height - 164f);
            Rect view = new Rect(0f, 0f, outer.width - 18f, Mathf.Max(outer.height, traits.Count * 42f));
            Widgets.BeginScrollView(outer, ref scrollPosition, view);
            float y = 0f;
            foreach (VarietyTraitDef trait in traits)
            {
                Rect row = new Rect(0f, y, view.width, 40f);
                Widgets.DrawHighlightIfMouseover(row);
                string currentGroup = settings.TraitGroup(trait);
                bool assigned = currentGroup.Equals(groupName, StringComparison.OrdinalIgnoreCase);
                bool previous = assigned;
                Widgets.CheckboxLabeled(new Rect(8f, y + 6f, view.width - 250f, 28f), TraitColorUI.Label(trait), ref assigned);

                Color oldColor = GUI.color;
                TextAnchor oldAnchor = Text.Anchor;
                GUI.color = assigned ? new Color(0.45f, 0.82f, 0.54f) : new Color(0.72f, 0.72f, 0.72f);
                Text.Anchor = TextAnchor.MiddleRight;
                Widgets.Label(new Rect(view.width - 238f, y + 6f, 226f, 28f), assigned ? "In This Group" : currentGroup);
                Text.Anchor = oldAnchor;
                GUI.color = oldColor;

                if (!trait.description.NullOrEmpty()) TooltipHandler.TipRegion(row, TraitColorUI.Tooltip(trait));
                if (assigned != previous)
                {
                    if (assigned) settings.SetTraitGroup(trait, groupName);
                    else if (groupName.Equals("Ungrouped", StringComparison.OrdinalIgnoreCase)) settings.ResetTraitGroup(trait);
                    else settings.SetTraitGroup(trait, "Ungrouped");
                }
                y += 42f;
            }
            Widgets.EndScrollView();

            if (Widgets.ButtonText(new Rect(inRect.x, inRect.yMax - 34f, 178f, 30f), "Restore All Defaults"))
            {
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                    "Restore every trait to its default XML-defined group? Other trait settings will be kept.",
                    delegate
                    {
                        foreach (VarietyTraitDef trait in TraitConfigUtility.TopLevelTraits()) settings.ResetTraitGroup(trait);
                    }, true));
            }
            if (Widgets.ButtonText(new Rect(inRect.xMax - 110f, inRect.yMax - 34f, 110f, 30f), "Done")) Close();
        }
    }
}