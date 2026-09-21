#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Media;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using CarRapide.Vehicle;

namespace CarRapide.EditorTools
{
    public static class VehicleExperienceReview
    {
        static float start,nextCapture;
        static int frame;
        static bool active,boarded,started;
        static bool passengerReview;
        static bool braked, passengerSat, observedIgnition, ignitionStayedLocked;
        static float peakSpeed, greatestContactError;
        static Quaternion initialRotation;
        static readonly float[] audioSamples=new float[512];
        static float peakAudio;
        static MediaEncoder video;
        static int videoFrame, previousCaptureRate;
        static Keyboard keyboard;
        static Vector3 origin;
        static KeyboardState requestedInput;
        static string Folder => passengerReview ? "Library/VehicleReview/Passenger" : "Library/VehicleReview/Run";
        [MenuItem("Car Rapide/Vehicle/Record 30 second Play Mode review")]
        public static void Begin()
        {
            if(active) throw new System.InvalidOperationException("A review is already running.");
            passengerReview=false;
            if(!EditorApplication.isPlaying) throw new System.InvalidOperationException("Enter Play Mode first.");
            Directory.CreateDirectory(Folder);
            var e=Object.FindAnyObjectByType<VehicleDriverExperience>();
            if(!e || !e.Ready || e.Driver.IsBoarding || e.Driver.IsSeated) throw new System.InvalidOperationException("Review requires a fresh Play Mode session.");
            File.WriteAllText(Folder+"/checks.txt","Unity "+Application.unityVersion+"\n");
            keyboard=InputSystem.AddDevice<Keyboard>();
            VehicleMotionBaker.Begin(e.Driver.GetComponent<DriverAnimationController>());
            InputSystem.onBeforeUpdate+=InjectInput;
            origin=e.transform.position;
            initialRotation=e.transform.rotation;
            peakSpeed=greatestContactError=peakAudio=0; braked=observedIgnition=false; ignitionStayedLocked=true;
            start=Time.time; nextCapture=0; frame=0; active=true; boarded=started=false;
            EditorApplication.update-=Tick; EditorApplication.update+=Tick;
        }
        [MenuItem("Car Rapide/Vehicle/Record driver review with silent MP4")]
        public static void BeginVideo()
        {
            Begin();
            var codec=new H264EncoderAttributes
            {gopSize=30,numConsecutiveBFrames=2,profile=VideoEncodingProfile.H264High};
            var track=new VideoTrackEncoderAttributes(codec)
            {width=1280,height=720,frameRate=new MediaRational(30),includeAlpha=false,targetBitRate=12000000};
            video=new MediaEncoder(Folder+"/driver-review.mp4",track,new AudioTrackAttributes[0]);
            previousCaptureRate=Time.captureFramerate;
            Time.captureFramerate=30;
            videoFrame=-1;
        }
        [MenuItem("Car Rapide/Vehicle/Record passenger Play Mode review")]
        public static void BeginPassenger()
        {
            if(active) throw new System.InvalidOperationException("A review is already running.");
            if(!EditorApplication.isPlaying) throw new System.InvalidOperationException("Enter Play Mode first.");
            var e=Object.FindAnyObjectByType<VehicleDriverExperience>();
            if(!e || !e.Ready || e.Engine.IsRunning || e.Driver.IsBoarding || e.Passenger.IsBusy)
                throw new System.InvalidOperationException("Passenger review requires a parked vehicle.");
            passengerReview=true; Directory.CreateDirectory(Folder);
            passengerSat=false; greatestContactError=0;
            File.WriteAllText(Folder+"/checks.txt","Passenger review\n");
            start=Time.time; nextCapture=0; frame=0; active=true;
            e.Passenger.RequestDemo();
            EditorApplication.update-=Tick; EditorApplication.update+=Tick;
        }
        static void Tick()
        {
            if(!active) return;
            if(!EditorApplication.isPlaying) {End();return;}
            var e=Object.FindAnyObjectByType<VehicleDriverExperience>();
            if(!e || !e.Ready) return;
            float t=Time.time-start;
            if(video!=null && Time.frameCount!=videoFrame)
            {
                videoFrame=Time.frameCount;
                Capture(null);
            }
            if(passengerReview)
            {
                passengerSat |= e.Passenger.IsSeated;
                greatestContactError=Mathf.Max(greatestContactError,e.Passenger.Rig.MaxContactError);
                if(t>=nextCapture)
                {
                    nextCapture=t+.5f;
                    Capture(Folder+"/frame-"+(frame++).ToString("D3")+".png");
                    File.AppendAllText(Folder+"/checks.txt",$"t={t:F2} seated={e.Passenger.IsSeated} errors={e.Passenger.Rig.ContactErrors.ToString("F3")}\n");
                }
                if(t>1 && !e.Passenger.IsBusy)
                {
                    Check(passengerSat,"Passenger reaches the seat before alighting");
                    Check(!e.Engine.PassengerBusy,"Passenger sequence releases ignition interlock");
                    Check(greatestContactError<.05f,"Passenger contacts remain within 5 cm (max="+greatestContactError.ToString("F3")+")");
                    End();
                }
                return;
            }
            if(t<1.5f) {requestedInput=new KeyboardState(Key.W,Key.A); e.Engine.RequestStart();}
            else if(!boarded)
            {
                Check(!e.GetComponent<VehicleController>().CanDrive && !e.Engine.IsStarting,"Ignition and movement locked before seating");
                var d=e.transform.position-origin;d.y=0; Check(d.magnitude<.015f,"No horizontal movement under held W+A before ignition");
                requestedInput=new KeyboardState(); e.Driver.RequestBoard(); boarded=true;
            }
            if(e.Driver.IsSeated && !started)
            {Check(e.Door.IsClosed,"Door closes before ignition"); e.Engine.RequestStart(); started=true;}
            if(e.Engine.IsStarting)
            {
                observedIgnition=true;
                requestedInput=new KeyboardState(Key.W,Key.A);
                var displacement=e.transform.position-origin; displacement.y=0;
                ignitionStayedLocked &= !e.GetComponent<VehicleController>().CanDrive && displacement.magnitude<.015f;
            }
            if(e.Engine.IsRunning)
            {
                requestedInput=t<25?new KeyboardState(Key.W,Key.D):t<28?new KeyboardState(Key.Space):new KeyboardState(Key.S);
                peakSpeed=Mathf.Max(peakSpeed,e.GetComponent<VehicleController>().SpeedKmh);
                AudioListener.GetOutputData(audioSamples,0);
                foreach(float sample in audioSamples) peakAudio=Mathf.Max(peakAudio,Mathf.Abs(sample));
                if(t>27 && t<28) braked |= e.GetComponent<VehicleController>().SpeedKmh<.5f;
            }
            greatestContactError=Mathf.Max(greatestContactError,e.Driver.Rig.MaxContactError);
            if(t>=nextCapture)
            {
                nextCapture=t+.5f;
                Capture(Folder+"/frame-"+(frame++).ToString("D3")+".png");
                var rig=e.Driver.Rig;
                var input=typeof(VehicleController).GetField("moveInput",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(e.GetComponent<VehicleController>());
                File.AppendAllText(Folder+"/checks.txt","input="+input+" errors="+rig.ContactErrors.ToString("F3")+"\n");
                File.AppendAllText(Folder+"/checks.txt",System.FormattableString.Invariant($"t={t:F2} action={e.Driver.CurrentAction} seated={e.Driver.IsSeated} running={e.Engine.IsRunning} speed={e.GetComponent<VehicleController>().SpeedKmh:F2} contacts={rig.MaxContactError:F3} keyW={keyboard.wKey.isPressed} current={Keyboard.current.deviceId}\n"));
            }
            if(t>=30)
            {
                Check(observedIgnition && ignitionStayedLocked,"Held controls remain locked throughout engine startup");
                Check(e.Engine.IsRunning && e.Engine.Audio.LoopPlaying,"Engine audio sources remain active during driving");
                Check(Vector3.Distance(origin,e.transform.position)>2,"Vehicle travels after ignition"); End();
                Check(peakSpeed>30,"Accelerates beyond 30 km/h");
                Check(Quaternion.Angle(initialRotation,e.transform.rotation)>10,"Steering changes heading");
                Check(braked,"Handbrake brings vehicle to rest");
                Check(Vector3.Dot(e.GetComponent<Rigidbody>().linearVelocity,e.transform.forward)<-1,"S engages reverse after braking");
                Check(peakAudio>.0001f,"Engine reaches the audio listener during driving (peak="+peakAudio.ToString("F4")+")");
                Check(greatestContactError<.05f,"Driver contacts remain within 5 cm (max="+greatestContactError.ToString("F3")+")");
            }
        }
        static void InjectInput()
        {
            if(!active || InputState.currentUpdateType!=InputUpdateType.Dynamic) return;
            keyboard.MakeCurrent(); InputState.Change(keyboard,requestedInput);
        }
        static void Check(bool pass,string message) => File.AppendAllText(Folder+"/checks.txt",(pass?"PASS ":"FAIL ")+message+"\n");
        static void End()
        {
            active=false; EditorApplication.update-=Tick;
            if(video!=null) {video.Dispose();video=null;Time.captureFramerate=previousCaptureRate;}
            VehicleMotionBaker.End();
            InputSystem.onBeforeUpdate-=InjectInput;
            if(keyboard!=null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            keyboard=null;
        }
        static void Capture(string path)
        {
            var camera=Camera.main; var old=camera.targetTexture; var previous=RenderTexture.active;
            var rt=RenderTexture.GetTemporary(1280,720,24); var tex=new Texture2D(1280,720,TextureFormat.RGBA32,false);
            try
            {
                camera.targetTexture=rt; camera.Render(); RenderTexture.active=rt;
                tex.ReadPixels(new Rect(0,0,1280,720),0,0); tex.Apply();
                if(path!=null) File.WriteAllBytes(path,tex.EncodeToPNG());
                else video.AddFrame(tex);
            }
            finally {camera.targetTexture=old;RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);Object.DestroyImmediate(tex);}
        }
    }
}
#endif
