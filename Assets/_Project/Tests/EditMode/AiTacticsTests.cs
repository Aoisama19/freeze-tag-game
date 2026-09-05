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
        private const float Safe = 15f;

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
                AiTactics.ChooseRunnerIntent(RunnerIntent.Wander, true, 3f, Danger, Safe, false));
        }

        [Test]
        public void Saving_itself_beats_saving_a_teammate()
        {
            Assert.AreEqual(
                RunnerIntent.Flee,
                AiTactics.ChooseRunnerIntent(RunnerIntent.Rescue, true, 3f, Danger, Safe, true));
        }

        [Test]
        public void A_catcher_in_sight_but_far_away_is_not_worth_fleeing()
        {
            Assert.AreEqual(
                RunnerIntent.Rescue,
                AiTactics.ChooseRunnerIntent(RunnerIntent.Wander, true, 20f, Danger, Safe, true));
        }

        [Test]
        public void Danger_is_inclusive_at_exactly_the_threshold()
        {
            Assert.AreEqual(
                RunnerIntent.Flee,
                AiTactics.ChooseRunnerIntent(RunnerIntent.Wander, true, Danger, Danger, Safe, true));
        }

        [Test]
        public void Runner_goes_for_a_rescue_when_nothing_is_chasing_it()
        {
            Assert.AreEqual(
                RunnerIntent.Rescue,
                AiTactics.ChooseRunnerIntent(RunnerIntent.Wander, false, float.MaxValue, Danger, Safe, true));
        }

        [Test]
        public void Runner_wanders_with_nothing_to_do()
        {
            Assert.AreEqual(
                RunnerIntent.Wander,
                AiTactics.ChooseRunnerIntent(RunnerIntent.Wander, false, float.MaxValue, Danger, Safe, false));
        }

        [Test]
        public void An_unreachable_teammate_is_not_a_rescue()
        {
            // The brain reports no reachable ally when the catcher is standing
            // over the frozen one. Walking in anyway just feeds it another runner.
            Assert.AreEqual(
                RunnerIntent.Wander,
                AiTactics.ChooseRunnerIntent(RunnerIntent.Wander, true, 20f, Danger, Safe, false));
        }
        [Test]
        public void A_fleeing_runner_keeps_running_until_it_is_genuinely_clear()
        {
            // Between danger and safe, a runner already fleeing stays fleeing.
            // Without this it turns back the moment the catcher is a hair out of
            // danger range, then flees again, and dithers on the spot.
            Assert.AreEqual(
                RunnerIntent.Flee,
                AiTactics.ChooseRunnerIntent(RunnerIntent.Flee, true, 12f, Danger, Safe, true));
        }

        [Test]
        public void A_runner_that_was_not_fleeing_does_not_start_at_the_same_distance()
        {
            // Same distance as the test above, different previous decision. That
            // gap is the whole point of the hysteresis.
            Assert.AreEqual(
                RunnerIntent.Rescue,
                AiTactics.ChooseRunnerIntent(RunnerIntent.Wander, true, 12f, Danger, Safe, true));
        }

        [Test]
        public void A_fleeing_runner_goes_back_to_rescuing_once_past_the_safe_distance()
        {
            Assert.AreEqual(
                RunnerIntent.Rescue,
                AiTactics.ChooseRunnerIntent(RunnerIntent.Flee, true, Safe + 1f, Danger, Safe, true));
        }

        [Test]
        public void A_remembered_catcher_is_still_worth_fleeing_from()
        {
            // catcherKnown covers memory as well as sight, so a catcher that has
            // just stepped behind a building is not instantly forgotten.
            Assert.AreEqual(
                RunnerIntent.Flee,
                AiTactics.ChooseRunnerIntent(RunnerIntent.Wander, true, 4f, Danger, Safe, true));
        }
    }
}
