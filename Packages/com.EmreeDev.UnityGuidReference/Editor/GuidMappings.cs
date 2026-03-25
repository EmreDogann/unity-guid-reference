using System;
using System.Collections.Generic;
using System.IO;
using Sherbert.Framework.Generic;
using UnityEditor;
using UnityEngine;

public sealed class GuidMappings : ScriptableObject
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

    private GuidMappings()
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

    public void Add(string transformKey, string componentKey, GuidItem guidItem, bool overwriteIfExists = false)
    {
        if (string.IsNullOrEmpty(transformKey))
        {
            Debug.LogError("[GuidReferenceMappings] Error: transformKey must have a valid GlobalObjectID!");
            return;
        }

        Undo.RecordObject(this, "Added GUID Mapping");

        if (!_map.TryGetValue(transformKey, out GuidRecord guidRecord))
        {
            guidRecord = new GuidRecord();
            _map.Add(transformKey, guidRecord);
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
                EditorUtility.SetDirty(this);
            }
            else
            {
                if (!guidRecord.assignedGuids.Exists(g => g.globalObjectID == componentKey))
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

    public void RemoveOrphaned(string transformKey, SerializableGuid componentGuid)
    {
        if (!_map.TryGetValue(transformKey, out GuidRecord guidRecord))
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
                EditorUtility.SetDirty(this);
                return;
            }
        }
    }

    public void RemoveComponent(string transformKey, string componentKey)
    {
        if (!_map.TryGetValue(transformKey, out GuidRecord guidRecord))
        {
            return;
        }

        Undo.RecordObject(this, "Remove GUID Mapping");

        int idx = guidRecord.assignedGuids.FindIndex(g => g.globalObjectID == componentKey);
        if (idx >= 0)
        {
            guidRecord.assignedGuids.RemoveAt(idx);
            Save();
            EditorUtility.SetDirty(this);
        }
    }

    public void RemoveComponentByGuid(string transformKey, SerializableGuid componentGuid)
    {
        if (!_map.TryGetValue(transformKey, out GuidRecord guidRecord))
        {
            return;
        }

        Undo.RecordObject(this, "Remove GUID Mapping");

        int idx = guidRecord.assignedGuids.FindIndex(g => g.guid == componentGuid);
        if (idx >= 0)
        {
            guidRecord.assignedGuids.RemoveAt(idx);
            Save();
            EditorUtility.SetDirty(this);
        }
    }

    public void RemoveRecord(string transformKey)
    {
        if (!_map.ContainsKey(transformKey))
        {
            return;
        }

        Undo.RecordObject(this, "Remove GUID Mapping");
        _map.Remove(transformKey);
        Save();
        EditorUtility.SetDirty(this);
    }

    public bool Contains(string transformKey, string componentKey = "")
    {
        if (string.IsNullOrEmpty(transformKey))
        {
            return false;
        }

        bool containsTransform = _map.TryGetValue(transformKey, out GuidRecord value);
        if (!string.IsNullOrEmpty(componentKey) && value != null)
        {
            return value.assignedGuids.Exists(g => g.globalObjectID == componentKey);
        }

        return containsTransform;
    }

    public bool TryGetRecord(string transformKey, out GuidRecord guidRecord)
    {
        return _map.TryGetValue(transformKey, out guidRecord);
    }

    public bool TryGetByKey(string transformKey, string componentKey, out GuidItem guidItem)
    {
        guidItem = null;
        if (!_map.TryGetValue(transformKey, out GuidRecord guidRecord))
        {
            return false;
        }

        guidItem = guidRecord.assignedGuids.Find(g => g.globalObjectID == componentKey);
        return guidItem != null;
    }

    public bool TryGetByGuid(string transformKey, SerializableGuid componentGuid, out GuidItem guidItem)
    {
        guidItem = null;
        if (!_map.TryGetValue(transformKey, out GuidRecord guidRecord))
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
            case PlayModeStateChange.EnteredEditMode:
                Debug.Log("Entered Editmode");
                break;
            case PlayModeStateChange.ExitingEditMode:
                Debug.Log("Exiting Editmode");
                break;
            case PlayModeStateChange.EnteredPlayMode:
                Debug.Log("Entered Playmode");
                break;
            case PlayModeStateChange.ExitingPlayMode:
                Debug.Log("Exiting Playmode");
                LoadOrCreate();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(stateChange), stateChange, null);
        }
    }

    private void UndoRedoPerformed()
    {
        Debug.Log("Undo Saving...");
        Save();
    }

    [MenuItem("Tools/Guid Referencing/Reload Mappings")]
    private static void ReloadGuidMappings()
    {
        DestroyImmediate(s_Instance);
        Undo.ClearUndo(s_Instance);
        LoadOrCreate();

        Debug.Log("Cleared Guid Mappings");
    }

    internal static void LoadOrCreate()
    {
        if (s_Instance == null)
        {
            CreateInstance<GuidMappings>().hideFlags = HideFlags.HideAndDontSave;
        }

        if (TryLoadAsset(s_Instance))
        {
            return;
        }

        SaveToJson(s_Instance, AssetPath);
    }

    internal static bool TryLoadAsset(GuidMappings settings)
    {
        if (File.Exists(AssetPath))
        {
            LoadFromJson(AssetPath, settings);
            return true;
        }

        //
        return false;
    }

    private static void LoadFromJson<T>(string path, T objectInstance) where T : ScriptableObject
    {
        string json = File.ReadAllText(path);
        EditorJsonUtility.FromJsonOverwrite(json, objectInstance);
    }

    private static void SaveToJson(ScriptableObject obj, string path)
    {
        string json = EditorJsonUtility.ToJson(obj, true);
        File.WriteAllText(path, json);
    }

    private void Save()
    {
        // Only save state in edit mode.
        if (!EditorApplication.isPlaying)
        {
            SaveToJson(this, AssetPath);
        }
    }
}