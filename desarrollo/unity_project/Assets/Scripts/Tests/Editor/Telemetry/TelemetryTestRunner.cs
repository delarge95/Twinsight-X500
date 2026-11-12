// Runs the telemetry EditMode tests (and the project EditMode tests, WebGL.Tests.Editor) without opening the Test Runner window and writes a summary to
// Temp/telemetry_tests.txt (menu: TwinSight > Telemetry > Run contract tests, or TelemetryTestRunner.Run() from code).
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace WebGL.Telemetry.Tests
{
    public static class TelemetryTestRunner
    {
        public const string OutFile = "Temp/telemetry_tests.txt";

        [MenuItem("TwinSight/Telemetry/Run contract tests")]
        public static void Run()
        {
            File.WriteAllText(OutFile, "RUNNING\n");
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.RegisterCallbacks(new Callbacks());
            api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode, assemblyNames = new[] { "WebGL.Telemetry.Tests.Editor", "WebGL.Tests.Editor" } }));
        }

        sealed class Callbacks : ICallbacks
        {
            readonly StringBuilder sb = new StringBuilder();
            public void RunStarted(ITestAdaptor testsToRun) { }
            public void TestStarted(ITestAdaptor test) { }

            public void TestFinished(ITestResultAdaptor r)
            {
                if (r.HasChildren) return;
                sb.AppendLine(r.TestStatus + " " + r.Test.FullName + (r.TestStatus == TestStatus.Passed ? "" : "\n  " + r.Message));
            }

            public void RunFinished(ITestResultAdaptor r)
            {
                sb.Insert(0, "TOTAL pass=" + r.PassCount + " fail=" + r.FailCount + " skip=" + r.SkipCount + " inconclusive=" + r.InconclusiveCount + "\n");
                sb.AppendLine("--- golden case counts ---");
                foreach (var kv in Cases.Summary) sb.AppendLine(kv.Key + ": " + kv.Value);
                File.WriteAllText(OutFile, sb.ToString());
                Debug.Log("[TelemetryTests] " + sb);
            }
        }
    }
}
