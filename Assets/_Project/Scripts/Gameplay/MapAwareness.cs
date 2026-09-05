using System;
using BarafPaani.AI;
using Mirror;
using UnityEngine;

namespace BarafPaani.Gameplay
{
    /// <summary>
    /// What this character's own minimap is allowed to show, decided on the
    /// server and sent only to the player it belongs to.
    ///
    /// Everything except the catcher is already known to everyone: characters
    /// are spawned objects whose transforms replicate, and isFrozen replicates
    /// too, so a runner's team-mates and a catcher's quarry need nothing extra.
    /// The catcher is the one piece of hidden information in the game, so it is
    /// the one thing computed here.
    ///
    /// The client is never asked whether it can see the catcher. The server
    /// works it out and syncs the answer to that connection alone, via
    /// SyncMode.Owner — a runner's client is not told where the catcher is until
    /// the server decides it has earned it.
    ///
    /// Worth being precise about the limit: this makes the minimap
    /// server-authoritative, not the catcher's position secret. The catcher is
    /// still a spawned object whose transform replicates to everyone so it can
    /// be drawn, so a modified client could read that directly. Closing that
    /// needs interest management to stop sending the catcher to runners who
    /// cannot see it, which is a separate change that fits on top of this one.
    /// </summary>
    [RequireComponent(typeof(PlayerRole))]
    public class MapAwareness : NetworkBehaviour
    {
        [SerializeField]
        [Tooltip("Used server-side to decide whether this character can see the catcher.")]
        private Vision _vision;

        [SerializeField]
        [Tooltip("Seconds between recalculating. It is a physics query, not a per-frame job.")]
        private float _refreshInterval = 0.2f;

        [SerializeField]
        [Tooltip("How long the blip lingers after losing sight, so the catcher stepping " +
                 "behind a building does not make it vanish instantly.")]
        private float _memorySeconds = 4f;

        [SyncVar]
        private Vector3 _catcherPosition;

        [SyncVar]
        private bool _catcherKnown;

        private PlayerRole _role;
        private Func<PlayerRole, Freezable, bool> _acceptCatcher;
        private float _nextRefresh;
        private float _lastSeenAt = float.NegativeInfinity;

        /// <summary>Whether this character's map should be drawing a catcher at all.</summary>
        public bool CatcherKnown => _catcherKnown;

        /// <summary>
        /// Where to draw the catcher. Only meaningful while
        /// <see cref="CatcherKnown"/> is true; it holds the last seen position
        /// during the memory window rather than a live one.
        /// </summary>
        public Vector3 CatcherPosition => _catcherPosition;

        private void Awake()
        {
            _role = GetComponent<PlayerRole>();

            if (_vision == null)
            {
                _vision = GetComponent<Vision>();
            }

            _acceptCatcher = (role, _) => role.Role == Role.Catcher;
        }

        [ServerCallback]
        private void Update()
        {
            if (Time.time < _nextRefresh)
            {
                return;
            }

            _nextRefresh = Time.time + _refreshInterval;
            Refresh(Time.time);
        }

        private void Refresh(float now)
        {
            // A catcher is told nothing here, because it already knows where
            // every runner is and runners replicate to it anyway. MapKnowledge
            // owns that rule; this just obeys it.
            if (!MapKnowledge.NeedsLineOfSight(_role.Role, Role.Catcher))
            {
                _catcherKnown = false;
                return;
            }

            Transform catcher = _vision != null
                ? _vision.FindNearestVisible(_acceptCatcher)
                : null;

            if (catcher != null)
            {
                _catcherPosition = catcher.position;
                _lastSeenAt = now;
                _catcherKnown = true;
                return;
            }

            _catcherKnown = now - _lastSeenAt <= _memorySeconds;
        }
    }
}
