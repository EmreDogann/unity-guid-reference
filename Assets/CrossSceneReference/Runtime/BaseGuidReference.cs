﻿using System;
using UnityEngine;
using Object = UnityEngine.Object;
#if UNITY_EDITOR
using UnityEditor;
#endif

// This call is the type used by any other code to hold a reference to an object by GUID
// If the target object is loaded, it will be returned, otherwise, NULL will be returned
// This always works in Game Objects, so calling code will need to use GetComponent<>
// or other methods to track down the specific objects need by any given system

// Ideally this would be a struct, but we need the ISerializationCallbackReciever
[Serializable]
public abstract class BaseGuidReference<T> : ISerializationCallbackReceiver where T : Object
{
    // Cache the referenced Game Object and component if we find one for performance
    private protected GameObject CachedGoReference;
    private protected bool IsCacheSet;

    // Store our GUID in a form that Unity can save
    [SerializeField]
    private byte[] serializedGuid;
    private protected Guid Guid;

#if UNITY_EDITOR
    // Decorate with some extra info in Editor so we can inform a user of what that GUID means
    [SerializeField]
    private string cachedName;
    [SerializeField]
    private SceneAsset cachedScene;
#endif

    // Set up events to let users register to cleanup their own cached references on destroy or to cache off values
    // public event Action<T> OnGuidAdded = delegate {};
    // public event Action OnGuidRemoved = delegate {};

    // Create concrete delegates to avoid boxing.
    // When called 10,000 times, boxing would allocate ~1MB of GC Memory
    private protected Action<GameObject, Component> AddDelegate;
    private protected Action RemoveDelegate;

    // Optimized accessor, and ideally the only code you ever call on this class
    public GameObject GameObject
    {
        get
        {
            if (IsCacheSet)
            {
                return CachedGoReference;
            }

            CachedGoReference = GuidManager.ResolveGuid(Guid, AddDelegate, RemoveDelegate);
            IsCacheSet = true;

            return CachedGoReference;
        }
    }

    protected BaseGuidReference() {}

    protected BaseGuidReference(GuidComponent target)
    {
        Guid = target.GetGuid();
    }

    protected virtual void GuidAdded(GameObject go, Component component)
    {
        CachedGoReference = go;
    }

    protected virtual void GuidRemoved()
    {
        CachedGoReference = null;
        IsCacheSet = false;
        // OnGuidRemoved();
    }

    // Convert system guid to a format unity likes to work with
    void ISerializationCallbackReceiver.OnBeforeSerialize()
    {
        serializedGuid = Guid.ToByteArray();
    }

    // Used mainly so derived classes can inject their own code to run before base implementation.
    protected virtual void Pre_OnAfterDeserialize() {}

    // Convert from byte array to system guid and reset state
    void ISerializationCallbackReceiver.OnAfterDeserialize()
    {
        Pre_OnAfterDeserialize();

        CachedGoReference = null;
        IsCacheSet = false;
        if (serializedGuid == null || serializedGuid.Length != 16)
        {
            serializedGuid = new byte[16];
        }

        Guid = new Guid(serializedGuid);

        AddDelegate = GuidAdded;
        RemoveDelegate = GuidRemoved;
    }
}