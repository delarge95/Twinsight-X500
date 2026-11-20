#if UNITY_STANDALONE
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using WebGL.Core.Managers;
using WebGL.Telemetry;
using WebGL.UI.Panels;

namespace WebGL.UI.Desktop
{
    /// <summary>PLAN_PC 39, steps 4 and 5 of the desktop UX cut: the ⓘ (hover, focus, Tab, full text, inside the window) and the «Avanzado» fold, and the honesty scan of the tree.</summary>
    internal sealed partial class DesktopCaptureUx
    {
        private const string LongText = "Texto largo de prueba para el ⓘ: explica en varias frases lo que una nota larga explicaba antes en una fila fija. Debe verse completo, con saltos de linea, sin cortarse y dentro de la ventana, aunque el icono este en una esquina. Ultima frase del texto de prueba FIN.";

        private VisualElement TipEl => Root.Q<VisualElement>("DesktopTooltip");
        private Label TipText => TipEl?.Q<Label>(className: "desktop-tooltip-text");
        private bool TipShown => TipEl != null && TipEl.resolvedStyle.display != DisplayStyle.None && TipEl.resolvedStyle.visibility == Visibility.Visible && TipEl.resolvedStyle.opacity > 0.5f;

        private static void SendOver(VisualElement e, bool over)
        {
            if (over) { using (var ev = PointerOverEvent.GetPooled()) { ev.target = e; e.SendEvent(ev); } }
            else { using (var ev = PointerOutEvent.GetPooled()) { ev.target = e; e.SendEvent(ev); } }
        }

        private IEnumerator CheckTipFor(string label, Button info, string expected)
        {
            yield return new WaitForSecondsRealtime(0.8f);
            VisualElement root = Root;
            Check(label + ": the tooltip shows the full text", TipShown && TipText != null && TipText.text == expected);
            Rect tip = TipEl.worldBound;
            Rect win = DesktopTooltip.WindowRect(root);
            Check(label + ": the tooltip is wrapped (several lines) and at most 380 px wide (" + F1(tip.width) + " x " + F1(tip.height) + ")", tip.width <= 382f && tip.height > 40f);
            Log(label + " tip " + tip + " window " + win);
            Check(label + ": the tooltip is inside the window", tip.xMin >= win.xMin - 1f && tip.yMin >= win.yMin - 1f && tip.xMax <= win.xMax + 1f && tip.yMax <= win.yMax + 1f);
            Check(label + ": the text is not cut (label as tall as its content, no nowrap)", TipText.resolvedStyle.whiteSpace != WhiteSpace.NoWrap);
        }

        private IEnumerator InfoAndFoldChecks()
        {
            Log("── paso 4: ⓘ y Avanzado ──");
            VisualElement root = Root;
            var host = new VisualElement { name = "UxInfoHost" };
            host.style.position = Position.Absolute;
            host.style.left = 0; host.style.top = 0; host.style.right = 0; host.style.bottom = 0;
            host.pickingMode = PickingMode.Ignore;
            root.Insert(0, host);

            // corner ⓘ at the top-left and at the bottom-right: the bubble must stay inside the window in both
            Button tl = DesktopInfo.Create("UxInfoTL", LongText);
            tl.style.position = Position.Absolute; tl.style.left = 2; tl.style.top = 2;
            Button br = DesktopInfo.Create("UxInfoBR", LongText);
            br.style.position = Position.Absolute; br.style.right = 2; br.style.bottom = 2;
            // Tab row: button, ⓘ, button
            var row = new VisualElement { name = "UxInfoRow" };
            row.style.position = Position.Absolute; row.style.left = 400; row.style.top = 200; row.style.flexDirection = FlexDirection.Row;
            var b0 = new Button { name = "UxTabB0", text = "A" };
            Button mid = DesktopInfo.Create("UxInfoMid", LongText);
            var b2 = new Button { name = "UxTabB2", text = "B" };
            row.Add(b0); row.Add(mid); row.Add(b2);
            host.Add(tl); host.Add(br); host.Add(row);
            yield return new WaitForSecondsRealtime(0.4f);
            float tw = tl.resolvedStyle.width, th = tl.resolvedStyle.height;
            float iw = tl.Q<VisualElement>(className: "dk-icon").resolvedStyle.width;
            Check("the ⓘ hit target is at least 24 x 24 px (" + F1(tw) + " x " + F1(th) + ", icon " + F1(iw) + " px)", tw >= 23.5f && th >= 23.5f && iw >= 14f);
            Check("the ⓘ can take the keyboard focus", tl.focusable && tl.tabIndex >= 0);

            // hover
            SendOver(tl, true);
            yield return CheckTipFor("hover (top-left corner)", tl, LongText);
            yield return Shot("ux_info_hover_esquina", 0.05f);
            SendOver(tl, false);
            yield return new WaitForSecondsRealtime(0.3f);
            Check("the tooltip hides when the pointer leaves", !TipShown);
            SendOver(br, true);
            yield return CheckTipFor("hover (bottom-right corner)", br, LongText);
            SendOver(br, false);
            yield return new WaitForSecondsRealtime(0.3f);

            // keyboard focus
            br.Focus();
            yield return CheckTipFor("focus (bottom-right corner)", br, LongText);
            using (var k = KeyDownEvent.GetPooled('\0', KeyCode.Escape, EventModifiers.None)) { k.target = br; br.SendEvent(k); }
            yield return new WaitForSecondsRealtime(0.3f);
            Check("Esc hides the tooltip", !TipShown);
            br.Blur();
            yield return new WaitForSecondsRealtime(0.2f);

            // Tab from a neighbour button lands on the ⓘ and shows it
            b0.Focus();
            yield return new WaitForSecondsRealtime(0.2f);
            Check("a plain button that gets the focus by code shows no tooltip", !TipShown);
            using (var mv = NavigationMoveEvent.GetPooled(NavigationMoveEvent.Direction.Right)) { mv.target = b0; b0.SendEvent(mv); } // what the pad stick sends
            yield return new WaitForSecondsRealtime(0.3f);
            bool onMid = root.panel.focusController.focusedElement == mid;
            Check("a move to the right (arrows, pad) puts the focus on the ⓘ", onMid);
            if (onMid) yield return CheckTipFor("navigation focus", mid, LongText);
            yield return Shot("ux_info_foco_tab", 0.05f);
            mid.Blur();

            // the pad's submit (and a click) on the ⓘ shows it at once
            mid.Blur();
            yield return new WaitForSecondsRealtime(0.2f);
            Click(mid);
            yield return new WaitForSecondsRealtime(0.1f);
            Check("a submit on the ⓘ (Enter / the pad's A) shows the text", TipShown && TipText.text == LongText);
            mid.Blur();
            yield return new WaitForSecondsRealtime(0.2f);

            // the fold
            const string id = "ux_probe_fold";
            PlayerPrefs.DeleteKey(DesktopFold.PrefKey(id));
            var fold = new DesktopFold(id, "UxFold");
            fold.SetTitle(AppLanguageManager.IsSpanish ? "Avanzado" : "Advanced");
            Button inner = new Button { name = "UxFoldInner", text = "Dentro" };
            fold.Body.Add(inner);
            row.Add(fold);
            yield return new WaitForSecondsRealtime(0.3f);
            Check("the fold is closed by default (body hidden, still in the tree)", !fold.IsOpen && fold.Body.resolvedStyle.display == DisplayStyle.None && fold.Contains(inner));
            Check("the fold header takes the keyboard focus", fold.Header.focusable);
            Click(fold.Header);
            yield return new WaitForSecondsRealtime(0.3f);
            Check("a submit on the header (Enter / the pad's A) opens the fold", fold.IsOpen && fold.Body.resolvedStyle.display != DisplayStyle.None);
            Check("the choice is stored in PlayerPrefs Desk_Fold_<id> = 1", PlayerPrefs.GetInt(DesktopFold.PrefKey(id), 0) == 1);
            var again = new DesktopFold(id);
            Check("a new fold with the same id starts open (remembers the choice)", again.IsOpen);
            inner.Focus();
            bool closed = DesktopFold.TryCloseFor(inner);
            yield return new WaitForSecondsRealtime(0.2f);
            Check("the pad's B inside the open fold closes it and puts the focus on the header", closed && !fold.IsOpen && root.panel.focusController.focusedElement == fold.Header);
            Check("the closed choice is stored (0)", PlayerPrefs.GetInt(DesktopFold.PrefKey(id), 0) == 0);
            fold.SetOpen(true);
            Check("SetOpen(true) without persist (what the probes use) leaves no choice behind", PlayerPrefs.GetInt(DesktopFold.PrefKey(id), 0) == 0);
            Check("the pad's B on the header itself is not consumed by the fold", !DesktopFold.TryCloseFor(fold.Header));
            PlayerPrefs.DeleteKey(DesktopFold.PrefKey(id));

            host.RemoveFromHierarchy();
            yield return new WaitForSecondsRealtime(0.2f);
        }

        private static int VisibleLabels(VisualElement root, string cls)
        {
            int n = 0;
            foreach (Label l in root.Query<Label>(className: cls).ToList()) if (!string.IsNullOrEmpty(l.text) && l.resolvedStyle.display != DisplayStyle.None) n++;
            return n;
        }

        /// <summary>Opens a popover; checks its ⓘ (hover shows the full text inside the window) and, when it has one, that its «Avanzado» fold is closed and opens.</summary>
        private IEnumerator FoldPopover(string pop, string foldName, string insideName, string infoName, string shot)
        {
            Tel.TogglePopover(pop);
            yield return new WaitForSecondsRealtime(0.6f);
            Button info = B(infoName);
            Check(pop + ": the ⓘ " + infoName + " is shown and carries the long note(s) (" + (info != null ? info.tooltip.Length : 0) + " chars)", info != null && Shown(info) && info.tooltip.Length > 30);
            if (foldName != null)
            {
                DesktopFold fold = Root.Q<DesktopFold>(foldName);
                Check(pop + ": the «Avanzado» fold is closed and still holds " + insideName, fold != null && !fold.IsOpen && fold.Contains(B(insideName)) && fold.Body.resolvedStyle.display == DisplayStyle.None);
                yield return Shot(shot + "_cerrado", 0.2f);
                Click(fold.Header);
                yield return new WaitForSecondsRealtime(0.4f);
                Check(pop + ": opening the fold shows " + insideName, fold.IsOpen && Shown(B(insideName)));
                yield return Shot(shot + "_abierto", 0.2f);
                fold.SetOpen(false);
                PlayerPrefs.DeleteKey(DesktopFold.PrefKey(foldName == "DkFaultFold" ? "faults" : "mission"));
            }
            else
            {
                yield return Shot(shot, 0.2f);
            }
            if (info != null && Shown(info))
            {
                SendOver(info, true);
                yield return CheckTipFor(pop + " ⓘ hover", info, info.tooltip);
                SendOver(info, false);
            }
            Tel.CloseAllPopovers();
            yield return new WaitForSecondsRealtime(0.3f);
        }

        private IEnumerator HonestyAndAdvancedChecks()
        {
            Log("── paso 4/5: cadenas de honestidad en el arbol ──");
            // every source popover text is written when the popover opens
            Tel.OpenMode();
            Tel.TogglePopover("source");
            yield return new WaitForSecondsRealtime(0.6f);
            foreach (string f in DesktopHonesty.Required(Lang))
            {
                Check("honesty «" + f + "» is in the tree (text or ⓘ)", DesktopInfo.TreeHas(Root, f));
            }
            foreach (string n in new[] { "DkInfoSrcInspector", "DkInfoSrcReplay", "DkInfoSrcSim", "DkInfoSrcSitl", "DkInfoSitlNote" })
            {
                Button i = B(n);
                Check("Fuente: " + n + " exists, is shown and carries a long text (" + (i != null ? i.tooltip.Length : 0) + " chars)", i != null && Shown(i) && i.tooltip.Length > 30);
            }
            Check("Fuente: the short origin badges stay visible (Sombra digital, Simulador de vuelo, Gemelo digital SITL)", VisibleLabels(Root.Q<VisualElement>("DkPopSource"), "dk-src-badge") >= 3);
            yield return Shot("ux_fuente_popover", 0.2f);
            Button simInfo = B("DkInfoSrcSim");
            SendOver(simInfo, true);
            yield return CheckTipFor("Fuente > Simulador ⓘ", simInfo, simInfo.tooltip);
            yield return Shot("ux_fuente_info_simulador", 0.05f);
            SendOver(simInfo, false);
            Tel.CloseAllPopovers();
            yield return new WaitForSecondsRealtime(0.3f);

            // folds and ⓘ of the other screens (simulator running: every control exists)
            Tel.StartSimulator();
            yield return new WaitForSecondsRealtime(2.5f);
            DesktopFold cmd = Root.Q<DesktopFold>("DkCmdFold");
            Check("Comandos: the «Avanzado» fold exists and is closed", cmd != null && !cmd.IsOpen && Shown(cmd));
            Check("Comandos: Mantener (H) and Volver (R) are visible, never in the fold", Shown(B("DkCmdHold")) && Shown(B("DkCmdRtl")) && !cmd.Contains(B("DkCmdHold")) && !cmd.Contains(B("DkCmdRtl")));
            Check("Comandos: the camera and the game mode are inside the closed fold", cmd.Contains(B("DkCamMode")) && cmd.Contains(B("DkGameMode")) && cmd.Body.resolvedStyle.display == DisplayStyle.None);
            yield return Shot("ux_comandos_avanzado_cerrado", 0.2f);
            Click(cmd.Header);
            yield return new WaitForSecondsRealtime(0.4f);
            Check("Comandos: a submit on the header opens it and shows the camera buttons", cmd.IsOpen && Shown(B("DkCamMode")) && Shown(B("DkGameMode")));
            yield return Shot("ux_comandos_avanzado_abierto", 0.2f);
            Click(cmd.Header);
            yield return new WaitForSecondsRealtime(0.2f);
            Check("Comandos: a second submit closes it", !cmd.IsOpen);
            PlayerPrefs.DeleteKey(DesktopFold.PrefKey("commands"));

            yield return FoldPopover("faults", "DkFaultFold", "DkFaultExplain", "DkInfoFaults", "ux_fallas");
            yield return FoldPopover("mission", "DkMsFold", "DkMsExport", "DkInfoMission", "ux_mision");
            yield return FoldPopover("fence", null, null, "DkInfoFence", "ux_geocerca");
            yield return FoldPopover("wind", null, null, "DkInfoWind", "ux_viento");
            yield return FoldPopover("batteryreact", null, null, "DkInfoBattery", "ux_bateria");

            Tel.ShowFlightTab(true);
            yield return new WaitForSecondsRealtime(1.0f);
            DesktopFold fl = Root.Q<DesktopFold>("DkFlFold");
            Check("Vuelo: the «Avanzado» fold exists and is closed, with the tags of origin still in the tree", fl != null && !fl.IsOpen && fl.Body.Query<Label>(className: "dk-tag").ToList().Count > 0);
            Check("Vuelo: the ⓘ of the origin badge carries the long detail and the Source line", B("DkInfoFlBadge") != null && B("DkInfoFlBadge").tooltip.Length > 30);
            yield return Shot("ux_vuelo_avanzado_cerrado", 0.2f);
            Click(fl.Header);
            yield return new WaitForSecondsRealtime(0.5f);
            Check("Vuelo: opening the fold shows the motors, the battery health and the controller rows", fl.IsOpen && fl.Body.resolvedStyle.display != DisplayStyle.None);
            yield return Shot("ux_vuelo_avanzado_abierto", 0.2f);
            PlayerPrefs.DeleteKey(DesktopFold.PrefKey("flight"));
            Tel.ShowFlightTab(false);
            DesktopTelemetry.Stop();
            yield return new WaitForSecondsRealtime(0.4f);
        }
    }
}
#endif
