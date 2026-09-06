using UnityEngine;

namespace BarafPaani.Gameplay
{
    /// <summary>
    /// Drives a character's animator from how fast it is actually moving.
    ///
    /// Speed is measured off the transform rather than read from the motor or
    /// the agent, which is what lets one component cover all three cases: the
    /// local player, a remote player whose transform is moved by
    /// NetworkTransform, and an AI moved by its NavMeshAgent. It also means
    /// animation costs nothing on the wire — every client already has the
    /// positions it needs to work the speed out for itself.
    /// </summary>
    public class CharacterAnimation : MonoBehaviour
    {
        private static readonly int SpeedParameter = Animator.StringToHash("Speed");

        [SerializeField]
        private Animator _animator;

        [SerializeField]
        [Tooltip("Seconds to ease between speeds, so stopping does not snap the legs still.")]
        private float _smoothing = 0.12f;

        /// <summary>
        /// Above this, the transform did not run — it was moved. Rounds restart
        /// by teleporting everyone back to their spawn, and without this guard
        /// that one frame reads as a sprint of several hundred metres a second.
        /// </summary>
        [SerializeField]
        private float _teleportSpeed = 25f;

        private Vector3 _lastPosition;
        private float _speed;
        private bool _frozen;

        private void Awake()
        {
            if (_animator == null)
            {
                _animator = GetComponentInChildren<Animator>();
            }

            _lastPosition = transform.position;
        }

        private void OnEnable()
        {
            // Coming back from a disabled state is a jump like any other.
            _lastPosition = transform.position;
            _speed = 0f;
        }

        private void Update()
        {
            if (_animator == null || Time.deltaTime <= 0f)
            {
                return;
            }

            float measured = MoveSpeed.Measure(
                _lastPosition, transform.position, Time.deltaTime, _teleportSpeed);

            _lastPosition = transform.position;

            // Nothing to drive while frozen — the animator is stopped dead, so
            // the character holds whatever stride it was caught in.
            if (_frozen)
            {
                return;
            }

            _speed = Mathf.Lerp(
                _speed, measured, _smoothing <= 0f ? 1f : Time.deltaTime / _smoothing);

            _animator.SetFloat(SpeedParameter, _speed);
        }

        /// <summary>
        /// Locks the character mid-stride, which is what being frozen should
        /// look like, and picks the animation back up on release.
        /// </summary>
        public void SetFrozen(bool frozen)
        {
            _frozen = frozen;

            if (_animator != null)
            {
                _animator.speed = frozen ? 0f : 1f;
            }
        }
    }
}
