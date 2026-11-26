#version 450
layout(location=0) in vec3 inPosition; // @vertex Position required
layout(location=2) in vec4 inColor; // @vertex Color optional @default = vec4(1,1,1,1)
layout(location=3) in vec4 inUv0; // @vertex TexCoord0 optional @default = vec4(0,0,0,0)
layout(push_constant) uniform Push{mat4 modelMatrix;}push;
layout(location=0) out vec4 vColor;
layout(location=1) out vec2 vUv;
layout(location=2) flat out float vFallSeed;
layout(set=0,binding=0) uniform VertexUniforms{mat4 ViewProj;}uVertex;
layout(set=2,binding=0) uniform MaterialUniforms
{
    vec4 _Color;
    vec4 _MainTex_ST;
    float _SwayHead;
    vec4 _HnsExpression;
    vec4 _HnsExpressionEnv;
    vec4 _HnsPreview;
}uMaterial;
void main()
{
    vec3 p=inPosition;
    if(uMaterial._HnsPreview.x<.5)p+=vec3(sin(uMaterial._SwayHead*.15+p.x*4.),0,sin(uMaterial._SwayHead*.15+p.z*4.))*.1*inColor.a;
    gl_Position=uVertex.ViewProj*push.modelMatrix*vec4(p,1);
    vColor=inColor;vUv=inUv0.xy;vFallSeed=inUv0.z;
}
