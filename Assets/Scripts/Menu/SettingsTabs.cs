using UnityEngine;
using UnityEngine.UI;

namespace Proto
{
    /// <summary>
    /// Memecah halaman setelan jadi beberapa sub-halaman yang bergantian tampil.
    ///
    /// Alasannya bukan estetika: satu daftar berisi empat seksi memaksa mata memindai seluruh
    /// tinggi layar untuk menemukan satu baris, dan panel setinggi itu tidak menyisakan ruang
    /// untuk baris baru — tiap tambahan mendorong tombol di dasar panel sampai bertindihan.
    /// Dengan rail tab, yang tumbuh cuma satu sub-halaman, dan sisanya tidak ikut bergeser.
    ///
    /// Halamannya dipertukarkan lewat SetActive, jadi baris yang tidak terlihat juga tidak
    /// dihitung layout dan tidak menerima klik — bukan sekadar disembunyikan.
    /// </summary>
    public class SettingsTabs : MonoBehaviour
    {
        [Tooltip("Baris tab di rail kiri. Urutannya harus sejajar dengan Pages.")]
        [SerializeField] MenuLine[] _lines;

        [Tooltip("Badan sub-halaman. Urutannya harus sejajar dengan Lines.")]
        [SerializeField] GameObject[] _pages;

        int _index;

        void Awake()
        {
            if (_lines == null) return;

            for (int i = 0; i < _lines.Length; i++)
            {
                if (_lines[i] == null) continue;

                var button = _lines[i].GetComponent<Button>();
                if (button == null) continue;

                // Ditangkap per iterasi — satu variabel bersama akan membuat keempat tab
                // membuka halaman terakhir.
                int index = i;
                button.onClick.AddListener(() => Select(index));
            }
        }

        /// <summary>
        /// Selalu membuka di tab pertama, bukan di tab yang terakhir dilihat. Panel ini muncul
        /// dari dua tempat (menu utama dan ESC di dalam run) dan sering cuma untuk satu setelan;
        /// posisi awal yang bisa ditebak lebih murah daripada yang "pintar".
        /// </summary>
        void OnEnable() => Select(0);

        public void Select(int index)
        {
            if (_pages == null || _pages.Length == 0) return;

            _index = Mathf.Clamp(index, 0, _pages.Length - 1);

            for (int i = 0; i < _pages.Length; i++)
            {
                if (_pages[i] != null) _pages[i].SetActive(i == _index);
            }

            if (_lines == null) return;

            for (int i = 0; i < _lines.Length; i++)
            {
                if (_lines[i] != null) _lines[i].Sticky = i == _index;
            }
        }
    }
}
