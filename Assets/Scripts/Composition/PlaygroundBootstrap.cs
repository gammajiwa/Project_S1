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

        enum Formation { Ring, Grid, Line, Blob }

        static readonly Color Ink = new Color(0.92f, 0.94f, 1f);

        ContentDatabase _db;
        EnemyManager _enemies;
        PlayerCaster _caster;
        Font _font;
        Canvas _canvas;
        Transform _rig;

        readonly List<PieceDefinition> _catalogue = new List<PieceDefinition>();
        readonly List<Button> _rows = new List<Button>();

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

            _enemies.Init(playerGo.transform, _caster, _balance, _db);
            _caster.Init(_enemies, _db, _balance);

            // Pemain berdiri diam di sini. PlayerMotor sengaja TIDAK dipasang: skill yang diukur
            // sambil pemainnya menghindar akan menembak dari titik yang berbeda tiap cast, dan
            // jangkauannya jadi mustahil dibandingkan antar percobaan.
            _caster.BaseMaxHp = 1000000f;
            _caster.Hp = _caster.MaxHp;
            _caster.CastWithoutWave = true;

            _enemies.OnDamage += Record;
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

        void BuildCatalogue()
        {
            for (int i = 0; i < _db.Pieces.Count; i++)
            {
                var p = _db.Pieces[i];
                if (p == null || p.IsRune || p.IsPassive) continue;
                _catalogue.Add(p);
            }

            // Diurutkan bintang lalu nama, jadi posisi sebuah piece di daftar tidak pernah berubah
            // antar sesi. Daftar yang berpindah-pindah membuat menguji ulang jadi mencari-cari.
            _catalogue.Sort((a, b) =>
                a.Stars != b.Stars ? a.Stars.CompareTo(b.Stars)
                                   : string.CompareOrdinal(a.DisplayName, b.DisplayName));
        }

        void Select(int index)
        {
            if (_catalogue.Count == 0) return;

            _selected = Mathf.Clamp(index, 0, _catalogue.Count - 1);

            var book = _caster.Book;
            var placed = new List<RuneInstance>(book.Placed);
            for (int i = 0; i < placed.Count; i++) book.Remove(placed[i]);

            var piece = _catalogue[_selected];

            // Rune polos di bawahnya: skill tidak bisa berdiri di atas kekosongan, dan rune ber-aura
            // akan diam-diam mengubah angka yang sedang diukur.
            var plain = PlainRune();
            if (plain != null)
            {
                for (int y = 0; y < Grimoire.Height; y++)
                for (int x = 0; x < Grimoire.Width; x++)
                {
                    var cell = new Vector2Int(x, y);
                    if (book.CanPlace(plain, cell, 0)) book.Place(plain, cell, 0);
                }
            }

            for (int y = 0; y < Grimoire.Height; y++)
            for (int x = 0; x < Grimoire.Width; x++)
            {
                var cell = new Vector2Int(x, y);
                if (!book.CanPlace(piece, cell, 0)) continue;

                book.Place(piece, cell, 0);
                y = Grimoire.Height;
                break;
            }

            for (int i = 0; i < _rows.Count; i++)
            {
                var img = _rows[i].GetComponent<Image>();
                img.color = i == _selected
                    ? new Color(0.28f, 0.34f, 0.5f, 0.95f)
                    : new Color(0.1f, 0.11f, 0.15f, 0.85f);
            }

            Respawn();
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

            for (int i = 0; i < _dummyCount; i++)
            {
                _enemies.SpawnDummy(DummyPoint(i, _dummyCount), _dummyHp);
            }

            _enemies.Running = true;
        }

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

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
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
            var panel = Panel(_canvas.transform, new Vector2(0f, 0f), new Vector2(0f, 1f),
                new Vector2(8f, 8f), new Vector2(268f, -8f), new Color(0.05f, 0.06f, 0.09f, 0.9f));

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
            crt.pivot = new Vector2(0.5f, 1f);

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

            Label(panel, "PILIH SKILL  (" + _catalogue.Count + ")", 14, TextAnchor.MiddleLeft,
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
                t.text = new string('*', p.Stars) + "  " + p.DisplayName + "   <" + p.Kind + ">";

                var button = row.GetComponent<Button>();
                button.onClick.AddListener(() => Select(index));
                _rows.Add(button);
            }
        }

        void BuildControls()
        {
            var bar = Panel(_canvas.transform, new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-330f, -150f), new Vector2(-8f, -8f), new Color(0.05f, 0.06f, 0.09f, 0.9f));

            _readout = Label(bar, "", 12, TextAnchor.UpperLeft,
                Vector2.zero, Vector2.one, new Vector2(10f, 8f), new Vector2(-10f, -34f));

            Label(bar, "PENGATURAN BONEKA", 13, TextAnchor.MiddleLeft,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(10f, -28f), new Vector2(-10f, -6f));

            _hint = Label(_canvas.transform, "", 12, TextAnchor.LowerRight,
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-420f, 8f), new Vector2(-8f, 80f));
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
                _formation = (Formation)slot;
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

            _caster.Mana = _caster.MaxMana;   // ruang uji: rem mana tidak boleh ikut mengaburkan hasil
            Redraw();
        }

        void Redraw()
        {
            if (_selected < 0 || _selected >= _catalogue.Count) return;

            var piece = _catalogue[_selected];
            float elapsed = Mathf.Max(0.001f, Time.time - _measureStart);

            _damage.TryGetValue(piece.DisplayName, out float dealt);

            var spells = _caster.Book.Spells;
            float damage = spells.Count > 0 ? spells[0].Damage : piece.BaseDamage;
            float cooldown = spells.Count > 0 ? spells[0].Cooldown : piece.BaseCooldown;

            _readout.text =
                piece.DisplayName + "   " + new string('*', piece.Stars) + "\n" +
                piece.Kind + " / " + piece.Element + "   bentuk " + piece.Shape +
                " (" + piece.Cells.Length + " petak)\n" +
                "damage " + damage.ToString("0") + "   cooldown " + cooldown.ToString("0.00") + "s" +
                "   radius " + piece.Radius.ToString("0.0") + "   jangkauan " + piece.Range.ToString("0") + "\n\n" +
                "total damage " + dealt.ToString("0") + "  dalam " + elapsed.ToString("0.0") + "s" +
                "   =  " + (dealt / elapsed).ToString("0") + " dps terukur\n" +
                "boneka hidup " + _enemies.AliveCount + " / " + _dummyCount;

            _hint.text =
                "PANAH ATAS/BAWAH pilih skill   |   R susun ulang boneka\n" +
                "1 lingkaran   2 kisi   3 baris   4 acak   |   +/- rapat-renggang (" + _spread.ToString("0.0") + ")\n" +
                "SHIFT tahan = gerak lambat   |   ESC kembali ke menu";
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
