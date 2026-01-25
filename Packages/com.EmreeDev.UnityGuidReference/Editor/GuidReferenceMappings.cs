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

    public class OrphanedGuidItemInfo
    {
        public GuidItem TransformGuid;
        public GuidItem GuidItem;
    }

    [Serializable]
    public class GuidRecord
    {
        public GuidItem transformGuid;
        public SerializableDictionary<string, GuidItem> componentGuids;
        public SerializableDictionary<SerializableGuid, GuidItem> orphanedGuids;
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
        else if (query.ComponentGuid != SerializableGuid.Empty)
        {
            foreach (GuidItem componentGuid in guidRecord.componentGuids.Values)
            {
                if (componentGuid.guid == query.ComponentGuid)
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
        Undo.RecordObject(this, $"Setting GUID state to {state.ToString()}");
        item.state = state;
        EditorUtility.SetDirty(this);
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
                if (guidRecord.componentGuids.TryGetValue(item.globalObjectID, out GuidItem guidItem) &&
                    guidItem.state == GuidState.Owned)
                {
                    guidRecord.componentGuids.Remove(item.globalObjectID);

                    item.state = GuidState.Orphaned;
                    item.globalObjectID = OrphanedGuidObjectId;
                    guidRecord.orphanedGuids.TryAdd(item.guid, item);

                    EditorUtility.SetDirty(this);
                }
            }
            else
            {
                guidRecord.transformGuid.state = GuidState.Orphaned;
            }
        }
    }

    internal bool AdoptGuid(GuidItem guidItem, string transformGlobalObjectId, string componentGlobalObjectId)
    {
        Undo.RecordObject(this, "Adopting Guid");

        if (_map.TryGetValue(transformGlobalObjectId, out GuidRecord guidRecord))
        {
            if (guidRecord.orphanedGuids.TryGetValue(guidItem.guid, out _) && guidItem.state == GuidState.Orphaned)
            {
                guidItem.state = GuidState.Owned;
                guidItem.globalObjectID = componentGlobalObjectId;

                guidRecord.orphanedGuids.Remove(guidItem.guid);
                guidRecord.componentGuids.Add(componentGlobalObjectId, guidItem);

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

        bool containsTransform = _map.TryGetValue(query.TransformKey, out GuidRecord guidRecord);
        if (containsTransform)
        {
            Undo.RecordObject(this, "Remove GUID Mapping");
            if (query.IsComponentKeyValid())
            {
                if (isOrphaned)
                {
                    if (guidRecord.orphanedGuids.TryGetValue(query.ComponentGuid, out GuidItem guidItem))
                    {
                        guidRecord.orphanedGuids.Remove(guidItem.guid);
                        EditorUtility.SetDirty(this);
                        return;
                    }
                }
                else
                {
                    if (guidRecord.componentGuids.TryGetValue(query.ComponentKey, out GuidItem guidItem))
                    {
                        guidRecord.componentGuids.Remove(guidItem.globalObjectID);
                        EditorUtility.SetDirty(this);
                        return;
                    }
                }
            }

            if (query.ComponentGuid != SerializableGuid.Empty)
            {
                if (isOrphaned)
                {
                    foreach (GuidItem orphanedGuid in guidRecord.orphanedGuids.Values)
                    {
                        if (orphanedGuid.guid == query.ComponentGuid)
                        {
                            guidRecord.componentGuids.Remove(orphanedGuid.guid);
                            EditorUtility.SetDirty(this);
                        }
                    }
                }
                else
                {
                    foreach (GuidItem componentGuid in guidRecord.componentGuids.Values)
                    {
                        if (componentGuid.guid == query.ComponentGuid)
                        {
                            guidRecord.componentGuids.Remove(componentGuid.globalObjectID);
                            EditorUtility.SetDirty(this);
                        }
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

            if (query.ComponentGuid != SerializableGuid.Empty)
            {
                foreach (GuidItem componentGuid in guidRecord.componentGuids.Values)
                {
                    if (componentGuid.guid == query.ComponentGuid)
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