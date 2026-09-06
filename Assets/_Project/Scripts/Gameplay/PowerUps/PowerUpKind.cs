namespace BarafPaani.Gameplay.PowerUps
{
    /// <summary>
    /// The power-ups a character can carry.
    ///
    /// Named rather than numbered. The old build passed integer ids around and
    /// translated them twice — once from an enum to an id at the pickup, once
    /// from an id back to a component at the holder — with a silent default in
    /// the middle that dropped anything unrecognised. See
    /// docs/old-build-issues.md, issue 3.
    /// </summary>
    public enum PowerUpKind
    {
        None = 0,
        SpeedBoost = 1,
        Invisibility = 2,
        Clone = 3,
    }
}
