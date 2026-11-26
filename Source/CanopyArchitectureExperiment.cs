using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace HorticultureNovelSeeds
{
    // Modular source-art assembly: reusable cropped organ textures, independent
    // planes, no sprite generation in Print and no per-plant runtime simulation.
    internal static class CanopyArchitectureExperiment
    {
        internal static bool Enabled {get;set;}
        private const int Limit=64;
        private sealed record Prepared(CanopyArchitectureMath.Parts Parts,int W,int H,Texture2D Leaves,Texture2D Fruit);
        private static readonly Dictionary<(ThingDef,Texture),Prepared> prepared=new();
        private static readonly Dictionary<(Material,Texture2D),Material> materials=new();
        internal static int PreparedCount=>prepared.Count;
        internal static int MaterialCount=>materials.Count;
#if HNS_NATIVE_FIXTURE
        internal static readonly HashSet<int> PrintedPlants=new(),FallbackPlants=new();
        internal static readonly Dictionary<int,int> PlaneCounts=new();
        internal static readonly List<string> Preparation=new();
#endif
        internal static CanopyArchitectureKind Resolve(CompPlantVariety comp)
        {
            if(!Enabled||comp?.ActiveTraits==null)return CanopyArchitectureKind.None;
            return comp.ActiveTraits.Where(t=>t!=null&&!t.produceOnlyVisual&&CanopyArchitectureMath.Valid(t.canopyArchitecture))
                .OrderBy(t=>t.defName,StringComparer.Ordinal).Select(t=>t.canopyArchitecture).FirstOrDefault();
        }
        internal static void Prepare(ThingDef def,int variation)
        {
            if(!Enabled||PlantMaskUtility.TextureForVariation(def,variation) is not Texture2D source)return;
            var key=(def,(Texture)source);
            if(prepared.ContainsKey(key)||prepared.Count>=Limit)return;
            string label=PlantMaskUtility.VariationLabel(def,variation)??"";
            if(label.Contains("Immature",StringComparison.OrdinalIgnoreCase)||label.Contains("Leafless",StringComparison.OrdinalIgnoreCase))return;
            PlantMaskUtility.BakedTextureSize(source.width,source.height,out int w,out int h);
            if(w<16||h<16||w>512||h>512)return;
            var p=OrganSpriteExperiment.Read(source,w,h).Select(c=>new OrganSpriteMath.Pixel(c.r,c.g,c.b,c.a)).ToArray();
            var parts=CanopyArchitectureMath.Extract(p,w,h);
            if(parts?.Leaves==null)return;
            Texture2D Make(CanopyArchitectureMath.Module m,string role)
            {
                if(m==null)return null;
                var t=new Texture2D(m.Box.W,m.Box.H,TextureFormat.RGBA32,true){name=source.name+"_HNS_Module_"+role,filterMode=source.filterMode,wrapMode=TextureWrapMode.Clamp,anisoLevel=source.anisoLevel};
                t.SetPixels32(m.Pixels.Select(c=>new Color32(c.R,c.G,c.B,c.A)).ToArray());t.Apply(true,true);return t;
            }
            prepared.Add(key,new(parts,w,h,Make(parts.Leaves,"Leaves"),Make(parts.Fruit,"Fruit")));
#if HNS_NATIVE_FIXTURE
            Preparation.Add(string.Join("\t",def.defName,variation,source.name,w,h,parts.Leaves.Box.W,parts.Leaves.Box.H,parts.Fruit?.Box.W??0,parts.Fruit?.Box.H??0,parts.Anchors.Length));
#endif
        }
        private static Material ModuleMaterial(Material source,Texture2D tex)
        {
            if(tex==null)return null;
            var key=(source,tex);if(materials.TryGetValue(key,out var mat))return mat;
            if(materials.Count>=Limit*2)return null;
            MaterialRequest req;
            if(!MaterialPool.TryGetRequestForMat(source,out req))req=new MaterialRequest(tex,source.shader,source.color){renderQueue=source.renderQueue};
            else req.mainTex=tex;
            mat=MaterialPool.MatFrom(req);
            if(mat!=null)materials.Add(key,mat);return mat;
        }
        private static Vector2 Rotate(Vector2 p,float rot)
        {
            float a=-rot*Mathf.Deg2Rad,c=Mathf.Cos(a),s=Mathf.Sin(a);
            return new Vector2(p.x*c-p.y*s,p.x*s+p.y*c);
        }
        internal static void Print(Plant plant,SectionLayer layer,Material source,CanopyArchitectureKind kind,Vector3 center,Vector2 size,float rotation,bool flipped,Color32[] colors)
        {
            if(!Enabled||!CanopyArchitectureMath.Valid(kind))return;
            if(source==null||source.shader!=ShaderDatabase.CutoutPlant||!source.HasProperty("_FallBehaviorEnabled")||
                source.GetFloat("_FallBehaviorEnabled")!=0f||source.mainTextureScale!=Vector2.one||source.mainTextureOffset!=Vector2.zero||
                !prepared.TryGetValue((plant.def,source.mainTexture),out var p))
            {
#if HNS_NATIVE_FIXTURE
                FallbackPlants.Add(plant.thingIDNumber);
#endif
                return;
            }
            var recipe=CanopyArchitectureMath.Recipe(kind,p.Parts);
            int printed=0;
            var b=p.Parts.Body;
            foreach(var a in recipe)
            {
                var m=a.Fruit?p.Parts.Fruit:p.Parts.Leaves;
                var mat=ModuleMaterial(source,a.Fruit?p.Fruit:p.Leaves);
                if(m==null||mat==null)continue;
                float u=(b.X+b.W*a.X)/p.W-.5f,v=(b.Y+b.H*a.Y)/p.H-.5f;
                var offset=Rotate(new Vector2((flipped?-u:u)*size.x,v*size.y),rotation);
                var point=center+new Vector3(offset.x,(a.Front?.10f:-.05f)*Altitudes.AltInc,offset.y);
                var moduleSize=new Vector2(size.x*m.Box.W/p.W*a.Scale,size.y*m.Box.H/p.H*a.Scale);
                float angle=rotation+(flipped?-a.Angle:a.Angle);
                // Exposure at the point of attachment, not an independent
                // rooted plant. Exact phase alignment needs separate testing.
                var subColors=new Color32[4];
                var low=Color32.Lerp(colors[0],colors[1],Mathf.Clamp01(v+.5f-.12f));
                var high=Color32.Lerp(colors[0],colors[1],Mathf.Clamp01(v+.5f+.12f));
                subColors[0]=subColors[3]=low;subColors[1]=subColors[2]=high;
                Printer_Plane.PrintPlane(layer,point,moduleSize,mat,angle,flipped,null,subColors,Plant.TopVerticesAltitudeBias,plant.HashOffset()%1024);
                printed++;
            }
#if HNS_NATIVE_FIXTURE
            if(printed>0){PrintedPlants.Add(plant.thingIDNumber);PlaneCounts[plant.thingIDNumber]=printed;}
            else FallbackPlants.Add(plant.thingIDNumber);
#endif
        }
    }
}
