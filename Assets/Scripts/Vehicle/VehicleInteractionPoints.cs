using UnityEngine;

namespace CarRapide.Vehicle
{
    /// <summary>Authored against the imported mesh, in metres. Never inferred from renderer bounds at runtime.</summary>
    public sealed class VehicleInteractionPoints : MonoBehaviour
    {
        public Transform driverOutside, doorHandle, doorPull, driverDoor, driverStep, driverCabin, driverSeat;
        public Transform wheelLeftHand, wheelRightHand, driverLeftFoot, driverRightFoot, ignition;
        public Transform receiverLeftFoot, receiverRightFoot, receiverHandGrip;
        public Transform passengerOutside, passengerStep, passengerCabin, passengerSeat, passengerDoorGrip;
        public Transform driverDoorMesh, driverMirror, rearDoorMesh, rearWindowMesh;
        public Vector3 ToLocal(Transform point) => transform.InverseTransformPoint(point.position);

        void OnDrawGizmosSelected()
        {
            var parent = transform.Find("InteractionPoints");
            if (!parent) return;
            Gizmos.color = new Color(0.2f, 0.9f, 0.65f);
            foreach (Transform t in parent)
            {
                Gizmos.DrawWireSphere(t.position, 0.035f);
                Gizmos.DrawLine(t.position, t.position + t.forward * 0.15f);
            }
        }
    }
}
