using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace HorticultureNovelSeeds
{
    internal static class ArticulationAtlas
    {
        internal const string ShaderName="HNS_OrganArticulation";
        private const int Limit=48,MaxSide=192;
        private static readonly Dictionary<(ThingDef,Texture,ResponsiveHabitKind),Texture2D> atlases=new();
        internal static int Count=>atlases.Count;
#if HNS_NATIVE_FIXTURE
        internal static readonly List<string> Preparation=new();
#endif
        internal static bool TryGet(ThingDef crop,Texture source,ResponsiveHabitKind kind,out Texture2D atlas)=>atlases.TryGetValue((crop,source,kind),out atlas);
        internal static void Prepare(ThingDef crop,int variation)
        {
            if(!ResponsiveHabitExperiment.ArticulationEnabled||PlantMaskUtility.TextureForVariation(crop,variation) is not Texture2D source)return;
            string label=PlantMaskUtility.VariationLabel(crop,variation)??"";
            if(label.Contains("Immature",StringComparison.OrdinalIgnoreCase)||label.Contains("Leafless",StringComparison.OrdinalIgnoreCase))return;
            float scale=Math.Min(1f,(float)MaxSide/Math.Max(source.width,source.height));
            int w=(int)MathF.Round(source.width*scale),h=(int)MathF.Round(source.height*scale);
            if(w<16||h<16||atlases.Count>=Limit)return;
            var pixels=OrganSpriteExperiment.Read(source,w,h).Select(c=>new OrganSpriteMath.Pixel(c.r,c.g,c.b,c.a)).ToArray();
            var organs=ArticulationMath.Prepare(pixels,w,h);
            if(organs==null)return;
            const int g=ArticulationMath.Grid;
            foreach(var kind in Enum.GetValues<ResponsiveHabitKind>().Where(ArticulationMath.Valid))
            {
                var key=(crop,(Texture)source,kind);
                if(atlases.ContainsKey(key)||atlases.Count>=Limit)continue;
                var cells=new Color32[g*g*w*h];int minDiff=int.MaxValue,maxDiff=0;bool preserved=true;
                for(int f=0;f<ArticulationMath.Frames;f++)
                {
                    var frame=ArticulationMath.Compose(organs,kind,f);int diff=0;
                    for(int y=0;y<h;y++)for(int x=0;x<w;x++)
                    {
                        int i=y*w+x;var p=frame[i];
                        if(p!=pixels[i])diff++;
                        if(p.A<pixels[i].A)preserved=false;
                        cells[(y+f/g*h)*g*w+x+f%g*w]=new Color32(p.R,p.G,p.B,p.A);
                    }
                    minDiff=Math.Min(minDiff,diff);maxDiff=Math.Max(maxDiff,diff);
                }
                if(!preserved||maxDiff<12||minDiff!=0)continue;
                var atlas=new Texture2D(g*w,g*h,TextureFormat.RGBA32,false)
                {name=source.name+"_HNS_Articulation_"+kind,filterMode=source.filterMode,wrapMode=TextureWrapMode.Clamp};
                atlas.SetPixels32(cells);atlas.Apply(false,true);atlases.Add(key,atlas);
#if HNS_NATIVE_FIXTURE
                Preparation.Add(string.Join("\t",crop.defName,variation,source.name,kind,w,h,organs.Organs.Length,organs.Recovered,minDiff,maxDiff,preserved));
#endif
            }
        }
    }
}
