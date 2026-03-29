#if COMPONENT_NAMES
using Sisus.ComponentNames;
#endif
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.Profiling;
using UnityEditor;
using UnityEditor.Search;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Search;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;
using ObjectField = UnityEditor.UIElements.ObjectField;

[CustomEditor(typeof(GuidComponent))]
public class GuidComponentDrawer : Editor
{
    private GuidComponent _guidComp;
    private InspectorHeader _inspectorHeader;
    private VisualElement _componentGUIDsContainer;
    private DragAndDrop.HierarchyDropHandlerV2 _hierarchyDropHandler;

    // Hierarchy Drop handler to prevent dragging and dropping GuidComponent onto other Game Objects.
    private DragAndDropVisualMode HeaderHierarchyDropHandler(
        EntityId dropTargetEntityId,
        HierarchyDropFlags dropMode,
        Transform parentForDraggedObjects,
        bool perform)
    {
        GuidComponent draggedObject = DragAndDrop.objectReferences[0] as GuidComponent;
        if (draggedObject)
        {
            return DragAndDropVisualMode.Rejected;
        }

        return DragAndDropVisualMode.None;
    }

    // SerializedProperty here only used for remembering the state of foldout:
    // https://discussions.unity.com/t/editorguilayout-foldout-no-way-to-remember-state/36422/6
    private SerializedProperty _transformGuidProp;
    private SerializedProperty _componentGuidsProp;
    private SerializedProperty _orphanedGuidsProp;

    public override VisualElement CreateInspectorGUI()
    {
        VisualElement root = new VisualElement();

        if (EditorApplication.isCompiling)
        {
            return root;
        }

        if (_hierarchyDropHandler == null ||
            !DragAndDrop.HasHandler(DragAndDropWindowTarget.hierarchy, _hierarchyDropHandler))
        {
            _hierarchyDropHandler = HeaderHierarchyDropHandler;
            DragAndDrop.AddDropHandlerV2(_hierarchyDropHandler);
        }

        root.RegisterCallback<AttachToPanelEvent>(_ => ObjectChangeEvents.changesPublished += ChangesPublished);
        root.RegisterCallback<DetachFromPanelEvent>(_ => ObjectChangeEvents.changesPublished -= ChangesPublished);

        _transformGuidProp = serializedObject.FindProperty(nameof(GuidComponent.transformGuid));
        _componentGuidsProp = serializedObject.FindProperty(nameof(GuidComponent.componentGuids));
        _orphanedGuidsProp = serializedObject.FindProperty(nameof(GuidComponent.orphanedComponentGuids));
        _guidComp = (GuidComponent)serializedObject.targetObject;

        CreateHeaderGUI(root);

        if (!_guidComp)
        {
            return root;
        }

        if (PrefabCheckerUtility.IsPartOfPrefabAssetOnly(_guidComp) || PrefabCheckerUtility.IsInPrefabStage(_guidComp))
        {
            HelpBox helpBox = new HelpBox(
                "Guid Components do not work in prefab assets.\nHowever, instances of prefabs in a scene will receive the correct guid information.",
                HelpBoxMessageType.Warning);
            root.Add(helpBox);
        }
        else
        {
            root.styleSheets.Add(StyleSheetUtility.GuidComponentStyle);

            VisualElement element = new VisualElement
            {
                focusable = true,
                delegatesFocus = true,
                style =
                {
                    flexDirection = FlexDirection.Row
                }
            };

            element.RegisterCallback<GeometryChangedEvent>(_ => { element.MarkDirtyRepaint(); });

            TextField textField = new TextField("Game Object")
            {
                isReadOnly = true,
                style =
                {
                    // This is required when parented to an empty Visual Element, otherwise the children inside that
                    // will not change layout when it resizes, and therefore not compute the new aligned field values.
                    flexGrow = 1
                }
            };
            textField.AddToClassList(TextField.alignedFieldUssClassName);
            textField.AddToClassList("guid-component__guid-text-field");
            textField.SetValueWithoutNotify(_guidComp.transformGuid.serializableGuid.ToString());

            Image icon = new Image { image = EditorGUIUtility.ObjectContent(null, typeof(GameObject)).image };
            icon.name = "guid-component-icon";
            element.Add(icon);
            element.Add(textField);

            root.Add(element);

            // "-2" will account for the two components we always know will be present, but do not want to check for,
            // those being: Transform & GuidComponent
            if (_guidComp.GetComponentGuids().Count > 0 || _guidComp.gameObject.GetComponentCount() - 2 > 0)
            {
                Foldout foldout = new Foldout
                {
                    text = "Components",
                    value = _transformGuidProp.isExpanded,
                    toggleOnLabelClick = true,
                    style = { unityFontStyleAndWeight = FontStyle.Bold }
                };
                foldout.contentContainer.style.unityFontStyleAndWeight = FontStyle.Normal;
                root.Add(foldout);

                _componentGUIDsContainer = foldout.contentContainer;

                // Populate rows
                RebuildGuidList(foldout.contentContainer);

                // Persist expansion state
                foldout.RegisterValueChangedCallback(evt =>
                {
                    if (evt.newValue)
                    {
                        _transformGuidProp.isExpanded = evt.newValue;

                        foldout.contentContainer.Clear();
                        RebuildGuidList(foldout.contentContainer);
                    }
                });
            }
        }

        return root;
    }

    private void ChangesPublished(ref ObjectChangeEventStream stream)
    {
        bool needsComponentListRebuilding = false;
        bool needsNotifyGuidComponent = false;
        for (int i = 0; i < stream.length; ++i)
        {
            ObjectChangeKind type = stream.GetEventType(i);
            switch (type)
            {
                case ObjectChangeKind.ChangeGameObjectStructure: // Component added/removed.
                    stream.GetChangeGameObjectStructureEvent(i,
                        out ChangeGameObjectStructureEventArgs structureArgs);
                    if (_guidComp && structureArgs.instanceId == _guidComp.gameObject.GetInstanceID())
                    {
                        needsNotifyGuidComponent = true;
                    }

                    needsComponentListRebuilding = true;
                    break;
                case ObjectChangeKind.ChangeGameObjectOrComponentProperties: // Properties or component order changed.
                    needsComponentListRebuilding = true;
                    break;
            }
        }

        if (needsNotifyGuidComponent && _guidComp)
        {
            _guidComp.OnValidate();
        }

        if (needsComponentListRebuilding && _guidComp && _componentGUIDsContainer != null)
        {
            RebuildGuidList(_componentGUIDsContainer);
        }
    }

    private void RebuildGuidList(VisualElement parent)
    {
        _componentGUIDsContainer.Clear();
        var assignedGuids = new List<Component>();

        // -------------------
        // Build Assigned List
        // -------------------

        var elements = new List<(VisualElement element, int index)>();
        foreach (ComponentGuid componentGuid in _guidComp.GetComponentGuids())
        {
            if (!componentGuid.CachedComponent)
            {
                continue;
            }

            assignedGuids.Add(componentGuid.CachedComponent);

            VisualElement element = new VisualElement
            {
                style = { flexDirection = FlexDirection.Row }
            };
#if COMPONENT_NAMES
            TextField labelFieldComponentGuid = new TextField($"{componentGuid.CachedComponent.GetName()}");
#else
            TextField labelFieldComponentGuid =
                new TextField($"{ObjectNames.NicifyVariableName(componentGuid.CachedComponent.GetType().Name)}");
#endif

            labelFieldComponentGuid.isDelayed = true;
            labelFieldComponentGuid.isReadOnly = true;
            labelFieldComponentGuid.style.flexGrow = 1;

            labelFieldComponentGuid.AddToClassList(TextField.alignedFieldUssClassName);
            labelFieldComponentGuid.AddToClassList("guid-component__guid-text-field");
            labelFieldComponentGuid.SetValueWithoutNotify(componentGuid.serializableGuid.ToString());

            Image icon = new Image
            {
                image = EditorGUIUtility.ObjectContent(null, componentGuid.CachedComponent.GetType()).image,
                name = "guid-component-icon"
            };

            Button button = new Button
            {
                name = "guid-component-button",
                tooltip = "Orphan Guid",
                style =
                {
                    backgroundImage = (StyleBackground)EditorGUIUtility.IconContent("Toolbar Minus").image
                }
            };
            button.AddToClassList("remove-button");

            button.clickable.clicked += () =>
            {
                serializedObject.Update();

                int idx = FindComponentGuidIndex(_componentGuidsProp, componentGuid);
                if (idx >= 0)
                {
                    MoveArrayElement(_componentGuidsProp, idx, _orphanedGuidsProp);

                    serializedObject.ApplyModifiedProperties();
                    Undo.SetCurrentGroupName("Orphan Guid from Component");
                    _guidComp.NotifyGuidRemoved(componentGuid);
                }
            };
            labelFieldComponentGuid.contentContainer.Add(button);

            element.Add(icon);
            element.Add(labelFieldComponentGuid);

            elements.Add((element, componentGuid.CachedComponent.GetComponentIndex()));
        }

        // ------------------------------
        // Build Candidate/Available List
        // ------------------------------

        var candidateGuids = _guidComp.GetComponents<Component>().ToList();
        candidateGuids.RemoveAll(component =>
            GuidComponentExcluders.Excluders.Contains(component.GetType()) || assignedGuids.Contains(component));

        // Look for new components on the GameObject. Exclude component types specified in GuidComponentExcluders.
        foreach (Component component in candidateGuids)
        {
            VisualElement element = new VisualElement
            {
                style = { flexDirection = FlexDirection.Row }
            };
#if COMPONENT_NAMES
            TextField labelFieldComponentGuid = new TextField($"{component.GetName()}");
#else
            TextField labelFieldComponentGuid =
                new TextField($"{ObjectNames.NicifyVariableName(component.GetType().Name)}");
#endif

            labelFieldComponentGuid.enabledSelf = false;
            labelFieldComponentGuid.isReadOnly = true;
            labelFieldComponentGuid.style.flexGrow = 1;
            labelFieldComponentGuid.AddToClassList(TextField.alignedFieldUssClassName);

            Image icon = new Image
            {
                image = EditorGUIUtility.ObjectContent(null, component.GetType()).image,
                name = "guid-component-icon"
            };

            Button button = new Button
            {
                name = "guid-component-button",
                tooltip = "Assign Guid",
                style = { backgroundImage = (StyleBackground)EditorGUIUtility.IconContent("Toolbar Plus").image }
            };
            button.AddToClassList("add-button");

            button.clickable.clicked += () =>
            {
                serializedObject.Update();

                int newIndex = _componentGuidsProp.arraySize;
                _componentGuidsProp.InsertArrayElementAtIndex(newIndex);
                SetAdoptionFields(
                    _componentGuidsProp.GetArrayElementAtIndex(newIndex),
                    component, _guidComp.gameObject);

                serializedObject.ApplyModifiedProperties();
                Undo.SetCurrentGroupName("Assign Guid to Component");
            };

            element.Add(icon);
            element.Add(labelFieldComponentGuid);
            element.Add(button);

            elements.Add((element, component.GetComponentIndex()));
        }

        elements = elements.OrderBy(element => element.index).ToList();
        foreach ((VisualElement element, int index) elementEntry in elements)
        {
            parent.Add(elementEntry.element);
        }

        // -------------------
        // Build Orphaned List
        // -------------------

        if (_guidComp.orphanedComponentGuids.Count > 0)
        {
            VisualElement separator = new VisualElement
            {
                enabledSelf = false,
                focusable = false
            };
            separator.AddToClassList("horizontal-separator");
            parent.Add(separator);
        }

        foreach (ComponentGuid orphanedGuid in _guidComp.orphanedComponentGuids)
        {
            Type orphanOwnerType = orphanedGuid.GetOwningType();
            string tooltip = "Orphaned: Cannot find owner.\nAssign new component or remove this guid.";
            VisualElement element = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    marginTop = 1,
                    marginBottom = 1
                }
            };

            ObjectField objectFieldOrphanedGuid = new ObjectField
            {
                tooltip = tooltip,
                objectType = orphanOwnerType
            };

            objectFieldOrphanedGuid.name = "error-guid-orphaned-object-field";

            objectFieldOrphanedGuid.RegisterCallback<DragUpdatedEvent>(evt =>
            {
                Component draggedObject = DragAndDrop.objectReferences[0] as Component;
                if (!draggedObject || !IsChildOf(draggedObject.gameObject, _guidComp.gameObject) ||
                    draggedObject.GetType() != orphanOwnerType ||
                    _guidComp.GetGuid(draggedObject) != Guid.Empty)
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Rejected;
                    evt.StopImmediatePropagation();
                }
            }, TrickleDown.TrickleDown);

            objectFieldOrphanedGuid.RegisterCallback<DragPerformEvent>(evt =>
            {
                Component draggedObject = DragAndDrop.objectReferences[0] as Component;
                if (draggedObject && IsChildOf(draggedObject.gameObject, _guidComp.gameObject) &&
                    draggedObject.GetType() == orphanOwnerType)
                {
                    serializedObject.Update();

                    int idx = FindComponentGuidIndex(_orphanedGuidsProp, orphanedGuid);
                    if (idx >= 0)
                    {
                        MoveArrayElement(_orphanedGuidsProp, idx, _componentGuidsProp);
                        SetAdoptionFields(
                            _componentGuidsProp.GetArrayElementAtIndex(_componentGuidsProp.arraySize - 1),
                            draggedObject, _guidComp.gameObject);

                        serializedObject.ApplyModifiedProperties();
                        Undo.SetCurrentGroupName("Adopt Orphaned Guid");
                    }

                    evt.StopImmediatePropagation();
                }
            }, TrickleDown.TrickleDown);

            // Override the object selector with our custom one.
            VisualElement oldObjectSelector = objectFieldOrphanedGuid.Q(null, ObjectField.selectorUssClassName);
            VisualElement objectSelector = new VisualElement();
            objectSelector.AddToClassList(ObjectField.selectorUssClassName);

            oldObjectSelector.parent.Add(objectSelector);
            oldObjectSelector.RemoveFromHierarchy();

            objectSelector.RegisterCallback<MouseUpEvent>(evt =>
            {
                if (evt.button == 0)
                {
                    SetupComponentPicker(objectFieldOrphanedGuid, orphanedGuid);
                    evt.StopPropagation();
                }
            });

            TextField labelFieldComponentGuid = new TextField
            {
                isReadOnly = true,
                style =
                {
                    flexGrow = 1
                },
                tooltip = tooltip
            };
            labelFieldComponentGuid.name = "error-guid-orphaned-label";
            labelFieldComponentGuid.value = orphanedGuid.serializableGuid.ToString();
            labelFieldComponentGuid.AddToClassList("guid-component__guid-text-field");

            // Ugly hack so the label section is aligned, if label is empty it won't call the align functions.
            CustomLabelField customField = new CustomLabelField(" ");
            customField.style.flexGrow = 1;
            customField.labelElement.style.flexDirection = FlexDirection.Row;
            customField.labelElement.name = "error-guid-orphaned-field";
            customField.labelElement.tooltip = tooltip;

            customField.SetCustomLabel(objectFieldOrphanedGuid);
            customField.SetCustomContent(labelFieldComponentGuid);

            Button button = new Button
            {
                name = "guid-component-button",
                tooltip = "Delete Orphaned Guid",
                style =
                {
                    backgroundImage = (StyleBackground)EditorGUIUtility.IconContent("Close").image
                }
            };
            button.AddToClassList("remove-button");

            button.clickable.clicked += () =>
            {
                serializedObject.Update();

                int idx = FindComponentGuidIndex(_orphanedGuidsProp, orphanedGuid);
                if (idx >= 0)
                {
                    _orphanedGuidsProp.DeleteArrayElementAtIndex(idx);

                    serializedObject.ApplyModifiedProperties();
                    Undo.SetCurrentGroupName("Remove Orphaned Guid");
                }
            };
            labelFieldComponentGuid.contentContainer.Add(button);

            Image icon = new Image { image = EditorGUIUtility.IconContent("Error").image };
            icon.name = "guid-component-icon";
            element.Add(icon);
            element.Add(customField);

            parent.Add(element);
        }
    }

    private static int FindComponentGuidIndex(SerializedProperty arrayProp, ComponentGuid target)
    {
        for (int i = 0; i < arrayProp.arraySize; i++)
        {
            SerializedProperty element = arrayProp.GetArrayElementAtIndex(i);
            SerializedProperty guidProp = element.FindPropertyRelative("serializableGuid");
            if ((SerializableGuid)guidProp.boxedValue == target.serializableGuid)
            {
                return i;
            }
        }

        return -1;
    }

    private static void CopyComponentGuidProperties(SerializedProperty source, SerializedProperty dest)
    {
        SerializedProperty dstGuid = dest.FindPropertyRelative("serializableGuid");
        dstGuid.boxedValue = source.FindPropertyRelative("serializableGuid").boxedValue;

        dest.FindPropertyRelative("cachedComponent").objectReferenceValue =
            source.FindPropertyRelative("cachedComponent").objectReferenceValue;
        dest.FindPropertyRelative("owningGameObject").objectReferenceValue =
            source.FindPropertyRelative("owningGameObject").objectReferenceValue;
        dest.FindPropertyRelative("globalGameObjectId").stringValue =
            source.FindPropertyRelative("globalGameObjectId").stringValue;
        dest.FindPropertyRelative("globalComponentId").stringValue =
            source.FindPropertyRelative("globalComponentId").stringValue;
        dest.FindPropertyRelative("cachedOwnerTypeReference").stringValue =
            source.FindPropertyRelative("cachedOwnerTypeReference").stringValue;
    }

    private static void MoveArrayElement(SerializedProperty sourceArray, int sourceIndex,
        SerializedProperty destArray)
    {
        SerializedProperty source = sourceArray.GetArrayElementAtIndex(sourceIndex);
        int destIndex = destArray.arraySize;
        destArray.InsertArrayElementAtIndex(destIndex);

        SerializedProperty dest = destArray.GetArrayElementAtIndex(destIndex);
        CopyComponentGuidProperties(source, dest);
        sourceArray.DeleteArrayElementAtIndex(sourceIndex);
    }

    // Duplicate logic from ComponentGuid.CachedComponent & ComponentGuid.OwningGameObject property setters to work
    // within the SerializedObject flow for Undo/Redo, Prefab modifications, etc.
    // Annoying I have to duplicate this logic, but it's recommended to work with SerializedObject's in custom editors.
    private static void SetAdoptionFields(SerializedProperty elementProp, Component newOwner,
        GameObject owningGameObject)
    {
        elementProp.FindPropertyRelative("serializableGuid").boxedValue = SerializableGuid.Empty;
        elementProp.FindPropertyRelative("cachedComponent").objectReferenceValue = newOwner;
        elementProp.FindPropertyRelative("owningGameObject").objectReferenceValue = owningGameObject;

        GlobalObjectId componentId = GlobalObjectId.GetGlobalObjectIdSlow(newOwner);
        if (componentId.identifierType == 2)
        {
            elementProp.FindPropertyRelative("globalComponentId").stringValue = componentId.ToString();
        }
        else if (componentId.identifierType != 0)
        {
            // identifierType 0 = Null/transient (e.g. mid-prefab reimport) — skip silently,
            // the existing GlobalComponentId remains valid.
            Debug.LogError(
                "[GuidComponent] Error: ComponentGuids can only be created for scene game objects! Setting to empty.");
            elementProp.FindPropertyRelative("globalComponentId").stringValue = string.Empty;
        }

        GlobalObjectId gameObjectId = GlobalObjectId.GetGlobalObjectIdSlow(owningGameObject);
        if (gameObjectId.identifierType == 2)
        {
            elementProp.FindPropertyRelative("globalGameObjectId").stringValue = gameObjectId.ToString();
        }
        else if (gameObjectId.identifierType != 0)
        {
            // identifierType 0 = Null/transient (e.g. mid-prefab reimport) — skip silently,
            // the existing GlobalComponentId remains valid.
            Debug.LogError(
                "[GuidComponent] Error: ComponentGuids can only be created for scene game objects! Setting to empty.");
            elementProp.FindPropertyRelative("globalGameObjectId").stringValue = string.Empty;
        }

        elementProp.FindPropertyRelative("cachedOwnerTypeReference").stringValue =
            newOwner.GetType().FullName + ", " + newOwner.GetType().Assembly.GetName().Name;
    }

    private void SetupComponentPicker(ObjectField objectField,
        ComponentGuid orphanedGuid)
    {
        SearchProvider searchProvider = new SearchProvider("local_components", "Local Components")
        {
            showDetailsOptions = ShowDetailsOptions.None,
            fetchItems = (context, items, provider) =>
                ComponentPickerFetchItemsHandler(context, provider, objectField, orphanedGuid),
            fetchThumbnail = (item, context) => item.thumbnail,
            fetchLabel = (item, context) => item.label,
            fetchDescription = (item, searchContext) => item.description,
            toObject = (item, type) => item.data as Component,
            active = true
        };

        SearchContext context = SearchService.CreateContext(searchProvider);
        context.wantsMore = true;

        SearchViewState viewState = new SearchViewState(context,
            SearchViewFlags.ObjectPicker | SearchViewFlags.CompactView | SearchViewFlags.DisableInspectorPreview |
            SearchViewFlags.HideSearchBar)
        {
            windowTitle = new GUIContent($"{_guidComp.gameObject.name} Component Selector"),
            title = "Component",
            selectHandler = (item, cancelled) => ComponentPickerSelectHandler(item, cancelled, objectField),
            trackingHandler = item => ComponentPickerTrackingHandler(item, objectField),
            position = SearchUtils.GetMainWindowCenteredPosition(new Vector2(600, 400)),
            hideTabs = true,
            group = "all",
            queryBuilderEnabled = false
        };

        SearchService.ShowPicker(viewState);
    }

    private class ObjectSearchPayload
    {
        public ComponentGuid OrphanedGuid;
        public Component Component;
        public ObjectField ObjectField;
    }

    private IEnumerable<SearchItem> ComponentPickerFetchItemsHandler(SearchContext searchContext,
        SearchProvider searchProvider, ObjectField objectField,
        ComponentGuid orphanedGuid)
    {
        if (_guidComp.gameObject == null)
        {
            yield break;
        }

        Type orphanOwnerType = orphanedGuid.GetOwningType();
        int index = 0;
        foreach (Component component in _guidComp.GetComponents<Component>())
        {
            if (GuidComponentExcluders.Excluders.Contains(component.GetType()))
            {
                continue;
            }

            if (_guidComp.componentGuids.Exists(guid => guid.CachedComponent == component) ||
                component.GetType() != orphanOwnerType)
            {
                continue;
            }

            yield return searchProvider.CreateItem(searchContext, component.GetEntityId().ToString(), index++,
                component.GetType().Name,
                component.GetType().Name,
                (Texture2D)EditorGUIUtility.ObjectContent(null, component.GetType()).image,
                new ObjectSearchPayload
                    { Component = component, OrphanedGuid = orphanedGuid, ObjectField = objectField });
        }
    }

    private void ComponentPickerSelectHandler(SearchItem searchItem, bool canceled, ObjectField objectField)
    {
        if (canceled || searchItem != null && searchItem.data == null)
        {
            objectField.value = null;
            return;
        }

        if (searchItem is { data: ObjectSearchPayload searchPayload } && searchPayload.Component != null)
        {
            serializedObject.Update();

            int idx = FindComponentGuidIndex(_orphanedGuidsProp, searchPayload.OrphanedGuid);
            if (idx >= 0)
            {
                MoveArrayElement(_orphanedGuidsProp, idx, _componentGuidsProp);
                SetAdoptionFields(
                    _componentGuidsProp.GetArrayElementAtIndex(_componentGuidsProp.arraySize - 1),
                    searchPayload.Component, _guidComp.gameObject);

                serializedObject.ApplyModifiedProperties();
                Undo.SetCurrentGroupName("Adopt Orphaned Guid");
            }
        }
    }

    private static void ComponentPickerTrackingHandler(SearchItem searchItem, ObjectField objectField)
    {
        if (searchItem.data is ObjectSearchPayload searchPayload && searchPayload.Component != null)
        {
            searchPayload.ObjectField.value = searchPayload.Component;
        }
        else
        {
            objectField.value = null;
        }
    }

    // Helper method: checks if candidate is a child of parent
    private bool IsChildOf(GameObject candidate, GameObject parent)
    {
        if (candidate == null || parent == null)
        {
            return false;
        }

        return candidate.transform.IsChildOf(parent.transform);
    }

    // ---- Header GUI ----
    private static ProfilerMarker _createInspectorGUIProfileMarker =
        new ProfilerMarker($"{nameof(GuidComponentDrawer)}.{nameof(CreateInspectorGUI)}");

    private static readonly Type InspectorWindowType =
        typeof(EditorWindow).Assembly.GetType("UnityEditor.InspectorWindow");
    // We can also check for this if we want to render this UITK Header in the "Properties" inspector editor.
    // private static readonly Type PropertyEditorType =
    //     typeof(EditorWindow).Assembly.GetType("UnityEditor.PropertyEditor");
    private static readonly Type EditorElementType =
        typeof(EditorWindow).Assembly.GetType("UnityEditor.UIElements.EditorElement");

    private static readonly FieldInfo PropertyViewerField =
        typeof(Editor).GetField("m_PropertyViewer", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly FieldInfo EditorElementField =
        InspectorWindowType.GetField("m_EditorsElement", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly FieldInfo HeaderField =
        EditorElementType.GetField("m_Header", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly FieldInfo InspectorElementField =
        EditorElementType.GetField("m_InspectorElement", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly FieldInfo FooterElementField =
        EditorElementType.GetField("m_Footer", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly FieldInfo EditorTargetObjectField =
        EditorElementType.GetField("m_EditorTarget", BindingFlags.NonPublic | BindingFlags.Instance);

    private static readonly Action<VisualElement, Rect> DragRect =
        ReflectionUtility.BuildFieldSetter<VisualElement, Rect>(EditorElementType,
            "m_DragRect", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly Action<VisualElement, Rect> ContentRect =
        ReflectionUtility.BuildFieldSetter<VisualElement, Rect>(EditorElementType,
            "m_ContentRect", BindingFlags.NonPublic | BindingFlags.Instance);

    private VisualElement m_EditorsElement;
    private VisualElement EditorsElement => m_EditorsElement ??= GetEditorVisualElement();

    private VisualElement GetEditorVisualElement()
    {
        object propertyViewer = PropertyViewerField.GetValue(this);
        if (propertyViewer == null || propertyViewer.GetType() != InspectorWindowType)
        {
            return null;
        }

        VisualElement editorsElement = EditorElementField.GetValue(propertyViewer) as VisualElement;
        return editorsElement;
    }

    private void CreateHeaderGUI(VisualElement root)
    {
        using (_createInspectorGUIProfileMarker.Auto())
        {
            VisualElement inspectorRoot = EditorsElement;
            if (inspectorRoot == null)
            {
                return;
            }

            var foundAll = inspectorRoot.Children();
            foreach (VisualElement element in foundAll)
            {
                if (element.GetType() != EditorElementType)
                {
                    continue;
                }

                Object targetObject = EditorTargetObjectField.GetValue(element) as Object;
                if (targetObject is not GuidComponent)
                {
                    continue;
                }

                // Inject UI Toolkit header.
                root.RegisterCallback<AttachToPanelEvent>(_ =>
                {
                    element.RegisterCallback<DragUpdatedEvent>(evt =>
                    {
                        DragRect((VisualElement)evt.currentTarget, ((VisualElement)evt.currentTarget).layout);
                        ContentRect((VisualElement)evt.currentTarget, ((VisualElement)evt.currentTarget).layout);
                    });

                    void CustomContextMenuItems(GenericMenu menu)
                    {
                        menu.AddItem(new GUIContent("Remove Guid Component"), false,
                            () =>
                            {
                                if (!EditorUtility.DisplayDialog(
                                        "Remove Guid Component",
                                        "Removing GuidComponent will permanently lose all GUIDs assigned to this GameObject. This cannot be undone after saving.\n\nAre you sure you want to continue?",
                                        "Remove", "Cancel"))
                                {
                                    return;
                                }

                                Undo.DestroyObjectImmediate(_guidComp);
                            });
                    }

                    InspectorElement inspector = InspectorElementField.GetValue(element) as InspectorElement;
                    IMGUIContainer footer = FooterElementField.GetValue(element) as IMGUIContainer;
                    _inspectorHeader =
                        new InspectorHeader(serializedObject, inspector, footer,
                            new InspectorHeader.DrawSettings
                            {
                                DrawEnableToggle = false,
                                DrawHelpIcon = false,
                                DrawPresetIcon = false,
                                HeaderTitleOverride = "Guid",
                                CustomContextMenuItems = CustomContextMenuItems
                            });

                    // Do this when visual tree is mostly ready.
                    _inspectorHeader.RegisterCallback<GeometryChangedEvent>(evt =>
                    {
                        // Disabling the default IMGUI header. Won't delete it at the risk of some internal Unity logic expecting it to be present and therefore breaking internal contracts.
                        IMGUIContainer IMGUIHeader = HeaderField.GetValue(element) as IMGUIContainer;

                        _inspectorHeader.name = IMGUIHeader.name;
                        IMGUIHeader.name = "";

                        IMGUIHeader.visible = false;
                        IMGUIHeader.style.display = DisplayStyle.None;
                        IMGUIHeader.onGUIHandler = () => {};
                    });

                    element.Insert(0, _inspectorHeader);
                });

                root.RegisterCallback<DetachFromPanelEvent>(evt =>
                {
                    // Restore IMGUI settings, otherwise weird stuff happens. Like when reordering components,
                    // it seems that those components adopt the settings in this UITK header's *now* old spot, therefore having their headers disappear.
                    IMGUIContainer IMGUIHeader =
                        HeaderField.GetValue(_inspectorHeader.parent) as IMGUIContainer;
                    IMGUIHeader.visible = true;
                    IMGUIHeader.style.display = DisplayStyle.Flex;

                    _inspectorHeader.RemoveFromHierarchy();
                });

                break;
            }
        }
    }
}