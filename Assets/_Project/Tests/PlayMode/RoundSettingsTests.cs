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
    /// How a round is set up and settled: spawn immunity, the length the menu
    /// asked for, and the score it leaves behind.
    /// </summary>
    public class RoundSettingsTests
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

        private static IEnumerator StartMatch()
        {
            LogAssert.ignoreFailingMessages = true;

            SceneManager.LoadScene("Game_3Talwaar", LoadSceneMode.Single);
            yield return null;
            yield return null;

            Object.FindFirstObjectByType<GameNetworkManager>().StartSinglePlayer();
            yield return new WaitForSeconds(1.5f);
        }

        private static GameObject Runner()
        {
            return NetworkServer.spawned.Values
                .Where(identity => identity != null)
                .Select(identity => identity.gameObject)
                .First(character =>
                    character.TryGetComponent(out PlayerRole role)
                    && role.Role == Role.Runner
                    && character.GetComponent<Freezable>() != null);
        }

        [UnityTest]
        public IEnumerator Everyone_is_safe_when_a_round_begins()
        {
            yield return StartMatch();

            int checked_ = 0;

            foreach (NetworkIdentity identity in NetworkServer.spawned.Values)
            {
                if (identity == null || !identity.TryGetComponent(out Freezable freezable))
                {
                    continue;
                }

                Assert.IsTrue(
                    freezable.IsImmune, $"{identity.name} could be taken before it could move");

                checked_++;
            }

            Assert.Greater(checked_, 0, "no characters were found to check");
        }

        [UnityTest]
        public IEnumerator A_safe_runner_cannot_be_frozen()
        {
            yield return StartMatch();

            Freezable runner = Runner().GetComponent<Freezable>();
            Assert.IsTrue(runner.IsImmune, "this test needs the runner still safe");

            // Called directly, which is how the AI and the tests reach it. A
            // rule enforced only in FreezeRules would not be enforced here.
            Assert.IsFalse(runner.Freeze(), "immunity should have refused the freeze");
            Assert.IsFalse(runner.IsFrozen);
        }

        [UnityTest]
        public IEnumerator Immunity_wears_off()
        {
            yield return StartMatch();

            Freezable runner = Runner().GetComponent<Freezable>();

            Assert.Greater(runner.ImmunityRemaining, 0f, "the HUD needs something to count down");

            // Long enough to outlast the three seconds granted at round start.
            yield return new WaitForSeconds(3.5f);

            Assert.IsFalse(runner.IsImmune, "immunity should not last the whole round");
            Assert.AreEqual(0f, runner.ImmunityRemaining);

            // Thawed first: the AI catcher has had three and a half seconds to
            // reach this runner, and a Freeze that returns false because it was
            // already frozen would look exactly like immunity still holding.
            runner.Unfreeze();

            Assert.IsTrue(runner.Freeze(), "and the runner should be catchable again");
        }

        [UnityTest]
        public IEnumerator A_round_is_as_long_as_the_menu_asked_for()
        {
            yield return StartMatch();

            GameNetworkManager manager = Object.FindFirstObjectByType<GameNetworkManager>();
            MatchState match = Object.FindFirstObjectByType<MatchState>();

            manager.RoundSeconds = 60f;
            match.RestartNow();

            yield return null;

            Assert.LessOrEqual(match.SecondsRemaining, 60f);
            Assert.Greater(match.SecondsRemaining, 55f, "the round did not take the menu's length");
        }

        [UnityTest]
        public IEnumerator An_impossible_round_length_is_pulled_back()
        {
            yield return StartMatch();

            GameNetworkManager manager = Object.FindFirstObjectByType<GameNetworkManager>();

            manager.RoundSeconds = 99999f;

            Assert.AreEqual(
                MatchSetup.MaxRoundSeconds,
                manager.RoundSeconds,
                "a silly number should be clamped, not taken at face value");
        }

        [UnityTest]
        public IEnumerator A_round_is_scored_once_and_not_once_per_tick()
        {
            // MatchState settles the outcome on a quarter-second tick and then
            // sits on it for six seconds before restarting. Scored in the wrong
            // place, one win would be counted two dozen times before the next
            // round began, and nothing would look wrong until the number did.
            yield return StartMatch();

            MatchState match = Object.FindFirstObjectByType<MatchState>();
            Assert.IsNotNull(match);

            Assert.AreEqual(0, match.CatcherWins, "nothing has been won yet");
            Assert.AreEqual(0, match.RunnerWins);

            foreach (NetworkIdentity identity in NetworkServer.spawned.Values)
            {
                if (identity != null
                    && identity.TryGetComponent(out PlayerRole role)
                    && role.Role == Role.Runner
                    && identity.TryGetComponent(out Freezable freezable))
                {
                    freezable.ClearImmunity();
                    freezable.Freeze();
                }
            }

            // Long enough for several ticks, well short of the restart delay.
            yield return new WaitForSeconds(1.5f);

            Assert.AreEqual(MatchOutcome.CatcherWins, match.Outcome);
            Assert.AreEqual(1, match.CatcherWins, "the round was scored more than once");
            Assert.AreEqual(0, match.RunnerWins, "the wrong side was given the round");
        }

        [UnityTest]
        public IEnumerator Rounds_are_numbered_from_one()
        {
            yield return StartMatch();

            MatchState match = Object.FindFirstObjectByType<MatchState>();

            Assert.AreEqual(1, match.RoundNumber, "the first round should be round one");

            match.RestartNow();
            yield return null;

            Assert.AreEqual(2, match.RoundNumber);

            // The score is the match's, so restarting a round does not clear it.
            Assert.AreEqual(0, match.CatcherWins);
        }

    }
}
#endif
