using System.IO;
using BarafPaani.Core;
using BarafPaani.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace BarafPaani.EditorTools
{
    /// <summary>
    /// Builds the settings overlay, which two different screens need.
    ///
    /// Shared rather than built twice. The main menu and the in-game menu offer
    /// the same settings, and two copies of this laid out separately would drift
    /// the first time either one gained an option — the same reason the maps are
    /// generated from one table rather than four copies of a builder.
    ///
    /// Deliberately self-contained: it brings its own small layout helpers
    /// instead of borrowing the private ones from whichever builder is calling,
    /// so neither builder has to grow a hole for it.
    /// </summary>
    public static class SettingsUiBuilder
    {
        private const string SettingsPath = "Assets/_Project/Settings/GameSettings.asset";

        /// <summary>
        /// Adds the overlay and everything behind it to a canvas, opened by the
        /// button handed in.
        ///
        /// Built last so it is the last sibling, which in UGUI is what puts it in
        /// front of whatever it is covering.
        /// </summary>
        public static SettingsPanel Build(GameObject canvas, Font font, Button openButton)
        {
            GameSettings settings = EnsureSettings();

            // Explicit comparison rather than ??, which does not use
            // UnityEngine.Object's overloaded equality and so cannot tell a
            // missing component from a present one.
            SettingsApplier applier = canvas.GetComponent<SettingsApplier>();

            if (applier == null)
            {
                applier = canvas.AddComponent<SettingsApplier>();
            }

            SerializedObject applierState = new SerializedObject(applier);
            applierState.FindProperty("_settings").objectReferenceValue = settings;
            applierState.ApplyModifiedPropertiesWithoutUndo();

            GameObject panel = new GameObject("SettingsPanel");
            panel.transform.SetParent(canvas.transform, false);

            RectTransform panelRect = panel.AddComponent<RectTransform>();
            Stretch(panelRect);

            Image shade = panel.AddComponent<Image>();
            shade.color = new Color(0.02f, 0.03f, 0.06f, 0.9f);

            Text heading = MakeText(panel, "Heading", font, 48);
            Place(heading.rectTransform, new Vector2(0f, 210f), new Vector2(700f, 64f));
            heading.text = "SETTINGS";

            Text volumeLabel = MakeText(panel, "VolumeLabel", font, 26);
            Place(volumeLabel.rectTransform, new Vector2(0f, 104f), new Vector2(700f, 36f));

            Slider volume = MakeSlider(panel, "VolumeSlider", new Vector2(0f, 56f));

            Text lookLabel = MakeText(panel, "SensitivityLabel", font, 26);
            Place(lookLabel.rectTransform, new Vector2(0f, -26f), new Vector2(700f, 36f));

            Slider look = MakeSlider(panel, "SensitivitySlider", new Vector2(0f, -74f));

            Button close = MakeButton(panel, font, "CloseSettingsButton", "BACK",
                new Vector2(0f, -190f), new Vector2(240f, 64f));

            SettingsPanel screen = canvas.AddComponent<SettingsPanel>();

            SerializedObject state = new SerializedObject(screen);
            state.FindProperty("_settings").objectReferenceValue = settings;
            state.FindProperty("_applier").objectReferenceValue = applier;
            state.FindProperty("_panel").objectReferenceValue = panel;
            state.FindProperty("_openButton").objectReferenceValue = openButton;
            state.FindProperty("_closeButton").objectReferenceValue = close;
            state.FindProperty("_volumeSlider").objectReferenceValue = volume;
            state.FindProperty("_volumeLabel").objectReferenceValue = volumeLabel;
            state.FindProperty("_sensitivitySlider").objectReferenceValue = look;
            state.FindProperty("_sensitivityLabel").objectReferenceValue = lookLabel;
            state.ApplyModifiedPropertiesWithoutUndo();

            panel.SetActive(false);

            return screen;
        }

        /// <summary>Creates the settings asset the first time, as the setup asset is.</summary>
        public static GameSettings EnsureSettings()
        {
            GameSettings existing = AssetDatabase.LoadAssetAtPath<GameSettings>(SettingsPath);

            if (existing != null)
            {
                return existing;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));

            GameSettings created = ScriptableObject.CreateInstance<GameSettings>();
            AssetDatabase.CreateAsset(created, SettingsPath);
            AssetDatabase.SaveAssets();

            return created;
        }

        /// <summary>
        /// A slider, built by hand. Unity needs the fill and the handle wired to
        /// the component explicitly; nothing does it for you from code, and a
        /// slider missing either simply does not move.
        /// </summary>
        private static Slider MakeSlider(GameObject parent, string name, Vector2 position)
        {
            GameObject root = new GameObject(name);
            root.transform.SetParent(parent.transform, false);

            RectTransform rect = root.AddComponent<RectTransform>();
            Place(rect, position, new Vector2(620f, 28f));

            Image background = root.AddComponent<Image>();
            background.color = new Color(1f, 1f, 1f, 0.18f);

            GameObject fillArea = new GameObject("Fill Area");
            fillArea.transform.SetParent(root.transform, false);
            Stretch(fillArea.AddComponent<RectTransform>());

            GameObject fill = new GameObject("Fill");
            fill.transform.SetParent(fillArea.transform, false);
            RectTransform fillRect = fill.AddComponent<RectTransform>();
            Stretch(fillRect);

            Image fillImage = fill.AddComponent<Image>();
            fillImage.color = new Color(1f, 0.85f, 0.3f, 0.85f);

            GameObject handleArea = new GameObject("Handle Slide Area");
            handleArea.transform.SetParent(root.transform, false);
            Stretch(handleArea.AddComponent<RectTransform>());

            GameObject handle = new GameObject("Handle");
            handle.transform.SetParent(handleArea.transform, false);
            RectTransform handleRect = handle.AddComponent<RectTransform>();
            handleRect.sizeDelta = new Vector2(26f, 40f);

            Image handleImage = handle.AddComponent<Image>();
            handleImage.color = Color.white;

            Slider slider = root.AddComponent<Slider>();
            slider.direction = Slider.Direction.LeftToRight;
            slider.fillRect = fillRect;
            slider.handleRect = handleRect;
            slider.targetGraphic = handleImage;

            return slider;
        }

        private static Button MakeButton(
            GameObject parent, Font font, string name, string label, Vector2 position, Vector2 size)
        {
            GameObject button = new GameObject(name);
            button.transform.SetParent(parent.transform, false);

            RectTransform rect = button.AddComponent<RectTransform>();
            Place(rect, position, size);

            Image plate = button.AddComponent<Image>();
            plate.color = new Color(0.16f, 0.19f, 0.26f, 0.95f);

            Button control = button.AddComponent<Button>();
            control.targetGraphic = plate;

            Text text = MakeText(button, "Label", font, 24);
            Stretch(text.rectTransform);
            text.text = label;

            button.AddComponent<ButtonFeel>();

            return control;
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
