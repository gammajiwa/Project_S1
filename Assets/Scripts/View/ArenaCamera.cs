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
    /// </summary>
    public class ArenaCamera : MonoBehaviour
    {
        Transform _target;

        /// <summary>Setengah lebar zona mati dalam unit dunia.</summary>
        float _deadX;
        float _deadZ;

        /// <summary>Sejauh mana rig boleh menggeser sebelum tanah kosong di luar arena terlihat.</summary>
        float _limitX;
        float _limitZ;

        Vector3 _velocity;

        [Tooltip("Detik untuk menyusul. Terlalu cepat = kamera menempel; terlalu lambat = pemain " +
                 "sempat keluar layar sebelum kamera sadar.")]
        [SerializeField] float _smooth = 0.35f;

        /// <summary>
        /// Batasnya dihitung dari yang benar-benar TERLIHAT, bukan dari angka yang diketik: ukuran
        /// ortografis, rasio layar dan sudut kemiringan semuanya ikut menentukan seberapa jauh
        /// tanah tertangkap, dan menebaknya berarti tepi arena bisa bocor di rasio layar tertentu.
        /// </summary>
        public void Init(Transform target, Camera cam, GameBalance balance)
        {
            _target = target;

            float halfHeight = cam.orthographicSize;
            float halfWidth = halfHeight * cam.aspect;

            // Kamera menunduk, jadi jangkauan vertikal di layar meregang di atas tanah.
            float pitch = Mathf.Max(15f, cam.transform.eulerAngles.x);
            float halfDepth = halfHeight / Mathf.Sin(pitch * Mathf.Deg2Rad);

            _limitX = Mathf.Max(0f, balance.ArenaHalfX - halfWidth);
            _limitZ = Mathf.Max(0f, balance.ArenaHalfZ - halfDepth);

            // Zona mati sekitar separuh layar. Cukup lebar supaya menghindar sehari-hari tidak
            // menggoyang kamera, cukup sempit supaya pemain tidak pernah kejepit di tepi.
            _deadX = halfWidth * 0.5f;
            _deadZ = halfDepth * 0.5f;
        }

        void LateUpdate()
        {
            if (_target == null) return;

            Vector3 focus = transform.position;
            Vector3 p = _target.position;

            // Hanya selisih DI LUAR zona mati yang dikejar. Menggeser sebanyak selisih penuh akan
            // memusatkan pemain lagi, dan zona matinya jadi tidak ada artinya.
            focus.x += Overshoot(p.x - focus.x, _deadX);
            focus.z += Overshoot(p.z - focus.z, _deadZ);

            focus.x = Mathf.Clamp(focus.x, -_limitX, _limitX);
            focus.z = Mathf.Clamp(focus.z, -_limitZ, _limitZ);
            focus.y = transform.position.y;

            transform.position = Vector3.SmoothDamp(transform.position, focus, ref _velocity, _smooth);
        }

        static float Overshoot(float delta, float dead)
        {
            if (delta > dead) return delta - dead;
            if (delta < -dead) return delta + dead;
            return 0f;
        }
    }
}
