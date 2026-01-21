using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

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
    [SerializeField]
    internal SerializableGuid _guid = SerializableGuid.Empty;

    [SerializeField] internal List<ComponentGuid> componentGUIDs = new List<ComponentGuid>();
    // Used to save out componentGUIDs as a way to prevent those values from getting reset when Reset() is triggered or when applying prefab.
    // Reference: https://discussions.unity.com/t/prevent-reset-from-clearing-out-serialized-fields/191838/3
    private readonly List<ComponentGuid> componentGUIDs_dump = new List<ComponentGuid>();

#if UNITY_EDITOR
    // This non-serialized copy of the serializedGuid is for editor use only.
    // It is intended to circumvent the issue of Guids being regenerated in a prefab when applying or reverting prefab modifications.
    // It attempts to do this as the non-serialized value does not get cleared on prefab apply or revert or operations of that nature.
    private byte[] serializedGuid_Editor;
#endif

#if UNITY_EDITOR
    public static event Action<GuidComponent> OnGameObjectGuidRequested;
    public static event Action<GuidComponent, ComponentGuid> OnComponentGuidRequested;
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
        return _guid.GetHashCode();
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
    internal void FindOrCreateGuid(ref SerializableGuid guid, byte[] serializedGuid_Editor)
#else
    internal void FindOrCreateGuid(ref Guid guid)
#endif
    {
#if UNITY_EDITOR
        if (IsSerializedGuidValid(serializedGuid_Editor))
        {
            Debug.Log("Found Cached Guid!");
            guid = SerializableGuid.Create(new Guid(serializedGuid_Editor));
        }
        else
        {
            Debug.Log("Requesting mapped or new game object guid...");
            // If we don't have a cached guid, then try find in mapping file. Whether found or not, this will fill this component's guid.
            OnGameObjectGuidRequested?.Invoke(this);
        }
#endif

        // If our serialized data is invalid, then we are a new object and need a new GUID
        if (guid == SerializableGuid.Empty)
        {
            guid = SerializableGuid.Create(Guid.NewGuid());
        }

        // Register with the GUID Manager so that GuidReferences can access this
        if (guid != SerializableGuid.Empty)
        {
            if (!GuidManager.Add(guid.Guid, this))
            {
                // If registration fails, we probably have a duplicate or invalid GUID, get us a new one.
                guid = SerializableGuid.Empty;
#if UNITY_EDITOR
                FindOrCreateGuid(ref guid, serializedGuid_Editor);
#else
                FindOrCreateGuid(ref guid);
#endif
            }
        }
    }

#if UNITY_EDITOR
    private void FindOrCreateComponentGuid(ComponentGuid componentGuid)
    {
        if (IsSerializedGuidValid(componentGuid.SerializedGuid_Editor))
        {
            Debug.Log("Found Cached Component Guid!");
            componentGuid.Guid = SerializableGuid.Create(new Guid(componentGuid.SerializedGuid_Editor));
        }
        else
        {
            Debug.Log("Requesting mapped or new component guid...");
            // If we don't have a cached guid, then try find in mapping file. Whether found or not, this will fill this component's guid.
            OnComponentGuidRequested?.Invoke(this, componentGuid);
        }

        // If our serialized data is invalid, then we are a new object and need a new GUID
        if (componentGuid.Guid == SerializableGuid.Empty)
        {
            componentGuid.Guid = SerializableGuid.Create(Guid.NewGuid());
        }

        // Register with the GUID Manager so that GuidReferences can access this
        if (componentGuid.Guid != SerializableGuid.Empty)
        {
            if (!GuidManager.Add(componentGuid.Guid.Guid, this))
            {
                // If registration fails, we probably have a duplicate or invalid GUID, get us a new one.
                componentGuid.Guid = SerializableGuid.Empty;
                FindOrCreateComponentGuid(componentGuid);
            }
        }
    }

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
        bool isPrefabAsset = PrefabUtility.IsPartOfPrefabAsset(this);
        bool isEditingInPrefabMode = IsEditingInPrefabMode();
        return isPrefabAsset || isEditingInPrefabMode;
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
            _guid = SerializableGuid.Empty;

            // Move all ComponentGuids over to the non-serialized dump list. See definition of componentGUIDs_dump for reasoning.
            componentGUIDs_dump.Clear();
            foreach (ComponentGuid componentGuid in componentGUIDs)
            {
                componentGuid.Guid = SerializableGuid.Empty;
                componentGUIDs_dump.Add(componentGuid);
            }
        }
        else
#endif
        {
            // Move all ComponentGuids over to the non-serialized dump list. See definition of componentGUIDs_dump for reasoning.
            componentGUIDs_dump.Clear();
            foreach (ComponentGuid componentGuid in componentGUIDs)
            {
                componentGUIDs_dump.Add(componentGuid);
            }
        }
    }

    // On load, we can go head a restore our system guids for later use
    public void OnAfterDeserialize()
    {
        if (componentGUIDs_dump != null && componentGUIDs_dump.Count > 0)
        {
            componentGUIDs.Clear();
            foreach (ComponentGuid componentGuid in componentGUIDs_dump)
            {
                componentGUIDs.Add(componentGuid);
            }
        }
    }

    private void Awake()
    {
#if UNITY_EDITOR
        if (IsAssetOnDisk())
        {
            return;
        }

        Debug.Log("Awake()");
        FindOrCreateGuid(ref _guid, serializedGuid_Editor);
        serializedGuid_Editor = _guid.Guid.ToByteArray();

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

            FindOrCreateComponentGuid(componentGuid);
            componentGuid.SerializedGuid_Editor = componentGuid.Guid.Guid.ToByteArray();
        }
#else
        FindOrCreateGuid(ref _guid);
        foreach (var componentGuid in componentGUIDs)
        {
            FindOrCreateGuid(ref componentGuid.Guid);
        }
#endif
    }

#if UNITY_EDITOR
    private void Reset()
    {
        if (IsAssetOnDisk())
        {
            return;
        }

        Debug.Log("Reset()");
        FindOrCreateGuid(ref _guid, serializedGuid_Editor);

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

            FindOrCreateComponentGuid(componentGuid);
            componentGuid.SerializedGuid_Editor = componentGuid.Guid.Guid.ToByteArray();
        }
    }

    internal void OnValidate()
    {
        if (IsAssetOnDisk())
        {
            return;
        }

        Debug.Log("OnValidate()");
        FindOrCreateGuid(ref _guid, serializedGuid_Editor);

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

            FindOrCreateComponentGuid(componentGuid);
            componentGuid.SerializedGuid_Editor = componentGuid.Guid.Guid.ToByteArray();
        }

        componentGUIDs.RemoveAll(guid => !guid.cachedComponent);
        componentGUIDs = componentGUIDs.OrderBy(guid => guid.cachedComponent.GetComponentIndex()).ToList();
    }
#endif

    /// <summary>
    ///     Get the Guid of the GameObject this GuidComponent is attached to.
    /// </summary>
    /// <returns>Guid of the GameObject. Guid.Empty if not found.</returns>
    public Guid GetGuid()
    {
        if (_guid.Equals(SerializableGuid.Empty))
        {
            return SerializableGuid.Empty.Guid;
        }

        return _guid.Guid;
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

        foreach (ComponentGuid componentGuid in componentGUIDs.Where(componentGuid =>
                     componentGuid.IsTypeOrSubclassOf(type)))
        {
            return componentGuid.Guid.Guid;
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

        foreach (ComponentGuid componentGuid in componentGUIDs.Where(componentGuid =>
                     componentGuid.cachedComponent == component))
        {
            return componentGuid.Guid.Guid;
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
        SerializableGuid serializableGuid = SerializableGuid.Create(guid);
        if (guid != _guid.Guid)
        {
            return componentGUIDs
                .Where(c => c.Guid == serializableGuid)
                .Select(componentGuid => componentGuid.cachedComponent)
                .FirstOrDefault();
        }

        return null;
    }

    // Let the manager know we are gone, so other objects no longer find this.
    public void OnDestroy()
    {
#if UNITY_EDITOR
        if (IsAssetOnDisk() && !EditorApplication.isPlaying)
        {
            return;
        }

        Debug.Log("OnDestroy()");

        // This is used mainly for the case where the user deletes a GuidComponent from a prefab view.
        // This will then go through and unregister all GuidComponents that were instances of this prefab.
        if (this && IsAssetOnDisk())
        {
            GuidManager.Remove(PrefabStageUtility.GetPrefabStage(gameObject).assetPath);
        }
        else
#endif
        {
            GuidManager.Remove(_guid.Guid);
            foreach (ComponentGuid componentGuid in componentGUIDs)
            {
                GuidManager.Remove(componentGuid.Guid.Guid);
            }
        }
    }
}

/// <summary>
///     Stores GUIDs for a cachedComponent on a GameObject
/// </summary>
[Serializable]
public class ComponentGuid : IEquatable<ComponentGuid>
{
    public Component cachedComponent;
    public SerializableGuid Guid;

#if UNITY_EDITOR
    // Store a copy of serializedGuid in a NonSerialized field, so we don't lose the GUID on Reset()
    // See GuidComponent.serializedGuid_Editor for more details about this.
    [NonSerialized] public byte[] SerializedGuid_Editor;
#endif

    public ComponentGuid() {}

    public ComponentGuid(Component cachedComponent)
    {
        this.cachedComponent = cachedComponent;
    }

    public ComponentGuid(Component cachedComponent, SerializableGuid guid)
    {
        this.cachedComponent = cachedComponent;
        Guid = guid;
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

        return Guid.Equals(other.Guid);
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
        return Guid.GetHashCode();
    }
}