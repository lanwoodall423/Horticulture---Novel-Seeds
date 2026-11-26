#version 450
// Opaque, nonseasonal whole-plant prototype. Existing CPU path owns masked,
// translucent and seasonal sources. The vertex stage is the installed Core program.
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
} uMaterial;
layout(set = 2, binding = 1) uniform sampler2D _MainTex;

// Match PlantVisualColorUtility's HSV and outline-strength semantics.
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

void main()
{
    vec4 src = texture(_MainTex, vUv * uMaterial._MainTex_ST.zw + uMaterial._MainTex_ST.xy);
    if (src.a < 0.35) discard;
    vec3 hsv = toHsv(src.rgb);
    vec4 controls = uMaterial._HnsControls;
    vec3 tint = uMaterial._HnsTint.rgb;
    float h = fract(hsv.x + controls.x);
    float s = clamp(hsv.y * controls.y, 0.0, 1.0);
    float tintMax = max(tint.r, max(tint.g, tint.b));
    float tintMin = min(tint.r, min(tint.g, tint.b));
    float chroma = clamp(tintMax - tintMin, 0.0, 1.0);
    if (chroma > 0.001)
    {
        vec3 th = toHsv(clamp(tint, 0.0, 1.0));
        float delta = fract(th.x - h + 0.5) - 0.5;
        h = fract(h + delta * chroma);
        s = mix(s, max(s, th.y), chroma);
    }
    s *= mix(1.0, 0.35, clamp(uMaterial._HnsDullness, 0.0, 1.0));
    float v = clamp(((hsv.z - 0.5) * controls.w + 0.5) * controls.z * max(0.0, tintMax), 0.0, 1.0);
    float outline = mix(0.12, 1.0, smoothstep(0.06, 0.34, hsv.z));
    outColor = vec4(mix(src.rgb, fromHsv(vec3(h, clamp(s, 0.0, 1.0), v)), outline), src.a);
}
