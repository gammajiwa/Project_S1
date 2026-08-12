// Bintik cahaya buram yang berkelip di latar menu — bokeh.
//
// Permintaan pemilik project (2026-08-12): "kasih efek bokeh, terus di belakangnya kaya ada
// cahaya kelap-kelip gitu, jangan terlalu kuat, cuman biar gak kosong".
//
// Dikerjakan shader, bukan particle system, dan itu keputusan: yang diminta puluhan titik yang
// masing-masing berkelip dengan iramanya sendiri. Sebagai partikel itu berarti puluhan quad,
// masing-masing dengan kurva warna sendiri, dan tidak satu pun bisa diatur tanpa membuka
// modulnya satu per satu. Di sini seluruh bidangnya SATU quad dan satu draw call, dan setiap
// titik mengambil ukuran, letak, serta fase kelipnya dari hash koordinat selnya sendiri.
//
// Tiga lapis dengan kerapatan berbeda, hanyut dengan laju berbeda. Itu yang memberi kedalaman:
// lapis yang rapat bergerak lebih pelan (jauh), yang jarang lebih cepat (dekat), dan mata
// membaca selisihnya sebagai jarak.
//
// Bokeh sungguhan bukan cakram rata — ia punya TEPI yang sedikit lebih terang dari tengahnya,
// karena bukaan lensa memusatkan cahaya di pinggir lingkaran keburamannya. Tanpa tepi itu yang
// tampil cuma titik lembut, dan titik lembut terbaca sebagai debu, bukan sebagai cahaya di luar
// fokus. Itu yang dikerjakan _Rim.
//
// Target: URP Forward, PC + Mobile. Satu pass, nol sampel tekstur, tanpa cabang dinamis.
Shader "Grimoire/MenuBokeh"
{
    Properties
    {
        [HDR] _Color ("Warna A", Color) = (0.62, 0.45, 1, 1)
        [HDR] _ColorB ("Warna B", Color) = (1, 0.78, 0.5, 1)

        // Seberapa sering sebuah titik memilih warna B alih-alih A. Diundi PER SEL, jadi yang
        // berubah komposisi lapangannya — bukan tiap titik jadi warna campuran yang sama, yang
        // justru menghasilkan satu warna baru dan tetap seragam.
        _Variety ("Sebaran warna A <-> B", Range(0, 1)) = 0.5

        [Space(8)]
        _Brightness ("Kecerahan", Range(0, 3)) = 0.5
        _Size ("Ukuran bintik", Range(0.02, 0.5)) = 0.17
        _Softness ("Kelembutan tepi", Range(0.05, 1)) = 0.55
        _Rim ("Penguatan tepi bokeh", Range(0, 2)) = 0.7

        [Space(8)]
        _Cells ("Kerapatan (sel per bidang)", Range(2, 40)) = 9

        [Space(8)]
        _Twinkle ("Kedalaman kelip", Range(0, 1)) = 0.65
        _TwinkleSpeed ("Kecepatan kelip (siklus/dtk)", Range(0, 2)) = 0.28

        [Space(8)]
        _Drift ("Kecepatan hanyut", Range(0, 0.2)) = 0.012

        // Bidangnya persegi, sebaran cahayanya tidak boleh. Tanpa topeng ini tepi quad-nya
        // memotong barisan titik dengan garis lurus, dan satu garis lurus cukup untuk
        // membongkar seluruh efeknya.
        _Mask ("Kelembutan topeng tepi", Range(0.01, 1)) = 0.6
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent-90"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _ColorB;
                float _Variety;
                float _Brightness;
                float _Size;
                float _Softness;
                float _Rim;
                float _Cells;
                float _Twinkle;
                float _TwinkleSpeed;
                float _Drift;
                float _Mask;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.uv = input.positionOS.xy * 2.0;
                return o;
            }

            float Hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            float2 Hash2(float2 p)
            {
                return float2(Hash(p), Hash(p + 19.19));
            }

            /// Satu lapis titik. cells = kerapatan, speed = laju hanyut, seed = supaya dua lapis
            /// dengan kerapatan sama tidak menghasilkan pola yang sama persis.
            float3 Layer(float2 uv, float cells, float speed, float seed)
            {
                float2 p = uv * cells + float2(seed, -_Time.y * speed);
                float2 id = floor(p);
                float2 f = frac(p) - 0.5;

                float2 jitter = (Hash2(id + seed) - 0.5) * 0.72;
                float r = length(f - jitter);

                // Ukuran diacak per sel. Bokeh dari bidang kedalaman yang sama tetap berbeda
                // besarnya karena sumbernya berbeda jarak; ukuran seragam terbaca sebagai kisi.
                float size = _Size * lerp(0.45, 1.0, Hash(id + seed + 7.3));

                // Gaussian, bukan smoothstep. smoothstep SELALU punya tepi — di jarak `size`
                // nilainya jatuh ke nol persis, dan mata menemukan lingkaran batas itu berapa
                // pun lembutnya transisi menuju ke sana. Gaussian tidak pernah benar-benar
                // mencapai nol: ekornya meluruh terus melewati `size`, jadi bulatannya saling
                // tumpang tindih dan tidak satu pun punya keliling. Itu yang dibaca sebagai
                // di luar fokus.
                float x = r / max(0.0001, size);
                float disc = exp(-x * x * lerp(9.0, 1.8, _Softness));

                // Cincin tipis tepat di dalam tepinya — inilah yang membedakan bokeh dari titik.
                float rim = smoothstep(size * 0.68, size * 0.93, r) *
                            (1.0 - smoothstep(size * 0.93, size * 1.02, r));

                // Tiap sel punya fase sendiri, jadi tidak ada dua titik yang berkedip bersamaan.
                float phase = Hash(id + seed + 3.7) * 6.2831853;
                float twinkle = 0.5 + 0.5 * sin(_Time.y * _TwinkleSpeed * 6.2831853 + phase);

                float value = (disc + rim * _Rim) * lerp(1.0 - _Twinkle, 1.0, twinkle);

                // Warna diundi per sel, bukan dicampur rata. Satu titik utuh warna A, tetangganya
                // utuh warna B — itu yang terbaca sebagai lapangan cahaya yang beragam. Mencampur
                // keduanya di tiap titik cuma melahirkan satu warna ketiga yang sama seragamnya.
                float pick = step(1.0 - _Variety, Hash(id + seed + 51.7));
                float3 tint = lerp(_Color.rgb, _ColorB.rgb, pick);

                return tint * value;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;

                // Yang rapat hanyut paling pelan: itu yang jauh.
                float3 sum = Layer(uv, _Cells * 1.9, _Drift * 0.45, 0.0) * 0.45
                           + Layer(uv, _Cells,        _Drift * 0.85, 11.3) * 0.75
                           + Layer(uv, _Cells * 0.55, _Drift * 1.4,  27.9) * 1.0;

                float r = length(input.uv);
                float mask = 1.0 - smoothstep(1.0 - _Mask, 1.0, r);

                return half4(sum * (mask * _Brightness * _Color.a), 1.0);
            }
            ENDHLSL
        }
    }
}
