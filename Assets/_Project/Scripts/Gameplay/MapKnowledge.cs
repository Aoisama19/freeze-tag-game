namespace BarafPaani.Gameplay
{
    /// <summary>
    /// Who knows where anyone is. One rule, consulted by both the AI and the
    /// minimap, so what a bot acts on and what a player is shown can never drift
    /// apart.
    ///
    /// A minimap that worked visibility out for itself would be the same logic
    /// living in two places, free to disagree with what the bots act on.
    ///
    /// The model:
    ///
    /// - A catcher knows where every runner is, always. On a 120 metre map a
    ///   catcher limited to a view cone walks past people and plays as broken
    ///   rather than fair, and in the playground game the catcher really can see
    ///   across the yard.
    ///
    /// - A runner always knows where its team-mates are. They are shouting, and
    ///   isFrozen is replicated to everyone regardless, so hiding it would be a
    ///   fiction the network does not support.
    ///
    /// - A runner knows where the catcher is only while it can see one. This is
    ///   the asymmetry that makes breaking line of sight worth doing.
    ///
    /// - Nobody knows where a hidden character is, whichever side they are on.
    ///   This is the one thing that overrides the catcher's global view, and it
    ///   is what the invisibility power-up buys.
    /// </summary>
    public static class MapKnowledge
    {
        /// <summary>
        /// Whether a viewer has to actually see this kind of character to know
        /// where it is. The only thing anyone has to look for is the catcher,
        /// and only runners have to look.
        /// </summary>
        public static bool NeedsLineOfSight(Role viewerRole, Role targetRole)
        {
            return viewerRole == Role.Runner && targetRole == Role.Catcher;
        }

        /// <summary>
        /// Whether the viewer knows where this character is, and so whether it
        /// belongs on their minimap.
        /// </summary>
        /// <param name="targetSeen">
        /// Whether the target is currently visible to the viewer — or was seen
        /// recently enough to still be believed. Ignored for anything the viewer
        /// knows about regardless.
        /// </param>
        public static bool KnowsPosition(Role viewerRole, Role targetRole, bool targetSeen)
        {
            return KnowsPosition(viewerRole, targetRole, targetSeen, targetHidden: false);
        }

        /// <summary>
        /// As above, but for a target that may be hidden.
        ///
        /// Hidden beats everything, including the catcher's otherwise unlimited
        /// view. A power-up that only removed a blip while leaving the AI
        /// walking straight at you would be worse than useless — it would look
        /// like the game cheating.
        /// </summary>
        public static bool KnowsPosition(
            Role viewerRole, Role targetRole, bool targetSeen, bool targetHidden)
        {
            if (targetHidden)
            {
                return false;
            }

            return !NeedsLineOfSight(viewerRole, targetRole) || targetSeen;
        }
    }
}
