using System;
using System.IO;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using AICompanion.Core;

namespace AICompanion.Services
{
    public class ElevenLabsVoiceService : MonoBehaviour, IVoiceService
    {
        public IEnumerator GenerateSpeechCoroutine(string text, Action<AudioClip> onComplete, Action<string> onError)
        {
            ServiceConfig config = ServiceConfig.Load();

            if (string.IsNullOrEmpty(config.elevenLabsApiKey))
            {
                onError?.Invoke("ElevenLabs API Key is missing. Please configure it in Settings.");
                yield break;
            }

            string voiceId = string.IsNullOrEmpty(config.elevenLabsVoiceId) ? "Vw2FAddBZobPTywXPmmm" : config.elevenLabsVoiceId;
            string modelId = string.IsNullOrEmpty(config.elevenLabsModelId) ? "eleven_multilingual_v2" : config.elevenLabsModelId;
            string url = $"https://api.elevenlabs.io/v1/text-to-speech/{voiceId}";

            // Request body JSON
            string jsonPayload = "{" +
                $"\"text\": \"{EscapeJson(text)}\"," +
                $"\"model_id\": \"{EscapeJson(modelId)}\"," +
                "\"voice_settings\": {" +
                    "\"stability\": 0.5," +
                    "\"similarity_boost\": 0.75" +
                "}" +
            "}";

            byte[] bodyBytes = Encoding.UTF8.GetBytes(jsonPayload);

            using (UnityWebRequest webRequest = new UnityWebRequest(url, "POST"))
            {
                webRequest.uploadHandler = new UploadHandlerRaw(bodyBytes);
                webRequest.downloadHandler = new DownloadHandlerBuffer();
                webRequest.timeout = 30;
                webRequest.SetRequestHeader("Content-Type", "application/json");
                webRequest.SetRequestHeader("xi-api-key", config.elevenLabsApiKey);

                yield return webRequest.SendWebRequest();

                if (webRequest.result == UnityWebRequest.Result.Success)
                {
                    byte[] audioBytes = webRequest.downloadHandler.data;
                    if (audioBytes == null || audioBytes.Length == 0)
                    {
                        onError?.Invoke("Received empty audio data from ElevenLabs.");
                        yield break;
                    }

                    // Save raw MP3 bytes to cache to allow native MPEG decoding via UnityWebRequestMultimedia
                    string cachedFilePath =
                        Path.Combine(
                            Application.temporaryCachePath,
                            "elevenlabs_" +
                            Guid.NewGuid().ToString("N") +
                            ".mp3");
                    try
                    {
                        File.WriteAllBytes(cachedFilePath, audioBytes);
                    }
                    catch (Exception ex)
                    {
                        onError?.Invoke($"Failed to cache voice file: {ex.Message}");
                        yield break;
                    }

                    // Load MP3 clip using Unity's built-in MPEG audio decoder
                    string fileUrl = "file://" + cachedFilePath;
                    using (UnityWebRequest audioRequest = UnityWebRequestMultimedia.GetAudioClip(fileUrl, AudioType.MPEG))
                    {
                        audioRequest.timeout = 10;
                        yield return audioRequest.SendWebRequest();

                        if (audioRequest.result == UnityWebRequest.Result.Success)
                        {
                            AudioClip clip = DownloadHandlerAudioClip.GetContent(audioRequest);
                            if (clip != null && clip.loadState == AudioDataLoadState.Loaded)
                            {
                                onComplete?.Invoke(clip);
                            }
                            else
                            {
                                onError?.Invoke("Failed to load decoded AudioClip.");
                            }
                        }
                        else
                        {
                            onError?.Invoke($"Audio Decoding Error: {audioRequest.error}");
                        }
                    }

                    try
                    {
                        File.Delete(cachedFilePath);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning(
                            "[ElevenLabsVoiceService] Could not " +
                            $"remove temporary speech audio: {ex.Message}");
                    }
                }
                else
                {
                    string errorDetails = webRequest.downloadHandler != null ? webRequest.downloadHandler.text : "";
                    onError?.Invoke($"ElevenLabs API Error: {webRequest.error}\nDetails: {errorDetails}");
                }
            }
        }

        private string EscapeJson(string str)
        {
            if (string.IsNullOrEmpty(str)) return "";
            StringBuilder sb = new StringBuilder();
            foreach (char c in str)
            {
                switch (c)
                {
                    case '\"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < ' ')
                        {
                            sb.Append($"\\u{(int)c:x4}");
                        }
                        else
                        {
                            sb.Append(c);
                        }
                        break;
                }
            }
            return sb.ToString();
        }
    }
}
