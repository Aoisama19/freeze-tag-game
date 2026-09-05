using BarafPaani.Core;
using BarafPaani.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BarafPaani.UI
{
    /// <summary>
    /// The main menu. Records what was chosen and loads the game.
    ///
    /// It starts nothing itself. The choice goes into MatchSetup and the game
    /// scene acts on it, which keeps the two scenes independent: the game scene
    /// still runs on its own when opened directly, and the menu does not need to
    /// know how a host is started.
    /// </summary>
    public class MenuController : MonoBehaviour
    {
        [SerializeField]
        private MatchSetup _setup;

        [Header("Role")]
        [SerializeField]
        private Button _catcherButton;

        [SerializeField]
        private Button _runnerButton;

        [SerializeField]
        private Text _roleLabel;

        [Header("Play")]
        [SerializeField]
        private Button _singlePlayerButton;

        [SerializeField]
        private Button _hostButton;

        [SerializeField]
        private Button _joinButton;

        [SerializeField]
        private InputField _addressField;

        [SerializeField]
        private Button _quitButton;

        [Header("Scenes")]
        [SerializeField]
        private string _gameScene = "Game";

        [Header("Look")]
        [SerializeField]
        private Color _chosen = new Color(1f, 0.85f, 0.3f);

        [SerializeField]
        private Color _unchosen = new Color(1f, 1f, 1f, 0.55f);

        private Role _role = Role.Catcher;

        private void Start()
        {
            if (_setup != null)
            {
                // Anything left over from a previous run is stale.
                _setup.ClearRequest();
                _role = _setup.HumanRole;
            }

            Wire(_catcherButton, () => ChooseRole(Role.Catcher));
            Wire(_runnerButton, () => ChooseRole(Role.Runner));

            Wire(_singlePlayerButton, () => Launch(GameMode.SinglePlayer));
            Wire(_hostButton, () => Launch(GameMode.Multiplayer));
            Wire(_joinButton, () => Launch(GameMode.MultiplayerJoin));

            Wire(_quitButton, Quit);

            ShowRole();
        }

        private static void Wire(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }

        private void ChooseRole(Role role)
        {
            _role = role;
            ShowRole();
        }

        private void ShowRole()
        {
            if (_roleLabel != null)
            {
                _roleLabel.text = _role == Role.Catcher
                    ? "You are the catcher"
                    : "You are a runner — an AI catches";
            }

            Tint(_catcherButton, _role == Role.Catcher);
            Tint(_runnerButton, _role == Role.Runner);
        }

        private void Tint(Button button, bool selected)
        {
            if (button == null)
            {
                return;
            }

            Image image = button.GetComponent<Image>();

            if (image != null)
            {
                image.color = selected ? _chosen : _unchosen;
            }
        }

        private void Launch(GameMode mode)
        {
            if (_setup == null)
            {
                Debug.LogError("The menu has no MatchSetup, so it cannot say what to start.", this);
                return;
            }

            // A joiner never picks a side: whoever is hosting hands roles out.
            Role role = mode == GameMode.MultiplayerJoin ? Role.Runner : _role;

            _setup.Request(mode, role, _addressField != null ? _addressField.text : null);
            SceneManager.LoadScene(_gameScene, LoadSceneMode.Single);
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
