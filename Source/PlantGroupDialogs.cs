using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace HorticultureNovelSeeds
{
    public class Dialog_PlantGroupName : Window, IHorticultureNamingSurface
    {
        private readonly NovelSeedsSettings settings;
        private readonly PlantGroupRecord group;
        private string groupName;
        private string validationMessage;
        private HorticultureNamingDocument canvasDocument;

        public override Vector2 InitialSize => new Vector2(520f, 250f);

        public Dialog_PlantGroupName(NovelSeedsSettings settings, PlantGroupRecord group = null)
        {
            this.settings = settings;
            this.group = group;
            groupName = group?.Name ?? string.Empty;
            doCloseX = true;
            absorbInputAroundWindow = true;
            canvasDocument = new HorticultureNamingDocument(this, "hns.plant-group.name");
        }

        public override void DoWindowContents(Rect inRect)
        {
            canvasDocument?.Draw(inRect);
        }

        public override void PostClose()
        {
            canvasDocument?.PostClose();
            base.PostClose();
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

        string IHorticultureNamingSurface.Title => group == null ? "Create Plant Group" : "Rename Plant Group";
        string IHorticultureNamingSurface.SourceLabel => "Persistent plant settings group";
        string IHorticultureNamingSurface.OriginLabel => "Membership is saved with Horticulture settings.";
        string IHorticultureNamingSurface.LineageLabel => "Assigning a plant here preserves its existing group rules.";
        IReadOnlyList<string> IHorticultureNamingSurface.TraitLabels => new string[0];
        string IHorticultureNamingSurface.Name { get => groupName; set => groupName = value ?? string.Empty; }
        string IHorticultureNamingSurface.ValidationMessage => validationMessage;
        void IHorticultureNamingSurface.Save() => Save();
        void IHorticultureNamingSurface.Cancel() => Close();
    }

    public class Dialog_PlantGroupMembers : Window
    {
        private readonly NovelSeedsSettings settings;
        private readonly PlantGroupRecord group;
        private string search = string.Empty;
        private HorticultureCollectionDialogDocument canvasDocument;
        private HorticultureCollectionDialogSurfaceAdapter canvasSurface;

        public override Vector2 InitialSize => new Vector2(720f, 760f);

        public Dialog_PlantGroupMembers(NovelSeedsSettings settings, PlantGroupRecord group)
        {
            this.settings = settings;
            this.group = group;
            doCloseX = true;
            absorbInputAroundWindow = true;
            canvasSurface = new HorticultureCollectionDialogSurfaceAdapter
            {
                TitleProvider = () => "Manage Plants - " + group.Name,
                DescriptionProvider = () => group.PlantCount + " plants use this group's settings. Selecting a plant assigned elsewhere moves it here.",
                SearchProvider = () => search,
                SearchSetter = value => search = value,
                RowsProvider = PlantRows,
                EmptyProvider = () => "No growable plants match this search.",
                CloseAction = () => Close()
            };
            canvasDocument = new HorticultureCollectionDialogDocument(canvasSurface, "hns.plant-group.members");
        }

        public override void DoWindowContents(Rect inRect)
        {
            canvasDocument?.Draw(inRect);
        }

        public override void PostClose()
        {
            canvasDocument?.PostClose();
            base.PostClose();
        }

        private IReadOnlyList<HorticultureDialogRow> PlantRows()
        {
            return DefDatabase<ThingDef>.AllDefsListForReading
                .Where(NovelSeedUtility.IsGrowableCrop)
                .OrderBy(plant => plant.label)
                .Take(1000)
                .Select((plant, index) => new HorticultureDialogRow
                {
                    Id = plant.defName ?? "plant-" + index,
                    Label = plant.LabelCap.ToString(),
                    Detail = settings.GroupForPlant(plant)?.Name ?? "Default group",
                    Status = settings.GroupForPlant(plant) == group ? "In this group" : "Other group",
                    Selected = group.Contains(plant),
                    CanToggle = true,
                    Toggle = value =>
                    {
                        if (value) settings.AssignPlantToGroup(plant, group);
                        else settings.RemovePlantFromGroup(plant);
                    }
                }).ToArray();
        }
    }
}
