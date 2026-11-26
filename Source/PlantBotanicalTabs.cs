using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace HorticultureNovelSeeds
{
    // Common native field-guide styling for plant inspector documents.
    public abstract class ITab_Botanical : ITab
    {
        protected static readonly Color Ink=new(.88f,.92f,.87f),Muted=new(.64f,.73f,.69f),Accent=new(.65f,.83f,.54f),
            Panel=new(.105f,.155f,.135f),Tile=new(.135f,.18f,.16f),Rule=new(.25f,.33f,.28f),
            Gold=new(.87f,.72f,.46f),Blue=new(.58f,.78f,.83f);
        protected abstract string TabTitle {get;}
        private Thing lastThing;
        private Vector2 scroll;
        private float contentHeight=900;
        protected ITab_Botanical(string key){size=new Vector2(548f,454f);labelKey=key;}
        public override bool IsVisible=>SelThing is Plant p&&p.Spawned&&p.TryGetComp<CompPlantVariety>()!=null;
        protected static void Fill(Rect r,Color c)=>Widgets.DrawBoxSolid(r,c);
        protected static void Label(Rect r,string text,GameFont font,Color c,TextAnchor anchor=TextAnchor.UpperLeft)
        {
            Text.Font=font;Text.Anchor=anchor;GUI.color=c;Widgets.Label(r,text??"");GUI.color=Color.white;
        }
        protected static float Height(string t,float w){Text.Font=GameFont.Small;return Mathf.Max(19,Text.CalcHeight(t??"",Mathf.Max(1,w)));}
        protected static void Heading(float w,ref float y,string t)
        {Label(new(0,y,w,19),t,GameFont.Tiny,Accent);Fill(new(0,y+19,w,1),Rule);y+=26;}
        protected static void Body(float w,ref float y,string t,Color c)
        {float h=Height(t,w);Label(new(0,y,w,h+3),t,GameFont.Small,c);y+=h+10;}
        protected static void Row(float w,ref float y,string name,string value,Color accent)
        {
            float labelWidth=160;
            float h=Mathf.Max(29,Height(value,w-labelWidth-14)+10);
            Fill(new(0,y,w,h),Tile);Fill(new(0,y,3,h),accent);
            Label(new(10,y+5,labelWidth-12,h-6),name,GameFont.Small,accent);
            Label(new(labelWidth,y+5,w-labelWidth-8,h-5),value,GameFont.Small,Ink);
            y+=h+4;
        }
        protected static void Meter(float w,ref float y,string name,float value,float full,Color color,string detail=null)
        {
            string text=float.IsFinite(value)?value.ToStringPercent():"Unknown";
            Label(new(0,y,w-100,22),name,GameFont.Small,Ink);
            Label(new(w-100,y,100,22),text,GameFont.Small,color,TextAnchor.MiddleRight);
            Fill(new(0,y+23,w,5),Rule);
            if(float.IsFinite(value))Fill(new(0,y+23,w*Mathf.Clamp01(value/full),5),color);
            y+=34;
            if(!string.IsNullOrEmpty(detail))
            {Text.Font=GameFont.Tiny;float h=Mathf.Max(18f,Text.CalcHeight(detail,w));Label(new(0,y,w,h),detail,GameFont.Tiny,Muted);y+=h+4;}
        }
        protected static void MeterAt(float x,float w,ref float y,string name,float value,float full,Color color)
        {
            Label(new(x,y,w-72,20),name,GameFont.Small,Ink);
            Label(new(x+w-72,y,72,20),float.IsFinite(value)?value.ToStringPercent():"Unknown",GameFont.Small,color,TextAnchor.MiddleRight);
            Fill(new(x,y+22,w,5),Rule);
            if(float.IsFinite(value))Fill(new(x,y+22,w*Mathf.Clamp01(value/full),5),color);
            y+=36;
        }
        protected static HorticultureCultivarPresentation Authority(CompPlantVariety c)=>c==null||c.PendingDiscovery?null:HorticulturePresentationPolicy.ForCultivar(c.Variety);
        protected static void Icon(Rect r,ThingDef def)
        {if(def?.uiIcon!=null&&def.uiIcon!=BaseContent.BadTex)GUI.DrawTexture(r,def.uiIcon,ScaleMode.ScaleToFit,true);}
        private void Header(Rect r,Plant p,HorticultureCultivarPresentation a)
        {
            Fill(r,Panel);Fill(new(r.x,r.y,3,r.height),Accent);
            Rect art=new(r.x+12,r.y+7,76,64);Fill(art,Tile);Icon(art.ContractedBy(5),p.def);
            Label(new(art.x-2,art.yMax+1,80,19),"SPECIES ART",GameFont.Tiny,Muted,TextAnchor.MiddleCenter);
            float x=r.x+101,w=r.width-114;
            Label(new(x,r.y+7,w,30),TabTitle,GameFont.Medium,Ink);
            string name=a?.Authority.Label??(p.TryGetComp<CompPlantVariety>().PendingDiscovery?"Undocumented variation":p.def.LabelCap.ToString());
            Text.Font=GameFont.Small;Label(new(x,r.y+36,w,22),name.Truncate(w),GameFont.Small,Muted);
            TooltipHandler.TipRegion(new Rect(x,r.y+35,w,23),name);
            Label(new(x,r.y+62,w-60,18),"MATURITY",GameFont.Tiny,Muted);
            Label(new(x+w-60,r.y+59,60,22),p.Growth.ToStringPercent(),GameFont.Small,Accent,TextAnchor.MiddleRight);
            Fill(new(x,r.y+82,w,5),Rule);Fill(new(x,r.y+82,w*Mathf.Clamp01(p.Growth),5),Accent);
        }
        protected override void FillTab()
        {
            var p=SelThing as Plant;var c=p?.TryGetComp<CompPlantVariety>();if(c==null||!p.Spawned)return;
            if(lastThing!=p){scroll=Vector2.zero;contentHeight=900;lastThing=p;}
            var oldFont=Text.Font;var oldAnchor=Text.Anchor;var oldColor=GUI.color;bool wrap=Text.WordWrap;
            try
            {
                Text.WordWrap=true;GUI.color=Color.white;var a=Authority(c);
                Rect bounds=new Rect(0,0,size.x,size.y).ContractedBy(11);
                Header(new(bounds.x,bounds.y,bounds.width,96),p,a);
                Rect outer=new(bounds.x,bounds.y+106,bounds.width,bounds.height-106);
                float w=outer.width-18;
                Widgets.BeginScrollView(outer,ref scroll,new Rect(0,0,w,Mathf.Max(outer.height,contentHeight)));
                try{float y=0;DrawBody(p,c,a,w,ref y);contentHeight=y+8;}
                finally{Widgets.EndScrollView();}
            }
            finally{Text.Font=oldFont;Text.Anchor=oldAnchor;Text.WordWrap=wrap;GUI.color=oldColor;}
        }
        protected abstract void DrawBody(Plant p,CompPlantVariety c,HorticultureCultivarPresentation a,float w,ref float y);
    }

    internal static class PlantInspectionFacts
    {
        internal readonly record struct Ecology(float Fertility,float Glow,float Temperature,float FertilityFactor,float LightFactor,float TemperatureFactor,float GrowthRate,bool Season,NeighborhoodMath.Pressure Pressure,int Occupied);
        internal static Ecology ReadEcology(Plant p)
        {
            var m=p.Map;var pos=p.Position;
            float t=GenTemperature.TryGetTemperatureForCell(pos,m,out float temp)?temp:float.NaN;
            int occupied=0;
            for(int z=-1;z<=1;z++)for(int x=-1;x<=1;x++)
            {if(x==0&&z==0)continue;var cell=pos+new IntVec3(x,0,z);if(cell.InBounds(m)&&cell.GetPlant(m)!=null)occupied++;}
            return new(m.fertilityGrid.FertilityAt(pos),m.glowGrid.GroundGlowAt(pos),t,p.GrowthRateFactor_Fertility,p.GrowthRateFactor_Light,p.GrowthRateFactor_Temperature,p.GrowthRate,PlantUtility.GrowthSeasonNow(pos,m,p.def),NeighborhoodExperiment.Current(p),occupied);
        }
        internal readonly record struct Harvest(float Base,float Estimate,bool Ready,bool Known,float Factor,float Regrowth,bool SeedEligible,bool SeedRequested);
        // Mirror the verified vanilla expectation, before randomized rounding.
        // Never call YieldNow: its postfix captures produce context and consumes RNG.
        internal static Harvest ReadHarvest(Plant p,CompPlantVariety c,HorticultureCultivarPresentation a)
        {
            var d=p.def.plant;bool ready=p.CanYieldNow();
            float b=ready?d.harvestYield*Mathf.Lerp(.5f,1f,Mathf.InverseLerp(d.harvestMinGrowth,1f,p.Growth))*Mathf.Lerp(.5f,1f,(float)p.HitPoints/Mathf.Max(1,p.MaxHitPoints)):0f;
            if(d.harvestYieldAffectedByDifficulty)b*=Find.Storyteller.difficulty.cropYieldFactor;
            bool known=a!=null||!c.HasAnyTraits;
            float factor=a==null?1f:NovelSeedUtility.YieldFactor(a.AuthorizedTraits)*ExpandedTraitUtility.SynergyFactor(p,a.AuthorizedTraits,"Yield");
            float regrowth=a==null?0f:NovelSeedUtility.PerennialHarvestAfterGrowth(a.AuthorizedTraits);
            return new(b,b*factor,ready,known,factor,regrowth,c.PendingDiscovery&&p.Growth>=.999f&&!p.Blighted,c.SaveSeedsRequested);
        }
    }

    public sealed class ITab_PlantEcology:ITab_Botanical
    {
        public ITab_PlantEcology():base("HNS_EcologyTab"){}
        protected override string TabTitle=>"Ecological profile";
#if HNS_NATIVE_FIXTURE
        internal static int LastDrawn=-1;
        internal static PlantInspectionFacts.Ecology LastFacts;
#endif
        protected override void DrawBody(Plant p,CompPlantVariety c,HorticultureCultivarPresentation a,float w,ref float y)
        {
            var f=PlantInspectionFacts.ReadEcology(p);
            Heading(w,ref y,"CURRENT GROWING CONDITIONS");
            float tile=43,gap=4,x0=3,y0=y+4;
            for(int z=1;z>=-1;z--)for(int x=-1;x<=1;x++)
            {
                var cell=p.Position+new IntVec3(x,0,z);
                bool center=x==0&&z==0,inside=cell.InBounds(p.Map);
                var plant=inside?cell.GetPlant(p.Map):null;
                Rect r=new(x0+(x+1)*(tile+gap),y0+(1-z)*(tile+gap),tile,tile);
                Fill(r,center?Accent:plant!=null?Gold:Rule);
                Fill(r.ContractedBy(center?2:1),Tile);
                if(plant!=null)Icon(r.ContractedBy(5),plant.def);
                else Label(r,inside?"-":"X",GameFont.Small,Muted,TextAnchor.MiddleCenter);
                TooltipHandler.TipRegion(r,center?"Selected specimen":!inside?"Outside map":plant==null?"No plant in this cell":plant.def.LabelCap+" - "+plant.Growth.ToStringPercent()+" mature");
            }
            float tx=159,tw=w-tx,ty=y;
            MeterAt(tx,tw,ref ty,"Soil fertility",f.Fertility,1.4f,Accent);
            MeterAt(tx,tw,ref ty,"Light at plant",f.Glow,1f,Gold);
            MeterAt(tx,tw,ref ty,"Current growth rate",f.GrowthRate,1.4f,Accent);
            Label(new(tx,ty+2,tw,23),"Temperature  "+(float.IsFinite(f.Temperature)?GenText.ToStringTemperature(f.Temperature,"F0"):"Unknown"),GameFont.Small,Blue);
            Label(new(x0,y+145,143,19),"NORTH UP",GameFont.Tiny,Muted,TextAnchor.MiddleCenter);
            y+=168;
            Label(new(0,y,w,21),f.Occupied+" / 8 neighboring cells planted",GameFont.Small,Ink);y+=25;
            Label(new(0,y,w,20),"West pressure "+f.Pressure.Left.ToStringPercent()+"  |  East pressure "+f.Pressure.Right.ToStringPercent(),GameFont.Small,Accent);y+=24;
            string context=f.Pressure.Left==0&&f.Pressure.Right==0?"No significant lateral plant pressure.":f.Pressure.Difference>0?"More mature vegetation to the west.":f.Pressure.Difference<0?"More mature vegetation to the east.":"Similar vegetation pressure on both lateral sides.";
            Body(w,ref y,context,Ink);
            Body(w,ref y,"Green: specimen. Ochre: nearby vegetation. Lateral pressure is the growth-band model used by Contextual Botany, not a simulated root or light network.",Muted);
            Heading(w,ref y,"GROWTH MULTIPLIERS");
            Meter(w,ref y,"Fertility factor",f.FertilityFactor,1.4f,Accent);
            Meter(w,ref y,"Light factor",f.LightFactor,1f,Gold);
            Meter(w,ref y,"Temperature factor",f.TemperatureFactor,1f,Blue);
            Row(w,ref y,"Growing season",f.Season?"Active":"Outside growing season",f.Season?Accent:Gold);
            if(p.Blighted)Body(w,ref y,"Blight is preventing normal growth.",Gold);
            else if(p.Dying)Body(w,ref y,"The plant is taking environmental damage.",Gold);
#if HNS_NATIVE_FIXTURE
            if(Event.current.type==EventType.Repaint){LastDrawn=p.thingIDNumber;LastFacts=f;}
#endif
        }
    }

    public sealed class ITab_PlantHarvest:ITab_Botanical
    {
        public ITab_PlantHarvest():base("HNS_HarvestTab"){}
        protected override string TabTitle=>"Harvest profile";
#if HNS_NATIVE_FIXTURE
        internal static int LastDrawn=-1;
        internal static PlantInspectionFacts.Harvest LastFacts;
        internal static bool LastAuthorized;
#endif
        protected override void DrawBody(Plant p,CompPlantVariety c,HorticultureCultivarPresentation a,float w,ref float y)
        {
            var f=PlantInspectionFacts.ReadHarvest(p,c,a);var d=p.def.plant;var product=d.harvestedThingDef;
            Heading(w,ref y,"HARVEST OUTLOOK");
            Fill(new(0,y,w,79),Tile);
            Rect icon=new(9,y+9,60,60);Icon(icon,product);
            Label(new(81,y+8,w-90,27),product?.LabelCap.ToString()??"No ordinary harvested product",GameFont.Medium,Ink);
            Label(new(81,y+40,w-92,29),f.Ready?"Ready to harvest":p.Blighted?"Blighted - no normal yield":product==null?"No ordinary crop yield":"Not ready",GameFont.Small,f.Ready?Accent:Gold);
            y+=88;
            if(product!=null)
            {
                Meter(w,ref y,"Maturity",p.Growth,1f,Accent);
                Row(w,ref y,"Minimum harvest growth",d.harvestMinGrowth.ToStringPercent(),Muted);
                Row(w,ref y,"Species base yield",d.harvestYield.ToString("0.##")+" units at full maturity",Muted);
                Row(w,ref y,f.Known?"Estimated yield now":"Species-only estimate",f.Estimate.ToString("0.##")+" units",Gold);
                Body(w,ref y,f.Known?"Expected primary output before random rounding, harvester efficiency and other mods; not a guaranteed harvest.":"Undocumented genetic effects are excluded. Actual yield may differ.",Muted);
                if(a!=null)Row(w,ref y,"Known genetic yield factor",f.Factor.ToStringPercent(),Accent);
                if(f.Regrowth>0)Row(w,ref y,"Perennial regrowth",f.Regrowth.ToStringPercent()+" growth after collection",Accent);
            }
            Heading(w,ref y,"SEED RECOVERY");
            if(c.PendingDiscovery)
                Body(w,ref y,f.SeedRequested?"Save Seeds requested. Recovery requires a successful player harvest or cut.":f.SeedEligible?"Mature novel variation. Use the existing Save Seeds command to request recovery.":"Novel variation: seed recovery requires full maturity and no blight.",Ink);
            else Body(w,ref y,a!=null?"Documented cultivar: sow through the existing cultivar selector. No additional seed-recovery request is needed.":"No novel seed recovery available for this specimen.",Muted);
            Heading(w,ref y,"DOCUMENTED PRODUCE QUALITIES");
            if(a==null)Body(w,ref y,c.HasAnyTraits?"Undocumented inherited qualities are unknown.":"No documented cultivar qualities.",Muted);
            else
            {
                var effective=a.AuthorizedTraits.Where(t=>HorticultureNovelSeedsMod.Settings?.ProduceTraitHasEffect(p.def,t)??PlantTagUtility.MeetsProduceEffectRequirements(p.def,t)).ToArray();
                bool any=false;
                foreach(var trait in effective)
                {
                    string line=NovelSeedUtility.InheritedProduceQualityLine(trait,product);
                    if(string.IsNullOrWhiteSpace(line)||line=="No Effect")continue;
                    Row(w,ref y,TraitColorUI.Label(trait),line,Accent);any=true;
                }
                if(!any)Body(w,ref y,"No distinct documented produce modifiers.",Muted);
                bool visuals=HorticultureNovelSeedsMod.Settings?.enableProduceVisuals!=false&&NovelSeedUtility.HasProduceVisual(p.def,a.AuthorizedTraits);
                if(visuals)
                {
                    var styles=NovelSeedUtility.ResolveProduceColorStyles(p.def,a.AuthorizedTraits);
                    Label(new(0,y,w,20),"Potential produce color modifiers",GameFont.Small,Accent);y+=24;
                    float sw=(w-12)/3;
                    for(int i=0;i<3;i++){var r=new Rect(i*(sw+6),y,sw,22);Fill(r,styles[i].Apply(Color.white));}
                    y+=29;
                    Body(w,ref y,"Modifier swatches on white, not the final harvested texture.",Muted);
                }
                var journal=a.Journal;
                if(journal?.harvests>0&&journal.minYield>=0)
                    Row(w,ref y,"Recorded harvests",journal.harvests+" confirmed; "+journal.minYield+" - "+journal.maxYield+" units",Accent);
            }
#if HNS_NATIVE_FIXTURE
            if(Event.current.type==EventType.Repaint){LastDrawn=p.thingIDNumber;LastFacts=f;LastAuthorized=a!=null;}
#endif
        }
    }
}
