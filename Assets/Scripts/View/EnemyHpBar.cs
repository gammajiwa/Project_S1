using UnityEngine;
using UnityEngine.UI;

namespace Proto
{
    /// <summary>
    /// Satu bar HP di atas kepala musuh. <b>Placeholder</b> — bentuknya sengaja sesederhana
    /// mungkin supaya bisa diganti art tanpa menyentuh kode: yang dipegang kode cuma dua hal,
    /// <i>seberapa penuh</i> dan <i>sepekat apa</i>.
    ///
    /// Cara menggantinya: buka <c>EnemyHpBar.prefab</c>, timpa sprite <c>Back</c> dan
    /// <c>Fill</c>, atur ukurannya, selesai. Selama anak bernama <c>Fill</c> masih sebuah
    /// <see cref="Image"/>, isinya boleh apa saja.
    ///
    /// Musuh di permainan ini <b>tidak punya GameObject</b> — swarm-nya digambar instanced dari
    /// larik data. Jadi bar ini bukan anak siapa-siapa: ia hidup di kanvas UI dan diletakkan tiap
    /// frame oleh <see cref="EnemyHpBars"/> dari posisi dunia musuhnya.
    /// </summary>
    [AddComponentMenu("Grimoire/Enemy Hp Bar")]
    [RequireComponent(typeof(RectTransform))]
    public class EnemyHpBar : MonoBehaviour
    {
        [Tooltip("Palang belakang. Kosong = anak bernama \"Back\".")]
        [SerializeField] Image _back;

        [Tooltip("Palang isi. Kosong = anak bernama \"Fill\". Digambar dengan Image Type Filled.")]
        [SerializeField] Image _fill;

        [Header("Warna")]
        [Tooltip("Warna saat HP penuh.")]
        public Color Healthy = new Color(0.42f, 0.85f, 0.35f, 1f);

        [Tooltip("Warna saat HP tinggal sedikit. Peralihannya mengikuti sisa HP, bukan waktu.")]
        public Color Hurt = new Color(0.88f, 0.18f, 0.15f, 1f);

        RectTransform _rect;
        CanvasGroup _group;
        bool _ready;

        void Awake() => Ensure();

        void Ensure()
        {
            if (_ready) return;
            _ready = true;

            _rect = (RectTransform)transform;

            _group = GetComponent<CanvasGroup>();
            if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();

            // Bar ini melayang di atas medan perang dan tidak pernah diklik. Membiarkannya bisa
            // di-raycast berarti beberapa puluh palang kecil menelan klik yang sedang diarahkan
            // ke piece tercecer di lantai.
            _group.blocksRaycasts = false;
            _group.interactable = false;

            if (_back == null) _back = Child("Back");
            if (_fill == null) _fill = Child("Fill");

            if (_back != null) _back.raycastTarget = false;

            if (_fill != null)
            {
                _fill.raycastTarget = false;

                // Dipaksa Filled di sini, bukan cuma diandalkan dari prefabnya: art pengganti
                // hampir selalu masuk sebagai Simple, dan Simple mengabaikan fillAmount tanpa
                // sepatah kata — barnya akan tampak penuh terus dan tidak ada yang tahu kenapa.
                _fill.type = Image.Type.Filled;
                _fill.fillMethod = Image.FillMethod.Horizontal;
                _fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            }
        }

        Image Child(string name)
        {
            var found = transform.Find(name);
            return found != null ? found.GetComponent<Image>() : null;
        }

        /// <summary>Letak di layar, dalam piksel kanvas.</summary>
        public void PlaceAt(Vector2 screen)
        {
            Ensure();
            _rect.anchoredPosition = screen;
        }

        /// <summary>Seberapa penuh (0..1) dan sepekat apa (0..1).</summary>
        public void Set(float ratio, float alpha)
        {
            Ensure();

            ratio = Mathf.Clamp01(ratio);

            if (_fill != null)
            {
                _fill.fillAmount = ratio;

                // Warnanya mengikuti SISA HP, bukan berapa besar pukulan terakhirnya. Yang perlu
                // dijawab bar ini cuma satu pertanyaan: masih perlu berapa pukulan lagi.
                _fill.color = Color.Lerp(Hurt, Healthy, ratio);
            }

            if (_group != null) _group.alpha = Mathf.Clamp01(alpha);
        }
    }
}
