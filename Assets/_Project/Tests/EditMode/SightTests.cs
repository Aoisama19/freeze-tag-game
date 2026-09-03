using BarafPaani.AI;
using NUnit.Framework;
using UnityEngine;

namespace BarafPaani.Tests
{
    /// <summary>
    /// The view-cone maths. Issue 2 in docs/old-build-issues.md was the caller
    /// misusing its result, but the old FieldOfView also hard-coded its radius
    /// and angle in Start() after exposing them as inspector fields, so the
    /// numbers on screen were a lie. Keeping the geometry honest and tested.
    /// </summary>
    public class SightTests
    {
        private const float Radius = 10f;
        private const float FieldOfView = 90f;

        [Test]
        public void Sees_a_target_straight_ahead()
        {
            Assert.IsTrue(Sight.InViewCone(
                Vector3.zero, Vector3.forward, new Vector3(0f, 0f, 5f), Radius, FieldOfView));
        }

        [Test]
        public void Does_not_see_a_target_behind()
        {
            Assert.IsFalse(Sight.InViewCone(
                Vector3.zero, Vector3.forward, new Vector3(0f, 0f, -5f), Radius, FieldOfView));
        }

        [Test]
        public void Does_not_see_a_target_beyond_the_radius()
        {
            Assert.IsFalse(Sight.InViewCone(
                Vector3.zero, Vector3.forward, new Vector3(0f, 0f, 25f), Radius, FieldOfView));
        }

        [Test]
        public void Sees_a_target_at_the_edge_of_the_cone()
        {
            // 45 degrees off forward, which is exactly half of a 90 degree cone.
            Vector3 edge = new Vector3(5f, 0f, 5f);

            Assert.IsTrue(Sight.InViewCone(
                Vector3.zero, Vector3.forward, edge, Radius, FieldOfView));
        }

        [Test]
        public void Does_not_see_a_target_just_outside_the_cone()
        {
            // ~63 degrees off forward, outside a 90 degree cone.
            Vector3 outside = new Vector3(5f, 0f, 2.5f);

            Assert.IsFalse(Sight.InViewCone(
                Vector3.zero, Vector3.forward, outside, Radius, FieldOfView));
        }

        [Test]
        public void Height_does_not_affect_visibility()
        {
            // Catchers and runners stand on the same ground; a target's height
            // should not push it out of the cone.
            Assert.IsTrue(Sight.InViewCone(
                Vector3.zero, Vector3.forward, new Vector3(0f, 3f, 5f), Radius, FieldOfView));
        }

        [Test]
        public void Standing_on_the_target_counts_as_seeing_it()
        {
            Assert.IsTrue(Sight.InViewCone(
                Vector3.zero, Vector3.forward, Vector3.zero, Radius, FieldOfView));
        }

        [Test]
        public void A_zero_facing_sees_nothing()
        {
            Assert.IsFalse(Sight.InViewCone(
                Vector3.zero, Vector3.zero, new Vector3(0f, 0f, 5f), Radius, FieldOfView));
        }
    }
}
