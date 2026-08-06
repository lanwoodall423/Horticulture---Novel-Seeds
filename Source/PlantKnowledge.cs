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
        // Retained for save/consumer compatibility. New framework data uses the namespaced domain.
        public const string DomainId = HorticultureKnowledgeContract.LegacyDomainId;
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
            HorticultureKnowledgeAdapter.RecordLegacyGain(pawn, cropDef, amount, reasonId);
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
        public static void Register() => HorticultureKnowledgeAdapter.Register();

        public static KnowledgeMenuModel Menu(Pawn pawn, bool colony) => HorticultureKnowledgeAdapter.Menu(pawn, colony);
    }

    public sealed class PlantKnowledgeUiProvider : IKnowledgeUiProvider
    {
        public string DomainId => PlantKnowledgeUtility.DomainId;

        public KnowledgeEntry BioEntry(Pawn pawn)
        {
            return HorticultureKnowledgeAdapter.BioEntry(pawn);
        }

        public KnowledgeMenuModel Menu(Pawn pawn, bool colony)
        {
            return HorticultureKnowledgeAdapter.Menu(pawn, colony);
        }
    }
}
