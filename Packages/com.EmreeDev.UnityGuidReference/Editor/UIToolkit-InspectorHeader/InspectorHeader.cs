using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.Presets;
using UnityEditor.UIElements;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

public class InspectorHeader : VisualElement
{
    public class DrawSettings
    {
        public bool DrawEnableToggle = true;
        public bool DrawIcon = true;
        public bool DrawLabel = true;
        public bool DrawHelpIcon = true;
        public bool DrawPresetIcon = true;
        public bool DrawSettingsIcon = true;
        public bool EnableContextMenu = true;
        public string HeaderTitleOverride = "";
    }

    private readonly SerializedObject _backingObject;
    private readonly InspectorElement _inspectorElement;
    private readonly IMGUIContainer _footerElement;
    private readonly DrawSettings _drawSettings;
    private readonly Action<GenericMenu, Rect, Object[], int> _showContextMenu;

    public InspectorHeader(SerializedObject serializedObject, InspectorElement inspectorElement,
        IMGUIContainer footer, DrawSettings drawSettings = null)
    {
        MethodInfo contextMenuMethod = typeof(GenericMenu).GetMethod("ObjectContextDropDown",
            BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance, null,
            new[] { typeof(Rect), typeof(Object[]), typeof(int) }, null);

        if (contextMenuMethod != null)
        {
            _showContextMenu =
                (Action<GenericMenu, Rect, Object[], int>)Delegate.CreateDelegate(
                    typeof(Action<GenericMenu, Rect, Object[], int>), contextMenuMethod, false);
        }

        styleSheets.Add(StyleSheetUtility.InspectorHeaderStyle);
        AddToClassList(StyleSheetUtility.InspectorUssClassName);

        _backingObject = serializedObject;
        _inspectorElement = inspectorElement;
        _footerElement = footer;
        _drawSettings = drawSettings;

        AssignObject();
    }

    /// <summary>
    ///     Override this method in a child class to customize inspector headers as a whole.
    /// </summary>
    /// <param name="serializedObject">The inspected object</param>
    /// <param name="inspector">The inspector element under the header.</param>
    /// <param name="footer">The footer element under the inspector.</param>
    /// <returns>The created header.</returns>
    private VisualElement CreateHeader(SerializedObject serializedObject, InspectorElement inspector,
        IMGUIContainer footer, DrawSettings drawSettings)
    {
        focusable = true;
        delegatesFocus = true;

        StyleSheetUtility.ApplyCurrentTheme(this);

        Object target = serializedObject.targetObject;
        // TODO: HideFlags.NotEditable
        // var header = new Disabler(() => !serializedObject.IsEditable())
        VisualElement header = new VisualElement { focusable = true };
        VisualElement headerContainer = header.contentContainer;
        headerContainer.AddToClassList(StyleSheetUtility.InspectorHeaderUssClassName);

        bool wasExpanded = InternalEditorUtility.GetIsInspectorExpanded(target);
        SetItemExpanded(wasExpanded, headerContainer, footer, target, inspector);

        Foldout foldout = AddHeaderFoldout(headerContainer, wasExpanded);
        foldout.RegisterValueChangedCallback(e =>
        {
            SetItemExpanded(e.newValue, headerContainer, footer, target, inspector);
        });

        header.AddManipulator(new DragAndClickManipulator
        {
            onStartDragging = () => StartDraggingItem(target),
            onClick = () =>
            {
                foldout.value = !foldout.value;
                header.Focus();
            }
        });

        header.RegisterCallback<MouseDownEvent>(e =>
        {
            if (e.button == 1)
            {
                ShowInspectorContextMenu(new Rect(e.mousePosition, default), headerContainer, serializedObject,
                    drawSettings);
            }
        });

        AddPrelabelHeaderElements(headerContainer, serializedObject, drawSettings);
        AddHeaderLabel(headerContainer, serializedObject, drawSettings);
        AddPostlabelHeaderElements(headerContainer, serializedObject, drawSettings);

        return header;
    }

    private void SetItemExpanded(bool expanded, VisualElement header, VisualElement footer, Object target,
        InspectorElement inspector)
    {
        inspector.style.display = expanded ? DisplayStyle.Flex : DisplayStyle.None;
        header.EnableInClassList(StyleSheetUtility.InspectorHeaderCollapsedUssClassName, !expanded);
        if (expanded)
        {
            footer.style.marginTop = 0;
        }
        else
        {
            footer.style.marginTop = -5;
        }

        InternalEditorUtility.SetIsInspectorExpanded(target, expanded);
    }

    private Foldout AddHeaderFoldout(VisualElement header, bool expanded)
    {
        Foldout foldout = new Foldout { pickingMode = PickingMode.Ignore, value = expanded };
        foldout.Query().Descendents<VisualElement>().ForEach(el => el.pickingMode = PickingMode.Ignore);
        foldout.AddToClassList(StyleSheetUtility.InspectorHeaderFoldoutUssClassName);
        header.Add(foldout);
        return foldout;
    }

    /// <summary>
    ///     Override this method to customize the elements before the header's label.
    /// </summary>
    /// <param name="header">The header</param>
    /// <param name="serializedObject">The inspected object</param>
    private void AddPrelabelHeaderElements(VisualElement header, SerializedObject serializedObject,
        DrawSettings drawSettings)
    {
        if (drawSettings.DrawIcon)
        {
            Image icon = new Image { image = AssetPreview.GetMiniThumbnail(serializedObject.targetObject) };
            icon.AddToClassList(StyleSheetUtility.InspectorHeaderIconUssClassName);
            header.Add(icon);
        }

        Toggle toggle = new Toggle();
        toggle.AddToClassList(StyleSheetUtility.InspectorHeaderEnableToggleUssClassName);
        toggle.BindProperty(serializedObject.FindProperty("m_Enabled"));
        if (!drawSettings.DrawEnableToggle || serializedObject.targetObject is not MonoBehaviour)
        {
            toggle.SetEnabled(false);
            toggle.visible = false;
        }

        header.Add(toggle);
    }

    /// <summary>
    ///     Override this method to customize the header's label.
    /// </summary>
    /// <param name="header">The header</param>
    /// <param name="serializedObject">The inspected object</param>
    private void AddHeaderLabel(VisualElement header, SerializedObject serializedObject,
        DrawSettings drawSettings)
    {
        // TODO: Renaming Component Name?
        // var label = new EditableLabel { bindingPath = "m_Name", isDelayed = true };
        // label.AddToClassList(StyleSheetUtility.InspectorHeaderLabelUssClassName);
        // label.editOnDoubleClick = false;
        // label.emptyTextLabel = ObjectNames.NicifyVariableName(serializedObject.targetObject.GetType().Name);
        // header.Add(label);
        // header.RegisterCallback<MouseDownEvent>(e =>
        // {
        //     if (e.altKey && e.button == 0)
        //     {
        //         label.BeginEditing();
        //     }
        // });

        if (drawSettings.DrawLabel)
        {
            Label label = new Label { pickingMode = PickingMode.Ignore, focusable = true };
            label.text = string.IsNullOrEmpty(drawSettings.HeaderTitleOverride)
                ? ObjectNames.NicifyVariableName(serializedObject.targetObject.GetType().Name)
                : drawSettings.HeaderTitleOverride;
            label.AddToClassList(StyleSheetUtility.InspectorHeaderLabelUssClassName);
            header.Add(label);
        }
    }

    /// <summary>
    ///     Override this method to customize the elements after the header's label.
    /// </summary>
    /// <param name="header">The header</param>
    /// <param name="serializedObject">The inspected object</param>
    private void AddPostlabelHeaderElements(VisualElement header, SerializedObject serializedObject,
        DrawSettings drawSettings)
    {
        Object target = serializedObject.targetObject;
        Type targetType = target.GetType();

        if (drawSettings.DrawHelpIcon)
        {
            // Check for attribute because Help.HasHelpForObject always returns true for most custom objects.
            bool hasHelp = Attribute.IsDefined(targetType, typeof(HelpURLAttribute));
            bool hasTooltip = Attribute.IsDefined(targetType, typeof(TooltipAttribute));

            Button help = new Button();
            help.AddToClassList(StyleSheetUtility.InspectorHeaderButtonUssClassName);
            help.style.backgroundImage = EditorGUIUtility.IconContent("_Help").image as Texture2D;
            help.SetEnabled(hasHelp || hasTooltip);
            help.visible = hasHelp || hasTooltip;
            if (hasHelp)
            {
                help.tooltip = $"Open Help for {targetType.Name}.";
                help.clicked += () => Help.ShowHelpForObject(target);
            }

            if (hasTooltip)
            {
                TooltipAttribute tooltipAttr =
                    (TooltipAttribute)Attribute.GetCustomAttributes(targetType, typeof(TooltipAttribute))[0];
                help.tooltip = tooltipAttr.tooltip;
            }

            header.Add(help);
        }

        if (drawSettings.DrawPresetIcon && new PresetType(target).IsValid() &&
            (target.hideFlags & HideFlags.NotEditable) == 0)
        {
            Button presets = new Button();
            presets.AddToClassList(StyleSheetUtility.InspectorHeaderButtonUssClassName);
            presets.style.backgroundImage = EditorGUIUtility.IconContent("Preset.Context").image as Texture2D;
            presets.clicked += () => ShowPresetSelector(serializedObject);
            header.Add(presets);
        }

        if (drawSettings.DrawSettingsIcon)
        {
            Button settings = new Button();
            settings.AddToClassList(StyleSheetUtility.InspectorHeaderButtonUssClassName);
            settings.style.backgroundImage = EditorGUIUtility.IconContent("_Menu").image as Texture2D;
            settings.clicked += () =>
                ShowInspectorContextMenu(settings.worldBound, header, serializedObject, drawSettings);
            header.Add(settings);
        }
    }

    /// <summary>
    ///     Override this method to add custom menu items to the header's context menu.
    /// </summary>
    /// <param name="menu">The context menu</param>
    /// <param name="header">The header</param>
    /// <param name="serializedObject">The inspected object</param>
    private void AddItemsToContextMenu(GenericMenu menu, VisualElement header,
        SerializedObject serializedObject) {}

    private void ShowInspectorContextMenu(Rect position, VisualElement header, SerializedObject serializedObject,
        DrawSettings drawSettings)
    {
        if (!drawSettings.EnableContextMenu)
        {
            return;
        }

        // TODO: HideFlags.NotEditable
        // if (!serializedObject.IsEditable())
        // {
        //     return;
        // }

        GenericMenu menu = new GenericMenu();
        // TODO: Editable Label?
        // Object target = serializedObject.targetObject;

//         var editableLabel = header?.Q<EditableLabel>(null, StyleSheetUtility.InspectorHeaderLabelUssClassName);
//         if (editableLabel != null
//             && editableLabel.style.display != DisplayStyle.None
//             && editableLabel.style.visibility != Visibility.Hidden)
//         {
// #if UNITY_EDITOR_OSX
//             var editNameLabel = new GUIContent("Edit Name (⌥ + Click)");
// #else
//             GUIContent editNameLabel = new GUIContent("Edit Name (Alt + Click)");
// #endif
//             menu.AddItem(editNameLabel, false, () => editableLabel.BeginEditing());
//         }

        AddItemsToContextMenu(menu, header, serializedObject);

        position.position = GUIUtility.GUIToScreenPoint(position.position);
        _showContextMenu?.Invoke(menu, position, serializedObject.targetObjects, 0);
    }

    private void ShowPresetSelector(SerializedObject serializedObject)
    {
        Object target = serializedObject.targetObject;
        if (!new PresetType(target).IsValid() || (target.hideFlags & HideFlags.NotEditable) != 0)
        {
            return;
        }

        PresetSelector.ShowSelector(serializedObject.targetObjects, null, true);
    }

    private void AssignObject()
    {
        Clear();

        SerializedProperty scriptProp = _backingObject.FindProperty("m_Script");
        if (scriptProp.objectReferenceValue == null)
        {
            if (scriptProp.objectReferenceInstanceIDValue != 0)
            {
                AssignControlsForInvalidScript();
            }

            return;
        }

        VisualElement header = CreateHeader(_backingObject, _inspectorElement, _footerElement, _drawSettings);

        if (header != null)
        {
            Add(header);
        }
    }

    private void AssignControlsForInvalidScript()
    {
        VisualElement header = new VisualElement { style = { height = 22 } };
        header.AddToClassList(StyleSheetUtility.InspectorHeaderUssClassName);
        VisualElement body = new VisualElement();
        body.style.paddingTop = 3;
        body.style.paddingBottom = 3;
        body.style.paddingRight = 0;
        body.style.paddingLeft = 15;

        Foldout foldout = AddHeaderFoldout(header, true);
        foldout.RegisterValueChangedCallback(e =>
        {
            header.EnableInClassList(StyleSheetUtility.InspectorHeaderCollapsedUssClassName, !e.newValue);
            body.style.display = e.newValue ? DisplayStyle.Flex : DisplayStyle.None;
        });

        Image icon = new Image { image = EditorGUIUtility.IconContent("Warning").image };
        icon.AddToClassList(StyleSheetUtility.InspectorHeaderIconUssClassName);
        header.Add(icon);

        Label label = new Label("Object With Invalid Script");
        label.AddToClassList(StyleSheetUtility.InspectorHeaderLabelUssClassName);
        header.Add(label);

        header.AddManipulator(new DragAndClickManipulator
        {
            onStartDragging = () => StartDraggingItem(_backingObject.targetObject),
            onClick = () => foldout.value = !foldout.value
        });

        body.Add(new HelpBox(
            "This object's script is invalid. Make sure it doesn't have errors, its" +
            " class has the same name as its file, and it's the right type.", HelpBoxMessageType.Warning));

        body.Add(new Button(() => Selection.activeEntityId = _backingObject.targetObject.GetEntityId())
        {
            text = "Select Object With Invalid Script"
        });

        Add(header);
        Add(body);
    }

    /// <summary> Start dragging an item to reorder the list. Only call this from mouse events where a button is pressed to avoid errors.</summary>
    private void StartDraggingItem(Object draggingObject)
    {
        DragAndDrop.PrepareStartDrag();
        DragAndDrop.objectReferences = new[] { draggingObject };
        DragAndDrop.StartDrag(DragAndDrop.objectReferences.Length > 1
            ? "<Multiple>"
            : ObjectNames.GetDragAndDropTitle(draggingObject));
    }
}