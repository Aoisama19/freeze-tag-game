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

        [SyncVar]
        private MatchPhase _phase = MatchPhase.Lobby;

        private readonly List<Freezable> _freezables = new List<Freezable>();

        private float _nextTick;
        private double _restartAt;

        public int RunnersTotal => _runnersTotal;

        public int RunnersFree => _runnersFree;

        public MatchOutcome Outcome => _outcome;

        public MatchPhase Phase => _phase;

        /// <summary>
        /// Whether anyone can be frozen. False in the lobby, so people can wander
        /// about picking sides without the catcher starting early.
        /// </summary>
        public bool FreezingAllowed => _phase == MatchPhase.Playing;

        /// <summary>Time left in the round, worked out locally from the end time.</summary>
        public float SecondsRemaining =>
            Mathf.Max(0f, (float)(_endsAt - NetworkTime.time));

        public override void OnStartServer()
        {
            // Single-player has nobody to wait for and the side was already
            // chosen in the menu, so the lobby would be a screen you press past
            // every time. Hosting waits, because that is the point of it.
            if (NetworkManager.singleton is Core.GameNetworkManager manager
                && manager.ActiveMode != Core.GameMode.SinglePlayer)
            {
                OpenLobby();
                return;
            }

            BeginRound();
        }

        /// <summary>Puts the match back to waiting, with sides open to change.</summary>
        [Server]
        public void OpenLobby()
        {
            _phase = MatchPhase.Lobby;
            _outcome = MatchOutcome.InProgress;
            _endsAt = 0d;

            ThawEveryone();
            ClearPowerUps();
            RemoveDecoys();
            CountRunners();
        }

        /// <summary>
        /// Leaves the lobby and plays. Refused unless the sides make a match
        /// that can actually be won — the check that used to be missing, which
        /// is how a host could choose Runner with bots off and leave nobody
        /// catching.
        /// </summary>
        [Server]
        public bool StartMatch()
        {
            if (_phase != MatchPhase.Lobby)
            {
                return false;
            }

            CountHumans(out int catchers, out int runners);

            bool fillWithBots = NetworkManager.singleton is Core.GameNetworkManager manager
                && manager.FillWithBots;

            if (!LobbyRules.CanStart(fillWithBots, catchers, runners))
            {
                return false;
            }

            BeginRound();
            return true;
        }

        /// <summary>
        /// Counts the people, not the bots. What the lobby is deciding is
        /// whether the humans present cover both sides; bots fill the rest.
        /// </summary>
        [Server]
        public void CountHumans(out int catchers, out int runners)
        {
            catchers = 0;
            runners = 0;

            foreach (NetworkIdentity identity in NetworkServer.spawned.Values)
            {
                if (identity == null
                    || identity.connectionToClient == null
                    || !identity.TryGetComponent(out PlayerRole role)
                    || PowerUps.Decoy.Is(identity))
                {
                    continue;
                }

                if (role.Role == Role.Catcher)
                {
                    catchers++;
                    continue;
                }

                runners++;
            }
        }

        /// <summary>
        /// Whether another person is already catching, so the lobby can refuse a
        /// second.
        ///
        /// People only. A bot holding the seat is not a reason to tell somebody
        /// no — with bots on there is always an AI catcher the moment nobody
        /// human took it, so counting it here would mean a host who picked
        /// Runner in the menu could never change their mind.
        /// </summary>
        [Server]
        public bool HasHumanCatcherOtherThan(PlayerRole asking)
        {
            foreach (NetworkIdentity identity in NetworkServer.spawned.Values)
            {
                if (identity == null
                    || identity.connectionToClient == null
                    || !identity.TryGetComponent(out PlayerRole role)
                    || role == asking
                    || PowerUps.Decoy.Is(identity))
                {
                    continue;
                }

                if (role.Role == Role.Catcher)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Stands any bot catcher down to a runner, so the person taking the
        /// seat is the only one in it. The spare bot is tidied up by the usual
        /// headcount when the round begins.
        /// </summary>
        [Server]
        public void MakeWayForCatcher(PlayerRole taking)
        {
            foreach (NetworkIdentity identity in NetworkServer.spawned.Values)
            {
                if (identity == null
                    || identity.connectionToClient != null
                    || !identity.TryGetComponent(out PlayerRole role)
                    || role == taking
                    || PowerUps.Decoy.Is(identity))
                {
                    continue;
                }

                if (role.Role == Role.Catcher)
                {
                    role.SetRole(Role.Runner);
                }
            }
        }

        [ServerCallback]
        private void Update()
        {
            if (Time.time < _nextTick)
            {
                return;
            }

            _nextTick = Time.time + _tickInterval;

            if (_phase == MatchPhase.Lobby)
            {
                // Kept current so the lobby can show who is on which side.
                CountRunners();
                return;
            }

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
                    _phase = MatchPhase.Over;
                    _restartAt = NetworkTime.time + _restartDelay;
                }

                return;
            }

            if (NetworkTime.time >= _restartAt)
            {
                BeginRound();
            }
        }

        /// <summary>
        /// Starts a fresh round immediately, without waiting out the delay that
        /// follows a result. The same reset the end of a round performs.
        /// </summary>
        [Server]
        public void RestartNow()
        {
            BeginRound();
        }

        [Server]
        private void BeginRound()
        {
            _phase = MatchPhase.Playing;
            _endsAt = NetworkTime.time + RoundLength();
            _outcome = MatchOutcome.InProgress;

            ThawEveryone();
            ClearPowerUps();
            RemoveDecoys();
            ReturnEveryoneToSpawn();
            GrantSpawnImmunity();
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
                    || !identity.TryGetComponent(out Freezable freezable)

                    // A decoy is meant to look like a runner to the catcher, so
                    // it looks like one here too. Counted, it would add a runner
                    // who can never be caught for good and a round nobody wins.
                    || PowerUps.Decoy.Is(identity))
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

        /// <summary>
        /// Nobody starts a round mid-sprint or holding what they saved from the
        /// last one. An effect left running across a restart is the sort of
        /// thing that looks like a physics bug rather than a stale flag.
        /// </summary>
        [Server]
        private void ClearPowerUps()
        {
            foreach (NetworkIdentity identity in NetworkServer.spawned.Values)
            {
                if (identity == null)
                {
                    continue;
                }

                if (identity.TryGetComponent(out PowerUps.PowerUpEffects effects))
                {
                    effects.ClearAll();
                }

                if (identity.TryGetComponent(out PowerUps.PowerUpHolder holder))
                {
                    holder.Clear();
                }
            }
        }

        /// <summary>
        /// Clears away anyone's clones. A decoy outliving the round it was made
        /// in would stand in the street for the rest of the match.
        /// </summary>
        [Server]
        private void RemoveDecoys()
        {
            // Collected first: destroying while walking NetworkServer.spawned
            // would be modifying the collection being iterated.
            List<NetworkIdentity> decoys = new List<NetworkIdentity>();

            foreach (NetworkIdentity identity in NetworkServer.spawned.Values)
            {
                if (PowerUps.Decoy.Is(identity))
                {
                    decoys.Add(identity);
                }
            }

            foreach (NetworkIdentity decoy in decoys)
            {
                if (decoy != null)
                {
                    NetworkServer.Destroy(decoy.gameObject);
                }
            }
        }

        /// <summary>
        /// How long this round runs for. The menu's answer if it gave one,
        /// otherwise this component's own setting — which is what a map scene
        /// opened on its own uses.
        /// </summary>
        private float RoundLength()
        {
            if (NetworkManager.singleton is Core.GameNetworkManager manager
                && manager.RoundSeconds > 0f)
            {
                return manager.RoundSeconds;
            }

            return _roundSeconds;
        }

        /// <summary>
        /// Everyone is safe for a moment at the start of a round.
        ///
        /// Without it a catcher who happens to be standing near a spawn point
        /// when the round begins takes whoever lands there before they have had
        /// a frame to move, which reads as the game being broken rather than
        /// the catcher being quick.
        /// </summary>
        [Server]
        private void GrantSpawnImmunity()
        {
            float seconds = NetworkManager.singleton is Core.GameNetworkManager manager
                ? manager.SpawnImmunitySeconds
                : 3f;

            foreach (Freezable freezable in _freezables)
            {
                if (freezable != null)
                {
                    freezable.GrantImmunity(seconds);
                }
            }
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
