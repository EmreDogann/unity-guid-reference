using System;
using System.Collections.Generic;
using System.IO;
using Sherbert.Framework.Generic;
using UnityEditor;
using UnityEngine;

public class GuidMappings : ScriptableObject
{
    private static GuidMappings s_Instance;
    public static GuidMappings Instance
    {
        get
        {
            if (s_Instance == null)
            {
                LoadOrCreate();
            }

            return s_Instance;
        }
    }

    protected GuidMappings()
    {
        if (s_Instance != null)
        {
            Debug.LogError("GuidMappings already exists. Did you query the singleton in a constructor?");
        }
        else
        {
            s_Instance = this;
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

    public readonly struct GuidRecordQuery
    {
        public readonly string TransformKey;
        public readonly string ComponentKey;
        public readonly SerializableGuid ComponentGuid;

        public GuidRecordQuery(string transformKey, string componentKey = "", SerializableGuid componentGuid = default)
        {
            TransformKey = transformKey;
            ComponentKey = componentKey;
            ComponentGuid = componentGuid;
        }

        public bool IsTransformKeyValid()
        {
            if (string.IsNullOrEmpty(TransformKey))
            {
                Debug.LogError(
                    "[GuidReferenceMappings] Error: GuidRecordQuery.TransformKey must have a valid GlobalObjectID!");
                return false;
            }

            return true;
        }

        public bool IsComponentKeyValid()
        {
            return !string.IsNullOrEmpty(ComponentKey);
        }
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
    private SerializableDictionary<string, GuidRecord> _map = new SerializableDictionary<string, GuidRecord>();

    public void Add(GuidRecordQuery query, GuidItem guidItem, bool overwriteIfExists = false)
    {
        if (!query.IsTransformKeyValid())
        {
            return;
        }

        Undo.RecordObject(this, "Added GUID Mapping");

        if (!_map.TryGetValue(query.TransformKey, out GuidRecord guidRecord))
        {
            guidRecord = new GuidRecord();
            _map.Add(query.TransformKey, guidRecord);
        }

        if (query.IsComponentKeyValid())
        {
            if (overwriteIfExists)
            {
                int idx = guidRecord.assignedGuids.FindIndex(g => g.globalObjectID == query.ComponentKey);
                if (idx >= 0)
                {
                    guidRecord.assignedGuids[idx] = guidItem;
                }
                else
                {
                    guidRecord.assignedGuids.Add(guidItem);
                }

                Save();
                EditorUtility.SetDirty(this);
            }
            else
            {
                if (!guidRecord.assignedGuids.Exists(g => g.globalObjectID == query.ComponentKey))
                {
                    guidRecord.assignedGuids.Add(guidItem);
                    Save();
                    EditorUtility.SetDirty(this);
                }
            }
        }
        else
        {
            guidRecord.transformGuid = guidItem;
            Save();
            EditorUtility.SetDirty(this);
        }
    }

    internal void ClearTransformOrphaned(string transformKey)
    {
        if (_map.TryGetValue(transformKey, out GuidRecord guidRecord) && guidRecord.transformOrphaned)
        {
            Undo.RecordObject(this, "Restoring Transform Guid");
            guidRecord.transformOrphaned = false;
            Save();
            EditorUtility.SetDirty(this);
        }
    }

    public void OrphanGuid(string transformGuidId, GuidItem item = null)
    {
        if (transformGuidId == OrphanedGuidObjectId)
        {
            return;
        }

        Undo.RecordObject(this, "Orphaning Guid");

        if (_map.TryGetValue(transformGuidId, out GuidRecord guidRecord))
        {
            if (item != null)
            {
                int componentIndex = guidRecord.assignedGuids.FindIndex(g => g.guid == item.guid);
                if (componentIndex >= 0)
                {
                    guidRecord.assignedGuids.RemoveAt(componentIndex);
                    guidRecord.orphanedGuids.Add(item);

                    Save();
                    EditorUtility.SetDirty(this);
                }
            }
            else
            {
                guidRecord.transformOrphaned = true;
            }
        }
    }

    internal bool AdoptGuid(GuidItem guidItem, string transformGlobalObjectId, string componentGlobalObjectId)
    {
        Undo.RecordObject(this, "Adopting Guid");

        if (_map.TryGetValue(transformGlobalObjectId, out GuidRecord guidRecord))
        {
            if (guidRecord.orphanedGuids.Contains(guidItem))
            {
                guidRecord.orphanedGuids.Remove(guidItem);
                guidItem.globalObjectID = componentGlobalObjectId;
                guidRecord.assignedGuids.Add(guidItem);

                Save();
                EditorUtility.SetDirty(this);

                return true;
            }
        }

        return false;
    }

    public void Remove(GuidRecordQuery query, bool isOrphaned)
    {
        if (!query.IsTransformKeyValid())
        {
            return;
        }

        if (!_map.TryGetValue(query.TransformKey, out GuidRecord guidRecord))
        {
            return;
        }

        Undo.RecordObject(this, "Remove GUID Mapping");

        if (isOrphaned && query.ComponentGuid != SerializableGuid.Empty)
        {
            for (int i = guidRecord.orphanedGuids.Count - 1; i >= 0; i--)
            {
                if (guidRecord.orphanedGuids[i].guid == query.ComponentGuid)
                {
                    guidRecord.orphanedGuids.RemoveAt(i);
                    Save();
                    EditorUtility.SetDirty(this);
                    return;
                }
            }

            return;
        }

        if (!isOrphaned && query.IsComponentKeyValid())
        {
            int idx = guidRecord.assignedGuids.FindIndex(g => g.globalObjectID == query.ComponentKey);
            if (idx >= 0)
            {
                guidRecord.assignedGuids.RemoveAt(idx);
                Save();
                EditorUtility.SetDirty(this);
            }

            return;
        }

        if (!isOrphaned && query.ComponentGuid != SerializableGuid.Empty)
        {
            int idx = guidRecord.assignedGuids.FindIndex(g => g.guid == query.ComponentGuid);
            if (idx >= 0)
            {
                guidRecord.assignedGuids.RemoveAt(idx);
                Save();
                EditorUtility.SetDirty(this);
            }

            return;
        }

        _map.Remove(query.TransformKey);
        Save();
        EditorUtility.SetDirty(this);
    }

    public bool Contains(GuidRecordQuery query)
    {
        if (!query.IsTransformKeyValid())
        {
            return false;
        }

        bool containsTransform = _map.TryGetValue(query.TransformKey, out GuidRecord value);
        if (query.IsComponentKeyValid() && value != null)
        {
            return value.assignedGuids.Exists(g => g.globalObjectID == query.ComponentKey);
        }

        return containsTransform;
    }

    public bool TryGet(GuidRecordQuery query, out GuidRecord guidRecord, out GuidItem guidItem)
    {
        guidRecord = null;
        guidItem = null;
        if (!query.IsTransformKeyValid())
        {
            return false;
        }

        bool containsTransform = _map.TryGetValue(query.TransformKey, out guidRecord);
        if (containsTransform)
        {
            if (query.IsComponentKeyValid())
            {
                guidItem = guidRecord.assignedGuids.Find(g => g.globalObjectID == query.ComponentKey);
                return guidItem != null;
            }

            if (query.ComponentGuid != SerializableGuid.Empty)
            {
                guidItem = guidRecord.assignedGuids.Find(g => g.guid == query.ComponentGuid);
                return guidItem != null;
            }

            guidItem = guidRecord.transformGuid;
        }

        return containsTransform;
    }

    internal void Initialize()
    {
        Undo.undoRedoPerformed -= UndoRedoPerformed;
        Undo.undoRedoPerformed += UndoRedoPerformed;

        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private void OnPlayModeStateChanged(PlayModeStateChange stateChange)
    {
        switch (stateChange)
        {
            case PlayModeStateChange.EnteredEditMode:
                // ReloadInPlace();
                break;
            case PlayModeStateChange.ExitingEditMode:
                break;
            case PlayModeStateChange.EnteredPlayMode:
                break;
            case PlayModeStateChange.ExitingPlayMode:
                Debug.Log("Exiting Playmode");

                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(stateChange), stateChange, null);
        }
    }

    private void UndoRedoPerformed()
    {
        Save();
    }

    internal static GuidMappings LoadOrCreate()
    {
        GuidMappings settings = CreateInstance<GuidMappings>();
        settings.hideFlags = HideFlags.HideAndDontSave;
        if (TryLoadAsset(settings))
        {
            return settings;
        }

        settings.Initialize();
        SaveToJson(settings, AssetPath);

        return settings;
    }

    internal static bool TryLoadAsset(GuidMappings settings)
    {
        if (File.Exists(AssetPath))
        {
            LoadFromJson(AssetPath, settings);
        }

        return settings != null;
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

    protected void Save()
    {
        // Only save state in edit mode.
        if (!EditorApplication.isPlaying)
        {
            SaveToJson(this, AssetPath);
        }
    }
}