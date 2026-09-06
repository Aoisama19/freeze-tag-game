using UnityEditor;
using UnityEngine;

namespace BarafPaani.EditorTools
{
    /// <summary>
    /// Reports the size and shape of a map prefab. Used to work out where a
    /// scene's spawn ring and NavMesh should sit without opening the editor and
    /// eyeballing it.
    /// </summary>
    public static class MapProbe
    {
        private static readonly string[] MapPrefabPaths =
        {
            "Assets/_Project/Art/Maps/3Talwaar/Prefab/3 Talwaar v4.prefab",
            "Assets/_Project/Art/Maps/BadshahiMasjid/Prefab/Badshahi Masjid v2.prefab",
            "Assets/_Project/Art/Maps/ClockTower/Prefab/Faislabad - Clock Tower v3.prefab",
        };

        /// <summary>
        /// Reports every map, so the numbers that go into the builder's table
        /// come from measuring rather than from guessing.
        /// </summary>
        [MenuItem("Baraf-Paani/Probe Maps")]
        public static void Report()
        {
            foreach (string path in MapPrefabPaths)
            {
                ReportOne(path);
            }
        }

        private static void ReportOne(string mapPrefabPath)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(mapPrefabPath);

            if (prefab == null)
            {
                Debug.LogError($"PROBE: no prefab at {mapPrefabPath}");
                return;
            }

            GameObject instance = Object.Instantiate(prefab);

            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>();
            Collider[] colliders = instance.GetComponentsInChildren<Collider>();

            if (renderers.Length == 0)
            {
                Debug.LogError("PROBE: map has no renderers");
                Object.DestroyImmediate(instance);
                return;
            }

            Bounds bounds = renderers[0].bounds;

            foreach (Renderer renderer in renderers)
            {
                bounds.Encapsulate(renderer.bounds);
            }

            Debug.Log(
                $"PROBE {System.IO.Path.GetFileNameWithoutExtension(mapPrefabPath)} " +
                $"renderers={renderers.Length} colliders={colliders.Length} " +
                $"centre=({bounds.center.x:F1},{bounds.center.y:F1},{bounds.center.z:F1}) " +
                $"size=({bounds.size.x:F1},{bounds.size.y:F1},{bounds.size.z:F1}) " +
                $"minY={bounds.min.y:F1} maxY={bounds.max.y:F1}");

            Object.DestroyImmediate(instance);
        }
    }
}
