using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace HorticultureNovelSeeds
{
    public class CompProperties_PlantVariety : CompProperties
    {
        public CompProperties_PlantVariety()
        {
            compClass = typeof(CompPlantVariety);
        }
    }

    public class CompPlantVariety : ThingComp
    {
        private string varietyId;
        private List<VarietyTraitDef> transientTraits;
        private bool pendingDiscovery;
        private bool resourceSatisfied;
        private bool saveSeedsRequested;
        private string crossPollinationParentVarietyId;
        [Unsaved(false)] private List<VarietyTraitDef> activeTraitsCache;
        [Unsaved(false)] private VarietyTraitDef requiredResourceTraitCache;
        [Unsaved(false)] private bool hasSelfSeedingCache;
        [Unsaved(false)] private bool hasHumongousSpacingCache;
        [Unsaved(false)] private bool hasPerennialDormancyCache;
        [Unsaved(false)] private float dormantGrowthFactorCache;
        [Unsaved(false)] private float tramplingDamageCache;
        [Unsaved(false)] private float beautyOffsetCache;
        [Unsaved(false)] private float blightChanceFactorCache;
        [Unsaved(false)] private float blightDamageFactorCache;
        [Unsaved(false)] private float workFactorCache;
        [Unsaved(false)] private float harvestWorkFactorCache;
        [Unsaved(false)] private float growthRateFactorCache;
        [Unsaved(false)] private float maxHitPointsFactorCache;
        [Unsaved(false)] private float forageNutritionFactorCache;
        [Unsaved(false)] private float coldGrowthOffsetCache;
        [Unsaved(false)] private float heatGrowthOffsetCache;
        [Unsaved(false)] private bool selfSeededAtMaturity;

        public bool HasAnyTraits => ActiveTraits.Count > 0;
        public bool PendingDiscovery => pendingDiscovery && transientTraits != null && transientTraits.Count > 0;
        public bool SaveSeedsRequested => PendingDiscovery && saveSeedsRequested;
        public VarietyRecord CrossPollinationParent => GameComponent_NovelSeeds.Instance?.GetVariety(crossPollinationParentVarietyId);
        public bool CrossPollinated => PendingDiscovery && !crossPollinationParentVarietyId.NullOrEmpty();
        public List<string> CrossPollinationParentIds => CrossPollinated
            ? new[] { varietyId, crossPollinationParentVarietyId }.Where(id => !id.NullOrEmpty()).Distinct().ToList()
            : new List<string>();
        public string DisplayVarietyName => CrossPollinated
            ? "HNS_CrossPollinatedLabel".Translate(CrossPollinationParent?.Label ?? "HNS_UnknownVariety".Translate()).ToString()
            : Variety?.Label ?? "HNS_PendingVariety".Translate().ToString();
        public List<VarietyTraitDef> DiscoveryTraits => PendingDiscovery ? ActiveTraits.ToList() : transientTraits;
        public VarietyTraitDef RequiredResourceTrait { get { EnsureTraitCache(); return requiredResourceTraitCache; } }
        public bool HasSelfSeeding { get { EnsureTraitCache(); return hasSelfSeedingCache; } }
        public bool HasHumongousSpacing { get { EnsureTraitCache(); return hasHumongousSpacingCache; } }
        public bool HasPerennialDormancy { get { EnsureTraitCache(); return hasPerennialDormancyCache; } }
        public float DormantGrowthFactor { get { EnsureTraitCache(); return dormantGrowthFactorCache; } }
        public float TramplingDamage { get { EnsureTraitCache(); return tramplingDamageCache; } }
        public float BeautyOffset { get { EnsureTraitCache(); return beautyOffsetCache; } }
        public float BlightChanceFactor { get { EnsureTraitCache(); return blightChanceFactorCache; } }
        public float BlightDamageFactor { get { EnsureTraitCache(); return blightDamageFactorCache; } }
        public float WorkFactor { get { EnsureTraitCache(); return workFactorCache; } }
        public float HarvestWorkFactor { get { EnsureTraitCache(); return harvestWorkFactorCache; } }
        public float GrowthRateFactor { get { EnsureTraitCache(); return growthRateFactorCache; } }
        public float MaxHitPointsFactor { get { EnsureTraitCache(); return maxHitPointsFactorCache; } }
        public float ForageNutritionFactor { get { EnsureTraitCache(); return forageNutritionFactorCache; } }
        public float ColdGrowthOffset { get { EnsureTraitCache(); return coldGrowthOffsetCache; } }
        public float HeatGrowthOffset { get { EnsureTraitCache(); return heatGrowthOffsetCache; } }
        public bool NeedsResource => RequiredResourceTrait != null && !resourceSatisfied;
        public void SatisfyResource() { resourceSatisfied = true; }
        public bool TryMarkSelfSeededAtMaturity(bool mature)
        {
            if (!mature) { selfSeededAtMaturity = false; return false; }
            if (selfSeededAtMaturity) return false;
            selfSeededAtMaturity = true;
            return true;
        }

        public IReadOnlyList<VarietyTraitDef> ActiveTraits
        {
            get
            {
                EnsureTraitCache();
                return activeTraitsCache;
            }
        }

        public VarietyRecord Variety => GameComponent_NovelSeeds.Instance?.GetVariety(varietyId);
        public string VarietyId => varietyId;

        public void SetVariety(VarietyRecord variety)
        {
            varietyId = variety?.id;
            transientTraits = null;
            pendingDiscovery = false;
            resourceSatisfied = false;
            saveSeedsRequested = false;
            crossPollinationParentVarietyId = null;
            InvalidateTraitCache();
            ExpandedTraitUtility.RegisterSelfSeedingPlant(this);
        }

        public void SetPendingTraits(List<VarietyTraitDef> traits)
        {
            varietyId = null;
            AddPendingTraits(traits);
        }

        public void AddPendingTraits(List<VarietyTraitDef> traits)
        {
            transientTraits = traits?.Where(t => t != null).Distinct().ToList();
            pendingDiscovery = transientTraits != null && transientTraits.Count > 0;
            resourceSatisfied = false;
            saveSeedsRequested = false;
            crossPollinationParentVarietyId = null;
            InvalidateTraitCache();
            ExpandedTraitUtility.RegisterSelfSeedingPlant(this);
        }

        public void SetCrossPollinatedTraits(List<VarietyTraitDef> traits, VarietyRecord donor)
        {
            AddPendingTraits(traits);
            crossPollinationParentVarietyId = PendingDiscovery ? donor?.id : null;
        }

        public void ClearPendingDiscovery()
        {
            pendingDiscovery = false;
            saveSeedsRequested = false;
            crossPollinationParentVarietyId = null;
        }

        private void RequestSaveSeeds()
        {
            Plant plant = parent as Plant;
            if (!PendingDiscovery || plant?.Spawned != true || plant.Growth < 0.999f || plant.Blighted)
            {
                return;
            }

            saveSeedsRequested = true;
            DesignationDef designationDef = plant.HarvestableNow ? DesignationDefOf.HarvestPlant : DesignationDefOf.CutPlant;
            DesignationManager designations = plant.Map.designationManager;
            if (designations.DesignationOn(plant, designationDef) == null)
            {
                designations.AddDesignation(new Designation(plant, designationDef));
            }
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo gizmo in base.CompGetGizmosExtra())
            {
                yield return gizmo;
            }

            Plant plant = parent as Plant;
            if (!PendingDiscovery || plant?.Spawned != true || plant.Growth < 0.999f || plant.Blighted)
            {
                yield break;
            }

            Command_Action command = new Command_Action
            {
                defaultLabel = "HNS_SaveSeeds".Translate(),
                defaultDesc = "HNS_SaveSeedsDesc".Translate(),
                icon = ContentFinder<Texture2D>.Get((plant.HarvestableNow ? DesignationDefOf.HarvestPlant : DesignationDefOf.CutPlant).texturePath),
                action = RequestSaveSeeds
            };
            if (SaveSeedsRequested)
            {
                command.Disable("HNS_SaveSeedsRequested".Translate());
            }
            yield return command;
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref varietyId, "varietyId");
            Scribe_Collections.Look(ref transientTraits, "transientTraits", LookMode.Def);
            Scribe_Values.Look(ref pendingDiscovery, "pendingDiscovery", false);
            Scribe_Values.Look(ref resourceSatisfied, "resourceSatisfied", false);
            Scribe_Values.Look(ref saveSeedsRequested, "saveSeedsRequested", false);
            Scribe_Values.Look(ref crossPollinationParentVarietyId, "crossPollinationParentVarietyId");
            if (Scribe.mode == LoadSaveMode.PostLoadInit && transientTraits == null)
            {
                transientTraits = new List<VarietyTraitDef>();
            }
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                InvalidateTraitCache();
                ExpandedTraitUtility.RegisterSelfSeedingPlant(this);
            }
        }

        private void InvalidateTraitCache()
        {
            activeTraitsCache = null;
            requiredResourceTraitCache = null;
        }

        private void EnsureTraitCache()
        {
            if (activeTraitsCache != null) return;
            activeTraitsCache = new List<VarietyTraitDef>();
            VarietyRecord variety = Variety;
            if (variety?.traits != null)
            {
                foreach (VarietyTraitDef trait in variety.traits)
                {
                    if (trait == null) continue;
                    bool replaced = false;
                    if (transientTraits != null)
                    {
                        foreach (VarietyTraitDef pending in transientTraits)
                        {
                            if (pending != null && NovelSeedUtility.SameConfigGroup(trait, pending)) { replaced = true; break; }
                        }
                    }
                    if (!replaced) activeTraitsCache.Add(trait);
                }
            }
            if (transientTraits != null)
            {
                foreach (VarietyTraitDef trait in transientTraits)
                    if (trait != null && !activeTraitsCache.Contains(trait)) activeTraitsCache.Add(trait);
            }

            hasSelfSeedingCache = false;
            hasHumongousSpacingCache = false;
            hasPerennialDormancyCache = false;
            dormantGrowthFactorCache = 0f;
            tramplingDamageCache = 0f;
            beautyOffsetCache = 0f;
            blightChanceFactorCache = 1f;
            blightDamageFactorCache = 1f;
            workFactorCache = 1f;
            harvestWorkFactorCache = 1f;
            growthRateFactorCache = 1f;
            maxHitPointsFactorCache = 1f;
            forageNutritionFactorCache = 1f;
            coldGrowthOffsetCache = 0f;
            heatGrowthOffsetCache = 0f;
            foreach (VarietyTraitDef trait in activeTraitsCache)
            {
                if (requiredResourceTraitCache == null && trait.requiredResourceDef != null) requiredResourceTraitCache = trait;
                hasSelfSeedingCache |= trait.selfSeeding;
                hasHumongousSpacingCache |= trait.humongousSpacing;
                if (trait.perennialColdDormancy)
                {
                    hasPerennialDormancyCache = true;
                    dormantGrowthFactorCache = Mathf.Max(dormantGrowthFactorCache, trait.dormantGrowthFactor > 0f ? trait.dormantGrowthFactor : 0.01f);
                }
                if (trait.tramplingDamage > 0f) tramplingDamageCache += trait.tramplingDamage;
                beautyOffsetCache += trait.beautyOffset;
                blightChanceFactorCache *= trait.blightChanceFactor <= 0f ? 1f : trait.blightChanceFactor;
                blightDamageFactorCache *= trait.blightDamageFactor <= 0f ? 1f : trait.blightDamageFactor;
                workFactorCache *= trait.workFactor <= 0f ? 1f : trait.workFactor;
                harvestWorkFactorCache *= trait.harvestWorkFactor <= 0f ? 1f : trait.harvestWorkFactor;
                growthRateFactorCache *= trait.growthRateFactor <= 0f ? 1f : trait.growthRateFactor;
                maxHitPointsFactorCache *= trait.maxHitPointsFactor <= 0f ? 1f : trait.maxHitPointsFactor;
                forageNutritionFactorCache *= trait.forageNutritionFactor <= 0f ? 1f : trait.forageNutritionFactor;
                coldGrowthOffsetCache += trait.coldGrowthOffset;
                heatGrowthOffsetCache += trait.heatGrowthOffset;
            }
            workFactorCache = Mathf.Max(0.05f, workFactorCache);
            harvestWorkFactorCache = Mathf.Max(0.05f, harvestWorkFactorCache * workFactorCache);
            growthRateFactorCache = Mathf.Max(0.05f, growthRateFactorCache);
            maxHitPointsFactorCache = Mathf.Max(0.05f, maxHitPointsFactorCache);
            forageNutritionFactorCache = Mathf.Max(0.05f, forageNutritionFactorCache);
        }

        public override string TransformLabel(string label)
        {
            if (!HasAnyTraits)
            {
                return base.TransformLabel(label);
            }
            VarietyRecord variety = Variety;
            string prefix = DisplayVarietyName;
            if (!CrossPollinated && variety != null && PendingDiscovery)
            {
                prefix = "HNS_EvolvingVarietyLabel".Translate(prefix);
            }
            return prefix + " " + label;
        }

        public override string CompInspectStringExtra()
        {
            if (!HasAnyTraits)
            {
                return null;
            }
            VarietyRecord variety = Variety;
            string name = DisplayVarietyName;
            string text = "HNS_PlantVariety".Translate() + ": " + name + "\n" + "HNS_Traits".Translate() + ": " + NovelSeedUtility.TraitSummary(ActiveTraits);
            if (CrossPollinated)
            {
                text += "\n" + "HNS_CrossPollinatedWith".Translate(CrossPollinationParent?.Label ?? "HNS_UnknownVariety".Translate());
            }
            else if (variety != null && PendingDiscovery)
            {
                text += "\n" + "HNS_PendingAdditionalTraits".Translate(NovelSeedUtility.TraitSummary(transientTraits));
            }
            if (NeedsResource)
            {
                VarietyTraitDef need = RequiredResourceTrait;
                text += "\n" + "HNS_NeedsResource".Translate(need.requiredResourceCount, need.requiredResourceDef.LabelCap);
            }
            return text;
        }
    }
    public class CompProperties_NovelSeedPack : CompProperties
    {
        public CompProperties_NovelSeedPack()
        {
            compClass = typeof(CompNovelSeedPack);
        }
    }

    public class CompNovelSeedPack : ThingComp
    {
        private ThingDef cropDef;
        private List<VarietyTraitDef> traits;
        private List<string> parentVarietyIds;
        private string originKind = "mutation";

        public ThingDef CropDef => cropDef;
        public List<VarietyTraitDef> Traits => traits;
        public string OriginKind => originKind.NullOrEmpty() ? "mutation" : originKind;
        public bool Valid => cropDef != null && traits != null && traits.Count > 0;

        public void Initialize(ThingDef crop, List<VarietyTraitDef> varietyTraits, IEnumerable<string> lineageParentIds = null,
            string discoveryOrigin = null)
        {
            cropDef = crop;
            traits = varietyTraits?.Where(t => t != null).Distinct().ToList() ?? new List<VarietyTraitDef>();
            parentVarietyIds = lineageParentIds?.Where(id => !id.NullOrEmpty()).Distinct().ToList() ?? new List<string>();
            originKind = discoveryOrigin.NullOrEmpty() ? (parentVarietyIds.Count > 0 ? "cross-pollination" : "mutation") : discoveryOrigin;
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Defs.Look(ref cropDef, "cropDef");
            Scribe_Collections.Look(ref traits, "traits", LookMode.Def);
            Scribe_Collections.Look(ref parentVarietyIds, "parentVarietyIds", LookMode.Value);
            Scribe_Values.Look(ref originKind, "originKind", "mutation");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (traits == null) traits = new List<VarietyTraitDef>();
                if (parentVarietyIds == null) parentVarietyIds = new List<string>();
                if (originKind.NullOrEmpty()) originKind = parentVarietyIds.Count > 0 ? "cross-pollination" : "mutation";
            }
        }

        public override IEnumerable<FloatMenuOption> CompFloatMenuOptions(Pawn pawn)
        {
            foreach (FloatMenuOption option in base.CompFloatMenuOptions(pawn))
            {
                yield return option;
            }

            if (!Valid)
            {
                yield return new FloatMenuOption("HNS_InvalidSeedPack".Translate(), null);
                yield break;
            }

            VarietyRecord existing = GameComponent_NovelSeeds.Instance?.FindMatchingVariety(cropDef, traits);
            if (existing != null)
            {
                yield return new FloatMenuOption("HNS_AlreadyUnlocked".Translate(existing.Label), null);
                yield break;
            }

            yield return new FloatMenuOption("HNS_NameAndUnlockVariety".Translate(cropDef.label), delegate
            {
                Job job = JobMaker.MakeJob(HNS_DefOf.HNS_UnlockVariety, parent);
                pawn.jobs.TryTakeOrderedJob(job);
            });
        }

        public override string TransformLabel(string label)
        {
            if (cropDef != null)
            {
                return "HNS_NovelSeedPackLabel".Translate(cropDef.label);
            }
            return base.TransformLabel(label);
        }

        public override string CompInspectStringExtra()
        {
            if (!Valid)
            {
                return "HNS_InvalidSeedPack".Translate();
            }
            return cropDef.LabelCap + "\n" + "HNS_Traits".Translate() + ": " + NovelSeedUtility.TraitSummary(traits);
        }

        public void OpenNamingDialog(Pawn pawn)
        {
            if (!Valid || parent.Destroyed)
            {
                return;
            }
            Find.WindowStack.Add(new Dialog_NameVariety(this, pawn));
        }

        public void UnlockWithName(string varietyName, Pawn discoverer)
        {
            if (!Valid || parent.Destroyed)
            {
                return;
            }
            VarietyRecord variety = GameComponent_NovelSeeds.Instance.UnlockVariety(cropDef, traits, varietyName.Trim(), parentVarietyIds, false,
                discoverer, OriginKind);
            HorticultureEventRouter.CultivarDocumented(discoverer, variety);
            Find.LetterStack.ReceiveLetter("HNS_VarietyUnlocked".Translate(variety.Label), "HNS_VarietyUnlockedDesc".Translate(variety.Label, cropDef.label), LetterDefOf.PositiveEvent);
            parent.Destroy();
        }
    }

    public class JobDriver_UnlockVariety : JobDriver
    {
        private CompNovelSeedPack Comp => TargetA.Thing?.TryGetComp<CompNovelSeedPack>();

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(TargetA, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            Toil prompt = ToilMaker.MakeToil("PromptForVarietyName");
            prompt.initAction = delegate
            {
                Comp?.OpenNamingDialog(pawn);
            };
            prompt.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return prompt;
        }
    }

    public class Dialog_NameVariety : Window
    {
        private readonly CompNovelSeedPack comp;
        private readonly Pawn discoverer;
        private string varietyName;

        public override Vector2 InitialSize => new Vector2(560f, 360f);

        public Dialog_NameVariety(CompNovelSeedPack comp, Pawn discoverer)
        {
            this.comp = comp;
            this.discoverer = discoverer;
            forcePause = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = false;
            doCloseX = true;
            closeOnAccept = false;
            forceCatchAcceptAndCancelEventEvenIfUnfocused = true;
            varietyName = string.Empty;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 36f), "HNS_NameDialogTitle".Translate());
            Text.Font = GameFont.Small;

            Rect body = new Rect(inRect.x, inRect.y + 48f, inRect.width, inRect.height - 104f);
            Widgets.Label(new Rect(body.x, body.y, body.width, 24f), "HNS_NameDialogPrompt".Translate(comp.CropDef.label));
            Widgets.Label(new Rect(body.x, body.y + 32f, body.width, 48f), "HNS_Traits".Translate() + ": " + NovelSeedUtility.TraitSummary(comp.Traits));
            varietyName = Widgets.TextField(new Rect(body.x, body.y + 92f, body.width, 32f), varietyName);

            Rect cancel = new Rect(inRect.xMax - 220f, inRect.yMax - 42f, 100f, 32f);
            Rect confirm = new Rect(inRect.xMax - 110f, inRect.yMax - 42f, 100f, 32f);
            if (Widgets.ButtonText(cancel, "HNS_NameDialogCancel".Translate()))
            {
                Close();
            }
            if (Widgets.ButtonText(confirm, "HNS_NameDialogConfirm".Translate()))
            {
                TryConfirm();
            }
        }

        public override void OnAcceptKeyPressed()
        {
            TryConfirm();
        }

        private void TryConfirm()
        {
            string trimmed = varietyName.Trim();
            if (!trimmed.NullOrEmpty())
            {
                comp.UnlockWithName(trimmed, discoverer);
                Close();
            }
        }

        private static string SuggestedName(CompNovelSeedPack comp)
        {
            if (comp?.CropDef == null)
            {
                return "New Variety";
            }
            VarietyTraitDef firstPositive = comp.Traits?.FirstOrDefault(t => HorticultureNovelSeedsMod.Settings?.TraitHasTag(t, "Positive") == true);
            VarietyTraitDef firstTrait = firstPositive ?? comp.Traits?.FirstOrDefault();
            return firstTrait == null ? comp.CropDef.label.CapitalizeFirst() : firstTrait.label + " " + comp.CropDef.label.CapitalizeFirst();
        }
    }

    public class Dialog_RenameVariety : Window
    {
        private readonly VarietyRecord variety;
        private string varietyName;

        public override Vector2 InitialSize => new Vector2(560f, 300f);

        public Dialog_RenameVariety(VarietyRecord variety)
        {
            this.variety = variety;
            forcePause = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = false;
            doCloseX = true;
            closeOnAccept = false;
            forceCatchAcceptAndCancelEventEvenIfUnfocused = true;
            varietyName = variety?.Label ?? string.Empty;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 36f), "HNS_RenameDialogTitle".Translate());
            Text.Font = GameFont.Small;

            string cropLabel = variety?.cropDef?.label ?? "plant";
            Rect body = new Rect(inRect.x, inRect.y + 48f, inRect.width, inRect.height - 104f);
            Widgets.Label(new Rect(body.x, body.y, body.width, 24f), "HNS_RenameDialogPrompt".Translate(cropLabel));
            varietyName = Widgets.TextField(new Rect(body.x, body.y + 42f, body.width, 32f), varietyName);

            Rect cancel = new Rect(inRect.xMax - 220f, inRect.yMax - 42f, 100f, 32f);
            Rect confirm = new Rect(inRect.xMax - 110f, inRect.yMax - 42f, 100f, 32f);
            if (Widgets.ButtonText(cancel, "HNS_NameDialogCancel".Translate()))
            {
                Close();
            }
            if (Widgets.ButtonText(confirm, "HNS_RenameDialogConfirm".Translate()))
            {
                TryConfirm();
            }
        }

        public override void OnAcceptKeyPressed()
        {
            TryConfirm();
        }

        private void TryConfirm()
        {
            string trimmed = varietyName.Trim();
            if (!trimmed.NullOrEmpty())
            {
                GameComponent_NovelSeeds.Instance?.RenameVariety(variety, trimmed);
                Close();
            }
        }
    }
}

