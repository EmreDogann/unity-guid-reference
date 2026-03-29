using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public class GuidManagerEditor
{
    private static GuidMappings GetMappings()
    {
        return GuidMappings.Instance;
    }

    private static GuidMappings.GuidItem CreateGuidItem(ComponentGuid componentGuid, SerializableGuid guid)
    {
        return new GuidMappings.GuidItem
        {
            globalObjectID = componentGuid.IsRootComponent()
                ? componentGuid.GlobalGameObjectId
                : componentGuid.GlobalComponentId,
            guid = guid
        };
    }

    private static void Register(ComponentGuid componentGuid, SerializableGuid guid)
    {
        if (string.IsNullOrEmpty(componentGuid.GlobalGameObjectId))
        {
            return;
        }

        GetMappings().Add(
            componentGuid.GlobalGameObjectId,
            componentGuid.IsRootComponent() ? "" : componentGuid.GlobalComponentId,
            CreateGuidItem(componentGuid, guid)
        );
    }

    private static void CacheMapping(ComponentGuid componentGuid, SerializableGuid guid)
    {
        if (string.IsNullOrEmpty(componentGuid.GlobalGameObjectId))
        {
            return;
        }

        GetMappings().Cache(
            componentGuid.GlobalGameObjectId,
            componentGuid.IsRootComponent() ? "" : componentGuid.GlobalComponentId,
            CreateGuidItem(componentGuid, guid)
        );
    }

    private static bool TryRestore(ComponentGuid componentGuid, out Guid guid)
    {
        guid = Guid.Empty;

        if (string.IsNullOrEmpty(componentGuid.GlobalGameObjectId))
        {
            return false;
        }

        bool found;
        GuidMappings.GuidItem guidItem;
        if (componentGuid.IsRootComponent())
        {
            found = GetMappings().TryGetRecord(componentGuid.GlobalGameObjectId, out GuidMappings.GuidRecord record);
            guidItem = found ? record.transformGuid : null;
        }
        else
        {
            found = GetMappings().TryGetByKey(componentGuid.GlobalGameObjectId, componentGuid.GlobalComponentId,
                out guidItem);
        }

        if (found && guidItem != null)
        {
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

    private static void OnCachedGuid(ComponentGuid componentGuid)
    {
        CacheMapping(componentGuid, componentGuid.serializableGuid);
    }

    private static void OnComponentRemoved(ComponentGuid guid)
    {
        if (string.IsNullOrEmpty(guid.GlobalGameObjectId))
        {
            return;
        }

        if (guid.IsRootComponent())
        {
            return;
        }

        GetMappings().RemoveComponentByGuid(guid.GlobalGameObjectId, guid.serializableGuid);
    }

    private static void OnGuidComponentDestroying(GuidComponent guidComponent)
    {
        if (string.IsNullOrEmpty(guidComponent.transformGuid.GlobalGameObjectId))
        {
            return;
        }

        GetMappings().RemoveRecord(guidComponent.transformGuid.GlobalGameObjectId);
    }

    static GuidManagerEditor()
    {
        GuidComponent.OnGuidRequested -= TryRestoreOrCreateGuid;
        GuidComponent.OnGuidRequested += TryRestoreOrCreateGuid;

        GuidComponent.OnCacheGuid -= OnCachedGuid;
        GuidComponent.OnCacheGuid += OnCachedGuid;

        GuidComponent.OnGuidRemoved -= OnComponentRemoved;
        GuidComponent.OnGuidRemoved += OnComponentRemoved;

        GuidComponent.OnGuidComponentDestroying -= OnGuidComponentDestroying;
        GuidComponent.OnGuidComponentDestroying += OnGuidComponentDestroying;

        EditorApplication.quitting -= OnEditorQuitting;
        EditorApplication.quitting += OnEditorQuitting;

        PrefabUtility.prefabInstanceUnpacked -= PrefabUnpacked;
        PrefabUtility.prefabInstanceUnpacked += PrefabUnpacked;

        PrefabUtility.prefabInstanceUpdated -= PrefabInstanceUpdated;
        PrefabUtility.prefabInstanceUpdated += PrefabInstanceUpdated;

        PrefabStage.prefabStageClosing -= PrefabStageClosing;
        PrefabStage.prefabStageClosing += PrefabStageClosing;
    }

    private static void PrefabInstanceUpdated(GameObject instance)
    {
        // PREFAB-1: Removing Components from the prefab asset will not call it's OnDestroy() function on
        // prefab instances. We need to clean up ourselves.
        if (!instance.GetComponent<GuidComponent>())
        {
            GlobalObjectId gameObjectId = GlobalObjectId.GetGlobalObjectIdSlow(instance);
            GetMappings().RemoveRecord(gameObjectId.ToString());
        }
    }

    private static void PrefabStageClosing(PrefabStage obj)
    {
        // PREFAB-2: PrefabUtility.GetPrefabStage() doesn't work when exiting prefab stage, it returns null because
        // it calls the GuidComponent's OnDestroy function after it has cleaned-up.
        Debug.Log("Prefab Stage Closing");
        GuidComponent.IsPrefabStageClosing = true;
        EditorApplication.delayCall += () => GuidComponent.IsPrefabStageClosing = false;
    }

    private static void PrefabUnpacked(GameObject unpackedGameObject, PrefabUnpackMode unpackMode)
    {
        // PREFAB-3: Unpacking a prefab instance will change its GlobalObjectId.
        // So, we need to refresh stored IDs to match the new format, while keeping existing GUIDs.

        switch (unpackMode)
        {
            case PrefabUnpackMode.OutermostRoot:
            {
                RefreshIds(unpackedGameObject.GetComponent<GuidComponent>());
                break;
            }
            case PrefabUnpackMode.Completely:
            {
                foreach (GuidComponent component in unpackedGameObject.GetComponentsInChildren<GuidComponent>(true))
                {
                    RefreshIds(component);
                }

                break;
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(unpackMode), unpackMode, null);
        }

        return;

        void RefreshIds(GuidComponent component)
        {
            if (component != null)
            {
                string prevGlobalGameObjectId = component.transformGuid.GlobalGameObjectId;
                var prevGlobalComponentIds =
                    component.componentGuids.Select(guid => guid.GlobalComponentId);

                component.RefreshGlobalObjectIds();

                string currentGlobalGameObjectId = component.transformGuid.GlobalGameObjectId;
                var currentGlobalComponentIds =
                    component.componentGuids.Select(guid => guid.GlobalComponentId);

                GetMappings().RefreshMapping(prevGlobalGameObjectId, currentGlobalGameObjectId,
                    prevGlobalComponentIds.Zip(currentGlobalComponentIds, (s, s1) => (s, s1)));
            }
        }
    }

    private static void OnEditorQuitting()
    {
        GuidComponent.IsQuitting = true;
    }
}