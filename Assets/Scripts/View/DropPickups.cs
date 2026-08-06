using System;
using System.Collections.Generic;
using UnityEngine;

namespace Proto
{
    /// <summary>
    /// Barang jatuhan sebagai benda nyata di lapangan, bukan angka yang tiba-tiba muncul di tas.
    ///
    /// Ini murni soal keterbacaan hadiah. Menambahkan piece langsung ke kantong sudah benar secara
    /// mekanik dan sama sekali tidak terasa: tidak ada apa pun di layar yang menandai bahwa sesuatu
    /// baru saja didapat. Jadi barangnya dilempar, memantul di dekat pemain, lalu tersedot masuk.
    ///
    /// Pemain berjalan otomatis dan tidak bisa disuruh memungut, jadi magnetnya harus MENJEMPUT,
    /// bukan menunggu diinjak — plus batas waktu yang menyerahkannya begitu saja. Hadiah yang bisa
    /// hilang karena pemain kebetulan lari ke arah lain adalah hukuman untuk sesuatu yang bukan
    /// keputusan pemain.
    /// </summary>
    public class DropPickups : MonoBehaviour
    {
        class Pickup
        {
            public Transform T;
            public PieceDefinition Piece;
            public Vector3 Velocity;
            public float Age;
            public bool Active;
        }

        const float TossHeight = 4.2f;
        const float Gravity = 14f;

        /// <summary>Jarak saat magnet mulai menjemput.</summary>
        const float MagnetRange = 7f;

        /// <summary>Sedekat ini dianggap terpungut.</summary>
        const float CollectRange = 0.9f;

        /// <summary>Setelah sekian detik barangnya diserahkan begitu saja, di mana pun ia berada.</summary>
        const float Patience = 6f;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        readonly List<Pickup> _pool = new List<Pickup>();

        Transform _player;
        Action<PieceDefinition> _onCollect;
        Material _material;
        MaterialPropertyBlock _mpb;

        public void Init(Transform player, Action<PieceDefinition> onCollect)
        {
            _player = player;
            _onCollect = onCollect;
            _mpb = new MaterialPropertyBlock();

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            _material = new Material(shader) { enableInstancing = true };
        }

        /// <summary>Melempar satu piece ke lapangan di dekat pemain.</summary>
        public void Toss(PieceDefinition piece)
        {
            if (piece == null || _player == null)
            {
                // Tanpa lapangan untuk dilempari, hadiahnya tetap harus sampai. Menjatuhkannya
                // diam-diam berarti pemain kehilangan drop karena alasan teknis.
                _onCollect?.Invoke(piece);
                return;
            }

            var p = Take();

            float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            Vector3 outward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));

            p.Piece = piece;
            p.Age = 0f;
            p.Active = true;

            // Berangkat dari pemain, bukan dari tempat musuh mati. Tempat matinya bisa di seberang
            // peta, dan barang yang mendarat di sana tidak pernah terbaca sebagai hadiah.
            p.T.position = _player.position + Vector3.up * 0.8f;
            p.Velocity = outward * UnityEngine.Random.Range(2.5f, 4.5f) + Vector3.up * TossHeight;

            _mpb.SetColor(BaseColorId, piece.Color);
            p.T.GetComponent<Renderer>().SetPropertyBlock(_mpb);

            p.T.localScale = Vector3.one * (0.32f + piece.Stars * 0.05f);
            p.T.gameObject.SetActive(true);
        }

        void Update()
        {
            if (_player == null) return;

            float dt = Time.deltaTime;
            Vector3 target = _player.position;

            for (int i = 0; i < _pool.Count; i++)
            {
                var p = _pool[i];
                if (!p.Active) continue;

                p.Age += dt;

                Vector3 pos = p.T.position;
                Vector3 toPlayer = target - pos;
                toPlayer.y = 0f;

                float distance = toPlayer.magnitude;

                if (distance <= CollectRange || p.Age >= Patience)
                {
                    Collect(p);
                    continue;
                }

                if (distance <= MagnetRange)
                {
                    // Semakin dekat semakin kencang, jadi ujungnya menyentak masuk alih-alih
                    // merayap. Kecepatan tetap terbaca seperti barangnya ragu-ragu.
                    float pull = Mathf.Lerp(16f, 5f, distance / MagnetRange);
                    p.Velocity.x = toPlayer.x / distance * pull;
                    p.Velocity.z = toPlayer.z / distance * pull;
                }

                p.Velocity.y -= Gravity * dt;
                pos += p.Velocity * dt;

                // Memantul sekali lalu diam di lantai. Tanpa lantai, barangnya terus jatuh menembus.
                if (pos.y < 0.35f)
                {
                    pos.y = 0.35f;
                    p.Velocity.y = p.Velocity.y < -2f ? -p.Velocity.y * 0.35f : 0f;
                }

                p.T.position = pos;
                p.T.Rotate(35f * dt, 130f * dt, 0f, Space.Self);
            }
        }

        void Collect(Pickup p)
        {
            p.Active = false;
            p.T.gameObject.SetActive(false);

            _onCollect?.Invoke(p.Piece);
            p.Piece = null;
        }

        Pickup Take()
        {
            for (int i = 0; i < _pool.Count; i++)
            {
                if (!_pool[i].Active) return _pool[i];
            }

            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Pickup";

            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);

            go.transform.SetParent(transform, false);

            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = _material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            go.SetActive(false);

            var fresh = new Pickup { T = go.transform };
            _pool.Add(fresh);
            return fresh;
        }
    }
}
