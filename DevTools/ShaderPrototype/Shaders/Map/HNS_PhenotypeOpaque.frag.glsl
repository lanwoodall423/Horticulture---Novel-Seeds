#version 450
// Opaque nonseasonal phenotype. R/G/B mask channels are mutually exclusive.
// The installed vanilla plant vertex stage preserves the wind inputs.
layout(location = 1) in vec2 vUv;
layout(location = 0) out vec4 outColor;
layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _Color; // @color @default = vec4(1.0, 1.0, 1.0, 1.0)
    vec4 _MainTex_ST; // @default = vec4(0.0, 0.0, 1.0, 1.0)
    float _SwayHead; // @default = 0.0
    vec4 _HnsTint; // @default = vec4(1.0, 1.0, 1.0, 1.0)
    vec4 _HnsControls; // @default = vec4(0.0, 1.0, 1.0, 1.0)
    float _HnsDullness; // @default = 0.0
    float _HnsMaskEnabled; // @default = 0.0
    vec4 _HnsTint1;
    vec4 _HnsControls1;
    float _HnsDullness1;
    vec4 _HnsTint2;
    vec4 _HnsControls2;
    float _HnsDullness2;
    vec4 _HnsPattern; // @default = vec4(0.0, 0.0, 0.0, 1.0)
    vec4 _HnsWind; // @default = vec4(1.0, 1.0, 1.0, 0.0)
} uMaterial;
layout(set = 2, binding = 1) uniform sampler2D _MainTex;
layout(set = 2, binding = 2) uniform sampler2D _HnsMask;

vec3 toHsv(vec3 rgb)
{
    float hi = max(rgb.r, max(rgb.g, rgb.b));
    float lo = min(rgb.r, min(rgb.g, rgb.b));
    float d = hi - lo;
    float h = 0.0;
    if (d > 0.0000001)
    {
        if (hi == rgb.r) h = (rgb.g - rgb.b) / d;
        else if (hi == rgb.g) h = 2.0 + (rgb.b - rgb.r) / d;
        else h = 4.0 + (rgb.r - rgb.g) / d;
        h = fract(h / 6.0);
    }
    return vec3(h, hi > 0.0000001 ? d / hi : 0.0, hi);
}

vec3 fromHsv(vec3 hsv)
{
    vec3 wave = clamp(abs(fract(hsv.xxx + vec3(0.0, 2.0/3.0, 1.0/3.0)) * 6.0 - 3.0) - 1.0, 0.0, 1.0);
    return hsv.z * mix(vec3(1.0), wave, hsv.y);
}

vec3 compose(vec3 src, vec3 tint, vec4 controls, float dullness)
{
    vec3 hsv = toHsv(src);
    float h = fract(hsv.x + controls.x);
    float s = clamp(hsv.y * controls.y, 0.0, 1.0);
    float tintMax = max(tint.r, max(tint.g, tint.b));
    float tintMin = min(tint.r, min(tint.g, tint.b));
    float chroma = clamp(tintMax - tintMin, 0.0, 1.0);
    if (chroma > 0.001)
    {
        vec3 th = toHsv(clamp(tint, 0.0, 1.0));
        float delta = fract(th.x - h + 0.5) - 0.5;
        if (delta == -0.5) delta = 0.5; // Mathf.DeltaAngle tie convention
        h = fract(h + delta * chroma);
        s = mix(s, max(s, th.y), chroma);
    }
    s *= mix(1.0, 0.35, clamp(dullness, 0.0, 1.0));
    float v = clamp(((hsv.z - 0.5) * controls.w + 0.5) * controls.z * max(0.0, tintMax), 0.0, 1.0);
    float outline = mix(0.12, 1.0, smoothstep(0.06, 0.34, hsv.z));
    return mix(src, fromHsv(vec3(h, clamp(s, 0.0, 1.0), v)), outline);
}

// All pattern coordinates are local sprite UVs, not guessed anatomy.
// Use large, legible patches and preserve the source's value/outline detail.
float hnsHash(vec2 p)
{
    vec3 q = fract(vec3(p.xyx) * 0.1031);
    q += dot(q, q.yzx + 33.33);
    return fract((q.x + q.y) * q.z);
}
float hnsNoise(vec2 p)
{
    vec2 i = floor(p), f = fract(p);
    f = f*f*(3.0-2.0*f);
    return mix(mix(hnsHash(i),hnsHash(i+vec2(1,0)),f.x),
               mix(hnsHash(i+vec2(0,1)),hnsHash(i+vec2(1,1)),f.x),f.y);
}
// Local sprite rim and a bounded cellular feature search.
float hnsRim(vec2 uv, float pixels)
{
    vec2 d = pixels/vec2(textureSize(_MainTex,0));
    return clamp(1.0-min(min(texture(_MainTex,uv+vec2(d.x,0)).a,texture(_MainTex,uv-vec2(d.x,0)).a),
                         min(texture(_MainTex,uv+vec2(0,d.y)).a,texture(_MainTex,uv-vec2(0,d.y)).a)),0.0,1.0);
}
vec2 hnsPoint(vec2 id)
{
    return vec2(hnsHash(id+vec2(5,13)),hnsHash(id+vec2(11,2)));
}
vec2 hnsCells(vec2 uv)
{
    vec2 cell = floor(uv), pos = fract(uv);
    float a = 8.0, b = 8.0;
    for (int y=-1;y<=1;y++)
        for (int x=-1;x<=1;x++)
        {
            vec2 off=vec2(x,y);
            float dist=length(off+.16+.68*hnsPoint(cell+off)-pos);
            if (dist<a) { b=a; a=dist; }
            else b=min(b,dist);
        }
    return vec2(a,b);
}

// Color affinity is continuous, not a hard anatomical classifier.
// Where a validated mask is already active, it takes priority.
float hnsTissueAffinity(vec3 src, vec3 regions)
{
    float hi = max(src.r,max(src.g,src.b));
    float lo = min(src.r,min(src.g,src.b));
    float chroma = (hi-lo)/max(hi,.001);
    float greenLead = (src.g-max(src.r,src.b))/max(hi,.001);
    float leaf = smoothstep(.005,.11,greenLead)*smoothstep(.065,.25,chroma);
    float affinity = mix(.13,1.0,leaf);
    float pale = (1.0-smoothstep(.07,.27,chroma))*smoothstep(.42,.83,hi);
    float warm = smoothstep(.035,.27,(src.r-src.g)/max(hi,.001));
    affinity *= (1.0-.75*pale)*(1.0-.55*warm);
    if (uMaterial._HnsMaskEnabled >= .5)
    {
        if (regions.g>.5) affinity = max(affinity,.76);
        else if (regions.r>.5) affinity = min(affinity,.10);
        else if (regions.b>.5) affinity = min(affinity,.28);
        else affinity *= .45;
    }
    return clamp(affinity,0.0,1.0);
}

// Reproductive-color affinity: a soft palette heuristic, not a claim that
// every warm or pale pixel is fruit. Shadows and green tissue are excluded.
// When an already-active automatic mask is available, use its produce region.
float hnsHarvestAffinity(vec3 src, vec3 regions)
{
    float hi=max(src.r,max(src.g,src.b)), lo=min(src.r,min(src.g,src.b));
    float chroma=(hi-lo)/max(hi,.001);
    float warm=smoothstep(-.055,.13,(src.r-src.g)/max(hi,.001)) *
        smoothstep(.15,.46,(src.r-src.b)/max(hi,.001)) *
        smoothstep(.26,.57,hi);
    float pale=(1.0-smoothstep(.07,.24,chroma))*smoothstep(.48,.78,hi);
    float green=smoothstep(.015,.12,(src.g-max(src.r,src.b))/max(hi,.001));
    float amount=max(warm,pale)*(1.0-green);
    if (uMaterial._HnsMaskEnabled >= .5)
    {
        if (regions.r>.5) amount=max(amount,.82);
        else amount*=.10;
    }
    return clamp(amount,0.0,1.0);
}
// A lower-central sprite proxy for stalks. Green-color affinity still gates
// it; this cannot identify individual stems on a dense canopy.
float hnsStalkAffinity(vec2 uv, float leafAffinity, vec3 regions)
{
    float central=1.0-smoothstep(.085,.25,abs(uv.x-.5));
    float lower=1.0-smoothstep(.42,.73,uv.y);
    float proxy=central*lower*leafAffinity;
    if (uMaterial._HnsMaskEnabled >= .5)
    {
        if (regions.b>.5) return .92;
        return proxy*.15;
    }
    return proxy;
}

vec3 hnsPattern(vec3 rgb, vec4 src, vec2 localUv, vec2 sampleUv, vec3 regions)
{
    vec4 p = uMaterial._HnsPattern;
    if (p.x < 0.5 || p.z <= 0.0) return rgb;
    float seed = p.y*37.0;
    vec2 q = localUv + vec2(seed,seed*.731);
    float n = hnsNoise(q*4.0);
    float fine = hnsNoise(q*13.0);
    float value = max(src.r,max(src.g,src.b));
    float shade = smoothstep(0.045,0.23,value);
    float affinity = hnsTissueAffinity(src.rgb, regions);
    // Preserve normal tissue between expressed sectors, even on all-green crops.
    float expression = mix(.73,1.0,smoothstep(.16,.82,hnsNoise(q*2.7+vec2(3.7,1.2))));
    // Keep a little of the original hue and all of the original luminance
    // structure rather than painting flat opaque bands over a sprite.
    float lightness = clamp(.28+.93*value,0.0,1.0);
    vec3 target = rgb;
    float blend = 0.0;
    if (p.x < 1.5)
    {
        float ribbon = sin((localUv.x*2.65 + localUv.y*.72 + n*.93 + p.y)*6.283185);
        float sector = smoothstep(.02,.32,ribbon + .72*(fine-.5));
        target = vec3(1.0,.91,.63)*lightness;
        blend = sector*.92;
    }
    else if (p.x < 2.5)
    {
        float blush = smoothstep(.13,.83,localUv.y+.38*(n-.5));
        target = mix(vec3(.34,.075,.28),vec3(.76,.30,.39),smoothstep(.22,.91,value));
        blend = .12+.88*blush;
    }
    else if (p.x < 3.5)
    {
        vec2 stepUv = 1.5 / vec2(textureSize(_MainTex,0));
        float rim = 1.0 - min(min(texture(_MainTex,sampleUv+vec2(stepUv.x,0)).a,texture(_MainTex,sampleUv-vec2(stepUv.x,0)).a),
                            min(texture(_MainTex,sampleUv+vec2(0,stepUv.y)).a,texture(_MainTex,sampleUv-vec2(0,stepUv.y)).a));
        target = mix(vec3(.25,.39,.40),vec3(.79,.87,.82),clamp(.79*value+.24*rim+.08*fine,0.0,1.0));
        blend = .62+.30*rim;
    }
    else if (p.x < 4.5)
    {
        vec2 cells = q*8.5;
        vec2 id = floor(cells);
        vec2 offset = vec2(hnsHash(id+vec2(7,3)),hnsHash(id+vec2(2,13)))*.50+.25;
        float radius = .18+.12*hnsHash(id+vec2(12,1));
        float spot = (1.0-smoothstep(radius-.045,radius+.055,length(fract(cells)-offset))) * step(.25,hnsHash(id));
        float mottle = smoothstep(.59,.87,n);
        target = mix(vec3(.64,.42,.08),vec3(1.0,.86,.30),lightness);
        blend = max(spot*.98,mottle*.42);
    }
    else if (p.x < 5.5)
    {
        // Stylized branching light markings, not actual anatomy or light emission.
        float curve = .045*sin(localUv.y*12.0+seed)+.035*(n-.5);
        float x = localUv.x-.5+curve;
        float mid = 1.0-smoothstep(.018,.040,abs(x));
        float branchWave = abs(fract(localUv.y*4.4-abs(x)*2.1+.10*fine)-.5);
        float branch = (1.0-smoothstep(.065,.13,branchWave))*smoothstep(.02,.15,abs(x));
        float veins = clamp(max(mid,branch*.86),0.0,1.0);
        vec3 jade = mix(rgb,vec3(.11,.36,.29)*(.55+value),.46);
        vec3 cyan = mix(vec3(.19,.69,.48),vec3(.80,.97,.74),lightness);
        target = mix(jade,cyan,veins);
        blend = .24+.76*veins;
    }
    else if (p.x < 6.5)
    {
        float tip = smoothstep(.3,.93,localUv.y + .28*(n-.5));
        target = mix(vec3(.55,.34,.09),vec3(.95,.77,.34),lightness);
        blend = .20 + .80*tip;
    }
    else if (p.x < 7.5)
    {
        float lesion = smoothstep(.51,.77,hnsNoise(q*7.0)+.16*fine);
        target = mix(vec3(.20,.12,.16),vec3(.65,.38,.24),value);
        blend = .14+.86*lesion;
    }
    else if (p.x < 8.5)
    {
        target = mix(vec3(.08,.24,.12),vec3(.48,.79,.39),lightness);
        blend = .66 + .34*n;
    }
    else if (p.x < 9.5)
    {
        vec2 cells = q*11.0;
        vec2 id = floor(cells);
        vec2 center = vec2(hnsHash(id+vec2(2,7)),hnsHash(id+vec2(9,1)))*.5+.25;
        float dotMask = (1.0-smoothstep(.13,.24,length(fract(cells)-center))) * step(.32,hnsHash(id));
        target = vec3(.88,.81,.62)*lightness;
        blend = .11+.89*dotMask;
    }
    else if (p.x < 10.5)
    {
        float ripple = sin((localUv.y*4.0+localUv.x*.65+n*.50)*6.283185);
        target = mix(vec3(.08,.37,.36),vec3(.52,.91,.84),lightness);
        blend = .28+.72*smoothstep(-.1,.75,ripple);
    }
    else if (p.x < 14.5)
    {
        vec2 cells = q*7.5;
        vec2 id = floor(cells);
        vec2 center = vec2(hnsHash(id+vec2(3,7)),hnsHash(id+vec2(9,2)))*.45+.28;
        vec2 delta = fract(cells)-center;
        float bead = (1.0-smoothstep(.18,.29,length(delta))) * step(.32,hnsHash(id));
        float gleam = (1.0-smoothstep(.035,.11,length(delta+vec2(.06,.07))))*bead;
        vec3 resin = p.x < 11.5 ? vec3(.95,.66,.15) :
                     p.x < 12.5 ? vec3(.43,.20,.63) :
                     p.x < 13.5 ? vec3(.94,.30,.08) : vec3(.58,.95,.16);
        target = mix(resin*(.48+.52*value),vec3(1.0,.94,.75),gleam*.85);
        blend = .18+.82*bead;
    }
    else if (p.x < 15.5)
    {
        vec2 stepUv = 1.5 / vec2(textureSize(_MainTex,0));
        float edge = 1.0-min(min(texture(_MainTex,sampleUv+vec2(stepUv.x,0)).a,texture(_MainTex,sampleUv-vec2(stepUv.x,0)).a),
                              min(texture(_MainTex,sampleUv+vec2(0,stepUv.y)).a,texture(_MainTex,sampleUv-vec2(0,stepUv.y)).a));
        float serration = step(.15,sin(localUv.y*63.0+localUv.x*19.0));
        target = mix(vec3(.16,.10,.06),vec3(.66,.49,.26),value);
        blend = .12+.88*edge*serration;
    }
    // Creative modes: opaque surface illusions; alpha is never expanded.
    else if (p.x < 16.5) // Velvetleaf
    {
        float nap = hnsNoise(vec2(q.x*28.0,q.y*7.0));
        vec3 forest = mix(vec3(.045,.10,.075),vec3(.23,.47,.27),pow(value,.85));
        target = mix(forest,vec3(.42,.31,.35)*lightness,smoothstep(.55,.92,nap)*.26);
        blend = .74+.14*nap;
    }
    else if (p.x < 17.5) // Lacquered
    {
        float stripe = abs(fract(localUv.x*2.5+localUv.y*.55+n*.30)-.5);
        float gloss = 1.0-smoothstep(.025,.095,stripe);
        target = mix(vec3(.12,.34,.20)*(.45+value),vec3(.82,.91,.65)*lightness,gloss);
        blend = .43+.57*gloss;
    }
    else if (p.x < 18.5) // Dustbloom
    {
        float powder = smoothstep(.22,.75,.55*n+.45*hnsNoise(q*22.0));
        target = mix(vec3(.59,.63,.43),vec3(.88,.84,.68),powder)*(.30+.74*value);
        blend = .29+.71*powder;
    }
    else if (p.x < 19.5) // Silvered
    {
        float brushing = hnsNoise(vec2(q.x*40.0,q.y*5.0));
        float shine = smoothstep(.3,.82,value+.16*(brushing-.5));
        target = mix(vec3(.26,.39,.32),vec3(.87,.92,.83),shine);
        blend = .71+.26*shine;
    }
    else if (p.x < 20.5) // Halo-Edged
    {
        float edge = hnsRim(sampleUv,2.0);
        float inner = hnsRim(sampleUv,4.0);
        float rim = clamp(max(edge,.30*inner)*mix(.60,1.0,fine),0.0,1.0);
        target = mix(mix(rgb,vec3(.15,.29,.12)*(.50+value),.5),vec3(.96,.90,.62)*lightness,rim);
        blend = .16+.84*rim;
    }
    else if (p.x < 21.5) // Netveined
    {
        vec2 cell = hnsCells(q*5.4);
        float net = 1.0-smoothstep(.025,.13,cell.y-cell.x);
        target = mix(mix(rgb,vec3(.13,.36,.22)*(.6+value),.4),vec3(.85,.90,.58)*lightness,net);
        blend = .23+.77*net;
    }
    else if (p.x < 22.5) // Tigerstriped
    {
        float wave = sin((localUv.y*4.1+localUv.x*1.55+n*.72+fine*.12)*6.283185);
        float band = smoothstep(-.20,.35,wave);
        target = mix(vec3(.22,.125,.11),vec3(.94,.72,.27),band)*(.35+.75*value);
        blend = mix(.75,.94,band)*mix(.76,1.0,n);
    }
    else if (p.x < 23.5) // Marbled
    {
        float flow = sin((localUv.x*2.1+localUv.y*.9+n*2.1+fine*.27)*6.283185);
        float ribbon = smoothstep(-.20,.48,flow);
        float seam = 1.0-smoothstep(.025,.14,abs(flow-.17));
        target = mix(vec3(.29,.54,.29),vec3(.93,.88,.67),ribbon)*lightness;
        target = mix(target,vec3(.97,.96,.85)*lightness,seam*.68);
        blend = .22+.78*max(ribbon,seam);
    }
    else if (p.x < 24.5) // Opaline
    {
        float phase = .20+localUv.x*.60+localUv.y*.37+n*.31;
        vec3 pearl = mix(vec3(.55,.79,.59),vec3(.92,.65,.77),.5+.5*cos(phase*6.283185));
        float lustre = smoothstep(.35,.78,value+.18*fine);
        target = mix(rgb,pearl*lightness,.5+.5*lustre);
        blend = .33+.67*lustre;
    }
    else if (p.x < 25.5) // Crystalline
    {
        vec2 cell = hnsCells(q*6.0);
        float rim = 1.0-smoothstep(.012,.095,cell.y-cell.x);
        float facet = smoothstep(.12,.68,cell.x);
        vec3 ice = mix(vec3(.17,.37,.37),vec3(.74,.88,.83),facet);
        target = mix(ice*(.42+.68*value),vec3(.93,.96,.85)*lightness,rim*.83);
        blend = max(.52*facet,.96*rim)*mix(.30,1.0,smoothstep(.15,.75,localUv.y+.28*(n-.5)));
    }
    else if (p.x < 26.5) // Bioluminescent, painted-light illusion
    {
        vec2 st = q*6.0;
        vec2 id = floor(st);
        vec2 center = hnsPoint(id)*.55+.22;
        float dist = length(fract(st)-center);
        float aura = (1.0-smoothstep(.12,.35,dist))*step(.28,hnsHash(id));
        float core = (1.0-smoothstep(.10,.20,dist))*step(.28,hnsHash(id));
        target = mix(vec3(.055,.25,.18)*(.6+value),vec3(.35,.76,.52)*lightness,aura);
        target = mix(target,vec3(.94,1.0,.62)*lightness,core);
        blend = .34+.66*max(aura,core);
    }
    else if (p.x < 27.5) // Nebula Mottle
    {
        float cloud = smoothstep(.18,.80,hnsNoise(q*3.8+vec2(fine*.32,n*.26)));
        vec3 nebula = mix(vec3(.19,.25,.14),vec3(.64,.14,.36),cloud);
        vec2 st = q*11.0;vec2 id = floor(st);
        float star = (1.0-smoothstep(.08,.20,length(fract(st)-(.18+.64*hnsPoint(id)))))*step(.70,hnsHash(id));
        target = mix(nebula*(.50+.63*value),vec3(.89,.84,.64)*lightness,star);
        blend = max(.96*smoothstep(.20,.69,cloud),.98*star);
    }
    else if (p.x < 28.5) // Coralbranch
    {
        float bend = localUv.x-.5+.09*sin(localUv.y*10.0+seed);
        float trunk = 1.0-smoothstep(.025,.065,abs(bend));
        float branches = 1.0-smoothstep(.055,.145,abs(fract(localUv.y*3.8-abs(bend)*2.8+n*.18)-.5));
        float forks = max(trunk,branches);
        target = mix(mix(rgb,vec3(.35,.23,.24)*(.5+value),.45),vec3(.98,.45,.31)*lightness,forks);
        target = mix(target,vec3(.96,.78,.53)*lightness,forks*smoothstep(.55,1.0,localUv.y)*.5);
        blend = .22+.78*forks;
    }
    else if (p.x < 29.5) // Prismatic
    {
        float phase = localUv.x*.8+localUv.y*.62+n*.32+p.y;
        // Three neighboring botanical hues rather than a full rainbow.
        vec3 spectrum = mix(vec3(.35,.66,.40),vec3(.95,.60,.22),smoothstep(-.25,.8,cos(phase*6.283185)));
        spectrum = mix(spectrum,vec3(.60,.26,.47),smoothstep(.05,.82,sin(phase*6.283185))*.87);
        target = spectrum*lightness;
        blend = .46+.54*smoothstep(.25,.77,value+.16*fine);
    }
    else if (p.x < 30.5) // Saltbloom
    {
        float edge = hnsRim(sampleUv,2.5);
        float crust = max(edge*.9,smoothstep(.58,.83,fine)*.6);
        vec3 olive = vec3(.25,.40,.19)*(.5+value);
        target = mix(olive,vec3(.96,.94,.80)*lightness,crust);
        blend = .20+.80*crust;
    }
    else if (p.x < 31.5) // Ashen
    {
        float grain = hnsNoise(vec2(q.x*21.0,q.y*8.0));
        float soot = smoothstep(.2,.85,.64*value+.36*grain);
        target = mix(vec3(.19,.24,.19),vec3(.70,.73,.65),soot);
        blend = .65+.30*soot;
    }
    else if (p.x < 32.5) // Boggloss
    {
        float slick = smoothstep(.44,.83,n+.13*fine);
        float line = 1.0-smoothstep(.03,.13,abs(fract(localUv.y*2.7+localUv.x*.8+n*.41)-.5));
        target = mix(vec3(.17,.25,.10),vec3(.25,.55,.37),slick)*(.50+.63*value);
        target = mix(target,vec3(.73,.89,.65)*lightness,line*slick*.68);
        blend = .45+.40*slick;
    }
    else if (p.x < 33.5) // Waxbound
    {
        float broad = smoothstep(.1,.9, .63*value+.37*n);
        target = mix(vec3(.30,.49,.24),vec3(.75,.84,.48),broad);
        blend = .52+.26*broad;
    }
    else if (p.x < 34.5) // Peatstained
    {
        float sediment = 1.0-smoothstep(.16,.81,localUv.y+.42*(n-.5));
        target = mix(vec3(.27,.37,.17),vec3(.38,.19,.075),sediment)*(.45+.74*value);
        blend = .13+.75*sediment;
    }
    else if (p.x < 35.5) // Scorched Margins
    {
        float edge = hnsRim(sampleUv,2.0);
        float tip = smoothstep(.55,.96,localUv.y+.18*(n-.5));
        float burn = max(edge*.82,tip*.67)*mix(.47,1.0,n);
        target = mix(vec3(.22,.095,.045),vec3(.88,.48,.19),lightness);
        blend = .05+.95*burn;
    }
    else if (p.x < 40.5) // Reproductive organ palette, 36..40
    {
        affinity = hnsHarvestAffinity(src.rgb,regions);
        // Preserve source variation on berries, corn kernels and pale bolls.
        float tissue = clamp(.34+.78*value,0.0,1.0);
        if (p.x < 36.5) target = vec3(.99,.78,.23)*tissue;
        else if (p.x < 37.5) target = vec3(.99,.96,.78)*tissue;
        else if (p.x < 38.5) target = mix(vec3(.22,.075,.22),vec3(.63,.30,.47),value);
        else if (p.x < 39.5) target = vec3(.98,.61,.66)*tissue;
        else
        {
            float sector=smoothstep(.22,.73,hnsNoise(q*9.0)+.22*fine);
            target=mix(vec3(.99,.95,.75),vec3(.83,.23,.29),sector)*tissue;
        }
        blend=.96;
    }
    else if (p.x < 41.5) // Cream Margins
    {
        float rim=hnsRim(sampleUv,2.1);
        float patches=smoothstep(.17,.60,n+.21*fine);
        target=vec3(.98,.94,.71)*lightness;
        blend=rim*mix(.32,1.0,patches);
    }
    else if (p.x < 42.5) // Lime Heart
    {
        float inner=1.0-hnsRim(sampleUv,2.5);
        float heart=smoothstep(.12,.76,inner*.73+n*.27);
        target=mix(vec3(.16,.36,.12),vec3(.82,.89,.28),heart)*lightness;
        blend=.30+.70*heart;
    }
    else if (p.x < 43.5) // Copper Tips
    {
        float tips=smoothstep(.43,.88,localUv.y+.28*(n-.5));
        target=mix(vec3(.38,.19,.10),vec3(.93,.57,.28),value);
        blend=tips*mix(.52,1.0,fine);
    }
    else if (p.x < 44.5) // Ivory Speckle
    {
        vec2 st=q*8.0, id=floor(st);
        vec2 delta=fract(st)-(.22+.56*hnsPoint(id));
        float radius=.12+.12*hnsHash(id+vec2(2,8));
        float spot=(1.0-smoothstep(radius-.04,radius+.05,length(delta)))*step(.24,hnsHash(id));
        target=vec3(.97,.93,.73)*lightness;
        blend=spot;
    }
    else if (p.x < 45.5) // Burgundy Margins
    {
        float rim=hnsRim(sampleUv,2.8);
        float patches=smoothstep(.14,.72,n+.18*fine);
        target=mix(vec3(.26,.055,.13),vec3(.69,.23,.37),value);
        blend=rim*mix(.55,1.0,patches);
    }
    else if (p.x < 48.5) // Lower-central stalk palette
    {
        affinity=hnsStalkAffinity(localUv,affinity,regions);
        if (p.x < 46.5) target=mix(vec3(.30,.075,.12),vec3(.77,.28,.30),value);
        else if (p.x < 47.5) target=vec3(.89,.61,.22)*lightness;
        else
        {
            float bands=smoothstep(-.22,.20,sin((localUv.y*7.0+n*.16)*6.283185));
            target=mix(rgb,vec3(.72,.20,.29)*lightness,bands);
        }
        blend=.98;
    }
    else if (p.x < 49.5) // Blue Jade
    {
        target=mix(vec3(.095,.29,.28),vec3(.41,.71,.61),smoothstep(.14,.85,value));
        blend=.88;
    }
    else if (p.x < 50.5) // Two-Tone Canopy
    {
        float young=smoothstep(.25,.77,localUv.y+.18*(n-.5));
        target=mix(vec3(.09,.30,.18),vec3(.77,.88,.28),young)*lightness;
        blend=mix(.65,.92,young);
    }
    else if (p.x < 51.5) // Bronze Flush: juvenile anthocyanin fades
    {
        float age=clamp(p.w,0.0,1.0);
        vec3 bronze=mix(vec3(.31,.14,.12),vec3(.88,.49,.24),value);
        vec3 mature=mix(vec3(.10,.28,.15),vec3(.40,.65,.29),value);
        target=mix(bronze,mature,age);
        blend=mix(.95,.68,age)*mix(.62,1.0,smoothstep(.18,.73,n));
    }
    else if (p.x < 52.5) // Frostbud: pale young growth, increasingly green
    {
        float age=clamp(p.w,0.0,1.0);
        float tips=smoothstep(.24,.76,localUv.y+.36*(n-.5));
        float margin=hnsRim(sampleUv,2.2);
        target=mix(vec3(.94,.93,.77)*lightness,vec3(.34,.57,.31)*lightness,age);
        blend=(1.0-age*.78)*max(tips,margin*.85);
    }
    else if (p.x < 53.5) // Autumn Crown: mature golden/russet flush
    {
        float age=clamp(p.w,0.0,1.0);
        float upper=smoothstep(.20,.74,localUv.y+.35*(n-.5));
        vec3 gold=mix(vec3(.53,.23,.10),vec3(.96,.75,.29),smoothstep(.22,.80,value+.14*fine));
        target=mix(vec3(.38,.67,.25)*lightness,gold,age);
        blend=mix(.28,.98,age)*upper;
    }
    else if (p.x < 54.5) // Ripening Blush: contrasting reproductive tissue
    {
        float age=clamp(p.w,0.0,1.0);
        affinity=hnsHarvestAffinity(src.rgb,regions);
        vec3 honey=vec3(.98,.89,.56)*lightness;
        vec3 blush=vec3(.97,.52,.43)*lightness;
        vec3 plum=mix(vec3(.31,.09,.19),vec3(.77,.31,.43),value);
        target=age<.5?mix(honey,blush,age*2.0):mix(blush,plum,(age-.5)*2.0);
        blend=.97;
    }
    else if (p.x < 55.5) // Maturing Variegation: increasing cream sectors
    {
        float age=clamp(p.w,0.0,1.0);
        float patch=smoothstep(.77-age*.46,.89-age*.31,n+.13*fine);
        target=mix(vec3(.62,.76,.44),vec3(.98,.94,.72),patch)*lightness;
        blend=patch*(.16+.84*age);
    }
    // Keep texture value structure even in the most striking cosmetic motifs.
    float targetV = max(target.r,max(target.g,target.b));
    float emphasis = smoothstep(.46,.88,p.z);
    float retainedValue = mix(value,targetV,mix(.52,.76,emphasis));
    if (targetV>.001) target *= retainedValue/targetV;
    // Strong alleles can express a vivid local accent without painting fruit.
    float amount = clamp(blend*p.z*shade*affinity*expression*mix(1.0,1.38,emphasis),0.0,.94);
    return clamp(mix(rgb,target,amount),0.0,1.0);
}

void main()
{
    vec2 uv = vUv * uMaterial._MainTex_ST.zw + uMaterial._MainTex_ST.xy;
    vec4 src = texture(_MainTex, uv);
    if (src.a < 0.35) discard;
    vec3 rgb = src.rgb;
    vec3 regions = vec3(0.0);
    if (uMaterial._HnsMaskEnabled < 0.5)
        rgb = compose(rgb, uMaterial._HnsTint.rgb, uMaterial._HnsControls, uMaterial._HnsDullness);
    else
    {
        regions = texture(_HnsMask, uv).rgb;
        if (regions.r > 0.5)
            rgb = compose(rgb, uMaterial._HnsTint.rgb, uMaterial._HnsControls, uMaterial._HnsDullness);
        else if (regions.g > 0.5)
            rgb = compose(rgb, uMaterial._HnsTint1.rgb, uMaterial._HnsControls1, uMaterial._HnsDullness1);
        else if (regions.b > 0.5)
            rgb = compose(rgb, uMaterial._HnsTint2.rgb, uMaterial._HnsControls2, uMaterial._HnsDullness2);
        // Unassigned pixels remain exactly as in the source.
    }
    outColor = vec4(hnsPattern(rgb, src, vUv, uv, regions), src.a);
}
