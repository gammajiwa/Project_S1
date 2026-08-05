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

        const int EvoLinePool = 8;
        

        // Same blue and gold as before, but they fill an area now instead of drawing a bar, so the
        // alpha has to stay low enough to read the pieces underneath.
        static readonly Color AreaIncomplete = new Color(0.35f, 0.62f, 1f, 0.26f);
        static readonly Color AreaComplete = new Color(1f, 0.85f, 0.3f, 0.34f);

        // Browsing the codex belongs to the main menu now. A run only ever writes to it.
        DiscoveryLog _codex;

        bool _shopOpen;
        int _rerollCost;
        readonly PieceDefinition[] _shop = new PieceDefinition[ShopSlots];

        Image[] _evoLines;
        List<EvoPreview> _previews = new List<EvoPreview>();
        float _previewTimer;

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
        int _gold;

        // Swallows any stray click/keypress carried in from entering play mode.
        float _inputLock = 0.4f;

        Image[] _spellBg;
        Image[] _spellFill;
        Text[] _spellText;

        Image[] _speedButtons;
        Text[] _speedLabels;
        int _speedSlot;

        // --- damage meter ---
        readonly DamageMeter _meter = new DamageMeter();
        Text _meterText;
        float _meterTimer;

        Text _buffText;

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
        Text _statusText;
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

        readonly StringBuilder _sb = new StringBuilder(256);

        public void Init(PlayerCaster player, EnemyManager enemies, Camera cam,
            ContentDatabase database, GameBalance balance)
        {
            Player = player;
            Enemies = enemies;
            _camera = cam;
            _db = database;
            _balance = balance;
            _tooltips = new TooltipBuilder(database, balance, OwnedCount);
            _rerollCost = balance.RerollCostStart;

            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (_font == null) _font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            BuildCanvas();
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
            BuildMeter();
            BuildFloaters();

            Enemies.OnWaveCleared += OnWaveCleared;
            Enemies.OnKill += OnEnemyKilled;
            Enemies.OnDamage += _meter.Record;
            Player.OnCast += OnSpellCast;
            Enemies.OnReaction += (pos, rx) => PushFloater(pos, rx.DisplayName + "!", rx.FlashColor);

            // Opening loadout sits in the middle of the grid, not the corner.
            var mid = new Vector2Int(Grimoire.Width / 2 - 1, Grimoire.Height / 2 - 1);
            Book.Place(_db.ById("emberrune"), mid, 0);
            Book.Place(_db.ById("fireball"), mid, 0);
            // Two pieces already scattered so the pick-up flow is obvious from the first second.
            AddLoose(_db.ById("frostnova"));
            AddLoose(_db.ById("chronorune"));

            for (int i = 0; i < Book.Placed.Count; i++) Discover(Book.Placed[i].Def);

            SetSpeed(0);
            Redraw();
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

            _heldText = MakeText("HeldInfo", new Vector2(Margin, -100),
                new Vector2(880, 22), 13, new Color(0.85f, 0.85f, 0.6f), new Vector2(0f, 1f),
                TextAnchor.UpperLeft);

            _evolveText = MakeText("EvolveInfo", new Vector2(Margin, -122),
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
                float y = Margin + i * 44;
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
                new Vector2(1f, 0f), TextAnchor.LowerRight).text = "SPELL AKTIF  (buff dari rune di bawahnya)";
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

            _statusText = MakeText("Status", new Vector2(0, -Margin), new Vector2(700, 40), 17,
                Color.white, new Vector2(0.5f, 1f), TextAnchor.UpperCenter);

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
            _meterText = MakeText("Meter", new Vector2(-Margin, Margin + MaxSpellRows * 44 + 34),
                new Vector2(400, 150), 13, new Color(0.85f, 0.85f, 0.9f),
                new Vector2(1f, 0f), TextAnchor.LowerRight);

            _buffText = MakeText("Buffs", new Vector2(Margin, -160), new Vector2(700, 24), 15,
                new Color(1f, 0.92f, 0.55f), new Vector2(0f, 1f), TextAnchor.UpperLeft);
            _buffText.text = "";
        }

        void DrawMeter()
        {
            // Throttled: the numbers move constantly but nobody reads them four times a frame.
            _meterTimer -= Time.unscaledDeltaTime;
            if (_meterTimer > 0f) return;

            _meterTimer = 0.25f;
            _meterText.text = _meter.BuildSummary(6);
        }

        void DrawBuffs()
        {
            var buffs = Player.Buffs;
            _sb.Length = 0;

            for (int i = 0; i < buffs.Length; i++)
            {
                if (buffs[i].Def == null) continue;

                if (_sb.Length > 0) _sb.Append("   ");
                _sb.Append(buffs[i].Def.DisplayName)
                    .Append(' ').Append(buffs[i].Remaining.ToString("0.0")).Append('s');
            }

            _buffText.text = _sb.Length > 0 ? "BUFF:  " + _sb : "";
        }

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

        /// <summary>Every drop lands loose. The player decides where it actually goes.</summary>
        void RouteDrop(PieceDefinition def, Vector3 at)
        {
            AddLoose(def);
            PushFloater(at, def.DisplayName, def.Color);
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

        bool CanStartWave() =>
            Player.Alive && !Enemies.WaveActive && Book.Spells.Count > 0;

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
            if (!Player.Alive || _inputLock > 0f) return;

            // Grimoire is locked while a wave runs â€” you only watch.
            if (Enemies.WaveActive)
            {
                if (_held != null) StashHeld();
                return;
            }

            Vector2 mouse = ProtoInput.MousePosition;

            if (ProtoInput.RightClickDown || ProtoInput.RotateDown)
            {
                if (_held != null)
                {
                    _heldRot++;
                    return;
                }

                // Right-click on a placed piece toggles its evolution lock.
                var lockCell = ScreenToCell(mouse);
                if (lockCell.x >= 0)
                {
                    var target = Book.SkillAt(lockCell) ?? Book.BaseAt(lockCell);
                    if (target != null) target.Locked = !target.Locked;
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

                    if (_bag.Place(_held, bagOrigin, _heldRot) != null)
                    {
                        _held = null;
                    }
                    else if (_bag.CanReplaceAt(_held, bagOrigin, _heldRot))
                    {
                        ScatterAll(_bag.ClearFootprint(_held, bagOrigin, _heldRot), mouse);
                        if (_bag.Place(_held, bagOrigin, _heldRot) != null) _held = null;
                    }

                    return;
                }

                var target = ScreenToCell(mouse);
                if (target.x >= 0)
                {
                    var gridOrigin = target - AnchorOffset(_held, _heldRot);

                    if (Book.Place(_held, gridOrigin, _heldRot) != null)
                    {
                        _held = null;
                    }
                    else if (Book.CanReplaceAt(_held, gridOrigin, _heldRot))
                    {
                        // Occupied â€” kick the old piece out and take its spot.
                        ScatterAll(Book.ClearFootprint(_held, gridOrigin, _heldRot), mouse);
                        if (Book.Place(_held, gridOrigin, _heldRot) != null) _held = null;
                    }

                    return;
                }

                // Clicked empty space â€” drop it right there.
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

            ScatterAll(Book.Remove(inst), mouse);
        }

        /// <summary>Shop / recipe panel clicks. Returns true when the click was consumed.</summary>
        bool ShopEventActive =>
            Player.Alive && !Enemies.WaveActive && Enemies.Wave > 0 && Enemies.Wave % _balance.ShopEveryWaves == 0;

        bool HandlePanelClick(Vector2 mouse)
        {
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

        /// <summary>Puts the held piece down on the floor â€” nothing is ever lost silently.</summary>
        void StashHeld()
        {
            if (_held == null) return;

            AddLoose(_held);
            _held = null;
        }

        void SellHeld()
        {
            if (_held == null) return;

            int value = ValueOf(_held);
            _gold += value;
            PushFloater(Player.transform.position + Vector3.up * 2f,
                _held.DisplayName + " kejual  +" + value, new Color(1f, 0.88f, 0.45f));
            _held = null;
        }

        void OnEnemyKilled(Vector3 at)
        {
            if (Random.value > _balance.KillDropChance) return;
            RouteDrop(_db.RandomDrop(_balance.RuneShareOfDrops), at);
        }

        void OnWaveCleared()
        {
            Player.Hp = Mathf.Min(Player.MaxHp, Player.Hp + _balance.HealPerWaveClear);

            // Shop is an event: it only shows up every few waves, with fresh stock.
            if (Enemies.Wave % _balance.ShopEveryWaves == 0) RollShop();

            for (int i = 0; i < _balance.WaveClearDrops; i++)
            {
                RouteDrop(_db.RandomDrop(_balance.RuneShareOfDrops), Player.transform.position + Vector3.up * 2f);
            }

            var evolutions = Book.ResolveEvolutions();
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
            }

            for (int i = 0; i < EvoLinePool; i++)
            {
                bool used = i < _previews.Count;
                _evoLines[i].enabled = used;
                if (!used) continue;

                var p = _previews[i];

                // Groups only have to touch now, not line up, so a bar between two ends would lie
                // about which cells are involved. Highlight the area they cover instead.
                var min = CellAnchor(p.From.x, p.From.y);
                var max = CellAnchor(p.To.x, p.To.y) + new Vector2(CellSize, CellSize);

                _evoLines[i].rectTransform.anchoredPosition = (min + max) * 0.5f;
                _evoLines[i].rectTransform.sizeDelta = (max - min) + new Vector2(6f, 6f);
                _evoLines[i].color = p.Complete ? AreaComplete : AreaIncomplete;
            }
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
            DrawMeter();
            DrawBuffs();
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

            if (_held != null)
            {
                // Carrying something â€” the card would just sit in the way.
                _tipBg.enabled = false;
                _tipText.enabled = false;
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
                Player.HideRange();
                return;
            }

            _tipText.text = ProtoInput.AltHeld
                ? _tooltips.BuildRecipeCard(hovered)
                : _tooltips.Build(hovered, spell, origin);

            var m = ProtoInput.MousePosition;
            float x = Mathf.Min(m.x + 18f, Screen.width - 372f);
            float y = Mathf.Max(m.y - 12f, 160f);

            _tipBg.rectTransform.anchoredPosition = new Vector2(x, y);
            _tipText.rectTransform.anchoredPosition = new Vector2(x + 10f, y - 8f);
            _tipBg.enabled = true;
            _tipText.enabled = true;

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

        void DrawSpells()
        {
            var spells = Book.Spells;

            for (int i = 0; i < MaxSpellRows; i++)
            {
                bool used = i < spells.Count;
                _spellBg[i].enabled = used;
                _spellFill[i].enabled = used;
                _spellText[i].enabled = used;
                if (!used) continue;

                var s = spells[i];
                float progress = s.Cooldown <= 0f ? 1f : 1f - Mathf.Clamp01(s.Source.CdTimer / s.Cooldown);
                _spellFill[i].fillAmount = progress;
                _spellFill[i].color = new Color(s.Source.Def.Color.r, s.Source.Def.Color.g,
                    s.Source.Def.Color.b, 0.35f);

                _sb.Length = 0;
                _sb.Append(s.Source.Def.DisplayName);
                _sb.Append("  dmg ").Append(s.Damage.ToString("0.0"));
                _sb.Append("  cd ").Append(s.Cooldown.ToString("0.00")).Append('s');
                _sb.Append("  mana ").Append(Mathf.RoundToInt(s.Source.Def.ManaCost));

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
            _sb.Append("    musuh ").Append(Enemies.AliveCount);
            _sb.Append(" (sisa ").Append(Enemies.PendingSpawns).Append(')');
            _sb.Append("    kills ").Append(Enemies.Kills);
            _sb.Append("    koin ").Append(_gold);
            _hudText.text = _sb.ToString();

            AnimateBars(Time.unscaledDeltaTime);

            _hpLabel.text = "HP  " + Mathf.CeilToInt(Player.Hp) + " / " + Mathf.RoundToInt(Player.MaxHp) +
                            (Player.HpRegen > 0f ? "   (+" + Player.HpRegen.ToString("0.0") + "/s)" : "");

            _manaLabel.text = "MANA  " + Mathf.FloorToInt(Player.Mana) + " / " + Mathf.RoundToInt(Player.MaxMana) +
                              "   (+" + Player.ManaRegen.ToString("0.0") + "/s)";

            _sb.Length = 0;
            _sb.Append("<ailment di musuh>");

            var counts = Enemies.StatusCounts;
            for (int i = 0; i < _db.Statuses.Count && i < counts.Length; i++)
            {
                var status = _db.Statuses[i];
                if (status == null) continue;

                _sb.Append("   ").Append(status.DisplayName).Append(' ').Append(counts[i]);
            }

            _statusText.text = _sb.ToString();
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
