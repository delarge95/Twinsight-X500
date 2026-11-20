// PLAN_PC 41: on PC, pressing the Cut or Filter card again turns its effect off (like Explode); the panel of fine adjustments stays reachable.
#if UNITY_STANDALONE
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using WebGL.Core.Managers;
using WebGL.UI.Panels;

namespace WebGL.Telemetry.Tests
{
    public class AnalyzeCardToggleTests
    {
        GameObject _go;
        VisualElement _container;
        AnalyzeModeHandler _handler;
        Button _cut, _filter;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("test-cross-section");
            var mgr = _go.AddComponent<CrossSectionManager>();
            typeof(CrossSectionManager).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly).Invoke(mgr, null); // StaticInstance.Awake registers Instance
            Assert.AreSame(mgr, CrossSectionManager.Instance);

            _container = new VisualElement();
            var grid = new VisualElement { name = "AnalyzeCardGrid" };
            _cut = new Button { name = "AnalyzeCrossSectionBtn" };
            _filter = new Button { name = "AnalyzeFilterBtn" };
            grid.Add(_cut);
            grid.Add(new Button { name = "AnalyzeExplodeBtn" });
            grid.Add(_filter);
            _container.Add(grid);
            _container.Add(new VisualElement { name = "CrossSectionPanel" });
            _container.Add(new VisualElement { name = "FilterSubPanel" });
            _handler = new AnalyzeModeHandler(null, _container); // root null: DelayAction runs at once
        }

        [TearDown]
        public void TearDown()
        {
            _handler.Dispose();
            Object.DestroyImmediate(_go);
        }

        [Test]
        public void Logic_Table()
        {
            Assert.AreEqual(AnalyzeCardToggle.Step.OpenPanel, AnalyzeCardToggle.ForCard(false, false));
            Assert.AreEqual(AnalyzeCardToggle.Step.OpenPanel, AnalyzeCardToggle.ForCard(true, false), "effect on, panel closed: the fine adjustment re-opens");
            Assert.AreEqual(AnalyzeCardToggle.Step.TurnOff, AnalyzeCardToggle.ForCard(true, true));
            Assert.AreEqual(AnalyzeCardToggle.Step.ClosePanel, AnalyzeCardToggle.ForCard(false, true));
        }

        [Test]
        public void Cut_Card_Pressed_Twice_Turns_The_Cut_Off()
        {
            Assert.IsFalse(CrossSectionManager.Instance.IsEnabled);
            _handler.PressCutCard();
            Assert.IsTrue(CrossSectionManager.Instance.IsEnabled, "first press: cut on and its panel open");
            Assert.IsTrue(_handler.IsSubPanelOpen);
            Assert.IsTrue(_cut.ClassListContains("submenu-card--active"));
            _handler.PressCutCard();
            Assert.IsFalse(CrossSectionManager.Instance.IsEnabled, "second press: cut off");
            Assert.IsFalse(_handler.IsSubPanelOpen);
            Assert.IsFalse(_cut.ClassListContains("submenu-card--active"), "the card is no longer lit");
        }

        [Test]
        public void Cut_Panel_Closed_With_The_Cut_On_Reopens_On_Press_And_Then_Turns_Off()
        {
            _handler.PressCutCard();
            _handler.NavigateToCardGrid(); // the panel's X: the cut persists
            Assert.IsTrue(CrossSectionManager.Instance.IsEnabled);
            Assert.IsTrue(_cut.ClassListContains("submenu-card--active"));
            _handler.PressCutCard();
            Assert.IsTrue(CrossSectionManager.Instance.IsEnabled, "the panel re-opens, the cut stays");
            Assert.IsTrue(_handler.IsSubPanelOpen);
            _handler.PressCutCard();
            Assert.IsFalse(CrossSectionManager.Instance.IsEnabled);
        }

        [Test]
        public void Filter_Card_Pressed_Again_Clears_The_Filter()
        {
            Assert.IsFalse(_handler.IsCategoryFilterActive);
            _handler.PressFilterCard();
            Assert.IsTrue(_handler.IsSubPanelOpen, "first press opens the panel");
            _handler.SetCategoryFilter("Avionics", null, true);
            Assert.IsTrue(_handler.IsCategoryFilterActive);
            Assert.IsTrue(_filter.ClassListContains("submenu-card--active"));
            _handler.PressFilterCard();
            Assert.IsFalse(_handler.IsCategoryFilterActive, "second press: the filter is removed");
            Assert.IsFalse(_handler.IsSubPanelOpen);
            Assert.IsFalse(_filter.ClassListContains("submenu-card--active"));
        }

        [Test]
        public void Filter_Card_With_No_Filter_And_Its_Panel_Open_Only_Closes_The_Panel()
        {
            _handler.PressFilterCard();
            _handler.PressFilterCard();
            Assert.IsFalse(_handler.IsSubPanelOpen);
            Assert.IsFalse(_handler.IsCategoryFilterActive);
        }

        [Test]
        public void Filter_Closed_Panel_With_A_Filter_Reopens_On_Press()
        {
            _handler.PressFilterCard();
            _handler.SetCategoryFilter("Avionics", null, true);
            _handler.NavigateToCardGrid();
            Assert.IsTrue(_filter.ClassListContains("submenu-card--active"), "the filter card stays lit while the filter narrows");
            _handler.PressFilterCard();
            Assert.IsTrue(_handler.IsSubPanelOpen);
            Assert.IsTrue(_handler.IsCategoryFilterActive);
        }
    }
}
#endif
