using UnityEngine;

namespace CarRapide.Vehicle
{
    public sealed class VehicleDoorController : MonoBehaviour
    {
        Transform door, mirror, handle;
        Quaternion closed;
        Vector3 hingeAxis;
        public float OpenAngle { get; private set; }
        public bool IsClosed => Mathf.Abs(OpenAngle) < 0.5f;

        public void Initialize(VehicleInteractionPoints points)
        {
            door = points.driverDoorMesh;
            mirror = points.driverMirror;
            handle = points.doorHandle;
            closed = door.localRotation;
            hingeAxis = door.parent.InverseTransformDirection(points.transform.up);
            // The FBX already has a hinge at its front edge. The .001 object is the RIGHT mirror.
            // Preserve world transforms when attaching the actual LEFT mirror and handle.
            mirror.SetParent(door, true);
            handle.SetParent(door, true);
            points.doorPull.SetParent(door,true);
        }

        public void SetOpening(float degrees)
        {
            OpenAngle = Mathf.Clamp(degrees, 0, 78);
            door.localRotation = Quaternion.AngleAxis(OpenAngle, hingeAxis) * closed;
        }
    }
}
