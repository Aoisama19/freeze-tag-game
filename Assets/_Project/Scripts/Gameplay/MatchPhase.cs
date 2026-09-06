namespace BarafPaani.Gameplay
{
    /// <summary>Where a match is up to.</summary>
    public enum MatchPhase
    {
        /// <summary>
        /// Waiting to begin. People are arriving and choosing sides; nobody can
        /// be frozen and no clock is running.
        /// </summary>
        Lobby,

        /// <summary>A round is being played.</summary>
        Playing,

        /// <summary>A round has been won and the result is up.</summary>
        Over,
    }
}
