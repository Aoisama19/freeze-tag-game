using Mirror;
using UnityEngine;

namespace BarafPaani.Gameplay
{
    /// <summary>
    /// Freeze state for one character, and the only place its consequences are
    /// applied.
    ///
    /// The old build stored this as gameObject.layer and then disabled the
    /// animator, agent, controller and input separately at each call site. That
    /// could not replicate — which is the root reason multiplayer needed its own
    /// parallel implementation — and the four disable lists had drifted apart,
    /// so a character frozen by the AI ended up in a different state than one
    /// frozen by a player. See docs/old-build-issues.md, issue 5.
    /// </summary>
    public class Freezable : NetworkBehaviour
    {
        [SerializeField]
        [Tooltip("Disabled while frozen so the character cannot be driven.")]
        private PlayerMotor _motor;

        [SerializeField]
        [Tooltip("Tinted while frozen.")]
        private CharacterAppearance _appearance;

        [SerializeField]
        [Tooltip("Stopped mid-stride while frozen.")]
        private CharacterAnimation _animation;

        [SerializeField]
        [Tooltip("Chimes on the way in and out.")]
        private Audio.CharacterAudio _audio;

        [SyncVar(hook = nameof(OnIsFrozenChanged))]
        private bool _isFrozen;

        // Tracks what has actually been applied, so the state can be applied
        // twice without doing the work twice, and so the initial state is
        // applied exactly once on spawn.
        private bool? _applied;

        public bool IsFrozen => _isFrozen;

        private void Awake()
        {
            if (_motor == null)
            {
                _motor = GetComponent<PlayerMotor>();
            }

            if (_appearance == null)
            {
                _appearance = GetComponent<CharacterAppearance>();
            }

            if (_animation == null)
            {
                _animation = GetComponent<CharacterAnimation>();
            }

            if (_audio == null)
            {
                _audio = GetComponent<Audio.CharacterAudio>();
            }
        }

        /// <summary>
        /// Freezes the character. Returns false if it was already frozen, which
        /// is the guard that stops a second touch counting as a second freeze.
        /// </summary>
        [Server]
        public bool Freeze()
        {
            if (_isFrozen)
            {
                return false;
            }

            _isFrozen = true;
            Apply(true);
            return true;
        }

        /// <summary>Frees the character. Returns false if it was not frozen.</summary>
        [Server]
        public bool Unfreeze()
        {
            if (!_isFrozen)
            {
                return false;
            }

            _isFrozen = false;
            Apply(false);
            return true;
        }

        public override void OnStartServer()
        {
            Apply(_isFrozen);
        }

        // Covers clients joining after someone is already frozen, where there is
        // no change for the hook to fire on.
        public override void OnStartClient()
        {
            Apply(_isFrozen);
        }

        private void OnIsFrozenChanged(bool previous, bool current)
        {
            Apply(current);
        }

        /// <summary>
        /// The single place freeze has consequences. Called from the SyncVar hook
        /// for remote clients and directly on the server, because Mirror only
        /// fires the hook server-side in host mode and only while the object is
        /// inside the host client's interest range.
        /// </summary>
        private void Apply(bool frozen)
        {
            if (_applied == frozen)
            {
                return;
            }

            // Whether this is a change or the state the character arrived in.
            // Spawning is not something to make a noise about — without this,
            // every character thaws audibly the moment it appears.
            bool changed = _applied.HasValue;

            _applied = frozen;

            if (_motor != null)
            {
                _motor.enabled = !frozen;
            }

            if (_appearance != null)
            {
                _appearance.SetFrozen(frozen);
            }

            if (_animation != null)
            {
                _animation.SetFrozen(frozen);
            }

            if (changed && _audio != null)
            {
                _audio.PlayFrozen(frozen);
            }
        }
    }
}
