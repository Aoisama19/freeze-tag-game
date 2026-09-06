namespace BarafPaani.Gameplay
{
    /// <summary>
    /// Who may be what, and when a match is fit to start. Pure, so the awkward
    /// combinations are settled here rather than found by four people in a
    /// match discovering nobody is catching.
    /// </summary>
    public static class LobbyRules
    {
        /// <summary>
        /// Whether sides can still be changed.
        ///
        /// Only in the lobby. Allowed mid-round, a catcher about to lose could
        /// simply stop being the catcher, and a runner about to be tagged could
        /// stop being catchable.
        /// </summary>
        public static bool CanChangeRole(MatchPhase phase)
        {
            return phase == MatchPhase.Lobby;
        }

        /// <summary>
        /// Whether a request to take a side is granted.
        ///
        /// Asking to be a runner always is. Asking to be the catcher is granted
        /// only when nobody else already is — first come, rather than the last
        /// person to click taking it off whoever had it, which in a four-player
        /// lobby is an argument rather than a game.
        /// </summary>
        public static bool CanTakeRole(
            MatchPhase phase, Role wanted, bool alreadyThisRole, bool someoneElseIsCatcher)
        {
            if (!CanChangeRole(phase) || alreadyThisRole)
            {
                return false;
            }

            return wanted != Role.Catcher || !someoneElseIsCatcher;
        }

        /// <summary>
        /// Whether a match can begin.
        ///
        /// With bots filling in, always: the server puts an AI in whatever seat
        /// nobody took. With bots switched off there is nobody to fill in, so
        /// the humans present have to cover both sides themselves — which is
        /// the case that used to produce a round nobody could win.
        /// </summary>
        public static bool CanStart(bool fillWithBots, int humanCatchers, int humanRunners)
        {
            if (fillWithBots)
            {
                return humanCatchers + humanRunners > 0;
            }

            return humanCatchers == 1 && humanRunners > 0;
        }

        /// <summary>
        /// Why a match cannot begin, for telling the people waiting. Empty when
        /// it can.
        /// </summary>
        public static string WhyNotStarting(
            bool fillWithBots, int humanCatchers, int humanRunners)
        {
            if (CanStart(fillWithBots, humanCatchers, humanRunners))
            {
                return string.Empty;
            }

            if (fillWithBots)
            {
                return "Waiting for someone to join";
            }

            if (humanCatchers == 0)
            {
                return "Bots are off, so somebody has to be the catcher";
            }

            if (humanCatchers > 1)
            {
                return "Only one catcher";
            }

            return "Bots are off, so somebody has to run";
        }
    }
}
