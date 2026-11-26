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
            int mode = 0; float strength = 0f; bool explicitAllele = false;
            foreach (var trait in comp.ActiveTraits)
            {
                if (!TraitAppearanceCatalog.TryPattern(trait, out int m, out float amount)) continue;
                bool explicitTrait = trait.proceduralPhenotype != ProceduralPhenotype.None;
                if (chosen != null && (explicitAllele && !explicitTrait || explicitAllele == explicitTrait &&
                    (strength > amount || strength == amount && string.CompareOrdinal(chosen.defName, trait.defName) <= 0))) continue;
                chosen = trait; mode = m; strength = amount; explicitAllele = explicitTrait;
            }
            if (chosen == null) return Vector4.zero;
            unchecked
            {
                uint hash = 2166136261;
                foreach (char c in (comp.parent?.def?.defName ?? "") + ":" + chosen.defName)
                    hash = (hash ^ c) * 16777619u;
                return new Vector4(mode, (hash & 65535u) / 65535f, strength, 1f);
            }
        }
    }
}
