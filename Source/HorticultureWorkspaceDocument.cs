using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace HorticultureNovelSeeds
{
    // Native field guide; journal facts and registry remain outside this window.
    public sealed partial class HorticultureWorkspaceDocument
    {
        public const int MaximumComparisonCount = 8;
        public const int MaximumLineageDepth = 12;
        private string page = "Overview";
        private ThingDef selectedPlant;
        private VarietyRecord selectedCultivar;
        private string search = "";
        private bool archived;
        private bool lineage;
        private bool compare;
        private readonly HashSet<string> comparisonIds = new HashSet<string>();
        private Vector2 leftScroll, rightScroll;
        private Game lastGame;

        public void PreOpen()
        {
            if (!ReferenceEquals(lastGame, Current.Game))
            {
                lastGame = Current.Game; page = "Overview"; search = ""; selectedPlant = null;
                selectedCultivar = null; comparisonIds.Clear(); lineage = compare = false;
                leftScroll = rightScroll = Vector2.zero;
            }
        }
        public void PostClose() { FieldGuideIndex.Close(); }
        internal void Opened(){PreOpen();FieldGuideIndex.Open();}
        internal void PrepareDiscoveries()=>PreparePage("Discoveries");
        internal void PrepareBreeding()=>PreparePage("Breeding");
#if HNS_NATIVE_FIXTURE
        internal static string LastDrawnPage;internal static int LastDrawnCount;
        internal void FixtureOverview()=>PreparePage("Overview");
        internal void FixtureCompare(IEnumerable<VarietyRecord> vs){PreparePage("Cultivars");comparisonIds.Clear();foreach(var v in vs)comparisonIds.Add(v.id);OpenCompare();}
#endif
        public void PrepareKnowledge(Pawn pawn) { PreparePage("Overview"); }
        public void PreparePlant(ThingDef plant)
        {
            if (!HorticulturePlantPolicy.IsSupported(plant)) return;
            PreparePage("Plants"); selectedPlant = plant;
        }
        public void PrepareCultivar(VarietyRecord variety)
        {
            if (!HorticulturePresentationPolicy.IsDocumented(variety)) return;
            PreparePage("Cultivars"); selectedCultivar = variety; archived |= variety.registryArchived;
        }
        public void PrepareLineage(VarietyRecord variety) { PrepareCultivar(variety); lineage = selectedCultivar == variety; }
        public void OpenCompare() { if (comparisonIds.Count >= 2) compare = true; }
        private void PreparePage(string next)
        {
            page = next; search = ""; compare = lineage = false; leftScroll = rightScroll = Vector2.zero;
        }

        public void Draw(Rect rect)
        {
            PreOpen();
            var component = GameComponent_NovelSeeds.Instance;
            if (component == null) { Widgets.Label(rect, "No active colony."); return; }
            var all = component.AllVarieties.Where(HorticulturePresentationPolicy.IsDocumented).ToList();
            comparisonIds.RemoveWhere(id => !all.Any(v => v.id == id));
            if (!HorticulturePresentationPolicy.IsDocumented(selectedCultivar)) selectedCultivar = null;
            string[] tabs = { "Overview", "Discoveries", "Plants", "Cultivars", "Breeding" };
            float width = rect.width / tabs.Length;
            for (int i = 0; i < tabs.Length; i++)
            {
                Rect tab=new(rect.x+width*i,rect.y,width-4,32);if(page==tabs[i])Fill(tab,Tile);
                if(Widgets.ButtonText(tab,tabs[i]))PreparePage(tabs[i]);
                if(page==tabs[i])Fill(new(tab.x,tab.yMax-3,tab.width,3),Accent);
            }
            var content = new Rect(rect.x, rect.y + 40, rect.width, rect.height - 40);
            if (Event.current.type==EventType.Repaint)
            {
#if HNS_NATIVE_FIXTURE
                LastDrawnPage=compare?"Compare":page;LastDrawnCount++;
#endif
            }
            if(page=="Overview"){DrawOverview(content,all);return;}
            if(page=="Discoveries"){DrawDiscoveries(content);return;}
            if(page=="Breeding"){DrawBreeding(content,all);return;}
            float leftWidth = Mathf.Clamp(content.width * .34f, 170, 360);
            Rect left = new Rect(content.x, content.y, leftWidth, content.height);
            Rect right = new Rect(left.xMax + 12, content.y, content.width - leftWidth - 12, content.height);
            float ly = left.y;
            NativeUi.Label(left, ref ly, "Search", 22);
            search = NativeUi.Field(left, ref ly, search);
            if (page == "Plants")
            {
                var plants = DefDatabase<ThingDef>.AllDefsListForReading.Where(p => HorticulturePresentationPolicy.HasPlantEvidence(p) || p == selectedPlant)
                    .Where(p => Matches(p.LabelCap.ToString())).OrderBy(p => p.label).Take(1000).ToArray();
                var list = NativeUi.Scroll(new Rect(left.x, ly, left.width, left.yMax - ly), ref leftScroll, plants.Length * 34);
                try { float y = 0; foreach (var p in plants) NativeUi.Button(list, ref y, p.LabelCap.ToString(), () => { selectedPlant = p; rightScroll = Vector2.zero; }); }
                finally { Widgets.EndScrollView(); }
                DrawPlant(right, all);
            }
            else
            {
                NativeUi.Toggle(left, ref ly, "Show archived", archived, v => archived = v);
                NativeUi.Button(left, ref ly, "Compare (" + comparisonIds.Count + ")", comparisonIds.Count >= 2 ? (Action)(() => compare = true) : null);
                var rows = all.Where(v => (archived || !v.registryArchived) && Matches(v.Label + " " + v.cropDef.LabelCap + " " +
                        HorticulturePresentationPolicy.ForCultivar(v)?.TraitText))
                    .OrderByDescending(v => v.registryFavorite).ThenBy(v => v.Label).Take(1000).ToArray();
                var list = NativeUi.Scroll(new Rect(left.x, ly, left.width, left.yMax - ly), ref leftScroll, rows.Length * 34);
                try
                {
                    float y = 0;
                    foreach (var v in rows)
                    {
                        var row = NativeUi.Row(list, ref y);
                        if (Widgets.ButtonText(new Rect(row.x, row.y, row.width - 42, row.height), (v.registryFavorite ? "* " : "") + v.Label))
                        { selectedCultivar = v; compare = lineage = false; rightScroll = Vector2.zero; }
                        bool included = comparisonIds.Contains(v.id);
                        bool next = included;
                        Widgets.Checkbox(new Vector2(row.xMax - 29, row.y + 3), ref next);
                        if (next != included)
                        {
                            if (!next) comparisonIds.Remove(v.id);
                            else if (comparisonIds.Count < MaximumComparisonCount) comparisonIds.Add(v.id);
                        }
                    }
                }
                finally { Widgets.EndScrollView(); }
                if (compare) DrawComparison(right, all);
                else DrawCultivar(right);
            }
        }
        private bool Matches(string value) => string.IsNullOrWhiteSpace(search) ||
            (value ?? "").IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;

        private void DrawPlant(Rect rect, List<VarietyRecord> all)
        {
            if (selectedPlant == null) { Widgets.Label(rect, "Select an observed plant."); return; }
            var selected = all.Where(v => v.cropDef == selectedPlant).Take(1000).ToArray();
            var view = NativeUi.Scroll(rect, ref rightScroll, 240 + selected.Length * 34);
            try
            {
                float y = 0;
                NativeUi.Label(view, ref y, selectedPlant.LabelCap.ToString());
                bool? available = CropProgressionCompatibility.IsAvailable(selectedPlant);
                NativeUi.Label(view, ref y, available.HasValue ? (available.Value ? "Crop progression: unlocked" : "Crop progression: locked") : "Sowing availability follows the active game rules.");
                DrawJournal(view, ref y, GameComponent_CultivationJournal.Instance?.ForSpecies(selectedPlant));
                foreach (var v in selected) NativeUi.Button(view, ref y, v.Label, () => PrepareCultivar(v));
            }
            finally { Widgets.EndScrollView(); }
        }

        private void DrawCultivar(Rect rect)
        {
            var v = selectedCultivar;
            var info = HorticulturePresentationPolicy.ForCultivar(v);
            if (info == null) { Widgets.Label(rect, "Select a documented cultivar."); return; }
            string potential=info.PotentialAppearance;
            string potentialText=string.IsNullOrEmpty(potential)?null:"Potential appearance (genetic tendencies; expression varies):\n"+potential;
            float potentialHeight=potentialText==null?0f:Mathf.Max(48f,Text.CalcHeight(potentialText,Mathf.Max(1f,rect.width-18f))+8f);
            var view = NativeUi.Scroll(rect, ref rightScroll, 1080 + potentialHeight + info.AuthorizedTraits.Count * 66 + (lineage ? 128 * 34 : 0));
            try
            {
                float y = 0;
                DrawCultivarHeader(view,ref y,v,info);
                NativeUi.Label(view, ref y, v.FirstDiscoveredInfo, 42);
                if(Widgets.ButtonText(new(0,y,view.width,30),"Locate specimen / produce"))Locate(v);y+=38;
                if(Widgets.ButtonText(new(0,y,view.width,30),"View growing zones / breeding"))PrepareBreeding();y+=38;
                Section(view,ref y,"DOCUMENTED GENETIC TENDENCIES");
                if(potentialText!=null)NativeUi.Label(view,ref y,potentialText,potentialHeight);
                var agricultural=info.AuthorizedTraits.Where(t=>!OrnamentalInheritance.IsAppearance(t)).ToArray();
                var ornamental=info.AuthorizedTraits.Where(OrnamentalInheritance.IsAppearance).ToArray();
                if(agricultural.Length>0)Section(view,ref y,"AGRICULTURAL / OTHER INHERITANCE");
                foreach(var trait in agricultural)NativeUi.Label(view,ref y,TraitColorUI.Label(trait)+": "+TraitColorUI.Description(trait),62);
                if(ornamental.Length>0)Section(view,ref y,"ORNAMENTAL INHERITANCE");
                foreach(var trait in ornamental)NativeUi.Label(view,ref y,TraitColorUI.Label(trait)+": "+TraitColorUI.Description(trait),62);
                DrawJournal(view, ref y, info.Journal);
                NativeUi.Button(view, ref y, "Rename", () => Find.WindowStack.Add(new Dialog_RenameVariety(v)));
                NativeUi.Button(view, ref y, v.registryFavorite ? "Remove favorite" : "Favorite", () => v.registryFavorite = !v.registryFavorite);
                NativeUi.Button(view, ref y, v.registryArchived ? "Restore" : "Archive", () => v.registryArchived = !v.registryArchived);

                NativeUi.Button(view, ref y, lineage ? "Hide pedigree" : "Show pedigree", () => lineage = !lineage);
                if (lineage)
                {
                    var seen = new HashSet<string>(StringComparer.Ordinal) { v.id };
                    int budget = 128;
                    DrawParents(view, ref y, info, seen, 0, ref budget);
                }
            }
            finally { Widgets.EndScrollView(); }
        }
        private static void DrawParents(Rect view, ref float y, HorticultureCultivarPresentation info, HashSet<string> seen, int depth, ref int budget)
        {
            if (depth >= MaximumLineageDepth || budget <= 0) return;
            foreach (var parent in info.Parents)
            {
                if (budget-- <= 0) break;
                NativeUi.Label(view, ref y, new string(' ', depth * 2) + parent.Label);
                if (parent.IsKnown && seen.Add(parent.Record.id))
                {
                    var next = HorticulturePresentationPolicy.ForCultivar(parent.Record);
                    if (next != null) DrawParents(view, ref y, next, seen, depth + 1, ref budget);
                }
            }
        }
        private void DrawComparison(Rect rect,List<VarietyRecord> all)
        {
            var chosen=all.Where(v=>comparisonIds.Contains(v.id)).Take(MaximumComparisonCount).ToArray();
            int cols=rect.width>=650?2:1;
            var view=NativeUi.Scroll(rect,ref rightScroll,100+Mathf.CeilToInt((float)chosen.Length/cols)*290);
            try
            {
                float y=0;Title(view,ref y,"Compare cultivars","Genetic modifiers, not measured harvest predictions.");
                if(Widgets.ButtonText(new(0,y,view.width,30),"Back to cultivar"))compare=false;y+=38;
                float w=(view.width-8*(cols-1))/cols;
                for(int i=0;i<chosen.Length;i++)
                {
                    var v=chosen[i];var info=HorticulturePresentationPolicy.ForCultivar(v);if(info==null)continue;
                    Rect r=new((i%cols)*(w+8),y+(i/cols)*290,w,282);Fill(r,Tile);
                    Art(new(r.x+8,r.y+8,80,76),FieldGuideIndex.Portrait(v,out bool rendered));
                    Label(new(r.x+8,r.y+85,80,18),rendered?"PREVIEW":"SPECIES ART",GameFont.Tiny,Muted,TextAnchor.MiddleCenter);
                    Label(new(r.x+96,r.y+10,r.width-104,34),v.Label,GameFont.Small,Ink);
                    Label(new(r.x+96,r.y+48,r.width-104,40),v.cropDef.LabelCap.ToString(),GameFont.Small,Muted);
                    Label(new(r.x+9,r.y+108,r.width-18,64),info.ModifierText,GameFont.Small,Accent);
                    Label(new(r.x+9,r.y+176,r.width-18,58),info.TraitText,GameFont.Small,Ink);
                    if(Widgets.ButtonText(new(r.x+8,r.yMax-35,r.width-16,28),"Open cultivar"))PrepareCultivar(v);
                }
            }
            finally{Widgets.EndScrollView();}
        }
        private static void DrawJournal(Rect view, ref float y, CultivationJournalRecord facts)
        {
            NativeUi.Label(view, ref y, facts == null ? "No cultivation observations recorded." :
                "Recorded sowings: " + facts.sowings + " ? Confirmed harvests: " + facts.harvests, 42);
            NativeUi.Label(view, ref y, facts?.minYield >= 0 ? "Observed primary yield: " + facts.minYield + "?" + facts.maxYield : "Observed yield: not measured");
        }
        private static void Locate(VarietyRecord variety)
        {
            var target = Find.CurrentMap?.listerThings?.AllThings.FirstOrDefault(t =>
                (t.TryGetComp<CompPlantVariety>() is CompPlantVariety plant && !plant.PendingDiscovery && plant.VarietyId == variety.id) ||
                t.TryGetComp<CompNovelProduceAppearance>()?.SourceVarietyIds?.Contains(variety.id) == true);
            if (target != null) LocateThing(target);
            else Messages.Message("No matching plant or produce on the current map.", MessageTypeDefOf.NeutralEvent, false);
        }
    }
}
