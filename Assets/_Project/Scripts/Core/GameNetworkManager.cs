using BarafPaani.Gameplay;
using Mirror;
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
            if (player.TryGetComponent(out PlayerRole role))
            {
                role.SetRole(numPlayers == 0 ? Role.Catcher : Role.Runner);
            }

            NetworkServer.AddPlayerForConnection(conn, player);
        }

        public void JoinMultiplayer(string address)
        {
            ActiveMode = GameMode.Multiplayer;
            networkAddress = address;
            StartClient();
        }
    }
}
