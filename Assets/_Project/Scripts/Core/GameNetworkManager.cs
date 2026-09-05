using System.Collections.Generic;
using BarafPaani.AI;
using BarafPaani.Gameplay;
using Mirror;
using Unity.AI.Navigation;
using UnityEngine;

namespace BarafPaani.Core
{
    /// <summary>
    /// The single entry point for both game modes.
    ///
    /// Single-player is a Mirror host that nobody can join; multiplayer is the
    /// same host with the door open. That is the whole difference. Nothing
    /// downstream of this class should ask which mode it is running in — see
    /// docs/architecture.md for why the old build's split into two codebases
    /// is the thing we are avoiding.
    /// </summary>
    public class GameNetworkManager : NetworkManager
    {
        [Header("Baraf-Paani")]
        [SerializeField]
        [Tooltip("Remote players allowed in when hosting a multiplayer match.")]
        private int _multiplayerMaxConnections = 8;

        [SerializeField]
        [Tooltip("Character used for AI-controlled runners.")]
        private GameObject _aiCharacterPrefab;

        [SerializeField]
        [Tooltip("How many runners a match should have, humans and AI together. " +
                 "Bots fill the gaps and step out again as people arrive.")]
        private int _targetRunners = 3;

        [SerializeField]
        [Tooltip("Off means no AI at all — the match is whoever turns up.")]
        private bool _fillWithBots = true;

        /// <summary>
        /// Whether bots make up the numbers. Settable so the menu can decide it
        /// before the host comes up.
        /// </summary>
        public bool FillWithBots
        {
            get => _fillWithBots;
            set => _fillWithBots = value;
        }

        /// <summary>Runners a match aims for, humans and bots together.</summary>
        public int TargetRunners
        {
            get => _targetRunners;
            set => _targetRunners = Mathf.Clamp(value, 0, MatchSetup.MaxBotRunners);
        }

        [SerializeField]
        [Tooltip("Which side you play. Set it to Runner and an AI takes the catcher role, " +
                 "so the chasing and guarding behaviour can be watched from the other side.")]
        private Role _humanRole = Role.Catcher;

        /// <summary>
        /// Which side the local player takes. Settable so the menu can choose it
        /// before the host comes up; the inspector value is the fallback when the
        /// game scene is opened on its own.
        /// </summary>
        public Role HumanRole
        {
            get => _humanRole;
            set => _humanRole = value;
        }

        /// <summary>How this session was started. Set before the host comes up.</summary>
        public GameMode ActiveMode { get; private set; } = GameMode.SinglePlayer;

        /// <summary>
        /// Starts a host with no room for remote clients. Mirror only enforces
        /// maxConnections on connections arriving through the transport, and it
        /// reserves connectionId 0 for the host's own player, so zero here locks
        /// the door without locking us out of our own game.
        /// </summary>
        public void StartSinglePlayer()
        {
            ActiveMode = GameMode.SinglePlayer;
            maxConnections = 0;
            StartHost();
        }

        public void StartMultiplayerHost()
        {
            ActiveMode = GameMode.Multiplayer;
            maxConnections = _multiplayerMaxConnections;
            StartHost();
        }

        /// <summary>
        /// Spawns a player and gives it a role. First one into the match is the
        /// catcher, everyone after is a runner — a placeholder until the menu
        /// does real role selection.
        ///
        /// This does the spawn itself instead of calling base, because the role
        /// has to be set *before* the object is spawned. In host mode Mirror
        /// serialises the spawn payload inside AddPlayerForConnection and then
        /// deserialises it straight back onto the very same object, so anything
        /// written after that call is silently overwritten with the pre-spawn
        /// value. Setting the role afterwards left every player a runner, and
        /// so nothing could ever freeze.
        /// </summary>
        public override void OnServerAddPlayer(NetworkConnectionToClient conn)
        {
            if (conn.identity != null)
            {
                Debug.LogError("There is already a player for this connection.");
                return;
            }

            Transform start = GetStartPosition();

            GameObject player = start != null
                ? Instantiate(playerPrefab, start.position, start.rotation)
                : Instantiate(playerPrefab);

            player.name = $"{playerPrefab.name} [connId={conn.connectionId}]";

            // Counted before the spawn, so the first player in sees zero.
            bool isFirstIn = numPlayers == 0;

            if (player.TryGetComponent(out PlayerRole role))
            {
                role.SetRole(isFirstIn ? _humanRole : Role.Runner);
            }

            NetworkServer.AddPlayerForConnection(conn, player);

            // After the human is in, so they are counted rather than added on top.
            FillWithAi();
        }

        public override void OnStartServer()
        {
            base.OnStartServer();

            CheckNavMeshes();
        }

        /// <summary>
        /// Tops the match back up after somebody leaves, so a match does not
        /// quietly empty out as people drop.
        /// </summary>
        public override void OnServerDisconnect(NetworkConnectionToClient conn)
        {
            base.OnServerDisconnect(conn);
            FillWithAi();
        }

        /// <summary>
        /// Checks the map came with navigation attached, rather than baking it.
        ///
        /// This used to call BuildNavMesh at server start, which worked in the
        /// editor and silently would not have worked in a build: runtime baking
        /// needs Read/Write enabled on every source mesh, and the map's are not
        /// readable. Unity warns about exactly this — "will work in playmode in
        /// the editor but not in player" — and the shipped game would have had
        /// no navigation at all, so the AI would simply have stood still.
        ///
        /// The bake is done once at edit time by the scene builder and committed
        /// as an asset. All that is left to do here is notice if it is missing.
        /// </summary>
        private static void CheckNavMeshes()
        {
            NavMeshSurface[] surfaces =
                FindObjectsByType<NavMeshSurface>(FindObjectsSortMode.None);

            if (surfaces.Length == 0)
            {
                Debug.LogWarning("No NavMeshSurface in the scene, so the AI has nowhere to walk.");
                return;
            }

            foreach (NavMeshSurface surface in surfaces)
            {
                if (surface.navMeshData == null)
                {
                    Debug.LogError(
                        $"'{surface.name}' has no baked NavMesh. Run " +
                        "Baraf-Paani > Rebuild Playable Scene.", surface);
                }
            }
        }

        /// <summary>
        /// Brings the match up to strength with AI, and lets bots step aside as
        /// people arrive.
        ///
        /// Keyed on headcount rather than on the game mode. AI used to be a
        /// single-player special case, which meant hosting a match and waiting
        /// for friends left you alone in an empty city. Counting instead means
        /// hosting alone plays exactly like single-player and thins out as
        /// people join, which is what "single-player is a host with nobody
        /// connected" should mean in practice.
        ///
        /// Safe to call repeatedly: it works from what is actually in the game
        /// rather than from what it did last time.
        /// </summary>
        private void FillWithAi()
        {
            if (!_fillWithBots)
            {
                // Asked for a match of people only. Say so once if that has left
                // nobody catching, rather than letting a dead match look like a
                // bug later.
                WarnIfNobodyIsCatching();
                return;
            }

            if (_aiCharacterPrefab == null)
            {
                Debug.LogWarning("No AI character prefab is set, so nobody can be caught.", this);
                return;
            }

            int catchers = 0;
            int runners = 0;
            List<NetworkIdentity> spareBots = new List<NetworkIdentity>();

            foreach (NetworkIdentity identity in NetworkServer.spawned.Values)
            {
                if (identity == null || !identity.TryGetComponent(out PlayerRole role))
                {
                    continue;
                }

                if (role.Role == Role.Catcher)
                {
                    catchers++;
                    continue;
                }

                runners++;

                // Only bots can be asked to leave.
                if (identity.TryGetComponent(out AiBrain _))
                {
                    spareBots.Add(identity);
                }
            }

            // Somebody has to catch. If no human took it, an AI does.
            if (catchers == 0)
            {
                SpawnAiCharacter(Role.Catcher, "AI Catcher");
            }

            for (int i = runners; i < _targetRunners; i++)
            {
                SpawnAiCharacter(Role.Runner, $"AI Runner {i + 1}");
            }

            // A human arrived and the match is over strength, so a bot steps out.
            for (int i = 0; i < runners - _targetRunners && spareBots.Count > 0; i++)
            {
                NetworkIdentity bot = spareBots[spareBots.Count - 1];
                spareBots.RemoveAt(spareBots.Count - 1);
                NetworkServer.Destroy(bot.gameObject);
            }
        }

        /// <summary>
        /// With bots off, only a human can catch — and right now only the first
        /// player into a match gets to choose a side, so a host who picked Runner
        /// leaves a match nobody can lose.
        /// </summary>
        private static void WarnIfNobodyIsCatching()
        {
            foreach (NetworkIdentity identity in NetworkServer.spawned.Values)
            {
                if (identity != null
                    && identity.TryGetComponent(out PlayerRole role)
                    && role.Role == Role.Catcher)
                {
                    return;
                }
            }

            Debug.LogWarning(
                "Bots are off and nobody is the catcher, so this match cannot be won.");
        }

        private void SpawnAiCharacter(Role role, string name)
        {
            Transform start = GetStartPosition();

            GameObject character = start != null
                ? Instantiate(_aiCharacterPrefab, start.position, start.rotation)
                : Instantiate(_aiCharacterPrefab);

            character.name = name;

            // Before the spawn, for exactly the same reason as human players.
            if (character.TryGetComponent(out PlayerRole playerRole))
            {
                playerRole.SetRole(role);
            }

            NetworkServer.Spawn(character);
        }

        public void JoinMultiplayer(string address)
        {
            ActiveMode = GameMode.Multiplayer;
            networkAddress = address;
            StartClient();
        }
    }
}
