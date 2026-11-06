#if UNITY_STANDALONE
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;
using WebGL.Core.Managers;
using WebGL.UI.Panels;

namespace WebGL.UI.Desktop
{
    /// <summary>
    /// Verification aid (-desk-anim &lt;folder&gt;): drives the UI inside the player and writes FRAME STRIPS (several frames of one
    /// interaction in a single PNG, with the frame times in the log) so the transitions can be inspected. It does not use the OS mouse.
    /// </summary>
    internal sealed class DesktopCaptureAnim : MonoBehaviour
    {
        public string OutputDirectory;

        private void Start() { StartCoroutine(Run()); }

        private static void Click(Button b)
        {
            if (b == null) return;
            using (var e = NavigationSubmitEvent.GetPooled()) { e.target = b; b.SendEvent(e); }
        }

        // fractions of the screen: x, y, w, h
        private IEnumerator Burst(string name, float fx, float fy, float fw, float fh, int n, float gap, int cols = 0, int down = 2)
        {
            int sw = Screen.width, sh = Screen.height;
            int cx = Mathf.RoundToInt(fx * sw), cy = Mathf.RoundToInt(fy * sh);
            int cw = Mathf.Max(8, Mathf.RoundToInt(fw * sw)), ch = Mathf.Max(8, Mathf.RoundToInt(fh * sh));
            int ow = cw / down, oh = ch / down;
            if (cols <= 0) cols = Mathf.Min(n, Mathf.Max(1, 3600 / ow));
            int rows = (n + cols - 1) / cols;
            var strip = new Texture2D(cols * ow, rows * oh, TextureFormat.RGB24, false);
            strip.SetPixels32(new Color32[cols * ow * rows * oh]);
            var tex = new Texture2D(cw, ch, TextureFormat.RGB24, false);
            var times = new List<string>();
            float t0 = Time.realtimeSinceStartup;
            for (int i = 0; i < n; i++)
            {
                yield return new WaitForEndOfFrame();
                times.Add(Mathf.RoundToInt((Time.realtimeSinceStartup - t0) * 1000f).ToString());
                tex.ReadPixels(new Rect(cx, sh - cy - ch, cw, ch), 0, 0);
                Color32[] src = tex.GetPixels32();
                int px = (i % cols) * ow, py = (rows - 1 - i / cols) * oh;
                var dst = new Color32[ow * oh];
                for (int y = 0; y < oh; y++)
                {
                    for (int x = 0; x < ow; x++) dst[y * ow + x] = src[(y * down) * cw + x * down];
                }

                strip.SetPixels32(px, py, ow, oh, dst);
                if (gap > 0f) yield return new WaitForSecondsRealtime(gap);
            }

            strip.Apply();
            File.WriteAllBytes(Path.Combine(OutputDirectory, name + ".png"), ImageConversion.EncodeToPNG(strip));
            Debug.Log("[AnimCapture] " + name + ": " + n + " frames at ms [" + string.Join(",", times) + "]");
            Destroy(strip);
            Destroy(tex);
        }

        private IEnumerator Run()
        {
            Directory.CreateDirectory(OutputDirectory);
            PlayerPrefs.SetInt("Onboarding_Seen", 1);
            WebGL.UI.ProceduralIcons.OutlineIcon.AnimateHover = true;
            yield return new WaitForSecondsRealtime(7f);
            UIManager ui = UIManager.Instance;
            VisualElement root = ui.Root;
            DesktopUIController ctrl = DesktopUIController.Instance;
            SelectionManager sm = SelectionManager.Instance;

            ui.PrepareViewerForDeepLink();
            yield return new WaitForSecondsRealtime(3f);
            if (ui.IsOnboardingOpen) { ui.ShortcutBack(); yield return new WaitForSecondsRealtime(1f); }
            OrbitCameraController.Instance.ResetView();
            yield return new WaitForSecondsRealtime(1f);

            // icon micro-animations: hover and click of the set (played from code, 64 px icons)
            var sheet = new VisualElement { pickingMode = PickingMode.Ignore };
            sheet.style.position = Position.Absolute; sheet.style.left = 0; sheet.style.top = 0; sheet.style.right = 0; sheet.style.bottom = 0;
            sheet.style.backgroundColor = new Color(0.055f, 0.067f, 0.086f, 1f);
            sheet.style.alignItems = Align.Center; sheet.style.justifyContent = Justify.Center;
            root.Add(sheet);
            string[] shapes = { "explode", "cut", "inspect", "focus", "isolate", "pins", "reset", "home", "settings", "env_time", "filter", "thermal", "analyze", "panel_l", "language", "help" };
            foreach (string sh in shapes)
            {
                var btn = new Button();
                btn.style.width = 100; btn.style.height = 100; btn.style.backgroundColor = Color.clear; btn.style.borderTopWidth = 0; btn.style.borderBottomWidth = 0; btn.style.borderLeftWidth = 0; btn.style.borderRightWidth = 0;
                btn.style.alignItems = Align.Center; btn.style.justifyContent = Justify.Center;
                var ic = new DesktopVectorIcon(sh, 72);
                ic.style.color = new Color(0.749f, 0.918f, 1f, 1f);
                btn.Add(ic);
                sheet.Add(btn);
                yield return new WaitForSecondsRealtime(0.3f);
                ic.PlayAnim(false);
                yield return Burst("00_icono_" + sh + "_hover", 0.5f - 0.04f, 0.5f - 0.07f, 0.08f, 0.14f, 8, 0.045f, 8, 1);
                if (sh == "explode" || sh == "cut" || sh == "reset")
                {
                    ic.PlayAnim(true);
                    yield return Burst("00_icono_" + sh + "_clic", 0.5f - 0.04f, 0.5f - 0.07f, 0.08f, 0.14f, 8, 0.04f, 8, 1);
                }

                btn.RemoveFromHierarchy();
            }

            sheet.RemoveFromHierarchy();

            // hero entrance (home button -> hero)
            Click(root.Q<Button>("HomeBtn"));
            yield return Burst("01_hero_entrada", 0.2f, 0.05f, 0.6f, 0.9f, 12, 0.05f, 4, 3);
            yield return new WaitForSecondsRealtime(1.2f);

            // tutorial from About
            Click(root.Q<Button>("HeroInfoBtn"));
            yield return new WaitForSecondsRealtime(0.8f);
            Click(root.Q<Button>("HeroHelpBtn"));
            yield return Burst("02_tutorial_apertura", 0.2f, 0.2f, 0.6f, 0.6f, 8, 0.03f, 4, 2);
            FieldInfo f = typeof(UIManager).GetField("_onboardingController", BindingFlags.NonPublic | BindingFlags.Instance);
            object onboarding = f != null ? f.GetValue(ui) : null;
            PropertyInfo pi = onboarding != null ? onboarding.GetType().GetProperty("Tutorial", BindingFlags.NonPublic | BindingFlags.Instance) : null;
            TutorialOverlay tut = pi != null ? pi.GetValue(onboarding) as TutorialOverlay : null;
            for (int s = 0; s < 6; s++)
            {
                if (tut != null) tut.GoTo(s);
                yield return Burst("03_tutorial_paso" + (s + 1) + "_ilustracion", 0.22f, 0.30f, 0.22f, 0.42f, 6, 0.32f, 3, 1);
                if (s < 5)
                {
                    if (tut != null) tut.GoTo(s + 1);
                    yield return Burst("04_tutorial_transicion_" + (s + 1) + "_a_" + (s + 2), 0.2f, 0.2f, 0.6f, 0.6f, 6, 0.025f, 3, 2);
                }
            }

            // touch variant (finger glyphs, gestures) and a locked row (only that action loops)
            if (tut != null)
            {
                tut.ForceTouch(true);
                for (int s2 = 0; s2 < 2; s2++)
                {
                    tut.GoTo(s2);
                    yield return Burst("11_tutorial_tactil_paso" + (s2 + 1), 0.22f, 0.30f, 0.22f, 0.42f, 6, 0.32f, 3, 1);
                }

                tut.ForceTouch(false);
                tut.GoTo(2);
                yield return Burst("12_tutorial_inspeccionar", 0.22f, 0.30f, 0.22f, 0.42f, 8, 0.3f, 4, 1);
                tut.GoTo(3);
                tut.LockRow(1);
                yield return Burst("13_tutorial_fila_bloqueada_explotar", 0.2f, 0.2f, 0.6f, 0.6f, 6, 0.4f, 3, 2);
                tut.LockRow(1);
                tut.GoTo(4);
                tut.LockRow(1);
                yield return Burst("14_tutorial_entorno_dia_noche_atardecer", 0.22f, 0.30f, 0.22f, 0.42f, 8, 0.32f, 4, 1);
                tut.LockRow(1);
                tut.GoTo(0);
                tut.LockRow(2);
                yield return Burst("15_tutorial_zoom_centrado", 0.22f, 0.30f, 0.22f, 0.42f, 6, 0.4f, 3, 1);
                tut.LockRow(2);
            }

            ui.ShortcutBack();
            yield return new WaitForSecondsRealtime(1f);
            if (ui.IsHeroVisible)
            {
                Click(root.Q<Button>("HeroExploreBtn"));
                yield return new WaitForSecondsRealtime(2f);
            }

            ui.ShortcutToggleMode(2);
            yield return Burst("05_modos_pastilla_analizar", 0.3f, 0f, 0.4f, 0.06f, 8, 0.03f, 2, 1);
            ui.ShortcutToggleMode(3);
            yield return Burst("06_modos_pastilla_estudio", 0.3f, 0f, 0.4f, 0.06f, 8, 0.03f, 2, 1);
            yield return new WaitForSecondsRealtime(0.5f);
            ui.ShortcutToggleMode(3);
            yield return new WaitForSecondsRealtime(0.4f);
            ui.ShortcutToggleMode(2);
            yield return new WaitForSecondsRealtime(0.5f);
            Click(root.Q<Button>("AnalyzeCrossSectionBtn"));
            yield return Burst("07_popover_corte_abre", 0.22f, 0.05f, 0.42f, 0.35f, 7, 0.03f, 4, 2);
            ui.ShortcutBack();
            ui.ShortcutToggleMode(2);
            yield return new WaitForSecondsRealtime(0.6f);

            ctrl.Tree.Open(false);
            yield return Burst("08_arbol_desliza", 0f, 0.05f, 0.3f, 0.7f, 7, 0.035f, 4, 2);
            ctrl.Tree.SetOpen(false);
            yield return new WaitForSecondsRealtime(0.8f);

            GameObject part = GameObject.Find("x500v2_arm_FL");
            Collider col = part != null ? part.GetComponentInChildren<Collider>() : null;
            sm.SimulateClickOn(col != null ? col.transform : part.transform);
            UIDetailsSheet.Instance.ShowInfo();
            yield return Burst("09_seleccion_inspector_y_camara", 0f, 0f, 1f, 1f, 8, 0.07f, 2, 3);
            yield return new WaitForSecondsRealtime(1f);
            sm.SimulateClickOn(null);
            yield return new WaitForSecondsRealtime(0.8f);

            ctrl.Chrome.OpenSettings();
            yield return Burst("10_ajustes_abre", 0.6f, 0.05f, 0.4f, 0.7f, 7, 0.03f, 4, 2);
            ctrl.Chrome.CloseSettings();
            Debug.Log("[AnimCapture] done");
            Application.Quit();
        }
    }
}
#endif
