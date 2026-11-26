using System;
using System.Collections.Generic;
using UnityEngine;

namespace HorticultureNovelSeeds
{
    // Explicit preparation only; never called by Print or an ordinary draw.
    // One-hot channels preserve the old first-painted-layer-wins rule.
    internal static class PackedPhenotypeMask
    {
        internal static Texture2D Create(IReadOnlyList<VisualMaskLayerRecord> layers)
        {
            if (layers == null || layers.Count != 3 || layers[0] == null || layers[1] == null || layers[2] == null)
                throw new ArgumentException("Exactly three resolved semantic layers are required.", nameof(layers));
            int size = VisualMaskLayerRecord.Resolution;
            var pixels = new Color32[size * size];
            var neutral = new Color32(0, 0, 0, 255);
            var red = new Color32(255, 0, 0, 255);
            var green = new Color32(0, 255, 0, 255);
            var blue = new Color32(0, 0, 255, 255);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    // Reuse the canonical CPU mapping, including the Y inversion.
                    int layer = PlantMaskUtility.LayerAt(layers, x, y, size, size);
                    pixels[y * size + x] = layer == 0 ? red : layer == 1 ? green : layer == 2 ? blue : neutral;
                }
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
            {
                name = "HNS_PackedSemanticMask",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }
    }
}
