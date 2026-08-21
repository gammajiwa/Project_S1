using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Proto.EditorTools
{
    /// <summary>
    /// Ruang uji: satu jendela untuk melompat ke keadaan yang mustahil dicapai dengan bermain.
    ///
    /// Ada karena pertanyaan "skill sudah OP belum" tidak bisa dijawab dari wave 1. Menjawabnya
    /// menuntut papan ★5 yang tersusun, wave tinggi, dan boss di lapangan — tiga hal yang butuh
    /// setengah jam bermain untuk dicapai, setiap kali, untuk satu kali lihat.
    ///
    /// SEMUANYA runtime saja. Tidak ada satu pun ScriptableObject yang disentuh: mengubah aset
    /// saat play mode TIDAK kembali sendiri saat stop, dan nilai uji yang tertinggal di aset akan
    /// tertulis sebagai nilai desain oleh generator berikutnya. Itu jebakan #22 di AI-HANDOFF,
    /// dan jendela ini dibangun untuk tidak bisa menginjaknya.
    ///
    /// Refleksi dipakai untuk field non-publik (Hp, Mana, Alive). Itu memang jelek, dan memang
    /// disengaja: alternatifnya melubangi enkapsulasi kode runtime demi sebuah alat editor, dan
    /// lubang itu akan tetap ada di build yang dikirim.
    /// </summary>
    public class TestBenchWindow : EditorWindow
    {
        const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        [MenuItem("Tools/Grimoire/Ruang Uji")]
        public static void Open()
        {
            var window = GetWindow<TestBenchWindow>("Ruang Uji");
            window.minSize = new Vector2(300f, 380f);
        }

        int _wave = 40;
        int _bossKind;
        int _minStars = 4;

        void OnInspectorUpdate() => Repaint();

        void OnGUI()
        {
            EditorGUILayout.LabelField("Ruang Uji", EditorStyles.boldLabel);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "Masuk Play mode dulu. Semua yang di sini bekerja pada objek yang hidup, " +
                    "dan tidak satu pun menyentuh aset — jadi apa pun yang kamu tekan hilang " +
                    "sendiri begitu Play berhenti.", MessageType.Info);
                return;
            }

            var caster = Find("Proto.PlayerCaster");
            var enemies = Find("Proto.EnemyManager");

            if (caster == null || enemies == null)
            {
                EditorGUILayout.HelpBox("Scene game belum terbangun. Buka Proto.unity lalu Play.",
                    MessageType.Warning);
                return;
            }

            DrawReadout(caster, enemies);
            EditorGUILayout.Space(8f);
            DrawWave(enemies);
            EditorGUILayout.Space(8f);
            DrawBoard(caster);
            EditorGUILayout.Space(8f);
            DrawBoss(enemies);
            EditorGUILayout.Space(8f);
            DrawVitals(caster);
            EditorGUILayout.Space(8f);
            DrawSpeed();
        }

        // ---------- bagian ----------

        void DrawReadout(Object caster, Object enemies)
        {
            EditorGUILayout.LabelField("Keadaan", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.LabelField("Wave", "" + Prop(enemies, "Wave"));
                EditorGUILayout.LabelField("Musuh hidup", "" + Prop(enemies, "AliveCount"));
                EditorGUILayout.LabelField("Boss aktif", "" + Prop(enemies, "BossActive"));
                EditorGUILayout.LabelField("HP pemain",
                    Field(caster, "Hp") + " / " + Prop(caster, "MaxHp"));
                EditorGUILayout.LabelField("Mana", "" + Field(caster, "Mana"));
            }
        }

        void DrawWave(Object enemies)
        {
            EditorGUILayout.LabelField("Wave", EditorStyles.boldLabel);
            _wave = EditorGUILayout.IntSlider("Nomor wave", _wave, 1, 80);

            if (GUILayout.Button("Mulai wave " + _wave))
            {
                Call(enemies, "StartWave", _wave);
                SetProp(enemies, "Wave", _wave);
            }
        }

        void DrawBoard(Object caster)
        {
            EditorGUILayout.LabelField("Papan", EditorStyles.boldLabel);
            _minStars = EditorGUILayout.IntSlider("Bintang minimal", _minStars, 1, 5);

            if (GUILayout.Button("Isi papan (rune ★3 + skill ★" + _minStars + ")"))
            {
                int r, s;
                FillBoard(caster, _minStars, out r, out s);
                Debug.Log($"[Ruang Uji] {r} rune dan {s} skill didudukkan. " +
                          "Yang tidak muat memang tidak muat — footprint bintang tinggi " +
                          "memakan papan, dan itu bagian dari desainnya.");
            }
        }

        void DrawBoss(Object enemies)
        {
            EditorGUILayout.LabelField("Boss", EditorStyles.boldLabel);
            _bossKind = EditorGUILayout.IntSlider("Jenis (0-2)", _bossKind, 0, 2);

            if (GUILayout.Button("Munculkan boss"))
            {
                // Wave dulu kalau belum berjalan: StartWave menyapu boss yang sudah ada, jadi
                // memanggilnya SESUDAH boss lahir akan menghapus boss yang barusan diminta.
                if (!(bool)Prop(enemies, "WaveActive")) Call(enemies, "StartWave", _wave);
                Call(enemies, "SpawnBoss", _bossKind);
            }
        }

        void DrawVitals(Object caster)
        {
            EditorGUILayout.LabelField("Nyawa & mana", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Isi penuh"))
                {
                    SetField(caster, "Alive", true);
                    SetField(caster, "Hp", System.Convert.ToSingle(Prop(caster, "MaxHp")));
                    SetField(caster, "Mana", System.Convert.ToSingle(Prop(caster, "MaxMana")));
                }

                if (GUILayout.Button("Mana tak terbatas"))
                {
                    SetField(caster, "Mana", 99999f);
                }
            }

            EditorGUILayout.HelpBox(
                "Mana tak terbatas MENGUBAH kesimpulan, bukan cuma mempercepatnya. Mana adalah " +
                "rem sesungguhnya di endgame — papan ★5 mentok di sekitar 10 dari 80 mana. " +
                "Dilepas, damage yang terukur bukan lagi damage yang akan dialami pemain.",
                MessageType.Warning);
        }

        void DrawSpeed()
        {
            EditorGUILayout.LabelField("Kecepatan", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                foreach (float speed in new[] { 0.25f, 1f, 2f, 4f, 8f })
                {
                    if (GUILayout.Button(speed + "x")) Time.timeScale = speed;
                }
            }
        }

        // ---------- papan ----------

        static void FillBoard(Object caster, int minStars, out int runes, out int skills)
        {
            runes = 0;
            skills = 0;

            var book = caster.GetType().GetProperty("Book").GetValue(caster);
            var bookType = book.GetType();
            var place = bookType.GetMethod("Place", Any);
            var canPlace = bookType.GetMethod("CanPlace", Any);
            if (place == null || canPlace == null) return;

            var runeList = new List<ScriptableObject>();
            var skillList = new List<ScriptableObject>();

            foreach (string guid in AssetDatabase.FindAssets("t:PieceDefinition"))
            {
                var piece = AssetDatabase.LoadAssetAtPath<ScriptableObject>(
                    AssetDatabase.GUIDToAssetPath(guid));

                var type = piece.GetType();
                string layer = "" + type.GetField("Layer").GetValue(piece);
                int stars = (int)type.GetField("Stars").GetValue(piece);

                if (layer == "Rune" && stars >= 3) runeList.Add(piece);
                if (layer == "Skill" && stars >= minStars) skillList.Add(piece);
            }

            // Rune DULU. Skill menuntut alas, dan skill yang didudukkan lebih dulu akan memakan
            // petak yang runenya masih butuh — hasilnya papan penuh skill tanpa satu pun aura.
            runes = Seat(book, canPlace, place, runeList);
            skills = Seat(book, canPlace, place, skillList);

            var compile = bookType.GetMethod("Compile", Any);
            if (compile != null) compile.Invoke(book, null);
        }

        static int Seat(object book, MethodInfo canPlace, MethodInfo place,
            List<ScriptableObject> pieces)
        {
            int seated = 0;

            foreach (var piece in pieces)
            {
                bool done = false;

                for (int y = 0; y < 7 && !done; y++)
                for (int x = 0; x < 7 && !done; x++)
                {
                    var cell = new Vector2Int(x, y);
                    if (!(bool)canPlace.Invoke(book, new object[] { piece, cell, 0 })) continue;

                    place.Invoke(book, new object[] { piece, cell, 0 });
                    seated++;
                    done = true;
                }
            }

            return seated;
        }

        // ---------- refleksi ----------

        static Object Find(string typeName)
        {
            var type = System.Type.GetType(typeName + ", Assembly-CSharp");
            if (type == null) return null;

            var found = Object.FindObjectsByType(type, FindObjectsSortMode.None);
            return found.Length > 0 ? found[0] : null;
        }

        static object Prop(Object target, string name)
        {
            var p = target.GetType().GetProperty(name, Any);
            return p == null ? "-" : p.GetValue(target);
        }

        static void SetProp(Object target, string name, object value)
        {
            var p = target.GetType().GetProperty(name, Any);
            if (p != null && p.SetMethod != null) p.SetValue(target, value);
        }

        static object Field(Object target, string name)
        {
            var f = target.GetType().GetField(name, Any);
            return f == null ? "-" : f.GetValue(target);
        }

        static void SetField(Object target, string name, object value)
        {
            var f = target.GetType().GetField(name, Any);
            if (f != null) f.SetValue(target, value);
        }

        static void Call(Object target, string name, params object[] args)
        {
            var m = target.GetType().GetMethod(name, Any);
            if (m != null) m.Invoke(target, args);
        }
    }
}
