using System;

namespace HorticultureNovelSeeds
{
    public enum WindHabitKind { None, RigidHabit, SuppleHabit, Flutterleaf, NoddingHabit, SweepingHabit }
    internal static class WindHabitMath
    {
        internal static bool Valid(WindHabitKind habit) => habit>=WindHabitKind.RigidHabit && habit<=WindHabitKind.SweepingHabit;
        // X/Z amplitude, phase-rate multiplier and secondary flutter amplitude.
        internal static (float X,float Z,float Rate,float Flutter) Controls(WindHabitKind habit) => habit switch
        {
            WindHabitKind.RigidHabit => (.12f,.12f,1f,0f),
            WindHabitKind.SuppleHabit => (1.85f,1.65f,.78f,.08f),
            WindHabitKind.Flutterleaf => (.88f,.80f,1.20f,.42f),
            WindHabitKind.NoddingHabit => (.45f,1.85f,.83f,.08f),
            WindHabitKind.SweepingHabit => (2.15f,.50f,.68f,.10f),
            _ => (1f,1f,1f,0f)
        };
    }
}
