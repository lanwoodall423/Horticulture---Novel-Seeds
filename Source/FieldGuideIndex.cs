using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace HorticultureNovelSeeds
{
    // Read-only current-map snapshot. Refreshed by GameComponentUpdate, never
    // by the window paint path. The real plant/pack remains the authority.
    internal static class FieldGuideIndex
    {
        internal enum State { Ready,Recovered,Requested,Growing,Blighted }
        internal sealed record Discovery(Thing Thing,ThingDef Species,State Status,float Growth);
        internal sealed record GrowingZone(Zone_Growing Zone,ThingDef Species,VarietyRecord Selected,VarietyRecord[] Mix);
        internal sealed record Snapshot(Map Map,Discovery[] Discoveries,GrowingZone[] Zones,int Total,int Ready,int Packs);
        private static Snapshot snapshot;
        private static Game game;
        private static bool open,refresh;
        private static int age;
        private const int MaximumRows=512;
        private const int PortraitLimit=24;
        private static readonly Dictionary<string,Texture2D> portraits=new();
        private static readonly Queue<VarietyRecord> portraitQueue=new();
        private static readonly HashSet<string> portraitRequested=new();
        internal static Snapshot Current=>snapshot?.Map==Find.CurrentMap?snapshot:null;
        internal static void Open(){open=true;refresh=true;}
        internal static void Close(){open=false;portraitQueue.Clear();portraitRequested.Clear();BreedingStudio.Clear();}
        internal static void RequestRefresh()=>refresh=true;
        private static bool Visible(Thing t,Map map)=>t?.Spawned==true&&t.Map==map&&t.Position.InBounds(map)&&!t.Position.Fogged(map)&&(t.Faction==null||t.Faction.IsPlayer);
        internal static bool TryDescribe(Thing t,Map map,out Discovery row)
        {
            row=null;
            if(map==null||!map.IsPlayerHome||!Visible(t,map))return false;
            if(t is Plant p&&HorticulturePlantPolicy.IsSupported(p.def)&&p.TryGetComp<CompPlantVariety>() is {PendingDiscovery:true} c)
            {
                var state=p.Blighted?State.Blighted:c.SaveSeedsRequested?State.Requested:p.Growth>=.999f?State.Ready:State.Growing;
                row=new(p,p.def,state,p.Growth);return true;
            }
            if(t.TryGetComp<CompNovelSeedPack>() is {Valid:true} pack&&HorticulturePlantPolicy.IsSupported(pack.CropDef))
            {
                // Already documented duplicates are not outstanding tasks.
                if(GameComponent_NovelSeeds.Instance?.FindMatchingVariety(pack.CropDef,pack.Traits)!=null)return false;
                row=new(t,pack.CropDef,State.Recovered,1f);return true;
            }
            return false;
        }
        internal static void Pump()
        {
            if(game!=Verse.Current.Game)
            {
                foreach(var t in portraits.Values)if(t!=null)UnityEngine.Object.Destroy(t);
                portraits.Clear();portraitQueue.Clear();portraitRequested.Clear();snapshot=null;game=Verse.Current.Game;refresh=true;
            }
            if(!open||game==null)return;
            BreedingStudio.Pump();
            var map=Find.CurrentMap;
            if(refresh||snapshot?.Map!=map||++age>=90)
            {
                age=0;refresh=false;
                if(map==null||!map.IsPlayerHome){snapshot=null;return;}
                var rows=new List<Discovery>();int total=0,ready=0,packs=0;
                // Count all visible entries while retaining a bounded priority
                // window. Ready and recovered entries cannot be displaced by seedlings.
                void Add(Thing t)
                {
                    if(!TryDescribe(t,map,out var row))return;
                    total++;if(row.Status==State.Ready)ready++;if(row.Status==State.Recovered)packs++;
                    rows.Add(row);
                }
                foreach(var t in map.listerThings.ThingsInGroup(ThingRequestGroup.Plant))Add(t);
                foreach(var t in map.listerThings.ThingsOfDef(HNS_DefOf.HNS_NovelSeedPack))Add(t);
                var ordered=rows.OrderBy(r=>r.Status).ThenByDescending(r=>r.Growth).ThenBy(r=>r.Thing.thingIDNumber).Take(MaximumRows).ToArray();
                var registry=GameComponent_NovelSeeds.Instance;
                var zones=map.zoneManager.AllZones.OfType<Zone_Growing>()
                    .Where(z=>HorticulturePlantPolicy.IsSupported(z.GetPlantDefToGrow()))
                    .Select(z=>new GrowingZone(z,z.GetPlantDefToGrow(),registry?.SelectedVarietyFor(z),registry?.BreedingVarietiesFor(z).Where(HorticulturePresentationPolicy.IsDocumented).ToArray()??Array.Empty<VarietyRecord>()))
                    .OrderByDescending(z=>z.Mix.Length).ThenBy(z=>z.Zone.label).Take(256).ToArray();
                snapshot=new(map,ordered,zones,total,ready,packs);
            }
            // At most one native preview per update. Never prepare from paint.
            if(portraitQueue.Count>0)
            {
                var v=portraitQueue.Dequeue();if(!HorticulturePresentationPolicy.IsDocumented(v))return;
                var key=PortraitKey(v);if(portraits.ContainsKey(key))return;
                Texture2D image=null;
                try
                {
                    string[] ids=v.traits.Select(t=>t.defName).ToArray();
                    if(BotanicalExpressionEngine.Enabled&&ExpressionMath.QualifiedSpecies(v.cropDef.defName)&&ids.Length>0&&ids.All(ExpressionMath.Known))
                    {
                        var env=SurfaceFinishMath.Environment(.5f,.95f,0);
                        if(!BotanicalExpressionEngine.Preview(v.cropDef,ids,8,1,0,new Vector4(env.X,env.Y,env.Light,env.Rain),out image,out var d)||d.Accepted.Length!=ids.Length)
                        {if(image!=null)UnityEngine.Object.Destroy(image);image=null;}
                    }
                }
                catch(Exception ex){Log.Warning("[Horticulture] Field guide preview deferred: "+ex.Message);}
                if(portraits.Count>=PortraitLimit)
                {
                    string oldest=portraits.Keys.First();
                    if(portraits[oldest]!=null)UnityEngine.Object.Destroy(portraits[oldest]);
                    portraits.Remove(oldest);portraitRequested.Remove(oldest);
                }
                portraits[key]=image;
            }
        }
        private static string PortraitKey(VarietyRecord v)=>v.id+"|"+BotanicalExpressionEngine.Enabled+"|"+NovelSeedUtility.TraitKey(v.traits);
        internal static Texture2D Portrait(VarietyRecord v,out bool rendered)
        {
            rendered=false;if(!HorticulturePresentationPolicy.IsDocumented(v))return null;
            var key=PortraitKey(v);
            if(portraits.TryGetValue(key,out var image)&&image!=null){rendered=true;return image;}
            if(!portraits.ContainsKey(key)&&portraitQueue.Count<PortraitLimit&&portraitRequested.Add(key))portraitQueue.Enqueue(v);
            return v.cropDef.uiIcon;
        }
        internal static bool Preserve(Thing thing)
        {
            if(!TryDescribe(thing,Find.CurrentMap,out var row)||row.Status!=State.Ready)return false;
            bool result=thing.TryGetComp<CompPlantVariety>().TryRequestSaveSeeds();RequestRefresh();return result;
        }
        internal static bool OrderDocumentation(Thing thing,Pawn pawn)
        {
            if(!TryDescribe(thing,Find.CurrentMap,out var row)||row.Status!=State.Recovered||pawn?.Faction?.IsPlayer!=true||
                !pawn.Spawned||pawn.Map!=thing.Map||pawn.Dead||pawn.Downed||pawn.InMentalState||thing.IsForbidden(pawn)||
                !pawn.CanReserveAndReach(thing,PathEndMode.Touch,Danger.Deadly))return false;
            return pawn.jobs.TryTakeOrderedJob(JobMaker.MakeJob(HNS_DefOf.HNS_UnlockVariety,thing),JobTag.Misc);
        }
        internal static void ChooseDocumenter(Thing thing)
        {
            if(!TryDescribe(thing,Find.CurrentMap,out var row)||row.Status!=State.Recovered)return;
            var options=thing.Map.mapPawns.FreeColonistsSpawned.Where(p=>!p.Dead&&!p.Downed&&!p.InMentalState&&!thing.IsForbidden(p)&&p.CanReserveAndReach(thing,PathEndMode.Touch,Danger.Deadly))
                .OrderBy(p=>p.LabelShort).Select(p=>new FloatMenuOption(p.LabelShort,()=>{if(!OrderDocumentation(thing,p))Messages.Message("Documentation is no longer available.",MessageTypeDefOf.RejectInput,false);})).ToList();
            if(options.Count==0)options.Add(new FloatMenuOption("No available colonist can reach this seed pack",null));
            Find.WindowStack.Add(new FloatMenu(options));
        }
    }

    public sealed class GameComponent_FieldGuide : GameComponent
    {
        public GameComponent_FieldGuide(Game game){}
        public override void GameComponentUpdate()=>FieldGuideIndex.Pump();
    }
}
