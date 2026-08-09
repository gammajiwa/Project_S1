using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Proto
{
    /// <summary>
    /// Ruangan singgah — toko, kejadian, dan slot — sebagai scene sendiri yang dimuat
    /// <b>additive dan dipramuat</b>.
    ///
    /// Panelnya tidak pindah ke mana-mana: UI toko/kejadian/slot tetap digambar
    /// <see cref="GrimoireUI"/> di kanvas yang sama. Yang pindah LATARNYA. Sebelum ini ketiganya
    /// digambar di atas arena, dan arena itu tempat pertarungan — panel dagang yang mengambang di
    /// atas rumput yang barusan berdarah tidak pernah terbaca sebagai singgah, cuma sebagai jeda.
    ///
    /// Ketiganya dimuat SEKALI di awal run lalu root-nya dinyala-matikan, bukan dimuat saat
    /// dibutuhkan. Itu pilihan pemilik project dan alasannya benar: memuat scene saat pemain
    /// menekan node berarti membayar jeda tepat di detik yang paling tidak sabar. Harganya memori
    /// — isi ketiga ruangan selalu ada — dan itu murah untuk tiga ruangan kecil tanpa gerombolan.
    ///
    /// Kamera arena DIMATIKAN lewat <c>Camera.enabled</c>, bukan lewat GameObject-nya.
    /// <c>AudioListener</c> menumpang di objek yang sama; mematikan objeknya akan membawa telinga
    /// scene ikut mati, dan console kembali penuh "no audio listeners" — persis yang barusan
    /// dibereskan.
    /// </summary>
    public class RoomLoader : MonoBehaviour
    {
        /// <summary>Nama scene per jenis node. Yang tidak terdaftar tidak punya ruangan.</summary>
        public const string ShopScene = "Room_Shop";
        public const string EventScene = "Room_Event";
        public const string SlotScene = "Room_Slot";

        readonly Dictionary<string, List<GameObject>> _roots = new Dictionary<string, List<GameObject>>();
        readonly List<Camera> _roomCameras = new List<Camera>();

        Camera _arenaCamera;
        string _shown;

        /// <summary>Ruangan yang sedang tampil, atau null.</summary>
        public string Shown => _shown;

        public void Init(Camera arenaCamera)
        {
            _arenaCamera = arenaCamera;

            Preload(ShopScene);
            Preload(EventScene);
            Preload(SlotScene);
        }

        /// <summary>
        /// Memuat satu ruangan di belakang layar, lalu langsung mematikannya.
        ///
        /// Scene yang tidak ada di Build Settings dilewati dengan peringatan, bukan error yang
        /// menghentikan run: ruangan itu hiasan, dan hiasan yang belum dibuat tidak boleh
        /// membunuh permainan yang sudah jalan.
        /// </summary>
        void Preload(string sceneName)
        {
            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogWarning("[RoomLoader] scene '" + sceneName + "' tidak ada di Build Settings — " +
                                 "ruangan itu dilewati, panelnya tetap tampil di atas arena.");
                return;
            }

            var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            if (op == null) return;

            op.completed += _ => Adopt(sceneName);
        }

        void Adopt(string sceneName)
        {
            var scene = SceneManager.GetSceneByName(sceneName);
            if (!scene.IsValid() || !scene.isLoaded) return;

            var roots = new List<GameObject>(scene.GetRootGameObjects());
            _roots[sceneName] = roots;

            for (int i = 0; i < roots.Count; i++)
            {
                // Kamera dicatat SEBELUM root-nya dimatikan. GetComponentsInChildren pada objek
                // yang sudah mati tetap menemukannya kalau diminta menyertakan yang nonaktif,
                // tapi mengandalkan itu berarti bergantung pada satu argumen yang gampang hilang
                // saat kode ini disunting nanti.
                _roomCameras.AddRange(roots[i].GetComponentsInChildren<Camera>(true));

                // Telinga kedua di scene membuat Unity mengeluh dan mencampur suara dua kali.
                // Ruangan tidak butuh telinganya sendiri — yang mendengar tetap kamera arena.
                foreach (var ear in roots[i].GetComponentsInChildren<AudioListener>(true)) Destroy(ear);

                roots[i].SetActive(false);
            }
        }

        /// <summary>Menampilkan satu ruangan. Nama yang tidak dikenal = sembunyikan semuanya.</summary>
        public void Show(string sceneName)
        {
            if (_shown == sceneName) return;

            _shown = null;

            foreach (var pair in _roots)
            {
                bool on = pair.Key == sceneName;
                for (int i = 0; i < pair.Value.Count; i++)
                {
                    if (pair.Value[i] != null) pair.Value[i].SetActive(on);
                }

                if (on) _shown = sceneName;
            }

            // Kamera arena dimatikan HANYA kalau ruangannya benar-benar tampil dan benar-benar
            // membawa kamera. Ruangan tanpa kamera yang mematikan kamera arena meninggalkan layar
            // hitam - kegagalan yang jauh lebih buruk daripada latar yang tidak berganti.
            bool roomHasCamera = false;
            for (int i = 0; i < _roomCameras.Count; i++)
            {
                if (_roomCameras[i] == null) continue;
                if (_roomCameras[i].isActiveAndEnabled) roomHasCamera = true;
            }

            if (_arenaCamera != null) _arenaCamera.enabled = _shown == null || !roomHasCamera;
        }

        public void Hide() => Show(null);
    }
}
