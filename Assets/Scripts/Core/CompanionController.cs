using System;
using UnityEngine;
using AICompanion.Core;
using AICompanion.Services;
using AICompanion.UI;

namespace AICompanion.Core
{
    public class CompanionController : MonoBehaviour
    {
        [Header("UI Reference")]
        [SerializeField] private CompanionUI companionUI;

        private ILLMService llmService;
        private IVoiceService voiceService;
        private IVRMService vrmService;
        private IContextService contextService;
        private ISpeechToTextService sttService;

        private AudioSource audioSource;
        private bool isSpeaking;
        private bool useWebcamContext;
        private bool useScreenContext;

        private void Awake()
        {
            // 1. Create and configure required services
            GameObject serviceContainer = new GameObject("ServiceContainer");
            serviceContainer.transform.SetParent(transform);

            llmService = serviceContainer.AddComponent<LLMService>();
            voiceService = serviceContainer.AddComponent<ElevenLabsVoiceService>();
            vrmService = serviceContainer.AddComponent<VRMService>();
            contextService = serviceContainer.AddComponent<ContextService>();
            sttService = serviceContainer.AddComponent<VoiceToTextService>();

            // Register services
            ServiceLocator.Register<ILLMService>(llmService);
            ServiceLocator.Register<IVoiceService>(voiceService);
            ServiceLocator.Register<IVRMService>(vrmService);
            ServiceLocator.Register<IContextService>(contextService);
            ServiceLocator.Register<ISpeechToTextService>(sttService);

            // 2. Setup Audio Source for speech playback
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f; // 2D flat stereo playback for maximum clarity
            gameObject.AddComponent<AudioLipsyncAnalyzer>();
        }

        private void Start()
        {
            if (companionUI == null)
            {
                companionUI = UnityEngine.Object.FindAnyObjectByType<CompanionUI>();
                if (companionUI == null)
                {
                    Debug.LogError("CompanionUI not found in scene!");
                    return;
                }
            }

            // Bind UI triggers
            companionUI.OnSendMessageRequested += HandleUserMessage;
            companionUI.OnImportVrmRequested += HandleImportVRM;
            companionUI.OnWebcamToggleChanged += HandleWebcamToggle;
            companionUI.OnScreenToggleChanged += HandleScreenToggle;
            companionUI.OnAnimationToggleChanged += HandleAnimationToggle;
            companionUI.OnMicToggleChanged += HandleMicToggle;
            companionUI.OnCyclePoseRequested += HandleCyclePose;
            companionUI.OnPoseSelected += HandlePoseSelected;

            // Populate Pose Dropdown
            if (vrmService != null)
            {
                var poses = vrmService.GetAvailablePoses();
                companionUI.PopulatePoseDropdown(poses);
            }

            // Sync initial state
            if (sttService != null)
            {
                sttService.SetListeningEnabled(companionUI.IsMicEnabled);
            }
        }

        private void HandleCyclePose()
        {
            if (vrmService != null)
            {
                vrmService.CyclePose();
            }
        }

        private void HandlePoseSelected(int index)
        {
            if (vrmService != null)
            {
                vrmService.SetPose(index);
            }
        }

        private void HandleAnimationToggle(bool enabled)
        {
            if (vrmService != null)
            {
                vrmService.SetAnimationEnabled(enabled);
            }
        }

        private void Update()
        {
            // Update preview feeds at 5 FPS to avoid performance impact
            if (Time.frameCount % 12 == 0)
            {
                if (useWebcamContext && contextService != null)
                {
                    Texture2D camTex = contextService.GetCameraFrame();
                    companionUI.SetWebcamTexture(camTex);
                }

                if (useScreenContext && contextService != null)
                {
                    // Screen context capture (captureDesktop: true for full screen as requested)
                    Texture2D screenTex = contextService.GetScreenFrame(true);
                    companionUI.SetScreenTexture(screenTex);
                }
            }
        }

        private void HandleWebcamToggle(bool enabled)
        {
            useWebcamContext = enabled;
            if (enabled)
            {
                contextService.StartCamera();
                companionUI.AppendLog("System", "Webcam access granted. The AI can now see your physical world.");
            }
            else
            {
                contextService.StopCamera();
                companionUI.SetWebcamTexture(null);
                companionUI.AppendLog("System", "Webcam access revoked.");
            }
        }

        private void HandleScreenToggle(bool enabled)
        {
            useScreenContext = enabled;
            if (enabled)
            {
                companionUI.AppendLog("System", "Screen capture context enabled. The AI can see your application screen.");
            }
            else
            {
                companionUI.SetScreenTexture(null);
                companionUI.AppendLog("System", "Screen capture context disabled.");
            }
        }

        private void HandleMicToggle(bool enabled)
        {
            if (sttService != null)
            {
                sttService.SetListeningEnabled(enabled);
                companionUI.AppendLog("System", enabled ? "Voice-to-text input enabled." : "Voice-to-text input disabled.");
            }
        }

        public void ProcessVoiceInput(string message)
        {
            HandleUserMessage(message);
        }

        private void HandleUserMessage(string message)
        {
            if (isSpeaking)
            {
                audioSource.Stop();
                isSpeaking = false;
            }

            companionUI.AppendLog("You", message);

            // Get current active frames if toggled on
            Texture2D webcamFrame = useWebcamContext ? contextService.GetCameraFrame() : null;
            Texture2D screenFrame = useScreenContext ? contextService.GetScreenFrame(false) : null;

            companionUI.AppendLog("System", "AI is thinking...");

            StartCoroutine(llmService.SendMessageCoroutine(
                message,
                screenFrame,
                webcamFrame,
                onComplete: (aiText) =>
                {
                    companionUI.AppendLog("AI", aiText);
                    TriggerExpressionFromText(aiText);
                    SpeakText(aiText);
                },
                onError: (error) =>
                {
                    companionUI.AppendLog("System", $"Error: {error}");
                    Debug.LogError(error);
                }
            ));
        }

        private void SpeakText(string text)
        {
            isSpeaking = true;
            StartCoroutine(voiceService.GenerateSpeechCoroutine(
                text,
                onComplete: (clip) =>
                {
                    audioSource.clip = clip;
                    audioSource.Play();
                },
                onError: (error) =>
                {
                    companionUI.AppendLog("System", $"Speech Generation Failed: {error}");
                    isSpeaking = false;
                }
            ));
        }

        private void TriggerExpressionFromText(string text)
        {
            string lowerText = text.ToLower();
            if (lowerText.Contains("!") || lowerText.Contains("happy") || lowerText.Contains("haha") || lowerText.Contains("smile") || lowerText.Contains("glad"))
            {
                vrmService.SetExpression("happy", 1.0f);
            }
            else if (lowerText.Contains("?") || lowerText.Contains("think") || lowerText.Contains("hmm") || lowerText.Contains("ponder"))
            {
                vrmService.SetExpression("thinking", 1.0f);
            }
            else if (lowerText.Contains("sorry") || lowerText.Contains("sad") || lowerText.Contains("cry"))
            {
                vrmService.SetExpression("sad", 1.0f);
            }
            else
            {
                vrmService.SetExpression("neutral", 1.0f);
            }
        }

        private void HandleImportVRM(string path)
        {
            companionUI.AppendLog("System", $"Attempting to import VRM avatar: {path}");
            vrmService.LoadVRM(
                path,
                onLoaded: (avatar) =>
                {
                    companionUI.AppendLog("System", "VRM avatar imported successfully!");
                },
                onError: (error) =>
                {
                    companionUI.AppendLog("System", $"Avatar Load Error: {error}");
                }
            );
        }

        private void OnDestroy()
        {
            if (companionUI != null)
            {
                companionUI.OnSendMessageRequested -= HandleUserMessage;
                companionUI.OnImportVrmRequested -= HandleImportVRM;
                companionUI.OnWebcamToggleChanged -= HandleWebcamToggle;
                companionUI.OnScreenToggleChanged -= HandleScreenToggle;
                companionUI.OnAnimationToggleChanged -= HandleAnimationToggle;
                companionUI.OnMicToggleChanged -= HandleMicToggle;
                companionUI.OnCyclePoseRequested -= HandleCyclePose;
                companionUI.OnPoseSelected -= HandlePoseSelected;
            }
        }
    }
}
