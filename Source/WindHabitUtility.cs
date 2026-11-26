using System;
using UnityEngine;
using Verse;
namespace HorticultureNovelSeeds
{
    internal static class WindHabitUtility
    {
        internal static bool Enabled { get; set; }
        internal static WindHabitKind Resolve(CompPlantVariety comp)
        {
            if(!Enabled || comp?.ActiveTraits==null) return WindHabitKind.None;
            VarietyTraitDef chosen=null;
            foreach(var t in comp.ActiveTraits)
                if(t!=null && !t.produceOnlyVisual && WindHabitMath.Valid(t.windHabit) &&
                    (chosen==null || string.CompareOrdinal(t.defName,chosen.defName)<0)) chosen=t;
            return chosen?.windHabit??WindHabitKind.None;
        }
        internal static Vector4 Controls(WindHabitKind habit)
        {
            var c=WindHabitMath.Controls(habit);
            return new Vector4(c.X,c.Z,c.Rate,c.Flutter);
        }
    }
}
