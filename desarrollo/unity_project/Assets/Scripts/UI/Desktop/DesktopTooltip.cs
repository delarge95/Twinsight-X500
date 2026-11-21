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
            { "ToolFrameBtn", "F" },
            { "AnalyzeExplodeBtn", "E" },
            { "ResetViewBtn", "R|Home" },
            { "SheetCloseBtn", "Esc" },
            { "CrossSectionBackBtn", "Esc" },
            { "FilterBackBtn", "Esc" },
            { "OnboardCloseBtn", "Esc" },
            { "HeroInfoBtn", "F1" },
            { "DkMode1", "1" }, { "DkMode2", "2" }, { "DkMode3", "3" }, { "DkMode4", "4" },
            { "DkSrcGear", "L" }, { "DkTelAlerts", "Shift+A" },
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
            root.RegisterCallback<FocusInEvent>(OnFocusIn, TrickleDown.TrickleDown);
            root.RegisterCallback<FocusOutEvent>(OnFocusOut, TrickleDown.TrickleDown);
            root.RegisterCallback<ClickEvent>(OnClick, TrickleDown.TrickleDown);
            root.RegisterCallback<NavigationSubmitEvent>(OnSubmit, TrickleDown.TrickleDown);
            root.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
        }

        public void Dispose()
        {
            _root.UnregisterCallback<PointerOverEvent>(OnOver, TrickleDown.TrickleDown);
            _root.UnregisterCallback<PointerOutEvent>(OnOut, TrickleDown.TrickleDown);
            _root.UnregisterCallback<PointerDownEvent>(OnDown, TrickleDown.TrickleDown);
            _root.UnregisterCallback<WheelEvent>(OnWheel, TrickleDown.TrickleDown);
            _root.UnregisterCallback<FocusInEvent>(OnFocusIn, TrickleDown.TrickleDown);
            _root.UnregisterCallback<FocusOutEvent>(OnFocusOut, TrickleDown.TrickleDown);
            _root.UnregisterCallback<ClickEvent>(OnClick, TrickleDown.TrickleDown);
            _root.UnregisterCallback<NavigationSubmitEvent>(OnSubmit, TrickleDown.TrickleDown);
            _root.UnregisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
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

        // Keyboard / pad focus (PLAN_PC 39): an ⓘ shows its text as soon as it gets the focus, whatever moved it there; any other element with a tooltip shows
        // it only when the focus came from navigation (Tab, arrows, the pad's stick in the Menus mode), never from a mouse press or a programmatic Focus().
        private void OnFocusIn(FocusInEvent evt)
        {
            if (!(evt.target is VisualElement target)) return;
            VisualElement host = FindTooltipHost(target);
            if (host == null) { Hide(); return; }
            if (!DesktopInfo.IsInfo(host) && evt.direction == FocusChangeDirection.unspecified) return;
            Hide();
            _hovered = host;
            ShowFor(host);
        }

        private void OnFocusOut(FocusOutEvent evt)
        {
            if (_hovered != null && evt.target is VisualElement t && (t == _hovered || _hovered.Contains(t)))
            {
                Hide();
                _hovered = null;
            }
        }

        // A click or a submit (Enter, Space, the pad's A) on an ⓘ shows its text at once.
        private void OnClick(ClickEvent evt)
        {
            if (evt.target is VisualElement target && FindTooltipHost(target) is VisualElement host && DesktopInfo.IsInfo(host)) ShowFor(host);
        }

        // Enter, Space and the pad A do not make a ClickEvent: a submit on an info button shows the text too.
        private void OnSubmit(NavigationSubmitEvent evt)
        {
            if (evt.target is VisualElement target && FindTooltipHost(target) is VisualElement host && DesktopInfo.IsInfo(host)) ShowFor(host);
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.Escape && _tip.resolvedStyle.display != DisplayStyle.None) Hide();
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
            _tip.schedule.Execute(() => Place(host, 0)).StartingIn(0);
        }

        /// <summary>The window in panel units: the logical size the UI scaler computed (the root element reports no layout of its own).</summary>
        internal static Rect WindowRect(VisualElement root)
        {
            Vector2 s = DesktopUIController.Instance != null ? DesktopUIController.Instance.LogicalSize : Vector2.zero;
            if (s.x > 1f && s.y > 1f) return new Rect(0f, 0f, s.x, s.y);
            Rect r = root.worldBound;
            return r.width > 1f ? r : new Rect(0f, 0f, Screen.width, Screen.height);
        }

        private void Place(VisualElement host, int attempt)
        {
            if (host == null || host.panel == null)
            {
                return;
            }

            // a text that wraps has no size until the layout pass after display:flex: wait for it (up to ~100 ms) instead of placing a 160x40 guess
            if ((float.IsNaN(_tip.resolvedStyle.width) || _tip.resolvedStyle.width <= 0f) && attempt < 6)
            {
                _tip.schedule.Execute(() => Place(host, attempt + 1)).StartingIn(16);
                return;
            }

            Rect hostRect = host.worldBound;
            // the root element may have no layout of its own yet (width 0 pinned every tooltip to the top-left corner): the panel's visual tree is the window
            Rect panelRect = WindowRect(_root);
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
