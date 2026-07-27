using System;
using System.Collections.Generic;
using AICompanion.Core;
using UnityEngine;

namespace AICompanion.Services
{
    /// <summary>
    /// Safe fallback when no VRM backend is active.
    /// Always compiled so scene references are never broken.
    /// </summary>
    public class VRMNoopService : MonoBehaviour, IVRMService
    {
        private static readonly List<string> EmptyPoses = new List<string>();

        private void Start()
        {
#if !JARVIS_VRM10 && !JARVIS_VRM0
            Debug.LogWarning("[VRMService] No VRM backend enabled. Define JARVIS_VRM10 (recommended) or JARVIS_VRM0.");
#endif
        }

        public void LoadVRM(string filePath, Action<GameObject> onLoaded, Action<string> onError)
        {
            var message = "VRM support is disabled. Set scripting define symbols (JARVIS_VRM10 recommended) and install matching VRM packages.";
            Debug.LogWarning($"[VRMService] {message}");
            onError?.Invoke(message);
        }

        public void LoadPlayerVRM(
            string filePath,
            Action<GameObject> onLoaded,
            Action<string> onError)
        {
            LoadVRM(filePath, onLoaded, onError);
        }

        public void LoadWorldVRM(
            string characterId,
            string filePath,
            Vector3 position,
            Quaternion rotation,
            Action<GameObject> onLoaded,
            Action<string> onError)
        {
            LoadVRM(filePath, onLoaded, onError);
        }

        public void SetExpression(string expressionName, float value) { }
        public void PlayMouthVolume(float volume) { }
        public void SetSpeaking(bool speaking) { }
        public void SetAnimationEnabled(bool enabled) { }
        public void CyclePose() { }

        public List<string> GetAvailablePoses() => EmptyPoses;

        public void SetPose(int index) { }
    }
}
