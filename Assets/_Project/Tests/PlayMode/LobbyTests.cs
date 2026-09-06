#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using BarafPaani.Core;
using BarafPaani.Gameplay;
using Mirror;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace BarafPaani.Tests
{
    /// <summary>
    /// The lobby: waiting before a hosted match, choosing sides, and refusing to
    /// start a match nobody could win.
    /// </summary>
    public class LobbyTests
    {
        [TearDown]
        public void TearDown()
        {
            if (NetworkClient.active || NetworkServer.active)
            {
                NetworkManager.singleton?.StopHost();
            }

            if (NetworkManager.singleton != null)
            {
                Object.DestroyImmediate(NetworkManager.singleton.gameObject);
            }
        }

        private static IEnumerator Host(bool fillWithBots = true)
        {
            LogAssert.ignoreFailingMessages = true;

            SceneManager.LoadScene("Game_3Talwaar", LoadSceneMode.Single);
            yield return null;
            yield return null;

            GameNetworkManager manager = Object.FindFirstObjectByType<GameNetworkManager>();
            Assert.IsNotNull(manager, "the Game scene has no GameNetworkManager");

            manager.FillWithBots = fillWithBots;
            manager.StartMultiplayerHost();
            yield return new WaitForSeconds(1.5f);
        }

        private static IEnumerator SinglePlayer()
        {
            LogAssert.ignoreFailingMessages = true;

            SceneManager.LoadScene("Game_3Talwaar", LoadSceneMode.Single);
            yield return null;
            yield return null;

            Object.FindFirstObjectByType<GameNetworkManager>().StartSinglePlayer();
            yield return new WaitForSeconds(1.5f);
        }

        private static MatchState Match()
        {
            MatchState match = Object.FindFirstObjectByType<MatchState>();
            Assert.IsNotNull(match, "no MatchState in the match");

            return match;
        }

        private static PlayerRole HumanRole()
        {
            return NetworkServer.spawned.Values
                .Where(identity => identity != null && identity.connectionToClient != null)
                .Select(identity => identity.GetComponent<PlayerRole>())
                .First(role => role != null);
        }

        [UnityTest]
        public IEnumerator Hosting_waits_in_the_lobby()
        {
            yield return Host();

            Assert.AreEqual(
                MatchPhase.Lobby, Match().Phase, "a hosted match should wait for people");
        }

        [UnityTest]
        public IEnumerator Single_player_does_not_wait_for_anyone()
        {
            yield return SinglePlayer();

            Assert.AreEqual(
                MatchPhase.Playing,
                Match().Phase,
                "there is nobody to wait for, and the side was chosen in the menu");
        }

        [UnityTest]
        public IEnumerator Nobody_can_be_frozen_while_the_lobby_is_open()
        {
            yield return Host();

            MatchState match = Match();
            Assert.AreEqual(MatchPhase.Lobby, match.Phase);
            Assert.IsFalse(match.FreezingAllowed, "a catcher must not start early");
        }

        [UnityTest]
        public IEnumerator A_player_can_change_sides_in_the_lobby()
        {
            yield return Host();

            PlayerRole me = HumanRole();
            Role started = me.Role;
            Role wanted = started == Role.Catcher ? Role.Runner : Role.Catcher;

            Assert.IsTrue(me.RequestRole(wanted), "the lobby should allow a change of side");
            Assert.AreEqual(wanted, me.Role);
        }

        [UnityTest]
        public IEnumerator Sides_are_locked_once_the_round_starts()
        {
            yield return Host();

            PlayerRole me = HumanRole();
            Assert.IsTrue(Match().StartMatch(), "a hosted match with bots on should start");

            yield return null;

            Role held = me.Role;
            Role wanted = held == Role.Catcher ? Role.Runner : Role.Catcher;

            Assert.IsFalse(
                me.RequestRole(wanted),
                "a catcher about to lose could otherwise stop being the catcher");

            Assert.AreEqual(held, me.Role);
        }

        [UnityTest]
        public IEnumerator A_person_can_take_the_catcher_seat_from_a_bot()
        {
            // With bots on there is always an AI catcher the moment nobody
            // human took it, so a host who picked Runner in the menu has to be
            // able to change their mind. Counting the bot as an occupant would
            // have made the seat unreachable for the rest of the match.
            yield return Host();

            PlayerRole me = HumanRole();

            if (me.Role == Role.Catcher)
            {
                Assert.IsTrue(me.RequestRole(Role.Runner));
            }

            yield return new WaitForSeconds(0.3f);

            Assert.IsTrue(me.RequestRole(Role.Catcher), "a bot must not hold the seat against a person");
            Assert.AreEqual(Role.Catcher, me.Role);

            Match().CountHumans(out int catchers, out _);
            Assert.AreEqual(1, catchers, "and there should be exactly one of them");

            foreach (NetworkIdentity identity in NetworkServer.spawned.Values)
            {
                if (identity == null
                    || identity.connectionToClient != null
                    || !identity.TryGetComponent(out PlayerRole bot))
                {
                    continue;
                }

                Assert.AreNotEqual(
                    Role.Catcher, bot.Role, "the bot should have stood down, not made a second catcher");
            }
        }

        [UnityTest]
        public IEnumerator A_match_with_nobody_catching_refuses_to_start()
        {
            // The sharp edge this whole thing exists to remove: bots off, the
            // host chooses to run, and nothing is chasing.
            yield return Host(fillWithBots: false);

            MatchState match = Match();
            PlayerRole me = HumanRole();

            if (me.Role == Role.Catcher)
            {
                Assert.IsTrue(me.RequestRole(Role.Runner));
            }

            match.CountHumans(out int catchers, out int runners);
            Assert.AreEqual(0, catchers, "this test needs nobody catching");
            Assert.Greater(runners, 0);

            Assert.IsFalse(
                match.StartMatch(), "a round nobody could win should not be startable");

            Assert.AreEqual(MatchPhase.Lobby, match.Phase, "and it should still be waiting");
        }

        [UnityTest]
        public IEnumerator One_catcher_and_nobody_to_chase_is_not_a_match_either()
        {
            yield return Host(fillWithBots: false);

            MatchState match = Match();
            PlayerRole me = HumanRole();

            if (me.Role != Role.Catcher)
            {
                Assert.IsTrue(me.RequestRole(Role.Catcher));
            }

            match.CountHumans(out int catchers, out int runners);
            Assert.AreEqual(1, catchers);
            Assert.AreEqual(0, runners, "bots are off, so the host is on their own");

            Assert.IsFalse(match.StartMatch(), "there is nobody to catch");
            Assert.AreEqual(MatchPhase.Lobby, match.Phase);
        }

        [UnityTest]
        public IEnumerator Turning_bots_back_on_makes_the_same_lobby_startable()
        {
            // The positive half of the pair above, and the only way to reach it
            // with one person in the match.
            yield return Host(fillWithBots: false);

            MatchState match = Match();
            Assert.IsFalse(match.StartMatch(), "one person and no bots is not a match");

            Object.FindFirstObjectByType<GameNetworkManager>().FillWithBots = true;

            Assert.IsTrue(match.StartMatch(), "with bots filling in, one person is enough");
            Assert.AreEqual(MatchPhase.Playing, match.Phase);
        }

    }
}
#endif
