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
    public class GuidItem
    {
        public string globalObjectID;
        public Component cachedComponent;
        public SerializableGuid guid;
    }

    [Serializable]
    public class OrphanGuidItem
    {
        public SerializableGuid guid;
        public string ownerTypeReference;
    }

    [Serializable]
    public class GuidRecord
    {
        public GuidItem transformGuid;
        public List<GuidItem> assignedGuids = new List<GuidItem>();
        public List<OrphanGuidItem> orphanedGuids = new List<OrphanGuidItem>();
    }

    [SerializeField]
    private SerializableDictionary<string, GuidRecord> goGlobalIdToGuidMap =
        new SerializableDictionary<string, GuidRecord>();

    internal IEnumerable<KeyValuePair<string, GuidRecord>> Records => goGlobalIdToGuidMap;

    public void Cache(string transformKey, string componentKey, GuidItem guidItem)
    {
        if (string.IsNullOrEmpty(transformKey))
        {
            Debug.LogError("[GuidMappings] Error: transformKey must have a valid GlobalObjectID!");
            return;
        }

        Undo.RecordObject(this, "Cache GUID Mapping");
        InsertMapping(transformKey, componentKey, guidItem, false);
    }

    public void Add(string transformKey, string componentKey, GuidItem guidItem, bool overwriteIfExists = false)
    {
        if (string.IsNullOrEmpty(transformKey))
        {
            Debug.LogError("[GuidMappings] Error: transformKey must have a valid GlobalObjectID!");
            return;
        }

        Undo.RecordObject(this, "Added GUID Mapping");
        InsertMapping(transformKey, componentKey, guidItem, overwriteIfExists);
    }

    private void InsertMapping(string transformKey, string componentKey, GuidItem guidItem, bool overwriteIfExists)
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
            guidRecord.transformGuid = guidItem;
        }
    }

    public void RefreshMapping(string oldTransformKey, string newTransformKey,
        IEnumerable<(string oldComponentKey, string newComponentKey)> componentKeys)
    {
        // We want this Undo record to be invisible, as this is a side effect of something like prefab unpacking.
        Undo.RecordObject(this, Undo.GetCurrentGroupName());

        if (!goGlobalIdToGuidMap.Remove(oldTransformKey, out GuidRecord guidRecord))
        {
            return;
        }

        goGlobalIdToGuidMap.Add(newTransformKey, guidRecord);
        guidRecord.transformGuid.globalObjectID = newTransformKey;

        foreach ((string oldComponentKey, string newComponentKey) keys in componentKeys)
        {
            GuidItem componentGuidItem =
                guidRecord.assignedGuids.Find(g => g.globalObjectID == keys.oldComponentKey);
            if (componentGuidItem != null)
            {
                componentGuidItem.globalObjectID = keys.newComponentKey;
            }
        }
    }

    public void RemoveComponent(string transformKey, string componentKey)
    {
        if (!goGlobalIdToGuidMap.TryGetValue(transformKey, out GuidRecord guidRecord))
        {
            return;
        }

        Undo.RecordObject(this, "Remove GUID Mapping");

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

        Undo.RecordObject(this, "Remove GUID Mapping");

        int idx = guidRecord.assignedGuids.FindIndex(g => g.guid == componentGuid);
        if (idx >= 0)
        {
            guidRecord.assignedGuids.RemoveAt(idx);
        }
    }

    public void RemoveRecord(string transformKey)
    {
        if (!goGlobalIdToGuidMap.ContainsKey(transformKey))
        {
            return;
        }

        Undo.RecordObject(this, "Remove GUID Mapping");
        goGlobalIdToGuidMap.Remove(transformKey);
    }

    public void CacheOrphan(string transformKey, OrphanGuidItem item)
    {
        if (string.IsNullOrEmpty(transformKey))
        {
            Debug.LogError("[GuidMappings] Error: transformKey must have a valid GlobalObjectID!");
            return;
        }

        Undo.RecordObject(this, "Cache Orphan GUID Mapping");
        InsertOrphan(transformKey, item, false);
    }

    public void AddOrphan(string transformKey, OrphanGuidItem item)
    {
        if (string.IsNullOrEmpty(transformKey))
        {
            Debug.LogError("[GuidMappings] Error: transformKey must have a valid GlobalObjectID!");
            return;
        }

        Undo.RecordObject(this, "Added Orphan GUID Mapping");
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
            Undo.RecordObject(this, "Remove Orphan GUID Mapping");
            guidRecord.orphanedGuids.RemoveAt(idx);
        }
    }

    public bool TryGetRecord(string transformKey, out GuidRecord guidRecord)
    {
        return goGlobalIdToGuidMap.TryGetValue(transformKey, out guidRecord);
    }

    public bool TryGetByKey(string transformKey, string componentKey, out GuidItem guidItem)
    {
        guidItem = null;
        if (!goGlobalIdToGuidMap.TryGetValue(transformKey, out GuidRecord guidRecord))
        {
            return false;
        }

        guidItem = guidRecord.assignedGuids.Find(g => g.globalObjectID == componentKey);
        return guidItem != null;
    }

    public bool TryGetByGuid(string transformKey, SerializableGuid componentGuid, out GuidItem guidItem)
    {
        guidItem = null;
        if (!goGlobalIdToGuidMap.TryGetValue(transformKey, out GuidRecord guidRecord))
        {
            return false;
        }

        guidItem = guidRecord.assignedGuids.Find(g => g.guid == componentGuid);
        return guidItem != null;
    }

    public void Clear()
    {
        goGlobalIdToGuidMap.Clear();
    }

    private void OnEnable()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private void OnDisable()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
    }

    private void OnPlayModeStateChanged(PlayModeStateChange stateChange)
    {
        switch (stateChange)
        {
            case PlayModeStateChange.ExitingPlayMode:
                Clear();
                break;
        }
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