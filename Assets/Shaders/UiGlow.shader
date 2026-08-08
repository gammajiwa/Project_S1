Shader "Grimoire/UiGlow"
{
    // Cahaya yang MENAMBAH, untuk kanvas.
    //
    // Kenapa shader sendiri dan bukan Bloom milik post-processing: kanvas ini
    // ScreenSpaceOverlay, dan Overlay digambar SESUDAH seluruh rantai post-processing
    // selesai. Bloom URP tidak akan pernah menyentuhnya berapa pun intensitasnya
    // dinaikkan. Menukar kanvasnya ke ScreenSpaceCamera memang membuka jalan itu, tapi
    // ikut menyeret urutan gambar, penskalaan, dan raycast seluruh UI — harga yang jauh
    // terlalu mahal untuk satu mata yang menyala.
    //
    // Yang dikerjakan di sini justru bagian yang terlihat dari bloom: pendar lembut yang
    // MENAMBAH ke apa pun di belakangnya. Tidak ada sprite yang dibutuhkan — pendarnya
    // gradien radial yang dihitung, jadi tidak ada tekstur baru yang harus dibuat dan
    // tidak ada tepi kotak yang bisa ketahuan.
    //
    // Target: Canvas (ScreenSpaceOverlay). Biaya: satu length() + satu smoothstep per piksel.

    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}

        _Color ("Warna pendar", Color) = (1, 0.35, 0.12, 1)

        [Tooltip] _Intensity ("Kepekatan di pusat", Range(0, 4)) = 1.2

        // Dalam pecahan setengah-sisi kotaknya: 1 = pendarnya persis menyentuh tepi.
        _Radius ("Jangkauan", Range(0.05, 1.5)) = 0.85

        // Pangkat kurva peluruhan. Kecil = bola rata bertepi jelas; besar = inti tajam
        // dengan kabut panjang, dan yang kedua itulah yang dibaca mata sebagai CAHAYA
        // alih-alih sebagai lingkaran yang digambar.
        _Falloff ("Ketajaman inti", Range(1, 8)) = 2.6

        [HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil ("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
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
        ColorMask [_ColorMask]

        // MENAMBAH, bukan menutupi. Alpha-blend biasa cuma menempelkan lingkaran berwarna
        // di depan gambar; yang membuat sesuatu terbaca menyala adalah warna di bawahnya
        // ikut naik.
        Blend SrcAlpha One

        Pass
        {
            Name "UiGlow"

            CGPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #include "UnityCG.cginc"

            fixed4 _Color;
            float _Intensity;
            float _Radius;
            float _Falloff;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                fixed4 color      : COLOR;
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                output.positionCS = UnityObjectToClipPos(input.positionOS);
                output.uv = input.uv;

                // Warna vertex dibawa supaya Image.color tetap jadi knob kedip/redup dari
                // kode tanpa harus membuat material baru per pemakai.
                output.color = input.color;
                return output;
            }

            fixed4 Fragment(Varyings input) : SV_Target
            {
                // Jarak dari pusat kotak, 0 di tengah dan 1 di tepi terdekat.
                float2 d = (input.uv - 0.5) * 2.0;
                float r = length(d) / max(0.05, _Radius);

                float glow = saturate(1.0 - r);
                glow = pow(glow, _Falloff) * _Intensity;

                fixed4 tint = _Color * input.color;
                return fixed4(tint.rgb, glow * tint.a);
            }
            ENDCG
        }
    }

    Fallback Off
}
