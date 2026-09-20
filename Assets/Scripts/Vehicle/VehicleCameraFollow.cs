using UnityEngine;

namespace CarRapide.Vehicle
{
    public sealed class VehicleCameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 localOffset = new Vector3(0f, 4f, -7f);
        [SerializeField, Min(0.01f)] private float positionSmoothTime = 0.15f;
        [SerializeField, Min(0f)] private float rotationSharpness = 8f;
        [SerializeField] private float lookHeight = 1.2f;

        private Vector3 positionVelocity;

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            positionVelocity = Vector3.zero;
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            Vector3 desiredPosition = target.TransformPoint(localOffset);

            transform.position = Vector3.SmoothDamp(
                transform.position,
                desiredPosition,
                ref positionVelocity,
                positionSmoothTime);

            Vector3 lookPoint = target.position + Vector3.up * lookHeight;
            Vector3 lookDirection = lookPoint - transform.position;

            if (lookDirection.sqrMagnitude <= 0.001f)
            {
                return;
            }

            Quaternion desiredRotation = Quaternion.LookRotation(lookDirection, Vector3.up);
            float rotationBlend = 1f - Mathf.Exp(-rotationSharpness * Time.deltaTime);

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                desiredRotation,
                rotationBlend);
        }
    }
}
