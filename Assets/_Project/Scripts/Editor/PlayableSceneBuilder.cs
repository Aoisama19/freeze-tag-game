using BarafPaani.AI;
using BarafPaani.Core;
using BarafPaani.Gameplay;
using BarafPaani.UI;
using kcp2k;
using Mirror;
using Unity.AI.Navigation;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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
        private const string AiPrefabPath = "Assets/_Project/Prefabs/AiCharacter.prefab";
        private const string ScenePath = "Assets/_Project/Scenes/Game.unity";

        /// <summary>
        /// More spawn points than characters, so round-robin never wraps and
        /// puts two people on the same spot. There were four points and five
        /// characters: the AI took all four, then the human wrapped back onto
        /// point one and was frozen on arrival by the catcher already standing
        /// there. Eight leaves headroom.
        /// </summary>
        private const int SpawnPointCount = 8;

        /// <summary>
        /// Ring radius, measured from the arena centre. Kept inside the arena
        /// walls with room to spare, and puts opposite points 80 metres apart —
        /// the AI spawns before the human, so the human lands across the ring
        /// from the catcher rather than next to it.
        /// </summary>
        private const float SpawnRingRadius = 40f;

        private const string MapPrefabPath =
            "Assets/_Project/Art/Maps/3Talwaar/Prefab/3 Talwaar v4.prefab";

        private const string NavMeshAssetPath = "Assets/_Project/Scenes/GameNavMesh.asset";

        private const string MinimapTexturePath =
            "Assets/_Project/Scenes/MinimapTexture.renderTexture";

        /// <summary>Layer the map geometry sits on, so it can block sight.</summary>
        private const string MapLayerName = "MapGeometry";

        /// <summary>Resolution of the minimap's render texture.</summary>
        private const int MinimapTextureSize = 512;

        /// <summary>On-screen size of the minimap panel, in canvas units.</summary>
        private const float MinimapPanelSize = 150f;

        private const float BlipSize = 9f;

        /// <summary>
        /// Map is framed slightly wider than the arena so the circle's edge
        /// still shows streets rather than the camera's clear colour.
        /// </summary>
        private const float MinimapZoomOut = 1.15f;

        /// <summary>
        /// High enough to clear the tallest building, so the map camera looks
        /// down on roofs rather than starting inside one.
        /// </summary>
        private const float MinimapCameraHeight = 80f;

        /// <summary>
        /// The playable square, cut down from 3 Talwaar's full 537x464 metres.
        /// The original walled off roughly 251x198 with road barriers, which is a
        /// long way for one catcher to cover; this keeps the same streets but
        /// tightens the game. Widening is a matter of changing this number.
        /// </summary>
        private const float ArenaSize = 120f;

        /// <summary>Tall enough that nobody vaults the arena walls.</summary>
        private const float ArenaHeight = 40f;

        /// <summary>
        /// How far up the NavMesh volume reaches. Deliberately short: 3 Talwaar's
        /// buildings have flat roofs, and a volume as tall as the arena walls
        /// bakes navigable surface onto every one of them. That put two spawn
        /// points on rooftops, 14 and 30 metres up, and would have let the agents
        /// path across the skyline. Street level only.
        /// </summary>
        private const float NavMeshVolumeHeight = 8f;

        /// <summary>
        /// Ground sits near y=0, so anything sampled much above this is a roof or
        /// a ledge rather than a street.
        /// </summary>
        private const float MaxSpawnHeight = 3f;

        /// <summary>
        /// Centre of the playable square, in the map's own coordinates. Sits on
        /// the middle of the area the original barriers enclosed.
        /// </summary>
        private static readonly Vector3 ArenaCentre = new Vector3(11f, 0f, -9f);

        [MenuItem("Baraf-Paani/Rebuild Playable Scene")]
        public static void Rebuild()
        {
            GameObject playerPrefab = BuildPlayerPrefab();
            GameObject aiPrefab = BuildAiPrefab();
            BuildScene(playerPrefab, aiPrefab);

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

            // Sight on a player is used server-side only, to decide whether this
            // runner has earned a catcher blip on their minimap.
            Vision vision = AddVision(root, cameraTarget.transform);

            MapAwareness awareness = root.AddComponent<MapAwareness>();

            // Owner, not Observers: the answer goes to this player's connection
            // and nobody else's. A runner's client is not told where the catcher
            // is until the server decides it has earned it.
            awareness.syncMode = SyncMode.Owner;

            SerializedObject awarenessState = new SerializedObject(awareness);
            awarenessState.FindProperty("_vision").objectReferenceValue = vision;
            awarenessState.ApplyModifiedPropertiesWithoutUndo();

            PlayerCameraRig rig = root.AddComponent<PlayerCameraRig>();

            SerializedObject serialized = new SerializedObject(rig);
            serialized.FindProperty("_followTarget").objectReferenceValue = cameraTarget.transform;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);

            return saved;
        }

        /// <summary>
        /// The AI character. Separate from the player prefab because a
        /// NavMeshAgent drives the transform itself and would fight a
        /// CharacterController, and because the server owns AI movement while a
        /// player owns their own — opposite sync directions.
        ///
        /// The rules are the same components either way: Freezable, PlayerRole
        /// and TagOnContact are shared, so an AI freezes and is freed by exactly
        /// the code that handles humans.
        /// </summary>
        private static GameObject BuildAiPrefab()
        {
            GameObject root = new GameObject("AiCharacter");

            // A plain collider rather than a CharacterController: the agent does
            // the moving, but other characters' proximity checks still need to be
            // able to find this one.
            CapsuleCollider capsule = root.AddComponent<CapsuleCollider>();
            capsule.height = 2f;
            capsule.radius = 0.35f;
            capsule.center = new Vector3(0f, 1f, 0f);

            NavMeshAgent agent = root.AddComponent<NavMeshAgent>();
            agent.radius = 0.35f;
            agent.height = 2f;
            agent.speed = 4.2f;
            agent.angularSpeed = 480f;
            agent.acceleration = 20f;
            agent.stoppingDistance = 0.6f;

            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, 1f, 0f);
            Object.DestroyImmediate(body.GetComponent<CapsuleCollider>());

            GameObject eye = new GameObject("Eye");
            eye.transform.SetParent(root.transform, false);
            eye.transform.localPosition = new Vector3(0f, 1.6f, 0f);

            root.AddComponent<NetworkIdentity>();

            NetworkTransformReliable sync = root.AddComponent<NetworkTransformReliable>();
            sync.target = root.transform;

            // Opposite of the player prefab: the server decides where AI go.
            sync.syncDirection = SyncDirection.ServerToClient;
            sync.syncInterval = 0.05f;

            root.AddComponent<PlayerRole>();

            Freezable freezable = root.AddComponent<Freezable>();
            SerializedObject freezeState = new SerializedObject(freezable);
            freezeState.FindProperty("_bodyRenderer").objectReferenceValue = body.GetComponent<Renderer>();
            freezeState.ApplyModifiedPropertiesWithoutUndo();

            root.AddComponent<TagOnContact>();

            Vision vision = AddVision(root, eye.transform);

            AiBrain brain = root.AddComponent<AiBrain>();
            SerializedObject brainState = new SerializedObject(brain);
            brainState.FindProperty("_vision").objectReferenceValue = vision;
            brainState.ApplyModifiedPropertiesWithoutUndo();

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, AiPrefabPath);
            Object.DestroyImmediate(root);

            return saved;
        }

        private static void BuildScene(GameObject playerPrefab, GameObject aiPrefab)
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            ClearGenerated(scene);

            GameObject map = BuildMap();
            BuildArenaWalls();

            // The AI walks on this, so it needs a NavMesh surface. Only the
            // component is set up here — GameNetworkManager bakes it when the
            // server starts, because a bake done at build time is runtime-only
            // data that does not survive saving the scene.
            //
            // Bounded to the arena rather than the whole map: 3 Talwaar is
            // 537x464 metres, most of it outlying scenery, and without a volume
            // the agents would happily path off into it.
            NavMeshSurface surface = map.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Volume;
            surface.center = new Vector3(
                ArenaCentre.x, (NavMeshVolumeHeight * 0.5f) - 2f, ArenaCentre.z);
            surface.size = new Vector3(ArenaSize, NavMeshVolumeHeight, ArenaSize);

            BuildCamera();
            BuildNetworkManager(playerPrefab, aiPrefab);
            BuildHud();
            BuildSpawnPoints(surface);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        /// <summary>
        /// Places the map and gives it collision.
        ///
        /// The imported prefab carries no colliders at all — the FBX was set to
        /// addColliders 0, and turning that on does not retro-fit the prefab,
        /// which stores its own component list. Without this, characters fall
        /// straight through the city and the NavMesh has nothing to bake onto.
        /// </summary>
        private static GameObject BuildMap()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MapPrefabPath);

            if (prefab == null)
            {
                Debug.LogError($"No map prefab at {MapPrefabPath}.");
                return new GameObject("Map");
            }

            GameObject map = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            map.name = "Map";
            map.transform.position = Vector3.zero;

            int added = 0;

            foreach (MeshFilter filter in map.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh == null || filter.GetComponent<Collider>() != null)
                {
                    continue;
                }

                filter.gameObject.AddComponent<MeshCollider>();
                added++;
            }

            int mapLayer = EnsureMapLayer();

            foreach (Transform piece in map.GetComponentsInChildren<Transform>(true))
            {
                piece.gameObject.layer = mapLayer;
            }

            Debug.Log($"Map: added {added} mesh colliders, on layer '{MapLayerName}'.");
            return map;
        }

        /// <summary>
        /// Four invisible walls around the arena. The map has no edge of its own
        /// where we have cut it down, so without these a runner can simply leave
        /// the game and stand in the scenery.
        /// </summary>
        private static void BuildArenaWalls()
        {
            GameObject walls = new GameObject("ArenaWalls");

            float half = ArenaSize * 0.5f;
            const float thickness = 2f;

            (string name, Vector3 offset, Vector3 size)[] sides =
            {
                ("North", new Vector3(0f, 0f, half), new Vector3(ArenaSize, ArenaHeight, thickness)),
                ("South", new Vector3(0f, 0f, -half), new Vector3(ArenaSize, ArenaHeight, thickness)),
                ("East", new Vector3(half, 0f, 0f), new Vector3(thickness, ArenaHeight, ArenaSize)),
                ("West", new Vector3(-half, 0f, 0f), new Vector3(thickness, ArenaHeight, ArenaSize))
            };

            foreach ((string name, Vector3 offset, Vector3 size) in sides)
            {
                GameObject wall = new GameObject($"Wall {name}");
                wall.transform.SetParent(walls.transform, false);
                wall.transform.position =
                    ArenaCentre + offset + new Vector3(0f, ArenaHeight * 0.5f, 0f);

                BoxCollider box = wall.AddComponent<BoxCollider>();
                box.size = size;
            }
        }

        /// <summary>
        /// Puts spawn points on a ring, then drops each one onto the NavMesh so
        /// it lands on ground a character can actually stand on. A ring position
        /// picked blind could easily sit inside a building or off a kerb.
        /// </summary>
        private static void BuildSpawnPoints(NavMeshSurface surface)
        {
            // Bake here, at edit time, and keep the result as its own asset.
            //
            // Baking at runtime instead looked tidier but does not survive a
            // build: RuntimeNavMeshBuilder needs Read/Write enabled on every
            // source mesh, and 3 Talwaar's are not readable. Unity says as much
            // — "will work in playmode in the editor but not in player" — so the
            // shipped game would have had no navigation and the AI would not
            // have moved at all.
            //
            // The data has to live in its own asset file rather than inside the
            // scene. Left embedded, that binary blob turns the whole scene file
            // binary, which kills diffs and the SmartMerge setup.
            surface.BuildNavMesh();

            int placed = 0;

            for (int i = 0; i < SpawnPointCount; i++)
            {
                float angle = i * Mathf.PI * 2f / SpawnPointCount;
                Vector3 ideal = ArenaCentre + new Vector3(
                    Mathf.Sin(angle) * SpawnRingRadius, 0f, Mathf.Cos(angle) * SpawnRingRadius);

                if (!NavMesh.SamplePosition(ideal, out NavMeshHit hit, SpawnRingRadius, NavMesh.AllAreas))
                {
                    Debug.LogWarning($"SpawnPoint {i + 1}: no navigable ground near {ideal}.");
                    continue;
                }

                // Belt and braces alongside the short NavMesh volume: never put
                // anyone on a roof, however the bake turns out.
                if (hit.position.y > MaxSpawnHeight)
                {
                    Debug.LogWarning(
                        $"SpawnPoint {i + 1}: nearest ground was {hit.position.y:F1}m up, so it was skipped.");
                    continue;
                }

                GameObject spawn = new GameObject($"SpawnPoint {i + 1}");
                spawn.transform.position = hit.position + new Vector3(0f, 0.1f, 0f);

                // Face the middle, so whoever spawns here is looking at the game
                // rather than out at empty ground.
                Vector3 inward = ArenaCentre - hit.position;
                inward.y = 0f;

                if (inward.sqrMagnitude > 0.001f)
                {
                    spawn.transform.rotation = Quaternion.LookRotation(inward.normalized);
                }

                spawn.AddComponent<NetworkStartPosition>();
                placed++;
            }

            SaveNavMesh(surface);

            Debug.Log($"Spawn points placed on the NavMesh: {placed} of {SpawnPointCount}.");
        }


        /// <summary>
        /// Builds the on-screen minimap: a top-down camera rendering the arena
        /// to a texture, with blips drawn over it.
        ///
        /// A live camera rather than a baked image, so the map cannot go stale
        /// against the geometry and costs nothing to keep in step when the arena
        /// changes. It renders the map layer only — characters are blips, and
        /// drawing them twice would just be noise.
        /// </summary>
        private static void BuildHud()
        {
            int mapLayer = EnsureMapLayer();

            Sprite circle = MinimapSprites.EnsureCircle();
            Sprite ring = MinimapSprites.EnsureRing();

            RenderTexture texture = BuildMinimapTexture();
            BuildMinimapCamera(texture, mapLayer);

            GameObject hud = new GameObject("HUD");
            Canvas canvas = hud.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = hud.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            hud.AddComponent<GraphicRaycaster>();

            GameObject panel = new GameObject("Minimap");
            panel.transform.SetParent(hud.transform, false);

            RectTransform panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(1f, 1f);
            panelRect.anchorMax = new Vector2(1f, 1f);
            panelRect.pivot = new Vector2(1f, 1f);
            panelRect.anchoredPosition = new Vector2(-24f, -24f);
            panelRect.sizeDelta = new Vector2(MinimapPanelSize, MinimapPanelSize);

            // The mask is what makes the map round. Its own graphic is hidden —
            // it exists to define the shape, not to be seen — and everything
            // under it, map and blips alike, is clipped to the disc.
            GameObject maskObject = new GameObject("Mask");
            maskObject.transform.SetParent(panel.transform, false);

            RectTransform maskRect = maskObject.AddComponent<RectTransform>();
            Stretch(maskRect);

            Image maskImage = maskObject.AddComponent<Image>();
            maskImage.sprite = circle;

            Mask mask = maskObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            GameObject mapObject = new GameObject("Map");
            mapObject.transform.SetParent(maskObject.transform, false);

            RectTransform mapRect = mapObject.AddComponent<RectTransform>();
            Stretch(mapRect);

            RawImage background = mapObject.AddComponent<RawImage>();
            background.texture = texture;

            GameObject blipArea = new GameObject("Blips");
            blipArea.transform.SetParent(maskObject.transform, false);

            RectTransform blipRect = blipArea.AddComponent<RectTransform>();
            Stretch(blipRect);

            GameObject blipPrefab = new GameObject("Blip");
            blipPrefab.transform.SetParent(blipArea.transform, false);

            RectTransform blipPrefabRect = blipPrefab.AddComponent<RectTransform>();
            blipPrefabRect.sizeDelta = new Vector2(BlipSize, BlipSize);

            Image blipImage = blipPrefab.AddComponent<Image>();
            blipImage.sprite = circle;
            blipImage.enabled = false;

            // Frame sits outside the mask, so the ring is not clipped by the
            // very shape it is drawing the edge of.
            GameObject frame = new GameObject("Frame");
            frame.transform.SetParent(panel.transform, false);

            RectTransform frameRect = frame.AddComponent<RectTransform>();
            Stretch(frameRect);

            Image frameImage = frame.AddComponent<Image>();
            frameImage.sprite = ring;
            frameImage.color = new Color(0.85f, 0.85f, 0.88f, 0.9f);
            frameImage.raycastTarget = false;

            MinimapView view = hud.AddComponent<MinimapView>();

            SerializedObject viewState = new SerializedObject(view);
            viewState.FindProperty("_blipArea").objectReferenceValue = blipRect;
            viewState.FindProperty("_blipPrefab").objectReferenceValue = blipImage;
            viewState.FindProperty("_arenaCentre").vector3Value = ArenaCentre;
            viewState.FindProperty("_mapExtent").floatValue = ArenaSize * 0.5f * MinimapZoomOut;
            viewState.ApplyModifiedPropertiesWithoutUndo();

            Debug.Log("HUD: circular minimap built.");
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// Saved as an asset, not just newed up: a RenderTexture created in code
        /// does not serialise into a scene, so the camera and the image would
        /// both come back pointing at nothing and the map would render black.
        /// </summary>
        private static RenderTexture BuildMinimapTexture()
        {
            RenderTexture texture = new RenderTexture(MinimapTextureSize, MinimapTextureSize, 16)
            {
                name = "MinimapTexture"
            };

            AssetDatabase.DeleteAsset(MinimapTexturePath);
            AssetDatabase.CreateAsset(texture, MinimapTexturePath);
            AssetDatabase.SaveAssets();

            return AssetDatabase.LoadAssetAtPath<RenderTexture>(MinimapTexturePath);
        }

        private static void BuildMinimapCamera(RenderTexture texture, int mapLayer)
        {
            GameObject cameraObject = new GameObject("MinimapCamera");
            cameraObject.transform.position =
                ArenaCentre + new Vector3(0f, MinimapCameraHeight, 0f);
            cameraObject.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            Camera mapCamera = cameraObject.AddComponent<Camera>();
            mapCamera.orthographic = true;

            // A shade wider than the arena, so the disc's edges are map rather
            // than empty background once the corners are masked away.
            mapCamera.orthographicSize = ArenaSize * 0.5f * MinimapZoomOut;

            mapCamera.cullingMask = 1 << mapLayer;
            mapCamera.clearFlags = CameraClearFlags.SolidColor;
            mapCamera.backgroundColor = new Color(0.10f, 0.11f, 0.13f);
            mapCamera.targetTexture = texture;
            mapCamera.depth = -10;
        }

        /// <summary>
        /// Adds sight, with the map set as the thing that blocks it.
        ///
        /// The blocking mask was empty until now, which meant line of sight was
        /// never actually blocked by anything — on a flat plane that made no
        /// difference, but on a city it would have let runners see the catcher
        /// straight through buildings and left the minimap blip permanently lit.
        /// </summary>
        private static Vision AddVision(GameObject root, Transform eye)
        {
            Vision vision = root.AddComponent<Vision>();

            SerializedObject state = new SerializedObject(vision);
            state.FindProperty("_eye").objectReferenceValue = eye;
            state.FindProperty("_blockingMask").intValue = 1 << EnsureMapLayer();
            state.ApplyModifiedPropertiesWithoutUndo();

            return vision;
        }

        /// <summary>
        /// Finds the layer the map geometry lives on, adding it to the project
        /// if it is not there yet. Sight needs the map on a layer of its own so
        /// buildings can block it without characters blocking each other.
        /// </summary>
        private static int EnsureMapLayer()
        {
            int existing = LayerMask.NameToLayer(MapLayerName);

            if (existing >= 0)
            {
                return existing;
            }

            Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");

            if (assets.Length == 0)
            {
                Debug.LogError("Could not open TagManager to add the map layer.");
                return 0;
            }

            SerializedObject tagManager = new SerializedObject(assets[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");

            // 0-7 are Unity's own and cannot be renamed.
            for (int i = 8; i < layers.arraySize; i++)
            {
                SerializedProperty layer = layers.GetArrayElementAtIndex(i);

                if (!string.IsNullOrEmpty(layer.stringValue))
                {
                    continue;
                }

                layer.stringValue = MapLayerName;
                tagManager.ApplyModifiedProperties();
                Debug.Log($"Added layer '{MapLayerName}' in slot {i}.");
                return i;
            }

            Debug.LogError($"No free layer slot for '{MapLayerName}'.");
            return 0;
        }

        /// <summary>
        /// Writes the baked navigation out as its own asset and points the
        /// surface at it, so the scene stores a reference rather than the data.
        /// </summary>
        private static void SaveNavMesh(NavMeshSurface surface)
        {
            NavMeshData data = surface.navMeshData;

            if (data == null)
            {
                Debug.LogError("NavMesh bake produced nothing, so the AI will have nowhere to walk.");
                return;
            }

            AssetDatabase.DeleteAsset(NavMeshAssetPath);
            AssetDatabase.CreateAsset(data, NavMeshAssetPath);
            AssetDatabase.SaveAssets();

            surface.navMeshData = AssetDatabase.LoadAssetAtPath<NavMeshData>(NavMeshAssetPath);

            Debug.Log($"NavMesh baked to {NavMeshAssetPath}.");
        }

        /// <summary>
        /// Removes anything a previous run created, so rebuilding does not stack
        /// duplicate cameras and managers into the scene.
        /// </summary>
        private static void ClearGenerated(Scene scene)
        {
            string[] generated =
            {
                "Ground", "Map", "ArenaWalls", "PlayerFollowCamera", "NetworkManager",
                "HUD", "MinimapCamera"
            };

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

        private static void BuildNetworkManager(GameObject playerPrefab, GameObject aiPrefab)
        {
            GameObject host = new GameObject("NetworkManager");

            KcpTransport transport = host.AddComponent<KcpTransport>();

            GameNetworkManager manager = host.AddComponent<GameNetworkManager>();
            manager.transport = transport;
            manager.playerPrefab = playerPrefab;
            manager.autoCreatePlayer = true;
            manager.playerSpawnMethod = PlayerSpawnMethod.RoundRobin;

            // Clients need the AI prefab registered or they cannot spawn what the
            // server tells them about.
            manager.spawnPrefabs.Clear();
            manager.spawnPrefabs.Add(aiPrefab);

            SerializedObject managerState = new SerializedObject(manager);
            managerState.FindProperty("_aiCharacterPrefab").objectReferenceValue = aiPrefab;
            managerState.ApplyModifiedPropertiesWithoutUndo();

            // Mirror's stock Host/Client/Server buttons. Temporary — it goes when
            // there is a real menu driving GameNetworkManager instead.
            host.AddComponent<NetworkManagerHUD>();
        }
    }
}
