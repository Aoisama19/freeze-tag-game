using BarafPaani.Gameplay;
using Mirror;
using UnityEngine;

namespace BarafPaani.Audio
{
    /// <summary>
    /// The sounds the round itself makes: starting, and the result.
    ///
    /// Watches MatchState rather than being told. The server already replicates
    /// the phase and the outcome, so every client can work out when they
    /// changed; a message saying "play this now" would be a second channel
    /// carrying the same information, and one that arrives at a different time.
    ///
    /// Which side you are on decides which of won and lost you hear, so the
    /// catcher and the runners get opposite sounds from the same result.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class MatchAudio : MonoBehaviour
    {
        [SerializeField]
        private SoundBank _sounds;

        [SerializeField]
        private AudioSource _source;

        private MatchState _match;
        private MatchPhase _lastPhase = MatchPhase.Lobby;
        private MatchOutcome _lastOutcome = MatchOutcome.InProgress;
        private bool _started;

        private void Awake()
        {
            if (_source == null)
            {
                _source = GetComponent<AudioSource>();
            }
        }

        private void Update()
        {
            MatchState match = FindMatch();

            if (match == null)
            {
                _started = false;
                return;
            }

            // The first frame a match is found is not a change to react to;
            // without this, joining a match already in progress announces a
            // round start that happened minutes ago.
            if (!_started)
            {
                _started = true;
                _lastPhase = match.Phase;
                _lastOutcome = match.Outcome;
                return;
            }

            if (match.Phase != _lastPhase)
            {
                if (match.Phase == MatchPhase.Playing)
                {
                    Play(_sounds != null ? _sounds.RoundStart : null);
                }

                _lastPhase = match.Phase;
            }

            if (match.Outcome != _lastOutcome)
            {
                if (match.Outcome != MatchOutcome.InProgress)
                {
                    PlayResult(match.Outcome);
                }

                _lastOutcome = match.Outcome;
            }
        }

        private void PlayResult(MatchOutcome outcome)
        {
            if (_sounds == null)
            {
                return;
            }

            bool catcher = MyRole() == Role.Catcher;
            bool catcherWon = outcome == MatchOutcome.CatcherWins;

            Play(catcher == catcherWon ? _sounds.RoundWon : _sounds.RoundLost);
        }

        private static Role MyRole()
        {
            NetworkIdentity player = NetworkClient.localPlayer;

            return player != null && player.TryGetComponent(out PlayerRole role)
                ? role.Role
                : Role.Runner;
        }

        private void Play(AudioClip clip)
        {
            if (clip != null && _source != null)
            {
                _source.PlayOneShot(clip);
            }
        }

        private MatchState FindMatch()
        {
            if (_match == null)
            {
                _match = FindFirstObjectByType<MatchState>();
            }

            return _match;
        }
    }
}
