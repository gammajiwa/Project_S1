using UnityEngine;

namespace Proto
{
    /// <summary>
    /// Mengisi data global shader <c>Grimoire/PropSeeThrough</c> tiap frame: posisi layar
    /// pemain, kedalaman matanya, jari-jari lubang, dan seberapa tipis pusatnya. Pohon yang
    /// memakai shader itu MEMUDAR di sekitar pemain — hanya piksel yang berdiri DI DEPAN
    /// pemain (lebih dekat ke kamera) yang ikut memudar, jadi pohon di belakang pemain
    /// tetap pejal.
    ///
    /// Duduk di GameObject kamera, sebelah CameraShake. WorldToViewportPoint dipilih karena
    /// nilainya sama jujurnya di kamera ortografis maupun perspektif — z-nya jarak dunia
    /// dari kamera, persis yang dibandingkan shader dengan -z ruang view per piksel.
    ///
    /// Ketiga angka di bawah ini disetel dari Inspector selagi main: mengubahnya langsung
    /// terlihat di frame berikutnya, tanpa membangun ulang batch pohon apa pun.
    /// </summary>
    public class SeeThroughFeeder : MonoBehaviour
    {
        static readonly int DataId = Shader.PropertyToID("_SeeThroughData");
        static readonly int StrengthId = Shader.PropertyToID("_SeeThroughStrength");
        static readonly int MinAlphaId = Shader.PropertyToID("_SeeThroughMin");

        [Tooltip("Jari-jari lubang, sebagai porsi TINGGI layar. 0,18 kira-kira dua setengah " +
                 "badan pemain — cukup lebar untuk ikut membuka musuh yang merapat, bukan " +
                 "cuma pemainnya sendiri.")]
        [Range(0.02f, 0.5f)] public float Radius = 0.18f;

        [Tooltip("Seberapa jauh pudarnya berjalan. 1 = penuh sampai Keburaman Pusat; 0 = mati " +
                 "total dan pohonnya pejal seperti prop lain.")]
        [Range(0f, 1f)] public float Strength = 1f;

        [Tooltip("Sisa keburaman DI PUSAT lubang. 0 = pohonnya hilang sama sekali (hutan " +
                 "jadi berlubang saat dilewati); 0,12–0,25 = bayangan tipis yang masih " +
                 "terbaca sebagai pohon. Ini angka pertama yang perlu disetel kalau hasilnya " +
                 "terasa kurang/kelewat tembus.")]
        [Range(0f, 1f)] public float MinAlpha = 0.14f;

        Transform _player;
        Camera _camera;

        public void Init(Transform player, Camera cam)
        {
            _player = player;
            _camera = cam;
        }

        void LateUpdate()
        {
            if (_player == null || _camera == null) return;

            // Setinggi dada, bukan kaki — lubangnya harus memeluk BADAN pemain di layar.
            var vp = _camera.WorldToViewportPoint(_player.position + Vector3.up * 1.1f);

            Shader.SetGlobalVector(DataId, new Vector4(vp.x, vp.y, vp.z, Radius));
            Shader.SetGlobalFloat(StrengthId, Strength);
            Shader.SetGlobalFloat(MinAlphaId, MinAlpha);
        }

        // Dimatikan = pudarnya ikut mati. Tanpa ini scene lain yang kebetulan memakai
        // shadernya mewarisi lubang milik run yang sudah bubar.
        void OnDisable() => Shader.SetGlobalFloat(StrengthId, 0f);
    }
}
