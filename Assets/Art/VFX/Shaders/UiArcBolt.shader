// Garis penghubung evolusi digambar sebagai BUSUR LISTRIK: bergoyang, berkedip, dan
// menyala tipis di sekelilingnya.
//
// Kenapa shader, bukan memecah garisnya jadi puluhan potongan di C#: satu sambungan =
// satu Image UGUI, dan kolamnya cuma 40 (EvoLinePool). Satu resep tiga bahan sudah makan
// dua sambungan, dan kabel "bisa digabung dengan apa" ikut memakai kolam yang sama —
// memecah tiap garis jadi 8 potong akan menghabiskan kolamnya sebelum papan penuh, lalu
// sambungan terakhir hilang diam-diam. Di sini bentuk petirnya digambar DI DALAM satu
// kotak; yang bertambah cuma tinggi kotaknya, bukan jumlah objeknya.
//
// Kotaknya SENGAJA jauh lebih tinggi dari garisnya (GrimoireUI mengalikan tebal garis
// dengan EvoBoltHeightMul). Goyangannya hidup di ruang itu — kotak setipis garisnya akan
// memotong goyangan itu dan yang tersisa cuma garis lurus yang berkedip.
//
// Semua ukuran di sini dalam PECAHAN TINGGI KOTAK, bukan piksel: satu material melayani
// garis tipis (belum lengkap) dan garis tebal (siap berevolusi) tanpa disetel dua kali,
// dan tampilannya ikut membesar sendiri saat papannya di-scale.
//
// Blend-nya ADITIF (SrcAlpha One): ini cahaya, bukan cat. Di atas papan buku yang gelap
// itu yang membuatnya terbaca sebagai listrik alih-alih sebagai pita berwarna.
Shader "Grimoire/UiArcBolt"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _Amplitude ("Goyangan (pecahan tinggi kotak)", Range(0, 0.45)) = 0.26
        _Core ("Tebal inti (setengah, pecahan tinggi)", Range(0.005, 0.25)) = 0.085
        _Glow ("Lebar nyala", Range(0.02, 0.5)) = 0.34
        _GlowPower ("Ketajaman nyala", Range(0.5, 6)) = 2.3
        _GlowGain ("Kuat nyala", Range(0, 2)) = 0.8
        _Speed ("Kecepatan kerja", Range(0, 40)) = 9
        _Detail ("Kerapatan patahan", Range(1, 24)) = 7
        _Flicker ("Kedip", Range(0, 1)) = 0.3
        _Strand ("Untai kedua", Range(0, 1)) = 0.45

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
            Name "UiArcBolt"

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

            float _Amplitude;
            float _Core;
            float _Glow;
            float _GlowPower;
            float _GlowGain;
            float _Speed;
            float _Detail;
            float _Flicker;
            float _Strand;

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

            // Jalur petir sebagai jumlah gelombang dengan perbandingan TIDAK harmonis. Tiga
            // gelombang yang periodenya tidak kelipatan satu sama lain tidak pernah berulang
            // dalam satu bentang garis, dan itulah yang membedakan "patah-patah" dari
            // "bergelombang rapi". Lebih murah dari tekstur derau, dan tidak menambah aset.
            float BoltPath(float x, float t, float phase)
            {
                float w = 6.28318530718 * _Detail;
                float d = sin(x * w + t + phase) * 0.55;
                d += sin(x * w * 2.31 - t * 1.37 + phase * 1.7) * 0.29;
                d += sin(x * w * 5.17 + t * 0.63 + phase * 2.9) * 0.16;
                return d;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float x = saturate(i.texcoord.x);

                // Ruang tegak diukur dari SUMBU garis: 0 di tengah kotak, ±0,5 di tepinya.
                float y = i.texcoord.y - 0.5;

                // Kedua ujungnya DIJEPIT ke nol. Busur yang masih bergoyang saat menyentuh
                // petak tidak terlihat menancap di bahannya — ia terlihat meleset darinya.
                float endFade = smoothstep(0.0, 0.07, x) * smoothstep(0.0, 0.07, 1.0 - x);

                // Beda fase antar sambungan diambil dari KERAPATAN PIKSEL uv, yang berbanding
                // terbalik dengan panjang garisnya. Tanpa ini seluruh busur di layar bergoyang
                // dengan pola yang sama persis dan hasilnya terbaca sebagai animasi berulang,
                // bukan sebagai listrik. Gratis: tidak butuh material per garis, jadi seluruh
                // kolam tetap satu batch.
                float phase = 1.0 / max(fwidth(i.texcoord.x), 1e-5) * 0.017;

                float t = _Time.y * _Speed;

                float amp = _Amplitude * endFade;
                float d1 = BoltPath(x, t, phase) * amp;
                float dist1 = abs(y - d1);

                float core = 1.0 - smoothstep(0.0, _Core, dist1);
                float glow = pow(saturate(1.0 - dist1 / max(_Glow, 1e-4)), _GlowPower) * _GlowGain;

                // Untai kedua: lebih tipis, lawan arah, amplitudo separuh. Satu garis tunggal
                // terbaca sebagai tali yang bergoyang; dua untai yang saling menyilang itulah
                // yang terbaca sebagai listrik.
                float d2 = BoltPath(x * 1.7 + 0.37, -t * 0.83, phase * 1.9) * amp * 0.55;
                float dist2 = abs(y - d2);
                float strand = (1.0 - smoothstep(0.0, _Core * 0.7, dist2)) * _Strand;
                strand += pow(saturate(1.0 - dist2 / max(_Glow * 0.7, 1e-4)), _GlowPower) * _Strand * 0.35;

                float flick = lerp(1.0 - _Flicker, 1.0,
                    0.5 + 0.5 * sin(t * 2.7 + phase * 4.3) * sin(t * 1.13 + phase));

                float intensity = saturate(core + strand + glow) * flick * endFade;

                // Intinya memutih, tepinya memegang warna aslinya — itu yang membuat garis
                // berwarna tetap terbaca sebagai BENDA PANAS, bukan sekadar garis terang.
                half3 rgb = lerp(i.color.rgb, half3(1.0, 1.0, 1.0), saturate(core * 0.8 + strand * 0.4));

                // Tekstur ikut dikalikan supaya Image bersprite tetap masuk akal; tanpa sprite
                // UGUI memberi putih dan baris ini tidak mengubah apa pun.
                half4 tex = tex2D(_MainTex, i.texcoord);

                half alpha = intensity * i.color.a * tex.a;

                #ifdef UNITY_UI_CLIP_RECT
                alpha *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(alpha - 0.001);
                #endif

                return half4(rgb * tex.rgb, alpha);
            }
            ENDCG
        }
    }

    FallBack "UI/Default"
}
