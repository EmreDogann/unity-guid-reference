#if COMPONENT_NAMES
using Sisus.ComponentNames;
#endif
using System;
using System.Reflection;
using Unity.Profiling;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

[CustomEditor(typeof(GuidComponent))]
public class GuidComponentDrawer : Editor
{
    private GuidComponent _guidComp;
    private InspectorHeader.InspectorItem _inspectorHeader;

    // SerializedProperty here only used for remembering the state of foldout:
    // https://discussions.unity.com/t/editorguilayout-foldout-no-way-to-remember-state/36422/6
    private SerializedProperty _serializedGuidProp;

    public override VisualElement CreateInspectorGUI()
    {
        VisualElement root = new VisualElement();

        _serializedGuidProp = serializedObject.FindProperty("_guid");
        _guidComp = (GuidComponent)serializedObject.targetObject;

        CreateHeaderGUI(root);

        if (_guidComp.IsAssetOnDisk())
        {
            HelpBox helpBox = new HelpBox(
                "Guid Components do not work in prefab assets.\nHowever, instances of prefabs in a scene will receive the correct guid information.",
                HelpBoxMessageType.Warning);
            root.Add(helpBox);
        }
        else
        {
            CustomLabelField labelFieldGOGuid = new CustomLabelField("GameObject GUID");
            Label goLabelValue = new Label(_guidComp.GetGuid().ToString())
            {
                style = { flexGrow = 1 }
            };
            goLabelValue.AddToClassList(CustomLabelField.labelUssClassName);
            labelFieldGOGuid.SetCustomContent(goLabelValue);
            root.Add(labelFieldGOGuid);

            if (_guidComp.GetComponentGUIDs().Count > 0)
            {
                Foldout foldout = new Foldout
                {
                    text = "Component GUIDs",
                    value = _serializedGuidProp.isExpanded,
                    toggleOnLabelClick = true,
                    style = { unityFontStyleAndWeight = FontStyle.Bold }
                };
                foldout.contentContainer.style.unityFontStyleAndWeight = FontStyle.Normal;
                root.Add(foldout);

                // Populate rows
                RebuildGuidList(foldout.contentContainer);

                // Persist expansion state
                foldout.RegisterValueChangedCallback(evt =>
                {
                    foldout.contentContainer.Clear();
                    if (evt.newValue)
                    {
                        RebuildGuidList(foldout.contentContainer);
                    }
                });
            }
        }

        return root;
    }

    private void RebuildGuidList(VisualElement parent)
    {
        foreach (ComponentGuid componentGuid in _guidComp.GetComponentGUIDs())
        {
#if COMPONENT_NAMES
            CustomLabelField labelFieldComponentGuid =
                new CustomLabelField($"{componentGuid.cachedComponent.GetName()}");
#else
            CustomLabelField labelFieldComponentGuid =
                new CustomLabelField($"{componentGuid.cachedComponent.GetType().Name}");
#endif

            Label componentLabelValue = new Label(componentGuid.Guid.ToString())
            {
                style =
                {
                    flexGrow = 1
                }
            };
            componentLabelValue.AddToClassList(CustomLabelField.labelUssClassName);
            labelFieldComponentGuid.SetCustomContent(componentLabelValue);

            parent.Add(labelFieldComponentGuid);
        }
    }

    // ---- Header GUI ----
    private static ProfilerMarker _createInspectorGUIProfileMarker =
        new ProfilerMarker($"{nameof(GuidComponentDrawer)}.{nameof(CreateInspectorGUI)}");
    private static readonly Type InspectorWindowType =
        typeof(EditorWindow).Assembly.GetType("UnityEditor.InspectorWindow");
    private static readonly Type PropertyEditorType =
        typeof(EditorWindow).Assembly.GetType("UnityEditor.PropertyEditor");
    private static readonly Type EditorElementType =
        typeof(EditorWindow).Assembly.GetType("UnityEditor.UIElements.EditorElement");
    private static readonly FieldInfo EditorElement =
        PropertyEditorType.GetField("m_EditorsElement", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly FieldInfo HeaderField =
        EditorElementType.GetField("m_Header", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly FieldInfo InspectorElement =
        EditorElementType.GetField("m_InspectorElement", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly FieldInfo FooterElement =
        EditorElementType.GetField("m_Footer", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly FieldInfo EditorTargetObject =
        EditorElementType.GetField("m_EditorTarget", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly Action<VisualElement, Rect> DragRect =
        ReflectionUtility.BuildFieldSetter<VisualElement, Rect>(EditorElementType,
            "m_DragRect", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly Action<VisualElement, Rect> ContentRect =
        ReflectionUtility.BuildFieldSetter<VisualElement, Rect>(EditorElementType,
            "m_ContentRect", BindingFlags.NonPublic | BindingFlags.Instance);
    private VisualElement m_EditorsElement;
    private VisualElement EditorsElement => m_EditorsElement ??= GetEditorVisualElement();

    private static VisualElement GetEditorVisualElement()
    {
        EditorWindow window = EditorWindow.GetWindow(InspectorWindowType);
        if (window)
        {
            return EditorElement.GetValue(window) as VisualElement;
        }

        return null;
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

            // Inject UI Toolkit header.
            root.RegisterCallback<AttachToPanelEvent>(_ =>
            {
                var foundAll = EditorsElement.Children();
                foreach (VisualElement element in foundAll)
                {
                    if (element.GetType() != EditorElementType)
                    {
                        continue;
                    }

                    Object targetObject = EditorTargetObject.GetValue(element) as Object;
                    if (targetObject is not GuidComponent)
                    {
                        continue;
                    }

                    Debug.Log("attaching to panel");

                    element.RegisterCallback<DragUpdatedEvent>(evt =>
                    {
                        DragRect((VisualElement)evt.currentTarget, ((VisualElement)evt.currentTarget).layout);
                        ContentRect((VisualElement)evt.currentTarget, ((VisualElement)evt.currentTarget).layout);
                    });

                    InspectorElement inspector = InspectorElement.GetValue(element) as InspectorElement;
                    IMGUIContainer footer = FooterElement.GetValue(element) as IMGUIContainer;
                    _inspectorHeader =
                        new InspectorHeader.InspectorItem(serializedObject, inspector, footer,
                            new InspectorHeader.InspectorItem.DrawSettings
                            {
                                DrawEnableToggle = false,
                                DrawHelpIcon = false,
                                DrawPresetIcon = false
                            });

                    element.Insert(0, _inspectorHeader);

                    // Disabling the default IMGUI header. Won't delete it at the risk of some internal Unity logic expecting it to be present and therefore breaking internal contracts.
                    IMGUIContainer IMGUIHeader = HeaderField.GetValue(element) as IMGUIContainer;

                    _inspectorHeader.name = IMGUIHeader.name;
                    IMGUIHeader.name = "";

                    IMGUIHeader.visible = false;
                    IMGUIHeader.style.display = DisplayStyle.None;
                    IMGUIHeader.onGUIHandler = () => {};

                    root.RegisterCallback<DetachFromPanelEvent>(_ =>
                    {
                        // root.UnregisterCallback<DragUpdatedEvent>(DragUpdatedCallback);

                        if (_inspectorHeader == null)
                        {
                            VisualElement inspectorRoot = EditorsElement;
                            if (inspectorRoot == null)
                            {
                                return;
                            }

                            var foundAll = inspectorRoot.Children();
                            foreach (VisualElement element in foundAll) {}

                            for (int i = element.childCount - 1; i >= 0; i--)
                            {
                                VisualElement childElement = element[i];
                                if (childElement.GetType() == typeof(InspectorHeader.InspectorItem))
                                {
                                    childElement.RemoveFromHierarchy();
                                }
                            }
                        }
                        else
                        {
                            _inspectorHeader.RemoveFromHierarchy();
                        }
                    });

                    break;
                }
            });
        }
    }
}