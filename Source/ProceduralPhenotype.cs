using System;
using Verse;
using UnityEngine;

namespace HorticultureNovelSeeds
{
    public enum ProceduralPhenotype
    {
        None = 0, Variegated = 1, Anthocyanin = 2, MoonFrost = 3, GoldFreckled = 4, AuroraVeins = 5
    }

    // Stable species/allele seed; no per-instance materials, random tick state,
    // baked textures or manual anatomy. Genetic conflicts resolve deterministically.
    internal static class ProceduralPhenotypeUtility
    {
        internal static Vector4 Resolve(CompPlantVariety comp)
        {
            if (comp?.ActiveTraits == null) return Vector4.zero;
            VarietyTraitDef chosen = null;
            foreach (var trait in comp.ActiveTraits)
                if (trait != null && !trait.produceOnlyVisual &&
                    trait.proceduralPhenotype >= ProceduralPhenotype.Variegated &&
                    trait.proceduralPhenotype <= ProceduralPhenotype.AuroraVeins &&
                    (chosen == null || string.CompareOrdinal(trait.defName, chosen.defName) < 0))
                    chosen = trait;
            if (chosen == null) return Vector4.zero;
            unchecked
            {
                uint hash = 2166136261;
                foreach (char c in (comp.parent?.def?.defName ?? "") + ":" + chosen.defName)
                    hash = (hash ^ c) * 16777619u;
                return new Vector4((int)chosen.proceduralPhenotype, (hash & 65535u) / 65535f,
                    Mathf.Clamp01(float.IsFinite(chosen.proceduralStrength) ? chosen.proceduralStrength : 0f), 1f);
            }
        }
    }
}
