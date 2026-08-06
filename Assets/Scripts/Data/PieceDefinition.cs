using UnityEngine;

namespace Proto
{
    /// <summary>
    /// One placeable thing: a base rune, a casting skill, or a passive sigil.
    /// All three share the same grid rules, so they share one asset type — splitting them
    /// would only duplicate the placement code three times.
    /// </summary>
    [CreateAssetMenu(fileName = "Piece_", menuName = "Grimoire/Piece")]
    public class PieceDefinition : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Never change after release — recipes, saves and the codex key off this.")]
        public string Id;

        public string DisplayName;

        [Range(1, 5)]
        public int Stars = 1;

        [Header("Grid")]
        public Layer Layer = Layer.Skill;

        public ShapeKind Shape = ShapeKind.Line2;

        public Color Color = Color.white;

        [Tooltip("Placeholder digenerate lewat Tools/Grimoire/Generate Placeholder Icons. " +
                 "Buat ganti art: timpa file PNG-nya di Assets/GameData/Icons — generator tidak " +
                 "pernah menimpa file yang sudah ada. Kosong = jatuh balik ke kotak warna.")]
        public Sprite Icon;

        [Header("Behaviour")]
        public CastKind Kind = CastKind.Projectile;

        public Element Element = Element.Arcane;

        [Header("Casting")]
        public float BaseDamage;
        public float BaseCooldown = 1f;
        public float Radius;
        public float Range;
        [Tooltip("Chain: berapa lompatan per cabang. Radial: berapa arah semburan.")]
        public int Hits = 1;

        [Min(1)]
        [Tooltip("Khusus Chain: berapa cabang petir yang berangkat SEKALIGUS. Tiap cabang " +
                 "melompat sendiri-sendiri dan tidak pernah menyambar musuh yang sama.")]
        public int Forks = 1;

        [Tooltip("Khusus Projectile/Radial: berapa kali peluru ini MEMANTUL ke musuh berikutnya " +
                 "setelah kena. 0 = mati saat menyentuh.")]
        public int Bounces;

        [Tooltip("Jarak cari sasaran pantulan berikutnya.")]
        public float BounceRange = 6f;

        public float ManaCost;

        [Header("Zone (khusus Kind = Zone)")]
        [Tooltip("Berapa detik kubangannya bertahan.")]
        public float ZoneDuration = 4f;

        [Tooltip("Jeda antar denyut damage di dalam kubangan.")]
        public float ZoneTickInterval = 0.5f;

        [Tooltip("Unit per detik kubangan ini MENGEMBARA. 0 = diam di tempat. Kubangan yang " +
                 "bergerak memaksa pemain membaca lapangan tiap saat, bukan sekali di awal.")]
        public float ZoneDrift;

        [Header("Pemicu")]
        [Tooltip("Cooldown = nembak sendiri. StatusThreshold = nunggu poin ailment di musuh cukup.")]
        public CastTrigger Trigger = CastTrigger.Cooldown;

        [Tooltip("Ailment yang ditunggu. Hanya dipakai kalau Trigger = StatusThreshold.")]
        public StatusDefinition TriggerStatus;

        [Min(1)]
        [Tooltip("Berapa poin di satu musuh sebelum skill ini meletus di musuh itu.")]
        public int TriggerPoints = 10;

        [Tooltip("Poin pemicunya dihabiskan setelah meletus.")]
        public bool ConsumeTriggerPoints = true;

        [Min(1)]
        [Tooltip("Khusus Detonate: berapa musuh maksimum yang diledakkan sekali cast. Tanpa batas " +
                 "ini sebuah peledak menskala dengan JUMLAH MUSUH tanpa plafon, dan skill bintang 2 " +
                 "melewati bintang 5 begitu gerombolannya besar.")]
        public int MaxDetonations = 8;

        [Header("Status yang ditempel")]
        public StatusDefinition AppliedStatus;

        public float StatusDuration;

        [Min(1)]
        [UnityEngine.Serialization.FormerlySerializedAs("AppliedStacks")]
        [Tooltip("Berapa POIN yang ditempel tiap kali kena. Segel bisa menambah ini.")]
        public int AppliedPoints = 1;

        [HideInInspector]
        [UnityEngine.Serialization.FormerlySerializedAs("Status")]
        [Tooltip("Peninggalan prototipe. Dipakai sekali untuk migrasi ke AppliedStatus.")]
        public StatusType LegacyStatus = StatusType.None;

        [Header("Utility (Orbit / Blink / Ward / Surge / SunStrike / RollingBall / Vortex / Push)")]
        [Tooltip("Buff yang ditempel ke PEMAIN saat skill ini menyala. Ini yang membedakan " +
                 "Haste dari Fortify dari Amuk — bukan kodenya, cuma aset buff-nya.")]
        public BuffDefinition GrantOnCast;

        [Tooltip("Detik antara telegraf muncul dan pukulannya mendarat. Khusus SunStrike. " +
                 "Nol berarti tidak ada aba-aba sama sekali, dan skill itu kehilangan seluruh " +
                 "ciri khasnya.")]
        public float TelegraphDelay = 0.9f;

        [Tooltip("Kekuatan lontaran ForcePush, atau kekuatan seretan Vortex. Unit per detik.")]
        public float PushForce = 12f;

        [Tooltip("Berapa lama musuh TERANGKAT dan tidak berdaya. Khusus Vortex.")]
        public float LiftDuration = 1.6f;

        [Tooltip("Kecepatan jelajah: bola RollingBall, atau pecahan Orbit saat meluncur.")]
        public float TravelSpeed = 9f;

        [Header("Charge")]
        [Tooltip("Buff yang didapat pemain TIAP MUSUH MATI selama piece ini terpasang. Isi dengan " +
                 "buff ber-MaxStacks di atas 1 dan kamu punya charge ala PoE: kumpulkan sambil " +
                 "membunuh, lalu belanjakan.")]
        public BuffDefinition GrantOnKill;

        [Tooltip("Skill ini MENGHABISKAN semua tumpukan buff ini saat cast, dan memukul lebih keras " +
                 "per tumpukan. Kosong = tidak memakai charge.")]
        public BuffDefinition ConsumesCharge;

        [Tooltip("Tambahan damage per tumpukan yang dihabiskan. 0.35 = +35% per charge.")]
        public float DamagePerCharge = 0.35f;

        [Header("Aura (khusus rune)")]
        public AuraKind Aura = AuraKind.None;

        [Tooltip("Nilai TOTAL rune ini, dibagi rata ke semua petaknya. " +
                 "Rune 3 petak dengan 0.3 memberi 0.1 per petak — skill yang cuma menginjak " +
                 "satu petak dapat sepertiganya.")]
        public float AuraValue;

        [Tooltip("Bonus damage TOTAL kalau elemen skill di atasnya sama dengan elemen rune ini. " +
                 "Juga dibagi rata per petak.")]
        public float ElementMatchBonus;

        [Header("Stat yang diberikan")]
        [Tooltip("Boleh diisi rune, segel, maupun skill. Persen ditulis desimal: 0.15 = 15%.")]
        public StatModifier[] Stats;

        [HideInInspector] public StatKind Stat = StatKind.None;
        [HideInInspector] public float StatValue;

        [Header("Teks")]
        [TextArea(2, 4)]
        public string Blurb;

        public Vector2Int[] Cells => Shapes.Of(Shape);

        public bool IsRune => Layer == Layer.Rune;

        public bool IsPassive => Kind == CastKind.Passive;

        public bool CanDrop => Stars <= 1;

        void OnValidate()
        {
            if (string.IsNullOrEmpty(Id)) Id = name.ToLowerInvariant().Replace("piece_", "");
            if (string.IsNullOrEmpty(DisplayName)) DisplayName = name;
            if (BaseCooldown < 0.05f) BaseCooldown = 0.05f;
        }
    }
}
