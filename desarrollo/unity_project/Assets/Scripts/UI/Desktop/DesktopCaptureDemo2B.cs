#if UNITY_STANDALONE
using System;
using System.Collections;
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
    /// Verification aid for phase 2B (only active with <c>-desk-demo2b &lt;folder&gt;</c>). Writes the checklist
    /// screenshots (Hero, tutorial step 1, viewer, side panel, foldout, side panel + ANALYZE) and
    /// <c>demo2b_WxH.log</c> with the measurements (part centred in the free area, capsule vs panel overlap,
    /// zoom-to-cursor drift), then quits.
    /// </summary>
    internal sealed class DesktopCaptureDemo2B : MonoBehaviour
    {
        public string OutputDirectory;

        private readonly StringBuilder _log = new StringBuilder();
        private string _tag;

        private void Start()
        {
            StartCoroutine(Run());
        }

        private void Log(string line)
        {
            _log.AppendLine(line);
            Debug.Log("[DesktopDemo2B] " + line);
        }

        private IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            string path = Path.Combine(OutputDirectory, name + "_" + _tag + ".png");
            var tex = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
            tex.Apply();
            File.WriteAllBytes(path, ImageConversion.EncodeToPNG(tex));
            Destroy(tex);
            yield return new WaitForSecondsRealtime(0.3f);
        }

        private static void Click(Button b)
        {
            using (var e = NavigationSubmitEvent.GetPooled())
            {
                e.target = b;
                b.SendEvent(e);
            }
        }

        private static Bounds BoundsOf(Transform t)
        {
            var renderers = t.GetComponentsInChildren<Renderer>();
            Bounds b = default;
            bool first = true;
            foreach (Renderer r in renderers)
            {
                if (first) { b = r.bounds; first = false; }
                else b.Encapsulate(r.bounds);
            }

            return b;
        }

        private IEnumerator Run()
        {
            Directory.CreateDirectory(OutputDirectory);
            yield return new WaitForSecondsRealtime(5f);

            _tag = Screen.width + "x" + Screen.height;
            UIManager ui = UIManager.Instance;
            DesktopUIController ctrl = DesktopUIController.Instance;
            VisualElement root = ui.Root;
            Camera cam = Camera.main;
            Log($"screen={Screen.width}x{Screen.height} scale={ctrl.Scaler.CurrentScale:F3} logical={ctrl.Scaler.LogicalSize} layoutDesktop={root.ClassListContains("layout--desktop")} touchSupported={Input.touchSupported} isMobile={Application.isMobilePlatform}");

            // 1) Hero
            yield return Shot("05_hero");

            // 2) viewer; the tutorial (first run) or opened by hand: step 1 in PC mode
            ui.PrepareViewerForDeepLink();
            yield return new WaitForSecondsRealtime(2.5f);
            if (!ui.IsOnboardingOpen)
            {
                FieldInfo f = typeof(UIManager).GetField("_onboardingController", BindingFlags.NonPublic | BindingFlags.Instance);
                object onboarding = f?.GetValue(ui);
                onboarding?.GetType().GetMethod("Show")?.Invoke(onboarding, null);
                yield return new WaitForSecondsRealtime(1.5f);
            }

            var stepDesc = root.Q<Label>("OnboardStepDesc");
            Log($"tutorial open={ui.IsOnboardingOpen} step1 text=\"{stepDesc?.text?.Replace("\n", " | ")}\" platformSwitchPc={root.Q<Button>("OnboardPlatformSwitch")?.ClassListContains("onboard-platform-switch--pc")}");
            yield return Shot("06_tutorial_step1");
            ui.ShortcutBack();
            yield return new WaitForSecondsRealtime(1.2f);
            if (ui.IsOnboardingOpen) { ui.ShortcutBack(); yield return new WaitForSecondsRealtime(1f); }

            OrbitCameraController.Instance?.ResetView();
            yield return new WaitForSecondsRealtime(1.5f);

            // 3) viewer without selection
            yield return Shot("01_viewer");

            // 4) a part selected + panel open
            GameObject partGo = GameObject.Find("x500v2_arm_FL");
            if (partGo == null || SelectionManager.Instance == null)
            {
                Log("arm FL not found");
                File.WriteAllText(Path.Combine(OutputDirectory, "demo2b_" + _tag + ".log"), _log.ToString());
                Application.Quit();
                yield break;
            }

            SelectionManager sm = SelectionManager.Instance;
            Collider col = partGo.GetComponentInChildren<Collider>();
            sm.SimulateClickOn(col != null ? col.transform : partGo.transform);
            yield return new WaitForSecondsRealtime(0.8f);
            Log($"peek visible={root.Q<VisualElement>("InfoBarPeek").ClassListContains("info-bar-peek--visible")} (closed panel)");
            yield return Shot("07_selected_peek");

            UIDetailsSheet.Instance.ShowInfo();
            yield return new WaitForSecondsRealtime(2.8f);

            VisualElement sheet = root.Q<VisualElement>("BottomSheet");
            VisualElement bar = root.Q<VisualElement>("BottomBar");
            VisualElement pill = root.Q<VisualElement>(className: "actions-row");
            float scale = ctrl.Scaler.CurrentScale;
            Rect sheetRect = sheet.worldBound;
            Rect pillRect = pill.worldBound;
            float freeCenterPx = (sheetRect.xMin - DesktopSidePanel.Margin) * 0.5f * scale;
            Bounds partBounds = BoundsOf(sm.CurrentSelection != null ? sm.CurrentSelection : partGo.transform);
            Vector3 partScreen = cam.WorldToScreenPoint(partBounds.center);
            OrbitCameraController.Instance.DesktopGetTargets(out Vector3 dbgFocus, out float dbgDist);
            Log($"camera pos={cam.transform.position} euler={cam.transform.eulerAngles} focusTarget={dbgFocus} dist={dbgDist:F2} partCenter={partBounds.center} partSize={partBounds.size} fov={cam.fieldOfView} aspect={cam.aspect:F3}");
            Log($"panel logical rect={sheetRect} width={sheetRect.width:F0} top={sheetRect.yMin:F0} bottomGap={root.worldBound.height - sheetRect.yMax:F0}");
            Log($"capsule rect={pillRect} overlapsPanel={pillRect.Overlaps(sheetRect)} capsuleCentre={(pillRect.center.x * scale):F0}px freeAreaCentre={freeCenterPx:F0}px");
            Log($"selected part screen x={partScreen.x:F0}px (free-area centre {freeCenterPx:F0}px, screen centre {Screen.width * 0.5f:F0}px) y={partScreen.y:F0}px of {Screen.height}");
            var desc = root.Q<Label>("PartDescription");
            Log($"description font={desc.resolvedStyle.fontSize} width={desc.resolvedStyle.width:F0}px text=\"{desc.text}\"");
            yield return Shot("02_panel_selected");

            // 5) a foldout open
            var specs = root.Q<Foldout>("FoldoutSpecifications");
            var ident = root.Q<Foldout>("FoldoutIdentification");
            if (ident != null) ident.value = true;
            if (specs != null) specs.value = true;
            yield return new WaitForSecondsRealtime(0.8f);
            var valueLabel = root.Q<Label>("PartMaterial");
            if (valueLabel != null)
            {
                Log($"data value font={valueLabel.resolvedStyle.fontSize} width={valueLabel.resolvedStyle.width:F0}px text=\"{valueLabel.text}\"");
            }

            yield return Shot("03_panel_foldout");
            if (ident != null) ident.value = false;
            if (specs != null) specs.value = false;

            // 6) panel + ANALYZE
            ui.ShortcutToggleMode(2);
            yield return new WaitForSecondsRealtime(1f);
            VisualElement grid = root.Q<VisualElement>("AnalyzeCardGrid");
            Log($"analyze grid rect={grid.worldBound} overlapsPanel={grid.worldBound.Overlaps(sheet.worldBound)} gridCentreX={(grid.worldBound.center.x * scale):F0}px");
            yield return Shot("04_panel_analyze");

            Click(root.Q<Button>("AnalyzeCrossSectionBtn"));
            yield return new WaitForSecondsRealtime(1f);
            VisualElement cut = root.Q<VisualElement>("CrossSectionPanel");
            VisualElement analyze = root.Q<VisualElement>("AnalyzeModeContainer");
            Log($"cut panel rect={cut.worldBound} analyzeContainer={analyze.worldBound} overlapsPanel={analyze.worldBound.Overlaps(sheet.worldBound)} top={analyze.worldBound.yMin:F0} header bottom={root.Q<VisualElement>("TopBar").worldBound.yMax:F0}");
            yield return Shot("04b_panel_analyze_cut");
            ui.ShortcutBack();
            ui.ShortcutToggleMode(2);
            yield return new WaitForSecondsRealtime(0.8f);

            // 7) zoom toward the cursor: the world point under the cursor must stay under the cursor
            UIDetailsSheet.Instance.SetSheetState(false);
            sm.SimulateClickOn(null);
            OrbitCameraController oc = OrbitCameraController.Instance;
            oc.ResetView();
            yield return new WaitForSecondsRealtime(1.5f);
            foreach (Vector2 cursor in new[] { new Vector2(Screen.width * 0.30f, Screen.height * 0.55f), new Vector2(Screen.width * 0.62f, Screen.height * 0.40f) })
            {
                oc.DesktopSnapToTargets();
                yield return null;
                Ray ray = cam.ScreenPointToRay(cursor);
                Vector3 hitPoint;
                if (Physics.Raycast(ray, out RaycastHit hit, 100f)) hitPoint = hit.point;
                else
                {
                    oc.DesktopGetTargets(out Vector3 focus, out float _);
                    float t = Vector3.Dot(focus - ray.origin, cam.transform.forward) / Vector3.Dot(ray.direction, cam.transform.forward);
                    hitPoint = ray.origin + ray.direction * t;
                }

                Vector3 before = cam.WorldToScreenPoint(hitPoint);
                oc.DesktopGetTargets(out _, out float d0);
                oc.DesktopZoomAt(cursor, 1.5f);
                oc.DesktopSnapToTargets();
                yield return null;
                yield return null;
                Vector3 after = cam.WorldToScreenPoint(hitPoint);
                oc.DesktopGetTargets(out _, out float d1);
                Log($"zoom-to-cursor cursor={cursor} dist {d0:F2}->{d1:F2} point screen before={before.x:F1},{before.y:F1} after={after.x:F1},{after.y:F1} drift={(new Vector2(after.x - before.x, after.y - before.y)).magnitude:F2}px");
            }

            Log("done");
            File.WriteAllText(Path.Combine(OutputDirectory, "demo2b_" + _tag + ".log"), _log.ToString());
            Application.Quit();
        }
    }
}
#endif
