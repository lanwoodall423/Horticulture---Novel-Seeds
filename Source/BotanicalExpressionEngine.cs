using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using E=HorticultureNovelSeeds.ExpressionMath;
using V=HorticultureNovelSeeds.PhenotypeVocabulary;

namespace HorticultureNovelSeeds
{
    // Opt-in composition engine. Legacy appearance paths stay intact when
    // unknown traits, texture transforms, seasonal material or budgets fail.
    internal static class BotanicalExpressionEngine
    {
        private static bool? testOverride;
        internal static bool Enabled {get=>testOverride??HorticultureNovelSeedsMod.Settings?.enableBotanicalExpression==true;set=>testOverride=value;}
        private readonly record struct WorkKey(ThingDef Def,string Architecture,bool Fuller);
        private static readonly Queue<WorkKey> work=new();
        private static readonly Dictionary<WorkKey,HashSet<Plant>> waiting=new();
        private static readonly HashSet<WorkKey> ready=new();
        internal static int PendingCount=>work.Count;
        internal static int PrepareJobs{get;private set;}
        internal static double MaxPrepareMilliseconds{get;private set;}
        private const long Budget=64L*1024*1024;
        private const int MaterialLimit=160;
        private sealed record SourceData(Texture2D Mask,LifeSuiteMath.Anatomy Anatomy,OrganSpriteMath.Pixel[] Pixels,Dictionary<int,Texture2D> Atlases);
        private static readonly Dictionary<Texture,SourceData> sources=new();
        private static readonly Dictionary<(Texture,E.Plan,AdaptivePhenotypeMath.State),Material> materials=new();
        private static long bytes;
        private static Vector4 last=new(-1,-1,-1,-1);
        internal static long Bytes=>bytes;
        internal static int SourceCount=>sources.Count;
        internal static int MaterialCount=>materials.Count;
#if HNS_NATIVE_FIXTURE
        internal sealed record Print(Material Material,E.Decision Decision,int Band,int Seed,float Bias,int Code,Color32 Payload,bool Flipped,AdaptivePhenotypeMath.State Function);
        internal static readonly Dictionary<int,Print> Prints=new();
        internal static readonly HashSet<(int,int)> Invalidations=new();
        internal static readonly Dictionary<int,bool> MeshPayload=new();
        internal static void RecordMesh(Plant p,SectionLayer layer,Material m)
        {
            var sub=layer.GetSubMesh(m);var print=Prints[p.thingIDNumber];
            MeshPayload[p.thingIDNumber]=sub.colors.Count>=4&&sub.colors.Skip(sub.colors.Count-4).All(c=>c.r==print.Payload.r&&c.g==print.Payload.g&&c.b==print.Payload.b)&&sub.uvs.Skip(sub.uvs.Count-4).All(u=>u.z==p.HashOffset()%1024);
        }
#endif
        private static bool LegacyCompatible(VarietyTraitDef t)=>t!=null&&(t.produceOnlyVisual||E.Known(t.defName)||t.proceduralPhenotype==0&&t.plantForm==0&&t.organForm==0&&t.windHabit==0&&t.surfaceFinish==0&&t.botanicalDetail==0&&t.canopyArchitecture==0&&t.responsiveHabit==0&&t.groundHabit==0&&t.visualMaskIndex<0);
        private static IEnumerable<string> Promoted(CompPlantVariety c)=>c.ActiveTraits.Where(t=>t!=null&&!t.produceOnlyVisual&&E.Known(t.defName)).Select(t=>t.defName);
        internal static AdaptivePhenotypeMath.Cue FunctionalCues(CompPlantVariety comp)
        {
            if(comp?.HasAnyTraits!=true)return 0;
            // The new layer must not replace a custom visual or unrelated
            // legacy pigmentation. Keep their established paths intact.
            var traits=comp.ActiveTraits.Where(t=>t!=null&&!t.produceOnlyVisual).ToArray();
            if(traits.Any(t=>!E.Known(t.defName)&&AdaptivePhenotypeMath.ForTrait(t.defName)==0))return 0;
            if(traits.Any(t=>HorticultureNovelSeedsMod.Settings?.VisualOverrideFor(comp.parent.def,t)!=null))return 0;
            return AdaptivePhenotypeMath.From(traits.Select(t=>t.defName));
        }
        internal static bool CanAttempt(CompPlantVariety comp)=>Enabled&&comp?.HasAnyTraits==true&&E.QualifiedSpecies(comp.parent.def.defName)&&comp.ActiveTraits.All(LegacyCompatible)&&(comp.ActiveTraits.Any(t=>!t.produceOnlyVisual&&E.Known(t.defName))||FunctionalCues(comp)!=0);
        internal static void ResourceChanged(CompPlantVariety c)
        {
            if(Enabled&&c?.parent is Plant p&&p.Spawned&&FunctionalCues(c)!=0)p.DirtyMapMesh(p.Map);
        }
        internal static bool CompatibleVisual(CompPlantVariety c,PlantVisualParameters v)
        {
            var functional=FunctionalCues(c);
            // Catalog defaults can set whole-sprite brightness/saturation
            // and therefore appear in the generic mask-color query. Only
            // bypass that query for the fully qualified no-override group;
            // real regional coloration and all custom overrides still defer.
            if(!CanAttempt(c)||NovelSeedUtility.HasRegionalPlantColor(c)||functional==0&&NovelSeedUtility.HasPlantMaskVisual(c))return false;
            // Geometry is already applied by the native plane construction.
            // Never discard legacy texture/overlay coloration or custom masks.
            v.scale=v.width=v.height=v.density=v.spread=v.shadowScale=1f;
            v.rotation=v.rotationVariation=v.scaleVariation=v.offsetX=v.offsetZ=0f;
            // Retain the native geometry of existing gameplay traits. Replace
            // only their default catalog fallback tint/brightness/saturation with the new
            // anatomical cue; explicit user visual overrides are excluded.
            if(functional!=0){v.brightness=v.saturation=v.tintRed=v.tintGreen=v.tintBlue=1f;}
            return v.IsDefault;
        }
        internal static void Request(Plant p)
        {
            if(testOverride.HasValue||p?.Spawned!=true)return;
            var c=p.TryGetComp<CompPlantVariety>();if(!CanAttempt(c))return;
            var d=E.Resolve(Promoted(c),E.FamilyOf(p.def.defName),int.MaxValue);
            var arch=d.Accepted.FirstOrDefault(id=>E.ChannelTag(id)=="HNSExpression_Architecture")??"";
            var key=new WorkKey(p.def,arch,arch.Length==0&&(FunctionalCues(c)&AdaptivePhenotypeMath.Cue.Yield)!=0);if(ready.Contains(key))return;
            if(!waiting.TryGetValue(key,out var set))
            {
                if(work.Count>=128)return;
                waiting.Add(key,set=new());work.Enqueue(key);
            }
            if(set.Count<8192)set.Add(p);
        }
        internal static void ScanMapPlants()
        {
            if(!Enabled||testOverride.HasValue||Current.Game==null)return;
            foreach(var map in Find.Maps)foreach(var p in map.listerThings.AllThings.OfType<Plant>())Request(p);
        }
        internal static void Pump()
        {
            if(!Enabled||testOverride.HasValue||work.Count==0)return;
            var key=work.Dequeue();if(!waiting.Remove(key,out var set))return;
            var watch=System.Diagnostics.Stopwatch.StartNew();
            try{Prepare(key.Def,key.Architecture.Length==0?Array.Empty<string>():new[]{key.Architecture},key.Fuller);}
            catch(Exception ex){Log.Warning("[HNS Expression] Deferred source preparation: "+key.Def.defName+" "+ex.Message);}
            finally{watch.Stop();PrepareJobs++;MaxPrepareMilliseconds=Math.Max(MaxPrepareMilliseconds,watch.Elapsed.TotalMilliseconds);ready.Add(key);}
            foreach(var p in set)if(p?.Spawned==true&&CanAttempt(p.TryGetComp<CompPlantVariety>()))p.DirtyMapMesh(p.Map);
        }
        internal static void ClearPending(){work.Clear();waiting.Clear();}
        internal static void Prepare(ThingDef def,IEnumerable<string> ids,bool fuller=false)
        {
            var names=ids.ToArray();
            for(int v=0;v<PlantMaskUtility.VariationCount(def);v++)
            {
                if(PlantMaskUtility.TextureForVariation(def,v) is not Texture2D source)continue;
                PrepareTexture(source,E.FamilyOf(def.defName),names,fuller);
            }
        }
        private static SourceData PrepareTexture(Texture2D source,E.Family family,string[] ids,bool fuller)
        {
            if(!sources.TryGetValue(source,out var a))
            {
                int w=source.width,h=source.height;
                if(w<16||h<16||w>128||h>128||sources.Count>=48||bytes+4L*w*h>Budget)return null;
                var pixels=OrganSpriteExperiment.Read(source,w,h).Select(c=>new OrganSpriteMath.Pixel(c.r,c.g,c.b,c.a)).ToArray();
                var anatomy=LifeSuiteMath.Analyze(pixels,w,h);if(anatomy==null)return null;
                var mask=new Texture2D(w,h,TextureFormat.RGBA32,false,true){name=source.name+"_HNS_ExpressionAnatomy",filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};
                mask.SetPixels32(anatomy.Mask.Select(c=>new Color32(c.R,c.G,c.B,c.A)).ToArray());mask.Apply(false,true);
                a=new(mask,anatomy,pixels,new());sources.Add(source,a);bytes+=4L*w*h;
            }
            var decision=E.Resolve(ids,family,a.Anatomy.Organs);int arch=decision.Plan.Architecture;
            if(arch==0&&fuller&&a.Anatomy.Organs>0)arch=AdaptivePhenotypeMath.FullerArchitecture;
            if(decision.Unknown||arch==0||a.Atlases.ContainsKey(arch))return a;
            int W=SculptureMath.Side(a.Anatomy.W),H=SculptureMath.Side(a.Anatomy.H),w0=a.Anatomy.W,h0=a.Anatomy.H;
            long needed=36L*W*H;if(bytes+needed>Budget)return a;
            var data=new Color32[9*W*H];int ox=(W-w0)/2,oy=(H-h0)/2;
            bool fullerChanged=false;int originalOccupied=a.Pixels.Count(p=>p.A>0);
            for(int f=0;f<9;f++)
            {
                var frame=arch==AdaptivePhenotypeMath.FullerArchitecture?AdaptivePhenotypeMath.Fuller(a.Pixels,a.Anatomy,f):SculptureMath.Frame(a.Pixels,w0,h0,a.Anatomy,arch,f);
                if(frame==null)return a;
                if(arch==AdaptivePhenotypeMath.FullerArchitecture&&f==8)fullerChanged=frame.Count(p=>p.A>0)>originalOccupied;
                for(int y=0;y<H;y++)for(int x=0;x<W;x++)
                {
                    var c=frame[y*W+x];if(x>=ox&&x<ox+w0&&y>=oy&&y<oy+h0)
                    {
                        var p=a.Pixels[(y-oy)*w0+x-ox];
                        if(p.A>0&&p!=c)throw new InvalidOperationException("Expression structure overwrote native tissue");
                    }
                    data[(y+f/3*H)*3*W+x+f%3*W]=new(c.R,c.G,c.B,c.A);
                }
            }
            if(arch==AdaptivePhenotypeMath.FullerArchitecture&&!fullerChanged)return a;
            var atlas=new Texture2D(W*3,H*3,TextureFormat.RGBA32,false){name=source.name+"_HNS_ExpressionArchitecture_"+arch,filterMode=source.filterMode,wrapMode=TextureWrapMode.Clamp};
            atlas.SetPixels32(data);atlas.Apply(false,true);a.Atlases.Add(arch,atlas);bytes+=needed;
            return a;
        }
        private static Material GetMaterial(Texture source,SourceData a,E.Plan plan,AdaptivePhenotypeMath.State function=default)
        {
            if(materials.TryGetValue((source,plan,function),out var mat))return mat;
            if(materials.Count>=MaterialLimit||plan.Architecture>0&&!a.Atlases.ContainsKey(plan.Architecture))return null;
            var shader=DefDatabase<ShaderDef>.GetNamedSilentFail("HNS_BotanicalExpression")?.Shader;
            if(shader==null||!shader.HasProperty(Shader.PropertyToID("_HnsExpression")))return null;
            mat=new Material(shader){name=source.name+"_HNS_Expression_"+plan,mainTexture=source,renderQueue=2900};
            mat.SetTexture("_HnsAnatomy",a.Mask);
            mat.SetTexture("_HnsArchitecture",plan.Architecture>0?a.Atlases[plan.Architecture]:source);
            mat.SetVector("_HnsExpression",new Vector4(plan.Architecture>0?1:0,plan.Tissue,plan.Reproductive,plan.Surface));
            mat.SetVector("_HnsExpressionEnv",SurfaceFinishUtility.Current(Find.CurrentMap));
            mat.SetVector("_HnsPreview",Vector4.zero);
            mat.SetVector("_HnsFunctional",new Vector4((int)function.Cues,function.Blocked?1:0,function.Regrown?1:0,0));
            WindManager.Notify_PlantMaterialCreated(mat);materials.Add((source,plan,function),mat);return mat;
        }
        internal static bool TryMaterial(Plant p,Material source,bool flipped,Color32[] colors,out Material mat,out bool expanded)
        {
            mat=null;expanded=false;var comp=p.TryGetComp<CompPlantVariety>();
            if(!CanAttempt(comp)||source==null||source.shader!=ShaderDatabase.CutoutPlant||!source.HasProperty("_FallBehaviorEnabled")||
                source.GetFloat("_FallBehaviorEnabled")!=0||source.mainTextureScale!=Vector2.one||source.mainTextureOffset!=Vector2.zero||
                !sources.TryGetValue(source.mainTexture,out var a))return false;
            var d=E.Resolve(Promoted(comp),E.FamilyOf(p.def.defName),a.Anatomy.Organs);
            var function=AdaptivePhenotypeMath.Resolve(FunctionalCues(comp),a.Anatomy.Leaves,a.Anatomy.Organs,comp.NeedsResource,comp.HarvestCycle,d.Plan.Architecture>0);
            if(function.Has(AdaptivePhenotypeMath.Cue.Yield)&&!a.Atlases.ContainsKey(AdaptivePhenotypeMath.FullerArchitecture))function=function with{Cues=function.Cues&~AdaptivePhenotypeMath.Cue.Yield};
            if(d.Unknown||!d.Plan.Valid&&!function.Valid)return false;
            var renderPlan=d.Plan;
            if(function.Has(AdaptivePhenotypeMath.Cue.Yield))renderPlan=renderPlan with{Architecture=AdaptivePhenotypeMath.FullerArchitecture};
            mat=GetMaterial(source.mainTexture,a,renderPlan,function);if(mat==null)return false;
            int band=E.Band(p.Growth),seed=E.Seed(p.thingIDNumber),code=comp.VisualChronicle;
            float bias=d.Plan.Context==1?E.HistoryBias(code):d.Plan.Context==2?NeighborhoodExperiment.Current(p).Difference:0f;
            float localBias=flipped?-bias:bias;
            PlantUtility.SetWindExposureColors(colors,p);
            for(int i=0;i<colors.Length;i++){var c=colors[i];c.r=(byte)Mathf.RoundToInt(band*255f/8);c.g=(byte)Mathf.RoundToInt(seed*255f/7);c.b=(byte)Mathf.RoundToInt((localBias+1)*127.5f);colors[i]=c;}
            expanded=renderPlan.Architecture>0;
            if(d.Plan.Valid)Observe(p,d,band,bias);
            ObserveFunction(p,function,band);
#if HNS_NATIVE_FIXTURE
            Prints[p.thingIDNumber]=new(mat,d,band,seed,bias,code,colors[0],flipped,function);
#endif
            return true;
        }
        private static void ObserveFunction(Plant p,AdaptivePhenotypeMath.State f,int band)
        {
            var o=PhenotypeObservation.For(p);if(o==null||!f.Valid)return;
            o.Functional=f;
            if(f.Has(AdaptivePhenotypeMath.Cue.Yield)&&band>=5)o.Add(V.F(V.Region.Reproductive,"functional-yield","fuller reproductive outlines",25));
            if(f.Has(AdaptivePhenotypeMath.Cue.Cold))o.Add(V.F(V.Region.Surface,"functional-cold","cool silvery margins on green tissue",25));
            if(f.Has(AdaptivePhenotypeMath.Cue.Heat))o.Add(V.F(V.Region.Surface,"functional-heat","warm wax-like leaf highlights",25));
            if(f.Has(AdaptivePhenotypeMath.Cue.Resource)&&f.Blocked)o.Add(V.F(V.Region.Foliage,"functional-resource","muted ochre leaf margins",25));
            if(f.Has(AdaptivePhenotypeMath.Cue.Perennial)&&f.Regrown)o.Add(V.F(V.Region.Foliage,"functional-regrowth","bronzed lower tissue and fresh green upper growth",25));
        }
        private static void Observe(Plant p,E.Decision d,int band,float bias)
        {
            var o=PhenotypeObservation.For(p);if(o==null)return;
            foreach(var id in d.Accepted)
            {
                var t=DefDatabase<VarietyTraitDef>.GetNamedSilentFail("HNS_"+id);if(t==null)continue;
                if(t.botanicalDetail!=BotanicalDetailKind.None)
                {
                    var fact=V.Detail(t.botanicalDetail);
                    // Distinct channel slots: composed architecture and veins
                    // must not suppress one another in the description.
                    o.Add(fact with{Slot=E.Library.First(x=>x.Id==id).Channel.ToString()});
                }
                else if(t.responsiveHabit==ResponsiveHabitKind.RipeningMosaic)
                    o.Add(V.F(V.Region.Reproductive,"reproductive","mixed ripening tones across reproductive organs"));
                else if(t.responsiveHabit==ResponsiveHabitKind.SectorSport)
                    o.Add(V.F(V.Region.Reproductive,"reproductive","localized rose-colored reproductive sectors"));
                else if(t.surfaceFinish!=SurfaceFinishKind.None)
                    o.Add(V.F(V.Region.Surface,"surface",id=="RainBeads"?"a moisture-responsive cuticle":id=="SatinCuticle"?"a satin cuticle":id=="SilverBloom"?"a silvery surface bloom":"a pearlescent cuticle"));
            }
            if(Math.Abs(bias)>.08f)o.Add(V.F(V.Region.Habit,"context",d.Plan.Context==1?"a crown biased by recorded earlier crowding":"a crown inclined toward lateral space"));
            o.Add(V.F(V.Region.Foliage,"individual","individualized pigment territories"));
        }
        internal static void Refresh(Map map)
        {
            if(!Enabled||map==null||map!=Find.CurrentMap)return;
            var e=SurfaceFinishUtility.Current(map);if(e==last)return;last=e;
            foreach(var m in materials.Values)m.SetVector("_HnsExpressionEnv",e);
        }
        internal static void GrowthChanged(Plant p,float before)
        {
            if(!Enabled||p?.Spawned!=true)return;
            var c=p.TryGetComp<CompPlantVariety>();if(!CanAttempt(c))return;
            int old=c.VisualChronicle;
            if(c.ActiveTraits.Any(t=>E.Id(t.defName)=="CrookedChronicle"))
                c.VisualChronicle=ChronicleMath.Record(old,before,p.Growth,NeighborhoodExperiment.Current(p));
            if(E.Band(before)!=E.Band(p.Growth)||old!=c.VisualChronicle)
            {
                p.DirtyMapMesh(p.Map);
#if HNS_NATIVE_FIXTURE
                Invalidations.Add((p.thingIDNumber,E.Band(p.Growth)));
#endif
            }
        }
        internal static void NeighborChanged(Map map,IntVec3 cell)
        {
            if(!Enabled||map==null)return;
            foreach(int dx in new[]{-1,1})
            {
                var at=cell+new IntVec3(dx,0,0);if(!at.InBounds(map))continue;
                var p=at.GetPlant(map);var c=p?.TryGetComp<CompPlantVariety>();
                if(p?.Spawned==true&&CanAttempt(c)&&c.ActiveTraits.Any(t=>E.Id(t.defName)=="GapSeeker"))p.DirtyMapMesh(map);
            }
        }
        internal static bool Preview(ThingDef def,string[] ids,int band,int seed,float bias,Vector4 env,out Texture2D image,out E.Decision decision)
        {
            image=null;decision=null;Prepare(def,ids);
            for(int v=0;v<PlantMaskUtility.VariationCount(def);v++)
            {
                if(PlantMaskUtility.TextureForVariation(def,v) is not Texture2D source||!sources.TryGetValue(source,out var a))continue;
                decision=E.Resolve(ids,E.FamilyOf(def.defName),a.Anatomy.Organs);
                if(decision.Unknown||!decision.Plan.Valid)continue;
                var m=GetMaterial(source,a,decision.Plan);if(m==null)continue;
                image=Readback(m,band,seed,bias,env);return true;
            }
            return false;
        }
        internal static Texture2D Readback(Material m,int band,int seed,float bias,Vector4 env)
        {
            int w=m.mainTexture.width,h=m.mainTexture.height;
            if(m.GetVector("_HnsExpression").x>.5f){w=SculptureMath.Side(w);h=SculptureMath.Side(h);}
            var copy=new Material(m);copy.SetFloat("_SwayHead",0);copy.SetVector("_HnsPreview",new Vector4(1,band/8f,seed/7f,(bias+1)*.5f));copy.SetVector("_HnsExpressionEnv",env);
            var old=RenderTexture.active;var rt=RenderTexture.GetTemporary(w,h,0,RenderTextureFormat.ARGB32);
            try
            {
                Graphics.Blit(Texture2D.blackTexture,rt);Graphics.Blit(m.mainTexture,rt,copy,0);RenderTexture.active=rt;
                var tex=new Texture2D(w,h,TextureFormat.RGBA32,false){filterMode=FilterMode.Point};tex.ReadPixels(new Rect(0,0,w,h),0,0,false);tex.Apply();return tex;
            }
            finally{RenderTexture.active=old;RenderTexture.ReleaseTemporary(rt);UnityEngine.Object.Destroy(copy);}
        }
    }
}
