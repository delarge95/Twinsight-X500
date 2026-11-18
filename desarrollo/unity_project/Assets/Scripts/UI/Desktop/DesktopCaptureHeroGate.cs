#if UNITY_STANDALONE
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;
using WebGL.UI.Panels;

namespace WebGL.UI.Desktop
{
    /// <summary>
    /// Post-build gate aid (-hero-gate &lt;folder&gt;, plus -lang es|en and -screen-width/height): captures the hero, the «Modelo» submenu and the viewer,
    /// and logs PASS / FAIL for: Model-slot labels fit their buttons, pins start OFF with the pins button off. Written to hero_gate_*.log.
    /// </summary>
    internal sealed class DesktopCaptureHeroGate : MonoBehaviour
    {
        public string OutputDirectory;
        private readonly StringBuilder _log = new StringBuilder();
        private int _fail;
        private void Start() { StartCoroutine(Run()); }
        private void Check(string n, bool ok) { if (!ok) _fail++; _log.AppendLine((ok ? "PASS " : "FAIL ") + n); Debug.Log("[HeroGate] " + (ok ? "PASS " : "FAIL ") + n); }
        private static void Click(Button b) { if (b == null) return; using (var e = NavigationSubmitEvent.GetPooled()) { e.target = b; b.SendEvent(e); } }

        private IEnumerator Shot(string tag, string name)
        {
            yield return new WaitForSecondsRealtime(0.8f);
            yield return new WaitForEndOfFrame();
            var tex = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
            tex.Apply();
            File.WriteAllBytes(Path.Combine(OutputDirectory, tag + "_" + name + ".png"), ImageConversion.EncodeToPNG(tex));
            Destroy(tex);
        }

        private IEnumerator Run()
        {
            Directory.CreateDirectory(OutputDirectory);
            PlayerPrefs.SetInt("Onboarding_Seen", 1);
            yield return new WaitForSecondsRealtime(6f);
            string tag = Screen.width + "x" + Screen.height + "_" + (AppLanguageManager.IsSpanish ? "es" : "en");
            UIManager ui = UIManager.Instance; VisualElement root = ui.Root;
            _log.AppendLine("window " + tag);
            yield return Shot(tag, "01_hero");
            Click(root.Q<Button>("HeroDeviceBtn"));
            yield return Shot(tag, "02_modelo");
            foreach (string n in new[] { "DeviceBtn_Drone", "DeviceBtn_Robot", "DeviceBtn_Satellite" })
            {
                Button b = root.Q<Button>(n);
                bool fits = false;
                if (b != null)
                {
                    Vector2 sz = b.MeasureTextSize(b.text, 0, VisualElement.MeasureMode.Undefined, 0, VisualElement.MeasureMode.Undefined);
                    float avail = b.resolvedStyle.width - b.resolvedStyle.paddingLeft - b.resolvedStyle.paddingRight;
                    fits = sz.x <= avail;
                    _log.AppendLine(n + " text='" + b.text + "' textW=" + sz.x.ToString("0") + " availW=" + avail.ToString("0") + " tooltip='" + b.tooltip + "'");
                }
                Check("Modelo: " + n + " label fits the button", fits);
            }
            Click(root.Q<Button>("SubmenuBackBtn_Devices"));
            yield return new WaitForSecondsRealtime(1f);
            Click(root.Q<Button>("HeroExploreBtn"));
            yield return new WaitForSecondsRealtime(3.5f);
            Button pins = root.Q<Button>("DkPins");
            bool pinsOn = pins != null && pins.ClassListContains("dk-ibtn--on");
            Check("pins: hidden at start", HotspotManager.Instance != null && !HotspotManager.Instance.IsVisible);
            Check("pins: button off at start", !pinsOn);
            yield return Shot(tag, "03_visor");
            _log.AppendLine(_fail == 0 ? "ALL PASS" : "FAILS " + _fail);
            File.WriteAllText(Path.Combine(OutputDirectory, "hero_gate_" + tag + ".log"), _log.ToString());
            Application.Quit();
        }
    }
}
#endif
