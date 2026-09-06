#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using System.Reflection;
using BarafPaani.Core;
using BarafPaani.Gameplay;
using BarafPaani.Gameplay.PowerUps;
using Mirror;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace BarafPaani.Tests
{
    /// <summary>
    /// Power-ups in a running match: picking one up, spending it, and the
    /// effect actually reaching the character.
    /// </summary>
    public class PowerUpTests
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

            SceneManager.LoadScene("Game", LoadSceneMode.Single);
            yield return null;
            yield return null;

            GameNetworkManager manager = Object.FindFirstObjectByType<GameNetworkManager>();
            Assert.IsNotNull(manager, "the Game scene has no GameNetworkManager");

            manager.StartSinglePlayer();
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
                    && character.GetComponent<PowerUpHolder>() != null);
        }

        private static float AgentSpeed(GameObject character)
        {
            NavMeshAgent agent = character.GetComponent<NavMeshAgent>();

            return agent != null ? agent.speed : 0f;
        }

        [UnityTest]
        public IEnumerator The_arena_has_pickups_in_it()
        {
            yield return StartMatch();

            PowerUpPickup[] pickups =
                Object.FindObjectsByType<PowerUpPickup>(FindObjectsSortMode.None);

            Assert.IsNotEmpty(pickups, "no power-ups were placed in the arena");

            foreach (PowerUpPickup pickup in pickups)
            {
                Assert.AreNotEqual(PowerUpKind.None, pickup.Kind, $"{pickup.name} holds nothing");

                // One on a roof is one nobody can ever reach.
                Assert.Less(pickup.transform.position.y, 6f, $"{pickup.name} is out of reach");
            }

            // Each is placed by sampling the NavMesh near an ideal point, and
            // the search radius is wide enough that two ideal points inside
            // buildings could snap to the same patch of street. Clustered
            // pickups would hand whoever found them the whole set at once.
            for (int i = 0; i < pickups.Length; i++)
            {
                for (int j = i + 1; j < pickups.Length; j++)
                {
                    float apart = Vector3.Distance(
                        pickups[i].transform.position, pickups[j].transform.position);

                    Assert.Greater(
                        apart,
                        8f,
                        $"{pickups[i].name} and {pickups[j].name} are {apart:0.0}m apart");
                }
            }
        }

        [UnityTest]
        public IEnumerator Every_character_can_carry_and_use_one()
        {
            yield return StartMatch();

            foreach (NetworkIdentity identity in NetworkServer.spawned.Values)
            {
                if (identity == null || identity.GetComponent<PlayerRole>() == null)
                {
                    continue;
                }

                Assert.IsNotNull(
                    identity.GetComponent<PowerUpHolder>(),
                    $"{identity.name} cannot carry a power-up");

                Assert.IsNotNull(
                    identity.GetComponent<PowerUpEffects>(),
                    $"{identity.name} has nowhere for an effect to land");
            }
        }

        [UnityTest]
        public IEnumerator Walking_into_a_pickup_takes_it()
        {
            yield return StartMatch();

            GameObject runner = Runner();
            PowerUpHolder holder = runner.GetComponent<PowerUpHolder>();
            Assert.AreEqual(0, holder.Count, "should start with empty pockets");

            PowerUpPickup pickup = Object
                .FindObjectsByType<PowerUpPickup>(FindObjectsSortMode.None)
                .First(one => one.Available);

            // Warp rather than assigning the position: a NavMeshAgent owns the
            // transform and would drag it straight back.
            NavMeshAgent agent = runner.GetComponent<NavMeshAgent>();

            if (agent != null)
            {
                agent.Warp(pickup.transform.position);
            }
            else
            {
                runner.transform.position = pickup.transform.position;
            }

            // The pickup looks for takers on a tick, not every frame, so this
            // has to outlast one check interval.
            yield return new WaitForSeconds(0.4f);

            // Not exactly one: the bot keeps moving, and may well walk over a
            // second on the way. What this is testing is that the one it was
            // put on was taken.
            Assert.IsFalse(pickup.Available, "the pickup should be gone until it respawns");
            Assert.GreaterOrEqual(holder.Count, 1, "walking onto a power-up should pick it up");
        }

        [UnityTest]
        public IEnumerator Using_a_speed_boost_makes_the_character_faster()
        {
            yield return StartMatch();

            GameObject runner = Runner();
            PowerUpHolder holder = runner.GetComponent<PowerUpHolder>();
            PowerUpEffects effects = runner.GetComponent<PowerUpEffects>();

            float before = AgentSpeed(runner);

            Assert.IsTrue(holder.Add(PowerUpKind.SpeedBoost));
            Assert.IsTrue(holder.Use(0), "a free runner holding one should be able to use it");

            yield return null;

            Assert.IsTrue(effects.SpeedBoosted, "the boost should be running");
            Assert.AreEqual(0, holder.Count, "using one should spend it");
            Assert.Greater(effects.SpeedBoostRemaining, 0f, "the HUD needs something to count down");
            Assert.Greater(AgentSpeed(runner), before, "the effect should have reached the agent");
        }

        [UnityTest]
        public IEnumerator A_frozen_runner_cannot_use_what_it_is_carrying()
        {
            yield return StartMatch();

            GameObject runner = Runner();
            PowerUpHolder holder = runner.GetComponent<PowerUpHolder>();

            holder.Add(PowerUpKind.SpeedBoost);
            runner.GetComponent<Freezable>().Freeze();

            Assert.IsFalse(holder.Use(0), "being frozen should stop a power-up going off");
            Assert.AreEqual(1, holder.Count, "and it should still be carrying it");
        }

        [UnityTest]
        public IEnumerator Pockets_do_not_hold_more_than_the_limit()
        {
            yield return StartMatch();

            PowerUpHolder holder = Runner().GetComponent<PowerUpHolder>();

            for (int i = 0; i < PowerUpRules.MaxCarried; i++)
            {
                Assert.IsTrue(holder.Add(PowerUpKind.SpeedBoost), $"slot {i} should have been free");
            }

            Assert.IsFalse(
                holder.Add(PowerUpKind.SpeedBoost),
                "a full holder should refuse, so the pickup is not spent for nothing");
        }

        [UnityTest]
        public IEnumerator A_boost_used_twice_does_not_stack_into_double_speed()
        {
            yield return StartMatch();

            GameObject runner = Runner();
            PowerUpHolder holder = runner.GetComponent<PowerUpHolder>();

            float before = AgentSpeed(runner);

            holder.Add(PowerUpKind.SpeedBoost);
            holder.Add(PowerUpKind.SpeedBoost);

            holder.Use(0);
            yield return null;

            float once = AgentSpeed(runner);

            holder.Use(0);
            yield return null;

            Assert.AreEqual(
                once,
                AgentSpeed(runner),
                0.001f,
                "a second boost should refresh the first, not compound with it");

            Assert.Greater(once, before, "the first one should still have done something");
        }

        [UnityTest]
        public IEnumerator A_new_round_takes_back_what_everyone_was_holding()
        {
            yield return StartMatch();

            GameObject runner = Runner();
            PowerUpHolder holder = runner.GetComponent<PowerUpHolder>();
            PowerUpEffects effects = runner.GetComponent<PowerUpEffects>();

            float unboosted = AgentSpeed(runner);

            holder.Add(PowerUpKind.SpeedBoost);
            holder.Add(PowerUpKind.SpeedBoost);
            holder.Use(0);
            yield return null;

            Assert.IsTrue(effects.SpeedBoosted);
            Assert.Greater(AgentSpeed(runner), unboosted);

            MatchState match = Object.FindFirstObjectByType<MatchState>();
            Assert.IsNotNull(match, "no MatchState in the running match");
            match.RestartNow();

            yield return null;

            Assert.AreEqual(0, holder.Count, "a new round should start with empty pockets");
            Assert.IsFalse(effects.SpeedBoosted, "and with nothing still running from the last one");
            Assert.AreEqual(
                unboosted,
                AgentSpeed(runner),
                0.001f,
                "the agent should be back to its own speed, not left running fast");
        }

        [UnityTest]
        public IEnumerator Going_invisible_hides_a_runner_from_the_catcher()
        {
            yield return StartMatch();

            GameObject runner = Runner();
            PowerUpHolder holder = runner.GetComponent<PowerUpHolder>();
            PowerUpEffects effects = runner.GetComponent<PowerUpEffects>();

            Assert.IsFalse(
                PowerUpEffects.IsHidden(runner.GetComponent<PlayerRole>()),
                "nobody starts hidden");

            holder.Add(PowerUpKind.Invisibility);
            Assert.IsTrue(holder.Use(0));

            yield return null;

            Assert.IsTrue(effects.Invisible, "the runner should be hidden");
            Assert.Greater(effects.InvisibilityRemaining, 0f);

            // The rule the catcher's global view goes through.
            Assert.IsFalse(
                MapKnowledge.KnowsPosition(
                    Role.Catcher,
                    Role.Runner,
                    targetSeen: true,
                    targetHidden: PowerUpEffects.IsHidden(runner.GetComponent<PlayerRole>())),
                "a hidden runner should be lost to the catcher");
        }

        [UnityTest]
        public IEnumerator An_invisible_runner_is_not_drawn()
        {
            yield return StartMatch();

            GameObject runner = Runner();
            PowerUpHolder holder = runner.GetComponent<PowerUpHolder>();

            Renderer body = runner.GetComponentInChildren<SkinnedMeshRenderer>();
            Assert.IsNotNull(body, "the runner has nothing to hide");
            Assert.IsTrue(body.enabled, "it should start visible");

            holder.Add(PowerUpKind.Invisibility);
            holder.Use(0);

            yield return null;

            Assert.IsFalse(body.enabled, "a hidden bot should not be drawn");
        }

        [UnityTest]
        public IEnumerator Invisibility_wears_off_and_gives_the_runner_back()
        {
            yield return StartMatch();

            GameObject runner = Runner();
            PowerUpEffects effects = runner.GetComponent<PowerUpEffects>();
            Renderer body = runner.GetComponentInChildren<SkinnedMeshRenderer>();

            // A short one, so the test does not sit through the real duration.
            typeof(PowerUpEffects)
                .GetField("_invisibleSeconds", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(effects, 0.3f);

            runner.GetComponent<PowerUpHolder>().Add(PowerUpKind.Invisibility);
            runner.GetComponent<PowerUpHolder>().Use(0);

            yield return null;
            Assert.IsTrue(effects.Invisible);

            yield return new WaitForSeconds(0.8f);

            Assert.IsFalse(effects.Invisible, "it should have worn off");
            Assert.IsTrue(body.enabled, "and the runner should be visible again");
        }

    }
}
#endif
