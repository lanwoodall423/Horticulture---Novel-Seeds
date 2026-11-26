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

void main()
{
    vec2 uv = vUv * uMaterial._MainTex_ST.zw + uMaterial._MainTex_ST.xy;
    vec4 src = texture(_MainTex, uv);
    if (src.a < 0.35) discard;
    vec3 rgb = src.rgb;
    if (uMaterial._HnsMaskEnabled < 0.5)
        rgb = compose(rgb, uMaterial._HnsTint.rgb, uMaterial._HnsControls, uMaterial._HnsDullness);
    else
    {
        vec3 mask = texture(_HnsMask, uv).rgb;
        if (mask.r > 0.5)
            rgb = compose(rgb, uMaterial._HnsTint.rgb, uMaterial._HnsControls, uMaterial._HnsDullness);
        else if (mask.g > 0.5)
            rgb = compose(rgb, uMaterial._HnsTint1.rgb, uMaterial._HnsControls1, uMaterial._HnsDullness1);
        else if (mask.b > 0.5)
            rgb = compose(rgb, uMaterial._HnsTint2.rgb, uMaterial._HnsControls2, uMaterial._HnsDullness2);
        // Unassigned pixels remain exactly as in the source.
    }
    outColor = vec4(rgb, src.a);
}
