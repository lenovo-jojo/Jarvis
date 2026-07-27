using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using AICompanion.Core;
using TMPro;

namespace AICompanion.Services
{
    public class CompanionCamera : MonoBehaviour
    {
        [Header("Target Framing")]
        [SerializeField] private float padding = 1.15f; // Extra framing margin (1.15 = 15% padding on top and bottom)
        [SerializeField] private float smoothSpeed = 8.0f; // Smooth interpolation speed
        [SerializeField] private Vector3 positionOffset = Vector3.zero; // Optional offset fine-tuning

        [Header("Orbit & Zoom Controls")]
        [SerializeField] private float rotateSensitivity = 0.25f;
        [SerializeField] private float zoomSensitivity = 0.01f;
        [SerializeField] private float minZoom = -2.0f;
        [SerializeField] private float maxZoom = 5.0f;
        [SerializeField] private float minPitch = -45f;
        [SerializeField] private float maxPitch = 45f;
        [SerializeField] private float groundClearance = 0.25f;

        [Header("Scene Navigation")]
        [SerializeField] private float moveSpeed = 2.5f;
        [SerializeField] private float sprintMultiplier = 2.5f;
        [SerializeField] private float focusSmoothTime = 0.12f;
        [SerializeField] private float verticalDragSensitivity = 0.005f;
        [SerializeField] private float minVerticalFrameOffset = -1.5f;
        [SerializeField] private float maxVerticalFrameOffset = 2.5f;

        private Camera cam;
        private Vector3 currentTargetCamPos;
        private bool isInitialized = false;

        private float orbitYaw = 180f; // Start at 180 degrees to face the avatar (who is rotated by 180)
        private float orbitPitch = 10f; // Slightly angled down
        private float zoomOffset = 0f;
        private bool isOrbitBlockedByAvatarDrag;
        private float sliderZoomInValue;
        private float sliderZoomOutValue;
        private bool hasSliderInput;
        private Vector3 sceneOffset;
        private Vector3 focusVelocity;
        private Vector3 smoothedFocusPos;
        private float smoothedFocusHeight = 1.8f;
        private GameObject framedTarget;
        private Vector3 stableLocalFocusPos;
        private float stableFocusHeight = 1.8f;
        private bool snapCameraThisFrame;

        private void Awake()
        {
            cam = GetComponent<Camera>();
        }

        public float OrbitYaw => orbitYaw;
        public float OrbitPitch => orbitPitch;
        public float ZoomOffset => zoomOffset;
        public Vector3 SceneOffset => sceneOffset;

        private void Start()
        {
            ServiceConfig config = ServiceConfig.Load();
            if (config != null && config.hasSavedFrame)
            {
                ApplySavedState(config);
            }
        }

        public void ApplySavedState(ServiceConfig config)
        {
            if (config == null || !config.hasSavedFrame)
            {
                return;
            }

            orbitYaw = config.camOrbitYaw;
            orbitPitch = Mathf.Clamp(
                config.camOrbitPitch,
                minPitch,
                maxPitch);
            zoomOffset = Mathf.Clamp(
                config.camZoomOffset,
                minZoom,
                maxZoom);
            sceneOffset = new Vector3(
                config.camSceneOffsetX,
                Mathf.Clamp(
                    config.camSceneOffsetY,
                    minVerticalFrameOffset,
                    maxVerticalFrameOffset),
                config.camSceneOffsetZ);

            transform.SetPositionAndRotation(
                ClampAboveGround(new Vector3(
                    config.camPosX,
                    config.camPosY,
                    config.camPosZ)),
                new Quaternion(
                    config.camRotX,
                    config.camRotY,
                    config.camRotZ,
                    config.camRotW));
            currentTargetCamPos = transform.position;
            focusVelocity = Vector3.zero;
            sliderZoomInValue = 0f;
            sliderZoomOutValue = 0f;
            hasSliderInput = false;
            isInitialized = true;
        }

        private void LateUpdate()
        {
            // 1. Locate active custom VRM or fallback procedural companion
            GameObject targetGo = GameObject.Find("CompanionAvatarVRM");
            if (targetGo == null)
            {
                targetGo = GameObject.Find("CompanionAvatarProcedural");
            }

            if (targetGo == null) return;

            // Measure a newly loaded avatar once. Following its animated head or
            // renderer bounds every frame makes the camera twitch with the pose.
            if (framedTarget != targetGo)
            {
                CacheStableFraming(targetGo);
            }

            Vector3 targetFocusPos = targetGo.transform.TransformPoint(stableLocalFocusPos);
            float targetHeight = stableFocusHeight;

            if (!isInitialized)
            {
                smoothedFocusPos = targetFocusPos;
                smoothedFocusHeight = targetHeight;
            }
            else
            {
                smoothedFocusPos = Vector3.SmoothDamp(smoothedFocusPos, targetFocusPos, ref focusVelocity, focusSmoothTime);
                smoothedFocusHeight = Mathf.Lerp(smoothedFocusHeight, targetHeight, Time.deltaTime * (1f / Mathf.Max(0.01f, focusSmoothTime)));
            }

            ProcessSceneNavigation();
            Vector3 focusPos = smoothedFocusPos + sceneOffset;

            // Calculate optimal base framing distance to fit the focus height
            float fov = cam != null ? cam.fieldOfView : 60f;
            float radFov = fov * Mathf.Deg2Rad;
            float baseDistance = (smoothedFocusHeight / 2.0f) * padding / Mathf.Tan(radFov / 2.0f);

            // 4. Input processing using New Input System Mouse class
            bool isInteracting = false;
            if (Mouse.current != null)
            {
                bool leftPressed = Mouse.current.leftButton.isPressed;
                bool leftPressedThisFrame = Mouse.current.leftButton.wasPressedThisFrame;
                bool rightPressed = Mouse.current.rightButton.isPressed;
                Vector2 mouseDelta = Mouse.current.delta.ReadValue();
                float scroll = Mouse.current.scroll.ReadValue().y;
                Vector2 mousePos = Mouse.current.position.ReadValue();
                bool pointerOverUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

                if (leftPressedThisFrame)
                {
                    isOrbitBlockedByAvatarDrag = IsPointerOverAvatar(targetGo, mousePos);
                }
                else if (!leftPressed)
                {
                    isOrbitBlockedByAvatarDrag = false;
                }

                bool backgroundLeftDrag =
                    leftPressed && !isOrbitBlockedByAvatarDrag;
                bool orbitDragPressed =
                    rightPressed || backgroundLeftDrag;

                // Right-drag provides full orbit control. Left-drag keeps
                // horizontal orbit, while vertical movement shifts the
                // character framing like a third-person game camera.
                if (!pointerOverUi && orbitDragPressed && mouseDelta.sqrMagnitude > 0.01f)
                {
                    orbitYaw += mouseDelta.x * rotateSensitivity;

                    if (rightPressed)
                    {
                        orbitPitch -= mouseDelta.y * rotateSensitivity;
                        orbitPitch = Mathf.Clamp(
                            orbitPitch,
                            minPitch,
                            maxPitch);
                    }
                    else
                    {
                        sceneOffset.y = Mathf.Clamp(
                            sceneOffset.y +
                                mouseDelta.y *
                                verticalDragSensitivity,
                            minVerticalFrameOffset,
                            maxVerticalFrameOffset);
                    }

                    isInteracting = true;
                }

                // Handle Scroll Zooming
                if (!pointerOverUi && Mathf.Abs(scroll) > 0.01f)
                {
                    zoomOffset -= scroll * zoomSensitivity;
                    zoomOffset = Mathf.Clamp(zoomOffset, minZoom, maxZoom);
                    isInteracting = true;
                }
            }

            if (!IsTypingIntoTextField() &&
                Keyboard.current != null &&
                Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                // Space quickly re-centers the camera framing on the avatar.
                orbitYaw = 180f;
                orbitPitch = 10f;
                zoomOffset = 0f;
                isInteracting = true;
            }

            if (!IsTypingIntoTextField() &&
                Keyboard.current != null &&
                Keyboard.current.fKey.wasPressedThisFrame)
            {
                // Instantly restore a clean, front-facing portrait shot.
                orbitYaw = 180f;
                orbitPitch = 0f;
                zoomOffset = 0f;
                sceneOffset = Vector3.zero;
                sliderZoomInValue = 0f;
                sliderZoomOutValue = 0f;
                hasSliderInput = false;
                focusVelocity = Vector3.zero;
                smoothedFocusPos = targetFocusPos;
                smoothedFocusHeight = targetHeight;
                focusPos = targetFocusPos;
                snapCameraThisFrame = true;
                isInteracting = true;
            }

            // Calculate target distance after zoom
            ApplySliderZoom();
            float finalDistance = baseDistance + zoomOffset;

            // Compute ideal/target position based on orbit angles
            // Convert Yaw/Pitch angles to look direction pointing back from center
            Quaternion orbitRotation = Quaternion.Euler(orbitPitch, orbitYaw, 0f);
            Vector3 targetOffsetDir = orbitRotation * Vector3.forward;
            Vector3 idealCamPos = ClampAboveGround(
                focusPos +
                targetOffsetDir * finalDistance +
                positionOffset);

            if (!isInitialized || snapCameraThisFrame)
            {
                transform.position = idealCamPos;
                transform.rotation = Quaternion.LookRotation(focusPos - transform.position);
                currentTargetCamPos = idealCamPos;
                isInitialized = true;
                snapCameraThisFrame = false;
            }

            if (isInteracting)
            {
                // When actively dragging or zooming, camera updates target instantly
                currentTargetCamPos = idealCamPos;
            }
            else
            {
                currentTargetCamPos = idealCamPos;
            }

            // 5. Smoothly transition to the target position & focus
            if (Vector3.Distance(transform.position, currentTargetCamPos) > 0.005f)
            {
                transform.position = Vector3.Lerp(transform.position, currentTargetCamPos, Time.deltaTime * smoothSpeed);
                Quaternion targetCamRot = Quaternion.LookRotation(focusPos - transform.position);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetCamRot, Time.deltaTime * smoothSpeed);
            }

            // Protect against stale save data or any external movement that
            // attempts to place the camera below the scene floor.
            transform.position = ClampAboveGround(transform.position);
        }

        private Vector3 ClampAboveGround(Vector3 position)
        {
            position.y = Mathf.Max(position.y, groundClearance);
            return position;
        }

        private Vector3[] GetBoundsCorners(Bounds b)
        {
            return new Vector3[] {
                new Vector3(b.min.x, b.min.y, b.min.z),
                new Vector3(b.min.x, b.min.y, b.max.z),
                new Vector3(b.min.x, b.max.y, b.min.z),
                new Vector3(b.min.x, b.max.y, b.max.z),
                new Vector3(b.max.x, b.min.y, b.min.z),
                new Vector3(b.max.x, b.min.y, b.max.z),
                new Vector3(b.max.x, b.max.y, b.min.z),
                new Vector3(b.max.x, b.max.y, b.max.z)
            };
        }

        private bool IsPointerOverAvatar(GameObject targetGo, Vector2 pointerPosition)
        {
            if (cam == null || targetGo == null)
            {
                return false;
            }

            Ray ray = cam.ScreenPointToRay(pointerPosition);
            return Physics.Raycast(ray, out RaycastHit hit) && hit.transform.IsChildOf(targetGo.transform);
        }

        private void CacheStableFraming(GameObject targetGo)
        {
            Bounds bounds = new Bounds(targetGo.transform.position, Vector3.zero);
            Renderer[] renderers = targetGo.GetComponentsInChildren<Renderer>();
            bool hasBounds = false;

            foreach (Renderer renderer in renderers)
            {
                if (renderer == null || !renderer.enabled || renderer.bounds.size.magnitude <= 0.05f)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            if (!hasBounds)
            {
                bounds = new Bounds(targetGo.transform.position + Vector3.up * 0.9f, new Vector3(1f, 1.8f, 1f));
            }

            Vector3 focusPosition = bounds.center;
            float focusHeight = bounds.size.y;
            Animator animator = targetGo.GetComponent<Animator>();
            if (animator != null && animator.isHuman)
            {
                Transform headBone = animator.GetBoneTransform(HumanBodyBones.Head);
                if (headBone != null)
                {
                    focusPosition = headBone.position;
                    focusHeight = 0.6f;
                }
            }

            framedTarget = targetGo;
            stableLocalFocusPos = targetGo.transform.InverseTransformPoint(focusPosition);
            stableFocusHeight = focusHeight;
            smoothedFocusPos = focusPosition;
            smoothedFocusHeight = focusHeight;
            focusVelocity = Vector3.zero;
        }

        public void SetZoomInSlider(float normalizedValue)
        {
            sliderZoomInValue = Mathf.Clamp01(normalizedValue);
            hasSliderInput = true;
        }

        public void SetZoomOutSlider(float normalizedValue)
        {
            sliderZoomOutValue = Mathf.Clamp01(normalizedValue);
            hasSliderInput = true;
        }

        private void ApplySliderZoom()
        {
            if (!hasSliderInput)
            {
                return;
            }
            float sliderDrivenZoom = Mathf.Lerp(minZoom, maxZoom, 0.5f + (sliderZoomOutValue - sliderZoomInValue) * 0.5f);
            zoomOffset = Mathf.Clamp(sliderDrivenZoom, minZoom, maxZoom);
        }

        private void ProcessSceneNavigation()
        {
            if (Keyboard.current == null || cam == null || IsTypingIntoTextField())
            {
                return;
            }

            Vector3 moveInput = Vector3.zero;
            if (Keyboard.current.wKey.isPressed) moveInput += Vector3.forward;
            if (Keyboard.current.sKey.isPressed) moveInput += Vector3.back;
            if (Keyboard.current.aKey.isPressed) moveInput += Vector3.left;
            if (Keyboard.current.dKey.isPressed) moveInput += Vector3.right;
            if (Keyboard.current.qKey.isPressed) moveInput += Vector3.down;
            if (Keyboard.current.eKey.isPressed) moveInput += Vector3.up;

            if (moveInput.sqrMagnitude > 0f)
            {
                float speed = moveSpeed * (Keyboard.current.leftShiftKey.isPressed ? sprintMultiplier : 1f);
                Vector3 worldMove =
                    (cam.transform.forward * moveInput.z) +
                    (cam.transform.right * moveInput.x) +
                    (Vector3.up * moveInput.y);
                sceneOffset += worldMove.normalized * speed * Time.deltaTime;
                sceneOffset.y = Mathf.Clamp(
                    sceneOffset.y,
                    minVerticalFrameOffset,
                    maxVerticalFrameOffset);
            }

            if (Keyboard.current.rKey.wasPressedThisFrame)
            {
                sceneOffset = Vector3.zero;
            }
        }

        private static bool IsTypingIntoTextField()
        {
            GameObject selected = EventSystem.current != null
                ? EventSystem.current.currentSelectedGameObject
                : null;

            return selected != null && selected.GetComponentInParent<TMP_InputField>() != null;
        }
    }
}
