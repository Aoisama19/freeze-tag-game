using BarafPaani.Core;
using BarafPaani.Gameplay;
using Mirror;
using UnityEngine;
using UnityEngine.UI;

namespace BarafPaani.UI
{
    /// <summary>
    /// The lobby: who is on which side, and how to start.
    ///
    /// This is what closes the gap where only the first player into a match got
    /// to pick a side. Everyone can now ask for one, and the server decides —
    /// see PlayerRole.RequestRole and LobbyRules.
    ///
    /// Only the host can start. In a friends-scale game that is the person who
    /// set it up, and a start button everyone can press is a start button
    /// somebody presses while two people are still choosing.
    /// </summary>
    public class LobbyHud : MonoBehaviour
    {
        [SerializeField]
        private Text _titleLabel;

        [SerializeField]
        private Text _rosterLabel;

        [SerializeField]
        private Text _hintLabel;

        private MatchState _match;
        private GameInput _input;

        private void OnEnable()
        {
            _input = new GameInput();
            _input.Player.Enable();
        }

        private void OnDisable()
        {
            if (_input == null)
            {
                return;
            }

            _input.Player.Disable();
            _input.Dispose();
            _input = null;
        }

        private void Update()
        {
            MatchState match = FindMatch();
            bool waiting = match != null && match.Phase == MatchPhase.Lobby;

            Show(waiting);

            if (!waiting)
            {
                return;
            }

            ShowRoster(match);
            ReadInput(match);
        }

        private void ShowRoster(MatchState match)
        {
            PlayerRole mine = MyRole();

            if (_titleLabel != null)
            {
                _titleLabel.text = "WAITING TO START";
            }

            if (_rosterLabel != null)
            {
                _rosterLabel.text = Roster(mine);
            }

            if (_hintLabel == null)
            {
                return;
            }

            if (!NetworkServer.active)
            {
                // A joiner cannot start the match, so telling them to press
                // anything would just be a button that does nothing.
                _hintLabel.text = "E  switch side          waiting for the host to start";
                return;
            }

            string blocked = WhyNotStarting();

            _hintLabel.text = blocked.Length > 0
                ? $"E  switch side          {blocked}"
                : "E  switch side          ENTER  start the match";
        }

        /// <summary>
        /// Reads the sides off the spawned characters rather than a list of its
        /// own. They are already the authoritative answer, and a second copy is
        /// a second thing to keep in step.
        /// </summary>
        private static string Roster(PlayerRole mine)
        {
            int catchers = 0;
            int runners = 0;

            foreach (NetworkIdentity identity in NetworkClient.spawned.Values)
            {
                if (identity == null
                    || !identity.TryGetComponent(out PlayerRole role)
                    || Gameplay.PowerUps.Decoy.Is(identity))
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

            string side = mine == null
                ? "not in the match yet"
                : mine.Role == Role.Catcher
                    ? "You are the CATCHER"
                    : "You are a RUNNER";

            return $"{side}\nCatching {catchers}      Running {runners}";
        }

        private string WhyNotStarting()
        {
            if (_match == null || NetworkManager.singleton is not GameNetworkManager manager)
            {
                return string.Empty;
            }

            _match.CountHumans(out int catchers, out int runners);

            return LobbyRules.WhyNotStarting(manager.FillWithBots, catchers, runners);
        }

        private void ReadInput(MatchState match)
        {
            if (_input == null)
            {
                return;
            }

            if (_input.Player.Interact.WasPressedThisFrame())
            {
                PlayerRole mine = MyRole();

                if (mine != null)
                {
                    mine.CmdRequestRole(mine.Role == Role.Catcher ? Role.Runner : Role.Catcher);
                }
            }

            // Server-side directly rather than through a command: the host is
            // already the server, and a command for it to send itself would be
            // a message with nowhere to go.
            if (NetworkServer.active && _input.Player.Attack.WasPressedThisFrame())
            {
                match.StartMatch();
            }
        }

        private static PlayerRole MyRole()
        {
            NetworkIdentity player = NetworkClient.localPlayer;

            return player != null ? player.GetComponent<PlayerRole>() : null;
        }

        private MatchState FindMatch()
        {
            if (_match == null)
            {
                _match = FindFirstObjectByType<MatchState>();
            }

            return _match;
        }

        private void Show(bool visible)
        {
            if (_titleLabel != null)
            {
                _titleLabel.enabled = visible;
            }

            if (_rosterLabel != null)
            {
                _rosterLabel.enabled = visible;
            }

            if (_hintLabel != null)
            {
                _hintLabel.enabled = visible;
            }
        }
    }
}
