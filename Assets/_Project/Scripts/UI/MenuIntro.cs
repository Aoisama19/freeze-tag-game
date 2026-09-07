using UnityEngine;

namespace BarafPaani.UI
{
    /// <summary>
    /// Brings the menu in when it opens: each piece fades up into place, a
    /// moment after the one before it.
    ///
    /// Driven from Update rather than a coroutine, so nothing holds state
    /// between frames that a disabled component would leak.
    /// </summary>
    public class MenuIntro : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Animated in this order.")]
        private RectTransform[] _pieces;

        [SerializeField]
        [Tooltip("How long each piece takes to arrive.")]
        private float _duration = 0.45f;

        [SerializeField]
        [Tooltip("Gap between one piece starting and the next.")]
        private float _stagger = 0.07f;

        [SerializeField]
        [Tooltip("How far below its resting place a piece starts.")]
        private float _rise = 34f;

        private CanvasGroup[] _groups;
        private Vector2[] _resting;
        private float _elapsed;
        private bool _finished;

        private void Awake()
        {
            if (_pieces == null || _pieces.Length == 0)
            {
                _finished = true;
                return;
            }

            _groups = new CanvasGroup[_pieces.Length];
            _resting = new Vector2[_pieces.Length];

            for (int i = 0; i < _pieces.Length; i++)
            {
                if (_pieces[i] == null)
                {
                    continue;
                }

                _resting[i] = _pieces[i].anchoredPosition;

                // Added rather than required, so the builder does not have to
                // put one on every element it lays out.
                //
                // Written as an explicit comparison, not ??. UnityEngine.Object
                // overloads == to report a missing or destroyed object as null,
                // and the null-coalescing operator does not use that overload —
                // so ?? hands back the missing component instead of making one,
                // and the next line throws. That is what happened here: the
                // whole intro silently did nothing from the moment it was added.
                CanvasGroup group = _pieces[i].GetComponent<CanvasGroup>();

                if (group == null)
                {
                    group = _pieces[i].gameObject.AddComponent<CanvasGroup>();
                }

                _groups[i] = group;
                _groups[i].alpha = 0f;
            }
        }

        private void OnEnable()
        {
            _elapsed = 0f;
            _finished = _pieces == null || _pieces.Length == 0;
        }

        private void Update()
        {
            if (_finished)
            {
                return;
            }

            _elapsed += Time.unscaledDeltaTime;
            bool anyLeft = false;

            for (int i = 0; i < _pieces.Length; i++)
            {
                if (_pieces[i] == null || _groups[i] == null)
                {
                    continue;
                }

                float through = Mathf.Clamp01((_elapsed - (i * _stagger)) / _duration);

                // Eased out, so a piece decelerates into place instead of
                // stopping dead.
                float eased = 1f - Mathf.Pow(1f - through, 3f);

                _groups[i].alpha = eased;
                _pieces[i].anchoredPosition =
                    _resting[i] + new Vector2(0f, (1f - eased) * -_rise);

                anyLeft |= through < 1f;
            }

            if (anyLeft)
            {
                return;
            }

            // Everything has arrived. Put each piece exactly where it belongs,
            // rather than wherever the last frame's easing left it.
            for (int i = 0; i < _pieces.Length; i++)
            {
                if (_pieces[i] != null)
                {
                    _pieces[i].anchoredPosition = _resting[i];
                }

                if (_groups[i] != null)
                {
                    _groups[i].alpha = 1f;
                }
            }

            _finished = true;
        }
    }
}
