using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace HorticultureNovelSeeds
{
    // Experimental, opt-in only from the isolated native fixture. No default
    // activation or shader assets in the ordinary candidate.
    internal static class WaylithPhenotypeShader
    {
        internal const string DefName = "HNS_PhenotypeOpaque";
        internal const int MaterialLimit = 128;
        internal static bool Enabled { get; set; }
        private static bool unavailable;
        private static readonly Dictionary<MaterialKey, Material> materials = new();
        internal static int MaterialCount => materials.Count;
#if HNS_NATIVE_FIXTURE
        internal static readonly HashSet<int> PrintedPlants = new();
#endif

        internal static bool TryForPlant(Material source, PlantVisualParameters visual, Plant plant,
            int isolatedMaskLayer, int variationIndex, out Material material)
        {
            material = null;
            if (!Enabled || isolatedMaskLayer != -2 || plant == null) return false;
            // A mask changes the meaning of unassigned pixels. Never substitute
            // whole-plant color just because shader materials are available.
            if (PlantMaskUtility.LayersForVariation(plant.def, variationIndex, false) != null) return false;
            if (!TryGet(source, visual, out material)) return false;
#if HNS_NATIVE_FIXTURE
            PrintedPlants.Add(plant.thingIDNumber);
#endif
            return true;
        }

        internal static bool TryGet(Material source, PlantVisualParameters visual, out Material material)
        {
            material = null;
            if (!Enabled || unavailable || source == null || source.shader != ShaderDatabase.CutoutPlant ||
                source.mainTexture is not Texture2D texture || !source.HasProperty("_FallBehaviorEnabled") ||
                source.GetFloat("_FallBehaviorEnabled") != 0f || visual.opacity != 1f) return false;
            var tint = new Vector4(visual.tintRed, visual.tintGreen, visual.tintBlue, 1f);
            var controls = new Vector4(visual.hueShift, visual.saturation, visual.brightness, visual.contrast);
            if (!Finite(tint) || !Finite(controls) || !float.IsFinite(visual.dullness)) return false;
            var key = new MaterialKey(source, texture, source.renderQueue, source.mainTextureScale,
                source.mainTextureOffset, tint, controls, visual.dullness);
            if (materials.TryGetValue(key, out material)) return true;
            if (materials.Count >= MaterialLimit) return false; // bounded lifetime: wind registry retains materials
            Material candidate = null;
            try
            {
                var def = DefDatabase<ShaderDef>.GetNamedSilentFail(DefName);
                Shader shader = def?.Shader;
                if (shader == null || shader == ShaderDatabase.CutoutPlant ||
                    !shader.HasProperty(Shader.PropertyToID("_HnsControls")) ||
                    !shader.HasProperty(Shader.PropertyToID("_HnsTint")) ||
                    !shader.HasProperty(Shader.PropertyToID("_HnsDullness")) ||
                    !shader.HasProperty(Shader.PropertyToID("_SwayHead")))
                    throw new InvalidOperationException("Phenotype shader or material layout unavailable.");
                candidate = new Material(shader)
                {
                    name = "HNS_Phenotype_" + texture.name,
                    mainTexture = texture,
                    mainTextureScale = source.mainTextureScale,
                    mainTextureOffset = source.mainTextureOffset,
                    renderQueue = source.renderQueue
                };
                candidate.SetVector("_HnsTint", tint);
                candidate.SetVector("_HnsControls", controls);
                candidate.SetFloat("_HnsDullness", visual.dullness);
                candidate.SetFloat("_SwayHead", source.GetFloat("_SwayHead"));
                // MaterialPool only registers vanilla CutoutPlant/TransparentPlant.
                WindManager.Notify_PlantMaterialCreated(candidate);
                materials.Add(key, candidate);
                material = candidate;
                return true;
            }
            catch (Exception e)
            {
                unavailable = true;
                if (candidate != null) UnityEngine.Object.Destroy(candidate);
                Log.WarningOnce("[Horticulture] Experimental phenotype shader unavailable; using legacy renderer. " + e.Message, 174329020);
                return false;
            }
        }

        private static bool Finite(Vector4 value) => float.IsFinite(value.x) && float.IsFinite(value.y) &&
            float.IsFinite(value.z) && float.IsFinite(value.w);
        private readonly record struct MaterialKey(Material Source, Texture2D Texture, int Queue, Vector2 Scale,
            Vector2 Offset, Vector4 Tint, Vector4 Controls, float Dullness);
    }
}
