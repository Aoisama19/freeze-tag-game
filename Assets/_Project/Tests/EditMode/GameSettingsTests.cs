using BarafPaani.Core;
using NUnit.Framework;
using UnityEngine;

namespace BarafPaani.Tests
{
    /// <summary>
    /// The settings hold sane values whatever they are handed.
    ///
    /// Worth checking because these come from sliders and from PlayerPrefs, and
    /// PlayerPrefs is a file on disk that anybody can edit. A negative
    /// sensitivity would invert the camera; a volume above one would clip.
    /// </summary>
    public class GameSettingsTests
    {
        private GameSettings _settings;

        [SetUp]
        public void SetUp()
        {
            _settings = ScriptableObject.CreateInstance<GameSettings>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_settings);
        }

        [Test]
        public void Volume_cannot_be_pushed_above_full()
        {
            _settings.SetMasterVolume(4f);

            Assert.AreEqual(1f, _settings.MasterVolume);
        }

        [Test]
        public void Volume_cannot_be_pushed_below_silence()
        {
            _settings.SetMasterVolume(-2f);

            Assert.AreEqual(0f, _settings.MasterVolume);
        }

        [Test]
        public void Silence_is_allowed()
        {
            // Nought is a legitimate choice, not an error to be corrected.
            _settings.SetMasterVolume(0f);

            Assert.AreEqual(0f, _settings.MasterVolume);
        }

        [Test]
        public void Sensitivity_cannot_be_turned_down_to_nothing()
        {
            // Zero would be a camera that will not turn at all, and no way back
            // from it except editing the file by hand.
            _settings.SetLookSensitivity(0f);

            Assert.AreEqual(GameSettings.MinSensitivity, _settings.LookSensitivity);
        }

        [Test]
        public void Sensitivity_cannot_be_inverted()
        {
            // A negative gain flips the camera, which is a feature somebody
            // might want one day but not one they can ask for by accident.
            _settings.SetLookSensitivity(-3f);

            Assert.AreEqual(GameSettings.MinSensitivity, _settings.LookSensitivity);
        }

        [Test]
        public void Sensitivity_has_a_ceiling()
        {
            _settings.SetLookSensitivity(999f);

            Assert.AreEqual(GameSettings.MaxSensitivity, _settings.LookSensitivity);
        }

        [Test]
        public void A_sensible_value_is_left_alone()
        {
            _settings.SetMasterVolume(0.42f);
            _settings.SetLookSensitivity(1.75f);

            Assert.AreEqual(0.42f, _settings.MasterVolume, 0.0001f);
            Assert.AreEqual(1.75f, _settings.LookSensitivity, 0.0001f);
        }
    }
}
