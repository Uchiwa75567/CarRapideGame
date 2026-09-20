using System;
using System.Collections;
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

        private static readonly Vector3 ExteriorCameraOffset = new Vector3(-4.2f, 2.6f, -4.5f);
        private static readonly Vector3 CabinCameraOffset = new Vector3(-1.65f, 1.75f, 0.7f);
        private static readonly Vector3 DrivingCameraOffset = new Vector3(0f, 4f, -7f);

        private static readonly Vector3 DriverOutsidePosition = new Vector3(-1.45f, 0f, 0.65f);
        private static readonly Vector3 DriverSeatPosition = new Vector3(-0.42f, 0.48f, 0.78f);
        private static readonly Vector3 ReceiverPosition = new Vector3(0.45f, 0.05f, -1.95f);

        private VehicleController vehicleController;
        private Rigidbody vehicleRigidbody;
        private VehicleCameraFollow cameraFollow;
        private Transform doorPivot;
        private Transform driverCharacter;
        private AudioSource engineStartSource;
        private AudioSource engineLoopSource;
        private ExperienceState state;

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
            cameraFollow = FindFirstObjectByType<VehicleCameraFollow>();
            if (cameraFollow != null)
            {
                cameraFollow.SetTarget(transform);
                cameraFollow.SetView(ExteriorCameraOffset, 1.15f, 0.2f);
            }

            BuildDoor();
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

        private IEnumerator OpenDoorRoutine()
        {
            state = ExperienceState.DoorOpening;

            Quaternion start = doorPivot.localRotation;
            Quaternion target = Quaternion.Euler(0f, -78f, 0f);
            const float duration = 0.7f;

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
            state = ExperienceState.Entering;

            if (cameraFollow != null)
            {
                cameraFollow.SetView(CabinCameraOffset, 1.25f, 0.45f);
            }

            Vector3 startPosition = driverCharacter.localPosition;
            Quaternion startRotation = driverCharacter.localRotation;
            Quaternion targetRotation = Quaternion.Euler(0f, 0f, 0f);

            const float duration = 1.15f;
            for (float time = 0f; time < duration; time += Time.deltaTime)
            {
                float t = Mathf.SmoothStep(0f, 1f, time / duration);
                driverCharacter.localPosition = Vector3.Lerp(startPosition, DriverSeatPosition, t);
                driverCharacter.localRotation = Quaternion.Slerp(startRotation, targetRotation, t);
                yield return null;
            }

            driverCharacter.localPosition = DriverSeatPosition;
            driverCharacter.localRotation = targetRotation;
            TryApplySeatedPose(driverCharacter.gameObject);

            yield return CloseDoorRoutine();
            state = ExperienceState.Seated;
        }

        private IEnumerator CloseDoorRoutine()
        {
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
                cameraFollow.SetView(DrivingCameraOffset, 1.2f, 1.15f);
            }

            state = ExperienceState.Driving;
        }

        private void BuildDoor()
        {
            GameObject pivot = new GameObject("DriverDoorPivot");
            pivot.transform.SetParent(transform);
            pivot.transform.localPosition = new Vector3(-0.78f, 0.35f, 0.72f);
            pivot.transform.localRotation = Quaternion.identity;
            doorPivot = pivot.transform;

            Material yellow = RuntimeMaterial(new Color(0.92f, 0.57f, 0.05f));
            Material blue = RuntimeMaterial(new Color(0.04f, 0.22f, 0.43f));

            CreateDoorPanel("DoorUpper", doorPivot, new Vector3(-0.04f, 0.95f, 0f), new Vector3(0.08f, 0.72f, 0.82f), yellow);
            CreateDoorPanel("DoorLower", doorPivot, new Vector3(-0.04f, 0.30f, 0f), new Vector3(0.08f, 0.58f, 0.82f), blue);
        }

        private void BuildCharacters()
        {
            driverCharacter = CreateCharacter(
                "Chauffeur",
                DriverResource,
                DriverOutsidePosition,
                Quaternion.Euler(0f, 90f, 0f),
                1.72f).transform;

            GameObject receiver = CreateCharacter(
                "ApprentiReceveur",
                ReceiverResource,
                ReceiverPosition,
                Quaternion.Euler(0f, 180f, 0f),
                1.68f);

            receiver.transform.SetParent(transform, false);
        }

        private GameObject CreateCharacter(
            string characterName,
            string resourcePath,
            Vector3 localPosition,
            Quaternion localRotation,
            float targetHeight)
        {
            GameObject prefab = Resources.Load<GameObject>(resourcePath);
            GameObject character;

            if (prefab != null)
            {
                character = Instantiate(prefab, transform);
                character.name = characterName;
                NormalizeCharacterHeight(character, targetHeight);
            }
            else
            {
                character = CreatePlaceholderHuman(characterName);
                character.transform.SetParent(transform, false);
            }

            character.transform.localPosition = localPosition;
            character.transform.localRotation = localRotation;
            return character;
        }

        private static void NormalizeCharacterHeight(GameObject character, float targetHeight)
        {
            Renderer[] renderers = character.GetComponentsInChildren<Renderer>();
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

        private GameObject CreatePlaceholderHuman(string characterName)
        {
            GameObject root = new GameObject(characterName + " Placeholder");

            Material skin = RuntimeMaterial(new Color(0.20f, 0.105f, 0.065f));
            Material shirt = RuntimeMaterial(new Color(0.055f, 0.08f, 0.14f));
            Material shorts = RuntimeMaterial(new Color(0.60f, 0.07f, 0.08f));

            CreateBodyPart("Torso", PrimitiveType.Capsule, root.transform, new Vector3(0f, 1.15f, 0f), new Vector3(0.35f, 0.48f, 0.22f), Quaternion.identity, shirt);
            CreateBodyPart("Head", PrimitiveType.Sphere, root.transform, new Vector3(0f, 1.82f, 0f), new Vector3(0.24f, 0.28f, 0.24f), Quaternion.identity, skin);
            CreateBodyPart("LeftArm", PrimitiveType.Capsule, root.transform, new Vector3(-0.43f, 1.15f, 0f), new Vector3(0.11f, 0.42f, 0.11f), Quaternion.Euler(0f, 0f, -8f), skin);
            CreateBodyPart("RightArm", PrimitiveType.Capsule, root.transform, new Vector3(0.43f, 1.15f, 0f), new Vector3(0.11f, 0.42f, 0.11f), Quaternion.Euler(0f, 0f, 8f), skin);
            CreateBodyPart("Shorts", PrimitiveType.Cube, root.transform, new Vector3(0f, 0.78f, 0f), new Vector3(0.55f, 0.38f, 0.28f), Quaternion.identity, shorts);
            CreateBodyPart("LeftLeg", PrimitiveType.Capsule, root.transform, new Vector3(-0.17f, 0.35f, 0f), new Vector3(0.13f, 0.42f, 0.13f), Quaternion.identity, skin);
            CreateBodyPart("RightLeg", PrimitiveType.Capsule, root.transform, new Vector3(0.17f, 0.35f, 0f), new Vector3(0.13f, 0.42f, 0.13f), Quaternion.identity, skin);

            return root;
        }

        private static void CreateBodyPart(
            string partName,
            PrimitiveType primitive,
            Transform parent,
            Vector3 localPosition,
            Vector3 localScale,
            Quaternion localRotation,
            Material material)
        {
            GameObject part = GameObject.CreatePrimitive(primitive);
            part.name = partName;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;
            part.transform.localRotation = localRotation;

            Collider collider = part.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
            }

            part.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static void CreateDoorPanel(
            string panelName,
            Transform parent,
            Vector3 localPosition,
            Vector3 localScale,
            Material material)
        {
            GameObject panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            panel.name = panelName;
            panel.transform.SetParent(parent, false);
            panel.transform.localPosition = localPosition;
            panel.transform.localScale = localScale;

            Collider collider = panel.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
            }

            panel.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static Material RuntimeMaterial(Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            Material material = new Material(shader);
            material.color = color;
            return material;
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
            const float duration = 1f;
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
                HumanPose pose = new HumanPose
                {
                    muscles = new float[HumanTrait.MuscleCount]
                };

                handler.GetHumanPose(ref pose);

                SetMuscle(ref pose, "Left Upper Leg Front-Back", -0.72f);
                SetMuscle(ref pose, "Right Upper Leg Front-Back", -0.72f);
                SetMuscle(ref pose, "Left Knee Stretch", -0.88f);
                SetMuscle(ref pose, "Right Knee Stretch", -0.88f);
                SetMuscle(ref pose, "Left Arm Front-Back", -0.32f);
                SetMuscle(ref pose, "Right Arm Front-Back", -0.32f);
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

        private void OnGUI()
        {
            string instruction = state switch
            {
                ExperienceState.WaitingForDoor => "Moteur éteint — E : ouvrir la porte",
                ExperienceState.DoorOpening => "Ouverture de la porte...",
                ExperienceState.DoorOpen => "E : s'installer au volant",
                ExperienceState.Entering => "Installation du chauffeur...",
                ExperienceState.Seated => "R : démarrer le moteur",
                ExperienceState.StartingEngine => "Démarrage du moteur...",
                ExperienceState.Driving => "Moteur démarré — W/S : accélérer/freiner • A/D : tourner • Espace : frein à main",
                _ => string.Empty
            };

            GUI.Box(new Rect(18f, 18f, 590f, 68f), instruction);
        }
    }
}
