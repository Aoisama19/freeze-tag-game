using Mirror;
using UnityEngine;

namespace BarafPaani.Gameplay
{
    /// <summary>
    /// A character's side, owned by the server and replicated to everyone.
    ///
    /// Roles have to be networked state for the same reason freeze does: the
    /// server is the only thing allowed to decide who is the catcher, and every
    /// client needs to agree on the answer.
    ///
    /// A player can ask to change sides in the lobby. It is a request, not an
    /// assignment — the client says what it would like and the server checks it
    /// against LobbyRules, so nothing stops two clients asking to be the catcher
    /// at once except the server answering one of them no.
    /// </summary>
    public class PlayerRole : NetworkBehaviour
    {
        [SyncVar]
        private Role _role = Role.Runner;

        private MatchState _match;

        public Role Role => _role;

        [Server]
        public void SetRole(Role role)
        {
            _role = role;
        }

        /// <summary>Asks to take a side. Only the owner of this character may.</summary>
        [Command]
        public void CmdRequestRole(Role wanted)
        {
            RequestRole(wanted);
        }

        /// <summary>
        /// The server side of that request. Separate so it can be exercised
        /// without a client attached.
        /// </summary>
        [Server]
        public bool RequestRole(Role wanted)
        {
            MatchState match = FindMatch();

            if (match == null)
            {
                return false;
            }

            bool granted = LobbyRules.CanTakeRole(
                match.Phase,
                wanted,
                alreadyThisRole: _role == wanted,
                someoneElseIsCatcher: match.HasHumanCatcherOtherThan(this));

            if (!granted)
            {
                return false;
            }

            if (wanted == Role.Catcher)
            {
                // A bot will be holding the seat whenever nobody human took it,
                // so it has to stand down or the match would have two catchers.
                match.MakeWayForCatcher(this);
            }

            _role = wanted;
            return true;
        }

        private MatchState FindMatch()
        {
            // MatchState is a spawned scene object, so nothing can be wired to
            // it ahead of time. Cached once found.
            if (_match == null)
            {
                _match = FindFirstObjectByType<MatchState>();
            }

            return _match;
        }
    }
}
