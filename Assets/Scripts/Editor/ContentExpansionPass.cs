using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Proto.EditorTools
{
    /// <summary>
    /// Grows the book from 32 pieces to 70, and the recipe web from 20 formulas to ~68.
    ///
    /// The shape of the content is a pyramid: 32 pieces at 1 star, 20 at 2, 10 at 3, 4 at 4, 4 at 5.
    /// That is not decoration — only 1-star pieces ever drop, and only 1 and 2 star pieces reach the
    /// shop, so a wide base is what makes the combination web wide. Every step up the pyramid has to
    /// be built, and the fewer results there are per tier, the more each one means.
    ///
    /// The 1-star skills fill a 4 elements x 6 archetypes grid. Doing it as a grid rather than
    /// piece-by-piece is what produces the formula count for free: any two 1-star pieces of an
    /// element are a plausible recipe, so each element carries its own ladder all the way up instead
    /// of Lightning dead-ending at 1 star the way it used to.
    ///
    /// Power per star roughly 2.4x, on top of a footprint that grows and a mana cost that grows.
    /// A star is meant to be felt, not read.
    ///
    /// Idempotent: matched by asset id, and it only ever adds or updates.
    /// </summary>
    public static class ContentExpansionPass
    {
        const string Root = "Assets/GameData";
        const string PieceFolder = Root + "/Pieces";
        const string RecipeFolder = Root + "/Recipes";

        [MenuItem("Tools/Grimoire/Expand Content to 70")]
        public static void Run()
        {
            var db = AssetDatabase.LoadAssetAtPath<ContentDatabase>(Root + "/ContentDatabase.asset");
            if (db == null)
            {
                Debug.LogError("[ContentExpansion] ContentDatabase.asset tidak ketemu.");
                return;
            }

            var pieces = new List<PieceDefinition>(db.Pieces);

            AddRunes(pieces);
            AddSigils(pieces);
            AddSkills(db, pieces);
            RetuneTopTier(db);
            GrantMoveSpeed(db);

            db.EditorSet(pieces, new List<RecipeDefinition>(db.Recipes));

            var recipes = new List<RecipeDefinition>(db.Recipes);
            int before = recipes.Count;
            AddRecipes(db, recipes);
            db.EditorSet(pieces, recipes);

            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[ContentExpansion] {pieces.Count} piece, {recipes.Count} resep " +
                      $"(+{recipes.Count - before}).\n{Census(db)}");
            Selection.activeObject = db;
        }

        // ---------- palette ----------

        /// <summary>
        /// One hue per element, brightening with rarity. A 5-star piece has to be recognisable as
        /// the heavy thing on a board covered in its own family.
        /// </summary>
        static Color Tint(Element element, int stars)
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

        // ---------- runes ----------

        static void AddRunes(List<PieceDefinition> pieces)
        {
            // 1 star: the two elements that had no base of their own to stand on.
            Add(pieces, Rune("frostrune", "Frost Rune", 1, ShapeKind.Square, Element.Ice,
                AuraKind.DamagePct, 0.8f, 0.4f,
                "2x2 base. Damage aura, and ice skills on top of it hit harder still."));

            Add(pieces, Rune("sparkrune", "Spark Rune", 1, ShapeKind.Line3, Element.Lightning,
                AuraKind.DamagePct, 0.6f, 0.36f,
                "3 long base. Damage aura, tuned for lightning."));

            // 2 star
            var siphon = Rune("runesiphon", "Siphon Rune", 2, ShapeKind.Square, Element.Arcane,
                AuraKind.CooldownPct, 0.4f, 0f,
                "2x2 base. Cuts cooldowns, feeds mana, and softens what casting costs.");
            Stats(siphon, Mod(StatKind.ManaRegen, 3f), Mod(StatKind.ManaCostPct, 0.12f));
            Add(pieces, siphon);

            var whetstone = Rune("runeasah", "Whetstone Rune", 2, ShapeKind.Line3, Element.Arcane,
                AuraKind.DamagePct, 0.5f, 0f,
                "3 long base. The only base that sharpens critical hits.");
            Stats(whetstone, Mod(StatKind.CritChance, 0.12f), Mod(StatKind.CritDamage, 0.3f));
            Add(pieces, whetstone);

            // 3 star
            var nadir = Rune("runenadir", "Nadir Rune", 3, ShapeKind.Big3, Element.Arcane,
                AuraKind.RadiusPct, 1.2f, 0f,
                "3x3 base. Enormous area aura, spread across nine cells.");
            Stats(nadir, Mod(StatKind.RangePct, 0.15f), Mod(StatKind.AreaPct, 0.1f));
            Add(pieces, nadir);
        }

        // ---------- sigils ----------

        static void AddSigils(List<PieceDefinition> pieces)
        {
            var keen = Sigil("segelmata", "Keen Sigil", 1, ShapeKind.Line2,
                "+8% critical chance. Passive - never fires.");
            Stats(keen, Mod(StatKind.CritChance, 0.08f));
            Add(pieces, keen);

            var chain = Sigil("segelrantai", "Chain Sigil", 1, ShapeKind.Corner,
                "+1 ailment point per application. Fills trigger thresholds faster.");
            Stats(chain, Mod(StatKind.AilmentPoints, 1f));
            Add(pieces, chain);

            var grand = Sigil("segelagung", "Grand Sigil", 2, ShapeKind.Tee,
                "EVOLVED. +55 max HP and +3 defence.");
            Stats(grand, Mod(StatKind.MaxHp, 55f), Mod(StatKind.Defense, 3f));
            Add(pieces, grand);

            var conduit = Sigil("segelarus", "Conduit Sigil", 2, ShapeKind.SBend,
                "EVOLVED. +5 mana per second and -15% mana cost.");
            Stats(conduit, Mod(StatKind.ManaRegen, 5f), Mod(StatKind.ManaCostPct, 0.15f));
            Add(pieces, conduit);

            var razor = Sigil("segeltajam", "Razor Sigil", 2, ShapeKind.Ell,
                "EVOLVED. +15% critical chance and +45% critical damage.");
            Stats(razor, Mod(StatKind.CritChance, 0.15f), Mod(StatKind.CritDamage, 0.45f));
            Add(pieces, razor);
        }

        // ---------- skills ----------

        struct Spec
        {
            public string Id, Name, Status, Blurb;
            public int Stars, Hits, Points;
            public Element Element;
            public CastKind Kind;
            public ShapeKind Shape;
            public float Damage, Cooldown, Radius, Range, Mana, Duration, ZoneLife, ZoneTick;
        }

        static void AddSkills(ContentDatabase db, List<PieceDefinition> pieces)
        {
            foreach (var spec in Specs)
            {
                var piece = Skill(spec, db);
                Add(pieces, piece);
            }
        }

        /// <summary>
        /// The 1-star band fills out the elements that only had one or two entries; everything above
        /// it is one evolution rung per element, so no element dead-ends before 5 stars.
        /// </summary>
        static readonly Spec[] Specs =
        {
            // ================= 1 star =================
            new Spec { Id = "emberburst", Name = "Ember Burst", Stars = 1, Element = Element.Fire,
                Kind = CastKind.Nova, Shape = ShapeKind.Corner, Damage = 12f, Cooldown = 2.6f,
                Radius = 4f, Range = 4f, Hits = 1, Mana = 12f, Status = "burn", Duration = 4f, Points = 1,
                Blurb = "Bursts into flame around you. Applies BURN." },

            new Spec { Id = "flamelash", Name = "Flame Lash", Stars = 1, Element = Element.Fire,
                Kind = CastKind.Line, Shape = ShapeKind.Line3, Damage = 13f, Cooldown = 2f,
                Radius = 1f, Range = 9f, Hits = 1, Mana = 11f, Status = "burn", Duration = 3f, Points = 1,
                Blurb = "Whips a line of fire outward. Applies BURN." },

            new Spec { Id = "cinderpatch", Name = "Cinder Patch", Stars = 1, Element = Element.Fire,
                Kind = CastKind.Zone, Shape = ShapeKind.Corner, Damage = 4f, Cooldown = 4.5f,
                Radius = 2.8f, Range = 9f, Hits = 1, Mana = 14f, Status = "burn", Duration = 4f, Points = 2,
                ZoneLife = 4f, ZoneTick = 0.5f,
                Blurb = "Leaves smouldering ground. Cheap, constant BURN." },

            new Spec { Id = "frostshard", Name = "Frost Shard", Stars = 1, Element = Element.Ice,
                Kind = CastKind.Projectile, Shape = ShapeKind.Line2, Damage = 13f, Cooldown = 1.2f,
                Radius = 0.7f, Range = 7f, Hits = 1, Mana = 6f, Status = "chill", Duration = 3f, Points = 1,
                Blurb = "A fast shard. The cheapest source of CHILL." },

            new Spec { Id = "icelance", Name = "Ice Lance", Stars = 1, Element = Element.Ice,
                Kind = CastKind.Line, Shape = ShapeKind.Line3, Damage = 15f, Cooldown = 2.2f,
                Radius = 1f, Range = 10f, Hits = 1, Mana = 12f, Status = "chill", Duration = 3f, Points = 1,
                Blurb = "Skewers everything in a line. Applies CHILL." },

            new Spec { Id = "hailstorm", Name = "Hailstorm", Stars = 1, Element = Element.Ice,
                Kind = CastKind.AreaAtTarget, Shape = ShapeKind.Corner, Damage = 11f, Cooldown = 2.6f,
                Radius = 3.6f, Range = 11f, Hits = 1, Mana = 13f, Status = "chill", Duration = 4f, Points = 1,
                Blurb = "Falls on the densest pack. Applies CHILL." },

            new Spec { Id = "sparkbolt", Name = "Spark Bolt", Stars = 1, Element = Element.Lightning,
                Kind = CastKind.Projectile, Shape = ShapeKind.Line2, Damage = 12f, Cooldown = 1f,
                Radius = 0.7f, Range = 8f, Hits = 1, Mana = 6f, Status = "shock", Duration = 3f, Points = 1,
                Blurb = "The fastest bolt in the book. Applies SHOCK." },

            new Spec { Id = "staticfield", Name = "Static Field", Stars = 1, Element = Element.Lightning,
                Kind = CastKind.Nova, Shape = ShapeKind.Corner, Damage = 10f, Cooldown = 2.4f,
                Radius = 4.2f, Range = 4.2f, Hits = 1, Mana = 12f, Status = "shock", Duration = 4f, Points = 2,
                Blurb = "Crackles around you. Heavy SHOCK, light damage." },

            new Spec { Id = "stormcell", Name = "Storm Cell", Stars = 1, Element = Element.Lightning,
                Kind = CastKind.Zone, Shape = ShapeKind.Corner, Damage = 4f, Cooldown = 5f,
                Radius = 3f, Range = 10f, Hits = 1, Mana = 15f, Status = "shock", Duration = 4f, Points = 2,
                ZoneLife = 5f, ZoneTick = 0.5f,
                Blurb = "A crackling patch of ground. Stacks SHOCK on everything inside." },

            new Spec { Id = "riftchain", Name = "Rift Chain", Stars = 1, Element = Element.Arcane,
                Kind = CastKind.Chain, Shape = ShapeKind.Line3, Damage = 9f, Cooldown = 2f,
                Radius = 8f, Range = 8f, Hits = 3, Mana = 12f, Status = "bleed", Duration = 4f, Points = 1,
                Blurb = "Tears from enemy to enemy. Applies BLEED." },

            // ================= 2 stars =================
            new Spec { Id = "infernowave", Name = "Inferno Wave", Stars = 2, Element = Element.Fire,
                Kind = CastKind.Line, Shape = ShapeKind.Tee, Damage = 36f, Cooldown = 2.1f,
                Radius = 1.6f, Range = 11f, Hits = 1, Mana = 20f, Status = "burn", Duration = 4f, Points = 2,
                Blurb = "EVOLVED. A wall of fire, wide enough to catch a crowd." },

            new Spec { Id = "ashfall", Name = "Ashfall", Stars = 2, Element = Element.Fire,
                Kind = CastKind.Zone, Shape = ShapeKind.SBend, Damage = 11f, Cooldown = 5.5f,
                Radius = 4f, Range = 11f, Hits = 1, Mana = 24f, Status = "burn", Duration = 5f, Points = 2,
                ZoneLife = 5f, ZoneTick = 0.5f,
                Blurb = "EVOLVED. Burning ash that settles and keeps burning." },

            new Spec { Id = "glacialspike", Name = "Glacial Spike", Stars = 2, Element = Element.Ice,
                Kind = CastKind.Projectile, Shape = ShapeKind.Ell, Damage = 38f, Cooldown = 1.1f,
                Radius = 0.9f, Range = 9f, Hits = 1, Mana = 18f, Status = "chill", Duration = 4f, Points = 2,
                Blurb = "EVOLVED. Single target, and it hits like nothing else its size." },

            new Spec { Id = "frostbitefield", Name = "Frostbite Field", Stars = 2, Element = Element.Ice,
                Kind = CastKind.Zone, Shape = ShapeKind.Square, Damage = 10f, Cooldown = 5.5f,
                Radius = 4.2f, Range = 10f, Hits = 1, Mana = 23f, Status = "chill", Duration = 5f, Points = 2,
                ZoneLife = 5f, ZoneTick = 0.5f,
                Blurb = "EVOLVED. Freezes the ground itself. Everything inside crawls." },

            new Spec { Id = "thunderclap", Name = "Thunderclap", Stars = 2, Element = Element.Lightning,
                Kind = CastKind.Nova, Shape = ShapeKind.Tee, Damage = 32f, Cooldown = 2.8f,
                Radius = 5.5f, Range = 5.5f, Hits = 1, Mana = 24f, Status = "shock", Duration = 4f, Points = 2,
                Blurb = "EVOLVED. A blast centred on you, wide enough to clear a ring." },

            new Spec { Id = "chainlightning", Name = "Chain Lightning", Stars = 2, Element = Element.Lightning,
                Kind = CastKind.Chain, Shape = ShapeKind.SBend, Damage = 26f, Cooldown = 2.2f,
                Radius = 9f, Range = 9f, Hits = 5, Mana = 22f, Status = "shock", Duration = 4f, Points = 2,
                Blurb = "EVOLVED. Leaps five times. Reaches far past its own range." },

            new Spec { Id = "voidlance", Name = "Void Lance", Stars = 2, Element = Element.Arcane,
                Kind = CastKind.Line, Shape = ShapeKind.Ell, Damage = 34f, Cooldown = 2f,
                Radius = 1.4f, Range = 11f, Hits = 1, Mana = 20f, Status = "bleed", Duration = 4f, Points = 2,
                Blurb = "EVOLVED. Opens a seam across the field. Applies BLEED." },

            new Spec { Id = "gravitywell", Name = "Gravity Well", Stars = 2, Element = Element.Arcane,
                Kind = CastKind.AreaAtTarget, Shape = ShapeKind.Square, Damage = 30f, Cooldown = 3f,
                Radius = 4.5f, Range = 11f, Hits = 1, Mana = 21f, Status = "seret", Duration = 2f, Points = 2,
                Blurb = "EVOLVED. Hauls the pack together. The setup every reaction wants." },

            // ================= 3 stars =================
            new Spec { Id = "firestormcore", Name = "Firestorm Core", Stars = 3, Element = Element.Fire,
                Kind = CastKind.AreaAtTarget, Shape = ShapeKind.Cross, Damage = 78f, Cooldown = 3f,
                Radius = 6f, Range = 12f, Hits = 1, Mana = 36f, Status = "burn", Duration = 5f, Points = 2,
                Blurb = "EVOLVED. Drops a burning core into the thick of them." },

            new Spec { Id = "rimenova", Name = "Rime Nova", Stars = 3, Element = Element.Ice,
                Kind = CastKind.Nova, Shape = ShapeKind.Cup, Damage = 72f, Cooldown = 3.2f,
                Radius = 7f, Range = 7f, Hits = 1, Mana = 34f, Status = "chill", Duration = 5f, Points = 3,
                Blurb = "EVOLVED. Everything within seven metres freezes solid." },

            new Spec { Id = "tempest", Name = "Tempest", Stars = 3, Element = Element.Lightning,
                Kind = CastKind.AreaAtTarget, Shape = ShapeKind.Cross, Damage = 74f, Cooldown = 3f,
                Radius = 6.2f, Range = 12f, Hits = 1, Mana = 35f, Status = "shock", Duration = 5f, Points = 3,
                Blurb = "EVOLVED. Calls the storm down onto the densest pack." },

            new Spec { Id = "ionstorm", Name = "Ion Storm", Stars = 3, Element = Element.Lightning,
                Kind = CastKind.Zone, Shape = ShapeKind.Wedge, Damage = 20f, Cooldown = 6f,
                Radius = 5f, Range = 11f, Hits = 1, Mana = 38f, Status = "shock", Duration = 5f, Points = 3,
                ZoneLife = 6f, ZoneTick = 0.45f,
                Blurb = "EVOLVED. Charged ground that keeps firing long after you cast it." },

            new Spec { Id = "nullsphere", Name = "Null Sphere", Stars = 3, Element = Element.Arcane,
                Kind = CastKind.Nova, Shape = ShapeKind.Cup, Damage = 80f, Cooldown = 3.4f,
                Radius = 6.5f, Range = 6.5f, Hits = 1, Mana = 36f, Status = "bleed", Duration = 5f, Points = 3,
                Blurb = "EVOLVED. Unmakes whatever stands too close. Applies BLEED." },

            new Spec { Id = "plaguebloom", Name = "Plague Bloom", Stars = 3, Element = Element.Arcane,
                Kind = CastKind.Zone, Shape = ShapeKind.Wedge, Damage = 18f, Cooldown = 6f,
                Radius = 4.8f, Range = 11f, Hits = 1, Mana = 36f, Status = "poison", Duration = 6f, Points = 3,
                ZoneLife = 6f, ZoneTick = 0.5f,
                Blurb = "EVOLVED. A spreading bloom of rot. The heaviest POISON there is." },

            // ================= 4 stars =================
            new Spec { Id = "ragnarok", Name = "Ragnarok", Stars = 4, Element = Element.Fire,
                Kind = CastKind.Line, Shape = ShapeKind.Hook, Damage = 165f, Cooldown = 3.6f,
                Radius = 2.4f, Range = 14f, Hits = 1, Mana = 56f, Status = "burn", Duration = 6f, Points = 3,
                Blurb = "EVOLVED. Splits the field end to end." },

            new Spec { Id = "wintersend", Name = "Winter's End", Stars = 4, Element = Element.Ice,
                Kind = CastKind.AreaAtTarget, Shape = ShapeKind.Slab, Damage = 155f, Cooldown = 3.4f,
                Radius = 8f, Range = 13f, Hits = 1, Mana = 54f, Status = "chill", Duration = 6f, Points = 3,
                Blurb = "EVOLVED. An eight-metre crater of ice, dropped where it hurts most." },

            new Spec { Id = "thundercrown", Name = "Thunder Crown", Stars = 4, Element = Element.Lightning,
                Kind = CastKind.Chain, Shape = ShapeKind.Hook, Damage = 120f, Cooldown = 3.2f,
                Radius = 10f, Range = 10f, Hits = 8, Mana = 58f, Status = "shock", Duration = 6f, Points = 3,
                Blurb = "EVOLVED. Eight leaps. Crosses the whole field if they are packed." },

            // ================= 5 stars =================
            new Spec { Id = "stormbreaker", Name = "Stormbreaker", Stars = 5, Element = Element.Lightning,
                Kind = CastKind.Chain, Shape = ShapeKind.Ring, Damage = 200f, Cooldown = 4f,
                Radius = 12f, Range = 12f, Hits = 12, Mana = 78f, Status = "shock", Duration = 6f, Points = 4,
                Blurb = "EVOLVED. Twelve leaps of raw storm. Nothing in a pack is safe from it." }
        };

        /// <summary>
        /// The three existing 5-star pieces were written before the tier above them existed. Against
        /// 4-star skills doing 120-165 they were barely a step up, which is the one thing the top of
        /// a pyramid cannot be.
        /// </summary>
        static void RetuneTopTier(ContentDatabase db)
        {
            var cataclysm = db.ById("cataclysm");
            if (cataclysm != null)
            {
                cataclysm.BaseDamage = 340f;
                cataclysm.ManaCost = 82f;
                EditorUtility.SetDirty(cataclysm);
            }

            var zero = db.ById("absolutezero");
            if (zero != null)
            {
                zero.BaseDamage = 300f;
                zero.BaseCooldown = 3.8f;
                zero.ManaCost = 78f;
                EditorUtility.SetDirty(zero);
            }

            var singularity = db.ById("singularity");
            if (singularity != null)
            {
                singularity.BaseDamage = 60f;
                singularity.ManaCost = 74f;
                EditorUtility.SetDirty(singularity);
            }
        }

        /// <summary>
        /// Movement is a build axis now, so it needs more than one source or it is not a choice.
        ///
        /// These three carry it rather than three new pieces: the count stays at 70, and all three
        /// are already about flow and timing, so the stat reads as belonging to them. Appended, not
        /// assigned — Current Rune and Flowing Ink already carry stats worth keeping.
        /// </summary>
        static void GrantMoveSpeed(ContentDatabase db)
        {
            Append(db.ById("chronorune"), StatKind.MoveSpeed, 0.4f);
            Append(db.ById("runearus"), StatKind.MoveSpeed, 0.5f);
            Append(db.ById("tintamengalir"), StatKind.MoveSpeed, 0.3f);
        }

        /// <summary>Adds a stat line, or updates it in place. Never duplicates it on a re-run.</summary>
        static void Append(PieceDefinition piece, StatKind kind, float value)
        {
            if (piece == null) return;

            var mods = new List<StatModifier>(piece.Stats ?? new StatModifier[0]);

            for (int i = 0; i < mods.Count; i++)
            {
                if (mods[i].Type != kind) continue;

                mods[i] = Mod(kind, value);
                piece.Stats = mods.ToArray();
                EditorUtility.SetDirty(piece);
                return;
            }

            mods.Add(Mod(kind, value));
            piece.Stats = mods.ToArray();
            EditorUtility.SetDirty(piece);
        }

        // ---------- recipes ----------

        static void AddRecipes(ContentDatabase db, List<RecipeDefinition> recipes)
        {
            // --- 2 star skills: two routes each, so no single drop gates a whole element ---
            R(recipes, db, "infernowave_a", "infernowave", "flamelash", "fireball");
            R(recipes, db, "infernowave_b", "infernowave", "flamelash", "emberburst");
            R(recipes, db, "ashfall_a", "ashfall", "cinderpatch", "hujanapi");
            R(recipes, db, "ashfall_b", "ashfall", "cinderpatch", "cinderpatch");
            R(recipes, db, "glacialspike_a", "glacialspike", "frostshard", "icelance");
            R(recipes, db, "glacialspike_b", "glacialspike", "frostshard", "frostshard");
            R(recipes, db, "frostbitefield_a", "frostbitefield", "hailstorm", "frostnova");
            R(recipes, db, "frostbitefield_b", "frostbitefield", "hailstorm", "icelance");
            R(recipes, db, "thunderclap_a", "thunderclap", "staticfield", "sparkbolt");
            R(recipes, db, "thunderclap_b", "thunderclap", "staticfield", "staticfield");
            R(recipes, db, "chainlightning_a", "chainlightning", "arcbolt", "sabetanpetir");
            R(recipes, db, "chainlightning_b", "chainlightning", "arcbolt", "sparkbolt");
            R(recipes, db, "voidlance_a", "voidlance", "riftchain", "belatiberputar");
            R(recipes, db, "voidlance_b", "voidlance", "riftchain", "riftchain");
            R(recipes, db, "gravitywell_a", "gravitywell", "pusaran", "belatiberputar");
            R(recipes, db, "gravitywell_b", "gravitywell", "pusaran", "kubanganracun");

            // --- 3 star skills ---
            R(recipes, db, "firestormcore_a", "firestormcore", "infernowave", "ashfall");
            R(recipes, db, "firestormcore_b", "firestormcore", "greaterfireball", "ashfall");
            R(recipes, db, "rimenova_a", "rimenova", "frostbitefield", "blizzard");
            R(recipes, db, "rimenova_b", "rimenova", "glacialspike", "badaisalju");
            R(recipes, db, "tempest_a", "tempest", "thunderclap", "chainlightning");
            R(recipes, db, "tempest_b", "tempest", "thunderclap", "thunderclap");
            R(recipes, db, "ionstorm_a", "ionstorm", "stormcell", "thunderclap");
            R(recipes, db, "ionstorm_b", "ionstorm", "stormcell", "chainlightning");
            R(recipes, db, "nullsphere_a", "nullsphere", "voidlance", "gravitywell");
            R(recipes, db, "nullsphere_b", "nullsphere", "voidlance", "steamburst");
            R(recipes, db, "plaguebloom_a", "plaguebloom", "kubanganracun", "gravitywell");
            R(recipes, db, "plaguebloom_b", "plaguebloom", "kubanganracun", "voidlance");

            // --- 4 star skills ---
            R(recipes, db, "ragnarok_a", "ragnarok", "firestormcore", "meteor");
            R(recipes, db, "ragnarok_b", "ragnarok", "firestormcore", "firestormcore");
            R(recipes, db, "wintersend_a", "wintersend", "rimenova", "prismabeku");
            R(recipes, db, "wintersend_b", "wintersend", "rimenova", "rimenova");
            R(recipes, db, "thundercrown_a", "thundercrown", "tempest", "ionstorm");
            R(recipes, db, "thundercrown_b", "thundercrown", "tempest", "tempest");

            // --- 5 star skills ---
            R(recipes, db, "cataclysm_b", "cataclysm", "ragnarok", "firestormcore");
            R(recipes, db, "absolutezero_b", "absolutezero", "wintersend", "rimenova");
            R(recipes, db, "singularity_b", "singularity", "nullsphere", "plaguebloom");
            R(recipes, db, "stormbreaker_a", "stormbreaker", "thundercrown", "tempest");
            R(recipes, db, "stormbreaker_b", "stormbreaker", "thundercrown", "ionstorm");

            // --- sigils fused into bases ---
            //
            // Runes can never be ingredients (the model only accepts skill-layer pieces), which left
            // the 3-star bases reachable by nothing at all: they are above the drop cap and above the
            // shop's high roll. Sigils are skill-layer, so fusing them is the one legal route in —
            // and it finally gives sigils a job beyond filling holes.
            R(recipes, db, "runearus_a", "runearus", "segelbara", "segelnadi");
            R(recipes, db, "runebenteng_a", "runebenteng", "segelvitalitas", "tintamengalir");
            R(recipes, db, "runesiphon_a", "runesiphon", "segelnadi", "segelsumur");
            R(recipes, db, "runeasah_a", "runeasah", "segelmata", "segelrantai");
            R(recipes, db, "runebadai_a", "runebadai", "segelbara", "segelbara", "segelrantai");
            R(recipes, db, "runenadir_a", "runenadir", "segeltajam", "segelagung");

            // --- 2 star sigils ---
            R(recipes, db, "segelagung_a", "segelagung", "segelvitalitas", "segelvitalitas");
            R(recipes, db, "segelarus_a", "segelarus", "segelbara", "segelsumur");
            R(recipes, db, "segeltajam_a", "segeltajam", "segelmata", "segelmata");
        }

        // ---------- asset plumbing ----------

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

        static PieceDefinition Rune(string id, string name, int stars, ShapeKind shape, Element element,
            AuraKind aura, float auraValue, float matchBonus, string blurb)
        {
            var asset = Load(id);

            asset.Id = id;
            asset.DisplayName = name;
            asset.Stars = stars;
            asset.Layer = Layer.Rune;
            asset.Kind = CastKind.AuraOnly;
            asset.Element = element;
            asset.Shape = shape;
            asset.Color = Tint(element, stars);
            asset.Aura = aura;
            asset.AuraValue = auraValue;
            asset.ElementMatchBonus = matchBonus;
            asset.Stats = new StatModifier[0];
            asset.Blurb = blurb;

            EditorUtility.SetDirty(asset);
            return asset;
        }

        static PieceDefinition Sigil(string id, string name, int stars, ShapeKind shape, string blurb)
        {
            var asset = Load(id);

            asset.Id = id;
            asset.DisplayName = name;
            asset.Stars = stars;
            asset.Layer = Layer.Skill;

            // Sigils MUST be Passive. Compile filters casters by Kind, not by damage — anything else
            // here and the sigil joins the firing line on a 0.05s cooldown.
            asset.Kind = CastKind.Passive;

            asset.Element = Element.Arcane;
            asset.Shape = shape;
            asset.Color = new Color(0.82f, 0.78f, 0.62f);
            asset.Trigger = CastTrigger.Cooldown;
            asset.BaseDamage = 0f;
            asset.ManaCost = 0f;
            asset.Blurb = blurb;

            EditorUtility.SetDirty(asset);
            return asset;
        }

        static PieceDefinition Skill(Spec spec, ContentDatabase db)
        {
            var asset = Load(spec.Id);

            asset.Id = spec.Id;
            asset.DisplayName = spec.Name;
            asset.Stars = spec.Stars;
            asset.Layer = Layer.Skill;
            asset.Kind = spec.Kind;
            asset.Element = spec.Element;
            asset.Shape = spec.Shape;
            asset.Color = Tint(spec.Element, spec.Stars);
            asset.Trigger = CastTrigger.Cooldown;
            asset.BaseDamage = spec.Damage;
            asset.BaseCooldown = spec.Cooldown;
            asset.Radius = spec.Radius;
            asset.Range = spec.Range;
            asset.Hits = spec.Hits;
            asset.ManaCost = spec.Mana;
            asset.AppliedStatus = db.StatusById(spec.Status);
            asset.StatusDuration = spec.Duration;
            asset.AppliedPoints = spec.Points;
            asset.Blurb = spec.Blurb;

            if (spec.ZoneLife > 0f)
            {
                asset.ZoneDuration = spec.ZoneLife;
                asset.ZoneTickInterval = spec.ZoneTick;
            }

            EditorUtility.SetDirty(asset);
            return asset;
        }

        static void R(List<RecipeDefinition> recipes, ContentDatabase db, string fileId,
            string resultId, params string[] ingredientIds)
        {
            var result = db.ById(resultId);
            if (result == null)
            {
                Debug.LogWarning($"[ContentExpansion] hasil '{resultId}' tidak ada, resep dilewati.");
                return;
            }

            string path = $"{RecipeFolder}/Recipe_{fileId}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<RecipeDefinition>(path);

            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<RecipeDefinition>();
                AssetDatabase.CreateAsset(asset, path);
            }

            var ingredients = new PieceDefinition[ingredientIds.Length];
            for (int i = 0; i < ingredientIds.Length; i++)
            {
                ingredients[i] = db.ById(ingredientIds[i]);
                if (ingredients[i] == null)
                {
                    Debug.LogWarning($"[ContentExpansion] bahan '{ingredientIds[i]}' " +
                                     $"untuk '{fileId}' tidak ada.");
                }
            }

            asset.Ingredients = ingredients;
            asset.Result = result;
            EditorUtility.SetDirty(asset);

            if (!recipes.Contains(asset)) recipes.Add(asset);
        }

        static string Census(ContentDatabase db)
        {
            var runes = new int[6];
            var sigils = new int[6];
            var skills = new int[6];

            for (int i = 0; i < db.Pieces.Count; i++)
            {
                var p = db.Pieces[i];
                if (p == null) continue;

                int s = Mathf.Clamp(p.Stars, 1, 5);
                if (p.IsRune) runes[s]++;
                else if (p.IsPassive) sigils[s]++;
                else skills[s]++;
            }

            var sb = new System.Text.StringBuilder("bintang | rune | segel | skill | total\n");
            for (int s = 1; s <= 5; s++)
            {
                sb.Append($"   {s}    |  {runes[s],2}  |  {sigils[s],2}   |  {skills[s],2}   " +
                          $"|  {runes[s] + sigils[s] + skills[s],2}\n");
            }

            return sb.ToString();
        }
    }
}
