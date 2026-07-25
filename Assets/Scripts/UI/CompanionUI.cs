using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using AICompanion.Core;

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

        public bool IsMicEnabled => micToggle != null && micToggle.isOn;

        private ServiceConfig currentConfig;

        private void Start()
        {
            // Load configuration
            currentConfig = ServiceConfig.Load();
            PopulateSettingsUI();
            ResolvePoseControls();

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

        private Button CreateRuntimePoseButton()
        {
            Transform parent = importVrmButton != null ? importVrmButton.transform.parent : transform;
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
                poseRect.anchorMin = new Vector2(0.5f, 0.5f);
                poseRect.anchorMax = new Vector2(0.5f, 0.5f);
                poseRect.pivot = new Vector2(0.5f, 0.5f);
                poseRect.sizeDelta = new Vector2(220f, 40f);
                poseRect.anchoredPosition = new Vector2(0f, -260f);
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

            return poseButton;
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
        }

        private void HandleCloseSettings()
        {
            currentConfig.llmBaseUrl = llmBaseUrlInput.text;
            currentConfig.llmApiKey = llmApiKeyInput.text;
            currentConfig.llmModelName = llmModelNameInput.text;
            currentConfig.elevenLabsApiKey = elevenLabsApiKeyInput.text;
            currentConfig.elevenLabsVoiceId = elevenLabsVoiceIdInput.text;
            currentConfig.isLocalMode = localModeToggle.isOn;
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
            chatLogText.text += $"\n<b><color={color}>{sender}:</color></b> {message}";
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
