using BarafPaani.Core;
using BarafPaani.Gameplay;
using kcp2k;
using Mirror;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BarafPaani.EditorTools
{
    /// <summary>
    /// Builds the player prefab and the playable scene from code.
    ///
    /// Both are committed assets; this exists so that how they are assembled is
    /// readable and repeatable instead of being inspector state nobody can diff
    /// or review. Running it again replaces both from scratch.
    /// </summary>
    public static class PlayableSceneBuilder
    {
        private const string PrefabPath = "Assets/_Project/Prefabs/Player.prefab";
        private const string ScenePath = "Assets/_Project/Scenes/Game.unity";

        private static readonly Vector3[] SpawnPoints =
        {
            new Vector3(0f, 0f, 0f),
            new Vector3(4f, 0f, 0f),
            new Vector3(0f, 0f, 4f),
            new Vector3(4f, 0f, 4f)
        };

        [MenuItem("Baraf-Paani/Rebuild Playable Scene")]
        public static void Rebuild()
        {
            GameObject playerPrefab = BuildPlayerPrefab();
            BuildScene(playerPrefab);

            AssetDatabase.SaveAssets();
            Debug.Log("Baraf-Paani: rebuilt the player prefab and the playable scene.");
        }

        private static GameObject BuildPlayerPrefab()
        {
            GameObject root = new GameObject("Player");

            CharacterController controller = root.AddComponent<CharacterController>();
            controller.height = 2f;
            controller.radius = 0.35f;
            controller.center = new Vector3(0f, 1f, 0f);

            // Placeholder body. Real avatars get baked to prefabs later; the old
            // build loaded them at runtime from Ready Player Me, which is exactly
            // the dependency we dropped.
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, 1f, 0f);
            Object.DestroyImmediate(body.GetComponent<CapsuleCollider>());

            GameObject cameraTarget = new GameObject("CameraTarget");
            cameraTarget.transform.SetParent(root.transform, false);
            cameraTarget.transform.localPosition = new Vector3(0f, 1.6f, 0f);

            root.AddComponent<NetworkIdentity>();

            NetworkTransformReliable sync = root.AddComponent<NetworkTransformReliable>();
            sync.target = root.transform;

            // The owning client drives its own movement and the server relays it.
            // Freeze and the other rules stay server-authoritative; see
            // docs/architecture.md on why input is a request, not an action.
            sync.syncDirection = SyncDirection.ClientToServer;

            // Mirror sets these in NetworkTransformBase.Reset(), but Reset runs
            // during AddComponent and throws before it gets there: it calls
            // GetPosition() while target is still null. The component is added
            // regardless, so we apply the defaults it never reached. 20Hz.
            sync.syncInterval = 0.05f;

            PlayerMotor motor = root.AddComponent<PlayerMotor>();
            root.AddComponent<PlayerRole>();

            Freezable freezable = root.AddComponent<Freezable>();
            SerializedObject freezeState = new SerializedObject(freezable);
            freezeState.FindProperty("_bodyRenderer").objectReferenceValue = body.GetComponent<Renderer>();
            freezeState.FindProperty("_motor").objectReferenceValue = motor;
            freezeState.ApplyModifiedPropertiesWithoutUndo();

            root.AddComponent<TagOnContact>();
            PlayerCameraRig rig = root.AddComponent<PlayerCameraRig>();

            SerializedObject serialized = new SerializedObject(rig);
            serialized.FindProperty("_followTarget").objectReferenceValue = cameraTarget.transform;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);

            return saved;
        }

        private static void BuildScene(GameObject playerPrefab)
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            ClearGenerated(scene);

            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(5f, 1f, 5f);

            BuildCamera();
            BuildNetworkManager(playerPrefab);

            for (int i = 0; i < SpawnPoints.Length; i++)
            {
                GameObject spawn = new GameObject($"SpawnPoint {i + 1}");
                spawn.transform.position = SpawnPoints[i];
                spawn.AddComponent<NetworkStartPosition>();
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        /// <summary>
        /// Removes anything a previous run created, so rebuilding does not stack
        /// duplicate cameras and managers into the scene.
        /// </summary>
        private static void ClearGenerated(Scene scene)
        {
            string[] generated = { "Ground", "PlayerFollowCamera", "NetworkManager" };

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                bool isGenerated = System.Array.IndexOf(generated, root.name) >= 0
                    || root.name.StartsWith("SpawnPoint");

                if (isGenerated)
                {
                    Object.DestroyImmediate(root);
                }
            }
        }

        private static void BuildCamera()
        {
            Camera main = Camera.main;

            if (main == null)
            {
                Debug.LogError("No camera tagged MainCamera in the scene.");
                return;
            }

            if (main.GetComponent<CinemachineBrain>() == null)
            {
                main.gameObject.AddComponent<CinemachineBrain>();
            }

            GameObject rig = new GameObject("PlayerFollowCamera");
            rig.AddComponent<CinemachineCamera>();

            CinemachineOrbitalFollow orbital = rig.AddComponent<CinemachineOrbitalFollow>();
            orbital.Radius = 5f;

            rig.AddComponent<CinemachineRotationComposer>();

            // Drives the orbit from mouse and stick look input.
            rig.AddComponent<CinemachineInputAxisController>();
        }

        private static void BuildNetworkManager(GameObject playerPrefab)
        {
            GameObject host = new GameObject("NetworkManager");

            KcpTransport transport = host.AddComponent<KcpTransport>();

            GameNetworkManager manager = host.AddComponent<GameNetworkManager>();
            manager.transport = transport;
            manager.playerPrefab = playerPrefab;
            manager.autoCreatePlayer = true;
            manager.playerSpawnMethod = PlayerSpawnMethod.RoundRobin;

            // Mirror's stock Host/Client/Server buttons. Temporary — it goes when
            // there is a real menu driving GameNetworkManager instead.
            host.AddComponent<NetworkManagerHUD>();
        }
    }
}
