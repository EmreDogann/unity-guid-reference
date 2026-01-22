using System;
using System.Collections.Generic;
using System.Linq;
using Sherbert.Framework.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
// Class to handle registering and accessing objects by GUID
public class GuidManagerEditor
{
    // Instance data
    private static readonly GuidReferenceMappings GuidReferenceMappings;

    private static GuidReferenceMappings GetOrCreateMappings()
    {
        if (GuidReferenceMappings)
        {
            return GuidReferenceMappings.GetOrCreate();
        }

        return GuidReferenceMappings;
    }

    public static void Register(GuidComponent component)
    {
        GlobalObjectId key = GlobalObjectId.GetGlobalObjectIdSlow(component.gameObject);

        GetOrCreateMappings().Set(key,
            new GuidReferenceMappings.GuidRecord
            {
                gameObjectGuid = new GuidReferenceMappings.GuidItem
                {
                    guid = SerializableGuid.Create(component.GetGuid()), state = GuidReferenceMappings.GuidState.Owned
                },
                componentGuids = new SerializableDictionary<string, GuidReferenceMappings.GuidItem>()
            });
    }

    public static void Register(ComponentGuid componentGuid)
    {
        GlobalObjectId key = GlobalObjectId.GetGlobalObjectIdSlow(componentGuid.cachedComponent.gameObject);
        GlobalObjectId componentKey = GlobalObjectId.GetGlobalObjectIdSlow(componentGuid.cachedComponent);

        if (GetOrCreateMappings().TryGet(key, out GuidReferenceMappings.GuidRecord record))
        {
            if (!record.componentGuids.TryGetValue(componentKey.ToString(), out GuidReferenceMappings.GuidItem _))
            {
                GuidReferenceMappings.GuidItem item = new GuidReferenceMappings.GuidItem
                {
                    guid = componentGuid.Guid,
                    state = GuidReferenceMappings.GuidState.Owned
                };

                GetOrCreateMappings().SetComponent(record, componentKey, item);
            }
        }
    }

    public static void Unregister(GuidComponent c)
    {
        Unregister(c.gameObject);
    }

    public static void Unregister(GameObject g)
    {
        GlobalObjectId key = GlobalObjectId.GetGlobalObjectIdSlow(g);

        GetOrCreateMappings().Remove(key);
    }

    public static void Unregister(Component c)
    {
        GlobalObjectId key = GlobalObjectId.GetGlobalObjectIdSlow(c.gameObject);
        GlobalObjectId componentKey = GlobalObjectId.GetGlobalObjectIdSlow(c);

        if (GetOrCreateMappings().TryGet(key, out GuidReferenceMappings.GuidRecord guidRecord))
        {
            guidRecord.componentGuids.Remove(componentKey);
        }
    }

    public static void Unregister(GameObject owningGO, ComponentGuid c)
    {
        GlobalObjectId key = GlobalObjectId.GetGlobalObjectIdSlow(owningGO);

        if (GetOrCreateMappings().TryGet(key, out GuidReferenceMappings.GuidRecord guidRecord))
        {
            var componentKey = guidRecord.componentGuids.First(pair => pair.Value.guid == c.Guid);
            guidRecord.componentGuids.Remove(componentKey.Key);
        }
    }

    public static bool TryRestore(GameObject g, out Guid guid)
    {
        GlobalObjectId key = GlobalObjectId.GetGlobalObjectIdSlow(g);
        // GlobalObjectID.identifierType 2 = Scene Object
        if (key.identifierType != 2)
        {
            guid = Guid.Empty;
            return false;
        }

        Debug.Log(key);

        if (GetOrCreateMappings().TryGet(key, out GuidReferenceMappings.GuidRecord guidRecord))
        {
            guid = guidRecord.gameObjectGuid.guid.Guid;
            return true;
        }

        guid = Guid.NewGuid();
        return false;
    }

    public static bool TryRestore(Component c, out Guid guid)
    {
        GlobalObjectId key = GlobalObjectId.GetGlobalObjectIdSlow(c.gameObject);
        // GlobalObjectID.identifierType 2 = Scene Object
        if (key.identifierType != 2)
        {
            guid = Guid.NewGuid();
            return false;
        }

        GlobalObjectId componentKey = GlobalObjectId.GetGlobalObjectIdSlow(c);

        if (GetOrCreateMappings().TryGet(key, out GuidReferenceMappings.GuidRecord guidRecord))
        {
            bool found =
                guidRecord.componentGuids.TryGetValue(componentKey.ToString(),
                    out GuidReferenceMappings.GuidItem guidItem);
            if (found)
            {
                guidItem.state = GuidReferenceMappings.GuidState.Owned;
                guid = guidItem.guid.Guid;
            }
            else
            {
                guid = Guid.NewGuid();
            }

            return found;
        }

        guid = Guid.NewGuid();
        return false;
    }

    private static void TryRestoreOrCreate(GuidComponent guidComponent)
    {
        if (TryRestore(guidComponent.gameObject, out Guid guid))
        {
            guidComponent._guid = SerializableGuid.Create(guid);
        }
        else if (guid != Guid.Empty)
        {
            guidComponent._guid = SerializableGuid.Create(guid);
            Register(guidComponent);
        }
        else
        {
            guidComponent._guid = SerializableGuid.Empty;
        }
    }

    private static void TryRestoreOrCreateComponent(GuidComponent guidComponent, ComponentGuid componentGuid)
    {
        if (TryRestore(componentGuid.cachedComponent, out Guid guid))
        {
            componentGuid.Guid = SerializableGuid.Create(guid);
        }
        else if (guid != Guid.Empty)
        {
            componentGuid.Guid = SerializableGuid.Create(guid);
            Register(componentGuid);
        }
        else
        {
            componentGuid.Guid = SerializableGuid.Empty;
        }
    }

    public static IEnumerable<SerializableGuid> GetOrphanedGuids(GuidComponent guidComponent)
    {
        var serializableGuids = new List<SerializableGuid>();
        GlobalObjectId key = GlobalObjectId.GetGlobalObjectIdSlow(guidComponent.gameObject);
        // GlobalObjectID.identifierType 2 = Scene Object
        if (key.identifierType != 2)
        {
            return null;
        }

        if (GetOrCreateMappings().TryGet(key, out GuidReferenceMappings.GuidRecord guidRecord))
        {
            foreach (GuidReferenceMappings.GuidItem guidItem in guidRecord.componentGuids.Values)
            {
                if (guidItem.state == GuidReferenceMappings.GuidState.Orphaned)
                {
                    serializableGuids.Add(guidItem.guid);
                }
            }
        }

        return serializableGuids;
    }

    public static void SetGuidState(GameObject gameObject, SerializableGuid guid, GuidReferenceMappings.GuidState state)
    {
        GlobalObjectId key = GlobalObjectId.GetGlobalObjectIdSlow(gameObject);
        // GlobalObjectID.identifierType 2 = Scene Object
        if (key.identifierType != 2)
        {
            return;
        }

        if (GetOrCreateMappings().TryGet(key, out GuidReferenceMappings.GuidRecord guidRecord))
        {
            foreach (GuidReferenceMappings.GuidItem guidItem in guidRecord.componentGuids.Values)
            {
                if (guidItem.guid == guid)
                {
                    GetOrCreateMappings().SetState(guidItem, state);
                    break;
                }
            }
        }
    }

    static GuidManagerEditor()
    {
        GuidReferenceMappings = GuidReferenceMappings.GetOrCreate();
        GuidComponent.OnGameObjectGuidRequested -= TryRestoreOrCreate;
        GuidComponent.OnGameObjectGuidRequested += TryRestoreOrCreate;

        GuidComponent.OnComponentGuidRequested -= TryRestoreOrCreateComponent;
        GuidComponent.OnComponentGuidRequested += TryRestoreOrCreateComponent;

        GuidComponent.OnComponentGuidRemoved -= OnComponentRemoved;
        GuidComponent.OnComponentGuidRemoved += OnComponentRemoved;

        ObjectChangeEvents.changesPublished -= ChangesPublished;
        ObjectChangeEvents.changesPublished += ChangesPublished;
        // ObjectFactory.componentWasAdded += OnComponentAdded;
    }

    private static void OnComponentRemoved(GuidComponent parentComponent, ComponentGuid guid)
    {
        SetGuidState(parentComponent.gameObject, guid.Guid, GuidReferenceMappings.GuidState.Orphaned);
    }

    private static void ChangesPublished(ref ObjectChangeEventStream stream)
    {
        var objectsNeedingUpdate = new List<Object>();

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