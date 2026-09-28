using UnityEngine;

namespace RustPlus.Gameplay.Player
{
    /// <summary>
    /// Thin MonoBehaviour shell around <see cref="PlayerLocomotion"/>: reads legacy Input
    /// Manager axes (this project runs activeInputHandler: 0), drives a CharacterController,
    /// and applies mouse look to a separate camera pivot so body yaw and camera pitch stay decoupled.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerMovementController : MonoBehaviour
    {
        [SerializeField] private Transform cameraPivot;
        [SerializeField] private float mouseSensitivity = 2f;
        [SerializeField] private float minPitch = -80f;
        [SerializeField] private float maxPitch = 80f;
        [SerializeField] private bool lockCursorOnStart = true;

        private readonly PlayerLocomotion _locomotion = new PlayerLocomotion();
        private CharacterController _controller;
        private float _verticalVelocity;
        private float _pitch;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();

            if (lockCursorOnStart)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        private void Update()
        {
            ApplyLook();
            ApplyMovement();
        }

        private void ApplyLook()
        {
            float yaw = Input.GetAxis("Mouse X") * mouseSensitivity;
            float pitchDelta = Input.GetAxis("Mouse Y") * mouseSensitivity;

            transform.Rotate(Vector3.up * yaw);

            _pitch = Mathf.Clamp(_pitch - pitchDelta, minPitch, maxPitch);
            if (cameraPivot != null)
            {
                cameraPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
            }
        }

        private void ApplyMovement()
        {
            bool isCrouching = Input.GetKey(KeyCode.LeftControl);
            bool isSprinting = Input.GetKey(KeyCode.LeftShift) && !isCrouching;
            bool jumpRequested = Input.GetButtonDown("Jump") && _controller.isGrounded;

            float speed = _locomotion.ResolveSpeed(isSprinting, isCrouching);
            Vector2 moveInput = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            Vector3 planarMotion = _locomotion.ResolvePlanarDirection(transform, moveInput) * speed;

            _verticalVelocity = _locomotion.IntegrateVerticalVelocity(_verticalVelocity, _controller.isGrounded, jumpRequested, Time.deltaTime);

            Vector3 motion = planarMotion;
            motion.y = _verticalVelocity;

            _controller.Move(motion * Time.deltaTime);
        }
    }
}
