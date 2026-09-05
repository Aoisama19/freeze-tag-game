using Mirror;
using UnityEngine;

namespace BarafPaani.UI
{
    /// <summary>
    /// Keeps the minimap camera over whoever is playing on this client, so the
    /// map shows the neighbourhood around you rather than the whole arena.
    ///
    /// North stays up. A map that rotates with the player is arguably more
    /// natural to read while running, but it makes fixed landmarks move, which
    /// is worse for learning a map you are meant to get to know. Easy to change
    /// if it feels wrong in play.
    /// </summary>
    public class MinimapCameraRig : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Metres above the player. Needs to clear the tallest building.")]
        private float _height = 80f;

        private void LateUpdate()
        {
            NetworkIdentity local = NetworkClient.localPlayer;

            if (local == null)
            {
                return;
            }

            // Relative to the player's own height, so the camera stays the same
            // distance up wherever the ground happens to sit.
            Vector3 position = local.transform.position;
            transform.position = new Vector3(position.x, position.y + _height, position.z);
        }
    }
}
