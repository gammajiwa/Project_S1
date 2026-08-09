// Permukaan cairan untuk bola HP & mana.
//
// Yang membuat bola terbaca sebagai CAIRAN bukan warnanya, melainkan garis atasnya. Bola yang
// terpotong garis lurus terbaca sebagai gelas berisi agar-agar: isinya jelas, tapi tidak ada yang
// memberi tahu bahwa ia bisa tumpah. Garis yang bergoyang pelan menjawab itu dalam satu detik
// tanpa satu pun tulisan.
//
// Goyangannya hanya MENGURANGI, tidak pernah menambah. Image bertipe Filled sudah memotong
// geometrinya di ketinggian isian, jadi tidak ada piksel di atas garis itu untuk digambar —
// gelombang yang mencoba naik akan terpotong rata dan justru terlihat rusak. Permukaannya
// diayun di antara (isian − amplitudo) dan (isian), seluruhnya di dalam geometri yang ada.
//
// Mati sendiri saat penuh: bola penuh tidak punya permukaan yang terlihat, dan menggoyang
// sesuatu yang tidak terlihat cuma membakar GPU.
Shader "Grimoire/VitalsLiquid"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _Fill ("Isian 0..1", Range(0,1)) = 1

        // Kotak UV sprite di dalam atlasnya (xMin, yMin, xMax, yMax). Dikirim dari C# lewat
        // DataUtility.GetOuterUV — tanpa ini, sprite yang dipak ke atlas akan menghitung
        // ketinggian permukaannya memakai koordinat atlas, dan garisnya mendarat di tempat acak.
        _UvRect ("Kotak UV sprite", Vector) = (0,0,1,1)

        _Amp ("Amplitudo (bagian dari tinggi)", Range(0,0.3)) = 0.045
        _Speed ("Kecepatan", Range(0,12)) = 2.4
        _Waves ("Jumlah gelombang", Range(0.5,8)) = 2.3
        _Soft ("Kelembutan tepi", Range(0.001,0.08)) = 0.012

        // Buih tipis di garis permukaan. Tanpa ini goyangannya terbaca sebagai gambar yang
        // bergetar; dengan ini, sebagai permukaan yang punya arah atas.
        _Crest ("Terang di permukaan", Range(0,1)) = 0.35

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15

        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
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
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _MainTex_ST;

            float _Fill;
            float4 _UvRect;
            float _Amp;
            float _Speed;
            float _Waves;
            float _Soft;
            float _Crest;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                OUT.color = v.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                half4 color = (tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd) * IN.color;

                // Koordinat lokal sprite, 0 di dasar bola dan 1 di puncaknya — bukan koordinat
                // atlas, yang bisa berupa potongan sembarang di tengah tekstur besar.
                float2 span = max(_UvRect.zw - _UvRect.xy, float2(1e-5, 1e-5));
                float2 local = (IN.texcoord - _UvRect.xy) / span;

                // Amplitudo dipadamkan di dua ujung. Penuh: tidak ada permukaan yang terlihat.
                // Kosong: menggoyang sisa setetes cuma membuatnya berkedip.
                float amp = _Amp
                          * smoothstep(0.998, 0.965, _Fill)
                          * smoothstep(0.0, 0.05, _Fill);

                // Dua gelombang berbeda frekuensi. Satu sinus saja terbaca sebagai pola, dan
                // pola terbaca sebagai mesin — bukan sebagai air.
                float t = _Time.y * _Speed;
                float w = sin(local.x * _Waves * 6.2831853 + t) * 0.6
                        + sin(local.x * _Waves * 2.7 - t * 1.37 + 1.7) * 0.4;

                // Permukaan diayun DI BAWAH garis potong geometri, tidak pernah di atasnya.
                float surface = _Fill - amp + amp * (w * 0.5 + 0.5);

                float below = smoothstep(surface + _Soft, surface - _Soft, local.y);
                color.a *= below;

                // Pita tipis tepat di permukaan, dicerahkan. Memberi arah "atas" pada cairannya.
                float crest = smoothstep(surface - _Soft * 4.0, surface, local.y) * below;
                color.rgb += crest * _Crest * amp * 22.0;

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif

                return color;
            }
            ENDCG
        }
    }
}
