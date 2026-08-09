Shader "Grimoire/EnemyVat"
{
    // Animasi yang dibaca dari TEKSTUR, bukan dari tulang.
    //
    // Musuh di game ini tidak punya GameObject — seluruh gerombolan keluar sebagai beberapa
    // panggilan instanced. Itu menutup pintu untuk Animator dan SkinnedMeshRenderer sekaligus,
    // karena keduanya menuntut satu objek per musuh. Di sini posisi tiap vertex untuk tiap
    // frame sudah dipanggang ke tekstur (lihat VatBaker), dan vertex shader tinggal
    // memindahkannya. Lima ratus musuh membayar nol skinning CPU dan nol Animator.
    //
    // Per instance yang dikirim cuma SATU float4: baris awal klipnya, panjang klipnya, dan
    // sudah sejauh mana ia berjalan. Matriksnya tetap matriks biasa, batchingnya tetap
    // batching yang sama.
    //
    // Pencahayaan sengaja Lambert sederhana, bukan PBR penuh: musuhnya setinggi belasan
    // piksel di kamera yang menunduk dari 18 unit, dan tidak ada satu pun sorotan spekular
    // di ukuran itu yang akan pernah terlihat oleh siapa pun.

    Properties
    {
        [MainTexture] _BaseMap ("Warna", 2D) = "white" {}
        [MainColor]   _BaseColor ("Pewarna", Color) = (1, 1, 1, 1)

        [NoScaleOffset] _VatTex ("Posisi panggangan", 2D) = "black" {}
        _VatRows ("Total baris tekstur", Float) = 1

        _Ambient ("Cahaya dasar", Range(0, 1)) = 0.35
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);      SAMPLER(sampler_BaseMap);
            TEXTURE2D(_VatTex);       SAMPLER(sampler_VatTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float  _VatRows;
                float  _Ambient;
            CBUFFER_END

            // Satu-satunya data per instance. x = baris awal klip, y = jumlah baris klip,
            // z = maju 0..1 di dalam klip itu.
            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _VatClip)
            UNITY_INSTANCING_BUFFER_END(Props)

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                float2 vatUv      : TEXCOORD2;   // x = kolom vertex ini, diisi VatBaker
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            // tex2Dlod, bukan sampling biasa: di vertex shader tidak ada turunan layar yang
            // bisa dipakai memilih mipmap, dan memintanya menghitung sendiri di sana adalah
            // cara termudah membuat seluruh model berkedut.
            float3 SampleVat(float column, float row)
            {
                float2 uv = float2(column, (row + 0.5) / max(1.0, _VatRows));
                return SAMPLE_TEXTURE2D_LOD(_VatTex, sampler_VatTex, uv, 0).xyz;
            }

            Varyings Vertex(Attributes input)
            {
                Varyings output = (Varyings)0;

                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float4 clip = UNITY_ACCESS_INSTANCED_PROP(Props, _VatClip);

                float3 posOS = input.positionOS.xyz;

                if (clip.y >= 1.0)
                {
                    float frames = clip.y;
                    float f = frac(clip.z) * frames;

                    float f0 = floor(f);

                    // Frame berikutnya DIPUTAR BALIK ke awal klip, bukan dibiarkan menyeberang
                    // ke baris sesudahnya. Baris sesudah klip ini milik klip LAIN, dan
                    // mencampur pose diam dengan pose lari di frame sambungan membuat modelnya
                    // meledak sekejap tiap kali animasinya mengulang.
                    float f1 = f0 + 1.0 >= frames ? 0.0 : f0 + 1.0;

                    float3 a = SampleVat(input.vatUv.x, clip.x + f0);
                    float3 b = SampleVat(input.vatUv.x, clip.x + f1);

                    posOS = lerp(a, b, f - f0);
                }

                // Normal dibiarkan normal pose netral. Yang benar adalah ikut memanggangnya,
                // tapi itu dua kali ukuran tekstur untuk perbedaan yang tidak akan pernah
                // terbaca pada musuh setinggi belasan piksel. Kalau nanti ada yang berdiri
                // dekat kamera, di sinilah tempatnya dibayar.
                VertexNormalInputs normals = GetVertexNormalInputs(input.normalOS);

                output.positionWS = TransformObjectToWorld(posOS);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = normals.normalWS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;

                Light main = GetMainLight();
                float3 n = normalize(input.normalWS);

                // Setengah-Lambert: gelap penuh di sisi membelakangi cahaya membuat musuh
                // menghilang sama sekali di kamera atas saat matahari rendah.
                float ndl = saturate(dot(n, main.direction)) * 0.5 + 0.5;

                float3 lit = albedo.rgb * (main.color * ndl + _Ambient);
                return half4(lit, albedo.a);
            }
            ENDHLSL
        }

        // Bayangan WAJIB ikut dipindahkan vertexnya. Pass bayangan yang memakai pose netral
        // akan menjatuhkan bayangan sosok berdiri diam di bawah musuh yang sedang berlari —
        // dan bayangan yang tidak sinkron lebih mengganggu daripada tidak ada bayangan.
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex ShadowVertex
            #pragma fragment ShadowFragment
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            TEXTURE2D(_VatTex);   SAMPLER(sampler_VatTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float  _VatRows;
                float  _Ambient;
            CBUFFER_END

            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _VatClip)
            UNITY_INSTANCING_BUFFER_END(Props)

            float3 _LightDirection;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 vatUv      : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            float3 SampleVatShadow(float column, float row)
            {
                float2 uv = float2(column, (row + 0.5) / max(1.0, _VatRows));
                return SAMPLE_TEXTURE2D_LOD(_VatTex, sampler_VatTex, uv, 0).xyz;
            }

            Varyings ShadowVertex(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);

                float4 clip = UNITY_ACCESS_INSTANCED_PROP(Props, _VatClip);
                float3 posOS = input.positionOS.xyz;

                if (clip.y >= 1.0)
                {
                    float frames = clip.y;
                    float f = frac(clip.z) * frames;
                    float f0 = floor(f);
                    float f1 = f0 + 1.0 >= frames ? 0.0 : f0 + 1.0;

                    posOS = lerp(SampleVatShadow(input.vatUv.x, clip.x + f0),
                                 SampleVatShadow(input.vatUv.x, clip.x + f1), f - f0);
                }

                float3 positionWS = TransformObjectToWorld(posOS);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

                output.positionCS = ApplyShadowBias(positionWS, normalWS, _LightDirection);

            #if UNITY_REVERSED_Z
                output.positionCS.z = min(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #else
                output.positionCS.z = max(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #endif

                return output;
            }

            half4 ShadowFragment(Varyings input) : SV_Target => 0;
            ENDHLSL
        }
    }

    Fallback Off
}
