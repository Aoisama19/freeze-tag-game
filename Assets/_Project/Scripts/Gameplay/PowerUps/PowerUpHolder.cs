using Mirror;
using UnityEngine;

namespace BarafPaani.Gameplay.PowerUps
{
    /// <summary>
    /// What one character is carrying, and the only thing allowed to change it.
    ///
    /// Server-authoritative. The old build added a component to the character
    /// for every power-up picked up and destroyed it on use, which is why none
    /// of it survived contact with the network: components added on one machine
    /// exist nowhere else. Here the inventory is replicated state and using one
    /// is a request the server grants or refuses.
    ///
    /// The list syncs to the owner only. What you are carrying is not something
    /// the other side gets to see.
    /// </summary>
    [RequireComponent(typeof(PlayerRole))]
    public class PowerUpHolder : NetworkBehaviour
    {
        private readonly SyncList<PowerUpKind> _carried = new SyncList<PowerUpKind>();

        [SerializeField]
        private int _maxCarried = PowerUpRules.MaxCarried;

        private Freezable _freezable;
        private PowerUpEffects _effects;
        private int _selected;

        public int Count => _carried.Count;

        public int Selected => PowerUpRules.ClampSlot(_selected, _carried.Count);

        /// <summary>What is in a slot, or None if the slot is empty.</summary>
        public PowerUpKind At(int slot)
        {
            return slot >= 0 && slot < _carried.Count ? _carried[slot] : PowerUpKind.None;
        }

        private void Awake()
        {
            _freezable = GetComponent<Freezable>();
            _effects = GetComponent<PowerUpEffects>();

            // Owner only: an inventory is the one piece of a character's state
            // the other players have no business reading.
            syncMode = SyncMode.Owner;
        }

        /// <summary>
        /// Takes one, if there is room. Returns false when full, which is what
        /// stops the pickup consuming itself for nothing.
        /// </summary>
        [Server]
        public bool Add(PowerUpKind kind)
        {
            if (kind == PowerUpKind.None || !PowerUpRules.CanCarry(_carried.Count, _maxCarried))
            {
                return false;
            }

            _carried.Add(kind);
            return true;
        }

        /// <summary>Moves the selection along. Owner-side; nothing is replicated.</summary>
        public void SelectNext()
        {
            _selected = PowerUpRules.NextSlot(Selected, _carried.Count);
        }

        public void SelectPrevious()
        {
            _selected = PowerUpRules.PreviousSlot(Selected, _carried.Count);
        }

        /// <summary>
        /// Asks the server to use what is selected. A request, not an action —
        /// the client says what it wants and the server decides, same as
        /// everything else that changes the state of a match.
        /// </summary>
        [Command]
        public void CmdUse(int slot)
        {
            Use(slot);
        }

        /// <summary>
        /// Spends one. Server-side, so the AI calls it directly and a player
        /// arrives here through CmdUse.
        /// </summary>
        [Server]
        public bool Use(int slot)
        {
            bool frozen = _freezable != null && _freezable.IsFrozen;

            if (!PowerUpRules.CanUse(frozen, _carried.Count))
            {
                return false;
            }

            slot = PowerUpRules.ClampSlot(slot, _carried.Count);
            PowerUpKind kind = _carried[slot];

            if (_effects == null || !_effects.Begin(kind))
            {
                return false;
            }

            int countBefore = _carried.Count;
            _carried.RemoveAt(slot);
            _selected = PowerUpRules.SlotAfterUsing(_selected, slot, countBefore);

            return true;
        }

        /// <summary>Empties the inventory. Used when a round restarts.</summary>
        [Server]
        public void Clear()
        {
            _carried.Clear();
            _selected = 0;
        }
    }
}
