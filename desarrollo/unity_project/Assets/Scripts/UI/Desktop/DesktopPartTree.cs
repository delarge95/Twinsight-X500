#if UNITY_STANDALONE
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;
using WebGL.Core.Events;
using WebGL.Core.Managers;
using WebGL.UI.Panels;

namespace WebGL.UI.Desktop
{
    /// <summary>
    /// PC only (phase 3B, U8). Left docked panel "PIEZAS": the catalog of parts as a tree grouped by assembly
    /// (structure / propulsion / avionics / fasteners, then mother part -> sub-pieces) with an accent-insensitive
    /// search box. Clicking a row selects the part through the same path as the deep link
    /// (<see cref="DeepLinkSelector.RequestSelect"/>), the current selection is highlighted (and its branch opened).
    /// Opens with Ctrl+F or "/" (focuses the search), closes with Esc or the X. Symmetric to the right details panel.
    /// </summary>
    internal sealed class DesktopPartTree
    {
        public const float Width = 280f;   // phase 4 (mockup)
        public const float Margin = 0f;

        private sealed class Node
        {
            public InfoPanelCatalogEntry Entry;      // null for category rows
            public string CategoryEs, CategoryEn;
            public Node Parent;
            public readonly List<Node> Children = new List<Node>();
            public bool Expanded;
            public Button Row;
            public Label Chevron;
            public Label Title;
            public string SearchKey = string.Empty;
            public int Depth;
            public string Id => Entry != null ? Entry.entryId : "cat:" + CategoryEn;
            /// <summary>Catalog group with no scene object of its own (e.g. "Arm Assembly Quadrant"): the row only folds.</summary>
            public bool IsGroup;
        }

        private static readonly string[][] Buckets =
        {
            new[] { "Estructura", "Structure", "x500v2_top_plate", "x500v2_bottom_plate", "x500v2_platform_board", "x500v2_landing_gear", "x500v2_rails_battery", "x500v2_arm" },
            new[] { "Propulsión", "Propulsion", "x500v2_motor", "x500v2_prop", "x500v2_esc" },
            new[] { "Aviónica y energía", "Avionics and power", "x500v2_pixhawk6c", "x500v2_gps_m10", "x500v2_power_module", "x500v2_pdb", "x500v2_battery", "x500v2_telemetry_radio", "x500v2_rc_receiver" },
        };

        private readonly VisualElement _root;
        private readonly VisualElement _panel;
        private readonly Label _title;
        private readonly TextField _search;
        private readonly Label _placeholder;
        private readonly Label _count;
        private readonly ScrollView _scroll;
        private readonly VisualElement _list;
        private readonly Button _pill;
        private readonly Label _pillLabel;

        private readonly List<Node> _top = new List<Node>();
        private readonly Dictionary<string, Node> _byId = new Dictionary<string, Node>();
        private readonly List<Node> _visible = new List<Node>();
        private Node _selected;
        private int _cursor = -1;
        private string _filter = string.Empty;
        private bool _open;
        private bool _built;
        private PartSelectedEvent _lastEvt;
        private bool _hasLastEvt;
        private void CacheSelection(PartSelectedEvent evt) { _lastEvt = evt; _hasLastEvt = true; if (_built) OnPartSelected(evt); }

        public bool IsOpen => _open;
        public float OccupiedWidth => _open ? Width + 2f * Margin : 0f;

        public DesktopPartTree(VisualElement root)
        {
            _root = root;

            _panel = new VisualElement { name = "DesktopPartTree", pickingMode = PickingMode.Position };
            _panel.AddToClassList("desktop-tree");
            _panel.AddToClassList("desktop-tree--hidden");

            var header = new VisualElement();
            header.AddToClassList("desktop-tree-header");
            _title = new Label("PIEZAS") { pickingMode = PickingMode.Ignore };
            _title.AddToClassList("desktop-tree-title");
            var close = new Button(() => SetOpen(false)) { text = "✕" };
            close.AddToClassList("desktop-tree-close");
            header.Add(_title);
            header.Add(close);
            _panel.Add(header);

            var searchBox = new VisualElement();
            searchBox.AddToClassList("desktop-tree-searchbox");
            _search = new TextField { name = "DesktopTreeSearch" };
            _search.AddToClassList("desktop-tree-search");
            _placeholder = new Label { pickingMode = PickingMode.Ignore };
            _placeholder.AddToClassList("desktop-tree-placeholder");
            searchBox.Add(_search);
            searchBox.Add(_placeholder);
            _panel.Add(searchBox);

            _count = new Label { pickingMode = PickingMode.Ignore };
            _count.AddToClassList("desktop-tree-count");
            _panel.Add(_count);

            _scroll = new ScrollView(ScrollViewMode.Vertical);
            _scroll.AddToClassList("desktop-tree-scroll");
            _list = _scroll.contentContainer;
            _panel.Add(_scroll);
            _root.Add(_panel);

            _search.RegisterValueChangedCallback(evt =>
            {
                _filter = Normalize(evt.newValue);
                _cursor = -1;
                Refresh();
            });
            _search.RegisterCallback<KeyDownEvent>(OnSearchKey, TrickleDown.TrickleDown);
            _search.RegisterCallback<FocusInEvent>(_ => UpdatePlaceholder());
            _search.RegisterCallback<FocusOutEvent>(_ => UpdatePlaceholder());

            // Pill "PIEZAS" next to the shortcuts pill (bottom-left).
            _pill = new Button { name = "DesktopTreePill" };
            _pill.AddToClassList("desktop-hint");
            _pill.AddToClassList("desktop-treepill");
            _pill.Add(DesktopHelpOverlay.MakeCap("/", true));
            _pillLabel = new Label { pickingMode = PickingMode.Ignore };
            _pillLabel.AddToClassList("desktop-hint-text");
            _pill.Add(_pillLabel);
            _pill.clicked += () => SetOpen(!_open);
            _pill.RegisterCallback<PointerDownEvent>(evt => { if (evt.button == 0) InputManager.InputBlocked = true; evt.StopPropagation(); });
            _pill.RegisterCallback<PointerUpEvent>(evt => { if (evt.button == 0) InputManager.InputBlocked = false; evt.StopPropagation(); });
            _root.Add(_pill);

            ApplyLanguage();
            AppLanguageManager.LanguageChanged += OnLanguageChanged;
            EventBus.Subscribe<PartSelectedEvent>(CacheSelection);
        }

        public void Dispose()
        {
            AppLanguageManager.LanguageChanged -= OnLanguageChanged;
            EventBus.Unsubscribe<PartSelectedEvent>(CacheSelection);
            _panel.RemoveFromHierarchy();
            _pill.RemoveFromHierarchy();
        }

        // ───────────────────────── public ─────────────────────────

        public void SetPillHidden(bool hidden) => _pill.EnableInClassList("desktop-hint--hidden", hidden);

        public void Toggle(bool focusSearch)
        {
            SetOpen(!_open);
            if (_open && focusSearch) FocusSearch();
        }

        public void SetOpen(bool open)
        {
            if (open == _open) return;
            _open = open;
            if (open)
            {
                if (SelectionManager.Instance != null && SelectionManager.Instance.CurrentSelection == null)
                {
                    _hasLastEvt = false;
                    _selected?.Row?.RemoveFromClassList("desktop-tree-row--selected");
                    _selected = null;
                }

                EnsureBuilt();
                SyncSelectionHighlight(true);
            }
            else
            {
                _search.Blur();
            }

            _panel.EnableInClassList("desktop-tree--hidden", !open);
            _root.EnableInClassList("layout--tree-open", open);
            _pill.EnableInClassList("desktop-treepill--active", open);
        }

        public void Open(bool focusSearch)
        {
            SetOpen(true);
            if (focusSearch) FocusSearch();
        }

        public void FocusSearch()
        {
            // Delayed so the "/" that opened the panel is not typed into the field.
            _search.schedule.Execute(() =>
            {
                _search.Focus();
                _search.SelectAll();
            }).ExecuteLater(60);
        }

        /// <summary>Esc: first clears the search, then closes. Returns true when it consumed the key.</summary>
        public bool HandleBack()
        {
            if (!_open) return false;
            if (_filter.Length > 0 || !string.IsNullOrEmpty(_search.value))
            {
                _search.SetValueWithoutNotify(string.Empty);
                _filter = string.Empty;
                _cursor = -1;
                _search.Blur();
                Refresh();
                return true;
            }

            SetOpen(false);
            return true;
        }

        public void Tick()
        {
            if (!_open) return;
            SelectionManager sm = SelectionManager.Instance;
            if (sm != null && sm.CurrentSelection == null && _selected != null)
            {
                _selected.Row?.RemoveFromClassList("desktop-tree-row--selected");
                _selected = null;
            }
        }

        // Test hook (capture demo): types a query as if the user did.
        public void SetQuery(string text)
        {
            EnsureBuilt();
            _search.value = text;
        }

        public int VisibleCount => _visible.Count;

        public bool SelectByQueryFirst()
        {
            for (int i = 0; i < _visible.Count; i++)
            {
                if (_visible[i].Entry != null && !_visible[i].IsGroup)
                {
                    Activate(_visible[i]);
                    return true;
                }
            }

            return false;
        }

        // ───────────────────────── build ─────────────────────────

        private void EnsureBuilt()
        {
            if (_built) return;
            _built = true;

            TextAsset asset = Resources.Load<TextAsset>("x500v2_info_panel_bilingual_catalog");
            InfoPanelCatalogRoot catalog = asset != null ? JsonUtility.FromJson<InfoPanelCatalogRoot>(asset.text) : null;
            if (catalog == null || catalog.entries == null)
            {
                Debug.LogWarning("[DesktopUI] part tree: catalog not found.");
                return;
            }

            var entries = new Dictionary<string, InfoPanelCatalogEntry>(StringComparer.OrdinalIgnoreCase);
            foreach (InfoPanelCatalogEntry e in catalog.entries)
            {
                if (e != null && !string.IsNullOrEmpty(e.entryId)) entries[e.entryId] = e;
            }

            bool IsTreeEntry(InfoPanelCatalogEntry e) =>
                !string.Equals(e.entryType, "hotspot_group", StringComparison.OrdinalIgnoreCase);

            // Primary parent: the parent with most children (the generic group), then the first listed.
            var primary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var reverse = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (InfoPanelCatalogEntry e in entries.Values)
            {
                if (!IsTreeEntry(e) || e.childIds == null) continue;
                foreach (string c in e.childIds)
                {
                    if (!reverse.TryGetValue(c, out List<string> list)) reverse[c] = list = new List<string>();
                    list.Add(e.entryId);
                }
            }

            foreach (InfoPanelCatalogEntry e in entries.Values)
            {
                if (!IsTreeEntry(e) || string.Equals(e.entryType, "fastener_family", StringComparison.OrdinalIgnoreCase)) continue;

                var candidates = new List<string>();
                if (e.parentIds != null) candidates.AddRange(e.parentIds);
                if (candidates.Count == 0 && reverse.TryGetValue(e.entryId, out List<string> rev)) candidates.AddRange(rev);

                string best = null;
                int bestChildren = -1;
                foreach (string c in candidates)
                {
                    if (!entries.TryGetValue(c, out InfoPanelCatalogEntry p) || !IsTreeEntry(p) || p == e) continue;
                    int n = p.childIds != null ? p.childIds.Length : 0;
                    if (n > bestChildren) { best = p.entryId; bestChildren = n; }
                }

                if (best != null) primary[e.entryId] = best;
            }

            _byId.Clear();
            _top.Clear();
            foreach (InfoPanelCatalogEntry e in entries.Values)
            {
                if (!IsTreeEntry(e)) continue;
                _byId[e.entryId] = new Node { Entry = e };
            }

            var topLevel = new List<Node>();
            foreach (Node n in _byId.Values)
            {
                if (primary.TryGetValue(n.Entry.entryId, out string pid) && _byId.TryGetValue(pid, out Node parent))
                {
                    n.Parent = parent;
                    parent.Children.Add(n);
                }
                else
                {
                    topLevel.Add(n);
                }
            }

            // Category buckets (hand-made: the catalog has no assembly field).
            var categories = new List<Node>();
            var placed = new HashSet<Node>();
            foreach (string[] b in Buckets)
            {
                var cat = new Node { CategoryEs = b[0], CategoryEn = b[1], Expanded = true };
                for (int i = 2; i < b.Length; i++)
                {
                    if (_byId.TryGetValue(b[i], out Node n) && n.Parent == null)
                    {
                        n.Parent = cat; cat.Children.Add(n); placed.Add(n);
                    }
                }
                categories.Add(cat);
            }

            var fasteners = new Node { CategoryEs = "Fijaciones", CategoryEn = "Fasteners" };
            var other = new Node { CategoryEs = "Otras piezas", CategoryEn = "Other parts" };
            foreach (Node n in topLevel)
            {
                if (placed.Contains(n)) continue;
                if (n.Entry.entryId == "x500v2_misc_group") continue; // internal runtime group
                Node target = (n.Entry.entryType == "fastener_family" || n.Entry.entryId.Contains("zslm_m3")) ? fasteners : other;
                n.Parent = target; target.Children.Add(n);
            }

            categories.Add(fasteners);
            if (other.Children.Count > 0) categories.Add(other);

            foreach (Node c in categories)
            {
                if (c.Children.Count == 0) continue;
                SortChildren(c);
                _top.Add(c);
            }

            foreach (Node n in _byId.Values)
            {
                if (n.Entry.entryType != "mother_part" || n.Entry.childIds == null) continue;
                foreach (string cid in n.Entry.childIds)
                {
                    if (entries.TryGetValue(cid, out InfoPanelCatalogEntry ce) && ce.entryType == "mother_part") { n.IsGroup = true; break; }
                }
            }

            foreach (Node c in _top) Index(c, 0);
            BuildRows();
            Refresh();

            if (_hasLastEvt) OnPartSelected(_lastEvt);
        }

        private static void SortChildren(Node n)
        {
            n.Children.Sort((a, b) => string.Compare(Normalize(Title(a)), Normalize(Title(b)), StringComparison.Ordinal));
            foreach (Node c in n.Children) SortChildren(c);
        }

        private void Index(Node n, int depth)
        {
            n.Depth = depth;
            var sb = new StringBuilder();
            if (n.Entry != null)
            {
                sb.Append(Normalize(n.Entry.es?.title)).Append(' ').Append(Normalize(n.Entry.en?.title)).Append(' ').Append(Normalize(n.Entry.entryId));
                if (n.Entry.aliases != null) foreach (string a in n.Entry.aliases) sb.Append(' ').Append(Normalize(a));
            }
            else
            {
                sb.Append(Normalize(n.CategoryEs)).Append(' ').Append(Normalize(n.CategoryEn));
            }

            n.SearchKey = sb.ToString();
            foreach (Node c in n.Children) Index(c, depth + 1);
        }

        private static string Title(Node n)
        {
            bool es = AppLanguageManager.IsSpanish;
            if (n.Entry == null) return es ? n.CategoryEs : n.CategoryEn;
            string t = es ? n.Entry.es?.title : n.Entry.en?.title;
            if (string.IsNullOrWhiteSpace(t)) t = es ? n.Entry.en?.title : n.Entry.es?.title;
            return string.IsNullOrWhiteSpace(t) ? n.Entry.entryId : t;
        }

        private void BuildRows()
        {
            _list.Clear();
            foreach (Node c in _top) BuildRowsRecursive(c);
        }

        private void BuildRowsRecursive(Node n)
        {
            var row = new Button();
            row.AddToClassList("desktop-tree-row");
            row.EnableInClassList("desktop-tree-row--category", n.Entry == null);
            row.EnableInClassList("desktop-tree-row--group", n.IsGroup);
            row.style.paddingLeft = 10 + n.Depth * 16;

            var chev = new Label(n.Children.Count > 0 ? "▸" : string.Empty) { pickingMode = PickingMode.Ignore };
            chev.AddToClassList("desktop-tree-chevron");
            var title = new Label { pickingMode = PickingMode.Ignore };
            title.AddToClassList("desktop-tree-rowtitle");
            row.Add(chev);
            row.Add(title);
            row.clicked += () => Activate(n);
            n.Row = row; n.Chevron = chev; n.Title = title;
            _list.Add(row);
            foreach (Node c in n.Children) BuildRowsRecursive(c);
        }

        // ───────────────────────── filter / render ─────────────────────────

        private bool Matches(Node n)
        {
            if (_filter.Length == 0) return true;
            foreach (string token in _filter.Split(' '))
            {
                if (token.Length > 0 && !n.SearchKey.Contains(token)) return false;
            }

            return true;
        }

        private bool AnyMatch(Node n)
        {
            if (Matches(n)) return true;
            foreach (Node c in n.Children) if (AnyMatch(c)) return true;
            return false;
        }

        private void Refresh()
        {
            if (!_built) { UpdatePlaceholder(); return; }

            _visible.Clear();
            int matches = 0;
            foreach (Node c in _top) Apply(c, true, ref matches);

            _count.text = _filter.Length == 0
                ? (AppLanguageManager.IsSpanish ? "Click para seleccionar · Esc para cerrar" : "Click to select · Esc to close")
                : (AppLanguageManager.IsSpanish ? matches + (matches == 1 ? " resultado" : " resultados") : matches + (matches == 1 ? " result" : " results"));
            UpdateCursor();
            UpdatePlaceholder();
        }

        private void Apply(Node n, bool parentShown, ref int matches)
        {
            bool filtering = _filter.Length > 0;
            bool selfMatch = Matches(n);
            bool show = parentShown && (!filtering || AnyMatch(n) || HasMatchingAncestor(n));
            n.Row.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            n.Title.text = Title(n);
            n.Row.EnableInClassList("desktop-tree-row--match", filtering && selfMatch && n.Entry != null && !n.IsGroup);

            bool expanded = filtering ? true : n.Expanded;
            if (n.Children.Count > 0) n.Chevron.text = expanded ? "▾" : "▸";
            if (show)
            {
                _visible.Add(n);
                if (filtering && n.Entry != null && !n.IsGroup) matches++;
            }

            foreach (Node c in n.Children) Apply(c, show && expanded, ref matches);
        }

        private bool HasMatchingAncestor(Node n)
        {
            if (_filter.Length == 0) return false;
            for (Node p = n.Parent; p != null; p = p.Parent) if (Matches(p) && p.Entry != null) return true;
            return false;
        }

        private void UpdatePlaceholder()
        {
            bool empty = string.IsNullOrEmpty(_search.value);
            bool focused = _root.panel != null && _root.panel.focusController?.focusedElement is VisualElement fe && IsInside(fe, _search);
            _placeholder.style.display = empty ? DisplayStyle.Flex : DisplayStyle.None;
            _placeholder.text = AppLanguageManager.IsSpanish ? "Buscar pieza (Ctrl+F)" : "Search part (Ctrl+F)";
            _placeholder.EnableInClassList("desktop-tree-placeholder--focus", focused);
        }

        private static bool IsInside(VisualElement e, VisualElement ancestor)
        {
            for (; e != null; e = e.parent) if (e == ancestor) return true;
            return false;
        }

        private void ApplyLanguage()
        {
            _title.text = AppLanguageManager.IsSpanish ? "PIEZAS" : "PARTS";
            _pillLabel.text = _title.text;
            UpdatePlaceholder();
        }

        private void OnLanguageChanged(string code)
        {
            ApplyLanguage();
            if (_built) Refresh();
        }

        // ───────────────────────── interaction ─────────────────────────

        private void Activate(Node n)
        {
            if (n.Entry == null || n.IsGroup)
            {
                n.Expanded = !n.Expanded;
                Refresh();
                return;
            }

            if (n.Children.Count > 0 && _filter.Length == 0)
            {
                // A group that is also a part (e.g. the arm assembly): the click selects it and opens it;
                // a second click on the selected group folds it.
                n.Expanded = _selected == n ? !n.Expanded : true;
                if (_selected == n) { Refresh(); return; }
            }

            _selected?.Row?.RemoveFromClassList("desktop-tree-row--selected");
            _selected = n;
            n.Row.AddToClassList("desktop-tree-row--selected");
            Refresh();
            Debug.Log("[DesktopUI] part tree: select " + n.Entry.entryId);
            DeepLinkSelector.RequestSelect(n.Entry.entryId, false);
        }

        private void OnSearchKey(KeyDownEvent evt)
        {
            switch (evt.keyCode)
            {
                case KeyCode.DownArrow: MoveCursor(1); evt.StopPropagation(); break;
                case KeyCode.UpArrow: MoveCursor(-1); evt.StopPropagation(); break;
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    if (_cursor >= 0 && _cursor < _visible.Count) Activate(_visible[_cursor]);
                    else SelectByQueryFirst();
                    evt.StopPropagation();
                    break;
            }
        }

        private void MoveCursor(int delta)
        {
            if (_visible.Count == 0) return;
            _cursor = Mathf.Clamp(_cursor + delta, 0, _visible.Count - 1);
            UpdateCursor();
            if (_visible[_cursor].Row != null) _scroll.ScrollTo(_visible[_cursor].Row);
        }

        private void UpdateCursor()
        {
            foreach (Node n in _visible) n.Row.RemoveFromClassList("desktop-tree-row--cursor");
            if (_cursor >= 0 && _cursor < _visible.Count) _visible[_cursor].Row.AddToClassList("desktop-tree-row--cursor");
        }

        // ───────────────────────── selection sync ─────────────────────────

        private void OnPartSelected(PartSelectedEvent evt)
        {
            if (!_built || evt.PartData == null) return;
            if (InfoPanelCatalogService.Instance.TryResolve(evt.PartData, evt.FromHotspot, evt.HotspotGroupLabel,
                    evt.SelectionLabel, evt.CanonicalPartName, out InfoPanelCatalogEntry entry)
                && entry != null && _byId.TryGetValue(entry.entryId, out Node n))
            {
                _selected?.Row?.RemoveFromClassList("desktop-tree-row--selected");
                _selected = n;
                if (_open) SyncSelectionHighlight(false);
                else
                {
                    for (Node p = n.Parent; p != null; p = p.Parent) p.Expanded = true;
                }
            }
        }

        private void SyncSelectionHighlight(bool scroll)
        {
            if (_selected == null || _selected.Row == null) { Refresh(); return; }
            for (Node p = _selected.Parent; p != null; p = p.Parent) p.Expanded = true;
            Refresh();
            _selected.Row.AddToClassList("desktop-tree-row--selected");
            _selected.Row.schedule.Execute(() => _scroll.ScrollTo(_selected.Row)).ExecuteLater(30);
        }

        // ───────────────────────── text ─────────────────────────

        /// <summary>Lower case without accents ("Hélice" -> "helice").</summary>
        internal static string Normalize(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            string d = s.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(d.Length);
            foreach (char c in d)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
            }

            return sb.ToString().Normalize(NormalizationForm.FormC);
        }
    }
}
#endif
