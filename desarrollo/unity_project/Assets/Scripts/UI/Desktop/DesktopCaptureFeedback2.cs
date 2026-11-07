#if UNITY_STANDALONE
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;
using WebGL.Core.Managers;
using WebGL.UI.ProceduralIcons;

namespace WebGL.UI.Desktop
{
    /// <summary>
    /// Verification aid (-desk-fb2 &lt;folder&gt;): frame strips of the Cut icon (hover and click), side-by-side render-mode icons,
    /// the render-mode icon in the Studio strip for every mode, the Render popover, and the Power chip appearing / disappearing,
    /// plus PASS / FAIL lines for the chip. It does not use the OS mouse.
    /// </summary>
    internal sealed class DesktopCaptureFeedback2 : MonoBehaviour
    {
        public string OutputDirectory;
        private int _fail;

        private void Start() { StartCoroutine(Run()); }

        private void Check(string name, bool ok)
        {
            if (!ok) _fail++;
            Debug.Log("[Fb2] " + (ok ? "PASS " : "FAIL ") + name);
        }

        private static void Click(Button b)
        {
            if (b == null) return;
            using (var e = NavigationSubmitEvent.GetPooled()) { e.target = b; b.SendEvent(e); }
        }

        private static Rect ScreenRect(VisualElement root, VisualElement ve, float pad)
        {
            float scale = Screen.width / Mathf.Max(1f, root.worldBound.width);
            Rect wb = ve.worldBound;
            float x = (wb.xMin - pad) * scale, w = (wb.width + 2f * pad) * scale;
            float h = (wb.height + 2f * pad) * scale;
            float y = Screen.height - (wb.yMax + pad) * scale;
            x = Mathf.Clamp(x, 0, Screen.width - 8);
            y = Mathf.Clamp(y, 0, Screen.height - 8);
            return new Rect(Mathf.Round(x), Mathf.Round(y), Mathf.Min(Mathf.Round(w), Screen.width - x), Mathf.Min(Mathf.Round(h), Screen.height - y));
        }

        /// <summary>n captures of the same region (rectFn is evaluated per frame) into one PNG; frame times go to the log.</summary>
        private IEnumerator Seq(string name, Func<Rect> rectFn, int n, float gap, int cols, Action<int> before = null, float wait = 0f)
        {
            Rect r0 = rectFn();
            int cw = (int)r0.width, ch = (int)r0.height;
            int rows = (n + cols - 1) / cols;
            var strip = new Texture2D(cols * cw, rows * ch, TextureFormat.RGB24, false);
            strip.SetPixels32(new Color32[cols * cw * rows * ch]);
            var tex = new Texture2D(cw, ch, TextureFormat.RGB24, false);
            var times = new List<string>();
            float t0 = Time.realtimeSinceStartup;
            for (int i = 0; i < n; i++)
            {
                before?.Invoke(i);
                if (wait > 0f) yield return new WaitForSecondsRealtime(wait);
                yield return new WaitForEndOfFrame();
                times.Add(Mathf.RoundToInt((Time.realtimeSinceStartup - t0) * 1000f).ToString());
                Rect r = rectFn();
                tex.ReadPixels(new Rect(r0.x, r0.y, cw, ch), 0, 0);
                strip.SetPixels((i % cols) * cw, (rows - 1 - i / cols) * ch, cw, ch, tex.GetPixels());
                if (gap > 0f) yield return new WaitForSecondsRealtime(gap);
            }

            strip.Apply();
            File.WriteAllBytes(Path.Combine(OutputDirectory, name + ".png"), ImageConversion.EncodeToPNG(strip));
            Debug.Log("[Fb2] " + name + ": " + n + " frames at ms [" + string.Join(",", times) + "]");
            Destroy(strip);
            Destroy(tex);
        }

        /// <summary>Frozen frames of an icon animation at evenly spaced phases (0..1): deterministic, independent of the capture rate.</summary>
        private IEnumerator Phases(string name, OutlineIcon ic, VisualElement cell, VisualElement root, int n, bool click, int cols, float pad)
        {
            yield return Seq(name, () => ScreenRect(root, cell, pad), n, 0f, cols, i =>
            {
                ic.DebugClick = click;
                ic.DebugPhase = n > 1 ? i / (float)(n - 1) : 0f;
                ic.MarkDirtyRepaint();
            }, 0.12f);
            ic.DebugPhase = -1f;
            ic.MarkDirtyRepaint();
        }

        private IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            var tex = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
            tex.Apply();
            File.WriteAllBytes(Path.Combine(OutputDirectory, name + ".png"), ImageConversion.EncodeToPNG(tex));
            Destroy(tex);
        }

        private static VisualElement NewSheet(VisualElement root)
        {
            var sheet = new VisualElement { pickingMode = PickingMode.Ignore };
            sheet.style.position = Position.Absolute; sheet.style.left = 0; sheet.style.top = 0; sheet.style.right = 0; sheet.style.bottom = 0;
            sheet.style.backgroundColor = new Color(0.055f, 0.067f, 0.086f, 1f);
            sheet.style.alignItems = Align.Center; sheet.style.justifyContent = Justify.Center;
            root.Add(sheet);
            return sheet;
        }

        private static DesktopVectorIcon BigIcon(VisualElement parent, string shape, float size, out VisualElement cell)
        {
            cell = new VisualElement { pickingMode = PickingMode.Ignore };
            cell.style.width = size + 24; cell.style.height = size + 24;
            cell.style.alignItems = Align.Center; cell.style.justifyContent = Justify.Center;
            var ic = new DesktopVectorIcon(shape, size);
            ic.style.color = new Color(0.749f, 0.918f, 1f, 1f);
            cell.Add(ic);
            parent.Add(cell);
            return ic;
        }

        private IEnumerator Run()
        {
            Directory.CreateDirectory(OutputDirectory);
            PlayerPrefs.SetInt("Onboarding_Seen", 1);
            OutlineIcon.AnimateHover = true;
            yield return new WaitForSecondsRealtime(7f);
            UIManager ui = UIManager.Instance;
            VisualElement root = ui.Root;
            DesktopUIController ctrl = DesktopUIController.Instance;
            ui.PrepareViewerForDeepLink();
            yield return new WaitForSecondsRealtime(3f);
            if (ui.IsOnboardingOpen) { ui.ShortcutBack(); yield return new WaitForSecondsRealtime(1f); }
            OrbitCameraController.Instance.ResetView();
            yield return new WaitForSecondsRealtime(1f);

            // ── 1. Cut icon: hover (once) and click (bigger) ──
            VisualElement sheet = NewSheet(root);
            DesktopVectorIcon cut = BigIcon(sheet, "cut", 120, out VisualElement cutCell);
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Phases("00_corte_hover", cut, cutCell, root, 15, false, 5, 36f);
            yield return Phases("00_corte_clic", cut, cutCell, root, 15, true, 5, 36f);
            // the same animation played for real (timing log), captured as it runs
            Func<Rect> cutRect = () => ScreenRect(root, cutCell, 36f);
            yield return Seq("00_corte_hover_tiempo_real", cutRect, 10, 0.0f, 5, i => { if (i == 0) cut.PlayAnim(false); });
            yield return new WaitForSecondsRealtime(1f);
            // small size (the real 16 px of the top bar, shown at 2x by the screen scale)
            sheet.Clear();
            DesktopVectorIcon cutSmall = BigIcon(sheet, "cut", 24, out VisualElement cutSmallCell);
            yield return new WaitForSecondsRealtime(0.4f);
            yield return Phases("00_corte_hover_24px", cutSmall, cutSmallCell, root, 10, false, 5, 14f);

            // ── 2. Render-mode icons side by side (all 7 distinct drawings) ──
            sheet.Clear();
            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            row.style.flexDirection = FlexDirection.Row;
            sheet.Add(row);
            string[] modes = { "realistic", "xray", "solid", "wire", "ghost", "thermal", "studio" };
            var modeIcons = new Dictionary<string, DesktopVectorIcon>();
            foreach (string m in modes)
            {
                DesktopVectorIcon ic = BigIcon(row, m, 72, out VisualElement _);
                modeIcons[m] = ic;
            }

            yield return new WaitForSecondsRealtime(0.5f);
            yield return Seq("01_modos_de_render_iconos", () => ScreenRect(root, row, 10f), 1, 0f, 1);
            var joined = new HashSet<string>();
            foreach (string m in modes) joined.Add(string.Join("|", OutlineIconData.Paths[m]));
            Check("render icons: 7 distinct drawings", joined.Count == modes.Length);

            // hover / click micro-animation of the three that changed
            foreach (string m in new[] { "solid", "xray", "wire" })
            {
                DesktopVectorIcon ic = modeIcons[m];
                VisualElement cell = ic.parent;
                yield return Phases("01_" + m + "_hover", ic, cell, root, 10, false, 5, 6f);
                yield return Phases("01_" + m + "_clic", ic, cell, root, 8, true, 4, 6f);
                yield return new WaitForSecondsRealtime(0.4f);
            }

            sheet.RemoveFromHierarchy();
            yield return new WaitForSecondsRealtime(0.3f);

            // ── 3. Studio strip + Render popover for every mode ──
            ui.ShortcutToggleMode(3);
            yield return new WaitForSecondsRealtime(1.2f);
            VisualElement strip = root.Q<VisualElement>("DkStudioStrip");
            ViewMode[] vm = { ViewMode.Realistic, ViewMode.XRay, ViewMode.SolidColor, ViewMode.Wireframe, ViewMode.Ghosted, ViewMode.Thermal };
            if (strip != null)
            {
                yield return Seq("02_barra_estudio_cada_modo", () => ScreenRect(root, strip, 4f), vm.Length, 0f, 3,
                    i => ViewModeManager.Instance.SetViewMode(vm[i]), 0.9f);
            }

            ViewModeManager.Instance.SetViewMode(ViewMode.XRay);
            yield return new WaitForSecondsRealtime(0.9f);
            VisualElement grid = root.Q<VisualElement>("ShaderMenu");
            if (grid != null) yield return Seq("02_popover_render_tarjetas", () => ScreenRect(root, grid, 18f), 1, 0f, 1);
            yield return Shot("02_popover_render_pantalla");
            ViewModeManager.Instance.SetViewMode(ViewMode.Realistic);
            yield return new WaitForSecondsRealtime(0.5f);
            ui.ShortcutToggleMode(3);
            yield return new WaitForSecondsRealtime(0.6f);

            // ── 4. Power chip ──
            VisualElement top = root.Q<VisualElement>("DkTopBar") ?? root.Q<Button>("DkPins")?.parent;
            Button chip = root.Q<Button>("DkChipPower");
            Check("power chip: exists", chip != null);
            DroneStateController drone = DroneStateController.Instance;
            Check("power chip: drone starts off", drone != null && !drone.IsOn);
            Check("power chip: hidden while off", chip != null && !chip.ClassListContains("dk-chip--live"));
            Func<Rect> topRect = () =>
            {
                Rect a = ScreenRect(root, root.Q<Button>("DkPins"), 8f);
                Rect b = ScreenRect(root, root.Q<Button>("DkReset"), 8f);
                return new Rect(a.x - 44f, a.y, (b.xMax - a.x) + 130f, a.height);
            };
            yield return Seq("03_chip_encendido_apareciendo", topRect, 10, 0.06f, 2, i => { if (i == 0) drone.TurnOn(); });
            yield return new WaitForSecondsRealtime(2.5f);
            Check("power chip: visible while the drone is on", chip.ClassListContains("dk-chip--live") && drone.IsOn);
            Button orig = root.Q<Button>("ToolPowerBtn");
            Check("power chip: original button shows active too", orig != null && orig.ClassListContains("submenu-card--active"));
            yield return Shot("03_chip_encendido_pantalla");
            Check("power chip: tooltip set", !string.IsNullOrEmpty(chip.tooltip));
            Debug.Log("[Fb2] chip tooltip = " + chip.tooltip);
            yield return Seq("03_chip_apagado_desapareciendo", topRect, 10, 0.06f, 2, i => { if (i == 0) Click(chip); });
            yield return new WaitForSecondsRealtime(2.2f);
            Check("power chip: click turned the drone off", !drone.IsOn && drone.CurrentState == DroneState.Off);
            Check("power chip: hidden after power-off", !chip.ClassListContains("dk-chip--live") && chip.resolvedStyle.display == DisplayStyle.None);
            Check("power chip: original button inactive after chip off", orig != null && !orig.ClassListContains("submenu-card--active"));
            // power on through the original button (ui handler) shows the chip as well
            ui.ShortcutToggleMode(1);
            yield return new WaitForSecondsRealtime(0.8f);
            Click(orig);
            yield return new WaitForSecondsRealtime(2.5f);
            Check("power chip: original button also shows the chip", drone.IsOn && chip.ClassListContains("dk-chip--live"));
            Click(root.Q<Button>("DkReset"));
            yield return new WaitForSecondsRealtime(0.6f);
            Check("power chip: Reset does not turn the drone off (chip stays)", drone.IsOn && chip.ClassListContains("dk-chip--live"));
            Click(chip);
            yield return new WaitForSecondsRealtime(2.2f);
            Check("power chip: off again", !drone.IsOn && !chip.ClassListContains("dk-chip--live"));

            Debug.Log("[Fb2] done, failures = " + _fail);
            Application.Quit();
        }
    }
}
#endif
