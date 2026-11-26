using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using V=HorticultureNovelSeeds.PhenotypeVocabulary;

namespace HorticultureNovelSeeds
{
    // Native plant selection tab. The art is explicitly a species reference;
    // the specimen's expressed phenotype comes from the renderer, not the icon.
    public sealed class ITab_PlantPhenotype : ITab
    {
        private static readonly Color Ink=new(.88f,.92f,.87f);
        private static readonly Color Muted=new(.64f,.73f,.69f);
        private static readonly Color Accent=new(.65f,.83f,.54f);
        private static readonly Color Panel=new(.105f,.155f,.135f);
        private static readonly Color Tile=new(.135f,.18f,.16f);
        private static readonly Color Rule=new(.25f,.33f,.28f);
        private static readonly Color Competition=new(.80f,.65f,.39f);
        private static readonly Color Unobserved=new(.38f,.43f,.41f);
        private Thing lastThing;
        private Vector2 scroll;
#if HNS_NATIVE_FIXTURE
        internal static int LastDrawnId=-1,DrawCount;
        internal static int LastHistorySamples,LastFactCount;
        internal static bool LastPotentialVisible;
#endif
        public ITab_PlantPhenotype()
        {
            size=new Vector2(548f,454f);
            labelKey="HNS_PhenotypeTab";
        }
        public override bool IsVisible=>SelThing is Plant p&&p.TryGetComp<CompPlantVariety>()?.HasAnyTraits==true;
        private static void Fill(Rect r,Color c)=>Widgets.DrawBoxSolid(r,c);
        private static void Label(Rect r,string value,GameFont font,Color color,TextAnchor anchor=TextAnchor.UpperLeft)
        {
            Text.Font=font;Text.Anchor=anchor;GUI.color=color;
            Widgets.Label(r,value??"");GUI.color=Color.white;
        }
        private static float Height(string value,float width,GameFont font=GameFont.Small)
        {
            Text.Font=font;
            return Mathf.Max(19f,Text.CalcHeight(value??"",Mathf.Max(1f,width)));
        }
        private static string TitleCase(string value)=>string.IsNullOrEmpty(value)?value:char.ToUpperInvariant(value[0])+value.Substring(1);
        private static string RegionLabel(V.Region region)=>region switch
        {
            V.Region.Habit=>"Growth habit",V.Region.Foliage=>"Foliage",V.Region.Reproductive=>"Reproduction",V.Region.Stem=>"Stems",
            V.Region.Surface=>"Surface",V.Region.Ground=>"Ground growth",V.Region.Motion=>"Movement",V.Region.Light=>"Light effects",_=>"Appearance"
        };
        private static Color RegionColor(V.Region region)=>region switch
        {
            V.Region.Habit=>new(.64f,.79f,.52f),
            V.Region.Foliage=>new(.45f,.76f,.50f),
            V.Region.Reproductive=>new(.90f,.69f,.43f),
            V.Region.Stem=>new(.72f,.59f,.43f),
            V.Region.Surface=>new(.68f,.79f,.82f),
            V.Region.Ground=>new(.69f,.64f,.45f),
            V.Region.Motion=>new(.55f,.77f,.78f),
            V.Region.Light=>new(.82f,.81f,.48f),_=>Muted
        };
        private static void Heading(float width,ref float y,string text)
        {
            Label(new Rect(0,y,width,20),text,GameFont.Tiny,Accent);
            Fill(new Rect(0,y+19,width,1),Rule);y+=26;
        }
        private static void Body(float width,ref float y,string value,Color color)
        {
            float h=Height(value,width);
            Label(new Rect(0,y,width,h+2),value,GameFont.Small,color);
            y+=h+9;
        }
        private static void DrawHeader(Rect r,Plant p,HorticultureCultivarPresentation authority)
        {
            Fill(r,Panel);Fill(new Rect(r.x,r.y,3,r.height),Accent);
            Rect art=new(r.x+12,r.y+7,76,64);
            Fill(art,Tile);
            var icon=p.def.uiIcon;
            if(icon!=null&&icon!=BaseContent.BadTex)GUI.DrawTexture(art.ContractedBy(5),icon,ScaleMode.ScaleToFit,true);
            Label(new Rect(art.x-2,art.yMax+1,80,19),"SPECIES ART",GameFont.Tiny,Muted,TextAnchor.MiddleCenter);
            float x=r.x+101,w=r.width-114;
            Label(new Rect(x,r.y+8,w,28),TitleCase(p.def.label),GameFont.Medium,Ink);
            string subtitle=authority!=null?authority.Authority.Label:p.TryGetComp<CompPlantVariety>().PendingDiscovery?"Undocumented variation":"Cultivar not documented";
            // A long cultivar label may be truncated only here; the full
            // documented identity remains available through the adjacent tab.
            Text.Font=GameFont.Small;
            Label(new Rect(x,r.y+37,w,20),subtitle.Truncate(w),GameFont.Small,Muted);
            TooltipHandler.TipRegion(new Rect(x,r.y+36,w,22),subtitle);
            float growth=Mathf.Clamp01(p.Growth);
            Label(new Rect(x,r.y+62,w-56,18),"MATURITY",GameFont.Tiny,Muted);
            Label(new Rect(x+w-56,r.y+59,56,22),growth.ToStringPercent(),GameFont.Small,Accent,TextAnchor.MiddleRight);
            Rect track=new(x,r.y+82,w,5);
            Fill(track,Rule);Fill(new Rect(track.x,track.y,track.width*growth,track.height),Accent);
        }
        private static void DrawHistory(float width,ref float y,int code)
        {
            Heading(width,ref y,"GROWING HISTORY");
            if(ChronicleMath.Count(code)==0)
            {
                Body(width,ref y,"No historical growth observations recorded.",Muted);return;
            }
            string[] stages={"40%  EARLY","65%  MIDDLE","90%  LATE"};
            string[] descriptions={"Not recorded","Open space","Left competition","Right competition","Both sides"};
            float gap=6,w=(width-gap*2)/3;
            for(int i=0;i<3;i++)
            {
                int v=ChronicleMath.At(code,i);if(v<0||v>4)v=0;
                Rect tile=new(i*(w+gap),y,w,62);
                Fill(tile,Tile);
                Label(new Rect(tile.x+7,tile.y+3,w-14,18),stages[i],GameFont.Tiny,v==0?Muted:Accent,TextAnchor.MiddleCenter);
                float cx=tile.center.x,cy=tile.y+28;
                for(int n=-1;n<=1;n++)
                {
                    bool occupied=n==0||n==-1&&(v==2||v==4)||n==1&&(v==3||v==4);
                    var color=v==0?Unobserved:n==0?Accent:occupied?Competition:Rule;
                    var cell=new Rect(cx+n*16-6,cy-6,12,12);
                    Fill(cell,color);
                    if(!occupied||v==0)Fill(cell.ContractedBy(2),Tile);
                }
                Label(new Rect(tile.x+3,tile.y+43,w-6,16),descriptions[v],GameFont.Tiny,v==0?Muted:Ink,TextAnchor.MiddleCenter);
                TooltipHandler.TipRegion(tile,stages[i]+": "+descriptions[v]+". Center = specimen; side squares = recorded lateral neighbors.");
            }
            y+=68;
            Label(new Rect(0,y,width,18),"Green: specimen   Ochre: neighbors   Outline: empty or unrecorded",GameFont.Tiny,Muted);
            y+=25;
        }
        protected override void FillTab()
        {
            var p=SelThing as Plant;var c=p?.TryGetComp<CompPlantVariety>();
            if(c==null||!c.HasAnyTraits)return;
            if(lastThing!=p){scroll=Vector2.zero;lastThing=p;}
            var oldFont=Text.Font;var oldAnchor=Text.Anchor;var oldColor=GUI.color;bool oldWrap=Text.WordWrap;
            try
            {
                Text.WordWrap=true;GUI.color=Color.white;
                var authority=c.PendingDiscovery?null:HorticulturePresentationPolicy.ForCultivar(c.Variety);
                var facts=c.VisualObservation?.Facts()??Array.Empty<V.Fact>();
                var groups=facts.GroupBy(f=>f.Region).Select(g=>(Region:g.Key,Value:TitleCase(V.Join(g.Select(f=>f.Phrase))))).ToArray();
                string description=V.Prose(facts);
                if(string.IsNullOrEmpty(description))description="No distinctive rendered expression is currently recorded.";
                string potential=authority?.PotentialAppearance;
                string function=authority==null?null:AdaptivePhenotypePresentation.Explain(c);
                Rect bounds=new Rect(0,0,size.x,size.y).ContractedBy(11);
                DrawHeader(new Rect(bounds.x,bounds.y,bounds.width,96),p,authority);
                Rect outer=new(bounds.x,bounds.y+106,bounds.width,bounds.height-106);
                float width=outer.width-18,detailWidth=width-126;
                float rowHeight=groups.Sum(g=>Mathf.Max(29f,Height(g.Value,detailWidth-10)+10f)+3f);
                float h=26+Height(description,width)+9+26+Mathf.Max(28,rowHeight)+
                    26+(ChronicleMath.Count(c.VisualChronicle)>0?93:38)+26+Height(potential??"Not documented.",width)+31+(string.IsNullOrEmpty(function)?0:26+Height(function,width)+9);
                Rect view=new(0,0,width,Mathf.Max(outer.height,h));
                Widgets.BeginScrollView(outer,ref scroll,view);
                try
                {
                    float y=0;
                    Heading(width,ref y,"INDIVIDUAL PHENOTYPE");
                    Body(width,ref y,description,facts.Length==0?Muted:Ink);
                    if(!string.IsNullOrEmpty(function)){Heading(width,ref y,"APPEARANCE AND CULTIVATION");Body(width,ref y,function,Ink);}
                    Heading(width,ref y,"BOTANICAL OBSERVATIONS");
                    if(groups.Length==0)Body(width,ref y,"No active visual observations.",Muted);
                    foreach(var g in groups)
                    {
                        float rh=Mathf.Max(29f,Height(g.Value,detailWidth-10)+10f);
                        Fill(new Rect(0,y,width,rh),Tile);Fill(new Rect(0,y,3,rh),RegionColor(g.Region));
                        Label(new Rect(9,y+5,112,rh-6),RegionLabel(g.Region),GameFont.Small,RegionColor(g.Region));
                        Label(new Rect(126,y+5,detailWidth-5,rh-5),g.Value,GameFont.Small,Ink);
                        y+=rh+3;
                    }
                    y+=7;
                    DrawHistory(width,ref y,c.VisualChronicle);
                    Heading(width,ref y,"CULTIVAR POTENTIAL");
                    if(!string.IsNullOrEmpty(potential))
                    {
                        Label(new Rect(0,y,width,19),"Inherited tendencies; not necessarily expressed.",GameFont.Tiny,Muted);y+=22;
                        Body(width,ref y,potential,Ink);
                    }
                    else Body(width,ref y,authority==null?"Undocumented cultivar. Inherited tendencies are unknown.":"No distinct documented appearance tendencies.",Muted);
                }
                finally{Widgets.EndScrollView();}
#if HNS_NATIVE_FIXTURE
                if(Event.current.type==EventType.Repaint)
                {
                    LastDrawnId=p.thingIDNumber;DrawCount++;LastHistorySamples=ChronicleMath.Count(c.VisualChronicle);
                    LastFactCount=facts.Length;LastPotentialVisible=!string.IsNullOrEmpty(potential);
                }
#endif
            }
            finally{Text.Font=oldFont;Text.Anchor=oldAnchor;Text.WordWrap=oldWrap;GUI.color=oldColor;}
        }
    }
}
