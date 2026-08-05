using UnityEngine;

namespace Proto
{
    /// <summary>
    /// Trauma-based screen shake. Callers add trauma rather than requesting a shake, so twenty
    /// reactions in one second build one big kick instead of twenty fighting each other.
    /// </summary>
    public class CameraShake : MonoBehaviour
    {
        [Tooltip("Geser maksimum dalam unit dunia saat trauma penuh.")]
        [SerializeField] float _maxOffset = 0.85f;

        [Tooltip("Trauma yang hilang per detik.")]
        [SerializeField] float _decay = 2.6f;

        [SerializeField] float _frequency = 22f;

        Vector3 _origin;
        float _trauma;
        float _seed;

        void Awake()
        {
            _origin = transform.localPosition;
            _seed = Random.value * 100f;
        }

        /// <summary>Clamped, so a chain reaction can never shake the screen into nausea.</summary>
        public void Add(float trauma)
        {
            _trauma = Mathf.Clamp01(_trauma + Mathf.Max(0f, trauma));
        }

        void LateUpdate()
        {
            if (_trauma <= 0f)
            {
                if (transform.localPosition != _origin) transform.localPosition = _origin;
                return;
            }

            // Squared: small hits stay a nudge, big ones really land.
            float amount = _trauma * _trauma * _maxOffset;

            // Unscaled time so the shake reads the same at 1x and at 5x game speed.
            float t = Time.unscaledTime * _frequency;
            float x = (Mathf.PerlinNoise(_seed, t) - 0.5f) * 2f;
            float y = (Mathf.PerlinNoise(_seed + 37f, t) - 0.5f) * 2f;

            // Along the camera's own axes, so it always reads as screen shake no matter the pitch.
            transform.localPosition = _origin + (transform.right * x + transform.up * y) * amount;

            // Scaled time here: at 5x the action is over faster, and so should the shake be.
            _trauma = Mathf.Max(0f, _trauma - Time.deltaTime * _decay);
        }
    }
}
