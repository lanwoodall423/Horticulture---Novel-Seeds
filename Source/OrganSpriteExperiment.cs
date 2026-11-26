using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace HorticultureNovelSeeds
{
    // Experimental preprocessing. No readback, segmentation or new texture
    // generation in Print. Prepared results are bounded by source texture+allele.
    internal static class OrganSpriteExperiment
    {
        internal static bool Enabled { get; set; }
        private const int TextureLimit=100, MaterialLimit=100;
        private static readonly Dictionary<(ThingDef,Texture,OrganFormKind),Texture2D> textures=new();
        private static readonly Dictionary<(Material,Texture2D),Material> materials=new();
        internal static int PreparedCount=>textures.Count;
#if HNS_NATIVE_FIXTURE
        internal static readonly HashSet<int> PrintedPlants=new(), FallbackPlants=new();
        internal static readonly List<string> Preparation=new();
#endif
        internal static OrganFormKind Resolve(CompPlantVariety comp)
        {
            if(!Enabled||comp?.ActiveTraits==null)return OrganFormKind.None;
            return comp.ActiveTraits.Where(t=>t!=null&&!t.produceOnlyVisual&&OrganSpriteMath.Valid(t.organForm))
                .OrderBy(t=>t.defName,StringComparer.Ordinal).Select(t=>t.organForm).FirstOrDefault();
        }
        internal static void Prepare(ThingDef crop,int variation)
        {
            if(!Enabled||crop==null||PlantMaskUtility.TextureForVariation(crop,variation) is not Texture2D source)return;
            string label=PlantMaskUtility.VariationLabel(crop,variation)??"";
            if(label.Contains("Immature",StringComparison.OrdinalIgnoreCase)||label.Contains("Leafless",StringComparison.OrdinalIgnoreCase))return;
            var masks=PlantMaskUtility.LayersForVariation(crop,variation,false);
            PlantMaskUtility.BakedTextureSize(source.width,source.height,out int w,out int h);
            if(w>512||h>512||w<16||h<16||textures.Count>=TextureLimit)return;
            Color32[] pixels=Read(source,w,h);
            var data=pixels.Select(c=>new OrganSpriteMath.Pixel(c.r,c.g,c.b,c.a)).ToArray();
            int opaque=data.Count(p=>p.A>=80);
            if(opaque==0)return;
            var fruit=new bool[data.Length];var stem=new bool[data.Length];
            for(int i=0;i<data.Length;i++)
            {
                if(data[i].A<80)continue;
                int layer=PlantMaskUtility.LayerAt(masks,i%w,i/w,w,h);
                // Palette candidates also allow flowering tissue (e.g. cotton)
                // which the harvested-product classifier intentionally excludes.
                fruit[i]=OrganSpriteMath.Reproductive(data[i])||layer==0;
                stem[i]=layer==2;
            }
            var fp=OrganSpriteMath.Parts(fruit,w,h,opaque,false);
            var sp=OrganSpriteMath.Parts(stem,w,h,opaque,true);
#if HNS_NATIVE_FIXTURE
            Preparation.Add(string.Join("\t",crop.defName,variation,label,source.name,w,h,fp.Count,sp.Count,fp.Sum(p=>p.Indices.Length),sp.Sum(p=>p.Indices.Length)));
#endif
            foreach(OrganFormKind kind in Enum.GetValues<OrganFormKind>())
            {
                if(!OrganSpriteMath.Valid(kind)||textures.Count>=TextureLimit)continue;
                var key=(crop,(Texture)source,kind);
                if(textures.ContainsKey(key))continue;
                var parts=kind==OrganFormKind.ThickStalk?sp:fp;
                if(parts.Count==0)continue;
                var result=OrganSpriteMath.Compose(data,w,h,parts,kind);
                if(result.SequenceEqual(data))continue;
                var tex=new Texture2D(w,h,TextureFormat.RGBA32,true)
                {
                    name=source.name+"_HNS_Organ_"+kind,filterMode=source.filterMode,wrapMode=TextureWrapMode.Clamp,anisoLevel=source.anisoLevel
                };
                tex.SetPixels32(result.Select(p=>new Color32(p.R,p.G,p.B,p.A)).ToArray());
                tex.Apply(true,true);textures.Add(key,tex);
            }
        }
        internal static Color32[] Read(Texture2D source,int w,int h)
        {
            RenderTexture previous=RenderTexture.active;
            RenderTexture temporary=RenderTexture.GetTemporary(w,h,0,RenderTextureFormat.ARGB32);
            Texture2D readable=null;
            try
            {
                Graphics.Blit(source,temporary);RenderTexture.active=temporary;
                readable=new Texture2D(w,h,TextureFormat.RGBA32,false);
                readable.ReadPixels(new Rect(0,0,w,h),0,0,false);
                readable.Apply(false,false);return readable.GetPixels32();
            }
            finally
            {
                RenderTexture.active=previous;RenderTexture.ReleaseTemporary(temporary);
                if(readable!=null)UnityEngine.Object.Destroy(readable);
            }
        }
        internal static bool TryMaterial(Plant plant,Material source,OrganFormKind kind,out Material material)
        {
            material=null;
            if(!Enabled||!OrganSpriteMath.Valid(kind))return false;
            if(source==null||source.shader!=ShaderDatabase.CutoutPlant||!source.HasProperty("_FallBehaviorEnabled")||
                source.GetFloat("_FallBehaviorEnabled")!=0f||source.mainTextureScale!=Vector2.one||source.mainTextureOffset!=Vector2.zero||
                !textures.TryGetValue((plant.def,source.mainTexture,kind),out var tex))
            {
#if HNS_NATIVE_FIXTURE
                FallbackPlants.Add(plant.thingIDNumber);
#endif
                return false;
            }
            var key=(source,tex);
            if(!materials.TryGetValue(key,out material))
            {
                if(materials.Count>=MaterialLimit)return false;
                MaterialRequest req;
                if(!MaterialPool.TryGetRequestForMat(source,out req))req=new MaterialRequest(tex,source.shader,source.color){renderQueue=source.renderQueue};
                else req.mainTex=tex;
                material=MaterialPool.MatFrom(req);
                if(material==null)return false;
                materials.Add(key,material);
            }
#if HNS_NATIVE_FIXTURE
            PrintedPlants.Add(plant.thingIDNumber);
#endif
            return true;
        }
    }
}
