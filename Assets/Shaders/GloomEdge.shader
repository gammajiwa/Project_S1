Shader "Grimoire/GloomEdge"
{
    // Kegelapan yang menggerogoti TEPI SEBUAH PANEL UI, saudara kanvas dari "Grimoire/Gloom".
    //
    // Kenapa shader terpisah, bukan Gloom yang dipakai ulang: Gloom membaca `positionWS` dan
    // berjalan di pass `UniversalForward`. Dua-duanya tidak ada artinya di Canvas — UI digambar
    // di pass sendiri, dan bidangnya tidak pernah punya posisi dunia yang berarti. Yang dipinjam
    // dari sana bukan kodenya melainkan SATU KEPUTUSAN yang membuatnya berhasil:
    //
    //     deraunya menggoyang GARIS BATASNYA, bukan kepekatannya.
    //
    // Mengalikan kepekatan dengan derau menghasilkan bingkai persegi rapi yang isinya belang —
    // masih terbaca sebagai "gradien yang dikasih noise". Menggeser AMBANG jaraknya membuat garis
    // batas gelapnya sendiri berkelok masuk-keluar puluhan piksel, dan di situlah ia berhenti
    // terbaca sebagai vignette dan mulai terbaca sebagai perkamen yang tepinya menghitam.
    //
    // Jaraknya dihitung dalam PIKSEL, bukan dalam UV. Panel peta besar (satu layar penuh) dan
    // panel peta intip (kotak melayang) punya rasio yang jauh berbeda; ukuran dalam UV membuat
    // pita gelapnya tebal di sisi pendek dan tipis di sisi panjang pada panel yang sama. Piksel
    // membuat keduanya kelihatan berasal dari satu bahan. `_RectSize` diisi dari C# tiap kali
    // ukuran panelnya berubah.
    //
    // Target: Canvas (ScreenSpaceOverlay), pass UI bawaan — tidak terikat URP/HDRP.
    // Biaya: 8 sampel value-noise per piksel (2 fbm), hanya di piksel yang memang tergelapkan;
    // bagian tengah panel keluar lebih awal tanpa menyentuh derau sama sekali.

    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}

        [Header(Warna)]
        _Color ("Warna kegelapan", Color) = (0.16, 0.10, 0.05, 1)
        _Ceiling ("Kepekatan maksimum", Range(0, 1)) = 0.55

        [Header(Mode kertas)]
        // Dua peran dari satu shader, memakai medan derau yang sama supaya garis batasnya
        // dijamin sinkron — dua material dengan derau berbeda akan terbaca sebagai dua tepi.
        //
        //   0 = LAPISAN GELAP. Bidang kosong yang ditumpuk di atas isi panel; cuma menghitamkan.
        //   1 = KERTASNYA SENDIRI. Menggambar sprite, menghitamkan tepinya, DAN memakan alphanya
        //       di pita terluar — ini yang membunuh potongan persegi. Menggelapkan saja tidak
        //       cukup: kertas gelap di atas hutan terang tetap persegi, cuma persegi yang gelap.
        [Toggle] _PaperMode ("Gambar sprite & robek tepi", Float) = 0

        [Header(Tepi robek)]
        // ROBEK, bukan memudar. Bedanya bukan seberapa jauh alphanya turun melainkan seberapa
        // CEPAT: gradien selebar puluhan piksel selalu terbaca sebagai kabut di depan kertas —
        // bentuk persegi aslinya masih kelihatan, cuma tepinya lunak. Potongan yang hampir
        // seketika memindahkan seluruh ketakberaturan ke GARIS potongnya, dan garis yang
        // berkelok tak beraturan itulah yang dibaca mata sebagai sobek.
        _TearDepth ("Kedalaman gigitan (piksel)", Float) = 64
        _TearScale ("Ukuran gigitan (piksel)", Float) = 70
        _TearFray ("Serat halus", Range(0, 1)) = 0.45
        _TearSoft ("Kelembutan potongan (piksel)", Range(0.5, 8)) = 1.5

        // Robekan yang HIDUP. Tanpa dua knob ini garis potongnya beku selamanya: `tp` di bawah
        // dihitung murni dari koordinat piksel, dan tidak ada kecepatan gloom yang bisa
        // menggerakkan sesuatu yang tidak punya waktu di dalam rumusnya.
        //
        // Geliat dikerjakan _TearWarp (bentuknya berubah DI TEMPAT), hanyut oleh _TearDrift.
        // Yang dominan sengaja geliatnya: tepi yang hanyut satu arah terus terbaca sebagai
        // tekstur yang jalan di belakang lubang, bukan sebagai kertas yang tepinya digerogoti.
        _TearWarp ("Geliat robekan", Range(0, 3)) = 0.8
        _TearDrift ("Hanyut robekan (piksel per detik)", Float) = 5

        // Perbandingan lebar:tinggi gambar aslinya, diisi dari C#. Dipakai untuk MENGISI kotak
        // panel tanpa memelarkan gambarnya — kelebihannya dipotong, bukan diperas.
        //
        // Kenapa di sini dan bukan lewat mask: kalau gambarnya dibesarkan melewati panel lalu
        // dipotong komponen mask, tepi yang dibaca shader jadi tepi GAMBAR — yang berada di luar
        // layar — dan pita larutnya ikut hilang ke luar sana. Memotong di UV membuat tepi gambar
        // dan tepi panel tetap satu benda.
        _TexAspect ("Rasio gambar (lebar/tinggi)", Float) = 1

        // Geser sampel perkamen, dalam satuan PANEL (1 = setinggi panel). Dipakai supaya latar
        // ikut bergerak saat peta di-scroll: latar yang diam sementara nodenya bergeser terbaca
        // sebagai node yang melayang di atas kaca, bukan sebagai peta yang digeser.
        _PaperScroll ("Geser perkamen (satuan panel)", Vector) = (0,0,0,0)

        [Header(Jangkauan dari tepi)]
        _Inset ("Mulai gelap (piksel dari tepi)", Float) = 190
        _Feather ("Gelap penuh (piksel dari tepi)", Float) = 8

        // Membalik ARAH gradasinya: pekat di TENGAH, memudar ke tepi.
        //
        // Bawaannya menggelapkan dari tepi ke dalam, dan itu benar untuk pekerjaan aslinya —
        // menggerogoti tepi sebuah panel yang isinya harus tetap terbaca. Tapi ia selalu
        // menyisakan lubang di tengah, dan gumpalan berlubang tidak pernah bisa jadi ASAP.
        // Dibalik, bidangnya jadi gumpalan pejal bertepi bergolak yang memudar keluar.
        [Toggle] _Invert ("Balik: pekat di tengah", Float) = 0

        [Header(Bentuk garis batas)]
        _Scale ("Ukuran gumpalan (piksel)", Float) = 210
        _Wobble ("Ketakberaturan garis batas (piksel)", Float) = 90
        _Warp ("Pelengkungan", Range(0, 1.5)) = 0.8
        _Churn ("Kecepatan bergolak", Float) = 0.05
        _Drift ("Kecepatan hanyut", Float) = 6

        // Diisi dari C#. Nilai bawaannya cuma penjaga supaya preview material tidak membagi nol.
        [HideInInspector] _RectSize ("Ukuran panel (piksel)", Vector) = (1920, 1080, 0, 0)

        // --- wajib ada supaya komponen Mask/RectMask2D UGUI tidak rusak saat material diganti ---
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
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "GloomEdge"

            CGPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            fixed4 _Color;
            float _Ceiling;
            float _PaperMode;
            float _TearDepth;
            float _TearScale;
            float _TearFray;
            float _TearSoft;
            float _TearWarp;
            float _TearDrift;
            float _TexAspect;
            float4 _PaperScroll;
            float _Inset;
            float _Feather;
            float _Invert;
            float _Scale;
            float _Wobble;
            float _Warp;
            float _Churn;
            float _Drift;
            float4 _RectSize;

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

                // Warna vertex dibawa supaya Image.color tetap jadi tombol redup/terang dan
                // fade panel ikut jalan tanpa menyentuh material.
                output.color = input.color;
                return output;
            }

            float Hash(float2 p)
            {
                p = frac(p * float2(127.1, 311.7));
                p += dot(p, p + 34.56);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 cell = floor(p);
                float2 f = frac(p);
                float2 blend = f * f * (3.0 - 2.0 * f);

                float a = Hash(cell);
                float b = Hash(cell + float2(1, 0));
                float c = Hash(cell + float2(0, 1));
                float d = Hash(cell + float2(1, 1));

                return lerp(lerp(a, b, blend.x), lerp(c, d, blend.x), blend.y);
            }

            float Fbm(float2 p)
            {
                float sum = 0.0;
                float amplitude = 0.5;

                [unroll]
                for (int i = 0; i < 4; i++)
                {
                    sum += ValueNoise(p) * amplitude;
                    p = p * 2.03 + 17.3;
                    amplitude *= 0.5;
                }

                return sum / 0.9375;
            }

            fixed4 Fragment(Varyings input) : SV_Target
            {
                float2 size = max(float2(1, 1), _RectSize.xy);
                float2 pixel = input.uv * size;

                // Jarak ke tepi TERDEKAT, per sumbu, dalam piksel.
                float2 toEdge = min(pixel, size - pixel);

                // ISI-lalu-POTONG, bukan melar. Sumbu yang "kelebihan" dipersempit jangkauan
                // sampelnya sehingga yang terbaca cuma bagian tengah gambar — bentuk nodanya
                // tetap bentuk aslinya, dan itu bedanya dengan perkamen yang keliatan ditarik.
                float panelAspect = size.x / size.y;
                float texAspect = max(0.001, _TexAspect);

                float2 uv = input.uv - 0.5;
                float2 scroll = _PaperScroll;

                // Geserannya diberi perlakuan yang SAMA dengan uv-nya. Kalau tidak, perkamen
                // bergerak lebih cepat atau lebih lambat dari node yang berdiri di atasnya —
                // dan latar yang menyeret sendiri terbaca lebih salah daripada latar yang diam.
                if (panelAspect > texAspect)
                {
                    uv.y *= texAspect / panelAspect;
                    scroll.y *= texAspect / panelAspect;
                }
                else
                {
                    uv.x *= panelAspect / texAspect;
                    scroll.x *= panelAspect / texAspect;
                }

                uv += 0.5 + scroll;

                // Diulang lewat frac, bukan lewat wrap mode tekstur: sprite yang dipak ke atlas
                // tidak punya wrap sendiri, dan menyetel wrapMode dari kode akan mengubah aset
                // impornya untuk SEMUA yang memakainya.
                //
                // Gradiennya diambil dari uv yang BELUM dibungkus. frac melompat dari 1 ke 0 di
                // garis sambungan, dan turunan lompatan itu raksasa — GPU akan menyimpulkan
                // "sangat mengecil di sini" lalu menarik mip paling kabur, meninggalkan garis
                // buram tepat di sambungan yang justru sedang disembunyikan.
                fixed4 paper = tex2Dgrad(_MainTex, frac(uv), ddx(uv), ddy(uv)) * input.color;

                // Potong lebih awal: bagian tengah panel tidak pernah membayar biaya derau.
                // Ambang paling longgar yang mungkin = _Inset + _Wobble, jadi apa pun yang lebih
                // dalam dari itu dijamin bersih berapa pun hasil deraunya nanti.
                // Ambang paling longgar yang mungkin, dihitung dari KEDUA efek: gelapnya bisa
                // menjangkau _Inset + _Wobble, robeknya bisa menggigit sedalam _TearDepth.
                // Mengambil yang terjauh menjamin tidak ada gigitan yang terpotong oleh
                // optimasi ini — dan gigitan yang terpotong akan tampil sebagai garis lurus
                // mendadak di tengah tepi yang sedang robek.
                float nearest = min(toEdge.x, toEdge.y);
                // _TearWarp menggeser koordinat sobeknya sejauh-jauhnya selebar satu gumpalan
                // sobek, jadi jangkauan gigitan ikut melebar segitu. Tanpa dijumlahkan di sini,
                // gigitan terjauh terpotong rata oleh optimasi ini — dan potongan rata itu
                // muncul sebagai garis lurus mendadak di tengah tepi yang sedang robek.
                if (nearest > max(_Inset + _Wobble, _TearDepth * 2.0 + _TearWarp * _TearScale))
                {
                    if (_PaperMode > 0.5) return paper;

                    // Saat dibalik, bagian dalam yang dilewati optimasi ini justru bagian yang
                    // PALING pekat — mengembalikan alpha nol di sini akan melubangi tengahnya,
                    // yaitu persis cacat yang sedang dihapus oleh mode ini.
                    if (_Invert > 0.5)
                        return fixed4(_Color.rgb * input.color.rgb,
                                      saturate(_Ceiling * _Color.a * input.color.a));

                    return fixed4(_Color.rgb, 0);
                }

                // Derau dibaca dari koordinat piksel panel, jadi gumpalannya berukuran tetap
                // di atas "kertas" — bukan ikut melar saat panelnya membesar.
                float2 p = pixel / max(1.0, _Scale);

                float2 churn = float2(_Time.y * _Churn, _Time.y * _Churn * 0.83);
                float2 warp = float2(Fbm(p + churn), Fbm(p * 1.17 - churn * 1.31)) - 0.5;

                p += warp * _Warp;
                p -= float2(_Time.y, _Time.y * 0.4) * _Drift / max(1.0, _Scale);

                float wobble = (Fbm(p) - 0.5) * _Wobble;

                // Ambangnya yang digoyang, bukan kepekatannya. Satu nilai derau dipakai untuk
                // KEDUA sumbu supaya lekukannya menyambung mengitari sudut, bukan patah di
                // diagonal seperti kalau tiap sisi punya deraunya sendiri.
                float start = max(0.0, _Inset + wobble);
                float full = min(start - 0.5, _Feather + wobble * 0.25);

                // Tiap sumbu menghasilkan kegelapannya sendiri, lalu digabung sebagai peluang
                // — bukan lewat min(). min() membuat lipatan diagonal tajam di tiap sudut;
                // menggabungkannya begini membuat sudut menggelap lebih pekat secara mulus,
                // yang memang bagaimana noda merambat masuk dari dua sisi sekaligus.
                float gx = 1.0 - smoothstep(full, start, toEdge.x);
                float gy = 1.0 - smoothstep(full, start, toEdge.y);
                float gloom = 1.0 - (1.0 - gx) * (1.0 - gy);

                // Dibalik, penggabungannya ikut berbalik jadi PERKALIAN: sebuah titik hanya
                // pekat kalau ia jauh dari kedua pasang tepi sekaligus. Memakai penggabungan
                // peluang yang sama seperti di atas akan membuat seluruh pita tepi ikut pekat,
                // dan yang keluar bujur sangkar penuh, bukan gumpalan.
                if (_Invert > 0.5) gloom = (1.0 - gx) * (1.0 - gy);

                gloom = saturate(gloom * _Ceiling);

                // --- tepi robek, dihitung untuk KEDUA mode ---
                //
                // Lapisan gelapnya WAJIB memakai potongan yang sama. Kalau tidak, ia tetap
                // persegi penuh sementara kertas di bawahnya sudah tercabik — dan yang tampil
                // adalah kotak gelap membayang di sekeliling kertas robek, yaitu bentuk yang
                // justru sedang dihapus.
                float2 tp = pixel / max(1.0, _TearScale);

                // Robekannya IKUT HIDUP, dan ini bagian yang sempat terlewat: menggerakkan noda
                // gloom tidak pernah bisa menggerakkan garis potongnya, karena `tp` di atas murni
                // koordinat piksel — tidak ada waktu di dalamnya sama sekali. Yang dibaca mata
                // sebagai "tepi peta" adalah garis potong itu, bukan nodanya.
                //
                // Medan warp DIPINJAM dari gloom alih-alih disampel ulang: dua Fbm lagi per
                // piksel untuk gerak serupa tidak sepadan, dan meminjamnya membuat noda dan
                // robekan menggeliat sebagai satu bahan — bukan dua lapisan yang berdenyut
                // sendiri-sendiri, yang justru akan terbaca sebagai dua tepi.
                tp += warp * _TearWarp;
                tp -= float2(_Time.y, _Time.y * 0.35) * _TearDrift / max(1.0, _TearScale);

                float bite = Fbm(tp) - 0.5;
                float fray = Fbm(tp * 4.7 + 31.7) - 0.5;

                // Selalu >= 0: nilai negatif berarti kertasnya TUMBUH keluar panel, dan yang
                // tumbuh keluar akan terpotong rata oleh tepi mesh — garis lurus, persis yang
                // sedang dihindari.
                float tear = max(0.0, _TearDepth * (0.55 + bite * 1.6 + fray * _TearFray * 1.6));

                // min(): dekat tepi kiri harus ikut robek berapa pun jaraknya dari tepi atas.
                float cut = min(toEdge.x, toEdge.y) - tear;

                float aa = max(fwidth(cut), _TearSoft);
                float intact = smoothstep(-aa, aa, cut);

                if (_PaperMode < 0.5)
                {
                    return fixed4(_Color.rgb * input.color.rgb,
                                  saturate(gloom * _Color.a * input.color.a) * intact);
                }

                // --- mode kertas ---
                //
                // Nodanya dicampur ke warna kertas, bukan ditumpuk sebagai lapisan: yang dicari
                // kertas yang MENGHITAM, dan lapisan gelap di atas kertas terang selalu terbaca
                // sebagai kaca kotor di depannya, bukan sebagai kertasnya sendiri.
                paper.rgb = lerp(paper.rgb, _Color.rgb, gloom);
                paper.a *= intact;
                return paper;
            }
            ENDCG
        }
    }

    Fallback Off
}
