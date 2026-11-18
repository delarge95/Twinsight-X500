#if UNITY_STANDALONE
using UnityEngine;
using UnityEngine.UIElements;
using WebGL.Core.Managers;
using WebGL.Core.Thermal;
using WebGL.Telemetry;

namespace WebGL.UI.Desktop
{
    /// <summary>
    /// MVP P3 (PC only): the card of the thermal field of the telemetry (<see cref="ThermalFieldPC"/>). It shows while the render mode is Thermal and a session runs: the source selector
    /// «Fuente térmica» (Telemetría | Simulación didáctica; the choice is remembered), and, with Telemetría, the legend (inferno gradient, auto-range min / max in °C, «Escala automática.
    /// Isotermas cada 5 °C.») with the origin of every source: six rows «Motor 1 · 31.2 °C» + their tag (estimado, simulado, medido; the flight controller says «no disponible» when
    /// the link does not carry it). With Simulación didáctica the legend is the didactic one (the old thermal legend of the status bar) and the card only says «Un color por pieza
    /// (simulación didáctica, ilustrativo, no FEA).». Telemetría without data: «Sin telemetría — sin datos térmicos» (the model is grey). Honesty: the card never prints a temperature
    /// without its origin; over the flight simulator every origin is «simulado».
    /// </summary>
    internal sealed partial class DesktopTelemetryUI
    {
        private VisualElement _thCard, _thLegendBlock, _thRows, _thGradient;
        private Label _thTitle, _thMin, _thMax, _thNote, _thNoData, _thNoDataHint, _thSimNote;
        private Button _thBtnTel, _thBtnSim;
        private readonly Label[] _thName = new Label[6], _thVal = new Label[6], _thTag = new Label[6];
        private Texture2D _thGradientTex;
        private float _thNext;

        // ── read by the verification harness ──
        public VisualElement ThermalCard => _thCard;
        public bool ThermalCardShown => _thCard != null && _thCard.resolvedStyle.display != DisplayStyle.None;
        public Button ThermalTelemetryButton => _thBtnTel;
        public Button ThermalSimButton => _thBtnSim;
        public string ThermalMinText => _thMin?.text;
        public string ThermalMaxText => _thMax?.text;
        public string ThermalRowText(int i) => _thName[i] == null || _thName[i].parent.resolvedStyle.display == DisplayStyle.None ? null : _thName[i].text + " | " + _thVal[i].text + " | " + _thTag[i].text;
        public string ThermalNoDataText => _thNoData != null && _thNoData.resolvedStyle.display != DisplayStyle.None ? _thNoData.text : null;
        public string ThermalSimNoteText => _thSimNote != null && _thSimNote.resolvedStyle.display != DisplayStyle.None ? _thSimNote.text : null;
        public bool ThermalLegendShown => _thLegendBlock != null && _thLegendBlock.resolvedStyle.display != DisplayStyle.None;

        partial void BuildThermalCard()
        {
            _thCard = new VisualElement { name = "DkThermal", pickingMode = PickingMode.Position };
            _thCard.AddToClassList("dk-thermal");
            _thCard.style.display = DisplayStyle.None;
            _thTitle = Text("dk-map-title", _thCard);

            var sel = new VisualElement();
            sel.AddToClassList("dk-thermal-sel");
            _thBtnTel = ToolButton("DkThermalSrcTel", "dk-b--secondary", () => SetThermalSource(ThermalSourceId.Telemetry));
            _thBtnSim = ToolButton("DkThermalSrcSim", "dk-b--secondary", () => SetThermalSource(ThermalSourceId.Simulation));
            _thBtnTel.AddToClassList("dk-fault-btn");
            _thBtnSim.AddToClassList("dk-fault-btn");
            sel.Add(_thBtnTel);
            sel.Add(_thBtnSim);
            _thCard.Add(sel);

            // the legend of the field
            _thLegendBlock = new VisualElement { pickingMode = PickingMode.Ignore };
            var bar = new VisualElement { pickingMode = PickingMode.Ignore };
            bar.AddToClassList("dk-thermal-bar");
            _thMin = Text("dk-thermal-lbl", bar);
            _thGradient = new VisualElement { name = "DkThermalGradient", pickingMode = PickingMode.Ignore };
            _thGradient.AddToClassList("dk-thermal-grad");
            bar.Add(_thGradient);
            _thMax = Text("dk-thermal-lbl", bar);
            _thLegendBlock.Add(bar);
            _thNote = Text("dk-note", _thLegendBlock);
            _thNote.AddToClassList("dk-note--muted");
            _thRows = new VisualElement { pickingMode = PickingMode.Ignore };
            for (int i = 0; i < 6; i++)
            {
                var row = new VisualElement { pickingMode = PickingMode.Ignore };
                row.AddToClassList("dk-thermal-row");
                _thName[i] = Text("dk-thermal-name", row);
                _thVal[i] = Text("dk-thermal-val", row);
                _thTag[i] = Text("dk-tag", row);
                _thRows.Add(row);
            }
            _thLegendBlock.Add(_thRows);
            _thCard.Add(_thLegendBlock);

            _thNoData = Text("dk-note", _thCard);
            _thNoData.AddToClassList("dk-note--warn");
            _thNoDataHint = Text("dk-note", _thCard);
            _thNoDataHint.AddToClassList("dk-note--muted");
            _thSimNote = Text("dk-note", _thCard);
            _thSimNote.AddToClassList("dk-note--muted");
            _root.Add(_thCard);
        }

        partial void DisposeThermal()
        {
            _thCard?.RemoveFromHierarchy();
            if (_thGradientTex != null) { Object.Destroy(_thGradientTex); _thGradientTex = null; }
        }

        partial void RelabelThermal()
        {
            if (_thCard == null) return;
            _thTitle.text = T("studio.thermalSource");
            _thBtnTel.text = T("studio.srcTelemetry");
            _thBtnSim.text = T("studio.srcSim");
            _thNote.text = T("legend.note");
            _thNoData.text = T("legend.noData");
            _thNoDataHint.text = T("legend.noDataHint");
            _thSimNote.text = T("legend.noteSim");
            _thNext = 0f;
        }

        /// <summary>Chooses the source of the Thermal mode (remembered): «Telemetría» = the quantitative field, «Simulación didáctica» = the didactic simulation.</summary>
        public void SetThermalSource(ThermalSourceId s)
        {
            ThermalFieldPC.Chosen = s;
            _thNext = 0f;
        }

        /// <summary>A 1 x 64 inferno strip of the scale (position 0 sits at the palette floor like the shader: the cool end is dark purple, not black).</summary>
        private Texture2D ThermalGradientTexture()
        {
            if (_thGradientTex != null) return _thGradientTex;
            const int w = 64;
            _thGradientTex = new Texture2D(w, 1, TextureFormat.RGBA32, false, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "ThermalFieldLegend" };
            for (int x = 0; x < w; x++)
            {
                double s = x / (double)(w - 1);
                double pal = 0.12 + (1 - 0.12) * s;
                double t = System.Math.Min(1, System.Math.Max(0, pal)) * 5.0;
                int i = System.Math.Min(4, (int)System.Math.Floor(t));
                double f = t - i;
                Color c = Color.Lerp(Inferno[i], Inferno[i + 1], (float)f);
                _thGradientTex.SetPixel(x, 0, c);
            }
            _thGradientTex.Apply(false, false);
            return _thGradientTex;
        }

        private static readonly Color[] Inferno =
        {
            new Color(0.001f, 0f, 0.014f), new Color(0.258f, 0.039f, 0.406f), new Color(0.578f, 0.148f, 0.404f),
            new Color(0.865f, 0.317f, 0.226f), new Color(0.988f, 0.645f, 0.04f), new Color(0.988f, 0.998f, 0.645f),
        };

        partial void TickThermal(bool viewer)
        {
            if (_thCard == null) return;
            bool thermalMode = ViewModeManager.Instance != null && ViewModeManager.Instance.CurrentMode == ViewMode.Thermal;
            bool show = viewer && thermalMode && DesktopTelemetry.IsRunning;
            if ((_thCard.resolvedStyle.display != DisplayStyle.None) != show) _thCard.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (!show) return;
            if (Time.unscaledTime < _thNext) return;
            _thNext = Time.unscaledTime + 0.1f;

            ThermalSourceId eff = ThermalFieldPC.EffectiveSource;
            _thBtnTel.EnableInClassList("dk-b--on", eff == ThermalSourceId.Telemetry);
            _thBtnSim.EnableInClassList("dk-b--on", eff == ThermalSourceId.Simulation);
            bool tel = eff == ThermalSourceId.Telemetry;
            ThermalFieldPC fld = ThermalFieldPC.Instance;
            bool data = tel && fld != null && ThermalFieldPC.Active && !fld.NoData;
            _thLegendBlock.style.display = data ? DisplayStyle.Flex : DisplayStyle.None;
            _thNoData.style.display = tel && !data ? DisplayStyle.Flex : DisplayStyle.None;
            _thNoDataHint.style.display = tel && !data ? DisplayStyle.Flex : DisplayStyle.None;
            _thSimNote.style.display = tel ? DisplayStyle.None : DisplayStyle.Flex;
            if (!data) return;

            string lang = Lang;
            if (_thGradient.style.backgroundImage.value.texture == null) _thGradient.style.backgroundImage = new StyleBackground(ThermalGradientTexture());
            ThermalRange r = fld.Range;
            _thMin.text = Mathf.RoundToInt((float)r.min) + " °C";
            _thMax.text = Mathf.RoundToInt((float)r.max) + " °C";

            // the origin of every source: from the link's provenance (simulator: every figure «simulado»)
            TelemetryClient client = DesktopTelemetry.Client;
            HelloMessage h = client.Hello;
            SourceClassification c = client.Classification;
            bool simulator = DesktopTelemetry.IsSimulator;
            HonestyLabels labels = HonestyLabels.Load();
            for (int i = 0; i < 6; i++)
            {
                ReadingOrigin o = FlightOrigins.ForSession(i < 4 ? FlightOrigins.MotorTemp(h, c) : i == 4 ? FlightOrigins.BatteryTemp(h, c) : FlightOrigins.FcTemp(h, c), simulator);
                _thName[i].text = i < 4 ? TelemetryStrings.F("thermal.src.motor", lang, "n", i + 1) : T(i == 4 ? "thermal.src.battery" : "thermal.src.fc");
                bool available = o != ReadingOrigin.Unavailable;
                _thVal[i].text = available ? JsMath.ToFixed(fld.TempOf(i), 1) + " °C" : "—";
                _thTag[i].text = FlightOrigins.Text(o, lang, labels);
                _thTag[i].EnableInClassList("dk-tag--measured", o == ReadingOrigin.Measured);
                _thTag[i].EnableInClassList("dk-tag--estimated", o == ReadingOrigin.Estimated);
                _thTag[i].EnableInClassList("dk-tag--simulated", o == ReadingOrigin.Simulated);
                _thTag[i].EnableInClassList("dk-tag--na", o == ReadingOrigin.Unavailable || o == ReadingOrigin.Command);
            }
        }
    }
}
#endif
