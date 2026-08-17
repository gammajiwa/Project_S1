using UnityEngine;

namespace Proto
{
    /// <summary>
    /// Mengisi data global shader <c>Grimoire/PropSeeThrough</c> tiap frame: posisi layar
    /// pemain, kedalaman matanya, dan jari-jari lubang. Pohon yang memakai shader itu
    /// melubangi dirinya dengan pola dither di sekitar pemain — hanya piksel yang berdiri
    /// DI DEPAN pemain (lebih dekat ke kamera) yang tembus, jadi pohon di belakang pemain
    /// tetap pejal.
    ///
    /// Duduk di GameObject kamera, sebelah CameraShake. WorldToViewportPoint dipilih karena
    /// nilainya sama jujurnya di kamera ortografis maupun perspektif — z-nya jarak dunia
    /// dari kamera, persis yang dibandingkan shader dengan -z ruang view per piksel.
    /// </summary>
    public class SeeThroughFeeder : MonoBehaviour
    {
        static readonly int DataId = Shader.PropertyToID("_SeeThroughData");
        static readonly int StrengthId = Shader.PropertyToID("_SeeThroughStrength");

        [Tooltip("Jari-jari lubang, sebagai porsi TINGGI layar. 0,14 kira-kira dua badan pemain.")]
        [Range(0.02f, 0.5f)] public float Radius = 0.14f;

        [Tooltip("Seberapa tembus pusat lubangnya. 1 = hilang penuh; 0,85 menyisakan bayangan " +
                 "dither tipis supaya pohonnya masih terasa ADA, cuma minggir dari mata.")]
        [Range(0f, 1f)] public float Strength = 0.85f;

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
        }

        // Dimatikan = lubangnya ikut mati. Tanpa ini scene lain yang kebetulan memakai
        // shadernya mewarisi lubang milik run yang sudah bubar.
        void OnDisable() => Shader.SetGlobalFloat(StrengthId, 0f);
    }
}
