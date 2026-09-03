using BarafPaani.AI;
using NUnit.Framework;

namespace BarafPaani.Tests
{
    /// <summary>
    /// Priorities for both sides. These are the rules that decide whether the
    /// game reads as two teams playing against each other or as four characters
    /// wandering past one another.
    /// </summary>
    public class AiTacticsTests
    {
        private const float Danger = 9f;

        // ----- catcher -------------------------------------------------------

        [Test]
        public void Catcher_chases_anyone_it_can_see()
        {
            Assert.AreEqual(
                CatcherIntent.Chase,
                AiTactics.ChooseCatcherIntent(true, false, true));
        }

        [Test]
        public void Chasing_beats_guarding()
        {
            // Even with frozen runners on the floor and guarding available, a
            // runner in sight is the better use of its time.
            Assert.AreEqual(
                CatcherIntent.Chase,
                AiTactics.ChooseCatcherIntent(true, true, true));
        }

        [Test]
        public void Catcher_guards_the_ice_when_it_cannot_see_anyone()
        {
            Assert.AreEqual(
                CatcherIntent.Guard,
                AiTactics.ChooseCatcherIntent(false, true, true));
        }

        [Test]
        public void Catcher_stops_guarding_once_its_turn_at_it_runs_out()
        {
            // The break is what stops it camping one frozen runner forever,
            // which would stall the endgame.
            Assert.AreEqual(
                CatcherIntent.Wander,
                AiTactics.ChooseCatcherIntent(false, true, false));
        }

        [Test]
        public void Catcher_wanders_when_nobody_is_frozen_and_nobody_is_visible()
        {
            Assert.AreEqual(
                CatcherIntent.Wander,
                AiTactics.ChooseCatcherIntent(false, false, true));
        }

        // ----- runner --------------------------------------------------------

        [Test]
        public void Runner_flees_a_catcher_that_is_too_close()
        {
            Assert.AreEqual(
                RunnerIntent.Flee,
                AiTactics.ChooseRunnerIntent(true, 3f, Danger, false));
        }

        [Test]
        public void Saving_itself_beats_saving_a_teammate()
        {
            Assert.AreEqual(
                RunnerIntent.Flee,
                AiTactics.ChooseRunnerIntent(true, 3f, Danger, true));
        }

        [Test]
        public void A_catcher_in_sight_but_far_away_is_not_worth_fleeing()
        {
            Assert.AreEqual(
                RunnerIntent.Rescue,
                AiTactics.ChooseRunnerIntent(true, 20f, Danger, true));
        }

        [Test]
        public void Danger_is_inclusive_at_exactly_the_threshold()
        {
            Assert.AreEqual(
                RunnerIntent.Flee,
                AiTactics.ChooseRunnerIntent(true, Danger, Danger, true));
        }

        [Test]
        public void Runner_goes_for_a_rescue_when_nothing_is_chasing_it()
        {
            Assert.AreEqual(
                RunnerIntent.Rescue,
                AiTactics.ChooseRunnerIntent(false, float.MaxValue, Danger, true));
        }

        [Test]
        public void Runner_wanders_with_nothing_to_do()
        {
            Assert.AreEqual(
                RunnerIntent.Wander,
                AiTactics.ChooseRunnerIntent(false, float.MaxValue, Danger, false));
        }

        [Test]
        public void An_unreachable_teammate_is_not_a_rescue()
        {
            // The brain reports no reachable ally when the catcher is standing
            // over the frozen one. Walking in anyway just feeds it another runner.
            Assert.AreEqual(
                RunnerIntent.Wander,
                AiTactics.ChooseRunnerIntent(true, 20f, Danger, false));
        }
    }
}
