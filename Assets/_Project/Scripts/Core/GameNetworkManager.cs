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
        [Tooltip("AI runners spawned to fill out a single-player match.")]
        private int _singlePlayerRunners = 3;

        [SerializeField]
        [Tooltip("Off makes you a runner and hands the catcher role to an AI, " +
                 "so the chasing and guarding behaviour can be watched from the other side.")]
        private bool _humanIsCatcher = true;

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
                role.SetRole(isFirstIn && _humanIsCatcher ? Role.Catcher : Role.Runner);
            }

            NetworkServer.AddPlayerForConnection(conn, player);
        }

        public override void OnStartServer()
        {
            base.OnStartServer();

            BuildNavMeshes();

            // Single-player is a host with nobody else in it, so the AI has to
            // provide the opposition. Multiplayer gets AI later, once we know
            // how many humans actually turned up.
            if (ActiveMode == GameMode.SinglePlayer)
            {
                SpawnAiCharacters();
            }
        }

        /// <summary>
        /// Bakes the map's NavMesh before any agent spawns.
        ///
        /// Done at runtime rather than committed as a baked asset: only the
        /// server needs one, it cannot go stale against the geometry, and it
        /// keeps a binary out of the repo. Worth revisiting for the real maps,
        /// where bake time will matter more than it does on a flat plane.
        /// </summary>
        private static void BuildNavMeshes()
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
                surface.BuildNavMesh();
            }
        }

        /// <summary>
        /// Fills the match out with AI. When the human is a runner an AI takes
        /// the catcher role, so the chasing and guarding side can be watched
        /// from the other end rather than only inferred from being caught.
        /// </summary>
        private void SpawnAiCharacters()
        {
            if (_aiCharacterPrefab == null)
            {
                Debug.LogWarning(
                    "No AI character prefab is set, so single-player has nobody to catch.", this);
                return;
            }

            if (!_humanIsCatcher)
            {
                SpawnAiCharacter(Role.Catcher, "AI Catcher");
            }

            for (int i = 0; i < _singlePlayerRunners; i++)
            {
                SpawnAiCharacter(Role.Runner, $"AI Runner {i + 1}");
            }
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
