using BarafPaani.Core;
using Mirror;
using UnityEngine;

namespace BarafPaani.Gameplay.PowerUps
{
    /// <summary>
    /// Turns key presses into requests to use a power-up. Local player only —
    /// the same split as PlayerMotor, and for the same reason: input is a thing
    /// one machine has, and what it asks for is the server's to grant.
    /// </summary>
    [RequireComponent(typeof(PowerUpHolder))]
    public class PowerUpInput : NetworkBehaviour
    {
        private PowerUpHolder _holder;
        private GameInput _input;

        private void Awake()
        {
            _holder = GetComponent<PowerUpHolder>();
        }

        public override void OnStartLocalPlayer()
        {
            _input = new GameInput();
            _input.Player.Enable();
        }

        public override void OnStopLocalPlayer()
        {
            if (_input == null)
            {
                return;
            }

            _input.Player.Disable();
            _input.Dispose();
            _input = null;
        }

        private void Update()
        {
            if (!isLocalPlayer || _input == null)
            {
                return;
            }

            if (_input.Player.Next.WasPressedThisFrame())
            {
                _holder.SelectNext();
            }

            if (_input.Player.Previous.WasPressedThisFrame())
            {
                _holder.SelectPrevious();
            }

            if (_input.Player.Attack.WasPressedThisFrame())
            {
                _holder.CmdUse(_holder.Selected);
            }
        }
    }
}
