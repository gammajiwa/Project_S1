using UnityEngine;

namespace Proto
{
    /// <summary>
    /// Grimoire yang melayang di atas telapak, bukan digenggam kaku.
    ///
    /// Dikerjakan sebagai SCRIPT, bukan animation clip, dan itu bukan soal selera. Tulang
    /// karakter ini mewarisi skala 190x — akibat CHAR_Root yang masuk dari FBX pada skala 100.
    /// Sebuah klip yang menganimasikan localPosition harus menulis angka sekecil 0,00004 untuk
    /// menghasilkan gerak 8 mm, dan Animator Unity MENIMPA transform alih-alih menambahkannya —
    /// jadi klip apa pun akan sekaligus menghapus penempatan buku di tangan. Script menambah
    /// di atas posisi rest, jadi penempatannya aman.
    ///
    /// Keuntungan lain: hover-nya jalan terus, tak peduli klip mana yang sedang diputar
    /// karakternya, dan tidak ikut terpotong saat transisi Animator.
    /// </summary>
    public class GrimoireHover : MonoBehaviour
    {
        [Header("Naik-turun")]
        [Tooltip("Simpangan naik-turun dalam METER dunia — bukan satuan lokal. " +
                 "Script mengonversinya sendiri, jadi angka di sini tetap terbaca meski " +
                 "skala tulang induknya aneh.")]
        public float BobMeters = 0.045f;

        [Min(0.1f)] public float BobPeriod = 3.1f;

        [Header("Geser samping")]
        public float SwayMeters = 0.022f;
        [Min(0.1f)] public float SwayPeriod = 4.7f;

        [Header("Miring & berputar")]
        public float TiltDegrees = 4.5f;
        [Min(0.1f)] public float TiltPeriod = 5.3f;
        public float SpinDegrees = 3.5f;
        [Min(0.1f)] public float SpinPeriod = 6.1f;

        Vector3 _restPos;
        Quaternion _restRot;
        float _phase;

        void Awake()
        {
            _restPos = transform.localPosition;
            _restRot = transform.localRotation;

            // Fase awal diacak. Kalau ada dua grimoire di layar dan keduanya berdenyut
            // serempak, mereka berhenti terbaca sebagai dua benda dan mulai terbaca
            // sebagai satu efek yang diduplikasi.
            _phase = Random.value * 10f;
        }

        void LateUpdate()
        {
            // LateUpdate, bukan Update: Animator menulis transform tulang di dalam Update,
            // dan hover ini harus menumpang DI ATAS hasil itu. Di Update ia akan tertimpa.
            float t = Time.time + _phase;
            const float TAU = 2f * Mathf.PI;

            Vector3 dunia = new Vector3(
                SwayMeters * Mathf.Sin(t * TAU / SwayPeriod),
                BobMeters  * Mathf.Sin(t * TAU / BobPeriod),
                0f);

            // Amplitudo ditulis dalam meter dunia lalu dikonversi ke ruang induk.
            // InverseTransformVector menangani rotasi DAN skala induk sekaligus — itu yang
            // membuat angka di inspector tetap berarti meski induknya berskala 190x.
            Vector3 lokal = transform.parent != null
                ? transform.parent.InverseTransformVector(dunia)
                : dunia;

            transform.localPosition = _restPos + lokal;

            transform.localRotation = _restRot * Quaternion.Euler(
                TiltDegrees * Mathf.Sin(t * TAU / TiltPeriod),
                SpinDegrees * Mathf.Sin(t * TAU / SpinPeriod),
                TiltDegrees * 0.6f * Mathf.Cos(t * TAU / TiltPeriod));
        }
    }
}
