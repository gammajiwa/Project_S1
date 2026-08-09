using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Proto.EditorTools
{
    /// <summary>
    /// Sustain yang bisa DITEMUKAN, panen di skill besar, dan langit-langit rune yang baru.
    ///
    /// Tiga masalah, satu pass:
    ///
    /// <b>Pertama, sustain praktis tidak pernah jatuh.</b> 82% drop itu bintang satu, dan di
    /// bintang satu cuma ada SATU segel per sumbu sustain — jadi "build bertahan" bukan pilihan
    /// yang ditawarkan game, melainkan undian yang hampir selalu kalah. Jawabannya BUKAN angka
    /// lebih besar, melainkan lebih banyak ENTRI ★1: peluang ketemu naik, kekuatannya tidak.
    /// Semua segel baru sengaja lebih pelit PER PETAK daripada yang lama — sustain boleh sering
    /// ketemu, tapi harus membayar ruang papan. Regen 6.5/dtk melawan buku enam skill yang
    /// menyedot berkali-kali itu tidak boleh pernah impas dari segel saja.
    ///
    /// <b>Kedua, panen: skill ★3+ tertentu memulihkan saat membunuh.</b> Angkanya per kill dan
    /// KECIL dengan sengaja — pengalinya laju kill (belasan per detik di wave besar), bukan satu.
    /// 0.3 mana/kill di 10 kill/detik = 3 mana/detik, kira-kira ongkos satu skill besar: panen
    /// membayar SATU skill selagi pembantaian bagus, bukan seluruh buku selamanya.
    ///
    /// <b>Ketiga, rune di atas bintang 3.</b> Dot ★5 menaruh nilai satu rune penuh di SATU petak;
    /// ia raksasa justru karena mahal — bobot drop ★5 nol (aturan GameBalance), jadi satu-satunya
    /// jalan adalah resep tiga segel ★3, yang masing-masing piramida ★1 sendiri. ★4 boleh jatuh
    /// (wave 11+, bobot 1.5) dan tetap punya resep sebagai jalur pasti. Rune "panjang" 5 sel
    /// memakai Cross — garis lurus 5 petak MUSTAHIL di batas keras 3x3 (siluet codex, tas,
    /// pool sel; lihat Shapes.cs), dan enum TIDAK ditambah karena semua bentuk yang perlu
    /// sudah ada.
    ///
    /// Idempotent: match by id lewat path aset, dijalankan dua kali hasilnya sama.
    /// </summary>
    public static class SustainPass
    {
        const string Root = "Assets/GameData";
        const string PieceFolder = Root + "/Pieces";
        const string RecipeFolder = Root + "/Recipes";

        [MenuItem("Tools/Grimoire/Generate Sustain & Runes")]
        public static void Run()
        {
            var db = AssetDatabase.LoadAssetAtPath<ContentDatabase>(Root + "/ContentDatabase.asset");
            if (db == null)
            {
                Debug.LogError("[SustainPass] ContentDatabase.asset tidak ketemu.");
                return;
            }

            var pieces = new List<PieceDefinition>(db.Pieces);
            int before = pieces.Count;

            AddSustainSigils(pieces);
            AddRunes(pieces);
            GrantKillRestore(db);

            // Didaftarkan dulu baru resep: R() mencari bahan lewat db.ById, dan piece baru
            // belum kelihatan sebelum EditorSet me-reset indeksnya.
            db.EditorSet(pieces, new List<RecipeDefinition>(db.Recipes));

            var recipes = new List<RecipeDefinition>(db.Recipes);
            int recipesBefore = recipes.Count;
            AddRecipes(db, recipes);
            db.EditorSet(pieces, recipes);

            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[SustainPass] +{pieces.Count - before} piece (total {pieces.Count}), " +
                      $"+{recipes.Count - recipesBefore} resep (total {recipes.Count}). " +
                      "Ikon placeholder: jalankan Tools/Grimoire/Generate Placeholder Icons.");
            Selection.activeObject = db;
        }

        // ---------- segel sustain ----------

        /// <summary>
        /// Empat entri ★1 baru (satu per sumbu) supaya kolam drop 82% benar-benar bisa
        /// mengeluarkan sustain, lalu satu ★2 per bar dan satu ★3 dua-bar di puncak.
        ///
        /// Semuanya Corner/Ell/Tee/SBend — bentuk yang menyebalkan dipasang. Segel lama yang
        /// setara memakai Line2 rapi; yang baru membayar kemudahan ditemukan dengan ruang.
        /// </summary>
        static void AddSustainSigils(List<PieceDefinition> pieces)
        {
            // ---- ★1: pelit per petak, tapi ADA di kolam drop ----

            // vs segelbara (Line2, +3): total sama, satu petak lebih boros.
            Add(pieces, Sigil("segelgerimis", "Drizzle Sigil", 1, ShapeKind.Corner,
                new Color(0.5f, 0.68f, 0.95f),
                "Siku 3 petak. +3 mana per detik. Gerimis kecil, tapi turun terus.",
                Mod(StatKind.ManaRegen, 3f)));

            // vs segelsumur (Line2, +2.5): total sama, footprint lebih besar.
            Add(pieces, Sigil("segelakar", "Root Sigil", 1, ShapeKind.Corner,
                new Color(0.45f, 0.72f, 0.4f),
                "Siku 3 petak. +2.5 HP per detik. Akar menyerap pelan, tidak pernah berhenti.",
                Mod(StatKind.HpRegen, 2.5f)));

            // vs segelnadi (Line2, +25): per petak 9.3 lawan 12.5 — lebih murah dihargai,
            // lebih mahal ditata.
            Add(pieces, Sigil("segelkristal", "Geode Sigil", 1, ShapeKind.Corner,
                new Color(0.55f, 0.5f, 0.95f),
                "Siku 3 petak. +28 mana maksimum. Rongga kristal: kolamnya melebar, bukan derasnya.",
                Mod(StatKind.MaxMana, 28f)));

            // vs segelvitalitas (Line2, +30): pola pelit yang sama.
            Add(pieces, Sigil("segelkarang", "Coral Sigil", 1, ShapeKind.Corner,
                new Color(0.9f, 0.55f, 0.5f),
                "Siku 3 petak. +35 HP maksimum. Karang tumbuh lambat dan tidak mundur.",
                Mod(StatKind.MaxHp, 35f)));

            // ---- ★2: satu per bar, sidegrade dari yang sudah ada ----

            // vs segelarus (+5 regen, -15% harga): regen lebih tipis, dapat sedikit kolam.
            Add(pieces, Sigil("segelembun", "Dewfall Sigil", 2, ShapeKind.Ell,
                new Color(0.42f, 0.62f, 1f),
                "EVOLVED. +4.5 mana per detik dan +15 mana maksimum.",
                Mod(StatKind.ManaRegen, 4.5f), Mod(StatKind.MaxMana, 15f)));

            Add(pieces, Sigil("segelrimba", "Heartwood Sigil", 2, ShapeKind.Tee,
                new Color(0.38f, 0.6f, 0.32f),
                "EVOLVED. +3.5 HP per detik dan +20 HP maksimum.",
                Mod(StatKind.HpRegen, 3.5f), Mod(StatKind.MaxHp, 20f)));

            // ---- ★3: dua bar sekaligus, masing-masing separuh hati ----
            //
            // Tiap sumbunya LEBIH LEMAH dari segel ★2 yang berdedikasi — itu harganya menutup
            // dua bar dengan empat petak. Bandingkan segelsamudra: lebih deras di mana, tapi
            // buta soal HP.
            Add(pieces, Sigil("segelfajar", "Dawn Sigil", 3, ShapeKind.SBend,
                new Color(1f, 0.8f, 0.45f),
                "EVOLVED. +3.5 mana per detik dan +2.5 HP per detik. Fajar mengisi keduanya.",
                Mod(StatKind.ManaRegen, 3.5f), Mod(StatKind.HpRegen, 2.5f)));
        }

        // ---------- panen di skill besar ----------

        /// <summary>
        /// Skill ★3+ yang SUDAH ADA mendapat efek panen — tidak ada skill baru di sini.
        ///
        /// Pemilihannya tematik (arcane melahap, es/racun menambal, petir menyedot badai) dan
        /// satu bar per skill kecuali puncak menara. Angka dibaca sebagai agregat: di wave 20
        /// laju kill tembus ~12/detik, jadi 0.3/kill = 3.6 mana/detik — separuh regen dasar,
        /// atau kira-kira upkeep SATU skill besar. Menumpuk ketiga pembawa mana (1.5/kill)
        /// legal, tapi menelan ~20 petak papan untuk skill-skill itu sendiri.
        /// </summary>
        static void GrantKillRestore(ContentDatabase db)
        {
            // Jalur mana: membunuh mengisi buku.
            KillRestore(db, "nullsphere", 0.3f, 0f);      // ★3 nova arcane — melahap yang dekat
            KillRestore(db, "thundercrown", 0.5f, 0f);    // ★4 chain 8 — tiap sambaran menyetor
            KillRestore(db, "stormbreaker", 0.7f, 0f);    // ★5 chain 12 — badai memberi makan badai

            // Jalur HP: membunuh menambal.
            KillRestore(db, "plaguebloom", 0f, 0.2f);     // ★3 zone racun — busuk jadi pupuk
            KillRestore(db, "wintersend", 0f, 0.35f);     // ★4 kawah es
            KillRestore(db, "absolutezero", 0f, 0.5f);    // ★5 — dingin yang mengawetkan

            // Puncak: satu-satunya dual, dan tiap separuhnya LEBIH KECIL dari pembawa
            // berdedikasi setingkatnya — dua bar itu kemewahannya, bukan angkanya.
            KillRestore(db, "cataclysm", 0.4f, 0.25f);    // ★5
        }

        /// <summary>Marker blurb panen. Satu konstanta supaya penulisan ulang selalu menemukannya.</summary>
        const string KillMark = "\nPANEN:";

        /// <summary>
        /// Menulis nilai panen dan satu baris blurb. Baris lama dipotong dulu di marker,
        /// jadi menjalankan pass dua kali tidak menumpuk kalimat.
        /// </summary>
        static void KillRestore(ContentDatabase db, string id, float mana, float hp)
        {
            var piece = db.ById(id);
            if (piece == null)
            {
                Debug.LogWarning($"[SustainPass] skill '{id}' tidak ada, panen dilewati.");
                return;
            }

            piece.RestoreManaOnKill = mana;
            piece.RestoreHpOnKill = hp;

            string blurb = piece.Blurb ?? "";
            int cut = blurb.IndexOf(KillMark, System.StringComparison.Ordinal);
            if (cut >= 0) blurb = blurb.Substring(0, cut);

            string effect;
            if (mana > 0f && hp > 0f) effect = $"tiap kill +{mana} mana & +{hp} HP.";
            else if (mana > 0f) effect = $"tiap kill +{mana} mana.";
            else effect = $"tiap kill +{hp} HP.";

            piece.Blurb = blurb.TrimEnd() + KillMark + " " + effect;
            EditorUtility.SetDirty(piece);
        }

        // ---------- rune ★4 & ★5 ----------

        /// <summary>
        /// Acuan efisiensi aura per petak dari konten lama: ★1 ~0.2, ★2 ~0.25, ★3 ~0.15 (tapi
        /// totalnya besar). ★4 di sini duduk di ~0.22-0.32 per petak dengan footprint raksasa —
        /// efeknya membayar ruang yang dimakan. Dot ★5 membalik logikanya: total setara rune
        /// ★3 penuh, dipadatkan ke SATU petak.
        /// </summary>
        static void AddRunes(List<PieceDefinition> pieces)
        {
            // ★4, Slab 3x2 (6 petak). Alas damage murni untuk build api: 0.3/petak + bonus
            // elemen. Skill api Slab (Winter's End itu es — Ragnarok Hook yang cocok separuh)
            // yang menutup keenam petaknya menyedot totalnya bulat-bulat.
            Add(pieces, Rune("runemercu", "Beacon Rune", 4, ShapeKind.Slab, Element.Fire,
                AuraKind.DamagePct, 1.8f, 0.5f,
                "Lempeng 3x2. Menara api: total +180% damage dibagi ke enam petak, " +
                "api di atasnya dapat lebih lagi."));

            // ★4, Ess (6 petak). Cooldown murni — dan SENGAJA tanpa bantuan mana sedikit pun:
            // cast lebih rapat berarti tagihan mana lebih rapat. Rune ini mempercepat bukunya
            // sekaligus membuatnya lebih lapar; yang memasang harus membawa sustain sendiri.
            Add(pieces, Rune("runegema", "Echo Rune", 4, ShapeKind.Ess, Element.Arcane,
                AuraKind.CooldownPct, 1.4f, 0f,
                "Huruf S, 6 petak. Gema: total -140% cooldown dibagi ke enam petak. " +
                "Menembak lebih sering juga berarti membayar lebih sering."));

            // ★4, Cross (5 petak) — rune "panjang" yang diminta. Garis lurus 5 sel tidak muat
            // batas keras 3x3, jadi salib inilah bentuk 5-sel yang sah. Efek tambahannya:
            // di samping aura, ia satu-satunya ALAS yang mengisi kedua bar — sustain yang
            // menempati lapisan rune membayar ruangnya dua kali, karena petaknya sekaligus
            // tempat skill berdiri.
            var sungai = Rune("runesungai", "River Rune", 4, ShapeKind.Cross, Element.Arcane,
                AuraKind.DamagePct, 1.1f, 0f,
                "Salib 5 petak. Sungai: total +110% damage dibagi lima petak, dan alirannya " +
                "mengisi mana maupun HP.");
            Stats(sungai, Mod(StatKind.ManaRegen, 3f), Mod(StatKind.HpRegen, 2f));
            Add(pieces, sungai);

            // ★5, SATU petak. Nilai penuh sebuah rune — setara total runebadai yang 3x3 —
            // dipadatkan ke satu sel: skill apa pun yang menginjaknya mendapat SEMUANYA,
            // dan delapan petak di sekelilingnya bebas untuk hal lain. Boleh sebesar ini
            // justru karena jalan masuknya cuma resep tiga segel ★3 (bobot drop ★5 nol,
            // dan itu TIDAK diubah di sini) — puncak menara dibangun, bukan dipungut.
            Add(pieces, Rune("runeinti", "Keystone Rune", 5, ShapeKind.Dot, Element.Arcane,
                AuraKind.DamagePct, 1.5f, 0f,
                "SATU petak. +150% damage untuk skill yang menginjaknya. Seluruh menara " +
                "dipadatkan jadi satu batu kunci."));
        }

        // ---------- resep ----------

        /// <summary>
        /// Semua di atas ★1 wajib punya jalan dibangun. Segel ★1 sustain jadi bahan — itu
        /// membuat drop sustain yang "kebanyakan" tetap berharga, dan memberi segel lama
        /// pekerjaan baru. Rune memakai bahan lapisan skill (aturan model: rune tidak pernah
        /// jadi bahan), meniru pola peleburan segel milik ContentExpansionPass.
        /// </summary>
        static void AddRecipes(ContentDatabase db, List<RecipeDefinition> recipes)
        {
            // --- segel ★2: dua rute, supaya tidak digerbangi satu jenis drop ---
            R(recipes, db, "segelembun_a", "segelembun", "segelgerimis", "segelnadi");
            R(recipes, db, "segelembun_b", "segelembun", "segelgerimis", "segelgerimis");
            R(recipes, db, "segelrimba_a", "segelrimba", "segelakar", "segelvitalitas");
            R(recipes, db, "segelrimba_b", "segelrimba", "segelakar", "segelakar");

            // --- segel ★3: dari pasangan ★2 — termasuk pasangan LAMA (arus + pemurni),
            // supaya inventori sustain yang sudah dimiliki pemain lama ikut naik kelas ---
            R(recipes, db, "segelfajar_a", "segelfajar", "segelembun", "segelrimba");
            R(recipes, db, "segelfajar_b", "segelfajar", "segelarus", "segelpemurni");

            // --- rune ★4: ★3 + ★2 dari sumbu yang sama tema ---
            R(recipes, db, "runemercu_a", "runemercu", "segelmurka", "segelamarah");
            R(recipes, db, "runegema_a", "runegema", "quickcastsigil", "segelsamudra");
            R(recipes, db, "runesungai_a", "runesungai", "segelfajar", "segelsamudra");

            // --- rune ★5: tiga segel ★3 — sumbu serang, nyawa, dan mana, dilebur jadi satu
            // titik. Tiap bahannya sendiri piramida (murka = amarah+tajam, benteng =
            // agung+perisai, samudra = telaga+arus), jadi harga sesungguhnya belasan drop
            // ★1 plus papan selebar 15 petak untuk memicu peleburannya. Semahal itulah
            // satu petak bernilai +150%. ---
            R(recipes, db, "runeinti_a", "runeinti", "segelmurka", "segelbenteng", "segelsamudra");
        }

        // ---------- perkakas aset (pola ContentExpansionPass, match by id) ----------

        static StatModifier Mod(StatKind kind, float value) =>
            new StatModifier { Type = kind, Value = value };

        static void Stats(PieceDefinition piece, params StatModifier[] mods)
        {
            piece.Stats = mods;
            EditorUtility.SetDirty(piece);
        }

        static void Add(List<PieceDefinition> pieces, PieceDefinition piece)
        {
            if (piece != null && !pieces.Contains(piece)) pieces.Add(piece);
        }

        static PieceDefinition Load(string id)
        {
            string path = $"{PieceFolder}/Piece_{id}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<PieceDefinition>(path);

            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<PieceDefinition>();
                AssetDatabase.CreateAsset(asset, path);
            }

            return asset;
        }

        static PieceDefinition Sigil(string id, string name, int stars, ShapeKind shape,
            Color color, string blurb, params StatModifier[] stats)
        {
            var asset = Load(id);

            asset.Id = id;
            asset.DisplayName = name;
            asset.Stars = stars;
            asset.Layer = Layer.Skill;

            // Segel WAJIB Passive — kind lain ikut antre menembak dan membakar mana
            // tiap cooldown tanpa damage. Invarian yang sama dengan SigilPass.
            asset.Kind = CastKind.Passive;

            asset.Element = Element.Arcane;
            asset.Shape = shape;
            asset.Color = color;
            asset.Trigger = CastTrigger.Cooldown;
            asset.BaseDamage = 0f;
            asset.ManaCost = 0f;
            asset.BaseCooldown = 1f;
            asset.Stats = stats;
            asset.Blurb = blurb;

            EditorUtility.SetDirty(asset);
            return asset;
        }

        static PieceDefinition Rune(string id, string name, int stars, ShapeKind shape,
            Element element, AuraKind aura, float auraValue, float matchBonus, string blurb)
        {
            var asset = Load(id);

            asset.Id = id;
            asset.DisplayName = name;
            asset.Stars = stars;
            asset.Layer = Layer.Rune;
            asset.Kind = CastKind.AuraOnly;
            asset.Element = element;
            asset.Shape = shape;
            asset.Color = RuneTint(element, stars);
            asset.Aura = aura;
            asset.AuraValue = auraValue;
            asset.ElementMatchBonus = matchBonus;
            asset.Stats = new StatModifier[0];
            asset.Blurb = blurb;

            EditorUtility.SetDirty(asset);
            return asset;
        }

        /// <summary>Palet ContentExpansionPass: satu hue per elemen, memutih seiring bintang.</summary>
        static Color RuneTint(Element element, int stars)
        {
            Color baseColor;
            switch (element)
            {
                case Element.Fire: baseColor = new Color(0.95f, 0.4f, 0.14f); break;
                case Element.Ice: baseColor = new Color(0.45f, 0.78f, 1f); break;
                case Element.Lightning: baseColor = new Color(0.92f, 0.86f, 0.3f); break;
                default: baseColor = new Color(0.66f, 0.42f, 0.95f); break;
            }

            return Color.Lerp(baseColor, Color.white, (stars - 1) * 0.13f);
        }

        static void R(List<RecipeDefinition> recipes, ContentDatabase db, string fileId,
            string resultId, params string[] ingredientIds)
        {
            var result = db.ById(resultId);
            if (result == null)
            {
                Debug.LogWarning($"[SustainPass] hasil '{resultId}' tidak ada, resep dilewati.");
                return;
            }

            var ingredients = new PieceDefinition[ingredientIds.Length];
            for (int i = 0; i < ingredientIds.Length; i++)
            {
                ingredients[i] = db.ById(ingredientIds[i]);
                if (ingredients[i] != null) continue;

                // Resep berbahan bolong akan diam-diam cocok dengan apa saja — lebih baik
                // tidak dibuat sama sekali.
                Debug.LogWarning($"[SustainPass] bahan '{ingredientIds[i]}' untuk " +
                                 $"'{fileId}' tidak ada, resep dilewati.");
                return;
            }

            string path = $"{RecipeFolder}/Recipe_{fileId}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<RecipeDefinition>(path);

            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<RecipeDefinition>();
                AssetDatabase.CreateAsset(asset, path);
            }

            asset.Ingredients = ingredients;
            asset.Result = result;
            EditorUtility.SetDirty(asset);

            if (!recipes.Contains(asset)) recipes.Add(asset);
        }
    }
}
