// Ritual REVEAL evolusi ("pertama jadi putih dulu, sudah itu di tengah mulai keluar
// gambar yang baru" — permintaan pemilik project): siluet hasil evolusi menyala putih
// polos, lalu warna aslinya TUMBUH dari pusat sampai gambar barunya utuh.
//
// Dipasang di satu Image UGUI berisi art/icon piece hasil evolusi, ditumpangkan di atas
// footprint tempat ia mendarat. Dua kenop yang digerakkan C# (EvoRevealFx):
//   _Flash  — 0..1, seberapa putih SELURUH siluet (fase pertama: naik ke 1).
//   _Reveal — 0..1, radius lingkaran warna asli dari pusat (fase kedua: 0 -> 1,
//             sementara _Flash diturunkan; yang di luar radius tetap putih).
// Di garis pertemuannya ada cincin nyala putih (_EdgeGain) — itulah "cahaya yang
// menggambar" gambar barunya, bukan sekadar dua gambar di-crossfade.
//
// Jarak dinormalkan terhadap SETENGAH DIAGONAL kotak (0,7071), jadi _Reveal = 1 dijamin
// menelan pojok kotak sebelum efeknya dinyatakan selesai — piece selebar apa pun.
//
// Blend-nya NORMAL (SrcAlpha OneMinusSrcAlpha), bukan aditif: fase putihnya harus PEKAT
// menutupi art lama/baru di bawahnya, dan warna yang muncul harus warna asli piece —
// cahaya aditif akan membuat keduanya tembus pandang di atas kertas terang.
Shader "Grimoire/UiEvoReveal"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _Flash ("Putih menyeluruh", Range(0, 1)) = 0
        _Reveal ("Radius warna dari pusat", Range(0, 1)) = 0
        _EdgeSoft ("Kelembutan garis pertemuan", Range(0.01, 0.5)) = 0.12
        _EdgeWidth ("Lebar cincin nyala", Range(0.01, 0.5)) = 0.10
        _EdgeGain ("Terang cincin nyala", Range(0, 4)) = 1.6

        // --- boilerplate UI: dibutuhkan Mask/RectMask2D dan CanvasRenderer ---
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
            Name "UiEvoReveal"

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
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            float4 _ClipRect;
            float4 _MainTex_ST;

            float _Flash;
            float _Reveal;
            float _EdgeSoft;
            float _EdgeWidth;
            float _EdgeGain;

            v2f vert(appdata_t v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(o.worldPosition);
                o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                half4 tex = tex2D(_MainTex, i.texcoord);

                // 0 di pusat kotak, 1 di pojoknya — lihat catatan 0,7071 di atas.
                float d = length(i.texcoord - 0.5) / 0.7071;

                // 1 = sudah berwarna (di dalam radius), 0 = masih putih.
                float revealed = smoothstep(_Reveal, _Reveal - max(_EdgeSoft, 1e-3), d);

                float whiteness = saturate(max(_Flash, 1.0 - revealed));
                half3 rgb = lerp(tex.rgb * i.color.rgb, half3(1.0, 1.0, 1.0), whiteness);

                // Cincin nyala di garis pertemuan. Ikut padam saat _Reveal masih 0 (belum
                // ada pertemuan) dan saat sudah melewati pojok (ritualnya selesai).
                float ring = 1.0 - saturate(abs(d - _Reveal) / max(_EdgeWidth, 1e-3));
                float ringLive = step(0.001, _Reveal) * step(_Reveal, 0.999);
                rgb += ring * ring * _EdgeGain * ringLive;

                half alpha = tex.a * i.color.a;

                #ifdef UNITY_UI_CLIP_RECT
                alpha *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(alpha - 0.001);
                #endif

                return half4(rgb, alpha);
            }
            ENDCG
        }
    }

    FallBack "UI/Default"
}
