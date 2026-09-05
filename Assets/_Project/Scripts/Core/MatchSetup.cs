using BarafPaani.Gameplay;
using UnityEngine;

namespace BarafPaani.Core
{
    /// <summary>
    /// What the player chose in the menu, carried into the game scene.
    ///
    /// An asset rather than a static field. docs/architecture.md rules out
    /// static mutable game state — the old build's GameManager singleton and
    /// Actions class both assumed one game in one process — and this way the
    /// choice is inspectable, and the game scene still runs on its own defaults
    /// when opened directly, which is what the tests rely on.
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
        [Tooltip("Set by the menu, cleared by the game scene once acted on. Without it, " +
                 "opening the game scene directly would start a match nobody asked for.")]
        private bool _launchRequested;

        public GameMode Mode => _mode;

        public Role HumanRole => _humanRole;

        public string JoinAddress => _joinAddress;

        public bool LaunchRequested => _launchRequested;

        /// <summary>Records a choice and asks the game scene to act on it.</summary>
        public void Request(GameMode mode, Role humanRole, string joinAddress = null)
        {
            _mode = mode;
            _humanRole = humanRole;

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
