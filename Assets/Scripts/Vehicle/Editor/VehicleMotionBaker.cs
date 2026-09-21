#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using CarRapide.Vehicle;

namespace CarRapide.EditorTools
{
    /// <summary>Captures the authored contact performance as editable, retargetable Humanoid clips.</summary>
    public static class VehicleMotionBaker
    {
        [Serializable] public class Sample { public string clip; public float time; public float[] muscles; }
        [Serializable] public class Recording { public List<Sample> samples=new List<Sample>(); }
        static Recording recording;
        static HumanPoseHandler handler;
        static HumanPose human;
        static DriverAnimationController driver;
        static float nextSample;
        const string CapturePath="Library/VehicleReview/humanoid-performance.json";
        public static void Begin(DriverAnimationController animation)
        {
            End(); recording=new Recording(); driver=animation;
            handler=new HumanPoseHandler(animation.Rig.Animator.avatar,animation.Rig.Animator.transform);
            human=new HumanPose {muscles=new float[HumanTrait.MuscleCount]};
            nextSample=0; EditorApplication.update+=SamplePose;
        }
        static void SamplePose()
        {
            if (!driver || !EditorApplication.isPlaying) {End();return;}
            if(Time.time<nextSample) return;
            nextSample=Time.time+1f/15;
            handler.GetHumanPose(ref human);
            recording.samples.Add(new Sample {clip=driver.ClipName,time=Time.time-driver.ClipStartedAt,muscles=(float[])human.muscles.Clone()});
        }
        public static void End()
        {
            EditorApplication.update-=SamplePose;
            handler?.Dispose(); handler=null; driver=null;
            if(recording!=null) File.WriteAllText(CapturePath,JsonUtility.ToJson(recording));
            recording=null;
        }
        [MenuItem("Car Rapide/Vehicle/Bake captured Humanoid performance")]
        public static void Bake()
        {
            if(EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before baking the clips.");
            var capture=JsonUtility.FromJson<Recording>(File.ReadAllText(CapturePath));
            var groups=new Dictionary<string,List<Sample>>();
            // A state can be visited more than once (idle before/after ignition). Keep
            // its longest continuous performance instead of mixing overlapping times.
            var segments=new List<List<Sample>>();
            foreach(var sample in capture.samples)
            {
                if(string.IsNullOrEmpty(sample.clip))continue;
                if(segments.Count==0 || segments[segments.Count-1][0].clip!=sample.clip ||
                    sample.time<segments[segments.Count-1][segments[segments.Count-1].Count-1].time)
                    segments.Add(new List<Sample>());
                segments[segments.Count-1].Add(sample);
            }
            foreach(var segment in segments)
            {
                string name=segment[0].clip;
                if(!groups.ContainsKey(name) || segment.Count>groups[name].Count) groups[name]=segment;
            }
            foreach(var group in groups)
            {
                if(group.Value.Count<3)continue;
                var path="Assets/Resources/CarRapide/Animation/"+group.Key+".anim";
                var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if(!clip)continue;
                // Contact targets carry translation at runtime. Muscle curves retarget the complete body pose.
                clip.ClearCurves();
                float offset=group.Value[0].time;
                for(int muscle=0;muscle<HumanTrait.MuscleCount;muscle++)
                {
                    var curve=new AnimationCurve();
                    var samples=group.Value;
                    var keys=new SortedSet<int> {0,samples.Count-1};
                    Simplify(samples,muscle,0,samples.Count-1,keys);
                    foreach(int index in keys)
                        curve.AddKey(new Keyframe(samples[index].time-offset,samples[index].muscles[muscle]));
                    for(int k=0;k<curve.length;k++)
                    {AnimationUtility.SetKeyLeftTangentMode(curve,k,AnimationUtility.TangentMode.ClampedAuto);AnimationUtility.SetKeyRightTangentMode(curve,k,AnimationUtility.TangentMode.ClampedAuto);}
                    clip.SetCurve("",typeof(Animator),HumanTrait.MuscleName[muscle],curve);
                }
                var settings=AnimationUtility.GetAnimationClipSettings(clip);
                settings.loopTime=group.Key.EndsWith("Idle") || group.Key.EndsWith("Outside");
                AnimationUtility.SetAnimationClipSettings(clip,settings); EditorUtility.SetDirty(clip);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("Baked complete Humanoid performance into "+groups.Count+" editable clips.");
        }

        static void Simplify(List<Sample> samples,int muscle,int first,int last,SortedSet<int> keys)
        {
            if(last-first<2) return;
            float greatest=.002f; int split=-1;
            for(int i=first+1;i<last;i++)
            {
                float t=Mathf.InverseLerp(samples[first].time,samples[last].time,samples[i].time);
                float error=Mathf.Abs(samples[i].muscles[muscle]-Mathf.Lerp(samples[first].muscles[muscle],samples[last].muscles[muscle],t));
                if(error>greatest) {greatest=error;split=i;}
            }
            if(split<0)return;
            keys.Add(split); Simplify(samples,muscle,first,split,keys); Simplify(samples,muscle,split,last,keys);
        }
    }
}
#endif
