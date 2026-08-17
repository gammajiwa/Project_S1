using UnityEngine;

namespace Proto
{
    /// <summary>
    /// Mata grimoire yang hidup di menu: melirik ke arah acak, kadang balik menatap
    /// lurus, dan sesekali "kedip" (bola dipipihkan sesaat — murah, tanpa kelopak).
    /// Cukup pasang di objek buku dan tunjuk <see cref="Eyeball"/> — bola mata hasil
    /// export Blender yang pivot-nya persis di pusat bola, jadi rotasi = lirikan.
    /// Tanpa armature, tanpa animator: transform doang, gaya kode UI kita.
    /// </summary>
    [AddComponentMenu("Grimoire/Grimoire Eye Rig")]
    public class GrimoireEyeRig : MonoBehaviour
    {
        [Tooltip("Transform bola mata (pivot di pusat bola). Rotasi lokal = arah lirik.")]
        public Transform Eyeball;

        [Header("Lirikan")]
        [Tooltip("Sudut lirik maksimum dari tatapan lurus, derajat.")]
        public float MaxAngle = 22f;

        [Tooltip("Jeda antar lirikan, detik — diundi di rentang x..y.")]
        public Vector2 GlanceInterval = new Vector2(1.4f, 4.2f);

        [Tooltip("Kecepatan menuju arah lirik baru (makin besar makin sigap).")]
        public float TurnSpeed = 7f;

        [Tooltip("Peluang lirikan berikutnya kembali menatap lurus ke depan.")]
        [Range(0f, 1f)] public float RecenterChance = 0.35f;

        [Header("Kedip")]
        [Tooltip("Jeda antar kedip, detik — diundi di rentang x..y. <= 0 = tidak kedip.")]
        public Vector2 BlinkInterval = new Vector2(4f, 9f);

        [Tooltip("Lama satu kedipan, detik.")]
        public float BlinkTime = 0.12f;

        Quaternion _baseRot;
        Vector3 _baseScale;
        Quaternion _target;
        float _nextGlance;
        float _nextBlink;
        float _blinkUntil;

        void Start()
        {
            if (Eyeball == null) { enabled = false; return; }

            _baseRot = Eyeball.localRotation;
            _baseScale = Eyeball.localScale;
            _target = _baseRot;
            _nextGlance = Time.time + Random.Range(GlanceInterval.x, GlanceInterval.y);
            _nextBlink = Time.time + Random.Range(BlinkInterval.x, BlinkInterval.y);
        }

        void Update()
        {
            float now = Time.time;

            if (now >= _nextGlance)
            {
                _nextGlance = now + Random.Range(GlanceInterval.x, GlanceInterval.y);
                if (Random.value < RecenterChance)
                {
                    _target = _baseRot;
                }
                else
                {
                    // arah lirik acak di dalam kerucut MaxAngle
                    Vector2 dir = Random.insideUnitCircle;
                    _target = _baseRot * Quaternion.Euler(
                        dir.y * MaxAngle, dir.x * MaxAngle, 0f);
                }
            }

            Eyeball.localRotation = Quaternion.Slerp(
                Eyeball.localRotation, _target, 1f - Mathf.Exp(-TurnSpeed * Time.deltaTime));

            if (BlinkInterval.y > 0f)
            {
                if (now >= _nextBlink)
                {
                    _blinkUntil = now + BlinkTime;
                    _nextBlink = now + Random.Range(BlinkInterval.x, BlinkInterval.y);
                }

                // kedip = pipihkan bola sesaat di sumbu tatap (murah tapi kebaca)
                bool closing = now < _blinkUntil;
                Vector3 s = _baseScale;
                if (closing)
                {
                    float t = Mathf.Sin(Mathf.PI * Mathf.Clamp01(
                        (_blinkUntil - now) / Mathf.Max(0.01f, BlinkTime)));
                    s.y = _baseScale.y * Mathf.Lerp(1f, 0.12f, t);
                }
                Eyeball.localScale = s;
            }
        }
    }
}
