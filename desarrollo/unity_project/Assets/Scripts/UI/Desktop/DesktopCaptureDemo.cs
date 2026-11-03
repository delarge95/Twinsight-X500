#if UNITY_STANDALONE
using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;
using WebGL.Core.Content;
using WebGL.Core.Managers;

namespace WebGL.UI.Desktop
{
    /// <summary>
    /// Verification aid for the PC build (only active with <c>-desk-demo &lt;folder&gt;</c>). It drives the UI
    /// through the states the phase-2A checklist needs, writes PNG screenshots with
    /// <see cref="ScreenCapture"/> (works even if the window is bigger than the monitor) and a
    /// <c>demo.log</c> with the measurements, then quits.
    /// </summary>
    internal sealed class DesktopCaptureDemo : MonoBehaviour
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
            Debug.Log("[DesktopDemo] " + line);
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

        private IEnumerator Run()
        {
            Directory.CreateDirectory(OutputDirectory);
            yield return new WaitForSecondsRealtime(5f);

            _tag = Screen.width + "x" + Screen.height;
            UIManager ui = UIManager.Instance;
            DesktopUIController ctrl = DesktopUIController.Instance;
            VisualElement root = ui.Root;
            Log($"screen={Screen.width}x{Screen.height} fullscreen={Screen.fullScreenMode} scale={ctrl.Scaler.CurrentScale:F3} logical={ctrl.Scaler.LogicalSize} compact={root.ClassListContains("ui-compact")} vsync={QualitySettings.vSyncCount} display={Screen.mainWindowDisplayInfo.width}x{Screen.mainWindowDisplayInfo.height} work={Screen.mainWindowDisplayInfo.workArea}");

            // 1) viewer, capsule fully visible
            ui.PrepareViewerForDeepLink();
            yield return new WaitForSecondsRealtime(2f);
            if (ui.IsOnboardingOpen)
            {
                Log("onboarding was open (first run): closing it with Esc");
                ui.ShortcutBack();
            }

            yield return new WaitForSecondsRealtime(1.5f);
            VisualElement bar = root.Q<VisualElement>("BottomBar");
            VisualElement pill = root.Q<VisualElement>(className: "actions-row");
            Log($"capsule worldBound={pill.worldBound} panelH={root.worldBound.height} fits={(pill.worldBound.yMax <= root.worldBound.height + 0.5f)}");
            yield return Shot("01_viewer");

            // 2) tooltip with shortcut (INSPECT mode open, hover PINS)
            ui.ShortcutToggleMode(1);
            yield return new WaitForSecondsRealtime(0.8f);
            Button pins = root.Q<Button>("ToolHotspotBtn");
            ctrl.Tooltip.ShowFor(pins);
            yield return new WaitForSecondsRealtime(0.6f);
            yield return Shot("02_tooltip_pins");
            ctrl.Tooltip.Hide();
            ui.ShortcutToggleMode(1);
            yield return new WaitForSecondsRealtime(0.5f);

            // 3) analyze + studio panels (fit check at this size)
            ui.ShortcutToggleMode(3);
            yield return new WaitForSecondsRealtime(0.8f);
            VisualElement studio = root.Q<VisualElement>("StudioPanel");
            Log($"studioPanel worldBound={studio.worldBound} top={studio.worldBound.yMin:F0} (header bottom={root.Q<VisualElement>("TopBar").worldBound.yMax:F0})");
            yield return Shot("03_studio");
            ui.ShortcutToggleMode(3);
            yield return new WaitForSecondsRealtime(0.5f);

            // 4) help overlay
            ctrl.Help.Open();
            yield return new WaitForSecondsRealtime(0.8f);
            yield return Shot("04_help");
            ctrl.Help.Close();
            yield return new WaitForSecondsRealtime(0.4f);

            // 5) motor selection (simulated clicks through the real selection logic)
            GameObject motor = GameObject.Find("x500v2_motor_FL");
            GameObject arm = GameObject.Find("x500v2_arm_FL");
            if (motor != null && arm != null && SelectionManager.Instance != null)
            {
                SelectionManager sm = SelectionManager.Instance;
                Collider motorCol = motor.GetComponentInChildren<Collider>();
                Collider armCol = arm.GetComponentInChildren<Collider>();

                sm.SimulateClickOn(motorCol != null ? motorCol.transform : motor.transform);
                Log($"click motor -> selection={sm.CurrentSelection?.name} full={sm.CurrentFullSelection?.name} sub={sm.CurrentSubSelection?.name}");
                OrbitCameraController.Instance?.FocusOnObject(motor.transform);
                yield return new WaitForSecondsRealtime(2.5f);
                yield return Shot("05_motor_selected");

                sm.SimulateClickOn(armCol != null ? armCol.transform : arm.transform);
                Log($"click arm -> selection={sm.CurrentSelection?.name} full={sm.CurrentFullSelection?.name} sub={sm.CurrentSubSelection?.name}");
                yield return new WaitForSecondsRealtime(0.6f);

                sm.SimulateClickOn(null);
                Log($"click background -> selection={sm.CurrentSelection?.name ?? "none"}");
            }
            else
            {
                Log("motor/arm FL not found");
            }

            // 6) CONFIG panel (display section)
            Button home = root.Q<Button>("HomeBtn");
            Click(home);
            yield return new WaitForSecondsRealtime(1f);
            Click(root.Q<Button>("HeroConfigBtn"));
            yield return new WaitForSecondsRealtime(1f);
            ScrollView cfgScroll = root.Q<VisualElement>("HeroSubmenu_Config")?.Q<ScrollView>();
            VisualElement section = root.Q<VisualElement>("DesktopDisplaySection");
            if (cfgScroll != null && section != null)
            {
                cfgScroll.ScrollTo(section);
            }

            yield return new WaitForSecondsRealtime(0.8f);
            yield return Shot("06_config");

            Log("done");
            File.WriteAllText(Path.Combine(OutputDirectory, "demo_" + _tag + ".log"), _log.ToString());
            Application.Quit();
        }
    }
}
#endif
