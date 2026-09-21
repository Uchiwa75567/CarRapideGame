using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Pose = CarRapide.Vehicle.CharacterContactRig.Pose;

namespace CarRapide.Vehicle
{
    /// <summary>One continuous rear-door boarding, seating and alighting performance.</summary>
    public sealed class PassengerController : MonoBehaviour
    {
        // Upper aisle surface measured from the original Carosserie triangles.
        const float CabinFloor = .336f;
        VehicleDriverExperience experience;
        VehicleInteractionPoints points;
        DriverAnimationController motion;
        Transform walkingFrame;
        Quaternion rearClosed;
        Vector3 rearAxis;
        public bool IsBusy { get; private set; }
        public bool IsSeated { get; private set; }
        public CharacterContactRig Rig => motion ? motion.Rig : null;

        public void Initialize(VehicleDriverExperience owner, VehicleInteractionPoints layout)
        {
            experience = owner;
            points = layout;
            rearClosed = points.rearDoorMesh.localRotation;
            rearAxis = points.rearDoorMesh.parent.InverseTransformDirection(layout.transform.up);
            points.passengerDoorGrip.SetParent(points.rearDoorMesh, true);
            points.rearWindowMesh.SetParent(points.rearDoorMesh, true);
            var character = owner.CreateCharacter("Passager", "Black_M_2_Casual", 1.70f);
            if (!character) return;
            walkingFrame = new GameObject("PassengerWaitingArea").transform;
            walkingFrame.SetPositionAndRotation(layout.transform.position, layout.transform.rotation);
            character.transform.SetParent(walkingFrame, false);
            motion = character.AddComponent<DriverAnimationController>();
            motion.Initialize(walkingFrame);
            var p = DriverController.Standing(points.ToLocal(points.passengerOutside), 0);
            p.pelvis.y -= .04f;
            DriverController.RelaxHands(ref p);
            Rig.SetPose(p);
            motion.Play("Client_Idle", 0);
        }

        public void RequestDemo()
        {
            if (!motion || IsBusy || experience.Driver.IsBoarding || experience.Engine.IsStarting || experience.Engine.IsRunning) return;
            IsBusy = true;
            experience.Engine.PassengerBusy = true;
            StartCoroutine(Demo());
        }

        Vector3 DoorGrip => walkingFrame.InverseTransformPoint(points.passengerDoorGrip.position);
        void Relax(ref Pose p, bool holdDoor = false)
        {
            DriverController.RelaxHands(ref p);
            if (holdDoor) p.rightHand = DoorGrip;
        }

        IEnumerator Demo()
        {
            experience.CameraDirector.PassengerView();
            motion.Play("Client_Walk");
            if (Mathf.Abs(Mathf.DeltaAngle(Rig.CurrentPose.yaw, 0)) > 1) yield return Turn(0);
            for (int i = 0; i < 6; i++) yield return WalkStep(i % 2 == 0, .34f);

            var p = Rig.CurrentPose;
            p.lean = 20;
            Relax(ref p, true);
            yield return motion.Transition(p, .75f);
            yield return RearDoor(0, -105);
            var ground = Rig.CurrentPose;
            var route = new List<Pose> { ground };

            motion.Play("Client_StepUp");
            p = ground;
            p.leftFoot = points.ToLocal(points.passengerStep);
            p.pelvis = new Vector3(0, .94f, -2.79f);
            Relax(ref p, true);
            yield return motion.Transition(p, 1.0f, .17f); route.Add(p);

            p.pelvis = new Vector3(0, 1.0f, -2.61f);
            p.rightFoot += new Vector3(0, .20f, .13f);
            p.lean = 25;
            Relax(ref p, true);
            yield return motion.Transition(p, .85f); route.Add(p);

            p.rightFoot = new Vector3(.12f, CabinFloor, -2.05f);
            p.pelvis = new Vector3(0, 1.09f, -2.31f);
            p.lean = 20;
            Relax(ref p);
            yield return motion.Transition(p, 1.05f, 0, .16f); route.Add(p);

            motion.Play("Client_Enter");
            p.leftFoot = new Vector3(-.13f, CabinFloor, -1.83f);
            p.pelvis = new Vector3(0, 1.13f, -2.07f);
            Relax(ref p);
            yield return motion.Transition(p, .9f, .12f); route.Add(p);

            p.rightFoot = new Vector3(.12f, CabinFloor, -1.39f);
            p.pelvis = new Vector3(0, 1.21f, -1.69f);
            p.lean = 5;
            Relax(ref p);
            yield return motion.Transition(p, .9f, 0, .10f); route.Add(p);

            p.leftFoot = new Vector3(.16f, CabinFloor, -1.52f);
            p.leftFootYaw = -60; p.yaw = -40;
            p.pelvis = new Vector3(.12f, 1.20f, -1.40f);
            Relax(ref p);
            yield return motion.Transition(p, .85f, .09f); route.Add(p);

            p.rightFoot = new Vector3(.18f, CabinFloor, -1.21f);
            p.leftFootYaw = p.rightFootYaw = p.yaw = -90;
            p.pelvis = new Vector3(.36f, 1.20f, -1.36f);
            Relax(ref p);
            yield return motion.Transition(p, .85f, 0, .09f); route.Add(p);

            motion.Play("Client_Sit");
            p.pelvis = points.ToLocal(points.passengerSeat);
            p.lean = 2;
            var facing = Quaternion.Euler(0, p.yaw, 0);
            p.leftHand = p.pelvis + facing * new Vector3(-.18f, .08f, .20f);
            p.rightHand = p.pelvis + facing * new Vector3(.18f, .08f, .20f);
            yield return motion.Transition(p, 1.2f);
            IsSeated = true;
            motion.Play("Client_Idle");
            yield return new WaitForSeconds(1.8f);

            IsSeated = false;
            motion.Play("Client_Walk");
            // Stand, turn in the aisle, then descend facing the vehicle with a hand on the door.
            for (int i = route.Count - 1; i >= 0; i--)
            {
                var target = route[i];
                var current = Rig.CurrentPose;
                bool left = (target.leftFoot - current.leftFoot).sqrMagnitude > .001f;
                bool right = (target.rightFoot - current.rightFoot).sqrMagnitude > .001f;
                yield return motion.Transition(target, .9f, left ? .12f : 0, right ? .12f : 0);
            }
            yield return RearDoor(-105, 0);
            p = Rig.CurrentPose; p.lean = 0; Relax(ref p);
            yield return motion.Transition(p, .6f);
            yield return Turn(180);
            for (int i = 0; i < 6; i++) yield return WalkStep(i % 2 == 0, -.34f);
            motion.Play("Client_Idle");
            IsBusy = false;
            experience.Engine.PassengerBusy = false;
            if (experience.Driver.IsSeated) experience.CameraDirector.SeatedView();
            else experience.CameraDirector.ExteriorView();
        }

        IEnumerator WalkStep(bool left, float distance)
        {
            var p = Rig.CurrentPose;
            p.pelvis.z += distance * .5f;
            if (left) p.leftFoot.z += distance; else p.rightFoot.z += distance;
            Relax(ref p);
            yield return motion.Transition(p, .60f, left ? .08f : 0, left ? 0 : .08f);
        }

        IEnumerator Turn(float yaw)
        {
            var p = Rig.CurrentPose;
            float halfway = Mathf.LerpAngle(p.yaw, yaw, .5f);
            var center = (p.leftFoot + p.rightFoot) * .5f;
            var facing = Quaternion.Euler(0, yaw, 0);
            p.yaw = halfway; p.leftFootYaw = halfway;
            p.leftFoot = center + facing * new Vector3(-.12f, 0, .035f);
            Relax(ref p);
            yield return motion.Transition(p, .7f, .05f);
            p.yaw = p.leftFootYaw = p.rightFootYaw = yaw;
            p.rightFoot = center + facing * new Vector3(.12f, 0, -.035f);
            Relax(ref p);
            yield return motion.Transition(p, .7f, 0, .05f);
        }

        IEnumerator RearDoor(float from, float to)
        {
            var p = Rig.CurrentPose;
            for (float t = 0; t < 1.25f; t += Time.deltaTime)
            {
                points.rearDoorMesh.localRotation = Quaternion.AngleAxis(Mathf.Lerp(from, to, Mathf.SmoothStep(0, 1, t / 1.25f)), rearAxis) * rearClosed;
                Relax(ref p, true); Rig.SetPose(p);
                yield return null;
            }
            points.rearDoorMesh.localRotation = Quaternion.AngleAxis(to, rearAxis) * rearClosed;
            Relax(ref p, true); Rig.SetPose(p);
        }

        void OnDestroy() { if (walkingFrame) Destroy(walkingFrame.gameObject); }
    }
}
