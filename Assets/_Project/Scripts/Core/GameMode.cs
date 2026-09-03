namespace BarafPaani.Core
{
    /// <summary>
    /// Which way the current session was started. Kept deliberately thin: this
    /// decides who fills the empty roles and whether anyone can connect, and
    /// nothing else. No gameplay code should branch on it.
    /// </summary>
    public enum GameMode
    {
        SinglePlayer,
        Multiplayer
    }
}
