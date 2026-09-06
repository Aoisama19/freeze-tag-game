using System.Collections.Generic;
using System.IO;
using BarafPaani.Core;
using BarafPaani.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BarafPaani.EditorTools
{
    /// <summary>
    /// Builds the main menu scene, using the artwork from the original game.
    ///
    /// The old menu was far larger than this — character select, abilities, map
    /// select, audio and controls panels. Most of that fronts systems this
    /// rebuild does not have yet, so what is here covers what actually exists:
    /// pick a side, then play alone, host, or join. The rest of the art is
    /// waiting for the systems it belongs to.
    /// </summary>
    public static class MainMenuBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/MainMenu.unity";
        private const string SetupPath = "Assets/_Project/Settings/MatchSetup.asset";
        private const string ArtRoot = "Assets/_Project/Art/UI/Menu/";
        private const string FontPath = "Assets/_Project/Art/UI/Fonts/Orbitron.ttf";

        [MenuItem("Baraf-Paani/Rebuild Main Menu")]
        public static void Rebuild()
        {
            Font font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);

            if (font == null)
            {
                Debug.LogWarning($"No font at {FontPath}; falling back to the built-in one.");
                font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }

            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            GameObject canvasObject = new GameObject("Canvas");
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            canvasObject.AddComponent<GraphicRaycaster>();

            BuildEventSystem();

            BuildBackground(canvasObject);
            Text title = MakeText(canvasObject, "Title", font, 90, TextAnchor.MiddleCenter);
            Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(1200f, 120f));
            title.text = "BARAF-PAANI";

            Text roleLabel = MakeText(canvasObject, "RoleLabel", font, 30, TextAnchor.MiddleCenter);
            Place(roleLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 200f), new Vector2(900f, 44f));

            Button catcher = MakeButton(canvasObject, "CatcherButton", font, "CATCHER",
                new Vector2(0.5f, 0.5f), new Vector2(-150f, 120f), new Vector2(260f, 70f),
                Sprite("PlayerType_people.png"));

            Button runner = MakeButton(canvasObject, "RunnerButton", font, "RUNNER",
                new Vector2(0.5f, 0.5f), new Vector2(150f, 120f), new Vector2(260f, 70f),
                Sprite("PlayerType_Runner.png"));

            Sprite playPlate = Sprite("Playbtn_Rectangle_55.png");

            Button single = MakeButton(canvasObject, "SinglePlayerButton", font, "PLAY ALONE",
                new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(420f, 76f), playPlate);

            Button host = MakeButton(canvasObject, "HostButton", font, "HOST A MATCH",
                new Vector2(0.5f, 0.5f), new Vector2(0f, -80f), new Vector2(420f, 76f), playPlate);

            Button join = MakeButton(canvasObject, "JoinButton", font, "JOIN",
                new Vector2(0.5f, 0.5f), new Vector2(-110f, -170f), new Vector2(200f, 76f), playPlate);

            InputField address = MakeAddressField(canvasObject, font);

            Toggle botsToggle = MakeToggle(canvasObject, font, "FillWithBotsToggle", "FILL WITH BOTS",
                new Vector2(-150f, 60f));

            Text botsLabel = MakeText(canvasObject, "BotsLabel", font, 20, TextAnchor.MiddleRight);
            Place(botsLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(90f, 60f),
                new Vector2(300f, 30f));

            InputField botCount = MakeNumberField(canvasObject, font);

            // Cycles rather than a row of buttons: the list comes from the build
            // settings at runtime, so the menu cannot know how many there will
            // be to lay out.
            // Below the join row, which is the last thing occupying the middle.
            // Everything above y=-208 is already taken by a button.
            Button mapButton = MakeButton(canvasObject, "MapButton", font, "MAP",
                new Vector2(0.5f, 0.5f), new Vector2(-150f, -250f), new Vector2(220f, 44f),
                Sprite("Modesbtns_Rectangle_55.png"));

            Text mapLabel = MakeText(canvasObject, "MapLabel", font, 20, TextAnchor.MiddleLeft);
            Place(mapLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(60f, -250f),
                new Vector2(360f, 30f));

            Button roundButton = MakeButton(canvasObject, "RoundButton", font, "ROUND",
                new Vector2(0.5f, 0.5f), new Vector2(-150f, -310f), new Vector2(220f, 44f),
                Sprite("Modesbtns_Rectangle_55.png"));

            Text roundLabel = MakeText(canvasObject, "RoundLabel", font, 20, TextAnchor.MiddleLeft);
            Place(roundLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(60f, -310f),
                new Vector2(360f, 30f));

            Button quit = MakeButton(canvasObject, "QuitButton", font, "QUIT",
                new Vector2(0.5f, 0f), new Vector2(0f, 70f), new Vector2(220f, 60f),
                Sprite("Quitbtn_Rectangle_56.png"));

            MenuController controller = canvasObject.AddComponent<MenuController>();

            // Loaded here, not before the scene was created. NewScene unloads
            // unused assets, and an asset held only by a local variable goes
            // with them — leaving a destroyed reference that assigns as null
            // without complaining. That shipped a menu whose buttons all
            // reported having no MatchSetup.
            MatchSetup setup = EnsureSetup();

            if (setup == null)
            {
                Debug.LogError(
                    $"No MatchSetup at {SetupPath}, so the menu will not be able to start anything.");
            }

            SerializedObject state = new SerializedObject(controller);
            state.FindProperty("_setup").objectReferenceValue = setup;
            state.FindProperty("_catcherButton").objectReferenceValue = catcher;
            state.FindProperty("_runnerButton").objectReferenceValue = runner;
            state.FindProperty("_roleLabel").objectReferenceValue = roleLabel;
            state.FindProperty("_singlePlayerButton").objectReferenceValue = single;
            state.FindProperty("_hostButton").objectReferenceValue = host;
            state.FindProperty("_joinButton").objectReferenceValue = join;
            state.FindProperty("_addressField").objectReferenceValue = address;
            state.FindProperty("_fillWithBotsToggle").objectReferenceValue = botsToggle;
            state.FindProperty("_botCountField").objectReferenceValue = botCount;
            state.FindProperty("_botsLabel").objectReferenceValue = botsLabel;
            state.FindProperty("_mapButton").objectReferenceValue = mapButton;
            state.FindProperty("_mapLabel").objectReferenceValue = mapLabel;
            state.FindProperty("_roundButton").objectReferenceValue = roundButton;
            state.FindProperty("_roundLabel").objectReferenceValue = roundLabel;
            state.FindProperty("_quitButton").objectReferenceValue = quit;
            state.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            AddToBuildSettings();

            Debug.Log("Baraf-Paani: rebuilt the main menu.");
        }

        /// <summary>
        /// Buttons do nothing without an event system, and a scene built from
        /// code has no reason to have picked one up.
        ///
        /// InputSystemUIInputModule, not StandaloneInputModule. The standalone
        /// one reads UnityEngine.Input, and this project is set to the new Input
        /// System only, where those calls throw — so the module never delivered a
        /// click and the whole menu was dead. Exactly the incompatibility that
        /// ruled out reusing the old minimap package.
        /// </summary>
        private static void BuildEventSystem()
        {
            GameObject events = new GameObject("EventSystem");
            events.AddComponent<UnityEngine.EventSystems.EventSystem>();

            InputSystemUIInputModule module = events.AddComponent<InputSystemUIInputModule>();

            // Added from code, so it has no actions bound. The defaults cover
            // pointer and navigation, which is all a menu needs.
            module.AssignDefaultActions();
        }

        /// <summary>
        /// The menu is only useful if it is the scene the game opens on, so it
        /// goes first in the build list and the game scene follows.
        /// </summary>
        private static void AddToBuildSettings()
        {
            List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(ScenePath, true)
            };

            // A joiner passes through this on the way in, so it has to be in the
            // build or JOIN would fail in a player and work in the editor.
            if (File.Exists(ConnectingSceneBuilder.ScenePath))
            {
                scenes.Add(new EditorBuildSettingsScene(ConnectingSceneBuilder.ScenePath, true));
            }

            // Every map, found on disk rather than listed here. A map added to
            // the builder's table and forgotten in a second list is a map the
            // menu offers and the build cannot load.
            foreach (string path in Directory.GetFiles(
                         "Assets/_Project/Scenes", "Game_*.unity"))
            {
                scenes.Add(new EditorBuildSettingsScene(path.Replace('\\', '/'), true));
            }

            EditorBuildSettings.scenes = scenes.ToArray();
        }

        /// <summary>
        /// Creates the shared setup asset if it is not there. Public because the
        /// game scene builder needs the same asset and either builder may run
        /// first.
        /// </summary>
        public static MatchSetup EnsureSetup()
        {
            MatchSetup existing = AssetDatabase.LoadAssetAtPath<MatchSetup>(SetupPath);

            if (existing != null)
            {
                return existing;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(SetupPath));

            MatchSetup setup = ScriptableObject.CreateInstance<MatchSetup>();
            AssetDatabase.CreateAsset(setup, SetupPath);
            AssetDatabase.SaveAssets();

            return AssetDatabase.LoadAssetAtPath<MatchSetup>(SetupPath);
        }

        private static Sprite Sprite(string file)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ArtRoot + file);

            if (sprite == null)
            {
                Debug.LogWarning($"Menu art missing: {ArtRoot + file}");
            }

            return sprite;
        }

        private static void BuildBackground(GameObject canvas)
        {
            GameObject background = new GameObject("Background");
            background.transform.SetParent(canvas.transform, false);

            RectTransform rect = background.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            Image image = background.AddComponent<Image>();
            Sprite art = Sprite("BackGrounds_BackGround_1.png");

            if (art != null)
            {
                image.sprite = art;
                image.preserveAspect = false;
            }
            else
            {
                image.color = new Color(0.07f, 0.08f, 0.1f);
            }

            image.raycastTarget = false;
        }

        private static Button MakeButton(
            GameObject canvas, string name, Font font, string label,
            Vector2 anchor, Vector2 position, Vector2 size, Sprite plate)
        {
            GameObject button = new GameObject(name);
            button.transform.SetParent(canvas.transform, false);

            RectTransform rect = button.AddComponent<RectTransform>();
            Place(rect, anchor, position, size);

            Image image = button.AddComponent<Image>();

            if (plate != null)
            {
                image.sprite = plate;
                image.type = Image.Type.Sliced;
            }
            else
            {
                image.color = new Color(0.15f, 0.17f, 0.22f, 0.95f);
            }

            Button control = button.AddComponent<Button>();
            control.targetGraphic = image;

            Text text = MakeText(button, "Label", font, 28, TextAnchor.MiddleCenter);
            RectTransform textRect = text.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            text.text = label;

            return control;
        }

        /// <summary>
        /// A checkbox. Built by hand because a Toggle needs its background and
        /// checkmark wiring, which nothing does for you from code.
        /// </summary>
        private static Toggle MakeToggle(
            GameObject canvas, Font font, string name, string label, Vector2 position)
        {
            GameObject root = new GameObject(name);
            root.transform.SetParent(canvas.transform, false);

            RectTransform rect = root.AddComponent<RectTransform>();
            Place(rect, new Vector2(0.5f, 0.5f), position, new Vector2(280f, 40f));

            GameObject box = new GameObject("Box");
            box.transform.SetParent(root.transform, false);

            RectTransform boxRect = box.AddComponent<RectTransform>();
            boxRect.anchorMin = new Vector2(0f, 0.5f);
            boxRect.anchorMax = new Vector2(0f, 0.5f);
            boxRect.pivot = new Vector2(0f, 0.5f);
            boxRect.anchoredPosition = new Vector2(0f, 0f);
            boxRect.sizeDelta = new Vector2(28f, 28f);

            Image background = box.AddComponent<Image>();
            background.color = new Color(0f, 0f, 0f, 0.6f);

            GameObject tick = new GameObject("Tick");
            tick.transform.SetParent(box.transform, false);

            RectTransform tickRect = tick.AddComponent<RectTransform>();
            tickRect.anchorMin = Vector2.zero;
            tickRect.anchorMax = Vector2.one;
            tickRect.offsetMin = new Vector2(5f, 5f);
            tickRect.offsetMax = new Vector2(-5f, -5f);

            Image tickImage = tick.AddComponent<Image>();
            tickImage.color = new Color(1f, 0.85f, 0.3f);

            Text caption = MakeText(root, "Label", font, 20, TextAnchor.MiddleLeft);
            RectTransform captionRect = caption.rectTransform;
            captionRect.anchorMin = new Vector2(0f, 0f);
            captionRect.anchorMax = new Vector2(1f, 1f);
            captionRect.offsetMin = new Vector2(38f, 0f);
            captionRect.offsetMax = Vector2.zero;
            caption.text = label;

            Toggle toggle = root.AddComponent<Toggle>();
            toggle.targetGraphic = background;
            toggle.graphic = tickImage;
            toggle.isOn = true;

            return toggle;
        }

        private static InputField MakeNumberField(GameObject canvas, Font font)
        {
            GameObject field = new GameObject("BotCountField");
            field.transform.SetParent(canvas.transform, false);

            RectTransform rect = field.AddComponent<RectTransform>();
            Place(rect, new Vector2(0.5f, 0.5f), new Vector2(280f, 60f), new Vector2(80f, 40f));

            Image image = field.AddComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.55f);

            Text text = MakeText(field, "Text", font, 22, TextAnchor.MiddleCenter);
            RectTransform textRect = text.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(8f, 4f);
            textRect.offsetMax = new Vector2(-8f, -4f);
            text.supportRichText = false;

            InputField input = field.AddComponent<InputField>();
            input.targetGraphic = image;
            input.textComponent = text;
            input.contentType = InputField.ContentType.IntegerNumber;
            input.characterLimit = 1;
            input.text = "3";

            return input;
        }

        private static InputField MakeAddressField(GameObject canvas, Font font)
        {
            GameObject field = new GameObject("AddressField");
            field.transform.SetParent(canvas.transform, false);

            RectTransform rect = field.AddComponent<RectTransform>();
            Place(rect, new Vector2(0.5f, 0.5f), new Vector2(120f, -170f), new Vector2(300f, 76f));

            Image image = field.AddComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.55f);

            Text text = MakeText(field, "Text", font, 24, TextAnchor.MiddleLeft);
            RectTransform textRect = text.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(14f, 6f);
            textRect.offsetMax = new Vector2(-14f, -6f);
            text.supportRichText = false;

            InputField input = field.AddComponent<InputField>();
            input.targetGraphic = image;
            input.textComponent = text;
            input.text = "localhost";

            return input;
        }

        private static Text MakeText(
            GameObject parent, string name, Font font, int size, TextAnchor anchor)
        {
            GameObject label = new GameObject(name);
            label.transform.SetParent(parent.transform, false);

            Text text = label.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.alignment = anchor;
            text.color = Color.white;
            text.raycastTarget = false;

            Outline outline = label.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.8f);
            outline.effectDistance = new Vector2(2f, -2f);

            return text;
        }

        private static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }
    }
}
