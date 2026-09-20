using UnityEngine;
using UnityEngine.InputSystem;

namespace CarRapide.Vehicle
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class VehicleController : MonoBehaviour
    {
        [Header("Speed")]
        [SerializeField, Min(0f)] private float acceleration = 14f;
        [SerializeField, Min(0f)] private float reverseAcceleration = 8f;
        [SerializeField, Min(0f)] private float maxForwardSpeed = 18f;
        [SerializeField, Min(0f)] private float maxReverseSpeed = 7f;

        [Header("Steering")]
        [SerializeField, Min(0f)] private float steeringDegreesPerSecond = 70f;
        [SerializeField, Range(0f, 1f)] private float minimumSteeringFactor = 0.3f;

        [Header("Braking")]
        [SerializeField, Min(0f)] private float brakeDeceleration = 28f;
        [SerializeField, Min(0f)] private float handbrakeDeceleration = 45f;
        [SerializeField, Min(0f)] private float rollingResistance = 1.2f;
        [SerializeField, Min(0f)] private float stopSpeedThreshold = 0.15f;

        [Header("Stability")]
        [SerializeField, Min(0f)] private float lateralGrip = 6f;
        [SerializeField] private bool lockPitchAndRoll = true;

        private Rigidbody vehicleRigidbody;
        private Vector2 moveInput;
        private bool handbrakePressed;

        public bool CanDrive { get; set; } = true;

        public float SpeedKmh
        {
            get
            {
                Vector3 velocity = vehicleRigidbody == null
                    ? Vector3.zero
                    : vehicleRigidbody.linearVelocity;

                velocity.y = 0f;
                return velocity.magnitude * 3.6f;
            }
        }

        private void Awake()
        {
            vehicleRigidbody = GetComponent<Rigidbody>();
            vehicleRigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            vehicleRigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            if (lockPitchAndRoll)
            {
                vehicleRigidbody.constraints |= RigidbodyConstraints.FreezeRotationX;
                vehicleRigidbody.constraints |= RigidbodyConstraints.FreezeRotationZ;
            }
        }

        private void Update()
        {
            moveInput = CanDrive ? ReadMoveInput() : Vector2.zero;
            handbrakePressed = !CanDrive || ReadHandbrake();
        }

        private void FixedUpdate()
        {
            float forwardSpeed = Vector3.Dot(vehicleRigidbody.linearVelocity, transform.forward);

            ApplyAccelerationAndBraking(forwardSpeed);
            ApplySteering(forwardSpeed);
            ApplyLateralGrip();
            ClampForwardSpeed();
        }

        private void ApplyAccelerationAndBraking(float forwardSpeed)
        {
            float throttle = moveInput.y;

            if (handbrakePressed)
            {
                ApplyBrake(handbrakeDeceleration);
                return;
            }

            if (throttle > 0.01f)
            {
                if (forwardSpeed < -0.25f)
                {
                    ApplyBrake(brakeDeceleration * throttle);
                }
                else if (forwardSpeed < maxForwardSpeed)
                {
                    vehicleRigidbody.AddForce(
                        transform.forward * (acceleration * throttle),
                        ForceMode.Acceleration);
                }

                return;
            }

            if (throttle < -0.01f)
            {
                float reverseInput = -throttle;

                if (forwardSpeed > 0.25f)
                {
                    ApplyBrake(brakeDeceleration * reverseInput);
                }
                else if (forwardSpeed > -maxReverseSpeed)
                {
                    vehicleRigidbody.AddForce(
                        -transform.forward * (reverseAcceleration * reverseInput),
                        ForceMode.Acceleration);
                }

                return;
            }

            ApplyBrake(rollingResistance);
        }

        private void ApplyBrake(float deceleration)
        {
            Vector3 velocity = vehicleRigidbody.linearVelocity;
            Vector3 horizontalVelocity = new Vector3(velocity.x, 0f, velocity.z);

            if (horizontalVelocity.magnitude <= stopSpeedThreshold)
            {
                vehicleRigidbody.linearVelocity = new Vector3(0f, velocity.y, 0f);
                return;
            }

            vehicleRigidbody.AddForce(
                -horizontalVelocity.normalized * deceleration,
                ForceMode.Acceleration);
        }

        private void ApplySteering(float forwardSpeed)
        {
            if (Mathf.Abs(forwardSpeed) <= 0.2f || Mathf.Abs(moveInput.x) <= 0.01f)
            {
                return;
            }

            float speedReference = forwardSpeed >= 0f
                ? Mathf.Max(maxForwardSpeed, 0.01f)
                : Mathf.Max(maxReverseSpeed, 0.01f);

            float speedFactor = Mathf.Clamp01(Mathf.Abs(forwardSpeed) / speedReference);
            float steeringFactor = Mathf.Lerp(minimumSteeringFactor, 1f, speedFactor);
            float travelDirection = forwardSpeed >= 0f ? 1f : -1f;

            float yaw = moveInput.x
                * steeringDegreesPerSecond
                * steeringFactor
                * travelDirection
                * Time.fixedDeltaTime;

            Quaternion targetRotation = vehicleRigidbody.rotation * Quaternion.Euler(0f, yaw, 0f);
            vehicleRigidbody.MoveRotation(targetRotation);
        }

        private void ApplyLateralGrip()
        {
            Vector3 localVelocity = transform.InverseTransformDirection(vehicleRigidbody.linearVelocity);
            float gripAmount = Mathf.Clamp01(lateralGrip * Time.fixedDeltaTime);

            localVelocity.x = Mathf.Lerp(localVelocity.x, 0f, gripAmount);
            vehicleRigidbody.linearVelocity = transform.TransformDirection(localVelocity);
        }

        private void ClampForwardSpeed()
        {
            Vector3 localVelocity = transform.InverseTransformDirection(vehicleRigidbody.linearVelocity);

            localVelocity.z = Mathf.Clamp(
                localVelocity.z,
                -maxReverseSpeed,
                maxForwardSpeed);

            vehicleRigidbody.linearVelocity = transform.TransformDirection(localVelocity);
        }

        private static Vector2 ReadMoveInput()
        {
            Vector2 input = Vector2.zero;

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                float horizontal = 0f;
                float vertical = 0f;

                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
                {
                    horizontal -= 1f;
                }

                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
                {
                    horizontal += 1f;
                }

                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
                {
                    vertical += 1f;
                }

                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
                {
                    vertical -= 1f;
                }

                input = new Vector2(horizontal, vertical);
            }

            Gamepad gamepad = Gamepad.current;
            if (gamepad != null)
            {
                Vector2 gamepadInput = gamepad.leftStick.ReadValue();

                if (gamepadInput.sqrMagnitude > input.sqrMagnitude)
                {
                    input = gamepadInput;
                }
            }

            return Vector2.ClampMagnitude(input, 1f);
        }

        private static bool ReadHandbrake()
        {
            bool keyboardBrake = Keyboard.current != null && Keyboard.current.spaceKey.isPressed;
            bool gamepadBrake = Gamepad.current != null && Gamepad.current.buttonSouth.isPressed;

            return keyboardBrake || gamepadBrake;
        }
    }
}
