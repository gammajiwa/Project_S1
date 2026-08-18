// Pohon (batang, tajuk, pohon mesh) yang berdiri DI DEPAN pemain MEMUDAR di sekitar posisi
// layar pemain — pemain tidak pernah hilang di balik hutan.
//
// Kenapa shader, bukan ganti material per pohon: pohon digambar lewat PropBatch
// (RenderMeshInstanced, ribuan instance, tanpa GameObject per pohon), jadi tidak ada renderer
// individual yang bisa dipudarkan. Pudarnya harus diputuskan per PIKSEL.
//
// ALPHA, BUKAN DITHER. Versi pertama shader ini melubangi diri dengan pola dither Bayer —
// clip() per piksel di antrean Opaque. Itu ditolak pemilik project ("gak pernah bener"):
// lubang dither tidak pernah terbaca sebagai kaca, melainkan sebagai taburan bintik yang
// bercampur warna tanah, dan tepinya berkerlip tiap kamera bergerak satu piksel. Sekarang
// pohonnya benar-benar TEMBUS: antrean Transparent-1, blending alpha biasa, dan gradasinya
// mulus dari pejal di tepi sampai bayangan tipis di pusat.
//
// ANTREANNYA MASIH PEJAL (Geometry+400 = 2400), bukan Transparent — dan itu keputusan yang
// paling mudah salah di seluruh berkas ini.
//
// URP menganggap apa pun di bawah 2500 sebagai pejal. Tinggal DI SITU berarti:
//   - lantai, rumput, pemain, dan musuh (semua antrean 2000) sudah tergambar saat pohon
//     menyusul, jadi campuran alpha-nya bercampur dengan yang benar;
//   - pohon IKUT masuk `_CameraDepthTexture`, yang disalin URP sesudah pass pejal. Ini
//     alasan sebenarnya. PC_Renderer memasang HAZE (kabut volumetrik) dan ia membaca
//     kedalaman scene; pohon yang pindah ke antrean transparan hilang dari peta itu, dan
//     kabutnya lalu dihitung memakai kedalaman TANAH DI BELAKANG pohon — pohon dekat pun
//     ikut diselimuti kabut sejauh latarnya. SSAO ikut kehilangan mereka;
//   - VFX (3000) tetap tergambar sesudahnya, dan karena pohon menulis kedalaman, sihir yang
//     berada di balik pohon tetap tertutup dengan benar.
//
// ZWrite tetap ON walau berbaur: pohon yang lebih jauh ditolak kedalaman oleh pohon yang
// lebih dekat, jadi tumpukan pohon tidak pernah berbaur dua kali jadi gumpalan gelap.
//
// Harganya: piksel pohon yang memudar TEPAT di atas langit akan bercampur dengan warna
// bersih kamera, bukan dengan langit (skybox digambar sesudah pejal). Di sini itu tidak
// pernah terjadi — kameranya ortografis menunduk dan pudarnya cuma terjadi di lingkaran
// sekitar pemain, yaitu tengah layar, yang selalu tanah.
//
// Datanya global, diisi SeeThroughFeeder tiap frame:
//   _SeeThroughData     xy = posisi viewport pemain, z = kedalaman mata pemain,
//                       w  = jari-jari lubang (porsi tinggi layar)
//   _SeeThroughStrength 0 = mati, 1 = pudar penuh sampai _SeeThroughMin
//   _SeeThroughMin      sisa keburaman di PUSAT lubang. 0 = hilang sama sekali; di atas nol
//                       pohonnya jadi bayangan tipis — masih terbaca sebagai pohon.
//
// Bayangan TIDAK ikut memudar — pass ShadowCaster dibiarkan pejal, supaya cahaya tidak bocor
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

        // Dua properti ini TIDAK dipakai shader ini sendiri — ia menampungnya supaya nilai
        // milik material paket pihak ketiga selamat saat shadernya ditukar di PropBatch.
        // Kartu daun paket aset memakai potong-alpha dan sering dua sisi; tanpa keduanya
        // daunnya berubah jadi kartu pejal satu sisi begitu pohon mesh ikut tembus pandang.
        _Cutoff("Alpha Cutoff", Range(0, 1)) = 0.5
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 2
    }

    SubShader
    {
        // Geometry+400 (2400): paling belakang di antrean PEJAL. Lihat catatan panjang di atas
        // — angka ini yang menjaga pohon tetap ada di peta kedalaman yang dibaca HAZE & SSAO.
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry+400"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite On
            ZTest LEqual
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            // Set keyword MENGIKUTI SimpleLit paket URP terpasang. _CLUSTER_LIGHT_LOOP wajib:
            // proyek ini jalan di Forward+, dan tanpa keyword itu pembacaan data cahaya kacau —
            // pohonnya tampil hitam pekat, persis bug pertama shader ini.
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            // multi_compile, bukan shader_feature: materialnya dirakit SAAT JALAN (salinan
            // material paket yang shadernya ditukar), jadi tidak ada satu pun aset .mat yang
            // bisa dibaca pemangkas varian saat build. shader_feature akan terpangkas habis
            // dan kartu daun kehilangan potongannya justru di build, bukan di editor.
            #pragma multi_compile _ _ALPHATEST_ON

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
                half _Cutoff;
                half _Cull;
            CBUFFER_END

            float4 _SeeThroughData;
            float _SeeThroughStrength;
            float _SeeThroughMin;

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

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);

                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * _BaseColor;

                // Potongan daun dulu, sebelum apa pun yang lain. Hanya kalau materialnya
                // memang memintanya — alpha peta warna milik batang & batu sering berisi
                // data lain, dan memotong dengannya tanpa diminta membuat propnya lenyap.
                #ifdef _ALPHATEST_ON
                clip(albedo.a - _Cutoff);
                #endif

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

                // Gradasinya LEBAR sekarang (35%..100% jari-jari), kebalikan dari versi dither.
                // Cincin tipis dulu wajib karena dither yang melebar jadi bintik; alpha justru
                // sebaliknya — peralihan pendek terbaca sebagai lubang bertepi keras yang
                // ikut bergerak bersama pemain, dan itu yang mencuri perhatian dari lapangan.
                half hole = 1.0 - smoothstep(_SeeThroughData.w * 0.35, _SeeThroughData.w, dist);

                // 0,4 unit toleransi: piksel yang sebidang dengan pemain bukan penghalang,
                // dan tanpa jeda ini pohon yang berdiri PERSIS di garis pemain berkedip
                // antara pejal dan tembus tiap langkah.
                bool inFront = i.viewZ < _SeeThroughData.z - 0.4;
                half fade = inFront ? hole * _SeeThroughStrength : 0.0;

                // Pusatnya menyisakan _SeeThroughMin, bukan nol. Pohon yang HILANG total
                // membuat hutannya berlubang saat pemain lewat; bayangan tipis tetap
                // membaca sebagai pohon yang sedang dilewati.
                half alpha = lerp(1.0, saturate(_SeeThroughMin), saturate(fade));

                // Pencahayaan lewat jalur resmi URP, bukan Lambert rakitan — cara termudah
                // agar hasilnya identik dengan URP/Lit di Forward+ maupun Forward biasa
                // (bayangan utama, lampu arena, lampu pemain, occlusion layar).
                InputData inputData = (InputData)0;
                inputData.positionWS = i.positionWS;
                inputData.normalWS = normalize(i.normalWS);
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(i.positionWS);
                inputData.shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                inputData.fogCoord = i.fogFactor;
                inputData.bakedGI = SampleSH(inputData.normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                inputData.shadowMask = half4(1, 1, 1, 1);

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = albedo.rgb;
                surfaceData.alpha = alpha;
                surfaceData.metallic = _Metallic;
                surfaceData.smoothness = _Smoothness;
                surfaceData.occlusion = 1;

                half4 color = UniversalFragmentPBR(inputData, surfaceData);

                // Kabutnya dicampur ke RGB saja. MixFog versi transparan tidak boleh menyentuh
                // alpha: pohon jauh yang ikut kehilangan alpha karena kabut akan menampakkan
                // langit lewat badannya sendiri.
                color.rgb = MixFog(color.rgb, i.fogFactor);
                color.a = alpha;
                return color;
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
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex shadowVert
            #pragma fragment shadowFrag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _ALPHATEST_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _Color;
                half _Smoothness;
                half _Metallic;
                half _Cutoff;
                half _Cull;
            CBUFFER_END

            float3 _LightDirection;

            struct ShadowAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct ShadowVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            ShadowVaryings shadowVert(ShadowAttributes v)
            {
                UNITY_SETUP_INSTANCE_ID(v);

                ShadowVaryings o;
                float3 positionWS = TransformObjectToWorld(v.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);

                #if UNITY_REVERSED_Z
                o.positionCS.z = min(o.positionCS.z, o.positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #else
                o.positionCS.z = max(o.positionCS.z, o.positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #endif
                return o;
            }

            half4 shadowFrag(ShadowVaryings i) : SV_Target
            {
                // Bayangan tetap PEJAL — tembus pandang cuma urusan mata kamera. Yang ikut
                // hanyalah potongan daun: kartu daun yang membayang sebagai kotak penuh
                // membuat bayangan pohon jadi papan iklan.
                #ifdef _ALPHATEST_ON
                clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).a * _BaseColor.a - _Cutoff);
                #endif
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex depthVert
            #pragma fragment depthFrag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _ALPHATEST_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _Color;
                half _Smoothness;
                half _Metallic;
                half _Cutoff;
                half _Cull;
            CBUFFER_END

            struct DepthAttributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct DepthVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            DepthVaryings depthVert(DepthAttributes v)
            {
                UNITY_SETUP_INSTANCE_ID(v);

                DepthVaryings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                return o;
            }

            half4 depthFrag(DepthVaryings i) : SV_Target
            {
                #ifdef _ALPHATEST_ON
                clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).a * _BaseColor.a - _Cutoff);
                #endif
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
