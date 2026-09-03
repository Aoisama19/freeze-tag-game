using Mirror;

namespace BarafPaani.Gameplay
{
    /// <summary>
    /// A character's side, owned by the server and replicated to everyone.
    ///
    /// Roles have to be networked state for the same reason freeze does: the
    /// server is the only thing allowed to decide who is the catcher, and every
    /// client needs to agree on the answer.
    /// </summary>
    public class PlayerRole : NetworkBehaviour
    {
        [SyncVar]
        private Role _role = Role.Runner;

        public Role Role => _role;

        [Server]
        public void SetRole(Role role)
        {
            _role = role;
        }
    }
}
