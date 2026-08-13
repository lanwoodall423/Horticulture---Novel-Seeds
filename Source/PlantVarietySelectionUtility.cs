using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace HorticultureNovelSeeds
{
    public static class PlantVarietySelectionUtility
    {
        public static void OpenVarietyMenu(List<IPlantToGrowSettable> settables, ThingDef plantDef, IEnumerable<VarietyRecord> varieties, Action<VarietyRecord> afterSelect = null, Action<List<IPlantToGrowSettable>, ThingDef> applyPlantDef = null)
        {
            if (!HorticulturePlantPolicy.IsSupported(plantDef))
            {
                return;
            }

            List<VarietyRecord> varietyList = varieties?.Where(v => v != null).OrderBy(v => v.Label).ToList() ?? new List<VarietyRecord>();
            List<FloatMenuOption> options = new List<FloatMenuOption>();
            string standardLabel = "HNS_SelectStandard".Translate(plantDef.label.CapitalizeFirst());
            if (ExpandedTraitUtility.StandardPlantMatchesGrowers(plantDef, settables))
            {
                options.Add(new FloatMenuOption(standardLabel, delegate
                {
                    SelectPlant(settables, plantDef, null, applyPlantDef);
                    afterSelect?.Invoke(null);
                }, plantDef));
            }
            else
            {
                options.Add(new FloatMenuOption(standardLabel + " (" + "HNS_RequiresZone".Translate("matching") + ")", null, plantDef));
            }

            foreach (VarietyRecord variety in varietyList)
            {
                VarietyRecord localVariety = variety;
                string optionLabel = "HNS_SelectVariety".Translate(localVariety.Label, NovelSeedUtility.TraitSummary(localVariety.traits));
                if (!ExpandedTraitUtility.VarietyMatchesGrowers(localVariety, settables))
                {
                    optionLabel += " (" + "HNS_RequiresZone".Translate(ExpandedTraitUtility.ZoneLabel(localVariety)) + ")";
                    options.Add(new FloatMenuOption(optionLabel, null, plantDef));
                }
                else
                {
                    options.Add(new FloatMenuOption(optionLabel, delegate
                    {
                        SelectPlant(settables, plantDef, localVariety, applyPlantDef);
                        afterSelect?.Invoke(localVariety);
                    }, plantDef));
                }
            }

            List<VarietyRecord> breedingCandidates = varietyList
                .Where(variety => ExpandedTraitUtility.VarietyMatchesGrowers(variety, settables)).ToList();
            if (breedingCandidates.Count >= 2)
            {
                options.Add(new FloatMenuOption("HNS_SelectBreedingMix".Translate(), delegate
                {
                    Find.WindowStack.Add(new Dialog_BreedingMix(settables, plantDef, breedingCandidates, afterSelect, applyPlantDef));
                }, plantDef));
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }

        public static void SelectPlant(List<IPlantToGrowSettable> settables, ThingDef plantDef, VarietyRecord variety, Action<List<IPlantToGrowSettable>, ThingDef> applyPlantDef = null)
        {
            if (!HorticulturePlantPolicy.IsSupported(plantDef) || settables == null)
            {
                return;
            }

            List<IPlantToGrowSettable> validSettables = settables.Where(s => s != null).Distinct().ToList();
            if (applyPlantDef != null)
            {
                applyPlantDef(validSettables, plantDef);
                foreach (IPlantToGrowSettable settable in validSettables)
                {
                    GameComponent_NovelSeeds.Instance?.SetSelectedVariety(settable, variety);
                }
            }
            else
            {
                foreach (IPlantToGrowSettable settable in validSettables)
                {
                    settable.SetPlantDefToGrow(plantDef);
                    GameComponent_NovelSeeds.Instance?.SetSelectedVariety(settable, variety);
                }
            }

            PlayerKnowledgeDatabase.KnowledgeDemonstrated(ConceptDefOf.SetGrowingZonePlant, KnowledgeAmount.Total);
        }

        public static void SelectBreedingMix(List<IPlantToGrowSettable> settables, ThingDef plantDef, IEnumerable<VarietyRecord> varieties, Action<List<IPlantToGrowSettable>, ThingDef> applyPlantDef = null)
        {
            if (!HorticulturePlantPolicy.IsSupported(plantDef) || settables == null) return;
            List<IPlantToGrowSettable> validSettables = settables.Where(settable => settable != null).Distinct().ToList();
            List<VarietyRecord> mix = varieties?.Where(variety => variety?.cropDef == plantDef).Distinct().ToList() ?? new List<VarietyRecord>();
            if (mix.Count < 2) return;
            if (applyPlantDef != null) applyPlantDef(validSettables, plantDef);
            else foreach (IPlantToGrowSettable settable in validSettables) settable.SetPlantDefToGrow(plantDef);
            foreach (IPlantToGrowSettable settable in validSettables) GameComponent_NovelSeeds.Instance?.SetBreedingMix(settable, mix);
            PlayerKnowledgeDatabase.KnowledgeDemonstrated(ConceptDefOf.SetGrowingZonePlant, KnowledgeAmount.Total);
        }
    }

    public sealed class Dialog_BreedingMix : Window
    {
        private readonly List<IPlantToGrowSettable> settables;
        private readonly ThingDef plantDef;
        private readonly List<VarietyRecord> varieties;
        private readonly HashSet<string> selected = new HashSet<string>();
        private readonly Action<VarietyRecord> afterSelect;
        private readonly Action<List<IPlantToGrowSettable>, ThingDef> applyPlantDef;
        private string search = string.Empty;
        private HorticultureCollectionDialogDocument canvasDocument;
        private HorticultureCollectionDialogSurfaceAdapter canvasSurface;

        public override Vector2 InitialSize => new Vector2(620f, 640f);

        public Dialog_BreedingMix(List<IPlantToGrowSettable> settables, ThingDef plantDef, List<VarietyRecord> varieties,
            Action<VarietyRecord> afterSelect, Action<List<IPlantToGrowSettable>, ThingDef> applyPlantDef)
        {
            this.settables = settables;
            this.plantDef = plantDef;
            this.varieties = varieties.OrderBy(variety => variety.Label).ToList();
            this.afterSelect = afterSelect;
            this.applyPlantDef = applyPlantDef;
            IReadOnlyList<VarietyRecord> current = settables?.Select(settable => GameComponent_NovelSeeds.Instance?.BreedingVarietiesFor(settable))
                .FirstOrDefault(mix => mix != null && mix.Count >= 2);
            if (current != null) foreach (VarietyRecord variety in current) selected.Add(variety.id);
            doCloseX = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = false;
            canvasSurface = new HorticultureCollectionDialogSurfaceAdapter
            {
                TitleProvider = () => "Breeding mix - " + plantDef.LabelCap,
                DescriptionProvider = () => "Choose at least two compatible cultivars. The existing breeding selection authority applies the mix when you save.",
                SearchProvider = () => search,
                SearchSetter = value => search = value,
                RowsProvider = VarietyRows,
                EmptyProvider = () => "No compatible cultivars match this search.",
                PrimaryLabelProvider = () => "Apply mix",
                PrimaryActionCallback = Apply,
                SecondaryLabelProvider = () => "Clear selection",
                SecondaryActionCallback = () => selected.Clear(),
                CloseAction = () => Close()
            };
            canvasDocument = new HorticultureCollectionDialogDocument(canvasSurface, "hns.breeding-mix");
        }

        public override void DoWindowContents(UnityEngine.Rect inRect) => canvasDocument?.Draw(inRect);

        public override void PostClose()
        {
            canvasDocument?.PostClose();
            base.PostClose();
        }

        private IReadOnlyList<HorticultureDialogRow> VarietyRows()
        {
            string query = search?.Trim();
            return varieties
                .Where(variety => variety != null)
                .Where(variety => string.IsNullOrEmpty(query)
                    || variety.Label.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
                    || NovelSeedUtility.TraitSummary(variety.traits).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                .Select((variety, index) => new HorticultureDialogRow
                {
                    Id = "cultivar-" + index,
                    Label = variety.Label,
                    Detail = NovelSeedUtility.TraitSummary(variety.traits),
                    Status = selected.Contains(variety.id) ? "Selected" : "Available",
                    Selected = selected.Contains(variety.id),
                    CanToggle = true,
                    Toggle = value =>
                    {
                        if (value) selected.Add(variety.id); else selected.Remove(variety.id);
                    }
                }).ToArray();
        }

        private void Apply()
        {
            List<VarietyRecord> mix = varieties.Where(variety => variety != null && selected.Contains(variety.id)).ToList();
            if (mix.Count < 2)
            {
                Messages.Message("HNS_BreedingMixRequiresTwo".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }
            PlantVarietySelectionUtility.SelectBreedingMix(settables, plantDef, mix, applyPlantDef);
            afterSelect?.Invoke(null);
            Close();
        }
    }
}
