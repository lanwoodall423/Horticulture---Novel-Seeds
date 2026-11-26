using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace HorticultureNovelSeeds
{
    public sealed partial class HorticultureWorkspaceDocument
    {
        private static readonly Color Ink=new(.90f,.94f,.88f),Muted=new(.63f,.72f,.67f),Accent=new(.68f,.83f,.57f),
            Panel=new(.105f,.155f,.135f),Tile=new(.15f,.20f,.17f),Rule=new(.27f,.35f,.29f),Gold=new(.89f,.73f,.43f),Blue=new(.58f,.77f,.83f);
        private static void Fill(Rect r,Color c)=>Widgets.DrawBoxSolid(r,c);
        private static void Label(Rect r,string value,GameFont font,Color color,TextAnchor anchor=TextAnchor.UpperLeft)
        {
            var old=Text.Font;var a=Text.Anchor;var c=GUI.color;
            try{Text.Font=font;Text.Anchor=anchor;GUI.color=color;Widgets.Label(r,value??"");}
            finally{Text.Font=old;Text.Anchor=a;GUI.color=c;}
        }
        private static void Art(Rect r,Texture tex)
        {
            Fill(r,Panel);if(tex!=null&&tex!=BaseContent.BadTex)GUI.DrawTexture(r.ContractedBy(5),tex,ScaleMode.ScaleToFit,true);
        }
        private static void Section(Rect v,ref float y,string label)
        {Label(new(v.x,y,v.width,24),label,GameFont.Small,Accent);Fill(new(v.x,y+25,v.width,1),Rule);y+=33;}
        private static void Title(Rect v,ref float y,string title,string sub)
        {Label(new(v.x,y,v.width,32),title,GameFont.Medium,Ink);Label(new(v.x,y+35,v.width,34),sub,GameFont.Small,Muted);y+=74;}
        private static void Metric(Rect r,string n,string label,Color color)
        {
            Fill(r,Tile);Fill(new(r.x,r.y,3,r.height),color);
            Label(new(r.x+12,r.y+5,r.width-22,33),n,GameFont.Medium,color);
            Label(new(r.x+12,r.y+Mathf.Min(40,r.height-23),r.width-22,23),label,GameFont.Small,Muted);
        }
        private static void Meter(Rect r,float value,Color c)
        {Fill(r,Rule);Fill(new(r.x,r.y,r.width*Mathf.Clamp01(value),r.height),c);}
        private static string StateLabel(FieldGuideIndex.State s)=>s switch
        {
            FieldGuideIndex.State.Ready=>"Ready to preserve",FieldGuideIndex.State.Recovered=>"Awaiting documentation",
            FieldGuideIndex.State.Requested=>"Preservation requested",FieldGuideIndex.State.Blighted=>"Blighted - cannot preserve",_=>"Growing"
        };
        private static Color StateColor(FieldGuideIndex.State s)=>s==FieldGuideIndex.State.Ready?Gold:s==FieldGuideIndex.State.Recovered?Accent:s==FieldGuideIndex.State.Blighted?new Color(.86f,.53f,.42f):Blue;
        private static void LocateThing(Thing t)
        {
            if(t?.Spawned!=true)return;
            Find.MainTabsRoot.SetCurrentTab(MainButtonDefOf.Inspect,false);CameraJumper.TryJumpAndSelect(t);
        }
        private static void DiscoveryCard(Rect r,FieldGuideIndex.Discovery item)
        {
            Fill(r,Tile);var color=StateColor(item.Status);Fill(new(r.x,r.y,3,r.height),color);
            Art(new(r.x+9,r.y+8,62,r.height-16),item.Thing is Plant?item.Species.uiIcon:item.Thing.def.uiIcon);
            float actionW=r.width>=720?160:116;float x=r.x+82,w=r.width-82-actionW-18;
            string title=item.Thing is Plant?"Unusual "+item.Species.label:item.Species.LabelCap+" seed pack";
            Label(new(x,r.y+7,w,25),title,GameFont.Small,Ink);
            Label(new(x,r.y+32,w,21),StateLabel(item.Status)+(item.Thing is Plant?"  |  "+item.Growth.ToStringPercent()+" mature":""),GameFont.Tiny,color);
            if(item.Thing is Plant)Meter(new(x,r.y+58,w,5),item.Growth,color);
            var locate=new Rect(r.xMax-actionW-8,r.y+7,actionW,28);
            if(Widgets.ButtonText(locate,"Locate"))LocateThing(item.Thing);
            var action=new Rect(locate.x,r.y+39,actionW,28);
            if(item.Status==FieldGuideIndex.State.Ready&&Widgets.ButtonText(action,"Preserve seeds"))
            {if(!FieldGuideIndex.Preserve(item.Thing))Messages.Message("This discovery is no longer ready.",MessageTypeDefOf.RejectInput,false);}
            if(item.Status==FieldGuideIndex.State.Recovered&&Widgets.ButtonText(action,"Document..."))FieldGuideIndex.ChooseDocumenter(item.Thing);
        }
        private void DrawOverview(Rect rect,List<VarietyRecord> all)
        {
            var s=FieldGuideIndex.Current;
            var recent=all.Where(v=>!v.registryArchived).OrderByDescending(v=>v.registryFavorite).ThenByDescending(v=>v.firstDiscoveredTick).Take(6).ToArray();
            var urgent=s?.Discoveries.Where(r=>r.Status is FieldGuideIndex.State.Ready or FieldGuideIndex.State.Recovered).Take(2).ToArray()??Array.Empty<FieldGuideIndex.Discovery>();
            int columns=rect.width>=850?3:2;
            var view=NativeUi.Scroll(rect,ref rightScroll,310+Math.Max(1,urgent.Length)*82+Mathf.CeilToInt((float)recent.Length/columns)*178);
            try
            {
                float y=0;Title(view,ref y,"Botanical field guide","Discover, preserve and cultivate the living history of your colony.");
                float w=(view.width-16)/3;
                Metric(new(0,y,w,74),all.Count.ToString(),"Documented cultivars",Accent);
                Metric(new(w+8,y,w,74),all.Select(v=>v.cropDef.defName).Concat(GameComponent_CultivationJournal.Instance?.ObservedSpecies??Enumerable.Empty<string>()).Distinct(StringComparer.Ordinal).Count().ToString(),"Recorded species",Blue);
                Metric(new(2*(w+8),y,w,74),s==null?"--":(s.Ready+s.Packs).ToString(),"Awaiting action",Gold);y+=88;
                Section(view,ref y,"DISCOVERIES AWAITING ATTENTION");
                if(urgent.Length==0){Label(new(0,y,view.width,55),s==null?"Scanning visible discoveries on your current home map...":"No mature discoveries or undocumented seed packs currently need attention.",GameFont.Small,Muted);y+=55;}
                foreach(var item in urgent){DiscoveryCard(new(0,y,view.width,76),item);y+=82;}
                if(Widgets.ButtonText(new(0,y,view.width,32),s==null?"Open discovery inbox":"Open discovery inbox  ("+s.Total+")"))PreparePage("Discoveries");
                y+=45;Section(view,ref y,"YOUR LIVING COLLECTION");
                if(recent.Length==0)
                {
                    Label(new(0,y,view.width,55),"Grow plants normally. When an unusual specimen matures, preserve its seeds, then document the recovered seed pack. Named cultivars can be sown again from growing zones.",GameFont.Small,Muted);y+=62;
                }
                float cw=(view.width-8*(columns-1))/columns;
                for(int i=0;i<recent.Length;i++)
                {
                    var v=recent[i];Rect r=new((i%columns)*(cw+8),y+(i/columns)*178,cw,170);
                    DrawCollectionCard(r,v);
                }
                y+=Mathf.CeilToInt((float)recent.Length/columns)*178;
                if(Widgets.ButtonText(new(0,y,view.width,32),"Browse all cultivars"))PreparePage("Cultivars");
            }
            finally{Widgets.EndScrollView();}
        }
        private void DrawCollectionCard(Rect r,VarietyRecord v)
        {
            var info=HorticulturePresentationPolicy.ForCultivar(v);if(info==null)return;
            Fill(r,Tile);var tex=FieldGuideIndex.Portrait(v,out bool rendered);
            Rect art=new(r.x+7,r.y+7,Mathf.Min(104,r.width*.37f),108);Art(art,tex);
            Label(new(art.x,art.yMax+2,art.width,17),rendered?"MATURE PREVIEW":"SPECIES ART",GameFont.Tiny,Muted,TextAnchor.MiddleCenter);
            float x=art.xMax+9,w=r.xMax-x-8;
            Label(new(x,r.y+9,w,30),v.Label,GameFont.Small,Ink);
            Label(new(x,r.y+41,w,21),v.cropDef.LabelCap.ToString(),GameFont.Tiny,Muted);
            Label(new(x,r.y+64,w,20),info.Generation.HasValue?"Generation "+info.Generation.Value:"Generation unknown",GameFont.Tiny,Accent);
            Label(new(x,r.y+86,w,42),string.Join(", ",info.TraitNames.Take(2)),GameFont.Tiny,Muted);
            if(Widgets.ButtonText(new(r.x+7,r.yMax-33,r.width-14,27),"Open cultivar"))PrepareCultivar(v);
        }
        private void DrawDiscoveries(Rect rect)
        {
            var s=FieldGuideIndex.Current;
            float y=rect.y;Title(rect,ref y,"Discovery inbox","Visible specimens and seed packs on this home map. Genetics remain unknown until documented.");
            float bw=(rect.width-8)/3;
            string[] modes={"All","Needs attention","Growing / requested"};
            for(int i=0;i<modes.Length;i++){int k=i;if(Widgets.ButtonText(new(rect.x+i*(bw+4),y,bw,30),modes[i],true,true,discoveryFilter!=i)){discoveryFilter=k;rightScroll=Vector2.zero;}}
            y+=38;
            var rows=(s?.Discoveries??Array.Empty<FieldGuideIndex.Discovery>()).Where(r=>discoveryFilter==0||discoveryFilter==1&&(r.Status==FieldGuideIndex.State.Ready||r.Status==FieldGuideIndex.State.Recovered)||discoveryFilter==2&&r.Status is not (FieldGuideIndex.State.Ready or FieldGuideIndex.State.Recovered)).ToArray();
            Label(new(rect.x,y,rect.width,23),s==null?"Waiting for current-map snapshot":rows.Length+" shown  |  "+s.Total+" visible discoveries"+(s.Total>s.Discoveries.Length?"  |  priority list capped at 512":""),GameFont.Small,Muted);y+=29;
            var outer=new Rect(rect.x,y,rect.width,Mathf.Max(1,rect.yMax-y));var view=NativeUi.Scroll(outer,ref rightScroll,Math.Max(70,rows.Length*82));
            try
            {
                if(rows.Length==0)Label(new(0,0,view.width,70),"Nothing to show for this filter. Unusual plants become available for preservation at maturity.",GameFont.Small,Muted);
                // Only draw rows intersecting the viewport; the index is bounded.
                int first=Math.Max(0,(int)(rightScroll.y/82)-1),last=Math.Min(rows.Length,(int)((rightScroll.y+outer.height)/82)+2);
                for(int i=first;i<last;i++)DiscoveryCard(new(0,i*82,view.width,76),rows[i]);
            }
            finally{Widgets.EndScrollView();}
        }
        private int discoveryFilter;
        private void DrawBreeding(Rect rect,List<VarietyRecord> all)
        {
            var s=FieldGuideIndex.Current;var zones=s?.Zones??Array.Empty<FieldGuideIndex.GrowingZone>();
            var view=NativeUi.Scroll(rect,ref rightScroll,200+Math.Max(1,zones.Length)*96);
            try
            {
                float y=0;Title(view,ref y,"Breeding and cultivation","Manage existing growing-zone selections; compatible cultivars can cross-pollinate when sown near eligible donors.");
                Section(view,ref y,"GROWING ZONES ON THIS MAP");
                if(zones.Length==0){Label(new(0,y,view.width,70),"Create a growing zone and select a crop. Discover at least two compatible cultivars to establish a breeding mix.",GameFont.Small,Muted);y+=76;}
                foreach(var z in zones)
                {
                    var row=new Rect(0,y,view.width,88);Fill(row,Tile);Art(new(8,y+8,64,72),z.Species.uiIcon);
                    Label(new(82,y+6,view.width-258,25),z.Zone.label+"  |  "+z.Species.LabelCap,GameFont.Small,Ink);
                    var selected=z.Selected;
                    string desc=z.Mix.Length>0?"Breeding mix: "+string.Join(", ",z.Mix.Select(v=>v.Label)):HorticulturePresentationPolicy.IsDocumented(selected)?"Cultivar: "+selected.Label:"Standard crop";
                    Label(new(82,y+34,view.width-258,44),desc,GameFont.Small,z.Mix.Length>0?Accent:Muted);
                    if(Widgets.ButtonText(new(view.width-166,y+7,158,30),"Locate zone"))
                    {if(z.Zone.Map==Find.CurrentMap&&z.Zone.Cells.Count>0){Find.MainTabsRoot.SetCurrentTab(MainButtonDefOf.Inspect,false);CameraJumper.TryJump(z.Zone.Cells[0],z.Zone.Map);Find.Selector.ClearSelection();Find.Selector.Select(z.Zone);}}
                    if(Widgets.ButtonText(new(view.width-166,y+43,158,30),"Choose cultivar..."))
                    {
                        if(z.Zone.Map==Find.CurrentMap&&z.Zone.GetPlantDefToGrow()==z.Species)
                            PlantVarietySelectionUtility.OpenVarietyMenu(new List<IPlantToGrowSettable>{z.Zone},z.Species,all.Where(v=>v.cropDef==z.Species&&!v.registryArchived),_=>FieldGuideIndex.RequestRefresh());
                    }
                    y+=96;
                }
            }
            finally{Widgets.EndScrollView();}
        }
        private void DrawCultivarHeader(Rect view,ref float y,VarietyRecord v,HorticultureCultivarPresentation info)
        {
            Rect r=new(0,y,view.width,142);Fill(r,Panel);var tex=FieldGuideIndex.Portrait(v,out bool rendered);Art(new(8,y+8,120,108),tex);
            Label(new(8,y+117,120,18),rendered?"MATURE PREVIEW":"SPECIES ART",GameFont.Tiny,Muted,TextAnchor.MiddleCenter);
            Label(new(140,y+10,view.width-148,32),v.Label,GameFont.Medium,Ink);
            Label(new(140,y+45,view.width-148,26),v.cropDef.LabelCap+"  |  "+info.Origin,GameFont.Small,Muted);
            Label(new(140,y+75,view.width-148,25),info.Generation.HasValue?"Generation "+info.Generation.Value:"Generation unknown",GameFont.Small,Accent);
            Label(new(140,y+104,view.width-148,29),rendered?"Illustrative mature expression; individuals vary.":"Reference artwork; individual appearance may differ.",GameFont.Tiny,Muted);
            y+=152;
            float w=(view.width-8)/2;
            var stats=new[]{("Yield",NovelSeedUtility.YieldFactor(info.AuthorizedTraits)),("Growth rate",NovelSeedUtility.GrowthRateFactor(info.AuthorizedTraits)),("Sow work",ExpandedTraitUtility.SowWorkFactor(info.AuthorizedTraits)),("Harvest work",ExpandedTraitUtility.HarvestWorkFactor(info.AuthorizedTraits))};
            for(int i=0;i<4;i++){var stat=stats[i];bool beneficial=i<2?stat.Item2>1:stat.Item2<1;Metric(new((i%2)*(w+8),y+(i/2)*67,w,61),((stat.Item2-1)*100).ToString("+0.#;-0.#;0")+"%",stat.Item1,Mathf.Approximately(stat.Item2,1)?Muted:beneficial?Accent:Gold);}
            y+=145;
        }
    }
}
