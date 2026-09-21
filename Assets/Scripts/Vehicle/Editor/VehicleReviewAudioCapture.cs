#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using Unity.Collections;
using UnityEditor.Media;
using UnityEngine;

namespace CarRapide.EditorTools
{
    /// <summary>Capture Unity's actual listener mix in step with fixed-rate video frames.</summary>
    public sealed class VehicleReviewAudioCapture : IDisposable
    {
        BinaryWriter output;
        readonly int channels, sampleRate;
        bool recording;
        public float RunningPeak { get; private set; }
        public int ClippedSamples { get; private set; }
        public AudioTrackAttributes Track => new AudioTrackAttributes
        {channelCount=(ushort)channels, sampleRate=new MediaRational(sampleRate), language="fr"};

        public VehicleReviewAudioCapture(string path)
        {
            sampleRate = AudioSettings.outputSampleRate;
            channels = AudioSettings.speakerMode switch
            {
                AudioSpeakerMode.Mono => 1,
                AudioSpeakerMode.Quad => 4,
                AudioSpeakerMode.Surround => 5,
                AudioSpeakerMode.Mode5point1 => 6,
                AudioSpeakerMode.Mode7point1 => 8,
                _ => 2
            };
            output = new BinaryWriter(File.Create(path));
            WriteHeader(0);
            recording = AudioRenderer.Start();
            if (!recording)
            {
                output.Dispose(); output = null;
                throw new InvalidOperationException("Unity audio is already being captured.");
            }
        }

        public void CaptureFrame(MediaEncoder encoder, bool engineRunning)
        {
            int count = AudioRenderer.GetSampleCountForCaptureFrame() * channels;
            if (count <= 0) return;
            using var samples = new NativeArray<float>(count, Allocator.Temp);
            if (!AudioRenderer.Render(samples) || !encoder.AddSamples(samples))
                throw new InvalidOperationException("Failed to encode the Unity listener audio.");
            foreach (float sample in samples)
            {
                float amplitude = Mathf.Abs(sample);
                if (engineRunning) RunningPeak = Mathf.Max(RunningPeak, amplitude);
                if (amplitude >= 1) ClippedSamples++;
                output.Write((short)Mathf.RoundToInt(Mathf.Clamp(sample, -1, 1) * 32767));
            }
        }

        void WriteHeader(int bytes)
        {
            output.Write(Encoding.ASCII.GetBytes("RIFF")); output.Write(36 + bytes);
            output.Write(Encoding.ASCII.GetBytes("WAVEfmt ")); output.Write(16);
            output.Write((short)1); output.Write((short)channels); output.Write(sampleRate);
            output.Write(sampleRate * channels * 2); output.Write((short)(channels * 2)); output.Write((short)16);
            output.Write(Encoding.ASCII.GetBytes("data")); output.Write(bytes);
        }

        public void Dispose()
        {
            if (recording) { AudioRenderer.Stop(); recording = false; }
            if (output == null) return;
            int bytes = (int)output.BaseStream.Length - 44;
            output.BaseStream.Position = 0; WriteHeader(bytes);
            output.Dispose(); output = null;
        }
    }
}
#endif
