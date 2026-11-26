#version 450
layout(location=0) in vec4 vColor;
layout(location=1) in vec2 vUv;
layout(location=2) flat in float vFallSeed;
layout(location=0) out vec4 outColor;
layout(set=2,binding=0) uniform MaterialUniforms
{
    vec4 _Color; // @color @default = vec4(1,1,1,1)
    vec4 _MainTex_ST; // @default = vec4(0,0,1,1)
    float _SwayHead; // @default = 0.0
    vec4 _HnsExpression; // @default = vec4(0,0,0,0)
    vec4 _HnsExpressionEnv; // @default = vec4(0,1,1,0)
    vec4 _HnsPreview; // @default = vec4(0,0,0,0)
    vec4 _HnsFunctional; // @default = vec4(0,0,0,0)
} uMaterial;
layout(set=2,binding=1) uniform sampler2D _MainTex;
layout(set=2,binding=2) uniform sampler2D _HnsAnatomy;
layout(set=2,binding=3) uniform sampler2D _HnsArchitecture;
float sat(float x){return clamp(x,0.,1.);}
float hash(float x){return fract(sin(x*12.9898+7.31)*43758.5453);}
bool outside(vec2 p){return any(lessThan(p,vec2(0)))||any(greaterThan(p,vec2(1)));}
void main()
{
    vec4 plan=uMaterial._HnsExpression,env=uMaterial._HnsExpressionEnv;
    vec3 state=uMaterial._HnsPreview.x>.5?uMaterial._HnsPreview.yzw:vColor.rgb;
    float growth=state.r,seed=floor(state.g*7.+.5),bias=state.b*2.-1.;
    vec2 uv=vUv;
    // Bounded per-individual tip inclination. Root is stationary.
    float y=sat((1.-uv.y-.16)/.84);
    float lean=(hash(seed+1.)-.5)*.075+bias*.12;
    uv.x-=lean*y*y;
    if(outside(uv))discard;
    vec4 src;vec2 base=uv;
    if(plan.x>.5)
    {
        float f=floor(growth*8.+.5);
        vec2 local=clamp(vec2(uv.x,1.-uv.y),1.5/vec2(textureSize(_HnsArchitecture,0)),1.-1.5/vec2(textureSize(_HnsArchitecture,0)));
        src=texture(_HnsArchitecture,(vec2(mod(f,3.),2.-floor(f/3.))+local)/3.);
        base=(uv-.5)*1.5+.5;
    }
    // Waylith native source sampling must share the vertical convention
    // of the CPU-read anatomy and architecture uploads. Keep map-space uv
    // for root/tip calculations; invert only at texture lookup.
    else src=texture(_MainTex,vec2(uv.x,1.-uv.y));
    if(src.a<.35)discard;
    vec4 a=outside(base)?vec4(0):texture(_HnsAnatomy,vec2(base.x,1.-base.y));
    float green=smoothstep(.0,.065,src.g-src.r)*smoothstep(.015,.085,src.g-src.b);
    float leaf=step(.75,a.a)*green;
    float organ=step(.32,a.a)*(1.-step(.75,a.a))*(1.-green);
    float territory=a.b,vein=a.r,thin=a.g;
    float individual=hash(floor(territory*255.+.5)+seed*37.);
    float tone=clamp(max(src.r,max(src.g,src.b))*1.25+.08,0.,1.);
    vec3 c=src.rgb;
    if(plan.y>.5)
    {
        float strength=leaf*(.45+.55*smoothstep(.05,.8,growth))*(.68+.32*individual);
        if(plan.y<1.5)c=mix(c,vec3(.95,.96,.72)*tone,strength*smoothstep(.14,.78,vein)*.86);
        else if(plan.y<2.5)c=mix(c,vec3(.82,.25,.40)*tone,strength*(.75*vein+.18*individual));
        else if(plan.y<3.5)c=mix(c,vec3(1.,.68,.30)*tone,strength*(.75*vein+.21*thin*individual));
        else c=mix(c,vec3(.93,.91,.65)*tone,strength*smoothstep(.35,.78,individual)*(.70+.30*thin));
    }
    // Each reproductive component has a rank in the art-derived map.
    // One stable group remains native. Growth shifts other groups separately.
    if(plan.z>.5&&organ>0.)
    {
        float group=hash(floor(territory*255.+.5)+seed*17.);
        float selected=smoothstep(.29,.55,group);
        vec3 target=plan.z<1.5?mix(vec3(.48,.83,.27),vec3(.98,.61,.22),smoothstep(group*.62+.15,group*.62+.42,growth)):vec3(.98,.39,.58);
        c=mix(c,target*tone,organ*selected*.73);
    }
    if(plan.w>.5&&leaf>0.)
    {
        float facing=sat(.5+.5*(env.x*(base.x-.5)*2.+env.y*(.5-base.y)*2.));
        float sheen=pow(facing,3.)*(.25+.75*thin)*env.z;
        if(plan.w<1.5)c=mix(c,c+vec3(.15,.18,.10),leaf*sheen*.72);
        else if(plan.w<2.5)c=mix(c,vec3(.68,.82,.85)*tone,leaf*(.32+.30*thin));
        else if(plan.w<3.5)c=mix(c,vec3(.75,.90,1.)*tone,leaf*sheen*.85);
        else
        {
            vec2 q=base*14.,g=fract(q)-.5;
            float bead=1.-smoothstep(.08,.19,length(g));
            c=mix(c,vec3(.89,.98,1.),leaf*bead*env.w*.82);
        }
    }
    // Functional visual cues are read-only consequences of existing traits
    // and saved resource/harvest states. Alpha and unrelated tissue intact.
    int flags=int(uMaterial._HnsFunctional.x+.5);
    float edge=smoothstep(.16,.70,thin);
    if((flags&2)!=0)c=mix(c,vec3(.55,.76,.83)*tone,leaf*(.18+.62*edge)*(.65+.35*individual));
    if((flags&4)!=0)c=mix(c,vec3(.90,.81,.38)*tone,leaf*(.14+.61*edge)*(.70+.30*individual));
    if((flags&16)!=0&&uMaterial._HnsFunctional.z>.5)
    {
        float lower=smoothstep(.46,.88,base.y);
        c=mix(c,vec3(.83,.55,.25)*tone,leaf*lower*.68);
        c=mix(c,vec3(.57,.88,.31)*tone,leaf*(1.-lower)*(.42+.22*edge)*(1.-.50*growth));
    }
    if((flags&8)!=0&&uMaterial._HnsFunctional.y>.5)
        c=mix(c,vec3(.85,.64,.25)*tone,leaf*(.25+.65*edge)*(.72+.28*individual));
    outColor=vec4(clamp(c,0.,1.),src.a);
}
