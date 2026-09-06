using BarafPaani.Core;
using BarafPaani.UI;
using kcp2k;
using Mirror;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BarafPaani.EditorTools
{
    /// <summary>
    /// Builds the scene a joining player passes through on the way in.
    ///
    /// Deliberately almost empty: a camera, a line of text and a way back. Its
    /// whole job is to hold a NetworkManager so a client can connect without
    /// having loaded a map first, and to load fast enough that nobody minds it
    /// being there.
    /// </summary>
    public static class ConnectingSceneBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/Connecting.unity";

        private const string FontPath = "Assets/_Project/Art/UI/Fonts/Orbitron.ttf";

        private const string PlayerPrefabPath = "Assets/_Project/Prefabs/Player.prefab";

        private const string AiPrefabPath = "Assets/_Project/Prefabs/AiCharacter.prefab";

        private const string DecoyPrefabPath = "Assets/_Project/Prefabs/Decoy.prefab";

        [MenuItem("Baraf-Paani/Rebuild Connecting Scene")]
        public static void Rebuild()
        {
            Font font = AssetDatabase.LoadAssetAtPath<Font>(FontPath)
                ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene, NewSceneMode.Single);

            BuildCamera();
            BuildEventSystem();

            GameObject canvasObject = new GameObject("Canvas");
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            canvasObject.AddComponent<GraphicRaycaster>();

            Text status = MakeText(canvasObject, "StatusLabel", font, 30);
            Place(status.rectTransform, new Vector2(0f, 20f), new Vector2(1200f, 60f));

            Button back = MakeBackButton(canvasObject, font);

            ConnectingScreen screen = canvasObject.AddComponent<ConnectingScreen>();

            // Loaded after NewScene, which unloads unused assets and would leave
            // a reference taken before it destroyed and silently null.
            MatchSetup setup = MainMenuBuilder.EnsureSetup();

            SerializedObject state = new SerializedObject(screen);
            state.FindProperty("_statusLabel").objectReferenceValue = status;
            state.FindProperty("_backButton").objectReferenceValue = back;
            state.FindProperty("_setup").objectReferenceValue = setup;
            state.ApplyModifiedPropertiesWithoutUndo();

            BuildNetworkManager(setup);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            Debug.Log("Baraf-Paani: rebuilt the connecting scene.");
        }

        /// <summary>
        /// The manager the client connects through, and the launcher that starts
        /// the attempt. Carries the same spawnable prefabs as a map scene,
        /// because the client has to be able to spawn what the server tells it
        /// about the moment it is connected — which is before the host's map has
        /// finished loading.
        /// </summary>
        private static void BuildNetworkManager(MatchSetup setup)
        {
            GameObject host = new GameObject("NetworkManager");

            KcpTransport transport = host.AddComponent<KcpTransport>();

            GameNetworkManager manager = host.AddComponent<GameNetworkManager>();
            manager.transport = transport;
            manager.playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            manager.autoCreatePlayer = true;
            manager.playerSpawnMethod = PlayerSpawnMethod.RoundRobin;

            manager.spawnPrefabs.Clear();
            manager.spawnPrefabs.Add(AssetDatabase.LoadAssetAtPath<GameObject>(AiPrefabPath));
            manager.spawnPrefabs.Add(AssetDatabase.LoadAssetAtPath<GameObject>(DecoyPrefabPath));

            GameLauncher launcher = host.AddComponent<GameLauncher>();

            SerializedObject state = new SerializedObject(launcher);
            state.FindProperty("_setup").objectReferenceValue = setup;
            state.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BuildCamera()
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";

            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.06f, 0.07f, 0.09f);

            cameraObject.AddComponent<AudioListener>();
        }

        /// <summary>
        /// The Input System's module, not the legacy one. This project is set to
        /// the new Input System only, where UnityEngine.Input throws — a
        /// StandaloneInputModule here would mean a Back button nobody can press.
        /// </summary>
        private static void BuildEventSystem()
        {
            GameObject events = new GameObject("EventSystem");
            events.AddComponent<UnityEngine.EventSystems.EventSystem>();

            InputSystemUIInputModule module = events.AddComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions();
        }

        private static Button MakeBackButton(GameObject canvas, Font font)
        {
            GameObject buttonObject = new GameObject("BackButton");
            buttonObject.transform.SetParent(canvas.transform, false);

            Image plate = buttonObject.AddComponent<Image>();
            plate.color = new Color(1f, 1f, 1f, 0.12f);

            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = plate;

            Place(buttonObject.GetComponent<RectTransform>(),
                new Vector2(0f, -90f), new Vector2(260f, 60f));

            Text label = MakeText(buttonObject, "Label", font, 22);
            Stretch(label.rectTransform);
            label.text = "BACK";

            return button;
        }

        private static Text MakeText(GameObject parent, string name, Font font, int size)
        {
            GameObject label = new GameObject(name);
            label.transform.SetParent(parent.transform, false);

            Text text = label.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.raycastTarget = false;

            return text;
        }

        private static void Place(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
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
    }
}
