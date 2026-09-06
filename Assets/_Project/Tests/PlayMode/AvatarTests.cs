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
    /// The characters people actually see: that they are the animated model
    /// rather than the capsule that stood in for it, that the animation is
    /// driven, and that freezing stops them where they are.
    /// </summary>
    public class AvatarTests
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

        private static GameObject[] Characters()
        {
            return NetworkServer.spawned.Values
                .Where(identity => identity != null && identity.GetComponent<PlayerRole>() != null)
                .Select(identity => identity.gameObject)
                .ToArray();
        }

        [UnityTest]
        public IEnumerator Every_character_wears_the_animated_model()
        {
            yield return StartMatch();

            GameObject[] characters = Characters();
            Assert.IsNotEmpty(characters, "no characters in the match");

            foreach (GameObject character in characters)
            {
                Animator animator = character.GetComponentInChildren<Animator>();

                Assert.IsNotNull(animator, $"{character.name} has no Animator");
                Assert.IsNotNull(
                    animator.runtimeAnimatorController,
                    $"{character.name} has an Animator with nothing to play");

                // Root motion would move the character a second time, on top of
                // the motor or the agent, and drift it off its own collider.
                Assert.IsFalse(
                    animator.applyRootMotion,
                    $"{character.name} would be moved by its animation as well as by its motor");

                Assert.IsNotNull(
                    character.GetComponentInChildren<SkinnedMeshRenderer>(),
                    $"{character.name} is not a skinned model");
            }
        }

        [UnityTest]
        public IEnumerator Walking_puts_the_legs_in_motion()
        {
            yield return StartMatch();

            GameObject runner = Characters().First(
                character => character.GetComponent<PlayerRole>().Role == Role.Runner);

            Animator animator = runner.GetComponentInChildren<Animator>();

            // Move it by hand rather than waiting on the AI to decide to run,
            // which keeps this about the animation and not about the brain.
            for (int step = 0; step < 20; step++)
            {
                runner.transform.position += runner.transform.forward * 0.08f;
                yield return null;
            }

            Assert.Greater(
                animator.GetFloat("Speed"),
                0.5f,
                "a character covering ground should be reported as moving");
        }

        [UnityTest]
        public IEnumerator A_teleport_home_does_not_read_as_a_sprint()
        {
            yield return StartMatch();

            GameObject runner = Characters().First(
                character => character.GetComponent<PlayerRole>().Role == Role.Runner);

            Animator animator = runner.GetComponentInChildren<Animator>();

            // Whatever it was doing when the round ended. A runner mid-sprint is
            // the interesting case, so this deliberately does not wait for it to
            // stand still first.
            float before = animator.GetFloat("Speed");

            // What the start of a round does to everyone: the far side of the
            // arena, in a single frame. Read literally that is thousands of
            // metres a second.
            runner.transform.position += new Vector3(60f, 0f, 60f);
            yield return null;
            yield return null;

            Assert.LessOrEqual(
                animator.GetFloat("Speed"),
                before + 0.5f,
                "a teleport should never speed a character up");

            Assert.Less(
                animator.GetFloat("Speed"),
                12f,
                "nothing on foot moves that fast, so this was the teleport being animated");
        }

        [UnityTest]
        public IEnumerator Freezing_stops_the_character_where_it_stands()
        {
            yield return StartMatch();

            GameObject runner = Characters().First(
                character => character.GetComponent<PlayerRole>().Role == Role.Runner);

            Animator animator = runner.GetComponentInChildren<Animator>();
            Freezable freezable = runner.GetComponent<Freezable>();

            Assert.AreEqual(1f, animator.speed, "a free runner should be animating");

            freezable.Freeze();
            yield return null;

            Assert.AreEqual(
                0f, animator.speed, "a frozen runner should be held mid-stride, not left running");

            freezable.Unfreeze();
            yield return null;

            Assert.AreEqual(1f, animator.speed, "thawing should start the animation again");
        }

        [UnityTest]
        public IEnumerator The_catcher_is_a_different_colour_from_the_runners()
        {
            yield return StartMatch();

            GameObject[] characters = Characters();

            GameObject catcher = characters.First(
                character => character.GetComponent<PlayerRole>().Role == Role.Catcher);

            GameObject runner = characters.First(
                character => character.GetComponent<PlayerRole>().Role == Role.Runner);

            Assert.AreNotEqual(
                Tint(catcher),
                Tint(runner),
                "the two sides need to be told apart at a glance");
        }

        [UnityTest]
        public IEnumerator A_frozen_runner_is_a_different_colour_again()
        {
            yield return StartMatch();

            GameObject runner = Characters().First(
                character => character.GetComponent<PlayerRole>().Role == Role.Runner);

            Color free = Tint(runner);

            runner.GetComponent<Freezable>().Freeze();
            yield return null;

            Assert.AreNotEqual(free, Tint(runner), "a frozen runner should look frozen");
        }

        private static Color Tint(GameObject character)
        {
            Renderer renderer = character.GetComponentInChildren<SkinnedMeshRenderer>();
            Assert.IsNotNull(renderer, $"{character.name} has nothing to colour");

            MaterialPropertyBlock block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);

            return block.GetColor("_BaseColor");
        }
    }
}
#endif
