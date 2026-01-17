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

/// <summary>
///     Stores GUIDs for a cachedComponent on a GameObject
/// </summary>
[Serializable]
public class ComponentGuid : IEquatable<ComponentGuid>
{
    public Component cachedComponent;
    public Guid Guid;
    public byte[] serializedGuid;

    // Store a copy of serializedGuid in a NonSerialized field, so we don't lose the GUID on Reset()
    // See GuidComponent.serializedGuid_Editor for more details about this.
#if UNITY_EDITOR
    [NonSerialized] public byte[] SerializedGuid_Editor;
#endif

    public ComponentGuid() {}

    public ComponentGuid(Component cachedComponent)
    {
        this.cachedComponent = cachedComponent;
    }

    public ComponentGuid(Component cachedComponent, Guid guid, byte[] serializedGuid)
    {
        this.cachedComponent = cachedComponent;
        Guid = guid;
        this.serializedGuid = serializedGuid;
    }

    public bool IsTypeOrSubclassOf(Type type)
    {
        return cachedComponent.GetType() == type || cachedComponent.GetType().IsSubclassOf(type);
    }

    public bool IsTypeOrSubclassOf<T>() where T : Component
    {
        return cachedComponent.GetType() == typeof(T) || cachedComponent.GetType().IsSubclassOf(typeof(T));
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
        return HashCode.Combine(serializedGuid);
    }
}

/// <summary>
///     Gives a GameObject and its Components a stable, non-replicatable Globally Unique IDentifier.
///     It can be used to reference a specific instance of an object no matter where it is.
///     This can also be used for other systems, such as Save/Load game
/// </summary>
[ExecuteAlways] [DisallowMultipleComponent]
public class GuidComponent : MonoBehaviour, ISerializationCallbackReceiver
{
    private static readonly Type GameObjectType = typeof(GameObject);

    // System guid we use for comparison and generation
    private Guid _guid = Guid.Empty;
    // Unity's serialization system doesn't know about System.Guid, so we convert to a byte array
    // Fun fact, we tried using strings at first, but that allocated memory and was twice as slow
    [SerializeField] private byte[] serializedGuid;

    [SerializeField] internal List<ComponentGuid> componentGUIDs = new List<ComponentGuid>();
    // Used to save out componentGUIDs as a way to prevent those values from getting reset when Reset() is triggered or when applying prefab.
    // Reference: https://discussions.unity.com/t/prevent-reset-from-clearing-out-serialized-fields/191838/3
    private readonly List<ComponentGuid> componentGUIDs_dump = new List<ComponentGuid>();

#if UNITY_EDITOR
    // This non-serialized copy of the serializedGuid is for editor use only.
    // It is intended to circumvent the issue of Guids being regenerated in a prefab when applying or reverting prefab modifications.
    // It attempts to do this as the non-serialized value does not get cleared on prefab apply or revert or operations of that nature.
    private byte[] serializedGuid_Editor;
    private bool _isDestroyed;
#endif

    private static bool IsSerializedGuidValid(byte[] serializedGuidArray)
    {
        return serializedGuidArray != null && serializedGuidArray.Length == 16;
    }

    public IReadOnlyList<ComponentGuid> GetComponentGUIDs()
    {
        return componentGUIDs;
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
        return Equals(_guid, other._guid) && componentGUIDs.SequenceEqual(other.componentGUIDs);
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
        return HashCode.Combine(serializedGuid);
    }

    #endregion

    /// <summary>
    ///     Checks if there are GUIDs for multiple components of the same type.
    /// </summary>
    /// <param name="T">Type of component to check.</param>
    /// <returns>True if there are multiple components of the same type. False if not.</returns>
    public bool HasMultipleComponentsOf(Type T)
    {
        return componentGUIDs.FindAll(componentGuid => componentGuid.IsTypeOrSubclassOf(T)).Count > 1;
    }

    /// <summary>
    ///     Checks if there are GUIDs for multiple components of the same type.
    /// </summary>
    /// <typeparam name="T">Type of component to check.</typeparam>
    /// <returns>True if there are multiple components of the same type. False if not.</returns>
    public bool HasMultipleComponentsOf<T>() where T : Component
    {
        return componentGUIDs.FindAll(componentGuid => componentGuid.IsTypeOrSubclassOf<T>()).Count > 1;
    }

    // When de-serializing or creating this GuidComponent, we want to either restore our serialized GUID or create a new one.
    // If the guid already exists and is valid, then this function will just register the guid with the GuidManager.
#if UNITY_EDITOR
    internal void CreateOrRegisterGuid(ref Guid guid, ref byte[] serializedGuid, byte[] serializedGuid_Editor)
#else
    internal void CreateOrRegisterGuid(ref Guid guid, ref byte[] _serializedGuid)
#endif
    {
        // If our serialized data is invalid, then we are a new object and need a new GUID
        if (!IsSerializedGuidValid(serializedGuid))
        {
#if UNITY_EDITOR
            // If we need to create a new GUID but there is already one assigned via serializedGuid_Editor, just use that.
            // This is useful for things like resetting and applying prefabs.
            if (IsSerializedGuidValid(serializedGuid_Editor))
            {
                serializedGuid = serializedGuid_Editor;
            }
            else
            {
                // If in editor, make sure we aren't a prefab of some kind
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
            }
#endif
        }
        else if (guid == Guid.Empty)
        {
            // Otherwise, we should set our system guid to our serialized guid
            guid = new Guid(serializedGuid);
        }

        // Register with the GUID Manager so that GuidReferences can access this
        if (guid != Guid.Empty)
        {
            if (!GuidManager.Add(guid, this))
            {
                // If registration fails, we probably have a duplicate or invalid GUID, get us a new one.
                serializedGuid = null;
                guid = Guid.Empty;
#if UNITY_EDITOR
                CreateOrRegisterGuid(ref guid, ref serializedGuid, serializedGuid_Editor);
#else
                CreateOrRegisterGuid(ref guid, ref _serializedGuid);
#endif
            }
        }
    }

#if UNITY_EDITOR
    internal bool IsEditingInPrefabMode()
    {
        if (EditorUtility.IsPersistent(this))
        {
            // If the game object is stored on disk, it is a prefab of some kind, despite not returning true for IsPartOfPrefabAsset =/
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
        return prefabStage && prefabStage.IsPartOfPrefabContents(gameObject);
    }

    internal bool IsAssetOnDisk()
    {
        return PrefabUtility.IsPartOfPrefabAsset(this) || IsEditingInPrefabMode();
    }
#endif

    // We cannot allow a GUID to be saved into a prefab, and we also need to convert System.Guids to byte[]s
    public void OnBeforeSerialize()
    {
#if UNITY_EDITOR
        if (!this || !gameObject)
        {
            return;
        }

        // This lets us detect if we are a prefab instance or a prefab asset.
        // A prefab asset cannot contain a GUID since it would then be duplicated when instanced.
        if (IsAssetOnDisk())
        {
            serializedGuid = null;
            _guid = Guid.Empty;

            // Move all ComponentGuids over to the non-serialized dump list. See definition of componentGUIDs_dump for reasoning.
            componentGUIDs_dump.Clear();
            foreach (ComponentGuid componentGuid in componentGUIDs)
            {
                componentGuid.serializedGuid = null;
                componentGuid.Guid = Guid.Empty;
                componentGUIDs_dump.Add(componentGuid);
            }
        }
        else
#endif
        {
            if (_guid != Guid.Empty)
            {
                serializedGuid = _guid.ToByteArray();
            }

            // Move all ComponentGuids over to the non-serialized dump list. See definition of componentGUIDs_dump for reasoning.
            componentGUIDs_dump.Clear();
            foreach (ComponentGuid componentGuid in componentGUIDs)
            {
                componentGUIDs_dump.Add(componentGuid);
            }

            foreach (ComponentGuid componentGuid in componentGUIDs_dump)
            {
                if (componentGuid.Guid != Guid.Empty)
                {
                    componentGuid.serializedGuid = componentGuid.Guid.ToByteArray();
                }
            }
        }
    }

    // On load, we can go head a restore our system guids for later use
    public void OnAfterDeserialize()
    {
        if (IsSerializedGuidValid(serializedGuid))
        {
            _guid = new Guid(serializedGuid);
        }

        if (componentGUIDs_dump != null && componentGUIDs_dump.Count > 0)
        {
            componentGUIDs.Clear();
            foreach (ComponentGuid componentGuid in componentGUIDs_dump)
            {
                componentGUIDs.Add(componentGuid);
            }
        }

        foreach (ComponentGuid componentGuid in componentGUIDs)
        {
            if (IsSerializedGuidValid(componentGuid.serializedGuid))
            {
                componentGuid.Guid = new Guid(componentGuid.serializedGuid);
            }
        }
    }

    private void Awake()
    {
#if UNITY_EDITOR
        PrefabUtility.prefabInstanceReverting += PrefabUtilityOnprefabInstanceReverting;

        CreateOrRegisterGuid(ref _guid, ref serializedGuid, serializedGuid_Editor);
        serializedGuid_Editor = serializedGuid;

        // Look for new components on the GameObject. Exclude component types specified in GuidComponentExcluders.
        foreach (Component component in gameObject.GetComponents<Component>())
        {
            if (GuidComponentExcluders.Excluders.Contains(component.GetType()))
            {
                continue;
            }

            ComponentGuid componentGuid = componentGUIDs.FirstOrDefault(c => c.cachedComponent == component);
            if (componentGuid == null)
            {
                componentGuid = new ComponentGuid { cachedComponent = component };
                componentGUIDs.Add(componentGuid);
            }

            CreateOrRegisterGuid(ref componentGuid.Guid, ref componentGuid.serializedGuid,
                componentGuid.SerializedGuid_Editor);
            componentGuid.SerializedGuid_Editor = componentGuid.serializedGuid;
        }
#else
        CreateOrRegisterGuid(ref _guid, ref serializedGuid);
        foreach (var componentGuid in componentGUIDs)
        {
            CreateOrRegisterGuid(ref componentGuid.Guid, ref componentGuid.serializedGuid);
        }
#endif
    }

    private bool _isAttemptingPrefabRevert;

    private void PrefabUtilityOnprefabInstanceReverting(GameObject obj)
    {
        _isAttemptingPrefabRevert = true;
        Debug.Log("reverting");
    }

#if UNITY_EDITOR
    private void OnSceneSaving(Scene scene, string path)
    {
        // Check for stale components
        for (int i = componentGUIDs.Count - 1; i >= 0; i--)
        {
            if (!componentGUIDs[i].cachedComponent)
            {
                GuidManager.Remove(componentGUIDs[i].Guid);
                componentGUIDs.RemoveAt(i);
            }
        }

        // Check for new components
        var components = gameObject.GetComponents<Component>()
            .Where(component => !GuidComponentExcluders.Excluders.Contains(component.GetType())).ToList();
        if (components.Count == 0)
        {
            componentGUIDs.Clear();
        }

        foreach (Component component in components)
        {
            ComponentGuid componentGuid =
                componentGUIDs.FirstOrDefault(c => c.cachedComponent == component);
            if (componentGuid == null)
            {
                componentGuid = new ComponentGuid { cachedComponent = component };
                componentGUIDs.Add(componentGuid);
            }

            CreateOrRegisterGuid(ref componentGuid.Guid, ref componentGuid.serializedGuid,
                componentGuid.SerializedGuid_Editor);
            componentGuid.SerializedGuid_Editor = componentGuid.serializedGuid;
        }
    }

    private void OnValidate()
    {
        Debug.Log("onValidate");
        // Similar to on Serialize, but gets called on Copying a Component or Applying a Prefab
        // at a time that lets us detect what we are
        if (IsAssetOnDisk())
        {
            serializedGuid = null;
            _guid = Guid.Empty;

            foreach (ComponentGuid componentGuid in componentGUIDs)
            {
                componentGuid.serializedGuid = null;
                componentGuid.Guid = Guid.Empty;
            }
        }
        else
        {
            CreateOrRegisterGuid(ref _guid, ref serializedGuid, serializedGuid_Editor);

            var components = gameObject.GetComponents<Component>()
                .Where(component => !GuidComponentExcluders.Excluders.Contains(component.GetType())).ToList();
            if (components.Count == 0)
            {
                componentGUIDs.Clear();
            }

            foreach (Component component in components)
            {
                ComponentGuid componentGuid =
                    componentGUIDs.FirstOrDefault(c => c.cachedComponent == component);
                if (componentGuid == null)
                {
                    componentGuid = new ComponentGuid { cachedComponent = component };
                    componentGUIDs.Add(componentGuid);
                }

                CreateOrRegisterGuid(ref componentGuid.Guid, ref componentGuid.serializedGuid,
                    componentGuid.SerializedGuid_Editor);
                componentGuid.SerializedGuid_Editor = componentGuid.serializedGuid;
            }
        }

        serializedGuid_Editor = serializedGuid;
    }

    private void OnEnable()
    {
        EditorSceneManager.sceneSaving += OnSceneSaving;
    }

    public void OnDisable()
    {
        EditorSceneManager.sceneSaving -= OnSceneSaving;

        // Don't run for objects loaded from disk.
        if (GetInstanceID() < 0)
        {
            // Here we run a delayed call to solve a very specific edge case where the cachedComponent,
            // which is part of a prefab, is removed on the prefab instance, and then that cachedComponent removal modification
            // is applied via the removed cachedComponent itself (not via Apply All). For some reason, when this is done,
            // Unity creates an intermediary instance of this cachedComponent but does not call OnDestroy(),
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
    }
#endif

    /// <summary>
    ///     Get the Guid of the GameObject this GuidComponent is attached to.
    /// </summary>
    /// <returns>Guid of the GameObject. Guid.Empty if not found.</returns>
    public Guid GetGuid()
    {
        // Never return an invalid GUID
        if (_guid == Guid.Empty && IsSerializedGuidValid(serializedGuid))
        {
            _guid = new Guid(serializedGuid);
        }

        return _guid;
    }

    /// <summary>
    ///     Get the Guid of the Component on the same level as this GuidComponent.
    /// </summary>
    /// <remarks>This does not handle multiple components of the same type. Will just return the first component it finds.</remarks>
    /// <param name="type">Type of component to look for.</param>
    /// <returns>Guid of the Component. Guid.Empty if not found.</returns>
    public Guid GetGuid(Type type)
    {
        if (type == GameObjectType)
        {
            return GetGuid();
        }

        foreach (ComponentGuid componentGuid in componentGUIDs)
        {
            if (!componentGuid.IsTypeOrSubclassOf(type))
            {
                continue;
            }

            // Never return an invalid GUID
            if (componentGuid.Guid == Guid.Empty && IsSerializedGuidValid(componentGuid.serializedGuid))
            {
                componentGuid.Guid = new Guid(componentGuid.serializedGuid);
            }

            return componentGuid.Guid;
        }

        return Guid.Empty;
    }

    /// <summary>
    ///     Same as <see cref="GetGuid(Type)" />.
    /// </summary>
    /// <param name="component">The component which you want to find the guid of.</param>
    public Guid GetGuid(Component component)
    {
        if (component is GuidComponent)
        {
            return Guid.Empty;
        }

        foreach (ComponentGuid componentGuid in componentGUIDs)
        {
            if (componentGuid.cachedComponent != component)
            {
                continue;
            }

            // Never return an invalid GUID
            if (componentGuid.Guid == Guid.Empty && IsSerializedGuidValid(componentGuid.serializedGuid))
            {
                componentGuid.Guid = new Guid(componentGuid.serializedGuid);
            }

            return componentGuid.Guid;
        }

        return Guid.Empty;
    }

    /// <summary>
    ///     Tried to get the component from a given guid.
    /// </summary>
    /// <param name="guid">The guid of the component to find.</param>
    /// <returns>If found, returns the Component, otherwise null.</returns>
    public Component GetComponentFromGuid(Guid guid)
    {
        if (guid != _guid)
        {
            return componentGUIDs
                .Where(c => c.Guid == guid)
                .Select(componentGuid => componentGuid.cachedComponent)
                .FirstOrDefault();
        }

        return null;
    }

    // Let the manager know we are gone, so other objects no longer find this.
    public void OnDestroy()
    {
#if UNITY_EDITOR
        PrefabUtility.prefabInstanceReverting -= PrefabUtilityOnprefabInstanceReverting;
        _isDestroyed = true;
        if (_isAttemptingPrefabRevert)
        {
            _isAttemptingPrefabRevert = false;
            return;
        }

        Debug.Log("destroy");
        // This is used mainly for the case where the user deletes a GuidComponent from a prefab view.
        // This will then go through and unregister all GuidComponents that were instances of this prefab.
        if (this && IsAssetOnDisk())
        {
            GuidManager.Remove(PrefabStageUtility.GetPrefabStage(gameObject).assetPath);
        }
        else
#endif
        {
            GuidManager.Remove(_guid);
            foreach (ComponentGuid componentGuid in componentGUIDs)
            {
                GuidManager.Remove(componentGuid.Guid);
            }
        }
    }
}