using UnityEngine;

namespace Proto
{
    /// <summary>
    /// Putaran pelan yang tidak pernah berhenti — untuk sigil, lingkaran sihir, dan hiasan
    /// lain yang harus terlihat HIDUP tanpa punya alasan gameplay untuk bergerak.
    ///
    /// Sumbu dinyatakan di ruang LOKAL: quad yang sudah direbahkan berputar pada normalnya
    /// sendiri, bukan pada sumbu dunia yang kebetulan searah sebelum dia dimiringkan.
    /// </summary>
    [AddComponentMenu("Grimoire/Slow Spin")]
    public class SlowSpin : MonoBehaviour
    {
        [Tooltip("Derajat per detik. Negatif = arah sebaliknya.")]
        public float DegreesPerSecond = 25f;

        [Tooltip("Sumbu putar di ruang lokal objek ini.")]
        public Vector3 LocalAxis = Vector3.forward;

        void Update()
        {
            transform.Rotate(LocalAxis, DegreesPerSecond * Time.deltaTime, Space.Self);
        }
    }
}
