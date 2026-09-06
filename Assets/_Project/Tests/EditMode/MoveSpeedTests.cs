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

        [Test]
        public void Walking_plays_the_animation_at_its_own_pace()
        {
            Assert.AreEqual(1f, MoveSpeed.PlaybackRate(2f, 5.66f, 1.5f), 0.001f);
        }

        [Test]
        public void Running_at_exactly_the_clip_speed_is_left_alone()
        {
            Assert.AreEqual(1f, MoveSpeed.PlaybackRate(5.66f, 5.66f, 1.5f), 0.001f);
        }

        [Test]
        public void Sprinting_past_the_clip_speeds_the_legs_up_to_match()
        {
            // The sprint speed against the run clip's own travel speed: without
            // this the legs would turn over at 5.66 m/s while the ground went
            // past at 7.5, which is what skating looks like.
            float rate = MoveSpeed.PlaybackRate(7.5f, 5.66f, 1.5f);

            Assert.AreEqual(7.5f / 5.66f, rate, 0.001f);
        }

        [Test]
        public void The_speed_up_is_capped_short_of_looking_silly()
        {
            Assert.AreEqual(1.5f, MoveSpeed.PlaybackRate(40f, 5.66f, 1.5f), 0.001f);
        }

        [Test]
        public void An_unset_clip_speed_never_slows_the_animation_to_a_crawl()
        {
            // A prefab built before the clip speed was measured would carry
            // zero here, and dividing by it would stop the animation dead.
            Assert.AreEqual(1f, MoveSpeed.PlaybackRate(7.5f, 0f, 1.5f), 0.001f);
        }
    }
}
