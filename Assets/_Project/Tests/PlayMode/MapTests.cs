#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using BarafPaani.Core;
using BarafPaani.Gameplay;
using BarafPaani.Gameplay.PowerUps;
using Mirror;
using Unity.AI.Navigation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace BarafPaani.Tests
{
    /// <summary>
    /// Every map, not just the one the rest of the tests play on.
    ///
    /// Three scenes are generated from one table now, so a map missing its
    /// NavMesh or its network manager would fail only when somebody picked it
    /// from the menu — which is exactly the kind of thing nobody finds until a
    /// friend chooses the map you never tried.
    /// </summary>
    public class MapTests
    {
        private static IEnumerable<string> MapScenes()
        {
            for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            {
                string name = System.IO.Path.GetFileNameWithoutExtension(
                    SceneUtility.GetScenePathByBuildIndex(i));

                if (name.StartsWith("Game_"))
                {
                    yield return name;
                }
            }
        }

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

        [UnityTest]
        public IEnumerator There_is_more_than_one_map_to_play()
        {
            yield return null;

            List<string> maps = new List<string>(MapScenes());

            Assert.GreaterOrEqual(maps.Count, 2, "the menu has nothing to choose between");
        }

        [UnityTest]
        public IEnumerator Every_map_is_playable()
        {
            foreach (string map in MapScenes())
            {
                LogAssert.ignoreFailingMessages = true;

                SceneManager.LoadScene(map, LoadSceneMode.Single);
                yield return null;
                yield return null;

                Assert.IsNotNull(
                    Object.FindFirstObjectByType<GameNetworkManager>(), $"{map} has no network manager");

                Assert.IsNotNull(
                    Object.FindFirstObjectByType<NavMeshSurface>(), $"{map} has no NavMesh surface");

                Assert.IsNotNull(
                    Object.FindFirstObjectByType<Canvas>(), $"{map} has no HUD");

                // The one that actually shipped broken. Scenes for maps added
                // after the first are created from an empty scene, which has no
                // camera, no listener and no light, and the builder used to
                // assume all three were already there. The result was a map that
                // loaded, ran a match, and rendered nothing at all.
                Camera view = Camera.main;
                Assert.IsNotNull(view, $"{map} has no camera tagged MainCamera");

                Assert.IsTrue(
                    view.isActiveAndEnabled, $"{map} has a main camera that is not rendering");

                Assert.IsNotNull(
                    Object.FindFirstObjectByType<AudioListener>(),
                    $"{map} has nothing listening, so the whole map is silent");

                bool sun = false;

                foreach (Light light in
                         Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                {
                    sun |= light.type == LightType.Directional;
                }

                Assert.IsTrue(sun, $"{map} has no directional light, so it renders unlit");

                // The one that would leave the AI standing still: a surface with
                // no baked data attached to it.
                NavMeshSurface surface = Object.FindFirstObjectByType<NavMeshSurface>();
                Assert.IsNotNull(
                    surface.navMeshData,
                    $"{map} has an unbaked NavMesh, so nothing would move");

                NetworkStartPosition[] spawns =
                    Object.FindObjectsByType<NetworkStartPosition>(FindObjectsSortMode.None);
                Assert.GreaterOrEqual(spawns.Length, 4, $"{map} has too few spawn points");

                // Inactive included on purpose. Mirror deactivates scene
                // objects that carry a NetworkIdentity until the server spawns
                // them, and no host has started here — this is inspecting what
                // the scene contains, not what a running match has woken up.
                PowerUpPickup[] pickups = Object.FindObjectsByType<PowerUpPickup>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None);

                Assert.IsNotEmpty(pickups, $"{map} has no power-ups");

                foreach (NetworkStartPosition spawn in spawns)
                {
                    Assert.Less(
                        spawn.transform.position.y, 6f, $"{map} has a spawn point on a roof");
                }

                if (NetworkManager.singleton != null)
                {
                    Object.DestroyImmediate(NetworkManager.singleton.gameObject);
                }
            }
        }

        [UnityTest]
        public IEnumerator Every_map_actually_starts_a_match()
        {
            foreach (string map in MapScenes())
            {
                LogAssert.ignoreFailingMessages = true;

                SceneManager.LoadScene(map, LoadSceneMode.Single);
                yield return null;
                yield return null;

                GameNetworkManager manager = Object.FindFirstObjectByType<GameNetworkManager>();
                manager.StartSinglePlayer();

                yield return new WaitForSeconds(1.5f);

                MatchState match = Object.FindFirstObjectByType<MatchState>();
                Assert.IsNotNull(match, $"{map} started no match");

                Assert.AreEqual(
                    3, match.RunnersTotal, $"{map} did not fill with runners");

                // Agents only get a path if the bake actually covered the arena.
                foreach (NavMeshAgent agent in
                         Object.FindObjectsByType<NavMeshAgent>(FindObjectsSortMode.None))
                {
                    Assert.IsTrue(
                        agent.isOnNavMesh, $"{map} spawned a bot off the NavMesh");
                }

                if (NetworkClient.active || NetworkServer.active)
                {
                    NetworkManager.singleton?.StopHost();
                }

                if (NetworkManager.singleton != null)
                {
                    Object.DestroyImmediate(NetworkManager.singleton.gameObject);
                }
            }
        }
    }
}
#endif
