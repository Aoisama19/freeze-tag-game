namespace BarafPaani.Gameplay.PowerUps
{
    /// <summary>
    /// The power-ups a character can carry.
    ///
    /// Named rather than numbered. Passing integer ids around means translating
    /// them at each end, once from an enum to an id at the pickup and once back
    /// again at the holder, with a silent default in the middle that drops
    /// anything unrecognised.
    /// </summary>
    public enum PowerUpKind
    {
        None = 0,
        SpeedBoost = 1,
        Invisibility = 2,
        Clone = 3,
    }
}
