#if UNITY_EDITOR
using System.Collections;
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
    /// The round running for real: counting the runners in a live match, and
    /// ending when the catcher has frozen all of them.
    /// </summary>
    public class MatchFlowTests
    {
        [TearDown]
        public void TearDown()
        {
            if (NetworkClient.active || NetworkServer.active)
            {
                NetworkManager.singleton?.StopHost();
            }

            // NetworkManager is DontDestroyOnLoad, so it outlives the scene
            // reload and would carry this test's settings into the next one.
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

            GameNetworkManager manager = Object.FindFirstObjectByType<GameNetworkManager>();
            Assert.IsNotNull(manager, "the Game scene has no GameNetworkManager");

            manager.StartSinglePlayer();
            yield return new WaitForSeconds(1.5f);
        }

        [UnityTest]
        public IEnumerator A_fresh_round_counts_every_runner_as_free()
        {
            yield return StartMatch();

            MatchState match = Object.FindFirstObjectByType<MatchState>();
            Assert.IsNotNull(match, "no MatchState in the running match");

            Assert.AreEqual(3, match.RunnersTotal, "expected the three AI runners");
            Assert.AreEqual(
                match.RunnersTotal, match.RunnersFree, "nobody should start frozen");
            Assert.AreEqual(MatchOutcome.InProgress, match.Outcome);
            Assert.Greater(match.SecondsRemaining, 0f, "the clock should be running");
        }

        [UnityTest]
        public IEnumerator Hosting_a_multiplayer_match_alone_still_fills_with_bots()
        {
            // AI used to be a single-player special case, so hosting and waiting
            // for friends left you alone in an empty city.
            LogAssert.ignoreFailingMessages = true;

            SceneManager.LoadScene("Game_3Talwaar", LoadSceneMode.Single);
            yield return null;
            yield return null;

            GameNetworkManager manager = Object.FindFirstObjectByType<GameNetworkManager>();
            Assert.IsNotNull(manager);

            manager.StartMultiplayerHost();
            yield return new WaitForSeconds(1.5f);

            MatchState match = Object.FindFirstObjectByType<MatchState>();
            Assert.IsNotNull(match, "no MatchState in the hosted match");

            Assert.AreEqual(
                3, match.RunnersTotal, "a host on their own should still get a full set of runners");
        }

        [UnityTest]
        public IEnumerator Freezing_every_runner_ends_the_round()
        {
            yield return StartMatch();

            MatchState match = Object.FindFirstObjectByType<MatchState>();
            Assert.IsNotNull(match);

            foreach (NetworkIdentity identity in NetworkServer.spawned.Values)
            {
                if (identity != null
                    && identity.TryGetComponent(out PlayerRole role)
                    && role.Role == Role.Runner
                    && identity.TryGetComponent(out Freezable freezable))
                {
                    // Everyone is safe for a few seconds at the start of a round,
                    // so a freeze here would otherwise be refused for a reason
                    // that has nothing to do with what this test is about.
                    freezable.ClearImmunity();
                    freezable.Freeze();
                }
            }

            // MatchState recounts on a tick rather than every frame.
            yield return new WaitForSeconds(0.6f);

            Assert.AreEqual(0, match.RunnersFree, "every runner should be frozen");
            Assert.AreEqual(
                MatchOutcome.CatcherWins,
                match.Outcome,
                "freezing everyone should have ended the round");
        }
    }
}
#endif
