using System.Linq;
using Verse;

namespace HorticultureNovelSeeds
{
    /// <summary>
    /// Compatibility surface for integrations that referenced the old settings UI type.
    /// Production settings rendering is owned by <see cref="InsightSettingsDocument"/> and
    /// is created by the mod instance, so navigation and selection never live here.
    /// </summary>
    public static class NovelSeedsSettingsUI
    {
        // Kept for the existing runtime discovery contract. The production document owns the
        // actual disclosure state and does not read this compatibility field.
#pragma warning disable CS0414
        private static bool showAdvancedGeneralSettings = false;
#pragma warning restore CS0414

        public static ThingDef CurrentPlantPreview => DefDatabase<ThingDef>.AllDefsListForReading
            .Where(NovelSeedUtility.IsGrowableCrop)
            .OrderBy(plant => plant.label)
            .FirstOrDefault();

        /// <summary>One-frame compatibility entry point for older callers.</summary>
        public static void DoWindowContents(UnityEngine.Rect inRect, NovelSeedsSettings settings)
        {
            InsightSettingsDocument document = new InsightSettingsDocument(settings);
            document.Draw(inRect);
            document.PostClose();
        }
    }
}
