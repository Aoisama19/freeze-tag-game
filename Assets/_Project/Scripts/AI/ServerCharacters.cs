using System;
using BarafPaani.Gameplay;
using Mirror;
using UnityEngine;

namespace BarafPaani.AI
{
    /// <summary>
    /// What the server knows exists, as opposed to what an agent can see.
    ///
    /// Reads Mirror's own NetworkServer.spawned rather than keeping a registry
    /// of ours. That collection is already the authoritative list of what is in
    /// the game, so there is nothing to register, nothing to forget to
    /// unregister, and no static mutable state of our own to go stale, which
    /// docs/architecture.md rules out.
    ///
    /// Server-only. Clients have no business running these.
    /// </summary>
    public static class ServerCharacters
    {
        /// <summary>Nearest character to <paramref name="from"/> that passes the filter.</summary>
        public static Transform FindNearest(
            Vector3 from,
            Transform self,
            Func<PlayerRole, Freezable, bool> accept)
        {
            Transform nearest = null;
            float nearestSquared = float.MaxValue;

            foreach (NetworkIdentity identity in NetworkServer.spawned.Values)
            {
                if (identity == null)
                {
                    continue;
                }

                Transform candidate = identity.transform;

                if (candidate == self)
                {
                    continue;
                }

                if (!candidate.TryGetComponent(out PlayerRole role)
                    || !candidate.TryGetComponent(out Freezable freezable))
                {
                    continue;
                }

                if (!accept(role, freezable))
                {
                    continue;
                }

                float squared = (candidate.position - from).sqrMagnitude;

                if (squared < nearestSquared)
                {
                    nearestSquared = squared;
                    nearest = candidate;
                }
            }

            return nearest;
        }
    }
}
