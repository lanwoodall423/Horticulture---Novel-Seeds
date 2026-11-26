using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using UnityEngine;
using Verse;

namespace HorticultureNovelSeeds
{
    public sealed class MaskTextureIdentityDetails
    {
        public string Key;
        public string TextureName;
        public int Width;
        public int Height;
        public string StateLabel;
        public string Orientation;
        public string ContentHash;
    }

    public static class MaskTextureIdentity
    {
        private static readonly Dictionary<string, string> IdentityCache = new Dictionary<string, string>();
        private static readonly Dictionary<string, MaskTextureIdentityDetails> DetailsCache = new Dictionary<string, MaskTextureIdentityDetails>();

        public static bool TryGet(Texture texture, string stateLabel, out string key)
        {
            return TryGet(texture, stateLabel, true, out key);
        }

        public static bool TryGetCached(Texture texture, string stateLabel, out string key)
        {
            return TryGet(texture, stateLabel, false, out key);
        }

        private static bool TryGet(Texture texture, string stateLabel, bool allowRead, out string key)
        {
            key = null;
            if (!TryGetDetails(texture, stateLabel, allowRead, out MaskTextureIdentityDetails details)) return false;
            key = details.Key;
            return true;
        }

        public static bool TryGetDetails(Texture texture, string stateLabel, out MaskTextureIdentityDetails details)
        {
            return TryGetDetails(texture, stateLabel, true, out details);
        }

        public static bool TryGetCachedDetails(Texture texture, string stateLabel, out MaskTextureIdentityDetails details)
        {
            return TryGetDetails(texture, stateLabel, false, out details);
        }

        private static bool TryGetDetails(Texture texture, string stateLabel, bool allowRead, out MaskTextureIdentityDetails details)
        {
            details = null;
            if (texture == null || texture.width <= 0 || texture.height <= 0) return false;
            string normalizedState = NormalizeStateLabel(stateLabel);
            string cacheKey = texture.GetInstanceID() + "|" + normalizedState;
            if (DetailsCache.TryGetValue(cacheKey, out details)) return details != null && !details.Key.NullOrEmpty();
            if (!allowRead) return false;
            Color32[] pixels = ReadPixels(texture, texture.width, texture.height);
            if (pixels == null)
            {
                IdentityCache[cacheKey] = string.Empty;
                DetailsCache[cacheKey] = null;
                return false;
            }
            string orientation = OrientationFor(texture.name);
            string contentHash = PixelFingerprint(pixels);
            string key = "v1|" + (texture.name ?? string.Empty) + "|" + texture.width + "x" + texture.height
                + "|state:" + normalizedState + "|orientation:" + orientation + "|pixels:" + contentHash;
            details = new MaskTextureIdentityDetails
            {
                Key = key,
                TextureName = texture.name ?? string.Empty,
                Width = texture.width,
                Height = texture.height,
                StateLabel = normalizedState,
                Orientation = orientation,
                ContentHash = contentHash
            };
            IdentityCache[cacheKey] = key;
            DetailsCache[cacheKey] = details;
            return true;
        }

        public static bool TryGet(ThingDef plantDef, int variationIndex, out string key)
        {
            key = null;
            Texture texture = PlantMaskUtility.TextureForVariation(plantDef, variationIndex);
            return TryGet(texture, PlantMaskUtility.VariationLabel(plantDef, variationIndex), out key);
        }

        public static bool TryGetCached(ThingDef plantDef, int variationIndex, out string key)
        {
            key = null;
            Texture texture = PlantMaskUtility.TextureForVariation(plantDef, variationIndex);
            return TryGetCached(texture, PlantMaskUtility.VariationLabel(plantDef, variationIndex), out key);
        }

        public static void PreloadPlantTextures()
        {
            foreach (ThingDef plant in DefDatabase<ThingDef>.AllDefsListForReading.Where(def => def?.plant != null))
                for (int variation = 0; variation < PlantMaskUtility.VariationCount(plant); variation++)
                    TryGet(plant, variation, out _);
        }

        public static string NormalizeStateLabel(string stateLabel)
        {
            if (stateLabel.NullOrEmpty()) return string.Empty;
            int marker = stateLabel.LastIndexOf(" of ", StringComparison.OrdinalIgnoreCase);
            if (marker > 0)
            {
                int start = marker - 1;
                while (start >= 0 && char.IsDigit(stateLabel[start])) start--;
                if (start >= 0 && int.TryParse(stateLabel.Substring(start + 1, marker - start - 1), out _))
                    return stateLabel.Substring(0, start).TrimEnd();
            }
            return stateLabel.Trim();
        }

        public static void ClearCache()
        {
            IdentityCache.Clear();
            DetailsCache.Clear();
        }

        public static Color32[] ReadPixels(Texture texture, int width, int height)
        {
            if (texture == null || width <= 0 || height <= 0) return null;
            RenderTexture temporary = null;
            Texture2D readable = null;
            RenderTexture previous = RenderTexture.active;
            try
            {
                temporary = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
                Graphics.Blit(texture, temporary);
                RenderTexture.active = temporary;
                readable = new Texture2D(width, height, TextureFormat.RGBA32, false);
                readable.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                readable.Apply(false, false);
                return readable.GetPixels32();
            }
            catch
            {
                return null;
            }
            finally
            {
                RenderTexture.active = previous;
                if (temporary != null) RenderTexture.ReleaseTemporary(temporary);
                if (readable != null) UnityEngine.Object.DestroyImmediate(readable);
            }
        }

        public static string PixelFingerprint(Color32[] pixels)
        {
            if (pixels == null) return "none";
            byte[] bytes = new byte[pixels.Length * 4];
            for (int index = 0; index < pixels.Length; index++)
            {
                Color32 pixel = pixels[index];
                int offset = index * 4;
                bytes[offset] = pixel.r;
                bytes[offset + 1] = pixel.g;
                bytes[offset + 2] = pixel.b;
                bytes[offset + 3] = pixel.a;
            }
            using (SHA256 sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", string.Empty);
        }

        public static string OrientationFor(string textureName)
        {
            if (textureName.NullOrEmpty()) return "unknown";
            string lower = textureName.ToLowerInvariant();
            if (lower.Contains("_north")) return "north";
            if (lower.Contains("_south")) return "south";
            if (lower.Contains("_east")) return "east";
            if (lower.Contains("_west")) return "west";
            return "default";
        }
    }

}
