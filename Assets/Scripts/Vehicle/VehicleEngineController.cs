using System.Collections;
using UnityEngine;

namespace CarRapide.Vehicle
{
    public sealed class VehicleEngineController : MonoBehaviour
    {
        VehicleController vehicle;
        DriverController driver;
        VehicleDoorController door;
        VehicleCameraController cameraDirector;
        public VehicleAudioController Audio { get; private set; }
        public bool IsStarting { get; private set; }
        public bool IsRunning { get; private set; }
        public bool PassengerBusy { get; set; }
        public void Initialize(VehicleController controller, DriverController chauffeur, VehicleDoorController realDoor, VehicleCameraController camera)
        {
            vehicle=controller; driver=chauffeur; door=realDoor; cameraDirector=camera;
            Audio=gameObject.AddComponent<VehicleAudioController>(); Audio.Initialize(vehicle);
        }
        public void RequestStart()
        {
            if (IsStarting || IsRunning || !driver.IsSeated || !door.IsClosed || PassengerBusy) return;
            if (!Audio.HasClips) { Debug.LogError("Engine audio is missing. Ignition remains locked.",this); return; }
            IsStarting=true; StartCoroutine(StartEngine());
        }
        IEnumerator StartEngine()
        {
            yield return driver.TurnIgnition(Mathf.Max(1.5f,Audio.StartDuration), Audio.Crank);
            Audio.Run(); IsRunning=true; IsStarting=false; vehicle.CanDrive=true; cameraDirector.DrivingView();
        }
        void OnDisable() { if(vehicle)vehicle.CanDrive=false; }
    }
}
