#if UNITY_STANDALONE
using UnityEngine;

namespace WebGL.Core.Managers
{
    /// <summary>
    /// PC only (phase 2B, U6). Desktop camera controls added to <see cref="OrbitCameraController"/>
    /// without touching the touch / WebGL code path (everything here is compiled for Standalone only):
    ///  - left button dragging on the BACKGROUND (no part under the cursor) orbits; a click without
    ///    dragging still selects / deselects (SelectionManager defers the background click to the release);
    ///  - mouse wheel zooms TOWARD THE CURSOR (the point under the cursor stays under the cursor);
    ///  - Shift + right button drag pans (the middle button keeps panning);
    ///  - right button keeps orbiting; damping is the existing one;
    ///  - a lateral view shift so that, with the docked side panel open, the focus sits in the middle
    ///    of the free area (AUX-U05). Driven by <c>DesktopSidePanel</c>.
    /// </summary>
    public partial class OrbitCameraController
    {
        /// <summary>Pixels the left button must travel on the background before it counts as an orbit drag.</summary>
        public const float DesktopDragThreshold = 6f;

        private bool _leftOrbitCandidate;
        private bool _leftOrbiting;
        private Vector2 _leftPress;
        private bool _rightPan;
        private float _zoomFocusUntil;

        private float _lateralTarget;
        private float _lateralCurrent;

        /// <summary>True while the left button is orbiting the view (after the drag threshold).</summary>
        public bool IsDesktopLeftOrbiting => _leftOrbiting;

        /// <summary>
        /// Fraction of the screen WIDTH by which the scene is displaced to the left of the screen centre
        /// (side panel width / (2 * screen width)). 0 = centred.
        /// </summary>
        public void SetDesktopViewShiftX(float ratio)
        {
            _lateralTarget = Mathf.Clamp(ratio, 0f, 0.45f);
        }

        // -- WP-10: keep framing the drone while telemetry moves it --------------------------------------------
        private bool _telemetryFollow;
        private bool _telemetryHavePrev;
        private Vector3 _telemetryPrevTarget;

        /// <summary>
        /// True while a telemetry session moves the drone root (TelemetryDroneDriver). The view keeps framing whatever the camera
        /// target is (the drone root, or a part of it while isolating / focusing): the focus point moves WITH the target in the same
        /// frame instead of easing towards it, so orbit, zoom, pan, Reset and the side-panel shift behave exactly as at rest.
        /// </summary>
        public void SetTelemetryFollow(bool on)
        {
            _telemetryFollow = on;
            _telemetryHavePrev = false;
        }

        private void ApplyDesktopTelemetryFollow()
        {
            if (!_telemetryFollow || target == null)
            {
                _telemetryHavePrev = false;
                return;
            }

            Vector3 p = target.position;
            if (_telemetryHavePrev) currentFocusPoint += p - _telemetryPrevTarget;
            _telemetryPrevTarget = p;
            _telemetryHavePrev = true;
        }

        private Vector3 DesktopLateralShift(Quaternion rotation, float dt)
        {
            _lateralCurrent = Mathf.Lerp(_lateralCurrent, _lateralTarget, 1f - Mathf.Exp(-dampingFactor * 1.4f * dt));
            if (Mathf.Abs(_lateralCurrent) < 0.0003f)
            {
                return Vector3.zero;
            }

            Camera cam = GetComponent<Camera>();
            if (cam == null)
            {
                return Vector3.zero;
            }

            float frustumHeight = 2f * currentDistance * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float frustumWidth = frustumHeight * cam.aspect;
            return (rotation * Vector3.right) * (frustumWidth * _lateralCurrent);
        }

        /// <summary>While a zoom-to-cursor step settles, the focus point eases at the zoom rate so both stay in sync.</summary>
        private float DesktopFocusRate(float damp)
        {
            return Time.time < _zoomFocusUntil ? Mathf.Max(damp, zoomDamping) : damp;
        }

        private static bool IsPointerOverPart()
        {
            return SelectionManager.Instance != null && SelectionManager.Instance.HoveredObject != null;
        }

        private void HandleDesktopMouseInput()
        {
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

            // ── Right button: orbit (Shift: pan) ──
            if (Input.GetMouseButtonDown(1))
            {
                _orbitGestureBlockedByUI = IsPointerOverCameraBlockingUI();
                _rightPan = shift;
                if (!_orbitGestureBlockedByUI) Debug.Log("[DesktopUI] camera: right button down, " + (_rightPan ? "Shift+right-drag pan" : "orbit"));
                if (!_orbitGestureBlockedByUI && !_rightPan)
                {
                    PickPivot(Input.mousePosition);
                }
            }

            if (Input.GetMouseButtonUp(1))
            {
                _orbitGestureBlockedByUI = false;
                _rightPan = false;
            }

            // ── Middle button: pan ──
            if (Input.GetMouseButtonDown(2))
            {
                _panGestureBlockedByUI = IsPointerOverCameraBlockingUI();
            }

            if (Input.GetMouseButtonUp(2))
            {
                _panGestureBlockedByUI = false;
            }

            // ── Left button: orbit when the drag starts on the background ──
            if (Input.GetMouseButtonDown(0))
            {
                _leftOrbitCandidate = !IsPointerOverCameraBlockingUI() && !IsPointerOverPart();
                _leftOrbiting = false;
                _leftPress = Input.mousePosition;
            }

            if (_leftOrbitCandidate && Input.GetMouseButton(0))
            {
                if (!_leftOrbiting && ((Vector2)Input.mousePosition - _leftPress).sqrMagnitude >= DesktopDragThreshold * DesktopDragThreshold)
                {
                    _leftOrbiting = true;
                    Debug.Log("[DesktopUI] camera: left-drag orbit started on the background");
                }

                if (_leftOrbiting)
                {
                    ApplyOrbit(Input.GetAxis("Mouse X") * rotationSpeed, Input.GetAxis("Mouse Y") * rotationSpeed);
                }
            }

            if (Input.GetMouseButtonUp(0))
            {
                _leftOrbitCandidate = false;
                _leftOrbiting = false;
            }

            if (Input.GetMouseButton(1) && !_orbitGestureBlockedByUI)
            {
                float mouseX = Input.GetAxis("Mouse X");
                float mouseY = Input.GetAxis("Mouse Y");
                if (_rightPan)
                {
                    ApplyPan(mouseX * panSpeed, mouseY * panSpeed);
                }
                else
                {
                    ApplyOrbit(mouseX * rotationSpeed, mouseY * rotationSpeed);
                }
            }

            if (Input.GetMouseButton(2) && !_panGestureBlockedByUI)
            {
                ApplyPan(Input.GetAxis("Mouse X") * panSpeed, Input.GetAxis("Mouse Y") * panSpeed);
            }

            // ── Wheel: zoom toward the cursor ──
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.001f && !IsPointerOverCameraBlockingUI()
                && !(InputManager.Instance != null && InputManager.Instance.IsPointerOverPickableUIForWheel()))
            {
                DesktopZoomAt(Input.mousePosition, scroll * MOUSE_SCROLL_SCALE);
            }
        }

        /// <summary>
        /// Zoom by <paramref name="amount"/> (same units as the classic wheel step) keeping the world point
        /// under <paramref name="screenPos"/> under the cursor. With r = newDistance / oldDistance and H the
        /// point under the cursor, the new focus is F + (1 - r) * (H - F): the camera slides along the cursor ray.
        /// </summary>
        public void DesktopZoomAt(Vector2 screenPos, float amount)
        {
            float oldDistance = targetDistance;
            ApplyZoom(amount);
            float newDistance = targetDistance;
            if (oldDistance < 0.0001f || Mathf.Approximately(oldDistance, newDistance))
            {
                return;
            }

            Camera cam = GetComponent<Camera>();
            if (cam == null)
            {
                return;
            }

            Vector3 focus = target != null ? target.position + targetOffset : targetFocusPoint;
            Ray ray = cam.ScreenPointToRay(screenPos);
            Vector3 hitPoint;
            if (Physics.Raycast(ray, out RaycastHit hit, RAYCAST_MAX_DISTANCE))
            {
                hitPoint = hit.point;
            }
            else
            {
                // Background: use the point of the cursor ray on the plane through the focus facing the camera.
                Vector3 forward = cam.transform.forward;
                float denom = Vector3.Dot(ray.direction, forward);
                if (denom < 0.0001f)
                {
                    return;
                }

                float t = Vector3.Dot(focus - ray.origin, forward) / denom;
                if (t <= 0f)
                {
                    return;
                }

                hitPoint = ray.origin + ray.direction * t;
            }

            Debug.Log($"[DesktopUI] camera: zoom toward cursor {screenPos} dist {oldDistance:F2}->{newDistance:F2}");
            float r = newDistance / oldDistance;
            SetDesktopFocus(focus + (1f - r) * (hitPoint - focus));
            _zoomFocusUntil = Time.time + 0.6f;
        }

        private void SetDesktopFocus(Vector3 focus)
        {
            if (target != null)
            {
                targetOffset = focus - target.position;
                targetOffset.x = Mathf.Clamp(targetOffset.x, -panLimit.x, panLimit.x);
                targetOffset.y = Mathf.Clamp(targetOffset.y, -panLimit.y, panLimit.y);
                targetOffset.z = Mathf.Clamp(targetOffset.z, -panLimit.y, panLimit.y);
            }
            else
            {
                targetFocusPoint = focus;
                targetFocusPoint.x = Mathf.Clamp(targetFocusPoint.x, -panLimit.x, panLimit.x);
                targetFocusPoint.y = Mathf.Clamp(targetFocusPoint.y, -panLimit.y, panLimit.y);
                targetFocusPoint.z = Mathf.Clamp(targetFocusPoint.z, -panLimit.y, panLimit.y);
            }
        }

        // ── Verification helpers (used by the capture demo / Editor tests) ──

        /// <summary>Settled camera state: focus point and distance the camera is easing to.</summary>
        public void DesktopGetTargets(out Vector3 focus, out float distance)
        {
            focus = target != null ? target.position + targetOffset : targetFocusPoint;
            distance = targetDistance;
        }

        /// <summary>Snaps the eased state to the targets (so a test can read the final screen position at once).</summary>
        public void DesktopSnapToTargets()
        {
            currentX = targetX;
            currentY = targetY;
            currentDistance = targetDistance;
            currentFocusPoint = target != null ? target.position + targetOffset : targetFocusPoint;
            _lateralCurrent = _lateralTarget;
        }
    }
}
#endif
