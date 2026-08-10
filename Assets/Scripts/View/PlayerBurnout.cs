using UnityEngine;

namespace Proto
{
    /// <summary>
    /// Pemain ikut hangus saat mati, sama seperti gerombolan yang dibunuhnya.
    ///
    /// Musuh sudah terbakar sejak lama lewat jalur VAT instanced; pemain tidak bisa menumpang di
    /// sana karena ia MeshRenderer biasa tanpa animasi terpanggang. Yang dipinjam bukan kodenya
    /// melainkan aturannya: bangkai tidak menghilang begitu saja — ia digerogoti dari bawah,
    /// menyisakan bara di tepi guntingnya, lalu habis.
    ///
    /// Kenapa ini penting untuk sesuatu yang murni kosmetik: kematian pemain adalah satu-satunya
    /// kejadian di seluruh run yang tidak bisa dibatalkan, dan sebelum ini ia satu-satunya yang
    /// tidak punya gambar sama sekali — kapsulnya berdiri diam sementara layar GAME OVER muncul
    /// di atasnya, seolah run-nya dihentikan dari luar, bukan berakhir di dalam.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerBurnout : MonoBehaviour
    {
        /// <summary>Lama membakar habis, detik. Unscaled — dunia sudah berhenti saat pemain mati.</summary>
        [Min(0.1f)] public float Seconds = 1.15f;

        /// <summary>Tunda sebelum api mulai, supaya pukulan terakhirnya sempat terbaca dulu.</summary>
        [Min(0f)] public float Delay = 0.15f;

        public Color EmberColor = new Color(1f, 0.45f, 0.08f, 1f);

        PlayerCaster _caster;
        Renderer _renderer;
        MaterialPropertyBlock _mpb;

        static readonly int BurnId = Shader.PropertyToID("_Burn");
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int EdgeColorId = Shader.PropertyToID("_EdgeColor");

        float _age = -1f;

        public void Init(PlayerCaster caster)
        {
            _caster = caster;
            _renderer = GetComponentInChildren<Renderer>();

            if (_renderer == null) return;

            var shader = Shader.Find("Grimoire/BurnAway");
            if (shader == null)
            {
                Debug.LogWarning("[PlayerBurnout] shader Grimoire/BurnAway tidak ketemu — " +
                                 "pemain tetap mati tanpa terbakar.", this);
                _renderer = null;
                return;
            }

            // Warna kapsulnya dibawa ke material baru supaya yang berubah cuma CARA ia hilang,
            // bukan rupanya selama masih hidup.
            var before = _renderer.sharedMaterial != null && _renderer.sharedMaterial.HasProperty(BaseColorId)
                ? _renderer.sharedMaterial.GetColor(BaseColorId)
                : new Color(0.85f, 0.78f, 0.55f, 1f);

            // Instance sendiri, bukan sharedMaterial: yang diubah nilainya per-objek, dan
            // menulis ke material bersama akan mewarnai apa pun yang kebetulan memakainya.
            var mat = new Material(shader) { hideFlags = HideFlags.DontSave };
            mat.SetColor(BaseColorId, before);
            mat.SetColor(EdgeColorId, EmberColor);
            mat.SetFloat(BurnId, 0f);

            _renderer.material = mat;
            _mpb = new MaterialPropertyBlock();
        }

        void LateUpdate()
        {
            if (_caster == null || _renderer == null) return;

            if (_caster.Alive)
            {
                // Bangkit lagi (ulang run tanpa memuat ulang scene) harus mengembalikan tubuhnya.
                if (_age >= 0f)
                {
                    _age = -1f;
                    Push(0f);
                    _renderer.enabled = true;
                }

                return;
            }

            if (_age < 0f) _age = 0f;
            _age += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01((_age - Delay) / Seconds);
            Push(t);

            // Dimatikan di ujung, bukan dibiarkan ter-clip habis: sebuah mesh yang seluruh
            // fragmennya dibuang tetap membayar draw call-nya setiap frame.
            if (t >= 1f) _renderer.enabled = false;
        }

        void Push(float burn)
        {
            _renderer.GetPropertyBlock(_mpb);
            _mpb.SetFloat(BurnId, burn);
            _renderer.SetPropertyBlock(_mpb);
        }
    }
}
