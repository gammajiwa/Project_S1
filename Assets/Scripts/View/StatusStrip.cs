using UnityEngine;
using UnityEngine.UI;

namespace Proto
{
    /// <summary>
    /// A row of icons with a number beside each, and a description on hover.
    ///
    /// Replaces three separate lines of running text — the player buffs, the curses on the player,
    /// and the ailment tally across the swarm. Text was the wrong shape for all three: it reflows
    /// every time an entry appears or leaves, so nothing sits still long enough to be recognised,
    /// and by the time you have read "WEAKENED 3.4s" the wave has moved on.
    ///
    /// Icons hold position, so the eye learns where each one lives. The number carries whatever
    /// counts for that strip — seconds left for a buff, enemies afflicted for an ailment.
    /// </summary>
    public class StatusStrip
    {
        readonly Image[] _icons;
        readonly Image[] _frames;
        readonly Text[] _numbers;
        readonly string[] _tooltips;
        readonly Rect[] _rects;

        readonly float _slot;
        readonly float _icon;
        Vector2 _origin;
        int _used;

        public StatusStrip(Transform canvas, Font font, int capacity, float iconSize, Color numberColor)
        {
            _icon = iconSize;
            _slot = iconSize + 26f;

            _icons = new Image[capacity];
            _frames = new Image[capacity];
            _numbers = new Text[capacity];
            _tooltips = new string[capacity];
            _rects = new Rect[capacity];

            for (int i = 0; i < capacity; i++)
            {
                // Frame first: creation order is draw order, so it has to exist before the icon.
                _frames[i] = MakeImage(canvas, $"StripFrame_{i}", iconSize + 4f);
                _icons[i] = MakeImage(canvas, $"StripIcon_{i}", iconSize);

                _numbers[i] = MakeText(canvas, font, $"StripNum_{i}", numberColor);
            }

            Hide();
        }

        static Image MakeImage(Transform canvas, string name, float size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(canvas, false);

            var img = go.AddComponent<Image>();
            img.raycastTarget = false;

            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(size, size);
            return img;
        }

        static Text MakeText(Transform canvas, Font font, string name, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(canvas, false);

            var text = go.AddComponent<Text>();
            text.font = font;
            text.fontSize = 13;
            text.fontStyle = FontStyle.Bold;
            text.color = color;
            text.alignment = TextAnchor.MiddleLeft;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            var rt = text.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(40f, 18f);
            return text;
        }

        /// <param name="origin">Top-left corner of the strip, in canvas pixels from the top-left.</param>
        public void Begin(Vector2 origin)
        {
            _origin = origin;
            _used = 0;
        }

        public void Push(Sprite icon, Color tint, string number, string tooltip)
        {
            if (_used >= _icons.Length) return;

            float x = _origin.x + _used * _slot;
            float y = _origin.y;

            _frames[_used].enabled = true;
            _frames[_used].sprite = null;
            _frames[_used].color = new Color(tint.r * 0.35f, tint.g * 0.35f, tint.b * 0.35f, 0.85f);
            _frames[_used].rectTransform.anchoredPosition = new Vector2(x - 2f, y + 2f);

            _icons[_used].enabled = true;
            _icons[_used].sprite = icon;

            // Without a sprite the Image draws a white box, so the tint has to carry the colour.
            _icons[_used].color = icon != null ? Color.white : tint;
            _icons[_used].rectTransform.anchoredPosition = new Vector2(x, y);

            _numbers[_used].enabled = !string.IsNullOrEmpty(number);
            _numbers[_used].text = number;
            _numbers[_used].rectTransform.anchoredPosition =
                new Vector2(x + _icon + 3f, y - _icon * 0.28f);

            _tooltips[_used] = tooltip;

            // Screen-space rect for hover testing. Canvas y counts down from the top; the mouse
            // counts up from the bottom, which is why this is not simply the anchored position.
            _rects[_used] = new Rect(x, Screen.height + y - _icon, _icon + 22f, _icon);

            _used++;
        }

        /// <summary>Switches off whatever the previous frame used and this one did not.</summary>
        public void Apply()
        {
            for (int i = _used; i < _icons.Length; i++)
            {
                _frames[i].enabled = false;
                _icons[i].enabled = false;
                _numbers[i].enabled = false;
            }
        }

        public void Hide()
        {
            _used = 0;
            Apply();
        }

        /// <summary>Description of the entry under the cursor, or null.</summary>
        public string TooltipAt(Vector2 mouse)
        {
            for (int i = 0; i < _used; i++)
            {
                if (_rects[i].Contains(mouse)) return _tooltips[i];
            }

            return null;
        }
    }
}
