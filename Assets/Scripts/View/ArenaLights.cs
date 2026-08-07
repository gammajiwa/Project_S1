using UnityEngine;

namespace Proto
{
    /// <summary>
    /// Beberapa lampu titik lembut yang mengembara pelan di arena.
    ///
    /// Matahari terarah memberi bentuk, tapi ia menyinari SEMUANYA sama rata — dan lapangan yang
    /// terang merata tidak punya tempat yang layak dituju. Kolam-kolam cahaya inilah yang membuat
    /// lantai punya daerah: satu sudut lebih hangat dari yang lain, dan mata langsung tahu ke mana
    /// harus melihat.
    ///
    /// Mengembara pelan dan tidak pernah menempel ke pemain. Lampu yang mengikuti pemain berhenti
    /// jadi tempat dan berubah jadi senter — dan senter tidak memberi tahu apa pun tentang
    /// lapangannya.
    /// </summary>
    public class ArenaLights : MonoBehaviour
    {
        struct Lamp
        {
            public Light Source;
            public Vector2 Home;
            public float Phase;
            public float Sway;
            public float BaseIntensity;
        }

        Lamp[] _lamps = new Lamp[0];
        GameBalance _balance;

        public int Count => _lamps.Length;

        public void Init(GameBalance balance, BiomeDefinition biome)
        {
            _balance = balance;
            Apply(biome);
        }

        /// <summary>
        /// Membangun ulang seluruh lampu untuk wajah arena yang sekarang.
        ///
        /// Wajib bisa dipanggil BERKALI-KALI. Versi pertama membangun lampunya sekali di awal dari
        /// wajah pertama, dan ketika siang/malam menjadi dua aset, malam tidak pernah mendapat satu
        /// lampu pun — wajahnya berganti, cahayanya tidak. Gagalnya diam: malam tetap tergambar,
        /// cuma tanpa satu pun sumber hangat, dan yang terlihat adalah malam yang datar.
        /// </summary>
        public void Apply(BiomeDefinition biome)
        {
            for (int i = 0; i < _lamps.Length; i++)
            {
                if (_lamps[i].Source != null) Destroy(_lamps[i].Source.gameObject);
            }

            _lamps = new Lamp[0];

            if (biome == null || _balance == null) return;

            var balance = _balance;
            int count = Mathf.Clamp(biome.LampCount, 0, 12);
            if (count == 0) return;

            _lamps = new Lamp[count];

            var state = Random.state;
            Random.InitState(4451);

            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("Lamp" + i);
                go.transform.SetParent(transform, false);

                // Disebar melingkar, bukan acak penuh. Acak penuh sering menumpuk dua lampu di
                // satu sudut dan meninggalkan separuh arena gelap gulita.
                float angle = (i / (float)count) * Mathf.PI * 2f + Random.Range(-0.4f, 0.4f);
                float radius = Random.Range(0.35f, 0.8f);

                var home = new Vector2(
                    Mathf.Cos(angle) * balance.ArenaHalfX * radius,
                    Mathf.Sin(angle) * balance.ArenaHalfZ * radius);

                var light = go.AddComponent<Light>();
                light.type = LightType.Point;

                // Bergantian hangat–dingin, bukan satu warna untuk semuanya.
                //
                // Malam yang seluruh lampunya sewarna cuma punya satu suhu, dan satu suhu berarti
                // tidak ada yang bisa dibandingkan — semuanya sama birunya atau sama jingganya, dan
                // lapangannya kembali rata seperti tanpa lampu. Yang membuat malam terbaca indah
                // adalah kolam hangat yang duduk di tengah kegelapan BIRU, dan itu menuntut
                // keduanya ada sekaligus.
                light.color = i % 2 == 0 ? biome.LampColor : biome.LampColorCool;
                light.intensity = biome.LampIntensity;
                light.range = biome.LampRange;

                // Bayangan MATI. Lampu titik berbayang di atas ratusan musuh adalah satu pass
                // bayangan tambahan per lampu, dan tidak satu pun dari bayangan itu terbaca di
                // kamera yang menunduk.
                light.shadows = LightShadows.None;

                go.transform.position = new Vector3(home.x, biome.LampHeight, home.y);

                _lamps[i] = new Lamp
                {
                    Source = light,
                    Home = home,
                    Phase = Random.Range(0f, 100f),
                    Sway = Random.Range(3f, 7f),
                    BaseIntensity = biome.LampIntensity
                };
            }

            Random.state = state;
        }

        void Update()
        {
            for (int i = 0; i < _lamps.Length; i++)
            {
                var lamp = _lamps[i];
                float t = Time.time * 0.12f + lamp.Phase;

                // Perlin, bukan sinus. Sinus menghasilkan ayunan yang berulang persis dan mata
                // menangkap polanya dalam beberapa detik; Perlin tidak pernah mengulang.
                float x = (Mathf.PerlinNoise(t, lamp.Phase) - 0.5f) * 2f * lamp.Sway;
                float z = (Mathf.PerlinNoise(lamp.Phase, t) - 0.5f) * 2f * lamp.Sway;

                lamp.Source.transform.position = new Vector3(
                    lamp.Home.x + x, lamp.Source.transform.position.y, lamp.Home.y + z);

                // Denyut halus. Cukup untuk terbaca sebagai hidup, terlalu kecil untuk terbaca
                // sebagai berkedip.
                float pulse = 0.88f + Mathf.PerlinNoise(t * 3f, 0f) * 0.24f;
                lamp.Source.intensity = lamp.BaseIntensity * pulse;
            }
        }
    }
}
