namespace BarafPaani.Gameplay
{
    /// <summary>
    /// Every rule about who may freeze or free whom, as one pure function.
    ///
    /// Deliberately has no Unity or Mirror types in it, so the rules can be
    /// tested directly instead of by standing up a networked scene. The old
    /// build spread this logic across four call sites that disagreed with each
    /// other; there is exactly one copy now, and it is the one under test.
    /// </summary>
    public static class FreezeRules
    {
        /// <summary>
        /// Distances are squared on both sides so callers can skip the square
        /// root in per-frame proximity checks.
        /// </summary>
        public static TagOutcome Resolve(
            Role actorRole,
            bool actorIsFrozen,
            Role targetRole,
            bool targetIsFrozen,
            float squaredDistance,
            float squaredRange)
        {
            return Resolve(
                actorRole,
                actorIsFrozen,
                targetRole,
                targetIsFrozen,
                targetIsImmune: false,
                squaredDistance,
                squaredRange);
        }

        /// <summary>
        /// As above, for a target that may still be under spawn immunity.
        ///
        /// Immunity stops a catcher freezing someone, and nothing else. It does
        /// not stop a team-mate freeing them: being rescued is not something
        /// anyone needs protecting from, and a runner frozen with time left on
        /// the clock would otherwise be stuck until it ran out.
        /// </summary>
        public static TagOutcome Resolve(
            Role actorRole,
            bool actorIsFrozen,
            Role targetRole,
            bool targetIsFrozen,
            bool targetIsImmune,
            float squaredDistance,
            float squaredRange)
        {
            // A frozen character is out of the game until someone frees them.
            if (actorIsFrozen)
            {
                return TagOutcome.None;
            }

            if (squaredDistance > squaredRange)
            {
                return TagOutcome.None;
            }

            // Catchers are never a target.
            if (targetRole == Role.Catcher)
            {
                return TagOutcome.None;
            }

            if (actorRole == Role.Catcher)
            {
                // The guard the multiplayer build was missing. Without it a
                // second touch re-froze an already frozen runner, the freeze
                // event fired twice, and the runners-left counter — the one
                // deciding win and loss — was decremented past where it should
                // have stopped.
                if (targetIsFrozen)
                {
                    return TagOutcome.None;
                }

                // Just arrived, or just been put back at the start of a round.
                // Without this a catcher standing on a spawn point takes people
                // the instant they appear, which is not a game.
                return targetIsImmune ? TagOutcome.None : TagOutcome.Freeze;
            }

            // A free runner reaching a frozen team-mate frees them.
            return targetIsFrozen ? TagOutcome.Unfreeze : TagOutcome.None;
        }
    }
}
