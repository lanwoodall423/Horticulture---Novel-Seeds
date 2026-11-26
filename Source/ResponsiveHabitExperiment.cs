using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace HorticultureNovelSeeds
{
    // Shared map-driven texture poses; no per-plant tick or remesh.
    internal static class ResponsiveHabitExperiment
    {
        internal const string DefName="HNS_ResponsiveHabit";
        internal static bool Enabled {get;set;}
        internal static bool ArticulationEnabled {get;set;}
        internal static string ShaderFor(ResponsiveHabitKind kind)=>ArticulationMath.Valid(kind)?ArticulationAtlas.ShaderName:DefName;
        private const int Limit=96;
        private readonly record struct Key(Material Source,ResponsiveHabitKind Kind,bool Sheltered,bool Flipped);
        private static readonly Dictionary<Key,Material> materials=new();
        private static readonly Dictionary<Material,Vector4> applied=new();
        internal static int MaterialCount=>materials.Count;
        internal static int Updates {get;private set;}
#if HNS_NATIVE_FIXTURE
        internal static readonly Dictionary<int,Material> PrintedPlants=new();
        internal static readonly Dictionary<int,bool> PrintedFlips=new();
#endif
        internal static ResponsiveHabitKind Resolve(CompPlantVariety comp)
        {
            if(!Enabled||comp?.ActiveTraits==null)return ResponsiveHabitKind.None;
            VarietyTraitDef chosen=null;
            foreach(var t in comp.ActiveTraits)
                if(t!=null&&!t.produceOnlyVisual&&ResponsiveHabitMath.Valid(t.responsiveHabit)&&ArticulationMath.Valid(t.responsiveHabit)==ArticulationEnabled&&
                   (chosen==null||string.CompareOrdinal(t.defName,chosen.defName)<0))chosen=t;
            return chosen?.responsiveHabit??ResponsiveHabitKind.None;
        }
        internal static Vector4 Current(Map map,ResponsiveHabitKind kind,bool sheltered,bool flipped)
        {
            if(ArticulationMath.Valid(kind))return new Vector4(ArticulationMath.Phase(kind,GenLocalDate.DayPercent(map),map.skyManager.CurSkyGlow,map.weatherManager.RainRate,sheltered,flipped),kind==ResponsiveHabitKind.SilkFlutter?1f:0f,0f,0f);
            var p=ResponsiveHabitMath.Controls(kind,GenLocalDate.DayPercent(map),map.skyManager.CurSkyGlow,map.weatherManager.RainRate,sheltered,flipped);
            return new Vector4(p.Height,p.Contraction,p.Lean,p.Droop);
        }
        internal static void Refresh(Map map)
        {
            if(!Enabled||map==null||map!=Find.CurrentMap)return;
            foreach(var pair in materials)
            {
                var key=pair.Key;var mat=pair.Value;
                var pose=Current(map,key.Kind,key.Sheltered,key.Flipped);
                if(applied.TryGetValue(mat,out var old)&&old==pose)continue;
                mat.SetVector("_HnsPose",pose);applied[mat]=pose;Updates++;
            }
        }
        internal static bool TryMaterial(Plant plant,Material source,ResponsiveHabitKind kind,bool flipped,out Material material)
        {
            material=null;
            if(!Enabled||!ResponsiveHabitMath.Valid(kind)||source==null||source.shader!=ShaderDatabase.CutoutPlant||
               !source.HasProperty("_FallBehaviorEnabled")||source.GetFloat("_FallBehaviorEnabled")!=0f||
               source.mainTexture is not Texture2D||source.mainTextureScale!=Vector2.one||source.mainTextureOffset!=Vector2.zero)return false;
            bool sheltered=plant.Map.roofGrid.RoofAt(plant.Position)!=null;
            // Only a directional habit needs a second mirrored material.
            bool directionFlip=(kind==ResponsiveHabitKind.Sunseeker||kind==ResponsiveHabitKind.SolarTilt)&&flipped;
            bool articulation=ArticulationMath.Valid(kind);
            Texture2D atlas=null;
            if(articulation&&(!ArticulationEnabled||!ArticulationAtlas.TryGet(plant.def,source.mainTexture,kind,out atlas)))return false;
            var key=new Key(source,kind,sheltered,directionFlip);
            if(!materials.TryGetValue(key,out material))
            {
                if(materials.Count>=Limit)return false;
                var shader=DefDatabase<ShaderDef>.GetNamedSilentFail(ShaderFor(kind))?.Shader;
                if(shader==null||!shader.HasProperty(Shader.PropertyToID("_HnsPose"))||!shader.HasProperty(Shader.PropertyToID("_SwayHead")))return false;
                material=new Material(shader){name="HNS_Pose_"+kind+"_"+source.mainTexture.name,mainTexture=articulation?atlas:source.mainTexture,renderQueue=source.renderQueue};
                material.SetFloat("_SwayHead",source.GetFloat("_SwayHead"));
                if(articulation)material.SetVector("_HnsAtlas",new Vector4(3f/atlas.width,3f/atlas.height,0f,0f));
                var pose=Current(plant.Map,kind,sheltered,directionFlip);
                material.SetVector("_HnsPose",pose);applied.Add(material,pose);
                WindManager.Notify_PlantMaterialCreated(material);materials.Add(key,material);
            }
#if HNS_NATIVE_FIXTURE
            PrintedPlants[plant.thingIDNumber]=material;PrintedFlips[plant.thingIDNumber]=directionFlip;
#endif
            return true;
        }
    }
}
