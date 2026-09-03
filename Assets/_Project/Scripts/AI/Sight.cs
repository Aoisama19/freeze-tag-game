using UnityEngine;

namespace BarafPaani.AI
{
    /// <summary>
    /// The geometry half of seeing something: is a point inside a view cone.
    /// Pure, so the maths can be tested without a scene or a physics query.
    /// </summary>
    public static class Sight
    {
        /// <summary>
        /// True if <paramref name="target"/> is within <paramref name="radius"/>
        /// and inside a cone of <paramref name="fieldOfViewDegrees"/> centred on
        /// <paramref name="forward"/>. The angle is the full width of the cone,
        /// not the half-angle, because that is what an inspector field labelled
        /// "field of view" is expected to mean.
        /// </summary>
        public static bool InViewCone(
            Vector3 eye,
            Vector3 forward,
            Vector3 target,
            float radius,
            float fieldOfViewDegrees)
        {
            Vector3 toTarget = target - eye;
            toTarget.y = 0f;

            if (toTarget.sqrMagnitude > radius * radius)
            {
                return false;
            }

            // Standing exactly on the target counts as seeing it; otherwise the
            // angle is undefined and Vector3.Angle would return garbage.
            if (toTarget.sqrMagnitude < 0.0001f)
            {
                return true;
            }

            Vector3 flatForward = forward;
            flatForward.y = 0f;

            if (flatForward.sqrMagnitude < 0.0001f)
            {
                return false;
            }

            return Vector3.Angle(flatForward, toTarget) <= fieldOfViewDegrees * 0.5f;
        }
    }
}
