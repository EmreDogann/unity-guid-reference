using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

[Serializable]
public class ComponentGuid : IEquatable<ComponentGuid>
{
    public Component component;
    public Guid Guid;
    public byte[] serializedGuid;
    [NonSerialized] public byte[] editorSerializedGuid;

    public ComponentGuid() {}

    public ComponentGuid(Component component)
    {
        this.component = component;
    }

    public ComponentGuid(Component component, Guid guid, byte[] serializedGuid)
    {
        this.component = component;
        Guid = guid;
        this.serializedGuid = serializedGuid;
    }

    public bool IsType(Type type)
    {
        return component.GetType() == type;
    }

    public bool IsType<T>() where T : Component
    {
        return component.GetType() == typeof(T);
    }

    public bool Equals(ComponentGuid other)
    {
        if (ReferenceEquals(null, other))
        {
            return false;
        }

        if (ReferenceEquals(this, null))
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return Guid.Equals(other.Guid) && serializedGuid != null &&
               serializedGuid.SequenceEqual(other.serializedGuid);
    }

    public override bool Equals(object obj)
    {
        if (ReferenceEquals(null, obj))
        {
            return false;
        }

        if (ReferenceEquals(this, obj))
        {
            return true;
        }

        if (obj.GetType() != GetType())
        {
            return false;
        }

        return Equals((ComponentGuid)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Guid, serializedGuid);
    }
}

// This component gives a GameObject a stable, non-replicatable Globally Unique IDentifier.
// It can be used to reference a specific instance of an object no matter where it is.
// This can also be used for other systems, such as Save/Load game
[ExecuteAlways] [DisallowMultipleComponent]
public class GuidComponent : MonoBehaviour, ISerializationCallbackReceiver
{
    // From: https://discussions.unity.com/t/prevent-reset-from-clearing-out-serialized-fields/191838/5
    // Additional Reference: https://www.sisus.co/optional-context-menu-items-in-unity/
    // [MenuItem("CONTEXT/" + nameof(GuidComponent) + "/Reset", true, int.MinValue)]
    // private static bool OnValidateReset()
    // {
    //     return false;
    // }
    //
    // [MenuItem("CONTEXT/" + nameof(GuidComponent) + "/Reset", false, int.MinValue)]
    // private static void OnReset()
    // {
    //     Debug.LogWarning("MyScript doesn't support Reset.");
    // }

    private readonly Type GameObjectType = typeof(GameObject);

    // System guid we use for comparison and generation
    private Guid _guid = Guid.Empty;
    [SerializeField] internal List<ComponentGuid> ComponentGUIDs = new List<ComponentGuid>();
    // Used to save out componentGUIDs as a way to prevent those values from getting reset when Reset() is triggered or when applying prefab.
    // Reference: https://discussions.unity.com/t/prevent-reset-from-clearing-out-serialized-fields/191838/3
    private readonly List<ComponentGuid> ComponentGUIDs_dump = new List<ComponentGuid>();

    private byte[] _editorValue;

    // Unity's serialization system doesn't know about System.Guid, so we convert to a byte array
    // Fun fact, we tried using strings at first, but that allocated memory and was twice as slow
    [SerializeField]
    private byte[] _serializedGuid;

#if UNITY_EDITOR
    private bool _isDestroyed;
#endif

    private static bool IsSerializedGuidValid(byte[] serializedGuidArray)
    {
        return serializedGuidArray != null && serializedGuidArray.Length == 16;
    }

    public IReadOnlyList<ComponentGuid> GetComponentGUIDs()
    {
        return ComponentGUIDs;
    }

    public bool IsGuidAssigned()
    {
        return _guid != Guid.Empty;
    }

    #region Equality

    public static bool operator ==(GuidComponent guidComponent, GuidComponent otherGuidComponent)
    {
        if (ReferenceEquals(guidComponent, otherGuidComponent))
        {
            return true;
        }

        if (ReferenceEquals(guidComponent, null))
        {
            return false;
        }

        if (ReferenceEquals(otherGuidComponent, null))
        {
            return false;
        }

        return guidComponent.Equals(otherGuidComponent);
    }

    public static bool operator !=(GuidComponent guidComponent, GuidComponent otherGuidComponent)
    {
        return !(guidComponent == otherGuidComponent);
    }

    public static bool operator ==(Object obj, GuidComponent guidComponent)
    {
        if (ReferenceEquals(obj, guidComponent))
        {
            return true;
        }

        if (ReferenceEquals(obj, null))
        {
            return false;
        }

        if (ReferenceEquals(guidComponent, null))
        {
            return false;
        }

        if (obj.GetType() != guidComponent.GetType())
        {
            return false;
        }

        return guidComponent.Equals(obj as GuidComponent);
    }

    public static bool operator !=(Object obj, GuidComponent guidComponent)
    {
        return !(obj == guidComponent);
    }

    protected bool Equals(GuidComponent other)
    {
        return Equals(_guid, other._guid) && ComponentGUIDs.SequenceEqual(other.ComponentGUIDs);
    }

    public override bool Equals(object obj)
    {
        if (ReferenceEquals(null, obj))
        {
            return false;
        }

        if (ReferenceEquals(this, null))
        {
            return false;
        }

        if (ReferenceEquals(this, obj))
        {
            return true;
        }

        if (obj.GetType() != GetType())
        {
            return false;
        }

        return Equals((GuidComponent)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(_guid, _serializedGuid);
    }

    #endregion

    public bool HasMultipleComponentsOf(Type T)
    {
        return ComponentGUIDs.FindAll(componentGuid => componentGuid.IsType(T)).Count > 1;
    }

    public bool HasMultipleComponentsOf<T>() where T : Component
    {
        return ComponentGUIDs.FindAll(componentGuid => componentGuid.IsType<T>()).Count > 1;
    }

    // When de-serializing or creating this component, we want to either restore our serialized GUID
    // or create a new one.
    internal void CreateGuid(ref Guid guid, ref byte[] serializedGuid, byte[] editorValue)
    {
        if (!IsSerializedGuidValid(serializedGuid) && IsSerializedGuidValid(editorValue))
        {
            serializedGuid = editorValue;
        }

        // if our serialized data is invalid, then we are a new object and need a new GUID
        if (serializedGuid == null || serializedGuid.Length != 16)
        {
#if UNITY_EDITOR
            // if in editor, make sure we aren't a prefab of some kind
            if (IsAssetOnDisk())
            {
                return;
            }

            Undo.RecordObject(this, "Added GUID");
            Undo.FlushUndoRecordObjects();
#endif

            guid = Guid.NewGuid();
            serializedGuid = guid.ToByteArray();

#if UNITY_EDITOR
            // If we are creating a new GUID for a prefab instance of a prefab, but we have somehow lost our prefab connection
            // force a save of the modified prefab instance properties
            if (PrefabUtility.IsPartOfNonAssetPrefabInstance(this))
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(this);
            }
#endif
        }
        else if (guid == Guid.Empty)
        {
            // otherwise, we should set our system guid to our serialized guid
            guid = new Guid(serializedGuid);
        }

        // register with the GUID Manager so that other components can access this
        if (guid != Guid.Empty)
        {
            if (!GuidManager.Add(guid, this))
            {
                // if registration fails, we probably have a duplicate or invalid GUID, get us a new one.
                serializedGuid = null;
                guid = Guid.Empty;
                CreateGuid(ref guid, ref serializedGuid, editorValue);
            }
        }
    }

#if UNITY_EDITOR
    internal bool IsEditingInPrefabMode()
    {
        if (EditorUtility.IsPersistent(this))
        {
            // if the game object is stored on disk, it is a prefab of some kind, despite not returning true for IsPartOfPrefabAsset =/
            return true;
        }

        // If the GameObject is not persistent let's determine which stage we are in first because getting Prefab info depends on it
        StageHandle mainStage = StageUtility.GetMainStageHandle();
        StageHandle currentStage = StageUtility.GetStageHandle(gameObject);
        PrefabStage prefabStage;
        if (currentStage != mainStage)
        {
            prefabStage = PrefabStageUtility.GetPrefabStage(gameObject);
            if (prefabStage)
            {
                return true;
            }
        }

        prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
        if (prefabStage && prefabStage.IsPartOfPrefabContents(gameObject))
        {
            return true;
        }

        return false;
    }

    internal bool IsAssetOnDisk()
    {
        return PrefabUtility.IsPartOfPrefabAsset(this) || IsEditingInPrefabMode();
    }
#endif

    // https://forum.unity.com/threads/iserializationcallbackreceiver-thread-safety-concerns.315475/#post-2347924
    // During OnBeforeSerialize, you can modify any local field that is serialized, as that's how you hand over the serialized representation for unity.

    // We cannot allow a GUID to be saved into a prefab, and we need to convert to byte[]
    public void OnBeforeSerialize()
    {
#if UNITY_EDITOR
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isUpdating)
        {
            return;
        }

        if (!this || !gameObject)
        {
            return;
        }

        // This lets us detect if we are a prefab instance or a prefab asset.
        // A prefab asset cannot contain a GUID since it would then be duplicated when instanced.
        if (IsAssetOnDisk())
        {
            _serializedGuid = null;
            _guid = Guid.Empty;

            ComponentGUIDs_dump.Clear();
            foreach (ComponentGuid componentGuid in ComponentGUIDs)
            {
                componentGuid.serializedGuid = null;
                componentGuid.Guid = Guid.Empty;
                ComponentGUIDs_dump.Add(componentGuid);
            }
        }
        else
#endif
        {
            if (_guid != Guid.Empty)
            {
                _serializedGuid = _guid.ToByteArray();
            }

            ComponentGUIDs_dump.Clear();
            foreach (ComponentGuid componentGuid in ComponentGUIDs)
            {
                ComponentGUIDs_dump.Add(componentGuid);
            }

            foreach (ComponentGuid componentGuid in ComponentGUIDs_dump)
            {
                if (componentGuid.Guid != Guid.Empty)
                {
                    componentGuid.serializedGuid = componentGuid.Guid.ToByteArray();
                }
            }
        }
    }

    // https://forum.unity.com/threads/iserializationcallbackreceiver-thread-safety-concerns.315475/#post-2347924
    // During OnAfterDeserialize do NOT modify any field that is serialized, even if its local.

    // On load, we can go head a restore our system guid for later use
    public void OnAfterDeserialize()
    {
        if (IsSerializedGuidValid(_serializedGuid))
        {
            _guid = new Guid(_serializedGuid);
        }

        if (ComponentGUIDs_dump != null && ComponentGUIDs_dump.Count > 0)
        {
            ComponentGUIDs.Clear();
            foreach (ComponentGuid componentGuid in ComponentGUIDs_dump)
            {
                ComponentGUIDs.Add(componentGuid);
            }
        }

        foreach (ComponentGuid componentGuid in ComponentGUIDs)
        {
            if (IsSerializedGuidValid(componentGuid.serializedGuid))
            {
                componentGuid.Guid = new Guid(componentGuid.serializedGuid);
            }
        }
    }

    private void Awake()
    {
        CreateGuid(ref _guid, ref _serializedGuid, _editorValue);

        foreach (Component component in gameObject.GetComponents<Component>())
        {
            if (GuidComponentExcluders.Excluders.Contains(component.GetType()))
            {
                continue;
            }

            ComponentGuid componentGuid = ComponentGUIDs.FirstOrDefault(c => c.component == component);
            if (componentGuid == null)
            {
                componentGuid = new ComponentGuid { component = component };
                ComponentGUIDs.Add(componentGuid);
            }

            CreateGuid(ref componentGuid.Guid, ref componentGuid.serializedGuid, componentGuid.editorSerializedGuid);
            componentGuid.editorSerializedGuid = componentGuid.serializedGuid;
        }

        _editorValue = _serializedGuid;
    }

#if UNITY_EDITOR
    private void OnSceneSaving(Scene scene, string path)
    {
        // Check for stale components
        for (int i = ComponentGUIDs.Count - 1; i >= 0; i--)
        {
            if (!ComponentGUIDs[i].component)
            {
                GuidManager.Remove(ComponentGUIDs[i].Guid);
                ComponentGUIDs.RemoveAt(i);
            }
        }

        // Check for new components
        var components = gameObject.GetComponents<Component>()
            .Where(component => !GuidComponentExcluders.Excluders.Contains(component.GetType())).ToList();
        if (components.Count == 0)
        {
            ComponentGUIDs.Clear();
        }

        foreach (Component component in components)
        {
            ComponentGuid componentGuid =
                ComponentGUIDs.FirstOrDefault(c => c.component == component);
            if (componentGuid == null)
            {
                componentGuid = new ComponentGuid { component = component };
                ComponentGUIDs.Add(componentGuid);
            }

            CreateGuid(ref componentGuid.Guid, ref componentGuid.serializedGuid, componentGuid.editorSerializedGuid);
            componentGuid.editorSerializedGuid = componentGuid.serializedGuid;
        }
    }
#endif

    private void OnValidate()
    {
#if UNITY_EDITOR
        // similar to on Serialize, but gets called on Copying a Component or Applying a Prefab
        // at a time that lets us detect what we are
        if (IsAssetOnDisk())
        {
            _serializedGuid = null;
            _guid = Guid.Empty;

            foreach (ComponentGuid componentGuid in ComponentGUIDs)
            {
                componentGuid.serializedGuid = null;
                componentGuid.Guid = Guid.Empty;
            }
        }
        else
#endif
        {
            CreateGuid(ref _guid, ref _serializedGuid, _editorValue);

            var components = gameObject.GetComponents<Component>()
                .Where(component => !GuidComponentExcluders.Excluders.Contains(component.GetType())).ToList();
            if (components.Count == 0)
            {
                ComponentGUIDs.Clear();
            }

            foreach (Component component in components)
            {
                ComponentGuid componentGuid =
                    ComponentGUIDs.FirstOrDefault(c => c.component == component);
                if (componentGuid == null)
                {
                    componentGuid = new ComponentGuid { component = component };
                    ComponentGUIDs.Add(componentGuid);
                }

                CreateGuid(ref componentGuid.Guid, ref componentGuid.serializedGuid,
                    componentGuid.editorSerializedGuid);
                componentGuid.editorSerializedGuid = componentGuid.serializedGuid;
            }
        }

        _editorValue = _serializedGuid;
    }

    private void OnEnable()
    {
#if UNITY_EDITOR
        EditorSceneManager.sceneSaving += OnSceneSaving;
#endif
    }

    public void OnDisable()
    {
#if UNITY_EDITOR
        EditorSceneManager.sceneSaving -= OnSceneSaving;

        // Don't run for objects loaded from disk.
        if (GetInstanceID() < 0)
        {
            // Here we run a delayed call to solve a very specific edge case where the component,
            // which is part of a prefab, is removed on the prefab instance, and then that component removal modification
            // is applied via the removed component itself (not via Apply All). For some reason, when this is done,
            // Unity creates an intermediary instance of this component but does not call OnDestroy(),
            // so this edge case ends up leaving behind dangling guids.

            // This very (and I mean VERY) ugly hack runs late (after all inspectors update, hopefully after the prefab modification is fully applied)
            // and checks if the object has already been destroyed, if not and object instance is now null, we have run into
            // the edge case described above, so we can manually call OnDestroy() ourselves.
            EditorApplication.delayCall += () =>
            {
                if (!_isDestroyed && !this)
                {
                    OnDestroy();
                }
            };
        }
#endif
    }

    // Never return an invalid GUID
    public Guid GetGuid()
    {
        if (_guid == Guid.Empty && IsSerializedGuidValid(_serializedGuid))
        {
            _guid = new Guid(_serializedGuid);
        }

        return _guid;
    }

    public Guid GetGuid(Type type)
    {
        if (type == GameObjectType)
        {
            return GetGuid();
        }

        foreach (ComponentGuid componentGuid in ComponentGUIDs)
        {
            if (!componentGuid.IsType(type))
            {
                continue;
            }

            if (componentGuid.Guid == Guid.Empty &&
                IsSerializedGuidValid(componentGuid.serializedGuid))
            {
                componentGuid.Guid = new Guid(componentGuid.serializedGuid);
            }

            return componentGuid.Guid;
        }

        return Guid.Empty;
    }

    public Guid GetGuid(Component component)
    {
        if (component is GuidComponent)
        {
            return Guid.Empty;
        }

        foreach (ComponentGuid componentGuid in ComponentGUIDs)
        {
            if (componentGuid.component != component)
            {
                continue;
            }

            if (componentGuid.Guid == Guid.Empty && IsSerializedGuidValid(componentGuid.serializedGuid))
            {
                componentGuid.Guid = new Guid(componentGuid.serializedGuid);
            }

            return componentGuid.Guid;
        }

        return Guid.Empty;
    }

    public Component GetComponentFromGuid(Guid guid)
    {
        if (guid != _guid)
        {
            return ComponentGUIDs
                .Where(c => c.Guid == guid)
                .Select(componentGuid => componentGuid.component)
                .FirstOrDefault();
        }

        return null;
    }

    // let the manager know we are gone, so other objects no longer find this
    public void OnDestroy()
    {
#if UNITY_EDITOR
        _isDestroyed = true;

        if (this && IsAssetOnDisk())
        {
            GuidManager.Remove(PrefabStageUtility.GetPrefabStage(gameObject).assetPath);
        }
        else
#endif
        {
            GuidManager.Remove(_guid);
            foreach (ComponentGuid componentGuid in ComponentGUIDs)
            {
                GuidManager.Remove(componentGuid.Guid);
            }
        }
    }
}