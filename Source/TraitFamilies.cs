using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace HorticultureNovelSeeds
{
    public static class TraitFamilyResolver
    {
        public static VarietyTraitDef Resolve(VarietyTraitDef root, ThingDef cropDef, NovelSeedsSettings settings)
        {
            if (root == null || root.configFamily.NullOrEmpty()) return root;
            if (ColorTraitFactory.IsColorFamily(root.configFamily)) return ColorTraitFactory.Select(root, cropDef);
            if (root.configFamily == PercentageTraitFactory.NutritiousFamily) return PercentageTraitFactory.Select();
            FamilySettingsRecord family = settings?.GetFamilySettings(root.configFamily, false);
            if (family?.enabled == false) return null;
            if (root.configFamily == "Synergy") return SynergyTraitFactory.Select(cropDef, settings);

            List<VarietyTraitDef> types = TraitConfigUtility.Types(root.configFamily)
                .Where(t => settings == null || settings.IsTraitAllowed(cropDef, t))
                .ToList();
            if (types.TryRandomElementByWeight(t => family?.GetType(t, false)?.weight ?? Mathf.Max(0f, t.commonality), out VarietyTraitDef selected))
                return selected;
            return null;
        }
    }

    public static class SynergyTraitFactory
    {
        public static readonly List<string> StatOptions = new List<string> { "GrowthRate", "Yield", "SowSpeed", "HarvestSpeed", "DiseaseResistance" };
        private static bool generated;

        public static string StatLabel(string stat)
        {
            switch (stat)
            {
                case "GrowthRate": return "Growth rate";
                case "Yield": return "Harvest yield";
                case "SowSpeed": return "Sowing speed";
                case "HarvestSpeed": return "Harvest speed";
                case "DiseaseResistance": return "Disease resistance";
                default: return stat;
            }
        }

        public static void GenerateAll()
        {
            if (generated) return;
            generated = true;
            VarietyTraitDef root = DefDatabase<VarietyTraitDef>.GetNamedSilentFail("HNS_Synergy");
            if (root == null) return;
            List<VarietyTraitDef> additions = new List<VarietyTraitDef>();
            foreach (ThingDef plant in DefDatabase<ThingDef>.AllDefsListForReading.Where(NovelSeedUtility.IsGrowableCrop))
            {
                foreach (string stat in StatOptions)
                {
                    string defName = "HNS_SynergyGenerated_" + Sanitize(plant.defName) + "_" + stat;
                    if (DefDatabase<VarietyTraitDef>.GetNamedSilentFail(defName) != null) continue;
                    additions.Add(new VarietyTraitDef
                    {
                        defName = defName,
                        label = "Synergy (" + plant.LabelCap + ") (" + StatLabel(stat) + ")",
                        description = "Gains 15% " + StatLabel(stat).ToLowerInvariant() + " when grown within three cells of " + plant.LabelCap + ".",
                        positive = true,
                        traitTags = new List<string> { "Positive" },
                        balanceValue = 1f,
                        commonality = 0f,
                        configCategory = "Synergy",
                        configFamily = "Synergy",
                        configType = plant.defName + "|" + stat,
                        hiddenFromConfig = true,
                        synergyPlantDef = plant,
                        synergyStat = stat,
                        synergyFactor = 1.15f,
                        exclusionTags = new List<string> { "synergy" },
                        generated = true,
                        modContentPack = root.modContentPack
                    });
                }
            }
            if (additions.Count > 0) DefDatabase<VarietyTraitDef>.Add(additions);
        }

        public static VarietyTraitDef Select(ThingDef cropDef, NovelSeedsSettings settings)
        {
            GenerateAll();
            FamilySettingsRecord family = settings?.GetFamilySettings("Synergy", false);
            List<ThingDef> plants = DefDatabase<ThingDef>.AllDefsListForReading.Where(NovelSeedUtility.IsGrowableCrop)
                .Where(p => family?.GetPlant(p, false)?.enabled != false).ToList();
            List<string> stats = StatOptions.Where(s => family?.GetStat(s, false)?.enabled != false).ToList();
            if (!plants.TryRandomElementByWeight(p => family?.GetPlant(p, false)?.weight ?? 1f, out ThingDef plant)) return null;
            if (!stats.TryRandomElementByWeight(s => family?.GetStat(s, false)?.weight ?? 1f, out string stat)) return null;
            return DefDatabase<VarietyTraitDef>.GetNamedSilentFail("HNS_SynergyGenerated_" + Sanitize(plant.defName) + "_" + stat);
        }

        private static string Sanitize(string value)
        {
            return new string((value ?? string.Empty).Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());
        }
    }
    public static class ColorTraitFactory
    {
        private static readonly string[] Families = { "ProduceColor", "LeafColor", "StemColor" };
        private static bool generated;
        public static bool IsColorFamily(string family) => !family.NullOrEmpty() && Families.Contains(family);
        public static void GenerateAll()
        {
            if (generated) return;
            generated = true;
            List<VarietyTraitDef> additions = new List<VarietyTraitDef>();
            for (int familyIndex = 0; familyIndex < Families.Length; familyIndex++)
            {
                string family = Families[familyIndex];
                VarietyTraitDef root = DefDatabase<VarietyTraitDef>.AllDefsListForReading.FirstOrDefault(t => t.configRoot && t.configFamily == family);
                if (root == null) continue;
                HashSet<string> names = new HashSet<string>();
                for (int hueStep = 0; hueStep < 24; hueStep++)
                foreach (float saturation in new[] { 0.55f, 0.75f, 0.95f })
                foreach (float value in new[] { 0.65f, 0.82f, 1f })
                {
                    Color color = Color.HSVToRGB(hueStep / 24f, saturation, value);
                    int red = Mathf.Clamp(Mathf.RoundToInt(color.r * 255f), 0, 255);
                    int green = Mathf.Clamp(Mathf.RoundToInt(color.g * 255f), 0, 255);
                    int blue = Mathf.Clamp(Mathf.RoundToInt(color.b * 255f), 0, 255);
                    string defName = "HNS_" + family + "_RGB_" + red + "_" + green + "_" + blue;
                    if (!names.Add(defName) || DefDatabase<VarietyTraitDef>.GetNamedSilentFail(defName) != null) continue;
                    string layer = (familyIndex == 0 ? "HNS_ColorLayerProduce" : familyIndex == 1 ? "HNS_ColorLayerLeaves" : "HNS_ColorLayerStem").Translate();
                    additions.Add(new VarietyTraitDef
                    {
                        defName = defName, label = root.label,
                        description = "HNS_ColorTraitDescription".Translate(layer),
                        commonality = 1f, configCategory = root.configCategory, configFamily = family,
                        configType = red + "," + green + "," + blue, hiddenFromConfig = true, generated = true,
                        tintRed = red / 255f, tintGreen = green / 255f, tintBlue = blue / 255f,
                        visualMaskIndex = familyIndex, inheritToProduce = true,
                        exclusionTags = root.exclusionTags?.ToList() ?? new List<string>(), modContentPack = root.modContentPack
                    });
                }
            }
            if (additions.Count > 0) DefDatabase<VarietyTraitDef>.Add(additions);
        }
        public static VarietyTraitDef Select(VarietyTraitDef root, ThingDef cropDef = null)
        {
            GenerateAll();
            if (cropDef != null) return SpeciesColorPaletteUtility.SelectTrait(root, cropDef);
            return DefDatabase<VarietyTraitDef>.AllDefsListForReading
                .Where(t => t.generated && t.configFamily == root.configFamily && t.visualMaskIndex >= 0).RandomElementWithFallback();
        }
        public static VarietyTraitDef TraitForColor(string family, Color color)
        {
            GenerateAll();
            return DefDatabase<VarietyTraitDef>.AllDefsListForReading
                .Where(t => t.generated && t.configFamily == family && t.visualMaskIndex >= 0)
                .OrderBy(t => PigmentColorUtility.PerceptualDistance(new Color(t.tintRed, t.tintGreen, t.tintBlue), color))
                .FirstOrDefault();
        }
        public static List<VarietyTraitDef> Cross(IEnumerable<VarietyTraitDef> first, IEnumerable<VarietyTraitDef> second, ThingDef cropDef)
        {
            List<VarietyTraitDef> result = new List<VarietyTraitDef>();
            foreach (string family in Families)
            {
                VarietyTraitDef left = first?.FirstOrDefault(t => t?.configFamily == family);
                VarietyTraitDef right = second?.FirstOrDefault(t => t?.configFamily == family);
                if (left == null || right == null) continue;
                Color mixed = PigmentColorUtility.Blend(new Color(left.tintRed, left.tintGreen, left.tintBlue),
                    new Color(right.tintRed, right.tintGreen, right.tintBlue));
                result.Add(TraitForColor(family, SpeciesColorPaletteUtility.Constrain(cropDef, mixed)));
            }
            return result.Where(trait => trait != null).ToList();
        }
        public static List<VisualSettingsRecord> ApplyIntrinsicColor(VarietyTraitDef trait, IEnumerable<VisualSettingsRecord> source)
        {
            List<VisualSettingsRecord> originals = source?.Where(v => v != null).Select(v => v.Clone()).ToList() ?? new List<VisualSettingsRecord>();
            VisualSettingsRecord baseline = originals.FirstOrDefault() ?? new VisualSettingsRecord();
            string[] names = { "Plant: Produce", "Plant: Leaves", "Plant: Stem", "Produce: Produce", "Produce: Leaves", "Produce: Container" };
            List<VisualSettingsRecord> result = new List<VisualSettingsRecord>();
            for (int i = 0; i < 6; i++)
            {
                VisualSettingsRecord visual = originals.FirstOrDefault(v => v.instanceName == names[i])?.Clone() ?? baseline.Clone();
                visual.instanceName = names[i];
                bool allowed = i == trait.visualMaskIndex || trait.visualMaskIndex == 0 && i == 3;
                visual.targetPlantProduce = allowed && i == 0; visual.targetPlantLeaves = allowed && i == 1; visual.targetPlantStem = allowed && i == 2;
                visual.targetProduceProduce = allowed && i == 3; visual.targetProduceLeaves = allowed && i == 4; visual.targetProduceContainer = allowed && i == 5;
                if (allowed) { visual.tintRed *= trait.tintRed; visual.tintGreen *= trait.tintGreen; visual.tintBlue *= trait.tintBlue; }
                result.Add(visual);
            }
            return result;
        }
    }

    public static class PercentageTraitFactory
    {
        public const string NutritiousFamily = "NutritiousPercent";
        private static bool generated;
        public static void GenerateAll()
        {
            if (generated) return;
            generated = true;
            VarietyTraitDef root = DefDatabase<VarietyTraitDef>.AllDefsListForReading.FirstOrDefault(t => t.configRoot && t.configFamily == NutritiousFamily);
            if (root == null) return;
            List<VarietyTraitDef> additions = new List<VarietyTraitDef>();
            for (int percent = 5; percent <= 100; percent += 5)
            {
                string defName = "HNS_Nutritious_" + percent;
                if (DefDatabase<VarietyTraitDef>.GetNamedSilentFail(defName) != null) continue;
                additions.Add(new VarietyTraitDef
                {
                    defName = defName, label = "Nutritious (+" + percent + "%)",
                    description = "Harvested produce provides " + percent + "% more nutrition.",
                    positive = true, traitTags = new List<string> { "Positive" },
                    balanceValue = percent / 5f,
                    commonality = percent == 5 ? 1f : percent == 10 ? 0.5f : percent == 15 ? 0.2f : 0f,
                    configCategory = root.configCategory, configFamily = NutritiousFamily, configType = percent.ToString(),
                    hiddenFromConfig = true, generated = true, percentageBonus = percent, nutritionFactor = 1f + percent / 100f,
                    inheritToProduce = true, requiredPlantTags = root.requiredPlantTags?.ToList() ?? new List<string>(),
                    exclusionTags = root.exclusionTags?.ToList() ?? new List<string>(), modContentPack = root.modContentPack
                });
            }
            if (additions.Count > 0) DefDatabase<VarietyTraitDef>.Add(additions);
        }
        public static VarietyTraitDef Select()
        {
            GenerateAll();
            List<VarietyTraitDef> options = DefDatabase<VarietyTraitDef>.AllDefsListForReading
                .Where(t => t.generated && t.configFamily == NutritiousFamily && t.percentageBonus >= 5 && t.percentageBonus <= 15).ToList();
            return options.TryRandomElementByWeight(t => t.commonality, out VarietyTraitDef selected) ? selected : null;
        }
        public static VarietyTraitDef Cross(IEnumerable<VarietyTraitDef> seedTraits, IEnumerable<VarietyTraitDef> pollenTraits)
        {
            GenerateAll();
            VarietyTraitDef seed = seedTraits?.FirstOrDefault(t => t?.configFamily == NutritiousFamily && t.percentageBonus > 0);
            VarietyTraitDef pollen = pollenTraits?.FirstOrDefault(t => t?.configFamily == NutritiousFamily && t.percentageBonus > 0);
            if (seed == null || pollen == null) return null;
            int lower = Mathf.Min(seed.percentageBonus, pollen.percentageBonus), higher = Mathf.Max(seed.percentageBonus, pollen.percentageBonus);
            int[] outcomes = { lower, higher, Mathf.Min(100, lower + 5), Mathf.Min(100, higher + 5) };
            return DefDatabase<VarietyTraitDef>.GetNamedSilentFail("HNS_Nutritious_" + outcomes[Rand.Range(0, outcomes.Length)]);
        }
    }
}
