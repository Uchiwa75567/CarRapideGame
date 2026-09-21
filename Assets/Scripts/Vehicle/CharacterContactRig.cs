using UnityEngine;

namespace CarRapide.Vehicle
{
    /// <summary>Final contact pass after Animator: pelvis, two-bone limbs and explicit knee/elbow poles.</summary>
    [DefaultExecutionOrder(100)]
    public sealed class CharacterContactRig : MonoBehaviour
    {
        public struct Pose
        {
            public Vector3 pelvis, leftFoot, rightFoot, leftHand, rightHand;
            public float yaw, leftFootYaw, rightFootYaw, lean, roll, leftHandWeight, rightHandWeight, leftGrip, rightGrip;
            public static Pose Blend(Pose a, Pose b, float t)
            {
                return new Pose {
                    pelvis = Vector3.LerpUnclamped(a.pelvis,b.pelvis,t),
                    leftFoot = Vector3.LerpUnclamped(a.leftFoot,b.leftFoot,t),
                    rightFoot = Vector3.LerpUnclamped(a.rightFoot,b.rightFoot,t),
                    leftHand = Vector3.LerpUnclamped(a.leftHand,b.leftHand,t),
                    rightHand = Vector3.LerpUnclamped(a.rightHand,b.rightHand,t),
                    yaw = Mathf.LerpAngle(a.yaw,b.yaw,t),
                    leftFootYaw = Mathf.LerpAngle(a.leftFootYaw,b.leftFootYaw,t),
                    rightFootYaw = Mathf.LerpAngle(a.rightFootYaw,b.rightFootYaw,t),
                    lean = Mathf.Lerp(a.lean,b.lean,t), roll = Mathf.Lerp(a.roll,b.roll,t),
                    leftHandWeight = Mathf.Lerp(a.leftHandWeight,b.leftHandWeight,t),
                    rightHandWeight = Mathf.Lerp(a.rightHandWeight,b.rightHandWeight,t)
                    ,leftGrip=Mathf.Lerp(a.leftGrip,b.leftGrip,t),rightGrip=Mathf.Lerp(a.rightGrip,b.rightGrip,t)
                };
            }
        }

        sealed class Limb
        {
            public Transform upper, lower, tip;
            public Quaternion tipRest;
            public float a, b;
        }
        Animator animator;
        Transform frame, hips, spine;
        Quaternion hipsRest, spineRest;
        Limb leftLeg, rightLeg, leftArm, rightArm;
        Pose pose;
        bool ready;
        public Animator Animator => animator;
        public float FootHeight { get; private set; }
        public float MaxContactError { get; private set; }
        public Vector4 ContactErrors { get; private set; }
        public Pose CurrentPose => pose;

        public void Initialize(Transform coordinateFrame)
        {
            frame = coordinateFrame;
            animator = GetComponent<Animator>();
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.applyRootMotion = false;
            hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            spine = animator.GetBoneTransform(HumanBodyBones.Spine);
            hipsRest = Quaternion.Inverse(frame.rotation) * hips.rotation;
            spineRest = spine.localRotation;
            leftLeg = MakeLimb(HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot);
            rightLeg = MakeLimb(HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot);
            leftArm = MakeLimb(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand);
            rightArm = MakeLimb(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand);
            FootHeight = frame.InverseTransformPoint(leftLeg.tip.position).y - transform.localPosition.y;
            ready = true;
        }

        Limb MakeLimb(HumanBodyBones a, HumanBodyBones b, HumanBodyBones c)
        {
            var l = new Limb { upper = animator.GetBoneTransform(a), lower = animator.GetBoneTransform(b), tip = animator.GetBoneTransform(c) };
            l.a = Vector3.Distance(l.upper.position,l.lower.position);
            l.b = Vector3.Distance(l.lower.position,l.tip.position);
            l.tipRest = Quaternion.Inverse(frame.rotation) * l.tip.rotation;
            return l;
        }
        public void SetPose(Pose value) { pose = value; }

        void LateUpdate() { if (ready) ApplyPose(); }
        public void ApplyPose()
        {
            Quaternion facing = frame.rotation * Quaternion.Euler(0,pose.yaw,0);
            hips.rotation = facing * Quaternion.Euler(pose.lean * 0.3f,0,pose.roll * 0.3f) * hipsRest;
            hips.position = frame.TransformPoint(pose.pelvis);
            spine.localRotation = spineRest;
            spine.rotation = facing * Quaternion.Euler(pose.lean,0,pose.roll) * Quaternion.Inverse(facing) * spine.rotation;
            Vector3 forward = facing * Vector3.forward;
            Vector3 right = facing * Vector3.right;
            MaxContactError = 0;
            float e0=Solve(leftLeg, frame.TransformPoint(pose.leftFoot + Vector3.up * FootHeight), hips.position + forward - right * 0.16f,
                frame.rotation * Quaternion.Euler(0,pose.leftFootYaw,0) * leftLeg.tipRest, 1);
            float e1=Solve(rightLeg, frame.TransformPoint(pose.rightFoot + Vector3.up * FootHeight), hips.position + forward + right * 0.16f,
                frame.rotation * Quaternion.Euler(0,pose.rightFootYaw,0) * rightLeg.tipRest, 1);
            float e2=Solve(leftArm, frame.TransformPoint(pose.leftHand), hips.position - right * 0.6f + forward * 0.12f,
                facing * Quaternion.Slerp(Quaternion.Euler(0,0,75),Quaternion.Euler(25,90,0),pose.leftGrip) * leftArm.tipRest, pose.leftHandWeight);
            float e3=Solve(rightArm, frame.TransformPoint(pose.rightHand), hips.position + right * 0.6f + forward * 0.12f,
                facing * Quaternion.Slerp(Quaternion.Euler(0,0,-75),Quaternion.Euler(25,-90,0),pose.rightGrip) * rightArm.tipRest, pose.rightHandWeight);
            ContactErrors=new Vector4(e0,e1,e2,e3);
        }

        float Solve(Limb limb, Vector3 target, Vector3 pole, Quaternion tipRotation, float weight)
        {
            if (weight <= 0) return 0;
            target = Vector3.Lerp(limb.tip.position, target, weight);
            Vector3 origin = limb.upper.position;
            Vector3 delta = target - origin;
            float distance = Mathf.Clamp(delta.magnitude, Mathf.Abs(limb.a-limb.b)+0.001f, limb.a+limb.b-0.001f);
            Vector3 direction = delta.normalized;
            Vector3 bend = Vector3.ProjectOnPlane(pole-origin,direction).normalized;
            if (bend.sqrMagnitude < 0.01f) bend = Vector3.Cross(direction,frame.right).normalized;
            float along = (limb.a*limb.a - limb.b*limb.b + distance*distance) / (2*distance);
            float height = Mathf.Sqrt(Mathf.Max(0,limb.a*limb.a-along*along));
            Vector3 joint = origin + direction * along + bend * height;
            limb.upper.rotation = Quaternion.FromToRotation(limb.lower.position-origin,joint-origin) * limb.upper.rotation;
            limb.lower.rotation = Quaternion.FromToRotation(limb.tip.position-limb.lower.position,target-limb.lower.position) * limb.lower.rotation;
            limb.tip.rotation = Quaternion.Slerp(limb.tip.rotation,tipRotation,weight);
            float error=weight>0.99f ? Vector3.Distance(limb.tip.position,target):0;
            MaxContactError = Mathf.Max(MaxContactError,error);
            return error;
        }
    }
}
