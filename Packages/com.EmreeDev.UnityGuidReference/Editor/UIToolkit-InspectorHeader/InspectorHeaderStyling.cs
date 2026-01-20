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
                _inspectorHeaderStyle = GetAsset<StyleSheet>("UIToolkit-InspectorHeader/InspectorHeader.uss");
            }

            return _inspectorHeaderStyle;
        }
    }

    private static T GetAsset<T>(string relativePath) where T : Object
    {
        string[] results =
            AssetDatabase.FindAssets($"a:packages t:StyleSheet glob:Editor/{relativePath}");
        if (results.Length == 0)
        {
            Debug.LogError(
                "Cannot find InspectorHeader.uss! Make sure the package is installed as a package in the \"Packages\" folder!");
            return null;
        }

        GUID.TryParse(results[0], out GUID guid);
        return AssetDatabase.LoadAssetByGUID<T>(guid);
    }
}