using System.Collections.Generic;
using System.Linq;
using KnowledgeFramework;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace HorticultureNovelSeeds
{
    public sealed class PlantKnowledgeRecord : KnowledgeRecord
    {
        public string cropDefName;
        public int plantsSown;
        public int plantsHarvested;
        public int plantsCut;
        public int plantsFertilized;
        public int seedsDiscovered;
        public int recipesCompleted;

        public PlantKnowledgeRecord() { }

        public PlantKnowledgeRecord(Pawn pawn, ThingDef cropDef) : base(pawn, cropDef?.defName)
        {
            cropDefName = cropDef?.defName;
        }

        public ThingDef CropDef => DefDatabase<ThingDef>.GetNamedSilentFail(cropDefName ?? subjectDefName);
        public KnowledgeRank Rank => PlantKnowledgeUtility.RankFor(experience);

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref cropDefName, "cropDefName");
            Scribe_Values.Look(ref plantsSown, "plantsSown");
            Scribe_Values.Look(ref plantsHarvested, "plantsHarvested");
            Scribe_Values.Look(ref plantsCut, "plantsCut");
            Scribe_Values.Look(ref plantsFertilized, "plantsFertilized");
            Scribe_Values.Look(ref seedsDiscovered, "seedsDiscovered");
            Scribe_Values.Look(ref recipesCompleted, "recipesCompleted");
            if (cropDefName.NullOrEmpty()) cropDefName = subjectDefName;
            subjectDefName = cropDefName;
        }
    }

    public static class PlantKnowledgeUtility
    {
        public const string DomainId = "plants";
        public const float AdeptThreshold = 100f;
        public const float ExpertThreshold = 300f;
        public const float MasterThreshold = 700f;

        public static KnowledgeRank RankFor(float experience) =>
            KnowledgeRanks.ForExperience(experience, AdeptThreshold, ExpertThreshold, MasterThreshold);

        public static float ProgressFor(float experience) =>
            KnowledgeRanks.Progress(experience, AdeptThreshold, ExpertThreshold, MasterThreshold);

        public static float PlantWorkSpeedFactor(Pawn pawn, ThingDef cropDef) =>
            HorticultureKnowledgeAdapter.PlantWorkSpeedFactor(pawn, cropDef);

        public static float CraftingSpeedFactor(Pawn pawn, IEnumerable<Thing> ingredients)
        {
            return HorticultureKnowledgeAdapter.ProduceWorkSpeedFactor(pawn, ingredients);
        }

        public static void RecordSowing(Pawn pawn, ThingDef cropDef)
        {
            Gain(pawn, cropDef, 2f, "sowing");
        }

        public static void RecordPlantWork(Pawn pawn, ThingDef cropDef, PlantDestructionMode mode)
        {
            const PlantDestructionMode harvest = (PlantDestructionMode)2;
            const PlantDestructionMode cut = (PlantDestructionMode)3;
            if (mode == harvest) Gain(pawn, cropDef, 5f, "harvesting");
            else if (mode == cut) Gain(pawn, cropDef, 2f, "cutting");
        }

        public static void RecordFertilizing(Pawn pawn, ThingDef cropDef)
        {
            Gain(pawn, cropDef, 6f, "fertilizing");
        }

        public static void RecordSeedDiscovery(Pawn pawn, ThingDef cropDef)
        {
            Gain(pawn, cropDef, 30f, "discovery");
        }

        public static void RecordProduceRecipe(Pawn pawn, IEnumerable<Thing> ingredients)
        {
            if (pawn == null || ingredients == null) return;
            List<ThingDef> crops = SourceCrops(ingredients);
            for (int i = 0; i < crops.Count; i++) Gain(pawn, crops[i], 4f, "produceRecipe");
        }

        public static float ExperienceFor(Pawn pawn, ThingDef cropDef) =>
            HorticultureKnowledgeAdapter.PersonalKnowledge(pawn, cropDef);

        private static List<ThingDef> SourceCrops(IEnumerable<Thing> ingredients) => ingredients
            .Select(ingredient => ingredient?.TryGetComp<CompNovelProduceAppearance>()?.SourcePlantDef)
            .Where(crop => crop != null).Distinct().ToList();

        private static void Gain(Pawn pawn, ThingDef cropDef, float amount, string reasonId)
        {
            if (pawn?.Faction?.def?.isPlayer != true || cropDef == null || amount <= 0f) return;
            KnowledgeService.Award(new KnowledgeAward
            {
                domainId = DomainId,
                subjectId = cropDef.defName,
                pawn = pawn,
                pawnKnowledge = amount,
                colonyKnowledge = amount,
                expertise = amount,
                reasonId = reasonId,
                source = "HorticultureNovelSeeds"
            });
        }
    }

    public sealed class PlantKnowledgeEffectProvider : IKnowledgeEffectProvider
    {
        public const string PlantWorkSpeed = "plantWorkSpeed";
        public const string ProductWorkSpeed = "productWorkSpeed";
        public string Id => "horticulture-work";
        public string DomainId => PlantKnowledgeUtility.DomainId;

        public float Apply(string effectId, KnowledgeEffectContext context, float value)
        {
            if (effectId != PlantWorkSpeed && effectId != ProductWorkSpeed) return value;
            float bonus = (int)context.pawnRank * 0.03f + (int)context.expertiseRank * 0.02f;
            return value * (1f + Mathf.Clamp(bonus, 0f, 0.15f));
        }
    }

    [HarmonyPatch(typeof(StatWorker), nameof(StatWorker.GetValueUnfinalized))]
    public static class HorticultureRecipeSpeedPatch
    {
        public static void Postfix(StatWorker __instance, StatRequest req, ref float __result)
        {
            if (!(req.Thing is Pawn pawn) || pawn.CurJob?.RecipeDef?.workSpeedStat == null ||
                __instance != pawn.CurJob.RecipeDef.workSpeedStat.Worker) return;
            IEnumerable<Thing> ingredients = pawn.CurJob.targetB.Thing is UnfinishedThing unfinished
                ? unfinished.ingredients
                : pawn.CurJob.placedThings?.Select(item => item.thing);
            __result *= PlantKnowledgeUtility.CraftingSpeedFactor(pawn, ingredients);
        }
    }

    public static class HorticultureSharedKnowledgeIntegration
    {
        private static readonly PlantKnowledgeUiProvider UiProvider = new PlantKnowledgeUiProvider();

        public static void Register()
        {
            KnowledgeDomainRegistry.RegisterDomain(new KnowledgeDomainDefinition(
                PlantKnowledgeUtility.DomainId,
                "Horticulture",
                "Knowledge of plant species and practical horticulture expertise.",
                expertiseEnabled: true,
                knowledgeRanks: new KnowledgeRankThresholds(PlantKnowledgeUtility.AdeptThreshold,
                    PlantKnowledgeUtility.ExpertThreshold, PlantKnowledgeUtility.MasterThreshold),
                expertiseRanks: new KnowledgeRankThresholds(PlantKnowledgeUtility.AdeptThreshold,
                    PlantKnowledgeUtility.ExpertThreshold, PlantKnowledgeUtility.MasterThreshold),
                subjectResolver: ResolveSubject,
                subjectSource: Subjects,
                revealThresholds: new Dictionary<string, float>
                {
                    { "identity", 0f }, { "approximateTraits", PlantKnowledgeUtility.AdeptThreshold },
                    { "exactTraits", PlantKnowledgeUtility.ExpertThreshold },
                    { "inheritancePredictions", PlantKnowledgeUtility.MasterThreshold }
                },
                sortOrder: 30));
            KnowledgeDomainRegistry.RegisterEffect(new PlantKnowledgeEffectProvider());
            KnowledgeDomainRegistry.RegisterUi(UiProvider);
        }

        public static KnowledgeMenuModel Menu(Pawn pawn, bool colony) => UiProvider.Menu(pawn, colony);

        private static KnowledgeSubjectDefinition ResolveSubject(string subjectId)
        {
            ThingDef crop = DefDatabase<ThingDef>.GetNamedSilentFail(subjectId);
            return crop == null ? null : new KnowledgeSubjectDefinition(crop.defName, crop.LabelCap,
                crop.description, crop);
        }

        private static IEnumerable<KnowledgeSubjectDefinition> Subjects() =>
            DefDatabase<ThingDef>.AllDefsListForReading.Where(NovelSeedUtility.IsGrowableCrop)
                .Select(crop => new KnowledgeSubjectDefinition(crop.defName, crop.LabelCap, crop.description, crop));
    }

    public sealed class PlantKnowledgeUiProvider : IKnowledgeUiProvider
    {
        public string DomainId => PlantKnowledgeUtility.DomainId;

        public KnowledgeEntry BioEntry(Pawn pawn)
        {
            IReadOnlyList<KnowledgeSnapshot> records = KnowledgeService.PawnKnowledge(DomainId, pawn);
            if (records.Count == 0) return null;
            KnowledgeSnapshot best = records.OrderByDescending(record => record.experience).First();
            ExpertiseSnapshot expertise = KnowledgeService.GetPawnExpertise(DomainId, pawn);
            ThingDef crop = DefDatabase<ThingDef>.GetNamedSilentFail(best.subjectId);
            return new KnowledgeEntry
            {
                label = "Horticulture",
                rank = expertise.rank,
                progress = expertise.progress,
                summary = records.Count + " crops / " + (crop?.LabelCap.ToString() ?? "missing definition"),
                tooltip = "Horticulture - " + expertise.rank + "\n\nKnowledge grows through completed plant work, discoveries, and recipes using known produce." +
                    "\n\nBest-known crop: " + (crop?.LabelCap.ToString() ?? best.subjectId) +
                    "\n- Plant work speed: +" + (((int)best.rank * 0.03f) + ((int)expertise.rank * 0.02f)).ToStringPercent() +
                    "\n\nKnown crops: " + records.Count +
                    "\nBest crop XP: " + best.experience.ToString("0"),
                openDetails = () => MainTabWindow_CultivarRegistry.OpenKnowledge(pawn)
            };
        }

        public KnowledgeMenuModel Menu(Pawn pawn, bool colony)
        {
            IReadOnlyList<KnowledgeSnapshot> records = colony
                ? KnowledgeService.ColonyKnowledge(DomainId) : KnowledgeService.PawnKnowledge(DomainId, pawn);
            var section = new KnowledgeMenuSection
            {
                id = "plants", label = "Plant Knowledge",
                emptyText = "No horticulture knowledge yet. Complete plant work or discover and use novel plants."
            };
            foreach (KnowledgeSnapshot record in records.OrderBy(value =>
                DefDatabase<ThingDef>.GetNamedSilentFail(value.subjectId)?.label ?? value.subjectId))
            {
                ThingDef crop = DefDatabase<ThingDef>.GetNamedSilentFail(record.subjectId);
                section.rows.Add(new KnowledgeMenuRow
                {
                    label = crop?.LabelCap.ToString() ?? "Missing definition: " + record.subjectId,
                    iconDef = crop,
                    rank = record.rank,
                    progress = record.progress,
                    status = record.rank + " - " + record.experience.ToString("0") + " XP",
                    tooltip = "Sown: " + record.EventCount("sowing") + "\nHarvested: " + record.EventCount("harvesting")
                        + "\nCut: " + record.EventCount("cutting") + "\nFertilized: " + record.EventCount("fertilizing")
                        + "\nDiscoveries: " + record.EventCount("discovery") + "\nProduce recipes: "
                        + record.EventCount("produceRecipe")
                });
            }
            if (colony) return new KnowledgeMenuModel
            {
                title = "Colony Horticulture Knowledge",
                sections = new List<KnowledgeMenuSection> { section }
            };
            ExpertiseSnapshot expertise = KnowledgeService.GetPawnExpertise(DomainId, pawn);
            return new KnowledgeMenuModel
            {
                title = (pawn?.LabelShortCap ?? "Colonist") + " - Horticulture",
                expertiseLabel = "Horticulture expertise",
                expertiseRank = expertise.rank,
                expertiseProgress = expertise.progress,
                sections = new List<KnowledgeMenuSection> { section }
            };
        }
    }
}
