using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
namespace HorticultureNovelSeeds
{
    // One explicitly prewarmed, bounded ground plane per mature cultivar.
    // These are render-only details, not spawned filth, plants or resources.
    internal static class GroundHabitExperiment
    {
        internal static bool Enabled {get;set;}
        private const int Limit=64;
        private static readonly Dictionary<(ThingDef,Texture,GroundHabitKind),Material> materials=new();
        internal static int MaterialCount=>materials.Count;
#if HNS_NATIVE_FIXTURE
        internal static readonly Dictionary<int,(Material Material,Vector3 Center,float Size)> PrintedPlants=new();
        internal static readonly List<string> Preparation=new();
#endif
        internal static GroundHabitKind Resolve(CompPlantVariety comp)
        {
            if(!Enabled||comp?.ActiveTraits==null)return GroundHabitKind.None;
            return comp.ActiveTraits.Where(t=>t!=null&&!t.produceOnlyVisual&&GroundHabitMath.Valid(t.groundHabit))
                .OrderBy(t=>t.defName,StringComparer.Ordinal).Select(t=>t.groundHabit).FirstOrDefault();
        }
        internal static bool FlowerSpecies(ThingDef d)=>d.defName=="Plant_Rose"||d.defName=="Plant_Daylily";
        internal static void Prepare(ThingDef def,int variation)
        {
            if(!Enabled||PlantMaskUtility.TextureForVariation(def,variation) is not Texture2D source)return;
            string label=PlantMaskUtility.VariationLabel(def,variation)??"";
            if(label.Contains("Immature",StringComparison.OrdinalIgnoreCase)||label.Contains("Leafless",StringComparison.OrdinalIgnoreCase))return;
            PlantMaskUtility.BakedTextureSize(source.width,source.height,out int w,out int h);
            if(w<16||h<16||w>512||h>512)return;
            var p=OrganSpriteExperiment.Read(source,w,h).Select(c=>new OrganSpriteMath.Pixel(c.r,c.g,c.b,c.a)).ToArray();
            var pal=GroundHabitMath.Sample(p);
            foreach(var kind in Enum.GetValues<GroundHabitKind>())
            {
                if(!GroundHabitMath.Valid(kind)||kind==GroundHabitKind.PetalDrift&&!FlowerSpecies(def)||materials.Count>=Limit)continue;
                var key=(def,(Texture)source,kind);if(materials.ContainsKey(key))continue;
                var pixels=GroundHabitMath.Generate(kind,pal,GroundHabitMath.StableSeed(def.defName+source.name+kind));
                int visible=pixels.Count(c=>c.A>=90);if(visible<12)continue;
                var tex=new Texture2D(GroundHabitMath.Size,GroundHabitMath.Size,TextureFormat.RGBA32,true)
                {name="HNS_Ground_"+source.name+"_"+kind,filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp,anisoLevel=1};
                tex.SetPixels32(pixels.Select(c=>new Color32(c.R,c.G,c.B,c.A)).ToArray());tex.Apply(true,true);
                var mat=MaterialPool.MatFrom(tex,ShaderDatabase.Cutout,Color.white);
                if(mat==null){UnityEngine.Object.Destroy(tex);continue;}materials.Add(key,mat);
#if HNS_NATIVE_FIXTURE
                Preparation.Add(string.Join("\t",def.defName,variation,source.name,kind,visible,pal.Leaf.R,pal.Leaf.G,pal.Leaf.B,pal.Petal.R,pal.Petal.G,pal.Petal.B));
#endif
            }
        }
        internal static void Print(Plant plant,SectionLayer layer,Material source,GroundHabitKind kind,Vector2 graphicSize)
        {
            if(!Enabled||!GroundHabitMath.Valid(kind)||source==null||plant.Growth<.65f||
                plant.Map==null||plant.Position.GetSnowDepth(plant.Map)>.3f||plant.Position.GetEdifice(plant.Map)!=null||
                plant.Map.terrainGrid.TerrainAt(plant.Position).fertility<=0f||
                source.shader!=ShaderDatabase.CutoutPlant||
                !materials.TryGetValue((plant.def,source.mainTexture,kind),out var mat))return;
            float size=Mathf.Clamp(Mathf.Max(graphicSize.x,graphicSize.y)*1.46f,.95f,1.55f);
            // Terrain-level altitude, below plant and shadows. Position stays
            // within the normal plant footprint and stable across reloads.
            var center=plant.Position.ToVector3Shifted();
            center.y=AltitudeLayer.Filth.AltitudeFor();
            int hash=plant.Position.GetHashCode()&0x7fffffff;
            float angle=hash%360;
            Printer_Plane.PrintPlane(layer,center,new Vector2(size,size),mat,angle,false,null,null,0f,0f);
            PhenotypeObservation.For(plant)?.Add(PhenotypeVocabulary.Ground(kind));
#if HNS_NATIVE_FIXTURE
            PrintedPlants[plant.thingIDNumber]=(mat,center,size);
#endif
        }
    }
}
