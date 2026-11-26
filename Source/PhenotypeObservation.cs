using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using V=HorticultureNovelSeeds.PhenotypeVocabulary;

namespace HorticultureNovelSeeds
{
    // One unsaved observation per currently rendered plant. It is rebuilt on
    // Print, never on every game tick. Dynamic facts read the accepted material
    // controls on inspection rather than guessing from genotype or weather.
    internal sealed class PhenotypeObservation
    {
        internal AdaptivePhenotypeMath.State Functional;
        private readonly List<V.Fact> facts=new();
        private readonly List<(ResponsiveHabitKind Kind,Material Material,int Seed)> poses=new();
        private readonly List<(SurfaceFinishKind Kind,Material Material)> surfaces=new();
        private readonly List<Material> glows=new();
        private readonly List<(BotanicalDetailKind Kind,Material Mat,float Growth,float Stress,int Lateral)> life=new();
        internal void AddLife(BotanicalDetailKind k,Material m,float growth,float stress,int lateral){if(m!=null&&!life.Contains((k,m,growth,stress,lateral)))life.Add((k,m,growth,stress,lateral));}
        private readonly List<(BotanicalDetailKind Kind,Material Mat)> tissues=new();
        internal void AddTissue(BotanicalDetailKind k,Material m){if(m!=null&&!tissues.Contains((k,m)))tissues.Add((k,m));}
        internal static PhenotypeObservation For(Plant p)=>p?.TryGetComp<CompPlantVariety>()?.VisualObservation;
        internal void Add(V.Fact f){if(!string.IsNullOrWhiteSpace(f.Phrase)&&!facts.Contains(f))facts.Add(f);}
        internal void AddPose(ResponsiveHabitKind k,Material m,int seed)
        {if(m!=null&&!poses.Contains((k,m,seed)))poses.Add((k,m,seed));}
        internal void AddSurface(SurfaceFinishKind k,Material m)
        {if(m!=null&&!surfaces.Contains((k,m)))surfaces.Add((k,m));}
        internal void AddGlow(Material m){if(m!=null&&!glows.Contains(m))glows.Add(m);}
        internal V.Fact[] Facts()
        {
            var result=new List<V.Fact>(facts);
            foreach(var p in poses)
            {
                if(p.Material==null)continue;
                var c=p.Material.GetVector("_HnsPose");
                int frame=MosaicMath.Valid(p.Kind)?MosaicMath.Frame(p.Seed):ArticulationMath.Valid(p.Kind)?
                    (p.Kind==ResponsiveHabitKind.SilkFlutter?ArticulationMath.WindFrame(p.Material.GetFloat("_SwayHead")):ArticulationMath.Frame(c.x)):0;
                if(ResponsiveHabitExperiment.UsesAtlas(p.Kind))
                {
                    if(!ArticulationAtlas.TryFrameFacts(p.Material.mainTexture,frame,out var info)||info.ChangedPixels==0)continue;
                    if(MosaicMath.Valid(p.Kind)){result.Add(V.Mosaic(info.States,info.ChangedOrgans,info.TotalOrgans));continue;}
                }
                result.Add(V.Pose(p.Kind,frame,c.y,c.z,c.w));
            }
            foreach(var s in surfaces)
                if(s.Material!=null)result.Add(V.Surface(s.Kind,s.Material.GetVector("_HnsEnvironment").w>.08f));
            if(glows.Any(m=>m!=null&&m.GetFloat("_HnsDarkness")>.08f))result.Add(V.Detail(BotanicalDetailKind.LanternBuds));
            foreach(var t in tissues)if(t.Mat!=null)
            {
                if(t.Kind==BotanicalDetailKind.LivingGlass&&t.Mat.GetVector("_HnsTissue").y<.08f)continue;
                result.Add(V.Detail(t.Kind));
            }
            foreach(var t in life)if(t.Mat!=null)
            {
                var e=t.Mat.GetVector("_HnsLifeEnv");
                if(LifeSuiteMath.Active(LifeSuiteMath.Mode(t.Kind),e.x,e.y,e.z,e.w,t.Growth,t.Stress,t.Lateral))result.Add(V.Detail(t.Kind));
            }
            return V.Resolve(result);
        }
        internal string Prose()=>V.Prose(Facts());
        internal string Detail()=>V.Describe(Facts());
        internal static string History(CompPlantVariety c)=>c==null?"":V.History(c.VisualChronicle);
        internal static string Notes(CompPlantVariety c)
        {
            string observed=c?.VisualObservation?.Prose();
            string detail=c?.VisualObservation?.Detail();
            string history=History(c);
            var policy=c==null||c.PendingDiscovery?null:HorticulturePresentationPolicy.ForCultivar(c.Variety);
            string potential=policy?.PotentialAppearance;
            var parts=new List<string>{"Individual phenotype\n"+(string.IsNullOrWhiteSpace(observed)?"No distinctive rendered expression is currently recorded.":observed)};
            if(!string.IsNullOrEmpty(detail))parts.Add("Botanical observations\n"+detail);
            if(!string.IsNullOrEmpty(history))parts.Add(history);
            if(!string.IsNullOrEmpty(potential))parts.Add("Cultivar potential (not necessarily expressed)\n"+potential);
            return string.Join("\n\n",parts);
        }
        internal static void Geometry(Plant p,PlantVisualParameters v,int meshCount)
        {
            var o=For(p);if(o==null)return;
            float w=v.width*v.scale,h=v.height*v.scale;
            float size=MathF.Sqrt(Math.Max(0f,w*h));
            if(size>1.09f)o.Add(V.F(V.Region.Habit,"size","enlarged overall growth",15));
            else if(size<.91f)o.Add(V.F(V.Region.Habit,"size","compact overall growth",15));
            if(h>0f&&w/h>1.12f)o.Add(V.F(V.Region.Habit,"shape","a broad low profile",10));
            else if(w>0f&&h/w>1.12f)o.Add(V.F(V.Region.Habit,"shape","a slender upright profile",10));
            int ordinary=Mathf.Max(1,Mathf.CeilToInt(p.Growth*p.def.plant.maxMeshCount));
            if(meshCount>ordinary)o.Add(V.F(V.Region.Habit,"density","more closely grouped growth",15));
            if(meshCount<ordinary)o.Add(V.F(V.Region.Habit,"density","more sparsely grouped growth",15));
            if(Math.Abs(v.rotation)>.08f||v.rotationVariation>4f)o.Add(V.F(V.Region.Habit,"orientation","inclined or irregularly angled growth",10));
            if(v.scaleVariation>.08f)o.Add(V.F(V.Region.Habit,"variation","uneven growth sizes",10));
        }
        private static V.Region MaskRegion(int i)=>i==0?V.Region.Reproductive:i==1?V.Region.Foliage:i==2?V.Region.Stem:V.Region.General;
        private static string ColorName(float r,float g,float b)
        {
            Color.RGBToHSV(new Color(Mathf.Clamp01(r),Mathf.Clamp01(g),Mathf.Clamp01(b)),out float hue,out float sat,out float value);
            if(sat<.13f)return value<.3f?"charcoal":value<.72f?"gray":"pale";
            int k=(int)MathF.Floor(hue*12f+.5f)%12;
            string[] names={"red","orange","gold","yellow-green","green","jade","teal","blue","indigo","violet","magenta","rose"};
            return (value<.5f?"dark ":"")+names[k];
        }
        private static void Palette(PhenotypeObservation o,PlantVisualParameters v,V.Region region)
        {
            if(Math.Max(v.tintRed,Math.Max(v.tintGreen,v.tintBlue))-Math.Min(v.tintRed,Math.Min(v.tintGreen,v.tintBlue))>.07f)
                o.Add(V.F(region,"tint",ColorName(v.tintRed,v.tintGreen,v.tintBlue)+"-tinted tissue",20));
            else if(Math.Abs(v.hueShift)>.035f)o.Add(V.F(region,"tint","shifted tissue coloration",15));
            if(v.saturation<.83f||v.dullness>.12f)o.Add(V.F(region,"saturation","muted coloration",15));
            else if(v.saturation>1.17f)o.Add(V.F(region,"saturation","rich coloration",15));
            if(v.brightness<.85f)o.Add(V.F(region,"brightness","darkened tissue",10));
            else if(v.brightness>1.15f)o.Add(V.F(region,"brightness","brightened tissue",10));
            if(v.opacity<.90f)o.Add(V.F(region,"opacity","partly translucent-looking tissue",15));
        }
        internal static void Styled(Plant plant,Material material,PlantVisualParameters v,int variation,int isolated,bool gpu)
        {
            var o=For(plant);if(o==null||material==null)return;
            var comp=plant.TryGetComp<CompPlantVariety>();
            if(gpu)
            {
                var pattern=material.GetVector("_HnsPattern");
                if(pattern.x>0f&&pattern.z>.01f)o.Add(V.Pigment((int)pattern.x,pattern.w));
                var wind=WindHabitUtility.Resolve(comp);
                if(material.HasProperty("_HnsWind")&&WindHabitMath.Valid(wind)&&Prefs.PlantWindSway)o.Add(V.Wind(wind));
                if(material.HasProperty("_HnsSurface")&&material.GetFloat("_HnsSurface")>0f)o.AddSurface((SurfaceFinishKind)(int)material.GetFloat("_HnsSurface"),material);
            }
            // Only qualified shader or successfully baked texture paths
            // provide detailed color facts. Do not invent a masked effect on
            // a source variation that lacks that mask.
            bool baked=material.mainTexture!=null&&material.mainTexture.name.EndsWith("_HNS_Styled",StringComparison.Ordinal);
            if(!gpu&&!baked)return;
            var masks=PlantMaskUtility.LayersForVariation(plant.def,variation,false);
            if(isolated>=0)
            {
                if(masks!=null&&masks.Count>isolated&&masks[isolated].HasPixels)Palette(o,v,MaskRegion(isolated));
                return;
            }
            if(isolated==-1)return;
            bool masked=gpu?material.GetFloat("_HnsMaskEnabled")>.5f:masks!=null;
            if(masked&&masks!=null)
            {
                for(int i=0;i<3&&i<masks.Count;i++)if(masks[i].HasPixels)
                    Palette(o,NovelSeedUtility.ResolvePlantTextureParameters(comp,i),MaskRegion(i));
            }
            else if(!NovelSeedUtility.HasRegionalPlantColor(comp))Palette(o,v,V.Region.General);
        }
        internal static string Potential(IEnumerable<VarietyTraitDef> traits)
        {
            var obs=new PhenotypeObservation();
            foreach(var t in traits.Where(t=>t!=null&&!t.produceOnlyVisual))
            {
                obs.Add(V.Form(t.plantForm,t.plantFormStrength,default,0,true));
                obs.Add(V.Organ(t.organForm));obs.Add(V.Architecture(t.canopyArchitecture));
                obs.Add(V.Detail(t.botanicalDetail));obs.Add(V.Ground(t.groundHabit));
                obs.Add(V.Wind(t.windHabit));obs.Add(V.Surface(t.surfaceFinish,true,true));
                obs.Add(V.Pose(t.responsiveHabit,4,1f,1f,1f,true));
                if(TraitAppearanceCatalog.TryPattern(t,out int mode,out float amount)&&amount>0)
                    obs.Add(V.Pigment(mode,1f));
                if(t.visualMaskIndex>=0&&Math.Max(t.tintRed,Math.Max(t.tintGreen,t.tintBlue))-Math.Min(t.tintRed,Math.Min(t.tintGreen,t.tintBlue))>.07f)
                    obs.Add(V.F(MaskRegion(t.visualMaskIndex),"tint",ColorName(t.tintRed,t.tintGreen,t.tintBlue)+"-tinted tissue",20));
                // Catalog shape defaults are tendencies, not the individual's
                // resolved aggregate, and can be disabled or overridden.
                if(TraitAppearanceCatalog.TryGet(t,out var profile))
                {
                    float size=profile.Scale*MathF.Sqrt(profile.Width*profile.Height);
                    if(size>1.09f)obs.Add(V.F(V.Region.Habit,"size","enlarged overall growth",15));
                    else if(size<.91f)obs.Add(V.F(V.Region.Habit,"size","compact overall growth",15));
                }
            }
            return obs.Detail();
        }
    }
}
