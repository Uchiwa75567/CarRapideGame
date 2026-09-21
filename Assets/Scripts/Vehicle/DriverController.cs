using System.Collections;
using UnityEngine;
using Pose = CarRapide.Vehicle.CharacterContactRig.Pose;

namespace CarRapide.Vehicle
{
    public sealed class DriverController : MonoBehaviour
    {
        VehicleInteractionPoints points;
        VehicleDoorController door;
        VehicleCameraController cameraDirector;
        DriverAnimationController motion;
        public bool IsSeated { get; private set; }
        public bool IsBoarding { get; private set; }
        public string CurrentAction { get; private set; }
        public CharacterContactRig Rig => motion.Rig;

        public void Initialize(VehicleInteractionPoints layout, VehicleDoorController realDoor, VehicleCameraController camera)
        {
            points=layout; door=realDoor; cameraDirector=camera;
            motion=gameObject.AddComponent<DriverAnimationController>(); motion.Initialize(layout.transform);
            var p = Standing(layout.ToLocal(layout.driverOutside),90);
            motion.Rig.SetPose(p); motion.Play("Driver_IdleOutside",0); motion.Rig.ApplyPose();
        }
        public static Pose Standing(Vector3 feet, float yaw)
        {
            var q=Quaternion.Euler(0,yaw,0);
            var p = new Pose {pelvis=feet+Vector3.up*.946f,yaw=yaw,leftFootYaw=yaw,rightFootYaw=yaw,
                leftFoot=feet+q*new Vector3(-.12f,0,.035f),rightFoot=feet+q*new Vector3(.12f,0,-.035f),leftHandWeight=1,rightHandWeight=1};
            RelaxHands(ref p); return p;
        }
        public static void RelaxHands(ref Pose p)
        {
            var q=Quaternion.Euler(0,p.yaw,0);
            p.leftHand=p.pelvis+q*new Vector3(-.25f,-.03f,.065f);
            p.rightHand=p.pelvis+q*new Vector3(.25f,-.03f,.065f);
        }
        public void RequestBoard()
        {
            if (IsBoarding || IsSeated || points.GetComponent<VehicleDriverExperience>().Engine.PassengerBusy) return;
            IsBoarding=true; StartCoroutine(Board());
        }
        void Phase(string clip,string caption) { CurrentAction=caption; motion.Play(clip); }

        IEnumerator Board()
        {
            var p=Rig.CurrentPose;
            Phase("Driver_WalkToDoor","Le chauffeur rejoint la porte…");
            p.pelvis += new Vector3(.19f,0,.045f); p.leftFoot += new Vector3(.38f,0,.08f); RelaxHands(ref p);
            yield return motion.Transition(p,.8f,.075f);
            p.pelvis += new Vector3(.19f,0,.035f); p.rightFoot += new Vector3(.38f,0,.08f); RelaxHands(ref p);
            yield return motion.Transition(p,.8f,0,.075f);
            cameraDirector.DoorView();
            Phase("Driver_GrabHandle","Main sur la poignée…");
            p.leftHand=points.ToLocal(points.doorHandle); p.lean=8;
            yield return motion.Transition(p,.95f);
            Phase("Driver_OpenDoor","Ouverture de la porte…");
            var hold=p;
            for(float t=0;t<1.6f;t+=Time.deltaTime)
            {
                float u=Mathf.SmoothStep(0,1,t/1.6f); door.SetOpening(76*u);
                hold.pelvis=p.pelvis+new Vector3(-.07f,0,.20f)*u;
                RelaxHands(ref hold);
                hold.leftHand=points.ToLocal(points.doorHandle);
                Rig.SetPose(hold); yield return null;
            }
            door.SetOpening(76); p=hold;
            p.rightHandWeight=0; p.leftHand=new Vector3(-1.045f,1.49f,2.02f); p.leftGrip=.65f;
            p.pelvis=new Vector3(-1.43f,.91f,1.57f); p.rightFoot=new Vector3(-1.50f,-.03634f,1.43f);
            yield return motion.Transition(p,1.0f,0,.1f);
            cameraDirector.BoardingView();
            Phase("Driver_StepUp","Premier appui sur le marchepied…");
            p.leftFoot=points.ToLocal(points.driverStep); p.pelvis=new Vector3(-1.29f,.91f,1.72f); p.lean=20;
            p.leftHand=new Vector3(-1.045f,1.49f,2.02f);
            yield return motion.Transition(p,1.1f,.20f);
            Phase("Driver_EnterCabin","Transfert du poids et entrée dans la cabine…");
            p.pelvis=new Vector3(-1.10f,.96f,1.61f); p.lean=25;
            p.rightFoot += new Vector3(.08f,.20f,.25f);
            yield return motion.Transition(p,.9f);
            p.rightFoot=new Vector3(-.76f,.336f,2.02f); p.pelvis=new Vector3(-.89f,1.05f,1.52f);
            p.leftHand=points.ToLocal(points.wheelLeftHand); p.leftGrip=1;
            yield return motion.Transition(p,1.05f,0,.21f);
            Phase("Driver_TurnToSeat","Rotation dans l’habitacle…");
            p.leftFoot=points.ToLocal(points.driverLeftFoot); p.leftFootYaw=20;
            // Keep the pelvis over the planted right foot until it lifts onto the cabin floor.
            p.yaw=40; p.pelvis=new Vector3(-.66f,1.10f,1.53f); p.lean=20;
            p.leftHand=points.ToLocal(points.wheelLeftHand); p.leftGrip=1;
            yield return motion.Transition(p,1.1f,.10f);
            p.rightFoot=points.ToLocal(points.driverRightFoot); p.rightFootYaw=0; p.leftFootYaw=0; p.yaw=0;
            p.pelvis=new Vector3(-.61f,1.22f,1.39f); p.rightHand=points.ToLocal(points.wheelRightHand); p.rightHandWeight=1; p.rightGrip=1;
            yield return motion.Transition(p,.9f,0,.10f);
            Phase("Driver_Sit","Installation sur le siège…");
            p.pelvis=points.ToLocal(points.driverSeat); p.lean=3;
            yield return motion.Transition(p,1.25f);
            cameraDirector.SeatedView();
            Phase("Driver_HandsOnWheel","Fermeture de la porte…");
            var seated=p;
            p.pelvis+=new Vector3(-.12f,-.02f,.12f); p.yaw=-12; p.lean=20; p.leftGrip=0;
            p.leftHand=points.ToLocal(points.doorPull);
            yield return motion.Transition(p,.85f);
            var pull=p;
            var closedReach=seated; closedReach.lean=14; closedReach.pelvis+=new Vector3(0,0,.06f); closedReach.leftGrip=0;
            for(float t=0;t<1.25f;t+=Time.deltaTime)
            {
                float u=Mathf.SmoothStep(0,1,t/1.25f); door.SetOpening(76*(1-u));
                p=Pose.Blend(pull,closedReach,u);
                p.leftHand=points.ToLocal(points.doorPull); Rig.SetPose(p); yield return null;
            }
            door.SetOpening(0); p=seated;
            yield return motion.Transition(p,.7f);
            motion.Play("Driver_DrivingIdle"); CurrentAction="Au volant"; IsSeated=true; IsBoarding=false;
        }
        public IEnumerator TurnIgnition(float duration, System.Action crank)
        {
            var seated=Rig.CurrentPose;
            var p=seated;
            p.lean=14; p.pelvis+=new Vector3(0,-.015f,.035f); p.rightGrip=0;
            motion.Play("Driver_StartEngine"); p.rightHand=points.ToLocal(points.ignition);
            yield return motion.Transition(p,.55f);
            crank();
            yield return new WaitForSeconds(duration);
            p=seated;
            yield return motion.Transition(p,.4f);
            motion.Play("Driver_DrivingIdle");
        }
    }
}
