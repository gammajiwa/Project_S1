using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Proto
{
    /// <summary>
    /// One line of the main menu. The look deliberately has no button chrome, so the highlight has
    /// to carry the whole affordance: the label warms to the accent colour and slides right while a
    /// marker fades in beside it. Responds to pointer *and* selection so keyboard navigation still
    /// reads correctly if it is added later.
    /// </summary>
    public class MenuLine : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        ISelectHandler, IDeselectHandler
    {
        [SerializeField] TextMeshProUGUI _label;
        [SerializeField] RectTransform _marker;

        [SerializeField] Color _idle = Color.white;
        [SerializeField] Color _highlight = Color.white;

        [SerializeField] float _slide = 16f;
        [SerializeField] float _speed = 12f;

        RectTransform _labelRect;
        CanvasGroup _markerGroup;
        float _restX;
        float _blend;
        bool _hot;

        void Awake()
        {
            if (_label != null)
            {
                _labelRect = _label.rectTransform;
                _restX = _labelRect.anchoredPosition.x;
            }

            if (_marker != null)
            {
                _markerGroup = _marker.GetComponent<CanvasGroup>();
                if (_markerGroup == null) _markerGroup = _marker.gameObject.AddComponent<CanvasGroup>();
            }
        }

        void OnEnable()
        {
            // Pages are toggled by SetActive, so a stale hover must not survive a page change.
            _hot = false;
            _blend = 0f;
            ApplyBlend();
        }

        void Update()
        {
            float target = _hot ? 1f : 0f;
            if (Mathf.Approximately(_blend, target)) return;

            _blend = Mathf.MoveTowards(_blend, target, Time.unscaledDeltaTime * _speed);
            ApplyBlend();
        }

        void ApplyBlend()
        {
            if (_label != null) _label.color = Color.Lerp(_idle, _highlight, _blend);

            if (_labelRect != null)
            {
                var pos = _labelRect.anchoredPosition;
                _labelRect.anchoredPosition = new Vector2(_restX + _slide * _blend, pos.y);
            }

            if (_markerGroup != null) _markerGroup.alpha = _blend;
        }

        public void OnPointerEnter(PointerEventData eventData) => _hot = true;

        public void OnPointerExit(PointerEventData eventData) => _hot = false;

        public void OnSelect(BaseEventData eventData) => _hot = true;

        public void OnDeselect(BaseEventData eventData) => _hot = false;
    }
}
