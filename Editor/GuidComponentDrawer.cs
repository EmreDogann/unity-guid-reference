using UnityEditor;
#if COMPONENT_NAMES
using Sisus.ComponentNames;
#endif

[CustomEditor(typeof(GuidComponent))]
public class GuidComponentDrawer : Editor
{
    private GuidComponent _guidComp;

    // SerializedProperty here only used for remembering the state of foldout:
    // https://discussions.unity.com/t/editorguilayout-foldout-no-way-to-remember-state/36422/6
    private SerializedProperty _serializedGuidProp;
    private bool _isInit;


    private void Initialize()
    {
        if (!_isInit)
        {
            _guidComp = (GuidComponent)serializedObject.targetObject;

            _isInit = true;
        }
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        _serializedGuidProp = serializedObject.FindProperty("_guid");
        Initialize();

        if (_guidComp.IsAssetOnDisk())
        {
            EditorGUILayout.HelpBox(
                "Guid Components do not work in prefab assets.\nHowever, instances of prefabs in a scene will receive the correct guid information.",
                MessageType.Warning);
        }
        else
        {
            // Draw label
            EditorGUILayout.LabelField("GameObject GUID", _guidComp.GetGuid().ToString());

            if (_guidComp.GetComponentGUIDs().Count > 0)
            {
                _serializedGuidProp.isExpanded =
                    EditorGUILayout.Foldout(_serializedGuidProp.isExpanded, "Component GUIDs", true);
                if (_serializedGuidProp.isExpanded)
                {
                    EditorGUI.indentLevel++;
                    foreach (ComponentGuid componentGuid in _guidComp.GetComponentGUIDs())
                    {
                        string label = "";
#if COMPONENT_NAMES
                        label = $"{componentGuid.cachedComponent.GetName()}:";
#else
                        label = $"{componentGuid.cachedComponent.GetType().Name}:";
#endif
                        EditorGUILayout.LabelField(label, componentGuid.Guid.ToString());
                    }

                    EditorGUI.indentLevel--;
                }
            }
        }

        serializedObject.ApplyModifiedProperties();
    }
}