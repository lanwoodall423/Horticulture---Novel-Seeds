#if HNS_NATIVE_FIXTURE
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Globalization;
using RimWorld;
using LudeonTK;
using UnityEngine;
using Verse;

namespace HorticultureNovelSeeds
{
    // A controlled visual test of real registered cultivar trait sets, not a
    // synthetic color override. Deliberately samples extreme colors independent
    // of ordinary mutation weights, compatibility tags and species palettes.
    internal static class NativeGardenProbe
    {
        private static readonly string[][] SpeciesOptions =
        {
            new[] {"Plant_Rice","Plant_Corn","Plant_Potato"},
            new[] {"Plant_Strawberry","Plant_Cotton","Plant_Healroot"},
            new[] {"Plant_Hops","Plant_Smokeleaf","Plant_Psychoid"},
            new[] {"Plant_Devilstrand","Plant_TreeOak","Plant_TreeCocoa"}
        };
        private static readonly string[] ExtraSpecies = {"Plant_TreePine","Plant_TreeBirch","Plant_TreePoplar","Plant_Haygrass","Plant_Rose","Plant_Daylily"};
        private static readonly Color[] Pigments =
        {
            new Color(.98f,.14f,.36f), new Color(.20f,.38f,.96f), new Color(.95f,.65f,.10f),
            new Color(.12f,.88f,.73f), new Color(.83f,.14f,.94f), new Color(.97f,.30f,.10f)
        };
        private static readonly List<Plant> current = new();
        private static readonly List<Row> inventory = new();
        private static readonly HashSet<int> custom = new(), legacy = new(), vanilla = new();
        private static readonly HashSet<ThingDef> chosen = new();
        private static readonly Dictionary<int, HashSet<string>> maskSources = new();
        internal static void RecordMaskSource(Plant plant, int variation)
        {
            if (!active || plant == null) return;
            if (!maskSources.TryGetValue(plant.thingIDNumber, out var sources)) maskSources.Add(plant.thingIDNumber, sources = new());
            var layers = PlantMaskUtility.LayersForVariation(plant.def, variation, false);
            sources.Add(variation + ":" + (layers == null ? "none" : PlantAutoMaskCache.RecordSource(plant.def, variation, false)));
        }
        private static Map map;
        private static IntVec3 start;
        private static string root;
        private static Action<string> onStage;
        private static Action<string,string> finish;
        private static int panel, frames, captureAge;
        private static string capture;
        private static bool active;
        private sealed record Row(int Panel, int RowNumber, int Column, Plant Plant, VarietyRecord Cultivar);
        internal static void RecordPrint(Plant plant, bool customPrint)
        {
            if (!active) return;
            if (customPrint) custom.Add(plant.thingIDNumber);
            else vanilla.Add(plant.thingIDNumber);
        }
        internal static void RecordLegacy(Plant plant)
        {
            if (active) legacy.Add(plant.thingIDNumber);
        }

        internal static void Begin(string path, Action<string> stage, Action<string,string> completed)
        {
            root = path; onStage = stage; finish = completed;
            try
            {
                map = Find.CurrentMap;
                Require(map != null, "current map");
                WaylithPhenotypeShader.Enabled = true;
                // Noon in local solar time, not merely the global time of day.
                int advance = Mathf.RoundToInt(Mathf.Repeat(.5f - GenLocalDate.DayPercent(map), 1f) * 60000f);
                Find.TickManager.DebugSetTicksGame(Find.TickManager.TicksGame + advance);
                map.weatherManager.TransitionTo(WeatherDefOf.Clear);
                map.weatherManager.lastWeather = WeatherDefOf.Clear;
                map.weatherManager.curWeatherAge = 4000;
                Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
                var origin = map.mapPawns.FreeColonistsSpawned.First().Position;
                // Reuse one deliberately cleared, unroofed and obstacle-free test
                // bed. Remove only vegetation in this disposable map.
                var candidates = GenRadial.RadialCellsAround(origin, 70, true)
                    .Where(c => Cells(c).All(x => x.InBounds(map)) && PlantCells(c).All(ClearCell))
                    .Take(120).Select(c => (Cell:c, Score:Cells(c).Count(ClearCell)))
                    .OrderByDescending(c => c.Score).ToArray();
                Require(candidates.Length > 0, "a visible unroofed plot with 18 clear plant positions");
                start = candidates[0].Cell;
                Log.Message("[HNS Garden] plot clear cells=" + candidates[0].Score);
                foreach (var cell in Cells(start).Where(ClearCell))
                {
                    cell.GetPlant(map)?.Destroy(DestroyMode.Vanish);
                    map.terrainGrid.SetTerrain(cell, TerrainDefOf.Soil);
                }
                Find.WindowStack.TryRemove(typeof(EditWindow_Log), false);
                Find.Selector.ClearSelection();
                Find.CameraDriver.JumpToCurrentMapLoc(start + new IntVec3(8,0,4));
                Find.CameraDriver.SetRootSize(8.4f);
                active = true;
                MakePanel();
                onStage("GardenSetup");
            }
            catch (Exception e) { End("FAIL", e.ToString()); }
        }

        private static bool ClearCell(IntVec3 x) => x.InBounds(map) && x.Standable(map) && x.GetEdifice(map) == null && !x.Fogged(map) && x.GetZone(map) == null && map.roofGrid.RoofAt(x) == null;

        private static IEnumerable<IntVec3> PlantCells(IntVec3 c)
        {
            for(int z = 0; z < 3; z++)
                for(int x = 0; x < 6; x++)
                    yield return c + new IntVec3(x * 3, 0, z * 4);
        }

        private static IEnumerable<IntVec3> Cells(IntVec3 c)
        {
            for (int z = -3; z <= 12; z++)
                for (int x = -3; x <= 18; x++)
                    yield return c + new IntVec3(x,0,z);
        }

        private static ThingDef Choose(string name)
        {
            foreach (string candidate in new[] {name}.Concat(ExtraSpecies))
            {
                var def = DefDatabase<ThingDef>.GetNamedSilentFail(candidate);
                if (HorticulturePlantPolicy.IsSupported(def) && chosen.Add(def)) return def;
            }
            throw new InvalidOperationException("No supported unique species for " + name);
        }
        private static VarietyTraitDef Trait(string name) => DefDatabase<VarietyTraitDef>.GetNamed("HNS_" + name);
        private static VarietyTraitDef Pigment(string family, int color) =>
            ColorTraitFactory.TraitForColor(family, Pigments[(color + Pigments.Length) % Pigments.Length]);

        private static List<VarietyTraitDef> Traits(int row, int col)
        {
            // Deliberate extremes of real trait definitions; no changes to trait
            // defs/settings and no claims about natural mutation eligibility.
            return col switch
            {
                1 => new() { Trait(row % 3 == 0 ? "Humongous" : "Giant"), Trait("HighYield"), Trait("Robust") },
                2 => new() { Trait("Dwarf"), Trait("LowYield"), Trait(row % 2 == 0 ? "VeryDelicate" : "Delicate") },
                3 => new() { Pigment("ProduceColor", row), Trait("DiseaseResistant") },
                4 => new() { Pigment("LeafColor", row + 3), Trait("Giant"), Trait("ColdResistant") },
                5 => new() { Pigment("ProduceColor", row + 2), Pigment("LeafColor", row + 4),
                    Pigment("StemColor", row + 1), Trait(row % 2 == 0 ? "Beautiful" : "Ugly"),
                    Trait(row % 2 == 0 ? "HeatResistant" : "DiseaseVulnerable") },
                _ => new()
            };
        }
        private static void MakePanel()
        {
            frames = 0; captureAge = 0; capture = null;
            var registry = GameComponent_NovelSeeds.Instance;
            Require(registry != null, "registry");
            for (int r = 0; r < 3; r++)
            {
                var def = Choose(SpeciesOptions[panel][r]);
                // Prewarm actual resolved masks before rendering. If shared
                // manual or seasonal materials cannot use the experimental
                // backend, their existing CPU path is intentionally retained.
                for (int v = 0; v < PlantMaskUtility.VariationCount(def); v++)
                    WaylithPhenotypeShader.PrepareMask(def, v);
                for (int c = 0; c < 6; c++)
                {
                    var plant = (Plant)ThingMaker.MakeThing(def);
                    var comp = plant.TryGetComp<CompPlantVariety>();
                    Require(comp != null, "plant genetics component " + def.defName);
                    VarietyRecord cultivar = null;
                    if (c != 0)
                    {
                        var traits = Traits(panel * 3 + r, c);
                        Require(traits.All(t => t != null) && traits.Count > 0, "valid actual traits");
                        cultivar = registry.UnlockVariety(def, traits, "Garden " + def.label + " " + c, originKind:"debug-visual-fixture");
                        Require(cultivar != null && cultivar.traits.Count == traits.Count, "registered cultivar");
                        comp.SetVariety(cultivar);
                        Require(comp.Variety == cultivar && comp.ActiveTraits.Count == traits.Count, "assigned real genetics");
                    }
                    else comp.SetVariety(null);
                    plant.sown = true; plant.Growth = 1f;
                    plant.HitPoints = plant.MaxHitPoints;
                    // Row A on top, columns left-to-right.
                    GenSpawn.Spawn(plant, start + new IntVec3(c * 3,0,(2-r)*4), map);
                    Require(plant.Spawned, "spawned garden plant");
                    current.Add(plant); inventory.Add(new Row(panel + 1,r + 1,c + 1,plant,cultivar));
                    plant.DirtyMapMesh(map);
                }
            }
            Log.Message("[HNS Garden] panel=" + (panel+1) + "; species=" + string.Join(", ",current.Select(p=>p.def.defName).Distinct()));
        }

        internal static void Update()
        {
            if (!active) return;
            try
            {
                frames++;
                if (Find.WindowStack.TryRemove(typeof(EditWindow_Log), false)) { frames = 0; return; }
                if (capture == null && frames >= 100 && current.All(p => custom.Contains(p.thingIDNumber) || vanilla.Contains(p.thingIDNumber)))
                {
                    capture = Path.Combine(root, "HNS-garden-panel-" + (panel+1) + ".png");
                    Log.Message("[HNS Garden] screenshot " + (panel+1) + "; local hour=" + GenLocalDate.HourOfDay(map) +
                        "; sky glow=" + map.skyManager.CurSkyGlow + "; shader materials=" + WaylithPhenotypeShader.MaterialCount +
                        "; prepared masks=" + WaylithPhenotypeShader.MaskCount);
                    ScreenCapture.CaptureScreenshot(capture);
                }
                if (capture != null && File.Exists(capture) && new FileInfo(capture).Length > 1000 && ++captureAge >= 30)
                {
                    onStage("GardenPanel" + (panel+1));
                    if (++panel == SpeciesOptions.Length)
                    {
                        WriteInventory();
                        onStage("GardenInventory");
                        End("PASS", "72 garden plants, 60 registered cultivars, 12 species, four daylight screenshots");
                        return;
                    }
                    foreach (var plant in current) if (plant.Spawned) plant.Destroy(DestroyMode.Vanish);
                    current.Clear();
                    MakePanel();
                    return;
                }
                if (frames > 1200) throw new InvalidOperationException("garden print/capture timeout " + (panel+1));
            }
            catch (Exception e) { End("FAIL", e.ToString()); }
        }

        private static string Backend(Plant p)
        {
            int id = p.thingIDNumber;
            bool gpu = WaylithPhenotypeShader.PrintedPlants.Contains(id), cpu = legacy.Contains(id);
            return gpu ? (cpu ? "Mixed GPU/legacy" : WaylithPhenotypeShader.MaskedPlants.Contains(id) ? "GPU masked" : "GPU whole") :
                cpu ? "Legacy material" : custom.Contains(id) ? "Geometry/effects only" : "Vanilla";
        }
        private static void WriteInventory()
        {
            var lines = new List<string> { "Panel\tRow\tColumn\tSpecies\tCultivar\tTrait IDs\tRenderer\tAutomatic mask sources" };
            foreach (var entry in inventory)
                lines.Add(string.Join("\t", entry.Panel, entry.RowNumber, entry.Column, entry.Plant.def.defName,
                    entry.Cultivar?.customName ?? "Vanilla control", string.Join(", ",entry.Cultivar?.traits.Select(t=>t.defName) ?? Enumerable.Empty<string>()), Backend(entry.Plant), maskSources.TryGetValue(entry.Plant.thingIDNumber, out var sources) ? string.Join(", ", sources.OrderBy(x => x)) : "not styled"));
            File.WriteAllLines(Path.Combine(root,"HNS-garden-inventory.tsv"),lines);
            Require(inventory.Count == 72 && inventory.Count(r=>r.Cultivar != null)==60 && chosen.Count == 12,"complete garden inventory");
            Require(inventory.Where(r=>r.Cultivar != null).All(r=>maskSources.TryGetValue(r.Plant.thingIDNumber, out var sources) && sources.All(x=>x.EndsWith(":local") || x.EndsWith(":bundled") || x.EndsWith(":none"))), "automatic-only provenance for all cultivars");
            Log.Message("[HNS Garden] renderer counts: " + string.Join("; ",inventory.GroupBy(r=>Backend(r.Plant)).Select(g=>g.Key+"="+g.Count())));
        }
        private static void Require(bool value, string label) { if (!value) throw new InvalidOperationException(label); }
        private static void End(string status, string message)
        {
            active = false;
            WaylithPhenotypeShader.Enabled = false;
            if (Find.TickManager != null) Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
            finish(status,message);
        }
    }
}
#endif
