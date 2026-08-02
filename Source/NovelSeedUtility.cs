using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace HorticultureNovelSeeds
{
    public struct ProduceColorStyle
    {
        public float tintRed;
        public float tintGreen;
        public float tintBlue;
        public float hueShift;
        public float saturation;
        public float brightness;
        public float contrast;
        public float opacity;
        public float dullness;

        public static ProduceColorStyle Identity => new ProduceColorStyle
        {
            tintRed = 1f,
            tintGreen = 1f,
            tintBlue = 1f,
            saturation = 1f,
            brightness = 1f,
            contrast = 1f,
            opacity = 1f
        };

        public bool IsDefault => Mathf.Approximately(tintRed, 1f) && Mathf.Approximately(tintGreen, 1f)
            && Mathf.Approximately(tintBlue, 1f) && Mathf.Approximately(hueShift, 0f)
            && Mathf.Approximately(saturation, 1f) && Mathf.Approximately(brightness, 1f)
            && Mathf.Approximately(contrast, 1f) && Mathf.Approximately(opacity, 1f)
            && Mathf.Approximately(dullness, 0f);

        public Color Apply(Color source)
        {
            return PlantVisualColorUtility.Apply(source, tintRed, tintGreen, tintBlue, hueShift,
                saturation, brightness, contrast, opacity, dullness);
        }

        public int ContentHash
        {
            get
            {
                unchecked
                {
                    int hash = 17;
                    hash = hash * 31 + Mathf.RoundToInt(tintRed * 1000f);
                    hash = hash * 31 + Mathf.RoundToInt(tintGreen * 1000f);
                    hash = hash * 31 + Mathf.RoundToInt(tintBlue * 1000f);
                    hash = hash * 31 + Mathf.RoundToInt(hueShift * 1000f);
                    hash = hash * 31 + Mathf.RoundToInt(saturation * 1000f);
                    hash = hash * 31 + Mathf.RoundToInt(brightness * 1000f);
                    hash = hash * 31 + Mathf.RoundToInt(contrast * 1000f);
                    hash = hash * 31 + Mathf.RoundToInt(opacity * 1000f);
                    return hash * 31 + Mathf.RoundToInt(dullness * 1000f);
                }
            }
        }
    }

    public static class NovelSeedUtility
    {
        private const string VanillaFlowersExpandedPackageId = "VanillaExpanded.VPEFlowers";
        private static readonly HashSet<string> SupersededByFlowersExpanded = new HashSet<string>
        {
            "Plant_Rose",
            "Plant_Daylily",
            "VCE_Hyacinth",
            "VCE_Lavender",
            "VCE_Lily",
            "VCE_Plumeria",
            "VCE_Tulip"
        };
        private static bool? vanillaFlowersExpandedActive;
        public const float SpontaneousMutationChance = 0.08f;
        public const float DefaultCrossPollinationChance = 0.10f;
        public const float DefaultWildMutationChance = 0.005f;
        private const float CrossPollinationRadius = 4.9f;

        public static bool IsGrowableCrop(ThingDef def)
        {
            return def?.plant != null
                && def.plant.Sowable
                && !def.plant.IsTree
                && !(VanillaFlowersExpandedActive && SupersededByFlowersExpanded.Contains(def.defName));
        }

        public static bool IsFlowerPlant(ThingDef def)
        {
            return IsGrowableCrop(def) && (def.plant.sowTags?.Contains("VCE_FlowerGarden") == true
                || def.plant.sowTags?.Contains("VPE_Blooming") == true
                || def.plant.purpose == PlantPurpose.Beauty);
        }

        public static bool VanillaFlowersExpandedActive => vanillaFlowersExpandedActive ??
            (vanillaFlowersExpandedActive = ModsConfig.IsActive(VanillaFlowersExpandedPackageId)
                || ModsConfig.IsActive(VanillaFlowersExpandedPackageId.ToLowerInvariant())).Value;
        public static void AssignMutationOnSow(Plant plant, Pawn sower = null)
        {
            if (plant == null || !IsGrowableCrop(plant.def))
            {
                return;
            }

            CompPlantVariety comp = plant.TryGetComp<CompPlantVariety>();
            if (comp == null || comp.HasAnyTraits)
            {
                return;
            }

            IPlantToGrowSettable grower = GridsUtility.GetPlantToGrowSettable(plant.Position, plant.Map);
            VarietyRecord selected = GameComponent_NovelSeeds.Instance?.VarietyForSowing(grower, plant.Position);
            List<VarietyTraitDef> inheritedTraits = null;
            if (selected != null && selected.cropDef == plant.def)
            {
                comp.SetVariety(selected);
                inheritedTraits = selected.traits;
            }

            if (selected != null && selected.cropDef == plant.def && TryAssignCrossPollination(plant, comp, selected))
            {
                ExpandedTraitUtility.ClearAdjacentPlantsForHumongous(plant);
                return;
            }

            float mutationChance = HorticultureNovelSeedsMod.Settings?.MutationChanceFor(plant.def) ?? SpontaneousMutationChance;
            if (Rand.Chance(mutationChance))
            {
                List<VarietyTraitDef> newTraits = RandomTraitSet(plant.def, inheritedTraits);
                if (newTraits.Count > 0)
                {
                    if (selected != null && selected.cropDef == plant.def)
                    {
                        comp.AddPendingTraits(newTraits);
                    }
                    else
                    {
                        comp.SetPendingTraits(newTraits);
                    }
                }
            }

            ExpandedTraitUtility.ClearAdjacentPlantsForHumongous(plant);
        }

        public static void AssignWildMutation(Plant plant)
        {
            if (plant == null || plant.sown || !IsGrowableCrop(plant.def)) return;
            CompPlantVariety comp = plant.TryGetComp<CompPlantVariety>();
            if (comp == null || comp.HasAnyTraits) return;

            NovelSeedsSettings settings = HorticultureNovelSeedsMod.Settings;
            float chance = settings?.wildMutationChance ?? DefaultWildMutationChance;
            if (!Rand.Chance(Mathf.Clamp01(chance))) return;
            List<VarietyTraitDef> traits = RandomWildTraitSet(plant.def, settings);
            if (traits.Count > 0) comp.SetPendingTraits(traits);
        }

        private static List<VarietyTraitDef> RandomWildTraitSet(ThingDef cropDef, NovelSeedsSettings settings)
        {
            List<VarietyTraitDef> defs = TraitConfigUtility.TopLevelTraits()
                .Where(trait => settings == null
                    ? trait.commonality > 0f
                    : settings.IsWildTraitAllowed(trait) && settings.WildTraitWeight(trait) > 0f)
                .ToList();
            List<VarietyTraitDef> result = new List<VarietyTraitDef>();
            HashSet<string> blockedTags = new HashSet<string>();
            int targetCount = NewTraitCount(settings);
            int maxCount = settings?.MaxTraitsPerEvent ?? 3;
            bool exceptional = IsExceptionalBalanceEvent(settings);
            while (result.Count < targetCount || ShouldAddCompensatingTrait(result, result.Count, maxCount, settings, exceptional))
            {
                List<VarietyTraitDef> candidates = defs
                    .Where(trait => !result.Any(existing => SameConfigGroup(existing, trait)) && !SharesBlockedTag(trait, blockedTags))
                    .ToList();
                VarietyTraitDef trait = SelectBalancedResolvedTrait(candidates, cropDef, settings, result,
                    root => settings?.WildTraitWeight(root) ?? Mathf.Max(0f, root.commonality), exceptional);
                if (trait == null)
                {
                    break;
                }
                result.Add(trait);
                AddBlockedTags(trait, blockedTags);
            }
            return result;
        }

        private static bool TryAssignCrossPollination(Plant plant, CompPlantVariety comp, VarietyRecord selected)
        {
            if (plant?.Map == null || comp == null || selected?.traits == null) return false;
            List<VarietyRecord> donors = GenRadial.RadialCellsAround(plant.Position, CrossPollinationRadius, true)
                .Where(cell => cell.InBounds(plant.Map)).Select(cell => cell.GetPlant(plant.Map))
                .Where(other => other != null && other != plant && other.sown && other.def == plant.def)
                .Select(other => other.TryGetComp<CompPlantVariety>()?.Variety)
                .Where(variety => variety != null && variety.id != selected.id).GroupBy(variety => variety.id).Select(group => group.First()).ToList();
            if (donors.Count == 0) return false;
            float chance = HorticultureNovelSeedsMod.Settings?.CrossPollinationChanceFor(plant.def) ?? DefaultCrossPollinationChance;
            if (!Rand.Chance(Mathf.Clamp01(chance))) return false;
            int maxDonorTraits = HorticultureNovelSeedsMod.Settings?.MaxCrossPollinationTraits ?? 3;
            bool exceptional = IsExceptionalBalanceEvent(HorticultureNovelSeedsMod.Settings);
            foreach (VarietyRecord donor in donors.InRandomOrder())
            {
                List<VarietyTraitDef> candidates = donor.traits.Where(trait => trait != null && !selected.traits.Any(existing => SameConfigGroup(existing, trait))).Distinct().ToList();
                for (int attempt = 0; attempt < 8; attempt++)
                {
                    List<VarietyTraitDef> replacements = new List<VarietyTraitDef>();
                    VarietyTraitDef nutritious = PercentageTraitFactory.Cross(selected.traits, donor.traits);
                    if (nutritious != null) replacements.Add(nutritious);
                    replacements.AddRange(ColorTraitFactory.Cross(selected.traits, donor.traits, plant.def));
                    int remaining = Mathf.Max(0, Mathf.Clamp(maxDonorTraits, 1, 10) - replacements.Count);
                    List<VarietyTraitDef> additions = replacements.ToList();
                    List<VarietyTraitDef> inheritedForBalance = selected.traits
                        .Where(existing => !replacements.Any(replacement => SameConfigGroup(existing, replacement)))
                        .Concat(replacements).ToList();
                    if (remaining > 0 && candidates.Count > 0)
                        additions.AddRange(CrossPollinatedTraitSubset(inheritedForBalance, candidates, remaining, HorticultureNovelSeedsMod.Settings, exceptional));
                    if (additions.Count == 0) continue;
                    List<VarietyTraitDef> combined = selected.traits.Where(existing => !replacements.Any(replacement => SameConfigGroup(existing, replacement)))
                        .Concat(additions).Where(trait => trait != null).Distinct().ToList();
                    if (GameComponent_NovelSeeds.Instance?.FindMatchingVariety(plant.def, combined) != null) continue;
                    comp.SetCrossPollinatedTraits(additions, donor);
                    return comp.CrossPollinated;
                }
            }
            return false;
        }
        private static List<VarietyTraitDef> CrossPollinatedTraitSubset(IEnumerable<VarietyTraitDef> inherited, List<VarietyTraitDef> candidates, int maxDonorTraits, NovelSeedsSettings settings, bool exceptional)
        {
            List<VarietyTraitDef> result = new List<VarietyTraitDef>();
            List<VarietyTraitDef> available = candidates.Where(trait => trait != null).Distinct().ToList();
            List<VarietyTraitDef> inheritedList = inherited?.Where(trait => trait != null).ToList() ?? new List<VarietyTraitDef>();
            HashSet<string> blockedTags = new HashSet<string>();
            foreach (VarietyTraitDef trait in inheritedList) AddBlockedTags(trait, blockedTags);

            int limit = Mathf.Clamp(maxDonorTraits, 1, 10);
            while (result.Count < limit && available.Count > 0)
            {
                bool needsCompensation = settings?.enableTraitBalancing == true && !exceptional
                    && Mathf.Abs(TraitBalanceScore(inheritedList.Concat(result), settings)) > settings.allowedTraitImbalance;
                if (result.Count > 0 && !needsCompensation && !Rand.Chance(0.65f)) break;
                List<VarietyTraitDef> valid = available.Where(trait => !SharesBlockedTag(trait, blockedTags)).ToList();
                if (valid.Count == 0) break;
                VarietyTraitDef trait = SelectBalancedDirectTrait(valid, inheritedList.Concat(result), settings, exceptional);
                if (trait == null) break;
                result.Add(trait);
                available.Remove(trait);
                AddBlockedTags(trait, blockedTags);
            }
            return result;
        }

        public static List<VarietyTraitDef> RandomTraitSet(IEnumerable<VarietyTraitDef> existingTraits = null)
        {
            return RandomTraitSet(null, existingTraits);
        }

        public static List<VarietyTraitDef> RandomTraitSet(ThingDef cropDef, IEnumerable<VarietyTraitDef> existingTraits = null)
        {
            NovelSeedsSettings settings = HorticultureNovelSeedsMod.Settings;
            List<VarietyTraitDef> defs = TraitConfigUtility.TopLevelTraits()
                .Where(t => TraitSelectionWeight(settings, cropDef, t) > 0f)
                .ToList();
            List<VarietyTraitDef> existing = existingTraits?.Where(t => t != null).Distinct().ToList() ?? new List<VarietyTraitDef>();
            List<VarietyTraitDef> result = new List<VarietyTraitDef>();
            HashSet<string> blockedTags = new HashSet<string>();
            foreach (VarietyTraitDef trait in existing)
            {
                AddBlockedTags(trait, blockedTags);
            }

            int targetCount = NewTraitCount(settings);
            int maxCount = settings?.MaxTraitsPerEvent ?? 3;
            bool exceptional = IsExceptionalBalanceEvent(settings);
            while (result.Count < targetCount || ShouldAddCompensatingTrait(existing.Concat(result), result.Count, maxCount, settings, exceptional))
            {
                List<VarietyTraitDef> candidates = defs.Where(t => !existing.Any(e => SameConfigGroup(e, t)) && !result.Any(e => SameConfigGroup(e, t)) && !SharesBlockedTag(t, blockedTags)).ToList();
                VarietyTraitDef trait = SelectBalancedResolvedTrait(candidates, cropDef, settings, existing.Concat(result),
                    root => TraitSelectionWeight(settings, cropDef, root), exceptional);
                if (trait == null)
                {
                    break;
                }
                result.Add(trait);
                AddBlockedTags(trait, blockedTags);
            }

            return result;
        }

        private sealed class ResolvedTraitOption
        {
            public VarietyTraitDef trait;
            public float weight;
        }

        private static VarietyTraitDef SelectBalancedResolvedTrait(IEnumerable<VarietyTraitDef> roots, ThingDef cropDef,
            NovelSeedsSettings settings, IEnumerable<VarietyTraitDef> currentTraits, Func<VarietyTraitDef, float> baseWeight, bool exceptional)
        {
            List<VarietyTraitDef> current = currentTraits?.Where(trait => trait != null).ToList() ?? new List<VarietyTraitDef>();
            List<ResolvedTraitOption> options = new List<ResolvedTraitOption>();
            foreach (VarietyTraitDef root in roots)
            {
                float weight = Mathf.Max(0f, baseWeight(root));
                if (weight <= 0f) continue;
                VarietyTraitDef resolved = TraitFamilyResolver.Resolve(root, cropDef, settings);
                if (resolved == null) continue;
                options.Add(new ResolvedTraitOption
                {
                    trait = resolved,
                    weight = BalanceAdjustedWeight(weight, current, resolved, settings, exceptional)
                });
            }
            return options.TryRandomElementByWeight(option => option.weight, out ResolvedTraitOption selected) ? selected.trait : null;
        }

        private static VarietyTraitDef SelectBalancedDirectTrait(IEnumerable<VarietyTraitDef> candidates,
            IEnumerable<VarietyTraitDef> currentTraits, NovelSeedsSettings settings, bool exceptional)
        {
            List<VarietyTraitDef> current = currentTraits?.Where(trait => trait != null).ToList() ?? new List<VarietyTraitDef>();
            return candidates.TryRandomElementByWeight(
                trait => BalanceAdjustedWeight(Mathf.Max(0.01f, trait.commonality), current, trait, settings, exceptional),
                out VarietyTraitDef selected) ? selected : null;
        }

        private static float BalanceAdjustedWeight(float baseWeight, IEnumerable<VarietyTraitDef> currentTraits,
            VarietyTraitDef candidate, NovelSeedsSettings settings, bool exceptional)
        {
            if (baseWeight <= 0f || settings?.enableTraitBalancing != true || exceptional) return baseWeight;
            float currentScore = TraitBalanceScore(currentTraits, settings);
            float projectedScore = currentScore + TraitBalanceValue(candidate, settings);
            float allowed = settings.allowedTraitImbalance;
            float currentExcess = Mathf.Max(0f, Mathf.Abs(currentScore) - allowed);
            float projectedExcess = Mathf.Max(0f, Mathf.Abs(projectedScore) - allowed);
            float change = projectedExcess - currentExcess;
            float preference = change <= 0f ? 1f + (-change * 2f) : 1f / (1f + change * 2f);
            return baseWeight * Mathf.Lerp(1f, preference, settings.traitBalanceStrength);
        }

        private static bool ShouldAddCompensatingTrait(IEnumerable<VarietyTraitDef> traits, int newTraitCount, int maxCount,
            NovelSeedsSettings settings, bool exceptional)
        {
            if (settings?.enableTraitBalancing != true || exceptional) return false;
            return newTraitCount < maxCount && Mathf.Abs(TraitBalanceScore(traits, settings)) > settings.allowedTraitImbalance;
        }

        private static bool IsExceptionalBalanceEvent(NovelSeedsSettings settings)
        {
            return settings?.enableTraitBalancing == true && Rand.Chance(Mathf.Clamp01(settings.exceptionalVarietyChance));
        }

        public static float TraitBalanceValue(VarietyTraitDef trait, NovelSeedsSettings settings = null)
        {
            if (trait == null) return 0f;
            if (!Mathf.Approximately(trait.balanceValue, 0f)) return trait.balanceValue;
            switch (trait.defName)
            {
                case "HNS_Humongous": return 3f;
                case "HNS_DiseaseResistant":
                case "HNS_HighYield":
                case "HNS_Perennial":
                case "HNS_SelfSeeding": return 2f;
                case "HNS_DiseaseVulnerable":
                case "HNS_LowYield": return -2f;
                case "HNS_VeryDelicate": return -3f;
            }
            if (trait.configFamily == PercentageTraitFactory.NutritiousFamily && trait.percentageBonus > 0)
                return trait.percentageBonus / 5f;
            settings = settings ?? HorticultureNovelSeedsMod.Settings;
            bool positive = settings?.TraitHasTag(trait, "Positive") ?? trait.traitTags?.Any(tag => tag.Equals("Positive", StringComparison.OrdinalIgnoreCase)) == true;
            bool negative = settings?.TraitHasTag(trait, "Negative") ?? trait.traitTags?.Any(tag => tag.Equals("Negative", StringComparison.OrdinalIgnoreCase)) == true;
            if (positive == negative) return 0f;
            return positive ? 1f : -1f;
        }

        public static float TraitBalanceScore(IEnumerable<VarietyTraitDef> traits, NovelSeedsSettings settings = null)
        {
            return traits?.Where(trait => trait != null).Sum(trait => TraitBalanceValue(trait, settings)) ?? 0f;
        }

        public static string TraitBalanceSummary(IEnumerable<VarietyTraitDef> traits, NovelSeedsSettings settings = null)
        {
            float score = TraitBalanceScore(traits, settings);
            string category = Mathf.Abs(score) < 0.01f ? "Balanced"
                : score >= 3f ? "Strongly Beneficial"
                : score > 0f ? "Beneficial"
                : score <= -3f ? "Strongly Detrimental"
                : "Detrimental";
            return "Trait Balance: " + score.ToString("+0;-0;0") + " (" + category + ")";
        }

        internal static bool SameConfigGroup(VarietyTraitDef left, VarietyTraitDef right)
        {
            if (left == null || right == null) return false;
            if (!left.configFamily.NullOrEmpty() && left.configFamily == right.configFamily) return true;
            return left == right;
        }

        private static int NewTraitCount(NovelSeedsSettings settings)
        {
            int max = settings?.MaxTraitsPerEvent ?? 3;
            if (max <= 1)
            {
                return 1;
            }

            int count = 1;
            while (count < max && Rand.Chance(0.30f))
            {
                count++;
            }
            return count;
        }

        private static float TraitSelectionWeight(NovelSeedsSettings settings, ThingDef cropDef, VarietyTraitDef trait)
        {
            if (trait == null)
            {
                return 0f;
            }
            if (settings != null && cropDef != null)
            {
                return settings.IsTraitAllowed(cropDef, trait) ? Mathf.Max(0f, settings.TraitWeight(cropDef, trait)) : 0f;
            }
            return trait.commonality > 0f ? trait.commonality : 0f;
        }

        public static string TraitKey(IEnumerable<VarietyTraitDef> traits)
        {
            if (traits == null)
            {
                return string.Empty;
            }
            return string.Join("|", traits.Where(t => t != null).Select(t => t.defName).OrderBy(x => x).ToArray());
        }

        public static string TraitSummary(IEnumerable<VarietyTraitDef> traits)
        {
            if (traits == null)
            {
                return string.Empty;
            }
            return TraitColorUI.Summary(traits);
        }

        public static bool DefaultTraitAppliesToProduce(VarietyTraitDef trait)
        {
            return trait != null;
        }
        public static bool IsInheritableProduceTrait(VarietyTraitDef trait)
        {
            return trait != null;
        }
        public static float ProduceNutritionFactor(IEnumerable<VarietyTraitDef> traits)
        {
            float factor = 1f;
            if (traits == null) return factor;
            foreach (VarietyTraitDef trait in traits.Where(trait => trait != null))
            {
                float value = trait.nutritionFactor <= 0f ? 1f : trait.nutritionFactor;
                factor *= value;
            }
            return Mathf.Clamp(factor, 0.1f, 5f);
        }

        public static string InheritedProduceQualityLine(VarietyTraitDef trait, ThingDef productDef = null)
        {
            if (trait == null) return string.Empty;
            List<string> lines = new List<string>();
            float factor = trait.nutritionFactor <= 0f ? 1f : trait.nutritionFactor;
            if (!Mathf.Approximately(factor, 1f))
                lines.Add("HNS_ProduceQualityNutrition".Translate(factor.ToStringPercent()).ToString());
            float medicalFactor = trait.medicalPotencyFactor <= 0f ? 1f : trait.medicalPotencyFactor;
            if (!Mathf.Approximately(medicalFactor, 1f))
                lines.Add("HNS_ProduceQualityMedicalPotency".Translate(medicalFactor.ToStringPercent()).ToString());
            if (trait.compoundThought != null)
                lines.Add("HNS_ProduceQualityCompound".Translate(trait.compoundThought.LabelCap).ToString());
            if (trait.compoundHediff != null)
                lines.Add("HNS_ProduceQualityCompound".Translate(trait.compoundHediff.LabelCap).ToString());
            bool material = productDef?.IsStuff == true;
            bool apparel = productDef?.thingClass != null && typeof(Apparel).IsAssignableFrom(productDef.thingClass);
            float armorHeat = trait.produceArmorHeatFactor <= 0f ? 1f : trait.produceArmorHeatFactor;
            if (!Mathf.Approximately(armorHeat, 1f) && (material || apparel))
                lines.Add("Armor - Heat: " + armorHeat.ToStringPercent() + " of base");
            float coldInsulation = trait.produceColdInsulationFactor <= 0f ? 1f : trait.produceColdInsulationFactor;
            if (!Mathf.Approximately(coldInsulation, 1f) && (material || apparel))
                lines.Add("Cold insulation: " + coldInsulation.ToStringPercent() + " of base");
            float hitPoints = trait.produceMaxHitPointsFactor <= 0f ? 1f : trait.produceMaxHitPointsFactor;
            if (!Mathf.Approximately(hitPoints, 1f)) lines.Add("Maximum hit points: " + hitPoints.ToStringPercent() + " of base");
            if (!Mathf.Approximately(trait.produceBeautyOffset, 0f)) lines.Add("Beauty: " + trait.produceBeautyOffset.ToStringWithSign());
            if (lines.Count == 0) lines.Add("No Effect");
            return string.Join("\n", lines);
        }

        public static float YieldFactor(CompPlantVariety comp)
        {
            return YieldFactor(comp?.ActiveTraits);
        }

        public static float YieldFactor(IEnumerable<VarietyTraitDef> traits)
        {
            float factor = 1f;
            if (traits == null)
            {
                return factor;
            }
            foreach (VarietyTraitDef trait in traits.Where(t => t != null))
            {
                factor *= trait.yieldFactor <= 0f ? 1f : trait.yieldFactor;
            }
            return factor;
        }

        private static VisualSettingsRecord VisualOverride(ThingDef cropDef, VarietyTraitDef trait)
        {
            return HorticultureNovelSeedsMod.Settings?.VisualOverrideFor(cropDef, trait);
        }

        private static IReadOnlyList<VisualSettingsRecord> VisualInstances(ThingDef cropDef, VarietyTraitDef trait)
        {
            return HorticultureNovelSeedsMod.Settings?.VisualInstancesFor(cropDef, trait)
                ?? new List<VisualSettingsRecord> { new VisualSettingsRecord(trait) };
        }

        public static PlantVisualParameters ResolveVisualParameters(CompPlantVariety comp)
        {
            PlantVisualParameters result = PlantVisualParameters.Default;
            if (comp?.ActiveTraits == null) return result;
            ThingDef cropDef = comp.parent?.def;
            Color radianceColorTotal = Color.clear, gloomColorTotal = Color.clear;
            float radianceColorWeight = 0f, gloomColorWeight = 0f, strongestOverlay = -1f;
            foreach (VarietyTraitDef trait in comp.ActiveTraits.Where(item => item != null))
            {
                if (trait.produceOnlyVisual) continue;
                VisualSettingsRecord visual = VisualOverride(cropDef, trait) ?? new VisualSettingsRecord(trait);
                ApplyShape(ref result, visual);
                if (trait.visualMaskIndex < 0) ApplyColor(ref result, visual);

                if (visual.radiance > 0f)
                {
                    result.radiance += visual.radiance;
                    result.radianceScale *= visual.radianceScale;
                    radianceColorTotal += new Color(visual.radianceRed, visual.radianceGreen, visual.radianceBlue) * visual.radiance;
                    radianceColorWeight += visual.radiance;
                }
                if (visual.gloom > 0f)
                {
                    result.gloom += visual.gloom;
                    result.gloomScale *= visual.gloomScale;
                    gloomColorTotal += new Color(visual.gloomRed, visual.gloomGreen, visual.gloomBlue) * visual.gloom;
                    gloomColorWeight += visual.gloom;
                }
                if (visual.overlayPattern > 0 && visual.overlayIntensity >= strongestOverlay)
                {
                    strongestOverlay = visual.overlayIntensity;
                    result.overlayPattern = visual.overlayPattern;
                    result.overlayIntensity = visual.overlayIntensity;
                    result.overlayScale = visual.overlayScale;
                    result.overlayRed = visual.overlayRed;
                    result.overlayGreen = visual.overlayGreen;
                    result.overlayBlue = visual.overlayBlue;
                }
            }
            ClampVisual(ref result);
            if (radianceColorWeight > 0f) { Color color = radianceColorTotal / radianceColorWeight; result.radianceRed = color.r; result.radianceGreen = color.g; result.radianceBlue = color.b; }
            if (gloomColorWeight > 0f) { Color color = gloomColorTotal / gloomColorWeight; result.gloomRed = color.r; result.gloomGreen = color.g; result.gloomBlue = color.b; }
            return result;
        }
        public static PlantVisualParameters ResolveProduceVisualParameters(ThingDef cropDef, IEnumerable<VarietyTraitDef> traits)
        {
            PlantVisualParameters result = PlantVisualParameters.Default;
            Color radianceColorTotal = Color.clear, gloomColorTotal = Color.clear;
            float radianceColorWeight = 0f, gloomColorWeight = 0f, strongestOverlay = -1f;
            foreach (VarietyTraitDef trait in traits?.Where(item => item != null) ?? Enumerable.Empty<VarietyTraitDef>())
            {
                VisualSettingsRecord source = VisualOverride(cropDef, trait) ?? new VisualSettingsRecord(trait);
                VisualSettingsRecord visual = source.CreateProduceVisualEditor();
                ApplyShape(ref result, visual);
                ApplyColor(ref result, visual);
                if (visual.radiance > 0f)
                {
                    result.radiance += visual.radiance;
                    result.radianceScale *= visual.radianceScale;
                    radianceColorTotal += new Color(visual.radianceRed, visual.radianceGreen, visual.radianceBlue) * visual.radiance;
                    radianceColorWeight += visual.radiance;
                }
                if (visual.gloom > 0f)
                {
                    result.gloom += visual.gloom;
                    result.gloomScale *= visual.gloomScale;
                    gloomColorTotal += new Color(visual.gloomRed, visual.gloomGreen, visual.gloomBlue) * visual.gloom;
                    gloomColorWeight += visual.gloom;
                }
                if (visual.overlayPattern > 0 && visual.overlayIntensity >= strongestOverlay)
                {
                    strongestOverlay = visual.overlayIntensity;
                    result.overlayPattern = visual.overlayPattern;
                    result.overlayIntensity = visual.overlayIntensity;
                    result.overlayScale = visual.overlayScale;
                    result.overlayRed = visual.overlayRed;
                    result.overlayGreen = visual.overlayGreen;
                    result.overlayBlue = visual.overlayBlue;
                }
            }
            ClampVisual(ref result);
            if (radianceColorWeight > 0f)
            {
                Color color = radianceColorTotal / radianceColorWeight;
                result.radianceRed = color.r; result.radianceGreen = color.g; result.radianceBlue = color.b;
            }
            if (gloomColorWeight > 0f)
            {
                Color color = gloomColorTotal / gloomColorWeight;
                result.gloomRed = color.r; result.gloomGreen = color.g; result.gloomBlue = color.b;
            }
            return result;
        }

        public static PlantVisualParameters ResolvePlantTextureParameters(CompPlantVariety comp, int maskIndex)
        {
            PlantVisualParameters result = PlantVisualParameters.Default;
            if (comp?.ActiveTraits == null) return result;
            ThingDef cropDef = comp.parent?.def;
            foreach (VarietyTraitDef trait in comp.ActiveTraits.Where(item => item != null))
            {
                VisualSettingsRecord wholeVisual = VisualOverride(cropDef, trait) ?? new VisualSettingsRecord(trait);
                ApplyShape(ref result, wholeVisual);
                foreach (VisualSettingsRecord maskVisual in VisualInstances(cropDef, trait).Where(item => item != null && item.TargetsPlantMask(maskIndex)))
                    ApplyColor(ref result, maskVisual);
            }
            ClampVisual(ref result);
            return result;
        }

        public static bool HasPlantMaskVisual(CompPlantVariety comp)
        {
            if (comp?.ActiveTraits == null) return false;
            ThingDef cropDef = comp.parent?.def;
            foreach (VarietyTraitDef trait in comp.ActiveTraits.Where(item => item != null))
                foreach (VisualSettingsRecord visual in VisualInstances(cropDef, trait))
                    if (visual?.HasAnyPlantTarget == true && HasColorChange(visual)) return true;
            return false;
        }

        private static bool HasColorChange(VisualSettingsRecord visual)
        {
            return !Mathf.Approximately(visual.tintRed, 1f) || !Mathf.Approximately(visual.tintGreen, 1f)
                || !Mathf.Approximately(visual.tintBlue, 1f) || !Mathf.Approximately(visual.hueShift, 0f)
                || !Mathf.Approximately(visual.saturation, 1f) || !Mathf.Approximately(visual.brightness, 1f)
                || !Mathf.Approximately(visual.contrast, 1f) || !Mathf.Approximately(visual.opacity, 1f)
                || !Mathf.Approximately(visual.dullness, 0f);
        }

        public static int PlantTextureVisualHash(CompPlantVariety comp)
        {
            unchecked
            {
                int hash = 17;
                ThingDef cropDef = comp?.parent?.def;
                foreach (VarietyTraitDef trait in comp?.ActiveTraits?.Where(item => item != null) ?? Enumerable.Empty<VarietyTraitDef>())
                {
                    hash = hash * 31 + trait.defName.GetHashCode();
                    foreach (VisualSettingsRecord visual in VisualInstances(cropDef, trait))
                    {
                        hash = hash * 31 + (visual.targetPlantProduce ? 1 : 0);
                        hash = hash * 31 + (visual.targetPlantLeaves ? 1 : 0);
                        hash = hash * 31 + (visual.targetPlantStem ? 1 : 0);
                        hash = hash * 31 + Mathf.RoundToInt(visual.tintRed * 1000f);
                        hash = hash * 31 + Mathf.RoundToInt(visual.tintGreen * 1000f);
                        hash = hash * 31 + Mathf.RoundToInt(visual.tintBlue * 1000f);
                        hash = hash * 31 + Mathf.RoundToInt(visual.hueShift * 1000f);
                        hash = hash * 31 + Mathf.RoundToInt(visual.saturation * 1000f);
                        hash = hash * 31 + Mathf.RoundToInt(visual.brightness * 1000f);
                        hash = hash * 31 + Mathf.RoundToInt(visual.contrast * 1000f);
                        hash = hash * 31 + Mathf.RoundToInt(visual.opacity * 1000f);
                        hash = hash * 31 + Mathf.RoundToInt(visual.dullness * 1000f);
                    }
                }
                return hash;
            }
        }

        public static Color ResolveProduceColor(CompPlantVariety comp)
        {
            return ResolveProduceColor(comp, -1);
        }

        public static List<Color> ResolveProduceMaskColors(CompPlantVariety comp)
        {
            return new List<Color> { ResolveProduceColor(comp, 0), ResolveProduceColor(comp, 1), ResolveProduceColor(comp, 2) };
        }

        private static Color ResolveProduceColor(CompPlantVariety comp, int maskIndex)
        {
            if (comp?.ActiveTraits == null) return Color.white;
            return ResolveProduceColorStyle(comp.parent?.def, comp.ActiveTraits, maskIndex).Apply(Color.white);
        }

        public static List<ProduceColorStyle> ResolveProduceColorStyles(ThingDef cropDef, IEnumerable<VarietyTraitDef> traits)
        {
            return new List<ProduceColorStyle>
            {
                ResolveProduceColorStyle(cropDef, traits, 0),
                ResolveProduceColorStyle(cropDef, traits, 1),
                ResolveProduceColorStyle(cropDef, traits, 2)
            };
        }

        public static bool HasProduceColorVisual(ThingDef cropDef, IEnumerable<VarietyTraitDef> traits)
        {
            return ResolveProduceColorStyles(cropDef, traits).Any(style => !style.IsDefault);
        }

        public static bool HasProduceVisual(ThingDef cropDef, IEnumerable<VarietyTraitDef> traits)
        {
            return HasProduceColorVisual(cropDef, traits) || !ResolveProduceVisualParameters(cropDef, traits).IsDefault;
        }

        private static ProduceColorStyle ResolveProduceColorStyle(ThingDef cropDef, IEnumerable<VarietyTraitDef> traits, int maskIndex)
        {
            ProduceColorStyle result = ProduceColorStyle.Identity;
            foreach (VarietyTraitDef trait in traits?.Where(item => item != null) ?? Enumerable.Empty<VarietyTraitDef>())
            {
                foreach (VisualSettingsRecord visual in VisualInstances(cropDef, trait))
                {
                    if (visual == null || !visual.HasAnyProduceTarget || maskIndex >= 0 && !visual.TargetsProduceMask(maskIndex)) continue;
                    bool maskVisual = visual.instanceName?.StartsWith("Produce:") == true;
                    result.tintRed *= maskVisual ? visual.tintRed : visual.produceTintRed;
                    result.tintGreen *= maskVisual ? visual.tintGreen : visual.produceTintGreen;
                    result.tintBlue *= maskVisual ? visual.tintBlue : visual.produceTintBlue;
                    result.hueShift += maskVisual ? visual.hueShift : visual.produceHueShift;
                    result.saturation *= maskVisual ? visual.saturation : visual.produceSaturation;
                    result.brightness *= maskVisual ? visual.brightness : visual.produceBrightness;
                    result.contrast *= maskVisual ? visual.contrast : visual.produceContrast;
                    result.opacity *= maskVisual ? visual.opacity : visual.produceOpacity;
                    result.dullness += maskVisual ? visual.dullness : visual.produceDullness;
                }
            }
            result.hueShift = Mathf.Repeat(result.hueShift + 0.5f, 1f) - 0.5f;
            result.saturation = Mathf.Clamp(result.saturation, 0f, 3f);
            result.brightness = Mathf.Clamp(result.brightness, 0.1f, 3f);
            result.contrast = Mathf.Clamp(result.contrast, 0.1f, 3f);
            result.opacity = Mathf.Clamp(result.opacity, 0.1f, 1f);
            result.dullness = Mathf.Clamp01(result.dullness);
            return result;
        }

        private static void ApplyShapeAndColor(ref PlantVisualParameters result, VisualSettingsRecord visual)
        {
            ApplyShape(ref result, visual);
            ApplyColor(ref result, visual);
        }

        private static void ApplyShape(ref PlantVisualParameters result, VisualSettingsRecord visual)
        {
            result.scale *= visual.scale; result.width *= visual.width; result.height *= visual.height;
            result.density *= visual.density; result.spread *= visual.spread; result.rotation += visual.rotation;
            result.rotationVariation += visual.rotationVariation; result.scaleVariation += visual.scaleVariation;
            result.offsetX += visual.offsetX; result.offsetZ += visual.offsetZ; result.shadowScale *= visual.shadowScale;
        }

        private static float ShapeStrength(VisualSettingsRecord visual)
        {
            return Mathf.Abs(Mathf.Log(Mathf.Max(0.01f, visual.scale)))
                + Mathf.Abs(Mathf.Log(Mathf.Max(0.01f, visual.width)))
                + Mathf.Abs(Mathf.Log(Mathf.Max(0.01f, visual.height)))
                + Mathf.Abs(Mathf.Log(Mathf.Max(0.01f, visual.density)))
                + Mathf.Abs(Mathf.Log(Mathf.Max(0.01f, visual.spread)))
                + Mathf.Abs(visual.rotation) / 180f + visual.rotationVariation / 180f + visual.scaleVariation
                + Mathf.Abs(visual.offsetX) + Mathf.Abs(visual.offsetZ) + Mathf.Abs(visual.shadowScale - 1f);
        }

        private static void ApplyColor(ref PlantVisualParameters result, VisualSettingsRecord visual)
        {
            result.tintRed *= visual.tintRed; result.tintGreen *= visual.tintGreen; result.tintBlue *= visual.tintBlue;
            result.hueShift += visual.hueShift; result.saturation *= visual.saturation; result.brightness *= visual.brightness;
            result.contrast *= visual.contrast; result.opacity *= visual.opacity; result.dullness += visual.dullness;
        }

        private static void ClampVisual(ref PlantVisualParameters result)
        {
            result.scale = Mathf.Clamp(result.scale, 0.1f, 6f); result.width = Mathf.Clamp(result.width, 0.1f, 4f); result.height = Mathf.Clamp(result.height, 0.1f, 4f);
            result.density = Mathf.Clamp(result.density, 0.1f, 4f); result.spread = Mathf.Clamp(result.spread, 0.1f, 3f);
            result.rotation = Mathf.Clamp(result.rotation, -180f, 180f); result.rotationVariation = Mathf.Clamp(result.rotationVariation, 0f, 180f);
            result.scaleVariation = Mathf.Clamp(result.scaleVariation, 0f, 0.75f); result.offsetX = Mathf.Clamp(result.offsetX, -0.5f, 0.5f); result.offsetZ = Mathf.Clamp(result.offsetZ, -0.5f, 0.5f);
            result.shadowScale = Mathf.Clamp(result.shadowScale, 0f, 3f); result.hueShift = Mathf.Repeat(result.hueShift + 0.5f, 1f) - 0.5f;
            result.saturation = Mathf.Clamp(result.saturation, 0f, 3f); result.brightness = Mathf.Clamp(result.brightness, 0.1f, 3f);
            result.contrast = Mathf.Clamp(result.contrast, 0.1f, 3f); result.opacity = Mathf.Clamp(result.opacity, 0.1f, 1f);
            result.dullness = Mathf.Clamp01(result.dullness); result.radiance = Mathf.Clamp01(result.radiance); result.gloom = Mathf.Clamp01(result.gloom);
        }
        private static PlantVisualParameters TraitVisualParameters(ThingDef cropDef, VarietyTraitDef trait)
        {
            PlantVisualParameters result = PlantVisualParameters.Default;
            VisualSettingsRecord visual = VisualOverride(cropDef, trait) ?? new VisualSettingsRecord(trait);
            ApplyShape(ref result, visual);
            ApplyColor(ref result, visual);
            VisualSettingsRecord radiance = visual;
            VisualSettingsRecord gloom = visual;
            VisualSettingsRecord overlay = visual;
            result.radiance = radiance.radiance;
            result.radianceScale = radiance.radianceScale;
            result.radianceRed = radiance.radianceRed; result.radianceGreen = radiance.radianceGreen; result.radianceBlue = radiance.radianceBlue;
            result.gloom = gloom.gloom;
            result.gloomScale = gloom.gloomScale;
            result.gloomRed = gloom.gloomRed; result.gloomGreen = gloom.gloomGreen; result.gloomBlue = gloom.gloomBlue;
            if (overlay.overlayPattern > 0)
            {
                result.overlayPattern = overlay.overlayPattern;
                result.overlayIntensity = overlay.overlayIntensity;
                result.overlayScale = overlay.overlayScale;
                result.overlayRed = overlay.overlayRed; result.overlayGreen = overlay.overlayGreen; result.overlayBlue = overlay.overlayBlue;
            }
            ClampVisual(ref result);
            return result;
        }

        private static float ScaleFor(ThingDef cropDef, VarietyTraitDef trait)
        {
            return TraitVisualParameters(cropDef, trait).scale;
        }

        private static float WidthFor(ThingDef cropDef, VarietyTraitDef trait)
        {
            return TraitVisualParameters(cropDef, trait).width;
        }

        private static float HeightFor(ThingDef cropDef, VarietyTraitDef trait)
        {
            return TraitVisualParameters(cropDef, trait).height;
        }

        private static float DensityFor(ThingDef cropDef, VarietyTraitDef trait)
        {
            return TraitVisualParameters(cropDef, trait).density;
        }

        public static float VisualScale(CompPlantVariety comp)
        {
            return VisualScale(comp?.parent?.def, comp?.ActiveTraits);
        }

        public static float VisualScale(IEnumerable<VarietyTraitDef> traits)
        {
            return VisualScale(null, traits);
        }

        private static float VisualScale(ThingDef cropDef, IEnumerable<VarietyTraitDef> traits)
        {
            float scale = 1f;
            if (traits == null) return scale;
            foreach (VarietyTraitDef trait in traits.Where(t => t != null)) scale *= ScaleFor(cropDef, trait);
            return Mathf.Max(0.1f, scale);
        }

        public static Color VisualTint(CompPlantVariety comp)
        {
            return VisualTint(comp?.parent?.def, comp?.ActiveTraits);
        }

        public static Color VisualTint(IEnumerable<VarietyTraitDef> traits)
        {
            return VisualTint(null, traits);
        }

        private static Color VisualTint(ThingDef cropDef, IEnumerable<VarietyTraitDef> traits)
        {
            Color tint = Color.white;
            if (traits == null) return tint;
            float dullness = 0f;
            foreach (VarietyTraitDef trait in traits.Where(t => t != null))
            {
                PlantVisualParameters visual = TraitVisualParameters(cropDef, trait);
                tint.r *= visual.tintRed;
                tint.g *= visual.tintGreen;
                tint.b *= visual.tintBlue;
                dullness += visual.dullness;
            }
            float dullnessFactor = 1f - Mathf.Clamp01(dullness);
            tint.r = Mathf.Clamp(tint.r * dullnessFactor, 0f, 2f);
            tint.g = Mathf.Clamp(tint.g * dullnessFactor, 0f, 2f);
            tint.b = Mathf.Clamp(tint.b * dullnessFactor, 0f, 2f);
            tint.a = 1f;
            return tint;
        }

        public static bool HasVisualTint(Color tint)
        {
            return !Mathf.Approximately(tint.r, 1f) || !Mathf.Approximately(tint.g, 1f) || !Mathf.Approximately(tint.b, 1f);
        }

        public static bool TraitHasTint(VarietyTraitDef trait)
        {
            return trait != null && (!Mathf.Approximately(trait.tintRed, 1f) || !Mathf.Approximately(trait.tintGreen, 1f) || !Mathf.Approximately(trait.tintBlue, 1f));
        }

        public static float VisualRadiance(CompPlantVariety comp)
        {
            ThingDef cropDef = comp?.parent?.def;
            return Mathf.Clamp01(comp?.ActiveTraits?.Where(t => t != null).Sum(t => TraitVisualParameters(cropDef, t).radiance) ?? 0f);
        }

        public static float VisualGloom(CompPlantVariety comp)
        {
            ThingDef cropDef = comp?.parent?.def;
            return Mathf.Clamp01(comp?.ActiveTraits?.Where(t => t != null).Sum(t => TraitVisualParameters(cropDef, t).gloom) ?? 0f);
        }

        public static float VisualWidth(CompPlantVariety comp)
        {
            ThingDef cropDef = comp?.parent?.def;
            return ProductVisual(comp?.ActiveTraits, trait => WidthFor(cropDef, trait));
        }

        public static float VisualHeight(CompPlantVariety comp)
        {
            ThingDef cropDef = comp?.parent?.def;
            return ProductVisual(comp?.ActiveTraits, trait => HeightFor(cropDef, trait));
        }

        public static float VisualDensity(CompPlantVariety comp)
        {
            ThingDef cropDef = comp?.parent?.def;
            return ProductVisual(comp?.ActiveTraits, trait => DensityFor(cropDef, trait));
        }

        public static bool VisualSpikes(CompPlantVariety comp)
        {
            ThingDef cropDef = comp?.parent?.def;
            return comp?.ActiveTraits?.Any(t => t != null && TraitVisualParameters(cropDef, t).overlayPattern == 1) == true;
        }

        private static float ProductVisual(IEnumerable<VarietyTraitDef> traits, System.Func<VarietyTraitDef, float> selector)
        {
            float factor = 1f;
            if (traits == null) return factor;
            foreach (VarietyTraitDef trait in traits.Where(t => t != null)) factor *= selector(trait);
            return Mathf.Max(0.1f, factor);
        }

        public static float VisualDullness(IEnumerable<VarietyTraitDef> traits)
        {
            return Mathf.Clamp01(traits?.Where(t => t != null).Sum(t => TraitVisualParameters(null, t).dullness) ?? 0f);
        }
        public static float BeautyOffset(CompPlantVariety comp)
        {
            return comp?.BeautyOffset ?? 0f;
        }

        public static float BeautyOffset(IEnumerable<VarietyTraitDef> traits)
        {
            float offset = 0f;
            if (traits == null)
            {
                return offset;
            }
            foreach (VarietyTraitDef trait in traits.Where(t => t != null))
            {
                offset += trait.beautyOffset;
            }
            return offset;
        }

        public static bool IsBeautyStat(StatDef stat)
        {
            return stat != null && (stat.defName == "Beauty" || stat.defName == "BeautyOutdoors");
        }

        public static string SignedNumber(float value)
        {
            if (Mathf.Approximately(value, 0f))
            {
                return "0";
            }
            string prefix = value > 0f ? "+" : string.Empty;
            return prefix + value.ToString("0.##");
        }

        public static float BlightChanceFactor(CompPlantVariety comp)
        {
            return comp?.BlightChanceFactor ?? 1f;
        }

        public static float BlightChanceFactor(IEnumerable<VarietyTraitDef> traits)
        {
            float factor = 1f;
            if (traits == null)
            {
                return factor;
            }
            foreach (VarietyTraitDef trait in traits.Where(t => t != null))
            {
                factor *= trait.blightChanceFactor <= 0f ? 1f : trait.blightChanceFactor;
            }
            return factor;
        }

        public static float BlightDamageFactor(CompPlantVariety comp)
        {
            return comp?.BlightDamageFactor ?? 1f;
        }

        public static float BlightDamageFactor(IEnumerable<VarietyTraitDef> traits)
        {
            float factor = 1f;
            if (traits == null)
            {
                return factor;
            }
            foreach (VarietyTraitDef trait in traits.Where(t => t != null))
            {
                factor *= trait.blightDamageFactor <= 0f ? 1f : trait.blightDamageFactor;
            }
            return factor;
        }

        public static float WorkFactor(CompPlantVariety comp)
        {
            return comp?.WorkFactor ?? 1f;
        }

        public static float WorkFactor(IEnumerable<VarietyTraitDef> traits)
        {
            float factor = 1f;
            if (traits == null)
            {
                return factor;
            }
            foreach (VarietyTraitDef trait in traits.Where(t => t != null))
            {
                factor *= trait.workFactor <= 0f ? 1f : trait.workFactor;
            }
            return Mathf.Max(0.05f, factor);
        }

        public static float MaxHitPointsFactor(CompPlantVariety comp)
        {
            return comp?.MaxHitPointsFactor ?? 1f;
        }

        public static float MaxHitPointsFactor(IEnumerable<VarietyTraitDef> traits)
        {
            float factor = 1f;
            if (traits == null)
            {
                return factor;
            }
            foreach (VarietyTraitDef trait in traits.Where(t => t != null))
            {
                factor *= trait.maxHitPointsFactor <= 0f ? 1f : trait.maxHitPointsFactor;
            }
            return Mathf.Max(0.05f, factor);
        }

        public static float PerennialHarvestAfterGrowth(CompPlantVariety comp)
        {
            return PerennialHarvestAfterGrowth(comp?.ActiveTraits);
        }

        public static float PerennialHarvestAfterGrowth(IEnumerable<VarietyTraitDef> traits)
        {
            float resetGrowth = 0f;
            if (traits == null)
            {
                return resetGrowth;
            }
            foreach (VarietyTraitDef trait in traits.Where(t => t?.perennial == true))
            {
                resetGrowth = Mathf.Max(resetGrowth, trait.harvestAfterGrowth > 0f ? trait.harvestAfterGrowth : 0.30f);
            }
            return Mathf.Clamp01(resetGrowth);
        }

        public static bool HasJoyResin(IEnumerable<VarietyTraitDef> traits)
        {
            return traits != null && traits.Any(t => t?.joyResinThought != null);
        }

        public static void ApplyJoyResinThought(Pawn pawn, IEnumerable<VarietyTraitDef> traits)
        {
            if (pawn?.needs?.mood?.thoughts?.memories == null || traits == null)
            {
                return;
            }

            foreach (ThoughtDef thought in traits.Where(t => t?.joyResinThought != null).Select(t => t.joyResinThought).Distinct())
            {
                pawn.needs.mood.thoughts.memories.TryGainMemory(thought);
            }
        }

        public static List<BodyPartRecord> UncoveredHands(Pawn pawn)
        {
            List<BodyPartRecord> hands = pawn?.health?.hediffSet?.GetNotMissingParts()
                .Where(part => part.def?.defName == "Hand")
                .ToList() ?? new List<BodyPartRecord>();
            if (pawn?.apparel?.WornApparel == null)
            {
                return hands;
            }

            return hands.Where(hand => !pawn.apparel.WornApparel.Any(apparel => apparel?.def?.apparel?.CoversBodyPart(hand) == true)).ToList();
        }

        public static bool HandsProtectedFromPlantContact(Pawn pawn)
        {
            return UncoveredHands(pawn).Count == 0;
        }

        public static float ThornScratchChance(IEnumerable<VarietyTraitDef> traits)
        {
            if (traits == null)
            {
                return 0f;
            }

            float chanceToAvoidScratch = 1f;
            foreach (VarietyTraitDef trait in traits.Where(t => t?.thornScratchChance > 0f))
            {
                chanceToAvoidScratch *= 1f - Mathf.Clamp01(trait.thornScratchChance);
            }
            return 1f - chanceToAvoidScratch;
        }

        public static float ThornScratchDamage(IEnumerable<VarietyTraitDef> traits)
        {
            float damage = 0f;
            if (traits == null)
            {
                return damage;
            }
            foreach (VarietyTraitDef trait in traits.Where(t => t?.thornScratchChance > 0f))
            {
                damage = Mathf.Max(damage, trait.thornScratchDamage > 0f ? trait.thornScratchDamage : 3f);
            }
            return damage;
        }

        public static void TryThornScratch(Pawn pawn, Thing instigator, IEnumerable<VarietyTraitDef> traits)
        {
            if (pawn?.health?.hediffSet == null)
            {
                return;
            }

            List<BodyPartRecord> handParts = UncoveredHands(pawn);
            if (handParts.Count == 0)
            {
                return;
            }

            float chance = ThornScratchChance(traits);
            if (chance <= 0f || !Rand.Chance(chance))
            {
                return;
            }

            BodyPartRecord hitPart = handParts.RandomElement();

            float damage = Mathf.Max(1f, ThornScratchDamage(traits));
            Thing source = instigator != null && !instigator.Destroyed ? instigator : null;
            pawn.TakeDamage(new DamageInfo(DamageDefOf.Scratch, damage, 0f, -1f, source, hitPart));
        }

        public static void TemperatureOffsets(CompPlantVariety comp, out float coldOffset, out float heatOffset)
        {
            coldOffset = comp?.ColdGrowthOffset ?? 0f;
            heatOffset = comp?.HeatGrowthOffset ?? 0f;
        }

        public static void TemperatureOffsets(IEnumerable<VarietyTraitDef> traits, out float coldOffset, out float heatOffset)
        {
            coldOffset = 0f;
            heatOffset = 0f;
            if (traits == null)
            {
                return;
            }
            foreach (VarietyTraitDef trait in traits.Where(t => t != null))
            {
                coldOffset += trait.coldGrowthOffset;
                heatOffset += trait.heatGrowthOffset;
            }
        }

        public static List<string> StatChangeLines(IEnumerable<VarietyTraitDef> traits, ThingDef cropDef = null)
        {
            List<string> lines = new List<string>();
            if (traits == null)
            {
                return lines;
            }

            foreach (VarietyTraitDef trait in traits.Where(t => t != null))
            {
                AddTraitStatChangeLines(lines, trait, cropDef);
            }
            return lines;
        }

        private static void AddTraitStatChangeLines(List<string> lines, VarietyTraitDef trait, ThingDef cropDef)
        {
            float yieldFactor = trait.yieldFactor <= 0f ? 1f : trait.yieldFactor;
            PlantVisualParameters visual = TraitVisualParameters(cropDef, trait);
            float visualScale = visual.scale, visualWidth = visual.width, visualHeight = visual.height, visualDensity = visual.density;
            float tintRed = visual.tintRed, tintGreen = visual.tintGreen, tintBlue = visual.tintBlue;
            float visualRadiance = visual.radiance, visualDullness = visual.dullness, visualGloom = visual.gloom;
            int overlayPattern = visual.overlayPattern;
            float hueShift = visual.hueShift, saturation = visual.saturation, brightness = visual.brightness, contrast = visual.contrast, opacity = visual.opacity;
            float spread = visual.spread, rotation = visual.rotation, rotationVariation = visual.rotationVariation, scaleVariation = visual.scaleVariation;
            float offsetX = visual.offsetX, offsetZ = visual.offsetZ, shadowScale = visual.shadowScale;
            float workFactor = trait.workFactor <= 0f ? 1f : trait.workFactor;
            float hitPointsFactor = trait.maxHitPointsFactor <= 0f ? 1f : trait.maxHitPointsFactor;
            float blightChanceFactor = trait.blightChanceFactor <= 0f ? 1f : trait.blightChanceFactor;
            float blightDamageFactor = trait.blightDamageFactor <= 0f ? 1f : trait.blightDamageFactor;

            if (!Mathf.Approximately(yieldFactor, 1f))
            {
                AddTraitEffectLine(lines, trait, "HNS_StatYieldMultiplier".Translate(yieldFactor.ToStringPercent()).ToString());
            }
            if (!Mathf.Approximately(workFactor, 1f))
            {
                AddTraitEffectLine(lines, trait, "HNS_StatWorkMultiplier".Translate(workFactor.ToStringPercent()).ToString());
            }
            if (!Mathf.Approximately(hitPointsFactor, 1f))
            {
                AddTraitEffectLine(lines, trait, "HNS_StatHitPoints".Translate(hitPointsFactor.ToStringPercent()).ToString());
            }
            if (!Mathf.Approximately(trait.beautyOffset, 0f))
            {
                AddTraitEffectLine(lines, trait, "HNS_StatBeauty".Translate(SignedNumber(trait.beautyOffset)).ToString());
            }
            if (trait.perennial)
            {
                float resetGrowth = Mathf.Clamp01(trait.harvestAfterGrowth > 0f ? trait.harvestAfterGrowth : 0.30f);
                AddTraitEffectLine(lines, trait, "HNS_StatPerennial".Translate(resetGrowth.ToStringPercent()).ToString());
            }
            if (!Mathf.Approximately(visualScale, 1f))
            {
                AddTraitEffectLine(lines, trait, "HNS_StatVisualScale".Translate(visualScale.ToStringPercent()).ToString());
            }
            bool hasTint = !Mathf.Approximately(tintRed, 1f) || !Mathf.Approximately(tintGreen, 1f) || !Mathf.Approximately(tintBlue, 1f);
            if (hasTint)
            {
                string tintLabel = VisualOverride(cropDef, trait) == null && !trait.visualTintLabel.NullOrEmpty()
                    ? trait.visualTintLabel + " " + TraitColorUI.Swatch(new Color(tintRed, tintGreen, tintBlue))
                    : TraitColorUI.Swatch(new Color(tintRed, tintGreen, tintBlue));
                AddTraitEffectLine(lines, trait, "HNS_StatVisualTint".Translate(tintLabel).ToString());
            }
            if (visualRadiance > 0f) AddTraitEffectLine(lines, trait, "HNS_StatVisualSetting".Translate("Visual radiance", visualRadiance.ToStringPercent()).ToString());
            if (visualDullness > 0f) AddTraitEffectLine(lines, trait, "HNS_StatVisualSetting".Translate("Visual dullness", visualDullness.ToStringPercent()).ToString());
            if (visualGloom > 0f) AddTraitEffectLine(lines, trait, "HNS_StatVisualSetting".Translate("Visual gloom", visualGloom.ToStringPercent()).ToString());
            if (!Mathf.Approximately(visualWidth, 1f)) AddTraitEffectLine(lines, trait, "HNS_StatVisualWidth".Translate(visualWidth.ToStringPercent()).ToString());
            if (!Mathf.Approximately(visualHeight, 1f)) AddTraitEffectLine(lines, trait, "HNS_StatVisualHeight".Translate(visualHeight.ToStringPercent()).ToString());
            if (!Mathf.Approximately(visualDensity, 1f)) AddTraitEffectLine(lines, trait, "HNS_StatVisualDensity".Translate(visualDensity.ToStringPercent()).ToString());
            if (!Mathf.Approximately(hueShift, 0f)) AddTraitEffectLine(lines, trait, "HNS_StatVisualSetting".Translate("Hue shift", SignedNumber(hueShift * 360f) + " deg").ToString());
            if (!Mathf.Approximately(saturation, 1f)) AddTraitEffectLine(lines, trait, "HNS_StatVisualSetting".Translate("Saturation", saturation.ToStringPercent()).ToString());
            if (!Mathf.Approximately(brightness, 1f)) AddTraitEffectLine(lines, trait, "HNS_StatVisualSetting".Translate("Brightness", brightness.ToStringPercent()).ToString());
            if (!Mathf.Approximately(contrast, 1f)) AddTraitEffectLine(lines, trait, "HNS_StatVisualSetting".Translate("Contrast", contrast.ToStringPercent()).ToString());
            if (!Mathf.Approximately(opacity, 1f)) AddTraitEffectLine(lines, trait, "HNS_StatVisualSetting".Translate("Opacity", opacity.ToStringPercent()).ToString());
            if (!Mathf.Approximately(spread, 1f)) AddTraitEffectLine(lines, trait, "HNS_StatVisualSetting".Translate("Mesh spread", spread.ToStringPercent()).ToString());
            if (!Mathf.Approximately(rotation, 0f)) AddTraitEffectLine(lines, trait, "HNS_StatVisualSetting".Translate("Rotation", SignedNumber(rotation) + " deg").ToString());
            if (!Mathf.Approximately(rotationVariation, 0f)) AddTraitEffectLine(lines, trait, "HNS_StatVisualSetting".Translate("Rotation variation", rotationVariation.ToString("0") + " deg").ToString());
            if (!Mathf.Approximately(scaleVariation, 0f)) AddTraitEffectLine(lines, trait, "HNS_StatVisualSetting".Translate("Size variation", scaleVariation.ToStringPercent()).ToString());
            if (!Mathf.Approximately(offsetX, 0f)) AddTraitEffectLine(lines, trait, "HNS_StatVisualSetting".Translate("Horizontal offset", SignedNumber(offsetX) + " cells").ToString());
            if (!Mathf.Approximately(offsetZ, 0f)) AddTraitEffectLine(lines, trait, "HNS_StatVisualSetting".Translate("Vertical offset", SignedNumber(offsetZ) + " cells").ToString());
            if (!Mathf.Approximately(shadowScale, 1f)) AddTraitEffectLine(lines, trait, "HNS_StatVisualSetting".Translate("Shadow size", shadowScale.ToStringPercent()).ToString());
            if (overlayPattern > 0) AddTraitEffectLine(lines, trait, "HNS_StatVisualOverlay".Translate(VisualOverlayLabel(overlayPattern)).ToString());
            if (trait.sowSkillOffset != 0)
            {
                AddTraitEffectLine(lines, trait, "HNS_StatSowSkill".Translate(SignedNumber(trait.sowSkillOffset)).ToString());
            }
            if (!Mathf.Approximately(trait.sowWorkFactor, 1f))
            {
                AddTraitEffectLine(lines, trait, "HNS_StatSowWork".Translate(trait.sowWorkFactor.ToStringPercent()).ToString());
            }
            if (!Mathf.Approximately(trait.harvestWorkFactor, 1f))
            {
                AddTraitEffectLine(lines, trait, "HNS_StatHarvestWork".Translate(trait.harvestWorkFactor.ToStringPercent()).ToString());
            }
            if (trait.perennialColdDormancy)
            {
                AddTraitEffectLine(lines, trait, "HNS_StatColdDormancy".Translate((trait.dormantGrowthFactor > 0f ? trait.dormantGrowthFactor : 0.01f).ToStringPercent()).ToString());
            }
            if (trait.selfSeeding) AddTraitEffectLine(lines, trait, "HNS_StatSelfSeeding".Translate().ToString());
            if (trait.tramplingDamage > 0f) AddTraitEffectLine(lines, trait, "HNS_StatTrampling".Translate(trait.tramplingDamage.ToString("0.#")).ToString());
            if (!Mathf.Approximately(trait.forageNutritionFactor, 1f)) AddTraitEffectLine(lines, trait, "HNS_StatForage".Translate(trait.forageNutritionFactor.ToStringPercent()).ToString());
            if (trait.humongousSpacing) AddTraitEffectLine(lines, trait, "HNS_StatSpacing".Translate().ToString());
            if (!trait.requiredSowTag.NullOrEmpty()) AddTraitEffectLine(lines, trait, "HNS_StatZone".Translate(trait.requiredSowTag == "VCE_Aquatic" ? "aquatic" : "sandy").ToString());
            if (!Mathf.Approximately(trait.fishingYieldFactor, 1f)) AddTraitEffectLine(lines, trait, "HNS_StatFishingYield".Translate(trait.fishingYieldFactor.ToStringPercent()).ToString());
            if (!Mathf.Approximately(trait.companionGrowthFactor, 1f)) AddTraitEffectLine(lines, trait, "HNS_StatCompanion".Translate(trait.companionGrowthFactor.ToStringPercent()).ToString());            if (trait.synergyPlantDef != null && !trait.synergyStat.NullOrEmpty()) AddTraitEffectLine(lines, trait, "HNS_StatSynergyTyped".Translate(trait.synergyPlantDef.LabelCap, SynergyTraitFactory.StatLabel(trait.synergyStat), (trait.synergyFactor > 0f ? trait.synergyFactor : 1.15f).ToStringPercent()).ToString());
            if (trait.byproductDef != null) AddTraitEffectLine(lines, trait, "HNS_StatByproduct".Translate(trait.byproductDef.LabelCap, Mathf.Clamp01(trait.byproductChance).ToStringPercent()).ToString());
            if (trait.resinHediff != null || trait.resinDamage != null) AddTraitEffectLine(lines, trait, "HNS_StatResin".Translate().ToString());
            float nutritionFactor = trait.nutritionFactor <= 0f ? 1f : trait.nutritionFactor;
            if (!Mathf.Approximately(nutritionFactor, 1f)) AddTraitEffectLine(lines, trait, "HNS_StatProduceNutrition".Translate(nutritionFactor.ToStringPercent()).ToString());
            float medicalPotencyFactor = trait.medicalPotencyFactor <= 0f ? 1f : trait.medicalPotencyFactor;
            if (!Mathf.Approximately(medicalPotencyFactor, 1f)) AddTraitEffectLine(lines, trait, "HNS_StatMedicalPotency".Translate(medicalPotencyFactor.ToStringPercent()).ToString());
            if (trait.compoundThought != null) AddTraitEffectLine(lines, trait, "HNS_StatCompound".Translate(trait.compoundThought.LabelCap).ToString());
            if (trait.compoundHediff != null) AddTraitEffectLine(lines, trait, "HNS_StatCompound".Translate(trait.compoundHediff.LabelCap).ToString());
            if (trait.requiredResourceDef != null) AddTraitEffectLine(lines, trait, "HNS_StatResourceNeed".Translate(trait.requiredResourceCount, trait.requiredResourceDef.LabelCap).ToString());
            if (!Mathf.Approximately(trait.coldGrowthOffset, 0f))
            {
                AddTraitEffectLine(lines, trait, "HNS_StatColdOffset".Translate(SignedTemperature(trait.coldGrowthOffset)).ToString());
            }
            if (!Mathf.Approximately(trait.heatGrowthOffset, 0f))
            {
                AddTraitEffectLine(lines, trait, "HNS_StatHeatOffset".Translate(SignedTemperature(trait.heatGrowthOffset)).ToString());
            }
            if (!Mathf.Approximately(blightChanceFactor, 1f))
            {
                AddTraitEffectLine(lines, trait, "HNS_StatBlightChance".Translate(blightChanceFactor.ToStringPercent()).ToString());
            }
            if (!Mathf.Approximately(blightDamageFactor, 1f))
            {
                AddTraitEffectLine(lines, trait, "HNS_StatBlightDamage".Translate(blightDamageFactor.ToStringPercent()).ToString());
            }
            if (trait.joyResinThought != null)
            {
                AddTraitEffectLine(lines, trait, "HNS_StatJoyresin".Translate().ToString());
            }
            if (trait.thornScratchChance > 0f)
            {
                float damage = trait.thornScratchDamage > 0f ? trait.thornScratchDamage : 3f;
                AddTraitEffectLine(lines, trait, "HNS_StatThorny".Translate(Mathf.Clamp01(trait.thornScratchChance).ToStringPercent(), damage.ToString("0.#")).ToString());
            }
        }

        private static string VisualOverlayLabel(int pattern)
        {
            switch (pattern)
            {
                case 1: return "spikes";
                case 2: return "spots";
                case 3: return "stripes";
                case 4: return "veins";
                case 5: return "speckles";
                default: return "none";
            }
        }
        private static void AddTraitEffectLine(List<string> lines, VarietyTraitDef trait, string effect)
        {
            lines.Add("HNS_StatTraitEffect".Translate(trait.LabelCap, effect).ToString());
        }

        public static string SignedTemperature(float offset)
        {
            if (Mathf.Approximately(offset, 0f))
            {
                return "0";
            }
            string prefix = offset > 0f ? "+" : string.Empty;
            return prefix + GenText.ToStringTemperature(offset, "F0");
        }
        public static VarietyRecord SelectedVarietyForPlanting(ThingDef plantDef, IntVec3 cell, Map map)
        {
            if (plantDef == null || map == null || !cell.IsValid)
            {
                return null;
            }

            IPlantToGrowSettable grower = GridsUtility.GetPlantToGrowSettable(cell, map);
            VarietyRecord selected = GameComponent_NovelSeeds.Instance?.VarietyForSowing(grower, cell);
            return selected != null && selected.cropDef == plantDef ? selected : null;
        }

        public static float SowingWorkFactor(ThingDef plantDef, IntVec3 cell, Map map)
        {
            List<VarietyTraitDef> traits = SelectedVarietyForPlanting(plantDef, cell, map)?.traits;
            return ExpandedTraitUtility.SowWorkFactor(traits) / ExpandedTraitUtility.SynergyFactorAt(cell, map, traits, "SowSpeed");
        }
        public static void DropDiscoverySeed(ThingDef cropDef, List<VarietyTraitDef> traits, IntVec3 position, Map map,
            IEnumerable<string> lineageParentIds = null, string originKind = null)
        {
            if (cropDef == null || traits == null || traits.Count == 0 || map == null)
            {
                return;
            }

            Thing seedPack = ThingMaker.MakeThing(HNS_DefOf.HNS_NovelSeedPack);
            seedPack.TryGetComp<CompNovelSeedPack>()?.Initialize(cropDef, traits, lineageParentIds, originKind);
            GenPlace.TryPlaceThing(seedPack, position, map, ThingPlaceMode.Near);
            Find.LetterStack.ReceiveLetter("HNS_SeedDiscovered".Translate(cropDef.LabelCap), "HNS_SeedDiscoveredDesc".Translate(cropDef.label, TraitSummary(traits)), LetterDefOf.PositiveEvent, seedPack);
        }

        private static void AddBlockedTags(VarietyTraitDef trait, HashSet<string> blockedTags)
        {
            if (trait?.exclusionTags == null)
            {
                return;
            }
            foreach (string tag in trait.exclusionTags)
            {
                blockedTags.Add(tag);
            }
        }

        private static bool SharesBlockedTag(VarietyTraitDef trait, HashSet<string> blockedTags)
        {
            if (trait.exclusionTags == null)
            {
                return false;
            }
            return trait.exclusionTags.Any(blockedTags.Contains);
        }
    }
}
