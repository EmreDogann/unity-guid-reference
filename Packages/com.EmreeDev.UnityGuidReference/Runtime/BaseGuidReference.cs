using System;
using UnityEngine;
using Object = UnityEngine.Object;
#if UNITY_EDITOR
using UnityEditor;
#endif

// Ideally this would be a struct, but we need the ISerializationCallbackReceiver
[Serializable]
public abstract class BaseGuidReference<T> : ISerializationCallbackReceiver where T : Object
{
    // Cache the referenced Game Object if we find one for performance
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

    // Create concrete delegates to avoid boxing.
    // When called 10,000 times, boxing would allocate ~1MB of GC Memory
    private protected Action<GameObject, Component> AddDelegate;
    private protected Action RemoveDelegate;

    /// <summary>
    ///     Try and get the referenced GameObject. Returns null if not found.
    /// </summary>
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

    // If the referenced guid was registered with the GuidManager, cache its GameObject for performance.
    protected virtual void GuidAdded(GameObject go, Component component)
    {
        CachedGoReference = go;
    }

    // Reset state if referenced guid was unregistered from GuidManager.
    protected virtual void GuidRemoved()
    {
        CachedGoReference = null;
        IsCacheSet = false;
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