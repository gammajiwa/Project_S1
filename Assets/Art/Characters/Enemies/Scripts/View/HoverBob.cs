using UnityEngine;

namespace Proto
{
    /// <summary>
    /// Naik-turun sinus + goyangan halus untuk benda yang MELAYANG — buku grimoire,
    /// relik, apa pun yang harus terlihat ditahan sihir, bukan ditaruh di rak.
    ///
    /// Berjalan di <c>LateUpdate</c> supaya menang atas Animator kalau suatu hari ada
    /// klip yang ikut menulis transform ini. Basisnya posisi lokal saat bangun, jadi
    /// geser tangan di editor otomatis jadi pusat ayunan baru.
    /// </summary>
    [AddComponentMenu("Grimoire/Hover Bob")]
    public class HoverBob : MonoBehaviour
    {
        [Tooltip("Setengah tinggi ayunan, unit lokal.")]
        public float Amplitude = 0.06f;

        [Tooltip("Detik untuk satu ayunan penuh.")]
        [Min(0.1f)] public float Period = 2.6f;

        [Tooltip("Kemiringan maksimal saat bergoyang, derajat. Nol = cuma naik-turun.")]
        public float SwayDegrees = 4f;

        Vector3 _basePos;
        Quaternion _baseRot;
        float _t;

        void Awake()
        {
            _basePos = transform.localPosition;
            _baseRot = transform.localRotation;
            _t = Random.value * 10f;  // dua pemegang buku tidak boleh mengayun serempak
        }

        void LateUpdate()
        {
            _t += Time.deltaTime;
            float w = (Mathf.PI * 2f) / Period;

            transform.localPosition = _basePos + Vector3.up * (Mathf.Sin(_t * w) * Amplitude);
            transform.localRotation = _baseRot * Quaternion.Euler(
                Mathf.Sin(_t * w * 0.7f) * SwayDegrees,
                0f,
                Mathf.Cos(_t * w * 0.5f) * SwayDegrees * 0.5f);
        }
    }
}
