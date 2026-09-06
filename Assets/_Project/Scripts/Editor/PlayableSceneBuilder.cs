using System.IO;
using BarafPaani.AI;
using BarafPaani.Audio;
using BarafPaani.Core;
using BarafPaani.Gameplay;
using BarafPaani.Gameplay.PowerUps;
using BarafPaani.UI;
using kcp2k;
using Mirror;
using Unity.AI.Navigation;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
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

        private const string DecoyPrefabPath = "Assets/_Project/Prefabs/Decoy.prefab";
        /// <summary>
        /// More spawn points than characters, so round-robin never wraps and
        /// puts two people on the same spot. There were four points and five
        /// characters: the AI took all four, then the human wrapped back onto
        /// point one and was frozen on arrival by the catcher already standing
        /// there. Eight leaves headroom.
        /// </summary>
        private const int SpawnPointCount = 8;


        private const string MenuFontPath = "Assets/_Project/Art/UI/Fonts/Orbitron.ttf";

        /// <summary>
        /// The character everyone wears. One model for both sides, told apart by
        /// colour rather than by shape, so the two are always the same size and
        /// nobody can read an advantage off the silhouette.
        /// </summary>
        private const string CharacterModelPath =
            "Assets/_Project/Art/Characters/YBot/Y Bot.fbx";

        private const string MatchSetupPath = "Assets/_Project/Settings/MatchSetup.asset";

        private const string SoundBankPath = "Assets/_Project/Settings/SoundBank.asset";

        private const string MinimapTexturePath =
            "Assets/_Project/Scenes/MinimapTexture.renderTexture";

        /// <summary>Layer the map geometry sits on, so it can block sight.</summary>
        private const string MapLayerName = "MapGeometry";

        /// <summary>Resolution of the minimap's render texture.</summary>
        private const int MinimapTextureSize = 512;

        /// <summary>On-screen size of the minimap panel, in canvas units.</summary>
        private const float MinimapPanelSize = 300f;

        private const float BlipSize = 20f;


        /// <summary>
        /// Power-up pickups, and where they sit. Deliberately a shorter ring
        /// than the spawn points: they are worth walking towards, and a runner
        /// heading inward for one is a runner heading towards the catcher.
        /// </summary>
        private static readonly PowerUpKind[] PickupRing =
        {
            PowerUpKind.SpeedBoost,
            PowerUpKind.Invisibility,
            PowerUpKind.Clone,
            PowerUpKind.SpeedBoost,
            PowerUpKind.Invisibility,
            PowerUpKind.Clone,
        };


        /// <summary>Waist height, so they are visible over a kerb but still walked into.</summary>
        private const float PickupHeight = 1f;


        /// <summary>
        /// Everything that differs between one map and the next.
        ///
        /// The builder was written around a single map and had its measurements
        /// as constants. Three more maps meant either four copies of the builder
        /// or one table, and a table is the only version where fixing something
        /// fixes it everywhere.
        /// </summary>
        private sealed class MapDefinition
        {
            /// <summary>Used for the scene name, the NavMesh asset and the menu.</summary>
            public string Name;

            public string PrefabPath;

            /// <summary>Centre of the playable square, in the map's own coordinates.</summary>
            public Vector3 ArenaCentre;

            /// <summary>
            /// The playable square, cut down from the model's full extent. These
            /// models are landmarks with a lot of outlying scenery; the arena is
            /// the part worth playing in, and widening one is a matter of
            /// changing this number.
            /// </summary>
            public float ArenaSize = 120f;

            /// <summary>
            /// Ring the spawn points sit on, measured from the arena centre.
            /// Kept well inside the walls so opposite points are far apart — the
            /// AI spawns before the human, so the human lands across the ring
            /// from the catcher rather than next to it.
            /// </summary>
            public float SpawnRingRadius = 40f;

            /// <summary>
            /// Ring the power-ups sit on. Deliberately shorter than the spawn
            /// ring: they are worth walking towards, and a runner heading inward
            /// for one is a runner heading towards the catcher.
            /// </summary>
            public float PickupRingRadius = 24f;

            /// <summary>
            /// How far up the NavMesh volume reaches. Deliberately short: these
            /// buildings have flat roofs, and a volume as tall as the arena walls
            /// bakes navigable surface onto every one of them. That put two spawn
            /// points on rooftops, 14 and 30 metres up, and would have let the
            /// agents path across the skyline. Street level only.
            /// </summary>
            public float NavMeshVolumeHeight = 8f;

            /// <summary>
            /// Ground sits near y=0, so anything sampled much above this is a
            /// roof or a ledge rather than a street.
            /// </summary>
            public float MaxSpawnHeight = 3f;

            /// <summary>
            /// Half-width of what the minimap shows, in metres. The map follows
            /// the player rather than framing the whole arena, so this is a
            /// neighbourhood — close enough to read streets, wide enough to see
            /// someone coming.
            /// </summary>
            public float MinimapViewExtent = 35f;

            /// <summary>
            /// High enough to clear the tallest building, so the map camera looks
            /// down on roofs rather than starting inside one.
            /// </summary>
            public float MinimapCameraHeight = 80f;

            public string ScenePath => $"Assets/_Project/Scenes/Game_{Name}.unity";

            public string NavMeshAssetPath => $"Assets/_Project/Scenes/Game_{Name}NavMesh.asset";
        }

        /// <summary>
        /// The maps, in the order the menu offers them.
        ///
        /// The centres and sizes come from MapProbe, which reports a prefab's
        /// bounds without anyone having to open the editor and eyeball it.
        /// </summary>
        private static readonly MapDefinition[] Maps =
        {
            new MapDefinition
            {
                Name = "3Talwaar",
                PrefabPath = "Assets/_Project/Art/Maps/3Talwaar/Prefab/3 Talwaar v4.prefab",

                // 3 Talwaar is 537x464 metres, most of it outlying scenery. The
                // original walled off roughly 251x198 with road barriers, which
                // is a long way for one catcher to cover; this keeps the same
                // streets and tightens the game.
                ArenaCentre = new Vector3(11f, 0f, -9f),
                ArenaSize = 120f,
            },

            new MapDefinition
            {
                Name = "BadshahiMasjid",
                PrefabPath =
                    "Assets/_Project/Art/Maps/BadshahiMasjid/Prefab/Badshahi Masjid v2.prefab",

                // 154 wide by 282 long, so the narrow axis is what limits the
                // arena: 120 leaves about 17 metres of margin either side, and
                // anything wider would put a wall through the building.
                ArenaCentre = new Vector3(10.1f, 0f, 60.7f),
                ArenaSize = 120f,

                // The minarets reach 53 metres, far higher than anything on the
                // other two maps, so the map camera has to start above them or
                // it renders from inside one.
                MinimapCameraHeight = 90f,
            },

            new MapDefinition
            {
                Name = "ClockTower",
                PrefabPath =
                    "Assets/_Project/Art/Maps/ClockTower/Prefab/Faislabad - Clock Tower v3.prefab",

                // A 460 metre square of city centred on the origin. Nothing here
                // is above 15 metres, so the usual camera height clears it
                // easily and the short NavMesh volume cannot reach a roof.
                ArenaCentre = new Vector3(0f, 0f, 0f),
                ArenaSize = 120f,
            },
        };

        /// <summary>Tall enough that nobody vaults the arena walls.</summary>
        private const float ArenaHeight = 40f;

        [MenuItem("Baraf-Paani/Rebuild Playable Scene")]
        public static void Rebuild()
        {
            // Built first: both prefabs point at the controller, so it has to
            // exist before they are saved or they save a null reference.
            CharacterAnimatorBuilder.Rebuild();

            SoundBuilder.Rebuild();
            SoundBank sounds = EnsureSoundBank();

            // Before the characters: both of them carry a reference to it.
            GameObject decoyPrefab = BuildDecoyPrefab(sounds);

            GameObject playerPrefab = BuildPlayerPrefab(decoyPrefab, sounds);
            GameObject aiPrefab = BuildAiPrefab(decoyPrefab, sounds);

            // Made once, outside the loop. Every scene's map camera renders to
            // it, and it is only ever one scene at a time — but more to the
            // point, creating it per scene would delete and recreate the asset
            // each time and leave the scenes built before it pointing at a
            // texture that no longer exists.
            RenderTexture minimap = EnsureMinimapTexture();

            foreach (MapDefinition map in Maps)
            {
                BuildScene(playerPrefab, aiPrefab, decoyPrefab, sounds, minimap, map);
            }

            AssetDatabase.SaveAssets();

            Debug.Log(
                $"Baraf-Paani: rebuilt the prefabs and {Maps.Length} playable "
                + (Maps.Length == 1 ? "scene." : "scenes."));
        }

        private static GameObject BuildPlayerPrefab(GameObject decoyPrefab, SoundBank sounds)
        {
            GameObject root = new GameObject("Player");

            CharacterController controller = root.AddComponent<CharacterController>();
            controller.height = 2f;
            controller.radius = 0.35f;
            controller.center = new Vector3(0f, 1f, 0f);

            // Added before the body, because the body brings CharacterAppearance
            // with it and a NetworkBehaviour has to find an identity on the way
            // in. Adding it afterwards still saves a working prefab, but Mirror
            // complains on every rebuild, and a warning nobody can act on is a
            // warning everyone learns to scroll past.
            root.AddComponent<NetworkIdentity>();

            AddBody(root);
            AddCharacterAudio(root, sounds);

            GameObject cameraTarget = new GameObject("CameraTarget");
            cameraTarget.transform.SetParent(root.transform, false);
            cameraTarget.transform.localPosition = new Vector3(0f, 1.6f, 0f);

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
            freezeState.FindProperty("_motor").objectReferenceValue = motor;
            freezeState.FindProperty("_appearance").objectReferenceValue =
                root.GetComponent<CharacterAppearance>();
            freezeState.FindProperty("_animation").objectReferenceValue =
                root.GetComponent<CharacterAnimation>();
            freezeState.FindProperty("_audio").objectReferenceValue =
                root.GetComponent<CharacterAudio>();
            freezeState.ApplyModifiedPropertiesWithoutUndo();

            root.AddComponent<TagOnContact>();

            AddPowerUpEffects(root, decoyPrefab);
            root.AddComponent<PowerUpHolder>();
            root.AddComponent<PowerUpInput>();

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
        private static GameObject BuildAiPrefab(GameObject decoyPrefab, SoundBank sounds)
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

            // Added before the body, because the body brings CharacterAppearance
            // with it and a NetworkBehaviour has to find an identity on the way
            // in. Adding it afterwards still saves a working prefab, but Mirror
            // complains on every rebuild, and a warning nobody can act on is a
            // warning everyone learns to scroll past.
            root.AddComponent<NetworkIdentity>();

            AddBody(root);
            AddCharacterAudio(root, sounds);

            GameObject eye = new GameObject("Eye");
            eye.transform.SetParent(root.transform, false);
            eye.transform.localPosition = new Vector3(0f, 1.6f, 0f);

            NetworkTransformReliable sync = root.AddComponent<NetworkTransformReliable>();
            sync.target = root.transform;

            // Opposite of the player prefab: the server decides where AI go.
            sync.syncDirection = SyncDirection.ServerToClient;
            sync.syncInterval = 0.05f;

            root.AddComponent<PlayerRole>();

            Freezable freezable = root.AddComponent<Freezable>();
            SerializedObject freezeState = new SerializedObject(freezable);
            freezeState.FindProperty("_appearance").objectReferenceValue =
                root.GetComponent<CharacterAppearance>();
            freezeState.FindProperty("_animation").objectReferenceValue =
                root.GetComponent<CharacterAnimation>();
            freezeState.FindProperty("_audio").objectReferenceValue =
                root.GetComponent<CharacterAudio>();
            freezeState.ApplyModifiedPropertiesWithoutUndo();

            root.AddComponent<TagOnContact>();

            AddPowerUpEffects(root, decoyPrefab);
            root.AddComponent<PowerUpHolder>();

            Vision vision = AddVision(root, eye.transform);

            AiBrain brain = root.AddComponent<AiBrain>();
            SerializedObject brainState = new SerializedObject(brain);
            brainState.FindProperty("_vision").objectReferenceValue = vision;
            brainState.ApplyModifiedPropertiesWithoutUndo();

            // After the brain, which it requires and reads the intent from.
            root.AddComponent<AiPowerUpUse>();

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, AiPrefabPath);
            Object.DestroyImmediate(root);

            return saved;
        }

        /// <summary>
        /// The clone: a standing copy of whoever made it.
        ///
        /// Built from the same body as a real character, because the whole
        /// point is that it cannot be told apart at a glance. It carries the
        /// pieces that make it worth chasing — a role, a collider, and a
        /// Freezable so tagging it appears to work — and none of the pieces
        /// that would make it a player. No TagOnContact especially: a decoy
        /// that froze people by standing near them would be a weapon.
        /// </summary>
        private static GameObject BuildDecoyPrefab(SoundBank sounds)
        {
            GameObject root = new GameObject("Decoy");

            CapsuleCollider capsule = root.AddComponent<CapsuleCollider>();
            capsule.height = 2f;
            capsule.radius = 0.35f;
            capsule.center = new Vector3(0f, 1f, 0f);

            root.AddComponent<NetworkIdentity>();

            AddBody(root);
            AddCharacterAudio(root, sounds);

            root.AddComponent<PlayerRole>();

            Freezable freezable = root.AddComponent<Freezable>();
            SerializedObject freezeState = new SerializedObject(freezable);
            freezeState.FindProperty("_appearance").objectReferenceValue =
                root.GetComponent<CharacterAppearance>();
            freezeState.FindProperty("_animation").objectReferenceValue =
                root.GetComponent<CharacterAnimation>();
            freezeState.FindProperty("_audio").objectReferenceValue =
                root.GetComponent<CharacterAudio>();
            freezeState.ApplyModifiedPropertiesWithoutUndo();

            // No NetworkTransform: it never moves, and Mirror already sends the
            // position it was spawned at.
            root.AddComponent<Decoy>();

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, DecoyPrefabPath);
            Object.DestroyImmediate(root);

            return saved;
        }

        private static void AddPowerUpEffects(GameObject root, GameObject decoyPrefab)
        {
            PowerUpEffects effects = root.AddComponent<PowerUpEffects>();

            SerializedObject state = new SerializedObject(effects);
            state.FindProperty("_decoyPrefab").objectReferenceValue = decoyPrefab;
            state.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BuildScene(
            GameObject playerPrefab,
            GameObject aiPrefab,
            GameObject decoyPrefab,
            SoundBank sounds,
            RenderTexture minimap,
            MapDefinition map)
        {
            Scene scene = OpenOrCreate(map.ScenePath);

            ClearGenerated(scene);

            GameObject geometry = BuildMap(map);
            BuildArenaWalls(map);

            // The AI walks on this, so it needs a NavMesh surface. Only the
            // component is set up here — GameNetworkManager bakes it when the
            // server starts, because a bake done at build time is runtime-only
            // data that does not survive saving the scene.
            //
            // Bounded to the arena rather than the whole map: 3 Talwaar is
            // 537x464 metres, most of it outlying scenery, and without a volume
            // the agents would happily path off into it.
            NavMeshSurface surface = geometry.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Volume;
            surface.center = new Vector3(
                map.ArenaCentre.x, (map.NavMeshVolumeHeight * 0.5f) - 2f, map.ArenaCentre.z);
            surface.size = new Vector3(map.ArenaSize, map.NavMeshVolumeHeight, map.ArenaSize);

            BuildCamera();
            EnsureLight();
            BuildEventSystem();
            BuildNetworkManager(playerPrefab, aiPrefab, decoyPrefab);
            BuildMatch();
            BuildHud(sounds, minimap, map);
            BuildSpawnPoints(surface, map);
            BuildPowerUpPickups(sounds, map);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, map.ScenePath);
        }

        /// <summary>
        /// Opens a map's scene, making an empty one the first time a map is
        /// built. Without this, adding a map to the table would fail on a scene
        /// file nobody has created yet.
        /// </summary>
        private static Scene OpenOrCreate(string path)
        {
            if (System.IO.File.Exists(path))
            {
                return EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(path));

            return EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        /// <summary>
        /// Places the map and gives it collision.
        ///
        /// The imported prefab carries no colliders at all — the FBX was set to
        /// addColliders 0, and turning that on does not retro-fit the prefab,
        /// which stores its own component list. Without this, characters fall
        /// straight through the city and the NavMesh has nothing to bake onto.
        /// </summary>
        private static GameObject BuildMap(MapDefinition definition)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(definition.PrefabPath);

            if (prefab == null)
            {
                Debug.LogError($"No map prefab at {definition.PrefabPath}.");
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
        private static void BuildArenaWalls(MapDefinition map)
        {
            GameObject walls = new GameObject("ArenaWalls");

            float half = map.ArenaSize * 0.5f;
            const float thickness = 2f;

            (string name, Vector3 offset, Vector3 size)[] sides =
            {
                ("North", new Vector3(0f, 0f, half), new Vector3(map.ArenaSize, ArenaHeight, thickness)),
                ("South", new Vector3(0f, 0f, -half), new Vector3(map.ArenaSize, ArenaHeight, thickness)),
                ("East", new Vector3(half, 0f, 0f), new Vector3(thickness, ArenaHeight, map.ArenaSize)),
                ("West", new Vector3(-half, 0f, 0f), new Vector3(thickness, ArenaHeight, map.ArenaSize))
            };

            foreach ((string name, Vector3 offset, Vector3 size) in sides)
            {
                GameObject wall = new GameObject($"Wall {name}");
                wall.transform.SetParent(walls.transform, false);
                wall.transform.position =
                    map.ArenaCentre + offset + new Vector3(0f, ArenaHeight * 0.5f, 0f);

                BoxCollider box = wall.AddComponent<BoxCollider>();
                box.size = size;
            }
        }

        /// <summary>
        /// Puts spawn points on a ring, then drops each one onto the NavMesh so
        /// it lands on ground a character can actually stand on. A ring position
        /// picked blind could easily sit inside a building or off a kerb.
        /// </summary>
        /// <summary>
        /// Scatters the pickups around the middle of the arena.
        ///
        /// Placed on the baked NavMesh rather than at their ideal angle, for the
        /// same reason spawn points are: an ideal position is frequently inside
        /// a building. The roof check is here too — a power-up fourteen metres
        /// up is not a power-up, it is a thing nobody can ever reach.
        /// </summary>
        private static void BuildPowerUpPickups(SoundBank sounds, MapDefinition map)
        {
            int placed = 0;

            for (int i = 0; i < PickupRing.Length; i++)
            {
                // Offset half a step off the spawn ring's angles, so a pickup is
                // never sitting directly on top of a spawn point.
                float angle = (i + 0.5f) * Mathf.PI * 2f / PickupRing.Length;

                Vector3 ideal = map.ArenaCentre + new Vector3(
                    Mathf.Sin(angle) * map.PickupRingRadius,
                    0f,
                    Mathf.Cos(angle) * map.PickupRingRadius);

                if (!NavMesh.SamplePosition(
                        ideal, out NavMeshHit hit, map.PickupRingRadius, NavMesh.AllAreas))
                {
                    Debug.LogWarning($"PowerUp {i + 1}: no navigable ground near {ideal}.");
                    continue;
                }

                if (hit.position.y > map.MaxSpawnHeight)
                {
                    Debug.LogWarning(
                        $"PowerUp {i + 1}: nearest ground was {hit.position.y:F1}m up, so it was skipped.");
                    continue;
                }

                GameObject pickup = GameObject.CreatePrimitive(PrimitiveType.Cube);
                pickup.name = $"PowerUp {i + 1} {PickupRing[i]}";
                pickup.transform.position = hit.position + new Vector3(0f, PickupHeight, 0f);
                pickup.transform.localScale = new Vector3(0.6f, 0.6f, 0.6f);
                pickup.transform.rotation = Quaternion.Euler(35f, 0f, 35f);

                // A trigger, so it is walked through rather than bumped into.
                // How close counts as picking it up is PowerUpPickup's own
                // range, not this collider — nothing reads its trigger events.
                pickup.GetComponent<BoxCollider>().isTrigger = true;

                // A scene identity, so Mirror owns it from the start of the
                // match. The old build's pickups were plain objects that each
                // client handled for itself, which is how two people could take
                // the same one.
                pickup.AddComponent<NetworkIdentity>();

                PowerUpPickup component = pickup.AddComponent<PowerUpPickup>();
                SerializedObject state = new SerializedObject(component);
                // intValue, not enumValueIndex: the latter is a position in the
                // name list, which only matches the value while the enum happens
                // to be numbered from zero with no gaps.
                state.FindProperty("_kind").intValue = (int)PickupRing[i];
                state.FindProperty("_sounds").objectReferenceValue = sounds;
                state.ApplyModifiedPropertiesWithoutUndo();

                placed++;
            }

            Debug.Log($"Power-up pickups placed on the NavMesh: {placed} of {PickupRing.Length}.");
        }

        private static void BuildSpawnPoints(NavMeshSurface surface, MapDefinition map)
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
                Vector3 ideal = map.ArenaCentre + new Vector3(
                    Mathf.Sin(angle) * map.SpawnRingRadius,
                    0f,
                    Mathf.Cos(angle) * map.SpawnRingRadius);

                if (!NavMesh.SamplePosition(
                        ideal, out NavMeshHit hit, map.SpawnRingRadius, NavMesh.AllAreas))
                {
                    Debug.LogWarning($"SpawnPoint {i + 1}: no navigable ground near {ideal}.");
                    continue;
                }

                // Belt and braces alongside the short NavMesh volume: never put
                // anyone on a roof, however the bake turns out.
                if (hit.position.y > map.MaxSpawnHeight)
                {
                    Debug.LogWarning(
                        $"SpawnPoint {i + 1}: nearest ground was {hit.position.y:F1}m up, so it was skipped.");
                    continue;
                }

                GameObject spawn = new GameObject($"SpawnPoint {i + 1}");
                spawn.transform.position = hit.position + new Vector3(0f, 0.1f, 0f);

                // Face the middle, so whoever spawns here is looking at the game
                // rather than out at empty ground.
                Vector3 inward = map.ArenaCentre - hit.position;
                inward.y = 0f;

                if (inward.sqrMagnitude > 0.001f)
                {
                    spawn.transform.rotation = Quaternion.LookRotation(inward.normalized);
                }

                spawn.AddComponent<NetworkStartPosition>();
                placed++;
            }

            SaveNavMesh(surface, map);

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
        /// <summary>
        /// The round runner, as its own scene object.
        ///
        /// Not on the NetworkManager, which is where it started: that object is
        /// DontDestroyOnLoad and is not a spawned scene identity, so Mirror never
        /// called OnStartServer on it. The round therefore never got an end time,
        /// the clock read as already expired, and every match was won by the
        /// runners the instant it began.
        /// </summary>
        private static void BuildMatch()
        {
            GameObject match = new GameObject("Match");
            match.AddComponent<NetworkIdentity>();
            match.AddComponent<MatchState>();
        }

        private static void BuildHud(SoundBank sounds, RenderTexture texture, MapDefinition map)
        {
            int mapLayer = EnsureMapLayer();

            Sprite circle = MinimapSprites.EnsureCircle();
            Sprite ring = MinimapSprites.EnsureRing();

            Camera mapCamera = BuildMinimapCamera(texture, mapLayer, map);

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
            viewState.FindProperty("_mapCamera").objectReferenceValue = mapCamera;
            viewState.ApplyModifiedPropertiesWithoutUndo();

            BuildMatchLabels(hud, sounds);

            Debug.Log("HUD: circular minimap and match labels built.");
        }


        /// <summary>
        /// The round's readout: who is left, the clock, and the result.
        ///
        /// Legacy UI Text with Unity's built-in font, not TextMeshPro. TMP ships
        /// with the UI package but needs its essential resources imported through
        /// a menu before it will render anything, and that is a step a fresh
        /// clone would not have taken. Worth upgrading once, deliberately.
        /// </summary>
        private static void BuildMatchLabels(GameObject hud, SoundBank sounds)
        {
            // The game's own font, brought over from the original build.
            Font font = AssetDatabase.LoadAssetAtPath<Font>(MenuFontPath)
                ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            Text runners = MakeLabel(hud, "RunnersLabel", font, 22, TextAnchor.UpperLeft);
            Place(runners.rectTransform, new Vector2(0f, 1f), new Vector2(24f, -24f), new Vector2(340f, 32f));

            Text clock = MakeLabel(hud, "ClockLabel", font, 34, TextAnchor.UpperCenter);
            Place(clock.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(200f, 44f));

            Text result = MakeLabel(hud, "ResultLabel", font, 40, TextAnchor.MiddleCenter);
            Place(result.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 120f), new Vector2(900f, 60f));
            result.enabled = false;

            Text carried = MakeLabel(hud, "PowerUpsLabel", font, 22, TextAnchor.LowerLeft);
            Place(carried.rectTransform, new Vector2(0f, 0f), new Vector2(24f, 24f), new Vector2(520f, 32f));
            carried.supportRichText = true;

            Text active = MakeLabel(hud, "PowerUpActiveLabel", font, 20, TextAnchor.LowerLeft);
            Place(active.rectTransform, new Vector2(0f, 0f), new Vector2(24f, 60f), new Vector2(320f, 28f));
            active.enabled = false;

            PowerUpHud powerUps = hud.AddComponent<PowerUpHud>();

            SerializedObject powerUpState = new SerializedObject(powerUps);
            powerUpState.FindProperty("_carriedLabel").objectReferenceValue = carried;
            powerUpState.FindProperty("_activeLabel").objectReferenceValue = active;
            powerUpState.ApplyModifiedPropertiesWithoutUndo();

            Text lobbyTitle = MakeLabel(hud, "LobbyTitleLabel", font, 34, TextAnchor.MiddleCenter);
            Place(lobbyTitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 140f), new Vector2(900f, 46f));

            Text lobbyRoster = MakeLabel(hud, "LobbyRosterLabel", font, 24, TextAnchor.MiddleCenter);
            Place(lobbyRoster.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 70f), new Vector2(900f, 70f));

            Text lobbyHint = MakeLabel(hud, "LobbyHintLabel", font, 20, TextAnchor.MiddleCenter);
            Place(lobbyHint.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(900f, 32f));

            LobbyHud lobby = hud.AddComponent<LobbyHud>();

            SerializedObject lobbyState = new SerializedObject(lobby);
            lobbyState.FindProperty("_titleLabel").objectReferenceValue = lobbyTitle;
            lobbyState.FindProperty("_rosterLabel").objectReferenceValue = lobbyRoster;
            lobbyState.FindProperty("_hintLabel").objectReferenceValue = lobbyHint;
            lobbyState.ApplyModifiedPropertiesWithoutUndo();

            AudioSource matchSource = hud.AddComponent<AudioSource>();
            matchSource.playOnAwake = false;

            // Flat, not positioned: a round starting is not somewhere in the
            // street, it is an announcement.
            matchSource.spatialBlend = 0f;

            MatchAudio matchAudio = hud.AddComponent<MatchAudio>();

            SerializedObject audioState = new SerializedObject(matchAudio);
            audioState.FindProperty("_sounds").objectReferenceValue = sounds;
            audioState.FindProperty("_source").objectReferenceValue = matchSource;
            audioState.ApplyModifiedPropertiesWithoutUndo();

            Text immunity = MakeLabel(hud, "ImmunityLabel", font, 26, TextAnchor.UpperCenter);
            Place(immunity.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -80f),
                new Vector2(500f, 36f));
            immunity.color = new Color(0.6f, 1f, 0.7f);
            immunity.enabled = false;

            BuildInGameMenu(hud, font);

            // Volume and look sensitivity are settings the menu owns, but they
            // have to be put into effect wherever the game actually is.
            SettingsApplier applier = hud.AddComponent<SettingsApplier>();
            SerializedObject applierState = new SerializedObject(applier);
            applierState.FindProperty("_settings").objectReferenceValue =
                MainMenuBuilder.EnsureSettings();
            applierState.ApplyModifiedPropertiesWithoutUndo();

            Text score = MakeLabel(hud, "ScoreLabel", font, 20, TextAnchor.UpperRight);
            Place(score.rectTransform, new Vector2(1f, 1f), new Vector2(-24f, -24f),
                new Vector2(560f, 30f));

            MatchHud matchHud = hud.AddComponent<MatchHud>();

            SerializedObject state = new SerializedObject(matchHud);
            state.FindProperty("_runnersLabel").objectReferenceValue = runners;
            state.FindProperty("_clockLabel").objectReferenceValue = clock;
            state.FindProperty("_resultLabel").objectReferenceValue = result;
            state.FindProperty("_immunityLabel").objectReferenceValue = immunity;
            state.FindProperty("_scoreLabel").objectReferenceValue = score;
            state.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Buttons do nothing without one of these, and a game scene built from
        /// code has no reason to have picked one up. The in-game menu was
        /// unclickable without it — the same way the main menu shipped once.
        /// </summary>
        private static void BuildEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null)
            {
                return;
            }

            GameObject events = new GameObject("EventSystem");
            events.AddComponent<EventSystem>();

            // The Input System's module. The legacy one reads UnityEngine.Input,
            // which throws in this project.
            InputSystemUIInputModule module = events.AddComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions();
        }

        /// <summary>
        /// The overlay reached with Escape: resume, leave the match, or quit.
        ///
        /// Built inactive. It is not a pause — the match carries on without the
        /// player reading it, because the server has not stopped for anyone.
        /// </summary>
        private static void BuildInGameMenu(GameObject hud, Font font)
        {
            GameObject panel = new GameObject("InGameMenu");
            panel.transform.SetParent(hud.transform, false);

            RectTransform panelRect = panel.AddComponent<RectTransform>();
            Stretch(panelRect);

            Image shade = panel.AddComponent<Image>();
            shade.color = new Color(0.02f, 0.03f, 0.05f, 0.72f);

            Text heading = MakeLabel(panel, "Heading", font, 40, TextAnchor.MiddleCenter);
            Place(heading.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 150f),
                new Vector2(600f, 56f));
            heading.text = "PAUSED";

            Button resume = MakeHudButton(panel, "ResumeButton", font, "RESUME", 60f);
            Button leave = MakeHudButton(panel, "LeaveButton", font, "LEAVE MATCH", -30f);
            Button quit = MakeHudButton(panel, "QuitButton", font, "QUIT GAME", -120f);

            Text hint = MakeLabel(panel, "Hint", font, 18, TextAnchor.MiddleCenter);
            Place(hint.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -210f),
                new Vector2(700f, 30f));
            hint.text = "The match keeps running while this is open";

            InGameMenu menu = hud.AddComponent<InGameMenu>();

            SerializedObject state = new SerializedObject(menu);
            state.FindProperty("_panel").objectReferenceValue = panel;
            state.FindProperty("_resumeButton").objectReferenceValue = resume;
            state.FindProperty("_leaveButton").objectReferenceValue = leave;
            state.FindProperty("_quitButton").objectReferenceValue = quit;
            state.ApplyModifiedPropertiesWithoutUndo();

            panel.SetActive(false);
        }

        private static Button MakeHudButton(
            GameObject parent, string name, Font font, string label, float y)
        {
            GameObject button = new GameObject(name);
            button.transform.SetParent(parent.transform, false);

            RectTransform rect = button.AddComponent<RectTransform>();
            Place(rect, new Vector2(0.5f, 0.5f), new Vector2(0f, y), new Vector2(340f, 68f));

            Image plate = button.AddComponent<Image>();
            plate.color = new Color(0.16f, 0.19f, 0.26f, 0.95f);

            Button control = button.AddComponent<Button>();
            control.targetGraphic = plate;

            Text text = MakeLabel(button, "Label", font, 26, TextAnchor.MiddleCenter);
            Stretch(text.rectTransform);
            text.text = label;

            button.AddComponent<ButtonFeel>();

            return control;
        }

        private static Text MakeLabel(GameObject parent, string name, Font font, int size, TextAnchor anchor)
        {
            GameObject label = new GameObject(name);
            label.transform.SetParent(parent.transform, false);

            Text text = label.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.alignment = anchor;
            text.color = Color.white;
            text.raycastTarget = false;

            // A dark outline, so white text stays readable over a bright street
            // as well as over shadow.
            Outline outline = label.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.75f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            return text;
        }

        private static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
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
        private static RenderTexture EnsureMinimapTexture()
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

        private static Camera BuildMinimapCamera(
            RenderTexture texture, int mapLayer, MapDefinition map)
        {
            GameObject cameraObject = new GameObject("MinimapCamera");

            // Starting position only; the rig moves it onto the local player as
            // soon as there is one.
            cameraObject.transform.position =
                map.ArenaCentre + new Vector3(0f, map.MinimapCameraHeight, 0f);
            cameraObject.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            Camera mapCamera = cameraObject.AddComponent<Camera>();
            mapCamera.orthographic = true;
            mapCamera.orthographicSize = map.MinimapViewExtent;

            mapCamera.cullingMask = 1 << mapLayer;
            mapCamera.clearFlags = CameraClearFlags.SolidColor;
            mapCamera.backgroundColor = new Color(0.10f, 0.11f, 0.13f);
            mapCamera.targetTexture = texture;
            mapCamera.depth = -10;

            cameraObject.AddComponent<MinimapCameraRig>();

            return mapCamera;
        }

        /// <summary>
        /// Adds sight, with the map set as the thing that blocks it.
        ///
        /// The blocking mask was empty until now, which meant line of sight was
        /// never actually blocked by anything — on a flat plane that made no
        /// difference, but on a city it would have let runners see the catcher
        /// straight through buildings and left the minimap blip permanently lit.
        /// </summary>
        /// <summary>
        /// Puts the character model under a root and wires up everything that
        /// makes it move and change colour.
        ///
        /// Shared by both prefabs on purpose: a player and an AI have to be the
        /// same character. When the visuals lived on each prefab separately in
        /// the old build they drifted, and an AI ended up a different height
        /// than a human — which changes who can see whom over a wall.
        /// </summary>
        /// <summary>
        /// Loads the sound bank, creating it and filling it in from the audio
        /// folders if it is not there. Built the same way as everything else, so
        /// a fresh clone gets a wired-up bank without anyone dragging clips into
        /// an inspector.
        /// </summary>
        private static SoundBank EnsureSoundBank()
        {
            SoundBank bank = AssetDatabase.LoadAssetAtPath<SoundBank>(SoundBankPath);

            if (bank == null)
            {
                bank = ScriptableObject.CreateInstance<SoundBank>();
                AssetDatabase.CreateAsset(bank, SoundBankPath);
            }

            SerializedObject state = new SerializedObject(bank);

            Assign(state, "_freeze", "Generated/Freeze");
            Assign(state, "_thaw", "Generated/Thaw");
            Assign(state, "_pickup", "Generated/Pickup");
            Assign(state, "_powerUp", "Generated/PowerUp");
            Assign(state, "_roundStart", "Generated/RoundStart");
            Assign(state, "_roundWon", "Generated/RoundWon");
            Assign(state, "_roundLost", "Generated/RoundLost");
            Assign(state, "_click", "Generated/Click");
            Assign(state, "_land", "Footsteps/Player_Land");

            SerializedProperty footsteps = state.FindProperty("_footsteps");
            footsteps.arraySize = 10;

            for (int i = 0; i < 10; i++)
            {
                footsteps.GetArrayElementAtIndex(i).objectReferenceValue =
                    Clip($"Footsteps/Player_Footstep_{i + 1:00}");
            }

            state.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(bank);

            return bank;
        }

        private static void Assign(SerializedObject state, string field, string clip)
        {
            state.FindProperty(field).objectReferenceValue = Clip(clip);
        }

        private static AudioClip Clip(string relative)
        {
            string path = $"Assets/_Project/Audio/{relative}.wav";
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);

            if (clip == null)
            {
                Debug.LogWarning($"Baraf-Paani: no audio clip at {path}.");
            }

            return clip;
        }

        /// <summary>
        /// Gives a character a voice: one spatial source, and the component that
        /// decides what comes out of it.
        /// </summary>
        private static void AddCharacterAudio(GameObject root, SoundBank sounds)
        {
            AudioSource source = root.AddComponent<AudioSource>();
            source.playOnAwake = false;

            // Fully 3D, so you can hear which direction someone is running from.
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 2f;
            source.maxDistance = 30f;

            CharacterAudio audio = root.AddComponent<CharacterAudio>();

            SerializedObject state = new SerializedObject(audio);
            state.FindProperty("_sounds").objectReferenceValue = sounds;
            state.FindProperty("_source").objectReferenceValue = source;
            state.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AddBody(GameObject root)
        {
            GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterModelPath);

            if (modelAsset == null)
            {
                Debug.LogError($"Baraf-Paani: no character model at {CharacterModelPath}.");
                return;
            }

            GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
            model.name = "Body";
            model.transform.SetParent(root.transform, false);

            // The model's origin is at its feet, same as the root's, so it
            // stands on the ground rather than floating a capsule's height up.
            model.transform.localPosition = Vector3.zero;

            Animator animator = model.GetComponent<Animator>();

            if (animator != null)
            {
                animator.runtimeAnimatorController =
                    AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                        CharacterAnimatorBuilder.ControllerPath);

                // The motor and the NavMeshAgent are what move a character.
                // Root motion would be a second thing moving it, and the two
                // fight: the visible character drifts away from its collider,
                // so freezes land on someone who is not standing there.
                animator.applyRootMotion = false;

                // Off-screen characters still need their transforms right —
                // freeze is decided by where a character is, not by whether
                // anyone is looking at it.
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }
            else
            {
                Debug.LogError("Baraf-Paani: the character model has no Animator.");
            }

            Renderer[] renderers = model.GetComponentsInChildren<Renderer>();

            CharacterAppearance appearance = root.AddComponent<CharacterAppearance>();
            SerializedObject look = new SerializedObject(appearance);
            SerializedProperty list = look.FindProperty("_renderers");
            list.arraySize = renderers.Length;

            for (int i = 0; i < renderers.Length; i++)
            {
                list.GetArrayElementAtIndex(i).objectReferenceValue = renderers[i];
            }

            look.ApplyModifiedPropertiesWithoutUndo();

            CharacterAnimation animation = root.AddComponent<CharacterAnimation>();
            SerializedObject motion = new SerializedObject(animation);
            motion.FindProperty("_animator").objectReferenceValue = animator;

            // Sprinting is faster than any clip we have, so the character needs
            // to know where the clips run out.
            motion.FindProperty("_topClipSpeed").floatValue =
                CharacterAnimatorBuilder.TopClipSpeed();

            motion.ApplyModifiedPropertiesWithoutUndo();

            ReportHeight(renderers);
        }

        /// <summary>
        /// Logs how tall the model actually is, because the colliders, the eye
        /// height and the camera target are all hand-set to a two metre
        /// character. If an imported model does not match, everything sighted
        /// through those numbers is quietly wrong.
        /// </summary>
        private static void ReportHeight(Renderer[] renderers)
        {
            if (renderers.Length == 0)
            {
                return;
            }

            Bounds bounds = renderers[0].bounds;

            foreach (Renderer renderer in renderers)
            {
                bounds.Encapsulate(renderer.bounds);
            }

            Debug.Log($"Baraf-Paani: character model stands {bounds.size.y:0.00}m tall.");
        }

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
        private static void SaveNavMesh(NavMeshSurface surface, MapDefinition map)
        {
            NavMeshData data = surface.navMeshData;

            if (data == null)
            {
                Debug.LogError("NavMesh bake produced nothing, so the AI will have nowhere to walk.");
                return;
            }

            AssetDatabase.DeleteAsset(map.NavMeshAssetPath);
            AssetDatabase.CreateAsset(data, map.NavMeshAssetPath);
            AssetDatabase.SaveAssets();

            surface.navMeshData = AssetDatabase.LoadAssetAtPath<NavMeshData>(map.NavMeshAssetPath);

            Debug.Log($"NavMesh baked to {map.NavMeshAssetPath}.");
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
                "HUD", "MinimapCamera", "Match"
            };

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                bool isGenerated = System.Array.IndexOf(generated, root.name) >= 0
                    || root.name.StartsWith("SpawnPoint")

                    // By component rather than by name. Anything this list
                    // forgets is left behind and quietly doubled on the next
                    // rebuild, which is exactly what happened to the pickups.
                    || root.GetComponent<PowerUpPickup>() != null;

                if (isGenerated)
                {
                    Object.DestroyImmediate(root);
                }
            }
        }

        /// <summary>
        /// The camera the player looks through, and the rig that follows them.
        ///
        /// Creates the camera rather than assuming one is there. A scene built
        /// for a map nobody had added yet starts from an empty scene, which has
        /// no camera, no audio listener and no light — so the first two maps
        /// added this way came out with nothing rendering at all. Depending on
        /// whatever the scene template happened to provide is the bug; building
        /// every scene the same way regardless is the fix.
        /// </summary>
        private static void BuildCamera()
        {
            Camera main = Camera.main ?? CreateMainCamera();

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

        private static Camera CreateMainCamera()
        {
            GameObject cameraObject = new GameObject("Main Camera");

            // Camera.main finds by tag, so without this the next call would
            // create a second one.
            cameraObject.tag = "MainCamera";

            Camera camera = cameraObject.AddComponent<Camera>();

            // The listener lives here, as it does on Unity's own camera. Its
            // absence is the whole game being silent, and the only warning is
            // one line in the console.
            cameraObject.AddComponent<AudioListener>();

            return camera;
        }

        /// <summary>
        /// A sun. An empty scene has none, and an unlit city reads as a broken
        /// map rather than a dark one.
        /// </summary>
        private static void EnsureLight()
        {
            foreach (Light existing in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (existing.type == LightType.Directional)
                {
                    return;
                }
            }

            GameObject sun = new GameObject("Directional Light");
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            Light light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.96f, 0.84f);
            light.intensity = 1.1f;
            light.shadows = LightShadows.Soft;
        }

        private static void BuildNetworkManager(
            GameObject playerPrefab, GameObject aiPrefab, GameObject decoyPrefab)
        {
            GameObject host = new GameObject("NetworkManager");

            KcpTransport transport = host.AddComponent<KcpTransport>();

            GameNetworkManager manager = host.AddComponent<GameNetworkManager>();
            manager.transport = transport;
            manager.playerPrefab = playerPrefab;

            // So a host can be found on the local network rather than having its
            // address read out over the phone. StartMultiplayerHost turns it on;
            // single-player never does.
            DiscoverySetup.AddTo(host, transport);
            manager.autoCreatePlayer = true;
            manager.playerSpawnMethod = PlayerSpawnMethod.RoundRobin;

            // Clients need the AI prefab registered or they cannot spawn what the
            // server tells them about.
            manager.spawnPrefabs.Clear();
            manager.spawnPrefabs.Add(aiPrefab);
            manager.spawnPrefabs.Add(decoyPrefab);

            SerializedObject managerState = new SerializedObject(manager);
            managerState.FindProperty("_aiCharacterPrefab").objectReferenceValue = aiPrefab;
            managerState.ApplyModifiedPropertiesWithoutUndo();

            // Starts whatever the menu asked for. Does nothing when the scene is
            // opened on its own, which is what keeps it usable without the menu.
            GameLauncher launcher = host.AddComponent<GameLauncher>();

            SerializedObject launcherState = new SerializedObject(launcher);
            launcherState.FindProperty("_setup").objectReferenceValue =
                MainMenuBuilder.EnsureSetup();
            launcherState.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
