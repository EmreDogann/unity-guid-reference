using System;
using System.Collections.Generic;
using System.IO;
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
                LoadOrCreate();
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
        public SerializedType ownerType;
        public SerializableGuid guid;
    }

    public class OrphanedGuidItemInfo
    {
        public string TransformKey;
        public GuidItem GuidItem;
    }

    [Serializable]
    public class GuidRecord
    {
        public bool transformOrphaned;
        public GuidItem transformGuid;
        public List<GuidItem> assignedGuids = new List<GuidItem>();
        public List<GuidItem> orphanedGuids = new List<GuidItem>();
    }

    private static readonly string OrphanedGuidObjectId = new GlobalObjectId().ToString();
    private static string _path;

    public static string AssetPath
    {
        get
        {
            if (_path == null)
            {
                _path = Path.Combine(Application.persistentDataPath, "GuidMappings.json");
            }

            return _path;
        }
    }

    [SerializeField]
    private SerializableDictionary<string, GuidRecord> goGlobalIdToGuidMap =
        new SerializableDictionary<string, GuidRecord>();

    public void Add(string transformKey, string componentKey, GuidItem guidItem, bool overwriteIfExists = false)
    {
        if (string.IsNullOrEmpty(transformKey))
        {
            Debug.LogError("[GuidReferenceMappings] Error: transformKey must have a valid GlobalObjectID!");
            return;
        }

        Undo.RecordObject(this, "Added GUID Mapping");

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

                Save();
            }
            else
            {
                if (!guidRecord.assignedGuids.Exists(g => g.globalObjectID == componentKey))
                {
                    guidRecord.assignedGuids.Add(guidItem);
                    Save();
                }
            }
        }
        else
        {
            guidRecord.transformGuid = guidItem;
            Save();
        }
    }

    internal void ClearTransformOrphaned(string transformKey)
    {
        if (goGlobalIdToGuidMap.TryGetValue(transformKey, out GuidRecord guidRecord) && guidRecord.transformOrphaned)
        {
            Undo.RecordObject(this, "Restoring Transform Guid");
            guidRecord.transformOrphaned = false;

            Save();
        }
    }

    public void OrphanGuid(string transformGuidId, GuidItem item = null)
    {
        if (transformGuidId == OrphanedGuidObjectId)
        {
            return;
        }

        Undo.RecordObject(this, "Orphaning Guid");

        if (goGlobalIdToGuidMap.TryGetValue(transformGuidId, out GuidRecord guidRecord))
        {
            if (item != null)
            {
                int componentIndex = guidRecord.assignedGuids.FindIndex(g => g.guid == item.guid);
                if (componentIndex >= 0)
                {
                    guidRecord.assignedGuids.RemoveAt(componentIndex);
                    guidRecord.orphanedGuids.Add(item);

                    Save();
                }
            }
            else
            {
                guidRecord.transformOrphaned = true;
                Save();
            }
        }
    }

    internal bool AdoptGuid(GuidItem guidItem, string transformGlobalObjectId, string componentGlobalObjectId)
    {
        Undo.RecordObject(this, "Adopting Guid");

        if (goGlobalIdToGuidMap.TryGetValue(transformGlobalObjectId, out GuidRecord guidRecord))
        {
            if (guidRecord.orphanedGuids.Contains(guidItem))
            {
                guidRecord.orphanedGuids.Remove(guidItem);
                guidItem.globalObjectID = componentGlobalObjectId;
                guidRecord.assignedGuids.Add(guidItem);

                Save();
                return true;
            }
        }

        return false;
    }

    public void RemoveOrphaned(string transformKey, SerializableGuid componentGuid)
    {
        if (!goGlobalIdToGuidMap.TryGetValue(transformKey, out GuidRecord guidRecord))
        {
            return;
        }

        Undo.RecordObject(this, "Remove GUID Mapping");

        for (int i = guidRecord.orphanedGuids.Count - 1; i >= 0; i--)
        {
            if (guidRecord.orphanedGuids[i].guid == componentGuid)
            {
                guidRecord.orphanedGuids.RemoveAt(i);

                Save();
                return;
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

            Save();
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

            Save();
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

        Save();
    }

    public bool Contains(string transformKey, string componentKey = "")
    {
        if (string.IsNullOrEmpty(transformKey))
        {
            return false;
        }

        bool containsTransform = goGlobalIdToGuidMap.TryGetValue(transformKey, out GuidRecord value);
        if (!string.IsNullOrEmpty(componentKey) && value != null)
        {
            return value.assignedGuids.Exists(g => g.globalObjectID == componentKey);
        }

        return containsTransform;
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

    private void OnEnable()
    {
        Undo.undoRedoPerformed += UndoRedoPerformed;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private void OnDisable()
    {
        Undo.undoRedoPerformed -= UndoRedoPerformed;
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
    }

    private void OnPlayModeStateChanged(PlayModeStateChange stateChange)
    {
        switch (stateChange)
        {
            case PlayModeStateChange.ExitingPlayMode:
                LoadOrCreate();
                break;
        }
    }

    private void UndoRedoPerformed()
    {
        Save();
    }

    [MenuItem("Tools/Guid Referencing/Reload Mappings")]
    private static void ReloadGuidMappings()
    {
        Undo.ClearUndo(_instance);
        DestroyImmediate(_instance);
        LoadOrCreate();
    }

    internal static void LoadOrCreate()
    {
        if (_instance == null)
        {
            CreateInstance<GuidMappings>().hideFlags = HideFlags.HideAndDontSave;
        }

        if (TryLoadAsset(_instance))
        {
            return;
        }

        SaveToJson(_instance, AssetPath);
    }

    internal static bool TryLoadAsset(GuidMappings settings)
    {
        if (File.Exists(AssetPath))
        {
            LoadFromJson(AssetPath, settings);
            return true;
        }

        return false;
    }

    private static void LoadFromJson<T>(string path, T objectInstance) where T : ScriptableObject
    {
        string json = File.ReadAllText(path);
        JsonUtility.FromJsonOverwrite(json, objectInstance);
    }

    private static void SaveToJson(ScriptableObject obj, string path)
    {
        string json = JsonUtility.ToJson(obj, true);
        File.WriteAllText(path, json);
    }

    private void Save()
    {
        // Only save state in edit mode.
        if (!EditorApplication.isPlaying)
        {
            SaveToJson(this, AssetPath);
            EditorUtility.SetDirty(this);
        }
    }
}