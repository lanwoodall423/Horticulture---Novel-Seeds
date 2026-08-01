using UnityEngine;

namespace HorticultureNovelSeeds
{
    public static class PlantVisualColorUtility
    {
        public static Color Apply(Color source, float tintRed, float tintGreen, float tintBlue,
            float hueShift, float saturation, float brightness, float contrast, float opacity,
            float dullness, float strength = 1f)
        {
            float originalAlpha = source.a;
            Color.RGBToHSV(source, out float sourceHue, out float sourceSaturation, out float sourceValue);

            float targetHue = Mathf.Repeat(sourceHue + hueShift, 1f);
            float targetSaturation = Mathf.Clamp01(sourceSaturation * saturation);
            float tintMaximum = Mathf.Max(tintRed, Mathf.Max(tintGreen, tintBlue));
            float tintMinimum = Mathf.Min(tintRed, Mathf.Min(tintGreen, tintBlue));
            float tintChroma = Mathf.Clamp01(tintMaximum - tintMinimum);
            if (tintChroma > 0.001f)
            {
                Color tint = new Color(Mathf.Clamp01(tintRed), Mathf.Clamp01(tintGreen), Mathf.Clamp01(tintBlue));
                Color.RGBToHSV(tint, out float tintHue, out float tintSaturation, out _);
                targetHue = LerpHue(targetHue, tintHue, tintChroma);
                targetSaturation = Mathf.Lerp(targetSaturation,
                    Mathf.Max(targetSaturation, tintSaturation), tintChroma);
            }
            targetSaturation *= Mathf.Lerp(1f, 0.35f, Mathf.Clamp01(dullness));

            float targetValue = Mathf.Clamp01(((sourceValue - 0.5f) * contrast + 0.5f)
                * brightness * Mathf.Max(0f, tintMaximum));
            Color styled = Color.HSVToRGB(targetHue, Mathf.Clamp01(targetSaturation), targetValue, false);
            float outlineStrength = Mathf.Lerp(0.12f, 1f,
                Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.06f, 0.34f, sourceValue)));
            Color result = Color.Lerp(source, styled, Mathf.Clamp01(strength) * outlineStrength);
            result.a = originalAlpha * Mathf.Clamp01(opacity);
            return result;
        }

        private static float LerpHue(float from, float to, float amount)
        {
            return Mathf.Repeat(from + Mathf.DeltaAngle(from * 360f, to * 360f) / 360f * amount, 1f);
        }
    }

    public struct PlantVisualParameters
    {
        public float scale, width, height, density, spread;
        public float rotation, rotationVariation, scaleVariation, offsetX, offsetZ, shadowScale;
        public float tintRed, tintGreen, tintBlue, hueShift, saturation, brightness, contrast, opacity, dullness;
        public float radiance, radianceScale, radianceRed, radianceGreen, radianceBlue;
        public float gloom, gloomScale, gloomRed, gloomGreen, gloomBlue;
        public int overlayPattern;
        public float overlayIntensity, overlayScale, overlayRed, overlayGreen, overlayBlue;

        public static PlantVisualParameters Default => new PlantVisualParameters
        {
            scale = 1f, width = 1f, height = 1f, density = 1f, spread = 1f, shadowScale = 1f,
            tintRed = 1f, tintGreen = 1f, tintBlue = 1f, saturation = 1f, brightness = 1f,
            contrast = 1f, opacity = 1f, radianceScale = 1f, gloomScale = 1f,
            radianceRed = 1f, radianceGreen = 0.77f, radianceBlue = 0.27f,
            gloomRed = 0.36f, gloomGreen = 0.27f, gloomBlue = 0.41f,
            overlayIntensity = 1f, overlayScale = 1f,
            overlayRed = 0.19f, overlayGreen = 0.15f, overlayBlue = 0.15f
        };

        public Color Tint => new Color(tintRed, tintGreen, tintBlue, opacity);
        public Color RadianceColor => new Color(radianceRed, radianceGreen, radianceBlue, 1f);
        public Color GloomColor => new Color(gloomRed, gloomGreen, gloomBlue, 1f);
        public Color OverlayColor => new Color(overlayRed, overlayGreen, overlayBlue, 1f);

        public bool IsDefault =>
            Mathf.Approximately(scale, 1f) && Mathf.Approximately(width, 1f) && Mathf.Approximately(height, 1f)
            && Mathf.Approximately(density, 1f) && Mathf.Approximately(spread, 1f) && Mathf.Approximately(rotation, 0f)
            && Mathf.Approximately(rotationVariation, 0f) && Mathf.Approximately(scaleVariation, 0f)
            && Mathf.Approximately(offsetX, 0f) && Mathf.Approximately(offsetZ, 0f) && Mathf.Approximately(shadowScale, 1f)
            && Mathf.Approximately(tintRed, 1f) && Mathf.Approximately(tintGreen, 1f) && Mathf.Approximately(tintBlue, 1f)
            && Mathf.Approximately(hueShift, 0f) && Mathf.Approximately(saturation, 1f) && Mathf.Approximately(brightness, 1f)
            && Mathf.Approximately(contrast, 1f) && Mathf.Approximately(opacity, 1f) && Mathf.Approximately(dullness, 0f)
            && radiance <= 0f && gloom <= 0f && overlayPattern == 0;
    }
}