using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CarRapide.Vehicle
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(VehicleController))]
    public sealed class VehicleDriverExperience : MonoBehaviour
    {
        private enum ExperienceState
        {
            WaitingForDoor,
            DoorOpening,
            DoorOpen,
            Entering,
            Seated,
            StartingEngine,
            Driving
        }

        private const string DriverResource = "CarRapide/Characters/Black_M_1_Casual";
        private const string ReceiverResource = "CarRapide/Characters/Black_M_2_Casual";

        private VehicleController vehicleController;
        private Rigidbody vehicleRigidbody;
        private VehicleCameraFollow cameraFollow;
        private Transform doorPivot;
        private Transform driverCharacter;
        private VehicleHumanoidMotion driverMotion;
        private VehicleHumanoidMotion receiverMotion;
        private AudioSource engineStartSource;
        private AudioSource engineLoopSource;
        private ExperienceState state;

        private Bounds vehicleBoundsLocal;
        private Bounds driverDoorBoundsLocal;
        private Vector3 driverOutsideFeetPosition;
        private Vector3 driverDoorFeetPosition;
        private Vector3 driverInsideFeetPosition;
        private Vector3 driverSeatHipsPosition;
        private Vector3 receiverFeetPosition;
        private Vector3 exteriorCameraOffset;
        private Vector3 exteriorCameraLookPoint;
        private Vector3 cabinCameraOffset;
        private Vector3 cabinCameraLookPoint;
        private Vector3 drivingCameraOffset;
        private Vector3 drivingCameraLookPoint;
        private float driverSideSign = -1f;
        private float vehicleFrontSign = 1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoAttachToPlayableVehicle()
        {
            VehicleController controller = FindFirstObjectByType<VehicleController>();
            if (controller != null && controller.GetComponent<VehicleDriverExperience>() == null)
            {
                controller.gameObject.AddComponent<VehicleDriverExperience>();
            }
        }

        private void Awake()
        {
            vehicleController = GetComponent<VehicleController>();
            vehicleRigidbody = GetComponent<Rigidbody>();

            vehicleController.CanDrive = false;
            vehicleRigidbody.linearVelocity = Vector3.zero;
            vehicleRigidbody.angularVelocity = Vector3.zero;
        }

        private void Start()
        {
            CalculateVehicleBounds();
            SetupRealDriverDoor();
            CalculateInteractionLayout();

            cameraFollow = FindFirstObjectByType<VehicleCameraFollow>();
            if (cameraFollow != null)
            {
                cameraFollow.SetTarget(transform);
                cameraFollow.SetView(exteriorCameraOffset, exteriorCameraLookPoint, 0.28f);
            }

            BuildCharacters();
            BuildAudio();

            state = ExperienceState.WaitingForDoor;
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.eKey.wasPressedThisFrame)
                {
                    if (state == ExperienceState.WaitingForDoor)
                    {
                        StartCoroutine(OpenDoorRoutine());
                    }
                    else if (state == ExperienceState.DoorOpen)
                    {
                        StartCoroutine(EnterVehicleRoutine());
                    }
                }

                if (keyboard.rKey.wasPressedThisFrame && state == ExperienceState.Seated)
                {
                    StartCoroutine(StartEngineRoutine());
                }
            }

            float normalizedVehicleSpeed = Mathf.InverseLerp(0f, 70f, vehicleController.SpeedKmh);
            if (receiverMotion != null)
            {
                receiverMotion.SetVehicleSpeed(normalizedVehicleSpeed);
            }

            if (state == ExperienceState.Driving && engineLoopSource != null)
            {
                engineLoopSource.pitch = Mathf.Lerp(0.82f, 1.42f, normalizedVehicleSpeed);
                engineLoopSource.volume = Mathf.Lerp(0.11f, 0.24f, normalizedVehicleSpeed);
            }
        }

        private void CalculateVehicleBounds()
        {
            BoxCollider vehicleBox = GetComponent<BoxCollider>();
            if (vehicleBox != null)
            {
                Vector3 absScale = new Vector3(
                    Mathf.Abs(transform.localScale.x),
                    Mathf.Abs(transform.localScale.y),
                    Mathf.Abs(transform.localScale.z));

                Vector3 size = Vector3.Scale(vehicleBox.size, absScale);
                Vector3 center = Vector3.Scale(vehicleBox.center, absScale);
                vehicleBoundsLocal = new Bounds(center, size);
                return;
            }

            Transform modelRoot = FindDeepChild(transform, "Car rapide") ?? transform;
            Renderer[] renderers = modelRoot.GetComponentsInChildren<Renderer>(true);

            if (renderers.Length == 0)
            {
                vehicleBoundsLocal = new Bounds(Vector3.zero, new Vector3(1.55f, 2.45f, 3.65f));
                return;
            }

            bool initialized = false;
            Bounds local = default;

            foreach (Renderer renderer in renderers)
            {
                foreach (Vector3 corner in BoundsCorners(renderer.bounds))
                {
                    Vector3 point = transform.InverseTransformPoint(corner);
                    if (!initialized)
                    {
                        local = new Bounds(point, Vector3.zero);
                        initialized = true;
                    }
                    else
                    {
                        local.Encapsulate(point);
                    }
                }
            }

            vehicleBoundsLocal = local;
        }

        private void CalculateInteractionLayout()
        {
            float width = Mathf.Max(0.5f, vehicleBoundsLocal.size.x);
            float height = Mathf.Max(1f, vehicleBoundsLocal.size.y);
            float length = Mathf.Max(1f, vehicleBoundsLocal.size.z);

            Vector3 doorCenter = driverDoorBoundsLocal.size.sqrMagnitude > 0.001f
                ? driverDoorBoundsLocal.center
                : new Vector3(vehicleBoundsLocal.min.x, vehicleBoundsLocal.center.y, vehicleBoundsLocal.center.z);

            driverSideSign = doorCenter.x >= vehicleBoundsLocal.center.x ? 1f : -1f;

            Bounds steeringBounds;
            bool hasSteeringWheel = TryFindNamedBounds("Volant", out steeringBounds);
            Vector3 steeringCenter = hasSteeringWheel
                ? steeringBounds.center
                : doorCenter + new Vector3(-driverSideSign * width * 0.18f, height * 0.03f, length * 0.16f);

            vehicleFrontSign = steeringCenter.z >= vehicleBoundsLocal.center.z ? 1f : -1f;

            float groundY = vehicleBoundsLocal.min.y + height * 0.025f;
            float stepY = groundY + height * 0.105f;

            float outsideX = driverSideSign > 0f
                ? driverDoorBoundsLocal.max.x + width * 0.28f
                : driverDoorBoundsLocal.min.x - width * 0.28f;

            float doorX = driverSideSign > 0f
                ? driverDoorBoundsLocal.max.x + width * 0.055f
                : driverDoorBoundsLocal.min.x - width * 0.055f;

            float insideX = driverSideSign > 0f
                ? driverDoorBoundsLocal.min.x - width * 0.06f
                : driverDoorBoundsLocal.max.x + width * 0.06f;

            driverOutsideFeetPosition = new Vector3(
                outsideX,
                groundY,
                doorCenter.z - vehicleFrontSign * length * 0.015f);

            driverDoorFeetPosition = new Vector3(
                doorX,
                groundY,
                doorCenter.z);

            driverInsideFeetPosition = new Vector3(
                insideX,
                stepY,
                doorCenter.z - vehicleFrontSign * length * 0.07f);

            // Seat is derived from the real steering wheel, so the driver ends up
            // behind the wheel instead of being guessed from the vehicle roof/door.
            driverSeatHipsPosition = steeringCenter + new Vector3(
                0f,
                -height * 0.18f,
                -vehicleFrontSign * length * 0.115f);

            Bounds rearDoorBounds;
            if (TryFindNamedBounds("Porte_arriere", out rearDoorBounds))
            {
                float rearSign = rearDoorBounds.center.z >= vehicleBoundsLocal.center.z ? 1f : -1f;

                receiverFeetPosition = new Vector3(
                    vehicleBoundsLocal.center.x - driverSideSign * width * 0.12f,
                    groundY + height * 0.105f,
                    rearDoorBounds.center.z + rearSign * Mathf.Max(0.08f, length * 0.025f));
            }
            else
            {
                float rearZ = vehicleFrontSign > 0f
                    ? vehicleBoundsLocal.min.z - length * 0.025f
                    : vehicleBoundsLocal.max.z + length * 0.025f;

                receiverFeetPosition = new Vector3(
                    vehicleBoundsLocal.center.x - driverSideSign * width * 0.12f,
                    groundY + height * 0.105f,
                    rearZ);
            }

            // Exterior camera: close enough to actually see the driver + door interaction.
            exteriorCameraOffset = new Vector3(
                doorCenter.x + driverSideSign * Mathf.Max(1.55f, width * 1.10f),
                groundY + Mathf.Max(1.55f, height * 0.68f),
                doorCenter.z - vehicleFrontSign * length * 0.12f);

            exteriorCameraLookPoint = doorCenter + new Vector3(
                0f,
                driverDoorBoundsLocal.size.y * 0.08f,
                -vehicleFrontSign * length * 0.03f);

            // Cabin camera sits just outside/above the driver's shoulder and looks
            // toward the real steering wheel/seat.
            cabinCameraOffset = steeringCenter + new Vector3(
                driverSideSign * Mathf.Max(0.72f, width * 0.48f),
                height * 0.12f,
                -vehicleFrontSign * length * 0.10f);

            cabinCameraLookPoint = Vector3.Lerp(
                steeringCenter,
                driverSeatHipsPosition + Vector3.up * height * 0.18f,
                0.45f);

            // Third-person camera is deliberately closer than the previous prototype.
            Vector3 vehicleCenter = vehicleBoundsLocal.center;
            drivingCameraOffset = vehicleCenter + new Vector3(
                0f,
                Mathf.Max(2.15f, height * 0.92f),
                -vehicleFrontSign * Mathf.Max(4.35f, length * 1.18f));

            drivingCameraLookPoint = vehicleCenter + new Vector3(
                0f,
                height * 0.28f,
                vehicleFrontSign * length * 0.18f);

            CreateOrMoveAnchor("DriverOutsidePoint", driverOutsideFeetPosition);
            CreateOrMoveAnchor("DriverDoorPoint", driverDoorFeetPosition);
            CreateOrMoveAnchor("DriverStepPoint", driverInsideFeetPosition);
            CreateOrMoveAnchor("DriverSeatPoint", driverSeatHipsPosition);
            CreateOrMoveAnchor("ReceiverPlatformPoint", receiverFeetPosition);
        }

        private void SetupRealDriverDoor()
        {
            Transform modelRoot = FindDeepChild(transform, "Car rapide");
            if (modelRoot == null)
            {
                Debug.LogWarning("Car Rapide: modèle visuel introuvable, impossible d'animer la vraie porte.");
                return;
            }

            Transform[] all = modelRoot.GetComponentsInChildren<Transform>(true);
            List<Transform> doorParts = all
                .Where(t => t.name.StartsWith("Porte_avant_gauche", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (doorParts.Count == 0)
            {
                Debug.LogWarning("Car Rapide: 'Porte_avant_gauche' introuvable dans le FBX.");
                return;
            }

            HashSet<Transform> matches = new HashSet<Transform>(doorParts);
            List<Transform> topLevelDoorParts = doorParts
                .Where(t =>
                {
                    Transform parent = t.parent;
                    while (parent != null && parent != modelRoot.parent)
                    {
                        if (matches.Contains(parent))
                        {
                            return false;
                        }
                        parent = parent.parent;
                    }
                    return true;
                })
                .ToList();

            bool initialized = false;
            Bounds localDoorBounds = default;

            foreach (Transform part in doorParts)
            {
                foreach (Renderer renderer in part.GetComponentsInChildren<Renderer>(true))
                {
                    foreach (Vector3 corner in BoundsCorners(renderer.bounds))
                    {
                        Vector3 local = transform.InverseTransformPoint(corner);
                        if (!initialized)
                        {
                            localDoorBounds = new Bounds(local, Vector3.zero);
                            initialized = true;
                        }
                        else
                        {
                            localDoorBounds.Encapsulate(local);
                        }
                    }
                }
            }

            if (!initialized)
            {
                Debug.LogWarning("Car Rapide: la porte gauche n'a pas de Renderer.");
                return;
            }

            driverDoorBoundsLocal = localDoorBounds;

            float distanceToMinZ = Mathf.Abs(localDoorBounds.center.z - vehicleBoundsLocal.min.z);
            float distanceToMaxZ = Mathf.Abs(vehicleBoundsLocal.max.z - localDoorBounds.center.z);
            float frontSign = distanceToMaxZ < distanceToMinZ ? 1f : -1f;
            float hingeZ = frontSign > 0f ? localDoorBounds.max.z : localDoorBounds.min.z;

            GameObject pivotObject = new GameObject("DriverDoorHinge_RealModel");
            pivotObject.transform.SetParent(transform, false);
            pivotObject.transform.localPosition = new Vector3(
                localDoorBounds.center.x,
                localDoorBounds.center.y,
                hingeZ);
            pivotObject.transform.localRotation = Quaternion.identity;
            doorPivot = pivotObject.transform;

            foreach (Transform part in topLevelDoorParts)
            {
                part.SetParent(doorPivot, true);
            }
        }

        private IEnumerator OpenDoorRoutine()
        {
            if (doorPivot == null)
            {
                state = ExperienceState.DoorOpen;
                yield break;
            }

            state = ExperienceState.DoorOpening;

            if (driverMotion != null)
            {
                driverMotion.SetMode(VehicleHumanoidMotion.MotionMode.ReachDoor, 0.18f);
            }

            Quaternion start = doorPivot.localRotation;
            float openAngle = -72f * driverSideSign * vehicleFrontSign;
            Quaternion target = Quaternion.Euler(0f, openAngle, 0f);
            const float duration = 0.75f;

            for (float time = 0f; time < duration; time += Time.deltaTime)
            {
                float t = Mathf.SmoothStep(0f, 1f, time / duration);
                doorPivot.localRotation = Quaternion.Slerp(start, target, t);
                yield return null;
            }

            doorPivot.localRotation = target;

            if (driverMotion != null)
            {
                driverMotion.SetMode(VehicleHumanoidMotion.MotionMode.Idle, 0.22f);
            }

            state = ExperienceState.DoorOpen;
        }

        private IEnumerator EnterVehicleRoutine()
        {
            if (driverCharacter == null)
            {
                state = ExperienceState.Seated;
                yield break;
            }

            state = ExperienceState.Entering;

            if (cameraFollow != null)
            {
                cameraFollow.SetView(
                    cabinCameraOffset,
                    cabinCameraLookPoint,
                    0.58f);
            }

            float faceDoorYaw = driverSideSign < 0f ? 90f : -90f;
            Quaternion faceDoorRotation = Quaternion.Euler(0f, faceDoorYaw, 0f);

            if (driverMotion != null)
            {
                driverMotion.SetMode(VehicleHumanoidMotion.MotionMode.Walk, 0.15f);
            }

            yield return MoveCharacterFeet(
                driverCharacter,
                driverDoorFeetPosition,
                faceDoorRotation,
                0.72f,
                0.035f);

            if (driverMotion != null)
            {
                driverMotion.SetMode(VehicleHumanoidMotion.MotionMode.StepIntoVehicle, 0.16f);
            }

            yield return MoveCharacterFeet(
                driverCharacter,
                driverInsideFeetPosition,
                faceDoorRotation,
                0.70f,
                0.075f);

            if (driverMotion != null)
            {
                driverMotion.SetMode(VehicleHumanoidMotion.MotionMode.SitDown, 0.22f);
            }

            float drivingYaw = vehicleFrontSign > 0f ? 0f : 180f;
            Quaternion seatRotation = Quaternion.Euler(0f, drivingYaw, 0f);

            yield return MoveCharacterHips(
                driverCharacter,
                driverSeatHipsPosition,
                seatRotation,
                0.78f);

            if (driverMotion != null)
            {
                driverMotion.SetMode(VehicleHumanoidMotion.MotionMode.DriverSeated, 0.32f);
            }

            yield return new WaitForSeconds(0.12f);
            yield return CloseDoorRoutine();
            state = ExperienceState.Seated;
        }

        private IEnumerator CloseDoorRoutine()
        {
            if (doorPivot == null)
            {
                yield break;
            }

            Quaternion start = doorPivot.localRotation;
            Quaternion target = Quaternion.identity;
            const float duration = 0.55f;

            for (float time = 0f; time < duration; time += Time.deltaTime)
            {
                float t = Mathf.SmoothStep(0f, 1f, time / duration);
                doorPivot.localRotation = Quaternion.Slerp(start, target, t);
                yield return null;
            }

            doorPivot.localRotation = target;
        }

        private IEnumerator StartEngineRoutine()
        {
            state = ExperienceState.StartingEngine;

            if (engineStartSource != null)
            {
                engineStartSource.Play();
                yield return new WaitForSeconds(engineStartSource.clip.length * 0.9f);
            }
            else
            {
                yield return new WaitForSeconds(1.1f);
            }

            vehicleController.CanDrive = true;

            if (engineLoopSource != null)
            {
                engineLoopSource.Play();
            }

            if (cameraFollow != null)
            {
                cameraFollow.SetView(
                    drivingCameraOffset,
                    drivingCameraLookPoint,
                    1.05f);
            }

            state = ExperienceState.Driving;
        }

        private void BuildCharacters()
        {
            float driverHeight = CalculateRealisticCharacterHeight();
            float receiverHeight = driverHeight * 0.97f;
            float faceDoorYaw = driverSideSign < 0f ? 90f : -90f;

            GameObject driver = CreateCharacter(
                "Chauffeur",
                DriverResource,
                Vector3.zero,
                Quaternion.Euler(0f, faceDoorYaw, 0f),
                driverHeight);

            if (driver != null)
            {
                driverCharacter = driver.transform;
                driverMotion = driver.GetComponent<VehicleHumanoidMotion>();
                if (driverMotion == null)
                {
                    driverMotion = driver.AddComponent<VehicleHumanoidMotion>();
                }

                driverMotion.SetRole(VehicleHumanoidMotion.CharacterRole.Driver);
                driverMotion.SetMode(VehicleHumanoidMotion.MotionMode.Idle, 0.01f);
                PlaceCharacterFeet(driverCharacter, driverOutsideFeetPosition);
            }

            float receiverYaw = vehicleFrontSign > 0f ? 0f : 180f;
            GameObject receiver = CreateCharacter(
                "ApprentiReceveur",
                ReceiverResource,
                Vector3.zero,
                Quaternion.Euler(0f, receiverYaw, 0f),
                receiverHeight);

            if (receiver != null)
            {
                receiverMotion = receiver.GetComponent<VehicleHumanoidMotion>();
                if (receiverMotion == null)
                {
                    receiverMotion = receiver.AddComponent<VehicleHumanoidMotion>();
                }

                receiverMotion.SetRole(VehicleHumanoidMotion.CharacterRole.Receiver);
                receiverMotion.SetMode(VehicleHumanoidMotion.MotionMode.ReceiverRide, 0.01f);
                PlaceCharacterFeet(receiver.transform, receiverFeetPosition);
            }
        }

        private float CalculateRealisticCharacterHeight()
        {
            // Project units are meters. The vehicle collider is around minibus scale,
            // so keep an adult driver around 1.55–1.66 m instead of deriving a giant
            // character from roof-rack/render bounds.
            float height = Mathf.Max(1f, vehicleBoundsLocal.size.y);
            float width = Mathf.Max(0.5f, vehicleBoundsLocal.size.x);

            float fromVehicleHeight = height * 0.64f;
            float fromVehicleWidth = width * 1.06f;
            float target = Mathf.Min(fromVehicleHeight, fromVehicleWidth);

            return Mathf.Clamp(target, 1.52f, 1.66f);
        }

        private GameObject CreateCharacter(
            string characterName,
            string resourcePath,
            Vector3 localPosition,
            Quaternion localRotation,
            float targetHeight)
        {
            GameObject prefab = Resources.Load<GameObject>(resourcePath);
            if (prefab == null)
            {
                Debug.LogWarning(
                    $"Car Rapide: personnage '{characterName}' absent. " +
                    "Utilise Car Rapide > Vehicle > Download / Fix Driver & Receiver.");
                return null;
            }

            GameObject character = Instantiate(prefab, transform);
            character.name = characterName;
            NormalizeCharacterHeight(character, targetHeight);
            character.transform.localPosition = localPosition;
            character.transform.localRotation = localRotation;

            foreach (Collider collider in character.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = false;
            }

            return character;
        }

        private static void NormalizeCharacterHeight(GameObject character, float targetHeight)
        {
            Renderer[] renderers = character.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                return;
            }

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            if (bounds.size.y <= 0.01f)
            {
                return;
            }

            float scale = targetHeight / bounds.size.y;
            character.transform.localScale *= scale;
        }

        private void PlaceCharacterFeet(Transform character, Vector3 targetFeetPosition)
        {
            character.localPosition += targetFeetPosition - GetCharacterFeetLocal(character);
        }

        private Vector3 GetCharacterFeetLocal(Transform character)
        {
            Renderer[] renderers = character.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                return character.localPosition;
            }

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            Vector3 worldFeet = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            return transform.InverseTransformPoint(worldFeet);
        }

        private Vector3 GetCharacterHipsLocal(Transform character)
        {
            Animator animator = character.GetComponentInChildren<Animator>();
            Transform hips = animator != null ? animator.GetBoneTransform(HumanBodyBones.Hips) : null;

            if (hips != null)
            {
                return transform.InverseTransformPoint(hips.position);
            }

            Renderer[] renderers = character.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                return character.localPosition;
            }

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            Vector3 worldHips = new Vector3(bounds.center.x, bounds.min.y + bounds.size.y * 0.53f, bounds.center.z);
            return transform.InverseTransformPoint(worldHips);
        }

        private IEnumerator MoveCharacterFeet(
            Transform character,
            Vector3 targetFeetPosition,
            Quaternion targetRotation,
            float duration,
            float stepArcHeight)
        {
            Vector3 startPosition = character.localPosition;
            Quaternion startRotation = character.localRotation;

            Vector3 feetNow = GetCharacterFeetLocal(character);
            Vector3 targetRoot = startPosition + (targetFeetPosition - feetNow);

            for (float time = 0f; time < duration; time += Time.deltaTime)
            {
                float raw = Mathf.Clamp01(time / duration);
                float t = raw * raw * raw * (raw * (raw * 6f - 15f) + 10f);

                Vector3 position = Vector3.Lerp(startPosition, targetRoot, t);
                position.y += Mathf.Sin(raw * Mathf.PI) * stepArcHeight;

                character.localPosition = position;
                character.localRotation = Quaternion.Slerp(startRotation, targetRotation, t);
                yield return null;
            }

            character.localPosition = targetRoot;
            character.localRotation = targetRotation;
        }

        private IEnumerator MoveCharacterHips(
            Transform character,
            Vector3 targetHipsPosition,
            Quaternion targetRotation,
            float duration)
        {
            Vector3 startPosition = character.localPosition;
            Quaternion startRotation = character.localRotation;

            Vector3 hipsNow = GetCharacterHipsLocal(character);
            Vector3 targetRoot = startPosition + (targetHipsPosition - hipsNow);

            for (float time = 0f; time < duration; time += Time.deltaTime)
            {
                float raw = Mathf.Clamp01(time / duration);
                float t = raw * raw * raw * (raw * (raw * 6f - 15f) + 10f);

                Vector3 position = Vector3.Lerp(startPosition, targetRoot, t);
                position.y += Mathf.Sin(raw * Mathf.PI) * 0.018f;

                character.localPosition = position;
                character.localRotation = Quaternion.Slerp(startRotation, targetRotation, t);
                yield return null;
            }

            character.localPosition = targetRoot;
            character.localRotation = targetRotation;
        }

        private void BuildAudio()
        {
            engineStartSource = gameObject.AddComponent<AudioSource>();
            engineStartSource.playOnAwake = false;
            engineStartSource.spatialBlend = 0.35f;
            engineStartSource.volume = 0.30f;
            engineStartSource.clip = CreateEngineStartClip();

            engineLoopSource = gameObject.AddComponent<AudioSource>();
            engineLoopSource.playOnAwake = false;
            engineLoopSource.loop = true;
            engineLoopSource.spatialBlend = 0.55f;
            engineLoopSource.volume = 0.11f;
            engineLoopSource.clip = CreateEngineLoopClip();
        }

        private static AudioClip CreateEngineStartClip()
        {
            const int sampleRate = 44100;
            const float duration = 1.55f;
            int sampleCount = Mathf.CeilToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];
            double phase = 0d;
            System.Random random = new System.Random(75567);

            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)sampleRate;
                float normalized = t / duration;
                float frequency = Mathf.Lerp(34f, 78f, Mathf.SmoothStep(0f, 1f, normalized));
                phase += Math.PI * 2d * frequency / sampleRate;

                float envelope = Mathf.SmoothStep(0f, 1f, Mathf.Min(1f, normalized * 6f))
                                 * (1f - Mathf.SmoothStep(0.80f, 1f, normalized));

                float motor = (float)Math.Sin(phase) * 0.55f
                              + (float)Math.Sin(phase * 2.03d) * 0.22f
                              + (float)Math.Sin(phase * 0.51d) * 0.12f;

                float noise = ((float)random.NextDouble() * 2f - 1f) * 0.08f;
                samples[i] = Mathf.Clamp((motor + noise) * envelope * 0.38f, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("Procedural Engine Start", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip CreateEngineLoopClip()
        {
            const int sampleRate = 44100;
            int sampleCount = sampleRate;
            float[] samples = new float[sampleCount];
            double phase = 0d;

            for (int i = 0; i < sampleCount; i++)
            {
                float wobble = Mathf.Sin(i / (float)sampleRate * Mathf.PI * 2f * 4f) * 2.5f;
                float frequency = 55f + wobble;
                phase += Math.PI * 2d * frequency / sampleRate;

                float sample = (float)Math.Sin(phase) * 0.55f
                               + (float)Math.Sin(phase * 2d) * 0.22f
                               + (float)Math.Sin(phase * 3d) * 0.10f;

                samples[i] = sample * 0.24f;
            }

            AudioClip clip = AudioClip.Create("Procedural Engine Loop", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private void CreateOrMoveAnchor(string anchorName, Vector3 localPosition)
        {
            Transform existing = FindDeepChild(transform, anchorName);
            if (existing == null)
            {
                GameObject anchor = new GameObject(anchorName);
                existing = anchor.transform;
                existing.SetParent(transform, false);
            }

            existing.localPosition = localPosition;
            existing.localRotation = Quaternion.identity;
        }

        private bool TryFindNamedBounds(string prefix, out Bounds localBounds)
        {
            localBounds = default;
            Transform modelRoot = FindDeepChild(transform, "Car rapide");
            if (modelRoot == null)
            {
                return false;
            }

            bool initialized = false;
            foreach (Transform item in modelRoot.GetComponentsInChildren<Transform>(true))
            {
                if (!item.name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                foreach (Renderer renderer in item.GetComponentsInChildren<Renderer>(true))
                {
                    foreach (Vector3 corner in BoundsCorners(renderer.bounds))
                    {
                        Vector3 local = transform.InverseTransformPoint(corner);
                        if (!initialized)
                        {
                            localBounds = new Bounds(local, Vector3.zero);
                            initialized = true;
                        }
                        else
                        {
                            localBounds.Encapsulate(local);
                        }
                    }
                }
            }

            return initialized;
        }

        private static Transform FindDeepChild(Transform root, string exactName)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name.Equals(exactName, StringComparison.OrdinalIgnoreCase))
                {
                    return child;
                }
            }

            return null;
        }

        private static IEnumerable<Vector3> BoundsCorners(Bounds bounds)
        {
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;

            yield return new Vector3(min.x, min.y, min.z);
            yield return new Vector3(min.x, min.y, max.z);
            yield return new Vector3(min.x, max.y, min.z);
            yield return new Vector3(min.x, max.y, max.z);
            yield return new Vector3(max.x, min.y, min.z);
            yield return new Vector3(max.x, min.y, max.z);
            yield return new Vector3(max.x, max.y, min.z);
            yield return new Vector3(max.x, max.y, max.z);
        }

        private void OnGUI()
        {
            string instruction = state switch
            {
                ExperienceState.WaitingForDoor => "Moteur éteint — E : ouvrir la vraie porte du Car Rapide",
                ExperienceState.DoorOpening => "Ouverture de la porte...",
                ExperienceState.DoorOpen => "E : entrer par la porte conducteur",
                ExperienceState.Entering => "Le chauffeur monte puis s'installe au volant...",
                ExperienceState.Seated => "R : démarrer le moteur",
                ExperienceState.StartingEngine => "Démarrage du moteur...",
                ExperienceState.Driving => "Moteur démarré — W/S : accélérer/freiner • A/D : tourner • Espace : frein à main",
                _ => string.Empty
            };

            GUI.Box(new Rect(18f, 18f, 650f, 68f), instruction);
        }
    }
}
