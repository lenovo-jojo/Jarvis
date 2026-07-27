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
        private bool isFaceFocusMode;
        private bool hasManualFocus;
        private Vector3 manualFocusPosition;
        private bool snapCameraThisFrame;

        private void Awake()
        {
            cam = GetComponent<Camera>();
            EnsureRuntimeDefaults();
        }

        public float OrbitYaw => orbitYaw;
        public float OrbitPitch => orbitPitch;
        public float ZoomOffset => zoomOffset;
        public Vector3 SceneOffset => sceneOffset;
        public bool ExploreMode => exploreMode;

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
            if (fullBodyFramingScale <= 0.1f)
            {
                fullBodyFramingScale = 1f;
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

            Vector3 activeLocalFocusPos = isFaceFocusMode
                ? stableLocalFaceFocusPos
                : stableLocalFullBodyFocusPos;
            Vector3 targetFocusPos =
                targetGo.transform.TransformPoint(activeLocalFocusPos);
            float targetHeight = isFaceFocusMode
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

            ProcessSceneNavigation(smoothedFocusPos + sceneOffset);
            Vector3 focusPos = hasManualFocus
                ? manualFocusPosition
                : smoothedFocusPos + sceneOffset;

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
                sceneOffset = Vector3.zero;
                hasManualFocus = false;
                isInteracting = true;
            }

            if (!IsTypingIntoTextField() &&
                Keyboard.current != null &&
                Keyboard.current.rKey.wasPressedThisFrame)
            {
                // Full-body reset.
                isFaceFocusMode = false;
                // The selected pose can be much wider/deeper than the avatar's
                // load pose. Capture it once on reset so the frame is stable
                // while still fitting the current seated silhouette.
                CacheStableFraming(targetGo);
                targetFocusPos = targetGo.transform.TransformPoint(
                    stableLocalFullBodyFocusPos);
                targetHeight = stableFullBodyFocusHeight;
                ResetToFraming(
                    targetFocusPos,
                    targetHeight,
                    -10f,
                    GetFaceForwardYaw(targetGo));
                focusPos = targetFocusPos;
                isInteracting = true;
            }

            if (!IsTypingIntoTextField() &&
                Keyboard.current != null &&
                Keyboard.current.fKey.wasPressedThisFrame)
            {
                // Face reset.
                isFaceFocusMode = true;
                targetFocusPos = targetGo.transform.TransformPoint(
                    stableLocalFaceFocusPos);
                targetHeight = stableFaceFocusHeight;
                ResetToFraming(
                    targetFocusPos,
                    targetHeight,
                    0f,
                    GetFaceForwardYaw(targetGo));
                focusPos = targetFocusPos;
                isInteracting = true;
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
            if (!isFaceFocusMode)
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

        private void ProcessSceneNavigation(
            Vector3 currentAutomaticFocusPosition)
        {
            if (Keyboard.current == null || cam == null || IsTypingIntoTextField())
            {
                return;
            }

            if (!exploreMode)
            {
                float speedMultiplier =
                    Keyboard.current.leftShiftKey.isPressed
                        ? sprintMultiplier
                        : 1f;

                float orbitInput = 0f;
                if (Keyboard.current.aKey.isPressed) orbitInput -= 1f;
                if (Keyboard.current.dKey.isPressed) orbitInput += 1f;
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
                if (!hasManualFocus)
                {
                    manualFocusPosition =
                        currentAutomaticFocusPosition;
                    hasManualFocus = true;
                }

                float speed = moveSpeed * (Keyboard.current.leftShiftKey.isPressed ? sprintMultiplier : 1f);
                Vector3 worldMove =
                    (cam.transform.forward * moveInput.z) +
                    (cam.transform.right * moveInput.x) +
                    (Vector3.up * moveInput.y);
                manualFocusPosition +=
                    worldMove.normalized * speed * Time.deltaTime;

                float relativeHeight =
                    manualFocusPosition.y - smoothedFocusPos.y;
                manualFocusPosition.y =
                    smoothedFocusPos.y + Mathf.Clamp(
                    relativeHeight,
                    minVerticalFrameOffset,
                    maxVerticalFrameOffset);

                sceneOffset =
                    manualFocusPosition - smoothedFocusPos;
            }
        }

        public void SetExploreMode(bool enabled)
        {
            exploreMode = enabled;
            if (!exploreMode)
            {
                hasManualFocus = false;
                sceneOffset = Vector3.zero;
                focusVelocity = Vector3.zero;
            }
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
            if (cam == null ||
                stableLocalFullBodyBoundsCorners == null ||
                stableLocalFullBodyBoundsCorners.Length == 0)
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

            foreach (Vector3 localCorner in stableLocalFullBodyBoundsCorners)
            {
                Vector3 worldCorner =
                    targetGo.transform.TransformPoint(localCorner);
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

        private static bool IsTypingIntoTextField()
        {
            GameObject selected = EventSystem.current != null
                ? EventSystem.current.currentSelectedGameObject
                : null;

            TMP_InputField inputField = selected != null
                ? selected.GetComponentInParent<TMP_InputField>()
                : null;
            return inputField != null && inputField.isFocused;
        }
    }
}
