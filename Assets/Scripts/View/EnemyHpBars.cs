using UnityEngine;

namespace Proto
{
    /// <summary>
    /// Bar HP di atas kepala musuh: <b>muncul saat kena, sembunyi kalau tidak</b>.
    ///
    /// Selalu-tampil sudah dicoba dan salah untuk permainan ini: beberapa ratus musuh di layar
    /// berarti beberapa ratus palang kecil, dan yang tenggelam bukan cuma pemandangan — angka
    /// damage, sisa HP pemain, dan piece yang tercecer di lantai ikut hilang di dalamnya. Bar
    /// yang cuma menyala saat ada yang terjadi menjawab satu-satunya pertanyaan yang benar-benar
    /// ditanyakan pemain, "yang ini masih perlu berapa pukulan lagi", dan diam di sisa waktunya.
    ///
    /// Musuh tidak punya GameObject — swarm-nya digambar instanced dari larik data. Jadi bar ini
    /// tidak bisa ditempel sebagai anak: ia kolam palang di kanvas UI yang diletakkan tiap frame
    /// dari posisi dunia musuhnya, pola yang sama dengan angka damage melayang.
    /// </summary>
    public class EnemyHpBars
    {
        /// <summary>
        /// Berapa lama bar bertahan setelah pukulan terakhir. Cukup panjang untuk dibaca di sela
        /// tembakan beruntun, cukup pendek supaya layar kembali bersih begitu perkelahian pindah.
        /// </summary>
        const float ShowSeconds = 2.2f;

        /// <summary>Bagian akhir masa tampil yang dipakai memudar, dalam detik.</summary>
        const float FadeSeconds = 0.5f;

        /// <summary>
        /// Batas jumlah palang yang digambar sekali waktu. Bukan penghematan: satu Blizzard
        /// melukai tiga puluh musuh dalam satu frame, dan tiga puluh palang yang muncul serentak
        /// adalah persis kekacauan yang aturan "cuma saat kena" ada untuk mencegah.
        /// </summary>
        const int PoolSize = 40;

        /// <summary>Tinggi bar di atas titik pijak musuh, dalam satuan dunia, sebelum dikali skalanya.</summary>
        const float HeadHeight = 1.75f;

        /// <summary>
        /// Pembesar palang boss. Aturan tampilnya sama persis dengan musuh biasa — ukurannya
        /// yang membedakan, dan itu memang satu-satunya beda yang perlu ada.
        /// </summary>
        const float BossBarScale = 1.9f;

        readonly EnemyHpBar[] _pool = new EnemyHpBar[PoolSize];
        readonly Camera _camera;
        readonly EnemyManager _enemies;

        // Skala kanvas membagi hasil WorldToScreenPoint — anchoredPosition bersatuan unit
        // kanvas, bukan piksel layar. Tanpa pembagian ini semua palang merosot ke arah
        // kiri-bawah begitu jendela game mengecil (bug yang sama dengan popup damage).
        readonly Canvas _canvasRef;

        float CanvasScale => _canvasRef != null ? Mathf.Max(0.0001f, _canvasRef.scaleFactor) : 1f;

        public EnemyHpBars(Transform canvas, Camera camera, EnemyManager enemies, GameObject prefab)
        {
            _camera = camera;
            _enemies = enemies;
            _canvasRef = canvas != null ? canvas.GetComponentInParent<Canvas>() : null;

            if (prefab == null)
            {
                Debug.LogWarning("[EnemyHpBars] EnemyHpBar.prefab tidak ada di Resources — " +
                                 "palang darurat dipakai. Prefabnya ada di " +
                                 "Assets/Prefabs/UI/Resources/EnemyHpBar.prefab.");
            }

            for (int i = 0; i < PoolSize; i++)
            {
                GameObject go;

                if (prefab != null)
                {
                    go = Object.Instantiate(prefab, canvas, false);
                }
                else
                {
                    // Prefabnya hilang: palangnya dirakit di sini, sederhana tapi TERLIHAT.
                    //
                    // Versi sebelumnya cuma membuat objek kosong, dan hasilnya adalah kegagalan
                    // paling mahal yang bisa dibuat UI — tidak ada error, tidak ada peringatan,
                    // cuma fitur yang diam. Yang mencarinya akan menyalahkan kode barnya, bukan
                    // prefab yang tidak ketemu.
                    go = Fallback(canvas);
                }

                go.name = "EnemyHp_" + i;

                var bar = go.GetComponent<EnemyHpBar>();
                if (bar == null) bar = go.AddComponent<EnemyHpBar>();

                var rect = (RectTransform)go.transform;
                rect.anchorMin = rect.anchorMax = Vector2.zero;
                rect.pivot = new Vector2(0.5f, 0.5f);

                go.SetActive(false);
                _pool[i] = bar;
            }
        }

        /// <summary>Palang darurat: dua kotak, bentuk yang sama dengan prefabnya.</summary>
        static GameObject Fallback(Transform canvas)
        {
            var go = new GameObject("EnemyHpBar", typeof(RectTransform));
            go.transform.SetParent(canvas, false);
            ((RectTransform)go.transform).sizeDelta = new Vector2(52f, 8f);

            Plate(go.transform, "Back", new Color(0.04f, 0.03f, 0.05f, 0.78f), 0f);
            var fill = Plate(go.transform, "Fill", new Color(0.42f, 0.85f, 0.35f, 1f), 1f);

            fill.type = UnityEngine.UI.Image.Type.Filled;
            fill.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)UnityEngine.UI.Image.OriginHorizontal.Left;

            return go;
        }

        static UnityEngine.UI.Image Plate(Transform parent, string name, Color color, float inset)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);

            var image = go.AddComponent<UnityEngine.UI.Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>
        /// Digerakkan dengan waktu TAK BERSKALA, sama dengan angka damage: di kecepatan 5x bar
        /// yang ikut waktu permainan hilang sebelum sempat dibaca, dan di jeda antar-wave ia harus
        /// tetap bisa memudar.
        /// </summary>
        public void Tick()
        {
            int used = 0;
            float now = Time.unscaledTime;

            // Boss DULUAN, dan itu disengaja: kolamnya berbatas 40, dan boss yang antre di
            // belakang gerombolan bisa kehilangan palangnya justru di detik paling ramai —
            // yaitu detik ia sedang dipukuli.
            used = TickBoss(used, now);

            if (_enemies != null && _camera != null)
            {
                var all = _enemies.All;

                for (int i = 0; i < all.Count && used < PoolSize; i++)
                {
                    var e = all[i];
                    if (e == null || !e.Alive) continue;

                    // Ruas boss dilewati DI SINI: boss dapat satu palang di kepalanya lewat
                    // TickBoss. Palang di tiap ruas ular cuma mengulang angka yang sama
                    // belasan kali — HP-nya satu kolam, bukan per ruas.
                    if (e.Boss != null) continue;

                    float since = now - e.HurtSeen;
                    if (since < 0f || since > ShowSeconds) continue;

                    // Yang belum tergores tidak pernah menampilkan bar penuh: itu tidak
                    // memberi tahu apa pun, dan cuma menambah palang di layar.
                    if (e.MaxHp <= 0f || e.Hp >= e.MaxHp) continue;

                    var world = e.Pos + Vector3.up * (HeadHeight * Mathf.Max(0.25f, e.Scale));
                    var screen = _camera.WorldToScreenPoint(world) / CanvasScale;

                    // Di belakang kamera WorldToScreenPoint memantulkan titiknya ke sisi yang
                    // salah, dan barnya muncul di seberang layar dari musuh yang dimaksud.
                    if (screen.z <= 0f) continue;

                    var bar = _pool[used++];
                    if (!bar.gameObject.activeSelf) bar.gameObject.SetActive(true);

                    // Dikembalikan ke ukuran normal: palang yang sama bisa saja baru dipakai
                    // boss di frame sebelumnya, dan skala yang tertinggal membuat satu grunt
                    // acak tampil dengan palang seukuran boss.
                    bar.transform.localScale = Vector3.one;

                    bar.PlaceAt(new Vector2(screen.x, screen.y));
                    bar.Set(e.Hp / e.MaxHp,
                        Mathf.Clamp01((ShowSeconds - since) / FadeSeconds));
                }
            }

            for (int i = used; i < PoolSize; i++)
            {
                if (_pool[i] != null && _pool[i].gameObject.activeSelf)
                    _pool[i].gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Satu palang untuk boss, di atas KEPALANYA — pengganti bar selebar layar yang dicabut.
        ///
        /// Aturan tampilnya tidak dibedakan: muncul saat kena, memudar, lalu hilang. Boss yang
        /// palangnya menyala terus akan menjadi satu-satunya benda di layar yang melanggar
        /// aturan itu, dan pengecualian semacam itu selalu terbaca sebagai HUD yang lupa
        /// dimatikan.
        /// </summary>
        int TickBoss(int used, float now)
        {
            if (_enemies == null || _camera == null || used >= PoolSize) return used;

            var boss = _enemies.Boss;
            if (boss == null || !boss.Alive || boss.MaxHp <= 0f) return used;

            var segments = boss.Segments;
            if (segments == null || segments.Count == 0) return used;

            // Kapan boss terakhir kena — dari ruas MANA PUN.
            //
            // Damage dicatat di ruas yang tertembak sementara palangnya berdiri di kepala. Tanpa
            // menyapu seluruh ruas, memukul ekor tidak memunculkan apa-apa: pemain melihat angka
            // damage terbang dari badan ular tapi tidak satu pun palang HP, dan kesimpulannya
            // bukan "ekornya bukan sasaran" melainkan "pukulanku tidak masuk".
            float hurt = float.MinValue;
            for (int i = 0; i < segments.Count; i++)
            {
                var s = segments[i];
                if (s != null && s.HurtSeen > hurt) hurt = s.HurtSeen;
            }

            float since = now - hurt;
            if (since < 0f || since > ShowSeconds) return used;

            var head = segments[0];
            float scale = head != null ? Mathf.Max(0.25f, head.Scale) : 1f;

            var world = boss.HeadPos + Vector3.up * (HeadHeight * scale);
            var screen = _camera.WorldToScreenPoint(world) / CanvasScale;
            if (screen.z <= 0f) return used;

            var bar = _pool[used++];
            if (!bar.gameObject.activeSelf) bar.gameObject.SetActive(true);

            bar.transform.localScale = Vector3.one * BossBarScale;
            bar.PlaceAt(new Vector2(screen.x, screen.y));
            bar.Set(boss.HpFraction, Mathf.Clamp01((ShowSeconds - since) / FadeSeconds));

            return used;
        }
    }
}
