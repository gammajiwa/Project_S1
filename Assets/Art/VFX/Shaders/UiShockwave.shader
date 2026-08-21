// Cincin kejut "distorsi ruang" untuk letupan akhir evolusi ("ada nge-pop sambil
// ngeluarin vfx distorsi ruang" — permintaan pemilik project).
//
// Kenapa DI-FAKE, bukan membiaskan layar sungguhan: seluruh UI combat hidup di canvas
// Screen Space OVERLAY, yang digambar SESUDAH kamera selesai — _CameraOpaqueTexture tidak
// pernah memuat buku/papan, jadi shader refraksi beneran hanya bisa membiaskan arena 3D
// DI BELAKANG buku, bukan bukunya sendiri. Yang dibaca mata sebagai "ruang tertekuk" pada
// efek 2D adalah dua isyaratnya: cincin yang melebar sambil menipis, dan TEPI YANG
// TERURAI WARNA (chromatic fringe). Dua-duanya digambar prosedural di sini — merah, hijau,
// dan biru masing-masing diberi radius yang sedikit berbeda, makin renggang saat cincinnya
// melebar, persis seperti kaca yang menyebarkan cahaya.
//
// Satu kenop animasi: _Progress 0..1 (C# yang menggerakkan). Radius, tebal, urai warna,
// dan pudarnya semua diturunkan dari situ — supaya kurvanya tidak bisa saling tertinggal.
//
// Sedikit goyangan sudut pada radius (_Wobble) membuat cincinnya bukan lingkaran jangka —
// lingkaran yang terlalu sempurna terbaca sebagai ikon, bukan sebagai gelombang.
//
// Blend ADITIF (SrcAlpha One): kilatan cahaya sekejap di atas papan, bukan cat.
Shader "Grimoire/UiShockwave"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _Progress ("Kemajuan (0 lahir, 1 padam)", Range(0, 1)) = 0
        _Width ("Tebal cincin di awal", Range(0.01, 0.3)) = 0.09
        _Chroma ("Urai warna maksimum", Range(0, 0.1)) = 0.035
        _Gain ("Terang", Range(0, 4)) = 1.8
        _Wobble ("Goyangan bentuk", Range(0, 0.05)) = 0.012

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
            Name "UiShockwave"

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

            float _Progress;
            float _Width;
            float _Chroma;
            float _Gain;
            float _Wobble;

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

            // Satu cincin lembut di radius tertentu; tebalnya MENIPIS bersama progress —
            // gelombang yang melebar tanpa menipis terbaca sebagai balon, bukan kejutan.
            float Ring(float d, float radius, float width)
            {
                return pow(saturate(1.0 - abs(d - radius) / max(width, 1e-4)), 2.0);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 p = i.texcoord - 0.5;
                float d = length(p) * 2.0;
                float ang = atan2(p.y, p.x);

                float t = saturate(_Progress);

                // Melesat cepat lalu melambat: kurva kuadrat sudah cukup meniru gelombang
                // yang kehilangan tenaganya.
                float radius = lerp(0.08, 0.98, 1.0 - (1.0 - t) * (1.0 - t));
                radius += sin(ang * 5.0 + t * 9.0) * _Wobble
                        + sin(ang * 3.0 - t * 6.0) * _Wobble * 0.6;

                float width = _Width * lerp(1.0, 0.35, t);

                // Urai warna: tiga kanal, tiga radius. Renggangnya ikut progress — di
                // kelahirannya cincin masih putih rapat, makin jauh makin terurai.
                float spread = _Chroma * t;
                float r = Ring(d, radius + spread, width);
                float g = Ring(d, radius, width);
                float b = Ring(d, radius - spread, width);

                float env = (1.0 - t) * (1.0 - t);

                half3 rgb = half3(r, g, b) * _Gain * env * _Color.rgb * i.color.rgb;

                half4 tex = tex2D(_MainTex, i.texcoord);
                half alpha = saturate(max(rgb.r, max(rgb.g, rgb.b)))
                    * i.color.a * tex.a * _Color.a;

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
