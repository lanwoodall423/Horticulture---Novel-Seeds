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
        internal static readonly List<string> MosaicPreparation=new(){"Species\tVariation\tSource\tTrait\tFrame\tChanged organs\tChanged pixels\tAlpha preserved"};
#endif

#if HNS_NATIVE_FIXTURE
        // Compare a real vanilla source against its exact neutral atlas pose.
        // Frame-ID swatches alone are insufficient: they accepted upside-down
        // artwork because the quadrant matcher deliberately ignored position.
        internal static readonly List<string> OrientationEvidence=new()
        {"Species\tVariation\tSource\tTrait\tNeutral frame\tDirect RGB error\tVertically mirrored RGB error\tHorizontally mirrored RGB error\tBoth mirrored RGB error\tLower opaque source pixels\tUpper opaque source pixels"};
        internal static string DiagnosticRoot;
        private static Color32[] ReadRendered(Texture2D tex,Material mat,int w,int h)
        {
            var previous=RenderTexture.active;
            var rt=RenderTexture.GetTemporary(w,h,0,RenderTextureFormat.ARGB32);
            Texture2D read=null;
            try
            {
                // Known black background for the cutout-discarded pixels.
                Graphics.Blit(Texture2D.blackTexture,rt);
                Graphics.Blit(tex,rt,mat,0);
                RenderTexture.active=rt;
                read=new Texture2D(w,h,TextureFormat.RGBA32,false);
                read.ReadPixels(new Rect(0,0,w,h),0,0,false);read.Apply(false,false);
                return read.GetPixels32();
            }
            finally
            {
                RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);
                if(read!=null)UnityEngine.Object.Destroy(read);
            }
        }
        private static float Error(Color32[] expected,Color32[] observed,int w,int h,bool flipX=false,bool flipY=false)
        {
            long error=0;int count=0;
            for(int y=0;y<h;y++)for(int x=0;x<w;x++)
            {
                var a=expected[(flipY?h-1-y:y)*w+(flipX?w-1-x:x)];
                var b=observed[y*w+x];
                if(a.r+a.g+a.b<=3&&b.r+b.g+b.b<=3)continue;
                count++;
                error+=Math.Max(Math.Abs(a.r-b.r),Math.Max(Math.Abs(a.g-b.g),Math.Abs(a.b-b.b)));
            }
            return count==0?1f:(float)error/(255f*count);
        }
        private static void VerifyOrientation(ThingDef crop,int variation,Texture2D source,Texture2D atlas,
            ResponsiveHabitKind kind,OrganSpriteMath.Pixel[] original,int w,int h)
        {
            int neutral=ArticulationMath.NeutralFrame(kind);
            var vanilla=new Material(ShaderDatabase.CutoutPlant){mainTexture=source,renderQueue=2900};
            var animated=new Material(DefDatabase<ShaderDef>.GetNamed(ShaderName).Shader){mainTexture=atlas,renderQueue=2900};
            vanilla.SetFloat("_FallBehaviorEnabled",0f);
            vanilla.SetFloat("_SwayHead",0f);animated.SetFloat("_SwayHead",0f);
            animated.SetVector("_HnsPose",new Vector4(neutral/(float)(ArticulationMath.Frames-1),0f,0f,0f));
            animated.SetVector("_HnsAtlas",new Vector4(1f/w,1f/h,0f,0f));
            try
            {
                var expected=ReadRendered(source,vanilla,w,h);
                var observed=ReadRendered(atlas,animated,w,h);
                float direct=Error(expected,observed,w,h);
                float vertical=Error(expected,observed,w,h,false,true);
                float horizontal=Error(expected,observed,w,h,true,false);
                float both=Error(expected,observed,w,h,true,true);
                int lower=original.Take((h/2)*w).Count(c=>c.A>=90);
                int upper=original.Skip((h/2)*w).Count(c=>c.A>=90);
                string F(float f)=>f.ToString("R",System.Globalization.CultureInfo.InvariantCulture);
                string row=string.Join("\t",crop.defName,variation,source.name,kind,neutral,F(direct),F(vertical),F(horizontal),F(both),lower,upper);
                OrientationEvidence.Add(row);
                if(!string.IsNullOrEmpty(DiagnosticRoot))
                    System.IO.File.WriteAllLines(System.IO.Path.Combine(DiagnosticRoot,"HNS-garden-articulation-orientation.tsv"),OrientationEvidence);
                Log.Message("[HNS Articulation] source orientation "+row);
                // Require direct positional agreement, not just a best match
                // among arbitrary reflections. Symmetric art may tie.
                if(direct>.065f||direct>Math.Min(vertical,Math.Min(horizontal,both))+.005f)
                    throw new InvalidOperationException("Articulation neutral source sprite orientation mismatch: "+row);
            }
            finally
            {
                UnityEngine.Object.Destroy(vanilla);UnityEngine.Object.Destroy(animated);
            }
        }
#endif
        internal static bool TryGet(ThingDef crop,Texture source,ResponsiveHabitKind kind,out Texture2D atlas)=>atlases.TryGetValue((crop,source,kind),out atlas);
        internal static void Prepare(ThingDef crop,int variation)
        {
            if((!ResponsiveHabitExperiment.ArticulationEnabled&&!ResponsiveHabitExperiment.MosaicEnabled)||PlantMaskUtility.TextureForVariation(crop,variation) is not Texture2D source)return;
            string label=PlantMaskUtility.VariationLabel(crop,variation)??"";
            if(label.Contains("Immature",StringComparison.OrdinalIgnoreCase)||label.Contains("Leafless",StringComparison.OrdinalIgnoreCase))return;
            float scale=Math.Min(1f,(float)MaxSide/Math.Max(source.width,source.height));
            int w=(int)MathF.Round(source.width*scale),h=(int)MathF.Round(source.height*scale);
            if(w<16||h<16||atlases.Count>=Limit)return;
            var pixels=OrganSpriteExperiment.Read(source,w,h).Select(c=>new OrganSpriteMath.Pixel(c.r,c.g,c.b,c.a)).ToArray();
            var organs=ArticulationMath.Prepare(pixels,w,h);
            if(organs==null||ResponsiveHabitExperiment.MosaicEnabled&&organs.Organs.Length<2)return;
            const int g=ArticulationMath.Grid;
            foreach(var kind in Enum.GetValues<ResponsiveHabitKind>().Where(k=>ResponsiveHabitExperiment.ModeMatches(k)&&ResponsiveHabitExperiment.UsesAtlas(k)))
            {
                var key=(crop,(Texture)source,kind);
                if(atlases.ContainsKey(key)||atlases.Count>=Limit)continue;
                var cells=new Color32[g*g*w*h];int minDiff=int.MaxValue,maxDiff=0;bool preserved=true;
                bool mosaic=MosaicMath.Valid(kind);var variants=new List<OrganSpriteMath.Pixel[]>();bool everyVariantChanged=true;
#if HNS_NATIVE_FIXTURE
                var mosaicRows=new List<string>();
#endif
                for(int f=0;f<ArticulationMath.Frames;f++)
                {
                    int changedOrgans=0;
                    var frame=mosaic?MosaicMath.Compose(organs,kind,f,out changedOrgans):ArticulationMath.Compose(organs,kind,f);int diff=0;
                    for(int y=0;y<h;y++)for(int x=0;x<w;x++)
                    {
                        int i=y*w+x;var p=frame[i];
                        if(p!=pixels[i])diff++;
                        if(p.A<pixels[i].A)preserved=false;
                        cells[(y+f/g*h)*g*w+x+f%g*w]=new Color32(p.R,p.G,p.B,p.A);
                    }
                    minDiff=Math.Min(minDiff,diff);maxDiff=Math.Max(maxDiff,diff);
                    if(mosaic&&f>0)
                    {
                        if(diff<6||changedOrgans==0)everyVariantChanged=false;
                        if(!variants.Any(v=>v.SequenceEqual(frame)))variants.Add(frame);
#if HNS_NATIVE_FIXTURE
                        mosaicRows.Add(string.Join("\t",crop.defName,variation,source.name,kind,f,changedOrgans,diff,preserved));
#endif
                    }
                }
                if(!preserved||maxDiff<12||minDiff!=0||mosaic&&(!everyVariantChanged||variants.Count<2))continue;
                var atlas=new Texture2D(g*w,g*h,TextureFormat.RGBA32,false)
                {name=source.name+"_HNS_Articulation_"+kind,filterMode=source.filterMode,wrapMode=TextureWrapMode.Clamp};
                atlas.SetPixels32(cells);atlas.Apply(false,true);atlases.Add(key,atlas);
#if HNS_NATIVE_FIXTURE
                VerifyOrientation(crop,variation,source,atlas,kind,pixels,w,h);
                if(mosaic)MosaicPreparation.AddRange(mosaicRows);
                Preparation.Add(string.Join("\t",crop.defName,variation,source.name,kind,w,h,organs.Organs.Length,organs.Recovered,minDiff,maxDiff,preserved));
#endif
            }
        }
    }
}
