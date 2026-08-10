Shader "Grimoire/BurnAway"
{
    // Mesh yang hangus dan hilang, untuk benda BIASA.
    //
    // Musuh sudah punya versinya sendiri di dalam Grimoire/EnemyVat, tapi jalur itu terikat
    // pada animasi yang dipanggang ke tekstur (VAT) dan pada renderer instanced-nya. Pemain
    // cuma sebuah kapsul dengan MeshRenderer biasa; menyalin shader musuh ke situ berarti
    // menyeret seluruh mesin VAT untuk satu objek yang tidak punya animasi sama sekali.
    //
    // Noisenya DIHITUNG, bukan disampel dari tekstur: satu benda yang terbakar sekali per run
    // tidak pantas menuntut aset baru, dan value-noise tiga oktaf sudah cukup untuk membuat
    // tepinya compang-camping alih-alih rata seperti gelas terisi.
    Properties
    {
        // Putih polos = perilaku lama (kapsul berwarna). Model bertekstur mengisi slot ini
        // dari material aslinya supaya rupanya tidak berubah selama masih hidup.
        _BaseMap ("Tekstur", 2D) = "white" {}
        _BaseColor ("Warna", Color) = (0.85, 0.78, 0.55, 1)
        _Burn ("Hangus", Range(0, 1)) = 0
        _EdgeWidth ("Lebar bara", Range(0.01, 0.5)) = 0.16
        _EdgeColor ("Warna bara", Color) = (1, 0.45, 0.08, 1)
        _NoiseScale ("Rapat noise", Float) = 6

        // Rentang tinggi mesh di ruang objek, untuk arah jalar bawah-ke-atas. Bawaannya persis
        // kapsul primitif (y -0,5..0,5); mesh lain (model pemain) mengisi rentang aslinya
        // lewat kode supaya gradien membentang dari kaki ke kepala, bukan menumpuk di pinggang.
        _HeightMin ("Kaki (y objek)", Float) = -0.5
        _HeightMax ("Kepala (y objek)", Float) = 0.5
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "AlphaTest"
        }

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }

            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _EdgeColor;
                float _Burn;
                float _EdgeWidth;
                float _NoiseScale;
                float _HeightMin;
                float _HeightMax;
            CBUFFER_END

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
                float3 positionOS : TEXCOORD1;
                float2 uv         : TEXCOORD2;
            };

            Varyings vert(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.positionOS = input.positionOS.xyz;
                o.uv = input.uv;
                return o;
            }

            float Hash(float3 p)
            {
                p = frac(p * 0.3183099 + float3(0.1, 0.2, 0.3));
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            float ValueNoise(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);

                float n000 = Hash(i + float3(0, 0, 0));
                float n100 = Hash(i + float3(1, 0, 0));
                float n010 = Hash(i + float3(0, 1, 0));
                float n110 = Hash(i + float3(1, 1, 0));
                float n001 = Hash(i + float3(0, 0, 1));
                float n101 = Hash(i + float3(1, 0, 1));
                float n011 = Hash(i + float3(0, 1, 1));
                float n111 = Hash(i + float3(1, 1, 1));

                float x00 = lerp(n000, n100, f.x);
                float x10 = lerp(n010, n110, f.x);
                float x01 = lerp(n001, n101, f.x);
                float x11 = lerp(n011, n111, f.x);

                return lerp(lerp(x00, x10, f.y), lerp(x01, x11, f.y), f.z);
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 p = input.positionOS * _NoiseScale;

                // Tiga oktaf: satu oktaf saja memberi gumpalan besar yang hilang serentak,
                // dan yang dicari justru tepi yang menggerogoti tidak rata.
                float n = ValueNoise(p) * 0.55
                        + ValueNoise(p * 2.13) * 0.30
                        + ValueNoise(p * 4.37) * 0.15;

                // Dari BAWAH ke atas: api menjalar naik, dan benda yang hilang dari ubun-ubun
                // terbaca sebagai tenggelam, bukan terbakar.
                float height = saturate((input.positionOS.y - _HeightMin) /
                                        max(_HeightMax - _HeightMin, 0.001));
                float threshold = _Burn * 1.35 - height * 0.35;

                clip(n - threshold);

                float3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb
                                * _BaseColor.rgb;

                // Pita bara di ambang guntingnya. Ditambahkan, bukan dicampur: bara yang
                // ikut menggelap bersama bayangan berhenti terbaca sebagai sesuatu yang panas.
                float edge = 1.0 - saturate((n - threshold) / max(_EdgeWidth, 0.001));
                float3 ember = _EdgeColor.rgb * edge * edge * 4.0 * step(0.001, _Burn);

                Light main = GetMainLight();
                float ndotl = saturate(dot(normalize(input.normalWS), main.direction));
                float3 lit = albedo * (main.color * ndotl * 0.75 + 0.35);

                return half4(lit + ember, 1);
            }
            ENDHLSL
        }

        // Tanpa pass ini benda yang memakai shader ini BERHENTI MELEMPAR BAYANGAN — dan itu
        // gagal diam-diam: tidak ada error, cuma pemain yang tiba-tiba melayang di atas rumput
        // tanpa jejak. Clip-nya diulang persis supaya bayangan ikut terkikis saat terbakar,
        // bukan bertahan utuh di tanah sementara badannya sudah habis.
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Off

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag

            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _EdgeColor;
                float _Burn;
                float _EdgeWidth;
                float _NoiseScale;
                float _HeightMin;
                float _HeightMax;
            CBUFFER_END

            float3 _LightDirection;
            float3 _LightPosition;

            struct ShadowAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct ShadowVaryings
            {
                float4 positionCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
            };

            float ShadowHash(float3 p)
            {
                p = frac(p * 0.3183099 + float3(0.1, 0.2, 0.3));
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            float ShadowNoise(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);

                float x00 = lerp(ShadowHash(i + float3(0, 0, 0)), ShadowHash(i + float3(1, 0, 0)), f.x);
                float x10 = lerp(ShadowHash(i + float3(0, 1, 0)), ShadowHash(i + float3(1, 1, 0)), f.x);
                float x01 = lerp(ShadowHash(i + float3(0, 0, 1)), ShadowHash(i + float3(1, 0, 1)), f.x);
                float x11 = lerp(ShadowHash(i + float3(0, 1, 1)), ShadowHash(i + float3(1, 1, 1)), f.x);

                return lerp(lerp(x00, x10, f.y), lerp(x01, x11, f.y), f.z);
            }

            ShadowVaryings ShadowVert(ShadowAttributes input)
            {
                ShadowVaryings o;

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirectionWS = _LightDirection;
                #endif

                // ApplyShadowBias mengembalikan POSISI DUNIA yang sudah digeser, bukan clip —
                // memasukkannya langsung ke positionCS adalah cara pass ini gagal kompilasi
                // diam-diam (persis yang pernah terjadi pada shader VAT musuh).
                float4 positionCS = TransformWorldToHClip(
                    ApplyShadowBias(positionWS, normalWS, lightDirectionWS));

                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif

                o.positionCS = positionCS;
                o.positionOS = input.positionOS.xyz;
                return o;
            }

            half4 ShadowFrag(ShadowVaryings input) : SV_Target
            {
                float3 p = input.positionOS * _NoiseScale;

                float n = ShadowNoise(p) * 0.55
                        + ShadowNoise(p * 2.13) * 0.30
                        + ShadowNoise(p * 4.37) * 0.15;

                float height = saturate((input.positionOS.y - _HeightMin) /
                                        max(_HeightMax - _HeightMin, 0.001));

                clip(n - (_Burn * 1.35 - height * 0.35));
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
}
