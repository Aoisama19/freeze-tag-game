using System.Collections;
using BarafPaani.Core;
using BarafPaani.Gameplay;
using kcp2k;
using Mirror;
using NUnit.Framework;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.TestTools;

#if UNITY_EDITOR
namespace BarafPaani.Tests
{
    /// <summary>
    /// Starts a real host with the real player prefab. The rules and the contact
    /// path are covered elsewhere; what this checks is the wiring between them —
    /// that a spawned player actually comes out with a role on it.
    /// </summary>
    public class HostRoleTests
    {
        private const string PrefabPath = "Assets/_Project/Prefabs/Player.prefab";

        private GameObject _managerObject;

        [TearDown]
        public void TearDown()
        {
            if (NetworkClient.active || NetworkServer.active)
            {
                NetworkManager.singleton?.StopHost();
            }

            if (_managerObject != null)
            {
                Object.DestroyImmediate(_managerObject);
            }
        }

        [UnityTest]
        public IEnumerator The_first_player_into_a_host_is_the_catcher()
        {
            // The bare test scene has no Cinemachine camera, and PlayerCameraRig
            // rightly complains about that. Not what this test is about.
            LogAssert.ignoreFailingMessages = true;

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.IsNotNull(prefab, "player prefab missing at " + PrefabPath);
            Assert.IsNotNull(prefab.GetComponent<PlayerRole>(), "prefab has no PlayerRole");
            Assert.IsNotNull(prefab.GetComponent<TagOnContact>(), "prefab has no TagOnContact");
            Assert.IsNotNull(prefab.GetComponent<Freezable>(), "prefab has no Freezable");

            _managerObject = new GameObject("TestNetworkManager");
            KcpTransport transport = _managerObject.AddComponent<KcpTransport>();
            GameNetworkManager manager = _managerObject.AddComponent<GameNetworkManager>();
            manager.transport = transport;
            Transport.active = transport;
            manager.playerPrefab = prefab;
            manager.autoCreatePlayer = true;

            manager.StartSinglePlayer();

            yield return new WaitForSeconds(1f);

            Assert.IsTrue(NetworkServer.active, "server never came up");
            Assert.IsNotNull(NetworkClient.localPlayer, "no local player was spawned");

            PlayerRole role = NetworkClient.localPlayer.GetComponent<PlayerRole>();
            Assert.IsNotNull(role, "spawned player has no PlayerRole");

            Assert.AreEqual(Role.Catcher, role.Role,
                "the first player into the match was not made the catcher");
        }
    }
}
#endif
