using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
///     A static class for accessing resources used by this package.
/// </summary>
public static class InspectorHeaderStyling
{
    /// <summary> USS class name of the items in the list. </summary>
    public const string InspectorUssClassName = "uitk-inspector";
    /// <summary> USS class name of the inspector headers. </summary>
    public const string InspectorHeaderUssClassName = "uitk-inspector__header";
    /// <summary> USS class name of collapsed inspector headers. </summary>
    public const string InspectorHeaderCollapsedUssClassName = "uitk-inspector__header--collapsed";
    /// <summary> USS class name of inspector header foldouts. </summary>
    public const string InspectorHeaderFoldoutUssClassName = "uitk-inspector__header-foldout";
    /// <summary> USS class name of inspector header labels. </summary>
    public const string InspectorHeaderLabelUssClassName = "uitk-inspector__header-label";
    /// <summary> USS class name of inspector header buttons. </summary>
    public const string InspectorHeaderButtonUssClassName = "uitk-inspector__header-button";
    /// <summary> USS class name of the inspector header icon. </summary>
    public const string InspectorHeaderIconUssClassName = "uitk-inspector__header-icon";
    /// <summary> USS class name of the inspector header enable toggle (Mono Behaviours only). </summary>
    public const string InspectorHeaderEnableToggleUssClassName = "uitk-inspector__header-enable-toggle";

    private static string _folderPath;
    private static StyleSheet _editableLabelStyle;
    private static StyleSheet _inspectorHeaderStyle;

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
            if (!_inspectorHeaderStyle)
            {
                _inspectorHeaderStyle = GetAsset<StyleSheet>("InspectorHeader.uss");
            }

            return _inspectorHeaderStyle;
        }
    }

    // This is the most practical way I found to not depend on specific file paths or GUIDs which change when assets are
    // duplicated. It makes it easier for the assets to exist multiple times in different places in the same project.
    private static string FolderPath
    {
        get
        {
            if (_folderPath == null)
            {
                _folderPath = "Packages/com.EmreeDev.UnityGuidReference/Editor";
            }

            return _folderPath;
        }
    }

    private static T GetAsset<T>(string relativePath) where T : Object
    {
        string path = Path.Combine(FolderPath, relativePath);

        if (Path.DirectorySeparatorChar != '/')
        {
            path = path.Replace(Path.DirectorySeparatorChar, '/');
        }

        return AssetDatabase.LoadAssetAtPath<T>(path);
    }
}