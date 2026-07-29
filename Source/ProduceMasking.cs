using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld;
using UnityEngine;
using Verse;

namespace HorticultureNovelSeeds
{
    public sealed class VisualMaskLayerRecord : IExposable
    {
        public const int Resolution = 256;
        private const int LegacyResolution = 64;
        private const int ByteCount = Resolution * Resolution / 8;
        private const int LegacyByteCount = LegacyResolution * LegacyResolution / 8;

        public string name = "Produce";
        private string packedMask;
        [Unsaved(false)] private byte[] maskBytes;
        [Unsaved(false)] private bool hasPixelsCached;
        [Unsaved(false)] private bool hasPixelsCacheValid;
        [Unsaved(false)] private int contentHashCached;
        [Unsaved(false)] private bool contentHashCacheValid;

        public bool HasPixels
        {
            get
            {
                EnsureMask();
                if (!hasPixelsCacheValid)
                {
                    hasPixelsCached = maskBytes.Any(value => value != 0);
                    hasPixelsCacheValid = true;
                }
                return hasPixelsCached;
            }
        }

        public void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving) packedMask = Convert.ToBase64String(GetBytes());
            Scribe_Values.Look(ref name, "name", "Produce");
            Scribe_Values.Look(ref packedMask, "mask");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                maskBytes = Decode(packedMask);
                InvalidateCaches();
                Normalize();
            }
        }

        public void Normalize()
        {
            if (name.NullOrEmpty()) name = "Mask";
            name = name.Trim();
            if (name.Length > 32) name = name.Substring(0, 32);
            EnsureMask();
        }

        public bool IsPainted(int x, int y)
        {
            if (x < 0 || x >= Resolution || y < 0 || y >= Resolution) return false;
            EnsureMask();
            int bit = y * Resolution + x;
            return (maskBytes[bit >> 3] & 1 << (bit & 7)) != 0;
        }

        public bool PaintCircle(int centerX, int centerY, int diameter, bool paint)
        {
            EnsureMask();
            diameter = Mathf.Max(1, diameter);
            bool changed = false;
            float radius = diameter * 0.5f;
            float offset = diameter % 2 == 0 ? 0.5f : 0f;
            float originX = centerX + offset;
            float originY = centerY + offset;
            float radiusSquared = radius * radius;
            int minX = Mathf.Max(0, Mathf.FloorToInt(originX - radius));
            int maxX = Mathf.Min(Resolution - 1, Mathf.CeilToInt(originX + radius));
            int minY = Mathf.Max(0, Mathf.FloorToInt(originY - radius));
            int maxY = Mathf.Min(Resolution - 1, Mathf.CeilToInt(originY + radius));
            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    float dx = x - originX;
                    float dy = y - originY;
                    if (dx * dx + dy * dy > radiusSquared) continue;
                    changed |= SetPainted(x, y, paint);
                }
            }
            return changed;
        }

        public bool PaintPixel(int x, int y, bool paint)
        {
            if (x < 0 || x >= Resolution || y < 0 || y >= Resolution) return false;
            EnsureMask();
            return SetPainted(x, y, paint);
        }

        public void Clear()
        {
            maskBytes = new byte[ByteCount];
            hasPixelsCached = false;
            hasPixelsCacheValid = true;
            contentHashCacheValid = false;
        }

        public VisualMaskLayerRecord Clone()
        {
            EnsureMask();
            return new VisualMaskLayerRecord { name = name, maskBytes = (byte[])maskBytes.Clone() };
        }


        public int ContentHash
        {
            get
            {
                if (contentHashCacheValid) return contentHashCached;
                unchecked
                {
                    int hash = 17;
                    foreach (byte value in GetBytes()) hash = hash * 31 + value;
                    contentHashCached = hash;
                    contentHashCacheValid = true;
                    return contentHashCached;
                }
            }
        }

        private bool SetPainted(int x, int y, bool painted)
        {
            int bit = y * Resolution + x;
            int index = bit >> 3;
            byte flag = (byte)(1 << (bit & 7));
            bool current = (maskBytes[index] & flag) != 0;
            if (current == painted) return false;
            if (painted) maskBytes[index] |= flag;
            else maskBytes[index] &= (byte)~flag;
            contentHashCacheValid = false;
            if (painted)
            {
                hasPixelsCached = true;
                hasPixelsCacheValid = true;
            }
            else
            {
                hasPixelsCacheValid = false;
            }
            return true;
        }

        private byte[] GetBytes()
        {
            EnsureMask();
            return maskBytes;
        }

        private void EnsureMask()
        {
            if (maskBytes == null || maskBytes.Length != ByteCount)
            {
                maskBytes = Decode(packedMask);
                InvalidateCaches();
            }
        }

        private static byte[] Decode(string value)
        {
            if (!value.NullOrEmpty())
            {
                try
                {
                    byte[] decoded = Convert.FromBase64String(value);
                    if (decoded.Length == ByteCount) return decoded;
                    if (decoded.Length == LegacyByteCount) return UpgradeLegacyMask(decoded);
                }
                catch (FormatException)
                {
                }
            }
            return new byte[ByteCount];
        }

        private static byte[] UpgradeLegacyMask(byte[] legacy)
        {
            byte[] upgraded = new byte[ByteCount];
            int scale = Resolution / LegacyResolution;
            for (int y = 0; y < LegacyResolution; y++)
            {
                for (int x = 0; x < LegacyResolution; x++)
                {
                    int legacyBit = y * LegacyResolution + x;
                    if ((legacy[legacyBit >> 3] & 1 << (legacyBit & 7)) == 0) continue;
                    for (int offsetY = 0; offsetY < scale; offsetY++)
                    {
                        for (int offsetX = 0; offsetX < scale; offsetX++)
                        {
                            int bit = (y * scale + offsetY) * Resolution + x * scale + offsetX;
                            upgraded[bit >> 3] |= (byte)(1 << (bit & 7));
                        }
                    }
                }
            }
            return upgraded;
        }

        private void InvalidateCaches()
        {
            hasPixelsCacheValid = false;
            contentHashCacheValid = false;
        }
    }

    public sealed class PlantMaskVariationRecord : IExposable
    {
        private int variationIndex;
        private List<VisualMaskLayerRecord> layers = new List<VisualMaskLayerRecord>();
        public int VariationIndex => variationIndex;
        public List<VisualMaskLayerRecord> Layers => layers;

        public PlantMaskVariationRecord() { }
        public PlantMaskVariationRecord(int variationIndex, IEnumerable<VisualMaskLayerRecord> source)
        {
            this.variationIndex = variationIndex;
            layers = source?.Select(layer => layer.Clone()).ToList() ?? new List<VisualMaskLayerRecord>();
            Normalize();
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref variationIndex, "variationIndex", 1);
            Scribe_Collections.Look(ref layers, "layers", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit) Normalize();
        }

        public void Normalize()
        {
            variationIndex = Mathf.Max(1, variationIndex);
            if (layers == null) layers = new List<VisualMaskLayerRecord>();
            layers.RemoveAll(layer => layer == null);
            while (layers.Count < 3) layers.Add(new VisualMaskLayerRecord());
            if (layers.Count > 3) layers.RemoveRange(3, layers.Count - 3);
            string[] names = { "Produce", "Leaves", "Stem" };
            for (int i = 0; i < 3; i++) { layers[i].name = names[i]; layers[i].Normalize(); }
        }
    }
    public static class PlantMaskUtility
    {
        private sealed class TextureVariation
        {
            public Texture texture;
            public string label;
        }

        private const string BloomingExtensionType = "VEF.Plants.BloomingPlantExtension";
        private static readonly Dictionary<ThingDef, List<TextureVariation>> TextureVariations = new Dictionary<ThingDef, List<TextureVariation>>();

        public static void BakedTextureSize(int sourceWidth, int sourceHeight, out int width, out int height)
        {
            sourceWidth = Mathf.Max(1, sourceWidth);
            sourceHeight = Mathf.Max(1, sourceHeight);
            int largest = Mathf.Max(sourceWidth, sourceHeight);
            if (largest >= VisualMaskLayerRecord.Resolution)
            {
                width = sourceWidth;
                height = sourceHeight;
                return;
            }
            float scale = VisualMaskLayerRecord.Resolution / (float)largest;
            width = Mathf.Max(1, Mathf.RoundToInt(sourceWidth * scale));
            height = Mathf.Max(1, Mathf.RoundToInt(sourceHeight * scale));
        }

        public static bool HasActiveMasks(ThingDef plantDef)
        {
            return plantDef != null && HorticultureNovelSeedsMod.Settings?.GetPlantSettings(plantDef, false)?.HasActivePlantMasks == true;
        }

        public static int VariationCount(ThingDef plantDef)
        {
            return VariationsFor(plantDef).Count;
        }

        public static Texture TextureForVariation(ThingDef plantDef, int variationIndex)
        {
            List<TextureVariation> variations = VariationsFor(plantDef);
            return variations[Mathf.Clamp(variationIndex, 0, variations.Count - 1)].texture ?? plantDef?.uiIcon;
        }

        public static string VariationLabel(ThingDef plantDef, int variationIndex)
        {
            List<TextureVariation> variations = VariationsFor(plantDef);
            return variations[Mathf.Clamp(variationIndex, 0, variations.Count - 1)].label;
        }

        public static int VariationIndexForTexture(ThingDef plantDef, Texture texture, int fallbackIndex = 0)
        {
            List<TextureVariation> variations = VariationsFor(plantDef);
            if (texture != null)
            {
                int textureId = texture.GetInstanceID();
                for (int i = 0; i < variations.Count; i++)
                    if (variations[i].texture != null && variations[i].texture.GetInstanceID() == textureId) return i;
            }
            return Mathf.Clamp(fallbackIndex, 0, variations.Count - 1);
        }

        public static List<VisualMaskLayerRecord> LayersForVariation(ThingDef plantDef, int variationIndex, bool create = false)
        {
            PlantSettingsRecord settings = HorticultureNovelSeedsMod.Settings?.GetPlantSettings(plantDef, create);
            return settings?.PlantMaskLayersForVariation(variationIndex, create);
        }

        public static int LayerAt(ThingDef plantDef, int sourceX, int sourceY, int width, int height, int variationIndex = 0)
        {
            PlantSettingsRecord settings = HorticultureNovelSeedsMod.Settings?.GetPlantSettings(plantDef, false);
            if (settings?.HasActivePlantMasks != true) return -2;
            return LayerAt(settings.PlantMaskLayersForVariation(variationIndex, false), sourceX, sourceY, width, height);
        }

        public static int LayerAt(IReadOnlyList<VisualMaskLayerRecord> layers, int sourceX, int sourceY, int width, int height)
        {
            if (layers == null) return -1;
            int maskX = Mathf.Clamp(sourceX * VisualMaskLayerRecord.Resolution / width, 0, VisualMaskLayerRecord.Resolution - 1);
            int maskY = VisualMaskLayerRecord.Resolution - 1 - Mathf.Clamp(sourceY * VisualMaskLayerRecord.Resolution / height, 0, VisualMaskLayerRecord.Resolution - 1);
            for (int i = 0; i < layers.Count; i++) if (layers[i].IsPainted(maskX, maskY)) return i;
            return -1;
        }

        public static int MaskHash(ThingDef plantDef, int variationIndex = -1)
        {
            PlantSettingsRecord settings = HorticultureNovelSeedsMod.Settings?.GetPlantSettings(plantDef, false);
            if (settings?.HasActivePlantMasks != true) return 0;
            unchecked
            {
                int hash = 486187739;
                IEnumerable<List<VisualMaskLayerRecord>> variations = variationIndex >= 0
                    ? new[] { settings.PlantMaskLayersForVariation(variationIndex, false) }
                    : settings.AllPlantMaskVariations();
                foreach (List<VisualMaskLayerRecord> layers in variations)
                    foreach (VisualMaskLayerRecord layer in layers) hash = hash * 31 + layer.ContentHash;
                return hash;
            }
        }

        private static List<TextureVariation> VariationsFor(ThingDef plantDef)
        {
            if (plantDef != null && TextureVariations.TryGetValue(plantDef, out List<TextureVariation> cached)) return cached;
            List<TextureVariation> result = new List<TextureVariation>();
            AddGraphicVariations(result, plantDef?.graphicData?.Graphic, "Normal");
            AddPathVariations(result, plantDef, BloomingPath(plantDef, "bloomGraphicPath"), "Blooming");
            AddPathVariations(result, plantDef, BloomingPath(plantDef, "alternateBloomGraphicPath"), "Alternate Bloom");
            AddPathVariations(result, plantDef, PrivatePath(plantDef?.plant, "leaflessGraphicPath"), "Leafless");
            AddPathVariations(result, plantDef, PrivatePath(plantDef?.plant, "immatureGraphicPath"), "Immature");
            AddPathVariations(result, plantDef, PrivatePath(plantDef?.plant, "leaflessImmatureGraphicPath"), "Leafless Immature");
            AddPathVariations(result, plantDef, PrivatePath(plantDef?.plant, "pollutedGraphicPath"), "Polluted");
            if (result.Count == 0) result.Add(new TextureVariation { texture = plantDef?.uiIcon, label = "Normal" });
            if (plantDef != null) TextureVariations[plantDef] = result;
            return result;
        }

        private static void AddPathVariations(List<TextureVariation> result, ThingDef plantDef, string path, string stateLabel)
        {
            if (path.NullOrEmpty() || plantDef?.graphicData == null) return;
            try
            {
                GraphicData data = plantDef.graphicData;
                Shader shader = data.shaderType?.Shader ?? ShaderDatabase.CutoutPlant;
                Graphic graphic = GraphicDatabase.Get<Graphic_Random>(path, shader, data.drawSize, data.color, data.colorTwo, data, data.maskPath);
                AddGraphicVariations(result, graphic, stateLabel);
            }
            catch (Exception exception)
            {
                Log.WarningOnce("[Horticulture - Novel Seeds] Could not load the " + stateLabel + " mask texture for " + plantDef.defName + ": " + exception.Message,
                    Gen.HashCombineInt(plantDef.shortHash, stateLabel.GetHashCode()));
            }
        }

        private static void AddGraphicVariations(List<TextureVariation> result, Graphic graphic, string stateLabel)
        {
            List<Texture> textures = new List<Texture>();
            if (graphic is Graphic_Random random && random.SubGraphicsCount > 0)
            {
                for (int i = 0; i < random.SubGraphicsCount; i++) textures.Add(random.SubGraphicAtIndex(i).MatSingle?.mainTexture);
            }
            else if (graphic != null)
            {
                textures.Add(graphic.MatSingle?.mainTexture);
            }
            HashSet<int> knownTextureIds = new HashSet<int>();
            foreach (TextureVariation entry in result)
                if (entry.texture != null) knownTextureIds.Add(entry.texture.GetInstanceID());

            List<Texture> distinctTextures = new List<Texture>();
            foreach (Texture texture in textures)
            {
                if (texture != null && knownTextureIds.Add(texture.GetInstanceID())) distinctTextures.Add(texture);
            }

            int total = distinctTextures.Count;
            for (int i = 0; i < total; i++)
            {
                result.Add(new TextureVariation
                {
                    texture = distinctTextures[i],
                    label = total > 1 ? stateLabel + " " + (i + 1) + " of " + total : stateLabel
                });
            }
        }

        private static string BloomingPath(ThingDef plantDef, string fieldName)
        {
            DefModExtension extension = plantDef?.modExtensions?.FirstOrDefault(item => item?.GetType().FullName == BloomingExtensionType);
            FieldInfo field = extension?.GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.Instance);
            return field?.GetValue(extension) as string;
        }

        private static string PrivatePath(object source, string fieldName)
        {
            FieldInfo field = source?.GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            return field?.GetValue(source) as string;
        }
    }
    [StaticConstructorOnStartup]
    public static class ProduceMaskRenderer
    {
        private sealed class SourcePixels
        {
            public int width;
            public int height;
            public Color32[] pixels;
        }

        private sealed class CachedGraphic
        {
            public string plantDefName;
            public Texture2D texture;
            public Graphic graphic;
            public Material material;
            public Vector2 drawSize;
        }

        private sealed class Graphic_StyledProduce : Graphic
        {
            private readonly Material material;
            private readonly Vector2 sourceDrawSize;
            private readonly PlantVisualParameters visual;

            public Graphic_StyledProduce(Material material, Vector2 sourceDrawSize, PlantVisualParameters visual)
            {
                this.material = material;
                this.sourceDrawSize = sourceDrawSize;
                this.visual = visual;
                drawSize = new Vector2(
                    sourceDrawSize.x * visual.scale * visual.width,
                    sourceDrawSize.y * visual.scale * visual.height);
            }

            public override Material MatSingle => material;

            public override Material MatAt(Rot4 rot, Thing thing = null)
            {
                return material;
            }

            public override void DrawWorker(Vector3 loc, Rot4 rot, ThingDef thingDef, Thing thing, float extraRotation)
            {
                DrawStyledGraphic(thing, loc, extraRotation, material, sourceDrawSize, visual);
            }
        }
        private static readonly Vector2[] MeshOffsets =
        {
            Vector2.zero, new Vector2(-0.18f, 0.13f), new Vector2(0.19f, 0.11f),
            new Vector2(-0.15f, -0.16f), new Vector2(0.17f, -0.15f)
        };
        private static readonly Dictionary<int, SourcePixels> SourceCache = new Dictionary<int, SourcePixels>();
        private static readonly Dictionary<string, CachedGraphic> GraphicCache = new Dictionary<string, CachedGraphic>();
        private static readonly Dictionary<string, List<ProduceColorStyle>> StyleCache = new Dictionary<string, List<ProduceColorStyle>>();
        private static readonly Dictionary<string, PlantVisualParameters> VisualCache = new Dictionary<string, PlantVisualParameters>();
        private static readonly Dictionary<string, Material> EffectMaterialCache = new Dictionary<string, Material>();
        private const int MaxCachedGraphics = 256;

        public static bool HasActiveMasks(ThingDef plantDef)
        {
            if (plantDef == null || HorticultureNovelSeedsMod.Settings?.enableProduceVisuals == false) return false;
            return HorticultureNovelSeedsMod.Settings?.GetPlantSettings(plantDef, false)?.HasActiveProduceMasks == true;
        }

        public static bool NeedsCustomDraw(ThingDef plantDef, IEnumerable<VarietyTraitDef> inheritedTraits)
        {
            return plantDef != null && HorticultureNovelSeedsMod.Settings?.enableProduceVisuals != false
                && NovelSeedUtility.HasProduceVisual(plantDef, inheritedTraits);
        }

        public static bool TryGetReplacementGraphic(Thing parent, ThingDef plantDef, IEnumerable<VarietyTraitDef> inheritedTraits,
            string styleKey, Graphic original, out Graphic replacement)
        {
            replacement = original;
            if (!TryGetGraphic(parent, plantDef, inheritedTraits, styleKey, original, out CachedGraphic cached, out _)) return false;
            replacement = cached.graphic;
            return replacement != null;
        }

        private static void DrawStyledGraphic(Thing parent, Vector3 drawLoc, float extraRotation, Material material,
            Vector2 sourceDrawSize, PlantVisualParameters visual, bool flip = false)
        {
            if (parent == null || material == null) return;
            float graphicSize = Mathf.Max(sourceDrawSize.x * visual.scale * visual.width,
                sourceDrawSize.y * visual.scale * visual.height);
            if (visual.radiance > 0f) DrawEffect(drawLoc, graphicSize, visual.radiance, visual.radianceScale,
                visual.RadianceColor, false, visual.offsetX, visual.offsetZ);
            if (visual.gloom > 0f) DrawEffect(drawLoc, graphicSize, visual.gloom, visual.gloomScale,
                visual.GloomColor, true, visual.offsetX, visual.offsetZ);

            int count = Mathf.Clamp(1 + Mathf.RoundToInt((visual.density - 1f) * 4f), 1, MeshOffsets.Length);
            int seed = parent.thingIDNumber;
            for (int i = 0; i < count; i++)
            {
                float variation = PlantVisualUtility.MeshScaleFactor(seed, i, visual.scaleVariation);
                Vector2 size = new Vector2(sourceDrawSize.x * visual.scale * visual.width * variation,
                    sourceDrawSize.y * visual.scale * visual.height * variation);
                Vector2 spreadOffset = MeshOffsets[i] * visual.spread;
                Vector3 location = drawLoc;
                location.x += visual.offsetX + spreadOffset.x;
                location.z += visual.offsetZ + spreadOffset.y;
                location.y += i * 0.0001f;
                float angle = extraRotation + PlantVisualUtility.MeshRotation(seed, i, visual.rotation, visual.rotationVariation);
                Graphics.DrawMesh(MeshPool.GridPlane(size, flip), location, Quaternion.AngleAxis(angle, Vector3.up), material, 0);
            }

            if (visual.overlayPattern > 0) DrawOverlay(drawLoc, graphicSize, visual);
        }

        private static bool TryGetGraphic(Thing parent, ThingDef plantDef, IEnumerable<VarietyTraitDef> inheritedTraits,
            string styleKey, Graphic baseGraphic, out CachedGraphic cached, out PlantVisualParameters visual)
        {
            cached = null;
            visual = PlantVisualParameters.Default;
            if (parent == null || !NeedsCustomDraw(plantDef, inheritedTraits)) return false;
            Graphic drawGraphic = baseGraphic is Graphic_StackCount stackGraphic ? stackGraphic.SubGraphicFor(parent) : baseGraphic;
            Material sourceMaterial = drawGraphic?.MatAt(parent.Rotation, parent);
            Texture2D source = sourceMaterial?.mainTexture as Texture2D;
            if (source == null) return false;

            List<VisualMaskLayerRecord> layers = null;
            if (HasActiveMasks(plantDef))
            {
                layers = HorticultureNovelSeedsMod.Settings.GetPlantSettings(plantDef, false)?.ProduceMaskLayers;
                if (layers.NullOrEmpty() || !layers.Any(layer => layer.HasPixels)) return false;
            }
            List<VarietyTraitDef> traits = inheritedTraits as List<VarietyTraitDef>
                ?? inheritedTraits?.Where(trait => trait != null).ToList() ?? new List<VarietyTraitDef>();
            if (styleKey.NullOrEmpty()) styleKey = plantDef.defName + "|" + string.Join(",", traits.Select(trait => trait.defName));
            if (!StyleCache.TryGetValue(styleKey, out List<ProduceColorStyle> styles))
            {
                styles = NovelSeedUtility.ResolveProduceColorStyles(plantDef, traits);
                StyleCache[styleKey] = styles;
            }
            if (!VisualCache.TryGetValue(styleKey, out visual))
            {
                visual = NovelSeedUtility.ResolveProduceVisualParameters(plantDef, traits);
                VisualCache[styleKey] = visual;
            }

            string key = BuildKey(plantDef, source, styles, layers, styleKey);
            if (GraphicCache.TryGetValue(key, out cached)) return cached.material != null;
            cached = CreateGraphic(plantDef, source, sourceMaterial, drawGraphic.drawSize, styles, layers, visual);
            if (cached?.material == null) return false;
            if (GraphicCache.Count >= MaxCachedGraphics) ClearAll();
            GraphicCache[key] = cached;
            return true;
        }

        public static void Invalidate(ThingDef plantDef)
        {
            if (plantDef == null) return;
            foreach (string key in GraphicCache.Where(pair => pair.Value.plantDefName == plantDef.defName).Select(pair => pair.Key).ToList())
            {
                DestroyEntry(GraphicCache[key]);
                GraphicCache.Remove(key);
            }
            string prefix = plantDef.defName + "|";
            foreach (string key in StyleCache.Keys.Where(key => key.StartsWith(prefix)).ToList()) StyleCache.Remove(key);
            foreach (string key in VisualCache.Keys.Where(key => key.StartsWith(prefix)).ToList()) VisualCache.Remove(key);
        }

        public static void ClearAll()
        {
            foreach (CachedGraphic entry in GraphicCache.Values) DestroyEntry(entry);
            GraphicCache.Clear();
            StyleCache.Clear();
            VisualCache.Clear();
        }

        private static CachedGraphic CreateGraphic(ThingDef plantDef, Texture2D source, Material sourceMaterial, Vector2 drawSize,
            IReadOnlyList<ProduceColorStyle> styles, List<VisualMaskLayerRecord> layers, PlantVisualParameters visual)
        {
            SourcePixels sourcePixels = ReadSource(source);
            if (sourcePixels == null) return null;
            Color32[] result = new Color32[sourcePixels.pixels.Length];
            for (int y = 0; y < sourcePixels.height; y++)
            {
                int maskY = VisualMaskLayerRecord.Resolution - 1 - Mathf.Clamp(y * VisualMaskLayerRecord.Resolution / sourcePixels.height, 0, VisualMaskLayerRecord.Resolution - 1);
                for (int x = 0; x < sourcePixels.width; x++)
                {
                    int index = y * sourcePixels.width + x;
                    Color original = sourcePixels.pixels[index];
                    int maskX = Mathf.Clamp(x * VisualMaskLayerRecord.Resolution / sourcePixels.width, 0, VisualMaskLayerRecord.Resolution - 1);
                    if (layers == null)
                    {
                        ProduceColorStyle style = (styles?.Count ?? 0) > 0 ? styles[0] : ProduceColorStyle.Identity;
                        result[index] = style.Apply(original);
                    }
                    else
                    {
                        int layerIndex = -1;
                        for (int i = 0; i < layers.Count; i++) if (layers[i].IsPainted(maskX, maskY)) { layerIndex = i; break; }
                        result[index] = layerIndex >= 0
                            ? (layerIndex < (styles?.Count ?? 0) ? styles[layerIndex] : ProduceColorStyle.Identity).Apply(original)
                            : original;
                    }
                }
            }

            Texture2D texture = new Texture2D(sourcePixels.width, sourcePixels.height, TextureFormat.RGBA32, false)
            {
                name = "HNS_ProduceMask_" + plantDef.defName,
                filterMode = source.filterMode,
                wrapMode = TextureWrapMode.Clamp
            };
            texture.SetPixels32(result);
            texture.Apply(false, true);
            int renderQueue = sourceMaterial?.renderQueue ?? 0;
            Graphic bakedGraphic = GraphicDatabase.Get<Graphic_Single>(texture, ShaderDatabase.Cutout, drawSize, Color.white, renderQueue);
            Material material = bakedGraphic?.MatSingle;
            Graphic graphic = material == null ? null : new Graphic_StyledProduce(material, drawSize, visual);
            return new CachedGraphic
            {
                plantDefName = plantDef.defName,
                texture = texture,
                graphic = graphic,
                material = material,
                drawSize = drawSize
            };
        }

        private static SourcePixels ReadSource(Texture2D source)
        {
            int id = source.GetInstanceID();
            if (SourceCache.TryGetValue(id, out SourcePixels cached)) return cached;
            PlantMaskUtility.BakedTextureSize(source.width, source.height, out int width, out int height);
            RenderTexture temporary = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
            RenderTexture previous = RenderTexture.active;
            try
            {
                Graphics.Blit(source, temporary);
                RenderTexture.active = temporary;
                Texture2D readable = new Texture2D(width, height, TextureFormat.RGBA32, false);
                readable.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                readable.Apply();
                cached = new SourcePixels { width = width, height = height, pixels = readable.GetPixels32() };
                UnityEngine.Object.Destroy(readable);
                SourceCache[id] = cached;
                return cached;
            }
            catch (Exception exception)
            {
                Log.Warning("[Horticulture - Novel Seeds] Could not read produce texture " + source.name + " for masking: " + exception.Message);
                return null;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(temporary);
            }
        }

        private static string BuildKey(ThingDef plantDef, Texture2D source, IReadOnlyList<ProduceColorStyle> styles, List<VisualMaskLayerRecord> layers, string traitKey)
        {
            string colorKey = string.Join(",", styles?.Select(style => style.ContentHash.ToString()) ?? Enumerable.Empty<string>());
            string maskKey = layers == null ? "none" : string.Join(",", layers.Select(layer => layer.ContentHash.ToString()));
            return traitKey + "|" + source.GetInstanceID() + "|" + colorKey + "|" + maskKey;
        }

        private static void DrawEffect(Vector3 drawLoc, float graphicSize, float strength, float scale, Color color, bool dense, float offsetX, float offsetZ)
        {
            string key = "effect|" + dense + "|" + QuantizedColor(color) + "|" + Mathf.RoundToInt(strength * 1000f);
            if (!EffectMaterialCache.TryGetValue(key, out Material material))
            {
                Texture2D texture = CreateRadialTexture(64, color, strength, dense);
                material = MaterialPool.MatFrom(texture, ShaderDatabase.MoteGlow, Color.white);
                EffectMaterialCache[key] = material;
            }
            float size = Mathf.Max(0.65f, graphicSize * (dense ? 1.5f : 1.4f) * scale);
            Vector3 center = drawLoc.WithYOffset(-Altitudes.AltInc / 2f);
            center.x += offsetX; center.z += offsetZ;
            Graphics.DrawMesh(MeshPool.GridPlane(new Vector2(size, size)), center, Quaternion.identity, material, 0);
        }

        private static void DrawOverlay(Vector3 drawLoc, float graphicSize, PlantVisualParameters visual)
        {
            string key = "overlay|" + visual.overlayPattern + "|" + QuantizedColor(visual.OverlayColor) + "|" + Mathf.RoundToInt(visual.overlayIntensity * 1000f);
            if (!EffectMaterialCache.TryGetValue(key, out Material material))
            {
                Texture2D texture = CreateOverlayTexture(64, visual.overlayPattern, visual.overlayIntensity, visual.OverlayColor);
                material = MaterialPool.MatFrom(texture, ShaderDatabase.TransparentPostLight, Color.white);
                EffectMaterialCache[key] = material;
            }
            float size = Mathf.Max(0.55f, graphicSize * 1.22f * visual.overlayScale);
            Vector3 center = drawLoc.WithYOffset(Altitudes.AltInc / 3f);
            center.x += visual.offsetX; center.z += visual.offsetZ;
            Graphics.DrawMesh(MeshPool.GridPlane(new Vector2(size, size)), center, Quaternion.AngleAxis(visual.rotation, Vector3.up), material, 0);
        }

        private static Texture2D CreateRadialTexture(int size, Color color, float strength, bool dense)
        {
            Texture2D texture = NewEffectTexture(size, "HNS_ProduceEffect");
            Color32[] pixels = new Color32[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float dx = ((x + 0.5f) / size) * 2f - 1f;
                float dy = ((y + 0.5f) / size) * 2f - 1f;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                float halo = Mathf.Pow(Mathf.Clamp01(1f - distance), dense ? 1.15f : 1.35f);
                float rays = dense ? 0f : Mathf.Pow(Mathf.Abs(Mathf.Cos(Mathf.Atan2(dy, dx) * 6f)), 18f) * Mathf.Clamp01(1f - distance * 0.8f) * 0.1f;
                Color pixel = color;
                pixel.a = Mathf.Clamp01((halo * (dense ? 0.7f : 0.55f) + rays) * strength);
                pixels[y * size + x] = pixel;
            }
            texture.SetPixels32(pixels); texture.Apply(true, true); return texture;
        }

        private static Texture2D CreateOverlayTexture(int size, int pattern, float intensity, Color color)
        {
            Texture2D texture = NewEffectTexture(size, "HNS_ProduceOverlay_" + pattern);
            Color32[] pixels = new Color32[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float nx = ((x + 0.5f) / size) * 2f - 1f;
                float ny = ((y + 0.5f) / size) * 2f - 1f;
                float distance = Mathf.Sqrt(nx * nx + ny * ny);
                float angle = Mathf.Atan2(ny, nx);
                float alpha = OverlayAlpha(pattern, x, y, nx, ny, distance, angle);
                Color pixel = color; pixel.a = Mathf.Clamp01(alpha * intensity);
                pixels[y * size + x] = pixel;
            }
            texture.SetPixels32(pixels); texture.Apply(true, true); return texture;
        }

        private static float OverlayAlpha(int pattern, int x, int y, float nx, float ny, float distance, float angle)
        {
            if (distance > 0.94f) return 0f;
            switch (pattern)
            {
                case 1:
                    float ray = Mathf.Pow(Mathf.Abs(Mathf.Cos(angle * 8f)), 22f);
                    return distance > 0.32f && distance < 0.42f + ray * 0.5f ? 0.78f : 0f;
                case 2:
                    float spots = Mathf.Sin(x * 0.73f + y * 1.37f) * Mathf.Sin(x * 1.61f - y * 0.51f);
                    return spots > 0.72f ? 0.72f : 0f;
                case 3: return Mathf.Abs(Mathf.Sin((nx + ny * 0.35f) * 22f)) > 0.82f ? 0.55f * Mathf.Clamp01(1f - distance) : 0f;
                case 4:
                    float vein = Mathf.Abs(Mathf.Sin(angle * 7f + distance * 10f));
                    return vein > 0.92f ? 0.68f * Mathf.Clamp01(1.1f - distance) : 0f;
                case 5:
                    int hash = unchecked(x * 73856093 ^ y * 19349663);
                    return (hash & 31) < 4 ? 0.72f : 0f;
                default: return 0f;
            }
        }

        private static Texture2D NewEffectTexture(int size, string name)
        {
            return new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
        }

        private static string QuantizedColor(Color color)
        {
            return Mathf.RoundToInt(color.r * 255f) + "," + Mathf.RoundToInt(color.g * 255f) + "," + Mathf.RoundToInt(color.b * 255f);
        }

        private static void DestroyEntry(CachedGraphic entry)
        {
            if (entry?.texture != null) UnityEngine.Object.Destroy(entry.texture);
        }
    }

    public sealed class Dialog_PlantMasks : Window
    {
        private sealed class MaskHistoryEntry
        {
            public int page;
            public int variation;
            public List<VisualMaskLayerRecord> layers;
        }

        private const int MaxHistoryEntries = 40;
        private readonly ThingDef plantDef;
        private readonly PlantSettingsRecord settings;
        private int selectedPage;
        private int selectedLayer;
        private int selectedVariation;
        private readonly int variationCount;
        private bool erase;
        private bool magicWand;
        private float magicWandTolerance = 0.12f;
        private int brushSize = 3;
        private float canvasZoom = 1f;
        private Vector2 canvasOffset = Vector2.zero;
        private int lastPaintMaskX = -1;
        private int lastPaintMaskY = -1;
        private int lastPaintContext = -1;
        private readonly List<MaskHistoryEntry> undoHistory = new List<MaskHistoryEntry>();
        private readonly List<MaskHistoryEntry> redoHistory = new List<MaskHistoryEntry>();
        private MaskHistoryEntry pendingStrokeHistory;
        private int magicWandTextureId = -1;
        private Color32[] magicWandPixels;
        private readonly Dictionary<VisualMaskLayerRecord, Texture2D> previewMasks = new Dictionary<VisualMaskLayerRecord, Texture2D>();
        private static readonly Color[] LayerColors =
        {
            new Color(0.95f, 0.22f, 0.68f),
            new Color(0.12f, 0.82f, 0.96f),
            new Color(1f, 0.80f, 0.10f)
        };

        public override Vector2 InitialSize => new Vector2(1080f, 780f);

        public Dialog_PlantMasks(ThingDef plantDef, bool openProducePage = false)
        {
            this.plantDef = plantDef;
            settings = HorticultureNovelSeedsMod.Settings.GetPlantSettings(plantDef);
            settings.Normalize();
            variationCount = PlantMaskUtility.VariationCount(plantDef);
            settings.EnsurePlantMaskVariationCount(variationCount);
            selectedPage = openProducePage ? 1 : 0;
            doCloseX = true;
            closeOnClickedOutside = false;
            absorbInputAroundWindow = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            HandleHistoryShortcuts();
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, inRect.width, 32f), "Masks - " + plantDef.LabelCap);
            Text.Font = GameFont.Small;
            DrawTabs(new Rect(0f, 38f, 420f, 32f));

            bool enabled = CurrentEnabled;
            Widgets.CheckboxLabeled(new Rect(0f, 80f, 210f, 28f), "Use " + PageName + " Masks", ref enabled);
            if (enabled != CurrentEnabled)
            {
                if (selectedPage == 0) settings.usePlantMasks = enabled;
                else settings.useProduceMasks = enabled;
                Changed();
            }
            if (selectedPage == 0 && variationCount > 1) DrawVariationSelector(new Rect(230f, 80f, 420f, 30f));
            Texture texture = selectedPage == 0 ? PlantMaskUtility.TextureForVariation(plantDef, selectedVariation) : plantDef.plant?.harvestedThingDef?.uiIcon;
            string subject = selectedPage == 0 ? plantDef.LabelCap.ToString() : plantDef.plant?.harvestedThingDef?.LabelCap.ToString() ?? "No harvested produce";
            Widgets.Label(new Rect(670f, 84f, inRect.width - 670f, 24f), subject);

            Rect layerPanel = new Rect(0f, 120f, 250f, inRect.height - 168f);
            Rect canvasPanel = new Rect(266f, 120f, 450f, inRect.height - 168f);
            Rect controls = new Rect(732f, 120f, inRect.width - 732f, inRect.height - 168f);
            Widgets.DrawMenuSection(layerPanel);
            Widgets.DrawMenuSection(canvasPanel);
            Widgets.DrawMenuSection(controls);
            DrawLayerPanel(layerPanel.ContractedBy(10f));
            DrawCanvas(canvasPanel.ContractedBy(12f), texture);
            DrawControls(controls.ContractedBy(12f));

            if (Widgets.ButtonText(new Rect(inRect.xMax - 110f, inRect.yMax - 36f, 110f, 30f), "Close")) Close();
        }

        public override void PostClose()
        {
            base.PostClose();
            DestroyPreviews();
            ClearMagicWandCache();
            settings.Normalize();
            ProduceMaskRenderer.Invalidate(plantDef);
            HorticultureNovelSeedsMod.Settings.Write();
        }

        private List<VisualMaskLayerRecord> CurrentLayers => selectedPage == 0 ? settings.PlantMaskLayersForVariation(selectedVariation) : settings.ProduceMaskLayers;
        private VisualMaskLayerRecord Selected => CurrentLayers[Mathf.Clamp(selectedLayer, 0, 2)];
        private bool CurrentEnabled => selectedPage == 0 ? settings.usePlantMasks : settings.useProduceMasks;
        private string PageName => selectedPage == 0 ? "Plant" : "Produce";

        private void DrawTabs(Rect rect)
        {
            string[] labels = { "Plant", "Produce" };
            for (int i = 0; i < labels.Length; i++)
            {
                Rect tab = new Rect(rect.x + i * 208f, rect.y, 200f, rect.height);
                if (i == selectedPage) Widgets.DrawHighlightSelected(tab);
                if (Widgets.ButtonText(tab, labels[i]))
                {
                    selectedPage = i;
                    selectedLayer = 0;
                    selectedVariation = 0;
                    ClearMagicWandCache();
                    ResetCanvasView();
                }
            }
        }

        private void DrawVariationSelector(Rect rect)
        {
            Widgets.Label(new Rect(rect.x, rect.y + 5f, 78f, 24f), "Variation");
            if (Widgets.ButtonText(new Rect(rect.x + 82f, rect.y, 190f, 30f), PlantMaskUtility.VariationLabel(plantDef, selectedVariation)))
            {
                List<FloatMenuOption> options = new List<FloatMenuOption>();
                for (int i = 0; i < variationCount; i++)
                {
                    int variation = i;
                    options.Add(new FloatMenuOption(PlantMaskUtility.VariationLabel(plantDef, i), delegate
                    {
                        selectedVariation = variation;
                        selectedLayer = 0;
                        DestroyPreviews();
                        ClearMagicWandCache();
                        ResetCanvasView();
                    }));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }
            if (Widgets.ButtonText(new Rect(rect.x + 280f, rect.y, 64f, 30f), "Prev"))
            {
                selectedVariation = (selectedVariation + variationCount - 1) % variationCount;
                DestroyPreviews();
                ClearMagicWandCache();
                ResetCanvasView();
            }
            if (Widgets.ButtonText(new Rect(rect.x + 350f, rect.y, 64f, 30f), "Next"))
            {
                selectedVariation = (selectedVariation + 1) % variationCount;
                DestroyPreviews();
                ClearMagicWandCache();
                ResetCanvasView();
            }
        }
        private void DrawLayerPanel(Rect rect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(rect.x, rect.y, rect.width, 28f), PageName + " Layers");
            Text.Font = GameFont.Small;
            float y = rect.y + 42f;
            for (int i = 0; i < 3; i++)
            {
                Rect row = new Rect(rect.x, y, rect.width, 42f);
                if (i == selectedLayer) Widgets.DrawHighlightSelected(row);
                else if (Mouse.IsOver(row)) Widgets.DrawHighlight(row);
                if (Widgets.ButtonInvisible(row)) selectedLayer = i;
                Color swatch = LayerColor(i);
                Widgets.DrawBoxSolid(new Rect(row.x + 8f, row.y + 10f, 20f, 20f), swatch);
                Widgets.DrawBox(new Rect(row.x + 8f, row.y + 10f, 20f, 20f));
                Widgets.Label(new Rect(row.x + 38f, row.y + 10f, row.width - 42f, 24f), CurrentLayers[i].name);
                y += 50f;
            }
            Color old = GUI.color;
            GUI.color = Color.gray;
            string note = selectedPage == 0
                ? "Per-mask visuals can style Produce, Leaves, and Stem independently."
                : "Per-mask visuals can style Produce, Leaves, and Container independently.";
            Widgets.Label(new Rect(rect.x, y + 12f, rect.width, 80f), note);
            GUI.color = old;
        }

        private void DrawCanvas(Rect rect, Texture texture)
        {
            Widgets.Label(new Rect(rect.x, rect.y, rect.width - 110f, 24f), PageName + " Preview");
            TextAnchor oldAnchor = Text.Anchor;
            Text.Anchor = TextAnchor.UpperRight;
            Widgets.Label(new Rect(rect.xMax - 110f, rect.y, 110f, 24f), "Zoom " + canvasZoom.ToStringPercent());
            Text.Anchor = oldAnchor;

            Rect stage = new Rect(rect.x, rect.y + 30f, rect.width, rect.height - 72f);
            Widgets.DrawBoxSolid(stage, new Color(0.12f, 0.13f, 0.13f));
            if (texture == null)
            {
                Widgets.Label(stage.ContractedBy(20f), "No texture is available for this page.");
                return;
            }

            Rect baseImageRect = FitRect(stage.ContractedBy(16f), texture.width, texture.height);
            Rect imageRect = ZoomedImageRect(baseImageRect);
            HandleCanvasZoom(stage, baseImageRect, ref imageRect);

            GUI.BeginGroup(stage);
            Rect localImageRect = imageRect;
            localImageRect.position -= stage.position;
            GUI.DrawTexture(localImageRect, texture, ScaleMode.StretchToFill, true);
            for (int i = 0; i < 3; i++)
            {
                VisualMaskLayerRecord layer = CurrentLayers[i];
                if (!layer.HasPixels) continue;
                Color old = GUI.color;
                Color color = LayerColor(i);
                color.a = i == selectedLayer ? 0.58f : 0.30f;
                GUI.color = color;
                GUI.DrawTexture(localImageRect, PreviewTexture(layer), ScaleMode.StretchToFill, true);
                GUI.color = old;
            }
            Widgets.DrawBox(localImageRect);
            GUI.EndGroup();

            HandlePainting(stage, imageRect, texture);
            Widgets.Label(new Rect(rect.x, rect.yMax - 34f, rect.width, 30f), selectedPage == 0 && variationCount > 1 ? "Scroll to zoom. This mask applies only to the selected texture variation." : "Scroll to zoom. Masks follow the displayed texture.");
        }

        private void DrawControls(Rect rect)
        {
            float y = rect.y;
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(rect.x, y, rect.width, 28f), Selected.name + " Mask");
            Text.Font = GameFont.Small;
            y += 42f;
            float toolWidth = (rect.width - 16f) / 3f;
            Rect paintButton = new Rect(rect.x, y, toolWidth, 30f);
            Rect eraseButton = new Rect(paintButton.xMax + 8f, y, toolWidth, 30f);
            Rect wandButton = new Rect(eraseButton.xMax + 8f, y, toolWidth, 30f);
            Widgets.DrawHighlightSelected(magicWand ? wandButton : erase ? eraseButton : paintButton);
            if (Widgets.ButtonText(paintButton, "Paint")) SetTool(false, false);
            if (Widgets.ButtonText(eraseButton, "Erase")) SetTool(true, false);
            if (Widgets.ButtonText(wandButton, "Magic Wand")) SetTool(false, true);
            TooltipHandler.TipRegion(wandButton, "Magic Wand: Assign the connected region of a similar color to this mask layer.");
            y += 44f;
            bool oldEnabled = GUI.enabled;
            GUI.enabled = oldEnabled && undoHistory.Count > 0;
            if (Widgets.ButtonText(new Rect(rect.x, y, halfWidth(rect), 30f), "Undo")) Undo();
            GUI.enabled = oldEnabled && redoHistory.Count > 0;
            if (Widgets.ButtonText(new Rect(rect.x + halfWidth(rect) + 8f, y, halfWidth(rect), 30f), "Redo")) Redo();
            GUI.enabled = oldEnabled;
            y += 44f;
            if (magicWand)
            {
                Widgets.Label(new Rect(rect.x, y, rect.width, 22f), "Color Tolerance: " + magicWandTolerance.ToStringPercent("F0"));
                Rect toleranceSlider = new Rect(rect.x, y + 22f, rect.width, 18f);
                magicWandTolerance = Widgets.HorizontalSlider(toleranceSlider, magicWandTolerance, 0.01f, 0.50f);
                TooltipHandler.TipRegion(toleranceSlider, "Higher tolerance includes a wider range of colors connected to the clicked pixel.");
            }
            else
            {
                Widgets.Label(new Rect(rect.x, y, rect.width, 22f), "Brush Size: " + brushSize + " px");
                brushSize = Mathf.RoundToInt(Widgets.HorizontalSlider(new Rect(rect.x, y + 22f, rect.width, 18f), brushSize, 1f, 25f));
            }
            y += 58f;
            float half = (rect.width - 8f) / 2f;
            if (Widgets.ButtonText(new Rect(rect.x, y, half, 30f), "Clear") && Selected.HasPixels)
            {
                RecordImmediateChange();
                Selected.Clear();
                Changed();
            }
            if (Widgets.ButtonText(new Rect(rect.x + half + 8f, y, half, 30f), "Preview Color")) Find.WindowStack.Add(new Dialog_MaskColorPreview(plantDef, selectedPage == 1, selectedVariation));
        }

        private static float halfWidth(Rect rect) => (rect.width - 8f) / 2f;

        private void SetTool(bool useErase, bool useMagicWand)
        {
            if (erase == useErase && magicWand == useMagicWand) return;
            erase = useErase;
            magicWand = useMagicWand;
            ResetPaintStroke();
        }

        private void HandlePainting(Rect stage, Rect imageRect, Texture texture)
        {
            Event current = Event.current;
            if (current.rawType == EventType.MouseUp && current.button == 0)
            {
                ResetPaintStroke();
                return;
            }
            if (magicWand)
            {
                if (stage.Contains(current.mousePosition) && imageRect.Contains(current.mousePosition) && current.type == EventType.MouseDown && current.button == 0)
                {
                    ApplyMagicWand(texture, imageRect, current.mousePosition);
                    current.Use();
                }
                return;
            }
            if (!stage.Contains(current.mousePosition) || !imageRect.Contains(current.mousePosition) || (current.type != EventType.MouseDown && current.type != EventType.MouseDrag) || current.button != 0) return;
            int maskX = Mathf.Clamp(Mathf.FloorToInt((current.mousePosition.x - imageRect.x) / imageRect.width * VisualMaskLayerRecord.Resolution), 0, VisualMaskLayerRecord.Resolution - 1);
            int maskY = Mathf.Clamp(Mathf.FloorToInt((current.mousePosition.y - imageRect.y) / imageRect.height * VisualMaskLayerRecord.Resolution), 0, VisualMaskLayerRecord.Resolution - 1);
            int context = (((selectedPage * 31 + selectedVariation) * 31 + selectedLayer) * 2) + (erase ? 1 : 0);
            if (current.type == EventType.MouseDown || context != lastPaintContext)
            {
                ResetPaintStroke();
                pendingStrokeHistory = CaptureHistory(selectedPage, selectedVariation);
            }
            int startX = lastPaintMaskX >= 0 ? lastPaintMaskX : maskX;
            int startY = lastPaintMaskY >= 0 ? lastPaintMaskY : maskY;
            int steps = Mathf.Max(Mathf.Abs(maskX - startX), Mathf.Abs(maskY - startY));
            bool changed = false;
            for (int step = 0; step <= steps; step++)
            {
                float progress = steps == 0 ? 1f : step / (float)steps;
                int paintX = Mathf.RoundToInt(Mathf.Lerp(startX, maskX, progress));
                int paintY = Mathf.RoundToInt(Mathf.Lerp(startY, maskY, progress));
                changed |= Selected.PaintCircle(paintX, paintY, brushSize, !erase);
                if (!erase)
                    for (int i = 0; i < CurrentLayers.Count; i++)
                        if (i != selectedLayer) changed |= CurrentLayers[i].PaintCircle(paintX, paintY, brushSize, false);
            }
            lastPaintMaskX = maskX;
            lastPaintMaskY = maskY;
            lastPaintContext = context;
            if (changed)
            {
                CommitStrokeHistory();
                Changed();
            }
            current.Use();
        }

        private void ApplyMagicWand(Texture texture, Rect imageRect, Vector2 mousePosition)
        {
            if (!EnsureMagicWandPixels(texture)) return;
            int resolution = VisualMaskLayerRecord.Resolution;
            int seedX = Mathf.Clamp(Mathf.FloorToInt((mousePosition.x - imageRect.x) / imageRect.width * resolution), 0, resolution - 1);
            int seedTopY = Mathf.Clamp(Mathf.FloorToInt((mousePosition.y - imageRect.y) / imageRect.height * resolution), 0, resolution - 1);
            int seedY = resolution - 1 - seedTopY;
            Color32 seed = magicWandPixels[seedY * resolution + seedX];
            if (seed.a < 16) return;

            bool[] visited = new bool[resolution * resolution];
            Queue<int> pending = new Queue<int>();
            List<int> region = new List<int>();
            int seedIndex = seedY * resolution + seedX;
            visited[seedIndex] = true;
            pending.Enqueue(seedIndex);
            while (pending.Count > 0)
            {
                int index = pending.Dequeue();
                Color32 candidate = magicWandPixels[index];
                if (candidate.a < 16 || ColorDistance(seed, candidate) > magicWandTolerance) continue;
                region.Add(index);
                int x = index % resolution;
                int y = index / resolution;
                EnqueueWandNeighbor(x - 1, y, resolution, visited, pending);
                EnqueueWandNeighbor(x + 1, y, resolution, visited, pending);
                EnqueueWandNeighbor(x, y - 1, resolution, visited, pending);
                EnqueueWandNeighbor(x, y + 1, resolution, visited, pending);
            }
            if (region.Count == 0) return;

            MaskHistoryEntry before = CaptureHistory(selectedPage, selectedVariation);
            bool changed = false;
            foreach (int index in region)
            {
                int x = index % resolution;
                int topY = resolution - 1 - index / resolution;
                changed |= Selected.PaintPixel(x, topY, true);
                for (int i = 0; i < CurrentLayers.Count; i++)
                    if (i != selectedLayer) changed |= CurrentLayers[i].PaintPixel(x, topY, false);
            }
            if (!changed) return;
            AddHistory(undoHistory, before);
            redoHistory.Clear();
            ResetPaintStroke();
            Changed();
        }

        private bool EnsureMagicWandPixels(Texture texture)
        {
            if (texture == null) return false;
            int textureId = texture.GetInstanceID();
            int resolution = VisualMaskLayerRecord.Resolution;
            if (magicWandTextureId == textureId && magicWandPixels?.Length == resolution * resolution) return true;

            RenderTexture previous = RenderTexture.active;
            RenderTexture temporary = RenderTexture.GetTemporary(resolution, resolution, 0, RenderTextureFormat.ARGB32);
            Texture2D readable = null;
            try
            {
                Graphics.Blit(texture, temporary);
                RenderTexture.active = temporary;
                readable = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
                readable.ReadPixels(new Rect(0f, 0f, resolution, resolution), 0, 0, false);
                readable.Apply(false, false);
                magicWandPixels = readable.GetPixels32();
                magicWandTextureId = textureId;
                return true;
            }
            catch (Exception exception)
            {
                Log.Warning("[Horticulture - Novel Seeds] Magic Wand could not read the selected texture: " + exception.Message);
                ClearMagicWandCache();
                return false;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(temporary);
                if (readable != null) UnityEngine.Object.Destroy(readable);
            }
        }

        private static void EnqueueWandNeighbor(int x, int y, int resolution, bool[] visited, Queue<int> pending)
        {
            if (x < 0 || x >= resolution || y < 0 || y >= resolution) return;
            int index = y * resolution + x;
            if (visited[index]) return;
            visited[index] = true;
            pending.Enqueue(index);
        }

        private static float ColorDistance(Color32 first, Color32 second)
        {
            float red = (first.r - second.r) / 255f;
            float green = (first.g - second.g) / 255f;
            float blue = (first.b - second.b) / 255f;
            return Mathf.Sqrt(red * red + green * green + blue * blue) / 1.7320508f;
        }

        private void ClearMagicWandCache()
        {
            magicWandTextureId = -1;
            magicWandPixels = null;
        }

        private void ResetPaintStroke()
        {
            lastPaintMaskX = -1;
            lastPaintMaskY = -1;
            lastPaintContext = -1;
            pendingStrokeHistory = null;
        }

        private void HandleHistoryShortcuts()
        {
            Event current = Event.current;
            if (current.type != EventType.KeyDown || !current.control) return;
            if (current.keyCode == KeyCode.Z && undoHistory.Count > 0)
            {
                Undo();
                current.Use();
            }
            else if (current.keyCode == KeyCode.Y && redoHistory.Count > 0)
            {
                Redo();
                current.Use();
            }
        }

        private MaskHistoryEntry CaptureHistory(int page, int variation)
        {
            List<VisualMaskLayerRecord> layers = page == 0
                ? settings.PlantMaskLayersForVariation(variation)
                : settings.ProduceMaskLayers;
            return new MaskHistoryEntry
            {
                page = page,
                variation = variation,
                layers = layers.Select(layer => layer.Clone()).ToList()
            };
        }

        private void CommitStrokeHistory()
        {
            if (pendingStrokeHistory == null) return;
            AddHistory(undoHistory, pendingStrokeHistory);
            redoHistory.Clear();
            pendingStrokeHistory = null;
        }

        private void RecordImmediateChange()
        {
            AddHistory(undoHistory, CaptureHistory(selectedPage, selectedVariation));
            redoHistory.Clear();
            ResetPaintStroke();
        }

        private static void AddHistory(List<MaskHistoryEntry> history, MaskHistoryEntry entry)
        {
            if (entry == null) return;
            history.Add(entry);
            if (history.Count > MaxHistoryEntries) history.RemoveAt(0);
        }

        private void Undo()
        {
            if (undoHistory.Count == 0) return;
            MaskHistoryEntry target = undoHistory[undoHistory.Count - 1];
            undoHistory.RemoveAt(undoHistory.Count - 1);
            AddHistory(redoHistory, CaptureHistory(target.page, target.variation));
            ApplyHistory(target);
        }

        private void Redo()
        {
            if (redoHistory.Count == 0) return;
            MaskHistoryEntry target = redoHistory[redoHistory.Count - 1];
            redoHistory.RemoveAt(redoHistory.Count - 1);
            AddHistory(undoHistory, CaptureHistory(target.page, target.variation));
            ApplyHistory(target);
        }

        private void ApplyHistory(MaskHistoryEntry entry)
        {
            selectedPage = entry.page;
            selectedVariation = Mathf.Clamp(entry.variation, 0, Mathf.Max(0, variationCount - 1));
            List<VisualMaskLayerRecord> destination = selectedPage == 0
                ? settings.PlantMaskLayersForVariation(selectedVariation)
                : settings.ProduceMaskLayers;
            destination.Clear();
            destination.AddRange(entry.layers.Select(layer => layer.Clone()));
            ResetPaintStroke();
            Changed();
        }

        private Rect ZoomedImageRect(Rect baseImageRect)
        {
            Vector2 size = baseImageRect.size * canvasZoom;
            return new Rect(baseImageRect.center.x - size.x * 0.5f + canvasOffset.x, baseImageRect.center.y - size.y * 0.5f + canvasOffset.y, size.x, size.y);
        }

        private void HandleCanvasZoom(Rect stage, Rect baseImageRect, ref Rect imageRect)
        {
            Event current = Event.current;
            if (current.type != EventType.ScrollWheel || !stage.Contains(current.mousePosition)) return;

            float nextZoom = Mathf.Clamp(canvasZoom * Mathf.Pow(1.15f, -current.delta.y), 0.5f, 12f);
            if (!Mathf.Approximately(nextZoom, canvasZoom))
            {
                Vector2 anchor = imageRect.Contains(current.mousePosition)
                    ? new Vector2(Mathf.InverseLerp(imageRect.xMin, imageRect.xMax, current.mousePosition.x), Mathf.InverseLerp(imageRect.yMin, imageRect.yMax, current.mousePosition.y))
                    : new Vector2(0.5f, 0.5f);
                Vector2 nextSize = baseImageRect.size * nextZoom;
                Vector2 anchoredPosition = current.mousePosition - Vector2.Scale(anchor, nextSize);
                Vector2 centeredPosition = baseImageRect.center - nextSize * 0.5f;
                canvasZoom = nextZoom;
                canvasOffset = anchoredPosition - centeredPosition;
                if (canvasZoom <= 1f) canvasOffset = Vector2.zero;
                imageRect = ZoomedImageRect(baseImageRect);
            }
            current.Use();
        }

        private void ResetCanvasView()
        {
            canvasZoom = 1f;
            canvasOffset = Vector2.zero;
            ResetPaintStroke();
        }

        private static Color LayerColor(int index) => LayerColors[Mathf.Clamp(index, 0, 2)];
        private Texture2D PreviewTexture(VisualMaskLayerRecord layer)
        {
            if (previewMasks.TryGetValue(layer, out Texture2D texture) && texture != null) return texture;
            texture = new Texture2D(VisualMaskLayerRecord.Resolution, VisualMaskLayerRecord.Resolution, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            Color32[] pixels = new Color32[VisualMaskLayerRecord.Resolution * VisualMaskLayerRecord.Resolution];
            for (int y = 0; y < VisualMaskLayerRecord.Resolution; y++)
                for (int x = 0; x < VisualMaskLayerRecord.Resolution; x++)
                    pixels[(VisualMaskLayerRecord.Resolution - 1 - y) * VisualMaskLayerRecord.Resolution + x] = layer.IsPainted(x, y) ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            previewMasks[layer] = texture;
            return texture;
        }

        private void Changed()
        {
            DestroyPreviews();
            ProduceMaskRenderer.Invalidate(plantDef);
        }

        private void DestroyPreviews()
        {
            foreach (Texture2D texture in previewMasks.Values) if (texture != null) UnityEngine.Object.Destroy(texture);
            previewMasks.Clear();
        }

        private static Rect FitRect(Rect bounds, float width, float height)
        {
            if (width <= 0f || height <= 0f) return bounds;
            float scale = Mathf.Min(bounds.width / width, bounds.height / height);
            Vector2 size = new Vector2(width * scale, height * scale);
            return new Rect(bounds.center.x - size.x / 2f, bounds.center.y - size.y / 2f, size.x, size.y);
        }
    }
    public sealed class Dialog_MaskColorPreview : Window
    {
        private readonly ThingDef plantDef;
        private readonly bool producePage;
        private readonly int variationIndex;
        private readonly PlantSettingsRecord settings;
        private readonly Color[] previewColors = new Color[3];
        private Texture2D previewTexture;

        public override Vector2 InitialSize => new Vector2(680f, 720f);

        public Dialog_MaskColorPreview(ThingDef plantDef, bool producePage, int variationIndex = 0)
        {
            this.plantDef = plantDef;
            this.producePage = producePage;
            this.variationIndex = Mathf.Clamp(variationIndex, 0, PlantMaskUtility.VariationCount(plantDef) - 1);
            settings = HorticultureNovelSeedsMod.Settings.GetPlantSettings(plantDef);
            RandomizeColors();
            doCloseX = true;
            closeOnClickedOutside = false;
            absorbInputAroundWindow = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            string pageName = producePage ? "Produce" : "Plant";
            Text.Font = GameFont.Medium;
            string variationSuffix = !producePage && PlantMaskUtility.VariationCount(plantDef) > 1 ? " (" + PlantMaskUtility.VariationLabel(plantDef, variationIndex) + ")" : string.Empty;
            Widgets.Label(new Rect(0f, 0f, inRect.width, 32f), pageName + " Color Preview - " + plantDef.LabelCap + variationSuffix);
            Text.Font = GameFont.Small;

            Rect stage = new Rect(0f, 42f, inRect.width, inRect.height - 174f);
            Widgets.DrawMenuSection(stage);
            Rect stageInner = stage.ContractedBy(12f);
            Widgets.DrawBoxSolid(stageInner, new Color(0.12f, 0.13f, 0.13f));
            EnsurePreviewTexture();
            if (previewTexture == null)
            {
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(stageInner.ContractedBy(30f), "No texture is available for this preview.");
                Text.Anchor = TextAnchor.UpperLeft;
            }
            else
            {
                Rect imageRect = FitRect(stageInner.ContractedBy(20f), previewTexture.width, previewTexture.height);
                GUI.DrawTexture(imageRect, previewTexture, ScaleMode.StretchToFill, true);
                Widgets.DrawBox(imageRect);
            }

            float legendY = stage.yMax + 12f;
            List<VisualMaskLayerRecord> layers = CurrentLayers;
            float legendWidth = (inRect.width - 16f) / 3f;
            for (int i = 0; i < 3; i++)
            {
                Rect item = new Rect(i * (legendWidth + 8f), legendY, legendWidth, 28f);
                Widgets.DrawBoxSolid(new Rect(item.x, item.y + 4f, 20f, 20f), previewColors[i]);
                Widgets.DrawBox(new Rect(item.x, item.y + 4f, 20f, 20f));
                Widgets.Label(new Rect(item.x + 28f, item.y + 4f, item.width - 28f, 24f), layers[i].name);
            }
            GUI.color = Color.gray;
            Widgets.Label(new Rect(0f, legendY + 34f, inRect.width, 24f), "Random contrasting tints are applied only inside painted masks. Unmasked areas are unchanged.");
            GUI.color = Color.white;

            float buttonY = inRect.yMax - 34f;
            if (Widgets.ButtonText(new Rect(0f, buttonY, 120f, 30f), "New Colors"))
            {
                RandomizeColors();
                DestroyPreviewTexture();
            }
            if (Widgets.ButtonText(new Rect(inRect.xMax - 100f, buttonY, 100f, 30f), "Close")) Close();
        }

        public override void PostClose()
        {
            base.PostClose();
            DestroyPreviewTexture();
        }

        private List<VisualMaskLayerRecord> CurrentLayers => producePage ? settings.ProduceMaskLayers : settings.PlantMaskLayersForVariation(variationIndex);

        private void RandomizeColors()
        {
            System.Random random = new System.Random(Environment.TickCount ^ GetHashCode());
            float baseHue = (float)random.NextDouble();
            for (int i = 0; i < previewColors.Length; i++)
            {
                float hue = Mathf.Repeat(baseHue + i / 3f + ((float)random.NextDouble() - 0.5f) * 0.08f, 1f);
                float saturation = 0.65f + (float)random.NextDouble() * 0.22f;
                float value = 0.86f + (float)random.NextDouble() * 0.14f;
                previewColors[i] = Color.HSVToRGB(hue, saturation, value);
            }
        }

        private void EnsurePreviewTexture()
        {
            if (previewTexture != null) return;
            Texture source = producePage ? plantDef.plant?.harvestedThingDef?.uiIcon : PlantMaskUtility.TextureForVariation(plantDef, variationIndex);
            if (source == null) return;
            PlantMaskUtility.BakedTextureSize(source.width, source.height, out int width, out int height);
            RenderTexture temporary = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
            RenderTexture previous = RenderTexture.active;
            try
            {
                Graphics.Blit(source, temporary);
                RenderTexture.active = temporary;
                Texture2D result = new Texture2D(width, height, TextureFormat.RGBA32, false)
                {
                    name = "HNS_MaskColorPreview_" + plantDef.defName,
                    filterMode = source is Texture2D sourceTexture ? sourceTexture.filterMode : FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };
                result.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                Color32[] pixels = result.GetPixels32();
                List<VisualMaskLayerRecord> layers = CurrentLayers;
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int index = y * width + x;
                        if (pixels[index].a == 0) continue;
                        int layerIndex = PlantMaskUtility.LayerAt(layers, x, y, width, height);
                        if (layerIndex < 0) continue;
                        Color original = pixels[index];
                        Color multiplier = Color.Lerp(Color.white, previewColors[layerIndex], 0.72f);
                        original.r *= multiplier.r;
                        original.g *= multiplier.g;
                        original.b *= multiplier.b;
                        pixels[index] = original;
                    }
                }
                result.SetPixels32(pixels);
                result.Apply(false, true);
                previewTexture = result;
            }
            catch (Exception exception)
            {
                Log.Warning("[Horticulture - Novel Seeds] Could not create mask color preview for " + plantDef.defName + ": " + exception.Message);
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(temporary);
            }
        }

        private void DestroyPreviewTexture()
        {
            if (previewTexture != null) UnityEngine.Object.Destroy(previewTexture);
            previewTexture = null;
        }

        private static Rect FitRect(Rect bounds, float width, float height)
        {
            if (width <= 0f || height <= 0f) return bounds;
            float scale = Mathf.Min(bounds.width / width, bounds.height / height);
            Vector2 size = new Vector2(width * scale, height * scale);
            return new Rect(bounds.center.x - size.x / 2f, bounds.center.y - size.y / 2f, size.x, size.y);
        }
    }
}
