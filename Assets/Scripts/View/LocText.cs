using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Proto
{
    /// <summary>
    /// Menempel pada sebuah label dan menuliskan kalimat dari <see cref="Loc"/> untuk kunci yang
    /// dipegangnya.
    ///
    /// Ada karena separuh UI permainan ini <b>dipanggang ke scene</b>: menu utama dan halaman
    /// setelan dibangun sekali oleh alat editor, dan teksnya sudah jadi isi aset scene sebelum
    /// permainan menyala. Teks seperti itu tidak bisa dilokalkan dengan memanggil
    /// <c>Loc.T</c> di suatu tempat — tidak ada tempat yang memanggilnya lagi. Yang bisa cuma
    /// benda yang ikut dipanggang bersamanya dan menulis ulang dirinya sendiri saat hidup.
    ///
    /// Mendengarkan <see cref="Loc.Changed"/>, jadi mengganti bahasa lewat setelan langsung
    /// terlihat di label yang sedang terbuka — termasuk label setelan itu sendiri.
    ///
    /// Label yang digambar ulang tiap frame (HUD di dalam run) TIDAK butuh ini; di sana
    /// memanggil <c>Loc.T</c> langsung lebih murah dan lebih jujur.
    /// </summary>
    [AddComponentMenu("Grimoire/Loc Text")]
    public class LocText : MonoBehaviour
    {
        [Tooltip("Kunci di tabel bahasa, misalnya \"menu.play\". Kosong = komponen ini diam.")]
        public string Key;

        [Tooltip("Dipakai apa adanya kalau kuncinya belum ada di tabel mana pun. Kosong = kunci " +
                 "yang bocor ke layar, dan itu memang disengaja: kunci mentah jelek dilihat, " +
                 "sedangkan label kosong lolos ke rilis tanpa ada yang sadar.")]
        [TextArea(1, 3)] public string Fallback;

        TextMeshProUGUI _tmp;
        Text _legacy;
        bool _found;

        void OnEnable()
        {
            Loc.Changed += Write;
            Write();
        }

        void OnDisable() => Loc.Changed -= Write;

#if UNITY_EDITOR
        void OnValidate()
        {
            if (isActiveAndEnabled) Write();
        }
#endif

        /// <summary>Menulis ulang label ini. Aman dipanggil kapan saja.</summary>
        public void Write()
        {
            if (string.IsNullOrEmpty(Key)) return;

            if (!_found)
            {
                _tmp = GetComponent<TextMeshProUGUI>();
                _legacy = GetComponent<Text>();
                _found = true;
            }

            var line = Loc.T(Key, Fallback);

            if (_tmp != null) _tmp.text = line;
            else if (_legacy != null) _legacy.text = line;
        }
    }
}
