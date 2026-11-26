using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace HorticultureNovelSeeds
{
    // Appearance-only defaults for native 1.7. No mutation weights, mechanical
    // stats, trait identities, save fields or inheritance rules are modified.
    // Tier is art direction, not a second probability system.
    internal static class TraitAppearanceCatalog
    {
        internal readonly record struct Profile(string Tier, string Motif, float Scale, float Width, float Height,
            float Density, int Pattern, float Strength, float Brightness, float Saturation, float Radiance);
        private static readonly Dictionary<string, Profile> Profiles = new(StringComparer.Ordinal)
        {
            ["HNS_Giant"] = new("Common", "Fuller habit", 1.18f, 1.03f, 1.04f, 1f, 0, 0f, 1f, 1f, 0f),
            ["HNS_Dwarf"] = new("Common", "Compact habit", 0.85f, 1.06f, 0.92f, 1f, 0, 0f, 1f, 1f, 0f),
            ["HNS_Humongous"] = new("Exceptional", "Massive layered growth; gilded ridges", 1.9f, 1.12f, 1.12f, 1.18f, 4, 0.53f, 1.04f, 1.05f, 0.1f),
            ["HNS_ColdResistant"] = new("Common", "Faint cool wax bloom", 1f, 1.02f, 0.98f, 1f, 3, 0.23f, 0.96f, 1f, 0f),
            ["HNS_ColdVulnerable"] = new("Common", "Pale purple tip blush", 1f, 0.98f, 1.03f, 1f, 2, 0.18f, 0.96f, 0.94f, 0f),
            ["HNS_HeatResistant"] = new("Common", "Warm amber cuticle", 1f, 1.02f, 0.96f, 1f, 6, 0.23f, 1.02f, 1.05f, 0f),
            ["HNS_HeatVulnerable"] = new("Common", "Dry brown tip marks", 1f, 0.98f, 1.02f, 0.95f, 7, 0.22f, 0.96f, 0.95f, 0f),
            ["HNS_DiseaseResistant"] = new("Common", "Dark healthy leaf cuticle", 1f, 1.02f, 1f, 1f, 8, 0.2f, 1.01f, 1.04f, 0f),
            ["HNS_DiseaseVulnerable"] = new("Common", "Muted irregular lesion mottling", 1f, 0.97f, 1f, 0.94f, 7, 0.32f, 0.97f, 0.96f, 0f),
            ["HNS_HighYield"] = new("Common", "Full, closely packed habit", 1.06f, 1.04f, 1f, 1.18f, 0, 0f, 1.04f, 1.04f, 0f),
            ["HNS_LowYield"] = new("Common", "Sparse smaller habit", 0.96f, 0.98f, 1f, 0.82f, 0, 0f, 0.98f, 0.96f, 0f),
            ["HNS_Easy"] = new("Common", "Open rounded habit", 0.98f, 1.06f, 0.96f, 0.96f, 0, 0f, 1.02f, 1f, 0f),
            ["HNS_Difficult"] = new("Common", "Upright wiry habit", 1f, 0.94f, 1.08f, 1.03f, 0, 0f, 0.98f, 1.02f, 0f),
            ["HNS_EasySow"] = new("Common", "Low broad habit", 0.96f, 1.1f, 0.91f, 1f, 0, 0f, 1f, 1f, 0f),
            ["HNS_DifficultSow"] = new("Common", "Tall tight clustered habit", 1f, 0.93f, 1.12f, 1.08f, 0, 0f, 1f, 1.02f, 0f),
            ["HNS_EasyHarvest"] = new("Common", "Open loose canopy", 1f, 1.08f, 0.98f, 0.87f, 0, 0f, 1.02f, 1f, 0f),
            ["HNS_DifficultHarvest"] = new("Common", "Close dense canopy", 1f, 0.98f, 1.04f, 1.14f, 0, 0f, 0.98f, 1.01f, 0f),
            ["HNS_ResourceDependent"] = new("Uncommon", "Mineral mottling (family)", 1f, 1f, 1f, 1f, 9, 0.32f, 1f, 1f, 0f),
            ["HNS_ResourceDependent_Mulch"] = new("Uncommon", "Earthy bronze grain", 1f, 1.02f, 1f, 1f, 6, 0.35f, 0.97f, 1f, 0f),
            ["HNS_ResourceDependent_Hay"] = new("Uncommon", "Straw-colored streaks", 1f, 1f, 1.06f, 1f, 1, 0.32f, 1f, 1f, 0f),
            ["HNS_ResourceDependent_Fungus"] = new("Uncommon", "Fungal ivory speckling", 1f, 1.04f, 1f, 1f, 9, 0.52f, 0.97f, 1f, 0f),
            ["HNS_Perennial"] = new("Uncommon", "Mature bronze/jade canopy", 1.04f, 1.07f, 0.98f, 1.04f, 8, 0.3f, 0.99f, 1.05f, 0f),
            ["HNS_SelfSeeding"] = new("Uncommon", "Small seed-like gold flecks", 1f, 1.03f, 1f, 1.08f, 4, 0.33f, 1.02f, 1f, 0f),
            ["HNS_Delicate"] = new("Common", "Slender pale tissue", 1f, 0.91f, 1.1f, 1f, 3, 0.13f, 1.04f, 0.92f, 0f),
            ["HNS_Robust"] = new("Common", "Broad squat dark-green habit", 1f, 1.12f, 0.9f, 1.05f, 8, 0.19f, 0.98f, 1.04f, 0f),
            ["HNS_VeryDelicate"] = new("Rare", "Ethereal thin silver foliage", 0.95f, 0.74f, 1.25f, 0.84f, 3, 0.57f, 1.02f, 0.91f, 0f),
            ["HNS_Forage"] = new("Uncommon", "Open grassy habit, mild golden tips", 1f, 1.04f, 0.98f, 0.78f, 6, 0.23f, 1.02f, 1.03f, 0f),
            ["HNS_FishHabitat"] = new("Uncommon", "Blue-green water ripple markings", 1f, 1.09f, 0.98f, 1f, 10, 0.42f, 1f, 1f, 0f),
            ["HNS_AquaticForage"] = new("Uncommon", "Olive-teal wet cuticle", 1f, 1.03f, 1f, 0.94f, 10, 0.3f, 1f, 1f, 0f),
            ["HNS_Aquatic"] = new("Uncommon", "Teal rippled pigment", 1f, 1.05f, 1f, 1f, 10, 0.38f, 1f, 1f, 0f),
            ["HNS_Sandy"] = new("Uncommon", "Dune-gold wax and brown tips", 1f, 0.94f, 1.04f, 0.95f, 6, 0.41f, 1.02f, 0.96f, 0f),
            ["HNS_Synergy"] = new("Uncommon", "Interconnected jade ridges (family)", 1f, 1f, 1f, 1f, 5, 0.3f, 1f, 1f, 0f),
            ["HNS_Byproduct"] = new("Uncommon", "Pale accessory flecks (family)", 1f, 1f, 1f, 1f, 4, 0.32f, 1f, 1f, 0f),
            ["HNS_HayByproduct"] = new("Uncommon", "Straw-gold linear streaks", 1f, 1.04f, 1.08f, 1.03f, 1, 0.33f, 1f, 1f, 0f),
            ["HNS_Resinous"] = new("Uncommon", "Lacquered droplets (family)", 1f, 1f, 1f, 1f, 11, 0.42f, 1f, 1.04f, 0f),
            ["HNS_Joyresin"] = new("Uncommon", "Honey-gold resin droplets", 1f, 1.03f, 1f, 1f, 11, 0.44f, 1f, 1.05f, 0f),
            ["HNS_Painresin"] = new("Rare", "Violet-black lacquer", 1f, 1.04f, 1f, 1f, 12, 0.57f, 0.97f, 0.98f, 0f),
            ["HNS_Burnresin"] = new("Rare", "Copper-red resin beads", 1f, 1.02f, 1f, 1f, 13, 0.62f, 1f, 1.04f, 0f),
            ["HNS_Toxresin"] = new("Rare", "Lime-green resin deposits", 1f, 1.03f, 1f, 1f, 14, 0.6f, 1f, 1.03f, 0f),
            ["HNS_Thorny"] = new("Common", "Dark serration and restrained spikes", 1f, 1.02f, 1f, 1f, 15, 0.31f, 1f, 1.02f, 0f),
            ["HNS_Beautiful"] = new("Common", "Soft ivory/gold highlight", 1f, 1.03f, 1.02f, 1.02f, 1, 0.22f, 1.04f, 1.06f, 0.08f),
            ["HNS_Ugly"] = new("Common", "Dusky bruised coloration", 1f, 0.98f, 1.02f, 1f, 7, 0.35f, 0.94f, 0.88f, 0f),
            ["HNS_ProduceColor"] = new("Uncommon", "Restrained inherited fruit pigment", 1f, 1f, 1f, 1f, 0, 0f, 1f, 1f, 0f),
            ["HNS_LeafColor"] = new("Uncommon", "Restrained inherited foliage pigment", 1f, 1f, 1f, 1f, 0, 0f, 1f, 1f, 0f),
            ["HNS_StemColor"] = new("Uncommon", "Restrained inherited stem pigment", 1f, 1f, 1f, 1f, 0, 0f, 1f, 1f, 0f),
            ["HNS_Nutritious"] = new("Variable", "Progressive rich gold speckles", 1f, 1f, 1f, 1f, 4, 0.2f, 1f, 1f, 0f),
            ["HNS_HighlyMedicinal"] = new("Uncommon", "Cool silver-green vein-like markings", 1f, 1.03f, 1f, 1f, 5, 0.4f, 1f, 1f, 0f),
            ["HNS_Compound"] = new("Uncommon", "Violet pigment accents (family)", 1f, 1f, 1f, 1f, 2, 0.3f, 1f, 1f, 0f),
            ["HNS_Joycompound"] = new("Uncommon", "Soft pink/violet blush", 1f, 1.02f, 1f, 1f, 2, 0.45f, 1.01f, 1.03f, 0f),
            ["HNS_Variegated"] = new("Exceptional", "Ivory sectors", 1f, 1f, 1f, 1f, 1, 0.86f, 1f, 1f, 0f),
            ["HNS_Anthocyanin"] = new("Exceptional", "Crimson blush", 1f, 1f, 1f, 1f, 2, 0.92f, 1f, 1f, 0f),
            ["HNS_MoonFrost"] = new("Exceptional", "Silver-blue bloom", 1f, 1f, 1f, 1f, 3, 0.92f, 1f, 1f, 0f),
            ["HNS_GoldFreckled"] = new("Exceptional", "Gold mottling", 1f, 1f, 1f, 1f, 4, 0.9f, 1f, 1f, 0f),
            ["HNS_AuroraVeins"] = new("Exceptional", "Bright turquoise branching motifs", 1f, 1f, 1f, 1f, 5, 0.95f, 1f, 1f, 0f),
        };

        internal static bool TryGet(VarietyTraitDef trait, out Profile profile)
        {
            profile = default;
            if (trait == null) return false;
            if (Profiles.TryGetValue(trait.defName ?? "", out profile)) return true;
            // Generated color IDs retain their original allele RGB and identity.
            if (ColorTraitFactory.IsColorFamily(trait.configFamily))
                return Profiles.TryGetValue("HNS_" + trait.configFamily, out profile);
            if (trait.configFamily == PercentageTraitFactory.NutritiousFamily && trait.percentageBonus > 0)
            {
                float potency = Mathf.Clamp01(trait.percentageBonus / 100f);
                profile = Profiles["HNS_Nutritious"] with
                {
                    Tier = trait.percentageBonus <= 5 ? "Common" : trait.percentageBonus <= 15 ? "Uncommon" : trait.percentageBonus < 50 ? "Rare" : "Exceptional",
                    Scale = 1f + .24f * potency, Density = 1f + .30f * potency,
                    Strength = .13f + .77f * potency, Saturation = 1f + .13f * potency,
                    Radiance = .14f * Mathf.InverseLerp(.50f, 1f, potency)
                };
                return true;
            }
            if (trait.configFamily == "Synergy")
            {
                profile = Profiles["HNS_Synergy"] with
                {
                    Pattern = trait.synergyStat switch { "Yield" => 4, "SowSpeed" => 1, "HarvestSpeed" => 6, "DiseaseResistance" => 8, _ => 5 },
                    Motif = "Companion-linked " + (trait.synergyStat ?? "growth") + " appearance"
                };
                return true;
            }
            return false; // external/custom defs keep their authored legacy settings
        }

        // Shader patterns are additive visual defaults, not genetic traits.
        // Explicit procedural alleles retain priority. For combined mechanical
        // traits choose the strongest motif, ties resolved by defName. Geometry
        // and color continue to compose through the existing settings resolver.
        internal static bool TryPattern(VarietyTraitDef trait, out int mode, out float strength)
        {
            mode = 0; strength = 0f;
            if (trait == null || trait.produceOnlyVisual) return false;
            if (trait.proceduralPhenotype != ProceduralPhenotype.None)
            {
                mode = (int)trait.proceduralPhenotype;
                strength = float.IsFinite(trait.proceduralStrength) ? Mathf.Clamp01(trait.proceduralStrength) : 0f;
                return mode >= 1 && mode <= 5 && strength > 0f;
            }
            if (!TryGet(trait, out var p) || p.Pattern == 0) return false;
            mode = p.Pattern; strength = p.Strength;
            return true;
        }

        // Called only while constructing default records; existing saved explicit
        // overrides are not rewritten. Regional color is handled separately.
        internal static void Apply(VarietyTraitDef trait, VisualSettingsRecord visual)
        {
            if (!TryGet(trait, out var p)) return;
            visual.scale = p.Scale; visual.width = p.Width; visual.height = p.Height;
            visual.density = p.Density; visual.brightness = p.Brightness; visual.saturation = p.Saturation;
            visual.radiance = p.Radiance;
            visual.gloom = 0f; visual.dullness = 0f;
            visual.tintRed = visual.tintGreen = visual.tintBlue = 1f;
            if (trait.visualMaskIndex >= 0 && !trait.configRoot)
            {
                // Preserve the exact allele in DefDatabase and inheritance. Only
                // the rendered color strength is moderated.
                visual.tintRed = Mathf.Lerp(1f, trait.tintRed, .52f);
                visual.tintGreen = Mathf.Lerp(1f, trait.tintGreen, .52f);
                visual.tintBlue = Mathf.Lerp(1f, trait.tintBlue, .52f);
            }
            if (trait.defName == "HNS_Thorny")
            {
                visual.overlayPattern = 1; visual.overlayIntensity = .28f; visual.overlayScale = .83f;
            }
            // Mild legacy fallback tint when a compatible GPU shader is absent.
            // Do not double tint the five already-approved procedural alleles.
            if (trait.proceduralPhenotype == ProceduralPhenotype.None && p.Pattern > 0)
            {
                Color tint = p.Pattern switch
                {
                    1 => new Color(1f,.96f,.83f), 2 => new Color(1f,.75f,.85f),
                    3 => new Color(.80f,.92f,1f), 4 => new Color(1f,.93f,.74f),
                    5 => new Color(.78f,1f,.93f), 6 => new Color(1f,.91f,.72f),
                    7 => new Color(.78f,.70f,.61f), 8 => new Color(.82f,1f,.85f),
                    9 => new Color(.94f,.88f,.78f), 10 => new Color(.73f,.99f,1f),
                    11 => new Color(1f,.86f,.62f), 12 => new Color(.81f,.66f,.95f),
                    13 => new Color(1f,.67f,.44f), 14 => new Color(.76f,1f,.55f),
                    15 => new Color(.82f,.77f,.66f), _ => Color.white
                };
                float intensity = p.Strength * .45f;
                visual.tintRed = Mathf.Lerp(1f,tint.r,intensity);
                visual.tintGreen = Mathf.Lerp(1f,tint.g,intensity);
                visual.tintBlue = Mathf.Lerp(1f,tint.b,intensity);
            }
        }
    }
}
