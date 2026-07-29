using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace HorticultureNovelSeeds
{
    public sealed class PlantTagExtension : DefModExtension
    {
        public List<string> addTags = new List<string>();
        public List<string> removeTags = new List<string>();
    }

    public static class PlantTagUtility
    {
        private static readonly Dictionary<ThingDef, List<string>> TagsByPlant = new Dictionary<ThingDef, List<string>>();
        private static readonly StringComparer Comparer = StringComparer.OrdinalIgnoreCase;
        private static readonly string[] CorePurposeTags =
        {
            "Produce", "Food", "Human Food", "Animal Feed", "Ingestible", "Nutrition-Giving",
            "Medicine", "Drug", "Material", "Building Material", "Crafting Material", "Textile",
            "Wood", "Dye", "Chemical", "Decorative", "Health Crop", "Food Crop"
        };

        public static void RebuildCache()
        {
            TagsByPlant.Clear();
            foreach (ThingDef plant in DefDatabase<ThingDef>.AllDefsListForReading.Where(NovelSeedUtility.IsGrowableCrop))
                TagsByPlant[plant] = InferTags(plant);
        }

        public static IReadOnlyList<string> TagsFor(ThingDef plantDef)
        {
            if (plantDef == null) return Array.Empty<string>();
            if (!TagsByPlant.TryGetValue(plantDef, out List<string> tags))
            {
                tags = InferTags(plantDef);
                TagsByPlant[plantDef] = tags;
            }
            return tags;
        }

        public static IReadOnlyList<string> InferredTagsFor(ThingDef plantDef)
        {
            return BuildInferredTags(plantDef);
        }

        public static bool InferredHasTag(ThingDef plantDef, string tag)
        {
            return !tag.NullOrEmpty() && BuildInferredTags(plantDef).Any(existing => Comparer.Equals(existing, tag.Trim()));
        }

        public static IReadOnlyList<string> ConfigurableTags()
        {
            HashSet<string> tags = new HashSet<string>(CorePurposeTags, Comparer);
            foreach (ThingDef plant in DefDatabase<ThingDef>.AllDefsListForReading.Where(NovelSeedUtility.IsGrowableCrop))
                foreach (string tag in BuildInferredTags(plant).Where(IsUserFacingTag)) tags.Add(tag);
            foreach (string tag in HorticultureNovelSeedsMod.Settings?.ConfiguredPlantTags() ?? Enumerable.Empty<string>())
                if (IsUserFacingTag(tag)) tags.Add(tag);
            foreach (VarietyTraitDef trait in DefDatabase<VarietyTraitDef>.AllDefsListForReading)
            {
                foreach (string tag in trait.requiredPlantTags ?? new List<string>()) if (IsUserFacingTag(tag)) tags.Add(tag);
                foreach (string tag in trait.anyPlantTags ?? new List<string>()) if (IsUserFacingTag(tag)) tags.Add(tag);
                foreach (string tag in trait.excludedPlantTags ?? new List<string>()) if (IsUserFacingTag(tag)) tags.Add(tag);
            }
            return tags.OrderBy(tag => tag).ToList();
        }

        public static IReadOnlyList<string> DisplayTagsFor(ThingDef plantDef)
        {
            return TagsFor(plantDef).Where(IsUserFacingTag).OrderBy(tag => tag).ToList();
        }

        public static bool HasTag(ThingDef plantDef, string tag)
        {
            return !tag.NullOrEmpty() && TagsFor(plantDef).Any(existing => Comparer.Equals(existing, tag.Trim()));
        }

        public static bool IsTraitRelevant(ThingDef plantDef, VarietyTraitDef traitDef)
        {
            return traitDef != null;
        }

        public static bool MeetsProduceEffectRequirements(ThingDef plantDef, VarietyTraitDef traitDef)
        {
            if (plantDef == null || traitDef == null) return false;
            VarietyTraitDef root = TraitConfigUtility.Root(traitDef);
            return RequirementsMatch(plantDef, root) && (root == traitDef || RequirementsMatch(plantDef, traitDef));
        }

        private static bool RequirementsMatch(ThingDef plantDef, VarietyTraitDef traitDef)
        {
            if (traitDef == null) return false;
            IReadOnlyList<string> tags = TagsFor(plantDef);
            bool Has(string required) => !required.NullOrEmpty() && tags.Any(tag => Comparer.Equals(tag, required.Trim()));
            if (traitDef.requiredPlantTags?.Any(required => !Has(required)) == true) return false;
            if (traitDef.anyPlantTags?.Count > 0 && !traitDef.anyPlantTags.Any(Has)) return false;
            if (traitDef.excludedPlantTags?.Any(Has) == true) return false;
            return true;
        }

        private static List<string> InferTags(ThingDef plantDef)
        {
            HashSet<string> tags = new HashSet<string>(BuildInferredTags(plantDef), Comparer);
            HorticultureNovelSeedsMod.Settings?.ApplyPlantTagOverrides(plantDef, tags);
            return tags.OrderBy(tag => tag).ToList();
        }

        private static List<string> BuildInferredTags(ThingDef plantDef)
        {
            HashSet<string> tags = new HashSet<string>(Comparer) { "Plant" };
            PlantProperties plant = plantDef?.plant;
            if (plant == null) return tags.ToList();

            Add(tags, "Purpose:" + plant.purpose);
            if (plant.sowTags != null)
                foreach (string sowTag in plant.sowTags.Where(tag => !tag.NullOrEmpty())) Add(tags, "SowTag:" + sowTag);

            ThingDef product = plant.harvestedThingDef;
            if (product != null)
            {
                Add(tags, "Produce");
                Add(tags, "Product:" + product.defName);
                AddProductCategories(tags, product);

                if (product.IsIngestible) Add(tags, "Ingestible");
                if (product.IsNutritionGivingIngestible)
                {
                    Add(tags, "Food");
                    Add(tags, "Nutrition-Giving");
                    if (product.ingestible?.HumanEdible != true) Add(tags, "Animal Feed");
                }
                if (plant.humanFoodPlant || product.ingestible?.HumanEdible == true)
                {
                    Add(tags, "Food");
                    Add(tags, "Human Food");
                }
                AddFoodTypeTags(tags, product.ingestible);

                if (product.IsMedicine) Add(tags, "Medicine");
                if (product.IsDrug || plant.drugForHarvestPurposes) Add(tags, "Drug");
                if (product.IsStuff)
                {
                    Add(tags, "Material");
                    Add(tags, "Crafting Material");
                    bool textile = MatchesMetadata(product, "textile", "fabric", "cloth", "leather", "wool", "synthread", "hyperweave");
                    if (textile) Add(tags, "Textile");
                    else Add(tags, "Building Material");
                }
                else if (!product.IsIngestible) Add(tags, "Crafting Material");
                if (MatchesMetadata(product, "wood", "log", "timber"))
                {
                    Add(tags, "Wood");
                    Add(tags, "Material");
                    Add(tags, "Building Material");
                }
                if (MatchesMetadata(product, "dye", "pigment")) Add(tags, "Dye");
                if (MatchesMetadata(product, "chemfuel", "chemical", "neutroamine", "biofuel")) Add(tags, "Chemical");
            }

            if (plant.purpose == PlantPurpose.Food) Add(tags, "Food Crop");
            if (plant.purpose == PlantPurpose.Health) Add(tags, "Health Crop");
            if (plant.purpose == PlantPurpose.Beauty || NovelSeedUtility.IsFlowerPlant(plantDef)) Add(tags, "Decorative");

            foreach (PlantTagExtension extension in plantDef.modExtensions?.OfType<PlantTagExtension>() ?? Enumerable.Empty<PlantTagExtension>())
            {
                if (extension?.addTags != null)
                    foreach (string tag in extension.addTags) Add(tags, tag);
                if (extension?.removeTags != null)
                    foreach (string tag in extension.removeTags.Where(tag => !tag.NullOrEmpty())) tags.Remove(tag.Trim());
            }
            return tags.OrderBy(tag => tag).ToList();
        }

        private static void AddProductCategories(HashSet<string> tags, ThingDef product)
        {
            HashSet<ThingCategoryDef> categories = new HashSet<ThingCategoryDef>();
            foreach (ThingCategoryDef category in product.thingCategories ?? new List<ThingCategoryDef>())
            {
                for (ThingCategoryDef current = category; current != null && categories.Add(current); current = current.parent)
                    Add(tags, "ProductCategory:" + current.defName);
            }
            foreach (StuffCategoryDef category in product.stuffProps?.categories ?? new List<StuffCategoryDef>())
                Add(tags, "StuffCategory:" + category.defName);
        }

        private static void AddFoodTypeTags(HashSet<string> tags, IngestibleProperties ingestible)
        {
            if (ingestible == null) return;
            foreach (FoodTypeFlags flag in Enum.GetValues(typeof(FoodTypeFlags)))
                if (flag != FoodTypeFlags.None && (ingestible.foodType & flag) == flag) Add(tags, "FoodType:" + flag);
        }

        private static bool MatchesMetadata(ThingDef product, params string[] terms)
        {
            IEnumerable<string> values = new[] { product.defName, product.label }
                .Concat(product.thingCategories?.SelectMany(category => category.Parents.Prepend(category)).Select(category => category.defName) ?? Enumerable.Empty<string>())
                .Concat(product.stuffProps?.categories?.Select(category => category.defName) ?? Enumerable.Empty<string>());
            return values.Where(value => !value.NullOrEmpty()).Any(value => terms.Any(term => value.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0));
        }

        private static bool IsUserFacingTag(string tag)
        {
            return !tag.NullOrEmpty() && tag.IndexOf(':') < 0 && !tag.Equals("Plant", StringComparison.OrdinalIgnoreCase);
        }

        private static void Add(HashSet<string> tags, string tag)
        {
            if (!tag.NullOrEmpty()) tags.Add(tag.Trim());
        }
    }
}