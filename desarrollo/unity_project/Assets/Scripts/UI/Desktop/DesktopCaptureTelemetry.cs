#if UNITY_STANDALONE
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using WebGL.Core.Managers;
using WebGL.Telemetry;

namespace WebGL.UI.Desktop
{
    /// <summary>
    /// Verification aid for WP-10 (-tel-capture &lt;folder&gt;): opens the viewer, starts the bundled local replay at a chosen second of the
    /// flight, writes frame strips (pose changes, spinning propellers, alert highlight, explode while flying, orbit, rest pose) and PASS / FAIL
    /// lines: pose against the golden-tested NedFrames axes, nose against the flight direction, propeller sense, manual power vs telemetry,
    /// and the rest pose after stopping. It does not use the OS mouse. Not active unless the flag is given.
    /// </summary>
    internal sealed class DesktopCaptureTelemetry : MonoBehaviour
    {
        public string OutputDirectory;
        int _fail;

        void Start() { StartCoroutine(Run()); }

        void Check(string name, bool ok)
        {
            if (!ok) _fail++;
            Debug.Log("[TelCap] " + (ok ? "PASS " : "FAIL ") + name);
        }

        Texture2D Grab(int w, int h)
        {
            var full = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            full.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
            full.Apply();
            var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(full, rt);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var t = new Texture2D(w, h, TextureFormat.RGB24, false);
            t.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            t.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            Destroy(full);
            return t;
        }

        /// <summary>n screenshots `gap` seconds apart into one PNG strip (cols per row); the pose of each frame is logged.</summary>
        IEnumerator Strip(string name, int n, float gap, int cols, int cw = 480, int ch = 270, Action<int> before = null)
        {
            int rows = (n + cols - 1) / cols;
            var strip = new Texture2D(cols * cw, rows * ch, TextureFormat.RGB24, false);
            strip.SetPixels32(new Color32[cols * cw * rows * ch]);
            var times = new List<string>();
            float t0 = Time.realtimeSinceStartup;
            for (int i = 0; i < n; i++)
            {
                before?.Invoke(i);
                yield return new WaitForEndOfFrame();
                times.Add(Mathf.RoundToInt((Time.realtimeSinceStartup - t0) * 1000f).ToString());
                var tile = Grab(cw, ch);
                strip.SetPixels((i % cols) * cw, (rows - 1 - i / cols) * ch, cw, ch, tile.GetPixels());
                Destroy(tile);
                LogPose(name + "[" + i + "]");
                if (gap > 0f && i < n - 1) yield return new WaitForSecondsRealtime(gap);
            }
            strip.Apply();
            File.WriteAllBytes(Path.Combine(OutputDirectory, name + ".png"), ImageConversion.EncodeToPNG(strip));
            Debug.Log("[TelCap] " + name + ": " + n + " frames at ms [" + string.Join(",", times) + "]");
            Destroy(strip);
        }

        IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            var tile = Grab(Screen.width, Screen.height);
            File.WriteAllBytes(Path.Combine(OutputDirectory, name + ".png"), ImageConversion.EncodeToPNG(tile));
            Destroy(tile);
        }

        static string V(Vector3 v) => "(" + v.x.ToString("F2") + "," + v.y.ToString("F2") + "," + v.z.ToString("F2") + ")";

        /// <summary>Pose sample: frame (NED, euler) vs the model root, with the expected axes from the golden-tested NedFrames.</summary>
        void LogPose(string tag)
        {
            var c = DesktopTelemetry.Client; var d = TelemetryDroneDriver.Instance;
            if (c?.Displayed == null || d == null || d.Root == null) return;
            var f = c.Displayed;
            NedFrames.Px4Euler(f.q, out double roll, out double pitch, out double yaw);
            AxesError(d, f, out float noseErr, out float rightErr, out float posErr);
            Debug.Log("[TelCap] pose " + tag + " lt=" + f.lt.ToString("F1") + "s ned=(" + f.p[0].ToString("F1") + "," + f.p[1].ToString("F1") + "," + f.p[2].ToString("F1") +
                      ") roll/pitch/yaw=" + roll.ToString("F0") + "/" + pitch.ToString("F0") + "/" + yaw.ToString("F0") + " rpm=" + f.rpm[0] + "," + f.rpm[1] + "," + f.rpm[2] + "," + f.rpm[3] +
                      " root=" + V(d.Root.position) + " noseErr=" + noseErr.ToString("F4") + " rightErr=" + rightErr.ToString("F4") + " posErr=" + posErr.ToString("F3") +
                      " alerts=" + c.Alerts.Count + " highlighted=[" + string.Join(",", new List<string>(d.HighlightedComponents).ToArray()) + "]");
        }

        /// <summary>Model nose / right in the world read from the root (rest nose -Z, rest right -X) against NedFrames body axes.</summary>
        static void AxesError(TelemetryDroneDriver d, TelemetryFrame f, out float noseErr, out float rightErr, out float posErr)
        {
            Quaternion rel = d.Root.rotation * Quaternion.Inverse(d.RestPose.RootRotation);
            Vector3 nose = rel * new Vector3(0, 0, -1), right = rel * new Vector3(-1, 0, 0);
            NedFrames.BodyAxesInUnity(f.q, out var fw, out var rt, out _);
            noseErr = (nose - new Vector3((float)fw[0], (float)fw[1], (float)fw[2])).magnitude;
            rightErr = (right - new Vector3((float)rt[0], (float)rt[1], (float)rt[2])).magnitude;
            // Body origin = root pose applied to the rest body origin.
            Vector3 local = Quaternion.Inverse(d.RestPose.RootRotation) * (d.RestPose.BodyOrigin - d.RestPose.RootPosition);
            Vector3 origin = d.Root.position + d.Root.rotation * local;
            Vector3 want = d.RestPose.BodyOrigin + NedFrames.NedToUnityVector(f.p) * d.RestPose.UnitsPerMeter;
            posErr = (origin - want).magnitude;
        }

        IEnumerator Run()
        {
            Directory.CreateDirectory(OutputDirectory);
            yield return new WaitForSecondsRealtime(8f);
            var ui = FindFirstObjectByType<UIManager>();
            ui?.PrepareViewerForDeepLink();
            Debug.Log("[TelCap] viewer opened through UIManager: " + (ui != null));
            yield return new WaitForSecondsRealtime(4f);
            var driver = TelemetryDroneDriver.Instance;
            var power = DroneStateController.Instance;
            Check("driver installed, telemetry stopped by default", driver != null && !DesktopTelemetry.IsRunning && !driver.Engaged);
            yield return Shot("00_reposo_antes");
            Vector3 restPos = GameObject.Find("x500v2_Drone").transform.position;
            Quaternion restRot = GameObject.Find("x500v2_Drone").transform.rotation;

            // ── 1. takeoff window: second 2 of the flight ──
            bool ok = DesktopTelemetry.StartLocalReplay(2);
            Check("local replay started (" + DesktopTelemetry.BadgeText("es") + ")", ok && DesktopTelemetry.IsRunning);
            Check("badge text is the approved local replay label", DesktopTelemetry.BadgeText("es") == "Sombra digital · reproducción local");
            yield return new WaitForSecondsRealtime(1.5f);
            Check("driver engaged, root moved", driver.Engaged);
            Debug.Log("[TelCap] link=" + DesktopTelemetry.LinkState + " source=" + DesktopTelemetry.Source.Endpoint + " units/m=" + driver.RestPose.UnitsPerMeter.ToString("F2"));
            var motorNames = new[] { "x500v2_motor_FR", "x500v2_motor_FL", "x500v2_motor_BR", "x500v2_motor_BL" };
            var sb = new StringBuilder();
            foreach (var n in motorNames) sb.Append(n + "->motor" + driver.LastMotorOfPart(n) + " ");
            Debug.Log("[TelCap] part to motor: " + sb);
            yield return Strip("01_despegue_poses", 8, 2.5f, 4);

            // ── propeller sense: angle change between two close frames, per motor ──
            var props = new[] { "x500v2_prop_FR", "x500v2_prop_FL", "x500v2_prop_BR", "x500v2_prop_BL" };
            var tr = new Transform[4];
            for (int i = 0; i < 4; i++) { tr[i] = FindDeep(driver.Root, props[i]); }
            var before = new Quaternion[4];
            for (int i = 0; i < 4; i++) before[i] = tr[i].localRotation;
            yield return null;
            var rest = driver.RestPose;
            for (int i = 0; i < 4; i++)
            {
                // signed turn about world up between two frames (positive = clockwise seen from above in Unity)
                Quaternion dq = tr[i].localRotation * Quaternion.Inverse(before[i]);
                Vector3 axisWorld = tr[i].parent.rotation * new Vector3(dq.x, dq.y, dq.z);
                float deg = Mathf.DeltaAngle(0, 2f * Mathf.Atan2(new Vector3(dq.x, dq.y, dq.z).magnitude, dq.w) * Mathf.Rad2Deg);
                float sign = Vector3.Dot(axisWorld, Vector3.up) >= 0 ? 1 : -1;
                Debug.Log("[TelCap] prop " + props[i] + " (motor " + driver.LastMotorOfPart(props[i].Replace("prop", "motor")) + ") turned " + (sign * deg).ToString("F1") + " deg in one frame of " + (Time.unscaledDeltaTime * 1000f).ToString("F0") + " ms = " + (sign * deg / Mathf.Max(Time.unscaledDeltaTime, 1e-4f)).ToString("F0") + " deg/s (about up; negative = CCW seen from above)");
            }

            // ── 2. moving window: second 40 ──
            DesktopTelemetry.StartLocalReplay(40);
            yield return new WaitForSecondsRealtime(1.5f);
            yield return Strip("02_vuelo_poses", 8, 2.5f, 4);

            // nose vs flight direction (speed > 3 m/s horizontal), sampled for 12 s
            double sum = 0; int cnt = 0; float worst = 0;
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 12f)
            {
                var f = DesktopTelemetry.Client?.Displayed;
                if (f != null)
                {
                    double sp = Math.Sqrt(f.v[0] * f.v[0] + f.v[1] * f.v[1]);
                    if (sp > 3)
                    {
                        Quaternion rel = driver.Root.rotation * Quaternion.Inverse(driver.RestPose.RootRotation);
                        Vector3 nose = rel * new Vector3(0, 0, -1); nose.y = 0; nose.Normalize();
                        Vector3 vel = NedFrames.NedToUnityVector(f.v); vel.y = 0; vel.Normalize();
                        float cos = Vector3.Dot(nose, vel);
                        sum += cos; cnt++; worst = Mathf.Max(worst, 1 - cos);
                    }
                }
                yield return null;
            }
            Debug.Log("[TelCap] nose vs velocity direction over " + cnt + " frames with speed > 3 m/s: mean cos = " + (cnt > 0 ? (sum / cnt).ToString("F3") : "n/a"));
            Check("model nose follows the flight direction (mean cos > 0.8, " + cnt + " frames)", cnt > 20 && sum / cnt > 0.8);

            // ── 3. manual power vs telemetry ──
            power?.TurnOn();
            yield return new WaitForSecondsRealtime(2.6f);
            power?.SetLoadCommand(0.8f);
            yield return new WaitForSecondsRealtime(1.2f);
            var ff = DesktopTelemetry.Client.Displayed;
            AxesError(driver, ff, out float ne, out float re, out float pe);
            Check("power on + Flying (hover) does not move the telemetry pose (noseErr " + ne.ToString("F4") + ", posErr " + pe.ToString("F3") + ")", power != null && power.CurrentState == DroneState.Flying && ne < 0.02f && pe < 0.5f);
            yield return Shot("03_encendido_manual_con_telemetria");

            // ── 4. alert: synthetic motor failure on motor 3 ──
            bool sent = DesktopTelemetry.Client.Send(ClientMessage.Inject(FaultKind.MotorFailure, 3), out string why);
            Check("inject motor_failure:3 accepted by the local source (" + why + ")", sent);
            yield return new WaitForSecondsRealtime(1.0f);
            yield return Strip("04_alerta_motor3", 6, 0.8f, 3);
            Check("alert highlight on motor3", new List<string>(driver.HighlightedComponents).Contains("motor3"));
            // Close-up on the faulty motor: focus the camera on its part (the camera target becomes the part, which moves with the flight).
            var m3 = driver.MotorPart(3);
            if (m3 != null)
            {
                OrbitCameraController.Instance?.FocusOnObject(m3);
                OrbitCameraController.Instance?.SetAngles(35f, 25f, false);
                yield return new WaitForSecondsRealtime(1.6f);
                var mpb = new MaterialPropertyBlock();
                var rend = m3.GetComponentInChildren<Renderer>();
                rend.GetPropertyBlock(mpb);
                Debug.Log("[TelCap] motor3 renderer " + rend.name + " _AlertIntensity = " + mpb.GetFloat("_AlertIntensity").ToString("F2") + " _AlertColor = " + mpb.GetColor("_AlertColor") + " (alert accent of DroneLitPC; _HighlightIntensity = " + mpb.GetFloat("_HighlightIntensity").ToString("F2") + ", selection/hover untouched)");
                yield return Strip("04b_alerta_detalle_motor3", 4, 0.35f, 2, 640, 360);
                OrbitCameraController.Instance?.ResetView();
                yield return new WaitForSecondsRealtime(1.5f);
            }
            DesktopTelemetry.Client.Send(ClientMessage.Clear(), out _);

            // ── 5. explode while flying, then orbit ──
            ui?.ShortcutToggleExplode();
            yield return new WaitForSecondsRealtime(2.5f);
            AxesError(driver, DesktopTelemetry.Client.Displayed, out ne, out re, out pe);
            Check("explode while flying: pose still follows the frame (posErr " + pe.ToString("F3") + ")", pe < 0.5f);
            yield return Shot("05_explosion_en_vuelo");
            ui?.ShortcutToggleExplode();
            yield return new WaitForSecondsRealtime(2f);
            OrbitCameraController.Instance?.SetAngles(70f, 15f, true);
            yield return new WaitForSecondsRealtime(1.2f);
            yield return Strip("06_orbita_siguiendo", 4, 1.2f, 4);
            OrbitCameraController.Instance?.SetAngles(0f, 20f, true);
            power?.TurnOff();
            yield return new WaitForSecondsRealtime(2.2f);

            // ── 6. stop: rest pose back ──
            DesktopTelemetry.Stop();
            yield return new WaitForSecondsRealtime(1.2f);
            var root = GameObject.Find("x500v2_Drone").transform;
            Check("stop restores the rest pose (dPos " + (root.position - restPos).magnitude.ToString("F4") + ", dRot " + Quaternion.Angle(root.rotation, restRot).ToString("F3") + ")",
                  (root.position - restPos).magnitude < 1e-3f && Quaternion.Angle(root.rotation, restRot) < 0.05f && !driver.Engaged);
            Check("simulated power resumes motion control (ExternalMotion off)", power == null || !power.ExternalMotion);
            yield return Shot("07_reposo_despues");

            Debug.Log("[TelCap] done, failures = " + _fail);
            Application.Quit();
        }

        static Transform FindDeep(Transform p, string name)
        {
            if (p.name == name) return p;
            for (int i = 0; i < p.childCount; i++) { var r = FindDeep(p.GetChild(i), name); if (r != null) return r; }
            return null;
        }
    }
}
#endif
