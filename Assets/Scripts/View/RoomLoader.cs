using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Proto
{
    /// <summary>
    /// Ruangan singgah — toko dan kejadian — sebagai scene sendiri yang dimuat
    /// <b>additive dan dipramuat</b>, lalu root-nya dinyala-matikan.
    ///
    /// Panelnya tidak pindah ke mana-mana: UI toko/kejadian tetap digambar
    /// <see cref="GrimoireUI"/> di kanvas yang sama. Yang pindah LATARNYA. Sebelum ini keduanya
    /// digambar di atas arena, dan arena itu tempat pertarungan — panel dagang yang mengambang di
    /// atas rumput yang barusan berdarah tidak pernah terbaca sebagai singgah, cuma sebagai jeda.
    ///
    /// <b>Pramuat ini sempat DICABUT lalu DIPASANG LAGI, dan pelajarannya layak dicatat.</b>
    /// Ia dicabut karena disangka membebani pertarungan. Ternyata tidak: root yang dimatikan tidak
    /// digambar sama sekali, jadi isi ruangan tidak pernah menyentuh frame time arena. Yang
    /// sebenarnya membuat game berat sama sekali di tempat lain — pass layar-penuh (Bloom, kabut,
    /// tonemapping, salinan layar) pada resolusi render 4K; 3840x2160 -> 1920x1080 memindahkan GPU
    /// frame time dari 39,9 ms ke 14,5 ms, sementara membuang 73% seluruh segitiga yang dikirim
    /// hanya menghemat 5,6 ms. Setelah itu ketahuan, pramuatnya dikembalikan — jeda muat di detik
    /// pemain menekan node adalah harga yang nyata, dan ia dibayar untuk keuntungan yang ternyata
    /// tidak pernah ada.
    ///
    /// Yang TIDAK ikut kembali: bug pemilihan scene-nya. Lihat <see cref="OnSceneLoaded"/>.
    ///
    /// Kamera arena DIMATIKAN lewat <c>Camera.enabled</c>, bukan lewat GameObject-nya.
    /// <c>AudioListener</c> menumpang di objek yang sama; mematikan objeknya akan membawa telinga
    /// scene ikut mati, dan console kembali penuh "no audio listeners".
    /// </summary>
    public class RoomLoader : MonoBehaviour
    {
        /// <summary>
        /// Nama scene per jenis node. Yang tidak terdaftar tidak punya ruangan.
        ///
        /// SLOT DICABUT atas perintah pemilik project, dan pencabutannya berlaku DI SINI: selama
        /// Room_Slot masih ikut dipramuat, ruangannya tetap bisa nongol tanpa node slot sama
        /// sekali. Satu-satunya cara yang benar-benar menutup jalur itu adalah tidak memuatnya.
        /// </summary>
        public const string ShopScene = "Room_Shop";
        public const string EventScene = "Room_Event";

        readonly Dictionary<string, List<GameObject>> _roots = new Dictionary<string, List<GameObject>>();
        readonly List<Camera> _roomCameras = new List<Camera>();

        /// <summary>
        /// Scene yang sudah diminta muat tapi belum mendarat. Ini kuncinya untuk mencocokkan
        /// callback <see cref="SceneManager.sceneLoaded"/> dengan permintaan kita sendiri, bukan
        /// dengan scene lain yang kebetulan mendarat berbarengan.
        /// </summary>
        readonly HashSet<string> _pending = new HashSet<string>();

        Camera _arenaCamera;

        /// <summary>Ruangan yang sedang tampil, atau null.</summary>
        string _shown;

        /// <summary>
        /// Ruangan yang DIMINTA tampil — belum tentu sama dengan <see cref="_shown"/>.
        ///
        /// Dipertahankan walau ruangannya dipramuat, karena pramuat tidak berarti seketika: run
        /// yang sangat pendek bisa sampai di node singgah sebelum scene-nya mendarat. Tanpa
        /// memisahkan "yang diminta" dari "yang tampil", ruangan yang sudah ditinggalkan bisa
        /// menyala sendiri beberapa frame setelah pemain pergi.
        /// </summary>
        string _want;

        /// <summary>Ruangan yang sedang tampil, atau null.</summary>
        public string Shown => _shown;

        public void Init(Camera arenaCamera)
        {
            _arenaCamera = arenaCamera;

            // Didaftarkan SEBELUM Preload: callback inilah yang membawa handle scene yang benar,
            // dan permintaan pertama bisa saja mendarat di frame yang sama.
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;

            Preload(ShopScene);
            Preload(EventScene);
        }

        void OnDestroy() => SceneManager.sceneLoaded -= OnSceneLoaded;

        /// <summary>
        /// Memuat satu ruangan di belakang layar, lalu langsung mematikannya.
        ///
        /// <b>Kalau scene-nya SUDAH terbuka di Hierarchy, dia yang dipakai — tidak dimuat lagi.</b>
        /// Ini bukan penghematan, ini perbaikan bug. Menggarap Room_Shop berarti scene itu ikut
        /// terbuka saat Play ditekan, dan memuatnya lagi menghasilkan kopi KEDUA yang utuh: isinya,
        /// kameranya, dan seluruh mesh-nya hidup berbarengan dengan yang pertama. Yang tampak di
        /// layar: game mendadak berat tanpa sebab yang kelihatan, dan latar ruangan nongol di
        /// tempat yang salah.
        ///
        /// Scene yang tidak ada di Build Settings dilewati dengan peringatan, bukan error yang
        /// menghentikan run: ruangan itu hiasan, dan hiasan yang belum dibuat tidak boleh
        /// membunuh permainan yang sudah jalan.
        /// </summary>
        void Preload(string sceneName)
        {
            var open = SceneManager.GetSceneByName(sceneName);
            if (open.IsValid() && open.isLoaded)
            {
                Adopt(open);
                return;
            }

            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogWarning("[RoomLoader] scene " + sceneName + " tidak ada di Build Settings — " +
                                 "ruangan itu dilewati, panelnya tetap tampil di atas arena.");
                return;
            }

            var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            if (op == null) return;

            _pending.Add(sceneName);
        }

        /// <summary>Menampilkan satu ruangan. Nama yang tidak dikenal, atau null, = kembali ke arena.</summary>
        public void Show(string sceneName)
        {
            _want = sceneName;
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

            ApplyCamera();
        }

        public void Hide() => Show(null);

        /// <summary>
        /// Menerima scene yang baru mendarat, lewat handle aslinya.
        ///
        /// Handle-nya dipakai apa adanya dan TIDAK dicari ulang lewat <c>GetSceneByName</c>: fungsi
        /// itu mengembalikan scene PERTAMA yang namanya cocok, jadi begitu ada dua yang bernama
        /// sama — persis yang terjadi kalau scene ruangan sudah terbuka di Hierarchy saat Play —
        /// yang dimatikan adalah yang pertama sementara kopi yang barusan dimuat ditinggal
        /// menyala. Itulah bug lamanya, dan ia mati di baris ini.
        /// </summary>
        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!_pending.Remove(scene.name)) return;

            Adopt(scene);

            // Mendarat terlambat, dan ternyata ruangan inilah yang sedang diminta? Nyalakan.
            // Kalau bukan, ia sudah dimatikan Adopt dan tinggal menunggu gilirannya.
            if (_want != scene.name) return;

            SetActive(scene.name, true);
            _shown = scene.name;
            ApplyCamera();
        }

        /// <summary>
        /// Mencatat root sebuah ruangan lalu mematikannya sampai ada yang memintanya.
        ///
        /// Root-nya DITAMBAHKAN ke daftar, bukan menimpanya. Kalau satu ruangan entah bagaimana
        /// masih berakhir punya dua kopi, kopi kedua ikut masuk daftar dan ikut dimatikan — jaring
        /// pengaman ini bekerja tanpa perlu tahu dari mana kopinya datang.
        /// </summary>
        void Adopt(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) return;

            List<GameObject> roots;
            if (!_roots.TryGetValue(scene.name, out roots))
            {
                roots = new List<GameObject>();
                _roots[scene.name] = roots;
            }

            var fresh = scene.GetRootGameObjects();
            for (int i = 0; i < fresh.Length; i++)
            {
                var root = fresh[i];
                if (root == null || roots.Contains(root)) continue;
                roots.Add(root);

                // Kamera dicatat SEBELUM root-nya dimatikan. GetComponentsInChildren pada objek
                // yang sudah mati tetap menemukannya kalau diminta menyertakan yang nonaktif,
                // tapi mengandalkan itu berarti bergantung pada satu argumen yang gampang hilang
                // saat kode ini disunting nanti.
                _roomCameras.AddRange(root.GetComponentsInChildren<Camera>(true));

                // Telinga kedua di scene membuat Unity mengeluh dan mencampur suara dua kali.
                // Ruangan tidak butuh telinganya sendiri — yang mendengar tetap kamera arena.
                foreach (var ear in root.GetComponentsInChildren<AudioListener>(true)) Destroy(ear);

                root.SetActive(false);
            }
        }

        void SetActive(string sceneName, bool on)
        {
            List<GameObject> roots;
            if (!_roots.TryGetValue(sceneName, out roots)) return;

            for (int i = 0; i < roots.Count; i++)
                if (roots[i] != null) roots[i].SetActive(on);
        }

        /// <summary>
        /// Kamera arena dimatikan HANYA kalau ruangannya benar-benar tampil dan benar-benar
        /// membawa kamera. Ruangan tanpa kamera yang mematikan kamera arena meninggalkan layar
        /// hitam — kegagalan yang jauh lebih buruk daripada latar yang tidak berganti.
        /// </summary>
        void ApplyCamera()
        {
            if (_arenaCamera == null) return;

            bool roomHasCamera = false;
            for (int i = 0; i < _roomCameras.Count; i++)
            {
                if (_roomCameras[i] == null) continue;
                if (_roomCameras[i].isActiveAndEnabled) roomHasCamera = true;
            }

            _arenaCamera.enabled = _shown == null || !roomHasCamera;
        }
    }
}
