#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using CarRapide.Vehicle;

namespace CarRapide.EditorTools
{
    public static class VehicleExperienceInstaller
    {
        const string ResourcesRoot="Assets/Resources/CarRapide";
        [MenuItem("Car Rapide/Vehicle/Install calibrated driver experience")]
        public static void Install()
        {
            if(EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop Play Mode before installation.");
            PrepareMaterials(); PrepareAnimations();
            var vehicle=Object.FindAnyObjectByType<VehicleController>();
            Undo.RegisterFullObjectHierarchyUndo(vehicle.gameObject,"Install driver experience");
            var model=vehicle.transform.Find("Car rapide");
            model.localScale=Vector3.one*2.2f;
            PrepareGlass(model);
            vehicle.transform.position=new Vector3(0,.03634f,0);
            var box=vehicle.GetComponent<BoxCollider>(); box.center=new Vector3(0,1.23f,0); box.size=new Vector3(2.14f,2.53268f,5.36f);
            const string physicsPath="Assets/Resources/CarRapide/VehicleSurface.physicMaterial";
            var surface=AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(physicsPath);
            if(!surface) {surface=new PhysicsMaterial("VehicleSurface");AssetDatabase.CreateAsset(surface,physicsPath);}
            // The arcade controller supplies rolling resistance and lateral grip itself.
            surface.staticFriction=surface.dynamicFriction=0;surface.frictionCombine=PhysicsMaterialCombine.Minimum;
            surface.bounciness=0;surface.bounceCombine=PhysicsMaterialCombine.Minimum;box.sharedMaterial=surface;
            var points=vehicle.GetComponent<VehicleInteractionPoints>() ?? vehicle.gameObject.AddComponent<VehicleInteractionPoints>();
            var container=vehicle.transform.Find("InteractionPoints");
            if(!container) {container=new GameObject("InteractionPoints").transform; container.SetParent(vehicle.transform,false);}
            Transform Anchor(string name,Vector3 position)
            {
                var t=container.Find(name); if(!t) {t=new GameObject(name).transform; t.SetParent(container,false);}
                t.localPosition=position; t.localRotation=Quaternion.identity; return t;
            }
            points.driverOutside=Anchor("DriverOutsidePoint",new Vector3(-1.88f,-.03634f,1.14f));
            points.doorHandle=Anchor("DoorHandlePoint",new Vector3(-1.068f,1.16f,1.30f));
            points.doorPull=Anchor("DoorInnerPullPoint",new Vector3(-1.022f,1.48f,1.91f));
            points.driverDoor=Anchor("DriverDoorPoint",new Vector3(-1.12f,.358f,1.75f));
            points.driverStep=Anchor("DriverStepPoint",new Vector3(-1.015f,.336f,1.95f));
            points.driverCabin=Anchor("DriverCabinPoint",new Vector3(-.69f,.358f,1.73f));
            points.driverSeat=Anchor("DriverSeatPoint",new Vector3(-.61f,1.14f,1.39f));
            points.wheelLeftHand=Anchor("SteeringWheelLeftHandPoint",new Vector3(-.85f,1.34f,1.72f));
            points.wheelRightHand=Anchor("SteeringWheelRightHandPoint",new Vector3(-.38f,1.34f,1.72f));
            points.driverLeftFoot=Anchor("DriverLeftFootPoint",new Vector3(-.77f,.43f,1.995f));
            points.driverRightFoot=Anchor("DriverRightFootPoint",new Vector3(-.49f,.44f,2.015f));
            points.ignition=Anchor("IgnitionPoint",new Vector3(-.40f,1.22f,1.80f));
            points.receiverLeftFoot=Anchor("ReceiverFootPoint",new Vector3(-.76f,.318f,-2.51f));
            points.receiverRightFoot=Anchor("ReceiverRightFootPoint",new Vector3(-.50f,.318f,-2.53f));
            points.receiverHandGrip=Anchor("ReceiverHandGripPoint",new Vector3(-.63f,1.62f,-2.20f));
            points.passengerOutside=Anchor("PassengerOutsidePoint",new Vector3(0,-.03634f,-3.89f));
            points.passengerStep=Anchor("PassengerStepPoint",new Vector3(-.13f,.318f,-2.51f));
            points.passengerCabin=Anchor("PassengerCabinPoint",new Vector3(0,.336f,-1.65f));
            points.passengerSeat=Anchor("PassengerSeatPoint",new Vector3(.64f,.87f,-1.36f));
            points.passengerDoorGrip=Anchor("PassengerDoorGripPoint",new Vector3(.28f,1.19f,-2.31f));
            Transform Find(string name) => model.GetComponentsInChildren<Transform>(true).First(t=>t.name==name);
            points.driverDoorMesh=Find("Porte_avant_gauche"); points.driverMirror=Find("Retroviseur gauche"); points.rearDoorMesh=Find("Porte_arriere");
            points.rearWindowMesh=Find("Fenetre_arriere");
            if(!vehicle.GetComponent<VehicleDriverExperience>()) vehicle.gameObject.AddComponent<VehicleDriverExperience>();
            var camera=Camera.main; camera.fieldOfView=48; camera.nearClipPlane=.05f;
            camera.transform.position=new Vector3(-4.7f,2.48634f,4.9f); camera.transform.LookAt(new Vector3(-.45f,1.08634f,.95f));
            EditorUtility.SetDirty(points); EditorUtility.SetDirty(vehicle);
            EditorSceneManager.MarkSceneDirty(vehicle.gameObject.scene);
            EditorSceneManager.SaveScene(vehicle.gameObject.scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Calibrated driver experience installed. E: board, R: ignition, P: passenger demo.");
        }

        static void PrepareGlass(Transform model)
        {
            const string path=ResourcesRoot+"/CabinGlass.mat";
            var glass=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!glass) {glass=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(glass,path);}
            glass.SetColor("_BaseColor",new Color(.66f,.81f,.87f,.16f));
            glass.SetFloat("_Surface",1); glass.SetFloat("_Blend",0);
            glass.SetFloat("_Smoothness",.82f); glass.SetFloat("_Metallic",0);
            glass.SetFloat("_Cull",2);
            BaseShaderGUI.SetMaterialKeywords(glass);
            glass.SetShaderPassEnabled("ShadowCaster",false);
            // The original FBX already separates Parebrise submeshes. Preserve every
            // vertex, frame and door; only correct their imported opaque material.
            foreach(var renderer in model.GetComponentsInChildren<MeshRenderer>())
            {
                var materials=renderer.sharedMaterials;
                for(int i=0;i<materials.Length;i++)
                    if(materials[i].name=="Parebrise" || materials[i]==glass) materials[i]=glass;
                renderer.sharedMaterials=materials;
            }
            EditorUtility.SetDirty(glass);
        }

        public static void PrepareMaterials()
        {
            for(int i=1;i<=2;i++)
            {
                string name=$"Black_M_{i}_Casual", path=$"{ResourcesRoot}/Characters/{name}.fbx";
                var importer=(ModelImporter)AssetImporter.GetAtPath(path);
                importer.animationType=ModelImporterAnimationType.Human; importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;
                importer.materialLocation=ModelImporterMaterialLocation.InPrefab;
                importer.optimizeGameObjects=false;
                string materialPath=$"{ResourcesRoot}/Characters/Materials/{name}.mat";
                var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if(!material) {material=new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material,materialPath);}
                material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>($"{ResourcesRoot}/Characters/{name}.fbm/B_M_Casual_{i}_color.jpg"));
                string normalPath=$"{ResourcesRoot}/Characters/{name}.fbm/B_M_Casual_{i}_high_nm.jpg";
                var textureImporter=(TextureImporter)AssetImporter.GetAtPath(normalPath);
                textureImporter.textureType=TextureImporterType.NormalMap; textureImporter.SaveAndReimport();
                material.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath)); material.EnableKeyword("_NORMALMAP");
                material.SetFloat("_Smoothness",.2f); material.SetFloat("_BumpScale",.6f);
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),"_Body"),material);
                importer.SaveAndReimport(); EditorUtility.SetDirty(material);
            }
        }
        static void PrepareAnimations()
        {
            string folder=ResourcesRoot+"/Animation"; Directory.CreateDirectory(folder); AssetDatabase.Refresh();
            string path=folder+"/Driver.controller";
            var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if(!controller) controller=AnimatorController.CreateAnimatorControllerAtPath(path);
            string[] names={"Driver_IdleOutside","Driver_WalkToDoor","Driver_GrabHandle","Driver_OpenDoor","Driver_StepUp","Driver_EnterCabin","Driver_TurnToSeat","Driver_Sit","Driver_HandsOnWheel","Driver_DrivingIdle","Driver_StartEngine","Receiver_Ride","Client_Idle","Client_Walk","Client_StepUp","Client_Enter","Client_Sit"};
            foreach(string name in names)
            {
                var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(folder+"/"+name+".anim");
                bool baked=clip && AnimationUtility.GetCurveBindings(clip).Length>=HumanTrait.MuscleCount;
                if(!clip) {clip=new AnimationClip {name=name,frameRate=30}; AssetDatabase.CreateAsset(clip,folder+"/"+name+".anim");}
                if(!baked)
                {
                    // Humanoid muscle curves provide subtle secondary motion; the final rig holds contacts.
                    clip.SetCurve("",typeof(Animator),"Chest Front-Back",new AnimationCurve(new Keyframe(0,0),new Keyframe(1.5f,.018f),new Keyframe(3,0)));
                    clip.SetCurve("",typeof(Animator),"Head Nod Down-Up",new AnimationCurve(new Keyframe(0,0),new Keyframe(1.5f,-.015f),new Keyframe(3,0)));
                    foreach(string side in new[]{"Left","Right"})
                    {
                        clip.SetCurve("",typeof(Animator),side+" Arm Down-Up",AnimationCurve.Constant(0,3,-.82f));
                        foreach(string finger in new[]{"Index","Middle","Ring","Little"})
                            for(int joint=1;joint<=3;joint++) clip.SetCurve("",typeof(Animator),side+"Hand."+finger+"."+joint+" Stretched",AnimationCurve.Constant(0,3,.35f));
                    }
                    var settings=AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime=true; AnimationUtility.SetAnimationClipSettings(clip,settings);
                }
                var machine=controller.layers[0].stateMachine;
                var state=machine.states.Select(x=>x.state).FirstOrDefault(x=>x.name==name) ?? machine.AddState(name);
                state.motion=clip; state.writeDefaultValues=true; EditorUtility.SetDirty(clip);
            }
            controller.layers[0].stateMachine.defaultState=controller.layers[0].stateMachine.states.First(x=>x.state.name=="Driver_IdleOutside").state;
            EditorUtility.SetDirty(controller);
        }
    }
}
#endif
