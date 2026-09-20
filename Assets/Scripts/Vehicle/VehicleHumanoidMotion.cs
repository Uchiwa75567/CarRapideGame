using System;
using UnityEngine;

namespace CarRapide.Vehicle
{
    [DisallowMultipleComponent]
    public sealed class VehicleHumanoidMotion : MonoBehaviour
    {
        public enum CharacterRole
        {
            Driver,
            Receiver
        }

        public enum MotionMode
        {
            Idle,
            ReachDoor,
            Walk,
            StepIntoVehicle,
            SitDown,
            DriverSeated,
            ReceiverRide
        }

        private Animator animator;
        private HumanPoseHandler poseHandler;
        private HumanPose pose;
        private float[] neutralMuscles;
        private float[] currentMuscles;
        private float[] targetMuscles;

        private CharacterRole role;
        private MotionMode mode = MotionMode.Idle;
        private float blendSpeed = 8f;
        private float vehicleSpeedNormalized;
        private float walkPhase;
        private bool initialized;

        public void SetRole(CharacterRole newRole)
        {
            role = newRole;
        }

        public void SetMode(MotionMode newMode, float blendSeconds = 0.25f)
        {
            mode = newMode;
            blendSpeed = 1f / Mathf.Max(0.05f, blendSeconds);
        }

        public void SetVehicleSpeed(float normalizedSpeed)
        {
            vehicleSpeedNormalized = Mathf.Clamp01(normalizedSpeed);
        }

        private void Awake()
        {
            Initialize();
        }

        private void OnEnable()
        {
            if (!initialized)
            {
                Initialize();
            }
        }

        private void Initialize()
        {
            animator = GetComponentInChildren<Animator>();
            if (animator == null || animator.avatar == null || !animator.avatar.isHuman)
            {
                return;
            }

            try
            {
                poseHandler = new HumanPoseHandler(animator.avatar, animator.transform);
                pose = new HumanPose
                {
                    muscles = new float[HumanTrait.MuscleCount]
                };

                poseHandler.GetHumanPose(ref pose);
                neutralMuscles = (float[])pose.muscles.Clone();
                currentMuscles = (float[])neutralMuscles.Clone();
                targetMuscles = new float[HumanTrait.MuscleCount];
                initialized = true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Humanoid motion unavailable on {name}: {exception.Message}");
            }
        }

        private void LateUpdate()
        {
            if (!initialized || poseHandler == null)
            {
                return;
            }

            Array.Copy(neutralMuscles, targetMuscles, neutralMuscles.Length);

            float delta = Time.deltaTime;
            walkPhase += delta * 8.5f;
            BuildTargetPose();

            float blend = 1f - Mathf.Exp(-blendSpeed * delta);
            for (int i = 0; i < currentMuscles.Length; i++)
            {
                currentMuscles[i] = Mathf.Lerp(currentMuscles[i], targetMuscles[i], blend);
            }

            pose.muscles = currentMuscles;
            poseHandler.SetHumanPose(ref pose);
        }

        private void BuildTargetPose()
        {
            float breathe = Mathf.Sin(Time.time * 1.7f) * 0.025f;

            switch (mode)
            {
                case MotionMode.Idle:
                    Set("Chest Front-Back", breathe);
                    Set("Left Arm Down-Up", -0.90f);
                    Set("Right Arm Down-Up", -0.90f);
                    break;

                case MotionMode.ReachDoor:
                    Set("Chest Front-Back", 0.08f);
                    Set("Left Arm Down-Up", role == CharacterRole.Driver ? 0.38f : -0.80f);
                    Set("Left Arm Front-Back", -0.48f);
                    Set("Left Forearm Stretch", -0.48f);
                    Set("Right Arm Down-Up", -0.82f);
                    break;

                case MotionMode.Walk:
                {
                    float step = Mathf.Sin(walkPhase);
                    float opposite = -step;
                    Set("Left Upper Leg Front-Back", step * 0.42f);
                    Set("Right Upper Leg Front-Back", opposite * 0.42f);
                    Set("Left Knee Stretch", -Mathf.Max(0f, opposite) * 0.55f);
                    Set("Right Knee Stretch", -Mathf.Max(0f, step) * 0.55f);
                    Set("Left Arm Front-Back", opposite * 0.30f);
                    Set("Right Arm Front-Back", step * 0.30f);
                    Set("Left Arm Down-Up", -0.82f);
                    Set("Right Arm Down-Up", -0.82f);
                    Set("Chest Left-Right", step * 0.035f);
                    break;
                }

                case MotionMode.StepIntoVehicle:
                    Set("Left Upper Leg Front-Back", 0.58f);
                    Set("Left Knee Stretch", -0.72f);
                    Set("Right Upper Leg Front-Back", -0.12f);
                    Set("Right Knee Stretch", -0.18f);
                    Set("Left Arm Down-Up", -0.35f);
                    Set("Left Arm Front-Back", -0.28f);
                    Set("Left Forearm Stretch", -0.35f);
                    Set("Right Arm Down-Up", -0.65f);
                    Set("Spine Front-Back", 0.08f);
                    break;

                case MotionMode.SitDown:
                    ApplySeatedLegs(0.72f);
                    Set("Spine Front-Back", 0.10f);
                    Set("Left Arm Down-Up", -0.48f);
                    Set("Right Arm Down-Up", -0.48f);
                    Set("Left Arm Front-Back", -0.28f);
                    Set("Right Arm Front-Back", -0.28f);
                    Set("Left Forearm Stretch", -0.45f);
                    Set("Right Forearm Stretch", -0.45f);
                    break;

                case MotionMode.DriverSeated:
                    ApplySeatedLegs(0.78f);
                    Set("Spine Front-Back", 0.02f + breathe);
                    Set("Left Arm Down-Up", -0.32f);
                    Set("Right Arm Down-Up", -0.32f);
                    Set("Left Arm Front-Back", -0.46f);
                    Set("Right Arm Front-Back", -0.46f);
                    Set("Left Forearm Stretch", -0.62f);
                    Set("Right Forearm Stretch", -0.62f);
                    break;

                case MotionMode.ReceiverRide:
                {
                    float sway = Mathf.Sin(Time.time * (2.2f + vehicleSpeedNormalized * 3.5f));
                    float amplitude = 0.035f + vehicleSpeedNormalized * 0.09f;

                    Set("Spine Left-Right", sway * amplitude);
                    Set("Chest Left-Right", -sway * amplitude * 0.7f);
                    Set("Left Upper Leg In-Out", -0.16f);
                    Set("Right Upper Leg In-Out", 0.16f);
                    Set("Left Knee Stretch", -0.12f - Mathf.Abs(sway) * 0.08f);
                    Set("Right Knee Stretch", -0.14f - Mathf.Abs(sway) * 0.08f);

                    Set("Right Shoulder Down-Up", 0.38f);
                    Set("Right Arm Down-Up", 0.76f);
                    Set("Right Arm Front-Back", -0.22f);
                    Set("Right Forearm Stretch", -0.52f);

                    Set("Left Arm Down-Up", -0.72f);
                    Set("Left Forearm Stretch", -0.18f);
                    break;
                }
            }
        }

        private void ApplySeatedLegs(float strength)
        {
            Set("Left Upper Leg Front-Back", -strength);
            Set("Right Upper Leg Front-Back", -strength);
            Set("Left Knee Stretch", -0.88f);
            Set("Right Knee Stretch", -0.88f);
            Set("Left Foot Up-Down", -0.10f);
            Set("Right Foot Up-Down", -0.10f);
        }

        private void Set(string muscleName, float value)
        {
            string[] names = HumanTrait.MuscleName;
            for (int i = 0; i < names.Length; i++)
            {
                if (names[i] == muscleName)
                {
                    targetMuscles[i] = Mathf.Clamp(value, -1f, 1f);
                    return;
                }
            }
        }

        private void OnDestroy()
        {
            poseHandler?.Dispose();
            poseHandler = null;
        }
    }
}
