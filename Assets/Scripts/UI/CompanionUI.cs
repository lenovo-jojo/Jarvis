using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using AICompanion.Core;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace AICompanion.UI
{
    public class CompanionUI : MonoBehaviour
    {
        [Header("Chat Panel")]
        [SerializeField] private TMP_Text chatLogText;
        [SerializeField] private TMP_InputField messageInputField;
        [SerializeField] private Button sendButton;

        [Header("Settings Controls")]
        [SerializeField] private Button openSettingsButton;
        [SerializeField] private Button closeSettingsButton;
        [SerializeField] private GameObject settingsPanel;
        [SerializeField] private TMP_InputField llmBaseUrlInput;
        [SerializeField] private TMP_InputField llmApiKeyInput;
        [SerializeField] private TMP_InputField llmModelNameInput;
        [SerializeField] private TMP_InputField elevenLabsApiKeyInput;
        [SerializeField] private TMP_InputField elevenLabsVoiceIdInput;
        [SerializeField] private Toggle localModeToggle;

        [Header("Vision & Context")]
        [SerializeField] private Toggle webcamToggle;
        [SerializeField] private Toggle screenToggle;
        [SerializeField] private Toggle animationToggle;
        [SerializeField] private Toggle micToggle;
        [SerializeField] private Button cyclePoseButton;
        [SerializeField] private TMPro.TMP_Dropdown poseDropdown;
        [SerializeField] private RawImage webcamPreviewImage;
        [SerializeField] private RawImage screenPreviewImage;
        [SerializeField] private Button importVrmButton;
        [SerializeField] private Slider zoomInSlider;
        [SerializeField] private Slider zoomOutSlider;
        [SerializeField] private TMP_Dropdown voiceModelDropdown;

        [Header("Status")]
        [SerializeField] private TMP_Text connectionStatusText;

        public event Action<string> OnSendMessageRequested;
        public event Action<string> OnImportVrmRequested;
        public event Action<bool> OnWebcamToggleChanged;
        public event Action<bool> OnScreenToggleChanged;
        public event Action<bool> OnAnimationToggleChanged;
        public event Action<bool> OnMicToggleChanged;
        public event Action OnCyclePoseRequested;
        public event Action<int> OnPoseSelected;
        public event Action<float> OnZoomInSliderChanged;
        public event Action<float> OnZoomOutSliderChanged;

        public bool IsMicEnabled => micToggle != null && micToggle.isOn;
        public bool IsInputFocused => messageInputField != null && messageInputField.isFocused;

        private ServiceConfig currentConfig;
        private Button runtimePoseButton;
        private static readonly Rect PoseFallbackRect = new Rect(16f, 16f, 180f, 42f);
        private readonly List<string> chatLines = new List<string>();
        private Coroutine aiTypingCoroutine;
        private int activeAiLineIndex = -1;
        private static readonly List<string> VoiceModels = new List<string> { "eleven_multilingual_v2", "animalesse.js" };

        private void Start()
        {
            // Load configuration
            currentConfig = ServiceConfig.Load();
            ResolvePoseControls();
            ResolveExtraControls();
            PopulateSettingsUI();

            // Setup listeners
            if (sendButton != null)
                sendButton.onClick.AddListener(HandleSend);
            if (messageInputField != null)
                messageInputField.onSubmit.AddListener(delegate { HandleSend(); });
            if (openSettingsButton != null)
                openSettingsButton.onClick.AddListener(() => settingsPanel.SetActive(true));
            if (closeSettingsButton != null)
                closeSettingsButton.onClick.AddListener(HandleCloseSettings);
            if (importVrmButton != null)
                importVrmButton.onClick.AddListener(HandleImportVRM);
            if (cyclePoseButton != null)
                cyclePoseButton.onClick.AddListener(() => OnCyclePoseRequested?.Invoke());
            if (poseDropdown != null)
            {
                poseDropdown.onValueChanged.AddListener((val) => OnPoseSelected?.Invoke(val));
            }

            if (webcamToggle != null)
                webcamToggle.onValueChanged.AddListener((val) => OnWebcamToggleChanged?.Invoke(val));
            if (screenToggle != null)
                screenToggle.onValueChanged.AddListener((val) => OnScreenToggleChanged?.Invoke(val));
            if (animationToggle != null)
            {
                animationToggle.onValueChanged.AddListener((val) => OnAnimationToggleChanged?.Invoke(val));
            }
            if (micToggle != null)
            {
                micToggle.onValueChanged.AddListener((val) => OnMicToggleChanged?.Invoke(val));
            }
            if (zoomInSlider != null)
            {
                zoomInSlider.onValueChanged.AddListener((val) => OnZoomInSliderChanged?.Invoke(val));
            }
            if (zoomOutSlider != null)
            {
                zoomOutSlider.onValueChanged.AddListener((val) => OnZoomOutSliderChanged?.Invoke(val));
            }
            if (voiceModelDropdown != null)
            {
                voiceModelDropdown.onValueChanged.AddListener((idx) =>
                {
                    if (idx >= 0 && idx < VoiceModels.Count)
                    {
                        currentConfig.elevenLabsModelId = VoiceModels[idx];
                    }
                });
            }

            if (settingsPanel != null)
                settingsPanel.SetActive(false);
            AppendLog("System", "Companion Initialized. Type a message or import a VRM to start!");
            UpdateConnectionStatus();
        }

        private void ResolvePoseControls()
        {
            if (cyclePoseButton == null)
            {
                Button[] buttons = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (Button button in buttons)
                {
                    if (button != null && button.name.IndexOf("pose", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        cyclePoseButton = button;
                        break;
                    }
                }
            }

            if (poseDropdown == null)
            {
                TMP_Dropdown[] dropdowns = UnityEngine.Object.FindObjectsByType<TMP_Dropdown>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (TMP_Dropdown dropdown in dropdowns)
                {
                    if (dropdown != null && dropdown.name.IndexOf("pose", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        poseDropdown = dropdown;
                        break;
                    }
                }
            }

            if (cyclePoseButton == null)
            {
                cyclePoseButton = CreateRuntimePoseButton();
            }
        }

        private void ResolveExtraControls()
        {
            if (zoomInSlider == null || zoomOutSlider == null || voiceModelDropdown == null)
            {
                var sliders = UnityEngine.Object.FindObjectsByType<Slider>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (var slider in sliders)
                {
                    if (slider == null)
                    {
                        continue;
                    }

                    string lowerName = slider.name.ToLowerInvariant();
                    if (zoomInSlider == null && lowerName.Contains("zoomin"))
                    {
                        zoomInSlider = slider;
                    }
                    else if (zoomOutSlider == null && lowerName.Contains("zoomout"))
                    {
                        zoomOutSlider = slider;
                    }
                }

                var dropdowns = UnityEngine.Object.FindObjectsByType<TMP_Dropdown>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (var dropdown in dropdowns)
                {
                    if (dropdown != null && voiceModelDropdown == null && dropdown.name.IndexOf("voicemodel", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        voiceModelDropdown = dropdown;
                    }
                }
            }

            if (zoomInSlider == null)
            {
                zoomInSlider = CreateRuntimeZoomSlider("ZoomInSlider", "Zoom +", new Vector2(-240f, -88f));
            }
            if (zoomOutSlider == null)
            {
                zoomOutSlider = CreateRuntimeZoomSlider("ZoomOutSlider", "Zoom -", new Vector2(-240f, -126f));
            }
            if (voiceModelDropdown == null)
            {
                voiceModelDropdown = CreateRuntimeVoiceModelDropdown();
            }
        }

        private Transform ResolveUiParent()
        {
            if (settingsPanel != null && settingsPanel.transform != null)
            {
                return settingsPanel.transform;
            }
            return transform;
        }

        private Slider CreateRuntimeZoomSlider(string objectName, string labelText, Vector2 anchoredPosition)
        {
            Transform parent = ResolveUiParent();

            GameObject root = new GameObject(objectName, typeof(RectTransform));
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.SetParent(parent, false);
            rootRect.anchorMin = new Vector2(1f, 0f);
            rootRect.anchorMax = new Vector2(1f, 0f);
            rootRect.pivot = new Vector2(1f, 0f);
            rootRect.sizeDelta = new Vector2(200f, 28f);
            rootRect.anchoredPosition = anchoredPosition;

            GameObject bg = new GameObject("Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform bgRect = bg.GetComponent<RectTransform>();
            bgRect.SetParent(rootRect, false);
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            bg.GetComponent<Image>().color = new Color(0.12f, 0.1f, 0.2f, 0.9f);

            GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
            RectTransform fillAreaRect = fillArea.GetComponent<RectTransform>();
            fillAreaRect.SetParent(rootRect, false);
            fillAreaRect.anchorMin = new Vector2(0f, 0f);
            fillAreaRect.anchorMax = new Vector2(1f, 1f);
            fillAreaRect.offsetMin = new Vector2(8f, 8f);
            fillAreaRect.offsetMax = new Vector2(-8f, -8f);

            GameObject fill = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform fillRect = fill.GetComponent<RectTransform>();
            fillRect.SetParent(fillAreaRect, false);
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(1f, 1f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            fill.GetComponent<Image>().color = new Color(0.95f, 0.69f, 0.25f, 1f);

            GameObject handle = new GameObject("Handle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform handleRect = handle.GetComponent<RectTransform>();
            handleRect.SetParent(rootRect, false);
            handleRect.sizeDelta = new Vector2(14f, 24f);
            handle.GetComponent<Image>().color = Color.white;

            Slider slider = root.AddComponent<Slider>();
            slider.targetGraphic = handle.GetComponent<Image>();
            slider.fillRect = fillRect;
            slider.handleRect = handleRect;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.SetValueWithoutNotify(0f);

            GameObject labelObj = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            RectTransform labelRect = labelObj.GetComponent<RectTransform>();
            labelRect.SetParent(rootRect, false);
            labelRect.anchorMin = new Vector2(0f, 1f);
            labelRect.anchorMax = new Vector2(0f, 1f);
            labelRect.pivot = new Vector2(0f, 0f);
            labelRect.anchoredPosition = new Vector2(0f, 6f);
            labelRect.sizeDelta = new Vector2(100f, 20f);
            TextMeshProUGUI label = labelObj.GetComponent<TextMeshProUGUI>();
            label.text = labelText;
            label.fontSize = 14f;
            label.color = new Color(1f, 0.92f, 0.72f, 1f);

            return slider;
        }

        private TMP_Dropdown CreateRuntimeVoiceModelDropdown()
        {
            Transform parent = ResolveUiParent();
            var resources = new TMP_DefaultControls.Resources();
            GameObject dropdownObject = TMP_DefaultControls.CreateDropdown(resources);
            dropdownObject.name = "VoiceModelDropdown";

            RectTransform rect = dropdownObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-240f, -164f);
            rect.sizeDelta = new Vector2(230f, 34f);

            return dropdownObject.GetComponent<TMP_Dropdown>();
        }

        private Button CreateRuntimePoseButton()
        {
            Transform parent = null;
            if (importVrmButton != null &&
                importVrmButton.transform != null &&
                importVrmButton.transform.parent != null &&
                importVrmButton.transform.parent.gameObject.activeInHierarchy)
            {
                parent = importVrmButton.transform.parent;
            }

            if (parent == null)
            {
                Canvas[] canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                foreach (Canvas canvas in canvases)
                {
                    if (canvas != null && canvas.isActiveAndEnabled)
                    {
                        parent = canvas.transform;
                        break;
                    }
                }
            }

            if (parent == null)
            {
                parent = transform;
            }

            if (parent == null)
            {
                return null;
            }

            GameObject poseButtonGo = new GameObject("RuntimePoseButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            RectTransform poseRect = poseButtonGo.GetComponent<RectTransform>();
            poseRect.SetParent(parent, false);

            if (importVrmButton != null)
            {
                RectTransform importRect = importVrmButton.GetComponent<RectTransform>();
                if (importRect != null)
                {
                    poseRect.anchorMin = importRect.anchorMin;
                    poseRect.anchorMax = importRect.anchorMax;
                    poseRect.pivot = importRect.pivot;
                    poseRect.sizeDelta = importRect.sizeDelta;
                    poseRect.anchoredPosition = importRect.anchoredPosition + new Vector2(0f, -50f);
                }
            }
            else
            {
                poseRect.anchorMin = new Vector2(0f, 0f);
                poseRect.anchorMax = new Vector2(0f, 0f);
                poseRect.pivot = new Vector2(0f, 0f);
                poseRect.sizeDelta = new Vector2(180f, 42f);
                poseRect.anchoredPosition = new Vector2(16f, 16f);
            }

            Image poseImage = poseButtonGo.GetComponent<Image>();
            Button poseButton = poseButtonGo.GetComponent<Button>();
            if (importVrmButton != null)
            {
                Image importImage = importVrmButton.GetComponent<Image>();
                if (importImage != null)
                {
                    poseImage.sprite = importImage.sprite;
                    poseImage.type = importImage.type;
                    poseImage.color = importImage.color;
                }

                poseButton.transition = importVrmButton.transition;
                poseButton.colors = importVrmButton.colors;
                poseButton.spriteState = importVrmButton.spriteState;
            }
            else
            {
                poseImage.color = new Color(0.2f, 0.6f, 0.9f, 0.9f);
            }

            GameObject labelGo = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            RectTransform labelRect = labelGo.GetComponent<RectTransform>();
            labelRect.SetParent(poseButtonGo.transform, false);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            TextMeshProUGUI label = labelGo.GetComponent<TextMeshProUGUI>();
            label.text = "Next Pose";
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = 22f;
            label.color = Color.white;

            if (importVrmButton != null)
            {
                TMP_Text importLabel = importVrmButton.GetComponentInChildren<TMP_Text>(true);
                if (importLabel != null)
                {
                    label.font = importLabel.font;
                    label.fontMaterial = importLabel.fontMaterial;
                    label.fontSize = importLabel.fontSize;
                    label.color = importLabel.color;
                }
            }

            runtimePoseButton = poseButton;
            return poseButton;
        }

        private void Update()
        {
            if (UnityEngine.InputSystem.Keyboard.current == null)
            {
                return;
            }

            if (!IsInputFocused && UnityEngine.InputSystem.Keyboard.current.pKey.wasPressedThisFrame)
            {
                OnCyclePoseRequested?.Invoke();
            }
        }

        private void OnGUI()
        {
            bool hasVisiblePoseControl =
                (poseDropdown != null && poseDropdown.gameObject.activeInHierarchy) ||
                (runtimePoseButton != null && runtimePoseButton.gameObject.activeInHierarchy);

            if (hasVisiblePoseControl || IsInputFocused)
            {
                return;
            }

            if (GUI.Button(PoseFallbackRect, "Next Pose"))
            {
                OnCyclePoseRequested?.Invoke();
            }
        }

        public void PopulatePoseDropdown(System.Collections.Generic.List<string> options)
        {
            if (poseDropdown != null)
            {
                poseDropdown.ClearOptions();
                poseDropdown.AddOptions(options);
            }
        }

        public void SetSelectedPoseInDropdown(int index)
        {
            if (poseDropdown != null)
            {
                poseDropdown.SetValueWithoutNotify(index);
            }
        }

        private void PopulateSettingsUI()
        {
            if (llmBaseUrlInput != null) llmBaseUrlInput.text = currentConfig.llmBaseUrl;
            if (llmApiKeyInput != null) llmApiKeyInput.text = currentConfig.llmApiKey;
            if (llmModelNameInput != null) llmModelNameInput.text = currentConfig.llmModelName;
            if (elevenLabsApiKeyInput != null) elevenLabsApiKeyInput.text = currentConfig.elevenLabsApiKey;
            if (elevenLabsVoiceIdInput != null) elevenLabsVoiceIdInput.text = currentConfig.elevenLabsVoiceId;
            if (localModeToggle != null) localModeToggle.isOn = currentConfig.isLocalMode;
            if (voiceModelDropdown != null)
            {
                voiceModelDropdown.ClearOptions();
                voiceModelDropdown.AddOptions(VoiceModels);
                int selected = Mathf.Max(0, VoiceModels.IndexOf(string.IsNullOrEmpty(currentConfig.elevenLabsModelId) ? "eleven_multilingual_v2" : currentConfig.elevenLabsModelId));
                voiceModelDropdown.SetValueWithoutNotify(selected);
            }
        }

        private void HandleCloseSettings()
        {
            currentConfig.llmBaseUrl = llmBaseUrlInput.text;
            currentConfig.llmApiKey = llmApiKeyInput.text;
            currentConfig.llmModelName = llmModelNameInput.text;
            currentConfig.elevenLabsApiKey = elevenLabsApiKeyInput.text;
            currentConfig.elevenLabsVoiceId = elevenLabsVoiceIdInput.text;
            currentConfig.isLocalMode = localModeToggle.isOn;
            if (voiceModelDropdown != null)
            {
                int idx = voiceModelDropdown.value;
                currentConfig.elevenLabsModelId = idx >= 0 && idx < VoiceModels.Count ? VoiceModels[idx] : "eleven_multilingual_v2";
            }
            currentConfig.Save();

            settingsPanel.SetActive(false);
            UpdateConnectionStatus();
            AppendLog("System", "Configuration Saved successfully.");
        }

        private void HandleSend()
        {
            string message = messageInputField.text;
            if (!string.IsNullOrEmpty(message))
            {
                messageInputField.text = "";
                OnSendMessageRequested?.Invoke(message);
            }
        }

        private void HandleImportVRM()
        {
            #if UNITY_EDITOR
            string path = UnityEditor.EditorUtility.OpenFilePanel("Load VRM Avatar", "", "vrm");
            if (!string.IsNullOrEmpty(path))
            {
                OnImportVrmRequested?.Invoke(path);
            }
            #else
            // In a runtime build, we'd use a file explorer or load from a custom input
            AppendLog("System", "Please input custom VRM path in Service Config (Desktop builds support OpenFilePanel).");
            #endif
        }

        public void AppendLog(string sender, string message)
        {
            string color = sender == "AI" ? "#FFC107" : (sender == "System" ? "#00BCD4" : "#4CAF50");
            chatLines.Add($"\n<mark=#161028CC><b><color={color}>{sender}:</color></b> {message}</mark>");
            RefreshChatLog();
        }

        public void StartAISpeechTyping(string fullMessage, float durationSeconds)
        {
            if (aiTypingCoroutine != null)
            {
                StopCoroutine(aiTypingCoroutine);
            }

            activeAiLineIndex = chatLines.Count;
            chatLines.Add("\n<mark=#161028CC><b><color=#FFC107>AI:</color></b> </mark>");
            RefreshChatLog();
            aiTypingCoroutine = StartCoroutine(TypeAIOverTime(fullMessage, Mathf.Max(0.6f, durationSeconds)));
        }

        public void CompleteAISpeechTyping(string finalMessage)
        {
            if (aiTypingCoroutine != null)
            {
                StopCoroutine(aiTypingCoroutine);
                aiTypingCoroutine = null;
            }

            if (activeAiLineIndex >= 0 && activeAiLineIndex < chatLines.Count)
            {
                chatLines[activeAiLineIndex] = $"\n<mark=#161028CC><b><color=#FFC107>AI:</color></b> {finalMessage}</mark>";
                activeAiLineIndex = -1;
                RefreshChatLog();
            }
        }

        private IEnumerator TypeAIOverTime(string fullMessage, float durationSeconds)
        {
            float startTime = Time.unscaledTime;
            int maxChars = fullMessage.Length;

            while (true)
            {
                float elapsed = Time.unscaledTime - startTime;
                float t = Mathf.Clamp01(elapsed / durationSeconds);
                int chars = Mathf.Clamp(Mathf.RoundToInt(maxChars * t), 0, maxChars);
                string typed = fullMessage.Substring(0, chars);

                if (activeAiLineIndex >= 0 && activeAiLineIndex < chatLines.Count)
                {
                    chatLines[activeAiLineIndex] = $"\n<mark=#161028CC><b><color=#FFC107>AI:</color></b> <mspace=0.55em>{typed}</mspace></mark>";
                    RefreshChatLog();
                }

                if (chars >= maxChars)
                {
                    break;
                }

                yield return null;
            }

            aiTypingCoroutine = null;
        }

        private void RefreshChatLog()
        {
            if (chatLogText == null)
            {
                return;
            }

            StringBuilder sb = new StringBuilder();
            foreach (string line in chatLines)
            {
                sb.Append(line);
            }
            chatLogText.text = sb.ToString();
        }

        public void SetWebcamTexture(Texture2D texture)
        {
            webcamPreviewImage.texture = texture;
            webcamPreviewImage.gameObject.SetActive(texture != null);
        }

        public void SetScreenTexture(Texture2D texture)
        {
            screenPreviewImage.texture = texture;
            screenPreviewImage.gameObject.SetActive(texture != null);
        }

        private void UpdateConnectionStatus()
        {
            if (connectionStatusText != null)
            {
                connectionStatusText.text = currentConfig.isLocalMode ? "Local Mode (Ollama)" : "Cloud Mode (Online API)";
            }
        }
    }
}
