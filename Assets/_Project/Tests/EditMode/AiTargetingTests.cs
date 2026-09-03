using BarafPaani.AI;
using BarafPaani.Gameplay;
using NUnit.Framework;

namespace BarafPaani.Tests
{
    /// <summary>
    /// Who an agent chases. One brain serves both roles, so these cover the
    /// catcher and the runner side of the same function — the old build had two
    /// AI classes that drifted apart instead (issue 6).
    /// </summary>
    public class AiTargetingTests
    {
        [Test]
        public void Catcher_chases_a_runner_still_in_the_game()
        {
            Assert.IsTrue(AiTargeting.WantsToApproach(Role.Catcher, false, Role.Runner, false));
        }

        [Test]
        public void Catcher_ignores_a_runner_already_frozen()
        {
            Assert.IsFalse(AiTargeting.WantsToApproach(Role.Catcher, false, Role.Runner, true));
        }

        [Test]
        public void Runner_goes_to_a_frozen_teammate()
        {
            Assert.IsTrue(AiTargeting.WantsToApproach(Role.Runner, false, Role.Runner, true));
        }

        [Test]
        public void Runner_ignores_a_teammate_who_is_fine()
        {
            Assert.IsFalse(AiTargeting.WantsToApproach(Role.Runner, false, Role.Runner, false));
        }

        [Test]
        public void A_frozen_agent_chases_nobody()
        {
            Assert.IsFalse(AiTargeting.WantsToApproach(Role.Catcher, true, Role.Runner, false));
            Assert.IsFalse(AiTargeting.WantsToApproach(Role.Runner, true, Role.Runner, true));
        }

        [Test]
        public void Nobody_chases_the_catcher()
        {
            Assert.IsFalse(AiTargeting.WantsToApproach(Role.Runner, false, Role.Catcher, false));
            Assert.IsFalse(AiTargeting.WantsToApproach(Role.Catcher, false, Role.Catcher, false));
        }
    }
}
