using System;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace HorticultureNovelSeeds
{
    // Optional integration; no AssemblyRef to Progression: Agriculture.
    internal static class CropProgressionCompatibility
    {
        private const string PackageId = "ferny.progressionagriculture";
        private static bool resolved;
        private static PropertyInfo instance;
        private static MethodInfo isUnlocked;
        private static MethodInfo unlock;
        private static bool warned;

        private static bool Active => ModsConfig.IsActive(PackageId);
        private static void Resolve()
        {
            if (resolved) return;
            resolved = true;
            var type = AccessTools.TypeByName("ProgressionAgriculture.GameComponent_UnlockedCrops");
            if (type == null) return;
            instance = AccessTools.Property(type, "Instance");
            isUnlocked = AccessTools.Method(type, "IsCropUnlocked", new[] { typeof(ThingDef) });
            unlock = AccessTools.Method(type, "UnlockCrop", new[] { typeof(ThingDef) });
        }

        // Null means unknown/incompatible, not a grant of sowing permission.
        internal static bool? IsAvailable(ThingDef crop)
        {
            if (crop == null) return false;
            if (!Active) return null; // ordinary RimWorld sowing rules still own availability
            try
            {
                Resolve();
                var registry = instance?.GetValue(null);
                if (registry != null && isUnlocked?.Invoke(registry, new object[] { crop }) is bool available) return available;
            }
            catch (Exception e) { Warn(e.Message); return null; }
            Warn("Crop availability API is unavailable.");
            return null;
        }

        internal static void NotifyUnlocked(ThingDef crop)
        {
            if (crop == null || !Active) return;
            try
            {
                Resolve();
                var registry = instance?.GetValue(null);
                if (registry == null || isUnlocked == null || unlock == null) { Warn("Crop unlock API is unavailable."); return; }
                if (isUnlocked.Invoke(registry, new object[] { crop }) is bool available && !available)
                    unlock.Invoke(registry, new object[] { crop });
            }
            catch (Exception e) { Warn(e.Message); }
        }

        private static void Warn(string detail)
        {
            if (warned) return;
            warned = true;
            Log.Warning("[Horticulture] Optional Progression: Agriculture integration unavailable: " + detail);
        }
    }
}
