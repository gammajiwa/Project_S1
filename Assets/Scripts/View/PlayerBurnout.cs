using UnityEngine;

namespace Proto
{
    /// <summary>
    /// Pemain ikut hangus saat mati, sama seperti gerombolan yang dibunuhnya.
    ///
    /// Musuh sudah terbakar sejak lama lewat jalur VAT instanced; pemain tidak bisa menumpang di
    /// sana karena ia renderer biasa tanpa animasi terpanggang. Yang dipinjam bukan kodenya
    /// melainkan aturannya: bangkai tidak menghilang begitu saja — ia digerogoti dari bawah,
    /// menyisakan bara di tepi guntingnya, lalu habis.
    ///
    /// Sejak pemain punya badan sungguhan (model + jubah), komponen ini membakar SEMUA renderer
    /// anak sekaligus — badan yang hangus sementara jubahnya tetap utuh melayang adalah gambar
    /// yang lebih aneh daripada tidak terbakar sama sekali. Rentang tinggi tiap mesh dikirim ke
    /// shader supaya jalarannya tetap kaki-ke-kepala pada mesh berukuran apa pun.
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
        Renderer[] _renderers;
        MaterialPropertyBlock _mpb;

        static readonly int BurnId = Shader.PropertyToID("_Burn");
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        static readonly int EdgeColorId = Shader.PropertyToID("_EdgeColor");
        static readonly int HeightMinId = Shader.PropertyToID("_HeightMin");
        static readonly int HeightMaxId = Shader.PropertyToID("_HeightMax");

        float _age = -1f;

        public void Init(PlayerCaster caster)
        {
            _caster = caster;

            var shader = Shader.Find("Grimoire/BurnAway");
            if (shader == null)
            {
                Debug.LogWarning("[PlayerBurnout] shader Grimoire/BurnAway tidak ketemu — " +
                                 "pemain tetap mati tanpa terbakar.", this);
                _renderers = null;
                return;
            }

            // Hanya badan yang dibakar. Player juga menggendong renderer lain yang lahir
            // belakangan (RangeRing pakai LineRenderer) — mereka bukan daging, jangan disentuh.
            //
            // BUKAN DAGING JUGA: quad VFX di buku (sigil Sprites/Default, glow MenuGlow).
            // Versi lama menelan SEMUA MeshRenderer lalu MENGGANTI materialnya sejak Init —
            // sigil ungu dan glow lahir sebagai kotak krem BurnAway polos, dan tiga ronde
            // perbaikan prefab tampak "tidak ngefek" karena dirampas di sini tiap play.
            // Saringannya sekarang: skinned mesh selalu ikut; MeshRenderer hanya ikut kalau
            // material aslinya keluarga Lit yang buram — shader sprite/partikel/glow dilewati.
            var found = new System.Collections.Generic.List<Renderer>();
            foreach (var r in GetComponentsInChildren<Renderer>())
            {
                if (r is SkinnedMeshRenderer) { found.Add(r); continue; }
                if (!(r is MeshRenderer)) continue;

                var m = r.sharedMaterial;
                string s = m != null && m.shader != null ? m.shader.name : "";
                bool vfx = s.Contains("Sprites") || s.Contains("Particles") ||
                           s.Contains("MenuGlow") || s.Contains("Additive") ||
                           s.Contains("AoeRing") || s.Contains("Unlit");
                if (!vfx) found.Add(r);
            }

            _renderers = found.ToArray();
            if (_renderers.Length == 0)
            {
                _renderers = null;
                return;
            }

            foreach (var r in _renderers)
            {
                // Warna aslinya dibawa ke material baru supaya yang berubah cuma CARA ia
                // hilang, bukan rupanya selama masih hidup.
                var before = r.sharedMaterial != null && r.sharedMaterial.HasProperty(BaseColorId)
                    ? r.sharedMaterial.GetColor(BaseColorId)
                    : new Color(0.85f, 0.78f, 0.55f, 1f);

                // Instance sendiri, bukan sharedMaterial: yang diubah nilainya per-objek, dan
                // menulis ke material bersama akan mewarnai apa pun yang kebetulan memakainya.
                var mat = new Material(shader) { hideFlags = HideFlags.DontSave };
                mat.SetColor(BaseColorId, before);
                mat.SetColor(EdgeColorId, EmberColor);
                mat.SetFloat(BurnId, 0f);

                // Teksturnya ikut pindah — material bakar ini MENGGANTIKAN material asli
                // sejak Init, dan tanpa membawa tekstur, model bertekstur hidup sebagai
                // siluet polos sampai mati.
                if (r.sharedMaterial != null && r.sharedMaterial.HasProperty(BaseMapId))
                {
                    var tex = r.sharedMaterial.GetTexture(BaseMapId);
                    if (tex != null) mat.SetTexture(BaseMapId, tex);
                }

                // Rentang tinggi dari mesh-nya sendiri, di ruang objek — kapsul primitif dan
                // model ber-skala impor sama-sama benar tanpa angka tebakan.
                var bounds = LocalBounds(r);
                mat.SetFloat(HeightMinId, bounds.min.y);
                mat.SetFloat(HeightMaxId, bounds.max.y);

                r.material = mat;
            }

            _mpb = new MaterialPropertyBlock();
        }

        static Bounds LocalBounds(Renderer r)
        {
            var skinned = r as SkinnedMeshRenderer;
            if (skinned != null && skinned.sharedMesh != null) return skinned.sharedMesh.bounds;

            var filter = r.GetComponent<MeshFilter>();
            if (filter != null && filter.sharedMesh != null) return filter.sharedMesh.bounds;

            return new Bounds(Vector3.zero, Vector3.one);
        }

        void LateUpdate()
        {
            if (_caster == null || _renderers == null) return;

            if (_caster.Alive)
            {
                // Bangkit lagi (ulang run tanpa memuat ulang scene) harus mengembalikan tubuhnya.
                if (_age >= 0f)
                {
                    _age = -1f;
                    Push(0f, true);
                }

                return;
            }

            if (_age < 0f) _age = 0f;
            _age += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01((_age - Delay) / Seconds);

            // Dimatikan di ujung, bukan dibiarkan ter-clip habis: mesh yang seluruh fragmennya
            // dibuang tetap membayar draw call-nya setiap frame.
            Push(t, t < 1f);
        }

        void Push(float burn, bool visible)
        {
            for (int i = 0; i < _renderers.Length; i++)
            {
                var r = _renderers[i];
                if (r == null) continue;

                r.GetPropertyBlock(_mpb);
                _mpb.SetFloat(BurnId, burn);
                r.SetPropertyBlock(_mpb);
                r.enabled = visible;
            }
        }
    }
}
