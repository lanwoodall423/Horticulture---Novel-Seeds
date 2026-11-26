using System;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace HorticultureNovelSeeds
{
    public sealed class ITab_PlantVariety : ITab_Botanical
    {
        public ITab_PlantVariety():base("HNS_VarietyTab"){}
        protected override string TabTitle=>"Cultivar dossier";
        public override bool IsVisible=>base.IsVisible&&SelThing.TryGetComp<CompPlantVariety>().HasAnyTraits;
#if HNS_NATIVE_FIXTURE
        internal static int LastDrawn=-1,LastTraits,LastParents;
        internal static bool LastAuthorized;
#endif
        private static string Factor(float x)=>((x-1f)*100f).ToString("+0.#;-0.#;0")+"%";
        protected override void DrawBody(Plant p,CompPlantVariety c,HorticultureCultivarPresentation a,float w,ref float y)
        {
            if(a==null)
            {
                Heading(w,ref y,"UNDOCUMENTED VARIATION");
                Body(w,ref y,"This individual has not been documented as a cultivar. Genetic identity, modifiers and lineage remain unknown.",Muted);
                if(Widgets.ButtonText(new Rect(0,y,w,28),"Open species in Horticulture"))MainTabWindow_CultivarRegistry.OpenPlant(p.def);
                y+=34;
            }
            else
            {
                Heading(w,ref y,"GENETIC PROFILE");
                var traits=a.AuthorizedTraits;
                float cardW=(w-6)/2;
                var stats=new[]{("Yield",NovelSeedUtility.YieldFactor(traits)),("Growth rate",NovelSeedUtility.GrowthRateFactor(traits)),("Sow work",ExpandedTraitUtility.SowWorkFactor(traits)),("Harvest work",ExpandedTraitUtility.HarvestWorkFactor(traits))};
                for(int i=0;i<stats.Length;i++)
                {
                    var r=new Rect((i%2)*(cardW+6),y+(i/2)*57,cardW,51);
                    Fill(r,Tile);Label(new(r.x+9,r.y+4,r.width-18,19),stats[i].Item1,GameFont.Tiny,Muted);
                    bool good=i>=2?stats[i].Item2<1:stats[i].Item2>1;
                    Label(new(r.x+9,r.y+21,r.width-18,26),Factor(stats[i].Item2),GameFont.Medium,Mathf.Approximately(stats[i].Item2,1)?Ink:good?Accent:Gold);
                }
                y+=116;
                Heading(w,ref y,"DOCUMENTED TRAITS  /  "+traits.Count);
                foreach(var t in traits)
                {
                    float h=Mathf.Max(48,Height(t.description,w-31)+31);
                    Fill(new(0,y,w,h),Tile);Fill(new(0,y,3,h),Accent);
                    Label(new(10,y+4,w-20,22),TraitColorUI.Label(t),GameFont.Small,Ink);
                    Label(new(10,y+25,w-20,h-26),t.description,GameFont.Small,Muted);
                    y+=h+4;
                }
                Heading(w,ref y,"LINEAGE");
                if(a.Parents.Count==0)Body(w,ref y,a.Origin,Muted);
                else
                {
                    int n=Math.Min(a.Parents.Count,3);float pw=(w-(n-1)*6)/n;
                    for(int i=0;i<n;i++)
                    {
                        var parent=a.Parents[i];Rect r=new(i*(pw+6),y,pw,35);
                        Fill(r,Tile);Text.Font=GameFont.Small;
                        Label(r.ContractedBy(4),parent.Label.Truncate(pw-8),GameFont.Small,parent.IsKnown?Ink:Muted,TextAnchor.MiddleCenter);
                        TooltipHandler.TipRegion(r,parent.Label);
                        Fill(new(r.center.x,y+35,1,13),Rule);
                    }
                    if(n>1)Fill(new(pw/2,y+47,w-pw,1),Rule);
                    Fill(new(w/2,y+47,1,10),Rule);
                    y+=57;
                }
                Fill(new(0,y,w,34),Panel);
                Label(new(5,y+4,w-10,26),a.Generation.HasValue?"Generation "+a.Generation.Value+"  |  "+a.Authority.Label:"Generation unknown  |  "+a.Authority.Label,GameFont.Small,Accent,TextAnchor.MiddleCenter);
                y+=44;
                if(a.Parents.Count>3)Body(w,ref y,"Additional documented parent references are available in the registry.",Muted);
                if(!string.IsNullOrEmpty(a.Authority.FirstDiscoveredInfo))Body(w,ref y,a.Authority.FirstDiscoveredInfo,Muted);
                if(Widgets.ButtonText(new Rect(0,y,w,30),"Open lineage in Horticulture"))MainTabWindow_CultivarRegistry.OpenLineage(a.Authority);
                y+=36;
            }
#if HNS_NATIVE_FIXTURE
            if(Event.current.type==EventType.Repaint){LastDrawn=p.thingIDNumber;LastTraits=a?.AuthorizedTraits.Count??0;LastParents=a?.Parents.Count??0;LastAuthorized=a!=null;}
#endif
        }
    }
}
