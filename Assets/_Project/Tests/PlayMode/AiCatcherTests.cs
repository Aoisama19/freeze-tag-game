using System.Collections;
using System.Reflection;
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
    /// The other way round: an AI takes the catcher role and the human plays a
    /// runner. Worth covering because the roles are handed out in two different
    /// places — OnServerAddPlayer for humans, SpawnAiCharacters for bots — and
    /// they have to agree on there being exactly one catcher.
    /// </summary>
    public class AiCatcherTests
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

        [UnityTest]
        public IEnumerator An_ai_takes_the_catcher_role_when_the_human_does_not()
        {
            LogAssert.ignoreFailingMessages = true;

            SceneManager.LoadScene("Game", LoadSceneMode.Single);
            yield return null;
            yield return null;

            GameNetworkManager manager = Object.FindFirstObjectByType<GameNetworkManager>();
            Assert.IsNotNull(manager, "the Game scene has no GameNetworkManager");

            // The toggle is a serialised field, deliberately not public API.
            FieldInfo humanIsCatcher = typeof(GameNetworkManager)
                .GetField("_humanIsCatcher", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(humanIsCatcher, "_humanIsCatcher has been renamed");
            humanIsCatcher.SetValue(manager, false);

            manager.StartSinglePlayer();
            yield return new WaitForSeconds(1.5f);

            AiBrain[] brains = Object.FindObjectsByType<AiBrain>(FindObjectsSortMode.None);
            Assert.AreEqual(4, brains.Length, "expected one AI catcher plus three AI runners");

            int aiCatchers = 0;
            foreach (AiBrain brain in brains)
            {
                if (brain.GetComponent<PlayerRole>().Role == Role.Catcher)
                {
                    aiCatchers++;
                }
            }

            Assert.AreEqual(1, aiCatchers, "there should be exactly one AI catcher");

            Assert.IsNotNull(NetworkClient.localPlayer, "the host got no player");
            Assert.AreEqual(
                Role.Runner,
                NetworkClient.localPlayer.GetComponent<PlayerRole>().Role,
                "the human should be a runner when the AI is catching");
        }
    }
}
