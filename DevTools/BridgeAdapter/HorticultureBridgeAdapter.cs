using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using KnowledgeFramework;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace HorticultureNovelSeeds
{
    public static class HorticultureBridgeAdapter
    {
        private const string NiceFixtureLabel = "HNS immutable UI fixture";

        public static string[] BridgeCommandSpecs() => new[]
        {
            "HORTICULTURE|R|Compact Novel Seeds colony summary",
            "HNS_VARIETIES|R|List unlocked varieties and lineage depth",
            "HNS_PLANTS|R|Aggregate live plants by crop and variety state",
            "HNS_PENDING|R|List plants carrying unsaved discoveries",
            "HNS_TRAITS|R|Aggregate unlocked and planted trait frequency",
            "HNS_GROWERS|R|Inspect grower types, commands, and compatible varieties",
            "HNS_PATCHES|R|Inspect live plant-menu Harmony prefix order",
            "HNS_IMMUTABLE_PATCHES|R|Inspect semantic sow targets and shared plant-definition fingerprint",
            "HNS_IMMUTABLE_GAMEPLAY_TEST|W|Exercise sow completion and perennial collection in the sandbox",
            "HNS_OPEN_GROWER_MENU|W|Open Nice Plants Menu for a grower zone ID",
            "HNS_NICE_STATE|R|Inspect the open Nice Plants Menu records",
            "HNS_NICE_FIXTURE_OPEN|W|Create a reversible grower fixture and open Nice Plants Menu",
            "HNS_NICE_FIXTURE_FOCUS|W|Focus the fixture cultivar detail after browser population",
            "HNS_NICE_FIXTURE_CLEANUP|W|Close the fixture browser and remove only its temporary zone",
            "HNS_OPPORTUNITIES|R|Analyze live horticulture for feature opportunities",
            "HNS_SETTINGS|R|Report active mutation settings",
            "HNS_BALANCE_TEST|R|Simulate trait generation and compare balance modes",
            "HNS_REGISTRY|R|Inspect cultivar registry and framework knowledge",
            "HNS_OPEN_REGISTRY|W|Open the Cultivar Registry main tab",
            "HNS_REGISTRY_UI|R|Inspect the open Cultivar Registry page and comparison state",
            "HNS_SET_REGISTRY_PAGE|W|Switch the Cultivar Registry page",
            "HNS_SET_REGISTRY_COMPARE|W|Select zero or two cultivars for comparison",
            "HNS_SET_REGISTRY_SCOPE|W|Switch Registry knowledge between Colony and Colonist",
            "HNS_AUTO_MASKS|R|Inspect automatic and manual plant-mask resolution",
            "HNS_EXPORT_MASK_DIAGNOSTIC|W|Export a source sprite and resolved layer overlays",
            "HNS_MASK_REGRESSIONS|R|Run each automatic mask regression separately",
            "HNS_CROSS_REGRESSIONS|R|Run deterministic cross-pollination regressions",
            "HNS_TRAIT_CATALOG_REGRESSIONS|R|Run trait specialization and balance regressions",
            "HNS_BREEDING_MIX_DIAGNOSTIC|R|Run deterministic Breeding Mix donor scenarios",
            "HNS_GENERATE_AUTO_MASKS|W|Generate every missing automatic plant mask",
            "HNS_OPEN_MASK_EDITOR|W|Open the existing mask editor for a plant def",
            "HNS_MASK_EDITOR_STATE|R|Inspect the current mask editor source and confidence",
            "HNS_MASK_EDITOR_ACTION|W|Exercise a mask editor command",
            "HNS_CAPTURE_UI|W|Capture the current RimWorld UI to a PNG",
            "HNS_DEV_MASK_GIZMO|W|Select a live plant and inspect or invoke its dev mask gizmo",
            "HNS_DEV_RANDOM_GRID|W|Plant and inspect a 10x10 random-variety grid",
            "HNS_WILDLIFE_LAYOUT|R|Inspect Wildlife tab layout members for integration",
            "HNS_KNOWLEDGE_STATE|R|Inspect framework plant knowledge for one colonist and crop",
            "HNS_AWARD_PLANT_KNOWLEDGE|W|Award one completed sowing event through Horticulture",
            "HNS_SAVE_TEST|W|Save the isolated test game under a named copy",
            "HNS_LOAD_TEST_SAVE|W|Load a named save in the isolated test process",
            "HNS_PERF_STATE|R|Report live map plant and Novel Seeds workload",
            "HNS_DISABLE_PATCHES|W|Remove Novel Seeds Harmony patches for isolated A/B profiling",
            "HNS_PLANT_PATCHES|R|List Harmony owners on hot plant methods",
            "HNS_DISABLE_GROWTH_OWNER|W|Remove one owner's Plant GrowthRate patches for isolated A/B profiling",
            "HNS_DPA_OPEN|W|Open Dubs Performance Analyzer in the isolated process",
            "HNS_DPA_API|R|Inspect Dubs Performance Analyzer runtime profiling API",
            "HNS_DPA_ENTRIES|R|List Dubs Performance Analyzer profiling entries",
            "HNS_DPA_START|W|Start a Dubs Performance Analyzer entry by exact or partial name",
            "HNS_DPA_SAMPLE|W|Stop profiling and report the highest average costs",
            "HNS_VALIDATE|R|Run read-only Novel Seeds state invariants",
            "HNS_ADAPTER_STATUS|R|Report Novel Seeds adapter identity"
        };

        public static string BridgeAdapterInfo() =>
            "HorticultureNovelSeeds|1.8.4|Use transient perennial traits on a destructive crop in the immutable gameplay regression.";

        public static List<string> ExecuteBridgeCommand(string command, string argument, Map map)
        {
            switch ((command ?? string.Empty).ToUpperInvariant())
            {
                case "HORTICULTURE": return Summary(map);
                case "HNS_VARIETIES": return Varieties(argument);
                case "HNS_PLANTS": return Plants(map, argument);
                case "HNS_PENDING": return Pending(map);
                case "HNS_TRAITS": return Traits(map);
                case "HNS_GROWERS": return Growers(map);
                case "HNS_PATCHES": return Patches();
                case "HNS_IMMUTABLE_PATCHES": return ImmutablePatches();
                case "HNS_IMMUTABLE_GAMEPLAY_TEST": return ImmutableGameplayTest(map);
                case "HNS_OPEN_GROWER_MENU": return OpenGrowerMenu(map, argument);
                case "HNS_NICE_STATE": return NiceState();
                case "HNS_NICE_FIXTURE_OPEN": return NiceFixtureOpen(map, argument);
                case "HNS_NICE_FIXTURE_FOCUS": return NiceFixtureFocus();
                case "HNS_NICE_FIXTURE_CLEANUP": return NiceFixtureCleanup(map);
                case "HNS_OPPORTUNITIES": return Opportunities(map);
                case "HNS_SETTINGS": return Settings();
                case "HNS_BALANCE_TEST": return BalanceTest(argument);
                case "HNS_REGISTRY": return Registry();
                case "HNS_OPEN_REGISTRY": return OpenRegistry();
                case "HNS_REGISTRY_UI": return RegistryUi();
                case "HNS_SET_REGISTRY_PAGE": return SetRegistryPage(argument);
                case "HNS_SET_REGISTRY_COMPARE": return SetRegistryCompare(argument);
                case "HNS_SET_REGISTRY_SCOPE": return SetRegistryScope(argument);
                case "HNS_AUTO_MASKS": return AutoMasks(argument);
                case "HNS_EXPORT_MASK_DIAGNOSTIC": return ExportMaskDiagnostic(argument);
                case "HNS_MASK_REGRESSIONS": return MaskRegressions();
                case "HNS_CROSS_REGRESSIONS": return CrossRegressions();
                case "HNS_TRAIT_CATALOG_REGRESSIONS": return TraitCatalogRegressions();
                case "HNS_BREEDING_MIX_DIAGNOSTIC": return BreedingMixDiagnostic();
                case "HNS_GENERATE_AUTO_MASKS": return GenerateAutoMasks();
                case "HNS_OPEN_MASK_EDITOR": return OpenMaskEditor(argument);
                case "HNS_MASK_EDITOR_STATE": return MaskEditorState();
                case "HNS_MASK_EDITOR_ACTION": return MaskEditorAction(argument);
                case "HNS_CAPTURE_UI": return CaptureUi(argument);
                case "HNS_DEV_MASK_GIZMO": return DevMaskGizmo(map, argument);
                case "HNS_DEV_RANDOM_GRID": return DevRandomGrid(map, argument);
                case "HNS_WILDLIFE_LAYOUT": return WildlifeLayout();
                case "HNS_KNOWLEDGE_STATE": return KnowledgeState(map, argument);
                case "HNS_AWARD_PLANT_KNOWLEDGE": return AwardPlantKnowledge(map, argument);
                case "HNS_SAVE_TEST": return SaveTest(argument);
                case "HNS_LOAD_TEST_SAVE": return LoadTestSave(argument);
                case "HNS_PERF_STATE": return PerfState(map);
                case "HNS_DISABLE_PATCHES": return DisablePatches();
                case "HNS_PLANT_PATCHES": return PlantPatches();
                case "HNS_DISABLE_GROWTH_OWNER": return DisableGrowthOwner(argument);
                case "HNS_DPA_OPEN": return DpaOpen();
                case "HNS_DPA_API": return DpaApi();
                case "HNS_DPA_ENTRIES": return DpaEntries();
                case "HNS_DPA_START": return DpaStart(argument);
                case "HNS_DPA_SAMPLE": return DpaSample(argument);
                case "HNS_VALIDATE": return Validate(map);
                case "HNS_ADAPTER_STATUS": return AdapterStatus();
                default: return null;
            }
        }

        private static List<string> Summary(Map map)
        {
            if (map == null) return NoMap();
            Stopwatch watch = Stopwatch.StartNew();
            List<PlantState> plants = NovelPlants(map);
            List<VarietyRecord> varieties = AllVarieties();
            int pending = plants.Count(item => item.Comp.PendingDiscovery);
            int maturePending = plants.Count(item => item.Comp.PendingDiscovery && item.Plant.Growth >= 0.999f && !item.Plant.Blighted);
            int cross = plants.Count(item => item.Comp.CrossPollinated);
            int adopted = plants.Count(item => item.Comp.Variety != null);
            watch.Stop();
            return new List<string>
            {
                "map=" + map.uniqueID + " tick=" + (Find.TickManager?.TicksGame ?? 0),
                "varieties=" + varieties.Count + " crops=" + varieties.Select(item => item.cropDef).Where(item => item != null).Distinct().Count(),
                "plants=novel:" + plants.Count + " adopted:" + adopted + " pending:" + pending + " maturePending:" + maturePending + " crossPending:" + cross,
                "lineage=maxDepth:" + varieties.Select(LineageDepth).DefaultIfEmpty(0).Max() + " crossbred:" + varieties.Count(item => item.parentVarietyIds?.Count > 1),
                "queryMs=" + watch.Elapsed.TotalMilliseconds.ToString("0.00")
            };
        }

        private static List<string> Varieties(string argument)
        {
            string filter = (argument ?? string.Empty).Trim();
            List<VarietyRecord> varieties = AllVarieties()
                .Where(item => filter.NullOrEmpty()
                    || item.Label.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0
                    || item.cropDef?.defName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderBy(item => item.cropDef?.label).ThenBy(item => item.Label).ToList();
            var result = new List<string> { "varieties=" + varieties.Count };
            foreach (VarietyRecord variety in varieties.Take(60))
                result.Add("variety=id:" + variety.id + " crop:" + variety.cropDef?.defName + " name:" + Clean(variety.Label)
                    + " traits:" + Clean(TraitSummary(variety.traits)) + " parents:" + (variety.parentVarietyIds?.Count ?? 0)
                    + " depth:" + LineageDepth(variety) + " discoverer:" + Clean(variety.firstDiscoveredBy));
            if (varieties.Count > 60) result.Add("truncated=" + (varieties.Count - 60));
            return result;
        }

        private static List<string> Plants(Map map, string argument)
        {
            if (map == null) return NoMap();
            string filter = (argument ?? string.Empty).Trim();
            List<Plant> all = map.listerThings.ThingsInGroup(ThingRequestGroup.Plant).OfType<Plant>()
                .Where(item => filter.NullOrEmpty() || item.def.defName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0
                    || item.Label.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            var result = new List<string> { "plants=" + all.Count + " crops=" + all.Select(item => item.def).Distinct().Count() };
            foreach (IGrouping<ThingDef, Plant> group in all.GroupBy(item => item.def).OrderByDescending(item => item.Count()).Take(50))
            {
                List<CompPlantVariety> comps = group.Select(item => item.TryGetComp<CompPlantVariety>()).Where(item => item != null).ToList();
                result.Add("crop=" + group.Key.defName + " total:" + group.Count()
                    + " novel:" + comps.Count(item => item.HasAnyTraits)
                    + " varieties:" + comps.Select(item => item.VarietyId).Where(item => !item.NullOrEmpty()).Distinct().Count()
                    + " pending:" + comps.Count(item => item.PendingDiscovery)
                    + " mature:" + group.Count(item => item.Growth >= 0.999f));
            }
            return result;
        }

        private static List<string> Pending(Map map)
        {
            if (map == null) return NoMap();
            List<PlantState> pending = NovelPlants(map).Where(item => item.Comp.PendingDiscovery)
                .OrderByDescending(item => item.Plant.Growth).ToList();
            var result = new List<string> { "pending=" + pending.Count };
            foreach (PlantState item in pending.Take(60))
                result.Add("plant=id:" + item.Plant.thingIDNumber + " crop:" + item.Plant.def.defName
                    + " cell:" + item.Plant.Position.x + "," + item.Plant.Position.z
                    + " growth:" + item.Plant.Growth.ToStringPercent()
                    + " mature:" + (item.Plant.Growth >= 0.999f) + " blighted:" + item.Plant.Blighted
                    + " cross:" + item.Comp.CrossPollinated + " saveRequested:" + item.Comp.SaveSeedsRequested
                    + " traits:" + Clean(TraitSummary(item.Comp.DiscoveryTraits)));
            return result;
        }

        private static List<string> Traits(Map map)
        {
            if (map == null) return NoMap();
            List<VarietyRecord> varieties = AllVarieties();
            List<PlantState> plants = NovelPlants(map);
            var unlocked = varieties.SelectMany(item => item.traits ?? new List<VarietyTraitDef>())
                .Where(item => item != null).GroupBy(item => item.label).ToDictionary(item => item.Key, item => item.Count());
            var planted = plants.SelectMany(item => item.Comp.ActiveTraits).Where(item => item != null)
                .GroupBy(item => item.label).ToDictionary(item => item.Key, item => item.Count());
            List<string> names = unlocked.Keys.Concat(planted.Keys).Distinct().OrderBy(name => name).ToList();
            var result = new List<string> { "traits=" + names.Count };
            foreach (string name in names)
                result.Add("trait=" + Clean(name) + " unlocked:" + Value(unlocked, name) + " planted:" + Value(planted, name));
            return result;
        }

        private static List<string> Opportunities(Map map)
        {
            if (map == null) return NoMap();
            List<Plant> allPlants = map.listerThings.ThingsInGroup(ThingRequestGroup.Plant).OfType<Plant>().Where(item => item.sown).ToList();
            List<PlantState> novel = NovelPlants(map);
            List<VarietyRecord> varieties = AllVarieties();
            var result = new List<string>
            {
                "signals=sown:" + allPlants.Count + " novel:" + novel.Count + " unlocked:" + varieties.Count
            };
            foreach (IGrouping<ThingDef, Plant> crop in allPlants.GroupBy(item => item.def).OrderByDescending(item => item.Count()).Take(12))
            {
                int cropVarieties = varieties.Count(item => item.cropDef == crop.Key);
                int plantedVarieties = novel.Count(item => item.Plant.def == crop.Key && item.Comp.Variety != null);
                if (cropVarieties == 0 && crop.Count() >= 6)
                    result.Add("gap=highUseNoVariety crop:" + crop.Key.defName + " plants:" + crop.Count());
                else if (cropVarieties > 0 && plantedVarieties == 0)
                    result.Add("gap=unadoptedCollection crop:" + crop.Key.defName + " varieties:" + cropVarieties);
            }
            int maturePending = novel.Count(item => item.Comp.PendingDiscovery && item.Plant.Growth >= 0.999f && !item.Plant.Blighted);
            if (maturePending > 0) result.Add("friction=matureDiscoveriesAwaitingNotice count:" + maturePending);
            int deep = varieties.Count(item => LineageDepth(item) >= 2);
            result.Add("potential=lineageDepth2Plus:" + deep + " crossbred:" + varieties.Count(item => item.parentVarietyIds?.Count > 1));
            result.Add("ideaSignal=compare variety performance by field and season without per-tick tracking");
            return result;
        }

        private static List<string> Growers(Map map)
        {
            if (map == null) return NoMap();
            List<Zone> zones = map.zoneManager?.AllZones?.Where(zone => zone is IPlantToGrowSettable).ToList() ?? new List<Zone>();
            List<VarietyRecord> varieties = AllVarieties();
            var result = new List<string> { "growers=" + zones.Count };
            foreach (Zone zone in zones)
            {
                IPlantToGrowSettable grower = (IPlantToGrowSettable)zone;
                ThingDef plant = grower.GetPlantDefToGrow();
                List<string> commands;
                try { commands = zone.GetGizmos().Where(gizmo => gizmo != null).Select(gizmo => gizmo.GetType().FullName).Distinct().ToList(); }
                catch (Exception exception) { commands = new List<string> { "error:" + exception.GetType().Name }; }
                List<VarietyRecord> compatible = varieties.Where(variety => ExpandedTraitUtility.VarietyMatchesGrowers(variety, new[] { grower })).ToList();
                result.Add("grower=id:" + zone.ID + " type:" + zone.GetType().FullName + " cells:" + zone.CellCount
                    + " plant:" + (plant?.defName ?? "none") + " gizmos:" + Clean(string.Join(",", commands.ToArray()))
                    + " compatible:" + Clean(string.Join(",", compatible.Select(variety => variety.cropDef.defName + "/" + variety.Label).ToArray())));
            }
            return result;
        }

        private static List<string> Patches()
        {
            var method = AccessTools.Method(typeof(Command_SetPlantToGrow), nameof(Command_SetPlantToGrow.ProcessInput));
            HarmonyLib.Patches patchInfo = Harmony.GetPatchInfo(method);
            var result = new List<string>
            {
                "niceHandling=" + NicePlantsMenuCompat.IsHandlingPlantMenu(),
                "prefixes=" + (patchInfo?.Prefixes.Count ?? 0)
            };
            if (patchInfo != null)
                foreach (Patch patch in patchInfo.Prefixes)
                    result.Add("prefix=owner:" + patch.owner + " priority:" + patch.priority + " index:" + patch.index
                        + " method:" + patch.PatchMethod?.DeclaringType?.FullName + "." + patch.PatchMethod?.Name);
            return result;
        }

        private static List<string> ImmutablePatches()
        {
            List<MethodBase> sowMethods = JobDriverPlantSow_MutationWorkUtility.SowWorkMethods().ToList();
            MethodBase completion = JobDriverPlantSow_MutationWorkUtility.CompletionMethod();
            var result = new List<string>
            {
                "sowTargets=" + sowMethods.Count,
                "completion=" + MethodLabel(completion),
                "plantDefs=" + DefDatabase<ThingDef>.AllDefsListForReading.Count(NovelSeedUtility.IsGrowableCrop),
                "fingerprint=" + SharedPlantDefinitionFingerprint()
            };
            foreach (MethodBase method in sowMethods)
            {
                HarmonyLib.Patches patches = Harmony.GetPatchInfo(method);
                result.Add("sowTarget=" + MethodLabel(method)
                    + " transpilerOwners:" + Clean(string.Join(",", patches?.Transpilers.Select(patch => patch.owner).Distinct().ToArray() ?? new string[0]))
                    + " prefixOwners:" + Clean(string.Join(",", patches?.Prefixes.Select(patch => patch.owner).Distinct().ToArray() ?? new string[0]))
                    + " postfixOwners:" + Clean(string.Join(",", patches?.Postfixes.Select(patch => patch.owner).Distinct().ToArray() ?? new string[0])));
            }

            MethodBase skillMethod = AccessTools.DeclaredMethod(typeof(WorkGiver_GrowerSow), "JobOnCell",
                new[] { typeof(Pawn), typeof(IntVec3), typeof(bool) });
            HarmonyLib.Patches skillPatches = Harmony.GetPatchInfo(skillMethod);
            result.Add("skillTarget=" + MethodLabel(skillMethod)
                + " transpilerOwners:" + Clean(string.Join(",", skillPatches?.Transpilers.Select(patch => patch.owner).Distinct().ToArray() ?? new string[0])));
            return result;
        }

        private static string SharedPlantDefinitionFingerprint()
        {
            ulong hash = 14695981039346656037UL;
            foreach (ThingDef def in DefDatabase<ThingDef>.AllDefsListForReading
                .Where(NovelSeedUtility.IsGrowableCrop).OrderBy(item => item.defName))
            {
                PlantProperties plant = def.plant;
                AddHash(ref hash, def.defName);
                AddHash(ref hash, def.label);
                AddHash(ref hash, def.description);
                AddHash(ref hash, plant.sowMinSkill.ToString());
                AddHash(ref hash, plant.sowWork.ToString("R"));
                AddHash(ref hash, plant.harvestWork.ToString("R"));
                AddHash(ref hash, plant.harvestYield.ToString("R"));
                AddHash(ref hash, plant.harvestAfterGrowth.ToString("R"));
                AddHash(ref hash, plant.minGrowthTemperature.ToString("R"));
                AddHash(ref hash, plant.minOptimalGrowthTemperature.ToString("R"));
                AddHash(ref hash, plant.maxOptimalGrowthTemperature.ToString("R"));
                AddHash(ref hash, plant.maxGrowthTemperature.ToString("R"));
                foreach (StatModifier modifier in def.statBases?.OrderBy(item => item.stat?.defName) ?? Enumerable.Empty<StatModifier>())
                {
                    AddHash(ref hash, modifier.stat?.defName);
                    AddHash(ref hash, modifier.value.ToString("R"));
                }
            }
            return hash.ToString("X16");
        }

        private static void AddHash(ref ulong hash, string value)
        {
            foreach (char character in value ?? string.Empty)
            {
                hash ^= character;
                hash *= 1099511628211UL;
            }
            hash ^= 255;
            hash *= 1099511628211UL;
        }

        private static string MethodLabel(MethodBase method) => method == null
            ? "missing"
            : method.DeclaringType?.FullName + "." + method.Name + "(" + string.Join(",", method.GetParameters().Select(parameter => parameter.ParameterType.Name).ToArray()) + ")";

        private static List<string> ImmutableGameplayTest(Map map)
        {
            if (map == null) return NoMap();
            Pawn pawn = map.mapPawns?.FreeColonistsSpawned?.FirstOrDefault();
            GameComponent_NovelSeeds component = GameComponent_NovelSeeds.Instance;
            VarietyRecord sowVariety = component?.AllVarieties?.FirstOrDefault(item => item?.cropDef != null
                && item.cropDef.defName == "VCE_Carrot" && !item.registryArchived);
            VarietyTraitDef perennialTrait = DefDatabase<VarietyTraitDef>.AllDefsListForReading
                .FirstOrDefault(trait => trait?.perennial == true);
            ThingDef perennialCrop = DefDatabase<ThingDef>.AllDefsListForReading
                .Where(NovelSeedUtility.IsGrowableCrop)
                .FirstOrDefault(def => def.plant.HarvestDestroys && def.plant.harvestedThingDef != null
                    && def.comps?.Any(props => props.compClass == typeof(CompPlantVariety)) == true);
            if (pawn == null || sowVariety == null || perennialTrait == null || perennialCrop == null)
                return new List<string>
                {
                    "error=required pawn or fixture definitions unavailable",
                    "pawn=" + (pawn != null),
                    "sowVariety=" + (sowVariety != null),
                    "perennialTrait=" + (perennialTrait != null),
                    "destructiveCrop=" + (perennialCrop != null)
                };

            List<IntVec3> cells = map.AllCells.OrderBy(candidate => candidate.DistanceToSquared(map.Center))
                .Where(candidate => candidate.GetZone(map) == null && candidate.GetEdifice(map) == null
                    && candidate.GetPlant(map) == null && map.fertilityGrid.FertilityAt(candidate) > 0f)
                .Take(2).ToList();
            if (cells.Count < 2) return new List<string> { "error=not enough clear fixture cells" };

            string fingerprintBefore = SharedPlantDefinitionFingerprint();
            HashSet<int> thingIdsBefore = new HashSet<int>(map.listerThings.AllThings.Select(thing => thing.thingIDNumber));
            Zone_Growing zone = null;
            Plant sowPlant = null;
            Plant perennialPlant = null;
            try
            {
                zone = new Zone_Growing(map.zoneManager) { label = NiceFixtureLabel };
                if (!map.zoneManager.AllZones.Contains(zone)) map.zoneManager.RegisterZone(zone);
                zone.AddCell(cells[0]);
                zone.SetPlantDefToGrow(sowVariety.cropDef);
                component.SetSelectedVariety(zone, sowVariety);

                sowPlant = ThingMaker.MakeThing(sowVariety.cropDef) as Plant;
                GenSpawn.Spawn(sowPlant, cells[0], map);
                sowPlant.Growth = 0f;
                sowPlant.sown = false;
                Job job = JobMaker.MakeJob(JobDefOf.Sow, sowPlant);
                job.plantDefToSow = sowVariety.cropDef;
                JobDriver_PlantSow driver = new JobDriver_PlantSow { pawn = pawn, job = job };
                MethodBase completion = JobDriverPlantSow_MutationWorkUtility.CompletionMethod();
                object displayClass = Activator.CreateInstance(completion.DeclaringType);
                completion.DeclaringType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .First(field => typeof(JobDriver_PlantSow).IsAssignableFrom(field.FieldType)).SetValue(displayClass, driver);
                FieldInfo workDoneField = AccessTools.Field(typeof(JobDriver_PlantSow), "sowWorkDone");
                float requiredWork = JobDriverPlantSow_MutationWorkUtility.AdjustedSowWork(sowVariety.cropDef.plant.sowWork, driver);
                workDoneField.SetValue(driver, Mathf.Max(0f, requiredWork - 1f));
                float knowledgeBefore = PlantKnowledgeUtility.ExperienceFor(pawn, sowVariety.cropDef);

                JobDriverPlantSow_AssignMutation_Patch.Prefix(displayClass,
                    out JobDriverPlantSow_AssignMutation_Patch.CompletionState cancelledState);
                JobDriverPlantSow_AssignMutation_Patch.Postfix(cancelledState);
                float knowledgeAfterCancel = PlantKnowledgeUtility.ExperienceFor(pawn, sowVariety.cropDef);
                bool cancelUnassigned = sowPlant.TryGetComp<CompPlantVariety>()?.HasAnyTraits != true;

                JobDriverPlantSow_AssignMutation_Patch.Prefix(displayClass,
                    out JobDriverPlantSow_AssignMutation_Patch.CompletionState completedState);
                workDoneField.SetValue(driver, requiredWork);
                JobDriverPlantSow_AssignMutation_Patch.Postfix(completedState);
                float knowledgeAfterComplete = PlantKnowledgeUtility.ExperienceFor(pawn, sowVariety.cropDef);
                bool completeAssigned = sowPlant.TryGetComp<CompPlantVariety>()?.HasAnyTraits == true;

                perennialPlant = ThingMaker.MakeThing(perennialCrop) as Plant;
                GenSpawn.Spawn(perennialPlant, cells[1], map);
                perennialPlant.TryGetComp<CompPlantVariety>()?.SetPendingTraits(new List<VarietyTraitDef> { perennialTrait });
                perennialPlant.Growth = 1f;
                perennialPlant.sown = true;
                float resetGrowth = NovelSeedUtility.PerennialHarvestAfterGrowth(perennialPlant.TryGetComp<CompPlantVariety>());
                bool harvestDestroys = PlantCollected_DropMutationSeed_Patch.EffectiveHarvestDestroys(true, perennialPlant, (PlantDestructionMode)2);
                bool cutDestroys = PlantCollected_DropMutationSeed_Patch.EffectiveHarvestDestroys(true, perennialPlant, (PlantDestructionMode)3);
                perennialPlant.PlantCollected(pawn, (PlantDestructionMode)2);
                bool perennialAlive = !perennialPlant.Destroyed;
                float growthAfterHarvest = perennialAlive ? perennialPlant.Growth : -1f;

                string fingerprintAfter = SharedPlantDefinitionFingerprint();
                bool cancelPassed = Mathf.Approximately(knowledgeBefore, knowledgeAfterCancel) && cancelUnassigned;
                bool completePassed = Mathf.Approximately(knowledgeAfterComplete, knowledgeBefore + 2f) && completeAssigned;
                bool perennialPassed = perennialCrop.plant.HarvestDestroys && !harvestDestroys && cutDestroys
                    && perennialAlive && Mathf.Approximately(growthAfterHarvest, resetGrowth);
                return new List<string>
                {
                    "cancelPassed=" + cancelPassed + " knowledge:" + knowledgeBefore.ToString("0.###") + "->" + knowledgeAfterCancel.ToString("0.###") + " unassigned:" + cancelUnassigned,
                    "completePassed=" + completePassed + " knowledge:" + knowledgeAfterCancel.ToString("0.###") + "->" + knowledgeAfterComplete.ToString("0.###") + " assigned:" + completeAssigned,
                    "perennialPassed=" + perennialPassed + " crop:" + perennialCrop.defName + " trait:" + perennialTrait.defName
                        + " baseDestroys:" + perennialCrop.plant.HarvestDestroys
                        + " harvestDestroys:" + harvestDestroys + " cutDestroys:" + cutDestroys + " alive:" + perennialAlive
                        + " growth:" + growthAfterHarvest.ToString("0.###") + " expected:" + resetGrowth.ToString("0.###"),
                    "fingerprintBefore=" + fingerprintBefore,
                    "fingerprintAfter=" + fingerprintAfter,
                    "passed=" + (cancelPassed && completePassed && perennialPassed && fingerprintBefore == fingerprintAfter)
                };
            }
            finally
            {
                if (sowPlant?.Destroyed == false) sowPlant.Destroy(DestroyMode.Vanish);
                if (perennialPlant?.Destroyed == false) perennialPlant.Destroy(DestroyMode.Vanish);
                if (zone != null)
                {
                    component?.ClearSelectedVariety(zone);
                    zone.Delete();
                }
                foreach (Thing thing in map.listerThings.AllThings
                    .Where(item => item.def == perennialCrop.plant.harvestedThingDef
                        && !thingIdsBefore.Contains(item.thingIDNumber)).ToList())
                {
                    if (!thing.Destroyed) thing.Destroy(DestroyMode.Vanish);
                }
            }
        }

        private static List<string> OpenGrowerMenu(Map map, string argument)
        {
            if (map == null) return NoMap();
            if (!int.TryParse(argument, out int zoneId)) return new List<string> { "error=invalid zone ID" };
            Zone zone = map.zoneManager?.AllZones?.FirstOrDefault(item => item.ID == zoneId);
            if (!(zone is IPlantToGrowSettable grower)) return new List<string> { "error=grower zone not found", "zoneId=" + zoneId };
            Window dialog = NicePlantsMenuCompat.CreateDialogForGrowers(new[] { grower });
            if (dialog == null) return new List<string> { "error=dialog creation failed" };
            Find.WindowStack.Add(dialog);
            return new List<string> { "opened=True", "zone=" + zoneId, "type=" + zone.GetType().FullName };
        }

        private static List<string> NiceState()
        {
            Type dialogType = AccessTools.TypeByName("NicePlantsMenu.Dialog_PlantBrowser");
            object dialog = Find.WindowStack?.Windows?.LastOrDefault(window => dialogType?.IsInstanceOfType(window) == true);
            if (dialog == null) return new List<string> { "open=False" };
            FieldInfo zonesField = AccessTools.Field(dialogType, "plantZones");
            FieldInfo availableField = AccessTools.Field(dialogType, "plantsAvailable");
            FieldInfo filteredField = AccessTools.Field(dialogType, "plantsFiltered");
            Type recordType = AccessTools.TypeByName("NicePlantsMenu.PlantRecord");
            FieldInfo plantField = recordType == null ? null : AccessTools.Field(recordType, "plant");
            IList zones = zonesField?.GetValue(dialog) as IList;
            IList available = availableField?.GetValue(dialog) as IList;
            IList filtered = filteredField?.GetValue(dialog) as IList;
            Func<IList, List<string>> names = list => list == null ? new List<string>() : list.Cast<object>()
                .Select(record => plantField?.GetValue(record) as ThingDef).Where(def => def != null).Select(def => def.defName).ToList();
            List<string> availableNames = names(available);
            List<string> filteredNames = names(filtered);
            return new List<string>
            {
                "open=True zones=" + (zones?.Count ?? 0) + " zoneTypes:" + Clean(string.Join(",", zones?.Cast<object>().Select(item => item.GetType().FullName).ToArray() ?? new string[0])),
                "available=" + availableNames.Count + " carrot:" + availableNames.Contains("VCE_Carrot"),
                "filtered=" + filteredNames.Count + " carrot:" + filteredNames.Contains("VCE_Carrot"),
                "fingerprint=" + SharedPlantDefinitionFingerprint()
            };
        }

        private static List<string> NiceFixtureOpen(Map map, string argument)
        {
            if (map == null) return NoMap();
            NiceFixtureCleanup(map);
            GameComponent_NovelSeeds component = GameComponent_NovelSeeds.Instance;
            string requested = (argument ?? string.Empty).Trim();
            VarietyRecord variety = component?.AllVarieties?.Where(item => item?.cropDef != null && !item.registryArchived)
                .FirstOrDefault(item => !requested.NullOrEmpty()
                    && (item.id.Equals(requested, StringComparison.OrdinalIgnoreCase)
                        || item.cropDef.defName.Equals(requested, StringComparison.OrdinalIgnoreCase)))
                ?? component?.AllVarieties?.FirstOrDefault(item => item?.cropDef != null
                    && item.traits?.Any(trait => trait?.perennial == true) == true && !item.registryArchived);
            if (variety == null) return new List<string> { "error=no suitable unlocked cultivar" };

            IntVec3 cell = map.AllCells.OrderBy(candidate => candidate.DistanceToSquared(map.Center)).FirstOrDefault(candidate =>
                candidate.GetZone(map) == null
                && candidate.GetEdifice(map) == null
                && map.fertilityGrid.FertilityAt(candidate) > 0f);
            if (!cell.IsValid) return new List<string> { "error=no unzoned fertile fixture cell" };

            Zone_Growing zone = new Zone_Growing(map.zoneManager) { label = NiceFixtureLabel };
            if (!map.zoneManager.AllZones.Contains(zone)) map.zoneManager.RegisterZone(zone);
            zone.AddCell(cell);
            zone.SetPlantDefToGrow(variety.cropDef);
            component.SetSelectedVariety(zone, variety);

            Window dialog = NicePlantsMenuCompat.CreateDialogForGrowers(new[] { zone });
            if (dialog == null)
            {
                component.ClearSelectedVariety(zone);
                zone.Delete();
                return new List<string> { "error=Nice Plants Menu unavailable" };
            }
            Find.WindowStack.Add(dialog);
            return new List<string>
            {
                "opened=True",
                "zone=" + zone.ID,
                "cell=" + cell.x + "," + cell.z,
                "crop=" + variety.cropDef.defName,
                "variety=" + variety.id,
                "fingerprint=" + SharedPlantDefinitionFingerprint()
            };
        }

        private static List<string> NiceFixtureFocus()
        {
            Type dialogType = AccessTools.TypeByName("NicePlantsMenu.Dialog_PlantBrowser");
            object dialog = Find.WindowStack?.Windows?.LastOrDefault(window => dialogType?.IsInstanceOfType(window) == true);
            if (dialog == null) return new List<string> { "error=fixture browser is not open" };
            IList zones = AccessTools.Field(dialogType, "plantZones")?.GetValue(dialog) as IList;
            Zone_Growing zone = zones?.Cast<object>().OfType<Zone_Growing>().FirstOrDefault(item => item.label == NiceFixtureLabel);
            VarietyRecord variety = zone == null ? null : GameComponent_NovelSeeds.Instance?.SelectedVarietyFor(zone);
            Type recordType = AccessTools.TypeByName("NicePlantsMenu.PlantRecord");
            FieldInfo plantField = recordType == null ? null : AccessTools.Field(recordType, "plant");
            IList available = AccessTools.Field(dialogType, "plantsAvailable")?.GetValue(dialog) as IList;
            object record = available?.Cast<object>().FirstOrDefault(item => plantField?.GetValue(item) == variety?.cropDef);
            if (zone == null || variety == null || record == null)
                return new List<string> { "error=fixture cultivar record not populated", "fingerprint=" + SharedPlantDefinitionFingerprint() };

            AccessTools.Field(dialogType, "drawInfoFor")?.SetValue(dialog, record);
            AccessTools.Field(dialogType, "hoveredInfo")?.SetValue(dialog, null);
            AccessTools.Field(dialogType, "lastShowedInfo")?.SetValue(dialog, null);
            AccessTools.Field(dialogType, "cannotBeHoveredTicks")?.SetValue(dialog, 100);
            AccessTools.Field(dialogType, "ticksDelayInfo")?.SetValue(dialog, 0);
            return new List<string>
            {
                "focused=True",
                "crop=" + variety.cropDef.defName,
                "variety=" + variety.id,
                "traits=" + Clean(TraitSummary(variety.traits)),
                "fingerprint=" + SharedPlantDefinitionFingerprint()
            };
        }

        private static List<string> NiceFixtureCleanup(Map map)
        {
            Type dialogType = AccessTools.TypeByName("NicePlantsMenu.Dialog_PlantBrowser");
            int windows = Find.WindowStack?.Windows?.Count(window => dialogType?.IsInstanceOfType(window) == true) ?? 0;
            if (dialogType != null) Find.WindowStack?.TryRemoveAssignableFromType(dialogType, false);
            List<Zone_Growing> zones = map?.zoneManager?.AllZones?.OfType<Zone_Growing>()
                .Where(zone => zone.label == NiceFixtureLabel).ToList() ?? new List<Zone_Growing>();
            foreach (Zone_Growing zone in zones)
            {
                GameComponent_NovelSeeds.Instance?.ClearSelectedVariety(zone);
                zone.Delete();
            }
            return new List<string>
            {
                "cleaned=True",
                "windows=" + windows,
                "zones=" + zones.Count,
                "fingerprint=" + SharedPlantDefinitionFingerprint()
            };
        }

        private static List<string> Settings()
        {
            NovelSeedsSettings settings = HorticultureNovelSeedsMod.Settings;
            if (settings == null) return new List<string> { "settings=unavailable" };
            return new List<string>
            {
                "mutation=global:" + settings.globalMutationChance.ToStringPercent() + " wild:" + settings.wildMutationChance.ToStringPercent(),
                "cross=global:" + settings.globalCrossPollinationChance.ToStringPercent()
                    + " minimumDonorGrowth:" + settings.MinimumDonorGrowth.ToStringPercent()
                    + " secondSlot:" + settings.SecondCrossPollinationTraitChance.ToStringPercent()
                    + " laterSlots:" + settings.LaterCrossPollinationTraitChance.ToStringPercent()
                    + " maxDonorTraits:" + settings.MaxCrossPollinationTraits,
                "traits=maxNew:" + settings.maxTraitsPerEvent + " defs:" + DefDatabase<VarietyTraitDef>.DefCount,
                "produceVisuals=" + settings.enableProduceVisuals
            };
        }

        private static List<string> BalanceTest(string argument)
        {
            NovelSeedsSettings settings = HorticultureNovelSeedsMod.Settings;
            if (settings == null) return new List<string> { "error=settings unavailable" };

            string[] parts = (argument ?? string.Empty).Split(new[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
            int runs = 1000;
            string cropFilter = null;
            foreach (string part in parts)
            {
                if (int.TryParse(part, out int parsed)) runs = Math.Max(100, Math.Min(10000, parsed));
                else cropFilter = part;
            }

            List<ThingDef> crops = DefDatabase<ThingDef>.AllDefsListForReading
                .Where(NovelSeedUtility.IsGrowableCrop)
                .Where(def => cropFilter.NullOrEmpty()
                    || def.defName.IndexOf(cropFilter, StringComparison.OrdinalIgnoreCase) >= 0
                    || def.label.IndexOf(cropFilter, StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderBy(def => def.defName).ToList();
            if (crops.Count == 0) return new List<string> { "error=no matching growable plants", "filter=" + Clean(cropFilter) };

            bool originalEnabled = settings.enableTraitBalancing;
            SimulationStats balanced;
            SimulationStats baseline;
            Stopwatch watch = Stopwatch.StartNew();
            try
            {
                settings.enableTraitBalancing = true;
                balanced = RunBalanceSimulation(crops, runs, settings, 731947);
                settings.enableTraitBalancing = false;
                baseline = RunBalanceSimulation(crops, runs, settings, 731947);
            }
            finally
            {
                settings.enableTraitBalancing = originalEnabled;
            }
            watch.Stop();

            var result = new List<string>
            {
                "test=traitBalance runs:" + runs + " crops:" + crops.Count + " filter:" + Clean(cropFilter ?? "all"),
                "config=strength:" + settings.traitBalanceStrength.ToStringPercent() + " tolerance:" + settings.allowedTraitImbalance
                    + " exceptional:" + settings.exceptionalVarietyChance.ToStringPercent() + " maxNew:" + settings.MaxTraitsPerEvent,
                balanced.Line("balanced"),
                baseline.Line("baseline"),
                "invariants=balanced:" + balanced.InvariantFailures + " baseline:" + baseline.InvariantFailures,
                "performance=totalMs:" + watch.Elapsed.TotalMilliseconds.ToString("0.0")
                    + " perEventUs:" + (watch.Elapsed.TotalMilliseconds * 1000d / Math.Max(1, runs * 2)).ToString("0.0")
            };
            float reduction = baseline.MeanAbsoluteScore - balanced.MeanAbsoluteScore;
            result.Add("finding=meanAbsoluteScoreChange:" + (-reduction).ToString("+0.00;-0.00;0.00")
                + " withinToleranceChange:" + (balanced.WithinToleranceRate - baseline.WithinToleranceRate).ToString("+0.0%;-0.0%;0.0%"));
            if (balanced.InvariantFailures > 0) result.Add("insight=balance selection produced invalid combinations; fix generation before extending traits");
            else if (balanced.Generated == 0) result.Add("insight=current trait configuration generated no testable outcomes");
            else if (balanced.WithinToleranceRate > 0.9f && balanced.StrongTailRate < settings.exceptionalVarietyChance * 0.5f)
                result.Add("insight=balance is compressing variety extremes; future archetype or breakthrough events should preserve rare identity");
            else if (reduction < 0.15f)
                result.Add("insight=current trait pool or event cap limits compensation; paired-trait packages would improve reliable balancing");
            else
                result.Add("insight=balancing materially reduces extremes while retaining " + balanced.StrongTailRate.ToStringPercent() + " exceptional outcomes");
            return result;
        }

        private static SimulationStats RunBalanceSimulation(List<ThingDef> crops, int runs, NovelSeedsSettings settings, int seed)
        {
            var stats = new SimulationStats(settings.allowedTraitImbalance);
            Rand.PushState(seed);
            try
            {
                for (int i = 0; i < runs; i++)
                {
                    ThingDef crop = crops[i % crops.Count];
                    List<VarietyTraitDef> traits = NovelSeedUtility.RandomTraitSet(crop);
                    stats.Record(traits, settings.MaxTraitsPerEvent);
                }
            }
            finally
            {
                Rand.PopState();
            }
            return stats;
        }

        private sealed class SimulationStats
        {
            private readonly int tolerance;
            private float absoluteScoreTotal;
            private int withinTolerance;
            private int strongTail;
            private int traitTotal;
            public int Attempts { get; private set; }
            public int Generated { get; private set; }
            public int Empty { get; private set; }
            public int InvariantFailures { get; private set; }
            public float MeanAbsoluteScore => Generated == 0 ? 0f : absoluteScoreTotal / Generated;
            public float WithinToleranceRate => Generated == 0 ? 0f : (float)withinTolerance / Generated;
            public float StrongTailRate => Generated == 0 ? 0f : (float)strongTail / Generated;

            public SimulationStats(int tolerance)
            {
                this.tolerance = tolerance;
            }

            public void Record(List<VarietyTraitDef> traits, int maxTraits)
            {
                Attempts++;
                traits = traits?.Where(trait => trait != null).ToList() ?? new List<VarietyTraitDef>();
                if (traits.Count == 0)
                {
                    Empty++;
                    return;
                }
                Generated++;
                float score = NovelSeedUtility.TraitBalanceScore(traits);
                float absolute = Math.Abs(score);
                absoluteScoreTotal += absolute;
                traitTotal += traits.Count;
                if (absolute <= tolerance) withinTolerance++;
                if (absolute >= 3f) strongTail++;
                if (traits.Count > maxTraits || HasDuplicateFamily(traits) || HasExclusionConflict(traits)) InvariantFailures++;
            }

            public string Line(string mode)
            {
                return "mode=" + mode + " generated:" + Generated + " empty:" + Empty
                    + " meanAbs:" + MeanAbsoluteScore.ToString("0.00")
                    + " within:" + WithinToleranceRate.ToStringPercent()
                    + " strongTail:" + StrongTailRate.ToStringPercent()
                    + " avgTraits:" + (Generated == 0 ? 0f : (float)traitTotal / Generated).ToString("0.00");
            }

            private static bool HasDuplicateFamily(List<VarietyTraitDef> traits)
            {
                return traits.Where(trait => !trait.configFamily.NullOrEmpty())
                    .GroupBy(trait => trait.configFamily).Any(group => group.Count() > 1);
            }

            private static bool HasExclusionConflict(List<VarietyTraitDef> traits)
            {
                for (int i = 0; i < traits.Count; i++)
                for (int j = i + 1; j < traits.Count; j++)
                    if ((traits[i].exclusionTags ?? new List<string>())
                        .Intersect(traits[j].exclusionTags ?? new List<string>()).Any()) return true;
                return false;
            }
        }

        private static List<string> Registry()
        {
            GameComponent_NovelSeeds component = GameComponent_NovelSeeds.Instance;
            if (component == null) return new List<string> { "registry=unavailable" };
            List<VarietyRecord> varieties = component.AllVarieties.ToList();
            return new List<string>
            {
                "registry=varieties:" + varieties.Count + " favorites:" + varieties.Count(variety => variety.registryFavorite)
                    + " archived:" + varieties.Count(variety => variety.registryArchived),
                "plantKnowledge=colony:" + KnowledgeQuery.ColonyFacets(HorticultureKnowledgeAdapter.DomainId)
                    .Select(record => record.subjectId).Distinct().Count(),
                "apiVersion=" + KnowledgeFrameworkApi.ApiVersion,
                "domainRegistered=" + (KnowledgeRegistry.Schema(HorticultureKnowledgeAdapter.DomainId) != null)
            };
        }

        private static List<string> OpenRegistry()
        {
            MainButtonDef def = DefDatabase<MainButtonDef>.GetNamedSilentFail("HNS_CultivarRegistry");
            if (def == null) return new List<string> { "error=registry main button def missing" };
            Find.MainTabsRoot.SetCurrentTab(def, true);
            return new List<string> { "opened=True", "tab=" + def.defName, "window=" + def.TabWindow?.GetType().FullName };
        }

        private static List<string> RegistryUi()
        {
            object window = RegistryWindow();
            if (window == null) return new List<string> { "registryUi=closed" };
            Type type = window.GetType();
            BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            object page = type.GetField("page", flags)?.GetValue(window);
            object scope = type.GetField("knowledgeState", flags)?.GetValue(window)?.GetType()
                .GetField("scope")?.GetValue(type.GetField("knowledgeState", flags)?.GetValue(window));
            object comparisons = type.GetField("comparisonIds", flags)?.GetValue(window);
            int comparisonCount = (int?)(comparisons?.GetType().GetProperty("Count")?.GetValue(comparisons, null)) ?? 0;
            return new List<string>
            {
                "registryUi=open page:" + page + " scope:" + scope,
                "comparisonCount=" + comparisonCount,
                "compareEnabled=" + (comparisonCount >= 2),
                "window=" + Clean((window as Window)?.windowRect.ToString())
            };
        }

        private static List<string> SetRegistryPage(string argument)
        {
            object window = RegistryWindow();
            if (window == null) return new List<string> { "error=registry is not open" };
            FieldInfo field = window.GetType().GetField("page", BindingFlags.Instance | BindingFlags.NonPublic);
            try { field.SetValue(window, Enum.Parse(field.FieldType, argument ?? string.Empty, true)); }
            catch { return new List<string> { "error=page must be Plants, Cultivars, Knowledge, or Compare" }; }
            return RegistryUi();
        }

        private static List<string> SetRegistryCompare(string argument)
        {
            object window = RegistryWindow();
            if (window == null) return new List<string> { "error=registry is not open" };
            object set = window.GetType().GetField("comparisonIds", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(window);
            MethodInfo clear = set?.GetType().GetMethod("Clear");
            MethodInfo add = set?.GetType().GetMethod("Add");
            clear?.Invoke(set, null);
            if (!string.Equals(argument, "clear", StringComparison.OrdinalIgnoreCase))
            {
                List<VarietyRecord> varieties = AllVarieties();
                if (varieties.Count < 2)
                {
                    ThingDef crop = varieties.FirstOrDefault()?.cropDef ?? DefDatabase<ThingDef>.AllDefsListForReading
                        .FirstOrDefault(NovelSeedUtility.IsGrowableCrop);
                    List<VarietyTraitDef> traits = DefDatabase<VarietyTraitDef>.AllDefsListForReading
                        .Where(trait => trait != null && HorticultureNovelSeedsMod.Settings?.IsTraitAllowed(crop, trait) != false).Take(2).ToList();
                    for (int i = varieties.Count; i < 2 && crop != null && i < traits.Count; i++)
                    {
                        VarietyRecord created = GameComponent_NovelSeeds.Instance?.UnlockVariety(crop,
                            new List<VarietyTraitDef> { traits[i] }, "Registry Test " + (i + 1));
                        if (created != null) varieties.Add(created);
                    }
                }
                foreach (VarietyRecord variety in varieties.Distinct().Take(2)) add?.Invoke(set, new object[] { variety.id });
            }
            return RegistryUi();
        }

        private static List<string> SetRegistryScope(string argument)
        {
            object window = RegistryWindow();
            if (window == null) return new List<string> { "error=registry is not open" };
            object state = window.GetType().GetField("knowledgeState", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(window);
            FieldInfo scope = state?.GetType().GetField("scope");
            try { scope.SetValue(state, Enum.Parse(scope.FieldType, argument ?? string.Empty, true)); }
            catch { return new List<string> { "error=scope must be Colony or Colonist" }; }
            return RegistryUi();
        }

        private static List<string> AutoMasks(string argument)
        {
            string filter = (argument ?? string.Empty).Trim();
            List<ThingDef> plants = DefDatabase<ThingDef>.AllDefsListForReading
                .Where(def => def?.plant != null && (filter.NullOrEmpty() || def.defName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0))
                .OrderBy(def => def.defName).Take(filter.NullOrEmpty() ? 12 : 60).ToList();
            List<string> result = new List<string> { "cache=" + PlantAutoMaskCache.CachePath, "plants=" + plants.Count };
            foreach (ThingDef plant in plants)
            {
                PlantSettingsRecord settings = HorticultureNovelSeedsMod.Settings?.GetPlantSettings(plant, false);
                int count = PlantMaskUtility.VariationCount(plant);
                for (int variation = 0; variation < count; variation++)
                {
                    AutoPlantMaskRecord auto = PlantAutoMaskCache.GetRecord(plant, variation, false);
                    List<VisualMaskLayerRecord> resolved = PlantMaskUtility.LayersForVariation(plant, variation, false);
                    result.Add("mask=plant:" + plant.defName + " variation:" + variation + " label:" + Clean(PlantMaskUtility.VariationLabel(plant, variation))
                        + " source:" + (settings?.HasManualPlantMask(variation) == true ? "Manual" : auto != null ? "Auto" : "None")
                        + " confidence:" + (auto?.Confidence.ToString("0.000") ?? "n/a") + " review:" + (auto?.LowConfidence == true)
                        + " pixels:" + string.Join(",", Enumerable.Range(0, 3).Select(layer => CountMaskPixels(resolved, layer).ToString())));
                }
            }
            if (filter.Equals("review", StringComparison.OrdinalIgnoreCase))
            {
                result.Clear();
                result.Add("review=low-confidence");
                foreach (ThingDef plant in DefDatabase<ThingDef>.AllDefsListForReading.Where(def => def?.plant != null).OrderBy(def => def.defName))
                    for (int variation = 0; variation < PlantMaskUtility.VariationCount(plant); variation++)
                    {
                        AutoPlantMaskRecord auto = PlantAutoMaskCache.GetRecord(plant, variation, false);
                        if (auto?.LowConfidence == true)
                            result.Add("mask=plant:" + plant.defName + " variation:" + variation + " confidence:" + auto.Confidence.ToString("0.000"));
                    }
            }
            return result;
        }

        private static int CountMaskPixels(List<VisualMaskLayerRecord> layers, int layer)
        {
            if (layers == null || layer >= layers.Count) return 0;
            int count = 0;
            for (int y = 0; y < VisualMaskLayerRecord.Resolution; y++)
                for (int x = 0; x < VisualMaskLayerRecord.Resolution; x++)
                    if (layers[layer].IsPainted(x, y)) count++;
            return count;
        }

        private static List<string> MaskRegressions()
        {
            Type type = typeof(PlantAutoMaskCache);
            BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            string[] methods = { "StemTopologyRegression", "StemTopologyRegressionDetails", "ForcedStemCredibilityRegression", "GroundcoverStemRegression", "LayerAbsenceRegression", "LowConfidenceFallbackRegression", "VisualRecolorRegression", "DeterministicClassificationRegression", "MaskCorrectionRegression" };
            List<string> result = new List<string>();
            foreach (string method in methods)
            {
                try { result.Add(method + "=" + type.GetMethod(method, flags)?.Invoke(null, null)); }
                catch (Exception exception) { result.Add(method + "=error:" + (exception.InnerException ?? exception).Message); }
            }
            try { result.Add("MaskPainterOperationsRegression=" + typeof(MaskPainterOperations).GetMethod("MaskPainterOperationsRegression", flags)?.Invoke(null, null)); }
            catch (Exception exception) { result.Add("MaskPainterOperationsRegression=error:" + (exception.InnerException ?? exception).Message); }
            return result;
        }

        private static List<string> CrossRegressions()
        {
            try
            {
                MethodInfo method = typeof(NovelSeedsDebugActions).GetMethod("CrossPollinationRegression",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                return new List<string> { "CrossPollinationRegression=" + method?.Invoke(null, null) };
            }
            catch (Exception exception)
            {
                return new List<string> { "CrossPollinationRegression=error:" + (exception.InnerException ?? exception).Message };
            }
        }

        private static List<string> TraitCatalogRegressions()
        {
            try
            {
                MethodInfo method = typeof(NovelSeedsDebugActions).GetMethod("TraitCatalogRegression",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                return new List<string> { "TraitCatalogRegression=" + method?.Invoke(null, null) };
            }
            catch (Exception exception)
            {
                return new List<string> { "TraitCatalogRegression=error:" + (exception.InnerException ?? exception).Message };
            }
        }

        private static List<string> BreedingMixDiagnostic()
        {
            try
            {
                MethodInfo resultMethod = typeof(NovelSeedsDebugActions).GetMethod("BreedingMixDiagnostic",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                MethodInfo reportMethod = typeof(NovelSeedsDebugActions).GetMethod("BreedingMixDiagnosticReport",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                return new List<string>
                {
                    "BreedingMixDiagnostic=" + resultMethod?.Invoke(null, null),
                    "BreedingMixReport=" + reportMethod?.Invoke(null, null)
                };
            }
            catch (Exception exception)
            {
                return new List<string> { "BreedingMixDiagnostic=error:" + (exception.InnerException ?? exception).Message };
            }
        }

        private static List<string> ExportMaskDiagnostic(string argument)
        {
            string[] parts = (argument ?? string.Empty).Split('|');
            ThingDef plant = DefDatabase<ThingDef>.GetNamedSilentFail(parts.ElementAtOrDefault(0)?.Trim());
            if (plant?.plant == null) return new List<string> { "error=plant def was not found" };
            int variation = 0;
            if (parts.Length > 1) int.TryParse(parts[1], out variation);
            variation = Math.Max(0, Math.Min(variation, PlantMaskUtility.VariationCount(plant) - 1));
            string outputDirectory = parts.Length > 2 && !parts[2].NullOrEmpty()
                ? string.Join("|", parts.Skip(2).ToArray())
                : Path.Combine(GenFilePaths.ConfigFolderPath, "HNS-MaskDiagnostics");
            Directory.CreateDirectory(outputDirectory);

            Texture sourceTexture = PlantMaskUtility.TextureForVariation(plant, variation);
            List<VisualMaskLayerRecord> layers = PlantMaskUtility.LayersForVariation(plant, variation, false);
            if (layers == null)
                layers = PlantAutoMaskCache.GetRecord(plant, variation, false)?.Layers.Select(layer => layer).ToList();
            if (sourceTexture == null || layers == null || layers.Count < 3)
                return new List<string> { "error=source texture or resolved masks are unavailable" };

            const int size = VisualMaskLayerRecord.Resolution;
            Color32[] source = ReadTexturePixels(sourceTexture, size);
            string stem = SafeFileName(plant.defName + "-v" + variation + "-" + PlantMaskUtility.VariationLabel(plant, variation));
            string sourcePath = Path.Combine(outputDirectory, stem + "-source.png");
            SavePng(sourcePath, size, size, source);
            List<string> result = new List<string> { "plant=" + plant.defName, "variation=" + variation, "source=" + sourcePath };
            result.Add("metadata=isTree:" + plant.plant.IsTree + " treeCategory:" + plant.plant.treeCategory
                + " forceIsTree:" + plant.plant.forceIsTree + " dieIfLeafless:" + plant.plant.dieIfLeafless
                + " harvestTag:" + (plant.plant.harvestTag ?? "none") + " product:" + (plant.plant.harvestedThingDef?.defName ?? "none")
                + " immatureReference:" + (PlantMaskUtility.ReferenceTextureForVariation(plant, variation, "Immature") != null)
                + " leaflessReference:" + (PlantMaskUtility.ReferenceTextureForVariation(plant, variation, "Leafless") != null));

            ThingDef product = plant.plant.harvestedThingDef;
            Texture productTexture = product?.uiIcon;
            if (productTexture == null || productTexture.name.Equals("BadTexture", StringComparison.OrdinalIgnoreCase))
                productTexture = product?.graphicData?.texPath.NullOrEmpty() == false
                    ? ContentFinder<Texture2D>.Get(product.graphicData.texPath, false) : null;
            if (productTexture != null && !productTexture.name.Equals("BadTexture", StringComparison.OrdinalIgnoreCase))
            {
                string productPath = Path.Combine(outputDirectory, stem + "-reference-product.png");
                SavePng(productPath, size, size, ReadTexturePixels(productTexture, size));
                result.Add("referenceProduct=" + productPath);
            }

            foreach (string state in new[] { "Immature", "Leafless" })
            {
                Texture referenceTexture = PlantMaskUtility.ReferenceTextureForVariation(plant, variation, state);
                if (referenceTexture == null) continue;
                string referencePath = Path.Combine(outputDirectory, stem + "-reference-" + state.ToLowerInvariant() + ".png");
                SavePng(referencePath, size, size, ReadTexturePixels(referenceTexture, size));
                result.Add("reference" + state + "=" + referencePath);
            }

            Color32[] overlay = new Color32[source.Length];
            Color32[] tint = { new Color32(79, 196, 112, 255), new Color32(238, 76, 85, 255), new Color32(76, 137, 232, 255) };
            string[] names = { "produce", "leaves", "stem" };
            for (int layerIndex = 0; layerIndex < 3; layerIndex++)
            {
                Color32[] mask = new Color32[source.Length];
                int painted = 0;
                for (int topY = 0; topY < size; topY++)
                {
                    int sourceY = size - 1 - topY;
                    for (int x = 0; x < size; x++)
                    {
                        int index = sourceY * size + x;
                        if (!layers[layerIndex].IsPainted(x, topY)) continue;
                        mask[index] = tint[layerIndex];
                        overlay[index] = tint[layerIndex];
                        painted++;
                    }
                }
                string maskPath = Path.Combine(outputDirectory, stem + "-" + names[layerIndex] + ".png");
                SavePng(maskPath, size, size, mask);
                result.Add(names[layerIndex] + "=" + maskPath + " pixels:" + painted);
            }
            for (int index = 0; index < overlay.Length; index++)
            {
                if (source[index].a <= 4) { overlay[index] = new Color32(0, 0, 0, 0); continue; }
                if (overlay[index].a > 0) continue;
                byte gray = (byte)Mathf.Clamp(Mathf.RoundToInt((source[index].r + source[index].g + source[index].b) / 3f * 0.38f), 18, 96);
                overlay[index] = new Color32(gray, gray, gray, source[index].a);
            }
            string overlayPath = Path.Combine(outputDirectory, stem + "-overlay.png");
            SavePng(overlayPath, size, size, overlay);
            result.Add("overlay=" + overlayPath);
            return result;
        }

        private static Color32[] ReadTexturePixels(Texture texture, int size)
        {
            RenderTexture temporary = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGB32);
            RenderTexture previous = RenderTexture.active;
            Texture2D readable = null;
            try
            {
                Graphics.Blit(texture, temporary);
                RenderTexture.active = temporary;
                readable = new Texture2D(size, size, TextureFormat.RGBA32, false);
                readable.ReadPixels(new Rect(0f, 0f, size, size), 0, 0, false);
                readable.Apply(false, false);
                return readable.GetPixels32();
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(temporary);
                if (readable != null) UnityEngine.Object.Destroy(readable);
            }
        }

        private static void SavePng(string path, int width, int height, Color32[] pixels)
        {
            Texture2D image = new Texture2D(width, height, TextureFormat.RGBA32, false);
            try
            {
                image.SetPixels32(pixels);
                image.Apply(false, false);
                File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.Destroy(image);
            }
        }

        private static string SafeFileName(string value)
        {
            foreach (char invalid in Path.GetInvalidFileNameChars()) value = value.Replace(invalid, '_');
            return value.Replace(' ', '_');
        }

        private static List<string> GenerateAutoMasks()
        {
            Stopwatch watch = Stopwatch.StartNew();
            AutoMaskBatchResult result = PlantAutoMaskCache.GenerateMissing(false);
            watch.Stop();
            return new List<string>
            {
                "generated=" + result.generated, "reused=" + result.reused, "manualSkipped=" + result.manualSkipped,
                "review=" + result.lowConfidence, "failed=" + result.failed, "elapsedMs=" + watch.ElapsedMilliseconds
            };
        }

        private static List<string> OpenMaskEditor(string argument)
        {
            string[] parts = (argument ?? string.Empty).Split('|');
            ThingDef plant = DefDatabase<ThingDef>.GetNamedSilentFail(parts[0].Trim());
            if (plant?.plant == null) return new List<string> { "error=plant def not found" };
            Dialog_PlantMasks editor = new Dialog_PlantMasks(plant);
            if (parts.Length > 1 && int.TryParse(parts[1], out int variation))
                editor.GetType().GetField("selectedVariation", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(editor,
                    Math.Max(0, Math.Min(variation, PlantMaskUtility.VariationCount(plant) - 1)));
            Find.WindowStack.Add(editor);
            return new List<string> { "opened=True", "plant=" + plant.defName, "variations=" + PlantMaskUtility.VariationCount(plant) };
        }

        private static List<string> MaskEditorState()
        {
            Dialog_PlantMasks editor = Find.WindowStack.Windows.OfType<Dialog_PlantMasks>().LastOrDefault();
            if (editor == null) return new List<string> { "open=False" };
            BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            ThingDef plant = editor.GetType().GetField("plantDef", flags)?.GetValue(editor) as ThingDef;
            int variation = (int)(editor.GetType().GetField("selectedVariation", flags)?.GetValue(editor) ?? 0);
            PlantSettingsRecord settings = HorticultureNovelSeedsMod.Settings?.GetPlantSettings(plant, false);
            AutoPlantMaskRecord auto = PlantAutoMaskCache.GetRecord(plant, variation, false);
            Type type = editor.GetType();
            int selectedLayer = (int)(type.GetField("selectedLayer", flags)?.GetValue(editor) ?? 0);
            Array locks = type.GetField("channelLocks", flags)?.GetValue(editor) as Array;
            object paintMode = type.GetField("paintSelectionMode", flags)?.GetValue(editor);
            object previewMode = type.GetField("previewMode", flags)?.GetValue(editor);
            MaskValidationResult validation = type.GetField("validationResult", flags)?.GetValue(editor) as MaskValidationResult;
            List<VisualMaskLayerRecord> layers = type.GetProperty("CurrentLayers", flags)?.GetValue(editor, null) as List<VisualMaskLayerRecord>;
            return new List<string>
            {
                "open=True", "plant=" + plant?.defName, "variation=" + variation,
                "source=" + (settings?.HasManualPlantMask(variation) == true ? "Manual" : "Auto-generated"),
                "confidence=" + (auto?.Confidence.ToString("0.000") ?? "n/a"), "review=" + (auto?.LowConfidence == true),
                "selectedLayer=" + selectedLayer, "paintMode=" + paintMode, "preview=" + previewMode,
                "locks=" + (locks == null ? "n/a" : string.Join(",", locks.Cast<object>())),
                "pixels=" + (layers == null ? "n/a" : string.Join(",", Enumerable.Range(0, Math.Min(3, layers.Count)).Select(layer => CountMaskPixels(layers, layer)))),
                "validation=" + (validation == null ? "none" : "transparent:" + validation.transparentPixels + ",overlap:" + validation.overlappingPixels
                    + ",empty:" + validation.emptyChannels + ",tiny:" + validation.tinyFragments + ",gaps:" + validation.unmaskedVisiblePixels)
            };
        }

        private static List<string> MaskEditorAction(string argument)
        {
            Dialog_PlantMasks editor = Find.WindowStack.Windows.OfType<Dialog_PlantMasks>().LastOrDefault();
            if (editor == null) return new List<string> { "error=mask editor is not open" };
            string action = (argument ?? string.Empty).Trim().ToLowerInvariant();
            Stopwatch watch = Stopwatch.StartNew();
            Dictionary<string, string> methods = new Dictionary<string, string>
            {
                ["promote"] = "PromoteAutoToManual", ["regenerate"] = "RegenerateAutoMask", ["reset"] = "ResetToAutoMask",
                ["grow"] = "GrowSelection", ["shrink"] = "ShrinkSelection", ["smooth"] = "SmoothSelection",
                ["feather"] = "FeatherSelection", ["tiny"] = "RemoveTinyFragments", ["holes"] = "FillSelectionHoles",
                ["largest"] = "KeepLargestSelection", ["smart"] = "SmartExpandSelection", ["validate"] = "ValidateCurrentMask",
                ["undo"] = "Undo", ["redo"] = "Redo"
            };
            BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            if (methods.TryGetValue(action, out string method)) editor.GetType().GetMethod(method, flags)?.Invoke(editor, null);
            else if (action.StartsWith("preview:"))
            {
                FieldInfo field = editor.GetType().GetField("previewMode", flags);
                string name = action.Substring("preview:".Length);
                field?.SetValue(editor, Enum.Parse(field.FieldType, name, true));
            }
            else if (action.StartsWith("lock:"))
            {
                if (int.TryParse(action.Substring("lock:".Length), out int layer))
                    editor.GetType().GetMethod("ToggleChannelLock", flags)?.Invoke(editor, new object[] { layer });
            }
            else if (action.StartsWith("scroll:"))
            {
                if (float.TryParse(action.Substring("scroll:".Length), out float y))
                    editor.GetType().GetField("controlsScroll", flags)?.SetValue(editor, new Vector2(0f, Math.Max(0f, y)));
            }
            else if (action.StartsWith("copy:") || action.StartsWith("project:"))
            {
                int separator = action.IndexOf(':');
                if (int.TryParse(action.Substring(separator + 1), out int target))
                    editor.GetType().GetMethod(action.StartsWith("copy:") ? "CopyMaskToVariation" : "ProjectMaskToVariation", flags)
                        ?.Invoke(editor, new object[] { target });
            }
            else if (action.StartsWith("variation:"))
            {
                if (int.TryParse(action.Substring("variation:".Length), out int variation))
                    editor.GetType().GetMethod("SelectVariation", flags)?.Invoke(editor, new object[] { variation, false });
            }
            else return new List<string> { "error=unknown mask editor action" };
            watch.Stop();
            List<string> result = MaskEditorState();
            result.Add("elapsedMs=" + watch.Elapsed.TotalMilliseconds.ToString("0.00"));
            return result;
        }

        private static List<string> CaptureUi(string argument)
        {
            string path = (argument ?? string.Empty).Trim();
            if (path.NullOrEmpty()) path = Path.Combine(GenFilePaths.ConfigFolderPath, "HNS-MaskPainter.png");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            Texture2D screenshot = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            try
            {
                screenshot.ReadPixels(new Rect(0f, 0f, Screen.width, Screen.height), 0, 0, false);
                screenshot.Apply(false, false);
                File.WriteAllBytes(path, screenshot.EncodeToPNG());
                return new List<string> { "capture=" + path, "width=" + Screen.width, "height=" + Screen.height };
            }
            finally { UnityEngine.Object.Destroy(screenshot); }
        }

        private static List<string> DevMaskGizmo(Map map, string argument)
        {
            if (map == null) return NoMap();
            string[] parts = (argument ?? string.Empty).Split('|');
            string requestedDef = parts[0].Trim();
            bool invoke = parts.Length > 1 && string.Equals(parts[1], "invoke", StringComparison.OrdinalIgnoreCase);
            Plant plant = map.listerThings.ThingsInGroup(ThingRequestGroup.Plant).OfType<Plant>()
                .Where(item => requestedDef.NullOrEmpty() || string.Equals(item.def.defName, requestedDef, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(item => item.def.plant?.IsTree == true).ThenByDescending(item => item.Growth).FirstOrDefault();
            if (plant == null) return new List<string> { "error=no matching live plant" };
            Find.Selector.ClearSelection();
            Find.Selector.Select(plant);
            List<Gizmo> gizmos = plant.GetGizmos().ToList();
            Command_Action command = gizmos.OfType<Command_Action>()
                .FirstOrDefault(item => item.defaultLabel == "DEV: Edit Plant Mask");
            if (invoke) command?.action?.Invoke();
            return new List<string>
            {
                "plant=" + plant.def.defName, "thingId=" + plant.thingIDNumber,
                "isTree=" + (plant.def.plant?.IsTree == true), "gizmos=" + gizmos.Count,
                "maskGizmo=" + (command != null), "invoked=" + (invoke && command != null)
            }.Concat(gizmos.OfType<Command>().Select(item => "gizmo=" + Clean(item.defaultLabel))).ToList();
        }

        private static List<string> DevRandomGrid(Map map, string argument)
        {
            if (map == null) return NoMap();
            Type actions = typeof(NovelSeedsDebugActions);
            BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            MethodInfo regression = actions.GetMethod("RandomVarietyGridRegression", flags);
            MethodInfo prepare = actions.GetMethod("PrepareRandomGridVarieties", flags);
            MethodInfo plantGrid = actions.GetMethod("PlantRandomVarietyGrid", flags, null,
                new[] { typeof(IntVec3), typeof(Map), typeof(List<List<VarietyRecord>>) }, null);
            MethodInfo gridCells = actions.GetMethod("RandomGridCells", flags);
            if (regression == null || prepare == null || plantGrid == null || gridCells == null)
                return new List<string> { "error=random grid methods unavailable" };
            if (!(bool)regression.Invoke(null, null))
                return new List<string> { "error=random grid regression failed" };
            IntVec3 center;
            string[] coordinates = (argument ?? string.Empty).Split(',');
            if (coordinates.Length == 2 && int.TryParse(coordinates[0], out int x) && int.TryParse(coordinates[1], out int z))
                center = new IntVec3(x, 0, z);
            else
                center = map.AllCells.FirstOrDefault(candidate =>
                {
                    IEnumerable<IntVec3> footprint = gridCells.Invoke(null, new object[] { candidate }) as IEnumerable<IntVec3>;
                    return footprint != null && footprint.All(cell => cell.InBounds(map) && cell.GetEdifice(map) == null
                        && map.fertilityGrid.FertilityAt(cell) > 0f);
                });
            object groups = prepare.Invoke(null, new object[] { GameComponent_NovelSeeds.Instance });
            int planted = (int)plantGrid.Invoke(null, new[] { (object)center, map, groups });
            CameraJumper.TryJump(center, map);
            IEnumerable<IntVec3> footprint = gridCells.Invoke(null, new object[] { center }) as IEnumerable<IntVec3>;
            HashSet<IntVec3> cells = new HashSet<IntVec3>(footprint ?? Enumerable.Empty<IntVec3>());
            List<Plant> plants = map.listerThings.ThingsInGroup(ThingRequestGroup.Plant).OfType<Plant>()
                .Where(plant => cells.Contains(plant.Position)).ToList();
            List<CompPlantVariety> comps = plants.Select(plant => plant.TryGetComp<CompPlantVariety>()).Where(comp => comp != null).ToList();
            List<int> speciesCounts = plants.GroupBy(plant => plant.def).Select(group => group.Count()).ToList();
            List<string> result = new List<string>
            {
                "regression=True", "center=" + center.x + "," + center.z, "planted=" + planted,
                "footprintPlants=" + plants.Count, "assigned=" + comps.Count(comp => comp.Variety != null),
                "species=" + plants.Select(plant => plant.def).Distinct().Count(),
                "minPerSpecies=" + (speciesCounts.Count > 0 ? speciesCounts.Min() : 0),
                "maxPerSpecies=" + (speciesCounts.Count > 0 ? speciesCounts.Max() : 0),
                "varieties=" + comps.Select(comp => comp.VarietyId).Where(id => !id.NullOrEmpty()).Distinct().Count(),
                "mature=" + plants.Count(plant => plant.Growth >= 0.999f), "sown=" + plants.Count(plant => plant.sown)
            };
            result.AddRange(cells.Where(cell => cell.GetPlant(map) == null).Take(10)
                .Select(cell => "empty=" + cell.x + "," + cell.z + " terrain:" + (cell.GetTerrain(map)?.defName ?? "null")
                    + " fertility:" + map.fertilityGrid.FertilityAt(cell).ToString("0.00")));
            return result;
        }

        private static object RegistryWindow()
        {
            MainButtonDef def = DefDatabase<MainButtonDef>.GetNamedSilentFail("HNS_CultivarRegistry");
            return def?.TabWindow is MainTabWindow_CultivarRegistry ? def.TabWindow : null;
        }

        private static List<string> WildlifeLayout()
        {
            Type type = typeof(MainTabWindow_Wildlife);
            MainTabWindow window = DefDatabase<MainButtonDef>.GetNamed("Wildlife").TabWindow;
            BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var result = new List<string>
            {
                "type=" + type.FullName + " base:" + type.BaseType?.FullName,
                "window=initial:" + window.InitialSize + " requested:" + window.RequestedTabSize + " rect:" + window.windowRect
            };
            foreach (PropertyInfo property in type.GetProperties(flags)
                .Where(property => property.Name.IndexOf("top", StringComparison.OrdinalIgnoreCase) >= 0
                    || property.Name.IndexOf("size", StringComparison.OrdinalIgnoreCase) >= 0
                    || property.Name.IndexOf("margin", StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderBy(property => property.Name))
            {
                string value;
                try { value = property.GetIndexParameters().Length == 0 ? Clean(property.GetValue(window, null)?.ToString()) : "indexed"; }
                catch (Exception exception) { value = "error:" + exception.GetType().Name; }
                result.Add("property=" + property.DeclaringType?.Name + "." + property.Name + " type:" + property.PropertyType.Name + " value:" + value);
            }
            foreach (FieldInfo field in type.GetFields(flags)
                .Where(field => field.Name.IndexOf("top", StringComparison.OrdinalIgnoreCase) >= 0
                    || field.Name.IndexOf("button", StringComparison.OrdinalIgnoreCase) >= 0
                    || field.Name.IndexOf("rect", StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderBy(field => field.Name))
            {
                string value;
                try { value = Clean(field.GetValue(window)?.ToString()); }
                catch (Exception exception) { value = "error:" + exception.GetType().Name; }
                result.Add("field=" + field.DeclaringType?.Name + "." + field.Name + " type:" + field.FieldType.Name + " value:" + value);
            }
            return result;
        }

        private static List<string> Validate(Map map)
        {
            if (map == null) return NoMap();
            List<VarietyRecord> varieties = AllVarieties();
            List<PlantState> plants = NovelPlants(map);
            var issues = new List<string>();
            foreach (IGrouping<string, VarietyRecord> duplicate in varieties.Where(item => !item.id.NullOrEmpty()).GroupBy(item => item.id).Where(item => item.Count() > 1))
                issues.Add("duplicateVarietyId=" + duplicate.Key + " count:" + duplicate.Count());
            foreach (PlantState item in plants.Where(item => !item.Comp.VarietyId.NullOrEmpty() && item.Comp.Variety == null))
                issues.Add("missingVariety=plant:" + item.Plant.thingIDNumber + " id:" + item.Comp.VarietyId);
            foreach (VarietyRecord variety in varieties.Where(item => item.cropDef == null))
                issues.Add("missingCrop=variety:" + variety.id);
            MethodInfo maskCorrectionMethod = AccessTools.Method(typeof(PlantAutoMaskCache), "MaskCorrectionRegression");
            bool maskCorrection = maskCorrectionMethod != null && (bool)maskCorrectionMethod.Invoke(null, null);
            if (!maskCorrection) issues.Add("autoMaskCorrection=failed");
            return new List<string> { "valid=" + (issues.Count == 0), "issues=" + issues.Count,
                "autoMaskCorrection=" + maskCorrection }.Concat(issues.Take(50)).ToList();
        }

        private static List<string> LoadTestSave(string argument)
        {
            string name = (argument ?? string.Empty).Trim();
            if (name.NullOrEmpty()) return new List<string> { "error=save name required" };
            GameDataSaveLoader.CheckVersionAndLoadGame(name);
            return new List<string> { "loadRequested=" + Clean(name) };
        }

        private static List<string> SaveTest(string argument)
        {
            string name = (argument ?? string.Empty).Trim();
            if (name.NullOrEmpty()) return new List<string> { "error=save name required" };
            MethodInfo method = AccessTools.Method(typeof(GameDataSaveLoader), "SaveGame", new[] { typeof(string) });
            if (method == null) return new List<string> { "error=SaveGame method unavailable" };
            method.Invoke(null, new object[] { name });
            return new List<string> { "saveRequested=" + Clean(name) };
        }

        private static List<string> KnowledgeState(Map map, string argument)
        {
            if (map == null) return NoMap();
            ParseKnowledgeArgument(argument, out int pawnId, out string cropName);
            Pawn pawn = ResolveKnowledgePawn(map, pawnId);
            if (pawn == null) return new List<string> { "error=no free colonist" };
            ThingDef crop = ResolveKnowledgeCrop(cropName);
            if (crop == null) return new List<string> { "error=no growable crop" };
            KnowledgeFacetSnapshotV2 personal = HorticultureKnowledgeAdapter.Facet(pawn, crop);
            KnowledgeFacetSnapshotV2 colony = HorticultureKnowledgeAdapter.Facet(null, crop, HorticultureKnowledgeAdapter.FacetIdentity,
                null, true);
            KnowledgeExpertiseSnapshotV2 expertise = KnowledgeQuery.Expertise(HorticultureKnowledgeAdapter.DomainId, pawn,
                HorticultureKnowledgeAdapter.ExpertiseTrack);
            IReadOnlyList<KnowledgeFacetSnapshotV2> personalRecords = KnowledgeQuery.PersonalFacets(HorticultureKnowledgeAdapter.DomainId, pawn);
            IReadOnlyList<KnowledgeFacetSnapshotV2> colonyRecords = KnowledgeQuery.ColonyFacets(HorticultureKnowledgeAdapter.DomainId);
            IReadOnlyList<KnowledgeValidationIssue> issues = KnowledgeRegistry.ValidationIssues;
            return new List<string>
            {
                "apiVersion=" + KnowledgeFrameworkApi.ApiVersion,
                "supportsDomains=" + KnowledgeFrameworkApi.Supports(1, KnowledgeFrameworkApi.DomainsCapability),
                "domain=" + HorticultureKnowledgeAdapter.DomainId,
                "subjects=" + KnowledgeRegistry.Subjects(HorticultureKnowledgeAdapter.DomainId).Count,
                "pawn=" + pawn.thingIDNumber + ":" + Clean(pawn.LabelShortCap),
                "crop=" + crop.defName,
                "personal=" + personal.amount.ToString("0.0") + " rank:" + HorticultureKnowledgeAdapter.TierFor(crop, pawn, false) + " sowing:" + personal.EventCount("sowing"),
                "colony=" + colony.amount.ToString("0.0") + " rank:" + HorticultureKnowledgeAdapter.TierFor(crop, null, true) + " sowing:" + colony.EventCount("sowing") + " pawnReference:" + (colony.pawn != null),
                "expertise=" + expertise.amount.ToString("0.0") + " rank:" + expertise.rank,
                "personalRecords=" + personalRecords.Count + " totalXp:" + personalRecords.Sum(record => record.amount).ToString("0.0"),
                "colonyRecords=" + colonyRecords.Count + " totalXp:" + colonyRecords.Sum(record => record.amount).ToString("0.0"),
                "validationIssues=" + issues.Count
            }.Concat(issues.Take(20).Select(issue => "issue=" + Clean(issue.ToString()))).ToList();
        }

        private static List<string> AwardPlantKnowledge(Map map, string argument)
        {
            if (map == null) return NoMap();
            ParseKnowledgeArgument(argument, out int pawnId, out string cropName);
            Pawn pawn = ResolveKnowledgePawn(map, pawnId);
            ThingDef crop = ResolveKnowledgeCrop(cropName);
            if (pawn == null || crop == null) return new List<string> { "error=colonist or crop unavailable" };
            float personalBefore = HorticultureKnowledgeAdapter.PersonalKnowledge(pawn, crop);
            float colonyBefore = HorticultureKnowledgeAdapter.ColonyKnowledge(crop);
            float expertiseBefore = KnowledgeQuery.Expertise(HorticultureKnowledgeAdapter.DomainId, pawn,
                HorticultureKnowledgeAdapter.ExpertiseTrack).amount;
            HorticultureKnowledgeAdapter.Observe(pawn, crop, HorticultureKnowledgeEvent.Sowing, map,
                sourceInstanceId: "bridge-sowing:" + pawn.thingIDNumber + ":" + crop.defName + ":" + (Find.TickManager?.TicksGame ?? 0));
            return new List<string>
            {
                "pawn=" + pawn.thingIDNumber + ":" + Clean(pawn.LabelShortCap),
                "crop=" + crop.defName,
                "personal=" + personalBefore.ToString("0.0") + "->" + HorticultureKnowledgeAdapter.PersonalKnowledge(pawn, crop).ToString("0.0"),
                "colony=" + colonyBefore.ToString("0.0") + "->" + HorticultureKnowledgeAdapter.ColonyKnowledge(crop).ToString("0.0"),
                "expertise=" + expertiseBefore.ToString("0.0") + "->" + KnowledgeQuery.Expertise(HorticultureKnowledgeAdapter.DomainId, pawn,
                    HorticultureKnowledgeAdapter.ExpertiseTrack).amount.ToString("0.0"),
                "sowingEvents=" + HorticultureKnowledgeAdapter.Facet(pawn, crop, HorticultureKnowledgeAdapter.FacetSowing).EventCount("sowing")
            };
        }

        private static void ParseKnowledgeArgument(string argument, out int pawnId, out string cropName)
        {
            string[] parts = (argument ?? string.Empty).Split(new[] { '|' }, 2);
            pawnId = parts.Length > 1 && int.TryParse(parts[0].Trim(), out int parsed) ? parsed : 0;
            cropName = parts.Length > 1 ? parts[1].Trim() : parts[0].Trim();
        }

        private static Pawn ResolveKnowledgePawn(Map map, int pawnId)
        {
            IEnumerable<Pawn> colonists = map.mapPawns?.FreeColonistsSpawned ?? Enumerable.Empty<Pawn>();
            return (pawnId > 0 ? colonists.FirstOrDefault(pawn => pawn.thingIDNumber == pawnId) : null)
                ?? colonists.FirstOrDefault();
        }

        private static ThingDef ResolveKnowledgeCrop(string argument)
        {
            string defName = (argument ?? string.Empty).Trim();
            ThingDef requested = defName.NullOrEmpty() ? null : DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            if (requested != null && NovelSeedUtility.IsGrowableCrop(requested)) return requested;
            string knownSubject = KnowledgeQuery.ColonyFacets(HorticultureKnowledgeAdapter.DomainId)
                .Where(record => record.amount > 0f).OrderByDescending(record => record.amount)
                .Select(record => record.subjectId).FirstOrDefault();
            return DefDatabase<ThingDef>.GetNamedSilentFail(knownSubject)
                ?? DefDatabase<ThingDef>.GetNamedSilentFail("Plant_Rice")
                ?? DefDatabase<ThingDef>.AllDefsListForReading.FirstOrDefault(NovelSeedUtility.IsGrowableCrop);
        }

        private static List<string> PerfState(Map map)
        {
            if (map == null) return NoMap();
            Stopwatch watch = Stopwatch.StartNew();
            List<Plant> plants = map.listerThings.ThingsInGroup(ThingRequestGroup.Plant).OfType<Plant>().ToList();
            int novel = 0, selfSeeding = 0, customVisual = 0;
            foreach (Plant plant in plants)
            {
                CompPlantVariety comp = plant.TryGetComp<CompPlantVariety>();
                if (comp?.HasAnyTraits != true) continue;
                novel++;
                if (comp.HasSelfSeeding) selfSeeding++;
                PlantVisualParameters visual = NovelSeedUtility.ResolveVisualParameters(comp);
                if (!visual.IsDefault || NovelSeedUtility.HasPlantMaskVisual(comp)) customVisual++;
            }
            watch.Stop();
            return new List<string>
            {
                "tick=" + (Find.TickManager?.TicksGame ?? -1),
                "plants=" + plants.Count,
                "novelPlants=" + novel,
                "selfSeedingPlants=" + selfSeeding,
                "customVisualPlants=" + customVisual,
                "scanMs=" + watch.Elapsed.TotalMilliseconds.ToString("0.00")
            };
        }

        private static List<string> DisablePatches()
        {
            const string owner = "lan.horticulture.novelseeds";
            int before = Harmony.GetAllPatchedMethods().Count(method =>
            {
                HarmonyLib.Patches patches = Harmony.GetPatchInfo(method);
                return patches != null && patches.Owners.Contains(owner);
            });
            new Harmony(owner).UnpatchAll(owner);
            int after = Harmony.GetAllPatchedMethods().Count(method =>
            {
                HarmonyLib.Patches patches = Harmony.GetPatchInfo(method);
                return patches != null && patches.Owners.Contains(owner);
            });
            return new List<string> { "owner=" + owner, "patchedMethodsBefore=" + before, "patchedMethodsAfter=" + after };
        }

        private static List<string> PlantPatches()
        {
            var result = new List<string>();
            foreach (MethodBase method in Harmony.GetAllPatchedMethods()
                .Where(method => method?.DeclaringType != null && typeof(Plant).IsAssignableFrom(method.DeclaringType)
                    && (method.Name.IndexOf("Tick", StringComparison.OrdinalIgnoreCase) >= 0
                        || method.Name.IndexOf("Growth", StringComparison.OrdinalIgnoreCase) >= 0
                        || method.Name.IndexOf("Leafless", StringComparison.OrdinalIgnoreCase) >= 0))
                .OrderBy(method => method.Name))
            {
                HarmonyLib.Patches patches = Harmony.GetPatchInfo(method);
                result.Add("method=" + method.DeclaringType.FullName + "." + method.Name);
                foreach (Patch patch in patches.Prefixes.Concat(patches.Postfixes).Concat(patches.Transpilers).Concat(patches.Finalizers))
                    result.Add("patch=owner:" + patch.owner + " method:" + patch.PatchMethod?.DeclaringType?.FullName + "." + patch.PatchMethod?.Name);
            }
            result.Insert(0, "lines=" + result.Count);
            return result;
        }

        private static List<string> DisableGrowthOwner(string argument)
        {
            string owner = (argument ?? string.Empty).Trim();
            if (owner.NullOrEmpty()) return new List<string> { "error=owner required" };
            MethodBase method = AccessTools.PropertyGetter(typeof(Plant), nameof(Plant.GrowthRate));
            HarmonyLib.Patches before = Harmony.GetPatchInfo(method);
            int beforeCount = before?.Postfixes.Count(patch => patch.owner == owner) ?? 0;
            new Harmony("hns.quicktest.ab").Unpatch(method, HarmonyPatchType.Postfix, owner);
            HarmonyLib.Patches after = Harmony.GetPatchInfo(method);
            int afterCount = after?.Postfixes.Count(patch => patch.owner == owner) ?? 0;
            return new List<string>
            {
                "owner=" + Clean(owner),
                "growthPostfixesBefore=" + beforeCount,
                "growthPostfixesAfter=" + afterCount
            };
        }

        private static List<string> DpaOpen()
        {
            MainButtonDef def = DefDatabase<MainButtonDef>.GetNamedSilentFail("DubsOptimizer");
            if (def == null) return new List<string> { "error=DubsOptimizer main button missing" };
            Find.MainTabsRoot.SetCurrentTab(def, true);
            return new List<string> { "opened=True", "window=" + def.TabWindow?.GetType().FullName };
        }

        private static List<string> DpaApi()
        {
            Assembly assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(item => item.GetName().Name == "PerformanceAnalyzer");
            if (assembly == null) return new List<string> { "error=PerformanceAnalyzer assembly not loaded" };
            BindingFlags flags = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var result = new List<string> { "assembly=" + assembly.FullName };
            foreach (Type type in assembly.GetTypes().Where(type =>
                type.FullName == "Analyzer.Window_Analyzer"
                || type.FullName == "Analyzer.Profiling.ProfileController"
                || type.FullName == "Analyzer.Profiling.ProfileLog"
                || type.FullName == "Analyzer.Profiling.ProfilingManager"
                || type.FullName == "Analyzer.Profiling.Entry").OrderBy(type => type.FullName))
            {
                result.Add("type=" + type.FullName);
                foreach (FieldInfo field in type.GetFields(flags).Take(60))
                    result.Add("field=" + field.Name + " type:" + field.FieldType.FullName + " static:" + field.IsStatic);
                foreach (PropertyInfo property in type.GetProperties(flags).Take(40))
                    result.Add("property=" + property.Name + " type:" + property.PropertyType.FullName);
                foreach (MethodInfo method in type.GetMethods(flags).Where(method => method.DeclaringType == type)
                    .Where(method => method.Name.IndexOf("Profile", StringComparison.OrdinalIgnoreCase) >= 0
                        || method.Name.IndexOf("Entry", StringComparison.OrdinalIgnoreCase) >= 0
                        || method.Name.IndexOf("Log", StringComparison.OrdinalIgnoreCase) >= 0
                        || method.Name.IndexOf("Start", StringComparison.OrdinalIgnoreCase) >= 0
                        || method.Name.IndexOf("Stop", StringComparison.OrdinalIgnoreCase) >= 0).Take(60))
                    result.Add("method=" + method.Name + " returns:" + method.ReturnType.FullName);
            }
            return result;
        }

        private static List<string> DpaEntries()
        {
            Type entryType = AccessTools.TypeByName("Analyzer.Profiling.Entry");
            IList initialEntries = AccessTools.Field(entryType, "entries")?.GetValue(null) as IList;
            if (initialEntries != null && initialEntries.Count <= 1)
                InitializeDpaEntries();
            IList entries = AccessTools.Field(entryType, "entries")?.GetValue(null) as IList;
            if (entries == null) return new List<string> { "error=DPA entries unavailable" };
            FieldInfo nameField = AccessTools.Field(entryType, "name");
            FieldInfo categoryField = AccessTools.Field(entryType, "category");
            FieldInfo activeField = AccessTools.Field(entryType, "isActive");
            FieldInfo loadingField = AccessTools.Field(entryType, "isLoading");
            FieldInfo patchedField = AccessTools.Field(entryType, "isPatched");
            var result = new List<string> { "entries=" + entries.Count };
            for (int index = 0; index < entries.Count; index++)
            {
                object entry = entries[index];
                result.Add("entry=index:" + index
                    + " name:" + Clean(nameField?.GetValue(entry)?.ToString())
                    + " category:" + Clean(categoryField?.GetValue(entry)?.ToString())
                    + " active:" + (activeField?.GetValue(entry) ?? false)
                    + " loading:" + (loadingField?.GetValue(entry) ?? false)
                    + " patched:" + (patchedField?.GetValue(entry) ?? false));
            }
            return result;
        }

        private static List<string> DpaStart(string argument)
        {
            Type entryType = AccessTools.TypeByName("Analyzer.Profiling.Entry");
            IList initialEntries = AccessTools.Field(entryType, "entries")?.GetValue(null) as IList;
            if (initialEntries != null && initialEntries.Count <= 1)
                InitializeDpaEntries();
            IList entries = AccessTools.Field(entryType, "entries")?.GetValue(null) as IList;
            FieldInfo nameField = AccessTools.Field(entryType, "name");
            Type guiControllerType = AccessTools.TypeByName("Analyzer.Profiling.GUIController");
            MethodInfo swapToEntry = AccessTools.Method(guiControllerType, "SwapToEntry", new[] { typeof(string) });
            if (entries == null || nameField == null || swapToEntry == null)
                return new List<string> { "error=DPA start API unavailable" };

            string requested = (argument ?? string.Empty).Trim();
            object selected = null;
            if (int.TryParse(requested, out int index) && index >= 0 && index < entries.Count)
                selected = entries[index];
            if (selected == null)
                selected = entries.Cast<object>().FirstOrDefault(entry =>
                    string.Equals(nameField.GetValue(entry)?.ToString(), requested, StringComparison.OrdinalIgnoreCase));
            if (selected == null)
                selected = entries.Cast<object>().FirstOrDefault(entry =>
                    (nameField.GetValue(entry)?.ToString() ?? string.Empty).IndexOf(requested, StringComparison.OrdinalIgnoreCase) >= 0);
            if (selected == null) return new List<string> { "error=DPA entry not found", "requested=" + Clean(requested) };

            Type analyzerType = AccessTools.TypeByName("Analyzer.Profiling.Analyzer");
            AccessTools.Method(analyzerType, "RefreshLogCount")?.Invoke(null, null);
            object profiles = AccessTools.Field(AccessTools.TypeByName("Analyzer.Profiling.ProfileController"), "profiles")?.GetValue(null);
            profiles?.GetType().GetMethod("Clear")?.Invoke(profiles, null);
            Type settingsType = AccessTools.TypeByName("Analyzer.Settings");
            AccessTools.Field(settingsType, "disableThreadedPatching")?.SetValue(null, true);
            AccessTools.Field(settingsType, "disableCleanup")?.SetValue(null, true);
            AccessTools.Field(settingsType, "verboseLogging")?.SetValue(null, true);
            Type componentType = AccessTools.TypeByName("Analyzer.GameComponent_Analyzer");
            object component = Current.Game?.GetComponent(componentType);
            AccessTools.Field(componentType, "TimeTillCleanup")?.SetValue(component, -1f);
            EnsureDpaRuntimeHooks();
            AccessTools.Method(analyzerType, "BeginProfiling")?.Invoke(null, null);
            Type selectedType = AccessTools.Field(entryType, "type")?.GetValue(selected) as Type;
            AccessTools.Method(AccessTools.TypeByName("Analyzer.Profiling.MethodTransplanting"), "PatchMethods")
                ?.Invoke(null, new object[] { selectedType });
            AccessTools.Field(entryType, "isPatched")?.SetValue(selected, true);
            swapToEntry.Invoke(null, new[] { nameField.GetValue(selected)?.ToString() });
            MethodBase thingDoTick = AccessTools.Method(typeof(Thing), "DoTick");
            string owners = string.Join(",", Harmony.GetPatchInfo(thingDoTick)?.Owners?.ToArray() ?? new string[0]);
            return new List<string>
            {
                "started=" + Clean(nameField.GetValue(selected)?.ToString()),
                "category=" + Clean(AccessTools.Field(entryType, "category")?.GetValue(selected)?.ToString()),
                "thingDoTickOwners=" + Clean(owners)
            };
        }

        private static List<string> DpaSample(string argument)
        {
            int limit = 40;
            if (int.TryParse((argument ?? string.Empty).Trim(), out int parsed)) limit = Math.Max(1, Math.Min(200, parsed));
            Type analyzerType = AccessTools.TypeByName("Analyzer.Profiling.Analyzer");
            IList logs = AccessTools.Field(analyzerType, "logs")?.GetValue(null) as IList;
            if (logs == null) return new List<string> { "error=DPA logs unavailable" };

            Type logType = AccessTools.TypeByName("Analyzer.Profiling.ProfileLog");
            FieldInfo average = AccessTools.Field(logType, "average");
            FieldInfo total = AccessTools.Field(logType, "total");
            FieldInfo calls = AccessTools.Field(logType, "calls");
            FieldInfo max = AccessTools.Field(logType, "max");
            FieldInfo label = AccessTools.Field(logType, "label");
            FieldInfo meth = AccessTools.Field(logType, "meth");
            var result = new List<string> { "logs=" + logs.Count };
            foreach (object log in logs.Cast<object>().OrderByDescending(item => Convert.ToDouble(average?.GetValue(item) ?? 0d)).Take(limit))
            {
                MethodBase method = meth?.GetValue(log) as MethodBase;
                result.Add("profile=average:" + Convert.ToDouble(average?.GetValue(log) ?? 0d).ToString("0.0000")
                    + " total:" + Convert.ToDouble(total?.GetValue(log) ?? 0d).ToString("0.0000")
                    + " calls:" + Convert.ToDouble(calls?.GetValue(log) ?? 0d).ToString("0.0")
                    + " max:" + Convert.ToDouble(max?.GetValue(log) ?? 0d).ToString("0.0000")
                    + " label:" + Clean(label?.GetValue(log)?.ToString())
                    + " method:" + Clean(method == null ? null : method.DeclaringType?.FullName + "." + method.Name));
            }
            return result;
        }

        private static void EnsureDpaRuntimeHooks()
        {
            Type modbaseType = AccessTools.TypeByName("Analyzer.Modbase");
            FieldInfo patchedField = AccessTools.Field(modbaseType, "isPatched");
            if (patchedField?.GetValue(null) is bool patched && patched) return;
            Harmony harmony = AccessTools.Field(modbaseType, "harmony")?.GetValue(null) as Harmony;
            if (harmony == null) throw new InvalidOperationException("DPA Harmony instance unavailable");
            Type rootHook = AccessTools.TypeByName("Analyzer.Profiling.H_RootUpdate");
            Type tickHook = AccessTools.TypeByName("Analyzer.Profiling.H_DoSingleTickUpdate");
            harmony.Patch(AccessTools.Method(typeof(Root_Play), "Update"),
                new HarmonyMethod(AccessTools.Method(rootHook, "Prefix")), new HarmonyMethod(AccessTools.Method(rootHook, "Postfix")));
            harmony.Patch(AccessTools.Method(typeof(TickManager), "DoSingleTick"),
                new HarmonyMethod(AccessTools.Method(tickHook, "Prefix")), new HarmonyMethod(AccessTools.Method(tickHook, "Postfix")));
            patchedField?.SetValue(null, true);
        }

        private static void InitializeDpaEntries()
        {
            Type guiControllerType = AccessTools.TypeByName("Analyzer.Profiling.GUIController");
            FieldInfo tabsField = AccessTools.Field(guiControllerType, "tabs");
            if (tabsField?.GetValue(null) == null)
                AccessTools.Method(guiControllerType, "InitialiseTabs")?.Invoke(null, null);
            AccessTools.Method(AccessTools.TypeByName("Analyzer.Window_Analyzer"), "LoadEntries")?.Invoke(null, null);
            EnsureDpaEntry("Single Tick", "Analyzer.Profiling.H_DoSingleTick");
            EnsureDpaEntry("Tick Things", "Analyzer.Profiling.H_TickListTick");
        }

        private static void EnsureDpaEntry(string name, string profilerTypeName)
        {
            Type entryType = AccessTools.TypeByName("Analyzer.Profiling.Entry");
            IList entries = AccessTools.Field(entryType, "entries")?.GetValue(null) as IList;
            FieldInfo nameField = AccessTools.Field(entryType, "name");
            if (entries == null || entries.Cast<object>().Any(entry =>
                string.Equals(nameField?.GetValue(entry)?.ToString(), name, StringComparison.OrdinalIgnoreCase))) return;
            Type categoryType = AccessTools.TypeByName("Analyzer.Profiling.Category");
            object tick = Enum.Parse(categoryType, "Tick");
            MethodInfo create = AccessTools.Method(entryType, "Create",
                new[] { typeof(string), categoryType, typeof(Type), typeof(bool), typeof(bool) });
            create?.Invoke(null, new object[] { name, tick, AccessTools.TypeByName(profilerTypeName), false, true });
        }

        private static List<string> AdapterStatus() => new List<string>
        {
            "adapter=HorticultureNovelSeeds",
            "version=1.8.4",
            "mode=diagnostics+ui",
            "commands=47"
        };

        private static List<VarietyRecord> AllVarieties() =>
            GameComponent_NovelSeeds.Instance?.AllVarieties?.Where(item => item != null).ToList() ?? new List<VarietyRecord>();

        private static List<PlantState> NovelPlants(Map map) =>
            map?.listerThings?.ThingsInGroup(ThingRequestGroup.Plant).OfType<Plant>()
                .Select(item => new PlantState(item, item.TryGetComp<CompPlantVariety>()))
                .Where(item => item.Comp != null && item.Comp.HasAnyTraits).ToList() ?? new List<PlantState>();

        private static int LineageDepth(VarietyRecord variety) => LineageDepth(variety, new HashSet<string>());

        private static int LineageDepth(VarietyRecord variety, HashSet<string> path)
        {
            if (variety?.parentVarietyIds == null || variety.parentVarietyIds.Count == 0 || !path.Add(variety.id)) return 0;
            int depth = 1 + variety.parentVarietyIds.Select(id => LineageDepth(GameComponent_NovelSeeds.Instance?.GetVariety(id), path)).DefaultIfEmpty(0).Max();
            path.Remove(variety.id);
            return depth;
        }

        private static string TraitSummary(IEnumerable<VarietyTraitDef> traits) =>
            string.Join(",", traits?.Where(item => item != null).Select(item => item.label).ToArray() ?? new string[0]);

        private static int Value(Dictionary<string, int> values, string key) => values.TryGetValue(key, out int value) ? value : 0;
        private static string Clean(string value) => (value ?? "none").Replace('\r', ' ').Replace('\n', ' ').Replace('|', '/');
        private static List<string> NoMap() => new List<string> { "error=no active map" };

        private sealed class PlantState
        {
            public readonly Plant Plant;
            public readonly CompPlantVariety Comp;
            public PlantState(Plant plant, CompPlantVariety comp) { Plant = plant; Comp = comp; }
        }
    }
}
