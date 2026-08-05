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

        [Header("Behaviour")]
        public CastKind Kind = CastKind.Projectile;

        public Element Element = Element.Arcane;

        [Header("Casting")]
        public float BaseDamage;
        public float BaseCooldown = 1f;
        public float Radius;
        public float Range;
        public int Hits = 1;
        public float ManaCost;

        [Header("Zone (khusus Kind = Zone)")]
        [Tooltip("Berapa detik kubangannya bertahan.")]
        public float ZoneDuration = 4f;

        [Tooltip("Jeda antar denyut damage di dalam kubangan.")]
        public float ZoneTickInterval = 0.5f;

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
