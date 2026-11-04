#if UNITY_STANDALONE
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using WebGL.Core.Managers;
using WebGL.UI.Panels;

namespace WebGL.UI.Desktop
{
    /// <summary>
    /// Ayuda de verificacion de la fase 3A (solo con <c>-desk-demo3a &lt;carpeta&gt;</c>): guarda las capturas
    /// (vista general, primeros planos, pieza seleccionada, CUT, X-Ray, Termico, Wireframe, panel lateral y
    /// presets de entorno) con ReadPixels y un log con datos de materiales, y cierra la app.
    /// </summary>
    internal sealed class DesktopCaptureDemo3A : MonoBehaviour
    {
        public string OutputDirectory;
        private readonly StringBuilder _log = new StringBuilder();

        private void Start() { StartCoroutine(Run()); }

        private void Log(string line) { _log.AppendLine(line); Debug.Log("[DesktopDemo3A] " + line); }

        private IEnumerator Shot(string name)
        {
            yield return new WaitForSecondsRealtime(0.8f);
            yield return new WaitForEndOfFrame();
            var tex = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
            tex.Apply();
            File.WriteAllBytes(Path.Combine(OutputDirectory, name + ".png"), ImageConversion.EncodeToPNG(tex));
            Destroy(tex);
        }

        private IEnumerator Run()
        {
            Directory.CreateDirectory(OutputDirectory);
            PlayerPrefs.SetInt("Onboarding_Seen", 1);
            yield return new WaitForSecondsRealtime(6f);
            UIManager ui = UIManager.Instance;
            ui.PrepareViewerForDeepLink();
            yield return new WaitForSecondsRealtime(3f);
            if (ui.IsOnboardingOpen) { ui.ShortcutBack(); yield return new WaitForSecondsRealtime(1f); }

            Log($"screen={Screen.width}x{Screen.height} tier={RenderQualityController.ResolvedTier} swapped={PCMaterialSwapper.SwappedCount} owns={PCStudioReflections.OwnsEnvironment} probeRenders={PCStudioReflections.Instance?.RenderCount}");

            OrbitCameraController oc = OrbitCameraController.Instance;
            SelectionManager sm = SelectionManager.Instance;
            EnvironmentController env = EnvironmentController.Instance;
            ViewModeManager vm = ViewModeManager.Instance;

            oc.ResetView(); oc.SetAngles(-30f, 22f, true); oc.SetDistance(8.5f, true);
            yield return Shot("01_vista_general");

            oc.SetAngles(-25f, 48f, true); oc.SetDistance(3.8f, true);
            yield return Shot("02_primer_plano_fibra_carbono");

            GameObject arm = GameObject.Find("x500v2_arm_FL");
            if (arm != null)
            {
                oc.FocusOnObject(arm.transform);
                yield return new WaitForSecondsRealtime(1.2f);
                oc.SetAngles(20f, 18f, true); oc.SetDistance(2.4f, true);
            }
            yield return Shot("03_primer_plano_aluminio");

            // Pieza seleccionada
            oc.ResetView(); oc.SetAngles(-30f, 22f, true); oc.SetDistance(8.5f, true);
            if (arm != null)
            {
                Collider col = arm.GetComponentInChildren<Collider>();
                sm.SimulateClickOn(col != null ? col.transform : arm.transform);
                yield return new WaitForSecondsRealtime(1.5f);
            }
            yield return Shot("08_ultra_pieza_seleccionada_exe");

            // Panel lateral abierto
            UIDetailsSheet.Instance.ShowInfo();
            yield return new WaitForSecondsRealtime(2.5f);
            yield return Shot("09_panel_lateral_abierto");
            UIDetailsSheet.Instance.SetSheetState(false);
            sm.SimulateClickOn(null);
            yield return new WaitForSecondsRealtime(1f);

            // Modos de visualizacion (vista general, sin seleccion)
            sm.Deselect();
            oc.ResetView(); oc.SetAngles(-30f, 22f, true); oc.SetDistance(8.5f, true);
            yield return new WaitForSecondsRealtime(1f);
            vm.SetViewMode(ViewMode.XRay); yield return Shot("05_modo_xray");
            vm.SetViewMode(ViewMode.Thermal); yield return Shot("06_modo_termica");
            vm.SetViewMode(ViewMode.Wireframe); yield return Shot("07_modo_wireframe");
            vm.SetViewMode(ViewMode.Realistic);
            yield return new WaitForSecondsRealtime(0.8f);

            // CUT
            CrossSectionManager cs = CrossSectionManager.Instance;
            oc.ResetView(); oc.SetAngles(-35f, 25f, true); oc.SetDistance(8.5f, true);
            cs.EnableCrossSection();
            cs.SetAxis1(CrossSectionAxis.X);
            cs.SetPosition1(0.2f);
            yield return Shot("04_modo_cut_activo");
            cs.DisableCrossSection();
            yield return new WaitForSecondsRealtime(0.5f);

            // Presets de entorno
            oc.SetAngles(-30f, 22f, true); oc.SetDistance(8.5f, true);
            string[] presets = { "Studio", "Studio Light", "Day", "Sunset", "Night", "Blue", "White" };
            int n = 10;
            foreach (string p in presets)
            {
                env.ApplyPreset(p);
                yield return new WaitForSecondsRealtime(1.4f);
                yield return Shot((n++) + "_entorno_" + p.Replace(" ", "_"));
            }

            Log("probeRenders=" + PCStudioReflections.Instance?.RenderCount);
            File.WriteAllText(Path.Combine(OutputDirectory, "demo3a.log"), _log.ToString());
            Application.Quit();
        }
    }
}
#endif
