using System.Collections.Generic;
using UnityEngine;

namespace Proto
{
    /// <summary>
    /// Coretan gosong yang ditinggalkan semburan api naga di tanah.
    ///
    /// Permintaan pemilik project: setelah beberapa kali naga melintas sambil menyembur,
    /// arenanya harus terlihat "dicoret-coret" — jejak hangus yang bercerita di mana saja
    /// apinya pernah lewat.
    ///
    /// Digambar lewat <c>RenderMeshInstanced</c> seperti semua yang lain di project ini:
    /// tidak ada GameObject per noda, ratusan bekas gosong = satu-dua draw call. Nodanya
    /// dijatuhkan sepanjang jalur api dengan jarak minimum, jadi satu lintasan menghasilkan
    /// GARIS noda yang menyambung — itulah coretannya.
    /// </summary>
    public class ScorchMarks
    {
        /// <summary>Noda paling banyak. Melewati ini, yang paling tua ditimpa duluan.</summary>
        const int Capacity = 320;

        /// <summary>Umur satu noda, detik. Fade-out menempati seperempat terakhirnya.</summary>
        const float Life = 55f;

        /// <summary>Jarak minimum antar noda. Lebih rapat = coretan lebih pekat, buffer lebih boros.</summary>
        const float Gap = 1.15f;

        /// <summary>Batas instance per panggilan gambar, jauh di bawah batas 1023 milik Unity.</summary>
        const int Chunk = 256;

        struct Mark
        {
            public Vector3 Pos;
            public float Yaw;
            public float Size;
            public float Age;
            public float Seed;
        }

        readonly Mark[] _marks = new Mark[Capacity];
        int _head;
        int _count;

        Vector3 _lastDrop = new Vector3(9999f, 0f, 9999f);

        readonly Mesh _quad;
        readonly Material _material;
        readonly Matrix4x4[] _matrices = new Matrix4x4[Chunk];
        readonly float[] _fade = new float[Chunk];
        readonly float[] _seed = new float[Chunk];
        readonly MaterialPropertyBlock _block = new MaterialPropertyBlock();

        static readonly int FadeId = Shader.PropertyToID("_Fade");
        static readonly int SeedId = Shader.PropertyToID("_Seed");

        public ScorchMarks()
        {
            _quad = EnemyRenderer.BorrowPrimitiveMesh(PrimitiveType.Quad);

            var shader = Shader.Find("Grimoire/Scorch");

            // Tanpa shadernya, noda digambar tidak sama sekali — dan itu pilihan yang benar:
            // magenta di sekujur tanah lebih merusak daripada coretan yang absen.
            if (shader != null)
            {
                _material = new Material(shader) { enableInstancing = true };
            }
        }

        /// <summary>
        /// Menjatuhkan satu noda di titik ini, kalau sudah cukup jauh dari noda terakhir.
        /// Dipanggil tiap frame selama api menyala — penjaga jaraknya yang mengubah hujan
        /// panggilan itu jadi barisan noda berjarak rapi.
        /// </summary>
        public void Drop(Vector3 at)
        {
            at.y = 0f;

            Vector3 d = at - _lastDrop;
            d.y = 0f;
            if (d.sqrMagnitude < Gap * Gap) return;

            _lastDrop = at;

            // Digeser dan diputar acak. Coretan dari noda yang persis segaris dan searah
            // terbaca sebagai sablon; geseran kecil yang membuatnya terbaca sebagai bekas
            // api yang menjilat ke mana-mana.
            at.x += Random.Range(-0.5f, 0.5f);
            at.z += Random.Range(-0.5f, 0.5f);
            at.y = 0.04f;   // sedikit di atas tanah supaya tidak berkelahi dengan z-buffer

            int slot = (_head + _count) % Capacity;

            if (_count < Capacity) _count++;
            else _head = (_head + 1) % Capacity;   // penuh: yang tertua memberi tempat

            _marks[slot] = new Mark
            {
                Pos = at,
                Yaw = Random.Range(0f, 360f),
                Size = Random.Range(2.0f, 3.4f),
                Age = 0f,
                Seed = Random.Range(0f, 40f),
            };
        }

        public void Clear()
        {
            _count = 0;
            _head = 0;
            _lastDrop = new Vector3(9999f, 0f, 9999f);
        }

        public void Draw(float dt)
        {
            if (_material == null || _count == 0) return;

            int pending = 0;

            for (int i = 0; i < _count; i++)
            {
                int idx = (_head + i) % Capacity;
                _marks[idx].Age += dt;

                var m = _marks[idx];
                if (m.Age >= Life) continue;   // yang habis dibiarkan tergilas ring buffer

                // Muncul cepat (terbakar itu seketika), pudar pelan di seperempat akhir.
                float appear = Mathf.Clamp01(m.Age / 0.25f);
                float vanish = Mathf.Clamp01((Life - m.Age) / (Life * 0.25f));

                _matrices[pending] = Matrix4x4.TRS(m.Pos,
                    Quaternion.Euler(90f, m.Yaw, 0f), new Vector3(m.Size, m.Size, 1f));
                _fade[pending] = appear * vanish;
                _seed[pending] = m.Seed;
                pending++;

                if (pending == Chunk) { Flush(pending); pending = 0; }
            }

            if (pending > 0) Flush(pending);
        }

        void Flush(int count)
        {
            _block.SetFloatArray(FadeId, _fade);
            _block.SetFloatArray(SeedId, _seed);

            var rp = new RenderParams(_material)
            {
                matProps = _block,
                shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off,
                receiveShadows = false,
                worldBounds = new Bounds(Vector3.zero, Vector3.one * 400f),
            };

            Graphics.RenderMeshInstanced(rp, _quad, 0, _matrices, count);
        }
    }
}
