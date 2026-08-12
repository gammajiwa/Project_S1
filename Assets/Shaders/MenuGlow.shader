// Cahaya latar menu: satu bidang menghadap kamera yang MENAMBAH cahaya, bukan menutupi.
//
// Permintaan pemilik project (2026-08-12): "butuh shader gitu yaah cahaya2 biar gak terlalu
// gelap". Latarnya sudah punya bara, percikan, dan dua lingkaran sihir — yang belum ada sumber
// cahaya. Semua lapis sebelumnya alpha-blend, artinya mereka cuma bisa MENGGANTI warna di
// belakangnya; tidak satu pun bisa membuat layar lebih terang dari warna dasarnya.
//
// Di sini blendingnya aditif. Itu bedanya: bidang ini menambahkan cahaya ke apa pun yang sudah
// tergambar, jadi buku dan lingkaran sihir yang duduk di depannya ikut naik terang, dan sudut
// layar yang jauh dari pusatnya dibiarkan gelap.
//
// SELURUH geraknya dihitung dari _Time di dalam shader — denyut maupun putaran berkasnya. Tidak
// ada satu pun skrip yang menyentuh transformnya, dan itu keputusan sadar: komponen yang memutar
// transform pernah menimpa rotasi yang disetel tangan pemilik project dan menghapusnya permanen.
// Yang berputar di sini adalah PIKSEL, bukan objek, jadi angka di Inspector tidak pernah berubah.
//
// Target: URP Forward, PC + Mobile. Satu pass, nol sampel tekstur, tanpa cabang dinamis.
Shader "Grimoire/MenuGlow"
{
    Properties
    {
        [HDR] _Color ("Warna cahaya", Color) = (0.55, 0.3, 1, 1)

        [Space(8)]
        _Core ("Ukuran inti (0-1)", Range(0.01, 1)) = 0.18
        _Falloff ("Ketajaman peluruhan", Range(0.5, 8)) = 2.6

        [Space(8)]
        _Rays ("Jumlah berkas", Range(0, 32)) = 9
        _RayStrength ("Kekuatan berkas", Range(0, 2)) = 0.55
        _RaySharp ("Ketajaman berkas", Range(1, 12)) = 3.5

        // Derajat per detik. Kecil saja — latar menu dipandangi lama, dan apa pun yang
        // berputar cukup cepat untuk ketahuan periodenya berhenti jadi suasana.
        // (ShaderLab tidak mengenal [Tooltip]; menaruhnya di Properties adalah parse error.)
        _Spin ("Putaran berkas (der/dtk)", Range(-30, 30)) = 2.5

        [Space(8)]
        _Pulse ("Kedalaman denyut", Range(0, 0.6)) = 0.18
        _PulseSpeed ("Kecepatan denyut (siklus/dtk)", Range(0, 1)) = 0.07
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent-100"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            // Aditif murni. Alpha tidak dipakai untuk mencampur — ia dilipat ke dalam rgb, jadi
            // bagian yang tidak bercahaya menambahkan NOL dan menghilang sendiri tanpa perlu
            // menebak urutan gambar terhadap lapis transparan lain.
            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Core;
                float _Falloff;
                float _Rays;
                float _RayStrength;
                float _RaySharp;
                float _Spin;
                float _Pulse;
                float _PulseSpeed;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);

                // Quad unit membentang -0,5..0,5. Dikalikan dua supaya jari-jarinya 1 di tepi —
                // sama seperti Grimoire/AoeRing, jadi kedua shader latar memakai satuan yang sama.
                o.uv = input.positionOS.xy * 2.0;
                return o;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float r = length(input.uv);

                // Inti pejal lalu meluruh. Inti dipisah dari peluruhannya supaya "seberapa besar
                // bagian yang menyala penuh" dan "seberapa cepat sisanya padam" bisa disetel
                // sendiri-sendiri — digabung jadi satu angka, membesarkan cahayanya selalu ikut
                // melunakkan tepinya, dan yang tersisa cuma kabut.
                float body = saturate((1.0 - r) / max(0.0001, 1.0 - _Core));
                float glow = pow(body, _Falloff);

                // Berkas cahaya. Sudut dihitung dari pusat bidangnya sendiri, jadi ia tetap benar
                // berapa pun bidang ini diskalakan atau dimiringkan.
                float angle = atan2(input.uv.y, input.uv.x);
                float ray = 0.5 + 0.5 * sin(angle * _Rays + radians(_Spin) * _Time.y);
                ray = pow(ray, _RaySharp);

                // Berkasnya DILEMAHKAN di dekat pusat. Tanpa ini semua berkas bertemu di satu
                // titik dan intinya jadi bintang berujung tajam, bukan sumber cahaya.
                ray *= smoothstep(0.0, 0.45, r) * _RayStrength;

                float pulse = 1.0 + _Pulse * sin(_Time.y * _PulseSpeed * 6.2831853);

                float3 rgb = _Color.rgb * (glow * (1.0 + ray) * pulse * _Color.a);
                return half4(rgb, 1.0);
            }
            ENDHLSL
        }
    }
}
