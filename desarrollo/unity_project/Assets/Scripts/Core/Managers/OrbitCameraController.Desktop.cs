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
            // negative = the left dock (part tree) is wider than the right one: the free area is then right of the screen centre
            _lateralTarget = Mathf.Clamp(ratio, -0.45f, 0.45f);
        }

        // -- WP-10 / polish: keep framing the drone while telemetry moves it ------------------------------------
        private bool _telemetryFollow;
        private bool _telemetryHavePrev;
        private bool _telemetrySnap;
        private Vector3 _telemetryPrevTarget;
        private Transform _telemetryPrevTargetRef;
        private Transform _telemetryBody;
        private Quaternion _telemetryRestRotation = Quaternion.identity;
        private bool _telemetryRecenter;
        private bool _telemetryHadSelection;
        private float _telemetryReframeAt = -1f;

        /// <summary>Seconds after a selection is cleared (alert click, then Esc) before the view returns to the whole drone during a session.</summary>
        private const float TelemetryReframeDelay = 0.6f;
        private Quaternion _telemetryPrevBodyRotation = Quaternion.identity;
        // offset (world axes, rest attitude) from the pivot of the drone root to the centre of what is VISIBLE of it, measured when the
        // follow engages. The framing offset the viewer leaves behind comes from the bounds of ALL renderers (hidden ones included) and can
        // sit several units from the visible drone: a session re-centres on the visible drone, and Reset does the same while it runs.
        private Vector3 _telemetryCenterOffset;
        private bool _telemetryHaveCenter;

        /// <summary>
        /// PLAN_PC 28.2: while a telemetry session runs the zoom-out reaches the whole flight (the viewer's own limit, 25 units, is about 1.5 m of the drone:
        /// the ground, the course and a 60 m climb are out of reach). 2400 units are about 150 m (the geofence is 120 m); the far plane of the session is 6000.
        /// </summary>
        public const float SessionMaxDistance = 2400f;

        /// <summary>Session wheel zoom: a fixed fraction of the current distance per notch (12 %), from the eye-level of the drone to the whole course; the viewer's own step is a fraction of its short range and would jump 20 m per notch here.</summary>
        private bool DesktopSessionZoom(float amount)
        {
            if (!_telemetryFollow || LegacyTelemetryFollow) return false;
            targetDistance = Mathf.Clamp(targetDistance * Mathf.Pow(0.88f, amount), GetZoomMinDistance(), GetZoomMaxDistance());
            return true;
        }

        /// <summary>-cam-legacy (tests): the follow of WP-10 (position only, offsets fixed in the world, no snap) to measure the drift it had.</summary>
        public static bool LegacyTelemetryFollow;

        /// <summary>
        /// True while a telemetry session moves the drone root (TelemetryDroneDriver). The view keeps framing whatever the camera
        /// target is (the drone root, or a part of it while isolating / focusing) and does it RIGIDLY: the focus point is a point of the
        /// drone body, so it moves with the target's position AND turns with the body (<paramref name="body"/> = the drone root, whose
        /// attitude comes from the flight). Before, the focus offset (the framing offset of the root, or the "focus on part" offset) stayed
        /// fixed in the world, so a yaw of 80 degrees swung the part out of the frame (PLAN_PC 23). Orbit angles, zoom, pan, Reset and the
        /// side-panel shift behave as at rest. Engaging and releasing snap the focus: the drone jumps between its rest pose and the flight
        /// pose in one frame, and the camera must not sweep across the scene in between.
        /// </summary>
        public void SetTelemetryFollow(bool on, Transform body = null, Quaternion restRotation = default)
        {
            if (on)
            {
                _telemetryBody = body;
                _telemetryRestRotation = restRotation.w == 0f && restRotation.x == 0f && restRotation.y == 0f && restRotation.z == 0f ? Quaternion.identity : restRotation;
                _telemetryPrevBodyRotation = _telemetryRestRotation;
                _telemetryHaveCenter = false;
                if (body != null && TryVisibleCenter(body, out Vector3 c)) { _telemetryHaveCenter = true; _telemetryCenterOffset = c - body.position; }
                _telemetryRecenter = _telemetryHaveCenter;
            }
            // on release the body is kept for one frame: the offset must turn back to the rest attitude before the snap
            _telemetryFollow = on;
            _telemetryHavePrev = false;
            _telemetrySnap = true;
        }

        /// <summary>The framing offset of Reset (measured at rest) turned to the current attitude of the drone while a session runs.</summary>
        private Vector3 DesktopResetOffset(Vector3 restOffset)
        {
            if (!_telemetryFollow || _telemetryBody == null || LegacyTelemetryFollow) return restOffset;
            if (_telemetryHaveCenter) restOffset = _telemetryCenterOffset;
            return (_telemetryBody.rotation * Quaternion.Inverse(_telemetryRestRotation)) * restOffset;
        }

        private void ApplyDesktopTelemetryFollow()
        {
            if (target == null)
            {
                _telemetryHavePrev = false;
                return;
            }

            if (LegacyTelemetryFollow)
            {
                if (!_telemetryFollow) { _telemetryHavePrev = false; return; }
                Vector3 lp = target.position;
                if (_telemetryHavePrev) currentFocusPoint += lp - _telemetryPrevTarget;
                _telemetryPrevTarget = lp;
                _telemetryHavePrev = true;
                return;
            }

            Vector3 p = target.position;
            Quaternion body = _telemetryBody != null ? _telemetryBody.rotation : Quaternion.identity;
            bool turn = _telemetryBody != null;

            if (_telemetrySnap)
            {
                // the drone changed pose in one frame (session start / stop): turn the offset with it and put the focus on it at once
                if (_telemetryRecenter && _telemetryFollow && turn && ReferenceEquals(target, _telemetryBody))
                {
                    // engaging on the whole drone: put the focus on its visible centre (turned to the flight attitude)
                    targetOffset = (body * Quaternion.Inverse(_telemetryRestRotation)) * _telemetryCenterOffset;
                }
                else if (turn)
                {
                    targetOffset = (body * Quaternion.Inverse(_telemetryPrevBodyRotation)) * targetOffset;
                }

                _telemetryRecenter = false;
                targetFocusPoint = p + targetOffset;
                currentFocusPoint = targetFocusPoint;
                _telemetrySnap = false;
                _telemetryHavePrev = _telemetryFollow;
                if (!_telemetryFollow) _telemetryBody = null;
            }
            else if (_telemetryFollow && _telemetryHavePrev && ReferenceEquals(target, _telemetryPrevTargetRef))
            {
                // rigid follow: the target's translation plus the swing of the body-fixed offset
                Vector3 before = targetOffset;
                if (turn) targetOffset = (body * Quaternion.Inverse(_telemetryPrevBodyRotation)) * targetOffset;
                currentFocusPoint += (p - _telemetryPrevTarget) + (targetOffset - before);
                targetFocusPoint = p + targetOffset;
            }

            TelemetryReframeAfterDeselect();

            // a different target (focus on a part, isolate): the normal glide goes there, it is not a jump of the drone
            _telemetryPrevTarget = p;
            _telemetryPrevTargetRef = target;
            _telemetryPrevBodyRotation = body;
            _telemetryHavePrev = _telemetryFollow;
        }

        /// <summary>
        /// During a session the drone keeps moving, so a view left on one part (an alert click focuses it) stops framing the drone once the
        /// selection is gone: the part sits at the centre and the rest of the drone is cut off. When the selection is cleared (and nothing is
        /// isolated) the view glides back to the whole drone, ONCE, keeping the orbit angles: the same distance / focus Reset uses, with the
        /// framing offset of the engaged drone. Zoom, orbit and pan afterwards are the user's.
        /// </summary>
        private void TelemetryReframeAfterDeselect()
        {
            if (!_telemetryFollow || _telemetryBody == null || LegacyTelemetryFollow || SelectionManager.Instance == null) { _telemetryReframeAt = -1f; return; }
            bool selected = SelectionManager.Instance.CurrentSelection != null;
            bool isolated = PartVisibilityManager.Instance != null && (PartVisibilityManager.Instance.GetIsolatedTransform() != null || PartVisibilityManager.Instance.GetIsolatedPart() != null);
            if (selected || isolated) { _telemetryHadSelection = selected || isolated; _telemetryReframeAt = -1f; return; }
            if (_telemetryHadSelection)
            {
                _telemetryHadSelection = false;
                if (!ReferenceEquals(target, _telemetryBody)) _telemetryReframeAt = Time.unscaledTime + TelemetryReframeDelay;
            }

            if (_telemetryReframeAt < 0f || Time.unscaledTime < _telemetryReframeAt) return;
            _telemetryReframeAt = -1f;
            if (ReferenceEquals(target, _telemetryBody)) return;
            ClearRetainedNavigationContext();
            target = _telemetryBody;
            CalibrateDefaultNavigationContext(immediate: false);
            RestoreDefaultZoomWindow();
            targetDistance = distance;
            targetOffset = DesktopResetOffset(initialTargetOffset);
        }

        /// <summary>Centre of the bounds of the renderers that are enabled and active under `root` (what the user sees of the drone).</summary>
        private static bool TryVisibleCenter(Transform root, out Vector3 center)
        {
            center = default;
            bool any = false;
            Bounds b = default;
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }

            if (any) center = b.center;
            return any;
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

            // MVP P0, «Modo juego»: the cursor is locked, the mouse orbits, the wheel dollies; no click reaches the scene or the UI (the shield, SelectionManager)
            if (DesktopGameMode.Active)
            {
                _leftOrbitCandidate = _leftOrbiting = false;
                _orbitGestureBlockedByUI = _panGestureBlockedByUI = false;
                Vector2 md = DesktopGameMode.ReadMouseDelta();
                ApplyOrbit(md.x * rotationSpeed, md.y * rotationSpeed);
                float gscroll = Input.GetAxis("Mouse ScrollWheel");
                if (Mathf.Abs(gscroll) > 0.001f) ApplyZoom(gscroll * MOUSE_SCROLL_SCALE);
                return;
            }

            // ── Right button: orbit (Shift: pan) ──
            if (Input.GetMouseButtonDown(1))
            {
                _orbitGestureBlockedByUI = IsPointerOverCameraBlockingUI();
                _rightPan = shift;
                if (!_orbitGestureBlockedByUI) Debug.Log("[DesktopUI] camera: right button down, " + (_rightPan ? "Shift+right-drag pan" : "orbit"));
                if (!_orbitGestureBlockedByUI && !_rightPan && !_chaseLive) // chasing: the pivot pick would move the focus off the drone
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
                if (_chaseLive) ApplyZoom(scroll * MOUSE_SCROLL_SCALE); // chasing: a plain dolly, the drone stays in the middle
                else DesktopZoomAt(Input.mousePosition, scroll * MOUSE_SCROLL_SCALE);
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

        // -- MVP P0: «Seguir» chase camera while piloting (PLAN_PC 32) ------------------------------------------------
        // The camera keeps orbiting the drone (focus = the drone, so it never leaves the frame; the side-panel shift keeps it in the middle of the
        // free area). While chasing, the yaw follows the heading of the drone with a lag, the user's orbit (mouse drag, game mode, pad) is an
        // OFFSET that glides back after 1.5 s (ChaseCamera). Zoom is a plain dolly (no zoom-to-cursor), pan and the right-button pivot pick are
        // off (they would move the drone off the centre). Reset, isolate, explode, cut and the selection keep working: the chase only drives the two
        // orbit angles, and only while the camera frames the whole drone (a focused or isolated part is the user's to inspect: the chase waits).
        private readonly WebGL.Telemetry.ChaseCamera _chase = new WebGL.Telemetry.ChaseCamera();
        private bool _chaseWanted;
        private bool _chaseLive;
        private bool _chaseEngagePending;

        /// <summary>Distance of the chase camera in metres (the wheel changes it; remembered between runs).</summary>
        public const float ChaseDefaultMeters = 2.4f, ChaseMinMeters = 0.8f, ChaseMaxMeters = 40f;
        private const string ChaseDistancePref = "Desk_ChaseDist";

        /// <summary>The chase camera is engaged right now (piloting with «Seguir», framing the whole drone).</summary>
        public bool ChaseActive => _chaseLive;
        public bool ChaseWanted => _chaseWanted;
        public WebGL.Telemetry.ChaseCamera Chase => _chase;

        public static float ChaseDistanceMeters
        {
            get
            {
                float v = ChaseDefaultMeters;
                try { v = PlayerPrefs.GetFloat(ChaseDistancePref, ChaseDefaultMeters); } catch (System.Exception) { }
                return WebGL.Telemetry.ChaseCamera.Clamp(v, ChaseMinMeters, ChaseMaxMeters);
            }
            set
            {
                try { PlayerPrefs.SetFloat(ChaseDistancePref, WebGL.Telemetry.ChaseCamera.Clamp(value, ChaseMinMeters, ChaseMaxMeters)); } catch (System.Exception) { }
            }
        }

        /// <summary>Asks for the chase camera (piloting on and mode «Seguir»). Off returns the view to the free orbit exactly where it is.</summary>
        public void DesktopSetChase(bool wanted)
        {
            if (wanted == _chaseWanted) return;
            _chaseWanted = wanted;
            if (wanted)
            {
                _chase.Unprime();
                _chaseEngagePending = true;
            }
            else
            {
                SaveChaseDistance();
                _chaseLive = false;
                _chase.Unprime();
                _chaseEngagePending = false;
            }
        }

        private void SaveChaseDistance()
        {
            float upm = TelemetryDroneDriver.Instance != null ? TelemetryDroneDriver.Instance.UnitsPerMeter : 0f;
            if (_chaseLive && upm > 0f) ChaseDistanceMeters = targetDistance / upm;
        }

        /// <summary>Verification / tests: the camera angles and the chase offsets.</summary>
        public void DesktopChaseState(out float yaw, out float pitch, out float userYaw, out float userPitch)
        {
            yaw = currentX; pitch = currentY; userYaw = _chase.UserYaw; userPitch = _chase.UserPitch;
        }

        /// <summary>Orbit by a pad / UI step (degrees). While chasing it is the offset that recentres; otherwise it orbits like the mouse.</summary>
        public void DesktopOrbitBy(float dYaw, float dPitch)
        {
            if (dYaw == 0f && dPitch == 0f) return;
            if (_chaseLive) { _chase.AddInput(dYaw, dPitch); return; }
            targetX += dYaw;
            targetY = Mathf.Clamp(targetY + dPitch, minVerticalAngle, maxVerticalAngle);
        }

        private void ApplyDesktopChase(float dt)
        {
            bool framingBody = _telemetryFollow && !LegacyTelemetryFollow && _telemetryBody != null && ReferenceEquals(target, _telemetryBody);
            bool live = _chaseWanted && framingBody;
            if (live != _chaseLive)
            {
                if (!live) SaveChaseDistance();
                _chaseLive = live;
                if (live) _chaseEngagePending = true; else _chase.Unprime();
            }

            if (!_chaseLive) return;

            float upm = TelemetryDroneDriver.Instance != null ? TelemetryDroneDriver.Instance.UnitsPerMeter : 0f;
            Quaternion rel = _telemetryBody.rotation * Quaternion.Inverse(_telemetryRestRotation);
            Vector3 fwd = rel * Vector3.back; // the nose of the model is -Z at rest (DronePose)
            bool has = WebGL.Telemetry.ChaseCamera.TryHeading(fwd.x, fwd.z, out float bodyYaw);

            if (_chaseEngagePending)
            {
                _chaseEngagePending = false;
                _chase.Prime(has ? bodyYaw : currentX);
                // the whole drone centred (its visible centre, like Reset) and the chase distance; the angles glide there through the normal smoothing
                targetOffset = DesktopResetOffset(initialTargetOffset);
                if (upm > 0f) targetDistance = Mathf.Clamp(ChaseDistanceMeters * upm, GetZoomMinDistance(), GetZoomMaxDistance());
            }

            bool reduce = AccessibilityManager.Instance != null && AccessibilityManager.Instance.ReducedMotion;
            _chase.Step(dt, bodyYaw, has, reduce);
            targetX = currentX + WebGL.Telemetry.ChaseCamera.DeltaAngle(currentX, _chase.Yaw); // the shortest way round: the viewer's own damping does the gliding
            targetY = _chase.Pitch;
            if (upm > 0f) targetDistance = Mathf.Clamp(targetDistance, ChaseMinMeters * upm, ChaseMaxMeters * upm);
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
