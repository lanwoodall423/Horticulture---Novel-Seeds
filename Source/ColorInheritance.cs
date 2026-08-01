using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace HorticultureNovelSeeds
{
    public sealed class PlantColorRangeExtension : DefModExtension
    {
        public bool unrestrictedColors;
        public float allowedHueRangeDegrees = -1f;
        public float minimumSaturation = -1f;
        public float maximumSaturation = -1f;
        public float minimumValue = -1f;
        public float maximumValue = -1f;
    }

    public sealed class SpeciesColorPaletteRecord : IExposable
    {
        public string plantDefName;
        public bool unrestricted;
        public bool hybridDerived;
        public List<int> packedColors = new List<int>();

        public ThingDef PlantDef => DefDatabase<ThingDef>.GetNamedSilentFail(plantDefName);
        public IEnumerable<Color> Colors => packedColors?.Select(Unpack) ?? Enumerable.Empty<Color>();

        public void ExposeData()
        {
            Scribe_Values.Look(ref plantDefName, "plantDef");
            Scribe_Values.Look(ref unrestricted, "unrestricted", false);
            Scribe_Values.Look(ref hybridDerived, "hybridDerived", false);
            Scribe_Collections.Look(ref packedColors, "colors", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
                packedColors = packedColors?.Distinct().ToList() ?? new List<int>();
        }

        public static int Pack(Color color)
        {
            Color32 value = color;
            return value.r | value.g << 8 | value.b << 16 | 255 << 24;
        }

        public static Color Unpack(int value)
        {
            return new Color32((byte)value, (byte)(value >> 8), (byte)(value >> 16), 255);
        }
    }

    public static class PigmentColorUtility
    {
        // Weighted geometric mixing in linear-light reflectance approximates subtractive pigments.
        // Hue and chroma are restored in perceptual HSV to prevent complementary mixes becoming gray.
        public static Color Blend(IEnumerable<KeyValuePair<Color, float>> weightedColors)
        {
            List<KeyValuePair<Color, float>> colors = weightedColors?
                .Where(item => item.Value > 0f).ToList() ?? new List<KeyValuePair<Color, float>>();
            if (colors.Count == 0) return Color.white;
            float total = colors.Sum(item => item.Value);
            if (total <= 0f) return Color.white;

            const float epsilon = 0.003f;
            float logR = 0f, logG = 0f, logB = 0f;
            float hueX = 0f, hueY = 0f, saturation = 0f, value = 0f, alpha = 0f;
            foreach (KeyValuePair<Color, float> item in colors)
            {
                float weight = item.Value / total;
                Color linear = item.Key.linear;
                logR += Mathf.Log(Mathf.Max(epsilon, linear.r)) * weight;
                logG += Mathf.Log(Mathf.Max(epsilon, linear.g)) * weight;
                logB += Mathf.Log(Mathf.Max(epsilon, linear.b)) * weight;
                Color.RGBToHSV(item.Key, out float hue, out float sat, out float val);
                float hueWeight = weight * Mathf.Max(0.12f, sat);
                float angle = hue * Mathf.PI * 2f;
                hueX += Mathf.Cos(angle) * hueWeight;
                hueY += Mathf.Sin(angle) * hueWeight;
                saturation += sat * weight;
                value += val * weight;
                alpha += item.Key.a * weight;
            }

            Color reflectance = new Color(Mathf.Exp(logR), Mathf.Exp(logG), Mathf.Exp(logB), alpha).gamma;
            Color.RGBToHSV(reflectance, out float mixedHue, out float mixedSaturation, out float mixedValue);
            if (hueX * hueX + hueY * hueY > 0.00001f)
                mixedHue = Mathf.Repeat(Mathf.Atan2(hueY, hueX) / (Mathf.PI * 2f), 1f);
            mixedSaturation = Mathf.Clamp01(Mathf.Max(mixedSaturation, saturation * 0.88f));
            mixedValue = Mathf.Clamp01(Mathf.Min(value, mixedValue * 1.08f));
            Color result = Color.HSVToRGB(mixedHue, mixedSaturation, mixedValue, false);
            result.a = alpha;
            return result;
        }

        public static Color Blend(Color first, Color second)
        {
            return Blend(new[]
            {
                new KeyValuePair<Color, float>(first, 1f),
                new KeyValuePair<Color, float>(second, 1f)
            });
        }

        public static float PerceptualDistance(Color first, Color second)
        {
            Color.RGBToHSV(first, out float firstHue, out float firstSaturation, out float firstValue);
            Color.RGBToHSV(second, out float secondHue, out float secondSaturation, out float secondValue);
            float hue = Mathf.Min(Mathf.Abs(firstHue - secondHue), 1f - Mathf.Abs(firstHue - secondHue));
            return hue * hue * 4f + Mathf.Pow(firstSaturation - secondSaturation, 2f)
                + Mathf.Pow(firstValue - secondValue, 2f) * 0.7f;
        }
    }

    public static class SpeciesColorPaletteUtility
    {
        public static SpeciesColorPaletteRecord Generate(ThingDef plantDef, string worldSeed, NovelSeedsSettings settings)
        {
            PlantColorRangeExtension extension = plantDef?.GetModExtension<PlantColorRangeExtension>();
            PlantSettingsRecord plantSettings = settings?.GetEffectivePlantSettings(plantDef, false);
            bool unrestricted = extension?.unrestrictedColors == true || plantSettings?.unrestrictedColors == true;
            int minimumSize = settings?.minimumPaletteSize ?? 2;
            int maximumSize = settings?.maximumPaletteSize ?? 5;
            StableRandom random = new StableRandom((worldSeed ?? string.Empty) + "|" + plantDef?.defName + "|HNS-color-v1");
            int count = random.Range(Mathf.Clamp(minimumSize, 1, 24), Mathf.Clamp(maximumSize, minimumSize, 24) + 1);
            float hueRange = extension?.allowedHueRangeDegrees >= 0f ? extension.allowedHueRangeDegrees : settings?.allowedHueRangeDegrees ?? 140f;
            float minimumSaturation = extension?.minimumSaturation >= 0f ? extension.minimumSaturation : settings?.minimumPaletteSaturation ?? 0.55f;
            float maximumSaturation = extension?.maximumSaturation >= 0f ? extension.maximumSaturation : settings?.maximumPaletteSaturation ?? 0.95f;
            float minimumValue = extension?.minimumValue >= 0f ? extension.minimumValue : settings?.minimumPaletteValue ?? 0.62f;
            float maximumValue = extension?.maximumValue >= 0f ? extension.maximumValue : settings?.maximumPaletteValue ?? 0.95f;
            Color baseColor = BaseColor(plantDef);
            Color.RGBToHSV(baseColor, out float baseHue, out _, out _);
            float range = unrestricted ? 1f : Mathf.Clamp(hueRange / 360f, 0f, 1f);
            float center = unrestricted ? random.NextFloat() : Mathf.Repeat(baseHue + random.SignedFloat() * range * 0.35f, 1f);
            float span = unrestricted ? 1f : range * Mathf.Lerp(0.45f, 1f, random.NextFloat());

            SpeciesColorPaletteRecord record = new SpeciesColorPaletteRecord
            {
                plantDefName = plantDef?.defName,
                unrestricted = unrestricted
            };
            for (int i = 0; i < count; i++)
            {
                float position = count == 1 ? 0f : i / (float)(count - 1) - 0.5f;
                float hue = Mathf.Repeat(center + position * span + random.SignedFloat() * span * 0.09f, 1f);
                float saturation = Mathf.Lerp(minimumSaturation, maximumSaturation, random.NextFloat());
                float value = Mathf.Lerp(minimumValue, maximumValue, random.NextFloat());
                record.packedColors.Add(SpeciesColorPaletteRecord.Pack(Color.HSVToRGB(hue, saturation, value)));
            }
            record.packedColors = record.packedColors.Distinct().ToList();
            return record;
        }

        public static VarietyTraitDef SelectTrait(VarietyTraitDef root, ThingDef cropDef)
        {
            SpeciesColorPaletteRecord palette = GameComponent_NovelSeeds.Instance?.PaletteFor(cropDef);
            Color color = palette?.Colors.RandomElementWithFallback(BaseColor(cropDef)) ?? BaseColor(cropDef);
            return ColorTraitFactory.TraitForColor(root.configFamily, color);
        }

        public static Color Constrain(ThingDef cropDef, Color color)
        {
            List<Color> colors = GameComponent_NovelSeeds.Instance?.PaletteFor(cropDef)?.Colors.ToList();
            return colors.NullOrEmpty() ? color : colors.OrderBy(candidate => PigmentColorUtility.PerceptualDistance(candidate, color)).First();
        }

        public static Color BaseColor(ThingDef plantDef)
        {
            Color color = plantDef?.graphicData?.color ?? Color.white;
            if (color == Color.white && plantDef?.plant?.harvestedThingDef?.graphicData != null)
                color = plantDef.plant.harvestedThingDef.graphicData.color;
            Color.RGBToHSV(color, out float hue, out float saturation, out float value);
            if (saturation < 0.12f) saturation = 0.55f;
            if (value < 0.25f) value = 0.7f;
            return Color.HSVToRGB(hue, saturation, value);
        }

        private sealed class StableRandom
        {
            private uint state;
            public StableRandom(string input)
            {
                state = 2166136261u;
                foreach (char character in input ?? string.Empty) state = (state ^ character) * 16777619u;
                if (state == 0u) state = 1u;
            }
            public float NextFloat()
            {
                state ^= state << 13; state ^= state >> 17; state ^= state << 5;
                return (state & 0x00ffffffu) / 16777216f;
            }
            public float SignedFloat() => NextFloat() * 2f - 1f;
            public int Range(int minimum, int maximum) => minimum + Mathf.FloorToInt(NextFloat() * Mathf.Max(1, maximum - minimum));
        }
    }
}
