using Mirror;
using Mirror.Discovery;
using UnityEngine;

namespace BarafPaani.EditorTools
{
    /// <summary>
    /// Adds LAN discovery to a scene's network manager, the same way on both
    /// sides of the conversation.
    ///
    /// Shared rather than done twice, because of one detail that fails
    /// silently: Mirror compares a secretHandshake on every discovery packet and
    /// drops anything that does not match. Its OnValidate fills that field with
    /// a random number whenever it is zero, and the host's scenes and the
    /// joiner's scene are built by separate runs — so left to itself, each side
    /// would pick its own number, no packet would ever match, and the host list
    /// would simply always be empty with nothing logged anywhere.
    /// </summary>
    public static class DiscoverySetup
    {
        /// <summary>
        /// Fixed so both sides agree. Not a secret in any real sense — it only
        /// keeps this game's broadcasts apart from those of any other Mirror
        /// game running on the same network.
        /// </summary>
        private const long Handshake = 0x42415241465041L;

        /// <summary>Adds discovery to an object that already carries the manager.</summary>
        public static NetworkDiscovery AddTo(GameObject host, Transport transport)
        {
            NetworkDiscovery discovery = host.AddComponent<NetworkDiscovery>();

            discovery.transport = transport;
            discovery.secretHandshake = Handshake;

            // The client re-asks every few seconds, which is also how a host
            // that has gone away stops being listed.
            discovery.enableActiveDiscovery = true;

            return discovery;
        }
    }
}
