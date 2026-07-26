using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

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

        [Header("Scene Navigation")]
        [SerializeField] private float moveSpeed = 2.5f;
        [SerializeField] private float sprintMultiplier = 2.5f;
        [SerializeField] private float focusSmoothTime = 0.12f;

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

        private void Awake()
        {
            cam = GetComponent<Camera>();
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

            // 2. Compute dynamic bounding box of all active renderers
            Bounds bounds = new Bounds(targetGo.transform.position, Vector3.zero);
            Renderer[] renderers = targetGo.GetComponentsInChildren<Renderer>();
            bool hasBounds = false;

            foreach (var r in renderers)
            {
                if (r.bounds.size.magnitude > 0.05f)
                {
                    if (!hasBounds)
                    {
                        bounds = r.bounds;
                        hasBounds = true;
                    }
                    else
                    {
                        bounds.Encapsulate(r.bounds);
                    }
                }
            }

            if (!hasBounds)
            {
                bounds = new Bounds(targetGo.transform.position, new Vector3(1f, 1.8f, 1f));
            }

            // 3. Determine dynamic target focus position and height (Focus on head/face if available)
            Vector3 targetFocusPos = bounds.center;
            float targetHeight = bounds.size.y;

            Animator animator = targetGo.GetComponent<Animator>();
            if (animator != null && animator.isHuman)
            {
                Transform headBone = animator.GetBoneTransform(HumanBodyBones.Head);
                if (headBone != null)
                {
                    targetFocusPos = headBone.position;
                    // 0.6f gives a beautiful head-and-shoulders portrait close-up!
                    targetHeight = 0.6f;
                }
            }

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

                bool orbitDragPressed = rightPressed || (leftPressed && !isOrbitBlockedByAvatarDrag);

                // Handle Orbit Rotation with drag (right mouse always, left mouse unless dragging avatar)
                if (!pointerOverUi && orbitDragPressed && mouseDelta.sqrMagnitude > 0.01f)
                {
                    orbitYaw += mouseDelta.x * rotateSensitivity;
                    orbitPitch -= mouseDelta.y * rotateSensitivity;
                    orbitPitch = Mathf.Clamp(orbitPitch, minPitch, maxPitch);
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

            if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                // Space quickly re-centers the camera framing on the avatar.
                orbitYaw = 180f;
                orbitPitch = 10f;
                zoomOffset = 0f;
                isInteracting = true;
            }

            // Calculate target distance after zoom
            ApplySliderZoom();
            float finalDistance = baseDistance + zoomOffset;

            // Compute ideal/target position based on orbit angles
            // Convert Yaw/Pitch angles to look direction pointing back from center
            Quaternion orbitRotation = Quaternion.Euler(orbitPitch, orbitYaw, 0f);
            Vector3 targetOffsetDir = orbitRotation * Vector3.forward;
            Vector3 idealCamPos = focusPos + targetOffsetDir * finalDistance + positionOffset;

            if (!isInitialized)
            {
                transform.position = idealCamPos;
                transform.rotation = Quaternion.LookRotation(focusPos - transform.position);
                currentTargetCamPos = idealCamPos;
                isInitialized = true;
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
            if (Keyboard.current == null || cam == null)
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
            }

            if (Keyboard.current.rKey.wasPressedThisFrame)
            {
                sceneOffset = Vector3.zero;
            }
        }
    }
}
