// Toon Tropical - stylized skybox: a banded gradient, a sun (or moon) with toon halo rings,
// flat cartoon clouds with an ink edge, distant island silhouettes on the horizon and stars
// at night. Colours come from TT_Environment (theme + time of day); the material sets the
// style. One plain CG pass, so the same material works in URP and the Built-in pipeline.
// Use it on perspective cameras (TT Environment > Sky). Orthographic views keep their
// solid backdrop colour.
Shader "ToonTropical/Sky"
{
    Properties
    {
        [Header(Gradient)]
        [IntRange] _Bands ("Gradient Bands (0 = smooth)", Range(0, 12)) = 6
        _GradientHeight ("Gradient Height", Range(0.1, 1)) = 0.32

        [Header(Sun and Moon)]
        _SunSize ("Disc Size (deg)", Range(0.5, 12)) = 4
        _HaloSize ("Halo Size (deg)", Range(0, 30)) = 11

        [Header(Clouds)]
        _CloudCoverage ("Coverage", Range(0, 1)) = 0.5
        _CloudScale ("Scale", Range(0.1, 4)) = 1.1
        _CloudSpeed ("Drift Speed", Range(0, 0.2)) = 0.012
        _CloudInk ("Ink Edge", Range(0, 0.05)) = 0.012

        [Header(Distant Islands)]
        _IslandHeight ("Height", Range(0, 0.15)) = 0.05
        _IslandAmount ("Amount", Range(0, 1)) = 0.5

        [Header(Stars)]
        _StarDensity ("Density", Range(0, 0.1)) = 0.03
    }
    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" }
        Cull Off ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float _Bands, _GradientHeight, _SunSize, _HaloSize;
            float _CloudCoverage, _CloudScale, _CloudSpeed, _CloudInk;
            float _IslandHeight, _IslandAmount, _StarDensity;

            // set every frame by TT_Environment
            float  _TT_Time;
            float4 _TT_SkyTop, _TT_SkyHorizon, _TT_SkyGround, _TT_SkyCloud, _TT_SkyIsland;
            float4 _TT_SunDir;      // xyz = towards the sun / moon, w = 1 when set
            float4 _TT_SunColor;    // rgb = disc colour, a = stars amount (night)
            float4 _TT_InkColor;

            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 pos : SV_POSITION; float3 dir : TEXCOORD0; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = v.vertex.xyz;
                return o;
            }

            float Hash(float2 p) { p = frac(p * float2(123.34, 456.21)); p += dot(p, p + 45.32); return frac(p.x * p.y); }
            float Hash3(float3 p) { return Hash(p.xy + p.z * 17.13); }
            float Noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), u.x),
                            lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), u.x), u.y);
            }
            float Fbm(float2 p) { return Noise(p) * 0.6 + Noise(p * 2.1 + 3.7) * 0.28 + Noise(p * 4.3 + 9.1) * 0.12; }

            fixed4 frag(v2f i) : SV_Target
            {
                // without TT_Environment the globals are 0: fall back to a pleasant day sky
                bool ready = _TT_SkyHorizon.a > 0.5;
                float3 top = ready ? _TT_SkyTop.rgb : float3(0.55, 0.78, 0.93);
                float3 hor = ready ? _TT_SkyHorizon.rgb : float3(0.98, 0.91, 0.83);
                float3 gnd = ready ? _TT_SkyGround.rgb : float3(0.80, 0.86, 0.88);
                float3 cloudC = ready ? _TT_SkyCloud.rgb : float3(1, 1, 1);
                float3 islandC = ready ? _TT_SkyIsland.rgb : float3(0.72, 0.80, 0.84);
                float3 ink = ready ? _TT_InkColor.rgb : float3(0.17, 0.12, 0.16);

                float3 d = normalize(i.dir);
                float h = d.y;

                // banded vertical gradient above the horizon, flat haze below it
                float t = saturate(h / _GradientHeight);
                t = pow(t, 0.8);
                if (_Bands > 0.5) t = floor(t * _Bands + 0.5) / _Bands;
                float3 col = h >= 0 ? lerp(hor, top, t) : gnd;

                // sun / moon: hard disc with two flat halo rings
                float3 sd = _TT_SunDir.w > 0.5 ? normalize(_TT_SunDir.xyz) : normalize(float3(-0.4, 0.45, 0.8));
                float3 sunC = ready ? _TT_SunColor.rgb : float3(1, 0.96, 0.82);
                float ang = degrees(acos(clamp(dot(d, sd), -1, 1)));
                if (ang < _HaloSize) col = lerp(col, sunC, ang < _HaloSize * 0.6 ? 0.28 : 0.14);
                if (ang < _SunSize) col = sunC;
                if (ang >= _SunSize && ang < _SunSize + 0.35) col = lerp(col, ink, 0.35);

                // stars (night): sparse hashed points, fading out toward the horizon
                float stars = ready ? _TT_SunColor.a : 0;
                if (stars > 0.01 && h > 0.05 && ang > _HaloSize)
                {
                    float3 p = d * 140.0;
                    float3 c = floor(p);
                    float r = Hash3(c);
                    float3 f = frac(p) - 0.5;
                    float s = step(1.0 - _StarDensity, r) * step(length(f), 0.11 + 0.08 * frac(r * 37.0));
                    float tw = 0.75 + 0.25 * sin(_TT_Time * 2.0 + r * 60.0);
                    col = lerp(col, float3(1, 0.97, 0.88), s * stars * saturate(h * 5.0) * tw);
                }

                // flat cartoon clouds on a dome: hard-edged fbm, an ink rim and a shaded underside
                if (h > 0.015 && _CloudCoverage > 0.001)
                {
                    float2 uv = d.xz / (h + 0.18) * _CloudScale + float2(_TT_Time, _TT_Time * 0.4) * _CloudSpeed;
                    float fade = saturate((h - 0.015) * 7.0);
                    float th = 0.78 - _CloudCoverage * 0.42;
                    float n = Fbm(uv) * fade;
                    float nUp = Fbm(uv + float2(0, -0.09)) * fade;       // sample just below: shade the base
                    if (n > th)
                    {
                        float3 cc = nUp > th ? cloudC : lerp(cloudC, top, 0.35) * 0.93;
                        col = n < th + _CloudInk ? lerp(cc, ink, 0.55) : cc;
                    }
                }

                // distant islands: low silhouettes along the horizon with an ink top line
                if (_IslandAmount > 0.001 && h > -0.01)
                {
                    float az = atan2(d.x, d.z);
                    float b = Noise(float2(az * 2.2, 3.1)) * 0.65 + Noise(float2(az * 6.0, 7.7)) * 0.35;
                    float bump = saturate((b - (1.0 - _IslandAmount)) / max(_IslandAmount, 1e-3));
                    float top_ = bump * _IslandHeight;
                    if (h < top_) col = h > top_ - 0.0025 ? lerp(islandC, ink, 0.5) : islandC;
                }
                return fixed4(col, 1);
            }
            ENDCG
        }
    }
    FallBack Off
}
