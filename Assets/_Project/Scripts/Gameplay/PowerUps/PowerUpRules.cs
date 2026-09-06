namespace BarafPaani.Gameplay.PowerUps
{
    /// <summary>
    /// When a power-up can be picked up, selected and used. Pure, so the awkward
    /// cases are settled here and tested rather than being rediscovered in a
    /// live match.
    /// </summary>
    public static class PowerUpRules
    {
        /// <summary>Most a character can hold at once, as in the original game.</summary>
        public const int MaxCarried = 3;

        /// <summary>Whether there is room for another one.</summary>
        public static bool CanCarry(int carried, int max)
        {
            return carried >= 0 && carried < max;
        }

        /// <summary>
        /// Whether a character is in a position to use anything at all.
        ///
        /// A frozen character is not: the old build left this to whichever
        /// script happened to be disabled at the time, which meant a runner
        /// frozen mid-reach could still fire off a power-up.
        /// </summary>
        public static bool CanUse(bool frozen, int carried)
        {
            return !frozen && carried > 0;
        }

        /// <summary>
        /// Keeps a selection pointing at something real. Picking one up, using
        /// one, or being emptied out all change the count under the selection.
        /// </summary>
        public static int ClampSlot(int slot, int count)
        {
            if (count <= 0)
            {
                return 0;
            }

            if (slot < 0)
            {
                return count - 1;
            }

            return slot >= count ? 0 : slot;
        }

        /// <summary>The next slot along, wrapping at the end.</summary>
        public static int NextSlot(int slot, int count)
        {
            return count <= 0 ? 0 : ClampSlot(slot + 1, count);
        }

        /// <summary>The previous slot, wrapping at the start.</summary>
        public static int PreviousSlot(int slot, int count)
        {
            return count <= 0 ? 0 : ClampSlot(slot - 1, count);
        }

        /// <summary>
        /// Where the selection ends up after the slot at <paramref name="used"/>
        /// is spent. Removing the last one in the list would otherwise leave the
        /// selection pointing one past the end.
        /// </summary>
        public static int SlotAfterUsing(int selected, int used, int countBefore)
        {
            int remaining = countBefore - 1;

            if (remaining <= 0)
            {
                return 0;
            }

            return ClampSlot(selected > used ? selected - 1 : selected, remaining);
        }
    }
}
