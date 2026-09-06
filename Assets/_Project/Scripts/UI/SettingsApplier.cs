using System.Collections.Generic;
using BarafPaani.Core;
using Unity.Cinemachine;
using UnityEngine;

namespace BarafPaani.UI
{
    /// <summary>
    /// Makes the saved settings actually take effect in whatever scene this sits
    /// in.
    ///
    /// Volume is applied everywhere. Look sensitivity only means anything where
    /// there is a camera to turn, so it is applied when one is found and quietly
    /// skipped when there is not — the menu has no camera to be sensitive.
    /// </summary>
    public class SettingsApplier : MonoBehaviour
    {
        [SerializeField]
        private GameSettings _settings;

        [SerializeField]
        [Tooltip("Seconds between looking for a camera that has not appeared yet.")]
        private float _retryInterval = 0.5f;

        /// <summary>
        /// The gains the camera was built with, before any sensitivity was
        /// applied to them.
        ///
        /// Captured once and multiplied from, never multiplied into. Applying a
        /// setting on top of an already-adjusted value compounds every time the
        /// slider moves, which is the same trap the speed boost had to avoid.
        /// Cinemachine also does not give every axis the same gain, so a single
        /// remembered number would flatten the difference between them.
        /// </summary>
        private readonly List<float> _baseGains = new List<float>();

        private CinemachineInputAxisController _look;
        private float _nextLookFor;

        private void Start()
        {
            if (_settings == null)
            {
                Debug.LogWarning("No GameSettings, so nothing can be applied.", this);
                return;
            }

            _settings.Load();
            Apply();
        }

        private void Update()
        {
            // The camera controller belongs to the player, who is spawned after
            // this scene loads, so it is not there to be found on the first
            // frame. Looked for on a slow tick rather than every frame.
            if (_look != null || _settings == null || Time.time < _nextLookFor)
            {
                return;
            }

            _nextLookFor = Time.time + _retryInterval;

            if (FindLook())
            {
                ApplySensitivity();
            }
        }

        /// <summary>Pushes the current settings out. Called live as sliders move.</summary>
        public void Apply()
        {
            if (_settings == null)
            {
                return;
            }

            AudioListener.volume = _settings.MasterVolume;

            if (FindLook())
            {
                ApplySensitivity();
            }
        }

        private bool FindLook()
        {
            if (_look == null)
            {
                _look = FindFirstObjectByType<CinemachineInputAxisController>();
                _baseGains.Clear();
            }

            return _look != null;
        }

        private void ApplySensitivity()
        {
            // The axis list is discovered at runtime, so it can be empty on the
            // frame the component appears and filled in on the next.
            if (_look.Controllers.Count == 0)
            {
                return;
            }

            if (_baseGains.Count != _look.Controllers.Count)
            {
                _baseGains.Clear();

                foreach (CinemachineInputAxisController.Controller axis in _look.Controllers)
                {
                    _baseGains.Add(axis.Input.Gain);
                }
            }

            for (int i = 0; i < _look.Controllers.Count; i++)
            {
                _look.Controllers[i].Input.Gain = _baseGains[i] * _settings.LookSensitivity;
            }
        }
    }
}
