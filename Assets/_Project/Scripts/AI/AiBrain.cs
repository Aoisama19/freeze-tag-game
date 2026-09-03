using System;
using BarafPaani.Gameplay;
using Mirror;
using UnityEngine;
using UnityEngine.AI;

namespace BarafPaani.AI
{
    /// <summary>
    /// One brain for both sides. It reads its own PlayerRole and behaves
    /// accordingly, so a catcher hunts and guards while a runner rescues and
    /// flees, all out of the same component.
    ///
    /// The old build had CatcherAI and RunnerAI as separate classes and they
    /// drifted — RunnerAI was rewritten in one repository and left alone in the
    /// other. One class means that cannot happen again.
    ///
    /// What each side knows is deliberately different. Frozen characters are
    /// known globally, because isFrozen is replicated to every client anyway and
    /// the HUD will show human players the same fact. The catcher's position is
    /// known only when it can actually be seen. See AiTactics.
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
        [Tooltip("Seconds between looking around. Not every frame, it is a physics query.")]
        private float _rescanInterval = 0.25f;

        [SerializeField]
        private float _wanderRadius = 18f;

        [SerializeField]
        [Tooltip("How close to a destination counts as arrived.")]
        private float _arriveDistance = 1.5f;

        [Header("Runner")]
        [SerializeField]
        [Tooltip("A visible catcher closer than this is worth running from.")]
        private float _dangerDistance = 9f;

        [SerializeField]
        [Tooltip("How far to bolt when fleeing.")]
        private float _fleeDistance = 14f;

        [SerializeField]
        [Tooltip("Skip a rescue if the frozen team-mate is this close to a visible catcher.")]
        private float _rescueAbortDistance = 5f;

        [Header("Catcher")]
        [SerializeField]
        [Tooltip("How long to lurk near frozen runners before going hunting again.")]
        private float _guardSeconds = 6f;

        [SerializeField]
        [Tooltip("How long to hunt before it is willing to guard again. Stops it camping.")]
        private float _guardBreakSeconds = 6f;

        [SerializeField]
        [Tooltip("How far off the frozen runner to lurk, so it does not stand on them.")]
        private float _guardStandoff = 4f;

        private NavMeshAgent _agent;
        private PlayerRole _role;
        private Freezable _freezable;

        private Func<PlayerRole, Freezable, bool> _acceptApproachTarget;
        private Func<PlayerRole, Freezable, bool> _acceptFrozenRunner;
        private Func<PlayerRole, Freezable, bool> _acceptCatcher;

        private Transform _visibleQuarry;
        private Transform _visibleCatcher;
        private Transform _frozenCharacter;

        private float _nextScanTime;
        private float _guardStartedAt;
        private float _guardAvailableAt;

        private CatcherIntent _catcherIntent = CatcherIntent.Wander;
        private RunnerIntent _runnerIntent = RunnerIntent.Wander;

        public CatcherIntent CatcherIntent => _catcherIntent;

        public RunnerIntent RunnerIntent => _runnerIntent;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _role = GetComponent<PlayerRole>();
            _freezable = GetComponent<Freezable>();

            if (_vision == null)
            {
                _vision = GetComponent<Vision>();
            }

            // Cached so the per-scan lookups do not allocate a delegate each time.
            //
            // One rule serves both sides: AiTargeting says a catcher wants live
            // runners and a runner wants frozen ones, so this single predicate
            // covers the catcher's chase and the runner's rescue.
            _acceptApproachTarget = (role, state) =>
                AiTargeting.WantsToApproach(_role.Role, _freezable.IsFrozen, role.Role, state.IsFrozen);

            // Guarding is a different question: the catcher wants to know where
            // frozen runners are precisely because it does not want to tag them.
            _acceptFrozenRunner = (role, state) => role.Role == Role.Runner && state.IsFrozen;

            _acceptCatcher = (role, state) => role.Role == Role.Catcher;
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
                Scan();
            }

            if (_role.Role == Role.Catcher)
            {
                TickCatcher(now);
                return;
            }

            TickRunner();
        }

        private void Scan()
        {
            Vector3 here = transform.position;

            if (_role.Role == Role.Catcher)
            {
                // Seen, not known: it has to spot a runner to chase one.
                _visibleQuarry = _vision != null
                    ? _vision.FindNearestVisible(_acceptApproachTarget)
                    : null;

                _frozenCharacter = ServerCharacters.FindNearest(here, transform, _acceptFrozenRunner);
                return;
            }

            // A runner has to actually see the catcher to be afraid of it.
            _visibleCatcher = _vision != null ? _vision.FindNearestVisible(_acceptCatcher) : null;

            // Known, not seen: frozen team-mates are calling out, and their state
            // is replicated to everyone regardless.
            _frozenCharacter = ServerCharacters.FindNearest(here, transform, _acceptApproachTarget);
        }

        private void TickCatcher(float now)
        {
            _catcherIntent = AiTactics.ChooseCatcherIntent(
                _visibleQuarry != null,
                _frozenCharacter != null,
                now >= _guardAvailableAt);

            switch (_catcherIntent)
            {
                case CatcherIntent.Chase:
                    _guardStartedAt = 0f;
                    MoveTo(_visibleQuarry.position);
                    return;

                case CatcherIntent.Guard:
                    Guard(now);
                    return;

                default:
                    Wander();
                    return;
            }
        }

        private void Guard(float now)
        {
            if (_guardStartedAt <= 0f)
            {
                _guardStartedAt = now;
            }

            // Long enough. Go hunting, so the endgame does not stall on a catcher
            // parked next to the ice forever.
            if (now - _guardStartedAt >= _guardSeconds)
            {
                _guardStartedAt = 0f;
                _guardAvailableAt = now + _guardBreakSeconds;
                _catcherIntent = CatcherIntent.Wander;
                Wander();
                return;
            }

            // Lurk a little off the frozen runner rather than standing on them,
            // so there is room for a rescuer to walk into.
            Vector3 frozenAt = _frozenCharacter.position;
            Vector3 offset = transform.position - frozenAt;
            offset.y = 0f;

            Vector3 direction = offset.sqrMagnitude > 0.01f ? offset.normalized : Vector3.forward;
            MoveTo(frozenAt + (direction * _guardStandoff));
        }

        private void TickRunner()
        {
            float catcherDistance = _visibleCatcher != null
                ? Vector3.Distance(transform.position, _visibleCatcher.position)
                : float.MaxValue;

            _runnerIntent = AiTactics.ChooseRunnerIntent(
                _visibleCatcher != null,
                catcherDistance,
                _dangerDistance,
                HasReachableFrozenAlly());

            switch (_runnerIntent)
            {
                case RunnerIntent.Flee:
                    Flee();
                    return;

                case RunnerIntent.Rescue:
                    MoveTo(_frozenCharacter.position);
                    return;

                default:
                    Wander();
                    return;
            }
        }

        /// <summary>
        /// A frozen team-mate is only worth going for if the catcher is not
        /// standing over them. Walking into a guarded rescue just hands the
        /// catcher another runner.
        /// </summary>
        private bool HasReachableFrozenAlly()
        {
            if (_frozenCharacter == null)
            {
                return false;
            }

            if (_visibleCatcher == null)
            {
                return true;
            }

            float guarded = (_frozenCharacter.position - _visibleCatcher.position).sqrMagnitude;
            return guarded > _rescueAbortDistance * _rescueAbortDistance;
        }

        private void Flee()
        {
            Vector3 away = transform.position - _visibleCatcher.position;
            away.y = 0f;

            Vector3 direction = away.sqrMagnitude > 0.01f ? away.normalized : transform.forward;
            Vector3 target = transform.position + (direction * _fleeDistance);

            if (NavMesh.SamplePosition(target, out NavMeshHit hit, _fleeDistance, NavMesh.AllAreas))
            {
                MoveTo(hit.position);
                return;
            }

            // Cornered: nowhere on the mesh that way. Keep moving rather than
            // standing still waiting to be tagged.
            Wander();
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

        private void MoveTo(Vector3 destination)
        {
            _agent.isStopped = false;
            _agent.SetDestination(destination);
        }

        private void Halt()
        {
            _catcherIntent = CatcherIntent.Wander;
            _runnerIntent = RunnerIntent.Wander;
            _visibleQuarry = null;
            _visibleCatcher = null;
            _frozenCharacter = null;
            _guardStartedAt = 0f;

            if (_agent.hasPath)
            {
                _agent.ResetPath();
            }

            _agent.isStopped = true;
        }
    }
}
