using UnityEngine;

namespace RustPlus.Gameplay.Player
{
    /// <summary>
    /// Pure movement math for the player controller, kept free of engine input/collision
    /// calls so it can be unit tested independently of a live CharacterController.
    /// </summary>
    public sealed class PlayerLocomotion
    {
        public float WalkSpeed = 4.5f;
        public float SprintSpeed = 7f;
        public float CrouchSpeed = 2f;
        public float JumpHeight = 1.2f;
        public float Gravity = -20f;
        public float GroundedStickVelocity = -2f;

        public float ResolveSpeed(bool isSprinting, bool isCrouching)
        {
            if (isCrouching)
            {
                return CrouchSpeed;
            }

            return isSprinting ? SprintSpeed : WalkSpeed;
        }

        public Vector3 ResolvePlanarDirection(Transform reference, Vector2 moveInput)
        {
            Vector2 clampedInput = Vector2.ClampMagnitude(moveInput, 1f);
            return reference.right * clampedInput.x + reference.forward * clampedInput.y;
        }

        public float IntegrateVerticalVelocity(float currentVerticalVelocity, bool isGrounded, bool jumpRequested, float deltaTime)
        {
            if (isGrounded)
            {
                float grounded = jumpRequested
                    ? Mathf.Sqrt(JumpHeight * -2f * Gravity)
                    : GroundedStickVelocity;
                return grounded;
            }

            return currentVerticalVelocity + Gravity * deltaTime;
        }
    }
}
