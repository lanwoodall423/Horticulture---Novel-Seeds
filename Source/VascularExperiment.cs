using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace HorticultureNovelSeeds
{
    // One read-only anatomy texture per source art; bounded shared material
    // per source and allele. No changes to alpha, mesh count or mechanics.
    internal static class VascularExperiment
    {
        internal static bool Enabled {get;set;}
        private sealed record Prepared(Texture2D Texture,VascularMath.Anatomy Data);
        private static readonly Dictionary<Texture,Prepared> masks=new();
        private static readonly Dictionary<(Material,BotanicalDetailKind),Material> materials=new();
        internal static int MaskCount=>masks.Count;
        internal static int MaterialCount=>materials.Count;
        private static Vector2 last=new(-1,-1);
#if HNS_NATIVE_FIXTURE

        internal sealed record Print(Material Material,VascularMath.Anatomy Anatomy,BotanicalDetailKind Kind,Texture Source);
        internal static readonly Dictionary<int,Print> Printed=new();
        internal static readonly HashSet<int> Fallback=new();
        internal static readonly List<string> Preparation=new();
#endif
        internal static void Prepare(ThingDef def,int variation)
        {
            if(!Enabled||PlantMaskUtility.TextureForVariation(def,variation) is not Texture2D source||masks.ContainsKey(source)||masks.Count>=48)return;
            var label=PlantMaskUtility.VariationLabel(def,variation)??"";
            if(label.Contains("Immature",StringComparison.OrdinalIgnoreCase)||label.Contains("Leafless",StringComparison.OrdinalIgnoreCase))return;
            // Match the original source dimensions for exact tissue protection.
            int w=source.width,h=source.height;
            if(w<16||h<16||w>512||h>512)return;
            var src=OrganSpriteExperiment.Read(source,w,h).Select(c=>new OrganSpriteMath.Pixel(c.r,c.g,c.b,c.a)).ToArray();
            var data=VascularMath.Build(src,w,h);if(data==null)return;
            for(int i=0;i<src.Length;i++)
                if(data.Mask[i].A!=0&&!VascularMath.Eligible(src[i]))throw new InvalidOperationException("vascular mask touched protected tissue");
            var tex=new Texture2D(w,h,TextureFormat.RGBA32,false,true){name=source.name+"_HNS_Anatomy",filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};
            tex.SetPixels32(data.Mask.Select(p=>new Color32(p.R,p.G,p.B,p.A)).ToArray());
            tex.Apply(false,true);masks.Add(source,new(tex,data));
#if HNS_NATIVE_FIXTURE
            Preparation.Add(string.Join("\t",def.defName,variation,source.name,w,h,data.Territories,data.TissuePixels,data.VeinPixels,data.ThinPixels));
#endif
        }
        private static Vector2 Environment(Map map)=>new(VascularMath.Light(map.skyManager.CurSkyGlow),VascularMath.Wet(map.weatherManager.RainRate));
        internal static bool TryMaterial(Plant p,Material source,BotanicalDetailKind kind,out Material material)
        {
            material=null;
            if(!Enabled||!VascularMath.Valid(kind))return false;
            if(source==null||source.shader!=ShaderDatabase.CutoutPlant||!source.HasProperty("_FallBehaviorEnabled")||source.GetFloat("_FallBehaviorEnabled")!=0f||
                source.mainTextureScale!=Vector2.one||source.mainTextureOffset!=Vector2.zero||!masks.TryGetValue(source.mainTexture,out var anatomy))
            {
#if HNS_NATIVE_FIXTURE
                Fallback.Add(p.thingIDNumber);
#endif
                return false;
            }
            var key=(source,kind);
            if(!materials.TryGetValue(key,out material))
            {
                if(materials.Count>=96)return false;
                var shader=DefDatabase<ShaderDef>.GetNamedSilentFail("HNS_VascularTissue")?.Shader;
                if(shader==null||!shader.HasProperty(Shader.PropertyToID("_HnsAnatomy"))||!shader.HasProperty(Shader.PropertyToID("_HnsTissue")))return false;
                material=new Material(shader){name=source.mainTexture.name+"_HNS_Tissue_"+kind,mainTexture=source.mainTexture,renderQueue=source.renderQueue};
                material.SetTexture("_HnsAnatomy",anatomy.Texture);
                material.SetFloat("_SwayHead",source.GetFloat("_SwayHead"));
                var v=Environment(p.Map);
                material.SetVector("_HnsTissue",new Vector4((int)kind-(int)BotanicalDetailKind.IvoryNerves+1,v.x,v.y,0));
                WindManager.Notify_PlantMaterialCreated(material);
                materials.Add(key,material);
            }
#if HNS_NATIVE_FIXTURE
            Printed[p.thingIDNumber]=new(material,anatomy.Data,kind,source.mainTexture);
#endif
            return true;
        }
        internal static void Refresh(Map map)
        {
            if(!Enabled||map==null||map!=Find.CurrentMap||materials.Count==0)return;
            var e=Environment(map);if((e-last).sqrMagnitude<.000004f)return;last=e;
            foreach(var m in materials.Values){var v=m.GetVector("_HnsTissue");v.y=e.x;v.z=e.y;m.SetVector("_HnsTissue",v);}
        }
    }
}
