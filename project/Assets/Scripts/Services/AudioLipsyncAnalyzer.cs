using UnityEngine;
using AICompanion.Core;

namespace AICompanion.Services
{
    [RequireComponent(typeof(AudioSource))]
    public class AudioLipsyncAnalyzer : MonoBehaviour
    {
        private AudioSource audioSource;
        private IVRMService vrmService;
        private readonly float[] samples = new float[256];

        [Header("Sensitivity Settings")]
        [Range(1f, 10f)]
        [SerializeField] private float multiplier = 3.0f;
        [Range(0.01f, 0.5f)]
        [SerializeField] private float smoothness = 0.15f;

        private float smoothedVolume;

        private void Awake()
        {
            audioSource = GetComponent<AudioSource>();
        }

        private void Start()
        {
            try
            {
                vrmService = ServiceLocator.Resolve<IVRMService>();
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"VRM service lookup failed in lipsync analyzer: {ex.Message}");
            }
        }

        private void Update()
        {
            if (vrmService == null)
            {
                try
                {
                    vrmService = ServiceLocator.Resolve<IVRMService>();
                }
                catch {}
            }

            if (audioSource == null || !audioSource.isPlaying || vrmService == null)
            {
                if (vrmService != null)
                {
                    vrmService.PlayMouthVolume(0f); // Closed mouth
                }
                return;
            }

            // Extract audio wave output samples
            audioSource.GetOutputData(samples, 0);

            // Calculate RMS (Root Mean Square) volume level
            float sum = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                sum += samples[i] * samples[i];
            }

            float rms = Mathf.Sqrt(sum / samples.Length);
            float targetVolume = Mathf.Clamp01(rms * multiplier);

            // Smooth the transition to avoid jittery movements
            smoothedVolume = Mathf.Lerp(smoothedVolume, targetVolume, smoothness);

            // Forward volume directly to the active VRM avatar controller
            vrmService.PlayMouthVolume(smoothedVolume);
        }
    }
}
