#version 450
// Live, inverse UV silhouette deformation of original unatlased plant art.
// One source sample; no new art, masks, or expanded mesh. Vanilla alpha cutoff.
layout(location = 1) in vec2 vUv;
layout(location = 0) out vec4 outColor;
layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _Color; // @color @default = vec4(1.0, 1.0, 1.0, 1.0)
    vec4 _MainTex_ST; // @default = vec4(0.0, 0.0, 1.0, 1.0)
    float _SwayHead; // @default = 0.0
    vec4 _HnsPose; // @default = vec4(1.0, 0.0, 0.0, 0.0)
} uMaterial;
layout(set = 2, binding = 1) uniform sampler2D _MainTex;
void main()
{
    vec4 p=uMaterial._HnsPose;
    vec2 uv;
    if(p.w>0.0)
    {
        uv.x=.5+(vUv.x-.5)/(1.0-p.y);
        if(uv.x<0.0||uv.x>1.0)discard;
        float side=2.0*uv.x-1.0;
        uv.y=vUv.y/(p.x*(1.0-p.w*side*side));
    }
    else
    {
        uv.y=vUv.y/p.x;
        if(uv.y<0.0||uv.y>1.0)discard;
        uv.x=.5+(vUv.x-.5-p.z*uv.y*uv.y)/(1.0-p.y*uv.y);
    }
    if(any(lessThan(uv,vec2(0)))||any(greaterThan(uv,vec2(1))))discard;
    vec4 src=texture(_MainTex,uv*uMaterial._MainTex_ST.zw+uMaterial._MainTex_ST.xy);
    if(src.a<.35)discard;
    outColor=src;
}
