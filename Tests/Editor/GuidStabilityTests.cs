using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Assert = UnityEngine.Assertions.Assert;
using Object = UnityEngine.Object;

namespace Tests.Editor
{
    public class GuidStabilityTests
    {
        private const string TestScenePath = "Assets/TemporaryGuidStabilityTestScene.unity";
        private const string GuidGameObjectName = "GuidTestGO";

        private Scene _testScene;
        private List<GameObject> _createdObjects;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _testScene = SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(_testScene.path))
            {
                EditorSceneManager.SaveScene(_testScene, TestScenePath);
            }
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            if (_testScene.path == TestScenePath)
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                AssetDatabase.DeleteAsset(TestScenePath);
            }
        }

        [SetUp]
        public void SetUp()
        {
            _createdObjects = new List<GameObject>();
            Undo.ClearAll();
            GuidMappings.Instance.Clear();

            EditorStepForwardToolbarButton.ShowButton();
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = _createdObjects.Count - 1; i >= 0; i--)
            {
                if (_createdObjects[i] != null)
                {
                    Object.DestroyImmediate(_createdObjects[i]);
                }
            }

            _createdObjects.Clear();

            EditorStepForwardToolbarButton.HideButton();
        }

        private static bool WaitForStepForward()
        {
            return EditorStepForwardToolbarButton.ConsumeStep();
        }

        /// <summary>
        ///     Creates a new GameObject, then adds GuidComponent.
        /// </summary>
        private GuidComponent CreateGuidComponent(string name = GuidGameObjectName)
        {
            GameObject go = new GameObject(name);
            _createdObjects.Add(go);

            Undo.RegisterCreatedObjectUndo(go, "Created GuidTestGO GameObject");
            Undo.IncrementCurrentGroup();

            GuidComponent component = Undo.AddComponent<GuidComponent>(go);
            Undo.IncrementCurrentGroup();

            return component;
        }

        /// <summary>
        ///     Simulates the GuidComponentDrawer "assign GUID" button.
        /// </summary>
        private void TrackComponent(GuidComponent guidComp, Component component)
        {
            Undo.RecordObject(guidComp, "Track Component");

            guidComp.componentGuids.Add(new ComponentGuid
            {
                CachedComponent = component,
                OwningGameObject = guidComp.gameObject
            });
            guidComp.OnValidate();

            Undo.IncrementCurrentGroup();
        }

        /// <summary>
        ///     Simulates the GuidComponentDrawer "Orphan GUID" button.
        /// </summary>
        private void UntrackComponent(GuidComponent guidComp, ComponentGuid componentGuid)
        {
            Undo.RecordObject(guidComp, "Untrack Component");

            guidComp.orphanedComponentGuids.Add(componentGuid);
            guidComp.componentGuids.Remove(componentGuid);
            guidComp.NotifyGuidRemoved(componentGuid);

            Undo.IncrementCurrentGroup();
        }

        /// <summary>
        ///     Simulates the GuidComponentDrawer "Remove Orphaned GUID" button.
        /// </summary>
        private void RemoveOrphanedGuid(GuidComponent guidComp, ComponentGuid componentGuid)
        {
            Undo.RecordObject(guidComp, "Remove Orphaned Component");

            guidComp.NotifyOrphanRemoved(componentGuid);
            guidComp.orphanedComponentGuids.Remove(componentGuid);

            Undo.IncrementCurrentGroup();
        }

        // ---- TransformGuid Tests ----

        [UnityTest]
        public IEnumerator TransformGuid_IsNotEmpty_OnCreation()
        {
            GuidComponent guid = CreateGuidComponent();

            Assert.AreNotEqual(guid.GetGuid(), Guid.Empty);

            yield return null;
        }

        [UnityTest]
        public IEnumerator TransformGuid_IsUnique_AcrossGameObjects()
        {
            GuidComponent guid1 = CreateGuidComponent("GO1");
            GuidComponent guid2 = CreateGuidComponent("GO2");

            Assert.AreNotEqual(guid1.GetGuid(), guid2.GetGuid());

            yield return null;
        }

        [UnityTest]
        public IEnumerator TransformGuid_IsStable_AcrossMultipleOnValidateCalls()
        {
            GuidComponent guid = CreateGuidComponent();
            Guid firstGuid = guid.GetGuid();

            guid.OnValidate();
            Assert.AreEqual(guid.GetGuid(), firstGuid);

            guid.OnValidate();
            Assert.AreEqual(guid.GetGuid(), firstGuid);

            yield return null;
        }

        [UnityTest]
        public IEnumerator TransformGuid_IsNew_AfterManualReAdd()
        {
            GuidComponent guid = CreateGuidComponent();
            Guid originalGuid = guid.GetGuid();
            GameObject go = guid.gameObject;

            Object.DestroyImmediate(guid);
            GuidComponent newGuid = go.AddComponent<GuidComponent>();

            Assert.AreNotEqual(newGuid.GetGuid(), Guid.Empty);
            Assert.AreNotEqual(newGuid.GetGuid(), originalGuid);

            yield return null;
        }

        [UnityTest]
        public IEnumerator TransformGuid_IsRestored_AfterUndoRemoval()
        {
            GuidComponent guid = CreateGuidComponent();
            Guid originalGuid = guid.GetGuid();
            GameObject go = guid.gameObject;

            Undo.DestroyObjectImmediate(guid);
            Undo.IncrementCurrentGroup();
            Undo.PerformUndo();

            GuidComponent restored = go.GetComponent<GuidComponent>();
            Assert.IsNotNull(restored);
            Assert.AreEqual(restored.GetGuid(), originalGuid);

            yield return null;
        }

        [UnityTest]
        public IEnumerator TransformGuid_IsNew_AfterGameObjectDuplication()
        {
            GuidComponent original = CreateGuidComponent();
            Guid originalGuid = original.GetGuid();

            // Doesn't call GuidComponent.OnValidate() function.
            GameObject clone = Object.Instantiate(original.gameObject);
            _createdObjects.Add(clone);
            GuidComponent cloneGuid = clone.GetComponent<GuidComponent>();

            Assert.IsNotNull(cloneGuid);
            Assert.AreNotEqual(cloneGuid.GetGuid(), Guid.Empty);
            Assert.AreNotEqual(cloneGuid.GetGuid(), originalGuid);
            Assert.AreEqual(original.GetGuid(), originalGuid);

            yield return null;
        }

        [UnityTest]
        public IEnumerator TransformGuid_IsStable_EnteringPlayMode()
        {
            // Edit Mode: Create Test GO
            Guid originalGuid = CreateGuidComponent(GuidGameObjectName).GetGuid();
            // Need to use some form of storage so local data lives past domain reload in EnterPlayMode().
            SessionState.SetString("guid", originalGuid.ToString());

            yield return new EnterPlayMode();

            // Restore Guid from session storage
            originalGuid = new Guid(SessionState.GetString("guid", ""));
            SessionState.EraseString("guid");

            // Play Mode: Find test GO in case reference lost due to domain reload.
            GameObject originalGO = GameObject.Find(GuidGameObjectName);
            Assert.IsNotNull(originalGO);

            GuidComponent originalPlayMode = originalGO.GetComponent<GuidComponent>();

            Assert.IsNotNull(originalPlayMode);
            Assert.AreEqual(originalPlayMode.GetGuid(), originalGuid);

            yield return new ExitPlayMode();

            // Re-add test game object to make sure it gets cleaned-up.
            GameObject editModeGO = GameObject.Find(GuidGameObjectName);
            _createdObjects.Add(editModeGO);

            yield return null;
        }

        [UnityTest]
        public IEnumerator TransformGuid_IsStable_ExitingPlayMode()
        {
            // Edit Mode: Create Test GO
            Guid originalGuid = CreateGuidComponent(GuidGameObjectName).GetGuid();
            // Need to use some form of storage so local data lives past domain reload in EnterPlayMode().
            SessionState.SetString("guid", originalGuid.ToString());

            yield return new EnterPlayMode();
            yield return new ExitPlayMode();

            // Restore Guid from session storage
            originalGuid = new Guid(SessionState.GetString("guid", ""));
            SessionState.EraseString("guid");

            // Edit Mode: Find test GO again in case reference lost due to domain reload.
            GameObject editModeGO = GameObject.Find(GuidGameObjectName);
            Assert.IsNotNull(editModeGO, "Cannot find Test GameObject!");

            // Re-add test game object to make sure it gets cleaned-up.
            _createdObjects.Add(editModeGO);

            GuidComponent editModeGuidComponent = editModeGO.GetComponent<GuidComponent>();
            Assert.IsNotNull(editModeGuidComponent, "Cannot find Test GameObject's Guid Component!");
            Assert.AreEqual(editModeGuidComponent.GetGuid(), originalGuid, "Guid lost after entering and exiting playmode!");

            yield return null;
        }

        // ---- ComponentGuid Tests ----

        [UnityTest]
        public IEnumerator ComponentGuid_IsAssigned_WhenTracked()
        {
            GuidComponent guidComp = CreateGuidComponent();
            BoxCollider collider = guidComp.gameObject.AddComponent<BoxCollider>();

            TrackComponent(guidComp, collider);

            Assert.AreEqual(guidComp.componentGuids.Count, 1);
            Assert.AreNotEqual(guidComp.componentGuids[0].serializableGuid, SerializableGuid.Empty);

            yield return null;
        }

        [UnityTest]
        public IEnumerator ComponentGuids_AreUnique_FromEachOtherAndTransformGuid()
        {
            GuidComponent guidComp = CreateGuidComponent();
            BoxCollider collider = guidComp.gameObject.AddComponent<BoxCollider>();
            Rigidbody rb = guidComp.gameObject.AddComponent<Rigidbody>();

            TrackComponent(guidComp, collider);
            TrackComponent(guidComp, rb);

            Guid transformGuid = guidComp.GetGuid();
            Guid colliderGuid = guidComp.componentGuids[0].serializableGuid.Guid;
            Guid rbGuid = guidComp.componentGuids[1].serializableGuid.Guid;

            Assert.AreNotEqual(colliderGuid, Guid.Empty);
            Assert.AreNotEqual(rbGuid, Guid.Empty);
            Assert.AreNotEqual(colliderGuid, rbGuid);
            Assert.AreNotEqual(colliderGuid, transformGuid);
            Assert.AreNotEqual(rbGuid, transformGuid);

            yield return null;
        }

        [UnityTest]
        public IEnumerator ComponentGuids_IsStable_AcrossMultipleOnValidateCalls()
        {
            GuidComponent guidComp = CreateGuidComponent();
            BoxCollider collider = guidComp.gameObject.AddComponent<BoxCollider>();

            TrackComponent(guidComp, collider);

            Guid transGuid = guidComp.GetGuid();
            Guid compGuid = guidComp.componentGuids[0].serializableGuid.Guid;

            guidComp.OnValidate();
            Assert.AreEqual(guidComp.GetGuid(), transGuid);
            Assert.AreEqual(guidComp.componentGuids[0].serializableGuid.Guid, compGuid);

            guidComp.OnValidate();
            Assert.AreEqual(guidComp.GetGuid(), transGuid);
            Assert.AreEqual(guidComp.componentGuids[0].serializableGuid.Guid, compGuid);

            yield return null;
        }

        [UnityTest]
        public IEnumerator ComponentGuids_AreRestored_AfterUndoRemoval()
        {
            GuidComponent guidComp = CreateGuidComponent();
            BoxCollider collider = guidComp.gameObject.AddComponent<BoxCollider>();

            TrackComponent(guidComp, collider);

            Guid transGuid = guidComp.GetGuid();
            Guid compGuid = guidComp.componentGuids[0].serializableGuid.Guid;
            GameObject go = guidComp.gameObject;

            Undo.DestroyObjectImmediate(guidComp);
            Undo.IncrementCurrentGroup();
            Undo.PerformUndo();

            GuidComponent restored = go.GetComponent<GuidComponent>();

            Assert.IsNotNull(restored);
            Assert.AreEqual(restored.GetGuid(), transGuid);
            Assert.AreEqual(restored.componentGuids.Count, 1);
            Assert.AreEqual(restored.componentGuids[0].serializableGuid.Guid, compGuid);

            yield return null;
        }

        [UnityTest]
        public IEnumerator ComponentGuids_AreCleared_AfterGameObjectDuplication()
        {
            GuidComponent guidComp = CreateGuidComponent();
            BoxCollider collider = guidComp.gameObject.AddComponent<BoxCollider>();

            TrackComponent(guidComp, collider);

            Guid origTransGuid = guidComp.GetGuid();
            Guid origCompGuid = guidComp.componentGuids[0].serializableGuid.Guid;

            GameObject clone = Object.Instantiate(guidComp.gameObject);
            _createdObjects.Add(clone);

            GuidComponent cloneGuid = clone.GetComponent<GuidComponent>();

            // Clone gets new transformGuid but componentGuids are cleared by duplication detection
            Assert.AreNotEqual(cloneGuid.GetGuid(), Guid.Empty);
            Assert.AreNotEqual(cloneGuid.GetGuid(), origTransGuid);
            Assert.AreEqual(cloneGuid.componentGuids.Count, 0);

            // Original is unaffected
            Assert.AreEqual(guidComp.GetGuid(), origTransGuid);
            Assert.AreEqual(guidComp.componentGuids.Count, 1);
            Assert.AreEqual(guidComp.componentGuids[0].serializableGuid.Guid, origCompGuid);

            yield return null;
        }

        [UnityTest]
        public IEnumerator ComponentGuid_IsOrphaned_WhenTrackedComponentDestroyed()
        {
            GuidComponent guidComp = CreateGuidComponent();
            BoxCollider collider = guidComp.gameObject.AddComponent<BoxCollider>();

            TrackComponent(guidComp, collider);

            Guid compGuid = guidComp.componentGuids[0].serializableGuid.Guid;

            Assert.AreEqual(guidComp.componentGuids.Count, 1);
            Assert.AreEqual(guidComp.orphanedComponentGuids.Count, 0);

            Object.DestroyImmediate(collider);
            guidComp.OnValidate();

            Assert.AreEqual(guidComp.componentGuids.Count, 0);
            Assert.AreEqual(guidComp.orphanedComponentGuids.Count, 1);
            Assert.AreEqual(guidComp.orphanedComponentGuids[0].serializableGuid.Guid, compGuid);

            yield return null;
        }

        [UnityTest]
        public IEnumerator ComponentGuid_IsStable_WhenUndoAndRedo()
        {
            GuidComponent guidComp = CreateGuidComponent();
            SphereCollider sphereCollider = guidComp.gameObject.AddComponent<SphereCollider>();
            BoxCollider boxCollider = guidComp.gameObject.AddComponent<BoxCollider>();
            Rigidbody rb = guidComp.gameObject.AddComponent<Rigidbody>();

            TrackComponent(guidComp, sphereCollider);
            TrackComponent(guidComp, boxCollider);
            TrackComponent(guidComp, rb);

            Guid originalSphereColliderGuid = guidComp.componentGuids[0].serializableGuid.Guid;
            Guid originalBoxColliderGuid = guidComp.componentGuids[1].serializableGuid.Guid;
            Guid originalRigidbodyGuid = guidComp.componentGuids[2].serializableGuid.Guid;

            UntrackComponent(guidComp, guidComp.componentGuids[1]); // Untrack Box Collider.
            UntrackComponent(guidComp, guidComp.componentGuids[1]); // Untrack Rigidbody (index is correct here as the array shrank).
            RemoveOrphanedGuid(guidComp, guidComp.orphanedComponentGuids[1]); // Remove Rigidbody orphaned guid.

            Undo.PerformUndo();
            Undo.PerformUndo();
            Undo.PerformUndo();

            Guid sphereColliderGuid = guidComp.componentGuids[0].serializableGuid.Guid;
            Guid boxColliderGuid = guidComp.componentGuids[1].serializableGuid.Guid;
            Guid rbGuid = guidComp.componentGuids[2].serializableGuid.Guid;

            Assert.AreEqual(sphereColliderGuid, originalSphereColliderGuid);
            Assert.AreEqual(boxColliderGuid, originalBoxColliderGuid);
            Assert.AreEqual(rbGuid, originalRigidbodyGuid);

            yield return null;
        }

        // ---- Structural Tests ----

        [UnityTest]
        public IEnumerator GuidComponent_HasDisallowMultipleComponentAttribute()
        {
            object[] attributes =
                typeof(GuidComponent).GetCustomAttributes(typeof(DisallowMultipleComponent), true);

            Assert.IsTrue(attributes.Length > 0);

            yield return null;
        }
    }
}