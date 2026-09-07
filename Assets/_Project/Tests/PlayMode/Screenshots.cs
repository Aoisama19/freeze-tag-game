#if UNITY_EDITOR
using System.Collections;
using System.IO;
using BarafPaani.Core;
using BarafPaani.UI;
using Mirror;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace BarafPaani.Tests
{
    /// <summary>
    /// Takes the pictures the README uses.
    ///
    /// Skipped unless asked for. It asserts almost nothing — it is a tool that
    /// happens to need play mode, and play mode is the only place the game is
    /// actually itself: the HUD filled in, the characters spawned and coloured
    /// by their roles, the animation running. Rendering the same scenes from the
    /// editor gives a world with nobody in it and a HUD whose labels were never
    /// populated.
    ///
    /// It asks for itself rather than relying on NUnit's Explicit attribute,
    /// which Unity's runner ignores — marked that way it ran with the whole
    /// suite and rewrote the screenshots on every test run.
    ///
    /// Run it with:
    ///   -runTests -testPlatform PlayMode -captureScreenshots
    /// and without -nographics, since there is nothing to capture without a
    /// graphics device.
    /// </summary>
    public class Screenshots
    {
        /// <summary>Skips unless the command line asked for pictures.</summary>
        private static void OnlyWhenAsked()
        {
            foreach (string argument in System.Environment.GetCommandLineArgs())
            {
                if (argument == "-captureScreenshots")
                {
                    return;
                }
            }

            Assert.Ignore("Pass -captureScreenshots to take screenshots.");
        }

        private const string Folder = "docs/screenshots";

        /// <summary>
        /// The canvas scaler's reference resolution, exactly. Captured at any
        /// other size the UI is scaled after its text was laid out, and every
        /// label comes out smeared.
        /// </summary>
        private const int Width = 1920;

        private const int Height = 1080;

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

        [UnityTest]
        public IEnumerator Capture_the_menu()
        {
            OnlyWhenAsked();
            LogAssert.ignoreFailingMessages = true;

            SceneManager.LoadScene("MainMenu", LoadSceneMode.Single);
            yield return null;

            // Long enough for the pieces to have finished arriving, or the shot
            // catches the menu half way through fading in.
            yield return new WaitForSeconds(2f);

            Save("menu");
        }

        [UnityTest]
        public IEnumerator Capture_each_map()
        {
            OnlyWhenAsked();

            for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            {
                string map = Path.GetFileNameWithoutExtension(
                    SceneUtility.GetScenePathByBuildIndex(i));

                if (!map.StartsWith("Game_"))
                {
                    continue;
                }

                LogAssert.ignoreFailingMessages = true;

                SceneManager.LoadScene(map, LoadSceneMode.Single);
                yield return null;
                yield return null;

                Object.FindFirstObjectByType<GameNetworkManager>().StartSinglePlayer();

                // Long enough for the bots to have left their spawn points and
                // for spawn immunity to have worn off, so the shot is of a match
                // rather than of everyone standing still.
                yield return new WaitForSeconds(5f);

                Save(map.Replace("Game_", string.Empty).ToLowerInvariant());

                if (NetworkClient.active || NetworkServer.active)
                {
                    NetworkManager.singleton?.StopHost();
                }

                if (NetworkManager.singleton != null)
                {
                    Object.DestroyImmediate(NetworkManager.singleton.gameObject);
                }

                yield return null;
            }
        }

        /// <summary>
        /// Renders what the player is looking at, HUD included, at a size worth
        /// putting in a document.
        ///
        /// The canvas is flipped to camera space first. A screen space overlay
        /// canvas is drawn straight to the display and never appears in a
        /// camera's render, so captured as it stands the picture would be the
        /// world with no interface on it at all.
        /// </summary>
        private static void Save(string name)
        {
            Camera camera = Camera.main;
            Assert.IsNotNull(camera, "nothing to take a picture through");

            Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            RenderMode[] modes = new RenderMode[canvases.Length];

            for (int i = 0; i < canvases.Length; i++)
            {
                modes[i] = canvases[i].renderMode;
                canvases[i].renderMode = RenderMode.ScreenSpaceCamera;
                canvases[i].worldCamera = camera;
                canvases[i].planeDistance = 1f;
            }

            RenderTexture target = new RenderTexture(Width, Height, 24);
            RenderTexture previous = RenderTexture.active;

            camera.targetTexture = target;
            camera.Render();

            RenderTexture.active = target;

            Texture2D shot = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            shot.ReadPixels(new Rect(0f, 0f, Width, Height), 0, 0);
            shot.Apply();

            RenderTexture.active = previous;
            camera.targetTexture = null;

            // Put back, so nothing that runs after this inherits a canvas that
            // has quietly changed how it draws.
            for (int i = 0; i < canvases.Length; i++)
            {
                canvases[i].renderMode = modes[i];
            }

            Directory.CreateDirectory(Folder);
            File.WriteAllBytes($"{Folder}/{name}.png", shot.EncodeToPNG());

            Object.DestroyImmediate(shot);
            target.Release();
            Object.DestroyImmediate(target);

            Debug.Log($"Baraf-Paani: screenshot saved to {Folder}/{name}.png");
        }
    }
}
#endif
