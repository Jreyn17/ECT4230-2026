// Toon Tropical - shared animation and water helpers for the Toon and Outline shaders.
// Driven by globals that the TT_Environment component sets every frame. Without that
// component every global is 0, so the pack renders static (and still looks right).
//
// Vertex colour mask baked into the meshes (Blender build):
//   R = wind sway weight (0 at the ground -> 1 at the tips)
//   G = flame: wobbles, flickers, unlit
//   B = glow (lantern glass): pulses, unlit
#ifndef TOONTROPICAL_COMMON_INCLUDED
#define TOONTROPICAL_COMMON_INCLUDED

float  _TT_Time;                 // seconds
float4 _TT_Wind;                 // xyz = wind direction * strength (metres), w = frequency
float4 _TT_WindParams;           // x = flutter, y = gust amount
float4 _TT_Push[4];              // foliage pushers (TT Foliage Pusher): xyz = feet, w = radius (0 = off)
float  _TT_PaletteOverride;      // 1 = sample _TT_PaletteGlobal instead of the material map
float  _TT_AdditionalLightScale; // time-of-day scale for point/spot lights (fires at night)
float  _TT_TintReady;            // 1 = _TT_PaletteBase is set (textured-mode theme tint)
#ifdef TT_BUILTIN                // Built-in Render Pipeline SubShaders (UnityCG.cginc)
sampler2D _TT_PaletteGlobal;
sampler2D _TT_PaletteBase;       // Tropical day palette: the reference the tint is measured from
#define TT_SAMPLE_PAL(tex, uv) tex2Dlod(tex, float4(uv, 0, 0)).rgb
#else                            // URP SubShaders (Core.hlsl)
TEXTURE2D(_TT_PaletteGlobal); SAMPLER(sampler_TT_PaletteGlobal);
TEXTURE2D(_TT_PaletteBase);
#define TT_SAMPLE_PAL(tex, uv) SAMPLE_TEXTURE2D_LOD(tex, sampler_TT_PaletteGlobal, uv, 0).rgb
#endif

// Textured mode (models from other packs, see the Toonify window): their own texture can't
// be palette-swapped, so the theme and time of day reach them as one overall tint, measured
// from four palette swatches (sand, wood, leaves, rock): current palette / Tropical day.
half3 TT_ThemeTint()
{
    if (_TT_PaletteOverride < 0.5 || _TT_TintReady < 0.5) return half3(1, 1, 1);
    const float2 uv0 = float2(0.0625, 0.9375), uv1 = float2(0.1875, 0.8125);
    const float2 uv2 = float2(0.6875, 0.9375), uv3 = float2(0.8125, 0.8125);
    half3 t = TT_SAMPLE_PAL(_TT_PaletteGlobal, uv0) / max(TT_SAMPLE_PAL(_TT_PaletteBase, uv0), 0.03)
            + TT_SAMPLE_PAL(_TT_PaletteGlobal, uv1) / max(TT_SAMPLE_PAL(_TT_PaletteBase, uv1), 0.03)
            + TT_SAMPLE_PAL(_TT_PaletteGlobal, uv2) / max(TT_SAMPLE_PAL(_TT_PaletteBase, uv2), 0.03)
            + TT_SAMPLE_PAL(_TT_PaletteGlobal, uv3) / max(TT_SAMPLE_PAL(_TT_PaletteBase, uv3), 0.03);
    return clamp(t * 0.25, 0.0, 2.0);
}

// Vertex colour mask: textured mode ignores it (other packs' vertex colours mean something
// else, and white would read as "flame + glow + sway").
#ifdef _TT_TEXTURED
    #define TT_MASK(c) float4(0, 0, 0, 1)     // alpha 1 = no cloth flutter
#else
    #define TT_MASK(c) (c)
#endif

float TT_Hash(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}

float TT_Noise(float2 p)
{
    float2 i = floor(p), f = frac(p);
    float a = TT_Hash(i), b = TT_Hash(i + float2(1, 0));
    float c = TT_Hash(i + float2(0, 1)), d = TT_Hash(i + float2(1, 1));
    float2 u = f * f * (3.0 - 2.0 * f);
    return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
}

float3 TT_ObjectPositionWS()
{
    return float3(UNITY_MATRIX_M._m03, UNITY_MATRIX_M._m13, UNITY_MATRIX_M._m23);
}

// Wind sway (mask.r) and flame wobble (mask.g), applied in world space so the outline
// hull, shadows and depth all move together with the visible mesh.
float3 TT_Animate(float3 posWS, float4 mask)
{
    float t = _TT_Time;
    float3 obj = TT_ObjectPositionWS();
    if (mask.r > 0.001)
    {
        float phase = dot(obj.xz, float2(0.37, 0.29)) + dot(posWS.xz, float2(0.06, 0.05));
        float gust = 1.0 + _TT_WindParams.y * (TT_Noise(float2(t * 0.23, obj.x * 0.1 + obj.z * 0.07)) - 0.5) * 2.0;
        float sway = (sin(t * _TT_Wind.w + phase) * 0.65 + 0.35) * gust;
        float3 dir = _TT_Wind.xyz;
        float3 perp = float3(-dir.z, 0.0, dir.x);
        float flutter = sin(t * _TT_Wind.w * 3.3 + phase * 4.1 + posWS.y * 2.0) * _TT_WindParams.x;
        posWS += (dir * sway + perp * flutter) * mask.r;
        posWS.y -= length(dir) * abs(sway) * mask.r * mask.r * 0.15;   // bend, don't stretch

        // characters walking through: swaying parts bend away from each pusher, tips most
        [unroll] for (int i = 0; i < 4; i++)
        {
            float4 p = _TT_Push[i];
            if (p.w <= 0.0) continue;
            float2 d = posWS.xz - p.xz;
            float dist = length(d);
            float f = saturate(1.0 - dist / p.w);
            f = f * f * (3.0 - 2.0 * f);                                  // smooth falloff
            f *= saturate(1.0 - (posWS.y - p.y - 1.6) / 0.6);             // not far overhead
            posWS.xz += d / max(dist, 1e-3) * f * p.w * 0.5 * mask.r;
            posWS.y -= f * p.w * 0.25 * mask.r * mask.r;                  // flatten as it bends
        }
    }
    // cloth (vertex alpha < 1: flags, pennants, sails): a ripple travelling from the pole to
    // the free edge, across the cloth's plane (the model's local Y in Blender = object Z here)
    float cloth = 1.0 - mask.a;
    if (cloth > 0.001 && t != 0.0)
    {
        float3 n = normalize(float3(UNITY_MATRIX_M._m02, UNITY_MATRIX_M._m12, UNITY_MATRIX_M._m22));
        float wind = saturate(length(_TT_Wind.xyz) / 0.12);
        float seed = dot(obj, float3(1.3, 0.0, 2.1));
        float w = sin(t * 7.5 - cloth * 7.0 + seed) + 0.35 * sin(t * 12.7 - cloth * 11.0 + seed * 1.7);
        posWS += n * w * cloth * 0.075 * (0.35 + 0.65 * wind);
        posWS.y += sin(t * 5.3 - cloth * 5.0 + seed) * cloth * 0.015 * wind;
    }
    if (mask.g > 0.5 && t != 0.0)
    {
        float s = dot(obj, float3(1.7, 3.1, 2.3));
        posWS.x += sin(t * 9.0 + posWS.y * 14.0 + s) * 0.012;
        posWS.z += cos(t * 7.3 + posWS.y * 11.0 + s) * 0.012;
        posWS.y += sin(t * 11.0 + s) * 0.01;
    }
    return posWS;
}

// Brightness flicker for flames (mask.g) and pulse for glow (mask.b).
half TT_Flicker(float4 mask)
{
    float t = _TT_Time;
    float s = dot(TT_ObjectPositionWS(), float3(1.7, 3.1, 2.3));
    half f = 0.12 * sin(t * 13.0 + s) + 0.08 * sin(t * 21.7 + s * 1.7);
    half p = 0.06 * sin(t * 2.4 + s);
    return 1.08 + (mask.g > 0.5 ? f : p);
}

// Palette swatches (8x8 grid): water = column 2, foam = column 1, both in the top row.
static const float2 TT_FOAM_UV = float2(0.1875, 0.9375);
#define TT_IS_WATER(cell) (cell.x == 2.0 && cell.y == 7.0)
#define TT_IS_FOAM(cell)  (cell.x == 1.0 && cell.y == 7.0)

// Animated toon water: drifting large-scale tint + crisp moving highlight lines on
// upward-facing water; a gentle pulse on the foam ring. Fragment-only, so tile seams
// (shared vertex positions) can never crack.
half3 TT_Water(half3 color, float2 uv, float3 posWS, float3 nWS, half3 foamLit)
{
    float2 cell = floor(uv * 8.0);
    float t = _TT_Time;
    if (TT_IS_WATER(cell) && nWS.y > 0.5)
    {
        float2 p = posWS.xz;
        float n = TT_Noise(p * 0.45 + float2(t * 0.05, t * 0.08))
                + 0.5 * TT_Noise(p * 1.1 - float2(t * 0.07, -t * 0.04));
        float bands = frac(n * 2.4 + t * 0.04);
        half line_ = step(bands, 0.06);
        half tint = TT_Noise(p * 0.18 + t * 0.015);
        color *= lerp(0.94, 1.05, tint);
        color = lerp(color, lerp(color, foamLit, 0.55), line_);
    }
    else if (TT_IS_FOAM(cell))
    {
        color *= 1.0 + 0.06 * sin(t * 1.6 + posWS.x * 0.8 + posWS.z * 0.6);
    }
    return color;
}

#endif
