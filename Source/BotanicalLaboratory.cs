using System;
using System.Linq;
using LudeonTK;
using RimWorld;
using UnityEngine;
using Verse;
using E=HorticultureNovelSeeds.ExpressionMath;

namespace HorticultureNovelSeeds
{
    public sealed class Dialog_BotanicalLaboratory:Window
    {
        private static readonly string[] Species={"Plant_Corn","Plant_Cotton","Plant_Daylily"};
        private static readonly string[] Presets={"Field lineage","Garden lineage","Collector lineage"};
        private int species,recipe,seed=1,band=8;
        private float bias,day=.5f,rain;
        private Texture2D preview;
        private E.Decision decision;
        private string signature;
        private string[] selectedIds;
        private ThingDef selectedDef;
        private bool selected,custom,compareSource;
        public override Vector2 InitialSize=>new(790,640);
        public Dialog_BotanicalLaboratory()
        {
            doCloseX=true;doCloseButton=true;absorbInputAroundWindow=true;
            var p=Find.Selector.SingleSelectedThing as Plant;
            var c=p?.TryGetComp<CompPlantVariety>();
            if(c?.Variety!=null&&!c.PendingDiscovery&&c.ActiveTraits.All(t=>E.Known(t.defName)))
            {selectedDef=p.def;selectedIds=c.ActiveTraits.Select(t=>E.Id(t.defName)).ToArray();selected=true;seed=E.Seed(p.thingIDNumber);band=E.Band(p.Growth);bias=E.HistoryBias(c.VisualChronicle);}
        }
        [DebugAction("Horticulture - Novel Seeds","Botanical laboratory",actionType=DebugActionType.Action,allowedGameStates=AllowedGameStates.Playing)]
        private static void Open()=>Find.WindowStack.Add(new Dialog_BotanicalLaboratory());
        private string[] Ids=>selected?selectedIds:E.Recipe(species,recipe);
        private ThingDef Def=>selected?selectedDef:DefDatabase<ThingDef>.GetNamed(Species[species]);
        private void SelectChannel(E.Channel channel,string id)
        {
            selectedDef=Def;selectedIds=Ids.Where(i=>!E.Library.Any(a=>a.Id==i&&a.Channel==channel)).Concat(string.IsNullOrEmpty(id)?Array.Empty<string>():new[]{id}).ToArray();
            selected=true;custom=true;signature=null;
        }
        private void EditChannels()
        {
            Find.WindowStack.Add(new FloatMenu(Enum.GetValues<E.Channel>().Select(channel=>new FloatMenuOption(channel.ToString(),()=>
            {
                var options=E.Library.Where(a=>a.Channel==channel&&a.Families.Contains(E.FamilyOf(Def.defName))).Select(a=>new FloatMenuOption(a.Id+"  /  "+a.Tier,()=>SelectChannel(channel,a.Id))).ToList();
                options.Insert(0,new FloatMenuOption("None",()=>SelectChannel(channel,null)));
                Find.WindowStack.Add(new FloatMenu(options));
            })).ToList()));
        }
        private void UpdatePreview()
        {
            string key=Def.defName+"|"+string.Join(",",Ids)+"|"+band+"|"+seed+"|"+bias.ToString("F2")+"|"+day.ToString("F2")+"|"+rain.ToString("F2");
            if(signature==key)return;signature=key;
            if(preview!=null)UnityEngine.Object.Destroy(preview);preview=null;
            var e=SurfaceFinishMath.Environment(day,.95f,rain);
            BotanicalExpressionEngine.Preview(Def,Ids,band,seed,bias,new Vector4(e.X,e.Y,e.Light,e.Rain),out preview,out decision);
        }
        public override void DoWindowContents(Rect r)
        {
            var old=Text.Font;var oldAnchor=Text.Anchor;
            try
            {
                Text.Font=GameFont.Medium;Widgets.Label(new Rect(0,0,r.width,30),"Botanical laboratory");
                Text.Font=GameFont.Small;Widgets.Label(new Rect(0,35,r.width,42),"Read-only native material preview. Experimental channels are not enabled in ordinary gameplay.");
                if(Widgets.ButtonText(new Rect(0,82,212,28),selected?Def.LabelCap.ToString():Species[species].Replace("Plant_","")))
                    Find.WindowStack.Add(new FloatMenu(Species.Select((s,i)=>new FloatMenuOption(s.Replace("Plant_",""),()=>{species=i;selected=false;custom=false;})).ToList()));
                if(Widgets.ButtonText(new Rect(220,82,235,28),custom?"Custom channel combination":selected?"Selected documented cultivar":Presets[recipe]))
                    Find.WindowStack.Add(new FloatMenu(Presets.Select((s,i)=>new FloatMenuOption(s,()=>{recipe=i;selected=false;custom=false;})).ToList()));
                if(Widgets.ButtonText(new Rect(465,82,127,28),"Next individual"))seed=(seed+1)%8;
                if(Widgets.ButtonText(new Rect(600,82,145,28),"Edit channels"))EditChannels();
                Rect art=new(0,124,340,340);Widgets.DrawBoxSolid(art,new Color(.10f,.15f,.12f));Widgets.DrawBoxSolid(art.ContractedBy(3),Color.black);
                UpdatePreview();if(compareSource&&Def.uiIcon!=null)GUI.DrawTexture(art.ContractedBy(10),Def.uiIcon,ScaleMode.ScaleToFit);
                else if(preview!=null)GUI.DrawTexture(art.ContractedBy(10),preview,ScaleMode.ScaleToFit);
                else Widgets.Label(art.ContractedBy(15),"No qualified preview for this combination.");
                float x=358,w=r.width-x,y=124;
                Widgets.Label(new Rect(x,y,w,24),"Growth phase  "+(band*12.5f).ToString("0")+"%");y+=25;
                band=Mathf.RoundToInt(Widgets.HorizontalSlider(new Rect(x,y,w,20),band,0,8));y+=30;
                Widgets.Label(new Rect(x,y,w,24),"Individual seed  "+seed);y+=25;
                seed=Mathf.RoundToInt(Widgets.HorizontalSlider(new Rect(x,y,w,20),seed,0,7));y+=30;
                Widgets.Label(new Rect(x,y,w,24),"Recorded/context bias (preview)");y+=25;
                bias=Widgets.HorizontalSlider(new Rect(x,y,w,20),bias,-1,1);y+=30;
                Widgets.Label(new Rect(x,y,w,24),"Light direction / time of day");y+=25;
                day=Widgets.HorizontalSlider(new Rect(x,y,w,20),day,0,1);y+=30;
                Widgets.Label(new Rect(x,y,w,24),"Rain input");y+=25;
                rain=Widgets.HorizontalSlider(new Rect(x,y,w,20),rain,0,1);y+=30;
                Text.Font=GameFont.Tiny;Widgets.Label(new Rect(x,y,w,65),"Accepted: "+string.Join(", ",decision?.Accepted??Array.Empty<string>())+"\nDeferred: "+string.Join("; ",decision?.Rejected??Array.Empty<string>()));
                Text.Font=GameFont.Small;
                Widgets.Label(new Rect(0,473,r.width,40),"Material budget: "+(BotanicalExpressionEngine.Bytes/1048576f).ToString("0.0")+" / 64 MiB  |  Shared materials: "+BotanicalExpressionEngine.MaterialCount);
                if(Widgets.ButtonText(new Rect(230,517,220,30),compareSource?"Show composed preview":"Show species reference"))compareSource=!compareSource;
                if(Widgets.ButtonText(new Rect(0,517,220,30),"Export preview PNG")&&preview!=null)
                {
                    string path=System.IO.Path.Combine(GenFilePaths.SaveDataFolderPath,"HNS-BotanicalLab-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+".png");
                    System.IO.File.WriteAllBytes(path,preview.EncodeToPNG());Messages.Message("Botanical preview saved: "+path,MessageTypeDefOf.TaskCompletion,false);
                }
#if HNS_NATIVE_FIXTURE
                if(preview!=null){LastContents=true;if(Event.current.type==EventType.Repaint)LastDrawn=true;}
#endif
            }
            finally{Text.Font=old;Text.Anchor=oldAnchor;GUI.color=Color.white;}
        }
#if HNS_NATIVE_FIXTURE
        internal static bool LastDrawn,LastContents;
        internal bool VerifyPreview(string directory)
        {
            UpdatePreview();
            if(preview==null||decision==null||decision.Plan.Count!=5||preview.GetPixels32().Count(c=>c.r+c.g+c.b>24)<30)return false;
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(directory,"HNS-garden-expression-lab-preview.png"),preview.EncodeToPNG());
            string original=signature;SelectChannel(E.Channel.Tissue,"IvoryNerves");UpdatePreview();
            if(preview==null||signature==original||decision.Plan.Tissue!=1||decision.Plan.Count!=5)return false;
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(directory,"HNS-garden-expression-lab-edited.png"),preview.EncodeToPNG());return true;
        }
#endif
        public override void PostClose(){if(preview!=null)UnityEngine.Object.Destroy(preview);base.PostClose();}
    }
}
