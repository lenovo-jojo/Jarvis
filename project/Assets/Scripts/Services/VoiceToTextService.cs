using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using AICompanion.Core;
using Debug = UnityEngine.Debug;

namespace AICompanion.Services
{
    public class VoiceToTextService : MonoBehaviour, ISpeechToTextService
    {
        private AudioClip micClip;
        private string micDevice;
        private bool isRecording = false;
        private bool isTranscribing = false;
        private bool isListeningEnabled = true;
        private float lastVoiceTime;
        private float resumeListeningAt;
        private string lastDispatchedTranscript = string.Empty;
        private float lastTranscriptAt = -100f;
        private const int Samplerate = 16000;
        private const int MaxDurationSeconds = 12;
        private const int CaptureBufferSeconds = MaxDurationSeconds + 2;
        private const float PreRollSeconds = 0.25f;
        private float captureStartedAt;
        private float recordingStartedAt;
        private int recordingStartSample;
        private const float MinimumSpeechSeconds = 0.12f;
        private const float ResumeCooldownSeconds = 1.25f;
        private const float RepeatedShortTranscriptWindowSeconds = 8f;

        [Header("Voice Trigger Settings")]
        [SerializeField] private float voiceThreshold = 0.015f; // Noise gate threshold
        [SerializeField] private float silenceTimeout = 1.6f; // Autostop duration of silence

        private float[] micSamples = new float[512];

        public bool IsRecording => isRecording;

        public void SetListeningEnabled(bool enabled)
        {
            bool wasEnabled = isListeningEnabled;
            isListeningEnabled = enabled;
            if (enabled && !wasEnabled)
            {
                ResetMicrophoneCapture(
                    cancelTranscription: true);
                RefreshMicrophoneDevice();
                // Do not immediately transcribe the tail of companion speech
                // or a frame-load/UI sound when the microphone comes back.
                resumeListeningAt =
                    Time.unscaledTime +
                    ResumeCooldownSeconds;
            }
            if (!enabled)
            {
                ResetMicrophoneCapture(
                    cancelTranscription: true);
            }
            Debug.Log($"[VoiceToTextService] Listening enabled: {enabled}");
        }

        private volatile bool audioConfigurationChanged;
        private void OnEnable() => AudioSettings.OnAudioConfigurationChanged += OnAudioConfigurationChanged;
        private void OnDisable()
        {
            AudioSettings.OnAudioConfigurationChanged -= OnAudioConfigurationChanged;
            ResetMicrophoneCapture(cancelTranscription: true);
        }
        private void OnAudioConfigurationChanged(bool deviceWasChanged)
        {
            audioConfigurationChanged = true;
        }
        private void Start()
        {
            RefreshMicrophoneDevice();
        }

        private void RefreshMicrophoneDevice()
        {
            if (Microphone.devices.Length > 0)
            {
                micDevice = Microphone.devices[0];
                Debug.Log($"[VoiceToTextService] Default microphone detected: {micDevice}");
            }
            else
            {
                micDevice = string.Empty;
                Debug.LogWarning("[VoiceToTextService] No microphone devices found on this system.");
            }
        }

        private void ResetMicrophoneCapture(
            bool cancelTranscription)
        {
            if (!string.IsNullOrEmpty(micDevice) &&
                Microphone.IsRecording(micDevice))
            {
                Microphone.End(micDevice);
            }
            isRecording = false;
            micClip = null;
            lastVoiceTime = 0f;

            if (cancelTranscription)
            {
                StopAllCoroutines();
                isTranscribing = false;
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus || !isListeningEnabled)
            {
                return;
            }

            ResetMicrophoneCapture(
                cancelTranscription: false);
            RefreshMicrophoneDevice();
            resumeListeningAt =
                Time.unscaledTime +
                ResumeCooldownSeconds;
        }

        private void Update()
        {
            if (audioConfigurationChanged)
            {
                audioConfigurationChanged = false;
                ResetMicrophoneCapture(cancelTranscription: false);
                RefreshMicrophoneDevice();
                resumeListeningAt = Time.unscaledTime + ResumeCooldownSeconds;
            }
            if (!isListeningEnabled) return;
            if (Time.unscaledTime < resumeListeningAt) return;
            if (isTranscribing) return;
            if (string.IsNullOrEmpty(micDevice)) return;

            // Monitor mic levels and auto-trigger recording when user speaks
            if (!isRecording)
            {
                // Temporarily monitor mic levels in a loop
                if (!Microphone.IsRecording(micDevice))
                {
                    StartMicrophoneBuffer();
                }

                int position = Microphone.GetPosition(micDevice);
                int micPosition = (position - micSamples.Length + micClip.samples) % micClip.samples;
                if (position >= micSamples.Length || Time.unscaledTime - captureStartedAt >= CaptureBufferSeconds)
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
                        StartRecording();
                    }
                }
            }
            else
            {
                // Active recording: check for silence timeout to automatically stop
                int position = Microphone.GetPosition(micDevice);
                int micPosition = (position - micSamples.Length + micClip.samples) % micClip.samples;
                if (position >= micSamples.Length || Time.unscaledTime - captureStartedAt >= CaptureBufferSeconds)
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
                    if (Time.time - lastVoiceTime > silenceTimeout || Time.unscaledTime - recordingStartedAt >= MaxDurationSeconds)
                    {
                        StopRecordingAndTranscribe();
                    }
                }
            }
        }

        public void StartRecording()
        {
            if (isRecording || string.IsNullOrEmpty(micDevice)) return;

            if (micClip == null || !Microphone.IsRecording(micDevice))
                StartMicrophoneBuffer();
            if (micClip == null) return;
            int position = Microphone.GetPosition(micDevice);
            // Retain the trigger audio without stopping/reopening the device.
            // Clamp pre-roll while a fresh buffer is still filling.
            int available = Time.unscaledTime - captureStartedAt >= CaptureBufferSeconds
                ? micClip.samples : position;
            int preRoll = Mathf.Min(Mathf.RoundToInt(Samplerate * PreRollSeconds), available);
            recordingStartSample = (position - preRoll + micClip.samples) % micClip.samples;
            recordingStartedAt = Time.unscaledTime;

            isRecording = true;
            lastVoiceTime = Time.time;
            Debug.Log("[VoiceToTextService] Voice trigger activated! Recording started...");
        }

        private void StartMicrophoneBuffer()
        {
            micClip = Microphone.Start(micDevice, true, CaptureBufferSeconds, Samplerate);
            captureStartedAt = Time.unscaledTime;
        }

        private void StopRecordingAndTranscribe()
        {
            if (!isRecording) return;

            isRecording = false;
            int lastPos = Microphone.GetPosition(micDevice);
            Microphone.End(micDevice);

            int sampleCount = micClip != null
                ? (lastPos - recordingStartSample + micClip.samples) % micClip.samples : 0;
            if (sampleCount <= 0)
            {
                Debug.LogWarning("[VoiceToTextService] Recorded clip was empty.");
                return;
            }

            Debug.Log(
                "[VoiceToTextService] Recording complete, " +
                "starting transcription...");

            // Create a trimmed audioclip containing only recorded data
            float[] trimmedSamples = new float[sampleCount];
            // GetData wraps at the end of the circular clip, preserving order.
            micClip.GetData(trimmedSamples, recordingStartSample);

            if (!HasUsableSpeech(trimmedSamples))
            {
                Debug.Log(
                    "[VoiceToTextService] Ignoring blank audio.");
                return;
            }

            byte[] wavData = ConvertToWav(trimmedSamples, Samplerate);

            StartCoroutine(SendWhisperRequestCoroutine(wavData));
        }

        private IEnumerator SendWhisperRequestCoroutine(byte[] wavData)
        {
            ServiceConfig config = ServiceConfig.Load();
            isTranscribing = true;

            if (config.isLocalMode)
            {
                yield return StartCoroutine(
                    TranscribeWithLocalWhisper(
                        wavData,
                        config));
                isTranscribing = false;
                yield break;
            }

            string apiKey =
                string.IsNullOrEmpty(
                    config.speechToTextApiKey)
                    ? config.llmApiKey
                    : config.speechToTextApiKey;

            if (string.IsNullOrEmpty(apiKey))
            {
                Debug.LogError(
                    "[VoiceToTextService] Speech-to-text API " +
                    "key is missing.");
                isTranscribing = false;
                yield break;
            }

            string baseUrl =
                string.IsNullOrEmpty(
                    config.speechToTextBaseUrl)
                    ? "https://api.openai.com/v1"
                    : config.speechToTextBaseUrl;
            string endpoint =
                $"{baseUrl.TrimEnd('/')}/audio/transcriptions";

            // Cloud transcription uses its own model and credentials.
            WWWForm form = new WWWForm();
            form.AddBinaryData("file", wavData, "audio.wav", "audio/wav");
            form.AddField(
                "model",
                string.IsNullOrEmpty(
                    config.speechToTextModelName)
                    ? "whisper-1"
                    : config.speechToTextModelName);

            using (UnityWebRequest webRequest = UnityWebRequest.Post(endpoint, form))
            {
                if (!string.IsNullOrEmpty(apiKey))
                {
                    webRequest.SetRequestHeader("Authorization", $"Bearer {apiKey}");
                }

                yield return webRequest.SendWebRequest();

                if (webRequest.result == UnityWebRequest.Result.Success)
                {
                    string jsonResponse = webRequest.downloadHandler.text;
                    string text = ParseWhisperText(jsonResponse);
                    if (!string.IsNullOrEmpty(text))
                    {
                        DispatchTranscript(text);
                    }
                }
                else
                {
                    Debug.LogError($"[VoiceToTextService] Whisper transcription error: {webRequest.error}\nDetails: {webRequest.downloadHandler?.text}");
                }
            }
            isTranscribing = false;
        }

        private IEnumerator TranscribeWithLocalWhisper(
            byte[] wavData,
            ServiceConfig config)
        {
            string executablePath =
                ResolveLocalWhisperExecutable(config);
            string modelPath =
                ResolveLocalWhisperModel(config);

            if (!File.Exists(executablePath) ||
                !File.Exists(modelPath))
            {
                Debug.LogError(
                    "[VoiceToTextService] Local Whisper is not " +
                    "installed. Expected executable at " +
                    $"'{executablePath}' and model at " +
                    $"'{modelPath}'.");
                yield break;
            }

            string jobId =
                Guid.NewGuid().ToString("N");
            string inputPath =
                Path.Combine(
                    Application.temporaryCachePath,
                    $"jarvis-mic-{jobId}.wav");
            string outputBase =
                Path.Combine(
                    Application.temporaryCachePath,
                    $"jarvis-mic-{jobId}");
            string outputPath =
                outputBase + ".txt";

            Process process = null;
            Task<string> standardError = null;
            string startupError = null;

            try
            {
                Directory.CreateDirectory(
                    Application.temporaryCachePath);
                File.WriteAllBytes(
                    inputPath,
                    wavData);

                ProcessStartInfo startInfo =
                    new ProcessStartInfo
                    {
                        FileName = executablePath,
                        Arguments =
                            "-ng -t 4 -nt -np -sns -nth 0.45 -otxt " +
                            $"-m {QuoteArgument(modelPath)} " +
                            $"-f {QuoteArgument(inputPath)} " +
                            $"-of {QuoteArgument(outputBase)}",
                        WorkingDirectory =
                            Path.GetDirectoryName(
                                executablePath),
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };

                process = new Process
                {
                    StartInfo = startInfo
                };
                process.Start();
                process.StandardOutput.ReadToEndAsync();
                standardError =
                    process.StandardError.ReadToEndAsync();
            }
            catch (Exception ex)
            {
                startupError = ex.Message;
            }

            if (!string.IsNullOrEmpty(startupError) ||
                process == null)
            {
                Debug.LogError(
                    "[VoiceToTextService] Could not start local " +
                    $"Whisper: {startupError}");
                DeleteTemporaryFile(inputPath);
                yield break;
            }

            while (!process.HasExited)
            {
                yield return null;
            }

            string errorText =
                standardError != null &&
                standardError.IsCompleted
                    ? standardError.Result
                    : string.Empty;
            int exitCode = process.ExitCode;
            process.Dispose();

            if (exitCode != 0)
            {
                Debug.LogError(
                    "[VoiceToTextService] Local Whisper failed " +
                    $"with exit code {exitCode}. {errorText}");
            }
            else if (File.Exists(outputPath))
            {
                string transcript =
                    File.ReadAllText(outputPath).Trim();
                if (!string.IsNullOrEmpty(transcript))
                {
                    DispatchTranscript(transcript);
                }
                else
                {
                    Debug.Log(
                        "[VoiceToTextService] No speech detected.");
                }
            }
            else
            {
                Debug.LogError(
                    "[VoiceToTextService] Local Whisper did not " +
                    "produce a transcript.");
            }

            DeleteTemporaryFile(inputPath);
            DeleteTemporaryFile(outputPath);
        }

        private void DispatchTranscript(string text)
        {
            string trimmed = text.Trim();
            if (IsBlankTranscript(trimmed) ||
                IsLikelyWhisperHallucination(trimmed))
            {
                Debug.Log(
                    "[VoiceToTextService] Ignoring blank or phantom " +
                    "transcription.");
                return;
            }

            string normalized =
                NormalizeTranscript(trimmed);
            int wordCount =
                normalized.Split(
                    new[] { ' ' },
                    StringSplitOptions.RemoveEmptyEntries)
                .Length;
            if (wordCount <= 2 &&
                string.Equals(
                    normalized,
                    lastDispatchedTranscript,
                    StringComparison.Ordinal) &&
                Time.unscaledTime - lastTranscriptAt <
                    RepeatedShortTranscriptWindowSeconds)
            {
                Debug.Log(
                    "[VoiceToTextService] Ignoring repeated short " +
                    $"transcription: '{trimmed}'");
                return;
            }

            lastDispatchedTranscript = normalized;
            lastTranscriptAt = Time.unscaledTime;

            Debug.Log(
                $"[VoiceToTextService] Transcribed speech: " +
                $"'{trimmed}'");
            CompanionController controller =
                FindAnyObjectByType<CompanionController>();
            if (controller != null)
            {
                controller.ProcessVoiceInput(trimmed);
            }
        }

        private static bool IsLikelyWhisperHallucination(
            string text)
        {
            string normalized = NormalizeTranscript(text);
            return normalized == "you" ||
                   normalized == "thanks for watching" ||
                   normalized == "thank you for watching" ||
                   normalized == "please subscribe" ||
                   normalized == "subtitles by amara org";
        }

        private static string NormalizeTranscript(string text)
        {
            StringBuilder normalized = new StringBuilder();
            bool previousWasSpace = true;
            foreach (char character in
                (text ?? string.Empty).ToLowerInvariant())
            {
                if (char.IsLetterOrDigit(character))
                {
                    normalized.Append(character);
                    previousWasSpace = false;
                }
                else if (!previousWasSpace)
                {
                    normalized.Append(' ');
                    previousWasSpace = true;
                }
            }
            return normalized.ToString().Trim();
        }

        private bool HasUsableSpeech(float[] samples)
        {
            if (samples == null ||
                samples.Length <
                    Mathf.CeilToInt(
                        Samplerate * MinimumSpeechSeconds))
            {
                return false;
            }

            float sampleThreshold =
                Mathf.Max(0.003f, voiceThreshold * 0.5f);
            int requiredVoicedSamples =
                Mathf.CeilToInt(
                    Samplerate * MinimumSpeechSeconds);
            int voicedSamples = 0;
            double sumSquares = 0d;

            for (int i = 0; i < samples.Length; i++)
            {
                float absolute = Mathf.Abs(samples[i]);
                sumSquares += samples[i] * samples[i];
                if (absolute >= sampleThreshold)
                {
                    voicedSamples++;
                }
            }

            float rms = Mathf.Sqrt(
                (float)(sumSquares / samples.Length));
            return voicedSamples >= requiredVoicedSamples &&
                rms >= Mathf.Max(0.0015f, voiceThreshold * 0.1f);
        }

        private static bool IsBlankTranscript(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return true;
            }

            string normalized =
                text.Trim().ToLowerInvariant();
            string[] silenceMarkers =
            {
                "[blank_audio]",
                "[blank audio]",
                "[silence]",
                "(silence)",
                "[no speech]",
                "(no speech)",
                "[inaudible]",
                "(inaudible)",
                "[music]",
                "(music)"
            };

            for (int i = 0; i < silenceMarkers.Length; i++)
            {
                if (normalized == silenceMarkers[i])
                {
                    return true;
                }
            }

            for (int i = 0; i < normalized.Length; i++)
            {
                if (char.IsLetterOrDigit(normalized[i]))
                {
                    return false;
                }
            }
            return true;
        }

        private static string
            ResolveLocalWhisperExecutable(
                ServiceConfig config)
        {
            if (!string.IsNullOrWhiteSpace(
                    config.localWhisperExecutablePath))
            {
                return config.localWhisperExecutablePath;
            }

            return Path.Combine(
                GetDefaultWhisperToolsRoot(),
                "tools",
                "jarvis-whisper",
                "whisper.cpp",
                "build",
                "bin",
                "whisper-cli");
        }

        private static string ResolveLocalWhisperModel(
            ServiceConfig config)
        {
            if (!string.IsNullOrWhiteSpace(
                    config.localWhisperModelPath))
            {
                return config.localWhisperModelPath;
            }

            return Path.Combine(
                GetDefaultWhisperToolsRoot(),
                "tools",
                "jarvis-whisper",
                "whisper.cpp",
                "models",
                "ggml-base.en.bin");
        }

        private static string GetDefaultWhisperToolsRoot()
        {
            string userProfile =
                Environment.GetFolderPath(
                    Environment.SpecialFolder.UserProfile);
            return Path.Combine(
                userProfile,
                "Documents",
                "Codex");
        }

        private static string QuoteArgument(string value)
        {
            return "\"" +
                value.Replace("\\", "\\\\")
                    .Replace("\"", "\\\"") +
                "\"";
        }

        private static void DeleteTemporaryFile(
            string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    "[VoiceToTextService] Could not remove " +
                    $"temporary audio: {ex.Message}");
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
                        float clamped =
                            Mathf.Clamp(s, -1f, 1f);
                        writer.Write(
                            (short)(
                                clamped *
                                short.MaxValue));
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
