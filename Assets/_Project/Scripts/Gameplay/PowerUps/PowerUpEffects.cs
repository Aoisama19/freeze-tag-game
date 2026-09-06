using Mirror;
using UnityEngine;
using UnityEngine.AI;

namespace BarafPaani.Gameplay.PowerUps
{
    /// <summary>
    /// The one place a power-up has consequences, for the same reason Freezable
    /// is the one place freezing does.
    ///
    /// In the old build each power-up was a component that reached out and
    /// changed whatever it fancied — a controller's sprint speed, a layer, a
    /// tag, a minimap component, a static event — and undid it in its own
    /// Deactivate. Nothing agreed on what "active" meant, so an effect
    /// interrupted halfway left the character permanently altered. Here the
    /// state is replicated, and applying it is a single method that can be run
    /// twice without doing the work twice.
    /// </summary>
    public class PowerUpEffects : NetworkBehaviour
    {
        [Header("Speed boost")]
        [SerializeField]
        private float _speedBoostSeconds = 6f;

        [SerializeField]
        [Tooltip("Multiplies both walking and sprinting, so it is worth having whatever you were doing.")]
        private float _speedBoostMultiplier = 1.7f;

        [SyncVar(hook = nameof(OnSpeedBoostChanged))]
        private bool _speedBoosted;

        [SyncVar]
        [Tooltip("Network time the boost runs out, so the HUD can count it down.")]
        private double _speedBoostEndsAt;

        [Header("Invisibility")]
        [SerializeField]
        private float _invisibleSeconds = 5f;

        [SyncVar(hook = nameof(OnInvisibleChanged))]
        private bool _invisible;

        [SyncVar]
        private double _invisibleEndsAt;

        private PlayerMotor _motor;
        private NavMeshAgent _agent;
        private CharacterAppearance _appearance;
        private float _agentBaseSpeed = -1f;

        // What has actually been applied, so the hook and the server can both
        // call Apply without the second one undoing the first.
        private bool? _appliedBoost;
        private bool? _appliedInvisible;

        public bool SpeedBoosted => _speedBoosted;

        /// <summary>
        /// Whether this character is hidden. Read by the minimap, by sight and
        /// by the AI, all of which go through MapKnowledge for what it means.
        /// </summary>
        public bool Invisible => _invisible;

        public float InvisibilityRemaining =>
            _invisible ? Mathf.Max(0f, (float)(_invisibleEndsAt - NetworkTime.time)) : 0f;

        /// <summary>
        /// Whether a character is hidden, for the call sites that hold a
        /// component rather than this one. Answers false for anything that
        /// cannot be hidden at all, so callers do not each need the null check.
        /// </summary>
        public static bool IsHidden(Component character)
        {
            return character != null
                && character.TryGetComponent(out PowerUpEffects effects)
                && effects.Invisible;
        }

        /// <summary>Seconds left on the boost, for the HUD. Zero when it is not running.</summary>
        public float SpeedBoostRemaining =>
            _speedBoosted ? Mathf.Max(0f, (float)(_speedBoostEndsAt - NetworkTime.time)) : 0f;

        private void Awake()
        {
            _motor = GetComponent<PlayerMotor>();
            _agent = GetComponent<NavMeshAgent>();
            _appearance = GetComponent<CharacterAppearance>();

            if (_agent != null)
            {
                _agentBaseSpeed = _agent.speed;
            }
        }

        public override void OnStartServer()
        {
            ApplyAll();
        }

        public override void OnStartClient()
        {
            ApplyAll();
        }

        private void ApplyAll()
        {
            ApplySpeedBoost(_speedBoosted);
            ApplyInvisible(_invisible);
        }

        /// <summary>
        /// Starts one. Returns false if this kind cannot run right now, which is
        /// what stops the holder spending an item on nothing.
        /// </summary>
        [Server]
        public bool Begin(PowerUpKind kind)
        {
            switch (kind)
            {
                case PowerUpKind.SpeedBoost:
                    return BeginSpeedBoost();

                case PowerUpKind.Invisibility:
                    return BeginInvisibility();

                default:
                    return false;
            }
        }

        [Server]
        private bool BeginSpeedBoost()
        {
            // Re-using one already running refreshes it rather than being
            // refused. Two boosts stacking into triple speed is the kind of
            // thing that only shows up when somebody tries it.
            _speedBoostEndsAt = NetworkTime.time + _speedBoostSeconds;
            _speedBoosted = true;
            ApplySpeedBoost(true);

            return true;
        }

        [Server]
        private bool BeginInvisibility()
        {
            _invisibleEndsAt = NetworkTime.time + _invisibleSeconds;
            _invisible = true;
            ApplyInvisible(true);

            return true;
        }

        /// <summary>Ends everything. Called when a round restarts.</summary>
        [Server]
        public void ClearAll()
        {
            _speedBoosted = false;
            _speedBoostEndsAt = 0d;
            ApplySpeedBoost(false);

            _invisible = false;
            _invisibleEndsAt = 0d;
            ApplyInvisible(false);
        }

        [ServerCallback]
        private void Update()
        {
            if (_speedBoosted && NetworkTime.time >= _speedBoostEndsAt)
            {
                _speedBoosted = false;
                ApplySpeedBoost(false);
            }

            if (_invisible && NetworkTime.time >= _invisibleEndsAt)
            {
                _invisible = false;
                ApplyInvisible(false);
            }
        }

        private void OnInvisibleChanged(bool previous, bool current)
        {
            ApplyInvisible(current);
        }

        private void ApplyInvisible(bool invisible)
        {
            if (_appliedInvisible == invisible)
            {
                return;
            }

            _appliedInvisible = invisible;

            // Only the look is applied here. Being hidden from the minimap and
            // from the AI is not something to switch off somewhere — it falls
            // out of MapKnowledge reading Invisible, so there is no second
            // copy of the rule to forget to undo.
            if (_appearance != null)
            {
                _appearance.SetHidden(invisible);
            }
        }

        private void OnSpeedBoostChanged(bool previous, bool current)
        {
            ApplySpeedBoost(current);
        }

        private void ApplySpeedBoost(bool boosted)
        {
            if (_appliedBoost == boosted)
            {
                return;
            }

            _appliedBoost = boosted;

            float multiplier = boosted ? _speedBoostMultiplier : 1f;

            if (_motor != null)
            {
                _motor.SpeedMultiplier = multiplier;
            }

            // Set from the speed captured at Awake rather than by multiplying
            // the current one, so an effect applied twice cannot compound.
            if (_agent != null && _agentBaseSpeed > 0f)
            {
                _agent.speed = _agentBaseSpeed * multiplier;
            }
        }
    }
}
