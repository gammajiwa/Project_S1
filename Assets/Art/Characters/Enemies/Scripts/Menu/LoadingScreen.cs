using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Proto
{
    /// <summary>
    /// Tirai yang menutup layar selama scene berikutnya dimuat — sigil berputar di atas kegelapan.
    ///
    /// Permintaan pemilik project: <i>"perlu loading biar nutupnya clean"</i>. Sebelum ini
    /// perpindahan memakai <c>SceneManager.LoadScene</c> yang SINKRON: satu frame terakhir menu
    /// tergambar, lalu seluruh proses berhenti selama scene game dibangun, lalu frame pertama
    /// arena muncul mentah. Yang terlihat pemain bukan "memuat", melainkan game yang membeku
    /// lalu berkedip.
    ///
    /// Tiga hal yang membuat tirai ini bekerja, dan ketiganya perlu:
    ///
    /// 1. <b>Hidup melewati pergantian scene</b> (<c>DontDestroyOnLoad</c>). Tirai yang lahir di
    ///    scene lama akan ikut mati bersamanya — tepat di detik ia paling dibutuhkan.
    /// 2. <b>Menahan aktivasi</b> (<c>allowSceneActivation = false</c>) sampai tirainya benar-benar
    ///    pekat. Tanpa itu scene baru bisa muncul di tengah fade dan pemain melihat potongan
    ///    arena menembus kegelapan yang belum selesai.
    /// 3. <b>Durasi minimum.</b> Scene yang dimuat lebih cepat dari fade-nya membuat tirai
    ///    berkedip masuk-keluar, dan kedipan terbaca sebagai glitch — justru lebih buruk daripada
    ///    tidak ada tirai sama sekali.
    /// </summary>
    public class LoadingScreen : MonoBehaviour
    {
        const string SigilPath = "Art/VFX/Packs/Hovl Studio/Magic effects pack/Textures/MagicCircle";

        /// <summary>Detik. Di bawah ini tirainya berkedip alih-alih menutup.</summary>
        const float MinimumVisible = 1.1f;

        const float FadeIn = 0.35f;
        const float FadeOut = 0.45f;

        /// <summary>
        /// Menutup layar, memuat <paramref name="sceneName"/>, lalu membuka lagi.
        ///
        /// Aman dipanggil dari mana pun: tirainya membangun kanvasnya sendiri dan tidak menuntut
        /// apa pun ada di scene pemanggilnya.
        /// </summary>
        public static void Go(string sceneName, Sprite sigil, Color ink)
        {
            if (string.IsNullOrEmpty(sceneName)) return;

            var go = new GameObject("LoadingScreen");
            DontDestroyOnLoad(go);

            var screen = go.AddComponent<LoadingScreen>();
            screen.Build(sigil, ink);
            screen.StartCoroutine(screen.Run(sceneName));
        }

        CanvasGroup _group;
        RectTransform _outer;
        RectTransform _inner;

        void Build(Sprite sigil, Color ink)
        {
            var canvasGo = new GameObject("Canvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);

            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            // Di atas SEGALANYA. Kanvas lain di game ini memakai urutan bawaan, jadi angka
            // setinggi ini tidak bisa dikalahkan tanpa seseorang sengaja melakukannya.
            canvas.sortingOrder = 32000;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            _group = canvasGo.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.blocksRaycasts = true;

            // Tirai pekat. Sengaja BUKAN hitam murni: seluruh permainan ini ungu malam, dan
            // hitam murni di tengahnya terbaca sebagai layar mati, bukan sebagai tirai.
            var veil = NewImage(canvasGo.transform, "Veil", new Color(0.035f, 0.018f, 0.062f, 1f));
            Stretch(veil.rectTransform);

            _outer = NewSigil(canvasGo.transform, "SigilOuter", sigil, ink, 420f);
            _inner = NewSigil(canvasGo.transform, "SigilInner", sigil,
                new Color(ink.r * 0.7f, ink.g * 0.8f, ink.b, ink.a * 0.75f), 250f);
        }

        RectTransform NewSigil(Transform parent, string name, Sprite sprite, Color ink, float size)
        {
            var image = NewImage(parent, name, ink, sprite);

            var rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(size, size);
            rect.anchoredPosition = Vector2.zero;

            // Aditif: tekstur sihir digambar di atas hitam pekat, bukan di atas alpha kosong.
            var shader = Shader.Find("Grimoire/UiAdditive");
            if (shader != null) image.material = new Material(shader);

            return rect;
        }

        static Image NewImage(Transform parent, string name, Color color, Sprite sprite = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var image = go.AddComponent<Image>();
            image.color = color;
            image.sprite = sprite;
            image.raycastTarget = false;
            return image;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        void Update()
        {
            // Berlawanan arah, dan itu yang membuat dua cincin terbaca sebagai satu MEKANISME
            // alih-alih satu gambar yang diputar. Unscaled: scene game bisa memulai dengan
            // timeScale nol (fase menyusun grid membekukan waktu), dan sigil yang membeku di
            // tengah layar loading terbaca sebagai game yang hang.
            float dt = Time.unscaledDeltaTime;
            if (_outer != null) _outer.Rotate(0f, 0f, -38f * dt);
            if (_inner != null) _inner.Rotate(0f, 0f, 64f * dt);
        }

        IEnumerator Run(string sceneName)
        {
            yield return Fade(0f, 1f, FadeIn);

            var load = SceneManager.LoadSceneAsync(sceneName);
            load.allowSceneActivation = false;

            float shown = 0f;

            // 0,9 bukan 1: Unity menahan sisa 10% terakhir sampai aktivasi diizinkan, jadi
            // menunggu progress mencapai 1 adalah menunggu sesuatu yang tidak akan pernah terjadi.
            while (load.progress < 0.9f || shown < MinimumVisible)
            {
                shown += Time.unscaledDeltaTime;
                yield return null;
            }

            load.allowSceneActivation = true;
            while (!load.isDone) yield return null;

            // Satu frame ekstra: scene baru baru saja aktif dan Awake/Start-nya belum menggambar
            // apa pun. Membuka tirai di frame ini memperlihatkan arena setengah jadi.
            yield return null;

            yield return Fade(1f, 0f, FadeOut);
            Destroy(gameObject);
        }

        IEnumerator Fade(float from, float to, float seconds)
        {
            float t = 0f;
            _group.alpha = from;

            while (t < seconds)
            {
                t += Time.unscaledDeltaTime;
                _group.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / seconds));
                yield return null;
            }

            _group.alpha = to;
        }
    }
}
