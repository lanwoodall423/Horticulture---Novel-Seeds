using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace HorticultureNovelSeeds
{
    internal static class BreedingStudio
    {
        internal sealed record Candidate(VarietyTraitDef Trait,string Status,bool Ornamental);
        internal sealed record Plan(VarietyRecord Seed,VarietyRecord Donor,Candidate[] Candidates,VarietyTraitDef[] Example,bool Independent,int Cap,float Chance,float DonorGrowth);
        internal static bool ValidParent(VarietyRecord v)=>HorticulturePresentationPolicy.IsDocumented(v)&&!v.registryArchived;
        internal static bool ValidPair(VarietyRecord a,VarietyRecord b)=>ValidParent(a)&&ValidParent(b)&&a!=b&&a.cropDef==b.cropDef;
        internal static Plan Describe(VarietyRecord a,VarietyRecord b)
        {
            if(!ValidPair(a,b))return null;
            var settings=HorticultureNovelSeedsMod.Settings;
            var seed=HorticulturePresentationPolicy.ForCultivar(a).AuthorizedTraits;
            var donor=HorticulturePresentationPolicy.ForCultivar(b).AuthorizedTraits;
            var rows=donor.Select(t=>new Candidate(t,NovelSeedUtility.BreedingCandidateStatus(t,seed,a.cropDef,settings),OrnamentalInheritance.IsAppearance(t))).ToArray();
            bool independent=OrnamentalInheritance.Enabled(settings,a.cropDef);
            int cap=settings?.MaxCrossPollinationTraits??3;
            // Only depict a structurally possible example for a simple, fully qualified
            // cross; no random sampling, mutation, generated trait defs or probabilities.
            var possible=rows.Where(r=>r.Status=="Eligible candidate").Select(r=>r.Trait).ToArray();
            var example=seed.ToList();
            if(rows.Any(r=>r.Status=="Special color/nutrition rules"))example.Clear();
            else
            {
                var first=possible.FirstOrDefault(t=>!independent||!OrnamentalInheritance.Pure(t));
                if(first!=null&&cap>0)example.Add(first);
                if(independent)
                {
                    var decorative=possible.FirstOrDefault(t=>OrnamentalInheritance.Pure(t)&&NovelSeedUtility.BreedingCandidateStatus(t,example,a.cropDef,settings)=="Eligible candidate");
                    if(decorative!=null)example.Add(decorative);
                }
                if(example.Count==seed.Count)example.Clear();
            }
            return new(a,b,rows,example.ToArray(),independent,cap,settings?.CrossPollinationChanceFor(a.cropDef)??NovelSeedUtility.DefaultCrossPollinationChance,settings?.MinimumDonorGrowth??NovelSeedUtility.DefaultMinimumDonorGrowth);
        }
        internal static bool CanApply(Zone_Growing zone,VarietyRecord a,VarietyRecord b)
        {
            if(!ValidPair(a,b)||zone==null||zone.Map!=Find.CurrentMap||zone.Map?.IsPlayerHome!=true||
                !zone.Map.zoneManager.AllZones.Contains(zone)||zone.GetPlantDefToGrow()!=a.cropDef||zone.Cells.Count==0)return false;
            var growers=new List<IPlantToGrowSettable>{zone};
            return ExpandedTraitUtility.VarietyMatchesGrowers(a,growers)&&ExpandedTraitUtility.VarietyMatchesGrowers(b,growers);
        }
        internal static bool Apply(Zone_Growing zone,VarietyRecord a,VarietyRecord b)
        {
            if(!CanApply(zone,a,b))return false;
            PlantVarietySelectionUtility.SelectBreedingMix(new List<IPlantToGrowSettable>{zone},a.cropDef,new[]{a,b});
            FieldGuideIndex.RequestRefresh();
            return GameComponent_NovelSeeds.Instance.BreedingVarietiesFor(zone).Select(v=>v.id).ToHashSet().SetEquals(new[]{a.id,b.id});
        }
        internal static VarietyRecord[] Outcomes(VarietyRecord a,VarietyRecord b,IEnumerable<VarietyRecord> all)
        {
            if(!ValidPair(a,b))return Array.Empty<VarietyRecord>();
            return all.Where(v=>HorticulturePresentationPolicy.IsDocumented(v)&&v.cropDef==a.cropDef&&v.parentVarietyIds?.Contains(a.id)==true&&v.parentVarietyIds.Contains(b.id))
                .OrderByDescending(v=>v.firstDiscoveredTick).Take(12).ToArray();
        }
        private sealed record Request(VarietyRecord A,VarietyRecord B,string Key,ThingDef Species,string[] Ids);
        private static readonly Dictionary<string,Texture2D> art=new();
        private static readonly Queue<Request> pending=new();
        private static readonly HashSet<string> requested=new();
        private static Game game;
        internal static void Clear()
        {
            foreach(var t in art.Values)if(t!=null)UnityEngine.Object.Destroy(t);
            art.Clear();pending.Clear();requested.Clear();game=null;
        }
        internal static void Pump()
        {
            if(game!=Current.Game){Clear();game=Current.Game;}
            if(pending.Count==0)return;
            var r=pending.Dequeue();if(!ValidParent(r.A)||(r.B!=null&&!ValidPair(r.A,r.B))){requested.Remove(r.Key);return;}
            Texture2D image=null;
            try{BotanicalExpressionEngine.PreviewQualified(r.Species,r.Ids,out image);}
            catch(Exception e){Log.Warning("[Horticulture] Breeding preview deferred: "+e.Message);}
            if(art.Count>=12){string k=art.Keys.First();if(art[k]!=null)UnityEngine.Object.Destroy(art[k]);art.Remove(k);requested.Remove(k);}
            art[r.Key]=image;
        }
        internal static Texture Portrait(VarietyRecord a,VarietyRecord b,IReadOnlyList<VarietyTraitDef> traits,out bool rendered)
        {
            rendered=false;if(!ValidParent(a)||(b!=null&&!ValidPair(a,b))||traits==null||traits.Count==0)return a?.cropDef?.uiIcon;
            var ids=traits.Select(t=>t.defName).OrderBy(x=>x,StringComparer.Ordinal).ToArray();
            string key=a.id+"|"+b?.id+"|"+BotanicalExpressionEngine.Enabled+"|"+string.Join(",",ids);
            if(art.TryGetValue(key,out var tex)&&tex!=null){rendered=true;return tex;}
            if(!art.ContainsKey(key)&&pending.Count<12&&requested.Add(key))pending.Enqueue(new(a,b,key,a.cropDef,ids));
            return a.cropDef.uiIcon;
        }
    }

    public sealed partial class HorticultureWorkspaceDocument
    {
        private VarietyRecord studioA,studioB;
        private Zone_Growing studioZone;
        private void PickStudioParent(bool seed,List<VarietyRecord> all)
        {
            var options=all.Where(BreedingStudio.ValidParent).Where(v=>seed||studioA!=null&&v.cropDef==studioA.cropDef&&v!=studioA)
                .OrderBy(v=>v.Label).Take(1000).Select(v=>new FloatMenuOption(v.Label+" ("+v.cropDef.label+")",()=>{
                    if(seed){studioA=v;if(!BreedingStudio.ValidPair(studioA,studioB))studioB=null;studioZone=null;}else studioB=v;
                    rightScroll=Vector2.zero;
                })).ToList();
            if(options.Count==0)options.Add(new FloatMenuOption("No documented compatible cultivars",null));
            Find.WindowStack.Add(new FloatMenu(options));
        }
        private void StudioCard(Rect r,VarietyRecord v,VarietyRecord donor,IReadOnlyList<VarietyTraitDef> traits,string heading,string title)
        {
            Fill(r,Tile);Label(new(r.x+8,r.y+4,r.width-16,22),heading,GameFont.Tiny,Accent);
            var tex=BreedingStudio.Portrait(v,donor,traits,out bool rendered);
            var art=new Rect(r.x+8,r.y+30,Mathf.Min(92,r.width*.31f),88);Art(art,tex);
            Label(new(art.x,art.yMax+2,art.width,23),rendered?"MATURE PREVIEW":"SPECIES ART",GameFont.Tiny,Muted,TextAnchor.MiddleCenter);
            var x=art.xMax+8;var w=r.xMax-x-7;
            Label(new(x,r.y+30,w,30),title,GameFont.Small,Ink);
            Label(new(x,r.y+59,w,62),traits==null?"":string.Join(", ",traits.Select(TraitColorUI.Label)),GameFont.Tiny,Muted);
        }
        private void DrawStudio(Rect view,ref float y,List<VarietyRecord> all,FieldGuideIndex.GrowingZone[] zones)
        {
            if(!BreedingStudio.ValidParent(studioA)){studioA=studioB=null;studioZone=null;}
            if(!BreedingStudio.ValidPair(studioA,studioB))studioB=null;
            float w=(view.width-16)/3;
            if(Widgets.ButtonText(new(0,y,w,30),studioA==null?"Choose seed parent...":studioA.Label+"  /  change"))PickStudioParent(true,all);
            if(Widgets.ButtonText(new(w+8,y,w,30),studioB==null?"Choose pollen parent...":studioB.Label+"  /  change"))PickStudioParent(false,all);
            if(studioB!=null&&Widgets.ButtonText(new(2*(w+8),y,w,30),"Swap preview direction"))(studioA,studioB)=(studioB,studioA);
            y+=38;
            var plan=BreedingStudio.Describe(studioA,studioB);
            if(plan==null){Label(new(0,y,view.width,52),"Select two distinct documented cultivars of the same crop. Preview direction is explanatory; a growing-zone mix can sow either parent.",GameFont.Small,Muted);y+=58;return;}
            var a=HorticulturePresentationPolicy.ForCultivar(plan.Seed);var b=HorticulturePresentationPolicy.ForCultivar(plan.Donor);
            StudioCard(new(0,y,w,146),plan.Seed,null,a.AuthorizedTraits,"SEED PARENT",plan.Seed.Label);
            StudioCard(new(w+8,y,w,146),plan.Donor,null,b.AuthorizedTraits,"POLLEN PARENT",plan.Donor.Label);
            StudioCard(new(2*(w+8),y,w,146),plan.Seed,plan.Donor,plan.Example,"CONDITIONAL EXAMPLE",plan.Example.Length==0?"No simple example":"Possible combination");
            y+=153;
            Label(new(0,y,view.width,34),$"Cross chance setting: {plan.Chance.ToStringPercent()}  |  Minimum donor growth: {plan.DonorGrowth.ToStringPercent()}  |  {plan.Cap} shared/mechanical slot(s)"+(plan.Independent?" + 1 independent ornamental slot":""),GameFont.Small,Accent);y+=37;
            Label(new(0,y,view.width,32),"Candidates are not guaranteed; actual sowing uses nearby healthy donors, chance, balance and slot rules. Preview is illustrative.",GameFont.Tiny,Muted);y+=35;
            Section(view,ref y,"DONOR TRAITS  /  DIRECTIONAL COMPATIBILITY");
            foreach(var row in plan.Candidates.Take(12))
            {
                Fill(new(0,y,view.width,24),Tile);
                Label(new(8,y+1,view.width*.46f,23),TraitColorUI.Label(row.Trait),GameFont.Small,Ink);
                Label(new(view.width*.49f,y+2,view.width*.49f,22),(row.Ornamental?"Ornamental | ":"Agricultural / other | ")+row.Status,GameFont.Tiny,row.Status=="Eligible candidate"?Accent:Gold);y+=27;
            }
            if(plan.Candidates.Length>12){Label(new(0,y,view.width,22),$"{plan.Candidates.Length-12} additional traits; see parent dossier.",GameFont.Tiny,Muted);y+=25;}
            Section(view,ref y,"GROWING-ZONE MIX");
            if(!BreedingStudio.CanApply(studioZone,studioA,studioB))studioZone=null;
            if(Widgets.ButtonText(new(0,y,view.width*.49f,32),studioZone?.label??"Choose compatible growing zone..."))
            {
                var opts=zones.Where(z=>BreedingStudio.CanApply(z.Zone,studioA,studioB)).Select(z=>new FloatMenuOption(z.Zone.label,()=>studioZone=z.Zone)).ToList();
                if(opts.Count==0)opts.Add(new FloatMenuOption("No compatible existing zone",null));Find.WindowStack.Add(new FloatMenu(opts));
            }
            var applyRect=new Rect(view.width*.51f,y,view.width*.49f,32);
            if(studioZone!=null&&Widgets.ButtonText(applyRect,"Apply two-parent mix..."))
            {
                var z=studioZone;var x=studioA;var q=studioB;
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("Replace the cultivar selection for "+z.label+" with this two-parent mix? Existing plants are unchanged.",()=>{
                    if(!BreedingStudio.Apply(z,x,q))Messages.Message("The selected mix is no longer available.",MessageTypeDefOf.RejectInput,false);
                },true));
            }
            else if(studioZone==null)Label(applyRect,"Select a compatible zone first.",GameFont.Small,Muted);
            y+=38;
            if(studioZone!=null&&GameComponent_NovelSeeds.Instance.BreedingVarietiesFor(studioZone).Select(v=>v.id).ToHashSet().SetEquals(new[]{studioA.id,studioB.id}))
            {Label(new(0,y,view.width,24),"Active mix confirmed in registry. Existing plants remain unchanged.",GameFont.Small,Accent);y+=28;}
            Section(view,ref y,"DOCUMENTED OFFSPRING");
            var outcomes=BreedingStudio.Outcomes(studioA,studioB,all);
            if(outcomes.Length==0){Label(new(0,y,view.width,34),"No documented offspring of this pair. Unidentified plants and seed packs remain in the discovery inbox.",GameFont.Small,Muted);y+=38;}
            foreach(var v in outcomes)
            {
                var info=HorticulturePresentationPolicy.ForCultivar(v);
                Fill(new(0,y,view.width,52),Tile);
                Label(new(8,y+4,view.width-155,24),v.Label+"  |  Generation "+(info.Generation?.ToString()??"unknown"),GameFont.Small,Ink);
                Label(new(8,y+27,view.width-155,24),info.TraitText,GameFont.Tiny,Accent);
                if(Widgets.ButtonText(new(view.width-142,y+9,134,30),"Open cultivar"))PrepareCultivar(v);y+=56;
            }
        }
#if HNS_NATIVE_FIXTURE
        internal void FixtureStudio(VarietyRecord a,VarietyRecord b,Zone_Growing zone){PrepareBreeding();studioA=a;studioB=b;studioZone=zone;}
        internal void FixtureStudioOutcomes(){rightScroll=new Vector2(0,435);}
#endif
    }
}
