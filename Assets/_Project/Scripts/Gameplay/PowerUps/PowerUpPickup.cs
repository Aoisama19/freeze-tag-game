using Mirror;
using UnityEngine;

namespace BarafPaani.Gameplay.PowerUps
{
    /// <summary>
    /// One power-up sitting in the world, waiting to be walked into.
    ///
    /// The server decides who gets it. Handled client-side instead, two players
    /// could each take the same pickup and neither would see the other do it.
    ///
    /// Contact is a proximity query on a tick rather than a trigger callback,
    /// the same way TagOnContact does it, and for the same reason: nothing in
    /// this game carries a Rigidbody, so OnTriggerEnter never fires between a
    /// static trigger and a character pushed around by a NavMeshAgent. Written
    /// as a trigger it works for the human, whose CharacterController raises
    /// them, and silently never fires for a single bot.
    /// </summary>
    public class PowerUpPickup : NetworkBehaviour
    {
        [SerializeField]
        private PowerUpKind _kind = PowerUpKind.SpeedBoost;

        [SerializeField]
        [Tooltip("Seconds before it comes back after being taken.")]
        private float _respawnSeconds = 20f;

        [SerializeField]
        [Tooltip("How close counts as walking into it. Generous, so it is taken rather than aimed at.")]
        private float _range = 1.4f;

        [SerializeField]
        [Tooltip("Seconds between checks. Being picked up does not need testing every frame.")]
        private float _checkInterval = 0.1f;

        [SerializeField]
        private LayerMask _characterMask = ~0;

        [SerializeField]
        private BarafPaani.Audio.SoundBank _sounds;

        [Header("Look")]
        [SerializeField]
        private float _spinDegreesPerSecond = 60f;

        [SerializeField]
        private float _bobHeight = 0.25f;

        [SerializeField]
        private float _bobsPerSecond = 0.5f;

        [SyncVar(hook = nameof(OnAvailableChanged))]
        private bool _available = true;

        private static readonly int BaseColour = Shader.PropertyToID("_BaseColor");
        private static readonly int LegacyColour = Shader.PropertyToID("_Color");

        private readonly Collider[] _hits = new Collider[8];
        private Collider _collider;
        private Renderer[] _renderers;
        private Vector3 _restPosition;
        private double _returnsAt;
        private float _nextCheckTime;

        public PowerUpKind Kind => _kind;

        public bool Available => _available;

        private void Awake()
        {
            _collider = GetComponent<Collider>();

            if (_collider != null)
            {
                // A trigger so characters walk through it rather than bouncing
                // off. Nothing reads the trigger events — the proximity query
                // above does the work — it is only here so the box is not a wall.
                _collider.isTrigger = true;
            }

            _renderers = GetComponentsInChildren<Renderer>();
            _restPosition = transform.position;

            Paint();
        }

        /// <summary>
        /// Colours the box by what is in it, through a property block so the
        /// three kinds do not need three material assets between them.
        /// </summary>
        private void Paint()
        {
            Color colour = Colour(_kind);
            MaterialPropertyBlock block = new MaterialPropertyBlock();

            foreach (Renderer renderer in _renderers)
            {
                if (renderer == null)
                {
                    continue;
                }

                renderer.GetPropertyBlock(block);
                block.SetColor(BaseColour, colour);
                block.SetColor(LegacyColour, colour);
                renderer.SetPropertyBlock(block);
            }
        }

        private static Color Colour(PowerUpKind kind)
        {
            switch (kind)
            {
                case PowerUpKind.SpeedBoost:
                    return new Color(1f, 0.78f, 0.2f);

                case PowerUpKind.Invisibility:
                    return new Color(0.55f, 0.4f, 0.9f);

                case PowerUpKind.Clone:
                    return new Color(0.3f, 0.85f, 0.6f);

                default:
                    return Color.grey;
            }
        }

        public override void OnStartServer()
        {
            Apply(_available);
        }

        public override void OnStartClient()
        {
            Apply(_available);
        }

        [ServerCallback]
        private void Update()
        {
            if (!_available)
            {
                if (NetworkTime.time >= _returnsAt)
                {
                    _available = true;
                    Apply(true);
                }

                return;
            }

            if (Time.time < _nextCheckTime)
            {
                return;
            }

            _nextCheckTime = Time.time + _checkInterval;
            LookForSomeoneToGiveItTo();
        }

        [Server]
        private void LookForSomeoneToGiveItTo()
        {
            int count = Physics.OverlapSphereNonAlloc(
                transform.position, _range, _hits, _characterMask, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                if (_hits[i] == null)
                {
                    continue;
                }

                PowerUpHolder holder = _hits[i].GetComponentInParent<PowerUpHolder>();

                // A full holder is refused, and the pickup stays put rather than
                // being spent on somebody with no room for it.
                if (holder == null || !holder.Add(_kind))
                {
                    continue;
                }

                _available = false;
                _returnsAt = NetworkTime.time + _respawnSeconds;
                Apply(false);

                return;
            }
        }

        private void LateUpdate()
        {
            if (!_available)
            {
                return;
            }

            // Purely local decoration: everyone runs the same clock, so it looks
            // the same everywhere without costing anything to sync.
            transform.Rotate(Vector3.up, _spinDegreesPerSecond * Time.deltaTime, Space.World);

            float bob = Mathf.Sin(Time.time * _bobsPerSecond * Mathf.PI * 2f) * _bobHeight;
            transform.position = _restPosition + new Vector3(0f, bob, 0f);
        }

        private void OnAvailableChanged(bool previous, bool current)
        {
            Apply(current);
        }

        private bool _applied;

        private void Apply(bool available)
        {
            // Heard where it was taken, on every client, because _available is
            // replicated. Not on the first application, which is only the state
            // the pickup starts the match in.
            if (_applied && !available && _sounds != null && _sounds.Pickup != null)
            {
                AudioSource.PlayClipAtPoint(_sounds.Pickup, transform.position, 0.8f);
            }

            _applied = true;

            if (_collider != null)
            {
                _collider.enabled = available;
            }

            foreach (Renderer renderer in _renderers)
            {
                if (renderer != null)
                {
                    renderer.enabled = available;
                }
            }
        }
    }
}
