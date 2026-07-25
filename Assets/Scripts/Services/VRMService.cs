using System;
using System.Collections.Generic;
using AICompanion.Core;
using UnityEngine;

namespace AICompanion.Services
{
    /// <summary>
    /// Stable facade that selects one VRM backend based on scripting defines.
    /// </summary>
    public class VRMService : MonoBehaviour, IVRMService
    {
        private const string Vrm10BackendTypeName = "AICompanion.Services.VRM10Service";
        private static readonly List<string> EmptyPoses = new List<string>();
        private IVRMService implementation;

        private void Awake()
        {
#if JARVIS_VRM10 && JARVIS_VRM0
            Debug.LogWarning("[VRMService] Both JARVIS_VRM10 and JARVIS_VRM0 are defined. Defaulting to VRM 1.0 backend.");
#endif

#if JARVIS_VRM10 || UNITY_EDITOR
            if (!TryAttachBackend(Vrm10BackendTypeName))
            {
                Debug.LogWarning("[VRMService] VRM10 backend was not found at runtime.");
                implementation = gameObject.AddComponent<VRMNoopService>();
            }
#elif JARVIS_VRM0
            implementation = gameObject.AddComponent<VRM0Service>();
#else
            implementation = gameObject.AddComponent<VRMNoopService>();
#endif
        }

        private bool TryAttachBackend(string fullTypeName)
        {
            Type backendType = null;
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var assembly in assemblies)
            {
                backendType = assembly.GetType(fullTypeName);
                if (backendType != null)
                {
                    break;
                }
            }

            if (backendType == null)
            {
                return false;
            }

            var component = gameObject.AddComponent(backendType);
            implementation = component as IVRMService;
            return implementation != null;
        }

        public void LoadVRM(string filePath, Action<GameObject> onLoaded, Action<string> onError)
        {
            if (implementation == null)
            {
                onError?.Invoke("[VRMService] No backend is active. Check JARVIS_VRM10/JARVIS_VRM0 scripting define symbols.");
                return;
            }

            implementation.LoadVRM(filePath, onLoaded, onError);
        }

        public void SetExpression(string expressionName, float value)
        {
            implementation?.SetExpression(expressionName, value);
        }

        public void PlayMouthVolume(float volume)
        {
            implementation?.PlayMouthVolume(volume);
        }

        public void SetAnimationEnabled(bool enabled)
        {
            implementation?.SetAnimationEnabled(enabled);
        }

        public void CyclePose()
        {
            implementation?.CyclePose();
        }

        public List<string> GetAvailablePoses()
        {
            if (implementation == null)
            {
                return EmptyPoses;
            }

            return implementation.GetAvailablePoses() ?? EmptyPoses;
        }

        public void SetPose(int index)
        {
            implementation?.SetPose(index);
        }
    }
}
