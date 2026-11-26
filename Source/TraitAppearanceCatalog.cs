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
            ["HNS_Variegated"] = new("Exceptional", "Cream and sage sectors concentrated on foliage; contrasting fruit and stems remain recognizable.", 1f, 1f, 1f, 1f, 1, 0.84f, 1f, 1f, 0f),
            ["HNS_Anthocyanin"] = new("Exceptional", "Wine-red and dusky rose foliage blush, strongest on selected tips.", 1f, 1f, 1f, 1f, 2, 0.84f, 1f, 1f, 0f),
            ["HNS_MoonFrost"] = new("Exceptional", "Cool sage-silver wax bloom with restrained pale margins.", 1f, 1f, 1f, 1f, 3, 0.84f, 1f, 1f, 0f),
            ["HNS_GoldFreckled"] = new("Exceptional", "Ochre freckles and irregular gold mottling on foliage.", 1f, 1f, 1f, 1f, 4, 0.86f, 1f, 1f, 0f),
            ["HNS_AuroraVeins"] = new("Exceptional", "Pale mint branching pigmentation over natural leaf tissue; no emitted light.", 1f, 1f, 1f, 1f, 5, 0.89f, 1f, 1f, 0f),
            ["HNS_Velvetleaf"] = new("Uncommon", "Deep moss and forest-green foliage with softly shaded velvet-like tissue.", 1f, 1f, 1f, 1f, 16, 0.72f, 1f, 1f, 0f),
            ["HNS_Lacquered"] = new("Uncommon", "Dark glossy-looking green foliage with intermittent pale reflections.", 1f, 1f, 1f, 1f, 17, 0.74f, 1f, 1f, 0f),
            ["HNS_Dustbloom"] = new("Uncommon", "Soft chalk and warm sage powder concentrated in irregular leaf patches.", 1f, 1f, 1f, 1f, 18, 0.68f, 1f, 1f, 0f),
            ["HNS_Silvered"] = new("Uncommon", "Silver-sage leaf tissue with directional pale highlights.", 1f, 1f, 1f, 1f, 19, 0.74f, 1f, 1f, 0f),
            ["HNS_HaloEdged"] = new("Rare", "Ivory inner leaf margins with a predominantly natural green interior.", 1f, 1f, 1f, 1f, 20, 0.84f, 1f, 1f, 0f),
            ["HNS_Netveined"] = new("Rare", "Pale yellow-green reticulated markings within natural foliage.", 1f, 1f, 1f, 1f, 21, 0.86f, 1f, 1f, 0f),
            ["HNS_Tigerstriped"] = new("Rare", "Broken ochre and muted brown bands interspersed with green tissue.", 1f, 1f, 1f, 1f, 22, 0.88f, 1f, 1f, 0f),
            ["HNS_Marbled"] = new("Rare", "Organic sage and cream marbling, with original green visible between sectors.", 1f, 1f, 1f, 1f, 23, 0.86f, 1f, 1f, 0f),
            ["HNS_Opaline"] = new("Exceptional", "Subtle rose, sage and pearl highlights; a static iridescence illusion.", 1f, 1f, 1f, 1f, 24, 0.87f, 1f, 1f, 0f),
            ["HNS_Crystalline"] = new("Exceptional", "Angular pale mineral-like markings concentrated at foliage tips; no physical crystals.", 1f, 1f, 1f, 1f, 25, 0.88f, 1f, 1f, 0f),
            ["HNS_Bioluminescent"] = new("Exceptional", "Sparse pale lime and mint flecks on green tissue; a pigment illusion, not emitted light.", 1f, 1f, 1f, 1f, 26, 0.9f, 1f, 1f, 0f),
            ["HNS_NebulaMottle"] = new("Exceptional", "Patchy wine-purple and olive mottling with pale speckles.", 1f, 1f, 1f, 1f, 27, 0.88f, 1f, 1f, 0f),
            ["HNS_Coralbranch"] = new("Exceptional", "Muted salmon and copper branching markings on green tissue.", 1f, 1f, 1f, 1f, 28, 0.9f, 1f, 1f, 0f),
            ["HNS_Prismatic"] = new("Exceptional", "Muted olive, copper and plum pigmentation in neighboring highlights.", 1f, 1f, 1f, 1f, 29, 0.88f, 1f, 1f, 0f),
            ["HNS_Saltbloom"] = new("Uncommon", "Intermittent chalk-white deposits at foliage margins.", 1f, 1f, 1f, 1f, 30, 0.68f, 1f, 1f, 0f),
            ["HNS_Ashen"] = new("Uncommon", "Soft gray-green dusty tissue with retained plant shading.", 1f, 1f, 1f, 1f, 31, 0.7f, 1f, 1f, 0f),
            ["HNS_Boggloss"] = new("Uncommon", "Dark olive and jade wet-looking patches with subtle pale reflections.", 1f, 1f, 1f, 1f, 32, 0.72f, 1f, 1f, 0f),
            ["HNS_Waxbound"] = new("Uncommon", "Warm jade and yellow-green waxy highlights.", 1f, 1f, 1f, 1f, 33, 0.7f, 1f, 1f, 0f),
            ["HNS_Peatstained"] = new("Uncommon", "Earthy brown and olive pigment concentrated toward lower foliage.", 1f, 1f, 1f, 1f, 34, 0.7f, 1f, 1f, 0f),
            ["HNS_ScorchedMargins"] = new("Uncommon", "Copper-brown dry leaf rims and tips with retained green interior.", 1f, 1f, 1f, 1f, 35, 0.77f, 1f, 1f, 0f),
            ["HNS_HoneyHarvest"] = new("Uncommon", "Honey-gold pigmentation in contrasting fruit, seed or flower tissue; foliage remains green.", 1f, 1f, 1f, 1f, 36, 0.85f, 1f, 1f, 0f),
            ["HNS_IvoryHarvest"] = new("Rare", "Cream-white reproductive tissue with retained natural shading.", 1f, 1f, 1f, 1f, 37, 0.92f, 1f, 1f, 0f),
            ["HNS_MulberryHarvest"] = new("Rare", "Deep mulberry and plum pigmentation in contrasting fruit or flower tissue.", 1f, 1f, 1f, 1f, 38, 0.93f, 1f, 1f, 0f),
            ["HNS_RoseHarvest"] = new("Rare", "Soft rose-colored fruit and flower tissue, distinct from green foliage.", 1f, 1f, 1f, 1f, 39, 0.9f, 1f, 1f, 0f),
            ["HNS_BicolorHarvest"] = new("Exceptional", "Cream and red-blushed sectors in contrasting fruit or flower tissue.", 1f, 1f, 1f, 1f, 40, 0.95f, 1f, 1f, 0f),
            ["HNS_CreamMargins"] = new("Rare", "Irregular cream leaf margins with a retained green center.", 1f, 1f, 1f, 1f, 41, 0.91f, 1f, 1f, 0f),
            ["HNS_LimeHeart"] = new("Uncommon", "Yellow-green leaf centers and darker outer foliage.", 1f, 1f, 1f, 1f, 42, 0.83f, 1f, 1f, 0f),
            ["HNS_CopperTips"] = new("Uncommon", "Bronze and copper pigmentation concentrated in upper foliage.", 1f, 1f, 1f, 1f, 43, 0.86f, 1f, 1f, 0f),
            ["HNS_IvorySpeckle"] = new("Rare", "Scattered ivory leaf spots with irregular natural spacing.", 1f, 1f, 1f, 1f, 44, 0.91f, 1f, 1f, 0f),
            ["HNS_BurgundyMargins"] = new("Rare", "Wine-red leaf margins and dark green interior tissue.", 1f, 1f, 1f, 1f, 45, 0.92f, 1f, 1f, 0f),
            ["HNS_RubyStalk"] = new("Uncommon", "Wine-red pigmentation concentrated in lower central green tissue; approximate stalk targeting.", 1f, 1f, 1f, 1f, 46, 0.91f, 1f, 1f, 0f),
            ["HNS_AmberStalk"] = new("Uncommon", "Golden ochre lower central tissue with naturally green outer leaves.", 1f, 1f, 1f, 1f, 47, 0.9f, 1f, 1f, 0f),
            ["HNS_StripedStalk"] = new("Rare", "Alternating wine and green markings on lower central tissue.", 1f, 1f, 1f, 1f, 48, 0.94f, 1f, 1f, 0f),
            ["HNS_BlueJade"] = new("Uncommon", "Muted blue-green foliage with jade highlights, retaining original fruit colors.", 1f, 1f, 1f, 1f, 49, 0.85f, 1f, 1f, 0f),
            ["HNS_TwoToneCanopy"] = new("Rare", "Deep green lower foliage with lighter lime-green upper growth.", 1f, 1f, 1f, 1f, 50, 0.91f, 1f, 1f, 0f),
            ["HNS_BronzeFlush"] = new("Uncommon", "Copper-bronze juvenile leaves that mature into deep green foliage.", 1f, 1f, 1f, 1f, 51, 0.91f, 1f, 1f, 0f),
            ["HNS_Frostbud"] = new("Rare", "Cream-white juvenile tips and pale leaf margins which gradually green as the plant develops.", 1f, 1f, 1f, 1f, 52, 0.92f, 1f, 1f, 0f),
            ["HNS_AutumnCrown"] = new("Rare", "Green juvenile foliage that develops golden and russet upper growth at maturity.", 1f, 1f, 1f, 1f, 53, 0.94f, 1f, 1f, 0f),
            ["HNS_RipeningBlush"] = new("Rare", "Contrasting reproductive tissue ripens from pale honey to rose and mulberry tones.", 1f, 1f, 1f, 1f, 54, 0.95f, 1f, 1f, 0f),
            ["HNS_MaturingVariegation"] = new("Rare", "Green young leaves gradually develop irregular cream and sage variegation.", 1f, 1f, 1f, 1f, 55, 0.92f, 1f, 1f, 0f),
            ["HNS_GapSeeker"] = new("Rare", "Upper growth bends away from occupied lateral cells toward a gap.", 1f, 1f, 1f, 1f, 0, 0f, 1f, 1f, 0f),
            ["HNS_CompanionBow"] = new("Rare", "Upper growth inclines toward a neighboring plant.", 1f, 1f, 1f, 1f, 0, 0f, 1f, 1f, 0f),
            ["HNS_CrownPartition"] = new("Rare", "Crowns restrict the occupied side and expand into the opposite open space.", 1f, 1f, 1f, 1f, 0, 0f, 1f, 1f, 0f),
            ["HNS_StandSpire"] = new("Rare", "Crowded individuals form narrow, elongated crowns.", 1f, 1f, 1f, 1f, 0, 0f, 1f, 1f, 0f),
            ["HNS_FreeRosette"] = new("Rare", "Isolated plants form broad low rosettes that become compact beside neighbors.", 1f, 1f, 1f, 1f, 0, 0f, 1f, 1f, 0f),
            ["HNS_Arching"] = new("Uncommon", "A gently curved, asymmetric upright growth habit.", 1f, 1f, 1f, 1f, 0, 0f, 1f, 1f, 0f),
            ["HNS_FanCrowned"] = new("Uncommon", "A broad spreading upper canopy with a narrower base.", 1f, 1f, 1f, 1f, 0, 0f, 1f, 1f, 0f),
            ["HNS_Rounded"] = new("Common", "A fuller middle canopy with compact rounded upper growth.", 1f, 1f, 1f, 1f, 0, 0f, 1f, 1f, 0f),
            ["HNS_Weeping"] = new("Rare", "A spreading canopy with drooping outer foliage.", 1f, 1f, 1f, 1f, 0, 0f, 1f, 1f, 0f),
            ["HNS_Spired"] = new("Rare", "A tapered, elongated upright canopy with narrow upper growth.", 1f, 1f, 1f, 1f, 0, 0f, 1f, 1f, 0f),
            ["HNS_Rootflare"] = new("Rare", "Visible, branching surface roots around the base of a mature plant.", 1f, 1f, 1f, 1f, 0, 0f, 1f, 1f, 0f),
            ["HNS_RunnerHabit"] = new("Rare", "Low creeping stems with small leafy daughter shoots.", 1f, 1f, 1f, 1f, 0, 0f, 1f, 1f, 0f),
            ["HNS_BasalCarpet"] = new("Uncommon", "A low rosette of broad leaves surrounding the main stem.", 1f, 1f, 1f, 1f, 0, 0f, 1f, 1f, 0f),
            ["HNS_LeafLitter"] = new("Uncommon", "A few naturally shed, differently oriented leaves near the plant.", 1f, 1f, 1f, 1f, 0, 0f, 1f, 1f, 0f),
            ["HNS_PetalDrift"] = new("Rare", "Scattered colorful petals beneath flowering plants.", 1f, 1f, 1f, 1f, 0, 0f, 1f, 1f, 0f),
            ["HNS_SuccessionBloom"] = new("Rare", "A plant carries unopened buds and open reproductive organs in a stable individual arrangement.", 1f, 1f, 1f, 1f, 0, 0f, 1f, 1f, 0f),
            ["HNS_SectorSport"] = new("Rare", "A local reproductive sector develops a contrasting rose-colored sport while the rest of the plant retains its native appearance.", 1f, 1f, 1f, 1f, 0, 0f, 1f, 1f, 0f),
            ["HNS_BiformHarvest"] = new("Rare", "Separate reproductive organs express stout and slender forms within one plant.", 1f, 1f, 1f, 1f, 0, 0f, 1f, 1f, 0f),
            ["HNS_SisterBuds"] = new("Rare", "Selected reproductive organs carry small adjacent daughter buds.", 1f, 1f, 1f, 1f, 0, 0f, 1f, 1f, 0f),
            ["HNS_RipeningMosaic"] = new("Rare", "Separate organs show unripe, ripe and late-stage botanical pigmentation in a stable individual mosaic.", 1f, 1f, 1f, 1f, 0, 0f, 1f, 1f, 0f),
            ["HNS_DawnPetals"] = new("Rare", "Individual reproductive organs open with daylight.", 1f, 1f, 1f, 1f, 0, 0f, 1f, 1f, 0f),
            ["HNS_DuskPetals"] = new("Rare", "Individual reproductive organs open after dark.", 1f, 1f, 1f, 1f, 0, 0f, 1f, 1f, 0f),
            ["HNS_RainPendant"] = new("Rare", "Reproductive organs droop independently under rain.", 1f, 1f, 1f, 1f, 0, 0f, 1f, 1f, 0f),
            ["HNS_SolarTilt"] = new("Rare", "Individual reproductive organs tilt with daytime light direction.", 1f, 1f, 1f, 1f, 0, 0f, 1f, 1f, 0f),
            ["HNS_SilkFlutter"] = new("Rare", "Reproductive organs flutter independently of the wind-bent plant.", 1f, 1f, 1f, 1f, 0, 0f, 1f, 1f, 0f),
            ["HNS_Nightfold"] = new("Rare", "Foliage closes inward when the sky becomes dark.", 1f, 1f, 1f, 1f, 0, 0f, 1f, 1f, 0f),
            ["HNS_Sunseeker"] = new("Uncommon", "The upper canopy leans gently toward the shifting daytime light.", 1f, 1f, 1f, 1f, 0, 0f, 1f, 1f, 0f),
            ["HNS_Rainbowed"] = new("Uncommon", "Foliage bows and droops under rain.", 1f, 1f, 1f, 1f, 0, 0f, 1f, 1f, 0f),
            ["HNS_Nightbloom"] = new("Rare", "A compact daytime habit that opens into a broad canopy after dark.", 1f, 1f, 1f, 1f, 0, 0f, 1f, 1f, 0f),
            ["HNS_Raincup"] = new("Rare", "Foliage cups inward and upward as rain begins.", 1f, 1f, 1f, 1f, 0, 0f, 1f, 1f, 0f),
            ["HNS_ForkedCrown"] = new("Rare", "Two broad, diverging foliage crowns above the original growth.", 1f, 1f, 1f, 1f, 0, 0f, 1f, 1f, 0f),
            ["HNS_TieredCanopy"] = new("Rare", "Overlapping tiers of foliage with a dense upper canopy.", 1f, 1f, 1f, 1f, 0, 0f, 1f, 1f, 0f),
            ["HNS_SpreadingRosette"] = new("Uncommon", "Low, outward-growing leafy structures around the original plant.", 1f, 1f, 1f, 1f, 0, 0f, 1f, 1f, 0f),
            ["HNS_FlowerSpray"] = new("Rare", "Additional leafy sprays carrying blossoms or fruit derived from the original artwork.", 1f, 1f, 1f, 1f, 0, 0f, 1f, 1f, 0f),
            ["HNS_AbundantClusters"] = new("Rare", "Additional reproductive structures distributed on existing foliage.", 1f, 1f, 1f, 1f, 0, 0f, 1f, 1f, 0f),
            ["HNS_LobedLeaf"] = new("Uncommon", "Rounded notches along the existing foliage silhouette.", 1f, 1f, 1f, 1f, 0, 0f, 1f, 1f, 0f),
            ["HNS_Windowleaf"] = new("Rare", "Small natural openings in broad leaf tissue.", 1f, 1f, 1f, 1f, 0, 0f, 1f, 1f, 0f),
            ["HNS_SideShoots"] = new("Uncommon", "Additional small branches and leaflets emerging from the canopy.", 1f, 1f, 1f, 1f, 0, 0f, 1f, 1f, 0f),
            ["HNS_CurlingTendrils"] = new("Rare", "Fine curling stems extending from the existing plant.", 1f, 1f, 1f, 1f, 0, 0f, 1f, 1f, 0f),
            ["HNS_LanternBuds"] = new("Rare", "Localized reproductive or leaf-tip luminescence visible in darkness.", 1f, 1f, 1f, 1f, 0, 0f, 1f, 1f, 0f),
            ["HNS_SatinCuticle"] = new("Uncommon", "Smooth leaf surfaces with directional satin highlights.", 1f, 1f, 1f, 1f, 0, 0f, 1f, 1f, 0f),
            ["HNS_VelvetNap"] = new("Uncommon", "Soft matte foliage with diffuse edge lighting and fine nap.", 1f, 1f, 1f, 1f, 0, 0f, 1f, 1f, 0f),
            ["HNS_SilverBloom"] = new("Rare", "A pale waxy surface that catches light along raised areas.", 1f, 1f, 1f, 1f, 0, 0f, 1f, 1f, 0f),
            ["HNS_RainBeads"] = new("Rare", "Hydrophobic foliage with localized simulated droplets in rain.", 1f, 1f, 1f, 1f, 0, 0f, 1f, 1f, 0f),
            ["HNS_PearlCuticle"] = new("Rare", "Subtle cool and warm structural color shifts with the light direction.", 1f, 1f, 1f, 1f, 0, 0f, 1f, 1f, 0f),
            ["HNS_RigidHabit"] = new("Uncommon", "A stiff, upright plant with very little wind movement.", 1f, 1f, 1f, 1f, 0, 0f, 1f, 1f, 0f),
            ["HNS_SuppleHabit"] = new("Uncommon", "Flexible foliage that sways farther in the breeze.", 1f, 1f, 1f, 1f, 0, 0f, 1f, 1f, 0f),
            ["HNS_Flutterleaf"] = new("Rare", "A light, quick secondary flutter over ordinary swaying.", 1f, 1f, 1f, 1f, 0, 0f, 1f, 1f, 0f),
            ["HNS_NoddingHabit"] = new("Rare", "A gentle forward-and-back nod, with less sideways bending.", 1f, 1f, 1f, 1f, 0, 0f, 1f, 1f, 0f),
            ["HNS_SweepingHabit"] = new("Rare", "A broad side-to-side sweep of the canopy.", 1f, 1f, 1f, 1f, 0, 0f, 1f, 1f, 0f),
            ["HNS_PlumpFruit"] = new("Rare", "Larger rounded fruit or flower structures, with the original foliage retained.", 1f, 1f, 1f, 1f, 0, 0f, 1f, 1f, 0f),
            ["HNS_LongFruit"] = new("Uncommon", "Elongated fruit or reproductive structures.", 1f, 1f, 1f, 1f, 0, 0f, 1f, 1f, 0f),
            ["HNS_BroadFruit"] = new("Uncommon", "Wider fruit or flower structures, leaving the canopy intact.", 1f, 1f, 1f, 1f, 0, 0f, 1f, 1f, 0f),
            ["HNS_ClusteredFruit"] = new("Rare", "Closely overlapping reproductive lobes derived from the original art.", 1f, 1f, 1f, 1f, 0, 0f, 1f, 1f, 0f),
            ["HNS_ThickStalk"] = new("Uncommon", "Broadened automatically identified stem tissue; unchanged when anatomy is uncertain.", 1f, 1f, 1f, 1f, 0, 0f, 1f, 1f, 0f),
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
                return ProceduralPhenotypeUtility.IsExplicitMode(trait.proceduralPhenotype) && strength > 0f;
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
