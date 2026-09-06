using BarafPaani.Gameplay;
using UnityEngine;

namespace BarafPaani.Audio
{
    /// <summary>
    /// The noise one character makes: footsteps while it moves, and a chime
    /// when it freezes or is freed.
    ///
    /// Footsteps are driven by ground covered rather than by animation events,
    /// which means the same component works for the local player, a remote
    /// player moved by NetworkTransform and a bot moved by its agent — the same
    /// reasoning as CharacterAnimation, and it reuses that component's speed so
    /// the two cannot disagree about whether this character is walking.
    ///
    /// Nothing here is networked. Every client can already see where everyone
    /// is, so every client can work out for itself what it should be hearing,
    /// and a footstep does not need a packet.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class CharacterAudio : MonoBehaviour
    {
        [SerializeField]
        private SoundBank _sounds;

        [SerializeField]
        private AudioSource _source;

        [SerializeField]
        [Tooltip("Metres between footsteps. Roughly one stride of the run cycle.")]
        private float _strideLength = 1.9f;

        [SerializeField]
        [Tooltip("Below this the character is shuffling, not walking, and makes no noise.")]
        private float _quietBelowSpeed = 0.6f;

        [SerializeField]
        [Range(0f, 1f)]
        private float _footstepVolume = 0.45f;

        /// <summary>
        /// Above this, the step was a teleport. Rounds begin by moving everyone
        /// home, and without this that lands as a burst of footsteps.
        /// </summary>
        [SerializeField]
        private float _teleportSpeed = 25f;

        private Vector3 _lastPosition;
        private float _sinceLastStep;

        private void Awake()
        {
            if (_source == null)
            {
                _source = GetComponent<AudioSource>();
            }

            _lastPosition = transform.position;
        }

        private void OnEnable()
        {
            _lastPosition = transform.position;
            _sinceLastStep = 0f;
        }

        private void Update()
        {
            float speed = MoveSpeed.Measure(
                _lastPosition, transform.position, Time.deltaTime, _teleportSpeed);

            float travelled = speed * Time.deltaTime;
            _lastPosition = transform.position;

            if (speed < _quietBelowSpeed)
            {
                // Reset rather than hold, so stopping and starting again does
                // not fire a step on the first frame of moving.
                _sinceLastStep = 0f;
                return;
            }

            _sinceLastStep += travelled;

            if (_sinceLastStep < _strideLength)
            {
                return;
            }

            _sinceLastStep -= _strideLength;
            Step();
        }

        private void Step()
        {
            AudioClip clip = _sounds != null ? _sounds.Footstep() : null;

            if (clip == null || _source == null)
            {
                return;
            }

            // A little pitch variation, so ten of the same clip in a row do not
            // read as a loop.
            _source.pitch = Random.Range(0.92f, 1.08f);
            _source.PlayOneShot(clip, _footstepVolume);
        }

        /// <summary>Called by Freezable, which owns what freezing does.</summary>
        public void PlayFrozen(bool frozen)
        {
            AudioClip clip = _sounds == null
                ? null
                : frozen
                    ? _sounds.Freeze
                    : _sounds.Thaw;

            if (clip == null || _source == null)
            {
                return;
            }

            _source.pitch = 1f;
            _source.PlayOneShot(clip);
        }

        /// <summary>Called when this character spends a power-up.</summary>
        public void PlayPowerUp()
        {
            if (_sounds == null || _sounds.PowerUp == null || _source == null)
            {
                return;
            }

            _source.pitch = 1f;
            _source.PlayOneShot(_sounds.PowerUp);
        }
    }
}
