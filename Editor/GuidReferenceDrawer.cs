using System;
using UnityEditor;
using UnityEngine;

// Using a property drawer to allow any class to have a field of type GuidReference and still get good UX
// If you are writing your own inspector for a class that uses a GuidReference, drawing it with
// EditorLayout.PropertyField(prop) or similar will get this to show up automatically
[CustomPropertyDrawer(typeof(GuidReference), true)]
public class GuidReferenceDrawer : PropertyDrawer
{
    private bool _isInit;
    private SerializedProperty _guidProp;
    private SerializedProperty _sceneProp;
    private SerializedProperty _nameProp;

    private readonly Type _targetType = typeof(GameObject);
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
            _sceneProp = property.FindPropertyRelative("cachedScene");

            sceneName = AssetDatabase.GetAssetOrScenePath(_sceneProp.objectReferenceValue)
                .TrimEnd(".unity".ToCharArray());
            sceneName = sceneName.Split('/')[^1];

            _fieldDrawer = new GuidObjectField(_targetType, new MultiSceneComponentsProvider(_targetType));
            _fieldDrawer.OnObjectChanged += OnObjectChanged;

            _showExtraInfo = _guidProp.isExpanded;
            _isInit = true;
        }
    }

    private void OnObjectChanged(SelectedGuidObject selectedGuidObject)
    {
        // Debug.Log($"GuidComponent: {selectedGuidObject.GuidComponent}, Component: {selectedGuidObject.ComponentRef}");
        if (selectedGuidObject?.GuidComponent)
        {
            Component component = selectedGuidObject.ComponentCache
                ? selectedGuidObject.GuidComponent.GetComponentFromGuid(selectedGuidObject.ComponentRef)
                : selectedGuidObject.GuidComponent;

            _nameProp.stringValue = component.name;
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
        _nameProp.serializedObject.ApplyModifiedProperties();
        _sceneProp.serializedObject.ApplyModifiedProperties();
    }

    // add an extra line to display source scene for targets
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        Initialize(property);
        return _fieldDrawer.GetPropertyHeight(property, label) +
               (_showExtraInfo ? EditorGUIUtility.singleLineHeight * 2 + 6.0f : 0.0f);
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        Initialize(property);
        position.height = EditorGUIUtility.singleLineHeight;

        // Using BeginProperty / EndProperty on the parent property means that
        // prefab override logic works on the entire property.
        EditorGUI.BeginProperty(position, label, property);

        // Draw prefix label, returning the new rect we can draw in
        Rect fieldRect = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);

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
        GameObject currentGameObject = GuidManager.ResolveGuid(currentGuid);

        Event e = Event.current;
        if (e.type == EventType.MouseDown && e.button == 1 && position.Contains(e.mousePosition))
        {
            GenericMenu menu = CreateContextMenu(null, currentGuid);
            menu.ShowAsContext();
        }

        // Debug.Log($"Guid: {currentGuid}, Component: {currentGameObject?.name}");
        if (currentGuid != Guid.Empty && !currentGameObject)
        {
            Rect guidCompPosition = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);
            // if our reference is set, but the target isn't loaded, we display the target and the scene it is in, and provide a way to clear the reference
            float buttonWidth = 19.0f;
            guidCompPosition.xMax -= buttonWidth + BUTTON_PADDING * 2.0f;

            warningLabel.text = $" {_nameProp.stringValue} -> {sceneName}";
            warningLabel.tooltip =
                $"Reference was not found / is not loaded.\n-------------------------------------\n<b>{_nameProp.stringValue}</b> (Game Object)\n\tv\n<b>{sceneName}</b> (Scene)";

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
            Rect guidCompPosition = new Rect(fieldRect);
            guidCompPosition.x = fieldRect.xMin;
            guidCompPosition.width = fieldRect.width - BUTTON_WIDTH - BUTTON_PADDING;

            // If our object is loaded, we can simply use an object field directly
            _fieldDrawer.DrawField(guidCompPosition, GUIContent.none, currentGameObject);

            Rect settingsRect = new Rect(guidCompPosition);
            settingsRect.width = BUTTON_WIDTH;
            settingsRect.x += fieldRect.width - BUTTON_WIDTH;

            bool settingsPressed = EditorGUI.DropdownButton(settingsRect, EditorGUIUtility.IconContent("_Popup"),
                FocusType.Keyboard,
                EditorStyles.miniButtonRight);
            if (settingsPressed)
            {
                GenericMenu menu = CreateContextMenu(settingsRect, currentGuid);
                menu.ShowAsContext();
            }
        }

        if (_showExtraInfo)
        {
            EditorGUI.indentLevel++;

            Rect extraInfoRect = position;
            extraInfoRect.y += EditorGUIUtility.singleLineHeight + LINE_PADDING * 2.0f;
            extraInfoRect.xMax -= 1.0f; // Magic number for alignment.

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