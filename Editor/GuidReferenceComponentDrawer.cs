﻿using System;
using UnityEditor;
using UnityEngine;
#if COMPONENT_NAMES
using Sisus.ComponentNames;
#endif

// Using a property drawer to allow any class to have a field of type GuidReference and still get good UX
// If you are writing your own inspector for a class that uses a GuidReference, drawing it with
// EditorLayout.PropertyField(prop) or similar will get this to show up automatically
[CustomPropertyDrawer(typeof(GuidReference<>), true)]
public class GuidReferenceComponentDrawer : PropertyDrawer
{
    private bool _isInit;
    private SerializedProperty _guidProp;
    private SerializedProperty _sceneProp;
    private SerializedProperty _nameProp;
    private SerializedProperty _goNameProp;

    private Type _targetType;
    private bool _showExtraInfo;
    private const float LINE_PADDING = 1.0f;
    private const float BUTTON_PADDING = 1.0f;
    private const float BUTTON_WIDTH = 20.0f;
    private const float NUM_OF_BUTTONS = 2f;
    private GuidObjectField _fieldDrawer;

    private string sceneName;

    // cache off GUI content to avoid creating garbage every frame in editor
    private readonly GUIContent clearButtonGUI =
        EditorGUIUtility.TrIconContent("d_Toolbar Minus", "Remove Cross Scene Reference");
    private readonly GUIContent _sceneLabel =
        new GUIContent("Containing Scene", "The target object is expected in this scene asset.");
    private readonly GUIContent warningLabel = EditorGUIUtility.IconContent("conflict-icon");

    private void Initialize(SerializedProperty property)
    {
        if (!_isInit)
        {
            _guidProp = property.FindPropertyRelative("serializedGuid");
            _nameProp = property.FindPropertyRelative("cachedName");
            _goNameProp = property.FindPropertyRelative("cachedGOName");
            _sceneProp = property.FindPropertyRelative("cachedScene");

            sceneName = AssetDatabase.GetAssetOrScenePath(_sceneProp.objectReferenceValue)
                .TrimEnd(".unity".ToCharArray());
            sceneName = sceneName.Split('/')[^1];

            _targetType = fieldInfo.FieldType.GetGenericArguments()[0];

            _fieldDrawer = new GuidObjectField(_targetType, new MultiSceneComponentsProvider(_targetType));
            _fieldDrawer.OnObjectChanged += OnObjectChanged;

            _showExtraInfo = _guidProp.isExpanded;
            _isInit = true;
        }
    }

    private void OnObjectChanged(SelectedGuidObject selectedGuidObject)
    {
        // Debug.Log($"GuidComponent: {selectedGuidObject.GuidComponent}, Component: {selectedGuidObject.ComponentRef}");
        if (selectedGuidObject?.GuidComponent != null)
        {
            Component component = selectedGuidObject.ComponentRef != Guid.Empty
                ? selectedGuidObject.GuidComponent.GetComponentFromGuid(selectedGuidObject.ComponentRef)
                : selectedGuidObject.GuidComponent;

#if COMPONENT_NAMES
            _nameProp.stringValue = cachedComponent.GetName();
#else
            _nameProp.stringValue = component.GetType().Name;
#endif
            _goNameProp.stringValue = component.gameObject.name;
            string scenePath = component.gameObject.scene.path;
            sceneName = scenePath.TrimEnd(".unity".ToCharArray());
            sceneName = sceneName.Split('/')[^1];

            _sceneProp.objectReferenceValue = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);

            byte[] byteArray = new byte[16];
            int arraySize = _guidProp.arraySize;
            if (selectedGuidObject.ComponentRef != Guid.Empty)
            {
                byteArray = selectedGuidObject.ComponentRef.ToByteArray();
            }
            else
            {
                byteArray = selectedGuidObject.GuidComponent.GetGuid(_targetType).ToByteArray();
            }

            arraySize = _guidProp.arraySize;
            for (int i = 0; i < arraySize; ++i)
            {
                SerializedProperty byteProp = _guidProp.GetArrayElementAtIndex(i);
                byteProp.intValue = byteArray[i];
            }
        }
        else
        {
            ClearPreviousGuid();
        }

        _guidProp.serializedObject.ApplyModifiedProperties();
    }

    // add an extra line to display source scene for targets
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        Initialize(property);
        return _fieldDrawer.GetPropertyHeight(property, label) +
               (_showExtraInfo ? EditorGUIUtility.singleLineHeight * 2 + 6.0f : 0.0f);
        ;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        Initialize(property);
        position.height = EditorGUIUtility.singleLineHeight;

        _guidProp.serializedObject.Update();

        // Using BeginProperty / EndProperty on the parent property means that
        // prefab override logic works on the entire property.
        EditorGUI.BeginProperty(position, label, property);

        // Draw prefix label, returning the new rect we can draw in
        // Rect guidCompPosition = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);

        // Working with array properties is a bit unwieldy
        // you have to get the property at each index manually
        byte[] byteArray = new byte[16];
        int arraySize = _guidProp.arraySize;
        for (int i = 0; i < arraySize; ++i)
        {
            SerializedProperty byteProp = _guidProp.GetArrayElementAtIndex(i);
            byteArray[i] = (byte)byteProp.intValue;
        }

        Guid currentGuid = new Guid(byteArray);
        Component currentComponent = GuidManager.ResolveGuid(currentGuid, _targetType);

        Event e = Event.current;
        if (e.type == EventType.MouseDown && e.button == 1 && position.Contains(e.mousePosition))
        {
            GenericMenu menu = CreateContextMenu(null, currentGuid);
            menu.ShowAsContext();
        }

        // Debug.Log($"Guid: {currentGuid}, Component: {currentComponent?.GetName()}");
        if (currentGuid != Guid.Empty && currentComponent == null)
        {
            Rect guidCompPosition = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);
            // if our reference is set, but the target isn't loaded, we display the target and the scene it is in, and provide a way to clear the reference
            float buttonWidth = 19.0f;
            guidCompPosition.xMax -= buttonWidth + BUTTON_PADDING * 2.0f;

            warningLabel.text = $" {_nameProp.stringValue} -> {_goNameProp.stringValue} -> {sceneName}";
            warningLabel.tooltip =
                $"Reference was not found / is not loaded.\n-------------------------------------\n<b>{_nameProp.stringValue}</b> (Component)\n\tv\n<b>{_goNameProp.stringValue}</b> (Game Object)\n\tv\n<b>{sceneName}</b> (Scene)";

            bool guiEnabled = GUI.enabled;
            GUI.enabled = false;

            Color guiBgColor = GUI.backgroundColor;
            GUI.backgroundColor = Color.yellow;
            Vector2 originalIconSize = EditorGUIUtility.GetIconSize();

            EditorGUIUtility.SetIconSize(new Vector2(warningLabel.image.width, warningLabel.image.height));
            EditorGUI.LabelField(guidCompPosition, warningLabel, EditorStyles.objectField);

            GUI.enabled = guiEnabled;
            GUI.backgroundColor = guiBgColor;
            EditorGUIUtility.SetIconSize(originalIconSize);

            Rect clearButtonRect = new Rect(guidCompPosition);
            clearButtonRect.xMin = guidCompPosition.xMax + BUTTON_PADDING;
            clearButtonRect.xMax += buttonWidth + 2.0f;

            if (GUI.Button(clearButtonRect, clearButtonGUI, EditorStyles.miniButtonRight))
            {
                ClearPreviousGuid();
            }
        }
        else
        {
            // Magic number "* 2.0f" - used to match start of picker button.
            bool shouldAutoSize = EditorGUIUtility.labelWidth >
                                  EditorGUIUtility.currentViewWidth - BUTTON_WIDTH * NUM_OF_BUTTONS -
                                  BUTTON_PADDING * 2.0f;

            Rect settingsRect = position;
            settingsRect.width = BUTTON_WIDTH;
            settingsRect.x = EditorGUIUtility.currentViewWidth - BUTTON_WIDTH - BUTTON_PADDING;
            // Magic "1.0f" used for visual padding.
            settingsRect.xMax += 1.0f +
                                 (shouldAutoSize
                                     ? EditorGUIUtility.labelWidth - EditorGUIUtility.currentViewWidth +
                                       BUTTON_WIDTH * NUM_OF_BUTTONS +
                                       BUTTON_PADDING
                                     : 0.0f);
            settingsRect.xMin = settingsRect.xMax - BUTTON_WIDTH;

            bool settingsPressed = EditorGUI.DropdownButton(settingsRect, EditorGUIUtility.IconContent("_Popup"),
                FocusType.Keyboard,
                EditorStyles.miniButtonRight);
            if (settingsPressed)
            {
                GenericMenu menu = CreateContextMenu(settingsRect, currentGuid);
                menu.ShowAsContext();
            }

            Rect guidCompPosition = new Rect(settingsRect);
            guidCompPosition.xMin = position.xMin;
            guidCompPosition.xMax -= BUTTON_WIDTH + BUTTON_PADDING;

            // If our object is loaded, we can simply use an object field directly
            _fieldDrawer.DrawField(guidCompPosition, label, currentComponent);
        }

        if (_showExtraInfo)
        {
            bool shouldAutoSize = EditorGUIUtility.labelWidth > EditorGUIUtility.currentViewWidth - 22.0f;
            EditorGUI.indentLevel++;

            Rect extraInfoRect = position;
            extraInfoRect.y += EditorGUIUtility.singleLineHeight + LINE_PADDING * 2.0f;
            extraInfoRect.xMax -= BUTTON_PADDING;
            extraInfoRect.xMax +=
                shouldAutoSize
                    ? EditorGUIUtility.labelWidth - EditorGUIUtility.currentViewWidth + 22.0f
                    : 0.0f;

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUI.TextField(extraInfoRect, new GUIContent("GUID"), currentGuid.ToString(),
                    EditorStyles.textField);
                extraInfoRect.y += EditorGUIUtility.singleLineHeight + LINE_PADDING * 2.0f;
                EditorGUI.ObjectField(extraInfoRect, _sceneLabel, _sceneProp.objectReferenceValue, typeof(SceneAsset),
                    false);
            }

            EditorGUI.indentLevel--;
        }

        EditorGUI.EndProperty();
    }

    private void ClearPreviousGuid()
    {
        _nameProp.stringValue = string.Empty;
        _sceneProp.objectReferenceValue = null;

        int arraySize = _guidProp.arraySize;
        for (int i = 0; i < arraySize; ++i)
        {
            SerializedProperty byteProp = _guidProp.GetArrayElementAtIndex(i);
            byteProp.intValue = 0;
        }
    }

    private GenericMenu CreateContextMenu(Rect? spawnAtRect, Guid currentGuid)
    {
        GenericMenu menu = new GenericMenu();
        if (spawnAtRect.HasValue)
        {
            menu.DropDown(spawnAtRect.Value);
        }

        menu.AddItem(new GUIContent("Open Extra Details"), _guidProp.isExpanded,
            () =>
            {
                _guidProp.isExpanded = !_guidProp.isExpanded;
                _showExtraInfo = _guidProp.isExpanded;
            });
        menu.AddSeparator("");
        menu.AddItem(new GUIContent("Copy GUID"), false,
            () => { EditorGUIUtility.systemCopyBuffer = currentGuid.ToString("N"); });

        return menu;
    }
}