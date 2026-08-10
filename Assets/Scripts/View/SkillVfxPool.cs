using System.Collections.Generic;
using UnityEngine;

namespace Proto
{
    /// <summary>
    /// Kolam instans prefab VFX skill, dipisah per prefab sumber.
    ///
    /// Satu kolam untuk semua kind karena masalahnya sama di mana-mana: peluru, orb, dan bola
    /// menggelinding dipakai ulang dari pool masing-masing, dan skill yang bergiliran memakai
    /// slot yang sama akan saling menukar efek. Kunci kamusnya PREFAB SUMBER — instans hanya
    /// pernah kembali ke tumpukan prefabnya sendiri, jadi tidak ada jalur yang bisa membuat
    /// Fireball meluncur membawa ekor es.
    ///
    /// Instans baru langsung DIPRERETELI: Light, AudioSource, dan seluruh script CFXR_* dicabut.
    /// Lampu per efek adalah harga render yang tidak pernah kita setujui, audio punya sistemnya
    /// sendiri, dan CFXR_Effect menghancurkan GameObject-nya sendiri selesai main — musuh bebuyutan
    /// pooling, karena slot yang "selesai" di sini justru sedang menunggu dipakai lagi.
    /// </summary>
    public class SkillVfxPool
    {
        readonly Transform _root;
        readonly Dictionary<GameObject, Stack<Transform>> _idle = new Dictionary<GameObject, Stack<Transform>>();

        class OneShot
        {
            public GameObject Src;
            public Transform T;
            public float Life;

            /// <summary>Sudah disuruh berhenti memancarkan; tinggal menunggu partikel terakhir mati.</summary>
            public bool Stopping;
        }

        readonly List<OneShot> _live = new List<OneShot>(32);

        /// <summary>Umur sekali-main per prefab, dihitung sekali. −1 berarti prefabnya LOOP.</summary>
        readonly Dictionary<GameObject, float> _lifeCache = new Dictionary<GameObject, float>();

        /// <summary>
        /// Pagar jumlah efek sekali-main yang hidup bersamaan. Detonate bisa meledakkan puluhan
        /// musuh sekali tagih; tanpa pagar, satu cast ramai menumbuhkan kolam yang tidak pernah
        /// menyusut lagi — pola yang sama dengan <c>MaxFlashes</c> di PlayerCaster.
        /// </summary>
        const int MaxLiveOneShots = 64;

        /// <summary>Umur paksaan untuk prefab loop yang dipakai sebagai semburan sekali-main.</summary>
        const float LoopBurstLife = 0.85f;

        /// <summary>Jeda setelah berhenti memancar, supaya partikel yang sudah lahir sempat mati.</summary>
        const float FadeGrace = 0.7f;

        public SkillVfxPool(Transform root)
        {
            _root = root;
        }

        /// <summary>
        /// Mengambil satu instans (dari tumpukan, atau lahir baru), menempatkannya, lalu
        /// menyalakannya dari nol. Pemanggil memegang transform-nya — menggeser tiap frame dan
        /// mengembalikannya lewat <see cref="Release"/> adalah tanggung jawabnya.
        /// </summary>
        public Transform Attach(GameObject prefab, Vector3 pos, Quaternion rot, float scale)
        {
            if (prefab == null) return null;

            Transform t = Take(prefab);

            // Posisi diatur SEBELUM aktif: TrailRenderer menulis titik pertamanya saat bangun,
            // dan instans pool yang bangun di posisi lama menyeret coretan melintasi arena.
            t.SetPositionAndRotation(pos, rot);
            t.localScale = Vector3.one * Mathf.Max(0.05f, scale);
            t.gameObject.SetActive(true);

            Restart(t);
            return t;
        }

        /// <summary>Mengembalikan instans ke tumpukan prefabnya. Aman dipanggil dengan null.</summary>
        public void Release(GameObject prefab, Transform inst)
        {
            if (inst == null) return;

            inst.gameObject.SetActive(false);

            if (prefab == null)
            {
                // Tidak tahu asalnya = tidak boleh masuk tumpukan mana pun.
                Object.Destroy(inst.gameObject);
                return;
            }

            if (!_idle.TryGetValue(prefab, out var stack))
            {
                stack = new Stack<Transform>();
                _idle.Add(prefab, stack);
            }

            stack.Push(inst);
        }

        /// <summary>Semburan sekali-main tanpa arah. Selesai sendiri, kembali ke kolam sendiri.</summary>
        public void Burst(GameObject prefab, Vector3 pos, float scale)
        {
            Burst(prefab, pos, scale, Quaternion.identity, 0f);
        }

        /// <summary>
        /// Semburan sekali-main dengan arah dan (opsional) umur paksaan. Umur paksaan dipakai
        /// prefab loop macam semburan api: tanpa itu ia menyala selamanya.
        /// </summary>
        public void Burst(GameObject prefab, Vector3 pos, float scale, Quaternion rot, float forceLife)
        {
            if (prefab == null) return;

            if (_live.Count >= MaxLiveOneShots) RecycleSoonest();

            float life = forceLife > 0f ? forceLife : LifeOf(prefab);
            if (life < 0f) life = LoopBurstLife;

            var t = Attach(prefab, pos, rot, scale);
            _live.Add(new OneShot { Src = prefab, T = t, Life = life, Stopping = false });
        }

        /// <summary>Menghitung mundur semburan yang hidup. Dipanggil sekali per frame.</summary>
        public void Tick(float dt)
        {
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                var s = _live[i];
                s.Life -= dt;
                if (s.Life > 0f) continue;

                if (!s.Stopping)
                {
                    // Dua tahap: berhenti memancar dulu, matikan belakangan. Memutus lampu pada
                    // prefab loop meninggalkan partikel setengah umur terpotong di udara.
                    StopEmitting(s.T);
                    s.Stopping = true;
                    s.Life = FadeGrace;
                    continue;
                }

                Release(s.Src, s.T);
                _live.RemoveAt(i);
            }
        }

        /// <summary>Kolam jenuh: korbankan yang paling dekat selesai, bukan yang baru lahir.</summary>
        void RecycleSoonest()
        {
            int soonest = 0;
            for (int i = 1; i < _live.Count; i++)
            {
                if (_live[i].Life < _live[soonest].Life) soonest = i;
            }

            Release(_live[soonest].Src, _live[soonest].T);
            _live.RemoveAt(soonest);
        }

        Transform Take(GameObject prefab)
        {
            if (_idle.TryGetValue(prefab, out var stack))
            {
                while (stack.Count > 0)
                {
                    var pooled = stack.Pop();
                    if (pooled != null) return pooled;
                }
            }

            var go = Object.Instantiate(prefab, _root);
            go.name = "SkillVfx " + prefab.name;
            go.SetActive(false);
            Strip(go);
            return go.transform;
        }

        /// <summary>
        /// Mencabut yang tidak boleh ikut: lampu, audio, dan script paket efek. Sekali saat lahir,
        /// bukan tiap pakai.
        /// </summary>
        static void Strip(GameObject go)
        {
            foreach (var light in go.GetComponentsInChildren<Light>(true)) Object.Destroy(light);
            foreach (var audio in go.GetComponentsInChildren<AudioSource>(true)) Object.Destroy(audio);

            foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true))
            {
                // Nama, bukan tipe: CFXR hidup di assembly-nya sendiri dan kode game tidak
                // mereferensikannya. Yang penting cuma satu — CFXR_Effect punya mode
                // menghancurkan diri selesai main, dan itu mencuri instans dari kolam.
                if (mb != null && mb.GetType().Name.StartsWith("CFXR_")) Object.Destroy(mb);
            }
        }

        static void Restart(Transform t)
        {
            foreach (var ps in t.GetComponentsInChildren<ParticleSystem>(true))
            {
                ps.Clear();
                ps.Play();
            }

            foreach (var trail in t.GetComponentsInChildren<TrailRenderer>(true)) trail.Clear();
        }

        static void StopEmitting(Transform t)
        {
            if (t == null) return;

            foreach (var ps in t.GetComponentsInChildren<ParticleSystem>(true))
            {
                ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        /// <summary>
        /// Umur satu kali main: durasi terpanjang + umur partikel terpanjangnya. Prefab yang punya
        /// SATU saja sistem loop dianggap loop seluruhnya (−1) — umurnya urusan pemanggil.
        /// </summary>
        float LifeOf(GameObject prefab)
        {
            if (_lifeCache.TryGetValue(prefab, out float cached)) return cached;

            float life = 0.4f;
            bool loops = false;

            foreach (var ps in prefab.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = ps.main;
                if (main.loop)
                {
                    loops = true;
                    break;
                }

                life = Mathf.Max(life, main.duration + main.startLifetime.constantMax);
            }

            float result = loops ? -1f : Mathf.Clamp(life, 0.4f, 6f);
            _lifeCache.Add(prefab, result);
            return result;
        }
    }
}
