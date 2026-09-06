#if UNITY_EDITOR
using System.Collections;
using System.Reflection;
using BarafPaani.Core;
using BarafPaani.UI;
using Mirror;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace BarafPaani.Tests
{
    /// <summary>
    /// The scene a joining player passes through.
    ///
    /// It exists so a client never loads a map of its own. A client needs a
    /// NetworkManager to connect through, and every one of those used to live
    /// inside a map — so joining meant loading whichever map the joiner had
    /// selected, connecting, and only then being moved to the host's.
    /// </summary>
    public class ConnectingTests
    {
        private const string SceneName = "Connecting";

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

        private static IEnumerator Load()
        {
            LogAssert.ignoreFailingMessages = true;

            SceneManager.LoadScene(SceneName, LoadSceneMode.Single);
            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator It_ships_in_the_build()
        {
            yield return null;

            bool found = false;

            for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            {
                string name = System.IO.Path.GetFileNameWithoutExtension(
                    SceneUtility.GetScenePathByBuildIndex(i));

                found |= name == SceneName;
            }

            // Missing from the build, JOIN would work in the editor and fail in
            // a player, which is the worst place to find out.
            Assert.IsTrue(found, "the connecting scene is not in the build settings");
        }

        [UnityTest]
        public IEnumerator It_can_connect_without_a_map()
        {
            yield return Load();

            // The whole point: something to connect through that is not a map.
            Assert.IsNotNull(
                Object.FindFirstObjectByType<GameNetworkManager>(),
                "nothing here can start a client, so joining would need a map first");

            Assert.IsNotNull(
                Object.FindFirstObjectByType<ConnectingScreen>(), "no status to show");
        }

        [UnityTest]
        public IEnumerator It_can_be_looked_at_and_pressed()
        {
            yield return Load();

            Assert.IsNotNull(Camera.main, "no camera, so the screen would be blank");

            Assert.IsNotNull(
                Object.FindFirstObjectByType<AudioListener>(), "nothing listening");

            EventSystem events = Object.FindFirstObjectByType<EventSystem>();
            Assert.IsNotNull(events, "no event system, so Back is not clickable");

            // The exact mistake that shipped the main menu unclickable once.
            Assert.IsNotNull(
                events.GetComponent<InputSystemUIInputModule>(),
                "the legacy input module cannot deliver a click in this project");

            Assert.IsNull(
                events.GetComponent<StandaloneInputModule>(),
                "StandaloneInputModule reads UnityEngine.Input, which throws here");

            Assert.IsNotNull(
                Object.FindFirstObjectByType<GraphicRaycaster>(), "clicks would hit nothing");
        }

        [UnityTest]
        public IEnumerator It_knows_what_to_spawn_before_the_map_arrives()
        {
            yield return Load();

            GameNetworkManager manager = Object.FindFirstObjectByType<GameNetworkManager>();

            // A client is connected, and told about spawned objects, before the
            // host's map has finished loading. Without the prefabs registered
            // here it could not create what the server describes.
            Assert.IsNotNull(manager.playerPrefab, "no player prefab");
            Assert.AreEqual(2, manager.spawnPrefabs.Count, "the AI and the decoy both have to be known");

            foreach (GameObject prefab in manager.spawnPrefabs)
            {
                Assert.IsNotNull(prefab, "a spawnable prefab is missing");
            }
        }

        [UnityTest]
        public IEnumerator Joining_from_the_menu_loads_this_and_not_a_map()
        {
            LogAssert.ignoreFailingMessages = true;

            SceneManager.LoadScene("MainMenu", LoadSceneMode.Single);
            yield return null;
            yield return null;

            MenuController menu = Object.FindFirstObjectByType<MenuController>();
            Assert.IsNotNull(menu);

            string target = (string)typeof(MenuController)
                .GetField("_connectingScene", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(menu);

            Assert.AreEqual(
                SceneName, target, "joining would load a map of the joiner's own choosing");
        }
    }
}
#endif
