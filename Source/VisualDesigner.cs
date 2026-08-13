using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace HorticultureNovelSeeds
{
    public class VisualSettingsRecord : IExposable
    {
        public string instanceName = "Visual 1";
        public bool targetPlantProduce = true;
        public bool targetPlantLeaves = true;
        public bool targetPlantStem = true;
        public bool targetProduceProduce = true;
        public bool targetProduceLeaves = true;
        public bool targetProduceContainer = true;

        public float scale = 1f;
        public float width = 1f;
        public float height = 1f;
        public float density = 1f;
        public float spread = 1f;
        public float rotation;
        public float rotationVariation;
        public float scaleVariation;
        public float offsetX;
        public float offsetZ;
        public float shadowScale = 1f;

        public float tintRed = 1f;
        public float tintGreen = 1f;
        public float tintBlue = 1f;
        public float hueShift;
        public float saturation = 1f;
        public float brightness = 1f;
        public float contrast = 1f;
        public float opacity = 1f;
        public float dullness;

        public bool applyToProduce = true;
        public bool produceUsesPlantColor = true;
        public float produceTintRed = 1f;
        public float produceTintGreen = 1f;
        public float produceTintBlue = 1f;
        public float produceHueShift;
        public float produceSaturation = 1f;
        public float produceBrightness = 1f;
        public float produceContrast = 1f;
        public float produceOpacity = 1f;
        public float produceDullness;
        public float produceScale = 1f;
        public float produceWidth = 1f;
        public float produceHeight = 1f;
        public float produceDensity = 1f;
        public float produceSpread = 1f;
        public float produceRotation;
        public float produceRotationVariation;
        public float produceScaleVariation;
        public float produceOffsetX;
        public float produceOffsetZ;
        public float produceShadowScale = 1f;
        public float produceRadiance;
        public float produceRadianceScale = 1f;
        public float produceRadianceRed = 1f;
        public float produceRadianceGreen = 0.77f;
        public float produceRadianceBlue = 0.27f;
        public float produceGloom;
        public float produceGloomScale = 1f;
        public float produceGloomRed = 0.36f;
        public float produceGloomGreen = 0.27f;
        public float produceGloomBlue = 0.41f;
        public int produceOverlayPattern;
        public float produceOverlayIntensity = 1f;
        public float produceOverlayScale = 1f;
        public float produceOverlayRed = 0.19f;
        public float produceOverlayGreen = 0.15f;
        public float produceOverlayBlue = 0.15f;
        public bool produceSpikes;
        private bool produceVisualInitialized;

        public float radiance;
        public float radianceScale = 1f;
        public float radianceRed = 1f;
        public float radianceGreen = 0.77f;
        public float radianceBlue = 0.27f;
        public float gloom;
        public float gloomScale = 1f;
        public float gloomRed = 0.36f;
        public float gloomGreen = 0.27f;
        public float gloomBlue = 0.41f;
        public int overlayPattern;
        public float overlayIntensity = 1f;
        public float overlayScale = 1f;
        public float overlayRed = 0.19f;
        public float overlayGreen = 0.15f;
        public float overlayBlue = 0.15f;
        public bool spikes;

        public VisualSettingsRecord() { }
        public VisualSettingsRecord(VarietyTraitDef trait) { CopyFrom(trait); }

        public void CopyFrom(VarietyTraitDef trait)
        {
            instanceName = "Visual 1";
            targetPlantProduce = targetPlantLeaves = targetPlantStem = true;
            targetProduceProduce = targetProduceLeaves = targetProduceContainer = true;
            scale = trait?.visualScale > 0f ? trait.visualScale : 1f;
            width = trait?.visualWidth > 0f ? trait.visualWidth : 1f;
            height = trait?.visualHeight > 0f ? trait.visualHeight : 1f;
            density = trait?.visualDensity > 0f ? trait.visualDensity : 1f;
            spread = shadowScale = saturation = brightness = contrast = opacity = radianceScale = gloomScale = overlayIntensity = overlayScale = 1f;
            rotation = rotationVariation = scaleVariation = offsetX = offsetZ = hueShift = 0f;
            tintRed = trait?.tintRed ?? 1f;
            tintGreen = trait?.tintGreen ?? 1f;
            tintBlue = trait?.tintBlue ?? 1f;
            dullness = trait?.visualDullness ?? 0f;
            applyToProduce = true;
            produceUsesPlantColor = true;
            produceTintRed = tintRed; produceTintGreen = tintGreen; produceTintBlue = tintBlue;
            produceHueShift = hueShift; produceSaturation = saturation; produceBrightness = brightness;
            produceContrast = contrast; produceOpacity = opacity; produceDullness = dullness;
            radiance = trait?.visualRadiance ?? 0f;
            radianceRed = 1f; radianceGreen = 0.77f; radianceBlue = 0.27f;
            gloom = trait?.visualGloom ?? 0f;
            gloomRed = 0.36f; gloomGreen = 0.27f; gloomBlue = 0.41f;
            overlayPattern = trait?.visualSpikes == true ? 1 : 0;
            overlayRed = 0.19f; overlayGreen = 0.15f; overlayBlue = 0.15f;
            spikes = trait?.visualSpikes == true;
            CopyPlantVisualToProduce();
            produceVisualInitialized = true;
            produceUsesPlantColor = false;
            if (trait?.produceOnlyVisual == true)
            {
                tintRed = tintGreen = tintBlue = 1f;
                hueShift = 0f;
                saturation = brightness = contrast = opacity = 1f;
                dullness = 0f;
                scale = width = height = density = spread = shadowScale = 1f;
                rotation = rotationVariation = scaleVariation = offsetX = offsetZ = 0f;
                radiance = gloom = 0f;
                overlayPattern = 0;
                spikes = false;
                targetPlantProduce = targetPlantLeaves = targetPlantStem = false;
                targetProduceProduce = true;
                targetProduceLeaves = targetProduceContainer = false;
            }
            Normalize();
        }

        public void CopyFrom(VisualSettingsRecord other)
        {
            if (other == null) return;
            instanceName = other.instanceName;
            targetPlantProduce = other.targetPlantProduce; targetPlantLeaves = other.targetPlantLeaves; targetPlantStem = other.targetPlantStem;
            targetProduceProduce = other.targetProduceProduce; targetProduceLeaves = other.targetProduceLeaves; targetProduceContainer = other.targetProduceContainer;
            scale = other.scale; width = other.width; height = other.height; density = other.density; spread = other.spread;
            rotation = other.rotation; rotationVariation = other.rotationVariation; scaleVariation = other.scaleVariation;
            offsetX = other.offsetX; offsetZ = other.offsetZ; shadowScale = other.shadowScale;
            tintRed = other.tintRed; tintGreen = other.tintGreen; tintBlue = other.tintBlue; hueShift = other.hueShift;
            saturation = other.saturation; brightness = other.brightness; contrast = other.contrast; opacity = other.opacity; dullness = other.dullness;
            applyToProduce = other.applyToProduce; produceUsesPlantColor = other.produceUsesPlantColor;
            produceTintRed = other.produceTintRed; produceTintGreen = other.produceTintGreen; produceTintBlue = other.produceTintBlue;
            produceHueShift = other.produceHueShift; produceSaturation = other.produceSaturation; produceBrightness = other.produceBrightness;
            produceContrast = other.produceContrast; produceOpacity = other.produceOpacity; produceDullness = other.produceDullness;
            produceScale = other.produceScale; produceWidth = other.produceWidth; produceHeight = other.produceHeight; produceDensity = other.produceDensity; produceSpread = other.produceSpread;
            produceRotation = other.produceRotation; produceRotationVariation = other.produceRotationVariation; produceScaleVariation = other.produceScaleVariation;
            produceOffsetX = other.produceOffsetX; produceOffsetZ = other.produceOffsetZ; produceShadowScale = other.produceShadowScale;
            produceRadiance = other.produceRadiance; produceRadianceScale = other.produceRadianceScale; produceRadianceRed = other.produceRadianceRed; produceRadianceGreen = other.produceRadianceGreen; produceRadianceBlue = other.produceRadianceBlue;
            produceGloom = other.produceGloom; produceGloomScale = other.produceGloomScale; produceGloomRed = other.produceGloomRed; produceGloomGreen = other.produceGloomGreen; produceGloomBlue = other.produceGloomBlue;
            produceOverlayPattern = other.produceOverlayPattern; produceOverlayIntensity = other.produceOverlayIntensity; produceOverlayScale = other.produceOverlayScale;
            produceOverlayRed = other.produceOverlayRed; produceOverlayGreen = other.produceOverlayGreen; produceOverlayBlue = other.produceOverlayBlue; produceSpikes = other.produceSpikes;
            produceVisualInitialized = other.produceVisualInitialized;
            radiance = other.radiance; radianceScale = other.radianceScale; radianceRed = other.radianceRed; radianceGreen = other.radianceGreen; radianceBlue = other.radianceBlue;
            gloom = other.gloom; gloomScale = other.gloomScale; gloomRed = other.gloomRed; gloomGreen = other.gloomGreen; gloomBlue = other.gloomBlue;
            overlayPattern = other.overlayPattern; overlayIntensity = other.overlayIntensity; overlayScale = other.overlayScale;
            overlayRed = other.overlayRed; overlayGreen = other.overlayGreen; overlayBlue = other.overlayBlue; spikes = other.spikes;
            Normalize();
        }

        public VisualSettingsRecord Clone() { VisualSettingsRecord result = new VisualSettingsRecord(); result.CopyFrom(this); return result; }

        public void ExposeData()
        {
            Scribe_Values.Look(ref instanceName, "instanceName", "Visual 1");
            Scribe_Values.Look(ref targetPlantProduce, "targetPlantProduce", true); Scribe_Values.Look(ref targetPlantLeaves, "targetPlantLeaves", true); Scribe_Values.Look(ref targetPlantStem, "targetPlantStem", true);
            Scribe_Values.Look(ref targetProduceProduce, "targetProduceProduce", true); Scribe_Values.Look(ref targetProduceLeaves, "targetProduceLeaves", true); Scribe_Values.Look(ref targetProduceContainer, "targetProduceContainer", true);
            Scribe_Values.Look(ref scale, "scale", 1f); Scribe_Values.Look(ref width, "width", 1f); Scribe_Values.Look(ref height, "height", 1f);
            Scribe_Values.Look(ref density, "density", 1f); Scribe_Values.Look(ref spread, "spread", 1f);
            Scribe_Values.Look(ref rotation, "rotation", 0f); Scribe_Values.Look(ref rotationVariation, "rotationVariation", 0f);
            Scribe_Values.Look(ref scaleVariation, "scaleVariation", 0f); Scribe_Values.Look(ref offsetX, "offsetX", 0f); Scribe_Values.Look(ref offsetZ, "offsetZ", 0f);
            Scribe_Values.Look(ref shadowScale, "shadowScale", 1f);
            Scribe_Values.Look(ref tintRed, "tintRed", 1f); Scribe_Values.Look(ref tintGreen, "tintGreen", 1f); Scribe_Values.Look(ref tintBlue, "tintBlue", 1f);
            Scribe_Values.Look(ref hueShift, "hueShift", 0f); Scribe_Values.Look(ref saturation, "saturation", 1f); Scribe_Values.Look(ref brightness, "brightness", 1f);
            Scribe_Values.Look(ref contrast, "contrast", 1f); Scribe_Values.Look(ref opacity, "opacity", 1f); Scribe_Values.Look(ref dullness, "dullness", 0f);
            Scribe_Values.Look(ref applyToProduce, "applyToProduce", true); Scribe_Values.Look(ref produceUsesPlantColor, "produceUsesPlantColor", true);
            Scribe_Values.Look(ref produceTintRed, "produceTintRed", 1f); Scribe_Values.Look(ref produceTintGreen, "produceTintGreen", 1f); Scribe_Values.Look(ref produceTintBlue, "produceTintBlue", 1f);
            Scribe_Values.Look(ref produceHueShift, "produceHueShift", 0f); Scribe_Values.Look(ref produceSaturation, "produceSaturation", 1f);
            Scribe_Values.Look(ref produceBrightness, "produceBrightness", 1f); Scribe_Values.Look(ref produceContrast, "produceContrast", 1f);
            Scribe_Values.Look(ref produceOpacity, "produceOpacity", 1f); Scribe_Values.Look(ref produceDullness, "produceDullness", 0f);
            Scribe_Values.Look(ref produceScale, "produceScale", 1f); Scribe_Values.Look(ref produceWidth, "produceWidth", 1f); Scribe_Values.Look(ref produceHeight, "produceHeight", 1f);
            Scribe_Values.Look(ref produceDensity, "produceDensity", 1f); Scribe_Values.Look(ref produceSpread, "produceSpread", 1f);
            Scribe_Values.Look(ref produceRotation, "produceRotation", 0f); Scribe_Values.Look(ref produceRotationVariation, "produceRotationVariation", 0f);
            Scribe_Values.Look(ref produceScaleVariation, "produceScaleVariation", 0f); Scribe_Values.Look(ref produceOffsetX, "produceOffsetX", 0f); Scribe_Values.Look(ref produceOffsetZ, "produceOffsetZ", 0f);
            Scribe_Values.Look(ref produceShadowScale, "produceShadowScale", 1f);
            Scribe_Values.Look(ref produceRadiance, "produceRadiance", 0f); Scribe_Values.Look(ref produceRadianceScale, "produceRadianceScale", 1f);
            Scribe_Values.Look(ref produceRadianceRed, "produceRadianceRed", 1f); Scribe_Values.Look(ref produceRadianceGreen, "produceRadianceGreen", 0.77f); Scribe_Values.Look(ref produceRadianceBlue, "produceRadianceBlue", 0.27f);
            Scribe_Values.Look(ref produceGloom, "produceGloom", 0f); Scribe_Values.Look(ref produceGloomScale, "produceGloomScale", 1f);
            Scribe_Values.Look(ref produceGloomRed, "produceGloomRed", 0.36f); Scribe_Values.Look(ref produceGloomGreen, "produceGloomGreen", 0.27f); Scribe_Values.Look(ref produceGloomBlue, "produceGloomBlue", 0.41f);
            Scribe_Values.Look(ref produceOverlayPattern, "produceOverlayPattern", 0); Scribe_Values.Look(ref produceOverlayIntensity, "produceOverlayIntensity", 1f); Scribe_Values.Look(ref produceOverlayScale, "produceOverlayScale", 1f);
            Scribe_Values.Look(ref produceOverlayRed, "produceOverlayRed", 0.19f); Scribe_Values.Look(ref produceOverlayGreen, "produceOverlayGreen", 0.15f); Scribe_Values.Look(ref produceOverlayBlue, "produceOverlayBlue", 0.15f);
            Scribe_Values.Look(ref produceSpikes, "produceSpikes", false); Scribe_Values.Look(ref produceVisualInitialized, "produceVisualInitialized", false);
            Scribe_Values.Look(ref radiance, "radiance", 0f); Scribe_Values.Look(ref radianceScale, "radianceScale", 1f);
            Scribe_Values.Look(ref radianceRed, "radianceRed", 1f); Scribe_Values.Look(ref radianceGreen, "radianceGreen", 0.77f); Scribe_Values.Look(ref radianceBlue, "radianceBlue", 0.27f);
            Scribe_Values.Look(ref gloom, "gloom", 0f); Scribe_Values.Look(ref gloomScale, "gloomScale", 1f);
            Scribe_Values.Look(ref gloomRed, "gloomRed", 0.36f); Scribe_Values.Look(ref gloomGreen, "gloomGreen", 0.27f); Scribe_Values.Look(ref gloomBlue, "gloomBlue", 0.41f);
            Scribe_Values.Look(ref overlayPattern, "overlayPattern", 0); Scribe_Values.Look(ref overlayIntensity, "overlayIntensity", 1f); Scribe_Values.Look(ref overlayScale, "overlayScale", 1f);
            Scribe_Values.Look(ref overlayRed, "overlayRed", 0.19f); Scribe_Values.Look(ref overlayGreen, "overlayGreen", 0.15f); Scribe_Values.Look(ref overlayBlue, "overlayBlue", 0.15f);
            Scribe_Values.Look(ref spikes, "spikes", false);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (spikes && overlayPattern == 0) overlayPattern = 1;
                Normalize();
            }
        }

        public void Normalize()
        {
            applyToProduce = true;
            if (instanceName.NullOrEmpty()) instanceName = "Visual 1";
            instanceName = instanceName.Trim();
            if (!produceVisualInitialized)
            {
                CopyPlantVisualToProduce();
                produceVisualInitialized = true;
            }
            produceUsesPlantColor = false;
            scale = Mathf.Clamp(scale, 0.25f, 3f); width = Mathf.Clamp(width, 0.25f, 2f); height = Mathf.Clamp(height, 0.25f, 2f);
            density = Mathf.Clamp(density, 0.25f, 2f); spread = Mathf.Clamp(spread, 0.1f, 2f);
            rotation = Mathf.Clamp(rotation, -180f, 180f); rotationVariation = Mathf.Clamp(rotationVariation, 0f, 180f); scaleVariation = Mathf.Clamp(scaleVariation, 0f, 0.75f);
            offsetX = Mathf.Clamp(offsetX, -0.5f, 0.5f); offsetZ = Mathf.Clamp(offsetZ, -0.5f, 0.5f); shadowScale = Mathf.Clamp(shadowScale, 0f, 2f);
            tintRed = Mathf.Clamp(tintRed, 0f, 2f); tintGreen = Mathf.Clamp(tintGreen, 0f, 2f); tintBlue = Mathf.Clamp(tintBlue, 0f, 2f);
            hueShift = Mathf.Clamp(hueShift, -0.5f, 0.5f); saturation = Mathf.Clamp(saturation, 0f, 2f); brightness = Mathf.Clamp(brightness, 0.25f, 2f);
            contrast = Mathf.Clamp(contrast, 0.25f, 2f); opacity = Mathf.Clamp(opacity, 0.1f, 1f); dullness = Mathf.Clamp01(dullness);
            produceTintRed = Mathf.Clamp(produceTintRed, 0f, 2f); produceTintGreen = Mathf.Clamp(produceTintGreen, 0f, 2f); produceTintBlue = Mathf.Clamp(produceTintBlue, 0f, 2f);
            produceHueShift = Mathf.Clamp(produceHueShift, -0.5f, 0.5f); produceSaturation = Mathf.Clamp(produceSaturation, 0f, 2f);
            produceBrightness = Mathf.Clamp(produceBrightness, 0.25f, 2f); produceContrast = Mathf.Clamp(produceContrast, 0.25f, 2f);
            produceOpacity = Mathf.Clamp(produceOpacity, 0.1f, 1f); produceDullness = Mathf.Clamp01(produceDullness);
            produceScale = Mathf.Clamp(produceScale, 0.25f, 3f); produceWidth = Mathf.Clamp(produceWidth, 0.25f, 2f); produceHeight = Mathf.Clamp(produceHeight, 0.25f, 2f);
            produceDensity = Mathf.Clamp(produceDensity, 0.25f, 2f); produceSpread = Mathf.Clamp(produceSpread, 0.1f, 2f);
            produceRotation = Mathf.Clamp(produceRotation, -180f, 180f); produceRotationVariation = Mathf.Clamp(produceRotationVariation, 0f, 180f); produceScaleVariation = Mathf.Clamp(produceScaleVariation, 0f, 0.75f);
            produceOffsetX = Mathf.Clamp(produceOffsetX, -0.5f, 0.5f); produceOffsetZ = Mathf.Clamp(produceOffsetZ, -0.5f, 0.5f); produceShadowScale = Mathf.Clamp(produceShadowScale, 0f, 2f);
            produceRadiance = Mathf.Clamp01(produceRadiance); produceRadianceScale = Mathf.Clamp(produceRadianceScale, 0.5f, 2.5f);
            produceRadianceRed = Mathf.Clamp01(produceRadianceRed); produceRadianceGreen = Mathf.Clamp01(produceRadianceGreen); produceRadianceBlue = Mathf.Clamp01(produceRadianceBlue);
            produceGloom = Mathf.Clamp01(produceGloom); produceGloomScale = Mathf.Clamp(produceGloomScale, 0.5f, 2.5f);
            produceGloomRed = Mathf.Clamp01(produceGloomRed); produceGloomGreen = Mathf.Clamp01(produceGloomGreen); produceGloomBlue = Mathf.Clamp01(produceGloomBlue);
            produceOverlayPattern = Mathf.Clamp(produceOverlayPattern, 0, 5); produceOverlayIntensity = Mathf.Clamp01(produceOverlayIntensity); produceOverlayScale = Mathf.Clamp(produceOverlayScale, 0.5f, 2f);
            produceOverlayRed = Mathf.Clamp01(produceOverlayRed); produceOverlayGreen = Mathf.Clamp01(produceOverlayGreen); produceOverlayBlue = Mathf.Clamp01(produceOverlayBlue);
            produceSpikes = produceOverlayPattern == 1;
            radiance = Mathf.Clamp01(radiance); radianceScale = Mathf.Clamp(radianceScale, 0.5f, 2.5f);
            radianceRed = Mathf.Clamp01(radianceRed); radianceGreen = Mathf.Clamp01(radianceGreen); radianceBlue = Mathf.Clamp01(radianceBlue);
            gloom = Mathf.Clamp01(gloom); gloomScale = Mathf.Clamp(gloomScale, 0.5f, 2.5f);
            gloomRed = Mathf.Clamp01(gloomRed); gloomGreen = Mathf.Clamp01(gloomGreen); gloomBlue = Mathf.Clamp01(gloomBlue);
            overlayPattern = Mathf.Clamp(overlayPattern, 0, 5); overlayIntensity = Mathf.Clamp01(overlayIntensity); overlayScale = Mathf.Clamp(overlayScale, 0.5f, 2f);
            overlayRed = Mathf.Clamp01(overlayRed); overlayGreen = Mathf.Clamp01(overlayGreen); overlayBlue = Mathf.Clamp01(overlayBlue);
            spikes = overlayPattern == 1;
        }

        public bool TargetsPlantMask(int index) => index == 0 ? targetPlantProduce : index == 1 ? targetPlantLeaves : targetPlantStem;
        public bool TargetsProduceMask(int index) => index == 0 ? targetProduceProduce : index == 1 ? targetProduceLeaves : targetProduceContainer;
        public bool HasAnyPlantTarget => targetPlantProduce || targetPlantLeaves || targetPlantStem;
        public bool HasAnyProduceTarget => targetProduceProduce || targetProduceLeaves || targetProduceContainer;

        public void SetPlantMaskTarget(int index, bool value)
        {
            if (index == 0) targetPlantProduce = value;
            else if (index == 1) targetPlantLeaves = value;
            else targetPlantStem = value;
        }

        public void SetProduceMaskTarget(int index, bool value)
        {
            if (index == 0) targetProduceProduce = value;
            else if (index == 1) targetProduceLeaves = value;
            else targetProduceContainer = value;
        }

        public void CopyPlantColorToProduce()
        {
            produceTintRed = tintRed; produceTintGreen = tintGreen; produceTintBlue = tintBlue;
            produceHueShift = hueShift; produceSaturation = saturation; produceBrightness = brightness;
            produceContrast = contrast; produceOpacity = opacity; produceDullness = dullness;
        }

        public void CopyProduceSettingsFrom(VisualSettingsRecord other)
        {
            if (other == null) return;
            applyToProduce = other.applyToProduce;
            produceUsesPlantColor = other.produceUsesPlantColor;
            produceTintRed = other.produceTintRed; produceTintGreen = other.produceTintGreen; produceTintBlue = other.produceTintBlue;
            produceHueShift = other.produceHueShift; produceSaturation = other.produceSaturation; produceBrightness = other.produceBrightness;
            produceContrast = other.produceContrast; produceOpacity = other.produceOpacity; produceDullness = other.produceDullness;
            produceScale = other.produceScale; produceWidth = other.produceWidth; produceHeight = other.produceHeight; produceDensity = other.produceDensity; produceSpread = other.produceSpread;
            produceRotation = other.produceRotation; produceRotationVariation = other.produceRotationVariation; produceScaleVariation = other.produceScaleVariation;
            produceOffsetX = other.produceOffsetX; produceOffsetZ = other.produceOffsetZ; produceShadowScale = other.produceShadowScale;
            produceRadiance = other.produceRadiance; produceRadianceScale = other.produceRadianceScale; produceRadianceRed = other.produceRadianceRed; produceRadianceGreen = other.produceRadianceGreen; produceRadianceBlue = other.produceRadianceBlue;
            produceGloom = other.produceGloom; produceGloomScale = other.produceGloomScale; produceGloomRed = other.produceGloomRed; produceGloomGreen = other.produceGloomGreen; produceGloomBlue = other.produceGloomBlue;
            produceOverlayPattern = other.produceOverlayPattern; produceOverlayIntensity = other.produceOverlayIntensity; produceOverlayScale = other.produceOverlayScale;
            produceOverlayRed = other.produceOverlayRed; produceOverlayGreen = other.produceOverlayGreen; produceOverlayBlue = other.produceOverlayBlue; produceSpikes = other.produceSpikes;
            produceVisualInitialized = other.produceVisualInitialized;
        }
        private void CopyPlantVisualToProduce()
        {
            CopyPlantColorToProduce();
            produceScale = scale; produceWidth = width; produceHeight = height; produceDensity = density; produceSpread = spread;
            produceRotation = rotation; produceRotationVariation = rotationVariation; produceScaleVariation = scaleVariation;
            produceOffsetX = offsetX; produceOffsetZ = offsetZ; produceShadowScale = shadowScale;
            produceRadiance = radiance; produceRadianceScale = radianceScale; produceRadianceRed = radianceRed; produceRadianceGreen = radianceGreen; produceRadianceBlue = radianceBlue;
            produceGloom = gloom; produceGloomScale = gloomScale; produceGloomRed = gloomRed; produceGloomGreen = gloomGreen; produceGloomBlue = gloomBlue;
            produceOverlayPattern = overlayPattern; produceOverlayIntensity = overlayIntensity; produceOverlayScale = overlayScale;
            produceOverlayRed = overlayRed; produceOverlayGreen = overlayGreen; produceOverlayBlue = overlayBlue; produceSpikes = spikes;
        }

        public VisualSettingsRecord CreateProduceVisualEditor()
        {
            Normalize();
            VisualSettingsRecord editor = new VisualSettingsRecord
            {
                applyToProduce = applyToProduce,
                scale = produceScale, width = produceWidth, height = produceHeight, density = produceDensity, spread = produceSpread,
                rotation = produceRotation, rotationVariation = produceRotationVariation, scaleVariation = produceScaleVariation,
                offsetX = produceOffsetX, offsetZ = produceOffsetZ, shadowScale = produceShadowScale,
                tintRed = produceTintRed, tintGreen = produceTintGreen, tintBlue = produceTintBlue, hueShift = produceHueShift,
                saturation = produceSaturation, brightness = produceBrightness, contrast = produceContrast, opacity = produceOpacity, dullness = produceDullness,
                radiance = produceRadiance, radianceScale = produceRadianceScale,
                radianceRed = produceRadianceRed, radianceGreen = produceRadianceGreen, radianceBlue = produceRadianceBlue,
                gloom = produceGloom, gloomScale = produceGloomScale,
                gloomRed = produceGloomRed, gloomGreen = produceGloomGreen, gloomBlue = produceGloomBlue,
                overlayPattern = produceOverlayPattern, overlayIntensity = produceOverlayIntensity, overlayScale = produceOverlayScale,
                overlayRed = produceOverlayRed, overlayGreen = produceOverlayGreen, overlayBlue = produceOverlayBlue, spikes = produceSpikes
            };
            editor.Normalize();
            return editor;
        }

        public void ApplyProduceVisualEditor(VisualSettingsRecord editor)
        {
            if (editor == null) return;
            editor.Normalize();
            applyToProduce = editor.applyToProduce;
            produceTintRed = editor.tintRed; produceTintGreen = editor.tintGreen; produceTintBlue = editor.tintBlue; produceHueShift = editor.hueShift;
            produceSaturation = editor.saturation; produceBrightness = editor.brightness; produceContrast = editor.contrast; produceOpacity = editor.opacity; produceDullness = editor.dullness;
            produceScale = editor.scale; produceWidth = editor.width; produceHeight = editor.height; produceDensity = editor.density; produceSpread = editor.spread;
            produceRotation = editor.rotation; produceRotationVariation = editor.rotationVariation; produceScaleVariation = editor.scaleVariation;
            produceOffsetX = editor.offsetX; produceOffsetZ = editor.offsetZ; produceShadowScale = editor.shadowScale;
            produceRadiance = editor.radiance; produceRadianceScale = editor.radianceScale; produceRadianceRed = editor.radianceRed; produceRadianceGreen = editor.radianceGreen; produceRadianceBlue = editor.radianceBlue;
            produceGloom = editor.gloom; produceGloomScale = editor.gloomScale; produceGloomRed = editor.gloomRed; produceGloomGreen = editor.gloomGreen; produceGloomBlue = editor.gloomBlue;
            produceOverlayPattern = editor.overlayPattern; produceOverlayIntensity = editor.overlayIntensity; produceOverlayScale = editor.overlayScale;
            produceOverlayRed = editor.overlayRed; produceOverlayGreen = editor.overlayGreen; produceOverlayBlue = editor.overlayBlue; produceSpikes = editor.spikes;
            produceUsesPlantColor = false;
            produceVisualInitialized = true;
            Normalize();
        }
    }
    [StaticConstructorOnStartup]
    public class Dialog_TraitVisualDesigner : Window, IHorticultureVisualDesignerSurface
    {
        private static readonly string[] TabLabels = { "Color", "Shape", "Effects" };
        private static readonly string[] MaskLabels = { "Plant: Produce", "Plant: Leaves", "Plant: Stem", "Produce: Produce", "Produce: Leaves", "Produce: Container" };
        private static readonly string[] OverlayLabels = { "None", "Spikes", "Spots", "Stripes", "Veins", "Speckles" };
        private static readonly Vector2[] PreviewOffsets =
        {
            new Vector2(0f, 0f), new Vector2(-0.18f, 0.13f), new Vector2(0.19f, 0.11f),
            new Vector2(-0.15f, -0.16f), new Vector2(0.17f, -0.15f)
        };
        private static Texture2D radialTexture;
        private static readonly Texture2D[] overlayTextures = new Texture2D[6];

        private readonly VarietyTraitDef trait;
        private readonly ThingDef plant;
        private readonly PlantGroupRecord plantGroup;
        private readonly GlobalTraitSettingsRecord globalRecord;
        private readonly TraitSettingsRecord plantRecord;
        private readonly OptionWeightRecord subtypeRecord;
        private readonly List<VisualSettingsRecord> inheritedVisuals;
        private ThingDef previewPlant;
        private bool editingProduce;
        private string activeSection = "Color";
        private VisualSettingsRecord produceEditor;
        private int selectedMask;
        private VisualMaskLayerRecord previewMaskLayer;
        private Texture2D previewMaskTexture;
        private int previewMaskHash;
        private int previewMaskSourceId;
        private readonly Dictionary<int, Texture2D> styledPreviewTextures = new Dictionary<int, Texture2D>();
        private int styledPreviewHash;
        private readonly List<ThingDef> previewPlants = new List<ThingDef>();
        private HorticultureVisualDesignerDocument canvasDocument;

        public override Vector2 InitialSize => new Vector2(920f, 760f);

        public Dialog_TraitVisualDesigner(NovelSeedsSettings settings, VarietyTraitDef trait, ThingDef plant = null)
        {
            this.trait = trait;
            this.plant = plant;
            plantGroup = null;
            globalRecord = settings.GetGlobalTraitSettings(trait);
            if (plant != null) plantRecord = settings.GetPlantSettings(plant).GetTraitSettings(trait);
            inheritedVisuals = settings.GlobalVisualCopies(trait);
            previewPlant = ResolvePreviewPlant(plant);
            doCloseX = true;
            absorbInputAroundWindow = true;
            InitializeCanvas();
        }

        public Dialog_TraitVisualDesigner(NovelSeedsSettings settings, VarietyTraitDef trait, PlantGroupRecord group)
        {
            this.trait = trait;
            plantGroup = group;
            plant = group?.Plants.FirstOrDefault();
            globalRecord = settings.GetGlobalTraitSettings(trait);
            plantRecord = group?.Settings.GetTraitSettings(trait);
            inheritedVisuals = settings.GlobalVisualCopies(trait);
            previewPlant = ResolvePreviewPlant(plant);
            doCloseX = true;
            absorbInputAroundWindow = true;
            InitializeCanvas();
        }
        public Dialog_TraitVisualDesigner(NovelSeedsSettings settings, VarietyTraitDef trait, ThingDef previewPlant, bool previewOnly)
        {
            this.trait = trait;
            plantGroup = null;
            plant = null;
            globalRecord = settings.GetGlobalTraitSettings(trait);
            inheritedVisuals = settings.GlobalVisualCopies(trait);
            this.previewPlant = ResolvePreviewPlant(previewPlant);
            doCloseX = true;
            absorbInputAroundWindow = true;
            InitializeCanvas();
        }

        public Dialog_TraitVisualDesigner(NovelSeedsSettings settings, VarietyTraitDef trait, OptionWeightRecord subtypeRecord, ThingDef previewPlant = null)
        {
            this.trait = trait;
            this.subtypeRecord = subtypeRecord;
            subtypeRecord.EnsureVisual(trait);
            plantGroup = null;
            plant = null;
            globalRecord = settings.GetGlobalTraitSettings(TraitConfigUtility.Root(trait));
            inheritedVisuals = new List<VisualSettingsRecord>();
            this.previewPlant = ResolvePreviewPlant(previewPlant);
            doCloseX = true;
            absorbInputAroundWindow = true;
            InitializeCanvas();
        }

        private void InitializeCanvas()
        {
            previewPlants.Clear();
            previewPlants.AddRange(DefDatabase<ThingDef>.AllDefsListForReading
                .Where(NovelSeedUtility.IsGrowableCrop)
                .GroupBy(def => def.defName)
                .Select(group => group.First())
                .OrderBy(def => def.label)
                .Take(256));
            if (previewPlant != null && !previewPlants.Contains(previewPlant)) previewPlants.Insert(0, previewPlant);
            canvasDocument = new HorticultureVisualDesignerDocument(this);
        }

        private static ThingDef ResolvePreviewPlant(ThingDef requested)
        {
            ThingDef context = requested ?? NovelSeedsSettingsUI.CurrentPlantPreview;
            if (context?.plant != null) return context;
            List<ThingDef> maskedPlants = DefDatabase<ThingDef>.AllDefsListForReading
                .Where(NovelSeedUtility.IsGrowableCrop)
                .Where(def =>
                {
                    PlantSettingsRecord record = HorticultureNovelSeedsMod.Settings?.GetPlantSettings(def, false);
                    return PlantMaskUtility.HasActiveMasks(def) || record?.HasActiveProduceMasks == true;
                })
                .OrderBy(def => def.label)
                .ToList();
            ThingDef fullyMasked = maskedPlants.FirstOrDefault(def =>
            {
                PlantSettingsRecord record = HorticultureNovelSeedsMod.Settings?.GetPlantSettings(def, false);
                return PlantMaskUtility.HasActiveMasks(def) && record?.HasActiveProduceMasks == true;
            });
            if (fullyMasked != null) return fullyMasked;
            if (maskedPlants.Count > 0) return maskedPlants[0];
            return DefDatabase<ThingDef>.GetNamedSilentFail("Plant_Rice")
                ?? DefDatabase<ThingDef>.AllDefsListForReading.FirstOrDefault(NovelSeedUtility.IsGrowableCrop);
        }
        public override void DoWindowContents(Rect inRect)
        {
            canvasDocument?.Draw(inRect);
        }

        public override void PostClose()
        {
            canvasDocument?.PostClose();
            base.PostClose();
            ClearPreviewMaskTexture();
            HorticultureNovelSeedsMod.Settings?.ClearVisualCache();
            ProduceMaskRenderer.ClearAll();
        }

        string IHorticultureVisualDesignerSurface.ContextLabel =>
            (previewPlant?.LabelCap.ToString() ?? "Plant") + " Visual Designer";

        string IHorticultureVisualDesignerSurface.TraitLabel => TraitColorUI.Label(trait);

        string IHorticultureVisualDesignerSurface.OriginLabel
        {
            get
            {
                if (subtypeRecord != null) return "Subtype override";
                if (plantRecord == null) return "Global/default visual";
                if (plantRecord.useCustomVisual) return plantGroup != null ? "Group override" : "Plant-specific override";
                return "Inherited from Global";
            }
        }

        string IHorticultureVisualDesignerSurface.InheritanceLabel
        {
            get
            {
                if (subtypeRecord != null) return "Subtype-specific visual is active.";
                if (plantRecord == null) return "Global/default visual is active.";
                return plantRecord.useCustomVisual
                    ? (plantGroup != null ? "Group override is active." : "Plant-specific override is active.")
                    : "Inherited from Global. Enable an override to edit.";
            }
        }

        string IHorticultureVisualDesignerSurface.StatusLabel =>
            (plantRecord == null || plantRecord.useCustomVisual || subtypeRecord != null)
                ? "Changes are cached and applied through the existing visual pipeline."
                : "Enable the override to edit this inherited visual.";

        bool IHorticultureVisualDesignerSurface.EditingProduce => editingProduce;
        string IHorticultureVisualDesignerSurface.ActiveSection
        {
            get => activeSection;
            set => activeSection = string.IsNullOrEmpty(value) ? "Color" : value;
        }

        bool IHorticultureVisualDesignerSurface.CanEdit =>
            plantRecord == null || plantRecord.useCustomVisual || subtypeRecord != null;

        bool IHorticultureVisualDesignerSurface.OverrideEnabled
        {
            get => plantRecord?.useCustomVisual == true;
            set
            {
                if (plantRecord == null || subtypeRecord != null) return;
                bool changed = plantRecord.useCustomVisual != value;
                plantRecord.useCustomVisual = value;
                if (value && changed) plantRecord.CopyVisualsFrom(inheritedVisuals);
                if (changed)
                {
                    HorticultureNovelSeedsMod.Settings?.ClearVisualCache();
                    ProduceMaskRenderer.ClearAll();
                    ClearPreviewMaskTexture();
                }
            }
        }

        bool IHorticultureVisualDesignerSurface.PerMaskEnabled
        {
            get => CurrentUsesPerMaskVisuals;
            set
            {
                if (!((IHorticultureVisualDesignerSurface)this).CanEdit) return;
                SetPerMaskVisuals(value);
                selectedMask = editingProduce ? 3 : 0;
                ClearPreviewMaskTexture();
            }
        }

        int IHorticultureVisualDesignerSurface.SelectedMask
        {
            get => selectedMask;
            set
            {
                int first = editingProduce ? 3 : 0;
                selectedMask = Mathf.Clamp(value, first, first + 2);
                ClearPreviewMaskTexture();
            }
        }

        IReadOnlyList<string> IHorticultureVisualDesignerSurface.MaskOptions =>
            editingProduce
                ? new[] { "Produce", "Leaves", "Container" }
                : new[] { "Produce", "Leaves", "Stem" };

        IReadOnlyList<string> IHorticultureVisualDesignerSurface.PreviewPlantOptions =>
            previewPlants.Select(def => def.LabelCap.ToString()).ToArray();

        int IHorticultureVisualDesignerSurface.SelectedPreviewPlant
        {
            get
            {
                int index = previewPlants.IndexOf(previewPlant);
                return index < 0 ? 0 : index;
            }
            set
            {
                if (previewPlants.Count == 0) return;
                previewPlant = previewPlants[Mathf.Clamp(value, 0, previewPlants.Count - 1)];
                ClearPreviewMaskTexture();
            }
        }

        float IHorticultureVisualDesignerSurface.GetValue(string key)
        {
            VisualSettingsRecord value = EditingVisual;
            switch (key)
            {
                case "color.red": return value.tintRed;
                case "color.green": return value.tintGreen;
                case "color.blue": return value.tintBlue;
                case "color.saturation": return value.saturation;
                case "color.brightness": return value.brightness;
                case "color.hue": return value.hueShift;
                case "color.contrast": return value.contrast;
                case "color.opacity": return value.opacity;
                case "shape.scale": return value.scale;
                case "shape.width": return value.width;
                case "shape.height": return value.height;
                case "shape.density": return value.density;
                case "shape.spread": return value.spread;
                case "shape.rotation": return value.rotation;
                case "shape.offsetX": return value.offsetX;
                case "shape.offsetZ": return value.offsetZ;
                case "effects.apply": return value.applyToProduce ? 1f : 0f;
                case "effects.radiance": return value.radiance;
                case "effects.gloom": return value.gloom;
                case "effects.overlay": return value.overlayIntensity;
                case "effects.radianceScale": return value.radianceScale;
                case "effects.gloomScale": return value.gloomScale;
                case "effects.spikes": return value.spikes ? 1f : 0f;
                default: return 0f;
            }
        }

        void IHorticultureVisualDesignerSurface.SetValue(string key, float value)
        {
            VisualSettingsRecord target = EditingVisual;
            switch (key)
            {
                case "color.red": target.tintRed = value; break;
                case "color.green": target.tintGreen = value; break;
                case "color.blue": target.tintBlue = value; break;
                case "color.saturation": target.saturation = value; break;
                case "color.brightness": target.brightness = value; break;
                case "color.hue": target.hueShift = value; break;
                case "color.contrast": target.contrast = value; break;
                case "color.opacity": target.opacity = value; break;
                case "shape.scale": target.scale = value; break;
                case "shape.width": target.width = value; break;
                case "shape.height": target.height = value; break;
                case "shape.density": target.density = value; break;
                case "shape.spread": target.spread = value; break;
                case "shape.rotation": target.rotation = value; break;
                case "shape.offsetX": target.offsetX = value; break;
                case "shape.offsetZ": target.offsetZ = value; break;
                case "effects.apply": target.applyToProduce = value > 0.5f; break;
                case "effects.radiance": target.radiance = value; break;
                case "effects.gloom": target.gloom = value; break;
                case "effects.overlay": target.overlayIntensity = value; break;
                case "effects.radianceScale": target.radianceScale = value; break;
                case "effects.gloomScale": target.gloomScale = value; break;
                case "effects.spikes": target.spikes = value > 0.5f; break;
                default: return;
            }
            MarkCustomized();
            ClearPreviewMaskTexture();
        }

        void IHorticultureVisualDesignerSurface.SetEditingProduce(bool value)
        {
            if (editingProduce == value) return;
            if (editingProduce) CommitProduceEditor();
            editingProduce = value;
            produceEditor = value ? SharedVisual.CreateProduceVisualEditor() : null;
            selectedMask = value ? 3 : 0;
            ClearPreviewMaskTexture();
        }

        void IHorticultureVisualDesignerSurface.ResetSection(string section)
        {
            VisualSettingsRecord target = EditingVisual;
            if (string.Equals(section, "Color", StringComparison.OrdinalIgnoreCase)) ResetColorSection(target);
            else if (string.Equals(section, "Shape", StringComparison.OrdinalIgnoreCase)) ResetShapeSection(target);
            else ResetEffectsSection(target);
            target.Normalize();
            if (ReferenceEquals(target, ProduceEditor)) CommitProduceEditor();
            MarkCustomized();
            ClearPreviewMaskTexture();
        }

        void IHorticultureVisualDesignerSurface.ResetCurrentMask()
        {
            if (!((IHorticultureVisualDesignerSurface)this).CanEdit || !CurrentUsesPerMaskVisuals) return;
            VisualSettingsRecord target = CurrentVisual;
            VisualSettingsRecord inherited = selectedMask >= 0 && selectedMask < inheritedVisuals.Count
                ? inheritedVisuals[selectedMask] : null;
            target.CopyFrom(inherited ?? new VisualSettingsRecord(trait));
            target.Normalize();
            if (ReferenceEquals(target, ProduceEditor)) CommitProduceEditor();
            MarkCustomized();
            ClearPreviewMaskTexture();
        }

        void IHorticultureVisualDesignerSurface.RestoreInherited()
        {
            if (subtypeRecord != null) subtypeRecord.ResetVisuals(trait);
            else if (plantRecord != null) plantRecord.useCustomVisual = false;
            else globalRecord.ResetVisuals(trait);
            HorticultureNovelSeedsMod.Settings?.ClearVisualCache();
            ProduceMaskRenderer.ClearAll();
            ClearPreviewMaskTexture();
        }

        void IHorticultureVisualDesignerSurface.RestoreXmlDefault()
        {
            if (plantRecord != null) plantRecord.useCustomVisual = false;
            else if (subtypeRecord != null) subtypeRecord.ResetVisuals(trait);
            else globalRecord.ResetVisuals(trait);
            HorticultureNovelSeedsMod.Settings?.ClearVisualCache();
            ProduceMaskRenderer.ClearAll();
            ClearPreviewMaskTexture();
        }

        void IHorticultureVisualDesignerSurface.DrawPreview(Rect rect)
        {
            VisualSettingsRecord previewVisual = WholeVisual;
            if (editingProduce || (CurrentUsesPerMaskVisuals && selectedMask >= 3))
                DrawProducePreview(rect, previewVisual);
            else DrawPreview(rect, previewVisual);
        }

        void IHorticultureVisualDesignerSurface.Close() => Close();

        /*
         * The old renderer below is intentionally retained only as the specialized preview
         * implementation.  DoWindowContents is owned by HorticultureVisualDesignerDocument;
         * no settings controls, scroll view, or navigation is painted here anymore.
         */
        private void LegacyChromeRemovedMarker()
        {
            // Kept as an explicit audit marker so future edits do not accidentally restore the
            // superseded manual editor chrome around the authoritative preview renderer.
        }

        /*
         * The following code is retained for the specialized preview/resource implementation.
         */
        private bool CurrentUsesPerMaskVisuals => subtypeRecord != null
            ? subtypeRecord.usePerMaskVisuals
            : plantRecord == null
                ? globalRecord.usePerMaskVisuals
                : plantRecord.useCustomVisual
                    ? plantRecord.usePerMaskVisuals
                    : inheritedVisuals.Count == 6 && inheritedVisuals[0].instanceName.StartsWith("Plant:");
        private bool PreviewUsesMask => CurrentUsesPerMaskVisuals;

        private bool UsingProduceMaskVisual => editingProduce && CurrentUsesPerMaskVisuals;

        private VisualSettingsRecord SharedVisual => subtypeRecord != null ? subtypeRecord.SharedVisual(trait)
            : plantRecord == null ? globalRecord.visual
            : plantRecord.useCustomVisual ? plantRecord.visual : inheritedVisuals[0];

        private VisualSettingsRecord ProduceEditor => produceEditor ?? (produceEditor = SharedVisual.CreateProduceVisualEditor());

        private void CommitProduceEditor()
        {
            if (produceEditor == null) return;
            SharedVisual.ApplyProduceVisualEditor(produceEditor);
            if (plantRecord?.useCustomVisual == true && plantRecord.usePerMaskVisuals) plantRecord.SyncProduceSettingsToMasks();
        }

        private VisualSettingsRecord ColorVisual => editingProduce && !UsingProduceMaskVisual ? ProduceEditor : CurrentVisual;
        private VisualSettingsRecord WholeVisual => editingProduce ? ProduceEditor : SharedVisual;
        private VisualSettingsRecord EditingVisual => string.Equals(activeSection, "Color", StringComparison.OrdinalIgnoreCase)
            ? ColorVisual : WholeVisual;

        private VisualSettingsRecord CurrentVisual
        {
            get
            {
                if (!CurrentUsesPerMaskVisuals)
                    return subtypeRecord != null ? subtypeRecord.SharedVisual(trait)
                        : plantRecord == null ? globalRecord.visual : plantRecord.useCustomVisual ? plantRecord.visual : inheritedVisuals[0];
                selectedMask = Mathf.Clamp(selectedMask, 0, 5);
                return subtypeRecord != null ? subtypeRecord.VisualForMask(trait, selectedMask)
                    : plantRecord == null ? globalRecord.VisualForMask(selectedMask)
                    : plantRecord.useCustomVisual ? plantRecord.VisualForMask(selectedMask) : inheritedVisuals[selectedMask];
            }
        }

        private void SetPerMaskVisuals(bool enabled)
        {
            if (subtypeRecord != null) subtypeRecord.SetPerMaskVisuals(trait, enabled);
            else if (plantRecord == null)
            {
                globalRecord.SetPerMaskVisuals(enabled);
                globalRecord.visualCustomized = true;
            }
            else plantRecord.SetPerMaskVisuals(enabled, inheritedVisuals);
            HorticultureNovelSeedsMod.Settings?.ClearVisualCache();
            ProduceMaskRenderer.ClearAll();
            ClearPreviewMaskTexture();
        }

        private static void ResetColorSection(VisualSettingsRecord target)
        {
            target.tintRed = 1f;
            target.tintGreen = 1f;
            target.tintBlue = 1f;
            target.hueShift = 0f;
            target.saturation = 1f;
            target.brightness = 1f;
            target.contrast = 1f;
            target.opacity = 1f;
            target.dullness = 0f;
        }

        private static void ResetShapeSection(VisualSettingsRecord target)
        {
            target.scale = 1f;
            target.width = 1f;
            target.height = 1f;
            target.density = 1f;
            target.spread = 1f;
            target.rotation = 0f;
            target.rotationVariation = 0f;
            target.scaleVariation = 0f;
            target.offsetX = 0f;
            target.offsetZ = 0f;
            target.shadowScale = 1f;
        }

        private static void ResetEffectsSection(VisualSettingsRecord target)
        {
            target.radiance = 0f;
            target.radianceScale = 1f;
            target.radianceRed = 1f;
            target.radianceGreen = 1f;
            target.radianceBlue = 1f;
            target.gloom = 0f;
            target.gloomScale = 1f;
            target.gloomRed = 1f;
            target.gloomGreen = 1f;
            target.gloomBlue = 1f;
            target.overlayPattern = 0;
            target.overlayIntensity = 1f;
            target.overlayScale = 1f;
            target.overlayRed = 1f;
            target.overlayGreen = 1f;
            target.overlayBlue = 1f;
            target.spikes = false;
        }
        private void MarkCustomized()
        {
            VisualSettingsRecord target = EditingVisual;
            target.Normalize();
            if (ReferenceEquals(target, ProduceEditor)) CommitProduceEditor();
            MarkVisualCustomized();
            HorticultureNovelSeedsMod.Settings?.ClearVisualCache();
            ProduceMaskRenderer.ClearAll();
        }

        private void MarkVisualCustomized()
        {
            if (subtypeRecord != null) subtypeRecord.visualCustomized = true;
            else if (plantRecord == null) globalRecord.visualCustomized = true;
        }

        private void DrawPreview(Rect rect, VisualSettingsRecord v)
        {
            Widgets.DrawMenuSection(rect);
            Rect inner = rect.ContractedBy(8f);
            Widgets.DrawBoxSolid(inner, new Color(0.16f, 0.18f, 0.17f));
            string maskSuffix = PreviewUsesMask ? " - " + MaskLabels[selectedMask] : string.Empty;
            Widgets.Label(new Rect(inner.x + 8f, inner.y + 6f, inner.width - 16f, 24f), (previewPlant?.LabelCap ?? "Plant") + " Preview" + maskSuffix);
            Rect stage = new Rect(inner.x + 18f, inner.y + 40f, inner.width - 36f, inner.height - 95f);
            EnsurePreviewTextures();
            if (v.radiance > 0f) DrawPreviewEffect(stage, radialTexture, new Color(v.radianceRed, v.radianceGreen, v.radianceBlue, v.radiance), v.radianceScale);
            if (v.gloom > 0f) DrawPreviewEffect(stage, radialTexture, new Color(v.gloomRed, v.gloomGreen, v.gloomBlue, v.gloom), v.gloomScale);

            bool maskShown;
            if (!TryDrawPerMaskPlantPreview(stage, out maskShown))
                maskShown = DrawPlantPreviewMeshes(stage, v, null, false);            if (v.overlayPattern > 0)
            {
                Color old = GUI.color;
                GUI.color = new Color(v.overlayRed, v.overlayGreen, v.overlayBlue, v.overlayIntensity);
                Rect overlay = ScaledAroundCenter(stage, v.overlayScale);
                GUI.DrawTexture(overlay, overlayTextures[v.overlayPattern], ScaleMode.StretchToFill, true);
                GUI.color = old;
            }
            string note = PreviewUsesMask
                ? string.Equals(activeSection, "Color", StringComparison.OrdinalIgnoreCase)
                    ? maskShown ? "Color preview is limited to " + MaskLabels[selectedMask] + "." : "The selected mask has no painted area; its color is not shown."
                    : "Shape and Effects apply to the whole plant. Masks affect Color only."
                : "Preview approximates map rendering. Texture styling is baked and cached in play.";
            Widgets.Label(new Rect(inner.x + 8f, inner.yMax - 42f, inner.width - 16f, 38f), note);
        }

        private Texture PlantGraphicTexture(int index)
        {
            Graphic graphic = previewPlant?.graphicData?.Graphic;
            if (graphic is Graphic_Random random && random.SubGraphicsCount > 0)
                graphic = random.SubGraphicAtIndex(index % random.SubGraphicsCount);
            return graphic?.MatSingle?.mainTexture ?? previewPlant?.uiIcon;
        }

        private int PlantGraphicVariationIndex(int index)
        {
            Graphic graphic = previewPlant?.graphicData?.Graphic;
            return graphic is Graphic_Random random && random.SubGraphicsCount > 0 ? index % random.SubGraphicsCount : 0;
        }

        private bool TryDrawPerMaskPlantPreview(Rect stage, out bool maskShown)
        {
            maskShown = false;
            if (!CurrentUsesPerMaskVisuals || !PlantMaskUtility.HasActiveMasks(previewPlant)) return false;

            int focusedLayer = Mathf.Clamp(selectedMask, 0, 2);
            VisualSettingsRecord[] visuals = { VisualForMaskIndex(0), VisualForMaskIndex(1), VisualForMaskIndex(2) };
            VisualSettingsRecord[] neutralVisuals = { new VisualSettingsRecord(), new VisualSettingsRecord(), new VisualSettingsRecord() };
            int compositeHash = unchecked(CompositePreviewHash(visuals, PlantMaskUtility.MaskHash(previewPlant)) * 31 + focusedLayer);
            if (compositeHash != styledPreviewHash)
            {
                ClearStyledPreviewTextures();
                styledPreviewHash = compositeHash;
            }

            // Preserve map geometry for every mask, but style only the mask currently being edited.
            DrawPlantPreviewMeshes(stage, WholeVisual, delegate(int index)
            {
                Texture source = PlantGraphicTexture(index);
                List<VisualMaskLayerRecord> masks = PlantMaskUtility.LayersForVariation(previewPlant, PlantGraphicVariationIndex(index), false);
                return GetCompositePreviewTexture(source, neutralVisuals, masks, -1);
            }, true);

            for (int layer = 0; layer < 3; layer++)
            {
                if (!PlantMaskUtility.AnyResolvedLayerHasPixels(previewPlant, layer)) continue;
                int capturedLayer = layer;
                IReadOnlyList<VisualSettingsRecord> layerStyles = layer == focusedLayer ? visuals : neutralVisuals;
                DrawPlantPreviewMeshes(stage, WholeVisual, delegate(int index)
                {
                    Texture source = PlantGraphicTexture(index);
                    List<VisualMaskLayerRecord> masks = PlantMaskUtility.LayersForVariation(previewPlant, PlantGraphicVariationIndex(index), false);
                    return GetCompositePreviewTexture(source, layerStyles, masks, capturedLayer);
                }, true);
            }

            maskShown = PlantMaskUtility.AnyResolvedLayerHasPixels(previewPlant, focusedLayer);
            return true;
        }
        private bool DrawPlantPreviewMeshes(Rect stage, VisualSettingsRecord visual, Func<int, Texture> textureProvider, bool textureAlreadyStyled)
        {
            if (previewPlant?.plant == null || visual == null) return false;
            const int previewSeed = 7919;
            int maxMeshCount = Mathf.Max(1, previewPlant.plant.maxMeshCount);
            int count = Mathf.Max(1, Mathf.CeilToInt(maxMeshCount * visual.density));
            float growthSize = previewPlant.plant.visualSizeRange.LerpThroughRange(1f);
            Vector2 baseSize = previewPlant.graphicData.drawSize * (growthSize * visual.scale);
            Vector2[] centers = new Vector2[count];
            Vector2[] sizes = new Vector2[count];
            float[] angles = new float[count];
            float minX = 0f, maxX = 0f, minY = 0f, maxY = 0f;

            for (int i = 0; i < count; i++)
            {
                float variation = PlantVisualUtility.MeshScaleFactor(previewSeed, i, visual.scaleVariation);
                sizes[i] = new Vector2(baseSize.x * visual.width * variation, baseSize.y * visual.height * variation);
                angles[i] = PlantVisualUtility.MeshRotation(previewSeed, i, visual.rotation, visual.rotationVariation);
                float radians = angles[i] * Mathf.Deg2Rad;
                float rotatedHeight = Mathf.Abs(Mathf.Sin(radians)) * sizes[i].x + Mathf.Abs(Mathf.Cos(radians)) * sizes[i].y;
                Vector2 offset = PlantVisualUtility.MeshOffset(previewSeed, i, i % maxMeshCount, maxMeshCount, maxMeshCount, visual.spread);
                if (maxMeshCount == 1 && offset.y - rotatedHeight / 2f < -0.5f)
                    offset.y = -0.5f + rotatedHeight / 2f;
                centers[i] = new Vector2(offset.x + visual.offsetX, -(offset.y + visual.offsetZ));


                float cosine = Mathf.Abs(Mathf.Cos(radians));
                float sine = Mathf.Abs(Mathf.Sin(radians));
                float halfWidth = (cosine * sizes[i].x + sine * sizes[i].y) * 0.5f;
                float halfHeight = (sine * sizes[i].x + cosine * sizes[i].y) * 0.5f;
                minX = Mathf.Min(minX, centers[i].x - halfWidth);
                maxX = Mathf.Max(maxX, centers[i].x + halfWidth);
                minY = Mathf.Min(minY, centers[i].y - halfHeight);
                maxY = Mathf.Max(maxY, centers[i].y + halfHeight);
            }

            float nominalPixelsPerCell = Mathf.Min(stage.width, stage.height) * 0.48f;
            float horizontalExtent = Mathf.Max(0.01f, Mathf.Max(Mathf.Abs(minX), Mathf.Abs(maxX)));
            float verticalExtent = Mathf.Max(0.01f, Mathf.Max(Mathf.Abs(minY), Mathf.Abs(maxY)));
            float pixelsPerCell = Mathf.Min(nominalPixelsPerCell,
                Mathf.Min((stage.width * 0.5f - 8f) / horizontalExtent, (stage.height * 0.5f - 8f) / verticalExtent));

            Matrix4x4 oldMatrix = GUI.matrix;
            bool maskShown = false;
            for (int i = count - 1; i >= 0; i--)
            {
                Texture texture = textureProvider != null ? textureProvider(i) : PlantGraphicTexture(i);
                if (texture == null) continue;
                Vector2 pixelSize = sizes[i] * pixelsPerCell;
                Vector2 pixelCenter = stage.center + centers[i] * pixelsPerCell;
                Rect draw = new Rect(pixelCenter.x - pixelSize.x / 2f, pixelCenter.y - pixelSize.y / 2f, pixelSize.x, pixelSize.y);
                GUIUtility.RotateAroundPivot(angles[i], draw.center);
                if (textureAlreadyStyled)
                {
                    Color old = GUI.color;
                    GUI.color = Color.white;
                    GUI.DrawTexture(draw, texture, ScaleMode.StretchToFill, true);
                    GUI.color = old;
                }
                else maskShown |= DrawStyledPreviewTexture(draw, texture, visual);
                GUI.matrix = oldMatrix;
            }
            return maskShown;
        }
        private VisualSettingsRecord VisualForMaskIndex(int index)
        {
            if (subtypeRecord != null) return subtypeRecord.VisualForMask(trait, index);
            if (plantRecord == null) return globalRecord.VisualForMask(index);
            if (plantRecord?.useCustomVisual == true) return plantRecord.VisualForMask(index);
            if (inheritedVisuals.Count == 6) return inheritedVisuals[index];
            return CurrentVisual;
        }

        private static bool SamePreviewGeometry(VisualSettingsRecord a, VisualSettingsRecord b)
        {
            return Mathf.Approximately(a.scale, b.scale) && Mathf.Approximately(a.width, b.width) && Mathf.Approximately(a.height, b.height)
                && Mathf.Approximately(a.density, b.density) && Mathf.Approximately(a.spread, b.spread)
                && Mathf.Approximately(a.rotation, b.rotation) && Mathf.Approximately(a.rotationVariation, b.rotationVariation)
                && Mathf.Approximately(a.scaleVariation, b.scaleVariation) && Mathf.Approximately(a.offsetX, b.offsetX)
                && Mathf.Approximately(a.offsetZ, b.offsetZ);
        }
        private bool TryDrawPerMaskProducePreview(Rect stage, Texture texture, out bool maskShown)
        {
            maskShown = false;
            PlantSettingsRecord settings = HorticultureNovelSeedsMod.Settings?.GetPlantSettings(previewPlant, false);
            if (!CurrentUsesPerMaskVisuals || settings?.HasActiveProduceMasks != true || texture == null) return false;

            int focusedLayer = Mathf.Clamp(selectedMask - 3, 0, 2);
            List<VisualMaskLayerRecord> masks = settings.ProduceMaskLayers;
            VisualSettingsRecord[] visuals = { VisualForMaskIndex(3), VisualForMaskIndex(4), VisualForMaskIndex(5) };
            VisualSettingsRecord[] neutralVisuals = { new VisualSettingsRecord(), new VisualSettingsRecord(), new VisualSettingsRecord() };
            IReadOnlyList<VisualSettingsRecord> focusedStyles = SharedVisual.applyToProduce ? visuals : neutralVisuals;
            int compositeHash = unchecked(CompositePreviewHash(visuals, MaskListHash(masks)) * 31 + focusedLayer + (SharedVisual.applyToProduce ? 101 : 0));
            if (compositeHash != styledPreviewHash)
            {
                ClearStyledPreviewTextures();
                styledPreviewHash = compositeHash;
            }

            DrawProducePreviewMeshes(stage, WholeVisual, GetCompositePreviewTexture(texture, neutralVisuals, masks, -1));
            for (int layer = 0; layer < 3; layer++)
            {
                if (!masks[layer].HasPixels) continue;
                IReadOnlyList<VisualSettingsRecord> layerStyles = layer == focusedLayer ? focusedStyles : neutralVisuals;
                DrawProducePreviewMeshes(stage, WholeVisual, GetCompositePreviewTexture(texture, layerStyles, masks, layer));
            }
            maskShown = masks[focusedLayer].HasPixels;
            return true;
        }

        private static int ProducePreviewCount(float density)
        {
            return Mathf.Clamp(1 + Mathf.RoundToInt((density - 1f) * 4f), 1, PreviewOffsets.Length);
        }
        private static void DrawProducePreviewMeshes(Rect stage, VisualSettingsRecord visual, Texture texture)
        {
            if (visual == null || texture == null) return;
            int count = ProducePreviewCount(visual.density);
            float baseSize = Mathf.Min(stage.width, stage.height) * 0.62f * visual.scale;
            for (int i = 0; i < count; i++)
            {
                float variation = PlantVisualUtility.MeshScaleFactor(7919, i, visual.scaleVariation);
                Vector2 offset = PreviewOffsets[i] * visual.spread;
                Vector2 size = new Vector2(baseSize * visual.width * variation, baseSize * visual.height * variation);
                Rect draw = new Rect(stage.center.x - size.x / 2f + offset.x * stage.width + visual.offsetX * stage.width * 0.45f,
                    stage.center.y - size.y / 2f + offset.y * stage.height - visual.offsetZ * stage.height * 0.45f, size.x, size.y);
                Matrix4x4 oldMatrix = GUI.matrix;
                float angle = PlantVisualUtility.MeshRotation(7919, i, visual.rotation, visual.rotationVariation);
                GUIUtility.RotateAroundPivot(angle, draw.center);
                Color old = GUI.color;
                GUI.color = Color.white;
                GUI.DrawTexture(draw, texture, ScaleMode.StretchToFill, true);
                GUI.color = old;
                GUI.matrix = oldMatrix;
            }
        }

        private void DrawProducePreview(Rect rect, VisualSettingsRecord v)
        {
            Widgets.DrawMenuSection(rect);
            Rect inner = rect.ContractedBy(8f);
            Widgets.DrawBoxSolid(inner, new Color(0.16f, 0.18f, 0.17f));
            ThingDef product = previewPlant?.plant?.harvestedThingDef;
            string maskSuffix = PreviewUsesMask ? " - " + MaskLabels[selectedMask] : string.Empty;
            Widgets.Label(new Rect(inner.x + 8f, inner.y + 6f, inner.width - 16f, 24f), (product?.LabelCap ?? "Produce") + " Preview" + maskSuffix);
            Rect stage = new Rect(inner.x + 30f, inner.y + 52f, inner.width - 60f, inner.height - 125f);
            EnsurePreviewTextures();
            if (v.radiance > 0f) DrawPreviewEffect(stage, radialTexture, new Color(v.radianceRed, v.radianceGreen, v.radianceBlue, v.radiance), v.radianceScale);
            if (v.gloom > 0f) DrawPreviewEffect(stage, radialTexture, new Color(v.gloomRed, v.gloomGreen, v.gloomBlue, v.gloom), v.gloomScale);

            Texture texture = product?.uiIcon ?? previewPlant?.uiIcon;
            bool maskShown = false;
            if (texture != null && !TryDrawPerMaskProducePreview(stage, texture, out maskShown))
            {
                int count = ProducePreviewCount(v.density);
                float baseSize = Mathf.Min(stage.width, stage.height) * 0.62f * v.scale;
                for (int i = 0; i < count; i++)
                {
                    float variation = PlantVisualUtility.MeshScaleFactor(7919, i, v.scaleVariation);
                    Vector2 offset = PreviewOffsets[i] * v.spread;
                    Vector2 size = new Vector2(baseSize * v.width * variation, baseSize * v.height * variation);
                    Rect draw = new Rect(stage.center.x - size.x / 2f + offset.x * stage.width + v.offsetX * stage.width * 0.45f,
                        stage.center.y - size.y / 2f + offset.y * stage.height - v.offsetZ * stage.height * 0.45f, size.x, size.y);
                    Matrix4x4 oldMatrix = GUI.matrix;
                    float angle = PlantVisualUtility.MeshRotation(7919, i, v.rotation, v.rotationVariation);
                    GUIUtility.RotateAroundPivot(angle, draw.center);
                    maskShown |= DrawStyledPreviewTexture(draw, texture, v.applyToProduce ? v : new VisualSettingsRecord());
                    GUI.matrix = oldMatrix;
                }
            }
            if (v.overlayPattern > 0)
            {
                Color old = GUI.color;
                GUI.color = new Color(v.overlayRed, v.overlayGreen, v.overlayBlue, v.overlayIntensity);
                GUI.DrawTexture(ScaledAroundCenter(stage, v.overlayScale), overlayTextures[v.overlayPattern], ScaleMode.StretchToFill, true);
                GUI.color = old;
            }
            string note = !SharedVisual.applyToProduce
                ? "Produce inheritance is disabled for this trait."
                : PreviewUsesMask
                ? string.Equals(activeSection, "Color", StringComparison.OrdinalIgnoreCase)
                        ? maskShown ? "Color preview is limited to " + MaskLabels[selectedMask] + "." : "The selected mask has no painted area; its color is not shown."
                        : "Shape and Effects apply to the whole produce graphic. Masks affect Color only."
                    : "Produce has an independent Color, Shape, and Effects profile.";
            Widgets.Label(new Rect(inner.x + 8f, inner.yMax - 48f, inner.width - 16f, 42f), note);
        }

        private static int MaskListHash(IReadOnlyList<VisualMaskLayerRecord> masks)
        {
            unchecked
            {
                int hash = 486187739;
                for (int i = 0; i < masks.Count; i++) hash = hash * 31 + masks[i].ContentHash;
                return hash;
            }
        }
        private static int CompositePreviewHash(IReadOnlyList<VisualSettingsRecord> visuals, int maskHash)
        {
            unchecked
            {
                int hash = 486187739;
                for (int i = 0; i < 3; i++) hash = hash * 31 + PreviewStyleHash(visuals[i], null);
                return hash * 31 + maskHash;
            }
        }

        private Texture2D GetCompositePreviewTexture(Texture source, IReadOnlyList<VisualSettingsRecord> visuals, IReadOnlyList<VisualMaskLayerRecord> masks, int targetLayer)
        {
            if (source == null) return null;
            int cacheKey = unchecked(source.GetInstanceID() * 397 ^ (targetLayer + 3));
            if (styledPreviewTextures.TryGetValue(cacheKey, out Texture2D cached)) return cached;
            RenderTexture previous = RenderTexture.active;
            PlantMaskUtility.BakedTextureSize(source.width, source.height, out int width, out int height);
            RenderTexture temporary = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);
            try
            {
                Graphics.Blit(source, temporary);
                RenderTexture.active = temporary;
                Texture2D result = new Texture2D(width, height, TextureFormat.RGBA32, true)
                {
                    name = source.name + "_HNS_CompositePreview", filterMode = source.filterMode, wrapMode = source.wrapMode
                };
                result.ReadPixels(new Rect(0f, 0f, width, height), 0, 0, false);
                Color[] pixels = result.GetPixels();
                for (int i = 0; i < pixels.Length; i++)
                {
                    Color c = pixels[i];
                    if (c.a <= 0f) continue;
                    int layer = PlantMaskUtility.LayerAt(masks, i % width, i / width, width, height);
                    if (targetLayer == -2)
                    {
                        if (layer >= 0) pixels[i] = ApplyPreviewStyle(c, visuals[layer]);
                    }
                    else
                    {
                        if (layer != targetLayer) c.a = 0f;
                        else pixels[i] = ApplyPreviewStyle(c, targetLayer >= 0 ? visuals[targetLayer] : visuals[0]);
                        if (layer != targetLayer) pixels[i] = c;
                    }
                }
                result.SetPixels(pixels);
                result.Apply(true, true);
                styledPreviewTextures[cacheKey] = result;
                return result;
            }
            catch (Exception exception)
            {
                Log.Warning("[Horticulture - Novel Seeds] Could not create combined visual designer preview: " + exception.Message);
                return null;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(temporary);
            }
        }

        private static Color ApplyPreviewStyle(Color source, VisualSettingsRecord visual)
        {
            return PlantVisualColorUtility.Apply(source, visual.tintRed, visual.tintGreen, visual.tintBlue,
                visual.hueShift, visual.saturation, visual.brightness, visual.contrast, visual.opacity, visual.dullness);
        }
        private bool DrawStyledPreviewTexture(Rect draw, Texture source, VisualSettingsRecord visual)
        {
            VisualMaskLayerRecord maskLayer = PreviewUsesMask ? SelectedPreviewMaskLayer() : null;
            bool hasMask = maskLayer?.HasPixels == true;
            Texture2D styledTexture = GetStyledPreviewTexture(source, visual, hasMask ? maskLayer : null);
            Color old = GUI.color;
            GUI.color = Color.white;
            if (hasMask) GUI.DrawTexture(draw, source, ScaleMode.StretchToFill, true);
            GUI.DrawTexture(draw, styledTexture ?? source, ScaleMode.StretchToFill, true);
            GUI.color = old;
            return hasMask;
        }

        private Texture2D GetStyledPreviewTexture(Texture source, VisualSettingsRecord visual, VisualMaskLayerRecord maskLayer)
        {
            if (source == null || visual == null) return null;
            int hash = PreviewStyleHash(visual, maskLayer);
            if (hash != styledPreviewHash)
            {
                ClearStyledPreviewTextures();
                styledPreviewHash = hash;
            }
            int sourceId = source.GetInstanceID();
            if (styledPreviewTextures.TryGetValue(sourceId, out Texture2D cached)) return cached;

            RenderTexture previous = RenderTexture.active;
            PlantMaskUtility.BakedTextureSize(source.width, source.height, out int width, out int height);
            RenderTexture temporary = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);
            try
            {
                Graphics.Blit(source, temporary);
                RenderTexture.active = temporary;
                Texture2D result = new Texture2D(width, height, TextureFormat.RGBA32, true)
                {
                    name = source.name + "_HNS_Preview", filterMode = source.filterMode, wrapMode = source.wrapMode
                };
                result.ReadPixels(new Rect(0f, 0f, width, height), 0, 0, false);
                Color[] pixels = result.GetPixels();
                for (int i = 0; i < pixels.Length; i++)
                {
                    Color c = pixels[i];
                    if (c.a <= 0f) continue;
                    if (maskLayer != null)
                    {
                        int x = i % width;
                        int y = i / width;
                        int maskX = Mathf.Clamp(x * VisualMaskLayerRecord.Resolution / width, 0, VisualMaskLayerRecord.Resolution - 1);
                        int maskY = VisualMaskLayerRecord.Resolution - 1 - Mathf.Clamp(y * VisualMaskLayerRecord.Resolution / height, 0, VisualMaskLayerRecord.Resolution - 1);
                        if (!maskLayer.IsPainted(maskX, maskY))
                        {
                            c.a = 0f;
                            pixels[i] = c;
                            continue;
                        }
                    }
                    pixels[i] = PlantVisualColorUtility.Apply(c, visual.tintRed, visual.tintGreen, visual.tintBlue,
                        visual.hueShift, visual.saturation, visual.brightness, visual.contrast, visual.opacity, visual.dullness);
                }
                result.SetPixels(pixels);
                result.Apply(true, true);
                styledPreviewTextures[sourceId] = result;
                return result;
            }
            catch (Exception exception)
            {
                Log.Warning("[Horticulture - Novel Seeds] Could not create styled visual designer preview: " + exception.Message);
                return null;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(temporary);
            }
        }

        private static int PreviewStyleHash(VisualSettingsRecord visual, VisualMaskLayerRecord maskLayer)
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + Mathf.RoundToInt(visual.tintRed * 1000f);
                hash = hash * 31 + Mathf.RoundToInt(visual.tintGreen * 1000f);
                hash = hash * 31 + Mathf.RoundToInt(visual.tintBlue * 1000f);
                hash = hash * 31 + Mathf.RoundToInt(visual.hueShift * 1000f);
                hash = hash * 31 + Mathf.RoundToInt(visual.saturation * 1000f);
                hash = hash * 31 + Mathf.RoundToInt(visual.brightness * 1000f);
                hash = hash * 31 + Mathf.RoundToInt(visual.contrast * 1000f);
                hash = hash * 31 + Mathf.RoundToInt(visual.opacity * 1000f);
                hash = hash * 31 + Mathf.RoundToInt(visual.dullness * 1000f);
                return hash * 31 + (maskLayer?.ContentHash ?? 0);
            }
        }

        private void ClearStyledPreviewTextures()
        {
            foreach (Texture2D texture in styledPreviewTextures.Values)
                if (texture != null) UnityEngine.Object.Destroy(texture);
            styledPreviewTextures.Clear();
        }
        private Texture2D GetPreviewMaskTexture(Texture source)
        {
            VisualMaskLayerRecord layer = SelectedPreviewMaskLayer();
            if (layer?.HasPixels != true || source == null) return null;
            int sourceId = source.GetInstanceID();
            int hash = layer.ContentHash;
            if (previewMaskTexture != null && previewMaskLayer == layer && previewMaskHash == hash && previewMaskSourceId == sourceId) return previewMaskTexture;
            ClearPreviewMaskTexture();
            PlantMaskUtility.BakedTextureSize(source.width, source.height, out int width, out int height);
            RenderTexture temporary = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
            RenderTexture previous = RenderTexture.active;
            try
            {
                Graphics.Blit(source, temporary);
                RenderTexture.active = temporary;
                Texture2D result = new Texture2D(width, height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                result.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                Color32[] pixels = result.GetPixels32();
                for (int y = 0; y < height; y++)
                {
                    int maskY = VisualMaskLayerRecord.Resolution - 1 - Mathf.Clamp(y * VisualMaskLayerRecord.Resolution / height, 0, VisualMaskLayerRecord.Resolution - 1);
                    for (int x = 0; x < width; x++)
                    {
                        int index = y * width + x;
                        int maskX = Mathf.Clamp(x * VisualMaskLayerRecord.Resolution / width, 0, VisualMaskLayerRecord.Resolution - 1);
                        if (!layer.IsPainted(maskX, maskY)) pixels[index].a = 0;
                    }
                }
                result.SetPixels32(pixels);
                result.Apply(false, true);
                previewMaskLayer = layer;
                previewMaskHash = hash;
                previewMaskSourceId = sourceId;
                previewMaskTexture = result;
                return result;
            }
            catch (Exception exception)
            {
                Log.Warning("[Horticulture - Novel Seeds] Could not create visual designer mask preview: " + exception.Message);
                return null;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(temporary);
            }
        }

        private VisualMaskLayerRecord SelectedPreviewMaskLayer()
        {
            if (!PreviewUsesMask || previewPlant == null) return null;
            PlantSettingsRecord settings = HorticultureNovelSeedsMod.Settings?.GetPlantSettings(previewPlant, false);
            if (settings == null) return null;
            return selectedMask < 3 ? settings.PlantMaskLayers[selectedMask] : settings.ProduceMaskLayers[selectedMask - 3];
        }

        private void ClearPreviewMaskTexture()
        {
            if (previewMaskTexture != null) UnityEngine.Object.Destroy(previewMaskTexture);
            previewMaskTexture = null;
            previewMaskLayer = null;
            previewMaskHash = 0;
            previewMaskSourceId = 0;
            ClearStyledPreviewTextures();
        }
        private static void DrawPreviewEffect(Rect stage, Texture texture, Color color, float scale)
        {
            Color old = GUI.color; GUI.color = color;
            GUI.DrawTexture(ScaledAroundCenter(stage, scale), texture, ScaleMode.StretchToFill, true);
            GUI.color = old;
        }

        private static Rect ScaledAroundCenter(Rect rect, float scale)
        {
            Vector2 size = rect.size * scale;
            return new Rect(rect.center.x - size.x / 2f, rect.center.y - size.y / 2f, size.x, size.y);
        }

        private static void EnsurePreviewTextures()
        {
            if (radialTexture == null) radialTexture = CreatePreviewTexture(0);
            for (int i = 1; i < overlayTextures.Length; i++) if (overlayTextures[i] == null) overlayTextures[i] = CreatePreviewTexture(i);
        }

        private static Texture2D CreatePreviewTexture(int pattern)
        {
            const int size = 64;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, true) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            Color32[] pixels = new Color32[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float nx = ((x + 0.5f) / size) * 2f - 1f, ny = ((y + 0.5f) / size) * 2f - 1f;
                float distance = Mathf.Sqrt(nx * nx + ny * ny), angle = Mathf.Atan2(ny, nx), alpha;
                if (pattern == 0) alpha = Mathf.Pow(Mathf.Clamp01(1f - distance), 1.3f) * 0.7f;
                else if (distance > 0.94f) alpha = 0f;
                else if (pattern == 1) { float ray = Mathf.Pow(Mathf.Abs(Mathf.Cos(angle * 8f)), 22f); alpha = distance > 0.32f && distance < 0.42f + ray * 0.5f ? 0.8f : 0f; }
                else if (pattern == 2) alpha = Mathf.Sin(x * 0.73f + y * 1.37f) * Mathf.Sin(x * 1.61f - y * 0.51f) > 0.72f ? 0.75f : 0f;
                else if (pattern == 3) alpha = Mathf.Abs(Mathf.Sin((nx + ny * 0.35f) * 22f)) > 0.82f ? 0.55f * Mathf.Clamp01(1f - distance) : 0f;
                else if (pattern == 4) alpha = Mathf.Abs(Mathf.Sin(angle * 7f + distance * 10f)) > 0.92f ? 0.68f * Mathf.Clamp01(1.1f - distance) : 0f;
                else alpha = (unchecked(x * 73856093 ^ y * 19349663) & 31) < 4 ? 0.72f : 0f;
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
            }
            texture.SetPixels32(pixels); texture.Apply(true, true); return texture;
        }
    }
}
