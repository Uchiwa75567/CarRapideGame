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
        private Vector3 viewOffsetVelocity;
        private Vector3 lookPointVelocity;
        private Vector3 currentViewOffset;
        private Vector3 targetViewOffset;
        private Vector3 currentLookLocalPoint;
        private Vector3 targetLookLocalPoint;
        private float viewTransitionTime = 0.2f;

        private void Awake()
        {
            currentViewOffset = localOffset;
            targetViewOffset = localOffset;
            currentLookLocalPoint = new Vector3(0f, lookHeight, 0f);
            targetLookLocalPoint = currentLookLocalPoint;
        }

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            positionVelocity = Vector3.zero;
        }

        public void SetView(Vector3 newLocalOffset, float newLookHeight, float transitionSeconds = 0.5f)
        {
            SetView(newLocalOffset, new Vector3(0f, newLookHeight, 0f), transitionSeconds);
        }

        public void SetView(Vector3 newLocalOffset, Vector3 newLookLocalPoint, float transitionSeconds = 0.5f)
        {
            targetViewOffset = newLocalOffset;
            targetLookLocalPoint = newLookLocalPoint;
            viewTransitionTime = Mathf.Max(0.01f, transitionSeconds);
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            currentViewOffset = Vector3.SmoothDamp(
                currentViewOffset,
                targetViewOffset,
                ref viewOffsetVelocity,
                viewTransitionTime);

            currentLookLocalPoint = Vector3.SmoothDamp(
                currentLookLocalPoint,
                targetLookLocalPoint,
                ref lookPointVelocity,
                viewTransitionTime);

            Vector3 desiredPosition = target.TransformPoint(currentViewOffset);

            transform.position = Vector3.SmoothDamp(
                transform.position,
                desiredPosition,
                ref positionVelocity,
                positionSmoothTime);

            Vector3 lookPoint = target.TransformPoint(currentLookLocalPoint);
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
