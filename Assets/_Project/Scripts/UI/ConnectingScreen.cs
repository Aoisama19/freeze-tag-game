using BarafPaani.Core;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BarafPaani.UI
{
    /// <summary>
    /// Where a joining player waits while the connection is made.
    ///
    /// This scene exists so a client never loads a map of its own. A client
    /// needs a NetworkManager to connect through, and every one of those used
    /// to live inside a map — so joining meant loading whichever map the
    /// joiner's own menu happened to have selected, connecting, and only then
    /// being moved to the host's. Two loads, the first of them wrong.
    ///
    /// This scene is almost empty, so it loads instantly. The server then sends
    /// its own scene name as the client authenticates and the client loads the
    /// host's map directly. One map load, and it is the right one.
    ///
    /// It also gives a failed join somewhere to be reported. Before this, a
    /// client that could not reach the host sat in a game map with no players
    /// and no explanation.
    /// </summary>
    public class ConnectingScreen : MonoBehaviour
    {
        [SerializeField]
        private Text _statusLabel;

        [SerializeField]
        private Button _backButton;

        [SerializeField]
        private MatchSetup _setup;

        [SerializeField]
        [Tooltip("Menu to return to when the join fails or is given up on.")]
        private string _menuScene = "MainMenu";

        [SerializeField]
        [Tooltip("Seconds to keep trying before saying so. Mirror itself does not time out quickly.")]
        private float _patience = 12f;

        private float _waitingSince;
        private bool _everConnected;
        private bool _givenUp;

        private void Start()
        {
            _waitingSince = Time.time;

            if (_backButton != null)
            {
                _backButton.onClick.RemoveAllListeners();
                _backButton.onClick.AddListener(Leave);
            }
        }

        private void Update()
        {
            if (_statusLabel == null || _givenUp)
            {
                return;
            }

            string host = _setup != null ? _setup.JoinAddress : "the host";

            if (NetworkClient.isConnected)
            {
                // Nothing more to do here. The server sends its scene as part of
                // authenticating, and Mirror loads it, which takes this screen
                // with it.
                _everConnected = true;
                _statusLabel.text = "Connected. Loading the host's map...";
                return;
            }

            if (NetworkClient.isConnecting)
            {
                float waited = Time.time - _waitingSince;

                _statusLabel.text = waited < _patience
                    ? $"Connecting to {host}..."
                    : $"Still trying {host}. It may be the wrong address, or the host may not be up.";

                return;
            }

            // Not connecting and not connected. Either the attempt was refused,
            // or it dropped after being made.
            _givenUp = true;

            _statusLabel.text = _everConnected
                ? "The host closed the match."
                : $"Could not reach {host}.";
        }

        private void Leave()
        {
            if (NetworkClient.active)
            {
                NetworkManager.singleton?.StopClient();
            }

            // The manager is DontDestroyOnLoad and would otherwise follow us
            // back to the menu and sit there half-connected.
            if (NetworkManager.singleton != null)
            {
                Destroy(NetworkManager.singleton.gameObject);
            }

            SceneManager.LoadScene(_menuScene, LoadSceneMode.Single);
        }
    }
}
