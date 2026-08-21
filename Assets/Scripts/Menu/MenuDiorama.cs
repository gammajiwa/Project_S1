using UnityEngine;

namespace Proto
{
    /// <summary>
    /// Drifts the menu camera along a slow ellipse. A still frame behind the menu reads as a
    /// screenshot rather than a game, and the drift costs nothing.
    /// </summary>
    public class MenuDiorama : MonoBehaviour
    {
        [SerializeField] float _radius = 1.6f;
        [SerializeField] float _depth = 0.7f;
        [SerializeField] float _bob = 0.35f;

        [Tooltip("Putaran penuh per detik. 0.03 = satu putaran ~33 detik.")]
        [SerializeField] float _speed = 0.03f;

        [Header("Maju-mundur (dolly)")]
        [Tooltip("Seberapa jauh kamera MENDEKAT dan menjauh sepanjang arah pandangnya, dalam unit.\n\n" +
                 "Beda dari _depth: yang itu menggeser kamera di sumbu Z dunia, jadi di kamera yang " +
                 "menunduk hasilnya ikut naik-turun di layar. Yang ini bergerak persis ke arah yang " +
                 "dilihat, jadi yang berubah cuma JARAK — bingkainya bernapas, isinya tidak melayang.")]
        [SerializeField] float _dolly;

        [Tooltip("Siklus penuh per detik. 0.012 = satu tarikan napas ~83 detik.")]
        [SerializeField] float _dollySpeed = 0.012f;

        Vector3 _origin;

        void Awake() => _origin = transform.localPosition;

        void LateUpdate()
        {
            // Unscaled so a stray timeScale from a previous scene can never freeze the backdrop.
            float now = Time.unscaledTime;
            float t = now * _speed * Mathf.PI * 2f;

            var at = _origin + new Vector3(
                Mathf.Sin(t) * _radius,
                Mathf.Sin(t * 0.6f) * _bob,
                Mathf.Cos(t) * _depth);

            // Dua irama yang tidak habis membagi satu sama lain — kalau periodenya sebanding,
            // keduanya bertemu di titik yang sama secara berkala dan matanya menemukan
            // pengulangan itu jauh lebih cepat daripada periode masing-masing.
            if (_dolly > 0.0001f)
            {
                float d = now * _dollySpeed * Mathf.PI * 2f;
                at += transform.localRotation * Vector3.forward * (Mathf.Sin(d) * _dolly);
            }

            transform.localPosition = at;
        }
    }
}
