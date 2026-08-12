// Penanda area AOE: cakram semi transparan yang tepinya lebih tegas.
//
// Permintaan pemilik project (2026-08-10): cakram AOE yang lama terlalu pekat — ia menutupi
// musuh dan efek yang sedang terjadi DI DALAM areanya. Aturannya sekarang: seluruh lingkaran
// tetap terisi (bukan donat bolong — area dalam ikut ditandai), tapi tipis; tepi luarnya yang
// tebal, karena TEPI adalah informasi sesungguhnya: sampai mana serangan ini menjangkau.
//
// Jarak dihitung di ruang OBJEK (silinder unit: jari-jari tutup 0,5), jadi shader ini bekerja
// untuk cakram Zone maupun cincin telegraf SunStrike tanpa parameter tambahan — keduanya
// primitive silinder yang digepengkan, dan skala transform-lah yang membawa radius dunianya.
Shader "Grimoire/AoeRing"
{
    Properties
    {
        // Dikendurkan dua kali atas permintaan pemilik project: "udah cakep tapi terlalu
        // mencolok". Tepinya tetap yang paling terbaca — cuma tidak lagi berteriak.
        _BaseColor ("Warna", Color) = (1, 0.5, 0.2, 1)
        _FillAlpha ("Kepekatan isi", Range(0, 1)) = 0.06
        _RimAlpha ("Kepekatan tepi", Range(0, 1)) = 0.4
        _RimStart ("Mulai tepi (0-1)", Range(0.5, 0.98)) = 0.86
        _Pulse ("Denyut tepi", Range(0, 0.5)) = 0.12

        // Aba-aba berwaktu. 1 = penanda biasa (kubangan Zone, cincin Orbital, gelombang) dan
        // shader ini berperilaku persis seperti sebelum parameter ini ada.
        //
        // Di bawah 1 ia jadi TELEGRAF: isinya merambat dari pusat ke tepi, dan saat menyentuh
        // tepi itulah hantamannya mendarat. Dulu waktu ini digambar dengan MENSKALAKAN
        // silindernya (1,45x menciut ke 1x) — dan silinder polos yang berubah ukuran adalah
        // persis yang terbaca sebagai primitif. Lebih buruk lagi, selama menciut cincinnya
        // berbohong soal radius: yang ditandai lebih lebar dari yang benar-benar dilukai.
        _Fill ("Isi telegraf (0-1)", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float _FillAlpha;
                float _RimAlpha;
                float _RimStart;
                float _Pulse;
                float _Fill;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.positionOS = input.positionOS.xyz;
                return o;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // Silinder unit: tepi tutupnya di jari-jari 0,5. Dinding samping silinder yang
                // digepengkan juga mendarat di r = 1, jadi ia otomatis ikut warna tepi.
                float r = saturate(length(input.positionOS.xz) * 2.0);

                float rim = smoothstep(_RimStart, 1.0, r);

                // Denyut pelan di tepi saja. Isi dibiarkan diam — yang berkedip menarik mata,
                // dan yang boleh menarik mata cuma garis jangkauannya.
                rim *= 1.0 - _Pulse + _Pulse * (0.5 + 0.5 * sin(_Time.y * 4.2 + r * 6.0));

                // step(), bukan if(): satu hujan Barrage menaruh sampai dua belas telegraf di
                // layar sekaligus, dan cabang dinamis di fragment membayar kedua sisinya.
                float timed = 1.0 - step(0.999, _Fill);

                // Bidang isi. Di luar mode telegraf nilainya dipaksa 1, jadi penanda biasa
                // piksel-per-piksel sama persis dengan sebelum parameter ini ada.
                float body = lerp(1.0, 1.0 - smoothstep(_Fill - 0.05, _Fill, r), timed);

                // Muka gelombang yang merambat. Ini yang membuatnya terbaca sebagai HITUNGAN
                // MUNDUR dan bukan sekadar bidang yang membesar — mata mengunci garis terang
                // yang bergerak, bukan luas bidang yang berubah pelan.
                float lead = smoothstep(_Fill - 0.09, _Fill, r) *
                             (1.0 - smoothstep(_Fill, _Fill + 0.03, r)) * timed;

                float a = saturate(_FillAlpha * body + rim * _RimAlpha + lead * _RimAlpha) * _BaseColor.a;

                // Tepi diberi sedikit dorongan ke putih supaya tetap terbaca di atas warna
                // lantai apa pun — warna murni yang gelap tenggelam di rumput gelap. Dorongan
                // ini ikut dikecilkan: putih itu yang bikin cincinnya menyilaukan.
                float3 rgb = lerp(_BaseColor.rgb, saturate(_BaseColor.rgb + 0.2), rim * 0.35);

                // Mukanya lebih putih dari tepi: dia harus menang melawan tepi yang diam,
                // kalau tidak yang bergerak justru jadi yang paling samar.
                rgb = lerp(rgb, saturate(_BaseColor.rgb + 0.45), lead * 0.8);

                return half4(rgb, a);
            }
            ENDHLSL
        }
    }
}
