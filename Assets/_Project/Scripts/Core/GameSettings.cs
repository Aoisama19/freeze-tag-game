using UnityEngine;

namespace BarafPaani.Core
{
    /// <summary>
    /// What the player has chosen about how the game sounds and handles.
    ///
    /// An asset for the same reason MatchSetup is one — it can be inspected and
    /// wired up, and there is no static mutable state carrying it about. The
    /// asset is only the accessor though: changes to a ScriptableObject do not
    /// survive a build, so the values themselves live in PlayerPrefs and are
    /// loaded into the asset on start.
    /// </summary>
    [CreateAssetMenu(menuName = "Baraf-Paani/Game Settings", fileName = "GameSettings")]
    public class GameSettings : ScriptableObject
    {
        private const string VolumeKey = "barafpaani.volume";

        private const string SensitivityKey = "barafpaani.sensitivity";

        public const float MinSensitivity = 0.25f;

        public const float MaxSensitivity = 3f;

        [SerializeField]
        [Range(0f, 1f)]
        private float _masterVolume = 0.8f;

        [SerializeField]
        [Tooltip("Multiplies how far the camera turns for a given movement of the mouse or stick.")]
        private float _lookSensitivity = 1f;

        public float MasterVolume => _masterVolume;

        public float LookSensitivity => _lookSensitivity;

        public void SetMasterVolume(float value)
        {
            _masterVolume = Mathf.Clamp01(value);
        }

        public void SetLookSensitivity(float value)
        {
            _lookSensitivity = Mathf.Clamp(value, MinSensitivity, MaxSensitivity);
        }

        /// <summary>
        /// Reads what was saved, falling back to whatever the asset already
        /// holds. A first run therefore starts on the defaults rather than on
        /// zero volume and a camera that will not turn.
        /// </summary>
        public void Load()
        {
            SetMasterVolume(PlayerPrefs.GetFloat(VolumeKey, _masterVolume));
            SetLookSensitivity(PlayerPrefs.GetFloat(SensitivityKey, _lookSensitivity));
        }

        public void Save()
        {
            PlayerPrefs.SetFloat(VolumeKey, _masterVolume);
            PlayerPrefs.SetFloat(SensitivityKey, _lookSensitivity);
            PlayerPrefs.Save();
        }
    }
}
