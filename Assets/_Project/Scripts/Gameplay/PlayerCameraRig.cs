using Mirror;
using Unity.Cinemachine;
using UnityEngine;

namespace BarafPaani.Gameplay
{
    /// <summary>
    /// Hands the scene's Cinemachine camera to whichever character belongs to
    /// this client. Each client follows its own player, so this is local-only.
    ///
    /// The follow target is an explicit reference rather than a child index. The
    /// old build addressed a camera target with transform.GetChild(2), which
    /// silently grabbed the wrong object whenever the prefab hierarchy changed.
    /// </summary>
    public class PlayerCameraRig : NetworkBehaviour
    {
        [SerializeField]
        [Tooltip("Usually a node at head height. Falls back to the character root.")]
        private Transform _followTarget;

        public override void OnStartLocalPlayer()
        {
            CinemachineCamera sceneCamera = FindFirstObjectByType<CinemachineCamera>();

            if (sceneCamera == null)
            {
                Debug.LogError(
                    "No CinemachineCamera in the scene, so the local player has no camera following it.", this);
                return;
            }

            Transform target = _followTarget != null ? _followTarget : transform;
            sceneCamera.Follow = target;
            sceneCamera.LookAt = target;
        }
    }
}
