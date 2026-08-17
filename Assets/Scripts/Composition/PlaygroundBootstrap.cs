using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace Proto
{
    /// <summary>
    /// Ruang uji satu piece. Pilih dari daftar, boneka muncul dalam formasi yang bisa dibaca,
    /// skillnya menembak, dan angka damage-nya bisa dilihat tanpa gangguan.
    ///
    /// Alasan ini berdiri sendiri dan bukan sekadar saklar di scene game: yang mau dilihat saat
    /// menguji sebuah skill adalah BENTUKNYA — seberapa lebar ledakannya, ke mana rantainya
    /// melompat, berapa jauh bolanya menggelinding. Di tengah wave sungguhan semua itu tertutup
    /// gerombolan yang bergerak, dan hasilnya cuma bisa ditebak-tebak.
    ///
    /// Jadi bonekanya DIAM. Itu bukan penyederhanaan yang malas — musuh diam adalah satu-satunya
    /// cara membaca jangkauan sebuah skill secara jujur.
    /// </summary>
    public class PlaygroundBootstrap : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] ContentDatabase _database;
        [SerializeField] GameBalance _balance;
        [SerializeField] SceneLook _look;

        [Header("Boneka")]
        [Tooltip("HP tiap boneka. Tinggi supaya tidak langsung mati dan bentuk skillnya sempat terbaca.")]
        [SerializeField] float _dummyHp = 100000f;

        [SerializeField] int _dummyCount = 40;

        /// <summary>
        /// <c>Arc</c> ada khusus untuk skill yang membaca ARAH kerumunan.
        ///
        /// Blink menolak menyala saat <c>CrowdPressure</c> nol, dan lingkaran yang simetris
        /// menghasilkan persis nol — tiap boneka dibatalkan oleh boneka di seberangnya. Jadi dua
        /// skill Blink duduk di daftar ruang uji sambil tidak pernah berkedip sekali pun, dan yang
        /// terbaca adalah skill rusak, bukan formasi yang salah.
        /// </summary>
        enum Formation { Ring, Grid, Line, Blob, Arc }

        static readonly Color Ink = new Color(0.92f, 0.94f, 1f);

        /// <summary>Berapa nama reaksi yang bisa mengambang bersamaan.</summary>
        const int FloatPoolSize = 10;

        ContentDatabase _db;
        EnemyManager _enemies;
        PlayerCaster _caster;
        Font _font;
        Canvas _canvas;
        Transform _rig;

        readonly List<PieceDefinition> _catalogue = new List<PieceDefinition>();
        readonly List<Button> _rows = new List<Button>();
        readonly System.Text.StringBuilder _sb = new System.Text.StringBuilder(512);

        int _selected = -1;
        Formation _formation = Formation.Ring;
        // 6,5 karena skill bintang rendah jangkauannya sekitar 8: pada 9 unit, hal pertama yang
        // dilihat saat ruang uji dibuka adalah skill yang tidak menembak sama sekali. Benar secara
        // logika, dan terbaca seperti rusak.
        float _spread = 6.5f;
        Text _readout;
        Text _hint;

        // Damage per piece, dijumlah sejak boneka terakhir dipasang. Ini satu-satunya angka yang
        // menjawab "skill ini sebenarnya sekuat apa" tanpa ikut campur RNG wave.
        readonly Dictionary<string, float> _damage = new Dictionary<string, float>();
        float _measureStart;

        // ---------- yang bikin ruang uji ini akhirnya bisa menguji SEMUANYA ----------

        /// <summary>
        /// Boneka membalas: menggigit, menguras mana, dan menempelkan kutukan.
        ///
        /// Tanpa ini tujuh skill di buku MUSTAHIL diuji, dan bukan karena rusak: Ward menolak
        /// menyala kalau perisai lama masih tebal, Heal menolak di nyawa penuh, Cleanse menolak
        /// kalau tidak ada kutukan, dan Restore menolak di atas 60% mana. Boneka yang diam
        /// membuat keempat syarat itu tidak pernah terpenuhi seumur sesi.
        /// </summary>
        bool _dummiesFight;

        /// <summary>
        /// Boneka bisa MATI dan langsung diganti yang baru.
        ///
        /// Buat apa pun yang lahir dari kill: charge (Frenzy Sigil, Power Sigil), panen mana/HP
        /// per kill, dan pemakan charge yang menolak menembak di nol tumpukan. Boneka ber-HP
        /// sejuta tidak pernah mati, jadi seluruh cabang itu tidak pernah berjalan.
        /// </summary>
        bool _dummiesFragile;

        float _fightTimer;

        DamagePopups _popups;

        Text[] _floaters;
        float[] _floatLife;
        Vector3[] _floatWorld;
        Camera _lens;

        void Awake()
        {
            if (_database == null || _balance == null || _look == null)
            {
                Debug.LogError("[Playground] ContentDatabase / GameBalance / SceneLook belum diisi.", this);
                enabled = false;
                return;
            }

            Application.runInBackground = true;
            _db = _database;
            _look.ApplyEnvironment();

            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (_font == null) _font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            BuildScene();
            BuildCatalogue();
            BuildUi();
            BuildReadouts();

            Select(0);
        }

        // =============================================================================
        //  scene
        // =============================================================================

        void BuildScene()
        {
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            camGo.transform.SetParent(transform, false);
            camGo.transform.position = new Vector3(0f, 18.5f, -7.5f);
            camGo.transform.rotation = Quaternion.Euler(68f, 0f, 0f);

            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 11f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = RenderColor.Of(_look.HorizonColor);
            camGo.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;

            _rig = camGo.transform;

            var volume = new GameObject("PostProcess").AddComponent<Volume>();
            volume.transform.SetParent(transform, false);
            volume.isGlobal = true;
            volume.sharedProfile = _look.PostProcess;

            var sun = new GameObject("Sun");
            sun.transform.SetParent(transform, false);
            _look.ApplySun(sun.AddComponent<Light>());

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(transform, false);
            ground.transform.localScale = new Vector3(6f, 1f, 6f);
            Destroy(ground.GetComponent<Collider>());
            var gr = ground.GetComponent<Renderer>();
            gr.sharedMaterial = _look.CreateSurface(_look.GroundColor);
            gr.receiveShadows = true;
            gr.shadowCastingMode = ShadowCastingMode.Off;

            var playerGo = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            playerGo.name = "Player";
            playerGo.transform.SetParent(transform, false);
            playerGo.transform.position = new Vector3(0f, 0.9f, 0f);
            playerGo.transform.localScale = Vector3.one * 0.9f;
            Destroy(playerGo.GetComponent<Collider>());
            playerGo.GetComponent<Renderer>().sharedMaterial = _look.CreateSurface(_look.PlayerColor);

            var managerGo = new GameObject("EnemyManager");
            managerGo.transform.SetParent(transform, false);
            _enemies = managerGo.AddComponent<EnemyManager>();

            _caster = playerGo.AddComponent<PlayerCaster>();

            // Sinar memantul membaca tepi LAYAR, jadi ruang uji harus menyerahkan kameranya juga —
            // kalau tidak, satu-satunya skill yang bentuknya adalah coretan di layar akan diuji
            // memantul di kotak yang tidak ada hubungannya dengan apa yang terlihat.
            _caster.Lens = cam;

            _enemies.Init(playerGo.transform, _caster, _balance, _db);
            _caster.Init(_enemies, _db, _balance);

            // Pemain berdiri diam di sini. PlayerMotor sengaja TIDAK dipasang: skill yang diukur
            // sambil pemainnya menghindar akan menembak dari titik yang berbeda tiap cast, dan
            // jangkauannya jadi mustahil dibandingkan antar percobaan.
            _caster.BaseMaxHp = 1000000f;
            _caster.Hp = _caster.MaxHp;
            _caster.CastWithoutWave = true;

            _enemies.OnDamage += Record;
            _lens = cam;
        }

        /// <summary>
        /// Angka damage melayang DAN nama reaksi — dua hal yang selama ini cuma ada di scene
        /// permainan, tidak pernah di ruang uji.
        ///
        /// Itu bug yang menyamar sebagai fitur hilang: yang menguji skill di sini melihat reaksi
        /// MELETUS (kilatan, partikel, damage tercatat) tapi tidak pernah melihat NAMANYA, lalu
        /// wajar menyimpulkan popup reaksinya rusak — padahal ruang uji ini memang tidak pernah
        /// membangun satu pun. Ruang uji yang menyembunyikan separuh umpan balik game tidak bisa
        /// dipakai menilai apa pun.
        /// </summary>
        void BuildReadouts()
        {
            _popups = new DamagePopups(_canvas.transform, _font, _lens);
            _enemies.OnEnemyDamaged += _popups.Push;

            _floaters = new Text[FloatPoolSize];
            _floatLife = new float[FloatPoolSize];
            _floatWorld = new Vector3[FloatPoolSize];

            for (int i = 0; i < FloatPoolSize; i++)
            {
                var go = new GameObject($"Float_{i}");
                go.transform.SetParent(_canvas.transform, false);

                var text = go.AddComponent<Text>();
                text.font = _font;
                text.fontSize = 20;
                text.fontStyle = FontStyle.Bold;
                text.alignment = TextAnchor.MiddleCenter;
                text.raycastTarget = false;
                text.horizontalOverflow = HorizontalWrapMode.Overflow;
                text.verticalOverflow = VerticalWrapMode.Overflow;

                var rt = text.rectTransform;
                rt.anchorMin = rt.anchorMax = Vector2.zero;
                rt.sizeDelta = new Vector2(320f, 28f);

                _floaters[i] = text;
            }

            _enemies.OnReaction += (pos, rx) => PushFloater(pos, rx.DisplayName + "!", rx.FlashColor);
        }

        void PushFloater(Vector3 world, string message, Color color)
        {
            for (int i = 0; i < FloatPoolSize; i++)
            {
                if (_floatLife[i] > 0f) continue;

                _floatLife[i] = 1.1f;
                _floatWorld[i] = world;
                _floaters[i].text = message;
                _floaters[i].color = color;
                return;
            }
        }

        void TickFloaters(float dt)
        {
            for (int i = 0; i < FloatPoolSize; i++)
            {
                if (_floatLife[i] <= 0f)
                {
                    if (_floaters[i].text.Length > 0) _floaters[i].text = "";
                    continue;
                }

                _floatLife[i] -= dt;
                _floatWorld[i] += Vector3.up * (1.4f * dt);

                var screen = _lens.WorldToScreenPoint(_floatWorld[i]);
                _floaters[i].rectTransform.anchoredPosition = new Vector2(screen.x, screen.y);

                var c = _floaters[i].color;
                c.a = Mathf.Clamp01(_floatLife[i]);
                _floaters[i].color = c;
            }
        }

        void Record(string source, float amount)
        {
            if (string.IsNullOrEmpty(source)) source = "?";
            _damage.TryGetValue(source, out float sum);
            _damage[source] = sum + amount;
        }

        // =============================================================================
        //  daftar piece
        // =============================================================================

        /// <summary>
        /// SEMUA piece masuk daftar — rune dan segel sekalian.
        ///
        /// Dulu barisnya <c>if (p.IsRune || p.IsPassive) continue;</c>, dan itu membuang 44 dari
        /// 136 piece dari satu-satunya tempat di project ini yang bisa memeriksanya. Alasannya
        /// masuk akal saat ditulis — rune dan segel tidak "menembak", jadi tidak ada yang bisa
        /// diukur — tapi kesimpulannya salah: yang ingin diperiksa dari sebuah rune adalah AURANYA,
        /// dan dari sebuah segel adalah apa yang ia ubah. Keduanya terbaca justru dengan
        /// mendudukkan satu skill acuan di atasnya dan membaca angka skill itu berubah.
        /// </summary>
        void BuildCatalogue()
        {
            for (int i = 0; i < _db.Pieces.Count; i++)
            {
                var p = _db.Pieces[i];
                if (p == null) continue;
                _catalogue.Add(p);
            }

            // Dikelompokkan: skill dulu, lalu segel, lalu rune — dan di dalam tiap kelompok urut
            // bintang lalu nama. Posisi sebuah piece jadi tidak pernah berubah antar sesi, dan
            // daftar yang berpindah-pindah membuat menguji ulang jadi mencari-cari.
            _catalogue.Sort((a, b) =>
            {
                int ga = Group(a), gb = Group(b);
                if (ga != gb) return ga.CompareTo(gb);
                if (a.Stars != b.Stars) return a.Stars.CompareTo(b.Stars);
                return string.CompareOrdinal(a.DisplayName, b.DisplayName);
            });
        }

        static int Group(PieceDefinition p) => p.IsRune ? 2 : p.IsPassive ? 1 : 0;

        /// <summary>
        /// Skill acuan untuk menguji rune dan segel: yang paling polos yang ada di buku.
        ///
        /// Harus PROJECTILE dan tanpa ailment — apa pun yang meledak, memantul, atau menempelkan
        /// status akan mencampur perubahannya sendiri ke dalam angka yang sedang dibaca, dan yang
        /// diuji jadi bukan lagi segelnya.
        /// </summary>
        PieceDefinition Probe()
        {
            if (_probe != null) return _probe;

            // Tiga tingkat, dan tingkat-tingkatnya wajib ada.
            //
            // Percobaan pertama cuma menerima Projectile TANPA ailment, dan hasilnya null: setiap
            // peluru di buku ini menempelkan sesuatu. Yang terjadi bukan pengukuran yang lebih
            // bersih melainkan rune dan segel yang dipilih menampilkan papan tanpa satu pun skill
            // — persis kegagalan yang sedang diperbaiki.
            _probe = Pick(p => p.Kind == CastKind.Projectile && p.AppliedStatus == null)
                     ?? Pick(p => p.Kind == CastKind.Projectile)
                     ?? Pick(p => true);

            return _probe;
        }

        PieceDefinition _probe;

        /// <summary>Skill berbintang paling rendah yang lolos saringan, atau null.</summary>
        PieceDefinition Pick(System.Func<PieceDefinition, bool> accept)
        {
            PieceDefinition best = null;

            for (int i = 0; i < _db.Pieces.Count; i++)
            {
                var p = _db.Pieces[i];
                if (p == null || p.IsRune || p.IsPassive) continue;
                if (!accept(p)) continue;
                if (best == null || p.Stars < best.Stars) best = p;
            }

            return best;
        }

        void Select(int index)
        {
            if (_catalogue.Count == 0) return;

            _selected = Mathf.Clamp(index, 0, _catalogue.Count - 1);

            var book = _caster.Book;
            var placed = new List<RuneInstance>(book.Placed);
            for (int i = 0; i < placed.Count; i++) book.Remove(placed[i]);

            var piece = _catalogue[_selected];

            // Alas papan. Untuk RUNE yang sedang diuji, alasnya rune itu sendiri — auranya cuma
            // bisa dibaca lewat skill yang berdiri di atasnya. Untuk yang lain, rune polos: rune
            // ber-aura akan diam-diam mengubah angka yang sedang diukur.
            var bed = piece.IsRune ? piece : PlainRune();

            if (bed != null)
            {
                for (int y = 0; y < Grimoire.Height; y++)
                for (int x = 0; x < Grimoire.Width; x++)
                {
                    var cell = new Vector2Int(x, y);
                    if (book.CanPlace(bed, cell, 0)) book.Place(bed, cell, 0);
                }
            }

            // Rune tidak menembak apa-apa, jadi yang didudukkan di atasnya skill acuan. Segel juga
            // tidak menembak — ia didudukkan DAN ditemani skill acuan, supaya yang berubah
            // terlihat sebagai angka skill itu bergeser.
            if (!piece.IsRune) Seat(book, piece);
            if (piece.IsRune || piece.IsPassive) Seat(book, Probe());

            SeatPartners(book, piece);
            FitSpread(piece);

            for (int i = 0; i < _rows.Count; i++)
            {
                var img = _rows[i].GetComponent<Image>();
                img.color = i == _selected
                    ? new Color(0.28f, 0.34f, 0.5f, 0.95f)
                    : new Color(0.1f, 0.11f, 0.15f, 0.85f);
            }

            Respawn();
        }

        /// <summary>
        /// Menarik boneka MASUK ke jangkauan skill yang barusan dipilih.
        ///
        /// Ini menutup kegagalan yang paling menyesatkan di ruang uji ini: Blade Dance
        /// (radius 3,6, berpusat di badan pemain) diuji melawan lingkaran boneka berjari-jari 6,5,
        /// jadi bilahnya berputar di ruang kosong dan panelnya melaporkan <b>0 dps</b>. Yang
        /// terbaca skill rusak; yang sebenarnya terjadi bonekanya di luar jangkauan.
        ///
        /// Jarak sebarnya tidak pernah DINAIKKAN — cuma diturunkan. Menaikkannya untuk skill
        /// berjangkauan jauh akan diam-diam mengubah sebaran yang barusan disetel tangan, dan
        /// tombol +/- ada supaya yang mengaturnya pemain.
        /// </summary>
        void FitSpread(PieceDefinition piece)
        {
            float reach = Reach(piece);
            if (reach <= 0f) return;

            // 0,75 dari jangkauan: cukup di dalam supaya semuanya kena, tapi tidak menumpuk di
            // badan pemain — bentuk skillnya masih harus terbaca, dan itu tujuan ruang uji ini.
            float want = reach * 0.75f;
            if (want >= _spread) return;

            _spread = Mathf.Max(2f, want);
        }

        /// <summary>
        /// Sejauh mana piece ini benar-benar bisa MELUKAI. Nol berarti "tidak diketahui" —
        /// pasif, rune, dan skill non-serangan tidak menggeser sebaran sama sekali.
        /// </summary>
        static float Reach(PieceDefinition piece)
        {
            if (piece == null || piece.IsRune || piece.IsPassive) return 0f;

            switch (piece.Kind)
            {
                // Berpusat di BADAN pemain: radiusnya adalah seluruh jangkauannya, dan inilah
                // keluarga yang paling sering diuji di luar jangkauan.
                case CastKind.Orbital:
                case CastKind.Nova:
                case CastKind.Shockwave:
                case CastKind.ForcePush:
                    return piece.Radius;

                default:
                    return Mathf.Max(piece.Range, piece.Radius);
            }
        }

        /// <summary>Mendudukkan satu piece di petak kosong pertama yang muat. Diam saja kalau penuh.</summary>
        static bool Seat(Grimoire book, PieceDefinition piece)
        {
            if (piece == null) return false;

            for (int y = 0; y < Grimoire.Height; y++)
            for (int x = 0; x < Grimoire.Width; x++)
            {
                var cell = new Vector2Int(x, y);
                if (!book.CanPlace(piece, cell, 0)) continue;

                book.Place(piece, cell, 0);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Mendudukkan piece LAIN yang tanpanya piece terpilih tidak akan pernah menembak.
        ///
        /// Tiga keluarga skill di buku ini sengaja tidak bisa berdiri sendiri, dan itu desain yang
        /// benar — tapi ruang uji yang mendudukkan SATU piece lalu menunggu berarti ketiganya
        /// tampil sebagai skill yang rusak:
        ///
        /// - <b>Peledak</b> menahan cooldown DAN mana kalau tidak ada yang bertanda. Papan berisi
        ///   satu peledak tidak akan pernah punya sesuatu untuk diledakkan.
        /// - <b>Pemakan charge</b> menolak menembak di nol tumpukan, dan tumpukannya lahir dari
        ///   segel yang dulu bahkan tidak masuk daftar ruang uji ini.
        /// - <b>Pemicu ambang</b> menunggu poin ailment menumpuk di seorang musuh.
        /// </summary>
        void SeatPartners(Grimoire book, PieceDefinition piece)
        {
            // Peledak & pemicu ambang: cari skill yang MENEMPELKAN status yang ditunggunya.
            if (piece.TriggerStatus != null &&
                (piece.Kind == CastKind.Detonate || piece.Trigger == CastTrigger.StatusThreshold))
            {
                Seat(book, Marker(piece.TriggerStatus, piece));
            }

            // Pemakan charge: cari piece yang MEMBERIKAN buff yang dimakannya saat membunuh.
            if (piece.ConsumesCharge == null) return;

            for (int i = 0; i < _db.Pieces.Count; i++)
            {
                var other = _db.Pieces[i];
                if (other == null || other.GrantOnKill != piece.ConsumesCharge) continue;

                Seat(book, other);
            }
        }

        /// <summary>
        /// Skill termurah yang menempelkan sebuah status. Termurah supaya damage-nya sendiri
        /// nyaris tidak mengotori angka yang sedang diukur — yang diuji peledaknya, bukan penandanya.
        /// </summary>
        PieceDefinition Marker(StatusDefinition status, PieceDefinition exclude)
        {
            PieceDefinition best = null;

            for (int i = 0; i < _db.Pieces.Count; i++)
            {
                var p = _db.Pieces[i];
                if (p == null || p == exclude || p.IsRune || p.IsPassive) continue;
                if (p.AppliedStatus != status) continue;
                if (best == null || p.BaseDamage < best.BaseDamage) best = p;
            }

            return best;
        }

        /// <summary>Rune tanpa aura dan tanpa bonus elemen, supaya pengukuran tidak dicemari.</summary>
        PieceDefinition PlainRune()
        {
            PieceDefinition best = null;

            for (int i = 0; i < _db.Pieces.Count; i++)
            {
                var p = _db.Pieces[i];
                if (p == null || !p.IsRune) continue;
                if (p.Aura != AuraKind.None || p.ElementMatchBonus > 0f) continue;
                if (best == null || p.Cells.Length < best.Cells.Length) best = p;
            }

            // Semua rune punya aura: pakai yang paling lemah dan katakan begitu di layar.
            if (best == null)
            {
                for (int i = 0; i < _db.Pieces.Count; i++)
                {
                    var p = _db.Pieces[i];
                    if (p == null || !p.IsRune) continue;
                    if (best == null || p.AuraValue < best.AuraValue) best = p;
                }
            }

            return best;
        }

        // =============================================================================
        //  boneka
        // =============================================================================

        void Respawn()
        {
            _enemies.ClearField();
            _damage.Clear();
            _measureStart = Time.time;

            for (int i = 0; i < _dummyCount; i++) Hatch(i);

            _enemies.Running = true;
        }

        /// <summary>
        /// Satu boneka. HP-nya tergantung mode: tebal (tidak pernah mati, bentuk skill terbaca
        /// utuh) atau tipis (mati dan diganti terus, supaya apa pun yang lahir dari KILL berjalan).
        /// </summary>
        void Hatch(int index) =>
            _enemies.SpawnDummy(DummyPoint(index, _dummyCount), _dummiesFragile ? 40f : _dummyHp);

        Vector3 DummyPoint(int index, int total)
        {
            switch (_formation)
            {
                case Formation.Ring:
                {
                    float a = index / (float)total * Mathf.PI * 2f;
                    return new Vector3(Mathf.Cos(a) * _spread, 0.55f, Mathf.Sin(a) * _spread);
                }

                case Formation.Grid:
                {
                    int side = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(total)));
                    float step = _spread * 2f / side;
                    return new Vector3((index % side - side * 0.5f) * step, 0.55f,
                                       (index / side - side * 0.5f) * step + _spread * 0.4f);
                }

                case Formation.Line:
                    return new Vector3((index - total * 0.5f) * 1.1f, 0.55f, _spread);

                // Setengah lingkaran, semuanya di SATU sisi. Ini satu-satunya formasi yang
                // menghasilkan CrowdPressure bukan nol, dan tanpanya Blink tidak akan pernah
                // menyala berapa lama pun ditunggu.
                case Formation.Arc:
                {
                    float a = -0.9f + index / (float)Mathf.Max(1, total - 1) * 1.8f;
                    float r = _spread * (0.75f + (index % 3) * 0.16f);
                    return new Vector3(Mathf.Sin(a) * r, 0.55f, Mathf.Cos(a) * r);
                }

                default:
                {
                    float a = Random.Range(0f, Mathf.PI * 2f);
                    float r = _spread * Mathf.Sqrt(Random.value);
                    return new Vector3(Mathf.Cos(a) * r, 0.55f, Mathf.Sin(a) * r);
                }
            }
        }

        // =============================================================================
        //  UI
        // =============================================================================

        void BuildUi()
        {
            var go = new GameObject("Canvas");
            go.transform.SetParent(transform, false);

            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            // Sama dengan kanvas HUD run: rujukan QHD, match tinggi — playground tidak boleh
            // berbohong soal proporsi UI yang sedang dites.
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(2560f, 1440f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;
            go.AddComponent<GraphicRaycaster>();

            if (UnityEngine.EventSystems.EventSystem.current == null)
            {
                var es = new GameObject("EventSystem");
                es.transform.SetParent(transform, false);
                es.AddComponent<UnityEngine.EventSystems.EventSystem>();

                // StandaloneInputModule adalah modul LAMA dan membaca lewat UnityEngine.Input.
                // Di project ini backend-nya Input System, jadi modul itu melempar exception tiap
                // frame — daftar skillnya tetap tergambar tapi tidak satu pun tombol bisa diklik,
                // dan pesan errornya tidak pernah menyebut nama scene ini.
#if ENABLE_INPUT_SYSTEM
                es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
                es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
#endif
            }

            BuildList();
            BuildControls();
        }

        void BuildList()
        {
            // 268 -> 360. Daftarnya sekarang memuat rune dan segel juga, dan barisnya membawa
            // label kategori di belakang nama — "Blade Dance <Orbital>" tidak muat di 268 dan
            // terpotong jadi "Dance <Orbital>", yang membuat daftar lengkap tidak ada gunanya.
            var panel = Panel(_canvas.transform, new Vector2(0f, 0f), new Vector2(0f, 1f),
                new Vector2(8f, 8f), new Vector2(360f, -8f), new Color(0.05f, 0.06f, 0.09f, 0.9f));

            var scroll = new GameObject("Scroll", typeof(RectTransform), typeof(ScrollRect), typeof(Image), typeof(Mask));
            scroll.transform.SetParent(panel, false);
            var srt = (RectTransform)scroll.transform;
            srt.anchorMin = Vector2.zero; srt.anchorMax = Vector2.one;
            srt.offsetMin = new Vector2(4f, 4f); srt.offsetMax = new Vector2(-4f, -34f);
            scroll.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.01f);

            var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup),
                typeof(ContentSizeFitter));
            content.transform.SetParent(scroll.transform, false);
            var crt = (RectTransform)content.transform;
            crt.anchorMin = new Vector2(0f, 1f); crt.anchorMax = new Vector2(1f, 1f);

            // Pivot di KIRI-atas, bukan tengah-atas. Dengan pivot tengah, isi yang lebih lebar
            // dari viewport-nya melebar ke DUA arah — dan yang melebar ke kiri jatuh di luar layar,
            // jadi tiap nama piece kehilangan huruf-huruf pertamanya.
            crt.pivot = new Vector2(0f, 1f);
            crt.anchoredPosition = Vector2.zero;

            var layout = content.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 2f;
            layout.childForceExpandHeight = false;
            layout.childControlHeight = true;
            layout.childControlWidth = true;

            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var sr = scroll.GetComponent<ScrollRect>();
            sr.content = crt;
            sr.horizontal = false;
            sr.scrollSensitivity = 28f;
            sr.viewport = srt;

            Label(panel, "PILIH PIECE  (" + _catalogue.Count + ")", 14, TextAnchor.MiddleLeft,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(8f, -30f), new Vector2(-8f, -6f));

            for (int i = 0; i < _catalogue.Count; i++)
            {
                int index = i;
                var p = _catalogue[i];

                var row = new GameObject("Row", typeof(RectTransform), typeof(Image), typeof(Button),
                    typeof(LayoutElement));
                row.transform.SetParent(content.transform, false);
                row.GetComponent<LayoutElement>().preferredHeight = 24f;
                row.GetComponent<Image>().color = new Color(0.1f, 0.11f, 0.15f, 0.85f);

                var text = new GameObject("T", typeof(RectTransform), typeof(Text));
                text.transform.SetParent(row.transform, false);
                var trt = (RectTransform)text.transform;
                trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
                trt.offsetMin = new Vector2(6f, 0f); trt.offsetMax = new Vector2(-6f, 0f);

                var t = text.GetComponent<Text>();
                t.font = _font;
                t.fontSize = 12;
                t.alignment = TextAnchor.MiddleLeft;
                t.color = p.Color;
                t.text = new string('*', p.Stars) + "  " + p.DisplayName + "   <" +
                         (p.IsRune ? "RUNE" : p.IsPassive ? "SEGEL" : p.Kind.ToString()) + ">";

                var button = row.GetComponent<Button>();
                button.onClick.AddListener(() => Select(index));
                _rows.Add(button);
            }
        }

        void BuildControls()
        {
            // 322x142 -> 440x430. Panelnya sekarang membawa keterangan skillnya DAN satu baris
            // yang menjelaskan kenapa ia diam; keduanya kalimat, bukan angka, dan kotak setinggi
            // 142 memotongnya di tengah kata.
            var bar = Panel(_canvas.transform, new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-448f, -438f), new Vector2(-8f, -8f), new Color(0.05f, 0.06f, 0.09f, 0.92f));

            _readout = Label(bar, "", 12, TextAnchor.UpperLeft,
                Vector2.zero, Vector2.one, new Vector2(10f, 8f), new Vector2(-10f, -34f));

            Label(bar, "PENGATURAN BONEKA", 13, TextAnchor.MiddleLeft,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(10f, -28f), new Vector2(-10f, -6f));

            // Lebih lebar dan lebih tinggi: petunjuknya sekarang empat baris, dan yang lama
            // (-420 lebar, 80 tinggi) membungkusnya jadi tujuh baris yang saling menumpuk.
            _hint = Label(_canvas.transform, "", 12, TextAnchor.LowerRight,
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-760f, 8f), new Vector2(-8f, 108f));
        }

        void Update()
        {
            // Semua lewat ProtoInput, tidak pernah UnityEngine.Input langsung: project ini memakai
            // Input System baru, dan kelas lama melempar exception TIAP FRAME di sana — sisa
            // komponen ini ikut mati bersamanya tanpa satu pun pesan yang menyebut nama scene ini.
            if (ProtoInput.RotateDown) Respawn();

            int slot = ProtoInput.SpeedSlotDown;
            if (slot >= 0)
            {
                _formation = (Formation)Mathf.Clamp(slot, 0, 4);
                Respawn();
            }

            if (ProtoInput.CycleFaceDown) _dummiesFight = !_dummiesFight;

            if (ProtoInput.MapDown)
            {
                _dummiesFragile = !_dummiesFragile;
                Respawn();
            }

            int spread = ProtoInput.SpreadStepDown;
            if (spread != 0)
            {
                _spread = Mathf.Clamp(_spread + spread * 1.5f, 2f, 20f);
                Respawn();
            }

            int step = ProtoInput.ListStepDown;
            if (step != 0) Select(_selected + step);

            if (ProtoInput.BackDown) UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");

            Time.timeScale = ProtoInput.SlowMotionHeld ? 0.2f : 1f;

            float dt = Time.deltaTime;
            TickDummies(dt);
            TickFloaters(Time.unscaledDeltaTime);
            Redraw();
        }

        /// <summary>
        /// Mengisi ulang mana, atau TIDAK — dan menyalakan balasan boneka.
        ///
        /// Baris yang dulu ada di sini, <c>_caster.Mana = _caster.MaxMana</c> tiap frame, terlihat
        /// seperti kemudahan ruang uji dan sebenarnya sebuah tembok: <c>CastRestore</c> menolak
        /// menyala di atas 60% mana, jadi selama mana dipaksa penuh tiap frame skill itu MUSTAHIL
        /// diuji di sini. Sekarang pengisian ulang cuma berlaku selama boneka tidak membalas —
        /// begitu mode lawan menyala, sumber daya dibiarkan bergerak sungguhan.
        /// </summary>
        void TickDummies(float dt)
        {
            if (!_dummiesFight)
            {
                _caster.Mana = _caster.MaxMana;
                _caster.Hp = _caster.MaxHp;
            }
            else if (_enemies.AliveCount > 0)
            {
                // Gigitan tetap: cukup untuk menguras perisai dan membuka ruang buat Heal, tapi
                // nyawa dasar pemain di sini sejuta — ia tidak akan mati di tengah pengukuran.
                _caster.TakeDamage(_balance.EnemyContactDps * 3f * dt);
                _caster.Mana = Mathf.Max(0f, _caster.Mana - 6f * dt);

                _fightTimer -= dt;
                if (_fightTimer <= 0f)
                {
                    _fightTimer = 3f;

                    // Kutukan berkala — satu-satunya cara Cleanse punya sesuatu untuk dibersihkan.
                    _caster.ApplyDebuff(_db.RandomDebuff());
                }
            }

            if (!_dummiesFragile) return;

            // Boneka yang mati langsung diganti, jadi laju kill-nya tetap dan charge menumpuk.
            int missing = _dummyCount - _enemies.AliveCount;
            for (int i = 0; i < missing; i++) Hatch(Random.Range(0, _dummyCount));
        }

        void Redraw()
        {
            if (_selected < 0 || _selected >= _catalogue.Count) return;

            var piece = _catalogue[_selected];
            float elapsed = Mathf.Max(0.001f, Time.time - _measureStart);

            _damage.TryGetValue(piece.DisplayName, out float dealt);

            var spells = _caster.Book.Spells;

            // Untuk rune & segel, angka yang berarti adalah angka SKILL ACUAN — itulah cara
            // satu-satunya membaca aura atau tempelan stat. Damage yang dicatat pun atas nama
            // skill acuan, bukan atas nama piece yang sedang dipilih.
            bool indirect = piece.IsRune || piece.IsPassive;
            var probe = indirect ? Probe() : null;
            if (indirect && probe != null) _damage.TryGetValue(probe.DisplayName, out dealt);

            float damage = spells.Count > 0 ? spells[0].Damage : piece.BaseDamage;
            float cooldown = spells.Count > 0 ? spells[0].Cooldown : piece.BaseCooldown;

            _sb.Length = 0;
            _sb.Append(piece.DisplayName).Append("   ").Append(new string('*', piece.Stars)).Append('\n');
            _sb.Append(piece.IsRune ? "RUNE" : piece.IsPassive ? "SEGEL / ITEM" : piece.Kind.ToString())
               .Append(" / ").Append(piece.Element)
               .Append("   bentuk ").Append(piece.Shape).Append(" (").Append(piece.Cells.Length).Append(" petak)\n");

            if (piece.IsRune)
            {
                _sb.Append("aura ").Append(piece.Aura).Append(' ').Append(piece.AuraValue.ToString("0.##"))
                   .Append("   cocok elemen +").Append(piece.ElementMatchBonus.ToString("0.##")).Append('\n');
            }

            if (piece.Stats != null && piece.Stats.Length > 0)
            {
                for (int i = 0; i < piece.Stats.Length; i++)
                {
                    _sb.Append(i == 0 ? "stat: " : "   ")
                       .Append(piece.Stats[i].Type).Append(' ').Append(piece.Stats[i].Value.ToString("0.##"));
                }

                _sb.Append('\n');
            }

            if (!indirect)
            {
                _sb.Append("damage ").Append(damage.ToString("0"))
                   .Append("   cooldown ").Append(cooldown.ToString("0.00")).Append('s')
                   .Append("   radius ").Append(piece.Radius.ToString("0.0"))
                   .Append("   jangkauan ").Append(piece.Range.ToString("0")).Append('\n');
            }
            else if (probe != null)
            {
                _sb.Append("diukur lewat skill acuan: ").Append(probe.DisplayName)
                   .Append("   damage sekarang ").Append(damage.ToString("0")).Append('\n');
            }

            // Kalimat manusianya. Tiap piece SUDAH membawa keterangan yang ditulis untuk dibaca —
            // panel ini cuma tidak pernah menampilkannya, jadi yang menguji cuma melihat angka
            // dan harus menebak sendiri skill itu sebenarnya melakukan apa.
            if (!string.IsNullOrEmpty(piece.Blurb)) _sb.Append('\n').Append(piece.Blurb).Append('\n');

            string why = Diagnose(piece);
            if (why != null) _sb.Append("\n>> ").Append(why).Append('\n');

            _sb.Append('\n');
            _sb.Append("total damage ").Append(dealt.ToString("0"))
               .Append("  dalam ").Append(elapsed.ToString("0.0")).Append("s   =  ")
               .Append((dealt / elapsed).ToString("0")).Append(" dps terukur\n");
            _sb.Append("boneka hidup ").Append(_enemies.AliveCount).Append(" / ").Append(_dummyCount)
               .Append("   papan ").Append(_caster.Book.Placed.Count).Append(" piece\n");

            if (_dummiesFight)
            {
                _sb.Append("LAWAN: nyawa ").Append(_caster.Hp.ToString("0"))
                   .Append("  mana ").Append(_caster.Mana.ToString("0")).Append(" / ").Append(_caster.MaxMana.ToString("0"))
                   .Append("  perisai ").Append(_caster.Shield.ToString("0"));
            }

            _readout.text = _sb.ToString();

            _hint.text =
                "PANAH ATAS/BAWAH pilih piece   |   R susun ulang boneka   |   SHIFT = gerak lambat\n" +
                "1 lingkaran  2 kisi  3 baris  4 acak  5 BUSUR (buat Blink)   |   +/- rapat-renggang (" +
                _spread.ToString("0.0") + ")\n" +
                "F boneka MEMBALAS: " + (_dummiesFight ? "NYALA" : "mati") +
                "  (buat Ward / Heal / Cleanse / Restore)   |   " +
                "M boneka RAPUH: " + (_dummiesFragile ? "NYALA" : "mati") + "  (buat charge & panen kill)\n" +
                "ESC kembali ke menu";
        }

        /// <summary>
        /// Kenapa skill ini DIAM, dalam satu kalimat — atau null kalau tidak ada yang perlu
        /// dijelaskan.
        ///
        /// Ini penutup keluhan "ada skill yang gw nggak tahu efeknya apa". Separuh buku ini
        /// sengaja MENAHAN diri: peledak menahan cooldown kalau tidak ada yang bertanda, Heal
        /// menolak di nyawa penuh, Blink menolak saat lapangan lengang. Semua penolakan itu benar,
        /// dan semuanya terlihat persis sama dari luar — skill yang tidak melakukan apa-apa.
        ///
        /// Menyebut sebabnya mengubah "rusak" jadi "belum syaratnya", dan itu satu-satunya beda
        /// antara ruang uji yang bisa dipakai menilai dan ruang uji yang bikin curiga.
        /// </summary>
        string Diagnose(PieceDefinition piece)
        {
            if (piece.IsRune) return "RUNE — tidak menembak. Yang diukur skill acuan di atasnya; " +
                                     "auranya menggeser angka skill itu.";

            if (piece.IsPassive) return "SEGEL — tidak menembak. Yang diukur skill acuan di " +
                                        "sebelahnya; stat segel menggeser angka skill itu.";

            switch (piece.Kind)
            {
                case CastKind.Heal:
                    return _dummiesFight ? null
                        : "Heal MENOLAK menyala di nyawa penuh. Tekan F (boneka membalas).";

                case CastKind.Cleanse:
                    return _dummiesFight ? null
                        : "Cleanse MENOLAK menyala kalau tidak ada kutukan. Tekan F.";

                case CastKind.Restore:
                    return _dummiesFight ? null
                        : "Restore MENOLAK menyala di atas 60% mana. Tekan F.";

                case CastKind.Ward:
                    return _dummiesFight ? null
                        : "Ward menolak menumpuk di atas perisai yang masih tebal. Tekan F " +
                          "supaya perisainya termakan.";

                case CastKind.Blink:
                {
                    var push = _enemies.CrowdPressure(_caster.transform.position, 12f);
                    return push.sqrMagnitude > 0.0001f ? null
                        : "Blink MENOLAK menyala kalau tekanan kerumunan NOL — dan lingkaran " +
                          "simetris menghasilkan nol persis. Tekan 5 (formasi busur).";
                }

                case CastKind.Detonate:
                    return "Peledak: menahan cooldown DAN mana sampai ada musuh bertanda. " +
                           "Penandanya sudah ikut didudukkan otomatis.";

                case CastKind.Orbital:
                    return "Melukai dalam radius " + piece.Radius.ToString("0.0") +
                           " dari BADANMU — cakram di kaki itu batas sebenarnya, bukan besar " +
                           "efeknya. Boneka di luar cakram tidak akan tersentuh.";

                case CastKind.Turret:
                    return "Menara ditanam sejauh " + piece.Range.ToString("0") +
                           ", lalu menembak sendiri sejauh " + piece.Radius.ToString("0") +
                           " selama " + piece.ZoneDuration.ToString("0.#") + " detik.";

                case CastKind.Tether:
                    return "Mengunci SATU musuh dan menggerusnya terus selama " +
                           piece.ZoneDuration.ToString("0.#") + " detik. Damage-nya berjalan, " +
                           "bukan meletus — angka per denyut memang kecil.";

                case CastKind.Ricochet:
                    return "Memantul " + piece.Bounces + "x. Kalau boneka berdempetan, pantulannya " +
                           "jatuh ke tetangga sebelah dan coretannya mampat — renggangkan dengan +.";

                case CastKind.Boomerang:
                    return "Melukai DUA KALI di jalur yang sama: sekali pergi, sekali pulang.";

                case CastKind.Shockwave:
                    return "Hanya TEPI cincin yang melukai, dan tiap musuh kena sekali saat tepinya lewat.";

                case CastKind.Barrage:
                    return piece.Hits + " hantaman beraba-aba, berurutan, di titik berpencar.";

                case CastKind.Seeker:
                    return piece.Hits + " rudal ke " + piece.Hits + " musuh BERBEDA. Kalau boneka " +
                           "lebih sedikit dari itu, sisanya tidak berangkat.";

                case CastKind.Surge:
                    return "Tidak melukai apa pun — menempelkan buff ke dirimu sendiri. " +
                           "Yang berubah angka skill LAIN.";

                case CastKind.Orbit:
                    return "Pecahan mengambang dan MENUNGGU. Baru meluncur kalau ada yang mendekat.";
            }

            if (piece.ConsumesCharge != null && _caster.StacksOf(piece.ConsumesCharge) <= 0)
            {
                return "MENOLAK menembak di nol charge. Generatornya sudah didudukkan — tekan M " +
                       "(boneka rapuh) supaya ada yang mati dan charge-nya menumpuk.";
            }

            // Yang tersisa: skill serangan biasa yang mungkin cuma kehabisan sasaran.
            float reach = Reach(piece);
            if (reach <= 0f) return null;

            return _enemies.Nearest(_caster.transform.position, reach) != null ? null
                : "Tidak ada boneka dalam jangkauan " + reach.ToString("0.0") +
                  " — rapatkan dengan tombol -.";
        }

        // =============================================================================
        //  perkakas UI
        // =============================================================================

        static RectTransform Panel(Transform parent, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin, Vector2 offsetMax, Color color)
        {
            var go = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin; rt.offsetMax = offsetMax;

            go.GetComponent<Image>().color = color;
            return rt;
        }

        Text Label(Transform parent, string content, int size, TextAnchor anchor,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin; rt.offsetMax = offsetMax;

            var t = go.GetComponent<Text>();
            t.font = _font;
            t.fontSize = size;
            t.alignment = anchor;
            t.color = Ink;
            t.text = content;
            t.raycastTarget = false;
            return t;
        }
    }
}
