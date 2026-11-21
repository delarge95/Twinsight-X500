#if UNITY_STANDALONE
using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

namespace WebGL.UI.Desktop
{
    /// <summary>
    /// PLAN_PC 42 (D): the panels of the Telemetry tab (Comprobar, Mision, Grabar / Mis sesiones, Aprender, Alertas, Fallas, Geocerca, Viento, Bateria, Fuente) open
    /// as side windows next to the part tree (left) or the inspector (right), never in the middle of the 3D canvas. A window moves by dragging its title bar, stays
    /// inside the window of the app, and remembers where it was left:
    ///   PlayerPrefs <c>Desk_Win_&lt;id&gt;</c> (string "x;y", logical px of the panel, invariant culture). No key = the default place beside the side panels.
    /// Double click on the title bar, or «Restablecer ventanas» in Ajustes (<see cref="ResetAll"/>), forgets the place. Esc closes (the popover rule of the tab),
    /// the close button and every control inside keep the focus ring of the app (Tab / the pad's stick in Menus).
    /// </summary>
    internal static class DesktopWindow
    {
        public const string PrefPrefix = "Desk_Win_";
        /// <summary>Every window id, for <see cref="ResetAll"/> and the documentation.</summary>
        public static readonly string[] Ids = { "source", "alerts", "faults", "fence", "wind", "batteryreact", "preflight", "mission", "record", "training" };
        public const float Gap = 8f;
        public const float StatusBar = 26f;

        public static string PrefKey(string id) => PrefPrefix + id;

        public static string Format(Vector2 p) => p.x.ToString("0", CultureInfo.InvariantCulture) + ";" + p.y.ToString("0", CultureInfo.InvariantCulture);

        public static bool TryParse(string s, out Vector2 p)
        {
            p = default;
            if (string.IsNullOrEmpty(s)) return false;
            string[] parts = s.Split(';');
            if (parts.Length != 2) return false;
            if (!float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x)) return false;
            if (!float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y)) return false;
            if (float.IsNaN(x) || float.IsNaN(y) || float.IsInfinity(x) || float.IsInfinity(y)) return false;
            p = new Vector2(x, y);
            return true;
        }

        /// <summary>Keeps a window of <paramref name="size"/> inside <paramref name="bounds"/> (the title bar always reachable: top-left wins when it does not fit).</summary>
        public static Vector2 Clamp(Vector2 p, Vector2 size, Rect bounds)
        {
            float x = Mathf.Min(p.x, bounds.xMax - size.x);
            float y = Mathf.Min(p.y, bounds.yMax - size.y);
            return new Vector2(Mathf.Max(bounds.xMin, x), Mathf.Max(bounds.yMin, y));
        }

        /// <summary>The default place: below the strips, beside the left side panel (tree) or the right one (inspector).</summary>
        public static Vector2 DefaultPos(bool right, float width, Vector2 screen, float leftTaken, float rightTaken, float top)
        {
            float x = right ? screen.x - rightTaken - Gap - width : leftTaken + Gap;
            return new Vector2(x, top);
        }

        public static bool TryLoad(string id, out Vector2 p)
        {
            p = default;
            try { return PlayerPrefs.HasKey(PrefKey(id)) && TryParse(PlayerPrefs.GetString(PrefKey(id)), out p); }
            catch (Exception) { return false; }
        }

        public static void Save(string id, Vector2 p)
        {
            try { PlayerPrefs.SetString(PrefKey(id), Format(p)); PlayerPrefs.Save(); } catch (Exception) { }
        }

        public static void Forget(string id)
        {
            try { PlayerPrefs.DeleteKey(PrefKey(id)); } catch (Exception) { }
        }

        public static void ResetAll()
        {
            foreach (string id in Ids) Forget(id);
            ResetRequested?.Invoke();
        }

        /// <summary>Raised by <see cref="ResetAll"/>: an open window goes back to its default place at once.</summary>
        public static event Action ResetRequested;
    }

    /// <summary>The live part: one per popover of the Telemetry tab (drag by the title bar, double click resets, place on open).</summary>
    internal sealed class DesktopWindowDrag
    {
        private readonly VisualElement _win, _head, _layer;
        private readonly string _id;
        private readonly bool _right;
        private readonly Func<float> _left, _rightTaken, _top;
        private bool _dragging;
        private int _pointer = -1;
        private Vector2 _grab;

        public string Id => _id;
        public VisualElement Window => _win;
        public bool Moved => DesktopWindow.TryLoad(_id, out _);

        public DesktopWindowDrag(VisualElement win, VisualElement layer, string id, bool right, Func<float> leftTaken, Func<float> rightTaken, Func<float> top)
        {
            _win = win; _layer = layer; _id = id; _right = right; _left = leftTaken; _rightTaken = rightTaken; _top = top;
            _head = win.Q<VisualElement>(className: "dk-pop-head");
            win.AddToClassList("dk-win");
            win.style.translate = new Translate(0, 0);
            if (_head == null) return;
            _head.AddToClassList("dk-win-head");
            _head.pickingMode = PickingMode.Position;
            _head.tooltip = string.Empty;
            _head.RegisterCallback<PointerDownEvent>(OnDown);
            _head.RegisterCallback<PointerMoveEvent>(OnMove);
            _head.RegisterCallback<PointerUpEvent>(OnUp);
            _head.RegisterCallback<PointerCaptureOutEvent>(_ => _dragging = false);
            _head.RegisterCallback<ClickEvent>(e => { if (e.clickCount == 2 && !(e.target is Button)) ResetPlace(); });
            DesktopWindow.ResetRequested += () => { if (_win.style.display != DisplayStyle.None) Place(); };
        }

        private Vector2 Screen => _layer.layout.width > 1f ? new Vector2(_layer.layout.width, _layer.layout.height) : new Vector2(UnityEngine.Screen.width, UnityEngine.Screen.height);

        private Vector2 Size()
        {
            float w = _win.layout.width, h = _win.layout.height;
            if (float.IsNaN(w) || w < 1f) w = _win.resolvedStyle.width;
            if (float.IsNaN(w) || w < 1f) w = 300f;
            if (float.IsNaN(h) || h < 1f) h = 120f;
            return new Vector2(w, h);
        }

        private Rect Bounds
        {
            get
            {
                Vector2 s = Screen;
                return new Rect(0f, 44f, s.x, Mathf.Max(60f, s.y - 44f - DesktopWindow.StatusBar));
            }
        }

        public Vector2 DefaultPos() => DesktopWindow.DefaultPos(_right, Size().x, Screen, _left(), _rightTaken(), _top());

        public Vector2 Position => new Vector2(_win.style.left.value.value, _win.style.top.value.value);

        /// <summary>Puts the window where it was left (or its default place), inside the app window; called on open and when the layout changes.</summary>
        private int _autoPlaces;

        /// <summary>After a size change (content filled in after opening): re-clamped at most three times per opening, never a layout loop.</summary>
        public void AutoPlace()
        {
            if (_dragging || _autoPlaces >= 3) return;
            _autoPlaces++;
            PlaceNow();
        }

        public void Place()
        {
            _autoPlaces = 0;
            PlaceNow();
        }

        private void PlaceNow()
        {
            Vector2 p = DesktopWindow.TryLoad(_id, out Vector2 saved) ? saved : DefaultPos();
            SetPos(DesktopWindow.Clamp(p, Size(), Bounds));
            float maxH = Bounds.yMax - _win.style.top.value.value - DesktopWindow.Gap;
            _win.style.maxHeight = Mathf.Max(120f, maxH);
        }

        public void ResetPlace()
        {
            DesktopWindow.Forget(_id);
            Place();
        }

        /// <summary>Moves the window (verification and the drag): kept inside, remembered.</summary>
        public void MoveTo(Vector2 p, bool remember = true)
        {
            Vector2 c = DesktopWindow.Clamp(p, Size(), Bounds);
            SetPos(c);
            if (remember) DesktopWindow.Save(_id, c);
        }

        private void SetPos(Vector2 p)
        {
            _win.style.left = p.x;
            _win.style.top = p.y;
            _win.style.right = StyleKeyword.Auto;
        }

        private void OnDown(PointerDownEvent e)
        {
            if (e.button != 0 || e.target is Button || (e.target as VisualElement)?.GetFirstAncestorOfType<Button>() != null) return;
            _dragging = true;
            _pointer = e.pointerId;
            _grab = (Vector2)e.position - Position;
            _head.CapturePointer(e.pointerId);
            e.StopPropagation();
        }

        private void OnMove(PointerMoveEvent e)
        {
            if (!_dragging || e.pointerId != _pointer) return;
            MoveTo((Vector2)e.position - _grab, false);
            e.StopPropagation();
        }

        private void OnUp(PointerUpEvent e)
        {
            if (!_dragging || e.pointerId != _pointer) return;
            _dragging = false;
            if (_head.HasPointerCapture(e.pointerId)) _head.ReleasePointer(e.pointerId);
            DesktopWindow.Save(_id, Position);
            e.StopPropagation();
        }
    }
}
#endif
