using Mirror;
using UnityEngine;

namespace BarafPaani.Gameplay
{
    /// <summary>
    /// Watches for characters close enough to tag and asks FreezeRules what that
    /// should mean. Runs on the server only, so no client can ask to freeze
    /// anyone — there is nothing to spoof because there is no message.
    /// </summary>
    [RequireComponent(typeof(PlayerRole))]
    [RequireComponent(typeof(Freezable))]
    public class TagOnContact : NetworkBehaviour
    {
        [SerializeField]
        [Tooltip("How close counts as a touch. Tuned against the placeholder capsule.")]
        private float _range = 1.2f;

        [SerializeField]
        [Tooltip("Seconds between checks. Contact does not need to be tested every frame.")]
        private float _checkInterval = 0.1f;

        [SerializeField]
        private LayerMask _characterMask = ~0;

        private readonly Collider[] _hits = new Collider[16];
        private PlayerRole _role;
        private Freezable _freezable;
        private float _nextCheckTime;

        private void Awake()
        {
            _role = GetComponent<PlayerRole>();
            _freezable = GetComponent<Freezable>();
        }

        [ServerCallback]
        private void Update()
        {
            if (Time.time < _nextCheckTime)
            {
                return;
            }

            _nextCheckTime = Time.time + _checkInterval;
            ResolveContacts();
        }

        private void ResolveContacts()
        {
            // A frozen character cannot tag or free anyone, so skip the query.
            if (_freezable.IsFrozen)
            {
                return;
            }

            float squaredRange = _range * _range;
            int count = Physics.OverlapSphereNonAlloc(
                transform.position, _range, _hits, _characterMask, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                Collider hit = _hits[i];

                if (hit == null || hit.transform.root == transform)
                {
                    continue;
                }

                PlayerRole targetRole = hit.GetComponentInParent<PlayerRole>();

                if (targetRole == null || !targetRole.TryGetComponent(out Freezable target))
                {
                    continue;
                }

                float squaredDistance =
                    (target.transform.position - transform.position).sqrMagnitude;

                TagOutcome outcome = FreezeRules.Resolve(
                    _role.Role,
                    _freezable.IsFrozen,
                    targetRole.Role,
                    target.IsFrozen,
                    squaredDistance,
                    squaredRange);

                switch (outcome)
                {
                    case TagOutcome.Freeze:
                        target.Freeze();
                        break;

                    case TagOutcome.Unfreeze:
                        target.Unfreeze();
                        break;
                }
            }
        }
    }
}
