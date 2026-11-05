// Toon Tropical - embers for fire particles (URP). Unlit, opaque, toon-style: particles
// pop out (alpha-clipped) instead of fading, matching the hard-edged look of the pack.
Shader "ToonTropical/Ember"
{
    Properties
    {
        _Cutoff ("Pop-out threshold", Range(0, 1)) = 0.35
    }
    SubShader
    {
        PackageRequirements { "com.unity.render-pipelines.universal" }   // URP only: skipped (not compiled) in Built-in projects
        Tags { "RenderType" = "TransparentCutout" "RenderPipeline" = "UniversalPipeline" "Queue" = "AlphaTest" }
        Pass
        {
            Name "Ember"
            Tags { "LightMode" = "UniversalForwardOnly" }   // ForwardOnly: also drawn by the Deferred path
            Cull Off
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half _Cutoff;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; };
            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.color = v.color;
                return o;
            }
            half4 frag(Varyings i) : SV_Target
            {
                clip(i.color.a - _Cutoff);
                return half4(i.color.rgb, 1);
            }
            ENDHLSL
        }
    }

    // Built-in Render Pipeline (picked automatically when no SRP is active).
    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" "Queue" = "AlphaTest" }
        Pass
        {
            Name "Ember"
            Cull Off
            ZWrite On

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            half _Cutoff;
            struct appdata { float4 vertex : POSITION; half4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; half4 color : COLOR; };
            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                return o;
            }
            half4 frag(v2f i) : SV_Target
            {
                clip(i.color.a - _Cutoff);
                return half4(i.color.rgb, 1);
            }
            ENDCG
        }
    }
    FallBack Off
}
