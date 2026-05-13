using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
///     Gives a GameObject and its Components a stable, non-replicatable Globally Unique Identifier.
///     It can be used to reference a specific instance of an object no matter where it is.
///     This can also be used for other systems, such as Save/Load game
/// </summary>
[ExecuteAlways] [DisallowMultipleComponent]
public class GuidComponent : MonoBehaviour
#if UNITY_EDITOR
    , ISerializationCallbackReceiver
#endif
{
    private static readonly Type GameObjectType = typeof(GameObject);

    [SerializeField] [HideInInspector]
    internal ComponentGuid transformGuid = new ComponentGuid();

    [SerializeField] [HideInInspector]
    internal List<ComponentGuid> componentGuids = new List<ComponentGuid>();

#if UNITY_EDITOR
    [SerializeField] [HideInInspector]
    internal List<ComponentGuid> orphanedComponentGuids = new List<ComponentGuid>();

    public interface IGuidMappingsHandler
    {
        void InitializeComponent(GuidComponent component);
        void OrphanGuid(ComponentGuid componentGuid);
        void RemoveOrphanedGuid(ComponentGuid componentGuid);
        void RemoveComponent(GuidComponent guidComponent);
        bool CheckIsDuplicate(GuidComponent guidComponent);
    }

    internal static IGuidMappingsHandler MappingsHandler;
    [NonSerialized]
    private bool _isInitializedAndReady;
#endif

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
        if (!Equals(transformGuid.serializableGuid, other.transformGuid.serializableGuid)) return false;
        if (componentGuids.Count != other.componentGuids.Count) return false;
        for (int i = 0; i < componentGuids.Count; i++)
            if (!componentGuids[i].Equals(other.componentGuids[i])) return false;
        return true;
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

    #region Get Guids

    public IReadOnlyList<ComponentGuid> GetComponentGuids()
    {
#if UNITY_EDITOR
        if (!_isInitializedAndReady)
        {
            OnValidate();
        }
#endif

        return componentGuids;
    }

    /// <summary>
    ///     Checks if there are GUIDs for multiple components of the same type.
    /// </summary>
    /// <param name="T">Type of component to check.</param>
    /// <returns>True if there are multiple components of the same type. False if not.</returns>
    public bool HasMultipleComponentsOf(Type T)
    {
#if UNITY_EDITOR
        if (!_isInitializedAndReady)
        {
            OnValidate();
        }
#endif

        int count = 0;
        foreach (ComponentGuid componentGuid in componentGuids)
        {
            if (componentGuid.IsTypeOrSubclassOf(T) && ++count > 1)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Checks if there are GUIDs for multiple components of the same type.
    /// </summary>
    /// <typeparam name="T">Type of component to check.</typeparam>
    /// <returns>True if there are multiple components of the same type. False if not.</returns>
    public bool HasMultipleComponentsOf<T>() where T : Component
    {
#if UNITY_EDITOR
        if (!_isInitializedAndReady)
        {
            OnValidate();
        }
#endif

        int count = 0;
        foreach (ComponentGuid componentGuid in componentGuids)
        {
            if (componentGuid.IsTypeOrSubclassOf<T>() && ++count > 1)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Get the Guid of the GameObject this GuidComponent is attached to.
    /// </summary>
    /// <returns>Guid of the GameObject. Guid.Empty if not found.</returns>
    public Guid GetGuid()
    {
#if UNITY_EDITOR
        if (!_isInitializedAndReady)
        {
            OnValidate();
        }
#endif

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
#if UNITY_EDITOR
        if (!_isInitializedAndReady)
        {
            OnValidate();
        }
#endif

        if (type == GameObjectType)
        {
            return GetGuid();
        }

        foreach (ComponentGuid componentGuid in componentGuids)
        {
            if (componentGuid.IsTypeOrSubclassOf(type))
                return componentGuid.serializableGuid.Guid;
        }

        return Guid.Empty;
    }

    /// <summary>
    ///     Same as <see cref="GetGuid(Type)" />.
    /// </summary>
    /// <param name="component">The component which you want to find the guid of.</param>
    public Guid GetGuid(Component component)
    {
#if UNITY_EDITOR
        if (!_isInitializedAndReady)
        {
            OnValidate();
        }
#endif

        if (component is GuidComponent)
        {
            return transformGuid.serializableGuid.Guid;
        }

        foreach (ComponentGuid componentGuid in componentGuids)
        {
            if (componentGuid.CachedComponent == component)
                return componentGuid.serializableGuid.Guid;
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
#if UNITY_EDITOR
        if (!_isInitializedAndReady)
        {
            OnValidate();
        }
#endif

        SerializableGuid serializableGuid = SerializableGuid.Create(guid);
        if (guid != transformGuid.serializableGuid.Guid)
        {
            foreach (ComponentGuid c in componentGuids)
            {
                if (c.serializableGuid == serializableGuid)
                    return c.CachedComponent;
            }
        }

        return null;
    }

    #endregion

    private void FindOrCreateGuid(ComponentGuid componentGuid)
    {
        // If our serialized data is invalid, either something went wrong, or we are a new object instantiated at runtime,
        // either way we need a new GUID
        if (transformGuid.serializableGuid == SerializableGuid.Empty)
        {
            transformGuid.serializableGuid = SerializableGuid.Create(Guid.NewGuid());
        }

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

    private void InitializeGuids()
    {
        transformGuid ??= new ComponentGuid();
        transformGuid.OwningGameObject = gameObject;

#if UNITY_EDITOR
        MappingsHandler?.InitializeComponent(this);
#else
        FindOrCreateGuid(transformGuid);

        foreach (ComponentGuid componentGuid in componentGuids)
        {
            FindOrCreateGuid(componentGuid);
        }
#endif
    }

    private void Awake()
    {
#if !UNITY_EDITOR
        FindOrCreateGuid(transformGuid);
        foreach (ComponentGuid componentGuid in componentGuids)
        {
            FindOrCreateGuid(componentGuid);
        }
#endif
    }

#if UNITY_EDITOR
    public void OnBeforeSerialize() {}

    public void OnAfterDeserialize()
    {
        // This is needed in edge cases such as:
        //   - User calls Object.Instantiate() on an object containing this Component.
        //     This clones the Component without calling it's OnValidate() function, and Awake/OnEnable functions
        //     are dependent on if the gameobject is active, so this is the most reliable way to ensure the GUID is
        //     always valid when duplicated/cloned.
        _isInitializedAndReady = false;
        EditorApplication.delayCall += () =>
        {
            // If the component was setup while this delayCall was queued
            // (maybe the user calls it's public functions immediately afterward),
            // then we don't need to setup.
            if (!_isInitializedAndReady)
            {
                if (MappingsHandler != null && MappingsHandler.CheckIsDuplicate(this))
                {
                    OnValidate();
                }
            }
        };
    }

    // Prevent "Reset" context menu item from clearing guids.
    private void Reset()
    {
        OnValidate();
    }

    internal void OnValidate()
    {
        if (PrefabCheckerUtility.IsPartOfAnyPrefab(this) &&
            (!PrefabCheckerUtility.IsPartOfValidPrefabInstance(this) || PrefabCheckerUtility.IsInPrefabStage(this)))
        {
            // If component is part of prefab asset, set its values to null.
            transformGuid = new ComponentGuid();
            componentGuids.Clear();
            orphanedComponentGuids.Clear();

            _isInitializedAndReady = true;
            return;
        }

        InitializeGuids();
        componentGuids.RemoveAll(guid =>
        {
            bool isMissing = !guid.CachedComponent;
            if (isMissing)
            {
                orphanedComponentGuids.Add(guid);
                MappingsHandler?.OrphanGuid(guid);
            }

            return isMissing;
        });

        _isInitializedAndReady = true;
    }
#endif

    // Let the manager know we are gone, so other objects no longer find this.
    public void OnDestroy()
    {
#if UNITY_EDITOR
        MappingsHandler?.RemoveComponent(this);
#else
        GuidManager.Remove(transformGuid.serializableGuid.Guid);
        foreach (ComponentGuid componentGuid in componentGuids)
        {
            GuidManager.Remove(componentGuid.serializableGuid.Guid);
        }
#endif
    }
}


/// <summary>
///     Stores GUIDs for a cachedComponent on a GameObject
/// </summary>
[Serializable]
public class ComponentGuid : IEquatable<ComponentGuid>
{
    [SerializeField]
    public SerializableGuid serializableGuid;

    [SerializeField]
    private Component cachedComponent;
    public Component CachedComponent
    {
        get => cachedComponent;
        internal set
        {
            if (cachedComponent == value)
            {
                return;
            }

            cachedComponent = value;
#if UNITY_EDITOR
            GlobalObjectId globalObjectID = GlobalObjectId.GetGlobalObjectIdSlow(value);
            // GlobalObjectID.identifierType 2 = Scene Object
            if (globalObjectID.identifierType == 2)
            {
                GlobalComponentId = globalObjectID.ToString();
            }
            else if (globalObjectID.identifierType != 0)
            {
                // identifierType 0 = Null/transient (e.g. mid-prefab reimport) — skip silently,
                // the existing GlobalComponentId remains valid.
                Debug.LogError(
                    "[GuidComponent] Error: ComponentGuids can only be created for scene game objects! Setting to empty.");
                GlobalComponentId = string.Empty;
            }

            if (value)
            {
                cachedOwnerTypeReference = value.GetType().FullName + ", " + value.GetType().Assembly.GetName().Name;
            }
#endif
        }
    }

    [SerializeField]
    private GameObject owningGameObject;
    public GameObject OwningGameObject
    {
        get => owningGameObject;
        internal set
        {
            if (owningGameObject == value)
            {
                return;
            }

            owningGameObject = value;
#if UNITY_EDITOR
            GlobalObjectId globalObjectID = GlobalObjectId.GetGlobalObjectIdSlow(value);
            // GlobalObjectID.identifierType 2 = Scene Object
            if (globalObjectID.identifierType == 2)
            {
                GlobalGameObjectId = globalObjectID.ToString();
            }
            else if (globalObjectID.identifierType != 0)
            {
                // identifierType 0 = Null/transient (e.g. mid-prefab reimport) — skip silently,
                // the existing GlobalGameObjectId remains valid.
                Debug.LogError(
                    "[GuidComponent] Error: ComponentGuids can only be created for scene game objects! Setting to empty.");
                GlobalGameObjectId = string.Empty;
            }
#endif
        }
    }

#if UNITY_EDITOR
    [SerializeField]
    private string globalGameObjectId = string.Empty;
    public string GlobalGameObjectId
    {
        get => globalGameObjectId;
        internal set => globalGameObjectId = value;
    }

    [SerializeField]
    private string globalComponentId = string.Empty;
    public string GlobalComponentId
    {
        get => globalComponentId;
        internal set => globalComponentId = value;
    }

    [SerializeField] [HideInInspector]
    private string cachedOwnerTypeReference = string.Empty;

    [NonSerialized]
    private Type _cachedOwnerType;
    public Type CachedOwnerType
    {
        get
        {
            if (_cachedOwnerType == null && !string.IsNullOrEmpty(cachedOwnerTypeReference))
                _cachedOwnerType = Type.GetType(cachedOwnerTypeReference);
            return _cachedOwnerType;
        }
    }

    internal string CachedOwnerTypeReference => cachedOwnerTypeReference;

    internal void SetCachedOwnerTypeReference(string typeRef)
    {
        cachedOwnerTypeReference = typeRef;
        _cachedOwnerType = null;
    }

    public ComponentGuid() {}

    public ComponentGuid(string gameObjectId, GameObject gameObject, string componentId, Component component)
    {
        globalGameObjectId = gameObjectId;
        globalComponentId = componentId;
        owningGameObject = gameObject;
        cachedComponent = component;

        if (cachedComponent)
        {
            cachedOwnerTypeReference = cachedComponent.GetType().FullName + ", " +
                                       cachedComponent.GetType().Assembly.GetName().Name;
        }
    }

    public bool IsRootComponent()
    {
        return !string.IsNullOrEmpty(globalGameObjectId) && string.IsNullOrEmpty(globalComponentId) && string.IsNullOrEmpty(cachedOwnerTypeReference);
    }

    public Type GetOwningType()
    {
        if (IsRootComponent())
        {
            return typeof(GameObject);
        }

        return cachedComponent ? cachedComponent.GetType() : CachedOwnerType;
    }
#endif

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