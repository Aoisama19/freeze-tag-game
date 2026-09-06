using UnityEngine;

namespace BarafPaani.Audio
{
    /// <summary>
    /// Every sound the game makes, in one asset.
    ///
    /// An asset rather than a singleton. The old build's AudioManager was a
    /// static instance with DontDestroyOnLoad that looked clips up by string at
    /// the call site, so a typo was silence and nothing could be checked until
    /// it was played. docs/architecture.md rules out static mutable game state
    /// for the same reasons.
    /// </summary>
    [CreateAssetMenu(menuName = "Baraf-Paani/Sound Bank", fileName = "SoundBank")]
    public class SoundBank : ScriptableObject
    {
        [Header("Freeze")]
        [SerializeField]
        private AudioClip _freeze;

        [SerializeField]
        private AudioClip _thaw;

        [Header("Power-ups")]
        [SerializeField]
        private AudioClip _pickup;

        [SerializeField]
        private AudioClip _powerUp;

        [Header("Round")]
        [SerializeField]
        private AudioClip _roundStart;

        [SerializeField]
        private AudioClip _roundWon;

        [SerializeField]
        private AudioClip _roundLost;

        [Header("Menu")]
        [SerializeField]
        private AudioClip _click;

        [Header("Footsteps")]
        [SerializeField]
        [Tooltip("Picked from at random, so a run does not sound like a metronome.")]
        private AudioClip[] _footsteps;

        [SerializeField]
        private AudioClip _land;

        public AudioClip Freeze => _freeze;

        public AudioClip Thaw => _thaw;

        public AudioClip Pickup => _pickup;

        public AudioClip PowerUp => _powerUp;

        public AudioClip RoundStart => _roundStart;

        public AudioClip RoundWon => _roundWon;

        public AudioClip RoundLost => _roundLost;

        public AudioClip Click => _click;

        public AudioClip Land => _land;

        /// <summary>A footstep, or null if none are set.</summary>
        public AudioClip Footstep()
        {
            if (_footsteps == null || _footsteps.Length == 0)
            {
                return null;
            }

            return _footsteps[Random.Range(0, _footsteps.Length)];
        }
    }
}
