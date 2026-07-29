using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace HorticultureNovelSeeds
{
    internal static class NovelProduceDefCache
    {
        private static HashSet<ThingDef> supportedDefs;

        public static bool Contains(ThingDef def)
        {
            if (def == null) return false;
            if (supportedDefs == null)
            {
                supportedDefs = new HashSet<ThingDef>(DefDatabase<ThingDef>.AllDefsListForReading.Where(candidate =>
                    candidate?.comps?.Any(props => props.compClass == typeof(CompNovelProduceAppearance)) == true));
            }
            return supportedDefs.Contains(def);
        }
    }

    public sealed class CompProperties_NovelProduceAppearance : CompProperties
    {
        public CompProperties_NovelProduceAppearance()
        {
            compClass = typeof(CompNovelProduceAppearance);
        }
    }

    public sealed class ProduceInheritanceData
    {
        public bool directVarietyProduce;
        public string sourcePlantDefName;
        public readonly List<string> sourceVarietyIds = new List<string>();
        public readonly List<string> sourceVarietyNames = new List<string>();
        public readonly List<string> traitDefNames = new List<string>();
        public readonly List<string> effectiveTraitDefNames = new List<string>();
        public readonly Dictionary<string, Color> materialColors = new Dictionary<string, Color>();
        public float nutritionFactor = 1f;

        public bool HasData => sourceVarietyNames.Count > 0 || traitDefNames.Count > 0 || materialColors.Count > 0 || !Mathf.Approximately(nutritionFactor, 1f);
    }

    public sealed class CompNovelProduceAppearance : ThingComp
    {
        private bool hasAppearance;
        private int packedColor;
        private List<int> packedMaskColors = new List<int>();
        private bool directVarietyProduce;
        private string sourcePlantDefName;
        private List<string> sourceVarietyIds = new List<string>();
        private List<string> sourceVarietyNames = new List<string>();
        private List<string> traitDefNames = new List<string>();
        private List<string> effectiveTraitDefNames = new List<string>();
        private float nutritionFactor = 1f;
        private bool hasInheritedTraitEffects;
        [Unsaved(false)] private ThingDef sourcePlantDefCache;
        [Unsaved(false)] private List<VarietyTraitDef> inheritedTraitsCache;
        [Unsaved(false)] private List<VarietyTraitDef> effectiveTraitsCache;
        [Unsaved(false)] private HashSet<string> effectiveTraitNamesCache;
        [Unsaved(false)] private float produceBeautyOffsetCache;
        [Unsaved(false)] private float medicalPotencyFactorCache;
        [Unsaved(false)] private float produceMaxHitPointsFactorCache;
        [Unsaved(false)] private float produceArmorHeatFactorCache;
        [Unsaved(false)] private float produceColdInsulationFactorCache;
        [Unsaved(false)] private string produceStyleKeyCache;
        private static readonly IReadOnlyList<string> EmptyStrings = new string[0];

        public bool HasStoredAppearance => hasAppearance;
        public bool HasVarietyData => sourceVarietyNames != null && sourceVarietyNames.Count > 0;
        public bool HasInheritedQualities => (traitDefNames?.Count ?? 0) > 0 || !Mathf.Approximately(nutritionFactor, 1f);
        public bool HasNovelData => HasVarietyData || HasInheritedQualities || hasAppearance;
        public float NutritionFactor => Mathf.Clamp(nutritionFactor, 0.1f, 5f);
        public bool DirectVarietyProduce => directVarietyProduce;
        public IReadOnlyList<string> SourceVarietyIds => sourceVarietyIds ?? EmptyStrings;
        public ThingDef SourcePlantDef { get { EnsureResolvedCaches(); return sourcePlantDefCache; } }
        public List<VarietyTraitDef> InheritedTraits { get { EnsureResolvedCaches(); return inheritedTraitsCache; } }
        public List<VarietyTraitDef> EffectiveTraits { get { EnsureResolvedCaches(); return effectiveTraitsCache; } }

        public bool HasProduceEffect(VarietyTraitDef trait)
        {
            EnsureResolvedCaches();
            return trait != null && effectiveTraitNamesCache.Contains(trait.defName);
        }
        public float ProduceBeautyOffset { get { EnsureResolvedCaches(); return produceBeautyOffsetCache; } }
        internal string ProduceStyleKey { get { EnsureResolvedCaches(); return produceStyleKeyCache; } }

        public List<string> SourceVarietyLabels
        {
            get
            {
                List<string> labels = new List<string>();
                int count = Mathf.Max(sourceVarietyIds?.Count ?? 0, sourceVarietyNames?.Count ?? 0);
                for (int i = 0; i < count; i++)
                {
                    string id = i < (sourceVarietyIds?.Count ?? 0) ? sourceVarietyIds[i] : null;
                    string fallback = i < (sourceVarietyNames?.Count ?? 0) ? sourceVarietyNames[i] : null;
                    string label = GameComponent_NovelSeeds.Instance?.GetVariety(id)?.Label;
                    labels.Add(!label.NullOrEmpty() ? label : !fallback.NullOrEmpty() ? fallback : "HNS_UnknownVariety".Translate().ToString());
                }
                return labels.Distinct().ToList();
            }
        }

        private bool AppearanceEnabled => HorticultureNovelSeedsMod.Settings?.enableProduceVisuals != false
            && (hasAppearance || directVarietyProduce && NovelSeedUtility.HasProduceVisual(SourcePlantDef, InheritedTraits));
        private bool UsesCustomProduceDraw => AppearanceEnabled && directVarietyProduce && ProduceMaskRenderer.NeedsCustomDraw(SourcePlantDef, InheritedTraits);

        public void InitializeFromPlant(CompPlantVariety plantComp, Color color, IEnumerable<Color> maskColors)
        {
            List<VarietyTraitDef> traits = plantComp?.ActiveTraits?.Where(trait => trait != null).Distinct().ToList() ?? new List<VarietyTraitDef>();
            List<VarietyTraitDef> inheritedTraits = traits;
            ThingDef sourcePlant = plantComp?.parent?.def;
            NovelSeedsSettings settings = HorticultureNovelSeedsMod.Settings;
            List<VarietyTraitDef> effectiveTraits = inheritedTraits
                .Where(trait => settings?.ProduceTraitHasEffect(sourcePlant, trait) ?? PlantTagUtility.MeetsProduceEffectRequirements(sourcePlant, trait))
                .ToList();
            ProduceInheritanceData data = new ProduceInheritanceData
            {
                directVarietyProduce = true,
                sourcePlantDefName = plantComp?.parent?.def?.defName,
                nutritionFactor = NovelSeedUtility.ProduceNutritionFactor(effectiveTraits)
            };
            data.sourceVarietyIds.Add(plantComp?.VarietyId ?? string.Empty);
            data.sourceVarietyNames.Add(plantComp?.DisplayVarietyName ?? "HNS_UnknownVariety".Translate().ToString());
            data.traitDefNames.AddRange(inheritedTraits.Select(trait => trait.defName));
            data.effectiveTraitDefNames.AddRange(effectiveTraits.Select(trait => trait.defName));
            InitializeData(data);
            SetAppearance(color, maskColors);
        }

        public void InitializeInherited(ProduceInheritanceData data)
        {
            InitializeInherited(data, null);
        }

        public void InitializeInherited(ProduceInheritanceData data, Color? materialColor)
        {
            InitializeData(data);
            if (materialColor.HasValue)
            {
                SetAppearance(materialColor.Value);
                parent?.TryGetComp<CompColorable>()?.SetColor(materialColor.Value);
            }
            else
            {
                hasAppearance = false;
                packedColor = 0;
                packedMaskColors.Clear();
            }
        }

        private void InitializeData(ProduceInheritanceData data)
        {
            directVarietyProduce = data?.directVarietyProduce == true;
            sourcePlantDefName = data?.sourcePlantDefName;
            sourceVarietyIds = data?.sourceVarietyIds?.ToList() ?? new List<string>();
            sourceVarietyNames = data?.sourceVarietyNames?.ToList() ?? new List<string>();
            traitDefNames = data?.traitDefNames?.Where(name => !name.NullOrEmpty()).Distinct().OrderBy(name => name).ToList() ?? new List<string>();
            effectiveTraitDefNames = data?.effectiveTraitDefNames?.Where(name => !name.NullOrEmpty()).Distinct().OrderBy(name => name).ToList() ?? new List<string>();
            nutritionFactor = QuantizeNutrition(data?.nutritionFactor ?? 1f);
            hasInheritedTraitEffects = traitDefNames.Count > 0;
            NormalizeSources();
            InvalidateResolvedCaches();
        }

        public void SetAppearance(Color color, IEnumerable<Color> maskColors = null)
        {
            Color32 quantized = color;
            packedColor = Pack(quantized);
            packedMaskColors = maskColors?.Select(item => Pack((Color32)item)).Take(3).ToList() ?? new List<int>();
            while (packedMaskColors.Count < 3) packedMaskColors.Add(packedColor);
            int white = Pack(new Color32(255, 255, 255, 255));
            hasAppearance = packedColor != white || packedMaskColors.Any(value => value != white);
        }

        internal bool TryGetReplacementGraphic(Graphic original, out Graphic replacement)
        {
            replacement = original;
            return UsesCustomProduceDraw
                && ProduceMaskRenderer.TryGetReplacementGraphic(parent, SourcePlantDef, InheritedTraits, ProduceStyleKey, original, out replacement);
        }

        public override Color? ForceColor()
        {
            return AppearanceEnabled && !UsesCustomProduceDraw ? (Color)Unpack(packedColor) : (Color?)null;
        }

        internal bool TryGetStoredColor(out Color color)
        {
            color = Color.white;
            if (!hasAppearance) return false;

            int white = Pack(new Color32(255, 255, 255, 255));
            if ((packedMaskColors?.Count ?? 0) > 0 && packedMaskColors[0] != white)
            {
                color = Unpack(packedMaskColors[0]);
                return true;
            }
            if (packedColor == white) return false;
            color = Unpack(packedColor);
            return true;
        }

        public override float GetStatFactor(StatDef stat)
        {
            if (stat == null || traitDefNames == null) return 1f;
            EnsureResolvedCaches();
            if (stat == StatDefOf.MedicalPotency) return medicalPotencyFactorCache;
            if (stat == StatDefOf.MaxHitPoints) return produceMaxHitPointsFactorCache;
            if (stat == StatDefOf.StuffPower_Armor_Heat || stat == StatDefOf.ArmorRating_Heat) return produceArmorHeatFactorCache;
            if (stat == StatDefOf.StuffPower_Insulation_Cold || stat == StatDefOf.Insulation_Cold) return produceColdInsulationFactorCache;
            return 1f;
        }
        public override bool AllowStackWith(Thing other)
        {
            CompNovelProduceAppearance otherComp = other?.TryGetComp<CompNovelProduceAppearance>();
            bool active = HasNovelData;
            bool otherActive = otherComp?.HasNovelData == true;
            if (!active || !otherActive) return active == otherActive;
            if (directVarietyProduce != otherComp.directVarietyProduce
                || sourcePlantDefName != otherComp.sourcePlantDefName
                || !Mathf.Approximately(NutritionFactor, otherComp.NutritionFactor)
                || !SourcesEqual(otherComp)
                || !SequenceEqual(traitDefNames, otherComp.traitDefNames)
                || !SequenceEqual(effectiveTraitDefNames, otherComp.effectiveTraitDefNames)
                || !SequenceEqual(packedMaskColors, otherComp.packedMaskColors))
            {
                return false;
            }
            return !AppearanceEnabled && !otherComp.AppearanceEnabled
                || AppearanceEnabled && otherComp.AppearanceEnabled && packedColor == otherComp.packedColor;
        }

        public override void PostSplitOff(Thing piece)
        {
            CompNovelProduceAppearance splitComp = piece?.TryGetComp<CompNovelProduceAppearance>();
            if (splitComp == null) return;
            splitComp.hasAppearance = hasAppearance;
            splitComp.packedColor = packedColor;
            splitComp.packedMaskColors = packedMaskColors?.ToList() ?? new List<int>();
            splitComp.directVarietyProduce = directVarietyProduce;
            splitComp.sourcePlantDefName = sourcePlantDefName;
            splitComp.sourceVarietyIds = sourceVarietyIds?.ToList() ?? new List<string>();
            splitComp.sourceVarietyNames = sourceVarietyNames?.ToList() ?? new List<string>();
            splitComp.traitDefNames = traitDefNames?.ToList() ?? new List<string>();
            splitComp.effectiveTraitDefNames = effectiveTraitDefNames?.ToList() ?? new List<string>();
            splitComp.nutritionFactor = nutritionFactor;
            splitComp.hasInheritedTraitEffects = hasInheritedTraitEffects;
            splitComp.InvalidateResolvedCaches();
        }

        public override void PostIngested(Pawn ingester)
        {
            if (ingester == null) return;
            foreach (VarietyTraitDef trait in EffectiveTraits)
            {
                if (trait.compoundThought != null && ingester.needs?.mood?.thoughts?.memories != null)
                    ingester.needs.mood.thoughts.memories.TryGainMemory(trait.compoundThought);
                if (trait.compoundHediff != null && ingester.health != null)
                {
                    Hediff hediff = ingester.health.AddHediff(trait.compoundHediff);
                    if (hediff != null && trait.compoundHediffSeverity > 0f)
                        hediff.Severity = Mathf.Max(hediff.Severity, trait.compoundHediffSeverity);
                }
            }
        }

        public override void PostExposeData()
        {
            Scribe_Values.Look(ref hasAppearance, "hasNovelProduceAppearance", false);
            Scribe_Values.Look(ref packedColor, "novelProduceColor", 0);
            Scribe_Collections.Look(ref packedMaskColors, "novelProduceMaskColors", LookMode.Value);
            Scribe_Values.Look(ref directVarietyProduce, "directVarietyProduce", false);
            Scribe_Values.Look(ref sourcePlantDefName, "sourcePlantDefName");
            Scribe_Collections.Look(ref sourceVarietyIds, "sourceVarietyIds", LookMode.Value);
            Scribe_Collections.Look(ref sourceVarietyNames, "sourceVarietyNames", LookMode.Value);
            Scribe_Collections.Look(ref traitDefNames, "inheritedTraitDefNames", LookMode.Value);
            Scribe_Collections.Look(ref effectiveTraitDefNames, "effectiveInheritedTraitDefNames", LookMode.Value);
            Scribe_Values.Look(ref nutritionFactor, "inheritedNutritionFactor", 1f);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                packedMaskColors = packedMaskColors ?? new List<int>();
                while (packedMaskColors.Count < 3) packedMaskColors.Add(packedColor);
                if (packedMaskColors.Count > 3) packedMaskColors.RemoveRange(3, packedMaskColors.Count - 3);
                sourceVarietyIds = sourceVarietyIds ?? new List<string>();
                sourceVarietyNames = sourceVarietyNames ?? new List<string>();
                traitDefNames = traitDefNames?.Where(name => !name.NullOrEmpty()).Distinct().OrderBy(name => name).ToList() ?? new List<string>();
                effectiveTraitDefNames = effectiveTraitDefNames?.Where(name => !name.NullOrEmpty()).Distinct().OrderBy(name => name).ToList() ?? new List<string>();
                nutritionFactor = QuantizeNutrition(nutritionFactor);
                hasInheritedTraitEffects = traitDefNames.Count > 0;
                NormalizeSources();
                InvalidateResolvedCaches();
            }
        }

        private void InvalidateResolvedCaches()
        {
            sourcePlantDefCache = null;
            inheritedTraitsCache = null;
            effectiveTraitsCache = null;
            effectiveTraitNamesCache = null;
            produceBeautyOffsetCache = 0f;
            medicalPotencyFactorCache = 1f;
            produceMaxHitPointsFactorCache = 1f;
            produceArmorHeatFactorCache = 1f;
            produceColdInsulationFactorCache = 1f;
            produceStyleKeyCache = null;
        }

        private void EnsureResolvedCaches()
        {
            if (inheritedTraitsCache != null) return;
            sourcePlantDefCache = sourcePlantDefName.NullOrEmpty() ? null : DefDatabase<ThingDef>.GetNamedSilentFail(sourcePlantDefName);
            inheritedTraitsCache = ResolveTraits(traitDefNames);
            effectiveTraitsCache = ResolveTraits(effectiveTraitDefNames);
            effectiveTraitNamesCache = new HashSet<string>(effectiveTraitDefNames ?? Enumerable.Empty<string>());
            produceStyleKeyCache = (sourcePlantDefName ?? string.Empty) + "|" + string.Join(",", traitDefNames ?? Enumerable.Empty<string>());
            produceBeautyOffsetCache = 0f;
            foreach (VarietyTraitDef trait in effectiveTraitsCache)
            {
                produceBeautyOffsetCache += trait.produceBeautyOffset;
                medicalPotencyFactorCache *= PositiveFactor(trait.medicalPotencyFactor);
                produceMaxHitPointsFactorCache *= PositiveFactor(trait.produceMaxHitPointsFactor);
                produceArmorHeatFactorCache *= PositiveFactor(trait.produceArmorHeatFactor);
                produceColdInsulationFactorCache *= PositiveFactor(trait.produceColdInsulationFactor);
            }
            medicalPotencyFactorCache = Mathf.Clamp(medicalPotencyFactorCache, 0.1f, 5f);
            produceMaxHitPointsFactorCache = Mathf.Clamp(produceMaxHitPointsFactorCache, 0.1f, 5f);
            produceArmorHeatFactorCache = Mathf.Clamp(produceArmorHeatFactorCache, 0.1f, 5f);
            produceColdInsulationFactorCache = Mathf.Clamp(produceColdInsulationFactorCache, 0.1f, 5f);
        }

        private static List<VarietyTraitDef> ResolveTraits(List<string> names)
        {
            List<VarietyTraitDef> result = new List<VarietyTraitDef>();
            if (names == null) return result;
            foreach (string name in names)
            {
                VarietyTraitDef trait = DefDatabase<VarietyTraitDef>.GetNamedSilentFail(name);
                if (trait != null && !result.Contains(trait)) result.Add(trait);
            }
            result.Sort((a, b) => string.Compare(a.label, b.label, System.StringComparison.OrdinalIgnoreCase));
            return result;
        }

        internal IEnumerable<KeyValuePair<string, string>> Sources()
        {
            int count = Mathf.Max(sourceVarietyIds?.Count ?? 0, sourceVarietyNames?.Count ?? 0);
            for (int i = 0; i < count; i++)
            {
                yield return new KeyValuePair<string, string>(
                    i < (sourceVarietyIds?.Count ?? 0) ? sourceVarietyIds[i] : string.Empty,
                    i < (sourceVarietyNames?.Count ?? 0) ? sourceVarietyNames[i] : string.Empty);
            }
        }

        internal IEnumerable<string> StoredTraitDefNames => traitDefNames ?? Enumerable.Empty<string>();
        internal IEnumerable<string> StoredEffectiveTraitDefNames => effectiveTraitDefNames ?? Enumerable.Empty<string>();

        private void NormalizeSources()
        {
            sourceVarietyIds = sourceVarietyIds ?? new List<string>();
            sourceVarietyNames = sourceVarietyNames ?? new List<string>();
            List<KeyValuePair<string, string>> sources = Sources()
                .Where(pair => !pair.Key.NullOrEmpty() || !pair.Value.NullOrEmpty())
                .GroupBy(pair => !pair.Key.NullOrEmpty() ? "id:" + pair.Key : "name:" + pair.Value)
                .Select(group => group.First())
                .OrderBy(pair => pair.Key)
                .ThenBy(pair => pair.Value)
                .ToList();
            sourceVarietyIds = sources.Select(pair => pair.Key ?? string.Empty).ToList();
            sourceVarietyNames = sources.Select(pair => pair.Value ?? string.Empty).ToList();
        }

        private bool SourcesEqual(CompNovelProduceAppearance other)
        {
            int count = Mathf.Max(sourceVarietyIds?.Count ?? 0, sourceVarietyNames?.Count ?? 0);
            int otherCount = Mathf.Max(other?.sourceVarietyIds?.Count ?? 0, other?.sourceVarietyNames?.Count ?? 0);
            if (count != otherCount) return false;
            for (int i = 0; i < count; i++)
            {
                string id = i < (sourceVarietyIds?.Count ?? 0) ? sourceVarietyIds[i] : string.Empty;
                string otherId = i < (other.sourceVarietyIds?.Count ?? 0) ? other.sourceVarietyIds[i] : string.Empty;
                if (!id.NullOrEmpty() || !otherId.NullOrEmpty())
                {
                    if (id != otherId) return false;
                    continue;
                }
                string name = i < (sourceVarietyNames?.Count ?? 0) ? sourceVarietyNames[i] : string.Empty;
                string otherName = i < (other.sourceVarietyNames?.Count ?? 0) ? other.sourceVarietyNames[i] : string.Empty;
                if (name != otherName) return false;
            }
            return true;
        }

        private static float QuantizeNutrition(float value)
        {
            return Mathf.Round(Mathf.Clamp(value, 0.1f, 5f) * 1000f) / 1000f;
        }

        private static bool SequenceEqual(List<string> first, List<string> second)
        {
            int count = first?.Count ?? 0;
            if (count != (second?.Count ?? 0)) return false;
            for (int i = 0; i < count; i++) if (first[i] != second[i]) return false;
            return true;
        }

        private static bool SequenceEqual(List<int> first, List<int> second)
        {
            int count = first?.Count ?? 0;
            if (count != (second?.Count ?? 0)) return false;
            for (int i = 0; i < count; i++) if (first[i] != second[i]) return false;
            return true;
        }

        private static float PositiveFactor(float value) => value <= 0f ? 1f : value;

        private static int Pack(Color32 color)
        {
            return color.r | color.g << 8 | color.b << 16 | color.a << 24;
        }

        private static Color32 Unpack(int packed)
        {
            return new Color32((byte)packed, (byte)(packed >> 8), (byte)(packed >> 16), (byte)(packed >> 24));
        }
    }

    public static class ProduceInheritanceUtility
    {
        public static ProduceInheritanceData FromIngredients(List<Thing> ingredients)
        {
            if (ingredients.NullOrEmpty()) return null;
            List<CompNovelProduceAppearance> qualityComps = ingredients
                .Select(thing => thing?.TryGetComp<CompNovelProduceAppearance>())
                .Where(comp => comp?.HasInheritedQualities == true)
                .ToList();
            if (qualityComps.Count == 0) return null;

            ProduceInheritanceData data = new ProduceInheritanceData();
            foreach (KeyValuePair<string, string> source in qualityComps.SelectMany(comp => comp.Sources())
                .GroupBy(pair => !pair.Key.NullOrEmpty() ? "id:" + pair.Key : "name:" + pair.Value)
                .Select(group => group.First())
                .OrderBy(pair => pair.Key).ThenBy(pair => pair.Value))
            {
                data.sourceVarietyIds.Add(source.Key ?? string.Empty);
                data.sourceVarietyNames.Add(source.Value ?? string.Empty);
            }
            data.traitDefNames.AddRange(qualityComps.SelectMany(comp => comp.StoredTraitDefNames)
                .Select(DefDatabase<VarietyTraitDef>.GetNamedSilentFail)
                .Where(trait => trait != null)
                .Select(trait => trait.defName)
                .Distinct()
                .OrderBy(name => name));
            data.effectiveTraitDefNames.AddRange(qualityComps.SelectMany(comp => comp.StoredEffectiveTraitDefNames)
                .Where(name => !name.NullOrEmpty())
                .Distinct()
                .OrderBy(name => name));

            foreach (IGrouping<ThingDef, Thing> materialGroup in ingredients
                .Where(thing => thing?.def != null && thing.TryGetComp<CompNovelProduceAppearance>()?.HasStoredAppearance == true)
                .GroupBy(thing => thing.def))
            {
                float total = 0f;
                Color weighted = Color.clear;
                foreach (Thing ingredient in materialGroup)
                {
                    CompNovelProduceAppearance comp = ingredient.TryGetComp<CompNovelProduceAppearance>();
                    if (comp == null || !comp.TryGetStoredColor(out Color color)) continue;
                    float weight = Mathf.Max(1, ingredient.stackCount);
                    weighted += color * weight;
                    total += weight;
                }
                if (total > 0f) data.materialColors[materialGroup.Key.defName] = weighted / total;
            }

            float totalWeight = 0f;
            float weightedFactor = 0f;
            foreach (Thing ingredient in ingredients.Where(thing => thing != null))
            {
                float nutrition = Mathf.Max(0f, ingredient.GetStatValue(StatDefOf.Nutrition));
                float weight = nutrition * Mathf.Max(1, ingredient.stackCount);
                if (weight <= 0f) continue;
                float factor = ingredient.TryGetComp<CompNovelProduceAppearance>()?.NutritionFactor ?? 1f;
                totalWeight += weight;
                weightedFactor += weight * factor;
            }
            data.nutritionFactor = totalWeight > 0f ? weightedFactor / totalWeight : 1f;
            return data.HasData ? data : null;
        }

        public static IEnumerable<Thing> ApplyToRecipeProducts(IEnumerable<Thing> products, ProduceInheritanceData data)
        {
            if (products == null) yield break;
            foreach (Thing product in products)
            {
                if (data != null && product != null)
                {
                    Color? materialColor = null;
                    if (product.Stuff != null && data.materialColors.TryGetValue(product.Stuff.defName, out Color color))
                        materialColor = color;
                    product.TryGetComp<CompNovelProduceAppearance>()?.InitializeInherited(data, materialColor);
                }
                yield return product;
            }
        }
    }

    public static class ProduceAppearanceContext
    {
        [System.ThreadStatic]
        private static ThingDef pendingProduct;

        [System.ThreadStatic]
        private static Color pendingColor;

        [System.ThreadStatic]
        private static List<Color> pendingMaskColors;

        [System.ThreadStatic]
        private static CompPlantVariety pendingPlantComp;

        public static void Capture(Plant plant, int yield)
        {
            pendingProduct = null;
            pendingPlantComp = null;
            pendingMaskColors = null;
            if (yield <= 0) return;

            CompPlantVariety comp = plant?.TryGetComp<CompPlantVariety>();
            ThingDef product = plant?.def?.plant?.harvestedThingDef;
            if (comp == null || !comp.HasAnyTraits || product == null) return;

            pendingColor = NovelSeedUtility.ResolveProduceColor(comp);
            pendingMaskColors = NovelSeedUtility.ResolveProduceMaskColors(comp);
            pendingPlantComp = comp;
            pendingProduct = product;
        }

        public static void ApplyIfPending(ThingDef def, Thing thing)
        {
            if (pendingProduct == null || def != pendingProduct) return;
            CompPlantVariety plantComp = pendingPlantComp;
            Color color = pendingColor;
            List<Color> maskColors = pendingMaskColors;
            pendingProduct = null;
            pendingPlantComp = null;
            pendingMaskColors = null;
            thing?.TryGetComp<CompNovelProduceAppearance>()?.InitializeFromPlant(plantComp, color, maskColors);
        }
    }

    [HarmonyPatch(typeof(Thing), nameof(Thing.Graphic), MethodType.Getter)]
    internal static class ThingGraphic_NovelProduceAppearance_Patch
    {
        public static void Postfix(Thing __instance, ref Graphic __result)
        {
            if (__instance is not ThingWithComps thing || __result == null || !NovelProduceDefCache.Contains(thing.def)) return;
            CompNovelProduceAppearance comp = thing.TryGetComp<CompNovelProduceAppearance>();
            if (comp != null && comp.TryGetReplacementGraphic(__result, out Graphic replacement)) __result = replacement;
        }
    }
    [HarmonyPatch(typeof(ThingMaker), nameof(ThingMaker.MakeThing))]
    public static class ThingMaker_MakeThing_ProduceAppearance_Patch
    {
        public static void Postfix(ThingDef def, Thing __result)
        {
            ProduceAppearanceContext.ApplyIfPending(def, __result);
        }
    }

    [HarmonyPatch(typeof(FoodUtility), nameof(FoodUtility.NutritionForEater))]
    public static class FoodUtility_NutritionForEater_NovelProduce_Patch
    {
        public static void Postfix(Thing food, ref float __result)
        {
            CompNovelProduceAppearance comp = food?.TryGetComp<CompNovelProduceAppearance>();
            if (comp?.HasInheritedQualities == true) __result *= comp.NutritionFactor;
        }
    }

    [HarmonyPatch(typeof(GenRecipe), nameof(GenRecipe.MakeRecipeProducts))]
    public static class GenRecipe_MakeRecipeProducts_NovelProduce_Patch
    {
        public static void Postfix(List<Thing> ingredients, ref IEnumerable<Thing> __result)
        {
            ProduceInheritanceData data = ProduceInheritanceUtility.FromIngredients(ingredients);
            if (data != null) __result = ProduceInheritanceUtility.ApplyToRecipeProducts(__result, data);
        }
    }
}
