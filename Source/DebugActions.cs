using System.Collections.Generic;
using System.Linq;
using LudeonTK;
using RimWorld;
using UnityEngine;
using Verse;

namespace HorticultureNovelSeeds
{
    public static class NovelSeedsDebugActions
    {
        private const string Category = "Horticulture - Novel Seeds";
        private const int MaxGenerationDepth = 10;
        private const int RandomGridMaximumSpecies = 100;

        [DebugAction(Category, "Show species color palettes", actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.Playing)]
        private static void ShowSpeciesColorPalettes()
        {
            IReadOnlyList<SpeciesColorPaletteRecord> palettes = GameComponent_NovelSeeds.Instance?.SpeciesColorPalettes;
            if (palettes == null) return;
            Find.WindowStack.Add(new Dialog_MessageBox(string.Join("\n", palettes.OrderBy(record => record.PlantDef?.label)
                .Select(record => (record.PlantDef?.LabelCap.ToString() ?? record.plantDefName) + ": "
                    + string.Join("  ", record.Colors.Select(ColorHex).ToArray())).ToArray()), "Species Color Palettes"));
        }

        private static string ColorHex(Color color)
        {
            Color32 value = color;
            return "#" + value.r.ToString("X2") + value.g.ToString("X2") + value.b.ToString("X2");
        }

        [DebugAction(Category, "Unlock custom variety", actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.Playing)]
        private static void UnlockCustomVariety()
        {
            Find.WindowStack.Add(new Dialog_DevUnlockVariety());
        }

        [DebugAction(Category, "Spawn random variety seed pack", actionType = DebugActionType.ToolMap, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void SpawnRandomVarietySeedPack()
        {
            if (!TryCreateRandomNovelSeed(out ThingDef cropDef, out List<VarietyTraitDef> traits))
            {
                Messages.Message("Could not generate a new random variety with the current trait settings.", MessageTypeDefOf.RejectInput, false);
                return;
            }
            SpawnSeedPack(UI.MouseCell(), cropDef, traits, null);
            Messages.Message("Spawned random " + cropDef.LabelCap + " variety seed pack.", MessageTypeDefOf.TaskCompletion, false);
        }

        [DebugAction(Category, "Plant 10x10 random varieties", actionType = DebugActionType.ToolMap, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void PlantRandomVarietyGrid()
        {
            Map map = Find.CurrentMap;
            List<List<VarietyRecord>> varietiesByPlant = PrepareRandomGridVarieties(GameComponent_NovelSeeds.Instance);
            if (map == null || varietiesByPlant.Count == 0)
            {
                Messages.Message("No active unlocked varieties are available to plant.", MessageTypeDefOf.RejectInput, false);
                return;
            }
            PlantRandomVarietyGrid(UI.MouseCell(), map, varietiesByPlant);
        }

        internal static int PlantRandomVarietyGrid(IntVec3 center, Map map, List<List<VarietyRecord>> varietiesByPlant)
        {
            if (map == null || varietiesByPlant == null || varietiesByPlant.Count == 0) return 0;
            int planted = 0;
            int skipped = 0;
            List<List<VarietyRecord>> availableSpecies = varietiesByPlant.ToList();
            Dictionary<ThingDef, int> plantedBySpecies = availableSpecies.ToDictionary(group => group[0].cropDef, group => 0);
            foreach (IntVec3 cell in RandomGridCells(center))
            {
                if (!cell.InBounds(map) || cell.GetEdifice(map) != null || map.fertilityGrid.FertilityAt(cell) <= 0f)
                {
                    skipped++;
                    continue;
                }

                bool occupied = false;
                while (availableSpecies.Count > 0 && !occupied)
                {
                    List<VarietyRecord> speciesVarieties = RandomLeastUsedSpecies(availableSpecies, plantedBySpecies);
                    VarietyRecord variety = speciesVarieties.RandomElement();
                    Plant plant = ThingMaker.MakeThing(variety.cropDef) as Plant;
                    CompPlantVariety comp = plant?.TryGetComp<CompPlantVariety>();
                    if (plant == null || comp == null)
                    {
                        plant?.Destroy(DestroyMode.Vanish);
                        availableSpecies.Remove(speciesVarieties);
                        continue;
                    }
                    comp.SetVariety(variety);
                    plant.HitPoints = plant.MaxHitPoints;
                    plant.Growth = 1f;
                    plant.sown = true;
                    Plant existing = cell.GetPlant(map);
                    if (existing != null) existing.Destroy(DestroyMode.Vanish);
                    GenSpawn.Spawn(plant, cell, map);
                    if (plant.Spawned && plant.Position == cell)
                    {
                        plantedBySpecies[variety.cropDef]++;
                        planted++;
                        occupied = true;
                    }
                    else
                    {
                        if (!plant.Destroyed) plant.Destroy(DestroyMode.Vanish);
                        availableSpecies.Remove(speciesVarieties);
                    }
                }
                if (!occupied) skipped++;
            }
            Messages.Message("Planted " + planted + " random-variety plants in a 10x10 grid"
                + (skipped > 0 ? "; skipped " + skipped + " blocked, non-growing, or out-of-bounds cells." : "."),
                planted > 0 ? MessageTypeDefOf.TaskCompletion : MessageTypeDefOf.RejectInput, false);
            return planted;
        }

        internal static List<List<VarietyRecord>> PrepareRandomGridVarieties(GameComponent_NovelSeeds registry)
        {
            if (registry == null) return new List<List<VarietyRecord>>();
            List<List<VarietyRecord>> groups = RandomGridVarieties(registry.AllVarieties);
            List<ThingDef> crops = GrowableCrops();
            int targetSpecies = Mathf.Min(RandomGridMaximumSpecies, crops.Count);
            if (groups.Count >= targetSpecies) return groups;

            HashSet<ThingDef> represented = new HashSet<ThingDef>(groups.Select(group => group[0].cropDef));
            foreach (ThingDef crop in crops.Where(crop => !represented.Contains(crop)).InRandomOrder())
            {
                VarietyRecord variety = CreateRandomGridVariety(registry, crop);
                if (variety != null) represented.Add(crop);
                if (represented.Count >= targetSpecies) break;
            }
            return RandomGridVarieties(registry.AllVarieties);
        }

        internal static List<VarietyRecord> RandomGridSelections(List<List<VarietyRecord>> varietiesByPlant, int count)
        {
            List<VarietyRecord> selections = new List<VarietyRecord>(count);
            if (varietiesByPlant == null || varietiesByPlant.Count == 0 || count <= 0) return selections;
            Dictionary<ThingDef, int> selectedBySpecies = varietiesByPlant.ToDictionary(group => group[0].cropDef, group => 0);
            while (selections.Count < count)
            {
                List<VarietyRecord> speciesVarieties = RandomLeastUsedSpecies(varietiesByPlant, selectedBySpecies);
                selections.Add(speciesVarieties.RandomElement());
                selectedBySpecies[speciesVarieties[0].cropDef]++;
            }
            return selections;
        }

        private static List<VarietyRecord> RandomLeastUsedSpecies(List<List<VarietyRecord>> varietiesByPlant,
            Dictionary<ThingDef, int> selectedBySpecies)
        {
            int minimum = varietiesByPlant.Min(group => selectedBySpecies[group[0].cropDef]);
            return varietiesByPlant.Where(group => selectedBySpecies[group[0].cropDef] == minimum).RandomElement();
        }

        private static VarietyRecord CreateRandomGridVariety(GameComponent_NovelSeeds registry, ThingDef crop)
        {
            for (int attempt = 0; attempt < 24; attempt++)
            {
                List<VarietyTraitDef> traits = NovelSeedUtility.RandomTraitSet(crop);
                if (traits.Count == 0) continue;
                VarietyRecord existing = registry.FindMatchingVariety(crop, traits);
                if (existing != null)
                {
                    existing.registryArchived = false;
                    return existing;
                }
                return registry.UnlockVariety(crop, traits, "DEV grid " + crop.LabelCap);
            }
            return null;
        }

        internal static List<List<VarietyRecord>> RandomGridVarieties(IEnumerable<VarietyRecord> varieties)
        {
            return varieties?.Where(variety => variety?.cropDef != null && NovelSeedUtility.IsGrowableCrop(variety.cropDef)
                    && !variety.registryArchived && !variety.id.NullOrEmpty())
                .GroupBy(variety => variety.cropDef)
                .Select(group => group.GroupBy(variety => variety.id).Select(records => records.First()).ToList())
                .Where(group => group.Count > 0).ToList() ?? new List<List<VarietyRecord>>();
        }

        internal static IEnumerable<IntVec3> RandomGridCells(IntVec3 center)
        {
            int minX = center.x - 5;
            int minZ = center.z - 5;
            for (int z = 0; z < 10; z++)
                for (int x = 0; x < 10; x++)
                    yield return new IntVec3(minX + x, 0, minZ + z);
        }

        internal static bool RandomVarietyGridRegression()
        {
            ThingDef firstCrop = new ThingDef { defName = "HNS_GridRegressionA", plant = new PlantProperties { sowTags = new List<string> { "Ground" } } };
            ThingDef secondCrop = new ThingDef { defName = "HNS_GridRegressionB", plant = new PlantProperties { sowTags = new List<string> { "Ground" } } };
            ThingDef thirdCrop = new ThingDef { defName = "HNS_GridRegressionC", plant = new PlantProperties { sowTags = new List<string> { "Ground" } } };
            firstCrop.plant.sowMinSkill = 0;
            secondCrop.plant.sowMinSkill = 0;
            thirdCrop.plant.sowMinSkill = 0;
            List<VarietyRecord> records = new List<VarietyRecord>
            {
                new VarietyRecord { id = "a1", cropDef = firstCrop },
                new VarietyRecord { id = "a2", cropDef = firstCrop },
                new VarietyRecord { id = "a2", cropDef = firstCrop },
                new VarietyRecord { id = "archived", cropDef = secondCrop, registryArchived = true },
                new VarietyRecord { id = "b1", cropDef = secondCrop },
                new VarietyRecord { id = "c1", cropDef = thirdCrop }
            };
            List<List<VarietyRecord>> grouped = RandomGridVarieties(records);
            List<VarietyRecord> selections = RandomGridSelections(grouped, 8);
            List<int> speciesCounts = selections.GroupBy(variety => variety.cropDef).Select(group => group.Count()).ToList();
            List<IntVec3> cells = RandomGridCells(new IntVec3(20, 0, 30)).ToList();
            return grouped.Count == 3 && grouped.Any(group => group.Count == 2) && grouped.Count(group => group.Count == 1) == 2
                && grouped.SelectMany(group => group).All(variety => !variety.registryArchived)
                && selections.Count == 8 && selections.Take(3).Select(variety => variety.cropDef).Distinct().Count() == 3
                && selections.Skip(3).Take(3).Select(variety => variety.cropDef).Distinct().Count() == 3
                && speciesCounts.Max() - speciesCounts.Min() <= 1
                && cells.Count == 100 && cells.Distinct().Count() == 100
                && cells.Min(cell => cell.x) == 15 && cells.Max(cell => cell.x) == 24
                && cells.Min(cell => cell.z) == 25 && cells.Max(cell => cell.z) == 34;
        }

        internal static bool CrossPollinationRegression()
        {
            return HorticultureNovelSeeds.CrossPollinationRegression.Run();
        }

        internal static bool TraitCatalogRegression()
        {
            return HorticultureNovelSeeds.TraitCatalogRegression.Run();
        }

        [DebugAction(Category, "Spawn crossbred seed pack by generation", actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void ChooseCrossbredGenerationDepth()
        {
            Find.WindowStack.Add(new Dialog_Slider(
                generations => generations + (generations == 1 ? " generation" : " generations") + " of cross-pollination",
                1,
                MaxGenerationDepth,
                BeginCrossbredSpawnTool,
                3));
        }

        private static void BeginCrossbredSpawnTool(int generations)
        {
            int depth = Mathf.Clamp(generations, 1, MaxGenerationDepth);
            DebugTools.curTool = new DebugTool("Spawn random variety seed pack (" + depth + " cross generations)", delegate
            {
                SpawnCrossbredSeedPack(UI.MouseCell(), depth);
            });
        }

        private static void SpawnCrossbredSeedPack(IntVec3 cell, int generations)
        {
            if (!TryBuildCrossPlan(generations, out CrossPlan plan))
            {
                Messages.Message("Could not build " + generations + " valid cross-pollination generations with the current trait settings.", MessageTypeDefOf.RejectInput, false);
                return;
            }

            GameComponent_NovelSeeds registry = GameComponent_NovelSeeds.Instance;
            if (registry == null)
            {
                Messages.Message("The variety registry is unavailable.", MessageTypeDefOf.RejectInput, false);
                return;
            }

            string prefix = "DEV " + plan.cropDef.LabelCap;
            VarietyRecord current = registry.UnlockVariety(plan.cropDef, plan.founderTraits, prefix + " founder", null, true);
            List<string> finalParentIds = null;
            List<VarietyTraitDef> finalTraits = null;
            for (int generation = 0; generation < plan.childTraits.Count; generation++)
            {
                VarietyRecord donor = registry.UnlockVariety(plan.cropDef, plan.donorTraits[generation], prefix + " donor " + (generation + 1), null, true);
                List<string> parentIds = new[] { current.id, donor.id }.Where(id => !id.NullOrEmpty()).Distinct().ToList();
                if (generation == plan.childTraits.Count - 1)
                {
                    finalParentIds = parentIds;
                    finalTraits = plan.childTraits[generation];
                }
                else
                {
                    current = registry.UnlockVariety(plan.cropDef, plan.childTraits[generation], prefix + " generation " + (generation + 1), parentIds, true);
                }
            }

            SpawnSeedPack(cell, plan.cropDef, finalTraits, finalParentIds);
            Messages.Message("Spawned a " + generations + "-generation " + plan.cropDef.LabelCap + " crossbred seed pack. Its hidden ancestry records were generated for the Lineage view.", MessageTypeDefOf.TaskCompletion, false);
        }

        private static bool TryCreateRandomNovelSeed(out ThingDef cropDef, out List<VarietyTraitDef> traits)
        {
            cropDef = null;
            traits = null;
            List<ThingDef> crops = GrowableCrops();
            GameComponent_NovelSeeds registry = GameComponent_NovelSeeds.Instance;
            for (int attempt = 0; attempt < 120 && crops.Count > 0; attempt++)
            {
                ThingDef crop = crops.RandomElement();
                List<VarietyTraitDef> generated = NovelSeedUtility.RandomTraitSet(crop);
                if (generated.Count == 0 || registry?.FindMatchingVariety(crop, generated) != null) continue;
                cropDef = crop;
                traits = generated;
                return true;
            }
            return false;
        }

        private static bool TryBuildCrossPlan(int generations, out CrossPlan plan)
        {
            plan = null;
            List<ThingDef> crops = GrowableCrops();
            GameComponent_NovelSeeds registry = GameComponent_NovelSeeds.Instance;
            if (registry == null || crops.Count == 0) return false;

            for (int planAttempt = 0; planAttempt < 80; planAttempt++)
            {
                ThingDef crop = crops.RandomElement();
                List<VarietyTraitDef> founderCandidates = NovelSeedUtility.RandomTraitSet(crop);
                if (founderCandidates.Count == 0) continue;
                List<VarietyTraitDef> current = new List<VarietyTraitDef> { founderCandidates.RandomElement() };
                CrossPlan candidatePlan = new CrossPlan(crop, current);
                HashSet<string> plannedChildren = new HashSet<string>();
                bool failed = false;

                for (int generation = 0; generation < generations; generation++)
                {
                    VarietyTraitDef addition = null;
                    List<VarietyTraitDef> combined = null;
                    for (int traitAttempt = 0; traitAttempt < 80 && addition == null; traitAttempt++)
                    {
                        List<VarietyTraitDef> additions = NovelSeedUtility.RandomTraitSet(crop, current);
                        foreach (VarietyTraitDef possible in additions.InRandomOrder())
                        {
                            List<VarietyTraitDef> proposed = current.Concat(new[] { possible }).Where(trait => trait != null).Distinct().ToList();
                            string key = NovelSeedUtility.TraitKey(proposed);
                            if (plannedChildren.Contains(key) || registry.FindMatchingVariety(crop, proposed) != null) continue;
                            addition = possible;
                            combined = proposed;
                            break;
                        }
                    }
                    if (addition == null)
                    {
                        failed = true;
                        break;
                    }

                    candidatePlan.donorTraits.Add(new List<VarietyTraitDef> { addition });
                    candidatePlan.childTraits.Add(combined);
                    plannedChildren.Add(NovelSeedUtility.TraitKey(combined));
                    current = combined;
                }

                if (!failed)
                {
                    plan = candidatePlan;
                    return true;
                }
            }
            return false;
        }

        private static void SpawnSeedPack(IntVec3 cell, ThingDef cropDef, List<VarietyTraitDef> traits, IEnumerable<string> parentIds)
        {
            if (Find.CurrentMap == null || !cell.InBounds(Find.CurrentMap) || cropDef == null || traits == null || traits.Count == 0) return;
            Thing seedPack = ThingMaker.MakeThing(HNS_DefOf.HNS_NovelSeedPack);
            seedPack.TryGetComp<CompNovelSeedPack>()?.Initialize(cropDef, traits, parentIds);
            GenPlace.TryPlaceThing(seedPack, cell, Find.CurrentMap, ThingPlaceMode.Near);
        }

        private static List<ThingDef> GrowableCrops()
        {
            return DefDatabase<ThingDef>.AllDefsListForReading.Where(NovelSeedUtility.IsGrowableCrop).ToList();
        }

        private sealed class CrossPlan
        {
            public readonly ThingDef cropDef;
            public readonly List<VarietyTraitDef> founderTraits;
            public readonly List<List<VarietyTraitDef>> donorTraits = new List<List<VarietyTraitDef>>();
            public readonly List<List<VarietyTraitDef>> childTraits = new List<List<VarietyTraitDef>>();

            public CrossPlan(ThingDef cropDef, List<VarietyTraitDef> founderTraits)
            {
                this.cropDef = cropDef;
                this.founderTraits = founderTraits;
            }
        }
    }
    public sealed class Dialog_DevUnlockVariety : Window
    {
        private readonly List<ThingDef> plants;
        private readonly HashSet<VarietyTraitDef> selectedTraits = new HashSet<VarietyTraitDef>();
        private ThingDef selectedPlant;
        private ThingDef cachedTraitPlant;
        private List<VarietyTraitDef> cachedTraits = new List<VarietyTraitDef>();
        private string plantSearch = string.Empty;
        private string traitSearch = string.Empty;
        private string varietyName = string.Empty;
        private Vector2 plantScroll;
        private Vector2 traitScroll;

        public override Vector2 InitialSize => new Vector2(1080f, 760f);

        public Dialog_DevUnlockVariety()
        {
            plants = DefDatabase<ThingDef>.AllDefsListForReading
                .Where(NovelSeedUtility.IsGrowableCrop)
                .GroupBy(def => def.defName)
                .Select(group => group.First())
                .OrderBy(def => def.label)
                .ThenBy(def => def.defName)
                .ToList();
            doCloseX = true;
            closeOnClickedOutside = false;
            absorbInputAroundWindow = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, inRect.width, 32f), "Unlock Custom Variety");
            Text.Font = GameFont.Small;

            Rect plantPanel = new Rect(0f, 42f, 320f, inRect.height - 100f);
            Rect traitPanel = new Rect(336f, 42f, inRect.width - 336f, inRect.height - 100f);
            Widgets.DrawMenuSection(plantPanel);
            Widgets.DrawMenuSection(traitPanel);
            DrawPlants(plantPanel.ContractedBy(10f));
            DrawTraits(traitPanel.ContractedBy(10f));

            float footerY = inRect.yMax - 42f;
            Widgets.Label(new Rect(0f, footerY + 5f, 92f, 24f), "Variety Name");
            varietyName = Widgets.TextField(new Rect(96f, footerY, 430f, 30f), varietyName ?? string.Empty);

            bool canUnlock = selectedPlant != null && selectedTraits.Count > 0;
            bool oldEnabled = GUI.enabled;
            GUI.enabled = oldEnabled && canUnlock;
            if (Widgets.ButtonText(new Rect(inRect.xMax - 250f, footerY, 140f, 30f), "Unlock Variety")) ConfirmUnlock();
            GUI.enabled = oldEnabled;
            if (Widgets.ButtonText(new Rect(inRect.xMax - 100f, footerY, 100f, 30f), "Cancel")) Close();
        }

        private void DrawPlants(Rect rect)
        {
            Widgets.Label(new Rect(rect.x, rect.y, rect.width, 24f), "Plant");
            plantSearch = Widgets.TextField(new Rect(rect.x, rect.y + 28f, rect.width, 30f), plantSearch ?? string.Empty);
            List<ThingDef> shown = plants.Where(plant => Matches(plant.label, plant.defName, null, plantSearch)).ToList();
            Rect outer = new Rect(rect.x, rect.y + 68f, rect.width, rect.height - 68f);
            Rect view = new Rect(0f, 0f, outer.width - 18f, Mathf.Max(outer.height, shown.Count * 32f));
            Widgets.BeginScrollView(outer, ref plantScroll, view);
            float y = 0f;
            foreach (ThingDef plant in shown)
            {
                Rect row = new Rect(0f, y, view.width, 28f);
                if (plant == selectedPlant) Widgets.DrawHighlightSelected(row);
                else if (Mouse.IsOver(row)) Widgets.DrawHighlight(row);
                if (Widgets.ButtonInvisible(row)) SelectPlant(plant);
                Widgets.Label(new Rect(8f, y + 4f, view.width - 16f, 24f), plant.LabelCap);
                TooltipHandler.TipRegion(row, plant.description ?? plant.defName);
                y += 32f;
            }
            Widgets.EndScrollView();
        }

        private void DrawTraits(Rect rect)
        {
            string count = selectedTraits.Count == 1 ? "1 selected" : selectedTraits.Count + " selected";
            Widgets.Label(new Rect(rect.x, rect.y, rect.width - 120f, 24f), selectedPlant == null ? "Traits" : selectedPlant.LabelCap.ToString() + " Traits");
            Text.Anchor = TextAnchor.UpperRight;
            GUI.color = Color.gray;
            Widgets.Label(new Rect(rect.xMax - 120f, rect.y, 120f, 24f), count);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;

            traitSearch = Widgets.TextField(new Rect(rect.x, rect.y + 28f, rect.width - 190f, 30f), traitSearch ?? string.Empty);
            bool hasPlant = selectedPlant != null;
            bool oldEnabled = GUI.enabled;
            GUI.enabled = oldEnabled && hasPlant;
            if (Widgets.ButtonText(new Rect(rect.xMax - 180f, rect.y + 28f, 84f, 30f), "Select All"))
                foreach (VarietyTraitDef trait in FilteredTraits()) selectedTraits.Add(trait);
            if (Widgets.ButtonText(new Rect(rect.xMax - 88f, rect.y + 28f, 88f, 30f), "Clear")) selectedTraits.Clear();
            GUI.enabled = oldEnabled;

            Rect outer = new Rect(rect.x, rect.y + 68f, rect.width, rect.height - 68f);
            if (!hasPlant)
            {
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = Color.gray;
                Widgets.Label(outer, "Select a plant");
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
                return;
            }

            List<IGrouping<string, VarietyTraitDef>> groups = FilteredTraits()
                .GroupBy(TraitGroup)
                .OrderBy(group => group.Key)
                .ToList();
            float contentHeight = groups.Sum(group => 34f + group.Count() * 32f);
            Rect view = new Rect(0f, 0f, outer.width - 18f, Mathf.Max(outer.height, contentHeight));
            Widgets.BeginScrollView(outer, ref traitScroll, view);
            float y = 0f;
            foreach (IGrouping<string, VarietyTraitDef> group in groups)
            {
                GUI.color = Color.gray;
                Widgets.Label(new Rect(4f, y + 5f, view.width - 8f, 24f), group.Key + " (" + group.Count() + ")");
                GUI.color = Color.white;
                y += 34f;
                foreach (VarietyTraitDef trait in group.OrderBy(item => item.label).ThenBy(item => item.defName))
                {
                    Rect row = new Rect(0f, y, view.width, 28f);
                    if (Mouse.IsOver(row)) Widgets.DrawHighlight(row);
                    bool selected = selectedTraits.Contains(trait);
                    bool previous = selected;
                    Widgets.CheckboxLabeled(new Rect(8f, y + 2f, view.width - 16f, 24f), TraitColorUI.Label(trait), ref selected);
                    if (selected != previous)
                    {
                        if (selected) selectedTraits.Add(trait);
                        else selectedTraits.Remove(trait);
                    }
                    string tip = TraitColorUI.Tooltip(trait);
                    if (!tip.NullOrEmpty()) tip += "\n\n";
                    tip += trait.defName;
                    TooltipHandler.TipRegion(row, tip);
                    y += 32f;
                }
            }
            Widgets.EndScrollView();
        }

        private void SelectPlant(ThingDef plant)
        {
            if (plant == selectedPlant) return;
            selectedPlant = plant;
            cachedTraitPlant = null;
            cachedTraits.Clear();
            selectedTraits.Clear();
            traitScroll = Vector2.zero;
        }

        private List<VarietyTraitDef> FilteredTraits()
        {
            return AvailableTraits().Where(trait => Matches(trait.label, trait.defName, trait.description, traitSearch)
                || Matches(TraitGroup(trait), null, null, traitSearch)).ToList();
        }

        private List<VarietyTraitDef> AvailableTraits()
        {
            if (selectedPlant == null) return new List<VarietyTraitDef>();
            if (cachedTraitPlant == selectedPlant) return cachedTraits;
            SynergyTraitFactory.GenerateAll();
            PercentageTraitFactory.GenerateAll();
            cachedTraitPlant = selectedPlant;
            cachedTraits = DefDatabase<VarietyTraitDef>.AllDefsListForReading
                .Where(IsSelectableTrait)
                .GroupBy(trait => trait.defName)
                .Select(group => group.First())
                .OrderBy(TraitGroup)
                .ThenBy(trait => trait.label)
                .ToList();
            return cachedTraits;
        }

        private static bool IsSelectableTrait(VarietyTraitDef trait)
        {
            if (trait == null || trait.configRoot) return false;
            if (trait.generated) return trait.configFamily == "Synergy" || trait.configFamily == PercentageTraitFactory.NutritiousFamily;
            return !trait.hiddenFromConfig;
        }

        private static string TraitGroup(VarietyTraitDef trait)
        {
            return HorticultureNovelSeedsMod.Settings?.TraitGroup(trait) ?? TraitConfigUtility.Category(trait);
        }

        private static bool Matches(string label, string defName, string description, string search)
        {
            if (search.NullOrEmpty()) return true;
            string query = search.Trim();
            return (label ?? string.Empty).IndexOf(query, System.StringComparison.OrdinalIgnoreCase) >= 0
                || (defName ?? string.Empty).IndexOf(query, System.StringComparison.OrdinalIgnoreCase) >= 0
                || (description ?? string.Empty).IndexOf(query, System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void ConfirmUnlock()
        {
            if (selectedPlant == null || selectedTraits.Count == 0) return;
            List<VarietyTraitDef> traits = selectedTraits.OrderBy(TraitGroup).ThenBy(trait => trait.label).ToList();
            string name = varietyName?.Trim();
            if (name.NullOrEmpty())
            {
                int number = (GameComponent_NovelSeeds.Instance?.VarietiesFor(selectedPlant).Count() ?? 0) + 1;
                name = "DEV " + selectedPlant.LabelCap + " variety " + number;
            }
            string finalName = name;
            string summary = TraitColorUI.Summary(traits.Take(12));
            if (traits.Count > 12) summary += ", and " + (traits.Count - 12) + " more";
            string message = "Unlock " + finalName + " for " + selectedPlant.LabelCap + " with " + traits.Count
                + (traits.Count == 1 ? " trait?" : " traits?") + "\n\n" + summary;
            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(message, delegate { Unlock(finalName, traits); }, true));
        }

        private void Unlock(string name, List<VarietyTraitDef> traits)
        {
            GameComponent_NovelSeeds registry = GameComponent_NovelSeeds.Instance;
            if (registry == null)
            {
                Messages.Message("The variety registry is unavailable.", MessageTypeDefOf.RejectInput, false);
                return;
            }
            VarietyRecord existing = registry.FindMatchingVariety(selectedPlant, traits);
            VarietyRecord variety = registry.UnlockVariety(selectedPlant, traits, name);
            if (existing != null)
                Messages.Message(existing.Label + " already has this trait combination.", MessageTypeDefOf.NeutralEvent, false);
            else
                Messages.Message("Unlocked " + variety.Label + " for " + selectedPlant.LabelCap + ".", MessageTypeDefOf.TaskCompletion, false);
            Close();
        }
    }
}
