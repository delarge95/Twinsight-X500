#if UNITY_STANDALONE
using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;
using WebGL.Core.Managers;
using WebGL.Telemetry;

namespace WebGL.UI.Desktop
{
    /// <summary>
    /// Lote 1a, tool 4 (PC only): the simulated wind of the FLIGHT SIMULATOR. A strip button «Viento» opens a popover with a direction dial («de dónde viene», draggable), a
    /// speed slider (0..12 m/s), a gust slider (0..4 m/s), the seed with «Otra» («la misma semilla repite las mismas ráfagas») and «Sin viento», and the reading
    /// «Viento 6 m/s del NO · ráfagas ±2 m/s · simulado». A thin arrow beside the drone shows where the wind blows to, its length proportional to the speed
    /// (<see cref="TelemetryWindCue"/>). Persisted in PlayerPrefs Desk_SimWind. Honesty: the wind is local state of the simulator («Viento del simulador · simulado»),
    /// a simple model (it pushes the drone, leans it into the wind and makes it burn more current, in flight only), NOT PX4 and NOT a real drone; in PX4 SITL the
    /// wind is Gazebo's and is neither read nor offered (the button is dimmed). The logic is the pure <see cref="Wind"/> / <see cref="Rng"/> / <see cref="FlightSim"/> (golden-tested).
    /// </summary>
    internal sealed partial class DesktopTelemetryUI
    {
        private Button _btnWind;
        private Label _windBtnLabel;
        private VisualElement _popWind;
        private Label _wnTitle, _wnTag, _wnReading, _wnNow, _wnDirLabel, _wnDirValue, _wnSeed, _wnRefused, _wnHonest;
        private Slider _wnSpeed, _wnGust;
        private Label _wnSpeedLabel, _wnGustLabel;
        private Button _wnSeedNew, _wnReset;
        private DkWindDial _wnDial;
        private WindConfig _wind;
        private TelemetryWindCue _windCue;
        private string _windRefusedCode;
        private double _windRefusedMin, _windRefusedMax;
        private float _windRefusedUntil;

        // ── read by the verification harness ──
        public Button WindButton => _btnWind;
        public VisualElement WindPopover => _popWind;
        public WindConfig WindRemembered => _wind;
        public TelemetryWindCue WindCue => _windCue;
        public Slider WindSpeedSlider => _wnSpeed;
        public Slider WindGustSlider => _wnGust;
        public DkWindDial WindDial => _wnDial;
        public Button WindSeedButton => _wnSeedNew;
        public Button WindResetButton => _wnReset;
        public string WindReadingText => _wnReading?.text;
        public string WindRefusal => _wnRefused != null && _wnRefused.style.display != DisplayStyle.None ? _wnRefused.text : null;

        partial void BuildWindButton(VisualElement strip)
        {
            _wind = SimSettings.LoadWind();
            _btnWind = StripButton("DkTelWind", "wind", out _windBtnLabel, () => TogglePopover("wind"));
            strip.Add(_btnWind);
        }

        partial void BuildWindPopover()
        {
            _popWind = NewPopover("DkPopWind", "dk-pop--w280", out _wnTitle);
            var tagRow = new VisualElement();
            tagRow.AddToClassList("dk-fault-tagrow");
            _wnTag = Text("dk-tag", tagRow);
            _wnTag.AddToClassList("dk-tag--simulated");
            _popWind.Add(tagRow);
            _wnReading = Text("dk-note", _popWind);
            _wnNow = Text("dk-note", _popWind);
            _wnNow.AddToClassList("dk-note--muted");

            // direction: the dial and its words
            var dirRow = new VisualElement();
            dirRow.AddToClassList("dk-wind-dirrow");
            _wnDial = new DkWindDial { name = "DkWindDial" };
            _wnDial.Changed += deg => ApplyWind(WindWith(w => w.fromDeg = deg));
            dirRow.Add(_wnDial);
            var dirText = new VisualElement { pickingMode = PickingMode.Ignore };
            dirText.AddToClassList("dk-wind-dirtext");
            _wnDirLabel = Text("dk-field-label", dirText);
            _wnDirValue = Text("dk-wind-dirvalue", dirText);
            dirRow.Add(dirText);
            _popWind.Add(dirRow);

            _wnSpeed = ToolSlider(_popWind, "DkWindSpeed", (float)Wind.SpeedMin, (float)Wind.SpeedMax, v => ApplyWind(WindWith(w => w.speed = Mathf.Round(v * 2f) / 2f)), out _wnSpeedLabel);
            _wnGust = ToolSlider(_popWind, "DkWindGust", (float)Wind.GustMin, (float)Wind.GustMax, v => ApplyWind(WindWith(w => w.gustSigma = Mathf.Round(v * 2f) / 2f)), out _wnGustLabel);

            _wnRefused = Text("dk-note", _popWind);
            _wnRefused.AddToClassList("dk-note--warn");
            _wnRefused.style.display = DisplayStyle.None;

            var foot = new VisualElement();
            foot.AddToClassList("dk-fault-foot");
            _wnSeed = Text("dk-note", foot);
            _wnSeedNew = ToolButton("DkWindSeedNew", "dk-b--ghost", () =>
            {
                // Not the model: the person asks for a different sequence, so the number comes from here; the model only receives it.
                double seed = Math.Floor(UnityEngine.Random.value * (Wind.SeedMax + 1));
                ApplyWind(WindWith(w => w.seed = Math.Min(seed, Wind.SeedMax)));
            });
            _wnSeedNew.AddToClassList("dk-fault-footbtn");
            _wnReset = ToolButton("DkWindReset", "dk-b--ghost", () => ApplyWind(WindWith(w => { w.speed = 0; w.gustSigma = 0; })));
            _wnReset.AddToClassList("dk-fault-footbtn");
            foot.Add(_wnSeedNew);
            foot.Add(_wnReset);
            _popWind.Add(foot);
            _wnHonest = Text("dk-note", _popWind);
            _wnHonest.AddToClassList("dk-note--muted");
            DesktopInfo.Beside(_wnTag, false, "DkInfoWind", _wnHonest); // UX step 5: the long honesty note behind an ⓘ next to the «simulado» tag
            RegisterToolPopover("wind", _popWind);
            RefreshWindControls();
        }

        partial void DisposeWind()
        {
            DestroyWindCue();
            _popWind?.RemoveFromHierarchy();
        }

        partial void RelabelWind()
        {
            if (_btnWind == null || _popWind == null) return;
            _windBtnLabel.text = T("tools.wind");
            _wnTitle.text = T("wind.title");
            _wnTag.text = T("wind.origin");
            _wnDirLabel.text = T("wind.dir");
            _wnSeedNew.text = T("wind.seedNew");
            _wnSeedNew.tooltip = T("wind.seedHint");
            _wnReset.text = T("wind.reset");
            _wnHonest.text = T("wind.seedHint") + " " + T("wind.honest");
            RefreshWindControls();
        }

        /// <summary>The wind the simulator has now (what is applied), or the remembered one while no simulator runs.</summary>
        private WindConfig WindNowConfig()
        {
            var src = RunningSimulator;
            return src != null ? src.Model.cfg.wind : _wind;
        }

        private WindConfig WindWith(Action<WindConfig> change)
        {
            WindConfig w = WindNowConfig().Clone();
            change(w);
            return w;
        }

        private static string FmtNum(double x) => x.ToString("0.#", CultureInfo.GetCultureInfo(Lang));

        /// <summary>"Viento 6 m/s del NO · ráfagas ±2 m/s · simulado": every value on screen says where it comes from.</summary>
        public static string WindReading(WindConfig w, string lang)
        {
            if (w.speed <= 0 && w.gustSigma <= 0) return TelemetryStrings.T("wind.readingCalm", lang);
            string dir = TelemetryStrings.T("wind.dir." + Wind.CompassIndex(w.fromDeg), lang);
            var ci = CultureInfo.GetCultureInfo(lang);
            return TelemetryStrings.F(w.gustSigma > 0 ? "wind.reading" : "wind.readingSteady", lang, "speed", w.speed.ToString("0.#", ci), "dir", dir, "gust", w.gustSigma.ToString("0.#", ci));
        }

        private void RefreshWindControls()
        {
            if (_wnSpeed == null) return;
            WindConfig w = WindNowConfig();
            string lang = Lang;
            _wnSpeed.SetValueWithoutNotify((float)w.speed);
            _wnGust.SetValueWithoutNotify((float)w.gustSigma);
            _wnSpeedLabel.text = T("wind.speed") + " · " + FmtNum(w.speed) + " m/s";
            _wnGustLabel.text = T("wind.gusts") + " · " + TelemetryStrings.F("wind.gustsValue", lang, "n", FmtNum(w.gustSigma));
            _wnDial.SetDegrees((float)(w.fromDeg % 360));
            _wnDirValue.text = TelemetryStrings.F("wind.dirValue", lang, "deg", FmtNum(w.fromDeg % 360), "name", T("wind.dir." + Wind.CompassIndex(w.fromDeg)));
            _wnReading.text = WindReading(w, lang);
            _wnSeed.text = TelemetryStrings.F("wind.seed", lang, "n", w.seed.ToString("0", CultureInfo.InvariantCulture));
            bool calm = w.speed == 0 && w.gustSigma == 0;
            _wnReset?.SetEnabled(!calm);
        }

        /// <summary>Applies a wind to the running simulator (a refusal leaves everything as it was and says why) and remembers what was accepted.</summary>
        public ConfigCheck ApplyWind(WindConfig next)
        {
            var src = RunningSimulator;
            ConfigCheck r;
            if (src != null)
                r = src.SetConfig(new SimConfigPatch { wind = new WindPatch { speed = next.speed, fromDeg = next.fromDeg, gustSigma = next.gustSigma, gustTau = next.gustTau, seed = next.seed } });
            else
                r = Wind.Validate(next);
            if (r.ok)
            {
                _wind = next.Clone();
                SimSettings.SaveWind(_wind);
                _windRefusedCode = null;
                _wnRefused.style.display = DisplayStyle.None;
            }
            else
            {
                _windRefusedCode = r.code;
                _windRefusedMin = r.min ?? 0;
                _windRefusedMax = r.max ?? 0;
                _windRefusedUntil = Time.unscaledTime + 8f;
                ShowWindRefusal();
            }
            RefreshWindControls();
            return r;
        }

        private void ShowWindRefusal()
        {
            if (_windRefusedCode == null) { _wnRefused.style.display = DisplayStyle.None; return; }
            _wnRefused.text = TelemetryStrings.F("wind.err." + _windRefusedCode, Lang, "min", FmtNum(_windRefusedMin), "max", FmtNum(_windRefusedMax));
            _wnRefused.style.display = DisplayStyle.Flex;
        }

        partial void TickWindPopover()
        {
            if (_windRefusedCode != null && Time.unscaledTime > _windRefusedUntil) { _windRefusedCode = null; _wnRefused.style.display = DisplayStyle.None; }
            RefreshWindControls();
            var src = RunningSimulator;
            bool show = false;
            if (src != null)
            {
                FlightSim.WindNow(src.Model, out double wn, out double we);
                double speed = Math.Sqrt(wn * wn + we * we);
                show = speed >= 0.05;
                if (show)
                {
                    double fromDeg = Math.Atan2(-we, -wn) * 180 / Math.PI;
                    _wnNow.text = TelemetryStrings.F("wind.now", Lang, "speed", FmtNum(speed), "dir", T("wind.dir." + Wind.CompassIndex(fromDeg)));
                }
            }
            _wnNow.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void DestroyWindCue()
        {
            if (_windCue != null) { UnityEngine.Object.Destroy(_windCue.gameObject); _windCue = null; }
        }

        partial void TickWind(bool viewer)
        {
            if (_btnWind == null) return;
            bool sim = DesktopTelemetry.IsRunning && DesktopTelemetry.IsSimulator;
            _btnWind.SetEnabled(sim);
            _btnWind.EnableInClassList("dk-strip-btn--on", _openPop == "wind");
            string tip = sim ? WindReading(WindNowConfig(), Lang) : T("tools.wind.need");
            if (_btnWind.tooltip != tip) _btnWind.tooltip = tip;
            if (!sim && _openPop == "wind") CloseAllPopovers();

            var drv = TelemetryDroneDriver.Instance;
            var client = DesktopTelemetry.Client;
            TelemetryFrame f = client != null ? client.Displayed : null;
            bool active = sim && f != null && drv != null && drv.Engaged && drv.GroundRef != null && !drv.GroundRef.IsLegacy;
            if (!active) { if (_windCue != null) DestroyWindCue(); return; }
            if (_windCue == null)
            {
                _windCue = TelemetryWindCue.Create(drv.UnitsPerMeter);
                if (_windCue == null) return;
            }
            FlightSim.WindNow(RunningSimulator.Model, out double wn, out double we);
            _windCue.Place(DronePose.BodyPosition(drv.RestPose, f.p), wn, we);
        }
    }

    /// <summary>The direction dial of the wind popover: a ring with a knob at where the wind comes FROM and an arrow inside that points to where it blows to. Drag the knob.</summary>
    internal sealed class DkWindDial : VisualElement
    {
        public event Action<float> Changed;
        private float _deg;
        private bool _dragging;

        public float Degrees => _deg;

        public DkWindDial()
        {
            AddToClassList("dk-wind-dial");
            style.width = 76;
            style.height = 76;
            style.minWidth = 76;
            style.minHeight = 76;
            generateVisualContent += Draw;
            RegisterCallback<PointerDownEvent>(e =>
            {
                if (e.button != 0) return;
                _dragging = true;
                this.CapturePointer(e.pointerId);
                InputManager.InputBlocked = true;
                FromPointer(e.localPosition);
                e.StopPropagation();
            });
            RegisterCallback<PointerMoveEvent>(e => { if (_dragging) { FromPointer(e.localPosition); e.StopPropagation(); } });
            RegisterCallback<PointerUpEvent>(e =>
            {
                if (!_dragging) return;
                _dragging = false;
                this.ReleasePointer(e.pointerId);
                InputManager.InputBlocked = false;
                e.StopPropagation();
            });
            RegisterCallback<PointerCaptureOutEvent>(_ => { _dragging = false; InputManager.InputBlocked = false; });
        }

        public void SetDegrees(float deg)
        {
            deg = ((deg % 360f) + 360f) % 360f;
            if (Mathf.Approximately(deg, _deg)) return;
            _deg = deg;
            MarkDirtyRepaint();
        }

        private void FromPointer(Vector2 p)
        {
            Vector2 c = new Vector2(layout.width * 0.5f, layout.height * 0.5f);
            float dx = p.x - c.x, dy = p.y - c.y;
            if (Mathf.Sqrt(dx * dx + dy * dy) < 6f) return;
            float deg = Mathf.Atan2(dx, -dy) * Mathf.Rad2Deg; // from north, clockwise (the screen's y grows downwards)
            deg = (((Mathf.Round(deg / 5f) * 5f) % 360f) + 360f) % 360f;
            if (Mathf.Approximately(deg, _deg)) return;
            _deg = deg;
            MarkDirtyRepaint();
            Changed?.Invoke(deg);
        }

        private void Draw(MeshGenerationContext ctx)
        {
            Painter2D p = ctx.painter2D;
            float w = layout.width, h = layout.height;
            if (w < 8f || h < 8f) return;
            Vector2 c = new Vector2(w * 0.5f, h * 0.5f);
            float R = Mathf.Min(w, h) * 0.5f - 9f;
            Color ring = new Color(1f, 1f, 1f, 0.28f), accent = new Color32(70, 175, 255, 255), soft = new Color32(191, 234, 255, 255);
            p.lineWidth = 1.5f;
            p.strokeColor = ring;
            p.BeginPath(); p.Arc(c, R, 0f, 360f); p.Stroke();
            // the north notch
            p.BeginPath(); p.MoveTo(new Vector2(c.x, c.y - R - 4f)); p.LineTo(new Vector2(c.x, c.y - R + 5f)); p.Stroke();
            float a = _deg * Mathf.Deg2Rad;
            Vector2 from = c + new Vector2(Mathf.Sin(a), -Mathf.Cos(a)) * R;      // where the wind comes from
            Vector2 to = c - new Vector2(Mathf.Sin(a), -Mathf.Cos(a)) * (R - 6f); // where it blows to
            Vector2 tail = c + new Vector2(Mathf.Sin(a), -Mathf.Cos(a)) * (R - 14f);
            p.lineWidth = 2f;
            p.strokeColor = soft;
            p.BeginPath(); p.MoveTo(tail); p.LineTo(to); p.Stroke();
            Vector2 dir = (to - tail).normalized, side = new Vector2(-dir.y, dir.x);
            p.fillColor = soft;
            p.BeginPath(); p.MoveTo(to); p.LineTo(to - dir * 9f + side * 4.5f); p.LineTo(to - dir * 9f - side * 4.5f); p.ClosePath(); p.Fill();
            p.fillColor = accent;
            p.BeginPath(); p.Arc(from, 6f, 0f, 360f); p.Fill();
        }
    }
}
#endif
