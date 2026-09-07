using System;
using BarafPaani.Gameplay;
using BarafPaani.Gameplay.PowerUps;
using UnityEngine;

namespace BarafPaani.AI
{
    /// <summary>
    /// Finds the nearest character this agent can actually see.
    ///
    /// The result is a local that only ever moves closer, returned once at the
    /// end. Assigning it inside the loop instead would make the answer whichever
    /// collider happened to be processed last rather than whether anything was
    /// visible at all, and OverlapSphere promises no order, so sight would
    /// flicker.
    /// </summary>
    public class Vision : MonoBehaviour
    {
        [SerializeField]
        private float _radius = 14f;

        [SerializeField]
        [Range(0f, 360f)]
        [Tooltip("Full width of the view cone, not the half angle.")]
        private float _fieldOfView = 120f;

        [SerializeField]
        [Tooltip("Where sight is measured from. Falls back to this transform.")]
        private Transform _eye;

        [SerializeField]
        private LayerMask _candidateMask = ~0;

        [SerializeField]
        [Tooltip("Geometry that blocks line of sight. Leave empty for open ground.")]
        private LayerMask _blockingMask = 0;

        private readonly Collider[] _hits = new Collider[24];

        public float Radius => _radius;

        private Transform Eye => _eye != null ? _eye : transform;

        /// <summary>
        /// Nearest visible transform whose <see cref="Freezable"/> and
        /// <see cref="PlayerRole"/> pass <paramref name="accept"/>, or null.
        /// </summary>
        public Transform FindNearestVisible(Func<PlayerRole, Freezable, bool> accept)
        {
            Transform eye = Eye;
            Vector3 eyePosition = eye.position;

            Transform nearest = null;
            float nearestSquared = float.MaxValue;

            int count = Physics.OverlapSphereNonAlloc(
                eyePosition, _radius, _hits, _candidateMask, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                Collider hit = _hits[i];

                if (hit == null || hit.transform.root == transform.root)
                {
                    continue;
                }

                PlayerRole role = hit.GetComponentInParent<PlayerRole>();

                if (role == null || !role.TryGetComponent(out Freezable freezable))
                {
                    continue;
                }

                if (!accept(role, freezable))
                {
                    continue;
                }

                // Hidden characters are not seen, whoever is looking. Checked
                // here rather than in every caller's filter, so a power-up
                // cannot be defeated by one query that forgot about it.
                // MapKnowledge says what hidden means for who knows what; for
                // sight it is this simple.
                if (PowerUpEffects.IsHidden(role))
                {
                    continue;
                }

                Vector3 candidatePosition = role.transform.position;

                if (!Sight.InViewCone(eyePosition, eye.forward, candidatePosition, _radius, _fieldOfView))
                {
                    continue;
                }

                if (IsBlocked(eyePosition, candidatePosition))
                {
                    continue;
                }

                float squared = (candidatePosition - eyePosition).sqrMagnitude;

                if (squared < nearestSquared)
                {
                    nearestSquared = squared;
                    nearest = role.transform;
                }
            }

            return nearest;
        }

        private bool IsBlocked(Vector3 from, Vector3 to)
        {
            if (_blockingMask.value == 0)
            {
                return false;
            }

            Vector3 offset = to - from;
            float distance = offset.magnitude;

            if (distance < 0.0001f)
            {
                return false;
            }

            return Physics.Raycast(
                from, offset / distance, distance, _blockingMask, QueryTriggerInteraction.Ignore);
        }

        private void OnDrawGizmosSelected()
        {
            Transform eye = Eye;
            Gizmos.color = new Color(1f, 0.9f, 0.3f, 0.35f);
            Gizmos.DrawWireSphere(eye.position, _radius);

            Vector3 left = Quaternion.Euler(0f, -_fieldOfView * 0.5f, 0f) * eye.forward;
            Vector3 right = Quaternion.Euler(0f, _fieldOfView * 0.5f, 0f) * eye.forward;

            Gizmos.DrawLine(eye.position, eye.position + (left * _radius));
            Gizmos.DrawLine(eye.position, eye.position + (right * _radius));
        }
    }
}
