using BarafPaani.Gameplay;
using NUnit.Framework;

namespace BarafPaani.Tests
{
    /// <summary>
    /// Locks down who knows what. These are the rules the minimap draws and the
    /// AI acts on, and the whole point of the shared rule is that those two can
    /// never disagree — so the rules are pinned here rather than in either.
    /// </summary>
    public class MapKnowledgeTests
    {
        [Test]
        public void A_catcher_knows_where_every_runner_is()
        {
            Assert.IsTrue(
                MapKnowledge.KnowsPosition(Role.Catcher, Role.Runner, targetSeen: false),
                "the catcher should not need to see a runner to know where it is");
        }

        [Test]
        public void A_catcher_never_needs_line_of_sight()
        {
            Assert.IsFalse(MapKnowledge.NeedsLineOfSight(Role.Catcher, Role.Runner));
            Assert.IsFalse(MapKnowledge.NeedsLineOfSight(Role.Catcher, Role.Catcher));
        }

        [Test]
        public void A_runner_always_knows_where_its_teammates_are()
        {
            Assert.IsTrue(
                MapKnowledge.KnowsPosition(Role.Runner, Role.Runner, targetSeen: false),
                "team-mates are shouting, and isFrozen is replicated to everyone anyway");
        }

        [Test]
        public void A_runner_only_knows_the_catcher_when_it_can_see_it()
        {
            Assert.IsFalse(
                MapKnowledge.KnowsPosition(Role.Runner, Role.Catcher, targetSeen: false),
                "an unseen catcher should not appear to a runner");

            Assert.IsTrue(
                MapKnowledge.KnowsPosition(Role.Runner, Role.Catcher, targetSeen: true));
        }

        [Test]
        public void Only_a_runner_looking_for_the_catcher_needs_line_of_sight()
        {
            Assert.IsTrue(MapKnowledge.NeedsLineOfSight(Role.Runner, Role.Catcher));
            Assert.IsFalse(MapKnowledge.NeedsLineOfSight(Role.Runner, Role.Runner));
        }

        [Test]
        public void A_hidden_runner_is_lost_even_to_the_catcher()
        {
            // The catcher's global view is exactly what invisibility is bought
            // to beat, so this is the case that matters.
            Assert.IsFalse(
                MapKnowledge.KnowsPosition(
                    Role.Catcher, Role.Runner, targetSeen: true, targetHidden: true));
        }

        [Test]
        public void A_hidden_team_mate_drops_off_the_map_too()
        {
            // Allies are otherwise known unconditionally.
            Assert.IsFalse(
                MapKnowledge.KnowsPosition(
                    Role.Runner, Role.Runner, targetSeen: true, targetHidden: true));
        }

        [Test]
        public void A_hidden_catcher_is_not_given_away_by_being_in_view()
        {
            Assert.IsFalse(
                MapKnowledge.KnowsPosition(
                    Role.Runner, Role.Catcher, targetSeen: true, targetHidden: true));
        }

        [Test]
        public void Nothing_changes_for_a_character_who_is_not_hidden()
        {
            Assert.IsTrue(
                MapKnowledge.KnowsPosition(
                    Role.Catcher, Role.Runner, targetSeen: false, targetHidden: false));

            Assert.IsFalse(
                MapKnowledge.KnowsPosition(
                    Role.Runner, Role.Catcher, targetSeen: false, targetHidden: false));
        }
    }
}
