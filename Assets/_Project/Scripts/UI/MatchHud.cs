using BarafPaani.Gameplay;
using Mirror;
using UnityEngine;
using UnityEngine.UI;

namespace BarafPaani.UI
{
    /// <summary>
    /// Shows how the round is going: who is still free, how long is left, and
    /// the result when it lands.
    ///
    /// Reads MatchState, which the server owns. Nothing here decides anything —
    /// if this disagreed with the server it would be this that is wrong.
    /// </summary>
    public class MatchHud : MonoBehaviour
    {
        [SerializeField]
        private Text _runnersLabel;

        [SerializeField]
        private Text _clockLabel;

        [SerializeField]
        private Text _resultLabel;

        [SerializeField]
        [Tooltip("Says how long you are still safe for after a round begins.")]
        private Text _immunityLabel;

        [SerializeField]
        [Tooltip("Rounds each side has taken so far.")]
        private Text _scoreLabel;

        [SerializeField]
        [Tooltip("Clock turns this colour once the round is nearly over.")]
        private Color _urgent = new Color(1f, 0.45f, 0.35f);

        [SerializeField]
        private float _urgentBelowSeconds = 30f;

        private Color _clockNormal = Color.white;
        private MatchState _match;

        private void Awake()
        {
            if (_clockLabel != null)
            {
                _clockNormal = _clockLabel.color;
            }
        }

        private void Update()
        {
            MatchState match = FindMatch();

            if (match == null)
            {
                SetVisible(false);
                return;
            }

            SetVisible(true);

            if (_runnersLabel != null)
            {
                _runnersLabel.text = $"Runners free  {match.RunnersFree} / {match.RunnersTotal}";
            }

            UpdateClock(match);
            UpdateResult(match);
            UpdateImmunity();
            UpdateScore(match);
        }

        /// <summary>
        /// The running score. Without it a round ends, restarts, and nothing
        /// carries over — there is no reason to play the next one.
        /// </summary>
        private void UpdateScore(MatchState match)
        {
            if (_scoreLabel == null)
            {
                return;
            }

            _scoreLabel.text =
                $"Round {match.RoundNumber}      Catcher {match.CatcherWins}  -  {match.RunnerWins} Runners";
        }

        /// <summary>
        /// How long you are still safe for. Worth saying out loud: a player who
        /// does not know they are briefly untouchable will run away from the
        /// catcher rather than past them.
        /// </summary>
        private void UpdateImmunity()
        {
            if (_immunityLabel == null)
            {
                return;
            }

            Mirror.NetworkIdentity me = Mirror.NetworkClient.localPlayer;

            float left = me != null && me.TryGetComponent(out Freezable freezable)
                ? freezable.ImmunityRemaining
                : 0f;

            _immunityLabel.enabled = left > 0f;

            if (left > 0f)
            {
                _immunityLabel.text = $"Safe for {left:0.0}s";
            }
        }

        /// <summary>
        /// MatchState lives on the network manager, which is spawned rather than
        /// placed, so it cannot be wired up in the scene ahead of time.
        /// </summary>
        private MatchState FindMatch()
        {
            if (_match != null)
            {
                return _match;
            }

            if (!NetworkClient.active && !NetworkServer.active)
            {
                return null;
            }

            _match = FindFirstObjectByType<MatchState>();
            return _match;
        }

        private void UpdateClock(MatchState match)
        {
            if (_clockLabel == null)
            {
                return;
            }

            float remaining = match.SecondsRemaining;
            int minutes = Mathf.FloorToInt(remaining / 60f);
            int seconds = Mathf.FloorToInt(remaining % 60f);

            _clockLabel.text = $"{minutes}:{seconds:00}";
            _clockLabel.color = remaining <= _urgentBelowSeconds ? _urgent : _clockNormal;
        }

        private void UpdateResult(MatchState match)
        {
            if (_resultLabel == null)
            {
                return;
            }

            switch (match.Outcome)
            {
                case MatchOutcome.CatcherWins:
                    _resultLabel.enabled = true;
                    _resultLabel.text = "Everyone frozen — catcher wins";
                    return;

                case MatchOutcome.RunnersWin:
                    _resultLabel.enabled = true;
                    _resultLabel.text = "Time up — runners win";
                    return;

                default:
                    _resultLabel.enabled = false;
                    return;
            }
        }

        private void SetVisible(bool visible)
        {
            if (_runnersLabel != null)
            {
                _runnersLabel.enabled = visible;
            }

            if (_clockLabel != null)
            {
                _clockLabel.enabled = visible;
            }

            if (!visible && _resultLabel != null)
            {
                _resultLabel.enabled = false;
            }
        }
    }
}
