// PLAN_PC 27.1. ONE place that decides whether a key may act as a HOTKEY.
//
// Rule: while the user types in ANY text field (the part-tree search box, the SITL URL field, any other TextField / ITextEdition)
// NO shortcut may fire: not 1-4, F, H, I, E, R, C, G, V, T, P, L, Shift+A, Home, ?, F1, [ ], /, not the piloting keys, and no
// click-to-fly. Only Esc (leaves / clears the field), Enter and the field's own editing keys act on the field; F11 and Alt+Enter
// (window keys) stay available.
//
// Every reader of the keyboard for a shortcut goes through this class (DesktopUIController.HandleShortcuts, the pilot keys of
// DesktopTelemetryUI, the tutorial arrows, the profiler key, the click-to-fly hook). It is compiled on every platform (it only uses
// UnityEngine.UIElements and the legacy Input) and does nothing in WebGL: nobody binds a root there, so Typing is always false.
//
// The seams (KeyHeld / KeyPressed / InputString / TypingOverride) let the in-player harness and the EditMode tests inject keys.

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace WebGL.Core.Managers
{
    public static class DesktopKeyboardGate
    {
        /// <summary>Key held down (default: the old Input Manager). The harness replaces it.</summary>
        public static Func<KeyCode, bool> KeyHeld = Input.GetKey;
        /// <summary>Key went down this frame.</summary>
        public static Func<KeyCode, bool> KeyPressed = Input.GetKeyDown;
        /// <summary>Characters typed this frame ("?" and "[" work on any keyboard layout this way).</summary>
        public static Func<string> InputString = () => Input.inputString;
        /// <summary>Tests only: forces the answer of <see cref="Typing"/>.</summary>
        public static Func<bool> TypingOverride;

        static VisualElement root;

        public static void Bind(VisualElement uiRoot) { root = uiRoot; }

        static readonly Dictionary<Type, bool> textFieldTypes = new Dictionary<Type, bool>();

        /// <summary>
        /// True for an element that EDITS text: the inner input of a field (an ITextEdition that is not read-only) AND the field itself (TextField,
        /// IntegerField, FloatField...: every TextInputBaseField&lt;T&gt;). Two traps of the old check (`e is ITextEdition`):
        ///  - the field itself holds the focus after a programmatic Focus() (Ctrl+F, "/") and it is NOT an ITextEdition: every hotkey went through while
        ///    the user typed in a freshly focused search box;
        ///  - every TextElement is an ITextEdition (read-only by default): a focused Button or Label counted as "typing" and killed the shortcuts.
        /// </summary>
        public static bool IsTextEditor(VisualElement e)
        {
            if (e == null) return false;
            if (e is ITextEdition edition) return !edition.isReadOnly;
            Type t = e.GetType();
            if (textFieldTypes.TryGetValue(t, out bool known)) return known;
            bool isField = false;
            for (Type b = t; b != null; b = b.BaseType)
            {
                if (b.IsGenericType && b.GetGenericTypeDefinition() == typeof(TextInputBaseField<>)) { isField = true; break; }
            }
            textFieldTypes[t] = isField;
            return isField;
        }

        /// <summary>The text field (or its inner input) that owns a focused element, or null when the element is not part of a text field.</summary>
        public static VisualElement TextFieldOf(Focusable focused)
        {
            VisualElement outermost = null; // the field, not its inner input
            for (VisualElement e = focused as VisualElement; e != null; e = e.parent)
                if (IsTextEditor(e)) outermost = e;
            return outermost;
        }

        /// <summary>
        /// True when the focus is inside a text field that is actually on screen. A field whose popover was closed (display none up the
        /// tree) keeps the UI Toolkit focus but nothing can be typed into it: it does not count (see <see cref="Typing"/>, which also releases it).
        /// </summary>
        public static bool IsTextFocus(Focusable focused)
        {
            VisualElement field = TextFieldOf(focused);
            return field != null && !IsHidden(field);
        }

        static bool IsHidden(VisualElement e)
        {
            for (VisualElement p = e; p != null; p = p.parent)
            {
                // the inline style answers at once (the popovers hide with it); the resolved style (USS classes) only means something once attached to a panel
                if (p.style.display == DisplayStyle.None) return true;
                if (p.panel != null && p.resolvedStyle.display == DisplayStyle.None) return true;
            }
            return false;
        }

        /// <summary>True while a text field has the keyboard focus. A focused field that is hidden is blurred here so it cannot swallow typing invisibly.</summary>
        public static bool Typing
        {
            get
            {
                if (TypingOverride != null) return TypingOverride();
                Focusable focused = root?.panel?.focusController?.focusedElement;
                VisualElement field = TextFieldOf(focused);
                if (field == null) return false;
                if (IsHidden(field)) { (focused as VisualElement)?.Blur(); field.Blur(); return false; }
                return true;
            }
        }

        // ───────────────────────── gated readers (the ONLY way a hotkey reads the keyboard) ─────────────────────────

        /// <summary>A shortcut key pressed this frame; false while typing.</summary>
        public static bool Down(KeyCode k) => !Typing && KeyPressed(k);

        /// <summary>A shortcut key held (piloting); false while typing.</summary>
        public static bool Held(KeyCode k) => !Typing && KeyHeld(k);

        /// <summary>The character typed this frame as a shortcut ("?", "/", "[", "]"); false while typing.</summary>
        public static bool Typed(char c)
        {
            if (Typing) return false;
            string s = InputString();
            return !string.IsNullOrEmpty(s) && s.IndexOf(c) >= 0;
        }

        public static bool Shift => KeyHeld(KeyCode.LeftShift) || KeyHeld(KeyCode.RightShift);
        public static bool Alt => KeyHeld(KeyCode.LeftAlt) || KeyHeld(KeyCode.RightAlt);
        public static bool Ctrl => KeyHeld(KeyCode.LeftControl) || KeyHeld(KeyCode.RightControl) || KeyHeld(KeyCode.LeftCommand) || KeyHeld(KeyCode.RightCommand);

        /// <summary>Window keys (F11, Alt+Enter) are NOT hotkeys of the viewer: they work while typing, so they read the raw key.</summary>
        public static bool WindowKeyDown(KeyCode k) => KeyPressed(k);

        /// <summary>Esc while typing: the field gives the focus back. True when it was typing (Esc is consumed).</summary>
        public static bool LeaveTextField()
        {
            Focusable focused = root?.panel?.focusController?.focusedElement;
            VisualElement field = TextFieldOf(focused);
            if (field == null) return false;
            (focused as VisualElement)?.Blur();
            field.Blur();
            return true;
        }

        /// <summary>Clears the injected seams (tests / harness).</summary>
        public static void ResetSeams()
        {
            KeyHeld = Input.GetKey;
            KeyPressed = Input.GetKeyDown;
            InputString = () => Input.inputString;
            TypingOverride = null;
        }
    }
}
