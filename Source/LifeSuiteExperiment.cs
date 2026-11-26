using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace HorticultureNovelSeeds
{
    internal static class LifeSuiteExperiment
    {
        internal static bool Enabled {get;set;}
        internal const string ShaderName="HNS_LifeSuite",OverlayShader="HNS_LifeSuiteGlow";
        private sealed record Prepared(Texture2D Mask,LifeSuiteMath.Anatomy Anatomy,Dictionary<int,Texture2D> Atlases);
        private static readonly Dictionary<Texture,Prepared> prepared=new();
        private static readonly Dictionary<(Material,int,int),Material> materials=new();
        private static Vector4 last=new(-1,-1,-1,-1);
        internal static int MapCount=>prepared.Count;
        internal static int MaterialCount=>materials.Count;
#if HNS_NATIVE_FIXTURE
        internal static float? TemperatureOverride,SeasonOverride;
        internal sealed record Print(Material Base,Material Extra,int Mode,int Organs,int Territories,bool Atlas,int Planes,float Growth,float Stress,int Lateral);
        internal static readonly Dictionary<int,Print> Prints=new();
        internal static readonly HashSet<int> Fallback=new();
        internal static readonly List<string> Preparation=new();
#endif
        internal static void Prepare(ThingDef def,int variation)
        {
            if(!Enabled||PlantMaskUtility.TextureForVariation(def,variation) is not Texture2D source||prepared.ContainsKey(source)||prepared.Count>=48)return;
            string label=PlantMaskUtility.VariationLabel(def,variation)??"";
            if(label.Contains("Leafless",StringComparison.OrdinalIgnoreCase))return;
            int w=source.width,h=source.height;
            if(w<16||h<16||w>256||h>256)return;
            var pixels=OrganSpriteExperiment.Read(source,w,h).Select(c=>new OrganSpriteMath.Pixel(c.r,c.g,c.b,c.a)).ToArray();
            var a=LifeSuiteMath.Analyze(pixels,w,h);if(a==null)return;
            var mask=new Texture2D(w,h,TextureFormat.RGBA32,false,true){name=source.name+"_HNS_LifeAnatomy",filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};
            mask.SetPixels32(a.Mask.Select(c=>new Color32(c.R,c.G,c.B,c.A)).ToArray());mask.Apply(false,true);
            var atlases=new Dictionary<int,Texture2D>();
            foreach(int mode in new[]{2,11,19})
            {
                var cells=new Color32[9*w*h];int changed=0,unique=0;var previous=new List<OrganSpriteMath.Pixel[]>();
                for(int f=0;f<9;f++)
                {
                    var frame=LifeSuiteMath.Frame(a,pixels,mode,f);
                    if(!previous.Any(x=>x.SequenceEqual(frame))){unique++;previous.Add(frame);}
                    for(int y=0;y<h;y++)for(int x=0;x<w;x++)
                    {
                        int i=y*w+x;var c=frame[i];
                        if(c.A<pixels[i].A)throw new InvalidOperationException("Life suite atlas lost native alpha");
                        if(c!=pixels[i])changed++;
                        cells[(y+f/3*h)*3*w+x+f%3*w]=new(c.R,c.G,c.B,c.A);
                    }
                }
                if(changed<12||unique<2)continue;
                var atlas=new Texture2D(w*3,h*3,TextureFormat.RGBA32,false){name=source.name+"_HNS_LifeAtlas_"+mode,filterMode=source.filterMode,wrapMode=TextureWrapMode.Clamp};
                atlas.SetPixels32(cells);atlas.Apply(false,true);atlases.Add(mode,atlas);
#if HNS_NATIVE_FIXTURE
                Preparation.Add(string.Join("\t",def.defName,variation,source.name,mode,a.Leaves,a.Organs,a.OrganPixels,unique,changed));
#endif
            }
            prepared.Add(source,new(mask,a,atlases));
#if HNS_NATIVE_FIXTURE
            Preparation.Add(string.Join("\t",def.defName,variation,source.name,0,a.Leaves,a.Organs,a.OrganPixels,0,0));
#endif
        }
        internal static Vector4 Environment(Map map)
        {
            float season=GenLocalDate.Season(map).ToString() switch{"Fall"=>.82f,"Winter"=>1f,"PermanentWinter"=>1f,_=>0f};
#if HNS_NATIVE_FIXTURE
            if(SeasonOverride.HasValue)season=SeasonOverride.Value;
#endif
            return new(VascularMath.Light(map.skyManager.CurSkyGlow),VascularMath.Wet(map.weatherManager.RainRate),GenLocalDate.DayPercent(map),season);
        }
        private static (float Growth,float Stress,int Lateral) State(Plant p,int mode)
        {
            float t=GenTemperature.TryGetTemperatureForCell(p.Position,p.Map,out float temp)?temp:20f;
#if HNS_NATIVE_FIXTURE
            if(TemperatureOverride.HasValue)t=TemperatureOverride.Value;
#endif
            float stress=LifeSuiteMath.Stress(mode,t,p.HitPoints/(float)Math.Max(1,p.MaxHitPoints));
            int bits=0;
            if(mode==15)
            {
                var n=NeighborhoodExperiment.Current(p);bits=LifeSuiteMath.Lateral(n.Left,n.Right);
            }
            if(mode==18)
                for(int dx=-1;dx<=1;dx+=2)
                {
                    var pos=p.Position+new IntVec3(dx,0,0);
                    if(!pos.InBounds(p.Map))continue;
                    var other=pos.GetPlant(p.Map);
                    if(other?.Spawned==true&&other.Growth>=.45f&&other.TryGetComp<CompPlantVariety>()?.ActiveTraits.Any(t=>t.botanicalDetail==BotanicalDetailKind.MycelialRelay)==true)bits|=dx<0?1:2;
                }
            return(p.Growth,stress,bits);
        }
        private static Material MaterialFor(Material source,Prepared a,int mode,int role,Map map)
        {
            var key=(source,mode,role);
            if(materials.TryGetValue(key,out var mat))return mat;
            if(materials.Count>=192)return null;
            bool glow=mode==10&&role==1;
            var shader=DefDatabase<ShaderDef>.GetNamedSilentFail(glow?OverlayShader:ShaderName)?.Shader;
            if(shader==null||!shader.HasProperty(Shader.PropertyToID("_HnsAnatomy"))||!shader.HasProperty(Shader.PropertyToID("_HnsLife")))return null;
            mat=new Material(shader){name=source.mainTexture.name+"_HNS_Life_"+mode+"_"+role,mainTexture=source.mainTexture,renderQueue=glow?3151:source.renderQueue};
            if(role==1&&(mode==8||mode==18))mat.renderQueue=2900;
            mat.SetTexture("_HnsAnatomy",a.Mask);
            bool atlas=role==0&&a.Atlases.TryGetValue(mode,out _);
            mat.SetTexture("_HnsLifeAtlas",atlas?a.Atlases[mode]:source.mainTexture);
            mat.SetVector("_HnsLife",new Vector4(mode,role,atlas?1:0,0));
            mat.SetVector("_HnsLifeEnv",Environment(map));
            mat.SetFloat("_SwayHead",source.GetFloat("_SwayHead"));
            WindManager.Notify_PlantMaterialCreated(mat);materials.Add(key,mat);
            return mat;
        }
        internal static bool TryMaterial(Plant p,Material source,BotanicalDetailKind kind,bool flipped,Color32[] colors,out Material mat)
        {
            mat=null;if(!Enabled||!LifeSuiteMath.Valid(kind))return false;
            int mode=LifeSuiteMath.Mode(kind);
            if(source==null||source.shader!=ShaderDatabase.CutoutPlant||!source.HasProperty("_FallBehaviorEnabled")||source.GetFloat("_FallBehaviorEnabled")!=0f||
                source.mainTextureScale!=Vector2.one||source.mainTextureOffset!=Vector2.zero||!prepared.TryGetValue(source.mainTexture,out var a)||
                (LifeSuiteMath.NeedsOrgan(mode)&&a.Anatomy.Organs==0)||((mode==11||mode==19)&&!a.Atlases.ContainsKey(mode)))
            {
#if HNS_NATIVE_FIXTURE
                Prints.Remove(p.thingIDNumber);Fallback.Add(p.thingIDNumber);
#endif
                return false;
            }
            mat=MaterialFor(source,a,mode,0,p.Map);if(mat==null)return false;
            var s=State(p,mode);
            PlantUtility.SetWindExposureColors(colors,p);
            for(int i=0;i<colors.Length;i++)
            {
                var c=colors[i];c.r=(byte)Mathf.RoundToInt(Mathf.Clamp01(s.Growth)*255);c.g=(byte)Mathf.RoundToInt(s.Stress*255);c.b=(byte)((mode==15&&flipped?((s.Lateral&1)<<1)|((s.Lateral&2)>>1):s.Lateral)*85);colors[i]=c;
            }
#if HNS_NATIVE_FIXTURE
            Prints[p.thingIDNumber]=new(mat,null,mode,a.Anatomy.Organs,a.Anatomy.Leaves,a.Atlases.ContainsKey(mode),0,s.Growth,s.Stress,s.Lateral);
#endif
            if(!LifeSuiteMath.Overlay(mode))PhenotypeObservation.For(p)?.AddLife(kind,mat,s.Growth,s.Stress,s.Lateral);
            return true;
        }
        internal static void PrintExtra(Plant p,SectionLayer layer,Material original,BotanicalDetailKind kind,Vector3 center,Vector2 size,float rotation,bool flipped,Color32[] colors)
        {
            if(!Enabled||!LifeSuiteMath.Valid(kind)||original==null||!prepared.TryGetValue(original.mainTexture,out var a))return;
            int mode=LifeSuiteMath.Mode(kind);if(!LifeSuiteMath.Overlay(mode))return;
            int role=mode==14?2:1;
            var mat=MaterialFor(original,a,mode,role,p.Map);if(mat==null)return;
            var s=State(p,mode);
            if(mode==8||mode==18)
            {
                if(p.Position.GetSnowDepth(p.Map)>.3f||p.Map.terrainGrid.TerrainAt(p.Position).fertility<=0f)return;
                center=p.Position.ToVector3Shifted();center.y=AltitudeLayer.Filth.AltitudeFor();
                float radius=mode==18?2.24f:Mathf.Clamp(Mathf.Max(size.x,size.y)*1.48f,1.1f,1.65f);
                size=new(radius,radius);rotation=0;flipped=false;
            }
            else if(mode==5)
            {
                center.z+=size.y*.24f;center.y+=Altitudes.AltInc/100f;size*=1.46f;rotation=0;flipped=false;
            }
            else if(mode==14)
            {
                center.y-=Altitudes.AltInc/100f;center.x+=size.x*.045f;center.z+=size.y*.035f;size*=1.06f;
            }
            else center.y+=Altitudes.AltInc/100f;
            Printer_Plane.PrintPlane(layer,center,size,mat,rotation,flipped,null,colors,mode==8||mode==18?0f:Plant.TopVerticesAltitudeBias,p.HashOffset()%1024);
            PhenotypeObservation.For(p)?.AddLife(kind,mat,s.Growth,s.Stress,s.Lateral);
#if HNS_NATIVE_FIXTURE
            if(Prints.TryGetValue(p.thingIDNumber,out var print))Prints[p.thingIDNumber]=print with {Extra=mat,Planes=1};
#endif
        }
        internal static void Refresh(Map map)
        {
            if(!Enabled||map==null||map!=Find.CurrentMap||materials.Count==0)return;
            var env=Environment(map);
            if((last-env).sqrMagnitude<.00000001f)return;last=env;
            foreach(var mat in materials.Values)mat.SetVector("_HnsLifeEnv",env);
        }
    }
}
