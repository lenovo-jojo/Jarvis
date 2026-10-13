using System.Collections;
using System.Collections.Generic;
using AICompanion.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace AICompanion.Services
{
    /// <summary>
    /// Opens a small town-specific chat when the embodied Jojo avatar is
    /// clicked. It shares JARVIS settings and memory without booting the main
    /// scene controller (which would load a duplicate set of avatars).
    /// </summary>
    public sealed class PossumSpringsConversationController :
        MonoBehaviour
    {
        private sealed class TownCharacter
        {
            public string id;
            public Transform root;
            public System.Action<string, bool>
                speechHandler;
            public System.Action summonHandler;
            public System.Action lookAtPlayerHandler;
        }

        private readonly Dictionary<string, TownCharacter>
            characters =
                new Dictionary<string, TownCharacter>(
                    System.StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> greetedCharacters =
            new HashSet<string>(
                System.StringComparer.OrdinalIgnoreCase);
        private readonly List<string> chatHistory =
            new List<string>();
        private VRMService vrmService;
        private LLMService llmService;
        private PossumSpringsMiniMap miniMap;
        private GameObject panel;
        private Text chatLog;
        private ScrollRect chatScroll;
        private RectTransform chatContent;
        private InputField messageInput;
        private Text inputPlaceholder;
        private Button sendButton;
        private GameObject phonePanel;
        private bool requestPending;
        private bool initialWhistleTriggered;
        private string activeCharacterId = "jojo";

        public void Initialize(
            Transform jojoTransform,
            VRMService activeVrmService,
            PossumSpringsMiniMap activeMiniMap)
        {
            vrmService = activeVrmService;
            miniMap = activeMiniMap;
            llmService = gameObject.AddComponent<LLMService>();
            EnsureEventSystem();
            BuildInterface();
            RegisterCharacter(
                "jojo",
                jojoTransform,
                null);
        }

        public void RegisterCharacter(
            string characterId,
            Transform characterRoot,
            System.Action<string, bool> speechHandler,
            System.Action summonHandler = null,
            System.Action lookAtPlayerHandler = null)
        {
            if (string.IsNullOrWhiteSpace(characterId) ||
                characterRoot == null)
            {
                return;
            }

            string normalized =
                characterId.Trim().ToLowerInvariant();
            characters[normalized] =
                new TownCharacter
                {
                    id = normalized,
                    root = characterRoot,
                    speechHandler = speechHandler,
                    summonHandler = summonHandler,
                    lookAtPlayerHandler =
                        lookAtPlayerHandler
                };
            if (!initialWhistleTriggered &&
                normalized == "mae")
            {
                initialWhistleTriggered = true;
                StartCoroutine(
                    WhistleAfterTownCastLoads());
            }
        }

        private IEnumerator WhistleAfterTownCastLoads()
        {
            yield return null;
            WhistleForAttention();
        }

        private void Update()
        {
            if (characters.Count == 0 ||
                Mouse.current == null ||
                !Mouse.current.leftButton.wasReleasedThisFrame ||
                (panel != null && panel.activeSelf) ||
                (miniMap != null && miniMap.IsFullMapOpen) ||
                (EventSystem.current != null &&
                 EventSystem.current.IsPointerOverGameObject()))
            {
                return;
            }

            Camera activeCamera = Camera.main;
            if (activeCamera == null)
            {
                return;
            }

            Ray ray = activeCamera.ScreenPointToRay(
                Mouse.current.position.ReadValue());
            RaycastHit[] hits = Physics.RaycastAll(
                ray,
                Mathf.Infinity,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Collide);
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider == null)
                {
                    continue;
                }

                foreach (TownCharacter character in
                    characters.Values)
                {
                    if (character.root != null &&
                        hit.collider.transform.IsChildOf(
                            character.root))
                    {
                        OpenConversation(character.id);
                        return;
                    }
                }
            }
        }

        private void OpenConversation(string characterId)
        {
            if (panel == null)
            {
                return;
            }

            activeCharacterId = characterId;
            panel.SetActive(true);
            SetConversationContext();
            if (characterId == "jojo")
            {
                vrmService
                    ?.FocusSelectedCharacterForConversation();
            }
            if (inputPlaceholder != null)
            {
                inputPlaceholder.text =
                    $"message {characterId}...";
            }
            if (greetedCharacters.Add(characterId))
            {
                string greeting =
                    GetGreeting(characterId);
                AppendLine(characterId, greeting);
                CompanionMemoryService.Instance
                    .RecordAssistantMessage(
                        $"{characterId}: {greeting}",
                        ServiceConfig.Load());
                ShowCharacterSpeech(
                    characterId,
                    greeting);
            }
            messageInput?.ActivateInputField();
        }

        private void SendMessage()
        {
            if (requestPending ||
                messageInput == null ||
                string.IsNullOrWhiteSpace(messageInput.text))
            {
                return;
            }

            string message = messageInput.text.Trim();
            messageInput.text = string.Empty;
            AppendLine("me", message);
            SetConversationContext();
            requestPending = true;
            SetControlsInteractable(false);
            StartCoroutine(
                llmService.SendMessageCoroutine(
                    message,
                    null,
                    null,
                    onComplete: HandleReply,
                    onError: HandleError));
        }

        private void HandleReply(string reply)
        {
            requestPending = false;
            SetControlsInteractable(true);
            string displayReply =
                string.IsNullOrWhiteSpace(reply)
                    ? GetFallbackReply(
                        activeCharacterId)
                    : reply.Trim();
            AppendReply(displayReply);
            messageInput?.ActivateInputField();
        }

        private void HandleError(string error)
        {
            requestPending = false;
            SetControlsInteractable(true);
            AppendLine(
                "system",
                string.IsNullOrWhiteSpace(error)
                    ? $"{activeCharacterId} couldn't reach " +
                      "the shared mind."
                    : error);
            messageInput?.ActivateInputField();
        }

        private void SetConversationContext()
        {
            CompanionMemoryService.Instance
                .SetConversationContext(
                    activeCharacterId,
                    GetVisibleCharacterIds());
        }

        private List<string> GetVisibleCharacterIds()
        {
            List<string> visible =
                new List<string>();
            Camera activeCamera = Camera.main;
            Plane[] planes =
                activeCamera != null
                    ? GeometryUtility
                        .CalculateFrustumPlanes(
                            activeCamera)
                    : null;
            foreach (TownCharacter character in
                characters.Values)
            {
                if (character.root == null)
                {
                    continue;
                }

                bool inFrame = planes == null;
                foreach (Renderer renderer in
                    character.root
                        .GetComponentsInChildren<
                            Renderer>(true))
                {
                    if (renderer.enabled &&
                        (planes == null ||
                         GeometryUtility.TestPlanesAABB(
                             planes,
                             renderer.bounds)))
                    {
                        inFrame = true;
                        break;
                    }
                }
                if (inFrame)
                {
                    visible.Add(character.id);
                }
            }
            if (!visible.Contains(activeCharacterId))
            {
                visible.Add(activeCharacterId);
            }
            return visible;
        }

        private void AppendReply(string reply)
        {
            string[] lines =
                reply.Split(
                    new[] { '\r', '\n' },
                    System.StringSplitOptions
                        .RemoveEmptyEntries);
            bool foundSpeaker = false;
            foreach (string rawLine in lines)
            {
                string line = rawLine.Trim();
                int separator = line.IndexOf(':');
                if (separator <= 0)
                {
                    continue;
                }

                string speaker =
                    line.Substring(0, separator)
                        .Trim()
                        .ToLowerInvariant();
                string message =
                    line.Substring(separator + 1)
                        .Trim();
                if (!characters.ContainsKey(speaker) ||
                    string.IsNullOrEmpty(message))
                {
                    continue;
                }

                foundSpeaker = true;
                AppendLine(speaker, message);
                ShowCharacterSpeech(
                    speaker,
                    message);
            }

            if (!foundSpeaker)
            {
                AppendLine(
                    activeCharacterId,
                    reply);
                ShowCharacterSpeech(
                    activeCharacterId,
                    reply);
            }
        }

        private void ShowCharacterSpeech(
            string characterId,
            string message)
        {
            if (characters.TryGetValue(
                    characterId,
                    out TownCharacter character) &&
                character.speechHandler != null)
            {
                character.speechHandler(
                    message,
                    true);
            }
            else
            {
                vrmService?.SetCharacterSpeech(
                    characterId,
                    message,
                    true);
            }
            StartCoroutine(
                ClearCharacterSpeechAfter(
                    characterId,
                    Mathf.Clamp(
                        message.Length * 0.045f,
                        2.2f,
                        7f)));
        }

        private IEnumerator ClearCharacterSpeechAfter(
            string characterId,
            float delay)
        {
            yield return new WaitForSeconds(delay);
            if (characters.TryGetValue(
                    characterId,
                    out TownCharacter character) &&
                character.speechHandler != null)
            {
                character.speechHandler(
                    string.Empty,
                    false);
            }
            else
            {
                vrmService?.SetCharacterSpeech(
                    characterId,
                    string.Empty,
                    false);
            }
        }

        private static string GetGreeting(
            string characterId)
        {
            return characterId == "jojo"
                ? "hey twin! what's up?"
                : characterId == "mae"
                    ? "oh hey. you new around here?"
                    : "hey, what's up?";
        }

        private static string GetFallbackReply(
            string characterId)
        {
            return characterId == "jojo"
                ? "i'm here, twin."
                : "i'm here.";
        }

        private void SetControlsInteractable(bool value)
        {
            if (sendButton != null)
            {
                sendButton.interactable = value;
            }
            if (messageInput != null)
            {
                messageInput.interactable = value;
            }
        }

        private void AppendLine(
            string speaker,
            string message)
        {
            AppendRawLine($"{speaker}: {message}");
        }

        private void AppendRawLine(string line)
        {
            if (chatLog == null)
            {
                return;
            }

            chatHistory.Add(line);
            chatLog.text =
                string.Join("\n", chatHistory);
            if (chatScroll != null)
            {
                StartCoroutine(
                    ScrollChatToBottom());
            }
        }

        private IEnumerator ScrollChatToBottom()
        {
            yield return null;
            Canvas.ForceUpdateCanvases();
            if (chatContent != null &&
                chatScroll.viewport != null)
            {
                float viewportHeight =
                    chatScroll.viewport.rect.height;
                float viewportWidth =
                    Mathf.Max(
                        1f,
                        chatScroll.viewport.rect.width);
                TextGenerationSettings settings =
                    chatLog.GetGenerationSettings(
                        new Vector2(
                            viewportWidth,
                            100000f));
                float preferredHeight =
                    chatLog.cachedTextGeneratorForLayout
                        .GetPreferredHeight(
                            chatLog.text,
                            settings) /
                    chatLog.pixelsPerUnit;
                chatContent.sizeDelta =
                    new Vector2(
                        0f,
                        Mathf.Max(
                            viewportHeight,
                            preferredHeight + 8f));
                LayoutRebuilder.ForceRebuildLayoutImmediate(
                    chatContent);
            }
            chatScroll.verticalNormalizedPosition = 0f;
        }

        private void BuildInterface()
        {
            Font font =
                Resources.GetBuiltinResource<Font>(
                    "LegacyRuntime.ttf");
            GameObject canvasObject =
                new GameObject(
                    "PossumSpringsConversationUI",
                    typeof(RectTransform),
                    typeof(Canvas),
                    typeof(CanvasScaler),
                    typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(
                transform,
                false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 90;
            CanvasScaler scaler =
                canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode =
                CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution =
                new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            panel = CreateImage(
                "JojoConversationPanel",
                canvasObject.transform,
                new Color(0.91f, 0.98f, 0.96f, 0.96f));
            RectTransform panelRect =
                panel.GetComponent<RectTransform>();
            panelRect.anchorMin =
                new Vector2(0.5f, 0f);
            panelRect.anchorMax =
                new Vector2(0.5f, 0f);
            panelRect.pivot =
                new Vector2(0.5f, 0f);
            panelRect.anchoredPosition =
                new Vector2(0f, 28f);
            panelRect.sizeDelta =
                new Vector2(980f, 250f);

            GameObject chatViewport = CreateImage(
                "ConversationViewport",
                panel.transform,
                new Color(1f, 1f, 1f, 0f));
            RectTransform viewportRect =
                chatViewport.GetComponent<RectTransform>();
            SetRect(
                viewportRect,
                new Vector2(28f, 82f),
                new Vector2(-28f, -24f));
            chatViewport.AddComponent<RectMask2D>();
            chatScroll =
                chatViewport.AddComponent<ScrollRect>();
            chatScroll.viewport = viewportRect;
            chatScroll.horizontal = false;
            chatScroll.vertical = true;
            chatScroll.movementType =
                ScrollRect.MovementType.Clamped;
            chatScroll.inertia = true;
            chatScroll.scrollSensitivity = 32f;

            GameObject contentObject =
                new GameObject(
                    "ConversationContent",
                    typeof(RectTransform));
            contentObject.transform.SetParent(
                chatViewport.transform,
                false);
            chatContent =
                contentObject.GetComponent<RectTransform>();
            chatContent.anchorMin =
                new Vector2(0f, 1f);
            chatContent.anchorMax =
                new Vector2(1f, 1f);
            chatContent.pivot =
                new Vector2(0.5f, 1f);
            chatContent.anchoredPosition = Vector2.zero;
            chatContent.sizeDelta =
                new Vector2(0f, 144f);

            chatLog = CreateText(
                "ConversationLog",
                contentObject.transform,
                font,
                24,
                new Color(0.16f, 0.27f, 0.3f, 1f));
            RectTransform logRect =
                chatLog.GetComponent<RectTransform>();
            SetRect(
                logRect,
                Vector2.zero,
                Vector2.zero);
            chatScroll.content = chatContent;
            chatLog.alignment = TextAnchor.UpperLeft;
            chatLog.horizontalOverflow =
                HorizontalWrapMode.Wrap;
            chatLog.verticalOverflow =
                VerticalWrapMode.Overflow;

            messageInput = CreateInput(
                panel.transform,
                font,
                out inputPlaceholder);
            RectTransform inputRect =
                messageInput.GetComponent<RectTransform>();
            inputRect.anchorMin =
                new Vector2(0f, 0f);
            inputRect.anchorMax =
                new Vector2(1f, 0f);
            inputRect.pivot =
                new Vector2(0.5f, 0f);
            inputRect.offsetMin =
                new Vector2(28f, 22f);
            inputRect.offsetMax =
                new Vector2(-170f, 72f);
            messageInput.onEndEdit.AddListener(
                value =>
                {
                    if (Keyboard.current != null &&
                        Keyboard.current.enterKey.wasPressedThisFrame)
                    {
                        SendMessage();
                    }
                });

            sendButton = CreateButton(
                "Send",
                panel.transform,
                font,
                "send",
                new Color(1f, 0.47f, 0.4f, 1f));
            RectTransform sendRect =
                sendButton.GetComponent<RectTransform>();
            sendRect.anchorMin =
                new Vector2(1f, 0f);
            sendRect.anchorMax =
                new Vector2(1f, 0f);
            sendRect.pivot =
                new Vector2(1f, 0f);
            sendRect.anchoredPosition =
                new Vector2(-28f, 22f);
            sendRect.sizeDelta =
                new Vector2(130f, 50f);
            sendButton.onClick.AddListener(SendMessage);

            Button closeButton = CreateButton(
                "Close",
                panel.transform,
                font,
                "x",
                new Color(0.27f, 0.75f, 0.7f, 1f));
            RectTransform closeRect =
                closeButton.GetComponent<RectTransform>();
            closeRect.anchorMin = Vector2.one;
            closeRect.anchorMax = Vector2.one;
            closeRect.pivot = Vector2.one;
            closeRect.anchoredPosition =
                new Vector2(-12f, -12f);
            closeRect.sizeDelta =
                new Vector2(42f, 42f);
            closeButton.onClick.AddListener(
                () => panel.SetActive(false));

            BuildPhoneInterface(
                canvasObject.transform,
                font);
            panel.SetActive(false);
        }

        private void BuildPhoneInterface(
            Transform canvasRoot,
            Font font)
        {
            Button phoneButton = CreateButton(
                "TownPhoneButton",
                canvasRoot,
                font,
                "phone",
                new Color(0.35f, 0.68f, 0.9f, 1f));
            RectTransform phoneRect =
                phoneButton.GetComponent<RectTransform>();
            phoneRect.anchorMin = Vector2.zero;
            phoneRect.anchorMax = Vector2.zero;
            phoneRect.pivot = Vector2.zero;
            phoneRect.anchoredPosition =
                new Vector2(28f, 28f);
            phoneRect.sizeDelta =
                new Vector2(180f, 52f);

            Button whistleButton = CreateButton(
                "TownWhistleButton",
                canvasRoot,
                font,
                "whistle",
                new Color(0.92f, 0.56f, 0.72f, 1f));
            RectTransform whistleRect =
                whistleButton.GetComponent<RectTransform>();
            whistleRect.anchorMin = Vector2.zero;
            whistleRect.anchorMax = Vector2.zero;
            whistleRect.pivot = Vector2.zero;
            whistleRect.anchoredPosition =
                new Vector2(220f, 28f);
            whistleRect.sizeDelta =
                new Vector2(180f, 52f);
            whistleButton.onClick.AddListener(
                WhistleForAttention);

            phonePanel = CreateImage(
                "TownPhonePanel",
                canvasRoot,
                new Color(0.91f, 0.98f, 0.96f, 0.98f));
            RectTransform phonePanelRect =
                phonePanel.GetComponent<RectTransform>();
            phonePanelRect.anchorMin = Vector2.zero;
            phonePanelRect.anchorMax = Vector2.zero;
            phonePanelRect.pivot = Vector2.zero;
            phonePanelRect.anchoredPosition =
                new Vector2(28f, 90f);
            phonePanelRect.sizeDelta =
                new Vector2(260f, 116f);

            Text title = CreateText(
                "PhoneTitle",
                phonePanel.transform,
                font,
                22,
                new Color(0.16f, 0.27f, 0.3f, 1f));
            RectTransform titleRect =
                title.GetComponent<RectTransform>();
            SetRect(
                titleRect,
                new Vector2(16f, 66f),
                new Vector2(-16f, -10f));
            title.alignment = TextAnchor.MiddleLeft;
            title.text = "who should join you?";

            Button callMaeButton = CreateButton(
                "CallMaeButton",
                phonePanel.transform,
                font,
                "call mae",
                new Color(1f, 0.56f, 0.7f, 1f));
            RectTransform callMaeRect =
                callMaeButton.GetComponent<RectTransform>();
            callMaeRect.anchorMin = Vector2.zero;
            callMaeRect.anchorMax = Vector2.one;
            callMaeRect.offsetMin =
                new Vector2(16f, 14f);
            callMaeRect.offsetMax =
                new Vector2(-16f, -56f);
            callMaeButton.onClick.AddListener(
                () =>
                {
                    if (characters.TryGetValue(
                            "mae",
                            out TownCharacter mae))
                    {
                        mae.summonHandler?.Invoke();
                    }
                    phonePanel.SetActive(false);
                });

            phoneButton.onClick.AddListener(
                () =>
                    phonePanel.SetActive(
                        !phonePanel.activeSelf));
            phonePanel.SetActive(false);
        }

        private void WhistleForAttention()
        {
            vrmService?.WhistleForCamera();
            foreach (TownCharacter character in
                characters.Values)
            {
                character.lookAtPlayerHandler
                    ?.Invoke();
            }
        }

        private static InputField CreateInput(
            Transform parent,
            Font font,
            out Text placeholder)
        {
            GameObject root = CreateImage(
                "MessageInput",
                parent,
                new Color(1f, 0.98f, 0.9f, 1f));
            InputField input =
                root.AddComponent<InputField>();
            Text valueText = CreateText(
                "Text",
                root.transform,
                font,
                23,
                new Color(0.16f, 0.27f, 0.3f, 1f));
            SetRect(
                valueText.GetComponent<RectTransform>(),
                new Vector2(18f, 5f),
                new Vector2(-18f, -5f));
            valueText.alignment =
                TextAnchor.MiddleLeft;
            placeholder = CreateText(
                "Placeholder",
                root.transform,
                font,
                23,
                new Color(0.35f, 0.45f, 0.47f, 0.6f));
            SetRect(
                placeholder.GetComponent<RectTransform>(),
                new Vector2(18f, 5f),
                new Vector2(-18f, -5f));
            placeholder.text = "message jojo...";
            placeholder.fontStyle = FontStyle.Italic;
            placeholder.alignment =
                TextAnchor.MiddleLeft;
            input.textComponent = valueText;
            input.placeholder = placeholder;
            input.lineType =
                InputField.LineType.SingleLine;
            return input;
        }

        private static Button CreateButton(
            string objectName,
            Transform parent,
            Font font,
            string label,
            Color color)
        {
            GameObject root =
                CreateImage(
                    objectName,
                    parent,
                    color);
            Button button = root.AddComponent<Button>();
            Text text = CreateText(
                "Label",
                root.transform,
                font,
                23,
                Color.white);
            SetRect(
                text.GetComponent<RectTransform>(),
                Vector2.zero,
                Vector2.zero);
            text.alignment = TextAnchor.MiddleCenter;
            text.text = label;
            return button;
        }

        private static GameObject CreateImage(
            string objectName,
            Transform parent,
            Color color)
        {
            GameObject result =
                new GameObject(
                    objectName,
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image));
            result.transform.SetParent(parent, false);
            result.GetComponent<Image>().color = color;
            return result;
        }

        private static Text CreateText(
            string objectName,
            Transform parent,
            Font font,
            int size,
            Color color)
        {
            GameObject result =
                new GameObject(
                    objectName,
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Text));
            result.transform.SetParent(parent, false);
            Text text = result.GetComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.color = color;
            return text;
        }

        private static void SetRect(
            RectTransform rect,
            Vector2 minimumOffset,
            Vector2 maximumOffset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = minimumOffset;
            rect.offsetMax = maximumOffset;
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null)
            {
                return;
            }

            GameObject eventObject =
                new GameObject(
                    "EventSystem",
                    typeof(EventSystem),
                    typeof(InputSystemUIInputModule));
            DontDestroyOnLoad(eventObject);
        }
    }
}
