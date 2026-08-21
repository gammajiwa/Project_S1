using System.Collections.Generic;
using UnityEngine;

namespace Proto
{
    /// <summary>
    /// Cahaya sesaat di tiap tempat sebuah skill terjadi.
    ///
    /// Permintaan pemilik project: <i>"setiap vfx kasih cahaya, warnanya sama, jangan terlalu
    /// terang — waktu malam hari nge-skill terlalu gelap, aneh rasanya kalo gak ada cahaya"</i>.
    /// Benar: efek partikel menggambar dirinya sendiri terang, tapi ia tidak menyinari apa pun.
    /// Ledakan yang tidak menerangi tanah di bawahnya terbaca sebagai stiker yang ditempel di
    /// layar, bukan sebagai sesuatu yang terjadi di dunia.
    ///
    /// <b>KOLAM, bukan satu lampu per efek.</b> Ini bukan kerapian, ini syarat hidup: wave ramai
    /// melahirkan belasan kilatan per detik dan ratusan efek hidup bersamaan. URP Forward memilih
    /// lampu tambahan per objek dengan batas yang kecil, dan ratusan lampu yang bersaing untuk
    /// slot itu bukan cuma lambat — ia berkedip, karena objek yang sama bisa memenangkan lampu
    /// berbeda dari frame ke frame.
    ///
    /// Jumlahnya dibatasi keras. Kalau semuanya terpakai, yang PALING DEKAT SELESAI dirampas —
    /// aturan yang sama dengan kolam kilatan, jadi cahaya dan kilatan tidak pernah berpisah
    /// karena kebijakan daur ulang yang berbeda.
    /// </summary>
    public class SkillLightPool
    {
        /// <summary>
        /// Sepuluh. Angka ini bukan hasil tebakan estetis melainkan batas praktis: di URP Forward,
        /// tiap objek cuma menerima beberapa lampu tambahan, dan menambah lampu di atas jumlah itu
        /// tidak menambah cahaya — ia cuma menambah persaingan dan kedipan.
        /// </summary>
        const int Capacity = 10;

        class Bulb
        {
            public Light Source;
            public float Life;
            public float MaxLife;
            public float Peak;
        }

        readonly List<Bulb> _pool = new List<Bulb>(Capacity);
        readonly Transform _root;

        /// <summary>Pengali intensitas global. Satu tuas untuk "kegelapan" seluruh permainan.</summary>
        public float Brightness = 1f;

        /// <summary>Pengali jangkauan. Dipisah dari terang: cahaya redup yang luas dan cahaya
        /// terang yang sempit adalah dua rasa yang berbeda.</summary>
        public float Reach = 1f;

        public SkillLightPool(Transform root) => _root = root;

        /// <summary>
        /// Satu denyut cahaya. <paramref name="size"/> memakai satuan yang sama dengan kilatan
        /// yang memanggilnya, jadi ledakan besar menyinari lebih jauh tanpa ada yang perlu
        /// mengarang angka kedua.
        /// </summary>
        public void Pulse(Vector3 at, Color tint, float size, float life)
        {
            if (Brightness <= 0.001f || life <= 0f) return;

            var bulb = Take();
            if (bulb == null) return;

            bulb.Source.transform.position = at + Vector3.up * 0.6f;
            bulb.Source.color = tint;

            // Jangkauan diikat ke ukuran kilatannya, dengan lantai supaya kilatan kecil tetap
            // menyentuh tanah. Tanpa lantai itu, tumbukan peluru — kejadian paling sering di
            // seluruh permainan — menyalakan lampu yang radiusnya lebih kecil dari musuhnya.
            bulb.Source.range = Mathf.Max(3.5f, size * 2.4f) * Mathf.Max(0.1f, Reach);

            // Umurnya sedikit lebih panjang dari kilatannya. Cahaya yang mati bersamaan dengan
            // partikelnya terbaca sebagai saklar; yang tertinggal sesaat terbaca sebagai
            // sesuatu yang benar-benar memancar lalu padam.
            bulb.MaxLife = Mathf.Max(0.08f, life * 1.6f);
            bulb.Life = bulb.MaxLife;

            bulb.Peak = Mathf.Clamp(size * 0.9f, 1.5f, 14f) * Mathf.Max(0f, Brightness);
            bulb.Source.intensity = bulb.Peak;
            bulb.Source.enabled = true;
        }

        public void Tick(float dt)
        {
            for (int i = 0; i < _pool.Count; i++)
            {
                var b = _pool[i];
                if (b.Life <= 0f) continue;

                b.Life -= dt;

                if (b.Life <= 0f)
                {
                    b.Source.enabled = false;
                    continue;
                }

                // Kuadrat, bukan lurus. Peluruhan lurus menghabiskan separuh umurnya masih
                // setengah terang, dan yang tampil bukan kilatan melainkan lampu yang diredupkan.
                float t = b.Life / b.MaxLife;
                b.Source.intensity = b.Peak * t * t;
            }
        }

        Bulb Take()
        {
            for (int i = 0; i < _pool.Count; i++)
            {
                if (_pool[i].Life <= 0f) return _pool[i];
            }

            if (_pool.Count < Capacity)
            {
                var go = new GameObject("SkillLight");
                go.transform.SetParent(_root, false);

                var light = go.AddComponent<Light>();
                light.type = LightType.Point;

                // Tanpa bayangan, selamanya. Sepuluh lampu berbayang di atas gerombolan 500 musuh
                // adalah sepuluh shadow pass untuk cahaya yang hidup seperlima detik.
                light.shadows = LightShadows.None;
                light.renderMode = LightRenderMode.ForceVertex;
                light.enabled = false;

                var fresh = new Bulb { Source = light };
                _pool.Add(fresh);
                return fresh;
            }

            // Jenuh: rampas yang paling dekat selesai.
            var weakest = _pool[0];
            for (int i = 1; i < _pool.Count; i++)
            {
                if (_pool[i].Life < weakest.Life) weakest = _pool[i];
            }

            return weakest;
        }
    }
}
