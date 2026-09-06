using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BarafPaani.UI
{
    /// <summary>
    /// Makes a button answer back: it swells slightly and brightens when the
    /// pointer is over it, and dents when pressed.
    ///
    /// Handles selection as well as hovering, so a gamepad or the arrow keys
    /// get the same feedback as a mouse. A button that only responds to a
    /// pointer looks broken to anyone not using one.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class ButtonFeel : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        IPointerDownHandler,
        IPointerUpHandler,
        ISelectHandler,
        IDeselectHandler
    {
        [SerializeField]
        private float _hoverScale = 1.06f;

        [SerializeField]
        private float _pressedScale = 0.97f;

        [SerializeField]
        [Tooltip("Higher settles faster. Framerate independent.")]
        private float _speed = 14f;

        [SerializeField]
        [Tooltip("Added to the plate's colour while hovered.")]
        private float _brighten = 0.15f;

        private RectTransform _rect;
        private Graphic _plate;
        private Color _resting;
        private bool _hovered;
        private bool _pressed;

        private void Awake()
        {
            _rect = GetComponent<RectTransform>();
            _plate = GetComponent<Graphic>();

            if (_plate != null)
            {
                _resting = _plate.color;
            }
        }

        private void OnDisable()
        {
            // Left mid-hover, the button would come back swollen next time the
            // menu opened.
            _hovered = false;
            _pressed = false;

            if (_rect != null)
            {
                _rect.localScale = Vector3.one;
            }

            if (_plate != null)
            {
                _plate.color = _resting;
            }
        }

        private void Update()
        {
            if (_rect == null)
            {
                return;
            }

            float target = _pressed ? _pressedScale : _hovered ? _hoverScale : 1f;
            float step = 1f - Mathf.Exp(-_speed * Time.unscaledDeltaTime);

            _rect.localScale = Vector3.Lerp(_rect.localScale, Vector3.one * target, step);

            if (_plate == null)
            {
                return;
            }

            Color wanted = _hovered
                ? new Color(
                    Mathf.Min(1f, _resting.r + _brighten),
                    Mathf.Min(1f, _resting.g + _brighten),
                    Mathf.Min(1f, _resting.b + _brighten),
                    _resting.a)
                : _resting;

            _plate.color = Color.Lerp(_plate.color, wanted, step);
        }

        public void OnPointerEnter(PointerEventData eventData) => _hovered = true;

        public void OnPointerExit(PointerEventData eventData)
        {
            _hovered = false;
            _pressed = false;
        }

        public void OnPointerDown(PointerEventData eventData) => _pressed = true;

        public void OnPointerUp(PointerEventData eventData) => _pressed = false;

        public void OnSelect(BaseEventData eventData) => _hovered = true;

        public void OnDeselect(BaseEventData eventData) => _hovered = false;
    }
}
