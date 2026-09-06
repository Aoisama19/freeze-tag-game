#if UNITY_EDITOR
using System.Collections;
using BarafPaani.Audio;
using BarafPaani.Core;
using BarafPaani.Gameplay;
using Mirror;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace BarafPaani.Tests
{
    /// <summary>
    /// That the game can actually make a noise: every character has a voice, the
    /// bank is filled in, and the round has something to announce itself with.
    ///
    /// Worth testing because the failure is silence, which looks exactly like
    /// working audio with the volume down.
    /// </summary>
    public class AudioTests
    {
        [TearDown]
        public void TearDown()
        {
            if (NetworkClient.active || NetworkServer.active)
            {
                NetworkManager.singleton?.StopHost();
            }

            if (NetworkManager.singleton != null)
            {
                Object.DestroyImmediate(NetworkManager.singleton.gameObject);
            }
        }

        private static IEnumerator StartMatch()
        {
            LogAssert.ignoreFailingMessages = true;

            SceneManager.LoadScene("Game_3Talwaar", LoadSceneMode.Single);
            yield return null;
            yield return null;

            Object.FindFirstObjectByType<GameNetworkManager>().StartSinglePlayer();
            yield return new WaitForSeconds(1.5f);
        }

        [UnityTest]
        public IEnumerator Every_character_has_a_voice()
        {
            yield return StartMatch();

            int checked_ = 0;

            foreach (NetworkIdentity identity in NetworkServer.spawned.Values)
            {
                if (identity == null || identity.GetComponent<PlayerRole>() == null)
                {
                    continue;
                }

                CharacterAudio audio = identity.GetComponent<CharacterAudio>();
                Assert.IsNotNull(audio, $"{identity.name} makes no sound at all");

                AudioSource source = identity.GetComponent<AudioSource>();
                Assert.IsNotNull(source, $"{identity.name} has nothing to play through");

                Assert.IsFalse(
                    source.playOnAwake, $"{identity.name} should not start making noise on spawn");

                Assert.AreEqual(
                    1f,
                    source.spatialBlend,
                    $"{identity.name} should be heard in the world, not flat in your ears");

                checked_++;
            }

            Assert.Greater(checked_, 0, "no characters were found to check");
        }

        [UnityTest]
        public IEnumerator The_sound_bank_has_every_clip_in_it()
        {
            yield return StartMatch();

            CharacterAudio audio = Object.FindFirstObjectByType<CharacterAudio>();
            Assert.IsNotNull(audio);

            SoundBank bank = (SoundBank)typeof(CharacterAudio)
                .GetField("_sounds", System.Reflection.BindingFlags.NonPublic
                                     | System.Reflection.BindingFlags.Instance)
                .GetValue(audio);

            Assert.IsNotNull(bank, "the characters have no sound bank");

            Assert.IsNotNull(bank.Freeze, "no freeze sound");
            Assert.IsNotNull(bank.Thaw, "no thaw sound");
            Assert.IsNotNull(bank.Pickup, "no pickup sound");
            Assert.IsNotNull(bank.PowerUp, "no power-up sound");
            Assert.IsNotNull(bank.RoundStart, "no round start sound");
            Assert.IsNotNull(bank.RoundWon, "no win sound");
            Assert.IsNotNull(bank.RoundLost, "no loss sound");
            Assert.IsNotNull(bank.Click, "no menu click");
            Assert.IsNotNull(bank.Land, "no landing sound");
            Assert.IsNotNull(bank.Footstep(), "no footsteps");
        }

        [UnityTest]
        public IEnumerator The_round_has_something_to_announce_itself_with()
        {
            yield return StartMatch();

            MatchAudio match = Object.FindFirstObjectByType<MatchAudio>();
            Assert.IsNotNull(match, "the round makes no sound");

            AudioSource source = match.GetComponent<AudioSource>();
            Assert.IsNotNull(source);

            Assert.AreEqual(
                0f,
                source.spatialBlend,
                "a round starting is an announcement, not a thing in the street");
        }

        [UnityTest]
        public IEnumerator Something_is_listening()
        {
            yield return StartMatch();

            Assert.IsNotNull(
                Object.FindFirstObjectByType<AudioListener>(),
                "without a listener the whole game is silent");
        }

        [UnityTest]
        public IEnumerator Spawning_does_not_make_a_thawing_noise()
        {
            // Freezable applies its state once on spawn. Treated as a change,
            // every character would chime the moment it appeared.
            //
            // Checked on the human, who stands still without input. The bots are
            // running by now and their sources are legitimately busy with
            // footsteps, so "nothing is playing" says nothing about them.
            yield return StartMatch();

            NetworkIdentity me = NetworkClient.localPlayer;
            Assert.IsNotNull(me, "no local player to listen to");

            AudioSource source = me.GetComponent<AudioSource>();
            Assert.IsNotNull(source);

            Assert.IsFalse(
                source.isPlaying, "a character standing still should be making no noise at all");
        }

        [UnityTest]
        public IEnumerator Being_frozen_is_audible()
        {
            // The other half: a real change does chime, so the guard above has
            // not simply switched the sound off.
            yield return StartMatch();

            NetworkIdentity me = NetworkClient.localPlayer;
            AudioSource source = me.GetComponent<AudioSource>();

            Assert.IsFalse(source.isPlaying, "nothing should be playing yet");

            Freezable freezable = me.GetComponent<Freezable>();

            // Immunity cleared first: everyone is safe for a few seconds at the
            // start of a round, so a freeze here would be refused for a reason
            // that has nothing to do with what this test is about.
            freezable.ClearImmunity();
            freezable.Freeze();
            yield return null;

            Assert.IsTrue(source.isPlaying, "freezing should have been heard");
        }

    }
}
#endif
