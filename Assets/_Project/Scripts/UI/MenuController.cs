using BarafPaani.Core;
using BarafPaani.Gameplay;
using UnityEngine;
using System.Collections.Generic;
using System.Text;
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

        [Header("Bots")]
        [SerializeField]
        private Toggle _fillWithBotsToggle;

        [SerializeField]
        private InputField _botCountField;

        [SerializeField]
        private Text _botsLabel;

        [Header("Map")]
        [SerializeField]
        private Button _mapButton;

        [SerializeField]
        private Text _mapLabel;

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
        private string _gameScene = "Game_3Talwaar";

        [SerializeField]
        [Tooltip("Where a joining player waits. Never a map — the host decides that.")]
        private string _connectingScene = "Connecting";

        [Header("Look")]
        [SerializeField]
        private Color _chosen = new Color(1f, 0.85f, 0.3f);

        [SerializeField]
        private Color _unchosen = new Color(1f, 1f, 1f, 0.55f);

        private Role _role = Role.Catcher;

        private readonly List<string> _maps = new List<string>();
        private int _map;

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

            FindMaps();
            Wire(_mapButton, NextMap);

            if (_fillWithBotsToggle != null)
            {
                _fillWithBotsToggle.isOn = _setup == null || _setup.FillWithBots;
                _fillWithBotsToggle.onValueChanged.RemoveAllListeners();
                _fillWithBotsToggle.onValueChanged.AddListener(_ => ShowBots());
            }

            if (_botCountField != null)
            {
                _botCountField.contentType = InputField.ContentType.IntegerNumber;
                _botCountField.text = (_setup != null ? _setup.BotRunners : 3).ToString();
                _botCountField.onEndEdit.RemoveAllListeners();

                // Clamped as it is typed, so the field cannot hold a number the
                // match would refuse anyway.
                _botCountField.onEndEdit.AddListener(_ => _botCountField.text = BotCount().ToString());
            }

            ShowRole();
            ShowBots();
            ShowMap();
        }

        /// <summary>
        /// The maps, read out of the build settings rather than listed here.
        ///
        /// A map added to the builder and forgotten in a list in the menu is a
        /// map nobody can reach; one listed here and missing from the build is a
        /// button that fails only in a player. Reading the build is the version
        /// where neither can happen.
        /// </summary>
        private void FindMaps()
        {
            _maps.Clear();

            for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            {
                string name = System.IO.Path.GetFileNameWithoutExtension(
                    SceneUtility.GetScenePathByBuildIndex(i));

                if (name.StartsWith("Game_"))
                {
                    _maps.Add(name);
                }
            }

            if (_maps.Count == 0)
            {
                // Falls back to whatever the scene field already says, so the
                // menu still works in a project mid-rebuild.
                _maps.Add(_gameScene);
            }

            int remembered = _setup != null ? _maps.IndexOf(_setup.MapScene) : -1;
            _map = remembered >= 0 ? remembered : 0;
        }

        private void NextMap()
        {
            _map = (_map + 1) % _maps.Count;
            ShowMap();
        }

        private void ShowMap()
        {
            if (_maps.Count == 0)
            {
                return;
            }

            _gameScene = _maps[_map];
            _setup?.ChooseMap(_gameScene);

            if (_mapLabel != null)
            {
                _mapLabel.text = $"Map    {Pretty(_maps[_map])}";
            }
        }

        /// <summary>Turns a scene name like Game_BadshahiMasjid into "Badshahi Masjid".</summary>
        private static string Pretty(string sceneName)
        {
            string bare = sceneName.StartsWith("Game_") ? sceneName.Substring(5) : sceneName;
            StringBuilder text = new StringBuilder();

            for (int i = 0; i < bare.Length; i++)
            {
                // A capital after a lower-case letter starts a new word. Leading
                // digits stay attached, so "3Talwaar" does not become "3 Talwaar"
                // with a stray space in front.
                if (i > 0 && char.IsUpper(bare[i]) && !char.IsUpper(bare[i - 1]))
                {
                    text.Append(' ');
                }

                text.Append(bare[i]);
            }

            return text.ToString();
        }

        /// <summary>How many runners the match should aim for, kept in range.</summary>
        private int BotCount()
        {
            if (_botCountField == null || !int.TryParse(_botCountField.text, out int count))
            {
                return 3;
            }

            return Mathf.Clamp(count, 0, MatchSetup.MaxBotRunners);
        }

        private void ShowBots()
        {
            bool fill = _fillWithBotsToggle == null || _fillWithBotsToggle.isOn;

            if (_botCountField != null)
            {
                _botCountField.interactable = fill;
            }

            if (_botsLabel != null)
            {
                _botsLabel.text = fill
                    ? $"Runners to fill (max {MatchSetup.MaxBotRunners})"
                    : "No bots — people only";
            }
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

            bool fill = _fillWithBotsToggle == null || _fillWithBotsToggle.isOn;

            _setup.Request(
                mode,
                role,
                fill,
                BotCount(),
                _addressField != null ? _addressField.text : null);

            // A joiner loads no map of its own. It waits in the connecting
            // scene, and the server sends its own map as the client
            // authenticates. Loading the map picked here first would be a load
            // of the wrong city.
            SceneManager.LoadScene(
                mode == GameMode.MultiplayerJoin ? _connectingScene : _gameScene,
                LoadSceneMode.Single);
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
