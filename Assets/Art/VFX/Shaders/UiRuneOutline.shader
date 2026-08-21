// Cahaya putih yang BERKELILING di outline piece SAAT IA DIPASANG di buku — sekali
// jalan, lalu habis ("waktu dipasang langsung muter-muter grid-nya, udah, abis" —
// permintaan pemilik project; versi pertama menyala terus-menerus dan itu keliru).
// Dua lapis dalam satu shader: garis dasar tipis yang memperlihatkan SILUET grid piece
// selama sweep berlangsung, dan satu denyut terang yang mengitarinya.
//
// Satu segmen = satu Image UGUI di sepanjang SATU tepi petak. GrimoireUI yang memutuskan
// tepi mana yang jadi perimeter (tepi petak yang tetangganya BUKAN milik piece yang sama),
// jadi shader ini tidak perlu tahu bentuk polyomino — ia cuma menggambar seutas garis.
//
// Shader ini SENGAJA tidak memegang waktu (_Time) sama sekali: sweep-nya one-shot milik
// C# (GrimoireUI.DrawRuneOutlines), dan animasi yang digerakkan pemanggil bisa berhenti,
// menunggu giliran (ritual evolusi), atau dibatalkan saat piece diangkat — hal-hal yang
// mustahil diatur kalau jamnya berdetak sendiri di GPU.
//
// SATU material untuk seluruh kolam segmen — memecahnya per segmen berarti satu batch per
// tepi petak. Karena materialnya satu, keadaan per segmen dititipkan lewat WARNA VERTEX
// (Image.color), bukan lewat properti material:
//   r = fase AWAL segmen (0..1 jarak tempuh mengelilingi PERIMETER — bukan sudut:
//       fase sudut pernah dicoba dan hasilnya "patah-patah", karena di bentuk L dua
//       tepi berjauhan bisa berbagi sudut yang sama dan menyala serempak)
//   g = posisi denyut sekarang (0..1 keliling; C# yang menggerakkannya per frame)
//   b = BENTANG fase segmen ini — fragmen menginterpolasi r + uv.x * b, jadi denyut
//       MELUNCUR di sepanjang segmen alih-alih melompat segmen-per-segmen
//   a = alpha keseluruhan (amplop nyala-pudar sweep, juga dari C#)
// Warna cahayanya sendiri datang dari _Color material — putih, sesuai permintaan.
//
// Denyutnya berjalan karena jarak-fase dihitung MELINGKAR (wrap): segmen berfase 0,95 dan
// 0,05 bertetangga, jadi cahaya yang lewat ujung "1" muncul lagi di "0" tanpa melompat.
//
// Blend ADITIF (SrcAlpha One): ini cahaya di atas kertas gelap, bukan cat — sama seperti
// UiArcBolt di sebelahnya.
Shader "Grimoire/UiRuneOutline"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Warna cahaya", Color) = (1,1,1,1)

        _Base ("Terang garis dasar (siluet)", Range(0, 1)) = 0.22
        _Gain ("Terang denyut berjalan", Range(0, 3)) = 1.4
        _HaloGain ("Terang halo lebar di sekitar denyut", Range(0, 1)) = 0.45
        _TrailLen ("Panjang ekor denyut (pecahan keliling)", Range(0.02, 0.6)) = 0.22
        _TrailPow ("Ketajaman ekor", Range(0.5, 8)) = 3.0
        _CoreSoft ("Kelembutan tebal garis", Range(0.05, 1)) = 0.55

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
        Blend SrcAlpha One
        ColorMask [_ColorMask]

        Pass
        {
            Name "UiRuneOutline"

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

            float _Base;
            float _Gain;
            float _HaloGain;
            float _TrailLen;
            float _TrailPow;
            float _CoreSoft;

            v2f vert(appdata_t v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(o.worldPosition);
                o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                o.color = v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // Melintang garis: 0 di sumbu, 1 di tepi kotak. Falloff-nya lembut supaya
                // segmen-segmen yang bersambung tidak memperlihatkan sambungannya.
                float across = abs(i.texcoord.y - 0.5) * 2.0;
                float strand = pow(saturate(1.0 - across), 1.0 / max(_CoreSoft, 1e-3));

                // Fase kontinu: awal segmen + bentangnya × posisi di sepanjang segmen —
                // denyut meluncur DI DALAM segmen, bukan berpindah kotak-per-kotak.
                float phase = i.color.r + i.texcoord.x * i.color.b;

                // Jarak fase MELINGKAR dari posisi denyut sekarang (kanal g): 0 tepat di
                // denyut, 1 di seberang keliling. TANPA end-fade di sumbu x — segmen harus
                // menyambung mulus dengan tetangganya, ujung yang dipudarkan malah
                // membuat kelilingnya tampak putus-putus.
                float pulseAt = i.color.g;
                float dist = abs(frac(phase - pulseAt + 0.5) - 0.5) * 2.0;

                // Dua lapis nyala: inti tajam yang jadi "kepala" cahayanya, plus halo lebar
                // dan lembut yang membuat lewatnya terasa bercahaya, bukan seperti kursor.
                float pulse = pow(saturate(1.0 - dist / max(_TrailLen, 1e-3)), _TrailPow);
                float halo = pow(saturate(1.0 - dist / max(_TrailLen * 2.6, 1e-3)), 2.0) * _HaloGain;

                float intensity = (_Base + (pulse + halo) * _Gain) * strand;

                // Sprite ikut dikalikan; Image tanpa sprite memberi putih, baris ini netral.
                half4 tex = tex2D(_MainTex, i.texcoord);

                half alpha = saturate(intensity) * i.color.a * tex.a * _Color.a;

                #ifdef UNITY_UI_CLIP_RECT
                alpha *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(alpha - 0.001);
                #endif

                return half4(_Color.rgb * tex.rgb, alpha);
            }
            ENDCG
        }
    }

    FallBack "UI/Default"
}
