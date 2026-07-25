#if !JARVIS_VRM10 && !JARVIS_VRM0
using System;
using System.Collections.Generic;
using AICompanion.Core;
using UnityEngine;

namespace AICompanion.Services
{
    /// <summary>
    /// Safe fallback when no VRM package/mode is configured.
    /// </summary>
    public class VRMNoopService : MonoBehaviour, IVRMService
    {
        private static readonly List<string> EmptyPoses = new List<string>();

        private void Start()
        {
            Debug.LogWarning("[VRMService] No VRM backend enabled. Define JARVIS_VRM10 (recommended) or JARVIS_VRM0.");
        }

        public void LoadVRM(string filePath, Action<GameObject> onLoaded, Action<string> onError)
        {
            var message = "VRM support is disabled. Set scripting define symbols (JARVIS_VRM10 recommended) and install matching VRM packages.";
            Debug.LogWarning($"[VRMService] {message}");
            onError?.Invoke(message);
        }

        public void SetExpression(string expressionName, float value)
        {
        }

        public void PlayMouthVolume(float volume)
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
