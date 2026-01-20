using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
///     A static class for accessing resources used by this package.
/// </summary>
public static class EditorAidResources
{
    private static string s_FolderPath;
    private static StyleSheet s_EditableLabelStyle;
    private static StyleSheet s_InspectorHeaderStyle;

    /// <summary> StyleSheet for <see cref="EditableLabel"/> </summary>
    // public static StyleSheet editableLabelStyle
    // {
    //     get
    //     {
    //         if (!s_EditableLabelStyle)
    //             s_EditableLabelStyle = GetAsset<StyleSheet>("EditableLabelStyle.uss");
    //         return s_EditableLabelStyle;
    //     }
    // }

    /// <summary> StyleSheet for <see cref="InspectorHeader" /> </summary>
    public static StyleSheet InspectorHeaderStyle
    {
        get
        {
            if (!s_InspectorHeaderStyle)
            {
                s_InspectorHeaderStyle = GetAsset<StyleSheet>("InspectorHeader.uss");
            }

            return s_InspectorHeaderStyle;
        }
    }

    // This is the most practical way I found to not depend on specific file paths or GUIDs which change when assets are
    // duplicated. It makes it easier for the assets to exist multiple times in different places in the same project.
    private static string folderPath
    {
        get
        {
            if (s_FolderPath == null)
            {
                s_FolderPath = "Packages/com.EmreeDev.UnityGuidReference/Editor";
            }

            return s_FolderPath;
        }
    }

    /// <summary>
    ///     Pass a custom root element to this method to use the appropiate USS variables and class names for Unity's current skin.
    /// </summary>
    /// <param name="rootElement">The custom root element that will contain the variables. </param>
    public static void ApplyCurrentTheme(VisualElement rootElement)
    {
        rootElement.styleSheets.Add(InspectorHeaderStyle);
    }

    private static T GetAsset<T>(string relativePath) where T : Object
    {
        string path = Path.Combine(folderPath, relativePath);

        if (Path.DirectorySeparatorChar != '/')
        {
            path = path.Replace(Path.DirectorySeparatorChar, '/');
        }

        return AssetDatabase.LoadAssetAtPath<T>(path);
    }
}