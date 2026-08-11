// Sinar laser untuk LineRenderer milik BoltPool.
//
// Sprites/Default menggambar pita warna FLAT — tepinya tajam, tengahnya sama terang
// dengan pinggirnya, dan itulah yang membuatnya terbaca sebagai penanda prototype,
// bukan sebagai cahaya. Laser sungguhan dibaca mata dari dua hal: inti yang nyaris
// putih karena terlalu terang, dan halo yang meluruh lembut ke luar. Dua-duanya
// profil MELINTANG pita (uv.y), jadi shader inilah tempatnya — bukan tekstur.
//
// Kontrak dengan BoltPool dipertahankan penuh:
// - warna per-bolt lewat VERTEX COLOR (LineRenderer.startColor/endColor),
// - fade-out lewat ALPHA vertex color yang diturunkan Tick() tiap frame,
// jadi tidak ada satu baris pun C# yang perlu tahu shader ini ada.
//
// Additive (One One): sinar menambah terang layar, tidak menutupinya — dua laser
// bersilang saling menguatkan, seperti cahaya sungguhan. ZWrite Off karena benda
// transparan yang menulis depth cuma bikin lubang di partikel lain.
Shader "Grimoire/LaserBeam"
{
    Properties
    {
        // 2.2: di atas ambang bloom look profile (0,8–1,25), jadi INTI-nya menyala.
        // Halo dibiarkan di bawah ambang — yang berpendar cukup jantungnya saja.
        _Intensity ("Intensity", Range(0.5, 6)) = 2.2

        // Kecepatan denyut memanjang. 0 = diam.
        _PulseSpeed ("Pulse Speed", Range(0, 60)) = 22
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        Blend One One
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half _Intensity;
                half _PulseSpeed;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                half4  color      : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                half4  color      : COLOR;
            };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                // 0 di sumbu pita, 1 di tepinya.
                half across = abs(i.uv.y - 0.5) * 2.0;
                half falloff = saturate(1.0 - across);

                // Inti sempit (pangkat tinggi = meluruh cepat) + halo lebar yang redup.
                half core = pow(falloff, 7.0);
                half glow = pow(falloff, 2.0) * 0.30;

                // Denyut memanjang yang halus — energi yang mengalir, bukan kelap-kelip
                // rusak. Amplitudo kecil dengan sengaja: 12%.
                half pulse = 1.0 - 0.12 * (0.5 + 0.5 * sin(i.uv.x * 24.0 - _Time.y * _PulseSpeed));

                // Jantung sinar memutih: warna yang terlalu terang kehilangan saturasinya.
                // Tanpa ini inti laser merah tetap merah — terbaca pita, bukan cahaya.
                half3 tint = lerp(i.color.rgb, half3(1, 1, 1), core * 0.55);

                half3 rgb = tint * (core + glow) * pulse * _Intensity;

                // Fade-out dari Tick(): alpha vertex mengalikan SEMUA cahaya. Additive
                // tidak membaca alpha, jadi meredup harus lewat rgb.
                rgb *= i.color.a;

                return half4(rgb, 1);
            }
            ENDHLSL
        }
    }

    // Di luar URP (mis. preview editor lama) jatuh ke transparansi vertex-colored
    // sederhana — bukan tampilan finalnya, tapi tidak magenta.
    Fallback "Sprites/Default"
}
