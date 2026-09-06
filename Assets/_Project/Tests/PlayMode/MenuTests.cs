#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BarafPaani.Core;
using BarafPaani.Gameplay;
using BarafPaani.UI;
using Mirror;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace BarafPaani.Tests
{
    /// <summary>
    /// The menu, which nothing covered until it shipped broken twice — once with
    /// an input module that could not deliver a click, and once with a null
    /// MatchSetup so every play button only logged an error.
    ///
    /// Both were invisible to every other test, because those load the game
    /// scene directly and start their own hosts. These check the menu is wired
    /// and can actually be pressed.
    /// </summary>
    public class MenuTests
    {
        [TearDown]
        public void TearDown()
        {
            if (NetworkClient.active || NetworkServer.active)
            {
                NetworkManager.singleton?.StopHost();
            }

            if (NetworkManager.singleton != null)
            {
                Object.DestroyImmediate(NetworkManager.singleton.gameObject);
            }
        }

        private static IEnumerator LoadMenu()
        {
            LogAssert.ignoreFailingMessages = true;

            SceneManager.LoadScene("MainMenu", LoadSceneMode.Single);
            yield return null;
            yield return null;
        }

        private static T Field<T>(MenuController menu, string name) where T : Object
        {
            FieldInfo field = typeof(MenuController)
                .GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.IsNotNull(field, $"{name} has been renamed");
            return field.GetValue(menu) as T;
        }

        [UnityTest]
        public IEnumerator The_menu_can_take_a_click()
        {
            yield return LoadMenu();

            EventSystem events = Object.FindFirstObjectByType<EventSystem>();
            Assert.IsNotNull(events, "no EventSystem, so nothing is clickable");

            // The legacy module reads UnityEngine.Input, which throws under the
            // new Input System. Having one is the same as having none.
            Assert.IsNotNull(
                events.GetComponent<InputSystemUIInputModule>(),
                "the event system needs the Input System's module, not the legacy one");

            Assert.IsNull(
                events.GetComponent<StandaloneInputModule>(),
                "StandaloneInputModule cannot deliver clicks in this project");

            Assert.IsNotNull(
                Object.FindFirstObjectByType<GraphicRaycaster>(),
                "the canvas needs a raycaster or clicks hit nothing");
        }

        [UnityTest]
        public IEnumerator Every_menu_button_is_wired_up()
        {
            yield return LoadMenu();

            MenuController menu = Object.FindFirstObjectByType<MenuController>();
            Assert.IsNotNull(menu, "the menu scene has no MenuController");

            // The one that was null, and the reason every play button only
            // managed to log an error.
            Assert.IsNotNull(
                Field<MatchSetup>(menu, "_setup"),
                "the menu has no MatchSetup, so it cannot say what to start");

            foreach (string name in new[]
                     {
                         "_catcherButton", "_runnerButton", "_singlePlayerButton",
                         "_hostButton", "_joinButton", "_quitButton"
                     })
            {
                Assert.IsNotNull(Field<Button>(menu, name), $"{name} is not wired up");
            }

            Assert.IsNotNull(Field<Toggle>(menu, "_fillWithBotsToggle"), "no bot toggle");
            Assert.IsNotNull(Field<InputField>(menu, "_botCountField"), "no bot count field");
        }

        [UnityTest]
        public IEnumerator Turning_bots_off_is_carried_into_the_match()
        {
            yield return LoadMenu();

            MenuController menu = Object.FindFirstObjectByType<MenuController>();
            MatchSetup setup = Field<MatchSetup>(menu, "_setup");

            Field<Toggle>(menu, "_fillWithBotsToggle").isOn = false;
            Field<Button>(menu, "_hostButton").onClick.Invoke();

            Assert.IsFalse(setup.FillWithBots, "the match should have been told to use no bots");
            Assert.AreEqual(GameMode.Multiplayer, setup.Mode);

            setup.ClearRequest();
        }

        [UnityTest]
        public IEnumerator A_bot_count_over_the_limit_is_pulled_back()
        {
            yield return LoadMenu();

            MenuController menu = Object.FindFirstObjectByType<MenuController>();
            MatchSetup setup = Field<MatchSetup>(menu, "_setup");

            Field<Toggle>(menu, "_fillWithBotsToggle").isOn = true;
            Field<InputField>(menu, "_botCountField").text = "99";
            Field<Button>(menu, "_singlePlayerButton").onClick.Invoke();

            Assert.AreEqual(
                MatchSetup.MaxBotRunners,
                setup.BotRunners,
                "a number past the limit should be clamped, not taken at face value");

            setup.ClearRequest();
        }

        [UnityTest]
        public IEnumerator Choosing_a_side_and_playing_records_it()
        {
            yield return LoadMenu();

            MenuController menu = Object.FindFirstObjectByType<MenuController>();
            MatchSetup setup = Field<MatchSetup>(menu, "_setup");
            Assert.IsNotNull(setup);

            Field<Button>(menu, "_runnerButton").onClick.Invoke();
            Field<Button>(menu, "_singlePlayerButton").onClick.Invoke();

            Assert.IsTrue(setup.LaunchRequested, "pressing play should have asked for a match");
            Assert.AreEqual(GameMode.SinglePlayer, setup.Mode);
            Assert.AreEqual(Role.Runner, setup.HumanRole, "the chosen side should have stuck");

            // Leave nothing behind for the next test to trip over.
            setup.ClearRequest();
        }

        [UnityTest]
        public IEnumerator No_two_controls_sit_on_top_of_each_other()
        {
            // The layout is written as hand-placed coordinates, and a control
            // added into what looked like a free gap has landed on another one
            // before now. Overlapping controls do not throw or log anything —
            // one of them simply stops being clickable, and only in the corner
            // where they cross.
            yield return LoadMenu();

            List<(string name, Rect rect)> controls = new List<(string, Rect)>();

            foreach (Selectable control in
                     Object.FindObjectsByType<Selectable>(FindObjectsSortMode.None))
            {
                RectTransform rect = control.GetComponent<RectTransform>();

                if (rect != null)
                {
                    controls.Add((control.name, ScreenRect(rect)));
                }
            }

            Assert.Greater(controls.Count, 4, "the menu has almost no controls in it");

            for (int i = 0; i < controls.Count; i++)
            {
                for (int j = i + 1; j < controls.Count; j++)
                {
                    // Children of another control are meant to sit inside it.
                    if (controls[i].rect.Overlaps(controls[j].rect))
                    {
                        Assert.Fail(
                            $"{controls[i].name} and {controls[j].name} overlap: "
                            + $"{controls[i].rect} vs {controls[j].rect}");
                    }
                }
            }
        }

        [UnityTest]
        public IEnumerator Every_control_is_on_the_screen()
        {
            // A control placed off the edge is as good as missing, and nothing
            // reports it.
            yield return LoadMenu();

            Rect screen = new Rect(0f, 0f, Screen.width, Screen.height);

            foreach (Selectable control in
                     Object.FindObjectsByType<Selectable>(FindObjectsSortMode.None))
            {
                RectTransform rect = control.GetComponent<RectTransform>();

                if (rect == null)
                {
                    continue;
                }

                Rect where = ScreenRect(rect);

                Assert.IsTrue(
                    screen.Overlaps(where),
                    $"{control.name} is off the screen at {where}");
            }
        }

        [UnityTest]
        public IEnumerator The_chosen_map_has_a_picture()
        {
            yield return LoadMenu();

            MenuController menu = Object.FindFirstObjectByType<MenuController>();
            Image preview = Field<Image>(menu, "_mapPreview");

            Assert.IsNotNull(preview, "the menu has nowhere to show a map");
            Assert.IsNotNull(preview.sprite, "no picture for the map it opened on");
            Assert.IsTrue(preview.enabled, "the picture is there but not being drawn");
        }

        private static Rect ScreenRect(RectTransform rect)
        {
            Vector3[] corners = new Vector3[4];
            rect.GetWorldCorners(corners);

            return new Rect(
                corners[0].x,
                corners[0].y,
                corners[2].x - corners[0].x,
                corners[2].y - corners[0].y);
        }
    }
}
#endif
