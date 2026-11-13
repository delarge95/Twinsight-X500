using NUnit.Framework;
using UnityEngine;
using WebGL.Core.Managers;

namespace WebGL.Tests.Editor
{
    /// <summary>
    /// PLAN_PC §26: with SimulationMode = Script and AutoSyncTransforms off, a raycast keeps hitting a collider where it was before its
    /// Transform moved (the flying drone could not be clicked). PickPhysicsSync makes queries see the current transforms.
    /// </summary>
    public class PickPhysicsSyncTests
    {
        private bool _savedAutoSync;
        private SimulationMode _savedMode;
        private GameObject _cube;

        [SetUp]
        public void SetUp()
        {
            _savedAutoSync = Physics.autoSyncTransforms;
            _savedMode = Physics.simulationMode;
            Physics.simulationMode = SimulationMode.Script; // the project setting
            _cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _cube.hideFlags = HideFlags.HideAndDontSave;
            _cube.transform.position = new Vector3(5000f, 0f, 0f);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_cube);
            Physics.autoSyncTransforms = _savedAutoSync;
            Physics.simulationMode = _savedMode;
        }

        private static bool HitsAt(float y) => Physics.Raycast(new Ray(new Vector3(5000f, y, -10f), Vector3.forward), 20f);

        [Test]
        public void WithoutSync_RaycastSeesTheOldColliderPose()
        {
            PickPhysicsSync.Apply(false);
            Physics.SyncTransforms();
            _cube.transform.position += Vector3.up * 5f;
            Assert.IsTrue(HitsAt(0f), "old behaviour: the collider stays where the part was");
            Assert.IsFalse(HitsAt(5f), "old behaviour: the drawn part is not hit");
        }

        [Test]
        public void WithSync_RaycastFollowsTheMovedTransform()
        {
            PickPhysicsSync.Apply(true);
            Assert.IsTrue(PickPhysicsSync.Enabled);
            _cube.transform.position += Vector3.up * 5f;
            Assert.IsTrue(HitsAt(5f), "the click lands on the part where it is drawn");
            Assert.IsFalse(HitsAt(0f), "nothing left at the old pose");
            _cube.transform.rotation = Quaternion.Euler(0f, 0f, 45f);
            _cube.transform.position += Vector3.up * 3f;
            Assert.IsTrue(HitsAt(8f));
        }
    }
}
