using BarafPaani.Core;
using BarafPaani.UI;
using kcp2k;
using Mirror;
using Mirror.Discovery;
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

            Text title = MakeText(canvasObject, "Title", font, 44);
            Place(title.rectTransform, new Vector2(0f, 320f), new Vector2(900f, 60f));
            title.text = "JOIN A MATCH";

            // Rows hang from the top edge of this, so its height is how many
            // hosts can be listed before they run off the bottom.
            GameObject listObject = new GameObject("HostList");
            listObject.transform.SetParent(canvasObject.transform, false);
            RectTransform list = listObject.AddComponent<RectTransform>();
            Place(list, new Vector2(0f, 60f), new Vector2(640f, 340f));

            Text empty = MakeText(canvasObject, "EmptyLabel", font, 22);
            Place(empty.rectTransform, new Vector2(0f, 180f), new Vector2(760f, 70f));

            InputField address = MakeAddressField(canvasObject, font);

            Button connect = MakeButton(canvasObject, font, "ConnectButton", "CONNECT",
                new Vector2(0f, -230f), new Vector2(260f, 56f));

            Text status = MakeText(canvasObject, "StatusLabel", font, 26);
            Place(status.rectTransform, new Vector2(0f, -310f), new Vector2(1200f, 50f));

            Button back = MakeBackButton(canvasObject, font);

            // Volume applies here too, or the game would be silent on the menu
            // and loud the moment a match loaded.
            SettingsApplier volume = canvasObject.AddComponent<SettingsApplier>();
            SerializedObject volumeState = new SerializedObject(volume);
            volumeState.FindProperty("_settings").objectReferenceValue =
                SettingsUiBuilder.EnsureSettings();
            volumeState.ApplyModifiedPropertiesWithoutUndo();

            ConnectingScreen screen = canvasObject.AddComponent<ConnectingScreen>();
            HostBrowser browser = canvasObject.AddComponent<HostBrowser>();

            // Loaded after NewScene, which unloads unused assets and would leave
            // a reference taken before it destroyed and silently null.
            MatchSetup setup = MainMenuBuilder.EnsureSetup();

            SerializedObject state = new SerializedObject(screen);
            state.FindProperty("_statusLabel").objectReferenceValue = status;
            state.FindProperty("_backButton").objectReferenceValue = back;
            state.FindProperty("_setup").objectReferenceValue = setup;
            state.ApplyModifiedPropertiesWithoutUndo();

            NetworkDiscovery discovery = BuildNetworkManager();

            SerializedObject browserState = new SerializedObject(browser);
            browserState.FindProperty("_discovery").objectReferenceValue = discovery;
            browserState.FindProperty("_listRoot").objectReferenceValue = list;
            browserState.FindProperty("_emptyLabel").objectReferenceValue = empty;
            browserState.FindProperty("_addressField").objectReferenceValue = address;
            browserState.FindProperty("_connectButton").objectReferenceValue = connect;
            browserState.FindProperty("_screen").objectReferenceValue = screen;
            browserState.FindProperty("_setup").objectReferenceValue = setup;
            browserState.FindProperty("_font").objectReferenceValue = font;
            browserState.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            Debug.Log("Baraf-Paani: rebuilt the connecting scene.");
        }

        /// <summary>
        /// The manager the client connects through, and the discovery that finds
        /// hosts to offer it. Carries the same spawnable prefabs as a map scene,
        /// because the client has to be able to spawn what the server tells it
        /// about the moment it is connected — which is before the host's map has
        /// finished loading.
        /// </summary>
        private static NetworkDiscovery BuildNetworkManager()
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

            // No GameLauncher here on purpose. Arriving on this screen is not a
            // request to connect to anything — the browser connects when a host
            // is picked, or when an address is typed and CONNECT is pressed.
            return DiscoverySetup.AddTo(host, transport);
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
            return MakeButton(canvas, font, "BackButton", "BACK",
                new Vector2(0f, -390f), new Vector2(260f, 56f));
        }

        private static Button MakeButton(
            GameObject canvas, Font font, string name, string text, Vector2 position, Vector2 size)
        {
            GameObject buttonObject = new GameObject(name);
            buttonObject.transform.SetParent(canvas.transform, false);

            Image plate = buttonObject.AddComponent<Image>();
            plate.color = new Color(1f, 1f, 1f, 0.12f);

            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = plate;

            Place(buttonObject.GetComponent<RectTransform>(), position, size);

            Text label = MakeText(buttonObject, "Label", font, 22);
            Stretch(label.rectTransform);
            label.text = text;

            return button;
        }

        /// <summary>
        /// Typing an address is the thing that always works. Broadcast does not
        /// cross subnets, does not reach the internet, and is blocked outright
        /// on plenty of networks, so the list is a convenience on top of this
        /// rather than a replacement for it.
        /// </summary>
        private static InputField MakeAddressField(GameObject canvas, Font font)
        {
            GameObject fieldObject = new GameObject("AddressField");
            fieldObject.transform.SetParent(canvas.transform, false);

            Image plate = fieldObject.AddComponent<Image>();
            plate.color = new Color(0f, 0f, 0f, 0.35f);

            Place(fieldObject.GetComponent<RectTransform>(),
                new Vector2(0f, -150f), new Vector2(420f, 56f));

            Text text = MakeText(fieldObject, "Text", font, 22);
            Stretch(text.rectTransform);
            text.supportRichText = false;

            Text placeholder = MakeText(fieldObject, "Placeholder", font, 22);
            Stretch(placeholder.rectTransform);
            placeholder.text = "address";
            placeholder.color = new Color(1f, 1f, 1f, 0.4f);

            InputField field = fieldObject.AddComponent<InputField>();
            field.textComponent = text;
            field.placeholder = placeholder;
            field.lineType = InputField.LineType.SingleLine;

            return field;
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
