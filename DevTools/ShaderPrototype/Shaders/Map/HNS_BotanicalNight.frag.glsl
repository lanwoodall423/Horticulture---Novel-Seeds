#version 450
// Additive post-light visual only. The glow grid and texture both remain inside
// the original plant silhouette; no light-grid or gameplay changes.
layout(location = 1) in vec2 vUv;
layout(location = 0) out vec4 outColor;
layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _Color; // @color @default = vec4(1.0, 1.0, 1.0, 1.0)
    vec4 _MainTex_ST; // @default = vec4(0.0, 0.0, 1.0, 1.0)
    float _SwayHead; // @default = 0.0
    float _HnsDarkness; // @default = 0.0
} uMaterial;
layout(set = 2, binding = 1) uniform sampler2D _MainTex;
float hash(vec2 p){return fract(sin(dot(p,vec2(127.1,311.7)))*43758.5453);}
void main()
{
    if(uMaterial._HnsDarkness<.001)discard;
    vec2 uv=vUv*uMaterial._MainTex_ST.zw+uMaterial._MainTex_ST.xy;
    vec4 s=texture(_MainTex,uv);if(s.a<.35)discard;
    float hi=max(s.r,max(s.g,s.b)),lo=min(s.r,min(s.g,s.b));
    float warm=smoothstep(-.02,.12,s.r-s.g)*smoothstep(.07,.22,s.r-s.b)*smoothstep(.2,.55,hi);
    float pale=(1.0-smoothstep(.05,.25,(hi-lo)/max(hi,.001)))*smoothstep(.4,.72,hi);
    float buds=max(warm,pale);
    vec2 grid=vUv*6.5,id=floor(grid),p=fract(grid)-vec2(.25+.5*hash(id),.25+.5*hash(id+vec2(7,1)));
    float dotGlow=(1.0-smoothstep(.055,.24,length(p)))*step(.32,hash(id+vec2(2,9)));
    float leaf=smoothstep(.015,.09,s.g-max(s.r,s.b))*smoothstep(.16,.45,hi);
    float glow=max(buds*.63,dotGlow*leaf*.40)*uMaterial._HnsDarkness;
    if(glow<.005)discard;
    vec3 col=mix(vec3(.33,.92,.64),vec3(.90,.75,.33),buds);
    outColor=vec4(col*glow,glow);
}
