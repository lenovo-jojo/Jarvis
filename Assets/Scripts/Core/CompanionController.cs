using System;
using System.Collections.Generic;
using UnityEngine;
using AICompanion.Core;
using AICompanion.Services;
using AICompanion.UI;
using System.Collections;
using System.IO;

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
        private readonly List<string> availablePoses = new List<string>();
        private int selectedPoseIndex = -1;
        private Coroutine speechLifecycleCoroutine;
        private Coroutine voiceGenerationCoroutine;
        private Coroutine thinkingReactionCoroutine;
        private Coroutine autosaveCoroutine;
        private int requestGeneration;
        private CompanionCamera companionCamera;
        private const float AutosaveIntervalSeconds = 60f;

        private void Awake()
        {
            // 1. Create and configure required services
            GameObject serviceContainer = new GameObject("ServiceContainer");
            serviceContainer.transform.SetParent(transform);

            llmService = serviceContainer.AddComponent<LLMService>();
            voiceService = serviceContainer.AddComponent<CompanionVoiceService>();
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
            companionUI.OnExploreModeToggleChanged +=
                HandleExploreModeToggle;
            companionUI.OnCyclePoseRequested += HandleCyclePose;
            companionUI.OnPoseSelected += HandlePoseSelected;
            companionUI.OnZoomInSliderChanged += HandleZoomInSliderChanged;
            companionUI.OnZoomOutSliderChanged += HandleZoomOutSliderChanged;
            companionUI.OnSaveFrameRequested += SaveCurrentFrame;
            companionUI.OnLoadFrameRequested += LoadSavedFrame;
            companionCamera = Camera.main != null ? Camera.main.GetComponent<CompanionCamera>() : null;
            LoadPlayerAvatar();

            // Populate Pose Dropdown
            if (vrmService != null)
            {
                availablePoses.Clear();
                var poses = vrmService.GetAvailablePoses();
                if (poses != null)
                {
                    availablePoses.AddRange(poses);
                }
                companionUI.PopulatePoseDropdown(availablePoses);

                // Sync initial pose if VRM is already loaded
                ServiceConfig config = ServiceConfig.Load();
                if (config != null && config.hasSavedFrame && !string.IsNullOrEmpty(config.savedPoseName))
                {
                    selectedPoseIndex = GetPoseIndexByName(config.savedPoseName);
                }
                else
                {
                    selectedPoseIndex = GetPoseIndexByName("Feet Focus");
                }

                if (selectedPoseIndex != -1)
                {
                    companionUI.SetSelectedPoseInDropdown(selectedPoseIndex);
                }

                if (availablePoses.Count == 0)
                {
                    companionUI.AppendLog("System", "No poses found in Assets/OptionalContent/Solo Scenes.");
                }
                else
                {
                    Debug.Log(
                        $"[CompanionController] Loaded " +
                        $"{availablePoses.Count} poses.");
                }
            }

            // Sync initial state
            if (sttService != null)
            {
                sttService.SetListeningEnabled(companionUI.IsMicEnabled);
            }

            autosaveCoroutine =
                StartCoroutine(AutosaveRoutine());
        }

        private void HandleCyclePose()
        {
            if (vrmService == null)
            {
                return;
            }

            if (availablePoses.Count == 0)
            {
                companionUI.AppendLog("System", "No poses available.");
                return;
            }

            int nextPoseIndex = -1;
            for (int offset = 1;
                 offset <= availablePoses.Count;
                 offset++)
            {
                int candidate =
                    (selectedPoseIndex + offset) %
                    availablePoses.Count;
                if (companionUI == null ||
                    companionUI.IsPoseEnabledForRotation(candidate))
                {
                    nextPoseIndex = candidate;
                    break;
                }
            }

            if (nextPoseIndex < 0)
            {
                Debug.LogWarning(
                    "[CompanionController] Pose rotation is empty.");
                return;
            }

            selectedPoseIndex = nextPoseIndex;
            vrmService.SetPose(selectedPoseIndex);
            companionUI.SetSelectedPoseInDropdown(selectedPoseIndex);
            Debug.Log(
                $"[CompanionController] Pose changed: " +
                availablePoses[selectedPoseIndex]);
        }

        private void HandlePoseSelected(int index)
        {
            if (vrmService == null)
            {
                return;
            }

            if (availablePoses.Count == 0)
            {
                companionUI.AppendLog("System", "No poses available.");
                return;
            }

            selectedPoseIndex = Mathf.Clamp(index, 0, availablePoses.Count - 1);
            vrmService.SetPose(selectedPoseIndex);
            companionUI.SetSelectedPoseInDropdown(selectedPoseIndex);
            Debug.Log(
                $"[CompanionController] Pose selected: " +
                availablePoses[selectedPoseIndex]);
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
            if (IsNonSpokenStatusText(message))
            {
                Debug.Log(
                    "[CompanionController] Ignored thinking-status " +
                    "microphone feedback.");
                return;
            }
            HandleUserMessage(message);
        }

        private void HandleUserMessage(string message)
        {
            requestGeneration++;
            int currentRequestGeneration =
                requestGeneration;
            EndThinkingReaction(false);

            if (voiceGenerationCoroutine != null)
            {
                StopCoroutine(voiceGenerationCoroutine);
                voiceGenerationCoroutine = null;
            }
            if (speechLifecycleCoroutine != null)
            {
                StopCoroutine(speechLifecycleCoroutine);
                speechLifecycleCoroutine = null;
            }
            if (isSpeaking)
            {
                audioSource.Stop();
                isSpeaking = false;
                vrmService.SetSpeaking(false);
                vrmService.SetExpression("neutral", 1.0f);
            }

            companionUI.AppendLog("You", message);

            // Get current active frames if toggled on
            Texture2D webcamFrame = useWebcamContext ? contextService.GetCameraFrame() : null;
            Texture2D screenFrame =
                useScreenContext
                    ? contextService.GetScreenFrame(true)
                    : null;

            StartThinkingReaction();

            StartCoroutine(llmService.SendMessageCoroutine(
                message,
                screenFrame,
                webcamFrame,
                onComplete: (aiText) =>
                {
                    if (currentRequestGeneration !=
                        requestGeneration)
                    {
                        return;
                    }
                    EndThinkingReaction(false);
                    TriggerExpressionFromText(aiText);
                    SpeakText(
                        aiText,
                        currentRequestGeneration);
                },
                onError: (error) =>
                {
                    if (currentRequestGeneration !=
                        requestGeneration)
                    {
                        return;
                    }
                    EndThinkingReaction(true);
                    companionUI.AppendLog("System", $"Error: {error}");
                    Debug.LogError(error);
                }
            ));
        }

        private void StartThinkingReaction()
        {
            if (thinkingReactionCoroutine != null)
            {
                StopCoroutine(thinkingReactionCoroutine);
            }
            thinkingReactionCoroutine =
                StartCoroutine(ThinkingReactionRoutine());
        }

        private IEnumerator ThinkingReactionRoutine()
        {
            vrmService.SetSpeaking(false);
            vrmService.SetExpression(
                "surprised",
                0.3f);
            yield return new WaitForSeconds(0.16f);
            vrmService.SetExpression(
                "thinking",
                0.72f);
            thinkingReactionCoroutine = null;
        }

        private void EndThinkingReaction(bool returnToNeutral)
        {
            if (thinkingReactionCoroutine != null)
            {
                StopCoroutine(thinkingReactionCoroutine);
                thinkingReactionCoroutine = null;
            }
            if (returnToNeutral && vrmService != null)
            {
                vrmService.SetExpression(
                    "neutral",
                    1f);
            }
        }

        private static bool IsNonSpokenStatusText(
            string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }
            string normalized =
                text.Trim().ToLowerInvariant()
                    .Replace("system:", string.Empty)
                    .Trim();
            return normalized == "ai is thinking" ||
                   normalized == "ai is thinking..." ||
                   normalized == "thinking" ||
                   normalized == "thinking...";
        }

        private void SpeakText(
            string text,
            int currentRequestGeneration)
        {
            if (IsNonSpokenStatusText(text))
            {
                Debug.LogWarning(
                    "[CompanionController] Blocked status text " +
                    "from the TTS pipeline.");
                return;
            }

            Debug.Log(
                "[CompanionController] AI response ready: " +
                text);

            voiceGenerationCoroutine =
                StartCoroutine(
                    voiceService.GenerateSpeechCoroutine(
                text,
                onComplete: (clip) =>
                {
                    voiceGenerationCoroutine = null;
                    if (currentRequestGeneration !=
                        requestGeneration)
                    {
                        return;
                    }
                    isSpeaking = true;
                    vrmService.SetSpeaking(true);
                    audioSource.clip = clip;
                    audioSource.Play();
                    companionUI.StartAISpeechTyping(
                        text,
                        clip.length);
                    if (speechLifecycleCoroutine != null)
                    {
                        StopCoroutine(speechLifecycleCoroutine);
                    }
                    speechLifecycleCoroutine = StartCoroutine(TrackSpeechLifecycle(text));
                },
                onError: (error) =>
                {
                    voiceGenerationCoroutine = null;
                    if (currentRequestGeneration !=
                        requestGeneration)
                    {
                        return;
                    }
                    companionUI.AppendLog("System", $"Speech Generation Failed: {error}");
                    companionUI.AppendLog("AI", text);
                    isSpeaking = false;
                    vrmService.SetSpeaking(false);
                    vrmService.SetExpression("neutral", 1.0f);
                }
            ));
        }

        private IEnumerator TrackSpeechLifecycle(string finalText)
        {
            while (audioSource != null && audioSource.isPlaying)
            {
                yield return null;
            }

            isSpeaking = false;
            vrmService.SetSpeaking(false);
            vrmService.SetExpression("neutral", 1.0f);
            companionUI.CompleteAISpeechTyping(finalText);
            speechLifecycleCoroutine = null;
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
                    selectedPoseIndex = GetPoseIndexByName("Feet Focus");
                    if (selectedPoseIndex != -1)
                    {
                        companionUI.SetSelectedPoseInDropdown(selectedPoseIndex);
                    }
                },
                onError: (error) =>
                {
                    companionUI.AppendLog("System", $"Avatar Load Error: {error}");
                }
            );
        }

        private void HandleZoomInSliderChanged(float value)
        {
            if (companionCamera == null && Camera.main != null)
            {
                companionCamera = Camera.main.GetComponent<CompanionCamera>();
            }
            if (companionCamera != null)
            {
                companionCamera.SetZoomInSlider(value);
            }
        }

        private void HandleZoomOutSliderChanged(float value)
        {
            if (companionCamera == null && Camera.main != null)
            {
                companionCamera = Camera.main.GetComponent<CompanionCamera>();
            }
            if (companionCamera != null)
            {
                companionCamera.SetZoomOutSlider(value);
            }
        }

        private void HandleExploreModeToggle(bool enabled)
        {
            if (companionCamera == null && Camera.main != null)
            {
                companionCamera =
                    Camera.main.GetComponent<CompanionCamera>();
            }

            companionCamera?.SetExploreMode(enabled);
        }

        private void LoadPlayerAvatar()
        {
            if (vrmService == null)
            {
                return;
            }

            ServiceConfig config = ServiceConfig.Load();
            string bundledPlayerPath = Path.Combine(
                Application.dataPath,
                "Characters",
                "jojo4.vrm");
            string playerPath =
                !string.IsNullOrWhiteSpace(config.playerVrmPath) &&
                File.Exists(config.playerVrmPath)
                    ? config.playerVrmPath
                    : bundledPlayerPath;

            if (!File.Exists(playerPath))
            {
                Debug.LogWarning(
                    $"[CompanionController] Player VRM not found: " +
                    $"{playerPath}");
                return;
            }

            vrmService.LoadPlayerVRM(
                playerPath,
                onLoaded: player =>
                {
                    if (companionCamera == null &&
                        Camera.main != null)
                    {
                        companionCamera =
                            Camera.main.GetComponent<CompanionCamera>();
                    }

                    companionCamera?.SetPlayerAvatar(
                        player.transform);
                    companionUI?.AppendLog(
                        "System",
                        "Jojo4 self-insert loaded for Explore Mode.");
                },
                onError: error =>
                {
                    companionUI?.AppendLog(
                        "System",
                        $"Player avatar load error: {error}");
                });
        }

        private int GetPoseIndexByName(string poseName)
        {
            for (int i = 0; i < availablePoses.Count; i++)
            {
                if (availablePoses[i].Equals(poseName, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }
            return -1;
        }

        public void SaveCurrentFrame()
        {
            SaveCurrentFrameInternal(true);
        }

        private void SaveCurrentFrameInternal(
            bool showConfirmation)
        {
            ServiceConfig config = ServiceConfig.Load();

            // 1. Get avatar position and rotation
            GameObject avatar = GameObject.Find("CompanionAvatarVRM");
            if (avatar == null)
            {
                avatar = GameObject.Find("CompanionAvatarProcedural");
            }

            if (avatar == null && !showConfirmation)
            {
                return;
            }

            if (avatar != null)
            {
                config.avatarPosX = avatar.transform.position.x;
                config.avatarPosY = avatar.transform.position.y;
                config.avatarPosZ = avatar.transform.position.z;

                config.avatarRotX = avatar.transform.rotation.x;
                config.avatarRotY = avatar.transform.rotation.y;
                config.avatarRotZ = avatar.transform.rotation.z;
                config.avatarRotW = avatar.transform.rotation.w;
            }

            // 2. Get current pose name
            if (selectedPoseIndex >= 0 && selectedPoseIndex < availablePoses.Count)
            {
                config.savedPoseName = availablePoses[selectedPoseIndex];
            }
            else
            {
                config.savedPoseName = "Feet Focus";
            }

            // 3. Get camera settings
            if (companionCamera != null)
            {
                config.camOrbitYaw = companionCamera.OrbitYaw;
                config.camOrbitPitch = companionCamera.OrbitPitch;
                config.camZoomOffset = companionCamera.ZoomOffset;

                Vector3 offset = companionCamera.SceneOffset;
                config.camSceneOffsetX = offset.x;
                config.camSceneOffsetY = offset.y;
                config.camSceneOffsetZ = offset.z;

                config.camPosX = companionCamera.transform.position.x;
                config.camPosY = companionCamera.transform.position.y;
                config.camPosZ = companionCamera.transform.position.z;

                config.camRotX = companionCamera.transform.rotation.x;
                config.camRotY = companionCamera.transform.rotation.y;
                config.camRotZ = companionCamera.transform.rotation.z;
                config.camRotW = companionCamera.transform.rotation.w;

                if (companionCamera.TryGetPlayerTransform(
                        out Vector3 playerPosition,
                        out Quaternion playerRotation))
                {
                    config.hasPlayerTransform = true;
                    config.playerPosX = playerPosition.x;
                    config.playerPosY = playerPosition.y;
                    config.playerPosZ = playerPosition.z;
                    config.playerRotX = playerRotation.x;
                    config.playerRotY = playerRotation.y;
                    config.playerRotZ = playerRotation.z;
                    config.playerRotW = playerRotation.w;
                }
            }

            config.hasSavedFrame = true;
            config.Save();

            if (showConfirmation)
            {
                string savedPoseDisplay =
                    companionUI != null
                        ? companionUI.GetPoseDisplayName(
                            config.savedPoseName)
                        : config.savedPoseName;
                companionUI.AppendLog(
                    "System",
                    $"saved current frame ({savedPoseDisplay}) " +
                    "as the default layout.");
            }
            Debug.Log(
                showConfirmation
                    ? $"[CompanionController] Saved current frame layout. " +
                      $"Pose: {config.savedPoseName}"
                    : $"[CompanionController] Autosaved. " +
                      $"Pose: {config.savedPoseName}");
        }

        private IEnumerator AutosaveRoutine()
        {
            WaitForSecondsRealtime wait =
                new WaitForSecondsRealtime(
                    AutosaveIntervalSeconds);
            while (true)
            {
                yield return wait;
                SaveCurrentFrameInternal(false);
            }
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                SaveCurrentFrameInternal(false);
            }
        }

        private void OnApplicationQuit()
        {
            SaveCurrentFrameInternal(false);
        }

        public void LoadSavedFrame()
        {
            ServiceConfig config = ServiceConfig.Load();
            if (config == null || !config.hasSavedFrame)
            {
                companionUI.AppendLog(
                    "System",
                    "No saved state yet. Arrange JARVIS, then tap Save State.");
                return;
            }

            int savedPoseIndex = GetPoseIndexByName(config.savedPoseName);
            if (savedPoseIndex >= 0)
            {
                selectedPoseIndex = savedPoseIndex;
                vrmService?.SetPose(selectedPoseIndex);
                companionUI.SetSelectedPoseInDropdown(selectedPoseIndex);
            }

            GameObject avatar = GameObject.Find("CompanionAvatarVRM");
            if (avatar == null)
            {
                avatar = GameObject.Find("CompanionAvatarProcedural");
            }
            if (avatar != null)
            {
                avatar.transform.SetPositionAndRotation(
                    new Vector3(
                        config.avatarPosX,
                        config.avatarPosY,
                        config.avatarPosZ),
                    new Quaternion(
                        config.avatarRotX,
                        config.avatarRotY,
                        config.avatarRotZ,
                        config.avatarRotW));
            }

            if (companionCamera == null && Camera.main != null)
            {
                companionCamera =
                    Camera.main.GetComponent<CompanionCamera>();
            }
            companionCamera?.ApplySavedState(config);
            if (config.hasPlayerTransform)
            {
                companionCamera?.ApplyPlayerTransform(
                    new Vector3(
                        config.playerPosX,
                        config.playerPosY,
                        config.playerPosZ),
                    new Quaternion(
                        config.playerRotX,
                        config.playerRotY,
                        config.playerRotZ,
                        config.playerRotW));
            }

            companionUI.AppendLog(
                "System",
                $"loaded saved state " +
                $"({companionUI.GetPoseDisplayName(config.savedPoseName)}).");
            Debug.Log(
                $"[CompanionController] Loaded saved frame layout. " +
                $"Pose: {config.savedPoseName}");
        }

        private void OnDestroy()
        {
            if (autosaveCoroutine != null)
            {
                StopCoroutine(autosaveCoroutine);
                autosaveCoroutine = null;
            }

            if (companionUI != null)
            {
                companionUI.OnSendMessageRequested -= HandleUserMessage;
                companionUI.OnImportVrmRequested -= HandleImportVRM;
                companionUI.OnWebcamToggleChanged -= HandleWebcamToggle;
                companionUI.OnScreenToggleChanged -= HandleScreenToggle;
                companionUI.OnAnimationToggleChanged -= HandleAnimationToggle;
                companionUI.OnMicToggleChanged -= HandleMicToggle;
                companionUI.OnExploreModeToggleChanged -=
                    HandleExploreModeToggle;
                companionUI.OnCyclePoseRequested -= HandleCyclePose;
                companionUI.OnPoseSelected -= HandlePoseSelected;
                companionUI.OnZoomInSliderChanged -= HandleZoomInSliderChanged;
                companionUI.OnZoomOutSliderChanged -= HandleZoomOutSliderChanged;
                companionUI.OnSaveFrameRequested -= SaveCurrentFrame;
                companionUI.OnLoadFrameRequested -= LoadSavedFrame;
            }
        }
    }
}
