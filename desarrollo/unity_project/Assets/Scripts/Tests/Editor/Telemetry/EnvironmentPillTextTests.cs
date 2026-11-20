// PLAN_PC 41: the environment pill texts. The real measurement (that no label leaves its pill, ES and EN, at 1280x720, 1904x984 and 150 %) needs a live panel and is done
// in the player by `-fix1-capture` (DesktopCaptureFix1.CheckPills); here, the texts themselves.
using NUnit.Framework;
using WebGL.UI.Panels;

namespace WebGL.Telemetry.Tests
{
    public class EnvironmentPillTextTests
    {
        string _lang;

        [SetUp] public void SetUp() { _lang = AppLanguageManager.CurrentLanguageCode; }
        [TearDown] public void TearDown() { AppLanguageManager.SetLanguage(_lang); }

        [Test]
        public void Studio_Light_Reads_Estudio_Claro_In_Spanish_And_Studio_Light_In_English()
        {
            AppLanguageManager.SetLanguage("es");
            Assert.AreEqual("Estudio claro", AppLanguageManager.TranslateStatic("Studio Light"));
            Assert.AreEqual("ESTUDIO CLARO", AppLanguageManager.TranslateStatic("STUDIO LIGHT"));
            AppLanguageManager.SetLanguage("en");
            Assert.AreEqual("Studio Light", AppLanguageManager.TranslateStatic("Studio Light"));
        }

        [Test]
        public void No_Spanish_Pill_Text_Is_Longer_Than_Two_Words()
        {
            AppLanguageManager.SetLanguage("es");
            foreach (string key in new[] { "Studio", "Studio Light", "Blueprint", "Day", "Night", "Sunset", "White", "Grey", "Black", "Yellow", "Orange", "Green", "Blue", "Purple", "Red" })
            {
                string t = AppLanguageManager.TranslateStatic(key);
                Assert.LessOrEqual(t.Split(' ').Length, 2, key + " -> " + t + ": it wraps in the pill, it must not need three lines");
            }
        }
    }
}
