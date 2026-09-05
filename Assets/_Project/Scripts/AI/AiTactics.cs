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
    /// will show the same fact to human players. A runner knows where the
    /// catcher is only while it can see one, plus a few seconds of memory after.
    /// Smart about teamwork, uncertain about danger.
    ///
    /// The catcher, by contrast, knows where every live runner is. On a 120
    /// metre map a catcher limited to a 14 metre view cone wanders uselessly and
    /// only notices anyone who walks into it, which reads as a broken opponent
    /// rather than a fair one. It is also true to the playground game: the
    /// catcher can see across the yard.
    /// </summary>
    public static class AiTactics
    {
        /// <param name="hasKnownRunner">
        /// Whether there is a live runner to go after. The catcher knows this
        /// globally rather than by sight.
        /// </param>
        /// <param name="guardAvailable">
        /// False while the catcher is taking a break from guarding. Without the
        /// break it would camp one frozen runner forever, which stalls the
        /// endgame and is horrible to play against.
        /// </param>
        public static CatcherIntent ChooseCatcherIntent(
            bool hasKnownRunner,
            bool anyRunnerFrozen,
            bool guardAvailable)
        {
            // A runner to chase always wins. Guarding is what you do when there
            // is nobody left to chase.
            if (hasKnownRunner)
            {
                return CatcherIntent.Chase;
            }

            if (anyRunnerFrozen && guardAvailable)
            {
                return CatcherIntent.Guard;
            }

            return CatcherIntent.Wander;
        }

        /// <param name="current">
        /// What this runner decided last tick. Used to hold a decision rather
        /// than retaking it from scratch every quarter second.
        /// </param>
        /// <param name="catcherKnown">
        /// Whether the catcher's position is known at all — seen now, or seen
        /// recently enough to still be worth believing.
        /// </param>
        /// <param name="catcherDistance">
        /// Only meaningful when <paramref name="catcherKnown"/> is true.
        /// </param>
        /// <param name="safeDistance">
        /// How far the catcher has to be before a fleeing runner will consider
        /// doing anything else. Must be larger than
        /// <paramref name="dangerDistance"/>: the gap between the two is what
        /// stops the runner dithering.
        /// </param>
        public static RunnerIntent ChooseRunnerIntent(
            RunnerIntent current,
            bool catcherKnown,
            float catcherDistance,
            float dangerDistance,
            float safeDistance,
            bool hasReachableFrozenAlly)
        {
            if (catcherKnown)
            {
                // Saving yourself comes before saving anyone else. This is the
                // first thing checked, so a rescue in progress is abandoned the
                // moment the catcher closes in.
                if (catcherDistance <= dangerDistance)
                {
                    return RunnerIntent.Flee;
                }

                // Already running: keep running until genuinely clear, rather
                // than turning back the instant the catcher is a hair outside
                // danger range. Without this the runner flips between fleeing
                // and rescuing on the boundary and effectively stands still.
                if (current == RunnerIntent.Flee && catcherDistance < safeDistance)
                {
                    return RunnerIntent.Flee;
                }
            }

            if (hasReachableFrozenAlly)
            {
                return RunnerIntent.Rescue;
            }

            return RunnerIntent.Wander;
        }
    }
}
