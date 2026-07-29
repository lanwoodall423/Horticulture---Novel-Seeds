using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace HorticultureNovelSeeds
{
    public static class HorticultureBridgeAdapter
    {
        public static string[] BridgeCommandSpecs() => new[]
        {
            "HORTICULTURE|R|Compact Novel Seeds colony summary",
            "HNS_VARIETIES|R|List unlocked varieties and lineage depth",
            "HNS_PLANTS|R|Aggregate live plants by crop and variety state",
            "HNS_PENDING|R|List plants carrying unsaved discoveries",
            "HNS_TRAITS|R|Aggregate unlocked and planted trait frequency",
            "HNS_GROWERS|R|Inspect grower types, commands, and compatible varieties",
            "HNS_PATCHES|R|Inspect live plant-menu Harmony prefix order",
            "HNS_OPEN_GROWER_MENU|W|Open Nice Plants Menu for a grower zone ID",
            "HNS_NICE_STATE|R|Inspect the open Nice Plants Menu records",
            "HNS_OPPORTUNITIES|R|Analyze live horticulture for feature opportunities",
            "HNS_SETTINGS|R|Report active mutation settings",
            "HNS_BALANCE_TEST|R|Simulate trait generation and compare balance modes",
            "HNS_REGISTRY|R|Inspect cultivar registry and breeding programs",
            "HNS_OPEN_REGISTRY|W|Open the Cultivar Registry main tab",
            "HNS_WILDLIFE_LAYOUT|R|Inspect Wildlife tab layout members for integration",
            "HNS_VALIDATE|R|Run read-only Novel Seeds state invariants",
            "HNS_ADAPTER_STATUS|R|Report Novel Seeds adapter identity"
        };

        public static string BridgeAdapterInfo() =>
            "HorticultureNovelSeeds|1.7.0|Added Wildlife tab layout diagnostics for registry integration.";

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
                case "HNS_OPEN_GROWER_MENU": return OpenGrowerMenu(map, argument);
                case "HNS_NICE_STATE": return NiceState();
                case "HNS_OPPORTUNITIES": return Opportunities(map);
                case "HNS_SETTINGS": return Settings();
                case "HNS_BALANCE_TEST": return BalanceTest(argument);
                case "HNS_REGISTRY": return Registry();
                case "HNS_OPEN_REGISTRY": return OpenRegistry();
                case "HNS_WILDLIFE_LAYOUT": return WildlifeLayout();
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

        private static List<string> OpenGrowerMenu(Map map, string argument)
        {
            if (map == null) return NoMap();
            if (!int.TryParse(argument, out int zoneId)) return new List<string> { "error=invalid zone ID" };
            Zone zone = map.zoneManager?.AllZones?.FirstOrDefault(item => item.ID == zoneId);
            if (!(zone is IPlantToGrowSettable grower)) return new List<string> { "error=grower zone not found", "zoneId=" + zoneId };
            Type dialogType = AccessTools.TypeByName("NicePlantsMenu.Dialog_PlantBrowser");
            FieldInfo plantZones = dialogType == null ? null : AccessTools.Field(dialogType, "plantZones");
            if (dialogType == null || plantZones == null) return new List<string> { "error=Nice Plants Menu unavailable" };
            Window dialog = Activator.CreateInstance(dialogType) as Window;
            if (dialog == null) return new List<string> { "error=dialog creation failed" };
            plantZones.SetValue(dialog, new List<IPlantToGrowSettable> { grower });
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
                "filtered=" + filteredNames.Count + " carrot:" + filteredNames.Contains("VCE_Carrot")
            };
        }

        private static List<string> Settings()
        {
            NovelSeedsSettings settings = HorticultureNovelSeedsMod.Settings;
            if (settings == null) return new List<string> { "settings=unavailable" };
            return new List<string>
            {
                "mutation=global:" + settings.globalMutationChance.ToStringPercent() + " wild:" + settings.wildMutationChance.ToStringPercent(),
                "cross=global:" + settings.globalCrossPollinationChance.ToStringPercent() + " maxDonorTraits:" + settings.MaxCrossPollinationTraits,
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
            List<BreedingProgramRecord> programs = component.BreedingPrograms.Where(program => program != null).ToList();
            var result = new List<string>
            {
                "registry=varieties:" + varieties.Count + " favorites:" + varieties.Count(variety => variety.registryFavorite)
                    + " archived:" + varieties.Count(variety => variety.registryArchived),
                "programs=total:" + programs.Count + " active:" + programs.Count(program => program.active)
                    + " fulfilled:" + programs.Count(program => varieties.Any(program.Matches))
            };
            foreach (BreedingProgramRecord program in programs.Take(30))
            {
                VarietyRecord best = component.CandidateVarieties(program).FirstOrDefault();
                result.Add("program=id:" + program.id + " name:" + Clean(program.name) + " crop:" + program.cropDef?.defName
                    + " active:" + program.active + " goals:" + program.desiredTraitRootDefNames.Count
                    + " matched:" + varieties.Count(program.Matches)
                    + " best:" + Clean(best?.Label) + " progress:" + (best == null ? 0 : program.MatchCount(best)) + "/" + program.desiredTraitRootDefNames.Count);
            }
            return result;
        }

        private static List<string> OpenRegistry()
        {
            MainButtonDef def = DefDatabase<MainButtonDef>.GetNamedSilentFail("HNS_CultivarRegistry");
            if (def == null) return new List<string> { "error=registry main button def missing" };
            Find.MainTabsRoot.SetCurrentTab(def, true);
            return new List<string> { "opened=True", "tab=" + def.defName, "window=" + def.TabWindow?.GetType().FullName };
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
            return new List<string> { "valid=" + (issues.Count == 0), "issues=" + issues.Count }.Concat(issues.Take(50)).ToList();
        }

        private static List<string> AdapterStatus() => new List<string>
        {
            "adapter=HorticultureNovelSeeds",
            "version=1.7.0",
            "mode=diagnostics+ui",
            "commands=17"
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
