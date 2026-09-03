using System.Collections;
using BarafPaani.Gameplay;
using kcp2k;
using Mirror;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BarafPaani.Tests
{
    /// <summary>
    /// Exercises the whole server-side tag path against real physics: the
    /// proximity query, the role lookup, the rules and the state change.
    /// FreezeRulesTests covers the rules alone; this covers the wiring around
    /// them, which is where the interesting failures live.
    /// </summary>
    public class FreezeContactTests
    {
        private GameObject _transport;

        [SetUp]
        public void SetUp()
        {
            _transport = new GameObject("Transport");
            Transport.active = _transport.AddComponent<KcpTransport>();

            // Stand the server up without binding a socket.
            NetworkServer.listen = false;
            NetworkServer.Listen(4);
        }

        [TearDown]
        public void TearDown()
        {
            NetworkServer.Shutdown();

            if (_transport != null)
            {
                Object.DestroyImmediate(_transport);
            }
        }

        private static GameObject MakeCharacter(string name, Role role, Vector3 position)
        {
            GameObject character = new GameObject(name);
            character.transform.position = position;

            CharacterController controller = character.AddComponent<CharacterController>();
            controller.height = 2f;
            controller.radius = 0.35f;
            controller.center = new Vector3(0f, 1f, 0f);

            character.AddComponent<NetworkIdentity>();
            PlayerRole playerRole = character.AddComponent<PlayerRole>();
            character.AddComponent<Freezable>();
            character.AddComponent<TagOnContact>();

            playerRole.SetRole(role);
            return character;
        }

        [UnityTest]
        public IEnumerator Catcher_freezes_a_runner_it_stands_next_to()
        {
            GameObject catcher = MakeCharacter("Catcher", Role.Catcher, Vector3.zero);
            GameObject runner = MakeCharacter("Runner", Role.Runner, new Vector3(0.8f, 0f, 0f));

            Physics.SyncTransforms();

            Assert.AreEqual(Role.Catcher, catcher.GetComponent<PlayerRole>().Role,
                "catcher role did not stick");

            // TagOnContact checks ten times a second.
            yield return new WaitForSeconds(0.35f);

            Assert.IsTrue(runner.GetComponent<Freezable>().IsFrozen,
                "runner standing next to the catcher was never frozen");
            Assert.IsFalse(catcher.GetComponent<Freezable>().IsFrozen,
                "the catcher froze itself");

            Object.DestroyImmediate(catcher);
            Object.DestroyImmediate(runner);
        }

        [UnityTest]
        public IEnumerator A_runner_out_of_reach_is_left_alone()
        {
            GameObject catcher = MakeCharacter("Catcher", Role.Catcher, Vector3.zero);
            GameObject runner = MakeCharacter("Runner", Role.Runner, new Vector3(6f, 0f, 0f));

            Physics.SyncTransforms();

            yield return new WaitForSeconds(0.35f);

            Assert.IsFalse(runner.GetComponent<Freezable>().IsFrozen,
                "a runner six metres away was frozen");

            Object.DestroyImmediate(catcher);
            Object.DestroyImmediate(runner);
        }

        [UnityTest]
        public IEnumerator A_runner_frees_a_frozen_teammate()
        {
            GameObject frozen = MakeCharacter("Frozen", Role.Runner, Vector3.zero);
            Assert.IsTrue(frozen.GetComponent<Freezable>().Freeze(), "could not freeze to set up");

            GameObject rescuer = MakeCharacter("Rescuer", Role.Runner, new Vector3(0.8f, 0f, 0f));

            Physics.SyncTransforms();

            yield return new WaitForSeconds(0.35f);

            Assert.IsFalse(frozen.GetComponent<Freezable>().IsFrozen,
                "a runner standing on a frozen team-mate did not free them");

            Object.DestroyImmediate(frozen);
            Object.DestroyImmediate(rescuer);
        }
    }
}
