using UnityEngine;
using UnityEngine.InputSystem;

namespace Eldoira.CameraControl
{
    /// <summary>
    /// XCOM-style tactical camera. Put this on a "rig" object at ground level;
    /// the Camera should be a child of it.
    ///   WASD / arrows  - pan
    ///   Q / E          - rotate 90° steps (smoothed)
    ///   Right mouse    - free orbit (drag)
    ///   Scroll wheel   - zoom
    ///   Middle mouse   - drag pan
    /// </summary>
    public class TacticalCameraController : MonoBehaviour
    {
        [SerializeField] private Camera cam;

        [Header("Pan")]
        [SerializeField] private float panSpeed = 20f;
        [SerializeField] private float dragPanSpeed = 0.05f;
        [SerializeField] private Vector2 boundsMin = new Vector2(-32f, -24f);
        [SerializeField] private Vector2 boundsMax = new Vector2(32f, 24f);

        [Header("Rotate")]
        [SerializeField] private float rotateStep = 90f;
        [SerializeField] private float orbitSpeed = 0.2f;
        [SerializeField] private float rotateSmoothing = 10f;

        [Header("Zoom")]
        [SerializeField] private float minDistance = 8f;
        [SerializeField] private float maxDistance = 70f;
        [SerializeField] private float zoomSpeed = 0.02f;
        [SerializeField] private float minPitch = 35f;
        [SerializeField] private float maxPitch = 70f;

        private float targetYaw;
        private float yaw;
        private float targetDistance = 40f;
        private float distance = 40f;

        private void Awake()
        {
            if (cam == null) cam = GetComponentInChildren<Camera>();
            yaw = targetYaw = transform.eulerAngles.y;
            ApplyCameraTransform();
        }

        private void Update()
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            float dt = Time.unscaledDeltaTime;

            // --- Pan ---
            Vector2 move = Vector2.zero;
            if (kb != null)
            {
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) move.y += 1;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) move.y -= 1;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) move.x += 1;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) move.x -= 1;

                if (kb.qKey.wasPressedThisFrame) targetYaw += rotateStep;
                if (kb.eKey.wasPressedThisFrame) targetYaw -= rotateStep;
            }

            Quaternion flatRot = Quaternion.Euler(0f, yaw, 0f);
            float zoomFactor = Mathf.Lerp(0.5f, 1.5f, Mathf.InverseLerp(minDistance, maxDistance, distance));
            Vector3 delta = flatRot * new Vector3(move.x, 0f, move.y) * (panSpeed * zoomFactor * dt);

            if (mouse != null)
            {
                if (mouse.middleButton.isPressed)
                {
                    Vector2 md = mouse.delta.ReadValue();
                    delta += flatRot * new Vector3(-md.x, 0f, -md.y) * (dragPanSpeed * zoomFactor * distance * 0.05f);
                }

                if (mouse.rightButton.isPressed)
                {
                    targetYaw += mouse.delta.ReadValue().x * orbitSpeed;
                }

                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f)
                {
                    targetDistance = Mathf.Clamp(targetDistance - scroll * zoomSpeed * targetDistance, minDistance, maxDistance);
                }
            }

            Vector3 pos = transform.position + delta;
            pos.x = Mathf.Clamp(pos.x, boundsMin.x, boundsMax.x);
            pos.z = Mathf.Clamp(pos.z, boundsMin.y, boundsMax.y);
            transform.position = pos;

            // --- Smooth rotate / zoom ---
            float t = 1f - Mathf.Exp(-rotateSmoothing * dt);
            yaw = Mathf.LerpAngle(yaw, targetYaw, t);
            distance = Mathf.Lerp(distance, targetDistance, t);

            ApplyCameraTransform();
        }

        private void ApplyCameraTransform()
        {
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            if (cam == null) return;

            float pitch = Mathf.Lerp(minPitch, maxPitch, Mathf.InverseLerp(minDistance, maxDistance, distance));
            Quaternion camRot = Quaternion.Euler(pitch, 0f, 0f);
            cam.transform.localRotation = camRot;
            cam.transform.localPosition = camRot * new Vector3(0f, 0f, -distance);
        }
    }
}
