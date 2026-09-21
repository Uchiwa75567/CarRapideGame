using UnityEngine;
using UnityEngine.InputSystem;

namespace CarRapide.Vehicle
{
    [RequireComponent(typeof(VehicleController), typeof(VehicleInteractionPoints))]
    public sealed class VehicleDriverExperience : MonoBehaviour
    {
        public DriverController Driver { get; private set; }
        public VehicleEngineController Engine { get; private set; }
        public PassengerController Passenger { get; private set; }
        public VehicleDoorController Door { get; private set; }
        public VehicleCameraController CameraDirector { get; private set; }
        public bool Ready { get; private set; }
        GUIStyle titleStyle, labelStyle, keyStyle;

        void Awake() { GetComponent<VehicleController>().CanDrive = false; }
        void Start()
        {
            var points = GetComponent<VehicleInteractionPoints>();
            if (!points.driverDoorMesh || !points.driverSeat || !points.receiverLeftFoot)
            { Debug.LogError("Car Rapide: install the calibrated experience using the Vehicle menu.",this); return; }
            Door = gameObject.AddComponent<VehicleDoorController>(); Door.Initialize(points);
            CameraDirector = gameObject.AddComponent<VehicleCameraController>(); CameraDirector.Initialize(transform,points);
            var driver = CreateCharacter("Chauffeur", "Black_M_1_Casual", 1.72f);
            var receiver = CreateCharacter("ApprentiReceveur", "Black_M_2_Casual", 1.70f);
            if (!driver || !receiver) return;
            Driver = driver.AddComponent<DriverController>(); Driver.Initialize(points,Door,CameraDirector);
            receiver.AddComponent<ReceiverController>().Initialize(points,GetComponent<Rigidbody>());
            Engine = gameObject.AddComponent<VehicleEngineController>(); Engine.Initialize(GetComponent<VehicleController>(),Driver,Door,CameraDirector);
            Passenger = gameObject.AddComponent<PassengerController>(); Passenger.Initialize(this,points);
            Ready = true;
        }

        public GameObject CreateCharacter(string characterName, string resource, float height)
        {
            var prefab = Resources.Load<GameObject>("CarRapide/Characters/"+resource);
            if (!prefab) { Debug.LogError("Missing bundled character: " + resource,this); return null; }
            var go = Instantiate(prefab,transform,false); go.name = characterName;
            var animator = go.GetComponent<Animator>();
            if (!animator || !animator.avatar || !animator.avatar.isHuman || !animator.avatar.isValid)
            { Debug.LogError("Character needs a valid Humanoid avatar: " + resource,this); Destroy(go); return null; }
            // Bake the rest mesh; renderer bounds include oversized animation padding.
            float min=float.PositiveInfinity,max=float.NegativeInfinity;
            foreach (var r in go.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var mesh = new Mesh(); r.BakeMesh(mesh);
                foreach (var v in mesh.vertices)
                {
                    float y=go.transform.InverseTransformPoint(r.transform.TransformPoint(v)).y;
                    min=Mathf.Min(min,y); max=Mathf.Max(max,y);
                }
                Destroy(mesh); r.updateWhenOffscreen = true;
            }
            go.transform.localScale = Vector3.one*(height/(max-min));
            animator.runtimeAnimatorController = Resources.Load<RuntimeAnimatorController>("CarRapide/Animation/Driver");
            foreach(var collider in go.GetComponentsInChildren<Collider>()) collider.enabled=false;
            return go;
        }

        void Update()
        {
            if (!Ready || Keyboard.current == null) return;
            if (Keyboard.current.eKey.wasPressedThisFrame) Driver.RequestBoard();
            if (Keyboard.current.rKey.wasPressedThisFrame) Engine.RequestStart();
            if (Keyboard.current.pKey.wasPressedThisFrame) Passenger.RequestDemo();
        }

        void OnGUI()
        {
            if (!Ready) return;
            var previousMatrix = GUI.matrix;
            float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one * scale);
            float width = Screen.width / scale;
            float height = Screen.height / scale;
            titleStyle ??= new GUIStyle(GUI.skin.label) {fontSize=18,fontStyle=FontStyle.Bold,normal={textColor=new Color(1,.83f,.35f)}};
            labelStyle ??= new GUIStyle(GUI.skin.label) {fontSize=14,normal={textColor=Color.white}};
            keyStyle ??= new GUIStyle(GUI.skin.label) {fontSize=12,normal={textColor=new Color(.74f,.8f,.82f)}};
            var old=GUI.color; GUI.color=new Color(.04f,.075f,.085f,.91f);
            GUI.DrawTexture(new Rect(24,height-114,Mathf.Min(680,width-48),90),Texture2D.whiteTexture);
            GUI.color=old;
            GUI.Label(new Rect(42,height-108,600,28),"CAR RAPIDE  /  PREMIER DÉPART",titleStyle);
            string instruction=Engine.IsRunning ? "Moteur en marche · " + GetComponent<VehicleController>().SpeedKmh.ToString("0")+" km/h"
                : Engine.IsStarting ? "Démarrage du moteur…" : Passenger.IsBusy ? "Montée et descente du passager…" : Driver.IsSeated ? "R  ·  Démarrer le moteur" : Driver.IsBoarding ? Driver.CurrentAction : "E  ·  Prendre place au volant";
            GUI.Label(new Rect(42,height-78,600,25),instruction,labelStyle);
            string hint = Engine.IsRunning ? "W / S  Accélérer · Freiner · Reculer     A / D  Direction     Espace  Frein à main"
                : Engine.IsStarting ? "Mise en route · Conduite verrouillée" : "À l’arrêt · Moteur éteint                         P  Démonstration passager";
            GUI.Label(new Rect(42,height-51,630,22),hint,keyStyle);
            GUI.matrix = previousMatrix;
        }
    }
}
