using System;
using System.Collections.Generic;
using System.Linq;
using Sherbert.Framework.Generic;
using UnityEditor;
using UnityEngine;

public class GuidReferenceMappings : ScriptableObject, ISerializationCallbackReceiver
{
    public enum GuidState
    {
        None,
        Owned,
        Orphaned
    }

    [Serializable]
    public class GuidItem
    {
        public GuidState state;
        public SerializableGuid guid;
    }

    [Serializable]
    public class GuidRecord
    {
        public GuidItem gameObjectGuid;
        public SerializableDictionary<string, GuidItem> componentGuids;
    }

    private const string DefaultAssetPath = "Assets/GuidReferenceMappings.asset";
    private static string AssetPathKey => $"{Application.dataPath}_GuidReferenceMappingsPath";
    private readonly Dictionary<string, GuidRecord> _map = new Dictionary<string, GuidRecord>();

    [SerializeField] private List<string> keys = new List<string>();
    [SerializeField] private List<GuidRecord> values = new List<GuidRecord>();

    public void Set(GlobalObjectId key, GuidRecord guids)
    {
        Set(key.ToString(), guids);
    }

    public void SetComponent(GuidRecord record, GlobalObjectId componentKey, GuidItem item)
    {
        Undo.RecordObject(this, "Add GUID Component Mapping");
        record.componentGuids[componentKey.ToString()] = item;
        EditorUtility.SetDirty(this);
    }

    public void Set(string key, GuidRecord guids)
    {
        Undo.RecordObject(this, "Add GUID Mapping");
        _map[key] = guids;
        EditorUtility.SetDirty(this);
    }

    public void SetState(GuidItem item, GuidState state)
    {
        Undo.RecordObject(this, "Orphaned GUID Mapping");
        item.state = state;
        EditorUtility.SetDirty(this);
    }

    public void Remove(GlobalObjectId key)
    {
        Remove(key.ToString());
    }

    public void Remove(string key)
    {
        Undo.RecordObject(this, "Remove GUID Mapping");
        _map.Remove(key);
        EditorUtility.SetDirty(this);
    }

    public bool TryGet(GlobalObjectId key, out GuidRecord guidRecord)
    {
        return TryGet(key.ToString(), out guidRecord);
    }

    public bool TryGet(string key, out GuidRecord guidRecord)
    {
        return _map.TryGetValue(key, out guidRecord);
    }

    // Serialize
    void ISerializationCallbackReceiver.OnBeforeSerialize()
    {
        keys.Clear();
        values.Clear();

        foreach (string key in _map.Keys.OrderBy(k => k))
        {
            keys.Add(key);
            values.Add(_map[key]);
        }
    }

    // Deserialize
    void ISerializationCallbackReceiver.OnAfterDeserialize()
    {
        _map.Clear();

        for (int i = 0; i < keys.Count; i++)
        {
            _map[keys[i]] = values[i];
        }
    }

    internal static GuidReferenceMappings GetOrCreate()
    {
        if (TryLoadAsset(out GuidReferenceMappings settings))
        {
            return settings;
        }

        settings = CreateInstance<GuidReferenceMappings>();
        AssetDatabase.CreateAsset(settings, DefaultAssetPath);
        AssetDatabase.SaveAssets();

        return settings;
    }

    internal static bool TryLoadAsset(out GuidReferenceMappings settings)
    {
        string assetPath = EditorPrefs.GetString(AssetPathKey, DefaultAssetPath);
        // try to load at the saved or default path
        settings = AssetDatabase.LoadAssetAtPath<GuidReferenceMappings>(assetPath);
        if (settings != null)
        {
            return true;
        }

        // if no asset at path try to find it in project's assets
        string assetGuid = AssetDatabase.FindAssets($"t:{typeof(GuidReferenceMappings)}").FirstOrDefault();
        if (string.IsNullOrEmpty(assetGuid))
        {
            return false;
        }

        assetPath = AssetDatabase.GUIDToAssetPath(assetGuid);
        settings = AssetDatabase.LoadAssetAtPath<GuidReferenceMappings>(assetPath);

        if (settings == null)
        {
            return false;
        }

        EditorPrefs.SetString(AssetPathKey, assetPath);
        return true;
    }
}