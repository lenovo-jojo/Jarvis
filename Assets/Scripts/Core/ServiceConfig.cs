using System;
using System.IO;
using UnityEngine;

namespace AICompanion.Core
{
    [Serializable]
    public class ServiceConfig
    {
        public string llmBaseUrl = "http://localhost:11434/v1";
        public string llmApiKey = "";
        public string llmModelName = "dolphin-mistral";
        public string elevenLabsApiKey = "";
        public string elevenLabsVoiceId = "Vw2FAddBZobPTywXPmmm"; // Custom default
        public string elevenLabsModelId = "eleven_multilingual_v2";
        public bool isLocalMode = true;
        public string customVrmPath = "";

        private static string FilePath => Path.Combine(Application.persistentDataPath, "companion_config.json");

        public static ServiceConfig Load()
        {
            if (File.Exists(FilePath))
            {
                try
                {
                    string json = File.ReadAllText(FilePath);
                    return JsonUtility.FromJson<ServiceConfig>(json);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error loading config: {ex.Message}");
                }
            }
            return new ServiceConfig();
        }

        public void Save()
        {
            try
            {
                string json = JsonUtility.ToJson(this, true);
                File.WriteAllText(FilePath, json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error saving config: {ex.Message}");
            }
        }
    }
}
