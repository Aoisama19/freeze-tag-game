using UnityEngine;

namespace BarafPaani.UI
{
    /// <summary>
    /// Keeps the menu's backdrop very slowly moving, so the screen is never
    /// quite still.
    ///
    /// Deliberately slower and smaller than anyone would consciously notice.
    /// A background that visibly slides reads as a bug; one that drifts a few
    /// pixels a second just stops the menu looking like a screenshot.
    ///
    /// The image is scaled up a little first, so panning never uncovers an edge.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class BackgroundDrift : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Headroom for the pan. 1.08 is eight percent bigger than the screen.")]
        private float _overscan = 1.08f;

        [SerializeField]
        [Tooltip("How far it wanders from centre, as a fraction of the overscan.")]
        private float _travel = 0.5f;

        [SerializeField]
        [Tooltip("Seconds for one full pass. Long on purpose.")]
        private float _period = 48f;

        [SerializeField]
        [Tooltip("How much it breathes in and out on top of the pan.")]
        private float _zoom = 0.02f;

        private RectTransform _rect;
        private Vector2 _resting;
        private float _time;

        private void Awake()
        {
            _rect = GetComponent<RectTransform>();
            _resting = _rect.anchoredPosition;
        }

        private void OnEnable()
        {
            // Started part-way in, so reopening the menu does not always begin
            // from the same corner.
            _time = Random.Range(0f, _period);
        }

        private void Update()
        {
            if (_period <= 0.01f)
            {
                return;
            }

            _time += Time.unscaledDeltaTime;

            float turns = (_time / _period) * Mathf.PI * 2f;

            // The two axes are on different multiples, so the path is a slow
            // figure rather than a line it retraces.
            float slack = _rect.rect.width * (_overscan - 1f) * _travel;

            _rect.anchoredPosition = _resting + new Vector2(
                Mathf.Sin(turns) * slack,
                Mathf.Sin(turns * 0.6f) * slack * 0.4f);

            float breath = _overscan + (Mathf.Sin(turns * 0.35f) * _zoom);
            _rect.localScale = new Vector3(breath, breath, 1f);
        }
    }
}
