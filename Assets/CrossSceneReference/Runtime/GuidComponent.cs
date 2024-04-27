using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

// This component gives a GameObject a stable, non-replicatable Globally Unique IDentifier.
// It can be used to reference a specific instance of an object no matter where it is.
// This can also be used for other systems, such as Save/Load game
[ExecuteInEditMode] [DisallowMultipleComponent]
public class GuidComponent : MonoBehaviour, ISerializationCallbackReceiver
{
    [Serializable]
    public class ComponentGuid
    {
        public Component component;
        public Guid Guid;
        public byte[] serializedGuid;

        public bool IsType(Type type)
        {
            return component.GetType() == type;
        }
    }

    // System guid we use for comparison and generation
    private Guid guid = Guid.Empty;
    [SerializeField] [HideInInspector] private List<ComponentGuid> componentGUIDs = new List<ComponentGuid>();

    private readonly Type GameObjectType = typeof(GameObject);

    // Unity's serialization system doesn't know about System.Guid, so we convert to a byte array
    // Fun fact, we tried using strings at first, but that allocated memory and was twice as slow
    [SerializeField]
    private byte[] serializedGuid;

    private bool IsSerializedGuidValid(byte[] serializedGuidArray)
    {
        return serializedGuidArray != null && serializedGuidArray.Length == 16;
    }

    public IReadOnlyList<ComponentGuid> GetComponentGUIDs()
    {
        return componentGUIDs;
    }

    public bool IsGuidAssigned()
    {
        return guid != Guid.Empty;
    }

    // public bool IsGuidAssigned<T>() where T : Component
    // {
    //     foreach (ComponentGuid componentGuid in componentGUIDs)
    //     {
    //         if (componentGuid.IsType<T>())
    //         {
    //             return componentGuid.Guid != Guid.Empty;
    //         }
    //     }
    //
    //     return false;
    // }

    // When de-serializing or creating this component, we want to either restore our serialized GUID
    // or create a new one.
    private void CreateGuid()
    {
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
                CreateGuid();
            }
        }
    }

    private void CreateGuid(ComponentGuid componentGuid)
    {
        // if our serialized data is invalid, then we are a new object and need a new GUID
        if (componentGuid.serializedGuid == null || componentGuid.serializedGuid.Length != 16)
        {
#if UNITY_EDITOR
            // if in editor, make sure we aren't a prefab of some kind
            if (IsAssetOnDisk())
            {
                return;
            }

#endif
            componentGuid.Guid = Guid.NewGuid();
            componentGuid.serializedGuid = componentGuid.Guid.ToByteArray();

#if UNITY_EDITOR
            Undo.RecordObject(this, "Added Component GUID");
            // If we are creating a new GUID for a prefab instance of a prefab, but we have somehow lost our prefab connection
            // force a save of the modified prefab instance properties
            if (PrefabUtility.IsPartOfNonAssetPrefabInstance(this))
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(this);
            }
#endif
        }
        else if (componentGuid.Guid == Guid.Empty)
        {
            // otherwise, we should set our system guid to our serialized guid
            componentGuid.Guid = new Guid(componentGuid.serializedGuid);
        }

        // register with the GUID Manager so that other components can access this
        if (componentGuid.Guid != Guid.Empty)
        {
            if (!GuidManager.Add(componentGuid.Guid, this))
            {
                // if registration fails, we probably have a duplicate or invalid GUID, get us a new one.
                componentGuid.serializedGuid = null;
                componentGuid.Guid = Guid.Empty;
                CreateGuid(componentGuid);
            }
        }
    }

#if UNITY_EDITOR
    private bool IsEditingInPrefabMode()
    {
        if (EditorUtility.IsPersistent(this))
        {
            // if the game object is stored on disk, it is a prefab of some kind, despite not returning true for IsPartOfPrefabAsset =/
            return true;
        }

        // If the GameObject is not persistent let's determine which stage we are in first because getting Prefab info depends on it
        StageHandle mainStage = StageUtility.GetMainStageHandle();
        StageHandle currentStage = StageUtility.GetStageHandle(gameObject);
        if (currentStage != mainStage)
        {
            PrefabStage prefabStage = PrefabStageUtility.GetPrefabStage(gameObject);
            if (prefabStage != null)
            {
                return true;
            }
        }

        return false;
    }

    private bool IsAssetOnDisk()
    {
        return PrefabUtility.IsPartOfPrefabAsset(this) || IsEditingInPrefabMode();
    }
#endif

    // We cannot allow a GUID to be saved into a prefab, and we need to convert to byte[]
    public void OnBeforeSerialize()
    {
#if UNITY_EDITOR
        // This lets us detect if we are a prefab instance or a prefab asset.
        // A prefab asset cannot contain a GUID since it would then be duplicated when instanced.
        if (gameObject != null && IsAssetOnDisk())
        {
            serializedGuid = null;
            guid = Guid.Empty;

            foreach (ComponentGuid componentGuid in componentGUIDs)
            {
                componentGuid.serializedGuid = null;
                componentGuid.Guid = Guid.Empty;
            }
        }
        else
#endif
        {
            if (guid != Guid.Empty)
            {
                serializedGuid = guid.ToByteArray();
            }

            foreach (ComponentGuid componentGuid in componentGUIDs)
            {
                if (componentGuid.Guid != Guid.Empty)
                {
                    componentGuid.serializedGuid = componentGuid.Guid.ToByteArray();
                }
            }
        }
    }

    // On load, we can go head a restore our system guid for later use
    public void OnAfterDeserialize()
    {
        if (IsSerializedGuidValid(serializedGuid))
        {
            guid = new Guid(serializedGuid);
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
        EditorSceneManager.sceneSaving += OnSceneSaving;
#endif

        CreateGuid();

        foreach (Component component in gameObject.GetComponents<Component>())
        {
            ComponentGuid componentGuid = componentGUIDs.FirstOrDefault(c => c.component == component);
            if (!GuidComponentExcluders.Excluders.Contains(component.GetType()) && componentGuid == null)
            {
                componentGuid = new ComponentGuid { component = component };
                componentGUIDs.Add(componentGuid);
            }

            if (componentGuid != null)
            {
                CreateGuid(componentGuid);
            }
        }
    }

    private void OnSceneSaving(Scene scene, string path)
    {
        // Check for stale components
        for (int i = componentGUIDs.Count - 1; i >= 0; i--)
        {
            if (componentGUIDs[i].component == null)
            {
                GuidManager.Remove(componentGUIDs[i].Guid);
                componentGUIDs.RemoveAt(i);
            }
            else if (!GuidManager.ExistsGuid(componentGUIDs[i].Guid))
            {
                componentGUIDs.RemoveAt(i);
            }
        }

        // Check for new components
        foreach (Component component in gameObject.GetComponents<Component>())
        {
            ComponentGuid componentGuid = componentGUIDs.FirstOrDefault(c => c.component == component);
            if (!GuidComponentExcluders.Excluders.Contains(component.GetType()) && componentGuid == null)
            {
                componentGuid = new ComponentGuid { component = component };
                componentGUIDs.Add(componentGuid);
            }

            if (componentGuid != null)
            {
                CreateGuid(componentGuid);
            }
        }
    }

    private void OnValidate()
    {
#if UNITY_EDITOR
        // similar to on Serialize, but gets called on Copying a Component or Applying a Prefab
        // at a time that lets us detect what we are
        if (IsAssetOnDisk())
        {
            serializedGuid = null;
            guid = Guid.Empty;

            foreach (ComponentGuid componentGuid in componentGUIDs)
            {
                componentGuid.serializedGuid = null;
                componentGuid.Guid = Guid.Empty;
            }
        }
        else
#endif
        {
            CreateGuid();

            foreach (Component component in gameObject.GetComponents<Component>())
            {
                ComponentGuid componentGuid = componentGUIDs.FirstOrDefault(c => c.component == component);
                if (!GuidComponentExcluders.Excluders.Contains(component.GetType()) && componentGuid == null)
                {
                    componentGuid = new ComponentGuid { component = component };
                    componentGUIDs.Add(componentGuid);
                }

                if (componentGuid != null)
                {
                    CreateGuid(componentGuid);
                }
            }
        }
    }

    // Never return an invalid GUID
    public Guid GetGuid()
    {
        if (guid == Guid.Empty && IsSerializedGuidValid(serializedGuid))
        {
            guid = new Guid(serializedGuid);
        }

        return guid;
    }

    // public Guid GetGuid<T>() where T : Object
    // {
    //     if (typeof(T) == GameObjectType)
    //     {
    //         return GetGuid();
    //     }
    //
    //     // foreach (ComponentGuid componentGuid in componentGUIDs)
    //     // {
    //     //     if (componentGuid.IsType<T>() && componentGuid.Guid == Guid.Empty &&
    //     //         IsSerializedGuidValid(componentGuid.serializedGuid))
    //     //     {
    //     //         componentGuid.Guid = new Guid(componentGuid.serializedGuid);
    //     //         return componentGuid.Guid;
    //     //     }
    //     // }
    //
    //     // Try and find the requested component and assign a GUID to it at runtime. Last resort.
    //     // T component = gameObject.GetComponent<T>();
    //     // if (component)
    //     // {
    //     //     ComponentGuid componentGuid = new ComponentGuid
    //     //         { type = new SerializableSystemType(typeof(T)) };
    //     //     componentGUIDs.Add(componentGuid);
    //     //     CreateGuid(componentGuid);
    //     //
    //     //     return componentGuid.Guid;
    //     // }
    //
    //     return Guid.Empty;
    // }

    public Guid GetGuid(Type type)
    {
        if (type == GameObjectType)
        {
            return GetGuid();
        }

        foreach (ComponentGuid componentGuid in componentGUIDs)
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

    public Component GetComponentFromGuid(Guid guid)
    {
        if (guid != this.guid)
        {
            return componentGUIDs.Where(c => c.Guid == guid).Select(componentGuid => componentGuid.component)
                .FirstOrDefault();
        }

        return null;
    }

    // let the manager know we are gone, so other objects no longer find this
    public void OnDestroy()
    {
#if UNITY_EDITOR
        EditorSceneManager.sceneSaving -= OnSceneSaving;
#endif

        GuidManager.Remove(guid);

        foreach (ComponentGuid componentGuid in componentGUIDs)
        {
            GuidManager.Remove(componentGuid.Guid);
        }
    }
}