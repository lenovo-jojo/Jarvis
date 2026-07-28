using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace AICompanion.Services
{
    public sealed class PossumSpringsMiniMap :
        MonoBehaviour
    {
        private const int MapTextureSize = 384;
        private Transform target;
        private CompanionCamera playerCamera;
        private Camera mapCamera;
        private RectTransform directionMarker;
        private RectTransform smallMapRect;
        private RectTransform fullMapRect;
        private GameObject fullMapOverlay;
        private RenderTexture mapTexture;
        private Texture2D circleTexture;
        private Texture2D arrowTexture;
        private Sprite circleSprite;
        private Sprite arrowSprite;
        private float cameraHeight;
        private float fullMapSize;
        private Bounds worldBounds;
        private bool fullMapOpen;

        public void Initialize(
            Transform followTarget,
            Bounds bounds,
            CompanionCamera companionCamera)
        {
            target = followTarget;
            playerCamera = companionCamera;
            worldBounds = bounds;
            cameraHeight =
                bounds.max.y + 30f;
            fullMapSize =
                Mathf.Max(
                    bounds.extents.x,
                    bounds.extents.z) *
                1.04f;

            BuildMapCamera(bounds);
            BuildMapInterface();
        }

        private void LateUpdate()
        {
            if (target == null ||
                mapCamera == null)
            {
                return;
            }

            HandleMapClick();

            Vector3 targetPosition =
                fullMapOpen
                    ? worldBounds.center
                    : target.position;
            mapCamera.transform.position =
                new Vector3(
                    targetPosition.x,
                    cameraHeight,
                    targetPosition.z);
            mapCamera.orthographicSize =
                fullMapOpen
                    ? fullMapSize
                    : 42f;
            if (directionMarker != null)
            {
                directionMarker.localRotation =
                    Quaternion.Euler(
                        0f,
                        0f,
                        -target.eulerAngles.y);
            }
        }

        private void BuildMapCamera(
            Bounds worldBounds)
        {
            GameObject cameraObject =
                new GameObject("PossumSpringsMiniMapCamera");
            cameraObject.transform.SetParent(
                transform,
                false);
            mapCamera =
                cameraObject.AddComponent<Camera>();
            mapCamera.orthographic = true;
            mapCamera.orthographicSize = 42f;
            mapCamera.clearFlags =
                CameraClearFlags.SolidColor;
            mapCamera.backgroundColor =
                new Color(0.18f, 0.24f, 0.28f, 1f);
            mapCamera.nearClipPlane = 0.1f;
            mapCamera.farClipPlane =
                Mathf.Max(
                    100f,
                    worldBounds.size.y + 80f);
            mapCamera.depth = -20f;
            mapCamera.transform.rotation =
                Quaternion.Euler(90f, 0f, 0f);

            mapTexture =
                new RenderTexture(
                    MapTextureSize,
                    MapTextureSize,
                    16,
                    RenderTextureFormat.ARGB32)
                {
                    name = "PossumSpringsMiniMap"
                };
            mapTexture.Create();
            mapCamera.targetTexture = mapTexture;
        }

        private void BuildMapInterface()
        {
            GameObject canvasObject =
                new GameObject("PossumSpringsMiniMapUI");
            canvasObject.transform.SetParent(
                transform,
                false);
            Canvas canvas =
                canvasObject.AddComponent<Canvas>();
            canvas.renderMode =
                RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 80;
            CanvasScaler scaler =
                canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode =
                CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution =
                new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();

            circleTexture =
                CreateCircleTexture(
                    128,
                    Color.white);
            circleSprite =
                Sprite.Create(
                    circleTexture,
                    new Rect(
                        0f,
                        0f,
                        circleTexture.width,
                        circleTexture.height),
                    new Vector2(0.5f, 0.5f),
                    100f);

            GameObject ringObject =
                CreateImageObject(
                    "MiniMapRing",
                    canvasObject.transform,
                    circleSprite,
                    new Color(
                        0.95f,
                        0.91f,
                        0.78f,
                        0.96f));
            RectTransform ring =
                ringObject.GetComponent<RectTransform>();
            smallMapRect = ring;
            AnchorTopLeft(
                ring,
                new Vector2(28f, -28f),
                new Vector2(228f, 228f));

            GameObject maskObject =
                CreateImageObject(
                    "MiniMapMask",
                    ringObject.transform,
                    circleSprite,
                    Color.white);
            RectTransform maskRect =
                maskObject.GetComponent<RectTransform>();
            Stretch(maskRect, 7f);
            Mask mask =
                maskObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            GameObject mapObject =
                new GameObject(
                    "MiniMapImage",
                    typeof(RectTransform),
                    typeof(RawImage));
            mapObject.transform.SetParent(
                maskObject.transform,
                false);
            RawImage rawImage =
                mapObject.GetComponent<RawImage>();
            rawImage.texture = mapTexture;
            rawImage.raycastTarget = false;
            Stretch(
                mapObject.GetComponent<RectTransform>(),
                0f);

            arrowTexture =
                CreateArrowTexture(
                    48);
            arrowSprite =
                Sprite.Create(
                    arrowTexture,
                    new Rect(
                        0f,
                        0f,
                        arrowTexture.width,
                        arrowTexture.height),
                    new Vector2(0.5f, 0.5f),
                    100f);
            GameObject markerObject =
                CreateImageObject(
                    "PlayerDirection",
                    ringObject.transform,
                    arrowSprite,
                    new Color(
                        0.2f,
                        0.96f,
                        0.9f,
                        1f));
            directionMarker =
                markerObject.GetComponent<RectTransform>();
            directionMarker.anchorMin =
                new Vector2(0.5f, 0.5f);
            directionMarker.anchorMax =
                new Vector2(0.5f, 0.5f);
            directionMarker.pivot =
                new Vector2(0.5f, 0.5f);
            directionMarker.anchoredPosition =
                Vector2.zero;
            directionMarker.sizeDelta =
                new Vector2(25f, 34f);

            BuildFullMapOverlay(
                canvasObject.transform);
        }

        private void BuildFullMapOverlay(
            Transform canvasTransform)
        {
            fullMapOverlay =
                CreateImageObject(
                    "FullMapOverlay",
                    canvasTransform,
                    null,
                    new Color(
                        0.04f,
                        0.07f,
                        0.09f,
                        0.88f));
            RectTransform overlayRect =
                fullMapOverlay.GetComponent<RectTransform>();
            Stretch(overlayRect, 0f);

            GameObject frameObject =
                CreateImageObject(
                    "FullMapFrame",
                    fullMapOverlay.transform,
                    null,
                    new Color(
                        0.95f,
                        0.91f,
                        0.78f,
                        0.98f));
            RectTransform frameRect =
                frameObject.GetComponent<RectTransform>();
            frameRect.anchorMin =
                new Vector2(0.5f, 0.5f);
            frameRect.anchorMax =
                new Vector2(0.5f, 0.5f);
            frameRect.pivot =
                new Vector2(0.5f, 0.5f);
            frameRect.anchoredPosition =
                Vector2.zero;
            frameRect.sizeDelta =
                new Vector2(790f, 790f);

            GameObject fullMapObject =
                new GameObject(
                    "FullTownMap",
                    typeof(RectTransform),
                    typeof(RawImage));
            fullMapObject.transform.SetParent(
                frameObject.transform,
                false);
            RawImage fullMapImage =
                fullMapObject.GetComponent<RawImage>();
            fullMapImage.texture = mapTexture;
            fullMapImage.raycastTarget = false;
            fullMapRect =
                fullMapObject.GetComponent<RectTransform>();
            Stretch(fullMapRect, 10f);

            GameObject hintObject =
                new GameObject(
                    "FullMapHint",
                    typeof(RectTransform),
                    typeof(Text));
            hintObject.transform.SetParent(
                fullMapOverlay.transform,
                false);
            Text hint =
                hintObject.GetComponent<Text>();
            hint.text =
                "click a street or open area to move your spawn";
            hint.alignment =
                TextAnchor.MiddleCenter;
            hint.fontSize = 24;
            hint.color = Color.white;
            hint.font =
                Resources.GetBuiltinResource<Font>(
                    "LegacyRuntime.ttf");
            RectTransform hintRect =
                hintObject.GetComponent<RectTransform>();
            hintRect.anchorMin =
                new Vector2(0.5f, 0.5f);
            hintRect.anchorMax =
                new Vector2(0.5f, 0.5f);
            hintRect.pivot =
                new Vector2(0.5f, 0.5f);
            hintRect.anchoredPosition =
                new Vector2(0f, -430f);
            hintRect.sizeDelta =
                new Vector2(760f, 50f);

            fullMapOverlay.SetActive(false);
        }

        private void HandleMapClick()
        {
            if (Mouse.current == null ||
                !Mouse.current.leftButton
                    .wasPressedThisFrame)
            {
                return;
            }

            Vector2 screenPosition =
                Mouse.current.position.ReadValue();
            if (!fullMapOpen)
            {
                if (smallMapRect != null &&
                    RectTransformUtility
                        .RectangleContainsScreenPoint(
                            smallMapRect,
                            screenPosition))
                {
                    SetFullMapOpen(true);
                }
                return;
            }

            if (fullMapRect != null &&
                RectTransformUtility
                    .RectangleContainsScreenPoint(
                        fullMapRect,
                        screenPosition))
            {
                TryChooseSpawn(
                    screenPosition);
                return;
            }

            SetFullMapOpen(false);
        }

        private void SetFullMapOpen(
            bool open)
        {
            fullMapOpen = open;
            if (fullMapOverlay != null)
            {
                fullMapOverlay.SetActive(open);
            }
            if (smallMapRect != null)
            {
                smallMapRect.gameObject.SetActive(!open);
            }
        }

        private void TryChooseSpawn(
            Vector2 screenPosition)
        {
            if (fullMapRect == null ||
                mapCamera == null ||
                playerCamera == null ||
                !RectTransformUtility
                    .ScreenPointToLocalPointInRectangle(
                        fullMapRect,
                        screenPosition,
                        null,
                        out Vector2 localPoint))
            {
                return;
            }

            Rect rect = fullMapRect.rect;
            float viewportX =
                Mathf.InverseLerp(
                    rect.xMin,
                    rect.xMax,
                    localPoint.x);
            float viewportY =
                Mathf.InverseLerp(
                    rect.yMin,
                    rect.yMax,
                    localPoint.y);
            Ray ray =
                mapCamera.ViewportPointToRay(
                    new Vector3(
                        viewportX,
                        viewportY,
                        0f));
            RaycastHit[] hits =
                Physics.RaycastAll(
                    ray.origin,
                    ray.direction,
                    mapCamera.farClipPlane,
                    Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore);
            System.Array.Sort(
                hits,
                (left, right) =>
                    left.distance.CompareTo(
                        right.distance));
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider == null ||
                    hit.normal.y < 0.55f ||
                    (target != null &&
                     hit.collider.transform.IsChildOf(
                         target)))
                {
                    continue;
                }

                playerCamera.TeleportPlayer(
                    hit.point +
                    Vector3.up * 0.08f);
                SetFullMapOpen(false);
                return;
            }
        }

        private static GameObject CreateImageObject(
            string objectName,
            Transform parent,
            Sprite sprite,
            Color color)
        {
            GameObject imageObject =
                new GameObject(
                    objectName,
                    typeof(RectTransform),
                    typeof(Image));
            imageObject.transform.SetParent(
                parent,
                false);
            Image image =
                imageObject.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return imageObject;
        }

        private static void AnchorTopLeft(
            RectTransform rect,
            Vector2 position,
            Vector2 size)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void Stretch(
            RectTransform rect,
            float inset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin =
                new Vector2(inset, inset);
            rect.offsetMax =
                new Vector2(-inset, -inset);
        }

        private static Texture2D CreateCircleTexture(
            int size,
            Color color)
        {
            Texture2D texture =
                new Texture2D(
                    size,
                    size,
                    TextureFormat.RGBA32,
                    false);
            texture.name = "MiniMapCircle";
            float center = (size - 1f) * 0.5f;
            float radius = center - 1f;
            Color clear =
                new Color(0f, 0f, 0f, 0f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance =
                        Vector2.Distance(
                            new Vector2(x, y),
                            new Vector2(
                                center,
                                center));
                    texture.SetPixel(
                        x,
                        y,
                        distance <= radius
                            ? color
                            : clear);
                }
            }
            texture.Apply();
            return texture;
        }

        private static Texture2D CreateArrowTexture(
            int size)
        {
            Texture2D texture =
                new Texture2D(
                    size,
                    size,
                    TextureFormat.RGBA32,
                    false);
            texture.name = "MiniMapArrow";
            Color clear =
                new Color(0f, 0f, 0f, 0f);
            float center = (size - 1f) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                float normalizedY =
                    y / (size - 1f);
                float halfWidth =
                    Mathf.Lerp(
                        size * 0.32f,
                        0f,
                        normalizedY);
                for (int x = 0; x < size; x++)
                {
                    bool inside =
                        Mathf.Abs(x - center) <=
                        halfWidth;
                    texture.SetPixel(
                        x,
                        y,
                        inside
                            ? Color.white
                            : clear);
                }
            }
            texture.Apply();
            return texture;
        }

        private void OnDestroy()
        {
            if (mapTexture != null)
            {
                mapTexture.Release();
                Destroy(mapTexture);
            }
            if (circleSprite != null)
            {
                Destroy(circleSprite);
            }
            if (arrowSprite != null)
            {
                Destroy(arrowSprite);
            }
            if (circleTexture != null)
            {
                Destroy(circleTexture);
            }
            if (arrowTexture != null)
            {
                Destroy(arrowTexture);
            }
        }
    }
}
