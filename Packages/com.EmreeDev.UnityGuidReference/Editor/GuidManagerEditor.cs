using System;
using UnityEditor;

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
    }
}