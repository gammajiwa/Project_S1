// Bekas gosong di tanah — noda hangus yang ditinggalkan semburan api naga.
//
// Bentuknya digambar MATEMATIS di shader, bukan dari tekstur: lingkaran gelap bertepi
// compang-camping, digoyang dua gelombang sinus per sudut dengan seed per-noda. Tekstur
// akan lebih fleksibel dan tidak dibutuhkan — noda hangus memang cuma butuh "gelap,
// bundar, tepinya tidak rata", dan tiga hal itu lebih murah dihitung daripada dimuat.
//
// Instanced karena penggambarnya RenderMeshInstanced: ratusan noda = satu draw call.
// _Fade per-instance dipakai untuk memudar seiring umur tanpa memecah batch.
Shader "Grimoire/Scorch"
{
    Properties
    {
        _CenterColor ("Warna tengah", Color) = (0.05, 0.04, 0.035, 1)
        _EdgeColor ("Warna tepi", Color) = (0.16, 0.10, 0.06, 1)
        _MaxAlpha ("Kepekatan maksimum", Range(0, 1)) = 0.82
    }

    SubShader
    {
        // Di atas tanah, di bawah semua transparan lain (api, asap, UI dunia).
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-500" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _CenterColor;
                half4 _EdgeColor;
                half  _MaxAlpha;
            CBUFFER_END

            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float, _Fade)
                UNITY_DEFINE_INSTANCED_PROP(float, _Seed)
            UNITY_INSTANCING_BUFFER_END(Props)

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes input)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.uv = input.uv;
                return o;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float fade = UNITY_ACCESS_INSTANCED_PROP(Props, _Fade);
                float seed = UNITY_ACCESS_INSTANCED_PROP(Props, _Seed);

                float2 p = input.uv * 2.0 - 1.0;
                float r = length(p);
                float ang = atan2(p.y, p.x);

                // Tepi compang-camping: dua frekuensi supaya tidak terbaca sebagai bunga
                // yang rapi. Seed menggeser fasenya per noda — dua noda bersebelahan tidak
                // boleh identik, karena barisan cap yang sama terbaca sebagai stempel.
                float edge = 0.78
                           + 0.13 * sin(ang * 5.0 + seed)
                           + 0.07 * sin(ang * 11.0 + seed * 2.7);

                float body = saturate((edge - r) / 0.18);

                // Tengah paling hangus, tepi cokelat terbakar — gradasi kecil yang membuat
                // nodanya terbaca sebagai BEKAS PANAS, bukan sebagai lubang hitam di tanah.
                half3 col = lerp(_CenterColor.rgb, _EdgeColor.rgb, smoothstep(0.15, 0.75, r));

                half alpha = body * _MaxAlpha * saturate(fade);
                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
}
