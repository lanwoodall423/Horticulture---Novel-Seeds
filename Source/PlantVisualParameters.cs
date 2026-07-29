using UnityEngine;

namespace HorticultureNovelSeeds
{
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