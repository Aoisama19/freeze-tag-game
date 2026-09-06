using UnityEngine;

namespace BarafPaani.Gameplay
{
    /// <summary>
    /// Working out how fast a character is travelling from where it was and
    /// where it is now.
    ///
    /// Pulled out of CharacterAnimation so the awkward part is testable without
    /// a running match: a round restart teleports everyone back to their spawn,
    /// and a teleport is a step like any other as far as a transform is
    /// concerned. Read literally, being moved 60 metres in one frame is a sprint
    /// at several thousand metres a second, and the character would explode into
    /// a run animation on the frame the round begins.
    /// </summary>
    public static class MoveSpeed
    {
        /// <summary>
        /// Metres per second covered on the ground, or zero if the step was too
        /// big to have been walked.
        /// </summary>
        public static float Measure(
            Vector3 from, Vector3 to, float deltaTime, float teleportSpeed)
        {
            if (deltaTime <= 0f)
            {
                return 0f;
            }

            Vector3 step = to - from;

            // Horizontal only. Falling is not running, and a character dropping
            // off a ledge should not break into a sprint on the way down.
            step.y = 0f;

            float speed = step.magnitude / deltaTime;

            return speed > teleportSpeed ? 0f : speed;
        }
    }
}
