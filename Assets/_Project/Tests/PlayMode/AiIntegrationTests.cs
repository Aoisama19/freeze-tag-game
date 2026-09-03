using System.Collections;
using BarafPaani.AI;
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
    /// Runs the real scene as a single-player host. Covers the things unit tests
    /// cannot: that the NavMesh bakes, that agents land on it, and that AI
    /// runners are spawned with the right role.
    /// </summary>
    public class AiIntegrationTests
    {
        [TearDown]
        public void TearDown()
        {
            if (NetworkClient.active || NetworkServer.active)
            {
                NetworkManager.singleton?.StopHost();
            }
        }

        [UnityTest]
        public IEnumerator Single_player_spawns_ai_runners_that_actually_move()
        {
            LogAssert.ignoreFailingMessages = true;

            SceneManager.LoadScene("Game", LoadSceneMode.Single);
            yield return null;
            yield return null;

            GameNetworkManager manager = Object.FindFirstObjectByType<GameNetworkManager>();
            Assert.IsNotNull(manager, "the Game scene has no GameNetworkManager");

            manager.StartSinglePlayer();
            yield return new WaitForSeconds(1.5f);

            AiBrain[] brains = Object.FindObjectsByType<AiBrain>(FindObjectsSortMode.None);
            Assert.AreEqual(3, brains.Length, "expected three AI runners to fill the match");

            foreach (AiBrain brain in brains)
            {
                Assert.AreEqual(
                    Role.Runner,
                    brain.GetComponent<PlayerRole>().Role,
                    "an AI character was not made a runner");
            }

            Assert.IsNotNull(NetworkClient.localPlayer, "the host got no player");
            Assert.AreEqual(
                Role.Catcher,
                NetworkClient.localPlayer.GetComponent<PlayerRole>().Role,
                "the host should be the catcher in single-player");

            // Wandering should move them. If the NavMesh never baked the agents
            // have nowhere to go and every position stays put.
            Vector3[] before = new Vector3[brains.Length];
            for (int i = 0; i < brains.Length; i++)
            {
                before[i] = brains[i].transform.position;
            }

            yield return new WaitForSeconds(2f);

            bool anyMoved = false;
            for (int i = 0; i < brains.Length; i++)
            {
                if ((brains[i].transform.position - before[i]).sqrMagnitude > 0.01f)
                {
                    anyMoved = true;
                    break;
                }
            }

            Assert.IsTrue(anyMoved, "no AI runner moved — the NavMesh probably did not bake");
        }
    }
}
