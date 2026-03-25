using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
// Class to handle registering and accessing objects by GUID
public class GuidManagerEditor
{
    // Instance data
    private static PlayModeStateChange _playModeStateChange;

    private static GuidMappings GetMappings()
    {
        return GuidMappings.Instance;
    }

    private static void Register(ComponentGuid componentGuid, SerializableGuid guid)
    {
        if (string.IsNullOrEmpty(componentGuid.GlobalGameObjectId))
        {
            return;
        }

        GuidMappings.GuidRecordQuery query = new GuidMappings.GuidRecordQuery(
            componentGuid.GlobalGameObjectId,
            componentGuid.IsRootComponent() ? "" : componentGuid.GlobalComponentId,
            SerializableGuid.Empty
        );

        GuidMappings.GuidItem item = new GuidMappings.GuidItem
        {
            globalObjectID = componentGuid.IsRootComponent()
                ? componentGuid.GlobalGameObjectId
                : componentGuid.GlobalComponentId,
            guid = guid,
            ownerType = new SerializedType(componentGuid.GetOwningType())
        };

        GetMappings().Add(query, item);
    }

    public static void Unregister(GuidMappings.OrphanedGuidItemInfo orphanedGuid)
    {
        GuidMappings.GuidRecordQuery query = new GuidMappings.GuidRecordQuery(
            orphanedGuid.TransformKey,
            "",
            orphanedGuid.GuidItem.guid
        );

        GetMappings().Remove(query, true);
    }

    private static bool TryRestore(ComponentGuid componentGuid, out Guid guid)
    {
        guid = Guid.Empty;

        if (string.IsNullOrEmpty(componentGuid.GlobalGameObjectId))
        {
            return false;
        }

        GuidMappings.GuidRecordQuery query = new GuidMappings.GuidRecordQuery(componentGuid.GlobalGameObjectId,
            componentGuid.IsRootComponent() ? "" : componentGuid.GlobalComponentId);
        if (GetMappings().TryGet(query, out _, out GuidMappings.GuidItem guidItem))
        {
            if (componentGuid.IsRootComponent())
            {
                GetMappings().ClearTransformOrphaned(componentGuid.GlobalGameObjectId);
            }

            guid = guidItem.guid.Guid;

            return true;
        }

        return false;
    }

    private static SerializableGuid TryRestoreOrCreateGuid(ComponentGuid componentGuid)
    {
        if (TryRestore(componentGuid, out Guid guid))
        {
            return SerializableGuid.Create(guid);
        }

        SerializableGuid serializableGuid = SerializableGuid.Create(Guid.NewGuid());
        Register(componentGuid, serializableGuid);
        return serializableGuid;
    }

    public static IEnumerable<GuidMappings.OrphanedGuidItemInfo> GetOrphanedGuids(ComponentGuid componentGuid)
    {
        if (string.IsNullOrEmpty(componentGuid.GlobalGameObjectId))
        {
            yield break;
        }

        GuidMappings.GuidRecordQuery query = new GuidMappings.GuidRecordQuery(componentGuid.GlobalGameObjectId);
        if (GetMappings().TryGet(query, out GuidMappings.GuidRecord guidRecord, out _))
        {
            foreach (GuidMappings.GuidItem guidItem in guidRecord.orphanedGuids)
            {
                yield return new GuidMappings.OrphanedGuidItemInfo
                {
                    TransformKey = query.TransformKey,
                    GuidItem = guidItem
                };
            }
        }
    }

    public static bool AdoptGuid(GuidMappings.GuidItem guidItem, ComponentGuid componentGuid)
    {
        if (string.IsNullOrEmpty(componentGuid.GlobalGameObjectId) ||
            guidItem.ownerType.Type != componentGuid.CachedComponent.GetType())
        {
            return false;
        }

        return GetMappings().AdoptGuid(guidItem,
            componentGuid.GlobalGameObjectId,
            componentGuid.GlobalComponentId
        );
    }

    static GuidManagerEditor()
    {
        GuidComponent.OnGuidRequested -= TryRestoreOrCreateGuid;
        GuidComponent.OnGuidRequested += TryRestoreOrCreateGuid;

        GuidComponent.OnGuidRemoved -= OnComponentRemoved;
        GuidComponent.OnGuidRemoved += OnComponentRemoved;

        // ObjectChangeEvents.changesPublished -= ChangesPublished;
        // ObjectChangeEvents.changesPublished += ChangesPublished;
        // ObjectFactory.componentWasAdded += OnComponentAdded;
    }

    private static void OnComponentRemoved(ComponentGuid guid)
    {
        GuidMappings.GuidRecordQuery query = new GuidMappings.GuidRecordQuery(
            guid.GlobalGameObjectId,
            "", // Component is destroyed at this point, cannot retrieve it's GlobalObjectID, and it's too risky to use its cached one.
            guid.IsRootComponent() ? SerializableGuid.Empty : guid.serializableGuid
        );

        if (GetMappings().TryGet(query, out GuidMappings.GuidRecord record, out GuidMappings.GuidItem item))
        {
            GetMappings().OrphanGuid(guid.GlobalGameObjectId, guid.IsRootComponent() ? null : item);
        }
    }

    private static void ChangesPublished(ref ObjectChangeEventStream stream)
    {
        for (int i = 0; i < stream.length; ++i)
        {
            ObjectChangeKind type = stream.GetEventType(i);
            switch (type)
            {
                case ObjectChangeKind.None:
                    break;
                case ObjectChangeKind.ChangeScene:
                    stream.GetChangeSceneEvent(i, out ChangeSceneEventArgs changeSceneEvent);
                    Debug.Log($"{type}: {changeSceneEvent.scene}");
                    break;
                case ObjectChangeKind.CreateGameObjectHierarchy:
                    stream.GetCreateGameObjectHierarchyEvent(i,
                        out CreateGameObjectHierarchyEventArgs createGameObjectHierarchyEvent);
                    GameObject newGameObject =
                        EditorUtility.EntityIdToObject(createGameObjectHierarchyEvent.instanceId) as GameObject;
                    Debug.Log($"{type}: {newGameObject} in scene {createGameObjectHierarchyEvent.scene}.");
                    break;
                case ObjectChangeKind.ChangeGameObjectStructureHierarchy:
                    stream.GetChangeGameObjectStructureHierarchyEvent(i,
                        out ChangeGameObjectStructureHierarchyEventArgs changeGameObjectStructureHierarchy);
                    GameObject gameObject =
                        EditorUtility.EntityIdToObject(changeGameObjectStructureHierarchy.instanceId) as GameObject;
                    Debug.Log($"{type}: {gameObject} in scene {changeGameObjectStructureHierarchy.scene}.");
                    break;
                case ObjectChangeKind.ChangeGameObjectStructure:
                    stream.GetChangeGameObjectStructureEvent(i,
                        out ChangeGameObjectStructureEventArgs changeGameObjectStructure);
                    GameObject gameObjectStructure =
                        EditorUtility.EntityIdToObject(changeGameObjectStructure.instanceId) as GameObject;
                    Debug.Log($"{type}: {gameObjectStructure} in scene {changeGameObjectStructure.scene}.");

                    // GuidComponent guidComponent = gameObjectStructure.GetComponent<GuidComponent>();
                    // if (guidComponent)
                    // {
                    //     foreach (ComponentGuid componentGuid in guidComponent.componentGUIDs)
                    //     {
                    //         if (!componentGuid.cachedComponent)
                    //         {
                    //             SetGuidState(gameObjectStructure, componentGuid.Guid,
                    //                 GuidReferenceMappings.GuidState.Orphaned);
                    //         }
                    //     }
                    // }

                    break;
                case ObjectChangeKind.ChangeGameObjectParent:
                    stream.GetChangeGameObjectParentEvent(i,
                        out ChangeGameObjectParentEventArgs changeGameObjectParent);
                    GameObject gameObjectChanged =
                        EditorUtility.EntityIdToObject(changeGameObjectParent.instanceId) as GameObject;
                    GameObject newParentGo =
                        EditorUtility.EntityIdToObject(changeGameObjectParent.newParentInstanceId) as GameObject;
                    GameObject previousParentGo =
                        EditorUtility.EntityIdToObject(changeGameObjectParent.previousParentInstanceId) as
                            GameObject;
                    Debug.Log(
                        $"{type}: {gameObjectChanged} from {previousParentGo} to {newParentGo} from scene {changeGameObjectParent.previousScene} to scene {changeGameObjectParent.newScene}.");
                    break;
                case ObjectChangeKind.ChangeGameObjectOrComponentProperties:
                    stream.GetChangeGameObjectOrComponentPropertiesEvent(i,
                        out ChangeGameObjectOrComponentPropertiesEventArgs changeGameObjectOrComponent);
                    Object goOrComponent = EditorUtility.EntityIdToObject(changeGameObjectOrComponent.instanceId);
                    if (goOrComponent is GameObject go)
                    {
                        Debug.Log(
                            $"{type}: GameObject {go} change properties in scene {changeGameObjectOrComponent.scene}.");
                    }
                    else if (goOrComponent is Component component)
                    {
                        Debug.Log(
                            $"{type}: Component {component} change properties in scene {changeGameObjectOrComponent.scene}.");
                    }

                    break;
                case ObjectChangeKind.DestroyGameObjectHierarchy:
                    stream.GetDestroyGameObjectHierarchyEvent(i,
                        out DestroyGameObjectHierarchyEventArgs destroyGameObjectHierarchyEvent);
                    // The destroyed GameObject can not be converted with EditorUtility.InstanceIDToObject as it has already been destroyed.
                    Debug.Log(GlobalObjectId.GetGlobalObjectIdSlow(destroyGameObjectHierarchyEvent.instanceId));
                    GameObject destroyParentGo =
                        EditorUtility.EntityIdToObject(destroyGameObjectHierarchyEvent
                            .parentInstanceId) as GameObject;
                    Debug.Log(
                        $"{type}: {destroyGameObjectHierarchyEvent.instanceId} with parent {destroyParentGo} in scene {destroyGameObjectHierarchyEvent.scene}.");
                    break;
                case ObjectChangeKind.CreateAssetObject:
                    stream.GetCreateAssetObjectEvent(i, out CreateAssetObjectEventArgs createAssetObjectEvent);
                    Object createdAsset = EditorUtility.EntityIdToObject(createAssetObjectEvent.instanceId);
                    string createdAssetPath = AssetDatabase.GUIDToAssetPath(createAssetObjectEvent.guid);
                    Debug.Log($"{type}: {createdAsset} at {createdAssetPath} in scene {createAssetObjectEvent.scene}.");
                    break;
                case ObjectChangeKind.DestroyAssetObject:
                    stream.GetDestroyAssetObjectEvent(i, out DestroyAssetObjectEventArgs destroyAssetObjectEvent);
                    // The destroyed asset can not be converted with EditorUtility.InstanceIDToObject as it has already been destroyed.
                    Debug.Log(
                        $"{type}: Instance Id {destroyAssetObjectEvent.instanceId} with Guid {destroyAssetObjectEvent.guid} in scene {destroyAssetObjectEvent.scene}.");
                    break;
                case ObjectChangeKind.ChangeAssetObjectProperties:
                    stream.GetChangeAssetObjectPropertiesEvent(i,
                        out ChangeAssetObjectPropertiesEventArgs changeAssetObjectPropertiesEvent);
                    Object changeAsset = EditorUtility.EntityIdToObject(changeAssetObjectPropertiesEvent.instanceId);
                    string changeAssetPath = AssetDatabase.GUIDToAssetPath(changeAssetObjectPropertiesEvent.guid);
                    Debug.Log(
                        $"{type}: {changeAsset} at {changeAssetPath} in scene {changeAssetObjectPropertiesEvent.scene}.");
                    break;
                case ObjectChangeKind.UpdatePrefabInstances:
                    stream.GetUpdatePrefabInstancesEvent(i,
                        out UpdatePrefabInstancesEventArgs updatePrefabInstancesEvent);
                    string s = "";
                    s +=
                        $"{type}: scene {updatePrefabInstancesEvent.scene}. Instances ({updatePrefabInstancesEvent.instanceIds.Length}):\n";
                    foreach (int prefabId in updatePrefabInstancesEvent.instanceIds)
                    {
                        s += EditorUtility.EntityIdToObject(prefabId) + "\n";
                    }

                    Debug.Log(s);
                    break;
                case ObjectChangeKind.ChangeChildrenOrder:
                    stream.GetChangeChildrenOrderEvent(i,
                        out ChangeChildrenOrderEventArgs changeChildrenOrderEventArgs);
                    Object changeChildren = EditorUtility.EntityIdToObject(changeChildrenOrderEventArgs.instanceId);
                    Debug.Log(
                        $"{type}: {changeChildren} in scene {changeChildrenOrderEventArgs.scene}.");
                    break;
                case ObjectChangeKind.ChangeRootOrder:
                    stream.GetChangeRootOrderEvent(i,
                        out ChangeRootOrderEventArgs changeRootOrderEventArgs);
                    Object changeRoot = EditorUtility.EntityIdToObject(changeRootOrderEventArgs.instanceId);
                    Debug.Log(
                        $"{type}: {changeRoot} in scene {changeRootOrderEventArgs.scene}.");
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}