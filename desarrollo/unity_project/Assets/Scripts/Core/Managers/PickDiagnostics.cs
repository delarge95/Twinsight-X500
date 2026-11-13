#if UNITY_STANDALONE
using System;
using UnityEngine;
using WebGL.Core.Content;

namespace WebGL.Core.Managers
{
    /// <summary>
    /// Describes what a left click at a screen position would do, following the same path as SelectionManager.HandleClick:
    /// app state, InputBlocked, UI gesture ownership, the UI element under the pointer (Panel.Pick: name, type, picking mode, rect),
    /// the selection raycast (collider, layer, distance) and how far that collider's PHYSICS pose is from where its mesh is drawn.
    /// Used by the -desk-pickprobe harness and, with <c>-pickdbg</c>, logged on every left click in the player.
    /// </summary>
    public static class PickDiagnostics
    {
        public static readonly bool LogClicks = Array.IndexOf(Environment.GetCommandLineArgs(), "-pickdbg") >= 0;

        /// <summary>Collider drift above this (metres) means the physics shape is not where the part is drawn.</summary>
        public const float StaleDriftMetres = 0.01f;

        public struct Result
        {
            public bool Interactive, InputBlocked, GestureOwned, UIBlocks, Hit;
            public string UIElement, Collider, Part;
            public int Layer;
            public float Distance, Drift;
            public string Outcome;   // "UI", "PART", "STALE", "BACKGROUND"
        }

        public static Result Describe(Vector2 screen)
        {
            var r = new Result();
            r.Interactive = AppStateMachine.Instance == null || AppStateMachine.Instance.IsInteractive();
            r.InputBlocked = InputManager.InputBlocked;
            InputManager im = InputManager.Instance;
            r.GestureOwned = im != null && im.UIGestureOwnsPointer;
            r.UIBlocks = im != null && im.IsScreenPositionOverSelectionBlockingUI(screen);
            r.UIElement = im != null ? im.DescribePickDetailAt(screen) : "no InputManager";

            Camera cam = Camera.main;
            SelectionManager sel = SelectionManager.Instance;
            if (cam != null && sel != null)
            {
                Ray ray = cam.ScreenPointToRay(screen);
                if (Physics.Raycast(ray, out RaycastHit hit, sel.MaxRayDistance, sel.SelectionLayerMask))
                {
                    r.Hit = true;
                    r.Collider = hit.collider.name;
                    r.Layer = hit.collider.gameObject.layer;
                    r.Distance = hit.distance;
                    r.Drift = ColliderDrift(hit.collider);
                    ExplodablePart ep = hit.collider.GetComponentInParent<ExplodablePart>();
                    r.Part = ep != null ? ep.name : hit.collider.transform.name;
                }
            }

            if (!r.Interactive || r.InputBlocked || r.GestureOwned || r.UIBlocks) r.Outcome = "UI";
            else if (!r.Hit) r.Outcome = "BACKGROUND";
            else if (r.Drift > StaleDriftMetres) r.Outcome = "STALE";
            else r.Outcome = "PART";
            return r;
        }

        /// <summary>Distance between the collider's physics bounds and the bounds of the mesh drawn on the same object (0 if unknown).</summary>
        public static float ColliderDrift(Collider c)
        {
            if (c == null || !c.enabled) return 0f;
            Renderer rend = c.GetComponent<Renderer>();
            if (rend == null || !rend.enabled) return 0f;
            return Vector3.Distance(c.bounds.center, rend.bounds.center);
        }

        public static string Format(Result r)
        {
            return "outcome=" + r.Outcome + " interactive=" + r.Interactive + " inputBlocked=" + r.InputBlocked + " gesture=" + r.GestureOwned
                + " uiBlocks=" + r.UIBlocks + " pick=" + r.UIElement
                + " hit=" + (r.Hit ? r.Collider + " part=" + r.Part + " layer=" + r.Layer + " dist=" + r.Distance.ToString("F2") + " drift=" + r.Drift.ToString("F3") + "m" : "none")
                + " physSync=" + PickPhysicsSync.Enabled;
        }
    }
}
#endif
