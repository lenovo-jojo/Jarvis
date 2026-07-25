using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using AICompanion.Core;

namespace AICompanion.Services
{
    public class LLMService : MonoBehaviour, ILLMService
    {
        [Serializable]
        private class ChatMessageItem
        {
            public string role;
            public string text;
            public string base64Image; // Optional camera image
            public string base64Screen; // Optional screen image
        }

        private List<ChatMessageItem> chatHistory = new List<ChatMessageItem>();
        private const string SystemPrompt = "You are a helpful, responsive, and friendly 3D anime companion. Keep your responses expressive, slightly playful, and relatively brief (2-3 sentences max) so they can be spoken clearly. When images of the user's camera or screen are provided, refer to them naturally to show you can see what they are doing or where they are.";

        public void ClearHistory()
        {
            chatHistory.Clear();
        }

        public IEnumerator SendMessageCoroutine(string userMessage, Texture2D screenshot, Texture2D webcamFeed, Action<string> onComplete, Action<string> onError)
        {
            ServiceConfig config = ServiceConfig.Load();

            // Determine URL and API Key based on mode
            string baseUrl = config.isLocalMode ? config.llmBaseUrl : "https://api.openai.com/v1";
            string apiKey = config.isLocalMode ? "" : config.llmApiKey;
            string modelName = config.isLocalMode ? config.llmModelName : "gpt-4o-mini"; // Standard multimodal cloud model

            // If empty baseUrl, fallback to default Ollama
            if (string.IsNullOrEmpty(baseUrl))
            {
                baseUrl = "http://localhost:11434/v1";
            }

            string endpoint = $"{baseUrl.TrimEnd('/')}/chat/completions";

            // Encode textures to Base64
            string base64Cam = null;
            string base64Scr = null;

            if (webcamFeed != null)
            {
                try
                {
                    byte[] jpegBytes = webcamFeed.EncodeToJPG(60);
                    base64Cam = Convert.ToBase64String(jpegBytes);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Webcam texture encoding error: {ex.Message}");
                }
            }

            if (screenshot != null)
            {
                try
                {
                    byte[] jpegBytes = screenshot.EncodeToJPG(60);
                    base64Scr = Convert.ToBase64String(jpegBytes);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Screenshot texture encoding error: {ex.Message}");
                }
            }

            // Append new user message to local history list
            chatHistory.Add(new ChatMessageItem
            {
                role = "user",
                text = userMessage,
                base64Image = base64Cam,
                base64Screen = base64Scr
            });

            // Construct JSON payload manually to guarantee compliance and support vision arrays perfectly
            StringBuilder jsonBuilder = new StringBuilder();
            jsonBuilder.Append("{");
            jsonBuilder.Append($"\"model\": \"{modelName}\",");
            jsonBuilder.Append("\"messages\": [");

            // 1. Append System Prompt
            jsonBuilder.Append("{");
            jsonBuilder.Append("\"role\": \"system\",");
            jsonBuilder.Append($"\"content\": \"{EscapeJson(SystemPrompt)}\"");
            jsonBuilder.Append("}");

            // 2. Append Chat History
            foreach (var item in chatHistory)
            {
                jsonBuilder.Append(",{");
                jsonBuilder.Append($"\"role\": \"{item.role}\",");

                if (!string.IsNullOrEmpty(item.base64Image) || !string.IsNullOrEmpty(item.base64Screen))
                {
                    // Multimodal array format
                    jsonBuilder.Append("\"content\": [");
                    jsonBuilder.Append("{");
                    jsonBuilder.Append("\"type\": \"text\",");
                    jsonBuilder.Append($"\"text\": \"{EscapeJson(item.text)}\"");
                    jsonBuilder.Append("}");

                    if (!string.IsNullOrEmpty(item.base64Image))
                    {
                        jsonBuilder.Append(",{");
                        jsonBuilder.Append("\"type\": \"image_url\",");
                        jsonBuilder.Append("\"image_url\": {");
                        jsonBuilder.Append($"\"url\": \"data:image/jpeg;base64,{item.base64Image}\"");
                        jsonBuilder.Append("}");
                        jsonBuilder.Append("}");
                    }

                    if (!string.IsNullOrEmpty(item.base64Screen))
                    {
                        jsonBuilder.Append(",{");
                        jsonBuilder.Append("\"type\": \"image_url\",");
                        jsonBuilder.Append("\"image_url\": {");
                        jsonBuilder.Append($"\"url\": \"data:image/jpeg;base64,{item.base64Screen}\"");
                        jsonBuilder.Append("}");
                        jsonBuilder.Append("}");
                    }

                    jsonBuilder.Append("]");
                }
                else
                {
                    // Simple string format
                    jsonBuilder.Append($"\"content\": \"{EscapeJson(item.text)}\"");
                }

                jsonBuilder.Append("}");
            }

            jsonBuilder.Append("]");
            jsonBuilder.Append("}");

            byte[] jsonBytes = Encoding.UTF8.GetBytes(jsonBuilder.ToString());

            using (UnityWebRequest webRequest = new UnityWebRequest(endpoint, "POST"))
            {
                webRequest.uploadHandler = new UploadHandlerRaw(jsonBytes);
                webRequest.downloadHandler = new DownloadHandlerBuffer();
                webRequest.SetRequestHeader("Content-Type", "application/json");

                if (!string.IsNullOrEmpty(apiKey))
                {
                    webRequest.SetRequestHeader("Authorization", $"Bearer {apiKey}");
                }

                yield return webRequest.SendWebRequest();

                if (webRequest.result == UnityWebRequest.Result.Success)
                {
                    string responseText = webRequest.downloadHandler.text;
                    string aiResponse = ParseChatResponse(responseText);

                    if (!string.IsNullOrEmpty(aiResponse))
                    {
                        // Enforce all lowercase letters for the companion's dialogue style
                        aiResponse = aiResponse.ToLowerInvariant();

                        // Add AI response to history
                        chatHistory.Add(new ChatMessageItem { role = "assistant", text = aiResponse });

                        // Cap history to last 10 messages to avoid context bloat
                        if (chatHistory.Count > 10)
                        {
                            chatHistory.RemoveRange(0, chatHistory.Count - 10);
                        }

                        onComplete?.Invoke(aiResponse);
                    }
                    else
                    {
                        onError?.Invoke("Failed to parse response payload.");
                    }
                }
                else
                {
                    string errorMsg = $"HTTP Error: {webRequest.responseCode} - {webRequest.error}";
                    if (webRequest.downloadHandler != null)
                    {
                        errorMsg += $"\nDetails: {webRequest.downloadHandler.text}";
                    }
                    onError?.Invoke(errorMsg);
                }
            }
        }

        private string ParseChatResponse(string json)
        {
            // Simple robust JSON parser for standard OpenAI chat response
            try
            {
                int contentIndex = json.IndexOf("\"content\"");
                if (contentIndex == -1) return null;

                // Move past "\"content\"" and the ":"
                int startQuote = json.IndexOf("\"", contentIndex + 9);
                while (startQuote != -1 && json[startQuote - 1] == '\\') // Ensure not escaped
                {
                    startQuote = json.IndexOf("\"", startQuote + 1);
                }

                if (startQuote == -1) return null;
                startQuote++; // Inside the quote

                // Find the ending matching quote, skipping escaped quotes \"
                StringBuilder sb = new StringBuilder();
                for (int i = startQuote; i < json.Length; i++)
                {
                    if (json[i] == '"' && json[i - 1] != '\\')
                    {
                        break;
                    }
                    sb.Append(json[i]);
                }

                return UnescapeJson(sb.ToString());
            }
            catch (Exception ex)
            {
                Debug.LogError($"Parser exception: {ex.Message}");
                return null;
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

        private string UnescapeJson(string str)
        {
            if (string.IsNullOrEmpty(str)) return "";
            return str
                .Replace("\\\"", "\"")
                .Replace("\\\\", "\\")
                .Replace("\\n", "\n")
                .Replace("\\r", "\r")
                .Replace("\\t", "\t");
        }
    }
}
