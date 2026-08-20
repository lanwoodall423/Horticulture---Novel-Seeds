using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace HorticultureNovelSeeds
{
    public sealed class AutoPlantMaskRecord : IExposable
    {
        private int formatVersion = PlantAutoMaskCache.FormatVersion;
        private int generatorVersion = PlantAutoMaskCache.GeneratorVersion;
        private string sourcePackageId;
        private string sourceModName;
        private string plantDefName;
        private int variationIndex;
        private string texturePath;
        private string textureContentHash;
        private int textureWidth;
        private int textureHeight;
        private string textureKey;
        private string graphicIdentity;
        private string growthState;
        private string directionIdentity;
        private string variationIdentity;
        private string produceSignature;
        private string eligibilityKey;
        private string morphologyIdentity;
        private float confidence;
        private bool lowConfidence;
        private List<VisualMaskLayerRecord> layers = new List<VisualMaskLayerRecord>();

        public int FormatVersion => formatVersion;
        public int GeneratorVersion => generatorVersion;
        public string SourcePackageId => sourcePackageId;
        public string SourceModName => sourceModName;
        public string PlantDefName => plantDefName;
        public int VariationIndex => variationIndex;
        public string TexturePath => texturePath;
        public string TextureContentHash => textureContentHash;
        public int TextureWidth => textureWidth;
        public int TextureHeight => textureHeight;
        public string TextureKey => textureKey;
        public string GraphicIdentity => graphicIdentity;
        public string GrowthState => growthState;
        public string DirectionIdentity => directionIdentity;
        public string VariationIdentity => variationIdentity;
        public string ProduceSignature => produceSignature;
        public string EligibilityKey => eligibilityKey;
        public string MorphologyIdentity => morphologyIdentity;
        public float Confidence => confidence;
        public bool LowConfidence => lowConfidence;
        public IReadOnlyList<VisualMaskLayerRecord> Layers => layers;

        public AutoPlantMaskRecord() { }

        internal AutoPlantMaskRecord(string plantDefName, int variationIndex, string textureKey,
            float confidence, IEnumerable<VisualMaskLayerRecord> layers, string eligibilityKey = null)
        {
            this.plantDefName = plantDefName;
            this.variationIndex = variationIndex;
            this.textureKey = textureKey;
            this.eligibilityKey = eligibilityKey;
            this.confidence = Mathf.Clamp01(confidence);
            lowConfidence = this.confidence < PlantAutoMaskCache.LowConfidenceThreshold;
            this.layers = layers?.Select(layer => layer.Clone()).ToList() ?? new List<VisualMaskLayerRecord>();
            Normalize();
        }

        internal AutoPlantMaskRecord(AutoMaskIdentity identity, float confidence,
            IEnumerable<VisualMaskLayerRecord> layers)
        {
            ApplyIdentity(identity);
            this.confidence = Mathf.Clamp01(confidence);
            lowConfidence = this.confidence < PlantAutoMaskCache.LowConfidenceThreshold;
            this.layers = layers?.Select(layer => layer.Clone()).ToList() ?? new List<VisualMaskLayerRecord>();
            Normalize();
        }

        internal AutoPlantMaskRecord CloneWithIdentity(AutoMaskIdentity identity)
        {
            return new AutoPlantMaskRecord(identity, confidence, layers);
        }

        internal void ApplyIdentity(AutoMaskIdentity identity)
        {
            if (identity == null) return;
            formatVersion = identity.FormatVersion;
            generatorVersion = identity.GeneratorVersion;
            sourcePackageId = identity.SourcePackageId;
            sourceModName = identity.SourceModName;
            plantDefName = identity.PlantDefName;
            variationIndex = identity.VariationIndex;
            texturePath = identity.TexturePath;
            textureContentHash = identity.TextureContentHash;
            textureWidth = identity.TextureWidth;
            textureHeight = identity.TextureHeight;
            textureKey = identity.TextureKey;
            graphicIdentity = identity.GraphicIdentity;
            growthState = identity.GrowthState;
            directionIdentity = identity.DirectionIdentity;
            variationIdentity = identity.VariationIdentity;
            produceSignature = identity.ProduceSignature;
            eligibilityKey = identity.EligibilityKey;
            morphologyIdentity = identity.MorphologyIdentity;
            variationIndex = Math.Max(0, variationIndex);
        }

        internal bool Matches(AutoMaskIdentity identity)
        {
            return identity != null && formatVersion == PlantAutoMaskCache.FormatVersion
                && generatorVersion == PlantAutoMaskCache.GeneratorVersion
                && sourcePackageId == identity.SourcePackageId && sourceModName == identity.SourceModName
                && plantDefName == identity.PlantDefName && variationIndex == identity.VariationIndex
                && texturePath == identity.TexturePath
                && textureContentHash == identity.TextureContentHash && textureWidth == identity.TextureWidth
                && textureHeight == identity.TextureHeight && textureKey == identity.TextureKey
                && graphicIdentity == identity.GraphicIdentity && growthState == identity.GrowthState
                && directionIdentity == identity.DirectionIdentity && variationIdentity == identity.VariationIdentity
                && produceSignature == identity.ProduceSignature && eligibilityKey == identity.EligibilityKey
                && morphologyIdentity == identity.MorphologyIdentity;
        }

        internal bool CanReuseFor(AutoMaskIdentity identity)
        {
            return identity != null && formatVersion == PlantAutoMaskCache.FormatVersion
                && generatorVersion == PlantAutoMaskCache.GeneratorVersion
                && sourcePackageId == identity.SourcePackageId && sourceModName == identity.SourceModName
                && variationIndex == identity.VariationIndex && texturePath == identity.TexturePath
                && textureContentHash == identity.TextureContentHash
                && textureWidth == identity.TextureWidth && textureHeight == identity.TextureHeight
                && textureKey == identity.TextureKey && graphicIdentity == identity.GraphicIdentity
                && growthState == identity.GrowthState && directionIdentity == identity.DirectionIdentity
                && produceSignature == identity.ProduceSignature && eligibilityKey == identity.EligibilityKey
                && morphologyIdentity == identity.MorphologyIdentity;
        }

        internal string MismatchReason(AutoMaskIdentity identity)
        {
            if (identity == null) return "current identity is unavailable";
            List<string> mismatches = new List<string>();
            Action<bool, string> check = (matches, name) => { if (!matches) mismatches.Add(name); };
            check(formatVersion == PlantAutoMaskCache.FormatVersion, "formatVersion");
            check(generatorVersion == PlantAutoMaskCache.GeneratorVersion, "generatorVersion");
            check(sourcePackageId == identity.SourcePackageId, "sourcePackageId");
            check(sourceModName == identity.SourceModName, "sourceModName");
            check(plantDefName == identity.PlantDefName, "plantDefName");
            check(variationIndex == identity.VariationIndex, "variationIndex");
            check(texturePath == identity.TexturePath, "texturePath");
            check(textureContentHash == identity.TextureContentHash, "textureContentHash");
            check(textureWidth == identity.TextureWidth, "textureWidth");
            check(textureHeight == identity.TextureHeight, "textureHeight");
            check(textureKey == identity.TextureKey, "textureKey");
            check(graphicIdentity == identity.GraphicIdentity, "graphicIdentity");
            check(growthState == identity.GrowthState, "growthState");
            check(directionIdentity == identity.DirectionIdentity, "directionIdentity");
            check(variationIdentity == identity.VariationIdentity, "variationIdentity");
            check(produceSignature == identity.ProduceSignature, "produceSignature");
            check(eligibilityKey == identity.EligibilityKey, "eligibilityKey");
            check(morphologyIdentity == identity.MorphologyIdentity, "morphologyIdentity");
            return mismatches.Count == 0 ? "unknown mismatch" : string.Join(",", mismatches.ToArray());
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref formatVersion, "formatVersion", PlantAutoMaskCache.FormatVersion, true);
            Scribe_Values.Look(ref generatorVersion, "generatorVersion", PlantAutoMaskCache.GeneratorVersion, true);
            Scribe_Values.Look(ref sourcePackageId, "sourcePackageId");
            Scribe_Values.Look(ref sourceModName, "sourceModName");
            Scribe_Values.Look(ref plantDefName, "plantDef");
            Scribe_Values.Look(ref variationIndex, "variationIndex", 0);
            Scribe_Values.Look(ref texturePath, "texturePath");
            Scribe_Values.Look(ref textureContentHash, "textureContentHash");
            Scribe_Values.Look(ref textureWidth, "textureWidth", 0, true);
            Scribe_Values.Look(ref textureHeight, "textureHeight", 0, true);
            Scribe_Values.Look(ref textureKey, "textureKey");
            Scribe_Values.Look(ref graphicIdentity, "graphicIdentity");
            Scribe_Values.Look(ref growthState, "growthState");
            Scribe_Values.Look(ref directionIdentity, "directionIdentity");
            Scribe_Values.Look(ref variationIdentity, "variationIdentity");
            Scribe_Values.Look(ref produceSignature, "produceSignature");
            Scribe_Values.Look(ref eligibilityKey, "eligibilityKey");
            Scribe_Values.Look(ref morphologyIdentity, "morphologyIdentity");
            Scribe_Values.Look(ref confidence, "confidence", 0f);
            Scribe_Values.Look(ref lowConfidence, "lowConfidence", true);
            Scribe_Collections.Look(ref layers, "layers", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit) Normalize();
        }

        internal void Normalize()
        {
            variationIndex = Mathf.Max(0, variationIndex);
            confidence = Mathf.Clamp01(confidence);
            formatVersion = formatVersion <= 0 ? PlantAutoMaskCache.FormatVersion : formatVersion;
            generatorVersion = generatorVersion <= 0 ? PlantAutoMaskCache.GeneratorVersion : generatorVersion;
            if (layers == null) layers = new List<VisualMaskLayerRecord>();
            layers.RemoveAll(layer => layer == null);
            while (layers.Count < 3) layers.Add(new VisualMaskLayerRecord());
            if (layers.Count > 3) layers.RemoveRange(3, layers.Count - 3);
            string[] names = { "Produce", "Leaves", "Stem" };
            for (int i = 0; i < 3; i++) { layers[i].name = names[i]; layers[i].Normalize(); }
        }
    }

    public sealed class AutoPlantMaskCacheFile : IExposable
    {
        private int formatVersion = PlantAutoMaskCache.FormatVersion;
        private int generatorVersion = PlantAutoMaskCache.GeneratorVersion;
        private List<AutoPlantMaskRecord> masks = new List<AutoPlantMaskRecord>();

        public int LoadedFormatVersion => formatVersion;
        public int LoadedGeneratorVersion => generatorVersion;
        public List<AutoPlantMaskRecord> Masks => masks ?? (masks = new List<AutoPlantMaskRecord>());

        public void ExposeData()
        {
            Scribe_Values.Look(ref formatVersion, "formatVersion", PlantAutoMaskCache.FormatVersion, true);
            Scribe_Values.Look(ref generatorVersion, "generatorVersion", PlantAutoMaskCache.GeneratorVersion, true);
            Scribe_Collections.Look(ref masks, "masks", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (masks == null) masks = new List<AutoPlantMaskRecord>();
                masks.RemoveAll(mask => mask == null || mask.PlantDefName.NullOrEmpty());
                foreach (AutoPlantMaskRecord mask in masks) mask.Normalize();
            }
        }
    }

    public struct AutoMaskBatchResult
    {
        public int generated;
        public int reused;
        public int localReused;
        public int bundled;
        public int manualSkipped;
        public int lowConfidence;
        public int failed;
        public int workItems;
        public long elapsedMilliseconds;
        public string currentPlant;
        public bool queued;
    }

    public static class PlantAutoMaskCache
    {
        private sealed class SourceData
        {
            public int size;
            public Color32[] pixels;
            public float[] hue;
            public float[] saturation;
            public float[] brightness;
            public int[] cluster;
            public int[] region;
            public List<ColorCluster> clusters;
            public List<ConnectedRegion> regions;
        }

        private sealed class ColorCluster
        {
            public float hue;
            public float saturation;
            public float brightness;
            public int count;
            public float separation;
        }

        private sealed class ConnectedRegion
        {
            public int cluster;
            public int count;
            public int minX = int.MaxValue;
            public int minY = int.MaxValue;
            public int maxX;
            public int maxY;
            public float centerX;
            public float centerY;
            public float Thickness;
            public float Branch;
            public float Compactness;
            public float Verticality;
        }

        private struct ProduceSignature
        {
            public bool HasValue;
            public Color produceColor;
            public List<Color> palette;
            public string key;
        }

        private struct LayerEligibility
        {
            public bool produce;
            public bool leaves;
            public bool stem;
            public bool forceStem;
            public bool structuralOnly;
        }

        public const int FormatVersion = 2;
        public const int GeneratorVersion = 15;
        public const float LowConfidenceThreshold = 0.54f;
        private const byte TransparentAlpha = 4;
        private const int AnalysisSize = VisualMaskLayerRecord.Resolution;
        private static readonly Dictionary<string, AutoPlantMaskRecord> Records = new Dictionary<string, AutoPlantMaskRecord>();
        private static readonly Dictionary<string, AutoPlantMaskRecord> BundledRecords = new Dictionary<string, AutoPlantMaskRecord>();
        private static readonly HashSet<string> SessionValidated = new HashSet<string>();
        private static readonly HashSet<string> SessionBundled = new HashSet<string>();
        private static readonly HashSet<string> FailedKeys = new HashSet<string>();
        private static readonly Dictionary<string, AutoMaskIdentity> CurrentIdentities = new Dictionary<string, AutoMaskIdentity>();
        private static bool initialized;
        private static bool bundleInitialized;
        private static bool dirty;
        private static bool generationQueued;
        private static AutoMaskBatchResult lastBatchResult;

        public static string CachePath => Path.Combine(GenFilePaths.ConfigFolderPath, "HorticultureNovelSeedsAutoMasks.xml");
        public static string BundledCachePath => Path.Combine(HorticultureNovelSeedsMod.ContentRootPath ?? string.Empty,
            "1.6", "AutoMasks", "BundledAutoMasks.xml");
        public static string BundledManifestPath => Path.Combine(HorticultureNovelSeedsMod.ContentRootPath ?? string.Empty,
            "1.6", "AutoMasks", "BundledAutoMasks.manifest.json");
        public static bool GenerationQueued => generationQueued;
        public static AutoMaskBatchResult LastBatchResult => lastBatchResult;
        public static int LocalRecordCount => Records.Count;
        public static int BundledRecordCount => BundledRecords.Count;

        internal static IEnumerable<AutoPlantMaskRecord> LocalRecords => Records.Values;
        internal static IEnumerable<AutoPlantMaskRecord> BundledRecordsForExport => BundledRecords.Values;

        public static string RecordSource(ThingDef plantDef, int variationIndex)
        {
            AutoPlantMaskRecord record = GetRecord(plantDef, variationIndex, false, true);
            if (record == null) return "none";
            return IsBundledRecord(record) ? "bundled" : "local";
        }

        public static bool PromoteBundledRecord(ThingDef plantDef, int variationIndex, bool save)
        {
            EnsureLoaded();
            EnsureBundleLoaded();
            if (plantDef == null) return false;
            string key = RecordKey(plantDef.defName, variationIndex);
            if (!BundledRecords.TryGetValue(key, out AutoPlantMaskRecord bundled)) return false;
            Texture texture = PlantMaskUtility.TextureForVariation(plantDef, variationIndex);
            AutoMaskIdentity identity = texture == null ? null : CurrentIdentityFor(plantDef, variationIndex, texture);
            if (!bundled.Matches(identity)) return false;
            Records[key] = bundled.CloneWithIdentity(identity);
            SessionValidated.Add(key);
            dirty = true;
            if (save) SaveIfDirty();
            return true;
        }

        public static void InitializeAndGenerateMissing()
        {
            EnsureLoaded();
            EnsureBundleLoaded();
            AutoMaskBatchResult scan;
            List<AutoMaskWorkItem> work = BuildWorkList(false, out scan);
            scan.workItems = work.Count;
            if (work.Count == 0)
            {
                scan.queued = false;
                lastBatchResult = scan;
                generationQueued = false;
                Log.Message("[Horticulture - Novel Seeds] Auto masks are current; no generation long event was queued (bundle hits "
                    + scan.bundled + ", local hits " + scan.localReused + ").");
                return;
            }

            generationQueued = true;
            scan.queued = true;
            lastBatchResult = scan;
            LongEventHandler.QueueLongEvent(() =>
            {
                Stopwatch timer = Stopwatch.StartNew();
                AutoMaskBatchResult result = ExecuteWork(work, scan);
                result.elapsedMilliseconds = timer.ElapsedMilliseconds;
                result.queued = true;
                lastBatchResult = result;
                generationQueued = false;
                Log.Message("[Horticulture - Novel Seeds] Auto masks: generated " + result.generated + ", reused " + result.reused
                    + ", bundled " + result.bundled + ", manual " + result.manualSkipped + ", review " + result.lowConfidence
                    + ", failed " + result.failed + ".");
            }, "HNS_GeneratingAutoMasks", false, exception =>
            {
                generationQueued = false;
                Log.Error("[Horticulture - Novel Seeds] Automatic plant-mask generation failed: " + exception);
            }, true, false);
        }

        public static AutoMaskBatchResult GenerateMissing(bool regenerateAutomatic)
        {
            EnsureLoaded();
            EnsureBundleLoaded();
            AutoMaskBatchResult scan;
            List<AutoMaskWorkItem> work = BuildWorkList(regenerateAutomatic, out scan);
            scan.workItems = work.Count;
            Stopwatch timer = Stopwatch.StartNew();
            AutoMaskBatchResult result = ExecuteWork(work, scan);
            result.elapsedMilliseconds = timer.ElapsedMilliseconds;
            result.queued = false;
            lastBatchResult = result;
            return result;
        }

        private sealed class AutoMaskWorkItem
        {
            public ThingDef plant;
            public int variation;
        }

        private static List<AutoMaskWorkItem> BuildWorkList(bool regenerateAutomatic, out AutoMaskBatchResult result)
        {
            result = new AutoMaskBatchResult();
            List<AutoMaskWorkItem> work = new List<AutoMaskWorkItem>();
            IEnumerable<ThingDef> plants = DefDatabase<ThingDef>.AllDefsListForReading
                .Where(def => HorticulturePlantPolicy.IsSupported(def) && def.graphicData != null).OrderBy(def => def.defName);
            foreach (ThingDef plant in plants)
            {
                int count = PlantMaskUtility.VariationCount(plant);
                for (int variation = 0; variation < count; variation++)
                {
                    if (HorticultureNovelSeedsMod.Settings?.GetPlantSettings(plant, false)?.HasManualPlantMask(variation) == true)
                    {
                        result.manualSkipped++;
                        continue;
                    }
                    AutoPlantMaskRecord existing = regenerateAutomatic ? null : GetRecord(plant, variation, false, true);
                    if (existing != null)
                    {
                        result.reused++;
                        if (existing.LowConfidence) result.lowConfidence++;
                        if (IsBundledRecord(existing)) result.bundled++;
                        else result.localReused++;
                        continue;
                    }
                    work.Add(new AutoMaskWorkItem { plant = plant, variation = variation });
                }
            }
            return work;
        }

        private static AutoMaskBatchResult ExecuteWork(List<AutoMaskWorkItem> work, AutoMaskBatchResult result)
        {
            result.workItems = work.Count;
            for (int index = 0; index < work.Count; index++)
            {
                AutoMaskWorkItem item = work[index];
                result.currentPlant = item.plant.defName + " / " + PlantMaskUtility.VariationLabel(item.plant, item.variation);
                LongEventHandler.SetCurrentEventText("Horticulture - Novel Seeds: analyzing " + result.currentPlant
                    + " (" + (index + 1) + "/" + work.Count + ")");
                AutoPlantMaskRecord generated = Generate(item.plant, item.variation, false);
                if (generated == null) result.failed++;
                else
                {
                    result.generated++;
                    if (generated.LowConfidence) result.lowConfidence++;
                }
            }
            result.currentPlant = string.Empty;
            SaveIfDirty();
            return result;
        }

        public static AutoPlantMaskRecord GetRecord(ThingDef plantDef, int variationIndex, bool generateIfMissing = true,
            bool allowIdentityGeneration = false)
        {
            EnsureLoaded();
            EnsureBundleLoaded();
            if (plantDef == null || !HorticulturePlantPolicy.IsSupported(plantDef)) return null;
            string key = RecordKey(plantDef.defName, variationIndex);
            if (!allowIdentityGeneration)
            {
                if (SessionValidated.Contains(key) && Records.TryGetValue(key, out AutoPlantMaskRecord sessionRecord)) return sessionRecord;
                if (SessionBundled.Contains(key) && BundledRecords.TryGetValue(key, out AutoPlantMaskRecord bundledSessionRecord)) return bundledSessionRecord;
                return null;
            }
            Texture texture = PlantMaskUtility.TextureForVariation(plantDef, variationIndex);
            if (texture == null) return null;
            AutoMaskIdentity identity = CurrentIdentityFor(plantDef, variationIndex, texture);
            if (identity == null) return null;
            if (Records.TryGetValue(key, out AutoPlantMaskRecord record) && record.Matches(identity))
            {
                SessionValidated.Add(key);
                return record;
            }
            if (BundledRecords.TryGetValue(key, out AutoPlantMaskRecord bundledRecord) && bundledRecord.Matches(identity))
            {
                SessionBundled.Add(key);
                return bundledRecord;
            }
            AutoPlantMaskRecord reusable = Records.Values
                .Where(candidate => candidate != null && candidate.CanReuseFor(identity))
                .OrderBy(candidate => candidate.PlantDefName)
                .FirstOrDefault();
            if (reusable != null)
            {
                AutoPlantMaskRecord clone = reusable.CloneWithIdentity(identity);
                Records[key] = clone;
                SessionValidated.Add(key);
                dirty = true;
                return clone;
            }
            return generateIfMissing ? Generate(plantDef, variationIndex, true) : null;
        }

        public static List<VisualMaskLayerRecord> LayersFor(ThingDef plantDef, int variationIndex, bool generateIfMissing = true,
            bool allowIdentityGeneration = false)
        {
            AutoPlantMaskRecord record = GetRecord(plantDef, variationIndex, generateIfMissing, allowIdentityGeneration);
            return IsRenderable(record) ? record.Layers.Select(layer => layer).ToList() : null;
        }

        internal static bool IsRenderable(AutoPlantMaskRecord record)
        {
            return record != null && !record.LowConfidence && record.Layers.Any(layer => layer?.HasPixels == true);
        }

        public static AutoPlantMaskRecord Generate(ThingDef plantDef, int variationIndex, bool save)
        {
            EnsureLoaded();
            EnsureBundleLoaded();
            Texture texture = PlantMaskUtility.TextureForVariation(plantDef, variationIndex);
            if (plantDef == null || !HorticulturePlantPolicy.IsSupported(plantDef) || texture == null) return null;
            ProduceSignature produce = ProduceColorFor(plantDef);
            LayerEligibility eligibility = EligibilityFor(plantDef, variationIndex, texture, produce);
            string key = RecordKey(plantDef.defName, variationIndex);
            FailedKeys.Remove(key);
            try
            {
                Color32[] immatureReference = ReadPixels(PlantMaskUtility.ReferenceTextureForVariation(plantDef,
                    variationIndex, "Immature"), AnalysisSize);
                string variationLabel = PlantMaskUtility.VariationLabel(plantDef, variationIndex) ?? string.Empty;
                string structuralState = ContainsIgnoreCase(variationLabel, "Immature") ? "Leafless Immature" : "Leafless";
                Texture structuralTexture = PlantMaskUtility.ReferenceTextureForVariation(plantDef, variationIndex, structuralState)
                    ?? PlantMaskUtility.ReferenceTextureForVariation(plantDef, variationIndex, "Leafless");
                Color32[] leaflessReference = ReadPixels(structuralTexture, AnalysisSize);
                if (leaflessReference != null && HasTreeMorphology(plantDef)) eligibility.forceStem = true;
                AutoMaskIdentity identity = BuildIdentity(plantDef, variationIndex, texture, produce, eligibility);
                if (identity == null) return Failed(key);
                SourceData source = AnalyzeTexture(texture);
                if (source == null) return Failed(key);
                List<VisualMaskLayerRecord> layers = Classify(source, produce, eligibility, immatureReference,
                    leaflessReference, out float confidence);
                AutoPlantMaskRecord record = new AutoPlantMaskRecord(identity, confidence, layers);
                CurrentIdentities[key] = identity;
                Records[key] = record;
                SessionValidated.Add(key);
                dirty = true;
                if (save) SaveIfDirty();
                return record;
            }
            catch (Exception exception)
            {
                Log.Warning("[Horticulture - Novel Seeds] Could not generate an automatic plant mask for "
                    + plantDef.defName + " variation " + variationIndex + ": " + exception.Message);
                return Failed(key);
            }
        }

        public static void SaveIfDirty()
        {
            if (!dirty) return;
            string temporary = CachePath + ".tmp";
            try
            {
                AutoPlantMaskCacheFile file = new AutoPlantMaskCacheFile();
                file.Masks.AddRange(Records.Values.OrderBy(mask => mask.PlantDefName).ThenBy(mask => mask.VariationIndex));
                if (File.Exists(temporary)) File.Delete(temporary);
                Scribe.saver.InitSaving(temporary, "HorticultureNovelSeedsAutoMasks");
                Scribe_Deep.Look(ref file, "autoMaskCache");
                Scribe.saver.FinalizeSaving();
                if (File.Exists(CachePath)) File.Replace(temporary, CachePath, null);
                else File.Move(temporary, CachePath);
                dirty = false;
            }
            catch (Exception exception)
            {
                Scribe.ForceStop();
                try { if (File.Exists(temporary)) File.Delete(temporary); } catch { }
                Log.Warning("[Horticulture - Novel Seeds] Could not save automatic plant masks: " + exception.Message);
            }
        }

        public static bool ExportBundle(string outputPath, out AutoMaskBundleValidationResult validation)
        {
            validation = new AutoMaskBundleValidationResult();
            EnsureLoaded();
            EnsureBundleLoaded();
            if (outputPath.NullOrEmpty())
            {
                validation.Error = "No bundle output path was supplied.";
                return false;
            }
            try
            {
                string directory = Path.GetDirectoryName(outputPath);
                if (!directory.NullOrEmpty()) Directory.CreateDirectory(directory);
                Dictionary<string, AutoPlantMaskRecord> effective =
                    new Dictionary<string, AutoPlantMaskRecord>(BundledRecords);
                foreach (KeyValuePair<string, AutoPlantMaskRecord> pair in Records) effective[pair.Key] = pair.Value;
                HashSet<string> currentKeys = new HashSet<string>();
                foreach (ThingDef plant in DefDatabase<ThingDef>.AllDefsListForReading
                    .Where(def => HorticulturePlantPolicy.IsSupported(def) && def.graphicData != null))
                {
                    for (int variation = 0; variation < PlantMaskUtility.VariationCount(plant); variation++)
                    {
                        string key = RecordKey(plant.defName, variation);
                        if (HorticultureNovelSeedsMod.Settings?.GetPlantSettings(plant, false)?.HasManualPlantMask(variation) != true)
                            currentKeys.Add(key);
                    }
                }
                List<AutoPlantMaskRecord> records = effective
                    .Where(pair => currentKeys.Contains(pair.Key) && !RequiresIdentityRegeneration(pair.Value))
                    .Select(pair => pair.Value)
                    .OrderBy(record => record.PlantDefName).ThenBy(record => record.VariationIndex).ToList();
                if (records.Count == 0)
                {
                    validation.Error = "No complete local or bundled records were available for export.";
                    return false;
                }
                string temporary = outputPath + ".tmp";
                if (File.Exists(temporary)) File.Delete(temporary);
                AutoPlantMaskBundleFile file = new AutoPlantMaskBundleFile();
                file.SetMetadata("mixed", "Horticulture - Novel Seeds", "generated-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss"));
                file.Masks.AddRange(records);
                Scribe.saver.InitSaving(temporary, "HorticultureNovelSeedsBundledAutoMasks");
                Scribe_Deep.Look(ref file, "autoMaskBundle");
                Scribe.saver.FinalizeSaving();
                if (File.Exists(outputPath)) File.Replace(temporary, outputPath, null);
                else File.Move(temporary, outputPath);
                validation = ValidateBundle(outputPath, true);
                return validation.Valid;
            }
            catch (Exception exception)
            {
                Scribe.ForceStop();
                validation.Error = exception.Message;
                try { if (File.Exists(outputPath + ".tmp")) File.Delete(outputPath + ".tmp"); } catch { }
                return false;
            }
        }

        public static AutoMaskBundleValidationResult ValidateBundle(string inputPath, bool validateAgainstCurrentDefs)
        {
            AutoMaskBundleValidationResult validation = new AutoMaskBundleValidationResult();
            if (inputPath.NullOrEmpty() || !File.Exists(inputPath))
            {
                validation.Error = "Bundle file does not exist.";
                return validation;
            }
            try
            {
                AutoPlantMaskBundleFile file = null;
                Scribe.loader.InitLoading(inputPath);
                Scribe_Deep.Look(ref file, "autoMaskBundle");
                Scribe.loader.FinalizeLoading();
                if (file == null)
                {
                    validation.Error = "Bundle root is missing.";
                    return validation;
                }
                if (file.LoadedFormatVersion != FormatVersion || file.LoadedGeneratorVersion != GeneratorVersion)
                {
                    validation.Error = "Bundle format/generator version is stale.";
                    return validation;
                }
                validation.BundleId = file.BundleId;
                HashSet<string> keys = new HashSet<string>();
                foreach (AutoPlantMaskRecord record in file.Masks)
                {
                    validation.RecordCount++;
                    if (record != null && record.LowConfidence) validation.LowConfidenceCount++;
                    string key = record == null ? string.Empty : RecordKey(record.PlantDefName, record.VariationIndex);
                    if (record == null || RequiresIdentityRegeneration(record) || !keys.Add(key))
                    {
                        validation.FailureCount++;
                        validation.FailureDetails.Add(key + ": incomplete or duplicate record");
                        continue;
                    }
                    if (!validateAgainstCurrentDefs) continue;
                    ThingDef plant = DefDatabase<ThingDef>.GetNamedSilentFail(record.PlantDefName);
                    Texture texture = plant == null ? null : PlantMaskUtility.TextureForVariation(plant, record.VariationIndex);
                    AutoMaskIdentity identity = texture == null ? null : CurrentIdentityFor(plant, record.VariationIndex, texture);
                    if (!record.Matches(identity))
                    {
                        validation.FailureCount++;
                        validation.FailureDetails.Add(key + ": " + record.MismatchReason(identity));
                    }
                }
                validation.Valid = validation.RecordCount > 0 && validation.FailureCount == 0;
                return validation;
            }
            catch (Exception exception)
            {
                Scribe.ForceStop();
                validation.Error = exception.Message;
                return validation;
            }
        }

        private static void EnsureLoaded()
        {
            if (initialized) return;
            initialized = true;
            if (!File.Exists(CachePath)) return;
            try
            {
                AutoPlantMaskCacheFile file = null;
                Scribe.loader.InitLoading(CachePath);
                Scribe_Deep.Look(ref file, "autoMaskCache");
                Scribe.loader.FinalizeLoading();
                if (file == null || file.LoadedFormatVersion != FormatVersion) return;
                foreach (AutoPlantMaskRecord record in file.Masks)
                    Records[RecordKey(record.PlantDefName, record.VariationIndex)] = record;
                if (file.LoadedGeneratorVersion != GeneratorVersion)
                {
                    foreach (string key in Records.Where(pair => RequiresIdentityRegeneration(pair.Value))
                        .Select(pair => pair.Key).ToList()) Records.Remove(key);
                    dirty = true;
                    Log.Message("[Horticulture - Novel Seeds] Loaded a legacy automatic plant-mask cache; records will be regenerated or reused by texture identity.");
                }
            }
            catch (Exception exception)
            {
                Scribe.ForceStop();
                Records.Clear();
                Log.Warning("[Horticulture - Novel Seeds] Could not load the automatic plant-mask cache: " + exception.Message);
            }
        }

        internal static bool RequiresIdentityRegeneration(AutoPlantMaskRecord record)
        {
            return record == null || record.FormatVersion != FormatVersion || record.GeneratorVersion != GeneratorVersion
                || record.SourcePackageId.NullOrEmpty() || record.SourceModName.NullOrEmpty()
                || record.PlantDefName.NullOrEmpty() || record.TexturePath.NullOrEmpty()
                || record.TextureContentHash.NullOrEmpty() || record.TextureWidth <= 0 || record.TextureHeight <= 0
                || record.TextureKey.NullOrEmpty() || record.GraphicIdentity.NullOrEmpty()
                || record.GrowthState.NullOrEmpty() || record.DirectionIdentity.NullOrEmpty()
                || record.VariationIdentity.NullOrEmpty() || record.ProduceSignature.NullOrEmpty()
                || record.EligibilityKey.NullOrEmpty() || record.MorphologyIdentity.NullOrEmpty();
        }

        private static AutoPlantMaskRecord Failed(string key)
        {
            FailedKeys.Add(key);
            return null;
        }

        private static string RecordKey(string plantDefName, int variationIndex) => plantDefName + "|" + variationIndex;

        private static string TextureKey(ThingDef plantDef, int variationIndex, Texture texture, ProduceSignature produce)
        {
            return MaskTextureIdentity.TryGet(texture, PlantMaskUtility.VariationLabel(plantDef, variationIndex), out string key)
                ? key : "unreadable|" + (texture?.name ?? "none") + "|" + texture?.width + "x" + texture?.height;
        }

        private static string EligibilityKey(ThingDef plantDef, int variationIndex, Texture texture,
            ProduceSignature produce, LayerEligibility eligibility)
        {
            Texture immature = PlantMaskUtility.ReferenceTextureForVariation(plantDef, variationIndex, "Immature");
            Texture leafless = PlantMaskUtility.ReferenceTextureForVariation(plantDef, variationIndex, "Leafless");
            string immatureKey = MaskTextureIdentity.TryGet(immature, "Immature", out string immatureIdentity) ? immatureIdentity : "none";
            string leaflessKey = MaskTextureIdentity.TryGet(leafless, "Leafless", out string leaflessIdentity) ? leaflessIdentity : "none";
            return "p:" + eligibility.produce + "|l:" + eligibility.leaves + "|s:" + eligibility.stem
                + "|f:" + eligibility.forceStem + "|struct:" + eligibility.structuralOnly
                + "|immature:" + immatureKey + "|leafless:" + leaflessKey + "|produce:" + produce.key;
        }

        private static AutoMaskIdentity BuildIdentity(ThingDef plantDef, int variationIndex, Texture texture)
        {
            ProduceSignature produce = ProduceColorFor(plantDef);
            LayerEligibility eligibility = EligibilityFor(plantDef, variationIndex, texture, produce);
            return BuildIdentity(plantDef, variationIndex, texture, produce, eligibility);
        }

        private static AutoMaskIdentity CurrentIdentityFor(ThingDef plantDef, int variationIndex, Texture texture)
        {
            string key = RecordKey(plantDef?.defName, variationIndex);
            if (CurrentIdentities.TryGetValue(key, out AutoMaskIdentity identity)) return identity;
            identity = BuildIdentity(plantDef, variationIndex, texture);
            if (identity != null) CurrentIdentities[key] = identity;
            return identity;
        }

        private static AutoMaskIdentity BuildIdentity(ThingDef plantDef, int variationIndex, Texture texture,
            ProduceSignature produce, LayerEligibility eligibility)
        {
            if (plantDef == null || texture == null) return null;
            string label = PlantMaskUtility.VariationLabel(plantDef, variationIndex) ?? string.Empty;
            if (!MaskTextureIdentity.TryGetDetails(texture, label, out MaskTextureIdentityDetails details)) return null;
            string textureKey = details.Key;
            string eligibilityKey = EligibilityKey(plantDef, variationIndex, texture, produce, eligibility);
            string sourcePackageId = plantDef.modContentPack?.PackageId;
            string sourceModName = plantDef.modContentPack?.Name;
            if (sourcePackageId.NullOrEmpty()) sourcePackageId = "Core";
            if (sourceModName.NullOrEmpty()) sourceModName = "Core";
            string morphology = "tree:" + HasTreeMorphology(plantDef)
                + "|isTree:" + (plantDef.plant?.IsTree == true)
                + "|forceTree:" + (plantDef.plant?.forceIsTree == true)
                + "|variation:" + label
                + "|produce:" + eligibility.produce + "|leaves:" + eligibility.leaves
                + "|stem:" + eligibility.stem + "|forceStem:" + eligibility.forceStem
                + "|structural:" + eligibility.structuralOnly;
            return new AutoMaskIdentity
            {
                SourcePackageId = sourcePackageId,
                SourceModName = sourceModName,
                PlantDefName = plantDef.defName,
                VariationIndex = variationIndex,
                TexturePath = PlantMaskUtility.TexturePathForVariation(plantDef, variationIndex) ?? string.Empty,
                TextureContentHash = details.ContentHash,
                TextureWidth = details.Width,
                TextureHeight = details.Height,
                TextureKey = textureKey,
                GraphicIdentity = PlantMaskUtility.GraphicIdentityFor(plantDef),
                GrowthState = details.StateLabel,
                DirectionIdentity = details.Orientation,
                VariationIdentity = PlantMaskUtility.VariationIdentityFor(plantDef, variationIndex),
                ProduceSignature = (produce.key ?? "none"),
                EligibilityKey = eligibilityKey,
                MorphologyIdentity = morphology,
                FormatVersion = FormatVersion,
                GeneratorVersion = GeneratorVersion
            };
        }

        private static bool IsBundledRecord(AutoPlantMaskRecord record)
        {
            if (record == null) return false;
            return BundledRecords.TryGetValue(RecordKey(record.PlantDefName, record.VariationIndex), out AutoPlantMaskRecord bundled)
                && ReferenceEquals(record, bundled);
        }

        private static void EnsureBundleLoaded()
        {
            if (bundleInitialized) return;
            bundleInitialized = true;
            if (HorticultureNovelSeedsMod.ContentRootPath.NullOrEmpty() || !File.Exists(BundledCachePath)) return;
            try
            {
                AutoPlantMaskBundleFile file = null;
                Scribe.loader.InitLoading(BundledCachePath);
                Scribe_Deep.Look(ref file, "autoMaskBundle");
                Scribe.loader.FinalizeLoading();
                if (file == null || file.LoadedFormatVersion != FormatVersion || file.LoadedGeneratorVersion != GeneratorVersion)
                {
                    Log.Warning("[Horticulture - Novel Seeds] Ignored bundled automatic masks because its format or generator version is stale.");
                    return;
                }
                foreach (AutoPlantMaskRecord record in file.Masks)
                {
                    if (RequiresIdentityRegeneration(record)) continue;
                    string key = RecordKey(record.PlantDefName, record.VariationIndex);
                    BundledRecords[key] = record;
                }
                Log.Message("[Horticulture - Novel Seeds] Loaded " + BundledRecords.Count + " bundled automatic plant masks.");
            }
            catch (Exception exception)
            {
                Scribe.ForceStop();
                BundledRecords.Clear();
                Log.Warning("[Horticulture - Novel Seeds] Could not load bundled automatic plant masks: " + exception.Message);
            }
        }

        private static string ReferenceFingerprint(Texture texture)
        {
            return texture == null ? "none" : texture.name + ":" + texture.width + "x" + texture.height + ":" + PixelFingerprint(texture);
        }

        private static int PixelFingerprint(Texture texture)
        {
            Color32[] pixels = ReadPixels(texture, 32);
            if (pixels == null) return 0;
            unchecked
            {
                int hash = 17;
                foreach (Color32 pixel in pixels)
                {
                    hash = hash * 31 + pixel.r;
                    hash = hash * 31 + pixel.g;
                    hash = hash * 31 + pixel.b;
                    hash = hash * 31 + pixel.a;
                }
                return hash;
            }
        }

        private static SourceData AnalyzeTexture(Texture texture)
        {
            Color32[] pixels = ReadPixels(texture, AnalysisSize);
            return AnalyzePixels(pixels);
        }

        private static SourceData AnalyzePixels(Color32[] pixels)
        {
            if (pixels == null || pixels.Length != AnalysisSize * AnalysisSize) return null;
            SourceData source = new SourceData
            {
                size = AnalysisSize,
                pixels = pixels,
                hue = new float[pixels.Length],
                saturation = new float[pixels.Length],
                brightness = new float[pixels.Length],
                cluster = Enumerable.Repeat(-1, pixels.Length).ToArray(),
                region = Enumerable.Repeat(-1, pixels.Length).ToArray()
            };
            List<int> opaque = new List<int>();
            for (int i = 0; i < pixels.Length; i++)
            {
                if (pixels[i].a <= TransparentAlpha) continue;
                Color.RGBToHSV(pixels[i], out source.hue[i], out source.saturation[i], out source.brightness[i]);
                opaque.Add(i);
            }
            if (opaque.Count < 8) return null;
            source.clusters = Cluster(source, opaque, Mathf.Clamp(Mathf.RoundToInt(Mathf.Sqrt(opaque.Count) / 42f), 2, 6));
            BuildConnectedRegions(source);
            return source;
        }

        private static List<ColorCluster> Cluster(SourceData source, List<int> opaque, int count)
        {
            List<ColorCluster> centers = new List<ColorCluster>();
            int first = opaque.OrderBy(index => source.hue[index] * 0.37f + source.saturation[index] * 0.41f + source.brightness[index] * 0.22f).First();
            centers.Add(ClusterAt(source, first));
            while (centers.Count < count)
            {
                int next = opaque.OrderByDescending(index => centers.Min(center => ColorDistance(source, index, center))).First();
                centers.Add(ClusterAt(source, next));
            }
            for (int iteration = 0; iteration < 7; iteration++)
            {
                float[] hueX = new float[count]; float[] hueY = new float[count];
                float[] saturation = new float[count]; float[] brightness = new float[count]; int[] totals = new int[count];
                foreach (int index in opaque)
                {
                    int cluster = 0; float best = float.MaxValue;
                    for (int candidate = 0; candidate < count; candidate++)
                    {
                        float distance = ColorDistance(source, index, centers[candidate]);
                        if (distance < best) { best = distance; cluster = candidate; }
                    }
                    source.cluster[index] = cluster;
                    float angle = source.hue[index] * Mathf.PI * 2f;
                    hueX[cluster] += Mathf.Cos(angle); hueY[cluster] += Mathf.Sin(angle);
                    saturation[cluster] += source.saturation[index]; brightness[cluster] += source.brightness[index]; totals[cluster]++;
                }
                for (int i = 0; i < count; i++)
                {
                    if (totals[i] == 0) continue;
                    centers[i].hue = Mathf.Repeat(Mathf.Atan2(hueY[i], hueX[i]) / (Mathf.PI * 2f), 1f);
                    centers[i].saturation = saturation[i] / totals[i]; centers[i].brightness = brightness[i] / totals[i]; centers[i].count = totals[i];
                }
            }
            for (int i = 0; i < centers.Count; i++)
                centers[i].separation = centers.Count == 1 ? 0f : centers.Where((_, other) => other != i).Min(other => ColorDistance(centers[i], other));
            return centers;
        }

        private static ColorCluster ClusterAt(SourceData source, int index)
        {
            return new ColorCluster { hue = source.hue[index], saturation = source.saturation[index], brightness = source.brightness[index], count = 1 };
        }

        private static void BuildConnectedRegions(SourceData source)
        {
            source.regions = new List<ConnectedRegion>();
            Queue<int> pending = new Queue<int>();
            for (int seed = 0; seed < source.pixels.Length; seed++)
            {
                if (source.cluster[seed] < 0 || source.region[seed] >= 0) continue;
                ConnectedRegion region = new ConnectedRegion { cluster = source.cluster[seed] };
                int regionIndex = source.regions.Count;
                source.region[seed] = regionIndex; pending.Enqueue(seed);
                float sumX = 0f; float sumY = 0f; float thickness = 0f; float branch = 0f;
                while (pending.Count > 0)
                {
                    int index = pending.Dequeue(); int x = index % source.size; int y = index / source.size;
                    region.count++; sumX += x; sumY += y;
                    region.minX = Mathf.Min(region.minX, x); region.maxX = Mathf.Max(region.maxX, x);
                    region.minY = Mathf.Min(region.minY, y); region.maxY = Mathf.Max(region.maxY, y);
                    int near = 0; int ring = 0;
                    for (int dy = -2; dy <= 2; dy++) for (int dx = -2; dx <= 2; dx++)
                    {
                        int nx = x + dx; int ny = y + dy;
                        if (nx < 0 || nx >= source.size || ny < 0 || ny >= source.size) continue;
                        int neighbor = ny * source.size + nx;
                        if (source.pixels[neighbor].a > TransparentAlpha) near++;
                        if ((Mathf.Abs(dx) == 2 || Mathf.Abs(dy) == 2) && source.pixels[neighbor].a > TransparentAlpha) ring++;
                    }
                    thickness += near / 25f; branch += ring / 16f;
                    for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        int nx = x + dx; int ny = y + dy;
                        if (nx < 0 || nx >= source.size || ny < 0 || ny >= source.size) continue;
                        int neighbor = ny * source.size + nx;
                        if (source.region[neighbor] < 0 && source.cluster[neighbor] == region.cluster)
                        { source.region[neighbor] = regionIndex; pending.Enqueue(neighbor); }
                    }
                }
                float width = region.maxX - region.minX + 1f; float height = region.maxY - region.minY + 1f;
                region.centerX = sumX / region.count / (source.size - 1f); region.centerY = sumY / region.count / (source.size - 1f);
                region.Thickness = thickness / region.count; region.Branch = branch / region.count;
                region.Compactness = Mathf.Clamp01(region.count / (width * height));
                region.Verticality = Mathf.Clamp01((height - width) / Mathf.Max(width, height) * 0.5f + 0.5f);
                source.regions.Add(region);
            }
        }

        private static List<VisualMaskLayerRecord> Classify(SourceData source, ProduceSignature produce,
            LayerEligibility eligibility, out float confidence)
        {
            return Classify(source, produce, eligibility, null, null, out confidence);
        }

        private static List<VisualMaskLayerRecord> Classify(SourceData source, ProduceSignature produce,
            LayerEligibility eligibility, Color32[] immatureReference, Color32[] leaflessReference,
            out float confidence)
        {
            List<VisualMaskLayerRecord> layers = new List<VisualMaskLayerRecord>
            {
                new VisualMaskLayerRecord { name = "Produce" }, new VisualMaskLayerRecord { name = "Leaves" }, new VisualMaskLayerRecord { name = "Stem" }
            };
            int opaquePixels = source.pixels.Count(pixel => pixel.a > TransparentAlpha);
            if (eligibility.structuralOnly)
            {
                for (int index = 0; index < source.pixels.Length; index++)
                    if (source.pixels[index].a > TransparentAlpha)
                        layers[2].PaintPixel(index % source.size, VisualMaskLayerRecord.Resolution - 1 - index / source.size, true);
                confidence = opaquePixels > 0 ? 1f : 0f;
                return layers;
            }

            float[] rootedStem = eligibility.stem ? BuildRootedStemMap(source, leaflessReference) : new float[source.pixels.Length];
            bool credibleStem = eligibility.stem && (!eligibility.leaves || HasCredibleStem(source, rootedStem));
            if (!credibleStem && eligibility.forceStem && eligibility.leaves)
            {
                rootedStem = BuildRootedStemMap(source, leaflessReference, true);
                credibleStem = HasCredibleStem(source, rootedStem);
            }
            eligibility.stem &= credibleStem;
            bool[] produceMap = eligibility.produce && produce.HasValue
                ? BuildProduceMap(source, produce, rootedStem, immatureReference, opaquePixels)
                : new bool[source.pixels.Length];
            int producePixels = 0; int leafPixels = 0; int stemPixels = 0; int unassignedPixels = 0;
            for (int index = 0; index < source.pixels.Length; index++)
            {
                if (source.pixels[index].a <= TransparentAlpha) continue;
                int x = index % source.size; int y = index / source.size;
                int selected = produceMap[index] ? 0 : eligibility.stem && rootedStem[index] >= 0.62f ? 2
                    : eligibility.leaves ? 1 : -1;
                if (selected < 0) { unassignedPixels++; continue; }
                int maskY = VisualMaskLayerRecord.Resolution - 1 - y;
                layers[selected].PaintPixel(x, maskY, true);
                if (selected == 0) producePixels++; else if (selected == 1) leafPixels++; else stemPixels++;
            }
            float stemShare = opaquePixels == 0 ? 0f : stemPixels / (float)opaquePixels;
            float produceShare = opaquePixels == 0 ? 0f : producePixels / (float)opaquePixels;
            float unassignedShare = opaquePixels == 0 ? 1f : unassignedPixels / (float)opaquePixels;
            confidence = 0.96f - Mathf.Max(0f, stemShare - 0.38f) * 1.4f
                - Mathf.Max(0f, produceShare - 0.24f) * 1.6f - unassignedShare * 0.35f;
            if (eligibility.forceStem && stemPixels == 0) confidence -= 0.30f;
            confidence = Mathf.Clamp01(confidence);
            return layers;
        }

        private static bool HasCredibleStem(SourceData source, float[] rootedStem)
        {
            int minX = source.size, minY = source.size, maxX = 0, maxY = 0;
            for (int index = 0; index < source.pixels.Length; index++)
            {
                if (source.pixels[index].a <= TransparentAlpha) continue;
                int x = index % source.size; int y = index / source.size;
                minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x);
                minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);
            }
            if (minX > maxX || minY > maxY) return false;
            float width = Mathf.Max(1f, maxX - minX + 1f);
            float height = Mathf.Max(1f, maxY - minY + 1f);
            if (height / width < 0.78f) return false;
            int rootedCount = 0; int rootedMinY = source.size; int rootedMaxY = 0;
            int baseMinX = source.size; int baseMaxX = -1;
            float baseTop = minY + height * 0.28f;
            for (int index = 0; index < rootedStem.Length; index++)
            {
                if (rootedStem[index] < 0.72f) continue;
                int x = index % source.size; int y = index / source.size;
                rootedCount++; rootedMinY = Mathf.Min(rootedMinY, y); rootedMaxY = Mathf.Max(rootedMaxY, y);
                if (y <= baseTop) { baseMinX = Mathf.Min(baseMinX, x); baseMaxX = Mathf.Max(baseMaxX, x); }
            }
            if (rootedCount < 8 || baseMaxX < baseMinX) return false;
            float verticalSpan = (rootedMaxY - rootedMinY + 1f) / height;
            float baseWidth = (baseMaxX - baseMinX + 1f) / width;
            float rootedShare = rootedCount / (float)source.pixels.Count(pixel => pixel.a > TransparentAlpha);
            return verticalSpan >= 0.42f && baseWidth <= 0.32f && rootedShare <= 0.30f;
        }

        private static bool[] BuildProduceMap(SourceData source, ProduceSignature produce, float[] rootedStem,
            Color32[] immatureReference, int opaquePixels)
        {
            bool alignedReference = AlphaIntersectionOverUnion(source.pixels, immatureReference) >= 0.88f;
            int sourceMinY = source.size; int sourceMaxY = 0;
            for (int index = 0; index < source.pixels.Length; index++)
                if (source.pixels[index].a > TransparentAlpha)
                {
                    int y = index / source.size;
                    sourceMinY = Mathf.Min(sourceMinY, y); sourceMaxY = Mathf.Max(sourceMaxY, y);
                }
            float sourceHeight = Mathf.Max(1f, sourceMaxY - sourceMinY + 1f);
            bool[] strong = new bool[source.pixels.Length];
            bool[] weak = new bool[source.pixels.Length];
            for (int index = 0; index < source.pixels.Length; index++)
            {
                if (source.pixels[index].a <= TransparentAlpha || rootedStem[index] >= 0.78f) continue;
                float distance = ProducePaletteDistance(source, index, produce);
                float stateDifference = alignedReference ? ColorDistance(source.pixels[index], immatureReference[index]) : 0f;
                strong[index] = alignedReference ? stateDifference >= 0.12f : distance <= 0.085f;
                weak[index] = alignedReference ? stateDifference >= 0.065f : distance <= 0.145f;
            }

            int[] distanceFromCore = Enumerable.Repeat(-1, source.pixels.Length).ToArray();
            Queue<int> pending = new Queue<int>();
            for (int index = 0; index < strong.Length; index++)
                if (strong[index]) { distanceFromCore[index] = 0; pending.Enqueue(index); }
            while (pending.Count > 0)
            {
                int index = pending.Dequeue();
                if (distanceFromCore[index] >= 4) continue;
                int x = index % source.size; int y = index / source.size;
                for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    int nx = x + dx; int ny = y + dy;
                    if (nx < 0 || nx >= source.size || ny < 0 || ny >= source.size) continue;
                    int neighbor = ny * source.size + nx;
                    if (!weak[neighbor] || distanceFromCore[neighbor] >= 0) continue;
                    distanceFromCore[neighbor] = distanceFromCore[index] + 1;
                    pending.Enqueue(neighbor);
                }
            }

            bool[] result = new bool[source.pixels.Length];
            bool[] visited = new bool[source.pixels.Length];
            for (int seed = 0; seed < result.Length; seed++)
            {
                if (distanceFromCore[seed] < 0 || visited[seed]) continue;
                List<int> component = new List<int>();
                int minX = source.size, minY = source.size, maxX = 0, maxY = 0, coreCount = 0;
                visited[seed] = true; pending.Enqueue(seed);
                while (pending.Count > 0)
                {
                    int index = pending.Dequeue(); component.Add(index);
                    int x = index % source.size; int y = index / source.size;
                    minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x); minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);
                    if (strong[index]) coreCount++;
                    for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        int nx = x + dx; int ny = y + dy;
                        if (nx < 0 || nx >= source.size || ny < 0 || ny >= source.size) continue;
                        int neighbor = ny * source.size + nx;
                        if (visited[neighbor] || distanceFromCore[neighbor] < 0) continue;
                        visited[neighbor] = true; pending.Enqueue(neighbor);
                    }
                }
                float share = component.Count / (float)Mathf.Max(1, opaquePixels);
                float compactness = component.Count / (float)((maxX - minX + 1) * (maxY - minY + 1));
                float coreShare = coreCount / (float)component.Count;
                float width = maxX - minX + 1f; float height = maxY - minY + 1f;
                float aspect = width / Mathf.Max(1f, height);
                float componentCenterY = (minY + maxY) * 0.5f;
                bool paletteRootMatch = !alignedReference && componentCenterY <= sourceMinY + sourceHeight * 0.22f;
                if (component.Count < 8 || coreCount < 3 || coreShare < 0.12f || share > 0.24f || paletteRootMatch
                    || compactness < 0.22f || aspect < 0.16f || aspect > 6.2f) continue;
                foreach (int index in component) result[index] = true;
            }
            int selectedPixels = result.Count(value => value);
            if (selectedPixels > opaquePixels * 0.24f) Array.Clear(result, 0, result.Length);
            return result;
        }

        private static float AlphaIntersectionOverUnion(Color32[] first, Color32[] second)
        {
            if (first == null || second == null || first.Length != second.Length) return 0f;
            int intersection = 0; int union = 0;
            for (int index = 0; index < first.Length; index++)
            {
                bool firstVisible = first[index].a > TransparentAlpha;
                bool secondVisible = second[index].a > TransparentAlpha;
                if (firstVisible || secondVisible) union++;
                if (firstVisible && secondVisible) intersection++;
            }
            return union == 0 ? 0f : intersection / (float)union;
        }

        private static float ProducePaletteDistance(SourceData source, int index, ProduceSignature produce)
        {
            if (produce.palette != null && produce.palette.Count > 0)
                return produce.palette.Min(color => ColorDistance(source, index, color));
            return ColorDistance(source, index, produce.produceColor);
        }

        private static float LocalThickness(SourceData source, int x, int y)
        {
            int occupied = 0; int total = 0;
            for (int dy = -3; dy <= 3; dy++) for (int dx = -3; dx <= 3; dx++)
            {
                int nx = x + dx; int ny = y + dy;
                if (nx < 0 || nx >= source.size || ny < 0 || ny >= source.size) continue;
                total++;
                if (source.pixels[ny * source.size + nx].a > TransparentAlpha) occupied++;
            }
            return total == 0 ? 0f : occupied / (float)total;
        }

        private static float[] BuildRootedStemMap(SourceData source, Color32[] leaflessReference = null,
            bool conservative = false)
        {
            float[] result = new float[source.pixels.Length];
            int minX = source.size, minY = source.size, maxX = 0, maxY = 0, opaqueCount = 0;
            for (int index = 0; index < source.pixels.Length; index++)
            {
                if (source.pixels[index].a <= TransparentAlpha) continue;
                int x = index % source.size; int y = index / source.size;
                minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x);
                minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y); opaqueCount++;
            }
            if (opaqueCount == 0) return result;

            if (leaflessReference != null && leaflessReference.Length == source.pixels.Length)
            {
                int referenced = 0;
                for (int index = 0; index < result.Length; index++)
                {
                    if (source.pixels[index].a <= TransparentAlpha || leaflessReference[index].a <= TransparentAlpha) continue;
                    result[index] = 1f; referenced++;
                }
                float referenceShare = referenced / (float)opaqueCount;
                if (referenceShare >= 0.025f && referenceShare <= 0.86f) return result;
                Array.Clear(result, 0, result.Length);
            }

            float width = Mathf.Max(1f, maxX - minX + 1f); float height = Mathf.Max(1f, maxY - minY + 1f);
            float centerX = (minX + maxX) * 0.5f;
            float rootTop = minY + height * (conservative ? 0.12f : 0.16f);
            float rootHalfWidth = width * (conservative ? 0.16f : 0.24f);
            int maxThickness = width <= source.size * 0.18f
                ? Mathf.CeilToInt(width) : conservative ? Mathf.Clamp(Mathf.RoundToInt(width * 0.075f), 5, 18)
                : Mathf.Clamp(Mathf.RoundToInt(width * 0.12f), 6, 28);
            float rootTolerance = conservative ? 0.24f : 0.42f;
            float localTolerance = conservative ? 0.10f : 0.15f;
            Queue<int> pending = new Queue<int>();
            int[] anchors = Enumerable.Repeat(-1, source.pixels.Length).ToArray();
            for (int index = 0; index < source.pixels.Length; index++)
            {
                if (source.pixels[index].a <= TransparentAlpha) continue;
                int x = index % source.size; int y = index / source.size;
                if (y > rootTop || Mathf.Abs(x - centerX) > rootHalfWidth) continue;
                if (!FitsStructuralWidth(source, x, y, index, 0.16f, maxThickness)) continue;
                result[index] = 1f; anchors[index] = index; pending.Enqueue(index);
            }
            if (pending.Count == 0) return result;
            while (pending.Count > 0)
            {
                int index = pending.Dequeue(); int x = index % source.size; int y = index / source.size;
                for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    int nx = x + dx; int ny = y + dy;
                    if (nx < 0 || nx >= source.size || ny < 0 || ny >= source.size) continue;
                    int neighbor = ny * source.size + nx;
                    if (result[neighbor] > 0f || source.cluster[neighbor] < 0) continue;
                    int anchor = anchors[index];
                    float rootDistance = ColorDistance(source, neighbor, anchor);
                    float localDistance = ColorDistance(source, neighbor, index);
                    if (rootDistance > rootTolerance || localDistance > localTolerance
                        || !FitsStructuralWidth(source, nx, ny, neighbor, 0.16f, maxThickness)) continue;
                    result[neighbor] = Mathf.Clamp01(1f - rootDistance * 0.28f);
                    anchors[neighbor] = anchor; pending.Enqueue(neighbor);
                }
            }
            return result;
        }

        private static bool FitsStructuralWidth(SourceData source, int x, int y, int referenceIndex,
            float tolerance, int maxThickness)
        {
            int limit = maxThickness + 1;
            int horizontal = 1 + ColorRun(source, x, y, -1, 0, referenceIndex, tolerance, limit)
                + ColorRun(source, x, y, 1, 0, referenceIndex, tolerance, limit);
            int vertical = 1 + ColorRun(source, x, y, 0, -1, referenceIndex, tolerance, limit)
                + ColorRun(source, x, y, 0, 1, referenceIndex, tolerance, limit);
            if (Mathf.Min(horizontal, vertical) <= maxThickness) return true;
            int risingDiagonal = 1 + ColorRun(source, x, y, -1, -1, referenceIndex, tolerance, limit)
                + ColorRun(source, x, y, 1, 1, referenceIndex, tolerance, limit);
            int fallingDiagonal = 1 + ColorRun(source, x, y, -1, 1, referenceIndex, tolerance, limit)
                + ColorRun(source, x, y, 1, -1, referenceIndex, tolerance, limit);
            return Mathf.Min(risingDiagonal, fallingDiagonal) <= maxThickness;
        }

        private static int ColorRun(SourceData source, int x, int y, int stepX, int stepY,
            int referenceIndex, float tolerance, int limit)
        {
            int count = 0;
            for (int step = 1; step <= limit; step++)
            {
                int nx = x + stepX * step; int ny = y + stepY * step;
                if (nx < 0 || nx >= source.size || ny < 0 || ny >= source.size) break;
                int index = ny * source.size + nx;
                if (source.pixels[index].a <= TransparentAlpha || ColorDistance(source, index, referenceIndex) > tolerance) break;
                count++;
            }
            return count;
        }

        private static ProduceSignature ProduceColorFor(ThingDef plantDef)
        {
            ThingDef produce = plantDef?.plant?.harvestedThingDef;
            Texture texture = produce?.uiIcon;
            ProduceSignature result = new ProduceSignature { key = produce?.defName ?? "none" };
            if (texture == null || texture.name.Equals("BadTexture", StringComparison.OrdinalIgnoreCase))
                texture = produce?.graphicData?.texPath.NullOrEmpty() == false
                    ? ContentFinder<Texture2D>.Get(produce.graphicData.texPath, false) : null;
            if (texture == null || texture.name.Equals("BadTexture", StringComparison.OrdinalIgnoreCase)) return result;
            Color32[] pixels = ReadPixels(texture, AnalysisSize);
            SourceData productSource = AnalyzePixels(pixels);
            if (productSource == null) return result;
            float red = 0f; float green = 0f; float blue = 0f; float weight = 0f;
            foreach (Color32 pixel in pixels)
            {
                if (pixel.a <= TransparentAlpha) continue;
                float alpha = pixel.a / 255f;
                red += pixel.r / 255f * alpha; green += pixel.g / 255f * alpha; blue += pixel.b / 255f * alpha; weight += alpha;
            }
            if (weight <= 0f) return result;
            result.HasValue = true;
            result.produceColor = new Color(red / weight, green / weight, blue / weight, 1f);
            result.palette = productSource.clusters.Where(cluster => cluster.count >= 4)
                .Select(cluster => Color.HSVToRGB(cluster.hue, cluster.saturation, cluster.brightness)).ToList();
            result.key += "|" + texture.name + "|" + texture.width + "x" + texture.height + "|"
                + PixelFingerprint(texture) + "|" + Mathf.RoundToInt(result.produceColor.r * 255f) + ","
                + Mathf.RoundToInt(result.produceColor.g * 255f) + "," + Mathf.RoundToInt(result.produceColor.b * 255f);
            return result;
        }

        private static LayerEligibility EligibilityFor(ThingDef plantDef, int variationIndex, Texture texture,
            ProduceSignature produce)
        {
            string label = PlantMaskUtility.VariationLabel(plantDef, variationIndex) ?? string.Empty;
            bool leafless = ContainsIgnoreCase(label, "Leafless") || ContainsIgnoreCase(texture?.name, "Leafless")
                || ContainsIgnoreCase(plantDef?.defName, "Leafless");
            bool immature = label.IndexOf("Immature", StringComparison.OrdinalIgnoreCase) >= 0;
            bool stump = plantDef?.thingCategories?.Any(category => category?.defName == "Stumps"
                || category.Parents.Any(parent => parent?.defName == "Stumps")) == true;
            ThingDef product = plantDef?.plant?.harvestedThingDef;
            bool visibleProductType = product != null && !product.IsStuff
                && (product.IsIngestible || plantDef.plant.humanFoodPlant || plantDef.plant.purpose == PlantPurpose.Food);
            bool treeMorphology = HasTreeMorphology(plantDef);
            return new LayerEligibility
            {
                produce = produce.HasValue && visibleProductType && !leafless && !immature && !stump,
                leaves = !leafless && !stump,
                stem = true,
                forceStem = stump || treeMorphology,
                structuralOnly = stump || leafless && treeMorphology
            };
        }

        private static bool HasTreeMorphology(ThingDef plantDef)
        {
            return plantDef?.plant != null && (plantDef.plant.IsTree || plantDef.plant.forceIsTree
                || plantDef.plant.treeCategory != TreeCategory.None);
        }

        private static bool ContainsIgnoreCase(string value, string fragment)
        {
            return value?.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static Color32[] ReadPixels(Texture texture, int size)
        {
            if (texture == null) return null;
            RenderTexture temporary = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGB32);
            RenderTexture previous = RenderTexture.active;
            Texture2D readable = null;
            try
            {
                Graphics.Blit(texture, temporary);
                RenderTexture.active = temporary;
                readable = new Texture2D(size, size, TextureFormat.RGBA32, false);
                readable.ReadPixels(new Rect(0f, 0f, size, size), 0, 0, false);
                readable.Apply(false, false);
                return readable.GetPixels32();
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(temporary);
                if (readable != null) UnityEngine.Object.Destroy(readable);
            }
        }

        private static float ColorDistance(SourceData source, int index, ColorCluster cluster)
        {
            float hue = Mathf.Abs(source.hue[index] - cluster.hue); hue = Mathf.Min(hue, 1f - hue);
            return Mathf.Sqrt(hue * hue * 2.2f + Mathf.Pow(source.saturation[index] - cluster.saturation, 2f) * 0.7f
                + Mathf.Pow(source.brightness[index] - cluster.brightness, 2f) * 0.55f);
        }

        private static float ColorDistance(SourceData source, int index, Color color)
        {
            Color.RGBToHSV(color, out float hue, out float saturation, out float brightness);
            float hueDistance = Mathf.Abs(source.hue[index] - hue); hueDistance = Mathf.Min(hueDistance, 1f - hueDistance);
            return Mathf.Sqrt(hueDistance * hueDistance * 2.2f + Mathf.Pow(source.saturation[index] - saturation, 2f) * 0.7f
                + Mathf.Pow(source.brightness[index] - brightness, 2f) * 0.55f);
        }

        private static float ColorDistance(SourceData source, int firstIndex, int secondIndex)
        {
            float hueDistance = Mathf.Abs(source.hue[firstIndex] - source.hue[secondIndex]);
            hueDistance = Mathf.Min(hueDistance, 1f - hueDistance);
            return Mathf.Sqrt(hueDistance * hueDistance * 2.2f
                + Mathf.Pow(source.saturation[firstIndex] - source.saturation[secondIndex], 2f) * 0.7f
                + Mathf.Pow(source.brightness[firstIndex] - source.brightness[secondIndex], 2f) * 0.55f);
        }

        private static float ColorDistance(Color32 first, Color32 second)
        {
            bool firstVisible = first.a > TransparentAlpha;
            bool secondVisible = second.a > TransparentAlpha;
            if (!firstVisible || !secondVisible) return firstVisible == secondVisible ? 0f : 1f;
            Color.RGBToHSV(first, out float firstHue, out float firstSaturation, out float firstBrightness);
            Color.RGBToHSV(second, out float secondHue, out float secondSaturation, out float secondBrightness);
            float hueDistance = Mathf.Abs(firstHue - secondHue); hueDistance = Mathf.Min(hueDistance, 1f - hueDistance);
            return Mathf.Sqrt(hueDistance * hueDistance * 2.2f
                + Mathf.Pow(firstSaturation - secondSaturation, 2f) * 0.7f
                + Mathf.Pow(firstBrightness - secondBrightness, 2f) * 0.55f);
        }

        private static float ColorDistance(ColorCluster first, ColorCluster second)
        {
            float hue = Mathf.Abs(first.hue - second.hue); hue = Mathf.Min(hue, 1f - hue);
            return Mathf.Sqrt(hue * hue * 2.2f + Mathf.Pow(first.saturation - second.saturation, 2f) * 0.7f
                + Mathf.Pow(first.brightness - second.brightness, 2f) * 0.55f);
        }

        private static float ColorDistance(ColorCluster cluster, Color color)
        {
            Color.RGBToHSV(color, out float hue, out float saturation, out float brightness);
            float hueDistance = Mathf.Abs(cluster.hue - hue); hueDistance = Mathf.Min(hueDistance, 1f - hueDistance);
            return Mathf.Clamp01(Mathf.Sqrt(hueDistance * hueDistance * 2.2f + Mathf.Pow(cluster.saturation - saturation, 2f) * 0.7f
                + Mathf.Pow(cluster.brightness - brightness, 2f) * 0.55f));
        }

#if HNS_VALIDATION
        internal static bool StemTopologyRegression()
        {
            Color32[] pixels = new Color32[AnalysisSize * AnalysisSize];
            Color32 wood = new Color32(116, 76, 42, 255); Color32 leaf = new Color32(54, 126, 57, 255);
            Action<int, int, int, int, Color32> fill = (x0, y0, x1, y1, color) =>
            {
                for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++) pixels[y * AnalysisSize + x] = color;
            };
            fill(122, 18, 133, 174, wood);
            fill(72, 130, 183, 138, wood);
            fill(82, 139, 90, 174, wood);
            fill(174, 139, 182, 174, wood);
            fill(42, 166, 106, 226, leaf);
            fill(150, 166, 214, 226, leaf);
            fill(102, 166, 153, 226, wood);
            SourceData source = AnalyzePixels(pixels);
            if (source == null) return false;
            List<VisualMaskLayerRecord> layers = Classify(source, default(ProduceSignature), new LayerEligibility
            {
                leaves = true, stem = true, forceStem = true
            }, out _);
            Func<VisualMaskLayerRecord, int, int, bool> painted = (layer, x, y) =>
                layer.IsPainted(x, VisualMaskLayerRecord.Resolution - 1 - y);
            return painted(layers[2], 127, 40) && painted(layers[2], 80, 134)
                && painted(layers[2], 178, 134) && painted(layers[1], 62, 196)
                && painted(layers[1], 194, 196) && painted(layers[1], 127, 196)
                && !painted(layers[2], 62, 196) && !painted(layers[2], 127, 196);
        }

        internal static string StemTopologyRegressionDetails()
        {
            Color32[] pixels = new Color32[AnalysisSize * AnalysisSize];
            Color32 wood = new Color32(116, 76, 42, 255); Color32 leaf = new Color32(54, 126, 57, 255);
            Action<int, int, int, int, Color32> fill = (x0, y0, x1, y1, color) =>
            {
                for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++) pixels[y * AnalysisSize + x] = color;
            };
            fill(122, 18, 133, 174, wood); fill(72, 130, 183, 138, wood);
            fill(82, 139, 90, 174, wood); fill(174, 139, 182, 174, wood);
            fill(42, 166, 106, 226, leaf); fill(150, 166, 214, 226, leaf);
            fill(102, 166, 153, 226, wood);
            SourceData source = AnalyzePixels(pixels);
            if (source == null) return "source=null";
            List<VisualMaskLayerRecord> layers = Classify(source, default(ProduceSignature), new LayerEligibility
            {
                leaves = true, stem = true, forceStem = true
            }, out float confidence);
            Func<VisualMaskLayerRecord, int, int, bool> painted = (layer, x, y) =>
                layer.IsPainted(x, VisualMaskLayerRecord.Resolution - 1 - y);
            int stemPixels = 0; int leafPixels = 0;
            for (int y = 0; y < AnalysisSize; y++) for (int x = 0; x < AnalysisSize; x++)
            {
                if (painted(layers[2], x, y)) stemPixels++;
                if (painted(layers[1], x, y)) leafPixels++;
            }
            return "trunk=" + painted(layers[2], 127, 40) + ",leftBranch=" + painted(layers[2], 80, 134)
                + ",rightBranch=" + painted(layers[2], 178, 134) + ",leftLeaf=" + painted(layers[1], 62, 196)
                + ",rightLeaf=" + painted(layers[1], 194, 196) + ",leafAsStem=" + painted(layers[2], 62, 196)
                + ",denseCanopyLeaf=" + painted(layers[1], 127, 196) + ",denseCanopyStem=" + painted(layers[2], 127, 196)
                + ",stemPixels=" + stemPixels + ",leafPixels=" + leafPixels + ",confidence=" + confidence.ToString("0.000");
        }

        public static bool MaskCorrectionRegression()
        {
            if (!StemTopologyRegression() || !ForcedStemCredibilityRegression()
                || !GroundcoverStemRegression() || !LayerAbsenceRegression()
                || !LowConfidenceFallbackRegression() || !VisualRecolorRegression()
                || !DeterministicClassificationRegression()) return false;
            int resolution = VisualMaskLayerRecord.Resolution;
            Color32[] pixels = new Color32[resolution * resolution];
            var source = new VisualMaskLayerRecord { name = "Leaves" };
            Color32 branch = new Color32(118, 78, 44, 255);
            for (int x = 40; x <= 96; x++)
            {
                int topY = 120;
                source.PaintPixel(x, topY, true);
                pixels[(resolution - 1 - topY) * resolution + x] = branch;
            }
            source.PaintPixel(140, 120, true);
            pixels[(resolution - 1 - 120) * resolution + 140] = branch;
            List<int> region = Dialog_PlantMasks.ConnectedMaskedRegion(pixels, source, 60, 120, 0.08f);
            return region.Count == 57 && region.Any(index => index % resolution == 96)
                && region.All(index => index % resolution != 140);
        }

        internal static bool LayerAbsenceRegression()
        {
            Color32[] foliage = new Color32[AnalysisSize * AnalysisSize];
            Color32 green = new Color32(52, 132, 58, 255);
            for (int y = 70; y <= 205; y++) for (int x = 28; x <= 227; x++)
            {
                float nx = (x - 127.5f) / 100f; float ny = (y - 138f) / 68f;
                if (nx * nx + ny * ny <= 1f) foliage[y * AnalysisSize + x] = green;
            }
            SourceData foliageSource = AnalyzePixels(foliage);
            if (foliageSource == null) return false;
            List<VisualMaskLayerRecord> foliageLayers = Classify(foliageSource, default(ProduceSignature),
                new LayerEligibility { produce = true, leaves = true, stem = true }, out _);
            if (foliageLayers[0].HasPixels || !foliageLayers[1].HasPixels || foliageLayers[2].HasPixels) return false;

            Color32[] fruiting = (Color32[])foliage.Clone();
            Color32 fruit = new Color32(202, 36, 42, 255);
            Action<int, int> paintFruit = (centerX, centerY) =>
            {
                for (int y = centerY - 7; y <= centerY + 7; y++) for (int x = centerX - 7; x <= centerX + 7; x++)
                    if ((x - centerX) * (x - centerX) + (y - centerY) * (y - centerY) <= 49)
                        fruiting[y * AnalysisSize + x] = fruit;
            };
            paintFruit(86, 122); paintFruit(166, 150);
            SourceData fruitingSource = AnalyzePixels(fruiting);
            if (fruitingSource == null) return false;
            List<VisualMaskLayerRecord> fruitingLayers = Classify(fruitingSource, new ProduceSignature
            {
                HasValue = true,
                produceColor = fruit
            },
                new LayerEligibility { produce = true, leaves = true }, out _);
            if (!fruitingLayers[0].HasPixels || !fruitingLayers[1].HasPixels || fruitingLayers[2].HasPixels) return false;

            Color32[] rootedProduce = (Color32[])foliage.Clone();
            for (int y = 72; y <= 100; y++) for (int x = 108; x <= 147; x++)
                rootedProduce[y * AnalysisSize + x] = fruit;
            SourceData rootedProduceSource = AnalyzePixels(rootedProduce);
            List<VisualMaskLayerRecord> rootedProduceLayers = Classify(rootedProduceSource, new ProduceSignature
            {
                HasValue = true, produceColor = fruit
            }, new LayerEligibility { produce = true, leaves = true }, out _);
            if (rootedProduceLayers[0].HasPixels) return false;

            Color32[] centralProduce = (Color32[])foliage.Clone();
            for (int y = 108; y <= 167; y++) for (int x = 96; x <= 159; x++)
            {
                float nx = (x - 127.5f) / 32f; float ny = (y - 137.5f) / 30f;
                if (nx * nx + ny * ny <= 1f) centralProduce[y * AnalysisSize + x] = fruit;
            }
            SourceData centralProduceSource = AnalyzePixels(centralProduce);
            List<VisualMaskLayerRecord> centralProduceLayers = Classify(centralProduceSource, new ProduceSignature
            {
                HasValue = true, produceColor = fruit
            }, new LayerEligibility { produce = true, leaves = true }, out _);
            if (!centralProduceLayers[0].HasPixels) return false;

            Color32[] trunk = new Color32[AnalysisSize * AnalysisSize];
            Color32 wood = new Color32(112, 73, 40, 255);
            for (int y = 24; y <= 220; y++) for (int x = 119; x <= 136; x++) trunk[y * AnalysisSize + x] = wood;
            SourceData trunkSource = AnalyzePixels(trunk);
            if (trunkSource == null) return false;
            List<VisualMaskLayerRecord> trunkLayers = Classify(trunkSource, default(ProduceSignature),
                new LayerEligibility { stem = true, forceStem = true }, out _);
            if (trunkLayers[0].HasPixels || trunkLayers[1].HasPixels || !trunkLayers[2].HasPixels) return false;

            List<VisualMaskLayerRecord> neutralLayers = Classify(foliageSource, default(ProduceSignature),
                default(LayerEligibility), out _);
            return neutralLayers.All(layer => !layer.HasPixels);
        }

        internal static bool GroundcoverStemRegression()
        {
            Color32[] pixels = new Color32[AnalysisSize * AnalysisSize];
            Color32 wood = new Color32(112, 73, 40, 255); Color32 leaf = new Color32(52, 132, 58, 255);
            for (int y = 72; y <= 184; y++) for (int x = 18; x <= 237; x++)
            {
                float nx = (x - 127.5f) / 110f; float ny = (y - 128f) / 56f;
                if (nx * nx + ny * ny <= 1f) pixels[y * AnalysisSize + x] = leaf;
            }
            for (int y = 72; y <= 128; y++) for (int x = 122; x <= 133; x++) pixels[y * AnalysisSize + x] = wood;
            for (int x = 62; x <= 193; x++) for (int y = 118; y <= 126; y++) pixels[y * AnalysisSize + x] = wood;
            SourceData source = AnalyzePixels(pixels);
            if (source == null) return false;
            List<VisualMaskLayerRecord> layers = Classify(source, default(ProduceSignature),
                new LayerEligibility { leaves = true, stem = true }, out _);
            return layers[1].HasPixels && !layers[2].HasPixels;
        }

        internal static bool ForcedStemCredibilityRegression()
        {
            Color32[] pixels = new Color32[AnalysisSize * AnalysisSize];
            Color32 wood = new Color32(103, 77, 45, 255);
            Color32 nearWoodLeaf = new Color32(105, 105, 48, 255);
            for (int y = 18; y <= 176; y++) for (int x = 120; x <= 135; x++)
                pixels[y * AnalysisSize + x] = wood;
            for (int y = 118; y <= 230; y++) for (int x = 24; x <= 231; x++)
            {
                float nx = (x - 127.5f) / 104f; float ny = (y - 174f) / 57f;
                if (nx * nx + ny * ny <= 1f && pixels[y * AnalysisSize + x].a == 0)
                    pixels[y * AnalysisSize + x] = nearWoodLeaf;
            }
            SourceData source = AnalyzePixels(pixels);
            if (source == null) return false;
            List<VisualMaskLayerRecord> layers = Classify(source, default(ProduceSignature),
                new LayerEligibility { leaves = true, stem = true, forceStem = true }, out _);
            int opaque = pixels.Count(pixel => pixel.a > TransparentAlpha);
            int stem = 0;
            for (int y = 0; y < AnalysisSize; y++) for (int x = 0; x < AnalysisSize; x++)
                if (layers[2].IsPainted(x, AnalysisSize - 1 - y)) stem++;
            return layers[2].IsPainted(127, AnalysisSize - 1 - 60)
                && layers[1].IsPainted(62, AnalysisSize - 1 - 190)
                && stem <= opaque * 0.30f;
        }

        internal static bool LowConfidenceFallbackRegression()
        {
            VisualMaskLayerRecord layer = new VisualMaskLayerRecord { name = "Leaves" };
            layer.PaintPixel(120, 120, true);
            AutoPlantMaskRecord low = new AutoPlantMaskRecord("Low", 0, "low", LowConfidenceThreshold - 0.01f,
                new[] { new VisualMaskLayerRecord(), layer, new VisualMaskLayerRecord() });
            AutoPlantMaskRecord high = new AutoPlantMaskRecord("High", 0, "high", LowConfidenceThreshold + 0.01f,
                new[] { new VisualMaskLayerRecord(), layer, new VisualMaskLayerRecord() });
            return !IsRenderable(low) && IsRenderable(high);
        }

        internal static bool VisualRecolorRegression()
        {
            Color dark = Color.HSVToRGB(0.31f, 0.55f, 0.08f, false);
            Color light = Color.HSVToRGB(0.31f, 0.55f, 0.62f, false);
            dark.a = 0.73f; light.a = 0.73f;
            Color darkStyled = PlantVisualColorUtility.Apply(dark, 0.92f, 0.18f, 0.16f,
                0f, 1f, 1f, 1f, 1f, 0f);
            Color lightStyled = PlantVisualColorUtility.Apply(light, 0.92f, 0.18f, 0.16f,
                0f, 1f, 1f, 1f, 1f, 0f);
            Color.RGBToHSV(darkStyled, out _, out _, out float darkValue);
            Color.RGBToHSV(lightStyled, out _, out _, out float lightValue);
            float darkChange = ColorDistance(dark, darkStyled);
            float lightChange = ColorDistance(light, lightStyled);
            return Mathf.Abs(darkStyled.a - dark.a) < 0.001f && Mathf.Abs(lightStyled.a - light.a) < 0.001f
                && darkValue < lightValue && darkChange < lightChange * 0.45f;
        }

        internal static bool DeterministicClassificationRegression()
        {
            Color32[] pixels = new Color32[AnalysisSize * AnalysisSize];
            Color32 leaf = new Color32(123, 67, 151, 255);
            Color32 fruit = new Color32(226, 178, 52, 255);
            for (int y = 54; y <= 211; y++) for (int x = 47; x <= 208; x++)
            {
                float nx = (x - 127.5f) / 81f; float ny = (y - 132.5f) / 79f;
                if (nx * nx + ny * ny <= 1f) pixels[y * AnalysisSize + x] = leaf;
            }
            for (int y = 118; y <= 151; y++) for (int x = 111; x <= 144; x++)
            {
                float nx = (x - 127.5f) / 17f; float ny = (y - 134.5f) / 17f;
                if (nx * nx + ny * ny <= 1f) pixels[y * AnalysisSize + x] = fruit;
            }
            SourceData firstSource = AnalyzePixels((Color32[])pixels.Clone());
            SourceData secondSource = AnalyzePixels((Color32[])pixels.Clone());
            if (firstSource == null || secondSource == null) return false;
            ProduceSignature produce = new ProduceSignature { HasValue = true, produceColor = fruit };
            LayerEligibility eligibility = new LayerEligibility { produce = true, leaves = true };
            List<VisualMaskLayerRecord> first = Classify(firstSource, produce, eligibility, out float firstConfidence);
            List<VisualMaskLayerRecord> second = Classify(secondSource, produce, eligibility, out float secondConfidence);
            return Mathf.Approximately(firstConfidence, secondConfidence)
                && first.Select(layer => layer.ContentHash).SequenceEqual(second.Select(layer => layer.ContentHash));
        }
#endif
    }
}
