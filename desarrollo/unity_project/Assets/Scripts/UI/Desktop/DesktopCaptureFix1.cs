#if UNITY_STANDALONE
using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;
using WebGL.Core.Managers;
using WebGL.UI.Panels;

namespace WebGL.UI.Desktop
{
    /// <summary>
    /// Verification aid for the desktop fixes of PLAN_PC 41 (-fix1-capture &lt;folder&gt;, plus -lang es|en, -uiscale and the window flags). In-player probe, no OS mouse or keyboard.
    /// (1) CUT: the cut plane tint and the cut seen from several angles, the Cut / Filter cards turning their effect off when pressed again (and the panel reopening).
    /// (2) ENVIRONMENT: every preset of the three selector pills (Studio, Time, Color): no label leaves its pill (also with longer texts), and the track (and fill) of
    /// the environment sliders has contrast over the popover on every one of them. One PNG per state (tag = WxH_lang) and PASS / FAIL lines.
    /// </summary>
    internal sealed class DesktopCaptureFix1 : MonoBehaviour
    {
        public string OutputDirectory;
        private int _fail, _pass, _blackShots;
        private readonly StringBuilder _log = new StringBuilder();
        private string _tag;

        private void Start() { StartCoroutine(Run()); }
        private void Log(string s) { _log.AppendLine(s); Debug.Log("[Fix1Probe] " + s); }

        private void Check(string name, bool ok)
        {
            if (ok) _pass++; else _fail++;
            Log((ok ? "PASS " : "FAIL ") + name);
        }

        private VisualElement Root => UIManager.Instance.Root;
        private Button B(string n) => Root.Q<Button>(n);
        private static string Lang => AppLanguageManager.IsSpanish ? "es" : "en";
        private static string F2(double x) => x.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
        private static bool Shown(VisualElement e) => e != null && e.resolvedStyle.display != DisplayStyle.None;

        private static void Click(Button b)
        {
            if (b == null) return;
            using (var e = NavigationSubmitEvent.GetPooled()) { e.target = b; b.SendEvent(e); }
        }

        private IEnumerator Shot(string name, float wait = 0.4f, Action<Texture2D> inspect = null)
        {
            if (wait > 0f) yield return new WaitForSecondsRealtime(wait);
            Texture2D use = null;
            yield return DesktopScreenGrab.Grab(t => use = t);
            float luma = DesktopScreenGrab.LastLuma;
            if (luma < 3f) { _blackShots++; Log("BLACK " + _tag + "_" + name); }
            File.WriteAllBytes(Path.Combine(OutputDirectory, _tag + "_" + name + ".png"), ImageConversion.EncodeToPNG(use));
            Log("shot " + _tag + "_" + name + " " + Screen.width + "x" + Screen.height + " luma " + F2(luma));
            inspect?.Invoke(use);
            Destroy(use);
        }

        private static float UiScale()
        {
            string[] a = Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++)
                if (a[i].Equals("-uiscale", StringComparison.OrdinalIgnoreCase) && float.TryParse(a[i + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float s)) return s;
            return 1f;
        }

        private IEnumerator Run()
        {
            Directory.CreateDirectory(OutputDirectory);
            PlayerPrefs.SetInt("Onboarding_Seen", 1);
            DesktopTelemetryUI.IgnoreFocusLoss = true;
            yield return new WaitForSecondsRealtime(7f);
            AccessibilityManager.Instance?.SetUIScale(UiScale());
            yield return new WaitForSecondsRealtime(0.5f);
            _tag = Screen.width + "x" + Screen.height + "_" + Lang + (UiScale() != 1f ? "_x" + F2(UiScale()) : "");
            UIManager ui = UIManager.Instance;
            ui.PrepareViewerForDeepLink();
            yield return new WaitForSecondsRealtime(3f);
            if (ui.IsOnboardingOpen) { UIManager.Instance.ShortcutBack(); yield return new WaitForSecondsRealtime(1f); }
            Log("window " + Screen.width + "x" + Screen.height + " lang " + Lang + " scale " + F2(UiScale()));

            yield return CutPart();
            yield return TogglePart();
            yield return EnvPart();

            Log("black captures: " + _blackShots);
            Check("no capture came out black", _blackShots == 0);
            Log("RESULT " + _pass + " pass, " + _fail + " fail");
            File.WriteAllText(Path.Combine(OutputDirectory, "fix1_" + _tag + ".log"), _log.ToString());
            Application.Quit();
        }

        // ───────────────────────── 1. CUT ─────────────────────────

        private IEnumerator CutPart()
        {
            Log("── corte ──");
            Click(B("DkMode2"));
            yield return new WaitForSecondsRealtime(0.8f);
            Click(B("AnalyzeCrossSectionBtn"));
            yield return new WaitForSecondsRealtime(1.2f);
            CrossSectionManager cs = CrossSectionManager.Instance;
            Check("the cut is on after pressing the card", cs != null && cs.IsEnabled);

            GameObject plane = GameObject.Find("[CrossSectionPlane1]");
            Renderer r = plane != null ? plane.GetComponent<Renderer>() : null;
            if (r != null)
            {
                Material m = r.sharedMaterial;
                float a = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor").a : -1f;
                Log("plane: shader " + m.shader.name + " alpha " + F2(a) + " _Surface " + (m.HasProperty("_Surface") ? F2(m.GetFloat("_Surface")) : "-") + " queue " + m.renderQueue +
                    " ZWrite " + (m.HasProperty("_ZWrite") ? F2(m.GetFloat("_ZWrite")) : "-") + " active " + plane.activeSelf + " size " + plane.transform.localScale);
                Check("the cut plane tint is faint (alpha <= 0.12)", a >= 0f && a <= 0.12f);
            }
            else Check("the cut plane exists", false);

            var views = new (string n, float yaw, float pitch)[] { ("3q", 35f, 22f), ("frente", 0f, 8f), ("lado", 90f, 6f), ("arriba", 20f, 78f), ("abajo", 30f, -35f), ("atras", 200f, 18f) };
            OrbitCameraController cam = OrbitCameraController.Instance;
            foreach (var v in views)
            {
                cam.SetAngles(v.yaw, v.pitch, true);
                cam.SetDistance(3.2f, true);
                yield return Shot("corte_y_" + v.n, 0.7f);
            }

            // the other axes (X, Z): deselect Y, select each one
            Click(B("CrossSectionAxisX")); yield return new WaitForSecondsRealtime(0.3f);
            Click(B("CrossSectionAxisY")); yield return new WaitForSecondsRealtime(0.3f);
            cam.SetAngles(35f, 22f, true);
            yield return Shot("corte_x_3q", 0.7f);
            cam.SetAngles(90f, 10f, true);
            yield return Shot("corte_x_lado", 0.7f);
            Click(B("CrossSectionAxisZ")); yield return new WaitForSecondsRealtime(0.3f);
            Click(B("CrossSectionAxisX")); yield return new WaitForSecondsRealtime(0.3f);
            cam.SetAngles(35f, 22f, true);
            yield return Shot("corte_z_3q", 0.7f);
            cam.SetAngles(0f, 10f, true);
            yield return Shot("corte_z_frente", 0.7f);
            Click(B("CrossSectionAxisY")); Click(B("CrossSectionAxisZ")); yield return new WaitForSecondsRealtime(0.3f);

            cs.DisableCrossSection();
            cam.SetAngles(0f, 10f, true);
            yield return new WaitForSecondsRealtime(0.4f);
        }

        // ───────────────── 1b. the Cut / Filter cards turn off when pressed again ─────────────────

        private IEnumerator TogglePart()
        {
            Log("── tarjetas Corte / Filtrar ──");
            if (B("AnalyzeCrossSectionBtn") == null || !Shown(B("AnalyzeCrossSectionBtn")))
            {
                Click(B("DkMode2"));
                yield return new WaitForSecondsRealtime(0.8f);
            }
            CrossSectionManager cs = CrossSectionManager.Instance;
            Button cut = B("AnalyzeCrossSectionBtn"), filter = B("AnalyzeFilterBtn"), explode = B("AnalyzeExplodeBtn");
            VisualElement cutPanel = Root.Q<VisualElement>("CrossSectionPanel");
            VisualElement filterPanel = Root.Q<VisualElement>("FilterSubPanel");
            if (cs.IsEnabled) { cs.DisableCrossSection(); yield return new WaitForSecondsRealtime(0.3f); }

            Click(cut); yield return new WaitForSecondsRealtime(0.8f);
            Check("Cut: 1st press switches the cut on", cs.IsEnabled);
            Check("Cut: 1st press opens the panel", !cutPanel.ClassListContains("submenu--hidden"));
            Check("Cut: the card is lit", cut.ClassListContains("submenu-card--active"));
            yield return Shot("tarjeta_corte_activa", 0.2f);
            Click(cut); yield return new WaitForSecondsRealtime(0.8f);
            Check("Cut: 2nd press switches the cut OFF", !cs.IsEnabled);
            Check("Cut: 2nd press closes the panel", cutPanel.ClassListContains("submenu--hidden"));
            Check("Cut: the card is no longer lit", !cut.ClassListContains("submenu-card--active"));

            // fine adjustment stays reachable: the panel's X keeps the cut, the card re-opens the panel, the next press turns it off
            Click(cut); yield return new WaitForSecondsRealtime(0.8f);
            Click(B("CrossSectionBackBtn")); yield return new WaitForSecondsRealtime(0.6f);
            Check("Cut: the panel's X closes the panel and keeps the cut", cs.IsEnabled && cutPanel.ClassListContains("submenu--hidden") && cut.ClassListContains("submenu-card--active"));
            Click(cut); yield return new WaitForSecondsRealtime(0.8f);
            Check("Cut: with the cut on and the panel closed, the card re-opens the panel (cut stays on)", cs.IsEnabled && !cutPanel.ClassListContains("submenu--hidden"));
            Click(cut); yield return new WaitForSecondsRealtime(0.8f);
            Check("Cut: and the next press turns it off", !cs.IsEnabled);

            // the top-bar chip also unlights the card
            Click(cut); yield return new WaitForSecondsRealtime(0.8f);
            Click(B("CrossSectionBackBtn")); yield return new WaitForSecondsRealtime(0.5f);
            Click(B("DkChipCut")); yield return new WaitForSecondsRealtime(0.6f);
            Check("Cut: the chip switches the cut off and the card follows", !cs.IsEnabled && !cut.ClassListContains("submenu-card--active"));

            // filter
            Click(filter); yield return new WaitForSecondsRealtime(0.8f);
            Check("Filter: 1st press opens the panel", !filterPanel.ClassListContains("submenu--hidden"));
            Click(B("CatBtn_Propulsion")); yield return new WaitForSecondsRealtime(0.5f);
            Check("Filter: a category narrows the filter", UIManager.Instance.FilterActive);
            Check("Filter: the card is lit", filter.ClassListContains("submenu-card--active"));
            yield return Shot("tarjeta_filtrar_activa", 0.2f);
            Click(filter); yield return new WaitForSecondsRealtime(0.8f);
            Check("Filter: 2nd press removes the filter", !UIManager.Instance.FilterActive);
            Check("Filter: 2nd press closes the panel", filterPanel.ClassListContains("submenu--hidden"));
            Check("Filter: the card is no longer lit", !filter.ClassListContains("submenu-card--active"));
            Click(filter); yield return new WaitForSecondsRealtime(0.8f);
            Click(B("CatBtn_Avionics")); yield return new WaitForSecondsRealtime(0.4f);
            Click(B("FilterBackBtn")); yield return new WaitForSecondsRealtime(0.5f);
            Check("Filter: the panel's X keeps the filter and the card lit", UIManager.Instance.FilterActive && filter.ClassListContains("submenu-card--active"));
            Click(filter); yield return new WaitForSecondsRealtime(0.8f);
            Check("Filter: pressing the card re-opens the panel (filter stays)", UIManager.Instance.FilterActive && !filterPanel.ClassListContains("submenu--hidden"));
            Click(filter); yield return new WaitForSecondsRealtime(0.8f);
            Check("Filter: and the next press removes it", !UIManager.Instance.FilterActive);

            // explode: unchanged
            Click(explode); yield return new WaitForSecondsRealtime(1.0f);
            Check("Explode: 1st press explodes", explode.ClassListContains("submenu-card--active"));
            Click(explode); yield return new WaitForSecondsRealtime(1.0f);
            Check("Explode: 2nd press reassembles", !explode.ClassListContains("submenu-card--active"));
        }

        // ───────────────────────── 2. ENVIRONMENT ─────────────────────────

        private static readonly string[] Pills = { "EnvPreset_Studio", "EnvPreset_Night", "EnvPreset_Blueprint" };

        private IEnumerator EnvPart()
        {
            Log("── entorno ──");
            Click(B("DkMode3"));
            yield return new WaitForSecondsRealtime(0.9f);
            Click(B("DkStudioEnv"));
            yield return new WaitForSecondsRealtime(0.9f);
            // values away from the ends so that both the track and the fill can be read
            Root.Q<Slider>("EnvLightRotation").value = 200f;
            Root.Q<Slider>("EnvLightIntensity").value = 4f;
            yield return Shot("entorno_inicial", 0.5f, SliderChecks("inicial"));
            CheckPills("inicial");

            for (int i = 1; i <= 3; i++) { Click(B("EnvPreset_Studio")); yield return Env("estudio" + i); }
            for (int i = 1; i <= 3; i++) { Click(B("EnvPreset_Night")); yield return Env("tiempo" + i); }
            for (int i = 1; i <= 9; i++) { Click(B("EnvPreset_Blueprint")); yield return Env("color" + i); }

            // longer texts than the real ones: nothing may leave its pill
            foreach (string n in Pills)
            {
                Label lab = B(n)?.Q<Label>(className: "submenu-label");
                if (lab != null) lab.text = AppLanguageManager.IsSpanish ? "ESTUDIO CLARO EXTENDIDO" : "EXTENDED STUDIO LIGHT";
            }
            yield return Shot("entorno_texto_largo", 0.5f);
            CheckPills("texto largo");
            foreach (string n in Pills)
            {
                Label lab = B(n)?.Q<Label>(className: "submenu-label");
                if (lab != null) lab.text = AppLanguageManager.IsSpanish ? "ATARDECER" : "SUNSET";
            }
            yield return Shot("entorno_texto_corto", 0.5f);
            CheckPills("texto corto");
        }

        private IEnumerator Env(string name)
        {
            yield return Shot("entorno_" + name, 0.7f, SliderChecks(name));
            CheckPills(name);
        }

        private void CheckPills(string tag)
        {
            foreach (string n in Pills)
            {
                Button btn = B(n);
                Label lab = btn?.Q<Label>(className: "submenu-label");
                if (btn == null || lab == null) { Check(tag + ": pill " + n + " exists", false); continue; }
                Rect br = btn.layout, lr = lab.layout;
                bool layoutOk = br.width > 8f && lr.width > 1f;
                Vector2 want = lab.MeasureTextSize(lab.text, lab.contentRect.width, VisualElement.MeasureMode.AtMost, 0f, VisualElement.MeasureMode.Undefined);
                bool textFits = want.x <= lab.contentRect.width + 1f && want.y <= lab.contentRect.height + 1f;
                bool inside = lr.xMin >= -0.5f && lr.xMax <= br.width + 0.5f && lr.yMin >= -0.5f && lr.yMax <= br.height + 0.5f;
                Log("  " + tag + " " + n + " text «" + lab.text.Replace("\n", "\\n") + "» btn " + F2(br.width) + "x" + F2(br.height) + " label " + F2(lr.width) + "x" + F2(lr.height) + " text " + F2(want.x) + "x" + F2(want.y));
                Check(tag + ": «" + lab.text.Replace("\n", " ") + "» stays inside its pill (" + n + ")", layoutOk && textFits && inside);
            }
        }

        private static double Lum(Color32 c)
        {
            double f(byte v) { double s = v / 255.0; return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4); }
            return 0.2126 * f(c.r) + 0.7152 * f(c.g) + 0.0722 * f(c.b);
        }

        private static double Ratio(double a, double b) { double hi = Math.Max(a, b), lo = Math.Min(a, b); return (hi + 0.05) / (lo + 0.05); }

        /// <summary>Contrast of the track (and of the fill when the slider has one) against the popover (rgb 18,21,27), read from the screenshot.</summary>
        private Action<Texture2D> SliderChecks(string tag)
        {
            return tex =>
            {
                Rect rb = Root.worldBound;
                if (rb.width < 10f) { Log("  no root size: slider contrast not read"); return; }
                float sx = tex.width / rb.width, sy = tex.height / rb.height;
                double bg = Lum(new Color32(18, 21, 27, 255));
                foreach (string n in new[] { "EnvLightRotation", "EnvLightIntensity", "EnvBackgroundIntensity" })
                {
                    Slider s = Root.Q<Slider>(n);
                    VisualElement tr = s?.Q(className: "unity-base-slider__tracker");
                    if (tr == null || !Shown(s)) continue;
                    Rect w = tr.worldBound;
                    if (w.width < 8f) { Log("  " + n + " has no layout"); continue; }
                    float f = (s.value - s.lowValue) / (s.highValue - s.lowValue);
                    int cy = (int)(tex.height - 1 - w.center.y * sy);
                    double best = 0, bestFill = 0;
                    if (f < 0.85f)
                    {
                        int px = (int)((w.xMin + (f + 0.6f * (1f - f)) * w.width) * sx);
                        for (int dy = -4; dy <= 4; dy++) best = Math.Max(best, Ratio(Lum(tex.GetPixel(px, cy + dy)), bg));
                    }
                    VisualElement fill = s.Q(className: "dk-slider-fill");
                    if (fill != null && f > 0.15f)
                    {
                        int px = (int)((w.xMin + 0.4f * f * w.width) * sx);
                        for (int dy = -4; dy <= 4; dy++) bestFill = Math.Max(bestFill, Ratio(Lum(tex.GetPixel(px, cy + dy)), bg));
                    }
                    Log("  " + tag + " " + n + " value " + F2(f) + " track " + F2(best) + ":1 fill " + (fill != null ? F2(bestFill) + ":1" : "none"));
                    if (f < 0.85f) Check(tag + ": the track of " + n + " reads over the popover (" + F2(best) + ":1 >= 3)", best >= 3.0);
                    if (fill != null && f > 0.15f) Check(tag + ": the fill of " + n + " reads over the popover (" + F2(bestFill) + ":1 >= 3)", bestFill >= 3.0);
                }
            };
        }
    }
}
#endif
