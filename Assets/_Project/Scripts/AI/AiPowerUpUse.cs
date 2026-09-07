using BarafPaani.Gameplay;
using BarafPaani.Gameplay.PowerUps;
using Mirror;
using UnityEngine;

namespace BarafPaani.AI
{
    /// <summary>
    /// Decides when a bot spends what it is carrying.
    ///
    /// Kept apart from AiBrain, which already decides where to go. This only
    /// reads the intent the brain has settled on and answers a narrower
    /// question, so neither has to know how the other works.
    ///
    /// Bots reach power-ups through the same holder a person does, so there is
    /// no second path to keep working.
    /// </summary>
    [RequireComponent(typeof(AiBrain))]
    [RequireComponent(typeof(PowerUpHolder))]
    public class AiPowerUpUse : NetworkBehaviour
    {
        [SerializeField]
        [Tooltip("Seconds between considering it, so a bot does not empty its pockets in one frame.")]
        private float _thinkInterval = 1f;

        private AiBrain _brain;
        private PowerUpHolder _holder;
        private PlayerRole _role;
        private Freezable _freezable;
        private float _nextThink;

        private void Awake()
        {
            _brain = GetComponent<AiBrain>();
            _holder = GetComponent<PowerUpHolder>();
            _role = GetComponent<PlayerRole>();
            _freezable = GetComponent<Freezable>();
        }

        [ServerCallback]
        private void Update()
        {
            if (Time.time < _nextThink)
            {
                return;
            }

            _nextThink = Time.time + _thinkInterval;

            if (_holder.Count == 0 || (_freezable != null && _freezable.IsFrozen))
            {
                return;
            }

            if (!WantsToUseSomething())
            {
                return;
            }

            // Slot zero: a bot has no reason to prefer one over another yet, and
            // pretending otherwise would be a rule nobody could see the effect of.
            _holder.Use(0);
        }

        /// <summary>
        /// Only in the moment it counts. A speed boost spent while wandering an
        /// empty street is a boost wasted, and a bot doing that reads as a bot
        /// that does not understand the game.
        /// </summary>
        private bool WantsToUseSomething()
        {
            if (_role.Role == Role.Catcher)
            {
                return _brain.CatcherIntent == CatcherIntent.Chase;
            }

            return _brain.RunnerIntent == RunnerIntent.Flee;
        }
    }
}
