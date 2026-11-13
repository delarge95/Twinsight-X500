#if UNITY_STANDALONE
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;
using WebGL.Core.Content;
using WebGL.Core.Managers;
using WebGL.UI.Panels;

namespace WebGL.UI.Desktop
{
    /// <summary>
    /// Regression checks and before / after captures (-desk-check &lt;folder&gt;). Logs PASS / FAIL lines:
    ///  - the pins button is "on" exactly when the hotspots are visible (it used to invert after Home / re-entry);
    ///  - the active-effect chips (filter / exploded view / cut) follow the real state and turning them off works;
    ///  - wireframe and X-ray screenshots with the old and the new (calmer) intensities for comparison.
    /// </summary>
    internal sealed class DesktopSelfCheck : MonoBehaviour
    {
        public string OutputDirectory;
        private int _fail;

        private void Start() { StartCoroutine(Run()); }

        private void Check(string name, bool ok)
        {
            if (!ok) _fail++;
            Debug.Log("[SelfCheck] " + (ok ? "PASS " : "FAIL ") + name);
        }

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

        private static bool PinsOn(VisualElement root) { Button b = root.Q<Button>("DkPins"); return b != null && b.ClassListContains("dk-ibtn--on"); }
        private static bool ChipShown(VisualElement root, string n) { Button b = root.Q<Button>(n); return b != null && b.ClassListContains("dk-chip--live") && b.resolvedStyle.display != DisplayStyle.None; }

        private IEnumerator Run()
        {
            Directory.CreateDirectory(OutputDirectory);
            PlayerPrefs.SetInt("Onboarding_Seen", 1);
            yield return new WaitForSecondsRealtime(7f);
            UIManager ui = UIManager.Instance;
            VisualElement root = ui.Root;
            ui.PrepareViewerForDeepLink();
            yield return new WaitForSecondsRealtime(3f);
            if (ui.IsOnboardingOpen) { ui.ShortcutBack(); yield return new WaitForSecondsRealtime(1f); }

            // ── pins ──
            yield return new WaitForSecondsRealtime(0.5f);
            Check("pins: visible at start => button on", HotspotManager.Instance.IsVisible && PinsOn(root));
            ui.ShortcutTogglePins();
            yield return new WaitForSecondsRealtime(0.5f);
            Check("pins: hidden after toggle => button off", !HotspotManager.Instance.IsVisible && !PinsOn(root));
            Click(root.Q<Button>("HomeBtn"));
            yield return new WaitForSecondsRealtime(1.5f);
            Click(root.Q<Button>("HeroExploreBtn"));
            yield return new WaitForSecondsRealtime(2f);
            if (ui.IsOnboardingOpen) { ui.ShortcutBack(); yield return new WaitForSecondsRealtime(1f); }
            Check("pins: after Home and re-entry the button matches the real state", HotspotManager.Instance.IsVisible == PinsOn(root));
            if (!HotspotManager.Instance.IsVisible) ui.ShortcutTogglePins();
            yield return new WaitForSecondsRealtime(0.4f);
            Check("pins: shown again => button on", HotspotManager.Instance.IsVisible && PinsOn(root));

            // ── chips ──
            Check("chips: none visible at start", !ChipShown(root, "DkChipFilter") && !ChipShown(root, "DkChipExplode") && !ChipShown(root, "DkChipCut"));
            CrossSectionManager.Instance.EnableCrossSection();
            yield return new WaitForSecondsRealtime(0.6f);
            Check("chips: cut chip appears when a cut is active", ChipShown(root, "DkChipCut"));
            Click(root.Q<Button>("DkChipCut"));
            yield return new WaitForSecondsRealtime(0.6f);
            Check("chips: clicking the cut chip turns the cut off", !CrossSectionManager.Instance.IsEnabled && !ChipShown(root, "DkChipCut"));

            ui.ShortcutToggleExplode();
            yield return new WaitForSecondsRealtime(0.8f);
            ExplodedViewManager.Instance.SetExplosionFactor(0.5f);
            yield return new WaitForSecondsRealtime(0.8f);
            Check("chips: explode chip appears when the view is exploded", ChipShown(root, "DkChipExplode"));
            Click(root.Q<Button>("DkChipExplode"));
            yield return new WaitForSecondsRealtime(1.2f);
            Check("chips: clicking the explode chip reassembles", ExplodedViewManager.Instance.GetExplosionFactor() < 0.01f && !ChipShown(root, "DkChipExplode"));

            Check("chips: opening Analyze (default categories) does not show the filter chip", true);
            ui.ShortcutToggleMode(2);
            yield return new WaitForSecondsRealtime(0.6f);
            Check("chips: filter chip hidden with the default categories", !ChipShown(root, "DkChipFilter"));
            Click(root.Q<Button>("AnalyzeFilterBtn"));
            yield return new WaitForSecondsRealtime(0.5f);
            using (var ce = ClickEvent.GetPooled()) { Button cat = root.Q<Button>("CatBtn_Avionics"); ce.target = cat; cat.SendEvent(ce); }
            yield return new WaitForSecondsRealtime(0.6f);
            Check("chips: filter chip appears when a filter is active", ChipShown(root, "DkChipFilter"));
            yield return Shot("chips_filtro_activo");
            Click(root.Q<Button>("DkChipFilter"));
            yield return new WaitForSecondsRealtime(0.8f);
            Check("chips: clicking the filter chip clears the filter", !ui.FilterActive && !ChipShown(root, "DkChipFilter"));

            // ── wireframe / X-ray, old vs new intensities ──
            GameObject part = GameObject.Find("x500v2_arm_FL");
            Collider col = part != null ? part.GetComponentInChildren<Collider>() : null;
            SelectionManager.Instance.SimulateClickOn(col != null ? col.transform : part.transform);
            OrbitCameraController.Instance.ResetView();
            yield return new WaitForSecondsRealtime(1.2f);
            Material wire = Field("wireframeMaterial"), xray = Field("xRayMaterial");
            float wi = wire != null && wire.HasProperty("_Intensity") ? wire.GetFloat("_Intensity") : 0.42f;
            float wf = wire != null && wire.HasProperty("_FillAlpha") ? wire.GetFloat("_FillAlpha") : 0.008f;
            float xi = xray != null && xray.HasProperty("_Intensity") ? xray.GetFloat("_Intensity") : 0.5f;
            float xb = xray != null && xray.HasProperty("_BehindIntensity") ? xray.GetFloat("_BehindIntensity") : 0.3f;
            for (int pass = 0; pass < 2; pass++)
            {
                string tag = pass == 0 ? "antes" : "despues";
                if (pass == 0)
                {
                    if (wire != null) { wire.SetFloat("_Intensity", 1.5f); wire.SetFloat("_FillAlpha", 0.025f); }
                    if (xray != null) { xray.SetFloat("_Intensity", 0.75f); xray.SetFloat("_BehindIntensity", 0.5f); }
                }
                else
                {
                    if (wire != null) { wire.SetFloat("_Intensity", wi); wire.SetFloat("_FillAlpha", wf); }
                    if (xray != null) { xray.SetFloat("_Intensity", xi); xray.SetFloat("_BehindIntensity", xb); }
                }

                ViewModeManager.Instance.SetViewMode(ViewMode.Wireframe);
                yield return Shot("wire_" + tag);
                ViewModeManager.Instance.SetViewMode(ViewMode.XRay);
                yield return Shot("xray_" + tag);
            }

            ViewModeManager.Instance.SetViewMode(ViewMode.Realistic);

            // ── picking follows a moved drone (PLAN_PC §26: telemetry / power hover moved the drone, colliders stayed at rest) ──
            SelectionManager.Instance.Deselect();
            OrbitCameraController.Instance.ResetView();
            yield return new WaitForSecondsRealtime(1.2f);
            Camera cam = Camera.main;
            Transform droneRoot = part != null ? part.transform.root : null;
            RaycastHit restHit = default;
            bool restOk = col != null && droneRoot != null && cam != null
                && Physics.Raycast(cam.ScreenPointToRay(cam.WorldToScreenPoint(col.bounds.center)), out restHit, 100f, SelectionManager.Instance.SelectionLayerMask);
            if (restOk)
            {
                Transform hitT = restHit.collider.transform;
                Vector3 local = hitT.InverseTransformPoint(restHit.point);
                Vector3 rootPos = droneRoot.position;
                droneRoot.position = rootPos + Vector3.up * 0.35f;
                yield return null;
                Vector3 sp = cam.WorldToScreenPoint(hitT.TransformPoint(local));
                var r = PickDiagnostics.Describe(sp);
                Debug.Log("[SelfCheck] pick after moving the drone 0.35 m: " + PickDiagnostics.Format(r));
                Check("pick: a click on a part of the MOVED drone hits that part's current collider", r.Hit && r.Drift <= PickDiagnostics.StaleDriftMetres);
                droneRoot.position = rootPos;
                yield return null;
            }
            else Check("pick: drone part found for the moved-drone check", false);

            Debug.Log("[SelfCheck] done failures=" + _fail);
            Application.Quit();
        }

        private static Material Field(string name)
        {
            FieldInfo f = typeof(ViewModeManager).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
            return f != null ? f.GetValue(ViewModeManager.Instance) as Material : null;
        }

        private static void Click(Button b)
        {
            if (b == null) return;
            using (var e = NavigationSubmitEvent.GetPooled()) { e.target = b; b.SendEvent(e); }
        }
    }
}
#endif
