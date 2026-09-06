using BarafPaani.Core;
using Mirror;
using UnityEngine;

namespace BarafPaani.Gameplay
{
    /// <summary>
    /// Drives one character from local input. Only the owning client runs this;
    /// everyone else sees the result arrive through NetworkTransform.
    ///
    /// Movement is camera-relative, so which way "forward" is depends on where
    /// the player is looking, not on which way the model happens to face.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerMotor : NetworkBehaviour
    {
        [Header("Speed")]
        [SerializeField] private float _walkSpeed = 4.5f;
        [SerializeField] private float _sprintSpeed = 7.5f;

        [Tooltip("Seconds to settle into a new facing. Higher reads as heavier.")]
        [SerializeField] private float _turnSmoothTime = 0.08f;

        [Header("Gravity and jump")]
        [SerializeField] private float _gravity = -19.62f;
        [SerializeField] private float _jumpHeight = 1.1f;

        [Tooltip("Small downward bias while grounded so the controller hugs slopes instead of skipping down them.")]
        [SerializeField] private float _groundedStick = -2f;

        private CharacterController _controller;
        private GameInput _input;

        /// <summary>
        /// Scales both speeds. Owned by PowerUpEffects, which is the only thing
        /// that sets it — the walk and sprint numbers themselves stay the
        /// character's own, so an effect ending restores them exactly.
        /// </summary>
        public float SpeedMultiplier { get; set; } = 1f;

        private Transform _camera;
        private float _turnVelocity;
        private float _verticalVelocity;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
        }

        public override void OnStartLocalPlayer()
        {
            _input = new GameInput();
            _input.Player.Enable();

            if (Camera.main != null)
            {
                _camera = Camera.main.transform;
            }
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

            Tick(
                _input.Player.Move.ReadValue<Vector2>(),
                _input.Player.Sprint.IsPressed(),
                _input.Player.Jump.WasPressedThisFrame(),
                Time.deltaTime);
        }

        /// <summary>
        /// Split out from Update so the movement itself can be exercised without
        /// an input device attached.
        /// </summary>
        private void Tick(Vector2 move, bool sprinting, bool jumped, float deltaTime)
        {
            ApplyGravity(jumped, deltaTime);

            Vector3 motion = Vector3.zero;

            if (move.sqrMagnitude > 0.0001f)
            {
                float cameraYaw = _camera != null ? _camera.eulerAngles.y : 0f;
                float targetAngle = (Mathf.Atan2(move.x, move.y) * Mathf.Rad2Deg) + cameraYaw;

                float angle = Mathf.SmoothDampAngle(
                    transform.eulerAngles.y, targetAngle, ref _turnVelocity, _turnSmoothTime);
                transform.rotation = Quaternion.Euler(0f, angle, 0f);

                float speed = (sprinting ? _sprintSpeed : _walkSpeed) * SpeedMultiplier;
                motion = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward * speed;
            }

            motion.y = _verticalVelocity;
            _controller.Move(motion * deltaTime);
        }

        private void ApplyGravity(bool jumped, float deltaTime)
        {
            if (_controller.isGrounded)
            {
                _verticalVelocity = _groundedStick;

                if (jumped)
                {
                    _verticalVelocity = Mathf.Sqrt(_jumpHeight * -2f * _gravity);
                }

                return;
            }

            _verticalVelocity += _gravity * deltaTime;
        }
    }
}
