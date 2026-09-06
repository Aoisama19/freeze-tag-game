using BarafPaani.Gameplay;
using NUnit.Framework;

namespace BarafPaani.Tests
{
    /// <summary>
    /// These are the freeze half of the acceptance checklist in
    /// docs/old-build-issues.md. Issues 4 and 6 were both cases of the rules
    /// existing in more than one place and disagreeing; these lock the one
    /// remaining copy down.
    /// </summary>
    public class FreezeRulesTests
    {
        private const float InRange = 1f;
        private const float OutOfRange = 9f;
        private const float SquaredRange = 4f;

        [Test]
        public void Catcher_freezes_a_runner_within_reach()
        {
            TagOutcome outcome = FreezeRules.Resolve(
                Role.Catcher, false, Role.Runner, false, InRange, SquaredRange);

            Assert.AreEqual(TagOutcome.Freeze, outcome);
        }

        [Test]
        public void Catcher_does_not_refreeze_an_already_frozen_runner()
        {
            // Issue 6. The multiplayer build had no guard here, so a second
            // touch fired the freeze event again and pushed the runners-left
            // counter past where it should have stopped.
            TagOutcome outcome = FreezeRules.Resolve(
                Role.Catcher, false, Role.Runner, true, InRange, SquaredRange);

            Assert.AreEqual(TagOutcome.None, outcome);
        }

        [Test]
        public void Runner_frees_a_frozen_teammate_within_reach()
        {
            TagOutcome outcome = FreezeRules.Resolve(
                Role.Runner, false, Role.Runner, true, InRange, SquaredRange);

            Assert.AreEqual(TagOutcome.Unfreeze, outcome);
        }

        [Test]
        public void Runner_does_nothing_to_a_runner_who_is_not_frozen()
        {
            TagOutcome outcome = FreezeRules.Resolve(
                Role.Runner, false, Role.Runner, false, InRange, SquaredRange);

            Assert.AreEqual(TagOutcome.None, outcome);
        }

        [Test]
        public void A_frozen_runner_cannot_free_anyone()
        {
            TagOutcome outcome = FreezeRules.Resolve(
                Role.Runner, true, Role.Runner, true, InRange, SquaredRange);

            Assert.AreEqual(TagOutcome.None, outcome);
        }

        [Test]
        public void A_frozen_catcher_cannot_freeze_anyone()
        {
            TagOutcome outcome = FreezeRules.Resolve(
                Role.Catcher, true, Role.Runner, false, InRange, SquaredRange);

            Assert.AreEqual(TagOutcome.None, outcome);
        }

        [Test]
        public void The_catcher_is_never_a_target()
        {
            Assert.AreEqual(
                TagOutcome.None,
                FreezeRules.Resolve(Role.Runner, false, Role.Catcher, false, InRange, SquaredRange));

            Assert.AreEqual(
                TagOutcome.None,
                FreezeRules.Resolve(Role.Catcher, false, Role.Catcher, false, InRange, SquaredRange));
        }

        [Test]
        public void Nothing_happens_out_of_reach()
        {
            Assert.AreEqual(
                TagOutcome.None,
                FreezeRules.Resolve(Role.Catcher, false, Role.Runner, false, OutOfRange, SquaredRange));

            Assert.AreEqual(
                TagOutcome.None,
                FreezeRules.Resolve(Role.Runner, false, Role.Runner, true, OutOfRange, SquaredRange));
        }

        [Test]
        public void Reach_is_inclusive_at_exactly_the_range()
        {
            TagOutcome outcome = FreezeRules.Resolve(
                Role.Catcher, false, Role.Runner, false, SquaredRange, SquaredRange);

            Assert.AreEqual(TagOutcome.Freeze, outcome);
        }

        [Test]
        public void A_runner_who_just_spawned_cannot_be_frozen()
        {
            // A catcher standing on a spawn point would otherwise take whoever
            // lands there before they have had a frame to move.
            Assert.AreEqual(
                TagOutcome.None,
                FreezeRules.Resolve(
                    Role.Catcher,
                    actorIsFrozen: false,
                    Role.Runner,
                    targetIsFrozen: false,
                    targetIsImmune: true,
                    squaredDistance: 0f,
                    squaredRange: 1f));
        }

        [Test]
        public void The_same_runner_a_moment_later_can_be()
        {
            Assert.AreEqual(
                TagOutcome.Freeze,
                FreezeRules.Resolve(
                    Role.Catcher,
                    actorIsFrozen: false,
                    Role.Runner,
                    targetIsFrozen: false,
                    targetIsImmune: false,
                    squaredDistance: 0f,
                    squaredRange: 1f));
        }

        [Test]
        public void Immunity_does_not_stop_a_rescue()
        {
            // Being freed is not something anyone needs protecting from, and a
            // runner frozen with time left on the clock would otherwise be
            // stuck until it ran out.
            Assert.AreEqual(
                TagOutcome.Unfreeze,
                FreezeRules.Resolve(
                    Role.Runner,
                    actorIsFrozen: false,
                    Role.Runner,
                    targetIsFrozen: true,
                    targetIsImmune: true,
                    squaredDistance: 0f,
                    squaredRange: 1f));
        }

        [Test]
        public void Immunity_does_not_put_a_catcher_in_reach()
        {
            // Catchers are never a target, immune or otherwise.
            Assert.AreEqual(
                TagOutcome.None,
                FreezeRules.Resolve(
                    Role.Runner,
                    actorIsFrozen: false,
                    Role.Catcher,
                    targetIsFrozen: false,
                    targetIsImmune: false,
                    squaredDistance: 0f,
                    squaredRange: 1f));
        }
    }
}
