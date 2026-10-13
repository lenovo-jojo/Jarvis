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
        private sealed class DialogueTurn
        {
            public string speaker;
            public string text;
        }

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
        private Coroutine worldPulseCoroutine;
        private int requestGeneration;
        private CompanionCamera companionCamera;
        private bool startupGreetingStarted;
        private readonly List<string>
            activeSpeakingCharacters =
                new List<string>();
        private const float AutosaveIntervalSeconds = 60f;
        private const float WorldPulseIntervalSeconds = 120f;

        private void Awake()
        {
            // World-space speech bubbles and outline passes can let the Game
            // view render far beyond the display refresh rate. Use a hard cap
            // rather than v-sync so ProMotion/120 Hz displays are capped too.
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 30;

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
            audioSource.loop = false;
            audioSource.spatialBlend = 0f; // 2D flat stereo playback for maximum clarity
            audioSource.dopplerLevel = 0f;
            audioSource.bypassEffects = true;
            audioSource.bypassListenerEffects = true;
            audioSource.bypassReverbZones = true;
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
            companionUI.OnSpeechBubbleToggleChanged +=
                HandleSpeechBubbleToggle;
            companionUI.OnCyclePoseRequested += HandleCyclePose;
            companionUI.OnPoseSelected += HandlePoseSelected;
            companionUI.OnZoomInSliderChanged += HandleZoomInSliderChanged;
            companionUI.OnZoomOutSliderChanged += HandleZoomOutSliderChanged;
            companionUI.OnSaveFrameRequested += SaveCurrentFrame;
            companionUI.OnLoadFrameRequested += LoadSavedFrame;
            companionUI.OnSatellitePhoneOpened +=
                RefreshSatellitePhone;
            companionUI.OnSatelliteCalled +=
                HandleSatelliteCalled;
            companionUI.OnWhistleRequested +=
                HandleWhistleRequested;
            companionCamera = Camera.main != null ? Camera.main.GetComponent<CompanionCamera>() : null;
            LoadPlayerAvatar();
            vrmService?.SetSpeechBubblesVisible(true);
            RefreshSatellitePhone();

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
                selectedPoseIndex =
                    GetPoseIndexByName(
                        "feets to the side sit");

                if (selectedPoseIndex != -1)
                {
                    companionUI.SetSelectedPoseInDropdown(selectedPoseIndex);
                }

                if (availablePoses.Count == 0)
                {
                    Debug.LogWarning(
                        "[CompanionController] No character poses were found.");
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
            worldPulseCoroutine =
                StartCoroutine(WorldPulseRoutine());
        }

        private void HandleCyclePose()
        {
            if (vrmService == null)
            {
                return;
            }

            if (availablePoses.Count == 0)
            {
                Debug.LogWarning(
                    "[CompanionController] No poses are available.");
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
                Debug.LogWarning(
                    "[CompanionController] No poses are available.");
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

        private void HandleSpeechBubbleToggle(
            bool enabled)
        {
            vrmService?.SetSpeechBubblesVisible(
                enabled);
        }

        private void RefreshSatellitePhone()
        {
            companionUI?.PopulateSatellitePhone(
                vrmService?.GetAvailableSatellites());
        }

        private void HandleSatelliteCalled(
            string satelliteId)
        {
            if (vrmService != null &&
                vrmService.CallSatellite(
                    satelliteId))
            {
                companionUI.AppendLog(
                    "System",
                    $"{satelliteId} joined Stella.");
                return;
            }

            companionUI.AppendLog(
                "System",
                $"{satelliteId} is not available yet.");
        }

        private void HandleWhistleRequested()
        {
            vrmService?.WhistleForCamera();
        }

        private void Update()
        {
            UnityEngine.InputSystem.Keyboard keyboard =
                UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null &&
                (companionUI == null ||
                 !companionUI.IsInputFocused) &&
                keyboard.zKey.wasPressedThisFrame &&
                (keyboard.leftCommandKey.isPressed ||
                 keyboard.rightCommandKey.isPressed))
            {
                vrmService?.UndoLastPlacement();
            }

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
            vrmService?.SetCameraAttentionLocked(false);
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
                ReleaseSpeechClip();
                isSpeaking = false;
                vrmService.SetSpeaking(false);
                HideActiveCharacterSpeech();
                vrmService.SetExpression("neutral", 1.0f);
                RestoreMicrophoneAfterSpeech();
            }

            companionUI.AppendLog("You", message);

            CompanionMemoryService.Instance
                .SetConversationContext(
                    vrmService
                        ?.GetPrimaryConversationCharacter(),
                    vrmService
                        ?.GetConversationCharactersInFrame());

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
            int currentRequestGeneration,
            bool releaseCameraLockWhenDone = false)
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
            List<DialogueTurn> turns =
                ParseDialogueTurns(
                    text,
                    GetChatSpeakerLabel(
                        vrmService
                            ?.GetPrimaryConversationCharacter()));
            if (speechLifecycleCoroutine != null)
            {
                StopCoroutine(speechLifecycleCoroutine);
            }
            speechLifecycleCoroutine =
                StartCoroutine(
                    SpeakDialogueSequence(
                        turns,
                        currentRequestGeneration,
                        releaseCameraLockWhenDone));
        }

        private IEnumerator SpeakDialogueSequence(
            List<DialogueTurn> turns,
            int currentRequestGeneration,
            bool releaseCameraLockWhenDone)
        {
            foreach (DialogueTurn turn in turns)
            {
                if (currentRequestGeneration !=
                    requestGeneration)
                {
                    break;
                }

                companionUI.AppendLog(
                    turn.speaker,
                    turn.text);

                AudioClip generatedClip = null;
                string generationError = null;
                voiceGenerationCoroutine =
                    StartCoroutine(
                        voiceService.GenerateSpeechCoroutine(
                            turn.speaker,
                            turn.text,
                            onComplete: clip =>
                                generatedClip = clip,
                            onError: error =>
                                generationError = error));
                yield return voiceGenerationCoroutine;
                voiceGenerationCoroutine = null;

                if (currentRequestGeneration !=
                    requestGeneration)
                {
                    if (generatedClip != null)
                    {
                        Destroy(generatedClip);
                    }
                    break;
                }

                if (generatedClip == null)
                {
                    companionUI.AppendLog(
                        "System",
                        "Speech Generation Failed: " +
                        generationError);
                    continue;
                }

                activeSpeakingCharacters.Clear();
                activeSpeakingCharacters.Add(
                    turn.speaker);
                vrmService.SetCharacterSpeech(
                    turn.speaker,
                    turn.text,
                    true);
                sttService?.SetListeningEnabled(false);
                vrmService.SetSpeaking(true);
                audioSource.clip = generatedClip;
                audioSource.Play();
                isSpeaking = true;

                while (audioSource != null &&
                       audioSource.isPlaying &&
                       currentRequestGeneration ==
                           requestGeneration)
                {
                    yield return null;
                }

                if (audioSource != null)
                {
                    audioSource.Stop();
                }
                isSpeaking = false;
                vrmService.SetSpeaking(false);
                HideActiveCharacterSpeech();
                ReleaseSpeechClip();
                RestoreMicrophoneAfterSpeech();

                if (currentRequestGeneration ==
                    requestGeneration)
                {
                    yield return new WaitForSecondsRealtime(
                        0.12f);
                }
            }

            isSpeaking = false;
            vrmService.SetSpeaking(false);
            vrmService.SetExpression("neutral", 1.0f);
            HideActiveCharacterSpeech();
            ReleaseSpeechClip();
            RestoreMicrophoneAfterSpeech();
            if (releaseCameraLockWhenDone)
            {
                vrmService?.SetCameraAttentionLocked(
                    false);
            }
            speechLifecycleCoroutine = null;
        }

        private void HideActiveCharacterSpeech()
        {
            foreach (string speaker in
                activeSpeakingCharacters)
            {
                vrmService?.SetCharacterSpeech(
                    speaker,
                    string.Empty,
                    false);
            }
            activeSpeakingCharacters.Clear();
        }

        private static List<DialogueTurn>
            ParseDialogueTurns(
                string response,
                string fallbackSpeaker)
        {
            List<DialogueTurn> turns =
                new List<DialogueTurn>();
            string[] lines =
                (response ?? string.Empty).Split(
                    new[] { '\n' },
                    StringSplitOptions
                        .RemoveEmptyEntries);
            foreach (string rawLine in lines)
            {
                string line = rawLine.Trim();
                int separator =
                    line.IndexOf(':');
                if (separator <= 0)
                {
                    continue;
                }
                string speaker =
                    line.Substring(
                            0,
                            separator)
                        .Trim()
                        .Trim(
                            '[', ']',
                            '*', '#',
                            ' ');
                string dialogue =
                    line.Substring(
                            separator + 1)
                        .Trim();
                if (speaker.Length == 0 ||
                    dialogue.Length == 0)
                {
                    continue;
                }
                turns.Add(
                    new DialogueTurn
                    {
                        speaker = speaker,
                        text = dialogue
                    });
            }

            if (turns.Count == 0)
            {
                turns.Add(
                    new DialogueTurn
                    {
                        speaker =
                            string.IsNullOrWhiteSpace(
                                fallbackSpeaker)
                                ? "charmy"
                                : fallbackSpeaker,
                        text =
                            string.IsNullOrWhiteSpace(
                                response)
                                ? "..."
                                : response.Trim()
                    });
            }
            return turns;
        }

        private static string GetChatSpeakerLabel(
            string characterId)
        {
            if (string.IsNullOrWhiteSpace(
                    characterId))
            {
                return "charmy";
            }
            return characterId
                .Trim()
                .ToLowerInvariant()
                .Replace("_jarvis", string.Empty)
                .Replace(" jarvis", string.Empty);
        }

        private void RestoreMicrophoneAfterSpeech()
        {
            sttService?.SetListeningEnabled(
                companionUI != null &&
                companionUI.IsMicEnabled);
        }

        private void ReleaseSpeechClip()
        {
            if (audioSource == null ||
                audioSource.clip == null)
            {
                return;
            }

            AudioClip finishedClip =
                audioSource.clip;
            audioSource.clip = null;
            Destroy(finishedClip);
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
                    LoadSpikeSatellite();
                    Debug.Log(
                        "[CompanionController] Jojo4 self-insert " +
                        "loaded for POV Mode.");
                },
                onError: error =>
                {
                    companionUI?.AppendLog(
                        "System",
                        $"Player avatar load error: {error}");
                });
        }

        private void LoadSpikeSatellite()
        {
            string spikePath =
                Path.Combine(
                    Application.dataPath,
                    "Characters",
                    "spike.vrm");
            if (!File.Exists(spikePath))
            {
                Debug.LogWarning(
                    "[CompanionController] Spike VRM not found: " +
                    spikePath);
                BeginStartupGreeting();
                return;
            }

            vrmService.LoadWorldVRM(
                "spike",
                spikePath,
                new Vector3(0f, 0.02f, 2f),
                Quaternion.identity,
                onLoaded: spike =>
                {
                    RefreshSatellitePhone();
                    BeginStartupGreeting();
                    Debug.Log(
                        "[CompanionController] Spike satellite loaded.");
                },
                onError: error =>
                {
                    companionUI?.AppendLog(
                        "System",
                        "Spike avatar load error: " +
                        error);
                    BeginStartupGreeting();
                });
        }

        private void BeginStartupGreeting()
        {
            if (startupGreetingStarted)
            {
                return;
            }
            startupGreetingStarted = true;
            vrmService?.SetCameraAttentionLocked(
                true);
            vrmService?.WhistleForCamera();
            StartCoroutine(
                StartupGreetingRoutine());
        }

        private IEnumerator StartupGreetingRoutine()
        {
            // Let the imported poses settle onto the blanket before the
            // characters begin their first conversational turn.
            yield return new WaitForSecondsRealtime(
                0.75f);

            List<string> startupCast =
                new List<string>
                {
                    "charmy",
                    "jojo"
                };
            List<string> satellites =
                vrmService?.GetAvailableSatellites();
            if (satellites != null)
            {
                foreach (string satellite in
                    satellites)
                {
                    if (!startupCast.Exists(
                            character =>
                                character.Equals(
                                    satellite,
                                    StringComparison
                                        .OrdinalIgnoreCase)))
                    {
                        startupCast.Add(
                            satellite);
                    }
                }
            }
            if (!startupCast.Exists(
                    character =>
                        character.Equals(
                            "spike",
                            StringComparison
                                .OrdinalIgnoreCase)))
            {
                startupCast.Add("spike");
            }

            CompanionMemoryService.Instance
                .SetConversationContext(
                    null,
                    startupCast);

            int greetingGeneration =
                ++requestGeneration;
            const string greetingDirection =
                "startup scene direction: charmy, jojo, and spike " +
                "must each greet me in their own voice. after greeting " +
                "me, let them exchange one brief, natural reaction with " +
                "each other. output only their labeled spoken turns.";
            yield return llmService.SendDirectiveCoroutine(
                greetingDirection,
                onComplete: aiText =>
                {
                    if (greetingGeneration !=
                        requestGeneration)
                    {
                        return;
                    }
                    TriggerExpressionFromText(aiText);
                    SpeakText(
                        aiText,
                        greetingGeneration,
                        true);
                },
                onError: error =>
                {
                    if (greetingGeneration ==
                        requestGeneration)
                    {
                        companionUI?.AppendLog(
                            "System",
                            "Startup greeting failed: " +
                            error);
                        vrmService
                            ?.SetCameraAttentionLocked(
                                false);
                    }
                });
        }

        private IEnumerator WorldPulseRoutine()
        {
            WaitForSecondsRealtime interval =
                new WaitForSecondsRealtime(
                    WorldPulseIntervalSeconds);
            while (true)
            {
                yield return interval;
                if (llmService == null ||
                    vrmService == null ||
                    isSpeaking ||
                    voiceGenerationCoroutine != null ||
                    speechLifecycleCoroutine != null ||
                    thinkingReactionCoroutine != null)
                {
                    continue;
                }

                List<string> cast =
                    new List<string>
                    {
                        "charmy",
                        "jojo"
                    };
                List<string> satellites =
                    vrmService.GetAvailableSatellites();
                if (satellites != null)
                {
                    cast.AddRange(satellites);
                }
                CompanionMemoryService.Instance
                    .SetConversationContext(
                        null,
                        cast);

                string state =
                    vrmService.GetWorldStateSummary();
                string allowedPoses =
                    string.Join(
                        ", ",
                        availablePoses);
                string directive =
                    "silent world pulse. use this compact scene reference:\n" +
                    state +
                    "\navailable exact pose names: " +
                    allowedPoses +
                    "\nfor charmy, jojo, and spike, return one line each " +
                    "as name: pose=<exact pose>; " +
                    "place=<where they want to go or stay>; " +
                    "friend=<character or none>; wish=<short item or " +
                    "addition they want>; idea=<short next idea>. " +
                    "choose independently according to each constitution. " +
                    "do not greet me and do not add narration.";
                int pulseGeneration =
                    requestGeneration;
                string pulseResponse = null;
                yield return llmService
                    .SendDirectiveCoroutine(
                        directive,
                        onComplete: response =>
                            pulseResponse = response,
                        onError: error =>
                            Debug.LogWarning(
                                "[WorldPulse] " + error));

                if (pulseGeneration !=
                        requestGeneration ||
                    string.IsNullOrWhiteSpace(
                        pulseResponse))
                {
                    continue;
                }
                ApplyWorldPulseResponse(
                    pulseResponse);
            }
        }

        private void ApplyWorldPulseResponse(
            string response)
        {
            string[] lines =
                response.Split(
                    new[] { '\n' },
                    StringSplitOptions
                        .RemoveEmptyEntries);
            foreach (string rawLine in lines)
            {
                int speakerSeparator =
                    rawLine.IndexOf(':');
                if (speakerSeparator <= 0)
                {
                    continue;
                }
                string character =
                    rawLine.Substring(
                            0,
                            speakerSeparator)
                        .Trim()
                        .Trim(
                            '*', '#',
                            '[', ']');
                string[] choices =
                    rawLine.Substring(
                            speakerSeparator + 1)
                        .Split(';');
                foreach (string choice in choices)
                {
                    string trimmed =
                        choice.Trim();
                    if (!trimmed.StartsWith(
                            "pose=",
                            StringComparison
                                .OrdinalIgnoreCase))
                    {
                        continue;
                    }
                    string poseName =
                        trimmed.Substring(5)
                            .Trim()
                            .Trim(
                                '"', '\'',
                                '.', ' ');
                    if (character.Equals(
                            "jojo",
                            StringComparison
                                .OrdinalIgnoreCase) &&
                        companionCamera != null &&
                        companionCamera.ExploreMode)
                    {
                        // Stella remains under direct player control in POV
                        // mode; her autonomous choice can wait for TV mode.
                        break;
                    }
                    vrmService
                        .SetCharacterPoseByName(
                            character,
                            poseName);
                    break;
                }
            }
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
                companionUI.AppendLog(
                    "System",
                    "saved current frame as the default layout.");
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
                "loaded saved state.");
            Debug.Log(
                $"[CompanionController] Loaded saved frame layout. " +
                $"Pose: {config.savedPoseName}");
        }

        private void OnDestroy()
        {
            if (audioSource != null)
            {
                audioSource.Stop();
                ReleaseSpeechClip();
            }
            if (autosaveCoroutine != null)
            {
                StopCoroutine(autosaveCoroutine);
                autosaveCoroutine = null;
            }
            if (worldPulseCoroutine != null)
            {
                StopCoroutine(worldPulseCoroutine);
                worldPulseCoroutine = null;
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
                companionUI.OnSpeechBubbleToggleChanged -=
                    HandleSpeechBubbleToggle;
                companionUI.OnCyclePoseRequested -= HandleCyclePose;
                companionUI.OnPoseSelected -= HandlePoseSelected;
                companionUI.OnZoomInSliderChanged -= HandleZoomInSliderChanged;
                companionUI.OnZoomOutSliderChanged -= HandleZoomOutSliderChanged;
                companionUI.OnSaveFrameRequested -= SaveCurrentFrame;
                companionUI.OnLoadFrameRequested -= LoadSavedFrame;
                companionUI.OnSatellitePhoneOpened -=
                    RefreshSatellitePhone;
                companionUI.OnSatelliteCalled -=
                    HandleSatelliteCalled;
                companionUI.OnWhistleRequested -=
                    HandleWhistleRequested;
            }
        }
    }
}
