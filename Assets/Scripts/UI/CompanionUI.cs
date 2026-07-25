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

            // Setup listeners
            sendButton.onClick.AddListener(HandleSend);
            messageInputField.onSubmit.AddListener(delegate { HandleSend(); });
            openSettingsButton.onClick.AddListener(() => settingsPanel.SetActive(true));
            closeSettingsButton.onClick.AddListener(HandleCloseSettings);
            importVrmButton.onClick.AddListener(HandleImportVRM);
            cyclePoseButton.onClick.AddListener(() => OnCyclePoseRequested?.Invoke());
            if (poseDropdown != null)
            {
                poseDropdown.onValueChanged.AddListener((val) => OnPoseSelected?.Invoke(val));
            }

            webcamToggle.onValueChanged.AddListener((val) => OnWebcamToggleChanged?.Invoke(val));
            screenToggle.onValueChanged.AddListener((val) => OnScreenToggleChanged?.Invoke(val));
            if (animationToggle != null)
            {
                animationToggle.onValueChanged.AddListener((val) => OnAnimationToggleChanged?.Invoke(val));
            }
            if (micToggle != null)
            {
                micToggle.onValueChanged.AddListener((val) => OnMicToggleChanged?.Invoke(val));
            }

            settingsPanel.SetActive(false);
            AppendLog("System", "Companion Initialized. Type a message or import a VRM to start!");
            UpdateConnectionStatus();
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
            llmBaseUrlInput.text = currentConfig.llmBaseUrl;
            llmApiKeyInput.text = currentConfig.llmApiKey;
            llmModelNameInput.text = currentConfig.llmModelName;
            elevenLabsApiKeyInput.text = currentConfig.elevenLabsApiKey;
            elevenLabsVoiceIdInput.text = currentConfig.elevenLabsVoiceId;
            localModeToggle.isOn = currentConfig.isLocalMode;
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
