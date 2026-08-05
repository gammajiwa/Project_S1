using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace Proto
{
    /// <summary>
    /// All prototype UI: the two-layer grimoire (grid snapping, lines hidden), the skill backpack,
    /// the sell box, per-spell cooldown dials, the buff panel, wave banner, speed control and
    /// floating combat text. Built entirely from code â€” prototype, no prefabs.
    /// </summary>
    public class GrimoireUI : MonoBehaviour
    {
        // grimoire
        const int CellSize = 40;
        const int CellGap = 3;
        const int Margin = 20;
        const int SkillInset = 8;

        // right-hand column
        const int BagCell = 34;
        const int BagGap = 3;
        const int BagY = 20;

        const int SellW = 182;
        const int SellH = 40;
        const int SellY = 190;

        const int MaxSpellRows = 8;
        const int SpellPanelW = 300;
        const int FloatPoolSize = 16;
        const int CooldownDiameter = 26;

        const int SpeedButtonW = 58;
        const int SpeedButtonH = 34;

        const int StartButtonW = 280;
        const int StartButtonH = 56;

        const int LoosePoolSize = 24;

        // Loose pieces are drawn at the exact grid scale so nothing changes size when placed.
        const int LooseCellSize = CellSize;
        const int LooseCellGap = CellGap;
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
        const int ShopSlotW = 196;
        const int ShopSlotH = 148;
        const int PanelW = 632;
        const int PanelH = 372;

        const int EvoLinePool = 8;
        

        static readonly Color LineIncomplete = new Color(0.35f, 0.62f, 1f, 0.95f);
        static readonly Color LineComplete = new Color(1f, 0.85f, 0.3f, 1f);

        // Browsing the codex belongs to the main menu now. A run only ever writes to it.
        DiscoveryLog _codex;

        bool _shopOpen;
        int _rerollCost;
        readonly PieceDefinition[] _shop = new PieceDefinition[ShopSlots];

        Image[] _evoLines;
        List<EvoPreview> _previews = new List<EvoPreview>();
        float _previewTimer;

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
        readonly List<string> _meterNames = new List<string>(12);
        readonly List<float> _meterValues = new List<float>(12);
        float _meterTotal;
        Text _meterText;
        float _meterTimer;

        Text _buffText;

        Text _hudText;
        Image _hpBg;
        Image _hpFill;
        Text _hpLabel;
        Image _manaBg;
        Image _manaFill;
        Text _manaLabel;
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
            Enemies.OnDamage += RecordDamage;
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

        static void ShapeBounds(Vector2Int[] shape, out int w, out int h)
        {
            int maxX = 0, maxY = 0;
            for (int i = 0; i < shape.Length; i++)
            {
                if (shape[i].x > maxX) maxX = shape[i].x;
                if (shape[i].y > maxY) maxY = shape[i].y;
            }

            w = maxX + 1;
            h = maxY + 1;
        }

        /// <summary>
        /// The cell inside a shape that sits under the cursor. Placement and the on-cursor preview
        /// both use this, otherwise the ghost and the real footprint drift apart.
        /// </summary>
        static Vector2Int AnchorOffset(PieceDefinition def, int rot)
        {
            ShapeBounds(Shapes.Rotate(def.Cells, rot), out int w, out int h);
            return new Vector2Int((w - 1) / 2, (h - 1) / 2);
        }

        static Vector2 PieceSize(Vector2Int[] shape)
        {
            ShapeBounds(shape, out int w, out int h);
            float step = LooseCellSize + LooseCellGap;
            return new Vector2(w * step - LooseCellGap, h * step - LooseCellGap);
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

        static Rect PanelRect()
        {
            return new Rect((Screen.width - PanelW) * 0.5f, (Screen.height - PanelH) * 0.5f, PanelW, PanelH);
        }

        static Rect ShopSlotRect(int i)
        {
            var p = PanelRect();
            int col = i % 3;
            int row = i / 3;

            float x = p.xMin + 12f + col * (ShopSlotW + 8f);
            float y = p.yMax - 46f - (row + 1) * ShopSlotH - row * 8f;
            return new Rect(x, y, ShopSlotW, ShopSlotH);
        }

        static Rect RerollRect()
        {
            var p = PanelRect();
            return new Rect(p.center.x - 120f, p.yMin + 12f, 240f, 34f);
        }

        static Rect ShopButtonRect() => new Rect(RightX(), SellY + SellH + 8, 88, 32);

        static Rect RecipeButtonRect() => new Rect(RightX() + 94, SellY + SellH + 8, 88, 32);

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
            _hpFill = MakeImage("HpFill", new Vector2(Margin, -50), new Vector2(260, 18),
                new Color(0.85f, 0.28f, 0.3f, 0.95f), new Vector2(0f, 1f));
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

        /// <summary>Damage per source across the whole run — the only way to prove balance.</summary>
        void RecordDamage(string source, float amount)
        {
            if (string.IsNullOrEmpty(source) || amount <= 0f) return;

            _meterTotal += amount;

            for (int i = 0; i < _meterNames.Count; i++)
            {
                if (_meterNames[i] != source) continue;
                _meterValues[i] += amount;
                return;
            }

            _meterNames.Add(source);
            _meterValues.Add(amount);
        }

        void DrawMeter()
        {
            _meterTimer -= Time.unscaledDeltaTime;
            if (_meterTimer > 0f) return;
            _meterTimer = 0.25f;

            if (_meterTotal <= 0f)
            {
                _meterText.text = "";
                return;
            }

            _sb.Length = 0;
            _sb.Append("DAMAGE  (total ").Append(Mathf.RoundToInt(_meterTotal)).Append(")\n");

            // Selection sort over a handful of entries — cheaper than allocating a sorted list.
            for (int rank = 0; rank < 6; rank++)
            {
                int best = -1;
                float bestValue = 0f;

                for (int i = 0; i < _meterValues.Count; i++)
                {
                    if (_meterValues[i] <= bestValue) continue;
                    if (RankOf(i) < rank) continue;

                    bestValue = _meterValues[i];
                    best = i;
                }

                if (best < 0) break;

                int pct = Mathf.RoundToInt(_meterValues[best] / _meterTotal * 100f);
                _sb.Append(_meterNames[best]).Append("  ").Append(pct).Append("%\n");
                _meterRank[best] = rank;
            }

            _meterText.text = _sb.ToString();
        }

        readonly Dictionary<int, int> _meterRank = new Dictionary<int, int>();

        int RankOf(int index) => _meterRank.TryGetValue(index, out int r) ? r : int.MaxValue;

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

        // ---------- layout helpers ----------

        static Vector2 CellAnchor(int x, int y) =>
            new Vector2(Margin + x * (CellSize + CellGap), Margin + y * (CellSize + CellGap));

        static float GridTop() => Margin + Grimoire.Height * (CellSize + CellGap);

        static float RightX() => Margin + Grimoire.Width * (CellSize + CellGap) + 12;

        static Vector2 BagAnchor(int x, int y) =>
            new Vector2(RightX() + x * (BagCell + BagGap), BagY + y * (BagCell + BagGap));

        static Rect SellRect() => new Rect(RightX(), SellY, SellW, SellH);

        static Vector2Int ScreenToCell(Vector2 mouse)
        {
            float step = CellSize + CellGap;
            int x = Mathf.FloorToInt((mouse.x - Margin) / step);
            int y = Mathf.FloorToInt((mouse.y - Margin) / step);

            if (x < 0 || x >= Grimoire.Width || y < 0 || y >= Grimoire.Height) return new Vector2Int(-1, -1);

            float offX = (mouse.x - Margin) - x * step;
            float offY = (mouse.y - Margin) - y * step;
            if (offX > CellSize || offY > CellSize) return new Vector2Int(-1, -1);

            return new Vector2Int(x, y);
        }

        static Vector2Int ScreenToBagCell(Vector2 mouse)
        {
            float step = BagCell + BagGap;
            int x = Mathf.FloorToInt((mouse.x - RightX()) / step);
            int y = Mathf.FloorToInt((mouse.y - BagY) / step);

            if (x < 0 || x >= Backpack.Width || y < 0 || y >= Backpack.Height) return new Vector2Int(-1, -1);

            float offX = (mouse.x - RightX()) - x * step;
            float offY = (mouse.y - BagY) - y * step;
            if (offX > BagCell || offY > BagCell) return new Vector2Int(-1, -1);

            return new Vector2Int(x, y);
        }

        static Rect SpeedRect(int i)
        {
            float right = Screen.width - (Margin + (Speeds.Length - 1 - i) * (SpeedButtonW + 6));
            float top = Screen.height - Margin;
            return new Rect(right - SpeedButtonW, top - SpeedButtonH, SpeedButtonW, SpeedButtonH);
        }

        static Rect StartButtonRect()
        {
            float cx = Screen.width * 0.5f;
            float cy = Screen.height * 0.5f + 120f;
            return new Rect(cx - StartButtonW * 0.5f, cy - StartButtonH * 0.5f, StartButtonW, StartButtonH);
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

        /// <summary>Drops scatter anywhere on screen, clear of the left column and the HUD strip.</summary>
        static Vector2 RandomScatterPos()
        {
            float left = RightX() + 60f;
            float right = Mathf.Max(left + 160f, Screen.width - 80f);
            float bottom = 70f;
            float top = Mathf.Max(bottom + 120f, Screen.height - 200f);

            return new Vector2(Random.Range(left, right), Random.Range(bottom, top));
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

        static Vector2 NearScatterPos(Vector2 near, int index)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f) + index * 1.1f;
            float dist = Random.Range(70f, 120f);

            var pos = near + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * dist;
            pos.x = Mathf.Clamp(pos.x, 70f, Mathf.Max(90f, Screen.width - 70f));
            pos.y = Mathf.Clamp(pos.y, 70f, Mathf.Max(90f, Screen.height - 190f));
            return pos;
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
                if (SpeedRect(i).Contains(mouse))
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
            _previewTimer -= Time.unscaledDeltaTime;
            if (_previewTimer <= 0f)
            {
                _previewTimer = 0.25f;
                _previews = Book.FindPendingGroups();
            }

            for (int i = 0; i < EvoLinePool; i++)
            {
                bool used = i < _previews.Count;
                _evoLines[i].enabled = used;
                if (!used) continue;

                var p = _previews[i];
                var a = CellAnchor(p.From.x, p.From.y) + new Vector2(CellSize * 0.5f, CellSize * 0.5f);
                var b = CellAnchor(p.To.x, p.To.y) + new Vector2(CellSize * 0.5f, CellSize * 0.5f);

                bool horizontal = Mathf.Abs(b.x - a.x) >= Mathf.Abs(b.y - a.y);
                float length = horizontal ? b.x - a.x : b.y - a.y;

                _evoLines[i].rectTransform.anchoredPosition = (a + b) * 0.5f;
                _evoLines[i].rectTransform.sizeDelta = horizontal
                    ? new Vector2(length + 6f, 5f)
                    : new Vector2(5f, length + 6f);
                _evoLines[i].color = p.Complete ? LineComplete : LineIncomplete;
            }
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
                ? BuildRecipeCard(hovered)
                : BuildTooltip(hovered, spell, origin);

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

        static string KindName(CastKind kind)
        {
            switch (kind)
            {
                case CastKind.Projectile: return "proyektil";
                case CastKind.Nova: return "ledakan melingkar";
                case CastKind.Chain: return "sambaran beruntun";
                case CastKind.Heal: return "penyembuh";
                default: return "alas";
            }
        }

        string BuildTooltip(PieceDefinition def, CompiledSpell spell, string origin)
        {
            _sb.Length = 0;
            _sb.Append(def.DisplayName).Append("  ").Append(Shapes.StarText(def.Stars));
            _sb.Append("   [").Append(origin).Append("]\n");

            if (def.Layer == Layer.Rune)
            {
                int cells = Mathf.Max(1, def.Cells.Length);
                int perCell = Mathf.RoundToInt(def.AuraValue / cells * 100f);

                _sb.Append("RUNE - alas, ").Append(cells).Append(" petak, elemen ")
                    .Append(def.Element).Append('\n');

                switch (def.Aura)
                {
                    case AuraKind.DamagePct:
                        _sb.Append("+").Append(Mathf.RoundToInt(def.AuraValue * 100f))
                            .Append("% damage TOTAL  (").Append(perCell).Append("% per petak)\n");
                        break;
                    case AuraKind.CooldownPct:
                        _sb.Append("-").Append(Mathf.RoundToInt(def.AuraValue * 100f))
                            .Append("% cooldown TOTAL  (").Append(perCell).Append("% per petak)\n");
                        break;
                    case AuraKind.RadiusPct:
                        _sb.Append("+").Append(Mathf.RoundToInt(def.AuraValue * 100f))
                            .Append("% area TOTAL  (").Append(perCell).Append("% per petak)\n");
                        break;
                    default:
                        _sb.Append("alas polos, nggak ngasih aura\n");
                        break;
                }

                if (def.ElementMatchBonus > 0f)
                {
                    _sb.Append("skill ber-elemen ").Append(def.Element).Append(" di atasnya: +")
                        .Append(Mathf.RoundToInt(def.ElementMatchBonus * 100f))
                        .Append("% damage TOTAL\n");
                }

                if (def.Stats != null && def.Stats.Length > 0)
                {
                    _sb.Append("stat: ");
                    for (int s = 0; s < def.Stats.Length; s++)
                    {
                        if (s > 0) _sb.Append(", ");
                        _sb.Append(def.Stats[s].Type).Append(' ').Append(def.Stats[s].Value.ToString("0.##"));
                    }

                    _sb.Append('\n');
                }
            }
            else if (def.Kind == CastKind.Passive)
            {
                _sb.Append("SEGEL pasif - ").Append(def.Cells.Length).Append(" petak, nggak nembak\n");
                switch (def.Stat)
                {
                    case StatKind.MaxHp: _sb.Append("+").Append(def.StatValue.ToString("0")).Append(" HP maksimum\n"); break;
                    case StatKind.MaxMana: _sb.Append("+").Append(def.StatValue.ToString("0")).Append(" mana maksimum\n"); break;
                    case StatKind.ManaRegen: _sb.Append("+").Append(def.StatValue.ToString("0.0")).Append(" mana / detik\n"); break;
                    case StatKind.HpRegen: _sb.Append("+").Append(def.StatValue.ToString("0.0")).Append(" HP / detik\n"); break;
                    case StatKind.FireDamagePct: _sb.Append("+").Append(Mathf.RoundToInt(def.StatValue * 100f)).Append("% damage skill API\n"); break;
                    case StatKind.IceDamagePct: _sb.Append("+").Append(Mathf.RoundToInt(def.StatValue * 100f)).Append("% damage skill ES\n"); break;
                    case StatKind.LightningDamagePct: _sb.Append("+").Append(Mathf.RoundToInt(def.StatValue * 100f)).Append("% damage skill PETIR\n"); break;
                }

                _sb.Append(spell == null && origin == "KEPASANG" ? "aktif\n" : "harus berdiri di atas rune biar aktif\n");
            }
            else
            {
                float dmg = spell != null ? spell.Damage : def.BaseDamage;
                float cd = spell != null ? spell.Cooldown : def.BaseCooldown;
                float range = spell != null ? spell.Range : def.Range;
                float radius = spell != null ? spell.Radius : def.Radius;

                _sb.Append(KindName(def.Kind)).Append(" - ").Append(def.Cells.Length).Append(" petak\n");
                _sb.Append(def.Kind == CastKind.Heal ? "heal " : "damage ").Append(dmg.ToString("0.0"));
                _sb.Append("     cooldown ").Append(cd.ToString("0.00")).Append('s');
                _sb.Append("     mana ").Append(Mathf.RoundToInt(def.ManaCost)).Append('\n');

                if (range > 0f) _sb.Append("jangkauan ").Append(range.ToString("0.0"));
                if (def.Kind == CastKind.Nova) _sb.Append("     radius ledak ").Append(radius.ToString("0.0"));
                if (def.Hits > 1) _sb.Append("     target ").Append(def.Hits);
                if (range > 0f || def.Hits > 1) _sb.Append('\n');

                if (def.AppliedStatus != null)
                {
                    _sb.Append("nempel ").Append(def.AppliedStatus.DisplayName);
                    if (def.AppliedPoints > 1) _sb.Append(" ").Append(def.AppliedPoints).Append(" poin");
                    _sb.Append(' ').Append(def.StatusDuration.ToString("0.0")).Append("s\n");
                }

                if (spell != null)
                {
                    if (spell.DamageBonus > 0f || spell.CooldownBonus > 0f || spell.RadiusBonus > 0f)
                    {
                        _sb.Append("dari rune di bawah:");
                        if (spell.DamageBonus > 0f)
                            _sb.Append("  +").Append(Mathf.RoundToInt(spell.DamageBonus * 100f)).Append("% DMG");
                        if (spell.CooldownBonus > 0f)
                            _sb.Append("  -").Append(Mathf.RoundToInt(spell.CooldownBonus * 100f)).Append("% CD");
                        if (spell.RadiusBonus > 0f)
                            _sb.Append("  +").Append(Mathf.RoundToInt(spell.RadiusBonus * 100f)).Append("% AREA");
                        _sb.Append('\n');
                    }
                    else
                    {
                        _sb.Append("rune di bawahnya nggak ngasih buff\n");
                    }
                }
                else
                {
                    _sb.Append("(angka dasar - belum kepasang di atas rune)\n");
                }
            }

            _sb.Append("harga jual ").Append(ValueOf(def)).Append(" koin\n");
            _sb.Append(def.Blurb);
            return _sb.ToString();
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

        /// <summary>ALT + hover: every recipe this piece appears in, with a tick per owned part.</summary>
        string BuildRecipeCard(PieceDefinition def)
        {
            _sb.Length = 0;
            _sb.Append("RESEP yang pakai ").Append(def.DisplayName).Append('\n');

            int found = 0;

            for (int i = 0; i < _db.Recipes.Count; i++)
            {
                var r = _db.Recipes[i];

                bool uses = false;
                for (int k = 0; k < r.Ingredients.Length; k++)
                {
                    if (r.Ingredients[k] != def) continue;
                    uses = true;
                    break;
                }

                if (!uses) continue;

                var result = r.Result;
                if (result == null) continue;

                found++;

                // Tick each ingredient against what is owned, counting duplicates separately.
                var seen = new Dictionary<PieceDefinition, int>();

                for (int k = 0; k < r.Ingredients.Length; k++)
                {
                    var ing = r.Ingredients[k];
                    if (ing == null) continue;

                    seen.TryGetValue(ing, out int used);
                    seen[ing] = used + 1;

                    bool have = OwnedCount(ing) >= used + 1;

                    if (k > 0) _sb.Append("  +  ");
                    _sb.Append(have ? "[v] " : "[ ] ").Append(ing.DisplayName);
                }

                _sb.Append("\n        =  ").Append(result.DisplayName)
                    .Append("  ").Append(Shapes.StarText(result.Stars)).Append('\n');
            }

            if (found == 0) _sb.Append("(belum ada resep yang pakai ini)\n");

            _sb.Append("\ntaruh bahannya SEGARIS & bersebelahan di grimoire.\n");
            _sb.Append("garis biru = belum lengkap, garis emas = jadi evo pas wave beres.");
            return _sb.ToString();
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

        void DrawHud()
        {
            _sb.Length = 0;
            _sb.Append("WAVE ").Append(Enemies.Wave);
            _sb.Append("    musuh ").Append(Enemies.AliveCount);
            _sb.Append(" (sisa ").Append(Enemies.PendingSpawns).Append(')');
            _sb.Append("    kills ").Append(Enemies.Kills);
            _sb.Append("    koin ").Append(_gold);
            _hudText.text = _sb.ToString();

            _hpFill.fillAmount = Player.MaxHp <= 0f ? 0f : Mathf.Clamp01(Player.Hp / Player.MaxHp);
            _hpLabel.text = "HP  " + Mathf.CeilToInt(Player.Hp) + " / " + Mathf.RoundToInt(Player.MaxHp) +
                            (Player.HpRegen > 0f ? "   (+" + Player.HpRegen.ToString("0.0") + "/s)" : "");

            _manaFill.fillAmount = Player.MaxMana <= 0f ? 0f : Mathf.Clamp01(Player.Mana / Player.MaxMana);
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
