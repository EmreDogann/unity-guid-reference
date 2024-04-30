using System;
using UnityEngine;

/// <summary>
///     References a GameObject that has a GuidComponent either in the same scene, or in a different scene.
/// </summary>
[Serializable]
public class GuidReference : BaseGuidReference<GameObject>
{
    public GuidReference() {}
    public GuidReference(GuidComponent target) : base(target) {}
}

/// <summary>
///     Reference a Component that is assigned a Guid by a GuidComponent either in the same scene, or in a different scene.
/// </summary>
/// <typeparam name="T">The type of the Component to reference. Type must derive from Component.</typeparam>
[Serializable]
public class GuidReference<T> : BaseGuidReference<T> where T : Component
{
    private protected T CachedComponentReference;

#if UNITY_EDITOR
    [SerializeField] private string cachedGOName;
#endif

    public GuidReference() {}
    public GuidReference(GuidComponent target) : base(target) {}

    /// <summary>
    ///     Try and get the referenced Component. Returns null if not found.
    /// </summary>
    public T Component
    {
        get
        {
            if (IsCacheSet)
            {
                return CachedComponentReference;
            }

            CachedComponentReference = GuidManager.ResolveGuid<T>(Guid, AddDelegate, RemoveDelegate);
            IsCacheSet = true;
            return CachedComponentReference;
        }
    }

    protected override void GuidAdded(GameObject go, Component component)
    {
        T componentRef = component as T;
        if (componentRef)
        {
            CachedComponentReference = componentRef;
            // OnGuidAdded(value);
        }

        base.GuidAdded(go, component);
    }

    protected override void GuidRemoved()
    {
        CachedComponentReference = null;
        base.GuidRemoved();
    }

    protected override void Pre_OnAfterDeserialize()
    {
        CachedComponentReference = null;
    }
}