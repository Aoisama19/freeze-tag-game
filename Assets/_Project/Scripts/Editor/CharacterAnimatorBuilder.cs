using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace BarafPaani.EditorTools
{
    /// <summary>
    /// Builds the character's animator controller from code.
    ///
    /// Same reason the scene and prefabs are built this way: a .controller is
    /// unreadable YAML that nobody can review, so the thing worth committing is
    /// the description of it. One blend tree, one parameter — everything else a
    /// character does is decided by gameplay code, not by animator transitions,
    /// which is what keeps freeze in one place instead of spread across states.
    /// </summary>
    public static class CharacterAnimatorBuilder
    {
        public const string ControllerPath =
            "Assets/_Project/Art/Characters/Locomotion.controller";

        private const string ClipFolder = "Assets/_Project/Art/Characters/Animations";

        /// <summary>The one parameter: metres per second, measured off the transform.</summary>
        public const string SpeedParameter = "Speed";

        /// <summary>
        /// Fallbacks for how fast a clip carries the character forward, used
        /// when the imported clip does not report it. Roughly what the Unity
        /// humanoid walk and run cycles cover on their own.
        /// </summary>
        private const float WalkSpeedFallback = 1.6f;

        private const float RunSpeedFallback = 5.2f;

        [MenuItem("Baraf-Paani/Rebuild Character Animator")]
        public static void Rebuild()
        {
            AnimationClip idle = LoadClip("Humanoid_Idle");
            AnimationClip walk = LoadClip("Humanoid_Walk");
            AnimationClip run = LoadClip("Humanoid_Run");

            if (idle == null || walk == null || run == null)
            {
                Debug.LogError(
                    "Baraf-Paani: missing a locomotion clip, so the animator was not built.");
                return;
            }

            AnimatorController controller =
                AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

            controller.AddParameter(SpeedParameter, AnimatorControllerParameterType.Float);

            AnimatorState state = controller.CreateBlendTreeInController(
                "Locomotion", out BlendTree tree);

            tree.blendParameter = SpeedParameter;
            tree.blendType = BlendTreeType.Simple1D;

            // Thresholds are the speeds the clips themselves travel at, so the
            // legs turn over at roughly the rate the character is covering
            // ground. Guessing here is what produces skating.
            tree.useAutomaticThresholds = false;
            tree.children = new[]
            {
                Child(idle, 0f),
                Child(walk, ForwardSpeed(walk, WalkSpeedFallback)),
                Child(run, ForwardSpeed(run, RunSpeedFallback)),
            };

            state.speed = 1f;
            controller.layers[0].stateMachine.defaultState = state;

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            Debug.Log(
                "Baraf-Paani: rebuilt the character animator — walk at "
                + $"{ForwardSpeed(walk, WalkSpeedFallback):0.00} m/s, "
                + $"run at {ForwardSpeed(run, RunSpeedFallback):0.00} m/s.");
        }

        /// <summary>
        /// The fastest speed the blend tree can cover on its own — the run
        /// clip's own travel speed. Past this there is no quicker clip to blend
        /// to, so CharacterAnimation speeds the playback up instead. Read from
        /// the clip so the prefab and the controller cannot disagree.
        /// </summary>
        public static float TopClipSpeed()
        {
            AnimationClip run = LoadClip("Humanoid_Run");

            return run == null ? RunSpeedFallback : ForwardSpeed(run, RunSpeedFallback);
        }

        private static ChildMotion Child(AnimationClip clip, float threshold)
        {
            return new ChildMotion
            {
                motion = clip,
                threshold = threshold,
                timeScale = 1f,
                directBlendParameter = SpeedParameter,
            };
        }

        /// <summary>
        /// How fast the clip carries the character forward. Root motion is
        /// switched off on the animator — the motor and the agent do the moving
        /// — but the clip's own speed is still the honest threshold to blend at.
        /// </summary>
        private static float ForwardSpeed(AnimationClip clip, float fallback)
        {
            float speed = new Vector3(clip.averageSpeed.x, 0f, clip.averageSpeed.z).magnitude;

            return speed > 0.05f ? speed : fallback;
        }

        private static AnimationClip LoadClip(string fileName)
        {
            string path = $"{ClipFolder}/{fileName}.fbx";

            // The clip is a sub-asset of the model. Unity also keeps a hidden
            // __preview__ clip in there, which is not the one we want.
            return AssetDatabase.LoadAllAssetsAtPath(path)
                .OfType<AnimationClip>()
                .FirstOrDefault(clip => !clip.name.StartsWith("__preview__"));
        }
    }
}
