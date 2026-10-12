using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AICompanion.Services
{
    /// <summary>
    /// A camera-facing, Neocities-inspired pixel speech bubble anchored above
    /// a character.
    /// </summary>
    [DefaultExecutionOrder(17000)]
    public sealed class CharacterSpeechBubble : MonoBehaviour
    {
        private RectTransform bubbleRoot;
        private RectTransform hardShadow;
        private RectTransform outerBlack;
        private RectTransform whiteBorder;
        private RectTransform blackPanel;
        private RectTransform tailBorder;
        private RectTransform tail;
        private TMP_Text messageText;
        private bool bubblesEnabled = true;
        private bool isSpeaking;
        private Transform authoredHead;
        private readonly Vector3[] bubbleCorners = new Vector3[4];
        private Coroutine typingCoroutine;
        private string currentMessage =
            string.Empty;
        private const float MinimumBubbleWidth = 260f;
        private const float MaximumBubbleWidth = 560f;
        private const float MinimumPanelHeight = 98f;
        private const float MaximumPanelHeight = 330f;
        private const float CharacterDelay = 0.028f;

        public void SetMessage(string message)
        {
            currentMessage =
                string.IsNullOrWhiteSpace(message)
                    ? string.Empty
                    : message.Trim();
            if (bubbleRoot == null &&
                currentMessage.Length > 0)
            {
                BuildBubble();
            }
            if (messageText != null)
            {
                messageText.text =
                    currentMessage;
                messageText.maxVisibleCharacters =
                    currentMessage.Length;
                ResizeBubbleToMessage();
            }
            RefreshVisibility();
        }

        public void SetBubbleVisible(bool visible)
        {
            bubblesEnabled = visible;
            RefreshVisibility();
        }

        public void SetSpeaking(
            bool speaking,
            float durationSeconds = 0f)
        {
            isSpeaking = speaking;
            if (isSpeaking &&
                bubbleRoot == null &&
                currentMessage.Length > 0)
            {
                BuildBubble();
            }
            if (isSpeaking)
            {
                // TMP cannot populate its character data while the bubble is
                // inactive. Show it before starting the typewriter pass.
                RefreshVisibility();
                BeginTyping(durationSeconds);
            }
            else
            {
                StopTyping();
                RefreshVisibility();
            }
        }

        private void RefreshVisibility()
        {
            if (bubbleRoot != null)
            {
                bubbleRoot.gameObject.SetActive(
                    bubblesEnabled &&
                    isSpeaking &&
                    messageText != null &&
                    !string.IsNullOrWhiteSpace(
                        messageText.text));
            }
        }

        private void BuildBubble()
        {
            GameObject canvasObject =
                new GameObject(
                    "CharacterSpeechBubble",
                    typeof(RectTransform),
                    typeof(Canvas));
            canvasObject.transform.SetParent(
                transform,
                false);
            Canvas canvas =
                canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 120;
            bubbleRoot =
                canvasObject.GetComponent<RectTransform>();
            bubbleRoot.sizeDelta =
                new Vector2(460f, 138f);
            bubbleRoot.localScale =
                Vector3.one * 0.0025f;

            hardShadow = CreateBlock(
                "HardShadow",
                bubbleRoot,
                new Vector2(8f, -8f),
                new Vector2(444f, 122f),
                Color.black);
            outerBlack = CreateBlock(
                "OuterBlack",
                bubbleRoot,
                Vector2.zero,
                new Vector2(444f, 122f),
                Color.black);
            whiteBorder = CreateBlock(
                "WhiteBorder",
                bubbleRoot,
                Vector2.zero,
                new Vector2(432f, 110f),
                Color.white);
            blackPanel = CreateBlock(
                    "BlackPanel",
                    bubbleRoot,
                    Vector2.zero,
                    new Vector2(420f, 98f),
                    Color.black);

            GameObject textObject =
                new GameObject(
                    "SpeechText",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(TextMeshProUGUI));
            RectTransform textRect =
                textObject.GetComponent<RectTransform>();
            textRect.SetParent(blackPanel, false);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin =
                new Vector2(18f, 12f);
            textRect.offsetMax =
                new Vector2(-18f, -12f);
            messageText =
                textObject.GetComponent<TextMeshProUGUI>();
            messageText.text = currentMessage;
            messageText.color = Color.white;
            messageText.fontSize = 24f;
            messageText.alignment =
                TextAlignmentOptions.MidlineLeft;
            messageText.textWrappingMode =
                TextWrappingModes.Normal;
            messageText.overflowMode =
                TextOverflowModes.Overflow;
            messageText.enableAutoSizing = true;
            messageText.fontSizeMin = 16f;
            messageText.fontSizeMax = 24f;
            messageText.characterSpacing = 1.5f;
            TMP_FontAsset pixelFont =
                Resources.Load<TMP_FontAsset>(
                    "Fonts & Materials/" +
                    "Electronic Highway Sign SDF");
            if (pixelFont != null)
            {
                messageText.font = pixelFont;
            }

            tailBorder = CreateBlock(
                    "TailBorder",
                    bubbleRoot,
                    new Vector2(0f, -61f),
                    new Vector2(24f, 24f),
                    Color.white);
            tailBorder.localRotation =
                Quaternion.Euler(0f, 0f, 45f);
            tail = CreateBlock(
                    "Tail",
                    bubbleRoot,
                    new Vector2(0f, -59f),
                    new Vector2(18f, 18f),
                    Color.black);
            tail.localRotation =
                Quaternion.Euler(0f, 0f, 45f);
            ResizeBubbleToMessage();
            RefreshVisibility();
        }

        private void ResizeBubbleToMessage()
        {
            if (messageText == null ||
                bubbleRoot == null ||
                blackPanel == null)
            {
                return;
            }

            messageText.text = currentMessage;
            messageText.ForceMeshUpdate();
            Vector2 natural =
                messageText.GetPreferredValues(
                    currentMessage);
            float outerWidth =
                Mathf.Clamp(
                    natural.x + 60f,
                    MinimumBubbleWidth,
                    MaximumBubbleWidth);
            float textWidth =
                outerWidth - 60f;
            Vector2 wrapped =
                messageText.GetPreferredValues(
                    currentMessage,
                    textWidth,
                    0f);
            float panelHeight =
                Mathf.Clamp(
                    wrapped.y + 30f,
                    MinimumPanelHeight,
                    MaximumPanelHeight);
            float outerHeight = panelHeight + 24f;

            bubbleRoot.sizeDelta =
                new Vector2(
                    outerWidth + 16f,
                    outerHeight + 16f);
            hardShadow.sizeDelta =
                new Vector2(
                    outerWidth,
                    outerHeight);
            outerBlack.sizeDelta =
                new Vector2(
                    outerWidth,
                    outerHeight);
            whiteBorder.sizeDelta =
                new Vector2(
                    outerWidth - 12f,
                    outerHeight - 12f);
            blackPanel.sizeDelta =
                new Vector2(
                    outerWidth - 24f,
                    panelHeight);
            tailBorder.anchoredPosition =
                new Vector2(
                    0f,
                    -outerHeight * 0.5f);
            tail.anchoredPosition =
                new Vector2(
                    0f,
                    -outerHeight * 0.5f + 2f);
        }

        private void BeginTyping(float durationSeconds)
        {
            StopTyping();
            if (messageText == null ||
                string.IsNullOrEmpty(currentMessage))
            {
                return;
            }

            messageText.text = currentMessage;
            ResizeBubbleToMessage();
            messageText.maxVisibleCharacters = 0;
            typingCoroutine =
                StartCoroutine(
                    TypeMessage(durationSeconds));
        }

        private IEnumerator TypeMessage(float durationSeconds)
        {
            messageText.ForceMeshUpdate();
            int characterCount =
                messageText.textInfo.characterCount;
            if (durationSeconds > 0.05f)
            {
                float startedAt = Time.unscaledTime;
                while (true)
                {
                    float progress = Mathf.Clamp01(
                        (Time.unscaledTime - startedAt) /
                        durationSeconds);
                    messageText.maxVisibleCharacters =
                        Mathf.Clamp(
                            Mathf.FloorToInt(
                                characterCount * progress),
                            0,
                            characterCount);
                    if (progress >= 1f)
                    {
                        break;
                    }
                    yield return null;
                }
                messageText.maxVisibleCharacters =
                    characterCount;
                typingCoroutine = null;
                yield break;
            }

            for (int visible = 1;
                 visible <= characterCount;
                 visible++)
            {
                messageText.maxVisibleCharacters =
                    visible;
                char current =
                    visible - 1 < currentMessage.Length
                        ? currentMessage[visible - 1]
                        : '\0';
                float delay =
                    current == '.' ||
                    current == '!' ||
                    current == '?'
                        ? CharacterDelay * 3f
                        : CharacterDelay;
                yield return new WaitForSecondsRealtime(
                    delay);
            }
            typingCoroutine = null;
        }

        private void StopTyping()
        {
            if (typingCoroutine != null)
            {
                StopCoroutine(typingCoroutine);
                typingCoroutine = null;
            }
            if (messageText != null)
            {
                messageText.maxVisibleCharacters =
                    currentMessage.Length;
            }
        }

        private static RectTransform CreateBlock(
            string name,
            Transform parent,
            Vector2 position,
            Vector2 size,
            Color color)
        {
            GameObject block =
                new GameObject(
                    name,
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image));
            RectTransform rect =
                block.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin =
                new Vector2(0.5f, 0.5f);
            rect.anchorMax =
                new Vector2(0.5f, 0.5f);
            rect.pivot =
                new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Image image =
                block.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }

        private void LateUpdate()
        {
            if (!bubblesEnabled ||
                !isSpeaking ||
                bubbleRoot == null ||
                Camera.main == null)
            {
                return;
            }

            if (authoredHead == null)
            {
                Animator animator = GetComponent<Animator>();
                if (animator != null && animator.isHuman)
                    authoredHead = animator.GetBoneTransform(HumanBodyBones.Head);
            }
            if (authoredHead != null)
            {
                // Seated bodies retain conservative standing renderer bounds.
                // Follow the evaluated head, not a mesh/collider bounding box.
                float characterScale = Mathf.Max(0.01f, Mathf.Abs(transform.lossyScale.y));
                bubbleRoot.position = authoredHead.position + Vector3.up * (0.33f * characterScale) +
                    Camera.main.transform.forward * (0.20f * characterScale);
            }
            else if (TryGetCharacterBounds(
                    out Bounds bounds))
            {
                bubbleRoot.position =
                    bounds.center +
                    Vector3.up *
                    (bounds.extents.y + 0.32f);
            }

            // Keep the world-space UI screen-aligned. This makes the same
            // readable front face follow the camera whether it is viewing the
            // character from the front, behind, or either side.
            bubbleRoot.rotation =
                Camera.main.transform.rotation;
            FitBubbleInFrame(Camera.main);
        }

        private void FitBubbleInFrame(Camera camera)
        {
            float depth = camera.WorldToViewportPoint(bubbleRoot.position).z;
            if (depth <= camera.nearClipPlane) return;
            Vector3 bottomLeft = camera.ViewportToWorldPoint(new Vector3(0f, 0f, depth));
            float viewWidth = Vector3.Distance(bottomLeft,
                camera.ViewportToWorldPoint(new Vector3(1f, 0f, depth)));
            float viewHeight = Vector3.Distance(bottomLeft,
                camera.ViewportToWorldPoint(new Vector3(0f, 1f, depth)));
            // Screen-relative sizing prevents Chef's 4x root scale or close
            // zoom from producing an enormous, cropped bubble.
            float worldScale = Mathf.Min(viewWidth * 0.38f / bubbleRoot.rect.width,
                viewHeight * 0.18f / bubbleRoot.rect.height);
            Vector3 parentScale = bubbleRoot.parent.lossyScale;
            bubbleRoot.localScale = new Vector3(worldScale / Mathf.Max(0.001f, Mathf.Abs(parentScale.x)),
                worldScale / Mathf.Max(0.001f, Mathf.Abs(parentScale.y)),
                worldScale / Mathf.Max(0.001f, Mathf.Abs(parentScale.z)));
            // Anchor the lower edge, not the panel center, above the head.
            bubbleRoot.position += camera.transform.up * (bubbleRoot.rect.height * worldScale * 0.5f);
            bubbleRoot.GetWorldCorners(bubbleCorners);
            Vector2 min = Vector2.one * float.PositiveInfinity;
            Vector2 max = Vector2.one * float.NegativeInfinity;
            foreach (Vector3 corner in bubbleCorners)
            {
                Vector3 viewport = camera.WorldToViewportPoint(corner);
                min = Vector2.Min(min, viewport);
                max = Vector2.Max(max, viewport);
            }
            Vector3 center = camera.WorldToViewportPoint(bubbleRoot.position);
            Vector2 shift = Vector2.zero;
            if (min.x < 0.03f) shift.x = 0.03f - min.x;
            else if (max.x > 0.97f) shift.x = 0.97f - max.x;
            if (min.y < 0.03f) shift.y = 0.03f - min.y;
            else if (max.y > 0.97f) shift.y = 0.97f - max.y;
            bubbleRoot.position = camera.ViewportToWorldPoint(center + (Vector3)shift);
        }

        private bool TryGetCharacterBounds(
            out Bounds bounds)
        {
            bounds = default;
            bool found = false;
            foreach (Renderer renderer in
                GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null ||
                    (bubbleRoot != null &&
                     renderer.transform.IsChildOf(
                         bubbleRoot)) ||
                    renderer.transform.name.EndsWith(
                        "_SelectionOutline",
                        System.StringComparison.Ordinal) ||
                    !renderer.enabled ||
                    renderer.bounds.size.sqrMagnitude <
                    0.0025f)
                {
                    continue;
                }

                if (!found)
                {
                    bounds = renderer.bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(
                        renderer.bounds);
                }
            }
            return found;
        }
    }
}
