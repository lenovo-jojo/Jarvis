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

        // Saved Frame Data
        public bool hasSavedFrame = false;
        public string savedPoseName = "";

        // Avatar position and rotation
        public float avatarPosX = 0f;
        public float avatarPosY = 0.02f;
        public float avatarPosZ = 2f;
        public float avatarRotX = 0f;
        public float avatarRotY = 0f;
        public float avatarRotZ = 0f;
        public float avatarRotW = 1f;

        // Camera values
        public float camOrbitYaw = 180f;
        public float camOrbitPitch = 10f;
        public float camZoomOffset = 0f;
        public float camSceneOffsetX = 0f;
        public float camSceneOffsetY = 0f;
        public float camSceneOffsetZ = 0f;

        public float camPosX = 0f;
        public float camPosY = 0f;
        public float camPosZ = 0f;
        public float camRotX = 0f;
        public float camRotY = 0f;
        public float camRotZ = 0f;
        public float camRotW = 1f;

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
