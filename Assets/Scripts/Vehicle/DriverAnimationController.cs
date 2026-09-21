using System.Collections;
using UnityEngine;

namespace CarRapide.Vehicle
{
    public sealed class DriverAnimationController : MonoBehaviour
    {
        public CharacterContactRig Rig { get; private set; }
        public string ClipName { get; private set; }
        public float ClipStartedAt { get; private set; }
        public void Initialize(Transform frame)
        {
            Rig = gameObject.AddComponent<CharacterContactRig>();
            Rig.Initialize(frame);
        }
        public void Play(string clip, float blend = 0.22f)
        {
            ClipName=clip; ClipStartedAt=Time.time;
            Rig.Animator.CrossFadeInFixedTime(clip,blend);
        }
        public IEnumerator Transition(CharacterContactRig.Pose target, float duration, float leftLift = 0, float rightLift = 0, System.Action<float> onProgress = null)
        {
            var start = Rig.CurrentPose;
            for (float elapsed=0; elapsed<duration; elapsed += Time.deltaTime)
            {
                float t = Mathf.Clamp01(elapsed/duration);
                float u = t*t*t*(t*(6*t-15)+10);
                var p = CharacterContactRig.Pose.Blend(start,target,u);
                // A planted foot has no lift and identical endpoints throughout a support phase.
                float arc = 16*t*t*(1-t)*(1-t);
                p.leftFoot.y += arc*leftLift;
                p.rightFoot.y += arc*rightLift;
                Rig.SetPose(p);
                onProgress?.Invoke(u);
                yield return null;
            }
            Rig.SetPose(target); onProgress?.Invoke(1);
        }
    }
}
