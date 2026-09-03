namespace BarafPaani.AI
{
    /// <summary>
    /// What an agent should be doing, given what it knows. Pure, like
    /// FreezeRules and AiTargeting, so the priorities can be tested without
    /// standing up a scene full of agents.
    ///
    /// The two sides know different things on purpose. A runner always knows
    /// where its frozen team-mates are, because in the real game they are
    /// shouting and because isFrozen is replicated to everyone anyway — the HUD
    /// will show the same fact to human players. A runner only knows where the
    /// catcher is when it can actually see one. Smart about teamwork, uncertain
    /// about danger.
    /// </summary>
    public static class AiTactics
    {
        /// <param name="guardAvailable">
        /// False while the catcher is taking a break from guarding. Without the
        /// break it would camp one frozen runner forever, which stalls the
        /// endgame and is horrible to play against.
        /// </param>
        public static CatcherIntent ChooseCatcherIntent(
            bool hasVisibleRunner,
            bool anyRunnerFrozen,
            bool guardAvailable)
        {
            // Someone in sight always wins. Guarding is what you do when there
            // is nobody to chase.
            if (hasVisibleRunner)
            {
                return CatcherIntent.Chase;
            }

            if (anyRunnerFrozen && guardAvailable)
            {
                return CatcherIntent.Guard;
            }

            return CatcherIntent.Wander;
        }

        /// <param name="catcherDistance">
        /// Only meaningful when <paramref name="catcherVisible"/> is true.
        /// </param>
        public static RunnerIntent ChooseRunnerIntent(
            bool catcherVisible,
            float catcherDistance,
            float dangerDistance,
            bool hasReachableFrozenAlly)
        {
            // Saving yourself comes before saving anyone else.
            if (catcherVisible && catcherDistance <= dangerDistance)
            {
                return RunnerIntent.Flee;
            }

            if (hasReachableFrozenAlly)
            {
                return RunnerIntent.Rescue;
            }

            return RunnerIntent.Wander;
        }
    }
}
