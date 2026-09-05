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
                new EditorBuildSettingsScene(ScenePath, true),
                new EditorBuildSettingsScene("Assets/_Project/Scenes/Game.unity", true)
            };

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
