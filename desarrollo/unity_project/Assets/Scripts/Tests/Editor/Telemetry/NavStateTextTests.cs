// Polish of WP-11: the PX4 nav_state names of the Vuelo tab in the language of the interface (TelemetryStrings.NavStateText).
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace WebGL.Telemetry.Tests
{
    public class NavStateTextTests
    {
        [Test]
        public void EveryPx4NavState_HasAnEsAndEnName_AndEnIsThePx4Name()
        {
            var labels = HonestyLabels.Load();
            var navIds = new List<int>();
            for (int i = 0; i < 40; i++) if (labels.NavState(i) != "nav " + i) navIds.Add(i);
            Assert.GreaterOrEqual(navIds.Count, 16, "labels.json navState has 16 states");
            CollectionAssert.AreEquivalent(navIds, TelemetryStrings.NavStateIds.ToList(), "every state of labels.json has a translation, and no more");
            foreach (int n in navIds)
            {
                string raw = labels.NavState(n);
                string en = TelemetryStrings.NavStateText(n, raw, "en");
                string es = TelemetryStrings.NavStateText(n, raw, "es");
                Assert.AreEqual(raw, en, "EN is the PX4 name (" + n + ")");
                Assert.IsNotEmpty(es);
            }
            Assert.AreEqual("Posición", TelemetryStrings.NavStateText(2, labels.NavState(2), "es"));
            Assert.AreEqual("Position", TelemetryStrings.NavStateText(2, labels.NavState(2), "en"));
            Assert.AreEqual("Mantener posición", TelemetryStrings.NavStateText(4, labels.NavState(4), "es"));
            Assert.AreEqual("nav 99", TelemetryStrings.NavStateText(99, labels.NavState(99), "es"), "unknown id keeps the raw label");
        }
    }
}
