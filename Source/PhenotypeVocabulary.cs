using System;
using System.Collections.Generic;
using System.Linq;

namespace HorticultureNovelSeeds
{
    // Semantic observations are independent of trait names and presentation.
    // A renderer reports a fact only after accepting its corresponding effect.
    internal static class PhenotypeVocabulary
    {
        internal enum Region { Habit, Foliage, Reproductive, Stem, Surface, Ground, Motion, Light, General }
        internal readonly record struct Fact(Region Region,string Slot,string Phrase,int Priority=10);
        internal static Fact F(Region r,string slot,string phrase,int priority=10)=>new(r,slot,phrase,priority);
        internal static string Join(IEnumerable<string> parts)
        {
            var a=parts.Where(s=>!string.IsNullOrWhiteSpace(s)).Distinct(StringComparer.Ordinal).ToArray();
            return a.Length switch{0=>"",1=>a[0],2=>a[0]+" and "+a[1],_=>string.Join(", ",a.Take(a.Length-1))+", and "+a.Last()};
        }
        internal static Fact[] Resolve(IEnumerable<Fact> facts)=>facts.Where(f=>!string.IsNullOrWhiteSpace(f.Phrase))
            .GroupBy(f=>(f.Region,f.Slot)).Select(g=>g.OrderByDescending(f=>f.Priority).ThenBy(f=>f.Phrase,StringComparer.Ordinal).First())
            .OrderBy(f=>f.Region).ThenBy(f=>f.Slot,StringComparer.Ordinal).ToArray();
        internal static string Describe(IEnumerable<Fact> facts,bool potential=false)
        {
            var groups=Resolve(facts).GroupBy(f=>f.Region);
            var parts=new List<string>();
            foreach(var g in groups)
            {
                string p=Join(g.Select(f=>f.Phrase));
                string subject=g.Key switch
                {
                    Region.Habit=>"Growth habit",Region.Foliage=>"Foliage",Region.Reproductive=>"Reproductive structures",
                    Region.Stem=>"Stems",Region.Surface=>"Surface",Region.Ground=>"Ground growth",
                    Region.Motion=>"Movement",Region.Light=>"Light effects",_=>"Overall appearance"
                };
                parts.Add(subject+": "+p);
            }
            if(parts.Count==0)return "";
            if(potential)return "May express "+string.Join("; ",parts.Select(p=>char.ToLowerInvariant(p[0])+p.Substring(1)))+".";
            return string.Join("; ",parts)+".";
        }
        // Field-note prose for inspection. The same normalized facts power
        // the labeled detailed field guide and conflict resolution.
        internal static string Prose(IEnumerable<Fact> facts)
        {
            var fs=Resolve(facts);
            if(fs.Length==0)return "";
            var sentences=new List<string>();
            string P(params Region[] regions)=>Join(fs.Where(f=>regions.Contains(f.Region)).Select(f=>f.Phrase));
            void Add(string prefix,string text){if(text.Length>0)sentences.Add(prefix+text+".");}
            Add("This specimen displays ",P(Region.Habit,Region.Foliage,Region.Stem,Region.Reproductive));
            Add("It also shows ",P(Region.Surface,Region.General,Region.Ground,Region.Light));
            Add("Its movement is characterized by ",P(Region.Motion));
            return string.Join(" ",sentences);
        }
        internal static Fact Form(PlantFormKind k,float strength,NeighborhoodMath.Pressure pressure,int code,bool potential=false)
        {
            bool left=pressure.Difference>0,right=pressure.Difference<0;
            bool crowded=Enumerable.Range(0,3).Any(i=>ChronicleMath.At(code,i)>=2);
            bool directional=Enumerable.Range(0,3).Any(i=>ChronicleMath.At(code,i) is 2 or 3);
            string p=k switch
            {
                PlantFormKind.Arching=>"an arching stem habit",
                PlantFormKind.FanCrowned=>"a fan-shaped crown",
                PlantFormKind.Rounded=>"a broad rounded canopy",
                PlantFormKind.Weeping=>"a drooping canopy",
                PlantFormKind.Spired=>"a tall narrow habit",
                PlantFormKind.GapSeeker=>potential?"crowns that lean toward available space":left?"upper growth bending right into open space":right?"upper growth bending left into open space":pressure.Density>0?"a slightly compressed crown":null,
                PlantFormKind.CompanionBow=>potential?"crowns that incline toward neighbors":left?"upper growth inclining toward a left-hand neighbor":right?"upper growth inclining toward a right-hand neighbor":pressure.Density>0?"a slightly compressed crown":null,
                PlantFormKind.CrownPartition=>potential||pressure.Density>0?"a crown shaped around neighboring vegetation":null,
                PlantFormKind.StandSpire=>potential||pressure.Density>0?"a narrow elongated canopy in crowded space":null,
                PlantFormKind.FreeRosette=>potential?"a spreading solitary or compact crowded habit":pressure.Density<.5f?"a low spreading crown":"a compact crown",
                PlantFormKind.CrookedChronicle=>potential?"a stalk that retains earlier bends":directional?"a stalk retaining bends from earlier competition":crowded?"a compressed stalk reflecting earlier crowding":null,
                PlantFormKind.RingedHabit=>potential||crowded?"successive compressed canopy layers":null,
                PlantFormKind.AsymmetricWhorls=>potential||directional?"asymmetric growth layers":crowded?"compressed older growth layers":null,
                PlantFormKind.ReleaseCrown=>potential||ChronicleMath.At(code,2)==1&&(ChronicleMath.At(code,0)>=2||ChronicleMath.At(code,1)>=2)?"a spreading upper crown above compact older growth":crowded?"a canopy retaining earlier compression":null,
                PlantFormKind.StretchBands=>potential||crowded?"elongated sections recording earlier crowding":null,
                _=>null
            };
            return F(Region.Habit,"shape",p,40);
        }
        internal static Fact Organ(OrganFormKind k)=>F(k==OrganFormKind.ThickStalk?Region.Stem:Region.Reproductive,"shape",k switch
        {
            OrganFormKind.PlumpFruit=>"plump reproductive organs",OrganFormKind.LongFruit=>"elongated reproductive organs",OrganFormKind.BroadFruit=>"broad reproductive organs",OrganFormKind.ClusteredFruit=>"clustered reproductive organs",OrganFormKind.ThickStalk=>"thickened stems",_=>null
        },40);
        internal static Fact Architecture(CanopyArchitectureKind k)=>F(k is CanopyArchitectureKind.FlowerSpray or CanopyArchitectureKind.AbundantClusters?Region.Reproductive:Region.Habit,"modules",k switch
        {
            CanopyArchitectureKind.ForkedCrown=>"a forked crown",CanopyArchitectureKind.TieredCanopy=>"tiered canopy growth",CanopyArchitectureKind.SpreadingRosette=>"a spreading rosette",CanopyArchitectureKind.FlowerSpray=>"a spray of additional reproductive growth",CanopyArchitectureKind.AbundantClusters=>"additional reproductive clusters",_=>null
        },40);
        internal static Fact Detail(BotanicalDetailKind k)=>F(k is BotanicalDetailKind.LanternBuds or BotanicalDetailKind.BioluminescentSignaling?Region.Light:k is BotanicalDetailKind.SubsurfaceRootHalo or BotanicalDetailKind.MycelialRelay?Region.Ground:k is BotanicalDetailKind.PollinationCloud or BotanicalDetailKind.TranslucentPetalTissue or BotanicalDetailKind.FruitRipeningCascade?Region.Reproductive:k==BotanicalDetailKind.WindDifferentialAnatomy?Region.Motion:k is BotanicalDetailKind.NyctinasticFolding or BotanicalDetailKind.HydraulicWilt or BotanicalDetailKind.MicroHabitatResponse or BotanicalDetailKind.CanopyVolumeParallax or BotanicalDetailKind.MorphogeneticReconfiguration or BotanicalDetailKind.EventDrivenExpression?Region.Habit:k==BotanicalDetailKind.CrystalDewLensing?Region.Surface:k>=BotanicalDetailKind.CircinateFrond&&k<=BotanicalDetailKind.CrestedFan?Region.Habit:Region.Foliage,"detail",k switch
        {
            BotanicalDetailKind.LobedLeaf=>"lobed leaf edges",BotanicalDetailKind.Windowleaf=>"small windows in the foliage",BotanicalDetailKind.SideShoots=>"additional lateral shoots",BotanicalDetailKind.CurlingTendrils=>"curling tendrils",BotanicalDetailKind.LanternBuds=>"softly luminous reproductive organs",BotanicalDetailKind.IvoryNerves=>"cream-colored internal veins",BotanicalDetailKind.CrimsonNetwork=>"wine-red branching veins and localized blush",BotanicalDetailKind.LivingGlass=>"light-sensitive thin foliage with shaded veins",BotanicalDetailKind.VascularChimera=>"localized cream-green foliage sectors",BotanicalDetailKind.BronzeReticulum=>"bronze capillary-like markings",BotanicalDetailKind.PhotosyntheticPulse=>"traveling daylight-sensitive vein highlights",BotanicalDetailKind.NyctinasticFolding=>"a folded nighttime crown",BotanicalDetailKind.HydraulicWilt=>"heat- or injury-stressed drooping growth",BotanicalDetailKind.CrystalDewLensing=>"small dew-like lens highlights",BotanicalDetailKind.PollinationCloud=>"sparse drifting pollen grains",BotanicalDetailKind.MosaicOrganism=>"individual cream, green and wine-colored tissue sectors",BotanicalDetailKind.SeasonalColorFront=>"an advancing bronze and gold seasonal color front",BotanicalDetailKind.SubsurfaceRootHalo=>"root-like traces in the surrounding soil",BotanicalDetailKind.LeafSkeletonization=>"partly skeletal late-season foliage",BotanicalDetailKind.BioluminescentSignaling=>"localized luminous veins and reproductive tissue",BotanicalDetailKind.WindDifferentialAnatomy=>"differential movement of the crown and reproductive organs",BotanicalDetailKind.FractalVenationBloom=>"progressively revealed golden branching veins",BotanicalDetailKind.TranslucentPetalTissue=>"light-brightened reproductive tissue edges",BotanicalDetailKind.CanopyVolumeParallax=>"a layered, depth-like foliage canopy",BotanicalDetailKind.MicroHabitatResponse=>"a crown inclined into available lateral space",BotanicalDetailKind.FruitRipeningCascade=>"reproductive clusters at different apparent ripening stages",BotanicalDetailKind.FrostEtching=>"pale frosted foliage markings",BotanicalDetailKind.MycelialRelay=>"fine pulsing ground connections to neighboring compatible plants",BotanicalDetailKind.MorphogeneticReconfiguration=>"later-developing lateral foliage modules",BotanicalDetailKind.EventDrivenExpression=>"rain-opened, moisture-darkened foliage",BotanicalDetailKind.CircinateFrond=>"unfurling pinnate lateral fronds",BotanicalDetailKind.ClimbingHelix=>"a winding leafy secondary stem",BotanicalDetailKind.UmbelCrown=>"an umbrella-shaped spray of reproductive stalks",BotanicalDetailKind.PendantRaceme=>"arching stems with hanging reproductive clusters",BotanicalDetailKind.CrestedFan=>"a broad crested fan of leaves and reproductive clusters",_=>null
        },40);
        internal static Fact Ground(GroundHabitKind k)=>F(Region.Ground,"habit",k switch
        {
            GroundHabitKind.Rootflare=>"a flared root base",GroundHabitKind.RunnerHabit=>"spreading runners",GroundHabitKind.BasalCarpet=>"a low carpet of foliage",GroundHabitKind.LeafLitter=>"scattered basal leaf litter",GroundHabitKind.PetalDrift=>"fallen petals around the base",_=>null
        },40);
        internal static Fact Wind(WindHabitKind k)=>F(Region.Motion,"wind",k switch
        {
            WindHabitKind.RigidHabit=>"restrained, rigid sway",WindHabitKind.SuppleHabit=>"supple stem movement",WindHabitKind.Flutterleaf=>"fine fluttering motion",WindHabitKind.NoddingHabit=>"nodding growth",WindHabitKind.SweepingHabit=>"broad sweeping sway",_=>null
        },40);
        internal static Fact Surface(SurfaceFinishKind k,bool wet,bool potential=false)=>F(Region.Surface,"finish",k switch
        {
            SurfaceFinishKind.SatinCuticle=>"a satin-like cuticle",SurfaceFinishKind.VelvetNap=>"a soft velvet-like finish",SurfaceFinishKind.SilverBloom=>"a silvery wax bloom",SurfaceFinishKind.RainBeads=>potential||wet?"beaded moisture on the tissue":null,SurfaceFinishKind.PearlCuticle=>"a pearlescent cuticle",_=>null
        },40);
        internal static Fact Pose(ResponsiveHabitKind k,int frame,float contraction,float lean,float droop,bool potential=false)
        {
            string p=k switch
            {
                ResponsiveHabitKind.Nightfold=>potential||contraction>.08f?"folded foliage in darkness":null,
                ResponsiveHabitKind.Nightbloom=>potential||contraction>.08f?"a contracted daylight crown":null,
                ResponsiveHabitKind.Sunseeker=>potential||Math.Abs(lean)>.02f?"a light-oriented crown":null,
                ResponsiveHabitKind.Rainbowed=>potential||droop>.03f?"rain-bowed foliage":null,
                ResponsiveHabitKind.Raincup=>potential||contraction>.08f?"rain-cupped foliage":null,
                ResponsiveHabitKind.DawnPetals or ResponsiveHabitKind.DuskPetals=>potential||frame<8?"partly closed reproductive organs":null,
                ResponsiveHabitKind.RainPendant=>potential||frame>0?"drooping reproductive organs":null,
                ResponsiveHabitKind.SolarTilt=>potential||frame!=4?"tilted reproductive organs":null,
                ResponsiveHabitKind.SilkFlutter=>potential||frame!=4?"independently fluttering reproductive organs":null,
                ResponsiveHabitKind.SuccessionBloom=>"a mixture of small buds and native reproductive organs",
                ResponsiveHabitKind.SectorSport=>"a localized rose-colored reproductive sport",
                ResponsiveHabitKind.BiformHarvest=>"contrasting reproductive organ shapes",
                ResponsiveHabitKind.SisterBuds=>"small adjacent daughter buds",
                ResponsiveHabitKind.RipeningMosaic=>"uneven ripening colors",
                _=>null
            };
            return F(k<=ResponsiveHabitKind.Raincup?Region.Habit:k==ResponsiveHabitKind.SilkFlutter?Region.Motion:Region.Reproductive,"pose",p,40);
        }
        internal static Fact Mosaic(int mask,int changed,int total)
        {
            bool Has(MosaicMath.OrganState s)=>(mask&(1<<(int)s))!=0;
            var phrases=new List<string>();
            if(Has(MosaicMath.OrganState.Bud))phrases.Add("small green buds");
            if(Has(MosaicMath.OrganState.Sport))phrases.Add("rose-colored sports");
            if(Has(MosaicMath.OrganState.Stout))phrases.Add("stout organs");
            if(Has(MosaicMath.OrganState.Slender))phrases.Add("slender organs");
            if(Has(MosaicMath.OrganState.Daughter))phrases.Add("adjacent daughter buds");
            if(Has(MosaicMath.OrganState.Unripe))phrases.Add("green unripe-looking organs");
            if(Has(MosaicMath.OrganState.Senescent))phrases.Add("bronze late-stage organs");
            string scope=changed==1?"one localized group of ":changed*2<total?"a few localized groups of ":"several localized groups of ";
            return F(Region.Reproductive,"pose",phrases.Count==0?null:scope+Join(phrases)+" amid otherwise native growth",50);
        }
        // Mode numbers are the actual uniform, not the trait def name.
        internal static Fact Pigment(int mode,float phase)
        {
            Region r=mode>=36&&mode<=40||mode==54?Region.Reproductive:mode>=46&&mode<=48?Region.Stem:mode==26?Region.Light:Region.Foliage;
            string p=mode switch
            {
                1=>"cream-and-sage variegation",2=>"wine-red tip blush",3=>"pale sage-silver margins",4=>"ochre freckling",5=>"contrasting vein-like markings",
                6=>"warm bronze markings",7=>"irregular brown mottling",8=>"deep green mottling",9=>"mineral-like speckling",10=>"teal rippled markings",
                11=>"resin-like flecks",12=>"violet markings",13=>"copper markings",14=>"lime-green markings",15=>"dark edge markings",
                16=>"velvety-looking foliage",17=>"lacquer-like highlights",18=>"a dusty bloom",19=>"silvered foliage",20=>"pale highlighted margins",
                21=>"netted vein-like markings",22=>"striped foliage",23=>"marbled foliage",24=>"opal-like shading",25=>"crystalline-looking highlights",
                26=>"luminous-looking markings",27=>"clouded mottling",28=>"coral-toned accents",29=>"prismatic-looking accents",
                30=>"pale salt-like bloom",31=>"ashen coloration",32=>"glossy bog-green tones",33=>"waxy-looking markings",34=>"peat-brown staining",35=>"scorched-looking margins",
                36=>"honey-gold reproductive pigment",37=>"ivory reproductive pigment",38=>"mulberry reproductive pigment",39=>"rose reproductive pigment",40=>"two-tone reproductive pigment",
                41=>"cream leaf margins",42=>"lime-green leaf centers",43=>"copper-colored tips",44=>"ivory speckles",45=>"burgundy margins",
                46=>"ruby-colored stalks",47=>"amber stalks",48=>"striped stalks",49=>"blue-jade foliage",50=>"contrasting canopy tones",
                51=>phase<.25f?"young green foliage with a faint bronze flush":phase<.75f?"developing bronze coloration":"a mature bronze flush",
                52=>phase<.25f?"pale young foliage":phase<.75f?"a receding pale bloom":"a residual pale bloom",
                53=>phase<.25f?"early green canopy":phase<.75f?"emerging autumn coloration":"autumnal crown coloration",
                54=>phase<.25f?"early reproductive pigment":phase<.75f?"developing ripening blush":"a mature ripening blush",
                55=>phase<.25f?"faint early variegation":phase<.75f?"developing cream sectors":"mature cream variegation",
                _=>null
            };
            return F(r,"pigment",p,30);
        }
        internal static string History(int code)
        {
            string[] stages={"Early growth","Middle growth","Late growth"};
            string[] context={null,"open lateral space","competition on the left","competition on the right","competition on both sides"};
            var parts=new List<string>();
            for(int i=0;i<3;i++)
            {
                int x=ChronicleMath.At(code,i);
                if(x>0&&x<=4)parts.Add(stages[i]+": "+context[x]);
            }
            return parts.Count==0?"":"Recorded growth history: "+string.Join("; ",parts)+".";
        }
    }
}
