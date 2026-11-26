#version 450
// Nine prewarmed organ-local poses. Nearest frame selection avoids blending
// two displaced cutout silhouettes. Half-texel clamp prevents atlas bleed.
layout(location = 1) in vec2 vUv;
layout(location = 0) out vec4 outColor;
layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _Color; // @color @default = vec4(1.0, 1.0, 1.0, 1.0)
    vec4 _MainTex_ST; // @default = vec4(0.0, 0.0, 1.0, 1.0)
    float _SwayHead; // @default = 0.0
    vec4 _HnsPose; // @default = vec4(0.5, 0.0, 0.0, 0.0)
    vec4 _HnsAtlas; // @default = vec4(0.01, 0.01, 0.0, 0.0)
} uMaterial;
layout(set = 2, binding = 1) uniform sampler2D _MainTex;
void main()
{
    float phase=uMaterial._HnsPose.x;
    if(uMaterial._HnsPose.y>0.5)phase=0.5+0.5*sin(uMaterial._SwayHead*0.65);
    float frame=floor(clamp(phase,0.0,1.0)*8.0+0.5);
    vec2 pad=uMaterial._HnsAtlas.xy*0.5;
    vec2 uv=clamp(vUv,pad,vec2(1.0)-pad);
    // Waylith samples Texture2D rows in reverse upload order; verified by GPU swatches.
    vec2 cell=vec2(mod(frame,3.0),2.0-floor(frame/3.0));
    vec4 src=texture(_MainTex,(cell+uv)/3.0);
    if(src.a<0.35)discard;
    outColor=src;
}
