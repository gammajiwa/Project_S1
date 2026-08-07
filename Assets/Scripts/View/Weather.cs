using UnityEngine;

namespace Proto
{
    /// <summary>
    /// VFX suasana yang mengikuti kamera: kupu-kupu, daun beterbangan, burung, hujan, badai.
    ///
    /// Dua lapis yang sengaja dipisah:
    ///
    /// <b>Ambient</b> selalu menyala selama wajah arena ini dipakai — ia bagian dari tempatnya.
    /// <b>Cuaca</b> diundi ulang tiap wave, dan itulah yang membuat dua wave berturut-turut tidak
    /// pernah terasa sama. Menggabungkan keduanya jadi satu daftar berarti kupu-kupu ikut hilang
    /// begitu hujan turun, dan yang hilang justru hal yang membuat tempatnya dikenali.
    ///
    /// Semua efek dipaksa ke <b>ruang simulasi DUNIA</b>. Prefab-nya lahir dalam ruang lokal, dan
    /// selama pemancarnya diam itu benar. Di sini pemancarnya menempel di kamera: dalam ruang lokal,
    /// setiap tetes hujan yang sudah jatuh ikut diseret saat pemain berjalan — seluruh hujan
    /// bergerak menyamping seperti kaca yang digeser, bukan seperti hujan.
    /// </summary>
    public class Weather : MonoBehaviour
    {
        Transform _follow;
        Transform _ambient;
        Transform _mood;

        // Simpul untuk efek yang MENEMPEL di kamera (daun, hujan suasana). Dulu mereka ditanam
        // langsung di transform ini dan tidak pernah ikut dibersihkan saat wajah arena berganti —
        // tiap pergantian siang/malam menumpuk satu set daun lagi, tanpa satu pun error.
        Transform _carried;

        struct CarriedFx
        {
            public Transform Node;
            public bool HideInRain;
            public bool OnlyClear;
        }

        // Efek ikut-kamera dicatat dengan flag cuacanya sendiri. Daftar kantong tidak memuat
        // mereka, jadi tanpa ini debu matahari terus berkedip di tengah badai.
        readonly System.Collections.Generic.List<CarriedFx> _carriedFx =
            new System.Collections.Generic.List<CarriedFx>();

        BiomeDefinition _biome;
        float[] _cumulative = System.Array.Empty<float>();
        int _current = -1;

        /// <summary>Cuaca yang sedang berlaku. Untuk ditampilkan atau dicatat.</summary>
        public string MoodName { get; private set; } = "";

        Light _sun;
        Atmosphere _sky;

        // Nilai cerah, direkam saat wajah arena dipasang. Mendung dihitung DARI sini, bukan dengan
        // mengalikan nilai yang sedang berlaku — mengalikan berulang membuat tiap pergantian cuaca
        // menggelapkan lapangan sedikit lagi sampai akhirnya hitam.
        float _clearSun;
        Color _clearSky;
        Color _clearEquator;
        Color _clearGround;
        Color _clearCloud;
        float _clearCoverage;

        Light _playerLight;

        // Jarak "pasti di luar layar" dari pusat kamera, dihitung dari kameranya sendiri. Nol
        // kalau kameranya tidak dititipkan — dan saat itu MinDistance di aset yang menentukan.
        float _offscreen;

        public void Init(Transform follow, Light sun, Atmosphere sky, Light playerLight,
            Camera cam = null)
        {
            _follow = follow;
            _sun = sun;
            _sky = sky;
            _playerLight = playerLight;

            if (cam != null && cam.orthographic)
            {
                // Setengah diagonal layar di lantai, plus margin untuk lebar pemancarnya sendiri.
                // Kawanan burung yang menetas DI DALAM pandangan berhenti jadi kejadian yang
                // melintas — ia cuma muncul, dan mata menangkap kemunculannya seketika.
                float halfW = cam.orthographicSize * cam.aspect;
                float halfH = cam.orthographicSize;
                _offscreen = Mathf.Sqrt(halfW * halfW + halfH * halfH) + 7f;
            }
        }

        public void Apply(BiomeDefinition biome)
        {
            _biome = biome;
            _current = -1;
            MoodName = "";

            Clear(ref _ambient);
            Clear(ref _mood);
            Clear(ref _carried);

            _passing.Clear();
            _pockets.Clear();
            _carriedFx.Clear();

            if (biome == null || _follow == null) return;

            _clearSun = biome.SunIntensity;
            _clearSky = biome.AmbientSky;
            _clearEquator = biome.AmbientEquator;
            _clearGround = biome.AmbientGround;
            _clearCloud = biome.CloudColor;
            _clearCoverage = biome.CloudCoverage;

            BuildAmbient(biome);

            // Bobotnya dinormalkan sekali di sini supaya pengundiannya cukup satu angka 0–1.
            // Total bobot berubah tiap kali daftarnya disunting, dan bersamanya berubah pula
            // seluruh urutan cuaca yang sudah disetujui.
            var moods = biome.WeatherMoods;
            _cumulative = new float[moods != null ? moods.Length : 0];

            float running = 0f;

            for (int i = 0; i < _cumulative.Length; i++)
            {
                running += Mathf.Max(0f, moods[i].Weight);
                _cumulative[i] = running;
            }

            if (running > 0f)
            {
                for (int i = 0; i < _cumulative.Length; i++) _cumulative[i] /= running;
            }

            Roll(1);
        }

        /// <summary>
        /// Mengundi cuaca untuk wave ini.
        ///
        /// Diturunkan dari NOMOR WAVE lewat <see cref="WaveHash"/>, bukan dari UnityEngine.Random.
        /// Yang terakhir itu urutan acak milik gameplay — jenis musuh, sebaran drop, arah semburan
        /// skill — dan mengambil satu angka dari sana menggeser seluruh sisanya. Deterministik juga
        /// berarti wave yang sama selalu bercuaca sama: "wave 7 itu yang hujan" benar di setiap run.
        /// (Kenapa bukan System.Random ber-seed wave: lihat catatan di WaveHash — seed berjajar
        /// menghasilkan cuaca yang mengeblok berbelas wave.)
        /// </summary>
        public void Roll(int wave)
        {
            if (_biome == null || _cumulative.Length == 0) return;

            float roll = WaveHash.Roll01(wave, 104729);

            int pick = _cumulative.Length - 1;

            for (int i = 0; i < _cumulative.Length; i++)
            {
                if (roll <= _cumulative[i]) { pick = i; break; }
            }

            if (pick == _current) return;

            _current = pick;

            var mood = _biome.WeatherMoods[pick];
            MoodName = mood.Name;

            Clear(ref _mood);
            // Tiap efek disetel SENDIRI-SENDIRI, dan ini akhirnya jalan keluar dari tarik-menarik
            // yang bolak-balik. Satu pengali untuk seluruh cuaca tidak pernah bisa benar: hujan dan
            // angin-berdaun ada di paket yang sama, tapi tetesnya perlu membesar sementara daunnya
            // perlu mengecil, dan daunnya perlu lahir jauh di kiri sementara hujan harus tepat di
            // atas kepala.
            _mood = new GameObject("Cuaca " + mood.Name).transform;
            _mood.SetParent(transform, false);

            if (mood.Effects != null)
            {
                foreach (var effect in mood.Effects)
                {
                    if (effect == null || !effect.Valid) continue;

                    var node = Spawn(effect.Prefab.name, new[] { effect.Prefab }, mood.Speed,
                        _mood, effect.Scale, effect.CoverageOnly, effect.Grain);

                    if (node == null) continue;

                    node.localPosition = new Vector3(effect.Offset.x, effect.Height, effect.Offset.y);
                }
            }

            // Kupu-kupu berteduh. Yang tetap beterbangan di tengah hujan adalah hal pertama yang
            // membocorkan bahwa cuacanya cuma lapisan yang ditumpuk di atas suasana, bukan sesuatu
            // yang terjadi PADA lapangannya.
            //
            // "Cerah" TIDAK disimpan sebagai flag di mood — ia diturunkan: tidak basah dan tidak
            // ada satu pun efek yang jatuh dari langit. Flag terpisah pasti lupa dicentang di
            // salah satu mood suatu hari, dan gagalnya diam: berkas matahari di tengah badai.
            bool clear = !mood.Wet && (mood.Effects == null || mood.Effects.Length == 0);

            for (int i = 0; i < _pockets.Count; i++)
            {
                var pocket = _pockets[i];
                if (pocket.Node == null) continue;

                bool hidden = (pocket.HideInRain && mood.Wet) || (pocket.OnlyClear && !clear);
                pocket.Node.gameObject.SetActive(!hidden);
            }

            for (int i = 0; i < _carriedFx.Count; i++)
            {
                var fx = _carriedFx[i];
                if (fx.Node == null) continue;

                bool hidden = (fx.HideInRain && mood.Wet) || (fx.OnlyClear && !clear);
                fx.Node.gameObject.SetActive(!hidden);
            }

            Lantern(mood.Wet);
            Overcast(mood.Overcast);
        }

        /// <summary>
        /// Menyalakan lampu pemain saat hujan.
        ///
        /// Di siang cerah ia mati karena tidak membeli apa pun — lantainya sudah terang merata.
        /// Begitu mendung turun ia jadi satu-satunya sumber hangat di layar, sekaligus yang menjaga
        /// pemain tetap terbaca saat lapangannya meredup.
        ///
        /// Nilai kering diambil dari biome, bukan dari lampu yang sedang menyala — membaca yang
        /// sedang menyala berarti hujan kedua akan mengingat nilai hujan pertama sebagai "kering".
        /// </summary>
        void Lantern(bool wet)
        {
            if (_playerLight == null || _biome == null) return;

            float dry = _biome.PlayerLightIntensity;
            float on = wet ? Mathf.Max(dry, _biome.RainPlayerLight) : dry;

            _playerLight.intensity = on;
            _playerLight.enabled = on > 0f;
        }

        /// <summary>
        /// Mendung: matahari meredup, ambient mendingin, bayangan awan menebal dan meluas.
        ///
        /// Keempatnya sekaligus, dan itu memang intinya. Meredupkan matahari saja menghasilkan
        /// lapangan gelap yang bayangannya tetap setajam tengah hari — mendung yang sebenarnya
        /// justru MENGHAPUS bayangan tajam, karena cahayanya datang dari seluruh langit.
        /// </summary>
        void Overcast(float amount)
        {
            amount = Mathf.Clamp01(amount);

            // Matahari diredupkan SEPARUH, bukan sepertiga. Percobaan pertama menjatuhkannya ke
            // 0,34 dan hasilnya bukan mendung melainkan senja — dan lapangan yang segelap itu
            // kehilangan siluet musuh, hal yang paling tidak boleh hilang.
            // Semua angka penggelapan diambil dari ASET BIOME, bukan dipatok di kode. Tiga kali
            // ditebak dari sini dan tiga kali meleset — yang tahu seberapa gelap itu "mendung"
            // adalah yang sedang melihat layarnya, bukan yang sedang menulis kodenya.
            if (_sun != null)
                _sun.intensity = Mathf.Lerp(_clearSun, _clearSun * _biome.OvercastSun, amount);

            // Ambient nyaris TIDAK diturunkan, dan ini bagian yang sempat salah arah.
            //
            // Mendung yang sebenarnya bukan lapangan yang gelap — ia lapangan yang cahayanya datang
            // dari SELURUH LANGIT alih-alih dari satu titik. Matahari melemah, tapi langitnya
            // justru jadi sumber cahaya yang lebih besar. Menurunkan ambient bersama matahari
            // menghapus justru bagian yang membuatnya terbaca sebagai siang yang mendung.
            // Ambient justru DINAIKKAN, bukan diturunkan — dan inilah yang selama ini terbalik.
            //
            // Saat mendung, matahari hilang tapi seluruh kubah langit berubah jadi lampu. Cahaya
            // total di lantai hampir tidak berkurang; yang berubah adalah ARAHNYA — dari satu titik
            // jadi dari mana-mana, sehingga bayangan melembut dan warnanya mendingin. Menurunkan
            // ambient bersama matahari menghapus persis bagian yang membuatnya terbaca sebagai
            // siang, dan yang tersisa cuma malam yang kehujanan.
            float lift = _biome.OvercastAmbient;

            RenderSettings.ambientSkyColor = Color.Lerp(_clearSky, _clearSky * lift, amount);
            RenderSettings.ambientEquatorColor = Color.Lerp(_clearEquator, _clearEquator * lift, amount);
            RenderSettings.ambientGroundColor = Color.Lerp(_clearGround, _clearGround * lift, amount);

            if (_sky == null || _biome == null) return;

            // Awan ikut menebal dan meluas. Hujan tanpa awan yang menggelap terbaca sebagai hujan
            // di bawah langit cerah — dan mata menangkap kejanggalan itu sebelum sempat menamainya.
            var thick = _clearCloud;
            thick.a = Mathf.Lerp(_clearCloud.a, _biome.OvercastCloud, amount);

            _biome.CloudColor = thick;
            _biome.CloudCoverage = Mathf.Lerp(_clearCoverage,
                Mathf.Max(_clearCoverage, _biome.OvercastCloud), amount);

            _sky.Apply(_biome);

            _biome.CloudColor = _clearCloud;
            _biome.CloudCoverage = _clearCoverage;
        }

        struct Passing
        {
            public AmbientVfxEntry Entry;
            public float DueAt;
            public GameObject Live;
            public float ExpiresAt;
        }

        readonly System.Collections.Generic.List<Passing> _passing =
            new System.Collections.Generic.List<Passing>();

        /// <summary>
        /// Menyusun suasana sesi ini: mengundi mana yang dipakai, lalu memisahkan yang MENETAP dari
        /// yang LEWAT.
        ///
        /// Undiannya per sesi, bukan per frame — suasana yang berubah-ubah sendiri di tengah
        /// permainan terbaca sebagai efek yang berkedip, bukan sebagai tempat.
        /// </summary>
        void BuildAmbient(BiomeDefinition biome)
        {
            var entries = biome.AmbientVfx;
            if (entries == null || entries.Length == 0) return;

            // System.Random, bukan UnityEngine.Random — yang terakhir itu urutan acak milik
            // gameplay, dan mengambil satu angka dari sana menggeser seluruh sisanya.
            var dice = new System.Random(System.Environment.TickCount);

            foreach (var entry in entries)
            {
                if (entry == null || !entry.Valid) continue;
                if (dice.NextDouble() > entry.Chance) continue;

                if (entry.FollowCamera)
                {
                    // Menempel di kamera: yang cakupannya luas dan seragam tidak perlu dihitung di
                    // luar layar sama sekali. Itu juga sebabnya ia tidak ikut daftar kantong —
                    // tidak ada yang perlu dipindahkan, karena ia tidak pernah tertinggal.
                    // Digantung di simpul sendiri supaya ikut dibersihkan saat wajah berganti.
                    if (_carried == null)
                    {
                        _carried = new GameObject("Ikut Kamera").transform;
                        _carried.SetParent(transform, false);
                    }

                    var carried = Spawn("Ikut " + entry.Prefab.name, new[] { entry.Prefab },
                        1f, _carried, entry.Scale, entry.CoverageOnly, entry.Grain);

                    if (carried != null)
                    {
                        carried.localPosition =
                            new Vector3(entry.Offset.x, entry.Height, entry.Offset.y);

                        _carriedFx.Add(new CarriedFx
                        {
                            Node = carried,
                            HideInRain = entry.HideInRain,
                            OnlyClear = entry.OnlyClear
                        });
                    }

                    continue;
                }

                if (entry.RepeatEvery <= 0f)
                {
                    Scatter(entry);
                    continue;
                }

                // Yang pertama tidak langsung datang. Kawanan burung yang sudah terbang di detik
                // nol terbaca sebagai bagian dari perlengkapan, bukan sebagai sesuatu yang lewat.
                _passing.Add(new Passing
                {
                    Entry = entry,
                    DueAt = Time.time + (float)dice.NextDouble() * entry.RepeatEvery
                });
            }
        }

        struct Pocket
        {
            public Transform Node;
            public float Near;
            public float Far;
            public float Height;
            public bool HideInRain;
            public bool OnlyClear;
        }

        readonly System.Collections.Generic.List<Pocket> _pockets =
            new System.Collections.Generic.List<Pocket>();

        /// <summary>
        /// Menetaskan satu efek sebagai BEBERAPA KANTONG yang berjauhan, bukan satu gerombolan di
        /// kaki pemain.
        ///
        /// Kantongnya tidak menempel di kamera. Ia ditaruh di titik dunia, dibiarkan tertinggal saat
        /// pemain berjalan, lalu dipindahkan ke depan begitu jaraknya terlalu jauh — jadi ia selalu
        /// cukup dekat untuk terlihat tanpa pernah menempel di karakter.
        /// </summary>
        void Scatter(AmbientVfxEntry entry)
        {
            if (_ambient == null)
            {
                _ambient = new GameObject("Ambient").transform;

                // DI LUAR simpul yang mengikuti kamera, dan ini wajib. Simpul itu bergerak tiap
                // frame; kantong yang tergantung padanya akan ikut terseret, dan seluruh gunanya —
                // tertinggal di tempat saat pemain berjalan — hilang tanpa satu pun error.
                _ambient.SetParent(transform.parent, false);
            }

            for (int i = 0; i < Mathf.Max(1, entry.Count); i++)
            {
                var group = Spawn(entry.Prefab.name + " " + i, new[] { entry.Prefab }, 1f,
                    _ambient, entry.Scale);

                if (group == null) continue;

                var pocket = new Pocket
                {
                    Node = group,
                    Near = entry.MinDistance,
                    Far = Mathf.Max(entry.MinDistance + 1f, entry.Spread),
                    Height = entry.Height,
                    HideInRain = entry.HideInRain,
                    OnlyClear = entry.OnlyClear
                };

                Place(pocket);
                _pockets.Add(pocket);
            }
        }

        void Place(Pocket pocket)
        {
            // Cincin, bukan lingkaran penuh: mengundi jarak secara merata dari nol akan menumpuk
            // sebagian besar titik di dekat pusat, dan yang dihindari justru itu.
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float radius = Mathf.Lerp(pocket.Near, pocket.Far, Random.value);

            Vector3 at = _follow != null ? _follow.position : Vector3.zero;

            pocket.Node.position = new Vector3(
                at.x + Mathf.Cos(angle) * radius, pocket.Height, at.z + Mathf.Sin(angle) * radius);
        }

        void TickPockets()
        {
            if (_follow == null) return;

            Vector3 at = _follow.position;

            for (int i = 0; i < _pockets.Count; i++)
            {
                var pocket = _pockets[i];
                if (pocket.Node == null) continue;

                Vector3 delta = pocket.Node.position - at;
                delta.y = 0f;

                // Dipindahkan hanya kalau sudah JAUH DI LUAR jangkauan, bukan tepat di batasnya.
                // Memindahkan di batas membuat kantongnya berkedip maju-mundur setiap kali pemain
                // bergoyang di sekitar jarak itu.
                if (delta.sqrMagnitude < pocket.Far * pocket.Far * 2.25f) continue;

                Place(pocket);
            }
        }

        void TickPassing()
        {
            for (int i = 0; i < _passing.Count; i++)
            {
                var slot = _passing[i];

                if (slot.Live != null)
                {
                    if (Time.time < slot.ExpiresAt) continue;

                    Destroy(slot.Live);
                    slot.Live = null;

                    // Jeda diacak setengah sampai satu setengah kali. Jeda yang tepat sama
                    // terbaca sebagai jadwal, bukan sebagai kebetulan.
                    slot.DueAt = Time.time + slot.Entry.RepeatEvery * Random.Range(0.5f, 1.5f);
                    _passing[i] = slot;
                    continue;
                }

                if (Time.time < slot.DueAt) continue;

                // Digantung di simpul DIAM, bukan di simpul yang mengikuti kamera. Kawanan yang
                // digantung di kamera akan terseret bersama pemain — ia berhenti terbang melintas
                // dan berubah jadi hiasan yang menempel di atas kepala.
                if (_ambient == null)
                {
                    _ambient = new GameObject("Ambient").transform;
                    _ambient.SetParent(transform.parent, false);
                }

                var group = Spawn("Lewat " + slot.Entry.Prefab.name, new[] { slot.Entry.Prefab },
                    1f, _ambient, slot.Entry.Scale);

                if (group != null)
                {
                    // JAUH dari pemain, dan ARAHNYA acak. Versi sebelumnya menetaskannya di lokal
                    // nol — yaitu tepat di atas pemain, menghadap arah yang sama setiap kali.
                    // Kawanan burung yang selalu lahir di titik yang sama dan terbang ke arah yang
                    // sama berhenti jadi kejadian pada detik kedua kalinya.
                    //
                    // Batas bawahnya DIPAKSA ke luar layar, berapa pun angka di asetnya. Yang lewat
                    // harus DATANG dari suatu tempat — yang menetas di dalam pandangan cuma muncul.
                    float angle = Random.Range(0f, Mathf.PI * 2f);
                    float near = Mathf.Max(slot.Entry.MinDistance, _offscreen);
                    float radius = Mathf.Lerp(near, Mathf.Max(near + 1f, slot.Entry.Spread),
                        Random.value);

                    Vector3 at = _follow != null ? _follow.position : Vector3.zero;

                    group.position = new Vector3(
                        at.x + Mathf.Cos(angle) * radius,
                        slot.Entry.Height,
                        at.z + Mathf.Sin(angle) * radius);

                    group.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                }

                slot.Live = group != null ? group.gameObject : null;
                slot.ExpiresAt = Time.time + slot.Entry.Lifetime;
                _passing[i] = slot;
            }
        }

        void Clear(ref Transform slot)
        {
            if (slot != null) Destroy(slot.gameObject);
            slot = null;
        }

        /// <summary>
        /// Menetaskan satu kelompok efek di bawah satu induk bernama.
        ///
        /// Dikelompokkan supaya hierarkinya tetap terbaca: satu simpul untuk yang selalu ada, satu
        /// lagi untuk cuaca yang sedang berlaku. Tanpa itu, dua belas sistem partikel berserakan di
        /// akar scene dan tidak ada yang tahu mana milik siapa saat ada yang harus dimatikan.
        /// </summary>
        /// <param name="coverageOnly">
        /// Benar = yang membesar CAKUPAN pemancarnya, ukuran butirannya tetap. Salah = butirannya
        /// ikut membesar.
        ///
        /// Dua kebutuhan yang berlawanan dan sempat dikerjakan satu cara. Hujan perlu MENUTUPI
        /// layar: tetesnya harus tetap sebesar tetes, yang harus meluas adalah petak tempat ia
        /// turun. Daun sebaliknya — yang salah justru ukuran daunnya sendiri.
        ///
        /// Membesarkan keduanya dengan skala transform menghasilkan badai bertetes selebar pohon
        /// dan daun selebar dua meter yang melayang seperti layang-layang.
        /// </param>
        Transform Spawn(string label, GameObject[] prefabs, float speed, Transform parent = null,
            float scale = 1f, bool coverageOnly = false, float grain = 1f)
        {
            if (prefabs == null || prefabs.Length == 0) return null;

            var root = new GameObject(label).transform;
            root.SetParent(parent != null ? parent : transform, false);

            for (int i = 0; i < prefabs.Length; i++)
            {
                if (prefabs[i] == null) continue;

                var go = Instantiate(prefabs[i], root);
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;

                float size = Mathf.Max(0.01f, scale);
                go.transform.localScale = Vector3.one * size;

                foreach (var system in go.GetComponentsInChildren<ParticleSystem>(true))
                {
                    var main = system.main;

                    // WAJIB. Lihat catatan kelas: ruang lokal menyeret partikel yang sudah lahir.
                    main.simulationSpace = ParticleSystemSimulationSpace.World;

                    // INI yang memisahkan cakupan dari ukuran.
                    //
                    // Shape  : skala transform hanya menggeser TEMPAT partikel lahir. Butirannya
                    //          tetap sebesar aslinya — persis yang dibutuhkan hujan.
                    // Hierarchy: skala transform ikut mengubah ukuran butirannya.
                    //
                    // Bawaannya Local, yang mengabaikan skala induk sepenuhnya — jadi menyetel
                    // skala transform tanpa mengubah mode ini tidak melakukan apa pun, dan gagalnya
                    // diam: efeknya tetap tergambar, cuma seukuran semula.
                    main.scalingMode = coverageOnly
                        ? ParticleSystemScalingMode.Shape
                        : ParticleSystemScalingMode.Hierarchy;

                    // Cakupan yang meluas tanpa tambahan partikel menghasilkan hujan yang makin
                    // jarang seiring makin lebar. Lajunya dinaikkan LINEAR terhadap skala, bukan
                    // kuadrat: mengikuti luas sebenarnya berarti 6x jadi 36x partikel, dan itu
                    // membeli kerapatan yang tidak pernah terlihat dengan harga yang terasa.
                    // Butiran dibesarkan TERPISAH dari cakupan, dan hanya yang memang butiran.
                    //
                    // Prefab hujan mencampur dua hal yang ukurannya berbeda tiga ratus kali: tetes
                    // selebar 0,09 unit, dan lembaran kabut selebar 15 unit. Satu pengali untuk
                    // keduanya berarti memilih antara tetes yang tak terlihat atau satu billboard
                    // kabut selebar 40 unit yang menutupi seluruh layar — dan yang kedua itulah
                    // yang selama ini disangka "mendungnya kegelapan".
                    if (grain != 1f)
                    {
                        var startSize = main.startSize;

                        // Ambangnya 5 unit, bukan 1. Angin-berdaun punya butiran 0,9–1,2 — di
                        // ambang 1 ia ikut terlewat, dan justru itu yang paling perlu dikecilkan.
                        // Yang benar-benar harus dilindungi cuma lembaran kabut selebar 15 unit.
                        if (startSize.constantMax <= 5f)
                        {
                            startSize.constantMax *= grain;
                            startSize.constantMin *= grain;
                            main.startSize = startSize;
                        }
                    }

                    if (coverageOnly && size > 1f)
                    {
                        var emission = system.emission;
                        var rate = emission.rateOverTime;
                        rate.constantMax *= size;
                        rate.constantMin *= size;
                        emission.rateOverTime = rate;

                        main.maxParticles = Mathf.Min(20000,
                            Mathf.RoundToInt(main.maxParticles * size));
                    }
                    main.simulationSpeed = Mathf.Max(0.01f, main.simulationSpeed * speed);

                    // Bayangan MATI. Ribuan partikel yang melempar bayangan berarti ribuan objek
                    // tambahan di peta bayangan tiap frame, dan tidak satu pun terbaca di kamera
                    // yang menunduk.
                    var renderer = system.GetComponent<ParticleSystemRenderer>();

                    if (renderer != null)
                    {
                        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                        renderer.receiveShadows = false;
                    }
                }
            }

            return root;
        }

        void LateUpdate()
        {
            if (_follow == null) return;

            // Simpul ini mengikuti kamera, dan itu benar untuk CUACA — hujan memang harus turun di
            // mana pun pemain berada. Kantong suasana justru sebaliknya: ia ditaruh di titik dunia
            // dan sengaja tertinggal, jadi ia diurus terpisah di TickPockets.
            Vector3 at = _follow.position;
            transform.position = new Vector3(at.x, 0f, at.z);

            TickPassing();
            TickPockets();
        }
    }
}
