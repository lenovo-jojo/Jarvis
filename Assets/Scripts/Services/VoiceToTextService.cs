using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using AICompanion.Core;

namespace AICompanion.Services
{
    public class VoiceToTextService : MonoBehaviour, ISpeechToTextService
    {
        private AudioClip micClip;
        private string micDevice;
        private bool isRecording = false;
        private bool isListeningEnabled = true;
        private float lastVoiceTime;
        private const int Samplerate = 16000;
        private const int MaxDurationSeconds = 12;

        [Header("Voice Trigger Settings")]
        [SerializeField] private float voiceThreshold = 0.015f; // Noise gate threshold
        [SerializeField] private float silenceTimeout = 1.6f; // Autostop duration of silence

        private float[] micSamples = new float[512];

        public bool IsRecording => isRecording;

        public void SetListeningEnabled(bool enabled)
        {
            isListeningEnabled = enabled;
            if (!enabled)
            {
                if (isRecording)
                {
                    isRecording = false;
                }
                if (!string.IsNullOrEmpty(micDevice) && Microphone.IsRecording(micDevice))
                {
                    Microphone.End(micDevice);
                }
            }
            Debug.Log($"[VoiceToTextService] Listening enabled: {enabled}");
        }

        private void Start()
        {
            // Detect default microphone
            if (Microphone.devices.Length > 0)
            {
                micDevice = Microphone.devices[0];
                Debug.Log($"[VoiceToTextService] Default microphone detected: {micDevice}");
            }
            else
            {
                Debug.LogWarning("[VoiceToTextService] No microphone devices found on this system.");
            }
        }

        private void Update()
        {
            if (!isListeningEnabled) return;
            if (string.IsNullOrEmpty(micDevice)) return;

            // Monitor mic levels and auto-trigger recording when user speaks
            if (!isRecording)
            {
                // Temporarily monitor mic levels in a loop
                if (!Microphone.IsRecording(micDevice))
                {
                    micClip = Microphone.Start(micDevice, true, 1, Samplerate);
                }

                int micPosition = Microphone.GetPosition(micDevice) - 512;
                if (micPosition > 0)
                {
                    micClip.GetData(micSamples, micPosition);
                    float sum = 0f;
                    for (int i = 0; i < micSamples.Length; i++)
                    {
                        sum += micSamples[i] * micSamples[i];
                    }
                    float rms = Mathf.Sqrt(sum / micSamples.Length);

                    // Voice detected! Auto-start recording
                    if (rms > voiceThreshold)
                    {
                        Microphone.End(micDevice);
                        StartRecording();
                    }
                }
            }
            else
            {
                // Active recording: check for silence timeout to automatically stop
                int micPosition = Microphone.GetPosition(micDevice) - 512;
                if (micPosition > 0)
                {
                    micClip.GetData(micSamples, micPosition);
                    float sum = 0f;
                    for (int i = 0; i < micSamples.Length; i++)
                    {
                        sum += micSamples[i] * micSamples[i];
                    }
                    float rms = Mathf.Sqrt(sum / micSamples.Length);

                    if (rms > voiceThreshold)
                    {
                        lastVoiceTime = Time.time;
                    }

                    // Autostop if silent for too long, or reached max duration
                    if (Time.time - lastVoiceTime > silenceTimeout || Microphone.GetPosition(micDevice) >= Samplerate * MaxDurationSeconds)
                    {
                        StopRecordingAndTranscribe();
                    }
                }
            }
        }

        public void StartRecording()
        {
            if (isRecording || string.IsNullOrEmpty(micDevice)) return;

            isRecording = true;
            lastVoiceTime = Time.time;
            Debug.Log("[VoiceToTextService] Voice trigger activated! Recording started...");
            micClip = Microphone.Start(micDevice, false, MaxDurationSeconds, Samplerate);
        }

        private void StopRecordingAndTranscribe()
        {
            if (!isRecording) return;

            isRecording = false;
            int lastPos = Microphone.GetPosition(micDevice);
            Microphone.End(micDevice);

            if (lastPos <= 0)
            {
                Debug.LogWarning("[VoiceToTextService] Recorded clip was empty.");
                return;
            }

            Debug.Log("[VoiceToTextService] Recording complete, sending to OpenAI Whisper...");

            // Create a trimmed audioclip containing only recorded data
            float[] trimmedSamples = new float[lastPos];
            micClip.GetData(trimmedSamples, 0);

            byte[] wavData = ConvertToWav(trimmedSamples, Samplerate);

            StartCoroutine(SendWhisperRequestCoroutine(wavData));
        }

        private IEnumerator SendWhisperRequestCoroutine(byte[] wavData)
        {
            ServiceConfig config = ServiceConfig.Load();
            string apiKey = config.elevenLabsApiKey; // We use ElevenLabsApiKey/Shared OpenAI Key in this project context

            if (string.IsNullOrEmpty(apiKey))
            {
                // Fallback: look for LLM API Key
                apiKey = config.llmApiKey;
            }

            if (string.IsNullOrEmpty(apiKey))
            {
                Debug.LogError("[VoiceToTextService] API Key is missing. Cannot transcribe audio.");
                yield break;
            }

            string endpoint = "https://api.openai.com/v1/audio/transcriptions";

            // Construct Multipart Form Request for Whisper API
            WWWForm form = new WWWForm();
            form.AddBinaryData("file", wavData, "audio.wav", "audio/wav");
            form.AddField("model", "whisper-1");

            using (UnityWebRequest webRequest = UnityWebRequest.Post(endpoint, form))
            {
                webRequest.SetRequestHeader("Authorization", $"Bearer {apiKey}");

                yield return webRequest.SendWebRequest();

                if (webRequest.result == UnityWebRequest.Result.Success)
                {
                    string jsonResponse = webRequest.downloadHandler.text;
                    string text = ParseWhisperText(jsonResponse);
                    if (!string.IsNullOrEmpty(text))
                    {
                        Debug.Log($"[VoiceToTextService] Transcribed speech: '{text}'");
                        // Trigger message processing via CompanionController
                        var controller = FindAnyObjectByType<CompanionController>();
                        if (controller != null)
                        {
                            controller.ProcessVoiceInput(text);
                        }
                    }
                }
                else
                {
                    Debug.LogError($"[VoiceToTextService] Whisper transcription error: {webRequest.error}\nDetails: {webRequest.downloadHandler?.text}");
                }
            }
        }

        private string ParseWhisperText(string json)
        {
            try
            {
                int textIdx = json.IndexOf("\"text\"");
                if (textIdx == -1) return null;
                int startQuote = json.IndexOf("\"", textIdx + 6);
                if (startQuote == -1) return null;
                startQuote++;
                int endQuote = json.IndexOf("\"", startQuote);
                if (endQuote == -1) return null;
                return json.Substring(startQuote, endQuote - startQuote);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[VoiceToTextService] JSON parse exception: {ex.Message}");
                return null;
            }
        }

        private byte[] ConvertToWav(float[] samples, int sampleRate)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    writer.Write(new char[4] { 'R', 'I', 'F', 'F' });
                    writer.Write(36 + samples.Length * 2);
                    writer.Write(new char[4] { 'W', 'A', 'V', 'E' });
                    writer.Write(new char[4] { 'f', 'm', 't', ' ' });
                    writer.Write(16);
                    writer.Write((short)1); // Audio format: PCM
                    writer.Write((short)1); // Channels: Mono
                    writer.Write(sampleRate);
                    writer.Write(sampleRate * 2);
                    writer.Write((short)2); // Block align
                    writer.Write((short)16); // Bits per sample
                    writer.Write(new char[4] { 'd', 'a', 't', 'a' });
                    writer.Write(samples.Length * 2);

                    foreach (var s in samples)
                    {
                        writer.Write((short)(s * short.MaxValue));
                    }
                }
                return stream.ToArray();
            }
        }

        public void StartRecording(Action<string> onCompleted, Action<string> onError)
        {
            StartRecording();
        }

        public void StopRecording(Action<string> onCompleted, Action<string> onError)
        {
            StopRecordingAndTranscribe();
        }
    }
}