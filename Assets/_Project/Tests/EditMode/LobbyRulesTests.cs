using BarafPaani.Gameplay;
using NUnit.Framework;

namespace BarafPaani.Tests
{
    public class LobbyRulesTests
    {
        [Test]
        public void Sides_can_be_changed_in_the_lobby()
        {
            Assert.IsTrue(LobbyRules.CanChangeRole(MatchPhase.Lobby));
        }

        [Test]
        public void But_not_once_the_round_is_running()
        {
            // Otherwise a catcher about to lose simply stops being the catcher.
            Assert.IsFalse(LobbyRules.CanChangeRole(MatchPhase.Playing));
            Assert.IsFalse(LobbyRules.CanChangeRole(MatchPhase.Over));
        }

        [Test]
        public void The_catcher_seat_is_first_come()
        {
            Assert.IsTrue(LobbyRules.CanTakeRole(
                MatchPhase.Lobby, Role.Catcher, alreadyThisRole: false, someoneElseIsCatcher: false));

            Assert.IsFalse(LobbyRules.CanTakeRole(
                MatchPhase.Lobby, Role.Catcher, alreadyThisRole: false, someoneElseIsCatcher: true));
        }

        [Test]
        public void Anyone_can_always_choose_to_run()
        {
            Assert.IsTrue(LobbyRules.CanTakeRole(
                MatchPhase.Lobby, Role.Runner, alreadyThisRole: false, someoneElseIsCatcher: true));
        }

        [Test]
        public void Asking_for_the_side_you_are_already_on_changes_nothing()
        {
            Assert.IsFalse(LobbyRules.CanTakeRole(
                MatchPhase.Lobby, Role.Runner, alreadyThisRole: true, someoneElseIsCatcher: false));
        }

        [Test]
        public void Nothing_can_be_taken_once_the_round_has_started()
        {
            Assert.IsFalse(LobbyRules.CanTakeRole(
                MatchPhase.Playing, Role.Catcher, alreadyThisRole: false, someoneElseIsCatcher: false));
        }

        [Test]
        public void With_bots_on_one_person_is_enough()
        {
            // The server puts an AI in whatever seat nobody took.
            Assert.IsTrue(LobbyRules.CanStart(fillWithBots: true, humanCatchers: 0, humanRunners: 1));
            Assert.IsTrue(LobbyRules.CanStart(fillWithBots: true, humanCatchers: 1, humanRunners: 0));
        }

        [Test]
        public void With_bots_on_an_empty_match_still_cannot_start()
        {
            Assert.IsFalse(LobbyRules.CanStart(fillWithBots: true, humanCatchers: 0, humanRunners: 0));
        }

        [Test]
        public void With_bots_off_somebody_has_to_catch()
        {
            // The exact case that used to produce a round nobody could win: the
            // host chooses Runner, bots are off, and nothing is chasing.
            Assert.IsFalse(LobbyRules.CanStart(fillWithBots: false, humanCatchers: 0, humanRunners: 3));

            Assert.AreEqual(
                "Bots are off, so somebody has to be the catcher",
                LobbyRules.WhyNotStarting(fillWithBots: false, humanCatchers: 0, humanRunners: 3));
        }

        [Test]
        public void With_bots_off_somebody_has_to_run()
        {
            Assert.IsFalse(LobbyRules.CanStart(fillWithBots: false, humanCatchers: 1, humanRunners: 0));
        }

        [Test]
        public void With_bots_off_one_catcher_and_a_runner_is_a_match()
        {
            Assert.IsTrue(LobbyRules.CanStart(fillWithBots: false, humanCatchers: 1, humanRunners: 1));

            Assert.IsEmpty(
                LobbyRules.WhyNotStarting(fillWithBots: false, humanCatchers: 1, humanRunners: 1));
        }
    }
}
