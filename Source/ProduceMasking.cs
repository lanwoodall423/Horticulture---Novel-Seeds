using System;
using System.Collections.Generic;
using System.Globalization;
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

    public static class PlantMaskUtility
    {
        private sealed class TextureVariation
        {
            public Texture texture;
            public string label;
            public string sourcePath;
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

        public static bool HasActiveMasks(ThingDef plantDef, bool allowIdentityGeneration = false)
        {
            if (plantDef == null) return false;
            PlantSettingsRecord settings = HorticultureNovelSeedsMod.Settings?.GetPlantSettings(plantDef, false);
            if (settings?.disableAutoPlantMasks == true) return false;
            for (int variation = 0; variation < VariationCount(plantDef); variation++)
                if (PlantAutoMaskCache.IsRenderable(PlantAutoMaskCache.GetRecord(plantDef, variation, false, allowIdentityGeneration))) return true;
            return false;
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

        public static string TexturePathForVariation(ThingDef plantDef, int variationIndex)
        {
            List<TextureVariation> variations = VariationsFor(plantDef);
            TextureVariation variation = variations[Mathf.Clamp(variationIndex, 0, variations.Count - 1)];
            return variation.sourcePath.NullOrEmpty() ? plantDef?.graphicData?.texPath : variation.sourcePath;
        }

        public static string VariationIdentityFor(ThingDef plantDef, int variationIndex)
        {
            List<TextureVariation> variations = VariationsFor(plantDef);
            TextureVariation variation = variations[Mathf.Clamp(variationIndex, 0, variations.Count - 1)];
            return (variation.label ?? string.Empty) + "|path:" + (variation.sourcePath ?? string.Empty)
                + "|texture:" + (variation.texture?.name ?? string.Empty);
        }

        public static string GraphicIdentityFor(ThingDef plantDef)
        {
            GraphicData data = plantDef?.graphicData;
            if (data == null) return "none";
            return (data.graphicClass?.FullName ?? string.Empty) + "|path:" + (data.texPath ?? string.Empty)
                + "|size:" + data.drawSize.x.ToString("R", CultureInfo.InvariantCulture)
                + "," + data.drawSize.y.ToString("R", CultureInfo.InvariantCulture);
        }

        public static Texture ReferenceTextureForVariation(ThingDef plantDef, int variationIndex, string stateLabel)
        {
            if (plantDef == null || stateLabel.NullOrEmpty()) return null;
            List<TextureVariation> variations = VariationsFor(plantDef);
            List<TextureVariation> candidates = variations.Where(variation => variation.label.Equals(stateLabel, StringComparison.OrdinalIgnoreCase)
                || variation.label.StartsWith(stateLabel + " ", StringComparison.OrdinalIgnoreCase)).ToList();
            if (candidates.Count == 0) return null;
            Texture current = variations[Mathf.Clamp(variationIndex, 0, variations.Count - 1)].texture;
            TextureVariation matchingName = candidates.FirstOrDefault(candidate => candidate.texture != null && current != null
                && candidate.texture.name.Equals(current.name, StringComparison.OrdinalIgnoreCase));
            if (matchingName != null) return matchingName.texture;
            int ordinal = VariationOrdinal(variations[Mathf.Clamp(variationIndex, 0, variations.Count - 1)].label);
            return candidates[Mathf.Clamp(ordinal, 0, candidates.Count - 1)].texture;
        }

        private static int VariationOrdinal(string label)
        {
            if (label.NullOrEmpty()) return 0;
            int marker = label.LastIndexOf(" of ", StringComparison.OrdinalIgnoreCase);
            if (marker < 0) return 0;
            int start = marker - 1;
            while (start >= 0 && char.IsDigit(label[start])) start--;
            return int.TryParse(label.Substring(start + 1, marker - start - 1), out int ordinal) ? Mathf.Max(0, ordinal - 1) : 0;
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

        public static List<VisualMaskLayerRecord> LayersForVariation(ThingDef plantDef, int variationIndex, bool create = false,
            bool allowIdentityGeneration = false)
        {
            PlantSettingsRecord settings = HorticultureNovelSeedsMod.Settings?.GetPlantSettings(plantDef, create);
            if (settings?.disableAutoPlantMasks == true) return null;
            return PlantAutoMaskCache.LayersFor(plantDef, variationIndex, false, allowIdentityGeneration);
        }

        public static bool AnyResolvedLayerHasPixels(ThingDef plantDef, int layerIndex)
        {
            int layer = Mathf.Clamp(layerIndex, 0, 2);
            for (int variation = 0; variation < VariationCount(plantDef); variation++)
            {
                List<VisualMaskLayerRecord> layers = LayersForVariation(plantDef, variation, false);
                if (layers != null && layers.Count > layer && layers[layer].HasPixels) return true;
            }
            return false;
        }

        public static int LayerAt(ThingDef plantDef, int sourceX, int sourceY, int width, int height, int variationIndex = 0)
        {
            List<VisualMaskLayerRecord> layers = LayersForVariation(plantDef, variationIndex, false);
            if (layers == null) return -2;
            return LayerAt(layers, sourceX, sourceY, width, height);
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
            if (!HasActiveMasks(plantDef)) return 0;
            unchecked
            {
                int hash = 486187739;
                int count = VariationCount(plantDef);
                IEnumerable<List<VisualMaskLayerRecord>> variations = variationIndex >= 0
                    ? new[] { LayersForVariation(plantDef, variationIndex, false) }
                    : Enumerable.Range(0, count).Select(index => LayersForVariation(plantDef, index, false));
                foreach (List<VisualMaskLayerRecord> layers in variations)
                {
                    if (layers == null) continue;
                    foreach (VisualMaskLayerRecord layer in layers) hash = hash * 31 + layer.ContentHash;
                }
                return hash;
            }
        }

        private static List<TextureVariation> VariationsFor(ThingDef plantDef)
        {
            if (plantDef != null && TextureVariations.TryGetValue(plantDef, out List<TextureVariation> cached)) return cached;
            List<TextureVariation> result = new List<TextureVariation>();
            GraphicData graphicData = plantDef?.graphicData;
            AddPathVariations(result, graphicData?.texPath, "Normal", IsCollectionGraphic(graphicData?.graphicClass));
            AddPathVariations(result, BloomingPath(plantDef, "bloomGraphicPath"), "Blooming", true);
            AddPathVariations(result, BloomingPath(plantDef, "alternateBloomGraphicPath"), "Alternate Bloom", true);
            AddPathVariations(result, PrivatePath(plantDef?.plant, "leaflessGraphicPath"), "Leafless", true);
            AddPathVariations(result, PrivatePath(plantDef?.plant, "immatureGraphicPath"), "Immature", true);
            AddPathVariations(result, PrivatePath(plantDef?.plant, "leaflessImmatureGraphicPath"), "Leafless Immature", true);
            AddPathVariations(result, PrivatePath(plantDef?.plant, "pollutedGraphicPath"), "Polluted", true);
            if (result.Count == 0) result.Add(new TextureVariation
            {
                texture = plantDef?.uiIcon,
                label = "Normal",
                sourcePath = graphicData?.texPath
            });
            if (plantDef != null) TextureVariations[plantDef] = result;
            return result;
        }

        private static bool IsCollectionGraphic(Type graphicClass)
        {
            return graphicClass != null && typeof(Graphic_Collection).IsAssignableFrom(graphicClass);
        }

        private static void AddPathVariations(List<TextureVariation> result, string path, string stateLabel, bool collection)
        {
            if (path.NullOrEmpty()) return;
            List<Texture2D> textures = collection
                ? ContentFinder<Texture2D>.GetAllInFolder(path).Where(IsSourceTexture).OrderBy(texture => texture.name, StringComparer.Ordinal).ToList()
                : DirectTextures(path);
            if (collection)
                textures = textures.GroupBy(TextureIdentity, StringComparer.Ordinal).Select(group => group.First()).ToList();
            HashSet<int> knownTextureIds = new HashSet<int>();
            foreach (TextureVariation entry in result)
                if (entry.texture != null) knownTextureIds.Add(entry.texture.GetInstanceID());
            textures.RemoveAll(texture => texture == null || !knownTextureIds.Add(texture.GetInstanceID()));

            int total = textures.Count;
            for (int i = 0; i < total; i++)
            {
                result.Add(new TextureVariation
                {
                    texture = textures[i],
                    label = total > 1 ? stateLabel + " " + (i + 1) + " of " + total : stateLabel,
                    sourcePath = collection
                        ? path + "/" + (textures[i]?.name ?? string.Empty)
                        : SourcePathForDirectTexture(path, textures[i])
                });
            }
        }

        private static string SourcePathForDirectTexture(string path, Texture texture)
        {
            if (path.NullOrEmpty() || texture == null) return path;
            string name = texture.name ?? string.Empty;
            foreach (string suffix in new[] { "_north", "_east", "_south", "_west" })
                if (name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) return path + suffix;
            return path;
        }

        private static List<Texture2D> DirectTextures(string path)
        {
            var textures = new List<Texture2D>();
            AddTexture(textures, path);
            foreach (string suffix in new[] { "_north", "_east", "_south", "_west" }) AddTexture(textures, path + suffix);
            return textures;
        }

        private static void AddTexture(List<Texture2D> textures, string path)
        {
            Texture2D texture = ContentFinder<Texture2D>.Get(path, false);
            if (texture != null && IsSourceTexture(texture)) textures.Add(texture);
        }

        private static bool IsSourceTexture(Texture2D texture)
        {
            return texture != null && !texture.name.EndsWith(Graphic_Single.MaskSuffix, StringComparison.OrdinalIgnoreCase);
        }

        private static string TextureIdentity(Texture2D texture)
        {
            string name = texture.name;
            foreach (string suffix in new[] { "_north", "_east", "_south", "_west" })
                if (name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) return name.Substring(0, name.Length - suffix.Length);
            return name;
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

            // Harvested items use whole-item produce color until automatic item targeting is qualified.
            List<VisualMaskLayerRecord> layers = null;
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

}
