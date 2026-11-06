#if UNITY_STANDALONE
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;
using WebGL.Core.Managers;
using WebGL.UI.Panels;

namespace WebGL.UI.Desktop
{
    /// <summary>
    /// Verification aid (only with <c>-desk-demoui &lt;folder&gt;</c> or <c>-desk-sliderprobe &lt;folder&gt; &lt;cut|explode|env|wheel&gt;</c>).
    /// demoui: captures every UI surface/state of the PC build and logs which pickable surfaces cover the canvas.
    /// sliderprobe: prepares a state with a slider (or the part tree) and logs camera/selection every 0.2 s so an
    /// external real-mouse script can drag the slider far outside its bounds and the log proves nothing moved.
    /// </summary>
    internal sealed class DesktopCaptureDemoUI : MonoBehaviour
    {
        public string OutputDirectory;
        public string ProbeKind;   // null = inventory

        private readonly StringBuilder _log = new StringBuilder();

        private bool _heroOnly;
        private void Start() { _heroOnly = ProbeKind == "hero"; StartCoroutine(ProbeKind == "tutorial" ? RunTutorial() : string.IsNullOrEmpty(ProbeKind) || _heroOnly ? RunInventory() : RunProbe()); }
        private void Log(string line) { _log.AppendLine(line); Debug.Log("[DesktopDemoUI] " + line); }

        private IEnumerator Shot(string name)
        {
            yield return new WaitForSecondsRealtime(0.9f);
            yield return new WaitForEndOfFrame();
            var tex = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
            tex.Apply();
            File.WriteAllBytes(Path.Combine(OutputDirectory, name + ".png"), ImageConversion.EncodeToPNG(tex));
            Destroy(tex);
            Cover(name);
        }

        private static void Click(Button b)
        {
            if (b == null) return;
            using (var e = NavigationSubmitEvent.GetPooled()) { e.target = b; b.SendEvent(e); }
        }

        // ── surfaces covering the canvas ──
        private static bool Visible(VisualElement e)
        {
            for (VisualElement c = e; c != null; c = c.parent)
            {
                if (c.resolvedStyle.display == DisplayStyle.None || c.resolvedStyle.visibility == Visibility.Hidden
                    || c.resolvedStyle.opacity < 0.05f) return false;
            }
            return true;
        }

        private static void Scan(VisualElement e, VisualElement root, Rect screenLogical, List<VisualElement> output)
        {
            if (e != root)
            {
                if (e.resolvedStyle.display == DisplayStyle.None || e.resolvedStyle.visibility == Visibility.Hidden
                    || e.resolvedStyle.opacity < 0.05f) return;
                Rect r = e.worldBound;
                if (e.pickingMode == PickingMode.Position && r.width * r.height > 300f && r.Overlaps(screenLogical)
                    && e.name != "WorldSpaceContainer" && Visible(e))
                {
                    output.Add(e);
                    return;
                }
            }
            foreach (VisualElement child in e.Children()) Scan(child, root, screenLogical, output);
        }

        private void Cover(string name)
        {
            VisualElement root = UIManager.Instance.Root;
            float s = DesktopUIController.Instance.UiScale;
            var screenLogical = new Rect(0, 0, Screen.width / s, Screen.height / s);
            var list = new List<VisualElement>();
            Scan(root, root, screenLogical, list);
            const int gx = 192, gy = 108;
            var grid = new bool[gx * gy];
            var gridO = new bool[gx * gy];
            var sb = new StringBuilder();
            foreach (VisualElement e in list)
            {
                Rect r = e.worldBound;
                float px = r.xMin * s, py = r.yMin * s, pw = r.width * s, ph = r.height * s;
                float pct = 100f * pw * ph / (Screen.width * (float)Screen.height);
                string cls = string.Join(".", e.GetClasses());
                sb.Append($"\n    - {e.name}.{(cls.Length > 40 ? cls.Substring(0, 40) : cls)} {pw:F0}x{ph:F0}@{px:F0},{py:F0} ({pct:F1}%)");
                int x0 = Mathf.Clamp((int)(px / Screen.width * gx), 0, gx - 1), x1 = Mathf.Clamp((int)((px + pw) / Screen.width * gx), 0, gx - 1);
                int y0 = Mathf.Clamp((int)(py / Screen.height * gy), 0, gy - 1), y1 = Mathf.Clamp((int)((py + ph) / Screen.height * gy), 0, gy - 1);
                for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++) grid[y * gx + x] = true;
                bool docked = e.name == "DkTopBar" || e.name == "DkRailL" || e.name == "DkRailR" || e.name == "DkStatus" || e.ClassListContains("details-sheet") || e.ClassListContains("desktop-tree") || e.ClassListContains("hotspot-dot");
                if (!docked) for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++) gridO[y * gx + x] = true;
            }
            int n = 0; foreach (bool b in grid) if (b) n++;
            int nO = 0; foreach (bool b in gridO) if (b) nO++;
            Log($"[{name}] cubre {100f * n / grid.Length:F1}% (acoplado+superpuesto), {100f * nO / gridO.Length:F1}% superpuesto al modelo, con {list.Count} superficies:{sb}");
        }

        private static GameObject FindPart() => GameObject.Find("x500v2_arm_FL");

        private IEnumerator Prepare()
        {
            Directory.CreateDirectory(OutputDirectory);
            PlayerPrefs.SetInt("Onboarding_Seen", 1);
            yield return new WaitForSecondsRealtime(6f);
        }

        private IEnumerator RunInventory()
        {
            yield return Prepare();
            UIManager ui = UIManager.Instance;
            DesktopUIController ctrl = DesktopUIController.Instance;
            VisualElement root = ui.Root;
            OrbitCameraController oc = OrbitCameraController.Instance;
            SelectionManager sm = SelectionManager.Instance;
            Log($"screen={Screen.width}x{Screen.height} scale={ctrl.UiScale:F2}");

            if (_heroOnly)
            {
                yield return Shot("01_hero");
                Click(root.Q<Button>("HeroConfigBtn")); yield return Shot("02_hero_config");
                Click(root.Q<Button>("SubmenuBackBtn_Config"));
                yield return new WaitForSecondsRealtime(0.6f);
                Click(root.Q<Button>("HeroDeviceBtn")); yield return Shot("03_hero_dispositivos");
                File.WriteAllText(Path.Combine(OutputDirectory, "hero_cobertura.log"), _log.ToString());
                Application.Quit();
                yield break;
            }

            ui.PrepareViewerForDeepLink();
            yield return new WaitForSecondsRealtime(3f);
            if (ui.IsOnboardingOpen) { ui.ShortcutBack(); yield return new WaitForSecondsRealtime(1f); }
            oc.ResetView(); oc.SetAngles(-30f, 22f, true); oc.SetDistance(8.5f, true);

            yield return new WaitForSecondsRealtime(1.5f);
            yield return Shot("05_visor_reposo");

            ui.ShortcutToggleMode(1); yield return new WaitForSecondsRealtime(0.8f);
            yield return Shot("06_modo_inspect");
            Click(root.Q<Button>("ToolPowerBtn")); yield return new WaitForSecondsRealtime(0.8f);
            yield return Shot("07_inspect_power");
            Click(root.Q<Button>("ToolPowerBtn"));
            ui.ShortcutToggleMode(1); yield return new WaitForSecondsRealtime(0.6f);

            ui.ShortcutToggleMode(2); yield return new WaitForSecondsRealtime(0.8f);
            yield return Shot("08_modo_analyze");
            Click(root.Q<Button>("AnalyzeCrossSectionBtn")); yield return new WaitForSecondsRealtime(0.8f);
            yield return Shot("09_analyze_cut");
            ui.ShortcutBack(); yield return new WaitForSecondsRealtime(0.6f);
            Click(root.Q<Button>("AnalyzeExplodeBtn")); yield return new WaitForSecondsRealtime(0.8f);
            yield return Shot("10_analyze_explode");
            Click(root.Q<Button>("AnalyzeExplodeBtn")); yield return new WaitForSecondsRealtime(0.5f);
            Click(root.Q<Button>("AnalyzeFilterBtn")); yield return new WaitForSecondsRealtime(0.8f);
            yield return Shot("11_analyze_filter");
            ui.ShortcutBack(); yield return new WaitForSecondsRealtime(0.5f);
            ui.ShortcutToggleMode(2); yield return new WaitForSecondsRealtime(0.6f);

            ui.ShortcutToggleMode(3); yield return new WaitForSecondsRealtime(0.8f);
            yield return Shot("12_modo_studio");
            Click(root.Q<Button>("ShaderMode_Thermal")); yield return new WaitForSecondsRealtime(1f);
            yield return Shot("13_studio_thermal_leyenda");
            Click(root.Q<Button>("ShaderMode_XRay")); yield return new WaitForSecondsRealtime(1f);
            yield return Shot("14_studio_xray");
            Click(root.Q<Button>("EnvPreset_Night")); yield return new WaitForSecondsRealtime(1f);
            yield return Shot("15_studio_entorno_night");
            Click(root.Q<Button>("EnvPreset_Studio"));
            ViewModeManager.Instance.SetViewMode(ViewMode.Realistic);
            ui.ShortcutToggleMode(3); yield return new WaitForSecondsRealtime(0.6f);

            GameObject part = FindPart();
            Collider col = part != null ? part.GetComponentInChildren<Collider>() : null;
            sm.SimulateClickOn(col != null ? col.transform : part.transform);
            yield return new WaitForSecondsRealtime(0.8f);
            yield return Shot("16_pieza_seleccionada_peek");
            UIDetailsSheet.Instance.ShowInfo(); yield return new WaitForSecondsRealtime(2.8f);
            yield return Shot("17_part_info_panel");
            ui.ShortcutToggleMode(2); yield return new WaitForSecondsRealtime(0.8f);
            Click(root.Q<Button>("AnalyzeCrossSectionBtn")); yield return new WaitForSecondsRealtime(0.8f);
            yield return Shot("18_panel_mas_analyze_cut");
            ui.ShortcutBack(); ui.ShortcutToggleMode(2);
            ui.ShortcutToggleMode(3); yield return new WaitForSecondsRealtime(0.8f);
            yield return Shot("19_panel_mas_studio");
            ui.ShortcutToggleMode(3);
            ctrl.Tree.Open(false); yield return new WaitForSecondsRealtime(0.5f);
            yield return Shot("20_arbol_piezas_mas_panel");
            ctrl.Tree.SetOpen(false);
            UIDetailsSheet.Instance.SetSheetState(false);
            sm.SimulateClickOn(null);
            yield return new WaitForSecondsRealtime(0.6f);
            ctrl.Tree.Open(false); yield return new WaitForSecondsRealtime(0.5f);
            yield return Shot("21_arbol_piezas");
            ctrl.Tree.SetOpen(false);
            ctrl.SetHelpOpen(true); yield return new WaitForSecondsRealtime(0.6f);
            yield return Shot("22_ayuda_atajos");
            ctrl.SetHelpOpen(false);
            ctrl.ShowTooltipFor("ModeAnalyzeBtn"); yield return new WaitForSecondsRealtime(0.6f);
            yield return Shot("23_tooltip");

            {
                FieldInfo f = typeof(UIManager).GetField("_onboardingController", BindingFlags.NonPublic | BindingFlags.Instance);
                object onboarding = f?.GetValue(ui);
                onboarding?.GetType().GetMethod("Show")?.Invoke(onboarding, null);
                yield return new WaitForSecondsRealtime(1.5f);
                yield return Shot("04_tutorial_paso1");
            }
            File.WriteAllText(Path.Combine(OutputDirectory, "inventario_cobertura.log"), _log.ToString());
            Application.Quit();
        }

        // ── hero + 6-step tutorial (phase 5) ──
        private IEnumerator RunTutorial()
        {
            yield return Prepare();
            UIManager ui = UIManager.Instance;
            VisualElement root = ui.Root;
            string tag = Screen.width + "x" + Screen.height;
            yield return Shot("10_hero_" + tag);
            Click(root.Q<Button>("HeroConfigBtn")); yield return Shot("11_hero_config_" + tag);
            Click(root.Q<Button>("SubmenuBackBtn_Config")); yield return new WaitForSecondsRealtime(0.6f);
            Click(root.Q<Button>("HeroInfoBtn")); yield return Shot("12_hero_acerca_" + tag);
            Click(root.Q<Button>("HeroHelpBtn")); yield return new WaitForSecondsRealtime(1.0f);
            FieldInfo f = typeof(UIManager).GetField("_onboardingController", BindingFlags.NonPublic | BindingFlags.Instance);
            object onboarding = f?.GetValue(ui);
            var desk = onboarding?.GetType().GetProperty("Tutorial", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(onboarding) as TutorialOverlay;
            Log("tutorial abierto=" + ui.IsOnboardingOpen + " desktop=" + (desk != null));
            for (int i = 0; i < 6; i++)
            {
                desk?.GoTo(i);
                yield return Shot("13_tutorial_paso" + (i + 1) + "_" + tag);
            }

            desk?.GoTo(0);
            ViewModeManager.Instance?.SetViewMode(ViewMode.Realistic);
            File.WriteAllText(Path.Combine(OutputDirectory, "tutorial_" + tag + ".log"), _log.ToString());
            Application.Quit();
        }

        // ── slider probe ──
        private IEnumerator RunProbe()
        {
            yield return Prepare();
            UIManager ui = UIManager.Instance;
            VisualElement root = ui.Root;
            DesktopUIController ctrl = DesktopUIController.Instance;
            SelectionManager sm = SelectionManager.Instance;
            OrbitCameraController oc = OrbitCameraController.Instance;
            ui.PrepareViewerForDeepLink();
            yield return new WaitForSecondsRealtime(3f);
            if (ui.IsOnboardingOpen) { ui.ShortcutBack(); yield return new WaitForSecondsRealtime(1f); }
            oc.ResetView(); oc.SetAngles(-30f, 22f, true); oc.SetDistance(8f, true);
            yield return new WaitForSecondsRealtime(1.5f);

            GameObject part = FindPart();
            Collider col = part.GetComponentInChildren<Collider>();
            sm.SimulateClickOn(col != null ? col.transform : part.transform);
            yield return new WaitForSecondsRealtime(1f);

            if (ProbeKind == "chrome")
            {
                string tag = Screen.width + "x" + Screen.height;
                string[] cl = Environment.GetCommandLineArgs();
                for (int ai = 0; ai < cl.Length - 1; ai++)
                {
                    if (cl[ai] == "-uiscale" && float.TryParse(cl[ai + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float us2))
                    {
                        AccessibilityManager.Instance?.SetUIScale(us2);
                        tag += "_ui" + Mathf.RoundToInt(us2 * 100f);
                    }
                }

                yield return new WaitForSecondsRealtime(0.6f);
                DesktopChrome chrome = ctrl.Chrome;
                sm.SimulateClickOn(null);
                oc.ResetView(); oc.SetAngles(-30f, 22f, true); oc.SetDistance(8.5f, true);
                yield return new WaitForSecondsRealtime(1.2f);
                yield return Shot("01_reposo_" + tag);
                GameObject arm = FindPart();
                Collider ac = arm.GetComponentInChildren<Collider>();
                sm.SimulateClickOn(ac != null ? ac.transform : arm.transform);
                yield return new WaitForSecondsRealtime(1.8f);
                yield return Shot("02_pieza_inspector_" + tag);
                if (!ctrl.SidePanel.IsOpen) { chrome.ToggleInspectorPublic(); yield return new WaitForSecondsRealtime(1.2f); yield return Shot("02b_pieza_inspector_manual_" + tag); }
                ui.ShortcutToggleMode(1); yield return new WaitForSecondsRealtime(0.8f);
                yield return Shot("03a_inspeccionar_" + tag);
                Click(root.Q<Button>("ToolPowerBtn")); yield return new WaitForSecondsRealtime(0.8f);
                yield return Shot("03b_energia_" + tag);
                Click(root.Q<Button>("ToolPowerBtn"));
                ui.ShortcutToggleMode(2); yield return new WaitForSecondsRealtime(0.8f);
                Click(root.Q<Button>("AnalyzeCrossSectionBtn")); yield return new WaitForSecondsRealtime(0.8f);
                yield return Shot("04a_analizar_corte_" + tag);
                Click(root.Q<Button>("CrossSectionBackBtn")); yield return new WaitForSecondsRealtime(0.5f);
                Click(root.Q<Button>("AnalyzeExplodeBtn")); yield return new WaitForSecondsRealtime(0.8f);
                yield return Shot("04b_analizar_explotar_" + tag);
                Click(root.Q<Button>("AnalyzeExplodeBtn")); yield return new WaitForSecondsRealtime(0.6f);
                Click(root.Q<Button>("AnalyzeFilterBtn")); yield return new WaitForSecondsRealtime(0.8f);
                yield return Shot("04c_analizar_filtro_" + tag);
                Click(root.Q<Button>("FilterBackBtn")); yield return new WaitForSecondsRealtime(0.4f);
                ui.ShortcutToggleMode(3); yield return new WaitForSecondsRealtime(1.0f);
                yield return Shot("05a_estudio_render_" + tag);
                Click(root.Q<Button>("ShaderMode_Thermal")); yield return new WaitForSecondsRealtime(1.0f);
                yield return Shot("05b_estudio_termico_" + tag);
                Click(root.Q<Button>("DkStudioEnv")); yield return new WaitForSecondsRealtime(0.6f);
                yield return Shot("05c_estudio_entorno_" + tag);
                ViewModeManager.Instance.SetViewMode(ViewMode.Realistic);
                ui.ShortcutToggleMode(3); yield return new WaitForSecondsRealtime(0.6f);
                ctrl.Tree.Open(false); yield return new WaitForSecondsRealtime(0.8f);
                yield return Shot("06_arbol_" + tag);
                ctrl.Tree.SetOpen(false);
                chrome.OpenSettings(); yield return new WaitForSecondsRealtime(0.8f);
                yield return Shot("07_ajustes_" + tag);
                chrome.CloseSettings();
                ctrl.SetHelpOpen(true); yield return new WaitForSecondsRealtime(0.6f);
                yield return Shot("08_ayuda_" + tag);
                ctrl.SetHelpOpen(false);
                File.WriteAllText(Path.Combine(OutputDirectory, "cobertura_" + tag + ".log"), _log.ToString());
                Application.Quit();
                yield break;
            }

            if (ProbeKind == "flow")
            {
                DesktopChrome chrome = ctrl.Chrome;
                sm.SimulateClickOn(null);
                oc.ResetView(); oc.SetAngles(-30f, 22f, true); oc.SetDistance(8.5f, true);
                yield return new WaitForSecondsRealtime(1.5f);
                string posFile = Path.Combine(OutputDirectory, "flow_pos.txt");
                string[] names = { "DkMode1", "DkMode2", "DkMode3", "DkTree", "DkInspector", "DkSettings", "DkHelp", "DkRailTree", "DkRailInspector",
                    "ToolHotspotBtn", "ToolPowerBtn", "AnalyzeCrossSectionBtn", "AnalyzeExplodeBtn", "AnalyzeFilterBtn", "CrossSectionPosition", "CrossSectionBackBtn",
                    "DkStudioRender", "DkStudioEnv", "ShaderMode_Thermal", "EnvPreset_Night", "EnvLightRotation", "DkSettingsClose", "QualityBtn_Low", "DkFocus", "SheetCloseBtn" };
                GameObject armGo = FindPart();
                Collider armCol = armGo.GetComponentInChildren<Collider>();
                Debug.Log("[flow] READY");
                File.WriteAllText(Path.Combine(OutputDirectory, "flow.ready"), "ready");
                float endFlow = Time.realtimeSinceStartup + 150f;
                float us = ctrl.UiScale;
                while (Time.realtimeSinceStartup < endFlow)
                {
                    var sbp = new StringBuilder();
                    foreach (string n in names)
                    {
                        VisualElement e = root.Q<VisualElement>(n);
                        if (e == null || !Visible(e)) continue;
                        VisualElement t = e.Q<VisualElement>(className: "unity-base-slider__dragger") ?? e;
                        Rect r = t.worldBound;
                        sbp.Append($"{n} {(r.center.x * us).ToString("F0", System.Globalization.CultureInfo.InvariantCulture)} {(r.center.y * us).ToString("F0", System.Globalization.CultureInfo.InvariantCulture)}\n");
                    }
                    Vector3 sp = Camera.main.WorldToScreenPoint(armCol.bounds.center);
                    sbp.Append($"ARM {sp.x.ToString("F0", System.Globalization.CultureInfo.InvariantCulture)} {(Screen.height - sp.y).ToString("F0", System.Globalization.CultureInfo.InvariantCulture)}\n");
                    try { File.WriteAllText(posFile, sbp.ToString()); } catch (IOException) { }
                    string mode = root.Q<Button>("DkMode1").ClassListContains("dk-seg-btn--on") ? "1" : root.Q<Button>("DkMode2").ClassListContains("dk-seg-btn--on") ? "2" : root.Q<Button>("DkMode3").ClassListContains("dk-seg-btn--on") ? "3" : "-";
                    oc.DesktopGetTargets(out Vector3 fc, out float dist);
                    Slider cutS = root.Q<Slider>("CrossSectionPosition");
                    Slider envS = root.Q<Slider>("EnvLightRotation");
                    string step = ""; try { string sf = Path.Combine(OutputDirectory, "flow_step.txt"); if (File.Exists(sf)) step = File.ReadAllText(sf).Trim(); } catch (IOException) { }
                    Debug.Log($"[flow] step={step} mode={mode} insp={ctrl.SidePanel.IsOpen} tree={ctrl.Tree.IsOpen} settings={chrome.IsSettingsOpen} help={ctrl.IsHelpOpen} sel={(sm.CurrentSelection != null ? sm.CurrentSelection.name : "none")} cut={(cutS != null ? cutS.value : -9f):F2} env={(envS != null ? envS.value : -9f):F0} view={ViewModeManager.Instance.CurrentMode} dist={dist:F2} cam={Camera.main.transform.eulerAngles.y:F1}");
                    yield return new WaitForSecondsRealtime(0.2f);
                }
                Application.Quit();
                yield break;
            }

            if (ProbeKind == "clean")
            {
                foreach (var doc in FindObjectsByType<UIDocument>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (doc.rootVisualElement != null) doc.rootVisualElement.style.display = DisplayStyle.None;
                sm.SimulateClickOn(null);
                oc.ResetView(); oc.SetAngles(-30f, 22f, true); oc.SetDistance(8.5f, true);
                yield return new WaitForSecondsRealtime(1.5f);
                yield return Shot("clean_idle");
                GameObject gps = GameObject.Find("x500v2_gps_m10");
                Collider gc = gps != null ? gps.GetComponentInChildren<Collider>() : null;
                sm.SimulateClickOn(gc != null ? gc.transform : gps.transform);
                yield return new WaitForSecondsRealtime(1.2f);
                yield return Shot("clean_gps_selected");
                File.WriteAllText(Path.Combine(OutputDirectory, "clean.done"), "ok");
                Application.Quit();
                yield break;
            }
            VisualElement target = null;
            switch (ProbeKind)
            {
                case "cut":
                    ui.ShortcutToggleMode(2); yield return new WaitForSecondsRealtime(0.8f);
                    Click(root.Q<Button>("AnalyzeCrossSectionBtn")); yield return new WaitForSecondsRealtime(1f);
                    target = root.Q<VisualElement>("CrossSectionPosition");
                    break;
                case "explode":
                    ui.ShortcutToggleMode(2); yield return new WaitForSecondsRealtime(0.8f);
                    Click(root.Q<Button>("AnalyzeExplodeBtn")); yield return new WaitForSecondsRealtime(1f);
                    target = root.Q<VisualElement>("ExplosionSlider");
                    break;
                case "env":
                    ui.ShortcutToggleMode(3); yield return new WaitForSecondsRealtime(1f);
                    target = root.Q<VisualElement>("EnvLightRotation");
                    break;
                case "diag":
                    {
                        int n = 0;
                        foreach (Renderer r in FindPart().GetComponentsInChildren<Renderer>(true))
                        {
                            if (n++ > 6) break;
                            Material m = r.sharedMaterial;
                            Debug.Log($"[diag] {r.name} enabled={r.enabled} visible={r.isVisible} layer={r.gameObject.layer} mat={(m != null ? m.name : "null")} shader={(m != null && m.shader != null ? m.shader.name : "null")} supported={(m != null && m.shader != null && m.shader.isSupported)} kw={(m != null ? string.Join(",", m.shaderKeywords) : "")} bounds={r.bounds.center}/{r.bounds.size}");
                        }
                        Camera c = Camera.main;
                        Debug.Log($"[diag] cam pos={c.transform.position} cull={c.cullingMask} near={c.nearClipPlane} far={c.farClipPlane} fov={c.fieldOfView} rp={UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline?.name}");
                        break;
                    }
                case "wheel":
                    ctrl.Tree.Open(false); yield return new WaitForSecondsRealtime(0.8f);
                    target = root.Q<VisualElement>(className: "desktop-tree-scroll");
                    break;
            }

            if (target == null) { Log("PROBE target not found for " + ProbeKind); Application.Quit(); yield break; }
            VisualElement thumb = target.Q<VisualElement>(className: "unity-base-slider__dragger") ?? target;
            float s = ctrl.UiScale;
            Rect tr = thumb.worldBound, br = target.worldBound;
            Log($"PROBE kind={ProbeKind} screen={Screen.width}x{Screen.height} thumbPx={(tr.center.x * s):F1},{(tr.center.y * s):F1} trackPx={(br.xMin * s):F0},{(br.yMin * s):F0},{(br.xMax * s):F0},{(br.yMax * s):F0}");
            Debug.Log("[probe] READY " + _log.ToString().Replace("\n", " "));
            File.WriteAllText(Path.Combine(OutputDirectory, "probe_" + ProbeKind + ".ready"), _log.ToString());

            float end = Time.realtimeSinceStartup + 40f;
            while (Time.realtimeSinceStartup < end)
            {
                oc.DesktopGetTargets(out Vector3 focus, out float dist);
                Vector3 e = Camera.main.transform.eulerAngles;
                string sel = sm.CurrentSelection != null ? sm.CurrentSelection.name : "none";
                string hov = sm.HoveredObject != null ? sm.HoveredObject.name : "none";
                float val = (target as Slider)?.value ?? (target as SliderInt)?.value ?? -1f;
                Debug.Log($"[probe] euler={e.x:F1},{e.y:F1} dist={dist:F2} focus={focus.x:F2},{focus.y:F2},{focus.z:F2} sel={sel} hover={hov} slider={val:F2} mouse={Input.mousePosition.x:F0},{Input.mousePosition.y:F0} focused={Application.isFocused} pick={InputManager.Instance.DescribePickAt(Input.mousePosition)}");
                yield return new WaitForSecondsRealtime(0.2f);
            }
            Application.Quit();
        }
    }
}
#endif
