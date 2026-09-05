namespace BarafPaani.Gameplay
{
    /// <summary>
    /// When a round is over and who won, as one pure function.
    ///
    /// The count this reads — runners still free — is the number the old build
    /// corrupted. Freezing an already frozen runner fired the event twice and
    /// decremented it past where it should have stopped, so the match could be
    /// decided by a bug rather than by play. FreezeRules already refuses the
    /// double freeze and is tested for it; this is the other half, kept
    /// separately testable so the win condition itself cannot drift.
    /// </summary>
    public static class MatchRules
    {
        public static MatchOutcome Resolve(int runnersTotal, int runnersFree, float secondsRemaining)
        {
            // Nobody to catch: there is nothing to decide yet. Guards the moment
            // between a round starting and its characters existing.
            if (runnersTotal <= 0)
            {
                return MatchOutcome.InProgress;
            }

            // Checked before the clock, so a freeze landing on the final second
            // wins it for the catcher rather than being beaten to it by time.
            if (runnersFree <= 0)
            {
                return MatchOutcome.CatcherWins;
            }

            // Surviving the round is how runners win. There is no other way.
            if (secondsRemaining <= 0f)
            {
                return MatchOutcome.RunnersWin;
            }

            return MatchOutcome.InProgress;
        }
    }
}
