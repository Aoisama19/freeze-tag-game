namespace BarafPaani.Gameplay
{
    /// <summary>
    /// Who knows where anyone is. One rule, consulted by both the AI and the
    /// minimap, so what a bot acts on and what a player is shown can never drift
    /// apart.
    ///
    /// That drift is not hypothetical. Issues 4 and 6 in the old build were both
    /// the same logic living in two places and disagreeing, and a minimap that
    /// worked out visibility for itself would be the same mistake in a new coat.
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
            return !NeedsLineOfSight(viewerRole, targetRole) || targetSeen;
        }
    }
}
