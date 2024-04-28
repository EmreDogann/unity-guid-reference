using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// Class to handle registering and accessing objects by GUID
public class GuidManager
{
    // for each GUID we need to know the Game Object it references
    // and an event to store all the callbacks that need to know when it is destroyed
    internal struct GuidInfo
    {
        public GuidComponent GuidComponent;
        public Guid Guid;

        public event Action<GameObject, Component> OnAdd;
        public event Action OnRemove;

        public GuidInfo(Guid guid, GuidComponent comp)
        {
            Guid = guid;
            GuidComponent = comp;
            OnAdd = null;
            OnRemove = null;
        }

        public void HandleAddCallback()
        {
            Component component = GuidComponent.GetComponentFromGuid(Guid);
            OnAdd?.Invoke(GuidComponent.gameObject, component);
        }

        public void HandleRemoveCallback()
        {
            OnRemove?.Invoke();
        }
    }

    // Singleton interface
    private static GuidManager _instance;

    // All the public API is static so you need not worry about creating an instance
    public static bool Add(Guid guid, GuidComponent guidComponent)
    {
        if (_instance == null)
        {
            _instance = new GuidManager();
        }

        return _instance.InternalAdd(guid, guidComponent);
    }

    public static void Remove(Guid guid)
    {
        if (_instance == null)
        {
            _instance = new GuidManager();
        }

        _instance.InternalRemove(guid);
    }

#if UNITY_EDITOR
    internal static void Remove(string prefabAssetPath)
    {
        if (_instance == null)
        {
            _instance = new GuidManager();
        }

        _instance.InternalRemove_Prefab(prefabAssetPath);
    }
#endif

    public static GameObject ResolveGuid(Guid guid, Action<GameObject, Component> onAddCallback,
        Action onRemoveCallback)
    {
        if (_instance == null)
        {
            _instance = new GuidManager();
        }

        return _instance.ResolveGuidInternal_GameObject(guid, onAddCallback, onRemoveCallback);
    }

    public static GameObject ResolveGuid(Guid guid, Action onDestroyCallback)
    {
        if (_instance == null)
        {
            _instance = new GuidManager();
        }

        return _instance.ResolveGuidInternal_GameObject(guid, null, onDestroyCallback);
    }

    public static GameObject ResolveGuid(Guid guid)
    {
        if (_instance == null)
        {
            _instance = new GuidManager();
        }

        return _instance.ResolveGuidInternal_GameObject(guid, null, null);
    }

    public static T ResolveGuid<T>(Guid guid, Action<GameObject, Component> onAddCallback, Action onRemoveCallback)
        where T : Component
    {
        if (_instance == null)
        {
            _instance = new GuidManager();
        }

        return _instance.ResolveGuidInternal_Component<T>(guid, onAddCallback, onRemoveCallback);
    }

    public static T ResolveGuid<T>(Guid guid, Action onDestroyCallback) where T : Component
    {
        if (_instance == null)
        {
            _instance = new GuidManager();
        }

        return _instance.ResolveGuidInternal_Component<T>(guid, null, onDestroyCallback);
    }

    public static T ResolveGuid<T>(Guid guid) where T : Component
    {
        if (_instance == null)
        {
            _instance = new GuidManager();
        }

        return _instance.ResolveGuidInternal_Component<T>(guid, null, null);
    }

    public static Component ResolveGuid(Guid guid, Type type)
    {
        if (_instance == null)
        {
            _instance = new GuidManager();
        }

        return _instance.ResolveGuidInternal_Component(guid, type);
    }

    public static bool ExistsGuid(Guid guid)
    {
        if (_instance == null)
        {
            _instance = new GuidManager();
        }

        return _instance.InternalExistsGuid(guid);
    }

    // instance data
    private readonly Dictionary<Guid, GuidInfo> _guidToObjectMap;

#if UNITY_EDITOR
    internal static Dictionary<Guid, GuidInfo> GetGuidInfos
    {
        get
        {
            if (_instance == null)
            {
                _instance = new GuidManager();
            }

            return _instance._guidToObjectMap;
        }
    }
    internal static event Action OnGuidAdded;
    internal static event Action OnGuidRemoved;
#endif

    private GuidManager()
    {
        _guidToObjectMap = new Dictionary<Guid, GuidInfo>();
    }

    private bool InternalExistsGuid(Guid guid)
    {
        return _guidToObjectMap.ContainsKey(guid);
    }

    private bool InternalAdd(Guid guid, GuidComponent guidComponent)
    {
        GuidInfo info = new GuidInfo(guid, guidComponent);

        if (_guidToObjectMap.TryAdd(guid, info))
        {
#if UNITY_EDITOR
            OnGuidAdded?.Invoke();
#endif
            return true;
        }

        GuidInfo existingInfo = _guidToObjectMap[guid];

        if (existingInfo.GuidComponent && existingInfo.GuidComponent.gameObject != guidComponent.gameObject)
        {
            // normally, a duplicate GUID is a big problem, means you won't necessarily be referencing what you expect
            if (Application.isPlaying)
            {
                Debug.AssertFormat(false, guidComponent,
                    "Guid Collision Detected between {0} and {1}.\nAssigning new Guid. Consider tracking runtime instances using a direct reference or other method.",
                    existingInfo.GuidComponent != null ? existingInfo.GuidComponent.gameObject.name : "NULL",
                    guidComponent != null ? guidComponent.gameObject.name : "NULL");
            }
            else
            {
                // however, at editor time, copying an object with a GUID will duplicate the GUID resulting in a collision and repair.
                // we warn about this just for pedantry reasons, and so you can detect if you are unexpectedly copying these components
                Debug.LogWarningFormat(guidComponent,
                    "Guid Collision Detected while creating {0}.\nAssigning new Guid.",
                    guidComponent.gameObject.name);
            }

            return false;
        }

        // if we already tried to find this GUID, but haven't set the game object to anything specific, copy any OnAdd callbacks then call them
        existingInfo.GuidComponent = info.GuidComponent;
        existingInfo.Guid = info.Guid;
        existingInfo.HandleAddCallback();

        _guidToObjectMap[guid] = existingInfo;

#if UNITY_EDITOR
        OnGuidAdded?.Invoke();
#endif

        return true;
    }

    private void InternalRemove(Guid guid)
    {
        if (_guidToObjectMap.TryGetValue(guid, out GuidInfo info))
        {
            // trigger all the destroy delegates that have registered
            info.HandleRemoveCallback();
        }

        bool result = _guidToObjectMap.Remove(guid);

#if UNITY_EDITOR
        if (result)
        {
            OnGuidRemoved?.Invoke();
        }
#endif
    }

#if UNITY_EDITOR
    private void InternalRemove_Prefab(string prefabAssetPath)
    {
        bool wasAnyRemoved = false;
        foreach (var entry in _guidToObjectMap.ToList())
        {
            if (!entry.Value.GuidComponent)
            {
                continue;
            }

            string path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(entry.Value.GuidComponent);
            if (path.Equals(prefabAssetPath))
            {
                Object obj =
                    PrefabUtility.GetCorrespondingObjectFromSourceAtPath(entry.Value.GuidComponent, prefabAssetPath);
                if (obj)
                {
                    bool result = _guidToObjectMap.Remove(entry.Key);

                    if (result)
                    {
                        wasAnyRemoved = true;
                    }
                }
            }
        }

        if (wasAnyRemoved)
        {
            OnGuidRemoved?.Invoke();
        }
    }
#endif

    // nice easy api to find a GUID, and if it works, register an on destroy callback
    // this should be used to register functions to cleanup any data you cache on finding
    // your target. Otherwise, you might keep components in memory by referencing them
    private GameObject ResolveGuidInternal_GameObject(Guid guid, Action<GameObject, Component> onAddCallback,
        Action onRemoveCallback)
    {
        if (guid == Guid.Empty)
        {
            return null;
        }

        if (_guidToObjectMap.TryGetValue(guid, out GuidInfo info))
        {
            if (!info.GuidComponent)
            {
                return null;
            }

            if (onAddCallback != null)
            {
                info.OnAdd += onAddCallback;
            }

            if (onRemoveCallback != null)
            {
                info.OnRemove += onRemoveCallback;
            }

            _guidToObjectMap[guid] = info;
            return info.GuidComponent.gameObject;
        }

        if (onAddCallback != null)
        {
            info.OnAdd += onAddCallback;
        }

        if (onRemoveCallback != null)
        {
            info.OnRemove += onRemoveCallback;
        }

        _guidToObjectMap.Add(guid, info);

        return null;
    }

    private T ResolveGuidInternal_Component<T>(Guid guid, Action<GameObject, Component> onAddCallback,
        Action onRemoveCallback) where T : Component
    {
        if (guid == Guid.Empty)
        {
            return null;
        }

        if (_guidToObjectMap.TryGetValue(guid, out GuidInfo info))
        {
            if (!info.GuidComponent)
            {
                return null;
            }

            if (onAddCallback != null)
            {
                info.OnAdd += onAddCallback;
            }

            if (onRemoveCallback != null)
            {
                info.OnRemove += onRemoveCallback;
            }

            _guidToObjectMap[guid] = info;
            return info.GuidComponent.GetComponentFromGuid(guid) as T;
        }

        if (onAddCallback != null)
        {
            info.OnAdd += onAddCallback;
        }

        if (onRemoveCallback != null)
        {
            info.OnRemove += onRemoveCallback;
        }

        _guidToObjectMap.Add(guid, info);

        return null;
    }

    private Component ResolveGuidInternal_Component(Guid guid, Type type)
    {
        if (guid == Guid.Empty)
        {
            return null;
        }

        if (_guidToObjectMap.TryGetValue(guid, out GuidInfo info))
        {
            if (!info.GuidComponent)
            {
                return null;
            }

            _guidToObjectMap[guid] = info;
            Component component = info.GuidComponent.GetComponentFromGuid(guid);
            if (component)
            {
                if (component.GetType() == type)
                {
                    return component;
                }

                return null;
            }
        }

        _guidToObjectMap.Add(guid, info);

        return null;
    }
}