// Gambar UI yang MENAMBAH cahaya, bukan menutupi.
//
// Ada untuk satu alasan yang sangat konkret: tekstur efek sihir hampir selalu digambar terang
// di atas latar HITAM PEKAT, bukan di atas alpha kosong. Dipasang ke Image biasa, yang tampil
// bukan cincin runenya melainkan kotak hitam seukuran rect-nya, dengan cincin di dalamnya.
//
// Blend One One membuang hitamnya secara harfiah: nol yang ditambahkan tidak mengubah apa pun,
// jadi latar teksturnya menghilang tanpa perlu ada kanal alpha sama sekali.
//
// Target: URP, Canvas Overlay maupun Camera. Tanpa dukungan Mask — yang memakainya layar
// loading, dan layar loading tidak pernah berada di dalam mask.
Shader "Grimoire/UiAdditive"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)

        // Diperlukan supaya komponen Image tidak mengeluh soal properti yang hilang saat Unity
        // menyalin setelan UI bawaannya ke material ini.
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend One One
        ColorMask [_ColorMask]

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Color;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.uv = TRANSFORM_TEX(input.uv, _MainTex);
                o.color = input.color * _Color;
                return o;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);

                // Alpha vertex dipakai sebagai peredup, bukan sebagai transparansi: pada blending
                // aditif tidak ada yang "tembus pandang", yang ada cuma menambah lebih sedikit.
                // Itu yang membuat fade in/out tetap bekerja lewat CanvasGroup biasa.
                return half4(tex.rgb * input.color.rgb * input.color.a, 1.0);
            }
            ENDHLSL
        }
    }
}
