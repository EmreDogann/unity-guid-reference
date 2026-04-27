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
    public class GuidPrefabStabilityTests
    {
        private const string TestScenePath = "Assets/TemporaryGuidPrefabTestScene.unity";
        private const string SimplePrefabPath = "Assets/TemporarySimplePrefab.prefab";
        private const string InnerPrefabPath = "Assets/TemporaryInnerPrefab.prefab";
        private const string NestedPrefabPath = "Assets/TemporaryNestedPrefab.prefab";

        private Scene _testScene;
        private List<GameObject> _createdObjects;

        private GameObject _simplePrefab;
        private GameObject _innerPrefab;
        private GameObject _nestedPrefab;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _testScene = SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(_testScene.path))
            {
                EditorSceneManager.SaveScene(_testScene, TestScenePath);
            }

            // Create simple prefab (GuidComponent + BoxCollider)
            GameObject simpleSource = new GameObject("SimplePrefab");
            simpleSource.AddComponent<GuidComponent>();
            simpleSource.AddComponent<BoxCollider>();
            _simplePrefab = PrefabUtility.SaveAsPrefabAsset(simpleSource, SimplePrefabPath);
            Object.DestroyImmediate(simpleSource);

            // Create inner prefab for nesting
            GameObject innerSource = new GameObject("InnerPrefab");
            innerSource.AddComponent<GuidComponent>();
            innerSource.AddComponent<BoxCollider>();
            _innerPrefab = PrefabUtility.SaveAsPrefabAsset(innerSource, InnerPrefabPath);
            Object.DestroyImmediate(innerSource);

            // Create nested prefab (outer with inner as nested child)
            GameObject outerSource = new GameObject("OuterPrefab");
            outerSource.AddComponent<GuidComponent>();
            PrefabUtility.InstantiatePrefab(_innerPrefab, outerSource.transform);
            _nestedPrefab = PrefabUtility.SaveAsPrefabAsset(outerSource, NestedPrefabPath);
            Object.DestroyImmediate(outerSource);
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            AssetDatabase.DeleteAsset(SimplePrefabPath);
            AssetDatabase.DeleteAsset(NestedPrefabPath);
            AssetDatabase.DeleteAsset(InnerPrefabPath);

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

        private GameObject InstantiatePrefab(GameObject prefab)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            _createdObjects.Add(instance);
            return instance;
        }

        private void TrackComponent(GuidComponent guidComp, Component component)
        {
            guidComp.componentGuids.Add(new ComponentGuid
            {
                CachedComponent = component,
                OwningGameObject = guidComp.gameObject
            });
            guidComp.OnValidate();
        }

        // ---- Prefab Asset Tests ----

        [UnityTest]
        public IEnumerator PrefabAsset_HasEmptyGuids()
        {
            GuidComponent assetGuid = _simplePrefab.GetComponent<GuidComponent>();

            Assert.AreEqual(assetGuid.GetGuid(), Guid.Empty);

            yield return null;
        }

        [UnityTest]
        public IEnumerator NestedPrefabAsset_HasEmptyGuids_AtAllLevels()
        {
            GuidComponent outerGuid = _nestedPrefab.GetComponent<GuidComponent>();
            GuidComponent innerGuid = _nestedPrefab.transform.GetChild(0).GetComponent<GuidComponent>();

            Assert.AreEqual(outerGuid.GetGuid(), Guid.Empty);
            Assert.AreEqual(innerGuid.GetGuid(), Guid.Empty);

            yield return null;
        }

        // ---- Prefab Instance Tests ----

        [UnityTest]
        public IEnumerator PrefabInstance_HasNonEmptyGuids()
        {
            GameObject instance = InstantiatePrefab(_simplePrefab);
            GuidComponent guidComp = instance.GetComponent<GuidComponent>();

            Assert.AreNotEqual(guidComp.GetGuid(), Guid.Empty);

            yield return null;
        }

        [UnityTest]
        public IEnumerator PrefabInstances_HaveUniqueGuids()
        {
            GameObject instance1 = InstantiatePrefab(_simplePrefab);
            GameObject instance2 = InstantiatePrefab(_simplePrefab);
            GuidComponent guid1 = instance1.GetComponent<GuidComponent>();
            GuidComponent guid2 = instance2.GetComponent<GuidComponent>();

            Assert.AreNotEqual(guid1.GetGuid(), guid2.GetGuid());

            yield return null;
        }

        [UnityTest]
        public IEnumerator PrefabInstance_GuidsRemainStable_AcrossOnValidateCalls()
        {
            GameObject instance = InstantiatePrefab(_simplePrefab);
            GuidComponent guidComp = instance.GetComponent<GuidComponent>();
            BoxCollider collider = instance.GetComponent<BoxCollider>();

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

        // ---- Nested Prefab Instance Tests ----

        [UnityTest]
        public IEnumerator NestedPrefabInstance_HasNonEmptyGuids_AtAllLevels()
        {
            GameObject instance = InstantiatePrefab(_nestedPrefab);
            GuidComponent outerGuid = instance.GetComponent<GuidComponent>();
            GuidComponent innerGuid = instance.transform.GetChild(0).GetComponent<GuidComponent>();

            Assert.AreNotEqual(outerGuid.GetGuid(), Guid.Empty);
            Assert.AreNotEqual(innerGuid.GetGuid(), Guid.Empty);

            yield return null;
        }

        [UnityTest]
        public IEnumerator NestedPrefabInstance_AllGuidsAreUnique()
        {
            GameObject instance = InstantiatePrefab(_nestedPrefab);
            GuidComponent outerGuid = instance.GetComponent<GuidComponent>();
            GuidComponent innerGuid = instance.transform.GetChild(0).GetComponent<GuidComponent>();

            Assert.AreNotEqual(outerGuid.GetGuid(), innerGuid.GetGuid());

            yield return null;
        }

        [UnityTest]
        public IEnumerator NestedPrefabInstance_GuidsRemainStable_AcrossOnValidateCalls()
        {
            GameObject instance = InstantiatePrefab(_nestedPrefab);
            GuidComponent outerGuid = instance.GetComponent<GuidComponent>();
            GuidComponent innerGuid = instance.transform.GetChild(0).GetComponent<GuidComponent>();

            Guid outerTransGuid = outerGuid.GetGuid();
            Guid innerTransGuid = innerGuid.GetGuid();

            outerGuid.OnValidate();
            innerGuid.OnValidate();
            Assert.AreEqual(outerGuid.GetGuid(), outerTransGuid);
            Assert.AreEqual(innerGuid.GetGuid(), innerTransGuid);

            outerGuid.OnValidate();
            innerGuid.OnValidate();
            Assert.AreEqual(outerGuid.GetGuid(), outerTransGuid);
            Assert.AreEqual(innerGuid.GetGuid(), innerTransGuid);

            yield return null;
        }

        // ---- Prefab Unpacking Tests ----

        [UnityTest]
        public IEnumerator PrefabInstance_UnpackOutermost_PreservesGuids()
        {
            GameObject instance = InstantiatePrefab(_simplePrefab);
            GuidComponent guidComp = instance.GetComponent<GuidComponent>();
            BoxCollider collider = instance.GetComponent<BoxCollider>();

            TrackComponent(guidComp, collider);

            Guid transGuid = guidComp.GetGuid();
            Guid compGuid = guidComp.componentGuids[0].serializableGuid.Guid;

            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.OutermostRoot,
                InteractionMode.AutomatedAction);

            Assert.AreEqual(guidComp.GetGuid(), transGuid);
            Assert.AreEqual(guidComp.componentGuids[0].serializableGuid.Guid, compGuid);

            yield return null;
        }

        [UnityTest]
        public IEnumerator PrefabInstance_UnpackCompletely_PreservesGuids()
        {
            GameObject instance = InstantiatePrefab(_simplePrefab);
            GuidComponent guidComp = instance.GetComponent<GuidComponent>();
            BoxCollider collider = instance.GetComponent<BoxCollider>();

            TrackComponent(guidComp, collider);

            Guid transGuid = guidComp.GetGuid();
            Guid compGuid = guidComp.componentGuids[0].serializableGuid.Guid;

            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely,
                InteractionMode.AutomatedAction);

            Assert.AreEqual(guidComp.GetGuid(), transGuid);
            Assert.AreEqual(guidComp.componentGuids[0].serializableGuid.Guid, compGuid);

            yield return null;
        }

        [UnityTest]
        public IEnumerator NestedPrefabInstance_UnpackOutermost_PreservesAllGuids()
        {
            GameObject instance = InstantiatePrefab(_nestedPrefab);
            GuidComponent outerGuid = instance.GetComponent<GuidComponent>();
            GuidComponent innerGuid = instance.transform.GetChild(0).GetComponent<GuidComponent>();

            Guid outerTransGuid = outerGuid.GetGuid();
            Guid innerTransGuid = innerGuid.GetGuid();

            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.OutermostRoot,
                InteractionMode.AutomatedAction);

            // Outer is now a regular scene object, inner remains a prefab instance
            Assert.AreEqual(outerGuid.GetGuid(), outerTransGuid);
            Assert.AreEqual(innerGuid.GetGuid(), innerTransGuid);

            yield return null;
        }

        [UnityTest]
        public IEnumerator NestedPrefabInstance_UnpackCompletely_PreservesAllGuids()
        {
            GameObject instance = InstantiatePrefab(_nestedPrefab);
            GuidComponent outerGuid = instance.GetComponent<GuidComponent>();
            GuidComponent innerGuid = instance.transform.GetChild(0).GetComponent<GuidComponent>();

            Guid outerTransGuid = outerGuid.GetGuid();
            Guid innerTransGuid = innerGuid.GetGuid();

            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely,
                InteractionMode.AutomatedAction);

            // Both are now regular scene objects
            Assert.AreEqual(outerGuid.GetGuid(), outerTransGuid);
            Assert.AreEqual(innerGuid.GetGuid(), innerTransGuid);

            yield return null;
        }
    }
}