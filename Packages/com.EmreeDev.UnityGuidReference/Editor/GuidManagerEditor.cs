using System;
using System.Collections.Generic;
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
            cachedComponent = componentGuid.IsRootComponent()
                ? componentGuid.OwningGameObject.transform
                : componentGuid.CachedComponent,
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

        // Clean up orphan entry if this guid was previously orphaned (handles adoption)
        if (!componentGuid.IsRootComponent())
        {
            GetMappings().RemoveOrphan(componentGuid.GlobalGameObjectId, componentGuid.serializableGuid);
        }
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
        GetMappings().AddOrphan(guid.GlobalGameObjectId, new GuidMappings.OrphanGuidItem
        {
            guid = guid.serializableGuid,
            ownerTypeReference = guid.CachedOwnerTypeReference
        });
    }

    private static void OnCachedOrphan(ComponentGuid componentGuid)
    {
        if (string.IsNullOrEmpty(componentGuid.GlobalGameObjectId))
        {
            return;
        }

        GetMappings().CacheOrphan(componentGuid.GlobalGameObjectId, new GuidMappings.OrphanGuidItem
        {
            guid = componentGuid.serializableGuid,
            ownerTypeReference = componentGuid.CachedOwnerTypeReference
        });
    }

    private static void OnOrphanRemoved(ComponentGuid componentGuid)
    {
        if (string.IsNullOrEmpty(componentGuid.GlobalGameObjectId))
        {
            return;
        }

        GetMappings().RemoveOrphan(componentGuid.GlobalGameObjectId, componentGuid.serializableGuid);
    }

    private static void OnGuidComponentDestroying(GuidComponent guidComponent)
    {
        if (string.IsNullOrEmpty(guidComponent.transformGuid.GlobalGameObjectId))
        {
            return;
        }

        GetMappings().RemoveRecord(guidComponent.transformGuid.GlobalGameObjectId);
    }

    // Reconciles componentGuids and orphanedComponentGuids against the GuidMappings cache, restoring
    // any entries that are present in the cache but missing from the component. This handles serialized
    // data wipes from paste/revert/reset. When everything is in sync, this is a no-op (early return
    // after a dict lookup).
    private static void ReconcileComponentGuids(GuidComponent guidComponent)
    {
        if (string.IsNullOrEmpty(guidComponent.transformGuid.GlobalGameObjectId))
        {
            return;
        }

        if (!GetMappings().TryGetRecord(guidComponent.transformGuid.GlobalGameObjectId,
                out GuidMappings.GuidRecord record))
        {
            return;
        }

        bool needsRestore = false;

        // --- Assigned guid reconciliation ---
        var missingGuids = new List<GuidMappings.GuidItem>();
        foreach (GuidMappings.GuidItem assignedGuid in record.assignedGuids)
        {
            if (!assignedGuid.cachedComponent)
            {
                continue;
            }

            if (!guidComponent.componentGuids.Exists(guid =>
                    guid.GlobalComponentId == assignedGuid.globalObjectID))
            {
                missingGuids.Add(assignedGuid);
            }
        }

        if (missingGuids.Count > 0)
        {
            needsRestore = true;
        }

        // --- Orphan reconciliation ---
        var missingOrphans = new List<GuidMappings.OrphanGuidItem>();
        foreach (GuidMappings.OrphanGuidItem orphan in record.orphanedGuids)
        {
            // Skip if this guid is now in componentGuids (was adopted)
            if (guidComponent.componentGuids.Exists(g => g.serializableGuid == orphan.guid))
            {
                continue;
            }

            // Skip if already in orphanedComponentGuids
            if (guidComponent.orphanedComponentGuids.Exists(g => g.serializableGuid == orphan.guid))
            {
                continue;
            }

            missingOrphans.Add(orphan);
        }

        if (missingOrphans.Count > 0)
        {
            needsRestore = true;
        }

        if (!needsRestore)
        {
            // Clean up stale orphans: remove from GuidMappings if guid was adopted
            foreach (GuidMappings.OrphanGuidItem orphan in record.orphanedGuids.ToList())
            {
                if (guidComponent.componentGuids.Exists(g => g.serializableGuid == orphan.guid))
                {
                    GetMappings().RemoveOrphan(guidComponent.transformGuid.GlobalGameObjectId, orphan.guid);
                }
            }

            return;
        }

        foreach (GuidMappings.GuidItem missing in missingGuids)
        {
            ComponentGuid componentGuid = new ComponentGuid(
                record.transformGuid.globalObjectID,
                guidComponent.gameObject,
                missing.globalObjectID,
                missing.cachedComponent);
            componentGuid.serializableGuid = missing.guid;
            guidComponent.componentGuids.Add(componentGuid);
        }

        foreach (GuidMappings.OrphanGuidItem missing in missingOrphans)
        {
            ComponentGuid orphan = new ComponentGuid(
                record.transformGuid.globalObjectID,
                guidComponent.gameObject,
                string.Empty,
                null);
            orphan.serializableGuid = missing.guid;
            orphan.SetCachedOwnerTypeReference(missing.ownerTypeReference);
            guidComponent.orphanedComponentGuids.Add(orphan);
        }

        // Clean up stale orphans: remove from GuidMappings if guid was adopted
        foreach (GuidMappings.OrphanGuidItem orphan in record.orphanedGuids.ToList())
        {
            if (guidComponent.componentGuids.Exists(g => g.serializableGuid == orphan.guid))
            {
                GetMappings().RemoveOrphan(guidComponent.transformGuid.GlobalGameObjectId, orphan.guid);
            }
        }
    }

    static GuidManagerEditor()
    {
        GuidComponent.OnGuidRequested -= TryRestoreOrCreateGuid;
        GuidComponent.OnGuidRequested += TryRestoreOrCreateGuid;

        GuidComponent.OnReconcileComponentGuids -= ReconcileComponentGuids;
        GuidComponent.OnReconcileComponentGuids += ReconcileComponentGuids;

        GuidComponent.OnCacheGuid -= OnCachedGuid;
        GuidComponent.OnCacheGuid += OnCachedGuid;

        GuidComponent.OnCacheOrphan -= OnCachedOrphan;
        GuidComponent.OnCacheOrphan += OnCachedOrphan;

        GuidComponent.OnGuidRemoved -= OnComponentRemoved;
        GuidComponent.OnGuidRemoved += OnComponentRemoved;

        GuidComponent.OnOrphanRemoved -= OnOrphanRemoved;
        GuidComponent.OnOrphanRemoved += OnOrphanRemoved;

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
                string[] prevGlobalComponentIds =
                    component.componentGuids.Select(guid => guid.GlobalComponentId).ToArray();

                component.RefreshGlobalObjectIds();

                string currentGlobalGameObjectId = component.transformGuid.GlobalGameObjectId;
                string[] currentGlobalComponentIds =
                    component.componentGuids.Select(guid => guid.GlobalComponentId).ToArray();

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