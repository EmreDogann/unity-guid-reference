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
    /// <summary> USS class name of elements of this type. </summary>
    public new static readonly string ussClassName = "editor-aid-list-of-inspectors";
    /// <summary> USS class name of the items in the list. </summary>
    public static readonly string inspectorItemUssClassName = "editor-aid-list-of-inspectors__inspector-item";
    /// <summary> USS class name of the inspector headers. </summary>
    public static readonly string itemHeaderUssClassName = "editor-aid-list-of-inspectors__item-header";
    /// <summary> USS class name of collapsed inspector headers. </summary>
    public static readonly string itemHeaderCollapsedUssClassName =
        "editor-aid-list-of-inspectors__item-header--collapsed";
    /// <summary> USS class name of inspector header foldouts. </summary>
    public static readonly string itemHeaderFoldoutUssClassName =
        "editor-aid-list-of-inspectors__item-header-foldout";
    /// <summary> USS class name of inspector header labels. </summary>
    public static readonly string itemHeaderLabelUssClassName = "editor-aid-list-of-inspectors__item-header-label";
    /// <summary> USS class name of inspector header buttons. </summary>
    public static readonly string itemHeaderButtonUssClassName =
        "editor-aid-list-of-inspectors__item-header-button";
    /// <summary> USS class name of the inspector header icon. </summary>
    public static readonly string itemHeaderIconUssClassName = "editor-aid-list-of-inspectors__item-header-icon";
    /// <summary> USS class name of the inspector header enable toggle (monobehaviours only). </summary>
    public static readonly string itemHeaderEnableToggleUssClassName =
        "editor-aid-list-of-inspectors__item-header-enable-toggle";
    [Obsolete("There's no custom tooltip element for item headers anymore.")]
    public static readonly string itemHeaderTooltipUssClassName = "editor-aid-list-control__item-header-tooltip";

    private static readonly Action<GenericMenu, Rect, Object[], int> s_ShowContextMenu;

    // REMOVE: List Stuff
    // private readonly SerializedProperty m_ArrayProp;
    // private readonly VisualElement m_TrackersContainer = new VisualElement();
    // CONSIDER: It seems we could use the size tracker as a local variable.
    // private readonly ValueTracker<int> m_SizeTracker = new ValueTracker<int>();

    static InspectorHeader()
    {
        MethodInfo contextMenuMethod = typeof(GenericMenu).GetMethod("ObjectContextDropDown",
            BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance, null,
            new[] { typeof(Rect), typeof(Object[]), typeof(int) }, null);

        if (contextMenuMethod != null)
        {
            s_ShowContextMenu =
                (Action<GenericMenu, Rect, Object[], int>)Delegate.CreateDelegate(
                    typeof(Action<GenericMenu, Rect, Object[], int>), contextMenuMethod, false);
        }
    }

    /// <summary>
    ///     Constructor. It receives a <see cref="SerializedProperty" /> for an array or a list of Objects
    ///     with a type derived from <see cref="Object">UnityEngine.Object</see>.
    /// </summary>
    /// <param name="arrayProp">A serialized property that represents an array of Unity Objects</param>
    public InspectorHeader(SerializedProperty arrayProp)
    {
        AddToClassList(ussClassName);
        // TODO: Styling?
        // styleSheets.Add(EditorAidResources.listOfInspectorsStyle);
        // REMOVE: List Stuff
        // m_TrackersContainer.style.display = DisplayStyle.None;

        if (arrayProp == null || !arrayProp.isArray || arrayProp.propertyType == SerializedPropertyType.String)
        {
            Debug.LogError(
                "arrayProp must be a valid SerializedProperty that points to an array or a list of object references");
        }

        // REMOVE: List Stuff
        // m_ArrayProp = arrayProp;

        // REMOVE: List Stuff
        // SerializedProperty sizeProp = m_ArrayProp.FindPropertyRelative("Array.size");
        // m_SizeTracker.SetUp(sizeProp, OnSizeChange, sizeProp.intValue);
        // m_TrackersContainer.Add(m_SizeTracker);

        // Add(m_TrackersContainer);
        // SetListSize(m_ArrayProp.arraySize);

        // BindTrackers();
    }

    // REMOVE: List Stuff
    // private void OnSizeChange(ChangeEvent<int> e)
    // {
    //     int prevListSize = GetListSize();
    //     SetListSize(m_ArrayProp.arraySize);
    //     if (GetListSize() > prevListSize)
    //     {
    //         BindTrackers();
    //     }
    // }

    // REMOVE: List Stuff
    // private void BindTrackers()
    // {
    //     m_TrackersContainer.Bind(m_ArrayProp.serializedObject);
    // }

    /// <summary>
    ///     Override this method in a child class to customize inspector headers as a whole.
    /// </summary>
    /// <param name="itemIndex">The index in the list</param>
    /// <param name="serializedObject">The inspected object</param>
    /// <param name="inspector">The inspector element under the header.</param>
    /// <param name="footer">The footer element under the inspector.</param>
    /// <returns>The created header.</returns>
    public static VisualElement CreateHeader(SerializedObject serializedObject,
        InspectorElement inspector, IMGUIContainer footer, InspectorItem.DrawSettings drawSettings)
    {
        Object target = serializedObject.targetObject;
        // TODO: HideFlags.NotEditable
        // var header = new Disabler(() => !serializedObject.IsEditable())
        VisualElement header = new VisualElement();
        VisualElement headerContainer = header.contentContainer;
        headerContainer.AddToClassList(itemHeaderUssClassName);

        bool wasExpanded = InternalEditorUtility.GetIsInspectorExpanded(target);
        SetItemExpanded(wasExpanded, headerContainer, footer, target, inspector);

        Foldout foldout = AddHeaderFoldout(headerContainer, wasExpanded);
        foldout.RegisterValueChangedCallback(e =>
        {
            SetItemExpanded(e.newValue, headerContainer, footer, target, inspector);
        });

        // TODO: Dragging
        header.AddManipulator(new DragAndClickManipulator
        {
            // onStartDragging = () => StartDraggingItem(),
            onClick = () => foldout.value = !foldout.value
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

    private static void SetItemExpanded(bool expanded, VisualElement header, VisualElement footer, Object target,
        InspectorElement inspector)
    {
        inspector.style.display = expanded ? DisplayStyle.Flex : DisplayStyle.None;
        header.EnableInClassList(itemHeaderCollapsedUssClassName, !expanded);
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

    private static Foldout AddHeaderFoldout(VisualElement header, bool expanded)
    {
        Foldout foldout = new Foldout { pickingMode = PickingMode.Ignore, value = expanded };
        foldout.Query().Descendents<VisualElement>().ForEach(el => el.pickingMode = PickingMode.Ignore);
        foldout.AddToClassList(itemHeaderFoldoutUssClassName);
        header.Add(foldout);
        return foldout;
    }

    /// <summary>
    ///     Override this method to customize the elements before the header's label.
    /// </summary>
    /// <param name="header">The header</param>
    /// <param name="itemIndex">The index in the list</param>
    /// <param name="serializedObject">The inspected object</param>
    private static void AddPrelabelHeaderElements(VisualElement header, SerializedObject serializedObject,
        InspectorItem.DrawSettings drawSettings)
    {
        if (drawSettings.DrawIcon)
        {
            Image icon = new Image { image = AssetPreview.GetMiniThumbnail(serializedObject.targetObject) };
            icon.AddToClassList(itemHeaderIconUssClassName);
            header.Add(icon);
        }

        Toggle toggle = new Toggle();
        toggle.AddToClassList(itemHeaderEnableToggleUssClassName);
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
    /// <param name="itemIndex">The index in the list</param>
    /// <param name="serializedObject">The inspected object</param>
    private static void AddHeaderLabel(VisualElement header, SerializedObject serializedObject,
        InspectorItem.DrawSettings drawSettings)
    {
        // TODO: Renaming Component Name?
        // var label = new EditableLabel { bindingPath = "m_Name", isDelayed = true };
        // label.AddToClassList(itemHeaderLabelUssClassName);
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
            Label label = new Label { pickingMode = PickingMode.Ignore };
            label.text = ObjectNames.NicifyVariableName(serializedObject.targetObject.GetType().Name);
            label.AddToClassList(itemHeaderLabelUssClassName);
            header.Add(label);
        }
    }

    /// <summary>
    ///     Override this method to customize the elements after the header's label.
    /// </summary>
    /// <param name="header">The header</param>
    /// <param name="itemIndex">The index in the list</param>
    /// <param name="serializedObject">The inspected object</param>
    private static void AddPostlabelHeaderElements(VisualElement header, SerializedObject serializedObject,
        InspectorItem.DrawSettings drawSettings)
    {
        Object target = serializedObject.targetObject;
        Type targetType = target.GetType();

        if (drawSettings.DrawHelpIcon)
        {
            // Check for attribute because Help.HasHelpForObject always returns true for most custom objects.
            bool hasHelp = Attribute.IsDefined(targetType, typeof(HelpURLAttribute));
            bool hasTooltip = Attribute.IsDefined(targetType, typeof(TooltipAttribute));

            Button help = new Button();
            help.AddToClassList(itemHeaderButtonUssClassName);
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
            presets.AddToClassList(itemHeaderButtonUssClassName);
            presets.style.backgroundImage = EditorGUIUtility.IconContent("Preset.Context").image as Texture2D;
            presets.clicked += () => ShowPresetSelector(serializedObject);
            header.Add(presets);
        }

        if (drawSettings.DrawSettingsIcon)
        {
            Button settings = new Button();
            settings.AddToClassList(itemHeaderButtonUssClassName);
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
    /// <param name="itemIndex">The index in the list</param>
    /// <param name="serializedObject">The inspected object</param>
    private static void AddItemsToContextMenu(GenericMenu menu, VisualElement header,
        SerializedObject serializedObject) {}

    private static void ShowInspectorContextMenu(Rect position, VisualElement header, SerializedObject serializedObject,
        InspectorItem.DrawSettings drawSettings)
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

//         var editableLabel = header?.Q<EditableLabel>(null, itemHeaderLabelUssClassName);
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
        s_ShowContextMenu?.Invoke(menu, position, serializedObject.targetObjects, 0);
    }

    private static void ShowPresetSelector(SerializedObject serializedObject)
    {
        Object target = serializedObject.targetObject;
        if (!new PresetType(target).IsValid() || (target.hideFlags & HideFlags.NotEditable) != 0)
        {
            return;
        }

        PresetSelector.ShowSelector(serializedObject.targetObjects, null, true);
    }

    // [RemoveFromDocs]
    // protected override VisualElement CreateItemForIndex(int index)
    // {
    //     var stopper = new BindingStopper();
    //     stopper.Add(new InspectorItem(this, index));
    //     return stopper;
    // }

    public class InspectorItem : VisualElement
    {
        private readonly SerializedObject m_BackingObject;
        private readonly InspectorElement _inspectorElement;
        private readonly IMGUIContainer _footerElement;
        private readonly DrawSettings _drawSettings;
        private readonly ValueTracker<Object> m_ObjectTracker = new ValueTracker<Object>();

        public class DrawSettings
        {
            public bool DrawEnableToggle = true;
            public bool DrawIcon = true;
            public bool DrawLabel = true;
            public bool DrawHelpIcon = true;
            public bool DrawPresetIcon = true;
            public bool DrawSettingsIcon = true;
            public bool EnableContextMenu = true;
        }

        public InspectorItem(SerializedObject serializedObject, InspectorElement inspectorElement,
            IMGUIContainer footer, DrawSettings drawSettings = null)
        {
            styleSheets.Add(EditorAidResources.InspectorHeaderStyle);
            AddToClassList(inspectorItemUssClassName);

            m_BackingObject = serializedObject;
            _inspectorElement = inspectorElement;
            _footerElement = footer;
            _drawSettings = drawSettings;

            // REMOVE: List Stuff
            // if (m_BackingProperty.propertyType != SerializedPropertyType.ObjectReference)
            // {
            //     Debug.LogError("Property needs to be an object reference to create an inspector");
            //     return;
            // }

            // REMOVE: List Stuff
            // m_ObjectTracker.SetUp(m_BackingProperty, e => AssignObject(), m_BackingProperty.objectReferenceValue);
            // m_OwnerList.m_TrackersContainer.Add(m_ObjectTracker);

            AssignObject();

            // REMOVE: List Stuff
            // RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
            // RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
        }

        // REMOVE: List Stuff
        // private void OnAttachToPanel(AttachToPanelEvent e)
        // {
        //     //We need to add it here in case OnDetachFromPanel happened because of panel interactions and not because the element was removed.
        //     m_OwnerList.m_TrackersContainer.Add(m_ObjectTracker);
        // }

        // private void OnDetachFromPanel(DetachFromPanelEvent e)
        // {
        //     m_ObjectTracker.RemoveFromHierarchy();
        // }

        private void AssignObject()
        {
            // Object obj = m_BackingProperty.objectReferenceValue;

            Clear();

            // if (!obj)
            // {
            //     if (m_BackingProperty.objectReferenceInstanceIDValue != 0)
            //     {
            //         AssignControlsForInvalidScript();
            //     }
            //
            //     return;
            // }

            VisualElement header = CreateHeader(m_BackingObject, _inspectorElement, _footerElement, _drawSettings);

            if (header != null)
            {
                Add(header);
            }

            // Add(inspector);

            // this.Bind(m_BackingProperty);
        }

        private void AssignControlsForInvalidScript()
        {
            VisualElement header = new VisualElement { style = { height = 22 } };
            header.AddToClassList(itemHeaderUssClassName);
            VisualElement body = new VisualElement();
            body.style.paddingTop = 3;
            body.style.paddingBottom = 3;
            body.style.paddingRight = 0;
            body.style.paddingLeft = 15;

            Foldout foldout = AddHeaderFoldout(header, true);
            foldout.RegisterValueChangedCallback(e =>
            {
                header.EnableInClassList(itemHeaderCollapsedUssClassName, !e.newValue);
                body.style.display = e.newValue ? DisplayStyle.Flex : DisplayStyle.None;
            });

            Image icon = new Image { image = EditorGUIUtility.IconContent("Warning").image };
            icon.AddToClassList(itemHeaderIconUssClassName);
            header.Add(icon);

            Label label = new Label("Object With Invalid Script");
            label.AddToClassList(itemHeaderLabelUssClassName);
            header.Add(label);

            // TODO: Dragging
            // header.AddManipulator(new DragAndClickManipulator
            // {
            //     onStartDragging = () => m_OwnerList.StartDraggingItem(m_Index),
            //     onClick = () => foldout.value = !foldout.value
            // });

            body.Add(new HelpBox(
                "This object's script is invalid. Make sure it doesn't have errors, its" +
                " class has the same name as its file, and it's the right type.", HelpBoxMessageType.Warning));

            body.Add(new Button(() => Selection.activeInstanceID = m_BackingObject.targetObject.GetInstanceID())
            {
                text = "Select Object With Invalid Script"
            });

            Add(header);
            Add(body);
        }
    }

    /// <summary> Start dragging an item to reorder the list. Only call this from mouse events where a button is pressed to avoid errors.</summary>
    /// <param name="index">Index of the item to drag.</param>
    public static void StartDraggingItem()
    {
        DragAndDrop.PrepareStartDrag();
        DragAndDrop.objectReferences = Array.Empty<Object>();
        DragAndDrop.paths = null;
        // DragAndDrop.SetGenericData("DraggedList", this);
        DragAndDrop.visualMode = DragAndDropVisualMode.Move;
        DragAndDrop.StartDrag("");
        // TODO: Dragging
        // m_DraggedIndex = index;
    }
}