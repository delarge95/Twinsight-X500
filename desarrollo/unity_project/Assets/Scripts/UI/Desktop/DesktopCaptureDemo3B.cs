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
    /// Ayuda de verificacion de la fase 3B (solo con <c>-desk-demo3b &lt;carpeta&gt;</c>): arbol de piezas
    /// (abierto, busqueda con y sin acentos, seleccion) y los modos de visualizacion. Guarda capturas del propio
    /// juego (ReadPixels), un log y cierra la app.
    /// </summary>
    internal sealed class DesktopCaptureDemo3B : MonoBehaviour
    {
        public string OutputDirectory;
        private readonly StringBuilder _log = new StringBuilder();

        private void Start() { StartCoroutine(Run()); }
        private void Log(string line) { _log.AppendLine(line); Debug.Log("[DesktopDemo3B] " + line); }

        private IEnumerator Shot(string name)
        {
            yield return new WaitForSecondsRealtime(0.9f);
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

            OrbitCameraController oc = OrbitCameraController.Instance;
            SelectionManager sm = SelectionManager.Instance;
            ViewModeManager vm = ViewModeManager.Instance;
            DesktopPartTree tree = DesktopUIController.Instance.Tree;
            Log($"screen={Screen.width}x{Screen.height} tier={RenderQualityController.ResolvedTier} lang={AppLanguageManager.CurrentLanguageCode}");

            oc.ResetView(); oc.SetAngles(-30f, 22f, true); oc.SetDistance(8.5f, true);
            yield return new WaitForSecondsRealtime(1f);

            // Arbol
            tree.Open(false);
            yield return Shot("01_arbol_piezas");
            tree.SetQuery("helice"); yield return new WaitForSecondsRealtime(0.4f);
            Log("query 'helice' -> " + tree.VisibleCount);
            yield return Shot("02_arbol_busqueda_helice");
            tree.SetQuery("Bateria"); yield return new WaitForSecondsRealtime(0.4f);
            int a = tree.VisibleCount;
            tree.SetQuery("Batería"); yield return new WaitForSecondsRealtime(0.4f);
            Log("query 'Bateria' -> " + a + " ; 'Batería' -> " + tree.VisibleCount);
            tree.SetQuery("brazo"); yield return new WaitForSecondsRealtime(0.4f);
            Log("query 'brazo' -> " + tree.VisibleCount);
            tree.SelectByQueryFirst();
            yield return new WaitForSecondsRealtime(2.5f);
            Log("selection after tree click: " + (sm.CurrentSelection != null ? sm.CurrentSelection.name : "none"));
            yield return Shot("03_arbol_seleccion_brazo");
            tree.SetQuery("");
            yield return new WaitForSecondsRealtime(0.5f);
            tree.SetQuery("gps"); yield return new WaitForSecondsRealtime(0.4f);
            tree.SelectByQueryFirst();
            yield return new WaitForSecondsRealtime(2.5f);
            Log("selection after tree click 2: " + (sm.CurrentSelection != null ? sm.CurrentSelection.name : "none"));
            tree.SetQuery("");
            yield return Shot("04_arbol_resaltado_gps");
            tree.SetOpen(false);
            sm.Deselect();
            oc.ResetView(); oc.SetAngles(-30f, 22f, true); oc.SetDistance(8.5f, true);
            yield return new WaitForSecondsRealtime(1f);

            // Modos de visualizacion
            ViewMode[] modes = { ViewMode.XRay, ViewMode.Wireframe, ViewMode.Ghosted, ViewMode.Blueprint, ViewMode.SolidColor, ViewMode.Thermal };
            int n = 10;
            foreach (ViewMode m in modes)
            {
                vm.SetViewMode(m);
                yield return Shot((n++) + "_modo_" + m.ToString().ToLowerInvariant());
            }
            // primeros planos
            oc.SetAngles(-35f, 28f, true); oc.SetDistance(3.4f, true);
            foreach (ViewMode m in new[] { ViewMode.XRay, ViewMode.Wireframe })
            {
                vm.SetViewMode(m);
                yield return Shot((n++) + "_modo_" + m.ToString().ToLowerInvariant() + "_cerca");
            }
            vm.SetViewMode(ViewMode.Realistic);
            File.WriteAllText(Path.Combine(OutputDirectory, "demo3b.log"), _log.ToString());
            Application.Quit();
        }
    }
}
#endif
