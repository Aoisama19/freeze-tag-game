using UnityEngine;

namespace BarafPaani.Core
{
    /// <summary>
    /// Starts the match the menu asked for, once the game scene is up.
    ///
    /// Only acts on an explicit request, so opening the game scene on its own
    /// still does nothing until you press something — which is what keeps the
    /// scene usable on its own and keeps the tests, which start their own hosts,
    /// from fighting an auto-start.
    /// </summary>
    [RequireComponent(typeof(GameNetworkManager))]
    public class GameLauncher : MonoBehaviour
    {
        [SerializeField]
        private MatchSetup _setup;

        private void Start()
        {
            if (_setup == null || !_setup.LaunchRequested)
            {
                return;
            }

            GameNetworkManager manager = GetComponent<GameNetworkManager>();

            // Consumed first: if starting throws, we do not want the next visit
            // to the scene retrying it forever.
            GameMode mode = _setup.Mode;
            string address = _setup.JoinAddress;
            manager.HumanRole = _setup.HumanRole;
            manager.FillWithBots = _setup.FillWithBots;
            manager.TargetRunners = _setup.BotRunners;
            manager.RoundSeconds = _setup.RoundSeconds;
            _setup.ClearRequest();

            switch (mode)
            {
                case GameMode.Multiplayer:
                    manager.StartMultiplayerHost();
                    return;

                case GameMode.MultiplayerJoin:
                    manager.JoinMultiplayer(address);
                    return;

                default:
                    manager.StartSinglePlayer();
                    return;
            }
        }
    }
}
