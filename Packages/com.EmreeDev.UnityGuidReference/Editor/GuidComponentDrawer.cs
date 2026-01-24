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
using ObjectField = UnityEditor.Search.ObjectField;

[CustomEditor(typeof(GuidComponent))]
public class GuidComponentDrawer : Editor
{
    private GuidComponent _guidComp;
    private InspectorHeader _inspectorHeader;
    private VisualElement _componentGUIDsContainer;

    // SerializedProperty here only used for remembering the state of foldout:
    // https://discussions.unity.com/t/editorguilayout-foldout-no-way-to-remember-state/36422/6
    private SerializedProperty _transformGuidProp;

    public override VisualElement CreateInspectorGUI()
    {
        VisualElement root = new VisualElement();

        if (EditorApplication.isCompiling)
        {
            return root;
        }

        root.RegisterCallback<AttachToPanelEvent>(_ => ObjectChangeEvents.changesPublished += ChangesPublished);
        root.RegisterCallback<DetachFromPanelEvent>(_ => ObjectChangeEvents.changesPublished -= ChangesPublished);

        _transformGuidProp = serializedObject.FindProperty(nameof(GuidComponent.transformGuid));
        _guidComp = (GuidComponent)serializedObject.targetObject;

        CreateHeaderGUI(root);

        if (!_guidComp)
        {
            return root;
        }

        if (_guidComp.IsAssetOnDisk())
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
            textField.AddToClassList(".guid-component__guid-text-field");
            textField.SetValueWithoutNotify(_guidComp.transformGuid.serializableGuid.ToString());

            Image icon = new Image { image = EditorGUIUtility.ObjectContent(null, typeof(GameObject)).image };
            icon.name = "guid-component-icon";
            element.Add(icon);
            element.Add(textField);

            root.Add(element);

            if (_guidComp.GetComponentGuids().Count > 0 || _guidComp.GetComponentGuidCandidates().Count > 0)
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
        for (int i = 0; i < stream.length; ++i)
        {
            ObjectChangeKind type = stream.GetEventType(i);
            switch (type)
            {
                case ObjectChangeKind.ChangeGameObjectStructureHierarchy:    // Component Order changed.
                case ObjectChangeKind.ChangeGameObjectOrComponentProperties: // GuidComponent Changed.
                case ObjectChangeKind.ChangeAssetObjectProperties:           // GuidReferenceMapping changed.
                case ObjectChangeKind.ChangeGameObjectStructure:             // Game Object changed.
                    needsComponentListRebuilding = true;
                    break;
            }
        }

        if (needsComponentListRebuilding)
        {
            RebuildGuidList(_componentGUIDsContainer);
        }
    }

    private void RebuildGuidList(VisualElement parent)
    {
        _componentGUIDsContainer.Clear();

        // -------------------
        // Build Assigned List
        // -------------------

        var elements = new List<(VisualElement element, int index)>();
        foreach (ComponentGuid componentGuid in _guidComp.GetComponentGuids())
        {
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

            labelFieldComponentGuid.isReadOnly = true;
            labelFieldComponentGuid.style.flexGrow = 1;
            labelFieldComponentGuid.AddToClassList(TextField.alignedFieldUssClassName);
            labelFieldComponentGuid.SetValueWithoutNotify(componentGuid.serializableGuid.ToString());

            labelFieldComponentGuid.labelElement.RegisterCallback<GeometryChangedEvent>(evt =>
                labelFieldComponentGuid.labelElement.MarkDirtyRepaint());

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
                Undo.RecordObject(_guidComp, "Orphaning Guid from Component");
                _guidComp.RemoveComponentGuid(componentGuid);

                _guidComp.OnValidate();
                serializedObject.Update();

                EditorUtility.SetDirty(_guidComp);

                RebuildGuidList(_componentGUIDsContainer);
            };
            labelFieldComponentGuid[1].Add(button);

            element.Add(icon);
            element.Add(labelFieldComponentGuid);

            elements.Add((element, componentGuid.CachedComponent.GetComponentIndex()));
        }

        // ------------------------------
        // Build Candidate/Available List
        // ------------------------------

        foreach (Component component in _guidComp.GetComponentGuidCandidates())
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
                Undo.RecordObject(_guidComp, "Assigning Guid to Component");
                _guidComp.componentGuids.Add(new ComponentGuid
                {
                    CachedComponent = component,
                    OwningGameObject = _guidComp.gameObject
                });

                _guidComp.OnValidate();
                serializedObject.Update();

                EditorUtility.SetDirty(_guidComp);

                RebuildGuidList(_componentGUIDsContainer);
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

        var orphanedList = GuidManagerEditor.GetOrphanedGuids(_guidComp.transformGuid).ToList();

        if (orphanedList.Count > 0)
        {
            VisualElement separator = new VisualElement
            {
                enabledSelf = false,
                focusable = false
            };
            separator.AddToClassList("horizontal-separator");
            parent.Add(separator);
        }

        foreach (GuidReferenceMappings.OrphanedGuidItemInfo orphanedGuid in orphanedList)
        {
            string tooltip = "Orphaned: Cannot find owner.\nAssign new component or remove this SerializableGuid.";
            VisualElement element = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center
                }
            };

            ObjectField objectFieldOrphanedGuid = new ObjectField
            {
                tooltip = tooltip,
                objectType = orphanedGuid.GuidItem.ownerType.Type
            };

            SetupComponentPicker(objectFieldOrphanedGuid);

            objectFieldOrphanedGuid.name = "error-guid-orphaned-object-field";
            objectFieldOrphanedGuid.RegisterValueChangedCallback(evt => {});

            objectFieldOrphanedGuid.RegisterCallback<DragUpdatedEvent>(evt =>
            {
                Component draggedObject = DragAndDrop.objectReferences[0] as Component;
                if (!draggedObject || !IsChildOf(draggedObject.gameObject, _guidComp.gameObject) ||
                    draggedObject.GetType() != orphanedGuid.GuidItem.ownerType.Type)
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Rejected;
                    evt.StopImmediatePropagation();
                }
            }, TrickleDown.TrickleDown);

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
            labelFieldComponentGuid.value = orphanedGuid.GuidItem.guid.ToString();

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
                GuidManagerEditor.Unregister(orphanedGuid);

                RebuildGuidList(_componentGUIDsContainer);
            };
            labelFieldComponentGuid[0].Add(button);

            Image icon = new Image { image = EditorGUIUtility.IconContent("Error").image };
            icon.name = "guid-component-icon";
            element.Add(icon);
            element.Add(customField);

            parent.Add(element);
        }
    }

    private void SetupComponentPicker(ObjectField objectField)
    {
        SearchProvider searchProvider = new SearchProvider("local_components", "Local Components")
        {
            showDetailsOptions = ShowDetailsOptions.None,
            fetchItems = (context, items, provider) => ComponentPickerFetchItemsHandler(context, provider),
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
            selectHandler = ComponentPickerSelectHandler,
            trackingHandler = ComponentPickerTrackingHandler,
            position = SearchUtils.GetMainWindowCenteredPosition(new Vector2(600, 400)),
            hideTabs = true,
            group = "all",
            queryBuilderEnabled = false
        };

        objectField.searchContext = context;
        objectField.searchViewState = viewState;
    }

    private IEnumerable<SearchItem> ComponentPickerFetchItemsHandler(SearchContext searchContext,
        SearchProvider searchProvider)
    {
        if (_guidComp.gameObject == null)
        {
            yield break;
        }

        int index = 0;
        foreach (Component component in _guidComp.gameObject.GetComponents<Component>())
        {
            if (GuidComponentExcluders.Excluders.Contains(component.GetType()))
            {
                continue;
            }

            if (_guidComp.componentGuids.Exists(guid => guid.CachedComponent == component))
            {
                continue;
            }

            yield return searchProvider.CreateItem(searchContext, component.GetEntityId().ToString(), index++,
                component.GetType().Name,
                component.GetType().Name,
                (Texture2D)EditorGUIUtility.ObjectContent(null, component.GetType()).image, component);
        }
    }

    private void ComponentPickerSelectHandler(SearchItem searchItem, bool canceled)
    {
        if (canceled)
        {
            return;
        }

        // GuidManagerEditor.SetGuidState(_guidComp.gameObject,);
        searchItem.ToObject<Component>();
    }

    private static void ComponentPickerTrackingHandler(SearchItem searchItem) {}

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

                    InspectorElement inspector = InspectorElementField.GetValue(element) as InspectorElement;
                    IMGUIContainer footer = FooterElementField.GetValue(element) as IMGUIContainer;
                    _inspectorHeader =
                        new InspectorHeader(serializedObject, inspector, footer,
                            new InspectorHeader.DrawSettings
                            {
                                DrawEnableToggle = false,
                                DrawHelpIcon = false,
                                DrawPresetIcon = false,
                                HeaderTitleOverride = "Guid"
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