using UnityEngine;
using UnityEngine.Rendering;

namespace Proto
{
    /// <summary>
    /// Draws the whole swarm without giving any enemy a GameObject.
    ///
    /// Before this, every enemy was a Capsule with its own Renderer, tinted through a
    /// MaterialPropertyBlock. An MPB opts that renderer out of the SRP Batcher, so 200 enemies meant
    /// 200 draw calls that nothing could merge — and that is the budget the VFX still have to fit
    /// into. Here the same 200 go out as one instanced call per tint.
    ///
    /// Tints are buckets rather than per-instance colours on purpose: an enemy can only ever look
    /// like one of a fixed, small set (plain, one of seven ailments, or an ailment washed toward
    /// white to warn that a reaction is one hit away). A handful of materials answers that without
    /// needing a custom shader, and no custom shader means nothing to go pink on a different
    /// pipeline.
    ///
    /// <b>Toward animation.</b> Instanced rendering rules out Animator and SkinnedMeshRenderer, so
    /// animation has to be baked. The seam is already here: every instance carries a phase and a
    /// yaw, and <see cref="Compose"/> is the single place an instance's transform is decided. A
    /// vertex-animation-texture shader needs one more per-instance value — a normalised animation
    /// time — pushed through a MaterialPropertyBlock float array. The mesh, the material and the
    /// batching all stay exactly as they are.
    /// </summary>
    public class EnemyRenderer
    {
        /// <summary>Hard ceiling of Graphics.RenderMeshInstanced. Bigger buckets are split.</summary>
        const int MaxPerDraw = 1023;

        static readonly int VatClipId = Shader.PropertyToID("_VatClip");

        readonly float _bodyScale;
        readonly bool _animate;

        readonly Mesh _mesh;
        readonly Material[] _materials;
        readonly RenderParams[] _params;

        readonly Vector3[] _stagePos;
        readonly float[] _stageYaw;
        readonly float[] _stagePhase;
        readonly Vector3[] _stageScale;
        readonly int[] _stageTint;

        // ---- animasi panggangan; semuanya null kalau renderer ini tidak memakai VAT ----

        readonly VatClipSet _vat;

        /// <summary>Per instance: x = baris awal klip, y = jumlah baris, z = maju 0..1.</summary>
        readonly Vector4[] _vatArgs;

        /// <summary>
        /// Potongan yang benar-benar dikirim per draw. Panjangnya TETAP <see cref="MaxPerDraw"/>
        /// dan tidak pernah berubah: MaterialPropertyBlock mengunci ukuran array pada pengiriman
        /// pertama, dan mengirim panjang yang berbeda di draw berikutnya diam-diam tidak
        /// berpengaruh — instance-nya tergambar memakai data draw sebelumnya.
        /// </summary>
        readonly Vector4[] _vatChunk;

        readonly MaterialPropertyBlock _vatBlock;
        readonly float[] _stageSpeed;

        VatClip _clipIdle;
        VatClip _clipWalk;
        VatClip _clipRun;

        readonly Matrix4x4[] _instances;
        readonly int[] _bucketCount;
        readonly int[] _bucketStart;
        readonly int[] _bucketCursor;

        int _pending;

        public int Batches { get; private set; }

        /// <summary>
        /// Same batching for enemies and for the shots they fire — different mesh, different
        /// palette, different size, and shots do not breathe.
        /// </summary>
        public EnemyRenderer(Mesh mesh, Color[] palette, int capacity, float bodyScale, bool animate,
            VatClipSet vat = null)
        {
            _vat = vat != null && vat.Mesh != null && vat.Positions != null ? vat : null;

            // Mesh panggangan menggantikan kapsulnya. Bentuknya pose netral — vertexnya digeser
            // shader — jadi yang dikirim ke sini tetap satu mesh statis seperti sebelumnya, dan
            // seluruh jalur batching di bawah tidak tahu bedanya.
            _mesh = _vat != null ? _vat.Mesh : mesh;
            _bodyScale = bodyScale;

            // Bob squash-stretch dimatikan begitu ada animasi sungguhan. Membiarkannya berarti
            // dua animasi berjalan di atas satu sama lain: kerangka yang melangkah sekaligus
            // memuai-mengempis seperti balon.
            _animate = animate && _vat == null;

            var shader = _vat != null ? Shader.Find("Grimoire/EnemyVat") : null;
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");

            _materials = new Material[palette.Length];
            _params = new RenderParams[palette.Length];

            for (int i = 0; i < palette.Length; i++)
            {
                _materials[i] = new Material(shader) { enableInstancing = true };
                _materials[i].SetColor("_BaseColor", palette[i]);
                _materials[i].SetColor("_Color", palette[i]);

                if (_vat != null)
                {
                    _materials[i].SetTexture("_VatTex", _vat.Positions);
                    _materials[i].SetFloat("_VatRows", Mathf.Max(1, _vat.TotalRows));

                    // Tekstur warna diambil dari material asli asetnya. Tanpa ini kerangkanya
                    // tergambar polos, dan seluruh gunanya memakai model 3D hilang.
                    var skin = SkinOf(_vat.SourceMaterial);
                    if (skin != null) _materials[i].SetTexture("_BaseMap", skin);
                }

                // Matte, disetel eksplisit. URP/Lit lahir dengan smoothness 0,5 — material yang
                // dibuat lewat kode tanpa menyentuhnya akan mengkilap seperti plastik basah.
                _materials[i].SetFloat("_Smoothness", 0f);
                _materials[i].SetFloat("_Glossiness", 0f);
                _materials[i].SetFloat("_Metallic", 0f);
                _materials[i].SetFloat("_SpecularHighlights", 0f);
                _materials[i].EnableKeyword("_SPECULARHIGHLIGHTS_OFF");

                _params[i] = new RenderParams(_materials[i])
                {
                    // Enemies are the one thing on screen there are hundreds of; shadows for them
                    // were already off before this, and stay off.
                    shadowCastingMode = ShadowCastingMode.Off,
                    receiveShadows = false,

                    // Set explicitly: an unset bounds can cull the whole batch away, and the swarm
                    // ranges far wider than the arena because it spawns outside it.
                    //
                    // Sengaja BESAR SEKALI, bukan sekadar cukup. Lapangannya tak berujung, jadi
                    // kotak sebesar arena akan membuat seluruh batch hilang dari layar begitu
                    // pemain berjalan cukup jauh dari titik nol — dan hilangnya total, tanpa error.
                    worldBounds = new Bounds(Vector3.zero, Vector3.one * 100000f)
                };
            }

            _stagePos = new Vector3[capacity];
            _stageYaw = new float[capacity];
            _stagePhase = new float[capacity];
            _stageScale = new Vector3[capacity];
            _stageTint = new int[capacity];

            _instances = new Matrix4x4[capacity];
            _bucketCount = new int[palette.Length];
            _bucketStart = new int[palette.Length];
            _bucketCursor = new int[palette.Length];

            if (_vat == null) return;

            _stageSpeed = new float[capacity];
            _vatArgs = new Vector4[capacity];
            _vatChunk = new Vector4[MaxPerDraw];
            _vatBlock = new MaterialPropertyBlock();

            // Ketiga peran dicari SEKALI di sini, bukan per musuh per frame. Pencariannya
            // memang cuma menyusuri tiga elemen, tapi lima ratus musuh enam puluh kali sedetik
            // membuat "cuma tiga elemen" jadi sembilan puluh ribu perbandingan string-kelas
            // per detik untuk jawaban yang tidak pernah berubah.
            _vat.TryGet(VatRole.Idle, out _clipIdle);
            _vat.TryGet(VatRole.Walk, out _clipWalk);
            _vat.TryGet(VatRole.Run, out _clipRun);
        }

        /// <summary>Tekstur warna sebuah material, dari nama properti mana pun yang dipakainya.</summary>
        static Texture SkinOf(Material source)
        {
            if (source == null) return null;
            if (source.HasProperty("_BaseMap")) return source.GetTexture("_BaseMap");
            if (source.HasProperty("_MainTex")) return source.GetTexture("_MainTex");
            return null;
        }

        /// <summary>Steals the built-in mesh off a throwaway primitive, then drops the primitive.</summary>
        public static Mesh BorrowPrimitiveMesh(PrimitiveType type)
        {
            var temp = GameObject.CreatePrimitive(type);
            var mesh = temp.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(temp);
            return mesh;
        }

        public void Begin() => _pending = 0;

        public void Add(Vector3 position, float yaw, float phase, int tint, float scale,
            float speed01 = 0f)
        {
            Add(position, yaw, phase, tint, Vector3.one * (scale <= 0f ? 1f : scale), speed01);
        }

        /// <summary>
        /// Varian skala TIDAK seragam. Perlu ada sendiri karena bentuk yang bukan makhluk hidup
        /// tidak pernah proporsional: batang pohon itu tinggi-kurus dan tajuk itu lebar-pipih, dan
        /// keduanya mustahil dari satu angka skala.
        /// </summary>
        public void Add(Vector3 position, float yaw, float phase, int tint, Vector3 scale,
            float speed01 = 0f)
        {
            if (_pending >= _stagePos.Length) return;
            if (tint < 0 || tint >= _materials.Length) tint = 0;

            _stagePos[_pending] = position;
            _stageYaw[_pending] = yaw;
            _stagePhase[_pending] = phase;
            _stageScale[_pending] = scale;
            _stageTint[_pending] = tint;
            if (_stageSpeed != null) _stageSpeed[_pending] = speed01;
            _pending++;
        }

        /// <summary>
        /// Counting sort into one flat buffer, then one draw per tint over its own slice. Sorting
        /// beats a buffer per bucket: this allocates the swarm once, not once per colour it might
        /// have turned.
        /// </summary>
        public void Draw(float time)
        {
            Batches = 0;
            if (_pending == 0) return;

            System.Array.Clear(_bucketCount, 0, _bucketCount.Length);
            for (int i = 0; i < _pending; i++) _bucketCount[_stageTint[i]]++;

            int running = 0;
            for (int b = 0; b < _bucketCount.Length; b++)
            {
                _bucketStart[b] = running;
                _bucketCursor[b] = running;
                running += _bucketCount[b];
            }

            for (int i = 0; i < _pending; i++)
            {
                int slot = _bucketCursor[_stageTint[i]]++;
                _instances[slot] = Compose(_stagePos[i], _stageYaw[i], _stagePhase[i],
                    _stageScale[i], _animate ? time : 0f, _bodyScale, _animate);

                // Ditulis di slot yang SAMA dengan matriksnya. Pengurutan ember mengacak urutan
                // musuh, dan data animasi yang tetap memakai urutan masuk akan memasangkan
                // kerangka dengan pose milik kerangka lain.
                if (_vatArgs != null) _vatArgs[slot] = ClipArgs(_stageSpeed[i], _stagePhase[i], time);
            }

            for (int b = 0; b < _bucketCount.Length; b++)
            {
                int remaining = _bucketCount[b];
                int start = _bucketStart[b];

                while (remaining > 0)
                {
                    int chunk = Mathf.Min(remaining, MaxPerDraw);

                    if (_vatArgs != null)
                    {
                        // Disalin ke penyangga berukuran tetap, lalu dikirim UTUH. Panjang array
                        // di MaterialPropertyBlock terkunci pada pengiriman pertama; mengirim
                        // potongan yang lebih pendek berikutnya tidak berpengaruh, dan sisanya
                        // tergambar memakai pose dari draw sebelumnya.
                        System.Array.Copy(_vatArgs, start, _vatChunk, 0, chunk);
                        _vatBlock.SetVectorArray(VatClipId, _vatChunk);
                        _params[b].matProps = _vatBlock;
                    }

                    Graphics.RenderMeshInstanced(_params[b], _mesh, 0, _instances, chunk, start);
                    Batches++;

                    start += chunk;
                    remaining -= chunk;
                }
            }
        }

        /// <summary>
        /// Peran mana yang diputar musuh ini, dan sudah sejauh mana.
        ///
        /// Dipilih dari KECEPATAN, bukan dari keadaan yang dikirim gameplay. Musuh di sini tidak
        /// punya mesin keadaan — yang ada cuma posisi yang berpindah — dan kecepatan adalah satu
        /// hal yang selalu benar tanpa perlu ada yang mengingat untuk memperbaruinya.
        /// </summary>
        Vector4 ClipArgs(float speed01, float phase, float time)
        {
            VatClip clip = speed01 < 0.05f ? _clipIdle
                         : speed01 < 0.6f ? _clipWalk
                         : _clipRun;

            if (clip.Rows <= 0) clip = _clipIdle;
            if (clip.Rows <= 0) return Vector4.zero;

            // Fase dipakai sebagai geseran awal, bukan cuma untuk bob: tanpa itu lima ratus
            // kerangka melangkah dengan kaki yang sama persis di frame yang sama, dan gerombolan
            // yang berbaris serempak terbaca sebagai satu benda, bukan sebagai kerumunan.
            float seconds = clip.Seconds > 0.01f ? clip.Seconds : 1f;
            float progress = time / seconds + phase * 0.159f;

            return new Vector4(clip.FirstRow, clip.Rows, progress, 0f);
        }

        /// <summary>
        /// One instance's transform. The bob is not decoration — it is the animation seam. Real
        /// baked animation replaces the body of this method and the shader; nothing that calls it
        /// has to change.
        /// </summary>
        static Matrix4x4 Compose(Vector3 position, float yaw, float phase, Vector3 scale, float time,
            float bodyScale, bool animate)
        {
            Vector3 body = scale * bodyScale;

            if (!animate)
            {
                return Matrix4x4.TRS(position, Quaternion.Euler(0f, yaw, 0f), body);
            }

            float wobble = Mathf.Sin(time * 7f + phase);

            // Squash and stretch around a fixed volume, so the silhouette breathes without drifting.
            float stretch = 1f + wobble * 0.06f;
            float squash = 1f / stretch;

            // Bigger bodies sit higher, or a scaled-up enemy sinks halfway into the floor.
            position.y += wobble * 0.06f + (scale.y - 1f) * bodyScale;

            return Matrix4x4.TRS(position, Quaternion.Euler(0f, yaw, 0f),
                new Vector3(body.x * squash, body.y * stretch, body.z * squash));
        }
    }
}
