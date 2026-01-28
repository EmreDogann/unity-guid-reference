using System;
using UnityEditorInternal;
using UnityEngine;

/// <summary>
///     <para>
///         An attribute that specifies a file location relative to the Project folder or Unity's preferences folder. Additional resources:
///         FilePathAttribute.Location.
///     </para>
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class FilePathAttribute : Attribute
{
    private string m_FilePath;
    private string m_RelativePath;
    private readonly Location m_Location;

    internal string filepath
    {
        get
        {
            if (m_FilePath == null && m_RelativePath != null)
            {
                m_FilePath = CombineFilePath(m_RelativePath, m_Location);
                m_RelativePath = null;
            }

            return m_FilePath;
        }
    }

    public FilePathAttribute(string relativePath, Location location)
    {
        m_RelativePath = !string.IsNullOrEmpty(relativePath)
            ? relativePath
            : throw new ArgumentException("Invalid relative path (it is empty)");
        m_Location = location;
    }

    private static string CombineFilePath(string relativePath, Location location)
    {
        if (relativePath[0] == '/')
        {
            relativePath = relativePath.Substring(1);
        }

        switch (location)
        {
            case Location.PreferencesFolder:
                return $"{InternalEditorUtility.unityPreferencesFolder}/{relativePath}";
            case Location.ProjectFolder:
                return relativePath;
            default:
                Debug.LogError("Unhandled enum: " + location);
                return relativePath;
        }
    }

    /// <summary>
    ///     <para>Specifies the folder location that Unity uses together with the relative path provided in the FilePathAttribute constructor.</para>
    /// </summary>
    public enum Location
    {
      /// <summary>
      ///     <para>Use this location to save a file relative to the preferences folder. Useful for per-user files that are across all projects.</para>
      /// </summary>
      PreferencesFolder,
      /// <summary>
      ///     <para>Use this location to save a file relative to the Project Folder. Useful for per-project files (not shared between projects).</para>
      /// </summary>
      ProjectFolder
    }
}