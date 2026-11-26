using System;
using System.Linq;
using Verse;
using E=HorticultureNovelSeeds.ExpressionMath;

namespace HorticultureNovelSeeds
{
    // One independent, compatible ornamental donor addition per successful
    // cross. No new mutation events or mechanical bonuses. Explicitly verify
    // the promoted definition is still mechanical-neutral (including patches).
    internal static class OrnamentalInheritance
    {
        internal const int MaximumAdditions=1;
        internal static bool Enabled(NovelSeedsSettings s,ThingDef def)=>s?.enableBotanicalExpression==true&&s.enableIndependentOrnamentalInheritance&&E.QualifiedSpecies(def?.defName);
        internal static bool IsAppearance(VarietyTraitDef t)=>Pure(t)||NovelSeedUtility.IsCosmeticCrossPollinationTrait(t);
        private static bool Neutral(float value)=>value==1f;
        internal static bool Pure(VarietyTraitDef t)
        {
            if(t==null||!E.Known(t.defName)||!t.balanceValueExplicit||t.balanceValue!=0f)return false;
            return Neutral(t.yieldFactor)&&Neutral(t.growthRateFactor)&&Neutral(t.blightChanceFactor)&&Neutral(t.blightDamageFactor)&&
                Neutral(t.workFactor)&&Neutral(t.maxHitPointsFactor)&&Neutral(t.sowWorkFactor)&&Neutral(t.harvestWorkFactor)&&
                Neutral(t.forageNutritionFactor)&&Neutral(t.fishingYieldFactor)&&Neutral(t.companionGrowthFactor)&&
                Neutral(t.produceArmorHeatFactor)&&Neutral(t.produceColdInsulationFactor)&&Neutral(t.produceMaxHitPointsFactor)&&
                Neutral(t.nutritionFactor)&&Neutral(t.medicalPotencyFactor)&&
                t.coldGrowthOffset==0&&t.heatGrowthOffset==0&&t.beautyOffset==0&&t.produceBeautyOffset==0&&t.sowSkillOffset==0&&t.percentageBonus==0&&
                !t.selfSeeding&&!t.perennial&&!t.perennialColdDormancy&&!t.humongousSpacing&&t.tramplingDamage==0&&
                t.byproductDef==null&&t.resinHediff==null&&t.resinDamage==null&&t.requiredResourceDef==null&&
                t.synergyPlantDef==null&&string.IsNullOrEmpty(t.synergyStat)&&string.IsNullOrEmpty(t.requiredSowTag)&&
                t.joyResinThought==null&&t.thornScratchChance==0&&t.compoundThought==null&&t.compoundHediff==null&&
                !t.inheritToProduce;
        }
    }
}
