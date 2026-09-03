using System;
using BarafPaani.Gameplay;
using Mirror;
using UnityEngine;
using UnityEngine.AI;

namespace BarafPaani.AI
{
    /// <summary>
    /// One brain for both sides. It asks AiTargeting who it wants to reach given
    /// its own role, so a catcher chases live runners and a runner goes to frozen
    /// team-mates using the same code.
    ///
    /// The old build had CatcherAI and RunnerAI as separate classes, and they
    /// drifted: RunnerAI was rewritten in one repository and left alone in the
    /// other. One class means that cannot happen again.
    ///
    /// Server-only. Clients receive the movement like any other transform change.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(PlayerRole))]
    [RequireComponent(typeof(Freezable))]
    public class AiBrain : NetworkBehaviour
    {
        [SerializeField]
        private Vision _vision;

        [SerializeField]
        [Tooltip("Seconds between looking around. Not every frame — it is a physics query.")]
        private float _rescanInterval = 0.25f;

        [SerializeField]
        private float _wanderRadius = 18f;

        [SerializeField]
        [Tooltip("How close to a wander destination counts as arrived.")]
        private float _arriveDistance = 1.5f;

        private NavMeshAgent _agent;
        private PlayerRole _role;
        private Freezable _freezable;
        private Func<PlayerRole, Freezable, bool> _accept;
        private Transform _target;
        private float _nextScanTime;
        private AiState _state = AiState.Searching;

        public AiState State => _state;

        public Transform Target => _target;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _role = GetComponent<PlayerRole>();
            _freezable = GetComponent<Freezable>();

            if (_vision == null)
            {
                _vision = GetComponent<Vision>();
            }

            // Cached so the per-scan lookup does not allocate a delegate.
            _accept = Wants;
        }

        [ServerCallback]
        private void Update()
        {
            Tick(Time.time);
        }

        /// <summary>
        /// One decision, then return. There is no loop and no coroutine holding
        /// state between frames.
        ///
        /// Issue 1 in the old build was CatcherAI.Delay(): StopCoroutine(Delay())
        /// built a fresh iterator and so stopped nothing, control fell back into
        /// a while (true) whose else branch had no yield, and the editor hung
        /// inside a single frame. Nothing shaped like that can happen here.
        /// </summary>
        private void Tick(float now)
        {
            if (_agent == null || !_agent.enabled)
            {
                return;
            }

            if (_freezable.IsFrozen)
            {
                Halt();
                return;
            }

            if (now >= _nextScanTime)
            {
                _nextScanTime = now + _rescanInterval;
                _target = _vision != null ? _vision.FindNearestVisible(_accept) : null;
            }

            // Between scans a target can be frozen, freed or destroyed by someone
            // else, so re-check before trusting it.
            if (_target != null && !StillWanted(_target))
            {
                _target = null;
            }

            if (_target == null)
            {
                _state = AiState.Searching;
                Wander();
                return;
            }

            _state = AiState.Approaching;
            _agent.isStopped = false;
            _agent.SetDestination(_target.position);
        }

        private bool Wants(PlayerRole candidate, Freezable candidateFreeze)
        {
            return AiTargeting.WantsToApproach(
                _role.Role, _freezable.IsFrozen, candidate.Role, candidateFreeze.IsFrozen);
        }

        private bool StillWanted(Transform candidate)
        {
            return candidate.TryGetComponent(out PlayerRole role)
                && candidate.TryGetComponent(out Freezable freezable)
                && Wants(role, freezable);
        }

        private void Wander()
        {
            _agent.isStopped = false;

            if (_agent.pathPending)
            {
                return;
            }

            if (_agent.hasPath && _agent.remainingDistance > _arriveDistance)
            {
                return;
            }

            Vector3 probe = transform.position + (UnityEngine.Random.insideUnitSphere * _wanderRadius);
            probe.y = transform.position.y;

            if (NavMesh.SamplePosition(probe, out NavMeshHit hit, _wanderRadius, NavMesh.AllAreas))
            {
                _agent.SetDestination(hit.position);
            }
        }

        private void Halt()
        {
            _state = AiState.Searching;
            _target = null;

            if (_agent.hasPath)
            {
                _agent.ResetPath();
            }

            _agent.isStopped = true;
        }
    }
}
