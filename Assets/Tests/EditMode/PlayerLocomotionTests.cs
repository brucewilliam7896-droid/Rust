using NUnit.Framework;
using RustPlus.Gameplay.Player;
using UnityEngine;

namespace RustPlus.Tests.EditMode
{
    public sealed class PlayerLocomotionTests
    {
        [Test]
        public void ResolveSpeed_PrefersCrouchOverSprint()
        {
            var locomotion = new PlayerLocomotion();

            float speed = locomotion.ResolveSpeed(isSprinting: true, isCrouching: true);

            Assert.AreEqual(locomotion.CrouchSpeed, speed);
        }

        [Test]
        public void ResolveSpeed_ReturnsSprintWhenNotCrouching()
        {
            var locomotion = new PlayerLocomotion();

            float speed = locomotion.ResolveSpeed(isSprinting: true, isCrouching: false);

            Assert.AreEqual(locomotion.SprintSpeed, speed);
        }

        [Test]
        public void ResolveSpeed_ReturnsWalkByDefault()
        {
            var locomotion = new PlayerLocomotion();

            float speed = locomotion.ResolveSpeed(isSprinting: false, isCrouching: false);

            Assert.AreEqual(locomotion.WalkSpeed, speed);
        }

        [Test]
        public void ResolvePlanarDirection_ClampsDiagonalInputToUnitLength()
        {
            var locomotion = new PlayerLocomotion();
            var reference = new GameObject("Reference").transform;

            try
            {
                Vector3 direction = locomotion.ResolvePlanarDirection(reference, new Vector2(1f, 1f));

                Assert.LessOrEqual(direction.magnitude, 1.0001f);
            }
            finally
            {
                Object.DestroyImmediate(reference.gameObject);
            }
        }

        [Test]
        public void IntegrateVerticalVelocity_GroundedWithoutJumpSticksToFloor()
        {
            var locomotion = new PlayerLocomotion();

            float velocity = locomotion.IntegrateVerticalVelocity(currentVerticalVelocity: 0f, isGrounded: true, jumpRequested: false, deltaTime: 0.02f);

            Assert.AreEqual(locomotion.GroundedStickVelocity, velocity);
        }

        [Test]
        public void IntegrateVerticalVelocity_GroundedJumpProducesUpwardVelocityFromJumpHeight()
        {
            var locomotion = new PlayerLocomotion();

            float velocity = locomotion.IntegrateVerticalVelocity(currentVerticalVelocity: 0f, isGrounded: true, jumpRequested: true, deltaTime: 0.02f);

            float expected = Mathf.Sqrt(locomotion.JumpHeight * -2f * locomotion.Gravity);
            Assert.AreEqual(expected, velocity, 0.0001f);
        }

        [Test]
        public void IntegrateVerticalVelocity_AirborneAccumulatesGravityOverTime()
        {
            var locomotion = new PlayerLocomotion();

            float velocity = locomotion.IntegrateVerticalVelocity(currentVerticalVelocity: 3f, isGrounded: false, jumpRequested: false, deltaTime: 0.5f);

            Assert.AreEqual(3f + locomotion.Gravity * 0.5f, velocity, 0.0001f);
        }
    }
}
