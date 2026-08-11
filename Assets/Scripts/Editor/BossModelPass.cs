using UnityEditor;
using UnityEngine;

namespace Proto.EditorTools
{
    /// <summary>
    /// Memasang model SnakeBoss ke SEMUA boss: kepala, ruas badan, ekor, dan teksturnya.
    ///
    /// Terpisah dari <see cref="BossPass"/> yang menulis angka-angka gameplay-nya. Alasannya:
    /// model diganti jauh lebih sering daripada angka, dan menggabungkan keduanya berarti setiap
    /// kali art ditukar, seluruh nyawa, kecepatan, dan jeda serangan boss ikut ditulis ulang ke
    /// nilai bawaan — menghapus setiap penyetelan tangan yang pernah dilakukan.
    ///
    /// <b>Skala dihitung dari BOUNDS mesh, tidak diketik.</b> Mesh-nya diimpor sangat kecil
    /// (kepala 0,079 unit panjangnya), dan angka pengali yang diketik tangan akan diam-diam salah
    /// begitu asetnya diekspor ulang dengan skala berbeda. Yang ditulis di sini adalah UKURAN YANG
    /// DIINGINKAN dalam unit dunia; pengalinya diturunkan.
    /// </summary>
    public static class BossModelPass
    {
        const string Folder = "Assets/Art/Characters/Bosses/SnakeBoss";
        const string Db = "Assets/GameData/ContentDatabase.asset";

        /// <summary>
        /// Seberapa jauh satu ruas harus MENIMPA ruas berikutnya, sebagai kelipatan jaraknya.
        ///
        /// Bukan 1,0. Ruas yang panjangnya persis sama dengan jaraknya bersambung hanya saat
        /// ularnya lurus — begitu ia membelok, sisi luar tikungan merentang dan sambungannya
        /// menganga. Seperlima kelebihan cukup untuk menutup tikungan paling tajam yang bisa
        /// dihasilkan TurnRate.
        /// </summary>
        const float JoinOverlap = 1.2f;

        // Tidak ada "panjang yang diinginkan" di sini, dan itu koreksi dari percobaan sebelumnya.
        //
        // Versi pertama memaksa tiap kepala jadi 2,4 unit. Hasilnya terukur: ketiga boss punya
        // kepala sama besar — termasuk `grub`, ANAK BUAH yang seluruh gunanya kecil. Yang salah
        // bukan angkanya melainkan idenya: memaksa panjang akhir berarti membagi habis `HeadScale`,
        // dan `HeadScale` justru satu-satunya bidang yang membedakan ukuran ketiganya.
        //
        // Yang benar sesuai maksud bidangnya: pengali aset menormalkan mesh ke SATU UNIT, lalu
        // `HeadScale` tiap boss membacanya sebagai ukuran dalam unit. Kepala ber-HeadScale 2,6
        // menjadi 2,6 unit; grub ber-HeadScale 1,15 menjadi 1,15 unit.

        [MenuItem("Tools/Grimoire/Pasang Model Boss")]
        public static void Run()
        {
            var db = AssetDatabase.LoadAssetAtPath<ContentDatabase>(Db);
            if (db == null)
            {
                Debug.LogError("[BossModel] ContentDatabase.asset tidak ketemu.");
                return;
            }

            // Dicocokkan lewat NAMA FILE, bukan lewat urutan folder.
            //
            // Ini satu-satunya tempat kepala bisa tertukar dengan ekor, dan tertukarnya tidak
            // akan pernah melempar error — ularnya cuma berjalan mundur seumur hidup build itu.
            var head = Mesh($"{Folder}/SK_Snake_Head.fbx");
            var body = Mesh($"{Folder}/SK_Snake_Segment.fbx");
            var tail = Mesh($"{Folder}/SK_Snake_Tail.fbx");
            var skin = AssetDatabase.LoadAssetAtPath<Texture2D>(
                $"{Folder}/Textures/SnakeBoss_Albedo.png");

            if (head == null || body == null || tail == null)
            {
                Debug.LogError($"[BossModel] mesh tidak lengkap di {Folder}. " +
                               $"head={head != null} body={body != null} tail={tail != null}");
                return;
            }

            if (skin == null) Debug.LogWarning($"[BossModel] tekstur tidak ketemu — model tampil polos.");

            // Wrap Mode WAJIB Repeat. UV mesh-nya keluar dari rentang 0..1, dan importer yang
            // menyetelnya ke Clamp akan meregangkan satu baris piksel tepi di sepanjang badan —
            // yang terlihat bukan tekstur salah melainkan model yang seperti belum diberi material.
            FixWrap(skin);

            int wired = 0, thickened = 0;

            for (int i = 0; i < db.BossKinds.Count; i++)
            {
                var boss = db.BossKinds[i];
                if (boss == null) continue;

                // Berkas khusus per boss, kalau asetnya memintanya; yang kosong jatuh ke bawaan.
                //
                // Tanpa ini pass ini menimpa SEMUA boss dengan tiga mesh yang sama, dan boss
                // varian kehilangan modelnya pada klik menu berikutnya — bukan saat diedit,
                // melainkan berjam-jam kemudian saat seseorang menjalankan pass untuk alasan lain.
                boss.HeadMesh = PickMesh(boss.HeadMeshFile, head, boss.Id, "kepala");
                boss.BodyMesh = PickMesh(boss.BodyMeshFile, body, boss.Id, "badan");
                boss.TailMesh = PickMesh(boss.TailMeshFile, tail, boss.Id, "ekor");
                boss.BoneSkin = PickSkin(boss.BoneSkinFile, skin, boss.Id);

                // Pengali dihitung PER BOSS, bukan sekali untuk semua.
                //
                // Tiap boss punya HeadScale dan TailScale sendiri (kepala ular 2,6, kepala
                // kelabang 2,9, kepala grub 1,15), dan pengali itu DIKALIKAN dengannya di
                // renderer. Satu pengali untuk bertiga berarti kepala grub — anak buah yang
                // seharusnya kecil — tampil hampir sebesar kepala boss sungguhan.
                // Koreksi orientasi, DITENTUKAN DARI PENGAMATAN bukan dari tebakan.
                //
                // Ketiga mesh dibuat BERDIRI: di rotasi nol, kamera atas menampilkan tengkorak
                // dari samping — profil rahang — bukan dari punggungnya. Guling 90 derajat pada
                // sumbu Z merebahkannya.
                //
                // Kepala butuh 180 derajat TAMBAHAN, dan ekor tidak. Diperiksa berdampingan di
                // play mode: setelah direbahkan, moncong kepala menunjuk −Z sementara ujung ekor
                // juga menunjuk −Z. Ekor memang harus begitu (menjauhi kepala), kepala tidak.
                boss.HeadMeshRotation = new Vector3(0f, 180f, 90f);
                boss.BodyMeshRotation = new Vector3(0f, 0f, 90f);
                boss.TailMeshRotation = new Vector3(0f, 0f, 90f);

                // Diturunkan dari mesh yang BENAR-BENAR dipakai boss ini, bukan dari mesh bawaan.
                // Varian berduri panjangnya berbeda 1,3% dari ruas polos, dan mengukur yang salah
                // membuat ruasnya meleset dari jaraknya — persis celah yang pass ini ada untuk cegah.
                boss.HeadMeshScale = UnitScale(boss.HeadMesh);

                // Badan dan ekor berbagi SATU pengali — itu kontrak BossVisual, yang menyerahkan
                // `bodyScale` yang sama ke renderer badan dan renderer ekor. Skala instansnya
                // sendiri diinterpolasi dari HeadScale ke TailScale, jadi yang dipakai di sini
                // rata-rata keduanya.
                boss.BodyMeshScale = UnitScale(boss.BodyMesh);

                // Ruas paling belakang WAJIB lebih panjang dari jaraknya, atau badannya berlubang.
                //
                // Ini keluhan "kok tiap badan gak nyambung". Panjang satu ruas di dunia sama
                // dengan skalanya (pengali aset sudah menormalkan mesh ke satu unit), dan skala
                // meruncing dari kepala sampai `TailScale`. Ketiga boss memakai TailScale yang
                // LEBIH KECIL dari `Spacing` — serpent 0,85 melawan 1,05; kelabang 0,7 melawan
                // 0,85 — jadi separuh belakang tiap boss pasti merenggang, dan tidak ada
                // penyetelan mesh yang bisa menutupnya.
                //
                // Yang dipatok ambang minimumnya saja. Kalau seseorang menaikkan TailScale di atas
                // ini demi rasa, angkanya tidak diganggu — yang dilarang cuma jatuh di bawah
                // ambang yang menghasilkan lubang.
                float minTail = boss.Spacing * JoinOverlap;

                if (boss.TailScale < minTail)
                {
                    thickened++;
                    boss.TailScale = minTail;
                }

                EditorUtility.SetDirty(boss);
                wired++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Ukuran akhir tiap boss dilaporkan satu per satu — pengalinya sekarang per boss,
            // jadi satu baris ringkasan tidak lagi bisa mewakili ketiganya.
            var report = new System.Text.StringBuilder();
            report.Append($"[BossModel] {wired} boss memakai model SnakeBoss" +
                          (thickened > 0 ? $", {thickened} ekor ditebalkan supaya tidak berlubang" : "") +
                          ".\n");
            report.Append("Urutan slot: Head<-SK_Snake_Head, Body<-SK_Snake_Segment, Tail<-SK_Snake_Tail.\n");

            for (int i = 0; i < db.BossKinds.Count; i++)
            {
                var boss = db.BossKinds[i];
                if (boss == null || boss.HeadMesh == null) continue;

                float headLen = boss.HeadMesh.bounds.size.z * boss.HeadMeshScale * boss.HeadScale;
                float bodyLen = boss.BodyMesh.bounds.size.z * boss.BodyMeshScale *
                                (boss.HeadScale + boss.TailScale) * 0.5f;

                float tailLen = boss.BodyMesh.bounds.size.z * boss.BodyMeshScale * boss.TailScale;

                report.Append($"  {boss.Id}: kepala {headLen:0.00}u · ruas tengah {bodyLen:0.00}u · " +
                              $"ruas EKOR {tailLen:0.00}u di jarak {boss.Spacing:0.00} " +
                              $"(tumpang {tailLen / boss.Spacing:0.00}x — di bawah 1 = berlubang)\n");
            }

            Debug.Log(report.ToString());
        }

        /// <summary>
        /// Pengali yang membuat mesh ini sepanjang TEPAT SATU UNIT.
        ///
        /// Dengan begitu skala instans tiap ruas (<c>HeadScale</c>, <c>TailScale</c>) berhenti jadi
        /// angka tanpa satuan dan mulai berarti "panjang dalam unit dunia" — dan itu satu-satunya
        /// bentuk yang bisa disetel tangan tanpa membuka kalkulator. Diturunkan dari bounds, bukan
        /// diketik: aset yang diekspor ulang dengan skala lain membetulkan dirinya sendiri.
        /// </summary>
        static float UnitScale(Mesh mesh) => 1f / Mathf.Max(0.0001f, mesh.bounds.size.z);

        /// <summary>
        /// Mesh yang diminta aset boss ini, atau bawaannya.
        ///
        /// Nama yang diisi tapi tidak ketemu SENGAJA tidak menggagalkan pass — ia jatuh ke bawaan
        /// sambil berteriak. Salah ketik satu huruf tanpa peringatan akan terbaca sebagai
        /// "variannya tidak jadi", dan yang dicari orang berikutnya adalah bug di renderer.
        /// </summary>
        static Mesh PickMesh(string file, Mesh fallback, string bossId, string part)
        {
            if (string.IsNullOrWhiteSpace(file)) return fallback;

            var found = Mesh($"{Folder}/{file}.fbx");
            if (found != null) return found;

            Debug.LogWarning($"[BossModel] {bossId}: mesh {part} '{file}.fbx' tidak ada di {Folder} " +
                             $"— dipakai bawaan.");
            return fallback;
        }

        static Texture2D PickSkin(string file, Texture2D fallback, string bossId)
        {
            if (string.IsNullOrWhiteSpace(file)) return fallback;

            var found = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Folder}/Textures/{file}.png");

            if (found == null)
            {
                Debug.LogWarning($"[BossModel] {bossId}: tekstur '{file}.png' tidak ada — dipakai bawaan.");
                return fallback;
            }

            FixWrap(found);
            return found;
        }

        static Mesh Mesh(string path)
        {
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (o is Mesh m) return m;
            }

            return null;
        }

        static void FixWrap(Texture2D texture)
        {
            if (texture == null) return;

            string path = AssetDatabase.GetAssetPath(texture);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null || importer.wrapMode == TextureWrapMode.Repeat) return;

            importer.wrapMode = TextureWrapMode.Repeat;
            importer.SaveAndReimport();

            Debug.Log($"[BossModel] wrap mode '{texture.name}' dibetulkan jadi Repeat.");
        }
    }
}
