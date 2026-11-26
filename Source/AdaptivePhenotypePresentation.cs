using System.Collections.Generic;
using RimWorld;

namespace HorticultureNovelSeeds
{
    internal static class AdaptivePhenotypePresentation
    {
        internal static string Explain(CompPlantVariety c)
        {
            if(c==null||c.PendingDiscovery||!HorticulturePresentationPolicy.IsDocumented(c.Variety)||c.VisualObservation==null)return null;
            var f=c.VisualObservation.Functional;var p=c.parent as Plant;
            var lines=new List<string>();
            if(f.Has(AdaptivePhenotypeMath.Cue.Yield)&&p.Growth>=.56f)lines.Add("Fuller reproductive outlines accompany High-Yield. Actual harvest depends on maturity, health and inherited modifiers; visible organs are not yield units.");
            if(f.Has(AdaptivePhenotypeMath.Cue.Cold))lines.Add("Cool margins are an inherited visual tendency of Cold-Resistant, not a reading of the current temperature.");
            if(f.Has(AdaptivePhenotypeMath.Cue.Heat))lines.Add("Warm cuticle highlights accompany Heat-Resistant, not heat damage.");
            if(f.Has(AdaptivePhenotypeMath.Cue.Resource))lines.Add(f.Blocked?"Ochre margins correspond to an unmet resource requirement; growth is gated until delivery.":"The required resource has been supplied; the temporary foliage cue is absent.");
            if(f.Has(AdaptivePhenotypeMath.Cue.Perennial)&&f.Regrown)lines.Add("Bronzed lower growth and fresh upper tissue reflect a recorded harvest cycle. Perennial regrowth uses the existing harvest rules.");
            return string.Join("\n",lines);
        }
    }
}
