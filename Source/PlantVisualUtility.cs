using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace HorticultureNovelSeeds
{
    [StaticConstructorOnStartup]
    public static class PlantVisualUtility
    {
        private const float GridPosRandomnessFactor = 0.30f;
        private static readonly Color32[] WorkingColors = new Color32[4];
        private static readonly Color32[] WhiteColors = { Color.white, Color.white, Color.white, Color.white };
        private static readonly Dictionary<StyledMaterialKey, Material> StyledMaterials = new Dictionary<StyledMaterialKey, Material>();
        private static readonly Dictionary<EffectMaterialKey, Material> EffectMaterials = new Dictionary<EffectMaterialKey, Material>();
        private static readonly Dictionary<OverlayMaterialKey, Material> OverlayMaterials = new Dictionary<OverlayMaterialKey, Material>();

        public static bool PrintScaledPlant(Plant plant, SectionLayer layer, PlantVisualParameters visual)
        {
            if (TryPrintIndependentMaskLayers(plant, layer, visual)) return false;
            Vector3 trueCenter = plant.TrueCenter();
            bool wantSnowOverlay = plant.Position.GetSnowDepth(plant.Map) > 0.8f;
            Rand.PushState();
            Rand.Seed = plant.Position.GetHashCode();
            try
            {
                int meshCount = Mathf.Max(1, Mathf.CeilToInt(plant.Growth * plant.def.plant.maxMeshCount * visual.density));
                float growthSize = plant.def.plant.visualSizeRange.LerpThroughRange(plant.Growth);
                Vector2 graphicSize = plant.def.graphicData.drawSize * (growthSize * visual.scale);
                int[] positions = PlantPosIndices.GetPositionIndices(plant);
                bool clampedBottom = false;
                int positionCount = Mathf.Max(positions.Length, meshCount);
                int visualSeed = plant.Position.GetHashCode();

                for (int i = 0; i < positionCount && i < meshCount; i++)
                {
                    int posIndex = positions[i % positions.Length];
                    float randomScale = MeshScaleFactor(visualSeed, i, visual.scaleVariation);
                    Vector2 planeSize = new Vector2(graphicSize.x * visual.width * randomScale, graphicSize.y * visual.height * randomScale);
                    float rotation = MeshRotation(visualSeed, i, visual.rotation, visual.rotationVariation);
                    float radians = rotation * Mathf.Deg2Rad;
                    float rotatedHeight = Mathf.Abs(Mathf.Sin(radians)) * planeSize.x + Mathf.Abs(Mathf.Cos(radians)) * planeSize.y;
                    Vector3 adjustedCenter = PositionForMesh(plant, trueCenter, posIndex, i, positions.Length, rotatedHeight, visual, visualSeed, ref clampedBottom);
                    adjustedCenter.x += visual.offsetX;
                    adjustedCenter.z += visual.offsetZ;

                    bool flipped = Rand.Bool;
                    Material snowMaterial = wantSnowOverlay && plant.SnowOverlayGraphic is Graphic snowGraphic ? snowGraphic.MatSingleFor(plant) : null;
                    Material material = plant.Graphic.MatSingleFor(plant);
                    int variationIndex = 0;
                    if (plant.Graphic is Graphic_Random random)
                    {
                        variationIndex = Rand.Range(0, random.SubGraphicsCount);
                        material = random.SubGraphicAtIndex(variationIndex).MatSingle;
                        if (wantSnowOverlay && plant.SnowOverlayGraphic is Graphic_Random randomSnow)
                            snowMaterial = randomSnow.SubGraphicAtIndex(variationIndex).MatSingle;
                    }
                    variationIndex = PlantMaskUtility.VariationIndexForTexture(plant.def, material?.mainTexture, variationIndex);

                    Vector2[] uvs = null;
                    if (NeedsStyledMaterial(visual) || PlantMaskUtility.HasActiveMasks(plant.def))
                    {
                        material = GetStyledMaterial(material, visual, plant, out bool preservesWind, -2, variationIndex);
                        if (preservesWind) PlantUtility.SetWindExposureColors(WorkingColors, plant);
                        else SetWhiteColors();
                    }
                    else
                    {
                        Graphic.TryGetTextureAtlasReplacementInfo(material, plant.def.category.ToAtlasGroup(), flipped, false, out material, out uvs, out _);
                        PlantUtility.SetWindExposureColors(WorkingColors, plant);
                    }

                    Printer_Plane.PrintPlane(layer, adjustedCenter, planeSize, material, rotation, flipUv: flipped, uvs: uvs, colors: WorkingColors, topVerticesAltitudeBias: Plant.TopVerticesAltitudeBias, uvzPayload: plant.HashOffset() % 1024);

                    if (wantSnowOverlay && snowMaterial != null)
                    {
                        Graphic.TryGetTextureAtlasReplacementInfo(snowMaterial, plant.def.category.ToAtlasGroup(), flipped, false, out snowMaterial, out uvs, out _);
                        Printer_Plane.PrintPlane(layer, adjustedCenter.WithYOffset(Altitudes.AltInc / 100), planeSize, snowMaterial, rotation, flipUv: flipped, uvs: uvs, colors: WorkingColors, topVerticesAltitudeBias: Plant.TopVerticesAltitudeBias, uvzPayload: plant.HashOffset() % 1024);
                    }
                }

                float effectSize = Mathf.Max(graphicSize.x * visual.width, graphicSize.y * visual.height);
                if (visual.radiance > 0f) PrintEffect(plant, layer, effectSize, visual.radiance, visual.radianceScale, visual.RadianceColor, false, visual.offsetX, visual.offsetZ);
                if (visual.gloom > 0f) PrintEffect(plant, layer, effectSize, visual.gloom, visual.gloomScale, visual.GloomColor, true, visual.offsetX, visual.offsetZ);
                if (visual.overlayPattern > 0) PrintOverlay(plant, layer, effectSize, visual, visual.offsetX, visual.offsetZ);

                if (plant.def.graphicData.shadowData != null && visual.shadowScale > 0f)
                {
                    Vector3 shadowCenter = trueCenter + plant.def.graphicData.shadowData.offset * growthSize * visual.scale;
                    shadowCenter.x += visual.offsetX;
                    shadowCenter.z += visual.offsetZ;
                    if (clampedBottom) shadowCenter.z = plant.Position.ToVector3Shifted().z + plant.def.graphicData.shadowData.offset.z + visual.offsetZ;
                    shadowCenter.y -= Altitudes.AltInc;
                    Vector3 shadowVolume = plant.def.graphicData.shadowData.volume * growthSize * visual.scale * visual.shadowScale;
                    Printer_Shadow.PrintShadow(layer, shadowCenter, shadowVolume, Rot4.North);
                }
            }
            finally { Rand.PopState(); }
            return false;
        }

        private static bool TryPrintIndependentMaskLayers(Plant plant, SectionLayer layer, PlantVisualParameters wholeVisual)
        {
            if (!PlantMaskUtility.HasActiveMasks(plant.def)) return false;
            CompPlantVariety comp = plant.TryGetComp<CompPlantVariety>();
            if (comp == null) return false;

            PlantVisualParameters[] maskVisuals =
            {
                NovelSeedUtility.ResolvePlantTextureParameters(comp, 0),
                NovelSeedUtility.ResolvePlantTextureParameters(comp, 1),
                NovelSeedUtility.ResolvePlantTextureParameters(comp, 2)
            };
            bool needsLayers = false;
            for (int i = 1; i < maskVisuals.Length; i++)
                if (PlantMaskUtility.AnyResolvedLayerHasPixels(plant.def, i) && !SameGeometry(maskVisuals[0], maskVisuals[i])) { needsLayers = true; break; }
            if (!needsLayers) return false;

            PlantVisualParameters baseVisual = maskVisuals[0];
            PrintIsolatedPlantLayer(plant, layer, baseVisual, -1, true, true);
            for (int i = 0; i < maskVisuals.Length; i++)
                if (PlantMaskUtility.AnyResolvedLayerHasPixels(plant.def, i)) PrintIsolatedPlantLayer(plant, layer, maskVisuals[i], i, false, false);
            PrintExternalEffects(plant, layer, wholeVisual);
            return true;
        }

        private static void PrintIsolatedPlantLayer(Plant plant, SectionLayer layer, PlantVisualParameters visual, int isolatedMaskLayer, bool printSnow, bool printShadow)
        {
            Vector3 trueCenter = plant.TrueCenter();
            bool wantSnowOverlay = printSnow && plant.Position.GetSnowDepth(plant.Map) > 0.8f;
            Rand.PushState();
            Rand.Seed = plant.Position.GetHashCode();
            try
            {
                int meshCount = Mathf.Max(1, Mathf.CeilToInt(plant.Growth * plant.def.plant.maxMeshCount * visual.density));
                float growthSize = plant.def.plant.visualSizeRange.LerpThroughRange(plant.Growth);
                Vector2 graphicSize = plant.def.graphicData.drawSize * (growthSize * visual.scale);
                int[] positions = PlantPosIndices.GetPositionIndices(plant);
                bool clampedBottom = false;
                int positionCount = Mathf.Max(positions.Length, meshCount);
                int visualSeed = plant.Position.GetHashCode();

                for (int i = 0; i < positionCount && i < meshCount; i++)
                {
                    int posIndex = positions[i % positions.Length];
                    float randomScale = MeshScaleFactor(visualSeed, i, visual.scaleVariation);
                    Vector2 planeSize = new Vector2(graphicSize.x * visual.width * randomScale, graphicSize.y * visual.height * randomScale);
                    float rotation = MeshRotation(visualSeed, i, visual.rotation, visual.rotationVariation);
                    float radians = rotation * Mathf.Deg2Rad;
                    float rotatedHeight = Mathf.Abs(Mathf.Sin(radians)) * planeSize.x + Mathf.Abs(Mathf.Cos(radians)) * planeSize.y;
                    Vector3 adjustedCenter = PositionForMesh(plant, trueCenter, posIndex, i, positions.Length, rotatedHeight, visual, visualSeed, ref clampedBottom);
                    adjustedCenter.x += visual.offsetX;
                    adjustedCenter.z += visual.offsetZ;

                    bool flipped = Rand.Bool;
                    Material snowMaterial = wantSnowOverlay && plant.SnowOverlayGraphic is Graphic snowGraphic ? snowGraphic.MatSingleFor(plant) : null;
                    Material material = plant.Graphic.MatSingleFor(plant);
                    int variationIndex = 0;
                    if (plant.Graphic is Graphic_Random random)
                    {
                        variationIndex = Rand.Range(0, random.SubGraphicsCount);
                        material = random.SubGraphicAtIndex(variationIndex).MatSingle;
                        if (wantSnowOverlay && plant.SnowOverlayGraphic is Graphic_Random randomSnow)
                            snowMaterial = randomSnow.SubGraphicAtIndex(variationIndex).MatSingle;
                    }
                    variationIndex = PlantMaskUtility.VariationIndexForTexture(plant.def, material?.mainTexture, variationIndex);

                    Vector2[] uvs = null;
                    material = GetStyledMaterial(material, visual, plant, out bool preservesWind, isolatedMaskLayer, variationIndex);
                    if (preservesWind) PlantUtility.SetWindExposureColors(WorkingColors, plant);
                    else SetWhiteColors();

                    Printer_Plane.PrintPlane(layer, adjustedCenter, planeSize, material, rotation, flipUv: flipped, uvs: uvs, colors: WorkingColors, topVerticesAltitudeBias: Plant.TopVerticesAltitudeBias, uvzPayload: plant.HashOffset() % 1024);

                    if (wantSnowOverlay && snowMaterial != null)
                    {
                        Graphic.TryGetTextureAtlasReplacementInfo(snowMaterial, plant.def.category.ToAtlasGroup(), flipped, false, out snowMaterial, out uvs, out _);
                        Printer_Plane.PrintPlane(layer, adjustedCenter.WithYOffset(Altitudes.AltInc / 100), planeSize, snowMaterial, rotation, flipUv: flipped, uvs: uvs, colors: WorkingColors, topVerticesAltitudeBias: Plant.TopVerticesAltitudeBias, uvzPayload: plant.HashOffset() % 1024);
                    }
                }

                if (printShadow && plant.def.graphicData.shadowData != null && visual.shadowScale > 0f)
                {
                    Vector3 shadowCenter = trueCenter + plant.def.graphicData.shadowData.offset * growthSize * visual.scale;
                    shadowCenter.x += visual.offsetX;
                    shadowCenter.z += visual.offsetZ;
                    if (clampedBottom) shadowCenter.z = plant.Position.ToVector3Shifted().z + plant.def.graphicData.shadowData.offset.z + visual.offsetZ;
                    shadowCenter.y -= Altitudes.AltInc;
                    Vector3 shadowVolume = plant.def.graphicData.shadowData.volume * growthSize * visual.scale * visual.shadowScale;
                    Printer_Shadow.PrintShadow(layer, shadowCenter, shadowVolume, Rot4.North);
                }
            }
            finally { Rand.PopState(); }
        }

        private static void PrintExternalEffects(Plant plant, SectionLayer layer, PlantVisualParameters visual)
        {
            float growthSize = plant.def.plant.visualSizeRange.LerpThroughRange(plant.Growth);
            Vector2 graphicSize = plant.def.graphicData.drawSize * (growthSize * visual.scale);
            float effectSize = Mathf.Max(graphicSize.x * visual.width, graphicSize.y * visual.height);
            if (visual.radiance > 0f) PrintEffect(plant, layer, effectSize, visual.radiance, visual.radianceScale, visual.RadianceColor, false, visual.offsetX, visual.offsetZ);
            if (visual.gloom > 0f) PrintEffect(plant, layer, effectSize, visual.gloom, visual.gloomScale, visual.GloomColor, true, visual.offsetX, visual.offsetZ);
            if (visual.overlayPattern > 0) PrintOverlay(plant, layer, effectSize, visual, visual.offsetX, visual.offsetZ);
        }

        private static bool SameGeometry(PlantVisualParameters a, PlantVisualParameters b)
        {
            return Mathf.Approximately(a.scale, b.scale) && Mathf.Approximately(a.width, b.width) && Mathf.Approximately(a.height, b.height)
                && Mathf.Approximately(a.density, b.density) && Mathf.Approximately(a.spread, b.spread)
                && Mathf.Approximately(a.rotation, b.rotation) && Mathf.Approximately(a.rotationVariation, b.rotationVariation)
                && Mathf.Approximately(a.scaleVariation, b.scaleVariation) && Mathf.Approximately(a.offsetX, b.offsetX)
                && Mathf.Approximately(a.offsetZ, b.offsetZ);
        }
        internal static float MeshScaleFactor(int seed, int meshIndex, float variation)
        {
            return 1f + StableSigned(seed, meshIndex, 17) * Mathf.Max(0f, variation);
        }

        internal static float MeshRotation(int seed, int meshIndex, float fixedRotation, float variation)
        {
            return fixedRotation + StableSigned(seed, meshIndex, 53) * Mathf.Max(0f, variation);
        }

        internal static Vector2 MeshOffset(int seed, int meshIndex, int posIndex, int maxMeshCount, int basePositionCount, float spread)
        {
            if (meshIndex >= Mathf.Max(1, basePositionCount))
            {
                return new Vector2(StableSigned(seed, meshIndex, 71), StableSigned(seed, meshIndex, 89)) * (0.42f * spread);
            }
            if (maxMeshCount <= 1)
            {
                return new Vector2(StableSigned(seed, meshIndex, 31), StableSigned(seed, meshIndex, 47)) * (0.05f * spread);
            }

            int gridWidth = GridWidth(maxMeshCount);
            float spacing = 1f / gridWidth;
            int xIndex = posIndex / gridWidth;
            int zIndex = posIndex % gridWidth;
            float jitterX = StableSigned(seed, meshIndex, 31) * spacing * GridPosRandomnessFactor * 0.5f;
            float jitterZ = StableSigned(seed, meshIndex, 47) * spacing * GridPosRandomnessFactor * 0.5f;
            return new Vector2(((0.5f + xIndex) * spacing - 0.5f + jitterX) * spread,
                ((0.5f + zIndex) * spacing - 0.5f + jitterZ) * spread);
        }

        private static int GridWidth(int maxMeshCount)
        {
            switch (maxMeshCount)
            {
                case 4: return 2;
                case 9: return 3;
                case 16: return 4;
                case 25: return 5;
                default: return Mathf.Max(1, Mathf.RoundToInt(Mathf.Sqrt(maxMeshCount)));
            }
        }

        private static float StableSigned(int seed, int meshIndex, int channel)
        {
            unchecked
            {
                uint value = (uint)(seed * 397 ^ meshIndex * 7919 ^ channel * 104729);
                value ^= value << 13;
                value ^= value >> 17;
                value ^= value << 5;
                return (value & 0x00ffffff) / 8388607.5f - 1f;
            }
        }

        private static Vector3 PositionForMesh(Plant plant, Vector3 trueCenter, int posIndex, int meshIndex, int basePositionCount,
            float planeHeight, PlantVisualParameters visual, int visualSeed, ref bool clampedBottom)
        {
            Vector2 offset = MeshOffset(visualSeed, meshIndex, posIndex, plant.def.plant.maxMeshCount, basePositionCount, visual.spread);
            Vector3 center = trueCenter + new Vector3(offset.x, 0f, offset.y);
            if (plant.def.plant.maxMeshCount == 1)
            {
                float bottom = plant.Position.z;
                if (center.z - planeHeight / 2f < bottom)
                {
                    center.z = bottom + planeHeight / 2f;
                    clampedBottom = true;
                }
            }
            return center;
        }
        private static bool NeedsStyledMaterial(PlantVisualParameters v)
        {
            return !Mathf.Approximately(v.tintRed, 1f) || !Mathf.Approximately(v.tintGreen, 1f) || !Mathf.Approximately(v.tintBlue, 1f)
                || !Mathf.Approximately(v.hueShift, 0f) || !Mathf.Approximately(v.saturation, 1f)
                || !Mathf.Approximately(v.brightness, 1f) || !Mathf.Approximately(v.contrast, 1f)
                || !Mathf.Approximately(v.opacity, 1f) || v.dullness > 0f;
        }

        private static Material GetStyledMaterial(Material source, PlantVisualParameters visual, Plant plant, out bool preservesWind, int isolatedMaskLayer = -2, int variationIndex = 0)
        {
            Texture2D sourceTexture = source.mainTexture as Texture2D;
            int maskKey = unchecked((PlantMaskUtility.MaskHash(plant.def, variationIndex) * 397 ^ NovelSeedUtility.PlantTextureVisualHash(plant.TryGetComp<CompPlantVariety>())) * 31 + isolatedMaskLayer);
            StyledMaterialKey key = new StyledMaterialKey(sourceTexture != null ? sourceTexture.GetInstanceID() : source.GetInstanceID(), source.shader.GetInstanceID(), source.renderQueue, maskKey, visual);
            if (StyledMaterials.TryGetValue(key, out Material cached))
            {
                preservesWind = cached.shader == source.shader;
                return cached;
            }
            if (sourceTexture != null)
            {
                try
                {
                    Texture2D styledTexture = BakeStyledTexture(sourceTexture, visual, plant, isolatedMaskLayer, variationIndex);
                    Material material;
                    if (visual.opacity < 0.999f)
                    {
                        material = MaterialPool.MatFrom(styledTexture, ShaderDatabase.TransparentPostLight, Color.white, source.renderQueue);
                        preservesWind = false;
                    }
                    else
                    {
                        MaterialRequest request;
                        if (!MaterialPool.TryGetRequestForMat(source, out request)) request = new MaterialRequest(styledTexture, source.shader, source.color) { renderQueue = source.renderQueue };
                        else request.mainTex = styledTexture;
                        material = MaterialPool.MatFrom(request);
                        preservesWind = true;
                    }
                    StyledMaterials.Add(key, material);
                    return material;
                }
                catch (Exception exception)
                {
                    Log.ErrorOnce("Horticulture - Novel Seeds could not style plant texture '" + sourceTexture.name + "'. " + exception, key.GetHashCode());
                }
            }
            Color fallbackColor = new Color(visual.tintRed * (1f - visual.dullness), visual.tintGreen * (1f - visual.dullness), visual.tintBlue * (1f - visual.dullness), visual.opacity);
            Material fallback = MaterialPool.MatFrom(source.mainTexture as Texture2D, ShaderDatabase.CutoutComplex, fallbackColor, source.renderQueue);
            StyledMaterials.Add(key, fallback);
            preservesWind = false;
            return fallback;
        }

        private static Texture2D BakeStyledTexture(Texture2D source, PlantVisualParameters visual, Plant plant, int isolatedMaskLayer = -2, int variationIndex = 0)
        {
            RenderTexture previous = RenderTexture.active;
            PlantMaskUtility.BakedTextureSize(source.width, source.height, out int width, out int height);
            RenderTexture temporary = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);
            try
            {
                Graphics.Blit(source, temporary);
                RenderTexture.active = temporary;
                Texture2D result = new Texture2D(width, height, TextureFormat.RGBA32, true)
                {
                    name = source.name + "_HNS_Styled", filterMode = source.filterMode, wrapMode = source.wrapMode, anisoLevel = source.anisoLevel
                };
                result.ReadPixels(new Rect(0f, 0f, width, height), 0, 0, false);
                Color[] pixels = result.GetPixels();
                CompPlantVariety varietyComp = plant.TryGetComp<CompPlantVariety>();
                List<VisualMaskLayerRecord> maskLayers = PlantMaskUtility.LayersForVariation(plant.def, variationIndex, false);
                PlantVisualParameters[] maskVisuals = maskLayers == null ? null : new[]
                {
                    NovelSeedUtility.ResolvePlantTextureParameters(varietyComp, 0),
                    NovelSeedUtility.ResolvePlantTextureParameters(varietyComp, 1),
                    NovelSeedUtility.ResolvePlantTextureParameters(varietyComp, 2)
                };
                for (int i = 0; i < pixels.Length; i++)
                {
                    Color c = pixels[i];
                    if (c.a <= 0f) continue;
                    int x = i % width;
                    int y = i / width;
                    int maskLayer = maskLayers == null ? -2 : PlantMaskUtility.LayerAt(maskLayers, x, y, width, height);
                    PlantVisualParameters pixelVisual;
                    if (isolatedMaskLayer >= -1)
                    {
                        if (maskLayer != isolatedMaskLayer)
                        {
                            c.a = 0f;
                            pixels[i] = c;
                            continue;
                        }
                        pixelVisual = visual;
                    }
                    else
                    {
                        if (maskLayer == -1) continue;
                        pixelVisual = maskLayer >= 0 ? maskVisuals[maskLayer] : visual;
                    }
                    if (!NeedsStyledMaterial(pixelVisual)) continue;
                    pixels[i] = PlantVisualColorUtility.Apply(c, pixelVisual.tintRed, pixelVisual.tintGreen,
                        pixelVisual.tintBlue, pixelVisual.hueShift, pixelVisual.saturation, pixelVisual.brightness,
                        pixelVisual.contrast, pixelVisual.opacity, pixelVisual.dullness);
                }
                result.SetPixels(pixels);
                result.Apply(true, true);
                return result;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(temporary);
            }
        }

        private static void PrintEffect(Plant plant, SectionLayer layer, float graphicSize, float strength, float scale, Color color, bool dense, float offsetX, float offsetZ)
        {
            EffectMaterialKey key = new EffectMaterialKey(strength, color, dense);
            if (!EffectMaterials.TryGetValue(key, out Material material))
            {
                Texture2D texture = CreateRadialTexture(64, color, strength, dense);
                material = MaterialPool.MatFrom(texture, ShaderDatabase.MoteGlow, Color.white);
                EffectMaterials.Add(key, material);
            }
            float size = Mathf.Max(0.65f, graphicSize * (dense ? 1.5f : 1.4f) * scale);
            Vector3 center = plant.TrueCenter().WithYOffset(-Altitudes.AltInc / 2f);
            center.x += offsetX; center.z += offsetZ;
            Printer_Plane.PrintPlane(layer, center, new Vector2(size, size), material, 0f, false, null, WhiteColors);
        }

        private static void PrintOverlay(Plant plant, SectionLayer layer, float graphicSize, PlantVisualParameters visual, float offsetX, float offsetZ)
        {
            OverlayMaterialKey key = new OverlayMaterialKey(visual.overlayPattern, visual.overlayIntensity, visual.OverlayColor);
            if (!OverlayMaterials.TryGetValue(key, out Material material))
            {
                Texture2D texture = CreateOverlayTexture(64, visual.overlayPattern, visual.overlayIntensity, visual.OverlayColor);
                material = MaterialPool.MatFrom(texture, ShaderDatabase.TransparentPostLight, Color.white);
                OverlayMaterials.Add(key, material);
            }
            float size = Mathf.Max(0.55f, graphicSize * 1.22f * visual.overlayScale);
            Vector3 center = plant.TrueCenter().WithYOffset(-Altitudes.AltInc / 3f);
            center.x += offsetX; center.z += offsetZ;
            Printer_Plane.PrintPlane(layer, center, new Vector2(size, size), material, visual.rotation, false, null, WhiteColors);
        }

        private static Texture2D CreateRadialTexture(int size, Color color, float strength, bool dense)
        {
            Texture2D texture = NewEffectTexture(size, "HNS_PlantEffect");
            Color32[] pixels = new Color32[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float dx = ((x + 0.5f) / size) * 2f - 1f;
                float dy = ((y + 0.5f) / size) * 2f - 1f;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                float halo = Mathf.Pow(Mathf.Clamp01(1f - distance), dense ? 1.15f : 1.35f);
                float rays = dense ? 0f : Mathf.Pow(Mathf.Abs(Mathf.Cos(Mathf.Atan2(dy, dx) * 6f)), 18f) * Mathf.Clamp01(1f - distance * 0.8f) * 0.1f;
                Color pixel = color; pixel.a = Mathf.Clamp01((halo * (dense ? 0.7f : 0.55f) + rays) * strength);
                pixels[y * size + x] = pixel;
            }
            texture.SetPixels32(pixels); texture.Apply(true, true); return texture;
        }

        private static Texture2D CreateOverlayTexture(int size, int pattern, float intensity, Color color)
        {
            Texture2D texture = NewEffectTexture(size, "HNS_PlantOverlay_" + pattern);
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
                case 3:
                    return Mathf.Abs(Mathf.Sin((nx + ny * 0.35f) * 22f)) > 0.82f ? 0.55f * Mathf.Clamp01(1f - distance) : 0f;
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
            return new Texture2D(size, size, TextureFormat.RGBA32, true) { name = name, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        }

        private static void SetWhiteColors()
        {
            for (int i = 0; i < WorkingColors.Length; i++) WorkingColors[i] = Color.white;
        }

        private readonly struct StyledMaterialKey : IEquatable<StyledMaterialKey>
        {
            private readonly int texture, shader, queue, mask;
            private readonly int r, g, b, hue, saturation, brightness, contrast, opacity, dullness;
            public StyledMaterialKey(int texture, int shader, int queue, int mask, PlantVisualParameters v)
            {
                this.texture = texture; this.shader = shader; this.queue = queue; this.mask = mask;
                r = Q(v.tintRed); g = Q(v.tintGreen); b = Q(v.tintBlue); hue = Q(v.hueShift);
                saturation = Q(v.saturation); brightness = Q(v.brightness); contrast = Q(v.contrast);
                opacity = Q(v.opacity); dullness = Q(v.dullness);
            }
            public bool Equals(StyledMaterialKey o) => texture == o.texture && shader == o.shader && queue == o.queue && mask == o.mask && r == o.r && g == o.g && b == o.b && hue == o.hue && saturation == o.saturation && brightness == o.brightness && contrast == o.contrast && opacity == o.opacity && dullness == o.dullness;
            public override bool Equals(object obj) => obj is StyledMaterialKey o && Equals(o);
            public override int GetHashCode() { unchecked { int h = texture; h = h * 397 ^ shader; h = h * 397 ^ queue; h = h * 397 ^ mask; h = h * 397 ^ r; h = h * 397 ^ g; h = h * 397 ^ b; h = h * 397 ^ hue; h = h * 397 ^ saturation; h = h * 397 ^ brightness; h = h * 397 ^ contrast; h = h * 397 ^ opacity; return h * 397 ^ dullness; } }
        }

        private readonly struct EffectMaterialKey : IEquatable<EffectMaterialKey>
        {
            private readonly int strength, color; private readonly bool dense;
            public EffectMaterialKey(float strength, Color color, bool dense) { this.strength = Q(strength); this.color = ((Color32)color).GetHashCode(); this.dense = dense; }
            public bool Equals(EffectMaterialKey o) => strength == o.strength && color == o.color && dense == o.dense;
            public override bool Equals(object obj) => obj is EffectMaterialKey o && Equals(o);
            public override int GetHashCode() => ((strength * 397) ^ color) * 397 ^ dense.GetHashCode();
        }

        private readonly struct OverlayMaterialKey : IEquatable<OverlayMaterialKey>
        {
            private readonly int pattern, intensity, color;
            public OverlayMaterialKey(int pattern, float intensity, Color color) { this.pattern = pattern; this.intensity = Q(intensity); this.color = ((Color32)color).GetHashCode(); }
            public bool Equals(OverlayMaterialKey o) => pattern == o.pattern && intensity == o.intensity && color == o.color;
            public override bool Equals(object obj) => obj is OverlayMaterialKey o && Equals(o);
            public override int GetHashCode() => ((pattern * 397) ^ intensity) * 397 ^ color;
        }

        private static int Q(float value) => Mathf.RoundToInt(value * 100f);
    }
}
