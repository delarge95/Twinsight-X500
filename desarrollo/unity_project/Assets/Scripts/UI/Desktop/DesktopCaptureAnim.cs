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
