using System;
using System.Collections.Generic;
using Sherbert.Framework.Generic;
using UnityEditor;
using UnityEngine;

public sealed class GuidMappings : ScriptableObject
{
    private static GuidMappings _instance;
    public static GuidMappings Instance
    {
        get
        {
            if (_instance == null)
            {
                CreateInstance<GuidMappings>().hideFlags = HideFlags.HideAndDontSave;
            }

            return _instance;
        }
    }

    private GuidMappings()
    {
        if (_instance != null)
        {
            Debug.LogError("GuidMappings already exists. Did you query the singleton in a constructor?");
        }
        else
        {
            _instance = this;
        }
    }

    [Serializable]
    public struct GuidItem
    {
        public string globalObjectID;
        public Component cachedComponent;
        public SerializableGuid guid;
    }

    [Serializable]
    public struct OrphanGuidItem
    {
        public SerializableGuid guid;
        public string ownerTypeReference;
    }

    [Serializable]
    public class GuidRecord
    {
        public GuidItem transformGuid;           // default (cachedComponent == null) means unset
        public List<GuidItem> assignedGuids = new List<GuidItem>();
        public List<OrphanGuidItem> orphanedGuids = new List<OrphanGuidItem>();
    }

    [SerializeField]
    private SerializableDictionary<string, GuidRecord> goGlobalIdToGuidMap =
        new SerializableDictionary<string, GuidRecord>();

    internal IEnumerable<KeyValuePair<string, GuidRecord>> Records => goGlobalIdToGuidMap;

    public bool Cache(string transformKey, string componentKey, GuidItem guidItem)
    {
        if (string.IsNullOrEmpty(transformKey))
        {
            Debug.LogError("[GuidMappings] Error: transformKey must have a valid GlobalObjectID!");
            return false;
        }

        return InsertMapping(transformKey, componentKey, guidItem, false);
    }

    public bool Add(string transformKey, string componentKey, GuidItem guidItem, bool overwriteIfExists = false)
    {
        if (string.IsNullOrEmpty(transformKey))
        {
            Debug.LogError("[GuidMappings] Error: transformKey must have a valid GlobalObjectID!");
            return false;
        }

        return InsertMapping(transformKey, componentKey, guidItem, overwriteIfExists);
    }

    private bool InsertMapping(string transformKey, string componentKey, GuidItem guidItem, bool overwriteIfExists)
    {
        if (!goGlobalIdToGuidMap.TryGetValue(transformKey, out GuidRecord guidRecord))
        {
            guidRecord = new GuidRecord();
            goGlobalIdToGuidMap.Add(transformKey, guidRecord);
        }

        if (!string.IsNullOrEmpty(componentKey))
        {
            if (overwriteIfExists)
            {
                int idx = guidRecord.assignedGuids.FindIndex(g => g.globalObjectID == componentKey);
                if (idx >= 0)
                {
                    guidRecord.assignedGuids[idx] = guidItem;
                }
                else
                {
                    guidRecord.assignedGuids.Add(guidItem);
                }
            }
            else
            {
                if (!guidRecord.assignedGuids.Exists(g => g.globalObjectID == componentKey))
                {
                    guidRecord.assignedGuids.Add(guidItem);
                }
            }
        }
        else
        {
            if (guidRecord.transformGuid.cachedComponent != null && guidRecord.transformGuid.cachedComponent != guidItem.cachedComponent)
            {
                Debug.LogWarning("[GuidMappings] Duplicate GuidComponent detected!");
                return false;
            }

            guidRecord.transformGuid = guidItem;
        }

        return true;
    }

    public void RefreshMapping(string oldTransformKey, string newTransformKey,
        IEnumerable<(string oldComponentKey, string newComponentKey)> componentKeys)
    {
        if (!goGlobalIdToGuidMap.Remove(oldTransformKey, out GuidRecord guidRecord))
        {
            return;
        }

        goGlobalIdToGuidMap.Add(newTransformKey, guidRecord);
        GuidItem transformGuid = guidRecord.transformGuid;
        transformGuid.globalObjectID = newTransformKey;
        guidRecord.transformGuid = transformGuid;

        foreach ((string oldComponentKey, string newComponentKey) keys in componentKeys)
        {
            int idx = guidRecord.assignedGuids.FindIndex(g => g.globalObjectID == keys.oldComponentKey);
            if (idx >= 0)
            {
                GuidItem item = guidRecord.assignedGuids[idx];
                item.globalObjectID = keys.newComponentKey;
                guidRecord.assignedGuids[idx] = item;
            }
        }
    }

    public void RemoveComponent(string transformKey, string componentKey)
    {
        if (!goGlobalIdToGuidMap.TryGetValue(transformKey, out GuidRecord guidRecord))
        {
            return;
        }

        int idx = guidRecord.assignedGuids.FindIndex(g => g.globalObjectID == componentKey);
        if (idx >= 0)
        {
            guidRecord.assignedGuids.RemoveAt(idx);
        }
    }

    public void RemoveComponentByGuid(string transformKey, SerializableGuid componentGuid)
    {
        if (!goGlobalIdToGuidMap.TryGetValue(transformKey, out GuidRecord guidRecord))
        {
            return;
        }

        int idx = guidRecord.assignedGuids.FindIndex(g => g.guid == componentGuid);
        if (idx >= 0)
        {
            guidRecord.assignedGuids.RemoveAt(idx);
        }
    }

    public void RemoveRecord(string transformKey)
    {
        goGlobalIdToGuidMap.Remove(transformKey);
    }

    public void CacheOrphan(string transformKey, OrphanGuidItem item)
    {
        if (string.IsNullOrEmpty(transformKey))
        {
            Debug.LogError("[GuidMappings] Error: transformKey must have a valid GlobalObjectID!");
            return;
        }

        InsertOrphan(transformKey, item, false);
    }

    public void AddOrphan(string transformKey, OrphanGuidItem item)
    {
        if (string.IsNullOrEmpty(transformKey))
        {
            Debug.LogError("[GuidMappings] Error: transformKey must have a valid GlobalObjectID!");
            return;
        }

        InsertOrphan(transformKey, item, false);
    }

    private void InsertOrphan(string transformKey, OrphanGuidItem item, bool overwriteIfExists)
    {
        if (!goGlobalIdToGuidMap.TryGetValue(transformKey, out GuidRecord guidRecord))
        {
            guidRecord = new GuidRecord();
            goGlobalIdToGuidMap.Add(transformKey, guidRecord);
        }

        int idx = guidRecord.orphanedGuids.FindIndex(g => g.guid == item.guid);
        if (idx >= 0)
        {
            if (overwriteIfExists)
            {
                guidRecord.orphanedGuids[idx] = item;
            }
        }
        else
        {
            guidRecord.orphanedGuids.Add(item);
        }
    }

    public void RemoveOrphan(string transformKey, SerializableGuid orphanGuid)
    {
        if (!goGlobalIdToGuidMap.TryGetValue(transformKey, out GuidRecord guidRecord))
        {
            return;
        }

        int idx = guidRecord.orphanedGuids.FindIndex(g => g.guid == orphanGuid);
        if (idx >= 0)
        {
            guidRecord.orphanedGuids.RemoveAt(idx);
        }
    }

    public bool TryGetRecord(string transformKey, out GuidRecord guidRecord)
    {
        return goGlobalIdToGuidMap.TryGetValue(transformKey, out guidRecord);
    }

    public bool TryGetByKey(string transformKey, string componentKey, out GuidItem guidItem)
    {
        guidItem = default;
        if (!goGlobalIdToGuidMap.TryGetValue(transformKey, out GuidRecord guidRecord))
        {
            return false;
        }

        int idx = guidRecord.assignedGuids.FindIndex(g => g.globalObjectID == componentKey);
        if (idx < 0) return false;
        guidItem = guidRecord.assignedGuids[idx];
        return true;
    }

    public bool TryGetByGuid(string transformKey, SerializableGuid componentGuid, out GuidItem guidItem)
    {
        guidItem = default;
        if (!goGlobalIdToGuidMap.TryGetValue(transformKey, out GuidRecord guidRecord))
        {
            return false;
        }

        int idx = guidRecord.assignedGuids.FindIndex(g => g.guid == componentGuid);
        if (idx < 0) return false;
        guidItem = guidRecord.assignedGuids[idx];
        return true;
    }

    public void Clear()
    {
        goGlobalIdToGuidMap.Clear();
    }

    internal static void RebuildGuidMappings()
    {
        Undo.ClearUndo(_instance);
        Instance.Clear();

        foreach (GuidComponent guidComponent in FindObjectsByType<GuidComponent>(FindObjectsSortMode.None))
        {
            guidComponent.OnValidate();
        }
    }
}