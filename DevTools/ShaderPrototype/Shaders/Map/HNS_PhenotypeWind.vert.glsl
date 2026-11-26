#version 450
// Genetic wind response. Uses the engine phase and original anchored exposure.
layout(location = 0) in vec3 inPosition; // @vertex Position required
layout(location = 2) in vec4 inColor; // @vertex Color optional @default = vec4(1.0, 1.0, 1.0, 1.0)
layout(location = 3) in vec4 inUv0; // @vertex TexCoord0 optional @default = vec4(0.0, 0.0, 0.0, 0.0)
layout(push_constant) uniform Push { mat4 modelMatrix; } push;
layout(location = 1) out vec2 vUv;
layout(set = 0, binding = 0) uniform VertexUniforms { mat4 ViewProj; } uVertex;
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

void main()
{
    vec4 w=uMaterial._HnsWind;
    float phase=uMaterial._SwayHead*.15*w.z;
    float exposure=clamp(inColor.a*2.0,0.0,1.0);
    float seed=inUv0.z*.013;
    vec2 pos=inPosition.xz;
    vec2 bend=sin(vec2(phase)+pos*4.0)*w.xy;
    vec2 flutter=sin(vec2(phase*3.7+seed,phase*4.3+seed*1.3)+pos*7.0)*w.w;
    vec2 displacement=.13*(bend*exposure+flutter*exposure*exposure);
    vec3 warped=inPosition+vec3(displacement.x,0.0,displacement.y);
    gl_Position=uVertex.ViewProj*push.modelMatrix*vec4(warped,1.0);
    vUv=inUv0.xy;
}
