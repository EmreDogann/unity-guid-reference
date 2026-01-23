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
        public SerializableType ownerType;
        public string globalObjectID;
        public SerializableGuid guid;
    }

    [Serializable]
    public class GuidRecord
    {
        public GuidItem transformGuid;
        public SerializableDictionary<string, GuidItem> componentGuids;
    }

    public readonly struct GuidRecordQuery
    {
        public readonly string TransformKey;
        public readonly string ComponentKey;
        public readonly Guid ComponentGuid;

        public GuidRecordQuery(string transformKey, string componentKey = "", Guid componentGuid = default)
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

    private const string DefaultAssetPath = "Assets/GuidReferenceMappings.asset";
    private static string AssetPathKey => $"{Application.dataPath}_GuidReferenceMappingsPath";
    private readonly Dictionary<string, GuidRecord> _map = new Dictionary<string, GuidRecord>();

    [SerializeField] private List<string> keys = new List<string>();
    [SerializeField] private List<GuidRecord> values = new List<GuidRecord>();

    public void Add(GuidRecordQuery query, GuidItem guidItem, bool overwriteIfExists = false)
    {
        if (!query.IsTransformKeyValid())
        {
            return;
        }

        Undo.RecordObject(this, "Added GUID Mapping");

        if (!_map.TryGetValue(query.TransformKey, out GuidRecord guidRecord))
        {
            guidRecord = new GuidRecord { componentGuids = new SerializableDictionary<string, GuidItem>() };
            _map.Add(query.TransformKey, guidRecord);
        }

        if (query.IsComponentKeyValid())
        {
            if (overwriteIfExists)
            {
                guidRecord.componentGuids[query.ComponentKey] = guidItem;
                EditorUtility.SetDirty(this);
            }
            else
            {
                if (guidRecord.componentGuids.TryAdd(query.ComponentKey, guidItem))
                {
                    EditorUtility.SetDirty(this);
                }
            }
        }
        else if (query.ComponentGuid != Guid.Empty)
        {
            foreach (GuidItem componentGuid in guidRecord.componentGuids.Values)
            {
                if (componentGuid.guid.Guid == query.ComponentGuid)
                {
                    guidRecord.componentGuids[componentGuid.globalObjectID] = guidItem;
                    EditorUtility.SetDirty(this);
                }
            }
        }
        else
        {
            guidRecord.transformGuid = guidItem;
            EditorUtility.SetDirty(this);
        }
    }

    public void SetState(GuidItem item, GuidState state)
    {
        Undo.RecordObject(this, "Orphaned GUID Mapping");
        item.state = state;
        EditorUtility.SetDirty(this);
    }

    public void Remove(GuidRecordQuery query)
    {
        if (!query.IsTransformKeyValid())
        {
            return;
        }

        bool containsTransform = _map.TryGetValue(query.TransformKey, out GuidRecord guidRecord);
        if (containsTransform)
        {
            Undo.RecordObject(this, "Remove GUID Mapping");
            if (query.IsComponentKeyValid())
            {
                if (guidRecord.componentGuids.TryGetValue(query.ComponentKey, out GuidItem guidItem))
                {
                    guidRecord.componentGuids.Remove(guidItem.globalObjectID);
                    EditorUtility.SetDirty(this);
                    return;
                }
            }

            if (query.ComponentGuid != Guid.Empty)
            {
                foreach (GuidItem componentGuid in guidRecord.componentGuids.Values)
                {
                    if (componentGuid.guid.Guid == query.ComponentGuid)
                    {
                        guidRecord.componentGuids.Remove(componentGuid.globalObjectID);
                        EditorUtility.SetDirty(this);
                    }
                }

                return;
            }

            _map.Remove(guidRecord.transformGuid.globalObjectID);
            EditorUtility.SetDirty(this);
        }
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
            return value.componentGuids.ContainsKey(query.ComponentKey);
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
                return guidRecord.componentGuids.TryGetValue(query.ComponentKey, out guidItem);
            }

            if (query.ComponentGuid != Guid.Empty)
            {
                foreach (GuidItem componentGuid in guidRecord.componentGuids.Values)
                {
                    if (componentGuid.guid.Guid == query.ComponentGuid)
                    {
                        guidItem = componentGuid;
                        return true;
                    }
                }

                return false;
            }

            guidItem = guidRecord.transformGuid;
        }

        return containsTransform;
    }

    // Serialize
    void ISerializationCallbackReceiver.OnBeforeSerialize()
    {
        keys.Clear();
        values.Clear();

        foreach (string key in _map.Keys)
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