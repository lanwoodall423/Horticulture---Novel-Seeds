using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace HorticultureNovelSeeds
{
    public class WorkGiver_FertilizeNovelPlant : WorkGiver_Scanner
    {
        public override PathEndMode PathEndMode => PathEndMode.Touch;
        public override ThingRequest PotentialWorkThingRequest => ThingRequest.ForGroup(ThingRequestGroup.Plant);
        public override Danger MaxPathDanger(Pawn pawn) => Danger.Some;

        public override bool HasJobOnThing(Pawn pawn, Thing thing, bool forced = false)
        {
            Plant plant = thing as Plant;
            CompPlantVariety comp = plant?.TryGetComp<CompPlantVariety>();
            VarietyTraitDef trait = comp?.RequiredResourceTrait;
            if (comp?.NeedsResource != true || trait?.requiredResourceDef == null || plant.IsForbidden(pawn) || !pawn.CanReserve(plant)) return false;
            return FindResource(pawn, trait.requiredResourceDef, trait.requiredResourceCount) != null;
        }

        public override Job JobOnThing(Pawn pawn, Thing thing, bool forced = false)
        {
            Plant plant = thing as Plant;
            VarietyTraitDef trait = plant?.TryGetComp<CompPlantVariety>()?.RequiredResourceTrait;
            Thing resource = trait == null ? null : FindResource(pawn, trait.requiredResourceDef, trait.requiredResourceCount);
            if (resource == null) return null;
            Job job = JobMaker.MakeJob(HNS_DefOf.HNS_FertilizePlant, plant, resource);
            job.count = trait.requiredResourceCount;
            return job;
        }

        private static Thing FindResource(Pawn pawn, ThingDef def, int count)
        {
            return GenClosest.ClosestThingReachable(pawn.Position, pawn.Map, ThingRequest.ForDef(def), PathEndMode.ClosestTouch,
                TraverseParms.For(pawn), 9999f, thing => thing.stackCount >= count && !thing.IsForbidden(pawn) && pawn.CanReserve(thing));
        }
    }

    public class JobDriver_FertilizeNovelPlant : JobDriver
    {
        private const TargetIndex PlantIndex = TargetIndex.A;
        private const TargetIndex ResourceIndex = TargetIndex.B;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.GetTarget(PlantIndex), job, 1, -1, null, errorOnFailed) &&
                   pawn.Reserve(job.GetTarget(ResourceIndex), job, 1, job.count, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedNullOrForbidden(PlantIndex);
            this.FailOnDestroyedNullOrForbidden(ResourceIndex);
            yield return Toils_Goto.GotoThing(ResourceIndex, PathEndMode.ClosestTouch);
            yield return Toils_Haul.StartCarryThing(ResourceIndex, false, true, false);
            yield return Toils_Goto.GotoThing(PlantIndex, PathEndMode.Touch);
            yield return Toils_General.Wait(180).WithProgressBarToilDelay(PlantIndex);
            Toil apply = ToilMaker.MakeToil("ApplyNovelPlantResource");
            apply.initAction = delegate
            {
                Plant plant = job.GetTarget(PlantIndex).Thing as Plant;
                CompPlantVariety comp = plant?.TryGetComp<CompPlantVariety>();
                VarietyTraitDef trait = comp?.RequiredResourceTrait;
                Thing carried = pawn.carryTracker.CarriedThing;
                if (comp?.NeedsResource != true || trait == null || carried == null || carried.def != trait.requiredResourceDef) return;
                int consumed = System.Math.Min(carried.stackCount, trait.requiredResourceCount);
                carried.SplitOff(consumed).Destroy();
                if (consumed >= trait.requiredResourceCount) comp.SatisfyResource();
                if (consumed >= trait.requiredResourceCount) HorticultureEventRouter.FertilizationCompleted(pawn, plant);
            };
            apply.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return apply;
        }
    }
}
