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
        private AudioSource engineStartSource;
        private AudioSource engineLoopSource;
        private ExperienceState state;

        private Bounds vehicleBoundsLocal;
        private Vector3 driverOutsidePosition;
        private Vector3 driverSeatPosition;
        private Vector3 receiverPosition;
        private Vector3 exteriorCameraOffset;
        private Vector3 cabinCameraOffset;
        private Vector3 drivingCameraOffset;

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
            CalculateVehicleLayout();

            cameraFollow = FindFirstObjectByType<VehicleCameraFollow>();
            if (cameraFollow != null)
            {
                cameraFollow.SetTarget(transform);
                cameraFollow.SetView(exteriorCameraOffset, vehicleBoundsLocal.size.y * 0.55f, 0.2f);
            }

            SetupRealDriverDoor();
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

            if (state == ExperienceState.Driving && engineLoopSource != null)
            {
                float normalizedSpeed = Mathf.InverseLerp(0f, 70f, vehicleController.SpeedKmh);
                engineLoopSource.pitch = Mathf.Lerp(0.82f, 1.42f, normalizedSpeed);
                engineLoopSource.volume = Mathf.Lerp(0.11f, 0.24f, normalizedSpeed);
            }
        }

        private void CalculateVehicleLayout()
        {
            Transform modelRoot = FindDeepChild(transform, "Car rapide") ?? transform;
            Renderer[] renderers = modelRoot.GetComponentsInChildren<Renderer>(true);

            if (renderers.Length == 0)
            {
                vehicleBoundsLocal = new Bounds(Vector3.zero, new Vector3(1.6f, 2.1f, 3.8f));
            }
            else
            {
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

            float width = vehicleBoundsLocal.size.x;
            float height = vehicleBoundsLocal.size.y;
            float length = vehicleBoundsLocal.size.z;

            driverOutsidePosition = new Vector3(
                vehicleBoundsLocal.min.x - Mathf.Max(0.22f, width * 0.10f),
                vehicleBoundsLocal.min.y,
                vehicleBoundsLocal.center.z + length * 0.24f);

            driverSeatPosition = new Vector3(
                vehicleBoundsLocal.min.x + width * 0.27f,
                vehicleBoundsLocal.min.y + height * 0.32f,
                vehicleBoundsLocal.center.z + length * 0.27f);

            receiverPosition = new Vector3(
                vehicleBoundsLocal.center.x,
                vehicleBoundsLocal.min.y + 0.03f,
                vehicleBoundsLocal.min.z - Mathf.Max(0.08f, length * 0.025f));

            exteriorCameraOffset = new Vector3(
                -Mathf.Max(3.6f, width * 2.2f),
                Mathf.Max(2.4f, height * 1.25f),
                -Mathf.Max(3.4f, length * 0.85f));

            cabinCameraOffset = new Vector3(
                vehicleBoundsLocal.min.x - width * 0.18f,
                vehicleBoundsLocal.min.y + height * 0.72f,
                vehicleBoundsLocal.center.z + length * 0.30f);

            drivingCameraOffset = new Vector3(
                0f,
                Mathf.Max(3.8f, height * 1.75f),
                -Mathf.Max(6.8f, length * 1.75f));
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

            GameObject pivotObject = new GameObject("DriverDoorHinge_RealModel");
            pivotObject.transform.SetParent(transform, false);
            pivotObject.transform.localPosition = new Vector3(
                localDoorBounds.center.x,
                localDoorBounds.center.y,
                localDoorBounds.max.z);
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

            Quaternion start = doorPivot.localRotation;
            Quaternion target = Quaternion.Euler(0f, 72f, 0f);
            const float duration = 0.75f;

            for (float time = 0f; time < duration; time += Time.deltaTime)
            {
                float t = Mathf.SmoothStep(0f, 1f, time / duration);
                doorPivot.localRotation = Quaternion.Slerp(start, target, t);
                yield return null;
            }

            doorPivot.localRotation = target;
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
                    vehicleBoundsLocal.size.y * 0.62f,
                    0.45f);
            }

            Vector3 startPosition = driverCharacter.localPosition;
            Quaternion startRotation = driverCharacter.localRotation;
            Quaternion targetRotation = Quaternion.identity;

            const float duration = 1.15f;
            for (float time = 0f; time < duration; time += Time.deltaTime)
            {
                float t = Mathf.SmoothStep(0f, 1f, time / duration);
                driverCharacter.localPosition = Vector3.Lerp(startPosition, driverSeatPosition, t);
                driverCharacter.localRotation = Quaternion.Slerp(startRotation, targetRotation, t);
                yield return null;
            }

            driverCharacter.localPosition = driverSeatPosition;
            driverCharacter.localRotation = targetRotation;
            TryApplySeatedPose(driverCharacter.gameObject);

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
                    vehicleBoundsLocal.size.y * 0.55f,
                    1.15f);
            }

            state = ExperienceState.Driving;
        }

        private void BuildCharacters()
        {
            GameObject driver = CreateCharacter(
                "Chauffeur",
                DriverResource,
                driverOutsidePosition,
                Quaternion.Euler(0f, 90f, 0f),
                Mathf.Max(1.50f, vehicleBoundsLocal.size.y * 0.82f));

            if (driver != null)
            {
                driverCharacter = driver.transform;
                TryApplyStandingPose(driver, false);
            }

            GameObject receiver = CreateCharacter(
                "ApprentiReceveur",
                ReceiverResource,
                receiverPosition,
                Quaternion.Euler(0f, 180f, 0f),
                Mathf.Max(1.48f, vehicleBoundsLocal.size.y * 0.79f));

            if (receiver != null)
            {
                TryApplyStandingPose(receiver, true);
            }
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

        private static void TryApplyStandingPose(GameObject character, bool receiverPose)
        {
            Animator animator = character.GetComponentInChildren<Animator>();
            if (animator == null || animator.avatar == null || !animator.avatar.isHuman)
            {
                return;
            }

            try
            {
                HumanPoseHandler handler = new HumanPoseHandler(animator.avatar, animator.transform);
                HumanPose pose = new HumanPose { muscles = new float[HumanTrait.MuscleCount] };
                handler.GetHumanPose(ref pose);

                SetMuscle(ref pose, "Left Arm Down-Up", -0.88f);
                SetMuscle(ref pose, "Right Arm Down-Up", receiverPose ? 0.55f : -0.88f);
                SetMuscle(ref pose, "Left Arm Front-Back", 0.02f);
                SetMuscle(ref pose, "Right Arm Front-Back", receiverPose ? -0.18f : 0.02f);
                SetMuscle(ref pose, "Right Forearm Stretch", receiverPose ? -0.38f : 0f);

                handler.SetHumanPose(ref pose);
                handler.Dispose();
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Pose personnage non appliquée: {exception.Message}");
            }
        }

        private static void TryApplySeatedPose(GameObject character)
        {
            Animator animator = character.GetComponentInChildren<Animator>();
            if (animator == null || animator.avatar == null || !animator.avatar.isHuman)
            {
                return;
            }

            try
            {
                HumanPoseHandler handler = new HumanPoseHandler(animator.avatar, animator.transform);
                HumanPose pose = new HumanPose { muscles = new float[HumanTrait.MuscleCount] };
                handler.GetHumanPose(ref pose);

                SetMuscle(ref pose, "Left Upper Leg Front-Back", -0.72f);
                SetMuscle(ref pose, "Right Upper Leg Front-Back", -0.72f);
                SetMuscle(ref pose, "Left Knee Stretch", -0.88f);
                SetMuscle(ref pose, "Right Knee Stretch", -0.88f);
                SetMuscle(ref pose, "Left Arm Down-Up", -0.50f);
                SetMuscle(ref pose, "Right Arm Down-Up", -0.50f);
                SetMuscle(ref pose, "Left Arm Front-Back", -0.34f);
                SetMuscle(ref pose, "Right Arm Front-Back", -0.34f);
                SetMuscle(ref pose, "Left Forearm Stretch", -0.55f);
                SetMuscle(ref pose, "Right Forearm Stretch", -0.55f);

                handler.SetHumanPose(ref pose);
                handler.Dispose();
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Pose chauffeur non appliquée: {exception.Message}");
            }
        }

        private static void SetMuscle(ref HumanPose pose, string muscleName, float value)
        {
            string[] names = HumanTrait.MuscleName;
            for (int i = 0; i < names.Length; i++)
            {
                if (names[i] == muscleName)
                {
                    pose.muscles[i] = Mathf.Clamp(value, -1f, 1f);
                    return;
                }
            }
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
                ExperienceState.DoorOpen => "E : s'installer au volant",
                ExperienceState.Entering => "Installation du chauffeur...",
                ExperienceState.Seated => "R : démarrer le moteur",
                ExperienceState.StartingEngine => "Démarrage du moteur...",
                ExperienceState.Driving => "Moteur démarré — W/S : accélérer/freiner • A/D : tourner • Espace : frein à main",
                _ => string.Empty
            };

            GUI.Box(new Rect(18f, 18f, 650f, 68f), instruction);
        }
    }
}
