using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using RimWorld;

namespace HorticultureNovelSeeds
{
    // Experimental, opt-in native morphology path. No extra materials, shaders
    // or textures: bend the actual batched sprite grid at mesh construction.
    internal static class PlantFormMesh
    {
        internal static bool Enabled { get; set; }
        internal readonly record struct Form(PlantFormKind Kind,float Strength,NeighborhoodMath.Pressure Pressure=default,int Chronicle=0)
        {
            internal bool Valid => PlantFormMath.IsValid(Kind,Strength);
        }
#if HNS_NATIVE_FIXTURE
        internal static readonly HashSet<int> PrintedPlants = new();
        internal sealed record NeighborhoodPrint(NeighborhoodMath.Pressure Pressure,int Prints,bool RootFixed,float TipShift,float TipHeight,float Width,bool GeometryChanged);
        internal static readonly Dictionary<int,NeighborhoodPrint> NeighborhoodPrints=new();
        internal sealed record ChroniclePrint(int Code,bool RootFixed,float LowerX,float MiddleX,float UpperX,float TipX,float TipHeight,float LowerWidth,float MiddleWidth,float UpperWidth,float MaxError,int MaterialId,int Vertices);
        internal static readonly Dictionary<int,ChroniclePrint> ChroniclePrints=new();
        internal static int Meshes, Vertices, Triangles;
        internal sealed record MosaicPrint(float Payload,int MaterialId,int Prints,bool Valid);
        internal static readonly Dictionary<int,MosaicPrint> MosaicPrints=new();
#endif
        internal static bool ModeMatches(PlantFormKind kind)=>ChronicleExperiment.Enabled?ChronicleMath.Valid(kind):NeighborhoodExperiment.Enabled?NeighborhoodMath.Valid(kind):!ChronicleMath.Valid(kind)&&!NeighborhoodMath.Valid(kind);
        internal static Form Resolve(CompPlantVariety comp)
        {
            if (!Enabled || comp?.ActiveTraits == null) return default;
            VarietyTraitDef chosen=null;
            foreach(var trait in comp.ActiveTraits)
            {
                if (trait == null || trait.produceOnlyVisual || !PlantFormMath.IsValid(trait.plantForm,trait.plantFormStrength)||!ModeMatches(trait.plantForm)) continue;
                if (chosen==null || trait.plantFormStrength>chosen.plantFormStrength ||
                    trait.plantFormStrength==chosen.plantFormStrength && string.CompareOrdinal(trait.defName,chosen.defName)<0)
                    chosen=trait;
            }
            return chosen==null ? default : new Form(chosen.plantForm,chosen.plantFormStrength,default,comp.VisualChronicle);
        }
        private static Vector3 Bilerp(Vector3 a,Vector3 b,Vector3 c,Vector3 d,float x,float y)
            => (a+(d-a)*x)*(1f-y)+(b+(c-b)*x)*y;

        // Let the game construct the source quad first. Its UVs (including atlas
        // and flip), altitude bias, wind exposure and per-plant UV.z payload are
        // the authoritative inputs. Replace only that just-appended quad.
        internal static void Print(Plant plant,SectionLayer layer,Vector3 center,Vector2 size,Material material,float rotation,
            bool flipped,Vector2[] uvs,Color32[] colors,float bias,float payload,Form form)
        {
            if (!form.Valid || material==null)
            {
#if HNS_NATIVE_FIXTURE
                if(ResponsiveHabitExperiment.MosaicEnabled&&ResponsiveHabitExperiment.PrintedPlants.TryGetValue(plant.thingIDNumber,out var printed)&&ReferenceEquals(printed,material))
                {
                    var mesh=layer.GetSubMesh(material);int first=mesh.uvs.Count;
                    Printer_Plane.PrintPlane(layer,center,size,material,rotation,flipped,uvs,colors,bias,payload);
                    bool valid=mesh.uvs.Count==first+4;
                    for(int i=first;i<mesh.uvs.Count;i++)valid&=mesh.uvs[i].z==payload;
                    int mid=material.GetInstanceID();int id=plant.thingIDNumber;
                    if(MosaicPrints.TryGetValue(id,out var old))MosaicPrints[id]=new(payload,mid,old.Prints+1,valid&&old.Valid&&old.Payload==payload&&old.MaterialId==mid);
                    else MosaicPrints[id]=new(payload,mid,1,valid);
                    return;
                }
#endif
                Printer_Plane.PrintPlane(layer,center,size,material,rotation,flipped,uvs,colors,bias,payload);
                return;
            }
            var sub=layer.GetSubMesh(material);
            int start=sub.verts.Count, triStart=sub.tris.Count;
            int n=ChronicleMath.Valid(form.Kind)?8:PlantFormMath.Subdivisions, stride=n+1, count=stride*stride;
            // Guard nonstandard channels and mesh size. A section can contain
            // other printers; don't disturb their vertex/index correspondence.
            bool eligible=!sub.finalized && start+count<=60000 && sub.uvs.Count==start && sub.colors.Count==start &&
                sub.uvsChannelTwo.Count==0 && sub.normals.Count==0 && sub.pollution.Count==0 &&
                float.IsFinite(size.x) && float.IsFinite(size.y) && size.x>0f && size.y>0f;
            Printer_Plane.PrintPlane(layer,center,size,material,rotation,flipped,uvs,colors,bias,payload);
            if (!eligible || sub.verts.Count!=start+4 || sub.uvs.Count!=start+4 || sub.colors.Count!=start+4 ||
                sub.tris.Count!=triStart+6) return;

            Vector3 a=sub.verts[start],b=sub.verts[start+1],c=sub.verts[start+2],d=sub.verts[start+3];
            Vector3 ua=sub.uvs[start],ub=sub.uvs[start+1],uc=sub.uvs[start+2],ud=sub.uvs[start+3];
            Color32 ca=sub.colors[start],cb=sub.colors[start+1],cc=sub.colors[start+2],cd=sub.colors[start+3];
            // Interpolate altitude with original v, not warped v: preserve
            // source draw ordering and avoid extra altitude extrapolation.
            sub.verts.RemoveRange(start,4);sub.uvs.RemoveRange(start,4);
            sub.colors.RemoveRange(start,4);sub.tris.RemoveRange(triStart,6);
            for(int j=0;j<=n;j++)
                for(int i=0;i<=n;i++)
                {
                    float u=i/(float)n,v=j/(float)n;
                    var warped=PlantFormMath.Warp(form.Kind,form.Strength,u,v,flipped,form.Pressure,form.Chronicle);
                    Vector3 pos=Bilerp(a,b,c,d,warped.X,warped.Y);
                    pos.y=Bilerp(a,b,c,d,u,v).y;
                    sub.verts.Add(pos);
                    Vector3 tex=Bilerp(ua,ub,uc,ud,u,v);
                    tex.z=payload;
                    sub.uvs.Add(tex);
                    sub.colors.Add(Color32.Lerp(Color32.Lerp(ca,cd,u),Color32.Lerp(cb,cc,u),v));
                }
            for(int j=0;j<n;j++)
                for(int i=0;i<n;i++)
                {
                    int k=start+j*stride+i;
                    sub.tris.Add(k);sub.tris.Add(k+stride);sub.tris.Add(k+stride+1);
                    sub.tris.Add(k);sub.tris.Add(k+stride+1);sub.tris.Add(k+1);
                }
            PhenotypeObservation.For(plant)?.Add(PhenotypeVocabulary.Form(form.Kind,form.Strength,form.Pressure,form.Chronicle));
#if HNS_NATIVE_FIXTURE
            PrintedPlants.Add(plant.thingIDNumber); Meshes++; Vertices+=count; Triangles+=2*n*n;
            if(ChronicleMath.Valid(form.Kind))
            {
                bool root=true;float error=0;
                for(int j=0;j<=n;j++)for(int i=0;i<=n;i++)
                {
                    float u=i/(float)n,v=j/(float)n;var q=ChronicleMath.Warp(form.Kind,form.Strength,u,v,form.Chronicle);
                    var expected=Bilerp(a,b,c,d,q.X,q.Y);expected.y=Bilerp(a,b,c,d,u,v).y;
                    error=Math.Max(error,(sub.verts[start+j*stride+i]-expected).magnitude);
                    if(j==0)root&=(sub.verts[start+i]-Bilerp(a,b,c,d,u,0f)).sqrMagnitude<1e-8f;
                }
                float X(int j)=>(sub.verts[start+j*stride+n/2].x-Bilerp(a,b,c,d,.5f,j/(float)n).x)/size.x;
                float W(int j)=>(sub.verts[start+j*stride+n]-sub.verts[start+j*stride]).magnitude/size.x;
                float h=(sub.verts[start+n*stride+n/2].z-Bilerp(a,b,c,d,.5f,0f).z)/size.y;
                ChroniclePrints[plant.thingIDNumber]=new(form.Chronicle,root,X(n/4),X(n/2),X(3*n/4),X(n),h,W(n/4),W(n/2),W(n),error,material.GetInstanceID(),count);
            }
            if(NeighborhoodMath.Valid(form.Kind))
            {
                bool fixedRoot=true,changed=false;
                for(int i=0;i<=n;i++)fixedRoot&=(sub.verts[start+i]-Bilerp(a,b,c,d,i/(float)n,0)).sqrMagnitude<1e-8f;
                for(int j=0;j<=n;j++)for(int i=0;i<=n;i++)
                    changed|=(sub.verts[start+j*stride+i]-Bilerp(a,b,c,d,i/(float)n,j/(float)n)).sqrMagnitude>1e-7f;
                Vector3 tip=sub.verts[start+n*stride+n/2],originalTip=Bilerp(a,b,c,d,.5f,1f);
                float tipShift=(tip.x-originalTip.x)/size.x;
                float tipHeight=(tip.z-Bilerp(a,b,c,d,.5f,0f).z)/size.y;
                float width=(sub.verts[start+n*stride+n]-sub.verts[start+n*stride]).magnitude/size.x;
                int id=plant.thingIDNumber;NeighborhoodPrints.TryGetValue(id,out var old);
                NeighborhoodPrints[id]=new(form.Pressure,(old?.Prints??0)+1,fixedRoot&&(old?.RootFixed??true),tipShift,tipHeight,width,changed);
            }
#endif
        }
    }
}
