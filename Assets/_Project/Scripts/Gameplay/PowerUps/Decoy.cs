using Mirror;
using UnityEngine;

namespace BarafPaani.Gameplay.PowerUps
{
    /// <summary>
    /// A standing copy of whoever made it, there to be chased instead of them.
    ///
    /// Issue 3 in docs/old-build-issues.md is this power-up. The old Clone found
    /// its bodies with GameObject.FindGameObjectsWithTag("Clone") — a scene-wide
    /// lookup, so every character holding the power-up found and moved the same
    /// objects and two users fought over them, and it indexed [0] with no length
    /// check so it threw when nothing in the scene carried the tag.
    ///
    /// A decoy is now spawned by the character that used the power-up, exists
    /// only for as long as it is meant to, and is destroyed by the server that
    /// made it. There is no pool and nothing shared.
    ///
    /// It carries PlayerRole and Freezable on purpose: that is what makes it
    /// worth chasing, because the catcher's targeting sees a runner and the tag
    /// query freezes it like one. It deliberately has no TagOnContact — a decoy
    /// that could freeze people by standing next to them would be a weapon
    /// rather than a trick.
    /// </summary>
    [RequireComponent(typeof(PlayerRole))]
    public class Decoy : NetworkBehaviour
    {
        [SerializeField]
        [Tooltip("Seconds before it disappears.")]
        private float _lifetimeSeconds = 12f;

        private double _diesAt;

        /// <summary>
        /// Sets it going. The role is the maker's own, so a catcher's decoy
        /// draws runners and a runner's decoy draws the catcher.
        /// </summary>
        [Server]
        public void Live(Role role)
        {
            GetComponent<PlayerRole>().SetRole(role);
            _diesAt = NetworkTime.time + _lifetimeSeconds;
        }

        [ServerCallback]
        private void Update()
        {
            // Guard against a decoy spawned without Live ever being called,
            // which would otherwise stand there for the rest of the match.
            if (_diesAt <= 0d)
            {
                _diesAt = NetworkTime.time + _lifetimeSeconds;
                return;
            }

            if (NetworkTime.time >= _diesAt)
            {
                NetworkServer.Destroy(gameObject);
            }
        }

        /// <summary>
        /// Whether this identity is a decoy rather than somebody playing.
        ///
        /// Used by the two places that count heads — the round's runner tally
        /// and the bot filling — because both would otherwise treat a decoy as a
        /// runner. A decoy in the tally makes a round unwinnable; a decoy in the
        /// headcount makes the game send a real bot home to make room for it.
        /// </summary>
        public static bool Is(NetworkIdentity identity)
        {
            return identity != null && identity.GetComponent<Decoy>() != null;
        }
    }
}
