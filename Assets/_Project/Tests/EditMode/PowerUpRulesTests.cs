using BarafPaani.Gameplay.PowerUps;
using NUnit.Framework;

namespace BarafPaani.Tests
{
    public class PowerUpRulesTests
    {
        [Test]
        public void An_empty_pocket_has_room()
        {
            Assert.IsTrue(PowerUpRules.CanCarry(0, 3));
        }

        [Test]
        public void A_full_pocket_does_not()
        {
            Assert.IsFalse(PowerUpRules.CanCarry(3, 3));
        }

        [Test]
        public void Carrying_more_than_the_limit_is_still_full()
        {
            // Should not be reachable, but returning true here would let a bad
            // count keep growing rather than settle.
            Assert.IsFalse(PowerUpRules.CanCarry(4, 3));
        }

        [Test]
        public void A_frozen_character_cannot_use_anything()
        {
            Assert.IsFalse(PowerUpRules.CanUse(frozen: true, carried: 3));
        }

        [Test]
        public void Nor_can_one_carrying_nothing()
        {
            Assert.IsFalse(PowerUpRules.CanUse(frozen: false, carried: 0));
        }

        [Test]
        public void A_free_character_holding_one_can()
        {
            Assert.IsTrue(PowerUpRules.CanUse(frozen: false, carried: 1));
        }

        [Test]
        public void Cycling_forward_wraps_at_the_end()
        {
            Assert.AreEqual(1, PowerUpRules.NextSlot(0, 3));
            Assert.AreEqual(2, PowerUpRules.NextSlot(1, 3));
            Assert.AreEqual(0, PowerUpRules.NextSlot(2, 3));
        }

        [Test]
        public void Cycling_back_wraps_at_the_start()
        {
            Assert.AreEqual(2, PowerUpRules.PreviousSlot(0, 3));
            Assert.AreEqual(0, PowerUpRules.PreviousSlot(1, 3));
        }

        [Test]
        public void Cycling_an_empty_pocket_stays_put()
        {
            Assert.AreEqual(0, PowerUpRules.NextSlot(0, 0));
            Assert.AreEqual(0, PowerUpRules.PreviousSlot(0, 0));
        }

        [Test]
        public void A_selection_past_the_end_comes_back_to_the_start()
        {
            // What happens after using one: the count shrinks under the pointer.
            Assert.AreEqual(0, PowerUpRules.ClampSlot(5, 2));
        }

        [Test]
        public void Using_the_last_one_leaves_the_selection_at_zero()
        {
            Assert.AreEqual(0, PowerUpRules.SlotAfterUsing(selected: 0, used: 0, countBefore: 1));
        }

        [Test]
        public void Using_one_below_the_selection_shifts_it_down()
        {
            // Holding three, pointing at the last, spending the first: the one
            // that was selected is now at slot 1, not 2.
            Assert.AreEqual(1, PowerUpRules.SlotAfterUsing(selected: 2, used: 0, countBefore: 3));
        }

        [Test]
        public void Using_the_end_of_the_list_pulls_the_selection_back_inside_it()
        {
            // Pointing at the last of three and spending it. Without this the
            // selection would sit one past the end of a two-item list.
            Assert.AreEqual(0, PowerUpRules.SlotAfterUsing(selected: 2, used: 2, countBefore: 3));
        }

        [Test]
        public void Using_one_above_the_selection_leaves_it_alone()
        {
            Assert.AreEqual(0, PowerUpRules.SlotAfterUsing(selected: 0, used: 2, countBefore: 3));
        }
    }
}
