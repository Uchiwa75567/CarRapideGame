using UnityEngine;

namespace CarRapide.Vehicle
{
    /// <summary>Recorded truck rumble and clatter, blended by speed and propulsion load.</summary>
    public sealed class VehicleAudioController : MonoBehaviour
    {
        AudioSource starter, idle, load, rattle;
        VehicleController vehicle;
        Transform enginePosition;
        bool running;
        float rpm, demand, runningTime;
        public float StartDuration => starter.clip ? starter.clip.length : 0;
        public bool HasClips => starter.clip && idle.clip && load.clip && rattle.clip;
        public bool LoopPlaying => idle.isPlaying && load.isPlaying && rattle.isPlaying;

        public void Initialize(VehicleController controller)
        {
            vehicle = controller;
            enginePosition = new GameObject("EngineAudio").transform;
            enginePosition.SetParent(transform, false);
            enginePosition.localPosition = new Vector3(0, .65f, 1.7f);
            starter = Source("Engine_Start", false, .52f);
            idle = Source("Engine_Idle", true, 0);
            load = Source("Engine_Loop", true, 0);
            rattle = Source("Engine_Rattle", true, 0);
        }

        AudioSource Source(string resource, bool loop, float volume)
        {
            var emitter = new GameObject(resource);
            emitter.transform.SetParent(enginePosition, false);
            var source = emitter.AddComponent<AudioSource>();
            source.clip = Resources.Load<AudioClip>("CarRapide/Audio/" + resource);
            source.playOnAwake = false;
            source.loop = loop;
            source.volume = volume;
            source.spatialBlend = .65f;
            source.minDistance = 5;
            source.maxDistance = 40;
            source.dopplerLevel = 0;
            source.rolloffMode = AudioRolloffMode.Linear;
            return source;
        }

        public void Crank() { starter.Play(); }

        public void Run()
        {
            running = true;
            rpm = demand = runningTime = 0;
            idle.pitch = .82f;
            load.pitch = .82f;
            rattle.pitch = .86f;
            idle.Play();
            load.Play();
            rattle.Play();
        }

        void Update()
        {
            if (!running) return;
            runningTime += Time.deltaTime;
            float response = 1 - Mathf.Exp(-Time.deltaTime * 4);
            float speed = Mathf.Clamp01(vehicle.SpeedKmh / 65);
            demand = Mathf.Lerp(demand, vehicle.EngineLoad, response);
            rpm = Mathf.Lerp(rpm, Mathf.Clamp01(speed * .72f + demand * .28f), response);
            // Small, slow fluctuations suggest uneven firing without an electronic vibrato.
            float uneven = Mathf.PerlinNoise(runningTime * 2.1f, 7.3f) * 2 - 1;
            float pitchVariation = uneven * .014f * (1 - rpm * .6f);
            idle.pitch = Mathf.Lerp(.82f, 1.04f, rpm) + pitchVariation;
            load.pitch = Mathf.Lerp(.82f, 1.32f, rpm) + pitchVariation;
            rattle.pitch = Mathf.Lerp(.86f, 1.20f, rpm) + pitchVariation * .5f;
            float grit = .94f + Mathf.PerlinNoise(13.1f, runningTime * 3.7f) * .12f;
            idle.volume = Mathf.Lerp(idle.volume, Mathf.Lerp(.46f, .24f, rpm) * grit, response);
            load.volume = Mathf.Lerp(load.volume, (.10f + .23f * rpm + .15f * demand) * grit, response);
            rattle.volume = Mathf.Lerp(rattle.volume, (.065f + .065f * rpm + .055f * demand) * grit, response);
        }

        void OnDisable()
        {
            if (starter) starter.Stop();
            if (idle) idle.Stop();
            if (load) load.Stop();
            if (rattle) rattle.Stop();
            running = false;
        }
    }
}
