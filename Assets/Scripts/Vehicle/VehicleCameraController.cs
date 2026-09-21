using UnityEngine;

namespace CarRapide.Vehicle
{
    public sealed class VehicleCameraController : MonoBehaviour
    {
        VehicleCameraFollow follow;
        public void Initialize(Transform vehicle,VehicleInteractionPoints points)
        {
            follow=Camera.main.GetComponent<VehicleCameraFollow>(); follow.SetTarget(vehicle);
            Camera.main.fieldOfView=48; Camera.main.nearClipPlane=.05f;
            follow.SnapView(new Vector3(-4.7f,2.45f,4.9f),new Vector3(-.45f,1.05f,.95f));
        }
        public void DoorView() => follow.SetView(new Vector3(-3.8f,2.1f,3.25f),new Vector3(-.95f,1.1f,1.52f),.75f);
        public void BoardingView() => follow.SetView(new Vector3(-3.0f,2.05f,.92f),new Vector3(-.60f,1.23f,1.68f),.75f);
        public void SeatedView() => follow.SetView(new Vector3(-2.5f,1.95f,2.85f),new Vector3(-.55f,1.40f,1.55f),.75f);
        public void DrivingView()
        {
            follow.SetFollowDamping(.1f);
            follow.SetView(new Vector3(0,2.85f,-5.8f),new Vector3(0,1.1f,.2f),1.6f);
        }
        public void PassengerView() => follow.SetView(new Vector3(-1.0f,2.1f,-5.6f),new Vector3(0,1.15f,-1.75f),.8f);
        public void ExteriorView() => follow.SetView(new Vector3(-4.7f,2.45f,4.9f),new Vector3(-.45f,1.05f,.95f),1);
    }
}
