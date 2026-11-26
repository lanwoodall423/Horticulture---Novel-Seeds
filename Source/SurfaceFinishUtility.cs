using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace HorticultureNovelSeeds
{
    internal static class SurfaceFinishUtility
    {
        internal static bool Enabled { get; set; }
        private static readonly Dictionary<Material,bool> materials=new();
        private static Vector4 last;
        private static Map lastMap;
        internal static int MaterialCount=>materials.Count;
        internal static SurfaceFinishKind Resolve(CompPlantVariety comp)
        {
            if(!Enabled||comp?.ActiveTraits==null)return SurfaceFinishKind.None;
            VarietyTraitDef chosen=null;
            foreach(var t in comp.ActiveTraits)
                if(t!=null&&!t.produceOnlyVisual&&SurfaceFinishMath.Valid(t.surfaceFinish)&&
                    (chosen==null||string.CompareOrdinal(t.defName,chosen.defName)<0))chosen=t;
            return chosen?.surfaceFinish??SurfaceFinishKind.None;
        }
        internal static Vector4 Current(Map map)
        {
            if(map==null)return new Vector4(0f,1f,1f,0f);
            var e=SurfaceFinishMath.Environment(GenLocalDate.DayPercent(map),map.skyManager.CurSkyGlow,map.weatherManager.RainRate);
            return new Vector4(e.X,e.Y,e.Light,e.Rain);
        }
        internal static void Register(Material mat,bool sheltered)
        {
            materials.Add(mat,sheltered);
            var v=Current(Find.CurrentMap);if(sheltered)v.w=0f;
            mat.SetVector("_HnsEnvironment",v);
        }
        internal static void Refresh(Map map)
        {
            if(!Enabled||map==null||map!=Find.CurrentMap||materials.Count==0)return;
            var v=Current(map);
            if(map==lastMap&&v==last)return;
            lastMap=map;last=v;
            foreach(var pair in materials)
            {
                var env=v;if(pair.Value)env.w=0f;
                pair.Key.SetVector("_HnsEnvironment",env);
            }
        }
    }
    [HarmonyPatch(typeof(SkyManager),nameof(SkyManager.SkyManagerUpdate))]
    internal static class SurfaceFinishSkyPatch
    {
        internal static void Postfix(SkyManager __instance)
        {
            if(!BotanicalExpressionEngine.Enabled&&!SurfaceFinishUtility.Enabled&&!BotanicalDetailExperiment.Enabled&&!ResponsiveHabitExperiment.Enabled)return;
            var map=Find.CurrentMap;
            if(map?.skyManager==__instance){SurfaceFinishUtility.Refresh(map);BotanicalDetailExperiment.Refresh(map);VascularExperiment.Refresh(map);LifeSuiteExperiment.Refresh(map);BotanicalExpressionEngine.Refresh(map);ResponsiveHabitExperiment.Refresh(map);}
        }
    }
}
