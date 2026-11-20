using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace TwinSight.EditorTools.PC
{
    /// <summary>
    /// Ayuda de verificacion: ejecuta todas las pruebas EditMode y escribe el resumen en
    /// <c>Temp/editmode_result.txt</c> (menu TwinSight/PC/Ejecutar EditMode). Solo Editor.
    /// </summary>
    public static class PCEditModeRunner
    {
        private sealed class Cb : ICallbacks
        {
            private readonly System.Text.StringBuilder _fails = new System.Text.StringBuilder();
            public void RunStarted(ITestAdaptor testsToRun) { }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result)
            {
                if (!result.HasChildren && result.TestStatus == TestStatus.Failed)
                    _fails.AppendLine("FAIL " + result.FullName + " :: " + result.Message);
            }
            public void RunFinished(ITestResultAdaptor result)
            {
                string s = result.PassCount + " pass, " + result.FailCount + " fail, " + result.SkipCount + " skip, " + result.InconclusiveCount + " inconclusive\n" + _fails;
                File.WriteAllText(Path.Combine(Path.GetDirectoryName(Application.dataPath), "Temp", "editmode_result.txt"), s);
                Debug.Log("[EditMode] " + s);
            }
        }

        [MenuItem("TwinSight/PC/Ejecutar EditMode")]
        public static void Run()
        {
            string p = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Temp", "editmode_result.txt");
            if (File.Exists(p)) File.Delete(p);
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.RegisterCallbacks(new Cb());
            api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode }));
        }
    }
}
