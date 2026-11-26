#version 450
layout(location = 1) in vec2 vUv;
layout(location = 2) flat in float vFallSeed;
layout(location = 0) out vec4 outColor;
layout(set = 2,binding = 0) uniform MaterialUniforms
{
    vec4 _Color; // @color @default = vec4(1.0, 1.0, 1.0, 1.0)
    vec4 _MainTex_ST; // @default = vec4(0.0, 0.0, 1.0, 1.0)
    float _SwayHead; // @default = 0.0
    vec4 _HnsTissue; // @default = vec4(1.0, 1.0, 0.0, 0.0)
} uMaterial;
layout(set=2,binding=1) uniform sampler2D _MainTex;
layout(set=2,binding=2) uniform sampler2D _HnsAnatomy;
void main()
{
    vec2 uv=vUv*uMaterial._MainTex_ST.zw+uMaterial._MainTex_ST.xy;
    vec4 src=texture(_MainTex,uv);
    if(src.a<0.35)discard;
    vec4 a=texture(_HnsAnatomy,uv);
    // Additional live color guard protects blended flower/fruit boundaries.
    float green=smoothstep(.0,.065,src.g-src.r)*smoothstep(.015,.085,src.g-src.b);
    if(a.a<.5||green<.01){outColor=src;return;}
    vec4 controls=uMaterial._HnsTissue;
    int mode=int(controls.x+.5);
    float vein=a.r,thin=a.g,sector=a.b;
    float light=controls.y,wet=controls.z;
    float lum=max(src.g,max(src.r,src.b));
    float tone=clamp(lum*1.30+.09,0.0,1.0);
    float specimen=fract(sin(vFallSeed*12.9898+sector*29.17)*43758.5453);
    vec3 c=src.rgb;
    if(mode==1)
    {
        // Ivory internal vascular paths, creamy halos along larger veins.
        float amount=smoothstep(.16,.78,vein)*(.62+.14*sector);
        c=mix(c,vec3(.94,.96,.71)*tone,amount);
    }
    else if(mode==2)
    {
        // Anthocyanin deposited preferentially in vascular bands and
        // selected blade sectors rather than across the complete plant.
        float amount=clamp(.72*vein+.30*smoothstep(.43,.88,sector),0.,.9);
        c=mix(c,vec3(.82,.27,.39)*tone,amount);
    }
    else if(mode==3)
    {
        // Thin laminae brighten as sky light rises; thicker veins retain
        // their dark shading. This is simulated transmission, not transparency.
        vec3 backlit=vec3(.80,1.0,.31)*tone;
        c=mix(c,backlit,light*(.22+.78*thin)*(1.0-.76*vein)*(.72+.22*sector));
        c=mix(c,c*vec3(.71,.91,.75),vein*.22);
    }
    else if(mode==4)
    {
        // Independent vascular territories form a botanical chimera.
        // Same cultivar, different individuals and foliage regions.
        float chosen=smoothstep(.36,.64,specimen);
        vec3 variegated=mix(vec3(.95,.91,.61),vec3(.40,.81,.47),smoothstep(.18,.76,sector))*tone;
        c=mix(c,variegated,chosen*(.56+.29*thin)*(1.0-.32*vein));
    }
    else if(mode==5)
    {
        // Moisture and illumination accent bronze capillaries, preserving
        // the original darkest shading and surrounding leaf green.
        float amount=clamp(vein*(.62+.22*wet)+.25*thin*sector,0.,.86);
        c=mix(c,vec3(1.0,.67,.28)*tone,amount);
    }
    outColor=vec4(mix(src.rgb,clamp(c,0.,1.),green),src.a);
}
