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
        }

        /// <summary>
        ///     Creates a new GameObject, then adds GuidComponent.
        /// </summary>
        private GuidComponent CreateGuidComponent(string name = "GuidTestGO")
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
            guidComp.componentGuids.Add(new ComponentGuid
            {
                CachedComponent = component,
                OwningGameObject = guidComp.gameObject
            });
            guidComp.OnValidate();
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
        public IEnumerator TransformGuid_RemainsStable_AcrossMultipleOnValidateCalls()
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

            Assert.IsNull(go.GetComponent<GuidComponent>());

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
        public IEnumerator ComponentGuids_RemainStable_AcrossMultipleOnValidateCalls()
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

        // ---- Structural Tests ----

        [UnityTest]
        public IEnumerator GuidComponent_HasDisallowMultipleComponentAttribute()
        {
            object[] attributes =
                typeof(GuidComponent).GetCustomAttributes(typeof(DisallowMultipleComponent), true);

            Assert.IsTrue(attributes.Length > 0);

            yield return null;
        }

        [UnityTest]
        public IEnumerator TrackedComponent_MovesToOrphaned_WhenDestroyed()
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
    }
}