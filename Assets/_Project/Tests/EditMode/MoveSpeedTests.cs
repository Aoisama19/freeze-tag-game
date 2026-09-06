using BarafPaani.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace BarafPaani.Tests
{
    public class MoveSpeedTests
    {
        private const float Teleport = 25f;

        [Test]
        public void Standing_still_is_no_speed()
        {
            Assert.AreEqual(
                0f, MoveSpeed.Measure(Vector3.zero, Vector3.zero, 0.02f, Teleport), 0.001f);
        }

        [Test]
        public void Walking_a_metre_in_a_quarter_second_is_four_metres_a_second()
        {
            float speed = MoveSpeed.Measure(
                Vector3.zero, new Vector3(1f, 0f, 0f), 0.25f, Teleport);

            Assert.AreEqual(4f, speed, 0.001f);
        }

        [Test]
        public void Falling_is_not_running()
        {
            // Straight down at speed, no ground covered.
            float speed = MoveSpeed.Measure(
                new Vector3(0f, 10f, 0f), Vector3.zero, 0.25f, Teleport);

            Assert.AreEqual(0f, speed, 0.001f);
        }

        [Test]
        public void A_teleport_back_to_spawn_does_not_read_as_a_sprint()
        {
            // What a round restart looks like: the far side of the arena, in one
            // frame. Taken literally this is 3000 m/s.
            float speed = MoveSpeed.Measure(
                Vector3.zero, new Vector3(60f, 0f, 0f), 0.02f, Teleport);

            Assert.AreEqual(0f, speed, "a teleport should be ignored, not animated");
        }

        [Test]
        public void A_paused_frame_is_not_a_division_by_zero()
        {
            float speed = MoveSpeed.Measure(Vector3.zero, new Vector3(1f, 0f, 0f), 0f, Teleport);

            Assert.AreEqual(0f, speed);
        }

        [Test]
        public void A_sprint_just_under_the_limit_still_counts()
        {
            float speed = MoveSpeed.Measure(
                Vector3.zero, new Vector3(0f, 0f, 0.15f), 0.02f, Teleport);

            Assert.AreEqual(7.5f, speed, 0.001f, "the sprint speed is real movement");
        }
    }
}
