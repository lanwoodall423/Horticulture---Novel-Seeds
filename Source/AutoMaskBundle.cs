using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Verse;

namespace HorticultureNovelSeeds
{
    public sealed class AutoMaskIdentity
    {
        public string SourcePackageId;
        public string SourceModName;
        public string PlantDefName;
        public int VariationIndex;
        public string TexturePath;
        public string TextureContentHash;
        public int TextureWidth;
        public int TextureHeight;
        public string TextureKey;
        public string GraphicIdentity;
        public string GrowthState;
        public string DirectionIdentity;
        public string VariationIdentity;
        public string ProduceSignature;
        public string EligibilityKey;
        public string MorphologyIdentity;
        public int FormatVersion;
        public int GeneratorVersion;

        public string StableKey => PlantDefName + "|" + GrowthState + "|" + DirectionIdentity + "|" + VariationIdentity;

        public AutoMaskIdentity Clone()
        {
            return (AutoMaskIdentity)MemberwiseClone();
        }
    }

    public sealed class AutoPlantMaskBundleFile : IExposable
    {
        private int formatVersion = PlantAutoMaskCache.FormatVersion;
        private int generatorVersion = PlantAutoMaskCache.GeneratorVersion;
        private string sourcePackageId;
        private string sourceModName;
        private string bundleId;
        private List<AutoPlantMaskRecord> masks = new List<AutoPlantMaskRecord>();

        public int LoadedFormatVersion => formatVersion;
        public int LoadedGeneratorVersion => generatorVersion;
        public string SourcePackageId => sourcePackageId;
        public string SourceModName => sourceModName;
        public string BundleId => bundleId;
        public List<AutoPlantMaskRecord> Masks => masks ?? (masks = new List<AutoPlantMaskRecord>());

        public void SetMetadata(string packageId, string modName, string id)
        {
            sourcePackageId = packageId;
            sourceModName = modName;
            bundleId = id;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref formatVersion, "formatVersion", PlantAutoMaskCache.FormatVersion, true);
            Scribe_Values.Look(ref generatorVersion, "generatorVersion", PlantAutoMaskCache.GeneratorVersion, true);
            Scribe_Values.Look(ref sourcePackageId, "sourcePackageId");
            Scribe_Values.Look(ref sourceModName, "sourceModName");
            Scribe_Values.Look(ref bundleId, "bundleId");
            Scribe_Collections.Look(ref masks, "masks", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (masks == null) masks = new List<AutoPlantMaskRecord>();
                masks.RemoveAll(mask => mask == null || mask.PlantDefName.NullOrEmpty());
                foreach (AutoPlantMaskRecord mask in masks) mask.Normalize();
            }
        }
    }

    public sealed class AutoMaskBundleValidationResult
    {
        public bool Valid;
        public string Error;
        public int RecordCount;
        public int LowConfidenceCount;
        public int FailureCount;
        public string BundleId;
        public readonly List<string> FailureDetails = new List<string>();

        public override string ToString()
        {
            return (Valid ? "valid" : "invalid") + "; records=" + RecordCount
                + "; lowConfidence=" + LowConfidenceCount + "; failures=" + FailureCount
                + (FailureDetails.Count == 0 ? string.Empty : "; details="
                    + string.Join(" | ", FailureDetails.Take(8).ToArray()))
                + (Error.NullOrEmpty() ? string.Empty : "; error=" + Error);
        }
    }
}
