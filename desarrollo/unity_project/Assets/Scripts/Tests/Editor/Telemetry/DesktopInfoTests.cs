#if UNITY_STANDALONE
// PLAN_PC 39 (UX step 4): the ⓘ and the «Avanzado» fold of the desktop interface, and the rule that no honesty string disappears from the tree when a
// long note moves behind an ⓘ. What needs a live panel (hover after 0.4 s, keyboard focus, Tab, the pad's A/B, the bubble inside the window at 1280x720) is
// checked in the player by `-ux-capture` (DesktopCaptureUx.Info.cs).
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using WebGL.UI.Desktop;

namespace WebGL.Telemetry.Tests
{
    public class DesktopInfoTests
    {
        const string Key = "ux_edit_fold";

        [TearDown]
        public void TearDown() { PlayerPrefs.DeleteKey(DesktopFold.PrefKey(Key)); }

        [Test]
        public void Info_Is_A_Focusable_24px_Button_With_The_Text_As_Tooltip_And_Empty_Text_Hides_It()
        {
            Button i = DesktopInfo.Create("X", "una explicacion larga");
            Assert.IsTrue(DesktopInfo.IsInfo(i));
            Assert.IsTrue(i.focusable, "reachable with Tab and with the pad's focus ring");
            Assert.IsTrue(i.ClassListContains("dk-ibtn--small"), "24 px target (USS .dk-ibtn--small)");
            Assert.AreEqual("una explicacion larga", i.tooltip);
            DesktopInfo.SetText(i, "");
            Assert.AreEqual(DisplayStyle.None, i.style.display.value);
            DesktopInfo.SetText(i, "otra");
            Assert.AreEqual("otra", i.tooltip);
            Assert.AreNotEqual(DisplayStyle.None, i.style.display.value);
        }

        [Test]
        public void Attach_Keeps_The_Note_In_The_Tree_Hidden_And_Gives_Its_Text_To_The_Info()
        {
            var parent = new VisualElement();
            var before = new Label("antes");
            var note = new Label("No es PX4 ni un dron real: todos sus datos son simulados.");
            parent.Add(before);
            parent.Add(note);
            var shortLabel = new Label("Simulado");
            VisualElement row = DesktopInfo.Attach(note, shortLabel);
            Assert.AreSame(parent, row.parent);
            Assert.AreEqual(1, parent.IndexOf(row), "the row takes the place of the note");
            Assert.IsTrue(row.Contains(note), "the note stays in the tree");
            Assert.AreEqual(DisplayStyle.None, note.style.display.value);
            Button info = row.Q<Button>(className: DesktopInfo.ClassName);
            Assert.AreEqual(note.text, info.tooltip);
            Assert.IsTrue(DesktopInfo.TreeHas(parent, "No es PX4"), "the long text is found through the tree scan");
            Assert.IsTrue(DesktopInfo.TreeHas(parent, "Simulado"), "the short label stays");
        }

        [Test]
        public void An_Empty_Note_Hides_The_Whole_Row()
        {
            var parent = new VisualElement();
            var note = new Label(string.Empty);
            parent.Add(note);
            VisualElement row = DesktopInfo.Attach(note);
            Assert.AreEqual(DisplayStyle.None, row.style.display.value);
        }

        [Test]
        public void Honesty_Strings_Exist_In_Both_Languages_And_Survive_Behind_An_Info()
        {
            foreach (string lang in new[] { "es", "en" })
            {
                List<string> need = DesktopHonesty.Required(lang);
                Assert.GreaterOrEqual(need.Count, 5, lang);
                var tree = new VisualElement();
                for (int k = 0; k < need.Count; k++)
                {
                    Assert.IsFalse(string.IsNullOrEmpty(need[k]), lang + " string " + k + " exists in labels.json");
                    // the even ones as text, the odd ones inside the tooltip of an ⓘ (what step 5 does with long notes)
                    if (k % 2 == 0) tree.Add(new Label("etiqueta " + need[k]));
                    else tree.Add(DesktopInfo.Create(null, "explicacion larga que contiene " + need[k] + " y mas texto"));
                }
                foreach (string s in need) Assert.IsTrue(DesktopInfo.TreeHas(tree, s), lang + ": «" + s + "» is in the tree (text or ⓘ)");
                Assert.IsFalse(DesktopInfo.TreeHas(new VisualElement(), need[0]), "an empty tree has none (the scan can fail)");
            }
        }

        [Test]
        public void Fold_Is_Closed_By_Default_Remembers_Only_A_User_Click_And_B_Closes_From_Inside()
        {
            PlayerPrefs.DeleteKey(DesktopFold.PrefKey(Key));
            var f = new DesktopFold(Key);
            var inner = new Button();
            f.Body.Add(inner);
            Assert.IsFalse(f.IsOpen);
            Assert.AreEqual(DisplayStyle.None, f.Body.style.display.value);
            Assert.IsTrue(f.Contains(inner), "closed is hidden, not removed");
            f.SetOpen(true);
            Assert.AreEqual(0, PlayerPrefs.GetInt(DesktopFold.PrefKey(Key), 0), "a probe or code does not store the choice");
            f.SetOpen(false);
            f.SetOpen(true, true);
            Assert.AreEqual(1, PlayerPrefs.GetInt(DesktopFold.PrefKey(Key), 0));
            Assert.IsTrue(new DesktopFold(Key).IsOpen, "the choice is remembered");
            Assert.IsTrue(DesktopFold.TryCloseFor(inner), "B from inside closes the open fold");
            Assert.IsFalse(f.IsOpen);
            Assert.AreEqual(0, PlayerPrefs.GetInt(DesktopFold.PrefKey(Key), 0));
            Assert.IsFalse(DesktopFold.TryCloseFor(inner), "a closed fold does not consume B");
            Assert.AreEqual("Desk_Fold_x", DesktopFold.PrefKey("x"));
        }
    }
}
#endif
