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

        readonly EnemyHpBar[] _pool = new EnemyHpBar[PoolSize];
        readonly Camera _camera;
        readonly EnemyManager _enemies;

        public EnemyHpBars(Transform canvas, Camera camera, EnemyManager enemies, GameObject prefab)
        {
            _camera = camera;
            _enemies = enemies;

            for (int i = 0; i < PoolSize; i++)
            {
                GameObject go;

                if (prefab != null)
                {
                    go = Object.Instantiate(prefab, canvas, false);
                }
                else
                {
                    // Tanpa prefab, kolam tetap DIBUAT, cuma kosong. Yang memanggilnya tiap frame
                    // tidak boleh perlu tahu bahwa prefabnya hilang.
                    go = new GameObject("EnemyHpBar", typeof(RectTransform));
                    go.transform.SetParent(canvas, false);
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

        /// <summary>
        /// Digerakkan dengan waktu TAK BERSKALA, sama dengan angka damage: di kecepatan 5x bar
        /// yang ikut waktu permainan hilang sebelum sempat dibaca, dan di jeda antar-wave ia harus
        /// tetap bisa memudar.
        /// </summary>
        public void Tick()
        {
            int used = 0;

            if (_enemies != null && _camera != null)
            {
                var all = _enemies.All;
                float now = Time.unscaledTime;

                for (int i = 0; i < all.Count && used < PoolSize; i++)
                {
                    var e = all[i];
                    if (e == null || !e.Alive) continue;

                    // Ruas boss dilewati: boss sudah punya bar sendiri selebar layar, dan palang
                    // kecil di tiap ruas ular cuma mengulang angka yang sama belasan kali.
                    if (e.Boss != null) continue;

                    float since = now - e.HurtSeen;
                    if (since < 0f || since > ShowSeconds) continue;

                    // Yang belum tergores tidak pernah menampilkan bar penuh: itu tidak
                    // memberi tahu apa pun, dan cuma menambah palang di layar.
                    if (e.MaxHp <= 0f || e.Hp >= e.MaxHp) continue;

                    var world = e.Pos + Vector3.up * (HeadHeight * Mathf.Max(0.25f, e.Scale));
                    var screen = _camera.WorldToScreenPoint(world);

                    // Di belakang kamera WorldToScreenPoint memantulkan titiknya ke sisi yang
                    // salah, dan barnya muncul di seberang layar dari musuh yang dimaksud.
                    if (screen.z <= 0f) continue;

                    var bar = _pool[used++];
                    if (!bar.gameObject.activeSelf) bar.gameObject.SetActive(true);

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
    }
}
