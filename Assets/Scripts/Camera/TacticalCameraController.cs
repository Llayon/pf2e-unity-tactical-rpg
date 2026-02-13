using UnityEngine;
using UnityEngine.InputSystem;

namespace PF2e.Camera
{
    /// <summary>
    /// Orbit camera controller for tactical gameplay.
    /// CameraRig (this) → Main Camera (child, offset by distance + pitch).
    /// WASD = pan relative to yaw, Q/E = rotate, Scroll = zoom.
    /// </summary>
    public class TacticalCameraController : MonoBehaviour
    {
        [SerializeField] private TacticalCameraSettings settings;
        [SerializeField] private UnityEngine.Camera cam;

        private Vector3 focusPoint;
        private float yaw;
        private float distance;
        private float targetDistance;
        private float pitch;

        // SmoothDamp velocity — only for zoom
        private float distanceVelocity;

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

            focusPoint = transform.position;
            yaw = settings.defaultYaw;
            distance = settings.defaultDistance;
            targetDistance = settings.defaultDistance;
            pitch = settings.defaultPitch;

            ApplyCameraTransform();
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if (keyboard == null) return;

            float dt = Time.deltaTime;

            // === PAN (WASD + edge scroll) ===
            Vector2 moveInput = Vector2.zero;
            if (keyboard.wKey.isPressed) moveInput.y += 1f;
            if (keyboard.sKey.isPressed) moveInput.y -= 1f;
            if (keyboard.dKey.isPressed) moveInput.x += 1f;
            if (keyboard.aKey.isPressed) moveInput.x -= 1f;

            if (settings.enableEdgeScroll && mouse != null)
            {
                var mousePos = mouse.position.ReadValue();
                float edge = settings.edgeScrollSize;
                if (mousePos.x < edge) moveInput.x -= 1f;
                else if (mousePos.x > Screen.width - edge) moveInput.x += 1f;
                if (mousePos.y < edge) moveInput.y -= 1f;
                else if (mousePos.y > Screen.height - edge) moveInput.y += 1f;
            }

            if (moveInput.sqrMagnitude > 1f) moveInput.Normalize();

            if (moveInput != Vector2.zero)
            {
                float yawRad = yaw * Mathf.Deg2Rad;
                Vector3 forward = new Vector3(Mathf.Sin(yawRad), 0f, Mathf.Cos(yawRad));
                Vector3 right = new Vector3(Mathf.Cos(yawRad), 0f, -Mathf.Sin(yawRad));

                focusPoint += (forward * moveInput.y + right * moveInput.x) * settings.panSpeed * dt;
                focusPoint.x = Mathf.Clamp(focusPoint.x, settings.boundsMinX, settings.boundsMaxX);
                focusPoint.z = Mathf.Clamp(focusPoint.z, settings.boundsMinZ, settings.boundsMaxZ);
            }

            // === ROTATION (Q/E) ===
            float rotateInput = 0f;
            if (keyboard.qKey.isPressed) rotateInput -= 1f;
            if (keyboard.eKey.isPressed) rotateInput += 1f;

            if (rotateInput != 0f)
            {
                yaw += rotateInput * settings.rotationSpeed * dt;
            }

            // === ZOOM (Scroll) ===
            float scrollY = mouse != null ? mouse.scroll.ReadValue().y : 0f;
            if (scrollY != 0f)
            {
                float scrollNormalized = scrollY / 120f;
                targetDistance -= scrollNormalized * settings.zoomSpeed;
                targetDistance = Mathf.Clamp(targetDistance, settings.minDistance, settings.maxDistance);
            }

            distance = Mathf.SmoothDamp(distance, targetDistance, ref distanceVelocity, settings.zoomSmoothTime);

            if (settings.pitchFollowsZoom)
            {
                float t = Mathf.InverseLerp(settings.maxDistance, settings.minDistance, distance);
                pitch = Mathf.Lerp(settings.minPitch, settings.maxPitch, t);
            }

            ApplyCameraTransform();
        }

        private void ApplyCameraTransform()
        {
            transform.position = focusPoint;
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            if (cam != null)
            {
                float pitchRad = pitch * Mathf.Deg2Rad;
                Vector3 offset = new Vector3(0f, Mathf.Sin(pitchRad), -Mathf.Cos(pitchRad)) * distance;
                cam.transform.localPosition = offset;
                cam.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            }
        }

        public void SetFocusPoint(Vector3 point)
        {
            focusPoint = point;
        }
    }
}
