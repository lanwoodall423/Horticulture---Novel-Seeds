using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace HorticultureNovelSeeds
{
    // Experimental, opt-in only from the isolated native fixture.
    internal static class WaylithPhenotypeShader
    {
        internal const string DefName = "HNS_PhenotypeOpaque";
        internal const string WindDefName = "HNS_PhenotypeWind";
        internal const int MaterialLimit = 128, MaskLimit = 64;
        internal static bool Enabled { get; set; }
        private static bool unavailable, windUnavailable;
        private static readonly Dictionary<MaterialKey, Material> materials = new();
        private static readonly Dictionary<(ThingDef, int), PreparedMask> masks = new();
        internal static int MaterialCount => materials.Count;
        internal static int MaskCount => masks.Count;
#if HNS_NATIVE_FIXTURE
        internal static readonly HashSet<int> PrintedPlants = new();
        internal static readonly HashSet<int> MaskedPlants = new();
        internal static readonly HashSet<int> ProceduralPlants = new();
        internal static readonly HashSet<int> MaskFreeProceduralPlants = new();
        internal static readonly HashSet<(int,int)> DevelopmentalPrints = new();
        internal static readonly Dictionary<int,Material> WindPrints = new();
        internal static readonly Dictionary<int,Material> SurfacePrints = new();
#endif

        // Caller must prepare from the main-thread editor/load boundary, not a print.
        internal static bool PrepareMask(ThingDef crop, int variation)
        {
            if (!Enabled || crop == null || masks.Count >= MaskLimit) return false;
            var key = (crop, variation);
            if (masks.ContainsKey(key)) return false; // no disposal/eviction of referenced textures
            var layers = PlantMaskUtility.LayersForVariation(crop, variation, false);
            var source = PlantMaskUtility.TextureForVariation(crop, variation) as Texture2D;
            if (source == null || layers == null || layers.Count != 3 || layers.Any(l => l == null)) return false;
            // Shared-manual resolution returns clones; do not qualify a mutable
            // or ambiguous identity by hash alone.
            var repeated = PlantMaskUtility.LayersForVariation(crop, variation, false);
            if (repeated == null || repeated.Count != 3 || Enumerable.Range(0, 3).Any(i => !ReferenceEquals(repeated[i], layers[i]))) return false;
            var texture = PackedPhenotypeMask.Create(layers);
            masks.Add(key, new PreparedMask(source, texture, layers.ToArray(), layers.Select(l => l.ContentHash).ToArray()));
            return true;
        }

        internal static bool TryForPlant(Material source, PlantVisualParameters visual, Plant plant,
            int isolatedMaskLayer, int variationIndex, out Material material)
        {
            material = null;
            if (!Enabled || isolatedMaskLayer != -2 || plant == null || source == null) return false;
            var comp = plant.TryGetComp<CompPlantVariety>();
            Vector4 pattern = ProceduralPhenotypeUtility.Resolve(comp);
            var wind = plant.Position.GetSnowDepth(plant.Map) > .8f ? WindHabitKind.None : WindHabitUtility.Resolve(comp);
            var finish=plant.Position.GetSnowDepth(plant.Map)>.8f ? SurfaceFinishKind.None : SurfaceFinishUtility.Resolve(comp);
            bool sheltered=SurfaceFinishMath.Valid(finish) && plant.Map.roofGrid.RoofAt(plant.Position)!=null;
            if (!NovelSeedUtility.HasRegionalPlantColor(comp) && (pattern.x > 0f || SurfaceFinishMath.Valid(finish) || WindHabitMath.Valid(wind) || !PlantMaskUtility.HasActiveMasks(plant.def) || NovelSeedUtility.HasPlantMaskVisual(comp)))
            {
                if (!TryGetCore(source, null, visual, PlantVisualParameters.Default, PlantVisualParameters.Default, pattern, out material, wind, finish, sheltered)) return false;
#if HNS_NATIVE_FIXTURE
                if (pattern.x > 0f) { ProceduralPlants.Add(plant.thingIDNumber); MaskFreeProceduralPlants.Add(plant.thingIDNumber); }
                PrintedPlants.Add(plant.thingIDNumber);
                if(WindHabitMath.Valid(wind)) WindPrints[plant.thingIDNumber]=material;
                if(SurfaceFinishMath.Valid(finish)) SurfacePrints[plant.thingIDNumber]=material;
                if (DevelopmentalPhenotype.IsMode((int)pattern.x) && material.GetVector("_HnsPattern").w == pattern.w)
                    DevelopmentalPrints.Add((plant.thingIDNumber, DevelopmentalPhenotype.Band(plant.Growth)));
#endif
                return true;
            }
            var layers = PlantMaskUtility.LayersForVariation(plant.def, variationIndex, false);
            if (layers == null)
            {
                if (!TryGetCore(source, null, visual, PlantVisualParameters.Default, PlantVisualParameters.Default, pattern, out material, wind, finish, sheltered)) return false;
            }
            else
            {
                if (!masks.TryGetValue((plant.def, variationIndex), out var prepared) ||
                    !prepared.Matches(source.mainTexture, layers)) return false;
                var a = NovelSeedUtility.ResolvePlantTextureParameters(comp, 0);
                var b = NovelSeedUtility.ResolvePlantTextureParameters(comp, 1);
                var c = NovelSeedUtility.ResolvePlantTextureParameters(comp, 2);
#if HNS_NATIVE_FIXTURE
                NativeShaderProbe.TryOverrideMaskStyles(plant, ref a, ref b, ref c);
#endif
                if (!TryGetCore(source, prepared.Texture, a, b, c, pattern, out material, wind, finish, sheltered)) return false;
#if HNS_NATIVE_FIXTURE
                MaskedPlants.Add(plant.thingIDNumber);
#endif
            }
#if HNS_NATIVE_FIXTURE
            PrintedPlants.Add(plant.thingIDNumber);
            if (pattern.x > 0f) ProceduralPlants.Add(plant.thingIDNumber);
#endif
            return true;
        }

        internal static bool TryGet(Material source, PlantVisualParameters visual, out Material material) =>
            TryGetCore(source, null, visual, PlantVisualParameters.Default, PlantVisualParameters.Default, Vector4.zero, out material);

        internal static bool TryGetMasked(Material source, Texture2D mask, PlantVisualParameters a,
            PlantVisualParameters b, PlantVisualParameters c, out Material material)
        {
            material = null;
            return mask != null && TryGetCore(source, mask, a, b, c, Vector4.zero, out material);
        }

        internal static bool TryGetCore(Material source, Texture2D mask, PlantVisualParameters a,
            PlantVisualParameters b, PlantVisualParameters c, Vector4 pattern, out Material material, WindHabitKind wind = WindHabitKind.None, SurfaceFinishKind finish = SurfaceFinishKind.None, bool sheltered = false)
        {
            material = null;
            bool motion=WindHabitMath.Valid(wind);
            if (!Enabled || (motion ? windUnavailable : unavailable) || source == null || source.shader != ShaderDatabase.CutoutPlant ||
                source.mainTexture is not Texture2D texture || !source.HasProperty("_FallBehaviorEnabled") ||
                source.GetFloat("_FallBehaviorEnabled") != 0f || a.opacity != 1f || b.opacity != 1f || c.opacity != 1f) return false;
            var s0 = new Style(a); var s1 = new Style(b); var s2 = new Style(c);
            if (!s0.Valid || !s1.Valid || !s2.Valid || !Finite(pattern)) return false;
            var key = new MaterialKey(source, texture, source.renderQueue, source.mainTextureScale,
                source.mainTextureOffset, mask, s0, s1, s2, pattern, wind, finish, sheltered);
            if (materials.TryGetValue(key, out material)) return true;
            if (materials.Count >= MaterialLimit) return false;
            Material candidate = null;
            try
            {
                var def = DefDatabase<ShaderDef>.GetNamedSilentFail(motion ? WindDefName : DefName);
                Shader shader = def?.Shader;
                if (shader == null || shader == ShaderDatabase.CutoutPlant ||
                    !shader.HasProperty(Shader.PropertyToID("_HnsControls")) ||
                    !shader.HasProperty(Shader.PropertyToID("_HnsTint")) ||
                    !shader.HasProperty(Shader.PropertyToID("_HnsDullness")) ||
                    !shader.HasProperty(Shader.PropertyToID("_HnsMaskEnabled")) ||
                    !shader.HasProperty(Shader.PropertyToID("_HnsMask")) ||
                    !shader.HasProperty(Shader.PropertyToID("_HnsTint1")) ||
                    !shader.HasProperty(Shader.PropertyToID("_HnsControls1")) ||
                    !shader.HasProperty(Shader.PropertyToID("_HnsDullness1")) ||
                    !shader.HasProperty(Shader.PropertyToID("_HnsTint2")) ||
                    !shader.HasProperty(Shader.PropertyToID("_HnsControls2")) ||
                    !shader.HasProperty(Shader.PropertyToID("_HnsDullness2")) ||
                    !shader.HasProperty(Shader.PropertyToID("_HnsPattern")) ||
                    !shader.HasProperty(Shader.PropertyToID("_SwayHead")) ||
                    (motion && !shader.HasProperty(Shader.PropertyToID("_HnsWind"))) ||
                    (SurfaceFinishMath.Valid(finish) && (!shader.HasProperty(Shader.PropertyToID("_HnsSurface")) || !shader.HasProperty(Shader.PropertyToID("_HnsEnvironment")))))
                    throw new InvalidOperationException("Phenotype shader or material layout unavailable.");
                candidate = new Material(shader)
                {
                    name = "HNS_Phenotype_" + texture.name,
                    mainTexture = texture,
                    mainTextureScale = source.mainTextureScale,
                    mainTextureOffset = source.mainTextureOffset,
                    renderQueue = source.renderQueue
                };
                candidate.SetVector("_HnsPattern", pattern);
                if(motion) candidate.SetVector("_HnsWind",WindHabitUtility.Controls(wind));
                if(SurfaceFinishMath.Valid(finish)) { candidate.SetFloat("_HnsSurface",(int)finish); SurfaceFinishUtility.Register(candidate,sheltered); }
                s0.Apply(candidate, "");
                s1.Apply(candidate, "1");
                s2.Apply(candidate, "2");
                candidate.SetFloat("_HnsMaskEnabled", mask == null ? 0f : 1f);
                candidate.SetTexture("_HnsMask", mask == null ? texture : mask);
                candidate.SetFloat("_SwayHead", source.GetFloat("_SwayHead"));
                WindManager.Notify_PlantMaterialCreated(candidate);
                materials.Add(key, candidate);
                material = candidate;
                return true;
            }
            catch (Exception e)
            {
                if(motion) windUnavailable=true; else unavailable = true;
                if (candidate != null) UnityEngine.Object.Destroy(candidate);
                Log.WarningOnce("[Horticulture] Experimental phenotype shader unavailable; using legacy renderer. " + e.Message, 174329020);
                return false;
            }
        }

        private static bool Finite(Vector4 value) => float.IsFinite(value.x) && float.IsFinite(value.y) &&
            float.IsFinite(value.z) && float.IsFinite(value.w);
        private readonly record struct Style(Vector4 Tint, Vector4 Controls, float Dullness)
        {
            internal Style(PlantVisualParameters v) : this(new Vector4(v.tintRed, v.tintGreen, v.tintBlue, 1f),
                new Vector4(v.hueShift, v.saturation, v.brightness, v.contrast), v.dullness) { }
            internal bool Valid => Finite(Tint) && Finite(Controls) && float.IsFinite(Dullness);
            internal void Apply(Material m, string suffix)
            {
                m.SetVector("_HnsTint" + suffix, Tint);
                m.SetVector("_HnsControls" + suffix, Controls);
                m.SetFloat("_HnsDullness" + suffix, Dullness);
            }
        }
        private readonly record struct MaterialKey(Material Source, Texture2D Texture, int Queue, Vector2 Scale,
            Vector2 Offset, Texture2D Mask, Style A, Style B, Style C, Vector4 Pattern, WindHabitKind Wind, SurfaceFinishKind Finish, bool Sheltered);
        private sealed record PreparedMask(Texture2D Source, Texture2D Texture, VisualMaskLayerRecord[] Layers, int[] Hashes)
        {
            internal bool Matches(Texture source, IReadOnlyList<VisualMaskLayerRecord> layers)
            {
                if (!ReferenceEquals(Source, source) || layers.Count != 3) return false;
                for (int i = 0; i < 3; i++)
                    // Conservative: shared resolver clones are not eligible. Changed or
                    // replaced layers fail closed until explicit re-preparation.
                    if (!ReferenceEquals(layers[i], Layers[i]) || layers[i].ContentHash != Hashes[i]) return false;
                return true;
            }
        }
    }
}
