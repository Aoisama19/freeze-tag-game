using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace BarafPaani.EditorTools
{
    /// <summary>
    /// Synthesises the game's sound effects and writes them out as wav assets.
    ///
    /// Built rather than sourced, for the same reason the scene and the animator
    /// are built: what gets committed is a readable description of the thing.
    /// It also settles the licensing question outright. The old project's entire
    /// audio folder was a single hour-long rip of a commercial recording by a
    /// named musician, which is not a sound library and is not ours to ship.
    /// These are a few hundred lines of arithmetic and belong to nobody.
    ///
    /// The footsteps are the exception: those are Unity's own Starter Assets
    /// recordings, under the Unity Companion License, kept in Audio/Footsteps
    /// with the licence alongside them. Recorded footsteps are worth far more
    /// than anything that can be faked with a noise burst.
    /// </summary>
    public static class SoundBuilder
    {
        private const string Folder = "Assets/_Project/Audio/Generated";

        private const int SampleRate = 44100;

        [MenuItem("Baraf-Paani/Rebuild Sounds")]
        public static void Rebuild()
        {
            Directory.CreateDirectory(Folder);

            Write("Freeze", Freeze());
            Write("Thaw", Thaw());
            Write("Pickup", Pickup());
            Write("PowerUp", PowerUp());
            Write("RoundStart", RoundStart());
            Write("RoundWon", Arpeggio(new[] { 523.25f, 659.25f, 783.99f, 1046.5f }, 0.11f));
            Write("RoundLost", Arpeggio(new[] { 440f, 349.23f, 293.66f, 220f }, 0.14f));
            Write("Click", Click());

            AssetDatabase.Refresh();
            Debug.Log("Baraf-Paani: rebuilt the generated sounds.");
        }

        /// <summary>
        /// Being frozen: a bright strike that rings and falls away, with a
        /// scatter of noise under it for the crack.
        /// </summary>
        private static float[] Freeze()
        {
            float[] samples = Silence(0.75f);

            Tone(samples, 1318.51f, 0f, 0.7f, 0.32f, Decay.Fast);
            Tone(samples, 1975.53f, 0.01f, 0.55f, 0.26f, Decay.Fast);

            // A little detuning so the two do not sit on top of each other as
            // one clean interval, which reads as a doorbell rather than ice.
            Tone(samples, 2637.02f, 0.02f, 0.3f, 0.5f, Decay.Slow);
            Noise(samples, 0f, 0.09f, 0.22f);

            return Normalise(samples);
        }

        /// <summary>Being freed: the same shape, going up instead of down.</summary>
        private static float[] Thaw()
        {
            float[] samples = Silence(0.6f);

            Sweep(samples, 440f, 1174.66f, 0f, 0.35f, 0.4f);
            Tone(samples, 1567.98f, 0.3f, 0.25f, 0.3f, Decay.Slow);

            return Normalise(samples);
        }

        /// <summary>Walking over a power-up: two quick rising notes.</summary>
        private static float[] Pickup()
        {
            float[] samples = Silence(0.34f);

            Tone(samples, 880f, 0f, 0.11f, 0.4f, Decay.Fast);
            Tone(samples, 1318.51f, 0.09f, 0.2f, 0.4f, Decay.Fast);

            return Normalise(samples);
        }

        /// <summary>Spending one: a short sweep up, with some body to it.</summary>
        private static float[] PowerUp()
        {
            float[] samples = Silence(0.5f);

            Sweep(samples, 330f, 1108.73f, 0f, 0.3f, 0.45f);
            Sweep(samples, 165f, 554.37f, 0f, 0.3f, 0.25f);

            return Normalise(samples);
        }

        /// <summary>The round beginning: three notes, the last one held.</summary>
        private static float[] RoundStart()
        {
            float[] samples = Silence(1.1f);

            Tone(samples, 440f, 0f, 0.2f, 0.4f, Decay.Fast);
            Tone(samples, 554.37f, 0.22f, 0.2f, 0.4f, Decay.Fast);
            Tone(samples, 659.25f, 0.44f, 0.6f, 0.45f, Decay.Slow);

            return Normalise(samples);
        }

        private static float[] Arpeggio(float[] notes, float spacing)
        {
            float[] samples = Silence((notes.Length * spacing) + 0.6f);

            for (int i = 0; i < notes.Length; i++)
            {
                bool last = i == notes.Length - 1;

                Tone(
                    samples,
                    notes[i],
                    i * spacing,
                    last ? 0.6f : spacing + 0.1f,
                    0.4f,
                    last ? Decay.Slow : Decay.Fast);
            }

            return Normalise(samples);
        }

        /// <summary>A menu press. Deliberately almost nothing.</summary>
        private static float[] Click()
        {
            float[] samples = Silence(0.09f);

            Tone(samples, 1760f, 0f, 0.05f, 0.35f, Decay.Fast);
            Noise(samples, 0f, 0.012f, 0.12f);

            return Normalise(samples);
        }

        private enum Decay
        {
            Fast,
            Slow,
        }

        private static float[] Silence(float seconds)
        {
            return new float[Mathf.CeilToInt(seconds * SampleRate)];
        }

        /// <summary>
        /// A plucked note: a sine with a little of its own octave for bite, under
        /// an envelope that opens fast and falls away.
        /// </summary>
        private static void Tone(
            float[] samples, float hertz, float start, float length, float gain, Decay decay)
        {
            int from = Mathf.Clamp(Mathf.RoundToInt(start * SampleRate), 0, samples.Length);
            int count = Mathf.Min(Mathf.RoundToInt(length * SampleRate), samples.Length - from);
            float falloff = decay == Decay.Fast ? 9f : 3.2f;

            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SampleRate;
                float envelope = Attack(i) * Mathf.Exp(-falloff * t);

                float value =
                    Mathf.Sin(2f * Mathf.PI * hertz * t)
                    + (0.25f * Mathf.Sin(4f * Mathf.PI * hertz * t));

                samples[from + i] += value * envelope * gain;
            }
        }

        /// <summary>A note that slides from one pitch to another.</summary>
        private static void Sweep(
            float[] samples, float fromHertz, float toHertz, float start, float length, float gain)
        {
            int from = Mathf.Clamp(Mathf.RoundToInt(start * SampleRate), 0, samples.Length);
            int count = Mathf.Min(Mathf.RoundToInt(length * SampleRate), samples.Length - from);

            // Phase is accumulated rather than computed from the current
            // frequency, because doing the latter makes the waveform jump every
            // time the pitch moves and the sweep clicks its way up.
            float phase = 0f;

            for (int i = 0; i < count; i++)
            {
                float through = i / (float)count;
                float hertz = Mathf.Lerp(fromHertz, toHertz, through * through);

                phase += 2f * Mathf.PI * hertz / SampleRate;

                float envelope = Attack(i) * (1f - (through * through));

                samples[from + i] += Mathf.Sin(phase) * envelope * gain;
            }
        }

        private static void Noise(float[] samples, float start, float length, float gain)
        {
            // Seeded, so rebuilding produces the same file and the asset does
            // not show up as changed on every run.
            System.Random random = new System.Random(20260906);

            int from = Mathf.Clamp(Mathf.RoundToInt(start * SampleRate), 0, samples.Length);
            int count = Mathf.Min(Mathf.RoundToInt(length * SampleRate), samples.Length - from);
            float previous = 0f;

            for (int i = 0; i < count; i++)
            {
                float white = (float)((random.NextDouble() * 2d) - 1d);

                // One-pole low pass, so it is a scuff rather than a hiss.
                previous = Mathf.Lerp(previous, white, 0.35f);

                float envelope = Attack(i) * (1f - (i / (float)count));

                samples[from + i] += previous * envelope * gain;
            }
        }

        /// <summary>
        /// A couple of milliseconds of fade-in. Without it every sound starts on
        /// a vertical edge, which is an audible click on top of the note.
        /// </summary>
        private static float Attack(int sample)
        {
            const int Length = 64;

            return sample >= Length ? 1f : sample / (float)Length;
        }

        private static float[] Normalise(float[] samples)
        {
            float peak = 0f;

            foreach (float sample in samples)
            {
                peak = Mathf.Max(peak, Mathf.Abs(sample));
            }

            if (peak <= 0.0001f)
            {
                return samples;
            }

            // Short of full scale, so a couple of these landing together do not
            // clip against each other.
            float scale = 0.85f / peak;

            for (int i = 0; i < samples.Length; i++)
            {
                samples[i] *= scale;
            }

            return samples;
        }

        private static void Write(string name, float[] samples)
        {
            string path = $"{Folder}/{name}.wav";

            File.WriteAllBytes(path, Wav(samples));
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }

        /// <summary>
        /// A 16-bit mono PCM wav. Unity can create an AudioClip in memory but
        /// cannot save one, so the container is written by hand.
        /// </summary>
        private static byte[] Wav(float[] samples)
        {
            const int BytesPerSample = 2;
            const int Channels = 1;

            using MemoryStream stream = new MemoryStream();
            using BinaryWriter writer = new BinaryWriter(stream);

            int dataBytes = samples.Length * BytesPerSample;

            writer.Write(new[] { 'R', 'I', 'F', 'F' });
            writer.Write(36 + dataBytes);
            writer.Write(new[] { 'W', 'A', 'V', 'E' });

            writer.Write(new[] { 'f', 'm', 't', ' ' });
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)Channels);
            writer.Write(SampleRate);
            writer.Write(SampleRate * Channels * BytesPerSample);
            writer.Write((short)(Channels * BytesPerSample));
            writer.Write((short)(BytesPerSample * 8));

            writer.Write(new[] { 'd', 'a', 't', 'a' });
            writer.Write(dataBytes);

            foreach (float sample in samples)
            {
                writer.Write((short)(Mathf.Clamp(sample, -1f, 1f) * short.MaxValue));
            }

            writer.Flush();

            return stream.ToArray();
        }
    }
}
