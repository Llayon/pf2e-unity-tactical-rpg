using UnityEngine;

namespace PF2e.Camera
{
    /// <summary>
    /// ScriptableObject holding all tactical camera parameters.
    /// </summary>
    [CreateAssetMenu(fileName = "CameraSettings", menuName = "PF2e/Camera Settings")]
    public class TacticalCameraSettings : ScriptableObject
    {
        [Header("Distance (Zoom)")]
        public float minDistance = 5f;
        public float maxDistance = 30f;
        public float defaultDistance = 15f;
        public float zoomSpeed = 5f;
        public float zoomSmoothTime = 0.15f;

        [Header("Pitch")]
        public float minPitch = 30f;
        public float maxPitch = 75f;
        public float defaultPitch = 55f;
        [Tooltip("Pitch interpolates between minPitch (at maxDistance) and maxPitch (at minDistance)")]
        public bool pitchFollowsZoom = true;

        [Header("Yaw (Rotation)")]
        public float defaultYaw = 0f;
        public float rotationSpeed = 60f;
        public float rotationSmoothTime = 0.15f;

        [Header("Pan (Movement)")]
        public float panSpeed = 15f;
        public float panSmoothTime = 0.1f;
        public float edgeScrollSize = 10f;
        public bool enableEdgeScroll = true;

        [Header("Bounds")]
        public float boundsMinX = -5f;
        public float boundsMaxX = 25f;
        public float boundsMinZ = -5f;
        public float boundsMaxZ = 25f;
    }
}
