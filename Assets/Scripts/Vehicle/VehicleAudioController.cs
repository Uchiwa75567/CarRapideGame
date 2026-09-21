using UnityEngine;

namespace CarRapide.Vehicle
{
    public sealed class VehicleAudioController : MonoBehaviour
    {
        AudioSource starter,idle,load;
        VehicleController vehicle;
        bool running;
        public float StartDuration => starter.clip ? starter.clip.length : 0;
        public bool HasClips => starter.clip && idle.clip && load.clip;
        public bool LoopPlaying => idle.isPlaying && load.isPlaying;
        public void Initialize(VehicleController controller)
        {
            vehicle=controller; starter=Source("Engine_Start",false,.52f); idle=Source("Engine_Idle",true,0); load=Source("Engine_Loop",true,0);
        }
        AudioSource Source(string resource,bool loop,float volume)
        {
            var source=gameObject.AddComponent<AudioSource>(); source.clip=Resources.Load<AudioClip>("CarRapide/Audio/"+resource);
            source.playOnAwake=false; source.loop=loop; source.volume=volume; source.spatialBlend=.65f;
            source.minDistance=5; source.maxDistance=32; source.dopplerLevel=0; source.rolloffMode=AudioRolloffMode.Linear;
            return source;
        }
        public void Crank() { starter.Play(); }
        public void Run() { running=true; idle.Play(); load.Play(); }
        void Update()
        {
            if (!running) return;
            float rpm=Mathf.Clamp01(vehicle.SpeedKmh/65), response=1-Mathf.Exp(-Time.deltaTime*4);
            idle.volume=Mathf.Lerp(idle.volume,Mathf.Lerp(.36f,.18f,rpm),response);
            load.volume=Mathf.Lerp(load.volume,Mathf.Lerp(.08f,.4f,rpm),response);
            idle.pitch=Mathf.Lerp(idle.pitch,Mathf.Lerp(.85f,1.06f,rpm),response);
            load.pitch=Mathf.Lerp(load.pitch,Mathf.Lerp(.82f,1.35f,rpm),response);
        }
        void OnDisable() { if(starter)starter.Stop(); if(idle)idle.Stop(); if(load)load.Stop(); running=false; }
    }
}
