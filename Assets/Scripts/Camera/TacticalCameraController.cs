using UnityEngine;
using UnityEngine.InputSystem;

namespace PF2e.Camera
{
    /// <summary>
    /// Orbit camera controller for tactical gameplay.
    /// Hierarchy: CameraRig (this, XZ movement + Y rotation) → Main Camera (offset back × distance, pitch).
    /// Controls: WASD = pan (relative to yaw), Q/E = rotate, Scroll = zoom.
    /// Pitch changes with zoom when pitchFollowsZoom is enabled.
    /// </summary>
    public class TacticalCameraController : MonoBehaviour
    {
        [SerializeField] private TacticalCameraSettings settings;
        [SerializeField] private UnityEngine.Camera cam;

        // --- Current state ---
        private Vector3 focusPoint;
        private float yaw;
        private float distance;
        private float pitch;

        // --- SmoothDamp velocities ---
        private Vector3 panVelocity;
        private float yawVelocity;
        private float distanceVelocity;

        // --- Input cache ---
        private Vector2 moveInput;
        private float rotateInput;
        private float zoomInput;

        public Vector3 FocusPoint => focusPoint;
        public float Yaw => yaw;
        public float Distance => distance;
        public float Pitch => pitch;

        private void Awake()
        {
            if (settings == null)
            {
                Debug.LogError("[TacticalCamera] CameraSettings not assigned!", this);
                enabled = false;
                return;
            }

            if (cam == null)
            {
                cam = GetComponentInChildren<UnityEngine.Camera>();
                if (cam == null)
                {
                    Debug.LogError("[TacticalCamera] No Camera found in children!", this);
                    enabled = false;
                    return;
                }
            }

            // Initialize to defaults
            focusPoint = transform.position;
            yaw = settings.defaultYaw;
            distance = settings.defaultDistance;
            pitch = settings.defaultPitch;

            ApplyCameraTransform();
        }

        private void Update()
        {
            ReadInput();
            UpdatePan();
            UpdateRotation();
            UpdateZoom();
            ApplyCameraTransform();
        }

        private void ReadInput()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            // WASD
            moveInput = Vector2.zero;
            if (keyboard.wKey.isPressed) moveInput.y += 1f;
            if (keyboard.sKey.isPressed) moveInput.y -= 1f;
            if (keyboard.dKey.isPressed) moveInput.x += 1f;
            if (keyboard.aKey.isPressed) moveInput.x -= 1f;

            // Edge scroll
            if (settings.enableEdgeScroll)
            {
                var mouse = Mouse.current;
                if (mouse != null)
                {
                    var mousePos = mouse.position.ReadValue();
                    float edge = settings.edgeScrollSize;

                    if (mousePos.x < edge) moveInput.x -= 1f;
                    else if (mousePos.x > Screen.width - edge) moveInput.x += 1f;
                    if (mousePos.y < edge) moveInput.y -= 1f;
                    else if (mousePos.y > Screen.height - edge) moveInput.y += 1f;
                }
            }

            // Normalize to prevent diagonal speed boost
            if (moveInput.sqrMagnitude > 1f)
                moveInput.Normalize();

            // Q/E rotation
            rotateInput = 0f;
            if (keyboard.qKey.isPressed) rotateInput -= 1f;
            if (keyboard.eKey.isPressed) rotateInput += 1f;

            // Scroll zoom
            var scrollMouse = Mouse.current;
            zoomInput = scrollMouse != null ? scrollMouse.scroll.ReadValue().y : 0f;
        }

        private void UpdatePan()
        {
            if (moveInput == Vector2.zero && panVelocity.sqrMagnitude < 0.001f) return;

            // Move relative to camera yaw
            float yawRad = yaw * Mathf.Deg2Rad;
            Vector3 forward = new Vector3(Mathf.Sin(yawRad), 0f, Mathf.Cos(yawRad));
            Vector3 right = new Vector3(Mathf.Cos(yawRad), 0f, -Mathf.Sin(yawRad));

            Vector3 targetMove = (forward * moveInput.y + right * moveInput.x) * settings.panSpeed;
            Vector3 targetFocus = focusPoint + targetMove * Time.deltaTime;

            // Clamp to bounds
            targetFocus.x = Mathf.Clamp(targetFocus.x, settings.boundsMinX, settings.boundsMaxX);
            targetFocus.z = Mathf.Clamp(targetFocus.z, settings.boundsMinZ, settings.boundsMaxZ);

            focusPoint = Vector3.SmoothDamp(focusPoint, targetFocus, ref panVelocity, settings.panSmoothTime);
        }

        private void UpdateRotation()
        {
            if (rotateInput == 0f && Mathf.Abs(yawVelocity) < 0.01f) return;

            float targetYaw = yaw + rotateInput * settings.rotationSpeed * Time.deltaTime;
            yaw = Mathf.SmoothDamp(yaw, targetYaw, ref yawVelocity, settings.rotationSmoothTime);
        }

        private void UpdateZoom()
        {
            if (zoomInput == 0f && Mathf.Abs(distanceVelocity) < 0.01f) return;

            // Scroll up = zoom in (decrease distance), scroll down = zoom out
            float targetDistance = distance - zoomInput * settings.zoomSpeed * Time.deltaTime;
            targetDistance = Mathf.Clamp(targetDistance, settings.minDistance, settings.maxDistance);
            distance = Mathf.SmoothDamp(distance, targetDistance, ref distanceVelocity, settings.zoomSmoothTime);

            // Update pitch based on zoom
            if (settings.pitchFollowsZoom)
            {
                float t = Mathf.InverseLerp(settings.maxDistance, settings.minDistance, distance);
                pitch = Mathf.Lerp(settings.minPitch, settings.maxPitch, t);
            }
        }

        private void ApplyCameraTransform()
        {
            // Rig position = focus point, rotation = yaw around Y
            transform.position = focusPoint;
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            // Camera: offset back by distance, rotated by pitch
            if (cam != null)
            {
                // Camera looks at focus point from above-behind
                float pitchRad = pitch * Mathf.Deg2Rad;
                Vector3 offset = new Vector3(0f, Mathf.Sin(pitchRad), -Mathf.Cos(pitchRad)) * distance;
                cam.transform.localPosition = offset;
                cam.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            }
        }

        /// <summary>
        /// Set focus point directly (e.g. to center on grid).
        /// </summary>
        public void SetFocusPoint(Vector3 point)
        {
            focusPoint = point;
            panVelocity = Vector3.zero;
        }
    }
}
