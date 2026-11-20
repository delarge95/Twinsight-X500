#if UNITY_STANDALONE
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace WebGL.UI.Desktop
{
    /// <summary>
    /// PC only (PLAN_PC 39, UX step 4). The "ⓘ" of the desktop interface: a long explanation lives behind a small button instead of in a row of text.
    /// The button is 24 px (the icon inside is 16 px, so the click/focus target is comfortable with a mouse and with a pad), takes the keyboard focus and
    /// the pad focus of the Menus mode, and <see cref="DesktopTooltip"/> shows its text (full, wrapped, max 380 px) after the pointer rests 0.4 s on it, as
    /// soon as it receives the focus (Tab, arrows, pad) and when it is clicked or submitted. The text is ALWAYS in the tree (the <c>tooltip</c> of the
    /// button), so nothing disappears from the interface or from the honesty scan: only its place moves.
    /// </summary>
    internal static class DesktopInfo
    {
        public const string ClassName = "dk-info";
        public const string RowClass = "dk-inforow";

        /// <summary>A new ⓘ; its text is set (and refreshed with the language) with <see cref="SetText"/>.</summary>
        public static Button Create(string name = null, string text = null)
        {
            var b = new Button { name = name };
            b.AddToClassList("dk-ibtn");
            b.AddToClassList("dk-ibtn--small");
            b.AddToClassList(ClassName);
            var ic = new DesktopVectorIcon("info", 16f) { pickingMode = PickingMode.Ignore };
            ic.AddToClassList("dk-icon");
            b.Add(ic);
            b.tooltip = text ?? string.Empty;
            return b;
        }

        public static bool IsInfo(VisualElement e) => e != null && e.ClassListContains(ClassName);

        /// <summary>Sets the text of an ⓘ; an empty text hides it.</summary>
        public static void SetText(VisualElement info, string text)
        {
            if (info == null) return;
            info.tooltip = text ?? string.Empty;
            info.style.display = string.IsNullOrEmpty(text) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        /// <summary>
        /// Moves a long note behind an ⓘ without touching the code that writes it: the note label stays in the tree (hidden, with its text) inside a row
        /// that holds the optional short label and the ⓘ, whose tooltip follows the note text. Call it right after the note was added to its parent.
        /// A note with no text (a state that does not apply) hides the whole row.
        /// </summary>
        public static VisualElement Attach(Label note, Label shortLabel = null, string infoName = null)
        {
            VisualElement parent = note.parent;
            var row = new VisualElement();
            row.AddToClassList(RowClass);
            int at = parent != null ? parent.IndexOf(note) : -1;
            if (parent != null) { note.RemoveFromHierarchy(); parent.Insert(at, row); }
            if (shortLabel != null) { shortLabel.RemoveFromHierarchy(); row.Add(shortLabel); }
            Button info = Create(infoName);
            row.Add(info);
            row.Add(note);
            note.style.display = DisplayStyle.None;
            string last = null;
            void Sync()
            {
                string t = note.text;
                if (t == last) return;
                last = t;
                info.tooltip = t ?? string.Empty;
                row.style.display = string.IsNullOrEmpty(t) ? DisplayStyle.None : DisplayStyle.Flex;
            }
            Sync();
            row.schedule.Execute(Sync).Every(200);
            return row;
        }

        /// <summary>
        /// Puts an ⓘ right after a short visible label (the anchor) and moves the long notes behind it: the notes stay in the tree (hidden, with their text,
        /// still written by the code that owns them) and the tooltip of the ⓘ is their joined text, kept in sync. With <paramref name="wrap"/> the anchor is first
        /// put in a row of its own (when its parent is a column); otherwise the anchor must already be in a row. No text at all hides the ⓘ.
        /// </summary>
        public static Button Beside(VisualElement anchor, bool wrap, string infoName, params Label[] notes)
        {
            VisualElement parent = anchor.parent;
            int at = parent.IndexOf(anchor);
            if (wrap)
            {
                var row = new VisualElement();
                row.AddToClassList(RowClass);
                anchor.RemoveFromHierarchy();
                parent.Insert(at, row);
                row.Add(anchor);
                parent = row;
                at = 0;
            }
            Button info = Create(infoName);
            parent.Insert(at + 1, info);
            foreach (Label n in notes) n.style.display = DisplayStyle.None;
            string last = null;
            void Sync()
            {
                var sb = new System.Text.StringBuilder();
                foreach (Label n in notes)
                {
                    if (string.IsNullOrEmpty(n.text)) continue;
                    if (sb.Length > 0) sb.Append((char)10).Append((char)10);
                    sb.Append(n.text);
                }
                string s = sb.ToString();
                if (s == last) return;
                last = s;
                SetText(info, s);
            }
            Sync();
            info.schedule.Execute(Sync).Every(200);
            return info;
        }

        /// <summary>Every text the user can read below a root: labels, buttons, fields and the tooltips (the ⓘ texts), hidden or not. The honesty scan uses it.</summary>
        public static List<string> CollectTexts(VisualElement root)
        {
            var list = new List<string>();
            if (root == null) return list;
            foreach (VisualElement e in root.Query<VisualElement>().ToList())
            {
                if (e is TextElement te && !string.IsNullOrEmpty(te.text)) list.Add(te.text);
                if (!string.IsNullOrEmpty(e.tooltip)) list.Add(e.tooltip);
            }
            return list;
        }

        /// <summary>True when the text (or its ⓘ) of the tree contains the fragment, ignoring case.</summary>
        public static bool TreeHas(VisualElement root, string fragment)
        {
            if (string.IsNullOrEmpty(fragment)) return true;
            foreach (string s in CollectTexts(root))
            {
                if (s.IndexOf(fragment, System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            return false;
        }
    }
}
#endif
