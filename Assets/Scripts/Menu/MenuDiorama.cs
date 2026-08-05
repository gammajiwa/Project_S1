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

        Vector3 _origin;

        void Awake() => _origin = transform.localPosition;

        void LateUpdate()
        {
            // Unscaled so a stray timeScale from a previous scene can never freeze the backdrop.
            float t = Time.unscaledTime * _speed * Mathf.PI * 2f;

            transform.localPosition = _origin + new Vector3(
                Mathf.Sin(t) * _radius,
                Mathf.Sin(t * 0.6f) * _bob,
                Mathf.Cos(t) * _depth);
        }
    }
}
