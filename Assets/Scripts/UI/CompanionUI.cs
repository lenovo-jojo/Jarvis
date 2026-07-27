using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using AICompanion.Core;
using AICompanion.Services;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine.Events;
using UnityEngine.EventSystems;

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
        [SerializeField] private Toggle exploreModeToggle;
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
        public event Action<bool> OnExploreModeToggleChanged;
        public event Action OnCyclePoseRequested;
        public event Action OnSaveFrameRequested;
        public event Action OnLoadFrameRequested;
        public event Action<int> OnPoseSelected;
        public event Action<float> OnZoomInSliderChanged;
        public event Action<float> OnZoomOutSliderChanged;

        public bool IsMicEnabled => micToggle != null && micToggle.isOn;
        public bool IsInputFocused => messageInputField != null && messageInputField.isFocused;

        private ServiceConfig currentConfig;
        private Button runtimePoseButton;
        private Button saveStateButton;
        private Button loadStateButton;
        private Button topMenuToggleButton;
        private Button chatMenuToggleButton;
        private Button chatExpandButton;
        private Button chatClearButton;
        private RectTransform chatDragHandle;
        private RectTransform chatResizeHandle;
        private Button poseMenuButton;
        private Button poseRotationButton;
        private Button memorySettingsButton;
        private CanvasScaler responsiveCanvasScaler;
        private RectTransform safeAreaRoot;
        private RectTransform topControlStack;
        private RectTransform chatDock;
        private RectTransform quickActions;
        private RectTransform posePickerPanel;
        private RectTransform poseRowsRoot;
        private RectTransform poseSelectionPanel;
        private RectTransform poseSelectionRowsRoot;
        private RectTransform memorySettingsPanel;
        private TMP_InputField constitutionInput;
        private Toggle memoryEnabledToggle;
        private Toggle longTermMemoryToggle;
        private Toggle constitutionEnabledToggle;
        private TMP_Text memoryStatusLabel;
        private GridLayoutGroup quickActionsLayout;
        private GameObject chatViewport;
        private TMP_Text chatDockTitle;
        private bool isTopMenuCollapsed;
        private bool isChatDockCollapsed;
        private bool isChatDockExpanded;
        private bool isChatDockFloating;
        private int lastScreenWidth = -1;
        private int lastScreenHeight = -1;
        private static Sprite roundedPanelSprite;
        private static readonly Rect PoseFallbackRect = new Rect(16f, 16f, 180f, 42f);
        private readonly List<string> chatLines = new List<string>();
        private readonly List<string> poseInternalNames =
            new List<string>();
        private readonly List<string> poseDisplayNames =
            new List<string>();
        private readonly List<bool> poseRotationEnabled =
            new List<bool>();
        private readonly Dictionary<Toggle, UnityAction<bool>>
            toggleVisualListeners =
                new Dictionary<Toggle, UnityAction<bool>>();
        private Coroutine aiTypingCoroutine;
        private int activeAiLineIndex = -1;
        private const int MaxVisibleChatLines = 5;
        private const int MaxRetainedChatLines = 30;
        private static readonly List<string> VoiceModels = new List<string> { "eleven_multilingual_v2", "animalese.js" };
        private static readonly Dictionary<string, string>
            FriendlyPoseNames =
                new Dictionary<string, string>(
                    StringComparer.OrdinalIgnoreCase)
                {
                    { "Bent Over", "forward fold" },
                    { "Optional Pose B", "hands together" },
                    { "Optional Pose A", "arms overhead" },
                    { "Feet Focus", "cozy feet" },
                    { "Optional Pose", "self-care pose" },
                    { "Holding Legs Up", "tucked legs" },
                    { "Pic Taking", "photo pose" },
                    { "Optional Pose C", "relaxed sitting" },
                    { "Optional Pose D", "open seated pose" }
                };

        private void Start()
        {
            // Load configuration
            currentConfig = ServiceConfig.Load();
            ResolvePoseControls();
            ResolveExtraControls();
            BuildResponsiveInterface();
            PopulateSettingsUI();

            // Setup listeners
            if (sendButton != null)
                sendButton.onClick.AddListener(HandleSend);
            if (messageInputField != null)
                messageInputField.onSubmit.AddListener(delegate { HandleSend(); });
            if (openSettingsButton != null)
            {
                openSettingsButton.onClick.AddListener(() =>
                {
                    currentConfig = ServiceConfig.Load();
                    PopulateSettingsUI();
                    if (posePickerPanel != null)
                    {
                        posePickerPanel.gameObject.SetActive(false);
                    }
                    if (poseSelectionPanel != null)
                    {
                        poseSelectionPanel.gameObject.SetActive(false);
                    }
                    if (memorySettingsPanel != null)
                    {
                        memorySettingsPanel.gameObject.SetActive(false);
                    }
                    settingsPanel.SetActive(true);
                });
            }
            if (closeSettingsButton != null)
                closeSettingsButton.onClick.AddListener(HandleCloseSettings);
            if (importVrmButton != null)
                importVrmButton.onClick.AddListener(HandleImportVRM);
            if (cyclePoseButton != null)
                cyclePoseButton.onClick.AddListener(() => OnCyclePoseRequested?.Invoke());
            if (saveStateButton != null)
                saveStateButton.onClick.AddListener(() => OnSaveFrameRequested?.Invoke());
            if (loadStateButton != null)
                loadStateButton.onClick.AddListener(() => OnLoadFrameRequested?.Invoke());
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
            if (exploreModeToggle != null)
            {
                exploreModeToggle.onValueChanged.AddListener(
                    (val) => OnExploreModeToggleChanged?.Invoke(val));
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
            AppendLog("System", "hi! :)");
            UpdateConnectionStatus();
        }

        private void ResolvePoseControls()
        {
            if (cyclePoseButton == null)
            {
                Button[] buttons = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Include);
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
                TMP_Dropdown[] dropdowns = UnityEngine.Object.FindObjectsByType<TMP_Dropdown>(FindObjectsInactive.Include);
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

            if (poseDropdown == null)
            {
                poseDropdown = CreateRuntimePoseDropdown();
            }
        }

        private void ResolveExtraControls()
        {
            if (micToggle == null)
            {
                Toggle sourceToggle =
                    webcamToggle != null
                        ? webcamToggle
                        : screenToggle;
                if (sourceToggle != null)
                {
                    GameObject micObject = Instantiate(
                        sourceToggle.gameObject,
                        sourceToggle.transform.parent);
                    micObject.name = "MicToggle";
                    micToggle = micObject.GetComponent<Toggle>();
                    micToggle.SetIsOnWithoutNotify(false);
                    SetToggleText(micToggle, "microphone");
                }
            }

            if (exploreModeToggle == null)
            {
                Toggle sourceToggle =
                    micToggle != null
                        ? micToggle
                        : webcamToggle;
                if (sourceToggle != null)
                {
                    GameObject exploreObject = Instantiate(
                        sourceToggle.gameObject,
                        sourceToggle.transform.parent);
                    exploreObject.name = "ExploreModeToggle";
                    exploreModeToggle =
                        exploreObject.GetComponent<Toggle>();
                    exploreModeToggle.SetIsOnWithoutNotify(false);
                    SetToggleText(exploreModeToggle, "explore");
                }
            }

            if (zoomInSlider == null || zoomOutSlider == null || voiceModelDropdown == null)
            {
                var sliders = UnityEngine.Object.FindObjectsByType<Slider>(FindObjectsInactive.Include);
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

                var dropdowns = UnityEngine.Object.FindObjectsByType<TMP_Dropdown>(FindObjectsInactive.Include);
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
                zoomInSlider = CreateRuntimeZoomSlider("ZoomInSlider", "zoom +", new Vector2(-240f, -88f));
            }
            if (zoomOutSlider == null)
            {
                zoomOutSlider = CreateRuntimeZoomSlider("ZoomOutSlider", "zoom -", new Vector2(-240f, -126f));
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

        private void BuildResponsiveInterface()
        {
            Canvas canvas = messageInputField != null
                ? messageInputField.GetComponentInParent<Canvas>()
                : UnityEngine.Object.FindAnyObjectByType<Canvas>();
            if (canvas == null)
            {
                return;
            }

            responsiveCanvasScaler = canvas.GetComponent<CanvasScaler>();
            if (responsiveCanvasScaler == null)
            {
                responsiveCanvasScaler =
                    canvas.gameObject.AddComponent<CanvasScaler>();
            }
            responsiveCanvasScaler.uiScaleMode =
                CanvasScaler.ScaleMode.ScaleWithScreenSize;
            responsiveCanvasScaler.screenMatchMode =
                CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;

            Transform uiParent =
                messageInputField != null && messageInputField.transform.parent != null
                    ? messageInputField.transform.parent
                    : canvas.transform;

            RectTransform uiParentRect = uiParent as RectTransform;
            RectTransform canvasRect = canvas.GetComponent<RectTransform>();
            if (uiParentRect != null && uiParentRect != canvasRect)
            {
                uiParentRect.anchorMin = Vector2.zero;
                uiParentRect.anchorMax = Vector2.one;
                uiParentRect.pivot = new Vector2(0.5f, 0.5f);
                uiParentRect.anchoredPosition = Vector2.zero;
                uiParentRect.sizeDelta = Vector2.zero;
            }

            GameObject safeAreaObject = new GameObject(
                "JarvisSafeArea",
                typeof(RectTransform));
            safeAreaRoot = safeAreaObject.GetComponent<RectTransform>();
            safeAreaRoot.SetParent(uiParent, false);
            safeAreaRoot.SetAsLastSibling();

            BuildTopControlStack();
            BuildChatDock();
            ApplyInterfaceTheme();
            ApplySafeArea();
        }

        private void BuildTopControlStack()
        {
            topControlStack = CreatePanel(
                "JarvisTopControls",
                safeAreaRoot,
                new Color32(244, 252, 247, 238));
            topControlStack.anchorMin = Vector2.one;
            topControlStack.anchorMax = Vector2.one;
            topControlStack.pivot = Vector2.one;

            VerticalLayoutGroup layout =
                topControlStack.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(14, 14, 14, 14);
            layout.spacing = 9f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            saveStateButton = CreateThemedButton(
                "SaveStateButton",
                topControlStack,
                "save state",
                new Color32(65, 196, 180, 255));
            loadStateButton = CreateThemedButton(
                "LoadStateButton",
                topControlStack,
                "load state",
                new Color32(101, 182, 232, 255));

            if (openSettingsButton != null)
            {
                openSettingsButton.transform.SetParent(topControlStack, false);
                EnsureLayoutElement(openSettingsButton.gameObject, 50f);
                SetButtonText(openSettingsButton, "settings");
                StyleButton(
                    openSettingsButton,
                    new Color32(238, 174, 197, 255));
            }

            topMenuToggleButton = CreateMenuToggleButton(
                "TopMenuToggleButton",
                topControlStack,
                new Color32(65, 196, 180, 255));
            topMenuToggleButton.onClick.AddListener(ToggleTopMenu);
        }

        private void BuildChatDock()
        {
            chatDock = CreatePanel(
                "JarvisChatDock",
                safeAreaRoot,
                new Color32(235, 251, 247, 241));
            chatDock.anchorMin = new Vector2(0.07f, 0f);
            chatDock.anchorMax = new Vector2(0.93f, 0f);
            chatDock.pivot = new Vector2(0.5f, 0f);
            chatDock.anchoredPosition = new Vector2(0f, 24f);
            chatDock.sizeDelta = new Vector2(0f, 272f);

            chatDockTitle = CreateLabel(
                "ChatDockTitle",
                chatDock,
                "jarvis // chat",
                19f,
                new Color32(44, 78, 92, 255),
                TextAlignmentOptions.Left,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(24f, -16f),
                new Vector2(310f, 30f));

            GameObject viewportObject = new GameObject(
                "ChatViewport",
                typeof(RectTransform),
                typeof(RectMask2D));
            RectTransform viewportRect =
                viewportObject.GetComponent<RectTransform>();
            viewportRect.SetParent(chatDock, false);
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = new Vector2(24f, 126f);
            viewportRect.offsetMax = new Vector2(-24f, -52f);
            chatViewport = viewportObject;

            if (chatLogText != null)
            {
                Transform previousParent = chatLogText.transform.parent;
                chatLogText.rectTransform.SetParent(viewportRect, false);
                chatLogText.rectTransform.anchorMin =
                    new Vector2(0f, 0f);
                chatLogText.rectTransform.anchorMax =
                    new Vector2(1f, 0f);
                chatLogText.rectTransform.pivot =
                    new Vector2(0.5f, 0f);
                chatLogText.rectTransform.anchoredPosition =
                    Vector2.zero;
                chatLogText.rectTransform.sizeDelta =
                    new Vector2(0f, viewportRect.rect.height);
                chatLogText.enableAutoSizing = true;
                chatLogText.fontSizeMin = 16f;
                chatLogText.fontSizeMax = 22f;
                chatLogText.alignment = TextAlignmentOptions.BottomLeft;
                chatLogText.textWrappingMode =
                    TextWrappingModes.Normal;
                chatLogText.overflowMode = TextOverflowModes.Overflow;
                chatLogText.color = new Color32(43, 63, 76, 255);
                chatLogText.margin = new Vector4(4f, 2f, 4f, 2f);

                if (previousParent != null &&
                    previousParent != chatDock &&
                    previousParent != viewportRect &&
                    previousParent.childCount == 0)
                {
                    previousParent.gameObject.SetActive(false);
                }
            }

            if (messageInputField != null)
            {
                RectTransform inputRect =
                    messageInputField.GetComponent<RectTransform>();
                inputRect.SetParent(chatDock, false);
                inputRect.anchorMin = new Vector2(0f, 0f);
                inputRect.anchorMax = new Vector2(1f, 0f);
                inputRect.pivot = new Vector2(0.5f, 0f);
                inputRect.offsetMin = new Vector2(24f, 64f);
                inputRect.offsetMax = new Vector2(-162f, 116f);
            }

            if (sendButton != null)
            {
                RectTransform sendRect =
                    sendButton.GetComponent<RectTransform>();
                sendRect.SetParent(chatDock, false);
                sendRect.anchorMin = new Vector2(1f, 0f);
                sendRect.anchorMax = new Vector2(1f, 0f);
                sendRect.pivot = new Vector2(1f, 0f);
                sendRect.anchoredPosition = new Vector2(-24f, 64f);
                sendRect.sizeDelta = new Vector2(126f, 52f);
                SetButtonText(sendButton, "send");
            }

            GameObject actionsObject = new GameObject(
                "QuickActions",
                typeof(RectTransform),
                typeof(GridLayoutGroup));
            quickActions = actionsObject.GetComponent<RectTransform>();
            quickActions.SetParent(chatDock, false);
            quickActions.anchorMin = new Vector2(0f, 0f);
            quickActions.anchorMax = new Vector2(1f, 0f);
            quickActions.pivot = new Vector2(0.5f, 0f);

            quickActionsLayout =
                actionsObject.GetComponent<GridLayoutGroup>();
            quickActionsLayout.spacing = new Vector2(10f, 10f);
            quickActionsLayout.childAlignment = TextAnchor.UpperCenter;
            quickActionsLayout.startAxis = GridLayoutGroup.Axis.Horizontal;
            quickActionsLayout.startCorner =
                GridLayoutGroup.Corner.UpperLeft;
            quickActionsLayout.constraint =
                GridLayoutGroup.Constraint.FixedColumnCount;

            MoveImportVrmToSettings();
            ConfigurePoseShortcutButton();
            BuildPoseMenuControl();
            BuildMemorySettingsControl();
            MoveToQuickActions(webcamToggle, quickActions);
            MoveToQuickActions(screenToggle, quickActions);

            if (animationToggle != null)
            {
                MoveToQuickActions(animationToggle, quickActions);
            }
            if (micToggle != null)
            {
                MoveToQuickActions(micToggle, quickActions);
            }
            if (exploreModeToggle != null)
            {
                MoveToQuickActions(exploreModeToggle, quickActions);
            }

            chatMenuToggleButton = CreateMenuToggleButton(
                "ChatMenuToggleButton",
                chatDock,
                new Color32(101, 182, 232, 255));
            chatMenuToggleButton.onClick.AddListener(ToggleChatDock);

            chatExpandButton = CreateMenuToggleButton(
                "ChatExpandButton",
                chatDock,
                new Color32(65, 196, 180, 255));
            chatExpandButton.onClick.AddListener(ToggleChatDockExpanded);
            SetButtonText(chatExpandButton, "max");

            chatClearButton = CreateMenuToggleButton(
                "ChatClearButton",
                chatDock,
                new Color32(238, 174, 197, 255));
            chatClearButton.onClick.AddListener(ClearChatDisplay);
            SetButtonText(chatClearButton, "clr");

            chatDragHandle = CreatePointerHandle(
                "ChatDragHandle",
                chatDock,
                new Color(1f, 1f, 1f, 0.001f));
            chatDragHandle.SetAsFirstSibling();
            AddPointerEvent(
                chatDragHandle.gameObject,
                EventTriggerType.BeginDrag,
                BeginChatDrag);
            AddPointerEvent(
                chatDragHandle.gameObject,
                EventTriggerType.Drag,
                DragChatDock);

            chatResizeHandle = CreatePointerHandle(
                "ChatResizeHandle",
                chatDock,
                new Color32(65, 196, 180, 210));
            AddPointerEvent(
                chatResizeHandle.gameObject,
                EventTriggerType.BeginDrag,
                BeginChatResize);
            AddPointerEvent(
                chatResizeHandle.gameObject,
                EventTriggerType.Drag,
                ResizeChatDock);
        }

        private void MoveImportVrmToSettings()
        {
            if (importVrmButton == null)
            {
                return;
            }

            Transform parent = settingsPanel != null
                ? settingsPanel.transform
                : quickActions;
            importVrmButton.transform.SetParent(parent, false);
            SetButtonText(importVrmButton, "import vrm");
            StyleButton(
                importVrmButton,
                new Color32(72, 176, 199, 255));

            RectTransform rect =
                importVrmButton.GetComponent<RectTransform>();
            LayoutElement element =
                importVrmButton.GetComponent<LayoutElement>();
            if (element == null)
            {
                element =
                    importVrmButton.gameObject.AddComponent<LayoutElement>();
            }

            if (settingsPanel != null)
            {
                element.ignoreLayout = true;
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = new Vector2(-300f, 260f);
                rect.sizeDelta = new Vector2(180f, 44f);
            }
            else
            {
                EnsureLayoutElement(
                    importVrmButton.gameObject,
                    38f);
            }
        }

        private void ConfigureSettingsButton(
            Button button,
            Vector2 anchoredPosition)
        {
            if (button == null)
            {
                return;
            }

            LayoutElement element =
                button.GetComponent<LayoutElement>();
            element.ignoreLayout = settingsPanel != null;

            if (settingsPanel == null)
            {
                EnsureLayoutElement(button.gameObject, 38f);
                return;
            }

            RectTransform rect =
                button.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(180f, 44f);
        }

        private void BuildPoseMenuControl()
        {
            Transform settingsParent = settingsPanel != null
                ? settingsPanel.transform
                : quickActions;
            poseMenuButton = CreateThemedButton(
                "PoseMenuButton",
                settingsParent,
                "select pose",
                new Color32(255, 253, 239, 255));
            ConfigureSettingsButton(
                poseMenuButton,
                new Vector2(-100f, 260f));
            TMP_Text poseMenuLabel =
                poseMenuButton.GetComponentInChildren<TMP_Text>(true);
            if (poseMenuLabel != null)
            {
                poseMenuLabel.color = new Color32(44, 78, 92, 255);
                poseMenuLabel.fontSizeMin = 11f;
                poseMenuLabel.fontSizeMax = 17f;
            }
            poseMenuButton.onClick.AddListener(TogglePoseSelector);

            poseRotationButton = CreateThemedButton(
                "PoseRotationButton",
                settingsParent,
                "pose rotation",
                new Color32(65, 196, 180, 255));
            ConfigureSettingsButton(
                poseRotationButton,
                new Vector2(100f, 260f));
            poseRotationButton.onClick.AddListener(TogglePosePicker);

            if (poseDropdown != null)
            {
                poseDropdown.gameObject.SetActive(false);
            }

            posePickerPanel = CreatePanel(
                "PosePickerPanel",
                settingsParent,
                new Color32(239, 250, 246, 250));
            posePickerPanel.SetAsLastSibling();

            CreateLabel(
                "PosePickerTitle",
                posePickerPanel,
                "pose rotation",
                19f,
                new Color32(44, 78, 92, 255),
                TextAlignmentOptions.Left,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(18f, -12f),
                new Vector2(320f, 34f));

            Button closeButton = CreateThemedButton(
                "ClosePosePickerButton",
                posePickerPanel,
                "x",
                new Color32(238, 174, 197, 255));
            LayoutElement closeLayout =
                closeButton.GetComponent<LayoutElement>();
            closeLayout.ignoreLayout = true;
            RectTransform closeRect =
                closeButton.GetComponent<RectTransform>();
            closeRect.anchorMin = Vector2.one;
            closeRect.anchorMax = Vector2.one;
            closeRect.pivot = Vector2.one;
            closeRect.anchoredPosition = new Vector2(-12f, -12f);
            closeRect.sizeDelta = new Vector2(36f, 36f);
            closeButton.onClick.AddListener(
                () => posePickerPanel.gameObject.SetActive(false));

            GameObject rowsObject = new GameObject(
                "PoseRows",
                typeof(RectTransform),
                typeof(VerticalLayoutGroup));
            poseRowsRoot = rowsObject.GetComponent<RectTransform>();
            poseRowsRoot.SetParent(posePickerPanel, false);
            poseRowsRoot.anchorMin = Vector2.zero;
            poseRowsRoot.anchorMax = Vector2.one;
            poseRowsRoot.offsetMin = new Vector2(14f, 14f);
            poseRowsRoot.offsetMax = new Vector2(-14f, -58f);

            VerticalLayoutGroup rowsLayout =
                rowsObject.GetComponent<VerticalLayoutGroup>();
            rowsLayout.spacing = 6f;
            rowsLayout.childAlignment = TextAnchor.UpperCenter;
            rowsLayout.childControlWidth = true;
            rowsLayout.childControlHeight = true;
            rowsLayout.childForceExpandWidth = true;
            rowsLayout.childForceExpandHeight = false;

            poseSelectionPanel = CreatePanel(
                "PoseSelectionPanel",
                settingsParent,
                new Color32(239, 250, 246, 250));
            poseSelectionPanel.SetAsLastSibling();

            CreateLabel(
                "PoseSelectionTitle",
                poseSelectionPanel,
                "select pose",
                19f,
                new Color32(44, 78, 92, 255),
                TextAlignmentOptions.Left,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(18f, -12f),
                new Vector2(320f, 34f));

            Button closeSelectionButton = CreateThemedButton(
                "ClosePoseSelectionButton",
                poseSelectionPanel,
                "x",
                new Color32(238, 174, 197, 255));
            LayoutElement closeSelectionLayout =
                closeSelectionButton.GetComponent<LayoutElement>();
            closeSelectionLayout.ignoreLayout = true;
            RectTransform closeSelectionRect =
                closeSelectionButton.GetComponent<RectTransform>();
            closeSelectionRect.anchorMin = Vector2.one;
            closeSelectionRect.anchorMax = Vector2.one;
            closeSelectionRect.pivot = Vector2.one;
            closeSelectionRect.anchoredPosition =
                new Vector2(-12f, -12f);
            closeSelectionRect.sizeDelta =
                new Vector2(36f, 36f);
            closeSelectionButton.onClick.AddListener(
                () =>
                    poseSelectionPanel.gameObject.SetActive(false));

            GameObject selectionRowsObject = new GameObject(
                "PoseSelectionRows",
                typeof(RectTransform),
                typeof(VerticalLayoutGroup));
            poseSelectionRowsRoot =
                selectionRowsObject.GetComponent<RectTransform>();
            poseSelectionRowsRoot.SetParent(
                poseSelectionPanel,
                false);
            poseSelectionRowsRoot.anchorMin = Vector2.zero;
            poseSelectionRowsRoot.anchorMax = Vector2.one;
            poseSelectionRowsRoot.offsetMin =
                new Vector2(14f, 14f);
            poseSelectionRowsRoot.offsetMax =
                new Vector2(-14f, -58f);

            VerticalLayoutGroup selectionRowsLayout =
                selectionRowsObject.GetComponent<VerticalLayoutGroup>();
            selectionRowsLayout.spacing = 6f;
            selectionRowsLayout.childAlignment =
                TextAnchor.UpperCenter;
            selectionRowsLayout.childControlWidth = true;
            selectionRowsLayout.childControlHeight = true;
            selectionRowsLayout.childForceExpandWidth = true;
            selectionRowsLayout.childForceExpandHeight = false;

            if (poseDisplayNames.Count > 0)
            {
                RebuildPoseRows();
                UpdatePoseMenuLabel(
                    poseDropdown != null
                        ? poseDropdown.value
                        : 0);
            }

            posePickerPanel.gameObject.SetActive(false);
            poseSelectionPanel.gameObject.SetActive(false);
        }

        private void BuildMemorySettingsControl()
        {
            Transform settingsParent = settingsPanel != null
                ? settingsPanel.transform
                : quickActions;

            memorySettingsButton = CreateThemedButton(
                "MemorySettingsButton",
                settingsParent,
                "memory",
                new Color32(238, 174, 197, 255));
            ConfigureSettingsButton(
                memorySettingsButton,
                new Vector2(300f, 260f));
            memorySettingsButton.onClick.AddListener(
                ToggleMemorySettings);

            memorySettingsPanel = CreatePanel(
                "MemorySettingsPanel",
                settingsParent,
                new Color32(239, 250, 246, 252));
            memorySettingsPanel.SetAsLastSibling();

            CreateLabel(
                "MemorySettingsTitle",
                memorySettingsPanel,
                "jarvis memory",
                22f,
                new Color32(44, 78, 92, 255),
                TextAlignmentOptions.Left,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(20f, -14f),
                new Vector2(360f, 36f));

            Button closeButton = CreateThemedButton(
                "CloseMemorySettingsButton",
                memorySettingsPanel,
                "x",
                new Color32(238, 174, 197, 255));
            LayoutElement closeLayout =
                closeButton.GetComponent<LayoutElement>();
            closeLayout.ignoreLayout = true;
            RectTransform closeRect =
                closeButton.GetComponent<RectTransform>();
            closeRect.anchorMin = Vector2.one;
            closeRect.anchorMax = Vector2.one;
            closeRect.pivot = Vector2.one;
            closeRect.anchoredPosition =
                new Vector2(-14f, -14f);
            closeRect.sizeDelta =
                new Vector2(40f, 40f);
            closeButton.onClick.AddListener(
                () =>
                {
                    SaveMemorySettings();
                    memorySettingsPanel.gameObject.SetActive(
                        false);
                });

            memoryEnabledToggle =
                CreateMemorySettingToggle(
                    "MemoryBankToggle",
                    memorySettingsPanel,
                    "memory bank",
                    new Vector2(18f, -64f));
            longTermMemoryToggle =
                CreateMemorySettingToggle(
                    "LongTermMemoryToggle",
                    memorySettingsPanel,
                    "long-term",
                    new Vector2(258f, -64f));
            constitutionEnabledToggle =
                CreateMemorySettingToggle(
                    "ConstitutionToggle",
                    memorySettingsPanel,
                    "constitution",
                    new Vector2(498f, -64f));

            memoryEnabledToggle.onValueChanged.AddListener(
                enabled =>
                {
                    currentConfig.memoryEnabled = enabled;
                    UpdateMemoryStatus();
                });
            longTermMemoryToggle.onValueChanged.AddListener(
                enabled =>
                {
                    currentConfig.longTermMemoryEnabled =
                        enabled;
                    UpdateMemoryStatus();
                });
            constitutionEnabledToggle.onValueChanged.AddListener(
                enabled =>
                {
                    currentConfig.constitutionEnabled =
                        enabled;
                    UpdateMemoryStatus();
                });

            memoryStatusLabel = CreateLabel(
                "MemoryStatus",
                memorySettingsPanel,
                "local memory ready",
                15f,
                new Color32(71, 110, 119, 255),
                TextAlignmentOptions.Left,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0f, -122f),
                new Vector2(-40f, 28f));
            memoryStatusLabel.margin =
                new Vector4(20f, 0f, 20f, 0f);

            CreateLabel(
                "ConstitutionLabel",
                memorySettingsPanel,
                "constitution",
                17f,
                new Color32(44, 78, 92, 255),
                TextAlignmentOptions.Left,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(20f, -158f),
                new Vector2(320f, 30f));

            constitutionInput =
                CreateConstitutionInput(
                    memorySettingsPanel);

            Button saveButton = CreateThemedButton(
                "SaveMemorySettingsButton",
                memorySettingsPanel,
                "save memory settings",
                new Color32(65, 196, 180, 255));
            LayoutElement saveLayout =
                saveButton.GetComponent<LayoutElement>();
            saveLayout.ignoreLayout = true;
            RectTransform saveRect =
                saveButton.GetComponent<RectTransform>();
            saveRect.anchorMin =
                new Vector2(0.5f, 0f);
            saveRect.anchorMax =
                new Vector2(0.5f, 0f);
            saveRect.pivot =
                new Vector2(0.5f, 0f);
            saveRect.anchoredPosition =
                new Vector2(0f, 18f);
            saveRect.sizeDelta =
                new Vector2(280f, 46f);
            saveButton.onClick.AddListener(
                SaveMemorySettings);

            PopulateMemorySettingsUI();
            memorySettingsPanel.gameObject.SetActive(false);
        }

        private Toggle CreateMemorySettingToggle(
            string objectName,
            Transform parent,
            string labelText,
            Vector2 anchoredPosition)
        {
            GameObject toggleObject = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(Toggle));
            RectTransform toggleRect =
                toggleObject.GetComponent<RectTransform>();
            toggleRect.SetParent(parent, false);
            toggleRect.anchorMin =
                new Vector2(0f, 1f);
            toggleRect.anchorMax =
                new Vector2(0f, 1f);
            toggleRect.pivot =
                new Vector2(0f, 1f);
            toggleRect.anchoredPosition =
                anchoredPosition;
            toggleRect.sizeDelta =
                new Vector2(224f, 40f);

            GameObject trackObject = new GameObject(
                "Background",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            trackObject.transform.SetParent(
                toggleObject.transform,
                false);

            Toggle toggle =
                toggleObject.GetComponent<Toggle>();
            toggle.targetGraphic =
                trackObject.GetComponent<Image>();

            CreateLabel(
                "Label",
                toggleObject.transform,
                labelText,
                16f,
                new Color32(44, 78, 92, 255),
                TextAlignmentOptions.MidlineLeft,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero);
            StyleToggle(toggle);
            return toggle;
        }

        private TMP_InputField CreateConstitutionInput(
            Transform parent)
        {
            TMP_DefaultControls.Resources resources =
                new TMP_DefaultControls.Resources();
            GameObject inputObject =
                TMP_DefaultControls.CreateInputField(
                    resources);
            inputObject.name = "ConstitutionInput";

            RectTransform inputRect =
                inputObject.GetComponent<RectTransform>();
            inputRect.SetParent(parent, false);
            inputRect.anchorMin =
                new Vector2(0f, 0f);
            inputRect.anchorMax =
                new Vector2(1f, 1f);
            inputRect.offsetMin =
                new Vector2(20f, 78f);
            inputRect.offsetMax =
                new Vector2(-20f, -198f);

            TMP_InputField input =
                inputObject.GetComponent<TMP_InputField>();
            input.lineType =
                TMP_InputField.LineType.MultiLineNewline;
            input.contentType =
                TMP_InputField.ContentType.Standard;
            input.characterLimit = 8000;

            TMP_Text placeholder =
                input.placeholder as TMP_Text;
            if (placeholder != null)
            {
                placeholder.text =
                    "write the principles jarvis should always follow...";
            }

            StyleInputField(input);
            if (input.textComponent != null)
            {
                input.textComponent.alignment =
                    TextAlignmentOptions.TopLeft;
                input.textComponent.textWrappingMode =
                    TextWrappingModes.Normal;
            }
            if (placeholder != null)
            {
                placeholder.alignment =
                    TextAlignmentOptions.TopLeft;
            }
            if (input.textViewport != null)
            {
                input.textViewport.offsetMin =
                    new Vector2(16f, 14f);
                input.textViewport.offsetMax =
                    new Vector2(-16f, -14f);
            }
            return input;
        }

        private void ToggleMemorySettings()
        {
            if (memorySettingsPanel == null)
            {
                return;
            }

            bool shouldOpen =
                !memorySettingsPanel.gameObject.activeSelf;
            if (posePickerPanel != null)
            {
                posePickerPanel.gameObject.SetActive(false);
            }
            if (poseSelectionPanel != null)
            {
                poseSelectionPanel.gameObject.SetActive(false);
            }

            memorySettingsPanel.gameObject.SetActive(
                shouldOpen);
            if (shouldOpen)
            {
                PopulateMemorySettingsUI();
                memorySettingsPanel.SetAsLastSibling();
            }
        }

        private void PopulateMemorySettingsUI()
        {
            if (currentConfig == null)
            {
                currentConfig = ServiceConfig.Load();
            }

            if (memoryEnabledToggle != null)
            {
                memoryEnabledToggle.SetIsOnWithoutNotify(
                    currentConfig.memoryEnabled);
                StyleToggle(memoryEnabledToggle);
            }
            if (longTermMemoryToggle != null)
            {
                longTermMemoryToggle.SetIsOnWithoutNotify(
                    currentConfig.longTermMemoryEnabled);
                StyleToggle(longTermMemoryToggle);
            }
            if (constitutionEnabledToggle != null)
            {
                constitutionEnabledToggle.SetIsOnWithoutNotify(
                    currentConfig.constitutionEnabled);
                StyleToggle(constitutionEnabledToggle);
            }
            if (constitutionInput != null)
            {
                constitutionInput.SetTextWithoutNotify(
                    CompanionMemoryService.Instance
                        .GetConstitution());
            }
            UpdateMemoryStatus();
        }

        private void SaveMemorySettings()
        {
            if (currentConfig == null)
            {
                currentConfig = ServiceConfig.Load();
            }

            if (memoryEnabledToggle != null)
            {
                currentConfig.memoryEnabled =
                    memoryEnabledToggle.isOn;
            }
            if (longTermMemoryToggle != null)
            {
                currentConfig.longTermMemoryEnabled =
                    longTermMemoryToggle.isOn;
            }
            if (constitutionEnabledToggle != null)
            {
                currentConfig.constitutionEnabled =
                    constitutionEnabledToggle.isOn;
            }
            if (constitutionInput != null)
            {
                CompanionMemoryService.Instance
                    .SaveConstitution(
                        constitutionInput.text);
            }
            currentConfig.Save();
            UpdateMemoryStatus();
        }

        private void UpdateMemoryStatus()
        {
            if (memoryStatusLabel == null)
            {
                return;
            }

            CompanionMemoryService memory =
                CompanionMemoryService.Instance;
            memoryStatusLabel.text =
                $"local only // memory bank: " +
                $"{memory.RecentMemoryCount} messages // " +
                $"long-term: {memory.LongTermMemoryCount} memories";
            memoryStatusLabel.color =
                currentConfig != null &&
                currentConfig.memoryEnabled
                    ? new Color32(39, 157, 145, 255)
                    : new Color32(123, 139, 143, 255);
        }

        private void TogglePosePicker()
        {
            if (posePickerPanel == null)
            {
                return;
            }

            bool shouldOpen = !posePickerPanel.gameObject.activeSelf;
            if (poseSelectionPanel != null)
            {
                poseSelectionPanel.gameObject.SetActive(false);
            }
            posePickerPanel.gameObject.SetActive(shouldOpen);
            if (shouldOpen)
            {
                posePickerPanel.SetAsLastSibling();
            }
        }

        private void TogglePoseSelector()
        {
            if (poseSelectionPanel == null)
            {
                return;
            }

            bool shouldOpen =
                !poseSelectionPanel.gameObject.activeSelf;
            if (posePickerPanel != null)
            {
                posePickerPanel.gameObject.SetActive(false);
            }
            poseSelectionPanel.gameObject.SetActive(shouldOpen);
            if (shouldOpen)
            {
                RebuildPoseSelectionRows();
                poseSelectionPanel.SetAsLastSibling();
            }
        }

        private void RebuildPoseRows()
        {
            if (poseRowsRoot == null)
            {
                return;
            }

            for (int i = poseRowsRoot.childCount - 1; i >= 0; i--)
            {
                GameObject child =
                    poseRowsRoot.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }

            while (poseRotationEnabled.Count < poseDisplayNames.Count)
            {
                poseRotationEnabled.Add(true);
            }
            if (poseRotationEnabled.Count > poseDisplayNames.Count)
            {
                poseRotationEnabled.RemoveRange(
                    poseDisplayNames.Count,
                    poseRotationEnabled.Count -
                    poseDisplayNames.Count);
            }

            for (int index = 0; index < poseDisplayNames.Count; index++)
            {
                int capturedIndex = index;
                GameObject rowObject = new GameObject(
                    $"PoseRow{index}",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image),
                    typeof(HorizontalLayoutGroup),
                    typeof(LayoutElement));
                rowObject.transform.SetParent(poseRowsRoot, false);

                Image rowImage = rowObject.GetComponent<Image>();
                rowImage.sprite = GetRoundedPanelSprite();
                rowImage.type = Image.Type.Sliced;
                rowImage.color = new Color32(255, 255, 255, 150);

                HorizontalLayoutGroup rowLayout =
                    rowObject.GetComponent<HorizontalLayoutGroup>();
                rowLayout.padding = new RectOffset(6, 6, 4, 4);
                rowLayout.spacing = 8f;
                rowLayout.childAlignment = TextAnchor.MiddleCenter;
                rowLayout.childControlWidth = true;
                rowLayout.childControlHeight = true;
                rowLayout.childForceExpandWidth = false;
                rowLayout.childForceExpandHeight = false;

                LayoutElement rowElement =
                    rowObject.GetComponent<LayoutElement>();
                rowElement.preferredHeight = 42f;
                rowElement.minHeight = 42f;

                TMP_Text poseLabel = CreateLabel(
                    $"PoseLabel{index}",
                    rowObject.transform,
                    poseDisplayNames[index],
                    17f,
                    new Color32(44, 78, 92, 255),
                    TextAlignmentOptions.MidlineLeft,
                    Vector2.zero,
                    Vector2.one,
                    Vector2.zero,
                    Vector2.zero);
                LayoutElement labelElement =
                    poseLabel.gameObject.AddComponent<LayoutElement>();
                labelElement.preferredHeight = 34f;
                labelElement.minHeight = 34f;
                labelElement.preferredWidth = 320f;
                labelElement.flexibleWidth = 1f;

                Toggle rotationToggle = CreatePoseRotationToggle(
                    rowObject.transform,
                    capturedIndex);
                rotationToggle.isOn =
                    poseRotationEnabled[capturedIndex];
                rotationToggle.onValueChanged.AddListener(
                    enabled =>
                    {
                        poseRotationEnabled[capturedIndex] = enabled;
                        PlayerPrefs.SetInt(
                            GetPoseRotationKey(
                                poseInternalNames[capturedIndex]),
                            enabled ? 1 : 0);
                        PlayerPrefs.Save();
                        RebuildPoseSelectionRows();
                    });
            }

            RebuildPoseSelectionRows();
        }

        private void RebuildPoseSelectionRows()
        {
            if (poseSelectionRowsRoot == null)
            {
                return;
            }

            for (int i =
                     poseSelectionRowsRoot.childCount - 1;
                 i >= 0;
                 i--)
            {
                GameObject child =
                    poseSelectionRowsRoot.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }

            int enabledCount = 0;
            for (int index = 0;
                 index < poseDisplayNames.Count;
                 index++)
            {
                if (!IsPoseEnabledForRotation(index))
                {
                    continue;
                }

                int capturedIndex = index;
                Button selectButton = CreateThemedButton(
                    $"SelectPose{index}",
                    poseSelectionRowsRoot,
                    poseDisplayNames[index],
                    new Color32(101, 182, 232, 255));
                LayoutElement selectElement =
                    selectButton.GetComponent<LayoutElement>();
                selectElement.preferredHeight = 42f;
                selectElement.minHeight = 42f;
                selectButton.onClick.AddListener(
                    () => SelectPoseFromPicker(capturedIndex));
                enabledCount++;
            }

            if (enabledCount == 0)
            {
                TMP_Text emptyLabel = CreateLabel(
                    "NoEnabledPoses",
                    poseSelectionRowsRoot,
                    "no poses enabled",
                    17f,
                    new Color32(91, 119, 128, 255),
                    TextAlignmentOptions.Center,
                    Vector2.zero,
                    Vector2.one,
                    Vector2.zero,
                    Vector2.zero);
                LayoutElement emptyElement =
                    emptyLabel.gameObject.AddComponent<LayoutElement>();
                emptyElement.preferredHeight = 52f;
                emptyElement.minHeight = 52f;
            }
        }

        private Toggle CreatePoseRotationToggle(
            Transform parent,
            int index)
        {
            GameObject toggleObject = new GameObject(
                $"PoseRotationToggle{index}",
                typeof(RectTransform),
                typeof(Toggle),
                typeof(LayoutElement));
            toggleObject.transform.SetParent(parent, false);

            GameObject trackObject = new GameObject(
                "Background",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            trackObject.transform.SetParent(
                toggleObject.transform,
                false);

            Toggle toggle = toggleObject.GetComponent<Toggle>();
            toggle.targetGraphic = trackObject.GetComponent<Image>();

            LayoutElement element =
                toggleObject.GetComponent<LayoutElement>();
            element.minWidth = 64f;
            element.preferredWidth = 64f;
            element.flexibleWidth = 0f;
            element.minHeight = 34f;
            element.preferredHeight = 34f;

            StyleToggle(toggle);
            return toggle;
        }

        private void SelectPoseFromPicker(int index)
        {
            if (index < 0 || index >= poseDisplayNames.Count)
            {
                return;
            }

            if (poseDropdown != null)
            {
                poseDropdown.SetValueWithoutNotify(index);
            }
            UpdatePoseMenuLabel(index);
            OnPoseSelected?.Invoke(index);
            if (posePickerPanel != null)
            {
                posePickerPanel.gameObject.SetActive(false);
            }
            if (poseSelectionPanel != null)
            {
                poseSelectionPanel.gameObject.SetActive(false);
            }
        }

        private void ConfigurePoseShortcutButton()
        {
            if (cyclePoseButton == null || safeAreaRoot == null)
            {
                return;
            }

            cyclePoseButton.transform.SetParent(safeAreaRoot, false);
            cyclePoseButton.transform.SetAsLastSibling();
            SetButtonText(cyclePoseButton, "p");
            StyleButton(
                cyclePoseButton,
                new Color32(65, 196, 180, 255));

            EnsureLayoutElement(cyclePoseButton.gameObject, 40f);
            LayoutElement layoutElement =
                cyclePoseButton.GetComponent<LayoutElement>();
            layoutElement.ignoreLayout = true;
            layoutElement.minWidth = 40f;
            layoutElement.preferredWidth = 40f;
            layoutElement.flexibleWidth = 0f;

            TMP_Text label =
                cyclePoseButton.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.fontSizeMin = 18f;
                label.fontSizeMax = 23f;
            }
        }

        private void MoveToQuickActions(
            Selectable control,
            RectTransform actionsRect,
            string buttonText = null)
        {
            if (control == null)
            {
                return;
            }

            control.transform.SetParent(actionsRect, false);
            EnsureLayoutElement(control.gameObject, 38f);

            Button button = control as Button;
            if (button != null && !string.IsNullOrEmpty(buttonText))
            {
                SetButtonText(button, buttonText);
            }
        }

        private void ApplyInterfaceTheme()
        {
            StyleButton(
                sendButton,
                new Color32(255, 124, 103, 255));
            StyleButton(
                importVrmButton,
                new Color32(72, 176, 199, 255));
            StyleButton(
                cyclePoseButton,
                new Color32(65, 196, 180, 255));
            StyleInputField(messageInputField);
            StyleDropdown(poseDropdown);
            SetToggleText(webcamToggle, "webcam");
            SetToggleText(screenToggle, "screen");
            SetToggleText(micToggle, "microphone");
            SetToggleText(exploreModeToggle, "explore");
            StyleToggle(webcamToggle);
            StyleToggle(screenToggle);
            StyleToggle(animationToggle);
            StyleToggle(micToggle);
            StyleToggle(exploreModeToggle);

            if (settingsPanel != null)
            {
                Image settingsImage = settingsPanel.GetComponent<Image>();
                if (settingsImage != null)
                {
                    settingsImage.sprite = GetRoundedPanelSprite();
                    settingsImage.type = Image.Type.Sliced;
                    settingsImage.color =
                        new Color32(239, 249, 245, 248);
                }

                TMP_InputField[] settingsInputs =
                    settingsPanel.GetComponentsInChildren<TMP_InputField>(true);
                foreach (TMP_InputField input in settingsInputs)
                {
                    StyleInputField(input);
                }

                Button[] settingsButtons =
                    settingsPanel.GetComponentsInChildren<Button>(true);
                foreach (Button button in settingsButtons)
                {
                    StyleButton(
                        button,
                        new Color32(101, 182, 232, 255));
                }

                TMP_Dropdown[] settingsDropdowns =
                    settingsPanel.GetComponentsInChildren<TMP_Dropdown>(true);
                foreach (TMP_Dropdown dropdown in settingsDropdowns)
                {
                    StyleDropdown(dropdown);
                }

                LowercaseStaticLabels(
                    settingsPanel,
                    settingsInputs);

                StyleButton(
                    importVrmButton,
                    new Color32(72, 176, 199, 255));
                StyleButton(
                    poseMenuButton,
                    new Color32(255, 253, 239, 255));
                TMP_Text poseLabel =
                    poseMenuButton != null
                        ? poseMenuButton.GetComponentInChildren<TMP_Text>(true)
                        : null;
                if (poseLabel != null)
                {
                    poseLabel.color =
                        new Color32(44, 78, 92, 255);
                }
                StyleButton(
                    poseRotationButton,
                    new Color32(65, 196, 180, 255));
                StyleButton(
                    memorySettingsButton,
                    new Color32(238, 174, 197, 255));
            }
        }

        private static void LowercaseStaticLabels(
            GameObject root,
            TMP_InputField[] inputFields)
        {
            if (root == null)
            {
                return;
            }

            TMP_Text[] labels =
                root.GetComponentsInChildren<TMP_Text>(true);
            foreach (TMP_Text label in labels)
            {
                bool isInputValue = false;
                foreach (TMP_InputField input in inputFields)
                {
                    if (input != null && input.textComponent == label)
                    {
                        isInputValue = true;
                        break;
                    }
                }

                if (!isInputValue && label != null)
                {
                    label.text = label.text.ToLowerInvariant();
                }
            }
        }

        private RectTransform CreatePanel(
            string objectName,
            Transform parent,
            Color color)
        {
            GameObject panelObject = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Shadow));
            RectTransform rect = panelObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);

            Image image = panelObject.GetComponent<Image>();
            image.sprite = GetRoundedPanelSprite();
            image.type = Image.Type.Sliced;
            image.color = color;

            Shadow shadow = panelObject.GetComponent<Shadow>();
            shadow.effectColor = new Color32(35, 80, 96, 45);
            shadow.effectDistance = new Vector2(0f, -7f);
            shadow.useGraphicAlpha = true;
            return rect;
        }

        private Button CreateThemedButton(
            string objectName,
            Transform parent,
            string labelText,
            Color color)
        {
            GameObject buttonObject = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button),
                typeof(LayoutElement));
            buttonObject.transform.SetParent(parent, false);
            Button button = buttonObject.GetComponent<Button>();
            EnsureLayoutElement(buttonObject, 50f);

            CreateLabel(
                "Label",
                buttonObject.transform,
                labelText,
                20f,
                Color.white,
                TextAlignmentOptions.Center,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero);
            StyleButton(button, color);
            return button;
        }

        private Button CreateMenuToggleButton(
            string objectName,
            Transform parent,
            Color color)
        {
            Button button = CreateThemedButton(
                objectName,
                parent,
                "-",
                color);
            LayoutElement layoutElement =
                button.GetComponent<LayoutElement>();
            layoutElement.ignoreLayout = true;
            layoutElement.minWidth = 40f;
            layoutElement.preferredWidth = 40f;
            layoutElement.flexibleWidth = 0f;

            TMP_Text label =
                button.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.fontSizeMin = 20f;
                label.fontSizeMax = 28f;
            }
            return button;
        }

        private static RectTransform CreatePointerHandle(
            string objectName,
            Transform parent,
            Color color)
        {
            GameObject handleObject = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            handleObject.transform.SetParent(parent, false);
            Image image = handleObject.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = true;
            return handleObject.GetComponent<RectTransform>();
        }

        private static void AddPointerEvent(
            GameObject target,
            EventTriggerType eventType,
            UnityAction<BaseEventData> callback)
        {
            EventTrigger trigger =
                target.GetComponent<EventTrigger>();
            if (trigger == null)
            {
                trigger = target.AddComponent<EventTrigger>();
            }
            if (trigger.triggers == null)
            {
                trigger.triggers =
                    new List<EventTrigger.Entry>();
            }

            EventTrigger.Entry entry =
                new EventTrigger.Entry
                {
                    eventID = eventType
                };
            entry.callback.AddListener(callback);
            trigger.triggers.Add(entry);
        }

        private void ToggleTopMenu()
        {
            SetTopMenuCollapsed(!isTopMenuCollapsed);
        }

        private void SetTopMenuCollapsed(bool collapsed)
        {
            isTopMenuCollapsed = collapsed;
            SetControlVisible(saveStateButton, !collapsed);
            SetControlVisible(loadStateButton, !collapsed);
            SetControlVisible(openSettingsButton, !collapsed);

            if (collapsed &&
                settingsPanel != null &&
                settingsPanel.activeSelf)
            {
                settingsPanel.SetActive(false);
            }

            SetButtonText(
                topMenuToggleButton,
                collapsed ? "+" : "-");
            ApplyResponsiveLayout();
        }

        private void ToggleChatDock()
        {
            SetChatDockCollapsed(!isChatDockCollapsed);
        }

        private void ToggleChatDockExpanded()
        {
            isChatDockExpanded = !isChatDockExpanded;
            isChatDockFloating = false;
            SetButtonText(
                chatExpandButton,
                isChatDockExpanded ? "fit" : "max");
            ApplyResponsiveLayout();
        }

        private void BeginChatDrag(BaseEventData eventData)
        {
            if (isChatDockCollapsed)
            {
                return;
            }

            ConvertChatDockToFloating();
            isChatDockExpanded = false;
            SetButtonText(chatExpandButton, "max");
        }

        private void DragChatDock(BaseEventData eventData)
        {
            PointerEventData pointer =
                eventData as PointerEventData;
            if (pointer == null ||
                chatDock == null ||
                isChatDockCollapsed)
            {
                return;
            }

            Canvas canvas =
                chatDock.GetComponentInParent<Canvas>();
            float scaleFactor =
                canvas != null
                    ? Mathf.Max(0.01f, canvas.scaleFactor)
                    : 1f;
            chatDock.anchoredPosition +=
                pointer.delta / scaleFactor;
            ClampChatDockToSafeArea();
        }

        private void BeginChatResize(BaseEventData eventData)
        {
            if (isChatDockCollapsed)
            {
                return;
            }

            ConvertChatDockToFloating();
            isChatDockExpanded = false;
            SetButtonText(chatExpandButton, "max");
        }

        private void ResizeChatDock(BaseEventData eventData)
        {
            PointerEventData pointer =
                eventData as PointerEventData;
            if (pointer == null ||
                chatDock == null ||
                isChatDockCollapsed)
            {
                return;
            }

            Canvas canvas =
                chatDock.GetComponentInParent<Canvas>();
            float scaleFactor =
                canvas != null
                    ? Mathf.Max(0.01f, canvas.scaleFactor)
                    : 1f;
            Vector2 delta = pointer.delta / scaleFactor;
            Rect safeRect =
                safeAreaRoot != null
                    ? safeAreaRoot.rect
                    : new Rect(
                        0f,
                        0f,
                        Screen.width,
                        Screen.height);
            float minWidth = Mathf.Min(420f, safeRect.width - 20f);
            float minHeight = Mathf.Min(220f, safeRect.height - 20f);
            Vector2 nextSize = new Vector2(
                Mathf.Clamp(
                    chatDock.sizeDelta.x + delta.x,
                    minWidth,
                    Mathf.Max(minWidth, safeRect.width - 20f)),
                Mathf.Clamp(
                    chatDock.sizeDelta.y + delta.y,
                    minHeight,
                    Mathf.Max(minHeight, safeRect.height - 20f)));
            Vector2 applied = nextSize - chatDock.sizeDelta;
            chatDock.sizeDelta = nextSize;
            chatDock.anchoredPosition +=
                new Vector2(applied.x * 0.5f, 0f);
            ClampChatDockToSafeArea();
            ApplyChatContentsLayout();
        }

        private void ConvertChatDockToFloating()
        {
            if (chatDock == null || isChatDockFloating)
            {
                return;
            }

            Canvas.ForceUpdateCanvases();
            Vector3[] corners = new Vector3[4];
            chatDock.GetWorldCorners(corners);
            Vector3 bottomCenter =
                (corners[0] + corners[3]) * 0.5f;
            Vector2 currentSize = chatDock.rect.size;
            chatDock.anchorMin = new Vector2(0.5f, 0f);
            chatDock.anchorMax = new Vector2(0.5f, 0f);
            chatDock.pivot = new Vector2(0.5f, 0f);
            chatDock.sizeDelta = currentSize;
            chatDock.position = bottomCenter;
            isChatDockFloating = true;
        }

        private void ClampChatDockToSafeArea()
        {
            if (chatDock == null || safeAreaRoot == null)
            {
                return;
            }

            Vector3[] dockCorners = new Vector3[4];
            Vector3[] safeCorners = new Vector3[4];
            chatDock.GetWorldCorners(dockCorners);
            safeAreaRoot.GetWorldCorners(safeCorners);
            Vector3 correction = Vector3.zero;

            if (dockCorners[0].x < safeCorners[0].x)
            {
                correction.x += safeCorners[0].x - dockCorners[0].x;
            }
            if (dockCorners[2].x > safeCorners[2].x)
            {
                correction.x += safeCorners[2].x - dockCorners[2].x;
            }
            if (dockCorners[0].y < safeCorners[0].y)
            {
                correction.y += safeCorners[0].y - dockCorners[0].y;
            }
            if (dockCorners[2].y > safeCorners[2].y)
            {
                correction.y += safeCorners[2].y - dockCorners[2].y;
            }

            chatDock.position += correction;
        }

        private void SetChatDockCollapsed(bool collapsed)
        {
            isChatDockCollapsed = collapsed;

            if (collapsed)
            {
                isChatDockFloating = false;
                isChatDockExpanded = false;
                SetButtonText(chatExpandButton, "max");
            }

            if (collapsed && posePickerPanel != null)
            {
                posePickerPanel.gameObject.SetActive(false);
            }
            if (collapsed && poseSelectionPanel != null)
            {
                poseSelectionPanel.gameObject.SetActive(false);
            }

            if (collapsed &&
                messageInputField != null &&
                messageInputField.isFocused)
            {
                messageInputField.DeactivateInputField();
            }

            SetControlVisible(chatDockTitle, !collapsed);
            SetControlVisible(chatViewport, !collapsed);
            SetControlVisible(messageInputField, !collapsed);
            SetControlVisible(sendButton, !collapsed);
            SetControlVisible(quickActions, !collapsed);
            SetControlVisible(chatExpandButton, !collapsed);
            SetControlVisible(chatClearButton, !collapsed);
            SetControlVisible(chatDragHandle, !collapsed);
            SetControlVisible(chatResizeHandle, !collapsed);
            SetButtonText(
                chatMenuToggleButton,
                collapsed ? "+" : "-");
            ApplyResponsiveLayout();
        }

        private static void SetControlVisible(
            Component control,
            bool visible)
        {
            if (control != null)
            {
                control.gameObject.SetActive(visible);
            }
        }

        private static void SetControlVisible(
            GameObject control,
            bool visible)
        {
            if (control != null)
            {
                control.SetActive(visible);
            }
        }

        private TextMeshProUGUI CreateLabel(
            string objectName,
            Transform parent,
            string value,
            float fontSize,
            Color color,
            TextAlignmentOptions alignment,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 anchoredPosition,
            Vector2 sizeDelta)
        {
            GameObject labelObject = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            RectTransform rect = labelObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot =
                (anchorMax - anchorMin).sqrMagnitude < 0.001f
                    ? anchorMin
                    : new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;

            TextMeshProUGUI label =
                labelObject.GetComponent<TextMeshProUGUI>();
            label.text = value;
            label.fontSize = fontSize;
            label.enableAutoSizing = true;
            label.fontSizeMin = Mathf.Max(12f, fontSize - 5f);
            label.fontSizeMax = fontSize;
            label.alignment = alignment;
            label.color = color;
            label.raycastTarget = false;
            if (chatLogText != null && chatLogText.font != null)
            {
                label.font = chatLogText.font;
            }
            return label;
        }

        private void StyleButton(Button button, Color backgroundColor)
        {
            if (button == null)
            {
                return;
            }

            Image image = button.GetComponent<Image>();
            if (image == null)
            {
                image = button.gameObject.AddComponent<Image>();
            }
            image.sprite = GetRoundedPanelSprite();
            image.type = Image.Type.Sliced;
            image.color = backgroundColor;
            button.targetGraphic = image;

            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.9f, 1f, 1f, 1f);
            colors.pressedColor = new Color(0.78f, 0.9f, 0.92f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.65f, 0.7f, 0.72f, 0.55f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.text = label.text.ToLowerInvariant();
                label.color = Color.white;
                label.fontStyle = FontStyles.Bold;
                label.enableAutoSizing = true;
                label.fontSizeMin = 13f;
                label.fontSizeMax = 21f;
                label.alignment = TextAlignmentOptions.Center;
            }
        }

        private void StyleInputField(TMP_InputField input)
        {
            if (input == null)
            {
                return;
            }

            Image image = input.GetComponent<Image>();
            if (image != null)
            {
                image.sprite = GetRoundedPanelSprite();
                image.type = Image.Type.Sliced;
                image.color = new Color32(255, 253, 239, 255);
            }

            if (input.textComponent != null)
            {
                input.textComponent.color = new Color32(39, 63, 82, 255);
                input.textComponent.enableAutoSizing = true;
                input.textComponent.fontSizeMin = 15f;
                input.textComponent.fontSizeMax = 21f;
                input.textComponent.alignment =
                    TextAlignmentOptions.MidlineLeft;
            }

            TMP_Text placeholder = input.placeholder as TMP_Text;
            if (placeholder != null)
            {
                if (input == messageInputField)
                {
                    placeholder.text = "message jarvis...";
                }
                else
                {
                    placeholder.text =
                        placeholder.text.ToLowerInvariant();
                }
                placeholder.color = new Color32(91, 119, 128, 180);
                placeholder.fontStyle = FontStyles.Italic;
                placeholder.enableAutoSizing = true;
                placeholder.fontSizeMin = 14f;
                placeholder.fontSizeMax = 20f;
                placeholder.alignment =
                    TextAlignmentOptions.MidlineLeft;
            }

            if (input.textViewport != null)
            {
                input.textViewport.offsetMin = new Vector2(16f, 7f);
                input.textViewport.offsetMax = new Vector2(-16f, -7f);
            }
            input.caretColor = new Color32(52, 168, 179, 255);
            input.selectionColor = new Color32(101, 182, 232, 90);
        }

        private void StyleDropdown(TMP_Dropdown dropdown)
        {
            if (dropdown == null)
            {
                return;
            }

            Image image = dropdown.GetComponent<Image>();
            if (image != null)
            {
                image.sprite = GetRoundedPanelSprite();
                image.type = Image.Type.Sliced;
                image.color = new Color32(255, 253, 239, 255);
            }
            if (dropdown.captionText != null)
            {
                dropdown.captionText.text =
                    dropdown.captionText.text.ToLowerInvariant();
                dropdown.captionText.color =
                    new Color32(39, 63, 82, 255);
                dropdown.captionText.enableAutoSizing = true;
                dropdown.captionText.fontSizeMin = 12f;
                dropdown.captionText.fontSizeMax = 18f;
            }
            if (dropdown.itemText != null)
            {
                dropdown.itemText.text =
                    dropdown.itemText.text.ToLowerInvariant();
                dropdown.itemText.color =
                    new Color32(39, 63, 82, 255);
            }
            foreach (TMP_Dropdown.OptionData option in dropdown.options)
            {
                if (option != null && option.text != null)
                {
                    option.text = option.text.ToLowerInvariant();
                }
            }
        }

        private void StyleToggle(Toggle toggle)
        {
            if (toggle == null)
            {
                return;
            }

            Image track = toggle.targetGraphic as Image;
            if (track == null)
            {
                track = toggle.GetComponentInChildren<Image>(true);
            }
            if (track == null)
            {
                GameObject trackObject = new GameObject(
                    "Background",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image));
                trackObject.transform.SetParent(
                    toggle.transform,
                    false);
                track = trackObject.GetComponent<Image>();
            }

            Graphic previousGraphic = toggle.graphic;
            if (previousGraphic != null &&
                previousGraphic.gameObject != track.gameObject)
            {
                previousGraphic.gameObject.SetActive(false);
            }

            toggle.graphic = null;
            toggle.targetGraphic = track;
            toggle.transition = Selectable.Transition.None;
            track.sprite = GetRoundedPanelSprite();
            track.type = Image.Type.Sliced;

            RectTransform trackRect =
                track.GetComponent<RectTransform>();
            trackRect.anchorMin = new Vector2(0f, 0.5f);
            trackRect.anchorMax = new Vector2(0f, 0.5f);
            trackRect.pivot = new Vector2(0.5f, 0.5f);
            trackRect.anchoredPosition = new Vector2(26f, 0f);
            trackRect.sizeDelta = new Vector2(48f, 26f);

            Transform existingKnob =
                track.transform.Find("JarvisSwitchKnob");
            Image knob;
            if (existingKnob == null)
            {
                GameObject knobObject = new GameObject(
                    "JarvisSwitchKnob",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image),
                    typeof(Shadow));
                knobObject.transform.SetParent(
                    track.transform,
                    false);
                knob = knobObject.GetComponent<Image>();
                Shadow knobShadow =
                    knobObject.GetComponent<Shadow>();
                knobShadow.effectColor =
                    new Color32(39, 78, 92, 45);
                knobShadow.effectDistance =
                    new Vector2(0f, -1f);
            }
            else
            {
                knob = existingKnob.GetComponent<Image>();
            }

            knob.sprite = GetRoundedPanelSprite();
            knob.type = Image.Type.Sliced;
            knob.color = Color.white;
            knob.raycastTarget = false;
            RectTransform knobRect =
                knob.GetComponent<RectTransform>();
            knobRect.anchorMin = new Vector2(0.5f, 0.5f);
            knobRect.anchorMax = new Vector2(0.5f, 0.5f);
            knobRect.pivot = new Vector2(0.5f, 0.5f);
            knobRect.sizeDelta = new Vector2(20f, 20f);

            TMP_Text label = toggle.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.text = label.text.ToLowerInvariant();
                label.color = new Color32(44, 78, 92, 255);
                label.enableAutoSizing = true;
                label.fontSizeMin = 11f;
                label.fontSizeMax = 17f;
                label.alignment = TextAlignmentOptions.MidlineLeft;
                label.rectTransform.anchorMin = Vector2.zero;
                label.rectTransform.anchorMax = Vector2.one;
                label.rectTransform.offsetMin =
                    new Vector2(60f, 0f);
                label.rectTransform.offsetMax =
                    new Vector2(-4f, 0f);
            }

            if (toggleVisualListeners.TryGetValue(
                    toggle,
                    out UnityAction<bool> oldListener))
            {
                toggle.onValueChanged.RemoveListener(oldListener);
            }

            UnityAction<bool> visualListener =
                isOn => UpdateToggleSwitchVisual(
                    track,
                    knobRect,
                    isOn);
            toggleVisualListeners[toggle] = visualListener;
            toggle.onValueChanged.AddListener(visualListener);
            UpdateToggleSwitchVisual(
                track,
                knobRect,
                toggle.isOn);
        }

        private static void UpdateToggleSwitchVisual(
            Image track,
            RectTransform knob,
            bool isOn)
        {
            track.color = isOn
                ? new Color32(65, 196, 180, 255)
                : new Color32(215, 226, 221, 255);
            knob.anchoredPosition =
                new Vector2(isOn ? 10f : -10f, 0f);
        }

        private static void SetToggleText(
            Toggle toggle,
            string value)
        {
            if (toggle == null)
            {
                return;
            }

            TMP_Text label =
                toggle.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.text = value;
            }
        }

        private static void EnsureLayoutElement(
            GameObject target,
            float preferredHeight)
        {
            LayoutElement element = target.GetComponent<LayoutElement>();
            if (element == null)
            {
                element = target.AddComponent<LayoutElement>();
            }
            element.preferredHeight = preferredHeight;
            element.minHeight = preferredHeight;
            element.flexibleWidth = 1f;
        }

        private void SetButtonText(Button button, string value)
        {
            if (button == null)
            {
                return;
            }
            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.text = value.ToLowerInvariant();
            }
        }

        private void ApplySafeArea()
        {
            if (safeAreaRoot == null ||
                Screen.width <= 0 ||
                Screen.height <= 0)
            {
                return;
            }

            Rect safeArea = Screen.safeArea;
            safeAreaRoot.anchorMin = new Vector2(
                safeArea.xMin / Screen.width,
                safeArea.yMin / Screen.height);
            safeAreaRoot.anchorMax = new Vector2(
                safeArea.xMax / Screen.width,
                safeArea.yMax / Screen.height);
            safeAreaRoot.offsetMin = Vector2.zero;
            safeAreaRoot.offsetMax = Vector2.zero;

            ApplyResponsiveLayout();
            lastScreenWidth = Screen.width;
            lastScreenHeight = Screen.height;
        }

        private void ApplyResponsiveLayout()
        {
            bool isPortrait = Screen.height > Screen.width;

            if (responsiveCanvasScaler != null)
            {
                responsiveCanvasScaler.referenceResolution = isPortrait
                    ? new Vector2(1080f, 1920f)
                    : new Vector2(1920f, 1080f);
                responsiveCanvasScaler.matchWidthOrHeight = 0.5f;
            }

            Canvas.ForceUpdateCanvases();

            if (topControlStack != null)
            {
                topControlStack.anchoredPosition = isPortrait
                    ? new Vector2(-18f, -18f)
                    : new Vector2(-28f, -28f);
                topControlStack.sizeDelta = isTopMenuCollapsed
                    ? new Vector2(54f, 54f)
                    : isPortrait
                        ? new Vector2(224f, 204f)
                        : new Vector2(238f, 210f);
                PositionTopMenuToggle();
            }
            PositionPoseShortcut(isPortrait);
            PositionPosePicker(isPortrait);

            if (chatDock == null)
            {
                return;
            }

            if (isChatDockCollapsed)
            {
                chatDock.anchorMin = new Vector2(1f, 0f);
                chatDock.anchorMax = new Vector2(1f, 0f);
                chatDock.pivot = new Vector2(1f, 0f);
                chatDock.anchoredPosition = new Vector2(
                    isPortrait ? -18f : -24f,
                    isPortrait ? 18f : 24f);
                chatDock.sizeDelta = new Vector2(54f, 54f);
                PositionChatMenuToggle();
                Canvas.ForceUpdateCanvases();
                return;
            }

            if (isChatDockExpanded)
            {
                chatDock.anchorMin = new Vector2(0.03f, 0.04f);
                chatDock.anchorMax = new Vector2(0.97f, 0.94f);
                chatDock.pivot = new Vector2(0.5f, 0.5f);
                chatDock.anchoredPosition = Vector2.zero;
                chatDock.sizeDelta = Vector2.zero;
            }
            else if (!isChatDockFloating)
            {
                chatDock.anchorMin = new Vector2(
                    isPortrait ? 0.04f : 0.07f,
                    0f);
                chatDock.anchorMax = new Vector2(
                    isPortrait ? 0.96f : 0.93f,
                    0f);
                chatDock.pivot = new Vector2(0.5f, 0f);
                chatDock.anchoredPosition = new Vector2(
                    0f,
                    isPortrait ? 18f : 24f);
                chatDock.sizeDelta = new Vector2(
                    0f,
                    isPortrait ? 600f : 272f);
            }

            ApplyChatContentsLayout();
            PositionChatMenuToggle();
            Canvas.ForceUpdateCanvases();
        }

        private void ApplyChatContentsLayout()
        {
            bool isPortrait = Screen.height > Screen.width;

            int controlCount =
                quickActions != null
                    ? Mathf.Max(1, quickActions.childCount)
                    : 1;
            int columnCount = isPortrait
                ? 2
                : controlCount;
            int rowCount = Mathf.CeilToInt(
                controlCount / (float)columnCount);
            float cellHeight = isPortrait ? 48f : 38f;
            float rowSpacing = 10f;
            float actionsHeight =
                (rowCount * cellHeight) +
                ((rowCount - 1) * rowSpacing);

            if (quickActions != null &&
                quickActionsLayout != null)
            {
                float dockWidth = chatDock.rect.width;
                if (dockWidth <= 1f && safeAreaRoot != null)
                {
                    dockWidth =
                        safeAreaRoot.rect.width *
                        (chatDock.anchorMax.x -
                         chatDock.anchorMin.x);
                }
                float actionsWidth = Mathf.Max(
                    240f,
                    dockWidth - 48f);
                float cellWidth =
                    (actionsWidth -
                     ((columnCount - 1) *
                      quickActionsLayout.spacing.x)) /
                    columnCount;

                quickActionsLayout.constraintCount = columnCount;
                quickActionsLayout.cellSize =
                    new Vector2(cellWidth, cellHeight);
                quickActions.offsetMin = new Vector2(24f, 14f);
                quickActions.offsetMax = new Vector2(
                    -24f,
                    14f + actionsHeight);
            }

            float inputBottom = isPortrait
                ? 28f + actionsHeight
                : 64f;
            float inputHeight = isPortrait ? 58f : 52f;

            if (messageInputField != null)
            {
                RectTransform inputRect =
                    messageInputField.GetComponent<RectTransform>();
                inputRect.offsetMin =
                    new Vector2(24f, inputBottom);
                inputRect.offsetMax =
                    new Vector2(-162f, inputBottom + inputHeight);
            }

            if (sendButton != null)
            {
                RectTransform sendRect =
                    sendButton.GetComponent<RectTransform>();
                sendRect.anchoredPosition =
                    new Vector2(-24f, inputBottom);
                sendRect.sizeDelta =
                    new Vector2(126f, inputHeight);
            }

            RectTransform viewportRect =
                chatViewport != null
                    ? chatViewport.GetComponent<RectTransform>()
                    : null;
            if (viewportRect != null)
            {
                viewportRect.offsetMin = new Vector2(
                    24f,
                    inputBottom + inputHeight + 14f);
                viewportRect.offsetMax =
                    new Vector2(-24f, -52f);
            }

            KeepNewestChatTextVisible();
        }

        private void PositionTopMenuToggle()
        {
            if (topMenuToggleButton == null)
            {
                return;
            }

            RectTransform toggleRect =
                topMenuToggleButton.GetComponent<RectTransform>();
            if (isTopMenuCollapsed)
            {
                toggleRect.anchorMin = Vector2.one;
                toggleRect.anchorMax = Vector2.one;
                toggleRect.pivot = Vector2.one;
                toggleRect.anchoredPosition = new Vector2(-7f, -7f);
            }
            else
            {
                toggleRect.anchorMin = new Vector2(0f, 1f);
                toggleRect.anchorMax = new Vector2(0f, 1f);
                toggleRect.pivot = Vector2.one;
                toggleRect.anchoredPosition = new Vector2(-8f, 0f);
            }
            toggleRect.sizeDelta = new Vector2(40f, 40f);
        }

        private void PositionPoseShortcut(bool isPortrait)
        {
            if (cyclePoseButton == null)
            {
                return;
            }

            float edgeMargin = isPortrait ? 18f : 28f;
            float menuHeight = isTopMenuCollapsed
                ? 54f
                : isPortrait
                    ? 204f
                    : 210f;

            RectTransform poseRect =
                cyclePoseButton.GetComponent<RectTransform>();
            poseRect.anchorMin = Vector2.one;
            poseRect.anchorMax = Vector2.one;
            poseRect.pivot = Vector2.one;
            poseRect.anchoredPosition = new Vector2(
                -edgeMargin,
                -(edgeMargin + menuHeight + 10f));
            poseRect.sizeDelta = new Vector2(40f, 40f);
        }

        private void PositionPosePicker(bool isPortrait)
        {
            if (posePickerPanel != null)
            {
                posePickerPanel.anchorMin =
                    new Vector2(0.5f, 0.5f);
                posePickerPanel.anchorMax =
                    new Vector2(0.5f, 0.5f);
                posePickerPanel.pivot =
                    new Vector2(0.5f, 0.5f);
                posePickerPanel.anchoredPosition =
                    Vector2.zero;
                posePickerPanel.sizeDelta =
                    new Vector2(720f, 510f);
            }

            if (poseSelectionPanel != null)
            {
                poseSelectionPanel.anchorMin =
                    new Vector2(0.5f, 0.5f);
                poseSelectionPanel.anchorMax =
                    new Vector2(0.5f, 0.5f);
                poseSelectionPanel.pivot =
                    new Vector2(0.5f, 0.5f);
                poseSelectionPanel.anchoredPosition =
                    Vector2.zero;
                poseSelectionPanel.sizeDelta =
                    new Vector2(560f, 510f);
            }

            if (memorySettingsPanel != null)
            {
                memorySettingsPanel.anchorMin =
                    new Vector2(0.5f, 0.5f);
                memorySettingsPanel.anchorMax =
                    new Vector2(0.5f, 0.5f);
                memorySettingsPanel.pivot =
                    new Vector2(0.5f, 0.5f);
                memorySettingsPanel.anchoredPosition =
                    Vector2.zero;
                memorySettingsPanel.sizeDelta =
                    new Vector2(760f, 610f);
            }
        }

        private void PositionChatMenuToggle()
        {
            if (chatMenuToggleButton == null)
            {
                return;
            }

            RectTransform toggleRect =
                chatMenuToggleButton.GetComponent<RectTransform>();
            toggleRect.anchorMin = Vector2.one;
            toggleRect.anchorMax = Vector2.one;
            toggleRect.pivot = Vector2.one;
            toggleRect.anchoredPosition =
                isChatDockCollapsed
                    ? new Vector2(-7f, -7f)
                    : new Vector2(-10f, -10f);
            toggleRect.sizeDelta = new Vector2(40f, 40f);

            if (chatExpandButton != null)
            {
                RectTransform expandRect =
                    chatExpandButton.GetComponent<RectTransform>();
                expandRect.anchorMin = Vector2.one;
                expandRect.anchorMax = Vector2.one;
                expandRect.pivot = Vector2.one;
                expandRect.anchoredPosition =
                    new Vector2(-58f, -10f);
                expandRect.sizeDelta = new Vector2(40f, 40f);
            }

            if (chatClearButton != null)
            {
                RectTransform clearRect =
                    chatClearButton.GetComponent<RectTransform>();
                clearRect.anchorMin = Vector2.one;
                clearRect.anchorMax = Vector2.one;
                clearRect.pivot = Vector2.one;
                clearRect.anchoredPosition =
                    new Vector2(-106f, -10f);
                clearRect.sizeDelta = new Vector2(40f, 40f);
            }

            if (chatDragHandle != null)
            {
                chatDragHandle.anchorMin = new Vector2(0f, 1f);
                chatDragHandle.anchorMax = new Vector2(1f, 1f);
                chatDragHandle.pivot = new Vector2(0.5f, 1f);
                chatDragHandle.anchoredPosition = Vector2.zero;
                chatDragHandle.sizeDelta = new Vector2(0f, 52f);
            }

            if (chatResizeHandle != null)
            {
                chatResizeHandle.anchorMin = Vector2.one;
                chatResizeHandle.anchorMax = Vector2.one;
                chatResizeHandle.pivot = Vector2.one;
                chatResizeHandle.anchoredPosition =
                    new Vector2(-154f, -18f);
                chatResizeHandle.sizeDelta = new Vector2(24f, 24f);
            }
        }

        private static Sprite GetRoundedPanelSprite()
        {
            if (roundedPanelSprite != null)
            {
                return roundedPanelSprite;
            }

            const int size = 64;
            const float radius = 14f;
            float center = (size - 1) * 0.5f;
            float inner = center - radius;
            Color[] pixels = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Max(Mathf.Abs(x - center) - inner, 0f);
                    float dy = Mathf.Max(Mathf.Abs(y - center) - inner, 0f);
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01(radius + 0.75f - distance);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            Texture2D texture =
                new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = "JarvisRoundedPanelTexture";
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            texture.SetPixels(pixels);
            texture.Apply();
            texture.hideFlags = HideFlags.DontSave;

            roundedPanelSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(16f, 16f, 16f, 16f));
            roundedPanelSprite.name = "JarvisRoundedPanelSprite";
            roundedPanelSprite.hideFlags = HideFlags.DontSave;
            return roundedPanelSprite;
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

            DeferDropdownActivation(dropdownObject);
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
                Canvas[] canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude);
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
            label.text = "next pose";
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

        private TMPro.TMP_Dropdown CreateRuntimePoseDropdown()
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
                Canvas[] canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude);
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

            // Create using TMP_DefaultControls
            var resources = new TMPro.TMP_DefaultControls.Resources();
            GameObject dropdownGo = TMPro.TMP_DefaultControls.CreateDropdown(resources);
            dropdownGo.name = "PoseDropdown";
            
            var dropdownRect = dropdownGo.GetComponent<RectTransform>();
            dropdownRect.SetParent(parent, false);

            // Position it nicely.
            if (importVrmButton != null)
            {
                RectTransform importRect = importVrmButton.GetComponent<RectTransform>();
                if (importRect != null)
                {
                    dropdownRect.anchorMin = importRect.anchorMin;
                    dropdownRect.anchorMax = importRect.anchorMax;
                    dropdownRect.pivot = importRect.pivot;
                    dropdownRect.sizeDelta = new Vector2(180f, 42f);
                    // Place it nicely below the "Next Pose" button which is 50 units below importVrmButton.
                    dropdownRect.anchoredPosition = importRect.anchoredPosition + new Vector2(0f, -100f);
                }
            }
            else
            {
                dropdownRect.anchorMin = new Vector2(0f, 0f);
                dropdownRect.anchorMax = new Vector2(0f, 0f);
                dropdownRect.pivot = new Vector2(0f, 0f);
                dropdownRect.sizeDelta = new Vector2(180f, 42f);
                dropdownRect.anchoredPosition = new Vector2(16f, 70f);
            }

            var dropdown = dropdownGo.GetComponent<TMPro.TMP_Dropdown>();
            
            // Set up font styling matching importVrmButton if possible
            if (importVrmButton != null)
            {
                var label = dropdownGo.transform.Find("Label")?.GetComponent<TMPro.TMP_Text>();
                var importLabel = importVrmButton.GetComponentInChildren<TMPro.TMP_Text>(true);
                if (label != null && importLabel != null)
                {
                    label.font = importLabel.font;
                    label.fontMaterial = importLabel.fontMaterial;
                    label.fontSize = importLabel.fontSize;
                    label.color = importLabel.color;
                    label.alignment = TMPro.TextAlignmentOptions.Center;
                }
            }

            DeferDropdownActivation(dropdownGo);
            return dropdown;
        }

        private void DeferDropdownActivation(GameObject dropdownObject)
        {
            dropdownObject.SetActive(false);
            StartCoroutine(ActivateDropdownNextFrame(dropdownObject));
        }

        private IEnumerator ActivateDropdownNextFrame(GameObject dropdownObject)
        {
            yield return null;
            if (dropdownObject != null)
            {
                dropdownObject.SetActive(true);

                // TMP_DefaultControls can create a dropdown during another
                // component's Start before TMP_Dropdown.Start is scheduled.
                // Ensure its private tween runner exists before the first click.
                TMP_Dropdown dropdown =
                    dropdownObject.GetComponent<TMP_Dropdown>();
                MethodInfo startMethod = typeof(TMP_Dropdown).GetMethod(
                    "Start",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                startMethod?.Invoke(dropdown, null);
            }
        }

        private void Update()
        {
            if (lastScreenWidth != Screen.width ||
                lastScreenHeight != Screen.height)
            {
                ApplySafeArea();
            }

            UnityEngine.InputSystem.Keyboard keyboard =
                UnityEngine.InputSystem.Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (!IsInputFocused && keyboard.pKey.wasPressedThisFrame)
            {
                OnCyclePoseRequested?.Invoke();
            }

            bool shortcutModifier =
                keyboard.leftCtrlKey.isPressed ||
                keyboard.rightCtrlKey.isPressed ||
                keyboard.leftMetaKey.isPressed ||
                keyboard.rightMetaKey.isPressed;

            if (!IsInputFocused &&
                shortcutModifier &&
                keyboard.sKey.wasPressedThisFrame)
            {
                OnSaveFrameRequested?.Invoke();
            }

            if (!IsInputFocused &&
                shortcutModifier &&
                keyboard.lKey.wasPressedThisFrame)
            {
                OnLoadFrameRequested?.Invoke();
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

            if (GUI.Button(PoseFallbackRect, "next pose"))
            {
                OnCyclePoseRequested?.Invoke();
            }
        }

        public void PopulatePoseDropdown(System.Collections.Generic.List<string> options)
        {
            poseInternalNames.Clear();
            poseDisplayNames.Clear();
            poseRotationEnabled.Clear();
            foreach (string option in options)
            {
                poseInternalNames.Add(option);
                poseDisplayNames.Add(GetFriendlyPoseName(option));
                poseRotationEnabled.Add(
                    PlayerPrefs.GetInt(
                        GetPoseRotationKey(option),
                        1) != 0);
            }

            if (poseDropdown != null)
            {
                poseDropdown.ClearOptions();
                poseDropdown.AddOptions(poseDisplayNames);
            }

            RebuildPoseRows();
            int selectedIndex =
                poseDropdown != null ? poseDropdown.value : 0;
            UpdatePoseMenuLabel(selectedIndex);
        }

        public void SetSelectedPoseInDropdown(int index)
        {
            if (poseDropdown != null)
            {
                poseDropdown.SetValueWithoutNotify(index);
            }
            UpdatePoseMenuLabel(index);
        }

        public bool IsPoseEnabledForRotation(int index)
        {
            if (poseRotationEnabled.Count == 0)
            {
                return true;
            }
            return index < 0 ||
                index >= poseRotationEnabled.Count ||
                poseRotationEnabled[index];
        }

        public string GetPoseDisplayName(string poseName)
        {
            return GetFriendlyPoseName(poseName);
        }

        private void UpdatePoseMenuLabel(int index)
        {
            if (poseMenuButton == null ||
                index < 0 ||
                index >= poseDisplayNames.Count)
            {
                return;
            }

            SetButtonText(
                poseMenuButton,
                poseDisplayNames[index]);
            TMP_Text label =
                poseMenuButton.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.color = new Color32(44, 78, 92, 255);
            }
        }

        private static string GetFriendlyPoseName(
            string poseName)
        {
            if (string.IsNullOrWhiteSpace(poseName))
            {
                return "pose";
            }

            if (FriendlyPoseNames.TryGetValue(
                    poseName,
                    out string friendlyName))
            {
                return friendlyName;
            }

            return poseName
                .Replace("_", " ")
                .Replace("-", " ")
                .ToLowerInvariant();
        }

        private static string GetPoseRotationKey(
            string poseName)
        {
            return "jarvis.pose-rotation." +
                poseName.ToLowerInvariant();
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
            if (memorySettingsPanel != null)
            {
                PopulateMemorySettingsUI();
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
            SaveMemorySettings();
            currentConfig.Save();

            if (posePickerPanel != null)
            {
                posePickerPanel.gameObject.SetActive(false);
            }
            if (poseSelectionPanel != null)
            {
                poseSelectionPanel.gameObject.SetActive(false);
            }
            if (memorySettingsPanel != null)
            {
                memorySettingsPanel.gameObject.SetActive(false);
            }
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
            string color = sender == "AI"
                ? "#E76F8B"
                : (sender == "System" ? "#279D91" : "#457B9D");
            string displaySender = sender.ToLowerInvariant();
            chatLines.Add(
                $"<b><color={color}>{displaySender}:</color></b> {message}");
            TrimChatHistory();
            RefreshChatLog();
        }

        public void StartAISpeechTyping(string fullMessage, float durationSeconds)
        {
            if (aiTypingCoroutine != null)
            {
                StopCoroutine(aiTypingCoroutine);
            }

            activeAiLineIndex = chatLines.Count;
            chatLines.Add("<b><color=#E76F8B>ai:</color></b> ");
            TrimChatHistory();
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
                chatLines[activeAiLineIndex] =
                    $"<b><color=#E76F8B>ai:</color></b> {finalMessage}";
                activeAiLineIndex = -1;
                RefreshChatLog();
            }
        }

        private void ClearChatDisplay()
        {
            if (aiTypingCoroutine != null)
            {
                StopCoroutine(aiTypingCoroutine);
                aiTypingCoroutine = null;
            }
            activeAiLineIndex = -1;
            chatLines.Clear();
            RefreshChatLog();
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
                    chatLines[activeAiLineIndex] =
                        $"<b><color=#E76F8B>ai:</color></b> {typed}";
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
            int firstLine = Mathf.Max(
                0,
                chatLines.Count - MaxVisibleChatLines);
            for (int i = firstLine; i < chatLines.Count; i++)
            {
                if (sb.Length > 0)
                {
                    sb.AppendLine();
                }
                sb.Append(chatLines[i]);
            }
            chatLogText.text = sb.ToString();
            KeepNewestChatTextVisible();
        }

        private void KeepNewestChatTextVisible()
        {
            if (chatLogText == null ||
                chatViewport == null)
            {
                return;
            }

            RectTransform viewportRect =
                chatViewport.GetComponent<RectTransform>();
            RectTransform textRect =
                chatLogText.rectTransform;
            chatLogText.ForceMeshUpdate();

            float viewportHeight =
                Mathf.Max(1f, viewportRect.rect.height);
            float contentHeight =
                Mathf.Max(
                    viewportHeight,
                    chatLogText.preferredHeight + 6f);

            textRect.anchorMin = new Vector2(0f, 0f);
            textRect.anchorMax = new Vector2(1f, 0f);
            textRect.pivot = new Vector2(0.5f, 0f);
            textRect.anchoredPosition = Vector2.zero;
            textRect.sizeDelta =
                new Vector2(0f, contentHeight);
        }

        private void TrimChatHistory()
        {
            while (chatLines.Count > MaxRetainedChatLines)
            {
                chatLines.RemoveAt(0);
                if (activeAiLineIndex >= 0)
                {
                    activeAiLineIndex--;
                }
            }
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
                connectionStatusText.text = currentConfig.isLocalMode
                    ? "local mode (ollama)"
                    : "cloud mode (online api)";
            }
        }
    }
}
