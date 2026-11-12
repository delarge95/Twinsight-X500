#if UNITY_STANDALONE
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using WebGL.UI.Panels;

namespace WebGL.UI.Desktop
{
    /// <summary>
    /// PC only (U3). UI Toolkit does not draw the <c>tooltip=</c> strings of a UXML at runtime, so this
    /// shows a small floating label of our own (same glass pill language as the rest of the UI) after the
    /// pointer rests 0.4 s over any element that has a <c>tooltip</c>. The label is followed by the key
    /// cap(s) of its shortcut, e.g. "Toggle Hotspots [H]".
    /// </summary>
    internal sealed class DesktopTooltip
    {
        private const long ShowDelayMs = 400;

        /// <summary>Shortcut caps by element name. A '|' separates alternatives (shown as "R / Home").</summary>
        private static readonly Dictionary<string, string> KeysByElement = new Dictionary<string, string>
        {
            { "ModeToolsBtn", "1" },
            { "ModeAnalyzeBtn", "2" },
            { "ModeStudioBtn", "3" },
            { "ToolHotspotBtn", "H" },
            { "ToolIsolateBtn", "I" },
            { "AnalyzeExplodeBtn", "E" },
            { "ResetViewBtn", "R|Home" },
            { "SheetCloseBtn", "Esc" },
            { "CrossSectionBackBtn", "Esc" },
            { "FilterBackBtn", "Esc" },
            { "OnboardCloseBtn", "Esc" },
            { "HeroInfoBtn", "F1" },
            { "DkMode1", "1" }, { "DkMode2", "2" }, { "DkMode3", "3" }, { "DkMode4", "4" },
            { "DkTelSource", "L" }, { "DkTelAlerts", "Shift+A" },
            { "DkPins", "H" }, { "DkChipFilter", "G" }, { "DkChipExplode", "E" }, { "DkChipCut", "C" }, { "DkIso", "I" }, { "DkReset", "R|Home" },
            { "DkTree", "T" }, { "DkRailTree", "T" }, { "DkRailSearch", "Ctrl+F" }, { "DkInspector", "P" }, { "DkRailInspector", "P" },
            { "DkHelp", "?" }, { "DkStatusHelp", "?" }, { "DkFocus", "F" }, { "DkIsolate", "I" },
            { "AnalyzeCrossSectionBtn", "C" }, { "AnalyzeFilterBtn", "G" }
        };

        private readonly VisualElement _root;
        private readonly VisualElement _tip;
        private readonly Label _text;
        private readonly VisualElement _keys;

        private VisualElement _hovered;
        private IVisualElementScheduledItem _pending;

        public DesktopTooltip(VisualElement root)
        {
            _root = root;

            _tip = new VisualElement { name = "DesktopTooltip", pickingMode = PickingMode.Ignore };
            _tip.AddToClassList("desktop-tooltip");
            _text = new Label { pickingMode = PickingMode.Ignore };
            _text.AddToClassList("desktop-tooltip-text");
            _keys = new VisualElement { pickingMode = PickingMode.Ignore };
            _keys.style.flexDirection = FlexDirection.Row;
            _keys.style.alignItems = Align.Center;
            _tip.Add(_text);
            _tip.Add(_keys);
            _tip.style.display = DisplayStyle.None;
            root.Add(_tip);

            root.RegisterCallback<PointerOverEvent>(OnOver, TrickleDown.TrickleDown);
            root.RegisterCallback<PointerOutEvent>(OnOut, TrickleDown.TrickleDown);
            root.RegisterCallback<PointerDownEvent>(OnDown, TrickleDown.TrickleDown);
            root.RegisterCallback<WheelEvent>(OnWheel, TrickleDown.TrickleDown);
        }

        public void Dispose()
        {
            _root.UnregisterCallback<PointerOverEvent>(OnOver, TrickleDown.TrickleDown);
            _root.UnregisterCallback<PointerOutEvent>(OnOut, TrickleDown.TrickleDown);
            _root.UnregisterCallback<PointerDownEvent>(OnDown, TrickleDown.TrickleDown);
            _root.UnregisterCallback<WheelEvent>(OnWheel, TrickleDown.TrickleDown);
            _pending?.Pause();
            _tip.RemoveFromHierarchy();
        }

        /// <summary>Shortcut label for an element name (null if none). Used by tests and the help overlay.</summary>
        public static string KeysFor(string elementName)
        {
            return elementName != null && KeysByElement.TryGetValue(elementName, out string keys) ? keys : null;
        }

        private static VisualElement FindTooltipHost(VisualElement start)
        {
            for (VisualElement e = start; e != null; e = e.parent)
            {
                if (!string.IsNullOrEmpty(e.tooltip))
                {
                    return e;
                }
            }

            return null;
        }

        private void OnOver(PointerOverEvent evt)
        {
            if (!(evt.target is VisualElement target))
            {
                return;
            }

            VisualElement host = FindTooltipHost(target);
            if (host == _hovered)
            {
                return;
            }

            Hide();
            _hovered = host;
            if (host == null)
            {
                return;
            }

            _pending = _root.schedule.Execute(() => ShowFor(_hovered)).StartingIn(ShowDelayMs);
        }

        private void OnOut(PointerOutEvent evt)
        {
            Hide();
            _hovered = null;
        }

        private void OnDown(PointerDownEvent evt)
        {
            Hide();
        }

        private void OnWheel(WheelEvent evt)
        {
            Hide();
        }

        public void Hide()
        {
            _pending?.Pause();
            _pending = null;
            _tip.RemoveFromClassList("desktop-tooltip--visible");
            _tip.style.display = DisplayStyle.None;
        }

        /// <summary>Shows the tooltip immediately for an element (also used by the capture demo).</summary>
        public void ShowFor(VisualElement host)
        {
            if (host == null || string.IsNullOrEmpty(host.tooltip) || host.panel == null)
            {
                return;
            }

            _text.text = AppLanguageManager.TranslateStatic(host.tooltip);

            _keys.Clear();
            string keys = KeysFor(host.name);
            if (!string.IsNullOrEmpty(keys))
            {
                string[] parts = keys.Split('|');
                for (int i = 0; i < parts.Length; i++)
                {
                    if (i > 0)
                    {
                        var sep = new Label("/") { pickingMode = PickingMode.Ignore };
                        sep.AddToClassList("desktop-keycap-sep");
                        _keys.Add(sep);
                    }

                    var cap = new Label(parts[i]) { pickingMode = PickingMode.Ignore };
                    cap.AddToClassList("desktop-keycap");
                    if (i > 0) cap.style.marginLeft = 6;
                    _keys.Add(cap);
                }

                _keys.style.display = DisplayStyle.Flex;
            }
            else
            {
                _keys.style.display = DisplayStyle.None;
            }

            _tip.style.display = DisplayStyle.Flex;
            _tip.style.left = 0;
            _tip.style.top = 0;
            _tip.style.visibility = Visibility.Hidden;

            // Position after layout so the tooltip's own size is known.
            _tip.schedule.Execute(() => Place(host)).StartingIn(0);
        }

        private void Place(VisualElement host)
        {
            if (host == null || host.panel == null)
            {
                return;
            }

            Rect hostRect = host.worldBound;
            Rect panelRect = _root.worldBound;
            float w = _tip.resolvedStyle.width;
            float h = _tip.resolvedStyle.height;
            if (float.IsNaN(w) || w <= 0f)
            {
                w = 160f;
                h = 40f;
            }

            const float gap = 10f;
            float x = hostRect.center.x - w * 0.5f;
            bool showAbove = hostRect.center.y > panelRect.height * 0.5f;
            float y = showAbove ? hostRect.yMin - h - gap : hostRect.yMax + gap;

            x = Mathf.Clamp(x, 8f, Mathf.Max(8f, panelRect.width - w - 8f));
            y = Mathf.Clamp(y, 8f, Mathf.Max(8f, panelRect.height - h - 8f));

            _tip.style.left = x;
            _tip.style.top = y;
            _tip.style.visibility = Visibility.Visible;
            _tip.AddToClassList("desktop-tooltip--visible");
        }
    }
}
#endif
