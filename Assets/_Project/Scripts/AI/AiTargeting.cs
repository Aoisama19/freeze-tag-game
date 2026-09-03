using BarafPaani.Gameplay;

namespace BarafPaani.AI
{
    /// <summary>
    /// Who an agent wants to walk towards. Separate from FreezeRules, which
    /// decides what happens once it gets there — chasing someone is not the
    /// same question as being allowed to tag them.
    /// </summary>
    public static class AiTargeting
    {
        public static bool WantsToApproach(
            Role actorRole,
            bool actorIsFrozen,
            Role targetRole,
            bool targetIsFrozen)
        {
            // Frozen agents go nowhere.
            if (actorIsFrozen)
            {
                return false;
            }

            // Nobody chases the catcher.
            if (targetRole == Role.Catcher)
            {
                return false;
            }

            // A catcher goes after runners still in the game; chasing one who is
            // already frozen would just be standing on them.
            if (actorRole == Role.Catcher)
            {
                return !targetIsFrozen;
            }

            // A runner goes to frozen team-mates, to free them.
            return targetIsFrozen;
        }
    }
}
