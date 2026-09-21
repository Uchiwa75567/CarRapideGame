using UnityEngine;

namespace CarRapide.Vehicle
{
    [DefaultExecutionOrder(10)]
    public sealed class ReceiverController : MonoBehaviour
    {
        CharacterContactRig rig;
        VehicleInteractionPoints points;
        Rigidbody body;
        Vector3 previousVelocity, filteredAcceleration, swayVelocity;
        CharacterContactRig.Pose rest;
        public void Initialize(VehicleInteractionPoints layout, Rigidbody vehicleBody)
        {
            points=layout; body=vehicleBody;
            rig=gameObject.AddComponent<CharacterContactRig>(); rig.Initialize(points.transform);
            rest=DriverController.Standing((points.ToLocal(points.receiverLeftFoot)+points.ToLocal(points.receiverRightFoot))*.5f,0);
            rest.leftFoot=points.ToLocal(points.receiverLeftFoot); rest.rightFoot=points.ToLocal(points.receiverRightFoot);
            rest.pelvis.y-=.085f; rest.pelvis.z-=.04f;
            rest.rightHand=points.ToLocal(points.receiverHandGrip); rest.lean=6;
            rig.SetPose(rest); rig.Animator.Play("Receiver_Ride"); previousVelocity=body.linearVelocity;
        }
        void Update()
        {
            if (!rig) return;
            Vector3 velocity=body.linearVelocity;
            var acceleration=points.transform.InverseTransformDirection((velocity-previousVelocity)/Mathf.Max(Time.deltaTime,.001f));
            previousVelocity=velocity;
            filteredAcceleration=Vector3.SmoothDamp(filteredAcceleration,Vector3.ClampMagnitude(acceleration,9),ref swayVelocity,.3f);
            float moving=Mathf.Clamp01(velocity.magnitude/10);
            var p=rest;
            p.pelvis += new Vector3(-filteredAcceleration.x*.005f, -Mathf.Abs(Mathf.Sin(Time.time*2.3f))*.009f*moving, -filteredAcceleration.z*.004f);
            p.roll=Mathf.Clamp(filteredAcceleration.x*1.1f,-7,7); p.lean=6+Mathf.Clamp(filteredAcceleration.z*.8f,-5,5);
            p.leftHand += new Vector3(Mathf.Sin(Time.time*1.4f)*.015f,Mathf.Sin(Time.time*1.9f)*.008f,0);
            rig.SetPose(p);
        }
    }
}
