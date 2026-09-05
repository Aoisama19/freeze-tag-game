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
        }

        public override void OnStartServer()
        {
            base.OnStartServer();

            CheckNavMeshes();

            // Single-player is a host with nobody else in it, so the AI has to
            // provide the opposition. Multiplayer gets AI later, once we know
            // how many humans actually turned up.
            if (ActiveMode == GameMode.SinglePlayer)
            {
                SpawnAiCharacters();
            }
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

            // Somebody has to catch. If the human is not doing it, an AI does.
            if (_humanRole != Role.Catcher)
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
