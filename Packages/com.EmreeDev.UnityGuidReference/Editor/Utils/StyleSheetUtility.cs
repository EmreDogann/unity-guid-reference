using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
///     A static class for accessing resources used by this package.
/// </summary>
public static class StyleSheetUtility
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

    /// <summary> USS class name of the items in the list. </summary>
    public const string GuidComponentValueUssClassName = "guid-component__guid-value";

    /// <summary> USS applied by <see cref="ApplyCurrentTheme(VisualElement)" /> to add style variables. </summary>
    private const string VariablesContainerUssClassName = "uitk-inspector-variables";

    private static StyleSheet _inspectorHeaderStyle;
    private static StyleSheet _guidComponentStyle;
    private static StyleSheet _darkTheme;
    private static StyleSheet _lightTheme;
    // private static StyleSheet _editableLabelStyle;

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
                _inspectorHeaderStyle = GetAsset<StyleSheet>("Editor/UIToolkit-InspectorHeader/InspectorHeader.uss");
            }

            return _inspectorHeaderStyle;
        }
    }

    /// <summary> StyleSheet for <see cref="GuidComponent" /> </summary>
    public static StyleSheet GuidComponentStyle
    {
        get
        {
            if (!_guidComponentStyle)
            {
                _guidComponentStyle = GetAsset<StyleSheet>("Editor/GuidComponent.uss");
            }

            return _guidComponentStyle;
        }
    }

    private static StyleSheet DarkTheme
    {
        get
        {
            if (!_darkTheme)
            {
                _darkTheme = GetAsset<StyleSheet>("Editor/Utils/InspectorVariables-Dark.uss");
            }

            return _darkTheme;
        }
    }

    private static StyleSheet LightTheme
    {
        get
        {
            if (!_lightTheme)
            {
                _lightTheme = GetAsset<StyleSheet>("Editor/Utils/InspectorVariables-Light.uss");
            }

            return _lightTheme;
        }
    }

    /// <summary>
    ///     Pass a custom root element to this method to use the appropiate USS variables and class names for Unity's current skin.
    /// </summary>
    /// <param name="rootElement">The custom root element that will contain the variables. </param>
    public static void ApplyCurrentTheme(VisualElement rootElement)
    {
        bool isDark = EditorGUIUtility.isProSkin;

        StyleSheet style = isDark ? DarkTheme : LightTheme;
        rootElement.styleSheets.Add(style);
        rootElement.AddToClassList(VariablesContainerUssClassName);
    }

    private static T GetAsset<T>(string relativePath) where T : Object
    {
        string[] results =
            AssetDatabase.FindAssets($"a:packages t:StyleSheet glob:{relativePath}");
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