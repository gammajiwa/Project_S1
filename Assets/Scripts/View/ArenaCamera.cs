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
    /// Rig dikunci di dalam kotak arena supaya tepi lapangan tidak pernah bocor ke layar. Hutan
    /// di luar arena tetap dibangkitkan dan terlihat — itu yang membuat dindingnya terbaca sebagai
    /// "tanah lapang berbatas pohon", bukan sebagai kekosongan yang tiba-tiba berhenti.
    /// </summary>
    public class ArenaCamera : MonoBehaviour
    {
        Transform _target;

        /// <summary>Setengah lebar zona mati dalam unit dunia.</summary>
        float _deadX;
        float _deadZ;

        /// <summary>Sejauh mana rig boleh menggeser sebelum tanah di luar arena terlihat.</summary>
        float _limitX;
        float _limitZ;

        Vector3 _velocity;

        [Tooltip("Detik untuk menyusul. Terlalu cepat = kamera menempel; terlalu lambat = pemain " +
                 "sempat keluar layar sebelum kamera sadar.")]
        [SerializeField] float _smooth = 0.35f;

        [Tooltip("Detik untuk menyusul saat pemain BERHENTI. Lebih pendek dari _smooth: sisa " +
                 "perjalanan kamera setelah pemain diam terbaca sebagai KARAKTERNYA yang meluncur " +
                 "mundur di layar, bukan sebagai kamera yang sedang menyusul.")]
        [SerializeField] float _settle = 0.12f;

        /// <summary>Posisi sasaran frame sebelumnya — dipakai menebak apakah ia sedang bergerak.</summary>
        Vector3 _lastTargetPos;

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

            float fraction = Mathf.Clamp(balance.CameraDeadZone, 0.05f, 0.8f);
            _deadX = halfWidth * fraction;
            _deadZ = halfDepth * fraction;

            _limitX = Mathf.Max(0f, balance.ArenaHalfX - halfWidth);
            _limitZ = Mathf.Max(0f, balance.ArenaHalfZ - halfDepth);
        }

        /// <summary>
        /// Kotak tempat kamera masih bisa MENENGAHKAN sasarannya. Titik lahir/teleport pemain
        /// harus di dalam kotak ini — di luarnya jepitan arena menahan rig, dan pemain muncul
        /// menepi di layar sejak detik pertama.
        /// </summary>
        public float LimitX => _limitX;

        public float LimitZ => _limitZ;

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
            _focus.x += Overshoot(p.x - _focus.x, _deadX);
            _focus.z += Overshoot(p.z - _focus.z, _deadZ);

            // Dijepit SETELAH digeser, dan pada _focus bukan pada posisi kamera — menjepit posisi
            // sementara sasarannya bebas berarti kamera terus mendorong ke dinding tanpa henti.
            if (!Roam)
            {
                _focus.x = Mathf.Clamp(_focus.x, -_limitX, _limitX);
                _focus.z = Mathf.Clamp(_focus.z, -_limitZ, _limitZ);
            }

            _focus.y = transform.position.y;

            // Pemain yang BERHENTI mendapat penyusulan yang jauh lebih pendek.
            //
            // Ini memperbaiki keluhan "badannya geser balik ke titik semula": selagi berlari,
            // pemain mengambang menjauh dari tengah layar (memang begitu cara zona mati bekerja),
            // dan begitu ia berhenti kamera masih menyisakan perjalanan setengah detik lebih.
            // Yang terbaca mata BUKAN kamera yang menyusul melainkan KARAKTERNYA yang meluncur
            // mundur — dan sejak karakternya punya kaki, ia terbaca seperti animasi yang di-rewind.
            // Terukur sebelum perbaikan: layarX 1219 -> 1171 selama 0,8 detik sesudah pemain diam.
            bool moving = (p - _lastTargetPos).sqrMagnitude > 0.0000025f;
            _lastTargetPos = p;

            transform.position = Vector3.SmoothDamp(transform.position, _focus, ref _velocity,
                moving ? _smooth : _settle);
        }

        static float Overshoot(float delta, float dead)
        {
            if (delta > dead) return delta - dead;
            if (delta < -dead) return delta + dead;
            return 0f;
        }
    }
}
