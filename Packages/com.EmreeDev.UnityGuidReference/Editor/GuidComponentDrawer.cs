#if COMPONENT_NAMES
using Sisus.ComponentNames;
#endif
using System;
using System.Collections.Generic;
using System.Reflection;
using Unity.Profiling;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

public class CustomInspectorField : BaseField<string>
{
    // Exposed for customization
    private readonly VisualElement ContentElement;

    public CustomInspectorField(string labelText)
        : base(labelText, new VisualElement())
    {
        ContentElement = this.Q<VisualElement>(className: inputUssClassName);
        // Ensure horizontal layout for content
        ContentElement.style.flexDirection = FlexDirection.Row;

        // Match PropertyField spacing exactly
        AddToClassList(alignedFieldUssClassName);

        // Add extra 1px to bottom margin, due to off-by-1 pixel offset compared to default fields like Object/Property fields.
        labelElement.style.marginBottom = 1;
    }

    /// <summary>
    ///     Replace the label area with custom UI.
    /// </summary>
    public void SetCustomLabel(VisualElement customLabel)
    {
        labelElement.Clear();
        labelElement.Add(customLabel);
    }

    /// <summary>
    ///     Replace the content area with custom UI.
    /// </summary>
    public void SetCustomContent(VisualElement customContent)
    {
        ContentElement.Clear();
        ContentElement.Add(customContent);
    }
}

[CustomEditor(typeof(GuidComponent))]
public class GuidComponentDrawer : Editor
{
    private GuidComponent _guidComp;

    // SerializedProperty here only used for remembering the state of foldout:
    // https://discussions.unity.com/t/editorguilayout-foldout-no-way-to-remember-state/36422/6
    private SerializedProperty _serializedGuidProp;

    public override VisualElement CreateInspectorGUI()
    {
        VisualElement root = new VisualElement();

        _serializedGuidProp = serializedObject.FindProperty("_guid");
        _guidComp = (GuidComponent)serializedObject.targetObject;
        FindHeaderGUI();

        if (_guidComp.IsAssetOnDisk())
        {
            HelpBox helpBox = new HelpBox(
                "Guid Components do not work in prefab assets.\nHowever, instances of prefabs in a scene will receive the correct guid information.",
                HelpBoxMessageType.Warning);
            root.Add(helpBox);
        }
        else
        {
            CustomInspectorField inspectorFieldGOGuid = new CustomInspectorField("GameObject GUID");
            Label goLabelValue = new Label(_guidComp.GetGuid().ToString())
            {
                style = { flexGrow = 1 }
            };
            goLabelValue.AddToClassList(CustomInspectorField.labelUssClassName);
            inspectorFieldGOGuid.SetCustomContent(goLabelValue);
            root.Add(inspectorFieldGOGuid);

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
            CustomInspectorField inspectorFieldComponentGuid =
                new CustomInspectorField($"{componentGuid.cachedComponent.GetName()}");
#else
            CustomInspectorField inspectorFieldComponentGuid =
                new CustomInspectorField($"{componentGuid.cachedComponent.GetType().Name}");
#endif

            Label componentLabelValue = new Label(componentGuid.Guid.ToString())
            {
                style =
                {
                    flexGrow = 1
                }
            };
            componentLabelValue.AddToClassList(CustomInspectorField.labelUssClassName);
            inspectorFieldComponentGuid.SetCustomContent(componentLabelValue);

            parent.Add(inspectorFieldComponentGuid);
        }
    }

    // ---- Header GUI ----
    private static ProfilerMarker _profileMarker =
        new ProfilerMarker($"{nameof(GuidComponentDrawer)}.{nameof(OnInspectorGUI)}");
    private static ProfilerMarker _profileMarker2 = new ProfilerMarker($"{nameof(GuidComponentDrawer)}.OnTitlebarGUI");
    private static readonly Type Type = typeof(EditorWindow).Assembly.GetType("UnityEditor.InspectorWindow");
    private static readonly Type Type2 = typeof(EditorWindow).Assembly.GetType("UnityEditor.PropertyEditor");
    private static readonly Type Type3 = typeof(EditorWindow).Assembly.GetType("UnityEditor.UIElements.EditorElement");
    private static readonly FieldInfo Field =
        Type2.GetField("m_EditorsElement", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly FieldInfo Field2 =
        Type3.GetField("m_Header", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly FieldInfo Field3 =
        Type3.GetField("m_EditorTarget", BindingFlags.NonPublic | BindingFlags.Instance);
    private static VisualElement m_EditorsElement;
    private static VisualElement EditorsElement => m_EditorsElement ??= GetEditorVisualElement();
    private readonly Dictionary<VisualElement, Action> _callbacks = new Dictionary<VisualElement, Action>();

    private static VisualElement GetEditorVisualElement()
    {
        EditorWindow window = EditorWindow.GetWindow(Type);
        if (window)
        {
            return Field.GetValue(window) as VisualElement;
        }

        return null;
    }

    private void FindHeaderGUI()
    {
        using (_profileMarker.Auto())
        {
            VisualElement inspectorRoot = EditorsElement;
            if (inspectorRoot == null)
            {
                return;
            }

            var foundAll = EditorsElement.Children();
            foreach (VisualElement element in foundAll)
            {
                if (element.GetType() != Type3)
                {
                    continue;
                }

                Object localTarget = Field3.GetValue(element) as Object;
                if (localTarget is not GuidComponent)
                {
                    continue;
                }

                IMGUIContainer value2 = Field2.GetValue(element) as IMGUIContainer;
                Action callback = null;
                if (_callbacks.TryGetValue(element, out Action found))
                {
                    callback = found;
                }
                else
                {
                    callback = _callbacks[element] = OnHeaderGUICallback;
                }

                if (value2 != null)
                {
                    value2.onGUIHandler = callback;
                }

                void OnHeaderGUICallback()
                {
                    using (_profileMarker2.Auto())
                    {
                        try
                        {
                            EditorGUILayout.InspectorTitlebar(true, _guidComp, true);
                        }
                        catch (Exception e)
                        {
                            Debug.LogException(e);
                        }
                    }
                }
            }
        }
    }
}