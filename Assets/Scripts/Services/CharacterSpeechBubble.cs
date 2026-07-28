using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AICompanion.Services
{
    /// <summary>
    /// A camera-facing, Neocities-inspired pixel speech bubble anchored above
    /// a character.
    /// </summary>
    public sealed class CharacterSpeechBubble : MonoBehaviour
    {
        private RectTransform bubbleRoot;
        private TMP_Text messageText;
        private bool bubblesEnabled = true;
        private bool isSpeaking;
        private string currentMessage =
            string.Empty;

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
            }
            RefreshVisibility();
        }

        public void SetBubbleVisible(bool visible)
        {
            bubblesEnabled = visible;
            RefreshVisibility();
        }

        public void SetSpeaking(bool speaking)
        {
            isSpeaking = speaking;
            if (isSpeaking &&
                bubbleRoot == null &&
                currentMessage.Length > 0)
            {
                BuildBubble();
            }
            RefreshVisibility();
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

            CreateBlock(
                "HardShadow",
                bubbleRoot,
                new Vector2(8f, -8f),
                new Vector2(444f, 122f),
                Color.black);
            CreateBlock(
                "OuterBlack",
                bubbleRoot,
                Vector2.zero,
                new Vector2(444f, 122f),
                Color.black);
            CreateBlock(
                "WhiteBorder",
                bubbleRoot,
                Vector2.zero,
                new Vector2(432f, 110f),
                Color.white);
            RectTransform panel =
                CreateBlock(
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
            textRect.SetParent(panel, false);
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
                TextOverflowModes.Ellipsis;
            messageText.characterSpacing = 1.5f;
            TMP_FontAsset pixelFont =
                Resources.Load<TMP_FontAsset>(
                    "Fonts & Materials/" +
                    "Electronic Highway Sign SDF");
            if (pixelFont != null)
            {
                messageText.font = pixelFont;
            }

            RectTransform tailBorder =
                CreateBlock(
                    "TailBorder",
                    bubbleRoot,
                    new Vector2(0f, -61f),
                    new Vector2(24f, 24f),
                    Color.white);
            tailBorder.localRotation =
                Quaternion.Euler(0f, 0f, 45f);
            RectTransform tail =
                CreateBlock(
                    "Tail",
                    bubbleRoot,
                    new Vector2(0f, -59f),
                    new Vector2(18f, 18f),
                    Color.black);
            tail.localRotation =
                Quaternion.Euler(0f, 0f, 45f);
            RefreshVisibility();
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

            if (TryGetCharacterBounds(
                    out Bounds bounds))
            {
                bubbleRoot.position =
                    bounds.center +
                    Vector3.up *
                    (bounds.extents.y + 0.32f);
            }

            Vector3 awayFromCamera =
                bubbleRoot.position -
                Camera.main.transform.position;
            if (awayFromCamera.sqrMagnitude > 0.001f)
            {
                bubbleRoot.rotation =
                    Quaternion.LookRotation(
                        awayFromCamera.normalized,
                        Vector3.up);
            }
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
