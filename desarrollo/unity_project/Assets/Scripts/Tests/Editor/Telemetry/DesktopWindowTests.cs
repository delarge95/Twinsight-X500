#if UNITY_STANDALONE
// PLAN_PC 42 (D): the side windows of the Telemetry tab remember where they were left (PlayerPrefs Desk_Win_<id> = "x;y"), stay inside the app window and
// forget their place with a double click or «Restablecer ventanas». The drag itself and the place beside the tree / inspector are checked in the player
// (`-fix2-capture`).
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using WebGL.UI.Desktop;

namespace WebGL.Telemetry.Tests
{
    public class DesktopWindowTests
    {
        const string Id = "mission";

        [TearDown]
        public void TearDown() { DesktopWindow.Forget(Id); }

        [Test]
        public void The_Key_Is_Documented_And_Every_Window_Has_One()
        {
            Assert.AreEqual("Desk_Win_mission", DesktopWindow.PrefKey(Id));
            CollectionAssert.AllItemsAreUnique(DesktopWindow.Ids);
            CollectionAssert.IsSubsetOf(new[] { "alerts", "faults", "fence", "wind", "batteryreact", "preflight", "mission", "record", "training", "source" }, DesktopWindow.Ids);
        }

        [Test]
        public void Format_And_Parse_Round_Trip_And_Junk_Is_Refused()
        {
            Assert.AreEqual("120;88", DesktopWindow.Format(new Vector2(120.4f, 87.6f)));
            Assert.IsTrue(DesktopWindow.TryParse("120;88", out Vector2 p));
            Assert.AreEqual(new Vector2(120, 88), p);
            foreach (string bad in new[] { null, "", "1", "a;b", "1;2;3", "NaN;1", "1;Infinity" }) Assert.IsFalse(DesktopWindow.TryParse(bad, out _), bad ?? "null");
        }

        [Test]
        public void Clamp_Keeps_The_Window_Inside_And_The_Title_Bar_Reachable()
        {
            var bounds = new Rect(0, 44, 1280, 650);
            Assert.AreEqual(new Vector2(100, 100), DesktopWindow.Clamp(new Vector2(100, 100), new Vector2(300, 200), bounds));
            Assert.AreEqual(new Vector2(980, 494), DesktopWindow.Clamp(new Vector2(5000, 5000), new Vector2(300, 200), bounds));
            Assert.AreEqual(new Vector2(0, 44), DesktopWindow.Clamp(new Vector2(-50, -50), new Vector2(300, 200), bounds));
            Assert.AreEqual(new Vector2(0, 44), DesktopWindow.Clamp(new Vector2(500, 500), new Vector2(2000, 2000), bounds), "too big: top-left wins");
        }

        [Test]
        public void Default_Place_Is_Beside_The_Tree_Or_The_Inspector()
        {
            Vector2 left = DesktopWindow.DefaultPos(false, 360, new Vector2(1904, 984), 40 + 280, 40 + 380, 140);
            Assert.AreEqual(new Vector2(328, 140), left);
            Vector2 right = DesktopWindow.DefaultPos(true, 360, new Vector2(1904, 984), 40, 420, 140);
            Assert.AreEqual(new Vector2(1904 - 420 - 8 - 360, 140), right);
        }

        [Test]
        public void A_Moved_Window_Is_Remembered_And_Reset_Forgets_It()
        {
            DesktopWindow.Save(Id, new Vector2(400, 300));
            Assert.IsTrue(DesktopWindow.TryLoad(Id, out Vector2 p));
            Assert.AreEqual(new Vector2(400, 300), p);
            Assert.AreEqual("400;300", PlayerPrefs.GetString("Desk_Win_mission"));
            bool raised = false;
            System.Action h = () => raised = true;
            DesktopWindow.ResetRequested += h;
            DesktopWindow.ResetAll();
            DesktopWindow.ResetRequested -= h;
            Assert.IsTrue(raised);
            Assert.IsFalse(DesktopWindow.TryLoad(Id, out _));
        }

        [Test]
        public void The_Drag_Handle_Turns_A_Popover_Into_A_Window_And_MoveTo_Saves_A_Clamped_Place()
        {
            var layer = new VisualElement();
            var pop = new VisualElement();
            var head = new VisualElement();
            head.AddToClassList("dk-pop-head");
            pop.Add(head);
            layer.Add(pop);
            var w = new DesktopWindowDrag(pop, layer, Id, false, () => 40, () => 40, () => 100);
            Assert.IsTrue(pop.ClassListContains("dk-win"));
            Assert.IsTrue(head.ClassListContains("dk-win-head"));
            w.MoveTo(new Vector2(-500, -500));
            Assert.IsTrue(DesktopWindow.TryLoad(Id, out Vector2 p));
            Assert.AreEqual(0f, p.x);
            Assert.AreEqual(44f, p.y, "never over the top bar");
            w.ResetPlace();
            Assert.IsFalse(DesktopWindow.TryLoad(Id, out _));
            Assert.AreEqual(48f, w.Position.x, "back beside the left rail");
        }
    }
}
#endif
