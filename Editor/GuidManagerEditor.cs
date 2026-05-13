using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public class GuidManagerEditor
{
    private static bool _isPrefabStageClosing;

    private static GuidMappings GetMappings()
    {
        return GuidMappings.Instance;
    }

    private sealed class EditorMappingsHandler : GuidComponent.IGuidMappingsHandler
    {
        public void InitializeComponent(GuidComponent component)
        {
            InitializeComponent_Impl(component);
            GuidMappingsDebugWindow.RebuildWindow();
        }

        public void OrphanGuid(ComponentGuid componentGuid)
        {
            OrphanGuid_Impl(componentGuid);
            GuidMappingsDebugWindow.RebuildWindow();
        }

        public void RemoveOrphanedGuid(ComponentGuid componentGuid)
        {
            RemoveOrphanedGuid_Impl(componentGuid);
            GuidMappingsDebugWindow.RebuildWindow();
        }

        public void RemoveComponent(GuidComponent guidComponent)
        {
            RemoveComponent_Impl(guidComponent);
            GuidMappingsDebugWindow.RebuildWindow();
        }

        public bool CheckIsDuplicate(GuidComponent guidComponent)
        {
            return CheckIsDuplicate_Impl(guidComponent);
        }
    }

    private static bool CheckIsDuplicate_Impl(GuidComponent guidComponent)
    {
        return GetMappings().TryGetRecord(guidComponent.transformGuid.GlobalGameObjectId, out GuidMappings.GuidRecord record)
               && record.transformGuid.cachedComponent != null
               && record.transformGuid.cachedComponent != guidComponent.transformGuid.OwningGameObject.transform;
    }

    private static void InitializeComponent_Impl(GuidComponent component)
    {
        if (string.IsNullOrEmpty(component.transformGuid.GlobalGameObjectId))
        {
            return;
        }

        GuidMappings mappings = GetMappings();

        // Undo/redo just restored GuidComponent (the source of truth).
        // Wipe this component's slice of the mappings so the repopulation
        // below cannot be "polluted" by stale entries from before the undo.
        if (Undo.isProcessing)
        {
            mappings.RemoveRecord(component.transformGuid.GlobalGameObjectId);
        }

        if (!ProcessComponentGuid(component, component.transformGuid, mappings))
        {
            // Duplicate found, reset GuidComponent and try again.
            component.transformGuid = new ComponentGuid
            {
                OwningGameObject = component.gameObject
            };
            component.componentGuids.Clear();
            component.orphanedComponentGuids.Clear();

            ProcessComponentGuid(component, component.transformGuid, mappings);
        }

        foreach (ComponentGuid cg in component.componentGuids)
        {
            ProcessComponentGuid(component, cg, mappings);
        }

        foreach (ComponentGuid orphan in component.orphanedComponentGuids)
        {
            if (!string.IsNullOrEmpty(orphan.GlobalGameObjectId))
            {
                mappings.CacheOrphan(orphan.GlobalGameObjectId, new GuidMappings.OrphanGuidItem
                {
                    guid = orphan.serializableGuid,
                    ownerTypeReference = orphan.CachedOwnerTypeReference
                });
            }
        }

        // Always reconcile: restore entries from GuidMappings that are missing from componentGuids/orphanedComponentGuids.
        // Handles serialized data wipes from paste/revert/reset. No-op when everything is in sync.
        ReconcileComponentGuids(component);
    }

    private static bool ProcessComponentGuid(GuidComponent guidComponent, ComponentGuid componentGuid, GuidMappings mappings)
    {
        string transformKey = componentGuid.GlobalGameObjectId;
        string componentKey = componentGuid.IsRootComponent() ? "" : componentGuid.GlobalComponentId;

        if (componentGuid.serializableGuid != SerializableGuid.Empty)
        {
            // Cannot cache, already exists, this entry is a duplicate!
            if (!mappings.Cache(transformKey, componentKey,
                    CreateGuidItem(componentGuid, componentGuid.serializableGuid)))
            {
                return false;
            }

            if (!componentGuid.IsRootComponent())
            {
                mappings.RemoveOrphan(transformKey, componentGuid.serializableGuid);
            }
        }
        else
        {
            // If we don't have a cached guid, then try find in mapping file. Whether found or not, this will fill this component's guid.
            if (TryRestore(componentGuid, out Guid restored))
            {
                componentGuid.serializableGuid = SerializableGuid.Create(restored);
            }
            else
            {
                Undo.RecordObject(guidComponent, Undo.GetCurrentGroupName());
                SerializableGuid newGuid = SerializableGuid.Create(Guid.NewGuid());
                componentGuid.serializableGuid = newGuid;
                mappings.Add(transformKey, componentKey, CreateGuidItem(componentGuid, newGuid));
            }
        }

        return true;
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

    private static bool TryRestore(ComponentGuid componentGuid, out Guid guid)
    {
        guid = Guid.Empty;

        if (string.IsNullOrEmpty(componentGuid.GlobalGameObjectId))
        {
            return false;
        }

        if (componentGuid.IsRootComponent())
        {
            if (GetMappings().TryGetRecord(componentGuid.GlobalGameObjectId, out GuidMappings.GuidRecord record)
                && record.transformGuid.cachedComponent != null)
            {
                guid = record.transformGuid.guid.Guid;
                return true;
            }

            return false;
        }

        if (GetMappings().TryGetByKey(componentGuid.GlobalGameObjectId, componentGuid.GlobalComponentId,
                out GuidMappings.GuidItem guidItem))
        {
            guid = guidItem.guid.Guid;
            return true;
        }

        return false;
    }

    private static void OrphanGuid_Impl(ComponentGuid guid)
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

    private static void RemoveOrphanedGuid_Impl(ComponentGuid componentGuid)
    {
        if (string.IsNullOrEmpty(componentGuid.GlobalGameObjectId))
        {
            return;
        }

        GetMappings().RemoveOrphan(componentGuid.GlobalGameObjectId, componentGuid.serializableGuid);
    }

    private static void RemoveComponent_Impl(GuidComponent guidComponent)
    {
        // We only want to remove the Guid Mapping if the user deleted the scene component
        // (not a prefab asset, and not an automated action like scene unload, or playmode enter/exit).
        if (PrefabCheckerUtility.IsPartOfPrefabAssetOnly(guidComponent)
            || PrefabCheckerUtility.IsInPrefabStage(guidComponent)
            || _isPrefabStageClosing)
        {
            return;
        }

        if (string.IsNullOrEmpty(guidComponent.transformGuid.GlobalGameObjectId))
        {
            return;
        }

        GetMappings().RemoveRecord(guidComponent.transformGuid.GlobalGameObjectId);
    }

    // Reconciles componentGuids and orphanedComponentGuids against the GuidMappings cache, restoring
    // any entries that are present in the cache but missing from the component. This handles serialized
    // data wipes from paste/revert/reset. When everything is in sync, this is a no-op (dict lookup + two
    // empty-foreach passes with no allocations).
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

        // Assigned guid reconciliation
        List<GuidMappings.GuidItem> missingGuids = null;
        foreach (GuidMappings.GuidItem assignedGuid in record.assignedGuids)
        {
            if (!assignedGuid.cachedComponent) continue;
            if (!guidComponent.componentGuids.Exists(g => g.GlobalComponentId == assignedGuid.globalObjectID))
                (missingGuids ??= new List<GuidMappings.GuidItem>()).Add(assignedGuid);
        }

        // Orphan reconciliation
        List<GuidMappings.OrphanGuidItem> missingOrphans = null;
        foreach (GuidMappings.OrphanGuidItem orphan in record.orphanedGuids)
        {
            if (guidComponent.componentGuids.Exists(g => g.serializableGuid == orphan.guid)) continue;
            if (guidComponent.orphanedComponentGuids.Exists(g => g.serializableGuid == orphan.guid)) continue;
            (missingOrphans ??= new List<GuidMappings.OrphanGuidItem>()).Add(orphan);
        }

        // Restore missing entries
        if (missingGuids != null)
        {
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
        }

        if (missingOrphans != null)
        {
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
        }

        // Stale orphan cleanup
        // Removes from GuidMappings any orphan whose guid was adopted (moved to componentGuids).
        List<SerializableGuid> staleOrphans = null;
        foreach (GuidMappings.OrphanGuidItem orphan in record.orphanedGuids)
        {
            if (guidComponent.componentGuids.Exists(g => g.serializableGuid == orphan.guid))
                (staleOrphans ??= new List<SerializableGuid>()).Add(orphan.guid);
        }

        if (staleOrphans != null)
        {
            foreach (SerializableGuid guid in staleOrphans)
                GetMappings().RemoveOrphan(guidComponent.transformGuid.GlobalGameObjectId, guid);
        }
    }

    static GuidManagerEditor()
    {
        GuidComponent.MappingsHandler = new EditorMappingsHandler();

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
        if (instance.GetComponent<GuidComponent>() is not {} guidComponent)
        {
            GlobalObjectId gameObjectId = GlobalObjectId.GetGlobalObjectIdSlow(instance);
            GetMappings().RemoveRecord(gameObjectId.ToString());
        }
        else
        {
            // PREFAB-5: Converting a plain GameObject into a prefab asset/instance will change its GlobalObjectID.
            // So, we need to refresh stored IDs to match the new format, while keeping existing GUIDs.
            GlobalObjectId.TryParse(guidComponent.transformGuid.GlobalGameObjectId, out GlobalObjectId id);
            if (id.targetPrefabId == 0)
            {
                RefreshIds(guidComponent);
            }
        }
    }

    private static void PrefabStageClosing(PrefabStage obj)
    {
        // PREFAB-2: PrefabUtility.GetPrefabStage() doesn't work when exiting prefab stage, it returns null because
        // it calls the GuidComponent's OnDestroy function after it has cleaned-up.
        _isPrefabStageClosing = true;
        EditorApplication.delayCall += () => _isPrefabStageClosing = false;
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
    }

    // Only used when unpacking prefab instances or converting plain GameObjects into prefabs, as that will change GlobalObjectIds
    static void RefreshIds(GuidComponent component)
    {
        if (component != null)
        {
            // Get new GlobalObjectIds (bulk operation)
            var entityIdList = new List<EntityId> { component.transformGuid.OwningGameObject.GetEntityId() };
            foreach (ComponentGuid componentGuid in component.componentGuids)
                if (componentGuid.CachedComponent != null) entityIdList.Add(componentGuid.CachedComponent.GetEntityId());
            EntityId[] entityIds = entityIdList.ToArray();

            GlobalObjectId[] newGlobalObjectIds = new GlobalObjectId[entityIds.Length];
            GlobalObjectId.GetGlobalObjectIdsSlow(entityIds, newGlobalObjectIds);

            string prevGlobalGameObjectId = component.transformGuid.GlobalGameObjectId;
            string currentGlobalGameObjectId = newGlobalObjectIds[0].ToString();
            component.transformGuid.GlobalGameObjectId = currentGlobalGameObjectId;

            var globalComponentIds = new List<(string oldComponentKey, string newComponentKey)>();
            for (int index = 0; index < component.componentGuids.Count; index++)
            {
                ComponentGuid compGuid = component.componentGuids[index];
                compGuid.GlobalGameObjectId = currentGlobalGameObjectId;

                string prevComponentId = compGuid.GlobalComponentId;
                if (compGuid.CachedComponent)
                {
                    // +1 because index 0 of the new GlobalObjectIds is the transformGuid.
                    compGuid.GlobalComponentId = newGlobalObjectIds[index+1].ToString();
                    globalComponentIds.Add((prevComponentId, compGuid.GlobalComponentId));
                }
            }

            foreach (ComponentGuid orphan in component.orphanedComponentGuids)
            {
                orphan.GlobalGameObjectId = currentGlobalGameObjectId;
            }

            GetMappings().RefreshMapping(prevGlobalGameObjectId, currentGlobalGameObjectId, globalComponentIds);
        }
    }
}