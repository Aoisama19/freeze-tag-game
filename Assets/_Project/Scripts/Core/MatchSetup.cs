using BarafPaani.Gameplay;
using UnityEngine;

namespace BarafPaani.Core
{
    /// <summary>
    /// What the player chose in the menu, carried into the game scene.
    ///
    /// An asset rather than a static field. docs/architecture.md rules out
    /// static mutable game state, which assumes one game in one process. This
    /// way the choice is inspectable, and the game scene still runs on its own
    /// defaults when opened directly, which is what the tests rely on.
    /// </summary>
    [CreateAssetMenu(menuName = "Baraf-Paani/Match Setup", fileName = "MatchSetup")]
    public class MatchSetup : ScriptableObject
    {
        [SerializeField]
        private GameMode _mode = GameMode.SinglePlayer;

        [SerializeField]
        private Role _humanRole = Role.Catcher;

        [SerializeField]
        private string _joinAddress = "localhost";

        [SerializeField]
        [Tooltip("Scene of the map to play. One scene per map, named Game_<Map>.")]
        private string _mapScene = "Game_3Talwaar";

        [SerializeField]
        [Tooltip("Length of a round, in seconds.")]
        private float _roundSeconds = 180f;

        [SerializeField]
        [Tooltip("Off means no AI at all — the match is whoever turns up.")]
        private bool _fillWithBots = true;

        [SerializeField]
        [Tooltip("Runners a match aims for, humans and bots together.")]
        private int _botRunners = 3;

        [SerializeField]
        [Tooltip("Set by the menu, cleared by the game scene once acted on. Without it, " +
                 "opening the game scene directly would start a match nobody asked for.")]
        private bool _launchRequested;

        /// <summary>Most bots a match will take. Keeps a typed-in number sane.</summary>
        public const int MaxBotRunners = 8;

        /// <summary>Longest round the menu will offer, in seconds.</summary>
        public const float MaxRoundSeconds = 600f;

        public GameMode Mode => _mode;

        public Role HumanRole => _humanRole;

        public string JoinAddress => _joinAddress;

        public string MapScene => _mapScene;

        public float RoundSeconds => _roundSeconds;

        /// <summary>Records the round length. Kept in range whatever the menu offers.</summary>
        public void ChooseRoundLength(float seconds)
        {
            _roundSeconds = Mathf.Clamp(seconds, 30f, MaxRoundSeconds);
        }

        /// <summary>Records the map. Kept apart from Request so the menu can
        /// remember a choice without asking for a match yet.</summary>
        public void ChooseMap(string scene)
        {
            if (!string.IsNullOrWhiteSpace(scene))
            {
                _mapScene = scene;
            }
        }

        public bool FillWithBots => _fillWithBots;

        public int BotRunners => _botRunners;

        public bool LaunchRequested => _launchRequested;

        /// <summary>Records a choice and asks the game scene to act on it.</summary>
        public void Request(
            GameMode mode,
            Role humanRole,
            bool fillWithBots,
            int botRunners,
            string joinAddress = null)
        {
            _mode = mode;
            _humanRole = humanRole;
            _fillWithBots = fillWithBots;
            _botRunners = Mathf.Clamp(botRunners, 0, MaxBotRunners);

            if (!string.IsNullOrWhiteSpace(joinAddress))
            {
                _joinAddress = joinAddress.Trim();
            }

            _launchRequested = true;
        }

        /// <summary>
        /// Consumes the request, so a later return to the game scene does not
        /// silently start another match on a stale choice.
        /// </summary>
        public void ClearRequest()
        {
            _launchRequested = false;
        }
    }
}
