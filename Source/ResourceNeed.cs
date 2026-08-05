using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace HorticultureNovelSeeds
{
    internal static class ResourcePaymentUtility
    {
        internal struct PaymentResult
        {
            internal int Consumed;
            internal bool FullyPaid;
        }

        internal static bool CanStartJob(bool needsResource, int requiredCount, bool plantForbidden,
            bool plantReservable, bool resourceAvailable, bool resourceReservable)
        {
            return needsResource && requiredCount > 0 && !plantForbidden && plantReservable
                && resourceAvailable && resourceReservable;
        }

        internal static bool CanSatisfyStack(int stackCount, int requiredCount)
        {
            return requiredCount > 0 && stackCount >= requiredCount;
        }

        internal static bool CanReserveStack(int stackCount, int requiredCount, bool reservable)
        {
            return reservable && CanSatisfyStack(stackCount, requiredCount);
        }

        internal static int ConsumedUnits(int carriedStackCount, int requiredCount)
        {
            return System.Math.Min(System.Math.Max(0, carriedStackCount), System.Math.Max(0, requiredCount));
        }

        internal static PaymentResult EvaluatePayment(bool needsResource, int carriedStackCount, int requiredCount)
        {
            int consumed = needsResource ? ConsumedUnits(carriedStackCount, requiredCount) : 0;
            return new PaymentResult
            {
                Consumed = consumed,
                FullyPaid = needsResource && requiredCount > 0 && consumed >= requiredCount
            };
        }
    }

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
            if (comp?.NeedsResource != true || trait?.requiredResourceDef == null) return false;
            Thing resource = FindResource(pawn, trait.requiredResourceDef, trait.requiredResourceCount);
            return ResourcePaymentUtility.CanStartJob(true, trait.requiredResourceCount,
                plant.IsForbidden(pawn), pawn.CanReserve(plant), resource != null, resource != null && pawn.CanReserve(resource));
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
                TraverseParms.For(pawn), 9999f, thing => ResourcePaymentUtility.CanSatisfyStack(thing.stackCount, count)
                    && !thing.IsForbidden(pawn) && pawn.CanReserve(thing));
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
                ResourcePaymentUtility.PaymentResult payment = ResourcePaymentUtility.EvaluatePayment(true,
                    carried.stackCount, trait.requiredResourceCount);
                if (payment.Consumed <= 0) return;
                carried.SplitOff(payment.Consumed).Destroy();
                if (payment.FullyPaid) comp.SatisfyResource();
                if (payment.FullyPaid) HorticultureEventRouter.FertilizationCompleted(pawn, plant);
            };
            apply.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return apply;
        }
    }
}
