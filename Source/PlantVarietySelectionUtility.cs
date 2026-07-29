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
            if (plantDef == null)
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
            if (plantDef == null || settables == null)
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
            if (plantDef == null || settables == null) return;
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
        private Vector2 scroll;

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
        }

        public override void DoWindowContents(UnityEngine.Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new UnityEngine.Rect(inRect.x, inRect.y, inRect.width, 34f), "HNS_BreedingMixTitle".Translate(plantDef.LabelCap));
            Text.Font = GameFont.Small;
            Widgets.Label(new UnityEngine.Rect(inRect.x, inRect.y + 42f, inRect.width, 48f), "HNS_BreedingMixDescription".Translate());
            UnityEngine.Rect outRect = new UnityEngine.Rect(inRect.x, inRect.y + 96f, inRect.width, inRect.height - 148f);
            UnityEngine.Rect viewRect = new UnityEngine.Rect(0f, 0f, outRect.width - 16f, varieties.Count * 54f);
            Widgets.BeginScrollView(outRect, ref scroll, viewRect);
            float y = 0f;
            foreach (VarietyRecord variety in varieties)
            {
                bool enabled = selected.Contains(variety.id);
                UnityEngine.Rect row = new UnityEngine.Rect(0f, y, viewRect.width, 50f);
                Widgets.DrawHighlightIfMouseover(row);
                Widgets.CheckboxLabeled(new UnityEngine.Rect(8f, y + 4f, row.width - 16f, 24f), variety.Label, ref enabled);
                Widgets.Label(new UnityEngine.Rect(34f, y + 27f, row.width - 42f, 22f), NovelSeedUtility.TraitSummary(variety.traits));
                if (enabled) selected.Add(variety.id); else selected.Remove(variety.id);
                y += 54f;
            }
            Widgets.EndScrollView();
            if (Widgets.ButtonText(new UnityEngine.Rect(inRect.xMax - 230f, inRect.yMax - 36f, 100f, 32f), "CancelButton".Translate())) Close();
            if (Widgets.ButtonText(new UnityEngine.Rect(inRect.xMax - 120f, inRect.yMax - 36f, 120f, 32f), "HNS_ApplyMix".Translate()))
            {
                List<VarietyRecord> mix = varieties.Where(variety => selected.Contains(variety.id)).ToList();
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
}
