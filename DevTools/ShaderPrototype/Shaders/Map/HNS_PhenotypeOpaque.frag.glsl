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
    vec4 _HnsPattern; // @default = vec4(0.0, 0.0, 0.0, 1.0)
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

// All pattern coordinates are local sprite UVs, not guessed anatomy.
// Use large, legible patches and preserve the source's value/outline detail.
float hnsHash(vec2 p)
{
    vec3 q = fract(vec3(p.xyx) * 0.1031);
    q += dot(q, q.yzx + 33.33);
    return fract((q.x + q.y) * q.z);
}
float hnsNoise(vec2 p)
{
    vec2 i = floor(p), f = fract(p);
    f = f*f*(3.0-2.0*f);
    return mix(mix(hnsHash(i),hnsHash(i+vec2(1,0)),f.x),
               mix(hnsHash(i+vec2(0,1)),hnsHash(i+vec2(1,1)),f.x),f.y);
}
vec3 hnsPattern(vec3 rgb, vec4 src, vec2 localUv, vec2 sampleUv)
{
    vec4 p = uMaterial._HnsPattern;
    if (p.x < 0.5 || p.z <= 0.0) return rgb;
    float seed = p.y*37.0;
    vec2 q = localUv + vec2(seed,seed*.731);
    float n = hnsNoise(q*4.0);
    float fine = hnsNoise(q*13.0);
    float value = max(src.r,max(src.g,src.b));
    float shade = smoothstep(0.09,0.32,value);
    // Keep a little of the original hue and all of the original luminance
    // structure rather than painting flat opaque bands over a sprite.
    float lightness = clamp(.28+.93*value,0.0,1.0);
    vec3 target = rgb;
    float blend = 0.0;
    if (p.x < 1.5)
    {
        float ribbon = sin((localUv.x*2.65 + localUv.y*.72 + n*.93 + p.y)*6.283185);
        float sector = smoothstep(.02,.32,ribbon + .72*(fine-.5));
        target = vec3(1.0,.91,.63)*lightness;
        blend = sector*.92;
    }
    else if (p.x < 2.5)
    {
        float blush = smoothstep(.13,.83,localUv.y+.38*(n-.5));
        target = mix(vec3(.34,.075,.28),vec3(.96,.16,.36),smoothstep(.22,.91,value));
        blend = .26+.74*blush;
    }
    else if (p.x < 3.5)
    {
        vec2 stepUv = 1.5 / vec2(textureSize(_MainTex,0));
        float rim = 1.0 - min(min(texture(_MainTex,sampleUv+vec2(stepUv.x,0)).a,texture(_MainTex,sampleUv-vec2(stepUv.x,0)).a),
                            min(texture(_MainTex,sampleUv+vec2(0,stepUv.y)).a,texture(_MainTex,sampleUv-vec2(0,stepUv.y)).a));
        target = mix(vec3(.11,.26,.33),vec3(.81,.92,.94),clamp(.79*value+.24*rim+.08*fine,0.0,1.0));
        blend = .75+.20*rim;
    }
    else if (p.x < 4.5)
    {
        vec2 cells = q*8.5;
        vec2 id = floor(cells);
        vec2 offset = vec2(hnsHash(id+vec2(7,3)),hnsHash(id+vec2(2,13)))*.50+.25;
        float radius = .18+.12*hnsHash(id+vec2(12,1));
        float spot = (1.0-smoothstep(radius-.045,radius+.055,length(fract(cells)-offset))) * step(.25,hnsHash(id));
        float mottle = smoothstep(.59,.87,n);
        target = mix(vec3(.64,.42,.08),vec3(1.0,.86,.30),lightness);
        blend = max(spot*.98,mottle*.42);
    }
    else if (p.x < 5.5)
    {
        // Stylized branching light markings, not actual anatomy or light emission.
        float curve = .045*sin(localUv.y*12.0+seed)+.035*(n-.5);
        float x = localUv.x-.5+curve;
        float mid = 1.0-smoothstep(.018,.040,abs(x));
        float branchWave = abs(fract(localUv.y*4.4-abs(x)*2.1+.10*fine)-.5);
        float branch = (1.0-smoothstep(.065,.13,branchWave))*smoothstep(.02,.15,abs(x));
        float veins = clamp(max(mid,branch*.86),0.0,1.0);
        vec3 jade = vec3(.035,.25,.25)*(.55+value);
        vec3 cyan = mix(vec3(.08,.73,.59),vec3(.60,1.0,.92),lightness);
        target = mix(jade,cyan,veins);
        blend = .77+.23*veins;
    }
    else if (p.x < 6.5)
    {
        float tip = smoothstep(.3,.93,localUv.y + .28*(n-.5));
        target = mix(vec3(.55,.34,.09),vec3(.95,.77,.34),lightness);
        blend = .20 + .80*tip;
    }
    else if (p.x < 7.5)
    {
        float lesion = smoothstep(.51,.77,hnsNoise(q*7.0)+.16*fine);
        target = mix(vec3(.20,.12,.16),vec3(.65,.38,.24),value);
        blend = .14+.86*lesion;
    }
    else if (p.x < 8.5)
    {
        target = mix(vec3(.08,.24,.12),vec3(.48,.79,.39),lightness);
        blend = .66 + .34*n;
    }
    else if (p.x < 9.5)
    {
        vec2 cells = q*11.0;
        vec2 id = floor(cells);
        vec2 center = vec2(hnsHash(id+vec2(2,7)),hnsHash(id+vec2(9,1)))*.5+.25;
        float dotMask = (1.0-smoothstep(.13,.24,length(fract(cells)-center))) * step(.32,hnsHash(id));
        target = vec3(.88,.81,.62)*lightness;
        blend = .11+.89*dotMask;
    }
    else if (p.x < 10.5)
    {
        float ripple = sin((localUv.y*4.0+localUv.x*.65+n*.50)*6.283185);
        target = mix(vec3(.08,.37,.36),vec3(.52,.91,.84),lightness);
        blend = .28+.72*smoothstep(-.1,.75,ripple);
    }
    else if (p.x < 14.5)
    {
        vec2 cells = q*7.5;
        vec2 id = floor(cells);
        vec2 center = vec2(hnsHash(id+vec2(3,7)),hnsHash(id+vec2(9,2)))*.45+.28;
        vec2 delta = fract(cells)-center;
        float bead = (1.0-smoothstep(.18,.29,length(delta))) * step(.32,hnsHash(id));
        float gleam = (1.0-smoothstep(.035,.11,length(delta+vec2(.06,.07))))*bead;
        vec3 resin = p.x < 11.5 ? vec3(.95,.66,.15) :
                     p.x < 12.5 ? vec3(.43,.20,.63) :
                     p.x < 13.5 ? vec3(.94,.30,.08) : vec3(.58,.95,.16);
        target = mix(resin*(.48+.52*value),vec3(1.0,.94,.75),gleam*.85);
        blend = .18+.82*bead;
    }
    else
    {
        vec2 stepUv = 1.5 / vec2(textureSize(_MainTex,0));
        float edge = 1.0-min(min(texture(_MainTex,sampleUv+vec2(stepUv.x,0)).a,texture(_MainTex,sampleUv-vec2(stepUv.x,0)).a),
                              min(texture(_MainTex,sampleUv+vec2(0,stepUv.y)).a,texture(_MainTex,sampleUv-vec2(0,stepUv.y)).a));
        float serration = step(.15,sin(localUv.y*63.0+localUv.x*19.0));
        target = mix(vec3(.16,.10,.06),vec3(.66,.49,.26),value);
        blend = .12+.88*edge*serration;
    }
    return clamp(mix(rgb,target,clamp(blend*p.z*shade,0.0,1.0)),0.0,1.0);
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
    outColor = vec4(hnsPattern(rgb, src, vUv, uv), src.a);
}
