using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using WebGL.Core.Managers;

namespace WebGL.Tests.Editor
{
    /// <summary>
    /// PLAN_PC 27.1: every hotkey reader goes through DesktopKeyboardGate and the gate is false while a text field has the focus.
    /// The part that needs a live panel (real focus, real key events) is checked in the player (DesktopGateProbe, -desk-check / -desk-fix).
    /// </summary>
    public class DesktopKeyboardGateTests
    {
        static readonly KeyCode[] Hotkeys =
        {
            KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4, KeyCode.F, KeyCode.H, KeyCode.I, KeyCode.E, KeyCode.R, KeyCode.C,
            KeyCode.G, KeyCode.V, KeyCode.T, KeyCode.P, KeyCode.L, KeyCode.A, KeyCode.Home, KeyCode.F1, KeyCode.W, KeyCode.S, KeyCode.D, KeyCode.Q,
            KeyCode.Space, KeyCode.LeftShift,
        };

        [TearDown]
        public void TearDown() { DesktopKeyboardGate.ResetSeams(); }

        [Test]
        public void TextField_AndItsChildren_CountAsTextFocus_ButAButtonDoesNot()
        {
            var field = new TextField();
            var inner = new TextElement();
            field.Add(inner);
            Assert.IsTrue(DesktopKeyboardGate.IsTextFocus(field));
            Assert.IsTrue(DesktopKeyboardGate.IsTextFocus(inner), "the focus of a TextField is on a child of it");
            Assert.AreSame(field, DesktopKeyboardGate.TextFieldOf(inner));
            Assert.IsFalse(DesktopKeyboardGate.IsTextFocus(new Button()));
            Assert.IsFalse(DesktopKeyboardGate.IsTextFocus(null));
        }

        [Test]
        public void TheFieldItself_WithoutItsInnerInput_IsATextFocus_TheBugOfTheOldCheck()
        {
            // a programmatic Focus() (Ctrl+F, "/") leaves the focus on the TextField, which is NOT an ITextEdition: the old check missed it
            var field = new TextField();
            Assert.IsFalse(field is ITextEdition, "this is the premise of the old bug: the field itself is not an ITextEdition");
            Assert.IsTrue(DesktopKeyboardGate.IsTextEditor(field));
            Assert.IsTrue(DesktopKeyboardGate.IsTextFocus(field));
            Assert.IsTrue(DesktopKeyboardGate.IsTextEditor(new IntegerField()), "numeric fields too");
            Assert.IsTrue(DesktopKeyboardGate.IsTextEditor(new FloatField()));
            Assert.IsFalse(DesktopKeyboardGate.IsTextEditor(new Label()));
            Assert.IsFalse(DesktopKeyboardGate.IsTextEditor(new Slider()));
            Assert.IsFalse(DesktopKeyboardGate.IsTextEditor(new Button()), "a Button is a TextElement (an ITextEdition, read-only): focusing it is not typing");
            var inner = field.Q<TextElement>();
            Assert.IsTrue(DesktopKeyboardGate.IsTextEditor(inner), "the inner input of a TextField edits text");
            var ro = new TextField { isReadOnly = true };
            Assert.IsFalse(DesktopKeyboardGate.IsTextEditor(ro.Q<TextElement>()), "a read-only input is not typing");
        }

        [Test]
        public void TextFieldOf_ReturnsTheOutermostField_NotItsInnerInput()
        {
            var field = new TextField();
            VisualElement inner = field.Q<TextElement>();
            Assert.IsNotNull(inner);
            Assert.AreSame(field, DesktopKeyboardGate.TextFieldOf(inner));
        }

        [Test]
        public void ATextFieldInAHiddenPopover_DoesNotCount()
        {
            var popover = new VisualElement();
            var field = new TextField();
            popover.Add(field);
            Assert.IsTrue(DesktopKeyboardGate.IsTextFocus(field));
            popover.style.display = DisplayStyle.None;
            Assert.IsFalse(DesktopKeyboardGate.IsTextFocus(field), "a closed popover keeps the UI Toolkit focus but nothing can be typed into it");
        }

        [Test]
        public void EveryGatedReader_IsFalseWhileTyping_AndReadsTheKeyWhenNot()
        {
            var pressed = new HashSet<KeyCode>();
            DesktopKeyboardGate.KeyPressed = k => pressed.Contains(k);
            DesktopKeyboardGate.KeyHeld = k => pressed.Contains(k);
            DesktopKeyboardGate.InputString = () => "?/[]";
            foreach (KeyCode k in Hotkeys) pressed.Add(k);

            bool typing = true;
            DesktopKeyboardGate.TypingOverride = () => typing;
            foreach (KeyCode k in Hotkeys)
            {
                Assert.IsFalse(DesktopKeyboardGate.Down(k), k + " pressed must not act while typing");
                Assert.IsFalse(DesktopKeyboardGate.Held(k), k + " held must not act while typing");
            }
            foreach (char c in "?/[]") Assert.IsFalse(DesktopKeyboardGate.Typed(c), "typed '" + c + "' must not act while typing");

            typing = false;
            foreach (KeyCode k in Hotkeys)
            {
                Assert.IsTrue(DesktopKeyboardGate.Down(k), k + " pressed acts when not typing");
                Assert.IsTrue(DesktopKeyboardGate.Held(k), k + " held acts when not typing");
            }
            foreach (char c in "?/[]") Assert.IsTrue(DesktopKeyboardGate.Typed(c));
        }

        [Test]
        public void WindowKeys_WorkWhileTyping()
        {
            DesktopKeyboardGate.TypingOverride = () => true;
            DesktopKeyboardGate.KeyPressed = k => k == KeyCode.F11;
            Assert.IsTrue(DesktopKeyboardGate.WindowKeyDown(KeyCode.F11), "F11 / Alt+Enter stay available while typing");
            Assert.IsFalse(DesktopKeyboardGate.Down(KeyCode.F11));
        }

        [Test]
        public void ModifierReaders_FollowTheSeam()
        {
            var held = new HashSet<KeyCode> { KeyCode.RightShift, KeyCode.LeftControl };
            DesktopKeyboardGate.KeyHeld = k => held.Contains(k);
            Assert.IsTrue(DesktopKeyboardGate.Shift);
            Assert.IsTrue(DesktopKeyboardGate.Ctrl);
            Assert.IsFalse(DesktopKeyboardGate.Alt);
        }

        [Test]
        public void WithoutABoundRoot_NothingIsTyping_AsInWebGL()
        {
            Assert.IsFalse(DesktopKeyboardGate.Typing);
            Assert.IsFalse(DesktopKeyboardGate.LeaveTextField());
        }
    }
}
