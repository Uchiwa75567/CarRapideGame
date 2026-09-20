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
        private float lookHeightVelocity;
        private Vector3 currentViewOffset;
        private Vector3 targetViewOffset;
        private float currentLookHeight;
        private float targetLookHeight;
        private float viewTransitionTime = 0.2f;

        private void Awake()
        {
            currentViewOffset = localOffset;
            targetViewOffset = localOffset;
            currentLookHeight = lookHeight;
            targetLookHeight = lookHeight;
        }

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            positionVelocity = Vector3.zero;
        }

        public void SetView(Vector3 newLocalOffset, float newLookHeight, float transitionSeconds = 0.5f)
        {
            targetViewOffset = newLocalOffset;
            targetLookHeight = newLookHeight;
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

            currentLookHeight = Mathf.SmoothDamp(
                currentLookHeight,
                targetLookHeight,
                ref lookHeightVelocity,
                viewTransitionTime);

            Vector3 desiredPosition = target.TransformPoint(currentViewOffset);

            transform.position = Vector3.SmoothDamp(
                transform.position,
                desiredPosition,
                ref positionVelocity,
                positionSmoothTime);

            Vector3 lookPoint = target.position + Vector3.up * currentLookHeight;
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
