using UnityEngine;

namespace Proto
{
    /// <summary>
    /// Memutar satu transform pelan sekali, selamanya. Dipakai lingkaran sihir raksasa di latar
    /// menu.
    ///
    /// Kenapa komponen sendiri dan bukan Animator: yang diminta putaran dengan siklus di atas
    /// sepuluh detik — di luar jangkauan yang enak diatur lewat kurva, dan sebuah klip animasi
    /// untuk satu sumbu berarti satu aset lagi yang harus dibuka orang untuk mengubah satu angka.
    ///
    /// <c>unscaledDeltaTime</c>, bukan <c>deltaTime</c>: menu bisa dimasuki dari dalam run yang
    /// menyisakan <c>timeScale</c> nol (fase menyusun grid membekukan waktu), dan latar yang
    /// membeku terbaca sebagai game yang hang.
    /// </summary>
    [AddComponentMenu("Grimoire/Menu Spin")]
    public class MenuSpin : MonoBehaviour
    {
        [Tooltip("Derajat per detik. 1.2 = satu putaran penuh 5 menit.")]
        [SerializeField] float _degreesPerSecond = 1.2f;

        [Tooltip("Sumbu putar di ruang LOKAL. (0,1,0) untuk lingkaran yang tergeletak di tanah, " +
                 "(0,0,1) untuk yang berdiri menghadap kamera.")]
        [SerializeField] Vector3 _axis = Vector3.up;

        Quaternion _authored;
        float _turned;

        void OnEnable() => _authored = transform.localRotation;

        /// <summary>
        /// Rotasi yang DISETEL TANGAN dikembalikan begitu komponen ini mati.
        ///
        /// Versi pertama memanggil <c>transform.Rotate</c> — menumpuk putaran ke transform itu
        /// sendiri. Akibatnya rotasi yang disetel orang ketimpa sudut sembarang tiap kali play
        /// mode dimasuki, dan begitu scene atau prefabnya tersimpan dalam keadaan itu, angkanya
        /// hilang permanen. Sudah terjadi, dan yang hilang kerjaan pemilik project.
        ///
        /// Sekarang sudut aslinya disimpan, putarannya cuma OFFSET di atasnya, dan offset itu
        /// dibuang saat mati. Apa pun yang tersimpan setelah ini adalah angka yang diketik orang.
        /// </summary>
        void OnDisable()
        {
            transform.localRotation = _authored;
            _turned = 0f;
        }

        void Update()
        {
            if (_axis.sqrMagnitude < 0.0001f) return;

            _turned += _degreesPerSecond * Time.unscaledDeltaTime;
            transform.localRotation = _authored * Quaternion.AngleAxis(_turned, _axis.normalized);
        }
    }
}
