#if !JARVIS_VRM10 && JARVIS_VRM0
using System;
using System.Collections.Generic;
using System.IO;
using AICompanion.Core;
using UnityEngine;

namespace AICompanion.Services
{
    /// <summary>
    /// Compile-safe VRM 0.x compatibility backend.
    /// </summary>
    public class VRM0Service : MonoBehaviour, IVRMService
    {
        private static readonly List<string> EmptyPoses = new List<string>();
        private bool hasLoggedModeWarning;

        private void LogModeWarningOnce()
        {
            if (hasLoggedModeWarning)
            {
                return;
            }

            hasLoggedModeWarning = true;
            Debug.Log("[VRMService] VRM 0.x compatibility mode is active (JARVIS_VRM0).");
        }

        public void LoadVRM(string filePath, Action<GameObject> onLoaded, Action<string> onError)
        {
            LogModeWarningOnce();

            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                var message = $"VRM file not found at: {filePath}";
                Debug.LogError($"[VRMService] {message}");
                onError?.Invoke(message);
                return;
            }

            var messageText = "VRM 0.x compatibility backend is compile-safe only in this package version. Enable JARVIS_VRM10 for full runtime loading support.";
            Debug.LogWarning($"[VRMService] {messageText}");
            onError?.Invoke(messageText);
        }

        public void SetExpression(string expressionName, float value)
        {
        }

        public void PlayMouthVolume(float volume)
        {
        }

        public void SetSpeaking(bool speaking)
        {
        }

        public void SetAnimationEnabled(bool enabled)
        {
        }

        public void CyclePose()
        {
        }

        public List<string> GetAvailablePoses()
        {
            return EmptyPoses;
        }

        public void SetPose(int index)
        {
        }
    }
}
#endif
