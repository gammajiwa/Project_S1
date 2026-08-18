using UnityEngine;

namespace Proto
{
    /// <summary>
    /// Menyelipkan tolehan ke animasi diam sebuah NPC — sesekali, bukan terus-menerus.
    ///
    /// Ada karena idle yang menoleh SEPANJANG waktu terbaca gelisah, bukan hidup: yang
    /// membuatnya terasa bernyawa justru jeda panjang menatap kosong, lalu satu tolehan
    /// pendek. Jadi klipnya dipisah — Idle berputar terus, dan komponen ini yang memutuskan
    /// kapan menoleh, ke kiri atau ke kanan.
    ///
    /// Jeda diundi ulang setiap kali, jadi dua penjual di layar yang sama tidak pernah
    /// menoleh serempak seperti mesin.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    [AddComponentMenu("Grimoire/Idle Looker")]
    public class IdleLooker : MonoBehaviour
    {
        [Tooltip("Jeda antar tolehan, detik — diundi di rentang x..y.")]
        public Vector2 Interval = new Vector2(6f, 14f);

        [Tooltip("Peluang tolehan mengarah ke KIRI. Sisanya ke kanan.")]
        [Range(0f, 1f)] public float LeftChance = 0.5f;

        [Tooltip("Nama pemicu di Animator untuk masing-masing arah.")]
        public string LeftTrigger = "LookLeft";
        public string RightTrigger = "LookRight";

        Animator _anim;
        float _next;

        void Awake()
        {
            _anim = GetComponent<Animator>();
            Schedule();
        }

        void Schedule()
        {
            // Tolehan pertama juga diundi: tanpa ini semua penjual menoleh di detik yang sama
            // setelah scene dimuat.
            _next = Time.time + Random.Range(Interval.x, Interval.y);
        }

        void Update()
        {
            if (_anim == null || Time.time < _next) return;

            _anim.SetTrigger(Random.value < LeftChance ? LeftTrigger : RightTrigger);
            Schedule();
        }
    }
}
