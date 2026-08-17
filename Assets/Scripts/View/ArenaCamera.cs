using UnityEngine;

namespace Proto
{
    /// <summary>
    /// Kamera yang DIAM sampai pemain benar-benar mendekati tepi layar, lalu ikut secukupnya saja.
    ///
    /// Kamera yang selalu menempel pada pemain membuat dunia yang bergerak dan pemain yang diam:
    /// di game yang seluruh bacaannya adalah "gerombolan datang dari arah mana", itu justru
    /// menghapus satu-satunya isyarat yang penting. Jadi ada zona mati di tengah — selama pemain
    /// masih di dalamnya, kamera tidak bergerak sama sekali.
    ///
    /// Komponen ini duduk di RIG, bukan di Camera-nya, karena <see cref="CameraShake"/> menulis
    /// localPosition kamera dan mengingat titik asalnya saat Awake. Kalau keduanya menulis
    /// transform yang sama, guncangan akan menarik kamera balik ke titik asal tiap frame dan
    /// membatalkan seluruh pengikutan ini tanpa error apa pun.
    ///
    /// Rig dijepit terhadap kotak arena, tapi jepitannya diukur ke TEPI ZONA MATI, bukan ke tepi
    /// layar: pemain yang menempel dinding arena pun tetap tertahan di tepi kotak, tidak pernah
    /// hanyut ke pojok layar (atau ke belakang buku). Hutan di luar arena memang dibangkitkan dan
    /// didandani, jadi tanah luar yang ikut tertangkap tetap terbaca sebagai tepi hutan, bukan
    /// kekosongan yang tiba-tiba berhenti.
    /// </summary>
    public class ArenaCamera : MonoBehaviour
    {
        Transform _target;

        /// <summary>
        /// Tepi kotak zona mati sebagai offset dunia dari titik fokus — min negatif, max positif,
        /// dan SENGAJA tidak simetris: HUD-nya berat sebelah (buku menutup kiri-bawah), jadi sisi
        /// kiri menahan pemain lebih jauh dari tepi layar daripada sisi kanan.
        /// </summary>
        float _deadMinX, _deadMaxX;
        float _deadMinZ, _deadMaxZ;

        /// <summary>Jepitan rig per arah — asimetris mengikuti zona matinya.</summary>
        float _limitMinX, _limitMaxX;
        float _limitMinZ, _limitMaxZ;

        Vector3 _velocity;

        [Tooltip("Detik untuk menyusul. Terlalu cepat = kamera menempel; terlalu lambat = pemain " +
                 "sempat keluar layar sebelum kamera sadar.")]
        [SerializeField] float _smooth = 0.35f;

        /// <summary>
        /// Zona matinya dihitung dari yang benar-benar TERLIHAT, bukan dari angka yang diketik:
        /// ukuran ortografis, rasio layar dan sudut kemiringan semuanya ikut menentukan seberapa
        /// jauh tanah tertangkap.
        /// </summary>
        public void Init(Transform target, Camera cam, GameBalance balance)
        {
            _target = target;

            float halfHeight = cam.orthographicSize;
            float halfWidth = halfHeight * cam.aspect;

            // Kamera menunduk, jadi jangkauan vertikal di layar meregang di atas tanah.
            float pitch = Mathf.Max(15f, cam.transform.eulerAngles.x);
            float halfDepth = halfHeight / Mathf.Sin(pitch * Mathf.Deg2Rad);

            // Margin per sisi dalam porsi layar → offset dunia dari pusat pandang. Margin 0,22
            // di kiri berarti batas kotaknya 22% dari tepi kiri layar = 56% setengah-lebar di
            // kiri pusat. Layar kiri = dunia −X, layar bawah = dunia −Z (kamera tak ber-yaw).
            _deadMinX = -halfWidth * (1f - 2f * Mathf.Clamp(balance.CameraDeadLeft, 0.05f, 0.45f));
            _deadMaxX = halfWidth * (1f - 2f * Mathf.Clamp(balance.CameraDeadRight, 0.05f, 0.45f));
            _deadMinZ = -halfDepth * (1f - 2f * Mathf.Clamp(balance.CameraDeadBottom, 0.05f, 0.45f));
            _deadMaxZ = halfDepth * (1f - 2f * Mathf.Clamp(balance.CameraDeadTop, 0.05f, 0.45f));

            // Jepitan dilonggarkan dari "tepi layar = tepi arena" menjadi "tepi ZONA = tepi
            // arena". Dengan jepitan lama, di dinding arena kamera berhenti total dan pemain
            // bebas berjalan sampai pojok layar — lalu lenyap di belakang buku. Sekarang pemain
            // yang menempel dinding pun masih tertahan di tepi kotak zona. Tanah di luar arena
            // yang ikut tertangkap bukan masalah: hutan di luar memang dibangkitkan dan
            // didandani, itulah yang membuat dindingnya terbaca sebagai tepi hutan.
            _limitMaxX = Mathf.Max(0f, balance.ArenaHalfX - _deadMaxX);
            _limitMinX = Mathf.Min(0f, -balance.ArenaHalfX - _deadMinX);
            _limitMaxZ = Mathf.Max(0f, balance.ArenaHalfZ - _deadMaxZ);
            _limitMinZ = Mathf.Min(0f, -balance.ArenaHalfZ - _deadMinZ);
        }

        /// <summary>
        /// Kotak tempat kamera masih bisa MENENGAHKAN sasarannya. Titik lahir/teleport pemain
        /// harus di dalam kotak ini — di luarnya jepitan arena menahan rig, dan pemain muncul
        /// menepi di layar sejak detik pertama.
        /// </summary>
        // Diambil sisi yang paling sempit: pemakainya menjepit simetris (titik lahir & teleport),
        // dan kotak konservatif menjamin kamera selalu bisa menahan pemain di dalam zona.
        public float LimitX => Mathf.Min(-_limitMinX, _limitMaxX);

        public float LimitZ => Mathf.Min(-_limitMinZ, _limitMaxZ);

        /// <summary>
        /// Titik yang dituju kamera, disimpan TERPISAH dari posisi kamera itu sendiri.
        ///
        /// Ini bukan kerapian, ini perbaikan bug. Dulu sasarannya dihitung dari
        /// <c>transform.position</c>, sementara transform itu sendiri sedang diperhalus MENUJU
        /// sasaran tersebut. Jadi tiap frame sasarannya dihitung ulang dari titik yang selalu
        /// tertinggal — umpan balik yang tidak pernah mengendap, dan kameranya merayap pelan tanpa
        /// henti bahkan saat pemain berdiri sama sekali diam.
        /// </summary>
        Vector3 _focus;

        /// <summary>Bebas dari jepitan arena — dinyalakan selama pemain di pulau rehat.</summary>
        public bool Roam;

        /// <summary>
        /// Lompat seketika (pindah ke/dari pulau). Fokus dan kecepatan ikut di-reset — tanpa itu
        /// kamera ber-SmoothDamp ratusan unit melintasi hutan, dan pemain menonton perjalanannya.
        /// </summary>
        public void Teleport(Vector3 at)
        {
            _focus = new Vector3(at.x, transform.position.y, at.z);
            transform.position = _focus;
            _velocity = Vector3.zero;
        }

        void Awake() => _focus = transform.position;

        void LateUpdate()
        {
            if (_target == null) return;

            Vector3 p = _target.position;

            // Hanya selisih DI LUAR zona mati yang dikejar. Menggeser sebanyak selisih penuh akan
            // memusatkan pemain lagi, dan zona matinya jadi tidak ada artinya.
            _focus.x += Overshoot(p.x - _focus.x, _deadMinX, _deadMaxX);
            _focus.z += Overshoot(p.z - _focus.z, _deadMinZ, _deadMaxZ);

            // Dijepit SETELAH digeser, dan pada _focus bukan pada posisi kamera — menjepit posisi
            // sementara sasarannya bebas berarti kamera terus mendorong ke dinding tanpa henti.
            if (!Roam)
            {
                _focus.x = Mathf.Clamp(_focus.x, _limitMinX, _limitMaxX);
                _focus.z = Mathf.Clamp(_focus.z, _limitMinZ, _limitMaxZ);
            }

            _focus.y = transform.position.y;

            transform.position = Vector3.SmoothDamp(transform.position, _focus, ref _velocity, _smooth);
        }

        static float Overshoot(float delta, float min, float max)
        {
            if (delta > max) return delta - max;
            if (delta < min) return delta - min;
            return 0f;
        }
    }
}
