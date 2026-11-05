// Toon Tropical - 3-band cel shader (URP).
// Matches the Blender look: hard N.L bands (shadow / mid / lit) multiply the palette colour;
// cast shadows blend in softly. Adds the pack's animation (wind sway, flame flicker,
// animated water + foam) and time-of-day palette swaps, all driven by TT_Environment
// globals (see ToonTropical_Common.hlsl). Without TT_Environment it renders static.
//
// Textured Mode is for models from other packs (Tools > Toon Tropical > Toonify): the same
// bands and shadows on the model's own texture, tinted by the theme / time of day, with
// optional alpha clipping and double-sided rendering for foliage cards.
Shader "ToonTropical/Toon"
{
    Properties
    {
        [MainTexture] _BaseMap ("Palette / Texture", 2D) = "white" {}
        [MainColor] _BaseColor ("Color", Color) = (1, 1, 1, 1)

        [Header(Bands)]
        _MidThreshold ("Shadow -> Mid at", Range(0, 1)) = 0.04
        _LitThreshold ("Mid -> Lit at", Range(0, 1)) = 0.42
        _BandSoftness ("Band Edge Softness", Range(0, 0.1)) = 0.0
        _ShadowTint ("Shadow Tint", Color) = (0.6196, 0.6510, 0.8196, 1)   // #9EA6D1
        _MidTint ("Mid Tint", Color) = (0.8627, 0.8667, 0.9333, 1)         // #DCDDEE
        _LitTint ("Lit Tint", Color) = (1, 1, 1, 1)

        [Header(Lighting)]
        _LightColorInfluence ("Main Light Color Influence", Range(0, 1)) = 0
        _AdditionalLights ("Additional Lights Strength", Range(0, 2)) = 1
        _ShadowBlur ("Shadow Blur (m, 0 = off)", Range(0, 0.3)) = 0.08

        [Header(Animation)]
        [Toggle] _AnimateWater ("Animate Water and Foam", Float) = 1

        [Header(Other Packs)]
        [Toggle(_TT_TEXTURED)] _Textured ("Textured Mode (own texture, not the palette)", Float) = 0
        [Toggle(_TT_ALPHACLIP)] _AlphaClip ("Alpha Clip (foliage cutouts)", Float) = 0
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }

    SubShader
    {
        PackageRequirements { "com.unity.render-pipelines.universal" }   // URP only: skipped (not compiled) in Built-in projects
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half _MidThreshold, _LitThreshold, _BandSoftness;
            half4 _ShadowTint, _MidTint, _LitTint;
            half _LightColorInfluence, _AdditionalLights, _ShadowBlur, _AnimateWater;
            half _Cutoff;
        CBUFFER_END

        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        #include "ToonTropical_Common.hlsl"

        void TT_Clip(float2 uv)
        {
            #ifdef _TT_ALPHACLIP
                clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).a * _BaseColor.a - _Cutoff);
            #endif
        }
        ENDHLSL

        Pass
        {
            Name "ForwardToon"
            Tags { "LightMode" = "UniversalForwardOnly" }   // ForwardOnly: also drawn by the Deferred path
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local _TT_TEXTURED
            #pragma shader_feature_local _TT_ALPHACLIP
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float4 shadowCoord : TEXCOORD3;
                half fogFactor : TEXCOORD4;
                half4 mask : TEXCOORD5;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                float4 mask = TT_MASK(v.color);
                float3 posWS = TT_Animate(TransformObjectToWorld(v.positionOS.xyz), mask);
                o.positionWS = posWS;
                o.positionCS = TransformWorldToHClip(posWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                VertexPositionInputs p = (VertexPositionInputs)0;
                p.positionWS = posWS;
                p.positionCS = o.positionCS;
                o.shadowCoord = GetShadowCoord(p);
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                o.mask = mask;
                return o;
            }

            half3 Band(half value)
            {
                half s = max(_BandSoftness, 1e-4);
                half toMid = smoothstep(_MidThreshold - s, _MidThreshold + s, value);
                half toLit = smoothstep(_LitThreshold - s, _LitThreshold + s, value);
                half3 c = lerp(_ShadowTint.rgb, _MidTint.rgb, toMid);
                return lerp(c, _LitTint.rgb, toLit);
            }

            half3 Palette(float2 uv)
            {
                half3 m = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).rgb;
                half3 g = SAMPLE_TEXTURE2D(_TT_PaletteGlobal, sampler_BaseMap, uv).rgb;
                return lerp(m, g, _TT_PaletteOverride) * _BaseColor.rgb;
            }

            half4 Albedo(float2 uv)
            {
                #ifdef _TT_TEXTURED
                    half4 t = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv) * _BaseColor;
                    t.rgb *= TT_ThemeTint();
                    return t;
                #else
                    return half4(Palette(uv), 1);
                #endif
            }

            // Cast-shadow visibility blurred over a disc (radius _ShadowBlur, metres) perpendicular
            // to the light: approximates the 4-degree sun the Blender look was authored with, so
            // frond and prop shadows have soft edges instead of shadow-map stair steps.
            static const float2 kDisc[8] = {
                float2(-0.613, 0.354), float2(0.171, 0.722), float2(0.744, 0.197), float2(0.469, -0.552),
                float2(-0.257, -0.689), float2(-0.931, -0.163), float2(0.0, 0.0), float2(0.282, 0.06) };

            half MainShadow(float3 posWS, float3 n, float3 l, float4 sc)
            {
                half s = GetMainLight(sc).shadowAttenuation;
                if (_ShadowBlur <= 0) return s;
                float3 t = normalize(cross(l, abs(l.y) < 0.99 ? float3(0, 1, 0) : float3(1, 0, 0)));
                float3 b = cross(l, t);
                float3 base = posWS + n * (_ShadowBlur * 0.5);   // lift off the surface: no self-shadow acne
                [unroll] for (int k = 0; k < 8; k++)
                {
                    float3 p = base + (t * kDisc[k].x + b * kDisc[k].y) * _ShadowBlur;
                    s += GetMainLight(TransformWorldToShadowCoord(p)).shadowAttenuation;
                }
                return s / 9.0;
            }

            half4 frag(Varyings i, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                half4 a = Albedo(i.uv);
                #ifdef _TT_ALPHACLIP
                    clip(a.a - _Cutoff);
                #endif
                half3 albedo = a.rgb;

                // flames and glowing glass: self-lit, flickering / pulsing
                if (i.mask.g > 0.5 || i.mask.b > 0.5)
                    return half4(MixFog(albedo * TT_Flicker(i.mask), i.fogFactor), 1);

                float3 n = normalize(i.normalWS) * IS_FRONT_VFACE(face, 1.0, -1.0);   // double-sided cards
                #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                    float4 sc = i.shadowCoord;
                #else
                    float4 sc = TransformWorldToShadowCoord(i.positionWS);
                #endif
                Light mainLight = GetMainLight(sc);
                half shadow = MainShadow(i.positionWS, n, mainLight.direction, sc);
                // Surface-angle bands stay hard. Cast shadows blend toward the shadow band by the
                // (soft, filtered) shadow visibility, the same result Blender's EEVEE gets by
                // averaging hard toon samples over its soft sun.
                half ndl = saturate(dot(n, mainLight.direction));
                half3 band = lerp(Band(0), Band(ndl), shadow);
                half3 lightTint = lerp(half3(1, 1, 1), mainLight.color, _LightColorInfluence);
                half3 color = albedo * band * lightTint;

                #ifndef _TT_TEXTURED
                    if (_AnimateWater > 0.5)
                        color = TT_Water(color, i.uv, i.positionWS, n, Palette(TT_FOAM_UV) * band);
                #endif

                // Additional (point / spot) lights: a soft facing term (a hard step cuts rings into
                // flat ground round a low light) and a smooth distance fade: a fire casts a soft warm
                // pool. Scaled by the time of day: fires glow at night, subtle by day.
                #if defined(_ADDITIONAL_LIGHTS)
                    half addScale = _AdditionalLights * (_TT_AdditionalLightScale > 0 ? _TT_AdditionalLightScale : 1.0);
                    InputData inputData = (InputData)0;
                    inputData.positionWS = i.positionWS;
                    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                    uint count = GetAdditionalLightsCount();
                    LIGHT_LOOP_BEGIN(count)
                        Light l = GetAdditionalLight(lightIndex, i.positionWS, half4(1, 1, 1, 1));
                        half facing = saturate(dot(n, l.direction)) * l.shadowAttenuation;
                        half stepA = smoothstep(0.0, _LitThreshold, facing) * 1.5;   // soft: no rings on flat ground
                        half fade = saturate(sqrt(l.distanceAttenuation));   // wide, soft pool
                        color += albedo * l.color * stepA * 0.5 * fade * addScale;
                    LIGHT_LOOP_END
                #endif

                color = MixFog(color, i.fogFactor);
                return half4(color, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0 Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local _TT_TEXTURED
            #pragma shader_feature_local _TT_ALPHACLIP
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; float4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                float3 posWS = TT_Animate(TransformObjectToWorld(v.positionOS.xyz), TT_MASK(v.color));
                float3 nWS = TransformObjectToWorldNormal(v.normalOS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 dir = normalize(_LightPosition - posWS);
                #else
                    float3 dir = _LightDirection;
                #endif
                o.positionCS = TransformWorldToHClip(ApplyShadowBias(posWS, nWS, dir));
                #if UNITY_REVERSED_Z
                    o.positionCS.z = min(o.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    o.positionCS.z = max(o.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                return o;
            }
            half4 frag(Varyings i) : SV_Target { TT_Clip(i.uv); return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On ColorMask R Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local _TT_TEXTURED
            #pragma shader_feature_local _TT_ALPHACLIP
            #pragma multi_compile_instancing
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                o.positionCS = TransformWorldToHClip(TT_Animate(TransformObjectToWorld(v.positionOS.xyz), TT_MASK(v.color)));
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                return o;
            }
            half frag(Varyings i) : SV_Target { TT_Clip(i.uv); return i.positionCS.z; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local _TT_TEXTURED
            #pragma shader_feature_local _TT_ALPHACLIP
            #pragma multi_compile_instancing
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; float4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float2 uv : TEXCOORD1; };
            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                o.positionCS = TransformWorldToHClip(TT_Animate(TransformObjectToWorld(v.positionOS.xyz), TT_MASK(v.color)));
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                return o;
            }
            half4 frag(Varyings i) : SV_Target { TT_Clip(i.uv); return half4(NormalizeNormalPerPixel(i.normalWS), 0); }
            ENDHLSL
        }
    }
    // ------------------------------------------------------------------------------------
    // Built-in Render Pipeline. Unity picks this SubShader automatically when no SRP is
    // active, so the same materials work in both pipelines. Same bands, palette swap,
    // wind / flame / water animation; cast shadows use the pipeline's own soft shadows.
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }

        CGINCLUDE
        #include "UnityCG.cginc"
        #define TT_BUILTIN 1
        sampler2D _BaseMap;
        float4 _BaseMap_ST;
        half4 _BaseColor;
        half _MidThreshold, _LitThreshold, _BandSoftness;
        half4 _ShadowTint, _MidTint, _LitTint;
        half _LightColorInfluence, _AdditionalLights, _ShadowBlur, _AnimateWater;
        half _Cutoff;
        #include "ToonTropical_Common.hlsl"

        half3 Band(half value)
        {
            half s = max(_BandSoftness, 1e-4);
            half toMid = smoothstep(_MidThreshold - s, _MidThreshold + s, value);
            half toLit = smoothstep(_LitThreshold - s, _LitThreshold + s, value);
            return lerp(lerp(_ShadowTint.rgb, _MidTint.rgb, toMid), _LitTint.rgb, toLit);
        }

        half3 Palette(float2 uv)
        {
            half3 m = tex2D(_BaseMap, uv).rgb;
            half3 g = tex2D(_TT_PaletteGlobal, uv).rgb;
            return lerp(m, g, _TT_PaletteOverride) * _BaseColor.rgb;
        }

        half4 Albedo(float2 uv)
        {
            #ifdef _TT_TEXTURED
                half4 t = tex2D(_BaseMap, uv) * _BaseColor;
                t.rgb *= TT_ThemeTint();
                return t;
            #else
                return half4(Palette(uv), 1);
            #endif
        }

        void TT_Clip(float2 uv)
        {
            #ifdef _TT_ALPHACLIP
                clip(tex2D(_BaseMap, uv).a * _BaseColor.a - _Cutoff);
            #endif
        }

        struct appdata
        {
            float4 vertex : POSITION;
            float3 normal : NORMAL;
            float2 uv : TEXCOORD0;
            float4 color : COLOR;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };
        ENDCG

        Pass
        {
            Name "FORWARD"
            Tags { "LightMode" = "ForwardBase" }
            Cull [_Cull]

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local _TT_TEXTURED
            #pragma shader_feature_local _TT_ALPHACLIP
            #pragma multi_compile_fwdbase
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Lighting.cginc"
            #include "AutoLight.cginc"

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 posWS : TEXCOORD1;
                float3 nWS : TEXCOORD2;
                half4 mask : TEXCOORD3;
                SHADOW_COORDS(4)
                UNITY_FOG_COORDS(5)
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                float4 mask = TT_MASK(v.color);
                float3 posWS = TT_Animate(mul(unity_ObjectToWorld, float4(v.vertex.xyz, 1)).xyz, mask);
                v.vertex.xyz = mul(unity_WorldToObject, float4(posWS, 1)).xyz;   // for the shadow macro
                o.posWS = posWS;
                o.pos = UnityWorldToClipPos(posWS);
                o.nWS = UnityObjectToWorldNormal(v.normal);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.mask = mask;
                TRANSFER_SHADOW(o);
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            half4 frag(v2f i, fixed facing : VFACE) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                half4 a = Albedo(i.uv);
                #ifdef _TT_ALPHACLIP
                    clip(a.a - _Cutoff);
                #endif
                half3 albedo = a.rgb;
                half3 color;
                if (i.mask.g > 0.5 || i.mask.b > 0.5)
                {
                    color = albedo * TT_Flicker(i.mask);              // flames, glowing glass
                }
                else
                {
                    float3 n = normalize(i.nWS) * (facing > 0 ? 1.0 : -1.0);
                    float3 l = normalize(_WorldSpaceLightPos0.xyz);
                    UNITY_LIGHT_ATTENUATION(shadow, i, i.posWS);
                    half ndl = saturate(dot(n, l));
                    half3 band = lerp(Band(0), Band(ndl), shadow);
                    half3 tint = lerp(half3(1, 1, 1), _LightColor0.rgb, _LightColorInfluence);
                    color = albedo * band * tint;
                    #ifndef _TT_TEXTURED
                        if (_AnimateWater > 0.5)
                            color = TT_Water(color, i.uv, i.posWS, n, Palette(TT_FOAM_UV) * band);
                    #endif
                }
                UNITY_APPLY_FOG(i.fogCoord, color);
                return half4(color, 1);
            }
            ENDCG
        }

        Pass
        {
            Name "FORWARD_DELTA"
            Tags { "LightMode" = "ForwardAdd" }
            Blend One One
            ZWrite Off
            Cull [_Cull]

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local _TT_TEXTURED
            #pragma shader_feature_local _TT_ALPHACLIP
            #pragma multi_compile_fwdadd_fullshadows
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Lighting.cginc"
            #include "AutoLight.cginc"

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 posWS : TEXCOORD1;
                float3 nWS : TEXCOORD2;
                half4 mask : TEXCOORD3;
                SHADOW_COORDS(4)
                UNITY_FOG_COORDS(5)
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                float4 mask = TT_MASK(v.color);
                float3 posWS = TT_Animate(mul(unity_ObjectToWorld, float4(v.vertex.xyz, 1)).xyz, mask);
                v.vertex.xyz = mul(unity_WorldToObject, float4(posWS, 1)).xyz;
                o.posWS = posWS;
                o.pos = UnityWorldToClipPos(posWS);
                o.nWS = UnityObjectToWorldNormal(v.normal);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.mask = mask;
                TRANSFER_SHADOW(o);
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            // point / spot lights (fires, torches, lanterns): hard bands, scaled by time of day
            half4 frag(v2f i, fixed facing : VFACE) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                half4 alb = Albedo(i.uv);
                #ifdef _TT_ALPHACLIP
                    clip(alb.a - _Cutoff);
                #endif
                if (i.mask.g > 0.5 || i.mask.b > 0.5) return 0;
                float3 n = normalize(i.nWS) * (facing > 0 ? 1.0 : -1.0);
                float3 l = _WorldSpaceLightPos0.w == 0 ? normalize(_WorldSpaceLightPos0.xyz)
                                                        : normalize(_WorldSpaceLightPos0.xyz - i.posWS);
                UNITY_LIGHT_ATTENUATION(atten, i, i.posWS);
                // soft facing and a smooth fade with distance: no hard disc round fires
                half a = saturate(dot(n, l));
                half stepA = smoothstep(0.0, _LitThreshold, a) * 1.5;   // soft: no rings on flat ground
                half fade = smoothstep(0.0, 1.0, saturate(atten));
                half addScale = _AdditionalLights * (_TT_AdditionalLightScale > 0 ? _TT_AdditionalLightScale : 1.0);
                half3 color = alb.rgb * _LightColor0.rgb * stepA * 0.5 * fade * addScale;
                UNITY_APPLY_FOG_COLOR(i.fogCoord, color, half4(0, 0, 0, 0));
                return half4(color, 1);
            }
            ENDCG
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On ZTest LEqual Cull [_Cull]

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local _TT_TEXTURED
            #pragma shader_feature_local _TT_ALPHACLIP
            #pragma multi_compile_shadowcaster
            #pragma multi_compile_instancing

            struct v2f { V2F_SHADOW_CASTER; float2 uv : TEXCOORD1; };

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                float3 posWS = TT_Animate(mul(unity_ObjectToWorld, float4(v.vertex.xyz, 1)).xyz, TT_MASK(v.color));
                v.vertex.xyz = mul(unity_WorldToObject, float4(posWS, 1)).xyz;
                TRANSFER_SHADOW_CASTER_NORMALOFFSET(o)
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                return o;
            }
            float4 frag(v2f i) : SV_Target { TT_Clip(i.uv); SHADOW_CASTER_FRAGMENT(i) }
            ENDCG
        }
    }
    FallBack "Hidden/InternalErrorShader"   // exists in every pipeline
}
