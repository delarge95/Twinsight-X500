#if UNITY_STANDALONE
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using WebGL.Core.Managers;
using WebGL.UI.ProceduralIcons;

namespace WebGL.UI.Desktop
{
    /// <summary>
    /// PC only (PLAN_PC 27.3). A small marker anchored on the affected part (motor N, battery, flight controller): a red ring with the fault icon and a
    /// label chip, drawn as UI Toolkit elements and projected from the part's world position every frame, like the pins (ring + chip, no 3D object).
    /// It is visible while the fault is active (a synthetic fault of the local replay, with «falla sintética» in the label) or while a critical alert
    /// stands on the part (label «alerta crítica»; a real alert of the source, never called synthetic). It only says what the data says.
    /// </summary>
    internal sealed class DesktopFaultMarkers
    {
        internal struct Target
        {
            public string Key;
            public string Component;
            public string Label;
            public bool Synthetic;
        }

        private sealed class Marker
        {
            public VisualElement Anchor, Ring, ChipBox;
            public Label Chip;
            public string Key, Component;
            public Transform Part;
            public Renderer[] Renderers;
            public IVisualElementScheduledItem Pulse;
        }

        private readonly VisualElement _root;
        private readonly VisualElement _layer;
        private readonly Dictionary<string, Marker> _active = new Dictionary<string, Marker>();
        private readonly List<string> _drop = new List<string>();
        private readonly List<Marker> _pool = new List<Marker>();

        public int Count => _active.Count;
        public bool IsVisible(string key) => _active.TryGetValue(key, out Marker m) && m.Anchor.style.display != DisplayStyle.None;
        public string LabelOf(string key) => _active.TryGetValue(key, out Marker m) ? m.Chip.text : null;
        public Vector2? ScreenOf(string key) => _active.TryGetValue(key, out Marker m) && m.Anchor.style.display != DisplayStyle.None ? new Vector2(m.Anchor.layout.x, m.Anchor.layout.y) : (Vector2?)null;
        public IEnumerable<string> Keys => _active.Keys;
        public string ComponentOf(string key) => _active.TryGetValue(key, out Marker m) ? m.Component : null;

        public DesktopFaultMarkers(VisualElement root)
        {
            _root = root;
            _layer = new VisualElement { name = "DkFaultMarkers", pickingMode = PickingMode.Ignore };
            _layer.AddToClassList("dk-fx-layer");
            root.Add(_layer);
        }

        public void Dispose() { _layer.RemoveFromHierarchy(); }

        /// <summary>Makes the markers exactly the given targets (keyed): new ones appear, missing ones go.</summary>
        public void SetTargets(List<Target> targets)
        {
            for (int i = 0; i < targets.Count; i++)
            {
                Target t = targets[i];
                if (!_active.TryGetValue(t.Key, out Marker m))
                {
                    m = Acquire();
                    m.Key = t.Key;
                    m.Component = t.Component;
                    m.Part = null;
                    m.Renderers = null;
                    _active[t.Key] = m;
                    m.Anchor.style.display = DisplayStyle.None; // shown by Tick once it has a screen position
                }
                if (m.Chip.text != t.Label) m.Chip.text = t.Label;
                m.ChipBox.EnableInClassList("dk-fx-chip--syn", t.Synthetic);
            }

            _drop.Clear();
            foreach (var kv in _active)
            {
                bool wanted = false;
                for (int i = 0; i < targets.Count; i++) if (targets[i].Key == kv.Key) { wanted = true; break; }
                if (!wanted) _drop.Add(kv.Key);
            }
            foreach (string k in _drop) { Release(_active[k]); _active.Remove(k); }
        }

        /// <summary>Projects every marker onto the screen. Called every frame (one to four markers).</summary>
        public void Tick(bool viewer)
        {
            Camera cam = Camera.main;
            var drv = TelemetryDroneDriver.Instance;
            foreach (Marker m in _active.Values)
            {
                if (!viewer || cam == null || drv == null || _root.panel == null) { m.Anchor.style.display = DisplayStyle.None; continue; }
                if (m.Part == null) { m.Part = drv.PartFor(m.Component); m.Renderers = m.Part != null ? m.Part.GetComponentsInChildren<Renderer>() : null; }
                if (m.Part == null) { m.Anchor.style.display = DisplayStyle.None; continue; }

                Vector3 world = Center(m);
                Vector3 vp = cam.WorldToViewportPoint(world);
                if (vp.z <= 0.01f) { m.Anchor.style.display = DisplayStyle.None; continue; }
                Vector2 p = RuntimePanelUtils.CameraTransformWorldToPanel(_root.panel, world, cam);
                m.Anchor.style.left = p.x;
                m.Anchor.style.top = p.y;
                if (m.Anchor.style.display == DisplayStyle.None) m.Anchor.style.display = DisplayStyle.Flex;
            }
        }

        private static Vector3 Center(Marker m)
        {
            if (m.Renderers == null || m.Renderers.Length == 0) return m.Part.position;
            bool any = false;
            Bounds b = default;
            foreach (Renderer r in m.Renderers)
            {
                if (r == null || !r.enabled) continue;
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            return any ? b.center : m.Part.position;
        }

        private Marker Acquire()
        {
            Marker m;
            if (_pool.Count > 0) { m = _pool[_pool.Count - 1]; _pool.RemoveAt(_pool.Count - 1); }
            else m = Build();
            _layer.Add(m.Anchor);
            m.Pulse?.Resume();
            return m;
        }

        private void Release(Marker m)
        {
            m.Pulse?.Pause();
            m.Anchor.RemoveFromHierarchy();
            m.Anchor.style.display = DisplayStyle.None;
            _pool.Add(m);
        }

        private static Marker Build()
        {
            var m = new Marker();
            m.Anchor = new VisualElement { pickingMode = PickingMode.Ignore };
            m.Anchor.AddToClassList("dk-fx-anchor");
            m.Ring = new VisualElement { pickingMode = PickingMode.Ignore };
            m.Ring.AddToClassList("dk-fx-ring");
            var icon = new DesktopVectorIcon("fault", 12) { pickingMode = PickingMode.Ignore };
            icon.AddToClassList("dk-fx-ringicon");
            m.Ring.Add(icon);
            m.Anchor.Add(m.Ring);
            m.ChipBox = new VisualElement { pickingMode = PickingMode.Ignore };
            m.ChipBox.AddToClassList("dk-fx-chip");
            m.Chip = new Label { pickingMode = PickingMode.Ignore };
            m.Chip.AddToClassList("dk-fx-chiptext");
            m.ChipBox.Add(m.Chip);
            m.Anchor.Add(m.ChipBox);
            // the ring breathes (Reduce animations: a still ring); the schedule is cheap and paused while the marker is pooled
            m.Pulse = m.Ring.schedule.Execute(() => { if (DesktopMotion.Reduce) m.Ring.RemoveFromClassList("dk-fx-ring--big"); else m.Ring.ToggleInClassList("dk-fx-ring--big"); }).Every(650);
            return m;
        }
    }
}
#endif
