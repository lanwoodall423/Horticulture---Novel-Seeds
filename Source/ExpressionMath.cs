using System;
using System.Collections.Generic;
using System.Linq;

namespace HorticultureNovelSeeds
{
    // Four composable visual channels plus an optional contextual geometry
    // channel. This is an explicit promotion list, not a blanket activation
    // of the experimental catalog. Unknown visual traits retain old paths.
    internal static class ExpressionMath
    {
        internal enum Channel { Architecture,Tissue,Reproductive,Surface,Context }
        internal enum Tier { Natural,Living,Exceptional,Exotic }
        internal enum Family { Unknown,Cereal,Broadleaf,Flower }
        internal readonly record struct Allele(string Id,Channel Channel,int Code,Tier Tier,Family[] Families,bool Organs);
        private static readonly Family[] all={Family.Unknown,Family.Cereal,Family.Broadleaf,Family.Flower};
        private static readonly Family[] flower={Family.Broadleaf,Family.Flower};
        internal static readonly Allele[] Library={
            new("WingedPetioles",Channel.Architecture,30,Tier.Natural,all,false),
            new("CandelabraTips",Channel.Architecture,27,Tier.Exceptional,new[]{Family.Cereal,Family.Broadleaf},true),
            new("PendantRaceme",Channel.Architecture,24,Tier.Exceptional,new[]{Family.Cereal,Family.Broadleaf,Family.Flower},true),
            new("UmbelCrown",Channel.Architecture,23,Tier.Exceptional,flower,true),
            new("HoodedSpathes",Channel.Architecture,32,Tier.Exceptional,flower,true),
            new("PetaloidDoubling",Channel.Architecture,39,Tier.Exceptional,flower,true),
            new("IvoryNerves",Channel.Tissue,1,Tier.Natural,all,false),
            new("CrimsonNetwork",Channel.Tissue,2,Tier.Exceptional,all,false),
            new("BronzeReticulum",Channel.Tissue,3,Tier.Natural,all,false),
            new("VascularChimera",Channel.Tissue,4,Tier.Exceptional,all,false),
            new("RipeningMosaic",Channel.Reproductive,1,Tier.Living,all,true),
            new("SectorSport",Channel.Reproductive,2,Tier.Exceptional,all,true),
            new("SatinCuticle",Channel.Surface,1,Tier.Natural,all,false),
            new("SilverBloom",Channel.Surface,2,Tier.Natural,all,false),
            new("PearlCuticle",Channel.Surface,3,Tier.Exotic,all,false),
            new("RainBeads",Channel.Surface,4,Tier.Living,all,false),
            new("CrookedChronicle",Channel.Context,1,Tier.Living,all,false),
            new("GapSeeker",Channel.Context,2,Tier.Living,all,false)
        };
        internal readonly record struct Plan(int Architecture,int Tissue,int Reproductive,int Surface,int Context)
        {
            internal bool Valid=>Architecture+Tissue+Reproductive+Surface+Context>0;
            internal int Count=>(Architecture>0?1:0)+(Tissue>0?1:0)+(Reproductive>0?1:0)+(Surface>0?1:0)+(Context>0?1:0);
        }
        internal sealed record Decision(Plan Plan,string[] Accepted,string[] Rejected,bool Unknown);
        internal static string Id(string id)=>id!=null&&id.StartsWith("HNS_",StringComparison.Ordinal)?id.Substring(4):id??"";
        private static readonly Dictionary<string,Allele> byId=Library.ToDictionary(a=>a.Id,StringComparer.Ordinal);
        internal static bool Known(string id)=>byId.ContainsKey(Id(id));
        internal static bool QualifiedSpecies(string def)=>def is "Plant_Corn" or "Plant_Cotton" or "Plant_Daylily";
        internal static bool Eligible(string id,string def)=>byId.TryGetValue(Id(id),out var a)&&QualifiedSpecies(def)&&a.Families.Contains(FamilyOf(def));
        internal static float DefaultWeight(string id)=>byId.TryGetValue(Id(id),out var a)?a.Tier switch{Tier.Natural=>.12f,Tier.Living=>.075f,Tier.Exceptional=>.035f,_=>.008f}:0f;
        internal static string ChannelTag(string id)=>byId.TryGetValue(Id(id),out var a)?"HNSExpression_"+a.Channel:null;
        internal static IEnumerable<string> EffectiveTags(string id,IEnumerable<string> tags,bool enabled)
        {
            string channel=enabled?ChannelTag(id):null;
            foreach(var t in tags??Enumerable.Empty<string>())
            {
                if(!enabled){yield return t;continue;}
                if(channel!=null&&t is "botanicalDetail" or "responsiveHabit" or "surfaceFinish" or "plantForm")continue;
                yield return t;
                // Unpromoted old rendering families conflict with promoted channels
                // that would otherwise suppress the old effect.
                if(channel==null&&t=="botanicalDetail"){yield return "HNSExpression_Architecture";yield return "HNSExpression_Tissue";}
                if(channel==null&&t=="responsiveHabit")yield return "HNSExpression_Reproductive";
                if(channel==null&&t=="surfaceFinish")yield return "HNSExpression_Surface";
                if(channel==null&&t=="plantForm")yield return "HNSExpression_Context";
            }
            if(channel!=null)yield return channel;
        }
        internal static Family FamilyOf(string def)=>def switch
        {
            "Plant_Corn" or "Plant_Rice" or "Plant_Haygrass"=>Family.Cereal,
            "Plant_Cotton" or "Plant_Strawberry" or "Plant_Hops"=>Family.Broadleaf,
            "Plant_Daylily" or "Plant_Rose"=>Family.Flower,
            _=>Family.Unknown
        };
        internal static Decision Resolve(IEnumerable<string> ids,Family family,int organs)
        {
            var selected=new Dictionary<Channel,Allele>();var rejected=new List<string>();bool unknown=false;
            foreach(string id in ids.Select(Id).Distinct(StringComparer.Ordinal).OrderBy(i=>i,StringComparer.Ordinal))
            {
                var a=Library.FirstOrDefault(a=>a.Id==id);
                if(a.Id==null){unknown=true;rejected.Add(id+": not promoted");continue;}
                if(!a.Families.Contains(family)||a.Organs&&organs<(a.Channel==Channel.Reproductive?2:1))
                {rejected.Add(id+": incompatible anatomy/species");continue;}
                if(selected.ContainsKey(a.Channel)){rejected.Add(id+": channel occupied");continue;}
                selected.Add(a.Channel,a);
            }
            int C(Channel c)=>selected.TryGetValue(c,out var a)?a.Code:0;
            return new(new(C(Channel.Architecture),C(Channel.Tissue),C(Channel.Reproductive),C(Channel.Surface),C(Channel.Context)),
                selected.OrderBy(k=>k.Key).Select(k=>k.Value.Id).ToArray(),rejected.ToArray(),unknown);
        }
        internal static int Band(float growth)=>Math.Clamp((int)MathF.Round(Math.Clamp(float.IsFinite(growth)?growth:0f,0f,1f)*8f),0,8);
        // Persistent thing IDs are saved by RimWorld. No position-derived seed,
        // new RNG calls, save fields or individual materials.
        internal static int Seed(int id)=>(id&int.MaxValue)%8;
        internal static float HistoryBias(int code)
        {
            float v=0;for(int i=0;i<3;i++)
            {
                int c=ChronicleMath.At(code,i);
                v+=(c==2?1f:c==3?-1f:0f)*(i+1);
            }
            return Math.Clamp(v/6f,-1f,1f);
        }
        internal static string[] Recipe(int row,int variant)
        {
            string[][][] choices={
                new[]{new[]{"WingedPetioles","BronzeReticulum","RipeningMosaic","SatinCuticle","CrookedChronicle"},new[]{"CandelabraTips","IvoryNerves","SectorSport","SilverBloom","GapSeeker"},new[]{"PendantRaceme","CrimsonNetwork","RipeningMosaic","RainBeads","CrookedChronicle"}},
                new[]{new[]{"PendantRaceme","CrimsonNetwork","SectorSport","PearlCuticle","CrookedChronicle"},new[]{"UmbelCrown","IvoryNerves","RipeningMosaic","SilverBloom","GapSeeker"},new[]{"PetaloidDoubling","BronzeReticulum","SectorSport","SatinCuticle","CrookedChronicle"}},
                new[]{new[]{"UmbelCrown","IvoryNerves","SectorSport","SatinCuticle","CrookedChronicle"},new[]{"HoodedSpathes","CrimsonNetwork","RipeningMosaic","PearlCuticle","GapSeeker"},new[]{"PetaloidDoubling","BronzeReticulum","SectorSport","SilverBloom","CrookedChronicle"}}
            };
            return choices[Math.Clamp(row,0,2)][Math.Clamp(variant,0,2)];
        }
    }
}
