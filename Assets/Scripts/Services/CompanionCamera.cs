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

        private Camera cam;
        private Vector3 currentTargetCamPos;
        private bool isInitialized = false;

        private float orbitYaw = 180f; // Start at 180 degrees to face the avatar (who is rotated by 180)
        private float orbitPitch = 10f; // Slightly angled down
        private float zoomOffset = 0f;
        private bool isOrbitBlockedByAvatarDrag;

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

            // 3. Calculate optimal base framing distance to fit the full height of the avatar
            float height = bounds.size.y;
            float fov = cam != null ? cam.fieldOfView : 60f;
            float radFov = fov * Mathf.Deg2Rad;
            float baseDistance = (height / 2.0f) * padding / Mathf.Tan(radFov / 2.0f);

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
            float finalDistance = baseDistance + zoomOffset;

            // Compute ideal/target position based on orbit angles
            // Convert Yaw/Pitch angles to look direction pointing back from center
            Quaternion orbitRotation = Quaternion.Euler(orbitPitch, orbitYaw, 0f);
            Vector3 targetOffsetDir = orbitRotation * Vector3.forward;
            Vector3 idealCamPos = bounds.center + targetOffsetDir * finalDistance + positionOffset;

            if (!isInitialized)
            {
                transform.position = idealCamPos;
                transform.rotation = Quaternion.LookRotation(bounds.center - transform.position);
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
                // When stationary, check if the companion left the viewport margins
                bool leavesFrame = false;
                Vector3[] corners = GetBoundsCorners(bounds);
                foreach (var corner in corners)
                {
                    Vector3 viewportPoint = cam.WorldToViewportPoint(corner);
                    if (viewportPoint.z < 0f || viewportPoint.x < 0.15f || viewportPoint.x > 0.85f || viewportPoint.y < 0.15f || viewportPoint.y > 0.85f)
                    {
                        leavesFrame = true;
                        break;
                    }
                }

                // If she left the frame, re-target to her new centered position
                if (leavesFrame)
                {
                    currentTargetCamPos = idealCamPos;
                }
            }

            // 5. Smoothly transition to the target position & focus
            if (Vector3.Distance(transform.position, currentTargetCamPos) > 0.005f)
            {
                transform.position = Vector3.Lerp(transform.position, currentTargetCamPos, Time.deltaTime * smoothSpeed);
                Quaternion targetCamRot = Quaternion.LookRotation(bounds.center - transform.position);
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
    }
}
