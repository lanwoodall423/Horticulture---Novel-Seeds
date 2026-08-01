using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace HorticultureNovelSeeds
{
    [HarmonyPatch]
    public static class GrowerSow_VarietySkill_Patch
    {
        private static readonly FieldInfo SowMinSkillField = AccessTools.Field(typeof(PlantProperties), nameof(PlantProperties.sowMinSkill));
        private static readonly MethodInfo EffectiveSowMinSkillMethod = AccessTools.Method(typeof(GrowerSow_VarietySkill_Patch), nameof(EffectiveSowMinSkill));

        public static IEnumerable<MethodBase> TargetMethods()
        {
            Type[] types =
            {
                typeof(WorkGiver_GrowerSow),
                AccessTools.TypeByName("VanillaPlantsExpandedMorePlants.WorkGiver_GrowerSowAquatic"),
                AccessTools.TypeByName("VanillaPlantsExpandedMorePlants.WorkGiver_GrowerSowSandy")
            };
            foreach (Type type in types.Where(t => t != null))
            {
                MethodInfo method = AccessTools.DeclaredMethod(type, "JobOnCell", new[] { typeof(Pawn), typeof(IntVec3), typeof(bool) });
                if (method != null) yield return method;
            }
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            foreach (CodeInstruction instruction in instructions)
            {
                yield return instruction;
                if (instruction.opcode == OpCodes.Ldfld && Equals(instruction.operand, SowMinSkillField))
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_1);
                    yield return new CodeInstruction(OpCodes.Ldarg_2);
                    yield return new CodeInstruction(OpCodes.Call, EffectiveSowMinSkillMethod);
                }
            }
        }

        public static int EffectiveSowMinSkill(int baseSkill, Pawn pawn, IntVec3 cell)
        {
            IPlantToGrowSettable grower = pawn?.Map == null ? null : GridsUtility.GetPlantToGrowSettable(cell, pawn.Map);
            ThingDef plantDef = grower?.GetPlantDefToGrow();
            VarietyRecord variety = GameComponent_NovelSeeds.Instance?.VarietyForSowing(grower, cell);
            if (plantDef?.plant == null || variety?.cropDef != plantDef) return baseSkill;
            return Mathf.Max(0, baseSkill + ExpandedTraitUtility.SowSkillOffset(variety.traits));
        }
    }

    [HarmonyPatch]
    public static class GrowerSow_VarietyZoneGate_Patch
    {
        public static IEnumerable<MethodBase> TargetMethods()
        {
            Type[] types =
            {
                typeof(WorkGiver_GrowerSow),
                AccessTools.TypeByName("VanillaPlantsExpandedMorePlants.WorkGiver_GrowerSowAquatic"),
                AccessTools.TypeByName("VanillaPlantsExpandedMorePlants.WorkGiver_GrowerSowSandy")
            };
            foreach (Type type in types.Where(t => t != null))
            {
                MethodInfo method = AccessTools.DeclaredMethod(type, "JobOnCell", new[] { typeof(Pawn), typeof(IntVec3), typeof(bool) });
                if (method != null) yield return method;
            }
        }

        [HarmonyPriority(Priority.First)]
        public static bool Prefix(Pawn pawn, IntVec3 c, ref Job __result)
        {
            IPlantToGrowSettable grower = pawn?.Map == null ? null : GridsUtility.GetPlantToGrowSettable(c, pawn.Map);
            ThingDef plantDef = grower?.GetPlantDefToGrow();
            VarietyRecord variety = GameComponent_NovelSeeds.Instance?.VarietyForSowing(grower, c);
            if (variety?.cropDef != plantDef) return true;
            string requiredTag = ExpandedTraitUtility.RequiredSowTag(variety.traits);
            if (requiredTag.NullOrEmpty() || ExpandedTraitUtility.GrowerMatchesTag(grower, requiredTag)) return true;
            __result = null;
            return false;
        }
    }

    [HarmonyPatch(typeof(Plant), nameof(Plant.LeaflessNow), MethodType.Getter)]
    public static class Plant_PerennialLeafless_Patch
    {
        public static void Postfix(Plant __instance, ref bool __result)
        {
            if (!__result || __instance.Map == null) return;
            CompPlantVariety comp = __instance.TryGetComp<CompPlantVariety>();
            if (comp?.HasPerennialDormancy != true) return;
            if (GenTemperature.TryGetTemperatureForCell(__instance.Position, __instance.Map, out float temperature) && temperature < __instance.def.plant.minOptimalGrowthTemperature)
                __result = false;
        }
    }

}
