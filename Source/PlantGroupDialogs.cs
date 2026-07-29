using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace HorticultureNovelSeeds
{
    public class Dialog_PlantGroupName : Window
    {
        private readonly NovelSeedsSettings settings;
        private readonly PlantGroupRecord group;
        private string groupName;
        private string validationMessage;
        private bool focused;

        public override Vector2 InitialSize => new Vector2(520f, 250f);

        public Dialog_PlantGroupName(NovelSeedsSettings settings, PlantGroupRecord group = null)
        {
            this.settings = settings;
            this.group = group;
            groupName = group?.Name ?? string.Empty;
            doCloseX = true;
            absorbInputAroundWindow = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 32f), group == null ? "Create Plant Group" : "Rename Plant Group");
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(inRect.x, inRect.y + 48f, inRect.width, 24f), "Group Name");
            GUI.SetNextControlName("HNS_PlantGroupName");
            groupName = Widgets.TextField(new Rect(inRect.x, inRect.y + 76f, inRect.width, 32f), groupName, 64);
            if (!focused)
            {
                GUI.FocusControl("HNS_PlantGroupName");
                focused = true;
            }
            if (!validationMessage.NullOrEmpty())
            {
                Color old = GUI.color;
                GUI.color = ColorLibrary.RedReadable;
                Widgets.Label(new Rect(inRect.x, inRect.y + 114f, inRect.width, 24f), validationMessage);
                GUI.color = old;
            }
            if (Widgets.ButtonText(new Rect(inRect.xMax - 220f, inRect.yMax - 36f, 100f, 30f), "Cancel")) Close();
            if (Widgets.ButtonText(new Rect(inRect.xMax - 110f, inRect.yMax - 36f, 110f, 30f), group == null ? "Create" : "Rename")) Save();
        }

        public override void OnAcceptKeyPressed()
        {
            Save();
        }

        private void Save()
        {
            string normalized = groupName?.Trim();
            if (normalized.NullOrEmpty())
            {
                validationMessage = "Enter a group name.";
                return;
            }
            bool duplicate = settings.PlantGroups.Any(other => other != group && other.Name.Equals(normalized, System.StringComparison.OrdinalIgnoreCase));
            if (duplicate)
            {
                validationMessage = "A group with this name already exists.";
                return;
            }
            if (group == null) settings.CreatePlantGroup(normalized); else settings.RenamePlantGroup(group, normalized);
            Close();
        }
    }

    public class Dialog_PlantGroupMembers : Window
    {
        private readonly NovelSeedsSettings settings;
        private readonly PlantGroupRecord group;
        private Vector2 scrollPosition;
        private string search = string.Empty;

        public override Vector2 InitialSize => new Vector2(720f, 760f);

        public Dialog_PlantGroupMembers(NovelSeedsSettings settings, PlantGroupRecord group)
        {
            this.settings = settings;
            this.group = group;
            doCloseX = true;
            absorbInputAroundWindow = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 32f), "Manage Plants - " + group.Name);
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(inRect.x, inRect.y + 40f, inRect.width, 24f), group.PlantCount + " plants use this group's settings. Selecting a plant assigned elsewhere moves it here.");
            search = Widgets.TextField(new Rect(inRect.x, inRect.y + 72f, inRect.width, 30f), search ?? string.Empty);

            List<ThingDef> plants = DefDatabase<ThingDef>.AllDefsListForReading
                .Where(NovelSeedUtility.IsGrowableCrop)
                .Where(plant => search.NullOrEmpty() || plant.LabelCap.ToString().IndexOf(search, System.StringComparison.OrdinalIgnoreCase) >= 0 || plant.defName.IndexOf(search, System.StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderBy(plant => plant.label)
                .ToList();

            Rect outer = new Rect(inRect.x, inRect.y + 114f, inRect.width, inRect.height - 164f);
            Rect view = new Rect(0f, 0f, outer.width - 18f, Mathf.Max(outer.height, plants.Count * 38f));
            Widgets.BeginScrollView(outer, ref scrollPosition, view);
            float y = 0f;
            foreach (ThingDef plant in plants)
            {
                Rect row = new Rect(0f, y, view.width, 36f);
                Widgets.DrawHighlightIfMouseover(row);
                bool assigned = group.Contains(plant);
                bool previous = assigned;
                Widgets.CheckboxLabeled(new Rect(8f, y + 4f, view.width - 220f, 28f), plant.LabelCap, ref assigned);
                PlantGroupRecord current = settings.GroupForPlant(plant);
                if (current != null)
                {
                    Color old = GUI.color;
                    GUI.color = current == group ? new Color(0.45f, 0.82f, 0.54f) : new Color(0.72f, 0.72f, 0.72f);
                    TextAnchor oldAnchor = Text.Anchor;
                    Text.Anchor = TextAnchor.MiddleRight;
                    Widgets.Label(new Rect(view.width - 210f, y + 4f, 198f, 28f), current == group ? "In This Group" : current.Name);
                    Text.Anchor = oldAnchor;
                    GUI.color = old;
                }
                if (assigned != previous)
                {
                    if (assigned) settings.AssignPlantToGroup(plant, group); else settings.RemovePlantFromGroup(plant);
                }
                y += 38f;
            }
            Widgets.EndScrollView();
            if (Widgets.ButtonText(new Rect(inRect.xMax - 110f, inRect.yMax - 34f, 110f, 30f), "Done")) Close();
        }
    }
}