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
///     Gives a GameObject and its Components a stable, non-replicatable Globally Unique Identifier.
///     It can be used to reference a specific instance of an object no matter where it is.
///     This can also be used for other systems, such as Save/Load game
/// </summary>
[ExecuteAlways] [DisallowMultipleComponent]
public class GuidComponent : MonoBehaviour, ISerializationCallbackReceiver
{
    private static readonly Type GameObjectType = typeof(GameObject);

    // System SerializableGuid we use for comparison and generation
    [SerializeField]
    internal ComponentGuid transformGuid;
    // Used to save out transformGuid as a way to prevent those serialized values from getting reset when Reset() is triggered or when applying prefab.
    // Reference: https://discussions.unity.com/t/prevent-reset-from-clearing-out-serialized-fields/191838/3
    private ComponentGuid _transformGuidDump;

    [SerializeField] internal List<ComponentGuid> componentGuids = new List<ComponentGuid>();
    // Used to save out componentGUIDs as a way to prevent those serialized values from getting reset when Reset() is triggered or when applying prefab.
    // Reference: https://discussions.unity.com/t/prevent-reset-from-clearing-out-serialized-fields/191838/3
    private readonly List<ComponentGuid> _componentGuidsDump = new List<ComponentGuid>();

#if UNITY_EDITOR
    private readonly List<Component> _componentGuidCandidates = new List<Component>();

    public static event Action<ComponentGuid> OnGuidRequested;
    public static event Action<ComponentGuid> OnCacheGuid;
    public static event Action<ComponentGuid> OnGuidRemoved;
#endif

    public IReadOnlyList<ComponentGuid> GetComponentGuids()
    {
        return componentGuids;
    }

    public IReadOnlyList<Component> GetComponentGuidCandidates()
    {
        return _componentGuidCandidates;
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
        return Equals(transformGuid.serializableGuid, other.transformGuid.serializableGuid) &&
               componentGuids.SequenceEqual(other.componentGuids);
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
        return transformGuid.GetHashCode();
    }

    #endregion

    /// <summary>
    ///     Checks if there are GUIDs for multiple components of the same type.
    /// </summary>
    /// <param name="T">Type of component to check.</param>
    /// <returns>True if there are multiple components of the same type. False if not.</returns>
    public bool HasMultipleComponentsOf(Type T)
    {
        return componentGuids.FindAll(componentGuid => componentGuid.IsTypeOrSubclassOf(T)).Count > 1;
    }

    /// <summary>
    ///     Checks if there are GUIDs for multiple components of the same type.
    /// </summary>
    /// <typeparam name="T">Type of component to check.</typeparam>
    /// <returns>True if there are multiple components of the same type. False if not.</returns>
    public bool HasMultipleComponentsOf<T>() where T : Component
    {
        return componentGuids.FindAll(componentGuid => componentGuid.IsTypeOrSubclassOf<T>()).Count > 1;
    }

    // When de-serializing or creating this GuidComponent, we want to either restore our serialized GUID or create a new one.
    // If the SerializableGuid already exists and is valid, then this function will just register the SerializableGuid with the GuidManager.
    private void FindOrCreateGuid(ComponentGuid componentGuid)
    {
#if UNITY_EDITOR
        if (componentGuid.serializableGuid != SerializableGuid.Empty)
        {
            Debug.Log("Found Cached Guid!");
            OnCacheGuid?.Invoke(componentGuid);
        }
        else
        {
            Debug.Log(
                $"Requesting mapped or new {(componentGuid.CachedComponent ? componentGuid.CachedComponent.GetType() + " " : "")}SerializableGuid...");
            // If we don't have a cached SerializableGuid, then try find in mapping file. Whether found or not, this will fill this component's SerializableGuid.
            OnGuidRequested?.Invoke(componentGuid);
        }
#else
        // If our serialized data is invalid, either something went wrong, or we are a new object instantiated at runtime,
        // either way we need a new GUID
        if (transformGuid.serializableGuid == SerializableGuid.Empty)
        {
            transformGuid.serializableGuid = SerializableGuid.Create(Guid.NewGuid());
        }
#endif

        if (componentGuid.CachedComponent is Transform)
        {
            // Register with the GUID Manager so that GuidReferences can access this
            if (componentGuid.serializableGuid != SerializableGuid.Empty)
            {
                if (!GuidManager.Add(componentGuid.serializableGuid.Guid, this))
                {
                    // If registration fails, we maybe have a duplicate or invalid GUID, get us a new one.
                    componentGuid.serializableGuid = SerializableGuid.Empty;
                    FindOrCreateGuid(componentGuid);
                }
            }
        }
    }

#if UNITY_EDITOR
    private bool IsEditingInPrefabMode()
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

    void ISerializationCallbackReceiver.OnBeforeSerialize()
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
            transformGuid.serializableGuid = SerializableGuid.Empty;
            _transformGuidDump = transformGuid;

            // Move all ComponentGuids over to the non-serialized dump list. See definition of componentGuidsDump for reasoning.
            _componentGuidsDump.Clear();
            foreach (ComponentGuid componentGuid in componentGuids)
            {
                componentGuid.serializableGuid = SerializableGuid.Empty;
                _componentGuidsDump.Add(componentGuid);
            }
        }
        else
#endif
        {
            _transformGuidDump = transformGuid;
            // Move all ComponentGuids over to the non-serialized dump list. See definition of componentGuidsDump for reasoning.
            _componentGuidsDump.Clear();
            foreach (ComponentGuid componentGuid in componentGuids)
            {
                _componentGuidsDump.Add(componentGuid);
            }
        }
    }

    // On load, we can go head a restore our system guids for later use
    void ISerializationCallbackReceiver.OnAfterDeserialize()
    {
        if (_transformGuidDump != null)
        {
            transformGuid = _transformGuidDump;
        }

        if (_componentGuidsDump != null && _componentGuidsDump.Count > 0)
        {
            componentGuids.Clear();
            foreach (ComponentGuid componentGuid in _componentGuidsDump)
            {
                componentGuids.Add(componentGuid);
            }
        }
    }

    private void InitializeGuids()
    {
        if (transformGuid == null)
        {
            transformGuid = new ComponentGuid();
        }

        transformGuid.OwningGameObject = gameObject;

        FindOrCreateGuid(transformGuid);

        _componentGuidCandidates.Clear();
        // Look for new components on the GameObject. Exclude component types specified in GuidComponentExcluders.
        foreach (Component component in gameObject.GetComponents<Component>())
        {
            if (GuidComponentExcluders.Excluders.Contains(component.GetType()))
            {
                continue;
            }

            ComponentGuid componentGuid = componentGuids.FirstOrDefault(c => c.CachedComponent == component);
            if (componentGuid == null)
            {
                _componentGuidCandidates.Add(component);
            }
            else
            {
                FindOrCreateGuid(componentGuid);
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
        InitializeGuids();
#else
        FindOrCreateGuid(transformGuid);
        foreach (ComponentGuid componentGuid in componentGuids)
        {
            FindOrCreateGuid(componentGuid);
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
        InitializeGuids();
    }

    internal void OnValidate()
    {
        if (IsAssetOnDisk())
        {
            return;
        }

        Debug.Log("OnValidate()");
        InitializeGuids();

        componentGuids.RemoveAll(guid =>
        {
            bool isMissing = !guid.CachedComponent;
            if (isMissing)
            {
                OnGuidRemoved?.Invoke(guid);
            }

            return isMissing;
        });
        componentGuids = componentGuids.OrderBy(guid => guid.CachedComponent.GetComponentIndex()).ToList();
    }
#endif

    /// <summary>
    ///     Get the Guid of the GameObject this GuidComponent is attached to.
    /// </summary>
    /// <returns>Guid of the GameObject. Guid.Empty if not found.</returns>
    public Guid GetGuid()
    {
        if (transformGuid.serializableGuid.Equals(SerializableGuid.Empty))
        {
            return SerializableGuid.Empty.Guid;
        }

        return transformGuid.serializableGuid.Guid;
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

        foreach (ComponentGuid componentGuid in componentGuids.Where(componentGuid =>
                     componentGuid.IsTypeOrSubclassOf(type)))
        {
            return componentGuid.serializableGuid.Guid;
        }

        return Guid.Empty;
    }

    /// <summary>
    ///     Same as <see cref="GetGuid(Type)" />.
    /// </summary>
    /// <param name="component">The component which you want to find the SerializableGuid of.</param>
    public Guid GetGuid(Component component)
    {
        if (component is GuidComponent)
        {
            return Guid.Empty;
        }

        foreach (ComponentGuid componentGuid in componentGuids.Where(componentGuid =>
                     componentGuid.CachedComponent == component))
        {
            return componentGuid.serializableGuid.Guid;
        }

        return Guid.Empty;
    }

    /// <summary>
    ///     Tried to get the component from a given SerializableGuid.
    /// </summary>
    /// <param name="guid">The SerializableGuid of the component to find.</param>
    /// <returns>If found, returns the Component, otherwise null.</returns>
    public Component GetComponentFromGuid(Guid guid)
    {
        SerializableGuid serializableGuid = SerializableGuid.Create(guid);
        if (guid != transformGuid.serializableGuid.Guid)
        {
            return componentGuids
                .Where(c => c.serializableGuid == serializableGuid)
                .Select(componentGuid => componentGuid.CachedComponent)
                .FirstOrDefault();
        }

        return null;
    }

    // Let the manager know we are gone, so other objects no longer find this.
    public void OnDestroy()
    {
#if UNITY_EDITOR
        Debug.Log("OnDestroy()");

        if (IsAssetOnDisk() && !EditorApplication.isPlaying)
        {
            return;
        }

        OnGuidRemoved?.Invoke(transformGuid);
        foreach (ComponentGuid componentGuid in componentGuids)
        {
            OnGuidRemoved?.Invoke(componentGuid);
        }
#endif
        {
            GuidManager.Remove(transformGuid.serializableGuid.Guid);
            foreach (ComponentGuid componentGuid in componentGuids)
            {
                GuidManager.Remove(componentGuid.serializableGuid.Guid);
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
    public SerializableGuid serializableGuid;

    [SerializeField] [HideInInspector]
    private Component _cachedComponent;
    public Component CachedComponent
    {
        get => _cachedComponent;
        internal set
        {
            if (_cachedComponent == value)
            {
                return;
            }

            _cachedComponent = value;
#if UNITY_EDITOR
            GlobalObjectId globalObjectID = GlobalObjectId.GetGlobalObjectIdSlow(value);
            // GlobalObjectID.identifierType 2 = Scene Object
            if (globalObjectID.identifierType == 2)
            {
                GlobalComponentId = globalObjectID.ToString();
            }
            else
            {
                Debug.LogError(
                    "[GuidComponent] Error: ComponentGuids can only be created for scene game objects! Setting to empty.");
                GlobalComponentId = string.Empty;
            }
#endif
        }
    }

    [SerializeField] [HideInInspector]
    private GameObject _owningGameObject;
    public GameObject OwningGameObject
    {
        get => _owningGameObject;
        internal set
        {
            if (_owningGameObject == value)
            {
                return;
            }

            _owningGameObject = value;
#if UNITY_EDITOR
            GlobalObjectId globalObjectID = GlobalObjectId.GetGlobalObjectIdSlow(value);
            // GlobalObjectID.identifierType 2 = Scene Object
            if (globalObjectID.identifierType == 2)
            {
                GlobalGameObjectId = globalObjectID.ToString();
            }
            else
            {
                Debug.LogError(
                    "[GuidComponent] Error: ComponentGuids can only be created for scene game objects! Setting to empty.");
                GlobalGameObjectId = string.Empty;
            }
#endif
        }
    }

#if UNITY_EDITOR
    [SerializeField] [HideInInspector]
    private string _globalGameObjectId = string.Empty;
    public string GlobalGameObjectId
    {
        get => _globalGameObjectId;
        internal set => _globalGameObjectId = value;
    }

    [SerializeField] [HideInInspector]
    private string _globalComponentId = string.Empty;
    public string GlobalComponentId
    {
        get
        {
            if (_cachedComponent)
            {
                return _globalComponentId;
            }

            _globalComponentId = string.Empty;
            return _globalComponentId;
        }
        internal set => _globalComponentId = value;
    }

    public bool IsRootComponent()
    {
        return !string.IsNullOrEmpty(_globalGameObjectId) && string.IsNullOrEmpty(_globalComponentId);
    }

    public Type GetOwningType()
    {
        if (IsRootComponent())
        {
            return typeof(GameObject);
        }

        return _cachedComponent ? _cachedComponent.GetType() : null;
    }
#endif

    public bool IsTypeOrSubclassOf(Type type)
    {
        return _cachedComponent.GetType() == type || _cachedComponent.GetType().IsSubclassOf(type);
    }

    public bool IsTypeOrSubclassOf<T>() where T : Component
    {
        return _cachedComponent.GetType() == typeof(T) || _cachedComponent.GetType().IsSubclassOf(typeof(T));
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

        return serializableGuid.Equals(other.serializableGuid);
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
        return serializableGuid.GetHashCode();
    }
}