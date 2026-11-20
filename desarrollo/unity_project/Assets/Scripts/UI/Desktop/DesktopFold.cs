#if UNITY_STANDALONE
using UnityEngine;
using UnityEngine.UIElements;

namespace WebGL.UI.Desktop
{
    /// <summary>
    /// PC only (PLAN_PC 39, UX step 4). The «Avanzado» fold: what is not needed every time goes behind a small fold that is CLOSED by default
    /// (the web's <c>adv-fold</c>). Header = chevron + title (a button: Tab, Enter/Space and the pad's A in Menus mode toggle it); body = <see cref="Body"/>.
    /// The pad's B closes an open fold from inside (<see cref="TryCloseFor"/>). Closed, the body is display:none but its elements stay in the tree.
    /// The choice is remembered per fold in PlayerPrefs, key <c>Desk_Fold_&lt;id&gt;</c> (int 1 = open; none or 0 = closed; PLAN_PC 39). Only a click of the
    /// user stores it: the probes open folds with <c>SetOpen(true)</c> (no persistence), so they never leave a choice behind.
    /// </summary>
    internal sealed class DesktopFold : VisualElement
    {
        public const string PrefPrefix = "Desk_Fold_";
        public static string PrefKey(string id) => PrefPrefix + id;

        private readonly string _id;
        private readonly Button _head;
        private readonly Label _title;
        private readonly VisualElement _chev;
        private bool _open;

        public VisualElement Body { get; }
        public bool IsOpen => _open;
        public Button Header => _head;

        public DesktopFold(string id, string name = null)
        {
            _id = id;
            this.name = name ?? "DkFold_" + id;
            AddToClassList("dk-fold");

            _head = new Button(() => SetOpen(!_open, true)) { name = this.name + "Head" };
            _head.AddToClassList("dk-fold-head");
            _chev = new DesktopVectorIcon("chevron", 14f) { pickingMode = PickingMode.Ignore };
            _chev.AddToClassList("dk-fold-chev");
            _chev.AddToClassList("dk-icon");
            _head.Add(_chev);
            _title = new Label { pickingMode = PickingMode.Ignore };
            _title.AddToClassList("dk-fold-title");
            _head.Add(_title);
            Add(_head);

            Body = new VisualElement { name = this.name + "Body" };
            Body.AddToClassList("dk-fold-body");
            Add(Body);

            SetOpen(ReadChoice(id), false);
        }

        private static bool ReadChoice(string id)
        {
            try { return PlayerPrefs.GetInt(PrefKey(id), 0) == 1; }
            catch { return false; }
        }

        public void SetTitle(string title) { _title.text = title ?? string.Empty; }

        public void SetOpen(bool open, bool persist = false)
        {
            _open = open;
            Body.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
            _chev.EnableInClassList("dk-fold-chev--open", open);
            EnableInClassList("dk-fold--open", open);
            if (persist)
            {
                try { PlayerPrefs.SetInt(PrefKey(_id), open ? 1 : 0); }
                catch { /* a missing store only means the choice is not remembered */ }
            }
        }

        /// <summary>The pad's B: if the focused element is inside an open fold's body, closes that fold, puts the focus on its header and returns true.</summary>
        public static bool TryCloseFor(VisualElement focused)
        {
            for (VisualElement e = focused; e != null; e = e.parent)
            {
                if (e is DesktopFold f && f._open && focused != f._head)
                {
                    f.SetOpen(false, true);
                    f._head.Focus();
                    return true;
                }
            }

            return false;
        }
    }
}
#endif
