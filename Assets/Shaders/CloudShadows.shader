Shader "Grimoire/CloudShadows"
{
    // Bayangan awan yang bergerak, dihitung seluruhnya di dalam shader.
    //
    // Tidak ada tekstur. Versi sebelumnya membangkitkan derau Perlin ke Texture2D lalu menggeser
    // UV-nya tiap frame, dan cara itu membawa dua kegagalan sekaligus: pergeserannya ditulis ke
    // nama properti yang tidak dimiliki shader-nya (jadi tidak pernah terjadi), dan ukuran ubinnya
    // harus dijaga sejalan dengan pembagi pergeserannya secara manual (jadi polanya menggeser
    // sendiri saat pemain berjalan).
    //
    // Di sini derau dihitung dari POSISI DUNIA fragmennya. Bidangnya boleh mengikuti kamera ke mana
    // pun; polanya tetap menempel di tanah, karena yang dibaca memang koordinat tanahnya. Tidak ada
    // UV yang perlu dikunci, jadi tidak ada UV yang bisa salah dikunci.
    //
    // Waktu masuk lewat _Time bawaan shader, bukan lewat kode C# — yang bergerak jadi bergerak
    // walau tidak ada satu pun skrip yang menyentuhnya tiap frame.

    Properties
    {
        [HDR] _Color ("Tint", Color) = (0.05, 0.07, 0.13, 1)

        [Space(8)]
        _Scale ("Ukuran gumpalan (unit dunia)", Float) = 34
        _Coverage ("Cakupan", Range(0, 1)) = 0.62
        _Softness ("Kelembutan tepi", Range(0.01, 0.8)) = 0.22

        [Space(8)]
        _Direction ("Arah hanyut (xy)", Vector) = (1, 0.35, 0, 0)
        _Speed ("Kecepatan hanyut (unit/detik)", Float) = 2
        _Evolve ("Kecepatan berubah bentuk", Float) = 0.06

        [Space(8)]
        _Warp ("Pelengkungan", Range(0, 1.5)) = 0.45
        _Stretch ("Peregangan (xy)", Vector) = (1, 1, 0, 0)

        [Space(8)]
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 10
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "CloudShadow"
            Tags { "LightMode" = "UniversalForward" }

            Blend [_SrcBlend] [_DstBlend]
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Scale;
                float _Coverage;
                float _Softness;
                float4 _Direction;
                float _Speed;
                float _Evolve;
                float _Warp;
                float4 _Stretch;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                return output;
            }

            // Hash tanpa tekstur. Dipakai sebagai sudut-sudut kisi derau nilai.
            float Hash(float2 p)
            {
                p = frac(p * float2(127.1, 311.7));
                p += dot(p, p + 34.56);
                return frac(p.x * p.y);
            }

            // Derau nilai dengan interpolasi halus. Lebih murah dari Perlin dan, setelah
            // ditumpuk beberapa oktaf, tidak bisa dibedakan pada jarak sejauh ini.
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

            // Empat oktaf. Satu oktaf terbaca sebagai gumpalan lembut tanpa bentuk; yang
            // berikutnya memberi tepi yang bisa dikenali sebagai awan.
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
                UNITY_SETUP_INSTANCE_ID(input);

                float size = max(0.001, _Scale);

                // KOORDINAT TANAH, bukan UV. Ini satu-satunya alasan polanya diam di dunia.
                float2 ground = input.positionWS.xz / size;
                ground *= float2(max(0.001, _Stretch.x), max(0.001, _Stretch.y));

                float2 drift = normalize(_Direction.xy + 1e-5) * (_Time.y * _Speed / size);
                float2 p = ground - drift;

                // Pelengkungan domain: derau yang menggeser derau. Tanpa ini gumpalannya bulat
                // dan seragam, dan keseragaman itulah yang terbaca sebagai pola buatan.
                float2 warp = float2(Fbm(p + 3.7), Fbm(p * 1.13 + 11.9)) - 0.5;
                p += warp * _Warp;

                // Bentuknya juga BERUBAH pelan, bukan cuma bergeser. Awan yang bentuknya tetap
                // sambil melintas terbaca sebagai stiker yang ditarik melewati layar.
                float shape = Fbm(p);
                float slow = Fbm(p * 0.53 - float2(_Time.y * _Evolve, _Time.y * _Evolve * 0.7));
                float value = lerp(shape, slow, 0.35);

                float threshold = 1.0 - _Coverage;
                float alpha = smoothstep(threshold - _Softness, threshold + _Softness, value);

                return float4(_Color.rgb, _Color.a * alpha);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
