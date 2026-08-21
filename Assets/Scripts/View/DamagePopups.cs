using TMPro;
using UnityEngine;

namespace Proto
{
    /// <summary>
    /// Floating damage numbers over the swarm.
    ///
    /// The hard part is not drawing them, it is <b>not</b> drawing forty of them. One Blizzard
    /// covers thirty enemies in a single frame, and a zone re-ticks every 0.4s on the same crowd —
    /// one label per hit turns the battlefield into unreadable soup. So hits that land close
    /// together in space and time merge into one growing number instead.
    ///
    /// Size and warmth ride on the share of the target's max HP, not on the raw number: 40 damage
    /// means something very different at wave 2 and at wave 20, and the popup should read the same
    /// way the player feels it.
    /// </summary>
    public class DamagePopups
    {
        const int PoolSize = 48;

        const float LifeSpan = 0.75f;

        /// <summary>How long a number stays open for more hits to be folded into it.</summary>
        const float MergeWindow = 0.35f;

        const float MergeRadius = 1.4f;

        const float RiseSpeed = 1.9f;

        static readonly Color LightHit = new Color(1f, 0.96f, 0.86f);

        /// <summary>
        /// Warna crit, dan ia sengaja BUKAN warna skillnya.
        ///
        /// Keputusan pemilik project: crit harus terbaca SERAGAM. Seluruh sisa layar sudah
        /// warna-warni per skill; kalau crit ikut memakai warna skillnya, satu-satunya
        /// pembedanya tinggal ukuran — dan ukuran sudah dipakai untuk menyatakan seberapa besar
        /// gigitannya terhadap HP. Satu merah pekat untuk semua crit, apa pun yang melemparnya.
        /// </summary>
        static readonly Color CritInk = new Color(0.88f, 0.07f, 0.07f);

        /// <summary>Pengali ukuran huruf crit di atas ukuran yang sudah dihitung dari share HP.</summary>
        const float CritScale = 1.5f;

        class Popup
        {
            public TextMeshProUGUI Label;
            public RectTransform Rect;
            public Vector3 World;
            public float Amount;
            public float Life;
            public float Age;
            public float Bump;
            public Color Tint;
            public bool Crit;
        }

        readonly Popup[] _pool = new Popup[PoolSize];
        readonly Camera _camera;

        // Kanvas induk — skalanya membagi hasil WorldToScreenPoint. anchoredPosition
        // bersatuan UNIT KANVAS (piksel layar / scaleFactor); menulis piksel layar mentah
        // ke sana menggencet semua popup ke arah kiri-bawah begitu jendela game lebih
        // kecil dari resolusi referensi ("dmg popup ngumpul di pojok kiri bawah").
        readonly Canvas _canvasRef;

        float CanvasScale => _canvasRef != null ? Mathf.Max(0.0001f, _canvasRef.scaleFactor) : 1f;

        /// <summary>Satu material outline BERSAMA seluruh kolam — lihat catatan di konstruktor.</summary>
        Material _outlineMat;

        public DamagePopups(Transform canvas, TMP_FontAsset font, Camera camera)
        {
            _camera = camera;
            _canvasRef = canvas != null ? canvas.GetComponentInParent<Canvas>() : null;

            for (int i = 0; i < PoolSize; i++)
            {
                var go = new GameObject($"Dmg_{i}");
                go.transform.SetParent(canvas, false);

                var text = go.AddComponent<TextMeshProUGUI>();
                if (font != null) text.font = font;
                text.fontSize = 18;
                // TANPA FontStyles.Bold: font angkanya sudah SemiBold, dan faux-bold TMP
                // menebalkan dengan menggeser-geser vertex — digabung outline, angkanya
                // keluar sebagai gumpalan ("popup dmg berantakan", laporan pemilik project).
                text.alignment = TextAlignmentOptions.Center;
                text.raycastTarget = false;
                text.textWrappingMode = TextWrappingModes.NoWrap;
                text.overflowMode = TextOverflowModes.Overflow;
                text.text = "";

                // OUTLINE, bukan drop shadow.
                //
                // Bayangan cuma menebalkan satu sisi, dan angka damage melayang ke SEMUA arah di
                // atas latar yang berubah tiap frame — lantai gelap, kilatan skill yang menyala,
                // badan musuh, tepi lingkaran sihir. Sisi yang tidak dibayangi akan bertemu
                // sesuatu seterang dirinya sendiri, dan di situ angkanya lenyap.
                //
                // Outline SDF di material, dan materialnya SATU untuk seluruh kolam: menyetel
                // outlineWidth per label akan mencetak 48 material instance dan memecah batching
                // 48 label jadi 48 draw call. Alpha pudar tetap jalan — shader TMP mengalikan
                // face dan outline dengan alpha warna vertex.
                if (_outlineMat == null)
                {
                    _outlineMat = new Material(text.fontSharedMaterial);
                    // Font angka sekarang Cinzel — serif tipis, jadi badan hurufnya DITEBALKAN
                    // lewat FaceDilate, bukan lewat faux-bold yang menggeser vertex (lihat
                    // catatan di atas). Angka-angka ini pernah disetel lebih kecil dan TIDAK
                    // TAMPAK sama sekali ("gak ada outline-nya") — biangnya atlas Cinzel yang
                    // dibake dengan padding 5: rentang SDF setipis itu tidak menyisakan ruang
                    // untuk dilate maupun outline. Padding sudah dinaikkan ke 16 di asetnya;
                    // angka di sini baru berarti SETELAH itu, dan dua-duanya dinaikkan karena
                    // pemilik project minta huruf yang jelas tebal ber-outline hitam.
                    // Softness dipatok NOL: outline yang lembut melebur ke face dan yang
                    // terbaca mata cuma "font agak gelap" — garis hitamnya harus bertepi.
                    _outlineMat.SetFloat(ShaderUtilities.ID_FaceDilate, 0.24f);
                    _outlineMat.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.3f);
                    _outlineMat.SetFloat(ShaderUtilities.ID_OutlineSoftness, 0f);
                    _outlineMat.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0f, 0f, 0f, 1f));
                }
                text.fontSharedMaterial = _outlineMat;

                var rect = text.rectTransform;
                rect.anchorMin = rect.anchorMax = Vector2.zero;
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(220f, 40f);

                _pool[i] = new Popup { Label = text, Rect = rect };
            }
        }

        /// <summary>
        /// One damage event. <paramref name="maxHp"/> is what turns a raw number into a readable
        /// one — it decides how big and how hot the label gets.
        /// </summary>
        public void Push(Vector3 world, float amount, float maxHp, Color tint, bool crit = false)
        {
            if (amount <= 0f) return;

            var target = FindMergeTarget(world, crit);
            if (target != null)
            {
                target.Amount += amount;
                target.Life = LifeSpan;
                target.Bump = crit ? 1.6f : 1f;
                // Gabungan lintas skill memakai warna penyumbang TERAKHIR. Mencampur warna
                // menghasilkan lumpur cokelat yang bukan milik siapa-siapa.
                target.Tint = tint;
                Apply(target, maxHp);
                return;
            }

            var slot = FreeSlot();
            slot.World = world + Vector3.up * 1.1f;
            slot.Amount = amount;
            slot.Life = LifeSpan;
            slot.Age = 0f;
            slot.Bump = crit ? 1.6f : 1f;
            slot.Tint = tint;
            slot.Crit = crit;
            Apply(slot, maxHp);
        }

        /// <summary>
        /// Crit dan non-crit TIDAK PERNAH digabung, walau mendarat di petak dan detik yang sama.
        ///
        /// Penggabungan ada untuk menjaga layar tetap terbaca, dan itu benar selama yang digabung
        /// sama-sama "satu gigitan lagi". Crit bukan itu — crit adalah kejadian, dan menuangnya
        /// ke dalam angka biasa yang kebetulan ada di dekat situ menghapus satu-satunya kali
        /// pemain seharusnya melihatnya.
        /// </summary>
        Popup FindMergeTarget(Vector3 world, bool crit)
        {
            float bestSqr = MergeRadius * MergeRadius;
            Popup best = null;

            for (int i = 0; i < PoolSize; i++)
            {
                var p = _pool[i];
                if (p.Life <= 0f || p.Age > MergeWindow || p.Crit != crit) continue;

                Vector3 d = p.World - world;
                d.y = 0f;

                float sqr = d.sqrMagnitude;
                if (sqr >= bestSqr) continue;

                bestSqr = sqr;
                best = p;
            }

            return best;
        }

        /// <summary>An idle label, or the one closest to expiring when the pool is saturated.</summary>
        Popup FreeSlot()
        {
            Popup weakest = _pool[0];

            for (int i = 0; i < PoolSize; i++)
            {
                var p = _pool[i];
                if (p.Life <= 0f) return p;
                if (p.Life < weakest.Life) weakest = p;
            }

            return weakest;
        }

        /// <summary>
        /// Font size is set here rather than per frame on purpose: changing it rebuilds the text
        /// mesh, and doing that every frame for 48 labels shows up in the profiler. The per-frame
        /// animation rides on localScale instead, which is free.
        /// </summary>
        static void Apply(Popup p, float maxHp)
        {
            float share = maxHp <= 0f ? 0f : Mathf.Clamp01(p.Amount / maxHp);

            p.Label.text = Format(p.Amount);

            // Rentangnya dilebarkan (dulu 16-34): dengan angka besar, ukuran adalah satu-satunya
            // hal yang membedakan gigitan kecil dari pukulan yang mematikan — dua angka empat
            // digit yang seukuran terbaca sama saja betapa pun jauh bedanya.
            float size = Mathf.Lerp(20f, 54f, Mathf.Clamp01(share * 1.8f));
            if (p.Crit) size *= CritScale;

            p.Label.fontSize = Mathf.RoundToInt(size);

            if (p.Crit)
            {
                // Tanpa pemucatan share. Crit kecil tetap crit, dan memucatkannya ke krem akan
                // membuat separuh crit di game ini tampil sebagai angka biasa yang kebetulan
                // agak besar — persis yang tidak boleh terjadi pada satu-satunya kejadian yang
                // dijanjikan terbaca seragam.
                p.Label.color = CritInk;
                return;
            }

            // Warna = milik SKILL yang melukai; panasnya tetap dari share. Gigitan kecil
            // pucat mendekati krem supaya lantai tidak penuh konfeti pekat, pukulan besar
            // memakai warna skillnya utuh — merah api, biru es, hijau racun.
            p.Label.color = Color.Lerp(LightHit, p.Tint, Mathf.Lerp(0.45f, 1f, Mathf.Clamp01(share * 2.2f)));
        }

        static string Format(float amount) => BigNumber.Damage(amount);

        /// <summary>
        /// Driven with unscaled time: at 5x speed a scaled label would be gone before the eye
        /// registers it, and between waves time is stopped entirely while numbers still need to fade.
        /// </summary>
        public void Tick(float dt)
        {
            for (int i = 0; i < PoolSize; i++)
            {
                var p = _pool[i];

                if (p.Life <= 0f)
                {
                    if (p.Label.text.Length > 0) p.Label.text = "";
                    continue;
                }

                p.Life -= dt;
                p.Age += dt;

                if (p.Life <= 0f)
                {
                    p.Label.text = "";
                    continue;
                }

                p.World += Vector3.up * (RiseSpeed * dt);
                p.Bump = Mathf.MoveTowards(p.Bump, 0f, dt * 4f);

                var screen = _camera.WorldToScreenPoint(p.World) / CanvasScale;
                p.Rect.anchoredPosition = new Vector2(screen.x, screen.y);
                // Sentakan lahir lebih besar dan mengempis lebih lambat: itu bagian "jedar"-nya.
                p.Rect.localScale = Vector3.one * (1f + p.Bump * 0.8f);

                var c = p.Label.color;
                c.a = Mathf.Clamp01(p.Life / (LifeSpan * 0.5f));
                p.Label.color = c;
            }
        }
    }
}
