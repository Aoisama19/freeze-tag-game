using System.IO;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BarafPaani.EditorTools
{
    /// <summary>
    /// Renders a picture of each map for the menu to show.
    ///
    /// Taken from the game's own scenes rather than sourced. A stock photograph
    /// of a landmark is neither ours to ship nor a picture of anything in this
    /// game. A render of the actual arena is both.
    ///
    /// Must run with a graphics device, so no -nographics on the command line.
    /// A camera with nothing to render to produces a black png and no error.
    /// </summary>
    public static class MapPreviewBuilder
    {
        private const string Folder = "Assets/_Project/Art/UI/MapPreviews";

        private const int Width = 640;

        private const int Height = 360;

        [MenuItem("Baraf-Paani/Rebuild Map Previews")]
        public static void Rebuild()
        {
            Directory.CreateDirectory(Folder);

            int made = 0;

            foreach (string path in Directory.GetFiles(
                         "Assets/_Project/Scenes", "Game_*.unity"))
            {
                if (Capture(path.Replace('\\', '/')))
                {
                    made++;
                }
            }

            AssetDatabase.Refresh();
            Debug.Log($"Baraf-Paani: rendered {made} map previews.");
        }

        private static bool Capture(string scenePath)
        {
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            string name = Path.GetFileNameWithoutExtension(scenePath);

            // Where to point the camera is read from the scene rather than from
            // the builder's table, so a map whose arena moves gets a picture of
            // where the arena actually is.
            NavMeshSurface surface = Object.FindFirstObjectByType<NavMeshSurface>();

            if (surface == null)
            {
                Debug.LogWarning($"{name} has no NavMesh surface, so there is nothing to frame.");
                return false;
            }

            Vector3 centre = new Vector3(surface.center.x, 0f, surface.center.z);
            float size = Mathf.Max(surface.size.x, 20f);

            GameObject rig = new GameObject("PreviewCamera");
            Camera camera = rig.AddComponent<Camera>();

            // Well outside the arena and steeply down. Closer in, a single wall
            // or a courtyard floor fills the frame and the picture says nothing
            // about which map it is — which is exactly what the first attempt
            // at this produced.
            rig.transform.position = centre + new Vector3(size * 0.7f, size * 0.6f, -size * 0.7f);
            rig.transform.LookAt(centre);

            camera.fieldOfView = 55f;
            camera.farClipPlane = size * 4f;
            camera.clearFlags = CameraClearFlags.Skybox;

            RenderTexture target = new RenderTexture(Width, Height, 24);
            camera.targetTexture = target;
            camera.Render();

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;

            Texture2D shot = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            shot.ReadPixels(new Rect(0f, 0f, Width, Height), 0, 0);
            shot.Apply();

            RenderTexture.active = previous;
            camera.targetTexture = null;

            string file = $"{Folder}/{name}.png";
            File.WriteAllBytes(file, shot.EncodeToPNG());

            Object.DestroyImmediate(rig);
            Object.DestroyImmediate(shot);
            target.Release();
            Object.DestroyImmediate(target);

            // The scene was only opened to look at, so leave it as it was found.
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            AssetDatabase.ImportAsset(file, ImportAssetOptions.ForceUpdate);
            AsSprite(file);

            Debug.Log($"Baraf-Paani: preview rendered for {name}.");

            return true;
        }

        /// <summary>
        /// A png imported as a plain texture cannot be put in an Image, and the
        /// failure is a menu with an empty box where the map should be.
        /// </summary>
        private static void AsSprite(string file)
        {
            if (AssetImporter.GetAtPath(file) is not TextureImporter importer)
            {
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.SaveAndReimport();
        }
    }
}
