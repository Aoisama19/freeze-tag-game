using BarafPaani.Gameplay;
using NUnit.Framework;

namespace BarafPaani.Tests
{
    /// <summary>
    /// The win condition. Worth pinning hard, because the count it reads is the
    /// one a double freeze would corrupt: decrementing runners-left past where
    /// it should stop lets a match be decided by a bug rather than by play.
    /// FreezeRules refuses the double freeze, and this checks what is done with
    /// the number afterwards.
    /// </summary>
    public class MatchRulesTests
    {
        [Test]
        public void A_round_with_free_runners_and_time_left_is_still_running()
        {
            Assert.AreEqual(
                MatchOutcome.InProgress,
                MatchRules.Resolve(runnersTotal: 4, runnersFree: 2, secondsRemaining: 90f));
        }

        [Test]
        public void Freezing_every_runner_wins_it_for_the_catcher()
        {
            Assert.AreEqual(
                MatchOutcome.CatcherWins,
                MatchRules.Resolve(runnersTotal: 4, runnersFree: 0, secondsRemaining: 90f));
        }

        [Test]
        public void Running_out_the_clock_wins_it_for_the_runners()
        {
            Assert.AreEqual(
                MatchOutcome.RunnersWin,
                MatchRules.Resolve(runnersTotal: 4, runnersFree: 1, secondsRemaining: 0f));
        }

        [Test]
        public void The_last_freeze_beats_the_clock()
        {
            // Both conditions true at once. The catcher got there, so the catcher
            // wins — checking the clock first would have taken it away on a
            // technicality.
            Assert.AreEqual(
                MatchOutcome.CatcherWins,
                MatchRules.Resolve(runnersTotal: 4, runnersFree: 0, secondsRemaining: 0f));
        }

        [Test]
        public void One_runner_left_free_is_enough_to_keep_going()
        {
            Assert.AreEqual(
                MatchOutcome.InProgress,
                MatchRules.Resolve(runnersTotal: 5, runnersFree: 1, secondsRemaining: 10f));
        }

        [Test]
        public void A_round_with_no_runners_yet_decides_nothing()
        {
            // Covers the gap between a round starting and its characters
            // spawning. Without this the catcher would win instantly, every time.
            Assert.AreEqual(
                MatchOutcome.InProgress,
                MatchRules.Resolve(runnersTotal: 0, runnersFree: 0, secondsRemaining: 180f));
        }

        [Test]
        public void Negative_time_still_ends_the_round()
        {
            // A tick can arrive late, so the clock is not guaranteed to land on
            // exactly zero.
            Assert.AreEqual(
                MatchOutcome.RunnersWin,
                MatchRules.Resolve(runnersTotal: 3, runnersFree: 3, secondsRemaining: -2f));
        }
    }
}
