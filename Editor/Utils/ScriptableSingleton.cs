using System.IO;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

/// <summary>
///     <para>Generic class for storing Editor state.</para>
/// </summary>
public class ScriptableSingleton<T> : ScriptableObject where T : ScriptableObject
{
    private static T s_Instance;

    public static T instance
    {
        get
        {
            if (s_Instance == null)
            {
                CreateAndLoad();
            }

            return s_Instance;
        }
    }

    public ScriptableSingleton()
    {
        if (s_Instance == null)
        {
            s_Instance = (object)this as T;
        }
    }

    protected static void InitializeSingleton()
    {
        if (s_Instance == null)
        {
            CreateAndLoad();
        }
    }

    private static void CreateAndLoad()
    {
        string filePath = GetFilePath();
        if (!string.IsNullOrEmpty(filePath))
        {
            InternalEditorUtility.LoadSerializedFileAndForget(filePath);
        }

        if (s_Instance != null)
        {
            return;
        }

        CreateInstance<T>().hideFlags = HideFlags.DontSave | HideFlags.HideInHierarchy;
    }

    protected static void ReloadInPlace()
    {
        string filePath = GetFilePath();
        if (!string.IsNullOrEmpty(filePath))
        {
            var loadedObject = InternalEditorUtility.LoadSerializedFileAndForget(filePath);
            if (loadedObject != null && loadedObject[0] != null)
            {
                EditorUtility.CopySerialized(loadedObject[0], s_Instance);
                DestroyImmediate(loadedObject[0], false);
            }
        }
    }

    protected virtual void Save(bool saveAsText)
    {
        if (s_Instance == null)
        {
            Debug.LogError("Cannot save ScriptableSingleton: no instance!");
        }
        else
        {
            string filePath = GetFilePath();
            if (!string.IsNullOrEmpty(filePath))
            {
                string directoryName = Path.GetDirectoryName(filePath);
                if (!Directory.Exists(directoryName))
                {
                    Directory.CreateDirectory(directoryName);
                }

                InternalEditorUtility.SaveToSerializedFileAndForget(new T[1]
                {
                    s_Instance
                }, filePath, saveAsText);
            }
            else
            {
                Debug.LogWarning(
                    $"Saving has no effect. Your class '{GetType()}' is missing the FilePathAttribute. Use this attribute to specify where to save your ScriptableSingleton.\nOnly call Save() and use this attribute if you want your state to survive between sessions of Unity.");
            }
        }
    }

    protected static string GetFilePath()
    {
        foreach (object customAttribute in typeof(T).GetCustomAttributes(true))
        {
            if (customAttribute is FilePathAttribute)
            {
                return (customAttribute as FilePathAttribute).filepath;
            }
        }

        return string.Empty;
    }
}