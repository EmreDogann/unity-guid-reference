using System;
using UnityEditor;
using UnityEngine;

// Using a property drawer to allow any class to have a field of type GuidRefernce and still get good UX
// If you are writing your own inspector for a class that uses a GuidReference, drawing it with
// EditorLayout.PropertyField(prop) or similar will get this to show up automatically
[CustomPropertyDrawer(typeof(BaseGuidReference<>), true)]
public class GuidReferenceComponentDrawer : PropertyDrawer
{
    private bool IsInit;
    private SerializedProperty guidProp;
    private SerializedProperty sceneProp;
    private SerializedProperty nameProp;

    private Type targetType;
    private bool showExtraInfo;
    private const float LINE_PADDING = 2.0f;
    private const float BUTTON_PADDING = 1.0f;

    // cache off GUI content to avoid creating garbage every frame in editor
    private readonly GUIContent sceneLabel =
        new GUIContent("Containing Scene", "The target object is expected in this scene asset.");
    private readonly GUIContent clearButtonGUI = new GUIContent("Clear", "Remove Cross Scene Reference");

    // add an extra line to display source scene for targets
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return base.GetPropertyHeight(property, label) +
               (showExtraInfo ? EditorGUIUtility.singleLineHeight + LINE_PADDING * 3.0f : 0.0f);
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (!IsInit)
        {
            guidProp = property.FindPropertyRelative("serializedGuid");
            nameProp = property.FindPropertyRelative("cachedName");
            sceneProp = property.FindPropertyRelative("cachedScene");

            targetType = fieldInfo.FieldType.GetGenericArguments()[0];

            showExtraInfo = guidProp.isExpanded;
            IsInit = true;
        }

        position.height = EditorGUIUtility.singleLineHeight;

        // Using BeginProperty / EndProperty on the parent property means that
        // prefab override logic works on the entire property.
        EditorGUI.BeginProperty(position, label, property);

        // Draw prefix label, returning the new rect we can draw in
        Rect guidCompPosition = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);

        Guid currentGuid;
        Component currentComponent = null;

        // working with array properties is a bit unwieldy
        // you have to get the property at each index manually
        byte[] byteArray = new byte[16];
        int arraySize = guidProp.arraySize;
        for (int i = 0; i < arraySize; ++i)
        {
            SerializedProperty byteProp = guidProp.GetArrayElementAtIndex(i);
            byteArray[i] = (byte)byteProp.intValue;
        }

        currentGuid = new Guid(byteArray);
        currentComponent = GuidManager.ResolveGuid(currentGuid, targetType);
        GuidComponent currentGuidComponent =
            currentComponent != null ? currentComponent.GetComponent<GuidComponent>() : null;

        GuidComponent component = null;

        // Debug.Log($"Guid: {currentGuid}, Component: {currentComponent}");
        if (currentGuid != Guid.Empty && currentGuidComponent == null)
        {
            // if our reference is set, but the target isn't loaded, we display the target and the scene it is in, and provide a way to clear the reference
            float buttonWidth = 55.0f;

            guidCompPosition.xMax -= buttonWidth;

            bool guiEnabled = GUI.enabled;
            GUI.enabled = false;
            EditorGUI.LabelField(guidCompPosition,
                new GUIContent(nameProp.stringValue, "Target GameObject is not currently loaded."),
                EditorStyles.objectField);
            GUI.enabled = guiEnabled;

            Rect clearButtonRect = new Rect(guidCompPosition);
            clearButtonRect.xMin = guidCompPosition.xMax;
            clearButtonRect.xMax += buttonWidth;

            if (GUI.Button(clearButtonRect, clearButtonGUI, EditorStyles.miniButton))
            {
                ClearPreviousGuid();
            }
        }
        else
        {
            float settingsButtonWidth = 20.0f;
            bool shouldAutoSize = EditorGUIUtility.labelWidth >
                                  EditorGUIUtility.currentViewWidth - settingsButtonWidth * 2 - BUTTON_PADDING * 2;

            Rect settingsRect = position;
            settingsRect.width = settingsButtonWidth;
            settingsRect.x = EditorGUIUtility.currentViewWidth - settingsButtonWidth - BUTTON_PADDING;
            settingsRect.xMax +=
                shouldAutoSize
                    ? EditorGUIUtility.labelWidth - EditorGUIUtility.currentViewWidth + settingsButtonWidth * 2.0f +
                      BUTTON_PADDING * 2.0f
                    : 0.0f;
            settingsRect.xMin = settingsRect.xMax - settingsButtonWidth;

            bool settingsPressed = EditorGUI.DropdownButton(settingsRect, EditorGUIUtility.IconContent("_Popup"),
                FocusType.Keyboard,
                EditorStyles.miniButtonRight);
            if (settingsPressed)
            {
                GenericMenu menu = new GenericMenu();
                menu.DropDown(settingsRect);
                menu.AddItem(new GUIContent("Show Extra Details..."), guidProp.isExpanded,
                    () =>
                    {
                        guidProp.isExpanded = !guidProp.isExpanded;
                        showExtraInfo = guidProp.isExpanded;
                    });

                menu.ShowAsContext();
            }

            guidCompPosition.xMax -= settingsButtonWidth + BUTTON_PADDING;
            guidCompPosition.xMax +=
                shouldAutoSize
                    ? EditorGUIUtility.labelWidth - EditorGUIUtility.currentViewWidth + settingsButtonWidth * 2 +
                      BUTTON_PADDING * 2
                    : 0.0f;

            // If our object is loaded, we can simply use an object field directly
            component =
                EditorGUI.ObjectField(guidCompPosition, currentGuidComponent, typeof(GuidComponent), true) as
                    GuidComponent;
        }

        if (currentGuidComponent != null && component == null)
        {
            ClearPreviousGuid();
        }

        // if we have a valid reference, draw the scene name of the scene it lives in so users can find it
        if (component != null)
        {
            nameProp.stringValue = component.name;
            string scenePath = component.gameObject.scene.path;
            sceneProp.objectReferenceValue = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);

            // only update the GUID Prop if something changed. This fixes multi-edit on GUID References
            if (component != currentGuidComponent)
            {
                byteArray = component.GetGuid(targetType).ToByteArray();
                arraySize = guidProp.arraySize;
                for (int i = 0; i < arraySize; ++i)
                {
                    SerializedProperty byteProp = guidProp.GetArrayElementAtIndex(i);
                    byteProp.intValue = byteArray[i];
                }
            }
        }

        if (showExtraInfo)
        {
            bool shouldAutoSize = EditorGUIUtility.labelWidth >
                                  EditorGUIUtility.currentViewWidth - 22.0f;
            EditorGUI.indentLevel++;

            Rect extraInfoRect = position;
            extraInfoRect.y += EditorGUIUtility.singleLineHeight + LINE_PADDING * 2.0f;
            extraInfoRect.xMax -= BUTTON_PADDING;
            extraInfoRect.xMax +=
                shouldAutoSize
                    ? EditorGUIUtility.labelWidth - EditorGUIUtility.currentViewWidth + 22.0f
                    : 0.0f;

            bool cachedGUIState = GUI.enabled;
            GUI.enabled = false;
            EditorGUI.ObjectField(extraInfoRect, sceneLabel, sceneProp.objectReferenceValue, typeof(SceneAsset), false);
            GUI.enabled = cachedGUIState;

            EditorGUI.indentLevel--;
        }

        EditorGUI.EndProperty();
    }

    private void ClearPreviousGuid()
    {
        nameProp.stringValue = string.Empty;
        sceneProp.objectReferenceValue = null;

        int arraySize = guidProp.arraySize;
        for (int i = 0; i < arraySize; ++i)
        {
            SerializedProperty byteProp = guidProp.GetArrayElementAtIndex(i);
            byteProp.intValue = 0;
        }
    }
}