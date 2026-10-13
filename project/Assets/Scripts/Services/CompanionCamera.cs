using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using AICompanion.Core;
using TMPro;

namespace AICompanion.Services
{
    public class CompanionCamera : MonoBehaviour
    {
        public enum PlayerViewStyle
        {
            Pov,
            OverTheShoulder
        }

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
        [SerializeField] private float minCameraDistance = 0.45f;
        [SerializeField] private float maxCameraDistance = 12f;
        [SerializeField] private float keyboardOrbitSpeed = 70f;
        [SerializeField] private float keyboardZoomSpeed = 1.8f;
        [SerializeField] private float keyboardPitchSpeed = 55f;

        [Header("Scene Navigation")]
        [SerializeField] private float moveSpeed = 2.5f;
        [SerializeField] private float sprintMultiplier = 2.5f;
        [SerializeField] private float focusSmoothTime = 0.12f;
        [SerializeField] private float verticalDragSensitivity = 0.005f;
        [SerializeField] private float minVerticalFrameOffset = -1.5f;
        [SerializeField] private float maxVerticalFrameOffset = 2.5f;
        [SerializeField] private float maxExploreHeight = 20f;
        [SerializeField] private float exploreLookSensitivity = 0.25f;
        [SerializeField] private float minExplorePitch = -85f;
        [SerializeField] private float maxExplorePitch = 85f;
        [SerializeField] private float playerTurnSpeed = 12f;
        [SerializeField] private float conversationTurnSpeed = 4f;
        [SerializeField] private float maxPovLean = 0.45f;
        [SerializeField] private float povLeanSensitivity = 0.0015f;
        [SerializeField] private PlayerViewStyle playerViewStyle =
            PlayerViewStyle.Pov;
        [SerializeField] private float shoulderDistance = 3.2f;
        [SerializeField] private float shoulderHeight = 0.35f;
        [SerializeField] private float shoulderSideOffset = 0.65f;
        [SerializeField] private float shoulderLookHeight = 0.2f;
        [SerializeField] private float minShoulderDistance = 1.35f;
        [SerializeField] private float maxShoulderDistance = 7f;
        [SerializeField] private float playerPinchZoomSensitivity = 0.01f;
        [SerializeField] private float povPinchFovSensitivity = 0.12f;
        [SerializeField] private float minPovFieldOfView = 35f;
        [SerializeField] private float maxPovFieldOfView = 80f;
        [SerializeField] private float jumpHeight = 1.15f;
        [SerializeField] private float gravity = -22f;
        [SerializeField] private float streetHeadingIncrement = 15f;
        [SerializeField] private float streetHeadingTurnThreshold = 32f;
        [SerializeField] private float streetHeadingAssistSpeed = 9f;

        [Header("Character Tracking")]
        [SerializeField] private Transform selectedTarget;
        [SerializeField] private bool exploreMode;
        [SerializeField] private float fullBodyFramingScale = 1f;
        [SerializeField] private float fullBodyVerticalBias = 0f;

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
        private Vector3 stableLocalFullBodyFocusPos;
        private float stableFullBodyFocusHeight = 1.8f;
        private Vector3[] stableLocalFullBodyBoundsCorners;
        private Vector3 stableLocalFaceFocusPos;
        private float stableFaceFocusHeight = 0.6f;
        private Vector3 stableLocalFaceForward = Vector3.forward;
        private bool isTvGroupFraming;
        private Vector3 tvGroupFocusPosition;
        private float tvGroupFocusHeight = 1.8f;
        private Vector3[] tvGroupBoundsCorners;
        private bool isFaceFocusMode;
        private bool hasManualFocus;
        private Vector3 manualFocusPosition;
        private bool snapCameraThisFrame;
        private Vector3 explorePosition;
        private float exploreYaw;
        private float explorePitch;
        private bool hasExplorePose;
        private Transform playerAvatar;
        private PlayerLocomotionAnimator playerLocomotion;
        private Vector3 playerLocalCameraPivot =
            new Vector3(0f, 1.35f, 0f);
        private const float PovBodyRevealPitch = 32f;
        private const float PovEyeForwardOffset = 0.035f;
        private const float PovNearClipPlane = 0.025f;
        private const float PovBodyNearClipPlane = 0.12f;
        private const float BlanketClickDragThreshold = 12f;
        private float characterNearClipPlane;
        private bool playerRenderersVisible = true;
        private bool exploreFollowingPlayer = true;
        private bool blanketClickArmed;
        private Vector2 blanketPressPosition;
        private RaycastHit blanketClickHit;
        private RuntimeHumanoidPosePlayer playerSeatedPose;
        private float povLeanDistance;
        private bool followWorldSurface;
        private float navigationMinHeight = 0.02f;
        private float navigationMaxHeight = 20f;
        private float playerVerticalVelocity;
        private bool playerGrounded;
        private bool hasStreetHeading;
        private Vector3 streetHeading = Vector3.forward;
        private bool pinchGestureActive;
        private float previousPinchDistance;

        private void Awake()
        {
            cam = GetComponent<Camera>();
            characterNearClipPlane =
                cam != null ? cam.nearClipPlane : 0.1f;
            EnsureRuntimeDefaults();
        }

        public float OrbitYaw => orbitYaw;
        public float OrbitPitch => orbitPitch;
        public float ZoomOffset => zoomOffset;
        public Vector3 SceneOffset => sceneOffset;
        public bool ExploreMode => exploreMode;
        public PlayerViewStyle CurrentPlayerViewStyle =>
            playerViewStyle;

        private void Start()
        {
            ServiceConfig config = ServiceConfig.Load();
            if (config != null && config.hasSavedFrame)
            {
                ApplySavedState(config);
            }
        }

        private void EnsureRuntimeDefaults()
        {
            // Older scenes do not contain camera fields added after the
            // component was first serialized. Unity can restore those missing
            // values as zero, which collapses both distance and movement
            // ranges. Repair only invalid values so inspector tuning is kept.
            if (groundClearance <= 0f)
            {
                groundClearance = 0.25f;
            }
            if (minCameraDistance <= 0f)
            {
                minCameraDistance = 0.45f;
            }
            if (maxCameraDistance <= minCameraDistance)
            {
                maxCameraDistance = 12f;
            }
            if (keyboardOrbitSpeed <= 0f)
            {
                keyboardOrbitSpeed = 70f;
            }
            if (keyboardZoomSpeed <= 0f)
            {
                keyboardZoomSpeed = 1.8f;
            }
            if (keyboardPitchSpeed <= 0f)
            {
                keyboardPitchSpeed = 55f;
            }
            if (verticalDragSensitivity <= 0f)
            {
                verticalDragSensitivity = 0.005f;
            }
            if (maxVerticalFrameOffset <= minVerticalFrameOffset)
            {
                minVerticalFrameOffset = -1.5f;
                maxVerticalFrameOffset = 2.5f;
            }
            if (maxExploreHeight <= groundClearance)
            {
                maxExploreHeight = 20f;
            }
            if (fullBodyFramingScale <= 0.1f)
            {
                fullBodyFramingScale = 1f;
            }
            if (streetHeadingIncrement <= 0f)
            {
                streetHeadingIncrement = 15f;
            }
            if (streetHeadingTurnThreshold <= 0f)
            {
                streetHeadingTurnThreshold = 32f;
            }
            if (streetHeadingAssistSpeed <= 0f)
            {
                streetHeadingAssistSpeed = 9f;
            }
            if (minShoulderDistance <= 0f)
            {
                minShoulderDistance = 1.35f;
            }
            if (maxShoulderDistance <= minShoulderDistance)
            {
                maxShoulderDistance = 7f;
            }
            if (playerPinchZoomSensitivity <= 0f)
            {
                playerPinchZoomSensitivity = 0.01f;
            }
            if (povPinchFovSensitivity <= 0f)
            {
                povPinchFovSensitivity = 0.12f;
            }
            if (maxPovFieldOfView <= minPovFieldOfView)
            {
                minPovFieldOfView = 35f;
                maxPovFieldOfView = 80f;
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
            // 1. Follow the explicitly selected avatar, or the nearest active
            // companion until a character selector supplies one.
            GameObject targetGo = ResolveTrackedTarget();

            if (targetGo == null) return;

            // Measure a newly loaded avatar once. Following its animated head or
            // renderer bounds every frame makes the camera twitch with the pose.
            if (framedTarget != targetGo)
            {
                CacheStableFraming(targetGo);
            }

            UpdateCharacterInteraction(targetGo);

            Vector3 activeLocalFocusPos = isFaceFocusMode
                ? stableLocalFaceFocusPos
                : stableLocalFullBodyFocusPos;
            Vector3 targetFocusPos =
                isTvGroupFraming
                    ? tvGroupFocusPosition
                    : targetGo.transform.TransformPoint(
                        activeLocalFocusPos);
            float targetHeight =
                isTvGroupFraming
                    ? tvGroupFocusHeight
                    : isFaceFocusMode
                        ? stableFaceFocusHeight
                        : stableFullBodyFocusHeight;
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
            Vector3 focusPos = hasManualFocus
                ? manualFocusPosition
                : smoothedFocusPos + sceneOffset;

            // 4. Input processing using New Input System Mouse class
            bool isInteracting = false;
            if (Mouse.current != null)
            {
                bool leftPressed = Mouse.current.leftButton.isPressed;
                bool leftPressedThisFrame = Mouse.current.leftButton.wasPressedThisFrame;
                bool leftReleasedThisFrame =
                    Mouse.current.leftButton.wasReleasedThisFrame;
                bool rightPressed = Mouse.current.rightButton.isPressed;
                Vector2 mouseDelta = Mouse.current.delta.ReadValue();
                float scroll = Mouse.current.scroll.ReadValue().y;
                Vector2 mousePos = Mouse.current.position.ReadValue();
                bool pointerOverUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

                if (leftPressedThisFrame)
                {
                    isOrbitBlockedByAvatarDrag =
                        IsPointerOverSelectableCharacter(
                            targetGo,
                            mousePos);
                    blanketClickArmed =
                        !pointerOverUi &&
                        TryRaycastBlanket(
                            mousePos,
                            out blanketClickHit);
                    blanketPressPosition = mousePos;
                }
                else if (!leftPressed)
                {
                    isOrbitBlockedByAvatarDrag = false;
                }

                if (blanketClickArmed &&
                    leftPressed &&
                    Vector2.Distance(
                        blanketPressPosition,
                        mousePos) >
                    BlanketClickDragThreshold)
                {
                    blanketClickArmed = false;
                }

                if (leftReleasedThisFrame)
                {
                    if (blanketClickArmed && !pointerOverUi)
                    {
                        SeatPlayerOnBlanket(blanketClickHit);
                        isInteracting = true;
                    }
                    blanketClickArmed = false;
                }

                bool backgroundLeftDrag =
                    leftPressed &&
                    !isOrbitBlockedByAvatarDrag &&
                    !blanketClickArmed;
                bool orbitDragPressed =
                    rightPressed || backgroundLeftDrag;

                // Right-drag provides full orbit control. Left-drag keeps
                // horizontal orbit, while vertical movement shifts the
                // character framing like a third-person game camera.
                if (!pointerOverUi && orbitDragPressed && mouseDelta.sqrMagnitude > 0.01f)
                {
                    if (exploreMode)
                    {
                        EnsureExplorePose();
                        exploreYaw += mouseDelta.x * exploreLookSensitivity;
                        explorePitch = Mathf.Clamp(
                            explorePitch -
                            mouseDelta.y * exploreLookSensitivity,
                            minExplorePitch,
                            maxExplorePitch);
                    }
                    else
                    {
                        orbitYaw += mouseDelta.x * rotateSensitivity;

                        if (rightPressed)
                        {
                            orbitPitch = Mathf.Clamp(
                                orbitPitch -
                                mouseDelta.y * rotateSensitivity,
                                minPitch,
                                maxPitch);
                        }
                        else
                        {
                            float verticalDelta =
                                mouseDelta.y * verticalDragSensitivity;
                            if (hasManualFocus)
                            {
                                manualFocusPosition.y += verticalDelta;
                                sceneOffset =
                                    manualFocusPosition -
                                    smoothedFocusPos;
                            }
                            else
                            {
                                sceneOffset.y = Mathf.Clamp(
                                    sceneOffset.y + verticalDelta,
                                    minVerticalFrameOffset,
                                    maxVerticalFrameOffset);
                            }
                        }
                    }

                    isInteracting = true;
                }

                // Handle Scroll Zooming
                if (!exploreMode &&
                    !pointerOverUi &&
                    Mathf.Abs(scroll) > 0.01f)
                {
                    zoomOffset -= scroll * zoomSensitivity;
                    zoomOffset = Mathf.Clamp(zoomOffset, minZoom, maxZoom);
                    isInteracting = true;
                }
                else if (exploreMode &&
                    exploreFollowingPlayer &&
                    !pointerOverUi &&
                    Mathf.Abs(scroll) > 0.01f)
                {
                    ApplyPlayerPinchZoom(scroll);
                    isInteracting = true;
                }
            }

            if (ProcessTouchPinchZoom())
            {
                isInteracting = true;
            }

            if (!IsTypingIntoTextField() &&
                Keyboard.current != null &&
                Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                if (exploreMode &&
                    playerAvatar != null &&
                    exploreFollowingPlayer)
                {
                    TryStartPlayerJump();
                }
                else if (exploreMode)
                {
                    // Return to the self-insert after an R/F companion snap.
                    exploreFollowingPlayer = true;
                    hasExplorePose = false;
                    SetPlayerVisible(true);
                }
                else
                {
                    // Space quickly re-centers character-mode framing.
                    isTvGroupFraming = false;
                    orbitYaw = 180f;
                    orbitPitch = 10f;
                    zoomOffset = 0f;
                    sceneOffset = Vector3.zero;
                    hasManualFocus = false;
                }
                isInteracting = true;
            }

            if (!IsTypingIntoTextField() &&
                Keyboard.current != null &&
                Keyboard.current.vKey.wasPressedThisFrame &&
                exploreMode &&
                playerAvatar != null)
            {
                SetPlayerViewStyle(
                    playerViewStyle ==
                    PlayerViewStyle.Pov
                        ? PlayerViewStyle.OverTheShoulder
                        : PlayerViewStyle.Pov);
                isInteracting = true;
            }

            if (!IsTypingIntoTextField() &&
                Keyboard.current != null &&
                Keyboard.current.rKey.wasPressedThisFrame)
            {
                // Full-body reset.
                exploreFollowingPlayer = false;
                if (exploreMode)
                {
                    SetPlayerVisible(false);
                }
                isFaceFocusMode = false;
                if (!exploreMode &&
                    TryCacheTvGroupFraming(
                        targetGo,
                        false,
                        out targetFocusPos,
                        out targetHeight))
                {
                    isTvGroupFraming = true;
                }
                else
                {
                    isTvGroupFraming = false;
                    // The selected pose can be much wider/deeper than the
                    // avatar's load pose. Capture it once so the frame stays
                    // stable while fitting the seated silhouette.
                    CacheStableFraming(targetGo);
                    targetFocusPos =
                        targetGo.transform.TransformPoint(
                            stableLocalFullBodyFocusPos);
                    targetHeight =
                        stableFullBodyFocusHeight;
                }
                ResetToFraming(
                    targetFocusPos,
                    targetHeight,
                    isTvGroupFraming ? -4f : -10f,
                    isTvGroupFraming
                        ? GetTvGroupYaw(
                            targetGo,
                            targetFocusPos)
                        : GetFaceForwardYaw(targetGo));
                focusPos = targetFocusPos;
                isInteracting = true;
            }

            if (!IsTypingIntoTextField() &&
                Keyboard.current != null &&
                Keyboard.current.fKey.wasPressedThisFrame)
            {
                if (exploreMode &&
                    playerAvatar != null)
                {
                    SetPlayerViewStyle(
                        playerViewStyle ==
                        PlayerViewStyle.Pov
                            ? PlayerViewStyle.OverTheShoulder
                            : PlayerViewStyle.Pov);
                }
                else
                {
                    // Face reset in TV mode.
                    exploreFollowingPlayer = false;
                    isFaceFocusMode = true;
                    if (TryCacheTvGroupFraming(
                            targetGo,
                            true,
                            out targetFocusPos,
                            out targetHeight))
                    {
                        isTvGroupFraming = true;
                    }
                    else
                    {
                        isTvGroupFraming = false;
                        targetFocusPos =
                            targetGo.transform.TransformPoint(
                                stableLocalFaceFocusPos);
                        targetHeight =
                            stableFaceFocusHeight;
                    }
                    ResetToFraming(
                        targetFocusPos,
                        targetHeight,
                        0f,
                        isTvGroupFraming
                            ? GetTvGroupYaw(
                                targetGo,
                                targetFocusPos)
                            : GetFaceForwardYaw(targetGo));
                    focusPos = targetFocusPos;
                }
                isInteracting = true;
            }

            // POV and shoulder views remain attached to the player so WASD
            // works in both styles.
            if (exploreMode && !snapCameraThisFrame)
            {
                ProcessExploreNavigation();
                return;
            }

            Quaternion orbitRotation =
                Quaternion.Euler(orbitPitch, orbitYaw, 0f);
            Vector3 targetOffsetDir =
                orbitRotation * Vector3.forward;

            // Calculate optimal base framing distance after shortcut input may
            // have switched between face and full-body framing. Full-body mode
            // also accounts for pose depth, preventing feet that point toward
            // the lens from becoming an extreme wide-angle close-up.
            float fov = cam != null ? cam.fieldOfView : 60f;
            float radFov = fov * Mathf.Deg2Rad;
            float baseDistance =
                (smoothedFocusHeight / 2.0f) *
                padding /
                Mathf.Tan(radFov / 2.0f);
            if (!isFaceFocusMode ||
                isTvGroupFraming)
            {
                baseDistance = Mathf.Max(
                    baseDistance,
                    CalculateFullBodyFitDistance(
                        targetGo,
                        focusPos,
                        targetOffsetDir));
            }

            // Calculate target distance after zoom
            ApplySliderZoom();
            float finalDistance = Mathf.Clamp(
                baseDistance + zoomOffset,
                minCameraDistance,
                maxCameraDistance);

            // Compute ideal/target position based on orbit angles
            // Convert Yaw/Pitch angles to look direction pointing back from center
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
                if (exploreMode)
                {
                    CaptureExplorePose();
                }
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
            position.y =
                Mathf.Max(
                    position.y,
                    navigationMinHeight +
                    groundClearance);
            return position;
        }

        private void ResetToFraming(
            Vector3 targetFocusPosition,
            float targetFocusHeight,
            float targetPitch,
            float? targetYaw = null)
        {
            orbitYaw = targetYaw ?? 180f;
            orbitPitch = targetPitch;
            zoomOffset = 0f;
            sceneOffset = Vector3.zero;
            hasManualFocus = false;
            sliderZoomInValue = 0f;
            sliderZoomOutValue = 0f;
            hasSliderInput = false;
            focusVelocity = Vector3.zero;
            smoothedFocusPos = targetFocusPosition;
            smoothedFocusHeight = targetFocusHeight;
            snapCameraThisFrame = true;
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
            return Physics.Raycast(
                       ray,
                       out RaycastHit hit,
                       Mathf.Infinity,
                       Physics.DefaultRaycastLayers,
                       QueryTriggerInteraction.Collide) &&
                   hit.transform.IsChildOf(
                       targetGo.transform);
        }

        private bool IsPointerOverSelectableCharacter(
            GameObject targetGo,
            Vector2 pointerPosition)
        {
            if (cam == null)
            {
                return false;
            }

            Ray ray = cam.ScreenPointToRay(
                pointerPosition);
            if (!Physics.Raycast(
                    ray,
                    out RaycastHit hit,
                    Mathf.Infinity,
                    Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Collide))
            {
                return false;
            }

            if (targetGo != null &&
                hit.transform.IsChildOf(
                    targetGo.transform))
            {
                return true;
            }

            return hit.transform.GetComponentInParent<
                       CharacterSelectionHighlight>() != null ||
                   (playerAvatar != null &&
                    hit.transform.IsChildOf(
                        playerAvatar)) ||
                   hit.transform.name.StartsWith(
                       "WorldAvatarVRM_",
                       System.StringComparison.OrdinalIgnoreCase);
        }

        private bool TryRaycastBlanket(
            Vector2 pointerPosition,
            out RaycastHit blanketHit)
        {
            blanketHit = default;
            if (cam == null)
            {
                return false;
            }

            Ray ray = cam.ScreenPointToRay(pointerPosition);
            if (!Physics.Raycast(ray, out RaycastHit hit))
            {
                return false;
            }

            Transform hitTransform = hit.transform;
            if (hitTransform == null ||
                hitTransform.name != "PicnicStage_Blanket")
            {
                return false;
            }

            blanketHit = hit;
            return true;
        }

        private void SeatPlayerOnBlanket(RaycastHit blanketHit)
        {
            if (playerAvatar == null)
            {
                return;
            }

            Renderer blanketRenderer =
                blanketHit.transform.GetComponent<Renderer>();
            float surfaceY =
                blanketRenderer != null
                    ? blanketRenderer.bounds.max.y
                    : blanketHit.point.y;

            Vector3 seatPosition = blanketHit.point;
            seatPosition.y = surfaceY;

            GameObject companion = ResolveTrackedTarget();
            Vector3 facingDirection =
                companion != null
                    ? companion.transform.position - seatPosition
                    : -blanketHit.transform.forward;
            facingDirection.y = 0f;
            Quaternion seatRotation =
                facingDirection.sqrMagnitude > 0.001f
                    ? Quaternion.LookRotation(
                        facingDirection.normalized,
                        Vector3.up)
                    : playerAvatar.rotation;

            playerAvatar.SetPositionAndRotation(
                seatPosition,
                seatRotation);
            playerLocomotion?.SetMotion(
                0f,
                false,
                false);

            playerSeatedPose =
                playerAvatar.GetComponent<
                    RuntimeHumanoidPosePlayer>();
            if (playerSeatedPose == null)
            {
                playerSeatedPose =
                    playerAvatar.gameObject.AddComponent<
                        RuntimeHumanoidPosePlayer>();
            }
            playerSeatedPose.SetPoseActive(
                true,
                surfaceY);
            exploreYaw = seatRotation.eulerAngles.y;
            hasExplorePose = false;
        }

        private void CacheStableFraming(GameObject targetGo)
        {
            isTvGroupFraming = false;
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

            Vector3 fullBodyFocusPosition =
                bounds.center +
                Vector3.up * (bounds.size.y * fullBodyVerticalBias);
            float fullBodyFocusHeight = Mathf.Max(
                bounds.size.y * fullBodyFramingScale,
                0.6f);
            Vector3 faceFocusPosition =
                bounds.center + Vector3.up * (bounds.extents.y * 0.55f);
            Vector3 faceForward = targetGo.transform.forward;
            Animator animator = targetGo.GetComponent<Animator>();
            if (animator != null && animator.isHuman)
            {
                Transform headBone = animator.GetBoneTransform(HumanBodyBones.Head);
                if (headBone != null)
                {
                    faceFocusPosition = headBone.position;
                    faceForward = headBone.forward;
                }
            }

            framedTarget = targetGo;
            stableLocalFullBodyFocusPos =
                targetGo.transform.InverseTransformPoint(
                    fullBodyFocusPosition);
            stableFullBodyFocusHeight = fullBodyFocusHeight;
            Vector3[] worldBoundsCorners = GetBoundsCorners(bounds);
            stableLocalFullBodyBoundsCorners =
                new Vector3[worldBoundsCorners.Length];
            for (int i = 0; i < worldBoundsCorners.Length; i++)
            {
                stableLocalFullBodyBoundsCorners[i] =
                    targetGo.transform.InverseTransformPoint(
                        worldBoundsCorners[i]);
            }
            stableLocalFaceFocusPos =
                targetGo.transform.InverseTransformPoint(
                    faceFocusPosition);
            stableFaceFocusHeight = 0.6f;
            stableLocalFaceForward =
                targetGo.transform.InverseTransformDirection(
                    faceForward.normalized);

            Vector3 initialFocusPosition = isFaceFocusMode
                ? faceFocusPosition
                : fullBodyFocusPosition;
            smoothedFocusPos = initialFocusPosition;
            smoothedFocusHeight = isFaceFocusMode
                ? stableFaceFocusHeight
                : stableFullBodyFocusHeight;
            focusVelocity = Vector3.zero;
        }

        private bool TryCacheTvGroupFraming(
            GameObject companion,
            bool tight,
            out Vector3 focusPosition,
            out float focusHeight)
        {
            focusPosition = Vector3.zero;
            focusHeight = 1.8f;
            if (companion == null ||
                playerAvatar == null ||
                !playerAvatar.gameObject.activeInHierarchy)
            {
                return false;
            }

            Bounds groupBounds = default;
            bool hasBounds = false;
            EncapsulateVisibleAvatarBounds(
                companion,
                ref groupBounds,
                ref hasBounds);
            EncapsulateVisibleAvatarBounds(
                playerAvatar.gameObject,
                ref groupBounds,
                ref hasBounds);
            if (!hasBounds)
            {
                return false;
            }

            if (tight)
            {
                Vector3 min = groupBounds.min;
                Vector3 max = groupBounds.max;
                min.y =
                    Mathf.Lerp(
                        min.y,
                        max.y,
                        0.4f);
                groupBounds.SetMinMax(
                    min,
                    max);
            }

            focusPosition =
                groupBounds.center;
            focusHeight =
                Mathf.Max(
                    groupBounds.size.y *
                    (tight ? 1.12f : fullBodyFramingScale),
                    tight ? 0.8f : 1.2f);
            tvGroupFocusPosition =
                focusPosition;
            tvGroupFocusHeight =
                focusHeight;
            tvGroupBoundsCorners =
                GetBoundsCorners(groupBounds);
            return true;
        }

        private static void EncapsulateVisibleAvatarBounds(
            GameObject avatar,
            ref Bounds bounds,
            ref bool hasBounds)
        {
            if (avatar == null)
            {
                return;
            }

            foreach (Renderer renderer in
                avatar.GetComponentsInChildren<Renderer>(
                    false))
            {
                if (renderer == null ||
                    !renderer.enabled ||
                    renderer.transform.name.EndsWith(
                        "_SelectionOutline",
                        System.StringComparison.Ordinal) ||
                    renderer.bounds.size.sqrMagnitude <
                    0.0025f)
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
                    bounds.Encapsulate(
                        renderer.bounds);
                }
            }
        }

        private float GetTvGroupYaw(
            GameObject companion,
            Vector3 focusPosition)
        {
            if (companion == null ||
                playerAvatar == null)
            {
                return orbitYaw;
            }

            Vector3 separation =
                playerAvatar.position -
                companion.transform.position;
            separation.y = 0f;
            if (separation.sqrMagnitude <
                0.0025f)
            {
                return GetFaceForwardYaw(
                    companion);
            }

            Vector3 cameraSide =
                Vector3.Cross(
                    Vector3.up,
                    separation.normalized);
            Vector3 currentSide =
                transform.position -
                focusPosition;
            currentSide.y = 0f;
            if (currentSide.sqrMagnitude >
                    0.0025f &&
                Vector3.Dot(
                    cameraSide,
                    currentSide.normalized) < 0f)
            {
                cameraSide = -cameraSide;
            }

            return Mathf.Atan2(
                cameraSide.x,
                cameraSide.z) *
                Mathf.Rad2Deg;
        }

        public void SetZoomInSlider(float normalizedValue)
        {
            sliderZoomInValue = Mathf.Clamp01(normalizedValue);
            sliderZoomOutValue = 0f;
            hasSliderInput = true;
        }

        public void SetZoomOutSlider(float normalizedValue)
        {
            sliderZoomOutValue = Mathf.Clamp01(normalizedValue);
            sliderZoomInValue = 0f;
            hasSliderInput = true;
        }

        private void ApplySliderZoom()
        {
            if (!hasSliderInput)
            {
                return;
            }
            // Zero on both sliders must preserve the current neutral framing.
            // Mapping through Lerp made the first slider movement jump to the
            // midpoint of the zoom range.
            float sliderDrivenZoom =
                (sliderZoomInValue * minZoom) +
                (sliderZoomOutValue * maxZoom);
            zoomOffset = Mathf.Clamp(sliderDrivenZoom, minZoom, maxZoom);
        }

        private void ProcessSceneNavigation()
        {
            if (Keyboard.current == null || cam == null || IsTypingIntoTextField())
            {
                return;
            }

            if (exploreMode)
            {
                return;
            }

            float speedMultiplier =
                Keyboard.current.leftShiftKey.isPressed
                    ? sprintMultiplier
                    : 1f;

            float orbitInput = 0f;
            if (Keyboard.current.aKey.isPressed) orbitInput -= 1f;
            if (Keyboard.current.dKey.isPressed) orbitInput += 1f;
            bool commandHeld =
                Keyboard.current.leftCommandKey.isPressed ||
                Keyboard.current.rightCommandKey.isPressed;
            if (!commandHeld &&
                Keyboard.current.zKey.isPressed)
            {
                orbitInput -= 1f;
            }
            if (Keyboard.current.cKey.isPressed)
            {
                orbitInput += 1f;
            }
            orbitYaw +=
                orbitInput *
                keyboardOrbitSpeed *
                speedMultiplier *
                Time.deltaTime;

            float zoomInput = 0f;
            if (Keyboard.current.wKey.isPressed) zoomInput -= 1f;
            if (Keyboard.current.sKey.isPressed) zoomInput += 1f;
            zoomOffset = Mathf.Clamp(
                zoomOffset +
                zoomInput *
                keyboardZoomSpeed *
                speedMultiplier *
                Time.deltaTime,
                minZoom,
                maxZoom);

            float verticalInput = 0f;
            if (Keyboard.current.qKey.isPressed) verticalInput -= 1f;
            if (Keyboard.current.eKey.isPressed) verticalInput += 1f;
            orbitPitch = Mathf.Clamp(
                orbitPitch -
                verticalInput *
                keyboardPitchSpeed *
                speedMultiplier *
                Time.deltaTime,
                minPitch,
                maxPitch);
        }

        private void ProcessExploreNavigation()
        {
            EnsureExplorePose();

            if (playerAvatar != null && exploreFollowingPlayer)
            {
                ProcessPlayerNavigation();
                return;
            }

            if (Keyboard.current != null && !IsTypingIntoTextField())
            {
                Vector3 localMove = Vector3.zero;
                if (Keyboard.current.wKey.isPressed) localMove += Vector3.forward;
                if (Keyboard.current.sKey.isPressed) localMove += Vector3.back;
                if (Keyboard.current.aKey.isPressed) localMove += Vector3.left;
                if (Keyboard.current.dKey.isPressed) localMove += Vector3.right;
                if (Keyboard.current.qKey.isPressed) localMove += Vector3.down;
                if (Keyboard.current.eKey.isPressed) localMove += Vector3.up;

                if (localMove.sqrMagnitude > 0f)
                {
                    Quaternion lookRotation =
                        Quaternion.Euler(explorePitch, exploreYaw, 0f);
                    Vector3 worldMove =
                        (lookRotation * Vector3.forward * localMove.z) +
                        (lookRotation * Vector3.right * localMove.x) +
                        (Vector3.up * localMove.y);
                    float speed = moveSpeed *
                        ((Keyboard.current.leftShiftKey.isPressed ||
                          Keyboard.current.rightShiftKey.isPressed)
                            ? sprintMultiplier
                            : 1f);
                    explorePosition +=
                        worldMove.normalized * speed * Time.deltaTime;
                }
            }

            explorePosition.y = Mathf.Clamp(
                explorePosition.y,
                navigationMinHeight +
                    groundClearance,
                navigationMaxHeight);
            transform.SetPositionAndRotation(
                explorePosition,
                Quaternion.Euler(explorePitch, exploreYaw, 0f));
            currentTargetCamPos = explorePosition;
            isInitialized = true;
        }

        private void ProcessPlayerNavigation()
        {
            Vector3 localMove = Vector3.zero;
            if (Keyboard.current != null && !IsTypingIntoTextField())
            {
                if (Keyboard.current.wKey.isPressed)
                    localMove += Vector3.forward;
                if (Keyboard.current.sKey.isPressed)
                    localMove += Vector3.back;
                if (Keyboard.current.aKey.isPressed)
                    localMove += Vector3.left;
                if (Keyboard.current.dKey.isPressed)
                    localMove += Vector3.right;
                if (Keyboard.current.qKey.isPressed)
                    localMove += Vector3.down;
                if (Keyboard.current.eKey.isPressed)
                    localMove += Vector3.up;
            }

            Quaternion planarLook =
                Quaternion.Euler(0f, exploreYaw, 0f);
            Vector3 horizontalMove =
                (planarLook * Vector3.forward * localMove.z) +
                (planarLook * Vector3.right * localMove.x);
            Vector3 worldMove =
                horizontalMove +
                Vector3.up *
                (followWorldSurface
                    ? 0f
                    : localMove.y);
            bool sprinting =
                Keyboard.current != null &&
                (Keyboard.current.leftShiftKey.isPressed ||
                 Keyboard.current.rightShiftKey.isPressed);
            float appliedSpeed = 0f;

            if (worldMove.sqrMagnitude > 0f)
            {
                RuntimeHumanoidPosePlayer activePlayerPose =
                    playerSeatedPose != null
                        ? playerSeatedPose
                        : playerAvatar.GetComponent<
                            RuntimeHumanoidPosePlayer>();
                if (activePlayerPose != null)
                {
                    activePlayerPose.SetPoseActive(
                        false,
                        playerAvatar.position.y);
                }

                float speed = moveSpeed *
                    (sprinting
                        ? sprintMultiplier
                        : 1f);
                horizontalMove =
                    ApplyStreetHeadingAssist(
                        horizontalMove,
                        localMove);
                worldMove =
                    horizontalMove +
                    Vector3.up *
                    (followWorldSurface
                        ? 0f
                        : localMove.y);
                appliedSpeed = speed *
                    horizontalMove.magnitude /
                    worldMove.magnitude;
                Vector3 previousPosition =
                    playerAvatar.position;
                Vector3 proposedPosition =
                    previousPosition +
                    (followWorldSurface
                        ? horizontalMove.normalized
                        : worldMove.normalized) *
                    speed *
                    Time.deltaTime;
                playerAvatar.position =
                    proposedPosition;
            }
            else
            {
                hasStreetHeading = false;
            }

            Vector3 playerPosition = playerAvatar.position;
            if (followWorldSurface)
            {
                playerPosition =
                    ApplyPlayerJumpAndGrounding(
                        playerPosition);
            }
            playerPosition.y = Mathf.Clamp(
                playerPosition.y,
                navigationMinHeight,
                Mathf.Max(
                    navigationMinHeight,
                    navigationMaxHeight - 1f));
            playerAvatar.position = playerPosition;
            playerLocomotion?.SetMotion(
                appliedSpeed,
                sprinting,
                followWorldSurface
                    ? !playerGrounded
                    : playerPosition.y >
                        navigationMinHeight + 0.05f);

            Quaternion cameraRotation =
                Quaternion.Euler(
                    explorePitch,
                    exploreYaw,
                    0f);
            Quaternion bodyRotation =
                playerViewStyle ==
                    PlayerViewStyle.OverTheShoulder
                    ? horizontalMove.sqrMagnitude > 0.001f
                        ? Quaternion.LookRotation(
                            horizontalMove.normalized,
                            Vector3.up)
                        : playerAvatar.rotation
                    : Quaternion.Euler(
                        0f,
                        exploreYaw,
                        0f);
            playerAvatar.rotation = Quaternion.Slerp(
                playerAvatar.rotation,
                bodyRotation,
                Time.deltaTime * playerTurnSpeed);

            if (playerViewStyle ==
                PlayerViewStyle.OverTheShoulder)
            {
                PositionOverShoulderCamera(
                    cameraRotation);
                SetPlayerRenderersVisible(true);
                if (cam != null)
                {
                    cam.nearClipPlane =
                        characterNearClipPlane;
                }
                return;
            }

            bool showingBody =
                explorePitch >= PovBodyRevealPitch;
            Vector3 pivot =
                GetLivePlayerEyePivot(bodyRotation);
            explorePosition = ClampAboveGround(
                pivot +
                cameraRotation *
                Vector3.forward *
                povLeanDistance);
            transform.SetPositionAndRotation(
                explorePosition,
                cameraRotation);
            SetPlayerRenderersVisible(showingBody);
            if (cam != null)
            {
                cam.nearClipPlane =
                    showingBody
                        ? PovBodyNearClipPlane
                        : PovNearClipPlane;
            }
            currentTargetCamPos = explorePosition;
            isInitialized = true;
        }

        private bool ProcessTouchPinchZoom()
        {
            if (!exploreMode ||
                !exploreFollowingPlayer ||
                Touchscreen.current == null)
            {
                pinchGestureActive = false;
                return false;
            }

            var touches = Touchscreen.current.touches;
            if (touches.Count < 2 ||
                !touches[0].press.isPressed ||
                !touches[1].press.isPressed)
            {
                pinchGestureActive = false;
                return false;
            }

            float pinchDistance =
                Vector2.Distance(
                    touches[0].position.ReadValue(),
                    touches[1].position.ReadValue());
            if (!pinchGestureActive)
            {
                pinchGestureActive = true;
                previousPinchDistance = pinchDistance;
                return false;
            }

            float pinchDelta =
                pinchDistance - previousPinchDistance;
            previousPinchDistance = pinchDistance;
            if (Mathf.Abs(pinchDelta) < 0.01f)
            {
                return false;
            }

            ApplyPlayerPinchZoom(pinchDelta);
            return true;
        }

        private void ApplyPlayerPinchZoom(float pinchDelta)
        {
            if (playerViewStyle ==
                PlayerViewStyle.OverTheShoulder)
            {
                shoulderDistance = Mathf.Clamp(
                    shoulderDistance -
                    pinchDelta *
                    playerPinchZoomSensitivity,
                    minShoulderDistance,
                    maxShoulderDistance);
                return;
            }

            if (cam != null)
            {
                cam.fieldOfView = Mathf.Clamp(
                    cam.fieldOfView -
                    pinchDelta *
                    povPinchFovSensitivity,
                    minPovFieldOfView,
                    maxPovFieldOfView);
            }
        }

        private Vector3 ApplyStreetHeadingAssist(
            Vector3 requestedMove,
            Vector3 localMove)
        {
            if (!followWorldSurface ||
                requestedMove.sqrMagnitude < 0.001f)
            {
                hasStreetHeading = false;
                return requestedMove;
            }

            // A/D always wins so intersections and intentional turns remain
            // responsive. The assist only keeps a forward/back street run
            // from slowly curving as the shoulder camera moves.
            bool movingStraight =
                Mathf.Abs(localMove.z) > 0.1f &&
                Mathf.Abs(localMove.x) < 0.1f;
            if (!movingStraight)
            {
                hasStreetHeading = false;
                return requestedMove;
            }

            Vector3 requestedDirection =
                requestedMove.normalized;
            Vector3 forwardDirection =
                localMove.z >= 0f
                    ? requestedDirection
                    : -requestedDirection;
            if (!hasStreetHeading ||
                Vector3.Angle(
                    streetHeading,
                    forwardDirection) >=
                    streetHeadingTurnThreshold)
            {
                float requestedYaw =
                    Mathf.Atan2(
                        forwardDirection.x,
                        forwardDirection.z) *
                    Mathf.Rad2Deg;
                float snappedYaw =
                    Mathf.Round(
                        requestedYaw /
                        Mathf.Max(
                            1f,
                            streetHeadingIncrement)) *
                    Mathf.Max(
                        1f,
                        streetHeadingIncrement);
                streetHeading =
                    Quaternion.Euler(
                        0f,
                        snappedYaw,
                        0f) *
                    Vector3.forward;
                hasStreetHeading = true;
            }

            Vector3 assistedDirection =
                Vector3.Slerp(
                    requestedDirection,
                    localMove.z >= 0f
                        ? streetHeading
                        : -streetHeading,
                    1f -
                    Mathf.Exp(
                        -streetHeadingAssistSpeed *
                        Time.deltaTime));
            return assistedDirection.normalized *
                requestedMove.magnitude;
        }

        private void TryStartPlayerJump()
        {
            if (!followWorldSurface ||
                !playerGrounded)
            {
                return;
            }

            playerVerticalVelocity =
                Mathf.Sqrt(
                    Mathf.Max(
                        0.1f,
                        jumpHeight) *
                    -2f *
                    Mathf.Min(
                        -0.1f,
                        gravity));
            playerGrounded = false;
        }

        private Vector3 ApplyPlayerJumpAndGrounding(
            Vector3 position)
        {
            float previousY = position.y;
            if (!playerGrounded)
            {
                playerVerticalVelocity +=
                    gravity * Time.deltaTime;
                position.y +=
                    playerVerticalVelocity *
                    Time.deltaTime;
            }

            if (!TryFindNearbySurface(
                    position,
                    out float surfaceY))
            {
                playerGrounded = false;
                return position;
            }

            if (playerGrounded)
            {
                position.y = surfaceY;
                playerVerticalVelocity = 0f;
                return position;
            }

            bool falling =
                playerVerticalVelocity <= 0f;
            bool crossedSurface =
                position.y <= surfaceY + 0.12f &&
                previousY >= surfaceY - 0.12f;
            if (falling && crossedSurface)
            {
                position.y = surfaceY;
                playerVerticalVelocity = 0f;
                playerGrounded = true;
            }
            return position;
        }

        private bool TryFindNearbySurface(
            Vector3 position,
            out float surfaceY)
        {
            Vector3 origin =
                position +
                Vector3.up * 1.5f;
            RaycastHit[] hits =
                Physics.RaycastAll(
                    origin,
                    Vector3.down,
                    5f,
                    Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore);
            bool found = false;
            float bestDistance = float.MaxValue;
            surfaceY = position.y;
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider == null ||
                    hit.normal.y < 0.45f ||
                    (playerAvatar != null &&
                     hit.collider.transform.IsChildOf(
                         playerAvatar)))
                {
                    continue;
                }

                float distance =
                    Mathf.Abs(
                        hit.point.y - position.y);
                if (distance >= bestDistance)
                {
                    continue;
                }

                bestDistance = distance;
                surfaceY = hit.point.y;
                found = true;
            }
            return found;
        }

        private void PositionOverShoulderCamera(
            Quaternion orbitRotation)
        {
            Vector3 eyePivot =
                GetLivePlayerEyePivot(
                    playerAvatar.rotation);
            Vector3 lookTarget =
                eyePivot +
                Vector3.up * shoulderLookHeight;
            Vector3 desiredPosition =
                lookTarget +
                orbitRotation *
                new Vector3(
                    shoulderSideOffset,
                    shoulderHeight,
                    -shoulderDistance);
            desiredPosition =
                ResolveShoulderObstruction(
                    lookTarget,
                    desiredPosition);
            desiredPosition =
                ClampAboveGround(
                    desiredPosition);

            Vector3 lookDirection =
                lookTarget - desiredPosition;
            Quaternion lookRotation =
                lookDirection.sqrMagnitude > 0.001f
                    ? Quaternion.LookRotation(
                        lookDirection.normalized,
                        Vector3.up)
                    : orbitRotation;
            explorePosition = desiredPosition;
            transform.SetPositionAndRotation(
                explorePosition,
                lookRotation);
            currentTargetCamPos = explorePosition;
            isInitialized = true;
        }

        private Vector3 ResolveShoulderObstruction(
            Vector3 lookTarget,
            Vector3 desiredPosition)
        {
            Vector3 offset =
                desiredPosition - lookTarget;
            float distance = offset.magnitude;
            if (distance <= 0.05f)
            {
                return desiredPosition;
            }

            RaycastHit[] hits =
                Physics.SphereCastAll(
                    lookTarget,
                    0.18f,
                    offset / distance,
                    distance,
                    Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore);
            float nearestDistance = distance;
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider == null ||
                    (playerAvatar != null &&
                     hit.collider.transform.IsChildOf(
                         playerAvatar)))
                {
                    continue;
                }

                nearestDistance =
                    Mathf.Min(
                        nearestDistance,
                        hit.distance);
            }

            if (nearestDistance >= distance)
            {
                return desiredPosition;
            }

            return lookTarget +
                offset.normalized *
                Mathf.Max(
                    0.35f,
                    nearestDistance - 0.12f);
        }

        private Vector3 FollowNearbySurface(
            Vector3 previousPosition,
            Vector3 proposedPosition)
        {
            Vector3 origin =
                proposedPosition +
                Vector3.up * 1.5f;
            RaycastHit[] hits =
                Physics.RaycastAll(
                    origin,
                    Vector3.down,
                    6f,
                    Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore);
            bool found = false;
            float bestDistance = float.MaxValue;
            float surfaceY = previousPosition.y;
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider == null ||
                    hit.normal.y < 0.45f ||
                    (playerAvatar != null &&
                     hit.collider.transform.IsChildOf(
                         playerAvatar)))
                {
                    continue;
                }

                float step =
                    Mathf.Abs(
                        hit.point.y -
                        previousPosition.y);
                if (step > 2.25f ||
                    step >= bestDistance)
                {
                    continue;
                }

                bestDistance = step;
                surfaceY = hit.point.y;
                found = true;
            }

            proposedPosition.y =
                found
                    ? surfaceY
                    : previousPosition.y;
            return proposedPosition;
        }

        private void EnsureExplorePose()
        {
            if (!hasExplorePose)
            {
                CaptureExplorePose();
            }
        }

        private void CaptureExplorePose()
        {
            explorePosition = ClampAboveGround(transform.position);
            Vector3 euler = transform.eulerAngles;
            exploreYaw = euler.y;
            explorePitch = Mathf.Clamp(
                Mathf.DeltaAngle(0f, euler.x),
                minExplorePitch,
                maxExplorePitch);
            hasExplorePose = true;
        }

        public void SetExploreMode(bool enabled)
        {
            exploreMode = enabled;
            isTvGroupFraming = false;
            if (!exploreMode)
            {
                SetPlayerVisible(true);
                SetPlayerRenderersVisible(true);
                if (cam != null)
                {
                    cam.nearClipPlane = characterNearClipPlane;
                }
                hasManualFocus = false;
                sceneOffset = Vector3.zero;
                focusVelocity = Vector3.zero;
            }
            else
            {
                hasManualFocus = false;
                exploreFollowingPlayer = true;
                SetPlayerVisible(true);
                SetPlayerRenderersVisible(
                    playerViewStyle ==
                    PlayerViewStyle.OverTheShoulder);
                if (cam != null)
                {
                    cam.nearClipPlane =
                        playerViewStyle ==
                        PlayerViewStyle.OverTheShoulder
                            ? characterNearClipPlane
                            : PovNearClipPlane;
                }
                if (playerAvatar != null)
                {
                    exploreYaw =
                        playerAvatar.eulerAngles.y;
                    explorePitch = 0f;
                    hasExplorePose = true;
                }
                else
                {
                    CaptureExplorePose();
                }
            }
        }

        public void SetPlayerAvatar(Transform player)
        {
            playerAvatar = player;
            playerLocomotion =
                playerAvatar.GetComponent<PlayerLocomotionAnimator>();
            if (playerLocomotion == null)
            {
                playerLocomotion =
                    playerAvatar.gameObject.AddComponent<
                        PlayerLocomotionAnimator>();
            }
            exploreFollowingPlayer = true;
            CachePlayerCameraPivot();
            SetPlayerVisible(true);
            SetPlayerRenderersVisible(
                !exploreMode ||
                playerViewStyle ==
                    PlayerViewStyle.OverTheShoulder);
            if (cam != null && exploreMode)
            {
                cam.nearClipPlane =
                    playerViewStyle ==
                    PlayerViewStyle.OverTheShoulder
                        ? characterNearClipPlane
                        : PovNearClipPlane;
            }
        }

        public void SetPlayerViewStyle(
            PlayerViewStyle viewStyle)
        {
            playerViewStyle = viewStyle;
            povLeanDistance = 0f;
            exploreFollowingPlayer = true;
            hasExplorePose = false;
            SetPlayerVisible(true);
            SetPlayerRenderersVisible(
                viewStyle ==
                PlayerViewStyle.OverTheShoulder);
            if (cam != null)
            {
                cam.nearClipPlane =
                    viewStyle ==
                    PlayerViewStyle.OverTheShoulder
                        ? characterNearClipPlane
                        : PovNearClipPlane;
            }
        }

        public void ConfigureWorldNavigation(
            float minimumHeight,
            float maximumHeight,
            float walkingSpeed)
        {
            navigationMinHeight = minimumHeight;
            navigationMaxHeight =
                Mathf.Max(
                    minimumHeight + 2f,
                    maximumHeight);
            maxExploreHeight = navigationMaxHeight;
            moveSpeed = Mathf.Max(1f, walkingSpeed);
            followWorldSurface = true;
            playerGrounded = false;
            playerVerticalVelocity = 0f;
        }

        public void TeleportPlayer(
            Vector3 worldPosition)
        {
            if (playerAvatar == null)
            {
                return;
            }

            playerAvatar.position =
                worldPosition;
            playerVerticalVelocity = 0f;
            playerGrounded = false;
            exploreFollowingPlayer = true;
            hasExplorePose = false;
            SetPlayerVisible(true);
            SetPlayerRenderersVisible(
                playerViewStyle ==
                PlayerViewStyle.OverTheShoulder);
        }

        private void UpdateCharacterInteraction(
            GameObject targetGo)
        {
            if (exploreMode)
            {
                return;
            }

            if (playerAvatar == null ||
                !playerAvatar.gameObject.activeInHierarchy ||
                targetGo == null)
            {
                return;
            }

            // Character/POV mode embodies the already-loaded Jojo4 avatar
            // at its current (and saveable) world transform. Posing a scene
            // character must never relocate or replace the player avatar.

            Vector3 playerToCompanion =
                targetGo.transform.position -
                playerAvatar.position;
            playerToCompanion.y = 0f;

            Vector3 companionToPlayer =
                playerAvatar.position -
                targetGo.transform.position;
            companionToPlayer.y = 0f;

            if (playerToCompanion.sqrMagnitude > 0.01f)
            {
                Quaternion playerFacing =
                    Quaternion.LookRotation(
                        playerToCompanion.normalized,
                        Vector3.up);
                playerAvatar.rotation = Quaternion.Slerp(
                    playerAvatar.rotation,
                    playerFacing,
                    Time.deltaTime *
                    conversationTurnSpeed);
            }

            if (companionToPlayer.sqrMagnitude > 0.01f)
            {
                Quaternion companionFacing =
                    Quaternion.LookRotation(
                        companionToPlayer.normalized,
                        Vector3.up);
                targetGo.transform.rotation =
                    Quaternion.Slerp(
                        targetGo.transform.rotation,
                        companionFacing,
                        Time.deltaTime *
                        conversationTurnSpeed);
            }

            playerLocomotion?.SetMotion(
                0f,
                false,
                false);
        }

        public bool TryGetPlayerTransform(
            out Vector3 position,
            out Quaternion rotation)
        {
            if (playerAvatar == null)
            {
                position = Vector3.zero;
                rotation = Quaternion.identity;
                return false;
            }

            position = playerAvatar.position;
            rotation = playerAvatar.rotation;
            return true;
        }

        public void ApplyPlayerTransform(
            Vector3 position,
            Quaternion rotation)
        {
            if (playerAvatar == null)
            {
                return;
            }

            playerAvatar.SetPositionAndRotation(
                position,
                rotation);
            hasExplorePose = false;
        }

        private void SetPlayerVisible(bool visible)
        {
            if (playerAvatar != null &&
                playerAvatar.gameObject.activeSelf != visible)
            {
                playerAvatar.gameObject.SetActive(visible);
            }
        }

        private void SetPlayerRenderersVisible(bool visible)
        {
            if (playerAvatar == null ||
                playerRenderersVisible == visible)
            {
                return;
            }

            Renderer[] renderers =
                playerAvatar.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer renderer in renderers)
            {
                if (renderer != null)
                {
                    renderer.enabled = visible;
                }
            }
            playerRenderersVisible = visible;
        }

        private Vector3 GetLivePlayerEyePivot(
            Quaternion bodyRotation)
        {
            if (playerAvatar == null)
            {
                return transform.position;
            }

            Animator animator =
                playerAvatar.GetComponent<Animator>();
            if (animator != null && animator.isHuman)
            {
                Transform leftEye =
                    animator.GetBoneTransform(
                        HumanBodyBones.LeftEye);
                Transform rightEye =
                    animator.GetBoneTransform(
                        HumanBodyBones.RightEye);
                if (leftEye != null && rightEye != null)
                {
                    return Vector3.Lerp(
                               leftEye.position,
                               rightEye.position,
                               0.5f) +
                           bodyRotation *
                           Vector3.forward *
                           PovEyeForwardOffset;
                }

                Transform head =
                    animator.GetBoneTransform(
                        HumanBodyBones.Head);
                if (head != null)
                {
                    return head.position +
                           Vector3.up * 0.08f +
                           bodyRotation *
                           Vector3.forward * 0.08f;
                }
            }

            return playerAvatar.TransformPoint(
                playerLocalCameraPivot);
        }

        private void CachePlayerCameraPivot()
        {
            if (playerAvatar == null)
            {
                return;
            }

            Animator animator =
                playerAvatar.GetComponent<Animator>();
            if (animator != null && animator.isHuman)
            {
                Transform head =
                    animator.GetBoneTransform(
                        HumanBodyBones.Head);
                if (head != null)
                {
                    playerLocalCameraPivot =
                        playerAvatar.InverseTransformPoint(
                            head.position);
                    return;
                }
            }

            Bounds bounds =
                new Bounds(
                    playerAvatar.position + Vector3.up,
                    Vector3.one);
            bool hasBounds = false;
            foreach (Renderer renderer in
                playerAvatar.GetComponentsInChildren<Renderer>())
            {
                if (!renderer.enabled)
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

            playerLocalCameraPivot =
                playerAvatar.InverseTransformPoint(
                    bounds.center +
                    Vector3.up * bounds.extents.y * 0.2f);
        }

        public void SetTrackedTarget(Transform target)
        {
            selectedTarget = target;
            framedTarget = null;
            hasManualFocus = false;
        }

        public void ClearTrackedTarget()
        {
            SetTrackedTarget(null);
        }

        private GameObject ResolveTrackedTarget()
        {
            if (selectedTarget != null &&
                selectedTarget.gameObject.activeInHierarchy)
            {
                return selectedTarget.gameObject;
            }

            GameObject namedTarget = GameObject.Find("CompanionAvatarVRM");
            GameObject nearestTarget = namedTarget;
            float nearestDistance = namedTarget != null
                ? (namedTarget.transform.position - transform.position).sqrMagnitude
                : float.PositiveInfinity;

            Animator[] animators =
                UnityEngine.Object.FindObjectsByType<Animator>(
                    FindObjectsInactive.Exclude);
            foreach (Animator animator in animators)
            {
                if (animator == null || !animator.isHuman)
                {
                    continue;
                }

                GameObject candidate = animator.gameObject;
                if (candidate.name.IndexOf(
                        "CompanionAvatar",
                        System.StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                float distance =
                    (candidate.transform.position - transform.position)
                    .sqrMagnitude;
                if (distance < nearestDistance)
                {
                    nearestTarget = candidate;
                    nearestDistance = distance;
                }
            }

            return nearestTarget != null
                ? nearestTarget
                : GameObject.Find("CompanionAvatarProcedural");
        }

        private float GetFaceForwardYaw(GameObject targetGo)
        {
            Vector3 faceForward =
                targetGo.transform.TransformDirection(
                    stableLocalFaceForward);
            faceForward.y = 0f;
            if (faceForward.sqrMagnitude < 0.001f)
            {
                return orbitYaw;
            }

            faceForward.Normalize();
            return Mathf.Atan2(
                faceForward.x,
                faceForward.z) * Mathf.Rad2Deg;
        }

        private float CalculateFullBodyFitDistance(
            GameObject targetGo,
            Vector3 focusPosition,
            Vector3 cameraOffsetDirection)
        {
            Vector3[] framingCorners =
                isTvGroupFraming
                    ? tvGroupBoundsCorners
                    : stableLocalFullBodyBoundsCorners;
            if (cam == null ||
                framingCorners == null ||
                framingCorners.Length == 0)
            {
                return minCameraDistance;
            }

            Vector3 viewDirection = -cameraOffsetDirection.normalized;
            Quaternion viewRotation =
                Quaternion.LookRotation(viewDirection, Vector3.up);
            Quaternion inverseViewRotation =
                Quaternion.Inverse(viewRotation);
            float verticalTangent =
                Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float horizontalTangent =
                verticalTangent * Mathf.Max(cam.aspect, 0.1f);
            float requiredDistance = minCameraDistance;

            foreach (Vector3 framingCorner in framingCorners)
            {
                Vector3 worldCorner =
                    isTvGroupFraming
                        ? framingCorner
                        : targetGo.transform.TransformPoint(
                            framingCorner);
                Vector3 viewRelative =
                    inverseViewRotation *
                    (worldCorner - focusPosition);

                float horizontalFit =
                    Mathf.Abs(viewRelative.x) *
                    padding /
                    Mathf.Max(horizontalTangent, 0.01f);
                float verticalFit =
                    Mathf.Abs(viewRelative.y) *
                    padding /
                    Mathf.Max(verticalTangent, 0.01f);
                float nearPlaneFit =
                    cam.nearClipPlane + 0.05f;

                requiredDistance = Mathf.Max(
                    requiredDistance,
                    Mathf.Max(
                        horizontalFit,
                        Mathf.Max(verticalFit, nearPlaneFit)) -
                    viewRelative.z);
            }

            return requiredDistance;
        }

        private float CalculateAvatarFitDistance(
            GameObject avatar,
            Vector3 focusPosition,
            Vector3 cameraOffsetDirection)
        {
            if (cam == null ||
                !TryGetAvatarBounds(avatar, out Bounds bounds))
            {
                return minCameraDistance;
            }

            Quaternion inverseViewRotation =
                Quaternion.Inverse(
                    Quaternion.LookRotation(
                        -cameraOffsetDirection.normalized,
                        Vector3.up));
            float verticalTangent =
                Mathf.Tan(
                    cam.fieldOfView *
                    0.5f *
                    Mathf.Deg2Rad);
            float horizontalTangent =
                verticalTangent * Mathf.Max(cam.aspect, 0.1f);
            float requiredDistance = minCameraDistance;

            foreach (Vector3 worldCorner in GetBoundsCorners(bounds))
            {
                Vector3 relative =
                    inverseViewRotation *
                    (worldCorner - focusPosition);
                float horizontalFit =
                    Mathf.Abs(relative.x) *
                    padding /
                    Mathf.Max(horizontalTangent, 0.01f);
                float verticalFit =
                    Mathf.Abs(relative.y) *
                    padding /
                    Mathf.Max(verticalTangent, 0.01f);
                requiredDistance = Mathf.Max(
                    requiredDistance,
                    Mathf.Max(horizontalFit, verticalFit) -
                    relative.z);
            }

            return requiredDistance;
        }

        private static bool TryGetCombinedAvatarBounds(
            GameObject first,
            GameObject second,
            out Bounds bounds)
        {
            bool hasFirst =
                TryGetAvatarBounds(first, out bounds);
            if (!TryGetAvatarBounds(
                    second,
                    out Bounds secondBounds))
            {
                return hasFirst;
            }

            if (!hasFirst)
            {
                bounds = secondBounds;
            }
            else
            {
                bounds.Encapsulate(secondBounds);
            }
            return true;
        }

        private static bool TryGetAvatarBounds(
            GameObject avatar,
            out Bounds bounds)
        {
            bounds = new Bounds();
            bool found = false;
            if (avatar == null)
            {
                return false;
            }

            Renderer[] renderers =
                avatar.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer renderer in renderers)
            {
                if (renderer == null ||
                    !renderer.enabled ||
                    renderer.bounds.size.sqrMagnitude < 0.0025f)
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
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return found;
        }

        private static bool IsTypingIntoTextField()
        {
            GameObject selected = EventSystem.current != null
                ? EventSystem.current.currentSelectedGameObject
                : null;

            TMP_InputField inputField = selected != null
                ? selected.GetComponentInParent<TMP_InputField>()
                : null;
            if (inputField != null && inputField.isFocused)
            {
                return true;
            }

            UnityEngine.UI.InputField legacyInput =
                selected != null
                    ? selected.GetComponentInParent<
                        UnityEngine.UI.InputField>()
                    : null;
            return legacyInput != null &&
                legacyInput.isFocused;
        }
    }
}
