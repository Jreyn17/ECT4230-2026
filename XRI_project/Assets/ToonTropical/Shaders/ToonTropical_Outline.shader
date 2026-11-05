// Toon Tropical - ink outline for the inverted-hull "_Outline" meshes (URP).
// The hull's normals are already flipped, so plain back-face culling shows only the
// rim around the object. Unlit, flat colour, casts no shadows. It runs the same wind /
// flame animation as ToonTropical/Toon (shared vertex mask), so ink stays glued to the
// moving mesh. Hide the _Outline child (or disable its renderer) to turn outlines off.
Shader "ToonTropical/Outline"
{
    Properties
    {
        [MainColor] _BaseColor ("Ink Color", Color) = (0.1686, 0.1216, 0.1608, 1)   // #2B1F29
    }

    SubShader
    {
        PackageRequirements { "com.unity.render-pipelines.universal" }   // URP only: skipped (not compiled) in Built-in projects
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
        CBUFFER_END
        #include "ToonTropical_Common.hlsl"
        ENDHLSL

        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "UniversalForwardOnly" }   // ForwardOnly: also drawn by the Deferred path
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            struct Attributes { float4 positionOS : POSITION; float4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; half fogFactor : TEXCOORD0; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                o.positionCS = TransformWorldToHClip(TT_Animate(TransformObjectToWorld(v.positionOS.xyz), v.color));
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                return half4(MixFog(_BaseColor.rgb, i.fogFactor), 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On ColorMask R Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            struct Attributes { float4 positionOS : POSITION; float4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; };
            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                o.positionCS = TransformWorldToHClip(TT_Animate(TransformObjectToWorld(v.positionOS.xyz), v.color));
                return o;
            }
            half frag(Varyings i) : SV_Target { return i.positionCS.z; }
            ENDHLSL
        }
    }

    // Built-in Render Pipeline (picked automatically when no SRP is active).
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "ForwardBase" }
            Cull Back
            ZWrite On

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            #define TT_BUILTIN 1
            half4 _BaseColor;
            #include "ToonTropical_Common.hlsl"

            struct appdata { float4 vertex : POSITION; float4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; UNITY_FOG_COORDS(0) };

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                o.pos = UnityWorldToClipPos(TT_Animate(mul(unity_ObjectToWorld, float4(v.vertex.xyz, 1)).xyz, v.color));
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                half3 c = _BaseColor.rgb;
                UNITY_APPLY_FOG(i.fogCoord, c);
                return half4(c, 1);
            }
            ENDCG
        }
    }
    FallBack Off
}
