using BarafPaani.Core;
using UnityEngine;
using UnityEngine.UI;

namespace BarafPaani.UI
{
    /// <summary>
    /// The settings screen: how loud the game is, and how far the camera turns.
    ///
    /// Changes take effect as the slider moves rather than when the panel is
    /// closed. Volume in particular is impossible to set sensibly if you cannot
    /// hear the result until afterwards.
    ///
    /// Saved on closing, not on every frame of a drag — PlayerPrefs.Save writes
    /// to disk, and doing that a hundred times while somebody sweeps a slider is
    /// a hundred writes nobody asked for.
    /// </summary>
    public class SettingsPanel : MonoBehaviour
    {
        [SerializeField]
        private GameSettings _settings;

        [SerializeField]
        private SettingsApplier _applier;

        [SerializeField]
        private GameObject _panel;

        [SerializeField]
        private Button _openButton;

        [SerializeField]
        private Button _closeButton;

        [SerializeField]
        private Slider _volumeSlider;

        [SerializeField]
        private Text _volumeLabel;

        [SerializeField]
        private Slider _sensitivitySlider;

        [SerializeField]
        private Text _sensitivityLabel;

        /// <summary>Whether the overlay is currently covering what is behind it.</summary>
        public bool IsOpen => _panel != null && _panel.activeSelf;

        /// <summary>
        /// Shuts the overlay and writes the values down. Public so the in-game
        /// menu can close it when Escape is pressed over the top of it.
        /// </summary>
        public void CloseNow()
        {
            Close();
        }

        private void Start()
        {
            if (_settings != null)
            {
                _settings.Load();
            }

            Wire(_openButton, () => Show(true));
            Wire(_closeButton, Close);

            if (_volumeSlider != null)
            {
                _volumeSlider.minValue = 0f;
                _volumeSlider.maxValue = 1f;
                _volumeSlider.value = _settings != null ? _settings.MasterVolume : 0.8f;
                _volumeSlider.onValueChanged.RemoveAllListeners();
                _volumeSlider.onValueChanged.AddListener(OnVolumeChanged);
            }

            if (_sensitivitySlider != null)
            {
                _sensitivitySlider.minValue = GameSettings.MinSensitivity;
                _sensitivitySlider.maxValue = GameSettings.MaxSensitivity;
                _sensitivitySlider.value = _settings != null ? _settings.LookSensitivity : 1f;
                _sensitivitySlider.onValueChanged.RemoveAllListeners();
                _sensitivitySlider.onValueChanged.AddListener(OnSensitivityChanged);
            }

            ShowValues();
            Show(false);
        }

        private static void Wire(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }

        private void OnVolumeChanged(float value)
        {
            _settings?.SetMasterVolume(value);
            _applier?.Apply();
            ShowValues();
        }

        private void OnSensitivityChanged(float value)
        {
            _settings?.SetLookSensitivity(value);
            _applier?.Apply();
            ShowValues();
        }

        private void ShowValues()
        {
            if (_settings == null)
            {
                return;
            }

            if (_volumeLabel != null)
            {
                _volumeLabel.text = $"Volume    {Mathf.RoundToInt(_settings.MasterVolume * 100f)}%";
            }

            if (_sensitivityLabel != null)
            {
                _sensitivityLabel.text = $"Look sensitivity    {_settings.LookSensitivity:0.00}x";
            }
        }

        private void Close()
        {
            _settings?.Save();
            Show(false);
        }

        private void Show(bool open)
        {
            if (_panel != null)
            {
                _panel.SetActive(open);
            }
        }
    }
}
