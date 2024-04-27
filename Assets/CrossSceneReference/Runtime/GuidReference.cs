using System;
using UnityEngine;

[Serializable]
public class GuidReference : BaseGuidReference<GameObject>
{
    public GuidReference() {}
    public GuidReference(GuidComponent target) : base(target) {}
}

[Serializable]
public class GuidReference<T> : BaseGuidReference<T> where T : Component
{
    private protected T CachedComponentReference;
    public GuidReference() {}
    public GuidReference(GuidComponent target) : base(target) {}

    public T component
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
        if (componentRef != null)
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

    protected override void PreOnAfterDeserialize()
    {
        CachedComponentReference = null;
    }
}