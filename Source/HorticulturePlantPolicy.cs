using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace HorticultureNovelSeeds
{
    /// <summary>Canonical eligibility policy for Novel Seeds plant behavior and knowledge.</summary>
    public static class HorticulturePlantPolicy
    {
        private const string VanillaFlowersExpandedPackageId = "VanillaExpanded.VPEFlowers";
        private static readonly HashSet<string> SupersededByFlowersExpanded = new HashSet<string>
        {
            "Plant_Rose",
            "Plant_Daylily",
            "VCE_Hyacinth",
            "VCE_Lavender",
            "VCE_Lily",
            "VCE_Plumeria",
            "VCE_Tulip"
        };
        private static bool? vanillaFlowersExpandedActive;

        /// <summary>
        /// Returns true for ordinary sowable plants, including sowable trees. Non-sowable
        /// decorative and wild-only plants are outside the Novel Seeds contract.
        /// </summary>
        public static bool IsSupported(ThingDef plantDef)
        {
            return plantDef?.plant != null
                && plantDef.plant.Sowable
                && !(VanillaFlowersExpandedActive && SupersededByFlowersExpanded.Contains(plantDef.defName));
        }

        public static bool IsSowableTree(ThingDef plantDef) => IsSupported(plantDef) && IsTree(plantDef);

        public static bool IsTree(ThingDef plantDef) => plantDef?.plant != null &&
            (plantDef.plant.IsTree || plantDef.plant.forceIsTree || plantDef.plant.treeCategory != TreeCategory.None);

        public static bool VanillaFlowersExpandedActive => vanillaFlowersExpandedActive ??
            (vanillaFlowersExpandedActive = ModsConfig.IsActive(VanillaFlowersExpandedPackageId)
                || ModsConfig.IsActive(VanillaFlowersExpandedPackageId.ToLowerInvariant())).Value;

        public static string RejectionReason(ThingDef plantDef)
        {
            if (plantDef?.plant == null) return "missing plant properties";
            if (!plantDef.plant.Sowable) return "not sowable";
            if (VanillaFlowersExpandedActive && SupersededByFlowersExpanded.Contains(plantDef.defName))
                return "superseded by Vanilla Expanded Plants; Flowers";
            return string.Empty;
        }
    }
}
