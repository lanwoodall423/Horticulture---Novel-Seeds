#version 450
layout(location=0) in vec3 inPosition; // @vertex Position required
layout(location=2) in vec4 inColor; // @vertex Color optional @default = vec4(1.0,1.0,1.0,1.0)
layout(location=3) in vec4 inUv0; // @vertex TexCoord0 optional @default = vec4(0.0,0.0,0.0,0.0)
layout(push_constant) uniform Push {mat4 modelMatrix;} push;
layout(location=0) out vec4 vColor;
layout(location=1) out vec2 vUv;
layout(location=2) flat out float vFallSeed;
layout(set=0,binding=0) uniform VertexUniforms{mat4 ViewProj;} uVertex;
layout(set=2,binding=0) uniform MaterialUniforms
{
    vec4 _Color;
    vec4 _MainTex_ST;
    float _SwayHead;
    vec4 _HnsLife;
    vec4 _HnsLifeEnv;
} uMaterial;
void main()
{
    int mode=int(uMaterial._HnsLife.x+.5),role=int(uMaterial._HnsLife.y+.5);
    float head=uMaterial._SwayHead;
    vec3 wind=vec3(sin(head*.15+inPosition.x*4.0),0,sin(head*.15+inPosition.z*4.0))*.1;
    if(role==1&&(mode==8||mode==18))wind=vec3(0);
    gl_Position=uVertex.ViewProj*push.modelMatrix*vec4(inPosition+wind*inColor.a,1);
    // Small camera-relative displacement on the separated rear foliage plane.
    // Orthographic screen position is used as a parallax proxy.
    if(role==2)gl_Position.xy+=clamp(gl_Position.xy/gl_Position.w,vec2(-1),vec2(1))*.016*gl_Position.w*inUv0.y;
    vColor=inColor;vUv=inUv0.xy;vFallSeed=inUv0.z;
}
