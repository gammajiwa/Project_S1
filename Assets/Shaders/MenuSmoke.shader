// Asap gelap yang bergumpal di sekeliling grimoire.
//
// Permintaan pemilik project (2026-08-12): "shader kaya asap atau api di sekitar tapi warna
// hitam terus banyak". Kelihatan berlawanan — menambah HITAM ke latar yang dikeluhkan terlalu
// gelap — dan justru di situ intinya: yang bikin latar terasa kosong bukan gelapnya, melainkan
// gelap yang RATA. Hitam tanpa bentuk tidak punya jarak, tidak punya gerak, dan mata tidak
// menemukan apa pun di dalamnya. Hitam yang BERGUMPAL punya keduanya.
//
// Karena itu asap ini dimaksudkan digambar di depan cahaya, bukan di depan kekosongan: yang
// terlihat adalah siluetnya menutup dan membuka cahaya di belakangnya. Ditaruh di atas latar
// yang benar-benar hitam, ia memang tidak akan terlihat — dan itu bukan bug, itu definisi.
//
// Derau dihitung di dalam shader, tanpa tekstur: tiga oktaf value-noise. Alasannya sama dengan
// Grimoire/CloudShadows — pola dari tekstur menuntut UV dikunci ke sesuatu, dan tiap penguncian
// adalah satu cara baru untuk salah.
//
// Target: URP Forward, PC + Mobile. Satu pass, nol sampel tekstur, tanpa cabang dinamis.
Shader "Grimoire/MenuSmoke"
{
    Properties
    {
        [HDR] _Color ("Warna asap", Color) = (0, 0, 0, 1)

        [Space(8)]
        _Density ("Kepekatan", Range(0, 3)) = 1.2
        _Coverage ("Cakupan", Range(0, 1)) = 0.52
        _Softness ("Kelembutan tepi", Range(0.01, 1)) = 0.35

        [Space(8)]
        _Scale ("Ukuran gumpalan", Range(0.2, 8)) = 1.6

        // Ke mana asapnya hanyut, dalam satuan UV per detik. Y positif = naik.
        _Drift ("Arah hanyut (xy)", Vector) = (0.02, 0.06, 0, 0)

        // Seberapa cepat bentuknya BERUBAH, terpisah dari seberapa cepat ia berpindah.
        // Dibiarkan jauh lebih kecil dari hanyutnya: bentuk yang berubah lebih cepat daripada
        // ia bergeser terbaca sebagai noda yang mendidih, bukan sebagai asap yang lewat.
        _Evolve ("Kecepatan berubah bentuk", Range(0, 1)) = 0.05

        [Space(8)]
        // 0 = asap (gumpalan bulat, hanyut apa adanya). 1 = api (dijulurkan ke atas dan
        // tepinya ditajamkan, jadi lidahnya terbaca menjilat bukan mengepul).
        _Flame ("Asap <-> Api", Range(0, 1)) = 0

        [Space(8)]
        // Bidangnya persegi, asapnya tidak boleh. Tanpa topeng ini tepi quad-nya terlihat
        // sebagai garis lurus, dan satu garis lurus cukup untuk membongkar seluruh efeknya.
        _Mask ("Kelembutan topeng tepi", Range(0.01, 1)) = 0.45
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+20"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Density;
                float _Coverage;
                float _Softness;
                float _Scale;
                float4 _Drift;
                float _Evolve;
                float _Flame;
                float _Mask;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

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

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);

                // Smoothstep pada pecahannya, bukan interpolasi lurus: yang lurus meninggalkan
                // kisi-kisi lurus yang terlihat jelas begitu deraunya dilapis.
                f = f * f * (3.0 - 2.0 * f);

                float a = Hash(i);
                float b = Hash(i + float2(1, 0));
                float c = Hash(i + float2(0, 1));
                float d = Hash(i + float2(1, 1));

                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float Fbm(float2 p)
            {
                float sum = 0.0;
                float amp = 0.5;

                [unroll]
                for (int i = 0; i < 3; i++)
                {
                    sum += ValueNoise(p) * amp;
                    p *= 2.03;   // bukan tepat 2: kelipatan bulat membuat tiap oktaf sejajar
                    amp *= 0.5;
                }

                return sum;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;

                // Mode api menjulurkan koordinatnya secara vertikal SEBELUM derau dibaca, jadi
                // gumpalannya lahir sudah memanjang ke atas alih-alih dipanjangkan belakangan.
                uv.y /= lerp(1.0, 2.4, _Flame);

                float2 p = uv * _Scale + _Drift.xy * _Time.y;
                float n = Fbm(p + Fbm(p + _Time.y * _Evolve));

                // Ambangnya digeser cakupan, bukan dikalikan: mengalikan cuma memucatkan
                // seluruh bidang, sedangkan menggeser ambang benar-benar mengubah berapa banyak
                // ruang yang berisi asap dan berapa yang kosong.
                float body = smoothstep(1.0 - _Coverage - _Softness, 1.0 - _Coverage + _Softness, n);

                // Api menajam ke arah ujungnya; asap dibiarkan tumpul.
                body = pow(body, lerp(1.0, 1.8, _Flame));

                // Topeng bulat supaya tepi quad-nya tidak pernah jadi garis.
                float r = length(input.uv);
                float mask = 1.0 - smoothstep(1.0 - _Mask, 1.0, r);

                // Api meredup ke bawah — lidahnya menyala di atas dan lenyap di pangkal.
                float rise = lerp(1.0, saturate(0.5 + uv.y * 0.5), _Flame);

                float a = saturate(body * mask * rise * _Density) * _Color.a;
                return half4(_Color.rgb, a);
            }
            ENDHLSL
        }
    }
}
