using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace HorticultureNovelSeeds
{
    // Target the real plant-work completion closure. Its first GenPlace call is the
    // primary harvested product; additional-yield calls must not inflate that count.
    // Fail closed if the closure or placement signature changes.
    [HarmonyPatch]
    internal static class HarvestJournalPatch
    {
        private static readonly MethodInfo Place = AccessTools.Method(typeof(GenPlace), nameof(GenPlace.TryPlaceThing),
            new[] { typeof(Thing), typeof(IntVec3), typeof(Map), typeof(ThingPlaceMode), typeof(Action<Thing, int>),
                typeof(Predicate<IntVec3>), typeof(Rot4?), typeof(int) });
        private static readonly MethodInfo Collected = AccessTools.Method(typeof(Plant), nameof(Plant.PlantCollected));
        private static readonly MethodInfo Yield = AccessTools.Method(typeof(Plant), nameof(Plant.YieldNow));
        private static readonly MethodInfo RecordPlace = AccessTools.Method(typeof(HarvestJournalPatch), nameof(PlaceAndRecord));
        private static FieldInfo driverField;
        private static MethodBase target;
        static bool Prepare()
        {
            if (Place != null && Collected != null && Yield != null)
            {
                var matches = new List<(MethodBase, FieldInfo)>();
                foreach (var type in typeof(JobDriver_PlantWork).GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic))
                {
                    var field = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                        .FirstOrDefault(f => typeof(JobDriver_PlantWork).IsAssignableFrom(f.FieldType));
                    if (field == null) continue;
                    foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    {
                        if (method.GetMethodBody() == null) continue;
                        var il = PatchProcessor.GetOriginalInstructions(method);
                        if (il.Any(i => i.Calls(Collected)) && il.Any(i => i.Calls(Yield)) && il.Any(i => i.Calls(Place)))
                            matches.Add((method, field));
                    }
                }
                if (matches.Count == 1) { target = matches[0].Item1; driverField = matches[0].Item2; return true; }
            }
            Log.Warning("[Horticulture] Harvest journal hook not qualified for this engine; no measured yield will be recorded.");
            return false;
        }
        static MethodBase TargetMethod() => target;
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.ToList();
            int index = code.FindIndex(i => i.Calls(Place));
            if (index < 0) return code;
            // The original arguments remain on the stack; append the closure's driver.
            var load = new CodeInstruction(OpCodes.Ldarg_0);
            load.labels.AddRange(code[index].labels);
            code[index].labels.Clear();
            load.blocks.AddRange(code[index].blocks);
            code[index].blocks.Clear();
            code[index].opcode = OpCodes.Call;
            code[index].operand = RecordPlace;
            code.Insert(index, load);
            return code;
        }
        private static bool PlaceAndRecord(Thing thing, IntVec3 center, Map map, ThingPlaceMode mode,
            Action<Thing, int> placedAction, Predicate<IntVec3> extraValidator, Rot4? rot, int squareRadius, object closure)
        {
            // Preserve the vanilla call and capture the requested count before stacking destroys the source.
            int count = thing?.stackCount ?? 0;
            bool result = GenPlace.TryPlaceThing(thing, center, map, mode, placedAction, extraValidator, rot, squareRadius);
            if (!result || count <= 0) return result;
            try
            {
                var driver = driverField?.GetValue(closure) as JobDriver_PlantWork;
                var plant = driver?.job?.GetTarget(TargetIndex.A).Thing as Plant;
                if (plant == null || thing.def != plant.def.plant.harvestedThingDef) return result;
                int cycle = plant.TryGetComp<CompPlantVariety>()?.HarvestCycle ?? 0;
                // PlantCollected increments the persisted cycle after this output is placed.
                string identity = HorticultureKnowledgeEventIdentity.Harvest(plant, cycle < int.MaxValue ? cycle + 1 : cycle, true, false, false);
                HorticultureEventRouter.HarvestOutputPlaced(driver.pawn, plant, count, identity);
            }
            catch (Exception e) { Log.WarningOnce("[Horticulture] Harvest journal capture failed: " + e.Message, 174329019); }
            return result;
        }
    }
}
