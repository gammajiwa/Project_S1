using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Proto.EditorTools
{
    /// <summary>
    /// Membuat scene Playground dan mendaftarkannya ke Build Settings.
    ///
    /// Digenerate, bukan diedit tangan, dengan alasan yang sama seperti MainMenu: seluruh isinya
    /// dibangun runtime oleh satu komponen, jadi satu-satunya hal yang perlu ada di file scene
    /// adalah komponen itu beserta referensi asetnya. Scene yang disunting tangan akan mengumpulkan
    /// objek yatim yang tidak ada di kode dan tidak ada yang tahu asalnya.
    /// </summary>
    public static class PlaygroundSceneBuilder
    {
        const string Root = "Assets/GameData";
        const string ScenePath = "Assets/Scenes/Playground.unity";

        [MenuItem("Tools/Grimoire/Build Playground Scene")]
        public static void Run()
        {
            var db = AssetDatabase.LoadAssetAtPath<ContentDatabase>(Root + "/ContentDatabase.asset");
            var balance = AssetDatabase.LoadAssetAtPath<GameBalance>(Root + "/GameBalance.asset");
            var look = AssetDatabase.LoadAssetAtPath<SceneLook>(Root + "/SceneLook_Game.asset");

            if (db == null || balance == null || look == null)
            {
                Debug.LogError("[Playground] ContentDatabase / GameBalance / SceneLook_Game " +
                               "tidak lengkap di " + Root);
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var go = new GameObject("_Playground");
            var boot = go.AddComponent<PlaygroundBootstrap>();

            var so = new SerializedObject(boot);
            so.FindProperty("_database").objectReferenceValue = db;
            so.FindProperty("_balance").objectReferenceValue = balance;
            so.FindProperty("_look").objectReferenceValue = look;
            so.ApplyModifiedProperties();

            EditorSceneManager.SaveScene(scene, ScenePath);
            Register();

            Debug.Log("[Playground] " + ScenePath + " dibuat dan terdaftar di Build Settings.");
        }

        /// <summary>
        /// Ditambahkan di URUTAN TERAKHIR dan dalam keadaan MATI.
        ///
        /// Urutan scene itu bermakna: indeks 0 adalah yang dijalankan saat game dibuka. Menyelipkan
        /// scene uji di depan berarti build yang dikirim ke client membuka ruang uji, bukan gamenya.
        /// Dimatikan pula supaya tidak ikut terbungkus ke dalam build rilis sama sekali — ESC di
        /// dalamnya tetap bisa kembali ke MainMenu saat dijalankan dari editor.
        /// </summary>
        static void Register()
        {
            var scenes = EditorBuildSettings.scenes;

            for (int i = 0; i < scenes.Length; i++)
            {
                if (scenes[i].path != ScenePath) continue;

                Debug.Log("[Playground] sudah terdaftar di Build Settings, indeks " + i + ".");
                return;
            }

            var grown = new EditorBuildSettingsScene[scenes.Length + 1];
            System.Array.Copy(scenes, grown, scenes.Length);
            grown[scenes.Length] = new EditorBuildSettingsScene(ScenePath, false);

            EditorBuildSettings.scenes = grown;
        }
    }
}
