// Toon Tropical - particles drawn as flat cel shapes (circle, star, ring) with an ink rim and
// a shaded lower half, matching the pack's look. Colour and fade come from the particle
// colour. One plain CG pass (no LightMode tag), so the same material renders in URP (as
// SRPDefaultUnlit) and in the Built-in pipeline. Used by the FX_ prefabs.
Shader "ToonTropical/Particle"
{
    Properties
    {
        [KeywordEnum(Circle, Star, Ring)] _Shape ("Shape", Float) = 0
        _InkColor ("Ink Color", Color) = (0.1686, 0.1216, 0.1608, 1)
        _InkWidth ("Ink Width", Range(0, 0.4)) = 0.14
        _Shade ("Shaded Half", Range(0, 0.5)) = 0.18
        [Toggle] _Glow ("Glow (no ink, no shading)", Float) = 0
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" "PreviewType" = "Plane" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma shader_feature_local _SHAPE_CIRCLE _SHAPE_STAR _SHAPE_RING
            #include "UnityCG.cginc"

            fixed4 _InkColor;
            float _InkWidth, _Shade, _Glow;
            float4 _TT_FxTint;      // time-of-day tint from TT_Environment (a = 0 when not set)

            struct appdata { float4 vertex : POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; UNITY_FOG_COORDS(1) };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.uv = v.uv * 2.0 - 1.0;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            // signed distance-ish: < 0 inside the shape, 0 at its edge
            float Shape(float2 p)
            {
                float r = length(p);
                #if defined(_SHAPE_STAR)
                    float a = atan2(p.y, p.x);
                    float k = 0.55 + 0.45 * pow(abs(cos(a * 2.5)), 2.0);   // 5 rounded points
                    return r - 0.95 * k;
                #elif defined(_SHAPE_RING)
                    return abs(r - 0.72) - 0.2;
                #else
                    return r - 0.95;
                #endif
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float s = Shape(i.uv);
                clip(-s);
                fixed3 c = i.color.rgb;
                if (_Glow < 0.5)
                {
                    if (dot(i.uv, normalize(float2(0.7, -1.0))) > 0.32) c *= 1.0 - _Shade;   // shaded half
                    if (s > -_InkWidth) c = _InkColor.rgb;                                 // ink rim
                    c *= lerp(float3(1, 1, 1), _TT_FxTint.rgb, _TT_FxTint.a);             // dusk / night
                }
                UNITY_APPLY_FOG(i.fogCoord, c);
                return fixed4(c, i.color.a);
            }
            ENDCG
        }
    }
    FallBack Off
}
