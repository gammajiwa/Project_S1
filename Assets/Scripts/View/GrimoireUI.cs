using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using static Proto.GrimoireLayout;

namespace Proto
{
    /// <summary>
    /// All prototype UI: the two-layer grimoire (grid snapping, lines hidden), the skill backpack,
    /// the sell box, per-spell cooldown dials, the buff panel, wave banner, speed control and
    /// floating combat text. Built entirely from code â€” prototype, no prefabs.
    /// </summary>
    public class GrimoireUI : MonoBehaviour
    {
        // Pixel measurements and screen rects live in GrimoireLayout, pulled in by `using static`
        // above. What stays here is pool sizing — how many widgets to allocate, not where they go.
        const int MaxSpellRows = 8;
        const int FloatPoolSize = 16;
        const int LoosePoolSize = 24;
        const int MaxCellsPerPiece = 9;

        static readonly float[] Speeds = { 1f, 2f, 3f, 5f };
        static readonly string[] SpeedLabels = { "1x", "2x", "3x", "5x" };

        // Grid lines stay hidden until you are holding something.
        static readonly Color HiddenCell = new Color(0.5f, 0.5f, 0.6f, 0.05f);
        static readonly Color ShownCell = new Color(0.5f, 0.5f, 0.6f, 0.16f);
        static readonly Color HiddenBagCell = new Color(0.7f, 0.6f, 0.45f, 0.08f);
        static readonly Color ShownBagCell = new Color(0.7f, 0.6f, 0.45f, 0.2f);
        static readonly Color ValidCell = new Color(0.25f, 0.8f, 0.35f, 0.9f);
        static readonly Color InvalidCell = new Color(0.85f, 0.2f, 0.2f, 0.9f);

        public PlayerCaster Player;
        public EnemyManager Enemies;

        /// <summary>Boleh null: arena tanpa biome tetap jalan, cuma tanpa tombol siang/malam.</summary>
        BiomeDresser _biome;

        ContentDatabase _db;
        GameBalance _balance;
        TooltipBuilder _tooltips;

        Grimoire Book => Player.Book;
        readonly Backpack _bag = new Backpack();

        Canvas _canvas;
        Font _font;
        Camera _camera;

        Image[] _baseCells;
        Image[] _skillCells;
        Image[] _bagCells;

        Sprite _circle;
        Image[] _cdBg;
        Image[] _cdFill;
        float[] _pulse;

        Image _sellBg;
        Text _sellLabel;

        // --- shop / recipes ---
        const int ShopSlots = 6;

        // One group needs a connector per pair of ingredients, not a single box, so the pool has to
        // cover (members - 1) segments for every group on the board at once.
        // Bigger now that the pool also carries the "what can this combine with" cables, which fan
        // out from the cursor to every partner on the board at once.
        const int EvoLinePool = 40;

        const float EvoLineThin = 5f;
        const float EvoLineThick = 8f;


        // Blue while the recipe is short a part, gold once it is complete. These are lines now, not
        // a wash over an area, so they can be opaque — a line has to be followed, not just noticed.
        static readonly Color LinkIncomplete = new Color(0.4f, 0.68f, 1f, 0.85f);
        static readonly Color LinkComplete = new Color(1f, 0.84f, 0.32f, 0.95f);

        // Browsing the codex belongs to the main menu now. A run only ever writes to it.
        DiscoveryLog _codex;

        bool _shopOpen;
        int _rerollCost;
        readonly PieceDefinition[] _shop = new PieceDefinition[ShopSlots];

        // ---------- peta run & pulau rehat ----------
        RunDirector _run;
        bool _mapOpen;

        Image _mapBg;
        Text _mapTitle;
        Text _mapLegend;
        Image _mapBtnBg;
        Text _mapBtnLabel;
        Image[] _mapEdges = System.Array.Empty<Image>();
        Image[] _mapNodes = System.Array.Empty<Image>();
        Image[] _mapRings = System.Array.Empty<Image>();
        Text[] _mapGlyphs = System.Array.Empty<Text>();
        Text _mapYou;
        int _mapSig = -1;

        // Segmen pendek per sambungan — jalur bezier putus-putus ala peta referensi.
        const int MapSegsPerEdge = 7;

        bool _gambleOpen;
        float _spinLeft;
        int _slotOutcome = -1;
        string _slotResultLine = "";
        Image _slotBg;
        Text _slotTitle;
        readonly Text[] _slotReels = new Text[3];
        Image _slotSpinBg;
        Text _slotSpinLabel;
        Text _slotInfo;

        bool _eventOpen;
        bool _eventDone;
        Image _eventBg;
        Text _eventTitle;
        Text _eventBody;
        Image _eventABg;
        Image _eventBBg;
        Text _eventALabel;
        Text _eventBLabel;

        Image[] _evoLines;
        List<EvoPreview> _previews = new List<EvoPreview>();
        List<EvoPreview> _bagPreviews = new List<EvoPreview>();
        float _previewTimer;

        /// <summary>Scratch buffer for ordering one group's ingredients. A recipe takes at most 3.</summary>
        readonly Vector2[] _evoWalk = new Vector2[4];

        List<Vector2> _partners = new List<Vector2>();
        PieceDefinition _partnerFor;
        float _partnerTimer;

        // Last previewed drag position, so the lines can be recomputed the moment the cursor moves
        // instead of waiting out the idle throttle.
        PieceDefinition _ghostDef;
        Vector2Int _ghostOrigin;
        int _ghostRot;

        Image _panelBg;
        Text _panelTitle;
        Image[] _shopSlotBg;
        Text[] _shopSlotText;
        Image _rerollBg;
        Text _rerollLabel;
        Image _shopBtnBg;
        Text _shopBtnLabel;
        Text _recipeBtnLabel;

        /// <summary>
        /// Every drop lands scattered across the screen. Whatever is still lying around when a
        /// wave starts gets sold.
        /// </summary>
        readonly List<PieceDefinition> _loose = new List<PieceDefinition>();
        readonly List<Vector2> _loosePos = new List<Vector2>();

        /// <summary>Flat pool of cells used to draw loose pieces and the piece on the cursor.</summary>
        Image[] _looseCells;

        PieceDefinition _held;
        int _heldRot;

        /// <summary>
        /// Skills lifted along with the rune currently in hand.
        ///
        /// Dropped the moment the rune is rotated: their offsets are expressed in the rune's
        /// un-rotated frame, and quietly re-deriving them through a rotation is the kind of thing
        /// that looks right until a 3-cell base is turned twice.
        /// </summary>
        List<Grimoire.Rider> _riders = new List<Grimoire.Rider>();

        int _gold;

        // Swallows any stray click/keypress carried in from entering play mode.
        float _inputLock = 0.4f;

        Image[] _spellBg;
        Image[] _spellFill;
        Text[] _spellText;

        /// <summary>Row order for the panel: indices into Book.Spells, sorted by damage.</summary>
        readonly int[] _spellOrder = new int[MaxSpellRows];

        Image[] _speedButtons;
        Text[] _speedLabels;
        int _speedSlot;

        // --- damage meter ---
        readonly DamageMeter _meter = new DamageMeter();
        Text _meterText;
        float _meterTimer;

        StatusStrip _buffStrip;
        StatusStrip _debuffStrip;
        StatusStrip _ailmentStrip;

        Text _hudText;
        Image _hpBg;
        Image _hpChip;
        Image _hpFill;
        Text _hpLabel;
        Image _manaBg;
        Image _manaFill;
        Text _manaLabel;

        // Animated bar state. The fill chases the real value; the chip trails behind it so you can
        // see how much was just taken off, which a bar that snaps can never show.
        float _hpShown = 1f;
        float _hpChipShown = 1f;
        float _manaShown = 1f;
        float _hurtFlash;
        static readonly Color HpFillColor = new Color(0.85f, 0.28f, 0.3f, 0.95f);
        static readonly Color HpChipColor = new Color(1f, 0.78f, 0.6f, 0.75f);
        static readonly Color ManaFillColor = new Color(0.35f, 0.6f, 1f, 0.95f);
        Image _tipBg;
        Text _tipText;
        Text _heldText;
        Text _bannerText;
        Text _gridTitle;
        Text _evolveText;
        float _evolveTimer;

        Image _startBg;
        Text _startLabel;

        Text[] _floaters;
        float[] _floatLife;
        Vector3[] _floatWorld;

        DamagePopups _popups;
        RecipePanel _recipes;

        readonly StringBuilder _sb = new StringBuilder(256);

        public void Init(PlayerCaster player, EnemyManager enemies, Camera cam,
            ContentDatabase database, GameBalance balance, BiomeDresser biome = null)
        {
            Player = player;
            Enemies = enemies;
            _biome = biome;
            _camera = cam;
            _db = database;
            _balance = balance;
            _tooltips = new TooltipBuilder(balance);
            _rerollCost = balance.RerollCostStart;

            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (_font == null) _font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            BuildCanvas();

            // Hidup di dunia, bukan di kanvas, jadi ia berdiri sendiri di luar hierarki UI.
            var pickupGo = new GameObject("DropPickups");
            _pickups = pickupGo.AddComponent<DropPickups>();
            _pickups.Init(player.transform, piece => AddLoose(piece));

            // Built first on purpose: on this canvas creation order is draw order, so the damage
            // numbers pass UNDER the grimoire and the panels instead of covering them.
            _popups = new DamagePopups(_canvas.transform, _font, cam);

            BuildGrid();
            BuildSkillWidgets();
            BuildBackpack();
            BuildSellBox();
            BuildLoose();
            BuildShop();
            _codex = DiscoveryLog.Load();
            BuildSpellPanel();
            BuildSpeedControl();
            BuildHud();
            BuildBossBar();
            BuildMeter();
            BuildFloaters();

            // Built last: on this canvas creation order is draw order, and the recipe card has to
            // sit on top of everything it is explaining.
            _recipes = new RecipePanel(_canvas.transform, _font, _db, OwnedCount);

            Enemies.OnWaveCleared += OnWaveCleared;
            Enemies.OnKill += OnEnemyKilled;
            Enemies.OnDamage += _meter.Record;
            Enemies.OnEnemyDamaged += _popups.Push;
            Player.OnCast += OnSpellCast;
            Enemies.OnReaction += (pos, rx) => PushFloater(pos, rx.DisplayName + "!", rx.FlashColor);

            var cheats = Player.Cheats;
            ApplyLoadout(cheats != null && cheats.LoadoutOverride != null
                ? cheats.LoadoutOverride
                : _db.DefaultHero);

            for (int i = 0; i < Book.Placed.Count; i++) Discover(Book.Placed[i].Def);

            SetSpeed(0);
            Redraw();

            ApplyCheats(cheats);
        }

        /// <summary>
        /// Saklar curang yang menyentuh UI dan alur wave. Sengaja dipanggil paling akhir: dia
        /// memulai wave, dan itu tidak boleh terjadi sebelum papan tersusun.
        /// </summary>
        void ApplyCheats(DebugConfig cheats)
        {
            if (cheats == null || !cheats.Enabled) return;

            if (cheats.CheatHideUI) _canvas.enabled = false;

            if (cheats.OpeningTimeScale != 1f) Time.timeScale = cheats.OpeningTimeScale;

            // Melompat ke wave mana pun. Papan sudah terisi di atas, jadi buku sihirnya sudah
            // punya isi saat gerombolan pertama tiba.
            if (cheats.OpeningWave > 0) Enemies.StartWave(cheats.OpeningWave);
        }

        /// <summary>
        /// Seats a hero's opening board. Runes go down first — a skill cannot stand on nothing.
        ///
        /// Positions come from the asset rather than from auto-placement, because where the two
        /// opening skills sit IS the first decision of a run: left apart they keep firing as two,
        /// pushed together they merge into one 2-star at the end of the wave. Auto-placement would
        /// have dropped them side by side and made that choice for the player.
        /// </summary>
        void ApplyLoadout(HeroLoadout hero)
        {
            if (hero == null)
            {
                Debug.LogError("[GrimoireUI] Tidak ada HeroLoadout di ContentDatabase — " +
                               "jalankan Tools/Grimoire/Generate Heroes.", this);
                return;
            }

            for (int pass = 0; pass < 2; pass++)
            {
                bool wantRunes = pass == 0;

                for (int i = 0; i < hero.Placed.Length; i++)
                {
                    var seat = hero.Placed[i];
                    if (seat.Piece == null || seat.Piece.IsRune != wantRunes) continue;

                    if (Book.Place(seat.Piece, seat.Origin, seat.Rot) == null)
                    {
                        Debug.LogWarning($"[GrimoireUI] '{hero.Id}': {seat.Piece.Id} tidak muat di " +
                                         $"{seat.Origin}, dilempar ke lantai.", this);
                        AddLoose(seat.Piece);
                    }
                }
            }

            for (int i = 0; i < hero.Loose.Length; i++) AddLoose(hero.Loose[i]);
        }

        void OnDestroy()
        {
            Time.timeScale = 1f;
        }

        const string GameSceneName = "Proto";
        const string MainMenuSceneName = "MainMenu";

        /// <summary>By name, not by build index: reloading by index breaks silently the moment the
        /// build list is reordered, and it was already broken while Proto sat outside that list.</summary>
        static void LoadScene(string sceneName)
        {
            Time.timeScale = 1f;
            UnityEngine.SceneManagement.SceneManager.LoadScene(sceneName);
        }

        // ---------- construction ----------

        void BuildCanvas()
        {
            var go = new GameObject("Canvas");
            go.transform.SetParent(transform, false);

            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;
        }

        Image MakeImage(string name, Vector2 pos, Vector2 size, Color color, Vector2 anchor)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_canvas.transform, false);

            var img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;

            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            return img;
        }

        Text MakeText(string name, Vector2 pos, Vector2 size, int fontSize, Color color,
            Vector2 anchor, TextAnchor align)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_canvas.transform, false);

            var text = go.AddComponent<Text>();
            text.font = _font;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = align;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            var rt = text.rectTransform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            return text;
        }

        void BuildGrid()
        {
            int count = Grimoire.Width * Grimoire.Height;
            _baseCells = new Image[count];
            _skillCells = new Image[count];

            for (int y = 0; y < Grimoire.Height; y++)
            {
                for (int x = 0; x < Grimoire.Width; x++)
                {
                    int i = y * Grimoire.Width + x;
                    var pos = CellAnchor(x, y);

                    _baseCells[i] = MakeImage($"Base_{x}_{y}", pos,
                        new Vector2(CellSize, CellSize), HiddenCell, Vector2.zero);

                    _skillCells[i] = MakeImage($"Skill_{x}_{y}",
                        pos + new Vector2(SkillInset, SkillInset),
                        new Vector2(CellSize - SkillInset * 2, CellSize - SkillInset * 2),
                        Color.white, Vector2.zero);
                    _skillCells[i].enabled = false;
                }
            }

            _gridTitle = MakeText("GridTitle", new Vector2(Margin, GridTop() + 8), new Vector2(400, 24), 17,
                new Color(0.85f, 0.82f, 0.95f), Vector2.zero, TextAnchor.LowerLeft);
            _gridTitle.text = "GRIMOIRE";

            // Below the three icon strips, which now own the band straight under the mana bar.
            _heldText = MakeText("HeldInfo", new Vector2(Margin, StripAilmentY - 34f),
                new Vector2(880, 22), 13, new Color(0.85f, 0.85f, 0.6f), new Vector2(0f, 1f),
                TextAnchor.UpperLeft);

            _evolveText = MakeText("EvolveInfo", new Vector2(Margin, StripAilmentY - 56f),
                new Vector2(880, 22), 14, new Color(0.55f, 1f, 0.7f), new Vector2(0f, 1f),
                TextAnchor.UpperLeft);
            _evolveText.text = "";
        }

        /// <summary>One radial cooldown dial per active skill, drawn on top of its cells.</summary>
        void BuildSkillWidgets()
        {
            _circle = MakeCircleSprite(64);
            _cdBg = new Image[MaxSpellRows];
            _cdFill = new Image[MaxSpellRows];
            _pulse = new float[MaxSpellRows];

            var size = new Vector2(CooldownDiameter, CooldownDiameter);

            for (int i = 0; i < MaxSpellRows; i++)
            {
                _cdBg[i] = MakeImage($"CdBg_{i}", Vector2.zero, size,
                    new Color(0.04f, 0.04f, 0.07f, 0.8f), Vector2.zero);
                _cdBg[i].sprite = _circle;
                _cdBg[i].rectTransform.pivot = new Vector2(0.5f, 0.5f);
                _cdBg[i].enabled = false;

                _cdFill[i] = MakeImage($"CdFill_{i}", Vector2.zero, size, Color.white, Vector2.zero);
                _cdFill[i].sprite = _circle;
                _cdFill[i].rectTransform.pivot = new Vector2(0.5f, 0.5f);
                _cdFill[i].type = Image.Type.Filled;
                _cdFill[i].fillMethod = Image.FillMethod.Radial360;
                _cdFill[i].fillOrigin = (int)Image.Origin360.Top;
                _cdFill[i].fillClockwise = true;
                _cdFill[i].enabled = false;
            }
        }

        static Sprite MakeCircleSprite(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp
            };

            float r = size * 0.5f;
            var pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - r;
                    float dy = y + 0.5f - r;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    byte a = (byte)(Mathf.Clamp01(r - d) * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, a);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        void BuildBackpack()
        {
            _bagCells = new Image[Backpack.Width * Backpack.Height];

            for (int y = 0; y < Backpack.Height; y++)
            {
                for (int x = 0; x < Backpack.Width; x++)
                {
                    int i = y * Backpack.Width + x;
                    _bagCells[i] = MakeImage($"Bag_{x}_{y}", BagAnchor(x, y),
                        new Vector2(BagCell, BagCell), HiddenBagCell, Vector2.zero);
                }
            }

            MakeText("BagTitle", new Vector2(RightX(), BagY + Backpack.Height * (BagCell + BagGap) + 2),
                new Vector2(300, 20), 13, new Color(0.85f, 0.8f, 0.6f),
                Vector2.zero, TextAnchor.LowerLeft).text = "TAS  (skill doang - drop skill masuk sini)";
        }

        void BuildSellBox()
        {
            _sellBg = MakeImage("SellBg", new Vector2(RightX(), SellY), new Vector2(SellW, SellH),
                new Color(0.35f, 0.15f, 0.15f, 0.9f), Vector2.zero);

            _sellLabel = MakeText("SellLabel", new Vector2(RightX(), SellY + 11), new Vector2(SellW, 20), 14,
                new Color(0.95f, 0.7f, 0.7f), Vector2.zero, TextAnchor.LowerCenter);
            _sellLabel.text = "JUAL";
        }

        void BuildLoose()
        {
            // +1 piece worth of cells so the held piece can ride the cursor.
            // loose pieces + the carried piece + one preview per shop slot
            _looseCells = new Image[(LoosePoolSize + 1 + ShopSlots) * MaxCellsPerPiece];

            for (int i = 0; i < _looseCells.Length; i++)
            {
                _looseCells[i] = MakeImage($"LooseCell_{i}", Vector2.zero,
                    new Vector2(LooseCellSize, LooseCellSize), Color.white, Vector2.zero);
                _looseCells[i].rectTransform.pivot = new Vector2(0.5f, 0.5f);
                _looseCells[i].enabled = false;
            }
        }

        /// <summary>Draws one piece centred on <paramref name="center"/>. Returns cells consumed.</summary>
        int DrawPiece(PieceDefinition def, int rot, Vector2 center, int cursor, float alpha)
        {
            var shape = Shapes.Rotate(def.Cells, rot);
            var size = PieceSize(shape);
            float step = LooseCellSize + LooseCellGap;
            Vector2 origin = center - size * 0.5f;

            bool isSkill = def.Layer == Layer.Skill;
            float inner = isSkill ? LooseCellSize - SkillInset * 2f : LooseCellSize;
            var color = new Color(def.Color.r, def.Color.g, def.Color.b, alpha);

            for (int i = 0; i < shape.Length && cursor < _looseCells.Length; i++, cursor++)
            {
                var img = _looseCells[cursor];
                img.enabled = true;
                img.color = color;
                img.rectTransform.sizeDelta = new Vector2(inner, inner);
                img.rectTransform.anchoredPosition = origin + new Vector2(
                    shape[i].x * step + LooseCellSize * 0.5f,
                    shape[i].y * step + LooseCellSize * 0.5f);
            }

            return cursor;
        }

        void BuildShop()
        {
            _shopBtnBg = MakeImage("ShopBtn", new Vector2(RightX(), SellY + SellH + 8),
                new Vector2(88, 32), new Color(0.2f, 0.3f, 0.45f, 0.92f), Vector2.zero);
            _shopBtnLabel = MakeText("ShopBtnLabel", new Vector2(RightX(), SellY + SellH + 16),
                new Vector2(88, 20), 14, Color.white, Vector2.zero, TextAnchor.LowerCenter);
            _shopBtnLabel.text = "TOKO";

            _recipeBtnLabel = MakeText("RecipeHint", new Vector2(RightX() + 94, SellY + SellH + 16),
                new Vector2(220, 20), 12, new Color(0.7f, 0.68f, 0.78f), Vector2.zero, TextAnchor.LowerLeft);
            _recipeBtnLabel.text = "ALT + hover = lihat resep";

            _panelBg = MakeImage("PanelBg", Vector2.zero, new Vector2(PanelW, PanelH),
                new Color(0.07f, 0.07f, 0.11f, 0.98f), new Vector2(0.5f, 0.5f));
            _panelBg.enabled = false;

            _panelTitle = MakeText("PanelTitle", Vector2.zero, new Vector2(PanelW - 24, 26), 17,
                new Color(0.9f, 0.88f, 0.98f), new Vector2(0.5f, 0.5f), TextAnchor.UpperLeft);
            _panelTitle.enabled = false;

            _shopSlotBg = new Image[ShopSlots];
            _shopSlotText = new Text[ShopSlots];

            for (int i = 0; i < ShopSlots; i++)
            {
                _shopSlotBg[i] = MakeImage($"ShopSlot_{i}", Vector2.zero, new Vector2(ShopSlotW, ShopSlotH),
                    new Color(0.13f, 0.13f, 0.18f, 0.95f), Vector2.zero);
                _shopSlotBg[i].enabled = false;

                _shopSlotText[i] = MakeText($"ShopSlotText_{i}", Vector2.zero, new Vector2(ShopSlotW - 10, 40), 13,
                    Color.white, Vector2.zero, TextAnchor.LowerCenter);
                _shopSlotText[i].enabled = false;
            }

            _rerollBg = MakeImage("RerollBg", Vector2.zero, new Vector2(240, 34),
                new Color(0.32f, 0.45f, 0.28f, 0.95f), Vector2.zero);
            _rerollBg.enabled = false;

            _rerollLabel = MakeText("RerollLabel", Vector2.zero, new Vector2(240, 22), 15,
                Color.white, Vector2.zero, TextAnchor.LowerCenter);
            _rerollLabel.enabled = false;

            _evoLines = new Image[EvoLinePool];
            for (int i = 0; i < EvoLinePool; i++)
            {
                _evoLines[i] = MakeImage($"EvoLine_{i}", Vector2.zero, new Vector2(4, 4), Color.white, Vector2.zero);
                _evoLines[i].rectTransform.pivot = new Vector2(0.5f, 0.5f);
                _evoLines[i].enabled = false;
            }

            RollShop();
        }

        /// <summary>Marks a piece as seen. Silent unless it is genuinely new.</summary>
        void Discover(PieceDefinition piece)
        {
            if (piece == null || _codex == null) return;
            if (!_codex.Discover(piece.Id)) return;

            PushFloater(Player.transform.position + Vector3.up * 2.6f,
                "BARU: " + piece.DisplayName, new Color(0.8f, 0.95f, 1f));
        }

        void RollShop()
        {
            for (int i = 0; i < ShopSlots; i++) _shop[i] = _db.ShopRoll(_balance.ShopHighRollChance);
        }

        void BuildSpellPanel()
        {
            _spellBg = new Image[MaxSpellRows];
            _spellFill = new Image[MaxSpellRows];
            _spellText = new Text[MaxSpellRows];

            for (int i = 0; i < MaxSpellRows; i++)
            {
                // Row 0 is the TOP row. The list is sorted by damage, so the heaviest skill has to
                // sit where the eye lands first — the old bottom-up order buried it.
                float y = Margin + (MaxSpellRows - 1 - i) * 44;
                _spellBg[i] = MakeImage($"SpellBg_{i}", new Vector2(-Margin, y), new Vector2(SpellPanelW, 40),
                    new Color(0.1f, 0.1f, 0.14f, 0.85f), new Vector2(1f, 0f));

                _spellFill[i] = MakeImage($"SpellFill_{i}", new Vector2(-Margin, y), new Vector2(SpellPanelW, 40),
                    new Color(0.3f, 0.3f, 0.45f, 0.55f), new Vector2(1f, 0f));
                _spellFill[i].type = Image.Type.Filled;
                _spellFill[i].fillMethod = Image.FillMethod.Horizontal;
                _spellFill[i].fillOrigin = 0;

                _spellText[i] = MakeText($"SpellText_{i}", new Vector2(-Margin - 8, y + 4),
                    new Vector2(SpellPanelW - 10, 36), 13, Color.white, new Vector2(1f, 0f), TextAnchor.LowerRight);
            }

            MakeText("SpellTitle", new Vector2(-Margin, Margin + MaxSpellRows * 44 + 6),
                new Vector2(400, 22), 15, new Color(0.85f, 0.82f, 0.95f),
                new Vector2(1f, 0f), TextAnchor.LowerRight).text =
                "SPELL AKTIF  -  urut damage yang sudah dihasilkan";
        }

        void BuildSpeedControl()
        {
            _speedButtons = new Image[Speeds.Length];
            _speedLabels = new Text[Speeds.Length];

            for (int i = 0; i < Speeds.Length; i++)
            {
                float x = -(Margin + (Speeds.Length - 1 - i) * (SpeedButtonW + 6));
                var pos = new Vector2(x, -Margin);

                _speedButtons[i] = MakeImage($"Speed_{i}", pos, new Vector2(SpeedButtonW, SpeedButtonH),
                    new Color(0.14f, 0.14f, 0.18f, 0.9f), new Vector2(1f, 1f));

                _speedLabels[i] = MakeText($"SpeedLabel_{i}", pos + new Vector2(0, -7),
                    new Vector2(SpeedButtonW, SpeedButtonH), 16, Color.white,
                    new Vector2(1f, 1f), TextAnchor.UpperCenter);
                _speedLabels[i].text = SpeedLabels[i];
            }

            MakeText("SpeedHint", new Vector2(-Margin, -Margin - SpeedButtonH - 4), new Vector2(300, 20), 12,
                new Color(0.6f, 0.6f, 0.68f), new Vector2(1f, 1f), TextAnchor.UpperRight).text =
                "kecepatan  (tombol 1/2/3/4)";

            BuildTimeControl();
        }

        Image _timeButton;
        Text _timeLabel;

        /// <summary>
        /// Tombol siang/malam. Alat DEBUG, dan hanya muncul kalau arenanya memang punya lebih dari
        /// satu wajah — tombol yang tidak mengubah apa pun lebih buruk daripada tidak ada tombol.
        /// </summary>
        void BuildTimeControl()
        {
            if (_biome == null || _biome.Faces < 2) return;

            float y = -(Margin + SpeedButtonH + 22);
            float width = Speeds.Length * SpeedButtonW + (Speeds.Length - 1) * 6;
            var pos = new Vector2(-Margin, y);

            _timeButton = MakeImage("TimeToggle", pos, new Vector2(width, SpeedButtonH),
                new Color(0.16f, 0.18f, 0.26f, 0.9f), new Vector2(1f, 1f));

            _timeLabel = MakeText("TimeToggleLabel", pos + new Vector2(0, -7),
                new Vector2(width, SpeedButtonH), 15, new Color(0.92f, 0.9f, 0.7f),
                new Vector2(1f, 1f), TextAnchor.UpperCenter);

            RefreshTimeLabel();
        }

        void RefreshTimeLabel()
        {
            if (_timeLabel == null || _biome == null) return;

            var face = _biome.Current;
            _timeLabel.text = (face != null ? face.DisplayName : "?") + "   (T)";
        }

        void ToggleTime()
        {
            if (_biome == null || _biome.Faces < 2) return;

            _biome.Show(_biome.FaceIndex + 1);
            RefreshTimeLabel();
        }

        Image _bossBg;
        Image _bossFill;
        Text _bossLabel;

        /// <summary>
        /// Bar HP boss, di ATAS layar dan lebar penuh.
        ///
        /// Panjang badan ularnya memang sudah menceritakan sisa HP-nya, dan itu bacaan yang bagus —
        /// tapi ia satu-satunya, dan cuma terbaca kalau seluruh ularnya kebetulan sedang di layar.
        /// Di tengah gerombolan tiga ratus musuh, itu hampir tidak pernah terjadi.
        /// </summary>
        void BuildBossBar()
        {
            _bossBg = MakeImage("BossBg", new Vector2(-320f, -14f), new Vector2(640f, 22f),
                new Color(0.1f, 0.03f, 0.05f, 0.92f), new Vector2(0.5f, 1f));

            _bossFill = MakeImage("BossFill", new Vector2(-320f, -14f), new Vector2(640f, 22f),
                new Color(0.85f, 0.2f, 0.24f, 0.95f), new Vector2(0.5f, 1f));
            _bossFill.type = Image.Type.Filled;
            _bossFill.fillMethod = Image.FillMethod.Horizontal;
            _bossFill.fillOrigin = 0;

            _bossLabel = MakeText("BossLabel", new Vector2(-320f, -15f), new Vector2(640f, 22f), 14,
                Color.white, new Vector2(0.5f, 1f), TextAnchor.UpperCenter);

            SetBossBar(false);
        }

        void SetBossBar(bool on)
        {
            _bossBg.enabled = on;
            _bossFill.enabled = on;
            _bossLabel.enabled = on;
        }

        void DrawBossBar()
        {
            var boss = Enemies.Boss;

            if (boss == null || !boss.Alive)
            {
                if (_bossBg.enabled) SetBossBar(false);
                return;
            }

            if (!_bossBg.enabled) SetBossBar(true);

            _bossFill.fillAmount = boss.HpFraction;
            _bossLabel.text = boss.Def.DisplayName + "   " +
                              Mathf.CeilToInt(boss.HpFraction * 100f) + "%";
        }

        void BuildHud()
        {
            _hudText = MakeText("Hud", new Vector2(Margin, -Margin), new Vector2(600, 26), 18,
                Color.white, new Vector2(0f, 1f), TextAnchor.UpperLeft);

            _hpBg = MakeImage("HpBg", new Vector2(Margin, -50), new Vector2(260, 18),
                new Color(0.16f, 0.07f, 0.08f, 0.9f), new Vector2(0f, 1f));

            // Sits between background and fill: creation order is draw order on this canvas.
            _hpChip = MakeImage("HpChip", new Vector2(Margin, -50), new Vector2(260, 18),
                HpChipColor, new Vector2(0f, 1f));
            _hpChip.type = Image.Type.Filled;
            _hpChip.fillMethod = Image.FillMethod.Horizontal;
            _hpChip.fillOrigin = 0;

            _hpFill = MakeImage("HpFill", new Vector2(Margin, -50), new Vector2(260, 18),
                HpFillColor, new Vector2(0f, 1f));
            _hpFill.type = Image.Type.Filled;
            _hpFill.fillMethod = Image.FillMethod.Horizontal;
            _hpFill.fillOrigin = 0;
            _hpLabel = MakeText("HpLabel", new Vector2(Margin + 6, -51), new Vector2(250, 18), 13,
                Color.white, new Vector2(0f, 1f), TextAnchor.UpperLeft);

            _manaBg = MakeImage("ManaBg", new Vector2(Margin, -72), new Vector2(260, 18),
                new Color(0.08f, 0.09f, 0.16f, 0.9f), new Vector2(0f, 1f));
            _manaFill = MakeImage("ManaFill", new Vector2(Margin, -72), new Vector2(260, 18),
                new Color(0.35f, 0.6f, 1f, 0.95f), new Vector2(0f, 1f));
            _manaFill.type = Image.Type.Filled;
            _manaFill.fillMethod = Image.FillMethod.Horizontal;
            _manaFill.fillOrigin = 0;
            _manaLabel = MakeText("ManaLabel", new Vector2(Margin + 6, -73), new Vector2(250, 18), 13,
                Color.white, new Vector2(0f, 1f), TextAnchor.UpperLeft);

            _tipBg = MakeImage("TipBg", Vector2.zero, new Vector2(360, 150),
                new Color(0.06f, 0.06f, 0.09f, 0.96f), Vector2.zero);
            _tipBg.rectTransform.pivot = new Vector2(0f, 1f);
            _tipBg.enabled = false;

            _tipText = MakeText("TipText", Vector2.zero, new Vector2(344, 140), 13,
                new Color(0.92f, 0.92f, 0.96f), Vector2.zero, TextAnchor.UpperLeft);
            _tipText.rectTransform.pivot = new Vector2(0f, 1f);
            _tipText.enabled = false;

            _bannerText = MakeText("Banner", new Vector2(0, 210), new Vector2(900, 100), 28,
                Color.white, new Vector2(0.5f, 0.5f), TextAnchor.MiddleCenter);
            _bannerText.text = "";

            _startBg = MakeImage("StartBg", new Vector2(0, 120), new Vector2(StartButtonW, StartButtonH),
                new Color(0.35f, 0.75f, 0.4f, 0.95f), new Vector2(0.5f, 0.5f));

            _startLabel = MakeText("StartLabel", new Vector2(0, 120), new Vector2(StartButtonW, StartButtonH),
                20, new Color(0.06f, 0.12f, 0.07f), new Vector2(0.5f, 0.5f), TextAnchor.MiddleCenter);
            _startLabel.text = "MULAI WAVE   (SPACE)";
        }

        void BuildMeter()
        {
            // A single line now, tucked under the panel title — the whole meter block it replaced
            // was a second copy of information the rows already carry.
            _meterText = MakeText("Meter", new Vector2(-Margin, Margin + MaxSpellRows * 44 + 26),
                new Vector2(SpellPanelW + 120, 20), 12, new Color(0.72f, 0.76f, 0.85f),
                new Vector2(1f, 0f), TextAnchor.LowerRight);

            _buffStrip = new StatusStrip(_canvas.transform, _font, 6, StripIcon,
                new Color(1f, 0.92f, 0.55f));

            _debuffStrip = new StatusStrip(_canvas.transform, _font, 4, StripIcon,
                new Color(1f, 0.5f, 0.5f));

            _ailmentStrip = new StatusStrip(_canvas.transform, _font, 8, StripIcon,
                new Color(0.85f, 0.88f, 0.95f));
        }

        /// <summary>
        /// One line above the spell panel: the run total, plus whatever damage did NOT come from a
        /// placed skill — reactions and ailments, which have no row of their own.
        /// </summary>
        void DrawMeter()
        {
            // Throttled: the numbers move constantly but nobody reads them four times a frame.
            _meterTimer -= Time.unscaledDeltaTime;
            if (_meterTimer > 0f) return;

            _meterTimer = 0.25f;

            if (_meter.Total <= 0f)
            {
                _meterText.text = "";
                return;
            }

            _sb.Length = 0;
            _sb.Append("total ").Append(Mathf.RoundToInt(_meter.Total));

            string others = _meter.BuildOtherSources(IsPlacedSkill, 4);
            if (others.Length > 0) _sb.Append("      ").Append(others);

            _meterText.text = _sb.ToString();
        }

        /// <summary>True when this damage source already has its own row in the spell panel.</summary>
        bool IsPlacedSkill(string source)
        {
            var spells = Book.Spells;
            for (int i = 0; i < spells.Count; i++)
            {
                if (spells[i].Source.Def.DisplayName == source) return true;
            }

            return false;
        }

        /// <summary>
        /// Three icon strips stacked under the mana bar: what is helping you, what is hurting you,
        /// and what is currently burning through the swarm.
        /// </summary>
        void DrawBuffs()
        {
            PushSlots(_buffStrip, Player.Buffs, StripBuffY);
            PushSlots(_debuffStrip, Player.Debuffs, StripDebuffY);

            _ailmentStrip.Begin(new Vector2(Margin, StripAilmentY));

            var counts = Enemies.StatusCounts;
            for (int i = 0; i < _db.Statuses.Count && i < counts.Length; i++)
            {
                var status = _db.Statuses[i];

                // Only what is actually on the field. A permanent row of zeroes is noise.
                if (status == null || counts[i] <= 0) continue;

                _ailmentStrip.Push(status.Icon, status.Color, counts[i].ToString(),
                    status.DisplayName + "  -  " + counts[i] + " musuh terkena\n" + status.Blurb);
            }

            _ailmentStrip.Apply();
        }

        void PushSlots(StatusStrip strip, PlayerCaster.BuffSlot[] slots, float y)
        {
            strip.Begin(new Vector2(Margin, y));

            for (int i = 0; i < slots.Length; i++)
            {
                var def = slots[i].Def;
                if (def == null) continue;

                // A charge's number is how many you are holding — that is the thing you are
                // watching. A plain buff's number is how long you have left.
                int stacks = Mathf.Max(1, slots[i].Stacks);
                string label = def.IsCharge ? stacks + "x" : slots[i].Remaining.ToString("0.0");

                strip.Push(def.Icon, def.Color, label,
                    def.DisplayName + (def.IsCharge ? "  -  " + stacks + "/" + def.MaxStacks + " charge" : "")
                    + "  -  " + slots[i].Remaining.ToString("0.0") + "s\n" +
                    _tooltips.DescribeMods(def));
            }

            strip.Apply();
        }

        /// <summary>Description of whichever strip icon the cursor is over, or null.</summary>
        string StripTooltip(Vector2 mouse) =>
            _buffStrip.TooltipAt(mouse) ?? _debuffStrip.TooltipAt(mouse) ?? _ailmentStrip.TooltipAt(mouse);

        void BuildFloaters()
        {
            _floaters = new Text[FloatPoolSize];
            _floatLife = new float[FloatPoolSize];
            _floatWorld = new Vector3[FloatPoolSize];

            for (int i = 0; i < FloatPoolSize; i++)
            {
                _floaters[i] = MakeText($"Float_{i}", Vector2.zero, new Vector2(300, 28), 20,
                    Color.white, Vector2.zero, TextAnchor.MiddleCenter);
                _floaters[i].text = "";
            }
        }

        int ValueOf(PieceDefinition def) => _balance.SellValueOf(def);

        // ---------- drop routing ----------

        /// <summary>Runes go straight into the grimoire â€” they have no storage.</summary>
        bool AutoPlaceInGrimoire(PieceDefinition def)
        {
            for (int rot = 0; rot < 4; rot++)
            {
                for (int y = 0; y < Grimoire.Height; y++)
                {
                    for (int x = 0; x < Grimoire.Width; x++)
                    {
                        if (Book.Place(def, new Vector2Int(x, y), rot) != null) return true;
                    }
                }
            }

            return false;
        }

        bool AutoStoreInBag(PieceDefinition def)
        {
            for (int rot = 0; rot < 4; rot++)
            {
                for (int y = 0; y < Backpack.Height; y++)
                {
                    for (int x = 0; x < Backpack.Width; x++)
                    {
                        if (_bag.Place(def, new Vector2Int(x, y), rot) != null) return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Kill drops wait here until the wave is over.
        ///
        /// The grimoire is locked while a wave runs, so a piece that lands mid-wave is something you
        /// can only watch — and by the time the wave ends the floor is carpeted with items that
        /// arrived while you had no say. Handing them all over at once, when the board unlocks, is
        /// the moment the player can actually act on them.
        /// </summary>
        readonly List<PieceDefinition> _pendingDrops = new List<PieceDefinition>();

        DropPickups _pickups;

        /// <summary>Value of drops rolled past the per-wave cap. Paid out as coins instead.</summary>
        int _pendingGold;

        void ReleaseDrops()
        {
            for (int i = 0; i < _balance.WaveClearDrops; i++)
            {
                var bonus = _db.RandomDrop(_balance.RuneShareOfDrops, _balance.DropStarWeights);
                if (bonus != null) _pendingDrops.Add(bonus);
            }

            int count = _pendingDrops.Count;

            // Dilempar ke lapangan, bukan dimasukkan langsung. Piece yang tiba-tiba ada di kantong
            // sudah benar secara mekanik dan sama sekali tidak terbaca sebagai hadiah.
            for (int i = 0; i < count; i++) _pickups.Toss(_pendingDrops[i]);
            _pendingDrops.Clear();

            _gold += _pendingGold;

            // One line for the whole haul. The pieces are already scattered on screen to be seen —
            // a floater per drop would just bury them under their own labels.
            if (count > 0 || _pendingGold > 0)
            {
                _sb.Length = 0;
                if (count > 0) _sb.Append(count).Append(" drop");
                if (_pendingGold > 0)
                {
                    if (_sb.Length > 0) _sb.Append("   ");
                    _sb.Append("kelebihan kejual +").Append(_pendingGold).Append(" koin");
                }

                PushFloater(Player.transform.position + Vector3.up * 2.4f,
                    _sb.ToString(), new Color(0.8f, 0.95f, 1f));
            }

            _pendingGold = 0;
        }

        void AddLoose(PieceDefinition def, Vector2? at = null)
        {
            if (_loose.Count >= LoosePoolSize)
            {
                // Screen is carpeted â€” the overflow is sold so nothing silently vanishes.
                _gold += ValueOf(def);
                PushFloater(Player.transform.position + Vector3.up * 2f,
                    "penuh, " + def.DisplayName + " kejual +" + ValueOf(def), new Color(1f, 0.88f, 0.45f));
                return;
            }

            _loose.Add(def);
            _loosePos.Add(at ?? RandomScatterPos());
            Discover(def);
        }

        /// <summary>Kicked-out pieces land right next to where they were, not across the screen.</summary>
        void ScatterAll(List<PieceDefinition> defs, Vector2 near)
        {
            for (int i = 0; i < defs.Count; i++) AddLoose(defs[i], NearScatterPos(near, i));
        }

        void RemoveLoose(int index)
        {
            _loose.RemoveAt(index);
            _loosePos.RemoveAt(index);
        }

        int ScreenToLoose(Vector2 mouse)
        {
            for (int i = _loose.Count - 1; i >= 0; i--)
            {
                var size = PieceSize(Shapes.Rotate(_loose[i].Cells, 0)) * 0.5f;
                var p = _loosePos[i];

                if (mouse.x >= p.x - size.x && mouse.x <= p.x + size.x &&
                    mouse.y >= p.y - size.y && mouse.y <= p.y + size.y)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>Anything left lying around when the wave starts has no home â€” it is sold.</summary>
        void SellLoose()
        {
            if (_loose.Count == 0) return;

            int value = 0;
            for (int i = 0; i < _loose.Count; i++) value += ValueOf(_loose[i]);

            int sold = _loose.Count;
            _loose.Clear();
            _loosePos.Clear();
            _gold += value;

            PushFloater(Player.transform.position + Vector3.up * 2f,
                sold + " tercecer kejual  +" + value + " koin", new Color(1f, 0.88f, 0.45f));
        }

        // ---------- runtime ----------

        void Update()
        {
            if (_inputLock > 0f) _inputLock -= Time.unscaledDeltaTime;

            if (ProtoInput.BackDown && _inputLock <= 0f)
            {
                LoadScene(MainMenuSceneName);
                return;
            }

            bool consumed = HandleSpeed();
            if (!consumed) HandleInput();

            if (_evolveTimer > 0f)
            {
                _evolveTimer -= Time.unscaledDeltaTime;
                if (_evolveTimer <= 0f) _evolveText.text = "";
            }

            Redraw();
            TickFloaters(Time.unscaledDeltaTime);
            _popups.Tick(Time.unscaledDeltaTime);
            HandleBanner();
        }

        /// <summary>Returns true when this frame's click landed on a speed button.</summary>
        bool HandleSpeed()
        {
            int key = ProtoInput.SpeedSlotDown;
            if (key >= 0)
            {
                SetSpeed(key);
                return false;
            }

            if (ProtoInput.CycleFaceDown)
            {
                ToggleTime();
                return true;
            }

            if (!ProtoInput.LeftClickDown) return false;

            Vector2 mouse = ProtoInput.MousePosition;
            for (int i = 0; i < Speeds.Length; i++)
            {
                if (SpeedRect(i, Speeds.Length).Contains(mouse))
                {
                    SetSpeed(i);
                    return true;
                }
            }

            if (_timeButton != null && TimeButtonRect(Speeds.Length).Contains(mouse))
            {
                ToggleTime();
                return true;
            }

            return false;
        }

        void SetSpeed(int slot)
        {
            _speedSlot = Mathf.Clamp(slot, 0, Speeds.Length - 1);
            Time.timeScale = Speeds[_speedSlot];

            for (int i = 0; i < Speeds.Length; i++)
            {
                bool on = i == _speedSlot;
                _speedButtons[i].color = on
                    ? new Color(0.9f, 0.75f, 0.3f, 0.95f)
                    : new Color(0.14f, 0.14f, 0.18f, 0.9f);
                _speedLabels[i].color = on ? new Color(0.1f, 0.1f, 0.12f) : Color.white;
            }
        }

        // Dengan sutradara run terpasang, tombol MULAI WAVE pensiun — portal yang memberangkatkan.
        bool CanStartWave() =>
            _run == null && Player.Alive && !Enemies.WaveActive && Book.Spells.Count > 0;

        void StartNextWave()
        {
            StashHeld();
            SellLoose();
            Player.ResetCooldowns();
            Enemies.StartWave(Enemies.Wave + 1);
        }

        void HandleBanner()
        {
            bool showStart = CanStartWave();
            _startBg.enabled = showStart;
            _startLabel.enabled = showStart;

            if (!Player.Alive)
            {
                _gridTitle.text = "GRIMOIRE";
                _bannerText.color = new Color(1f, 0.4f, 0.4f);
                _bannerText.text = "MATI di wave " + Enemies.Wave +
                                   "\nSPACE buat ulang   -   ESC buat balik ke menu";

                if (ProtoInput.RestartDown) LoadScene(GameSceneName);

                return;
            }

            if (!Enemies.WaveActive)
            {
                _gridTitle.text = "GRIMOIRE   (bisa diubah)";
                _bannerText.color = new Color(0.55f, 0.95f, 0.6f);

                if (_run != null)
                {
                    if (_run.Resting)
                        _bannerText.text = "PULAU REHAT — " + RunDirector.KindLabel(_run.RestKind) +
                                           "\nklik portal LANJUT kalau sudah";
                    else if (Enemies.Wave == 0)
                        _bannerText.text = "SUSUN GRIMOIRE-MU\nlalu klik PORTAL buat berangkat   -   M = peta";
                    else
                        _bannerText.text = "WAVE " + Enemies.Wave +
                                           " BERES\nklik PORTAL berikutnya   -   M = peta";

                    if (Book.Spells.Count == 0)
                    {
                        _bannerText.color = new Color(1f, 0.75f, 0.4f);
                        _bannerText.text += "\n\npasang minimal 1 SKILL di atas rune dulu";
                    }

                    return;
                }

                if (Enemies.Wave == 0)
                    _bannerText.text = "SUSUN GRIMOIRE-MU";
                else if (ShopEventActive)
                    _bannerText.text = "WAVE " + Enemies.Wave + " BERES\nTOKO BUKA - klik tombol TOKO di kiri";
                else
                    _bannerText.text = "WAVE " + Enemies.Wave + " BERES\nsusun ulang, atau jual yang nggak kepake";

                if (!showStart)
                {
                    _bannerText.color = new Color(1f, 0.75f, 0.4f);
                    _bannerText.text += "\n\npasang minimal 1 SKILL di atas rune dulu";
                }
                else if (ProtoInput.RestartDown && _inputLock <= 0f)
                {
                    StartNextWave();
                }

                return;
            }

            _gridTitle.text = "GRIMOIRE   (TERKUNCI - wave lagi jalan)";
            _bannerText.text = "";
        }

        void HandleInput()
        {
            // Peta boleh diintip kapan pun, termasuk saat wave berjalan — ia murni baca.
            if (_run != null && ProtoInput.MapDown) _mapOpen = !_mapOpen;

            if (!Player.Alive || _inputLock > 0f) return;

            // Grimoire is locked while a wave runs â€” you only watch.
            if (Enemies.WaveActive)
            {
                if (_held != null) StashHeld();
                return;
            }

            Vector2 mouse = ProtoInput.MousePosition;

            // R rotates, right-click locks. They used to share right-click, which meant the same
            // button did two unrelated things depending on whether your hand was full.
            if (ProtoInput.RotateDown)
            {
                if (_held != null)
                {
                    DropRiders();
                    _heldRot++;
                }

                return;
            }

            if (ProtoInput.RightClickDown)
            {
                // A locked piece is never eaten by an evolution, and no connector is ever drawn to
                // it — that is how you protect a piece you actually want to keep.
                var lockCell = ScreenToCell(mouse);
                if (lockCell.x >= 0)
                {
                    var target = Book.SkillAt(lockCell) ?? Book.BaseAt(lockCell);
                    if (target != null) target.Locked = !target.Locked;
                    return;
                }

                var lockBag = ScreenToBagCell(mouse);
                if (lockBag.x >= 0)
                {
                    var stored = _bag.At(lockBag);
                    if (stored != null) stored.Locked = !stored.Locked;
                }

                return;
            }

            if (!ProtoInput.LeftClickDown) return;

            if (HandlePanelClick(mouse)) return;

            if (CanStartWave() && StartButtonRect().Contains(mouse))
            {
                StartNextWave();
                return;
            }

            if (_held != null)
            {
                if (SellRect().Contains(mouse))
                {
                    SellHeld();
                    return;
                }

                var bagTarget = ScreenToBagCell(mouse);
                if (bagTarget.x >= 0)
                {
                    var bagOrigin = bagTarget - AnchorOffset(_held, _heldRot);

                    // The bag has no bases, so a rune cannot go in and its passengers have nowhere
                    // to ride to. They come off here rather than vanishing with the base.
                    if (_bag.Place(_held, bagOrigin, _heldRot) != null)
                    {
                        DropRiders();
                        _held = null;
                    }
                    else if (_bag.CanReplaceAt(_held, bagOrigin, _heldRot))
                    {
                        ScatterAll(_bag.ClearFootprint(_held, bagOrigin, _heldRot), mouse);

                        if (_bag.Place(_held, bagOrigin, _heldRot) != null)
                        {
                            DropRiders();
                            _held = null;
                        }
                    }

                    return;
                }

                var target = ScreenToCell(mouse);
                if (target.x >= 0)
                {
                    var gridOrigin = target - AnchorOffset(_held, _heldRot);

                    if (Book.Place(_held, gridOrigin, _heldRot) != null)
                    {
                        LandRiders(gridOrigin, mouse);
                        _held = null;
                    }
                    else if (Book.CanReplaceAt(_held, gridOrigin, _heldRot))
                    {
                        // Occupied â€” kick the old piece out and take its spot.
                        ScatterAll(Book.ClearFootprint(_held, gridOrigin, _heldRot), mouse);

                        if (Book.Place(_held, gridOrigin, _heldRot) != null)
                        {
                            LandRiders(gridOrigin, mouse);
                            _held = null;
                        }
                    }

                    return;
                }

                // Clicked empty space â€” drop it right there.
                DropRiders();
                AddLoose(_held, mouse);
                _held = null;
                return;
            }

            int looseIndex = ScreenToLoose(mouse);
            if (looseIndex >= 0)
            {
                _held = _loose[looseIndex];
                _heldRot = 0;
                RemoveLoose(looseIndex);
                return;
            }

            var bagCell = ScreenToBagCell(mouse);
            if (bagCell.x >= 0)
            {
                var stored = _bag.At(bagCell);
                if (stored != null)
                {
                    _held = stored.Def;
                    _heldRot = stored.Rot;
                    _bag.Remove(stored);
                }

                return;
            }

            var cell = ScreenToCell(mouse);
            if (cell.x < 0) return;

            // Skills sit on top, so they get picked up first.
            var inst = Book.SkillAt(cell) ?? Book.BaseAt(cell);
            if (inst == null) return;

            _held = inst.Def;
            _heldRot = inst.Rot;

            // Lifting a base takes whatever is standing on it. Anything bridging two bases belongs
            // to neither, so Remove sends those to the floor as it always did.
            _riders = Book.LiftRiders(inst);
            ScatterAll(Book.Remove(inst), mouse);
        }

        /// <summary>Shop / recipe panel clicks. Returns true when the click was consumed.</summary>
        bool ShopEventActive =>
            Player.Alive && !Enemies.WaveActive &&
            (_run != null
                ? _run.Resting && _run.RestKind == RunNodeKind.Shop
                : Enemies.Wave > 0 && Enemies.Wave % _balance.ShopEveryWaves == 0);

        bool HandlePanelClick(Vector2 mouse)
        {
            if (_run != null && MapButtonRect().Contains(mouse))
            {
                _mapOpen = !_mapOpen;
                return true;
            }

            if (_mapOpen)
            {
                // Peta itu kaca: klik di luar menutupnya, klik di dalam tidak memilih apa-apa —
                // memilih tetap urusan portal di lapangan.
                _mapOpen = MapPanelRect().Contains(mouse);
                return true;
            }

            if (_eventOpen)
            {
                // Modal sungguhan: kejadian menelan SEMUA klik. Pilihan yang bisa tertutup oleh
                // klik nyasar bukan pilihan.
                if (EventOptionRect(0).Contains(mouse))
                {
                    _gold += _balance.EventGoldGift;
                    Announce("+" + _balance.EventGoldGift + " KOIN", new Color(1f, 0.84f, 0.32f));
                    _eventDone = true;
                    _eventOpen = false;
                }
                else if (EventOptionRect(1).Contains(mouse) && _gold >= _balance.EventTradeCost)
                {
                    _gold -= _balance.EventTradeCost;
                    var prize = _db.RandomOfStar(3, 0.25f);

                    if (prize != null)
                    {
                        AddLoose(prize, NearScatterPos(PanelRect().center, 2));
                        Discover(prize);
                        Announce(prize.DisplayName + "!", new Color(0.75f, 0.5f, 1f));
                    }

                    _eventDone = true;
                    _eventOpen = false;
                }

                return true;
            }

            if (_gambleOpen)
            {
                if (!PanelRect().Contains(mouse))
                {
                    _gambleOpen = false;
                    return true;
                }

                if (RerollRect().Contains(mouse) && _spinLeft <= 0f)
                {
                    if (_gold < _balance.GambleCost)
                    {
                        _slotResultLine = "koin kurang";
                        return true;
                    }

                    // Hasil diundi SEKARANG, animasi cuma menunda pengumumannya — gulungan yang
                    // menentukan hasil di frame berhentinya sendiri tidak bisa dites.
                    _gold -= _balance.GambleCost;
                    _slotOutcome = RollGambleOutcome();
                    _spinLeft = 1.15f;
                }

                return true;
            }

            if (ShopEventActive && ShopButtonRect().Contains(mouse))
            {
                _shopOpen = !_shopOpen;
                return true;
            }

            if (!_shopOpen) return false;

            if (!PanelRect().Contains(mouse))
            {
                // Clicking outside closes the panel instead of dropping the held piece behind it.
                _shopOpen = false;
                return true;
            }

            if (RerollRect().Contains(mouse))
            {
                if (_gold >= _rerollCost)
                {
                    _gold -= _rerollCost;
                    _rerollCost += _balance.RerollCostIncrement;
                    RollShop();
                }

                return true;
            }

            for (int i = 0; i < ShopSlots; i++)
            {
                if (_shop[i] == null) continue;
                if (!ShopSlotRect(i).Contains(mouse)) continue;

                int price = _balance.PriceOf(_shop[i]);
                if (_gold < price) return true;

                _gold -= price;
                AddLoose(_shop[i], NearScatterPos(ShopSlotRect(i).center, i));
                _shop[i] = null;
                return true;
            }

            return true;
        }

        /// <summary>Re-seats the skills that travelled with a base. Whatever no longer fits falls.</summary>
        void LandRiders(Vector2Int runeOrigin, Vector2 near)
        {
            if (_riders.Count == 0) return;

            ScatterAll(Book.SeatRiders(_riders, runeOrigin), near);
            _riders.Clear();
        }

        /// <summary>Lets go of the passengers without moving the base. They land where you stand.</summary>
        void DropRiders()
        {
            if (_riders.Count == 0) return;

            for (int i = 0; i < _riders.Count; i++) AddLoose(_riders[i].Def);
            _riders.Clear();
        }

        /// <summary>Puts the held piece down on the floor â€” nothing is ever lost silently.</summary>
        void StashHeld()
        {
            if (_held == null) return;

            DropRiders();
            AddLoose(_held);
            _held = null;
        }

        void SellHeld()
        {
            if (_held == null) return;

            // Only the base is sold. Selling a rune must not quietly sell whatever was standing
            // on it — those go back on the floor.
            DropRiders();

            int value = ValueOf(_held);
            _gold += value;
            PushFloater(Player.transform.position + Vector3.up * 2f,
                _held.DisplayName + " kejual  +" + value, new Color(1f, 0.88f, 0.45f));
            _held = null;
        }

        void OnEnemyKilled(Vector3 at)
        {
            if (Random.value > _balance.KillDropChance) return;

            var drop = _db.RandomDrop(_balance.RuneShareOfDrops, _balance.DropStarWeights);
            if (drop == null) return;

            if (_pendingDrops.Count < _balance.MaxDropsPerWave) _pendingDrops.Add(drop);
            else _pendingGold += ValueOf(drop);
        }

        void OnWaveCleared()
        {
            Player.Hp = Mathf.Min(Player.MaxHp, Player.Hp + _balance.HealPerWaveClear);

            // Toko milik PETA sekarang — stoknya dikocok saat mendarat di node toko. Jalur
            // kelipatan-wave lama cuma hidup kalau run berjalan tanpa sutradara peta.
            if (_run == null && Enemies.Wave % _balance.ShopEveryWaves == 0) RollShop();

            ReleaseDrops();

            var evolutions = Book.ResolveEvolutions();

            // The bag cooks too. Spare copies used to pile up in there with nowhere to go.
            evolutions.AddRange(_bag.ResolveEvolutions(_db));
            for (int i = 0; i < Book.Placed.Count; i++) Discover(Book.Placed[i].Def);
            if (evolutions.Count == 0) return;

            _sb.Length = 0;
            _sb.Append("EVOLVE!   ");
            for (int i = 0; i < evolutions.Count; i++)
            {
                if (i > 0) _sb.Append("   |   ");
                _sb.Append(evolutions[i]);
            }

            _evolveText.text = _sb.ToString();
            _evolveTimer = 6f;
            PushFloater(Player.transform.position + Vector3.up * 3f, "EVOLVE!", new Color(0.55f, 1f, 0.7f));
        }

        void DrawEvoLines()
        {
            var ghostDef = ResolveGhost(out var ghostOrigin);

            // While a piece is in hand the line has to track the cursor, so a moved ghost forces an
            // immediate rebuild instead of waiting out the idle throttle.
            bool ghostChanged = ghostDef != _ghostDef || ghostOrigin != _ghostOrigin || _heldRot != _ghostRot;
            _ghostDef = ghostDef;
            _ghostOrigin = ghostOrigin;
            _ghostRot = _heldRot;

            _previewTimer -= Time.unscaledDeltaTime;
            if (_previewTimer <= 0f || ghostChanged)
            {
                _previewTimer = 0.25f;
                _previews = Book.FindPendingGroups(ghostDef, ghostOrigin, _heldRot);
                _bagPreviews = _bag.FindPendingGroups(_db);
            }

            int cursor = 0;
            for (int g = 0; g < _previews.Count; g++) cursor = DrawEvoGroup(_previews[g], cursor, GridPoint);

            // The bag evolves as well, so it needs the same connectors — in bag coordinates.
            for (int g = 0; g < _bagPreviews.Count; g++)
                cursor = DrawEvoGroup(_bagPreviews[g], cursor, BagPoint);

            cursor = DrawPartnerLines(cursor);

            for (int i = cursor; i < EvoLinePool; i++) _evoLines[i].enabled = false;
        }

        /// <summary>
        /// While a piece is in hand, blue cables run from the cursor to everything on the board it
        /// could combine with.
        ///
        /// Always blue. These are possibilities, and gold means one thing only in this UI: that
        /// group is going to evolve when the wave ends. Nothing still in your hand can make that
        /// promise, so nothing still in your hand is allowed to be gold.
        ///
        /// Drawn from the CURSOR rather than a grid cell, so the answer is there the instant the
        /// piece leaves the floor instead of only once it already hovers a legal square.
        /// </summary>
        int DrawPartnerLines(int cursor)
        {
            if (_held == null || !Player.Alive || Enemies.WaveActive) return cursor;

            _partnerTimer -= Time.unscaledDeltaTime;
            if (_partnerTimer <= 0f || _partnerFor != _held)
            {
                _partnerTimer = 0.25f;
                _partnerFor = _held;
                _partners = Book.FindPartners(_held);
            }

            var from = ProtoInput.MousePosition;

            for (int i = 0; i < _partners.Count && cursor < EvoLinePool; i++)
            {
                DrawEvoLink(_evoLines[cursor++], from, GridPoint(_partners[i]),
                    LinkIncomplete, EvoLineThin);
            }

            return cursor;
        }

        /// <summary>
        /// Wires one recipe group together. The ingredients are visited nearest-first so the chain
        /// hugs the pieces instead of criss-crossing the board when a group has three parts.
        /// </summary>
        int DrawEvoGroup(EvoPreview preview, int cursor, System.Func<Vector2, Vector2> toPixels)
        {
            var members = preview.Members;
            if (members == null || members.Length < 2) return cursor;

            // Gold is a promise that this group WILL evolve when the wave ends. A group that only
            // adds up because the piece on the cursor is counted in has promised nothing — the
            // player can still drop it somewhere else, so it stays blue until it is actually down.
            bool settled = preview.Complete && !preview.NeedsHeldPiece;

            var color = settled ? LinkComplete : LinkIncomplete;
            float thickness = settled ? EvoLineThick : EvoLineThin;

            // Walk order: start at the first ingredient, then always hop to the closest one left.
            System.Array.Copy(members, _evoWalk, members.Length);
            int count = members.Length;

            for (int i = 1; i < count; i++)
            {
                int nearest = i;
                float bestSqr = float.MaxValue;

                for (int k = i; k < count; k++)
                {
                    float sqr = (_evoWalk[k] - _evoWalk[i - 1]).sqrMagnitude;
                    if (sqr >= bestSqr) continue;

                    bestSqr = sqr;
                    nearest = k;
                }

                var swap = _evoWalk[i];
                _evoWalk[i] = _evoWalk[nearest];
                _evoWalk[nearest] = swap;

                if (cursor >= EvoLinePool) break;
                DrawEvoLink(_evoLines[cursor++], toPixels(_evoWalk[i - 1]), toPixels(_evoWalk[i]),
                    color, thickness);
            }

            return cursor;
        }

        /// <summary>Both ends in canvas pixels — the caller decides what space it is working in.</summary>
        static void DrawEvoLink(Image line, Vector2 a, Vector2 b, Color color, float thickness)
        {
            var delta = b - a;

            line.enabled = true;
            line.color = color;
            line.rectTransform.anchoredPosition = (a + b) * 0.5f;
            line.rectTransform.sizeDelta = new Vector2(delta.magnitude, thickness);
            line.rectTransform.localRotation =
                Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        }

        /// <summary>Centre of a (possibly fractional) grid cell, in canvas pixels.</summary>
        static Vector2 GridPoint(Vector2 cell)
        {
            float step = CellSize + CellGap;
            return new Vector2(Margin + cell.x * step + CellSize * 0.5f,
                               Margin + cell.y * step + CellSize * 0.5f);
        }

        /// <summary>Same, for the backpack — different origin and a smaller cell.</summary>
        static Vector2 BagPoint(Vector2 cell)
        {
            float step = BagCell + BagGap;
            return new Vector2(RightX() + cell.x * step + BagCell * 0.5f,
                               BagY + cell.y * step + BagCell * 0.5f);
        }

        /// <summary>
        /// The grid cell the held piece would occupy right now, or null when nothing is held or the
        /// cursor is off the grid. Only reports a spot the piece could legally take.
        /// </summary>
        PieceDefinition ResolveGhost(out Vector2Int origin)
        {
            origin = default;
            if (_held == null || !Player.Alive || Enemies.WaveActive) return null;

            var cell = ScreenToCell(ProtoInput.MousePosition);
            if (cell.x < 0) return null;

            origin = cell - AnchorOffset(_held, _heldRot);
            return _held;
        }

        // ==================================================================
        //  peta run, pulau rehat, slot, kejadian
        // ==================================================================

        /// <summary>Menyambungkan sutradara run. UI yang memegang gold dan papan, jadi semua
        /// akibat node (stok toko, hadiah slot, tukar nasib) dieksekusi di sini.</summary>
        public void AttachRun(RunDirector run)
        {
            _run = run;

            run.CanEmbark = () => Player.Alive && Book.Spells.Count > 0;

            // Ritual yang dulu milik tombol MULAI WAVE, sekarang milik langkah masuk portal.
            run.OnEmbark = () =>
            {
                StashHeld();
                SellLoose();
                Player.ResetCooldowns();
            };

            run.OnRestEntered = OnRestEntered;
            run.OnAnnounce = (message, color) => Announce(message, color);
            run.ClickBlocked = () => _mapOpen || _shopOpen || _gambleOpen || _eventOpen;

            BuildRunPanels();
        }

        void OnRestEntered(RunNodeKind kind)
        {
            _shopOpen = false;
            _gambleOpen = false;
            _eventOpen = false;

            if (kind == RunNodeKind.Shop)
            {
                // Stok DIKOCOK tiap kali singgah — node toko yang isinya itu-itu saja bukan
                // hadiah, cuma etalase.
                RollShop();
                _shopOpen = true;
            }
            else if (kind == RunNodeKind.Gamble)
            {
                _gambleOpen = true;
                _spinLeft = 0f;
                _slotOutcome = -1;
                _slotResultLine = "";
            }
            else if (kind == RunNodeKind.Event)
            {
                _eventOpen = true;
                _eventDone = false;
            }
        }

        void BuildRunPanels()
        {
            int nodeCap = Mathf.Max(1, _balance.MapFloorsPerAct * _balance.MapLanes);

            // PEKAT penuh, bukan 0,97 — di bawahnya ada papan grimoire dan teks banner, dan
            // keduanya membayang tembus di alpha berapa pun selain satu.
            _mapBg = MakeImage("MapBg", Vector2.zero, Vector2.zero,
                new Color(0.045f, 0.055f, 0.1f, 1f), Vector2.zero);
            _mapTitle = MakeText("MapTitle", Vector2.zero, new Vector2(320f, 30f), 22,
                new Color(1f, 0.9f, 0.6f), Vector2.zero, TextAnchor.MiddleCenter);
            _mapLegend = MakeText("MapLegend", Vector2.zero, new Vector2(460f, 24f), 13,
                new Color(0.8f, 0.85f, 0.9f), Vector2.zero, TextAnchor.MiddleCenter);

            Centre(_mapBg.rectTransform);
            Centre(_mapTitle.rectTransform);
            Centre(_mapLegend.rectTransform);

            // Jalur = bezier PUTUS-PUTUS, pola yang sama dengan peta referensi (RoguelikeMapUI):
            // tiap sambungan dipecah jadi segmen pendek bercelah dengan control point acak
            // ber-seed — jalan setapak yang berkelok, bukan kabel penggaris.
            _mapEdges = new Image[nodeCap * 2 * MapSegsPerEdge];

            for (int i = 0; i < _mapEdges.Length; i++)
            {
                _mapEdges[i] = MakeImage("MapSeg" + i, Vector2.zero, new Vector2(10f, 5f),
                    Color.gray, Vector2.zero);
                Centre(_mapEdges[i].rectTransform);
                _mapEdges[i].enabled = false;
            }

            _mapNodes = new Image[nodeCap];
            _mapRings = new Image[nodeCap];
            _mapGlyphs = new Text[nodeCap];

            for (int i = 0; i < nodeCap; i++)
            {
                _mapRings[i] = MakeImage("MapRing" + i, Vector2.zero, new Vector2(42f, 42f),
                    Color.white, Vector2.zero);
                Centre(_mapRings[i].rectTransform);
                _mapRings[i].enabled = false;

                _mapNodes[i] = MakeImage("MapNode" + i, Vector2.zero, new Vector2(34f, 34f),
                    Color.white, Vector2.zero);
                Centre(_mapNodes[i].rectTransform);
                _mapNodes[i].enabled = false;

                _mapGlyphs[i] = MakeText("MapGlyph" + i, Vector2.zero, new Vector2(36f, 30f), 16,
                    Color.black, Vector2.zero, TextAnchor.MiddleCenter);
                Centre(_mapGlyphs[i].rectTransform);
                _mapGlyphs[i].enabled = false;
            }

            _mapYou = MakeText("MapYou", Vector2.zero, new Vector2(120f, 24f), 15,
                new Color(1f, 0.85f, 0.4f), Vector2.zero, TextAnchor.MiddleCenter);
            Centre(_mapYou.rectTransform);
            _mapYou.text = "KAMU";
            _mapYou.enabled = false;

            _mapBg.enabled = false;
            _mapTitle.enabled = false;
            _mapLegend.enabled = false;

            _mapBtnBg = MakeImage("MapBtn", Vector2.zero, Vector2.zero,
                new Color(0.2f, 0.3f, 0.45f, 0.9f), Vector2.zero);
            _mapBtnLabel = MakeText("MapBtnLabel", Vector2.zero, new Vector2(88f, 32f), 14,
                Color.white, Vector2.zero, TextAnchor.MiddleCenter);
            _mapBtnLabel.text = "PETA (M)";
            Centre(_mapBtnBg.rectTransform);
            Centre(_mapBtnLabel.rectTransform);
            _mapBtnBg.enabled = false;
            _mapBtnLabel.enabled = false;

            // ---- slot ----
            _slotBg = MakeImage("SlotBg", Vector2.zero, new Vector2(PanelW, PanelH),
                new Color(0.1f, 0.05f, 0.11f, 0.97f), Vector2.zero);
            _slotTitle = MakeText("SlotTitle", Vector2.zero, new Vector2(500f, 30f), 22,
                new Color(1f, 0.45f, 0.85f), Vector2.zero, TextAnchor.MiddleCenter);
            _slotInfo = MakeText("SlotInfo", Vector2.zero, new Vector2(500f, 44f), 15,
                new Color(0.9f, 0.9f, 0.95f), Vector2.zero, TextAnchor.MiddleCenter);
            _slotSpinBg = MakeImage("SlotSpin", Vector2.zero, Vector2.zero,
                new Color(0.75f, 0.2f, 0.5f, 0.95f), Vector2.zero);
            _slotSpinLabel = MakeText("SlotSpinLabel", Vector2.zero, new Vector2(240f, 34f), 18,
                Color.white, Vector2.zero, TextAnchor.MiddleCenter);

            Centre(_slotBg.rectTransform);
            Centre(_slotTitle.rectTransform);
            Centre(_slotInfo.rectTransform);
            Centre(_slotSpinBg.rectTransform);
            Centre(_slotSpinLabel.rectTransform);

            for (int i = 0; i < 3; i++)
            {
                _slotReels[i] = MakeText("SlotReel" + i, Vector2.zero, new Vector2(120f, 80f), 52,
                    Color.white, Vector2.zero, TextAnchor.MiddleCenter);
                Centre(_slotReels[i].rectTransform);
                _slotReels[i].enabled = false;
            }

            _slotBg.enabled = false;
            _slotTitle.enabled = false;
            _slotInfo.enabled = false;
            _slotSpinBg.enabled = false;
            _slotSpinLabel.enabled = false;

            // ---- kejadian ----
            _eventBg = MakeImage("EventBg", Vector2.zero, new Vector2(PanelW, PanelH),
                new Color(0.08f, 0.06f, 0.12f, 0.97f), Vector2.zero);
            _eventTitle = MakeText("EventTitle", Vector2.zero, new Vector2(500f, 30f), 22,
                new Color(0.75f, 0.5f, 1f), Vector2.zero, TextAnchor.MiddleCenter);
            _eventBody = MakeText("EventBody", Vector2.zero, new Vector2(540f, 140f), 17,
                new Color(0.92f, 0.92f, 0.97f), Vector2.zero, TextAnchor.MiddleCenter);
            _eventABg = MakeImage("EventA", Vector2.zero, Vector2.zero,
                new Color(0.2f, 0.4f, 0.25f, 0.95f), Vector2.zero);
            _eventBBg = MakeImage("EventB", Vector2.zero, Vector2.zero,
                new Color(0.4f, 0.24f, 0.45f, 0.95f), Vector2.zero);
            _eventALabel = MakeText("EventALabel", Vector2.zero, new Vector2(260f, 60f), 16,
                Color.white, Vector2.zero, TextAnchor.MiddleCenter);
            _eventBLabel = MakeText("EventBLabel", Vector2.zero, new Vector2(260f, 60f), 16,
                Color.white, Vector2.zero, TextAnchor.MiddleCenter);

            Centre(_eventBg.rectTransform);
            Centre(_eventTitle.rectTransform);
            Centre(_eventBody.rectTransform);
            Centre(_eventABg.rectTransform);
            Centre(_eventBBg.rectTransform);
            Centre(_eventALabel.rectTransform);
            Centre(_eventBLabel.rectTransform);

            _eventBg.enabled = false;
            _eventTitle.enabled = false;
            _eventBody.enabled = false;
            _eventABg.enabled = false;
            _eventBBg.enabled = false;
            _eventALabel.enabled = false;
            _eventBLabel.enabled = false;
        }

        /// <summary>Widget yang diposisikan lewat titik TENGAHNYA — pivot bawaan MakeImage ada
        /// di pojok, dan panel yang dihitung dari pojok selalu meleset separuh ukurannya.</summary>
        static void Centre(RectTransform rt) => rt.pivot = new Vector2(0.5f, 0.5f);

        static Rect EventOptionRect(int side)
        {
            var panel = PanelRect();
            float w = (panel.width - 48f) * 0.5f;
            float x = side == 0 ? panel.xMin + 16f : panel.xMax - 16f - w;
            return new Rect(x, panel.yMin + 18f, w, 64f);
        }

        void DrawRunPanels(float dt)
        {
            bool hasRun = _run != null;

            if (_mapBtnBg != null)
            {
                _mapBtnBg.enabled = hasRun;
                _mapBtnLabel.enabled = hasRun;

                if (hasRun)
                {
                    var r = MapButtonRect();
                    _mapBtnBg.rectTransform.anchoredPosition = r.center;
                    _mapBtnBg.rectTransform.sizeDelta = r.size;
                    _mapBtnLabel.rectTransform.anchoredPosition = r.center;
                }
            }

            DrawMapOverlay();
            DrawGamble(dt);
            DrawEvent();
        }

        void DrawMapOverlay()
        {
            bool open = _mapOpen && _run != null && _mapBg != null;

            if (_mapBg != null)
            {
                _mapBg.enabled = open;
                _mapTitle.enabled = open;
                _mapLegend.enabled = open;
                if (_mapYou != null) _mapYou.enabled = open && _run != null && _run.Map.At >= 0;
            }

            if (!open)
            {
                _mapSig = -1;

                for (int i = 0; i < _mapNodes.Length; i++)
                {
                    if (_mapNodes[i] == null) continue;
                    _mapNodes[i].enabled = false;
                    _mapRings[i].enabled = false;
                    _mapGlyphs[i].enabled = false;
                }

                for (int i = 0; i < _mapEdges.Length; i++)
                {
                    if (_mapEdges[i] != null) _mapEdges[i].enabled = false;
                }

                return;
            }

            var map = _run.Map;
            var reachable = map.Reachable();

            // Tata letak cuma dihitung saat ada yang berubah — enam ratus segmen yang disusun
            // ulang enam puluh kali sedetik adalah harga tanpa barang.
            int sig = _run.Act * 100000 + (map.At + 2) * 100 + map.Nodes.Count;

            if (sig != _mapSig)
            {
                _mapSig = sig;
                LayoutMap(map, reachable);
            }

            // Denyut per frame — murah, cuma warna: node yang BISA diinjak yang bernapas.
            float pulse = 0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 5f);

            for (int i = 0; i < map.Nodes.Count && i < _mapNodes.Length; i++)
            {
                var n = map.Nodes[i];

                if (reachable.Contains(n))
                {
                    var tone = RunDirector.KindColor(n.Kind);
                    tone.a = pulse;
                    _mapNodes[i].color = tone;
                    _mapRings[i].color = new Color(1f, 1f, 1f, pulse);
                }
                else if (map.At == n.Index)
                {
                    _mapRings[i].color = new Color(1f, 0.85f, 0.4f, 0.6f + 0.4f * pulse);
                }
            }
        }

        /// <summary>Posisi layar satu node — KIRI ke KANAN seperti peta referensi (lantai jadi
        /// kolom, lajur jadi baris), plus jitter ber-seed supaya berhenti terbaca sebagai kisi.
        /// Seed membuatnya diam: peta yang bergoyang tiap frame bukan peta.</summary>
        Vector2 MapNodePos(RunNode n, Rect panel, int floors, int lanes)
        {
            float left = panel.xMin + 64f;
            float right = panel.xMax - 64f;
            float bottom = panel.yMin + 70f;
            float top = panel.yMax - 84f;

            float colW = (right - left) / Mathf.Max(1, floors - 1);
            float rowH = (top - bottom) / Mathf.Max(1, lanes - 1);

            uint h = (uint)((n.Index + 1) * 2654435761u);
            float jx = ((h & 0xFF) / 255f - 0.5f) * 16f;
            float jy = (((h >> 8) & 0xFF) / 255f - 0.5f) * 30f;

            return new Vector2(left + n.Floor * colW + jx, bottom + n.Lane * rowH + jy);
        }

        void LayoutMap(RunMap map, List<RunNode> reachable)
        {
            var panel = MapPanelRect();

            _mapBg.rectTransform.sizeDelta = panel.size;
            _mapBg.rectTransform.anchoredPosition = panel.center;

            _mapTitle.rectTransform.anchoredPosition = new Vector2(panel.center.x, panel.yMax - 30f);
            _mapTitle.text = "PETA RUN  -  ACT " + _run.Act;

            _mapLegend.rectTransform.anchoredPosition = new Vector2(panel.center.x, panel.yMin + 22f);
            _mapLegend.text =
                "W wave    E elite    T toko    ? kejadian    S slot    B boss        M = tutup";

            int seg = 0;

            foreach (var n in map.Nodes)
            {
                Vector2 a = MapNodePos(n, panel, map.Floors, map.Lanes);

                foreach (int nextIndex in n.Next)
                {
                    Vector2 b = MapNodePos(map.Nodes[nextIndex], panel, map.Floors, map.Lanes);

                    bool walked = _run.Trail.Contains(n.Index) && _run.Trail.Contains(nextIndex);
                    bool offered = map.At == n.Index && reachable.Contains(map.Nodes[nextIndex]);

                    Color tone = walked ? new Color(0.55f, 0.85f, 0.55f, 0.85f)
                        : offered ? new Color(1f, 0.85f, 0.3f, 0.95f)
                        : new Color(0.55f, 0.58f, 0.66f, 0.38f);

                    seg = DrawTrail(a, b, n.Index * 31 + nextIndex * 7, tone, seg);
                }
            }

            for (; seg < _mapEdges.Length; seg++) _mapEdges[seg].enabled = false;

            for (int i = 0; i < _mapNodes.Length; i++)
            {
                bool live = i < map.Nodes.Count;
                if (_mapNodes[i] == null) continue;

                _mapNodes[i].enabled = live;
                _mapRings[i].enabled = live;
                _mapGlyphs[i].enabled = live;
                if (!live) continue;

                var n = map.Nodes[i];
                var pos = MapNodePos(n, panel, map.Floors, map.Lanes);

                bool now = map.At == n.Index;
                bool next = reachable.Contains(n);
                bool walked = _run.Trail.Contains(n.Index);

                // Jitter ukuran & rotasi ala peta referensi — ber-seed, jadi diam di tempat.
                uint h = (uint)((n.Index + 1) * 1274126177u);
                float wobble = ((h & 0xFF) / 255f - 0.5f) * 0.24f;
                float twist = (((h >> 8) & 0xFF) / 255f - 0.5f) * 14f;

                float size = (now ? 46f : next ? 38f : 32f) * (1f + wobble);

                // Boss selalu paling besar — ia tujuan seluruh act, dan mata harus menemukannya
                // dalam sedetik pertama peta terbuka.
                if (n.Kind == RunNodeKind.Boss) size *= 1.35f;

                var tone = RunDirector.KindColor(n.Kind);
                Color ring;

                if (now) ring = new Color(1f, 0.85f, 0.4f, 1f);
                else if (next) ring = Color.white;
                else if (walked)
                {
                    ring = new Color(0.55f, 0.85f, 0.55f, 0.7f);
                    tone.a = 0.85f;
                }
                else
                {
                    // Terkunci: DIPUCATKAN, bukan sekadar dipudarkan — warna jenisnya masih
                    // terbaca (buat merencanakan jalur), tapi jelas belum bisa diinjak.
                    tone = Color.Lerp(tone, new Color(0.3f, 0.32f, 0.38f), 0.55f);
                    tone.a = 0.55f;
                    ring = new Color(0f, 0f, 0f, 0.35f);
                }

                _mapRings[i].rectTransform.anchoredPosition = pos;
                _mapRings[i].rectTransform.sizeDelta = new Vector2(size + 8f, size + 8f);
                _mapRings[i].rectTransform.localEulerAngles = new Vector3(0f, 0f, twist);
                _mapRings[i].color = ring;

                _mapNodes[i].rectTransform.anchoredPosition = pos;
                _mapNodes[i].rectTransform.sizeDelta = new Vector2(size, size);
                _mapNodes[i].rectTransform.localEulerAngles = new Vector3(0f, 0f, twist);
                _mapNodes[i].color = tone;

                _mapGlyphs[i].rectTransform.anchoredPosition = pos;
                _mapGlyphs[i].text = RunDirector.KindLabel(n.Kind).Substring(0, 1);
                _mapGlyphs[i].color = new Color(0f, 0f, 0f, 0.85f);

                if (now && _mapYou != null)
                {
                    _mapYou.rectTransform.anchoredPosition =
                        pos + new Vector2(0f, size * 0.5f + 16f);
                }
            }
        }

        /// <summary>
        /// Satu sambungan sebagai bezier kubik yang di-sample jadi segmen pendek BERCELAH —
        /// jalan setapak, bukan kabel. Control point diacak ber-seed dari indeks node, jadi
        /// kelokannya sama setiap kali peta dibuka.
        /// </summary>
        int DrawTrail(Vector2 from, Vector2 to, int seed, Color tone, int seg)
        {
            Vector2 span = to - from;
            float length = span.magnitude;
            if (length < 1f) return seg;

            Vector2 perp = new Vector2(-span.y, span.x) / length;

            var rng = new System.Random(seed);
            float curve = length * 0.3f;
            Vector2 p1 = from + span * 0.33f
                         + perp * ((float)(rng.NextDouble() * 2.0 - 1.0) * curve);
            Vector2 p2 = from + span * 0.67f
                         + perp * ((float)(rng.NextDouble() * 2.0 - 1.0) * curve);

            Vector2 prev = from;

            for (int i = 1; i <= MapSegsPerEdge && seg < _mapEdges.Length; i++)
            {
                float t = i / (float)MapSegsPerEdge;
                float u = 1f - t;

                Vector2 point = u * u * u * from + 3f * u * u * t * p1 + 3f * u * t * t * p2
                                + t * t * t * to;

                // Celah 30% di tiap segmen — patahan kecil itulah yang membuatnya terbaca
                // sebagai jejak di peta, bukan kabel listrik.
                Vector2 centre = (prev + point) * 0.5f;
                Vector2 dir = point - prev;
                float len = dir.magnitude * 0.7f;

                var img = _mapEdges[seg++];
                img.enabled = true;
                img.color = tone;
                img.rectTransform.anchoredPosition = centre;
                img.rectTransform.sizeDelta = new Vector2(len, 5f);
                img.rectTransform.localEulerAngles =
                    new Vector3(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);

                prev = point;
            }

            return seg;
        }

        static readonly string[] SlotFaces = { "X", "o", "O", "*2", "*3", "*4" };

        void DrawGamble(float dt)
        {
            bool open = _gambleOpen && _run != null;
            if (_slotBg == null) return;

            _slotBg.enabled = open;
            _slotTitle.enabled = open;
            _slotInfo.enabled = open;
            _slotSpinBg.enabled = open;
            _slotSpinLabel.enabled = open;
            for (int i = 0; i < 3; i++) _slotReels[i].enabled = open;

            if (!open) return;

            var panel = PanelRect();
            _slotBg.rectTransform.anchoredPosition = panel.center;
            _slotTitle.rectTransform.anchoredPosition = new Vector2(panel.center.x, panel.yMax - 28f);
            _slotTitle.text = "MESIN SLOT — " + _balance.GambleCost + " KOIN SEKALI PUTAR";

            for (int i = 0; i < 3; i++)
            {
                _slotReels[i].rectTransform.anchoredPosition =
                    new Vector2(panel.center.x + (i - 1) * 130f, panel.center.y + 24f);
            }

            var spin = RerollRect();
            _slotSpinBg.rectTransform.anchoredPosition = spin.center;
            _slotSpinBg.rectTransform.sizeDelta = spin.size;
            _slotSpinLabel.rectTransform.anchoredPosition = spin.center;
            _slotSpinLabel.text = _spinLeft > 0f ? ". . ." : "PUTAR!";

            _slotInfo.rectTransform.anchoredPosition = new Vector2(panel.center.x, panel.yMin + 58f);
            _slotInfo.text = _slotResultLine + "\nkoin: " + _gold;

            if (_spinLeft > 0f)
            {
                _spinLeft -= dt;

                // Gulungan mengocok wajah dari WAKTU, bukan dari Random gameplay — kocokan visual
                // enam puluh kali sedetik menggeser seluruh sebaran drop tanpa satu pun error.
                for (int i = 0; i < 3; i++)
                {
                    int face = (int)(Time.unscaledTime * 21f + i * 7.3f) % SlotFaces.Length;
                    _slotReels[i].text = SlotFaces[face];
                }

                if (_spinLeft <= 0f) SettleGamble();
            }
        }

        int RollGambleOutcome()
        {
            var weights = _balance.GambleWeights;
            if (weights == null || weights.Length == 0) return 0;

            float total = 0f;
            for (int i = 0; i < weights.Length; i++) total += Mathf.Max(0f, weights[i]);
            if (total <= 0f) return 0;

            float roll = Random.value * total;

            for (int i = 0; i < weights.Length; i++)
            {
                roll -= Mathf.Max(0f, weights[i]);
                if (roll <= 0f) return i;
            }

            return 0;
        }

        void SettleGamble()
        {
            _spinLeft = 0f;

            int outcome = _slotOutcome;
            _slotOutcome = -1;
            if (outcome < 0) return;

            bool win = outcome != 0;

            for (int i = 0; i < 3; i++)
            {
                _slotReels[i].text = win ? SlotFaces[outcome] : SlotFaces[(i * 2 + 1) % 3];
            }

            switch (outcome)
            {
                case 1:
                    _gold += _balance.GambleSmallGold;
                    _slotResultLine = "+" + _balance.GambleSmallGold + " koin";
                    break;

                case 2:
                    _gold += _balance.GambleBigGold;
                    _slotResultLine = "+" + _balance.GambleBigGold + " KOIN!";
                    break;

                case 3:
                case 4:
                case 5:
                    var prize = _db.RandomOfStar(outcome - 1, 0.25f);

                    if (prize != null)
                    {
                        AddLoose(prize, NearScatterPos(PanelRect().center, 1));
                        Discover(prize);
                        _slotResultLine = "JACKPOT: " + prize.DisplayName + "!";
                    }

                    break;

                default:
                    _slotResultLine = "zonk.";
                    break;
            }

            Announce(_slotResultLine,
                win ? new Color(1f, 0.84f, 0.32f) : new Color(0.7f, 0.7f, 0.75f));
        }

        void DrawEvent()
        {
            bool open = _eventOpen && _run != null;
            if (_eventBg == null) return;

            _eventBg.enabled = open;
            _eventTitle.enabled = open;
            _eventBody.enabled = open;
            _eventABg.enabled = open;
            _eventBBg.enabled = open;
            _eventALabel.enabled = open;
            _eventBLabel.enabled = open;

            if (!open) return;

            var panel = PanelRect();
            _eventBg.rectTransform.anchoredPosition = panel.center;
            _eventTitle.rectTransform.anchoredPosition = new Vector2(panel.center.x, panel.yMax - 28f);
            _eventTitle.text = "PERTAPA HUTAN";
            _eventBody.rectTransform.anchoredPosition = new Vector2(panel.center.x, panel.center.y + 30f);
            _eventBody.text = "Sesosok pertapa duduk menghadap api unggun.\n\n" +
                              "\"Jalan di depanmu tidak gratis, penyihir.\nPilih bekalmu.\"";

            var a = EventOptionRect(0);
            var b = EventOptionRect(1);

            _eventABg.rectTransform.anchoredPosition = a.center;
            _eventABg.rectTransform.sizeDelta = a.size;
            _eventALabel.rectTransform.anchoredPosition = a.center;
            _eventALabel.text = "AMBIL BERKAH\n+" + _balance.EventGoldGift + " koin";

            bool affordable = _gold >= _balance.EventTradeCost;
            _eventBBg.rectTransform.anchoredPosition = b.center;
            _eventBBg.rectTransform.sizeDelta = b.size;
            _eventBBg.color = affordable
                ? new Color(0.4f, 0.24f, 0.45f, 0.95f)
                : new Color(0.25f, 0.22f, 0.27f, 0.8f);
            _eventBLabel.rectTransform.anchoredPosition = b.center;
            _eventBLabel.text = "TUKAR NASIB\n-" + _balance.EventTradeCost + " koin  >  piece *3";
        }

        void Redraw()
        {
            DrawGrid();
            DrawEvoLines();
            DrawSkillWidgets(Time.deltaTime);
            DrawBackpack();
            DrawLoose();
            DrawSellBox();
            DrawSpells();
            DrawHud();
            DrawBossBar();
            DrawMeter();
            DrawBuffs();
            DrawRunPanels(Time.deltaTime);
            UpdateTooltip();
        }

        void DrawGrid()
        {
            var emptyColor = _held != null ? ShownCell : HiddenCell;

            for (int y = 0; y < Grimoire.Height; y++)
            {
                for (int x = 0; x < Grimoire.Width; x++)
                {
                    int i = y * Grimoire.Width + x;
                    var cell = new Vector2Int(x, y);

                    var baseRune = Book.BaseAt(cell);
                    _baseCells[i].color = baseRune != null ? Tint(baseRune) : emptyColor;

                    var skill = Book.SkillAt(cell);
                    _skillCells[i].enabled = skill != null;
                    if (skill != null) _skillCells[i].color = Tint(skill);
                }
            }

            UpdateHeldText();

            if (_held == null) return;

            var hover = ScreenToCell(ProtoInput.MousePosition);
            if (hover.x < 0) return;

            var origin = hover - AnchorOffset(_held, _heldRot);
            bool valid = Book.CanPlace(_held, origin, _heldRot);
            var shape = Shapes.Rotate(_held.Cells, _heldRot);
            var tint = valid ? ValidCell : InvalidCell;

            for (int i = 0; i < shape.Length; i++)
            {
                var c = origin + shape[i];
                if (!Grimoire.InBounds(c)) continue;

                int idx = c.y * Grimoire.Width + c.x;
                if (_held.Layer == Layer.Rune) _baseCells[idx].color = tint;
                else
                {
                    _skillCells[idx].enabled = true;
                    _skillCells[idx].color = tint;
                }
            }
        }

        /// <summary>Locked pieces are washed out so you can see at a glance what evolution skips.</summary>
        static Color Tint(RuneInstance inst)
        {
            return inst.Locked ? Color.Lerp(inst.Def.Color, Color.white, 0.55f) : inst.Def.Color;
        }

        void DrawBackpack()
        {
            var emptyColor = _held != null ? ShownBagCell : HiddenBagCell;

            for (int y = 0; y < Backpack.Height; y++)
            {
                for (int x = 0; x < Backpack.Width; x++)
                {
                    int i = y * Backpack.Width + x;
                    var stored = _bag.At(new Vector2Int(x, y));
                    _bagCells[i].color = stored != null ? stored.Def.Color : emptyColor;
                }
            }

            if (_held == null) return;

            var hover = ScreenToBagCell(ProtoInput.MousePosition);
            if (hover.x < 0) return;

            var origin = hover - AnchorOffset(_held, _heldRot);
            bool valid = _bag.CanPlace(_held, origin, _heldRot);
            var shape = Shapes.Rotate(_held.Cells, _heldRot);
            var tint = valid ? ValidCell : InvalidCell;

            for (int i = 0; i < shape.Length; i++)
            {
                var c = origin + shape[i];
                if (!Backpack.InBounds(c)) continue;
                _bagCells[c.y * Backpack.Width + c.x].color = tint;
            }
        }

        void DrawLoose()
        {
            int cursor = 0;

            for (int i = 0; i < _loose.Count; i++)
            {
                cursor = DrawPiece(_loose[i], 0, _loosePos[i], cursor, 1f);
            }

            cursor = DrawPanels(cursor);

            // The carried piece rides the cursor, but not over the grid or bag â€” those already
            // show the footprint, and drawing both looks like a double image.
            if (_held != null)
            {
                var mouse = ProtoInput.MousePosition;
                bool overGrid = ScreenToCell(mouse).x >= 0;
                bool overBag = ScreenToBagCell(mouse).x >= 0;

                if (!overGrid && !overBag) cursor = DrawPiece(_held, _heldRot, mouse, cursor, 0.9f);
            }

            for (int i = cursor; i < _looseCells.Length; i++) _looseCells[i].enabled = false;
        }

        void UpdateHeldText()
        {
            if (_held != null)
            {
                string kind = _held.Layer == Layer.Rune ? "RUNE" : "SKILL";
                string rule = _held.Layer == Layer.Rune
                    ? "RUNE nggak bisa masuk tas - pasang di grimoire atau JUAL"
                    : "bisa dipasang di grimoire atau disimpan di tas";

                _heldText.text = "[" + kind + " - " + _held.DisplayName + "] " + _held.Blurb +
                                 "   |   " + rule + "   (klik kanan = putar)";
                return;
            }

            if (Enemies.WaveActive)
            {
                _heldText.text = "wave lagi jalan - grimoire terkunci, nonton aja sampai wave beres";
                return;
            }

            _heldText.text = "klik item buat ambil  |  klik kanan = putar  |  " +
                             "item yang tercecer pas wave mulai = kejual";
        }

        /// <summary>Detailed hover card + the ground ring showing the hovered skill's reach.</summary>
        void UpdateTooltip()
        {
            PieceDefinition hovered = null;
            CompiledSpell spell = null;
            string origin = "";

            // Strip icons win: they sit in the HUD corner, well away from the board, so a hit there
            // is unambiguous — and their whole reason to exist is answering "what is this".
            string strip = StripTooltip(ProtoInput.MousePosition);
            if (strip != null)
            {
                _recipes.Hide();
                ShowCard(strip);
                Player.HideRange();
                return;
            }

            if (_held != null)
            {
                // Carrying something â€” the card would just sit in the way.
                _tipBg.enabled = false;
                _tipText.enabled = false;
                _recipes.Hide();
                Player.ShowRange(_held.Range, _held.Color);
                return;
            }

            {
                var mouse = ProtoInput.MousePosition;

                int looseIndex = ScreenToLoose(mouse);
                if (looseIndex >= 0)
                {
                    hovered = _loose[looseIndex];
                    origin = "TERCECER";
                }

                if (hovered == null)
                {
                    var bagCell = ScreenToBagCell(mouse);
                    if (bagCell.x >= 0)
                    {
                        var stored = _bag.At(bagCell);
                        if (stored != null)
                        {
                            hovered = stored.Def;
                            origin = "DI TAS (nggak nembak)";
                        }
                    }
                }

                if (hovered == null)
                {
                    var cell = ScreenToCell(mouse);
                    if (cell.x >= 0)
                    {
                        var inst = Book.SkillAt(cell) ?? Book.BaseAt(cell);
                        if (inst != null)
                        {
                            hovered = inst.Def;
                            origin = inst.Locked ? "KEPASANG - TERKUNCI" : "KEPASANG";
                            spell = FindSpell(inst);
                        }
                    }
                }
            }

            if (hovered == null)
            {
                _tipBg.enabled = false;
                _tipText.enabled = false;
                _recipes.Hide();
                Player.HideRange();
                return;
            }

            // ALT swaps the stat card for the recipe card. They occupy the same corner of the
            // screen, so exactly one of them is ever up.
            if (ProtoInput.AltHeld)
            {
                _tipBg.enabled = false;
                _tipText.enabled = false;
                _recipes.Show(hovered, ProtoInput.MousePosition);
                ShowHoverRange(hovered, spell);
                return;
            }

            _recipes.Hide();
            ShowCard(_tooltips.Build(hovered, spell, origin));
            ShowHoverRange(hovered, spell);
        }

        /// <summary>Puts the hover card next to the cursor, clamped so it never leaves the screen.</summary>
        void ShowCard(string body)
        {
            var m = ProtoInput.MousePosition;
            float x = Mathf.Min(m.x + 18f, Screen.width - 372f);
            float y = Mathf.Max(m.y - 12f, 160f);

            _tipText.text = body;
            _tipBg.rectTransform.anchoredPosition = new Vector2(x, y);
            _tipText.rectTransform.anchoredPosition = new Vector2(x + 10f, y - 8f);
            _tipBg.enabled = true;
            _tipText.enabled = true;
        }

        void ShowHoverRange(PieceDefinition hovered, CompiledSpell spell)
        {
            if (hovered.Layer == Layer.Skill && hovered.Range > 0f)
            {
                Player.ShowRange(spell != null ? spell.Range : hovered.Range, hovered.Color);
            }
            else
            {
                Player.HideRange();
            }
        }

        CompiledSpell FindSpell(RuneInstance inst)
        {
            var spells = Book.Spells;
            for (int i = 0; i < spells.Count; i++)
            {
                if (spells[i].Source == inst) return spells[i];
            }

            return null;
        }

        void OnSpellCast(RuneInstance inst)
        {
            var spells = Book.Spells;
            for (int i = 0; i < spells.Count && i < MaxSpellRows; i++)
            {
                if (spells[i].Source != inst) continue;
                _pulse[i] = 1f;
                return;
            }
        }

        Vector2 SkillCentroid(RuneInstance inst)
        {
            Vector2 sum = Vector2.zero;
            int n = 0;

            foreach (var c in inst.Cells())
            {
                sum += CellAnchor(c.x, c.y) + new Vector2(CellSize * 0.5f, CellSize * 0.5f);
                n++;
            }

            return n == 0 ? sum : sum / n;
        }

        void DrawSkillWidgets(float dt)
        {
            var spells = Book.Spells;

            for (int i = 0; i < MaxSpellRows; i++)
            {
                bool used = i < spells.Count;
                _cdBg[i].enabled = used;
                _cdFill[i].enabled = used;

                if (!used)
                {
                    _pulse[i] = 0f;
                    continue;
                }

                var s = spells[i];
                float progress = s.Cooldown <= 0f ? 1f : 1f - Mathf.Clamp01(s.Source.CdTimer / s.Cooldown);

                _pulse[i] = Mathf.MoveTowards(_pulse[i], 0f, dt * 3.5f);
                float scale = 1f + _pulse[i] * 0.6f;

                var center = SkillCentroid(s.Source);
                var col = s.Source.Def.Color;

                _cdBg[i].rectTransform.anchoredPosition = center;
                _cdBg[i].rectTransform.localScale = Vector3.one * scale;

                _cdFill[i].rectTransform.anchoredPosition = center;
                _cdFill[i].rectTransform.localScale = Vector3.one * scale;
                _cdFill[i].fillAmount = progress;

                bool manaStarved = progress >= 1f && Player.Mana < s.Source.Def.ManaCost;
                if (manaStarved) _cdFill[i].color = new Color(0.35f, 0.55f, 1f, 0.7f);
                else if (progress >= 1f) _cdFill[i].color = new Color(col.r, col.g, col.b, 0.95f);
                else _cdFill[i].color = new Color(col.r * 0.85f, col.g * 0.85f, col.b * 0.85f, 0.55f);
            }
        }

        int DrawPanels(int cursor)
        {
            bool eventOn = ShopEventActive;
            if (!eventOn) _shopOpen = false;

            _shopBtnBg.enabled = eventOn;
            _shopBtnLabel.enabled = eventOn;
            _shopBtnLabel.text = "TOKO BUKA";

            _panelBg.enabled = _shopOpen;
            _panelTitle.enabled = _shopOpen;
            _rerollBg.enabled = _shopOpen;
            _rerollLabel.enabled = _shopOpen;

            _shopBtnBg.color = _shopOpen
                ? new Color(0.35f, 0.55f, 0.8f, 0.95f)
                : new Color(0.25f, 0.4f, 0.6f, 0.95f);

            for (int i = 0; i < ShopSlots; i++)
            {
                _shopSlotBg[i].enabled = _shopOpen;
                _shopSlotText[i].enabled = _shopOpen;
            }

            if (!_shopOpen) return cursor;

            var panel = PanelRect();
            _panelBg.rectTransform.anchoredPosition = panel.center;
            _panelTitle.rectTransform.anchoredPosition = new Vector2(panel.xMin + 14f, panel.yMax - 8f);
            _panelTitle.rectTransform.pivot = new Vector2(0f, 1f);
            _panelTitle.text = "TOKO   -   koin " + _gold + "   |   klik barang buat beli, klik di luar buat nutup";

            for (int i = 0; i < ShopSlots; i++)
            {
                var rect = ShopSlotRect(i);
                _shopSlotBg[i].rectTransform.anchoredPosition = new Vector2(rect.xMin, rect.yMin);
                _shopSlotText[i].rectTransform.anchoredPosition = new Vector2(rect.xMin + 5f, rect.yMin + 6f);

                var def = _shop[i];
                if (def == null)
                {
                    _shopSlotBg[i].color = new Color(0.1f, 0.1f, 0.12f, 0.7f);
                    _shopSlotText[i].text = "(kebeli)";
                    _shopSlotText[i].color = new Color(0.5f, 0.5f, 0.55f);
                    continue;
                }

                int price = _balance.PriceOf(def);
                bool afford = _gold >= price;

                _shopSlotBg[i].color = afford
                    ? new Color(0.15f, 0.16f, 0.22f, 0.95f)
                    : new Color(0.16f, 0.11f, 0.11f, 0.95f);

                _sb.Length = 0;
                _sb.Append(def.DisplayName).Append("  ").Append(Shapes.StarText(def.Stars)).Append('\n');
                _sb.Append(price).Append(" koin");
                _shopSlotText[i].text = _sb.ToString();
                _shopSlotText[i].color = afford ? Color.white : new Color(0.95f, 0.55f, 0.5f);

                cursor = DrawPiece(def, 0, new Vector2(rect.center.x, rect.center.y + 18f), cursor, 1f);
            }

            var reroll = RerollRect();
            _rerollBg.rectTransform.anchoredPosition = new Vector2(reroll.xMin, reroll.yMin);
            _rerollBg.color = _gold >= _rerollCost
                ? new Color(0.32f, 0.45f, 0.28f, 0.95f)
                : new Color(0.3f, 0.18f, 0.18f, 0.95f);

            _rerollLabel.rectTransform.anchoredPosition = new Vector2(reroll.xMin, reroll.yMin + 8f);
            _rerollLabel.text = "REROLL  -  " + _rerollCost + " koin  (harga naik terus)";

            return cursor;
        }

        /// <summary>How many of this piece the player owns anywhere: grid, bag, floor, or in hand.</summary>
        int OwnedCount(PieceDefinition piece)
        {
            if (piece == null) return 0;
            int n = 0;

            for (int i = 0; i < Book.Placed.Count; i++)
            {
                if (Book.Placed[i].Def == piece) n++;
            }

            for (int i = 0; i < _bag.Placed.Count; i++)
            {
                if (_bag.Placed[i].Def == piece) n++;
            }

            for (int i = 0; i < _loose.Count; i++)
            {
                if (_loose[i] == piece) n++;
            }

            if (_held == piece) n++;
            return n;
        }

        void DrawSellBox()
        {
            bool armed = _held != null && SellRect().Contains(ProtoInput.MousePosition);
            _sellBg.color = armed
                ? new Color(0.85f, 0.3f, 0.25f, 0.95f)
                : new Color(0.35f, 0.15f, 0.15f, 0.9f);

            _sellLabel.text = _held != null ? "JUAL  +" + ValueOf(_held) : "JUAL";
        }

        /// <summary>
        /// Orders the panel by damage, heaviest first, into <see cref="_spellOrder"/>.
        ///
        /// Insertion sort on purpose: the list is never longer than the number of skills that fit on
        /// a 7x7 board, and this runs every frame — <c>List.Sort</c> would allocate a comparer on a
        /// hot path to save nothing.
        /// </summary>
        int SortSpellsByDamage()
        {
            var spells = Book.Spells;
            int count = Mathf.Min(spells.Count, _spellOrder.Length);

            for (int i = 0; i < count; i++) _spellOrder[i] = i;

            for (int i = 1; i < count; i++)
            {
                int current = _spellOrder[i];
                int k = i - 1;

                while (k >= 0 && Heavier(spells[current], spells[_spellOrder[k]]))
                {
                    _spellOrder[k + 1] = _spellOrder[k];
                    k--;
                }

                _spellOrder[k + 1] = current;
            }

            return count;
        }

        /// <summary>
        /// Ranked by damage actually DEALT this run, not by the number on the card.
        ///
        /// The printed damage is a guess about a skill; the meter is what it did. A slow nuke and a
        /// fast bolt can share a damage figure and contribute wildly differently, and once the two
        /// panels merged there was no reason to keep ranking by the weaker signal.
        ///
        /// Falls back to per-cast damage until a skill has landed anything, so a freshly placed
        /// piece still sorts sensibly instead of dropping to the bottom.
        /// </summary>
        bool Heavier(CompiledSpell a, CompiledSpell b)
        {
            float dealtA = _meter.DealtBy(a.Source.Def.DisplayName);
            float dealtB = _meter.DealtBy(b.Source.Def.DisplayName);

            if (dealtA > 0f || dealtB > 0f)
            {
                if (!Mathf.Approximately(dealtA, dealtB)) return dealtA > dealtB;
            }

            if (!Mathf.Approximately(a.Damage, b.Damage)) return a.Damage > b.Damage;
            return a.Cooldown < b.Cooldown;
        }

        void DrawSpells()
        {
            var spells = Book.Spells;
            int count = SortSpellsByDamage();

            for (int i = 0; i < MaxSpellRows; i++)
            {
                bool used = i < count;
                _spellBg[i].enabled = used;
                _spellFill[i].enabled = used;
                _spellText[i].enabled = used;
                if (!used) continue;

                var s = spells[_spellOrder[i]];
                float progress = s.Cooldown <= 0f ? 1f : 1f - Mathf.Clamp01(s.Source.CdTimer / s.Cooldown);
                _spellFill[i].fillAmount = progress;
                _spellFill[i].color = new Color(s.Source.Def.Color.r, s.Source.Def.Color.g,
                    s.Source.Def.Color.b, 0.35f);

                _sb.Length = 0;
                _sb.Append(i + 1).Append(". ").Append(s.Source.Def.DisplayName);

                // Share of the run's damage, folded in from what used to be a separate meter panel.
                int share = _meter.ShareOf(s.Source.Def.DisplayName);
                if (share > 0) _sb.Append("  ").Append(share).Append('%');

                _sb.Append("   ").Append(s.Damage.ToString("0.0")).Append(" dmg");
                _sb.Append("   ").Append((s.Cooldown <= 0f ? 0f : s.Damage / s.Cooldown).ToString("0.0"))
                    .Append(" dps");
                _sb.Append("   ").Append(s.Cooldown.ToString("0.00")).Append('s');
                _sb.Append("   ").Append(Mathf.RoundToInt(s.Source.Def.ManaCost)).Append(" mana");

                if (s.DamageBonus > 0f) _sb.Append("  +").Append(Mathf.RoundToInt(s.DamageBonus * 100f)).Append("%D");
                if (s.CooldownBonus > 0f) _sb.Append("  -").Append(Mathf.RoundToInt(s.CooldownBonus * 100f)).Append("%CD");
                if (s.RadiusBonus > 0f) _sb.Append("  +").Append(Mathf.RoundToInt(s.RadiusBonus * 100f)).Append("%A");

                _spellText[i].text = _sb.ToString();
                _spellText[i].color = s.DamageBonus + s.CooldownBonus + s.RadiusBonus > 0f
                    ? new Color(1f, 0.92f, 0.55f)
                    : Color.white;
            }
        }

        /// <summary>
        /// Unscaled on purpose: at 5x speed the bars would otherwise animate too fast to read, and
        /// during the build phase time is stopped entirely but the bars still need to settle.
        /// </summary>
        void AnimateBars(float dt)
        {
            float hpTarget = Player.MaxHp <= 0f ? 0f : Mathf.Clamp01(Player.Hp / Player.MaxHp);
            float manaTarget = Player.MaxMana <= 0f ? 0f : Mathf.Clamp01(Player.Mana / Player.MaxMana);

            if (hpTarget < _hpShown - 0.0005f) _hurtFlash = 1f;

            _hpShown = Mathf.MoveTowards(_hpShown, hpTarget, dt * 3.2f);
            _manaShown = Mathf.MoveTowards(_manaShown, manaTarget, dt * 2.4f);

            // The chip only lags on the way down; healing should not leave a stale bar behind.
            _hpChipShown = _hpChipShown < _hpShown
                ? _hpShown
                : Mathf.MoveTowards(_hpChipShown, _hpShown, dt * 0.5f);

            _hpFill.fillAmount = _hpShown;
            _hpChip.fillAmount = _hpChipShown;
            _manaFill.fillAmount = _manaShown;

            _hurtFlash = Mathf.Max(0f, _hurtFlash - dt * 3f);
            _hpFill.color = Color.Lerp(HpFillColor, Color.white, _hurtFlash);

            // Below a third, the bar breathes — readable without stealing attention from the board.
            float pulse = hpTarget <= 0.33f && Player.Alive
                ? 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f)
                : 0f;
            _hpBg.color = Color.Lerp(new Color(0.16f, 0.07f, 0.08f, 0.9f),
                new Color(0.55f, 0.12f, 0.14f, 0.95f), pulse);

            // Mana reads brighter the moment it is topped up, so "ready to cast" is visible.
            _manaFill.color = Color.Lerp(ManaFillColor, new Color(0.62f, 0.85f, 1f, 0.98f),
                Mathf.InverseLerp(0.9f, 1f, _manaShown));
        }

        void DrawHud()
        {
            _sb.Length = 0;
            _sb.Append("WAVE ").Append(Enemies.Wave);

            if (Enemies.WaveActive)
            {
                _sb.Append(Enemies.Closing
                    ? "    HABISKAN SISANYA"
                    : "    sisa " + (Enemies.PendingSpawns + Enemies.AliveCount) + "/" + Enemies.WaveTotal);
            }

            _sb.Append("    musuh ").Append(Enemies.AliveCount);
            _sb.Append("    kills ").Append(Enemies.Kills);
            _sb.Append("    koin ").Append(_gold);
            _hudText.text = _sb.ToString();

            AnimateBars(Time.unscaledDeltaTime);

            _hpLabel.text = "HP  " + Mathf.CeilToInt(Player.Hp) + " / " + Mathf.RoundToInt(Player.MaxHp) +
                            (Player.HpRegen > 0f ? "   (+" + Player.HpRegen.ToString("0.0") + "/s)" : "");

            _manaLabel.text = "MANA  " + Mathf.FloorToInt(Player.Mana) + " / " + Mathf.RoundToInt(Player.MaxMana) +
                              "   (+" + Player.ManaRegen.ToString("0.0") + "/s)";

            // Ailment tally moved to its own icon strip under the mana bar — see DrawBuffs.
        }

        /// <summary>
        /// Pengumuman sekali lewat, memakai floater yang sama dengan reaksi. Widget baru untuk tiap
        /// kabar penting hanya menambah satu lagi tempat yang harus dipelajari pemain.
        /// </summary>
        public void Announce(string message, Color color, Vector3? at = null)
        {
            PushFloater(at ?? Player.transform.position + Vector3.up * 3f, message, color);
        }

        void PushFloater(Vector3 world, string message, Color color)
        {
            for (int i = 0; i < FloatPoolSize; i++)
            {
                if (_floatLife[i] > 0f) continue;

                _floatLife[i] = 1.1f;
                _floatWorld[i] = world;
                _floaters[i].text = message;
                _floaters[i].color = color;
                return;
            }
        }

        void TickFloaters(float dt)
        {
            for (int i = 0; i < FloatPoolSize; i++)
            {
                if (_floatLife[i] <= 0f)
                {
                    if (_floaters[i].text.Length > 0) _floaters[i].text = "";
                    continue;
                }

                _floatLife[i] -= dt;
                _floatWorld[i] += Vector3.up * (1.4f * dt);

                var screen = _camera.WorldToScreenPoint(_floatWorld[i]);
                _floaters[i].rectTransform.anchoredPosition = new Vector2(screen.x, screen.y);

                var c = _floaters[i].color;
                c.a = Mathf.Clamp01(_floatLife[i]);
                _floaters[i].color = c;
            }
        }
    }
}
