using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.AI;

namespace BarafPaani.Gameplay
{
    /// <summary>
    /// Runs the round: counts who is left, decides when it is over, and starts
    /// the next one.
    ///
    /// The server owns all of it and the results are replicated, so every client
    /// agrees on the score and nobody can talk themselves into a win. Clients
    /// read the counts to draw the HUD and nothing else.
    ///
    /// The clock is synced as the network time the round ends at, not as a
    /// counter ticking down. A counter would need syncing constantly and would
    /// still drift; an end time is sent once and every client subtracts for
    /// itself.
    /// </summary>
    public class MatchState : NetworkBehaviour
    {
        [SerializeField]
        [Tooltip("Length of a round. Runners win by surviving it.")]
        private float _roundSeconds = 180f;

        [SerializeField]
        [Tooltip("How long the result stays up before the next round starts.")]
        private float _restartDelay = 6f;

        [SerializeField]
        [Tooltip("Seconds between recounts. Freezes do not need catching the same frame.")]
        private float _tickInterval = 0.25f;

        [SyncVar]
        private int _runnersTotal;

        [SyncVar]
        private int _runnersFree;

        [SyncVar]
        private double _endsAt;

        [SyncVar]
        private MatchOutcome _outcome = MatchOutcome.InProgress;

        private readonly List<Freezable> _freezables = new List<Freezable>();

        private float _nextTick;
        private double _restartAt;

        public int RunnersTotal => _runnersTotal;

        public int RunnersFree => _runnersFree;

        public MatchOutcome Outcome => _outcome;

        /// <summary>Time left in the round, worked out locally from the end time.</summary>
        public float SecondsRemaining =>
            Mathf.Max(0f, (float)(_endsAt - NetworkTime.time));

        public override void OnStartServer()
        {
            BeginRound();
        }

        [ServerCallback]
        private void Update()
        {
            if (Time.time < _nextTick)
            {
                return;
            }

            _nextTick = Time.time + _tickInterval;

            // A round with no end time has not been started. Guards against ever
            // silently running a match whose clock reads as already expired.
            if (_endsAt <= 0d)
            {
                BeginRound();
                return;
            }

            Tick();
        }

        private void Tick()
        {
            CountRunners();

            if (_outcome == MatchOutcome.InProgress)
            {
                _outcome = MatchRules.Resolve(_runnersTotal, _runnersFree, SecondsRemaining);

                if (_outcome != MatchOutcome.InProgress)
                {
                    _restartAt = NetworkTime.time + _restartDelay;
                }

                return;
            }

            if (NetworkTime.time >= _restartAt)
            {
                BeginRound();
            }
        }

        [Server]
        private void BeginRound()
        {
            _endsAt = NetworkTime.time + _roundSeconds;
            _outcome = MatchOutcome.InProgress;

            ThawEveryone();
            ReturnEveryoneToSpawn();
            CountRunners();
        }

        private void CountRunners()
        {
            int total = 0;
            int free = 0;

            foreach (NetworkIdentity identity in NetworkServer.spawned.Values)
            {
                if (identity == null
                    || !identity.TryGetComponent(out PlayerRole role)
                    || role.Role != Role.Runner
                    || !identity.TryGetComponent(out Freezable freezable))
                {
                    continue;
                }

                total++;

                if (!freezable.IsFrozen)
                {
                    free++;
                }
            }

            _runnersTotal = total;
            _runnersFree = free;
        }

        [Server]
        private void ThawEveryone()
        {
            _freezables.Clear();

            foreach (NetworkIdentity identity in NetworkServer.spawned.Values)
            {
                if (identity != null && identity.TryGetComponent(out Freezable freezable))
                {
                    _freezables.Add(freezable);
                }
            }

            foreach (Freezable freezable in _freezables)
            {
                freezable.Unfreeze();
            }
        }

        /// <summary>
        /// Puts everyone back on a spawn point for the new round.
        ///
        /// Players own their own transforms — movement syncs client to server —
        /// so setting a position here would be overwritten by the next update
        /// from the client. RpcTeleport is Mirror's way of saying the move is
        /// authoritative. Agents are server-driven and just get moved.
        /// </summary>
        [Server]
        private void ReturnEveryoneToSpawn()
        {
            if (NetworkManager.singleton == null)
            {
                return;
            }

            foreach (NetworkIdentity identity in NetworkServer.spawned.Values)
            {
                if (identity == null || !identity.TryGetComponent(out PlayerRole _))
                {
                    continue;
                }

                Transform start = NetworkManager.singleton.GetStartPosition();

                if (start == null)
                {
                    continue;
                }

                if (identity.TryGetComponent(out NetworkTransformReliable sync))
                {
                    sync.RpcTeleport(start.position, start.rotation);
                }

                // An agent drives its own transform and ignores being written to
                // directly, so it has to be warped rather than moved.
                if (identity.TryGetComponent(out NavMeshAgent agent) && agent.isOnNavMesh)
                {
                    agent.Warp(start.position);
                    identity.transform.rotation = start.rotation;
                    continue;
                }

                identity.transform.SetPositionAndRotation(start.position, start.rotation);
            }
        }
    }
}
