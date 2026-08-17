// Pohon (batang, tajuk, pohon mesh) yang berdiri DI DEPAN pemain melubangi dirinya dengan
// pola dither di sekitar posisi layar pemain — pemain tidak pernah hilang di balik hutan.
//
// Kenapa shader, bukan ganti material per pohon: pohon digambar lewat PropBatch
// (RenderMeshInstanced, ribuan instance, tanpa GameObject per pohon), jadi tidak ada renderer
// individual yang bisa dipudarkan. Lubangnya harus diputuskan per PIKSEL.
//
// Datanya global, diisi SeeThroughFeeder tiap frame:
//   _SeeThroughData     xy = posisi viewport pemain, z = kedalaman mata pemain,
//                       w  = jari-jari lubang (porsi tinggi layar)
//   _SeeThroughStrength 0 = mati, 1 = pusat lubang hilang penuh
//
// Bayangan TIDAK ikut bolong — pass ShadowCaster dibiarkan pejal, supaya cahaya tidak bocor
// dari pohon yang cuma tembus di mata kamera.
Shader "Grimoire/PropSeeThrough"
{
    Properties
    {
        _BaseMap("Base Map", 2D) = "white" {}
        _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        _Color("Color (legacy)", Color) = (1, 1, 1, 1)
        _Smoothness("Smoothness", Range(0, 1)) = 0
        _Metallic("Metallic", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _Color;
                half _Smoothness;
                half _Metallic;
            CBUFFER_END

            float4 _SeeThroughData;
            float _SeeThroughStrength;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float viewZ : TEXCOORD3;
                float fogFactor : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);

                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);

                // Kedalaman MATA, bukan kedalaman buffer: kameranya ortografis, dan di ortho
                // nilai w clip selalu 1 — satu-satunya jarak yang jujur adalah -z ruang view.
                o.viewZ = -TransformWorldToView(o.positionWS).z;
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half Dither4x4(float2 pixel)
            {
                int2 p = int2(fmod(pixel, 4.0));
                int index = p.y * 4 + p.x;

                // Matriks Bayer 4x4, dinormalkan 1..16 / 16.
                const half d[16] =
                {
                    0.0625, 0.5625, 0.1875, 0.6875,
                    0.8125, 0.3125, 0.9375, 0.4375,
                    0.2500, 0.7500, 0.1250, 0.6250,
                    1.0000, 0.5000, 0.8750, 0.3750
                };
                return d[index];
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);

                // Posisi layar piksel ini, disamakan ruangnya dengan WorldToViewportPoint
                // (y dari BAWAH) — SV_POSITION menghitung y dari atas di D3D.
                float2 uvScreen = i.positionCS.xy / _ScreenParams.xy;
                #if UNITY_UV_STARTS_AT_TOP
                uvScreen.y = 1.0 - uvScreen.y;
                #endif

                float aspect = _ScreenParams.x / _ScreenParams.y;
                float2 toPlayer = uvScreen - _SeeThroughData.xy;
                toPlayer.x *= aspect;
                float dist = length(toPlayer);

                half hole = 1.0 - smoothstep(_SeeThroughData.w * 0.55, _SeeThroughData.w, dist);
                bool inFront = i.viewZ < _SeeThroughData.z - 0.4;
                half keep = 1.0 - (inFront ? hole * _SeeThroughStrength : 0.0);
                clip(keep - Dither4x4(i.positionCS.xy));

                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * _BaseColor;

                float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                Light mainLight = GetMainLight(shadowCoord);

                half3 n = normalize(i.normalWS);
                half ndl = saturate(dot(n, mainLight.direction));
                half3 lighting = mainLight.color * mainLight.shadowAttenuation * ndl + SampleSH(n);

                half3 color = albedo.rgb * lighting;
                color = MixFog(color, i.fogFactor);
                return half4(color, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex shadowVert
            #pragma fragment shadowFrag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;

            struct ShadowAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct ShadowVaryings
            {
                float4 positionCS : SV_POSITION;
            };

            ShadowVaryings shadowVert(ShadowAttributes v)
            {
                UNITY_SETUP_INSTANCE_ID(v);

                ShadowVaryings o;
                float3 positionWS = TransformObjectToWorld(v.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));

                #if UNITY_REVERSED_Z
                o.positionCS.z = min(o.positionCS.z, o.positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #else
                o.positionCS.z = max(o.positionCS.z, o.positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #endif
                return o;
            }

            half4 shadowFrag(ShadowVaryings i) : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex depthVert
            #pragma fragment depthFrag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct DepthAttributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct DepthVaryings
            {
                float4 positionCS : SV_POSITION;
            };

            DepthVaryings depthVert(DepthAttributes v)
            {
                UNITY_SETUP_INSTANCE_ID(v);

                DepthVaryings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                return o;
            }

            half4 depthFrag(DepthVaryings i) : SV_Target { return 0; }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
