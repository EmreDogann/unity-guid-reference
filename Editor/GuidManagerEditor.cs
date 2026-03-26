using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
// Class to handle registering and accessing objects by GUID
public class GuidManagerEditor
{
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

        GuidMappings.GuidItem item = new GuidMappings.GuidItem
        {
            globalObjectID = componentGuid.IsRootComponent()
                ? componentGuid.GlobalGameObjectId
                : componentGuid.GlobalComponentId,
            guid = guid,
            ownerType = new SerializedType(componentGuid.GetOwningType())
        };

        GetMappings().Add(
            componentGuid.GlobalGameObjectId,
            componentGuid.IsRootComponent() ? "" : componentGuid.GlobalComponentId,
            item
        );
    }

    // Can only unregister/delete orphaned guids
    public static void Unregister(GuidMappings.OrphanedGuidItemInfo orphanedGuid)
    {
        GetMappings().RemoveOrphaned(orphanedGuid.TransformKey, orphanedGuid.GuidItem.guid);
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
            if (found)
            {
                GetMappings().ClearTransformOrphaned(componentGuid.GlobalGameObjectId);
            }
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

    public static IEnumerable<GuidMappings.OrphanedGuidItemInfo> GetOrphanedGuids(ComponentGuid componentGuid)
    {
        if (string.IsNullOrEmpty(componentGuid.GlobalGameObjectId))
        {
            yield break;
        }

        if (GetMappings().TryGetRecord(componentGuid.GlobalGameObjectId, out GuidMappings.GuidRecord guidRecord))
        {
            foreach (GuidMappings.GuidItem guidItem in guidRecord.orphanedGuids)
            {
                yield return new GuidMappings.OrphanedGuidItemInfo
                {
                    TransformKey = componentGuid.GlobalGameObjectId,
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
    }

    private static void OnComponentRemoved(ComponentGuid guid)
    {
        if (guid.IsRootComponent())
        {
            GetMappings().OrphanGuid(guid.GlobalGameObjectId);
        }
        else if (GetMappings()
                 .TryGetByGuid(guid.GlobalGameObjectId, guid.serializableGuid, out GuidMappings.GuidItem item))
        {
            // Component is destroyed at this point, cannot retrieve its GlobalObjectID, so we look up by guid.
            GetMappings().OrphanGuid(guid.GlobalGameObjectId, item);
        }
    }
}