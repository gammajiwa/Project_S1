Shader "Grimoire/Gloom"
{
    // Kegelapan yang tumbuh MENJAUHI PEMAIN, dengan tepi bertitik seperti raster komik.
    //
    // Bukan vignette. Vignette milik LAYAR — ia menggelapkan sudut monitor dan tidak tahu apa pun
    // tentang dunia, jadi berjalan ke arah mana pun tidak mengubah apa yang gelap. Ini milik DUNIA:
    // pusatnya pemain, dan tempat yang sama tetap gelap sampai pemain benar-benar mendatanginya.
    //
    // Dipasang berlapis pada ketinggian berbeda. Satu bidang datar selalu terbaca sebagai stiker
    // yang menempel di tanah; dua bidang yang terpisah beberapa unit akan saling berparalaks saat
    // kamera bergerak, dan pergeseran itulah yang dibaca sebagai TEBAL. Itu sebabnya lapisannya
    // dibiarkan berbeda skala dan berbeda arah hanyut — kalau sama, keduanya bergerak seragam dan
    // kembali jadi satu bidang.

    Properties
    {
        [HDR] _Color ("Tint", Color) = (0.01, 0.02, 0.04, 1)

        [Space(8)]
        _Center ("Titik pusat dunia (xz)", Vector) = (0, 0, 0, 0)
        _Inner ("Mulai gelap (unit)", Float) = 13
        _Outer ("Gelap penuh (unit)", Float) = 30

        [Space(8)]
        _Scale ("Ukuran gumpalan (unit)", Float) = 9
        _Churn ("Kecepatan bergolak", Float) = 0.12
        _Drift ("Kecepatan hanyut", Float) = 0.6
        _Warp ("Pelengkungan", Range(0, 1.5)) = 0.8
        _Wobble ("Ketakberaturan garis batas (unit)", Float) = 11

        [Space(8)]
        _DotSize ("Jarak titik (piksel)", Range(2, 40)) = 9
        _DotAngle ("Sudut kisi titik", Range(0, 90)) = 45
        _DotSharpness ("Ketajaman tepi titik", Range(0.5, 4)) = 1.4
        _Smooth ("Titik ← → Halus", Range(0, 1)) = 0
        _Ceiling ("Kepekatan maksimum", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+50"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "Gloom"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _Center;
                float _Inner;
                float _Outer;
                float _Scale;
                float _Churn;
                float _Drift;
                float _Warp;
                float _Wobble;
                float _DotSize;
                float _DotAngle;
                float _DotSharpness;
                float _Smooth;
                float _Ceiling;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                return output;
            }

            float Hash(float2 p)
            {
                p = frac(p * float2(127.1, 311.7));
                p += dot(p, p + 34.56);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 cell = floor(p);
                float2 f = frac(p);
                float2 blend = f * f * (3.0 - 2.0 * f);

                float a = Hash(cell);
                float b = Hash(cell + float2(1, 0));
                float c = Hash(cell + float2(0, 1));
                float d = Hash(cell + float2(1, 1));

                return lerp(lerp(a, b, blend.x), lerp(c, d, blend.x), blend.y);
            }

            float Fbm(float2 p)
            {
                float sum = 0.0;
                float amplitude = 0.5;

                [unroll]
                for (int i = 0; i < 4; i++)
                {
                    sum += ValueNoise(p) * amplitude;
                    p = p * 2.03 + 17.3;
                    amplitude *= 0.5;
                }

                return sum / 0.9375;
            }

            float4 Fragment(Varyings input) : SV_Target
            {
                // JARAK DUNIA dari pemain, bukan jarak dari tengah layar. Ini seluruh bedanya dari
                // vignette: yang gelap adalah TEMPAT, dan tempat itu tetap gelap sampai didatangi.
                float2 ground = input.positionWS.xz;
                float away = distance(ground, _Center.xy);

                // Dipotong lebih awal supaya lingkaran bersih di sekitar pemain tidak pernah
                // membayar biaya derau sama sekali.
                if (away < _Inner - _Wobble) return float4(_Color.rgb, 0);

                // Derau dibaca dari KOORDINAT DUNIA. Itu yang membuatnya terbaca sebagai TEMPAT
                // yang gelap alih-alih sebagai lensa: lekuk yang sama tetap di petak tanah yang
                // sama saat pemain berjalan melewatinya.
                float2 p = ground / max(0.001, _Scale);

                float2 churn = float2(_Time.y * _Churn, _Time.y * _Churn * 0.83);
                float2 warp = float2(Fbm(p + churn), Fbm(p * 1.17 - churn * 1.31)) - 0.5;

                p += warp * _Warp;
                p -= float2(_Time.y * _Drift, _Time.y * _Drift * 0.4) / max(0.001, _Scale);

                float value = Fbm(p) - 0.5;

                // Deraunya menggoyang JARAK AMBANGNYA, bukan kepekatannya — dan ini seluruh bedanya
                // dari percobaan sebelumnya.
                //
                // Mengalikan kepekatan dengan derau cuma membuat lingkaran yang sama jadi berbintik:
                // batasnya tetap lingkaran sempurna, cuma isinya belang. Menggeser AMBANG-nya
                // membuat garis batasnya sendiri berkelok masuk-keluar sejauh belasan unit, dan di
                // situlah ia berhenti terbaca sebagai vignette.
                float wobble = value * _Wobble;
                float inner = _Inner + wobble;
                float outer = max(inner + 0.5, _Outer + wobble);

                float gloom = saturate(smoothstep(inner, outer, away) * _Ceiling);
                if (gloom <= 0.0001) return float4(_Color.rgb, 0);

                // ---- titik halftone ala cetakan komik ----
                //
                // Kisinya dibaca dari KOORDINAT PIKSEL, bukan dari koordinat dunia. Titik cetak
                // berukuran tetap di atas kertas; kisi yang mengikuti dunia akan membesar-mengecil
                // mengikuti zoom, dan yang tersisa bukan raster komik melainkan tekstur bintik.
                float radians = _DotAngle * 0.017453292;
                float s = sin(radians);
                float c = cos(radians);

                float2 pixel = input.positionCS.xy;
                float2 grid = float2(pixel.x * c - pixel.y * s, pixel.x * s + pixel.y * c);
                grid /= max(2.0, _DotSize);

                float2 f = frac(grid) - 0.5;
                float toDot = length(f);

                // Jari-jarinya AKAR dari kepekatan, bukan kepekatan itu sendiri. Yang dibaca mata
                // adalah LUAS titiknya, dan luas tumbuh sebagai kuadrat jari-jari — memetakannya
                // lurus membuat separuh gelap terlihat seperti seperempat gelap.
                float radius = sqrt(gloom) * 0.72;

                float aa = fwidth(toDot) * _DotSharpness;
                float dots = smoothstep(radius + aa, radius - aa, toDot);

                float alpha = lerp(dots, gloom, saturate(_Smooth));

                return float4(_Color.rgb, saturate(_Color.a * alpha));
            }
            ENDHLSL
        }
    }

    Fallback Off
}
