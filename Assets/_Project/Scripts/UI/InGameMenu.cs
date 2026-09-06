using BarafPaani.Gameplay;
using Mirror;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BarafPaani.UI
{
    /// <summary>
    /// The menu you can reach without closing the game: resume, leave the
    /// match, or quit.
    ///
    /// Not a pause. This is a networked game and the world does not stop for
    /// one player — everyone else keeps running while you are reading it. What
    /// it does stop is your own movement and camera, so that reaching for a
    /// button does not also sprint you into a wall and spin the view.
    ///
    /// The key is bound here in code rather than in the input asset. Editing
    /// that asset regenerates GameInput.cs, which cannot be compiled until the
    /// scripts that use it already exist — a loop this project has been round
    /// once already.
    /// </summary>
    public class InGameMenu : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Everything that makes up the overlay. Hidden until asked for.")]
        private GameObject _panel;

        [SerializeField]
        private Button _resumeButton;

        [SerializeField]
        private Button _leaveButton;

        [SerializeField]
        private Button _quitButton;

        [SerializeField]
        [Tooltip("Opened from here, and closed by Escape before this menu is.")]
        private SettingsPanel _settings;

        [SerializeField]
        private string _menuScene = "MainMenu";

        private InputAction _toggle;
        private bool _open;

        private void Awake()
        {
            _toggle = new InputAction("InGameMenu", InputActionType.Button, "<Keyboard>/escape");
            _toggle.AddBinding("<Gamepad>/start");

            Wire(_resumeButton, Close);
            Wire(_leaveButton, Leave);
            Wire(_quitButton, Quit);

            Show(false);
        }

        private void OnEnable() => _toggle.Enable();

        private void OnDisable()
        {
            _toggle.Disable();

            // Whatever was switched off for the overlay goes back on, or leaving
            // the scene with it open would strand the next match unable to move.
            SetPlayerInput(true);
        }

        private void OnDestroy() => _toggle.Dispose();

        private static void Wire(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }

        private void Update()
        {
            if (!_toggle.WasPressedThisFrame())
            {
                return;
            }

            // Escape backs out one layer at a time. Pressed over the settings it
            // closes those and leaves this menu up, rather than dropping the
            // player straight back into a match they were not looking at.
            if (_settings != null && _settings.IsOpen)
            {
                _settings.CloseNow();
                return;
            }

            Show(!_open);
        }

        private void Close() => Show(false);

        private void Show(bool open)
        {
            _open = open;

            // Closing this closes what it opened, or the settings would be left
            // hanging over the match with no menu behind them.
            if (!open && _settings != null && _settings.IsOpen)
            {
                _settings.CloseNow();
            }

            if (_panel != null)
            {
                _panel.SetActive(open);
            }

            SetPlayerInput(!open);
        }

        /// <summary>
        /// Turns this player's own controls on and off.
        ///
        /// Only this player's. Freezing, the round clock and everyone else carry
        /// on exactly as before, because they are the server's business and it
        /// has not stopped for anyone.
        /// </summary>
        private void SetPlayerInput(bool enabled)
        {
            NetworkIdentity me = NetworkClient.localPlayer;

            if (me != null && me.TryGetComponent(out PlayerMotor motor))
            {
                motor.enabled = enabled;
            }

            // The camera would otherwise swing as the mouse crosses the screen
            // towards a button.
            CinemachineInputAxisController look =
                FindFirstObjectByType<CinemachineInputAxisController>();

            if (look != null)
            {
                look.enabled = enabled;
            }
        }

        private void Leave()
        {
            if (NetworkServer.active)
            {
                NetworkManager.singleton?.StopHost();
            }
            else if (NetworkClient.active)
            {
                NetworkManager.singleton?.StopClient();
            }

            // DontDestroyOnLoad, so without this it follows us back to the menu
            // and the next match finds a manager that is already half running.
            if (NetworkManager.singleton != null)
            {
                Destroy(NetworkManager.singleton.gameObject);
            }

            SceneManager.LoadScene(_menuScene, LoadSceneMode.Single);
        }

        private static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
