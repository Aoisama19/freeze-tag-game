using System.IO;
using UnityEditor;
using UnityEngine;

namespace BarafPaani.EditorTools
{
    /// <summary>
    /// Generates the minimap's circle and ring sprites.
    ///
    /// Drawn in code rather than imported, so there is no binary in the repo to
    /// go stale and the shapes stay crisp at whatever size we settle on. Both
    /// are plain white, so the UI can tint them per role without needing a
    /// sprite per colour.
    /// </summary>
    public static class MinimapSprites
    {
        public const string CirclePath = "Assets/_Project/Art/UI/MinimapCircle.png";
        public const string RingPath = "Assets/_Project/Art/UI/MinimapRing.png";

        private const int Resolution = 256;

        /// <summary>Filled disc, used for the map mask and for blips.</summary>
        public static Sprite EnsureCircle()
        {
            return EnsureSprite(CirclePath, innerRadius: 0f);
        }

        /// <summary>Hollow ring, used as the frame around the map.</summary>
        public static Sprite EnsureRing()
        {
            return EnsureSprite(RingPath, innerRadius: 0.88f);
        }

        private static Sprite EnsureSprite(string path, float innerRadius)
        {
            Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);

            if (existing != null)
            {
                return existing;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(path));

            Texture2D texture = Draw(innerRadius);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            ConfigureAsSprite(path);

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        /// <summary>
        /// Draws a disc, or an annulus when <paramref name="innerRadius"/> is
        /// above zero. Edges are feathered over roughly a pixel so the shape
        /// does not look like a staircase when scaled down.
        /// </summary>
        private static Texture2D Draw(float innerRadius)
        {
            Texture2D texture = new Texture2D(Resolution, Resolution, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[Resolution * Resolution];

            float centre = (Resolution - 1) * 0.5f;
            float outerRadius = centre;
            float feather = 1.5f;

            for (int y = 0; y < Resolution; y++)
            {
                for (int x = 0; x < Resolution; x++)
                {
                    float distance = Mathf.Sqrt(
                        ((x - centre) * (x - centre)) + ((y - centre) * (y - centre)));

                    float alpha = Mathf.Clamp01((outerRadius - distance) / feather);

                    if (innerRadius > 0f)
                    {
                        float inner = outerRadius * innerRadius;
                        alpha = Mathf.Min(alpha, Mathf.Clamp01((distance - inner) / feather));
                    }

                    pixels[(y * Resolution) + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        private static void ConfigureAsSprite(string path)
        {
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
            {
                Debug.LogError($"Could not configure {path} as a sprite.");
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();
        }
    }
}
