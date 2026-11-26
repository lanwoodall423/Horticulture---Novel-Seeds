using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace HorticultureNovelSeeds
{
    // Explicit prewarm, bounded by source+allele. No generation during mesh printing.
    internal static class BotanicalDetailExperiment
    {
        internal static bool Enabled {get;set;}
        private const int Limit=96;
        private static readonly Dictionary<(ThingDef,Texture,BotanicalDetailKind),Texture2D> textures=new();
        private static readonly Dictionary<(Material,Texture2D),Material> materials=new();
        private static readonly Dictionary<Material,Material> glows=new();
        internal static int PreparedCount=>textures.Count;
        internal static int GlowCount=>glows.Count;
        private static float lastDark=-1f;
#if HNS_NATIVE_FIXTURE
        internal static readonly HashSet<int> PrintedPlants=new(),FallbackPlants=new();
        internal static readonly Dictionary<int,Material> GlowPlants=new();
        internal static readonly List<string> Preparation=new();
#endif
        internal static bool ModeMatches(BotanicalDetailKind k)=>LifeSuiteExperiment.ForgeEnabled?SculptureMath.ForgeValid(k):LifeSuiteExperiment.SculptureEnabled?SculptureMath.Valid(k):LifeSuiteExperiment.Enabled?LifeSuiteMath.Valid(k):VascularExperiment.Enabled?VascularMath.Valid(k):!SculptureMath.ForgeValid(k)&&!SculptureMath.Valid(k)&&!LifeSuiteMath.Valid(k)&&!VascularMath.Valid(k);
        internal static BotanicalDetailKind Resolve(CompPlantVariety comp)
        {
            if(!Enabled||comp?.ActiveTraits==null)return BotanicalDetailKind.None;
            VarietyTraitDef chosen=null;
            foreach(var t in comp.ActiveTraits)
                if(t!=null&&!t.produceOnlyVisual&&BotanicalDetailMath.Valid(t.botanicalDetail)&&ModeMatches(t.botanicalDetail)&&
                    (chosen==null||string.CompareOrdinal(t.defName,chosen.defName)<0))chosen=t;
            return chosen?.botanicalDetail??BotanicalDetailKind.None;
        }
        internal static void Prepare(ThingDef def,int variation)
        {
            if(LifeSuiteExperiment.Enabled){LifeSuiteExperiment.Prepare(def,variation);return;}
            if(VascularExperiment.Enabled){VascularExperiment.Prepare(def,variation);return;}
            if(!Enabled||PlantMaskUtility.TextureForVariation(def,variation) is not Texture2D source)return;
            string label=PlantMaskUtility.VariationLabel(def,variation)??"";
            if(label.Contains("Immature",StringComparison.OrdinalIgnoreCase)||label.Contains("Leafless",StringComparison.OrdinalIgnoreCase))return;
            PlantMaskUtility.BakedTextureSize(source.width,source.height,out int w,out int h);
            if(w<16||h<16||w>512||h>512||textures.Count>=Limit)return;
            var pixels=OrganSpriteExperiment.Read(source,w,h).Select(c=>new OrganSpriteMath.Pixel(c.r,c.g,c.b,c.a)).ToArray();
            foreach(var kind in Enum.GetValues<BotanicalDetailKind>())
            {
                if(!BotanicalDetailMath.Topology(kind)||textures.Count>=Limit)continue;
                var key=(def,(Texture)source,kind);if(textures.ContainsKey(key))continue;
                var result=BotanicalDetailMath.Edit(pixels,w,h,kind);
#if HNS_NATIVE_FIXTURE
                Preparation.Add(string.Join("\t",def.defName,variation,source.name,kind,w,h,result.Removed,result.Added));
#endif
                if(result.Removed+result.Added<3)continue;
                var tex=new Texture2D(w,h,TextureFormat.RGBA32,true){name=source.name+"_HNS_Detail_"+kind,filterMode=source.filterMode,wrapMode=TextureWrapMode.Clamp,anisoLevel=source.anisoLevel};
                tex.SetPixels32(result.Pixels.Select(p=>new Color32(p.R,p.G,p.B,p.A)).ToArray());tex.Apply(true,true);
                textures.Add(key,tex);
            }
        }
        private static bool Eligible(Material source)=>source!=null&&source.shader==ShaderDatabase.CutoutPlant&&
            source.HasProperty("_FallBehaviorEnabled")&&source.GetFloat("_FallBehaviorEnabled")==0f&&source.mainTextureScale==Vector2.one&&source.mainTextureOffset==Vector2.zero;
        internal static bool TryMaterial(Plant plant,Material source,BotanicalDetailKind kind,out Material material)
        {
            material=null;if(!Enabled||!BotanicalDetailMath.Topology(kind))return false;
            if(!Eligible(source)||!textures.TryGetValue((plant.def,source.mainTexture,kind),out var tex))
            {
#if HNS_NATIVE_FIXTURE
                FallbackPlants.Add(plant.thingIDNumber);
#endif
                return false;
            }
            var key=(source,tex);
            if(!materials.TryGetValue(key,out material))
            {
                if(materials.Count>=Limit)return false;
                MaterialRequest req;
                if(!MaterialPool.TryGetRequestForMat(source,out req))req=new MaterialRequest(tex,source.shader,source.color){renderQueue=source.renderQueue};
                else req.mainTex=tex;
                material=MaterialPool.MatFrom(req);if(material==null)return false;
                materials.Add(key,material);
            }
#if HNS_NATIVE_FIXTURE
            PrintedPlants.Add(plant.thingIDNumber);
#endif
            return true;
        }
        internal static void PrintGlow(Plant plant,SectionLayer layer,Material source,BotanicalDetailKind kind,Vector3 center,Vector2 size,float rotation,bool flipped,Color32[] colors)
        {
            if(!Enabled||kind!=BotanicalDetailKind.LanternBuds||!Eligible(source))return;
            if(!glows.TryGetValue(source,out var mat))
            {
                if(glows.Count>=Limit)return;
                var shader=DefDatabase<ShaderDef>.GetNamedSilentFail("HNS_BotanicalNight")?.Shader;
                if(shader==null||!shader.HasProperty(Shader.PropertyToID("_HnsDarkness"))||!shader.HasProperty(Shader.PropertyToID("_SwayHead")))return;
                mat=new Material(shader){name="HNS_Night_"+source.mainTexture.name,mainTexture=source.mainTexture,renderQueue=3151};
                mat.SetFloat("_SwayHead",source.GetFloat("_SwayHead"));
                mat.SetFloat("_HnsDarkness",BotanicalDetailMath.Darkness(plant.Map.skyManager.CurSkyGlow));
                WindManager.Notify_PlantMaterialCreated(mat);glows.Add(source,mat);
            }
            Printer_Plane.PrintPlane(layer,center.WithYOffset(Altitudes.AltInc/100f),size,mat,rotation,flipped,null,colors,Plant.TopVerticesAltitudeBias,plant.HashOffset()%1024);
            PhenotypeObservation.For(plant)?.AddGlow(mat);
#if HNS_NATIVE_FIXTURE
            GlowPlants[plant.thingIDNumber]=mat;
#endif
        }
        internal static void Refresh(Map map)
        {
            if(!Enabled||map==null||map!=Find.CurrentMap||glows.Count==0)return;
            float dark=BotanicalDetailMath.Darkness(map.skyManager.CurSkyGlow);
            if(Math.Abs(lastDark-dark)<.003f)return;
            lastDark=dark;
            foreach(var m in glows.Values)m.SetFloat("_HnsDarkness",dark);
        }
    }
}
